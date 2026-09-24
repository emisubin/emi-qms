import { useEffect, useRef, useState } from 'react';
import { createNotice, deleteNotice, deleteNoticeAttachment, downloadNoticeAttachment, fetchJson, getNotice, listNotices, updateNotice, uploadNoticeAttachment } from './api';
import type { NoticeDetail, NoticeListResponse } from './notices';
import { NoticeBody } from './NoticeBoardPage';
import './osan-notices.css';

type Settings = { version:number; pinned:boolean; popupEnabled:boolean; popupVersion:number };
const message = (e:unknown) => e instanceof Error ? e.message : '처리하지 못했습니다. 다시 시도해 주세요.';
export function OsanNoticeBoard({userKey, noticeId, compose, admin, enabled, onList, onOpen, onCompose}:{userKey?:string;noticeId?:string;compose?:boolean;admin:boolean;enabled:boolean;onList:()=>void;onOpen:(id:string)=>void;onCompose:()=>void}) {
 const [list,setList]=useState<NoticeListResponse>();const [detail,setDetail]=useState<NoticeDetail>();
 const [page,setPage]=useState(1);const [query,setQuery]=useState('');const [search,setSearch]=useState('');const [error,setError]=useState('');const [busy,setBusy]=useState(false);
 const [edit,setEdit]=useState(false);const [title,setTitle]=useState('');const [body,setBody]=useState('');const [files,setFiles]=useState<File[]>([]);
 const [settings,setSettings]=useState<Settings>();const [reload,setReload]=useState(0);const requestId=useRef(crypto.randomUUID());
 useEffect(()=>{let active=true;setError('');setEdit(false);setDetail(undefined);setFiles([]);setSettings(undefined);
  if(compose){setTitle('');setBody('');requestId.current=crypto.randomUUID();return()=>{active=false;};}
  if(noticeId){getNotice(userKey,noticeId).then(async value=>{if(!active)return;setDetail(value);setTitle(value.title);setBody(value.body);
   // Reading remains usable while maintenance temporarily blocks receipt writes.
   await fetchJson(`/api/osan/notices/${noticeId}/read`,userKey,{method:'POST'}).catch(()=>{});
  }).catch(e=>{if(active)setError(message(e));});
   if(admin)fetchJson<Settings>(`/api/osan/notices/${noticeId}/settings`,userKey).then(v=>{if(active)setSettings(v);}).catch(e=>{if(active)setError(message(e));});
  }else{setList(undefined);listNotices(userKey,page,20,search).then(v=>{if(active)setList(v);}).catch(e=>{if(active)setError(message(e));});}
  return()=>{active=false;};
 },[userKey,noticeId,compose,admin,page,reload,search]);
 const run=async(action:()=>Promise<void>)=>{setBusy(true);setError('');try{await action();}catch(e){setError(message(e));}finally{setBusy(false);}};
 const save=()=>run(async()=>{
  if(files.length+(detail?.attachments.length??0)>10)throw new Error('첨부파일은 최대 10개입니다.');
  if(files.some(f=>f.size>20*1024*1024)||files.reduce((a,f)=>a+f.size,0)+(detail?.attachments.reduce((a,f)=>a+f.byteSize,0)??0)>100*1024*1024)throw new Error('첨부파일은 개별 20MB, 합계 100MB 이하여야 합니다.');
  const saved=detail?await updateNotice(userKey,detail.noticeId,{expectedVersion:detail.version,title,body,bodyFormat:detail.bodyFormat}):await createNotice(userKey,{requestId:requestId.current,title,body,bodyFormat:'PlainTextV1'});
  // Keep the saved post visible if an attachment fails; retry only remaining files.
  setDetail(saved);setEdit(true);
  for(const file of files){await uploadNoticeAttachment(userKey,saved.noticeId,file);setFiles(previous=>previous.filter(item=>item!==file));}
  onOpen(saved.noticeId);setReload(n=>n+1);
 });
 const changeSettings=(values:Partial<Settings>,reannounce=false)=>run(async()=>{if(!detail||!settings)return;
  const value=await fetchJson<Settings>(`/api/osan/notices/${detail.noticeId}/settings`,userKey,{method:'PUT',body:JSON.stringify({...settings,...values,expectedVersion:settings.version,reannounce})});setSettings(value);setDetail(await getNotice(userKey,detail.noticeId));
 });
 const items=list?.items??[];
 return <section className="osan-board"><header className="board-heading"><h1>공지사항</h1><p>업데이트와 업무에 필요한 소식을 확인하세요.</p></header>
 {error&&<p role="alert">{error} <button onClick={()=>setReload(n=>n+1)}>다시 불러오기</button></p>}
 {compose||edit?<form onSubmit={e=>{e.preventDefault();void save();}}><div className="board-toolbar"><h2>{detail?'공지 수정':'공지 작성'}</h2><div><button type="button" disabled={busy} onClick={onList}>취소</button><button className="primary" disabled={busy||!enabled}>저장</button></div></div>
 <label className="editor-line">제목<input maxLength={100} required value={title} disabled={busy} onChange={e=>setTitle(e.target.value)}/></label>
 <label className="editor-line">첨부파일<input type="file" multiple accept=".jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.hwp,.zip" disabled={busy} onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label><p className="muted">최대 10개 · 개별 20MB · 합계 100MB</p>
 <p className="muted">{files.map(file=>file.name).join(', ')}</p><label className="board-body-label">내용<textarea maxLength={2000} required value={body} disabled={busy} onChange={e=>setBody(e.target.value)}/></label>
 </form>:noticeId?<>{!detail?<p>공지를 불러오는 중…</p>:<><div className="board-toolbar"><button onClick={onList}>목록</button><div>{detail.canEdit&&<button disabled={!enabled||busy} onClick={()=>setEdit(true)}>수정</button>}{detail.canDelete&&<button disabled={!enabled||busy} onClick={()=>{if(window.confirm('공지를 삭제할까요? 내용과 첨부 이력은 보존됩니다.'))void run(async()=>{await deleteNotice(userKey,detail.noticeId);onList();});}}>삭제</button>}</div></div>
 <div className="article-heading"><h2>{detail.title}</h2><p>{detail.authorDisplayName} · {new Date(detail.createdAtUtc).toLocaleString('ko-KR')}</p></div>
 <div className="attachments">{detail.attachments.map(file=><div key={file.attachmentId}><button onClick={()=>void run(async()=>{const data=await downloadNoticeAttachment(userKey,detail.noticeId,file.attachmentId,file.fileName);const url=URL.createObjectURL(data.blob);const a=document.createElement('a');a.href=url;a.download=data.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);})}>{file.fileName}</button>{file.canDelete&&<button aria-label={`${file.fileName} 삭제`} disabled={!enabled||busy} onClick={()=>void run(async()=>{await deleteNoticeAttachment(userKey,detail.noticeId,file.attachmentId);setReload(n=>n+1);})}>삭제</button>}</div>)}</div>
 <div className="article-body"><NoticeBody body={detail.body} bodyFormat={detail.bodyFormat}/></div>
 {admin&&settings&&<details className="settings"><summary>관리자 설정</summary><div className="settings-inner"><label><input type="checkbox" checked={settings.pinned} disabled={!enabled||busy} onChange={e=>void changeSettings({pinned:e.target.checked})}/>상단 고정</label><label><input type="checkbox" checked={settings.popupEnabled} disabled={!enabled||busy} onChange={e=>void changeSettings({popupEnabled:e.target.checked})}/>팝업 표시</label><button disabled={!enabled||busy||!settings.popupEnabled} onClick={()=>{if(window.confirm('이미 확인한 사용자에게도 한 번 더 표시할까요?'))void changeSettings({},true);}}>다시 공지</button><p>팝업은 계정마다 한 번 표시됩니다. 일반 수정으로 다시 표시되지 않습니다.</p></div></details>}
 </>}</>:<><div className="board-toolbar"><span>전체 {list?.totalCount??0}건</span><button className="primary" disabled={!enabled} onClick={onCompose}>글쓰기</button></div>
 <table className="board-table"><thead><tr><th className="number">번호</th><th>제목</th><th className="author">작성자</th><th className="date">등록일</th><th className="file">첨부</th></tr></thead><tbody>{items.map((item,i)=><tr key={item.noticeId} className={item.pinned?'pinned':''}><td className="number">{item.pinned?<span className="pin">공지</span>:(list?.totalCount??0)-(page-1)*20-i}</td><td className="title"><button className={item.isRead ? 'notice-title-read' : 'notice-title-unread'} onClick={()=>onOpen(item.noticeId)}>{item.title}</button></td><td>{item.authorDisplayName}</td><td>{new Date(item.createdAtUtc).toLocaleDateString('ko-KR')}</td><td className="file">{item.attachmentCount||'—'}</td></tr>)}</tbody></table>
 {!list?<p>목록을 불러오는 중…</p>:items.length===0?<p>표시할 공지가 없습니다.</p>:null}
 <form className="board-search" onSubmit={e=>{e.preventDefault();setPage(1);setSearch(query.trim());}}><input aria-label="공지 검색" placeholder="제목·작성자 검색" value={query} onChange={e=>setQuery(e.target.value)}/><button>검색</button></form><div className="board-pages"><button disabled={page===1} onClick={()=>setPage(p=>p-1)}>이전</button><span>{page} / {Math.max(1,Math.ceil((list?.totalCount??0)/20))}</span><button disabled={!list||page*20>=list.totalCount} onClick={()=>setPage(p=>p+1)}>다음</button></div>
 </>}
 </section>;
}

