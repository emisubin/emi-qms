import { useId, useState, type CSSProperties, type ReactNode } from 'react';
import { DataEntryDialog } from './DataEntryDialog';
import type { G2Day } from './g2';

export function G2MonthPicker({ value, onClose, onSelect }: { value: string; onClose: () => void; onSelect: (value: string) => void }) {
  const [year, setYear] = useState(Number(value.slice(0,4)));
  const [month, setMonth] = useState(Number(value.slice(5,7)));
  const id = useId();
  return <DataEntryDialog title="조회 연월 선택" variant="compact" onClose={onClose} footer={<><button onClick={onClose}>취소</button><button className="primary" form={id} type="submit">조회</button></>}><form id={id} onSubmit={e=>{e.preventDefault();onSelect(`${year}-${String(month).padStart(2,'0')}`);}}><label className="form-row"><span>연도</span><select aria-label="조회 연도" value={year} onChange={e=>setYear(Number(e.target.value))}>{Array.from({length:101},(_,i)=><option key={2000+i} value={2000+i}>{2000+i}년</option>)}</select></label><label className="form-row"><span>월</span><select aria-label="조회 월" value={month} onChange={e=>setMonth(Number(e.target.value))}>{Array.from({length:12},(_,i)=><option key={i+1} value={i+1}>{i+1}월</option>)}</select></label></form></DataEntryDialog>;
}

export function G2MonthHeading({value,onOpen}:{value:string;onOpen:()=>void}) {
  return <><div className="query-title">조회 월</div><div className="month-title-row"><strong>{Number(value.slice(0,4))}년 {Number(value.slice(5))}월</strong><button aria-label="조회 연월 선택" title="조회 연월 선택" onClick={onOpen}><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m15.5 15.5 5 5"/></svg></button></div></>;
}
export function G2Calendar({month,days,selected,color,label,buttonLabel,onSelect,renderMetrics}:{month:string;days:G2Day[];selected:string;color:(date:string)=>string;label:string;buttonLabel:string;onSelect:(date:string)=>void;renderMetrics:(day:G2Day)=>ReactNode}) {
  const first=new Date(`${month}-01T00:00:00Z`).getUTCDay();
  const count=new Date(Number(month.slice(0,4)),Number(month.slice(5)),0).getDate();
  return <div className="calendar" style={{'--weeks':Math.ceil((first+count)/7)} as CSSProperties} aria-label={`${Number(month.slice(0,4))}년 ${Number(month.slice(5))}월 ${label}`}>
    {Array.from('일월화수목금토').map((s,i)=><div key={s} className={`weekday ${i===0?'red':i===6?'blue':''}`}>{s}</div>)}
    {Array.from({length:first},(_,i)=><div key={`blank${i}`} className="calendar-empty"/>)}
    {days.map(d=><button key={d.date} className={d.date===selected?'selected':''} aria-label={`${d.date} ${buttonLabel}`} onClick={()=>onSelect(d.date)}><b className={color(d.date)}>{Number(d.date.slice(8))}</b>{renderMetrics(d)}</button>)}
  </div>;
}
export function G2InputTabs({id,label,tabs,value,onChange}:{id:string;label:string;tabs:ReadonlyArray<{value:string;label:string}>;value:string;onChange:(value:string)=>void}) {
  return <div role="tablist" aria-label={label} className="tabs">{tabs.map((tab,i)=><button key={tab.value} id={`${id}-${tab.value}`} type="button" role="tab" aria-selected={value===tab.value} aria-controls={`${id}-panel`} tabIndex={value===tab.value?0:-1} onClick={()=>onChange(tab.value)} onKeyDown={e=>{
    if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;
    e.preventDefault();const index=e.key==='Home'?0:e.key==='End'?tabs.length-1:(i+(e.key==='ArrowRight'?1:tabs.length-1))%tabs.length;
    onChange(tabs[index].value);document.getElementById(`${id}-${tabs[index].value}`)?.focus();
  }}>{tab.label}</button>)}</div>;
}
