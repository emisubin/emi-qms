import { Fragment, useEffect, useId, useRef, useState } from 'react';
import { ApiError, getG2Days } from './api';
import { DataEntryDialog } from './DataEntryDialog';
import { G2OperationEditor, type G2EditorKind } from './G2OperationEditor';
import { monthBounds, todaySeoul, type G2Day, type G2RangeResponse, type G2WriteAccess } from './g2';
import { useG2Holidays } from './useG2Holidays';
import './g2-operations.css';

const g2EditorTitles = {plan:'계획 입력',actual:'실적 입력',count:'실사 입력'};
const weekday = (date: string) => new Date(`${date}T00:00:00Z`).getUTCDay();
const fmt = (value: number | null | undefined) => value == null ? '' : value.toLocaleString('ko-KR');
const rate = (actual: number | null | undefined, plan: number | null | undefined) => actual == null || !plan ? '' : `${Math.round(actual / plan * 100)}%`;
const sum = (days: G2Day[], value: (day: G2Day) => number | null | undefined) => {
  const quantities = days.map(value).filter((n): n is number => n != null);
  return quantities.length ? quantities.reduce((a, b) => a + b, 0) : null;
};
const columns = <colgroup><col style={{width:48}}/><col style={{width:172}}/><col span={6} style={{width:83}}/><col style={{width:85}}/><col style={{width:175}}/></colgroup>;
type Modal = { kind: G2EditorKind | 'menu' | 'detail'; date: string } | { kind: 'month' } | null;
function MonthPicker({ value, onClose, onSelect }: { value: string; onClose: () => void; onSelect: (value: string) => void }) {
  const [year, setYear] = useState(Number(value.slice(0,4)));
  const [month, setMonth] = useState(Number(value.slice(5,7)));
  const id = useId();
  return <DataEntryDialog title="조회 연월 선택" variant="compact" onClose={onClose} footer={<><button onClick={onClose}>취소</button><button className="pms-save" form={id} type="submit">조회</button></>}><form id={id} onSubmit={e=>{e.preventDefault();onSelect(`${year}-${String(month).padStart(2,'0')}`);}}><label className="g2-form-row"><span>연도</span><input aria-label="조회 연도" type="number" min="2000" max="2100" required value={year} onChange={e=>setYear(Number(e.target.value))}/></label><label className="g2-form-row"><span>월</span><select aria-label="조회 월" value={month} onChange={e=>setMonth(Number(e.target.value))}>{Array.from({length:12},(_,i)=><option key={i+1} value={i+1}>{i+1}월</option>)}</select></label></form></DataEntryDialog>;
}
function Detail({ day, future, mobile, editNote }: { day:G2Day; future:boolean; mobile?:boolean; editNote?:()=>void }) {
  const actual = (value: number|null|undefined) => future ? '' : fmt(value);
  return <div className={`g2-day-detail${mobile?' g2-day-detail--mobile':''}`}>
    {mobile&&<table><caption>납품·생산</caption><thead><tr><th>구분</th><th>계획</th><th>실적</th><th>달성률</th></tr></thead><tbody><tr><th>LSE 납품</th><td>{fmt(day.deliveryPlan?.quantity)}</td><td>{actual(day.delivery?.quantity)}</td><td>{future?'':rate(day.delivery?.quantity,day.deliveryPlan?.quantity)}</td></tr><tr><th>EMI 생산</th><td>{fmt(day.productionPlan?.quantity)}</td><td>{actual(day.productionTotal)}</td><td>{future?'':rate(day.productionTotal,day.productionPlan?.quantity)}</td></tr></tbody></table>}
    <div className="g2-detail-tables"><table><caption>{mobile?'생산·수리·불량 실적':'생산·수리 실적'}</caption><thead><tr><th>구분</th><th>오전</th><th>오후</th>{!mobile&&<th>합계</th>}</tr></thead><tbody><tr><th>생산</th><td>{actual(day.morningProduction?.quantity)}</td><td>{actual(day.afternoonProduction?.quantity)}</td>{!mobile&&<td>{actual(day.productionTotal)}</td>}</tr><tr><th>수리</th><td>{actual(day.morningRepair?.quantity)}</td><td>{actual(day.afternoonRepair?.quantity)}</td>{!mobile&&<td>{actual(day.repairTotal)}</td>}</tr>{mobile&&<tr><th>신규 불량</th><td></td><td>{actual(day.defect?.quantity)}</td></tr>}</tbody></table>
    {!mobile&&<table><caption>불량 현황</caption><thead><tr><th>구분</th><th>수량</th></tr></thead><tbody><tr><th>신규 불량</th><td>{actual(day.defect?.quantity)}</td></tr><tr><th>불량 재고</th><td>{actual(day.defectInventory)}</td></tr></tbody></table>}
    <table><caption>재고·실사</caption><thead><tr><th>구분</th><th>재고</th><th>실사</th></tr></thead><tbody><tr><th>납품 가능</th><td>{actual(day.inventory)}</td><td>{actual(day.physicalCount?.quantity)}</td></tr><tr><th>불량</th><td>{actual(day.defectInventory)}</td><td>{actual(day.defectInventoryCount?.quantity)}</td></tr></tbody></table></div>
    <table className="g2-detail-notes"><tbody><tr><th>계획 비고</th><td><div tabIndex={mobile?0:undefined}>{day.planNote?.text}</div></td></tr><tr><th>실적 비고</th><td><div tabIndex={mobile?0:undefined}>{day.actualNote?.text}</div></td></tr>{mobile&&<tr><th>행 비고</th><td>{editNote?<button className="g2-note-button" onClick={editNote}>{day.rowNote?.text||'비고 입력'}</button>:<div tabIndex={0}>{day.rowNote?.text}</div>}</td></tr>}</tbody></table>
  </div>;
}
export function G2OperationsPage({ developmentUserKey, canEditProduction, canEditDelivery, mutationEnabled, access: suppliedAccess }: {
  developmentUserKey:string|undefined; canEditProduction:boolean; canEditDelivery:boolean; mutationEnabled:boolean; access?:G2WriteAccess;
}) {
  // A new identity gets a fresh workspace, including dialogs and outstanding reads.
  return <Workspace key={developmentUserKey??'signed-in'} user={developmentUserKey} enabled={mutationEnabled} access={suppliedAccess??{production:canEditProduction,delivery:canEditDelivery,productionPlan:false,deliveryPlan:false,planNote:false,note:false,inventory:false}}/>;
}
function Workspace({user,enabled,access}:{user:string|undefined;enabled:boolean;access:G2WriteAccess}) {
  const [month,setMonth]=useState(todaySeoul().slice(0,7));
  const [data,setData]=useState<G2RangeResponse|null>(null);
  const [error,setError]=useState('');
  const [revision,setRevision]=useState(0);
  const [expanded,setExpanded]=useState<Set<string>>(new Set());
  const [modal,setModal]=useState<Modal>(null);
  const [menu,setMenu]=useState<string|null>(null);
  const [feedback,setFeedback]=useState('');
  const [mobile,setMobile]=useState(()=>typeof matchMedia==='function'&&matchMedia('(max-width:860px)').matches);
  const entryRef=useRef<HTMLButtonElement>(null);
  useEffect(()=>{if(typeof matchMedia!=='function')return;const query=matchMedia('(max-width:860px)');const update=()=>setMobile(query.matches);query.addEventListener('change',update);return()=>query.removeEventListener('change',update);},[]);
  useEffect(()=>{
    let active=true;
    const {from,to}=monthBounds(`${month}-01`);
    void getG2Days(user,from,to).then(result=>{if(active){setData(result);setError('');}}).catch(e=>{if(active)setError(e instanceof ApiError&&e.status===403?'G2 자료를 조회할 권한이 없습니다.':'G2 자료를 불러오지 못했습니다. 다시 시도해 주세요.');});
    return()=>{active=false;};
  },[month,user,revision]);
  useEffect(()=>{if(!menu)return;const close=(e:MouseEvent)=>{if(e.target instanceof Element&&!e.target.closest('.g2-data-entry'))setMenu(null);};const escape=(e:KeyboardEvent)=>{if(e.key==='Escape'){setMenu(null);entryRef.current?.focus();}};document.addEventListener('click',close);document.addEventListener('keydown',escape);return()=>{document.removeEventListener('click',close);document.removeEventListener('keydown',escape);};},[menu]);
  const days=data?.days??[];
  const holidays=useG2Holidays(user,days);
  const today=data?.today??todaySeoul();
  const entries=([{kind:'plan',allowed:access.productionPlan||access.deliveryPlan||access.planNote},{kind:'actual',allowed:access.production||access.delivery||access.note},{kind:'count',allowed:access.inventory}] as const).filter(item=>item.allowed);
  const canInput=entries.length>0;
  const openEditor=(kind:G2EditorKind,date:string)=>{setMenu(null);setModal({kind,date});};
  const openMenu=(date:string,key:string)=>{if(mobile)setModal({kind:'menu',date});else setMenu(menu===key?null:key);};
  const closeModal=()=>{setModal(null);queueMicrotask(()=>{if(document.activeElement===document.body)entryRef.current?.focus();});};
  const entryChoices=(date:string)=>entries.map(({kind})=><button type="button" key={kind} disabled={!enabled||((kind==='count'||(kind==='actual'&&!access.note))&&date>today)} onClick={()=>openEditor(kind,date)}>{g2EditorTitles[kind]}</button>);
  const entryButton=(date:string,top=false)=><div className="g2-data-entry"><button type="button" ref={top?entryRef:undefined} className="g2-primary" disabled={!enabled} aria-expanded={menu===(top?'top':date)||modal?.kind==='menu'} aria-haspopup={mobile?'dialog':true} onClick={()=>openMenu(date,top?'top':date)}>데이터 입력</button>{!mobile&&menu===(top?'top':date)&&<div className="g2-entry-options">{entryChoices(date)}</div>}</div>;
  const defaultDate=month===today.slice(0,7)?today:`${month}-01`;
  const cutoff=days.filter(d=>d.date<=today);
  const futureMonth=month>today.slice(0,7);
  const summaryDays=futureMonth?days:cutoff;
  const period=summaryDays.length?`${Number(month.slice(5))}.1 ~ ${Number(summaryDays.at(-1)!.date.slice(5,7))}.${Number(summaryDays.at(-1)!.date.slice(8))}${futureMonth?' 계획':''}`:'';
  const latestStock=[...cutoff].reverse().find(d=>d.inventory!=null||d.defectInventory!=null);
  const color=(date:string)=>holidays.has(date)||weekday(date)===0?'g2-date-red':weekday(date)===6?'g2-date-blue':'';
  const title=`${Number(month.slice(0,4))}년 ${Number(month.slice(5))}월`;
  const summary=(kind:'delivery'|'production')=>{const p=sum(summaryDays,d=>d[kind==='delivery'?'deliveryPlan':'productionPlan']?.quantity);const a=futureMonth?null:sum(summaryDays,d=>kind==='delivery'?d.delivery?.quantity:d.productionTotal);return <><div className="g2-query-title">{kind==='delivery'?'LSE 납품':'EMI 생산'} <small>{period}</small></div><div className="g2-summary-values"><span>계획<strong>{fmt(p)}</strong></span>{!futureMonth&&<><span>실적<strong>{fmt(a)}</strong></span><span>달성률<strong>{rate(a,p)}</strong></span></>}</div></>;};
  const monthCell=<><div className="g2-query-title">조회 월</div><div className="g2-month-title"><strong>{title}</strong><button aria-label="조회 연월 선택" title="조회 연월 선택" onClick={()=>{setMenu(null);setModal({kind:'month'});}}><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m15.5 15.5 5 5"/></svg></button></div></>;
  const totalRow=(label:string,rows:G2Day[],showActual:boolean)=><tr><th colSpan={2}>{label}</th>{(['delivery','production'] as const).map(kind=>{const p=sum(rows,d=>d[kind==='delivery'?'deliveryPlan':'productionPlan']?.quantity),a=showActual?sum(rows,d=>kind==='delivery'?d.delivery?.quantity:d.productionTotal):null;return <Fragment key={kind}><td className="g2-group">{fmt(p)}</td><td>{fmt(a)}</td><td>{rate(a,p)}</td></Fragment>;})}<td className="g2-group"/><td/></tr>;
  const selected=modal&&'date'in modal?days.find(d=>d.date===modal.date):null;
  return <section className="g2-operations">
    <div className="g2-operations-heading"><h1>생산/출하 관리</h1>{canInput&&entryButton(defaultDate,true)}</div><p className="g2-operations-description">일자별 납품·생산 계획과 실적, 재고를 확인합니다.</p>
    {!enabled&&canInput&&<p role="status">현재 조회만 가능합니다.</p>}{feedback&&<p role="status">{feedback}</p>}
    {error?<div className="g2-form-error" role="alert">{error}<button onClick={()=>{setError('');setRevision(n=>n+1);}}>다시 시도</button></div>:!data?<p role="status">G2 일별 자료를 불러오는 중입니다.</p>:<>
    {!mobile?<div className="g2-aligned-viewport"><div className="g2-aligned-content"><table className="g2-query-table" aria-label="월 조회 및 요약">{columns}<tbody><tr><td colSpan={2}>{monthCell}</td><td colSpan={3} className="g2-group">{summary('delivery')}</td><td colSpan={3} className="g2-group">{summary('production')}</td><td colSpan={2} className="g2-group"><div className="g2-query-title">재고 현황 <small>{latestStock?`${Number(latestStock.date.slice(5,7))}.${Number(latestStock.date.slice(8))} 기준`:''}</small></div><div className="g2-summary-values"><span>납품 가능<strong>{fmt(latestStock?.inventory)}</strong></span><span>불량<strong>{fmt(latestStock?.defectInventory)}</strong></span></div></td></tr></tbody></table>
    <div className="g2-table-caption"><strong>월간 생산·출하 현황</strong><span>수량 단위: 개</span></div><table className="g2-daily-table" aria-label="월간 생산·출하 현황">{columns}<thead><tr><th rowSpan={2}>주</th><th rowSpan={2}>일자</th><th colSpan={3} className="g2-group">LSE 납품</th><th colSpan={3} className="g2-group">EMI 생산</th><th rowSpan={2} className="g2-group">재고</th><th rowSpan={2}>비고</th></tr><tr>{[0,1].map(i=><Fragment key={i}><th className="g2-group">계획</th><th>실적</th><th>달성률</th></Fragment>)}</tr></thead><tbody>{days.map((d,index)=>{const future=d.date>today;const open=expanded.has(d.date);const toggle=()=>setExpanded(current=>{const next=new Set(current);if(next.has(d.date))next.delete(d.date);else next.add(d.date);return next;});return <Fragment key={d.date}><tr className={`g2-date-row ${d.date===today?'g2-today':''}`} onClick={toggle}><td>{Math.floor((index+weekday(`${month}-01`))/7)+1}주</td><td className={color(d.date)} title={holidays.get(d.date)}><button aria-expanded={open} aria-controls={`g2-detail-${d.date}`} onClick={e=>{e.stopPropagation();toggle();}} aria-label={`${d.date} 상세 ${open?'접기':'펼치기'}`}><span aria-hidden="true">{open?'−':'+'}</span> {Number(d.date.slice(5,7))}/{Number(d.date.slice(8))} ({'일월화수목금토'[weekday(d.date)]})</button></td><td className="g2-group">{fmt(d.deliveryPlan?.quantity)}</td><td>{future?'':fmt(d.delivery?.quantity)}</td><td>{future?'':rate(d.delivery?.quantity,d.deliveryPlan?.quantity)}</td><td className="g2-group">{fmt(d.productionPlan?.quantity)}</td><td>{future?'':fmt(d.productionTotal)}</td><td>{future?'':rate(d.productionTotal,d.productionPlan?.quantity)}</td><td className="g2-group">{future?'':fmt(d.inventory)}</td><td className="g2-row-note" onClick={e=>{e.stopPropagation();if(access.note&&enabled)openEditor('note',d.date);}}>{access.note?<button disabled={!enabled} aria-label={`${d.date} 행 비고 수정`}>{d.rowNote?.text}</button>:d.rowNote?.text}</td></tr>{open&&<tr id={`g2-detail-${d.date}`} className="g2-expanded-row"><td colSpan={10}><Detail day={d} future={future}/>{canInput&&<div className="g2-detail-actions">{entryButton(d.date)}</div>}</td></tr>}</Fragment>;})}</tbody><tfoot>{totalRow('오늘까지 누계',cutoff,true)}{totalRow('월 전체',days,month<today.slice(0,7))}</tfoot></table></div></div>:<>
    <div className="g2-mobile-month">{monthCell}</div><div className="g2-calendar-legend">납품·생산 실적 · 단위: 개</div><div className="g2-calendar" style={{gridTemplateRows:`24px repeat(${Math.ceil((weekday(`${month}-01`)+days.length)/7)},minmax(0,1fr))`}} aria-label={`${title} 생산·납품 달력`}>{Array.from('일월화수목금토').map((s,i)=><strong key={s} className={i===0?'g2-date-red':i===6?'g2-date-blue':''}>{s}</strong>)}{Array.from({length:weekday(`${month}-01`)},(_,i)=><div key={`blank${i}`} className="g2-calendar-empty"/>)}{days.map(d=><button key={d.date} className={d.date===today?'g2-today':''} aria-label={`${d.date} 상세 보기`} onClick={()=>setModal({kind:'detail',date:d.date})}><b className={color(d.date)}>{Number(d.date.slice(8))}</b><span>납품 <em>{d.date>today?'':fmt(d.delivery?.quantity)}</em></span><span>생산 <em>{d.date>today?'':fmt(d.productionTotal)}</em></span></button>)}</div></>}
    <p className="g2-operations-footnote">토요일은 파란색, 일요일·공휴일은 빨간색으로 표시합니다. 미래 날짜는 계획과 비고만 표시합니다.</p></>}
    {modal?.kind==='month'&&<MonthPicker value={month} onClose={closeModal} onSelect={value=>{if(value!==month){setData(null);setError('');setExpanded(new Set());setMonth(value);}closeModal();}}/>}
    {modal?.kind==='detail'&&selected&&<DataEntryDialog title={`${Number(selected.date.slice(5,7))}월 ${Number(selected.date.slice(8))}일 (${'일월화수목금토'[weekday(selected.date)]})`} variant="detail" onClose={closeModal} footer={canInput?<button className="pms-save" disabled={!enabled} onClick={()=>setModal({kind:'menu',date:selected.date})}>데이터 입력</button>:undefined}><Detail day={selected} future={selected.date>today} mobile editNote={access.note&&enabled?()=>openEditor('note',selected.date):undefined}/></DataEntryDialog>}
    {modal?.kind==='menu'&&<DataEntryDialog title="데이터 입력" variant="menu" onClose={closeModal} footer={<button onClick={closeModal}>닫기</button>}>{entryChoices(modal.date)}</DataEntryDialog>}
    {modal&&['plan','actual','count','note'].includes(modal.kind)&&'date'in modal&&<G2OperationEditor key={`${modal.kind}-${modal.date}`} kind={modal.kind as G2EditorKind} initialDate={modal.date} today={today} user={user} access={access} enabled={enabled} onClose={closeModal} onSaved={async()=>{setFeedback('저장했습니다.');setRevision(n=>n+1);}}/>}
  </section>;
}
