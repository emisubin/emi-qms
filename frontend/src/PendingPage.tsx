import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiError,
  addPendingComment,
  assignPendingIssue,
  createPendingIssue,
  getMaterialIqcQueue,
  getPendingIssue,
  getPendingActionPhotoBlob,
  listPendingAssignees,
  listPendingIssues,
  listPendingTypeFilterOptions,
  listPendingTypeManualOptions,
  listProjects,
  transitionPendingIssue,
  uploadPendingActionPhoto,
  removePendingActionPhoto
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { MobileSheet } from './MobileSheet';
import { OperationalProjectDashboard } from './OperationalProjectDashboard';
import type { ProjectListItem } from './projects';
import type {
  CreatePendingRequest,
  PendingAssignee,
  PendingDetail,
  PendingActionEvidence as PendingActionEvidenceModel,
  PendingActionPhoto,
  PendingIssue,
  PendingIssueType,
  PendingListResponse,
  PendingPriority,
  PendingReinspection,
  PendingStatus
} from './pending';
import type { PendingTypeOption } from './pendingTypes';
import { buildPendingTimeline } from './pendingTimeline';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type PendingPageProps = {
  developmentUserKey: string | undefined;
  pendingId?: string;
  initialProjectId?: string;
  canManage: boolean;
  onOpenPending: (pendingId: string) => void;
  onOpenProjectPending: (projectId: string) => void;
  onBackToList: () => void;
  onOpenProject: (projectId: string) => void;
  onOpenIqc: (attemptId: string) => void;
  onBadgeRefresh: () => void;
};

type AsyncState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'error'; message: string };

const emptyCreate: CreatePendingRequest = {
  projectId: '',
  issueType: '',
  title: '',
  description: '',
  priority: 'Normal',
  assigneeUserId: null,
  dueDate: null,
  actionDepartmentCode: null
};

const statusOptions: Array<{ value: PendingStatus | ''; label: string }> = [
  { value: '', label: '전체 상태' },
  { value: 'Registered', label: '등록' },
  { value: 'ActionRequested', label: '조치 요청' },
  { value: 'InProgress', label: '조치 중' },
  { value: 'ReinspectionRequested', label: '재검사 요청' },
  { value: 'Closed', label: '종결' }
];

const transitionLabels: Record<PendingStatus, string> = {
  Registered: '등록',
  ActionRequested: '조치 요청',
  InProgress: '조치 시작',
  ReinspectionRequested: '조치 완료',
  Closed: '종결'
};

export function PendingPage(props: PendingPageProps) {
  return props.pendingId
    ? <PendingDetailView {...props} pendingId={props.pendingId} />
    : <PendingListView {...props} />;
}

