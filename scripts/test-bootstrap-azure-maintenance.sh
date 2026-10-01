#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/pms-first-rollout-test.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT
cat >"$scratch/az" <<'PY'
#!/usr/bin/env python3
import json, os, sys, subprocess
from datetime import datetime, timezone
from pathlib import Path
a=sys.argv[1:]; root=Path(os.environ['MOCK_STATE']); scenario=os.environ['SCENARIO']
def arg(k): return a[a.index(k)+1]
state=root/'state.json'
s=json.loads(state.read_text()) if state.exists() else {
 'active':{'backend':True,'frontend':True},'updated':[],
 'maintenance':{'CHEONGJU':'Idle','OSAN':'Idle'},'executions':{},'drained':[]}
def out(v): print(json.dumps(v))
def save(): state.write_text(json.dumps(s))
if not state.exists():save()
if '--only-show-errors' in a:
 sys.exit(subprocess.run([sys.executable,os.environ['RECOVERY_MOCK_SCRIPT'],*a]).returncode)
name=arg('--name') if '--name' in a else ''
with (root/'calls').open('a') as f: f.write(' '.join(a[:3])+':'+name+'\n')
if a[:2]==['account','show']: out({'id':'subscription'})
elif a[:3]==['containerapp','revision','list']:
 out([{'name':name+'--old','properties':{'active':s['active'][name]}}])
elif a[:3]==['containerapp','revision','show']: out({'properties':{'healthState':'Healthy','runningState':'Running'}})
elif a[:3]==['containerapp','revision','deactivate']:
 if scenario=='stop-failure' and name=='backend': sys.exit(1)
 s['active'][name]=False;save();out({})
elif a[:3]==['containerapp','revision','activate']: s['active'][name]=True;save();out({})
elif a[:3]==['containerapp','replica','list']:
 out([{'name':'replica'}] if s['active'][name] else [])
elif a[:2]==['containerapp','show']:
 image=os.environ[name.upper()+'_RELEASE_IMAGE'] if name in s['updated'] else 'registry/pms-'+name+':'+'a'*40
 out({'properties':{'configuration':{'activeRevisionsMode':'Single'},'latestRevisionName':name+'--old','latestReadyRevisionName':name+'--old','provisioningState':'Succeeded','template':{'containers':[{'image':image,'env':[{'name':'Secret','secretRef':'secret-ref'}]}]}}})
elif a[:2]==['containerapp','update']:
 if scenario in ['update-failure','fail-cleanup-cheongju-running'] and name=='frontend':sys.exit(1)
 s['updated'].append(name);s['active'][name]=True;save();out({})
elif a[:3]==['containerapp','job','show']:
 entries=[{'name':'ASPNETCORE_ENVIRONMENT','value':'Production'},{'name':'BusinessUnits__Enabled','value':'true'}]
 if scenario.startswith('maintenance-stale-') and name=='maintenance':entries.append({'name':scenario[18:],'value':'true'})
 if scenario.startswith('stale-') and name=='migration':entries.append({'name':scenario[6:],'value':'true'})
 if scenario.startswith('duplicate-') and name=='migration':entries.append({'name':scenario[10:],'value':'true'})
 entries.append({'name':'SyntheticPrivateMarker','value':'synthetic-secret-value-must-not-log'})
 entries += [{'name':'ConnectionStrings__Qms'+t+p,'secretRef':'secret-'+t.lower()+'-'+p.lower()} for t in ['Directory','Cheongju','Osan'] for p in ['Runtime','Migration']]
 container={'name':name,'image':'old','args':['--migrate-only'] if name=='migration' else ['--maintenance-prepare'],'env':entries,'resources':{'cpu':0.5,'memory':'1Gi'}}
 out({'properties':{'configuration':{'triggerType':'Manual','replicaRetryLimit':1 if scenario=='unsafe-retry' else 0,'manualTriggerConfig':{'parallelism':2 if scenario=='unsafe-parallel' else 1,'replicaCompletionCount':2 if scenario=='unsafe-completion' else 1}},'template':{'containers':[container,container] if scenario=='unsafe-containers' else [container]}}})
