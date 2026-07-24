import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiError,
  downloadQualityInspectionPdf,
  finalizeQualityInspection,
  getQualityInspectionPhotoBlob,
  getQualityInspectionPanel,
  getQualityInspectionQueue,
  listQualityActionDepartments,
  reconcileQualityInspectionHandoffs,
  removeQualityInspectionPhoto,
  retryQualityInspectionPdf,
  saveQualityInspectionResponses,
  startQualityInspection,
  uploadQualityInspectionPhoto
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { workflowShapeRole } from './design-system';
import { MobileSheet } from './MobileSheet';
import { PendingInspectionContext } from './PendingInspectionContext';
import type {
  QualityActionDepartment,
  QualityCheckResult,
  QualityInspectionDetail,
  QualityInspectionItemValue,
  QualityInspectionPanel,
  QualityInspectionPhoto,
  QualityInspectionProject,
  QualityInspectionQueueResponse,
  QualityInspectionStage
} from './qualityInspections';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type QueueState = { kind: 'loading' } | { kind: 'ready'; data: QualityInspectionQueueResponse } | { kind: 'error'; message: string };
type DetailState = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; data: QualityInspectionDetail } | { kind: 'error'; message: string };
type Feedback = { tone: 'success' | 'error' | 'info'; message: string };
type DraftValue = { checkResult: QualityCheckResult | null; textValue: string; note: string };
type OperationReceipt = { fingerprint: string; operationId: string };

const stageTabs: Array<{ value: QualityInspectionStage; label: string; short: string }> = [
  { value: 'LQC', label: 'LQC', short: '10' },
  { value: 'OQC', label: 'OQC 자체검수', short: '12' },
  { value: 'CustomerInspection', label: '전진검수', short: '13' },
  { value: 'FAT', label: 'FAT', short: '14' }
];

