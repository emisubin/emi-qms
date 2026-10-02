#!/usr/bin/env python3
import copy
from concurrent.futures import Future
from contextlib import redirect_stderr
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import subprocess
import sys
import unittest
import threading
import time
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("checkpoint", Path(__file__).with_name("azure-recovery-checkpoint.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CheckpointTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="recovery-checkpoint-test-")
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name) / "recovery.json"
        self.now = datetime(2026, 1, 2, tzinfo=timezone.utc)
        self.clock = patch.object(module, "utc_now", lambda: self.now)
        self.clock.start()
        self.addCleanup(self.clock.stop)
        env = dict(AZURE_SUBSCRIPTION_ID="subscription", AZURE_RESOURCE_GROUP="synthetic",
                   RECOVERY_POSTGRES_SERVER_NAME="synthetic-pg", BACKEND_APP_NAME="backend",
                   FRONTEND_APP_NAME="frontend", MIGRATION_JOB_NAME="migration", MAINTENANCE_JOB_NAME="maintenance",
                   SOURCE_SHA="a" * 40, MAINTENANCE_RELEASE_ID="11111111-1111-1111-1111-111111111111",
                   MAINTENANCE_EXPECTED_ENDS_AT_UTC="2026-01-02T01:00:00Z",
                   RECOVERY_CHECKPOINT_TIMEOUT_SECONDS="1", RECOVERY_CHECKPOINT_POLL_SECONDS="1")
        self.checkpoint = module.Checkpoint(env)
        self.server = {"id": self.checkpoint.server_id, "fullyQualifiedDomainName": self.checkpoint.host,
                       "state": "Ready", "backup": {"backupRetentionDays": 14,
                       "earliestRestoreDate": "2025-12-20T00:00:00Z"}}
        self.backups = [self.backup("old", "2026-01-01T00:00:00Z")]
        self.active, self.replicas, self.running, self.extra_job = False, False, False, False
        self.executions = {"migration": {}, "maintenance": {}}
        self.calls = []
        self.checkpoint.read = self.read
        self.checkpoint.preflight(self.path)
        self.checkpoint.arm(self.path)
        self.now += timedelta(seconds=20)
        self.drained = "2026-01-02T00:00:10Z"
        self.initial_drains = ["initial-" + target for target in ("directory", "cheongju", "osan")]
        self.final_drains = ["final-" + target for target in ("directory", "cheongju", "osan")]
        for key in self.initial_drains:
            self.executions["migration"][key] = {"status": "Succeeded", "startTime": "2026-01-02T00:00:01Z", "endTime": self.drained}
        self.fresh = self.backup("new", "2026-01-02T00:00:15.1234567Z")

    def backup(self, name, completed):
        return {"id": self.checkpoint.server_id + "/backups/" + name, "name": name,
                "type": "Microsoft.DBforPostgreSQL/flexibleServers/backups", "backupType": "Full",
                "source": "Automatic", "completedTime": completed}

    def read(self, *args):
        self.calls.append(args)
        if args[:3] == ("postgres", "flexible-server", "show"):
            return copy.deepcopy(self.server)
        if args[:4] == ("postgres", "flexible-server", "backup", "list"):
            return copy.deepcopy(self.backups)
        if args[:3] == ("containerapp", "revision", "list"):
            if not self.active and "--all" not in args:
                return []
            return [{"name": "synthetic-revision", "properties": {
                "active": self.active, "replicas": 1 if self.replicas else 0}}]
        if args[:3] == ("containerapp", "replica", "list"):
            return [{}] if self.replicas else []
        if args[:3] == ("containerapp", "job", "list"):
            return [{"name": name, "properties": {"configuration": {"triggerType": "Manual"}}}
                    for name in ["migration", "maintenance"] + (["unexpected"] if self.extra_job else [])]
        if args[:4] == ("containerapp", "job", "execution", "list"):
            name = args[args.index("--name") + 1]
            return ([{"name": "inflight", "properties": {"status": "Running"}}] if self.running else
                    [{"name": key, "properties": value} for key, value in self.executions.get(name, {}).items()])
        raise AssertionError(args)

    def test_azure_read_timeout_uses_fixed_failure_reason(self):
        self.checkpoint.deadline = None
        with patch.object(module.subprocess, "run",
                          side_effect=subprocess.TimeoutExpired(cmd=["az"], timeout=30)):
            with self.assertRaisesRegex(RuntimeError, "^AZURE_READ_TIMEOUT$"):
                module.Checkpoint.read(self.checkpoint, "account", "show")

    def test_azure_read_retries_only_classified_transient_failures_within_budget(self):
        self.checkpoint.deadline = None
        transient = SimpleNamespace(returncode=1, stdout="", stderr="ERROR: (ServiceUnavailable) private host password=value")
        success = SimpleNamespace(returncode=0, stdout="[]", stderr="")
        with patch.object(module.subprocess, "run", side_effect=[transient, success]) as run, \
                patch.object(module.time, "sleep") as sleep:
            self.assertEqual([], module.Checkpoint.read(self.checkpoint, "containerapp", "replica", "list"))
            self.assertEqual(2, run.call_count)
            sleep.assert_called_once_with(2)
        for stderr in ("ERROR: (AuthorizationFailed) secret", "ERROR: (ResourceNotFound) secret",
                       "unknown private failure", "ERROR: (TooManyRequests) secret\nRetry-After: 600"):
            with self.subTest(stderr=stderr), patch.object(module.subprocess, "run",
                    return_value=SimpleNamespace(returncode=1, stdout="", stderr=stderr)) as run, \
                    patch.object(module.time, "sleep") as sleep:
                with self.assertRaisesRegex(module.AzureReadError, "^AZURE_READ_FAILED$") as raised:
                    module.Checkpoint.read(self.checkpoint, "containerapp", "replica", "list", "--name", "private-app")
                self.assertEqual(1, run.call_count)
                sleep.assert_not_called()
                self.assertNotIn("secret", json.dumps(raised.exception.diagnostic))
                self.assertNotIn("private", json.dumps(raised.exception.diagnostic))
        with patch.object(module.subprocess, "run", return_value=transient) as run, \
                patch.object(module.time, "sleep"):
            with self.assertRaises(module.AzureReadError) as raised:
                module.Checkpoint.read(self.checkpoint, "containerapp", "replica", "list")
            self.assertEqual(3, run.call_count)
            self.assertEqual(503, raised.exception.diagnostic["httpStatus"])
            self.assertEqual(3, raised.exception.diagnostic["attempt"])

    def test_azure_read_deadline_and_malformed_success_never_pass(self):
        self.checkpoint.deadline = time.monotonic() + 0.5
        with patch.object(module.subprocess, "run", return_value=SimpleNamespace(
                returncode=1, stdout="", stderr="ERROR: (InternalServerError) private")) as run, \
                patch.object(module.time, "sleep") as sleep:
            with self.assertRaises(module.AzureReadError):
                module.Checkpoint.read(self.checkpoint, "containerapp", "replica", "list")
            self.assertLessEqual(run.call_args.kwargs["timeout"], 0.5)
            sleep.assert_not_called()
        with patch.object(module.subprocess, "run", return_value=SimpleNamespace(
                returncode=0, stdout="not-json", stderr="")) as run:
            with self.assertRaises(json.JSONDecodeError):
                module.Checkpoint.read(self.checkpoint, "containerapp", "replica", "list")
            self.assertEqual(1, run.call_count)

    def test_main_saves_only_structured_read_failure_without_advancing_gate(self):
        diagnostic = {"command": "containerapp replica list", "atUtc": self.drained,
                      "attempt": 3, "exitCode": 1, "httpStatus": 503,
                      "errorClass": "ServiceUnavailable"}
        failure = module.AzureReadError("AZURE_READ_FAILED", diagnostic)
        output = io.StringIO()
        with patch.object(module, "Checkpoint", return_value=self.checkpoint), \
                patch.object(self.checkpoint, "wait", side_effect=failure), \
                patch.object(sys, "argv", ["checkpoint", "wait", "--state", str(self.path)]), \
                redirect_stderr(output):
            self.assertEqual(1, module.main())
        state = json.loads(self.path.read_text())
        self.assertEqual("armed", state["phase"])
        self.assertEqual(diagnostic, state["azureReadFailure"])
        self.assertEqual(0o600, self.path.stat().st_mode & 0o777)
        self.assertNotIn("ServiceUnavailable", output.getvalue())
        self.assertIn("reason=AZURE_READ_FAILED", output.getvalue())

    def verify(self):
        for key in self.final_drains:
            self.executions["migration"][key] = {"status": "Succeeded", "startTime": "2026-01-02T00:00:17Z", "endTime": "2026-01-02T00:00:19Z"}
        self.checkpoint.verify(self.path, self.final_drains)

    def test_new_terminal_executions_and_replayed_drains_are_rejected(self):
        self.candidate()
        for status in ("Succeeded", "Failed", "Stopped"):
            for job in ("migration", "maintenance"):
                with self.subTest(status=status, job=job):
                    self.executions[job]["unexpected-finished"] = {"status": status}
                    with self.assertRaisesRegex(RuntimeError, "UNEXPECTED_JOB_EXECUTION"):
                        self.verify()
                    self.executions[job].pop("unexpected-finished")
        with self.assertRaisesRegex(RuntimeError, "DRAIN_EXECUTION_REUSED"):
            self.checkpoint.verify(self.path, self.initial_drains)
        with self.assertRaisesRegex(RuntimeError, "EXACT_DRAIN_EXECUTIONS_REQUIRED"):
            self.checkpoint.verify(self.path, [])
        self.verify()

    def test_encrypted_evidence_round_trip_tampering_and_fail_closed(self):
        with tempfile.TemporaryDirectory(prefix="synthetic-recovery-key-") as workspace:
            root = Path(workspace)
            certificate, key = root / "recipient.pem", root / "recipient.key"
            subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-sha256",
                            "-days", "1", "-addext", "subjectKeyIdentifier=hash", "-subj", "/CN=Synthetic Recovery Test", "-keyout", str(key),
                            "-out", str(certificate)], capture_output=True, check=True)
            env = dict(self.checkpoint.env, RECOVERY_EVIDENCE_ENCRYPTION_REQUIRED="true",
                       RECOVERY_EVIDENCE_CERTIFICATE_PEM=certificate.read_text())
            self.checkpoint = module.Checkpoint(env)
            self.checkpoint.read = self.read
            state = json.loads(self.path.read_text())
            state["binding"] = self.checkpoint.binding
            self.checkpoint.save(self.path, state)
            self.candidate()
            self.verify()
            encrypted = self.path.with_suffix(".p7m")
            def decrypt():
                return subprocess.run(["openssl", "cms", "-decrypt", "-binary", "-inform", "DER",
                                       "-in", str(encrypted), "-recip", str(certificate), "-inkey", str(key)],
                                      capture_output=True)
            result = decrypt()
            self.assertEqual(0, result.returncode)
            self.assertEqual(self.path.read_bytes(), result.stdout)
            self.assertNotIn(self.checkpoint.server_id.encode(), encrypted.read_bytes())
            self.assertNotIn(b"Synthetic Recovery Test", encrypted.read_bytes())
            with patch.object(module.os, "replace", side_effect=OSError("encrypted storage failure")):
                with self.assertRaises(OSError):
                    self.checkpoint.save(self.path, {"phase": "must-not-advance"})
            self.assertEqual("verified", json.loads(self.path.read_text())["phase"])
            ciphertext = bytearray(encrypted.read_bytes())
            ciphertext[-4] ^= 1  # Alter the GCM authentication tag, preserving DER structure.
            encrypted.write_bytes(ciphertext)
            self.assertNotEqual(0, decrypt().returncode)
            prior = self.path.read_bytes()
            with patch.object(module.subprocess, "run", side_effect=OSError("encrypt failure")):
                with self.assertRaises(OSError):
                    self.checkpoint.save(self.path, {"phase": "must-not-advance"})
            self.assertEqual(prior, self.path.read_bytes())
            self.checkpoint.certificate = "invalid-public-certificate"
            with self.assertRaisesRegex(RuntimeError, "EVIDENCE_ENCRYPTION_FAILED"):
                self.checkpoint.save(self.path, {"phase": "must-not-advance"})
            self.assertEqual(prior, self.path.read_bytes())
            for value in ("", "invalid", "-----BEGIN PRIVATE KEY-----\nsecret\n-----END PRIVATE KEY-----"):
                env = dict(self.checkpoint.env, RECOVERY_EVIDENCE_ENCRYPTION_REQUIRED="true",
                           RECOVERY_EVIDENCE_CERTIFICATE_PEM=value)
                with self.assertRaises(RuntimeError):
                    module.Checkpoint(env)

    def candidate(self):
        self.backups.append(self.fresh)
        self.checkpoint.wait(self.path, self.drained, self.initial_drains)

    def logical_mode(self, create=None, verify=None):
        self.checkpoint.evidence_kind = "VerifiedLogicalDatabaseBackup"
        self.checkpoint.logical_config = {"binding": self.checkpoint.binding}
        evidence = {"binding": self.checkpoint.binding, "evidenceKind": "VerifiedLogicalDatabaseBackup",
                    "drainedAtUtc": self.drained, "startedAtUtc": "2026-01-02T00:00:11Z",
                    "completedAtUtc": "2026-01-02T00:00:18Z"}
        self.checkpoint.logical_module = SimpleNamespace(
            RecoveryError=type("RecoveryError", (RuntimeError,), {}),
            create_backup=create or (lambda _: copy.deepcopy(evidence)),
            verify_backup=verify or (lambda *_: None))
        return evidence

    def test_logical_backup_uses_same_freeze_and_final_drain_without_new_azure_snapshot(self):
        self.logical_mode()
        self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        state = json.loads(self.path.read_text())
        self.assertEqual("VerifiedLogicalDatabaseBackup", state["evidenceKind"])
        self.assertNotIn("backup", state)
        self.assertEqual(1, len(self.backups))
        self.verify()
        self.assertEqual("verified", json.loads(self.path.read_text())["phase"])

    def test_logical_backup_failed_creation_never_produces_candidate(self):
        def fail(_):
            raise RuntimeError("private diagnostic must not escape")
        self.logical_mode(create=fail)
        with self.assertRaisesRegex(RuntimeError, "^LOGICAL_BACKUP_FAILED$"):
            self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.assertEqual("armed", json.loads(self.path.read_text())["phase"])
        self.assertEqual("UNEXPECTED_FAILURE", json.loads(self.path.read_text())["logicalBackupFailureCode"])
        self.assertNotIn("private diagnostic", self.path.read_text())

    def test_simultaneous_observation_and_worker_failure_preserves_both_boundaries(self):
        self.logical_mode()
        released = threading.Event()
        real_quiet = self.checkpoint.quiet
        calls = []
        def create(_):
            released.wait(2)
            raise self.checkpoint.logical_module.RecoveryError("RESTORED_INVENTORY_MISMATCH")
        def quiet(state):
            calls.append(1)
            if len(calls) > 1:
                released.set()
                raise module.AzureReadError("AZURE_READ_FAILED", {"command": "containerapp replica list"})
            real_quiet(state)
        self.checkpoint.logical_module.create_backup = create
        self.checkpoint.quiet = quiet
        result = Future.result
        with patch.object(Future, "result", lambda future, timeout=None: result(
                future, min(timeout, 0.01) if timeout is not None else None)):
            with self.assertRaisesRegex(module.AzureReadError, "^AZURE_READ_FAILED$"):
                self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        state = json.loads(self.path.read_text())
        self.assertEqual("armed", state["phase"])
        self.assertEqual("RESTORED_INVENTORY_MISMATCH", state["logicalBackupFailureCode"])

    def test_worker_completing_at_wait_timeout_is_still_validated(self):
        self.logical_mode()
        result = Future.result
        def finish_at_timeout(future, timeout=None):
            value = result(future, timeout)
            if timeout is not None:
                raise module.FutureTimeout()
            return value
        with patch.object(Future, "result", finish_at_timeout):
            self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.assertEqual("candidate", json.loads(self.path.read_text())["phase"])

    def test_logical_failure_preserves_only_driver_codes_in_private_evidence(self):
        self.logical_mode()
        driver_error = self.checkpoint.logical_module.RecoveryError
        for error, expected in (
                (driver_error("CANONICAL_SCHEMA_RESTORE_FAILED"), "CANONICAL_SCHEMA_RESTORE_FAILED"),
                (driver_error("password=private"), "UNEXPECTED_FAILURE"),
                (driver_error("A" * 65), "UNEXPECTED_FAILURE"),
                (driver_error("SAFE_CODE", "private"), "UNEXPECTED_FAILURE"),
                (RuntimeError("CANONICAL_SCHEMA_RESTORE_FAILED"), "UNEXPECTED_FAILURE"),
                (MemoryError("private"), "UNEXPECTED_FAILURE")):
            with self.subTest(error_type=type(error).__name__, expected=expected):
                def fail(_, error=error):
                    raise error
                self.checkpoint.logical_module.create_backup = fail
                with self.assertRaisesRegex(RuntimeError, "^LOGICAL_BACKUP_FAILED$"):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)
                state = json.loads(self.path.read_text())
                self.assertEqual("armed", state["phase"])
                self.assertEqual(expected, state["logicalBackupFailureCode"])
                self.assertEqual(0, self.path.stat().st_mode & 0o077)
                self.assertNotIn("private", self.path.read_text())

    def test_generated_window_is_accepted_by_real_logical_driver(self):
        driver_spec = importlib.util.spec_from_file_location(
            "logical_driver_boundary", Path(__file__).with_name("postgres-logical-recovery.py"))
        driver = importlib.util.module_from_spec(driver_spec)
        sys.modules[driver_spec.name] = driver
        driver_spec.loader.exec_module(driver)
        root = Path(self.directory.name)
        certificate, key, ca = (root / name for name in ("certificate", "key", "ca"))
        for path in (certificate, key, ca):
            path.write_text("synthetic file\n")
            path.chmod(0o600)
        self.checkpoint.binding.update(
            evidenceCertificateSha256=driver._certificate_binding_sha256(certificate),
            evidenceKind="VerifiedLogicalDatabaseBackup", logicalConfigSha256="b" * 64)
        evidence = self.logical_mode()
        self.checkpoint.logical_config.update(
            archiveDir=str(root / "archive"), certificatePath=str(certificate),
            privateKeyPath=str(key), sslRootCertPath=str(ca),
            postgresImage="sha256:" + "c" * 64, relayPort=15432,
            targets=[dict(code=code, dbname=code.lower() + "_db", host=self.checkpoint.host,
                          user="synthetic_reader", password="synthetic_password",
                          runtimeRole=code.lower() + "_runtime")
                     for code in ("DIRECTORY", "CHEONGJU", "OSAN")])
        state = json.loads(self.path.read_text())
        state["binding"] = self.checkpoint.binding
        self.checkpoint.save(self.path, state)
        parsed = []
        def create(config):
            parsed.append(driver._parse_config(config))
            return evidence
        self.checkpoint.logical_module.create_backup = create
        verified = []
        self.checkpoint.logical_module.verify_backup = lambda _, config: verified.append(driver._parse_config(config))
        self.checkpoint.wait(self.path, self.drained.replace("Z", "+00:00"), self.initial_drains)
        self.assertEqual(1, len(parsed))
        self.assertTrue(parsed[0].deadline_utc.endswith("Z"))
        self.assertEqual(driver._instant(self.drained, "INVALID"),
                         driver._instant(parsed[0].drained_at_utc, "INVALID"))
        self.verify()
        self.assertEqual(2, len(verified))
        remaining = driver._instant(verified[-1].deadline_utc, "INVALID") - self.now
        self.assertGreater(remaining, timedelta(seconds=299))
        self.assertLessEqual(remaining, timedelta(seconds=300))

    def test_logical_backup_wrong_binding_or_old_time_rejected(self):
        evidence = self.logical_mode()
        for field, value in (("binding", {}), ("drainedAtUtc", "2026-01-01T00:00:00Z"),
                             ("startedAtUtc", self.drained), ("completedAtUtc", "2026-01-03T00:00:00Z"),
                             ("evidenceKind", "AzureAvailableFullBackup")):
            with self.subTest(field=field):
                bad = dict(evidence, **{field: value})
                self.checkpoint.logical_module.create_backup = lambda _, item=bad: item
                with self.assertRaisesRegex(RuntimeError, "LOGICAL_BACKUP_EVIDENCE_INVALID"):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)
                self.assertEqual("armed", json.loads(self.path.read_text())["phase"])

    def test_logical_backup_detects_app_or_finished_job_during_creation(self):
        evidence = self.logical_mode()
        for change in ("app", "job"):
            def create(_, change=change):
                if change == "app":
                    self.active = True
                else:
                    self.executions["maintenance"]["unexpected"] = {"status": "Succeeded"}
                return evidence
            self.checkpoint.logical_module.create_backup = create
            with self.assertRaisesRegex(RuntimeError, "APP_REACTIVATED|UNEXPECTED_JOB_EXECUTION"):
                self.checkpoint.wait(self.path, self.drained, self.initial_drains)
            self.active = False
            self.executions["maintenance"].pop("unexpected", None)
            self.assertEqual("armed", json.loads(self.path.read_text())["phase"])

    def test_logical_final_verification_rechecks_archives_and_rejects_tampering(self):
        self.logical_mode()
        self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        def fail(*_):
            raise RuntimeError("private path or ciphertext details")
        self.checkpoint.logical_module.verify_backup = fail
        with self.assertRaisesRegex(RuntimeError, "^LOGICAL_BACKUP_EVIDENCE_INVALID$"):
            self.verify()
        self.assertEqual("candidate", json.loads(self.path.read_text())["phase"])

    def test_recovery_kind_is_explicit_and_configuration_cannot_silently_change_default(self):
        for patch_values, reason in (({"RECOVERY_EVIDENCE_KIND": "unknown"}, "INVALID_RECOVERY_EVIDENCE_KIND"),
                                     ({"RECOVERY_LOGICAL_BACKUP_CONFIG": "/tmp/private.json"}, "LOGICAL_BACKUP_CONFIGURATION_INVALID"),
                                     ({"RECOVERY_EVIDENCE_KIND": "VerifiedLogicalDatabaseBackup"}, "LOGICAL_BACKUP_CONFIGURATION_INVALID")):
            with self.subTest(patch_values=patch_values), self.assertRaisesRegex(RuntimeError, reason):
                module.Checkpoint(dict(self.checkpoint.env, **patch_values))

    def test_selects_exact_available_full_backup_and_requires_final_verification(self):
        self.candidate()
        state = json.loads(self.path.read_text())
        self.assertEqual("candidate", state["phase"])
        self.assertEqual(self.fresh["completedTime"], state["backup"]["completedTime"])
        self.assertEqual(self.drained, state["drainedAtUtc"])
        self.verify()
        state = json.loads(self.path.read_text())
        self.assertEqual("verified", state["phase"])
        self.assertEqual(self.checkpoint.binding, state["binding"])
        self.assertEqual(0o600, self.path.stat().st_mode & 0o777)
        self.assertTrue(all("create" not in call and "restore" not in call for call in self.calls))

    def test_old_and_equal_backup_times_never_pass(self):
        self.backups.append(self.backup("equal", self.drained))
        with self.assertRaisesRegex(RuntimeError, "BACKUP_WAIT_EXPIRED"):
            self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.assertEqual("armed", json.loads(self.path.read_text())["phase"])

    def test_new_backup_can_appear_after_a_bounded_wait(self):
        def publish(_):
            self.backups.append(self.fresh)
        with patch.object(module.time, "sleep", publish):
            self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.assertEqual("candidate", json.loads(self.path.read_text())["phase"])

    def test_malformed_foreign_duplicate_future_and_incomplete_backups_fail_closed(self):
        invalid = []
        for key, value in [("id", "/other/server/backups/new"), ("type", "other/type"),
                           ("completedTime", "2026-01-02T01:00:00Z"), ("completedTime", "2026-01-02T00:00:15"),
                           ("completedTime", "bad"), ("backupType", "Unknown")]:
            item = dict(self.fresh)
            item[key] = value
            invalid.append([item])
        invalid.extend([[self.fresh, self.fresh], {"value": [self.fresh], "nextLink": "unread-page"}])
        for values in invalid:
            with self.subTest(values=values):
                self.backups = values
                with self.assertRaises((RuntimeError, ValueError)):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)
                self.assertEqual("armed", json.loads(self.path.read_text())["phase"])

    def test_on_demand_and_nonautomatic_backups_are_not_selected(self):
        for key, value in [("backupType", "CustomerOnDemand"), ("source", "Customer Initiated")]:
            with self.subTest(key=key):
                self.backups = [dict(self.fresh, **{key: value})]
                with self.assertRaisesRegex(RuntimeError, "BACKUP_WAIT_EXPIRED"):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)

    def test_source_state_retention_and_restore_range_are_rechecked(self):
        original = copy.deepcopy(self.server)
        for target, key, value in [("root", "id", "other"), ("root", "fullyQualifiedDomainName", "other"),
                                  ("root", "state", "Updating"), ("backup", "backupRetentionDays", 7),
                                  ("backup", "earliestRestoreDate", "2026-01-02T01:00:00Z")]:
            with self.subTest(key=key):
                self.server = copy.deepcopy(original)
                (self.server if target == "root" else self.server["backup"])[key] = value
                with self.assertRaises(RuntimeError):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)

    def test_quiescence_and_job_changes_block_candidate_and_verification(self):
        for attribute in ("active", "replicas", "running", "extra_job"):
            with self.subTest(attribute=attribute):
                setattr(self, attribute, True)
                with self.assertRaises(RuntimeError):
                    self.checkpoint.wait(self.path, self.drained, self.initial_drains)
                setattr(self, attribute, False)
        self.candidate()
        self.active = True
        with self.assertRaises(RuntimeError):
            self.verify()

    def quiet_state(self):
        self.checkpoint.allowed_drains = set(self.initial_drains)
        self.checkpoint.deadline = time.monotonic() + 5
        return json.loads(self.path.read_text())

    def test_quiet_uses_two_revision_reads_for_130_revisions_and_no_replica_reads(self):
        state = self.quiet_state()
        original = self.checkpoint.read
        revision_list_calls = []
        replica_list_calls = []

        def revisions(*args):
            if args[:3] == ("containerapp", "revision", "list"):
                revision_list_calls.append(args)
                self.assertIn("--all", args)
                app = args[args.index("--name") + 1]
                return [{"name": f"{app}-{index}", "properties": {
                    "active": False, "replicas": 0}} for index in range(65)]
            if args[:3] == ("containerapp", "replica", "list"):
                replica_list_calls.append(args)
            return original(*args)

        self.checkpoint.read = revisions
        self.checkpoint.quiet(state)
        self.assertEqual(2, len(revision_list_calls))
        self.assertEqual([], replica_list_calls)

    def test_quiet_revision_summary_fails_closed_for_every_unsafe_shape(self):
        state = self.quiet_state()
        original = self.checkpoint.read
        valid = {"name": "inactive", "properties": {"active": False, "replicas": 0}}
        for revisions_value, error in (
                ([], "REVISION_LIST_INVALID"),
                ({"value": [valid]}, "REVISION_LIST_INVALID"),
                ([None], "REVISION_LIST_INVALID"),
                ([{"name": "", "properties": {"active": False, "replicas": 0}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "invalid/name", "properties": {"active": False, "replicas": 0}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "duplicate", "properties": {"active": False, "replicas": 0}},
                  {"name": "duplicate", "properties": {"active": False, "replicas": 0}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "inactive", "properties": {"active": False}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "inactive", "properties": {"active": False, "replicas": -1}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "inactive", "properties": {"active": False, "replicas": "0"}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "inactive", "properties": {"active": False, "replicas": False}}],
                 "REVISION_LIST_INVALID"),
                ([{"name": "active", "properties": {"active": True, "replicas": 0}}],
                 "APP_REACTIVATED"),
                ([{"name": "inactive", "properties": {"active": False, "replicas": 1}}],
                 "REPLICA_STILL_RUNNING")):
            with self.subTest(error=error):
                def unsafe(*args, revisions_value=revisions_value):
                    if args[:3] == ("containerapp", "revision", "list"):
                        return copy.deepcopy(revisions_value)
                    return original(*args)
                self.checkpoint.read = unsafe
                with self.assertRaisesRegex(RuntimeError, f"^{error}$"):
                    self.checkpoint.quiet(state)

        def read_failure(*args):
            if args[:3] == ("containerapp", "revision", "list"):
                raise RuntimeError("AZURE_READ_FAILED")
            return original(*args)
        self.checkpoint.read = read_failure
        with self.assertRaisesRegex(RuntimeError, "^AZURE_READ_FAILED$"):
            self.checkpoint.quiet(state)

    def test_quiet_still_rejects_new_jobs_after_valid_revision_summary(self):
        state = self.quiet_state()
        self.extra_job = True
        with self.assertRaisesRegex(RuntimeError, "^JOB_SET_CHANGED$"):
            self.checkpoint.quiet(state)

    def test_selected_backup_disappearance_or_range_change_blocks_verification(self):
        self.candidate()
        self.backups = []
        with self.assertRaisesRegex(RuntimeError, "SELECTED_BACKUP_NO_LONGER_AVAILABLE"):
            self.verify()
        self.backups = [self.fresh]
        self.server["backup"]["earliestRestoreDate"] = "2026-01-02T00:00:16Z"
        with self.assertRaises(RuntimeError):
            self.verify()

    def test_api_error_and_failed_evidence_write_do_not_advance_phase(self):
        with patch.object(self.checkpoint, "read", side_effect=RuntimeError("read failure")):
            with self.assertRaises(RuntimeError):
                self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.backups.append(self.fresh)
        with patch.object(module.os, "replace", side_effect=OSError("disk failure")):
            with self.assertRaises(OSError):
                self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.assertEqual("armed", json.loads(self.path.read_text())["phase"])
        self.assertEqual([self.path], list(self.path.parent.iterdir()))

    def test_binding_replay_time_and_nonprivate_evidence_are_rejected(self):
        original = self.path.read_text()
        for key, value in [("sourceSha", "b" * 40), ("releaseId", "other"), ("serverId", "other")]:
            state = json.loads(original)
            state["binding"][key] = value
            self.path.write_text(json.dumps(state))
            with self.assertRaises(RuntimeError):
                self.checkpoint.wait(self.path, self.drained, self.initial_drains)
        self.path.write_text(original)
        for drained in ["2025-01-01T00:00:00Z", "2099-01-01T00:00:00Z", "", None]:
            with self.assertRaises((RuntimeError, ValueError)):
                self.checkpoint.wait(self.path, drained, self.initial_drains)
        os.chmod(self.path, 0o644)
        with self.assertRaises(RuntimeError):
            self.checkpoint.wait(self.path, self.drained, self.initial_drains)

    def test_main_surfaces_only_allowlisted_reason_and_phase(self):
        arguments = ["azure-recovery-checkpoint.py", "wait", "--state", str(self.path)]
        for error, expected_reason in [
                (RuntimeError("BACKUP_WAIT_EXPIRED"), "BACKUP_WAIT_EXPIRED"),
                (RuntimeError("AZURE_READ_TIMEOUT"), "AZURE_READ_TIMEOUT"),
                (RuntimeError("/subscriptions/private-id"), "UNEXPECTED_FAILURE"),
                (OSError("synthetic-secret-value-must-not-log"), "UNEXPECTED_FAILURE")]:
            with self.subTest(expected_reason=expected_reason):
                output = io.StringIO()
                with patch.object(sys, "argv", arguments), patch.object(module, "Checkpoint", side_effect=error), redirect_stderr(output):
                    self.assertEqual(1, module.main())
                self.assertEqual(
                    "recoveryCheckpoint=FAILED_NO_DATABASE_CHANGE_ALLOWED"
                    f" phase=wait reason={expected_reason}\n",
                    output.getvalue())
                self.assertNotIn("private-id", output.getvalue())
                self.assertNotIn("synthetic-secret", output.getvalue())


if __name__ == "__main__":
    unittest.main()
