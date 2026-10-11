import { useCallback, useEffect, useState } from 'react';
import { OsanMenuHeading } from './OsanMenuHeading';
import { useNativeDialog } from './useNativeDialog';
import { todaySeoul } from './g2';
import { calculateMaterial, getMaterialImage, quantityKeys, type MaterialItem, type MaterialValues, type QuantityKey } from './g2Materials';
import './g2-materials-mobile.css';

export type G2MaterialsMobileProps = {
  user: string | undefined;
  date: string;
  onDateChange: (date: string) => void;
  items: MaterialItem[];
  draft: Record<number, MaterialValues>;
  canEdit: boolean;
  loading: boolean;
  saving: boolean;
  dateLocked?: boolean;
  error: string;
  status: string;
  onChange: (id: number, key: QuantityKey, value: number | null) => void;
  onRetry: () => void;
};
const fmt = (value: unknown) => value == null ? '' : typeof value === 'number' ? value.toLocaleString('ko-KR', { maximumFractionDigits: 4 }) : String(value);
const fields: { title: string; fields: [QuantityKey, string][] }[] = [
  { title: '제조 Part', fields: [['working', '제작중'], ['kitting', '키팅대차'], ['rack', '적재 랙']] },
  { title: '자재 Part', fields: [['materials', '자재실 합계'], ['transit', '이동중']] },
];
function totals(values: MaterialValues, item: MaterialItem) {
  const calculated = calculateMaterial(values, item.setQuantity);
  return quantityKeys.every(key => values[key] == null) && !Object.keys(values.calculationOverrides??{}).length ? { ...calculated, Q: null, Y: null, Z: null } : calculated;
}
function Picture({ item, user }: { item: MaterialItem; user: string | undefined }) {
  const [image, setImage] = useState<{ id: number; user: string | undefined; url: string } | null>(null);
  const hasImage = !!item.imageKeys?.length;
  useEffect(() => {
    if (!hasImage) return;
    const controller = new AbortController();
    let url: string | undefined;
    getMaterialImage(user, item.id, 0, controller.signal).then(blob => {
      if (controller.signal.aborted) return;
      url = URL.createObjectURL(blob);
      setImage({ id: item.id, user, url });
    }).catch(() => { /* Keep the approved placeholder if an image is unavailable. */ });
    return () => { controller.abort(); if (url) URL.revokeObjectURL(url); };
  }, [hasImage, item.id, user]);
  return hasImage && image?.id === item.id && image.user === user
    ? <img className="thumb" src={image.url} alt="" loading="lazy" />
    : <span className="thumb placeholder"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m3 7 9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10" /></svg></span>;
}
function Pair({ label, value, group = false }: { label: string; value: unknown; group?: boolean }) {
  return <tr className={group ? 'group' : ''}><th scope="row">{label}</th><td>{fmt(value)}</td></tr>;
}
function Detail({ item, onClose, ...props }: G2MaterialsMobileProps & { item: MaterialItem; onClose: () => void }) {
  const [tab, setTab] = useState<'stock' | 'info'>('stock');
  const [editing, setEditing] = useState(false);
  const [validation, setValidation] = useState('');
  const closeDetail = useCallback((open: boolean) => { if (!open) onClose(); }, [onClose]);
  const dialog = useNativeDialog(true, closeDetail);
  const formDialog = useNativeDialog(editing, setEditing);
  const values = props.draft[item.id] ?? item.values;
  const result = totals(values, item);
  const autosave = validation || props.error || (props.saving ? '자동 저장 중…' : props.status || '변경한 값은 자동 저장됩니다');
  return <>
    <dialog id="detail" ref={dialog} aria-label="자재 상세">
      <header className="modalhead"><strong>자재 상세</strong><button type="button" aria-label="닫기" onClick={onClose}>×</button></header>
      <div id="identity"><div className="identity"><Picture item={item} user={props.user} /><div><strong>{item.name}</strong><p>{item.spec}</p></div></div></div>
      <nav className="tabs" aria-label="자재 상세 보기"><button type="button" className={tab === 'stock' ? 'active' : ''} aria-pressed={tab === 'stock'} onClick={() => setTab('stock')}>재고 현황</button><button type="button" className={tab === 'info' ? 'active' : ''} aria-pressed={tab === 'info'} onClick={() => setTab('info')}>품목 정보</button></nav>
      <section id="detailbody"><table className="kv"><tbody>{tab === 'stock' ? <>
        <Pair label="제작중" value={values.working} /><Pair label="키팅대차" value={values.kitting} /><Pair label="적재 랙" value={values.rack} /><Pair label="제조실 합계" value={result.Q} group /><Pair label="자재실 합계" value={values.materials} /><Pair label="이동중" value={values.transit} /><Pair label="생산가능대수 합계" value={result.Z} group />
      </> : <>
        <Pair label="구분" value={item.category.trim()} /><Pair label="업체명" value={item.vendor} /><Pair label="품목코드" value={item.code} /><Pair label="1SET 수량" value={item.setQuantity} /><Pair label="BOX" value={item.packBox} /><Pair label="PT(EA)" value={item.packPalletEa} /><Pair label="PT(BOX)" value={item.packPalletBox} /><Pair label="포장 비고" value={item.packNote} />
      </>}</tbody></table></section>
      <div className="modalfoot"><span id="total">총재고 합계<b>{fmt(result.Y)} <small>{item.unit}</small></b></span>{props.canEdit && <button id="edit" type="button" className="primary" onClick={() => setEditing(true)}>실사 입력</button>}</div>
    </dialog>
    <dialog id="form" className="mi-dialog mi-form-dialog" ref={formDialog} aria-labelledby="form-title">
      <form id="editform" onSubmit={event => event.preventDefault()}>
        <header className="mi-header"><h2 id="form-title">실사 입력</h2><button type="button" aria-label="입력 닫기" onClick={() => setEditing(false)}>×</button></header>
        <section className="mi-body"><div id="formname">{item.name}{Object.keys(values.calculationOverrides??{}).length>0&&<small>직접 입력된 합계 포함 · 관련 수량 수정 시 자동 계산</small>}</div><div id="fields">{fields.map(part => <fieldset className="part" key={part.title}><legend>{part.title}</legend>{part.fields.map(([key, label]) => <label className="mi-field" key={key}><span>{label}</span><input aria-label={label} name={key} type="number" inputMode="decimal" min="0" max="999999999999.9999" step="0.0001" value={values[key] ?? ''} disabled={!props.canEdit || props.loading} onChange={event => {
          const input = event.currentTarget;
          if (!input.validity.valid) { setValidation('0 이상의 수량을 소수점 넷째 자리까지 입력해 주세요'); return; }
          const value = input.value === '' ? null : input.valueAsNumber;
          if (value !== null && !Number.isFinite(value)) return;
          setValidation('');
          props.onChange(item.id, key, value);
        }} /></label>)}</fieldset>)}</div><p className="mi-hint">빈칸은 미입력, 0은 재고 없음으로 유지됩니다.</p></section>
        <div className="mi-footer"><span id="autosave-state" role={props.error || validation ? 'alert' : 'status'}>{autosave}</span>{props.error && <button type="button" disabled={props.saving} onClick={props.onRetry}>다시 시도</button>}<button type="button" onClick={() => setEditing(false)}>닫기</button></div>
      </form>
    </dialog>
  </>;
}
export function G2MaterialsMobile(props: G2MaterialsMobileProps) {
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('전체');
  const [selected, setSelected] = useState<number | null>(null);
  const closeDetail = useCallback(() => setSelected(null), []);
  const list = props.items.filter(item => (category === '전체' || item.category.trim() === category) && [item.name, item.spec, item.code].some(value => value.toLowerCase().includes(query.toLowerCase())));
  const item = props.items.find(value => value.id === selected);
  return <section id="g2-material-mobile-scope">
    <div className="title-wrap"><OsanMenuHeading title="자재 재고 현황" description="일일 자재 실사 재고를 조회하고 입력합니다." /></div>
    <section className="controls"><div className="datebar"><label htmlFor="g2m-mobile-date">실사일</label><input id="g2m-mobile-date" type="date" max={todaySeoul()} value={props.date} disabled={props.saving || props.dateLocked || props.loading} onChange={event => { if (event.target.value) { closeDetail(); props.onDateChange(event.target.value); } }} /></div><label className="search"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10" cy="10" r="6.5" /><path d="m15 15 5 5" /></svg><input placeholder="품명·규격·품목코드 검색" aria-label="자재 검색" value={query} onChange={event => setQuery(event.target.value)} /></label><nav aria-label="자재 구분">{['전체', '사급', '도급', '부자재'].map(value => <button key={value} type="button" className={category === value ? 'active' : ''} aria-pressed={category === value} onClick={() => setCategory(value)}>{value}</button>)}</nav></section>
    <div className="listhead"><span>{category} {list.length}개</span><div className="number-head"><span>총재고<br />합계</span><span>G2 생산<br />가능 대수</span></div></div>
    <section id="list">{props.loading ? <p className="empty" role="status">실사 데이터를 불러오는 중입니다.</p> : list.map(value => {
      const result = totals(props.draft[value.id] ?? value.values, value);
      return <button className="item" type="button" key={value.id} onClick={() => setSelected(value.id)}><Picture item={value} user={props.user} /><span className="itemtext"><strong>{value.name}</strong><span className="spec">{value.spec}<br />{value.category.trim()} · {value.code}</span></span><span className="number-cols"><span className="qty">{fmt(result.Y)}<small>{value.unit}</small></span><span className={`qty possible ${result.Z !== null && result.Z < 180 ? 'low' : ''}`}>{fmt(result.Z)}<small>대</small></span></span></button>;
    })}{!props.loading && !list.length && !props.error && <p className="empty">{props.items.length ? '검색 결과가 없습니다.' : '등록된 자재 품목이 없습니다. 관리자에게 문의해 주세요.'}</p>}</section>
    {props.error ? <div className="save-status" role="alert">{props.error} <button type="button" disabled={props.saving} onClick={props.onRetry}>다시 시도</button></div> : <div className="save-status" role="status">{props.saving ? '자동 저장 중…' : props.status}</div>}
    {item && <Detail key={`${props.date}:${item.id}`} {...props} item={item} onClose={closeDetail} />}
  </section>;
}
