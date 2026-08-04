import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsActionBar, DsEmptyState, DsInputFlow, DsInputSection, DsReadOnlyBanner } from './design-system';
import {
  ApiError,
  cancelMaterialReceipt,
  confirmMaterialReceipt,
  getMaterialIqcQueue,
  getMaterialReceipts,
  getPendingIssue,
  reconcileMaterialIqcQueue,
  recordMaterialIqcResult,
  registerMaterialArrival
} from './api';
import { MobileSheet } from './MobileSheet';
import { IqcReportWorkspace } from './IqcReportWorkspace';
import { OperationalProjectDashboard } from './OperationalProjectDashboard';
import { PendingInspectionContext } from './PendingInspectionContext';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';
import { useActionFeedback, type ActionFeedbackState } from './useActionFeedback';
import type {
  MaterialIqcQueueItem,
  MaterialReceipt,
  MaterialReceiptListResponse,
  MaterialReceiptStatus,
  MaterialReceivingItem
} from './materials';

type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; message: string };

type MaterialAction =
  | { kind: 'arrival'; item: MaterialReceivingItem }
  | { kind: 'receipt'; item: MaterialReceivingItem; receipt: MaterialReceipt };

export function MaterialReceivingPage({
  developmentUserKey,
  canUpdate,
  initialProjectCode,
  initialProjectId,
  initialReceiptId,
  initialRisk,
  onOpenIqc,
  onOpenKitting,
  onOpenPending
}: {
  developmentUserKey: string;
  canUpdate: boolean;
  initialProjectCode?: string;
  initialProjectId?: string;
  initialReceiptId?: string;
  initialRisk?: 'customer-supply-overdue';
  onOpenIqc: (requestId?: string) => void;
  onOpenKitting: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const [state, setState] = useState<LoadState<MaterialReceiptListResponse>>({ kind: 'loading' });
  const [search, setSearch] = useState(initialProjectCode ?? '');
  const [appliedSearch, setAppliedSearch] = useState(initialProjectCode ?? '');
  const [includeCompleted, setIncludeCompleted] = useState(Boolean(initialProjectCode || initialProjectId || initialReceiptId));
  const [supplyFilter, setSupplyFilter] = useState<'All' | 'Purchased' | 'CustomerSupplied'>(initialRisk ? 'CustomerSupplied' : 'All');
  const [customerSupplyOverdueOnly, setCustomerSupplyOverdueOnly] = useState(initialRisk === 'customer-supply-overdue');
  const [activeFilter, setActiveFilter] = useState<'all' | 'iqc' | 'blocked' | 'confirm'>('all');
  const [action, setAction] = useState<MaterialAction | null>(null);
  const [expandedProjectId, setExpandedProjectId] = useState<string | null>(null);
  const actions = useActionFeedback();
  const loadGenerationRef = useRef(0);

  const load = useCallback(async (preserve = false) => {
    const generation = loadGenerationRef.current + 1;
    loadGenerationRef.current = generation;
    if (!preserve) setState({ kind: 'loading' });
    try {
      const data = await getMaterialReceipts(developmentUserKey, appliedSearch, includeCompleted, '', '', supplyFilter);
      if (generation !== loadGenerationRef.current) return false;
      setState({ kind: 'ready', data });
      if (!preserve && (initialProjectCode || initialProjectId)) {
        setExpandedProjectId(
          initialProjectId
          ?? data.items.find((item) => item.projectCode === initialProjectCode)?.projectId
          ?? null);
      }
      return true;
    } catch (error) {
      if (generation !== loadGenerationRef.current) return false;
      if (!preserve) setState({ kind: 'error', message: errorMessage(error, '자재 입고 현황을 불러오지 못했습니다.') });
      return false;
    }
  }, [appliedSearch, developmentUserKey, includeCompleted, initialProjectCode, initialProjectId, supplyFilter]);

  useEffect(() => {
    queueMicrotask(() => void load());
  }, [load]);

  useEffect(() => {
    if (state.kind !== 'ready') {
      return;
    }
    const receiptId = initialReceiptId ?? new URLSearchParams(window.location.search).get('receipt');
    if (!receiptId) {
      return;
    }
    for (const item of state.data.items) {
      const receipt = item.receipts.find((candidate) => candidate.receiptId === receiptId);
      if (receipt) {
        queueMicrotask(() => setAction({ kind: 'receipt', item, receipt }));
        break;
      }
    }
  }, [initialReceiptId, state]);

  const visibleItems = useMemo(() => {
    if (state.kind !== 'ready') {
      return [];
    }
    const riskFilteredItems = customerSupplyOverdueOnly
      ? state.data.items.filter((item) => item.customerSupplyOverdue)
      : state.data.items;
    if (activeFilter === 'all') return riskFilteredItems;
    if (activeFilter === 'confirm') {
      return riskFilteredItems.filter((item) => item.receipts.some((receipt) => receipt.status === 'Passed' || receipt.status === 'InspectionNotRequired'));
    }
    const statusByFilter: Record<Exclude<typeof activeFilter, 'all' | 'confirm'>, MaterialReceiptStatus> = {
      iqc: 'IqcRequested',
      blocked: 'FailedBlocked'
    };
    return riskFilteredItems.filter((item) => item.receipts.some((receipt) => receipt.status === statusByFilter[activeFilter]));
  }, [activeFilter, customerSupplyOverdueOnly, state]);
  const receiptSelection = useSelectedRows(visibleItems.map((item) => item.itemId));
  const visibleProjects = useMemo(() => {
    const grouped = new Map<string, { projectId: string; projectCode: string; projectTitle: string; items: MaterialReceivingItem[] }>();
    for (const item of visibleItems) {
      const project = grouped.get(item.projectId) ?? {
        projectId: item.projectId,
        projectCode: item.projectCode,
        projectTitle: item.projectTitle,
        items: []
      };
      project.items.push(item);
      grouped.set(item.projectId, project);
    }
    return [...grouped.values()];
  }, [visibleItems]);

  async function runAction(scope: string, operation: () => Promise<unknown>, success: string) {
    const result = await actions.run(scope, operation, {
      loadingMessage: '처리 중입니다.',
      successMessage: success,
      errorFallback: '자재 작업을 완료하지 못했습니다.',
      refresh: () => load(true)
    });
    if (result === 'success' || result === 'partial') {
      setAction(null);
    }
  }

  const actionPanel = action ? (
    <MaterialActionPanel
      action={action}
      canUpdate={canUpdate}
      feedback={actions.latestFeedback}
      onClose={() => { setAction(null); actions.reset(); }}
      onOpenIqc={onOpenIqc}
      onOpenPending={onOpenPending}
      onRun={runAction}
      developmentUserKey={developmentUserKey}
    />
  ) : null;

  return (
    <section className="page-surface material-workspace" data-testid="material-receiving-page">
      <header className="material-hero">
        <div>
          <p className="eyebrow">자재 흐름</p>
          <h2>자재 입고 관리</h2>
          <p>도착부터 IQC, 입고 확정까지 한 흐름으로 관리합니다.</p>
          {initialProjectCode ? <p className="workspace-project-filter" role="status">현재 프로젝트: <strong>{initialProjectCode}</strong></p> : null}
        </div>
        <div className="material-hero-actions">
          <button type="button" onClick={onOpenKitting}>패널 키팅</button>
          <button type="button" className="primary-button" onClick={() => onOpenIqc()}>IQC 검사함</button>
        </div>
      </header>

      {!canUpdate ? <DsReadOnlyBanner description="도착·IQC·입고 확정 현황을 조회할 수 있습니다. 도착 등록과 입고 확정은 자재 담당자에게 요청하세요." /> : null}

      {actions.latestFeedback && action === null ? <InlineActionFeedback feedback={actions.latestFeedback} /> : null}

      {state.kind === 'ready' ? (
        <div className="material-summary-strip" aria-label="자재 입고 요약">
          <SummaryButton label="도착 등록 대기" value={state.data.summary.pendingArrivalCount} active={activeFilter === 'all'} onClick={() => setActiveFilter('all')} />
          <SummaryButton label="IQC 대기" value={state.data.summary.waitingIqcCount} active={activeFilter === 'iqc'} onClick={() => setActiveFilter('iqc')} />
          <SummaryButton label="부적합 차단" value={state.data.summary.failedBlockedCount} active={activeFilter === 'blocked'} tone="danger" onClick={() => setActiveFilter('blocked')} />
          <SummaryButton label="확정 대기" value={state.data.summary.readyToConfirmCount} active={activeFilter === 'confirm'} onClick={() => setActiveFilter('confirm')} />
        </div>
      ) : null}

      <form className="material-search-panel" onSubmit={(event) => {
        event.preventDefault();
        const nextSearch = search.trim();
        if (nextSearch === appliedSearch) {
          void load();
        } else {
          setAppliedSearch(nextSearch);
        }
      }}>
        <label>
          <span>프로젝트·품목 검색</span>
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="PJT 코드, 발주품목, 업체" />
        </label>
        <label className="material-completed-toggle">
          <input type="checkbox" checked={includeCompleted} onChange={(event) => setIncludeCompleted(event.target.checked)} />
          <span>완료 포함</span>
        </label>
        <button type="submit">검색</button>
      </form>

      <div className="material-supply-filter" role="group" aria-label="공급 방식 필터">
        {([
          ['All', '전체'],
          ['Purchased', '일반 구매'],
          ['CustomerSupplied', '사급']
        ] as const).map(([value, label]) => (
          <button type="button" key={value} data-active={supplyFilter === value && !customerSupplyOverdueOnly} onClick={() => {
            setSupplyFilter(value);
            setCustomerSupplyOverdueOnly(false);
          }}>{label}</button>
        ))}
        <button type="button" data-active={customerSupplyOverdueOnly} onClick={() => {
          setSupplyFilter('CustomerSupplied');
          setCustomerSupplyOverdueOnly(true);
        }}>제공 지연</button>
        {state.kind === 'ready' ? <span>사급 {state.data.summary.customerSuppliedItemCount} · 제공 지연 {state.data.summary.customerSupplyOverdueCount}</span> : null}
      </div>

      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="material-receipts"
          visibleIds={visibleItems.map((item) => item.itemId)}
          selectedIds={receiptSelection.selectedIds}
          allSelected={receiptSelection.allSelected}
          busy={receiptSelection.busy}
          filters={{ search: appliedSearch, includeCompleted: String(includeCompleted), supplyType: supplyFilter === 'All' ? undefined : supplyFilter }}
          onBusyChange={receiptSelection.setBusy}
          onToggleAll={receiptSelection.toggleAll}
          onClear={receiptSelection.clear}
        />
      ) : null}

      {state.kind === 'loading' ? <MaterialLoading /> : null}
      {state.kind === 'error' ? <p className="error-text" role="alert">{state.message}</p> : null}
      {state.kind === 'ready' && visibleItems.length === 0 ? (
        <DsEmptyState
          title="표시할 입고 항목이 없습니다."
          description="필터를 초기화하거나 구매팀의 발주품 입력 여부를 확인해 주세요."
          primaryAction={{ label: '필터 초기화', onClick: () => {
            setSearch('');
            setAppliedSearch('');
            setIncludeCompleted(false);
            setSupplyFilter('All');
            setCustomerSupplyOverdueOnly(false);
            setActiveFilter('all');
          } }}
        />
      ) : null}

      {state.kind === 'ready' ? (
        <div className="material-project-list" role="list" aria-label="자재 입고 프로젝트">
          {visibleProjects.map((project, projectIndex) => {
            const expanded = expandedProjectId === project.projectId;
            const completedCount = project.items.filter((item) => item.receiptCompleted).length;
            const partialCount = project.items.filter((item) => !item.receiptCompleted && (item.confirmedQuantity ?? 0) > 0).length;
            const confirmCount = project.items.flatMap((item) => item.receipts).filter((receipt) => receipt.status === 'Passed' || receipt.status === 'InspectionNotRequired').length;
            return (
              <article className="material-project-row" key={project.projectId} role="listitem" data-expanded={expanded}>
                <button
                  type="button"
                  className="material-project-toggle"
                  aria-expanded={expanded}
                  onClick={() => {
                    setExpandedProjectId(expanded ? null : project.projectId);
                    setAction(null);
                  }}
                >
                  <span className="material-project-number">{String(projectIndex + 1).padStart(2, '0')}</span>
                  <span className="material-project-identity"><small>{project.projectCode}</small><strong>{project.projectTitle}</strong></span>
                  <span><small>구매품</small><b>{project.items.length}건</b></span>
                  <span><small>입고 완료</small><b>{completedCount}/{project.items.length}</b></span>
                  <span><small>부분 입고</small><b>{partialCount}건</b></span>
                  <span><small>확정 대기</small><b>{confirmCount}건</b></span>
                  <i aria-hidden="true">{expanded ? '−' : '+'}</i>
                </button>
                {expanded ? (
                  <div className="material-project-purchases">
                    <div className="material-purchase-head" role="row">
                      <span>선택</span><span>구분</span><span>발주품목</span><span>업체</span><span>발주·예정량</span><span>입고예정일</span><span>입고 현황</span><span>작업</span>
                    </div>
                    {project.items.map((item) => (
                      <div className="material-purchase-entry" key={item.itemId} data-action-open={action?.item.itemId === item.itemId}>
                        <MaterialPurchaseRow
                          item={item}
                          canUpdate={canUpdate}
                          onAction={setAction}
                          selected={receiptSelection.selectedIds.has(item.itemId)}
                          selectionBusy={receiptSelection.busy}
                          onSelectionChange={(checked) => receiptSelection.toggle(item.itemId, checked)}
                        />
                        {action?.item.itemId === item.itemId ? (
                          <div className="material-continuous-action material-purchase-action" aria-live="polite">{actionPanel}</div>
                        ) : null}
                      </div>
                    ))}
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}

export function MaterialIqcPage({
  developmentUserKey,
  canInspect,
  initialProjectId,
  initialRequestId,
  onBack,
  onOpenProject,
  onOpenPending
}: {
  developmentUserKey: string;
  canInspect: boolean;
  initialProjectId?: string;
  initialRequestId?: string;
  onBack: () => void;
  onOpenProject?: (projectId: string) => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const layout = useAdaptiveLayout();
  const [state, setState] = useState<LoadState<MaterialIqcQueueItem[]>>({ kind: 'loading' });
  const [includeDecided, setIncludeDecided] = useState(false);
  const [selected, setSelected] = useState<MaterialIqcQueueItem | null>(null);
  const [reason, setReason] = useState('');
  const [reconciliationWarning, setReconciliationWarning] = useState('');
  const actions = useActionFeedback();
  const loadGenerationRef = useRef(0);
  const reconciliationIdentityRef = useRef('');
  const iqcVisibleIds = state.kind === 'ready' ? state.data.map((item) => item.attemptId) : [];
  const iqcSelection = useSelectedRows(iqcVisibleIds);

  const load = useCallback(async (preserve = false) => {
    const generation = loadGenerationRef.current + 1;
    loadGenerationRef.current = generation;
    if (!preserve) setState({ kind: 'loading' });
    try {
      const reconciliationIdentity = `${developmentUserKey}:${initialProjectId ?? 'all'}`;
      if (canInspect && reconciliationIdentityRef.current !== reconciliationIdentity) {
        try {
          await reconcileMaterialIqcQueue(developmentUserKey);
          reconciliationIdentityRef.current = reconciliationIdentity;
          setReconciliationWarning('');
        } catch (error) {
          setReconciliationWarning(errorMessage(error, '기존 누락 도착분의 IQC 자동 복구를 완료하지 못했습니다.'));
        }
      }
      const response = await getMaterialIqcQueue(developmentUserKey, includeDecided);
      if (generation !== loadGenerationRef.current) return false;
      let visibleItems = initialProjectId
        ? response.items.filter((item) => item.projectId === initialProjectId)
        : response.items;
      const unresolvedPendingIds = [...new Set(visibleItems
        .filter((item) => item.pendingIssueId && !item.pendingIssueNumber)
        .map((item) => item.pendingIssueId!))];
      if (unresolvedPendingIds.length > 0) {
        const pendingDetails = await Promise.allSettled(unresolvedPendingIds.map((pendingId) => getPendingIssue(developmentUserKey, pendingId)));
        const issueNumberById = new Map<string, number>();
        pendingDetails.forEach((result, index) => {
          if (result.status === 'fulfilled') issueNumberById.set(unresolvedPendingIds[index], result.value.issue.issueNumber);
        });
        visibleItems = visibleItems.map((item) => item.pendingIssueId && !item.pendingIssueNumber
          ? { ...item, pendingIssueNumber: issueNumberById.get(item.pendingIssueId) ?? null }
          : item);
      }
      setState({ kind: 'ready', data: visibleItems });
      const requestedId = initialRequestId ?? new URLSearchParams(window.location.search).get('request');
      setSelected((current) => visibleItems.find((item) => item.attemptId === requestedId)
        ?? visibleItems.find((item) => item.attemptId === current?.attemptId)
        ?? null);
      return true;
    } catch (error) {
      if (generation !== loadGenerationRef.current) return false;
      if (!preserve) setState({ kind: 'error', message: errorMessage(error, 'IQC 검사함을 불러오지 못했습니다.') });
      return false;
    }
  }, [canInspect, developmentUserKey, includeDecided, initialProjectId, initialRequestId]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  async function submit(result: 'Passed' | 'Failed') {
    if (!selected || reason.trim().length < 3) {
      actions.setFeedback('iqc:decision', { tone: 'error', message: '판정 사유를 3자 이상 입력해 주세요.' });
      queueMicrotask(() => document.querySelector<HTMLElement>('[data-field="iqcReason"]')?.focus());
      return;
    }
    let pendingIssueId: string | null = null;
    const actionResult = await actions.run(`iqc:${selected.attemptId}:decision`, async () => {
      const response = await recordMaterialIqcResult(
        developmentUserKey,
        selected.attemptId,
        selected.receiptVersion,
        result,
        reason.trim()
      );
      pendingIssueId = response.pendingIssueId;
    }, {
      loadingMessage: 'IQC 판정을 저장하는 중입니다.',
      successMessage: result === 'Passed' ? 'IQC 합격으로 판정했습니다.' : '부적합 Pending을 등록하고 입고를 차단했습니다.',
      errorFallback: 'IQC 판정을 저장하지 못했습니다.',
      refresh: () => load(true)
    });
    if (actionResult === 'success' || actionResult === 'partial') {
      setReason('');
      setSelected(null);
      if (result === 'Failed' && pendingIssueId) {
        window.setTimeout(() => onOpenPending(pendingIssueId!), 450);
      }
    }
  }

  const inspector = selected ? (
    <div className="iqc-inspection-workspace">
      {selected.pendingIssueId ? <PendingInspectionContext pendingId={selected.pendingIssueId} developmentUserKey={developmentUserKey} /> : null}
      {selected.decisionMode === 'Legacy' && selected.status === 'Requested' ? (
        <IqcInspector
          item={selected}
          reason={reason}
          feedback={actions.latestFeedback}
          disabled={!canInspect || actions.hasBusyPrefix('iqc:') || selected.status !== 'Requested'}
          onReason={setReason}
          onSubmit={submit}
          onClose={() => { setSelected(null); actions.reset(); setReason(''); }}
          onOpenPending={onOpenPending}
        />
      ) : (
        <IqcReportWorkspace
          attemptId={selected.attemptId}
          developmentUserKey={developmentUserKey}
          canInspect={canInspect}
          onClose={() => { setSelected(null); actions.reset(); setReason(''); void load(); }}
          onChanged={() => setIncludeDecided(true)}
        />
      )}
    </div>
  ) : null;
  const reinspectionItems = state.kind === 'ready' ? state.data.filter((item) => item.pendingIssueId !== null) : [];
  const initialInspectionItems = state.kind === 'ready' ? state.data.filter((item) => item.pendingIssueId === null) : [];

  function renderIqcCard(item: MaterialIqcQueueItem) {
    return (
      <article className="iqc-request-card selected-export-row" key={item.attemptId} data-status={item.status} data-report={item.reportStatus ?? item.decisionMode} data-reinspection={item.pendingIssueId !== null || undefined}>
        <SelectionCheckbox checked={iqcSelection.selectedIds.has(item.attemptId)} disabled={iqcSelection.busy} label={`${item.projectCode} ${item.orderItem ?? '품목'} 선택`} onChange={(checked) => iqcSelection.toggle(item.attemptId, checked)} />
        <button type="button" className="iqc-request-open" onClick={() => { setSelected(item); setReason(item.reason ?? ''); }}>
          <span className="iqc-request-top"><strong>{item.projectCode}</strong><span className="material-card-badges">{item.pendingIssueId ? <span className="iqc-reinspection-badge">{item.pendingIssueNumber ? `재검사 · P-${String(item.pendingIssueNumber).padStart(4, '0')}` : 'Pending 재검사'}</span> : null}{item.decisionMode === 'Legacy' ? <span className="iqc-report-badge" data-kind="legacy">이전 양식</span> : <span className="iqc-report-badge" data-kind={item.reportStatus ?? 'new'}>{item.decisionMode === 'ScanBased' ? (item.reportStatus === 'Finalized' ? '스캔 검사 완료' : item.reportStatus === 'Draft' ? '스캔 등록 중' : '외함 스캔 검사') : item.reportStatus === 'Finalized' ? '성적서 완료' : item.reportStatus === 'Draft' ? '작성 중' : '신규 성적서'}</span>}{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={false} /> : null}<StatusBadge status={item.status === 'Requested' ? 'IqcRequested' : item.status === 'Passed' ? 'Passed' : 'FailedBlocked'} /></span></span>
          <b>{item.orderItem ?? '발주품목 미입력'}</b>
          <small>{item.projectTitle}</small>
          <span>{formatQuantity(item.quantity, item.unit)} · {item.attemptNumber}차 검사</span>
          <small>{formatDateTime(item.requestedAtUtc)} 요청</small>
        </button>
      </article>
    );
  }

  if (state.kind === 'ready' && !initialProjectId) {
    const grouped = new Map<string, MaterialIqcQueueItem[]>();
    for (const item of state.data) {
      grouped.set(item.projectId, [...(grouped.get(item.projectId) ?? []), item]);
    }
    const projects = [...grouped.values()];
    const requestedCount = state.data.filter((item) => item.status === 'Requested').length;
    const pendingCount = state.data.filter((item) => item.pendingIssueId !== null).length;
    const inProgressCount = state.data.filter((item) => item.reportStatus === 'Draft').length;
    const completedCount = state.data.filter((item) => item.status === 'Passed').length;
    return (
      <OperationalProjectDashboard
        testId="quality-iqc-dashboard"
        eyebrow="QUALITY · IQC"
        title="IQC 프로젝트"
        description="도착분 IQC가 있는 프로젝트를 선택하면 해당 프로젝트의 구매품목 검사만 표시합니다."
        unitLabel="도착분"
        metrics={[
          { label: '검사 대기', value: requestedCount, helper: '판정 전 도착분' },
          { label: '작성 중', value: inProgressCount, helper: '검사성적서 작성 중' },
          { label: 'Pending 재검사', value: pendingCount, helper: '부적합 조치 확인', tone: 'warning' },
          { label: '합격', value: completedCount, helper: '입고 확정 가능', tone: 'positive' }
        ]}
        projects={projects.map((items) => {
          const first = items[0];
          return {
            projectId: first.projectId,
            projectCode: first.projectCode,
            projectTitle: first.projectTitle,
            totalCount: items.length,
            readyCount: items.filter((item) => item.status === 'Requested' && item.reportStatus !== 'Draft').length,
            inProgressCount: items.filter((item) => item.reportStatus === 'Draft').length,
            blockedCount: items.filter((item) => item.status === 'Failed' || item.pendingIssueId !== null).length,
            completedCount: items.filter((item) => item.status === 'Passed').length,
            detail: `${items.length}개 도착분 · 품목별 검사`
          };
        })}
        emptyMessage="IQC 대상 프로젝트가 없습니다."
        readOnlyDescription={!canInspect ? 'IQC 현황과 판정 결과를 조회할 수 있습니다. 성적서 작성과 판정은 품질 담당자 권한이 필요합니다.' : undefined}
        onOpenProject={(projectId) => onOpenProject?.(projectId)}
      />
    );
  }

  return (
    <section className="page-surface material-workspace material-iqc-workspace" data-testid="material-iqc-page">
      <header className="material-hero material-hero--quality">
        <div>
          <p className="eyebrow">수입검사</p>
          <h2>IQC 검사함</h2>
          <p>요청된 도착분을 확인하고 합격 또는 부적합을 기록합니다.</p>
          {initialProjectId && state.kind === 'ready' ? <p className="workspace-project-filter" role="status">선택 프로젝트: <strong>{state.data[0]?.projectCode ?? '현재 프로젝트'}</strong></p> : null}
        </div>
        <button type="button" onClick={onBack}>IQC 프로젝트</button>
      </header>

      {!canInspect ? <DsReadOnlyBanner description="IQC 현황과 판정 결과를 조회할 수 있습니다. 검사성적서 작성과 합격·부적합 판정은 품질 담당자에게 요청하세요." /> : null}
      {reconciliationWarning ? <p className="warning-text" role="alert">{reconciliationWarning} 현재 검사 목록은 계속 확인할 수 있습니다.</p> : null}

      {actions.latestFeedback && selected === null ? <InlineActionFeedback feedback={actions.latestFeedback} /> : null}

      <div className="iqc-toolbar">
        <div><strong>{state.kind === 'ready' ? state.data.filter((item) => item.status === 'Requested').length : '-'}건</strong><span>검사 대기</span></div>
        <label><input type="checkbox" checked={includeDecided} onChange={(event) => setIncludeDecided(event.target.checked)} /> 판정 완료 포함</label>
      </div>

      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="material-iqc"
          visibleIds={iqcVisibleIds}
          selectedIds={iqcSelection.selectedIds}
          allSelected={iqcSelection.allSelected}
          busy={iqcSelection.busy}
          filters={{ includeDecided: String(includeDecided), projectId: initialProjectId }}
          onBusyChange={iqcSelection.setBusy}
          onToggleAll={iqcSelection.toggleAll}
          onClear={iqcSelection.clear}
        />
      ) : null}

      {state.kind === 'loading' ? <MaterialLoading /> : null}
      {state.kind === 'error' ? <p className="error-text" role="alert">{state.message}</p> : null}
      {state.kind === 'ready' && state.data.length === 0 ? <div className="material-empty-state"><strong>검사 대기 항목이 없습니다.</strong><span>새 IQC 요청이 들어오면 여기에 표시됩니다.</span></div> : null}
      {state.kind === 'ready' && reinspectionItems.length > 0 ? (
        <section className="iqc-queue-section iqc-queue-section--reinspection" aria-labelledby="iqc-reinspection-queue-title">
          <header><div><p className="eyebrow">부적합 후속 검사</p><h3 id="iqc-reinspection-queue-title">Pending 재검사</h3></div><strong>{reinspectionItems.filter((item) => item.status === 'Requested').length}건</strong></header>
          <div className={layout.isMobile ? 'iqc-card-list iqc-card-list--mobile' : 'iqc-card-list'}>{reinspectionItems.map(renderIqcCard)}</div>
        </section>
      ) : null}
      {state.kind === 'ready' && initialInspectionItems.length > 0 ? (
        <section className="iqc-queue-section" aria-labelledby="iqc-initial-queue-title">
          {reinspectionItems.length > 0 ? <header><div><p className="eyebrow">일반 수입검사</p><h3 id="iqc-initial-queue-title">일반 IQC</h3></div><strong>{initialInspectionItems.filter((item) => item.status === 'Requested').length}건</strong></header> : null}
          <div className={layout.isMobile ? 'iqc-card-list iqc-card-list--mobile' : 'iqc-card-list'}>{initialInspectionItems.map(renderIqcCard)}</div>
        </section>
      ) : null}

      {layout.isMobile ? (
        <MobileSheet open={selected !== null} title={selected?.decisionMode === 'Detailed' ? '디지털 검사성적서' : selected?.decisionMode === 'ScanBased' ? '외함 스캔 IQC' : 'IQC 판정'} eyebrow="수입검사" description={selected?.decisionMode === 'Detailed' ? '항목·사진·판정을 한 흐름으로 기록합니다.' : selected?.decisionMode === 'ScanBased' ? '서명 검사서를 등록하고 적합·부적합을 확정합니다.' : '도착분과 검사 차수를 확인한 뒤 판정합니다.'} onClose={() => { setSelected(null); actions.reset(); setReason(''); void load(); }} fullScreen>
          {inspector}
        </MobileSheet>
      ) : inspector ? <aside className={`material-action-drawer${selected?.decisionMode !== 'Legacy' ? ' material-action-drawer--iqc-report' : ''}`}>{inspector}</aside> : null}
    </section>
  );
}

function MaterialPurchaseRow({ item, canUpdate, onAction, selected, selectionBusy, onSelectionChange }: {
  item: MaterialReceivingItem;
  canUpdate: boolean;
  onAction: (action: MaterialAction) => void;
  selected: boolean;
  selectionBusy: boolean;
  onSelectionChange: (selected: boolean) => void;
}) {
  const [historyOpen, setHistoryOpen] = useState(false);
  const activeReceipts = item.receipts.filter((receipt) => receipt.status !== 'Cancelled');
  const historyId = `material-history-${item.itemId}`;
  const latestStatus = item.receiptCompleted
    ? '입고 완료'
    : (item.confirmedQuantity ?? 0) > 0
      ? '부분 입고'
      : item.receipts.some((receipt) => receipt.status === 'Passed' || receipt.status === 'InspectionNotRequired')
        ? '입고 확정 대기'
        : item.receipts.some((receipt) => receipt.status === 'FailedBlocked')
          ? 'IQC 부적합'
          : item.receipts.some((receipt) => receipt.status === 'IqcRequested')
            ? 'IQC 대기'
            : item.receipts.length > 0
              ? '도착 등록'
              : '도착 대기';
  function toggleHistory(target: EventTarget | null) {
    if (item.receipts.length === 0 || (target instanceof Element && target.closest('button, input, label, a, select, textarea'))) {
      return;
    }
    setHistoryOpen((current) => !current);
  }
  return (
    <article className="material-purchase-row selected-export-row" data-completed={item.receiptCompleted}>
      <div
        className="material-purchase-main"
        role="row"
        tabIndex={item.receipts.length > 0 ? 0 : undefined}
        aria-expanded={item.receipts.length > 0 ? historyOpen : undefined}
        aria-controls={item.receipts.length > 0 ? historyId : undefined}
        onClick={(event) => toggleHistory(event.target)}
        onKeyDown={(event) => {
          const targetIsInteractive = event.target instanceof Element && event.target.closest('button, input, label, a, select, textarea');
          if ((event.key === 'Enter' || event.key === ' ') && !targetIsInteractive) {
            event.preventDefault();
            toggleHistory(event.target);
          }
        }}
      >
        <SelectionCheckbox checked={selected} disabled={selectionBusy} label={`${item.projectCode} ${item.orderItem ?? '품목'} 선택`} onChange={onSelectionChange} />
        <span className="material-purchase-supply">{item.supplyType === 'CustomerSupplied' ? item.customerSupplyOverdue ? '사급 지연' : '사급' : '도급'}</span>
        <span className="material-purchase-name">
          <strong>{item.orderItem ?? '발주품목 미입력'}</strong>
          <small>{activeReceipts.length > 0 ? `${activeReceipts.length}회 도착 · 행을 눌러 이력 ${historyOpen ? '접기' : '보기'}` : '도착 이력 없음'}</small>
        </span>
        <span>{item.supplyType === 'CustomerSupplied' ? '고객 제공' : item.supplierName ?? '-'}</span>
        <span>{formatQuantity(item.orderQuantity, item.orderUnit)}</span>
        <span>{item.expectedReceiptDate ?? '-'}</span>
        <span className="material-purchase-status" data-status={item.receiptCompleted ? 'completed' : (item.confirmedQuantity ?? 0) > 0 ? 'partial' : 'active'}>
          <strong>{latestStatus}</strong>
          <small>확정 {formatQuantity(item.confirmedQuantity, item.orderUnit)} · 잔여 {formatQuantity(item.remainingQuantity, item.orderUnit)}</small>
        </span>
        <button type="button" className="primary-button material-arrival-open" disabled={!canUpdate || item.arrivalsClosed} onClick={() => onAction({ kind: 'arrival', item })}>도착입력</button>
      </div>
      {item.receipts.length > 0 && historyOpen ? (
        <div className="material-receipt-history" id={historyId}>
          <header><strong>도착·IQC 이력</strong><span>{item.receipts.length}건</span></header>
          <div>
            {item.receipts.map((receipt) => (
              <button type="button" key={receipt.receiptId} className="material-receipt-line" onClick={() => onAction({ kind: 'receipt', item, receipt })}>
                <span>{receipt.arrivalDate}</span>
                <strong>{formatQuantity(receipt.quantity, receipt.unit)}</strong>
                <span>{receipt.note ?? '비고 없음'}</span>
                <StatusBadge status={receipt.status} />
                <i aria-hidden="true">열기</i>
              </button>
            ))}
          </div>
        </div>
      ) : null}
    </article>
  );
}

function MaterialActionPanel({ action, canUpdate, feedback, onClose, onOpenIqc, onOpenPending, onRun, developmentUserKey }: {
  action: MaterialAction;
  canUpdate: boolean;
  feedback: ActionFeedbackState | null;
  onClose: () => void;
  onOpenIqc: (requestId?: string) => void;
  onOpenPending: (pendingId: string) => void;
  onRun: (scope: string, operation: () => Promise<unknown>, success: string) => Promise<void>;
  developmentUserKey: string;
}) {
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState(action.item.orderUnit ?? 'EA');
  const [arrivalDate, setArrivalDate] = useState(new Date().toISOString().slice(0, 10));
  const [note, setNote] = useState('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  async function guarded(scope: string, operation: () => Promise<unknown>, success: string) {
    setSaving(true);
    try { await onRun(scope, operation, success); } finally { setSaving(false); }
  }

  if (action.kind === 'arrival') {
    async function submit(event: FormEvent) {
      event.preventDefault();
      await guarded(`material:${action.item.itemId}:arrival`, async () => {
        const response = await registerMaterialArrival(developmentUserKey, action.item.itemId, {
          quantity: Number(quantity),
          unit,
          arrivalDate,
          note: note.trim() || null
        });
        if (response.status === 'IqcRequested' && !response.iqcAttemptId) {
          throw new ApiError(409, '도착분은 저장됐지만 IQC 검사 업무 생성이 확인되지 않았습니다. 품질 검사함에서 누락 복구 후 다시 확인해 주세요.');
        }
        if (response.status !== 'IqcRequested' && response.status !== 'InspectionNotRequired') {
          throw new ApiError(409, '도착분의 다음 업무 상태를 확인하지 못했습니다. 목록을 새로고침해 주세요.');
        }
        return response;
      }, action.item.materialCategoryRequiresIqc === false
        ? '도착분을 저장하고 입고 확정 업무를 생성했습니다.'
        : '도착분 저장과 IQC 검사 업무 생성을 확인했습니다.');
    }
    return (
      <form className="material-action-form" onSubmit={submit}>
        <DsInputFlow title="도착분 입력" description="수량과 도착일만 확인하면 저장과 동시에 IQC 업무가 생성됩니다.">
          <DsInputSection number={1} title="대상 확인" description="선택한 프로젝트와 구매품목입니다.">
            <ActionContext item={action.item} />
            {action.item.orderQuantity === null || !action.item.orderUnit ? (
              <div className="material-action-notice" role="alert">
                <strong>구매팀 입력이 필요합니다.</strong>
                <span>{action.item.supplyType === 'CustomerSupplied' ? '제공 예정 수량·단위' : '발주 수량·단위'}를 구매 탭에서 먼저 입력해 주세요.</span>
              </div>
            ) : null}
          </DsInputSection>
          <DsInputSection number={2} title="도착 정보" description="이번에 실제 도착한 수량과 날짜를 입력합니다.">
            <div className="material-form-pair">
              <label><span>도착 수량</span><input data-autofocus inputMode="decimal" value={quantity} onChange={(event) => setQuantity(event.target.value)} required /></label>
              <label><span>단위</span><input value={unit} onChange={(event) => setUnit(event.target.value)} maxLength={20} required disabled={action.item.orderUnit !== null} /></label>
            </div>
            <label><span>도착일</span><input type="date" value={arrivalDate} onChange={(event) => setArrivalDate(event.target.value)} required /></label>
            <label><span>비고</span><textarea value={note} onChange={(event) => setNote(event.target.value)} placeholder="운송 상태나 확인 메모" /></label>
          </DsInputSection>
          <DsActionBar
            description="저장하면 별도 IQC 요청 버튼 없이 품질 담당자 검사함으로 전달됩니다."
            feedback={feedback ? <InlineActionFeedback feedback={feedback} /> : undefined}
          >
            <button type="button" onClick={onClose}>취소</button>
            <button className="primary-button" disabled={!canUpdate || saving || action.item.orderQuantity === null || !action.item.orderUnit}>{saving ? '저장 중' : '도착분 저장'}</button>
          </DsActionBar>
        </DsInputFlow>
      </form>
    );
  }

  const receipt = action.receipt;
  const pendingId = receipt.iqcAttempts.findLast((attempt) => attempt.pendingIssueId)?.pendingIssueId;
  const requestedAttemptId = receipt.iqcAttempts.findLast((attempt) => attempt.status === 'Requested')?.attemptId;
  return (
    <div className="material-action-form">
      <ActionContext item={action.item} receipt={receipt} />
      <ReceiptSteps status={receipt.status} />
      {receipt.note ? <div className="material-action-notice"><strong>도착 메모</strong><span>{receipt.note}</span></div> : null}
      {feedback ? <InlineActionFeedback feedback={feedback} /> : null}
      <div className="material-action-buttons material-action-buttons--stack">
        {receipt.status === 'Arrived' || receipt.status === 'InspectionNotRequired' ? <><textarea aria-label="취소 사유" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="취소 사유 3자 이상" /><button type="button" disabled={!canUpdate || saving || reason.trim().length < 3} onClick={() => void guarded(`material:${receipt.receiptId}:cancel`, () => cancelMaterialReceipt(developmentUserKey, receipt.receiptId, receipt.version, reason), '도착 등록을 취소했습니다.')}>도착 취소</button></> : null}
        {receipt.status === 'IqcRequested' ? <button type="button" className="primary-button" onClick={() => onOpenIqc(requestedAttemptId)}>이 IQC 검사 열기</button> : null}
        {receipt.status === 'FailedBlocked' && pendingId ? <button type="button" onClick={() => onOpenPending(pendingId)}>연결된 Pending 열기</button> : null}
        {receipt.status === 'Passed' || receipt.status === 'InspectionNotRequired' ? <button type="button" className="primary-button" disabled={!canUpdate || saving} onClick={() => void guarded(`material:${receipt.receiptId}:confirm`, () => confirmMaterialReceipt(developmentUserKey, receipt.receiptId, receipt.version), '입고를 확정했습니다. 전량 확정이면 품목도 자동 완료됩니다.')}>입고 확정</button> : null}
        {receipt.status === 'Confirmed' ? <div className="material-success-panel"><strong>입고 확정 완료</strong><span>{formatDateTime(receipt.confirmedAtUtc)}</span></div> : null}
        {receipt.status === 'Cancelled' ? <div className="material-action-notice"><strong>취소된 도착분</strong><span>{receipt.cancellationReason}</span></div> : null}
      </div>
    </div>
  );
}

function IqcInspector({ item, reason, feedback, disabled, onReason, onSubmit, onClose, onOpenPending }: {
  item: MaterialIqcQueueItem;
  reason: string;
  feedback: ActionFeedbackState | null;
  disabled: boolean;
  onReason: (value: string) => void;
  onSubmit: (result: 'Passed' | 'Failed') => Promise<void>;
  onClose: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  return (
    <div className="material-action-form iqc-inspector">
      <DsInputFlow title="IQC 판정" description="도착분을 확인하고 사유를 남긴 뒤 결과 버튼 하나를 누르세요.">
        <DsInputSection number={1} title="검사 대상" description="품목과 도착 차수·수량을 확인합니다.">
          <div className="iqc-inspector-context"><span>{item.projectCode}</span><h3>{item.orderItem ?? '발주품목 미입력'}</h3>{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={false} /> : null}<p>{item.projectTitle}</p></div>
          <dl className="material-item-meta"><div><dt>검사 차수</dt><dd>{item.attemptNumber}차</dd></div><div><dt>도착 수량</dt><dd>{formatQuantity(item.quantity, item.unit)}</dd></div></dl>
          <div className="iqc-check-guide"><strong>기본 확인</strong><span>품명·수량·외관·식별 정보를 확인한 뒤 판정하세요.</span></div>
          {item.pendingIssueId ? <button type="button" onClick={() => onOpenPending(item.pendingIssueId!)}>연결된 Pending 보기</button> : null}
        </DsInputSection>
        <DsInputSection number={2} title="판정 근거" description="확인 결과를 3자 이상 기록합니다.">
          <label><span>판정 사유</span><textarea data-autofocus data-field="iqcReason" aria-invalid={feedback?.tone === 'error'} value={reason} onChange={(event) => onReason(event.target.value)} placeholder="확인 결과를 3자 이상 기록" disabled={disabled} /></label>
        </DsInputSection>
        <DsActionBar description="합격은 입고 확정 업무로, 부적합은 Pending 조치로 자동 연결됩니다." feedback={feedback ? <InlineActionFeedback feedback={feedback} /> : undefined}>
          <button type="button" onClick={onClose}>닫기</button>
          <button type="button" className="iqc-fail-button" disabled={disabled} onClick={() => void onSubmit('Failed')}>부적합</button>
          <button type="button" className="primary-button" disabled={disabled} onClick={() => void onSubmit('Passed')}>합격</button>
        </DsActionBar>
      </DsInputFlow>
    </div>
  );
}

function SummaryButton({ label, value, active, tone, onClick }: { label: string; value: number; active: boolean; tone?: string; onClick: () => void }) {
  return <button type="button" data-active={active} data-tone={tone} onClick={onClick}><span>{label}</span><strong>{value}</strong></button>;
}

function ReceiptSteps({ status }: { status: MaterialReceiptStatus }) {
  const noInspection = status === 'InspectionNotRequired';
  const stages: Array<{ value: MaterialReceiptStatus; label: string }> = [
    { value: 'Arrived', label: '도착' },
    { value: 'IqcRequested', label: noInspection ? 'IQC 비대상' : 'IQC' },
    { value: 'Passed', label: noInspection ? '확정 대기' : '합격' },
    { value: 'Confirmed', label: '확정' }
  ];
  const current = noInspection ? 2 : status === 'FailedBlocked' ? 1 : status === 'Cancelled' ? 0 : stages.findIndex((stage) => stage.value === status);
  return <ol className="receipt-stepper" data-status={status}>{stages.map((stage, index) => <li key={stage.value} data-complete={index <= current}><i /><span>{stage.label}</span></li>)}</ol>;
}

function StatusBadge({ status }: { status: MaterialReceiptStatus }) {
  return <span className="material-status-badge" data-status={status}>{receiptStatusLabel(status)}</span>;
}

function SupplyBadge({ overdue }: { overdue: boolean }) {
  return <span className="customer-supply-badge" data-overdue={overdue}>{overdue ? '사급 · 제공 지연' : '사급 · 고객 제공'}</span>;
}

function ActionContext({ item, receipt }: { item: MaterialReceivingItem; receipt?: MaterialReceipt }) {
  return <div className="material-action-context"><span>{item.projectCode}</span><h3>{item.orderItem ?? '발주품목 미입력'}</h3>{item.supplyType === 'CustomerSupplied' ? <SupplyBadge overdue={item.customerSupplyOverdue} /> : null}<p>{item.projectTitle} · {item.supplyType === 'CustomerSupplied' ? '고객 제공' : item.supplierName ?? '업체 미입력'}</p>{receipt ? <strong>{formatQuantity(receipt.quantity, receipt.unit)} · {receipt.arrivalDate}</strong> : null}</div>;
}

function MaterialLoading() {
  return <div className="material-loading" aria-label="불러오는 중"><i /><i /><i /></div>;
}

function InlineActionFeedback({ feedback }: { feedback: ActionFeedbackState }) {
  return (
    <p
      className="action-feedback material-inline-feedback"
      data-tone={feedback.tone}
      role={feedback.tone === 'error' ? 'alert' : 'status'}
      aria-live={feedback.tone === 'error' ? 'assertive' : 'polite'}
      tabIndex={feedback.tone === 'error' || feedback.tone === 'partial' ? -1 : undefined}
    >
      {feedback.message}
    </p>
  );
}

function receiptStatusLabel(status: MaterialReceiptStatus) {
  return ({ Arrived: '도착 등록', IqcRequested: 'IQC 대기', Passed: 'IQC 합격', InspectionNotRequired: 'IQC 비대상 · 확정 대기', FailedBlocked: '부적합 차단', Confirmed: '입고 확정', Cancelled: '취소' } as const)[status];
}

function formatQuantity(quantity: number | null | undefined, unit: string | null | undefined) {
  return quantity == null ? '-' : `${quantity.toLocaleString('ko-KR', { maximumFractionDigits: 3 })} ${unit ?? ''}`.trim();
}

function formatDateTime(value: string | null) {
  if (!value) return '-';
  return new Intl.DateTimeFormat('ko-KR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    const fieldMessage = error.errors ? Object.values(error.errors).flat()[0] : null;
    return fieldMessage ?? error.message;
  }
  return fallback;
}
