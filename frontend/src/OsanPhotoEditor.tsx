import { OsanRecordPhoto } from './OsanRecordPhoto';
import { createPortal } from 'react-dom';
import { useNativeDialog } from './useNativeDialog';
import { dismissOnBackdrop } from './dialogBackdrop';
import { OsanStageAction } from './OsanStageActions';
import { useEffect, useRef, useState } from 'react';
import { OsanPhotoPreview } from './OsanPhotoPreview';
import { SavedPhoto } from './OsanPhotoGallery';
import { ApiError, fetchJson } from './api';
import { validateOsanRecord, type OsanProgressTarget } from './osanProgress';

export function OsanPhotoEditor({projectId,target,stage,userKey,mutationAllowed,canManageStages=false,onSaved}:{projectId:string;target:OsanProgressTarget;stage:number;userKey?:string;mutationAllowed:boolean;canManageStages?:boolean;onSaved:()=>void}){
 const step=target.steps.find(s=>s.sequenceNumber===stage)!;
 const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [editing,setEditing]=useState(false);
 const [files,setFiles]=useState<File[]>([]);const [comment,setComment]=useState('');const [retained,setRetained]=useState<string[]>([]);
 const [editReason,setEditReason]=useState('');const [submitted,setSubmitted]=useState(false);
 const [adminAction,setAdminAction]=useState<'reject'|'reset'|null>(null);const [reason,setReason]=useState('');
 const operation=useRef(crypto.randomUUID());const adminOperation=useRef(crypto.randomUUID());const pendingBody=useRef<FormData|null>(null);
 const editDialog=useNativeDialog(editing,setEditing);
 const cameraInput=useRef<HTMLInputElement>(null);const albumInput=useRef<HTMLInputElement>(null);
 const alive=useRef(true);const uncertainSave=useRef(false);
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;};},[]);
 function beginEdit(){setComment(step.comment??'');setRetained(step.photos.map(p=>p.photoId));setEditReason('');setFiles([]);setError('');setSubmitted(false);pendingBody.current=null;uncertainSave.current=false;operation.current=crypto.randomUUID();setEditing(true);}
 async function save(){
  if(busy||!mutationAllowed||!step.canEdit||!editReason.trim())return;
  const validation=validateOsanRecord(files,comment,canManageStages,step.photos.filter(p=>retained.includes(p.photoId)));
  if(validation){setError(validation);return;}
  setBusy(true);setError('');
  try{
   if(!pendingBody.current){
    const body=new FormData();body.set('operationId',operation.current);body.set('completionMode','individual');
    body.set('comment',comment);body.set('reason',editReason.trim());body.set('retainedPhotoIds',JSON.stringify(retained));
    body.set('stageSequence',String(stage));body.set('targets',JSON.stringify([{targetId:target.targetId,expectedVersion:target.version}]));
    files.forEach(f=>body.append('photos',f,f.name));pendingBody.current=body;
   }
   setSubmitted(true);
   await fetchJson(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/steps/${encodeURIComponent(step.stepId)}/edit`,userKey,{method:'POST',body:pendingBody.current});
   if(alive.current){setEditing(false);setFiles([]);setSubmitted(false);pendingBody.current=null;uncertainSave.current=false;onSaved();}
  }catch(e){if(alive.current){setError(e instanceof Error?e.message:'저장을 완료하지 못했습니다. 다시 시도해 주세요.');
   if(e instanceof ApiError&&[400,403,413,422].includes(e.status)){if(!uncertainSave.current){setSubmitted(false);pendingBody.current=null;}}
   else uncertainSave.current=true;}}
  finally{if(alive.current)setBusy(false);}
 }
 async function manage(){
  if(busy||!adminAction||!reason.trim()||!mutationAllowed||!canManageStages)return;
  setBusy(true);setError('');
  try{await fetchJson(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/steps/${step.stepId}/${adminAction}`,userKey,{method:'POST',body:JSON.stringify({operationId:adminOperation.current,reason,expectedVersion:target.version})});if(alive.current){setAdminAction(null);onSaved();}}
  catch(e){if(alive.current)setError(e instanceof Error?e.message:'처리를 완료하지 못했습니다.');}
  finally{if(alive.current)setBusy(false);}
 }
 return <section className="osan-photo-editor" aria-label={`${target.displayName} ${step.stepName} 사진 수정`}>
  {canManageStages&&mutationAllowed&&!editing&&<>{!step.openIssue&&step.status==='Completed'&&<OsanStageAction placement="management" type="button" disabled={busy} onClick={()=>{setAdminAction('reject');setReason('');adminOperation.current=crypto.randomUUID();}}>반려</OsanStageAction>}<OsanStageAction placement="management" className="osan-stage-reset" type="button" disabled={busy} onClick={()=>{setAdminAction('reset');setReason('');adminOperation.current=crypto.randomUUID();}}>초기화</OsanStageAction></>}
  {adminAction&&<div className="osan-stage-management"><label>{adminAction==='reject'?'반려 사유':'초기화 사유'}<textarea value={reason} maxLength={1000} disabled={busy||!mutationAllowed} onChange={e=>setReason(e.target.value)}/></label><p>해당 단계만 미완료로 돌아갑니다. 이전 기록은 이력에 보존됩니다.</p><button type="button" disabled={busy||!reason.trim()||!mutationAllowed} onClick={()=>void manage()}>{adminAction==='reject'?'반려 처리':'초기화 처리'}</button><button type="button" disabled={busy} onClick={()=>setAdminAction(null)}>취소</button></div>}
  {!step.openIssue&&step.canEdit&&mutationAllowed&&!editing&&<OsanStageAction placement="primary" tone="neutral" aria-label="사진 수정" type="button" disabled={busy} onClick={beginEdit}>사진·코멘트 수정</OsanStageAction>}
  {editing&&createPortal(<dialog ref={editDialog} className="osan-progress-completion-modal" aria-label="사진·코멘트 수정" onCancel={e=>{if(busy||submitted)e.preventDefault();else setEditing(false);}} onClick={e=>dismissOnBackdrop(e,()=>{if(!busy&&!submitted)setEditing(false);})}><h2>사진·코멘트 수정</h2><p>유지할 사진을 선택하고 새 사진을 추가해 주세요. 이전 사진과 코멘트는 이력에 보존됩니다.</p>
    <div className="osan-progress-previews">{step.photos.map(photo=><OsanRecordPhoto key={photo.photoId} excluded={!retained.includes(photo.photoId)} action={retained.includes(photo.photoId)?'사진 제외':'복원'} disabled={busy||submitted||!mutationAllowed||!step.canEdit} onAction={()=>setRetained(ids=>ids.includes(photo.photoId)?ids.filter(id=>id!==photo.photoId):[...ids,photo.photoId])}><SavedPhoto projectId={projectId} photo={photo} userKey={userKey} preview/></OsanRecordPhoto>)}{files.map((f,i)=><OsanRecordPhoto key={`${f.name}:${f.lastModified}:${i}`} name={f.name} action="사진 제거" disabled={busy||submitted||!mutationAllowed||!step.canEdit} onAction={()=>setFiles(current=>current.filter((_,index)=>index!==i))}><OsanPhotoPreview file={f} projectId={projectId} userKey={userKey} alt={f.name}/></OsanRecordPhoto>)}</div>
    <div className="osan-progress-photo-inputs"><button type="button" disabled={busy||submitted||!mutationAllowed||!step.canEdit} onClick={()=>cameraInput.current?.click()}>촬영</button><button type="button" disabled={busy||submitted||!mutationAllowed||!step.canEdit} onClick={()=>albumInput.current?.click()}>업로드</button></div>
    <input ref={albumInput} hidden aria-label="사진 선택" type="file" accept="image/jpeg,image/png,image/heic,image/heif,.heic,.heif" multiple disabled={busy||submitted||!mutationAllowed||!step.canEdit} onChange={e=>setFiles(Array.from(e.target.files??[]))}/>
    <input ref={cameraInput} hidden aria-label="카메라 촬영" type="file" accept="image/*" capture="environment" disabled={busy||submitted||!mutationAllowed||!step.canEdit} onChange={e=>setFiles(Array.from(e.target.files??[]))}/>
    <label className="osan-comment-input">코멘트<textarea maxLength={1000} value={comment} disabled={busy||submitted||!mutationAllowed||!step.canEdit} onChange={e=>setComment(e.target.value)}/><span>{comment.length} / 1000자</span></label>
    <label className="osan-comment-input">수정 사유 (필수)<textarea maxLength={1000} value={editReason} disabled={busy||submitted||!mutationAllowed||!step.canEdit} placeholder="사진·코멘트를 수정하는 사유를 입력해 주세요." onChange={e=>setEditReason(e.target.value)}/><span>{editReason.length} / 1000자</span></label>
    <p className="osan-progress-photo-instruction">{canManageStages?'관리자는 사진 없이 코멘트만으로 저장할 수 있습니다.':'수정 후에도 사진을 1장 이상 유지하거나 새로 첨부해야 합니다.'}</p><p className="osan-progress-photo-limits">JPEG·PNG·HEIC 최대 5장, 유지할 사진과 새 사진 전체 40MiB. 원본을 저장합니다.</p><button className="osan-progress-submit" type="button" disabled={busy||!step.canEdit||!editReason.trim()||!mutationAllowed||!!validateOsanRecord(files,comment,canManageStages,step.photos.filter(p=>retained.includes(p.photoId)))} onClick={()=>void save()}>{busy?'저장 중…':submitted?'같은 사진으로 저장 재시도':'수정 저장'}</button>
    <button className="osan-progress-close" type="button" disabled={busy||submitted||!mutationAllowed||!step.canEdit} onClick={()=>{setEditing(false);setFiles([]);}}>취소</button>
    {validateOsanRecord(files,comment,canManageStages,step.photos.filter(p=>retained.includes(p.photoId)))&&<p role="alert">{validateOsanRecord(files,comment,canManageStages,step.photos.filter(p=>retained.includes(p.photoId)))}</p>}
  {error&&<p role="alert">{error} {submitted&&<button type="button" disabled={busy} onClick={onSaved}>최신 기록 확인</button>}</p>}
  </dialog>,document.querySelector('.osan-progress-page')??document.querySelector('.app-shell')??document.body)}
  {error&&!editing&&<p role="alert">{error}</p>}
 </section>;
}
