import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, deleteG2InventoryCount, getG2Home, saveG2InventoryCount, saveG2Target } from './api';
import { G2ProductionDeliveryInventoryChart, G2ShiftProductionChart } from './G2Charts';
import { G2DateRangeFilter, G2HorizontalTable, type G2HorizontalRow } from './G2DataViews';
import { formatG2Date, formatG2Modified, todaySeoul, type G2Day, type G2HomeResponse, type G2Target } from './g2';
import { applyG2HomePreview, type G2PreviewField, type G2PreviewInputs } from './G2HomePreview';
import { useG2DateRange } from './useG2DateRange';
import { useG2Holidays, type G2HolidayMap } from './useG2Holidays';

type State = { kind: 'loading' } | { kind: 'ready'; data: G2HomeResponse } | { kind: 'forbidden'; message: string } | { kind: 'error'; message: string };

type G2KpiItem = {
  label: string;
  value: string;
  tone: 'blue' | 'orange' | 'red' | 'violet' | 'morning' | 'afternoon';
  help?: string;
};

function averageQuantity(values: Array<number | null | undefined>) {
  const quantities = values.filter((value): value is number => value !== null && value !== undefined);
  if (quantities.length === 0) return null;
  return quantities.reduce((sum, quantity) => sum + quantity, 0) / quantities.length;
}

function formatQuantity(value: number | null) {
  return value === null ? '—' : `${new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 1 }).format(value)}대`;
}

function G2ChartKpiPanel({ label, items }: { label: string; items: G2KpiItem[] }) {
  return <aside className={`g2-chart-kpis g2-chart-kpis-${items.length}`} aria-label={label}>{items.map((item, index) => {
    const tooltipId = `g2-kpi-help-${label === '생산·납품·재고 핵심 지표' ? 'flow' : 'shift'}-${index}`;
    return <div className="g2-chart-kpi" data-tone={item.tone} key={item.label}>
      <span className="g2-chart-kpi-label">{item.label}{item.help ? <span className="g2-kpi-info" tabIndex={0} aria-label={`${item.label} 계산 안내`} aria-describedby={tooltipId}>i<span className="g2-kpi-tooltip" id={tooltipId} role="tooltip">{item.help}</span></span> : null}</span>
      <strong>{item.value}</strong>
    </div>;
  })}</aside>;
}

function G2AttendanceSummaryTable({ days, holidays }: { days: G2Day[]; holidays: G2HolidayMap }) {
  const [expanded, setExpanded] = useState({ morning: false, afternoon: false });
  function toggle(shift: 'morning' | 'afternoon') {
    setExpanded(current => ({ ...current, [shift]: !current[shift] }));
  }
  function totalButton(day: G2Day, shift: 'morning' | 'afternoon') {
    const isMorning = shift === 'morning';
    const label = isMorning ? '오전 합계' : '오후 합계';
    const quantity = isMorning ? day.morningAttendanceTotal : day.afternoonAttendanceTotal;
    const quantityLabel = quantity === null ? '미입력' : `${quantity}명`;
    const action = expanded[shift] ? '접기' : '보기';
    return <button className="g2-table-total-button" type="button" aria-expanded={expanded[shift]} aria-label={`${formatG2Date(day.date)} ${label} ${quantityLabel} 세부 인원 ${action}`} onClick={() => toggle(shift)}><strong>{quantity ?? '—'}</strong></button>;
  }
  const rows: G2HorizontalRow[] = [
    {
      key: 'morning-total',
      label: <button className="g2-table-disclosure" type="button" aria-expanded={expanded.morning} aria-label={`오전 합계 세부 인원 ${expanded.morning ? '접기' : '보기'}`} onClick={() => toggle('morning')}>오전 합계<span aria-hidden="true">{expanded.morning ? '▾' : '▸'}</span></button>,
      rowClassName: 'g2-summary-row',
      value: day => totalButton(day, 'morning')
    },
    ...(expanded.morning ? [
      { key: 'morning-emi', label: '오전 EMI', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.morningEmiAttendance?.quantity ?? '—' },
      { key: 'morning-contractor', label: '오전 도급', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.morningContractorAttendance?.quantity ?? '—' }
    ] : []),
    {
      key: 'afternoon-total',
      label: <button className="g2-table-disclosure" type="button" aria-expanded={expanded.afternoon} aria-label={`오후 합계 세부 인원 ${expanded.afternoon ? '접기' : '보기'}`} onClick={() => toggle('afternoon')}>오후 합계<span aria-hidden="true">{expanded.afternoon ? '▾' : '▸'}</span></button>,
      rowClassName: 'g2-summary-row',
      value: day => totalButton(day, 'afternoon')
    },
    ...(expanded.afternoon ? [
      { key: 'afternoon-emi', label: '오후 EMI', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.afternoonEmiAttendance?.quantity ?? '—' },
      { key: 'afternoon-contractor', label: '오후 도급', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.afternoonContractorAttendance?.quantity ?? '—' }
    ] : []),
    {
      key: 'attendance-total',
      label: '오전·오후 전체 합계',
      rowClassName: 'g2-grand-total-row',
      value: day => <strong>{day.attendanceTotal ?? '—'}</strong>
    }
  ];

  return <><p className="g2-table-help">오전·오후 합계를 눌러 EMI 직원과 도급 직원 인원을 확인하세요. 전체 합계는 두 조를 단순 합산합니다.</p><G2HorizontalTable days={days} caption="제조 인원 출근 현황" rows={rows} holidays={holidays} /></>;
}

