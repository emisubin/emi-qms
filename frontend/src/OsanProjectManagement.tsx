import { useEffect, useState } from 'react';
import { fetchJson } from './api';
import type { OsanProjectDetail } from './projects';

const fields = [ ['title','장비명'], ['projectCode','프로젝트 코드'], ['productName','part분류'],
  ['quantity','수량'], ['customerName','거래처'], ['poNumber','PO No'], ['workOrderNumber','W/O No'], ['deliveryDate','납기일'] ] as const;

export function OsanProjectManagement({ project, userKey, onSaved, onDeleted, mutationAllowed }: {
  project: OsanProjectDetail; userKey?: string; mutationAllowed: boolean; onSaved: () => void; onDeleted: () => void;
}) {
  const [access, setAccess] = useState<{ canManage: boolean; editToken: string }>();
  const [mode, setMode] = useState<'edit'|'delete'|null>(null);
  const [draft, setDraft] = useState<Record<string,string>>({});
  const [reason, setReason] = useState('');
  const [busy,setBusy]=useState(false); const [error,setError]=useState('');
  const path=`/api/osan/projects/${encodeURIComponent(project.projectId)}`;
  useEffect(()=>{const c=new AbortController();setAccess(undefined);
    fetchJson<{canManage:boolean;editToken:string}>(`${path}/management`,userKey,{signal:c.signal}).then(value=>{if(!c.signal.aborted)setAccess(value);})
      .catch((e:unknown)=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'관리 권한을 확인할 수 없습니다.');});
    return()=>c.abort();},[path,userKey,project]);
  const started=project.targets.some(t=>t.status!=='NotStarted'||t.steps.some(s=>s.status!=='NotStarted'));
  const open=(next:'edit'|'delete')=>{setDraft(Object.fromEntries(fields.map(([key])=>[key,String(project[key]??'')])));setMode(next);setError('');setReason('');};
  async function save(){
    if(!access||busy||!mutationAllowed)return;
    if(!project.editToken){setError('프로젝트를 새로고침한 후 다시 수정해 주세요.');return;}
    setBusy(true);setError('');
    try{
      await fetchJson(path,userKey,{method:mode==='delete'?'DELETE':'PUT',body:JSON.stringify(mode==='delete'
        ?{expectedToken:project.editToken,reason}
        :{expectedToken:project.editToken,fields:{...draft,quantity:Number(draft.quantity),operationId:crypto.randomUUID()}})});
      setMode(null);if(mode==='delete')onDeleted();else onSaved();
    }catch(e){setError(e instanceof Error?e.message:'저장하지 못했습니다. 다시 시도해 주세요.');}finally{setBusy(false);}
  }
  if(!access?.canManage||!mutationAllowed)return error?<p role="alert">{error}</p>:null;
  return <div className="osan-management">
    {!mode?<div className="actions"><button type="button" onClick={()=>open('edit')}>프로젝트 정보 수정</button><button type="button" onClick={()=>open('delete')}>프로젝트 삭제</button></div>
      :<form onSubmit={e=>{e.preventDefault();void save();}} aria-label={mode==='edit'?'프로젝트 정보 수정':'프로젝트 삭제'}>
        <h3>{mode==='edit'?'프로젝트 정보 수정':'프로젝트 삭제'}</h3>
        {mode==='edit'?<><div className="osan-management-fields">{fields.map(([key,label])=><label key={key}>{label}
          <input aria-label={`${label} 수정`} type={key==='quantity'?'number':key==='deliveryDate'?'date':'text'}
            required={!['poNumber','workOrderNumber'].includes(key)} disabled={busy||(key==='quantity'&&started)}
            min={key==='quantity'?1:undefined} max={key==='quantity'?500:undefined}
            maxLength={key==='title'||key==='customerName'?200:key==='projectCode'?80:100}
            value={draft[key]??''} onChange={e=>setDraft({...draft,[key]:e.target.value})}/></label>)}</div>
          {started&&<p>진행이 시작된 프로젝트의 수량은 변경할 수 없습니다.</p>}</>
        :<><p>홈·프로젝트·진행 현황에서 숨깁니다. 진행 이력과 사진은 보존됩니다.</p><label>삭제 사유<textarea aria-label="삭제 사유" required maxLength={500} disabled={busy} value={reason} onChange={e=>setReason(e.target.value)}/></label></>}
        {error&&<p role="alert">{error}</p>}
        <div className="actions"><button type="submit" disabled={busy}>{busy?'저장 중…':mode==='delete'?'삭제 확인':'변경 저장'}</button><button type="button" disabled={busy} onClick={()=>setMode(null)}>취소</button></div>
      </form>}
  </div>;
}
