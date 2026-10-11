import { useEffect, useState } from 'react';
import { ApiError, getG2Days, getG2Home, listSystemHolidays } from './api';
import type { G2Day } from './g2';
import { getMaterialDay, type MaterialDay } from './g2Materials';
import { G2PageFrame } from './G2PageFrame';
import { achievement, achievementColor, deliveryForecast, displayQuantity as fmt, materialShortages, recentWorkday, SAFETY_STOCK, shiftDate } from './g2Dashboard';
import './g2-home-approved.css';

type Props={developmentUserKey:string|undefined;canManageInventory:boolean;canManageTargets:boolean;mutationEnabled:boolean};
type Snapshot={date:string|null;day:G2Day|undefined;days:G2Day[];materials:MaterialDay|null;materialError:string|null;through:string};
type State={kind:'loading';user:string|undefined}|{kind:'error';user:string|undefined;message:string}|{kind:'ready';user:string|undefined;data:Snapshot};
const shortDate=(date:string)=>`${Number(date.slice(5,7))}.${date.slice(8)}`;
const percent=(rate:number|null)=>rate==null?'—':rate.toLocaleString('ko-KR',{maximumFractionDigits:1});
function Metric({production,day}:{production:boolean;day:G2Day|undefined}) {
  const actual=production?day?.productionTotal:day?.delivery?.quantity;
  const plan=production?day?.productionPlan?.quantity:day?.deliveryPlan?.quantity;
  const rate=achievement(actual,plan),color=achievementColor(rate);
  return <section className="metric"><div className="section-head"><h2>{production?'EMI 생산':'LSE 납품'}</h2><span>계획 대비 달성률</span></div><div className="metric-body"><div className="gauge"><svg viewBox="0 0 240 140" role="img" aria-label={`실적 ${fmt(actual)}${actual==null?'':'대'} / 계획 ${fmt(plan)}${plan==null?'':'대'}, 달성률 ${percent(rate)}%`}><path d="M 25 116 A 95 95 0 0 1 215 116" pathLength="100" fill="none" stroke="#edf0f3" strokeWidth="17"/>{rate!=null&&<path d="M 25 116 A 95 95 0 0 1 215 116" pathLength="100" fill="none" stroke={color} strokeWidth="17" strokeDasharray={`${Math.min(rate,100)} 100`}/>}<text className={actual==null?'gauge-empty':'gauge-actual'} x="120" y="108" textAnchor="middle" style={{fill:color}}>{fmt(actual)}{actual!=null&&<tspan className="actual-unit" dx="5" style={{fill:'#282828'}}>대</tspan>}</text></svg></div><p className="achievement-below"><b>{percent(rate)}{rate!=null&&<small>%</small>}</b><span className="plan-inline">(계획 {fmt(plan)}{plan==null?'':'대'})</span></p></div>{production?<p className="attendance">출근 <b>오전 {fmt(day?.morningAttendanceTotal)}{day?.morningAttendanceTotal==null?'':'명'}</b><span>EMI {fmt(day?.morningEmiAttendance?.quantity)} · 도급 {fmt(day?.morningContractorAttendance?.quantity)}</span><i/><b>오후 {fmt(day?.afternoonAttendanceTotal)}{day?.afternoonAttendanceTotal==null?'':'명'}</b><span>EMI {fmt(day?.afternoonEmiAttendance?.quantity)} · 도급 {fmt(day?.afternoonContractorAttendance?.quantity)}</span></p>:<p className="metric-note">{rate==null?'계획과 실적 입력 후 달성률을 확인할 수 있습니다.':rate>=100?`납품 계획 ${fmt(plan)}대 완료`:`계획까지 ${fmt(Math.max(0,plan!-actual!))}대 남음`}</p>}</section>;
}
function Stock({data}:{data:Snapshot}) {
  const points=deliveryForecast(data.day,data.days),shortage=points.find(p=>p.quantity<SAFETY_STOCK);
  const min=Math.min(180,...points.map(p=>p.quantity)),max=Math.max(240,...points.map(p=>p.quantity));
  const y=(n:number)=>44+(max-n)/(max-min)*144;
  const x=(i:number)=>points.length===1?70:70+i*519/(points.length-1);
  const line=points.map((p,i)=>`${i?'L':'M'}${x(i)} ${y(p.quantity)}`).join('');
  const ticks=[max,Math.round((max+SAFETY_STOCK)/2),SAFETY_STOCK,min].filter((v,i,a)=>a.indexOf(v)===i);
  return <section className="stock"><div className="section-head"><h2>제품 재고 · 납품 전망</h2><span>안전재고 180대</span></div><div className="stock-summary"><div><span>기준일 재고</span><strong>{fmt(data.day?.inventory)}{data.day?.inventory!=null&&<small>대</small>}</strong></div><div className="forecast-callout"><span>안전재고 미달 예상</span><b>{shortage?`${Number(shortage.date.slice(5,7))}월 ${Number(shortage.date.slice(8))}일 ${shortage===points[0]?'기준일':'납품 후'}`:points.length>1?'조회 기간 내 없음':'확인 대기'}</b><p>{shortage?`예상 ${fmt(shortage.quantity)}대 · 안전재고보다 ${fmt(SAFETY_STOCK-shortage.quantity)}대 부족`:points.length>1?`${shortDate(data.through)}까지 등록된 납품 계획 기준`:'재고와 납품 계획을 확인해 주세요.'}</p></div></div>{points.length>0?<svg className="stock-chart" viewBox="0 0 700 236" role="img" aria-label={`납품 전망: ${points.map(p=>`${p.date} ${p.quantity}대`).join(', ')}`}><g className="gridline">{ticks.map(t=><path key={t} d={`M70 ${y(t)}H650`}/>)}</g><g className="axis">{ticks.map(t=><text key={t} x="40" y={y(t)+4}>{fmt(t)}</text>)}</g><text className="safety-label" x="648" y={y(SAFETY_STOCK)-8} textAnchor="end">안전재고 180대</text><path d={`${line}L${x(points.length-1)} 208H70Z`} fill="#EDF3FB"/><path className="safety" d={`M70 ${y(SAFETY_STOCK)}H650`}/><path d={line} fill="none" stroke="#477DC0" strokeWidth="2.5" strokeDasharray="6 4"/>{points.map((p,i)=><g key={p.date}><circle cx={x(i)} cy={y(p.quantity)} r={p.quantity<180?6:5} fill={p.quantity<180?'#c63b42':'#fff'} stroke={p.quantity<180?'#c63b42':'#477DC0'} strokeWidth="2"><title>{p.date} · {fmt(p.quantity)}대</title></circle>{(points.length<=7||i===0||i===points.length-1||p===shortage)&&<><text className="point-label" x={x(i)} y={y(p.quantity)-16} style={{fill:p.quantity<180?'#b3232e':'#333'}}>{fmt(p.quantity)}대</text><text className="axis dates" x={x(i)} y="230">{shortDate(p.date)} {i?'납품 후':'기준'}</text></>}</g>)}</svg>:<p className="empty-chart">기준일 재고가 입력되지 않았습니다.</p>}<p className="note">{shortDate(data.through)}까지 등록된 납품 계획 반영 · 추가 생산 미반영</p></section>;
}
export function G2HomePage({developmentUserKey}:Props) {
  const [state,setState]=useState<State>({kind:'loading',user:developmentUserKey});
  const [retry,setRetry]=useState(0);
  useEffect(()=>{
    let active=true;
    void (async()=>{
      try {
        const home=await getG2Home(developmentUserKey);
        const from=shiftDate(home.today,-31),through=shiftDate(home.today,30);
        const [range,holidays]=await Promise.all([getG2Days(developmentUserKey,from,through),listSystemHolidays(developmentUserKey,{dateFrom:from,dateTo:home.today})]);
        const date=recentWorkday(home.today,range.days,new Set(holidays.map(h=>h.holidayDate)));
        let materials:MaterialDay|null=null,materialError:string|null=null;
        if(date){try{materials=await getMaterialDay(developmentUserKey,date);}catch(error){materialError=error instanceof ApiError&&error.status===403?'자재를 조회할 권한이 없습니다.':'자재 실사 데이터를 불러오지 못했습니다.';}}
        if(active)setState({kind:'ready',user:developmentUserKey,data:{date,day:range.days.find(d=>d.date===date),days:range.days,materials,materialError,through}});
      }catch(error){if(active)setState({kind:'error',user:developmentUserKey,message:error instanceof ApiError&&error.status===403?'G2 현황을 조회할 권한이 없습니다.':'G2 현황을 불러오지 못했습니다.'});}
    })();
    return()=>{active=false;};
  },[developmentUserKey,retry]);
  if(state.user!==developmentUserKey||state.kind==='loading')return <G2PageFrame id="g2-home-scope" title="G2 홈" description="최근 작업일의 생산·납품 실적과 재고 현황을 확인합니다."><p role="status">G2 현황을 불러오는 중입니다.</p></G2PageFrame>;
  if(state.kind==='error')return <G2PageFrame id="g2-home-scope" title="G2 홈" description="최근 작업일의 생산·납품 실적과 재고 현황을 확인합니다."><p role="alert">{state.message}</p><button onClick={()=>{setState({kind:'loading',user:developmentUserKey});setRetry(n=>n+1);}}>다시 시도</button></G2PageFrame>;
  const data=state.data,day=data.day;
  const defectRate=achievement(day?.defect?.quantity,day?.productionTotal);
  const materials=materialShortages(data.materials?.items??[]);
  const weekday=data.date?new Date(`${data.date}T00:00:00Z`).getUTCDay():0;
  return <G2PageFrame id="g2-home-scope" title="G2 홈" description="최근 작업일의 생산·납품 실적과 재고 현황을 확인합니다." actions={<div className="date"><span>최근 작업일</span><strong>{data.date?.replaceAll('-','.')??'확인 필요'} <small style={{color:weekday===6?'#376ca3':weekday===0?'#b3232e':undefined}}>{data.date?'일월화수목금토'[weekday]:''}</small></strong></div>}><div className="overview"><div className="top-metrics"><Metric production day={day}/><Metric production={false} day={day}/><section className="quality"><div className="section-head"><h2>불량률</h2><span>기준일 생산 대비</span></div><div className="quality-body"><div className="big">{percent(defectRate)}{defectRate!=null&&<small>%</small>}</div><div className="quality-detail"><span>신규 불량 <b>{fmt(day?.defect?.quantity)}{day?.defect?.quantity==null?'':'대'}</b></span><span>생산 수량 <b>{fmt(day?.productionTotal)}{day?.productionTotal==null?'':'대'}</b></span></div></div><p className="metric-note">기준일 신규 불량 ÷ 생산 수량</p></section></div><div className="bottom-grid"><Stock data={data}/><section className="materials"><div className="section-head"><h2>안전재고 미달 자재 {data.materials&&<em>{materials.rows.length}</em>}</h2><span>180대 미만</span></div>{data.materialError?<p role="alert">{data.materialError}</p>:!data.date||!data.materials?<p className="note">자재 실사 기준일을 확인할 수 없습니다.</p>:data.materials.items.length===0?<p className="note">등록된 자재 품목이 없습니다. 관리자에게 문의해 주세요.</p>:<><table className="compact-materials" aria-label="안전재고 미달 자재"><thead><tr><th scope="col">품목명</th><th scope="col">규격</th><th scope="col">재고 부족분</th><th scope="col">생산 가능 대수</th></tr></thead><tbody>{materials.rows.map(({item,capacity,shortage})=><tr key={item.id}><td><small>{item.category}</small><strong title={`${item.name} · ${item.spec}`}>{item.name}</strong></td><td className="material-spec">{item.spec}</td><td>{fmt(shortage)}<span>{item.unit}</span></td><td className="capacity">{fmt(capacity)}<span>대</span></td></tr>)}</tbody></table>{materials.rows.length===0&&<p className="note">{materials.incomplete?'입력된 재고를 확인해 주세요.':'안전재고 미달 자재가 없습니다.'}</p>}{materials.incomplete>0&&<p className="note">실사 미입력·확인 필요 {materials.incomplete}개 품목은 계산에서 제외했습니다.</p>}<p className="note">부족분: 180대 생산에 필요한 자재 수량 기준<br/>실사일 {data.date?shortDate(data.date):'확인 필요'}</p></>}</section></div></div></G2PageFrame>;
}