function PendingListView({
  developmentUserKey,
  initialProjectId,
  canManage,
  onOpenPending,
  onOpenProjectPending,
  onBackToList,
  onBadgeRefresh
}: PendingPageProps) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<AsyncState<PendingListResponse>>({ kind: 'loading' });
  const [status, setStatus] = useState<PendingStatus | ''>('');
  const [issueType, setIssueType] = useState<PendingIssueType | ''>('');
  const [priority, setPriority] = useState<PendingPriority | ''>('');
  const [projectId, setProjectId] = useState(initialProjectId ?? '');
  const [projectOptions, setProjectOptions] = useState<ProjectListItem[]>([]);
  const [projectsLoaded, setProjectsLoaded] = useState(false);
  const [typeOptions, setTypeOptions] = useState<PendingTypeOption[] | null>(null);
  const [typeOptionsError, setTypeOptionsError] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [draftStatus, setDraftStatus] = useState<PendingStatus | ''>('');
  const [draftIssueType, setDraftIssueType] = useState<PendingIssueType | ''>('');
  const [draftPriority, setDraftPriority] = useState<PendingPriority | ''>('');
  const mobileFilterTriggerRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      const data = await listPendingIssues(developmentUserKey, {
        status: status || undefined,
        issueType: issueType || undefined,
        priority: priority || undefined,
        projectId: projectId || undefined
      });
      setState({ kind: 'ready', data });
    } catch (error) {
      setState({ kind: 'error', message: messageForError(error) });
    }
  }, [developmentUserKey, issueType, priority, projectId, status]);

  useEffect(() => {
    let active = true;
    void listProjects(developmentUserKey, '', 'All', { pageSize: 100 })
      .then((response) => {
        if (active) {
          setProjectOptions(response.items);
          setProjectsLoaded(true);
        }
      })
      .catch(() => {
        if (active) {
          setProjectOptions([]);
          setProjectsLoaded(true);
        }
      });
    return () => { active = false; };
  }, [developmentUserKey]);

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (!active) return;
      setTypeOptions(null);
      setTypeOptionsError(false);
      void listPendingTypeFilterOptions(developmentUserKey)
        .then((options) => { if (active) setTypeOptions(options); })
        .catch(() => { if (active) setTypeOptionsError(true); });
    });
    return () => { active = false; };
  }, [developmentUserKey]);

  useEffect(() => {
    let active = true;
    void listPendingIssues(developmentUserKey, {
      status: status || undefined,
      issueType: issueType || undefined,
      priority: priority || undefined,
      projectId: projectId || undefined
    }).then((data) => {
      if (active) setState({ kind: 'ready', data });
    }).catch((error) => {
      if (active) setState({ kind: 'error', message: messageForError(error) });
    });
    return () => { active = false; };
  }, [developmentUserKey, issueType, priority, projectId, status]);

  const items = state.kind === 'ready' ? state.data.items : [];
  const pendingVisibleIds = items.map((item) => item.pendingId);
  const pendingSelection = useSelectedRows(pendingVisibleIds);
  const activeFilterCount = [status, issueType, priority].filter(Boolean).length;

  if (!initialProjectId) {
    if (state.kind === 'loading' || !projectsLoaded) {
      return <section className="page-surface pending-page"><PendingLoading /></section>;
    }
    if (state.kind === 'error') {
      return <section className="page-surface pending-page"><PendingError message={state.message} onRetry={load} /></section>;
    }

    const issuesByProject = new Map<string, PendingIssue[]>();
    for (const item of state.data.items) {
      issuesByProject.set(item.projectId, [...(issuesByProject.get(item.projectId) ?? []), item]);
    }

    return (
      <>
        <OperationalProjectDashboard
          testId="pending-dashboard"
          eyebrow="이슈 관리 · 프로젝트 대기열"
          title="Pending 프로젝트"
          description="프로젝트별 진행 중·긴급·기한 초과 이슈를 확인한 뒤 한 프로젝트 안에서 조치와 재검사를 처리합니다."
          unitLabel="이슈"
          columnLabels={{
            total: '전체',
            ready: '진행 중',
            inProgress: '긴급',
            blocked: '기한 초과',
            completed: '종결'
          }}
          metrics={[
            { label: '진행 중', value: state.data.summary.openCount, helper: '종결 전 전체 이슈' },
            { label: '긴급', value: state.data.summary.urgentCount, helper: '즉시 확인 필요', tone: 'warning' },
            { label: '기한 초과', value: state.data.summary.overdueCount, helper: '조치 기한 경과', tone: 'warning' },
            { label: '재검사 대기', value: state.data.summary.reinspectionCount, helper: '품질 판정 필요' }
          ]}
          projects={projectOptions.map((project) => {
            const projectIssues = issuesByProject.get(project.projectId) ?? [];
            const openCount = projectIssues.filter((item) => item.status !== 'Closed').length;
            const urgentCount = projectIssues.filter((item) => item.status !== 'Closed' && item.priority === 'Urgent').length;
            const overdueCount = projectIssues.filter((item) => item.status !== 'Closed' && item.isOverdue).length;
            const reinspectionCount = projectIssues.filter((item) => item.status === 'ReinspectionRequested').length;
            const closedCount = projectIssues.filter((item) => item.status === 'Closed').length;
            return {
              projectId: project.projectId,
              projectCode: project.projectCode,
              projectTitle: project.projectTitle,
              totalCount: projectIssues.length,
              readyCount: openCount,
              inProgressCount: urgentCount,
              blockedCount: overdueCount,
              completedCount: closedCount,
              detail: `재검사 ${reinspectionCount}건 · ${project.customerName ?? '고객사 미입력'}`,
              inProgressTone: 'warning' as const
            };
          }).sort((left, right) =>
            right.readyCount - left.readyCount
            || right.inProgressCount - left.inProgressCount
            || right.blockedCount - left.blockedCount
            || left.projectCode.localeCompare(right.projectCode, 'ko-KR'))}
          emptyMessage="조회할 프로젝트가 없습니다."
          primaryAction={canManage ? {
            label: '+ Pending 등록',
            disabled: typeOptions === null || typeOptionsError,
            onClick: () => setShowCreate(true)
          } : undefined}
          onOpenProject={onOpenProjectPending}
        />
        {feedback ? <p className="action-feedback" data-tone="success" role="status">{feedback}</p> : null}
        {typeOptionsError ? <p className="action-feedback" data-tone="error" role="alert">Pending 유형을 불러오지 못해 신규 등록을 안전하게 차단했습니다. 새로고침해 주세요.</p> : null}
        {showCreate ? (
          <PendingCreateDialog
            developmentUserKey={developmentUserKey}
            onClose={() => setShowCreate(false)}
            onCreated={(detail) => {
              setShowCreate(false);
              setFeedback(`#${padIssueNumber(detail.issue.issueNumber)} Pending이 등록되었습니다.`);
              void load();
              onBadgeRefresh();
            }}
          />
        ) : null}
      </>
    );
  }

  const selectedProject = projectOptions.find((project) => project.projectId === initialProjectId);
  const selectedProjectTitle = selectedProject?.projectTitle ?? items[0]?.projectTitle ?? 'Pending 프로젝트';
  const selectedProjectCode = selectedProject?.projectCode ?? items[0]?.projectCode ?? '프로젝트';

  return (
    <section className={isMobile ? 'page-surface pending-page pending-page--project mobile-first-page' : 'page-surface pending-page pending-page--project'} aria-labelledby="pending-title">
      <header className={isMobile ? 'page-header pending-header mobile-page-header' : 'page-header pending-header'}>
        <div>
          <p className="eyebrow">PENDING · PROJECT</p>
          <h2 id="pending-title">{selectedProjectTitle}</h2>
          <p className="muted-text">{selectedProjectCode} · 이 프로젝트의 등록·조치·재검사·종결 이력만 표시합니다.</p>
        </div>
        <div className="pending-project-actions">
          <button type="button" onClick={onBackToList}>Pending 프로젝트</button>
          {canManage ? <button className="primary-button" type="button" disabled={typeOptions === null || typeOptionsError} onClick={() => setShowCreate(true)}>+ Pending 등록</button> : null}
        </div>
      </header>

      {typeOptionsError ? <p className="action-feedback" data-tone="error" role="alert">Pending 유형을 불러오지 못해 유형 필터와 신규 등록을 안전하게 차단했습니다. 새로고침해 주세요.</p> : null}

      {state.kind === 'ready' ? <PendingSummaryCards data={state.data} /> : null}

      {isMobile ? (
        <>
          <button
            ref={mobileFilterTriggerRef}
            type="button"
            className="mobile-filter-trigger"
            aria-expanded={mobileFiltersOpen}
            onClick={() => {
              setDraftStatus(status);
              setDraftIssueType(issueType);
              setDraftPriority(priority);
              setMobileFiltersOpen(true);
            }}
          >
            <span><strong>Pending 필터</strong><small>{activeFilterCount ? `${activeFilterCount}개 조건 적용 중` : '긴급도·상태·유형으로 찾기'}</small></span>
            <span aria-hidden="true">⌕</span>
          </button>
          <MobileSheet
            open={mobileFiltersOpen}
            title="Pending 필터"
            eyebrow="ISSUE FILTER"
            description="조건을 고른 뒤 적용하세요. 취소하면 현재 목록 조건이 유지됩니다."
            onClose={() => setMobileFiltersOpen(false)}
            triggerRef={mobileFilterTriggerRef}
            fullScreen
            footer={(
              <>
                <button type="button" onClick={() => { setDraftStatus(''); setDraftIssueType(''); setDraftPriority(''); }}>초기화</button>
                <button type="button" onClick={() => setMobileFiltersOpen(false)}>취소</button>
                <button
                  type="button"
                  className="primary-button"
                  onClick={() => {
                    setProjectId(initialProjectId);
                    setStatus(draftStatus);
                    setIssueType(draftIssueType);
                    setPriority(draftPriority);
                    setMobileFiltersOpen(false);
                  }}
                >
                  조건 적용
                </button>
              </>
            )}
          >
            <div className="mobile-filter-form">
              <label><span>상태</span><select data-autofocus value={draftStatus} onChange={(event) => setDraftStatus(event.target.value as PendingStatus | '')}>{statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
              <label><span>유형</span><select disabled={typeOptions === null || typeOptionsError} value={draftIssueType} onChange={(event) => setDraftIssueType(event.target.value as PendingIssueType | '')}><option value="">전체 유형</option>{typeOptions?.map((option) => <option key={option.code} value={option.code}>{option.displayName}{option.isActive ? '' : ' · 사용 중지'}</option>)}</select></label>
              <label><span>긴급도</span><select value={draftPriority} onChange={(event) => setDraftPriority(event.target.value as PendingPriority | '')}><option value="">전체 긴급도</option><option value="Urgent">긴급</option><option value="Normal">일반</option></select></label>
            </div>
          </MobileSheet>
        </>
      ) : (
        <div className="pending-filter-bar pending-filter-bar--project" aria-label="Pending 필터">
          <label><span>상태</span><select value={status} onChange={(event) => setStatus(event.target.value as PendingStatus | '')}>{statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
          <label><span>유형</span><select disabled={typeOptions === null || typeOptionsError} value={issueType} onChange={(event) => setIssueType(event.target.value as PendingIssueType | '')}><option value="">전체 유형</option>{typeOptions?.map((option) => <option key={option.code} value={option.code}>{option.displayName}{option.isActive ? '' : ' · 사용 중지'}</option>)}</select></label>
          <label><span>긴급도</span><select value={priority} onChange={(event) => setPriority(event.target.value as PendingPriority | '')}><option value="">전체 긴급도</option><option value="Urgent">긴급</option><option value="Normal">일반</option></select></label>
          <button type="button" onClick={() => { setStatus(''); setIssueType(''); setPriority(''); }}>필터 초기화</button>
        </div>
      )}

      {state.kind === 'ready' ? (
        <SelectedExportTray
          developmentUserKey={developmentUserKey}
          screen="pending"
          visibleIds={pendingVisibleIds}
          selectedIds={pendingSelection.selectedIds}
          allSelected={pendingSelection.allSelected}
          busy={pendingSelection.busy}
          filters={{ status: status || undefined, issueType: issueType || undefined, priority: priority || undefined, projectId: projectId || undefined }}
          onBusyChange={pendingSelection.setBusy}
          onToggleAll={pendingSelection.toggleAll}
          onClear={pendingSelection.clear}
        />
      ) : null}

      {feedback ? <p className="action-feedback" data-tone="success" role="status">{feedback}</p> : null}
      {state.kind === 'loading' ? <PendingLoading /> : null}
      {state.kind === 'error' ? <PendingError message={state.message} onRetry={load} /> : null}
      {state.kind === 'ready' && items.length === 0 ? <PendingEmpty canManage={canManage} onCreate={() => setShowCreate(true)} /> : null}
      {state.kind === 'ready' && items.length > 0 ? (
        <div className="pending-list" aria-label={`Pending ${items.length}건`}>
          {items.map((item) => (
            <PendingCard
              key={item.pendingId}
              item={item}
              selected={pendingSelection.selectedIds.has(item.pendingId)}
              selectionBusy={pendingSelection.busy}
              onSelectionChange={(checked) => pendingSelection.toggle(item.pendingId, checked)}
              onOpen={() => onOpenPending(item.pendingId)}
            />
          ))}
        </div>
      ) : null}

      {showCreate ? (
        <PendingCreateDialog
          developmentUserKey={developmentUserKey}
          initialProjectId={initialProjectId}
          onClose={() => setShowCreate(false)}
          onCreated={(detail) => {
            setShowCreate(false);
            setFeedback(`#${padIssueNumber(detail.issue.issueNumber)} Pending이 등록되었습니다.`);
            void load();
            onBadgeRefresh();
          }}
        />
      ) : null}
    </section>
  );
}

function PendingSummaryCards({ data }: { data: PendingListResponse }) {
  const cards = [
    ['진행 중', data.summary.openCount, 'open'],
    ['긴급', data.summary.urgentCount, 'urgent'],
    ['기한 초과', data.summary.overdueCount, 'overdue'],
    ['재검사 대기', data.summary.reinspectionCount, 'reinspection'],
    ['종결', data.summary.closedCount, 'closed']
  ] as const;
  return <div className="pending-summary-grid">{cards.map(([label, value, tone]) => <div key={label} data-tone={tone}><span>{label}</span><strong>{value}</strong></div>)}</div>;
}

function PendingCard({ item, selected, selectionBusy, onSelectionChange, onOpen }: { item: PendingIssue; selected: boolean; selectionBusy: boolean; onSelectionChange: (selected: boolean) => void; onOpen: () => void }) {
  return (
    <article className="pending-card selected-export-row" data-priority={item.priority} data-overdue={item.isOverdue}>
      <SelectionCheckbox checked={selected} disabled={selectionBusy} label={`Pending ${item.issueNumber} 선택`} onChange={onSelectionChange} />
      <div className="pending-card-main">
        <div className="pending-card-badges">
          <span className="pending-number">P-{padIssueNumber(item.issueNumber)}</span>
          <span className="status-badge" data-tone={pendingStatusTone(item.status)}>{item.statusLabel}</span>
          <span className="status-badge" data-tone={item.priority === 'Urgent' ? 'danger' : 'neutral'}>{item.priorityLabel}</span>
          <span className="status-badge" data-tone="info">{item.issueTypeLabel}</span>
        </div>
        <button className="pending-title-button" type="button" onClick={onOpen}>{item.title}</button>
        <p>{item.description}</p>
        <div className="pending-card-meta">
          <span>담당 {item.assigneeDisplayName ?? '미지정'}</span>
          {item.targetType === 'Panel' && item.targetLabel ? <span>패널 {item.targetLabel}</span> : null}
          {item.actionDepartmentCode ? <span>조치 부서 {departmentLabel(item.actionDepartmentCode)}</span> : null}
          <span className={item.isOverdue ? 'negative-text' : ''}>기한 {item.dueDate ? formatDate(item.dueDate) : '미정'}{item.isOverdue ? ' · 초과' : ''}</span>
        </div>
      </div>
      <button type="button" onClick={onOpen}>상세 보기</button>
    </article>
  );
}

function PendingCreateDialog({
  developmentUserKey,
  initialProjectId,
  onClose,
  onCreated
}: {
  developmentUserKey: string | undefined;
  initialProjectId?: string;
  onClose: () => void;
  onCreated: (detail: PendingDetail) => void;
}) {
  const [form, setForm] = useState<CreatePendingRequest>({ ...emptyCreate, projectId: initialProjectId ?? '' });
  const [projects, setProjects] = useState<ProjectListItem[]>([]);
  const [assignees, setAssignees] = useState<PendingAssignee[]>([]);
  const [typeOptions, setTypeOptions] = useState<PendingTypeOption[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const departmentCodes = useMemo(
    () => Array.from(new Set(assignees.map((assignee) => assignee.departmentCode))),
    [assignees]
  );

  useEffect(() => {
    let active = true;
    void Promise.allSettled([listProjects(developmentUserKey), listPendingAssignees(developmentUserKey), listPendingTypeManualOptions(developmentUserKey)])
      .then(([projectResult, assigneeResult, typeResult]) => {
        if (!active) return;
        if (projectResult.status === 'fulfilled') {
          setProjects(projectResult.value.items.filter((item) => item.status === 'Active' || item.status === 'OnHold'));
        }
        if (assigneeResult.status === 'fulfilled') {
          setAssignees(assigneeResult.value);
        }
        if (typeResult.status === 'fulfilled' && typeResult.value.length > 0) {
          setTypeOptions(typeResult.value);
          setForm((current) => ({ ...current, issueType: typeResult.value[0].code }));
        } else {
          setTypeOptions([]);
          setError('Pending 유형 목록을 불러오지 못해 등록을 차단했습니다. 창을 닫고 다시 시도해 주세요.');
        }
        if (projectResult.status === 'rejected' && assigneeResult.status === 'rejected') {
          setError('프로젝트와 담당자 목록을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.');
        } else if (projectResult.status === 'rejected') {
          setError('프로젝트 목록을 불러오지 못했습니다. 창을 닫고 다시 시도해 주세요.');
        } else if (assigneeResult.status === 'rejected') {
          setError('담당자 목록을 불러오지 못했습니다. 담당자를 비워 등록하거나 다시 시도해 주세요.');
        }
        setLoadingOptions(false);
      })
      .catch((loadError) => {
        if (!active) return;
        setError(messageForError(loadError));
        setLoadingOptions(false);
      });
    return () => { active = false; };
  }, [developmentUserKey]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    if (!form.projectId || !form.issueType || form.title.trim().length < 3 || form.description.trim().length < 10) {
      setError('프로젝트, Pending 유형, 3자 이상의 제목, 10자 이상의 상세 내용을 입력해 주세요.');
      return;
    }
    if (form.issueType === 'ManufacturingStop' && !form.actionDepartmentCode) {
      setError('제조 중단 Pending은 조치 담당 부서를 선택해 주세요.');
      return;
    }
    setSubmitting(true);
    try {
      onCreated(await createPendingIssue(developmentUserKey, form));
    } catch (submitError) {
      setError(messageForError(submitError));
      setSubmitting(false);
    }
  }

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="dialog pending-create-dialog" role="dialog" aria-modal="true" aria-labelledby="pending-create-title">
        <header className="page-header"><div><p className="eyebrow">NEW ISSUE</p><h2 id="pending-create-title">Pending 등록</h2></div><button type="button" onClick={onClose}>닫기</button></header>
        <form className="pending-create-form" onSubmit={submit}>
          <label className="form-field"><span>프로젝트 *</span><select disabled={loadingOptions} value={form.projectId} onChange={(event) => setForm({ ...form, projectId: event.target.value })}><option value="">프로젝트 선택</option>{projects.map((project) => <option key={project.projectId} value={project.projectId}>{project.projectCode} · {project.projectTitle}</option>)}</select></label>
          <label className="form-field"><span>유형 *</span><select disabled={loadingOptions || typeOptions.length === 0} value={form.issueType} onChange={(event) => setForm({ ...form, issueType: event.target.value as PendingIssueType, actionDepartmentCode: event.target.value === 'ManufacturingStop' ? form.actionDepartmentCode : null })}><option value="">유형 선택</option>{typeOptions.map((option) => <option key={option.code} value={option.code}>{option.displayName}</option>)}</select></label>
          <label className="form-field"><span>긴급도 *</span><select value={form.priority} onChange={(event) => setForm({ ...form, priority: event.target.value as PendingPriority })}><option value="Normal">일반</option><option value="Urgent">긴급 · 업무 차단</option></select></label>
          {form.issueType === 'ManufacturingStop' ? <label className="form-field"><span>조치 담당 부서 *</span><select disabled={loadingOptions} value={form.actionDepartmentCode ?? ''} onChange={(event) => setForm({ ...form, actionDepartmentCode: event.target.value || null, assigneeUserId: null })}><option value="">부서 선택</option>{departmentCodes.map((code) => <option key={code} value={code}>{departmentLabel(code)}</option>)}</select></label> : null}
          <label className="form-field"><span>조치 담당</span><select disabled={loadingOptions} value={form.assigneeUserId ?? ''} onChange={(event) => { const assignee = assignees.find((item) => item.userId === event.target.value); setForm({ ...form, assigneeUserId: event.target.value || null, actionDepartmentCode: form.issueType === 'ManufacturingStop' ? assignee?.departmentCode ?? form.actionDepartmentCode : form.actionDepartmentCode }); }}><option value="">나중에 지정</option>{assignees.filter((assignee) => form.issueType !== 'ManufacturingStop' || !form.actionDepartmentCode || assignee.departmentCode === form.actionDepartmentCode).map((assignee) => <option key={assignee.userId} value={assignee.userId}>{assignee.displayName} · {departmentLabel(assignee.departmentCode)}</option>)}</select></label>
          <label className="form-field"><span>조치 기한</span><input type="date" value={form.dueDate ?? ''} onChange={(event) => setForm({ ...form, dueDate: event.target.value || null })} /></label>
          <label className="form-field pending-full-field"><span>제목 *</span><input maxLength={160} value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} placeholder="무엇이 업무를 막고 있는지 한 줄로 입력" /></label>
          <label className="form-field pending-full-field"><span>상세 내용 *</span><textarea maxLength={2000} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} placeholder="발생 위치, 현상, 영향, 필요한 조치를 입력해 주세요." /></label>
          <div className="pending-attachment-note pending-full-field"><strong>사진 첨부 안내</strong><span>조치 근거 사진은 담당자가 조치를 시작한 뒤 Pending 상세에서 등록할 수 있습니다.</span></div>
          {error ? <p className="action-feedback pending-full-field" data-tone="error" role="alert">{error}</p> : null}
          <div className="dialog-actions pending-full-field"><button type="button" onClick={onClose}>취소</button><button className="primary-button" disabled={submitting || loadingOptions || typeOptions.length === 0} type="submit">{submitting ? '등록 중…' : 'Pending 등록'}</button></div>
        </form>
      </section>
    </div>
  );
}

