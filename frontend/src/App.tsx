import { Fragment, FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  applyPanelInformationExcel,
  applyProjectExcel,
  applyProcurementExcel,
  changePanelCount,
  changeProjectStatus,
  createProject,
  defaultDevelopmentUserKey,
  deleteProject,
  downloadPanelInformationTemplate,
  downloadProjectExcelTemplate,
  downloadProcurementTemplate,
  getCurrentUser,
  getDeletedProject,
  getPanel,
  getPanelInformation,
  getPanelInformationHistory,
  getProject,
  getProjectProcurement,
  getProjectProcurementHistory,
  getProjectSummary,
  getProcurementDashboard,
  getMaterialReceipts,
  getReadyHealth,
  getSalesOwners,
  listDeletedProjects,
  listPanels,
  listProjects,
  previewPanelInformationExcel,
  previewProjectExcel,
  previewProcurementExcel,
  purgeAllDeletedProjects,
  purgeDeletedProject,
  restoreDeletedProject,
  updateMaterialReceipts,
  updatePanelInformation,
  updateProjectProcurement,
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
  ProcurementExcelPreviewResponse,
  ProcurementDashboardResponse,
  ProcurementHistoryResponse,
  ProcurementItem,
  ProcurementProjectSummary,
  ProcurementResponse,
  ProjectDetail,
  ProjectDashboardSummary,
  ProjectExcelPreviewResponse,
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
  | { kind: 'detail'; projectId: string; section?: 'panels' | 'procurement' }
  | { kind: 'deleted-detail'; projectId: string }
  | { kind: 'edit'; projectId: string }
  | { kind: 'panel-info-edit'; projectId: string }
  | { kind: 'procurement-edit'; projectId: string }
  | { kind: 'procurement-dashboard' }
  | { kind: 'materials-receipts' }
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
  'dev-procurement',
  'dev-materials',
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

  const procurementEditMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/procurement\/edit$/);
  if (procurementEditMatch?.[1]) {
    return { kind: 'procurement-edit', projectId: procurementEditMatch[1] };
  }

  if (window.location.pathname === '/materials/receipts') {
    return { kind: 'materials-receipts' };
  }

  if (window.location.pathname === '/procurement') {
    return { kind: 'procurement-dashboard' };
  }

  const panelMatch = window.location.pathname.match(/^\/projects\/([^/]+)\/panels\/([^/]+)$/);
  if (panelMatch?.[1] && panelMatch?.[2]) {
    return { kind: 'panel', projectId: panelMatch[1], panelId: panelMatch[2] };
  }

  const detailMatch = window.location.pathname.match(/^\/projects\/([^/]+)$/);
  if (detailMatch?.[1]) {
    const section = new URLSearchParams(window.location.search).get('section');
    return {
      kind: 'detail',
      projectId: detailMatch[1],
      section: section === 'procurement' ? 'procurement' : 'panels'
    };
  }

  return { kind: 'list' };
}

