import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, cancelLogisticsDraft, createLogisticsBatch, createPackingUnit, finalizeLogisticsOperation, getLogisticsDraft, getLogisticsQueue, uploadLogisticsEvidence } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsActionBar, DsInputFlow, DsInputSection } from './design-system';
import { OperationalProjectDashboard } from './OperationalProjectDashboard';
import type { LogisticsDraftResponse, LogisticsMutationResponse, LogisticsQueueItem, LogisticsQueueResponse, LogisticsStage } from './logistics';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

interface LogisticsPageProps {
  developmentUserKey?: string;
  canMutate: boolean;
  initialStage?: LogisticsStage;
  initialProjectId?: string;
  initialPanelId?: string;
  initialUnitId?: string;
  initialDraftId?: string;
  onLocationChange: (stage: LogisticsStage, draftId?: string) => void;
  onBack: () => void;
  onOpenProject?: (projectId: string) => void;
}

const stageMeta: Record<LogisticsStage, { number: string; label: string; short: string; evidence: string; next: string }> = {
  packing: { number: '01', label: '포장', short: '포장 묶음', evidence: '포장 사진', next: '출발 처리' },
  departure: { number: '02', label: '출발', short: '상차 확인', evidence: '상차 사진', next: '납품 완료' },
  delivery: { number: '03', label: '납품', short: '인수 확인', evidence: '서명 명세서', next: '영업 정산' }
};

function messageOf(error: unknown) {
  if (error instanceof ApiError) return error.message;
  return error instanceof Error ? error.message : '물류 정보를 불러오지 못했습니다.';
}

