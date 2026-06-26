import { FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  applyPanelInformationExcel,
  changePanelCount,
  changeProjectStatus,
  createProject,
  defaultDevelopmentUserKey,
  deleteProject,
  downloadPanelInformationTemplate,
  getCurrentUser,
  getDeletedProject,
  getPanel,
  getPanelInformation,
  getPanelInformationHistory,
  getProject,
  getReadyHealth,
  getSalesOwners,
  listDeletedProjects,
  listPanels,
  listProjects,
  previewPanelInformationExcel,
  updatePanelInformation,
  updateProject
} from './api';
import type { ReadyHealth } from './health';
import type { CurrentUser } from './identity';
import { maxPanelsPerProject } from './projects';
import type {
  AuditEvent,
  DeletedProjectDetail,
  DeletedProjectListItem,
  PanelInformationExcelPreviewResponse,
  PanelInformationHistoryResponse,
  PanelInformationPanel,
  PanelInformationResponse,
  PanelInputUnit,
  PanelPlaceholder,
  PackagingMethod,
  ProjectDetail,
  ProjectListItem,
  ProjectListTab,
  ProjectStatus,
  ProjectWorkStatus,
  ProductWorkflowStage,
  SalesOwner
} from './projects';

type View =
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'detail'; projectId: string }
  | { kind: 'deleted-detail'; projectId: string }
  | { kind: 'edit'; projectId: string }
  | { kind: 'panel-info-edit'; projectId: string }
  | { kind: 'panel'; projectId: string; panelId: string };

type LoadState<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; data: T }
  | { kind: 'empty' }
  | { kind: 'forbidden'; message: string }
  | { kind: 'not-found'; message: string }
  | { kind: 'error'; message: string };

type ProjectFormValues = {
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  panelCount: string;
  deliveryDate: string;
  salesOwnerUserId: string;
  packagingMethod: string;
  salesAmount: string;
  currencyCode: string;
  deliveryLocation: string;
  reason: string;
};

const developmentUsers = [
  'dev-sales',
  'dev-design',
  'dev-admin',
  'dev-production',
  'dev-manufacturing',
  'dev-quality',
  'dev-logistics',
  'dev-viewer',
  'dev-disabled'
];

const emptyForm: ProjectFormValues = {
  customerName: '',
  item: '',
  projectCode: '',
  projectTitle: '',
  panelCount: '1',
  deliveryDate: '',
  salesOwnerUserId: '',
  packagingMethod: '',
  salesAmount: '',
  currencyCode: 'KRW',
  deliveryLocation: '',
  reason: ''
};

function initialViewFromLocation(): View {
  if (typeof window === 'undefined') {
    return { kind: 'list' };
  }

  const panelInformationEditMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/panel-information\/edit$/);
  if (panelInformationEditMatch?.[1]) {
    return { kind: 'panel-info-edit', projectId: panelInformationEditMatch[1] };
  }

  const panelMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/panels\/([^/]+)$/);
  if (panelMatch?.[1] && panelMatch?.[2]) {
    return { kind: 'panel', projectId: panelMatch[1], panelId: panelMatch[2] };
  }

  const detailMatch = window.location.pathname.match(/^\/projects\/([^/]+)$/);
  if (detailMatch?.[1]) {
    return { kind: 'detail', projectId: detailMatch[1] };
  }

  return { kind: 'list' };
}

function pathForView(view: View) {
  switch (view.kind) {
    case 'detail':
      return `/projects/${view.projectId}`;
    case 'panel-info-edit':
      return `/projects/${view.projectId}/panel-information/edit`;
    case 'panel':
      return `/projects/${view.projectId}/panels/${view.panelId}`;
    default:
      return '/';
  }
}

export function App() {
  const [developmentUserKey, setDevelopmentUserKey] = useState(defaultDevelopmentUserKey ?? 'dev-sales');
  const [view, setViewState] = useState<View>(() => initialViewFromLocation());
  const [health, setHealth] = useState<LoadState<ReadyHealth>>({ kind: 'loading' });
  const [currentUser, setCurrentUser] = useState<LoadState<CurrentUser>>({ kind: 'loading' });

  const setView = useCallback((nextView: View) => {
    setViewState(nextView);
    if (typeof window === 'undefined') {
      return;
    }

    const nextPath = pathForView(nextView);
    if (window.location.pathname !== nextPath) {
      window.history.pushState(null, '', nextPath);
    }
  }, []);

  const loadShell = useCallback(() => {
    getReadyHealth()
      .then((data) => setHealth({ kind: 'ready', data }))
      .catch((error: unknown) => setHealth(toLoadError(error, 'API 상태를 확인할 수 없습니다.')));

    getCurrentUser(developmentUserKey)
      .then((data) => setCurrentUser({ kind: 'ready', data }))
      .catch((error: unknown) => setCurrentUser(toLoadError(error, '개발 사용자를 확인할 수 없습니다.')));
  }, [developmentUserKey]);

  useEffect(() => {
    loadShell();
  }, [loadShell]);

  useEffect(() => {
    const handlePopState = () => {
      setViewState(initialViewFromLocation());
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  const user = currentUser.kind === 'ready' ? currentUser.data : null;
  const permissions = user?.permissions ?? [];
  const canCreate = permissions.includes('Project.Create');
  const canUpdate = permissions.includes('Project.Update');
  const canHold = permissions.includes('Project.Hold');
  const canCancel = permissions.includes('Project.Cancel');
  const canDelete = permissions.includes('Project.Delete');
  const canReadDeleted = permissions.includes('Project.Deleted.Read');
  const canReadSalesAmount = permissions.includes('Project.SalesAmount.Read');
  const canUpdatePanelInfo = permissions.includes('PanelInfo.Update');
  const canReadAuditAll = permissions.includes('Audit.Read.All');

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">EMI QMS</p>
          <h1>프로젝트·제품 패널 관리</h1>
        </div>
        <label className="dev-user-select">
          <span>개발 사용자</span>
          <select
            value={developmentUserKey}
            onChange={(event) => {
              setDevelopmentUserKey(event.target.value);
              setView({ kind: 'list' });
            }}
          >
            {developmentUsers.map((userKey) => (
              <option key={userKey} value={userKey}>{userKey}</option>
            ))}
          </select>
        </label>
      </header>

      <section className="system-strip" aria-label="시스템 상태">
        <StatusChip label="API" value={health.kind === 'ready' ? health.data.status : health.kind} />
        <StatusChip
          label="Database"
          value={health.kind === 'ready' ? health.data.database.reason : '-'}
        />
        <StatusChip
          label="User"
          value={currentUser.kind === 'ready' ? currentUser.data.displayName : currentUser.kind}
        />
      </section>

      {currentUser.kind === 'forbidden' || currentUser.kind === 'not-found' || currentUser.kind === 'error' ? (
        <StateMessage state={currentUser} />
      ) : null}

      {view.kind === 'list' ? (
        <ProjectListPage
          developmentUserKey={developmentUserKey}
          canCreate={canCreate}
          canReadDeleted={canReadDeleted}
          canReadSalesAmount={canReadSalesAmount}
          onCreate={() => setView({ kind: 'create' })}
          onOpen={(projectId) => setView({ kind: 'detail', projectId })}
          onOpenDeleted={(projectId) => setView({ kind: 'deleted-detail', projectId })}
        />
      ) : null}

      {view.kind === 'create' ? (
        <ProjectCreatePage
          developmentUserKey={developmentUserKey}
          onCancel={() => setView({ kind: 'list' })}
          onCreated={(projectId) => setView({ kind: 'detail', projectId })}
        />
      ) : null}

      {view.kind === 'detail' ? (
        <ProjectDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdate={canUpdate}
          canHold={canHold}
          canCancel={canCancel}
          canDelete={canDelete}
          canReadSalesAmount={canReadSalesAmount}
          canUpdatePanelInfo={canUpdatePanelInfo}
          canReadAuditAll={canReadAuditAll}
          onBack={() => setView({ kind: 'list' })}
          onEdit={() => setView({ kind: 'edit', projectId: view.projectId })}
          onEditPanelInformation={() => setView({ kind: 'panel-info-edit', projectId: view.projectId })}
          onOpenPanel={(panelId) => setView({ kind: 'panel', projectId: view.projectId, panelId })}
        />
      ) : null}

      {view.kind === 'deleted-detail' ? (
        <DeletedProjectDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canReadSalesAmount={canReadSalesAmount}
          onBack={() => setView({ kind: 'list' })}
        />
      ) : null}

      {view.kind === 'edit' ? (
        <ProjectEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          onCancel={() => setView({ kind: 'detail', projectId: view.projectId })}
          onSaved={() => setView({ kind: 'detail', projectId: view.projectId })}
        />
      ) : null}

      {view.kind === 'panel-info-edit' ? (
        <PanelInformationEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdatePanelInfo={canUpdatePanelInfo}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId })}
        />
      ) : null}

      {view.kind === 'panel' ? (
        <PanelPlaceholderDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          panelId={view.panelId}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId })}
        />
      ) : null}
    </main>
  );
}

function ProjectListPage({
  developmentUserKey,
  canCreate,
  canReadDeleted,
  canReadSalesAmount,
  onCreate,
  onOpen,
  onOpenDeleted
}: {
  developmentUserKey: string;
  canCreate: boolean;
  canReadDeleted: boolean;
  canReadSalesAmount: boolean;
  onCreate: () => void;
  onOpen: (projectId: string) => void;
  onOpenDeleted: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState<ProjectListTab>('All');
  const [state, setState] = useState<LoadState<Array<ProjectListItem | DeletedProjectListItem>>>({ kind: 'loading' });
  const requestIdRef = useRef(0);
  const abortControllerRef = useRef<AbortController | null>(null);

  const load = useCallback(() => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const request = tab === 'Deleted'
      ? listDeletedProjects(developmentUserKey, search, { signal: controller.signal })
      : listProjects(developmentUserKey, search, tab, { signal: controller.signal });

    queueMicrotask(() => {
      if (requestId === requestIdRef.current && !controller.signal.aborted) {
        setState({ kind: 'loading' });
      }
    });

    request
      .then((response) => {
        if (requestId !== requestIdRef.current || controller.signal.aborted) {
          return;
        }

        setState(response.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: response.items });
      })
      .catch((error: unknown) => {
        if (requestId !== requestIdRef.current || controller.signal.aborted || isAbortError(error)) {
          return;
        }

        setState(toLoadError(error, '프로젝트 목록을 불러올 수 없습니다.'));
      })
      .finally(() => {
        if (requestId === requestIdRef.current && abortControllerRef.current === controller) {
          abortControllerRef.current = null;
        }
      });
  }, [developmentUserKey, search, tab]);

  useEffect(() => {
    load();
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [load]);

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Projects</p>
          <h2>프로젝트 목록</h2>
        </div>
        {canCreate ? <button type="button" className="primary-button" onClick={onCreate}>신규 프로젝트</button> : null}
      </div>

      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault();
          load();
        }}
      >
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="고객사, Item, PJT Code, PJT Title 검색"
        />
        <button type="submit">검색</button>
      </form>

      <div className="tab-row" role="tablist" aria-label="프로젝트 상태">
        {projectTabs(canReadDeleted).map((item) => (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={tab === item.value}
            className={tab === item.value ? 'tab-button active' : 'tab-button'}
            onClick={() => setTab(item.value)}
          >
            {item.label}
          </button>
        ))}
      </div>

      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">등록된 프로젝트가 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      {state.kind === 'ready' ? (
        <ProjectListView
          projects={state.data}
          canReadSalesAmount={canReadSalesAmount}
          onOpen={(projectId) => tab === 'Deleted' ? onOpenDeleted(projectId) : onOpen(projectId)}
        />
      ) : null}
    </section>
  );
}

