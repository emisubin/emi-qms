#!/usr/bin/env bash
# One-time, explicitly approved outage rollout. Never used by the normal release workflow.
set -euo pipefail
exec python3 - "$@" <<'PY'
import copy
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile
import time
import uuid
from datetime import datetime, timezone

os.umask(0o077)
env = os.environ

def require(condition, code):
    if not condition:
        raise RuntimeError(code)

def required(name):
    value = env.get(name, '')
    require(bool(value), 'MISSING_' + name)
    return value

try:
    require(required('FIRST_MAINTENANCE_ROLLOUT_APPROVED') == 'true', 'APPROVAL_REQUIRED')
    keys = ['SOURCE_SHA', 'AZURE_SUBSCRIPTION_ID', 'AZURE_RESOURCE_GROUP',
            'ACR_LOGIN_SERVER', 'PUBLIC_HOSTNAME', 'BACKEND_APP_NAME', 'FRONTEND_APP_NAME',
            'MIGRATION_JOB_NAME', 'MAINTENANCE_JOB_NAME', 'BACKEND_RELEASE_IMAGE',
            'FRONTEND_RELEASE_IMAGE', 'MAINTENANCE_RELEASE_ID', 'MAINTENANCE_ACTOR_USER_ID',
            'MAINTENANCE_TITLE', 'MAINTENANCE_BODY', 'MAINTENANCE_STARTS_AT_UTC',
            'MAINTENANCE_EXPECTED_ENDS_AT_UTC']
    cfg = {key: required(key) for key in keys}
    # A trusted, operator-prepared read-only diagnostic is mandatory. It must
    # inspect provider Processing/leases and open backend transactions; it must
    # never clear state or repair data. Do not accept a shell command here.
    drain_check = Path(required('FIRST_ROLLOUT_DRAIN_CHECK_FILE'))
    require(drain_check.is_absolute() and drain_check.suffix == '.py'
            and drain_check.is_file() and not drain_check.is_symlink(), 'INVALID_DRAIN_HELPER')
    drain_stat = drain_check.stat()
    require(drain_stat.st_uid == os.getuid()
            and not drain_stat.st_mode & (stat.S_IWGRP | stat.S_IWOTH), 'UNTRUSTED_DRAIN_HELPER')
    drain_source = drain_check.read_bytes()
    compile(drain_source, str(drain_check), 'exec')
    require(len({cfg[k] for k in ['BACKEND_APP_NAME', 'FRONTEND_APP_NAME']}) == 2
            and cfg['MIGRATION_JOB_NAME'] != cfg['MAINTENANCE_JOB_NAME'], 'DUPLICATE_TARGETS')
    require(bool(re.fullmatch('[0-9a-f]{40}', cfg['SOURCE_SHA'])), 'INVALID_SOURCE')
    for kind in ['BACKEND', 'FRONTEND']:
        require(bool(re.fullmatch(re.escape(cfg['ACR_LOGIN_SERVER']) + '/pms-' + kind.lower()
                                 + '@sha256:[0-9a-f]{64}', cfg[kind + '_RELEASE_IMAGE'])), 'INVALID_IMAGE')
    for key in ['MAINTENANCE_RELEASE_ID', 'MAINTENANCE_ACTOR_USER_ID']:
        require(uuid.UUID(cfg[key]).int != 0, 'INVALID_UUID')
    start = datetime.fromisoformat(cfg['MAINTENANCE_STARTS_AT_UTC'].replace('Z', '+00:00'))
    end = datetime.fromisoformat(cfg['MAINTENANCE_EXPECTED_ENDS_AT_UTC'].replace('Z', '+00:00'))
    require(start.tzinfo is not None and end.tzinfo is not None
            and end > start and end > datetime.now(timezone.utc), 'INVALID_WINDOW')
    require(0 < len(cfg['MAINTENANCE_TITLE'].strip()) <= 100
            and 0 < len(cfg['MAINTENANCE_BODY'].strip()) <= 1800, 'INVALID_NOTICE')
    az = env.get('FIRST_ROLLOUT_AZ_BIN', 'az')
    curl = env.get('FIRST_ROLLOUT_HTTP_BIN', 'curl')
    if az != 'az' or curl != 'curl':
        require(env.get('FIRST_ROLLOUT_ALLOW_TEST_OVERRIDES') == 'true'
                and cfg['PUBLIC_HOSTNAME'] == 'pms.synthetic.internal', 'COMMAND_OVERRIDE_REJECTED')
    attempts = int(env.get('FIRST_ROLLOUT_POLL_ATTEMPTS', '90'))
    interval = int(env.get('FIRST_ROLLOUT_POLL_INTERVAL_SECONDS', '10'))
    require(1 <= attempts <= 999 and 0 <= interval <= 60, 'INVALID_POLL')
