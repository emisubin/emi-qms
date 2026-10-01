#!/usr/bin/env python3
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location(
    "postgres_logical_recovery", Path(__file__).with_name("postgres-logical-recovery.py"))
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)


class LogicalRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="logical-recovery-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.archive = self.root / "archive"
        self.archive.mkdir(mode=0o700)
        self.certificate = self.root / "recipient.pem"
        self.private_key = self.root / "recipient.key"
        self.root_certificate = self.root / "root.pem"
        self.certificate.write_text("synthetic public certificate\n")
        self.private_key.write_text("synthetic private key\n")
        self.private_key.chmod(0o600)
        self.root_certificate.write_text("synthetic root certificate\n")
        certificate_hash = module._certificate_binding_sha256(self.certificate)
        self.now = datetime.now(timezone.utc)
        self.config = {
            "archiveDir": str(self.archive),
            "certificatePath": str(self.certificate),
            "privateKeyPath": str(self.private_key),
            "sslRootCertPath": str(self.root_certificate),
            "postgresImage": "sha256:" + "1" * 64,
            "relayPort": 15432,
            "verificationTimeoutSeconds": 60,
            "binding": {
                "sourceSha": "2" * 40,
                "releaseId": "11111111-1111-1111-1111-111111111111",
                "serverId": "/subscriptions/synthetic/servers/pms",
                "expectedHost": "pms.postgres.database.azure.com",
                "apps": ["backend", "frontend"],
                "evidenceCertificateSha256": certificate_hash,
                "evidenceKind": module.EVIDENCE_KIND,
                "logicalConfigSha256": "3" * 64,
            },
            "targets": [
                self.target("DIRECTORY", "directory_db", "directory_runtime"),
                self.target("CHEONGJU", "cheongju_db", "cheongju_runtime"),
                self.target("OSAN", "osan_db", "osan_runtime"),
            ],
        }

    @staticmethod
    def target(code, database, runtime_role):
        return {
            "code": code,
            "dbname": database,
            "host": "pms.postgres.database.azure.com",
            "user": "backup_reader",
            "password": "synthetic password:'quoted'\\with-special-value",
            "runtimeRole": runtime_role,
        }

    def with_window(self):
        value = dict(self.config)
        value["drainedAtUtc"] = module._utc(self.now - timedelta(minutes=1))
        value["deadlineUtc"] = module._utc(self.now + timedelta(minutes=30))
        return value

    @staticmethod
    def completed(returncode=0, stdout=b""):
        return subprocess.CompletedProcess([], returncode, stdout=stdout, stderr=b"")

    def environment_runner(self, arguments, **kwargs):
        if arguments[1:3] == ["image", "inspect"]:
            output = ("sha256:" + "1" * 64 + "\n").encode()
        elif arguments[1:3] == ["container", "inspect"]:
            return self.completed(returncode=1)
        elif arguments[1:3] == ["container", "ls"]:
            output = b""
        elif arguments[-1] == "--version":
            output = f"{arguments[-2]} (PostgreSQL) 16.10\n".encode()
        elif "getent" in arguments:
            output = b"192.168.65.254 STREAM host.docker.internal\n"
        elif arguments[-1] == "version":
            output = b"OpenSSL 3.6.3 1 Oct 2026\n"
        else:
            raise AssertionError(arguments)
        return self.completed(stdout=output)

    def test_validate_config_preflight_does_not_require_window(self):
        with patch.object(module.subprocess, "run", side_effect=self.environment_runner):
            validated = module.validate_config(self.config)
        self.assertIsNone(validated.drained_at_utc)
        self.assertIsNone(validated.deadline_utc)
        self.assertEqual(1800, module._parse_config(dict(self.config,
            verificationTimeoutSeconds=1800)).verification_timeout_seconds)

    def test_create_requires_drain_and_deadline_but_verify_only_requires_drain(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        with self.assertRaisesRegex(module.RecoveryError, "^RECOVERY_WINDOW_REQUIRED$"):
            recovery.create()
        drained = dict(self.config, drainedAtUtc=module._utc(self.now))
        self.assertIsNone(module._parse_config(drained).deadline_utc)

    def test_operation_deadline_is_the_smaller_of_window_and_timeout(self):
        config = self.with_window()
        config["verificationTimeoutSeconds"] = 60
        config["deadlineUtc"] = module._utc(self.now + timedelta(seconds=7))
        start = time.monotonic()
        recovery = module.PostgresLogicalRecovery(
            module._parse_config(config), now=lambda: self.now)
        self.assertGreaterEqual(recovery.operation_deadline - start, 6.9)
        self.assertLessEqual(recovery.operation_deadline - start, 7.1)

    def test_private_key_and_immutable_image_are_fail_closed(self):
        self.private_key.chmod(0o644)
        with self.assertRaisesRegex(module.RecoveryError, "^PRIVATE_KEY_INVALID$"):
            module._parse_config(self.config)
        self.private_key.chmod(0o600)
        mutable = dict(self.config, postgresImage="postgres:16")
        with self.assertRaisesRegex(module.RecoveryError, "^POSTGRES_IMAGE_NOT_IMMUTABLE$"):
            module._parse_config(mutable)

    def test_source_credentials_exist_only_in_private_service_file(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        recovery.bridge_address = "192.168.65.254"
        service = self.root / "service.conf"
        password_file = self.root / "service.pgpass"
        target = validated.targets[0]
        recovery._write_connection_files(service, password_file, target)
        command = recovery._source_run_arguments(
            service, password_file, "pg_dump", "--format=custom")
        joined = " ".join(command)
        self.assertNotIn(target.password, joined)
        self.assertNotIn(target.user, joined)
        self.assertNotIn(target.dbname, joined)
        self.assertEqual(0o600, service.stat().st_mode & 0o777)
        self.assertEqual(0o600, password_file.stat().st_mode & 0o777)
        contents = service.read_text()
        self.assertIn("host=pms.postgres.database.azure.com", contents)
        self.assertIn("hostaddr=192.168.65.254", contents)
        self.assertIn("sslmode=verify-full", contents)
        self.assertIn("options=-c default_transaction_read_only=on", contents)
        self.assertNotIn("password", contents)
        self.assertIn("synthetic password\\:'quoted'\\\\with-special-value",
                      password_file.read_text())

    def test_certificate_binding_uses_canonical_pem_bytes(self):
        original = self.config["binding"]["evidenceCertificateSha256"]
        self.certificate.write_text(self.certificate.read_text().rstrip() + "\n\n")
        self.assertEqual(original, module._certificate_binding_sha256(self.certificate))
        module._parse_config(self.config)

    def inventory(self, code):
        identity = {
            "singleton": True,
            "database_kind": "directory" if code == "DIRECTORY" else "business",
            "business_unit_code": None if code == "DIRECTORY" else code,
            "schema_contract": ("0001_business_unit_directory" if code == "DIRECTORY"
                                else "0086_business_unit_database_identity"),
        }
        lines = ["bootstrapRole|synthetic_bootstrap",
                 "identity|" + json.dumps([identity], separators=(",", ":")), "ledger|[]"]
        for role in module.TARGET_CODES:
            for database in module.TARGET_CODES:
                lines.append(f"connect|{role}|{database}|{'true' if role == database else 'false'}")
        path = self.root / f"{code}.inventory"
        path.write_text("\n".join(lines) + "\n")
        return path

    def test_identity_and_connect_isolation_are_exact(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        for target in validated.targets:
            inventory = self.inventory(target.code)
            recovery._validate_identity(target, inventory)
            recovery._validate_connect_matrix(inventory)
        broken = self.inventory("DIRECTORY")
        broken.write_text(broken.read_text().replace(
            "connect|DIRECTORY|CHEONGJU|false", "connect|DIRECTORY|CHEONGJU|true"))
        with self.assertRaisesRegex(module.RecoveryError, "^DATABASE_CONNECT_ISOLATION_MISMATCH$"):
            recovery._validate_connect_matrix(broken)

    def test_inventory_captures_required_exact_and_aggregate_evidence(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        sql = recovery._inventory_sql(validated.targets[1]).decode()
        self.assertIn("qms_database_identity", sql)
        self.assertIn("schema_migrations", sql)
        self.assertIn("deployment_maintenance", sql)
        self.assertIn("count(*)", sql)
        self.assertIn("sha256(convert_to(to_jsonb(t)::text", sql)
        self.assertIn("pg_sequences", sql)
        self.assertIn("pg_largeobject", sql)
        self.assertIn("has_database_privilege", sql)

    def test_schema_normalization_ignores_only_dump_metadata(self):
        first = self.root / "first.sql"
        second = self.root / "second.sql"
        first.write_text("-- Dumped from database version 16.6\n"
                         "-- Dumped by pg_dump version 16.14\n"
                         "\\restrict alpha\nCREATE TABLE example(id int);\n\\unrestrict alpha\n")
        second.write_text("-- Dumped from database version 16.14\n"
                          "-- Dumped by pg_dump version 16.14\n"
                          "\\restrict beta\nCREATE TABLE example(id int);\n\\unrestrict beta\n")
        self.assertEqual(module.PostgresLogicalRecovery._normalized_schema_sha256(first),
                         module.PostgresLogicalRecovery._normalized_schema_sha256(second))
        second.write_text(second.read_text().replace("id int", "id bigint"))
        self.assertNotEqual(module.PostgresLogicalRecovery._normalized_schema_sha256(first),
                            module.PostgresLogicalRecovery._normalized_schema_sha256(second))

    def test_inventory_canonicalization_is_bounded_and_order_independent(self):
        first = self.root / "first.inventory"
        second = self.root / "second.inventory"
        first.write_bytes(b"row|b\r\nrow|a\nrow|a\n")
        second.write_bytes(b"row|a\nrow|b\nrow|a\n")
        module.PostgresLogicalRecovery._canonicalize_inventory(first, chunk_bytes=6)
        module.PostgresLogicalRecovery._canonicalize_inventory(second, chunk_bytes=5)
        self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_cms_commands_require_gcm_oaep_ski_and_decrypt_private_key(self):
        validated = module._parse_config(self.config)
        commands = []

        def runner(arguments, **kwargs):
            commands.append(arguments)
            if arguments[1:3] == ["cms", "-encrypt"] or arguments[1:3] == ["cms", "-decrypt"]:
                source = Path(arguments[arguments.index("-in") + 1])
                destination = Path(arguments[arguments.index("-out") + 1])
                destination.write_bytes(source.read_bytes())
            return self.completed()

        recovery = module.PostgresLogicalRecovery(validated, run=runner)
        plain = self.root / "plain"
        plain.write_bytes(b"synthetic")
        cipher = self.root / "cipher"
        restored = self.root / "restored"
        recovery._encrypt(plain, cipher)
        recovery._decrypt(cipher, restored)
        self.assertEqual(plain.read_bytes(), restored.read_bytes())
        encrypt = commands[0]
        self.assertIn("-keyid", encrypt)
        self.assertIn("-aes-256-gcm", encrypt)
        self.assertIn("rsa_padding_mode:oaep", encrypt)
        self.assertIn("rsa_oaep_md:sha256", encrypt)
        self.assertNotIn(str(self.private_key), encrypt)
        self.assertIn(str(self.private_key), commands[1])

    def test_archive_extraction_rejects_extra_or_link_members(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        bundle = self.root / "bad.tar"
        with tarfile.open(bundle, "w") as archive:
            info = tarfile.TarInfo("../outside")
            info.size = 1
            archive.addfile(info, io.BytesIO(b"x"))
        with self.assertRaisesRegex(module.RecoveryError, "^ARCHIVE_CONTENT_INVALID$"):
            recovery._extract(bundle, self.root / "extract")

    def test_globals_restore_preserves_raw_and_only_applies_exact_compatibility(self):
        validated = module._parse_config(self.config)
        recovery = module.PostgresLogicalRecovery(validated)
        payload = self.root / "globals-payload"
        payload.mkdir()
        raw = ("CREATE ROLE synthetic_bootstrap;\n"
               "ALTER ROLE synthetic_bootstrap WITH SUPERUSER;\n"
               + module.AZURE_PLATFORM_GRANT + "\n"
               "CREATE TABLESPACE temptblspace OWNER azure_pg_admin "
               "LOCATION '/mnt/pg_tmp';\n")
        (payload / "globals.sql").write_text(raw)
        for code in module.TARGET_CODES:
            folder = payload / code
            folder.mkdir()
            (folder / "schema.sql").write_text("CREATE TABLE example(id int);\n")
        restore, has_tablespace = recovery._prepare_globals_restore(
            payload, "synthetic_bootstrap")
        self.assertTrue(has_tablespace)
        self.assertEqual(raw, (payload / "globals.sql").read_text())
        restored_text = restore.read_text()
        self.assertNotIn("CREATE ROLE synthetic_bootstrap;", restored_text)
        self.assertNotIn(module.AZURE_PLATFORM_GRANT, restored_text)
        self.assertIn("ALTER ROLE synthetic_bootstrap WITH SUPERUSER;", restored_text)
        compatibility = recovery._platform_compatibility(payload)
        self.assertEqual(module.AZURE_PLATFORM_GRANT,
                         compatibility[0]["excludedStatement"])
        self.assertEqual(module._sha256(payload / "globals.sql"),
                         compatibility[0]["sourceGlobalsSha256"])

        (payload / "DIRECTORY" / "schema.sql").write_text(
            "GRANT pg_signal_autovacuum_worker TO app;\n")
        with self.assertRaisesRegex(
                module.RecoveryError, "^SCHEMA_PLATFORM_ROLE_DEPENDENCY_UNSUPPORTED$"):
            recovery._platform_compatibility(payload)

    def test_restore_container_is_owned_networkless_tmpfs_and_removed(self):
        validated = module._parse_config(self.config)
        payload = self.root / "payload"
        payload.mkdir()
        (payload / "globals.sql").write_text(
            "CREATE ROLE synthetic_bootstrap;\n"
            "ALTER ROLE synthetic_bootstrap WITH SUPERUSER;\n")
        for target in validated.targets:
            folder = payload / target.code
            folder.mkdir()
            (folder / "database.dump").write_bytes(b"dump")
            (folder / "schema.sql").write_bytes(b"raw-schema")
            (folder / "canonical-schema.sql").write_bytes(b"schema")
            (folder / "inventory.txt").write_bytes(
                b"bootstrapRole|synthetic_bootstrap\n")
        commands = []
        present = {"value": False}
        inventory_reads = {"value": 0}
        deadline_checks = {"value": 0}
        recovery = None

        def runner(arguments, **kwargs):
            commands.append(arguments)
            if arguments[1:3] == ["run", "--detach"]:
                present["value"] = True
            if arguments[1:4] == ["container", "rm", "--force"]:
                present["value"] = False
            if arguments[1:3] == ["container", "inspect"]:
                if not present["value"]:
                    return self.completed(returncode=1)
                format_value = arguments[arguments.index("-f") + 1]
                name = arguments[-1]
                if module.OWNER_LABEL in format_value:
                    output = module.OWNER_VALUE
                elif module.RUN_LABEL in format_value:
                    output = name.removeprefix("emi-qms-logical-recovery-")
                elif format_value == "{{.Image}}":
                    output = self.config["postgresImage"]
                elif format_value == "{{.HostConfig.NetworkMode}}":
                    output = "none"
                else:
                    output = "container-id"
                return self.completed(stdout=(output + "\n").encode())
            if arguments[1:3] == ["container", "ls"]:
                return self.completed(stdout=b"container-id\n" if present["value"] else b"")
            if "pg_isready" in arguments:
                return self.completed()
            stdout = kwargs.get("stdout")
            if hasattr(stdout, "write") and "pg_dump" in arguments:
                stdout.write(b"schema")
            if hasattr(stdout, "write") and "psql" in arguments and "--dbname" in arguments:
                database = arguments[arguments.index("--dbname") + 1]
                if database != "postgres":
                    stdout.write(b"bootstrapRole|synthetic_bootstrap\n")
                    inventory_reads["value"] += 1
                    if inventory_reads["value"] == 3:
                        recovery.operation_deadline = time.monotonic() - 1
            return self.completed(stdout=b"container-id\n")

        class SyntheticRecovery(module.PostgresLogicalRecovery):
            def _deadline(self):
                deadline_checks["value"] += 1
                return super()._deadline()

            def _validate_identity(self, target, inventory):
                return None

            def _validate_connect_matrix(self, inventory):
                return None

        recovery = SyntheticRecovery(validated, run=runner)
        proof = recovery._restore_payload(payload)
        self.assertEqual(set(module.TARGET_CODES), set(proof))
        create = next(command for command in commands if command[1:3] == ["run", "--detach"])
        self.assertIn("none", create[create.index("--network") + 1:])
        self.assertIn("--tmpfs", create)
        self.assertNotIn("--publish", create)
        self.assertTrue(any(command[1:4] == ["container", "rm", "--force"] for command in commands))
        self.assertFalse(present["value"])
        self.assertGreaterEqual(deadline_checks["value"], 1)

    def test_create_and_verify_return_encrypted_manifest_contract_and_cleanup_plaintext(self):
        config = self.with_window()
        validated = module._parse_config(config)

        class SyntheticRecovery(module.PostgresLogicalRecovery):
            def validate_environment(self):
                self.bridge_address = "192.168.65.254"

            def _source_files(self, work):
                (work / "globals.sql").write_text("-- no passwords\n")
                artifacts = {}
                for target in self.config.targets:
                    folder = work / target.code
                    folder.mkdir(mode=0o700)
                    for name, value in (("database.dump", b"dump-" + target.code.encode()),
                                        ("schema.sql", b"schema-" + target.code.encode()),
                                        ("inventory.txt",
                                         b"bootstrapRole|synthetic_bootstrap\ninventory-"
                                         + target.code.encode() + b"\n")):
                        (folder / name).write_bytes(value)
                        (folder / name).chmod(0o600)
                    artifacts[target.code] = {
                        "databaseDumpSha256": module._sha256(folder / "database.dump"),
                        "schemaSha256": module._sha256(folder / "schema.sql"),
                        "inventorySha256": module._sha256(folder / "inventory.txt"),
                        "rawSourceProofSha256": module.hashlib.sha256(
                            (self._normalized_schema_sha256(folder / "schema.sql")
                             + module._sha256(folder / "inventory.txt")).encode()
                        ).hexdigest(),
                    }
                artifacts["GLOBALS"] = {
                    "globalsSha256": module._sha256(work / "globals.sql"),
                    "bootstrapRole": "synthetic_bootstrap",
                }
                return artifacts

            def _prepare_canonical_cluster(self, payload):
                for code in module.TARGET_CODES:
                    (payload / code / "canonical-schema.sql").write_bytes(
                        (payload / code / "schema.sql").read_bytes())
                return "synthetic-container", "1" * 24, "synthetic_bootstrap"

            def _encrypt(self, source, destination):
                with module._new_private_file(destination) as output:
                    output.write(source.read_bytes())

            def _decrypt(self, source, destination):
                with module._new_private_file(destination) as output:
                    output.write(source.read_bytes())

            def _restore_payload(self, payload, scope=None):
                return {
                    code: module.hashlib.sha256(
                        (self._normalized_schema_sha256(
                            payload / code / "canonical-schema.sql")
                         + module._sha256(payload / code / "inventory.txt")).encode()
                    ).hexdigest()
                    for code in module.TARGET_CODES
                }

            def _cleanup_owned_container(self, name, run_id):
                return None

            def _verify_source_unchanged(self, work, artifacts):
                return None

        recovery = SyntheticRecovery(validated, now=lambda: self.now)
        manifest = recovery.create()
        self.assertEqual(self.config["binding"], manifest["binding"])
        self.assertEqual(module.EVIDENCE_KIND, manifest["evidenceKind"])
        self.assertEqual(list(module.TARGET_CODES), [item["code"] for item in manifest["databases"]])
        for key in ("archive", "manifestCiphertext"):
            path = Path(manifest[key]["path"])
            self.assertTrue(path.is_file())
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
        self.assertFalse(any(path.name.startswith(".logical-") for path in self.archive.iterdir()))
        recovery.verify(manifest)


@unittest.skipUnless(os.environ.get("POSTGRES_LOGICAL_RECOVERY_E2E_IMAGE"),
                     "set POSTGRES_LOGICAL_RECOVERY_E2E_IMAGE to an immutable local PG16 image ID")
class LogicalRecoveryDockerEndToEndTests(unittest.TestCase):
    """Real TLS source, logical dumps, globals, and one offline three-DB restore."""

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="logical-recovery-e2e-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.archive = self.root / "archive"
        self.archive.mkdir(mode=0o700)
        self.image = os.environ["POSTGRES_LOGICAL_RECOVERY_E2E_IMAGE"]
        self.openssl = os.environ.get("POSTGRES_LOGICAL_RECOVERY_E2E_OPENSSL",
                                      "/opt/homebrew/opt/openssl@3/bin/openssl")
        self.run_id = secrets.token_hex(8)
        self.container = f"emi-qms-logical-source-test-{self.run_id}"
        self.password = "space quote' back\\slash:colon"
        self.host = "pms.postgres.database.azure.com"
        self._create_certificates()
        self.addCleanup(self._cleanup_source)
        self._start_source()
        self._initialize_source()

    def _run(self, arguments, *, input_bytes=None, allow_failure=False):
        result = subprocess.run(arguments, input=input_bytes, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, check=False, timeout=120)
        if not allow_failure and result.returncode != 0:
            self.fail("synthetic PostgreSQL setup command failed")
        return result

    def _create_certificates(self):
        self.ca_key = self.root / "tls-ca.key"
        self.ca_cert = self.root / "tls-ca.crt"
        server_key = self.root / "server.key"
        server_request = self.root / "server.csr"
        server_cert = self.root / "server.crt"
        evidence_key = self.root / "recipient.key"
        evidence_cert = self.root / "recipient.pem"
        self._run([self.openssl, "req", "-x509", "-newkey", "rsa:2048", "-nodes",
                   "-keyout", str(self.ca_key), "-out", str(self.ca_cert), "-days", "1",
                   "-subj", "/CN=logical-recovery-test-ca"])
        self._run([self.openssl, "req", "-new", "-newkey", "rsa:2048", "-nodes",
                   "-keyout", str(server_key), "-out", str(server_request),
                   "-subj", f"/CN={self.host}", "-addext", f"subjectAltName=DNS:{self.host}"])
        self._run([self.openssl, "x509", "-req", "-in", str(server_request),
                   "-CA", str(self.ca_cert), "-CAkey", str(self.ca_key), "-CAcreateserial",
                   "-out", str(server_cert), "-days", "1", "-copy_extensions", "copy"])
        self._run([self.openssl, "req", "-x509", "-newkey", "rsa:2048", "-nodes",
                   "-keyout", str(evidence_key), "-out", str(evidence_cert), "-days", "1",
                   "-subj", "/CN=logical-recovery-evidence",
                   "-addext", "subjectKeyIdentifier=hash"])
        for path in (self.ca_key, server_key, evidence_key):
            path.chmod(0o600)
        self.server_key = server_key
        self.server_cert = server_cert
        self.evidence_key = evidence_key
        self.evidence_cert = evidence_cert

    def _start_source(self):
        self._run([
            "docker", "run", "--detach", "--name", self.container,
            "--label", "com.emi-qms.test.owner=postgres-logical-recovery-e2e",
            "--label", f"com.emi-qms.test.run-id={self.run_id}",
            "--network", "bridge", "--publish", "127.0.0.1::5432",
            "--tmpfs", "/var/lib/postgresql/data:rw,nosuid,nodev,noexec",
            "--mount", f"type=bind,src={self.server_cert},dst=/run/tls/server.crt,readonly",
            "--mount", f"type=bind,src={self.server_key},dst=/run/tls/server.key,readonly",
            "--env", "POSTGRES_PASSWORD=synthetic-bootstrap-only", self.image,
            "-c", "ssl=on", "-c", "ssl_cert_file=/run/tls/server.crt",
            "-c", "ssl_key_file=/run/tls/server.key",
        ])
        for _ in range(60):
            ready = self._run(["docker", "exec", self.container, "pg_isready",
                               "--username", "postgres", "--dbname", "postgres"],
                              allow_failure=True)
            if ready.returncode == 0:
                break
            time.sleep(0.5)
        else:
            self.fail("synthetic PostgreSQL source did not become ready")
        port = self._run(["docker", "port", self.container, "5432/tcp"]).stdout.decode().strip()
        self.port = int(port.rsplit(":", 1)[1])

    def _cleanup_source(self):
        inspected = self._run([
            "docker", "container", "inspect", "-f",
            '{{ index .Config.Labels "com.emi-qms.test.run-id" }}', self.container
        ], allow_failure=True)
        if inspected.returncode == 0 and inspected.stdout.decode().strip() == self.run_id:
            self._run(["docker", "container", "rm", "--force", self.container])

    @staticmethod
    def _sql_literal(value):
        return "'" + value.replace("'", "''") + "'"

    def _psql(self, database, sql):
        self._run(["docker", "exec", "--interactive", self.container, "psql", "--no-psqlrc",
                   "--quiet", "--set", "ON_ERROR_STOP=1", "--username", "postgres",
                   "--dbname", database], input_bytes=sql.encode())

    def _initialize_source(self):
        password = self._sql_literal(self.password)
        self._psql("postgres", f"""
CREATE ROLE backup_reader LOGIN SUPERUSER PASSWORD {password};
CREATE ROLE directory_runtime NOLOGIN;
CREATE ROLE cheongju_runtime NOLOGIN;
CREATE ROLE osan_runtime NOLOGIN;
CREATE ROLE synthetic_auditor NOLOGIN;
CREATE DATABASE directory_db;
CREATE DATABASE cheongju_db;
CREATE DATABASE osan_db;
REVOKE CONNECT ON DATABASE directory_db FROM PUBLIC;
REVOKE CONNECT ON DATABASE cheongju_db FROM PUBLIC;
REVOKE CONNECT ON DATABASE osan_db FROM PUBLIC;
GRANT CONNECT ON DATABASE directory_db TO directory_runtime;
GRANT CONNECT ON DATABASE cheongju_db TO cheongju_runtime;
GRANT CONNECT ON DATABASE osan_db TO osan_runtime;
""")
        for code, database, runtime_role in (
                ("DIRECTORY", "directory_db", "directory_runtime"),
                ("CHEONGJU", "cheongju_db", "cheongju_runtime"),
                ("OSAN", "osan_db", "osan_runtime")):
            kind = "directory" if code == "DIRECTORY" else "business"
            business = "NULL" if code == "DIRECTORY" else self._sql_literal(code)
            contract = ("0001_business_unit_directory" if code == "DIRECTORY"
                        else "0086_business_unit_database_identity")
            maintenance = "" if code == "DIRECTORY" else """
CREATE TABLE deployment_maintenance(singleton boolean PRIMARY KEY, enabled boolean NOT NULL);
INSERT INTO deployment_maintenance VALUES (true, false);
"""
            self._psql(database, f"""
CREATE TABLE qms_database_identity(
  singleton boolean PRIMARY KEY, database_kind text NOT NULL,
  business_unit_code text, schema_contract text NOT NULL);
INSERT INTO qms_database_identity VALUES
  (true, {self._sql_literal(kind)}, {business}, {self._sql_literal(contract)});
CREATE TABLE schema_migrations(version text PRIMARY KEY, applied_at timestamptz NOT NULL);
INSERT INTO schema_migrations VALUES ('0001', '2026-01-01T00:00:00Z');
{maintenance}
CREATE TABLE payloads(
  id bigserial PRIMARY KEY, note text NOT NULL, binary_value bytea NOT NULL,
  amount numeric(18,4) NOT NULL, occurred_at timestamptz NOT NULL);
ALTER TABLE payloads OWNER TO {runtime_role};
INSERT INTO payloads(note,binary_value,amount,occurred_at)
VALUES ('quote '' and slash \\ and colon :', decode(repeat('ab',1048576),'hex'),
        123456789.0123, '2026-09-30T15:00:00Z');
INSERT INTO payloads(note,binary_value,amount,occurred_at)
VALUES ('second distinct row', decode(repeat('ef',262144),'hex'),
        -99.5000, '2026-10-01T00:00:00Z');
SELECT setval(pg_get_serial_sequence('payloads','id'), 987654,
              {'false' if code == 'DIRECTORY' else 'true'});
SELECT lo_from_bytea(0, decode(repeat('cd',524288),'hex'));
GRANT USAGE ON SCHEMA public TO synthetic_auditor;
GRANT SELECT(note) ON payloads TO synthetic_auditor;
""")

    def test_real_tls_dump_encrypt_restore_and_verify(self):
        now = datetime.now(timezone.utc)
        targets = [
            {"code": "DIRECTORY", "dbname": "directory_db", "runtimeRole": "directory_runtime"},
            {"code": "CHEONGJU", "dbname": "cheongju_db", "runtimeRole": "cheongju_runtime"},
            {"code": "OSAN", "dbname": "osan_db", "runtimeRole": "osan_runtime"},
        ]
        for target in targets:
            target.update({"host": self.host, "user": "backup_reader", "password": self.password})
        config = {
            "archiveDir": str(self.archive),
            "certificatePath": str(self.evidence_cert),
            "privateKeyPath": str(self.evidence_key),
            "sslRootCertPath": str(self.ca_cert),
            "postgresImage": self.image,
            "relayPort": self.port,
            "verificationTimeoutSeconds": 1800,
            "opensslPath": self.openssl,
            "drainedAtUtc": module._utc(now - timedelta(minutes=1)),
            "deadlineUtc": module._utc(now + timedelta(minutes=30)),
            "binding": {
                "sourceSha": "2" * 40,
                "releaseId": "11111111-1111-1111-1111-111111111111",
                "serverId": "/subscriptions/synthetic/servers/pms",
                "expectedHost": self.host,
                "apps": ["backend", "frontend"],
                "evidenceCertificateSha256": module._certificate_binding_sha256(self.evidence_cert),
                "evidenceKind": module.EVIDENCE_KIND,
                "logicalConfigSha256": "3" * 64,
            },
            "targets": targets,
        }
        validated = module._parse_config(config)
        probe = module.PostgresLogicalRecovery(validated)
        probe.validate_environment()
        service = self.root / "probe.service"
        password_file = self.root / "probe.pgpass"
        probe._write_connection_files(service, password_file, validated.targets[0])
        tls_check = probe._execute_source(
            service, password_file, "psql", "--no-password", "--no-psqlrc", "--tuples-only",
            "--no-align", "--command",
            "select ssl::text || '|' || current_setting('default_transaction_read_only') "
            "from pg_stat_ssl where pid=pg_backend_pid();",
            code="SOURCE_TLS_PROBE_FAILED", capture=True, timeout=30)
        self.assertEqual("true|on", tls_check.decode().strip())
        with self.assertRaisesRegex(module.RecoveryError, "^SOURCE_READ_ONLY_WRITE_ACCEPTED$"):
            probe._execute_source(
                service, password_file, "psql", "--no-password", "--no-psqlrc",
                "--command", "create table forbidden_write(id int);",
                code="SOURCE_READ_ONLY_WRITE_ACCEPTED", timeout=30)

        timeout_scope = secrets.token_hex(12)
        with self.assertRaisesRegex(module.RecoveryError, "^SOURCE_TIMEOUT_EXPECTED$"):
            probe._execute_source(
                service, password_file, "sh", "-c", "sleep 30",
                code="SOURCE_TIMEOUT_EXPECTED", scope_run_id=timeout_scope, timeout=1)
        leaked = self._run([
            "docker", "container", "ls", "--all", "--quiet",
            "--filter", f"label={module.RUN_LABEL}={timeout_scope}",
        ])
        self.assertEqual(b"", leaked.stdout.strip())
        service.unlink()
        password_file.unlink()

        manifest = module.create_backup(config)
        verify_config = dict(config)
        verify_config.pop("deadlineUtc")
        module.verify_backup(manifest, verify_config)
        self.assertEqual(list(module.TARGET_CODES),
                         [database["code"] for database in manifest["databases"]])
        self.assertFalse(any(path.name.startswith(".logical-") for path in self.archive.iterdir()))


if __name__ == "__main__":
    unittest.main()