export function QualityInspectionsPage({
  developmentUserKey,
  canInspect,
  initialStage = 'LQC',
  initialProjectId,
  initialPanelId,
  onOpenIqc,
  onBack,
  onOpenPending
}: {
  developmentUserKey: string;
  canInspect: boolean;
  initialStage?: QualityInspectionStage;
  initialProjectId?: string;
  initialPanelId?: string;
  onOpenIqc: () => void;
  onBack: () => void;
  onOpenPending: (pendingId: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [stage, setStage] = useState<QualityInspectionStage>(initialStage);
  const [queueState, setQueueState] = useState<QueueState>({ kind: 'loading' });
  const [detailState, setDetailState] = useState<DetailState>({ kind: 'idle' });
  const [selectedProjectId, setSelectedProjectId] = useState(initialProjectId ?? '');
  const [selectedPanelId, setSelectedPanelId] = useState(initialPanelId ?? '');
  const [draft, setDraft] = useState<Record<string, DraftValue>>({});
  const [departments, setDepartments] = useState<QualityActionDepartment[]>([]);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [savingAction, setSavingAction] = useState('');
  const [decisionOpen, setDecisionOpen] = useState(false);
  const [decisionError, setDecisionError] = useState('');
  const [decisionConflict, setDecisionConflict] = useState(false);
  const [decision, setDecision] = useState<'Passed' | 'Failed'>('Passed');
  const [decisionReason, setDecisionReason] = useState('검사 기준을 확인했습니다.');
  const [actionDepartment, setActionDepartment] = useState('');
  const [actionAssignee, setActionAssignee] = useState('');
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoAlt, setPhotoAlt] = useState('');
  const [photoItemId, setPhotoItemId] = useState('');
  const decisionTriggerRef = useRef<HTMLButtonElement>(null);
  const operationReceipts = useRef<Record<string, OperationReceipt>>({});
  const reconciliationAttemptedForUser = useRef('');
  const finalizeInFlight = useRef(false);

  const writeLocation = useCallback((nextStage: QualityInspectionStage, projectId?: string, panelId?: string) => {
    const params = new URLSearchParams({ stage: nextStage });
    if (projectId) params.set('project', projectId);
    if (panelId) params.set('panel', panelId);
    window.history.replaceState(null, '', `/quality/inspections?${params.toString()}`);
  }, []);

  const loadQueue = useCallback(async (
    nextStage: QualityInspectionStage,
    preferredProjectId?: string,
    preferredPanelId?: string
  ) => {
    setQueueState({ kind: 'loading' });
    try {
      if (canInspect && reconciliationAttemptedForUser.current !== developmentUserKey) {
        reconciliationAttemptedForUser.current = developmentUserKey;
        try {
          const reconciled = await reconcileQualityInspectionHandoffs(developmentUserKey);
          const recoveredCount = reconciled.recoveredOqcHandoffCount
            + reconciled.recoveredInspectionHandoffCount
            + reconciled.recoveredPackingHandoffCount;
          if (recoveredCount > 0) {
            setFeedback({
              tone: 'info',
              message: `누락된 품질 후속 업무 ${recoveredCount}건을 자동으로 복구했습니다.`
            });
          } else if (reconciled.unresolvedAssigneeCount > 0) {
            setFeedback({
              tone: 'info',
              message: `담당자 미지정으로 복구하지 못한 후속 업무 ${reconciled.unresolvedAssigneeCount}건이 있습니다.`
            });
          }
        } catch {
          setFeedback({
            tone: 'info',
            message: '누락 업무 자동 점검에 실패했지만 현재 검사 목록은 계속 불러옵니다.'
          });
        }
      }
      const data = await getQualityInspectionQueue(developmentUserKey, nextStage);
      setQueueState({ kind: 'ready', data });
      const project = data.projects.find((item) => item.projectId === preferredProjectId)
        ?? data.projects.find((item) => item.blockedCount > 0 || item.inProgressCount > 0 || item.readyCount > 0)
        ?? data.projects[0];
      const panel = project?.panels.find((item) => item.panelId === preferredPanelId)
        ?? project?.panels.find((item) => item.status === 'Failed' || item.status === 'InProgress' || item.status === 'Ready')
        ?? project?.panels[0];
      setSelectedProjectId(project?.projectId ?? '');
      setSelectedPanelId(panel?.panelId ?? '');
      writeLocation(nextStage, project?.projectId, panel?.panelId);
    } catch (error) {
      setQueueState({ kind: 'error', message: errorMessage(error, '품질검사 목록을 불러오지 못했습니다.') });
    }
  }, [canInspect, developmentUserKey, writeLocation]);

  const loadDetail = useCallback(async (panelId: string, nextStage: QualityInspectionStage) => {
    if (!panelId) {
      setDetailState({ kind: 'idle' });
      return;
    }
    setDetailState({ kind: 'loading' });
    try {
      setDetailState({ kind: 'ready', data: await getQualityInspectionPanel(developmentUserKey, panelId, nextStage) });
    } catch (error) {
      setDetailState({ kind: 'error', message: errorMessage(error, '검사 상세를 불러오지 못했습니다.') });
    }
  }, [developmentUserKey]);

  useEffect(() => {
    queueMicrotask(() => void loadQueue(stage, initialProjectId, initialPanelId));
  // Deep-link selection is applied on initial mount; later selection is local state.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [developmentUserKey]);

  useEffect(() => {
    queueMicrotask(() => void loadDetail(selectedPanelId, stage));
  }, [loadDetail, selectedPanelId, stage]);

  useEffect(() => {
    if (!canInspect) return;
    void listQualityActionDepartments(developmentUserKey).then(setDepartments).catch(() => setDepartments([]));
  }, [canInspect, developmentUserKey]);

  const detail = detailState.kind === 'ready' ? detailState.data : null;
  useEffect(() => {
    if (!detail) return;
    const values: Record<string, DraftValue> = {};
    const current = new Map(detail.responses.map((item) => [item.templateItemId, item]));
    for (const item of detail.items) {
      const value = current.get(item.itemId);
      values[item.itemId] = {
        checkResult: value?.checkResult ?? null,
        textValue: value?.textValue ?? '',
        note: value?.note ?? ''
      };
    }
    setDraft(values);
    setPhotoItemId((currentValue) => detail.items.some((item) => item.itemId === currentValue && item.isAvailable !== false)
      ? currentValue
      : detail.items.find((item) => item.isAvailable !== false)?.itemId ?? '');
    if (detail.panel.pendingId && detail.panel.actionDepartmentCode) {
      setActionDepartment(detail.panel.actionDepartmentCode);
      setActionAssignee('');
    }
    setDecisionError('');
    setDecisionConflict(false);
  }, [detail]);

  const projects = queueState.kind === 'ready' ? queueState.data.projects : [];
  const selectedProject = projects.find((item) => item.projectId === selectedProjectId) ?? null;
  const qualityVisibleIds = selectedProject?.panels.map((item) => item.panelId) ?? [];
  const qualitySelection = useSelectedRows(qualityVisibleIds);
  const selectedPanel = selectedProject?.panels.find((item) => item.panelId === selectedPanelId) ?? null;
  const panel = detail?.panel ?? selectedPanel;
  const selectedDepartment = departments.find((item) => item.departmentCode === actionDepartment) ?? null;
  const requiredItems = detail?.decisionMode === 'Checklist' ? detail.items.filter((item) => item.isRequired) : [];
  const completedRequired = requiredItems.filter((item) => {
    const value = draft[item.itemId];
    return item.responseType === 'Check' ? Boolean(value?.checkResult) : Boolean(value?.textValue.trim());
  }).length;
  const progress = requiredItems.length ? Math.round((completedRequired / requiredItems.length) * 100) : 0;
  const hasUnavailableRequired = requiredItems.some((item) => item.isAvailable === false);
  const canMutatePanel = canInspect && panel?.canMutate === true;
  const isReinspection = Boolean(panel?.pendingId && detail?.reportStatus !== 'Finalized');
  const hasFailedResponse = detail?.decisionMode === 'Checklist'
    && detail.items.some((item) => item.isAvailable !== false && draft[item.itemId]?.checkResult === 'Fail');

  function operationId(action: string, fingerprint: string) {
    const current = operationReceipts.current[action];
    if (current?.fingerprint === fingerprint) return current.operationId;
    const next = { fingerprint, operationId: crypto.randomUUID() };
    operationReceipts.current[action] = next;
    return next.operationId;
  }

  async function refresh(projectId: string, panelId: string) {
    const [queue, nextDetail] = await Promise.all([
      getQualityInspectionQueue(developmentUserKey, stage),
      getQualityInspectionPanel(developmentUserKey, panelId, stage)
    ]);
    setQueueState({ kind: 'ready', data: queue });
    setDetailState({ kind: 'ready', data: nextDetail });
    setSelectedProjectId(projectId);
    setSelectedPanelId(panelId);
  }

  function selectStage(next: QualityInspectionStage) {
    setStage(next);
    setFeedback(null);
    setSelectedProjectId('');
    setSelectedPanelId('');
    void loadQueue(next);
  }

  function selectProject(project: QualityInspectionProject) {
    const nextPanel = project.panels.find((item) => item.status === 'Failed' || item.status === 'InProgress' || item.status === 'Ready') ?? project.panels[0];
    setSelectedProjectId(project.projectId);
    setSelectedPanelId(nextPanel?.panelId ?? '');
    setFeedback(null);
    writeLocation(stage, project.projectId, nextPanel?.panelId);
  }

  function selectPanel(nextPanel: QualityInspectionPanel) {
    setSelectedPanelId(nextPanel.panelId);
    setFeedback(null);
    writeLocation(stage, selectedProjectId, nextPanel.panelId);
  }

  function openDecision() {
    setDecision(hasFailedResponse ? 'Failed' : 'Passed');
    setDecisionError('');
    setDecisionConflict(false);
    setDecisionOpen(true);
  }

  async function start() {
    if (!selectedProject || !panel || !canMutatePanel) return;
    const fingerprint = `${selectedProject.projectId}|${panel.panelId}|${stage}`;
    setSavingAction('start');
    setFeedback(null);
    try {
      await startQualityInspection(developmentUserKey, {
        operationId: operationId('start', fingerprint),
        projectId: selectedProject.projectId,
        panelId: panel.panelId,
        stageCode: stage
      });
      delete operationReceipts.current.start;
      await refresh(selectedProject.projectId, panel.panelId);
      setFeedback({ tone: 'success', message: `${panel.displayCode} ${stageLabel(stage)} 검사를 시작했습니다.` });
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '검사를 시작하지 못했습니다.') });
    } finally {
      setSavingAction('');
    }
  }

  function responsePayload(): QualityInspectionItemValue[] {
    return (detail?.items ?? []).flatMap((item) => {
      if (item.isAvailable === false) return [];
      const value = draft[item.itemId];
      if (!value) return [];
      if (item.responseType === 'Check' && !value.checkResult) return [];
      if (item.responseType === 'Text' && !value.textValue.trim()) return [];
      return [{
        templateItemId: item.itemId,
        checkResult: item.responseType === 'Check' ? value.checkResult : null,
        textValue: item.responseType === 'Text' ? value.textValue.trim() : null,
        note: value.note.trim() || null
      }];
    });
  }

  async function saveDraft(showFeedback = true) {
    if (!detail?.reportId || !detail.reportVersion || !canMutatePanel || detail.decisionMode !== 'Checklist') return null;
    const responses = responsePayload();
    const fingerprint = `${detail.reportId}|${detail.reportVersion}|${JSON.stringify(responses)}`;
    setSavingAction('save');
    if (showFeedback) setFeedback(null);
    try {
      const result = await saveQualityInspectionResponses(developmentUserKey, detail.reportId, {
        operationId: operationId('save', fingerprint),
        expectedReportVersion: detail.reportVersion,
        responses
      });
      delete operationReceipts.current.save;
      if (showFeedback) {
        await refresh(result.projectId, result.panelId);
        setFeedback({ tone: 'success', message: '검사 항목을 저장했습니다.' });
      }
      return result;
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '검사 항목을 저장하지 못했습니다.') });
      return null;
    } finally {
      setSavingAction('');
    }
  }

  async function finalize() {
    if (!detail?.reportId || !detail.reportVersion || !canMutatePanel || finalizeInFlight.current) return;
    const validationError = validateDecision(
      detail,
      draft,
      decision,
      decisionReason,
      actionDepartment
    );
    if (validationError) {
      setDecisionError(validationError);
      setDecisionConflict(false);
      return;
    }
    finalizeInFlight.current = true;
    setSavingAction('finalize');
    setFeedback(null);
    setDecisionError('');
    setDecisionConflict(false);
    try {
      const responses = detail.decisionMode === 'Checklist' ? responsePayload() : null;
      const fingerprint = [
        detail.reportId,
        detail.reportVersion,
        decision,
        decisionReason.trim(),
        actionDepartment,
        actionAssignee,
        JSON.stringify(responses)
      ].join('|');
      const result = await finalizeQualityInspection(developmentUserKey, detail.reportId, {
        operationId: operationId('finalize', fingerprint),
        expectedReportVersion: detail.reportVersion,
        result: decision,
        reason: decisionReason.trim(),
        actionDepartmentCode: decision === 'Failed' ? actionDepartment : null,
        assigneeUserId: decision === 'Failed' && actionAssignee ? actionAssignee : null,
        responses
      });
      delete operationReceipts.current.finalize;
      setDecisionOpen(false);
      await refresh(result.projectId, result.panelId);
      setFeedback({
        tone: 'success',
        message: decision === 'Passed'
          ? passedFeedback(stage, selectedProject?.fatRequired === true, result.nextStageCode)
          : `${stageLabel(stage)} ${stage === 'CustomerInspection' || stage === 'FAT' ? 'PUNCH' : '부적합'} Pending #${result.pendingNumber ?? '-'} 생성`
      });
    } catch (error) {
      setDecisionError(errorMessage(error, '검사 판정을 확정하지 못했습니다.'));
      setDecisionConflict(error instanceof ApiError && error.status === 409);
    } finally {
      finalizeInFlight.current = false;
      setSavingAction('');
    }
  }

  async function reloadDecisionAfterConflict() {
    if (!selectedProject || !panel || savingAction) return;
    setSavingAction('reload');
    try {
      await refresh(selectedProject.projectId, panel.panelId);
      setDecisionError('');
      setDecisionConflict(false);
    } catch (error) {
      setDecisionError(errorMessage(error, '최신 검사 내용을 불러오지 못했습니다.'));
    } finally {
      setSavingAction('');
    }
  }

  async function uploadPhoto() {
    if (!detail?.reportId || !detail.reportVersion || !photoFile || !photoItemId || !photoAlt.trim() || !canMutatePanel) return;
    const fingerprint = [detail.reportId, detail.reportVersion, photoItemId, photoAlt.trim(), photoFile.name, photoFile.size, photoFile.lastModified].join('|');
    setSavingAction('photo');
    setFeedback(null);
    try {
      const result = await uploadQualityInspectionPhoto(
        developmentUserKey,
        detail.reportId,
        operationId('photo', fingerprint),
        photoItemId,
        detail.reportVersion,
        photoAlt.trim(),
        photoFile
      );
      delete operationReceipts.current.photo;
      setPhotoFile(null);
      setPhotoAlt('');
      await refresh(result.projectId, result.panelId);
      setFeedback({ tone: 'success', message: '사진 증빙을 등록했습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '사진 증빙을 등록하지 못했습니다.') });
    } finally {
      setSavingAction('');
    }
  }

  async function removePhoto(photoId: string) {
    if (!detail?.reportId || !detail.reportVersion || !canMutatePanel) return;
    const fingerprint = [detail.reportId, photoId, detail.reportVersion].join('|');
    setSavingAction('remove-photo');
    setFeedback(null);
    try {
      const result = await removeQualityInspectionPhoto(
        developmentUserKey,
        detail.reportId,
        photoId,
        operationId('remove-photo', fingerprint),
        detail.reportVersion
      );
      delete operationReceipts.current['remove-photo'];
      await refresh(result.projectId, result.panelId);
      setFeedback({ tone: 'success', message: '사진 증빙을 삭제했습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, '사진 증빙을 삭제하지 못했습니다.') });
    } finally {
      setSavingAction('');
    }
  }

  async function retryPdf() {
    if (!detail?.reportId || !canMutatePanel) return;
    const fingerprint = detail.reportId;
    setSavingAction('pdf');
    setFeedback(null);
    try {
      const result = await retryQualityInspectionPdf(
        developmentUserKey,
        detail.reportId,
        operationId('pdf', fingerprint)
      );
      delete operationReceipts.current.pdf;
      await refresh(result.projectId, result.panelId);
      setFeedback({ tone: 'success', message: 'PDF 생성을 다시 요청했습니다.' });
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, 'PDF를 다시 만들지 못했습니다.') });
    } finally {
      setSavingAction('');
    }
  }

  async function downloadPdf() {
    if (!detail?.reportId) return;
    try {
      const blob = await downloadQualityInspectionPdf(developmentUserKey, detail.reportId);
      const source = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = source;
      anchor.download = `${panel?.displayCode ?? 'panel'}-${stage}-report.pdf`;
      anchor.click();
      URL.revokeObjectURL(source);
    } catch (error) {
      setFeedback({ tone: 'error', message: errorMessage(error, 'PDF를 내려받지 못했습니다.') });
    }
  }

  const stageSummary = useMemo(() => stageTabs.find((item) => item.value === stage)!, [stage]);

  return (
    <section className={isMobile ? 'page-surface quality-inspection-page quality-inspection-page--mobile' : 'page-surface quality-inspection-page'} data-testid="quality-inspection-page">
      <header className="quality-inspection-hero">
        <div>
          <p className="eyebrow">QUALITY · PANEL GATE</p>
          <h2>품질 검사</h2>
          <p>LQC는 제조 단계와 나란히 진행하고, 완료된 패널부터 다음 검사로 넘깁니다.</p>
        </div>
        <button type="button" onClick={onBack}>프로젝트 보기</button>
        <span className="quality-hero-circle" aria-hidden="true" />
        <span className="quality-hero-square" aria-hidden="true" />
      </header>

      <nav className="quality-stage-tabs" aria-label="품질 검사 단계">
        <button type="button" className="quality-stage-tab quality-stage-tab--iqc" onClick={onOpenIqc}>
          <span>07</span><strong>IQC</strong><small>자재</small>
        </button>
        {stageTabs.map((item) => (
          <button
            key={item.value}
            type="button"
            className={stage === item.value ? 'quality-stage-tab active' : 'quality-stage-tab'}
            data-stage={item.value.toLowerCase()}
            onClick={() => selectStage(item.value)}
          >
            <span>{item.short}</span><strong>{item.label}</strong><small>{item.value === 'CustomerInspection' ? '고객' : '패널'}</small>
          </button>
        ))}
      </nav>

      {queueState.kind === 'loading' ? <QualityLoading label={`${stageSummary.label} 대기열 확인 중`} /> : null}
      {queueState.kind === 'error' ? (
        <div className="quality-empty-state" role="alert"><strong>품질 대기열을 확인할 수 없습니다.</strong><span>{queueState.message}</span><button type="button" onClick={() => void loadQueue(stage)}>다시 불러오기</button></div>
      ) : null}
      {queueState.kind === 'ready' && projects.length === 0 ? (
        <div className="quality-empty-state"><strong>{stageSummary.label} 대기 패널이 없습니다.</strong><span>앞 단계가 완료되면 담당 패널이 이곳에 표시됩니다.</span></div>
      ) : null}

      {queueState.kind === 'ready' && projects.length > 0 ? (
        <div className="quality-workspace">
          <aside className="quality-project-rail" aria-label="품질 프로젝트 대기열">
            <div className="quality-section-label"><span>PROJECT QUEUE</span><strong>{projects.length}</strong></div>
            <div className="quality-project-list">
              {projects.map((project) => (
                <button
                  key={project.projectId}
                  type="button"
                  className={selectedProjectId === project.projectId ? 'quality-project-card active' : 'quality-project-card'}
                  data-shape-role={workflowShapeRole({
                    blocked: project.blockedCount > 0,
                    completed: project.completedCount > 0 && project.readyCount === 0 && project.inProgressCount === 0,
                    active: selectedProjectId === project.projectId,
                    inProgress: project.inProgressCount > 0
                  })}
                  onClick={() => selectProject(project)}
                >
                  <span>{project.projectCode}</span>
                  <strong>{project.projectTitle}</strong>
                  <small>대기 {project.readyCount} · 진행 {project.inProgressCount} · 차단 {project.blockedCount}</small>
                </button>
              ))}
            </div>
          </aside>

          {selectedProject ? (
            <main className="quality-panel-workarea">
              <header className="quality-project-summary">
                <div><p>{selectedProject.projectCode}</p><h3>{selectedProject.projectTitle}</h3></div>
                <div className="quality-mini-metrics" aria-label="프로젝트 검사 요약">
                  <span data-tone="ready"><strong>{selectedProject.readyCount}</strong><small>대기</small></span>
                  <span data-tone="progress"><strong>{selectedProject.inProgressCount}</strong><small>진행</small></span>
                  <span className="is-blocked" data-tone="blocked"><strong>{selectedProject.blockedCount}</strong><small>차단</small></span>
                  <span className="is-done" data-tone="completed"><strong>{selectedProject.completedCount}</strong><small>완료</small></span>
                </div>
              </header>

              <SelectedExportTray
                developmentUserKey={developmentUserKey}
                screen="quality-inspections"
                visibleIds={qualityVisibleIds}
                selectedIds={qualitySelection.selectedIds}
                allSelected={qualitySelection.allSelected}
                busy={qualitySelection.busy}
                filters={{ stage, projectId: selectedProject.projectId }}
                onBusyChange={qualitySelection.setBusy}
                onToggleAll={qualitySelection.toggleAll}
                onClear={qualitySelection.clear}
              />

              <div className="quality-panel-strip" aria-label="검사 패널 선택">
                {selectedProject.panels.map((item) => (
                  <div className="quality-panel-selectable" key={`${item.panelId}-${item.stageCode}`}>
                    <SelectionCheckbox checked={qualitySelection.selectedIds.has(item.panelId)} disabled={qualitySelection.busy} label={`${item.displayCode} 선택`} onChange={(checked) => qualitySelection.toggle(item.panelId, checked)} />
                    <button
                      type="button"
                      className={selectedPanelId === item.panelId ? 'quality-panel-chip active' : 'quality-panel-chip'}
                      data-status={statusKey(item.status)}
                      onClick={() => selectPanel(item)}
                    >
                      <span className="quality-status-shape" aria-hidden="true" />
                      <span><strong>{item.displayCode}</strong><small>{statusLabel(item.status)} · {item.attemptNumber ? `${item.attemptNumber}차` : '신규'}</small></span>
                    </button>
                  </div>
                ))}
              </div>

              {detailState.kind === 'loading' ? <QualityLoading label="패널 검사 불러오는 중" /> : null}
              {detailState.kind === 'error' ? <div className="quality-empty-state" role="alert"><strong>검사 상세를 열 수 없습니다.</strong><span>{detailState.message}</span></div> : null}
              {panel && detailState.kind !== 'loading' ? (
                <div className="quality-detail-grid">
                  <article className="quality-focus-card" data-status={statusKey(panel.status)}>
                    <header>
                      <span className="quality-focus-symbol">{stageSummary.short}</span>
                      <div><p>{panel.stageLabel} · {panel.attemptNumber ? `${panel.attemptNumber}차 검사` : '신규 검사'}</p><h3>{panel.displayCode}</h3><span>{panel.panelName ?? '패널 이름 미입력'}</span></div>
                      <strong className="quality-progress-value">{detail?.reportStatus === 'Finalized' ? statusLabel(panel.status) : `${progress}%`}</strong>
                    </header>
                    <div className="quality-progress-track"><span style={{ width: `${detail?.reportStatus === 'Finalized' ? 100 : progress}%` }} /></div>

                    {feedback ? <p className="quality-feedback" data-tone={feedback.tone}>{feedback.message}</p> : null}
                    {panel.pendingId ? (
                      <button type="button" className="quality-pending-link" onClick={() => onOpenPending(panel.pendingId!)}>
                        <span>조치 대기 · {panel.actionDepartmentCode ?? '부서 확인'}</span><strong>Pending #{panel.pendingNumber ?? '-'} →</strong>
                      </button>
                    ) : null}
                    {panel.pendingId && detail?.reportStatus !== 'Finalized' ? <PendingInspectionContext pendingId={panel.pendingId} developmentUserKey={developmentUserKey} /> : null}

                    {!detail?.reportId && panel.status !== 'Completed' ? (
                      <div className="quality-start-card">
                        <span>START CHECK</span><strong>검사 항목을 불러오고 판정 근거를 기록합니다.</strong>
                        <button type="button" disabled={!canMutatePanel || savingAction === 'start'} onClick={() => void start()}>{savingAction === 'start' ? '시작 중' : `${stageSummary.label} 시작`}</button>
                      </div>
                    ) : null}

                    {detail?.reportId ? (
                      <>
                        {detail.decisionMode === 'Aggregate' ? (
                          <section className="quality-aggregate-decision" aria-label={`${stageSummary.label} 패널 통합 판정`}>
                            <span>PANEL DECISION</span>
                            <strong>이 검사는 패널 전체를 한 번에 판정합니다.</strong>
                            <p>항목별 체크 없이 적합 또는 부적합을 선택합니다. 부적합이면 근거 사진이나 30자 이상의 구체적인 사유를 남기면 Pending으로 이동합니다.</p>
                          </section>
                        ) : <div className="quality-item-list">
                          {detail.items.map((item) => {
                            const value = draft[item.itemId] ?? { checkResult: null, textValue: '', note: '' };
                            return (
                              <section key={item.itemId} className="quality-item" data-result={value.checkResult?.toLowerCase() ?? 'empty'} data-available={item.isAvailable === false ? 'false' : 'true'}>
                                <header><span>{String(item.displayOrder).padStart(2, '0')}</span><div><strong>{item.label}</strong><small>{item.availabilityMessage ?? item.guidance ?? (item.isRequired ? '필수 확인' : '선택 입력')}</small></div>{item.isAvailable === false ? <em>제조 대기</em> : item.isRequired ? <em>필수</em> : null}</header>
                                {item.responseType === 'Check' ? (
                                  <div className="quality-choice-row" role="group" aria-label={`${item.label} 결과`}>
                                    {([['Pass', '적합'], ['NotApplicable', '해당없음'], ['Fail', '부적합']] as const).map(([result, label]) => (
                                      <button key={result} type="button" className={value.checkResult === result ? 'selected' : ''} data-result={result.toLowerCase()} disabled={!canMutatePanel || detail.reportStatus === 'Finalized' || item.isAvailable === false} onClick={() => setDraft((current) => ({ ...current, [item.itemId]: { ...value, checkResult: result } }))}>{label}</button>
                                    ))}
                                  </div>
                                ) : (
                                  <textarea value={value.textValue} maxLength={item.maxTextLength ?? 1000} disabled={!canMutatePanel || detail.reportStatus === 'Finalized' || item.isAvailable === false} placeholder="측정값·특이사항을 입력하세요." onChange={(event) => setDraft((current) => ({ ...current, [item.itemId]: { ...value, textValue: event.target.value } }))} />
                                )}
                                {item.responseType === 'Check' && value.checkResult === 'NotApplicable' ? <input value={value.note} disabled={!canMutatePanel || detail.reportStatus === 'Finalized' || item.isAvailable === false} placeholder="해당없음 사유" onChange={(event) => setDraft((current) => ({ ...current, [item.itemId]: { ...value, note: event.target.value } }))} /> : null}
                              </section>
                            );
                          })}
                        </div>}

                        <section className="quality-evidence-card">
                          <header><div><span>PHOTO EVIDENCE</span><strong>사진 증빙</strong></div><em>{detail.photos.length}/5</em></header>
                          {detail.photos.length ? <div className="quality-photo-list">{detail.photos.map((photo) => <QualityPhotoEvidence key={photo.photoId} developmentUserKey={developmentUserKey} reportId={detail.reportId!} photo={photo} editable={canMutatePanel && detail.reportStatus !== 'Finalized'} onRemove={() => void removePhoto(photo.photoId)} />)}</div> : <p>사진은 선택 사항입니다. 확정 시 등록된 증빙만 snapshot에 포함됩니다.</p>}
                          {canMutatePanel && detail.reportStatus !== 'Finalized' ? (
                            <div className="quality-photo-uploader">
                              {detail.decisionMode === 'Checklist' ? <label><span>연결 항목</span><select value={photoItemId} onChange={(event) => setPhotoItemId(event.target.value)}>{detail.items.filter((item) => item.isAvailable !== false).map((item) => <option key={item.itemId} value={item.itemId}>{item.displayOrder}. {item.label}</option>)}</select></label> : null}
                              <label className="quality-photo-file"><input type="file" accept="image/jpeg,image/png" capture="environment" onChange={(event) => setPhotoFile(event.target.files?.[0] ?? null)} /><span>{photoFile ? photoFile.name : '카메라 또는 사진 선택'}</span><small>JPEG·PNG / 장당 5MB 이하</small></label>
                              <label><span>사진 설명</span><input value={photoAlt} maxLength={200} placeholder="예: 배선 체결 상태" onChange={(event) => setPhotoAlt(event.target.value)} /></label>
                              <button type="button" disabled={!photoFile || !photoItemId || !photoAlt.trim() || Boolean(savingAction)} onClick={() => void uploadPhoto()}>{savingAction === 'photo' ? '등록 중' : '사진 등록'}</button>
                            </div>
                          ) : null}
                        </section>

                        {detail.reportStatus !== 'Finalized' ? (
                          <div className="quality-actions">
                            {detail.decisionMode === 'Checklist' ? <button type="button" disabled={!canMutatePanel || Boolean(savingAction)} onClick={() => void saveDraft()}>{savingAction === 'save' ? '저장 중' : '임시 저장'}</button> : null}
                            <button ref={decisionTriggerRef} type="button" className="quality-decision-button" disabled={!canMutatePanel || Boolean(savingAction) || hasUnavailableRequired} onClick={openDecision}>{hasUnavailableRequired ? '제조 단계 진행 대기' : '판정 확정'}</button>
                          </div>
                        ) : (
                          <div className="quality-finalized-card" data-result={detail.result?.toLowerCase()}><span>{detail.result === 'Passed' ? 'PASS' : stage === 'CustomerInspection' || stage === 'FAT' ? 'PUNCH' : 'FAIL'}</span><strong>{detail.reason}</strong><small>PDF {detail.pdfStatus === 'Ready' ? '생성 완료' : detail.pdfStatus === 'Failed' ? '재시도 필요' : '생성 중'}</small><div>{detail.pdfStatus === 'Ready' ? <button type="button" onClick={() => void downloadPdf()}>PDF 내려받기</button> : null}{detail.pdfStatus === 'Failed' && canMutatePanel ? <button type="button" disabled={Boolean(savingAction)} onClick={() => void retryPdf()}>{savingAction === 'pdf' ? '요청 중' : 'PDF 다시 만들기'}</button> : null}</div></div>
                        )}
                      </>
                    ) : null}
                  </article>

                  <aside className="quality-history-card">
                    <div className="quality-section-label"><span>ATTEMPT LOG</span><strong>{detail?.history.length ?? 0}</strong></div>
                    {detail?.history.length ? <ol>{detail.history.map((attempt) => <li key={attempt.attemptId} data-status={statusKey(attempt.status)}><span /><div><strong>{attempt.attemptNumber}차 · {statusLabel(attempt.status)}</strong><small>{attempt.pendingNumber ? `Pending #${attempt.pendingNumber}` : '검사 기록'}</small></div></li>)}</ol> : <p>첫 검사 기록을 준비하고 있습니다.</p>}
                  </aside>
                </div>
              ) : null}
            </main>
          ) : null}
        </div>
      ) : null}

      <MobileSheet
        open={decisionOpen}
        title={`${stageSummary.label} 판정`}
        eyebrow="FINAL JUDGMENT"
        description="판정을 확정하면 성적서가 잠기고 다음 단계 또는 Pending으로 연결됩니다."
        triggerRef={decisionTriggerRef}
        onClose={() => {
          if (!savingAction) setDecisionOpen(false);
        }}
      >
        <div className="quality-decision-form" aria-busy={savingAction === 'finalize'}>
          <div className="quality-decision-options">
            <button type="button" className={decision === 'Passed' ? 'selected' : ''} data-result="passed" disabled={Boolean(savingAction) || hasFailedResponse} onClick={() => { setDecision('Passed'); setDecisionError(''); }}><span>○</span><strong>합격</strong><small>{hasFailedResponse ? '부적합 항목 확인 필요' : '다음 단계 인계'}</small></button>
            <button type="button" className={decision === 'Failed' ? 'selected' : ''} data-result="failed" disabled={Boolean(savingAction)} onClick={() => { setDecision('Failed'); setDecisionError(''); }}><span>▰</span><strong>{stage === 'CustomerInspection' || stage === 'FAT' ? 'PUNCH 발생' : '부적합'}</strong><small>조치 Pending 생성</small></button>
          </div>
          <label><span>{isReinspection ? '재검사 코멘트' : '판정 사유'} <small>{decisionReason.length}/1000</small></span><textarea value={decisionReason} maxLength={1000} disabled={Boolean(savingAction)} placeholder={isReinspection ? '조치 내용을 확인한 결과와 재검사 판정 근거를 입력하세요.' : '검사 판정 근거를 입력하세요.'} onChange={(event) => { setDecisionReason(event.target.value); setDecisionError(''); }} /></label>
          {decision === 'Failed' && !isReinspection ? (
            <>
              <label><span>조치 담당 부서</span><select value={actionDepartment} disabled={Boolean(savingAction)} onChange={(event) => { setActionDepartment(event.target.value); setActionAssignee(''); setDecisionError(''); }}><option value="">부서 선택</option>{departments.map((item) => <option key={item.departmentCode} value={item.departmentCode}>{item.departmentName}</option>)}</select></label>
              <label><span>조치 담당자 <small>선택</small></span><select value={actionAssignee} disabled={!selectedDepartment || Boolean(savingAction)} onChange={(event) => setActionAssignee(event.target.value)}><option value="">담당자 미지정</option>{selectedDepartment?.assignees.map((item) => <option key={item.userId} value={item.userId}>{item.displayName}</option>)}</select></label>
            </>
          ) : null}
          {decisionError ? <p className="quality-decision-error" role="alert">{decisionError}</p> : null}
          {decisionConflict ? <button type="button" className="quality-decision-reload" disabled={Boolean(savingAction)} onClick={() => void reloadDecisionAfterConflict()}>{savingAction === 'reload' ? '불러오는 중' : '최신 검사 내용 다시 불러오기'}</button> : null}
          <button type="button" className="quality-finalize-submit" disabled={Boolean(savingAction) || decisionReason.trim().length < 3 || (decision === 'Failed' && !actionDepartment)} onClick={() => void finalize()}>{savingAction === 'finalize' ? '확정 중' : decision === 'Passed' ? (isReinspection ? '합격 · Pending 해제' : '합격 확정 및 인계') : (isReinspection ? '불합격 · 재조치 요청' : 'Pending 생성 및 확정')}</button>
        </div>
      </MobileSheet>
    </section>
  );
}

