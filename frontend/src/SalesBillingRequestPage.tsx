import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiError,
  createSalesBillingBatch,
  downloadSalesBillingWorkbook,
  getSalesBillingCandidates,
  listSalesBillingBatches
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsActionBar, DsBadge, DsInputFlow, DsInputSection, DsPageHeader, DsSurface, DsToolbar } from './design-system';
import type { SalesBillingBatch, SalesBillingCandidate, SalesBillingCandidateList } from './salesBilling';
import { formatMoney } from './salesKpiFormat';

type PageState =
  | { kind: 'loading' }
  | { kind: 'ready'; candidates: SalesBillingCandidateList; batches: SalesBillingBatch[] }
  | { kind: 'error'; message: string };

export function SalesBillingRequestPage({
  developmentUserKey,
  onOpenSalesKpi,
  onOpenProject
}: {
  developmentUserKey: string | undefined;
  onOpenSalesKpi: () => void;
  onOpenProject: (projectId: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [periodStart, setPeriodStart] = useState('');
  const [periodEnd, setPeriodEnd] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ tone: 'success' | 'error'; message: string } | null>(null);
  const latestLoadId = useRef(0);

  const load = useCallback(async (start?: string, end?: string, quiet = false) => {
    const loadId = ++latestLoadId.current;
    if (!quiet) setState({ kind: 'loading' });
    try {
      const [candidates, batches] = await Promise.all([
        getSalesBillingCandidates(developmentUserKey, start, end),
        listSalesBillingBatches(developmentUserKey)
      ]);
      if (loadId !== latestLoadId.current) return;
      setPeriodStart(candidates.period.periodStart);
      setPeriodEnd(candidates.period.periodEnd);
      setSelected((current) => new Set([...current].filter((id) => candidates.items.some((item) => item.projectId === id && item.canSelect))));
      setState({ kind: 'ready', candidates, batches: batches.items });
    } catch (error) {
      if (loadId !== latestLoadId.current) return;
      setState({ kind: 'error', message: messageOf(error, '발행요청 대상을 불러오지 못했습니다.') });
    }
  }, [developmentUserKey]);

  useEffect(() => { void load(); }, [load]);

  const selectableIds = useMemo(() => state.kind === 'ready'
    ? state.candidates.items.filter((item) => item.canSelect).map((item) => item.projectId)
    : [], [state]);
  const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selected.has(id));

  function toggleProject(projectId: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(projectId)) next.delete(projectId); else next.add(projectId);
      return next;
    });
  }

  function toggleAll() {
    setSelected(allSelected ? new Set() : new Set(selectableIds));
  }

  async function download(batchId: string) {
    const file = await downloadSalesBillingWorkbook(developmentUserKey, batchId);
    const url = URL.createObjectURL(file.blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = file.fileName;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  async function createRequest() {
    if (selected.size === 0 || busy) return;
    setBusy(true);
    setFeedback(null);
    try {
      const batch = await createSalesBillingBatch(developmentUserKey, {
        operationId: crypto.randomUUID(),
        periodStart,
        periodEnd,
        projectIds: [...selected],
        note: note.trim() || null
      });
      await download(batch.batchId);
      setSelected(new Set());
      setNote('');
      await load(periodStart, periodEnd, true);
      setFeedback({ tone: 'success', message: `${batch.projectCount}개 프로젝트의 회계팀 발행요청 자료를 만들었습니다.` });
    } catch (error) {
      setFeedback({ tone: 'error', message: messageOf(error, '발행요청 자료를 만들지 못했습니다.') });
    } finally {
      setBusy(false);
    }
  }

  if (state.kind === 'loading') return <section className="page-surface billing-request-page"><p role="status">출하 프로젝트를 불러오는 중입니다.</p></section>;
  if (state.kind === 'error') return <section className="page-surface billing-request-page"><div className="billing-feedback" data-tone="error" role="alert"><strong>{state.message}</strong><button type="button" onClick={() => void load()}>다시 시도</button></div></section>;

  const { candidates, batches } = state;
  return (
    <section className="page-surface billing-request-page" data-mobile-experience={isMobile || undefined} aria-labelledby="billing-request-title">
      <DsPageHeader
        eyebrow="영업 · 회계 요청"
        title="세금계산서 발행요청"
        titleId="billing-request-title"
        description="출하된 프로젝트를 골라 회계팀 전달용 Excel 자료를 한 번에 만듭니다."
        actions={<DsToolbar label="영업 화면 이동"><button type="button" className="secondary-button" onClick={onOpenSalesKpi}>연간 KPI</button></DsToolbar>}
      />

      <div className="billing-summary-cards">
        <article><span>출하 프로젝트</span><strong>{candidates.candidateCount}</strong><small>선택 기간 기준</small></article>
        <article><span>요청 가능</span><strong>{candidates.selectableCount}</strong><small>금액·Pending 확인 완료</small></article>
        <article><span>이미 요청</span><strong>{candidates.requestedCount}</strong><small>중복 요청 차단</small></article>
      </div>

      {feedback ? <div className="billing-feedback" data-tone={feedback.tone} role={feedback.tone === 'error' ? 'alert' : 'status'}>{feedback.message}</div> : null}

      <DsInputFlow title="발행요청 자료 만들기" description="기간을 확인하고 프로젝트를 선택한 뒤 Excel 생성 버튼 하나를 누르세요.">
        <DsInputSection number={1} title="요청 기간" description={candidates.period.isRecommended ? '1일·16일 업무주기 권장 기간입니다.' : '직접 선택한 기간입니다.'}>
          <DsSurface className="billing-period-card">
            <div><span className="eyebrow">요청 대상 기간</span><strong>{periodStart} – {periodEnd}</strong><small>{candidates.period.isRecommended ? '1일·16일 업무주기 권장 기간' : '직접 선택한 기간'}</small></div>
            {!isMobile ? <div className="billing-period-inputs"><label>시작일<input type="date" value={periodStart} onChange={(event) => setPeriodStart(event.target.value)} /></label><label>종료일<input type="date" value={periodEnd} onChange={(event) => setPeriodEnd(event.target.value)} /></label><button type="button" onClick={() => void load(periodStart, periodEnd)}>조회</button></div> : <button type="button" onClick={() => void load()}>권장 기간 새로고침</button>}
          </DsSurface>
        </DsInputSection>
        <DsInputSection number={2} title="프로젝트 선택" description="요청 가능한 프로젝트만 체크할 수 있습니다." actions={<DsBadge>{selected.size}개 선택</DsBadge>}>
          <DsSurface className="billing-selection-card">
            {candidates.items.length === 0 ? <p className="billing-empty">이 기간에 출하된 프로젝트가 없습니다.</p> : isMobile ? (
              <div className="billing-mobile-list">
                {candidates.items.map((item) => <BillingMobileItem key={item.projectId} item={item} checked={selected.has(item.projectId)} onToggle={() => toggleProject(item.projectId)} onOpen={() => onOpenProject(item.projectId)} />)}
              </div>
            ) : (
              <div className="billing-table-wrap"><table className="billing-table"><thead><tr><th><input aria-label="요청 가능 프로젝트 전체 선택" type="checkbox" checked={allSelected} onChange={toggleAll} disabled={selectableIds.length === 0} /></th><th>프로젝트</th><th>고객 / 품목</th><th>출하일</th><th>수량</th><th>금액</th><th>영업담당</th><th>상태</th></tr></thead><tbody>
                {candidates.items.map((item) => <tr key={item.projectId} data-disabled={!item.canSelect || undefined}><td><input aria-label={`${item.projectCode} 선택`} type="checkbox" disabled={!item.canSelect} checked={selected.has(item.projectId)} onChange={() => toggleProject(item.projectId)} /></td><td><button type="button" className="billing-project-link" onClick={() => onOpenProject(item.projectId)}><strong>{item.projectCode}</strong><small>{item.projectTitle}</small></button></td><td><strong>{item.customerName}</strong><small>{item.item}</small></td><td>{item.lastDepartureDate}<small>{item.firstDepartureDate !== item.lastDepartureDate ? `${item.firstDepartureDate}부터` : '당일 출하'}</small></td><td>{item.departedPanelCount}/{item.activePanelCount}</td><td>{item.salesAmount === null ? '-' : formatMoney(item.salesAmount, item.currencyCode)}</td><td>{item.salesOwnerName}</td><td>{item.canSelect ? <DsBadge tone="success">요청 가능</DsBadge> : <DsBadge tone="neutral">{item.requested ? `요청 #${item.requestNumber}` : item.blockedReason ?? '확인 필요'}</DsBadge>}</td></tr>)}
              </tbody></table></div>
            )}
          </DsSurface>
        </DsInputSection>
        <DsInputSection number={3} title="회계팀 전달 메모" description="추가 안내가 있을 때만 입력합니다.">
          <label><span>회계팀 전달 메모 <small>선택</small></span><input maxLength={500} value={note} onChange={(event) => setNote(event.target.value)} placeholder="이번 요청에 필요한 참고사항" /></label>
        </DsInputSection>
        <DsActionBar description="선택 프로젝트를 월별 발행요청 Excel 한 건으로 만듭니다.">
          <button type="button" className="primary-button" disabled={selected.size === 0 || busy} onClick={() => void createRequest()}>{busy ? '자료 생성 중…' : `선택 ${selected.size}개 Excel 만들기`}</button>
        </DsActionBar>
      </DsInputFlow>

      <DsSurface className="billing-history-card">
        <header><div><p className="eyebrow">REQUEST HISTORY</p><h3>최근 발행요청 자료</h3></div><DsBadge tone="neutral">{batches.length}건</DsBadge></header>
        {batches.length === 0 ? <p className="billing-empty">아직 만든 발행요청 자료가 없습니다.</p> : <div className="billing-history-list">{batches.map((batch) => <article key={batch.batchId}><div><strong>요청 #{batch.requestNumber}</strong><span>{batch.periodStart} – {batch.periodEnd} · {batch.projectCount}개</span><small>{batch.createdByName} · {formatDateTime(batch.createdAtUtc)}</small></div><button type="button" onClick={() => void download(batch.batchId)}>Excel 다시 받기</button></article>)}</div>}
      </DsSurface>
    </section>
  );
}

function BillingMobileItem({ item, checked, onToggle, onOpen }: { item: SalesBillingCandidate; checked: boolean; onToggle: () => void; onOpen: () => void }) {
  return <article data-disabled={!item.canSelect || undefined}><label><input type="checkbox" disabled={!item.canSelect} checked={checked} onChange={onToggle} /><span><strong>{item.projectCode}</strong><small>{item.customerName} · {item.item}</small></span></label><button type="button" onClick={onOpen}>{item.lastDepartureDate}<b>{item.salesAmount === null ? '-' : formatMoney(item.salesAmount, item.currencyCode)}</b></button><p>{item.canSelect ? `${item.departedPanelCount}/${item.activePanelCount} panel · ${item.salesOwnerName}` : item.requested ? `발행요청 #${item.requestNumber} 완료` : item.blockedReason ?? '확인이 필요합니다.'}</p></article>;
}

function messageOf(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    const validation = error.errors ? Object.values(error.errors).flat()[0] : null;
    return validation ?? error.message;
  }
  return error instanceof Error ? error.message : fallback;
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('ko-KR', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Seoul' }).format(new Date(value));
}
