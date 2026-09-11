import { dismissOnBackdrop } from './dialogBackdrop';
import { useEffect, useRef, useState } from 'react';
import { fetchJson } from './api';
import { OsanPhotoGallery } from './OsanPhotoGallery';
import type { OsanProgressPhoto } from './osanProgress';
interface HistoryItem { id:string;eventType:string;actorDisplayName:string;occurredAtUtc:string;comment:string|null;reason:string|null;photos:OsanProgressPhoto[] }
const labels:Record<string,string>={Completed:'완료',Edited:'수정 완료',Rejected:'반려',Approved:'수정 승인',Reset:'초기화',Edit:'수정 완료',Complete:'완료',Request:'수정 요청',Completion:'완료',PhotoEdit:'수정 완료',Reject:'반려',Approve:'수정 승인'};
export function OsanStageHistory({projectId,stepId,title,userKey}:{projectId:string;stepId:string;title:string;userKey?:string}) {
 const [open,setOpen]=useState(false);const [items,setItems]=useState<HistoryItem[]>();const [error,setError]=useState('');const [retry,setRetry]=useState(0);const dialog=useRef<HTMLDialogElement>(null);
 useEffect(()=>{if(open)dialog.current?.showModal();else dialog.current?.close();},[open]);
 useEffect(()=>{if(!open)return;const c=new AbortController();setItems(undefined);setError('');
 fetchJson<HistoryItem[]>(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/steps/${encodeURIComponent(stepId)}/history`,userKey,{signal:c.signal}).then(r=>{if(!c.signal.aborted)setItems(r);}).catch(e=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'이력을 불러오지 못했습니다.');});return()=>c.abort();},[projectId,stepId,userKey,open,retry]);
 return <><button type="button" onClick={()=>setOpen(true)}>이력 보기</button><dialog ref={dialog} className="osan-record-history" aria-label="단계 저장 이력" onCancel={()=>setOpen(false)} onClick={e=>dismissOnBackdrop(e,()=>setOpen(false))}>
 <header><div><h2>{title} 이력</h2></div><button type="button" onClick={()=>setOpen(false)}>닫기</button></header>
 <p className="history-intro">최신 기록부터 표시합니다. 각 기록을 펼치면 당시 저장 내용을 확인할 수 있습니다.</p>
 {error?<p role="alert">{error} <button type="button" onClick={()=>setRetry(n=>n+1)}>다시 불러오기</button></p>:!items?<p role="status">이력 불러오는 중…</p>:!items.length?<p>등록된 이력이 없습니다.</p>:<div className="history-timeline">{items.map((item,i)=><details key={item.id} className="history-entry" open={i===0?true:undefined}>
 <summary><span className="history-event">{labels[item.eventType]??item.eventType}</span><span className="history-meta">{item.actorDisplayName} · {new Date(item.occurredAtUtc).toLocaleString('ko-KR',{timeZone:'Asia/Seoul'})}</span><span className="history-expand" aria-hidden="true"/></summary>
 <div className="history-entry-body">{!!item.photos.length&&<><p className="history-label">당시 사진 · {item.photos.length}장</p><OsanPhotoGallery projectId={projectId} photos={item.photos} userKey={userKey} history/></>}
 {item.reason?<><p className="history-label">사유 / 처리 내용</p><p className="history-comment">{item.reason}</p></>:<><p className="history-label">당시 코멘트</p><p className="history-comment">{item.comment||'등록된 코멘트가 없습니다.'}</p></>}
 </div></details>)}</div>}</dialog></>;
}