function QualityPhotoEvidence({
  developmentUserKey,
  reportId,
  photo,
  editable,
  onRemove
}: {
  developmentUserKey: string;
  reportId: string;
  photo: QualityInspectionPhoto;
  editable: boolean;
  onRemove: () => void;
}) {
  const [source, setSource] = useState('');
  useEffect(() => {
    let active = true;
    let objectUrl = '';
    void getQualityInspectionPhotoBlob(developmentUserKey, reportId, photo.photoId).then((blob) => {
      if (!active) return;
      objectUrl = URL.createObjectURL(blob);
      setSource(objectUrl);
    }).catch(() => setSource(''));
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [developmentUserKey, photo.photoId, reportId]);
  return (
    <figure>
      {source ? <img src={source} alt={photo.altText} /> : <div aria-label="사진 불러오는 중" />}
      <figcaption><strong>{photo.altText}</strong><small>{Math.max(1, Math.round(photo.byteSize / 1024))}KB</small></figcaption>
      {editable ? <button type="button" onClick={onRemove}>삭제</button> : null}
    </figure>
  );
}

function QualityLoading({ label }: { label: string }) {
  return <div className="quality-loading"><span /><span /><span /><p>{label}</p></div>;
}

function stageLabel(stage: string) {
  return stage === 'LQC' ? 'LQC' : stage === 'OQC' ? 'OQC' : stage === 'CustomerInspection' ? '전진검수' : stage === 'FAT' ? 'FAT' : stage === 'ManufacturingCompleted' ? '제조 완료 확인' : stage === 'PackingCompleted' ? '포장' : stage;
}

function passedFeedback(stage: QualityInspectionStage, fatRequired: boolean, nextStageCode: string | null) {
  if (stage === 'LQC' && !nextStageCode) return 'LQC 합격 · 이 패널의 제조 완료 시 OQC가 자동으로 열립니다.';
  if (stage === 'OQC' && fatRequired) return 'OQC 합격 · 이 패널의 전진검수와 FAT를 동시에 열었습니다.';
  if ((stage === 'CustomerInspection' || stage === 'FAT') && !nextStageCode) {
    return `${stageLabel(stage)} 합격 · 이 패널의 병행 품질검사 완료를 기다립니다.`;
  }
  return `합격 확정 · 다음 단계 ${nextStageCode ? stageLabel(nextStageCode) : '인계 완료'}`;
}

function statusLabel(status: string) {
  return status === 'Ready' || status === 'Requested' ? '시작 전' : status === 'InProgress' ? '진행 중' : status === 'Failed' ? '조치 필요' : status === 'Passed' || status === 'Completed' || status === 'Confirmed' ? '완료' : status;
}

function statusKey(status: string) {
  return status === 'Ready' || status === 'Requested' ? 'ready' : status === 'InProgress' ? 'inprogress' : status === 'Failed' ? 'blocked' : status === 'Passed' || status === 'Completed' || status === 'Confirmed' ? 'completed' : 'ready';
}

function errorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    return error.errors ? Object.values(error.errors).flat().find(Boolean) ?? error.message : error.message;
  }
  return error instanceof Error ? error.message : fallback;
}