elif a[:4]==['containerapp','job','execution','list']:out([])
elif a[:3]==['containerapp','job','update']:out({})
elif a[:3]==['containerapp','job','start']:
 payload=json.loads(Path(arg('--yaml')).read_text());c=payload['containers'][0]
 entries={e['name']:e.get('value') for e in c['env']}
 assert len(entries)==len(c['env'])
 by_name={e['name']:e for e in c['env']}
 for key,value in {'ASPNETCORE_ENVIRONMENT':'Production','BusinessUnits__Enabled':'true',
                   'SyntheticPrivateMarker':'synthetic-secret-value-must-not-log'}.items():
  assert by_name[key]=={'name':key,'value':value}, 'template value changed'
 for target_name in ['Directory','Cheongju','Osan']:
  for purpose in ['Runtime','Migration']:
   key='ConnectionStrings__Qms'+target_name+purpose
   assert by_name[key]=={'name':key,'secretRef':'secret-'+target_name.lower()+'-'+purpose.lower()}, 'secret reference changed'
 if name=='migration':
  target=entries['Database__MigrationTarget']
  assert target in ['DIRECTORY','CHEONGJU','OSAN']
  command=c['args'][0]
  assert command in ['--deployment-drain-check','--migrate-only']
  mode='drain' if command=='--deployment-drain-check' else 'migration'
  assert c['image']==os.environ['BACKEND_RELEASE_IMAGE']
  assert entries['DeploymentDrain__RequireMaintenance']=='false'
  assert 'DeploymentDrain__ReleaseId' not in entries
  snapshot_key='DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256'
  if mode=='drain' and target=='OSAN' and scenario=='mail-exception':
   assert entries[snapshot_key]==os.environ['ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256']
  else:assert snapshot_key not in entries
  expected=os.environ.get('BUSINESS_SCHEMA_SEPARATION_APPROVED','false') if mode=='migration' and target!='DIRECTORY' else 'false'
  assert entries['Database__BusinessSchemaSeparationApproved']==expected
  assert not any(s['active'].values()), 'database check while serving'
  with (root/(mode+'-targets')).open('a') as f:f.write(target+'\n')
  assert entries['Database__RecoveryPostgresHost']=='synthetic-pg.postgres.database.azure.com'
  if mode!='drain':
   assert s['drained']==['DIRECTORY','CHEONGJU','OSAN']*2, 'migration before final drain'
   evidence=list(root.glob('pms-first-maintenance-*/recovery.json'))
   assert len(evidence)==1 and json.loads(evidence[0].read_text())['phase']=='verified', 'migration before recovery evidence'
  execution=name+'-'+mode+'-execution-'+target+'-'+str(len(s['executions']))
  s['executions'][execution]={'job':name,'target':target,'mode':mode}
  save()
  if (scenario=='start-uncertain' and mode=='migration') or scenario=='drain-'+target+'-start-uncertain':sys.exit(1)
 else:
  target=entries['Maintenance__BusinessUnit']
  assert target in ['CHEONGJU','OSAN']
  action=c['args'][0].replace('--maintenance-','')
  assert entries['Maintenance__Verified']==str(action=='complete').lower()
  assert c['image']==os.environ['BACKEND_RELEASE_IMAGE']
  with (root/'actions').open('a') as f:f.write(action+'-'+target+'\n')
  execution=name+'-execution-'+action+'-'+target
  s['executions'][execution]={'job':name,'target':target,'action':action}
 save()
 if (scenario=='complete-osan-start-response-lost' and name=='maintenance'
     and action=='complete' and target=='OSAN'):sys.exit(1)
 out({'name':execution})
