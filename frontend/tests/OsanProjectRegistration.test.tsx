import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@microsoft/teams-js', () => ({
  app: {
    initialize: vi.fn(async () => undefined),
    getContext: vi.fn(async () => ({}))
  }
}));

import { App } from '../src/App';
import {
  resetBusinessUnitRequestContext,
  selectBusinessUnit,
  setRuntimeMutationAllowed
} from '../src/api';

const projectId = '91000000-0000-0000-0000-000000000001';

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function currentUser(permissions = ['projects.read', 'Project.Create', 'Project.Read.All']) {
  const principal = {
    userId: '50000000-0000-0000-0000-000000000001',
    developmentUserKey: 'dev-admin',
    displayName: 'Osan Admin',
    email: null,
    authProvider: 'Dev' as const,
    isActive: true,
    approvalPending: false,
    department: 'management-support',
    departmentName: '경영지원',
    profilePhotoVersion: null,
    roles: ['system-administrator']
  };
  return {
    ...principal,
    permissions,
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal,
    businessUnitAccess: {
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }
  };
}

function projectDetail(quantity = 2) {
  return {
    projectId,
    title: '  저장된 Title  '.trim(),
    projectCode: 'AbC  001',
    customerName: '거래처',
    poNumber: '001-PO/+',
    workOrderNumber: '000-W/O',
    deliveryDate: '2026-12-31',
    productName: '제품  이름',
    quantity,
    status: 'Active',
    createdAtUtc: '2026-09-07T00:00:00Z',
    targets: Array.from({ length: quantity }, (_, targetIndex) => ({
      targetId: `92000000-0000-0000-0000-${String(targetIndex + 1).padStart(12, '0')}`,
      sequenceNumber: targetIndex + 1,
      displayName: `제품  이름 ${targetIndex + 1}`,
      status: 'NotStarted',
      steps: ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'].map((stepName, stepIndex) => ({
        stepId: `${93000000 + targetIndex}-${String(stepIndex + 1).padStart(4, '0')}-0000-0000-000000000001`,
        sequenceNumber: stepIndex + 1,
        stepCode: `STEP_${stepIndex + 1}`,
        stepName,
        status: 'NotStarted'
      }))
    }))
  };
}

function shellFetch(handler?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    const handled = handler?.(url, init);
    if (handled) return handled;
    if (url.pathname === '/health/ready') return json({ status: 'ready', database: { reason: 'ready' } });
    if (url.pathname === '/api/runtime-mode') return json({
      mode: 'Development',
      reviewSafe: false,
      mutationAllowed: true,
      databaseReadOnly: false,
      ready: true,
      reason: 'development'
    });
    if (url.pathname === '/api/me') return json(currentUser());
    return json({ title: 'unexpected test request' }, 404);
  });
}

function fillCreateForm() {
  fireEvent.change(screen.getByLabelText(/^프로젝트 Title/), { target: { value: '  저장된 Title  ' } });
  fireEvent.change(screen.getByLabelText(/^프로젝트 코드/), { target: { value: ' AbC  001 ' } });
  fireEvent.change(screen.getByLabelText(/^거래처/), { target: { value: ' 거래처 ' } });
  fireEvent.change(screen.getByLabelText('PO No'), { target: { value: ' 001-PO/+ ' } });
  fireEvent.change(screen.getByLabelText('W/O No'), { target: { value: ' 000-W/O ' } });
  fireEvent.change(screen.getByLabelText(/^납기일/), { target: { value: '2026-12-31' } });
  fireEvent.change(screen.getByLabelText(/^제품명/), { target: { value: ' 제품  이름 ' } });
  fireEvent.change(screen.getByLabelText(/^수량/), { target: { value: '2' } });
}

