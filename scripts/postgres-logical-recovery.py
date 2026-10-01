#!/usr/bin/env python3
"""Create and verify encrypted PostgreSQL logical recovery evidence.

The caller owns the maintenance-window and Azure quiet-state checks.  This
module only reads the three approved databases through the caller's local TLS
relay and restores their logical archives into one isolated PostgreSQL 16
container.  It never changes a source database.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import heapq
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import stat
import subprocess
import tarfile
import tempfile
import time
from typing import Any, BinaryIO, Callable, Mapping
import uuid


FORMAT = "emi-postgres-logical-recovery-v1"
EVIDENCE_KIND = "VerifiedLogicalDatabaseBackup"
OWNER_LABEL = "com.emi-qms.recovery.owner"
RUN_LABEL = "com.emi-qms.recovery.run-id"
OWNER_VALUE = "postgres-logical-recovery"
TARGET_CODES = ("DIRECTORY", "CHEONGJU", "OSAN")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
IMAGE_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
NAME_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]{0,62}$")
SIMPLE_ROLE_RE = re.compile(r"^[a-z_][a-z0-9_]{0,62}$")
HOST_RE = re.compile(r"^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.postgres\.database\.azure\.com$")
AZURE_TEMP_TABLESPACE_PATH = "/mnt/pg_tmp"
AZURE_PLATFORM_GRANT = (
    "GRANT pg_signal_autovacuum_worker TO azure_pg_admin WITH ADMIN OPTION, "
    "INHERIT TRUE GRANTED BY azuresu;"
)
AZURE_PLATFORM_GRANT_REASON = "AZURE_PG16_PLATFORM_ROLE_ABSENT_FROM_LOCAL_POSTGRES_16_14"
CANONICAL_SCHEMA_METHOD = "RawSourceSchemaPsqlRoundTrip"


class RecoveryError(RuntimeError):
    """A fixed diagnostic that never embeds provider output or credentials."""


@dataclass(frozen=True)
class Target:
    code: str
    dbname: str
    host: str
    user: str
    password: str
    runtime_role: str


@dataclass(frozen=True)
class ValidatedConfig:
    archive_dir: Path
    certificate_path: Path
    private_key_path: Path
    ssl_root_cert_path: Path
    postgres_image: str
    relay_port: int
    targets: tuple[Target, ...]
    binding: dict[str, Any]
    drained_at_utc: str | None
    deadline_utc: str | None
    verification_timeout_seconds: int
    openssl_path: str
    docker_path: str
    file_hashes: dict[str, str]


def _require(condition: bool, code: str) -> None:
    if not condition:
        raise RecoveryError(code)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _certificate_binding_sha256(path: Path) -> str:
    """Match the caller's canonical PEM binding while retaining raw file hashes."""
    return hashlib.sha256(path.read_bytes().strip()).hexdigest()