function PendingDetailView({
  developmentUserKey,
  pendingId,
  canManage,
  onBackToList,
  onOpenProject,
  onOpenIqc,
  onBadgeRefresh
}: PendingPageProps & { pendingId: string }) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<AsyncState<PendingDetail>>({ kind: 'loading' });
  const [assignees, setAssignees] = useState<PendingAssignee[]>([]);
  const [reason, setReason] = useState('');
  const [comment, setComment] = useState('');
  const [nextAssignee, setNextAssignee] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [discoveredReinspection, setDiscoveredReinspection] = useState<PendingReinspection | null>(null);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoAlt, setPhotoAlt] = useState('');

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      setState({ kind: 'ready', data: await getPendingIssue(developmentUserKey, pendingId) });
    } catch (error) {
      setState({ kind: 'error', message: messageForError(error) });
    }
  }, [developmentUserKey, pendingId]);

  useEffect(() => {
    let active = true;
    void getPendingIssue(developmentUserKey, pendingId).then((data) => {
      if (active) setState({ kind: 'ready', data });
    }).catch((error) => {
      if (active) setState({ kind: 'error', message: messageForError(error) });
    });
    return () => { active = false; };
  }, [developmentUserKey, pendingId]);
  useEffect(() => {
    if (!canManage) return;
    void listPendingAssignees(developmentUserKey).then(setAssignees).catch(() => setAssignees([]));
  }, [canManage, developmentUserKey]);

  const detail = state.kind === 'ready' ? state.data : null;
  const timeline = useMemo(() => detail ? buildPendingTimeline(detail) : [], [detail]);
  useEffect(() => {
    if (!detail || detail.issue.status !== 'ReinspectionRequested' || detail.reinspection) {
      setDiscoveredReinspection(null);
      return;
    }
    let active = true;
    void getMaterialIqcQueue(developmentUserKey, false).then((response) => {
      if (!active) return;
      const attempt = response.items.find((item) => item.pendingIssueId === pendingId && item.status === 'Requested');
      setDiscoveredReinspection(attempt ? {
        attemptId: attempt.attemptId,
        attemptNumber: attempt.attemptNumber,
        orderItem: attempt.orderItem,
        quantity: attempt.quantity,
        unit: attempt.unit,
        linkUrl: `/quality/iqc?request=${attempt.attemptId}`
      } : null);
    }).catch(() => active && setDiscoveredReinspection(null));
    return () => { active = false; };
  }, [detail, developmentUserKey, pendingId]);

  async function runMutation(operation: () => Promise<PendingDetail>, successMessage: string, resetFields?: () => void) {
    setBusy(true);
    setFeedback(null);
    try {
      const updated = await operation();
      resetFields?.();
      setState({ kind: 'ready', data: updated });
      setFeedback(successMessage);
      onBadgeRefresh();
    } catch (error) {
      setFeedback(messageForError(error));
    } finally {
      setBusy(false);
    }
  }

  async function uploadActionPhoto() {
    if (!detail || !photoFile || !photoAlt.trim()) return;
    setBusy(true);
    setFeedback(null);
    try {
      const result = await uploadPendingActionPhoto(
        developmentUserKey,
        pendingId,
        crypto.randomUUID(),
        detail.issue.version,
        photoAlt.trim(),
        photoFile
      );
      setState({ kind: 'ready', data: result.detail });
      setPhotoFile(null);
      setPhotoAlt('');
      setFeedback('조치 사진을 등록했습니다. 조치 완료 시 변경할 수 없는 근거로 확정됩니다.');
    } catch (error) {
      setFeedback(messageForError(error));
    } finally {
      setBusy(false);
    }
  }

  async function removeActionPhoto(photoId: string) {
    if (!detail) return;
    setBusy(true);
    setFeedback(null);
    try {
      const result = await removePendingActionPhoto(
        developmentUserKey,
        pendingId,
        photoId,
        crypto.randomUUID(),
        detail.issue.version
      );
      setState({ kind: 'ready', data: result.detail });
      setFeedback('확정 전 조치 사진을 삭제했습니다.');
    } catch (error) {
      setFeedback(messageForError(error));
    } finally {
      setBusy(false);
    }
  }

  if (state.kind === 'loading') return <section className="page-surface pending-page"><PendingLoading /></section>;
  if (state.kind === 'error') return <section className="page-surface pending-page"><PendingError message={state.message} onRetry={load} /><button type="button" onClick={onBackToList}>목록으로</button></section>;
  if (!detail) return null;
  const issue = detail.issue;
  const reinspection = detail.reinspection ?? discoveredReinspection;
  const nextTransition = detail.allowedTransitions[0];
  const completingAction = nextTransition === 'ReinspectionRequested';

  return (
    <section className={isMobile ? 'page-surface pending-page mobile-first-page mobile-pending-detail-page' : 'page-surface pending-page'} aria-labelledby="pending-detail-title">
      <header className={isMobile ? 'pending-detail-header mobile-detail-hero' : 'page-header pending-detail-header'}>
        <div>
          <button className={isMobile ? 'mobile-back-button' : 'link-button'} type="button" onClick={onBackToList}>← Pending List</button>
          <div className="pending-card-badges"><span className="pending-number">P-{padIssueNumber(issue.issueNumber)}</span><span className="status-badge" data-tone={pendingStatusTone(issue.status)}>{issue.statusLabel}</span><span className="status-badge" data-tone={issue.priority === 'Urgent' ? 'danger' : 'neutral'}>{issue.priorityLabel}</span><span className="status-badge" data-tone="info">{issue.issueTypeLabel}</span></div>
          <h2 id="pending-detail-title">{issue.title}</h2>
          <button className="link-button" type="button" onClick={() => onOpenProject(issue.projectId)}>{issue.projectCode} · {issue.projectTitle}</button>
        </div>
        <span className={issue.isOverdue ? 'pending-due negative-text' : 'pending-due'}>조치 기한<br /><strong>{issue.dueDate ? formatDate(issue.dueDate) : '미정'}</strong></span>
      </header>

      {feedback ? <p className="action-feedback" data-tone={feedback.includes('수 없습니다') || feedback.includes('필요') ? 'error' : 'success'} role="status">{feedback}</p> : null}

      <div className="pending-detail-layout">
        <div className="pending-detail-main">
          <section className="pending-section"><h3>발생 내용</h3><p className="pending-description">{issue.description}</p><dl className="pending-facts"><div><dt>등록자</dt><dd>{issue.createdByDisplayName}</dd></div><div><dt>조치 담당</dt><dd>{issue.assigneeDisplayName ?? '미지정'}</dd></div>{issue.targetType === 'Panel' ? <div><dt>대상 패널</dt><dd>{issue.targetLabel ?? '패널'}</dd></div> : null}{issue.actionDepartmentCode ? <div><dt>조치 부서</dt><dd>{departmentLabel(issue.actionDepartmentCode)}</dd></div> : null}<div><dt>등록일</dt><dd>{formatDateTime(issue.createdAtUtc)}</dd></div><div><dt>최근 변경</dt><dd>{formatDateTime(issue.updatedAtUtc)}</dd></div></dl></section>

          {issue.status === 'ReinspectionRequested' ? (
            <section className="pending-section pending-reinspection-action" aria-labelledby="pending-reinspection-title">
              <div>
                <p className="eyebrow">QUALITY REINSPECTION</p>
                <h3 id="pending-reinspection-title">품질 재검사에서 판정해 주세요</h3>
                {reinspection ? (
                  <p><strong>{reinspection.orderItem ?? issue.targetLabel ?? '검사 품목'}</strong> · {formatPendingQuantity(reinspection.quantity, reinspection.unit)} · {reinspection.attemptNumber}차 검사</p>
                ) : <p>재검사 업무를 준비하고 있습니다. 잠시 후 다시 확인해 주세요.</p>}
              </div>
              <button className="primary-button" type="button" disabled={!reinspection} onClick={() => reinspection && onOpenIqc(reinspection.attemptId)}>품질 재검사 열기</button>
            </section>
          ) : null}

          {canManage && detail.canAssign ? <section className="pending-section"><h3>담당 변경</h3><div className="pending-inline-action"><select aria-label="새 조치 담당" value={nextAssignee} onChange={(event) => setNextAssignee(event.target.value)}><option value="">새 담당자 선택</option>{assignees.filter((item) => item.userId !== issue.assigneeUserId).map((item) => <option key={item.userId} value={item.userId}>{item.displayName} · {departmentLabel(item.departmentCode)}</option>)}</select><input aria-label="담당 변경 사유" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="변경 사유 (3자 이상)" /><button disabled={busy || !nextAssignee || reason.trim().length < 3} type="button" onClick={() => void runMutation(() => assignPendingIssue(developmentUserKey, pendingId, nextAssignee, issue.version, reason), '조치 담당자가 변경되었습니다.', () => { setReason(''); setNextAssignee(''); })}>담당 변경</button></div></section> : null}

          <PendingActionEvidence
            pendingId={pendingId}
            evidence={detail.actionEvidence}
            developmentUserKey={developmentUserKey}
            busy={busy}
            photoFile={photoFile}
            photoAlt={photoAlt}
            onPhotoFile={setPhotoFile}
            onPhotoAlt={setPhotoAlt}
            onUpload={() => void uploadActionPhoto()}
            onRemove={(photoId) => void removeActionPhoto(photoId)}
          />

          {nextTransition ? <section className="pending-section pending-next-action"><div><p className="eyebrow">NEXT ACTION</p><h3>{transitionLabels[nextTransition]}</h3><p>{completingAction ? '처리 내용을 남기고 완료하면 품질 재검사 업무와 알림이 자동 생성됩니다.' : '현재 상태를 확인하고 처리 내용을 남겨 다음 단계로 넘깁니다.'}</p></div><div className="pending-transition-control"><input aria-label="처리 내용" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="처리 내용 또는 확인 결과 (3자 이상)" /><button className="primary-button" disabled={busy || reason.trim().length < 3} type="button" onClick={() => void runMutation(() => transitionPendingIssue(developmentUserKey, pendingId, nextTransition, issue.version, reason), completingAction ? '조치를 완료하고 품질 재검사 업무를 생성했습니다.' : `${transitionLabels[nextTransition]} 상태로 변경되었습니다.`, () => setReason(''))}>{transitionLabels[nextTransition]}</button></div></section> : null}
        </div>

        <section className="pending-timeline"><div className="subsection-header"><div><p className="eyebrow">ACTIVITY</p><h3>코멘트와 처리 이력</h3></div><span>{timeline.length}건</span></div>{detail.canComment ? <form className="pending-comment-form pending-timeline-composer" onSubmit={(event) => { event.preventDefault(); if (comment.trim()) void runMutation(() => addPendingComment(developmentUserKey, pendingId, comment), '코멘트와 처리 이력에 추가했습니다.', () => setComment('')); }}><label><span>새 코멘트</span><textarea aria-label="처리 활동 코멘트" value={comment} onChange={(event) => setComment(event.target.value)} placeholder="조치 내용, 확인 결과 또는 다음 담당자에게 전달할 내용을 남겨 주세요." /></label><button className="primary-button" disabled={busy || !comment.trim()} type="submit">코멘트 등록</button></form> : null}<div className="pending-timeline-list">{timeline.map((event) => <article key={event.key}><span className="pending-timeline-dot" /><div><strong>{event.title}</strong>{event.summary ? <p>{event.summary}</p> : null}{event.detail ? <p>{event.detail}</p> : null}{event.note ? <p>{event.note}</p> : null}<small>{event.actor} · {formatDateTime(event.createdAtUtc)}</small></div></article>)}</div></section>
      </div>
    </section>
  );
}