elif a[:4]==['containerapp','job','execution','show']:
 execution=arg('--job-execution-name');pending=s['executions'][execution]
 if 'status' not in pending:
  action=pending.get('action');target=pending['target'];mode=pending.get('mode')
  if mode=='drain':
   if scenario=='drain-'+target+'-unknown':out({'properties':{'status':'Unknown'}});sys.exit(0)
   if scenario=='drain-'+target+'-running':out({'properties':{'status':'Running'}});sys.exit(0)
   pending['status']='Failed' if scenario=='drain-'+target+'-failed' else 'Succeeded'
   if scenario=='recovery-final-drain-failed' and len(s['drained'])>=3:pending['status']='Failed'
   if pending['status']=='Succeeded':s['drained'].append(target)
   pending['endTime']=None if scenario=='recovery-missing-endtime' else datetime.now(timezone.utc).isoformat()
   save();out({'properties':pending});sys.exit(0)
  if ((scenario=='complete-osan-running' and action=='complete' and target=='OSAN')
      or (scenario=='fail-cleanup-cheongju-running' and action=='fail' and target=='CHEONGJU')):
   out({'properties':{'status':'Running'}});sys.exit(0)
  if scenario=='complete-osan-status-unknown' and action=='complete' and target=='OSAN':
   out({'properties':{'status':'Unknown'}});sys.exit(0)
  terminal_status='Stopped' if (scenario=='activate-osan-failure' and action=='activate' and target=='OSAN') else None
  if terminal_status is None and ((scenario=='migration-failure' and name=='migration') \
   or (scenario=='complete-failure' and action=='complete') \
   or (scenario=='prepare-osan-failure' and action=='prepare' and target=='OSAN') \
   or (scenario=='complete-osan-failure' and action=='complete' and target=='OSAN')):
   terminal_status='Failed'
  fail=terminal_status is not None
  if not fail and name=='maintenance':
   current=s['maintenance'][target]
   transitions={
    'prepare':({'Idle','Completed'},'Announced'),
    'activate':({'Announced'},'Active'),
    'fail':({'Announced','Active','Delayed','Completed','Failed'},'Failed'),
    'complete':({'Active','Delayed','Failed'},'Completed')}
   allowed,next_state=transitions[action]
   fail=current not in allowed
   if not fail:s['maintenance'][target]=next_state
  pending['status']=(terminal_status or 'Failed') if fail else 'Succeeded';save()
 out({'properties':{'status':pending['status']}})
else: raise AssertionError(a)
PY
cat >"$scratch/curl" <<'PY'
#!/usr/bin/env python3
import sys
print('200' if sys.argv[-1].endswith('/health/live') else '401',end='')
PY
chmod +x "$scratch/az" "$scratch/curl"
export FIRST_MAINTENANCE_ROLLOUT_APPROVED=true SOURCE_SHA
SOURCE_SHA="$(printf 'a%.0s' {1..40})"
export AZURE_SUBSCRIPTION_ID=subscription AZURE_RESOURCE_GROUP=synthetic ACR_LOGIN_SERVER=registry
export PUBLIC_HOSTNAME=pms.synthetic.internal BACKEND_APP_NAME=backend FRONTEND_APP_NAME=frontend
export MIGRATION_JOB_NAME=migration MAINTENANCE_JOB_NAME=maintenance
export BACKEND_RELEASE_IMAGE FRONTEND_RELEASE_IMAGE
BACKEND_RELEASE_IMAGE="registry/pms-backend@sha256:$(printf 'b%.0s' {1..64})"
FRONTEND_RELEASE_IMAGE="registry/pms-frontend@sha256:$(printf 'c%.0s' {1..64})"
export MAINTENANCE_RELEASE_ID=11111111-1111-1111-1111-111111111111 MAINTENANCE_ACTOR_USER_ID=22222222-2222-2222-2222-222222222222
export MAINTENANCE_TITLE='Synthetic release' MAINTENANCE_BODY='Synthetic notice'
export MAINTENANCE_STARTS_AT_UTC=2099-01-01T00:00:00Z MAINTENANCE_EXPECTED_ENDS_AT_UTC=2099-01-01T01:00:00Z
export FIRST_ROLLOUT_AZ_BIN="$scratch/az" FIRST_ROLLOUT_HTTP_BIN="$scratch/curl"
export FIRST_ROLLOUT_ALLOW_TEST_OVERRIDES=true FIRST_ROLLOUT_POLL_ATTEMPTS=1 FIRST_ROLLOUT_POLL_INTERVAL_SECONDS=0
export RECOVERY_POSTGRES_SERVER_NAME=synthetic-pg RECOVERY_CHECKPOINT_TIMEOUT_SECONDS=60 RECOVERY_CHECKPOINT_POLL_SECONDS=1
export RECOVERY_MOCK_SCRIPT="$root/scripts/test-support/azure-recovery-mock.py"
scenarios=(success approval-true mail-exception invalid-mail-exception stop-failure migration-failure start-uncertain update-failure \
  complete-failure prepare-osan-failure activate-osan-failure complete-osan-failure \
  complete-osan-start-response-lost complete-osan-running complete-osan-status-unknown \
  fail-cleanup-cheongju-running \
  no-approval malformed-approval unsafe-retry unsafe-parallel unsafe-completion unsafe-containers)
