import { useEffect, useRef, useState } from 'react';
import { fetchJson } from './api';
import './maintenance-announcement.css';
import type { MaintenanceStatus } from './useMaintenanceStatus';
export function MaintenanceAnnouncement({status,userKey,scope,unavailable,onOpenNotice}:{status?:MaintenanceStatus;userKey?:string;scope:string;unavailable:boolean;onOpenNotice:(id:string)=>void}) {
 const [open,setOpen]=useState(false);const [claimed,setClaimed]=useState('');
 const claimRequest=useRef<{identity:string;promise:Promise<{claimed:boolean}>}|null>(null);
 const identity=status?.releaseId?`${userKey ?? ''}:${scope}:${status.releaseId}:${status.popupVersion}`:'';
 useEffect(()=>{if(!status?.releaseId||status.state==='Completed'||!status.popupPending||identity===claimed)return;let active=true;
  if(claimRequest.current?.identity!==identity)claimRequest.current={identity,promise:fetchJson<{claimed:boolean}>(`/api/maintenance/${status.releaseId}/popup/${status.popupVersion}/claim`,userKey,{method:'POST'})};
  claimRequest.current.promise.then(value=>{if(active){setClaimed(identity);if(value.claimed)setOpen(true);}}).catch(()=>{if(claimRequest.current?.identity===identity)claimRequest.current=null;});
  return()=>{active=false;};
 },[status?.state,status?.releaseId,status?.popupVersion,status?.popupPending,userKey,scope,identity,claimed]);
 if(!status||status.state==='Idle'||status.state==='Completed')return null;
 const time=(v:string|null)=>v?new Date(v).toLocaleString('ko-KR',{month:'numeric',day:'numeric',hour:'2-digit',minute:'2-digit'}):'확인 중';
 const campus=scope.startsWith('OSAN:')?'오산':scope.startsWith('CHEONGJU:')?'청주':scope.startsWith('INTERIOR_BUSBAR:')?'인테리어 부스바':'현재 이용 중인 캠퍼스';
 const note=status.writeBlocked?'업데이트 중에는 등록·수정·삭제가 제한됩니다. 완료 후 다시 저장해 주세요.':'작성 중인 내용은 업데이트 시작 전에 저장해 주세요.';
 const caption=status.writeBlocked?'업데이트 중에는 데이터를 저장할 수 없습니다.':'업데이트 예정 시간에는 데이터를 저장할 수 없습니다.';
 return <>{status.writeBlocked&&<div className="update-banner" role="status"><span>{caption} {`${time(status.startsAtUtc)} ~ ${time(status.expectedEndsAtUtc)}`}{unavailable&&' · 최신 상태 확인 중'}</span><button onClick={()=>setOpen(true)}>자세히 보기</button></div>}
 {open&&<section className="update-popup" role="region" aria-label="업데이트 안내"><header><h2>{status.title||'PMS 업데이트 안내'}</h2><button aria-label="업데이트 안내 닫기" onClick={()=>setOpen(false)}>×</button></header><div className="update-popup-body"><p>{caption}</p><dl><dt>{status.writeBlocked?'저장 제한 시간':'저장 제한 예정 시간'}</dt><dd>{`${time(status.startsAtUtc)} ~ ${time(status.expectedEndsAtUtc)}`}</dd></dl><dl><dt>저장 제한 대상</dt><dd>{campus}</dd></dl><p className="update-note">{note}</p></div><footer><small>계정마다 한 번 표시되는 안내입니다.</small>{status.noticeId&&<button onClick={()=>{onOpenNotice(status.noticeId!);setOpen(false);}}>업데이트 내용 보기</button>}<button onClick={()=>setOpen(false)}>확인</button></footer></section>}</>;
}
