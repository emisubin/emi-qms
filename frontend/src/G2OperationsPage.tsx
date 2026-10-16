import { useG2Mobile } from './useG2Mobile';
import { G2MonthPicker, G2MonthHeading, G2Calendar } from './G2SharedControls';
import { G2PageFrame } from './G2PageFrame';
import { Fragment, useEffect, useRef, useState } from 'react';
import { ApiError, getG2Days } from './api';
import { DataEntryDialog } from './DataEntryDialog';
import { G2OperationEditor, type G2EditorKind } from './G2OperationEditor';
import { monthBounds, todaySeoul, type G2Day, type G2RangeResponse, type G2WriteAccess } from './g2';
import { useG2Holidays } from './useG2Holidays';
import './g2-approved/pc.css';
import './g2-approved/mobile.css';
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
function Detail({ day, future, mobile, editNote }: { day:G2Day; future:boolean; mobile?:boolean; editNote?:()=>void }) {
  const actual = (value:number|null|undefined) => future?'':fmt(value);
  const production=<><thead><tr><th>구분</th><th>오전</th><th>오후</th></tr></thead><tbody><tr><th>생산</th><td>{actual(day.morningProduction?.quantity)}</td><td>{actual(day.afternoonProduction?.quantity)}</td></tr><tr><th>수리</th><td colSpan={2}>{actual(day.repairTotal)}</td></tr>{mobile&&<tr><th>부적합</th><td colSpan={2}>{actual(day.defect?.quantity)}</td></tr>}</tbody></>;
  const nonconformanceStock=<><thead><tr><th>항목</th><th>수량</th></tr></thead><tbody><tr><th>단순 부적합 재고</th><td>{actual(day.simpleDefectInventory)}</td></tr><tr><th>복합·장기 부적합 재고</th><td>{actual(day.complexLongDefectInventory)}</td></tr></tbody></>;
  if(mobile)return <>
    <section className="detail-group"><h3>납품·생산 현황</h3><table className="popup-summary"><thead><tr><th></th><th>계획</th><th>실적</th><th>달성률</th></tr></thead><tbody><tr><th>LSE 납품</th><td>{fmt(day.deliveryPlan?.quantity)}</td><td>{actual(day.delivery?.quantity)}</td><td>{future?'':rate(day.delivery?.quantity,day.deliveryPlan?.quantity)}</td></tr><tr><th>EMI 생산</th><td>{fmt(day.productionPlan?.quantity)}</td><td>{actual(day.productionTotal)}</td><td>{future?'':rate(day.productionTotal,day.productionPlan?.quantity)}</td></tr></tbody></table></section>
    <section className="detail-group popup-section"><h3>생산·수리·부적합 실적</h3><table aria-label="생산·수리·부적합 실적">{production}</table></section>
    <section className="detail-group"><h3>G2 재고·실사</h3><table className="popup-inventory" aria-label="G2 재고·실사"><thead><tr><th>구분</th><th>재고</th><th>실사</th></tr></thead><tbody><tr><th>납품 가능</th><td>{actual(day.inventory)}</td><td>{actual(day.physicalCount?.quantity)}</td></tr></tbody></table></section>
    <section className="detail-group"><h3>부적합 재고</h3><table className="nonconformance-stock" aria-label="부적합 재고">{nonconformanceStock}</table></section>
    <section className="detail-group"><h3>비고</h3><div className="popup-memos"><div><b>계획 비고</b><p tabIndex={0}>{day.planNote?.text}</p></div><div><b>실적 비고</b><p tabIndex={0}>{day.actualNote?.text}</p></div>{editNote?<button className="row-note" onClick={editNote}><b>행 비고</b><span>{day.rowNote?.text||'비고 입력'}</span></button>:<div><b>행 비고</b><p tabIndex={0}>{day.rowNote?.text}</p></div>}</div></section>
  </>;
  return <><div className="detail-tables"><table className="mini-table"><caption>생산·수리 실적</caption>{production}</table><table className="mini-table"><caption>G2 재고 실사·신규 부적합</caption><thead><tr><th>항목</th><th>수량</th></tr></thead><tbody><tr><th>G2 재고 실사</th><td>{actual(day.physicalCount?.quantity)}</td></tr><tr><th>신규 부적합</th><td>{actual(day.defect?.quantity)}</td></tr></tbody></table><table className="mini-table nonconformance-stock"><caption>부적합 재고</caption>{nonconformanceStock}</table></div><table className="detail-notes" aria-label="계획 및 실적 비고"><tbody><tr><th>계획 비고</th><td>{day.planNote?.text}</td></tr><tr><th>실적 비고</th><td>{day.actualNote?.text}</td></tr></tbody></table></>;
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
  const [selectedDate,setSelectedDate]=useState(todaySeoul());
  const [feedback,setFeedback]=useState('');
  const mobile=useG2Mobile();
  const entryRef=useRef<HTMLButtonElement>(null);
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
  const entryChoices=(date:string)=>entries.map(({kind})=><button type="button" data-open={kind} key={kind} disabled={!enabled||((kind==='count'||(kind==='actual'&&!access.note))&&date>today)} onClick={()=>openEditor(kind,date)}>{g2EditorTitles[kind]}</button>);
  const entryButton=(date:string,top=false)=><div className="g2-data-entry entry-menu"><button type="button" ref={top?entryRef:undefined} className={top?'primary':undefined} disabled={!enabled} aria-expanded={menu===(top?'top':date)||modal?.kind==='menu'} aria-haspopup={mobile?'dialog':true} onClick={()=>openMenu(date,top?'top':date)}>데이터 입력</button>{!mobile&&menu===(top?'top':date)&&<div className="entry-options">{entryChoices(date)}</div>}</div>;
  const defaultDate=month===today.slice(0,7)?today:`${month}-01`;
  const cutoff=days.filter(d=>d.date<=today);
  const futureMonth=month>today.slice(0,7);
  const summaryDays=futureMonth?days:cutoff;
  const short=(date:string)=>date.slice(5).replace('-','.');
  const period=summaryDays.length?`${short(`${month}-01`)} ~ ${short(summaryDays.at(-1)!.date)}${futureMonth?' 계획':''}`:'';
  const latestStock=[...cutoff].reverse().find(d=>d.inventory!=null||d.defectInventory!=null);
  const color=(date:string)=>holidays.has(date)||weekday(date)===0?'red':weekday(date)===6?'blue':'';

  const summary=(kind:'delivery'|'production')=>{const p=sum(summaryDays,d=>d[kind==='delivery'?'deliveryPlan':'productionPlan']?.quantity);const a=futureMonth?null:sum(summaryDays,d=>kind==='delivery'?d.delivery?.quantity:d.productionTotal);return <><div className="query-title">{kind==='delivery'?'LSE 납품':'EMI 생산'} <span>{period}</span></div><div className="summary-values"><span>계획<b>{fmt(p)}</b></span>{<><span>실적<b>{fmt(a)}</b></span><span>달성률<b>{rate(a,p)}</b></span></>}</div></>;};
  const monthCell=<G2MonthHeading value={month} onOpen={()=>{setMenu(null);setModal({kind:'month'});}}/>;
  const totalRow=(label:string,rows:G2Day[],showActual:boolean)=><tr><th colSpan={2}>{label}</th>{(['delivery','production'] as const).map(kind=>{const p=sum(rows,d=>d[kind==='delivery'?'deliveryPlan':'productionPlan']?.quantity),a=showActual?sum(rows,d=>kind==='delivery'?d.delivery?.quantity:d.productionTotal):null;return <Fragment key={kind}><td className="group">{fmt(p)}</td><td>{fmt(a)}</td><td>{rate(a,p)}</td></Fragment>;})}<td className="group"/><td/></tr>;
  const selected=modal&&'date'in modal?days.find(d=>d.date===modal.date):null;
  return <G2PageFrame id="g2-approved-scope" title="생산/출하 관리" description="일자별 납품·생산 계획과 실적, 재고를 확인합니다." actions={canInput&&entryButton(defaultDate,true)} fitMobile><section className={`g2-operations g2-approved ${mobile?'phone':'page grid'}`}>
    {!enabled&&canInput&&<p role="status">현재 조회만 가능합니다.</p>}{feedback&&<p role="status">{feedback}</p>}
    {error?<div className="g2-form-error" role="alert">{error}<button onClick={()=>{setError('');setRevision(n=>n+1);}}>다시 시도</button></div>:!data?<p role="status">G2 일별 자료를 불러오는 중입니다.</p>:<>
    {!mobile?<div className="aligned-viewport"><div className="aligned-content"><table className="query-bar" aria-label="월 조회 및 요약">{columns}<tbody><tr><td colSpan={2}>{monthCell}</td><td colSpan={3} className="query-summary group">{summary('delivery')}</td><td colSpan={3} className="query-summary group">{summary('production')}</td><td colSpan={2} className="query-action group"><div className="query-title">재고 현황 <span>{latestStock?`${short(latestStock.date)} 기준`:''}</span></div><div className="summary-values stock-values"><span>납품 가능<b>{fmt(latestStock?.inventory)}</b></span><span>단순 부적합<b>{fmt(latestStock?.simpleDefectInventory)}</b></span><span>복합·장기 부적합<b>{fmt(latestStock?.complexLongDefectInventory)}</b></span></div></td></tr></tbody></table>
    <div className="caption"><strong>월간 생산·출하 현황</strong><span>수량 단위: 개</span></div><table id="operations" className="g2-daily-table" aria-label="월간 생산·출하 현황">{columns}<thead><tr><th rowSpan={2}>주</th><th rowSpan={2}>일자</th><th colSpan={3} className="group">LSE 납품</th><th colSpan={3} className="group">EMI 생산</th><th rowSpan={2} className="group">재고</th><th rowSpan={2}>비고</th></tr><tr>{[0,1].map(i=><Fragment key={i}><th className="group">계획</th><th>실적</th><th>달성률</th></Fragment>)}</tr></thead><tbody id="rows">{days.map((d,index)=>{const future=d.date>today;const open=expanded.has(d.date);const week=Math.floor((index+weekday(`${month}-01`))/7);const start=index===0||weekday(d.date)===0;const weekDays=days.filter((_,i)=>Math.floor((i+weekday(`${month}-01`))/7)===week);const span=weekDays.length+weekDays.filter(x=>expanded.has(x.date)).length;const toggle=()=>setExpanded(current=>{const next=new Set(current);if(next.has(d.date))next.delete(d.date);else next.add(d.date);return next;});return <Fragment key={d.date}><tr data-row={d.date} className={`g2-date-row ${d.date===today?'today':''}`} onClick={toggle}><>{start&&<td className="week" rowSpan={span}><span className="wk">{week+1}주</span></td>}</><td className={`date ${color(d.date)}`} title={holidays.get(d.date)}><button className="expander" aria-expanded={open} aria-controls={`g2-detail-${d.date}`} onClick={e=>{e.stopPropagation();toggle();}} aria-label={`${d.date} 상세 ${open?'접기':'펼치기'}`}><span aria-hidden="true">{open?'▾':'▸'}</span></button>{short(d.date)} ({'일월화수목금토'[weekday(d.date)]})</td><td className="group">{fmt(d.deliveryPlan?.quantity)}</td><td>{future?'':fmt(d.delivery?.quantity)}</td><td>{future?'':rate(d.delivery?.quantity,d.deliveryPlan?.quantity)}</td><td className="group">{fmt(d.productionPlan?.quantity)}</td><td>{future?'':fmt(d.productionTotal)}</td><td>{future?'':rate(d.productionTotal,d.productionPlan?.quantity)}</td><td className="group">{future?'':fmt(d.inventory)}</td><td className="memo" onClick={e=>{e.stopPropagation();if(access.note&&enabled)openEditor('note',d.date);}}>{access.note?<button className="inline-memo" disabled={!enabled} aria-label={`${d.date} 행 비고 수정`}>{d.rowNote?.text}</button>:d.rowNote?.text}</td></tr>{open&&<tr id={`g2-detail-${d.date}`} className="detail"><td colSpan={9}><Detail day={d} future={future}/>{canInput&&<div className="actions">{entryButton(d.date)}</div>}</td></tr>}</Fragment>;})}</tbody><tfoot className="totals">{totalRow('오늘까지 누계',cutoff,true)}{totalRow('월 전체',days,month<today.slice(0,7))}</tfoot></table></div></div>:<>
    <div className="month-block">{monthCell}</div><div className="list-caption"><span>월간 생산·출하 현황</span><span>단위: 개</span></div><div id="daily"><div className="calendar-legend">납품·생산 실적 · 단위: 개</div><G2Calendar month={month} days={days} selected={selectedDate} color={color} label="생산·납품 달력" buttonLabel="상세 보기" onSelect={date=>{setSelectedDate(date);setModal({kind:'detail',date});}} renderMetrics={d=><><span className="cal-metric">{d.date<=today&&d.delivery?.quantity!=null&&<><small>납품</small><em>{fmt(d.delivery.quantity)}</em></>}</span><span className="cal-metric">{d.date<=today&&d.productionTotal!=null&&<><small>생산</small><em>{fmt(d.productionTotal)}</em></>}</span></>}/></div></>}
    <p className="footnote">토요일은 파란색, 일요일·공휴일은 빨간색으로 표시합니다. 미래 날짜는 계획과 비고만 표시합니다.</p></>}
    {modal?.kind==='month'&&<G2MonthPicker value={month} onClose={closeModal} onSelect={value=>{if(value!==month){setData(null);setError('');setExpanded(new Set());setMonth(value);}closeModal();}}/>}
    {modal?.kind==='detail'&&selected&&<DataEntryDialog title={`${short(selected.date)} (${'일월화수목금토'[weekday(selected.date)]}) 상세`} variant="detail" onClose={closeModal} footer={canInput?<button className="primary" disabled={!enabled} onClick={()=>setModal({kind:'menu',date:selected.date})}>데이터 입력</button>:undefined}><Detail day={selected} future={selected.date>today} mobile editNote={access.note&&enabled?()=>openEditor('note',selected.date):undefined}/></DataEntryDialog>}
    {modal?.kind==='menu'&&<DataEntryDialog title={`${short(modal.date)} 데이터 입력`} variant="menu" onClose={closeModal} footer={<button id="sheet-cancel" onClick={closeModal}>취소</button>}>{entryChoices(modal.date)}</DataEntryDialog>}
    {modal&&['plan','actual','count','note'].includes(modal.kind)&&'date'in modal&&<G2OperationEditor key={`${modal.kind}-${modal.date}`} kind={modal.kind as G2EditorKind} initialDate={modal.date} today={today} user={user} access={access} enabled={enabled} onClose={closeModal} onSaved={async()=>{setFeedback('저장했습니다.');setRevision(n=>n+1);}}/>}
  </section></G2PageFrame>;
}