for failure in wrong-server timeout read-failed foreign-backup incomplete-list future-backup missing-time active-app job-running terminal-job final-backup-missing evidence-failed final-drain-failed missing-endtime; do scenarios+=("recovery-${failure}"); done
for target in DIRECTORY CHEONGJU OSAN; do
  for failure in failed unknown running start-uncertain; do scenarios+=("drain-${target}-${failure}"); done
done
for stale in Database__RecoveryPostgresHost Database:RecoveryPostgresHost DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256 DeploymentDrain:AcceptedHistoricalOsanMailAttemptSha256 Database__MigrationTarget Database__BootstrapTarget Database__BusinessSchemaSeparationApproved DeploymentDrain__RequireMaintenance DeploymentDrain__ReleaseId; do scenarios+=("stale-${stale}"); done
# Both exact duplicates and .NET-equivalent spellings are rejected before stop/start.
for stale in database__migrationtarget dAtAbAsE__BootstrapTarget database__businessschemaseparationapproved deploymentdrain__requiremaintenance DeploymentDrain__releaseid \
  Database:MigrationTarget Database:BootstrapTarget Database:BusinessSchemaSeparationApproved DeploymentDrain:RequireMaintenance DeploymentDrain:ReleaseId; do scenarios+=("stale-${stale}"); done
for duplicate in BusinessUnits__Enabled businessunits__enabled BusinessUnits:Enabled businessunits:enabled; do scenarios+=("duplicate-${duplicate}"); done
for stale in Maintenance__BusinessUnit maintenance__businessunit Maintenance:BusinessUnit maintenance:releaseid; do scenarios+=("maintenance-stale-${stale}"); done
case_number=0
for scenario in "${scenarios[@]}"; do
  case_number=$((case_number + 1))
  export SCENARIO="$scenario" MOCK_STATE="$scratch/$case_number-$scenario"
  mkdir "$MOCK_STATE"
  export FIRST_MAINTENANCE_ROLLOUT_APPROVED=true
  export RECOVERY_CHECKPOINT_TIMEOUT_SECONDS=60
  [[ "$scenario" != recovery-timeout ]] || export RECOVERY_CHECKPOINT_TIMEOUT_SECONDS=1
  unset BUSINESS_SCHEMA_SEPARATION_APPROVED ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256
  if [[ "$scenario" == mail-exception ]]; then
    ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256="$(printf '1%.0s' {1..64})"
    export ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256
  fi
  [[ "$scenario" != invalid-mail-exception ]] || export ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256='*'
  [[ "$scenario" != approval-true ]] || export BUSINESS_SCHEMA_SEPARATION_APPROVED=true
  [[ "$scenario" != malformed-approval ]] || export BUSINESS_SCHEMA_SEPARATION_APPROVED=TRUE
  [[ "$scenario" == no-approval ]] && export FIRST_MAINTENANCE_ROLLOUT_APPROVED=false
  status=0
  TMPDIR="$MOCK_STATE" bash "$root/scripts/bootstrap-azure-maintenance.sh" >"$MOCK_STATE/result" 2>&1 || status=$?
  python3 - "$scenario" "$status" "$MOCK_STATE" <<'PY'
import json,sys
from pathlib import Path
scenario,status,folder=sys.argv[1:];root=Path(folder);status=int(status)
assert (status==0)==(scenario in ['success','approval-true','mail-exception']), (scenario,status,(root/'result').read_text())
assert 'synthetic-secret-value-must-not-log' not in (root/'result').read_text()
if scenario=='recovery-wrong-server':
 assert 'recoveryCheckpoint=FAILED_NO_DATABASE_CHANGE_ALLOWED phase=preflight reason=SERVER_IDENTITY_OR_STATE_INVALID' in (root/'result').read_text()
if scenario=='recovery-read-failed':
 assert 'recoveryCheckpoint=FAILED_NO_DATABASE_CHANGE_ALLOWED phase=wait reason=AZURE_READ_FAILED' in (root/'result').read_text()
if scenario in ['no-approval','malformed-approval','invalid-mail-exception']:
 assert not (root/'calls').exists()
elif scenario.startswith(('stale-','maintenance-stale-','duplicate-','unsafe-')) or scenario=='recovery-wrong-server':
 calls=(root/'calls').read_text()
 assert 'containerapp revision deactivate:' not in calls
 assert 'containerapp job start:' not in calls
 assert 'containerapp job update:' not in calls