export function G2HomePage({
  developmentUserKey,
  canManageInventory,
  canManageTargets,
  mutationEnabled
}: {
  developmentUserKey: string | undefined;
  canManageInventory: boolean;
  canManageTargets: boolean;
  mutationEnabled: boolean;
}) {
  const initialDate = todaySeoul();
  const [year, setYear] = useState(() => Number(initialDate.slice(0, 4)));
  const [month, setMonth] = useState(() => Number(initialDate.slice(5, 7)));
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [countEditor, setCountEditor] = useState<{ date: string; value: string } | null>(null);
  const [targetType, setTargetType] = useState<'DailyProduction' | 'Delivery' | 'Inventory'>('DailyProduction');
  const [targetDate, setTargetDate] = useState(initialDate);
  const [targetValue, setTargetValue] = useState('');
  const [previewInputs, setPreviewInputs] = useState<G2PreviewInputs>({});
  const dialogInputRef = useRef<HTMLInputElement>(null);
  const loadSequenceRef = useRef(0);
  const data = state.kind === 'ready' ? state.data : null;

  const load = useCallback(async (nextYear = year, nextMonth = month, preserve = false) => {
    const loadSequence = ++loadSequenceRef.current;
    if (!preserve) setState({ kind: 'loading' });
    try {
      const nextData = await getG2Home(developmentUserKey, nextYear, nextMonth);
      if (loadSequence === loadSequenceRef.current) { setPreviewInputs({}); setState({ kind: 'ready', data: nextData }); }
    } catch (error) {
      if (loadSequence !== loadSequenceRef.current) return;
      setState(error instanceof ApiError && error.status === 403 ? { kind: 'forbidden', message: 'G2 현황을 조회할 권한이 없습니다.' } : { kind: 'error', message: error instanceof ApiError ? error.message : 'G2 현황을 불러오지 못했습니다.' });
    }
  }, [developmentUserKey, month, year]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);
  useEffect(() => { if (countEditor) dialogInputRef.current?.focus(); }, [countEditor]);

  function moveMonth(delta: number) {
    const date = new Date(Date.UTC(year, month - 1 + delta, 1));
    const nextYear = date.getUTCFullYear(); const nextMonth = date.getUTCMonth() + 1;
    loadSequenceRef.current += 1;
    setState({ kind: 'loading' }); setYear(nextYear); setMonth(nextMonth); setTargetDate(`${nextYear}-${String(nextMonth).padStart(2, '0')}-01`); setTargetValue(''); setFeedback(null);
  }

  const dateRange = useG2DateRange(data?.days ?? [], data ? `${data.year}-${data.month}` : 'loading');
  const holidays = useG2Holidays(developmentUserKey, data?.days ?? []);
  const previewDays = useMemo(() => applyG2HomePreview(data?.days ?? [], previewInputs), [data?.days, previewInputs]);
  const visibleDays = useMemo(
    () => previewDays.filter(day => day.date >= dateRange.from && day.date <= dateRange.to),
    [dateRange.from, dateRange.to, previewDays]
  );
  function openCount(date: string) {
    const count = data?.days.find(day => day.date === date)?.physicalCount;
    setCountEditor({ date, value: count ? String(count.quantity) : '' }); setFeedback(null);
  }

  async function saveCount() {
    if (!data || !countEditor || busy) return;
    if (!countEditor.date) { setFeedback('실사 날짜를 선택해 주세요.'); return; }
    if (countEditor.value.trim() === '') { setFeedback('실사 수량을 입력해 주세요.'); return; }
    const quantity = Number(countEditor.value);
    if (!Number.isInteger(quantity) || quantity < 0) { setFeedback('실사 수량은 0 이상의 정수로 입력해 주세요.'); return; }
    const current = data.days.find(day => day.date === countEditor.date)?.physicalCount ?? null;
    setBusy(true); setFeedback('재고 실사를 저장하는 중입니다.');
    try { await saveG2InventoryCount(developmentUserKey, countEditor.date, quantity, current?.version ?? null); await load(year, month, true); setFeedback('재고 실사를 저장했습니다.'); setCountEditor(null); }
    catch (error) { setFeedback(error instanceof ApiError ? error.message : '재고 실사를 저장하지 못했습니다.'); }
    finally { setBusy(false); }
  }

  async function deleteCount() {
    if (!data || !countEditor || busy) return;
    const current = data.days.find(day => day.date === countEditor.date)?.physicalCount;
    if (!current) return;
    setBusy(true); setFeedback('재고 실사를 삭제하는 중입니다.');
    try { await deleteG2InventoryCount(developmentUserKey, countEditor.date, current.version); await load(year, month, true); setFeedback('재고 실사를 삭제했습니다.'); setCountEditor(null); }
    catch (error) { setFeedback(error instanceof ApiError ? error.message : '재고 실사를 삭제하지 못했습니다.'); }
    finally { setBusy(false); }
  }

  function matchingTarget(): G2Target | null {
    const day = data?.days.find(item => item.date === targetDate);
    const target = targetType === 'DailyProduction'
      ? day?.dailyProductionTarget
      : targetType === 'Delivery'
        ? day?.deliveryTarget
        : day?.inventoryTarget;
    return target?.effectiveDate === targetDate ? target : null;
  }

  function setPreviewValue(date: string, field: G2PreviewField, value: string) {
    setPreviewInputs(current => ({ ...current, [date]: { ...current[date], [field]: value } }));
  }

  function previewInput(day: G2Day, field: G2PreviewField, label: string) {
    const rawValue = previewInputs[day.date]?.[field];
    const quantity = day[field]?.quantity;
    return <input
      className="g2-preview-input"
      type="number"
      min="0"
      step="1"
      inputMode="numeric"
      aria-label={`${formatG2Date(day.date)} ${label} 임시 예상값`}
      value={rawValue ?? (quantity === null || quantity === undefined ? '' : String(quantity))}
      placeholder="—"
      onChange={event => setPreviewValue(day.date, field, event.target.value)}
    />;
  }

  async function saveTarget() {
    if (!data || busy) return;
    if (!targetDate) { setFeedback('적용 시작일을 선택해 주세요.'); return; }
    if (targetValue.trim() === '') { setFeedback('목표 수량을 입력해 주세요.'); return; }
    const quantity = Number(targetValue);
    if (!Number.isInteger(quantity) || quantity < 0) { setFeedback('목표 수량은 0 이상의 정수로 입력해 주세요.'); return; }
    setBusy(true); setFeedback('목표를 저장하는 중입니다.');
    try { await saveG2Target(developmentUserKey, targetType, targetDate, quantity, matchingTarget()?.version ?? null); await load(year, month, true); setFeedback('적용 시작일 기준 목표를 저장했습니다.'); }
    catch (error) { setFeedback(error instanceof ApiError ? error.message : '목표를 저장하지 못했습니다.'); }
    finally { setBusy(false); }
  }

  if (state.kind === 'loading') return <section className="page-surface g2-page"><p role="status">G2 월간 현황을 불러오는 중입니다.</p></section>;
  if (state.kind === 'forbidden' || state.kind === 'error') return <section className="page-surface g2-page"><div className="g2-state" role="alert"><strong>{state.message}</strong><button type="button" onClick={() => void load()}>다시 시도</button></div></section>;
  if (!data) return null;
  const hasNegativeInventory = visibleDays.some(day => day.inventory !== null && day.inventory < 0);
  const productionAverage = averageQuantity(visibleDays.map(day => day.productionTotal));
  const deliveryAverage = averageQuantity(visibleDays.map(day => day.delivery?.quantity));
  const inventoryAverage = averageQuantity(visibleDays.map(day => day.inventory));
  const morningAverage = averageQuantity(visibleDays.map(day => day.morningProduction?.quantity));
  const afternoonAverage = averageQuantity(visibleDays.map(day => day.afternoonProduction?.quantity));
  const todayInventoryDay = previewDays.find(day => day.date === data.today);
  const inventoryShortage = todayInventoryDay?.inventory !== null && todayInventoryDay?.inventory !== undefined && todayInventoryDay.inventoryTarget !== null
    ? Math.max(todayInventoryDay.inventoryTarget.quantity - todayInventoryDay.inventory, 0)
    : null;
  const shortageHelp = todayInventoryDay?.inventory !== null && todayInventoryDay?.inventory !== undefined && todayInventoryDay.inventoryTarget !== null
    ? `오늘(${formatG2Date(data.today)}) 기준 재고목표 - 재고입니다. 부족하지 않으면 0대로 표시합니다.`
    : `오늘(${formatG2Date(data.today)})의 재고와 재고목표가 모두 있어야 부족분을 계산합니다.`;
  const countDates = data.days.map(day => day.date).filter(date => date <= data.today);
  const countDateMinimum = countDates[0];
  const countDateMaximum = countDates[countDates.length - 1];
  const targetDateMinimum = data.days[0]?.date;
  const targetDateMaximum = data.days[data.days.length - 1]?.date;

  return <section className="page-surface g2-page" aria-labelledby="g2-home-title">
    <header className="page-header"><div><p className="eyebrow">G2 운영관리</p><h2 id="g2-home-title">G2 홈</h2><p>이번 달 생산·납품·재고와 제조 출근 현황을 한눈에 확인합니다.</p></div><div className="button-row g2-month-controls"><button type="button" disabled={busy} onClick={() => moveMonth(-1)} aria-label="이전 달">이전</button><strong>{year}년 {month}월</strong><button type="button" disabled={busy} onClick={() => moveMonth(1)} aria-label="다음 달">다음</button></div></header>
    {!mutationEnabled && (canManageInventory || canManageTargets) ? <p className="g2-review-safe" role="status">현재 검수 전용 읽기 모드이므로 실사와 목표를 수정할 수 없습니다.</p> : null}
    {feedback ? <p className="g2-feedback" role={feedback.includes('못') || feedback.includes('확인') ? 'alert' : 'status'} aria-live="polite">{feedback}</p> : null}
    <G2DateRangeFilter label="홈 표시 기간" from={dateRange.from} to={dateRange.to} minimum={dateRange.firstDate} maximum={dateRange.lastDate} filteredCount={visibleDays.length} totalCount={data.days.length} onFromChange={dateRange.setFrom} onToChange={dateRange.setTo} onReset={dateRange.reset} />

    <article className="g2-card g2-chart-card"><header><div><p className="eyebrow">일별 흐름</p><h3>생산 · 납품 · 재고</h3></div></header>
      {!data.hasInventoryBaseline ? <p className="g2-empty-note">재고 실사를 입력하면 해당 날짜부터 자동 재고 선이 시작됩니다.</p> : null}
      {hasNegativeInventory ? <p className="g2-warning" role="alert">계산 재고가 음수인 날짜가 있습니다. 입력값과 실사를 확인해 주세요.</p> : null}
      <div className="g2-chart-dashboard">
        <G2ProductionDeliveryInventoryChart days={visibleDays} holidays={holidays} />
        <G2ChartKpiPanel label="생산·납품·재고 핵심 지표" items={[
          { label: '일일 생산 평균', value: formatQuantity(productionAverage), tone: 'blue' },
          { label: '일일 납품 평균', value: formatQuantity(deliveryAverage), tone: 'orange' },
          { label: '일일 재고 평균', value: formatQuantity(inventoryAverage), tone: 'red' },
          { label: '재고 부족분', value: formatQuantity(inventoryShortage), tone: 'violet', help: shortageHelp }
        ]} />
      </div>
      {canManageInventory ? <div className="g2-marker-actions" aria-label="재고 실사 날짜별 수정">{visibleDays.filter(day => day.physicalCount).map(day => <button key={day.date} type="button" disabled={!mutationEnabled} onClick={() => openCount(day.date)}><b>{Number(day.date.slice(-2))}일 실사</b><span>{day.physicalCount?.quantity}대 · 수정</span></button>)}<button type="button" disabled={!mutationEnabled || !countDateMaximum} onClick={() => countDateMaximum && openCount(countDateMaximum)}>실사 입력</button></div> : null}
    </article>

    <article className="g2-card g2-preview-card"><header><div><p className="eyebrow">선택 기간</p><h3>생산 현황</h3></div>{Object.keys(previewInputs).length > 0 ? <button type="button" onClick={() => setPreviewInputs({})}>임시값 초기화</button> : null}</header><p className="g2-preview-note">표의 생산·납품·불량 숫자는 조회용 임시 예상값입니다. 저장되지 않으며 새로 조회하면 초기화됩니다.</p><G2HorizontalTable days={visibleDays} caption="생산 현황" rows={[
      { label: '오전 생산', value: day => previewInput(day, 'morningProduction', '오전 생산') },
      { label: '오후 생산', value: day => previewInput(day, 'afternoonProduction', '오후 생산') },
      { label: '생산 합계', value: day => <strong>{day.productionTotal ?? '—'}</strong> },
      { label: '납품 목표', value: day => day.deliveryTarget?.quantity ?? '—' },
      { label: '납품', value: day => previewInput(day, 'delivery', '납품') },
      { label: '불량', value: day => previewInput(day, 'defect', '불량') },
      { label: '재고', rowClassName: 'g2-inventory-row', value: day => day.inventory ?? '기준 없음', cellClassName: day => day.inventory !== null && day.inventory < 0 ? 'g2-negative' : undefined }
    ]} holidays={holidays} /></article>

    {canManageTargets ? <article className="g2-card g2-target-card"><header><div><p className="eyebrow">목표 관리</p><h3>적용 시작일별 목표</h3></div></header><div className="g2-inline-form"><label>목표 종류<select value={targetType} disabled={!mutationEnabled || busy} onChange={event => { setTargetType(event.target.value as typeof targetType); setTargetValue(''); }}><option value="DailyProduction">일 생산목표</option><option value="Delivery">납품 목표</option><option value="Inventory">재고목표</option></select></label><label className="g2-target-date-field">적용 시작일<input type="date" min={targetDateMinimum} max={targetDateMaximum} value={targetDate} disabled={!mutationEnabled || busy} required onChange={event => { setTargetDate(event.target.value); setTargetValue(''); }} /></label><label>목표 수량<input type="number" min="0" step="1" value={targetValue} disabled={!mutationEnabled || busy} required onChange={event => setTargetValue(event.target.value)} /></label><button className="primary-button" type="button" disabled={!mutationEnabled || busy || !targetDate || targetValue.trim() === ''} onClick={() => void saveTarget()}>목표 저장</button></div><small>같은 적용일을 다시 저장하면 기존 목표를 수정합니다. 이전·다음 달로 이동해 과거와 미래 목표를 관리할 수 있습니다.</small></article> : null}

    <article className="g2-card g2-chart-card"><header><div><p className="eyebrow">조별 생산</p><h3>오전조 · 오후조 생산량</h3></div></header><div className="g2-chart-dashboard">
      <G2ShiftProductionChart days={visibleDays} holidays={holidays} />
      <G2ChartKpiPanel label="조별 생산 핵심 지표" items={[
        { label: '오전조 일일 생산 평균', value: formatQuantity(morningAverage), tone: 'morning' },
        { label: '오후조 일일 생산 평균', value: formatQuantity(afternoonAverage), tone: 'afternoon' }
      ]} />
    </div></article>

    <article className="g2-card"><header><div><p className="eyebrow">선택 기간</p><h3>제조 인원 출근 현황</h3></div></header><G2AttendanceSummaryTable days={visibleDays} holidays={holidays} /></article>

    {countEditor ? <div className="g2-dialog-backdrop" onMouseDown={event => { if (event.target === event.currentTarget && !busy) setCountEditor(null); }}><section className="g2-dialog" role="dialog" aria-modal="true" aria-labelledby="g2-count-title" onKeyDown={event => { if (event.key === 'Escape' && !busy) setCountEditor(null); }}><header><div><p className="eyebrow">자동 재고 기준점</p><h3 id="g2-count-title">재고 실사 입력</h3></div><button type="button" disabled={busy} onClick={() => setCountEditor(null)}>닫기</button></header><label>실사 날짜<input type="date" min={countDateMinimum} max={countDateMaximum} value={countEditor.date} disabled={busy} required onChange={event => openCount(event.target.value)} /></label><label>실사 수량<input ref={dialogInputRef} type="number" min="0" step="1" value={countEditor.value} disabled={busy} required onChange={event => setCountEditor(current => current ? { ...current, value: event.target.value } : null)} /></label>{data.days.find(day => day.date === countEditor.date)?.physicalCount ? <small>마지막 수정: {formatG2Modified(data.days.find(day => day.date === countEditor.date)?.physicalCount ?? null)}</small> : null}<div className="button-row"><button className="primary-button" type="button" disabled={busy || !countEditor.date || countEditor.value.trim() === ''} onClick={() => void saveCount()}>저장</button>{data.days.find(day => day.date === countEditor.date)?.physicalCount ? <button className="danger-button" type="button" disabled={busy} onClick={() => void deleteCount()}>실사 삭제</button> : null}</div></section></div> : null}
  </section>;
}
