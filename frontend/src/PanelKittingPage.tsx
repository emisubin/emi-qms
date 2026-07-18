import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, completePanelKitting, getPanelKittingQueue } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import type { PanelKittingProject, PanelKittingQueueResponse } from './panelKitting';
import { SelectedExportTray } from './SelectedExcelExport';

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
  const [saving, setSaving] = useState(false);
  const [exportBusy, setExportBusy] = useState(false);
  const [notice, setNotice] = useState<{ tone: 'success' | 'error'; message: string } | null>(null);
  const operationReceipt = useRef<OperationReceipt | null>(null);
  const linkedPanelRef = useRef<HTMLButtonElement | null>(null);

  const load = useCallback(async (preferredProjectId?: string) => {
    setState({ kind: 'loading' });
    try {
      const data = await getPanelKittingQueue(developmentUserKey);
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
    } catch (error) {
      setState({ kind: 'error', message: errorMessage(error, '키팅 작업 목록을 불러오지 못했습니다.') });
    }
  }, [developmentUserKey, initialProjectId]);

  useEffect(() => {
    queueMicrotask(() => void load(initialProjectId));
  }, [initialProjectId, load]);

  useEffect(() => {
    setLinkedPanelId(initialPanelId ?? '');
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
    setNotice(null);
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
    setNotice(null);
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

    setSaving(true);
    setNotice(null);
    try {
      const result = await completePanelKitting(developmentUserKey, {
        operationId: operationReceipt.current.operationId,
        projectId: selectedProject.projectId,
        panelIds: selectedPanelIds
      });
      setNotice({
        tone: 'success',
        message: result.projectKittingCompleted
          ? `마지막 패널까지 완료했습니다. 제조 업무 ${result.generatedWorkItemCount}건이 생성되었습니다.`
          : `${result.completedPanelCount}면을 완료하고 제조 업무 ${result.generatedWorkItemCount}건을 넘겼습니다.`
      });
      setSelectedPanelIds([]);
      operationReceipt.current = null;
      await load(selectedProject.projectId);
    } catch (error) {
      setNotice({ tone: 'error', message: errorMessage(error, '키팅 완료를 저장하지 못했습니다.') });
    } finally {
      setSaving(false);
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
          <p>입고 준비가 끝난 패널만 골라 제조 현장으로 바로 넘깁니다.</p>
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
                  <small>준비 패널 {selectablePanelIds.length}면</small>
                </div>
                <div className="kitting-selection-actions">
                  <button
                    type="button"
                    className="primary-button kitting-complete-button"
                    disabled={!canComplete || saving || selectedPanelIds.length === 0}
                    onClick={() => void submit()}
                  >
                    {saving ? '처리 중…' : `${selectedPanelIds.length}면 키팅 완료`}
                  </button>
                </div>
              </div>

              {!canComplete ? (
                <p className="kitting-permission-note">조회만 가능합니다. 키팅 완료는 자재 담당 권한이 필요합니다.</p>
              ) : null}
              {notice ? <p className="kitting-notice" data-tone={notice.tone} role="status">{notice.message}</p> : null}

              <div className="kitting-panel-grid" aria-label={`${selectedProject.projectTitle} 패널 목록`}>
                {selectedProject.panels.map((panel, index) => {
                  const selected = selectedPanelIds.includes(panel.panelId);
                  return (
                    <button
                      key={panel.panelId}
                      type="button"
                      className="kitting-panel-card"
                      ref={panel.panelId === linkedPanelId ? linkedPanelRef : undefined}
                      data-panel-id={panel.panelId}
                      data-shape={index % 4 === 0 ? 'angular' : index % 4 === 1 ? 'rounded' : index % 4 === 2 ? 'square' : 'soft'}
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
                      {panel.panelId === linkedPanelId ? <span className="kitting-linked-label">연결된 제조 대상</span> : null}
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
              {project.ready ? '입고 준비 완료' : `입고 ${project.completedItemCount}/${project.activeItemCount}`}
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
        <p>{project.ready ? '입고 조건 충족 · 패널을 선택해 제조로 넘기세요.' : '입고 완료 전에는 키팅을 진행할 수 없습니다.'}</p>
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
        {project.ready ? 'READY' : 'WAITING'}
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
  return panel.selectable ? '제조 인계 준비됨' : '입고 준비 대기';
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
