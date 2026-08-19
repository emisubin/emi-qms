import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ApiError, getG2Days, saveG2Attendance, saveG2Operations } from './api';
import { G2FilteredHorizontalTable, type G2HorizontalRow } from './G2DataViews';
import { formatG2Date, formatG2Modified, monthBounds, todaySeoul, type G2Day, type G2MetricValue, type G2RangeResponse } from './g2';
import { useG2Holidays, type G2HolidayMap } from './useG2Holidays';

type State = { kind: 'loading' } | { kind: 'ready'; data: G2RangeResponse } | { kind: 'forbidden'; message: string } | { kind: 'error'; message: string };
type Feedback = { tone: 'status' | 'error'; message: string } | null;

function parseQuantity(value: string, label: string) {
  if (value.trim() === '') return null;
  const quantity = Number(value);
  if (!Number.isInteger(quantity) || quantity < 0) throw new Error(`${label}은 0 이상의 정수로 입력해 주세요.`);
  return quantity;
}

function metricInput(value: { quantity: number | null } | null) { return value?.quantity === null || !value ? '' : String(value.quantity); }

function G2PageState({ state, onRetry, children }: { state: State; onRetry: () => void; children: (data: G2RangeResponse) => ReactNode }) {
  if (state.kind === 'loading') return <section className="page-surface g2-page"><p role="status">G2 일별 자료를 불러오는 중입니다.</p></section>;
  if (state.kind === 'forbidden' || state.kind === 'error') return <section className="page-surface g2-page"><div className="g2-state" role="alert"><strong>{state.message}</strong><button type="button" onClick={onRetry}>다시 시도</button></div></section>;
  return <>{children(state.data)}</>;
}

function useG2Month(developmentUserKey: string | undefined, initialDate: string) {
  const [date, setDateState] = useState(initialDate);
  const [state, setState] = useState<State>({ kind: 'loading' });
  const loadSequenceRef = useRef(0);
  const monthKey = date.slice(0, 7);
  const load = useCallback(async (targetDate: string, preserve = false) => {
    const loadSequence = ++loadSequenceRef.current;
    if (!preserve) setState({ kind: 'loading' });
    const { from, to } = monthBounds(targetDate);
    try {
      const nextData = await getG2Days(developmentUserKey, from, to);
      if (loadSequence === loadSequenceRef.current) setState({ kind: 'ready', data: nextData });
    } catch (error) {
      if (loadSequence !== loadSequenceRef.current) return;
      setState(error instanceof ApiError && error.status === 403 ? { kind: 'forbidden', message: 'G2 자료를 조회할 권한이 없습니다.' } : { kind: 'error', message: error instanceof ApiError ? error.message : 'G2 자료를 불러오지 못했습니다.' });
    }
  }, [developmentUserKey]);
  useEffect(() => { queueMicrotask(() => void load(`${monthKey}-01`)); }, [load, monthKey]);
  function setDate(next: string) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(next)) return;
    if (next.slice(0, 7) !== monthKey) {
      loadSequenceRef.current += 1;
      setState({ kind: 'loading' });
    }
    setDateState(next);
  }
  const selected = state.kind === 'ready' ? state.data.days.find(day => day.date === date) ?? null : null;
  return { date, setDate, state, selected, reload: () => load(date, true) };
}

function Field({ label, value, onChange, disabled, metric }: { label: string; value: string; onChange: (value: string) => void; disabled: boolean; metric: G2MetricValue | null }) {
  return <label className="g2-entry-field"><span>{label}</span><input type="number" min="0" step="1" inputMode="numeric" value={value} disabled={disabled} placeholder="미입력" onChange={event => onChange(event.target.value)} /><small>{formatG2Modified(metric)}</small></label>;
}

