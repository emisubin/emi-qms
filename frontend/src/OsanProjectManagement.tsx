import { OsanButton } from './OsanButton';
import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { fetchJson } from './api';
import { OsanCustomerMatch } from './OsanCustomerMatch';
import type { OsanProjectDetail } from './projects';

const fields = [ ['title','장비명'], ['projectCode','프로젝트 코드'], ['productName','part 분류'],
  ['customerName','고객사'], ['poNumber','PO No'], ['workOrderNumber','W/O No'], ['deliveryDate','납기일'] ] as const;

export function OsanProjectManagement({ project, userKey, onSaved, onDeleted, mutationAllowed, actionsContainer, mobileDeleteContainer }: {
  actionsContainer?: HTMLElement | null;
  mobileDeleteContainer?: HTMLElement | null;
  project: OsanProjectDetail; userKey?: string; mutationAllowed: boolean; onSaved: () => void; onDeleted: () => void;
}) {
  const [access, setAccess] = useState<{ canManage: boolean; editToken: string }>();
  const [mode, setMode] = useState<'edit'|'delete'|null>(null);
  const [draft, setDraft] = useState<Record<string,string>>({});
  const [customerId,setCustomerId]=useState<string>();
  const [reason, setReason] = useState('');
  const [deliveryHold, setDeliveryHold] = useState(false);
  const [busy,setBusy]=useState(false); const [error,setError]=useState('');
  const path=`/api/osan/projects/${encodeURIComponent(project.projectId)}`;
  useEffect(()=>{const c=new AbortController();setAccess(undefined);
    fetchJson<{canManage:boolean;editToken:string}>(`${path}/management`,userKey,{signal:c.signal}).then(value=>{if(!c.signal.aborted)setAccess(value);})
      .catch((e:unknown)=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'관리 권한을 확인할 수 없습니다.');});
    return()=>c.abort();},[path,userKey,project]);
  const open=(next:'edit'|'delete')=>{setCustomerId(project.customerId);setDraft(Object.fromEntries(fields.map(([key])=>[key,String(project[key]??'')])));setMode(next);setDeliveryHold(project.deliveryHold ?? false);setError('');setReason('');};
  async function save(){
    if(!access||busy||!mutationAllowed)return;
    if(!project.editToken){setError('프로젝트를 새로고침한 후 다시 수정해 주세요.');return;}
    setBusy(true);setError('');
    try{
      await fetchJson(path,userKey,{method:mode==='delete'?'DELETE':'PUT',body:JSON.stringify(mode==='delete'
        ?{expectedToken:project.editToken,reason}
        :{expectedToken:project.editToken,deliveryHold,holdReason:reason,fields:{...draft,customerId,operationId:crypto.randomUUID()}})});
      setMode(null);if(mode==='delete')onDeleted();else onSaved();
    }catch(e){setError(e instanceof Error?e.message:'저장하지 못했습니다. 다시 시도해 주세요.');}finally{setBusy(false);}
  }
  if(!access?.canManage||!mutationAllowed)return error?<p role="alert">{error}</p>:null;
  const buttons = <><OsanButton type="button" tone="soft" className="osan-detail-back osan-detail-edit" aria-label="프로젝트 정보 수정" onClick={()=>open('edit')}>{actionsContainer ? '수정' : '프로젝트 정보 수정'}</OsanButton><OsanButton type="button" className={`osan-detail-back ${mobileDeleteContainer ? 'osan-detail-desktop-delete' : ''}`} aria-label="프로젝트 삭제" onClick={()=>open('delete')}>{actionsContainer ? '삭제' : '프로젝트 삭제'}</OsanButton></>;
  return <div className={mode ? 'osan-management' : 'osan-management-idle'}>
    {!mode && mobileDeleteContainer && createPortal(<button type="button" onClick={()=>{mobileDeleteContainer.closest('details')?.removeAttribute('open');open('delete');}}>프로젝트 삭제</button>,mobileDeleteContainer)}
    {!mode?(actionsContainer ? createPortal(buttons, actionsContainer) : <div className="actions">{buttons}</div>)
      :<form onSubmit={e=>{e.preventDefault();void save();}} aria-label={mode==='edit'?'프로젝트 정보 수정':'프로젝트 삭제'}>
        <h3>{mode==='edit'?'프로젝트 정보 수정':'프로젝트 삭제'}</h3>
        {mode==='edit'?<><div className="osan-management-fields">{fields.map(([key,label])=><label key={key}>{label}
          <>{key==='customerName'?<OsanCustomerMatch retainedCustomer={project.customerId ? {customerId:project.customerId,name:project.customerName} : undefined} value={draft.customerName??''} customerId={customerId} disabled={busy} userKey={userKey} onChange={(name,id)=>{setDraft({...draft,customerName:name});setCustomerId(id);}}/>:<input aria-label={`${label} 수정`} type={key==='deliveryDate'?'date':'text'}
            required={!['poNumber','workOrderNumber'].includes(key)} disabled={busy}
            maxLength={key==='title'?200:key==='projectCode'?80:100}
            value={draft[key]??''} onChange={e=>setDraft({...draft,[key]:e.target.value})}/>}</></label>)}</div>
          <label className="osan-management-hold"><input type="checkbox" checked={deliveryHold} disabled={busy} onChange={e=>setDeliveryHold(e.target.checked)}/><span>납기 HOLD</span></label>
          <p className="osan-management-hold-help">HOLD 중에는 홈과 진행 현황의 맨 아래에 표시되며 진행 작업은 계속 등록할 수 있습니다. 기존 납기일은 유지됩니다.</p>
          {deliveryHold !== (project.deliveryHold ?? false) && <label>납기 HOLD 변경 사유<textarea required maxLength={500} disabled={busy} value={reason} onChange={e=>setReason(e.target.value)}/></label>}
          </>
        :<><p>홈·프로젝트·진행 현황에서 숨깁니다. 진행 이력과 사진은 보존됩니다.</p><label>삭제 사유<textarea aria-label="삭제 사유" required maxLength={500} disabled={busy} value={reason} onChange={e=>setReason(e.target.value)}/></label></>}
        {error&&<p role="alert">{error}</p>}
        <div className="actions"><button type="submit" disabled={busy}>{busy?'저장 중…':mode==='delete'?'삭제 확인':'변경 저장'}</button><button type="button" disabled={busy} onClick={()=>setMode(null)}>취소</button></div>
      </form>}
  </div>;
}