except Exception as error:
    print('firstMaintenanceRollout=INVALID_CONFIGURATION', file=sys.stderr)
    sys.exit(63)

state_dir = Path(tempfile.mkdtemp(prefix='pms-first-maintenance-'))
print('firstMaintenanceRolloutEvidence=' + str(state_dir), flush=True)
backend, frontend = cfg['BACKEND_APP_NAME'], cfg['FRONTEND_APP_NAME']
rg_args = ['--resource-group', cfg['AZURE_RESOURCE_GROUP']]
baseline = {}
quiescing = False
migration_started = False
maintenance_active = False

def record(event, **data):
    with (state_dir / 'events.jsonl').open('a') as stream:
        stream.write(json.dumps(dict(event=event, **data)) + '\n')
    print('firstMaintenanceRolloutStep=' + event, flush=True)

def azure(*args):
    # Capture errors without ever printing provider output, configuration or secrets.
    result = subprocess.run([az, *args, '--output', 'json'], capture_output=True, text=True)
    require(result.returncode == 0, 'AZURE_COMMAND_FAILED')
    return json.loads(result.stdout) if result.stdout.strip() else None

def app(name):
    return azure('containerapp', 'show', *rg_args, '--name', name)['properties']

def revisions(name):
    return azure('containerapp', 'revision', 'list', *rg_args, '--name', name)

def active(name):
    return [r['name'] for r in revisions(name) if r['properties'].get('active') is True]

def poll(check, failure):
    for _ in range(attempts):
        if check():
            return
        time.sleep(interval)
    raise RuntimeError(failure)

def ready(name, image):
    p = app(name)
    revision = p.get('latestRevisionName')
    if (not revision or revision != p.get('latestReadyRevisionName')
            or p.get('provisioningState') != 'Succeeded'
            or p['template']['containers'][0]['image'] != image):
        return False
    r = azure('containerapp', 'revision', 'show', *rg_args, '--name', name,
              '--revision', revision)['properties']
    return r.get('healthState') == 'Healthy' and r.get('runningState') in ['Running', 'RunningAtMaxScale']

def security_smoke():
    for path, expected in [('/health/live', '200'), ('/', '401'), ('/api/me', '401')]:
        result = subprocess.run([curl, '--silent', '--show-error', '--output', '/dev/null',
                                 '--write-out', '%{http_code}', '--max-time', '20',
                                 'https://' + cfg['PUBLIC_HOSTNAME'] + path], capture_output=True, text=True)
        require(result.returncode == 0 and result.stdout == expected, 'PUBLIC_SECURITY_FAILED')

def stop(name):
    # Deactivate every active revision; zero minimum scale alone allows scale-up.
    for revision in active(name):
        azure('containerapp', 'revision', 'deactivate', *rg_args, '--name', name, '--revision', revision)
    def empty():
        if active(name):
            return False
        for revision in revisions(name):
            replicas = azure('containerapp', 'replica', 'list', *rg_args,
                             '--name', name, '--revision', revision['name'])
            if replicas:
                return False
        return True
    poll(empty, 'REPLICAS_NOT_STOPPED')
    record('stopped', app=name)

def job(name):
    value = azure('containerapp', 'job', 'show', *rg_args, '--name', name)
    require(value['properties']['configuration']['triggerType'] == 'Manual', 'JOB_NOT_MANUAL')
    executions = azure('containerapp', 'job', 'execution', 'list', *rg_args, '--name', name)
    require(not any(e['properties'].get('status') not in ['Succeeded', 'Failed', 'Stopped']
                    for e in executions), 'JOB_ALREADY_RUNNING')
    return value

