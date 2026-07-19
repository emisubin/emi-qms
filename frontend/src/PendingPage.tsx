import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiError,
  addPendingComment,
  assignPendingIssue,
  createPendingIssue,
  getPendingIssue,
  listPendingAssignees,
  listPendingIssues,
  listPendingTypeFilterOptions,
  listPendingTypeManualOptions,
  listProjects,
  transitionPendingIssue
} from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import { MobileSheet } from './MobileSheet';
import type { ProjectListItem } from './projects';
import type {
  CreatePendingRequest,
  PendingAssignee,
  PendingDetail,
  PendingIssue,
  PendingIssueType,
  PendingListResponse,
  PendingPriority,
  PendingStatus
} from './pending';
import type { PendingTypeOption } from './pendingTypes';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type PendingPageProps = {
  developmentUserKey: string | undefined;
  pendingId?: string;
  initialProjectId?: string;
  canManage: boolean;
  onOpenPending: (pendingId: string) => void;
  onBackToList: () => void;
  onOpenProject: (projectId: string) => void;
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
  ReinspectionRequested: '재검사 요청',
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
  onOpenProject,
  onBadgeRefresh
}: PendingPageProps) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<AsyncState<PendingListResponse>>({ kind: 'loading' });
  const [status, setStatus] = useState<PendingStatus | ''>('');
  const [issueType, setIssueType] = useState<PendingIssueType | ''>('');
  const [priority, setPriority] = useState<PendingPriority | ''>('');
  const [projectId, setProjectId] = useState(initialProjectId ?? '');
  const [projectOptions, setProjectOptions] = useState<ProjectListItem[]>([]);
  const [typeOptions, setTypeOptions] = useState<PendingTypeOption[] | null>(null);
  const [typeOptionsError, setTypeOptionsError] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [draftProjectId, setDraftProjectId] = useState(initialProjectId ?? '');
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
        if (active) setProjectOptions(response.items);
      })
      .catch(() => {
        if (active) setProjectOptions([]);
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
  const activeFilterCount = [projectId, status, issueType, priority].filter(Boolean).length;
  return (
    <section className={isMobile ? 'page-surface pending-page mobile-first-page' : 'page-surface pending-page'} aria-labelledby="pending-title">
      <header className={isMobile ? 'page-header pending-header mobile-page-header' : 'page-header pending-header'}>
        <div>
          <p className="eyebrow">{isMobile ? 'URGENT ISSUE CONTROL' : 'COMMON ISSUE CONTROL'}</p>
          <h2 id="pending-title">{isMobile ? '현장 Pending' : 'Pending List'}</h2>
          <p className="muted-text">{isMobile ? '긴급·기한 초과 이슈부터 확인하고 바로 조치하세요.' : '관리자가 구성한 Pending 유형으로 등록하고 담당 조치부터 재검사·종결까지 추적합니다.'}</p>
        </div>
        {canManage ? <button className="primary-button" type="button" disabled={typeOptions === null || typeOptionsError} onClick={() => setShowCreate(true)}>+ Pending 등록</button> : null}
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
              setDraftProjectId(projectId);
              setDraftStatus(status);
              setDraftIssueType(issueType);
              setDraftPriority(priority);
              setMobileFiltersOpen(true);
            }}
          >
            <span><strong>Pending 필터</strong><small>{activeFilterCount ? `${activeFilterCount}개 조건 적용 중` : '긴급도·상태·프로젝트로 찾기'}</small></span>
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
                <button type="button" onClick={() => { setDraftProjectId(''); setDraftStatus(''); setDraftIssueType(''); setDraftPriority(''); }}>초기화</button>
                <button type="button" onClick={() => setMobileFiltersOpen(false)}>취소</button>
                <button
                  type="button"
                  className="primary-button"
                  onClick={() => {
                    setProjectId(draftProjectId);
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
              <label><span>프로젝트</span><select data-autofocus value={draftProjectId} onChange={(event) => setDraftProjectId(event.target.value)}><option value="">전체 프로젝트</option>{projectOptions.map((project) => <option key={project.projectId} value={project.projectId}>{project.projectCode} · {project.projectTitle}</option>)}</select></label>
              <label><span>상태</span><select value={draftStatus} onChange={(event) => setDraftStatus(event.target.value as PendingStatus | '')}>{statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
              <label><span>유형</span><select disabled={typeOptions === null || typeOptionsError} value={draftIssueType} onChange={(event) => setDraftIssueType(event.target.value as PendingIssueType | '')}><option value="">전체 유형</option>{typeOptions?.map((option) => <option key={option.code} value={option.code}>{option.displayName}{option.isActive ? '' : ' · 사용 중지'}</option>)}</select></label>
              <label><span>긴급도</span><select value={draftPriority} onChange={(event) => setDraftPriority(event.target.value as PendingPriority | '')}><option value="">전체 긴급도</option><option value="Urgent">긴급</option><option value="Normal">일반</option></select></label>
            </div>
          </MobileSheet>
        </>
      ) : (
        <div className="pending-filter-bar" aria-label="Pending 필터">
          <label><span>프로젝트</span><select value={projectId} onChange={(event) => setProjectId(event.target.value)}><option value="">전체 프로젝트</option>{projectOptions.map((project) => <option key={project.projectId} value={project.projectId}>{project.projectCode} · {project.projectTitle}</option>)}</select></label>
          <label><span>상태</span><select value={status} onChange={(event) => setStatus(event.target.value as PendingStatus | '')}>{statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
          <label><span>유형</span><select disabled={typeOptions === null || typeOptionsError} value={issueType} onChange={(event) => setIssueType(event.target.value as PendingIssueType | '')}><option value="">전체 유형</option>{typeOptions?.map((option) => <option key={option.code} value={option.code}>{option.displayName}{option.isActive ? '' : ' · 사용 중지'}</option>)}</select></label>
          <label><span>긴급도</span><select value={priority} onChange={(event) => setPriority(event.target.value as PendingPriority | '')}><option value="">전체 긴급도</option><option value="Urgent">긴급</option><option value="Normal">일반</option></select></label>
          <button type="button" onClick={() => { setProjectId(''); setStatus(''); setIssueType(''); setPriority(''); }}>필터 초기화</button>
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
              onOpenProject={() => onOpenProject(item.projectId)}
            />
          ))}
        </div>
      ) : null}

      <aside className="pending-attachment-note" aria-label="첨부 기능 안내">
        <strong>첨부파일은 정책 확정 후 제공됩니다.</strong>
        <span>저장 위치·파일 검역·접근권한·보존/복구 기준을 먼저 확정하고 안전하게 연결합니다. 현재는 코멘트에 조치 근거를 남겨 주세요.</span>
      </aside>

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