function PendingActionEvidence({
  pendingId,
  evidence,
  developmentUserKey,
  busy,
  photoFile,
  photoAlt,
  onPhotoFile,
  onPhotoAlt,
  onUpload,
  onRemove
}: {
  pendingId: string;
  evidence: PendingActionEvidenceModel;
  developmentUserKey: string | undefined;
  busy: boolean;
  photoFile: File | null;
  photoAlt: string;
  onPhotoFile: (file: File | null) => void;
  onPhotoAlt: (value: string) => void;
  onUpload: () => void;
  onRemove: (photoId: string) => void;
}) {
  const drafts = evidence.draftPhotos ?? [];
  return (
    <section className="pending-section pending-action-evidence" aria-labelledby="pending-action-evidence-title">
      <header>
        <div>
          <p className="eyebrow">ACTION EVIDENCE</p>
          <h3 id="pending-action-evidence-title">조치 내용과 사진</h3>
        </div>
        <span>{evidence.confirmedRounds.reduce((sum, round) => sum + round.photos.length, 0)}장 확정</span>
      </header>

      {evidence.canManageDraft ? (
        <div className="pending-action-photo-editor">
          <p>사진은 선택 사항입니다. <strong>조치 완료</strong>를 누르면 현재 사진이 확정되어 수정·삭제할 수 없습니다.</p>
          <div className="pending-action-photo-limits">
            <span>이번 회차 {evidence.remainingPhotosThisRound}/{evidence.maxPhotosPerRound}장 남음</span>
            <span>{formatBytes(evidence.remainingBytesThisRound)} 남음</span>
            <span>Pending 전체 {evidence.remainingPhotosForPending}/{evidence.maxPhotosPerPending}장 남음</span>
          </div>
          <div className="pending-action-photo-inputs">
            <label><span>조치 사진</span><input type="file" accept="image/jpeg,image/png" capture="environment" disabled={busy || evidence.remainingPhotosThisRound < 1 || evidence.remainingPhotosForPending < 1} onChange={(event) => onPhotoFile(event.target.files?.[0] ?? null)} /><small>{photoFile?.name ?? 'JPEG·PNG / 장당 5MB 이하'}</small></label>
            <label><span>사진 설명</span><input value={photoAlt} maxLength={200} disabled={busy} placeholder="예: 교체 후 체결 상태" onChange={(event) => onPhotoAlt(event.target.value)} /></label>
            <button className="primary-button" type="button" disabled={busy || !photoFile || photoAlt.trim().length === 0} onClick={onUpload}>사진 등록</button>
          </div>
          {drafts.length > 0 ? <div className="pending-action-photo-grid" aria-label="확정 전 조치 사진">{drafts.map((photo) => <PendingActionPhotoFigure key={photo.photoId} pendingId={pendingId} photo={photo} developmentUserKey={developmentUserKey} editable={!busy} onRemove={() => onRemove(photo.photoId)} />)}</div> : <p className="muted-text">아직 등록한 조치 사진이 없습니다.</p>}
        </div>
      ) : <p className="pending-action-photo-privacy">조치 중 사진은 담당자에게만 보이며, 조치 완료 시 확정 근거로 공개됩니다.</p>}

      {evidence.confirmedRounds.length > 0 ? (
        <div className="pending-action-rounds">
          {evidence.confirmedRounds.map((round, index) => (
            <details key={round.actionRound} open={index === 0}>
              <summary><strong>{round.actionRound}차 조치 완료</strong><span>{round.photos.length}장 · {formatDateTime(round.confirmedAtUtc)}</span></summary>
              <div className="pending-action-reason"><span>조치 내용</span><strong>{round.actionReasonSnapshot}</strong><small>{round.confirmedByDisplayName}</small></div>
              <div className="pending-action-photo-grid">{round.photos.map((photo) => <PendingActionPhotoFigure key={photo.photoId} pendingId={pendingId} photo={photo} developmentUserKey={developmentUserKey} editable={false} />)}</div>
            </details>
          ))}
        </div>
      ) : <p className="muted-text">확정된 조치 사진이 없습니다. 사진 없이 조치를 완료해도 기존 흐름은 그대로 진행됩니다.</p>}
    </section>
  );
}