export function LogisticsPage({
  developmentUserKey,
  canMutate,
  initialStage = 'packing',
  initialProjectId,
  initialPanelId,
  initialUnitId,
  initialDraftId,
  onLocationChange,
  onBack,
  onOpenProject
}: LogisticsPageProps) {
  const layout = useAdaptiveLayout();
  const [stage, setStage] = useState<LogisticsStage>(initialStage);
  const [queue, setQueue] = useState<LogisticsQueueResponse | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [draft, setDraft] = useState<LogisticsMutationResponse | null>(null);
  const [draftDetails, setDraftDetails] = useState<LogisticsDraftResponse | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [altText, setAltText] = useState('물류 처리 증빙');
  const [departureDate, setDepartureDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);
  const errorRef = useRef<HTMLDivElement>(null);
  const onLocationChangeRef = useRef(onLocationChange);
  useEffect(() => { onLocationChangeRef.current = onLocationChange; }, [onLocationChange]);

  const applyDraftDetails = useCallback((details: LogisticsDraftResponse) => {
    setDraftDetails(details);
    setDraft({
      operationId: crypto.randomUUID(),
      projectId: details.projectId,
      targetId: details.targetId,
      stage: details.stage,
      status: details.status,
      version: details.version,
      nextStage: 'evidence',
      replayed: false
    });
    if (details.departureDate) setDepartureDate(details.departureDate);
    setSelected(new Set());
  }, []);

  const refreshDraft = useCallback(async (targetId: string) => {
    const details = await getLogisticsDraft(developmentUserKey, stage, targetId);
    if (details.status !== 'Draft') throw new Error('이미 확정되거나 취소된 물류 작업입니다. 목록을 새로 확인해 주세요.');
    applyDraftDetails(details);
    return details;
  }, [applyDraftDetails, developmentUserKey, stage]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const response = await getLogisticsQueue(developmentUserKey, stage, initialProjectId);
      setQueue(response);
      const recoverableDraftId = initialProjectId ? initialDraftId ?? response.drafts?.[0]?.targetId : undefined;
      if (recoverableDraftId) {
        try {
          await refreshDraft(recoverableDraftId);
          if (!initialDraftId) {
            onLocationChangeRef.current(stage, recoverableDraftId);
            setFeedback({ kind: 'success', text: '중간에 멈춘 물류 작업을 복구했습니다. 증빙을 확인하고 저장하면 바로 확정됩니다.' });
          }
        } catch (error) {
          setDraft(null);
          setDraftDetails(null);
          setFeedback({ kind: 'error', text: messageOf(error) });
        }
      } else {
        setDraft(null);
        setDraftDetails(null);
        const preferred = response.projects.flatMap((project) => project.items).find((item) =>
          item.targetId === initialUnitId || item.panelIds.includes(initialPanelId ?? ''));
        setSelected(preferred ? new Set([preferred.targetId]) : new Set());
      }
    } catch (error) {
      setQueue(null);
      setFeedback({ kind: 'error', text: messageOf(error) });
      setLoading(false);
      return;
    }
    setLoading(false);
  }, [developmentUserKey, initialDraftId, initialPanelId, initialProjectId, initialUnitId, refreshDraft, stage]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { setStage(initialStage); }, [initialStage]);
  useEffect(() => { if (feedback?.kind === 'error') errorRef.current?.focus(); }, [feedback]);

  const allItems = useMemo(() => queue?.projects.flatMap((project) =>
    project.items.map((item) => ({ project, item }))) ?? [], [queue]);
  const logisticsVisibleIds = allItems.map(({ item }) => item.targetId);
  const logisticsExportSelection = useSelectedRows(logisticsVisibleIds);
  const selectedEntries = allItems.filter(({ item }) => selected.has(item.targetId));
  const selectedProjectId = selectedEntries[0]?.project.projectId;
  const selectionCrossesProjects = selectedEntries.some(({ project }) => project.projectId !== selectedProjectId);
  const selectedBlocked = selectedEntries.some(({ item }) => item.hasOpenPending || !item.canMutate);
  const selectedCount = draftDetails
    ? draftDetails.panelIds.length
    : selected.size;
  const selectedLabel = draftDetails?.displayCode
    ?? (selectedEntries.map(({ item }) => item.displayCode).join(', ') || '대상을 선택하세요');
  const evidenceCount = draftDetails?.evidence.length ?? 0;

  function toggle(item: LogisticsQueueItem) {
    if (draft || !item.canMutate) return;
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(item.targetId)) next.delete(item.targetId);
      else next.add(item.targetId);
      return next;
    });
  }

  async function saveAndFinalize() {
    if ((!draft && (!selectedProjectId || selected.size === 0 || selectionCrossesProjects || selectedBlocked))
      || (!file && evidenceCount === 0)) return;
    setBusy(true);
    setFeedback(null);
    let workingDraft = draft;
    try {
      if (!workingDraft) {
        const operationId = crypto.randomUUID();
        workingDraft = stage === 'packing'
          ? await createPackingUnit(developmentUserKey, {
            operationId,
            projectId: selectedProjectId!,
            panelIds: selectedEntries.flatMap(({ item }) => item.panelIds),
            note: note.trim() || null,
            specification: null,
            weightText: null
          })
          : await createLogisticsBatch(developmentUserKey, stage, {
            operationId,
            projectId: selectedProjectId!,
            panelIds: selectedEntries.flatMap(({ item }) => item.panelIds),
            departureDate: stage === 'departure' ? departureDate : null
          });
        setDraft(workingDraft);
        onLocationChange(stage, workingDraft.targetId);
      }
      if (file) {
        workingDraft = await uploadLogisticsEvidence(
          developmentUserKey,
          stage,
          workingDraft.targetId,
          crypto.randomUUID(),
          workingDraft.version,
          stage === 'delivery' ? '' : altText,
          file
        );
        setDraft(workingDraft);
      }
      await finalizeLogisticsOperation(
        developmentUserKey,
        stage,
        workingDraft.targetId,
        crypto.randomUUID(),
        workingDraft.version
      );
      setFeedback({ kind: 'success', text: `${stageMeta[stage].label} 확정 완료 · 다음 단계는 ${stageMeta[stage].next}입니다.` });
      setDraft(null);
      setDraftDetails(null);
      setFile(null);
      setSelected(new Set());
      onLocationChange(stage);
    } catch (error) {
      if (workingDraft) {
        try {
          await refreshDraft(workingDraft.targetId);
          onLocationChange(stage, workingDraft.targetId);
        } catch {
          // A concurrent finalization can make the draft unreadable as a draft; the queue reload will reconcile it.
        }
      }
      setFeedback({ kind: 'error', text: `${messageOf(error)} 입력 내용은 임시 작업으로 보존했으며 이 화면에서 다시 저장할 수 있습니다.` });
    } finally {
      setBusy(false);
    }
  }

  async function cancelDraft() {
    if (!draft) return;
    setBusy(true);
    setFeedback(null);
    try {
      await cancelLogisticsDraft(developmentUserKey, stage, draft.targetId, crypto.randomUUID(), draft.version);
      setDraft(null);
      setDraftDetails(null);
      setFile(null);
      setSelected(new Set());
      onLocationChange(stage);
      setFeedback({ kind: 'success', text: '임시 물류 작업을 취소했습니다. 대기 목록에서 다시 시작할 수 있습니다.' });
    } catch (error) {
      setFeedback({ kind: 'error', text: messageOf(error) });
    } finally {
      setBusy(false);
    }
  }

  if (!loading && queue && !initialProjectId) {
    const readyCount = queue.projects.reduce((sum, project) =>
      sum + project.items.filter((item) => !item.hasOpenPending).length, 0);
    const draftCount = queue.drafts.length;
    return (
      <OperationalProjectDashboard
        testId={`logistics-${stage}-dashboard`}
        eyebrow={`LOGISTICS · ${stage.toUpperCase()}`}
        title={`${stageMeta[stage].label} 프로젝트`}
        description={`${stageMeta[stage].label} 대상이 있는 프로젝트를 선택하면 해당 프로젝트의 처리 대상만 표시합니다.`}
        unitLabel="대상"
        metrics={[
          { label: '처리 대기', value: readyCount, helper: `${stageMeta[stage].label} 가능 대상` },
          { label: '작성 중', value: draftCount, helper: '증빙·확정 진행 중' },
          { label: '차단', value: queue.blockedCount, helper: 'Pending 확인 필요', tone: 'warning' },
          { label: '대상 프로젝트', value: queue.projects.length, helper: '현재 업무 기준' }
        ]}
        projects={queue.projects.map((project) => {
          const projectDraftCount = queue.drafts.filter((item) => item.projectId === project.projectId).length;
          const blockedCount = project.items.filter((item) => item.hasOpenPending).length;
          const projectReadyCount = project.items.length - blockedCount;
          return {
            projectId: project.projectId,
            projectCode: project.projectCode,
            projectTitle: project.projectTitle,
            totalCount: project.items.length + projectDraftCount,
            readyCount: projectReadyCount,
            inProgressCount: projectDraftCount,
            blockedCount,
            completedCount: 0,
            detail: `${stageMeta[stage].evidence} 등록 후 한 번에 확정`
          };
        })}
        emptyMessage={`${stageMeta[stage].label} 대상 프로젝트가 없습니다.`}
        onBack={onBack}
        onOpenProject={(projectId) => onOpenProject?.(projectId)}
      />
    );
  }

  return (
    <section className="logistics-page" data-mobile={layout.mode === 'mobile'}>
      <header className="logistics-hero">
        <button type="button" className="logistics-back" onClick={onBack} aria-label="프로젝트로 돌아가기">←</button>
        <div>
          <span className="logistics-kicker">LOGISTICS CONTROL</span>
          <h1>{stageMeta[stage].label} 처리</h1>
          <p>선택한 프로젝트의 {stageMeta[stage].label} 대상과 증빙만 처리합니다.</p>
        </div>
        <div className="logistics-today" aria-label={`오늘 할 일 ${queue?.todayCount ?? 0}건`}>
          <strong>{queue?.todayCount ?? '—'}</strong>
          <span>오늘</span>
        </div>
      </header>

      <div className="logistics-priority-strip">
        <div><strong>{queue?.todayCount ?? 0}</strong><span>처리 대기</span></div>
        <div data-alert={(queue?.blockedCount ?? 0) > 0}><strong>{queue?.blockedCount ?? 0}</strong><span>차단 확인</span></div>
        <p><b>{stageMeta[stage].short}</b><span>대상과 증빙을 입력하고 저장 한 번으로 확정합니다.</span></p>
      </div>

      {feedback ? <div ref={errorRef} tabIndex={-1} role={feedback.kind === 'error' ? 'alert' : 'status'}
        className="logistics-feedback" data-kind={feedback.kind}>{feedback.text}</div> : null}

      {loading ? <div className="logistics-state"><span className="logistics-loader" /><strong>물류 대기 목록을 확인하고 있습니다.</strong></div> : null}
      {!loading && !queue && !feedback ? <div className="logistics-state"><strong>목록을 불러오지 못했습니다.</strong><button onClick={() => void load()}>다시 시도</button></div> : null}
      {!loading && queue && allItems.length === 0 && !draft ? <div className="logistics-state logistics-empty"><span>✓</span><strong>현재 {stageMeta[stage].label} 대기 작업이 없습니다.</strong><p>새 업무가 생성되면 이곳에 표시됩니다.</p></div> : null}

      {!loading && queue ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="logistics"
          visibleIds={logisticsVisibleIds}
          selectedIds={logisticsExportSelection.selectedIds}
          allSelected={logisticsExportSelection.allSelected}
          busy={logisticsExportSelection.busy}
          filters={{ stage, projectId: initialProjectId }}
          onBusyChange={logisticsExportSelection.setBusy}
          onToggleAll={logisticsExportSelection.toggleAll}
          onClear={logisticsExportSelection.clear}
        />
      ) : null}

      {!loading && queue && (allItems.length > 0 || draft) ? (
        <div className="logistics-workspace">
          <section className="logistics-queue" aria-label={`${stageMeta[stage].label} 대상 선택`}>
            <header><div><span>STEP 1</span><h2>대상 선택</h2></div><strong>{selectedCount} 선택</strong></header>
            {allItems.length === 0 ? <p className="logistics-readonly">진행 중인 draft를 복구했습니다. 증빙과 확정을 이어서 진행하세요.</p> : null}
            {queue.projects.map((project) => (
              <article key={project.projectId} className="logistics-project-group">
                <div className="logistics-project-heading"><span>{project.projectCode}</span><strong>{project.projectTitle}</strong><small>{project.items.length}건</small></div>
                <div className="logistics-target-list">
                  {project.items.map((item) => {
                    const active = selected.has(item.targetId);
                    return (
                      <div className="logistics-target-selectable" key={item.targetId}>
                        <SelectionCheckbox checked={logisticsExportSelection.selectedIds.has(item.targetId)} disabled={logisticsExportSelection.busy} label={`${item.displayCode} 내보내기 선택`} onChange={(checked) => logisticsExportSelection.toggle(item.targetId, checked)} />
                        <button type="button" className="logistics-target-card" data-selected={active}
                          data-blocked={item.hasOpenPending} onClick={() => toggle(item)} aria-pressed={active} disabled={draft !== null || !item.canMutate}>
                          <span className="logistics-target-shape">{item.targetType === 'Panel' ? 'P' : 'U'}</span>
                          <span><small>{item.displayCode}</small><strong>{item.title}</strong><em>{item.supportingText}</em></span>
                          <span className="logistics-target-status">{item.hasOpenPending ? 'Pending' : active ? '선택됨' : '대기'}</span>
                        </button>
                      </div>
                    );
                  })}
                </div>
              </article>
            ))}
          </section>

          <aside className="logistics-action-panel">
            <DsInputFlow title={`${stageMeta[stage].label} 입력`} description="대상과 증빙을 확인하면 저장 한 번으로 바로 확정됩니다.">
              <DsInputSection number={1} title="선택 대상 확인" description="왼쪽 목록에서 선택한 처리 대상입니다.">
                <div className="logistics-selection-summary">
                  <span className="logistics-summary-circle">{selectedCount}</span>
                  <div><small>선택 대상</small><strong>{selectedLabel}</strong></div>
                </div>
                {draft ? <div className="logistics-draft-token"><span>복구됨</span><strong>v{draft.version}</strong><small>등록 증빙 {evidenceCount}개 · 저장을 다시 눌러 확정할 수 있습니다.</small></div> : null}
              </DsInputSection>
              <DsInputSection number={2} title="증빙과 처리 정보" description={`${stageMeta[stage].evidence}를 먼저 첨부하세요.`}>
                <div className="logistics-create-form">
                  {!draft && stage === 'packing' ? <label>포장 메모 <input value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} placeholder="선택 입력" /></label> : null}
                  {!draft && stage === 'departure' ? <label>출발일 <input type="date" value={departureDate} onChange={(event) => setDepartureDate(event.target.value)} /></label> : null}
                  <label className="logistics-file-field">
                    <span>{stageMeta[stage].evidence} <b>필수</b></span>
                    <input type="file" accept={stage === 'delivery' ? 'image/jpeg,image/png,application/pdf' : 'image/jpeg,image/png'}
                      onChange={(event) => setFile(event.target.files?.[0] ?? null)} disabled={!canMutate || busy} />
                    <strong>{file?.name ?? (evidenceCount > 0 ? `등록된 증빙 ${evidenceCount}개 사용` : '파일을 먼저 선택하세요')}</strong>
                    <small>{stage === 'delivery' ? 'JPEG·PNG·PDF / 최대 10MB' : 'JPEG·PNG / 최대 5MB'}</small>
                  </label>
                  {stage !== 'delivery' ? <label>사진 설명 <input value={altText} maxLength={160} onChange={(event) => setAltText(event.target.value)} /></label> : null}
                  {selectionCrossesProjects ? <p className="logistics-inline-error">같은 프로젝트 대상만 함께 처리할 수 있습니다.</p> : null}
                  {selectedBlocked ? <p className="logistics-inline-error">Pending 또는 담당 권한을 확인해 주세요.</p> : null}
                  {!canMutate ? <p className="logistics-readonly">조회 전용입니다. 물류 담당자에게 처리를 요청해 주세요.</p> : null}
                </div>
              </DsInputSection>
              <DsActionBar description={`저장하면 증빙 등록과 ${stageMeta[stage].label} 확정이 연속 처리됩니다.`}>
                {draft ? <button type="button" className="logistics-secondary" onClick={() => void cancelDraft()} disabled={!canMutate || busy}>임시 작업 취소</button> : null}
                <button type="button" className="logistics-primary" onClick={() => void saveAndFinalize()}
                  disabled={!canMutate || busy || (!draft && (selected.size === 0 || selectionCrossesProjects || selectedBlocked)) || (!file && evidenceCount === 0)}>
                  {busy ? '저장 및 확정 중…' : `${stageMeta[stage].label} 저장 및 확정`}
                </button>
              </DsActionBar>
            </DsInputFlow>
          </aside>
        </div>
      ) : null}
    </section>
  );
}
