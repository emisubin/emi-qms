import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, getSalesKpi, getSalesKpiMonth, getSalesTargets, saveSalesTargets } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsBadge, DsPageHeader, DsSurface, DsToolbar } from './design-system';
import type { SalesKpiMonthDetail, SalesKpiResponse, SalesTargetsResponse } from './salesKpi';
import { SalesKpiChart } from './SalesKpiChart';
import { formatMoney } from './salesKpiFormat';
import { useActionFeedback } from './useActionFeedback';

type PageState = { kind: 'loading' } | { kind: 'ready'; data: SalesKpiResponse } | { kind: 'forbidden'; message: string } | { kind: 'error'; message: string };
type DetailState = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; data: SalesKpiMonthDetail } | { kind: 'error'; message: string };

export function SalesKpiPage({
  developmentUserKey,
  initialYear,
  initialCurrency,
  canManageTargets,
  onOpenProject,
  onOpenBilling = () => undefined
}: {
  developmentUserKey: string | undefined;
  initialYear?: number;
  initialCurrency?: string;
  canManageTargets: boolean;
  onOpenProject: (projectId: string) => void;
  onOpenBilling?: () => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [detail, setDetail] = useState<DetailState>({ kind: 'idle' });
  const [selectedMonth, setSelectedMonth] = useState(new Date().getMonth() + 1);
  const [targets, setTargets] = useState<SalesTargetsResponse | null>(null);
  const [targetInputs, setTargetInputs] = useState<string[]>(Array.from({ length: 12 }, () => ''));
  const [targetEditorOpen, setTargetEditorOpen] = useState(false);
  const generation = useRef(0);
  const actions = useActionFeedback();

  const load = useCallback(async (year?: number, currency?: string, preserve = false) => {
    const current = ++generation.current;
    if (!preserve) setState({ kind: 'loading' });
    try {
      const data = await getSalesKpi(developmentUserKey, { year, currency });
      if (generation.current !== current) return false;
      setState({ kind: 'ready', data });
      const url = new URL(window.location.href);
      url.searchParams.set('year', String(data.year));
      url.searchParams.set('currency', data.currency);
      window.history.replaceState(null, '', `${url.pathname}?${url.searchParams.toString()}`);
      return true;
    } catch (error) {
      if (generation.current !== current) return false;
      setState(error instanceof ApiError && error.status === 403
        ? { kind: 'forbidden', message: '영업 금액 지표를 조회할 권한이 없습니다.' }
        : { kind: 'error', message: error instanceof ApiError ? error.message : '영업 지표를 불러오지 못했습니다.' });
      return false;
    }
  }, [developmentUserKey]);

  useEffect(() => { queueMicrotask(() => void load(initialYear, initialCurrency)); }, [initialCurrency, initialYear, load]);

  const data = state.kind === 'ready' ? state.data : null;
  const openMonth = useCallback(async (month: number) => {
    if (!data) return;
    setSelectedMonth(month);
    setDetail({ kind: 'loading' });
    try {
      setDetail({ kind: 'ready', data: await getSalesKpiMonth(developmentUserKey, data.year, month, data.currency) });
    } catch (error) {
      setDetail({ kind: 'error', message: error instanceof ApiError ? error.message : '월별 근거를 불러오지 못했습니다.' });
    }
  }, [data, developmentUserKey]);

  useEffect(() => { if (data) queueMicrotask(() => void openMonth(selectedMonth)); }, [data?.year, data?.currency]); // eslint-disable-line react-hooks/exhaustive-deps

  async function openTargetEditor() {
    if (!data || !canManageTargets) return;
    const response = await getSalesTargets(developmentUserKey, data.year, data.currency);
    setTargets(response);
    setTargetInputs(response.months.map((month) => month.amount === null ? '' : String(month.amount)));
    setTargetEditorOpen(true);
    actions.reset('sales-targets');
  }

  async function saveTargetValues() {
    if (!data || !targets) return;
    await actions.run('sales-targets', async () => {
      const months = targetInputs.flatMap((value, index) => {
        if (value.trim() === '') return [];
        const amount = Number(value);
        if (!Number.isFinite(amount) || amount < 0) throw new Error(`${index + 1}월 목표 금액을 확인해 주세요.`);
        return [{ month: index + 1, amount, expectedVersion: targets.months[index]?.version ?? null }];
      });
      const response = await saveSalesTargets(developmentUserKey, { year: data.year, currency: data.currency, months });
      setTargets(response);
    }, {
      loadingMessage: '월별 목표를 저장하는 중입니다.',
      successMessage: '월별 목표를 저장했습니다.',
      errorFallback: '월별 목표를 저장하지 못했습니다.',
      refresh: () => load(data.year, data.currency, true)
    });
  }

  const targetFeedback = actions.feedbackFor('sales-targets');
  const kpiCards = useMemo(() => data ? [
    ['이번 달 확정', formatMoney(data.kpi.currentMonthRevenue, data.currency), '현재 월 세금계산서 기준'],
    ['연간 확정 매출', formatMoney(data.kpi.revenueTotal, data.currency), `${data.year}년 누계`],
    ['등록 목표 누계', formatMoney(data.kpi.targetTotal, data.currency), `${data.kpi.registeredTargetMonthCount}개월 등록`],
    ['목표 달성률', data.kpi.achievementRate === null ? '목표 미등록' : `${data.kpi.achievementRate}%`, '확정 매출 기준'],
    [data.kpi.exceededTargetAmount && data.kpi.exceededTargetAmount > 0 ? '초과 달성액' : '잔여 목표', formatMoney(data.kpi.exceededTargetAmount && data.kpi.exceededTargetAmount > 0 ? data.kpi.exceededTargetAmount : data.kpi.remainingTargetAmount, data.currency), '예상 파이프라인 제외']
  ] : [], [data]);
  const visibleKpiCards = isMobile
    ? [kpiCards[1], kpiCards[3], kpiCards[4]].filter((card): card is string[] => Boolean(card))
    : kpiCards;

  if (state.kind === 'loading') return <section className="page-surface sales-kpi-page"><p role="status">영업 지표를 불러오는 중입니다.</p></section>;
  if (state.kind === 'forbidden' || state.kind === 'error') return <section className="page-surface sales-kpi-page"><div className="sales-kpi-state" role="alert"><strong>{state.message}</strong><button type="button" onClick={() => void load(initialYear, initialCurrency)}>다시 시도</button></div></section>;
  if (!data) return null;

  return (
    <section className="page-surface sales-kpi-page" data-mobile-experience={isMobile || undefined} aria-labelledby="sales-kpi-title">
      <DsPageHeader
        className="sales-kpi-header"
        eyebrow="영업 현황"
        title="연간 매출 성과"
        titleId="sales-kpi-title"
        description="월 실적과 목표, 달성률을 한 화면에서 비교합니다."
        actions={<DsToolbar className="sales-kpi-controls" label="매출 조회 조건">
          <label>연도<select value={data.year} onChange={(event) => void load(Number(event.target.value), data.currency)}>{data.availableYears.map((year) => <option key={year}>{year}</option>)}</select></label>
          <label>통화<select value={data.currency} onChange={(event) => void load(data.year, event.target.value)}>{data.availableCurrencies.map((currency) => <option key={currency}>{currency}</option>)}</select></label>
          {canManageTargets && !isMobile ? <button type="button" className="secondary-button" onClick={() => void openTargetEditor()}>목표 관리</button> : null}
          <button type="button" className="secondary-button" onClick={onOpenBilling}>발행요청 자료</button>
        </DsToolbar>}
      />

      <div className="sales-kpi-cards">{visibleKpiCards.map(([label, value, note]) => <article key={label} data-shape-role="surface"><span>{label}</span><strong>{value}</strong><small>{note}</small></article>)}</div>

      <DsSurface className="sales-kpi-main-card">
        <header><div><p className="eyebrow">12개월 비교</p><h3>{data.year}년 월별 실적 · 목표 · 달성률</h3></div><DsBadge>{data.currency}</DsBadge></header>
        <SalesKpiChart months={data.months} currency={data.currency} year={data.year} selectedMonth={selectedMonth} mobile={isMobile} onSelectMonth={(month) => void openMonth(month)} />
        {isMobile ? (
          <label className="sales-kpi-month-control">
            <span>근거를 확인할 월</span>
            <select value={selectedMonth} onChange={(event) => void openMonth(Number(event.target.value))}>
              {data.months.map((month) => <option key={month.month} value={month.month}>{month.month}월</option>)}
            </select>
          </label>
        ) : null}
      </DsSurface>

      <div className="sales-kpi-subgrid">
        <article className="sales-pipeline-card"><span>예상 파이프라인</span><strong>{formatMoney(data.pipeline.amount, data.currency)}</strong><small>진행 프로젝트 {data.pipeline.projectCount}건 · 달성률에는 포함하지 않음</small></article>
        {!isMobile ? <article className="sales-data-note"><span>집계 확인</span><strong>{data.missingAmountCount}건</strong><small>완료 정산 중 판매금액 또는 통화 미입력</small></article> : null}
      </div>

      {isMobile ? (
        <details className="ds-surface sales-month-detail sales-month-detail--mobile">
          <summary><span>{selectedMonth}월 매출 근거</span><b>{detail.kind === 'ready' ? `${detail.data.projects.length}건` : '보기'}</b></summary>
          <div className="sales-month-detail-content">
            {detail.kind === 'loading' ? <p role="status">근거 목록을 불러오는 중입니다.</p> : null}
            {detail.kind === 'error' ? <p role="alert">{detail.message}</p> : null}
            {detail.kind === 'ready' && detail.data.projects.length === 0 ? <p>이 달에 완료된 확정 매출이 없습니다.</p> : null}
            {detail.kind === 'ready' && detail.data.projects.length > 0 ? <div className="sales-month-list">{detail.data.projects.map((project) => <button key={project.projectId} type="button" onClick={() => onOpenProject(project.projectId)}><span><strong>{project.projectName}</strong><small>{project.projectCode} · {project.invoiceIssuedDate}</small></span><b>{formatMoney(project.amount, data.currency)}</b></button>)}</div> : null}
          </div>
        </details>
      ) : (
        <DsSurface className="sales-month-detail">
          <header><div><p className="eyebrow">매출 근거</p><h3>{selectedMonth}월 확정 매출 근거</h3></div><DsBadge tone="neutral">{detail.kind === 'ready' ? `${detail.data.projects.length}건` : ''}</DsBadge></header>
          {detail.kind === 'loading' ? <p role="status">근거 목록을 불러오는 중입니다.</p> : null}
          {detail.kind === 'error' ? <p role="alert">{detail.message}</p> : null}
          {detail.kind === 'ready' && detail.data.projects.length === 0 ? <p>이 달에 완료된 확정 매출이 없습니다.</p> : null}
          {detail.kind === 'ready' && detail.data.projects.length > 0 ? <div className="sales-month-list">{detail.data.projects.map((project) => <button key={project.projectId} type="button" onClick={() => onOpenProject(project.projectId)}><span><strong>{project.projectName}</strong><small>{project.projectCode} · {project.invoiceIssuedDate}</small></span><b>{formatMoney(project.amount, data.currency)}</b></button>)}</div> : null}
        </DsSurface>
      )}

      {targetEditorOpen && targets ? <DsSurface className="sales-target-editor" label="월별 목표 관리">
        <header><div><p className="eyebrow">ADMIN TARGETS</p><h3>{data.year}년 월별 목표</h3></div><button type="button" onClick={() => setTargetEditorOpen(false)}>닫기</button></header>
        <div className="sales-target-grid">{targetInputs.map((value, index) => <label key={index}><span>{index + 1}월</span><input type="number" min="0" step="10000" value={value} placeholder="미등록" onChange={(event) => setTargetInputs((current) => current.map((item, itemIndex) => itemIndex === index ? event.target.value : item))} /></label>)}</div>
        {targetFeedback ? <p className="sales-target-feedback" data-tone={targetFeedback.tone} role={targetFeedback.tone === 'error' ? 'alert' : 'status'}>{targetFeedback.message}</p> : null}
        <button type="button" className="primary-button" disabled={actions.isBusy('sales-targets')} onClick={() => void saveTargetValues()}>선택한 목표 저장</button>
      </DsSurface> : null}
    </section>
  );
}
