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
  getAuditHistory,
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
  SalesOwner
} from './projects';

type View =
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'detail'; projectId: string }
  | { kind: 'deleted-detail'; projectId: string }
  | { kind: 'edit'; projectId: string }
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

export function App() {
  const [developmentUserKey, setDevelopmentUserKey] = useState(defaultDevelopmentUserKey ?? 'dev-sales');
  const [view, setView] = useState<View>({ kind: 'list' });
  const [health, setHealth] = useState<LoadState<ReadyHealth>>({ kind: 'loading' });
  const [currentUser, setCurrentUser] = useState<LoadState<CurrentUser>>({ kind: 'loading' });

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

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">EMI QMS</p>
          <h1>프로젝트 등록·패널 Placeholder</h1>
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
          onBack={() => setView({ kind: 'list' })}
          onEdit={() => setView({ kind: 'edit', projectId: view.projectId })}
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
  const [tab, setTab] = useState<ProjectListTab>('Active');
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
        <div className="project-list">
          {state.data.map((project) => (
            <button
              className="project-row"
              key={project.projectId}
              type="button"
              onClick={() => tab === 'Deleted' ? onOpenDeleted(project.projectId) : onOpen(project.projectId)}
            >
              <span>
                <strong>{project.projectTitle}</strong>
                <small>{project.customerName} · {project.item}</small>
              </span>
              <span>{project.projectCode}</span>
              <span>{project.activePanelCount}면</span>
              <span>{formatDate(project.deliveryDate)}</span>
              <ProjectStatusBadge status={project.status} />
              {'deletedAtUtc' in project ? <span>{formatDateTime(project.deletedAtUtc)}</span> : null}
              {canReadSalesAmount && project.salesAmount !== undefined ? (
                <SalesAmountField amount={project.salesAmount} currencyCode={project.currencyCode} />
              ) : null}
            </button>
          ))}
        </div>
      ) : null}
    </section>
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
  onBack,
  onEdit,
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
  onBack: () => void;
  onEdit: () => void;
  onOpenPanel: (panelId: string) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [panels, setPanels] = useState<PanelPlaceholder[]>([]);
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([]);
  const [dialog, setDialog] = useState<null | 'hold' | 'resume' | 'cancel' | 'reactivate' | 'delete'>(null);
  const [reason, setReason] = useState('');
  const [confirmProjectTitle, setConfirmProjectTitle] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(() => {
    Promise.all([
      getProject(developmentUserKey, projectId),
      listPanels(developmentUserKey, projectId),
      getAuditHistory(developmentUserKey, projectId)
    ])
      .then(([project, panelItems, history]) => {
        setProjectState({ kind: 'ready', data: project });
        setPanels(panelItems);
        setAuditEvents(history.items);
      })
      .catch((error: unknown) => setProjectState(toLoadError(error, '프로젝트 상세를 불러올 수 없습니다.')));
  }, [developmentUserKey, projectId]);

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
        developmentUserKey={developmentUserKey}
        projectId={projectId}
        project={project}
        canUpdatePanelInfo={canUpdatePanelInfo}
      />

      <section className="subsection">
        <div className="subsection-header">
          <h3>패널 Placeholder</h3>
          <span>진행률은 체크리스트 적용 후 계산</span>
        </div>
        <PanelPlaceholderList panels={panels} onOpenPanel={onOpenPanel} />
      </section>

      <section className="subsection">
        <h3>변경이력</h3>
        <AuditHistory events={auditEvents} />
      </section>

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
  developmentUserKey,
  projectId,
  project,
  canUpdatePanelInfo
}: {
  developmentUserKey: string;
  projectId: string;
  project: ProjectDetail;
  canUpdatePanelInfo: boolean;
}) {
  const [state, setState] = useState<LoadState<PanelInformationResponse>>({ kind: 'loading' });
  const [historyState, setHistoryState] = useState<LoadState<PanelInformationHistoryResponse>>({ kind: 'loading' });
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
    setHistoryState({ kind: 'loading' });
    setMessage('');

    Promise.all([
      getPanelInformation(developmentUserKey, projectId),
      getPanelInformationHistory(developmentUserKey, projectId)
    ])
      .then(([panelInfo, history]) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setState({ kind: 'ready', data: panelInfo });
        setHistoryState({ kind: 'ready', data: history });
        setRows(panelInfo.panels.map((panel) => panelToRowForm(panel, editInputUnitRef.current)));
      })
      .catch((error: unknown) => {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setState(toLoadError(error, '패널정보를 불러올 수 없습니다.'));
      });
  }, [developmentUserKey, projectId]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    window.localStorage.setItem('emi-qms-panel-display-unit', displayUnit);
  }, [displayUnit]);

  const data = state.kind === 'ready' ? state.data : null;
  const canEdit = canUpdatePanelInfo && project.status === 'Active';
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
      getPanelInformationHistory(developmentUserKey, projectId)
        .then((history) => setHistoryState({ kind: 'ready', data: history }))
        .catch((error: unknown) => setHistoryState(toLoadError(error, '입력이력을 불러올 수 없습니다.')));
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

  return (
    <section className="subsection panel-info-section">
      <div className="subsection-header">
        <div>
          <h3>패널정보</h3>
          <span>{formatPackagingMethod(project.packagingMethod)}</span>
        </div>
        <div className="button-row">
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

      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}

      {data ? (
        <>
          <div className="panel-info-summary">
            <StatusChip label="완료" value={`${data.panelInfoCompletedCount}/${data.activePanelCount}`} />
            <StatusChip label="미완료" value={String(data.panelInfoPendingCount)} />
            <StatusChip label="QR 가능" value={String(data.qrEligibleCount)} />
            <StatusChip label="동일명칭" value={String(data.duplicatePanelNameGroupCount)} />
          </div>

          {data.panelInformationStatusMessage ? (
            <p role="status" className="warning-text">{data.panelInformationStatusMessage}</p>
          ) : null}
          {!canUpdatePanelInfo ? <p className="muted-text">읽기 전용</p> : null}
          {canUpdatePanelInfo && project.status !== 'Active' ? (
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

          <div className="panel-info-table" role="table" aria-label="패널정보 직접 입력">
            <div className="panel-info-table-head" role="row">
              <span>No</span>
              <span>Panel Name</span>
              <span>W</span>
              <span>H</span>
              <span>D</span>
              <span>정보 상태</span>
              <span>QR</span>
              <span>동일명칭</span>
            </div>
            {visibleRows.map((row) => (
              <PanelInformationEditableRow
                key={row.panelId}
                row={row}
                displayUnit={displayUnit}
                canEdit={canEdit && row.original.panelStatus === 'Active'}
                onPanelNameChange={setPanelName}
                onSizeChange={setSizeInput}
              />
            ))}
          </div>

          <div className="panel-info-cards">
            {visibleRows.map((row) => (
              <PanelInformationCard
                key={row.panelId}
                row={row}
                displayUnit={displayUnit}
                canEdit={canEdit && row.original.panelStatus === 'Active'}
                onPanelNameChange={setPanelName}
                onSizeChange={setSizeInput}
              />
            ))}
          </div>

          {message ? <p role="alert" className={successMessage(message) ? 'success-text' : 'error-text'}>{message}</p> : null}

          <section className="subsection">
            <h3>입력이력</h3>
            {historyState.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
            {historyState.kind !== 'ready' && historyState.kind !== 'loading' ? <StateMessage state={historyState} /> : null}
            {historyState.kind === 'ready' ? (
              <>
                <ExcelImportHistory batches={historyState.data.excelImportBatches} />
                <AuditHistory events={historyState.data.auditEvents} />
              </>
            ) : null}
          </section>
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
      <strong>{row.panelNumber}<small>{row.displayCode}</small></strong>
      <input aria-label={`${row.panelNumber} 패널명`} value={row.currentPanelName} disabled={!canEdit} onChange={(event) => onPanelNameChange(row.panelId, event.target.value)} />
      <input aria-label={`${row.panelNumber} W`} inputMode="decimal" value={row.widthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'widthInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} H`} inputMode="decimal" value={row.heightInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'heightInput', event.target.value)} />
      <input aria-label={`${row.panelNumber} D`} inputMode="decimal" value={row.depthInput} disabled={!canEdit} onChange={(event) => onSizeChange(row.panelId, 'depthInput', event.target.value)} />
      <span>{row.original.panelInfoCompleted ? '완료' : '미완료'}</span>
      <span>{row.original.qrEligible ? '가능' : '불가'}</span>
      <span>{row.original.hasDuplicateName ? `동일 명칭 ${row.original.duplicateNameCount}면` : '-'}</span>
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
    <article className="panel-info-card">
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
        <div><dt>패널정보</dt><dd>{row.original.panelInfoCompleted ? '완료' : '미완료'}</dd></div>
        <div><dt>QR</dt><dd>{row.original.qrEligible ? '가능' : '불가'}</dd></div>
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
            <div className="panel-info-summary">
              <StatusChip label="신규" value={String(preview.newCount)} />
              <StatusChip label="변경" value={String(preview.changedCount)} />
              <StatusChip label="동일" value={String(preview.unchangedCount)} />
              <StatusChip label="오류" value={String(preview.errorCount)} />
            </div>
            {preview.reasonRequired ? (
              <label className="form-field">
                <span>수정사유*</span>
                <textarea value={reason} onChange={(event) => setReason(event.target.value)} />
              </label>
            ) : null}
            <div className="excel-preview-table">
              {preview.rows.map((row) => (
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
            <div className="button-row">
              <button type="button" className="primary-button" disabled={isApplying || preview.errorCount > 0} onClick={applyFile}>
                {isApplying ? '적용 중' : '전체 적용'}
              </button>
            </div>
          </>
        ) : null}
        {message ? <p role="alert" className="error-text">{message}</p> : null}
      </div>
    </div>
  );
}

function ExcelImportHistory({ batches }: { batches: PanelInformationHistoryResponse['excelImportBatches'] }) {
  if (batches.length === 0) {
    return <p className="empty-text">Excel 입력이력이 없습니다.</p>;
  }

  return (
    <ol className="audit-list">
      {batches.map((batch) => (
        <li key={batch.importBatchId}>
          <strong>{batch.originalFileName}</strong>
          <span>신규 {batch.newPanelCount} · 변경 {batch.changedPanelCount} · 동일 {batch.unchangedPanelCount}</span>
          <small>{batch.uploadedByUserName ?? batch.uploadedByUserId ?? '-'} · {formatDateTime(batch.uploadedAtUtc)}</small>
        </li>
      ))}
    </ol>
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
          <p className="eyebrow">Panel Placeholder</p>
          <h2>{state.kind === 'ready' ? state.data.displayCode : '패널 상세'}</h2>
        </div>
        <button type="button" onClick={onBack}>프로젝트</button>
      </div>
      {state.kind === 'loading' ? <p className="muted-text">Loading</p> : null}
      {state.kind !== 'ready' && state.kind !== 'loading' ? <StateMessage state={state} /> : null}
      {state.kind === 'ready' ? (
        <dl className="detail-grid">
          <div><dt>패널명</dt><dd>{state.data.panelName ?? '패널명 미정'}</dd></div>
          <div><dt>패널 상태</dt><dd>{state.data.panelStatus}</dd></div>
          <div><dt>패널정보</dt><dd>{state.data.panelInfoCompleted ? '완료' : '대기'}</dd></div>
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
        <h3>보존된 패널 Placeholder</h3>
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
  return <span className="status-badge" data-status={status}>{status}</span>;
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
    return <p className="empty-text">패널 Placeholder가 없습니다.</p>;
  }

  return (
    <div className="panel-list">
      {panels.map((panel) => (
        <button key={panel.panelId} type="button" className="panel-row" onClick={() => onOpenPanel(panel.panelId)}>
          <strong>{panel.displayCode}</strong>
          <span>{panel.panelName ?? '패널명 미정'}</span>
          <span>{panel.panelInfoCompleted ? '패널정보 완료' : '패널정보 대기'}</span>
          <span>{panel.qrEligible ? 'QR 생성 조건 충족' : 'QR 생성 조건 미충족'}</span>
          <span>{panel.panelStatus}</span>
        </button>
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

function StatusChip({ label, value }: { label: string; value: string }) {
  return (
    <div className="status-chip">
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
    return '사이즈 미정';
  }

  if (unit === 'Inch') {
    return `${(panel.widthMm / 25.4).toFixed(2)} x ${(panel.heightMm / 25.4).toFixed(2)} x ${(panel.depthMm / 25.4).toFixed(2)} inch`;
  }

  return `${trimTrailingZeros(panel.widthMm.toFixed(3))} x ${trimTrailingZeros(panel.heightMm.toFixed(3))} x ${trimTrailingZeros(panel.depthMm.toFixed(3))} mm`;
}

function projectTabs(canReadDeleted: boolean): Array<{ value: ProjectListTab; label: string }> {
  const tabs: Array<{ value: ProjectListTab; label: string }> = [
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
    return '사이즈 미정';
  }

  return `${panel.width} x ${panel.height} x ${panel.depth} mm`;
}
