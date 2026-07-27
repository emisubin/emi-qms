import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  checkManufacturingStep,
  completeManufacturingAssemblyBatch,
  completeManufacturingExecution,
  getManufacturingPanel,
  getManufacturingQueue,
  listManufacturingActionDepartments,
  resumeManufacturingExecution,
  startManufacturingExecution,
  stopManufacturingExecution
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsActionBar, DsInputFlow, DsInputSection } from './design-system';
import { MobileSheet } from './MobileSheet';
import { OperationalProjectDashboard } from './OperationalProjectDashboard';
import type {
  ManufacturingActionDepartment,
  ManufacturingExecutionDetail,
  ManufacturingPanel,
  ManufacturingQueueResponse,
  ManufacturingStatus,
  StopManufacturingRequest
} from './manufacturing';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type QueueState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: ManufacturingQueueResponse }
  | { kind: 'error'; message: string };

type DetailState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; data: ManufacturingExecutionDetail }
  | { kind: 'error'; message: string };

type Feedback = { tone: 'success' | 'error' | 'info'; message: string };
type OperationReceipt = { fingerprint: string; operationId: string };
type StopReason = StopManufacturingRequest['reasonCode'];

const stopReasonOptions: Array<{ value: StopReason; label: string }> = [
  { value: 'Material', label: '자재·부품 문제' },
  { value: 'Staffing', label: '인력 부족' },
  { value: 'WorkUnavailable', label: '작업 불가' },
  { value: 'Other', label: '기타' }
];

