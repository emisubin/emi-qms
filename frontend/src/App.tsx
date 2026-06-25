import { FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  changePanelCount,
  changeProjectStatus,
  createProject,
  defaultDevelopmentUserKey,
  getAuditHistory,
  getCurrentUser,
  getPanel,
  getProject,
  getReadyHealth,
  getSalesOwners,
  listPanels,
  listProjects,
  updateProject
} from './api';
import type { ReadyHealth } from './health';
import type { CurrentUser } from './identity';
import { maxPanelsPerProject } from './projects';
import type {
  AuditEvent,
  PanelPlaceholder,
  ProjectDetail,
  ProjectListItem,
  ProjectStatus,
  SalesOwner
} from './projects';

type View =
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'detail'; projectId: string }
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
  salesAmount: string;
  currencyCode: string;
  deliveryLocation: string;
  reason: string;
};

const developmentUsers = [
  'dev-sales',
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
  const canReadSalesAmount = permissions.includes('Project.SalesAmount.Read');

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
          canReadSalesAmount={canReadSalesAmount}
          onCreate={() => setView({ kind: 'create' })}
          onOpen={(projectId) => setView({ kind: 'detail', projectId })}
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
          canReadSalesAmount={canReadSalesAmount}
          onBack={() => setView({ kind: 'list' })}
          onEdit={() => setView({ kind: 'edit', projectId: view.projectId })}
          onOpenPanel={(panelId) => setView({ kind: 'panel', projectId: view.projectId, panelId })}
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
  canReadSalesAmount,
  onCreate,
  onOpen
}: {
  developmentUserKey: string;
  canCreate: boolean;
  canReadSalesAmount: boolean;
  onCreate: () => void;
  onOpen: (projectId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [state, setState] = useState<LoadState<ProjectListItem[]>>({ kind: 'loading' });

  const load = useCallback(() => {
    listProjects(developmentUserKey, search)
      .then((response) => setState(response.items.length === 0 ? { kind: 'empty' } : { kind: 'ready', data: response.items }))
      .catch((error: unknown) => setState(toLoadError(error, '프로젝트 목록을 불러올 수 없습니다.')));
  }, [developmentUserKey, search]);

  useEffect(() => {
    load();
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
              onClick={() => onOpen(project.projectId)}
            >
              <span>
                <strong>{project.projectTitle}</strong>
                <small>{project.customerName} · {project.item}</small>
              </span>
              <span>{project.projectCode}</span>
              <span>{project.activePanelCount}면</span>
              <span>{formatDate(project.deliveryDate)}</span>
              <ProjectStatusBadge status={project.status} />
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
  canReadSalesAmount,
  onBack,
  onEdit,
  onOpenPanel
}: {
  developmentUserKey: string;
  projectId: string;
  canUpdate: boolean;
  canHold: boolean;
  canCancel: boolean;
  canReadSalesAmount: boolean;
  onBack: () => void;
  onEdit: () => void;
  onOpenPanel: (panelId: string) => void;
}) {
  const [projectState, setProjectState] = useState<LoadState<ProjectDetail>>({ kind: 'loading' });
  const [panels, setPanels] = useState<PanelPlaceholder[]>([]);
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([]);
  const [dialog, setDialog] = useState<null | 'hold' | 'resume' | 'cancel' | 'reactivate'>(null);
  const [reason, setReason] = useState('');
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
    if (!dialog) {
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
        </div>
      </div>

      <ProjectSummary project={project} canReadSalesAmount={canReadSalesAmount} />

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

      {dialog ? (
        <StatusReasonDialog
          action={dialog}
          reason={reason}
          error={dialogError}
          isSaving={isSaving}
          onReasonChange={setReason}
          onCancel={() => {
            setDialog(null);
            setReason('');
            setDialogError('');
          }}
          onSubmit={submitStatusChange}
        />
      ) : null}
    </section>
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

function ProjectSummary({ project, canReadSalesAmount }: { project: ProjectDetail; canReadSalesAmount: boolean }) {
  return (
    <dl className="detail-grid">
      <div><dt>상태</dt><dd><ProjectStatusBadge status={project.status} /></dd></div>
      <div><dt>고객사</dt><dd>{project.customerName}</dd></div>
      <div><dt>Item</dt><dd>{project.item}</dd></div>
      <div><dt>PJT Code</dt><dd>{project.projectCode}</dd></div>
      <div><dt>면수</dt><dd>{project.activePanelCount}</dd></div>
      <div><dt>납기일</dt><dd>{formatDate(project.deliveryDate)}</dd></div>
      <div><dt>영업담당자</dt><dd>{project.salesOwnerName}</dd></div>
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

function AuditHistory({ events }: { events: AuditEvent[] }) {
  if (events.length === 0) {
    return <p className="empty-text">변경이력이 없습니다.</p>;
  }

  return (
    <ol className="audit-list">
      {events.map((event) => (
        <li key={event.auditEventId}>
          <strong>{event.action}</strong>
          <span>{event.fieldName ? `${event.fieldName}: ${event.oldValue ?? '-'} → ${event.newValue ?? '-'}` : event.reason ?? '-'}</span>
          <small>{event.changedByUserName ?? event.changedByUserId ?? '-'} · {formatDateTime(event.changedAtUtc)}</small>
        </li>
      ))}
    </ol>
  );
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
    salesAmount: form.salesAmount.trim() ? Number(form.salesAmount) : null,
    currencyCode: form.salesAmount.trim() ? form.currencyCode.trim().toUpperCase() : null,
    deliveryLocation: form.deliveryLocation.trim() || null
  };
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

function lowerFirst(value: string) {
  return value.length === 0 ? value : `${value[0].toLowerCase()}${value.slice(1)}`;
}

function formatDate(value: string) {
  return value;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function formatSize(panel: PanelPlaceholder) {
  if (panel.width === null || panel.height === null || panel.depth === null) {
    return '사이즈 미정';
  }

  return `${panel.width} x ${panel.height} x ${panel.depth} mm`;
}