else:
 state=json.loads((root/'state.json').read_text())
 if scenario in ['success','approval-true','mail-exception','stop-failure'] or scenario.startswith(('drain-','stale-','recovery-')):assert all(state['active'].values())
 else:assert not any(state['active'].values())
 calls=(root/'calls').read_text()
 if scenario=='stop-failure' or scenario.startswith(('drain-','stale-','recovery-')):
  assert not (root/'migration-targets').exists()
  assert 'containerapp job update:migration' not in calls
 if scenario.startswith('drain-'):
  target=scenario.split('-')[1];targets=['DIRECTORY','CHEONGJU','OSAN']
  expected=targets[:targets.index(target)+1]
  assert (root/'drain-targets').read_text().splitlines()==expected
  assert state['drained']==expected[:-1]
 if scenario.startswith('stale-'):assert 'containerapp job start:' not in calls
 if scenario not in ['success','approval-true','mail-exception','stop-failure'] and not scenario.startswith(('drain-','stale-','recovery-')):assert 'containerapp revision activate' not in calls
 if scenario in ['success','approval-true','mail-exception']:
  assert (root/'migration-targets').read_text()=='DIRECTORY\nCHEONGJU\nOSAN\n'
  assert (root/'drain-targets').read_text()=='DIRECTORY\nCHEONGJU\nOSAN\n'*2
  assert state['drained']==['DIRECTORY','CHEONGJU','OSAN']*2
  assert (root/'actions').read_text()=='prepare-CHEONGJU\nprepare-OSAN\nactivate-CHEONGJU\nactivate-OSAN\ncomplete-CHEONGJU\ncomplete-OSAN\n'
  assert state['maintenance']=={'CHEONGJU':'Completed','OSAN':'Completed'}
 expected_actions={
  'prepare-osan-failure':'prepare-CHEONGJU\nprepare-OSAN\nfail-CHEONGJU\nfail-OSAN\n',
  'activate-osan-failure':'prepare-CHEONGJU\nprepare-OSAN\nactivate-CHEONGJU\nactivate-OSAN\nfail-CHEONGJU\nfail-OSAN\n',
  'complete-osan-failure':'prepare-CHEONGJU\nprepare-OSAN\nactivate-CHEONGJU\nactivate-OSAN\ncomplete-CHEONGJU\ncomplete-OSAN\nfail-CHEONGJU\nfail-OSAN\n'}
 if scenario in expected_actions:
  assert (root/'actions').read_text()==expected_actions[scenario]
  expected_states={'prepare-osan-failure':{'CHEONGJU':'Failed','OSAN':'Idle'}}
  assert state['maintenance']==expected_states.get(
   scenario,{'CHEONGJU':'Failed','OSAN':'Failed'})
 uncertain_complete={
  'complete-osan-start-response-lost','complete-osan-running','complete-osan-status-unknown'}
 if scenario in uncertain_complete:
  assert (root/'actions').read_text()=='prepare-CHEONGJU\nprepare-OSAN\nactivate-CHEONGJU\nactivate-OSAN\ncomplete-CHEONGJU\ncomplete-OSAN\n'
  assert state['maintenance']=={'CHEONGJU':'Completed','OSAN':'Active'}
  assert 'maintenance-result-uncertain-manual-reconciliation-required' in (root/'result').read_text()
  assert not any(line.startswith('fail-') for line in (root/'actions').read_text().splitlines())
  # The original execution may complete after the script exits. No competing
  # fail execution was started, so late completion cannot overwrite Failed.
  state['maintenance']['OSAN']='Completed'
  assert state['maintenance']=={'CHEONGJU':'Completed','OSAN':'Completed'}
 if scenario=='fail-cleanup-cheongju-running':
  assert (root/'actions').read_text().endswith('fail-CHEONGJU\n')
  assert 'fail-OSAN\n' not in (root/'actions').read_text()
  assert 'maintenance-fail-cleanup-uncertain-manual-reconciliation-required' in (root/'result').read_text()
 for p in root.glob('pms-first-maintenance-*/migration-execution.json'):raise AssertionError('execution payload retained')
 for p in root.glob('pms-first-maintenance-*/maintenance-execution.json'):raise AssertionError('execution payload retained')

print('firstMaintenanceTest='+scenario+':PASS')
PY
done

printf 'firstMaintenanceTests=PASS cases=%s\n' "${#scenarios[@]}"
