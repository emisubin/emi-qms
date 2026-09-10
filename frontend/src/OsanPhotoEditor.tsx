import { useEffect, useRef, useState } from 'react';
import { ApiError, fetchJson } from './api';
import { getOsanProgressPhoto, validateOsanPhotos, type OsanProgressTarget } from './osanProgress';

interface EditRequest { requestId:string;targetId:string;stepId:string;requestedBy:string;requestedByName:string;
 requestedAt:string;approvedAt:string|null;approvedByName:string|null;usedAt:string|null;photoIds:string[];originalPhotoIds?:string[] }
interface EditState {canApprove:boolean;currentUserId:string;items:EditRequest[]}
function ImagePreview({file}:{file:File}){const [url,setUrl]=useState('');useEffect(()=>{const u=URL.createObjectURL(file);setUrl(u);return()=>URL.revokeObjectURL(u);},[file]);return url?<img src={url} alt={file.name}/>:null;}
function HistoryPhoto({projectId,photoId,userKey}:{projectId:string;photoId:string;userKey?:string}){
 const [url,setUrl]=useState('');const [error,setError]=useState('');useEffect(()=>{let objectUrl='';const c=new AbortController();
 getOsanProgressPhoto(projectId,photoId,userKey,c.signal).then(blob=>{if(!c.signal.aborted){objectUrl=URL.createObjectURL(blob);setUrl(objectUrl);}}).catch(()=>{if(!c.signal.aborted)setError('이력 사진을 불러오지 못했습니다.');});
 return()=>{c.abort();if(objectUrl)URL.revokeObjectURL(objectUrl);};},[projectId,photoId,userKey]);
 return error?<p role="alert">{error}</p>:url?<img src={url} alt="이전 수정 사진"/>:null;
}
export function OsanPhotoEditor({projectId,target,stage,userKey,mutationAllowed,onSaved}:{projectId:string;target:OsanProgressTarget;stage:number;userKey?:string;mutationAllowed:boolean;onSaved:()=>void}){
 const path=`/api/osan/projects/${encodeURIComponent(projectId)}/progress/photo-edits`;
 const [state,setState]=useState<EditState>();const [epoch,setEpoch]=useState(0);const [error,setError]=useState('');
 const [busy,setBusy]=useState(false);const [editing,setEditing]=useState(false);const [files,setFiles]=useState<File[]>([]);
 const [history,setHistory]=useState(false); const [submitted,setSubmitted]=useState(false);
 const requestId=useRef(crypto.randomUUID()); const alive=useRef(true);
 const uncertainSave=useRef(false);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;};},[]);
 useEffect(()=>{const c=new AbortController();setState(undefined);
 fetchJson<EditState>(path,userKey,{signal:c.signal}).then(s=>{if(!c.signal.aborted)setState(s);}).catch((e:unknown)=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'승인 정보를 불러오지 못했습니다.');});
 return()=>c.abort();},[path,userKey,epoch]);
 const step=target.steps.find(s=>s.sequenceNumber===stage)!;
 const items=state?.items.filter(r=>r.stepId===step.stepId)??[];const active=items.find(r=>!r.usedAt);
 async function action(kind:'request'|'approve'|'save'){
  if(busy||!mutationAllowed)return;setBusy(true);setError('');
  try{
   if(kind==='save'){
    if(!active)return;const error=validateOsanPhotos(files);if(error){setError(error);return;}
    const body=new FormData();body.set('operationId',active.requestId);body.set('completionMode','individual');
    body.set('stageSequence',String(stage));body.set('targets',JSON.stringify([{targetId:target.targetId,expectedVersion:target.version}]));
    files.forEach(f=>body.append('photos',f,f.name));setSubmitted(true);
    await fetchJson(`${path}/${active.requestId}/save`,userKey,{method:'POST',body});
   }else await fetchJson(kind==='request'?path:`${path}/${active!.requestId}/approve`,userKey,{method:'POST',
    body:JSON.stringify(kind==='request'?{requestId:requestId.current,targetId:target.targetId,stageSequence:stage}:{})});
   if(alive.current){setState(undefined);setEpoch(e=>e+1);setEditing(false);setFiles([]);setSubmitted(false);uncertainSave.current=false;requestId.current=crypto.randomUUID();if(kind==='save')onSaved();}
  }catch(e){if(alive.current){setError(e instanceof Error?e.message:'요청을 완료하지 못했습니다. 다시 시도해 주세요.');
   if(kind==='save'){if(e instanceof ApiError&&[400,403,413,422].includes(e.status)){if(!uncertainSave.current)setSubmitted(false);}else uncertainSave.current=true;}}}
  finally{if(alive.current)setBusy(false);}
 }
 return <section className="osan-photo-editor" aria-label={`${target.displayName} ${step.stepName} 사진 수정`}>
  <p>사진 수정은 관리자 승인 후 1회 가능합니다.</p>
  {state&&!active&&mutationAllowed&&<button type="button" disabled={busy} onClick={()=>void action('request')}>사진 수정 승인 요청</button>}
  {active&&<p>{active.requestedByName} · {active.approvedAt?'수정 승인됨 · 저장 후 다시 잠깁니다.':'관리자 승인 대기'}</p>}
  {active&&!active.approvedAt&&state?.canApprove&&<button type="button" disabled={busy||!mutationAllowed} onClick={()=>void action('approve')}>사진 수정 1회 승인</button>}
  {active?.approvedAt&&active.requestedBy===state?.currentUserId&&mutationAllowed&&!editing&&<button type="button" disabled={busy} onClick={()=>setEditing(true)}>사진 수정</button>}
  {editing&&<div><p>새 사진으로 이 단계의 사진 전체를 교체합니다. 기존 사진과 완료 기록은 보존됩니다.</p>
    <label>사진 선택<input type="file" accept="image/jpeg,image/png" multiple disabled={busy||submitted||!mutationAllowed} onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label>
    <label>카메라 촬영<input type="file" accept="image/jpeg,image/png" capture="environment" disabled={busy||submitted||!mutationAllowed} onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label>
    <div className="osan-progress-photo-region">{files.map((f,i)=><ImagePreview key={i} file={f}/>)}</div>
    {!files.length&&<p>사진을 선택하지 않고 저장하면 현재 사진을 모두 제외합니다. 이전 사진은 이력에 보존됩니다.</p>}
    <button type="button" disabled={busy||!active||!mutationAllowed||!!validateOsanPhotos(files)} onClick={()=>void action('save')}>{busy?'저장 중…':submitted?'같은 사진으로 저장 재시도':'사진 변경 저장'}</button>
    <button type="button" disabled={busy||submitted||!mutationAllowed} onClick={()=>{setEditing(false);setFiles([]);}}>취소</button>
    {validateOsanPhotos(files)&&<p role="alert">{validateOsanPhotos(files)}</p>}
  </div>}
  {editing&&submitted&&state&&!active&&<p>승인이 사용되었거나 상태가 변경되었습니다. <button type="button" onClick={onSaved}>최신 사진 확인</button></p>}
  {error&&<p role="alert">{error} <button type="button" disabled={busy} onClick={()=>setEpoch(e=>e+1)}>승인 상태 새로고침</button></p>}
  {items.some(r=>r.usedAt)&&<><button type="button" onClick={()=>setHistory(!history)} aria-expanded={history}>사진 변경 이력</button>
   {history&&<div><p>최초 완료 사진</p><div className="osan-progress-photo-region">{(items[0]?.originalPhotoIds??[]).map(id=><HistoryPhoto key={id} projectId={projectId} photoId={id} userKey={userKey}/>)}</div></div>}
   {history&&items.filter(r=>r.usedAt).map(r=><div key={r.requestId}><p>{r.requestedByName} · {new Date(r.usedAt!).toLocaleString('ko-KR')} · 승인: {r.approvedByName} · {r.photoIds.length}장</p>
   <div className="osan-progress-photo-region">{r.photoIds.map(id=><HistoryPhoto key={id} projectId={projectId} photoId={id} userKey={userKey}/>)}</div></div>)}</>}
 </section>;
}