describe('Osan project registration', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    resetBusinessUnitRequestContext(true);
    selectBusinessUnit('OSAN');
    setRuntimeMutationAllowed(false);
    window.history.replaceState(null, '', '/projects');
  });

  afterEach(() => {
    resetBusinessUnitRequestContext(true);
    setRuntimeMutationAllowed(false);
    vi.unstubAllGlobals();
  });

  it('renders each desktop project as one accessible table row', async () => {
    vi.stubGlobal('fetch', shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') {
        return json({ items: [projectDetail()] });
      }
      if (url.pathname === `/api/osan/projects/${projectId}`) return json(projectDetail());
      return undefined;
    }));

    render(<App />);

    const table = await screen.findByRole('table', { name: '오산 프로젝트 목록' });
    const sharedPage = table.closest('[data-presentation-contract="project-list-page-v1"]');
    expect(sharedPage).not.toBeNull();
    expect(sharedPage).toHaveAttribute('data-presentation-layout', 'desktop');
    expect(sharedPage).toHaveClass('page-surface', 'project-list-page');
    expect(table).toHaveClass('project-list-table', 'project-list-desktop');
    const sharedList = table.closest('[data-presentation-contract="project-list-v1"]');
    expect(sharedList).not.toBeNull();
    expect(sharedList).toHaveAttribute('data-presentation-layout', 'desktop');
    expect(sharedList).toHaveAttribute('data-presentation-column-count', '8');
    const rows = within(table).getAllByRole('row');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveClass('project-list-head');
    expect(within(rows[0]).getAllByRole('columnheader')).toHaveLength(8);
    expect(within(rows[0]).getByRole('columnheader', { name: '프로젝트명' })).toBeInTheDocument();
    expect(within(rows[0]).getByRole('columnheader', { name: '상태' })).toBeInTheDocument();
    expect(within(rows[0]).getByRole('columnheader', { name: '진행률' })).toBeInTheDocument();

    const projectRow = within(table).getByRole('row', { name: '저장된 Title 상세 열기' });
    expect(projectRow).toHaveClass('project-list-row');
    expect(within(projectRow).getAllByRole('cell')).toHaveLength(8);
    expect(projectRow).toHaveAttribute('data-presentation-row', 'project');
    const listCode = projectRow.querySelector('.project-code-value');
    expect(listCode).toHaveTextContent('AbC  001', { normalizeWhitespace: false });
    expect(listCode).toHaveClass('project-code-value');
    expect(within(projectRow).getByText('시작 전')).toBeInTheDocument();
    expect(within(projectRow).getByText('0%')).toBeInTheDocument();

    fireEvent.click(projectRow);
    expect(await screen.findByRole('heading', { name: '저장된 Title' })).toBeInTheDocument();
  });

  it('uses the shared page composition and filters loaded projects without Osan-only mutations', async () => {
    const completedProjectId = '91000000-0000-0000-0000-000000000002';
    const earlyProjectId = '91000000-0000-0000-0000-000000000003';
    const completedProject = {
      ...projectDetail(1),
      projectId: completedProjectId,
      title: '완료 검색명',
      projectCode: 'Done-Code',
      customerName: '두번째 거래처',
      productName: '완료 제품',
      deliveryDate: '2027-01-15',
      status: 'Completed'
    };
    const earlyProject = {
      ...projectDetail(1),
      projectId: earlyProjectId,
      title: '납기 이전 프로젝트',
      projectCode: 'Early-Code',
      customerName: '세번째 거래처',
      productName: '초기 제품',
      deliveryDate: '2026-06-30'
    };
    const fetchMock = shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') {
        return json({ items: [projectDetail(), completedProject, earlyProject] });
      }
      return undefined;
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const table = await screen.findByRole('table', { name: '오산 프로젝트 목록' });
    const page = table.closest('[data-presentation-contract="project-list-page-v1"]');
    expect(page).not.toBeNull();
    const directChildren = Array.from(page?.children ?? []);
    const directIndex = (selector: string) => directChildren.findIndex((element) => element.matches(selector));
    expect(directIndex('.page-header')).toBeLessThan(directIndex('form.toolbar'));
    expect(directIndex('form.toolbar')).toBeLessThan(directIndex('.project-kpi-grid'));
    expect(directIndex('.project-kpi-grid')).toBeLessThan(directIndex('.tab-row'));
    expect(directIndex('.tab-row')).toBeLessThan(directIndex('[data-presentation-contract="project-list-v1"]'));

    const summary = within(page as HTMLElement).getByLabelText('프로젝트 요약');
    const kpiCards = within(summary).getAllByRole('article');
    expect(kpiCards).toHaveLength(3);
    expect(kpiCards[0]).toHaveTextContent('전체 프로젝트3등록 프로젝트');
    expect(kpiCards[1]).toHaveTextContent('시작 전2진행 시작 전');
    expect(kpiCards[2]).toHaveTextContent('완료1전체 단계 완료');

    const pageQueries = within(page as HTMLElement);
    const statusTabs = pageQueries.getByRole('tablist', { name: '프로젝트 상태' });
    expect(within(statusTabs).getAllByRole('tab').map((item) => item.textContent)).toEqual(['전체', '시작 전', '완료']);
    expect(pageQueries.queryByRole('tab', { name: '보류' })).not.toBeInTheDocument();
    expect(pageQueries.queryByRole('tab', { name: '취소' })).not.toBeInTheDocument();
    expect(pageQueries.queryByRole('tab', { name: '삭제' })).not.toBeInTheDocument();
    expect(pageQueries.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(page).not.toHaveTextContent('Excel');
    expect(page).not.toHaveTextContent('Pending');
    expect(page).not.toHaveTextContent('병목');

    const searchInput = pageQueries.getByPlaceholderText('거래처, 제품명, 프로젝트 코드, 프로젝트 Title 검색');
    for (const query of ['완료 검색명', 'done-code', '두번째 거래처', '완료 제품']) {
      fireEvent.change(searchInput, { target: { value: query } });
      expect(within(table).getAllByRole('row')).toHaveLength(2);
      expect(within(table).getByText('완료 검색명')).toBeInTheDocument();
    }

    fireEvent.change(searchInput, { target: { value: '' } });
    fireEvent.change(pageQueries.getByLabelText('시작일'), { target: { value: '2027-01-01' } });
    fireEvent.change(pageQueries.getByLabelText('종료일'), { target: { value: '2027-12-31' } });
    expect(within(table).getAllByRole('row')).toHaveLength(2);
    expect(within(table).getByText('완료 검색명')).toBeInTheDocument();

    fireEvent.click(pageQueries.getByRole('button', { name: '필터 초기화' }));
    fireEvent.click(pageQueries.getByRole('tab', { name: '시작 전' }));
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(within(table).queryByText('완료 검색명')).not.toBeInTheDocument();

    fireEvent.change(searchInput, { target: { value: '일치하지 않는 검색어' } });
    expect(await screen.findByText('조건에 맞는 프로젝트가 없습니다.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '검색 조건 초기화' }));
    expect(await screen.findByRole('table', { name: '오산 프로젝트 목록' })).toBeInTheDocument();
    expect(pageQueries.getByRole('tab', { name: '전체' })).toHaveAttribute('aria-selected', 'true');
    expect(within(screen.getByRole('table', { name: '오산 프로젝트 목록' })).getAllByRole('row')).toHaveLength(4);

    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET'
    ))).toHaveLength(1);
  });

  it('lists projects and completes the exact eight-field create-to-detail flow once', async () => {
    let releaseCreate: ((response: Response) => void) | undefined;
    const pendingCreate = new Promise<Response>((resolve) => {
      releaseCreate = resolve;
    });
    const postedBodies: unknown[] = [];
    const fetchMock = shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') {
        return json({ items: [] });
      }
      if (url.pathname === '/api/osan/projects' && init?.method === 'POST') {
        postedBodies.push(JSON.parse(String(init.body)));
        return pendingCreate;
      }
      if (url.pathname === `/api/osan/projects/${projectId}`) {
        return json(projectDetail());
      }
      return undefined;
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    expect(await screen.findByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
    expect(await screen.findByText('등록된 프로젝트가 없습니다.')).toBeInTheDocument();
    const emptyCreateActions = screen.getAllByRole('button', { name: '신규 프로젝트' });
    expect(emptyCreateActions).toHaveLength(2);
    fireEvent.click(emptyCreateActions[1]);
    expect(await screen.findByRole('heading', { name: '프로젝트 등록' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/projects/create');

    expect(screen.getAllByRole('textbox')).toHaveLength(6);
    expect(screen.getAllByRole('spinbutton')).toHaveLength(1);
    fillCreateForm();
    const submit = screen.getByRole('button', { name: '프로젝트 등록' });
    fireEvent.click(submit);
    fireEvent.click(submit);
    expect(submit).toBeDisabled();
    await waitFor(() => expect(postedBodies).toHaveLength(1));
    expect(postedBodies[0]).toMatchObject({
      title: '저장된 Title',
      projectCode: 'AbC  001',
      customerName: '거래처',
      poNumber: '001-PO/+',
      workOrderNumber: '000-W/O',
      deliveryDate: '2026-12-31',
      productName: '제품  이름',
      quantity: 2
    });
    expect((postedBodies[0] as { operationId: string }).operationId).toMatch(/^[0-9a-f-]{36}$/i);

    releaseCreate?.(json({ operationId: (postedBodies[0] as { operationId: string }).operationId, replayed: false, project: projectDetail() }, 201));
    expect(await screen.findByRole('heading', { name: '저장된 Title' })).toBeInTheDocument();
    expect(window.location.pathname).toBe(`/projects/${projectId}`);
    expect(screen.getByText('001-PO/+')).toBeInTheDocument();
    expect(screen.getByText('000-W/O')).toBeInTheDocument();
    const sharedSummary = document.querySelector('[data-presentation-contract="project-summary-v1"]');
    expect(sharedSummary).toHaveAttribute('data-presentation-layout', 'desktop');
    const detailCode = document.querySelector('.project-summary-more dd.project-code-value');
    expect(detailCode).toHaveTextContent('AbC  001', { normalizeWhitespace: false });
    expect(detailCode).toHaveClass('project-code-value');
    expect(document.querySelector('.project-summary-primary .status-badge')).toHaveTextContent('시작 전');
    expect(document.querySelector('.project-summary-compact')).not.toBeNull();
    const tablist = screen.getByRole('tablist', { name: '프로젝트 상세 섹션' });
    const tabs = within(tablist).getAllByRole('tab');
    expect(tabs).toHaveLength(1);
    expect(tabs[0]).toHaveTextContent('진행 관리');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
    expect(tabs[0]).toHaveAttribute('aria-controls', 'osan-progress-panel');
    const progressPanel = screen.getByRole('tabpanel', { name: '진행 관리' });
    expect(progressPanel).toHaveAttribute('id', 'osan-progress-panel');
    expect(progressPanel).toHaveClass('project-detail-tab-content');
    const sharedStatusBoard = progressPanel.querySelector('[data-presentation-contract="project-status-board-v1"]');
    expect(sharedStatusBoard).toHaveAttribute('data-department', 'manufacturing');
    expect(sharedStatusBoard).toHaveAttribute('data-presentation-layout', 'desktop');
    expect(progressPanel.querySelector('.project-department-metrics')).not.toBeNull();
    const targetTable = within(progressPanel).getByRole('table', { name: '진행 관리 대상 현황' });
    expect(targetTable).toHaveClass('project-panel-status-table');
    const targetRows = within(targetTable).getAllByRole('row');
    expect(targetRows).toHaveLength(3);
    expect(within(targetRows[0]).getAllByRole('columnheader')).toHaveLength(5);
    expect(targetTable.querySelector('button')).not.toBeInTheDocument();
    for (const targetRow of targetRows.slice(1)) {
      expect(targetRow).toHaveClass('project-panel-status-row');
      expect(targetRow.tagName).toBe('DIV');
      expect(targetRow).toHaveAttribute('data-interactive', 'false');
      expect(targetRow).not.toHaveAttribute('tabindex');
      expect(within(targetRow).getAllByRole('cell')).toHaveLength(5);
      expect(targetRow).toHaveTextContent('시작 전');
      expect(targetRow).toHaveTextContent('0/7단계 완료');
      expect(targetRow).toHaveTextContent('입고검사');
      expect(targetRow.querySelector('.project-progress-meter')?.getAttribute('aria-label')).toMatch(/진행률 0% \(0\/7\)$/);
    }
    expect(progressPanel.querySelector('.project-department-records')).toBeNull();
    for (const forbiddenAction of ['Pending', '보류', '중단', '취소']) {
      expect(screen.queryByRole('button', { name: forbiddenAction })).not.toBeInTheDocument();
    }
  });

  it('keeps input after a server conflict and reuses the operation id on retry', async () => {
    const posts: Array<{ body: Record<string, unknown> }> = [];
    const fetchMock = shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') return json({ items: [] });
      if (url.pathname === '/api/osan/projects' && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Record<string, unknown>;
        posts.push({ body });
        return posts.length === 1
          ? json({
              errorCode: 'osan_project_code_conflict',
              message: '이미 등록된 프로젝트 코드입니다.',
              errors: { ProjectCode: ['이미 등록된 프로젝트 코드입니다.'] }
            }, 409)
          : json({ operationId: body.operationId, replayed: false, project: projectDetail() }, 201);
      }
      if (url.pathname === `/api/osan/projects/${projectId}`) return json(projectDetail());
      return undefined;
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);
    await screen.findByRole('heading', { name: '프로젝트 목록' });
    fireEvent.click(screen.getAllByRole('button', { name: '신규 프로젝트' })[0]);
    fillCreateForm();
    fireEvent.click(screen.getByRole('button', { name: '프로젝트 등록' }));

    expect(await screen.findByText('이미 등록된 프로젝트 코드입니다.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^프로젝트 코드/)).toHaveValue(' AbC  001 ');
    fireEvent.change(screen.getByLabelText(/^프로젝트 코드/), { target: { value: ' AbC  002 ' } });
    fireEvent.click(screen.getByRole('button', { name: '프로젝트 등록' }));
    expect(await screen.findByRole('heading', { name: '저장된 Title' })).toBeInTheDocument();
    expect(posts).toHaveLength(2);
    expect(posts[1].body.operationId).toBe(posts[0].body.operationId);
  });

  it('shows an operation conflict and uses a new operation id on retry', async () => {
    const posts: Array<Record<string, unknown>> = [];
    vi.stubGlobal('fetch', shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') return json({ items: [] });
      if (url.pathname === '/api/osan/projects' && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Record<string, unknown>;
        posts.push(body);
        return posts.length === 1
          ? json({
              errorCode: 'osan_project_operation_conflict',
              message: '같은 요청 식별자가 다른 입력에 사용되었습니다.',
              errors: { OperationId: ['새 요청으로 다시 시도해 주세요.'] }
            }, 409)
          : json({ operationId: body.operationId, replayed: false, project: projectDetail() }, 201);
      }
      if (url.pathname === `/api/osan/projects/${projectId}`) return json(projectDetail());
      return undefined;
    }));

    render(<App />);
    await screen.findByRole('heading', { name: '프로젝트 목록' });
    fireEvent.click(screen.getAllByRole('button', { name: '신규 프로젝트' })[0]);
    fillCreateForm();
    fireEvent.click(screen.getByRole('button', { name: '프로젝트 등록' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('같은 요청 식별자가 다른 입력에 사용되었습니다.');
    expect(screen.getByLabelText(/^프로젝트 Title/)).toHaveValue('  저장된 Title  ');
    fireEvent.click(screen.getByRole('button', { name: '프로젝트 등록' }));

    expect(await screen.findByRole('heading', { name: '저장된 Title' })).toBeInTheDocument();
    expect(posts).toHaveLength(2);
    expect(posts[1].operationId).not.toBe(posts[0].operationId);
  });

  it('shows client validation and does not send decimals or out-of-range quantities', async () => {
    const fetchMock = shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') return json({ items: [] });
      return undefined;
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);
    await screen.findByRole('heading', { name: '프로젝트 목록' });
    fireEvent.click(screen.getAllByRole('button', { name: '신규 프로젝트' })[0]);
    fillCreateForm();

    for (const quantity of ['1.5', '0', '-1', '501']) {
      fireEvent.change(screen.getByLabelText(/^수량/), { target: { value: quantity } });
      fireEvent.click(screen.getByRole('button', { name: '프로젝트 등록' }));
      expect(await screen.findByText('수량은 1 이상 500 이하의 정수로 입력해 주세요.')).toBeInTheDocument();
    }
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname === '/api/osan/projects' && init?.method === 'POST'
    ))).toHaveLength(0);
  });

  it('renders forbidden list/create states and keeps create hidden without permission', async () => {
    vi.stubGlobal('fetch', shellFetch((url) => {
      if (url.pathname === '/api/me') return json(currentUser(['projects.read']));
      if (url.pathname === '/api/osan/projects') {
        return json({ errorCode: 'forbidden', message: '목록 권한이 없습니다.' }, 403);
      }
      return undefined;
    }));

    render(<App />);
    expect(await screen.findByText('프로젝트를 볼 권한이 없습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument();
  });

  it('requires projects.read as well as Project.Create for the button and direct create route', async () => {
    const fetchMock = shellFetch((url) => {
      if (url.pathname === '/api/me') return json(currentUser(['Project.Create']));
      if (url.pathname === '/api/osan/projects') {
        return json({ errorCode: 'forbidden', message: '목록 권한이 없습니다.' }, 403);
      }
      return undefined;
    });
    vi.stubGlobal('fetch', fetchMock);

    const listRender = render(<App />);
    expect(await screen.findByText('프로젝트를 볼 권한이 없습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument();
    listRender.unmount();

    window.history.replaceState(null, '', '/projects/create');
    render(<App />);
    expect(await screen.findByText('프로젝트를 등록할 수 없습니다.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname === '/api/osan/projects' && init?.method === 'POST'
    ))).toHaveLength(0);
  });

  it('renders loading and recoverable error states before an empty list', async () => {
    let releaseFirstList: ((response: Response) => void) | undefined;
    const firstList = new Promise<Response>((resolve) => {
      releaseFirstList = resolve;
    });
    let listCalls = 0;
    vi.stubGlobal('fetch', shellFetch((url, init) => {
      if (url.pathname === '/api/osan/projects' && (init?.method ?? 'GET') === 'GET') {
        listCalls += 1;
        return listCalls === 1 ? firstList : json({ items: [] });
      }
      return undefined;
    }));

    render(<App />);
    expect(await screen.findByText('프로젝트를 불러오는 중입니다.')).toBeInTheDocument();
    releaseFirstList?.(json({ message: '일시적으로 불러오지 못했습니다.' }, 500));
    expect(await screen.findByText('프로젝트를 불러오지 못했습니다.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
    expect(await screen.findByText('등록된 프로젝트가 없습니다.')).toBeInTheDocument();
    expect(listCalls).toBe(2);
  });
});