function ProjectListView({
  projects,
  canReadSalesAmount,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  onOpen: (projectId: string) => void;
}) {
  const isMobile = useIsMobileViewport();

  return (
    <div className="project-list">
      {isMobile
        ? <ProjectListMobile projects={projects} canReadSalesAmount={canReadSalesAmount} onOpen={onOpen} />
        : <ProjectListDesktop projects={projects} canReadSalesAmount={canReadSalesAmount} onOpen={onOpen} />}
    </div>
  );
}

function ProjectListDesktop({
  projects,
  canReadSalesAmount,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  onOpen: (projectId: string) => void;
}) {
  return (
    <div className="project-list-table project-list-desktop" role="table" aria-label="프로젝트 목록" data-testid="project-list-desktop">
      <div className="project-list-head" role="row">
        <span className="align-left">프로젝트명</span>
        <span className="align-left">고객사</span>
        <span className="align-center">Code</span>
        <span className="align-left">Item</span>
        <span className="align-center">면수</span>
        <span className="align-center">납기일</span>
        <span className="align-center">상태</span>
        <span className="align-center">진행률</span>
      </div>
      {projects.map((project) => (
        <button
          className="project-list-row"
          key={project.projectId}
          type="button"
          role="row"
          onClick={() => onOpen(project.projectId)}
        >
          <span className="align-left">
            <strong>{project.projectTitle}</strong>
            {'deletedAtUtc' in project ? <small>삭제일시 {formatDateTime(project.deletedAtUtc)}</small> : null}
            {canReadSalesAmount && project.salesAmount !== undefined ? (
              <small><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></small>
            ) : null}
          </span>
          <span className="align-left">{project.customerName}</span>
          <span className="align-center">{project.projectCode}</span>
          <span className="align-left">{project.item}</span>
          <span className="align-center">{project.activePanelCount}면</span>
          <span className="align-center">{formatDate(project.deliveryDate)}</span>
          <span className="align-center">{formatProjectWorkStatus(project.projectWorkStatus)}</span>
          <span className="align-center">{formatProjectProgress(project.projectProgressPercent)}</span>
        </button>
      ))}
    </div>
  );
}

function ProjectListMobile({
  projects,
  canReadSalesAmount,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  onOpen: (projectId: string) => void;
}) {
  return (
    <div className="project-list-cards project-list-mobile" data-testid="project-list-mobile">
      {projects.map((project) => (
        <article key={project.projectId} className="project-list-card" data-testid="project-list-card">
          <div className="subsection-header">
            <h3>{project.projectTitle}</h3>
            <button type="button" onClick={() => onOpen(project.projectId)}>상세 보기</button>
          </div>
          <dl className="mobile-detail-list">
            <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
            <div><dt>Code</dt><dd>{project.projectCode}</dd></div>
            <div><dt>Item</dt><dd>{project.item}</dd></div>
            <div><dt>면수</dt><dd>{project.activePanelCount}면</dd></div>
            <div><dt>납기일</dt><dd>{formatDate(project.deliveryDate)}</dd></div>
            <div><dt>상태</dt><dd>{formatProjectWorkStatus(project.projectWorkStatus)}</dd></div>
            <div><dt>진행률</dt><dd>{formatProjectProgress(project.projectProgressPercent)}</dd></div>
            {'deletedAtUtc' in project ? <div><dt>삭제일시</dt><dd>{formatDateTime(project.deletedAtUtc)}</dd></div> : null}
            {canReadSalesAmount && project.salesAmount !== undefined ? (
              <div><dt>판매금액</dt><dd><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></dd></div>
            ) : null}
          </dl>
        </article>
      ))}
    </div>
  );
}

function ProjectCreatePage({
  developmentUserKey,
  onCancel,
  onCreated
}: {
  developmentUserKey: string;
  onCancel: () => void;
  onCreated: (projectId: string) => void;
}) {
  const [owners, setOwners] = useState<SalesOwner[]>([]);
  const [form, setForm] = useState<ProjectFormValues>(emptyForm);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    getSalesOwners(developmentUserKey)
      .then((data) => {
        setOwners(data);
        setForm((current) => ({ ...current, salesOwnerUserId: current.salesOwnerUserId || data[0]?.userId || '' }));
      })
      .catch((error: unknown) => setMessage(error instanceof Error ? error.message : '영업담당자를 불러올 수 없습니다.'));
  }, [developmentUserKey]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    const validation = validateProjectForm(form, false);
    setErrors(validation);
    setMessage('');
    if (Object.keys(validation).length > 0) {
      return;
    }

    setIsSaving(true);
    try {
      const project = await createProject(developmentUserKey, toCreateRequest(form));
      onCreated(project.projectId);
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Sales</p>
          <h2>프로젝트 등록</h2>
        </div>
        <button type="button" onClick={onCancel}>목록</button>
      </div>
      <ProjectForm
        form={form}
        owners={owners}
        errors={errors}
        isSaving={isSaving}
        submitLabel="등록"
        onChange={setForm}
        onSubmit={submit}
      />
      {message ? <p role="alert" className="error-text">{message}</p> : null}
    </section>
  );
}

function ProjectDetailPage({
  developmentUserKey,
  projectId,
  canUpdate,
  canHold,
  canCancel,
  canDelete,
  canReadSalesAmount,
  canUpdatePanelInfo,
  canReadAuditAll,
  onBack,
  onEdit,
  onEditPanelInformation,
  onOpenPanel
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdate: boolean;
  canHold: boolean;
  canCancel: boolean;
  canDelete: boolean;
  canReadSalesAmount: boolean;
  canUpdatePanelInfo: boolean;
  canReadAuditAll: boolean;
  onBack: () => void;
  onEdit: () => void;
  onEditPanelInformation: () => void;
  onOpenPanel: (panelId: string) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [panelInfoState, setPanelInfoState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [historyState, setHistoryState] = useState<LoadState<PanelInformationHistoryResponse>>({ kind: 'empty' });
  const [dialog, setDialog] = useState<null | 'hold' | 'resume' | 'cancel' | 'reactivate' | 'delete'>(null);
  const [reason, setReason] = useState('');
  const [confirmProjectTitle, setConfirmProjectTitle] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(() => {
    setProjectState({ kind: 'loading' });
    setPanelInfoState({ kind: 'loading' });
    setHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });

    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanelInformation(developmentUserKey, projectId),
      canReadAuditAll ? getPanelInformationHistory(developmentUserKey, projectId) : Promise.resolve(null)
    ])
      .then(([project, panelInfo, history]) => {
        setProjectState({ kind: 'ready', data: project });
        setPanelInfoState({ kind: 'ready', data: panelInfo });
        setHistoryState(history ? { kind: 'ready', data: history } : { kind: 'empty' });
      })
      .catch((error: unknown) => {
        const state = toLoadError<ProjectDetail>(error, '프로젝트 상세를 불러올 수 없습니다.');
        setProjectState(state);
        setPanelInfoState(toLoadError(error, '제품·패널 목록을 불러올 수 없습니다.'));
      });
  }, [canReadAuditAll, developmentUserKey, projectId]);

  useEffect(() => {
    load();
  }, [load]);

  async function submitStatusChange() {
    if (!dialog || dialog === 'delete') {
      return;
    }

    if (!reason.trim()) {
      setDialogError('사유는 필수입니다.');
      return;
    }

    setIsSaving(true);
    setDialogError('');
    try {
      await changeProjectStatus(developmentUserKey, projectId, dialog, { reason });
      setDialog(null);
      setReason('');
      load();
    } catch (error) {
      setDialogError(error instanceof Error ? error.message : '상태 변경에 실패했습니다.');
    } finally {
      setIsSaving(false);
    }
  }

  async function submitDelete() {
    if (!reason.trim()) {
      setDialogError('삭제 사유는 필수입니다.');
      return;
    }

    if (!confirmProjectTitle.trim()) {
      setDialogError('PJT Title 확인 입력은 필수입니다.');
      return;
    }

    setIsSaving(true);
    setDialogError('');
    try {
      await deleteProject(developmentUserKey, projectId, {
        reason,
        confirmProjectTitle
      });
      setDialog(null);
      onBack();
    } catch (error) {
      setDialogError(error instanceof Error ? error.message : '삭제에 실패했습니다.');
    } finally {
      setIsSaving(false);
    }
  }

  if (projectState.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  const project = projectState.data;
  const canShowEdit = canUpdate;
  const isOnHold = project.status === 'OnHold';
  const isCancelled = project.status === 'Cancelled';

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Project Detail</p>
          <h2>{project.projectTitle}</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>목록</button>
          {canShowEdit ? <button type="button" onClick={onEdit}>수정</button> : null}
          {canHold && project.status === 'Active' ? <button type="button" onClick={() => setDialog('hold')}>보류</button> : null}
          {canUpdate && isOnHold ? <button type="button" onClick={() => setDialog('resume')}>보류 해제</button> : null}
          {canCancel && (project.status === 'Active' || isOnHold) ? <button type="button" onClick={() => setDialog('cancel')}>취소</button> : null}
          {canUpdate && isCancelled ? <button type="button" onClick={() => setDialog('reactivate')}>재활성</button> : null}
          {canDelete && project.status !== 'Completed' ? <button type="button" className="danger-button" onClick={() => setDialog('delete')}>삭제</button> : null}
        </div>
      </div>

      <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />

      <PanelInformationSection
        project={project}
        state={panelInfoState}
        canUpdatePanelInfo={canUpdatePanelInfo}
        onEdit={onEditPanelInformation}
        onOpenPanel={onOpenPanel}
      />

      {canReadAuditAll ? (
        <section className="subsection">
          <h3>전체 이력</h3>
          {historyState.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
          {historyState.kind !== 'ready' && historyState.kind !== 'loading' && historyState.kind !== 'empty' ? <StateMessage state={historyState} /> : null}
          {historyState.kind === 'ready' ? <GroupedHistory groups={historyState.data.groups} /> : null}
        </section>
      ) : null}

      {dialog && dialog !== 'delete' ? (
        <StatusReasonDialog
          action={dialog}
          reason={reason}
          error={dialogError}
          isSaving={isSaving}
          onReasonChange={setReason}
          onCancel={() => {
            setDialog(null);
            setReason('');
            setConfirmProjectTitle('');
            setDialogError('');
          }}
          onSubmit={submitStatusChange}
        />
      ) : null}
      {dialog === 'delete' ? (
        <DeleteProjectDialog
          projectTitle={project.projectTitle}
          reason={reason}
          confirmProjectTitle={confirmProjectTitle}
          error={dialogError}
          isSaving={isSaving}
          onReasonChange={setReason}
          onConfirmProjectTitleChange={setConfirmProjectTitle}
          onCancel={() => {
            setDialog(null);
            setReason('');
            setConfirmProjectTitle('');
            setDialogError('');
          }}
          onSubmit={submitDelete}
        />
      ) : null}
    </section>
  );
}

