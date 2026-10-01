#!/usr/bin/env python3
import copy
from contextlib import redirect_stderr
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
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
            return [{"name": "synthetic-revision", "properties": {"active": self.active}}]
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