def wait_job(name, execution):
    def finished():
        value = azure('containerapp', 'job', 'execution', 'show', *rg_args,
                      '--name', name, '--job-execution-name', execution)
        status = value['properties'].get('status')
        require(status not in ['Failed', 'Stopped'], 'JOB_FAILED')
        return status == 'Succeeded'
    poll(finished, 'JOB_TIMEOUT_INSPECT_EXISTING_EXECUTION')

def start_job(name, *args):
    # The pre-start marker makes a lost Azure response explicitly non-retryable.
    record('job-start-requested', job=name)
    value = azure('containerapp', 'job', 'start', *rg_args, '--name', name, *args)
    execution = value.get('name', '')
    require(bool(execution) and not any(c.isspace() for c in execution), 'EXECUTION_NAME_MISSING')
    record('job-started', job=name, execution=execution)
    wait_job(name, execution)
    record('job-succeeded', job=name, execution=execution)

def maintenance(action):
    template = copy.deepcopy(maintenance_template)
    container = template['containers'][0]
    container['image'] = cfg['BACKEND_RELEASE_IMAGE']
    container['args'] = ['--maintenance-' + action]
    fields = {'ReleaseId': 'RELEASE_ID', 'ActorUserId': 'ACTOR_USER_ID', 'Title': 'TITLE',
              'Body': 'BODY', 'StartsAtUtc': 'STARTS_AT_UTC', 'ExpectedEndsAtUtc': 'EXPECTED_ENDS_AT_UTC'}
    container['env'] += [{'name': 'Maintenance__' + k, 'value': cfg['MAINTENANCE_' + v]}
                         for k, v in fields.items()]
    container['env'].append({'name': 'Maintenance__Verified', 'value': str(action == 'complete').lower()})
    # Azure CLI accepts JSON as YAML, with a JobExecutionTemplate root (containers, not properties).
    path = state_dir / 'maintenance-execution.json'
    path.write_text(json.dumps(template))
    try:
        start_job(cfg['MAINTENANCE_JOB_NAME'], '--yaml', str(path))
        record('maintenance-' + action)
    finally:
        path.unlink(missing_ok=True)

