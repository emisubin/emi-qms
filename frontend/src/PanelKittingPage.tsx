import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, completePanelKitting, getPanelKittingQueue } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { workflowShapeRole } from './design-system';
import type { PanelKittingProject, PanelKittingQueueResponse } from './panelKitting';
import { SelectedExportTray } from './SelectedExcelExport';
import { useActionFeedback } from './useActionFeedback';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PanelKittingQueueResponse }
  | { kind: 'error'; message: string };

type OperationReceipt = {
  fingerprint: string;
  operationId: string;
};

export function PanelKittingPage({
  developmentUserKey,
  canComplete,
  initialProjectId,
  initialPanelId,
  onBack,
  onOpenReceipts
}: {
  developmentUserKey: string;
  canComplete: boolean;
  initialProjectId?: string;
  initialPanelId?: string;
  onBack: () => void;
  onOpenReceipts: () => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [selectedProjectId, setSelectedProjectId] = useState(initialProjectId ?? '');
  const [linkedPanelId, setLinkedPanelId] = useState(initialPanelId ?? '');
  const [selectedPanelIds, setSelectedPanelIds] = useState<string[]>([]);
  const [exportBusy, setExportBusy] = useState(false);
  const actions = useActionFeedback();
  const operationReceipt = useRef<OperationReceipt | null>(null);
  const linkedPanelRef = useRef<HTMLButtonElement | null>(null);
  const loadGenerationRef = useRef(0);

  const load = useCallback(async (preferredProjectId?: string, preserve = false) => {
    const generation = loadGenerationRef.current + 1;
    loadGenerationRef.current = generation;
    if (!preserve) setState({ kind: 'loading' });
    try {
      const data = await getPanelKittingQueue(developmentUserKey);
      if (generation !== loadGenerationRef.current) return false;
      setState({ kind: 'ready', data });
      setSelectedProjectId((current) => {
        const requested = preferredProjectId ?? current ?? initialProjectId;
        if (requested && data.projects.some((project) => project.projectId === requested)) {
          return requested;
        }
        return data.projects.find((project) => project.pendingPanelCount > 0)?.projectId
          ?? data.projects[0]?.projectId
          ?? '';
      });
      return true;
    } catch (error) {
      if (generation !== loadGenerationRef.current) return false;
      if (!preserve) setState({ kind: 'error', message: errorMessage(error, '키팅 작업 목록을 불러오지 못했습니다.') });
      return false;
    }
  }, [developmentUserKey, initialProjectId]);

  useEffect(() => {
    queueMicrotask(() => void load(initialProjectId));
  }, [initialProjectId, load]);

  useEffect(() => {
    queueMicrotask(() => setLinkedPanelId(initialPanelId ?? ''));
  }, [initialPanelId]);

  useEffect(() => {
    if (state.kind === 'ready' && linkedPanelId) {
      linkedPanelRef.current?.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }
  }, [linkedPanelId, selectedProjectId, state.kind]);

  const projects = state.kind === 'ready' ? state.data.projects : [];
  const selectedProject = projects.find((project) => project.projectId === selectedProjectId) ?? null;
  const selectablePanelIds = useMemo(
    () => selectedProject?.panels.filter((panel) => panel.selectable).map((panel) => panel.panelId) ?? [],
    [selectedProject]
  );
  const allSelectableSelected = selectablePanelIds.length > 0
    && selectablePanelIds.every((panelId) => selectedPanelIds.includes(panelId));

  function selectProject(projectId: string) {
    setSelectedProjectId(projectId);
    setSelectedPanelIds([]);
    setLinkedPanelId('');
    actions.reset();
    operationReceipt.current = null;
    const url = new URL(window.location.href);
    url.searchParams.set('project', projectId);
    url.searchParams.delete('panel');
    window.history.replaceState(null, '', `${url.pathname}?${url.searchParams.toString()}`);
  }

  function togglePanel(panelId: string) {
    setSelectedPanelIds((current) => current.includes(panelId)
      ? current.filter((candidate) => candidate !== panelId)
      : [...current, panelId]);
    actions.reset();
    operationReceipt.current = null;
  }

  async function submit() {
    if (!selectedProject || selectedPanelIds.length === 0 || !canComplete) {
      return;
    }

    const fingerprint = [...selectedPanelIds].sort().join('|');
    if (operationReceipt.current?.fingerprint !== fingerprint) {
      operationReceipt.current = { fingerprint, operationId: createOperationId() };
    }
    const currentReceipt = operationReceipt.current;
    if (!currentReceipt) return;

    let successMessage = '';
    const actionResult = await actions.run('kitting:bulk', async () => {
      const result = await completePanelKitting(developmentUserKey, {
        operationId: currentReceipt.operationId,
        projectId: selectedProject.projectId,
        panelIds: selectedPanelIds
      });
      successMessage = result.projectKittingCompleted
        ? `마지막 패널까지 키팅 완료 상태를 공유했습니다.`
        : `${result.completedPanelCount}면의 키팅 완료 상태를 공유했습니다.`;
    }, {
      loadingMessage: '선택한 패널의 키팅 완료 상태를 공유하는 중입니다.',
      successMessage: '키팅 완료 알림을 저장했습니다.',
      errorFallback: '키팅 완료 알림을 저장하지 못했습니다.',
      refresh: () => load(selectedProject.projectId, true),
      conflicts: { prefixes: ['kitting:'] }
    });
    if (actionResult === 'success' || actionResult === 'partial') {
      actions.setFeedback('kitting:bulk', {
        tone: actionResult,
        message: actionResult === 'partial'
          ? `${successMessage} 최신 패널 목록을 불러오지 못했습니다. 새로고침해 주세요.`
          : successMessage
      });
      setSelectedPanelIds([]);
      operationReceipt.current = null;
    }
  }

  return (
    <section
      className={isMobile ? 'page-surface panel-kitting-page panel-kitting-page--mobile' : 'page-surface panel-kitting-page'}
      data-testid="panel-kitting-page"
    >
      <header className="kitting-hero">
        <div className="kitting-hero-copy">
          <p className="eyebrow">PANEL HANDOFF</p>
          <h2>패널 키팅</h2>
          <p>실제로 키팅을 마친 패널을 선택해 생산관리와 제조팀에 자재 준비 상태를 알립니다.</p>
        </div>
        <div className="kitting-hero-actions">
          <button type="button" onClick={onBack}>프로젝트</button>
          <button type="button" onClick={onOpenReceipts}>입고 현황</button>
        </div>
        <span className="kitting-hero-orbit" aria-hidden="true" />
        <span className="kitting-hero-block" aria-hidden="true" />
      </header>

      {state.kind === 'loading' ? <KittingLoading /> : null}
      {state.kind === 'error' ? (
        <div className="kitting-state-card" role="alert">
          <strong>작업 목록을 확인할 수 없습니다.</strong>
          <span>{state.message}</span>
          <button type="button" onClick={() => void load(selectedProjectId)}>다시 불러오기</button>
        </div>
      ) : null}
      {state.kind === 'ready' && projects.length === 0 ? (
        <div className="kitting-state-card">
          <strong>키팅 대상 프로젝트가 없습니다.</strong>
          <span>진행 프로젝트의 구매품목과 활성 패널을 먼저 확인해 주세요.</span>
        </div>
      ) : null}

      {state.kind === 'ready' && projects.length > 0 ? (
        <div className="kitting-workspace">
          <ProjectQueue
            projects={projects}
            selectedProjectId={selectedProjectId}
            mobile={isMobile}
            onSelect={selectProject}
          />

          {selectedProject ? (
            <main className="kitting-panel-workspace">
              <ProjectReadiness project={selectedProject} />

              <SelectedExportTray
                developmentUserKey={developmentUserKey}
                screen="material-kitting"
                visibleIds={selectablePanelIds}
                selectedIds={new Set(selectedPanelIds)}
                allSelected={allSelectableSelected}
                busy={exportBusy}
                filters={{ projectId: selectedProject.projectId }}
                onBusyChange={setExportBusy}
                onToggleAll={(checked) => setSelectedPanelIds(checked ? selectablePanelIds : [])}
                onClear={() => setSelectedPanelIds([])}
              />

              <div className="kitting-selection-bar">
                <div>
                  <span className="kitting-selection-count">{selectedPanelIds.length}</span>
                  <span>선택</span>
                  <small>알림 가능 {selectablePanelIds.length}면</small>
                </div>
                <div className="kitting-selection-actions">
                  <button
                    type="button"
                    className="primary-button kitting-complete-button"
                    disabled={!canComplete || actions.isBusy('kitting:bulk') || selectedPanelIds.length === 0}
                    onClick={() => void submit()}
                  >
                    {actions.isBusy('kitting:bulk') ? '처리 중…' : `${selectedPanelIds.length}면 키팅 완료 알림`}
                  </button>
                </div>
              </div>

              {!canComplete ? (
                <p className="kitting-permission-note">조회만 가능합니다. 키팅 완료 알림은 자재 담당 권한이 필요합니다.</p>
              ) : null}
              {actions.latestFeedback ? <p className="action-feedback kitting-notice" data-tone={actions.latestFeedback.tone} role={actions.latestFeedback.tone === 'error' ? 'alert' : 'status'}>{actions.latestFeedback.message}</p> : null}

              <div className="kitting-panel-grid" aria-label={`${selectedProject.projectTitle} 패널 목록`}>
                {selectedProject.panels.map((panel) => {
                  const selected = selectedPanelIds.includes(panel.panelId);
                  return (
                    <button
                      key={panel.panelId}
                      type="button"
                      className="kitting-panel-card"
                      ref={panel.panelId === linkedPanelId ? linkedPanelRef : undefined}
                      data-panel-id={panel.panelId}
                      data-shape-role={workflowShapeRole({
                        completed: panel.kittingCompleted,
                        active: selected || panel.panelId === linkedPanelId,
                        inProgress: panel.selectable
                      })}
                      data-selected={selected}
                      data-completed={panel.kittingCompleted}
                      data-linked={panel.panelId === linkedPanelId}
                      disabled={!panel.selectable || exportBusy}
                      aria-pressed={selected}
                      onClick={() => togglePanel(panel.panelId)}
                    >
                      <span className="kitting-panel-check" aria-hidden="true">{panel.kittingCompleted ? '✓' : selected ? '✓' : ''}</span>
                      <span className="kitting-panel-copy">
                        <small>{panel.displayCode}</small>
                        <strong>{panel.panelName ?? '패널명 미입력'}</strong>
                        <span>{panelStatus(panel)}</span>
                      </span>
                      <span className="kitting-panel-status" data-ready={panel.selectable}>
                        {panel.kittingCompleted ? '완료' : panel.selectable ? '선택 가능' : '대기'}
                      </span>
                      {panel.panelId === linkedPanelId ? <span className="kitting-linked-label">연결된 패널</span> : null}
                    </button>
                  );
                })}
              </div>
            </main>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}

function ProjectQueue({
  projects,
  selectedProjectId,
  mobile,
  onSelect
}: {
  projects: PanelKittingProject[];
  selectedProjectId: string;
  mobile: boolean;
  onSelect: (projectId: string) => void;
}) {
  const pendingCount = projects.reduce((total, project) => total + project.pendingPanelCount, 0);
  return (
    <aside className="kitting-project-queue" aria-label="키팅 프로젝트 큐">
      <header>
        <div>
          <p className="eyebrow">{mobile ? 'PROJECT QUEUE' : 'ACTIVE PROJECTS'}</p>
          <h3>작업 프로젝트</h3>
        </div>
        <span className="kitting-queue-total">{pendingCount}</span>
      </header>
      <div className="kitting-project-list">
        {projects.map((project) => (
          <button
            key={project.projectId}
            type="button"
            className="kitting-project-card"
            data-active={project.projectId === selectedProjectId}
            onClick={() => onSelect(project.projectId)}
          >
            <span className="kitting-project-code">{project.projectCode}</span>
            <strong>{project.projectTitle}</strong>
            <span className="kitting-project-meta">
              <i data-ready={project.ready} />
              {project.ready ? '입고 완료 · 참고' : `입고 ${project.completedItemCount}/${project.activeItemCount} · 참고`}
            </span>
            <span className="kitting-project-panels">대기 <b>{project.pendingPanelCount}</b>면</span>
          </button>
        ))}
      </div>
    </aside>
  );
}

function ProjectReadiness({ project }: { project: PanelKittingProject }) {
  const totalPanels = project.pendingPanelCount + project.completedPanelCount;
  const percent = totalPanels === 0 ? 0 : Math.round((project.completedPanelCount / totalPanels) * 100);
  return (
    <section className="kitting-readiness" aria-label="선택 프로젝트 준비 상태">
      <div className="kitting-project-title">
        <span>{project.projectCode}</span>
        <h3>{project.projectTitle}</h3>
        <p>{project.ready ? '전체 입고 완료 · 실제 키팅을 마친 패널만 알려 주세요.' : `입고 ${project.completedItemCount}/${project.activeItemCount} · 실제 키팅 완료 여부는 지금 공유할 수 있습니다.`}</p>
      </div>
      <div className="kitting-readiness-metrics">
        <div className="kitting-progress-ring" style={{ '--progress': `${percent * 3.6}deg` } as React.CSSProperties}>
          <strong>{percent}%</strong>
          <span>키팅</span>
        </div>
        <dl>
          <div><dt>입고</dt><dd>{project.completedItemCount}/{project.activeItemCount}</dd></div>
          <div><dt>완료</dt><dd>{project.completedPanelCount}면</dd></div>
          <div><dt>남음</dt><dd>{project.pendingPanelCount}면</dd></div>
        </dl>
      </div>
      <span className="kitting-ready-pill" data-ready={project.ready}>
        {project.ready ? 'RECEIVED' : 'REFERENCE'}
      </span>
    </section>
  );
}

function KittingLoading() {
  return (
    <div className="kitting-loading" aria-label="키팅 목록 불러오는 중">
      <span /><span /><span />
    </div>
  );
}

function panelStatus(panel: PanelKittingProject['panels'][number]) {
  if (panel.kittingCompleted) {
    return panel.completedByDisplayName ? `${panel.completedByDisplayName} 완료` : '키팅 완료';
  }
  if (!panel.panelInfoCompleted) {
    return '패널정보 입력 필요';
  }
  return panel.selectable ? '키팅 완료 알림 가능' : '패널정보 입력 대기';
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}

function createOperationId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (value) => {
    const random = Math.floor(Math.random() * 16);
    const variant = value === 'x' ? random : (random & 0x3) | 0x8;
    return variant.toString(16);
  });
}