export function ManufacturingPage({
  developmentUserKey,
  canMutate,
  initialProjectId,
  initialPanelId,
  onBack,
  onOpenProject,
  onOpenPending
}: {
  developmentUserKey: string;
  canMutate: boolean;
  initialProjectId?: string;
  initialPanelId?: string;
  onBack: () => void;
  onOpenProject?: (projectId: string) => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [queueState, setQueueState] = useState<QueueState>({ kind: 'loading' });
  const [detailState, setDetailState] = useState<DetailState>({ kind: 'idle' });
  const [selectedProjectId, setSelectedProjectId] = useState(initialProjectId ?? '');
  const [selectedPanelId, setSelectedPanelId] = useState(initialPanelId ?? '');
  const [departments, setDepartments] = useState<ManufacturingActionDepartment[]>([]);
  const [savingAction, setSavingAction] = useState('');
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [batchFeedback, setBatchFeedback] = useState<Feedback | null>(null);
  const [batchOpen, setBatchOpen] = useState(false);
  const [stopOpen, setStopOpen] = useState(false);
  const [stopReason, setStopReason] = useState<StopReason>('Material');
  const [stopDescription, setStopDescription] = useState('');
  const [stopDepartment, setStopDepartment] = useState('');
  const [stopAssignee, setStopAssignee] = useState('');
  const stopTriggerRef = useRef<HTMLButtonElement>(null);
  const batchTriggerRef = useRef<HTMLButtonElement>(null);
  const operationReceipts = useRef<Record<string, OperationReceipt>>({});
  const mutationInFlightRef = useRef(false);

  const writeLocation = useCallback((projectId: string, panelId?: string) => {
    if (typeof window === 'undefined') return;
    const params = new URLSearchParams();
    if (projectId) params.set('project', projectId);
    if (panelId) params.set('panel', panelId);
    window.history.replaceState(null, '', `/manufacturing/work${params.size ? `?${params.toString()}` : ''}`);
  }, []);

  const loadQueue = useCallback(async (preferredProjectId?: string, preferredPanelId?: string) => {
    setQueueState({ kind: 'loading' });
    try {
      const data = await getManufacturingQueue(developmentUserKey, initialProjectId);
      setQueueState({ kind: 'ready', data });
      if (!initialProjectId) {
        setSelectedProjectId('');
        setSelectedPanelId('');
        return;
      }
      const requestedProject = preferredProjectId ?? selectedProjectId ?? initialProjectId;
      const project = data.projects.find((item) => item.projectId === requestedProject)
        ?? data.projects.find((item) => item.blockedCount > 0 || item.inProgressCount > 0 || item.readyCount > 0)
        ?? data.projects[0];
      const requestedPanel = preferredPanelId ?? selectedPanelId ?? initialPanelId;
      const panel = project?.panels.find((item) => item.panelId === requestedPanel)
        ?? project?.panels.find((item) => item.status === 'Blocked' || item.status === 'InProgress' || item.status === 'Ready')
        ?? project?.panels[0];
      setSelectedProjectId(project?.projectId ?? '');
      setSelectedPanelId(panel?.panelId ?? '');
      if (project) writeLocation(project.projectId, panel?.panelId);
    } catch (error) {
      setQueueState({ kind: 'error', message: errorMessage(error, '제조 작업 목록을 불러오지 못했습니다.') });
    }
  }, [developmentUserKey, initialPanelId, initialProjectId, selectedPanelId, selectedProjectId, writeLocation]);

  const loadDetail = useCallback(async (panelId: string) => {
    if (!panelId) {
      setDetailState({ kind: 'idle' });
      return;
    }
    setDetailState({ kind: 'loading' });
    try {
      setDetailState({ kind: 'ready', data: await getManufacturingPanel(developmentUserKey, panelId) });
    } catch (error) {
      setDetailState({ kind: 'error', message: errorMessage(error, '패널 제조 상세를 불러오지 못했습니다.') });
    }
  }, [developmentUserKey]);

  useEffect(() => {
    queueMicrotask(() => void loadQueue(initialProjectId, initialPanelId));
  // Initial deep-link selection is intentionally applied once per identity change.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [developmentUserKey, initialPanelId, initialProjectId]);

  useEffect(() => {
    queueMicrotask(() => void loadDetail(selectedPanelId));
  }, [loadDetail, selectedPanelId]);

  useEffect(() => {
    if (!canMutate) return;
    void listManufacturingActionDepartments(developmentUserKey)
      .then(setDepartments)
      .catch(() => setDepartments([]));
  }, [canMutate, developmentUserKey]);

  const projects = queueState.kind === 'ready' ? queueState.data.projects : [];
  const selectedProject = projects.find((project) => project.projectId === selectedProjectId) ?? null;
  const manufacturingVisibleIds = selectedProject?.panels.map((item) => item.panelId) ?? [];
  const manufacturingSelection = useSelectedRows(manufacturingVisibleIds);
  const selectedManufacturingPanels = selectedProject?.panels
    .filter((item) => manufacturingSelection.selectedIds.has(item.panelId)) ?? [];
  const assemblyBatchTargets = selectedManufacturingPanels.filter(isAssemblyBatchEligible);
  const assemblyBatchExcluded = selectedManufacturingPanels.filter((item) => !isAssemblyBatchEligible(item));
  const assemblyBatchStepCount = assemblyBatchTargets.length;
  const selectedPanel = selectedProject?.panels.find((panel) => panel.panelId === selectedPanelId) ?? null;
  const detail = detailState.kind === 'ready' ? detailState.data : null;
  const panel = detail?.panel ?? selectedPanel;
  const selectedDepartment = departments.find((item) => item.departmentCode === stopDepartment) ?? null;
  const allStepsChecked = Boolean(detail?.steps.length) && detail!.steps.every((step) => step.checked);
  const nextStep = detail?.steps.find((step) => !step.checked) ?? null;
  const mayMutatePanel = canMutate && panel?.canMutate === true;

  function operationId(action: string, fingerprint: string) {
    const current = operationReceipts.current[action];
    if (current?.fingerprint === fingerprint) return current.operationId;
    const next = { fingerprint, operationId: crypto.randomUUID() };
    operationReceipts.current[action] = next;
    return next.operationId;
  }

  async function refreshAfterMutation(projectId: string, panelId: string) {
    const [queue, panelDetail] = await Promise.all([
      getManufacturingQueue(developmentUserKey, projectId),
      getManufacturingPanel(developmentUserKey, panelId)
    ]);
    setQueueState({ kind: 'ready', data: queue });
    setDetailState({ kind: 'ready', data: panelDetail });
    setSelectedProjectId(projectId);
    setSelectedPanelId(panelId);
  }

  async function mutate(
    action: string,
    fingerprint: string,
    request: (operationId: string) => Promise<{ projectId: string; panelId: string }>,
    successMessage: string
  ) {
    if (mutationInFlightRef.current) {
      return false;
    }
    mutationInFlightRef.current = true;
    const receiptId = operationId(action, fingerprint);
    setSavingAction(action);
    setFeedback({ tone: 'info', message: '제조 단계를 저장하는 중입니다. 완료될 때까지 잠시 기다려 주세요.' });
    try {
      const result = await request(receiptId);
      delete operationReceipts.current[action];
      await refreshAfterMutation(result.projectId, result.panelId);
      setFeedback({ tone: 'success', message: successMessage });
      return true;
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '제조 작업을 처리하지 못했습니다.') });
      return false;
    } finally {
      mutationInFlightRef.current = false;
      setSavingAction('');
    }
  }

  function selectPanel(nextPanel: ManufacturingPanel) {
    if (mutationInFlightRef.current) return;
    setSelectedPanelId(nextPanel.panelId);
    setFeedback(null);
    writeLocation(selectedProjectId, nextPanel.panelId);
  }

  async function start() {
    if (!selectedProject || !panel || !mayMutatePanel) return;
    await mutate(
      'start',
      `${selectedProject.projectId}|${panel.panelId}`,
      (id) => startManufacturingExecution(developmentUserKey, {
        operationId: id,
        projectId: selectedProject.projectId,
        panelId: panel.panelId
      }),
      `${panel.displayCode} 제조와 단계별 LQC를 함께 시작했습니다.`
    );
  }

  async function checkNextStep() {
    if (!panel?.executionId || !nextStep || !mayMutatePanel) return;
    await mutate(
      'check-step',
      `${panel.executionId}|${nextStep.stepId}|${panel.version}`,
      (id) => checkManufacturingStep(developmentUserKey, panel.executionId!, {
        operationId: id,
        stepId: nextStep.stepId,
        expectedVersion: panel.version
      }),
      `${nextStep.stepName} 단계를 확인했습니다.`
    );
  }

  async function resume() {
    if (!panel?.executionId || !mayMutatePanel) return;
    await mutate(
      'resume',
      `${panel.executionId}|${panel.version}`,
      (id) => resumeManufacturingExecution(developmentUserKey, panel.executionId!, {
        operationId: id,
        expectedVersion: panel.version
      }),
      `${panel.displayCode} 제조 작업을 재개했습니다.`
    );
  }

  async function complete() {
    if (!panel?.executionId || !allStepsChecked || !mayMutatePanel) return;
    await mutate(
      'complete',
      `${panel.executionId}|${panel.version}`,
      (id) => completeManufacturingExecution(developmentUserKey, panel.executionId!, {
        operationId: id,
        expectedVersion: panel.version
      }),
      `${panel.displayCode} 제조를 완료했습니다. LQC도 합격이면 OQC가 자동으로 열립니다.`
    );
  }

  async function submitStop() {
    if (!panel?.executionId || !stopDepartment || stopDescription.trim().length < 10 || !mayMutatePanel) return;
    const fingerprint = [panel.executionId, stopReason, stopDescription.trim(), stopDepartment, stopAssignee, panel.version].join('|');
    const completed = await mutate(
      'stop',
      fingerprint,
      (id) => stopManufacturingExecution(developmentUserKey, panel.executionId!, {
        operationId: id,
        reasonCode: stopReason,
        description: stopDescription.trim(),
        actionDepartmentCode: stopDepartment,
        assigneeUserId: stopAssignee || null,
        expectedVersion: panel.version
      }),
      `${panel.displayCode} 작업을 중단하고 긴급 Pending을 생성했습니다.`
    );
    if (completed) {
      setStopOpen(false);
      setStopDescription('');
      setStopAssignee('');
    }
  }

  async function completeAssemblyBatch() {
    if (!selectedProject || assemblyBatchTargets.length === 0 || mutationInFlightRef.current) return;
    mutationInFlightRef.current = true;
    const panels = [...assemblyBatchTargets]
      .sort((left, right) => left.panelId.localeCompare(right.panelId))
      .map((item) => ({
        panelId: item.panelId,
        executionId: item.executionId!,
        expectedVersion: item.version
      }));
    const fingerprint = panels
      .map((item) => `${item.panelId}|${item.executionId}|${item.expectedVersion}`)
      .join(',');
    const receiptId = operationId('assembly-batch', `${selectedProject.projectId}|${fingerprint}`);
    setSavingAction('assembly-batch');
    manufacturingSelection.setBusy(true);
    setBatchFeedback({ tone: 'info', message: `${panels.length}면의 조립 단계를 저장하는 중입니다.` });
    try {
      const result = await completeManufacturingAssemblyBatch(developmentUserKey, {
        operationId: receiptId,
        projectId: selectedProject.projectId,
        panels
      });
      delete operationReceipts.current['assembly-batch'];
      const queue = await getManufacturingQueue(developmentUserKey, selectedProject.projectId);
      setQueueState({ kind: 'ready', data: queue });
      if (selectedPanelId) {
        setDetailState({ kind: 'ready', data: await getManufacturingPanel(developmentUserKey, selectedPanelId) });
      }
      manufacturingSelection.clear();
      setBatchOpen(false);
      setBatchFeedback({
        tone: 'success',
        message: `${result.completedPanelCount}면의 조립 단계만 완료했습니다. 다른 제조 단계는 유지됩니다.`
      });
    } catch (error) {
      setBatchFeedback({
        tone: 'error',
        message: errorMessage(error, '선택 패널의 조립 단계를 처리하지 못했습니다. 선택은 유지됩니다.')
      });
    } finally {
      mutationInFlightRef.current = false;
      setSavingAction('');
      manufacturingSelection.setBusy(false);
    }
  }

  if (queueState.kind === 'ready' && !initialProjectId) {
    const dashboardProjects = queueState.data.projects;
    const totals = dashboardProjects.reduce((summary, project) => ({
      ready: summary.ready + project.readyCount,
      progress: summary.progress + project.inProgressCount,
      blocked: summary.blocked + project.blockedCount,
      completed: summary.completed + project.completedCount
    }), { ready: 0, progress: 0, blocked: 0, completed: 0 });
    return (
      <OperationalProjectDashboard
        testId="manufacturing-dashboard"
        eyebrow="MANUFACTURING · PROJECT QUEUE"
        title="제조 프로젝트"
        description="프로젝트를 선택하면 그 프로젝트의 패널 제조 작업만 표시합니다."
        unitLabel="패널"
        metrics={[
          { label: '착수 대기', value: totals.ready, helper: '제조 시작 전 패널' },
          { label: '제조 중', value: totals.progress, helper: '현재 작업 중' },
          { label: '중단', value: totals.blocked, helper: 'Pending 확인 필요', tone: 'warning' },
          { label: '완료', value: totals.completed, helper: '제조 완료 패널', tone: 'positive' }
        ]}
        projects={dashboardProjects.map((project) => ({
          projectId: project.projectId,
          projectCode: project.projectCode,
          projectTitle: project.projectTitle,
          totalCount: project.panels.length,
          readyCount: project.readyCount,
          inProgressCount: project.inProgressCount,
          blockedCount: project.blockedCount,
          completedCount: project.completedCount,
          detail: `제조 진행률 ${project.panels.length ? Math.round((project.panels.reduce((sum, panel) => sum + panel.checkedStepCount, 0) / project.panels.reduce((sum, panel) => sum + Math.max(1, panel.totalStepCount), 0)) * 100) : 0}%`
        }))}
        emptyMessage="제조 대상 프로젝트가 없습니다."
        onBack={onBack}
        onOpenProject={(projectId) => onOpenProject?.(projectId)}
      />
    );
  }

  return (
    <section
      className={isMobile ? 'page-surface manufacturing-page manufacturing-page--mobile' : 'page-surface manufacturing-page'}
      data-testid="manufacturing-page"
    >
      <header className="manufacturing-hero">
        <div>
          <p className="eyebrow">SHOP FLOOR · PANEL FLOW</p>
          <h2>제조 작업</h2>
          <p>패널 제조와 단계별 LQC를 함께 진행하고, 둘 다 끝나면 OQC로 자동 인계합니다.</p>
        </div>
        <button type="button" className="manufacturing-back" onClick={onBack}>제조 프로젝트</button>
        <span className="manufacturing-hero-disc" aria-hidden="true" />
        <span className="manufacturing-hero-angle" aria-hidden="true" />
      </header>

      {queueState.kind === 'loading' ? <ManufacturingLoading /> : null}
      {queueState.kind === 'error' ? (
        <div className="manufacturing-state" role="alert">
          <strong>제조 작업을 확인할 수 없습니다.</strong>
          <span>{queueState.message}</span>
          <button type="button" onClick={() => void loadQueue()}>다시 불러오기</button>
        </div>
      ) : null}
      {queueState.kind === 'ready' && projects.length === 0 ? (
        <div className="manufacturing-state">
          <strong>제조 대기 패널이 없습니다.</strong>
          <span>생산관리에서 패널을 제조 투입 요청하면 이곳에 업무가 나타납니다.</span>
        </div>
      ) : null}

      {queueState.kind === 'ready' && projects.length > 0 ? (
        <div className="manufacturing-workspace manufacturing-workspace--single-project">
          {selectedProject ? (
          <aside className="manufacturing-project-queue manufacturing-panel-rail" aria-label={`${selectedProject.projectTitle} 패널 목록`}>
            <div className="manufacturing-section-heading">
              <p>PANEL QUEUE</p>
              <strong>{selectedProject.panels.length}면</strong>
            </div>
            <div className="manufacturing-panel-list">
              {selectedProject.panels.map((item) => (
                <div className="manufacturing-panel-selectable" key={item.panelId}>
                  <SelectionCheckbox checked={manufacturingSelection.selectedIds.has(item.panelId)} disabled={manufacturingSelection.busy || Boolean(savingAction)} label={`${item.displayCode} 선택`} onChange={(checked) => manufacturingSelection.toggle(item.panelId, checked)} />
                  <button
                    type="button"
                    className="manufacturing-panel-chip"
                    data-status={item.status.toLowerCase()}
                    data-active={item.panelId === selectedPanelId}
                    disabled={Boolean(savingAction)}
                    onClick={() => selectPanel(item)}
                  >
                    <span className="manufacturing-status-shape" aria-hidden="true">{item.status === 'Completed' ? '✓' : ''}</span>
                    <span><strong>{item.displayCode}</strong><small>{statusLabel(item.status)} · {item.checkedStepCount}/{item.totalStepCount || 4}</small></span>
                  </button>
                </div>
              ))}
            </div>
          </aside>
          ) : null}

          {selectedProject ? (
            <main className="manufacturing-panel-area">
              <section className="manufacturing-project-summary">
                <div>
                  <p>{selectedProject.projectCode}</p>
                  <h3>{selectedProject.projectTitle}</h3>
                </div>
                <div className="manufacturing-metrics" aria-label="제조 진행 요약">
                  <Metric label="대기" value={selectedProject.readyCount} tone="ready" />
                  <Metric label="진행" value={selectedProject.inProgressCount} tone="progress" />
                  <Metric label="중단" value={selectedProject.blockedCount} tone="blocked" />
                  <Metric label="완료" value={selectedProject.completedCount} tone="completed" />
                </div>
              </section>

              <SelectedExportTray
                developmentUserKey={developmentUserKey}
                screen="manufacturing"
                visibleIds={manufacturingVisibleIds}
                selectedIds={manufacturingSelection.selectedIds}
                allSelected={manufacturingSelection.allSelected}
                busy={manufacturingSelection.busy}
                filters={{ projectId: selectedProject.projectId }}
                onBusyChange={manufacturingSelection.setBusy}
                onToggleAll={manufacturingSelection.toggleAll}
                onClear={manufacturingSelection.clear}
              />
              {canMutate ? (
                <>
                  <section className="manufacturing-batch-bar" aria-label="선택 패널 일괄 작업">
                    <div>
                      <strong>조립 단계 일괄 완료</strong>
                      <small>
                        선택 {selectedManufacturingPanels.length}면 · 처리 가능 {assemblyBatchTargets.length}면
                        {assemblyBatchExcluded.length > 0 ? ` · 제외 ${assemblyBatchExcluded.length}면` : ''}
                      </small>
                    </div>
                    <button
                      ref={batchTriggerRef}
                      type="button"
                      className="primary-button"
                      disabled={assemblyBatchTargets.length === 0 || manufacturingSelection.busy || Boolean(savingAction)}
                      onClick={() => setBatchOpen(true)}
                    >
                      {savingAction === 'assembly-batch' ? '처리 중…' : `선택 패널 조립 단계 완료 (${assemblyBatchTargets.length}면)`}
                    </button>
                  </section>
                  {batchFeedback ? (
                    <p className="manufacturing-feedback manufacturing-batch-feedback" data-tone={batchFeedback.tone} role="status">
                      {batchFeedback.message}
                    </p>
                  ) : null}
                </>
              ) : null}

              {detailState.kind === 'loading' || detailState.kind === 'idle' ? <ManufacturingDetailLoading /> : null}
              {detailState.kind === 'error' ? (
                <div className="manufacturing-state" role="alert">
                  <strong>패널 상세를 확인할 수 없습니다.</strong>
                  <span>{detailState.message}</span>
                  <button type="button" onClick={() => void loadDetail(selectedPanelId)}>다시 불러오기</button>
                </div>
              ) : null}

              {detail && panel ? (
                <div className="manufacturing-detail-grid">
                  <section className="manufacturing-focus-card" data-status={panel.status.toLowerCase()} aria-busy={Boolean(savingAction)}>
                    <DsInputFlow title={`${panel.displayCode} 제조 입력`} description="현재 단계만 확인 버튼으로 처리하면 다음 단계가 자동으로 열립니다.">
                      <DsInputSection number={1} title="패널과 진행 상태" description="선택한 패널과 현재 제조 진행률입니다.">
                        <header>
                          <div className="manufacturing-focus-symbol" aria-hidden="true">
                            <span>{panel.status === 'Completed' ? '✓' : panel.checkedStepCount}</span>
                          </div>
                          <div>
                            <p>{statusLabel(panel.status)} · {panel.workflowStage}</p>
                            <h3>{panel.displayCode}</h3>
                            <span>{panel.panelName ?? '패널명 미입력'}</span>
                          </div>
                          <strong className="manufacturing-progress-count">{panel.checkedStepCount}/{panel.totalStepCount || 4}</strong>
                        </header>
                        <div className="manufacturing-progress-track" aria-label={`제조 단계 ${panel.checkedStepCount}/${panel.totalStepCount || 4}`}>
                          <span style={{ width: `${Math.min(100, (panel.checkedStepCount / (panel.totalStepCount || 4)) * 100)}%` }} />
                        </div>
                      </DsInputSection>
                      <DsInputSection number={2} title="현재 단계 처리" description={nextStep ? `${nextStep.stepName} 단계만 확인하면 됩니다.` : '현재 패널의 제조 상태를 확인하세요.'}>
                        {panel.status === 'Ready' ? (
                          <div className="manufacturing-ready-copy">
                            <strong>제조 투입 요청됨 · {panel.kittingCompleted ? '키팅 완료' : '키팅 미보고'}</strong>
                            <span>키팅은 참고 정보입니다. 작업지시와 도면을 확인한 뒤 이 패널의 실행을 시작하세요.</span>
                          </div>
                        ) : (
                          <ol className="manufacturing-step-list">
                            {detail.steps.map((step) => (
                              <li key={step.stepId} data-checked={step.checked} data-next={step.stepId === nextStep?.stepId}>
                                <span>{step.checked ? '✓' : step.sequenceNumber}</span>
                                <div>
                                  <strong>{step.stepName}</strong>
                                  <small>{step.checked
                                    ? `${step.checkedByDisplayName ?? '담당자'} · ${formatOperationalTime(step.checkedAtUtc)}`
                                    : step.stepId === nextStep?.stepId ? '다음 확인 단계' : '앞 단계를 먼저 확인하세요'}</small>
                                </div>
                              </li>
                            ))}
                          </ol>
                        )}
                        {!mayMutatePanel ? <p className="manufacturing-readonly-note">조회 전용입니다. 제조 시작·체크·중단·완료는 제조 담당 권한이 필요합니다.</p> : null}
                      </DsInputSection>
                      {mayMutatePanel ? (
                        <DsActionBar
                          description="검은색 버튼은 지금 할 수 있는 다음 작업 하나만 표시합니다."
                          feedback={feedback ? <p className="manufacturing-feedback" data-tone={feedback.tone} role="status">{feedback.message}</p> : undefined}
                        >
                          {panel.status === 'Ready' ? (
                            <button type="button" className="primary-button" disabled={Boolean(savingAction)} onClick={() => void start()}>
                              {savingAction === 'start' ? '시작 중…' : '제조 시작'}
                            </button>
                          ) : null}
                          {panel.status === 'InProgress' && nextStep ? (
                            <button type="button" className="primary-button" disabled={Boolean(savingAction)} onClick={() => void checkNextStep()}>
                              {savingAction === 'check-step' ? '확인 중…' : `${nextStep.sequenceNumber}단계 확인`}
                            </button>
                          ) : null}
                          {panel.status === 'InProgress' && allStepsChecked ? (
                            <button type="button" className="primary-button manufacturing-complete" disabled={Boolean(savingAction)} onClick={() => void complete()}>
                              {savingAction === 'complete' ? '완료 중…' : '제조 완료'}
                            </button>
                          ) : null}
                          {panel.status === 'InProgress' ? (
                            <button ref={stopTriggerRef} type="button" className="manufacturing-stop" disabled={Boolean(savingAction)} onClick={() => {
                              if (!mutationInFlightRef.current) setStopOpen(true);
                            }}>
                              작업 중단
                            </button>
                          ) : null}
                          {panel.status === 'Blocked' ? (
                            <button type="button" className="primary-button" disabled={Boolean(savingAction)} onClick={() => void resume()}>
                              {savingAction === 'resume' ? '확인 중…' : 'Pending 확인 후 재개'}
                            </button>
                          ) : null}
                        </DsActionBar>
                      ) : feedback ? <p className="manufacturing-feedback" data-tone={feedback.tone} role="status">{feedback.message}</p> : null}
                    </DsInputFlow>

                    {panel.status === 'Blocked' && panel.activePendingId ? (
                      <button type="button" className="manufacturing-pending-link" onClick={() => onOpenPending(panel.activePendingId!)}>
                        <span>긴급 Pending #{panel.activePendingNumber}</span>
                        <strong>{departmentName(panel.actionDepartmentCode, departments)} 조치 확인 →</strong>
                      </button>
                    ) : null}
                  </section>

                  <aside className="manufacturing-timeline">
                    <div className="manufacturing-section-heading">
                      <p>EXECUTION LOG</p>
                      <strong>작업 이력</strong>
                    </div>
                    {detail.events.length === 0 ? (
                      <div className="manufacturing-empty-log">제조를 시작하면 실행 이력이 여기에 쌓입니다.</div>
                    ) : (
                      <ol>
                        {[...detail.events].reverse().map((event) => (
                          <li key={event.eventId} data-event={event.eventType.toLowerCase()}>
                            <span aria-hidden="true" />
                            <div>
                              <strong>{event.eventLabel}</strong>
                              {event.stopDescription ? <p>{event.stopDescription}</p> : null}
                              <small>{event.actorDisplayName} · {formatOperationalTime(event.createdAtUtc)}</small>
                            </div>
                          </li>
                        ))}
                      </ol>
                    )}
                  </aside>
                </div>
              ) : null}
            </main>
          ) : null}
        </div>
      ) : null}

      <MobileSheet
        open={batchOpen}
        title="조립 단계 일괄 완료"
        eyebrow="MANUFACTURING · BATCH"
        description="현장에서 함께 작업한 패널의 조립 입력을 한 번에 반영합니다."
        onClose={() => {
          if (!mutationInFlightRef.current) setBatchOpen(false);
        }}
        triggerRef={batchTriggerRef}
        footer={(
          <>
            <button type="button" disabled={Boolean(savingAction)} onClick={() => setBatchOpen(false)}>취소</button>
            <button
              type="button"
              className="primary-button"
              data-autofocus
              disabled={assemblyBatchTargets.length === 0 || Boolean(savingAction)}
              onClick={() => void completeAssemblyBatch()}
            >
              {savingAction === 'assembly-batch' ? '저장 중…' : `${assemblyBatchTargets.length}면 조립 단계 완료`}
            </button>
          </>
        )}
      >
        <div className="manufacturing-batch-confirm">
          <div className="manufacturing-batch-counts">
            <span><small>선택</small><strong>{selectedManufacturingPanels.length}면</strong></span>
            <span><small>처리 가능</small><strong>{assemblyBatchTargets.length}면</strong></span>
            <span><small>조립 처리</small><strong>{assemblyBatchStepCount}단계</strong></span>
            <span><small>제외</small><strong>{assemblyBatchExcluded.length}면</strong></span>
          </div>
          <p className="manufacturing-batch-warning">
            선택한 패널의 조립 단계만 완료 처리합니다. 다른 제조 단계는 그대로 유지됩니다.
          </p>
          <section>
            <h3>처리 대상</h3>
            <ul>
              {assemblyBatchTargets.map((item) => (
                <li key={item.panelId}><strong>{item.displayCode}</strong><span>{item.panelName ?? '패널명 미입력'}</span></li>
              ))}
            </ul>
          </section>
          {assemblyBatchExcluded.length > 0 ? (
            <section>
              <h3>이번 처리에서 제외</h3>
              <ul>
                {assemblyBatchExcluded.map((item) => (
                  <li key={item.panelId}>
                    <strong>{item.displayCode}</strong>
                    <span>{assemblyBatchExclusionReason(item, canMutate)}</span>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
          {batchFeedback?.tone === 'error' ? (
            <p className="manufacturing-feedback" data-tone="error" role="alert">{batchFeedback.message}</p>
          ) : null}
        </div>
      </MobileSheet>

      <MobileSheet
        open={stopOpen}
        title="제조 작업 중단"
        eyebrow="BLOCK & ESCALATE"
        description="중단 사유와 조치 부서를 남기면 패널에 긴급 Pending이 연결됩니다."
        onClose={() => setStopOpen(false)}
        triggerRef={stopTriggerRef}
      >
        <div className="manufacturing-stop-form">
          <label>
            <span>중단 사유</span>
            <select value={stopReason} onChange={(event) => setStopReason(event.target.value as StopReason)}>
              {stopReasonOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          <label>
            <span>상세 설명 <small>{stopDescription.trim().length}/1000</small></span>
            <textarea
              value={stopDescription}
              maxLength={1000}
              rows={4}
              placeholder="현장에서 확인한 문제와 필요한 조치를 10자 이상 입력하세요."
              onChange={(event) => setStopDescription(event.target.value)}
            />
          </label>
          <label>
            <span>조치 담당 부서</span>
            <select value={stopDepartment} onChange={(event) => { setStopDepartment(event.target.value); setStopAssignee(''); }}>
              <option value="">부서 선택</option>
              {departments.map((item) => <option key={item.departmentCode} value={item.departmentCode}>{item.departmentName}</option>)}
            </select>
          </label>
          <label>
            <span>담당자 <small>선택</small></span>
            <select value={stopAssignee} disabled={!selectedDepartment} onChange={(event) => setStopAssignee(event.target.value)}>
              <option value="">부서 공용 Pending으로 생성</option>
              {selectedDepartment?.assignees.map((owner) => <option key={owner.userId} value={owner.userId}>{owner.displayName}</option>)}
            </select>
          </label>
          <button
            type="button"
            className="primary-button manufacturing-stop-submit"
            disabled={savingAction === 'stop' || !stopDepartment || stopDescription.trim().length < 10}
            onClick={() => void submitStop()}
          >
            {savingAction === 'stop' ? 'Pending 생성 중…' : '작업 중단 · 긴급 Pending 생성'}
          </button>
        </div>
      </MobileSheet>
    </section>
  );
}

function Metric({ label, value, tone }: { label: string; value: number; tone: string }) {
  return <span data-tone={tone}><strong>{value}</strong><small>{label}</small></span>;
}

function ManufacturingLoading() {
  return <div className="manufacturing-loading" role="status"><span /><span /><span /><p>제조 queue를 불러오는 중입니다.</p></div>;
}

function ManufacturingDetailLoading() {
  return <div className="manufacturing-detail-loading" role="status"><span /><div><i /><i /><i /><i /></div></div>;
}

function statusLabel(status: ManufacturingStatus) {
  switch (status) {
    case 'Ready': return '시작 전';
    case 'InProgress': return '진행 중';
    case 'Blocked': return '중단';
    case 'Completed': return '완료';
    case 'Cancelled': return '취소';
    default: return status;
  }
}

function isAssemblyBatchEligible(panel: ManufacturingPanel) {
  return panel.canMutate
    && panel.status === 'InProgress'
    && Boolean(panel.executionId)
    && panel.version > 0
    && panel.assemblyStepSequence !== null
    && panel.assemblyStepSequence !== undefined
    && panel.assemblyStepChecked === false;
}

function assemblyBatchExclusionReason(panel: ManufacturingPanel, canMutate: boolean) {
  if (!canMutate || !panel.canMutate) return '조회 전용 사용자';
  if (panel.status === 'Blocked') return '중단 Pending 처리 필요';
  if (panel.status === 'Ready') return '제조 시작 전';
  if (panel.status === 'Completed' || panel.status === 'Cancelled') return '진행 중 제조 실행 아님';
  if (!panel.executionId || panel.version < 1) return '제조 실행 정보 없음';
  if (panel.assemblyStepChecked === true) return '조립 단계 이미 완료';
  return '조립 단계 식별 불가';
}

function formatOperationalTime(value: string | null) {
  if (!value) return '시간 비공개';
  return new Intl.DateTimeFormat('ko-KR', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function departmentName(code: string | null, departments: ManufacturingActionDepartment[]) {
  if (!code) return '담당 부서';
  return departments.find((item) => item.departmentCode === code)?.departmentName ?? code;
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError || error instanceof Error) return error.message;
  return fallback;
}