def _canonical(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def _instant(value: Any, code: str) -> datetime:
    _require(isinstance(value, str) and value.endswith("Z"), code)
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise RecoveryError(code) from error
    _require(parsed.tzinfo is not None, code)
    return parsed.astimezone(timezone.utc)


def _utc(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat(timespec="microseconds").replace("+00:00", "Z")


def _private_directory(path: Path, *, create: bool = False) -> None:
    _require(path.is_absolute() and not path.is_symlink(), "ARCHIVE_DIRECTORY_INVALID")
    if create and not path.exists():
        path.mkdir(mode=0o700, parents=False)
    _require(path.is_dir() and stat.S_IMODE(path.stat().st_mode) == 0o700,
             "ARCHIVE_DIRECTORY_NOT_PRIVATE")


def _regular_file(path: Path, code: str, *, private: bool = False) -> None:
    _require(path.is_absolute() and not path.is_symlink() and path.is_file(), code)
    if private:
        _require(stat.S_IMODE(path.stat().st_mode) == 0o600, code)


def _new_private_file(path: Path) -> BinaryIO:
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    return os.fdopen(descriptor, "wb")


def _parse_config(config: Mapping[str, Any]) -> ValidatedConfig:
    _require(isinstance(config, Mapping), "CONFIGURATION_INVALID")
    required = {
        "archiveDir", "certificatePath", "privateKeyPath", "postgresImage",
        "relayPort", "sslRootCertPath", "targets", "binding"
    }
    _require(required <= set(config), "CONFIGURATION_MISSING")

    archive_dir = Path(config["archiveDir"])
    _private_directory(archive_dir, create=True)
    certificate = Path(config["certificatePath"])
    private_key = Path(config["privateKeyPath"])
    root_cert = Path(config["sslRootCertPath"])
    _regular_file(certificate, "CERTIFICATE_INVALID")
    _regular_file(private_key, "PRIVATE_KEY_INVALID", private=True)
    _regular_file(root_cert, "SSL_ROOT_CERTIFICATE_INVALID")

    image = config["postgresImage"]
    _require(isinstance(image, str) and IMAGE_RE.fullmatch(image) is not None,
             "POSTGRES_IMAGE_NOT_IMMUTABLE")
    relay_port = config["relayPort"]
    _require(type(relay_port) is int and 1 <= relay_port <= 65535, "RELAY_PORT_INVALID")

    raw_targets = config["targets"]
    _require(isinstance(raw_targets, list) and len(raw_targets) == 3, "TARGET_SET_INVALID")
    targets: list[Target] = []
    for raw in raw_targets:
        _require(isinstance(raw, Mapping), "TARGET_INVALID")
        _require({"code", "dbname", "host", "user", "password", "runtimeRole"} <= set(raw),
                 "TARGET_INVALID")
        code = raw["code"]
        dbname = raw["dbname"]
        host = raw["host"]
        user = raw["user"]
        password = raw["password"]
        runtime_role = raw["runtimeRole"]
        _require(code in TARGET_CODES, "TARGET_CODE_INVALID")
        _require(isinstance(dbname, str) and NAME_RE.fullmatch(dbname) is not None,
                 "TARGET_DATABASE_INVALID")
        _require(isinstance(host, str) and HOST_RE.fullmatch(host) is not None,
                 "TARGET_HOST_INVALID")
        _require(isinstance(user, str) and NAME_RE.fullmatch(user) is not None,
                 "TARGET_USER_INVALID")
        _require(isinstance(runtime_role, str) and NAME_RE.fullmatch(runtime_role) is not None,
                 "TARGET_RUNTIME_ROLE_INVALID")
        _require(isinstance(password, str) and password
                 and "\n" not in password and "\r" not in password and "\x00" not in password,
                 "TARGET_PASSWORD_INVALID")
        targets.append(Target(code, dbname, host, user, password, runtime_role))
    targets.sort(key=lambda target: TARGET_CODES.index(target.code))
    _require(tuple(target.code for target in targets) == TARGET_CODES, "TARGET_SET_INVALID")
    _require(len({target.dbname for target in targets}) == 3, "TARGET_DATABASE_DUPLICATE")
    _require(not ({target.dbname.lower() for target in targets}
                  & {"postgres", "template0", "template1", "azure_sys", "azure_maintenance"}),
             "TARGET_DATABASE_RESERVED")
    _require(len({target.runtime_role for target in targets}) == 3,
             "TARGET_RUNTIME_ROLE_INVALID")

    binding = config["binding"]
    _require(isinstance(binding, Mapping), "BINDING_INVALID")
    binding = json.loads(_canonical(binding))
    binding_keys = {"sourceSha", "releaseId", "serverId", "expectedHost", "apps",
                    "evidenceCertificateSha256", "evidenceKind", "logicalConfigSha256"}
    _require(set(binding) == binding_keys, "BINDING_INVALID")
    for key in binding_keys - {"apps"}:
        _require(isinstance(binding.get(key), str) and binding[key], "BINDING_INVALID")
    _require(isinstance(binding["apps"], list) and binding["apps"]
             and all(isinstance(app, str) and app for app in binding["apps"]), "BINDING_INVALID")
    _require(re.fullmatch(r"[0-9a-f]{40}", binding["sourceSha"]) is not None, "BINDING_INVALID")
    try:
        release_id = uuid.UUID(binding["releaseId"])
    except ValueError as error:
        raise RecoveryError("BINDING_INVALID") from error
    _require(release_id.int != 0, "BINDING_INVALID")
    _require(SHA256_RE.fullmatch(binding["evidenceCertificateSha256"]) is not None,
             "BINDING_INVALID")
    _require(SHA256_RE.fullmatch(binding["logicalConfigSha256"]) is not None, "BINDING_INVALID")
    _require(binding["evidenceKind"] == EVIDENCE_KIND, "EVIDENCE_KIND_INVALID")
    _require(binding["expectedHost"] == targets[0].host
             and all(target.host == binding["expectedHost"] for target in targets),
             "TARGET_HOST_BINDING_MISMATCH")
    _require(_certificate_binding_sha256(certificate) == binding["evidenceCertificateSha256"],
             "CERTIFICATE_BINDING_MISMATCH")

    drained = (_instant(config["drainedAtUtc"], "DRAIN_TIMESTAMP_INVALID")
               if "drainedAtUtc" in config else None)
    deadline = (_instant(config["deadlineUtc"], "DEADLINE_INVALID")
                if "deadlineUtc" in config else None)
    _require(drained is None or deadline is None or drained < deadline, "DEADLINE_INVALID")
    verification_timeout = config.get("verificationTimeoutSeconds", 1800)
    _require(type(verification_timeout) is int and 1 <= verification_timeout <= 1800,
             "VERIFICATION_TIMEOUT_INVALID")

    openssl_path = config.get("opensslPath", "openssl")
    docker_path = config.get("dockerPath", "docker")
    _require(isinstance(openssl_path, str) and openssl_path, "OPENSSL_INVALID")
    _require(isinstance(docker_path, str) and docker_path, "DOCKER_INVALID")
    return ValidatedConfig(
        archive_dir, certificate, private_key, root_cert, image, relay_port,
        tuple(targets), binding, _utc(drained) if drained else None,
        _utc(deadline) if deadline else None, verification_timeout, openssl_path, docker_path,
        {
            "certificate": _sha256(certificate),
            "privateKey": _sha256(private_key),
            "sslRootCertificate": _sha256(root_cert),
        })


class PostgresLogicalRecovery:
    def __init__(self, config: ValidatedConfig,
                 *, now: Callable[[], datetime] | None = None,
                 run: Callable[..., subprocess.CompletedProcess[bytes]] | None = None):
        self.config = config
        self.now = now or (lambda: datetime.now(timezone.utc))
        self.run = run or subprocess.run
        self.bridge_address = ""
        operation_budget = float(config.verification_timeout_seconds)
        if config.deadline_utc is not None:
            deadline_remaining = (
                _instant(config.deadline_utc, "DEADLINE_INVALID")
                - self.now().astimezone(timezone.utc)
            ).total_seconds()
            operation_budget = min(operation_budget, max(0.0, deadline_remaining))
        self.operation_deadline = time.monotonic() + operation_budget

    def _execute(self, arguments: list[str], code: str, *,
                 stdout_path: Path | None = None, stdin_path: Path | None = None,
                 capture: bool = False, timeout: int = 300) -> bytes:
        remaining = self.operation_deadline - time.monotonic()
        _require(remaining > 0, "RECOVERY_OPERATION_TIMEOUT")
        timeout = max(1, min(timeout, int(remaining + 0.999)))
        output: BinaryIO | int = subprocess.PIPE
        stream: BinaryIO | None = None
        stdin: BinaryIO | None = None
        try:
            if stdout_path is not None:
                stream = _new_private_file(stdout_path)
                output = stream
            if stdin_path is not None:
                stdin = stdin_path.open("rb")
            try:
                result = self.run(arguments, stdin=stdin, stdout=output, stderr=subprocess.PIPE,
                                  timeout=timeout, check=False)
            except (OSError, subprocess.SubprocessError) as error:
                raise RecoveryError(code) from error
            _require(result.returncode == 0, code)
            if stream is not None:
                stream.flush()
                os.fsync(stream.fileno())
            return result.stdout if capture and isinstance(result.stdout, bytes) else b""
        finally:
            if stdin is not None:
                stdin.close()
            if stream is not None:
                stream.close()

    def _cleanup_run(self, arguments: list[str], code: str,
                     *, timeout: int = 30) -> subprocess.CompletedProcess[bytes]:
        """Run owned-resource cleanup independently of the operation deadline."""
        try:
            return self.run(arguments, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            timeout=timeout, check=False)
        except (OSError, subprocess.SubprocessError) as error:
            raise RecoveryError(code) from error

    def _container_inspect_optional(self, name: str, format_value: str) -> str | None:
        result = self._cleanup_run([
            self.config.docker_path, "container", "inspect", "-f", format_value, name
        ], "OWNED_CONTAINER_STATE_UNKNOWN")
        if result.returncode == 0:
            _require(isinstance(result.stdout, bytes), "OWNED_CONTAINER_STATE_UNKNOWN")
            return result.stdout.decode().strip()

        existence = self._cleanup_run([
            self.config.docker_path, "container", "ls", "--all", "--quiet",
            "--filter", f"name=^/{name}$"
        ], "OWNED_CONTAINER_STATE_UNKNOWN")
        _require(existence.returncode == 0 and isinstance(existence.stdout, bytes),
                 "OWNED_CONTAINER_STATE_UNKNOWN")
        _require(not existence.stdout.strip(), "OWNED_CONTAINER_STATE_UNKNOWN")
        return None

    def _cleanup_owned_container(self, name: str, run_id: str) -> None:
        owner = self._container_inspect_optional(
            name, f'{{{{ index .Config.Labels "{OWNER_LABEL}" }}}}')
        if owner is None:
            return
        run_label = self._cleanup_run([
            self.config.docker_path, "container", "inspect", "-f",
            f'{{{{ index .Config.Labels "{RUN_LABEL}" }}}}', name
        ], "OWNED_CONTAINER_CLEANUP_UNSAFE")
        _require(run_label.returncode == 0 and isinstance(run_label.stdout, bytes)
                 and owner == OWNER_VALUE and run_label.stdout.decode().strip() == run_id,
                 "OWNED_CONTAINER_CLEANUP_UNSAFE")
        self._cleanup_run([
            self.config.docker_path, "container", "rm", "--force", name
        ], "OWNED_CONTAINER_CLEANUP_FAILED", timeout=60)
        remaining = self._container_inspect_optional(name, "{{.Id}}")
        _require(remaining is None, "OWNED_CONTAINER_CLEANUP_FAILED")

    def _execute_owned_run(self, purpose: str, run_arguments: list[str], code: str,
                           *, scope_run_id: str | None = None,
                           **execute_options: Any) -> bytes:
        run_id = scope_run_id or secrets.token_hex(12)
        _require(re.fullmatch(r"[0-9a-f]{24}", run_id) is not None,
                 "OWNED_CONTAINER_SCOPE_INVALID")
        name = f"emi-qms-logical-{purpose}-{run_id}"
        command = [
            self.config.docker_path, "run", "--rm", "--name", name,
            "--label", f"{OWNER_LABEL}={OWNER_VALUE}",
            "--label", f"{RUN_LABEL}={run_id}",
            *run_arguments,
        ]
        try:
            return self._execute(command, code, **execute_options)
        finally:
            self._cleanup_owned_container(name, run_id)

    def validate_environment(self) -> None:
        openssl = self._execute([self.config.openssl_path, "version"], "OPENSSL_UNAVAILABLE",
                                capture=True, timeout=15).decode("ascii", "replace")
        _require(openssl.startswith("OpenSSL 3."), "OPENSSL_3_REQUIRED")
        inspected = self._execute([
            self.config.docker_path, "image", "inspect", "-f", "{{.Id}}", self.config.postgres_image
        ], "POSTGRES_IMAGE_UNAVAILABLE", capture=True, timeout=30).decode().strip()
        _require(inspected == self.config.postgres_image, "POSTGRES_IMAGE_IDENTITY_MISMATCH")
        for program in ("postgres", "pg_dump", "pg_dumpall", "pg_restore", "psql"):
            version = self._execute_owned_run("preflight", [
                "--network", "none",
                self.config.postgres_image, program, "--version"
            ], "POSTGRES_VERSION_CHECK_FAILED", capture=True, timeout=30).decode("ascii", "replace")
            _require(re.search(r"(?:PostgreSQL\)|\(PostgreSQL\)) 16(?:\.|\s)", version) is not None,
                     "POSTGRES_16_REQUIRED")
        address_output = self._execute_owned_run("preflight", [
            "--network", "bridge",
            "--add-host", "host.docker.internal:host-gateway", self.config.postgres_image,
            "getent", "ahostsv4", "host.docker.internal"
        ], "DOCKER_BRIDGE_RESOLUTION_FAILED", capture=True, timeout=30).decode("ascii", "replace")
        addresses = []
        for line in address_output.splitlines():
            token = line.split(maxsplit=1)[0] if line.split() else ""
            try:
                address = ipaddress.ip_address(token)
            except ValueError:
                continue
            if address.version == 4:
                addresses.append(str(address))
        _require(bool(addresses), "DOCKER_BRIDGE_RESOLUTION_FAILED")
        self.bridge_address = addresses[0]

    def _deadline(self) -> None:
        _require(time.monotonic() < self.operation_deadline, "RECOVERY_OPERATION_TIMEOUT")
        if self.config.deadline_utc is not None:
            _require(self.now().astimezone(timezone.utc)
                     < _instant(self.config.deadline_utc, "DEADLINE_INVALID"),
                     "RECOVERY_DEADLINE_EXPIRED")

    @staticmethod
    def _password_file_value(value: str) -> str:
        return value.replace("\\", "\\\\").replace(":", "\\:")

    def _write_connection_files(self, service_path: Path, password_path: Path,
                                target: Target) -> None:
        values = {
            "host": target.host,
            "hostaddr": self.bridge_address,
            "port": str(self.config.relay_port),
            "dbname": target.dbname,
            "user": target.user,
            "sslmode": "verify-full",
            "sslrootcert": "/run/recovery/root.crt",
            "connect_timeout": "15",
            "options": "-c default_transaction_read_only=on",
        }
        with _new_private_file(service_path) as stream:
            stream.write(b"[source]\n")
            for key, value in values.items():
                stream.write(f"{key}={value}\n".encode())
            stream.flush()
            os.fsync(stream.fileno())

        # pg_dumpall chooses its own maintenance database, so the password entry
        # must cover every database while remaining bound to this host/port/user.
        fields = (target.host, str(self.config.relay_port), "*", target.user,
                  target.password)
        with _new_private_file(password_path) as stream:
            stream.write((":".join(self._password_file_value(value) for value in fields) + "\n").encode())
            stream.flush()
            os.fsync(stream.fileno())

    def _source_run_arguments(self, service: Path, password_file: Path,
                              program: str, *arguments: str) -> list[str]:
        command: list[str] = []
        if program == "psql":
            command.append("--interactive")
        command.extend([
            "--network", "bridge",
            "--add-host", "host.docker.internal:host-gateway",
            "--mount", f"type=bind,src={service},dst=/run/recovery/pg_service.conf,readonly",
            "--mount", f"type=bind,src={password_file},dst=/run/recovery/.pgpass,readonly",
            "--mount", f"type=bind,src={self.config.ssl_root_cert_path},dst=/run/recovery/root.crt,readonly",
            "--env", "PGSERVICEFILE=/run/recovery/pg_service.conf", "--env", "PGSERVICE=source",
            "--env", "PGPASSFILE=/run/recovery/.pgpass",
            self.config.postgres_image, program, *arguments,
        ])
        return command

    def _execute_source(self, service: Path, password_file: Path,
                        program: str, *arguments: str, code: str,
                        scope_run_id: str | None = None,
                        **execute_options: Any) -> bytes:
        return self._execute_owned_run(
            "source", self._source_run_arguments(service, password_file, program, *arguments),
            code, scope_run_id=scope_run_id, **execute_options)

    @staticmethod
    def _literal(value: str) -> str:
        return "'" + value.replace("'", "''") + "'"

    def _inventory_sql(self, target: Target) -> bytes:
        lines = [
            "\\set ON_ERROR_STOP on",
            "\\pset tuples_only on",
            "\\pset format unaligned",
            "select 'bootstrapRole|' || rolname from pg_roles where oid=10;",
            "select 'identity|' || coalesce((select jsonb_agg(to_jsonb(identity) order by to_jsonb(identity)::text)::text from qms_database_identity identity), '[]');",
            "select 'ledger|' || coalesce((select jsonb_agg(to_jsonb(ledger) order by version)::text from schema_migrations ledger), '[]');",
        ]
        if target.code != "DIRECTORY":
            lines.append("select 'maintenance|' || coalesce((select jsonb_agg(to_jsonb(maintenance) order by to_jsonb(maintenance)::text)::text from deployment_maintenance maintenance), '[]');")
        else:
            lines.append("select 'maintenance|[]';")
        lines.extend([
            "select format('select %L || count(*)::text from %I.%I;', 'table|' || schemaname || '.' || tablename || '|', schemaname, tablename) from pg_tables where schemaname not in ('pg_catalog','information_schema') order by schemaname, tablename \\gexec",
            "select format('select %L || encode(sha256(convert_to(to_jsonb(t)::text, %L)), %L) from %I.%I t;', 'row|' || schemaname || '.' || tablename || '|', 'UTF8', 'hex', schemaname, tablename) from pg_tables where schemaname not in ('pg_catalog','information_schema') order by schemaname, tablename \\gexec",
            "select 'sequence|' || schemaname || '.' || sequencename || '|' || jsonb_build_object('owner',sequenceowner,'type',data_type,'start',start_value,'min',min_value,'max',max_value,'increment',increment_by,'cycle',cycle,'cache',cache_size,'last',last_value)::text from pg_sequences where schemaname not in ('pg_catalog','information_schema') order by schemaname, sequencename;",
            "select format('select %L || jsonb_build_object(%L,last_value,%L,is_called)::text from %I.%I;', 'sequenceState|' || schemaname || '.' || sequencename || '|', 'last', 'isCalled', schemaname, sequencename) from pg_sequences where schemaname not in ('pg_catalog','information_schema') order by schemaname, sequencename \\gexec",
            "select 'largeObject|' || loid::text || '|' || pageno::text || '|' || encode(sha256(data),'hex') from pg_largeobject order by loid,pageno;",
        ])
        for role_target in self.config.targets:
            for database_target in self.config.targets:
                label = f"connect|{role_target.code}|{database_target.code}|"
                lines.append(
                    f"select {self._literal(label)} || case when has_database_privilege("
                    f"{self._literal(role_target.runtime_role)}, {self._literal(database_target.dbname)}, 'CONNECT') "
                    "then 'true' else 'false' end;")
        return ("\n".join(lines) + "\n").encode()

    @staticmethod
    def _canonicalize_inventory(path: Path, *, chunk_bytes: int = 8 * 1024 * 1024) -> None:
        """External-sort bounded row fingerprints so heap order cannot affect proof."""
        chunks: list[Path] = []
        lines: list[bytes] = []
        size = 0

        def flush() -> None:
            nonlocal lines, size
            if not lines:
                return
            chunk = path.with_name(f".{path.name}.sort-{len(chunks)}")
            with _new_private_file(chunk) as stream:
                for line in sorted(lines):
                    stream.write(line)
                stream.flush()
                os.fsync(stream.fileno())
            chunks.append(chunk)
            lines = []
            size = 0

        try:
            with path.open("rb") as source:
                for raw_line in source:
                    line = raw_line.rstrip(b"\r\n") + b"\n"
                    lines.append(line)
                    size += len(line)
                    if size >= chunk_bytes:
                        flush()
            flush()
            canonical = path.with_name(f".{path.name}.canonical")
            streams = [chunk.open("rb") for chunk in chunks]
            try:
                with _new_private_file(canonical) as output:
                    for line in heapq.merge(*streams):
                        output.write(line)
                    output.flush()
                    os.fsync(output.fileno())
            finally:
                for stream in streams:
                    stream.close()
            os.replace(canonical, path)
        finally:
            for chunk in chunks:
                chunk.unlink(missing_ok=True)

    @staticmethod
    def _normalized_schema_sha256(path: Path) -> str:
        """Ignore only pg_dump-generated version and random restriction metadata."""
        digest = hashlib.sha256()
        for line in path.read_bytes().splitlines(keepends=True):
            if line.startswith(b"-- Dumped from database version "):
                digest.update(b"-- Dumped from database version <normalized>\n")
            elif line.startswith(b"-- Dumped by pg_dump version "):
                digest.update(b"-- Dumped by pg_dump version <normalized>\n")
            elif line.startswith(b"\\restrict "):
                digest.update(b"\\restrict <normalized>\n")
            elif line.startswith(b"\\unrestrict "):
                digest.update(b"\\unrestrict <normalized>\n")
            else:
                digest.update(line)
        return digest.hexdigest()

    def _source_files(self, work: Path) -> dict[str, dict[str, str]]:
        artifacts: dict[str, dict[str, str]] = {}
        globals_path = work / "globals.sql"
        bootstrap_roles: list[str] = []
        for index, target in enumerate(self.config.targets):
            self._deadline()
            target_dir = work / target.code
            target_dir.mkdir(mode=0o700)
            service = work / f"service-{target.code}"
            password_file = work / f"password-{target.code}"
            self._write_connection_files(service, password_file, target)
            try:
                dump = target_dir / "database.dump"
                schema = target_dir / "schema.sql"
                inventory = target_dir / "inventory.txt"
                self._execute_source(service, password_file, "pg_dump", "--create",
                                     "--format=custom", "--no-password", code="PG_DUMP_FAILED",
                                     stdout_path=dump, timeout=1800)
                self._execute_source(service, password_file, "pg_dump", "--create",
                                     "--schema-only", "--no-password", code="SCHEMA_DUMP_FAILED",
                                     stdout_path=schema, timeout=900)
                if index == 0:
                    self._execute_source(service, password_file, "pg_dumpall", "--globals-only",
                                         "--no-role-passwords", "--no-password",
                                         code="GLOBALS_DUMP_FAILED", stdout_path=globals_path,
                                         timeout=300)
                    _require(re.search(rb"\bPASSWORD\b", globals_path.read_bytes(), re.IGNORECASE) is None,
                             "GLOBALS_CONTAIN_ROLE_PASSWORD")
                inventory_sql = target_dir / "inventory.sql"
                with _new_private_file(inventory_sql) as stream:
                    stream.write(self._inventory_sql(target))
                self._execute_source(service, password_file, "psql", "--no-password",
                                     "--no-psqlrc", "--quiet",
                                     code="SOURCE_INVENTORY_FAILED", stdout_path=inventory,
                                     stdin_path=inventory_sql, timeout=900)
                self._canonicalize_inventory(inventory)
                bootstrap_roles.append(self._bootstrap_role(inventory))
                self._validate_identity(target, inventory)
                self._validate_connect_matrix(inventory)
                artifacts[target.code] = {
                    "databaseDumpSha256": _sha256(dump),
                    "schemaSha256": _sha256(schema),
                    "inventorySha256": _sha256(inventory),
                    "rawSourceProofSha256": hashlib.sha256(
                        (self._normalized_schema_sha256(schema) + _sha256(inventory)).encode()
                    ).hexdigest(),
                }
            finally:
                service.unlink(missing_ok=True)
                password_file.unlink(missing_ok=True)
        _require(len(set(bootstrap_roles)) == 1, "SOURCE_BOOTSTRAP_ROLE_MISMATCH")
        _require(bootstrap_roles[0] not in {target.runtime_role for target in self.config.targets},
                 "SOURCE_BOOTSTRAP_ROLE_CONFLICT")
        artifacts["GLOBALS"] = {
            "globalsSha256": _sha256(globals_path),
            "bootstrapRole": bootstrap_roles[0],
        }
        return artifacts

    def _verify_source_unchanged(self, work: Path,
                                 artifacts: Mapping[str, Mapping[str, str]]) -> None:
        """Re-read all source proofs after the complete backup and restore cycle."""
        for target in self.config.targets:
            self._deadline()
            service = work / f"final-service-{target.code}"
            password_file = work / f"final-password-{target.code}"
            self._write_connection_files(service, password_file, target)
            try:
                schema = work / target.code / "final-source-schema.sql"
                inventory = work / target.code / "final-source-inventory.txt"
                self._execute_source(service, password_file, "pg_dump", "--create",
                                     "--schema-only", "--no-password",
                                     code="FINAL_SOURCE_SCHEMA_READ_FAILED", stdout_path=schema,
                                     timeout=900)
                inventory_sql = work / target.code / "final-source-inventory.sql"
                with _new_private_file(inventory_sql) as stream:
                    stream.write(self._inventory_sql(target))
                self._execute_source(service, password_file, "psql", "--no-password",
                                     "--no-psqlrc", "--quiet",
                                     code="FINAL_SOURCE_INVENTORY_FAILED", stdout_path=inventory,
                                     stdin_path=inventory_sql, timeout=900)
                self._canonicalize_inventory(inventory)
                _require(self._bootstrap_role(inventory)
                         == artifacts["GLOBALS"]["bootstrapRole"],
                         "SOURCE_BOOTSTRAP_ROLE_CHANGED")
                self._validate_identity(target, inventory)
                self._validate_connect_matrix(inventory)
                proof = hashlib.sha256(
                    (self._normalized_schema_sha256(schema) + _sha256(inventory)).encode()
                ).hexdigest()
                _require(proof == artifacts[target.code]["rawSourceProofSha256"],
                         "SOURCE_CHANGED_DURING_BACKUP")
            finally:
                service.unlink(missing_ok=True)
                password_file.unlink(missing_ok=True)

    @staticmethod
    def _bootstrap_role(inventory: Path) -> str:
        values = [line.split("|", 1)[1] for line in inventory.read_text().splitlines()
                  if line.startswith("bootstrapRole|")]
        _require(len(values) == 1 and SIMPLE_ROLE_RE.fullmatch(values[0]) is not None,
                 "SOURCE_BOOTSTRAP_ROLE_INVALID")
        return values[0]

    def _prepare_globals_restore(self, payload: Path,
                                 bootstrap_role: str) -> tuple[Path, bool]:
        globals_path = payload / "globals.sql"
        lines = globals_path.read_text(encoding="utf-8").splitlines(keepends=True)
        bootstrap_create = f"CREATE ROLE {bootstrap_role};"
        matches = [index for index, line in enumerate(lines)
                   if line.rstrip("\r\n") == bootstrap_create]
        _require(len(matches) == 1, "GLOBALS_BOOTSTRAP_ROLE_CREATE_INVALID")

        sql_identifier = r'(?:[A-Za-z_][A-Za-z0-9_$]*|"(?:[^"]|"")+")'
        tablespace_pattern = re.compile(
            rf"^CREATE TABLESPACE {sql_identifier} OWNER {sql_identifier} "
            rf"LOCATION '{re.escape(AZURE_TEMP_TABLESPACE_PATH)}';$")
        tablespace_lines = [line.rstrip("\r\n") for line in lines
                            if line.startswith("CREATE TABLESPACE ")]
        _require(all(tablespace_pattern.fullmatch(line) is not None
                     for line in tablespace_lines), "GLOBALS_TABLESPACE_UNSUPPORTED")

        compatibility = self._platform_compatibility(payload)
        excluded_statements = {
            item["excludedStatement"] for item in compatibility
        }

        destination = payload / "globals.restore.sql"
        with _new_private_file(destination) as stream:
            for index, line in enumerate(lines):
                if index != matches[0] and line.rstrip("\r\n") not in excluded_statements:
                    stream.write(line.encode("utf-8"))
            stream.flush()
            os.fsync(stream.fileno())
        return destination, bool(tablespace_lines)

    @staticmethod
    def _platform_compatibility(payload: Path) -> list[dict[str, str]]:
        globals_path = payload / "globals.sql"
        text = globals_path.read_text(encoding="utf-8")
        occurrence_count = text.count("pg_signal_autovacuum_worker")
        if occurrence_count == 0:
            return []
        exact_lines = [line for line in text.splitlines() if line == AZURE_PLATFORM_GRANT]
        _require(occurrence_count == 1 and len(exact_lines) == 1,
                 "GLOBALS_PLATFORM_COMPATIBILITY_INVALID")
        role_token = b"pg_signal_autovacuum_worker"
        _require(all(role_token not in (payload / code / "schema.sql").read_bytes()
                     for code in TARGET_CODES),
                 "SCHEMA_PLATFORM_ROLE_DEPENDENCY_UNSUPPORTED")
        return [{
            "excludedStatement": AZURE_PLATFORM_GRANT,
            "sourceGlobalsSha256": _sha256(globals_path),
            "reason": AZURE_PLATFORM_GRANT_REASON,
        }]

    def _validate_identity(self, target: Target, inventory: Path) -> None:
        identity_line = next((line for line in inventory.read_text().splitlines() if line.startswith("identity|")), None)
        _require(identity_line is not None, "DATABASE_IDENTITY_MISSING")
        try:
            identities = json.loads(identity_line.split("|", 1)[1])
        except json.JSONDecodeError as error:
            raise RecoveryError("DATABASE_IDENTITY_INVALID") from error
        _require(isinstance(identities, list) and len(identities) == 1, "DATABASE_IDENTITY_INVALID")
        identity = identities[0]
        expected_kind = "directory" if target.code == "DIRECTORY" else "business"
        expected_code = None if target.code == "DIRECTORY" else target.code
        expected_schema = "0001_business_unit_directory" if target.code == "DIRECTORY" else "0086_business_unit_database_identity"
        _require(identity.get("singleton") is True and identity.get("database_kind") == expected_kind
                 and identity.get("business_unit_code") == expected_code
                 and identity.get("schema_contract") == expected_schema,
                 "DATABASE_IDENTITY_MISMATCH")

    def _validate_connect_matrix(self, inventory: Path) -> None:
        observed = {}
        for line in inventory.read_text().splitlines():
            if line.startswith("connect|"):
                _, role_code, database_code, value = line.split("|", 3)
                observed[(role_code, database_code)] = value
        for role in TARGET_CODES:
            for database in TARGET_CODES:
                expected = "true" if role == database else "false"
                _require(observed.get((role, database)) == expected,
                         "DATABASE_CONNECT_ISOLATION_MISMATCH")

    def _bundle(self, work: Path, bundle: Path) -> None:
        allowed = [work / "globals.sql"]
        for code in TARGET_CODES:
            allowed.extend(work / code / name for name in (
                "database.dump", "schema.sql", "canonical-schema.sql", "inventory.txt"))
        with _new_private_file(bundle) as output:
            with tarfile.open(fileobj=output, mode="w") as archive:
                for path in allowed:
                    archive.add(path, arcname=path.relative_to(work), recursive=False)
            output.flush()
            os.fsync(output.fileno())

    def _encrypt(self, source: Path, destination: Path) -> None:
        with _new_private_file(destination):
            pass
        self._execute([
            self.config.openssl_path, "cms", "-encrypt", "-binary", "-keyid", "-aes-256-gcm",
            "-outform", "DER", "-in", str(source), "-out", str(destination),
            "-recip", str(self.config.certificate_path), "-keyopt", "rsa_padding_mode:oaep",
            "-keyopt", "rsa_oaep_md:sha256"
        ], "EVIDENCE_ENCRYPTION_FAILED", timeout=120)
        _require(destination.stat().st_size > 0, "EVIDENCE_ENCRYPTION_FAILED")

    def _decrypt(self, source: Path, destination: Path) -> None:
        with _new_private_file(destination):
            pass
        self._execute([
            self.config.openssl_path, "cms", "-decrypt", "-binary", "-inform", "DER",
            "-in", str(source), "-out", str(destination), "-recip", str(self.config.certificate_path),
            "-inkey", str(self.config.private_key_path)
        ], "EVIDENCE_DECRYPTION_FAILED", timeout=120)

    def _extract(self, bundle: Path, destination: Path) -> None:
        destination.mkdir(mode=0o700)
        expected = {"globals.sql"}
        expected.update(f"{code}/{name}" for code in TARGET_CODES
                        for name in (
                            "database.dump", "schema.sql", "canonical-schema.sql", "inventory.txt"))
        with tarfile.open(bundle, "r") as archive:
            members = archive.getmembers()
            _require({member.name for member in members} == expected
                     and all(member.isfile() and not member.issym() and not member.islnk() for member in members),
                     "ARCHIVE_CONTENT_INVALID")
            for member in members:
                target = destination / member.name
                target.parent.mkdir(mode=0o700, exist_ok=True)
                source = archive.extractfile(member)
                _require(source is not None, "ARCHIVE_CONTENT_INVALID")
                with _new_private_file(target) as output:
                    shutil.copyfileobj(source, output)

    def _container_inspect(self, name: str, format_value: str, code: str) -> str:
        return self._execute([
            self.config.docker_path, "container", "inspect", "-f", format_value, name
        ], code, capture=True, timeout=30).decode().strip()

    def _assert_owned_offline_container(self, name: str, run_id: str) -> None:
        _require(self._container_inspect(
            name, f'{{{{ index .Config.Labels "{OWNER_LABEL}" }}}}',
            "RESTORE_CONTAINER_OWNERSHIP_FAILED") == OWNER_VALUE,
            "RESTORE_CONTAINER_OWNERSHIP_FAILED")
        _require(self._container_inspect(
            name, f'{{{{ index .Config.Labels "{RUN_LABEL}" }}}}',
            "RESTORE_CONTAINER_OWNERSHIP_FAILED") == run_id,
            "RESTORE_CONTAINER_OWNERSHIP_FAILED")
        _require(self._container_inspect(
            name, "{{.Image}}", "RESTORE_CONTAINER_IMAGE_FAILED")
            == self.config.postgres_image, "RESTORE_CONTAINER_IMAGE_FAILED")
        _require(self._container_inspect(
            name, "{{.HostConfig.NetworkMode}}", "RESTORE_CONTAINER_NETWORK_FAILED") == "none",
            "RESTORE_CONTAINER_NETWORK_FAILED")

    def _start_restore_cluster(self, payload: Path) -> tuple[str, str, str]:
        bootstrap_roles = {
            self._bootstrap_role(payload / target.code / "inventory.txt")
            for target in self.config.targets
        }
        _require(len(bootstrap_roles) == 1, "ARCHIVE_BOOTSTRAP_ROLE_MISMATCH")
        bootstrap_role = next(iter(bootstrap_roles))
        globals_restore, needs_azure_temp_tablespace = self._prepare_globals_restore(
            payload, bootstrap_role)
        run_id = secrets.token_hex(12)
        name = f"emi-qms-logical-recovery-{run_id}"
        _require(self._container_inspect_optional(name, "{{.Id}}") is None,
                 "RESTORE_CONTAINER_SCOPE_EXISTS")
        scope_claimed = False
        try:
            scope_claimed = True
            run_arguments = [
                self.config.docker_path, "run", "--detach", "--name", name,
                "--label", f"{OWNER_LABEL}={OWNER_VALUE}", "--label", f"{RUN_LABEL}={run_id}",
                "--network", "none", "--tmpfs",
                "/var/lib/postgresql/data:rw,nosuid,nodev,noexec,size=6g",
            ]
            if needs_azure_temp_tablespace:
                run_arguments.extend([
                    "--tmpfs", f"{AZURE_TEMP_TABLESPACE_PATH}:rw,nosuid,nodev,noexec,size=2g"
                ])
            run_arguments.extend([
                "--env", "POSTGRES_HOST_AUTH_METHOD=trust", "--env", f"POSTGRES_USER={bootstrap_role}",
                "--env", "POSTGRES_DB=postgres", self.config.postgres_image
            ])
            self._execute(run_arguments, "RESTORE_CONTAINER_CREATE_FAILED",
                          capture=True, timeout=60)
            self._assert_owned_offline_container(name, run_id)
            for _ in range(60):
                self._deadline()
                remaining = self.operation_deadline - time.monotonic()
                wait_timeout = max(1, min(10, int(remaining + 0.999)))
                try:
                    result = self.run([
                        self.config.docker_path, "exec", name, "pg_isready", "--username", bootstrap_role,
                        "--dbname", "postgres"
                    ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                        timeout=wait_timeout, check=False)
                except (OSError, subprocess.SubprocessError) as error:
                    raise RecoveryError("RESTORE_CONTAINER_STATE_UNKNOWN") from error
                if result.returncode == 0:
                    break
                time.sleep(1)
            else:
                raise RecoveryError("RESTORE_CONTAINER_NOT_READY")

            if needs_azure_temp_tablespace:
                self._execute([
                    self.config.docker_path, "exec", "--user", "root", name,
                    "chown", "postgres:postgres", AZURE_TEMP_TABLESPACE_PATH
                ], "RESTORE_TABLESPACE_PREPARATION_FAILED", timeout=30)

            self._execute([
                self.config.docker_path, "exec", "--interactive", name, "psql", "--no-psqlrc",
                "--quiet", "--set", "ON_ERROR_STOP=1", "--username", bootstrap_role, "--dbname", "postgres"
            ], "GLOBALS_RESTORE_FAILED", stdin_path=globals_restore, timeout=300)
            return name, run_id, bootstrap_role
        except Exception:
            if scope_claimed:
                self._cleanup_owned_container(name, run_id)
            raise

    def _prepare_canonical_cluster(self, payload: Path) -> tuple[str, str, str]:
        scope = self._start_restore_cluster(payload)
        name, run_id, bootstrap_role = scope
        try:
            for target in self.config.targets:
                self._execute([
                    self.config.docker_path, "exec", "--interactive", name, "psql",
                    "--no-psqlrc", "--quiet", "--set", "ON_ERROR_STOP=1",
                    "--username", bootstrap_role, "--dbname", "postgres"
                ], "CANONICAL_SCHEMA_RESTORE_FAILED",
                    stdin_path=payload / target.code / "schema.sql", timeout=900)
                canonical = payload / target.code / "canonical-schema.sql"
                self._execute([
                    self.config.docker_path, "exec", name, "pg_dump", "--create", "--schema-only",
                    "--no-password", "--username", bootstrap_role, "--dbname", target.dbname
                ], "CANONICAL_SCHEMA_READ_FAILED", stdout_path=canonical, timeout=900)

            for target in self.config.targets:
                self._assert_owned_offline_container(name, run_id)
                self._execute([
                    self.config.docker_path, "exec", name, "dropdb", "--if-exists", "--force",
                    "--username", bootstrap_role, target.dbname
                ], "CANONICAL_DATABASE_CLEANUP_FAILED", timeout=60)
            database_literals = ",".join(self._literal(target.dbname)
                                         for target in self.config.targets)
            remaining = self._execute([
                self.config.docker_path, "exec", name, "psql", "--no-psqlrc", "--tuples-only",
                "--no-align", "--username", bootstrap_role, "--dbname", "postgres",
                "--command", f"select count(*) from pg_database where datname in ({database_literals});"
            ], "CANONICAL_DATABASE_CLEANUP_FAILED", capture=True, timeout=30).decode().strip()
            _require(remaining == "0", "CANONICAL_DATABASE_CLEANUP_FAILED")
            self._assert_owned_offline_container(name, run_id)
            return scope
        except Exception:
            self._cleanup_owned_container(name, run_id)
            raise

    def _restore_payload(self, payload: Path,
                         scope: tuple[str, str, str] | None = None) -> dict[str, str]:
        owns_scope = scope is None
        if scope is None:
            scope = self._start_restore_cluster(payload)
        name, run_id, bootstrap_role = scope
        self._assert_owned_offline_container(name, run_id)
        try:

            proof: dict[str, str] = {}
            for target in self.config.targets:
                self._execute([
                    self.config.docker_path, "exec", "--interactive", name, "pg_restore",
                    "--exit-on-error", "--create", "--no-password", "--username", bootstrap_role,
                    "--dbname", "postgres"
                ], "DATABASE_RESTORE_FAILED", stdin_path=payload / target.code / "database.dump", timeout=1800)
            for target in self.config.targets:
                schema = payload / target.code / "restored-schema.sql"
                self._execute([
                    self.config.docker_path, "exec", name, "pg_dump", "--create", "--schema-only",
                    "--no-password", "--username", bootstrap_role, "--dbname", target.dbname
                ], "RESTORED_SCHEMA_READ_FAILED", stdout_path=schema, timeout=900)
                inventory_sql = payload / target.code / "restored-inventory.sql"
                with _new_private_file(inventory_sql) as stream:
                    stream.write(self._inventory_sql(target))
                inventory = payload / target.code / "restored-inventory.txt"
                self._execute([
                    self.config.docker_path, "exec", "--interactive", name, "psql", "--no-psqlrc",
                    "--quiet", "--username", bootstrap_role, "--dbname", target.dbname
                ], "RESTORED_INVENTORY_FAILED", stdout_path=inventory,
                              stdin_path=inventory_sql, timeout=900)
                self._canonicalize_inventory(inventory)
                self._validate_identity(target, inventory)
                self._validate_connect_matrix(inventory)
                _require(self._normalized_schema_sha256(schema)
                         == self._normalized_schema_sha256(
                             payload / target.code / "canonical-schema.sql"),
                         "RESTORED_SCHEMA_MISMATCH")
                _require(_sha256(inventory) == _sha256(payload / target.code / "inventory.txt"),
                         "RESTORED_INVENTORY_MISMATCH")
                proof[target.code] = hashlib.sha256(
                    (self._normalized_schema_sha256(schema) + _sha256(inventory)).encode()
                ).hexdigest()
            return proof
        finally:
            if owns_scope:
                self._cleanup_owned_container(name, run_id)

    @staticmethod
    def _publish_no_replace(source: Path, destination: Path) -> None:
        try:
            os.link(source, destination)
        except FileExistsError as error:
            raise RecoveryError("EVIDENCE_ALREADY_EXISTS") from error
        source.unlink()

    def _files_unchanged(self) -> None:
        observed = {
            "certificate": _sha256(self.config.certificate_path),
            "privateKey": _sha256(self.config.private_key_path),
            "sslRootCertificate": _sha256(self.config.ssl_root_cert_path),
        }
        _require(observed == self.config.file_hashes, "CONFIGURATION_FILE_CHANGED")

    def create(self) -> dict[str, Any]:
        _require(self.config.drained_at_utc is not None and self.config.deadline_utc is not None,
                 "RECOVERY_WINDOW_REQUIRED")
        self.validate_environment()
        self._deadline()
        started = _utc(self.now())
        work = Path(tempfile.mkdtemp(prefix=".logical-recovery-", dir=self.config.archive_dir))
        work.chmod(0o700)
        cluster_scope: tuple[str, str, str] | None = None
        try:
            artifacts = self._source_files(work)
            cluster_scope = self._prepare_canonical_cluster(work)
            for target in self.config.targets:
                canonical = work / target.code / "canonical-schema.sql"
                canonical_expected = self._normalized_schema_sha256(canonical)
                artifacts[target.code]["canonicalSchemaSha256"] = _sha256(canonical)
                artifacts[target.code]["canonicalExpectedSchemaSha256"] = canonical_expected
                artifacts[target.code]["sourceProofSha256"] = hashlib.sha256(
                    (canonical_expected + artifacts[target.code]["inventorySha256"]).encode()
                ).hexdigest()
            bundle = work / "payload.tar"
            self._bundle(work, bundle)
            encrypted = work / "payload.cms"
            self._encrypt(bundle, encrypted)
            decrypted = work / "payload.decrypted.tar"
            self._decrypt(encrypted, decrypted)
            _require(_sha256(bundle) == _sha256(decrypted), "CIPHERTEXT_ROUND_TRIP_MISMATCH")
            payload = work / "payload"
            self._extract(decrypted, payload)
            proof = self._restore_payload(payload, cluster_scope)
            _require(all(proof[target.code] == artifacts[target.code]["sourceProofSha256"]
                         for target in self.config.targets), "RESTORED_SOURCE_PROOF_MISMATCH")
            self._cleanup_owned_container(cluster_scope[0], cluster_scope[1])
            cluster_scope = None
            self._verify_source_unchanged(work, artifacts)
            completed = _utc(self.now())
            release = re.sub(r"[^A-Za-z0-9_-]", "-", self.config.binding["releaseId"])
            archive_path = self.config.archive_dir / f"{release}.logical-backup.cms"
            manifest_path = self.config.archive_dir / f"{release}.logical-manifest.cms"
            _require(not archive_path.exists() and not archive_path.is_symlink()
                     and not manifest_path.exists() and not manifest_path.is_symlink(),
                     "EVIDENCE_ALREADY_EXISTS")
            archive_hash = _sha256(encrypted)
            databases = []
            for target in self.config.targets:
                databases.append({
                    "code": target.code,
                    "databaseDumpSha256": artifacts[target.code]["databaseDumpSha256"],
                    "schemaSha256": artifacts[target.code]["schemaSha256"],
                    "canonicalSchemaSha256": artifacts[target.code]["canonicalSchemaSha256"],
                    "canonicalExpectedSchemaSha256": artifacts[target.code]["canonicalExpectedSchemaSha256"],
                    "inventorySha256": artifacts[target.code]["inventorySha256"],
                    "rawSourceProofSha256": artifacts[target.code]["rawSourceProofSha256"],
                    "sourceProofSha256": artifacts[target.code]["sourceProofSha256"],
                    "restoreProofSha256": proof[target.code],
                })
            core = {
                "schemaVersion": 1,
                "format": FORMAT,
                "binding": self.config.binding,
                "drainedAtUtc": self.config.drained_at_utc,
                "startedAtUtc": started,
                "completedAtUtc": completed,
                "evidenceKind": EVIDENCE_KIND,
                "postgresImage": self.config.postgres_image,
                "bootstrapRole": artifacts["GLOBALS"]["bootstrapRole"],
                "canonicalSchemaMethod": CANONICAL_SCHEMA_METHOD,
                "platformCompatibility": self._platform_compatibility(payload),
                "archive": {"path": str(archive_path), "sha256": archive_hash},
                "globalsSha256": artifacts["GLOBALS"]["globalsSha256"],
                "databases": databases,
                "rolePasswordsRestored": False,
                "credentialReconnectionRequired": True,
            }
            manifest_plain = work / "manifest.json"
            with _new_private_file(manifest_plain) as stream:
                stream.write(_canonical(core))
                stream.flush()
                os.fsync(stream.fileno())
            manifest_encrypted = work / "manifest.cms"
            self._encrypt(manifest_plain, manifest_encrypted)
            manifest_decrypted = work / "manifest.decrypted.json"
            self._decrypt(manifest_encrypted, manifest_decrypted)
            _require(manifest_decrypted.read_bytes() == manifest_plain.read_bytes(),
                     "MANIFEST_ROUND_TRIP_MISMATCH")
            self._files_unchanged()
            archive_published = False
            try:
                self._publish_no_replace(encrypted, archive_path)
                archive_published = True
                self._publish_no_replace(manifest_encrypted, manifest_path)
                archive_path.chmod(0o600)
                manifest_path.chmod(0o600)
            except Exception:
                if archive_published:
                    archive_path.unlink(missing_ok=True)
                raise
            directory = os.open(self.config.archive_dir, os.O_RDONLY)
            try:
                os.fsync(directory)
            finally:
                os.close(directory)
            return core | {"manifestCiphertext": {
                "path": str(manifest_path), "sha256": _sha256(manifest_path)}}
        finally:
            try:
                if cluster_scope is not None:
                    self._cleanup_owned_container(cluster_scope[0], cluster_scope[1])
            finally:
                shutil.rmtree(work, ignore_errors=False)

    def verify(self, manifest: Mapping[str, Any]) -> None:
        _require(self.config.drained_at_utc is not None, "DRAIN_TIMESTAMP_REQUIRED")
        self.validate_environment()
        _require(isinstance(manifest, Mapping) and isinstance(manifest.get("manifestCiphertext"), Mapping),
                 "MANIFEST_INVALID")
        core = {key: value for key, value in manifest.items() if key != "manifestCiphertext"}
        ciphertext_data = manifest["manifestCiphertext"]
        manifest_path = Path(ciphertext_data.get("path", ""))
        archive_data = core.get("archive")
        _require(isinstance(archive_data, Mapping), "MANIFEST_INVALID")
        archive_path = Path(archive_data.get("path", ""))
        for path, expected in ((manifest_path, ciphertext_data.get("sha256")),
                               (archive_path, archive_data.get("sha256"))):
            _regular_file(path, "EVIDENCE_NOT_PRIVATE", private=True)
            _require(path.parent == self.config.archive_dir and SHA256_RE.fullmatch(str(expected)) is not None
                     and _sha256(path) == expected, "EVIDENCE_HASH_MISMATCH")
        _require(core.get("format") == FORMAT and core.get("schemaVersion") == 1
                 and core.get("binding") == self.config.binding
                 and core.get("drainedAtUtc") == self.config.drained_at_utc
                 and core.get("evidenceKind") == EVIDENCE_KIND
                 and core.get("postgresImage") == self.config.postgres_image
                 and core.get("canonicalSchemaMethod") == CANONICAL_SCHEMA_METHOD
                 and core.get("rolePasswordsRestored") is False
                 and core.get("credentialReconnectionRequired") is True,
                 "MANIFEST_BINDING_MISMATCH")
        started = _instant(core.get("startedAtUtc"), "MANIFEST_TIMESTAMP_INVALID")
        completed = _instant(core.get("completedAtUtc"), "MANIFEST_TIMESTAMP_INVALID")
        _require(started <= completed and _instant(self.config.drained_at_utc, "DRAIN_TIMESTAMP_INVALID") <= started,
                 "MANIFEST_TIMESTAMP_INVALID")
        work = Path(tempfile.mkdtemp(prefix=".logical-verify-", dir=self.config.archive_dir))
        work.chmod(0o700)
        try:
            plain_manifest = work / "manifest.json"
            self._decrypt(manifest_path, plain_manifest)
            _require(plain_manifest.read_bytes() == _canonical(core), "MANIFEST_CIPHERTEXT_MISMATCH")
            bundle = work / "payload.tar"
            self._decrypt(archive_path, bundle)
            payload = work / "payload"
            self._extract(bundle, payload)
            databases = core.get("databases")
            _require(isinstance(databases, list) and [item.get("code") for item in databases] == list(TARGET_CODES),
                     "MANIFEST_DATABASE_SET_INVALID")
            for item in databases:
                code = item["code"]
                expected = {
                    "databaseDumpSha256": _sha256(payload / code / "database.dump"),
                    "schemaSha256": _sha256(payload / code / "schema.sql"),
                    "canonicalSchemaSha256": _sha256(
                        payload / code / "canonical-schema.sql"),
                    "canonicalExpectedSchemaSha256": self._normalized_schema_sha256(
                        payload / code / "canonical-schema.sql"),
                    "inventorySha256": _sha256(payload / code / "inventory.txt"),
                    "rawSourceProofSha256": hashlib.sha256(
                        (self._normalized_schema_sha256(payload / code / "schema.sql")
                         + _sha256(payload / code / "inventory.txt")).encode()
                    ).hexdigest(),
                }
                expected["sourceProofSha256"] = hashlib.sha256(
                    (expected["canonicalExpectedSchemaSha256"]
                     + expected["inventorySha256"]).encode()
                ).hexdigest()
                _require(all(item.get(key) == value for key, value in expected.items()),
                         "ARCHIVE_ARTIFACT_HASH_MISMATCH")
            _require(core.get("globalsSha256") == _sha256(payload / "globals.sql"),
                     "ARCHIVE_ARTIFACT_HASH_MISMATCH")
            _require(core.get("platformCompatibility")
                     == self._platform_compatibility(payload),
                     "MANIFEST_PLATFORM_COMPATIBILITY_MISMATCH")
            bootstrap_roles = {
                self._bootstrap_role(payload / target.code / "inventory.txt")
                for target in self.config.targets
            }
            _require(len(bootstrap_roles) == 1
                     and core.get("bootstrapRole") == next(iter(bootstrap_roles)),
                     "MANIFEST_BOOTSTRAP_ROLE_MISMATCH")
            _require(all(item.get("restoreProofSha256") == item.get("sourceProofSha256")
                         for item in databases),
                     "RESTORE_PROOF_MISMATCH")
            self._files_unchanged()
        finally:
            shutil.rmtree(work, ignore_errors=False)


def validate_config(config: Mapping[str, Any]) -> ValidatedConfig:
    validated = _parse_config(config)
    PostgresLogicalRecovery(validated).validate_environment()
    return validated


def create_backup(config: Mapping[str, Any]) -> dict[str, Any]:
    validated = _parse_config(config)
    return PostgresLogicalRecovery(validated).create()


def verify_backup(manifest: Mapping[str, Any], config: Mapping[str, Any]) -> None:
    validated = _parse_config(config)
    PostgresLogicalRecovery(validated).verify(manifest)
