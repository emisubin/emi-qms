import { useEffect, useId, useState } from 'react';
import { ApiError, getG2Days, saveG2Plans, saveG2Workspace } from './api';
import { DataEntryDialog } from './DataEntryDialog';
import type { G2Day, G2WorkspaceRequest, G2WriteAccess } from './g2';

export type G2EditorKind = 'plan' | 'actual' | 'count' | 'note';
const g2EditorTitles = { plan:'계획 입력', actual:'실적 입력', count:'실사 입력', note:'행 비고' };
const labels = { productionPlan:'생산 계획',deliveryPlan:'납품 계획',morningProduction:'오전 생산 실적',afternoonProduction:'오후 생산 실적',morningRepair:'오전 수리 실적',afternoonRepair:'오후 수리 실적',defect:'신규 불량 실적',delivery:'납품 실적',physicalCount:'납품 가능 재고 실사',defectInventoryCount:'불량 재고 실사' };
type QuantityField = keyof typeof labels;
type NoteField = 'planNote' | 'actualNote' | 'rowNote';
type Values = Record<QuantityField | NoteField,string>;
function valuesFor(day: G2Day): Values {
  return Object.fromEntries([...Object.keys(labels).map(key => [key,day[key as QuantityField]?.quantity?.toString() ?? '']), ...['planNote','actualNote','rowNote'].map(key=>[key,day[key as NoteField]?.text ?? ''])]) as Values;
}
function quantity(value:string,label:string) { if (!value.trim()) return null; const n=Number(value); if (!Number.isSafeInteger(n)||n<0||n>2147483647) throw new Error(`${label}은 0 이상의 정수로 입력해 주세요.`); return n; }
export function G2OperationEditor({ kind, initialDate, today, user, access, enabled, onClose, onSaved }: {
  kind:G2EditorKind; initialDate:string; today:string; user:string|undefined; access:G2WriteAccess; enabled:boolean; onClose:()=>void; onSaved:()=>Promise<void>;
}) {
  const [date,setDate]=useState(initialDate);
  const [end,setEnd]=useState(initialDate);
  const [source,setSource]=useState<G2Day|null>(null);
  const [values,setValues]=useState<Values|null>(null);
  const [tab,setTab]=useState<'morning'|'afternoon'|'delivery'>(access.production?'morning':'delivery');
  const [sundays,setSundays]=useState(true);
  const [overwrite,setOverwrite]=useState(false);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [retry,setRetry]=useState(0);
  const [preview,setPreview]=useState<G2Day[]|null>(null);
  const [previewError,setPreviewError]=useState('');
  const formId=useId();
  useEffect(()=>{
    let active=true;
    void getG2Days(user,date,date).then(data=>{if(active){const day=data.days.find(d=>d.date===date);if(!day)throw new Error('선택한 날짜를 찾을 수 없습니다.');setSource(day);setValues(valuesFor(day));}}).catch(e=>{if(active)setError(e instanceof Error?e.message:'기존 값을 불러오지 못했습니다.');});
    return()=>{active=false;};
  },[date,user,retry]);
  useEffect(()=>{
    if(kind!=='plan'||end<date)return;
    let active=true;
    void getG2Days(user,date,end).then(data=>{if(active){setPreview(data.days);setPreviewError('');}}).catch(()=>{if(active){setPreview(null);setPreviewError('기간 미리보기를 불러오지 못했습니다. 기간을 다시 선택해 주세요.');}});
    return()=>{active=false;};
  },[kind,date,end,user,retry]);
  function changeDate(next:string){if(!next)return;setDate(next);if(end<next||end===date)setEnd(next);setSource(null);setValues(null);setError('');setPreview(null);}
  const write=(field:keyof Values,value:string)=>setValues(v=>v?{...v,[field]:value}:v);
  const numeric=(field:QuantityField)=><label className="g2-form-row" key={field}><span>{labels[field]}</span><input type="number" min="0" max="2147483647" step="1" inputMode="numeric" value={values?.[field]??''} onChange={e=>write(field,e.target.value)} disabled={!source||busy||!enabled}/></label>;
  const noteField:NoteField=kind==='plan'?'planNote':kind==='note'?'rowNote':'actualNote';
  const canNote=kind==='plan'?access.planNote:access.note;
  async function save(){
    if(!source||!values||busy||!enabled)return;
    setError('');
    try {
      if((kind==='count'||(kind==='actual'&&!access.note))&&date>today)throw new Error('실적과 실사는 오늘 또는 과거 날짜에만 입력할 수 있습니다.');
      if(kind==='plan'&&end<date)throw new Error('종료일은 시작일 이후로 선택해 주세요.');
      if(kind==='plan'&&end!==date){
        if(!preview||previewError)throw new Error('기간 미리보기를 확인한 후 저장해 주세요.');
        const request:Parameters<typeof saveG2Plans>[1]={from:date,to:end,excludeSundays:sundays,overwrite};
        if(access.productionPlan&&values.productionPlan.trim())request.productionPlan=quantity(values.productionPlan,labels.productionPlan);
        if(access.deliveryPlan&&values.deliveryPlan.trim())request.deliveryPlan=quantity(values.deliveryPlan,labels.deliveryPlan);
        if(access.planNote&&values.planNote.trim())request.planNote=values.planNote;
        if(Object.keys(request).length===4)throw new Error('적용할 계획이나 비고를 입력해 주세요.');
        setBusy(true);await saveG2Plans(user,request);
      }else{
        const request:G2WorkspaceRequest={};
        const fields:QuantityField[]=kind==='plan'?[...(access.productionPlan?['productionPlan' as const]:[]),...(access.deliveryPlan?['deliveryPlan' as const]:[])]:kind==='count'&&access.inventory?['physicalCount','defectInventoryCount']:kind==='actual'&&date<=today?[...(access.production?['morningProduction','afternoonProduction','morningRepair','afternoonRepair','defect'] as const:[]),...(access.delivery?['delivery' as const]:[])]:[];
        for(const field of fields)if(values[field]!==valuesFor(source)[field])request[field]={quantity:quantity(values[field],labels[field]),expectedVersion:source[field]?.version??null};
        if(kind!=='count'&&canNote&&values[noteField]!==valuesFor(source)[noteField])request[noteField]={text:values[noteField]||null,expectedVersion:source[noteField]?.version??null};
        if(!Object.keys(request).length)throw new Error('변경한 값이 없습니다.');
        setBusy(true);await saveG2Workspace(user,date,request);
      }
      await onSaved();onClose();
    } catch(e){setError(e instanceof ApiError||e instanceof Error?e.message:'저장하지 못했습니다.');}
    finally{setBusy(false);}
  }
  const rangeDays=(preview??[]).filter(d=>!sundays||new Date(`${d.date}T00:00:00Z`).getUTCDay()!==0);
  let additions=0,kept=0,changed=0;
  for(const day of rangeDays)for(const field of ['productionPlan','deliveryPlan','planNote'] as const){if(!values?.[field]||(field==='productionPlan'&&!access.productionPlan)||(field==='deliveryPlan'&&!access.deliveryPlan)||(field==='planNote'&&!access.planNote))continue;const existing=field==='planNote'?day.planNote?.text:day[field]?.quantity;if(existing==null)additions++;else if(overwrite)changed++;else kept++;}
  return <DataEntryDialog title={g2EditorTitles[kind]} busy={busy} onClose={onClose} footer={<><button type="button" disabled={busy} onClick={onClose}>취소</button><button type="submit" form={formId} className="pms-save" disabled={busy||!source||!enabled}>{busy?'저장 중…':'저장'}</button></>}>
    <form id={formId} className="g2-editor" onSubmit={e=>{e.preventDefault();void save();}}>
      {kind==='plan'?<div className="g2-form-row"><span>계획 기간</span><div className="g2-date-pair"><label>시작일<input aria-label="계획 시작일" type="date" required value={date} disabled={busy} onChange={e=>changeDate(e.target.value)}/></label><label>종료일<input aria-label="계획 종료일" type="date" required min={date} value={end} disabled={busy} onChange={e=>{setEnd(e.target.value);setPreview(null);}}/></label></div></div>:<label className="g2-form-row"><span>기준일</span><input type="date" required value={date} max={kind==='count'||(kind==='actual'&&!access.note)?today:undefined} disabled={busy} onChange={e=>changeDate(e.target.value)}/></label>}
      {!source&&!error&&<p role="status">기존 값을 불러오는 중입니다.</p>}
      {kind==='actual'&&<div role="tablist" aria-label="실적 입력 구분" className="g2-entry-tabs">{(['morning','afternoon','delivery'] as const).map((t,i)=><button key={t} id={`${formId}-${t}`} type="button" role="tab" aria-selected={tab===t} aria-controls={`${formId}-panel`} tabIndex={tab===t?0:-1} onKeyDown={e=>{if(['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();const next=(i+(e.key==='ArrowRight'?1:2))%3;const value=(['morning','afternoon','delivery'] as const)[next];setTab(value);document.getElementById(`${formId}-${value}`)?.focus();}}} onClick={()=>setTab(t)}>{['오전','오후','납품'][i]}</button>)}</div>}
      <div role={kind==='actual'?'tabpanel':undefined} id={`${formId}-panel`} aria-labelledby={kind==='actual'?`${formId}-${tab}`:undefined}>
        {kind==='plan'&&<>{access.productionPlan&&numeric('productionPlan')}{access.deliveryPlan&&numeric('deliveryPlan')}</>}
        {kind==='count'&&access.inventory&&<>{numeric('physicalCount')}{numeric('defectInventoryCount')}</>}
        {kind==='actual'&&date<=today&&<>{tab==='morning'&&access.production&&<>{numeric('morningProduction')}{numeric('morningRepair')}</>}{tab==='afternoon'&&access.production&&<>{numeric('afternoonProduction')}{numeric('afternoonRepair')}{numeric('defect')}</>}{tab==='delivery'&&access.delivery&&numeric('delivery')}</>}
        {kind!=='count'&&canNote&&<label className="g2-form-row"><span>{kind==='plan'?'계획 비고':kind==='note'?'행 비고':'비고'}</span><textarea maxLength={2000} aria-label={kind==='actual'?'공유 실적 비고':kind==='plan'?'계획 비고':'행 비고'} value={values?.[noteField]??''} disabled={!source||busy||!enabled} onChange={e=>write(noteField,e.target.value)}/></label>}
      </div>
      {kind==='actual'&&date>today&&<p className="g2-form-hint">미래 날짜에는 비고만 입력할 수 있습니다.</p>}{kind==='actual'&&<p className="g2-form-hint">오전·오후·납품이 함께 사용하는 비고입니다. 기존 내용에 이어서 작성해 주세요.</p>}
      {kind==='plan'&&<><div className="g2-plan-checks"><label><input type="checkbox" checked={sundays} disabled={busy} onChange={e=>setSundays(e.target.checked)}/>일요일 제외</label><label><input type="checkbox" checked={overwrite} disabled={busy} onChange={e=>setOverwrite(e.target.checked)}/>기존 계획도 변경</label></div><p className="g2-plan-preview">{end===date?'선택한 날짜의 기존 값을 수정합니다.':preview?`적용 ${rangeDays.length}일 · 새 입력 ${additions}개 · 유지 ${kept}개 · 변경 ${changed}개`:previewError||'기간 미리보기 확인 중…'}</p></>}
      <p className="g2-form-hint">{kind==='plan'&&end!==date?'기간 입력에서 빈 칸은 기존 값을 유지합니다.':'기존 값을 비우고 저장하면 제거됩니다. 0은 실제 값으로 저장됩니다.'}</p>
      {error&&<div className="g2-form-error" role="alert">{error}{!source&&<button type="button" onClick={()=>{setError('');setRetry(n=>n+1);}}>다시 불러오기</button>}</div>}
    </form>
  </DataEntryDialog>;
}
