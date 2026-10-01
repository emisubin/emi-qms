#!/usr/bin/env python3
"""Synthetic Azure responses shared by the two release-runner integration tests."""
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import sys

a = sys.argv[1:]
first = "MOCK_STATE" in os.environ
root = Path(os.environ["MOCK_STATE"] if first else os.environ["AZURE_RELEASE_TEST_STATE"])
scenario = os.environ["SCENARIO"] if first else os.environ["AZURE_RELEASE_TEST_SCENARIO"]
env = os.environ
now = datetime.now(timezone.utc)
server_id = (f'/subscriptions/{env["AZURE_SUBSCRIPTION_ID"]}/resourceGroups/{env["AZURE_RESOURCE_GROUP"]}'
             '/providers/Microsoft.DBforPostgreSQL/flexibleServers/' + env["RECOVERY_POSTGRES_SERVER_NAME"])


def arg(key):
    return a[a.index(key) + 1]


if first:
    state = json.loads((root / "state.json").read_text())
    active = state["active"]
    drained_count = len(state["drained"])
else:
    active = {env[k]: (root / (env[k] + "-active")).read_text().strip() == "1"
              for k in ("BACKEND_APP_NAME", "FRONTEND_APP_NAME")}
    drained_count = len((root / "drain-completed").read_text().splitlines())
quiet = not any(active.values())
with (root / "recovery-reads").open("a") as stream:
    stream.write("recovery-read:" + " ".join(a[:4]) + "\n")

if a[:3] == ["postgres", "flexible-server", "show"]:
    out = {"id": server_id, "fullyQualifiedDomainName": env["RECOVERY_POSTGRES_SERVER_NAME"] + ".postgres.database.azure.com",
           "state": "Ready", "backup": {"backupRetentionDays": 14,
           "earliestRestoreDate": (now - timedelta(days=13)).isoformat()}}
    if scenario == "recovery-wrong-server":
        out["id"] = server_id + "-other"
elif a[:4] == ["postgres", "flexible-server", "backup", "list"]:
    def backup(name, completed):
        return dict(id=server_id + "/backups/" + name, name=name, type="Microsoft.DBforPostgreSQL/flexibleServers/backups",
                    backupType="Full", source="Automatic", completedTime=completed)
    out = [backup("old", (now - timedelta(hours=1)).isoformat())]
    if quiet and drained_count >= 3:
        if scenario == "recovery-read-failed":
            print("synthetic-secret-value-must-not-log", file=sys.stderr)
            sys.exit(1)
        if scenario not in ("recovery-timeout", "recovery-final-backup-missing") or (
                scenario == "recovery-final-backup-missing" and drained_count < 6):
            path = root / "recovery-backup.json"
            if not path.exists():
                path.write_text(json.dumps(backup("new", now.isoformat())))
            value = json.loads(path.read_text())
            if scenario == "recovery-foreign-backup": value["id"] = "/different/server/backups/new"
            if scenario == "recovery-future-backup": value["completedTime"] = (now + timedelta(hours=1)).isoformat()
            if scenario == "recovery-missing-time": value.pop("completedTime")
            out.append(value)
        if scenario == "recovery-incomplete-list": out = {"value": out, "nextLink": "unread-page"}
        if scenario == "recovery-evidence-failed":
            pattern = "pms-first-maintenance-*/recovery.json" if first else "pms-recovery-checkpoint.*/recovery.json"
            for path in root.glob(pattern): os.chmod(path.parent, 0o755)
elif a[:3] == ["containerapp", "revision", "list"]:
    if quiet and scenario == "recovery-revision-read-failed":
        print("synthetic-secret-value-must-not-log", file=sys.stderr)
        sys.exit(1)
    out = [{"name": arg("--name") + "--old", "properties": {
        "active": active[arg("--name")] or (quiet and scenario == "recovery-active-app")}}]
    if quiet and scenario in ("recovery-multiple-inactive", "recovery-replica-running"):
        out.extend({"name": arg("--name") + f"--inactive-{index}",
                    "properties": {"active": False}} for index in range(1, 4))
    if quiet and scenario == "recovery-mixed-active-revision":
        out.append({"name": arg("--name") + "--unexpected-active",
                    "properties": {"active": True}})
    if "--all" not in a:
        out = [revision for revision in out if revision["properties"]["active"]]
elif a[:3] == ["containerapp", "replica", "list"]:
    out = ([{}] if active[arg("--name")] or (
        quiet and scenario == "recovery-replica-running"
        and arg("--revision").endswith("--inactive-2")) else [])
elif a[:3] == ["containerapp", "job", "list"]:
    out = [{"name": env[k], "properties": {"configuration": {"triggerType": "Manual"}}}
           for k in ("MIGRATION_JOB_NAME", "MAINTENANCE_JOB_NAME", "DATABASE_BOOTSTRAP_JOB_NAME", "MEMBERSHIP_BACKFILL_JOB_NAME")
           if env.get(k)]
elif a[:4] == ["containerapp", "job", "execution", "list"]:
    if first:
        out = [{"name": key, "properties": {field: value.get(field) for field in ("status", "startTime", "endTime")}}
               for key, value in state["executions"].items() if value["job"] == arg("--name")]
    else:
        job = arg("--name")
        out = [{"name": "synthetic-drain-" + str(index), "properties": {"status": "Succeeded"}}
               for index in range(1, drained_count + 1)] if job == env["MIGRATION_JOB_NAME"] else []
    if quiet and scenario == "recovery-job-running":
        out.append({"name": "inflight", "properties": {"status": "Running"}})
    if quiet and scenario == "recovery-terminal-job":
        out.append({"name": "unexpected-finished", "properties": {"status": "Succeeded"}})
else:
    raise AssertionError(a)
print(json.dumps(out))