type NoticePopupProps={userKey?:string;scope:string;onOpen:(id:string)=>void};
export function OsanNoticePopups(props:NoticePopupProps) {
 return <ScopedOsanNoticePopups key={`${props.userKey ?? ''}:${props.scope}`} {...props}/>;
}
function ScopedOsanNoticePopups({userKey,scope,onOpen}:NoticePopupProps) {
 const [popup,setPopup]=useState<{noticeId:string;title:string;body:string;popupVersion:number}>();
 const dialogRef=useRef<HTMLElement>(null);
 useEffect(()=>{if(!popup)return;const previous=document.activeElement as HTMLElement|null;dialogRef.current?.querySelector<HTMLButtonElement>('button')?.focus();return()=>previous?.focus();},[popup]);
 useEffect(()=>{let active=true;const load=async()=>{
  const data=await fetchJson<{items:NonNullable<typeof popup>[]}>(`/api/osan/notices/popups`,userKey);
  for(const item of data.items){if(!active)return;const claim=await fetchJson<{claimed:boolean}>(`/api/osan/notices/${item.noticeId}/popups/${item.popupVersion}/claim`,userKey,{method:'POST'});if(claim.claimed&&active){setPopup(item);break;}}
 };void load().catch(()=>{});return()=>{active=false;};},[userKey,scope]);
 if(!popup)return null;
 return <div className="osan-notice-backdrop"><section ref={dialogRef} onKeyDown={event=>{if(event.key==='Escape'){event.preventDefault();setPopup(undefined);}if(event.key==='Tab'){const buttons=dialogRef.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)');if(!buttons?.length)return;const first=buttons[0],last=buttons[buttons.length-1];if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}}} className="osan-notice-popup" role="dialog" aria-modal="true" aria-label="공지사항"><header><strong>공지사항</strong><button aria-label="닫기" onClick={()=>setPopup(undefined)}>×</button></header><h2>{popup.title}</h2><div>{popup.body}</div><footer><small>계정마다 한 번 표시되는 공지입니다.</small><button onClick={()=>{onOpen(popup.noticeId);setPopup(undefined);}}>자세히 보기</button><button className="primary" onClick={()=>setPopup(undefined)}>확인</button></footer></section></div>;
}