function PendingActionPhotoFigure({
  pendingId,
  photo,
  developmentUserKey,
  editable,
  onRemove
}: {
  pendingId: string;
  photo: PendingActionPhoto;
  developmentUserKey: string | undefined;
  editable: boolean;
  onRemove?: () => void;
}) {
  const [source, setSource] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    void getPendingActionPhotoBlob(developmentUserKey, pendingId, photo.photoId)
      .then((blob) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(blob);
        setSource(objectUrl);
        setFailed(false);
      })
      .catch(() => {
        if (active) setFailed(true);
      });
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [developmentUserKey, pendingId, photo.photoId]);
  return (
    <figure className="pending-action-photo">
      {source ? <img src={source} alt={photo.altText} /> : <div className="pending-action-photo-placeholder">{failed ? '사진을 불러오지 못함' : '사진 불러오는 중'}</div>}
      <figcaption><strong>{photo.altText}</strong><span>{formatBytes(photo.byteSize)}</span></figcaption>
      {editable && onRemove ? <button type="button" onClick={onRemove}>삭제</button> : null}
    </figure>
  );
}

function PendingLoading() {
  return <div className="pending-loading" role="status"><span /><span /><span /><p>Pending을 불러오는 중입니다…</p></div>;
}

function PendingError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return <div className="pending-empty" role="alert"><strong>Pending을 불러오지 못했습니다.</strong><p>{message}</p><button type="button" onClick={onRetry}>다시 시도</button></div>;
}

