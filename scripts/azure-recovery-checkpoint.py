#!/usr/bin/env python3
"""Select an Azure-listed, post-drain full backup. Never create or restore one."""
import argparse
from datetime import datetime, timezone
import json
import hashlib
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile
import time
import uuid


def require(condition, code):
    if not condition:
        raise RuntimeError(code)


def instant(value):
    require(isinstance(value, str) and value == value.strip(), "INVALID_TIMESTAMP")
    require(re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})", value),
            "INVALID_TIMESTAMP")
    # Python 3.9 accepts only 3/6 fractional digits. Preserve the provider's exact
    # string in evidence; truncate to microseconds only for conservative comparison.
    normalized = re.sub(r"\.(\d{1,7})(?=Z|[+-])", lambda m: "." + (m[1] + "000000")[:6], value)
    result = datetime.fromisoformat(normalized.replace("Z", "+00:00"))
    require(result.tzinfo is not None, "TIMEZONE_REQUIRED")
    return result.astimezone(timezone.utc)


def utc_now():
    return datetime.now(timezone.utc)


class Checkpoint:
    def __init__(self, environment, az="az"):
        self.env, self.az, self.deadline = environment, az, None
        encrypted_required = environment.get("RECOVERY_EVIDENCE_ENCRYPTION_REQUIRED", "false")
        require(encrypted_required in ("true", "false"), "INVALID_ENCRYPTION_REQUIREMENT")
        self.certificate = environment.get("RECOVERY_EVIDENCE_CERTIFICATE_PEM", "").strip()
        require(encrypted_required != "true" or self.certificate, "EVIDENCE_CERTIFICATE_REQUIRED")
        require(not self.certificate or re.fullmatch(
            r"-----BEGIN CERTIFICATE-----\s+[A-Za-z0-9+/=\s]+-----END CERTIFICATE-----", self.certificate),
            "PUBLIC_CERTIFICATE_ONLY")
        for key in ("AZURE_SUBSCRIPTION_ID", "AZURE_RESOURCE_GROUP", "RECOVERY_POSTGRES_SERVER_NAME",
                    "BACKEND_APP_NAME", "FRONTEND_APP_NAME", "MIGRATION_JOB_NAME", "MAINTENANCE_JOB_NAME",
                    "SOURCE_SHA", "MAINTENANCE_RELEASE_ID", "MAINTENANCE_EXPECTED_ENDS_AT_UTC"):
            require(bool(environment.get(key)), "MISSING_CONFIGURATION")
        require(re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", environment["RECOVERY_POSTGRES_SERVER_NAME"]),
                "INVALID_SERVER_NAME")
        require(re.fullmatch(r"[0-9a-f]{40}", environment["SOURCE_SHA"]), "INVALID_SOURCE")
        require(uuid.UUID(environment["MAINTENANCE_RELEASE_ID"]).int != 0, "INVALID_RELEASE")
        require(az == "az" or (environment.get("PUBLIC_HOSTNAME") == "pms.synthetic.internal"
                and (environment.get("FIRST_ROLLOUT_ALLOW_TEST_OVERRIDES") == "true"
                     or environment.get("AZURE_RELEASE_ALLOW_TEST_OVERRIDES") == "true")),
                "COMMAND_OVERRIDE_REJECTED")
        self.timeout = int(environment.get("RECOVERY_CHECKPOINT_TIMEOUT_SECONDS", "900"))
        self.interval = int(environment.get("RECOVERY_CHECKPOINT_POLL_SECONDS", "30"))
        require(1 <= self.timeout <= 1800 and 1 <= self.interval <= 60, "INVALID_WAIT_BOUND")
        self.ends_at = instant(environment["MAINTENANCE_EXPECTED_ENDS_AT_UTC"])
        require(self.ends_at > utc_now(), "WINDOW_EXPIRED")
        self.server_id = (f'/subscriptions/{environment["AZURE_SUBSCRIPTION_ID"]}'
                          f'/resourceGroups/{environment["AZURE_RESOURCE_GROUP"]}'
                          '/providers/Microsoft.DBforPostgreSQL/flexibleServers/'
                          + environment["RECOVERY_POSTGRES_SERVER_NAME"])
        self.host = environment["RECOVERY_POSTGRES_SERVER_NAME"] + ".postgres.database.azure.com"
        self.binding = {"sourceSha": environment["SOURCE_SHA"], "releaseId": environment["MAINTENANCE_RELEASE_ID"],
                        "serverId": self.server_id, "expectedHost": self.host,
                        "apps": [environment["BACKEND_APP_NAME"], environment["FRONTEND_APP_NAME"]],
                        "evidenceCertificateSha256": hashlib.sha256(self.certificate.encode()).hexdigest()
                            if self.certificate else None}
        self.rg = ["--resource-group", environment["AZURE_RESOURCE_GROUP"]]

    def read(self, *args):
        remaining = 30 if self.deadline is None else min(30, self.deadline - time.monotonic())
        require(remaining > 0, "BACKUP_WAIT_EXPIRED")
        result = subprocess.run([self.az, *args, "--subscription", self.env["AZURE_SUBSCRIPTION_ID"],
                                 "--output", "json", "--only-show-errors"],
                                capture_output=True, text=True, timeout=remaining)
        require(result.returncode == 0, "AZURE_READ_FAILED")
        return json.loads(result.stdout)

    def server(self):
        value = self.read("postgres", "flexible-server", "show", *self.rg,
                          "--name", self.env["RECOVERY_POSTGRES_SERVER_NAME"])
        require(value["id"].lower() == self.server_id.lower()
                and value["fullyQualifiedDomainName"].lower() == self.host
                and value["state"] == "Ready", "SERVER_IDENTITY_OR_STATE_INVALID")
        backup = value["backup"]
        require(type(backup["backupRetentionDays"]) is int and 14 <= backup["backupRetentionDays"] <= 35,
                "BACKUP_RETENTION_INVALID")
        require(instant(backup["earliestRestoreDate"]) < utc_now(), "RESTORE_RANGE_INVALID")
        return {"earliestRestoreDate": backup["earliestRestoreDate"],
                "retentionDays": backup["backupRetentionDays"]}

    def backups(self):
        # Azure CLI's list command consumes the SDK pager. A raw/partial page is not a list.
        values = self.read("postgres", "flexible-server", "backup", "list", *self.rg,
                           "--server-name", self.env["RECOVERY_POSTGRES_SERVER_NAME"])
        require(isinstance(values, list), "BACKUP_LIST_INCOMPLETE")
        ids, result = set(), []
        for value in values:
            expected = self.server_id + "/backups/" + value["name"]
            require(value["id"].lower() == expected.lower()
                    and value["type"].lower() == "microsoft.dbforpostgresql/flexibleservers/backups"
                    and value["id"].lower() not in ids, "BACKUP_IDENTITY_INVALID")
            ids.add(value["id"].lower())
            completed = instant(value["completedTime"])
            require(completed <= utc_now(), "BACKUP_TIME_IN_FUTURE")
            require(value["backupType"] in ("Full", "CustomerOnDemand"), "BACKUP_TYPE_UNKNOWN")
            if value["backupType"] == "Full" and value["source"] == "Automatic":
                result.append({key: value[key] for key in ("id", "name", "backupType", "completedTime", "source")})
        return result

    def jobs(self):
        values = self.read("containerapp", "job", "list", *self.rg)
        require(isinstance(values, list) and values, "JOB_LIST_INVALID")
        jobs = {}
        for value in values:
            name = value["name"]
            require(re.fullmatch(r"[a-zA-Z0-9-]+", name)
                    and value["properties"]["configuration"]["triggerType"] == "Manual", "JOB_MODE_INVALID")
            require(name not in jobs, "DUPLICATE_JOB")
            executions = self.read("containerapp", "job", "execution", "list", *self.rg, "--name", name)
            require(isinstance(executions, list), "EXECUTION_LIST_INVALID")
            jobs[name] = {}
            for execution in executions:
                key, properties = execution["name"], execution["properties"]
                require(isinstance(key, str) and re.fullmatch(r"[a-zA-Z0-9-]+", key)
                        and key not in jobs[name], "EXECUTION_ID_INVALID")
                require(properties["status"] in ("Succeeded", "Failed", "Stopped"), "JOB_STILL_RUNNING")
                jobs[name][key] = {field: properties.get(field) for field in ("status", "startTime", "endTime")}
        require(self.env["MIGRATION_JOB_NAME"] in jobs and self.env["MAINTENANCE_JOB_NAME"] in jobs,
                "REQUIRED_JOB_MISSING")
        return jobs

    def quiet(self, state):
        for name in self.binding["apps"]:
            revisions = self.read("containerapp", "revision", "list", *self.rg, "--name", name)
            require(isinstance(revisions, list) and revisions, "REVISION_LIST_INVALID")
            for revision in revisions:
                require(revision["properties"]["active"] is False, "APP_REACTIVATED")
                replicas = self.read("containerapp", "replica", "list", *self.rg,
                                     "--name", name, "--revision", revision["name"])
                require(isinstance(replicas, list) and not replicas, "REPLICA_STILL_RUNNING")
        current = self.jobs()
        require(current.keys() == state["jobs"].keys(), "JOB_SET_CHANGED")
        migration = self.env["MIGRATION_JOB_NAME"]
        for name, executions in current.items():
            for key, details in executions.items():
                if key in state["jobs"][name]:
                    require(details == state["jobs"][name][key], "BASELINE_EXECUTION_CHANGED")
                else:
                    require(name == migration and key in self.allowed_drains
                            and details["status"] == "Succeeded", "UNEXPECTED_JOB_EXECUTION")
            # Azure lists retained history; old entries may age out, but every
            # exact drain created by this release must remain observable.
        require(set(self.allowed_drains) <= current[migration].keys(), "DRAIN_EXECUTION_MISSING")

    def save(self, path, state, *, new=False):
        require(not path.parent.is_symlink() and stat.S_IMODE(path.parent.stat().st_mode) & 0o077 == 0,
                "EVIDENCE_DIRECTORY_NOT_PRIVATE")
        require(not path.is_symlink() and (not new or not path.exists()), "EVIDENCE_ALREADY_EXISTS")
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode="w", dir=path.parent, delete=False) as stream:
                temporary = Path(stream.name)
                json.dump(state, stream, indent=2)
                stream.flush()
                os.fsync(stream.fileno())
            if self.certificate:
                encrypted = path.with_suffix(".p7m")
                require(not encrypted.is_symlink(), "ENCRYPTED_EVIDENCE_SYMLINK")
                with tempfile.TemporaryDirectory(dir=path.parent) as workspace:
                    certificate = Path(workspace) / "recipient.pem"
                    ciphertext = Path(workspace) / "recovery.p7m"
                    certificate.write_text(self.certificate + "\n")
                    certificate.chmod(0o600)
                    ciphertext.touch(mode=0o600)
                    # The CI runner has only the public certificate. OpenSSL 3
                    # CMS uses authenticated AES-GCM and RSA-OAEP key transport.
                    result = subprocess.run([
                        "openssl", "cms", "-encrypt", "-binary", "-keyid", "-aes-256-gcm", "-outform", "DER",
                        "-in", str(temporary), "-out", str(ciphertext), "-recip", str(certificate),
                        "-keyopt", "rsa_padding_mode:oaep", "-keyopt", "rsa_oaep_md:sha256"],
                        capture_output=True, timeout=15)
                    require(result.returncode == 0 and ciphertext.stat().st_size > 0,
                            "EVIDENCE_ENCRYPTION_FAILED")
                    with ciphertext.open("rb") as stream:
                        os.fsync(stream.fileno())
                    os.replace(ciphertext, encrypted)
            os.replace(temporary, path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)

    def load(self, path):
        require(not path.is_symlink() and stat.S_ISREG(path.stat().st_mode)
                and stat.S_IMODE(path.stat().st_mode) & 0o077 == 0, "EVIDENCE_NOT_PRIVATE")
        state = json.loads(path.read_text())
        require(state["binding"] == self.binding and state["schemaVersion"] == 1, "EVIDENCE_BINDING_MISMATCH")
        return state

    def preflight(self, path):
        server = self.server()
        require(self.backups(), "NO_AVAILABLE_FULL_BACKUP")
        state = {"schemaVersion": 1, "binding": self.binding, "preparedAtUtc": utc_now().isoformat(),
                 "server": server, "jobs": self.jobs(), "phase": "prepared"}
        self.save(path, state, new=True)

    def arm(self, path):
        state = self.load(path)
        require(state["phase"] == "prepared", "CHECKPOINT_ALREADY_USED")
        jobs = self.jobs()
        require(jobs.keys() == state["jobs"].keys(), "JOB_SET_CHANGED")
        # The ordinary release has now finished its notice/maintenance jobs.
        # From this boundary only this runner's read-only drain executions may appear.
        state.update(phase="armed", jobs=jobs, armedAtUtc=utc_now().isoformat())
        self.save(path, state)

    def bind_drains(self, state, names, previous=()):
        require(len(names) == 3 and len(set(names)) == 3
                and all(isinstance(name, str) and re.fullmatch(r"[a-zA-Z0-9-]+", name) for name in names),
                "EXACT_DRAIN_EXECUTIONS_REQUIRED")
        require(not (set(names) & (state["jobs"][self.env["MIGRATION_JOB_NAME"]].keys() | set(previous))),
                "DRAIN_EXECUTION_REUSED")
        self.allowed_drains = list(previous) + list(names)

    def wait(self, path, drained_at, drain_executions):
        state = self.load(path)
        require(state["phase"] == "armed", "CHECKPOINT_ALREADY_USED")
        self.bind_drains(state, drain_executions)
        drained = instant(drained_at)
        require(instant(state["armedAtUtc"]) <= drained <= utc_now(), "DRAIN_TIME_INVALID")
        self.deadline = time.monotonic() + min(self.timeout, (self.ends_at - utc_now()).total_seconds())
        while True:
            require(time.monotonic() < self.deadline, "BACKUP_WAIT_EXPIRED")
            self.quiet(state)
            server = self.server()
            candidates = [b for b in self.backups() if instant(b["completedTime"]) > drained
                          and instant(b["completedTime"]) > instant(server["earliestRestoreDate"])]
            if candidates:
                self.quiet(state)
                state.update(phase="candidate", drainedAtUtc=drained_at, server=server,
                             backup=min(candidates, key=lambda b: instant(b["completedTime"])),
                             allowedDrains=self.allowed_drains, selectedAtUtc=utc_now().isoformat(), evidenceKind="AzureAvailableFullBackup")
                self.save(path, state)
                return
            time.sleep(max(0, min(self.interval, self.deadline - time.monotonic())))

    def verify(self, path, drain_executions):
        state = self.load(path)
        require(state["phase"] == "candidate" and utc_now() < self.ends_at, "CHECKPOINT_NOT_READY")
        self.bind_drains(state, drain_executions, state["allowedDrains"])
        self.deadline = time.monotonic() + min(120, (self.ends_at - utc_now()).total_seconds())
        self.quiet(state)
        server = self.server()
        require(state["backup"] in self.backups()
                and instant(state["backup"]["completedTime"]) > instant(state["drainedAtUtc"])
                and instant(state["backup"]["completedTime"]) > instant(server["earliestRestoreDate"]),
                "SELECTED_BACKUP_NO_LONGER_AVAILABLE")
        self.quiet(state)
        state.update(phase="verified", allowedDrains=self.allowed_drains, verifiedAtUtc=utc_now().isoformat(), server=server)
        self.save(path, state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("preflight", "arm", "wait", "verify"))
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--az-bin", default="az")
    parser.add_argument("--drained-at")
    parser.add_argument("--drain-execution", action="append", default=[])
    args = parser.parse_args()
    try:
        checkpoint = Checkpoint(os.environ, args.az_bin)
        if args.mode == "preflight":
            checkpoint.preflight(args.state)
            print(checkpoint.host)
        elif args.mode == "arm":
            checkpoint.arm(args.state)
            print("recoveryCheckpoint=ARMED")
        elif args.mode == "wait":
            checkpoint.wait(args.state, args.drained_at, args.drain_execution)
            print("recoveryCheckpoint=CANDIDATE_REQUIRES_FINAL_DRAIN")
        else:
            checkpoint.verify(args.state, args.drain_execution)
            print("recoveryCheckpoint=VERIFIED_AVAILABLE_FULL_BACKUP")
    except Exception:
        # Provider stderr, secrets and private identifiers never become release logs.
        print("recoveryCheckpoint=FAILED_NO_DATABASE_CHANGE_ALLOWED", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