export function G2OperationsPage({ developmentUserKey, canEditProduction, canEditDelivery, mutationEnabled }: { developmentUserKey: string | undefined; canEditProduction: boolean; canEditDelivery: boolean; mutationEnabled: boolean }) {
  const month = useG2Month(developmentUserKey, todaySeoul());
  const holidays = useG2Holidays(developmentUserKey, month.state.kind === 'ready' ? month.state.data.days : []);
  const [inputs, setInputs] = useState({ morning: '', afternoon: '', delivery: '' });
  const [baseline, setBaseline] = useState(inputs);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  useLayoutEffect(() => {
    const values = { morning: metricInput(month.selected?.morningProduction ?? null), afternoon: metricInput(month.selected?.afternoonProduction ?? null), delivery: metricInput(month.selected?.delivery ?? null) };
    setInputs(values); setBaseline(values);
  }, [month.selected]);

  async function save() {
    if (!month.selected || busy) return;
    try {
      const request: Parameters<typeof saveG2Operations>[2] = {};
      if (canEditProduction && inputs.morning !== baseline.morning) request.morningProduction = { quantity: parseQuantity(inputs.morning, '오전 생산량'), expectedVersion: month.selected.morningProduction?.version ?? null };
      if (canEditProduction && inputs.afternoon !== baseline.afternoon) request.afternoonProduction = { quantity: parseQuantity(inputs.afternoon, '오후 생산량'), expectedVersion: month.selected.afternoonProduction?.version ?? null };
      if (canEditDelivery && inputs.delivery !== baseline.delivery) request.delivery = { quantity: parseQuantity(inputs.delivery, '일일 납품량'), expectedVersion: month.selected.delivery?.version ?? null };
      if (Object.keys(request).length === 0) { setFeedback({ tone: 'error', message: '변경한 값을 하나 이상 입력해 주세요.' }); return; }
      setBusy(true); setFeedback({ tone: 'status', message: '생산·납품 수량을 저장하는 중입니다.' });
      await saveG2Operations(developmentUserKey, month.date, request); await month.reload(); setFeedback({ tone: 'status', message: '생산·납품 수량을 저장했습니다.' });
    } catch (error) { setFeedback({ tone: 'error', message: error instanceof ApiError || error instanceof Error ? error.message : '생산·납품 수량을 저장하지 못했습니다.' }); }
    finally { setBusy(false); }
  }

  return <G2PageState state={month.state} onRetry={month.reload}>{data => <section className="page-surface g2-page" aria-labelledby="g2-operations-title"><header className="page-header"><div><p className="eyebrow">G2 운영관리</p><h2 id="g2-operations-title">생산/출하 관리</h2><p>오전조·오후조 생산량과 하루 전체 납품량을 각각 또는 한꺼번에 저장합니다.</p></div><label className="g2-date-picker">입력 날짜<input type="date" value={month.date} disabled={busy} required onChange={event => month.setDate(event.target.value)} /></label></header>
    {month.selected?.isForecast ? <p className="g2-forecast-note"><b>예상</b> 미래 날짜의 예상 수량을 입력하고 있습니다.</p> : null}
    {!mutationEnabled ? <p className="g2-review-safe" role="status">현재 검수 전용 읽기 모드이므로 값을 수정할 수 없습니다.</p> : null}
    <article className="g2-card g2-entry-card"><div className="g2-entry-grid"><Field label="오전 생산량" value={inputs.morning} onChange={value => setInputs(current => ({ ...current, morning: value }))} disabled={!mutationEnabled || !canEditProduction || busy} metric={month.selected?.morningProduction ?? null} /><Field label="오후 생산량" value={inputs.afternoon} onChange={value => setInputs(current => ({ ...current, afternoon: value }))} disabled={!mutationEnabled || !canEditProduction || busy} metric={month.selected?.afternoonProduction ?? null} /><Field label="일일 납품량" value={inputs.delivery} onChange={value => setInputs(current => ({ ...current, delivery: value }))} disabled={!mutationEnabled || !canEditDelivery || busy} metric={month.selected?.delivery ?? null} /></div>{feedback ? <p className="g2-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'} aria-live="polite">{feedback.message}</p> : null}<button type="button" className="primary-button" disabled={!mutationEnabled || busy || (!canEditProduction && !canEditDelivery)} onClick={() => void save()}>변경한 값 저장</button></article>
    <MonthOperationsTable days={data.days} holidays={holidays} />
  </section>}</G2PageState>;
}

function MonthOperationsTable({ days, holidays }: { days: G2Day[]; holidays: G2HolidayMap }) {
  return <G2FilteredHorizontalTable title="월간 입력 현황" filterLabel="입력 현황 표시 기간" caption="생산·납품·재고 월간 입력 현황" days={days} rows={[
    { label: '오전 생산', value: day => day.morningProduction?.quantity ?? '—' },
    { label: '오후 생산', value: day => day.afternoonProduction?.quantity ?? '—' },
    { label: '생산 합계', value: day => <strong>{day.productionTotal ?? '—'}</strong> },
    { label: '납품', value: day => day.delivery?.quantity ?? '—' },
    { label: '재고', value: day => day.inventory ?? '기준 없음', cellClassName: day => day.inventory !== null && day.inventory < 0 ? 'g2-negative' : undefined }
  ]} holidays={holidays} />;
}

export function G2AttendancePage({ developmentUserKey, canEdit, mutationEnabled }: { developmentUserKey: string | undefined; canEdit: boolean; mutationEnabled: boolean }) {
  const month = useG2Month(developmentUserKey, todaySeoul());
  const holidays = useG2Holidays(developmentUserKey, month.state.kind === 'ready' ? month.state.data.days : []);
  const [inputs, setInputs] = useState({ morningEmi: '', morningContractor: '', afternoonEmi: '', afternoonContractor: '' });
  const [baseline, setBaseline] = useState(inputs);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [expanded, setExpanded] = useState({ morning: false, afternoon: false });
  useLayoutEffect(() => { const values = { morningEmi: metricInput(month.selected?.morningEmiAttendance ?? null), morningContractor: metricInput(month.selected?.morningContractorAttendance ?? null), afternoonEmi: metricInput(month.selected?.afternoonEmiAttendance ?? null), afternoonContractor: metricInput(month.selected?.afternoonContractorAttendance ?? null) }; setInputs(values); setBaseline(values); }, [month.selected]);
  const totals = useMemo(() => {
    const value = (input: string) => /^\d+$/.test(input) ? Number(input) : 0;
    return { morning: value(inputs.morningEmi) + value(inputs.morningContractor), afternoon: value(inputs.afternoonEmi) + value(inputs.afternoonContractor) };
  }, [inputs]);

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

  const attendanceRows: G2HorizontalRow[] = [
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
    { key: 'attendance-total', label: '하루 총원', rowClassName: 'g2-grand-total-row', value: day => <strong>{day.attendanceTotal ?? '—'}</strong> }
  ];

  async function save() {
    if (!month.selected || busy) return;
    try {
      const request: Parameters<typeof saveG2Attendance>[2] = {};
      if (inputs.morningEmi !== baseline.morningEmi) request.morningEmiAttendance = { quantity: parseQuantity(inputs.morningEmi, '오전 EMI 출근 인원'), expectedVersion: month.selected.morningEmiAttendance?.version ?? null };
      if (inputs.morningContractor !== baseline.morningContractor) request.morningContractorAttendance = { quantity: parseQuantity(inputs.morningContractor, '오전 도급 출근 인원'), expectedVersion: month.selected.morningContractorAttendance?.version ?? null };
      if (inputs.afternoonEmi !== baseline.afternoonEmi) request.afternoonEmiAttendance = { quantity: parseQuantity(inputs.afternoonEmi, '오후 EMI 출근 인원'), expectedVersion: month.selected.afternoonEmiAttendance?.version ?? null };
      if (inputs.afternoonContractor !== baseline.afternoonContractor) request.afternoonContractorAttendance = { quantity: parseQuantity(inputs.afternoonContractor, '오후 도급 출근 인원'), expectedVersion: month.selected.afternoonContractorAttendance?.version ?? null };
      if (Object.keys(request).length === 0) { setFeedback({ tone: 'error', message: '변경한 값을 하나 이상 입력해 주세요.' }); return; }
      setBusy(true); setFeedback({ tone: 'status', message: '출근 인원을 저장하는 중입니다.' }); await saveG2Attendance(developmentUserKey, month.date, request); await month.reload(); setFeedback({ tone: 'status', message: '출근 인원을 저장했습니다.' });
    } catch (error) { setFeedback({ tone: 'error', message: error instanceof ApiError || error instanceof Error ? error.message : '출근 인원을 저장하지 못했습니다.' }); }
    finally { setBusy(false); }
  }

  return <G2PageState state={month.state} onRetry={month.reload}>{data => <section className="page-surface g2-page" aria-labelledby="g2-attendance-title"><header className="page-header"><div><p className="eyebrow">G2 운영관리</p><h2 id="g2-attendance-title">제조 인원 출근 관리</h2><p>오전·오후의 EMI 직원과 도급 직원을 숫자로 관리합니다.</p></div><label className="g2-date-picker">입력 날짜<input type="date" value={month.date} disabled={busy} required onChange={event => month.setDate(event.target.value)} /></label></header>
    {month.selected?.isForecast ? <p className="g2-forecast-note"><b>예상</b> 미래 날짜의 예상 출근 인원을 입력하고 있습니다.</p> : null}{!mutationEnabled ? <p className="g2-review-safe" role="status">현재 검수 전용 읽기 모드이므로 값을 수정할 수 없습니다.</p> : null}
    <article className="g2-card g2-entry-card"><div className="g2-entry-grid g2-entry-grid-four"><Field label="오전 EMI" value={inputs.morningEmi} onChange={value => setInputs(current => ({ ...current, morningEmi: value }))} disabled={!mutationEnabled || !canEdit || busy} metric={month.selected?.morningEmiAttendance ?? null} /><Field label="오전 도급" value={inputs.morningContractor} onChange={value => setInputs(current => ({ ...current, morningContractor: value }))} disabled={!mutationEnabled || !canEdit || busy} metric={month.selected?.morningContractorAttendance ?? null} /><Field label="오후 EMI" value={inputs.afternoonEmi} onChange={value => setInputs(current => ({ ...current, afternoonEmi: value }))} disabled={!mutationEnabled || !canEdit || busy} metric={month.selected?.afternoonEmiAttendance ?? null} /><Field label="오후 도급" value={inputs.afternoonContractor} onChange={value => setInputs(current => ({ ...current, afternoonContractor: value }))} disabled={!mutationEnabled || !canEdit || busy} metric={month.selected?.afternoonContractorAttendance ?? null} /></div><div className="g2-live-totals" aria-live="polite"><span>오전 합계 <b>{totals.morning}</b></span><span>오후 합계 <b>{totals.afternoon}</b></span><span>하루 총원 <b>{totals.morning + totals.afternoon}</b></span></div>{feedback ? <p className="g2-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'}>{feedback.message}</p> : null}<button type="button" className="primary-button" disabled={!mutationEnabled || !canEdit || busy} onClick={() => void save()}>변경한 값 저장</button></article>
    <G2FilteredHorizontalTable title="월간 출근 현황" filterLabel="출근 현황 표시 기간" caption="제조 인원 월간 출근 현황" days={data.days} rows={attendanceRows} holidays={holidays} />
  </section>}</G2PageState>;
}
