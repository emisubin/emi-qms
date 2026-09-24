#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/pms-first-rollout-test.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT
cat >"$scratch/az" <<'PY'
#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
a=sys.argv[1:]; root=Path(os.environ['MOCK_STATE']); scenario=os.environ['SCENARIO']
def arg(k): return a[a.index(k)+1]
state=root/'state.json'
s=json.loads(state.read_text()) if state.exists() else {'active':{'backend':True,'frontend':True},'updated':[]}
def out(v): print(json.dumps(v))
def save(): state.write_text(json.dumps(s))
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
 if scenario=='update-failure' and name=='frontend':sys.exit(1)
 s['updated'].append(name);s['active'][name]=True;save();out({})
elif a[:3]==['containerapp','job','show']:
 entries=[{'name':'ASPNETCORE_ENVIRONMENT','value':'Production'},{'name':'BusinessUnits__Enabled','value':'true'}]
 entries += [{'name':'ConnectionStrings__Qms'+t+p,'secretRef':'secret-'+t.lower()+'-'+p.lower()} for t in ['Directory','Cheongju','Osan'] for p in ['Runtime','Migration']]
 out({'properties':{'configuration':{'triggerType':'Manual','replicaRetryLimit':0,'manualTriggerConfig':{'parallelism':1,'replicaCompletionCount':1}},'template':{'containers':[{'name':name,'image':'old','args':['--migrate-only'] if name=='migration' else ['--maintenance-prepare'],'env':entries,'resources':{'cpu':0.5,'memory':'1Gi'}}]}}})
elif a[:4]==['containerapp','job','execution','list']:out([])
elif a[:3]==['containerapp','job','update']:out({})
elif a[:3]==['containerapp','job','start']:
 if name=='migration':
  assert not any(s['active'].values()), 'migration while serving'
  if scenario=='start-uncertain':sys.exit(1)
 else:
  payload=json.loads(Path(arg('--yaml')).read_text());c=payload['containers'][0]
  action=c['args'][0].replace('--maintenance-','');s['action']=action;save()
  assert c['image']==os.environ['BACKEND_RELEASE_IMAGE']
  assert any(e.get('secretRef') for e in c['env'])
  with (root/'actions').open('a') as f:f.write(action+'\n')
 out({'name':name+'-execution'})
elif a[:4]==['containerapp','job','execution','show']:
 fail=(scenario=='migration-failure' and name=='migration') or (scenario=='complete-failure' and s.get('action')=='complete')
 out({'properties':{'status':'Failed' if fail else 'Succeeded'}})
else: raise AssertionError(a)
PY
cat >"$scratch/curl" <<'PY'
#!/usr/bin/env python3
import sys
print('200' if sys.argv[-1].endswith('/health/live') else '401',end='')
PY
chmod +x "$scratch/az" "$scratch/curl"
cat >"$scratch/drain-check.py" <<'PY'
import json, os, sys
from pathlib import Path
root=Path(os.environ['MOCK_STATE'])
state=json.loads((root/'state.json').read_text())
assert not any(state['active'].values()), 'drain check ran before quiescence'
(root/'drain-checked').write_text('checked')
sys.exit(1 if os.environ['SCENARIO']=='drain-failure' else 0)
PY
chmod 600 "$scratch/drain-check.py"
export FIRST_MAINTENANCE_ROLLOUT_APPROVED=true SOURCE_SHA="$(printf 'a%.0s' {1..40})"
export AZURE_SUBSCRIPTION_ID=subscription AZURE_RESOURCE_GROUP=synthetic ACR_LOGIN_SERVER=registry
export PUBLIC_HOSTNAME=pms.synthetic.internal BACKEND_APP_NAME=backend FRONTEND_APP_NAME=frontend
export MIGRATION_JOB_NAME=migration MAINTENANCE_JOB_NAME=maintenance
export BACKEND_RELEASE_IMAGE="registry/pms-backend@sha256:$(printf 'b%.0s' {1..64})"
export FRONTEND_RELEASE_IMAGE="registry/pms-frontend@sha256:$(printf 'c%.0s' {1..64})"
export MAINTENANCE_RELEASE_ID=11111111-1111-1111-1111-111111111111 MAINTENANCE_ACTOR_USER_ID=22222222-2222-2222-2222-222222222222
export MAINTENANCE_TITLE='Synthetic release' MAINTENANCE_BODY='Synthetic notice'
export MAINTENANCE_STARTS_AT_UTC=2099-01-01T00:00:00Z MAINTENANCE_EXPECTED_ENDS_AT_UTC=2099-01-01T01:00:00Z
export FIRST_ROLLOUT_AZ_BIN="$scratch/az" FIRST_ROLLOUT_HTTP_BIN="$scratch/curl"
export FIRST_ROLLOUT_ALLOW_TEST_OVERRIDES=true FIRST_ROLLOUT_POLL_ATTEMPTS=1 FIRST_ROLLOUT_POLL_INTERVAL_SECONDS=0
for scenario in success stop-failure drain-failure migration-failure start-uncertain update-failure complete-failure no-approval no-drain-helper; do
  export SCENARIO="$scenario" MOCK_STATE="$scratch/$scenario"
  mkdir "$MOCK_STATE"
  export FIRST_MAINTENANCE_ROLLOUT_APPROVED=true
  export FIRST_ROLLOUT_DRAIN_CHECK_FILE="$scratch/drain-check.py"
  [[ "$scenario" == no-approval ]] && export FIRST_MAINTENANCE_ROLLOUT_APPROVED=false
  [[ "$scenario" == no-drain-helper ]] && export FIRST_ROLLOUT_DRAIN_CHECK_FILE="$scratch/missing.py"
  status=0
  TMPDIR="$MOCK_STATE" bash "$root/scripts/bootstrap-azure-maintenance.sh" >"$MOCK_STATE/result" 2>&1 || status=$?
  python3 - "$scenario" "$status" "$MOCK_STATE" <<'PY'
import json,sys
from pathlib import Path
scenario,status,folder=sys.argv[1:];root=Path(folder);status=int(status)
assert (status==0)==(scenario=='success'), (scenario,status,(root/'result').read_text())
if scenario in ['no-approval','no-drain-helper']:
 assert not (root/'calls').exists()
else:
 state=json.loads((root/'state.json').read_text())
 if scenario in ['success','stop-failure','drain-failure']:assert all(state['active'].values())
 else:assert not any(state['active'].values())
 calls=(root/'calls').read_text()
 if scenario in ['stop-failure','drain-failure']:assert 'containerapp job start:migration' not in calls
 if scenario=='drain-failure':
  assert (root/'drain-checked').exists()
  assert 'containerapp job update:migration' not in calls
 if scenario not in ['success','stop-failure','drain-failure']:assert 'containerapp revision activate' not in calls
 if scenario=='success':assert (root/'actions').read_text()=='prepare\nactivate\ncomplete\n'
 for p in root.glob('pms-first-maintenance-*/maintenance-execution.json'):raise AssertionError('execution payload retained')
 for p in root.glob('pms-first-maintenance-*/drain-check.py'):raise AssertionError('drain helper retained')
print('firstMaintenanceTest='+scenario+':PASS')
PY
done
