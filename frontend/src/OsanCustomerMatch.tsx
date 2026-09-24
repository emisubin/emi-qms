import { useEffect, useState, useId, useRef } from 'react';
import { fetchJson } from './api';
import './osan-customer-match.css';
export type CustomerOption={customerId:string;name:string;version?:number};
export function OsanCustomerMatch({value,customerId,onChange,userKey,disabled,retainedCustomer}:{value:string;customerId?:string;onChange:(name:string,id?:string)=>void;userKey?:string;disabled?:boolean;retainedCustomer?:CustomerOption}) {
 const resultId=useId();const changeRef=useRef(onChange);useEffect(()=>{changeRef.current=onChange;},[onChange]);
 const [customers,setCustomers]=useState<CustomerOption[]>([]);const [error,setError]=useState('');const [open,setOpen]=useState(false);const [search,setSearch]=useState('');
 useEffect(()=>{const c=new AbortController();fetchJson<{items:CustomerOption[]}>('/api/osan/customers',userKey,{signal:c.signal}).then(v=>setCustomers(v.items)).catch(e=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'고객사를 불러올 수 없습니다.');});return()=>c.abort();},[userKey]);
 const normalize=(s:string)=>s.trim().replace(/\s+/g,'').toLocaleLowerCase();
 const candidates=customers.filter(c=>normalize(c.name).includes(normalize(value))&&value.trim());
 useEffect(()=>{
  if(customerId || !value.trim() || !customers.length)return;
  const text=value.trim().replace(/\s+/g,'').toLocaleLowerCase();
  const exact=customers.filter(c=>c.name.trim().replace(/\s+/g,'').toLocaleLowerCase()===text);
  const matches=exact.length?exact:customers.filter(c=>c.name.trim().replace(/\s+/g,'').toLocaleLowerCase().includes(text));
  if(matches.length===1)changeRef.current(value,matches[0].customerId);
 },[customers,value,customerId]);
 const matched=customers.find(c=>c.customerId===customerId);
 const choose=(c:CustomerOption)=>{onChange(c.name,c.customerId);setOpen(false);};
 const type=(text:string)=>{if(retainedCustomer&&text===retainedCustomer.name){onChange(text,retainedCustomer.customerId);return;}const exact=customers.filter(c=>normalize(c.name)===normalize(text));const matching=exact.length?exact:customers.filter(c=>normalize(c.name).includes(normalize(text))&&text.trim());onChange(text,matching.length===1?matching[0].customerId:undefined);};
 return <div className="osan-customer-match"><button type="button" className="customer-picker-button" aria-label="고객사 선택" disabled={disabled} onClick={()=>{setSearch(value);setOpen(true);}}>고객사 선택</button>
 <input aria-label="고객사" value={value} maxLength={200} disabled={disabled} onChange={e=>type(e.target.value)} autoComplete="off" aria-describedby={resultId}/>
 <small id={resultId} role="status">{error|| (matched?`연결된 고객사: ${matched.name}`:retainedCustomer&&retainedCustomer.customerId===customerId&&retainedCustomer.name===value?`기존 고객사 유지: ${value}`:!value?'고객사 이름을 입력하세요.':candidates.length>1?'여러 고객사가 일치합니다. 고객사를 선택해 주세요.':'등록된 고객사와 연결해야 저장할 수 있습니다.')}</small>
 {!matched&&candidates.length>1&&<button type="button" disabled={disabled} onClick={()=>{setSearch(value);setOpen(true);}}>일치하는 고객사 {candidates.length}개 확인</button>}
 {open&&<div className="customer-match-backdrop" onClick={()=>setOpen(false)}><section role="dialog" aria-modal="true" aria-label="고객사 선택" onClick={e=>e.stopPropagation()}><header><h3>고객사 선택</h3><button type="button" aria-label="닫기" onClick={()=>setOpen(false)}>×</button></header><input autoFocus aria-label="고객사 목록 검색" placeholder="고객사 이름 검색" value={search} onChange={e=>setSearch(e.target.value)}/><div className="customer-match-list">{customers.filter(c=>normalize(c.name).includes(normalize(search))).map(c=><button type="button" key={c.customerId} onClick={()=>choose(c)}>{c.name}</button>)}</div><small>등록된 고객사가 없으면 관리자에게 등록을 요청하세요.</small></section></div>}
 </div>;
}