type PanelInformationRowForm = {
  panelId: string;
  sequenceNumber: number;
  panelNumber: string;
  displayCode: string;
  panelInfoVersion: number;
  original: PanelInformationPanel;
  originalPanelName: string;
  currentPanelName: string;
  panelNameDirty: boolean;
  originalWidthMm: string | null;
  originalHeightMm: string | null;
  originalDepthMm: string | null;
  widthInput: string;
  heightInput: string;
  depthInput: string;
  sizeDirty: boolean;
  sizeClearRequested: boolean;
  sizeInputUnit: PanelInputUnit;
};

function PanelInformationSection({
  project,
  state,
  canUpdatePanelInfo,
  onEdit,
  onOpenPanel
}: {
  project: ProjectDetail;
  state: LoadState<PanelInformationResponse>;
  canUpdatePanelInfo: boolean;
  onEdit: () => void;
  onOpenPanel: (panelId: string) => void;
}) {
  const canShowEdit = canUpdatePanelInfo && project.status === 'Active';
  const [displayUnit, setDisplayUnit] = useState<PanelInputUnit>(() => readDisplayUnit());

  const changeDisplayUnit = (unit: PanelInputUnit) => {
    setDisplayUnit(unit);
    window.localStorage.setItem('emi-qms-panel-display-unit', unit);
  };

  return (
    <section className="page-surface panel-info-section">
      <div className="subsection-header">
        <div>
          <h3>제품·패널 목록</h3>
          <span>{formatPackagingMethod(project.packagingMethod)}</span>
        </div>
        <div className="button-row">
          <div className="unit-toggle" role="group" aria-label="표시 단위">
            <button
              type="button"
              className={displayUnit === 'Mm' ? 'secondary-button active' : 'secondary-button'}
              aria-pressed={displayUnit === 'Mm'}
              onClick={() => changeDisplayUnit('Mm')}
            >
              mm
            </button>
            <button
              type="button"
              className={displayUnit === 'Inch' ? 'secondary-button active' : 'secondary-button'}
              aria-pressed={displayUnit === 'Inch'}
              onClick={() => changeDisplayUnit('Inch')}
            >
              inch
            </button>
          </div>
          {canShowEdit ? <button type="button" className="primary-button" onClick={onEdit}>패널정보 수정</button> : null}
        </div>
      </div>

      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <div className="panel-info-summary project-workflow-summary">
            <StatusChip label="QR 가능" value={`${project.qrEligibleCount}/${project.activePanelCount}`} />
            <StatusChip label="제조 완료" value={`${project.manufacturingCompletedCount}/${project.activePanelCount}`} />
            <StatusChip label="검사 완료" value={`${project.inspectionCompletedCount}/${project.activePanelCount}`} />
          </div>
          {state.data.panelInformationStatusMessage ? (
            <p role="status" className="warning-text">{state.data.panelInformationStatusMessage}</p>
          ) : null}
          <ProjectPanelList
            panels={state.data.panels}
            packagingMethod={state.data.packagingMethod}
            displayUnit={displayUnit}
            onOpenPanel={onOpenPanel}
          />
        </>
      ) : null}
    </section>
  );
}

