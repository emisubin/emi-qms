import { useG2Mobile } from './useG2Mobile';
import { useEffect, useId, useRef, useState } from 'react';
import { ApiError, getG2Days, saveG2Attendance } from './api';
import { DataEntryDialog } from './DataEntryDialog';
import { G2PageFrame } from './G2PageFrame';
import { G2Calendar, G2MonthHeading, G2MonthPicker, G2InputTabs } from './G2SharedControls';
import { monthBounds, todaySeoul, type G2Day, type G2RangeResponse, type SaveG2AttendanceRequest } from './g2';
import { useG2Holidays } from './useG2Holidays';
import './g2-approved/pc.css';
import './g2-approved/mobile.css';
import './g2-operations.css';
import './g2-attendance.css';

const fields = ['morningEmiAttendance', 'morningContractorAttendance', 'afternoonEmiAttendance', 'afternoonContractorAttendance'] as const;
type Values = Record<typeof fields[number], string>;
const labels = ['오전 EMI', '오전 도급', '오후 EMI', '오후 도급'];
const shifts = [{ value: 'morning', label: '오전' }, { value: 'afternoon', label: '오후' }];
const fmt = (n: number | null | undefined) => n == null ? '' : n.toLocaleString('ko-KR');
const weekday = (date: string) => new Date(`${date}T00:00:00Z`).getUTCDay();
const sum = (values: (number | null | undefined)[]) => values.every(n => n == null) ? null : values.reduce<number>((a, n) => a + (n ?? 0), 0);
const quantities = (day?: G2Day) => fields.map(key => day?.[key]?.quantity);
const valuesFor = (day: G2Day): Values => Object.fromEntries(fields.map(key => [key, day[key]?.quantity?.toString() ?? ''])) as Values;
const columns = <colgroup><col style={{ width: 48 }}/><col style={{ width: 172 }}/><col span={6}/><col style={{ width: 130 }}/></colgroup>;

