import { useEffect, useState } from 'react';
import { fetchJson } from './api';

export type MaintenanceStatus={releaseId:string|null;version:number;popupVersion:number;title:string;body:string;startsAtUtc:string|null;expectedEndsAtUtc:string|null;state:'Idle'|'Announced'|'Active'|'Delayed'|'Failed'|'Completed';writeBlocked:boolean;noticeId:string|null;popupPending:boolean};
export function useMaintenanceStatus(userKey:string|undefined,scope:string,enabled:boolean) {
 const identity = `${userKey ?? ''}:${scope}`;
 const [result,setResult]=useState<{identity:string;state?:MaintenanceStatus;unavailable:boolean}>();
 useEffect(()=>{if(!enabled)return;const c=new AbortController();let running=false;
  const poll=async()=>{if(running)return;running=true;try{const data=await fetchJson<MaintenanceStatus>('/api/maintenance',userKey,{signal:c.signal});if(!c.signal.aborted){setResult({identity,state:data,unavailable:false});}}catch{if(!c.signal.aborted)setResult(previous=>({identity,state:previous?.identity===identity?previous.state:undefined,unavailable:true}));}finally{running=false;}};
  void poll();const timer=setInterval(()=>void poll(),10000);return()=>{c.abort();clearInterval(timer);};
 },[userKey,scope,enabled,identity]);
 return enabled && result?.identity === identity ? result : {state:undefined,unavailable:false};
}