function pathForView(view: View) {
  switch (view.kind) {
    case 'detail':
      return `/projects/${view.projectId}${view.section === 'procurement' ? '?section=procurement' : ''}`;
    case 'panel-info-edit':
      return `/projects/${view.projectId}/panel-information/edit`;
    case 'procurement-edit':
      return `/projects/${view.projectId}/procurement/edit`;
    case 'materials-receipts':
      return '/materials/receipts';
    case 'procurement-dashboard':
      return '/procurement';
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
    if (`${window.location.pathname}${window.location.search}` !== nextPath) {
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
  const canPurgeDeletedProjects = canReadAuditAll;
  const canUpdateProcurement = permissions.includes('ProcurementPlan.Update');
  const canUpdateMaterialReceipt = permissions.includes('MaterialReceipt.Update');
  const navigationItems: NavigationItem[] = [
    { label: '프로젝트', view: { kind: 'list' }, active: isProjectWorkspace(view) },
    { label: '구매', view: { kind: 'procurement-dashboard' }, active: isProcurementWorkspace(view) }
  ];

  return (
    <main className="app-shell">
      <AppNavigation items={navigationItems} onNavigate={setView} />

      <div className="app-content">
        <header className="topbar">
          <div>
            <p className="eyebrow">EMI</p>
            <h1>프로젝트·제품 패널 관리</h1>
          </div>
          <div className="topbar-actions">
            {canUpdateMaterialReceipt ? <button type="button" onClick={() => setView({ kind: 'materials-receipts' })}>자재 입고 입력</button> : null}
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
          </div>
        </header>

        <AppMobileNavigation items={navigationItems} onNavigate={setView} />

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
            canPurgeDeletedProjects={canPurgeDeletedProjects}
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
          canUpdateProcurement={canUpdateProcurement}
          initialSection={view.section ?? 'panels'}
          onBack={() => setView({ kind: 'list' })}
          onEdit={() => setView({ kind: 'edit', projectId: view.projectId })}
          onEditPanelInformation={() => setView({ kind: 'panel-info-edit', projectId: view.projectId })}
          onEditProcurement={() => setView({ kind: 'procurement-edit', projectId: view.projectId })}
          onOpenPanel={(panelId) => setView({ kind: 'panel', projectId: view.projectId, panelId })}
        />
      ) : null}

      {view.kind === 'deleted-detail' ? (
        <DeletedProjectDetailPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canReadSalesAmount={canReadSalesAmount}
          canPurgeDeletedProjects={canPurgeDeletedProjects}
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

      {view.kind === 'procurement-edit' ? (
        <ProcurementEditPage
          developmentUserKey={developmentUserKey}
          projectId={view.projectId}
          canUpdateProcurement={canUpdateProcurement}
          onBack={() => setView({ kind: 'detail', projectId: view.projectId, section: 'procurement' })}
        />
      ) : null}

      {view.kind === 'procurement-dashboard' ? (
        <ProcurementDashboardPage
          developmentUserKey={developmentUserKey}
          canUpdateProcurement={canUpdateProcurement}
          onBack={() => setView({ kind: 'list' })}
          onOpenProject={(projectId) => setView({ kind: 'detail', projectId, section: 'procurement' })}
          onEditProject={(projectId) => setView({ kind: 'procurement-edit', projectId })}
        />
      ) : null}

      {view.kind === 'materials-receipts' ? (
        <MaterialReceiptsPage
          developmentUserKey={developmentUserKey}
          canUpdateMaterialReceipt={canUpdateMaterialReceipt}
          onBack={() => setView({ kind: 'list' })}
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
      </div>
    </main>
  );
}

type NavigationItem = {
  label: string;
  view: View;
  active: boolean;
};

function AppNavigation({ items, onNavigate }: { items: NavigationItem[]; onNavigate: (view: View) => void }) {
  return (
    <aside className="app-sidebar" role="navigation" aria-label="공통 메뉴">
      <div>
        <p className="eyebrow">업무 메뉴</p>
        <strong>업무 메뉴</strong>
      </div>
      <div className="app-nav">
        {items.map((item) => (
          <button
            key={item.label}
            type="button"
            className={item.active ? 'app-nav-button active' : 'app-nav-button'}
            aria-current={item.active ? 'page' : undefined}
            onClick={() => onNavigate(item.view)}
          >
            {item.label}
          </button>
        ))}
      </div>
    </aside>
  );
}

function AppMobileNavigation({ items, onNavigate }: { items: NavigationItem[]; onNavigate: (view: View) => void }) {
  return (
    <nav className="app-mobile-nav" aria-label="공통 메뉴">
      {items.map((item) => (
        <button
          key={item.label}
          type="button"
          className={item.active ? 'app-nav-button active' : 'app-nav-button'}
          aria-current={item.active ? 'page' : undefined}
          onClick={() => onNavigate(item.view)}
        >
          {item.label}
        </button>
      ))}
    </nav>
  );
}

function isProjectWorkspace(view: View) {
  return view.kind === 'list'
    || view.kind === 'create'
    || view.kind === 'detail'
    || view.kind === 'deleted-detail'
    || view.kind === 'edit'
    || view.kind === 'panel-info-edit'
    || view.kind === 'panel';
}

function isProcurementWorkspace(view: View) {
  return view.kind === 'procurement-dashboard'
    || view.kind === 'procurement-edit';
}

function ProjectListPage({
  developmentUserKey,
  canCreate,
  canReadDeleted,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  onCreate,
  onOpen,
  onOpenDeleted
}: {
  developmentUserKey: string;
  canCreate: boolean;
  canReadDeleted: boolean;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  onCreate: () => void;
  onOpen: (projectId: string) => void;
  onOpenDeleted: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [tab, setTab] = useState<ProjectListTab>('All');
  const [state, setState] = useState<LoadState<Array<ProjectListItem | DeletedProjectListItem>>>({ kind: 'loading' });
  const [summaryState, setSummaryState] = useState<LoadState<ProjectDashboardSummary>>({ kind: 'loading' });
  const [showProjectExcel, setShowProjectExcel] = useState(false);
  const [projectExcelMessage, setProjectExcelMessage] = useState('');
  const [purgeMessage, setPurgeMessage] = useState('');
  const [purgeAllConfirmText, setPurgeAllConfirmText] = useState('');
  const [isPurgingAll, setIsPurgingAll] = useState(false);
  const [isDownloadingProjectTemplate, setIsDownloadingProjectTemplate] = useState(false);
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
      : listProjects(developmentUserKey, search, tab, {
        signal: controller.signal,
        deliveryDateFrom: dateFrom,
        deliveryDateTo: dateTo
      });

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
  }, [dateFrom, dateTo, developmentUserKey, search, tab]);

  useEffect(() => {
    load();
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [load]);

  useEffect(() => {
    const controller = new AbortController();
    queueMicrotask(() => setSummaryState({ kind: 'loading' }));
    getProjectSummary(developmentUserKey, { signal: controller.signal })
      .then((summary) => setSummaryState({ kind: 'ready', data: summary }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted && !isAbortError(error)) {
          setSummaryState(toLoadError(error, '프로젝트 요약을 불러올 수 없습니다.'));
        }
      });

    return () => controller.abort();
  }, [developmentUserKey]);

  async function downloadProjectTemplate() {
    setIsDownloadingProjectTemplate(true);
    setProjectExcelMessage('');
    try {
      const template = await downloadProjectExcelTemplate(developmentUserKey);
      const url = URL.createObjectURL(template.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = template.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      setProjectExcelMessage('프로젝트 Excel 양식을 다운로드했습니다.');
    } catch (error) {
      handleFormError(error, () => undefined, setProjectExcelMessage);
    } finally {
      setIsDownloadingProjectTemplate(false);
    }
  }

  async function purgeAllDeleted() {
    setPurgeMessage('');
    if (purgeAllConfirmText !== '삭제 보관함 비우기') {
      setPurgeMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurgingAll(true);
    try {
      const result = await purgeAllDeletedProjects(developmentUserKey, purgeAllConfirmText);
      setPurgeAllConfirmText('');
      setPurgeMessage(result.deletedProjectCount === 0 ? '비울 삭제 프로젝트가 없습니다.' : `삭제 프로젝트 ${result.deletedProjectCount}건을 완전히 삭제했습니다.`);
      load();
    } catch (error) {
      handleFormError(error, () => undefined, setPurgeMessage);
    } finally {
      setIsPurgingAll(false);
    }
  }

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Projects</p>
          <h2>프로젝트 목록</h2>
        </div>
        {canCreate ? (
          <div className="button-row">
            <button type="button" onClick={downloadProjectTemplate} disabled={isDownloadingProjectTemplate}>
              {isDownloadingProjectTemplate ? '다운로드 중' : '프로젝트 Excel 양식'}
            </button>
            <button type="button" onClick={() => setShowProjectExcel(true)}>프로젝트 Excel 업로드</button>
            <button type="button" className="primary-button" onClick={onCreate}>신규 프로젝트</button>
          </div>
        ) : null}
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
        <label className="date-filter-field">
          <span>시작일</span>
          <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
        </label>
        <label className="date-filter-field">
          <span>종료일</span>
          <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
        </label>
        <button type="button" onClick={() => { setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
        <button type="submit">검색</button>
      </form>

      {summaryState.kind === 'ready' ? <ProjectKpiGrid summary={summaryState.data} /> : null}
      {summaryState.kind !== 'ready' && summaryState.kind !== 'loading' && summaryState.kind !== 'empty' ? <StateMessage state={summaryState} /> : null}

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

      {tab === 'Deleted' && canPurgeDeletedProjects ? (
        <section className="danger-zone" aria-label="삭제 보관함 비우기">
          <div>
            <strong>삭제 보관함 비우기</strong>
            <p className="muted-text">삭제 보관함의 모든 프로젝트와 관련 데이터를 완전히 삭제합니다. 되돌릴 수 없습니다.</p>
          </div>
          <label className="form-field compact-field">
            <span>확인 문구: 삭제 보관함 비우기</span>
            <input value={purgeAllConfirmText} onChange={(event) => setPurgeAllConfirmText(event.target.value)} />
          </label>
          <button type="button" className="danger-button" disabled={isPurgingAll || purgeAllConfirmText !== '삭제 보관함 비우기'} onClick={purgeAllDeleted}>
            {isPurgingAll ? '삭제 중' : '삭제 보관함 비우기'}
          </button>
        </section>
      ) : null}

      {state.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">등록된 프로젝트가 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      {state.kind === 'ready' ? (
        <ProjectListView
          projects={state.data}
          canReadSalesAmount={canReadSalesAmount}
          canPurgeDeletedProjects={canPurgeDeletedProjects && tab === 'Deleted'}
          developmentUserKey={developmentUserKey}
          onPurged={load}
          onOpen={(projectId) => tab === 'Deleted' ? onOpenDeleted(projectId) : onOpen(projectId)}
        />
      ) : null}
      {projectExcelMessage ? <p role="alert" className={successMessage(projectExcelMessage) ? 'success-text' : 'error-text'}>{projectExcelMessage}</p> : null}
      {purgeMessage ? <p role="alert" className={successMessage(purgeMessage) ? 'success-text' : 'error-text'}>{purgeMessage}</p> : null}
      {showProjectExcel ? (
        <ProjectExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowProjectExcel(false)}
          onApplied={() => {
            setShowProjectExcel(false);
            load();
            setProjectExcelMessage('프로젝트 Excel을 저장했습니다.');
          }}
        />
      ) : null}
    </section>
  );
}

function ProjectListView({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
  onOpen: (projectId: string) => void;
}) {
  const isMobile = useIsMobileViewport();

  return (
    <div className="project-list">
      {isMobile
        ? <ProjectListMobile projects={projects} canReadSalesAmount={canReadSalesAmount} canPurgeDeletedProjects={canPurgeDeletedProjects} developmentUserKey={developmentUserKey} onPurged={onPurged} onOpen={onOpen} />
        : <ProjectListDesktop projects={projects} canReadSalesAmount={canReadSalesAmount} canPurgeDeletedProjects={canPurgeDeletedProjects} developmentUserKey={developmentUserKey} onPurged={onPurged} onOpen={onOpen} />}
    </div>
  );
}

function ProjectExcelDialog({
  developmentUserKey,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  onClose: () => void;
  onApplied: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProjectExcelPreviewResponse | null>(null);
  const [message, setMessage] = useState('');
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const isMobile = useIsMobileViewport();

  async function runPreview() {
    if (!file) {
      setMessage('선택한 파일이 없습니다.');
      return;
    }

    setIsPreviewing(true);
    setMessage('');
    try {
      setPreview(await previewProjectExcel(developmentUserKey, file));
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function apply() {
    if (!file || !preview || !canApplyProjectExcel(preview, file, isApplying)) {
      return;
    }

    setIsApplying(true);
    setMessage('');
    try {
      await applyProjectExcel(developmentUserKey, file, preview.fileSha256);
      onApplied();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const disabledReason = projectExcelApplyDisabledReason(preview, file, isApplying);
  const canApply = !disabledReason;

  return (
    <DialogBackdrop ariaLabel="프로젝트 Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div className="dialog wide-dialog">
        <div className="subsection-header">
          <h3>프로젝트 Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <input type="file" accept=".xlsx" onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
            setMessage('');
          }} />
          <button type="button" disabled={isPreviewing} onClick={runPreview}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>

        {preview ? (
          <>
            <div className="excel-preview-action-bar">
              <div className="excel-preview-counts">
                <span>신규 {preview.newCount}건</span>
                <span className={preview.needsReviewCount > 0 ? 'negative-text' : undefined}>확인 {preview.needsReviewCount}건</span>
                <span className={preview.errorCount > 0 ? 'negative-text' : undefined}>오류 {preview.errorCount}건</span>
              </div>
              <button type="button" className="primary-button" disabled={!canApply} onClick={apply}>
                {isApplying ? '저장 중' : 'Excel 저장'}
              </button>
              {disabledReason ? <p className="warning-text">{disabledReason}</p> : null}
            </div>
            <ExcelIssueSummary rows={preview.rows} />
            {isMobile ? <ProjectExcelPreviewMobile rows={preview.rows} /> : <ProjectExcelPreviewDesktop rows={preview.rows} />}
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </DialogBackdrop>
  );
}

function DialogBackdrop({
  ariaLabel,
  closeDisabled = false,
  onClose,
  children
}: {
  ariaLabel: string;
  closeDisabled?: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div
      className="dialog-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label={ariaLabel}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !closeDisabled) {
          onClose();
        }
      }}
    >
      {children}
    </div>
  );
}

function ProjectExcelPreviewDesktop({ rows }: { rows: ProjectExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-table project-excel-preview excel-preview-desktop">
      {rows.map((row) => (
        <div className="excel-preview-row project-excel-preview-row" key={`${row.excelRowNumber}-${row.projectTitle ?? 'row'}`}>
          <strong>Row {row.excelRowNumber || '-'}</strong>
          <span>{emptyDash(row.customerName)}</span>
          <span>{emptyDash(row.projectCode)}</span>
          <span>{emptyDash(row.projectTitle)}</span>
          <span>{row.panelCount ?? '-'}</span>
          <span>{emptyDash(row.deliveryDate)}</span>
          <span>{formatPackagingMethod(row.packagingMethod)}</span>
          <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{projectExcelResultLabel(row.resultType)}</span>
          <small>{row.errorMessages.join(' ')}</small>
        </div>
      ))}
    </div>
  );
}

type ExcelIssueRow = {
  excelRowNumber: number;
  resultType: string;
  errorMessages: readonly string[];
  sourceProjectText?: string | null;
  sourceProjectCodeText?: string | null;
  projectTitle?: string | null;
  projectCode?: string | null;
  panelName?: string | null;
  no?: number | null;
  orderItem?: string | null;
  orderDate?: string | null;
  expectedReceiptDate?: string | null;
  packagingMethod?: string | null;
  salesOwnerText?: string | null;
  salesOwnerName?: string | null;
};

function ExcelIssueSummary({ rows }: { rows: ExcelIssueRow[] }) {
  const [filter, setFilter] = useState<'All' | 'Error' | 'NeedsReview'>('All');
  const issueRows = rows.filter((row) => row.resultType === 'Error' || row.resultType === 'NeedsReview' || row.errorMessages.length > 0);
  if (issueRows.length === 0) {
    return null;
  }

  const errorCount = issueRows.filter((row) => row.resultType === 'Error').length;
  const reviewCount = issueRows.filter((row) => row.resultType === 'NeedsReview').length;
  const visibleRows = issueRows.filter((row) => filter === 'All' || row.resultType === filter);

  return (
    <section className="excel-issue-summary" aria-label="Excel 오류 요약">
      <div className="subsection-header">
        <div>
          <h4>오류 {errorCount}건 · 확인 필요 {reviewCount}건</h4>
          <p className="muted-text">저장할 수 없는 행은 아래 내용을 수정한 뒤 다시 미리보기를 실행하세요.</p>
        </div>
        <div className="button-row">
          <button type="button" className={filter === 'All' ? 'active-filter' : undefined} onClick={() => setFilter('All')}>전체 보기</button>
          <button type="button" className={filter === 'Error' ? 'active-filter' : undefined} onClick={() => setFilter('Error')}>오류만 보기</button>
          <button type="button" className={filter === 'NeedsReview' ? 'active-filter' : undefined} onClick={() => setFilter('NeedsReview')}>확인 필요만 보기</button>
        </div>
      </div>
      <div className="excel-issue-list">
        {visibleRows.map((row) => (
          <article className="excel-issue-card" key={`${row.excelRowNumber}-${row.resultType}-${issueEntityText(row)}`}>
            <strong>{row.excelRowNumber || '-'}행</strong>
            <span>대상: {issueEntityText(row)}</span>
            <span>필드: {issueFieldText(row)}</span>
            <span>입력값: {issueInputText(row)}</span>
            <span>문제: {issueProblemText(row)}</span>
            <span>해결: {issueSolutionText(row)}</span>
          </article>
        ))}
      </div>
    </section>
  );
}

function issueEntityText(row: ExcelIssueRow) {
  if (row.sourceProjectText || row.sourceProjectCodeText) {
    return `${row.sourceProjectText ?? '-'}${row.sourceProjectCodeText ? ` / ${row.sourceProjectCodeText}` : ''}`;
  }

  if (row.projectTitle || row.projectCode) {
    return `${row.projectTitle ?? '-'}${row.projectCode ? ` / ${row.projectCode}` : ''}`;
  }

  if (row.no) {
    return `No.${row.no}${row.panelName ? ` / ${row.panelName}` : ''}`;
  }

  return row.panelName ?? '-';
}

function issueProblemText(row: ExcelIssueRow) {
  if (row.errorMessages.length > 0) {
    return row.errorMessages.join(' ');
  }

  if (row.resultType === 'NeedsReview') {
    return '확인할 항목이 있습니다.';
  }

  return '입력값을 확인해 주세요.';
}

function issueFieldText(row: ExcelIssueRow) {
  const problem = issueProblemText(row);
  if (problem.includes('프로젝트')) {
    return row.sourceProjectCodeText ? 'PJT Code' : '프로젝트명';
  }

  if (problem.includes('입고일') || problem.includes('입고예정일')) {
    return '입고일';
  }

  if (problem.includes('발주일')) {
    return '발주일';
  }

  if (problem.includes('포장방식')) {
    return '포장방식';
  }

  if (problem.includes('영업담당자')) {
    return '영업담당자';
  }

  return '행 전체';
}

function issueInputText(row: ExcelIssueRow) {
  const field = issueFieldText(row);
  if (field === 'PJT Code') {
    return row.sourceProjectCodeText ?? row.projectCode ?? '-';
  }

  if (field === '프로젝트명') {
    return row.sourceProjectText ?? row.projectTitle ?? '-';
  }

  if (field === '입고일') {
    return row.expectedReceiptDate ?? '-';
  }

  if (field === '발주일') {
    return row.orderDate ?? '-';
  }

  if (field === '포장방식') {
    return row.packagingMethod ?? '-';
  }

  if (field === '영업담당자') {
    return row.salesOwnerText ?? row.salesOwnerName ?? '-';
  }

  return row.orderItem ?? row.panelName ?? '-';
}

function issueSolutionText(row: ExcelIssueRow) {
  const problem = issueProblemText(row);
  if (problem.includes('프로젝트') || row.resultType === 'NeedsReview') {
    return '등록된 프로젝트를 선택하거나 Excel의 프로젝트명을 확인해 주세요.';
  }

  if (problem.includes('날짜')) {
    return 'yyyy-mm-dd 형식으로 입력해 주세요.';
  }

  if (problem.includes('필수') || problem.includes('누락')) {
    return '필수 입력값을 채운 뒤 다시 미리보기를 실행해 주세요.';
  }

  return '행의 값을 수정한 뒤 다시 미리보기를 실행해 주세요.';
}

function ProjectExcelPreviewMobile({ rows }: { rows: ProjectExcelPreviewResponse['rows'] }) {
  return (
    <div className="excel-preview-cards excel-preview-mobile">
      {rows.map((row) => (
        <article className="excel-preview-card" key={`${row.excelRowNumber}-${row.projectTitle ?? 'row'}-mobile`}>
          <div className="subsection-header">
            <h3>Row {row.excelRowNumber || '-'}</h3>
            <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{projectExcelResultLabel(row.resultType)}</span>
          </div>
          <dl className="mobile-detail-list">
            <div><dt>고객사</dt><dd>{emptyDash(row.customerName)}</dd></div>
            <div><dt>Code</dt><dd>{emptyDash(row.projectCode)}</dd></div>
            <div><dt>PJT Title</dt><dd>{emptyDash(row.projectTitle)}</dd></div>
            <div><dt>면수</dt><dd>{row.panelCount ?? '-'}</dd></div>
            <div><dt>납기일</dt><dd>{emptyDash(row.deliveryDate)}</dd></div>
            <div><dt>포장방식</dt><dd>{formatPackagingMethod(row.packagingMethod)}</dd></div>
            <div><dt>영업담당자</dt><dd>{emptyDash(row.salesOwnerName ?? row.salesOwnerText)}</dd></div>
            <div><dt>오류</dt><dd>{row.errorMessages.join(' ') || '-'}</dd></div>
          </dl>
        </article>
      ))}
    </div>
  );
}

function ProjectKpiGrid({ summary }: { summary: ProjectDashboardSummary }) {
  return (
    <div className="dashboard-kpi-grid project-kpi-grid" aria-label="프로젝트 요약">
      <DashboardKpiCard title="전체 프로젝트" value={summary.totalProjectCount} helperText="완료·삭제 제외" />
      <DashboardKpiCard title="진행" value={summary.activeProjectCount} helperText="진행 프로젝트" variant="positive" />
      <DashboardKpiCard title="보류" value={summary.onHoldProjectCount} helperText="보류 프로젝트" variant="warning" />
      <DashboardKpiCard title="취소 프로젝트" value={summary.cancelledProjectCount} helperText="취소 프로젝트" />
      <DashboardKpiCard title="제조 완료 프로젝트" value={summary.manufacturingCompletedProjectCount} helperText="모든 제품 제조 완료" />
      <DashboardKpiCard title="검사 완료 프로젝트" value={summary.inspectionCompletedProjectCount} helperText="모든 제품 검사 완료" />
    </div>
  );
}

function ProjectListDesktop({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
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
        <Fragment key={project.projectId}>
          <button
            className="project-list-row"
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
          {canPurgeDeletedProjects && 'deletedAtUtc' in project ? (
            <div className="deleted-project-actions">
              <DeletedProjectRestoreControl projectId={project.projectId} developmentUserKey={developmentUserKey} onRestored={onPurged} />
              <DeletedProjectPurgeControl projectId={project.projectId} developmentUserKey={developmentUserKey} onPurged={onPurged} />
            </div>
          ) : null}
        </Fragment>
      ))}
    </div>
  );
}

function ProjectListMobile({
  projects,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  developmentUserKey,
  onPurged,
  onOpen
}: {
  projects: Array<ProjectListItem | DeletedProjectListItem>;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  developmentUserKey: string;
  onPurged: () => void;
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
          {canPurgeDeletedProjects && 'deletedAtUtc' in project ? (
            <div className="deleted-project-actions">
              <DeletedProjectRestoreControl projectId={project.projectId} developmentUserKey={developmentUserKey} onRestored={onPurged} />
              <DeletedProjectPurgeControl projectId={project.projectId} developmentUserKey={developmentUserKey} onPurged={onPurged} />
            </div>
          ) : null}
        </article>
      ))}
    </div>
  );
}