function AttendanceEditor({ initialDate, today, user, enabled, onClose, onSaved }: {
  initialDate: string; today: string; user: string | undefined; enabled: boolean; onClose: () => void; onSaved: () => void;
}) {
  const [date, setDate] = useState(initialDate);
  const [source, setSource] = useState<G2Day | null>(null);
  const [values, setValues] = useState<Values | null>(null);
  const [shift, setShift] = useState('morning');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  const submitting = useRef(false);
  const id = useId();
  useEffect(() => {
    let active = true;
    if (!date || date > today) return;
    void getG2Days(user, date, date).then(result => {
      if (!active) return;
      const day = result.days.find(d => d.date === date);
      if (!day) throw new Error('선택한 날짜를 찾을 수 없습니다.');
      setSource(day); setValues(valuesFor(day));
    }).catch(e => { if (active) setError(e instanceof Error ? e.message : '기존 값을 불러오지 못했습니다.'); });
    return () => { active = false; };
  }, [date, today, user, retry]);
  function changeDate(next: string) { setDate(next); setSource(null); setValues(null); setError(next > today ? '오늘 또는 과거 날짜만 입력할 수 있습니다.' : ''); }
  async function save() {
    if (!source || !values || !enabled || submitting.current || source.date !== date || date > today) return;
    try {
      const request: SaveG2AttendanceRequest = {};
      fields.forEach((field, i) => {
        if (values[field] === valuesFor(source)[field]) return;
        const n = values[field].trim() === '' ? null : Number(values[field]);
        if (n !== null && (!Number.isSafeInteger(n) || n < 0 || n > 2147483647)) throw new Error(`${labels[i]} 인원은 0 이상의 정수로 입력해 주세요.`);
        request[field] = { quantity: n, expectedVersion: source[field]?.version ?? null };
      });
      if (!Object.keys(request).length) throw new Error('변경한 값을 하나 이상 입력해 주세요.');
      submitting.current = true; setBusy(true); setError('');
      await saveG2Attendance(user, date, request);
      onSaved(); onClose();
    } catch (e) { setError(e instanceof Error ? e.message : '출근 인원을 저장하지 못했습니다.'); }
    finally { submitting.current = false; setBusy(false); }
  }
  const nums = fields.map(key => values?.[key]?.trim() ? Number(values[key]) : null);
  const totals = [sum(nums.slice(0, 2)), sum(nums.slice(2)), sum(nums)];
  return <DataEntryDialog title="출근 인원 입력" onClose={onClose} busy={busy} footer={<><button type="button" disabled={busy} onClick={onClose}>취소</button><button className="primary" form={id} type="submit" disabled={!enabled || busy || !source || date > today}>{busy ? '저장 중…' : '저장'}</button></>}>
    <form id={id} onSubmit={e => { e.preventDefault(); void save(); }}>
      <div className="form-row"><label htmlFor={`${id}-date`}>입력 날짜</label><input id={`${id}-date`} className="attendance-entry-date" type="date" required max={today} value={date} disabled={busy} onChange={e => changeDate(e.target.value)}/></div>
      <G2InputTabs id={id} label="출근 시간대" tabs={shifts} value={shift} onChange={setShift}/>
      <section role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-${shift}`}><div className="form-row"><span className="fieldlabel">{shift === 'morning' ? '오전' : '오후'} 출근</span><div className="pair">{fields.slice(shift === 'morning' ? 0 : 2, shift === 'morning' ? 2 : 4).map((field, i) => <label key={field}>{i === 0 ? 'EMI' : '도급'}<input type="number" min="0" max="2147483647" step="1" inputMode="numeric" placeholder="미입력" value={values?.[field] ?? ''} disabled={!source || !enabled || busy} onChange={e => setValues(v => v ? { ...v, [field]: e.target.value } : v)}/></label>)}</div></div></section>
      {!source && !error && <p role="status">기존 값을 불러오는 중입니다.</p>}
      <div className="preview attendance-totals" aria-live="polite">{['오전 합계', '오후 합계', '하루 총원'].map((label, i) => <span key={label}>{label}<b>{totals[i] == null ? '—' : `${fmt(totals[i])}명`}</b></span>)}</div>
      <p className="hint">하루 총원은 오전 합계 + 오후 합계입니다.<br/>기존 값을 비우고 저장하면 미입력으로 변경됩니다.</p>
      {error && <div className="g2-form-error" role="alert">{error}{!source && date && date <= today && <button type="button" onClick={() => { setError(''); setRetry(n => n + 1); }}>다시 불러오기</button>}</div>}
    </form>
  </DataEntryDialog>;
}

export function G2AttendancePage(props: { developmentUserKey: string | undefined; canEdit: boolean; mutationEnabled: boolean }) {
  return <AttendanceWorkspace key={props.developmentUserKey ?? 'signed-in'} {...props}/>;
}
function AttendanceWorkspace({ developmentUserKey: user, canEdit, mutationEnabled }: { developmentUserKey: string | undefined; canEdit: boolean; mutationEnabled: boolean }) {
  const [month, setMonth] = useState(todaySeoul().slice(0, 7));
  const [data, setData] = useState<G2RangeResponse | null>(null);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [feedback, setFeedback] = useState('');
  const [modal, setModal] = useState<{ kind: 'month' } | { kind: 'entry' | 'detail'; date: string } | null>(null);
  const [selected, setSelected] = useState(todaySeoul());
  const mobile = useG2Mobile();
  useEffect(() => {
    let active = true;
    const { from, to } = monthBounds(`${month}-01`);
    void getG2Days(user, from, to).then(next => { if (active) { setData(next); setError(''); } }).catch(e => { if (active) setError(e instanceof ApiError && e.status === 403 ? 'G2 자료를 조회할 권한이 없습니다.' : 'G2 자료를 불러오지 못했습니다. 다시 시도해 주세요.'); });
    return () => { active = false; };
  }, [month, user, revision]);
  const days = data?.days ?? [];
  const holidays = useG2Holidays(user, days);
  const today = data?.today ?? todaySeoul();
  const enabled = canEdit && mutationEnabled;
  const openEntry = (date: string) => { if (enabled && date <= today) setModal({ kind: 'entry', date }); };
  const short = (date: string) => date.slice(5).replace('-', '.');
  const color = (date: string) => holidays.has(date) || weekday(date) === 0 ? 'red' : weekday(date) === 6 ? 'blue' : '';
  const latest = [...days].filter(d => d.date <= today && quantities(d).some(n => n != null)).sort((a, b) => b.date.localeCompare(a.date))[0];
  const latestValues = quantities(latest);
  const selectedDay = modal && 'date' in modal ? days.find(d => d.date === modal.date) : undefined;
  const detailValues = selectedDay && selectedDay.date <= today ? quantities(selectedDay) : [null, null, null, null];
  const monthHeading = <G2MonthHeading value={month} onOpen={() => setModal({ kind: 'month' })}/>;
  const summary = (offset: number) => <><div className="query-title">{offset === 0 ? '오전' : '오후'} 출근 <span>{latest ? `${short(latest.date)} 기준` : '입력 내역 없음'}</span></div><div className="summary-values">{['EMI', '도급', '합계'].map((label, i) => <span key={label}>{label}<b>{fmt(i === 2 ? sum(latestValues.slice(offset, offset + 2)) : latestValues[offset + i])}</b></span>)}</div></>;
  return <G2PageFrame id="g2-approved-scope" title="제조 인원 출근 관리" description="일자별 오전·오후 출근 인원과 하루 총원을 확인합니다." fitMobile actions={canEdit && <button className="primary" disabled={!enabled || !data || !!error} onClick={() => openEntry(month === today.slice(0, 7) || month > today.slice(0, 7) ? today : `${month}-01`)}>데이터 입력</button>}>
    <section className={`g2-attendance g2-approved ${mobile ? 'phone' : 'grid'}`}>
      {!mutationEnabled && canEdit && <p role="status">현재 조회만 가능합니다.</p>}{feedback && <p role="status">{feedback}</p>}
      {error ? <div role="alert" className="g2-form-error">{error}<button onClick={() => { setError(''); setRevision(n => n + 1); }}>다시 시도</button></div> : !data ? <p role="status">G2 일별 자료를 불러오는 중입니다.</p> : mobile ? <>
        <div className="month-block">{monthHeading}</div><div className="list-caption"><span>월간 출근 현황</span><span>단위: 명</span></div><div id="daily"><div className="calendar-legend">오전·오후 합계 · 날짜를 누르면 상세 보기</div><G2Calendar month={month} days={days} selected={selected} color={color} label="출근 달력" buttonLabel="출근 상세" onSelect={date => { setSelected(date); setModal({ kind: 'detail', date }); }} renderMetrics={d => <span className="cal-metrics">{[sum(quantities(d).slice(0, 2)), sum(quantities(d).slice(2))].map((n, i) => <span className="cal-metric" key={i}>{d.date <= today && n != null && <><small>{i === 0 ? '오전' : '오후'}</small><em>{fmt(n)}</em></>}</span>)}</span>}/></div>
      </> : <>
        <table className="query-bar" aria-label="월 조회 및 인원 요약">{columns}<tbody><tr><td colSpan={2}>{monthHeading}</td><td colSpan={3} className="group">{summary(0)}</td><td colSpan={3} className="group">{summary(2)}</td><td className="group"><div className="query-title">하루 총원</div><strong className="attendance-day-total">{fmt(sum(latestValues))}</strong></td></tr></tbody></table>
        <div className="caption"><strong>월간 출근 현황</strong><span>수량 단위: 명{canEdit ? ' · 날짜 행을 누르면 입력·수정' : ''}</span></div><table id="operations" aria-label="월간 출근 현황">{columns}<thead><tr><th rowSpan={2}>주</th><th rowSpan={2}>일자</th><th colSpan={3} className="group">오전</th><th colSpan={3} className="group">오후</th><th rowSpan={2} className="group">하루 총원</th></tr><tr>{[0, 1].map(i => ['EMI', '도급', '합계'].map((label, j) => <th key={`${i}-${j}`} className={j === 0 ? 'group' : undefined}>{label}</th>))}</tr></thead><tbody>{days.map((d, index) => {
          const w = weekday(d.date), future = d.date > today, v = quantities(d);
          return <tr key={d.date} data-row={d.date} className={future ? 'future' : d.date === today ? 'today' : ''} onClick={() => openEntry(d.date)}>{(index === 0 || w === 0) && <td className="week" rowSpan={Math.min(7 - w, days.length - index)}>{Math.floor((Number(d.date.slice(8)) - 1 + weekday(`${month}-01`)) / 7) + 1}주</td>}<td className={`date ${color(d.date)}`} title={holidays.get(d.date)}>{canEdit && !future ? <button className="date-edit" disabled={!enabled} aria-label={`${d.date} 출근 인원 수정`}>{Number(d.date.slice(5, 7))}.{d.date.slice(8)} ({'일월화수목금토'[w]})</button> : `${Number(d.date.slice(5, 7))}.${d.date.slice(8)} (${'일월화수목금토'[w]})`}</td>{[v[0], v[1], sum(v.slice(0, 2)), v[2], v[3], sum(v.slice(2)), sum(v)].map((n, i) => <td key={i} className={[0, 3, 6].includes(i) ? 'group' : undefined}>{future ? '' : fmt(n)}</td>)}</tr>;
        })}</tbody></table><p className="footnote">토요일은 파란색, 일요일·공휴일은 빨간색으로 표시합니다. 미래 날짜는 빈칸으로 표시합니다.</p>
      </>}
      {modal?.kind === 'month' && <G2MonthPicker value={month} onClose={() => setModal(null)} onSelect={value => { if (value !== month) { setData(null); setError(''); setFeedback(''); setMonth(value); } setModal(null); }}/ >}
      {modal?.kind === 'detail' && selectedDay && <DataEntryDialog title={`${short(selectedDay.date)} 출근 현황`} variant="detail" onClose={() => setModal(null)} footer={canEdit ? <button className="primary" disabled={!enabled || selectedDay.date > today} onClick={() => openEntry(selectedDay.date)}>입력·수정</button> : undefined}><section className="detail-group"><h3>출근 인원</h3><table aria-label="오전 오후 출근 상세"><thead><tr><th>구분</th><th>오전</th><th>오후</th></tr></thead><tbody>{[['EMI', detailValues[0], detailValues[2]], ['도급', detailValues[1], detailValues[3]], ['합계', sum(detailValues.slice(0, 2)), sum(detailValues.slice(2))]].map(([label, a, b]) => <tr key={label}><th>{label}</th><td>{fmt(a as number | null)}</td><td>{fmt(b as number | null)}</td></tr>)}</tbody></table><div className="popup-facts attendance-total"><span>하루 총원</span><b>{sum(detailValues) == null ? '미입력' : `${fmt(sum(detailValues))}명`}</b></div></section></DataEntryDialog>}
      {modal?.kind === 'entry' && <AttendanceEditor key={modal.date} initialDate={modal.date} today={today} user={user} enabled={enabled} onClose={() => setModal(null)} onSaved={() => { setFeedback('출근 인원을 저장했습니다.'); setRevision(n => n + 1); }}/ >}
    </section>
  </G2PageFrame>;
}
