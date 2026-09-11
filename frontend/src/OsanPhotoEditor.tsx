import { useEffect, useRef, useState } from 'react';
import { SavedPhoto } from './OsanPhotoGallery';
import { ApiError, fetchJson } from './api';
import { validateOsanPhotos, validateOsanRecord, type OsanProgressTarget } from './osanProgress';

interface EditRequest { requestId:string;targetId:string;stepId:string;requestedBy:string;requestedByName:string;
 requestedAt:string;approvedAt:string|null;approvedByName:string|null;usedAt:string|null;photoIds:string[];originalPhotoIds?:string[];invalidatedAt?:string|null }
interface EditState {canApprove:boolean;currentUserId:string;items:EditRequest[]}
function ImagePreview({file}:{file:File}){const [url,setUrl]=useState('');useEffect(()=>{const u=URL.createObjectURL(file);setUrl(u);return()=>URL.revokeObjectURL(u);},[file]);return url?<img src={url} alt={file.name}/>:null;}
export function OsanPhotoEditor({projectId,target,stage,userKey,mutationAllowed,onSaved}:{projectId:string;target:OsanProgressTarget;stage:number;userKey?:string;mutationAllowed:boolean;onSaved:()=>void}){
 const path=`/api/osan/projects/${encodeURIComponent(projectId)}/progress/photo-edits`;
 const [state,setState]=useState<EditState>();const [epoch,setEpoch]=useState(0);const [error,setError]=useState('');
 const [busy,setBusy]=useState(false);const [editing,setEditing]=useState(false);const [files,setFiles]=useState<File[]>([]);
 const [comment,setComment]=useState(''); const [retained,setRetained]=useState<string[]>([]); const [adminAction,setAdminAction]=useState<'reject'|'reset'|null>(null);const [reason,setReason]=useState(''); const adminOperation=useRef(crypto.randomUUID()); const [submitted,setSubmitted]=useState(false);
 const requestId=useRef(crypto.randomUUID()); const alive=useRef(true);
 const uncertainSave=useRef(false);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;};},[]);
 useEffect(()=>{const c=new AbortController();setState(undefined);
 fetchJson<EditState>(path,userKey,{signal:c.signal}).then(s=>{if(!c.signal.aborted)setState(s);}).catch((e:unknown)=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'승인 정보를 불러오지 못했습니다.');});
 return()=>c.abort();},[path,userKey,epoch]);
 const step=target.steps.find(s=>s.sequenceNumber===stage)!;
 const items=state?.items.filter(r=>r.stepId===step.stepId)??[];const active=items.find(r=>!r.usedAt&&!r.invalidatedAt);
 async function action(kind:'request'|'approve'|'save'){
  if(busy||!mutationAllowed)return;setBusy(true);setError('');
  try{
   if(kind==='save'){
    if(!active)return;const error=validateOsanRecord(files,comment,!!state?.canApprove,step.photos.filter(p=>retained.includes(p.photoId)));if(error){setError(error);return;}
    const body=new FormData();body.set('operationId',active.requestId);body.set('completionMode','individual');
    body.set('comment',comment);body.set('retainedPhotoIds',JSON.stringify(retained));
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
 async function manage() {
  if(busy||!adminAction||!reason.trim()||!mutationAllowed)return;
  setBusy(true);setError('');
  try { await fetchJson(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/steps/${step.stepId}/${adminAction}`,userKey,{method:'POST',body:JSON.stringify({operationId:adminOperation.current,reason,expectedVersion:target.version})}); if(alive.current){setAdminAction(null);onSaved();} }
  catch(e){if(alive.current)setError(e instanceof Error?e.message:'처리를 완료하지 못했습니다.');}
  finally{if(alive.current)setBusy(false);}
 }
 return <section className="osan-photo-editor" aria-label={`${target.displayName} ${step.stepName} 사진 수정`}>
  {state?.canApprove&&mutationAllowed&&!editing&&<>{!active?.approvedAt&&step.status==='Completed'&&<button type="button" disabled={busy} onClick={()=>{setAdminAction('reject');setReason('');adminOperation.current=crypto.randomUUID();}}>반려</button>}<button type="button" disabled={busy} onClick={()=>{setAdminAction('reset');setReason('');adminOperation.current=crypto.randomUUID();}}>초기화</button></>}
  {adminAction&&<div className="osan-stage-management"><label>{adminAction==='reject'?'반려 사유':'초기화 사유'}<textarea value={reason} maxLength={1000} disabled={busy} onChange={e=>setReason(e.target.value)}/></label><p>해당 단계만 미완료로 돌아갑니다. 이전 기록은 이력에 보존됩니다.</p><button type="button" disabled={busy||!reason.trim()} onClick={()=>void manage()}>{adminAction==='reject'?'반려 처리':'초기화 처리'}</button><button type="button" disabled={busy} onClick={()=>setAdminAction(null)}>취소</button></div>}
  <p>사진·코멘트 수정은 승인 또는 반려 후 1회 가능합니다.</p>
  {state&&!active&&mutationAllowed&&step.status==='Completed'&&<button type="button" disabled={busy} onClick={()=>void action('request')}>사진 수정 승인 요청</button>}
  {active&&<p>{active.requestedByName} · {active.approvedAt?'수정 승인됨 · 저장 후 다시 잠깁니다.':'관리자 승인 대기'}</p>}
  {active&&!active.approvedAt&&state?.canApprove&&<button type="button" disabled={busy||!mutationAllowed} onClick={()=>void action('approve')}>사진 수정 1회 승인</button>}
  {active?.approvedAt&&mutationAllowed&&!editing&&<button type="button" disabled={busy} onClick={()=>{setComment(step.comment??'');setRetained(step.photos.map(p=>p.photoId));setEditing(true);}}>사진 수정</button>}
  {editing&&<div><p>유지할 사진을 선택하고 새 사진을 추가해 주세요. 이전 사진과 코멘트는 이력에 보존됩니다.</p>
    <div className="osan-retained-photos">{step.photos.map(photo=><label key={photo.photoId}><input type="checkbox" checked={retained.includes(photo.photoId)} disabled={busy||submitted} onChange={e=>setRetained(ids=>e.target.checked?[...ids,photo.photoId]:ids.filter(id=>id!==photo.photoId))}/><span>사진 유지</span><SavedPhoto projectId={projectId} photo={photo} userKey={userKey}/></label>)}</div>
    <label>사진 선택<input type="file" accept="image/jpeg,image/png" multiple disabled={busy||submitted||!mutationAllowed} onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label>
    <label>카메라 촬영<input type="file" accept="image/jpeg,image/png" capture="environment" disabled={busy||submitted||!mutationAllowed} onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label>
    <div className="osan-progress-photo-region">{files.map((f,i)=><ImagePreview key={i} file={f}/>)}</div>
    <label className="osan-comment-input">코멘트<textarea maxLength={1000} value={comment} disabled={busy||submitted} onChange={e=>setComment(e.target.value)}/><span>{comment.length} / 1000자</span></label>
    <button type="button" disabled={busy||!active||!mutationAllowed||!!validateOsanRecord(files,comment,!!state?.canApprove,step.photos.filter(p=>retained.includes(p.photoId)))} onClick={()=>void action('save')}>{busy?'저장 중…':submitted?'같은 사진으로 저장 재시도':'사진 변경 저장'}</button>
    <button type="button" disabled={busy||submitted||!mutationAllowed} onClick={()=>{setEditing(false);setFiles([]);}}>취소</button>
    {validateOsanPhotos(files)&&<p role="alert">{validateOsanPhotos(files)}</p>}
  </div>}
  {editing&&submitted&&state&&!active&&<p>승인이 사용되었거나 상태가 변경되었습니다. <button type="button" onClick={onSaved}>최신 사진 확인</button></p>}
  {error&&<p role="alert">{error} <button type="button" disabled={busy} onClick={()=>setEpoch(e=>e+1)}>승인 상태 새로고침</button></p>}

 </section>;
}