function PanelInformationEditPage({
  developmentUserKey,
  projectId,
  canUpdatePanelInfo,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdatePanelInfo: boolean;
  onBack: () => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [state, setState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [rows, setRows] = useState<PanelInformationRowForm[]>([]);
  const [editInputUnit, setEditInputUnit] = useState<PanelInputUnit>('Mm');
  const [displayUnit, setDisplayUnit] = useState<PanelInputUnit>(() => readDisplayUnit());
  const [filter, setFilter] = useState<'All' | 'Completed' | 'Pending' | 'QrEligible'>('All');
  const [search, setSearch] = useState('');
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [isDownloadingTemplate, setIsDownloadingTemplate] = useState(false);
  const [showExcel, setShowExcel] = useState(false);
  const requestIdRef = useRef(0);
  const dirtyRef = useRef(false);
  const editInputUnitRef = useRef<PanelInputUnit>('Mm');

  useEffect(() => {
    editInputUnitRef.current = editInputUnit;
  }, [editInputUnit]);

  const load = useCallback(() => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    dirtyRef.current = false;
    setState({ kind: 'loading' });
    setProjectState({ kind: 'loading' });
    setMessage('');

    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanelInformation(developmentUserKey, projectId)
    ])
      .then(([project, panelInfo]) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setProjectState({ kind: 'ready', data: project });
        setState({ kind: 'ready', data: panelInfo });
        setRows(panelInfo.panels.map((panel) => panelToRowForm(panel, editInputUnitRef.current)));
      })
      .catch((error: unknown) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setState(toLoadError(error, '패널정보를 불러올 수 없습니다.'));
        setProjectState(toLoadError(error, '프로젝트를 불러올 수 없습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    window.localStorage.setItem('emi-qms-panel-display-unit', displayUnit);
  }, [displayUnit]);

  const data = state.kind === 'ready' ? state.data : null;
  const project = projectState.kind === 'ready' ? projectState.data : null;
  const canEdit = canUpdatePanelInfo && project?.status === 'Active';
  const visibleRows = rows.filter((row) => {
    const panel = row.original;
    const matchesFilter = filter === 'All'
      || (filter === 'Completed' && panel.panelInfoCompleted)
      || (filter === 'Pending' && !panel.panelInfoCompleted)
      || (filter === 'QrEligible' && panel.qrEligible);
    const query = search.trim().toLowerCase();
    const matchesSearch = !query
      || row.panelNumber.toLowerCase().includes(query)
      || row.displayCode.toLowerCase().includes(query)
      || row.currentPanelName.toLowerCase().includes(query)
      || (panel.panelName ?? '').toLowerCase().includes(query);
    return matchesFilter && matchesSearch;
  });
  const reasonRequired = rows.some(panelRowNeedsReason);
  const hasChanges = rows.some(panelRowChanged);

  function setPanelName(panelId: string, value: string) {
    dirtyRef.current = true;
    setRows((current) => current.map((row) => row.panelId === panelId
      ? { ...row, currentPanelName: value, panelNameDirty: true }
      : row));
  }

  function setSizeInput(panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) {
    dirtyRef.current = true;
    setRows((current) => current.map((row) => {
      if (row.panelId !== panelId) {
        return row;
      }

      const next = { ...row, [field]: value, sizeDirty: true, sizeInputUnit: editInputUnit };
      return { ...next, sizeClearRequested: !next.widthInput.trim() && !next.heightInput.trim() && !next.depthInput.trim() };
    }));
  }

  function changeEditInputUnit(nextUnit: PanelInputUnit) {
    if (rows.some((row) => row.sizeDirty)) {
      setMessage('저장되지 않은 사이즈 입력이 있습니다. 저장하거나 변경을 취소한 후 단위를 변경해 주세요.');
      return;
    }

    setEditInputUnit(nextUnit);
    setRows((current) => current.map((row) => rowWithSizeInputs(row, nextUnit)));
  }

  async function save() {
    if (!canEdit || !data) {
      return;
    }

    if (reasonRequired && !reason.trim()) {
      setMessage('기존 패널정보를 변경하려면 수정사유가 필요합니다.');
      return;
    }

    setIsSaving(true);
    setMessage('');
    try {
      const saved = await updatePanelInformation(developmentUserKey, projectId, {
        reason: reason.trim() || null,
        panels: rows.filter(panelRowChanged).map(panelRowToUpdateRequest)
      });
      dirtyRef.current = false;
      setReason('');
      setState({ kind: 'ready', data: saved });
      setRows(saved.panels.map((panel) => panelToRowForm(panel, editInputUnit)));
      setMessage('패널정보를 저장했습니다.');
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadTemplate() {
    if (!canUpdatePanelInfo) {
      return;
    }

    setIsDownloadingTemplate(true);
    setMessage('');
    try {
      const template = await downloadPanelInformationTemplate(developmentUserKey, projectId, editInputUnit);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      setMessage('Excel 양식을 다운로드했습니다.');
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsDownloadingTemplate(false);
    }
  }

  if (!canUpdatePanelInfo) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  if (projectState.kind === 'loading' || state.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  if (state.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={state} /></section>;
  }

  return (
    <section className="page-surface panel-info-section">
      <div className="subsection-header">
        <div>
          <h3>패널정보 수정</h3>
          <span>{formatPackagingMethod(projectState.data.packagingMethod)}</span>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>상세</button>
          <button type="button" onClick={load}>새로고침</button>
          {canUpdatePanelInfo ? (
            <button type="button" onClick={downloadTemplate} disabled={isDownloadingTemplate}>
              {isDownloadingTemplate ? '다운로드 중' : 'Excel 양식 다운로드'}
            </button>
          ) : null}
          <button type="button" onClick={() => setShowExcel(true)} disabled={!canEdit}>Excel 업로드</button>
          <button type="button" className="primary-button" disabled={!canEdit || isSaving || !hasChanges} onClick={save}>
            {isSaving ? '저장 중' : '직접 입력 저장'}
          </button>
        </div>
      </div>
      {data ? (
        <>
          <div className="panel-info-summary">
            <StatusChip label="입력 완료" value={`${data.panelInfoCompletedCount}/${data.activePanelCount}`} />
            <StatusChip
              label="입력 미완료"
              value={`${data.panelInfoPendingCount}/${data.activePanelCount}`}
              tone={data.panelInfoPendingCount > 0 ? 'danger' : undefined}
            />
            <StatusChip label="QR 가능" value={String(data.qrEligibleCount)} />
            <StatusChip label="동일명칭" value={String(data.duplicatePanelNameGroupCount)} />
          </div>

          {data.panelInformationStatusMessage ? (
            <p role="status" className="warning-text">{data.panelInformationStatusMessage}</p>
          ) : null}
          {!canUpdatePanelInfo ? <p className="muted-text">읽기 전용</p> : null}
          {canUpdatePanelInfo && projectState.data.status !== 'Active' ? (
            <p role="alert" className="warning-text">현재 프로젝트 상태에서는 패널정보를 수정할 수 없습니다.</p>
          ) : null}

          <div className="toolbar panel-toolbar">
            <label>
              <span>입력 단위</span>
              <select value={editInputUnit} onChange={(event) => changeEditInputUnit(event.target.value as PanelInputUnit)}>
                <option value="Mm">mm</option>
                <option value="Inch">inch</option>
              </select>
            </label>
            <label>
              <span>표시 단위</span>
              <select value={displayUnit} onChange={(event) => setDisplayUnit(event.target.value as PanelInputUnit)}>
                <option value="Mm">mm</option>
                <option value="Inch">inch</option>
              </select>
            </label>
            <label>
              <span>필터</span>
              <select value={filter} onChange={(event) => setFilter(event.target.value as typeof filter)}>
                <option value="All">전체</option>
                <option value="Completed">완료</option>
                <option value="Pending">미완료</option>
                <option value="QrEligible">QR 가능</option>
              </select>
            </label>
            <label>
              <span>검색</span>
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="No 또는 패널명" />
            </label>
          </div>

          {canUpdatePanelInfo ? (
            <div className="panel-template-help">
              <strong>입력 단위: {editInputUnit === 'Inch' ? 'inch' : 'mm'}</strong>
              <span>No는 수정하지 마세요.</span>
              <span>도번은 업로드 시 저장되지 않습니다.</span>
              <span>목포장은 panel name, w, h, d가 모두 필요합니다.</span>
              <span>청랩·고강도박스 포장은 panel name만 필수입니다.</span>
              <span>사이즈를 입력하는 경우 w, h, d를 모두 입력해야 합니다.</span>
            </div>
          ) : null}

          {reasonRequired ? (
            <label className="form-field panel-reason-field">
              <span>수정사유*</span>
              <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
            </label>
          ) : null}

          <PanelInfoEditDesktop
            rows={visibleRows}
            displayUnit={displayUnit}
            canEdit={canEdit}
            onPanelNameChange={setPanelName}
            onSizeChange={setSizeInput}
          />

          <PanelInfoEditMobile
            rows={visibleRows}
            displayUnit={displayUnit}
            canEdit={canEdit}
            onPanelNameChange={setPanelName}
            onSizeChange={setSizeInput}
          />

          {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}

        </>
      ) : null}

      {showExcel && data ? (
        <PanelInformationExcelDialog
          developmentUserKey={developmentUserKey}
          projectId={projectId}
          defaultInputUnit={editInputUnit}
          onClose={() => setShowExcel(false)}
          onApplied={(next) => {
            setShowExcel(false);
            dirtyRef.current = false;
            setReason('');
            setState({ kind: 'ready', data: next });
            setRows(next.panels.map((panel) => panelToRowForm(panel, editInputUnit)));
            load();
          }}
        />
      ) : null}
    </section>
  );
}

function PanelInfoEditDesktop({
  rows,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  rows: PanelInformationRowForm[];
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-table panel-info-edit-desktop" role="table" aria-label="패널정보 직접 입력" data-testid="panel-info-edit-desktop">
      <div className="panel-info-table-head" role="row">
        <span>No</span>
        <span>패널명</span>
        <span>W</span>
        <span>H</span>
        <span>D</span>
        <span>제품정보</span>
        <span>QR</span>
      </div>
      {rows.map((row) => (
        <PanelInformationEditableRow
          key={row.panelId}
          row={row}
          displayUnit={displayUnit}
          canEdit={canEdit && row.original.panelStatus === 'Active'}
          onPanelNameChange={onPanelNameChange}
          onSizeChange={onSizeChange}
        />
      ))}
    </div>
  );
}

function PanelInfoEditMobile({
  rows,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  rows: PanelInformationRowForm[];
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-cards panel-info-edit-mobile" data-testid="panel-info-edit-mobile">
      {rows.map((row) => (
        <PanelInformationCard
          key={row.panelId}
          row={row}
          displayUnit={displayUnit}
          canEdit={canEdit && row.original.panelStatus === 'Active'}
          onPanelNameChange={onPanelNameChange}
          onSizeChange={onSizeChange}
        />
      ))}
    </div>
  );
}

function PanelInformationEditableRow({
  row,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  row: PanelInformationRowForm;
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <div className="panel-info-table-row" role="row">
      <strong>{row.sequenceNumber}<small>{row.displayCode}</small></strong>
      <input aria-label={`${row.panelNumber} 패널명`} value={row.currentPanelName} disabled={!canEdit} onChange={(event) => onPanelNameChange(row.panelId, event.target.value)} />
      <input aria-label={`${row.panelNumber} W`} inputMode="decimal" value={row.widthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'widthInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} H`} inputMode="decimal" value={row.heightInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'heightInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} D`} inputMode="decimal" value={row.depthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'depthInput', event.target.value)} />
      <span className={row.original.panelInfoCompleted ? undefined : 'negative-text'}>{row.original.panelInfoCompleted ? '입력 완료' : '미입력'}</span>
      <span className={row.original.qrEligible ? undefined : 'negative-text'}>{row.original.qrEligible ? '생성 가능' : '생성 불가'}</span>
      <small className="panel-display-size">{formatPanelSizeInUnit(row.original, displayUnit)}</small>
    </div>
  );
}

function PanelInformationCard({
  row,
  displayUnit,
  canEdit,
  onPanelNameChange,
  onSizeChange
}: {
  row: PanelInformationRowForm;
  displayUnit: PanelInputUnit;
  canEdit: boolean;
  onPanelNameChange: (panelId: string, value: string) => void;
  onSizeChange: (panelId: string, field: 'widthInput' | 'heightInput' | 'depthInput', value: string) => void;
}) {
  return (
    <article className="panel-info-card" data-testid="panel-info-edit-card">
      <div className="subsection-header">
        <h3>{row.panelNumber}</h3>
        <span>{row.displayCode}</span>
      </div>
      <FormField label="패널명">
        <input value={row.currentPanelName} disabled={!canEdit} onChange={(event) => onPanelNameChange(row.panelId, event.target.value)} />
      </FormField>
      <div className="dimension-grid">
        <FormField label="W">
          <input inputMode="decimal" value={row.widthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'widthInput', event.target.value)} />
        </FormField>
        <FormField label="H">
          <input inputMode="decimal" value={row.heightInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'heightInput', event.target.value)} />
        </FormField>
        <FormField label="D">
          <input inputMode="decimal" value={row.depthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'depthInput', event.target.value)} />
        </FormField>
      </div>
      <dl className="mini-status-grid">
        <div><dt>제품정보</dt><dd className={row.original.panelInfoCompleted ? undefined : 'negative-text'}>{row.original.panelInfoCompleted ? '입력 완료' : '미입력'}</dd></div>
        <div><dt>QR</dt><dd className={row.original.qrEligible ? undefined : 'negative-text'}>{row.original.qrEligible ? '생성 가능' : '생성 불가'}</dd></div>
        <div><dt>표시</dt><dd>{formatPanelSizeInUnit(row.original, displayUnit)}</dd></div>
      </dl>
      {row.original.hasDuplicateName ? <p className="muted-text">동일 명칭 {row.original.duplicateNameCount}면</p> : null}
    </article>
  );
}

function PanelInformationExcelDialog({
  developmentUserKey,
  projectId,
  defaultInputUnit,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  projectId: string;
  defaultInputUnit: PanelInputUnit;
  onClose: () => void;
  onApplied: (response: PanelInformationResponse) => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [inputUnit, setInputUnit] = useState<PanelInputUnit>(defaultInputUnit);
  const [preview, setPreview] = useState<PanelInformationExcelPreviewResponse | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);

  async function previewFile() {
    if (!file) {
      setMessage('Excel 파일을 선택하세요.');
      return;
    }

    setIsPreviewing(true);
    setMessage('');
    try {
      setPreview(await previewPanelInformationExcel(developmentUserKey, projectId, file, inputUnit));
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function applyFile() {
    if (!file || !preview) {
      return;
    }

    if (preview.errorCount > 0) {
      setMessage('오류가 있는 Excel은 적용할 수 없습니다.');
      return;
    }

    if (preview.reasonRequired && !reason.trim()) {
      setMessage('기존 패널정보를 변경하려면 수정사유가 필요합니다.');
      return;
    }

    if (preview.newCount + preview.changedCount === 0) {
      setMessage('적용할 변경사항이 없습니다.');
      return;
    }

    setIsApplying(true);
    setMessage('');
    try {
      const expectedVersions = preview.rows
        .filter((row) => row.panelId && row.expectedPanelInfoVersion !== null)
        .map((row) => ({
          panelId: row.panelId!,
          expectedPanelInfoVersion: row.expectedPanelInfoVersion!
        }));
      const previewExpectedVersions = preview.expectedPanelInfoVersions.length > 0
        ? preview.expectedPanelInfoVersions
        : expectedVersions;
      const response = await applyPanelInformationExcel(
        developmentUserKey,
        projectId,
        file,
        inputUnit,
        preview.fileSha256,
        preview.expectedPackagingMethod,
        reason.trim() || null,
        previewExpectedVersions);
      onApplied(response);
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const canApplyPreview = preview !== null
    && preview.errorCount === 0
    && preview.newCount + preview.changedCount > 0
    && (!preview.reasonRequired || reason.trim().length > 0)
    && !isApplying;

  return (
    <div className="dialog-backdrop" role="dialog" aria-modal="true" aria-label="Excel 업로드">
      <div className="dialog wide-dialog">
        <div className="subsection-header">
          <h3>Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <label className="form-field">
            <span>파일</span>
            <input type="file" accept=".xlsx" onChange={(event) => {
              setFile(event.target.files?.[0] ?? null);
              setPreview(null);
              setMessage('');
            }} />
          </label>
          <label className="form-field">
            <span>파일 단위</span>
            <select value={inputUnit} onChange={(event) => setInputUnit(event.target.value as PanelInputUnit)}>
              <option value="Mm">mm</option>
              <option value="Inch">inch</option>
            </select>
          </label>
          <button type="button" onClick={previewFile} disabled={isPreviewing}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>

        {preview ? (
          <>
            <div className="excel-preview-action-bar">
              <div className="excel-preview-counts">
                <span>신규 {preview.newCount}건</span>
                <span>변경 {preview.changedCount}건</span>
                <span>동일 {preview.unchangedCount}건</span>
                <span>건너뜀 {preview.skippedCount}건</span>
                <span className={preview.errorCount > 0 ? 'negative-text' : undefined}>오류 {preview.errorCount}건</span>
              </div>
              {preview.reasonRequired ? (
                <label className="form-field excel-preview-reason">
                  <span>수정사유*</span>
                  <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
                </label>
              ) : null}
              {preview.newCount + preview.changedCount === 0 ? (
                <p className="muted-text">적용할 변경사항이 없습니다.</p>
              ) : null}
              <button type="button" className="primary-button" disabled={!canApplyPreview} onClick={applyFile}>
                {isApplying ? '저장 중' : 'Excel 저장'}
              </button>
            </div>
            <ExcelPreviewDesktop rows={preview.rows} />
            <ExcelPreviewMobile rows={preview.rows} />
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </div>
  );
}

function ExcelPreviewDesktop({ rows }: { rows: PanelInformationExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-table excel-preview-desktop" data-testid="excel-preview-desktop">
      {rows.map((row) => (
        <div key={`${row.excelRowNumber}-${row.no ?? 'no'}`} className="excel-preview-row" data-result={row.resultType}>
          <strong>Row {row.excelRowNumber}</strong>
          <span>{row.no ? `No.${row.no}` : 'No 없음'}</span>
          <span>{row.panelName ?? '패널명 없음'}</span>
          <span>{row.widthMm ?? '-'} / {row.heightMm ?? '-'} / {row.depthMm ?? '-'}</span>
          <span>{row.resultType}</span>
          <small>{row.errorMessages.join(' ')}</small>
        </div>
      ))}
    </div>
  );
}

function ExcelPreviewMobile({ rows }: { rows: PanelInformationExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-cards excel-preview-mobile" data-testid="excel-preview-mobile">
      {rows.map((row) => (
        <article key={`${row.excelRowNumber}-${row.no ?? 'no'}-mobile`} className="excel-preview-card" data-result={row.resultType}>
          <div className="subsection-header">
            <h3>{row.no ? `No.${row.no}` : `Row ${row.excelRowNumber}`}</h3>
            <span className={row.resultType === 'Error' ? 'negative-text' : undefined}>결과: {row.resultType}</span>
          </div>
          <dl className="mobile-detail-list">
            <div>
              <dt>Panel Name</dt>
              <dd>
                <span>기존: {row.currentValue?.panelName ?? '-'}</span>
                <span>변경: {row.panelName ?? '-'}</span>
              </dd>
            </div>
            <div>
              <dt>W/H/D</dt>
              <dd>
                <span>기존: {formatPreviewSize(row.currentValue)}</span>
                <span>변경: {formatPreviewSize(row)}</span>
              </dd>
            </div>
            <div>
              <dt>상태</dt>
              <dd className={row.resultType === 'Error' ? 'negative-text' : undefined}>
                {row.errorMessages.length > 0 ? row.errorMessages.join(' ') : previewResultLabel(row.resultType)}
              </dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function ProjectEditPage({
  developmentUserKey,
  projectId,
  onCancel,
  onSaved
}: {
  developmentUserKey: string;
  projectId: string;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [owners, setOwners] = useState<SalesOwner[]>([]);
  const [panels, setPanels] = useState<PanelPlaceholder[]>([]);
  const [form, setForm] = useState<ProjectFormValues>(emptyForm);
  const [selectedCancelPanels, setSelectedCancelPanels] = useState<string[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const initializedProjectIdRef = useRef<string | null>(null);
  const isDirtyRef = useRef(false);
  const loadRequestIdRef = useRef(0);

  useEffect(() => {
    let isCurrent = true;
    const requestId = loadRequestIdRef.current + 1;
    loadRequestIdRef.current = requestId;
    initializedProjectIdRef.current = null;
    isDirtyRef.current = false;
    setProjectState({ kind: 'loading' });
    setOwners([]);
    setPanels([]);
    setSelectedCancelPanels([]);
    setErrors({});
    setMessage('');
    setForm(emptyForm);

    Promise.all([
      getProject(developmentUserKey, projectId),
      getSalesOwners(developmentUserKey),
      listPanels(developmentUserKey, projectId)
    ])
      .then(([project, ownerItems, panelItems]) => {
        if (!isCurrent || requestId !== loadRequestIdRef.current) {
          return;
        }

        setProjectState({ kind: 'ready', data: project });
        setOwners(ownerItems);
        setPanels(panelItems);
        if (initializedProjectIdRef.current !== projectId && !isDirtyRef.current) {
          initializedProjectIdRef.current = projectId;
          setForm(projectToForm(project));
        }
      })
      .catch((error: unknown) => {
        if (!isCurrent || requestId !== loadRequestIdRef.current) {
          return;
        }

        setProjectState(toLoadError(error, '프로젝트 수정 정보를 불러올 수 없습니다.'));
      });

    return () => {
      isCurrent = false;
    };
  }, [developmentUserKey, projectId]);

  function handleFormChange(values: ProjectFormValues) {
    isDirtyRef.current = true;
    setForm(values);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    const validation = validateProjectForm(form, true);
    const targetPanelCount = Number(form.panelCount);
    const currentPanelCount = projectState.kind === 'ready' ? projectState.data.activePanelCount : targetPanelCount;
    if (targetPanelCount < currentPanelCount && selectedCancelPanels.length !== currentPanelCount - targetPanelCount) {
      validation.cancelPanelIds = '감소 면수만큼 취소할 패널을 선택하세요.';
    }

    setErrors(validation);
    setMessage('');
    if (Object.keys(validation).length > 0) {
      return;
    }

    setIsSaving(true);
    try {
      await updateProject(developmentUserKey, projectId, toUpdateRequest(form));
      if (targetPanelCount !== currentPanelCount) {
        await changePanelCount(developmentUserKey, projectId, {
          panelCount: targetPanelCount,
          expectedActivePanelCount: currentPanelCount,
          cancelPanelIds: targetPanelCount < currentPanelCount ? selectedCancelPanels : [],
          reason: form.reason
        });
      }
      isDirtyRef.current = false;
      onSaved();
    } catch (error) {
      handleFormError(error, setErrors, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  if (projectState.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p></section>;
  }

  if (projectState.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={projectState} /></section>;
  }

  const project = projectState.data;
  const activePanels = panels.filter((panel) => panel.panelStatus === 'Active');
  const targetPanelCount = Number(form.panelCount || project.activePanelCount);
  const isDecrease = Number.isFinite(targetPanelCount) && targetPanelCount < project.activePanelCount;

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Sales</p>
          <h2>프로젝트 수정</h2>
        </div>
        <button type="button" onClick={onCancel}>상세</button>
      </div>

      <ProjectForm
        form={form}
        owners={owners}
        errors={errors}
        isSaving={isSaving}
        submitLabel="저장"
        includeReason
        onChange={handleFormChange}
        onSubmit={submit}
      />

      {project.status === 'Cancelled' ? (
        <p role="alert" className="error-text">취소된 프로젝트는 재활성 후 면수를 변경할 수 있습니다.</p>
      ) : null}

      {isDecrease ? (
        <PanelCancellationSelector
          panels={activePanels}
          selectedPanelIds={selectedCancelPanels}
          onChange={setSelectedCancelPanels}
          error={errors.cancelPanelIds}
        />
      ) : null}

      {message ? <p role="alert" className="error-text">{message}</p> : null}
    </section>
  );
}

function PanelPlaceholderDetailPage({
  developmentUserKey,
  projectId,
  panelId,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  panelId: string;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<PanelPlaceholder>>({ kind: 'loading' });

  useEffect(() => {
    getPanel(developmentUserKey, projectId, panelId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '패널 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, panelId, projectId]);

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Product Panel</p>
          <h2>{state.kind === 'ready' ? `${state.data.displayCode} 제품 상세` : '제품 상세'}</h2>
        </div>
        <button type="button" onClick={onBack}>프로젝트</button>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <dl className="detail-grid">
          <div><dt>패널명</dt><dd>{state.data.panelName ?? '패널명 미입력'}</dd></div>
          <div><dt>제품상태</dt><dd>{formatPanelStatus(state.data.panelStatus)}</dd></div>
          <div><dt>패널정보</dt><dd>{state.data.panelInfoCompleted ? '완료' : '미완료'}</dd></div>
          <div><dt>QR 조건</dt><dd>{state.data.qrEligible ? '충족' : '미충족'}</dd></div>
          <div><dt>W/H/D</dt><dd>{formatSize(state.data)}</dd></div>
        </dl>
      ) : null}
    </section>
  );
}

function DeletedProjectDetailPage({
  developmentUserKey,
  projectId,
  canReadSalesAmount,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  canReadSalesAmount: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<DeletedProjectDetail>>({ kind: 'loading' });

  useEffect(() => {
    getDeletedProject(developmentUserKey, projectId)
      .then((data) => setState({ kind: 'ready', data }))
      .catch((error: unknown) => setState(toLoadError(error, '삭제 프로젝트 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, projectId]);

  if (state.kind === 'loading') {
    return <section className="page-surface"><p className="muted-text">Loading</p></section>;
  }

  if (state.kind !== 'ready') {
    return <section className="page-surface"><StateMessage state={state} /></section>;
  }

  const project = state.data;
  return (
    <section className="page-surface deleted-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Deleted Archive</p>
          <h2>{project.projectTitle}</h2>
        </div>
        <button type="button" onClick={onBack}>목록</button>
      </div>

      <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />
      <dl className="detail-grid">
        <div><dt>삭제일시</dt><dd>{formatDateTime(project.deletedAtUtc)}</dd></div>
        <div><dt>삭제자</dt><dd>{project.deletedByUserName ?? project.deletedByUserId ?? '-'}</dd></div>
        <div><dt>삭제 사유</dt><dd>{project.deleteReason}</dd></div>
      </dl>

      <section className="subsection">
        <h3>보존된 제품·패널 목록</h3>
        <PanelPlaceholderList panels={project.panels} onOpenPanel={() => undefined} />
      </section>

      <section className="subsection">
        <h3>삭제 프로젝트 변경이력</h3>
        <AuditHistory events={project.auditHistory} />
      </section>
    </section>
  );
}

function ProjectForm({
  form,
  owners,
  errors,
  isSaving,
  submitLabel,
  includeReason = false,
  onChange,
  onSubmit
}: {
  form: ProjectFormValues;
  owners: SalesOwner[];
  errors: Record<string, string>;
  isSaving: boolean;
  submitLabel: string;
  includeReason?: boolean;
  onChange: (values: ProjectFormValues) => void;
  onSubmit: (event: FormEvent) => void;
}) {
  const setField = (field: keyof ProjectFormValues, value: string) => onChange({ ...form, [field]: value });

  return (
    <form className="project-form" onSubmit={onSubmit}>
      <FormField label="고객사*" error={errors.customerName}>
        <input value={form.customerName} onChange={(event) => setField('customerName', event.target.value)} />
      </FormField>
      <FormField label="Item*" error={errors.item}>
        <input value={form.item} onChange={(event) => setField('item', event.target.value)} />
      </FormField>
      <FormField label="PJT Code*" error={errors.projectCode}>
        <input value={form.projectCode} onChange={(event) => setField('projectCode', event.target.value)} />
      </FormField>
      <FormField label="PJT Title*" error={errors.projectTitle}>
        <input value={form.projectTitle} onChange={(event) => setField('projectTitle', event.target.value)} />
      </FormField>
      <FormField label="면수*" error={errors.panelCount}>
        <input
          min="1"
          max={maxPanelsPerProject}
          type="number"
          value={form.panelCount}
          onChange={(event) => setField('panelCount', event.target.value)}
        />
      </FormField>
      <FormField label="납기일*" error={errors.deliveryDate}>
        <input type="date" value={form.deliveryDate} onChange={(event) => setField('deliveryDate', event.target.value)} />
      </FormField>
      <FormField label="영업담당자*" error={errors.salesOwnerUserId}>
        <select value={form.salesOwnerUserId} onChange={(event) => setField('salesOwnerUserId', event.target.value)}>
          <option value="">선택</option>
          {owners.map((owner) => (
            <option key={owner.userId} value={owner.userId}>{owner.displayName}</option>
          ))}
        </select>
      </FormField>
      <FormField label="포장방식*" error={errors.packagingMethod}>
        <select value={form.packagingMethod} onChange={(event) => setField('packagingMethod', event.target.value)}>
          <option value="">선택</option>
          <option value="WoodenCrate">목포장</option>
          <option value="StretchWrap">청랩포장</option>
          <option value="HeavyDutyBox">고강도박스포장</option>
        </select>
      </FormField>
      <FormField label="판매금액" error={errors.salesAmount}>
        <input value={form.salesAmount} inputMode="decimal" onChange={(event) => setField('salesAmount', event.target.value)} />
      </FormField>
      <FormField label="통화" error={errors.currencyCode}>
        <input maxLength={3} value={form.currencyCode} onChange={(event) => setField('currencyCode', event.target.value.toUpperCase())} />
      </FormField>
      <FormField label="납품장소" error={errors.deliveryLocation}>
        <input value={form.deliveryLocation} onChange={(event) => setField('deliveryLocation', event.target.value)} />
      </FormField>
      {includeReason ? (
        <FormField label="수정사유*" error={errors.reason}>
          <textarea value={form.reason} onChange={(event) => setField('reason', event.target.value)} />
        </FormField>
      ) : null}
      <div className="form-actions">
        <button type="submit" className="primary-button" disabled={isSaving}>
          {isSaving ? '저장 중' : submitLabel}
        </button>
      </div>
    </form>
  );
}

function FormField({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      {children}
      {error ? <small role="alert">{error}</small> : null}
    </label>
  );
}

function useIsMobileViewport() {
  const query = '(max-width: 860px)';
  const [isMobile, setIsMobile] = useState(() => window.matchMedia?.(query).matches ?? false);

  useEffect(() => {
    const mediaQuery = window.matchMedia?.(query);
    if (!mediaQuery) {
      return;
    }

    const update = (event: MediaQueryListEvent) => setIsMobile(event.matches);
    mediaQuery.addEventListener('change', update);
    return () => mediaQuery.removeEventListener('change', update);
  }, []);

  return isMobile;
}

function ProjectSummary({ project, canReadSalesAmount }: { project: ProjectListItem; canReadSalesAmount: boolean }) {
  return (
    <dl className="detail-grid">
      <div><dt>상태</dt><dd><ProjectStatusBadge status={project.status} /></dd></div>
      <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
      <div><dt>Item</dt><dd>{project.item}</dd></div>
      <div><dt>PJT Code</dt><dd>{project.projectCode}</dd></div>
      <div><dt>면수</dt><dd>{project.activePanelCount}</dd></div>
      <div><dt>납기일</dt><dd>{formatDate(project.deliveryDate)}</dd></div>
      <div><dt>영업담당자</dt><dd>{project.salesOwnerName}</dd></div>
      <div><dt>포장방식</dt><dd>{formatPackagingMethod(project.packagingMethod)}</dd></div>
      <div><dt>납품장소</dt><dd>{project.deliveryLocation ?? '-'}</dd></div>
      {canReadSalesAmount && project.salesAmount !== undefined ? (
        <div><dt>판매금액</dt><dd><SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} /></dd></div>
      ) : null}
      <div><dt>진행률</dt><dd>체크리스트 적용 후 계산</dd></div>
    </dl>
  );
}

function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return <span className="status-badge" data-status={status}>{formatProjectStatus(status)}</span>;
}

function SalesAmountField({ amount, currencyCode }: { amount: number; currencyCode?: string }) {
  return <span>{currencyCode ?? ''} {amount.toLocaleString()}</span>;
}

function PanelPlaceholderList({
  panels,
  onOpenPanel
}: {
  panels: PanelPlaceholder[];
  onOpenPanel: (panelId: string) => void;
}) {
  if (panels.length === 0) {
    return <p className="empty-text">제품·패널이 없습니다.</p>;
  }

  return (
    <div className="panel-list">
      {panels.map((panel) => (
        <button key={panel.panelId} type="button" className="panel-row" onClick={() => onOpenPanel(panel.panelId)}>
          <strong>{panel.displayCode}</strong>
          <span>{panel.panelName ?? '패널명 미입력'}</span>
          <span>{panel.panelInfoCompleted ? '패널정보 완료' : '패널정보 대기'}</span>
          <span>{panel.qrEligible ? 'QR 생성 조건 충족' : 'QR 생성 조건 미충족'}</span>
          <span>{formatPanelStatus(panel.panelStatus)}</span>
        </button>
      ))}
    </div>
  );
}

function ProjectPanelList({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  if (panels.length === 0) {
    return <p className="empty-text">제품·패널이 없습니다.</p>;
  }

  return (
    <>
      <PanelListDesktop
        panels={panels}
        packagingMethod={packagingMethod}
        displayUnit={displayUnit}
        onOpenPanel={onOpenPanel}
      />
      <PanelListMobile
        panels={panels}
        packagingMethod={packagingMethod}
        displayUnit={displayUnit}
        onOpenPanel={onOpenPanel}
      />
    </>
  );
}

function PanelListDesktop({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  return (
    <div className="product-panel-table product-panel-desktop" role="table" aria-label="제품·패널 목록" data-testid="project-panel-list-desktop">
      <div className="product-panel-table-head" role="row">
        <span>No</span>
        <span>패널명</span>
        <span>사이즈</span>
        <span>제품정보</span>
        <span>QR</span>
        <span>상태</span>
      </div>
      {panels.map((panel) => (
        <button key={panel.panelId} type="button" className="product-panel-row" role="row" onClick={() => onOpenPanel(panel.panelId)}>
          <span>{panel.sequenceNumber}</span>
          <span>
            <strong className={panel.panelName ? undefined : 'negative-text'}>{panel.panelName ?? '미입력'}</strong>
            {panel.hasDuplicateName ? <small>동일 명칭 {panel.duplicateNameCount}면</small> : null}
          </span>
          <span className={panelSizeClass(panel, packagingMethod)}>
            {formatPanelSizeForPackaging(panel, displayUnit, packagingMethod)}
          </span>
          <span className={panel.panelInfoCompleted ? undefined : 'negative-text'}>
            {panel.panelInfoCompleted ? '입력 완료' : '미입력'}
          </span>
          <span className={panel.qrEligible ? undefined : 'negative-text'}>
            {panel.qrEligible ? '생성 가능' : '생성 불가'}
          </span>
          <span>{formatWorkflowStage(panel.workflowStage)}</span>
        </button>
      ))}
    </div>
  );
}

function PanelListMobile({
  panels,
  packagingMethod,
  displayUnit,
  onOpenPanel
}: {
  panels: PanelInformationPanel[];
  packagingMethod: PackagingMethod | null;
  displayUnit: PanelInputUnit;
  onOpenPanel: (panelId: string) => void;
}) {
  return (
    <div className="product-panel-cards product-panel-mobile" data-testid="project-panel-list-mobile">
      {panels.map((panel) => (
        <article key={panel.panelId} className="product-panel-card" data-testid="project-panel-card">
          <div className="subsection-header">
            <h3>{panel.panelNumber}</h3>
            <button type="button" onClick={() => onOpenPanel(panel.panelId)}>상세 보기</button>
          </div>
          <dl className="mobile-detail-list">
            <div>
              <dt>패널명</dt>
              <dd>
                <strong className={panel.panelName ? undefined : 'negative-text'}>{panel.panelName ?? '미입력'}</strong>
                {panel.hasDuplicateName ? <small>동일 명칭 {panel.duplicateNameCount}면</small> : null}
              </dd>
            </div>
            <div>
              <dt>사이즈</dt>
              <dd className={panelSizeClass(panel, packagingMethod)}>{formatPanelSizeForPackaging(panel, displayUnit, packagingMethod)}</dd>
            </div>
            <div>
              <dt>제품정보</dt>
              <dd className={panel.panelInfoCompleted ? undefined : 'negative-text'}>{panel.panelInfoCompleted ? '입력 완료' : '미입력'}</dd>
            </div>
            <div>
              <dt>QR</dt>
              <dd className={panel.qrEligible ? undefined : 'negative-text'}>{panel.qrEligible ? '생성 가능' : '생성 불가'}</dd>
            </div>
            <div>
              <dt>상태</dt>
              <dd>{formatWorkflowStage(panel.workflowStage)}</dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function PanelCancellationSelector({
  panels,
  selectedPanelIds,
  error,
  onChange
}: {
  panels: PanelPlaceholder[];
  selectedPanelIds: string[];
  error?: string;
  onChange: (ids: string[]) => void;
}) {
  return (
    <section className="subsection">
      <h3>취소할 패널 선택</h3>
      <div className="checkbox-grid">
        {panels.map((panel) => (
          <label key={panel.panelId}>
            <input
              type="checkbox"
              checked={selectedPanelIds.includes(panel.panelId)}
              onChange={(event) => {
                onChange(event.target.checked
                  ? [...selectedPanelIds, panel.panelId]
                  : selectedPanelIds.filter((id) => id !== panel.panelId));
              }}
            />
            <span>{panel.displayCode}</span>
          </label>
        ))}
      </div>
      {error ? <p role="alert" className="error-text">{error}</p> : null}
    </section>
  );
}

function StatusReasonDialog({
  action,
  reason,
  error,
  isSaving,
  onReasonChange,
  onCancel,
  onSubmit
}: {
  action: 'hold' | 'resume' | 'cancel' | 'reactivate';
  reason: string;
  error: string;
  isSaving: boolean;
  onReasonChange: (reason: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  const title = {
    hold: '프로젝트 보류',
    resume: '보류 해제',
    cancel: '프로젝트 취소',
    reactivate: '프로젝트 재활성'
  }[action];

  return (
    <div className="dialog-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <div className="dialog">
        <h3>{title}</h3>
        <label className="form-field">
          <span>사유*</span>
          <textarea value={reason} onChange={(event) => onReasonChange(event.target.value)} />
          {error ? <small role="alert">{error}</small> : null}
        </label>
        <div className="button-row">
          <button type="button" onClick={onCancel}>닫기</button>
          <button type="button" className="primary-button" disabled={isSaving} onClick={onSubmit}>
            {isSaving ? '처리 중' : '확인'}
          </button>
        </div>
      </div>
    </div>
  );
}

function DeleteProjectDialog({
  projectTitle,
  reason,
  confirmProjectTitle,
  error,
  isSaving,
  onReasonChange,
  onConfirmProjectTitleChange,
  onCancel,
  onSubmit
}: {
  projectTitle: string;
  reason: string;
  confirmProjectTitle: string;
  error: string;
  isSaving: boolean;
  onReasonChange: (reason: string) => void;
  onConfirmProjectTitleChange: (value: string) => void;
  onCancel: () => void;
  onSubmit: () => void;
}) {
  return (
    <div className="dialog-backdrop" role="dialog" aria-modal="true" aria-label="프로젝트 삭제">
      <div className="dialog">
        <h3>프로젝트 삭제</h3>
        <p className="warning-text">
          삭제는 오등록·중복등록 프로젝트를 일반 업무목록에서 제거하는 기능입니다. 실제로 중단된 프로젝트는 취소 기능을 사용해 주세요.
        </p>
        <p className="muted-text">확인할 PJT Title: {projectTitle}</p>
        <label className="form-field">
          <span>삭제 사유*</span>
          <textarea value={reason} onChange={(event) => onReasonChange(event.target.value)} />
        </label>
        <label className="form-field">
          <span>PJT Title 확인 입력*</span>
          <input value={confirmProjectTitle} onChange={(event) => onConfirmProjectTitleChange(event.target.value)} />
        </label>
        {error ? <p role="alert" className="error-text">{error}</p> : null}
        <div className="button-row">
          <button type="button" onClick={onCancel}>닫기</button>
          <button type="button" className="danger-button" disabled={isSaving} onClick={onSubmit}>
            {isSaving ? '삭제 중' : '삭제'}
          </button>
        </div>
      </div>
    </div>
  );
}

function AuditHistory({ events }: { events: AuditEvent[] }) {
  if (events.length === 0) {
    return <p className="empty-text">변경이력이 없습니다.</p>;
  }

  return (
    <ol className="audit-list">
      {events.map((event) => (
        <li key={event.auditEventId}>
          <strong>{event.panelDisplayName ?? event.action}</strong>
          <span>{event.fieldName ? `${event.fieldName}: ${event.oldValue ?? '-'} → ${event.newValue ?? '-'}` : event.reason ?? '-'}</span>
          {event.entityType === 'Panel' ? <small>입력 방식: {formatInputSource(event.inputSource)}</small> : null}
          {event.importFileName ? <small>입력 파일: {event.importFileName}</small> : null}
          {event.inputUnit ? <small>입력 단위: {formatInputUnit(event.inputUnit)}</small> : null}
          {event.originalInputValue ? (
            <small>입력값: {event.originalInputValue}{event.inputUnit ? ` ${formatInputUnit(event.inputUnit)}` : ''}</small>
          ) : null}
          {event.fieldName?.endsWith('Mm') && event.newValue ? <small>저장값: {event.newValue} mm</small> : null}
          {event.importBatchId && event.importFileName ? <small>Excel Batch: {event.importFileName}</small> : null}
          {event.reason ? <small>수정사유: {event.reason}</small> : null}
          <small>{event.changedByUserName ?? event.changedByUserId ?? '-'} · {formatDateTime(event.changedAtUtc)}</small>
        </li>
      ))}
    </ol>
  );
}

function GroupedHistory({ groups }: { groups: PanelInformationHistoryResponse['groups'] }) {
  if (groups.length === 0) {
    return <p className="empty-text">전체 이력이 없습니다.</p>;
  }

  return (
    <ol className="audit-list grouped-audit-list">
      {groups.map((group) => (
        <li key={group.groupId}>
          <strong>{formatInputSource(group.inputSource)} · 대상 패널 {group.affectedPanelCount}면</strong>
          <span>변경항목 {group.changeCount}건</span>
          {group.importFileName ? <small>입력 파일: {group.importFileName}</small> : null}
          {group.reason ? <small>수정사유: {group.reason}</small> : null}
          <small>{group.changedByName ?? group.changedByUserId ?? '-'} · {formatDateTime(group.changedAtUtc)}</small>
          <details>
            <summary>변경 상세</summary>
            <ol className="audit-change-list">
              {group.changes.map((change, index) => (
                <li key={`${group.groupId}-${change.entityId}-${change.fieldName ?? index}-${index}`}>
                  <strong>{change.panelDisplayName ?? change.panelNumber ?? change.displayCode ?? change.entityType}</strong>
                  <span>{change.fieldName ?? '-'}: {change.oldValue ?? '-'} → {change.newValue ?? '-'}</span>
                  {change.originalInputValue ? (
                    <small>원본 입력값: {change.originalInputValue}{change.inputUnit ? ` ${formatInputUnit(change.inputUnit)}` : ''}</small>
                  ) : null}
                  {change.inputUnit ? <small>입력단위: {formatInputUnit(change.inputUnit)}</small> : null}
                </li>
              ))}
            </ol>
          </details>
        </li>
      ))}
    </ol>
  );
}

function formatInputSource(source: AuditEvent['inputSource']) {
  if (source === 'Direct') {
    return '직접 입력';
  }

  if (source === 'Excel') {
    return 'Excel 입력';
  }

  return '기존 이력';
}

function formatInputUnit(unit: PanelInputUnit) {
  return unit === 'Inch' ? 'inch' : 'mm';
}

function StatusChip({ label, value, tone }: { label: string; value: string; tone?: 'danger' }) {
  return (
    <div className="status-chip" data-tone={tone}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function StateMessage<T>({ state }: { state: LoadState<T> }) {
  if (state.kind === 'forbidden') {
    return <p role="alert" className="error-text">권한이 없습니다.</p>;
  }

  if (state.kind === 'not-found') {
    return <p role="alert" className="error-text">대상을 찾을 수 없습니다.</p>;
  }

  if (state.kind === 'error') {
    return <p role="alert" className="error-text">{state.message}</p>;
  }

  return null;
}

function validateProjectForm(form: ProjectFormValues, includeReason: boolean): Record<string, string> {
  const errors: Record<string, string> = {};
  const required: Array<keyof ProjectFormValues> = [
    'customerName',
    'item',
    'projectCode',
    'projectTitle',
    'panelCount',
    'deliveryDate',
    'salesOwnerUserId'
  ];

  for (const field of required) {
    if (!form[field].trim()) {
      errors[field] = '필수 입력값입니다.';
    }
  }

  if (Number(form.panelCount) < 1 || !Number.isInteger(Number(form.panelCount))) {
    errors.panelCount = '1 이상의 정수여야 합니다.';
  }

  if (Number(form.panelCount) > maxPanelsPerProject) {
    errors.panelCount = `1 이상 ${maxPanelsPerProject} 이하의 정수여야 합니다.`;
  }

  if (!form.packagingMethod.trim()) {
    errors.packagingMethod = '포장방식은 필수 선택값입니다.';
  }

  if (form.salesAmount.trim() && Number(form.salesAmount) < 0) {
    errors.salesAmount = '0 이상의 금액이어야 합니다.';
  }

  if (form.salesAmount.trim() && !/^[A-Z]{3}$/.test(form.currencyCode.trim())) {
    errors.currencyCode = '통화는 3자리 대문자여야 합니다.';
  }

  if (includeReason && !form.reason.trim()) {
    errors.reason = '수정사유는 필수입니다.';
  }

  return errors;
}

function toCreateRequest(form: ProjectFormValues) {
  return {
    customerName: form.customerName.trim(),
    item: form.item.trim(),
    projectCode: form.projectCode.trim(),
    projectTitle: form.projectTitle.trim(),
    panelCount: Number(form.panelCount),
    deliveryDate: form.deliveryDate,
    salesOwnerUserId: form.salesOwnerUserId,
    packagingMethod: toPackagingMethod(form.packagingMethod),
    salesAmount: form.salesAmount.trim() ? Number(form.salesAmount) : null,
    currencyCode: form.salesAmount.trim() ? form.currencyCode.trim().toUpperCase() : null,
    deliveryLocation: form.deliveryLocation.trim() || null
  };
}

function toPackagingMethod(value: string): PackagingMethod | null {
  if (value === 'WoodenCrate' || value === 'StretchWrap' || value === 'HeavyDutyBox') {
    return value;
  }

  return null;
}

function projectToForm(project: ProjectDetail): ProjectFormValues {
  return {
    customerName: project.customerName,
    item: project.item,
    projectCode: project.projectCode,
    projectTitle: project.projectTitle,
    panelCount: String(project.activePanelCount),
    deliveryDate: project.deliveryDate,
    salesOwnerUserId: project.salesOwnerUserId,
    packagingMethod: project.packagingMethod ?? '',
    salesAmount: project.salesAmount === undefined ? '' : String(project.salesAmount),
    currencyCode: project.currencyCode ?? 'KRW',
    deliveryLocation: project.deliveryLocation ?? '',
    reason: ''
  };
}

function toUpdateRequest(form: ProjectFormValues) {
  return {
    ...toCreateRequest(form),
    reason: form.reason.trim()
  };
}

function handleFormError(
  error: unknown,
  setErrors: (errors: Record<string, string>) => void,
  setMessage: (message: string) => void
) {
  if (error instanceof ApiError && error.errors) {
    setErrors(Object.fromEntries(Object.entries(error.errors).map(([key, value]) => [lowerFirst(key), value[0] ?? '입력값을 확인하세요.'])));
    setMessage(error.message);
    return;
  }

  setMessage(error instanceof Error ? error.message : '요청을 처리할 수 없습니다.');
}

function toLoadError<T>(error: unknown, fallback: string): LoadState<T> {
  if (error instanceof ApiError) {
    if (error.status === 403) {
      return { kind: 'forbidden', message: error.message };
    }

    if (error.status === 404) {
      return { kind: 'not-found', message: error.message };
    }

    return { kind: 'error', message: error.message };
  }

  return { kind: 'error', message: error instanceof Error ? error.message : fallback };
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError';
}

function lowerFirst(value: string) {
  return value.length === 0 ? value : `${value[0].toLowerCase()}${value.slice(1)}`;
}

function formatDate(value: string) {
  return value;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function formatPackagingMethod(value: string | null) {
  if (value === 'WoodenCrate') {
    return '목포장';
  }

  if (value === 'StretchWrap') {
    return '청랩포장';
  }

  if (value === 'HeavyDutyBox') {
    return '고강도박스포장';
  }

  return '미지정';
}

function formatProjectStatus(status: ProjectStatus) {
  return {
    Active: '진행',
    OnHold: '보류',
    Completed: '완료',
    Cancelled: '취소'
  }[status];
}

function formatProjectWorkStatus(status: ProjectWorkStatus) {
  return {
    BeforeManufacturing: '제조 전',
    ManufacturingInProgress: '제조 중',
    ManufacturingCompleted: '제조 완료',
    InspectionInProgress: '검사 중',
    InspectionCompleted: '검사 완료',
    ReadyForShipment: '출하 준비',
    ShipmentCompleted: '출하 완료',
    OnHold: '보류',
    Completed: '완료',
    Cancelled: '취소'
  }[status];
}

function formatProjectProgress(progressPercent: number | null) {
  return progressPercent === null ? '-' : `${progressPercent}%`;
}

function formatPanelStatus(status: PanelInformationPanel['panelStatus'] | PanelPlaceholder['panelStatus']) {
  return status === 'Active' ? '진행' : '취소';
}

function formatWorkflowStage(stage: ProductWorkflowStage) {
  return {
    BeforeManufacturing: '제조 전',
    ManufacturingInProgress: '제조 중',
    ManufacturingCompleted: '제조 완료',
    InspectionInProgress: '검사 중',
    InspectionCompleted: '검사 완료',
    PackingCompleted: '포장 완료',
    ShipmentCompleted: '출하 완료'
  }[stage];
}

function previewResultLabel(resultType: PanelInformationExcelPreviewResponse['rows'][number]['resultType']) {
  return {
    New: '적용 예정',
    Changed: '적용 예정',
    Unchanged: '동일',
    Skipped: '건너뜀',
    Error: '오류'
  }[resultType];
}

function formatPreviewSize(value: Pick<PanelInformationPanel, 'widthMm' | 'heightMm' | 'depthMm'> | PanelInformationExcelPreviewResponse['rows'][number] | null) {
  if (!value || value.widthMm === null || value.heightMm === null || value.depthMm === null) {
    return '-';
  }

  return `${canonicalMmString(value.widthMm)} / ${canonicalMmString(value.heightMm)} / ${canonicalMmString(value.depthMm)}`;
}

function readDisplayUnit(): PanelInputUnit {
  return window.localStorage.getItem('emi-qms-panel-display-unit') === 'Inch' ? 'Inch' : 'Mm';
}

function panelToRowForm(panel: PanelInformationPanel, inputUnit: PanelInputUnit): PanelInformationRowForm {
  const originalWidthMm = canonicalMmString(panel.widthMm);
  const originalHeightMm = canonicalMmString(panel.heightMm);
  const originalDepthMm = canonicalMmString(panel.depthMm);
  return {
    panelId: panel.panelId,
    sequenceNumber: panel.sequenceNumber,
    panelNumber: panel.panelNumber,
    displayCode: panel.displayCode,
    panelInfoVersion: panel.panelInfoVersion,
    original: panel,
    originalPanelName: panel.panelName ?? '',
    currentPanelName: panel.panelName ?? '',
    panelNameDirty: false,
    originalWidthMm,
    originalHeightMm,
    originalDepthMm,
    widthInput: formatInputDimension(originalWidthMm, inputUnit),
    heightInput: formatInputDimension(originalHeightMm, inputUnit),
    depthInput: formatInputDimension(originalDepthMm, inputUnit),
    sizeDirty: false,
    sizeClearRequested: false,
    sizeInputUnit: inputUnit
  };
}

function rowWithSizeInputs(row: PanelInformationRowForm, inputUnit: PanelInputUnit): PanelInformationRowForm {
  return {
    ...row,
    widthInput: formatInputDimension(row.originalWidthMm, inputUnit),
    heightInput: formatInputDimension(row.originalHeightMm, inputUnit),
    depthInput: formatInputDimension(row.originalDepthMm, inputUnit),
    sizeInputUnit: inputUnit,
    sizeDirty: false,
    sizeClearRequested: false
  };
}

function canonicalMmString(valueMm: number | null) {
  return valueMm === null ? null : trimTrailingZeros(valueMm.toFixed(3));
}

function formatInputDimension(valueMm: string | null, unit: PanelInputUnit) {
  if (valueMm === null) {
    return '';
  }

  const numericMm = Number(valueMm);
  const value = unit === 'Inch' ? numericMm / 25.4 : numericMm;
  return trimTrailingZeros(value.toFixed(unit === 'Inch' ? 3 : 3));
}

function decimalOrNull(value: string) {
  const trimmed = value.trim();
  return trimmed ? Number(trimmed) : null;
}

function panelRowChanged(row: PanelInformationRowForm) {
  return panelNameActuallyChanged(row) || sizeActuallyChanged(row);
}

function panelRowNeedsReason(row: PanelInformationRowForm) {
  if (!panelRowChanged(row)) {
    return false;
  }

  return (panelNameActuallyChanged(row) && row.originalPanelName !== '')
    || (sizeActuallyChanged(row) && (
      row.originalWidthMm !== null
      || row.originalHeightMm !== null
      || row.originalDepthMm !== null
    ));
}

function panelNameActuallyChanged(row: PanelInformationRowForm) {
  return row.panelNameDirty && row.currentPanelName.trim() !== row.originalPanelName;
}

function sizeActuallyChanged(row: PanelInformationRowForm) {
  if (!row.sizeDirty) {
    return false;
  }

  return row.originalWidthMm !== canonicalInputToMmString(decimalOrNull(row.widthInput), row.sizeInputUnit)
    || row.originalHeightMm !== canonicalInputToMmString(decimalOrNull(row.heightInput), row.sizeInputUnit)
    || row.originalDepthMm !== canonicalInputToMmString(decimalOrNull(row.depthInput), row.sizeInputUnit);
}

function canonicalInputToMmString(value: number | null, unit: PanelInputUnit) {
  if (value === null) {
    return null;
  }

  return trimTrailingZeros(round3(unit === 'Inch' ? value * 25.4 : value).toFixed(3));
}

function round3(value: number) {
  return Math.round((value + Number.EPSILON) * 1000) / 1000;
}

function panelRowToUpdateRequest(row: PanelInformationRowForm) {
  const request: {
    panelId: string;
    expectedPanelInfoVersion: number;
    panelNameUpdate?: { isChanged: boolean; value: string | null };
    sizeUpdate?: {
      isChanged: boolean;
      clear: boolean;
      inputUnit: PanelInputUnit | null;
      width: number | null;
      height: number | null;
      depth: number | null;
    };
  } = {
    panelId: row.panelId,
    expectedPanelInfoVersion: row.panelInfoVersion
  };

  if (panelNameActuallyChanged(row)) {
    request.panelNameUpdate = {
      isChanged: true,
      value: row.currentPanelName.trim() || null
    };
  }

  if (row.sizeDirty) {
    request.sizeUpdate = {
      isChanged: true,
      clear: row.sizeClearRequested,
      inputUnit: row.sizeClearRequested ? null : row.sizeInputUnit,
      width: row.sizeClearRequested ? null : decimalOrNull(row.widthInput),
      height: row.sizeClearRequested ? null : decimalOrNull(row.heightInput),
      depth: row.sizeClearRequested ? null : decimalOrNull(row.depthInput)
    };
  }

  return request;
}

function trimTrailingZeros(value: string) {
  return value.replace(/\.?0+$/, '');
}

function successMessage(message: string) {
  return message.includes('저장했습니다') || message.includes('다운로드했습니다');
}

function formatPanelSizeInUnit(panel: PanelInformationPanel, unit: PanelInputUnit) {
  if (panel.widthMm === null || panel.heightMm === null || panel.depthMm === null) {
    return '미입력';
  }

  if (unit === 'Inch') {
    return `${(panel.widthMm / 25.4).toFixed(2)} × ${(panel.heightMm / 25.4).toFixed(2)} × ${(panel.depthMm / 25.4).toFixed(2)} inch`;
  }

  return `${trimTrailingZeros(panel.widthMm.toFixed(3))} × ${trimTrailingZeros(panel.heightMm.toFixed(3))} × ${trimTrailingZeros(panel.depthMm.toFixed(3))} mm`;
}

function formatPanelSizeForPackaging(
  panel: PanelInformationPanel,
  unit: PanelInputUnit,
  packagingMethod: PackagingMethod | null
) {
  if (panel.widthMm !== null && panel.heightMm !== null && panel.depthMm !== null) {
    return formatPanelSizeInUnit(panel, unit);
  }

  return packagingMethod === 'WoodenCrate' ? '미입력' : '선택사항';
}

function panelSizeClass(panel: PanelInformationPanel, packagingMethod: PackagingMethod | null) {
  return packagingMethod === 'WoodenCrate'
    && (panel.widthMm === null || panel.heightMm === null || panel.depthMm === null)
    ? 'negative-text'
    : undefined;
}

function projectTabs(canReadDeleted: boolean): Array<{ value: ProjectListTab; label: string }> {
  const tabs: Array<{ value: ProjectListTab; label: string }> = [
    { value: 'All', label: '전체' },
    { value: 'Active', label: '진행' },
    { value: 'OnHold', label: '보류' },
    { value: 'Completed', label: '완료' },
    { value: 'Cancelled', label: '취소' }
  ];

  if (canReadDeleted) {
    tabs.push({ value: 'Deleted', label: '삭제 보관함' });
  }

  return tabs;
}

function formatSize(panel: PanelPlaceholder) {
  if (panel.width === null || panel.height === null || panel.depth === null) {
    return '미입력';
  }

  return `${panel.width} × ${panel.height} × ${panel.depth} mm`;
}