function DeletedProjectRestoreControl({
  projectId,
  developmentUserKey,
  onRestored
}: {
  projectId: string;
  developmentUserKey: string;
  onRestored: () => void;
}) {
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [isRestoring, setIsRestoring] = useState(false);

  async function restore() {
    setMessage('');
    setIsRestoring(true);
    try {
      await restoreDeletedProject(developmentUserKey, projectId, reason.trim() || null);
      setReason('');
      setMessage('프로젝트를 복구했습니다.');
      onRestored();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsRestoring(false);
    }
  }

  return (
    <div className="restore-inline-control">
      <label className="form-field compact-field">
        <span>복구 사유</span>
        <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="선택 입력" />
      </label>
      <button type="button" className="primary-button" disabled={isRestoring} onClick={restore}>
        {isRestoring ? '복구 중' : '복구'}
      </button>
      {message ? <span role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</span> : null}
    </div>
  );
}

function DeletedProjectPurgeControl({
  projectId,
  developmentUserKey,
  onPurged
}: {
  projectId: string;
  developmentUserKey: string;
  onPurged: () => void;
}) {
  const [confirmText, setConfirmText] = useState('');
  const [message, setMessage] = useState('');
  const [isPurging, setIsPurging] = useState(false);

  async function purge() {
    setMessage('');
    if (confirmText !== '완전 삭제') {
      setMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurging(true);
    try {
      await purgeDeletedProject(developmentUserKey, projectId, confirmText);
      setMessage('프로젝트를 완전히 삭제했습니다.');
      onPurged();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPurging(false);
    }
  }

  return (
    <div className="purge-inline-control">
      <label className="form-field compact-field">
        <span>확인 문구: 완전 삭제</span>
        <input value={confirmText} onChange={(event) => setConfirmText(event.target.value)} />
      </label>
      <button type="button" className="danger-button" disabled={isPurging || confirmText !== '완전 삭제'} onClick={purge}>
        {isPurging ? '삭제 중' : '완전 삭제'}
      </button>
      {message ? <span role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</span> : null}
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
      .catch((error: unknown) => setMessage(friendlyErrorMessage(error, '영업담당자를 불러올 수 없습니다.')));
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
  canUpdateProcurement,
  initialSection,
  onBack,
  onEdit,
  onEditPanelInformation,
  onEditProcurement,
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
  canUpdateProcurement: boolean;
  initialSection: 'panels' | 'procurement';
  onBack: () => void;
  onEdit: () => void;
  onEditPanelInformation: () => void;
  onEditProcurement: () => void;
  onOpenPanel: (panelId: string) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [panelInfoState, setPanelInfoState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [procurementState, setProcurementState] = useState<LoadState<ProcurementResponse>>({ kind: 'loading' });
  const [historyState, setHistoryState] = useState<LoadState<PanelInformationHistoryResponse>>({ kind: 'empty' });
  const [procurementHistoryState, setProcurementHistoryState] = useState<LoadState<ProcurementHistoryResponse>>({ kind: 'empty' });
  const [activeDetailSection, setActiveDetailSection] = useState<'panels' | 'procurement'>(initialSection);
  const [dialog, setDialog] = useState<null | 'hold' | 'resume' | 'cancel' | 'reactivate' | 'delete'>(null);
  const [reason, setReason] = useState('');
  const [confirmProjectTitle, setConfirmProjectTitle] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(() => {
    setProjectState({ kind: 'loading' });
    setPanelInfoState({ kind: 'loading' });
    setProcurementState({ kind: 'loading' });
    setHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });
    setProcurementHistoryState(canReadAuditAll ? { kind: 'loading' } : { kind: 'empty' });

    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanelInformation(developmentUserKey, projectId),
      getProjectProcurement(developmentUserKey, projectId).catch(() => null),
      canReadAuditAll ? getPanelInformationHistory(developmentUserKey, projectId) : Promise.resolve(null),
      canReadAuditAll ? getProjectProcurementHistory(developmentUserKey, projectId) : Promise.resolve(null)
    ])
      .then(([project, panelInfo, procurement, history, procurementHistory]) => {
        setProjectState({ kind: 'ready', data: project });
        setPanelInfoState({ kind: 'ready', data: panelInfo });
        setProcurementState(procurement ? { kind: 'ready', data: procurement } : { kind: 'empty' });
        setHistoryState(history ? { kind: 'ready', data: history } : { kind: 'empty' });
        setProcurementHistoryState(procurementHistory ? { kind: 'ready', data: procurementHistory } : { kind: 'empty' });
      })
      .catch((error: unknown) => {
        const state = toLoadError<ProjectDetail>(error, '프로젝트 상세를 불러올 수 없습니다.');
        setProjectState(state);
        setPanelInfoState(toLoadError(error, '제품·패널 목록을 불러올 수 없습니다.'));
        setProcurementState(toLoadError(error, '구매정보를 불러올 수 없습니다.'));
        setHistoryState(toLoadError(error, '전체 이력을 불러올 수 없습니다.'));
        setProcurementHistoryState(toLoadError(error, '전체 이력을 불러올 수 없습니다.'));
      });
  }, [canReadAuditAll, developmentUserKey, projectId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    setActiveDetailSection(initialSection);
  }, [initialSection, projectId]);

  function selectDetailSection(section: 'panels' | 'procurement') {
    setActiveDetailSection(section);
    if (typeof window !== 'undefined') {
      const nextPath = `/projects/${projectId}${section === 'procurement' ? '?section=procurement' : ''}`;
      if (`${window.location.pathname}${window.location.search}` !== nextPath) {
        window.history.replaceState(null, '', nextPath);
      }
    }
  }

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

      <div className="section-switcher" role="tablist" aria-label="프로젝트 상세 섹션">
        <button
          type="button"
          role="tab"
          aria-selected={activeDetailSection === 'panels'}
          className={activeDetailSection === 'panels' ? 'secondary-button active' : 'secondary-button'}
          onClick={() => selectDetailSection('panels')}
        >
          제품 목록
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeDetailSection === 'procurement'}
          className={activeDetailSection === 'procurement' ? 'secondary-button active' : 'secondary-button'}
          onClick={() => selectDetailSection('procurement')}
        >
          구매
        </button>
      </div>

      {activeDetailSection === 'panels' ? (
        <PanelInformationSection
          project={project}
          state={panelInfoState}
          canUpdatePanelInfo={canUpdatePanelInfo}
          onEdit={onEditPanelInformation}
          onOpenPanel={onOpenPanel}
        />
      ) : null}

      {activeDetailSection === 'procurement' ? (
        <ProcurementSection
          state={procurementState}
          canUpdateProcurement={canUpdateProcurement && project.status === 'Active'}
          onEdit={onEditProcurement}
        />
      ) : null}

      {canReadAuditAll ? (
        <section className="subsection">
          <h3>전체 이력</h3>
          {historyState.kind === 'loading' || procurementHistoryState.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
          {historyState.kind !== 'ready' && historyState.kind !== 'loading' && historyState.kind !== 'empty' ? <StateMessage state={historyState} /> : null}
          {procurementHistoryState.kind !== 'ready' && procurementHistoryState.kind !== 'loading' && procurementHistoryState.kind !== 'empty' ? <StateMessage state={procurementHistoryState} /> : null}
          {historyState.kind === 'ready' || procurementHistoryState.kind === 'ready' ? (
            <div className="history-stack">
              {historyState.kind === 'ready' ? <GroupedHistory groups={historyState.data.groups} emptyText={procurementHistoryState.kind === 'ready' && procurementHistoryState.data.groups.length > 0 ? null : '전체 이력이 없습니다.'} /> : null}
              {procurementHistoryState.kind === 'ready' ? <ProcurementGroupedHistory groups={procurementHistoryState.data.groups} /> : null}
            </div>
          ) : null}
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

      {state.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
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

type ProcurementRowForm = {
  itemId: string | null;
  rowVersion: number | null;
  sourceProjectText: string;
  sourceProjectCodeText: string;
  standardLeadTime: string;
  orderItem: string;
  technicalOwner: string;
  orderDate: string;
  expectedReceiptDate: string;
  shipmentDisplayDate: string | null;
  issueNote: string;
  receiptCompleted: boolean;
  receiptCompletedAtUtc: string;
  receiptCompletionNote: string;
  dDayText: string;
};

function ProcurementDashboardPage({
  developmentUserKey,
  canUpdateProcurement,
  onBack,
  onOpenProject,
  onEditProject
}: {
  developmentUserKey: string;
  canUpdateProcurement: boolean;
  onBack: () => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [state, setState] = useState<LoadState<ProcurementDashboardResponse>>({ kind: 'loading' });
  const [expandedProjectId, setExpandedProjectId] = useState<string | null>(null);
  const [expandedProcurementState, setExpandedProcurementState] = useState<LoadState<ProcurementResponse>>({ kind: 'empty' });
  const [showExcel, setShowExcel] = useState(false);
  const procurementRequestIdRef = useRef(0);
  const isMobile = useIsMobileViewport();

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getProcurementDashboard(developmentUserKey, search, dateFrom, dateTo)
      .then((response) => {
        setState({ kind: 'ready', data: response });
        setExpandedProjectId((current) => current && response.projects.some((project) => project.projectId === current)
          ? current
          : null);
      })
      .catch((error: unknown) => setState(toLoadError(error, '구매 목록을 불러올 수 없습니다.')));
  }, [dateFrom, dateTo, developmentUserKey, search]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  useEffect(() => {
    const requestId = procurementRequestIdRef.current + 1;
    procurementRequestIdRef.current = requestId;
    if (!expandedProjectId) {
      queueMicrotask(() => setExpandedProcurementState({ kind: 'empty' }));
      return;
    }

    queueMicrotask(() => setExpandedProcurementState({ kind: 'loading' }));
    getProjectProcurement(developmentUserKey, expandedProjectId)
      .then((response) => {
        if (requestId === procurementRequestIdRef.current) {
          setExpandedProcurementState({ kind: 'ready', data: response });
        }
      })
      .catch((error: unknown) => {
        if (requestId === procurementRequestIdRef.current) {
          setExpandedProcurementState(toLoadError(error, '선택 프로젝트 구매정보를 불러올 수 없습니다.'));
        }
      });
  }, [developmentUserKey, expandedProjectId]);

  function toggleExpandedProject(projectId: string) {
    setExpandedProjectId((current) => current === projectId ? null : projectId);
  }

  return (
    <section className="page-surface procurement-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Procurement</p>
          <h2>구매</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>프로젝트 목록</button>
          {canUpdateProcurement ? <button type="button" className="primary-button" onClick={() => setShowExcel(true)}>Excel 업로드</button> : null}
        </div>
      </div>

      {state.kind === 'ready' ? <DashboardKpiGrid summary={state.data.summary} /> : null}
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}

      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); load(); }}>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트명, 고객사, Code, 발주품목 검색" />
        <label className="date-filter-field">
          <span>입고예정 시작일</span>
          <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
        </label>
        <label className="date-filter-field">
          <span>입고예정 종료일</span>
          <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
        </label>
        <button type="button" onClick={() => { setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
        <button type="submit">검색</button>
      </form>

      {state.kind === 'ready' && state.data.projects.length === 0 ? <p className="empty-text">표시할 구매 프로젝트가 없습니다.</p> : null}
      {state.kind === 'ready' && state.data.projects.length > 0 ? (
        <div className="procurement-dashboard-layout">
          {isMobile ? (
            <ProcurementProjectCards
              projects={state.data.projects}
              expandedProjectId={expandedProjectId}
              expandedState={expandedProcurementState}
              canUpdateProcurement={canUpdateProcurement}
              onSelect={toggleExpandedProject}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          ) : (
            <ProcurementProjectTable
              projects={state.data.projects}
              expandedProjectId={expandedProjectId}
              expandedState={expandedProcurementState}
              canUpdateProcurement={canUpdateProcurement}
              onSelect={toggleExpandedProject}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          )}
        </div>
      ) : null}

      {showExcel ? (
        <ProcurementExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowExcel(false)}
          onApplied={() => {
            setShowExcel(false);
            load();
            if (expandedProjectId) {
              getProjectProcurement(developmentUserKey, expandedProjectId)
                .then((response) => setExpandedProcurementState({ kind: 'ready', data: response }))
                .catch((error: unknown) => setExpandedProcurementState(toLoadError(error, '선택 프로젝트 구매정보를 불러올 수 없습니다.')));
            }
          }}
        />
      ) : null}
    </section>
  );
}

function DashboardKpiGrid({ summary }: { summary: ProcurementDashboardResponse['summary'] }) {
  return (
    <div className="dashboard-kpi-grid" aria-label="구매 요약">
      <DashboardKpiCard title="입고대기품목" value={summary.pendingReceiptCount} helperText="완료 체크되지 않은 구매품목" variant="warning" />
      <DashboardKpiCard title="입고완료품목" value={summary.receiptCompletedCount} helperText="완료 체크된 구매품목" variant="positive" />
      <DashboardKpiCard title="입고예정일 경과 품목" value={summary.pastExpectedReceiptDateCount} helperText="상태가 아닌 날짜 참고값" variant="warning" />
    </div>
  );
}

function DashboardKpiCard({
  title,
  value,
  helperText,
  variant = 'neutral'
}: {
  title: string;
  value: string | number;
  helperText: string;
  variant?: 'neutral' | 'positive' | 'warning';
}) {
  return (
    <article className="dashboard-kpi-card" data-variant={variant}>
      <span>{title}</span>
      <strong>{value}</strong>
      <small>{helperText}</small>
    </article>
  );
}

function ProcurementProjectTable({
  projects,
  expandedProjectId,
  expandedState,
  canUpdateProcurement,
  onSelect,
  onOpenProject,
  onEditProject
}: {
  projects: ProcurementProjectSummary[];
  expandedProjectId: string | null;
  expandedState: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onSelect: (projectId: string) => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  return (
    <div className="procurement-project-table procurement-desktop" role="table" aria-label="구매 프로젝트 목록">
      <div className="procurement-project-head" role="row">
        <span>프로젝트명</span><span>Code</span><span>Item</span><span>면수</span><span>납기일</span><span>구매품목</span><span>입고완료</span>
      </div>
      {projects.map((project) => (
        <Fragment key={project.projectId}>
          <button
            type="button"
            role="row"
            className={project.projectId === expandedProjectId ? 'procurement-project-row active' : 'procurement-project-row'}
            aria-expanded={project.projectId === expandedProjectId}
            onClick={() => onSelect(project.projectId)}
            onDoubleClick={() => onOpenProject(project.projectId)}
          >
            <span>{project.projectTitle}</span>
            <span>{project.projectCode}</span>
            <span>{project.item}</span>
            <span>{project.activePanelCount}면</span>
            <span>{emptyDash(project.deliveryDate)}</span>
            <span>{project.procurementItemCount}건</span>
            <span>{project.receiptCompletedCount}건</span>
          </button>
          {project.projectId === expandedProjectId ? (
            <ProcurementProjectExpanded
              project={project}
              state={expandedState}
              canUpdateProcurement={canUpdateProcurement}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          ) : null}
        </Fragment>
      ))}
    </div>
  );
}

function ProcurementProjectCards({
  projects,
  expandedProjectId,
  expandedState,
  canUpdateProcurement,
  onSelect,
  onOpenProject,
  onEditProject
}: {
  projects: ProcurementProjectSummary[];
  expandedProjectId: string | null;
  expandedState: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onSelect: (projectId: string) => void;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  return (
    <div className="procurement-project-cards procurement-mobile" data-testid="procurement-dashboard-mobile">
      {projects.map((project) => (
        <article key={project.projectId} className={project.projectId === expandedProjectId ? 'procurement-project-card active' : 'procurement-project-card'}>
          <div className="subsection-header">
            <h3>{project.projectTitle}</h3>
            <button type="button" onClick={() => onOpenProject(project.projectId)}>상세 보기</button>
          </div>
          <dl className="mobile-detail-list">
            <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
            <div><dt>Code</dt><dd>{project.projectCode}</dd></div>
            <div><dt>Item</dt><dd>{project.item}</dd></div>
            <div><dt>구매품목</dt><dd>{project.procurementItemCount}건</dd></div>
            <div><dt>입고완료</dt><dd>{project.receiptCompletedCount}건</dd></div>
            <div><dt>최근 입고예정일</dt><dd>{emptyDash(project.nearestExpectedReceiptDate)}</dd></div>
            <div><dt>예정일까지</dt><dd>{project.dDayText}</dd></div>
          </dl>
          <button
            type="button"
            className="secondary-button"
            aria-expanded={project.projectId === expandedProjectId}
            onClick={() => onSelect(project.projectId)}
          >
            {project.projectId === expandedProjectId ? '구매정보 접기' : '구매정보 보기'}
          </button>
          {project.projectId === expandedProjectId ? (
            <ProcurementProjectExpanded
              project={project}
              state={expandedState}
              canUpdateProcurement={canUpdateProcurement}
              onOpenProject={onOpenProject}
              onEditProject={onEditProject}
            />
          ) : null}
        </article>
      ))}
    </div>
  );
}

function ProcurementProjectExpanded({
  project,
  state,
  canUpdateProcurement,
  onOpenProject,
  onEditProject
}: {
  project: ProcurementProjectSummary;
  state: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onOpenProject: (projectId: string) => void;
  onEditProject: (projectId: string) => void;
}) {
  return (
    <section className="procurement-project-expanded" aria-label={`${project.projectTitle} 구매정보`}>
      <div className="subsection-header">
        <div>
          <h3>{project.projectTitle}</h3>
          <span>선택한 프로젝트 구매정보</span>
        </div>
        <div className="button-row">
          <button type="button" onClick={() => onOpenProject(project.projectId)}>프로젝트 상세</button>
          {canUpdateProcurement ? <button type="button" className="primary-button" onClick={() => onEditProject(project.projectId)}>구매정보 수정</button> : null}
        </div>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">프로젝트를 선택하세요.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        state.data.items.length === 0
          ? <p className="empty-text">등록된 구매정보가 없습니다.</p>
          : <ProcurementReadOnlyList items={state.data.items} />
      ) : null}
    </section>
  );
}

function ProcurementSection({
  state,
  canUpdateProcurement,
  onEdit
}: {
  state: LoadState<ProcurementResponse>;
  canUpdateProcurement: boolean;
  onEdit: () => void;
}) {
  return (
    <section className="subsection procurement-section">
      <div className="subsection-header">
        <div>
          <h3>구매정보</h3>
          <span>입고예정 정보</span>
        </div>
        {canUpdateProcurement ? <button type="button" className="primary-button" onClick={onEdit}>구매정보 수정</button> : null}
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">등록된 구매정보가 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        state.data.items.length === 0
          ? <p className="empty-text">등록된 구매정보가 없습니다.</p>
          : <ProcurementReadOnlyList items={state.data.items} />
      ) : null}
    </section>
  );
}