try:
    require(azure('account', 'show')['id'] == cfg['AZURE_SUBSCRIPTION_ID'], 'SUBSCRIPTION_MISMATCH')
    for name in [backend, frontend]:
        p = app(name)
        require(p['configuration']['activeRevisionsMode'] == 'Single', 'UNSAFE_REVISION_MODE')
        current = active(name)
        require(len(current) == 1, 'UNSAFE_ACTIVE_REVISIONS')
        image = p['template']['containers'][0]['image']
        kind = 'backend' if name == backend else 'frontend'
        require(bool(re.fullmatch(re.escape(cfg['ACR_LOGIN_SERVER']) + '/pms-' + kind
                                 + '(?::[0-9a-f]{40}|@sha256:[0-9a-f]{64})', image)), 'UNSAFE_BASELINE_IMAGE')
        require(ready(name, image), 'BASELINE_NOT_READY')
        baseline[name] = dict(image=image, revisions=current,
                              secretRefs=[{'name': e['name'], 'secretRef': e['secretRef']}
                                          for e in p['template']['containers'][0].get('env', []) if 'secretRef' in e])
    security_smoke()
    migration_job = job(cfg['MIGRATION_JOB_NAME'])
    migration_containers = migration_job['properties']['template']['containers']
    require(len(migration_containers) == 1
            and migration_containers[0].get('args') == ['--migrate-only'], 'INVALID_MIGRATION_COMMAND')
    prepared = job(cfg['MAINTENANCE_JOB_NAME'])
    for configured_job in [migration_job, prepared]:
        configuration = configured_job['properties']['configuration']
        require(configuration.get('replicaRetryLimit') == 0
                and configuration.get('manualTriggerConfig', {}).get('parallelism') == 1
                and configuration.get('manualTriggerConfig', {}).get('replicaCompletionCount') == 1,
                'UNSAFE_JOB_RETRY_OR_PARALLELISM')
    maintenance_template = prepared['properties']['template']
    containers = maintenance_template['containers']
    require(len(containers) == 1 and containers[0]['name'] == cfg['MAINTENANCE_JOB_NAME'], 'INVALID_MAINTENANCE_CONTAINER')
    entries = containers[0].get('env', [])
    names = {e['name'] for e in entries}
    require(len(names) == len(entries) and not any(n.startswith('Maintenance__') for n in names), 'STALE_MAINTENANCE_CONFIG')
    values = {e['name']: e.get('value') for e in entries}
    require(values.get('ASPNETCORE_ENVIRONMENT') == 'Production'
            and values.get('BusinessUnits__Enabled') == 'true', 'INVALID_PRODUCTION_CONFIG')
    require(all('ConnectionStrings__Qms' + target + purpose in names
                for target in ['Directory', 'Cheongju', 'Osan'] for purpose in ['Runtime', 'Migration']), 'MISSING_DB_CONFIG')
    require(all(e.get('secretRef') and not e.get('value') for e in entries
                if e['name'].startswith('ConnectionStrings__')), 'PLAINTEXT_DB_CONFIG_REJECTED')
    (state_dir / 'baseline.json').write_text(json.dumps(dict(source=cfg['SOURCE_SHA'], apps=baseline)))
    record('baseline-verified')
    quiescing = True
    stop(frontend)
    stop(backend)
    # Execute the exact helper inspected before mutations, from a private file,
    # so replacing the configured path mid-rollout cannot change its behavior.
    helper_copy = state_dir / 'drain-check.py'
    helper_copy.write_bytes(drain_source)
    try:
        diagnostic = subprocess.run([sys.executable, str(helper_copy)], capture_output=True)
        require(diagnostic.returncode == 0, 'PROVIDER_OR_TRANSACTION_DRAIN_NOT_VERIFIED')
    finally:
        helper_copy.unlink(missing_ok=True)
    record('provider-and-transaction-drain-verified')
    azure('containerapp', 'job', 'update', *rg_args, '--name', cfg['MIGRATION_JOB_NAME'],
          '--image', cfg['BACKEND_RELEASE_IMAGE'])
    # From this point even an uncertain start must leave the outage intact.
    migration_started = True
    record('migration-boundary')
    start_job(cfg['MIGRATION_JOB_NAME'])
    maintenance('prepare')
    maintenance('activate')
    maintenance_active = True
    for name, image in [(backend, cfg['BACKEND_RELEASE_IMAGE']), (frontend, cfg['FRONTEND_RELEASE_IMAGE'])]:
        azure('containerapp', 'update', *rg_args, '--name', name, '--image', image)
        poll(lambda: ready(name, image), 'NEW_REVISION_NOT_READY')
        record('app-ready', app=name)
    security_smoke()
    maintenance('complete')
    maintenance_active = False
    record('complete')
except Exception:
    failed = False
    if migration_started:
        # Never run the previous image against an advanced ledger.
        for name in [frontend, backend]:
            try:
                stop(name)
            except Exception:
                failed = True
        record('outage-forward-fix-required', stopVerificationFailed=failed)
    elif quiescing:
        for name in [backend, frontend]:
            for revision in baseline[name]['revisions']:
                try:
                    azure('containerapp', 'revision', 'activate', *rg_args, '--name', name, '--revision', revision)
                except Exception:
                    failed = True
        try:
            for name in [backend, frontend]:
                poll(lambda: ready(name, baseline[name]['image']), 'RESTORE_NOT_READY')
            security_smoke()
        except Exception:
            failed = True
        record('pre-migration-restoration', verified=not failed)
    else:
        record('preflight-failed-no-mutation')
    print('firstMaintenanceRollout=FAILED_INSPECT_EVIDENCE_NO_AUTOMATIC_RETRY', file=sys.stderr)
    sys.exit(1)
print('firstMaintenanceRollout=PASS')
PY
