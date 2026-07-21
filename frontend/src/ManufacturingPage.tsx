import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  checkManufacturingStep,
  completeManufacturingExecution,
  confirmPanelManufacturingCompleted,
  getManufacturingPanel,
  getManufacturingCompletionQueue,
  getManufacturingQueue,
  listManufacturingActionDepartments,
  resumeManufacturingExecution,
  startManufacturingExecution,
  stopManufacturingExecution
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { workflowShapeRole } from './design-system';
import { MobileSheet } from './MobileSheet';
import type {
  ManufacturingActionDepartment,
  ManufacturingExecutionDetail,
  ManufacturingPanel,
  ManufacturingProject,
  ManufacturingQueueResponse,
  ManufacturingStatus,
  StopManufacturingRequest
} from './manufacturing';
import type { QualityInspectionPanel } from './qualityInspections';
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
  onOpenPending
}: {
  developmentUserKey: string;
  canMutate: boolean;
  initialProjectId?: string;
  initialPanelId?: string;
  onBack: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [queueState, setQueueState] = useState<QueueState>({ kind: 'loading' });
  const [detailState, setDetailState] = useState<DetailState>({ kind: 'idle' });
  const [selectedProjectId, setSelectedProjectId] = useState(initialProjectId ?? '');
  const [selectedPanelId, setSelectedPanelId] = useState(initialPanelId ?? '');
  const [departments, setDepartments] = useState<ManufacturingActionDepartment[]>([]);
  const [completionPanels, setCompletionPanels] = useState<QualityInspectionPanel[]>([]);
  const [savingAction, setSavingAction] = useState('');
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [stopOpen, setStopOpen] = useState(false);
  const [stopReason, setStopReason] = useState<StopReason>('Material');
  const [stopDescription, setStopDescription] = useState('');
  const [stopDepartment, setStopDepartment] = useState('');
  const [stopAssignee, setStopAssignee] = useState('');
  const stopTriggerRef = useRef<HTMLButtonElement>(null);
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
      const data = await getManufacturingQueue(developmentUserKey);
      setQueueState({ kind: 'ready', data });
      void getManufacturingCompletionQueue(developmentUserKey)
        .then((response) => setCompletionPanels(response.projects.flatMap((project) => project.panels)))
        .catch(() => setCompletionPanels([]));
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
  const selectedPanel = selectedProject?.panels.find((panel) => panel.panelId === selectedPanelId) ?? null;
  const detail = detailState.kind === 'ready' ? detailState.data : null;
  const panel = detail?.panel ?? selectedPanel;
  const completionTask = completionPanels.find((item) => item.panelId === panel?.panelId) ?? null;
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
      getManufacturingQueue(developmentUserKey),
      getManufacturingPanel(developmentUserKey, panelId)
    ]);
    setQueueState({ kind: 'ready', data: queue });
    setDetailState({ kind: 'ready', data: panelDetail });
    setSelectedProjectId(projectId);
    setSelectedPanelId(panelId);
    void getManufacturingCompletionQueue(developmentUserKey)
      .then((response) => setCompletionPanels(response.projects.flatMap((project) => project.panels)))
      .catch(() => setCompletionPanels([]));
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

  function selectProject(project: ManufacturingProject) {
    if (mutationInFlightRef.current) return;
    const nextPanel = project.panels.find((item) => item.status === 'Blocked' || item.status === 'InProgress' || item.status === 'Ready')
      ?? project.panels[0];
    setSelectedProjectId(project.projectId);
    setSelectedPanelId(nextPanel?.panelId ?? '');
    setFeedback(null);
    writeLocation(project.projectId, nextPanel?.panelId);
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
      `${panel.displayCode} 제조 작업을 시작했습니다.`
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
      `${panel.displayCode} 제조를 완료하고 LQC 업무를 생성했습니다.`
    );
  }

  async function confirmCompletion() {
    if (!selectedProject || !panel || !completionTask || !canMutate) return;
    await mutate(
      'confirm-completion',
      `${selectedProject.projectId}|${panel.panelId}`,
      (id) => confirmPanelManufacturingCompleted(developmentUserKey, {
        operationId: id,
        projectId: selectedProject.projectId,
        panelId: panel.panelId
      }),
      `${panel.displayCode} 제조 완료를 확인하고 OQC로 넘겼습니다.`
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

  return (
    <section
      className={isMobile ? 'page-surface manufacturing-page manufacturing-page--mobile' : 'page-surface manufacturing-page'}
      data-testid="manufacturing-page"
    >
      <header className="manufacturing-hero">
        <div>
          <p className="eyebrow">SHOP FLOOR · PANEL FLOW</p>
          <h2>제조 작업</h2>
          <p>키팅 완료 패널을 시작하고, 네 단계 확인 후 LQC로 넘깁니다.</p>
        </div>
        <button type="button" className="manufacturing-back" onClick={onBack}>프로젝트 보기</button>
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
          <span>자재 화면에서 패널 키팅을 완료하면 이곳에 제조 업무가 나타납니다.</span>
        </div>
      ) : null}

      {queueState.kind === 'ready' && projects.length > 0 ? (
        <div className="manufacturing-workspace">
          <aside className="manufacturing-project-queue" aria-label="제조 프로젝트 목록">
            <div className="manufacturing-section-heading">
              <p>PROJECT QUEUE</p>
              <strong>{projects.length}개 프로젝트</strong>
            </div>
            <div className="manufacturing-project-list">
              {projects.map((project) => (
                <button
                  key={project.projectId}
                  type="button"
                  className="manufacturing-project-card"
                  data-active={project.projectId === selectedProjectId}
                  data-shape-role={workflowShapeRole({
                    blocked: project.blockedCount > 0,
                    completed: project.completedCount > 0 && project.readyCount === 0 && project.inProgressCount === 0,
                    active: project.projectId === selectedProjectId,
                    inProgress: project.inProgressCount > 0
                  })}
                  disabled={Boolean(savingAction)}
                  onClick={() => selectProject(project)}
                >
                  <span>{project.projectCode}</span>
                  <strong>{project.projectTitle}</strong>
                  <small>대기 {project.readyCount} · 진행 {project.inProgressCount} · 중단 {project.blockedCount}</small>
                </button>
              ))}
            </div>
          </aside>

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

              <div className="manufacturing-panel-strip" aria-label="프로젝트 패널">
                {selectedProject.panels.map((item) => (
                  <div className="manufacturing-panel-selectable" key={item.panelId}>
                    <SelectionCheckbox checked={manufacturingSelection.selectedIds.has(item.panelId)} disabled={manufacturingSelection.busy} label={`${item.displayCode} 선택`} onChange={(checked) => manufacturingSelection.toggle(item.panelId, checked)} />
                    <button
                      type="button"
                      className="manufacturing-panel-chip"
                      data-status={item.status.toLowerCase()}
                      data-active={item.panelId === selectedPanelId}
                      disabled={Boolean(savingAction)}
                      onClick={() => selectPanel(item)}
                    >
                      <span className="manufacturing-status-shape" aria-hidden="true">{item.status === 'Completed' ? '✓' : ''}</span>
                      <span><strong>{item.displayCode}</strong><small>{statusLabel(item.status)}</small></span>
                    </button>
                  </div>
                ))}
              </div>

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

                    {panel.status === 'Ready' ? (
                      <div className="manufacturing-ready-copy">
                        <strong>키팅 완료 · 제조 시작 준비</strong>
                        <span>작업지시와 도면을 준비한 뒤 이 패널의 실행을 시작하세요.</span>
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

                    {!mayMutatePanel ? (
                      <p className="manufacturing-readonly-note">조회 전용입니다. 제조 시작·체크·중단·완료는 제조 담당 권한이 필요합니다.</p>
                    ) : null}
                    {feedback ? <p className="manufacturing-feedback" data-tone={feedback.tone} role="status">{feedback.message}</p> : null}

                    {mayMutatePanel ? (
                      <div className="manufacturing-actions">
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
                            {savingAction === 'complete' ? '완료 중…' : '제조 완료 · LQC 전달'}
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
                      </div>
                    ) : null}

                    {panel.status === 'Blocked' && panel.activePendingId ? (
                      <button type="button" className="manufacturing-pending-link" onClick={() => onOpenPending(panel.activePendingId!)}>
                        <span>긴급 Pending #{panel.activePendingNumber}</span>
                        <strong>{departmentName(panel.actionDepartmentCode, departments)} 조치 확인 →</strong>
                      </button>
                    ) : null}
                    {panel.status === 'Completed' && completionTask ? (
                      <section className="manufacturing-confirmation-card" data-status={completionTask.status.toLowerCase()}>
                        <span className="manufacturing-confirmation-mark" aria-hidden="true">11</span>
                        <div>
                          <small>LQC PASS · NEXT GATE</small>
                          <strong>{completionTask.status === 'Confirmed' ? '제조 완료 확인됨' : '제조 완료 확인 대기'}</strong>
                          <p>{completionTask.status === 'Confirmed' ? 'OQC 업무가 생성되었습니다.' : 'LQC 합격 내용을 확인하면 OQC 담당자에게 즉시 인계됩니다.'}</p>
                        </div>
                        {completionTask.status !== 'Confirmed' ? (
                          <button type="button" disabled={!canMutate || Boolean(savingAction)} onClick={() => void confirmCompletion()}>
                            {savingAction === 'confirm-completion' ? '확인 중' : '제조 완료 확인'}
                          </button>
                        ) : <span className="manufacturing-confirmed-disc">✓</span>}
                      </section>
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