function ProcurementReadOnlyList({ items }: { items: ProcurementItem[] }) {
  const isMobile = useIsMobileViewport();
  return isMobile
    ? <ProcurementCards items={items} editable={false} onChange={() => undefined} />
    : (
      <div className="procurement-table procurement-readonly-table procurement-desktop" role="table" aria-label="구매정보">
        <div className="procurement-table-head" role="row">
          <span>통상납기</span>
          <span>발주품목</span>
          <span>기술 담당자</span>
          <span>발주일</span>
          <span>입고예정일</span>
          <span>이슈사항</span>
          <span>입고 완료</span>
        </div>
        {items.map((item) => (
          <div className="procurement-table-row" role="row" key={item.itemId}>
            <span>{emptyDash(item.standardLeadTime)}</span>
            <span className="order-item-badge">{emptyDash(item.orderItem)}</span>
            <span>{emptyDash(item.technicalOwner)}</span>
            <span>{emptyDash(item.orderDate)}</span>
            <span>{emptyDash(item.expectedReceiptDate)}</span>
            <span>{emptyDash(item.issueNote)}</span>
            <ReceiptCompletionBadge completed={item.receiptCompleted} />
          </div>
        ))}
      </div>
    );
}

function ReceiptCompletionBadge({ completed }: { completed: boolean }) {
  return (
    <span className="receipt-completion-badge" data-completed={completed ? 'true' : 'false'}>
      {completed ? '완료' : '미완료'}
    </span>
  );
}