function validateDecision(
  detail: QualityInspectionDetail,
  draft: Record<string, DraftValue>,
  decision: 'Passed' | 'Failed',
  reason: string,
  actionDepartment: string
) {
  if (reason.trim().length < 3) return '판정 사유를 3자 이상 입력해 주세요.';

  if (detail.decisionMode === 'Checklist') {
    for (const item of detail.items.filter((candidate) => candidate.isRequired && candidate.isAvailable !== false)) {
      const value = draft[item.itemId];
      if (item.responseType === 'Check' && !value?.checkResult) return `${item.label}: 필수 검사 결과를 입력해 주세요.`;
      if (item.responseType === 'Text' && !value?.textValue.trim()) return `${item.label}: 필수 측정값을 입력해 주세요.`;
      if (value?.checkResult === 'NotApplicable' && !value.note.trim()) return `${item.label}: 해당없음 사유를 입력해 주세요.`;
    }

    const hasFail = detail.items.some((item) => item.isAvailable !== false && draft[item.itemId]?.checkResult === 'Fail');
    if (decision === 'Passed' && hasFail) return '부적합 항목이 있어 합격으로 확정할 수 없습니다.';
    if (decision === 'Failed' && !hasFail) return '부적합 판정에는 하나 이상의 부적합 항목이 필요합니다.';
  }

  if (decision === 'Failed' && detail.photos.length === 0 && reason.trim().length < 30) {
    return '부적합 판정은 사진 1장 이상 또는 구체적인 근거 30자 이상이 필요합니다.';
  }
  if (decision === 'Failed' && !actionDepartment) return '조치 담당 부서를 선택해 주세요.';
  return null;
}