function PendingCard({ item, selected, selectionBusy, onSelectionChange, onOpen, onOpenProject }: { item: PendingIssue; selected: boolean; selectionBusy: boolean; onSelectionChange: (selected: boolean) => void; onOpen: () => void; onOpenProject: () => void }) {
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
          <button className="link-button" type="button" onClick={onOpenProject}>{item.projectCode} · {item.projectTitle}</button>
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
  onClose,
  onCreated
}: {
  developmentUserKey: string | undefined;
  onClose: () => void;
  onCreated: (detail: PendingDetail) => void;
}) {
  const [form, setForm] = useState<CreatePendingRequest>(emptyCreate);
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
          <div className="pending-attachment-note pending-full-field"><strong>파일 첨부 준비 중</strong><span>보안 저장·검역 정책 확정 전에는 파일을 받지 않습니다. 필요한 근거는 우선 상세 내용과 코멘트로 남겨 주세요.</span></div>
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
  const timeline = useMemo(() => detail?.history.slice().reverse() ?? [], [detail]);

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

  if (state.kind === 'loading') return <section className="page-surface pending-page"><PendingLoading /></section>;
  if (state.kind === 'error') return <section className="page-surface pending-page"><PendingError message={state.message} onRetry={load} /><button type="button" onClick={onBackToList}>목록으로</button></section>;
  if (!detail) return null;
  const issue = detail.issue;

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

          {canManage && detail.canAssign ? <section className="pending-section"><h3>담당 변경</h3><div className="pending-inline-action"><select aria-label="새 조치 담당" value={nextAssignee} onChange={(event) => setNextAssignee(event.target.value)}><option value="">새 담당자 선택</option>{assignees.filter((item) => item.userId !== issue.assigneeUserId).map((item) => <option key={item.userId} value={item.userId}>{item.displayName} · {departmentLabel(item.departmentCode)}</option>)}</select><input aria-label="담당 변경 사유" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="변경 사유 (3자 이상)" /><button disabled={busy || !nextAssignee || reason.trim().length < 3} type="button" onClick={() => void runMutation(() => assignPendingIssue(developmentUserKey, pendingId, nextAssignee, issue.version, reason), '조치 담당자가 변경되었습니다.', () => { setReason(''); setNextAssignee(''); })}>담당 변경</button></div></section> : null}

          {detail.allowedTransitions.length > 0 ? <section className="pending-section pending-next-action"><div><p className="eyebrow">NEXT ACTION</p><h3>{transitionLabels[detail.allowedTransitions[0]]}</h3><p>현재 상태를 확인하고 변경 사유를 남겨 다음 담당자에게 넘깁니다.</p></div><div className="pending-transition-control"><input aria-label="상태 변경 사유" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="처리 내용 또는 확인 결과 (3자 이상)" /><button className="primary-button" disabled={busy || reason.trim().length < 3} type="button" onClick={() => void runMutation(() => transitionPendingIssue(developmentUserKey, pendingId, detail.allowedTransitions[0], issue.version, reason), `${transitionLabels[detail.allowedTransitions[0]]} 상태로 변경되었습니다.`, () => setReason(''))}>{transitionLabels[detail.allowedTransitions[0]]}</button></div></section> : null}

          <section className="pending-section"><div className="subsection-header"><h3>코멘트</h3><span>{detail.comments.length}개</span></div>{detail.comments.length === 0 ? <p className="muted-text">아직 코멘트가 없습니다. 조치 근거와 재검사 결과를 남겨 주세요.</p> : <div className="pending-comments">{detail.comments.map((item) => <article key={item.commentId}><strong>{item.createdByDisplayName}</strong><p>{item.body}</p><time>{formatDateTime(item.createdAtUtc)}</time></article>)}</div>}{detail.canComment ? <form className="pending-comment-form" onSubmit={(event) => { event.preventDefault(); if (comment.trim()) void runMutation(() => addPendingComment(developmentUserKey, pendingId, comment), '코멘트가 추가되었습니다.', () => setComment('')); }}><textarea aria-label="새 코멘트" value={comment} onChange={(event) => setComment(event.target.value)} placeholder="조치 내용, 확인 결과 또는 다음 요청을 남겨 주세요." /><button className="primary-button" disabled={busy || !comment.trim()} type="submit">코멘트 등록</button></form> : null}</section>
        </div>

        <aside className="pending-timeline"><h3>변경 이력</h3>{timeline.map((event) => <article key={event.historyId}><span className="pending-timeline-dot" /><div><strong>{event.eventLabel}</strong><p>{event.fromStatusLabel && event.toStatusLabel ? `${event.fromStatusLabel} → ${event.toStatusLabel}` : event.toStatusLabel ?? ''}{event.fromAssigneeDisplayName !== event.toAssigneeDisplayName && event.toAssigneeDisplayName ? ` · ${event.toAssigneeDisplayName}` : ''}</p>{event.reason ? <p>{event.reason}</p> : null}<small>{event.changedByDisplayName} · {formatDateTime(event.createdAtUtc)}</small></div></article>)}<div className="pending-attachment-note"><strong>첨부파일 보류</strong><span>보안 정책 확정 후 이 타임라인에 파일 audit를 함께 표시합니다.</span></div></aside>
      </div>
    </section>
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
function formatDate(value: string) { return new Intl.DateTimeFormat('ko-KR', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(`${value}T00:00:00`)); }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('ko-KR', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value)); }
function departmentLabel(value: string) { return ({ sales: '영업', design: '설계', procurement: '구매', materials: '자재', 'production-planning': '생산관리', manufacturing: '제조', quality: '품질', logistics: '물류' } as Record<string, string>)[value] ?? value; }