function ProcurementEditPage({
  developmentUserKey,
  projectId,
  canUpdateProcurement,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdateProcurement: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<ProcurementResponse>>({ kind: 'loading' });
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [rows, setRows] = useState<ProcurementRowForm[]>([]);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [showExcel, setShowExcel] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    setProjectState({ kind: 'loading' });
    Promise.all([
      getProjectProcurement(developmentUserKey, projectId),
      getProject(developmentUserKey, projectId)
    ])
      .then(([response, project]) => {
        setState({ kind: 'ready', data: response });
        setProjectState({ kind: 'ready', data: project });
        setRows(response.items.map(procurementItemToForm));
      })
      .catch((error: unknown) => {
        setState(toLoadError(error, '구매정보를 불러올 수 없습니다.'));
        setProjectState(toLoadError(error, '프로젝트 정보를 불러오지 못했습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  if (!canUpdateProcurement) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  function updateRow(index: number, next: Partial<ProcurementRowForm>) {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...next } : row));
  }

  function addRow() {
    const projectDeliveryDate = state.kind === 'ready' ? state.data.projectDeliveryDate : null;
    setRows((current) => [...current, emptyProcurementRow(projectDeliveryDate)]);
  }

  async function save() {
    setIsSaving(true);
    setMessage('');
    try {
      const response = await updateProjectProcurement(developmentUserKey, projectId, {
        reason: reason.trim() || null,
        items: rows.map(procurementFormToRequest)
      });
      setState({ kind: 'ready', data: response });
      setRows(response.items.map(procurementItemToForm));
      setReason('');
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsSaving(false);
    }
  }

  async function downloadTemplate() {
    setIsDownloading(true);
    setMessage('');
    try {
      const template = await downloadProcurementTemplate(developmentUserKey, projectId);
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
      setIsDownloading(false);
    }
  }

  return (
    <section className="page-surface procurement-section">
      {projectState.kind === 'loading' ? <p className="muted-text">프로젝트 정보를 불러오는 중입니다.</p> : null}
      {projectState.kind === 'ready' ? <ProjectContextSummary project={projectState.data} /> : null}
      {projectState.kind !== 'ready' && projectState.kind !== 'loading' ? <StateMessage state={projectState} /> : null}
      <div className="subsection-header">
        <div>
          <p className="eyebrow">Procurement</p>
          <h2>구매정보 수정</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>상세</button>
          <button type="button" onClick={addRow}>행 추가</button>
          <button type="button" onClick={downloadTemplate} disabled={isDownloading}>{isDownloading ? '다운로드 중' : 'Excel 양식 다운로드'}</button>
          <button type="button" onClick={() => setShowExcel(true)}>Excel 업로드</button>
          <button type="button" className="primary-button" disabled={isSaving} onClick={save}>{isSaving ? '저장 중' : '저장'}</button>
        </div>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <label className="form-field panel-reason-field">
            <span>수정사유</span>
            <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
          </label>
          <ProcurementEditableList rows={rows} onChange={updateRow} />
        </>
      ) : null}
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
      {showExcel ? (
        <ProcurementExcelDialog
          developmentUserKey={developmentUserKey}
          onClose={() => setShowExcel(false)}
          onApplied={() => {
            setShowExcel(false);
            onBack();
          }}
        />
      ) : null}
    </section>
  );
}

function ProcurementEditableList({
  rows,
  onChange
}: {
  rows: ProcurementRowForm[];
  onChange: (index: number, next: Partial<ProcurementRowForm>) => void;
}) {
  const isMobile = useIsMobileViewport();
  return isMobile
    ? <ProcurementCards items={rows} editable onChange={onChange} />
    : (
      <div className="procurement-table procurement-desktop" role="table" aria-label="구매정보 수정">
        <div className="procurement-table-head editable" role="row">
          <span>통상납기</span>
          <span>발주품목</span>
          <span>기술 담당자</span>
          <span>발주일</span>
          <span>입고예정일</span>
          <span>이슈사항</span>
          <span>입고 완료</span>
        </div>
        {rows.map((row, index) => (
          <div className="procurement-table-row editable" role="row" key={row.itemId ?? `new-${index}`}>
            <input value={row.standardLeadTime} onChange={(event) => onChange(index, { standardLeadTime: event.target.value })} />
            <input className="order-item-input" value={row.orderItem} onChange={(event) => onChange(index, { orderItem: event.target.value })} />
            <input value={row.technicalOwner} onChange={(event) => onChange(index, { technicalOwner: event.target.value })} />
            <input type="date" value={row.orderDate} onChange={(event) => onChange(index, { orderDate: event.target.value })} />
            <input type="date" value={row.expectedReceiptDate} onChange={(event) => onChange(index, { expectedReceiptDate: event.target.value })} />
            <input value={row.issueNote} onChange={(event) => onChange(index, { issueNote: event.target.value })} />
            <div className="receipt-input-cell">
              <label className="checkbox-field">
                <input type="checkbox" checked={row.receiptCompleted} onChange={(event) => onChange(index, { receiptCompleted: event.target.checked })} />
                입고 완료
              </label>
              <ReceiptCompletionBadge completed={row.receiptCompleted} />
            </div>
          </div>
        ))}
      </div>
    );
}

function ProcurementCards({
  items,
  editable,
  onChange
}: {
  items: ProcurementItem[] | ProcurementRowForm[];
  editable: boolean;
  onChange: (index: number, next: Partial<ProcurementRowForm>) => void;
}) {
  return (
    <div className="procurement-cards procurement-mobile" data-testid="procurement-mobile">
      {items.map((item, index) => {
        const row = isProcurementForm(item) ? item : procurementItemToForm(item);
        return (
          <article className="procurement-card" key={row.itemId ?? `new-${index}`}>
            {editable ? (
              <>
                <FormField label="발주품목"><input className="order-item-input" value={row.orderItem} onChange={(event) => onChange(index, { orderItem: event.target.value })} /></FormField>
                <FormField label="기술 담당자"><input value={row.technicalOwner} onChange={(event) => onChange(index, { technicalOwner: event.target.value })} /></FormField>
                <FormField label="통상납기"><input value={row.standardLeadTime} onChange={(event) => onChange(index, { standardLeadTime: event.target.value })} /></FormField>
                <FormField label="발주일"><input type="date" value={row.orderDate} onChange={(event) => onChange(index, { orderDate: event.target.value })} /></FormField>
                <FormField label="입고예정일"><input type="date" value={row.expectedReceiptDate} onChange={(event) => onChange(index, { expectedReceiptDate: event.target.value })} /></FormField>
                <div className="readonly-field"><span>프로젝트 출하일</span><strong>{emptyDash(row.shipmentDisplayDate)}</strong></div>
                <FormField label="이슈사항"><input value={row.issueNote} onChange={(event) => onChange(index, { issueNote: event.target.value })} /></FormField>
                <div className="receipt-input-cell">
                  <label className="checkbox-field"><input type="checkbox" checked={row.receiptCompleted} onChange={(event) => onChange(index, { receiptCompleted: event.target.checked })} /> 입고 완료</label>
                  <ReceiptCompletionBadge completed={row.receiptCompleted} />
                </div>
              </>
            ) : (
              <>
                <h3 className="order-item-badge">{emptyDash(row.orderItem)}</h3>
                <dl className="mobile-detail-list">
                  <div><dt>기술 담당자</dt><dd>{emptyDash(row.technicalOwner)}</dd></div>
                  <div><dt>통상납기</dt><dd>{emptyDash(row.standardLeadTime)}</dd></div>
                  <div><dt>발주일</dt><dd>{emptyDash(row.orderDate)}</dd></div>
                  <div><dt>입고예정일</dt><dd>{emptyDash(row.expectedReceiptDate)}</dd></div>
                  <div><dt>이슈사항</dt><dd>{emptyDash(row.issueNote)}</dd></div>
                  <div><dt>입고 완료</dt><dd><ReceiptCompletionBadge completed={row.receiptCompleted} /></dd></div>
                </dl>
              </>
            )}
          </article>
        );
      })}
    </div>
  );
}

function ProcurementExcelDialog({
  developmentUserKey,
  onClose,
  onApplied
}: {
  developmentUserKey: string;
  onClose: () => void;
  onApplied: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ProcurementExcelPreviewResponse | null>(null);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [selections, setSelections] = useState<Record<number, string>>({});
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [actionBarOffset, setActionBarOffset] = useState(144);
  const actionBarRef = useRef<HTMLDivElement | null>(null);

  const selectionArray = Object.entries(selections)
    .filter(([, projectId]) => projectId)
    .map(([sourceGroupSequence, projectId]) => ({ sourceGroupSequence: Number(sourceGroupSequence), projectId }));

  async function runPreview() {
    if (!file) {
      setMessage('Excel 파일을 선택하세요.');
      return;
    }

    setIsPreviewing(true);
    setMessage('');
    try {
      setPreview(await previewProcurementExcel(developmentUserKey, file, selectionArray));
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function apply() {
    if (!file || !preview) {
      return;
    }

    setIsApplying(true);
    setMessage('');
    try {
      await applyProcurementExcel(
        developmentUserKey,
        file,
        preview.fileSha256,
        reason.trim() || null,
        selectionArray,
        preview.expectedVersions);
      onApplied();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsApplying(false);
    }
  }

  const canApply = preview !== null
    && preview.newCount + preview.changedCount > 0
    && (!preview.reasonRequired || reason.trim().length > 0)
    && !isApplying;
  const applyDisabledReason = procurementApplyDisabledReason(preview, file, reason, isApplying);

  useEffect(() => {
    const actionBar = actionBarRef.current;
    if (!preview || !actionBar) {
      setActionBarOffset(144);
      return;
    }

    const updateOffset = () => setActionBarOffset(Math.ceil(actionBar.getBoundingClientRect().height + 16));
    updateOffset();

    if (typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    const observer = new ResizeObserver(updateOffset);
    observer.observe(actionBar);
    return () => observer.disconnect();
  }, [preview, reason]);

  return (
    <DialogBackdrop ariaLabel="구매 Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
      <div
        className="dialog wide-dialog"
        style={{ '--excel-action-bar-offset': `${actionBarOffset}px` } as React.CSSProperties}
      >
        <div className="subsection-header">
          <h3>구매 Excel 업로드</h3>
          <button type="button" onClick={onClose}>닫기</button>
        </div>
        <div className="toolbar">
          <input type="file" accept=".xlsx" onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
            setPreview(null);
          }} />
          <button type="button" disabled={isPreviewing} onClick={runPreview}>{isPreviewing ? '미리보기 중' : 'Preview'}</button>
        </div>
        {preview ? (
          <>
            <div className="excel-preview-action-bar" ref={actionBarRef}>
              <p className="muted-text">출하일은 구매품목 저장 대상이 아니며 프로젝트 납기일로 자동 표시됩니다.</p>
              {(preview.errorCount > 0 || preview.needsReviewCount > 0) && preview.newCount + preview.changedCount > 0 ? (
                <p className="warning-text">저장 가능한 항목만 반영됩니다. 저장 불가능한 항목은 수정 후 다시 업로드해 주세요.</p>
              ) : null}
              {preview.reasonRequired ? (
                <label className="form-field excel-preview-reason">
                  <span>수정사유*</span>
                  <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
                </label>
              ) : null}
              <button type="button" className="primary-button" disabled={!canApply} onClick={apply}>{isApplying ? '저장 중' : '저장 가능한 항목 적용'}</button>
              {!canApply && applyDisabledReason ? <p className="warning-text">{applyDisabledReason}</p> : null}
            </div>
            <div className="procurement-match-list">
              {preview.projectMatches.map((match) => (
                <div className="procurement-match-card" key={match.sourceGroupSequence}>
                  <strong>{match.excelProjectTitle ?? '-'}</strong>
                  <span>{procurementMatchStatusLabel(match.matchStatus)}</span>
                  {match.matchStatus !== 'Matched' && match.candidates.length > 0 ? (
                    <select
                      value={selections[match.sourceGroupSequence] ?? ''}
                      onChange={(event) => setSelections((current) => ({ ...current, [match.sourceGroupSequence]: event.target.value }))}
                    >
                      <option value="">프로젝트 선택</option>
                      {match.candidates.map((candidate) => (
                        <option key={candidate.projectId} value={candidate.projectId}>{candidate.projectTitle} ({candidate.projectCode})</option>
                      ))}
                    </select>
                  ) : <span>{match.matchedProjectTitle ?? '-'}</span>}
                </div>
              ))}
            </div>
            <ProcurementPreview rows={preview.rows} />
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </DialogBackdrop>
  );
}

function ProcurementPreview({ rows }: { rows: ProcurementExcelPreviewResponse['rows'] }) {
  const isMobile = useIsMobileViewport();
  const saveableRows = rows.filter((row) => row.resultType === 'New' || row.resultType === 'Changed');
  const blockedRows = rows.filter((row) => row.resultType === 'NeedsReview' || row.resultType === 'Error');
  const sections = [
    { title: '저장 가능한 데이터 목록', rows: saveableRows, kind: 'saveable' },
    { title: '저장 불가능한 데이터 목록', rows: blockedRows, kind: 'blocked' }
  ];

  if (isMobile) {
    return (
      <div className="excel-preview-cards excel-preview-mobile">
        {sections.map((section) => (
          <section className={`excel-preview-section ${section.kind}`} key={section.title}>
            <h4>{section.title} {section.rows.length}건</h4>
            {section.rows.map((row) => (
              <article className="excel-preview-card" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
                <div className="subsection-header">
                  <h3>Row {row.excelRowNumber || '-'}</h3>
                  <span className={row.resultType === 'Error' || row.resultType === 'NeedsReview' ? 'negative-text' : undefined}>{procurementResultLabel(row.resultType)}</span>
                </div>
                <dl className="mobile-detail-list">
                  <div><dt>프로젝트</dt><dd>{emptyDash(row.sourceProjectText)}</dd></div>
                  <div><dt>Code</dt><dd>{emptyDash(row.sourceProjectCodeText)}</dd></div>
                  <div><dt>통상납기</dt><dd>{emptyDash(row.standardLeadTime)}</dd></div>
                  <div><dt>발주품목</dt><dd className="order-item-badge">{emptyDash(row.orderItem)}</dd></div>
                  <div><dt>기술 담당자</dt><dd>{emptyDash(row.technicalOwner)}</dd></div>
                  <div><dt>발주일</dt><dd>{emptyDash(row.orderDate)}</dd></div>
                  <div><dt>입고예정일</dt><dd>{emptyDash(row.expectedReceiptDate)}</dd></div>
                  <div><dt>이슈사항</dt><dd>{emptyDash(row.issueNote)}</dd></div>
                  <div><dt>입고 완료</dt><dd>{formatReceiptCompleted(row.receiptCompleted)}</dd></div>
                  {section.kind === 'blocked' ? (
                    <div className="excel-preview-row-reasons">
                      <strong>사유</strong>
                      <dl>
                        <div><dt>필드</dt><dd>{issueFieldText(row)}</dd></div>
                        <div><dt>입력값</dt><dd>{issueInputText(row)}</dd></div>
                        <div><dt>문제</dt><dd>{issueProblemText(row)}</dd></div>
                      </dl>
                    </div>
                  ) : null}
                </dl>
              </article>
            ))}
            {section.rows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
          </section>
        ))}
      </div>
    );
  }

  return (
    <div className="excel-preview-table excel-preview-desktop">
      <section className="excel-preview-section saveable" key="saveable">
        <h4>저장 가능한 데이터 목록 {saveableRows.length}건</h4>
        <div className="excel-preview-grid saveable" role="table" aria-label="저장 가능한 데이터 목록">
          <div className="excel-preview-head" role="row">
            <span>Excel 행</span>
            <span>프로젝트</span>
            <span>Code</span>
            <span>통상납기</span>
            <span>발주품목</span>
            <span>기술 담당자</span>
            <span>발주일</span>
            <span>입고예정일</span>
            <span>이슈사항</span>
            <span>입고 완료</span>
          </div>
          {saveableRows.map((row) => (
            <div className="excel-preview-row" data-result={row.resultType} role="row" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
              <strong>{row.excelRowNumber || '-'}</strong>
              <span>{emptyDash(row.sourceProjectText)}</span>
              <span>{emptyDash(row.sourceProjectCodeText)}</span>
              <span>{emptyDash(row.standardLeadTime)}</span>
              <span className="order-item-badge">{emptyDash(row.orderItem)}</span>
              <span>{emptyDash(row.technicalOwner)}</span>
              <span>{emptyDash(row.orderDate)}</span>
              <span>{emptyDash(row.expectedReceiptDate)}</span>
              <span>{emptyDash(row.issueNote)}</span>
              <span>{formatReceiptCompleted(row.receiptCompleted)}</span>
            </div>
          ))}
        </div>
        {saveableRows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      </section>
      <section className="excel-preview-section blocked" key="blocked">
        <h4>저장 불가능한 데이터 목록 {blockedRows.length}건</h4>
        <div className="excel-preview-grid blocked" role="table" aria-label="저장 불가능한 데이터 목록">
          <div className="excel-preview-head" role="row">
            <span>Excel 행</span>
            <span>프로젝트</span>
            <span>Code</span>
            <span>통상납기</span>
            <span>발주품목</span>
            <span>기술 담당자</span>
            <span>발주일</span>
            <span>입고예정일</span>
            <span>이슈사항</span>
            <span>입고 완료</span>
          </div>
          {blockedRows.map((row) => (
            <div className="excel-preview-row-group" key={`${row.excelRowNumber}-${row.sourceGroupSequence}`}>
              <div className="excel-preview-row" data-result={row.resultType} role="row">
                <strong>{row.excelRowNumber || '-'}</strong>
                <span>{emptyDash(row.sourceProjectText)}</span>
                <span>{emptyDash(row.sourceProjectCodeText)}</span>
                <span>{emptyDash(row.standardLeadTime)}</span>
                <span className="order-item-badge">{emptyDash(row.orderItem)}</span>
                <span>{emptyDash(row.technicalOwner)}</span>
                <span>{emptyDash(row.orderDate)}</span>
                <span>{emptyDash(row.expectedReceiptDate)}</span>
                <span>{emptyDash(row.issueNote)}</span>
                <span>{formatReceiptCompleted(row.receiptCompleted)}</span>
              </div>
              <div className="excel-preview-row-reasons">
                <strong>사유</strong>
                <dl>
                  <div><dt>필드</dt><dd>{issueFieldText(row)}</dd></div>
                  <div><dt>입력값</dt><dd>{issueInputText(row)}</dd></div>
                  <div><dt>문제</dt><dd>{issueProblemText(row)}</dd></div>
                </dl>
              </div>
            </div>
          ))}
        </div>
        {blockedRows.length === 0 ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      </section>
    </div>
  );
}

function formatReceiptCompleted(value: boolean | null) {
  if (value === null) {
    return '-';
  }

  return value ? '완료' : '미완료';
}

function procurementResultLabel(resultType: string) {
  switch (resultType) {
    case 'New': return '신규';
    case 'Changed': return '변경';
    case 'Unchanged': return '동일';
    case 'Skipped': return '건너뜀';
    case 'MissingFromUpload': return '업로드 누락';
    case 'NeedsReview': return '확인 필요';
    case 'Error': return '오류';
    default: return resultType;
  }
}

function procurementMatchStatusLabel(status: string) {
  switch (status) {
    case 'Matched': return '매칭 완료';
    case 'NeedsReview': return '확인 필요';
    case 'Unmatched': return '등록되지 않은 프로젝트';
    case 'Error': return '오류';
    default: return status;
  }
}

function MaterialReceiptsPage({
  developmentUserKey,
  canUpdateMaterialReceipt,
  onBack
}: {
  developmentUserKey: string;
  canUpdateMaterialReceipt: boolean;
  onBack: () => void;
}) {
  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [state, setState] = useState<LoadState<ProcurementItem[]>>({ kind: 'loading' });
  const [items, setItems] = useState<ProcurementItem[]>([]);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [includeCompleted, setIncludeCompleted] = useState(false);
  const isMobile = useIsMobileViewport();

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    getMaterialReceipts(developmentUserKey, search, includeCompleted, dateFrom, dateTo)
      .then((response) => {
        setItems(response.items);
        setState(response.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: response.items });
      })
      .catch((error: unknown) => setState(toLoadError(error, '자재 입고 입력 항목을 불러올 수 없습니다.')));
  }, [dateFrom, dateTo, developmentUserKey, includeCompleted, search]);

  useEffect(() => {
    queueMicrotask(load);
  }, [load]);

  if (!canUpdateMaterialReceipt) {
    return <section className="page-surface"><StateMessage state={{ kind: 'forbidden', message: '권한이 없습니다.' }} /></section>;
  }

  function setReceipt(itemId: string, next: Partial<ProcurementItem>) {
    setItems((current) => current.map((item) => item.itemId === itemId ? { ...item, ...next } : item));
  }

  async function save() {
    setMessage('');
    try {
      await updateMaterialReceipts(developmentUserKey, {
        reason: reason.trim() || null,
        items: items.map((item) => ({
          itemId: item.itemId,
          expectedRowVersion: item.rowVersion,
          receiptCompleted: item.receiptCompleted,
          receiptCompletedAtUtc: item.receiptCompletedAtUtc,
          receiptCompletionNote: item.receiptCompletionNote
        }))
      });
      setReason('');
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    }
  }

  return (
    <section className="page-surface procurement-section">
      <div className="subsection-header">
        <div>
          <p className="eyebrow">Materials</p>
          <h2>자재 입고 입력</h2>
        </div>
        <div className="button-row">
          <button type="button" onClick={onBack}>프로젝트 목록</button>
          <button type="button" className="primary-button" onClick={save}>저장</button>
        </div>
      </div>
      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); load(); }}>
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트 또는 발주품목 검색" />
        <label className="date-filter-field">
          <span>입고예정 시작일</span>
          <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
        </label>
        <label className="date-filter-field">
          <span>입고예정 종료일</span>
          <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
        </label>
        <button type="button" onClick={() => { setDateFrom(''); setDateTo(''); }}>필터 초기화</button>
        <button type="submit">검색</button>
      </form>
      <div className="inline-help-row">
        <p className="muted-text">자재 입고 입력 대상만 표시됩니다. 완료된 항목은 저장 후 기본 목록에서 사라집니다.</p>
        <label className="checkbox-field">
          <input
            type="checkbox"
            checked={includeCompleted}
            onChange={(event) => setIncludeCompleted(event.target.checked)}
          />
          완료 항목 포함
        </label>
      </div>
      <label className="form-field panel-reason-field">
        <span>수정사유</span>
        <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
      </label>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind === 'empty' ? <p className="empty-text">표시할 항목이 없습니다.</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' && state.kind !== 'empty' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <MaterialReceiptGroups items={items} onChange={setReceipt} isMobile={isMobile} />
      ) : null}
      {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}
    </section>
  );
}

function MaterialReceiptGroups({
  items,
  onChange,
  isMobile
}: {
  items: ProcurementItem[];
  onChange: (itemId: string, next: Partial<ProcurementItem>) => void;
  isMobile: boolean;
}) {
  const groups = groupMaterialReceiptItems(items);
  return (
    <div className={isMobile ? 'material-receipt-groups procurement-mobile' : 'material-receipt-groups procurement-desktop'} data-testid="material-receipt-mobile">
      {groups.map((group) => (
        <section className="material-receipt-group" key={group.projectId}>
          <div className="material-receipt-group-header">
            <strong>{group.projectTitle}</strong>
            <span>PJT Code: {group.projectCode}</span>
            <span>출하일: {emptyDash(group.shipmentDisplayDate)}</span>
          </div>
          <div className="material-receipt-items" role="table" aria-label={`${group.projectTitle} 자재 입고 입력`}>
            <div className="material-receipt-head" role="row">
              <span>발주품목</span><span>기술 담당자</span><span>입고예정일</span><span>입고 완료</span><span>완료일</span><span>완료 비고</span>
            </div>
            {group.items.map((item) => (
              <div className="material-receipt-row" role="row" key={item.itemId}>
                <span className="order-item-badge">{emptyDash(item.orderItem)}</span>
                <span>{emptyDash(item.technicalOwner)}</span>
                <span>{emptyDash(item.expectedReceiptDate)}</span>
                <div className="receipt-input-cell">
                  <label className="checkbox-field">
                    <input type="checkbox" checked={item.receiptCompleted} onChange={(event) => onChange(item.itemId, { receiptCompleted: event.target.checked })} />
                    입고 완료
                  </label>
                  <ReceiptCompletionBadge completed={item.receiptCompleted} />
                </div>
                <input type="datetime-local" value={toDateTimeLocal(item.receiptCompletedAtUtc ?? '')} onChange={(event) => onChange(item.itemId, { receiptCompletedAtUtc: fromDateTimeLocal(event.target.value) })} />
                <textarea value={item.receiptCompletionNote ?? ''} onChange={(event) => onChange(item.itemId, { receiptCompletionNote: event.target.value })} />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

function groupMaterialReceiptItems(items: ProcurementItem[]) {
  const groups: Array<{
    projectId: string;
    projectTitle: string;
    projectCode: string;
    shipmentDisplayDate: string | null;
    items: ProcurementItem[];
  }> = [];

  for (const item of items) {
    let group = groups.find((candidate) => candidate.projectId === item.projectId);
    if (!group) {
      group = {
        projectId: item.projectId,
        projectTitle: item.projectTitle,
        projectCode: item.projectCode,
        shipmentDisplayDate: formatShipmentDisplayDate(item),
        items: []
      };
      groups.push(group);
    }

    group.items.push(item);
  }

  return groups;
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
      onBack();
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
            onBack();
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
  const applyDisabledReason = panelExcelApplyDisabledReason(preview, file, reason, isApplying);

  return (
    <DialogBackdrop ariaLabel="Excel 업로드" onClose={onClose} closeDisabled={isPreviewing || isApplying}>
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
              {!canApplyPreview && applyDisabledReason ? <p className="warning-text">{applyDisabledReason}</p> : null}
            </div>
            <ExcelIssueSummary rows={preview.rows} />
            <ExcelPreviewDesktop rows={preview.rows} />
            <ExcelPreviewMobile rows={preview.rows} />
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </DialogBackdrop>
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
  const [state, setState] = useState<LoadState<{ project: ProjectDetail; panel: PanelPlaceholder }>>({ kind: 'loading' });

  useEffect(() => {
    Promise.all([
      getProject(developmentUserKey, projectId),
      getPanel(developmentUserKey, projectId, panelId)
    ])
      .then(([project, panel]) => setState({ kind: 'ready', data: { project, panel } }))
      .catch((error: unknown) => setState(toLoadError(error, '패널 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, panelId, projectId]);

  return (
    <section className="page-surface">
      <div className="page-header">
        <div>
          <p className="eyebrow">Product Panel</p>
          <h2>{state.kind === 'ready' ? `${state.data.panel.displayCode} 제품 상세` : '제품 상세'}</h2>
        </div>
        <button type="button" onClick={onBack}>프로젝트</button>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <>
          <ProjectContextSummary project={state.data.project} />
          <section className="project-context-summary product-context-summary" aria-label="제품 요약">
            <div><span>제품</span><strong>No.{state.data.panel.sequenceNumber} · {state.data.panel.panelName ?? '패널명 미입력'}</strong></div>
            <div><span>사이즈</span><strong>{formatSize(state.data.panel)}</strong></div>
            <div><span>제품 상태</span><strong>{formatWorkflowStage(state.data.panel.workflowStage)}</strong></div>
            <div><span>패널정보</span><strong>{state.data.panel.panelInfoCompleted ? '입력 완료' : '미입력'}</strong></div>
            <div><span>QR</span><strong>{state.data.panel.qrEligible ? '생성 가능' : '생성 불가'}</strong></div>
          </section>
        </>
      ) : null}
    </section>
  );
}

function DeletedProjectDetailPage({
  developmentUserKey,
  projectId,
  canReadSalesAmount,
  canPurgeDeletedProjects,
  onBack
}: {
  developmentUserKey: string;
  projectId: string;
  canReadSalesAmount: boolean;
  canPurgeDeletedProjects: boolean;
  onBack: () => void;
}) {
  const [state, setState] = useState<LoadState<DeletedProjectDetail>>({ kind: 'loading' });
  const [confirmText, setConfirmText] = useState('');
  const [restoreReason, setRestoreReason] = useState('');
  const [message, setMessage] = useState('');
  const [isPurging, setIsPurging] = useState(false);
  const [isRestoring, setIsRestoring] = useState(false);

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
  async function restore() {
    setMessage('');
    setIsRestoring(true);
    try {
      await restoreDeletedProject(developmentUserKey, projectId, restoreReason.trim() || null);
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsRestoring(false);
    }
  }

  async function purge() {
    setMessage('');
    if (confirmText !== '완전 삭제') {
      setMessage('확인 문구를 정확히 입력해 주세요.');
      return;
    }

    setIsPurging(true);
    try {
      await purgeDeletedProject(developmentUserKey, projectId, confirmText);
      onBack();
    } catch (error) {
      handleFormError(error, () => undefined, setMessage);
    } finally {
      setIsPurging(false);
    }
  }

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

      {canPurgeDeletedProjects ? (
        <>
          <section className="restore-zone" aria-label="삭제 프로젝트 복구">
            <div>
              <strong>복구</strong>
              <p className="muted-text">복구하면 일반 프로젝트 목록에 다시 표시됩니다. 삭제 전 프로젝트 상태는 유지됩니다.</p>
            </div>
            <label className="form-field compact-field">
              <span>복구 사유</span>
              <input value={restoreReason} onChange={(event) => setRestoreReason(event.target.value)} placeholder="선택 입력" />
            </label>
            <button type="button" className="primary-button" disabled={isRestoring} onClick={restore}>
              {isRestoring ? '복구 중' : '복구'}
            </button>
          </section>
          <section className="danger-zone" aria-label="삭제 프로젝트 완전 삭제">
            <div>
              <strong>완전 삭제</strong>
              <p className="muted-text">이 프로젝트와 연결된 구매정보, 제품·패널, 감사이력을 완전히 삭제합니다. 되돌릴 수 없습니다.</p>
            </div>
            <label className="form-field compact-field">
              <span>확인 문구: 완전 삭제</span>
              <input value={confirmText} onChange={(event) => setConfirmText(event.target.value)} />
            </label>
            <button type="button" className="danger-button" disabled={isPurging || confirmText !== '완전 삭제'} onClick={purge}>
              {isPurging ? '삭제 중' : '완전 삭제'}
            </button>
            {message ? <p role="alert" className="error-text">{message}</p> : null}
          </section>
        </>
      ) : null}

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

function ProjectContextSummary({ project }: { project: ProjectListItem }) {
  return (
    <section className="project-context-summary" aria-label="프로젝트 요약" data-testid="project-context-summary">
      <div className="project-context-title">
        <span>프로젝트</span>
        <strong>{project.projectTitle}</strong>
      </div>
      <div><span>고객사</span><strong>{project.customerName}</strong></div>
      <div><span>Code</span><strong>{project.projectCode}</strong></div>
      <div><span>Item</span><strong>{project.item}</strong></div>
      <div><span>납기일</span><strong>{formatDate(project.deliveryDate)}</strong></div>
      <div><span>포장방식</span><strong>{formatPackagingMethod(project.packagingMethod)}</strong></div>
      <div><span>상태</span><strong>{formatProjectStatus(project.status)}</strong></div>
    </section>
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
    <DialogBackdrop ariaLabel={title} onClose={onCancel} closeDisabled={isSaving}>
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
    </DialogBackdrop>
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
    <DialogBackdrop ariaLabel="프로젝트 삭제" onClose={onCancel} closeDisabled={isSaving}>
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
    </DialogBackdrop>
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

function GroupedHistory({
  groups,
  emptyText = '전체 이력이 없습니다.'
}: {
  groups: PanelInformationHistoryResponse['groups'];
  emptyText?: string | null;
}) {
  if (groups.length === 0) {
    return emptyText ? <p className="empty-text">{emptyText}</p> : null;
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

function ProcurementGroupedHistory({ groups }: { groups: ProcurementHistoryResponse['groups'] }) {
  if (groups.length === 0) {
    return null;
  }

  return (
    <ol className="audit-list grouped-audit-list procurement-audit-list">
      {groups.map((group) => (
        <li key={group.groupId}>
          <strong>{formatInputSource(group.inputSource)} · 대상 구매품목 {group.affectedItemCount}건</strong>
          <span>변경항목 {group.changeCount}건</span>
          {group.importFileName ? <small>입력 파일: {group.importFileName}</small> : null}
          {group.reason ? <small>수정사유: {group.reason}</small> : null}
          <small>{group.changedByName ?? group.changedByUserId ?? '-'} · {formatDateTime(group.changedAtUtc)}</small>
          <details>
            <summary>변경 상세</summary>
            <ol className="audit-change-list">
              {group.changes.map((change, index) => (
                <li key={`${group.groupId}-${change.entityId}-${change.fieldName ?? index}-${index}`}>
                  <strong>{change.sequenceNumber ? `구매품목 ${change.sequenceNumber}` : '구매품목'}</strong>
                  <span>{formatProcurementFieldName(change.fieldName)}: {change.oldValue ?? '-'} → {change.newValue ?? '-'}</span>
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

function formatProcurementFieldName(fieldName: string | null) {
  switch (fieldName) {
    case 'StandardLeadTime':
      return '통상납기';
    case 'OrderItem':
      return '발주품목';
    case 'TechnicalOwner':
      return '기술 담당자';
    case 'OrderDate':
      return '발주일';
    case 'ExpectedReceiptDate':
      return '입고예정일';
    case 'ShipmentText':
      return '출하일';
    case 'IssueNote':
      return '이슈사항';
    case 'ReceiptCompleted':
      return '입고 완료';
    case 'ReceiptCompletedAtUtc':
      return '입고 완료일';
    case 'ReceiptCompletedByUserId':
      return '입고 완료자';
    case 'ReceiptCompletionNote':
      return '완료 비고';
    default:
      return fieldName ?? '-';
  }
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
    setMessage(friendlyErrorMessage(error, '입력값을 확인해 주세요.'));
    return;
  }

  setMessage(friendlyErrorMessage(error, '요청을 처리할 수 없습니다.'));
}

function toLoadError<T>(error: unknown, fallback: string): LoadState<T> {
  if (error instanceof ApiError) {
    if (error.status === 403) {
      return { kind: 'forbidden', message: friendlyErrorMessage(error, '권한이 없습니다.') };
    }

    if (error.status === 404) {
      return { kind: 'not-found', message: friendlyErrorMessage(error, '대상을 찾을 수 없습니다.') };
    }

    return { kind: 'error', message: friendlyErrorMessage(error, fallback) };
  }

  return { kind: 'error', message: friendlyErrorMessage(error, fallback) };
}

function friendlyErrorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    if (error.status === 0) {
      return '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.';
    }

    return sanitizeUserMessage(error.message, fallback);
  }

  if (error instanceof TypeError && error.message.includes('fetch')) {
    return '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.';
  }

  if (error instanceof Error) {
    return sanitizeUserMessage(error.message, fallback);
  }

  return fallback;
}

function sanitizeUserMessage(message: string, fallback: string) {
  const forbidden = [
    'One or more validation errors occurred',
    'Failed to fetch',
    'Internal Server Error',
    'Bad Request',
    'Unauthorized',
    'Forbidden',
    'Conflict',
    'Stack Trace',
    'SQL'
  ];
  if (!message || forbidden.some((text) => message.includes(text))) {
    return fallback;
  }

  return message.replaceAll('QMS', '시스템');
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

function projectExcelResultLabel(resultType: ProjectExcelPreviewResponse['rows'][number]['resultType']) {
  return {
    New: '신규',
    NeedsReview: '확인 필요',
    Error: '오류'
  }[resultType];
}

function canApplyProjectExcel(
  preview: ProjectExcelPreviewResponse | null,
  file: File | null,
  isApplying: boolean
) {
  return !projectExcelApplyDisabledReason(preview, file, isApplying);
}

function projectExcelApplyDisabledReason(
  preview: ProjectExcelPreviewResponse | null,
  file: File | null,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.errorCount > 0) {
    return '오류 행이 있습니다.';
  }

  if (preview.needsReviewCount > 0) {
    return '확인할 항목이 있습니다. 내용을 확인해 주세요.';
  }

  if (preview.newCount === 0) {
    return '변경사항이 없습니다.';
  }

  return '';
}

function procurementApplyDisabledReason(
  preview: ProcurementExcelPreviewResponse | null,
  file: File | null,
  reason: string,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.newCount + preview.changedCount === 0) {
    if (preview.errorCount > 0) {
      return '저장 가능한 항목이 없습니다. 오류 행을 수정해 주세요.';
    }

    if (preview.needsReviewCount > 0) {
      return '저장 가능한 항목이 없습니다. 확인할 프로젝트를 선택해 주세요.';
    }

    return '변경사항이 없습니다.';
  }

  if (preview.reasonRequired && reason.trim().length === 0) {
    return '수정사유를 입력해 주세요.';
  }

  return '';
}

function panelExcelApplyDisabledReason(
  preview: PanelInformationExcelPreviewResponse | null,
  file: File | null,
  reason: string,
  isApplying: boolean
) {
  if (!file) {
    return '선택한 파일이 없습니다.';
  }

  if (!preview) {
    return '미리보기를 먼저 실행해 주세요.';
  }

  if (isApplying) {
    return '저장 중입니다.';
  }

  if (preview.errorCount > 0) {
    return '오류 행이 있습니다.';
  }

  if (preview.newCount + preview.changedCount === 0) {
    return '변경사항이 없습니다.';
  }

  if (preview.reasonRequired && reason.trim().length === 0) {
    return '수정사유를 입력해 주세요.';
  }

  return '';
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

function emptyDash(value: string | null | undefined) {
  return value && value.trim() ? value : '-';
}

function formatShipmentDisplayDate(item: ProcurementItem) {
  return item.shipmentDisplayDate ?? item.projectDeliveryDate;
}

function procurementItemToForm(item: ProcurementItem): ProcurementRowForm {
  return {
    itemId: item.itemId,
    rowVersion: item.rowVersion,
    sourceProjectText: item.sourceProjectText ?? item.projectTitle,
    sourceProjectCodeText: item.sourceProjectCodeText ?? item.projectCode,
    standardLeadTime: item.standardLeadTime ?? '',
    orderItem: item.orderItem ?? '',
    technicalOwner: item.technicalOwner ?? '',
    orderDate: item.orderDate ?? '',
    expectedReceiptDate: item.expectedReceiptDate ?? '',
    shipmentDisplayDate: formatShipmentDisplayDate(item),
    issueNote: item.issueNote ?? '',
    receiptCompleted: item.receiptCompleted,
    receiptCompletedAtUtc: item.receiptCompletedAtUtc ?? '',
    receiptCompletionNote: item.receiptCompletionNote ?? '',
    dDayText: item.dDayText
  };
}

function emptyProcurementRow(projectDeliveryDate: string | null = null): ProcurementRowForm {
  return {
    itemId: null,
    rowVersion: null,
    sourceProjectText: '',
    sourceProjectCodeText: '',
    standardLeadTime: '',
    orderItem: '',
    technicalOwner: '',
    orderDate: '',
    expectedReceiptDate: '',
    shipmentDisplayDate: projectDeliveryDate,
    issueNote: '',
    receiptCompleted: false,
    receiptCompletedAtUtc: '',
    receiptCompletionNote: '',
    dDayText: '-'
  };
}

function procurementFormToRequest(row: ProcurementRowForm) {
  return {
    itemId: row.itemId,
    expectedRowVersion: row.rowVersion,
    standardLeadTime: row.standardLeadTime.trim() || null,
    orderItem: row.orderItem.trim() || null,
    technicalOwner: row.technicalOwner.trim() || null,
    orderDate: row.orderDate || null,
    expectedReceiptDate: row.expectedReceiptDate || null,
    issueNote: row.issueNote.trim() || null,
    receiptCompleted: row.receiptCompleted,
    receiptCompletedAtUtc: row.receiptCompletedAtUtc || null,
    receiptCompletionNote: row.receiptCompletionNote.trim() || null
  };
}

function isProcurementForm(value: ProcurementItem | ProcurementRowForm): value is ProcurementRowForm {
  return !('projectId' in value);
}

function toDateTimeLocal(value: string | null) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

function fromDateTimeLocal(value: string) {
  return value ? new Date(value).toISOString() : '';
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