function PendingEmpty({ canManage, onCreate }: { canManage: boolean; onCreate: () => void }) {
  return <div className="pending-empty"><span aria-hidden="true">✓</span><strong>조건에 맞는 Pending이 없습니다.</strong><p>모든 업무가 정상이라면 이 상태가 맞습니다. 새 이슈가 생기면 즉시 등록해 담당자를 연결하세요.</p>{canManage ? <button className="primary-button" type="button" onClick={onCreate}>첫 Pending 등록</button> : null}</div>;
}

function formatPendingQuantity(quantity: number | null, unit: string | null) {
  if (quantity === null) return '수량 미입력';
  return `${new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 3 }).format(quantity)}${unit ? ` ${unit}` : ''}`;
}

function messageForError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 403) return '현재 역할에는 이 작업 권한이 없습니다. 생산관리 또는 담당자에게 요청해 주세요.';
    if (error.status === 409) return `${error.message} 최신 내용을 다시 불러와 주세요.`;
    return error.message;
  }
  return '알 수 없는 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.';
}

function pendingStatusTone(status: PendingStatus) {
  if (status === 'Closed') return 'success';
  if (status === 'ReinspectionRequested') return 'info';
  if (status === 'InProgress') return 'warning';
  return 'neutral';
}

function padIssueNumber(value: number) { return String(value).padStart(4, '0'); }
function formatBytes(value: number) { return value >= 1024 * 1024 ? `${(value / 1024 / 1024).toFixed(1)}MB` : `${Math.ceil(value / 1024)}KB`; }
function formatDate(value: string) { return new Intl.DateTimeFormat('ko-KR', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(`${value}T00:00:00`)); }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('ko-KR', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value)); }
function departmentLabel(value: string) { return ({ sales: '영업', design: '설계', procurement: '구매', materials: '자재', 'production-planning': '생산관리', manufacturing: '제조', quality: '품질', logistics: '물류' } as Record<string, string>)[value] ?? value; }
