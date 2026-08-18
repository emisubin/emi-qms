import { StrictMode } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const teamsJsMock = vi.hoisted(() => ({
  context: null as unknown,
  initialize: vi.fn(async () => undefined),
  getContext: vi.fn(async () => ({}))
}));

vi.mock('@microsoft/teams-js', () => ({
  app: {
    initialize: teamsJsMock.initialize,
    getContext: teamsJsMock.getContext
  }
}));

import { App } from '../src/App';
import { HomePage } from '../src/HomePage';

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const projectId = '71000000-0000-0000-0000-000000000010';
const noticeId = '81000000-0000-0000-0000-000000000001';
const onHoldProjectId = '71000000-0000-0000-0000-000000000011';
const cancelledProjectId = '71000000-0000-0000-0000-000000000012';
const panelIds = [
  '72000000-0000-0000-0000-000000000001',
  '72000000-0000-0000-0000-000000000002',
  '72000000-0000-0000-0000-000000000003',
  '72000000-0000-0000-0000-000000000004'
];
let adminUserDeletionScheduled = false;
let adminDepartmentDeletionScheduled = false;
let adminHolidayDeletionScheduled = false;
let manufacturingReleasedPanelIds = new Set<string>();
let useSetScopedProductionPlan = false;

describe('App', () => {
  beforeEach(() => {
    adminUserDeletionScheduled = false;
    adminDepartmentDeletionScheduled = false;
    adminHolidayDeletionScheduled = false;
    manufacturingReleasedPanelIds = new Set<string>();
    useSetScopedProductionPlan = false;
    teamsJsMock.context = null;
    teamsJsMock.initialize.mockClear();
    teamsJsMock.getContext.mockClear();
    teamsJsMock.initialize.mockResolvedValue(undefined);
    teamsJsMock.getContext.mockImplementation(async () => teamsJsMock.context ?? {});
    window.localStorage.clear();
    window.history.pushState(null, '', '/projects');
    Object.defineProperty(document, 'referrer', { value: '', configurable: true });
    vi.stubGlobal('fetch', vi.fn(mockFetch));
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:panel-template');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  it('uses Home for / and /home while keeping the project list at /projects', async () => {
    window.history.pushState(null, '', '/');
    const { unmount } = render(<App />);

    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(screen.getByAltText('EMI PMS - Project Management System')).toBeInTheDocument();
    const companyInformation = screen.getByLabelText('회사 정보');
    expect(within(companyInformation).getByText('(주) 이엠아이')).toBeInTheDocument();
    expect(within(companyInformation).getByText('경기도 오산시 세남로길 14-11 (세교동 63-1)')).toBeInTheDocument();
    expect(within(companyInformation).getByText('이엠아이 청주캠퍼스 / 충북 청주시 청원구 오창읍 서오창산단3로 110')).toBeInTheDocument();
    const privacyNoticeEntry = within(companyInformation).getByRole('button', { name: '개인정보·이용 안내' });
    fireEvent.click(privacyNoticeEntry);
    expect(await screen.findByRole('heading', { name: '개인정보·이용 안내' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/privacy-notice');
    expect(screen.getAllByText('사내 규정에 따름')).toHaveLength(3);
    expect(screen.getByRole('button', { name: '개인정보·이용 안내' })).not.toHaveAttribute('aria-current');
    fireEvent.click(screen.getByRole('button', { name: '홈으로' }));
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '내 업무' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '공지사항' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '알림' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Pending' })).toBeInTheDocument();

    const navigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    expect(within(navigation).getByRole('button', { name: '홈' })).toHaveClass('active');
    fireEvent.click(within(navigation).getByRole('button', { name: '프로젝트' }));
    expect(await screen.findByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/projects');
    fireEvent.click(within(navigation).getByRole('button', { name: 'EMI PMS 로고로 홈 이동' }));
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/');
    fireEvent.click(within(navigation).getByRole('button', { name: '프로젝트' }));
    expect(await screen.findByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
    fireEvent.click(within(navigation).getByRole('button', { name: '홈' }));
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/');

    const desktopNavigation = screen.getByRole('navigation', { name: '공통 메뉴' });
    fireEvent.click(within(desktopNavigation).getByRole('button', { name: '프로젝트' }));
    expect(await screen.findByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
    act(() => {
      window.history.pushState(null, '', '/home');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();

    unmount();
    window.history.pushState(null, '', '/home');
    render(<App />);
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
  });

  it('canonicalizes a trailing slash and opens the Pending project dashboard', async () => {
    window.history.pushState(null, '', '/pending/');

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Pending 프로젝트' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/pending');
  });

  it('loads the exact project metadata for an empty project-scoped Pending route', async () => {
    window.history.pushState(null, '', `/pending?projectId=${projectId}`);
    const exactProjectTitle = '목록 밖 프로젝트 Pending';
    const pendingProjectIds: Array<string | null> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/pending') {
        pendingProjectIds.push(url.searchParams.get('projectId'));
      }
      if (url.pathname === '/api/projects') {
        return json({ items: [], page: 1, pageSize: 100, totalCount: 101 });
      }
      if (url.pathname === `/api/projects/${projectId}`) {
        return json(projectDetail(true, 'Active', exactProjectTitle));
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: exactProjectTitle })).toBeInTheDocument();
    expect(screen.getByText(/PJT-003A · 이 프로젝트의 등록·조치·재검사·종결 이력만 표시합니다/)).toBeInTheDocument();
    expect(screen.getByText('조건에 맞는 Pending이 없습니다.')).toBeInTheDocument();
    expect(pendingProjectIds.length).toBeGreaterThan(0);
    expect(pendingProjectIds.every((candidate) => candidate === projectId)).toBe(true);
  });

  it('keeps a project-scoped Pending route fail-closed until project metadata retry succeeds', async () => {
    window.history.pushState(null, '', `/pending?projectId=${projectId}`);
    let projectRequestCount = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}`) {
        projectRequestCount += 1;
        return projectRequestCount === 1
          ? json({ title: '프로젝트 정보를 불러오지 못했습니다.' }, 503)
          : json(projectDetail(true, 'Active', '재시도 프로젝트 Pending'));
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Pending을 불러오지 못했습니다.');
    expect(screen.queryByRole('heading', { name: 'Pending 프로젝트' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
    expect(await screen.findByRole('heading', { name: '재시도 프로젝트 Pending' })).toBeInTheDocument();
    expect(projectRequestCount).toBe(2);
  });

  it('keeps Pending detail visible when an older backend omits action evidence', async () => {
    const pendingId = '88000000-0000-0000-0000-000000000099';
    window.history.pushState(null, '', `/pending/${pendingId}`);
    mockMobileViewport(true);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === `/api/pending/${pendingId}`) {
        return json({
          issue: {
            pendingId,
            issueNumber: 99,
            projectId,
            projectCode: 'PJT-003A',
            projectTitle: 'TASK-003A Demo',
            targetType: 'Project',
            targetId: projectId,
            targetLabel: null,
            issueType: 'Quality',
            issueTypeLabel: '품질',
            title: '계약 전환 중 Pending 상세',
            description: '증거 응답이 없어도 핵심 상세는 유지되어야 합니다.',
            status: 'Registered',
            statusLabel: '등록',
            priority: 'Normal',
            priorityLabel: '일반',
            actionDepartmentCode: 'production-planning',
            assigneeUserId: null,
            assigneeDisplayName: null,
            dueDate: null,
            isOverdue: false,
            version: 1,
            createdByUserId: salesOwnerId,
            createdByDisplayName: 'dev-sales',
            createdAtUtc: '2026-08-01T00:00:00Z',
            updatedAtUtc: '2026-08-01T00:00:00Z'
          },
          comments: [],
          history: [],
          allowedTransitions: [],
          canComment: false,
          canAssign: false,
          reinspection: null
        });
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '계약 전환 중 Pending 상세' })).toBeInTheDocument();
    expect(screen.getByText('증거 응답이 없어도 핵심 상세는 유지되어야 합니다.')).toBeInTheDocument();
    expect(screen.getByText(/조치 사진 정보를 확인할 수 없습니다/)).toBeInTheDocument();
    expect(document.querySelector('.mobile-pending-detail-page')).not.toBeNull();
  });

  it('shows the actual user account in the shell and department metrics on Home', async () => {
    window.history.pushState(null, '', '/');
    render(<App />);

    expect(await screen.findByRole('heading', { name: '연간 매출 성과' })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: '월별 확정 매출과 목표 막대, 달성률 선 비교' })).toBeInTheDocument();

    const accountTrigger = document.querySelector<HTMLButtonElement>('.account-identity-trigger');
    expect(accountTrigger).not.toBeNull();
    expect(accountTrigger).toHaveTextContent('영업');
    expect(accountTrigger).toHaveTextContent('dev-sales');
    fireEvent.click(accountTrigger!);

    const accountDialog = await screen.findByRole('dialog', { name: '내 계정' });
    expect(accountDialog.querySelector('input[type="file"]')).toHaveAttribute('hidden');
    const changePhotoButton = within(accountDialog).getByRole('button', { name: '사진 변경' });
    expect(changePhotoButton).toBeInTheDocument();
    expect(within(accountDialog).getByRole('button', { name: '사진 제거' })).toBeDisabled();
    expect(within(accountDialog).queryByRole('button', { name: '개인정보·이용 안내' })).not.toBeInTheDocument();
    expect(within(accountDialog).getByRole('button', { name: '로그아웃' })).toBeInTheDocument();

    fireEvent.click(changePhotoButton);
    const consentDialog = await screen.findByRole('dialog', { name: '프로필 사진 선택 동의' });
    expect(within(consentDialog).getByText(/동의하지 않아도 기본 이니셜로/)).toBeInTheDocument();
    fireEvent.click(within(consentDialog).getByRole('button', { name: '취소' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '프로필 사진 선택 동의' })).not.toBeInTheDocument());

    const commonNavigation = screen.getByRole('navigation', { name: '공통 메뉴' });
    for (const label of ['홈', '내 업무', '프로젝트', 'Pending', '생산관리', '구매', '자재', '제조', '품질', '물류', '영업', '알림']) {
      expect(within(commonNavigation).getByRole('button', { name: label })).toBeInTheDocument();
    }
    expect(within(commonNavigation).queryByRole('button', { name: '관리자' })).not.toBeInTheDocument();
    const sidebarFooter = commonNavigation.querySelector('.app-sidebar-footer');
    expect(sidebarFooter).not.toBeNull();
    expect(within(sidebarFooter as HTMLElement).getByLabelText('개발 사용자')).toBeInTheDocument();

    const topbarActions = document.querySelector('.topbar-actions');
    expect(topbarActions).not.toBeNull();
    expect(within(topbarActions as HTMLElement).queryByRole('button', { name: '자재' })).not.toBeInTheDocument();
  });

  it('shows form management to quality and production department heads but not manufacturing heads', async () => {
    window.history.pushState(null, '', '/');
    render(<App />);

    const userSelector = await screen.findByLabelText('개발 사용자');
    const navigation = screen.getByRole('navigation', { name: '공통 메뉴' });
    expect(within(navigation).queryByRole('button', { name: '양식 관리' })).not.toBeInTheDocument();

    fireEvent.change(userSelector, { target: { value: 'dev-quality' } });
    await waitFor(() => expect(userSelector).toHaveValue('dev-quality'));
    expect(await within(navigation).findByRole('button', { name: '양식 관리' })).toBeInTheDocument();

    fireEvent.change(userSelector, { target: { value: 'dev-manufacturing' } });
    await waitFor(() => expect(userSelector).toHaveValue('dev-manufacturing'));
    await waitFor(() => expect(within(navigation).queryByRole('button', { name: '양식 관리' })).not.toBeInTheDocument());

    fireEvent.change(userSelector, { target: { value: 'dev-production' } });
    await waitFor(() => expect(userSelector).toHaveValue('dev-production'));
    expect(await within(navigation).findByRole('button', { name: '양식 관리' })).toBeInTheDocument();
  });

  it('opens the customer-supplied overdue material queue from the Materials Home metric', async () => {
    window.history.pushState(null, '', '/');
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    const metric = await screen.findByRole('button', { name: /사급 제공 지연/ });
    expect(metric).toHaveTextContent('1');
    fireEvent.click(metric);

    expect(await screen.findByRole('heading', { name: '자재 입고 관리' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/materials/receipts');
    expect(new URLSearchParams(window.location.search).get('risk')).toBe('customer-supply-overdue');
    expect(screen.getByRole('button', { name: '제공 지연' })).toHaveAttribute('data-active', 'true');
  });

  it('loads the notice board independently while keeping Pending discoverable', async () => {
    window.history.pushState(null, '', '/');
    const calls: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const requestUrl = new URL(String(input));
      calls.push(requestUrl.pathname);
      return mockFetch(input, init);
    }));

    render(<App />);

    await screen.findByLabelText('공지사항 요약');
    expect(screen.getByRole('heading', { name: 'Pending' })).toBeInTheDocument();
    await waitFor(() => expect(calls).toContain('/api/notices'));
    expect(calls).toContain('/api/pending');
  });

  it('keeps an attempted forbidden widget visible as an error instead of hiding it', async () => {
    window.history.pushState(null, '', '/');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/my-work/summary') {
        return json({ title: 'forbidden' }, 403);
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    const widget = await screen.findByLabelText('내 업무 요약');
    expect(await within(widget).findByRole('alert')).toHaveTextContent('현재 권한으로 이 요약을 확인할 수 없습니다.');
    expect(within(widget).getByRole('button', { name: '다시 시도' })).toBeInTheDocument();
  });

  it('ignores a delayed Home response after the effective request context changes', async () => {
    let myWorkCallCount = 0;
    let resolveOldResponse: ((response: Response) => void) | undefined;
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/my-work/summary') {
        myWorkCallCount += 1;
        if (myWorkCallCount === 1) {
          return new Promise<Response>((resolve) => {
            resolveOldResponse = resolve;
          });
        }
        return Promise.resolve(json({
          requestedCount: 8,
          inProgressCount: 0,
          completedCount: 0,
          blockingCount: 0,
          assignedProjectCount: 0,
          assignedProjectBreakdown: []
        }));
      }
      return mockFetch(input, init);
    }));

    const callbacks = {
      onOpenMyWork: vi.fn(),
      onOpenNotices: vi.fn(),
      onOpenNotice: vi.fn(),
      onCreateNotice: vi.fn(),
      onOpenPending: vi.fn(),
      onOpenNotifications: vi.fn()
    };
    const { rerender } = render(
      <HomePage
        developmentUserKey="dev-sales"
        requestContextKey="effective-user-one"
        canReadPending={false}
        {...callbacks}
      />
    );
    await waitFor(() => expect(resolveOldResponse).toBeDefined());

    rerender(
      <HomePage
        developmentUserKey="dev-sales"
        requestContextKey="effective-user-two"
        canReadPending={false}
        {...callbacks}
      />
    );
    const widget = await screen.findByLabelText('내 업무 요약');
    expect(await within(widget).findByText('8')).toBeInTheDocument();

    await act(async () => {
      resolveOldResponse?.(json({
        requestedCount: 99,
        inProgressCount: 0,
        completedCount: 0,
        blockingCount: 0,
        assignedProjectCount: 0,
        assignedProjectBreakdown: []
      }));
      await Promise.resolve();
    });
    expect(within(widget).getByText('8')).toBeInTheDocument();
    expect(within(widget).queryByText('99')).not.toBeInTheDocument();
  });

  it('keeps healthy Home widgets usable while one widget fails and retries independently', async () => {
    let notificationAttempts = 0;
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/notifications/summary') {
        notificationAttempts += 1;
        return Promise.resolve(notificationAttempts === 1
          ? json({ title: 'temporarily unavailable' }, 503)
          : json({ unreadCount: 3, blockingCount: 1 }));
      }
      return mockFetch(input, init);
    }));
    const callbacks = {
      onOpenMyWork: vi.fn(),
      onOpenNotices: vi.fn(),
      onOpenNotice: vi.fn(),
      onCreateNotice: vi.fn(),
      onOpenPending: vi.fn(),
      onOpenNotifications: vi.fn()
    };

    render(
      <HomePage
        developmentUserKey="dev-sales"
        requestContextKey="effective-user"
        canReadPending={false}
        {...callbacks}
      />
    );

    const myWorkWidget = await screen.findByLabelText('내 업무 요약');
    expect((await within(myWorkWidget).findAllByText('1')).length).toBeGreaterThan(0);
    const notificationWidget = await screen.findByLabelText('알림 요약');
    expect(await within(notificationWidget).findByRole('alert')).toHaveTextContent('알림 요약을 불러올 수 없습니다.');
    fireEvent.click(within(notificationWidget).getByRole('button', { name: '다시 시도' }));
    expect(await within(notificationWidget).findByText('3')).toBeInTheDocument();
    expect(within(notificationWidget).getByText('1')).toBeInTheDocument();
    expect(notificationAttempts).toBe(2);
  });

  it('shows a failed mobile priority source next to its summary value', async () => {
    window.history.pushState(null, '', '/');
    mockMobileViewport(true);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/notifications/summary') {
        return json({ title: 'temporarily unavailable' }, 503);
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    const priority = await screen.findByLabelText('긴급·차단 우선 확인');
    expect(await within(priority).findByText('알림 요약 오류 · 아래에서 재시도')).toBeInTheDocument();
  });

  afterEach(() => {
    window.history.pushState(null, '', '/');
    Object.defineProperty(window, 'matchMedia', { writable: true, value: undefined });
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows project registration actions for Sales users', async () => {
    render(<App />);

    expect(await screen.findByRole('button', { name: '신규 프로젝트' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '프로젝트 Excel 양식' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '프로젝트 Excel 업로드' }));
    const dialog = await screen.findByRole('dialog', { name: '프로젝트 Excel 업로드' });
    const fileInput = dialog.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [new File(['xlsx'], 'projects.xlsx')] } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Preview' }));
    expect(await within(dialog).findByText('신규 1건')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Excel 저장' })).toBeEnabled();
    expect(screen.getAllByText('KRW 1,250,000.5').length).toBeGreaterThan(0);
  });

  it('shows review-safe mode and disables mutation actions while keeping navigation available', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/runtime-mode') {
        return json({
          mode: 'ReviewSafe',
          reviewSafe: true,
          mutationAllowed: false,
          backgroundWorkersEnabled: false,
          externalProvidersEnabled: false,
          databaseReadOnly: true,
          migrationExecutionEnabled: false,
          environment: 'Development',
          ready: true,
          reason: 'ready',
          expectedMigration: '0027_notification_access_scope_and_manual_work_items',
          actualMigration: '0027_notification_access_scope_and_manual_work_items',
          migrationLedgerStatus: 'CompatibleWithApprovedLegacy',
          expectedMigrationCount: 27,
          actualMigrationCount: 28,
          missingMigrations: [],
          unexpectedMigrations: [],
          approvedLegacyMigrations: ['0020_teams_activity_delivery_channel'],
          migrationSchemaCompatible: true,
          migrationLedgerReady: true
        });
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByText(/검수 전용 읽기 모드/)).toBeInTheDocument();
    expect(screen.getByText(/Canonical 27개, Live 28개/)).toBeInTheDocument();
    expect(screen.getByText(/승인된 과거 marker 1건/)).toBeInTheDocument();
    const createButton = await screen.findByRole('button', { name: '신규 프로젝트' });
    await waitFor(() => expect(createButton).toBeDisabled());
    expect(createButton).toHaveAttribute('title', '검수 전용 읽기 모드에서는 변경 작업을 수행할 수 없습니다.');
    expect(screen.getAllByRole('button', { name: '프로젝트' }).some((button) => !button.hasAttribute('disabled'))).toBe(true);
    expect(screen.getByRole('tab', { name: '진행' })).toBeEnabled();
  });

  it('fails closed when runtime mode cannot be loaded', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/runtime-mode') {
        return new Response(null, { status: 503 });
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByText(/실행 모드를 확인할 수 없어 변경 작업을 차단했습니다/)).toBeInTheDocument();
    const createButton = await screen.findByRole('button', { name: '신규 프로젝트' });
    await waitFor(() => expect(createButton).toBeDisabled());
  });

  it('shows all project tabs by default with a sticky desktop header and workflow progress', async () => {
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    expect(commonNavigation).toHaveTextContent('프로젝트');
    expect(commonNavigation).toHaveTextContent('구매');
    expect(screen.getAllByRole('button', { name: '프로젝트' }).some((button) => button.getAttribute('aria-current') === 'page')).toBe(true);
    const projectSummary = await screen.findByLabelText('프로젝트 요약');
    expect(projectSummary).toHaveTextContent('전체 프로젝트');
    expect(projectSummary).not.toHaveTextContent('QR 가능 패널');
    expect(projectSummary).toHaveTextContent('제조 완료 프로젝트');
    expect(projectSummary).toHaveTextContent('검사 완료 프로젝트');

    const tabs = await screen.findAllByRole('tab');
    expect(tabs.slice(0, 5).map((tab) => tab.textContent)).toEqual(['전체', '진행', '보류', '완료', '취소']);
    expect(screen.getByRole('tab', { name: '전체' })).toHaveAttribute('aria-selected', 'true');

    const table = await screen.findByRole('table', { name: '프로젝트 목록' });
    const header = table.querySelector('.project-list-head');
    expect(header).not.toBeNull();
    expect(header).toHaveTextContent('프로젝트명고객사CodeItem면수납기일상태진행률');
    expect(header).toHaveClass('project-list-head');
    expect(within(table).getByText('TASK-003A Demo')).toBeInTheDocument();
    expect(within(table).getByText('OnHold Project')).toBeInTheDocument();
    expect(within(table).getByText('Completed Project')).toBeInTheDocument();
    expect(within(table).getByText('Cancelled Project')).toBeInTheDocument();
    expect(within(table).getByText('생산관리')).toBeInTheDocument();
    expect(within(table).getByText('6%')).toBeInTheDocument();
    expect(table).not.toHaveTextContent('BeforeManufacturing');
    expect(table).not.toHaveTextContent('0/4');

    fireEvent.click(screen.getByRole('tab', { name: '진행' }));
    await waitFor(() => expect(screen.queryByText('OnHold Project')).not.toBeInTheDocument());
    expect(screen.getByText('TASK-003A Demo')).toBeInTheDocument();
  });

  it('selects visible projects without opening rows and exports only the selection snapshot', async () => {
    render(<App />);

    const table = await screen.findByRole('table', { name: '프로젝트 목록' });
    const exportButton = screen.getByRole('button', { name: '선택 Excel 내보내기' });
    expect(exportButton).toBeDisabled();
    expect(screen.getByText('0개 선택')).toBeInTheDocument();

    const firstSelection = within(table).getByRole('checkbox', { name: 'PJT-003A TASK-003A Demo 선택' });
    const secondSelection = within(table).getByRole('checkbox', { name: 'PJT-003A OnHold Project 선택' });
    fireEvent.click(firstSelection);
    fireEvent.click(secondSelection);

    expect(screen.getByText('2개 선택')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
    expect(exportButton).not.toBeDisabled();
    const selectionTray = screen.getByLabelText('선택 프로젝트 내보내기');
    expect(within(selectionTray).getByRole('checkbox', { name: '현재 목록 전체 선택' })).toBePartiallyChecked();

    fireEvent.click(exportButton);
    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/data-exports/selected'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          screen: 'projects',
          ids: [projectId, onHoldProjectId],
          filters: {
            search: '',
            deliveryDateFrom: '',
            deliveryDateTo: ''
          }
        })
      })
    ));
    expect(await screen.findByText('Excel 파일 생성을 완료했습니다')).toBeInTheDocument();
    expect(screen.getByText('2개 선택')).toBeInTheDocument();
    expect(firstSelection).toBeChecked();
    expect(secondSelection).toBeChecked();

    fireEvent.click(screen.getByRole('button', { name: '선택 해제' }));
    expect(screen.getByText('0개 선택')).toBeInTheDocument();
    expect(exportButton).toBeDisabled();
  });

  it('shows my work and notification pages from the common menu', async () => {
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    await waitFor(() => expect(within(commonNavigation).getAllByText('1').length).toBeGreaterThan(0));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '내 업무' }));
    expect(await screen.findByRole('heading', { name: '내 업무' })).toBeInTheDocument();
    const myWorkTabs = screen.getByRole('tablist', { name: '내 업무 상태' });
    expect(within(myWorkTabs).getByRole('button', { name: '전체' })).toBeInTheDocument();
    expect(within(myWorkTabs).getByRole('button', { name: '시작 전' })).toHaveAttribute('aria-selected', 'true');
    expect(within(myWorkTabs).getByRole('button', { name: '진행 중' })).toBeInTheDocument();
    expect(within(myWorkTabs).getByRole('button', { name: '담당 프로젝트' })).toBeInTheDocument();
    expect(screen.getAllByText('담당 프로젝트').length).toBeGreaterThan(0);
    expect(screen.queryByText('담당 프로젝트 구분')).not.toBeInTheDocument();
    expect(screen.getByText('생산계획, 담당자 입력')).toBeInTheDocument();
    expect(screen.getAllByText('시작 전').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: '시작' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '작업 완료' }).length).toBeGreaterThan(0);

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '알림' }));
    expect(await screen.findByRole('heading', { name: '알림' })).toBeInTheDocument();
    const notificationTabs = screen.getByRole('tablist', { name: '알림 읽음 상태' });
    expect(within(notificationTabs).getByRole('button', { name: '전체' })).toBeInTheDocument();
    expect(within(notificationTabs).getByRole('button', { name: '읽지 않음' })).toHaveAttribute('aria-selected', 'true');
    expect(within(notificationTabs).getByRole('button', { name: '읽음' })).toBeInTheDocument();
    expect(screen.getByText('프로젝트가 생성되었습니다.')).toBeInTheDocument();
    expect(screen.getAllByText('읽지 않음').length).toBeGreaterThan(0);
  });

  it('keeps titles in their original column and renders operational copy in the rightmost detail column', async () => {
    const pendingId = '88000000-0000-0000-0000-000000000001';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/my-work') {
        return Promise.resolve(json({
          items: [
            {
              workItemId: '76000000-0000-0000-0000-000000000081',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              projectDeliveryDate: '2026-07-01',
              workflowStageCode: 'MaterialArrived',
              workflowStageName: '자재 도착',
              responsibilityType: 'MaterialsPrimary',
              responsibilityLabel: '자재 정담당자',
              title: '구매품 변경 확인 · 부스바',
              description: 'PJT-003A 구매품 변경 내용을 확인하고 입고 계획에 반영해 주세요.\n\n상세 내용\n입고예정일 변경 7/22 → 7/23\n이슈사항 변경 - → 하루 늦게 들어오기로 함 /materials/receipts?project=PJT-003A',
              status: 'Requested',
              statusLabel: '시작 전',
              priority: 'Normal',
              priorityLabel: '일반',
              dueDate: null,
              createdAtUtc: '2026-07-23T00:00:00Z',
              startedAtUtc: null,
              completedAtUtc: null,
              linkUrl: '/materials/receipts?project=PJT-003A'
            },
            {
              workItemId: '76000000-0000-0000-0000-000000000082',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              projectDeliveryDate: '2026-07-01',
              workflowStageCode: 'IQC',
              workflowStageName: '수입검사',
              responsibilityType: 'PendingAction',
              responsibilityLabel: 'Pending 조치',
              title: 'Pending 조치 · 부스바 찍힘',
              description: 'IQC 부적합: 표면 찍힘 사진 확인 필요',
              status: 'Requested',
              statusLabel: '시작 전',
              priority: 'Blocking',
              priorityLabel: '긴급',
              dueDate: null,
              createdAtUtc: '2026-07-23T00:01:00Z',
              startedAtUtc: null,
              completedAtUtc: null,
              linkUrl: `/pending/${pendingId}`
            }
          ]
        }));
      }
      if (path === '/api/notifications') {
        return Promise.resolve(json({
          items: [
            {
              notificationId: '77000000-0000-0000-0000-000000000081',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              workItemId: null,
              workItemTitle: null,
              workflowStageCode: 'MaterialArrived',
              workflowStageName: '자재 도착',
              notificationType: 'Info',
              notificationTypeLabel: '정보',
              severity: 'Info',
              severityLabel: '정보',
              visibilityScope: 'RecipientOnly',
              visibilityScopeLabel: '수신자',
              sourceKind: 'WorkAssignment',
              sourceKindLabel: '업무 배정',
              title: '구매품 변경 확인 · 부스바',
              message: 'PJT-003A 구매품 변경 내용을 확인하고 입고 계획에 반영해 주세요.\n\n상세 내용\n입고예정일 변경 7/22 → 7/23',
              linkUrl: '/materials/receipts?project=PJT-003A',
              createdAtUtc: '2026-07-23T00:00:00Z',
              readAtUtc: null
            },
            {
              notificationId: '77000000-0000-0000-0000-000000000082',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              workItemId: null,
              workItemTitle: null,
              workflowStageCode: 'IQC',
              workflowStageName: '수입검사',
              notificationType: 'Blocking',
              notificationTypeLabel: '차단',
              severity: 'Critical',
              severityLabel: '긴급',
              visibilityScope: 'RecipientOnly',
              visibilityScopeLabel: '수신자',
              sourceKind: 'PendingAssignment',
              sourceKindLabel: 'Pending 배정',
              title: '긴급 Pending · 부스바 찍힘',
              message: 'IQC 부적합: 표면 찍힘 사진 확인 필요',
              linkUrl: `/pending/${pendingId}`,
              createdAtUtc: '2026-07-23T00:01:00Z',
              readAtUtc: null
            },
            {
              notificationId: '77000000-0000-0000-0000-000000000083',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              workItemId: null,
              workItemTitle: null,
              workflowStageCode: 'ReceiptConfirmed',
              workflowStageName: '입고 확정',
              notificationType: 'Info',
              notificationTypeLabel: '정보',
              severity: 'Info',
              severityLabel: '정보',
              visibilityScope: 'RecipientOnly',
              visibilityScopeLabel: '수신자',
              sourceKind: 'WorkAssignment',
              sourceKindLabel: '업무 배정',
              title: '입고 확정 · 부스바',
              message: 'PJT-003A · 부스바 · 4 EA · 7/23 도착분이 IQC 합격했습니다. 입고 확정을 진행해 주세요.',
              linkUrl: '/materials/receipts?project=PJT-003A',
              createdAtUtc: '2026-07-23T00:02:00Z',
              readAtUtc: null
            }
          ]
        }));
      }
      return mockFetch(input, init);
    }));

    render(<App />);
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '내 업무' }));
    expect(await screen.findByText('PJT-003A 구매품 변경 내용을 확인하고 입고 계획에 반영해 주세요.')).toBeInTheDocument();
    expect(screen.getByText('입고예정일 변경 7/22 → 7/23')).toBeInTheDocument();
    expect(screen.getByText('IQC 부적합: 표면 찍힘 사진 확인 필요')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: '상세 내용' })).toBeInTheDocument();
    expect(screen.getByText('입고예정일 변경 7/22 → 7/23').closest('td')).toHaveClass('workflow-detail-column');

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '알림' }));
    expect(await screen.findByText('PJT-003A 구매품 변경 내용을 확인하고 입고 계획에 반영해 주세요.')).toBeInTheDocument();
    expect(screen.getByText('입고예정일 변경 7/22 → 7/23')).toBeInTheDocument();
    expect(screen.getByText('IQC 부적합: 표면 찍힘 사진 확인 필요')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: '상세 내용' })).toBeInTheDocument();
    const iqcDetail = screen.getByText('PJT-003A · 부스바 · 4 EA · 7/23 도착분이 IQC 합격했습니다. 입고 확정을 진행해 주세요.');
    expect(iqcDetail.closest('td')).toHaveClass('workflow-detail-column');
  });

  it('keeps my work visible and reports partial success when the post-action refresh fails', async () => {
    let workCompleted = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/my-work/76000000-0000-0000-0000-000000000001/complete' && init?.method === 'POST') {
        workCompleted = true;
        return json({ status: 'Completed', statusLabel: '완료' });
      }
      if (path === '/api/my-work' && workCompleted) {
        return json({ title: 'refresh failed' }, 500);
      }
      return mockFetch(input, init);
    }));
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '내 업무' }));
    expect(await screen.findByText('생산계획, 담당자 입력')).toBeInTheDocument();
    const selectedWork = screen.getByRole('checkbox', { name: '생산계획, 담당자 입력 선택' });
    fireEvent.click(selectedWork);
    fireEvent.click(screen.getByRole('button', { name: '작업 완료' }));

    expect(await screen.findByText('생산계획, 담당자 입력 업무는 완료했지만 최신 목록을 불러오지 못했습니다. 새로고침해 주세요.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '작업 완료' })).not.toBeDisabled();
    expect(selectedWork).toBeChecked();
  });

  it('keeps notification success feedback visible after the read row leaves the active tab', async () => {
    let notificationRead = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/notifications/77000000-0000-0000-0000-000000000001/read' && init?.method === 'POST') {
        notificationRead = true;
        return json({ notificationId: '77000000-0000-0000-0000-000000000001', readAtUtc: '2026-07-18T10:00:00Z' });
      }
      if (path === '/api/notifications' && notificationRead) {
        return json({ items: [] });
      }
      if (path === '/api/notifications/summary' && notificationRead) {
        return json({ unreadCount: 0, blockingCount: 0 });
      }
      return mockFetch(input, init);
    }));
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '알림' }));
    expect(await screen.findByText('프로젝트가 생성되었습니다.')).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: '읽음' }).at(-1)!);

    expect(await screen.findByText('프로젝트가 생성되었습니다. 알림을 읽음 처리했습니다.')).toBeInTheDocument();
    expect(screen.getByText('표시할 알림이 없습니다.')).toBeInTheDocument();
  });

  it('uses a top-left trigger with an accessible permission-derived mobile drawer', async () => {
    mockMobileViewport(true);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/me') {
        const user = currentUser(readDevUser(init));
        return json({ ...user, permissions: [...user.permissions, 'Pending.Read'] });
      }
      return mockFetch(input, init);
    }));
    render(<App />);

    const menuButton = await screen.findByRole('button', { name: '메뉴 열기' });
    expect(screen.queryByRole('navigation', { name: '모바일 공통 메뉴' })).not.toBeInTheDocument();
    expect(document.querySelector('.app-mobile-nav')).not.toBeInTheDocument();

    const accountButton = screen.getByRole('button', { name: '내 계정 열기' });
    fireEvent.click(accountButton);
    const accountSheet = await screen.findByRole('dialog', { name: '내 계정' });
    expect(accountButton).toHaveAttribute('aria-expanded', 'true');
    expect(accountSheet.querySelector('input[type="file"]')).toHaveAttribute('hidden');
    expect(within(accountSheet).getByRole('button', { name: '프로필 사진 변경 안내 열기' })).toBeInTheDocument();
    expect(within(accountSheet).queryByRole('button', { name: '개인정보·이용 안내' })).not.toBeInTheDocument();
    expect(within(accountSheet).getByLabelText('모바일 시스템 상태')).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '내 계정' })).not.toBeInTheDocument());
    await waitFor(() => expect(accountButton).toHaveFocus());

    fireEvent.click(menuButton);

    const menuDrawer = await screen.findByRole('dialog', { name: '전체 업무 메뉴' });
    const mobileNavigation = within(menuDrawer).getByRole('navigation', { name: '모바일 공통 메뉴' });
    expect(menuButton).toHaveAttribute('aria-expanded', 'true');
    expect(within(menuDrawer).getByLabelText('개발 사용자')).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '홈' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '내 업무 1건' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '프로젝트' })).toHaveAttribute('aria-current', 'page');
    expect(within(mobileNavigation).getByRole('button', { name: 'Pending' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '생산관리' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '구매' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '자재' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '제조' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '품질' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '물류' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '알림 1건' })).toBeInTheDocument();
    expect(Array.from(menuDrawer.querySelectorAll('.mobile-menu-item-shape')).map((shape) => shape.getAttribute('data-shape-role')))
      .toEqual(expect.arrayContaining(['active', 'control']));
    expect(menuDrawer.querySelector('[data-shape]')).not.toBeInTheDocument();
    await waitFor(() => expect(within(mobileNavigation).getByRole('button', { name: '홈' })).toHaveFocus());

    fireEvent.keyDown(document, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '전체 업무 메뉴' })).not.toBeInTheDocument());
    await waitFor(() => expect(menuButton).toHaveFocus());
    expect(menuButton).toHaveAttribute('aria-expanded', 'false');

    fireEvent.click(menuButton);
    const reopenedDrawer = await screen.findByRole('dialog', { name: '전체 업무 메뉴' });
    fireEvent.click(within(reopenedDrawer).getByRole('button', { name: '생산관리' }));
    expect(within(reopenedDrawer).getByRole('button', { name: '생산관리' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('dialog', { name: '전체 업무 메뉴' })).toBeInTheDocument();
    fireEvent.click(within(reopenedDrawer).getByRole('button', { name: '생산계획' }));
    expect(await screen.findByLabelText('생산계획 요약')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: '전체 업무 메뉴' })).not.toBeInTheDocument();
    expect(window.location.pathname).toBe('/production-planning/plans');

    fireEvent.click(menuButton);
    const logoDrawer = await screen.findByRole('dialog', { name: '전체 업무 메뉴' });
    fireEvent.click(within(logoDrawer).getByRole('button', { name: 'EMI PMS 메뉴 로고로 홈 이동' }));
    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: '전체 업무 메뉴' })).not.toBeInTheDocument();
    expect(window.location.pathname).toBe('/');
  });

  it('opens Home from the mobile header logo', async () => {
    mockMobileViewport(true);
    window.history.pushState(null, '', '/projects');
    render(<App />);

    const mobileLogo = await screen.findByRole('button', { name: 'EMI PMS 모바일 로고로 홈 이동' });
    expect(window.location.pathname).toBe('/projects');
    fireEvent.click(mobileLogo);

    expect(await screen.findByRole('heading', { name: '업무 홈' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/');
  });

  it('shows every operational menu in the mobile drawer for a sales user', async () => {
    mockMobileViewport(true);
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '메뉴 열기' }));
    const menuDrawer = await screen.findByRole('dialog', { name: '전체 업무 메뉴' });
    const mobileNavigation = within(menuDrawer).getByRole('navigation', { name: '모바일 공통 메뉴' });
    // Anchored to the full accessible name (with an optional badge suffix) so
    // department parents cannot collide with always-visible workspace children
    // such as 제조 vs 제조 투입.
    for (const label of ['홈', '내 업무', '프로젝트', 'Pending', '생산관리', '구매', '자재', '제조', '품질', '물류', '알림']) {
      expect(within(mobileNavigation).getByRole('button', { name: new RegExp(`^${label}( \\d+건)?$`) })).toBeInTheDocument();
    }
    // Departments are whole-row disclosures: children stay hidden until the
    // parent is tapped, and opening another department closes the previous one.
    expect(within(mobileNavigation).queryByRole('button', { name: '입고 관리' })).not.toBeInTheDocument();
    fireEvent.click(within(mobileNavigation).getByRole('button', { name: '자재' }));
    expect(within(mobileNavigation).getByRole('button', { name: '자재' })).toHaveAttribute('aria-expanded', 'true');
    expect(within(mobileNavigation).getByRole('button', { name: '입고 관리' })).toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '패널 키팅' })).toBeInTheDocument();
    fireEvent.click(within(mobileNavigation).getByRole('button', { name: '품질' }));
    expect(within(mobileNavigation).queryByRole('button', { name: '입고 관리' })).not.toBeInTheDocument();
    expect(within(mobileNavigation).getByRole('button', { name: '수입검사(IQC)' })).toBeInTheDocument();
    expect(within(mobileNavigation).queryByRole('button', { name: '관리자' })).not.toBeInTheDocument();
  });

  it('returns from IQC project detail to the IQC project list, clearing the project query', async () => {
    window.history.pushState(null, '', `/quality/iqc?project=${projectId}`);
    render(<App />);

    const iqcDetail = await screen.findByTestId('material-iqc-page');
    fireEvent.click(within(iqcDetail).getByRole('button', { name: 'IQC 프로젝트' }));

    expect(await screen.findByTestId('quality-iqc-dashboard')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/quality/iqc');
    expect(window.location.search).toBe('');
    expect(screen.queryByTestId('material-iqc-page')).not.toBeInTheDocument();
  });

  it('renders the Teams Activity tab route with recent notifications and work summary', async () => {
    window.history.pushState(null, '', '/teams/activity');
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'EMI PMS 알림' })).toBeInTheDocument();
    expect(screen.getByText('Teams 알림을 선택하면 관련 업무를 확인할 수 있습니다. 상세 업무 화면은 시스템 링크에서 확인하세요.')).toBeInTheDocument();
    expect(await screen.findByText('프로젝트가 생성되었습니다.')).toBeInTheDocument();
    expect(screen.getByText('최근 알림')).toBeInTheDocument();
    expect(screen.getByText('내 미완료 업무')).toBeInTheDocument();
    expect(screen.getByText('생산계획, 담당자 입력')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '내 업무 전체 보기' })).toBeInTheDocument();
  });

  it('opens a notification detail from Teams context subEntityId on the Teams Activity route', async () => {
    const notificationId = '77000000-0000-0000-0000-000000000001';
    Object.defineProperty(document, 'referrer', { value: 'https://teams.microsoft.com/l/entity/app/home', configurable: true });
    teamsJsMock.context = {
      page: {
        subEntityId: `notification:${notificationId}`
      }
    };
    window.history.pushState(null, '', '/teams/activity');

    render(<App />);

    expect(await screen.findByRole('heading', { name: '알림 상세' })).toBeInTheDocument();
    expect(await screen.findByText('TASK-003A Demo 프로젝트가 생성되었습니다.')).toBeInTheDocument();
    expect(window.location.pathname).toBe(`/teams/activity/notifications/${notificationId}`);
    expect(teamsJsMock.initialize).toHaveBeenCalled();
    expect(teamsJsMock.getContext).toHaveBeenCalled();
  });

  it('opens a notification detail from the Teams Activity context query fallback', async () => {
    const notificationId = '77000000-0000-0000-0000-000000000001';
    const context = encodeURIComponent(JSON.stringify({ subEntityId: `notification:${notificationId}` }));
    window.history.pushState(null, '', `/teams/activity?context=${context}`);

    render(<App />);

    expect(await screen.findByRole('heading', { name: '알림 상세' })).toBeInTheDocument();
    expect(await screen.findByText('TASK-003A Demo 프로젝트가 생성되었습니다.')).toBeInTheDocument();
    expect(window.location.pathname).toBe(`/teams/activity/notifications/${notificationId}`);
    expect(teamsJsMock.initialize).not.toHaveBeenCalled();
  });

  it('opens the target project section from a work item deep link', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-production' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '내 업무' }));
    expect(await screen.findByText('생산계획, 담당자 입력')).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: '이동' })[0]);

    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/my-work/76000000-0000-0000-0000-000000000001/start'),
      expect.objectContaining({ method: 'POST' })
    ));
    expect(await screen.findByRole('heading', { name: '생산계획 수정' })).toBeInTheDocument();
  });

  it('opens the workflow summary for unimplemented work item and notification links', async () => {
    const materialWorkItemId = '76000000-0000-0000-0000-000000000005';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/my-work') {
        return Promise.resolve(json({
          items: [
            {
              workItemId: materialWorkItemId,
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              projectDeliveryDate: '2026-07-01',
              workflowStageCode: 'MaterialArrived',
              workflowStageName: '자재 도착',
              responsibilityType: 'MaterialsPrimary',
              responsibilityLabel: '자재 정담당자',
              title: '자재 도착 등록',
              description: '자재 도착 단계 처리가 필요합니다.',
              status: 'Requested',
              statusLabel: '시작 전',
              priority: 'Normal',
              priorityLabel: '일반',
              dueDate: null,
              createdAtUtc: '2026-06-25T00:00:00Z',
              startedAtUtc: null,
              completedAtUtc: null,
              linkUrl: `/projects/${projectId}?section=workflow`
            }
          ]
        }));
      }

      if (url.pathname === `/api/my-work/${materialWorkItemId}/start` && init?.method === 'POST') {
        return Promise.resolve(json({ status: 'InProgress', statusLabel: '진행 중' }));
      }

      if (url.pathname === '/api/notifications') {
        return Promise.resolve(json({
          items: [
            {
              notificationId: '77000000-0000-0000-0000-000000000005',
              projectId,
              projectTitle: 'TASK-003A Demo',
              projectCode: 'PJT-003A',
              projectItem: 'UL67',
              notificationType: 'Reference',
              notificationTypeLabel: '참조',
              severity: 'Info',
              severityLabel: '정보',
              title: '자재 도착 단계 알림',
              message: '자재 도착 단계 확인이 필요합니다.',
              linkUrl: `/projects/${projectId}?section=workflow`,
              createdAtUtc: '2026-06-25T00:00:00Z',
              readAtUtc: null
            }
          ]
        }));
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '내 업무' }));
    expect(await screen.findByText('자재 도착 등록')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '이동' }));

    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining(`/api/my-work/${materialWorkItemId}/start`),
      expect.objectContaining({ method: 'POST' })
    ));
    expect(await screen.findByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('영업 등록부터 세금계산서 완료까지 18단계의 현재 위치와 부서 인계를 한눈에 확인합니다.')).toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '알림' }));
    expect(await screen.findByText('자재 도착 단계 알림')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '이동' }));

    expect(await screen.findByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('heading', { name: '자재 입고 처리' })).not.toBeInTheDocument();
  });

  it('renders project list cards for mobile layout without raw enum values', async () => {
    mockMobileViewport(true);
    render(<App />);

    const mobileList = await screen.findByTestId('project-list-mobile');
    const firstCard = within(mobileList).getAllByTestId('project-list-card')[0];
    expect(screen.getByLabelText('선택 프로젝트 내보내기')).toBeInTheDocument();
    const mobileSelection = within(firstCard).getByRole('checkbox', { name: 'PJT-003A TASK-003A Demo 선택' });
    fireEvent.click(mobileSelection);
    expect(screen.getByText('1개 선택')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '선택 Excel 내보내기' })).not.toBeDisabled();
    expect(firstCard).toHaveTextContent('TASK-003A Demo');
    expect(firstCard).toHaveTextContent('고객사EMI Test Customer');
    expect(firstCard).toHaveTextContent('CodePJT-003A');
    expect(firstCard).toHaveTextContent('ItemUL67');
    expect(firstCard).toHaveTextContent('면수4면');
    expect(firstCard).toHaveTextContent('납기일2026-10-10');
    expect(firstCard).toHaveTextContent('상태생산관리');
    expect(firstCard).toHaveTextContent('진행률6%');
    expect(firstCard).not.toHaveTextContent('BeforeManufacturing');

    const filterTrigger = screen.getByRole('button', { name: /검색·필터/ });
    fireEvent.click(filterTrigger);
    const filterSheet = await screen.findByRole('dialog', { name: '프로젝트 검색·필터' });
    const searchInput = within(filterSheet).getByPlaceholderText('고객사, Item, Code, Title');
    fireEvent.change(searchInput, { target: { value: 'TASK' } });
    const dateFromInput = within(filterSheet).getByLabelText('납기 시작일');
    dateFromInput.focus();
    fireEvent.change(dateFromInput, { target: { value: '2026-01-01' } });
    await waitFor(() => expect(dateFromInput).toHaveFocus());
    fireEvent.click(within(filterSheet).getByRole('button', { name: '취소' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '프로젝트 검색·필터' })).not.toBeInTheDocument());
    expect(filterTrigger).toHaveTextContent('전체 프로젝트 표시 중');

    fireEvent.click(filterTrigger);
    const reopenedFilterSheet = await screen.findByRole('dialog', { name: '프로젝트 검색·필터' });
    fireEvent.change(within(reopenedFilterSheet).getByPlaceholderText('고객사, Item, Code, Title'), { target: { value: 'TASK' } });
    fireEvent.click(within(reopenedFilterSheet).getByRole('button', { name: '조건 적용' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: '프로젝트 검색·필터' })).not.toBeInTheDocument());
    expect(filterTrigger).toHaveTextContent('1개 조건 적용 중');
    await waitFor(() => expect(filterTrigger).toHaveFocus());

    const filteredMobileList = await screen.findByTestId('project-list-mobile');
    fireEvent.click(within(filteredMobileList).getAllByRole('button', { name: '상세 보기' })[0]);
    expect(await screen.findByRole('heading', { name: 'TASK-003A Demo' })).toBeInTheDocument();
    const mobileActions = screen.getByText('프로젝트 작업').closest('details');
    expect(mobileActions).not.toBeNull();
    fireEvent.click(screen.getByText('프로젝트 작업'));
    expect(within(mobileActions as HTMLElement).getByRole('button', { name: '수정' })).toBeInTheDocument();
    expect(within(mobileActions as HTMLElement).getByRole('button', { name: '보류' })).toBeInTheDocument();
    expect(within(mobileActions as HTMLElement).getByRole('button', { name: '취소' })).toBeInTheDocument();
    expect(within(mobileActions as HTMLElement).getByRole('button', { name: '삭제' })).toBeInTheDocument();
  });

  it('hides business action buttons from System Administrator while showing sales amount', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });

    await waitFor(() => expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument());
    expect(screen.getAllByText('KRW 1,250,000.5').length).toBeGreaterThan(0);
  });

  it('shows calendar holiday admin page for System Administrator', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '공휴일' }));

    expect(await screen.findByRole('heading', { name: '휴일 관리' })).toBeInTheDocument();
    expect(screen.getByText('회사 창립기념 휴일')).toBeInTheDocument();
    expect(screen.getAllByText('대체공휴일').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: 'Excel 양식 다운로드' }));
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled());
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [new File(['xlsx'], 'holidays.xlsx')] } });
    fireEvent.click(screen.getByRole('button', { name: '미리보기' }));
    expect(await screen.findByText('오류 1행')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '저장 가능한 행 반영' })).toBeEnabled();
  });

  it('shows admin dashboard and system management pages for System Administrator', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));

    expect(await screen.findByRole('heading', { name: '관리자' })).toBeInTheDocument();
    expect(screen.getByText('발송 실패')).toBeInTheDocument();
    expect(screen.getByText('L0 예정일 임박')).toBeInTheDocument();
    expect(screen.getByText('L1 초과')).toBeInTheDocument();
    expect(screen.queryByText('발송 완료')).not.toBeInTheDocument();
    expect(screen.queryByText('마지막 일일 요약')).not.toBeInTheDocument();
    expect(screen.queryByText('최근 기준정보 변경')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '승인 대기 사용자 보기' }));
    expect(await screen.findByRole('heading', { name: '승인 대기 사용자' })).toBeInTheDocument();
    expect(screen.getByText('Entra Pending User')).toBeInTheDocument();
    expect(screen.queryByText('Entra Sales User')).not.toBeInTheDocument();
    expect(screen.queryByText('Dev System Administrator')).not.toBeInTheDocument();
    expect(window.location.pathname).toBe('/admin/users');
    expect(window.location.search).toBe('?filter=approval-pending');

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '실패 알림 보기' }));
    expect(await screen.findByRole('heading', { name: '알림 발송 상태' })).toBeInTheDocument();
    expect(screen.getByText('현재 필터:')).toBeInTheDocument();
    expect(screen.getAllByText('발송 실패').length).toBeGreaterThan(0);
    expect(screen.getByText('수신자 이메일 또는 사용자 정보를 확인하세요.')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: '미처리 실패' })).toHaveAttribute('aria-selected', 'true');
    const failedConfirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getByLabelText('발송 실패 테스트 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 확인 처리' }));
    expect(await screen.findByText(/처리 완료 1건/)).toBeInTheDocument();
    failedConfirmSpy.mockRestore();
    expect(screen.queryByRole('button', { name: '수동 재처리' })).not.toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '대기 알림 보기' }));
    expect(await screen.findByRole('heading', { name: '알림 발송 상태' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: '미처리 대기' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getAllByText('발송 worker 처리 대기 중입니다.').length).toBeGreaterThan(0);
    const pendingConfirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getByLabelText('예정일 초과 알림 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 재발송' }));
    expect(await screen.findByText(/처리 완료 1건/)).toBeInTheDocument();
    pendingConfirmSpy.mockRestore();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '진행 중 에스컬레이션 보기' }));
    expect(await screen.findByRole('heading', { name: '에스컬레이션 상태' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: '진행 중' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'L1 예정일 초과' })).toBeInTheDocument();
    expect(screen.getByText('진행 중 에스컬레이션은 예정일 임박 또는 초과 후 아직 완료/취소되지 않은 업무입니다. L0는 예정일 임박, L1~L3는 초과 단계입니다.')).toBeInTheDocument();
    expect(screen.getByText('정담당자 조치 상태를 확인하세요.')).toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    expect(screen.queryByRole('button', { name: 'Item' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '포장방식' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '생산계획 단계 설정' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '구매 필수 항목 설정' })).not.toBeInTheDocument();
    expect(screen.queryByText('대상을 찾을 수 없습니다.')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '사용자 관리' }));
    expect(await screen.findByRole('heading', { name: '사용자 관리' })).toBeInTheDocument();
    expect(screen.getByText(/부서를 선택하면 기본 역할이 자동 지정됩니다/)).toBeInTheDocument();
    const entraSalesRow = screen.getByText('Entra Sales User').closest('tr');
    expect(entraSalesRow).not.toBeNull();
    fireEvent.click(within(entraSalesRow as HTMLElement).getByRole('button', { name: '수정' }));
    fireEvent.change(within(entraSalesRow as HTMLElement).getByRole('combobox'), {
      target: { value: '10000000-0000-0000-0000-000000000001' }
    });
    expect(within(entraSalesRow as HTMLElement).getByRole('checkbox', { name: /system-administrator/ })).toBeChecked();
    expect(within(entraSalesRow as HTMLElement).getByRole('checkbox', { name: 'sales' })).not.toBeChecked();
    const departmentHeadCheckbox = within(entraSalesRow as HTMLElement).getByRole('checkbox', { name: '지정' });
    fireEvent.click(departmentHeadCheckbox);
    expect(departmentHeadCheckbox).toBeChecked();
    fireEvent.click(within(entraSalesRow as HTMLElement).getByRole('button', { name: '취소' }));
    expect(screen.getAllByRole('button', { name: '삭제' }).length).toBeGreaterThan(0);
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getAllByRole('button', { name: '삭제' })[0]);
    expect(await screen.findByText('사용자를 삭제 예정으로 처리했습니다.')).toBeInTheDocument();
    expect(screen.getAllByText(/삭제 예정/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/완전 삭제 예정일/).length).toBeGreaterThan(0);
    confirmSpy.mockRestore();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(screen.getByRole('button', { name: '부서' }));
    expect(await screen.findByRole('heading', { name: '부서 관리' })).toBeInTheDocument();
    expect(screen.getByDisplayValue('영업')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '추가' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '삭제' })).toBeInTheDocument();
    const departmentConfirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getByRole('button', { name: '삭제' }));
    expect(await screen.findByText('부서를 삭제 예정으로 처리했습니다.')).toBeInTheDocument();
    expect(screen.getAllByText('삭제 예정').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/완전 삭제 예정일 2026-07-14/).length).toBeGreaterThan(0);
    departmentConfirmSpy.mockRestore();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(screen.getByRole('button', { name: '공휴일' }));
    expect(await screen.findByRole('heading', { name: '휴일 관리' })).toBeInTheDocument();
    const holidayConfirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getAllByRole('button', { name: '삭제' })[0]);
    expect(await screen.findByText('휴일을 삭제 예정으로 처리했습니다.')).toBeInTheDocument();
    expect(screen.getAllByText('삭제 예정').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/완전 삭제 예정일 2026-07-14/).length).toBeGreaterThan(0);
    holidayConfirmSpy.mockRestore();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '권한 매트릭스' }));
    expect(await screen.findByRole('heading', { name: '권한 매트릭스' })).toBeInTheDocument();
    expect(screen.getByText('Read administrator history')).toBeInTheDocument();
    expect(document.querySelectorAll('.permission-matrix-value-cell').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: '삭제' })).not.toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '알림 수동 발송' }));
    expect(await screen.findByRole('heading', { name: '알림 수동 발송' })).toBeInTheDocument();
    expect(screen.getByDisplayValue('[테스트] 프로젝트 생성 알림')).toBeInTheDocument();
    const teamsActivitySelect = screen.getByRole('listbox', { name: /Teams Activity 수신자/ }) as HTMLSelectElement;
    const teamsActivityOption = within(teamsActivitySelect).getByRole('option', { name: /Entra Notification User/ }) as HTMLOptionElement;
    teamsActivityOption.selected = true;
    fireEvent.change(teamsActivitySelect);
    const mailSelect = screen.getByRole('listbox', { name: /Mail 사용자/ }) as HTMLSelectElement;
    const mailOption = within(mailSelect).getByRole('option', { name: /Entra Notification User/ }) as HTMLOptionElement;
    mailOption.selected = true;
    fireEvent.change(mailSelect);
    expect(screen.queryByText('발송 방식')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '발송' })).toBeInTheDocument();
    expect(screen.queryByText(/Correlation ID/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '발송' }));
    expect(await screen.findByText('발송 요청이 접수되었습니다. 알림발송상태에서 결과를 확인할 수 있습니다. 잠시 후 이동합니다.')).toBeInTheDocument();
    expect(screen.queryByText((_, element) => Boolean(element?.textContent?.includes('N003-UNIT-FRONT')))).not.toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: '알림 발송 상태' })).toBeInTheDocument();
    const sentRow = (await screen.findByText('Daily Digest')).closest('tr');
    expect(sentRow).not.toBeNull();
    expect(within(sentRow as HTMLTableRowElement).getByText('발송 완료')).toBeInTheDocument();
    expect(within(sentRow as HTMLTableRowElement).queryByText('미처리')).not.toBeInTheDocument();
  });

  it('shows Processing lease state and disables admin actions', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    window.history.pushState(null, '', '/admin/system/notification-deliveries?status=Processing');
    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(await screen.findByRole('heading', { name: '알림 발송 상태' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: '발송 처리 중' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getAllByText('발송 처리 중').length).toBeGreaterThan(0);
    expect(screen.getByText('claim lease 유효')).toBeInTheDocument();
    expect(screen.getByText('발송 처리 중인 항목은 claim 소유권 보호를 위해 선택하거나 상태를 변경할 수 없습니다.')).toBeInTheDocument();
    expect(screen.getByLabelText('Daily Digest 선택')).toBeDisabled();
    expect(screen.getByRole('button', { name: '선택 확인 처리' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '선택 제외 처리' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '선택 재발송' })).toBeDisabled();
  });

  it('shows masked delivery attempt audit in admin detail', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    window.history.pushState(null, '', '/admin/system/notification-deliveries/79000000-0000-0000-0000-000000000101');
    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(await screen.findByRole('heading', { name: '알림 발송 상세' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '발송 시도 이력' })).toBeInTheDocument();
    expect(screen.getAllByText('발송 완료').length).toBeGreaterThan(0);
    expect(screen.getAllByText('1회').length).toBeGreaterThan(0);
    expect(screen.queryByText('opaque-test-worker')).not.toBeInTheDocument();
  });

  it('labels Web Push as provider acceptance in the admin monitor', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    window.history.pushState(null, '', '/admin/system/notification-deliveries');
    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(await screen.findByRole('heading', { name: '알림 발송 상태' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('채널'), { target: { value: 'WebPush' } });
    expect(await screen.findByText('PWA 푸시')).toBeInTheDocument();
    expect(screen.getAllByText('인앱 연동 PWA 푸시').length).toBeGreaterThan(0);
    expect(screen.getByText('푸시 서비스 접수')).toBeInTheDocument();
    expect(screen.getByText((content, element) => element?.tagName === 'SMALL' && content.startsWith('서비스 접수'))).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '기기 푸시 알림' }));
    expect(await screen.findByRole('heading', { name: '알림 발송 상세' })).toBeInTheDocument();
    expect(screen.getAllByText('푸시 서비스 접수').length).toBeGreaterThan(0);
    expect(screen.getByText('서비스 접수')).toBeInTheDocument();
  });

  it('shows field-level department validation errors', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '관리자' }));
    fireEvent.click(await screen.findByRole('button', { name: '부서' }));

    const addDepartmentSection = (await screen.findByText('부서 추가')).closest('.subsection') as HTMLElement;
    const textInputs = within(addDepartmentSection).getAllByRole('textbox');
    fireEvent.change(textInputs[0], { target: { value: 'bad code' } });
    fireEvent.change(textInputs[1], { target: { value: '' } });
    fireEvent.change(within(addDepartmentSection).getByRole('spinbutton'), { target: { value: '10000' } });
    fireEvent.click(screen.getByRole('button', { name: '추가' }));

    expect(await screen.findByText('부서 코드는 영문 대문자, 숫자, 하이픈(-), 언더스코어(_)만 사용할 수 있습니다.')).toBeInTheDocument();
    expect(screen.getByText('부서명은 필수입니다.')).toBeInTheDocument();
    expect(screen.getByText('정렬 순서는 0 이상 9999 이하로 입력해주세요.')).toBeInTheDocument();
  });

  it('hides sales amount and project write buttons from Manufacturing users', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-manufacturing' } });

    await waitFor(() => expect(screen.queryByRole('button', { name: '신규 프로젝트' })).not.toBeInTheDocument());
    expect(screen.queryByText('KRW 1,250,000.5')).not.toBeInTheDocument();

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByRole('tab', { name: '설계' });

    expect(screen.queryByRole('button', { name: '수정' })).not.toBeInTheDocument();
  });

  it('uses the whole-workflow progress as the project detail progress source', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url, window.location.origin);
      if (url.pathname === `/api/projects/${projectId}/workflow`) {
        return Promise.resolve(json({
          ...projectWorkflowResponse(projectId),
          completedRequiredStageCount: 7,
          progressPercent: 41
        }));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByText('TASK-003A Demo'));

    await waitFor(() => expect(screen.getAllByText('41%').length).toBeGreaterThanOrEqual(2));
    expect(screen.queryByText('6%')).not.toBeInTheDocument();
  });

  it('validates required fields on the create form', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    expect(await screen.findByLabelText('FAT 필요 여부')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: '등록' }));

    expect((await screen.findAllByText('필수 입력값입니다.')).length).toBeGreaterThanOrEqual(5);
    expect(screen.getByText('포장방식은 필수 선택값입니다.')).toBeInTheDocument();
  });

  it('creates and displays the optional LSE TASK NO in project basic information', async () => {
    const requestBodies: Array<Record<string, unknown>> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Record<string, unknown>;
        requestBodies.push(body);
      }
      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    await screen.findByRole('option', { name: 'Dev Sales User' });
    fillCreateForm('LSE-UI-001', 'LSE UI Project');
    fireEvent.change(screen.getByLabelText('LSE TASK NO'), { target: { value: ' LSE-104-105 ' } });
    fireEvent.click(screen.getByRole('button', { name: '등록' }));

    await waitFor(() => expect(requestBodies).toHaveLength(1));
    expect(requestBodies[0].lseTaskNumber).toBe('LSE-104-105');

    fireEvent.click((await screen.findAllByText('TASK-003A Demo'))[0]);
    expect(await screen.findByText('LSE-104-105')).toBeInTheDocument();
  });

  it('shows project edit validation next to the invalid field', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-sales' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    const customerName = await screen.findByLabelText('고객사*');
    fireEvent.change(customerName, { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    expect(await screen.findByText('입력값을 확인해 주세요.')).toBeInTheDocument();
    expect(screen.getByText('고객사: 필수 입력값입니다.')).toBeInTheDocument();
    expect(customerName.closest('.form-field')).toHaveTextContent('필수 입력값입니다.');
    expect(customerName.closest('.form-field')).toHaveClass('has-error');
  });

  it('shows a friendly duplicate title conflict message', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    await screen.findByRole('option', { name: 'Dev Sales User' });
    fillCreateForm('DUP-001', 'Duplicate Project');
    fireEvent.click(screen.getByRole('button', { name: '등록' }));

    expect(await screen.findByText('동일한 PJT Title이 이미 존재합니다.')).toBeInTheDocument();
  });

  it('disables the submit button while saving and navigates to detail after create', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    await screen.findByRole('option', { name: 'Dev Sales User' });
    fillCreateForm('NEW-001', 'New Project');
    fireEvent.click(screen.getByRole('button', { name: '등록' }));

    expect(await screen.findByRole('button', { name: '저장 중' })).toBeDisabled();
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    const productPanelTable = await screen.findByRole('table', { name: '설계' });
    expect(within(productPanelTable).getByText('No')).toBeInTheDocument();
    expect(within(productPanelTable).getByText('패널명')).toBeInTheDocument();
    expect(within(productPanelTable).getAllByText('미입력').length).toBeGreaterThanOrEqual(4);
  });

  it('requires panel selections when decreasing panel count', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('면수*'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('수정사유*'), { target: { value: '면수 감소' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    expect(await screen.findByText('감소 면수만큼 취소할 패널을 선택하세요.')).toBeInTheDocument();
  });

  it('keeps project edit form hidden until all initial data is loaded', async () => {
    const salesOwners = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === '/api/sales-owners') {
        return salesOwners.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    expect(await screen.findByText('프로젝트 정보를 불러오는 중입니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '저장' })).not.toBeInTheDocument();

    salesOwners.resolve(json([{ userId: salesOwnerId, displayName: 'Dev Sales User' }]));

    expect(await screen.findByLabelText('면수*')).toHaveValue(4);
    expect(screen.getByRole('button', { name: '저장' })).toBeEnabled();
  });

  it('does not overwrite user edits when a stale project edit load resolves later', async () => {
    const staleProject = createDeferred<Response>();
    let delayNextProjectDetail = false;
    let delayedProjectDetail = false;

    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (delayNextProjectDetail
          && !delayedProjectDetail
          && path === `/api/projects/${projectId}`
          && init?.method === undefined) {
        delayedProjectDetail = true;
        return staleProject.promise;
      }

      return mockFetch(input, init);
    }));

    render(<StrictMode><App /></StrictMode>);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    delayNextProjectDetail = true;
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    const panelCount = await screen.findByLabelText('면수*');
    const customerName = screen.getByLabelText('고객사*');
    expect(panelCount).toHaveValue(4);

    fireEvent.change(panelCount, { target: { value: '6' } });
    fireEvent.change(customerName, { target: { value: 'Changed Customer' } });
    expect(panelCount).toHaveValue(6);
    expect(customerName).toHaveValue('Changed Customer');

    staleProject.resolve(json(projectDetail(true, 'Active', 'TASK-003A Demo')));

    await waitFor(() => {
      expect(panelCount).toHaveValue(6);
      expect(customerName).toHaveValue('Changed Customer');
    });
  });

  it('reinitializes the edit form when navigating to a different project', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('PJT Title*'), { target: { value: 'Unsaved Title' } });
    expect(screen.getByLabelText('PJT Title*')).toHaveValue('Unsaved Title');

    fireEvent.click(screen.getByRole('button', { name: '상세' }));
    const breadcrumbs = await screen.findByRole('navigation', { name: '현재 위치' });
    fireEvent.click(within(breadcrumbs).getByRole('button', { name: '프로젝트' }));
    fireEvent.click(await screen.findByText('OnHold Project'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));

    expect(await screen.findByLabelText('PJT Title*')).toHaveValue('OnHold Project');
  });

  it('shows stale panel count conflicts without overwriting the edited value', async () => {
    let changePanelCountBody: { panelCount: number; expectedActivePanelCount: number } | undefined;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === `/api/projects/${projectId}/change-panel-count`) {
        changePanelCountBody = JSON.parse(String(init?.body));
        return json({ title: '다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.' }, 409);
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('button', { name: '수정' }));
    fireEvent.change(await screen.findByLabelText('면수*'), { target: { value: '6' } });
    fireEvent.change(screen.getByLabelText('고객사*'), { target: { value: 'Changed Customer' } });
    fireEvent.change(screen.getByLabelText('수정사유*'), { target: { value: '면수 증가' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    expect(await screen.findByText('다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.')).toBeInTheDocument();
    expect(screen.getByLabelText('면수*')).toHaveValue(6);
    expect(screen.getByLabelText('고객사*')).toHaveValue('Changed Customer');
    expect(changePanelCountBody).toEqual(expect.objectContaining({
      panelCount: 6,
      expectedActivePanelCount: 4
    }));
  });

  it('requires reasons for hold and cancel dialogs', async () => {
    render(<App />);

    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByRole('tab', { name: '설계' });
    fireEvent.click(screen.getByRole('button', { name: '보류' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: '프로젝트 보류' })).getByRole('button', { name: '확인' }));

    expect(await screen.findByText('사유는 필수입니다.')).toBeInTheDocument();
  });

  it('renders OnHold and Cancelled status badges', async () => {
    render(<App />);

    expect((await screen.findAllByText('보류')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('취소').length).toBeGreaterThan(0);
  });

  it('ignores a stale active tab response after the cancelled tab loads', async () => {
    const active = createDeferred<Response>();
    const cancelled = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Active') {
        return active.promise;
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return cancelled.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));
    cancelled.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Race Cancelled', cancelledProjectId)]));

    expect(await screen.findByText('Race Cancelled')).toBeInTheDocument();
    active.resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Race Active', projectId)]));

    await waitFor(() => expect(screen.queryByText('Race Active')).not.toBeInTheDocument());
    expect(screen.getByRole('tab', { name: '취소' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ignores stale cancelled responses after the deleted archive tab loads', async () => {
    const cancelled = createDeferred<Response>();
    const deleted = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return cancelled.promise;
      }

      if (url.pathname === '/api/deleted-projects') {
        return deleted.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));
    fireEvent.click(await screen.findByRole('tab', { name: '삭제 보관함' }));
    deleted.resolve(projectListResponse([deletedProjectListItem('dev-sales')]));

    expect(await screen.findByText('Deleted Project')).toBeInTheDocument();
    cancelled.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Late Cancelled', cancelledProjectId)]));

    await waitFor(() => expect(screen.queryByText('Late Cancelled')).not.toBeInTheDocument());
    expect(screen.getByRole('tab', { name: '삭제 보관함' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows deleted project restore only to administrators', async () => {
    const calls: string[] = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/deleted-projects/${projectId}/restore`) {
        calls.push(`${init?.method ?? 'GET'} ${url.pathname}`);
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.click(await screen.findByRole('tab', { name: '삭제 보관함' }));
    expect(await screen.findByText('Deleted Project')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '복구' })).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    fireEvent.click(await screen.findByRole('tab', { name: '삭제 보관함' }));
    expect(await screen.findByRole('button', { name: '복구' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '복구' }));

    await waitFor(() => expect(calls).toContain(`POST /api/deleted-projects/${projectId}/restore`));
  });

  it('keeps the latest search result when an earlier search fails later', async () => {
    const alpha = createDeferred<Response>();
    const beta = createDeferred<Response>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('search') === 'Alpha') {
        return alpha.promise;
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('search') === 'Beta') {
        return beta.promise;
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    const search = await screen.findByPlaceholderText('고객사, Item, PJT Code, PJT Title 검색');
    fireEvent.change(search, { target: { value: 'Alpha' } });
    fireEvent.change(search, { target: { value: 'Beta' } });
    beta.resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Beta Result', projectId)]));

    expect(await screen.findByText('Beta Result')).toBeInTheDocument();
    alpha.resolve(json({ title: 'stale failure' }, 500));

    await waitFor(() => expect(screen.queryByText('stale failure')).not.toBeInTheDocument());
    expect(screen.queryByText('Alpha Result')).not.toBeInTheDocument();
  });

  it('does not render an error banner for an aborted stale request', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Active') {
        return abortableResponse(init?.signal ?? undefined);
      }

      if (url.pathname === '/api/projects' && url.searchParams.get('status') === 'Cancelled') {
        return Promise.resolve(projectListResponse([projectListItem('dev-sales', 'Cancelled', 'Abort Cancelled', cancelledProjectId)]));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByRole('tab', { name: '취소' }));

    expect(await screen.findByText('Abort Cancelled')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('downloads the xlsx panel template with the selected unit and server filename', async () => {
    let requestedSearch = '';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information/import/template`) {
        requestedSearch = url.search;
        return Promise.resolve(new Response(new Blob(['xlsx']), {
          status: 200,
          headers: {
            'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'Content-Disposition': "attachment; filename*=UTF-8''TASK-003A_Demo_Panel_Information_inch.xlsx"
          }
        }));
      }

      return mockFetch(input, init);
    }));

    let downloadedFileName = '';
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloadedFileName = this.download;
    });
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    fireEvent.click(await screen.findByRole('button', { name: '패널명·사이즈 수정' }));
    fireEvent.change(await screen.findByLabelText('입력 단위'), { target: { value: 'Inch' } });
    fireEvent.click(screen.getByRole('button', { name: 'Excel 양식 다운로드' }));

    expect(await screen.findByText('Excel 양식을 다운로드했습니다.')).toBeInTheDocument();
    expect(requestedSearch).toBe('?unit=inch');
    expect(clickSpy).toHaveBeenCalled();
    expect(downloadedFileName).toBe('TASK-003A_Demo_Panel_Information_inch.xlsx');
    expect(screen.queryByText(/CSV/i)).not.toBeInTheDocument();
  });

  it('shows panel template download only to panel information editors', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    expect(await screen.findByRole('button', { name: '패널명·사이즈 수정' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '패널명·사이즈 수정' }));
    expect(await screen.findByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-manufacturing' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    await screen.findByRole('tab', { name: '설계' });
    expect(screen.queryByRole('button', { name: '패널명·사이즈 수정' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
  });

  it('keeps UL891 design read-only in project detail and opens the dedicated edit page', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname;
      if (path === `/api/projects/${projectId}/set-structure`) {
        return json(ul891SetStructure());
      }
      if (path === `/api/projects/${projectId}/qr`) {
        return json({ projectId, eligibleCount: 0, issuedCount: 0, panels: [] });
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));

    expect(await screen.findByRole('table', { name: '저장된 세트 공통 설계정보' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '패널 QR' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '패널명·사이즈 수정' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('세트 사양명')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '수정' }));

    expect(await screen.findByText('UL891 설계 입력')).toBeInTheDocument();
    expect(screen.getByLabelText('세트 사양명')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '임시저장' })).not.toBeInTheDocument();
    expect(screen.queryByText(/v1/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Draft/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '저장' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '패널 QR' })).not.toBeInTheDocument();
  });

  it('shows one panel as a department workspace and preserves exact work links', async () => {
    window.history.pushState(null, '', `/projects/${projectId}/panels/${panelIds[0]}`);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      const path = url.pathname;
      if (path === `/api/projects/${projectId}/set-structure`) {
        return json({ projectId, structureMode: 'FlatPanel', isLegacyFlat: false, canEditOrder: false, canEditDesign: false, specs: [], orderedProcurementItems: [], recoveryCases: [] });
      }
      if (path === '/api/materials/kitting') {
        return json({ projects: [{
          projectId,
          projectCode: 'PJT-003A',
          projectTitle: 'TASK-003A Demo',
          activeItemCount: 1,
          completedItemCount: 1,
          ready: true,
          pendingPanelCount: 0,
          completedPanelCount: 1,
          panels: [{ panelId: panelIds[0], displayCode: 'P01', panelName: 'MAIN', panelInfoCompleted: true, kittingCompleted: true, completedAtUtc: '2026-07-04T01:00:00Z', completedByDisplayName: '담당자', selectable: false }]
        }] });
      }
      const manufacturingPanel = {
        panelId: panelIds[0], displayCode: 'P01', panelName: 'MAIN', workflowStage: 'ManufacturingInProgress', kittingCompleted: true,
        workItemId: 'work-1', workItemStatus: 'InProgress', executionId: 'execution-1', status: 'InProgress', version: 2,
        checkedStepCount: 2, totalStepCount: 4, activePendingId: null, activePendingNumber: null, actionDepartmentCode: null,
        startedAtUtc: '2026-07-05T01:00:00Z', completedAtUtc: null, canMutate: false
      };
      if (path === '/api/manufacturing/queue') {
        return json({ projects: [{ projectId, projectCode: 'PJT-003A', projectTitle: 'TASK-003A Demo', readyCount: 0, inProgressCount: 1, blockedCount: 0, completedCount: 0, panels: [manufacturingPanel] }] });
      }
      if (path === `/api/manufacturing/panels/${panelIds[0]}`) {
        return json({ panel: manufacturingPanel, steps: [{ stepId: 'step-1', sequenceNumber: 1, stepName: '조립', checked: true, checkedByDisplayName: '담당자', checkedAtUtc: '2026-07-05T01:10:00Z' }], events: [] });
      }
      if (path === '/api/quality/inspections/queue') {
        const stage = url.searchParams.get('stage');
        return json({ projects: stage === 'LQC' ? [{
          projectId, projectCode: 'PJT-003A', projectTitle: 'TASK-003A Demo', fatRequired: false, readyCount: 1, inProgressCount: 0, blockedCount: 0, completedCount: 0,
          panels: [{ panelId: panelIds[0], displayCode: 'P01', panelName: 'MAIN', workflowStage: 'ManufacturingCompleted', stageCode: 'LQC', stageLabel: 'LQC', workItemId: 'quality-work-1', workItemStatus: 'Requested', attemptId: null, attemptNumber: 0, status: 'Ready', version: 1, pendingId: null, pendingNumber: null, actionDepartmentCode: null, canMutate: false }]
        }] : [] });
      }
      if (path === `/api/quality/inspections/panels/${panelIds[0]}`) {
        return json({ panel: { panelId: panelIds[0], displayCode: 'P01', panelName: 'MAIN', workflowStage: 'ManufacturingCompleted', stageCode: 'LQC', stageLabel: 'LQC', workItemId: 'quality-work-1', workItemStatus: 'Requested', attemptId: null, attemptNumber: 0, status: 'Ready', version: 1, pendingId: null, pendingNumber: null, actionDepartmentCode: null, canMutate: false }, decisionMode: 'Checklist', reportId: null, reportStatus: null, reportVersion: null, result: null, reason: null, pdfStatus: null, items: [], responses: [], photos: [], history: [] });
      }
      if (path === '/api/logistics/queue') return json({ stage: url.searchParams.get('stage'), todayCount: 0, blockedCount: 0, projects: [], drafts: [] });
      if (path === `/api/logistics/projects/${projectId}/history`) {
        return json({ projectId, items: [{ targetId: 'packing-1', stage: 'packing', displayCode: 'PU-001', status: 'Finalized', version: 1, note: null, specification: null, weightText: null, departureDate: null, panelCodes: ['P01'], unitCodes: [], evidence: [], createdByName: '담당자', createdAtUtc: '2026-07-06T01:00:00Z', finalizedByName: '담당자', finalizedAtUtc: '2026-07-06T02:00:00Z', cancelledByName: null, cancelledAtUtc: null }] });
      }
      if (path === `/api/projects/${projectId}/qr`) {
        return json({ projectId, eligibleCount: 1, issuedCount: 1, panels: [{ panelId: panelIds[0], sequenceNumber: 1, displayCode: 'P01', displayName: 'MAIN', qrEligible: true, hasActiveQr: true, qr: { qrCodeId: 'qr-1', projectId, panelId: panelIds[0], status: 'Active', scanUrl: '/q/token', issuedByName: '담당자', issuedAtUtc: '2026-07-06T01:00:00Z' } }] });
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'P01 패널 상세' })).toBeInTheDocument();
    const summary = await screen.findByLabelText('패널 부서별 현재 상태');
    expect(within(summary).getByRole('button', { name: /키팅.*키팅 완료/ })).toBeInTheDocument();
    expect(within(summary).getByRole('button', { name: /제조.*제조 중/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: '자재·키팅' }));
    expect(window.location.search).toBe('?tab=materials');
    expect(screen.getByText('구매품목과 자재 입고는 아직 패널/BOM에 자동 귀속되지 않습니다. 임의로 이 패널의 자재라고 표시하지 않습니다.')).toBeInTheDocument();
    expect(screen.getByText('P01 키팅')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '키팅 업무 조회' }));
    expect(window.location.pathname).toBe('/materials/kitting');
    expect(new URLSearchParams(window.location.search).get('panel')).toBe(panelIds[0]);
  });

  it('shows a friendly template download server error', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information/import/template`) {
        return Promise.resolve(json({ title: '양식을 다운로드할 수 없습니다.' }, 500));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    fireEvent.click(await screen.findByRole('button', { name: '패널명·사이즈 수정' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Excel 양식 다운로드' }));

    expect(await screen.findByText('양식을 다운로드할 수 없습니다.')).toBeInTheDocument();
  });

  it('does not send size updates when only the panel name changes after switching the edit unit', async () => {
    const savedRequests: Array<{
      panels: Array<{
        panelNameUpdate?: { isChanged: boolean; value: string | null };
        sizeUpdate?: unknown;
      }>;
    }> = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json(panelInformationWithSize(projectId, 'DRIFT-B')));
      }

      if (url.pathname === `/api/projects/${projectId}/panel-information`) {
        return Promise.resolve(json(panelInformationWithSize(projectId, 'DRIFT-A')));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    fireEvent.click(await screen.findByRole('button', { name: '패널명·사이즈 수정' }));
    fireEvent.change(await screen.findByLabelText('입력 단위'), { target: { value: 'Inch' } });
    fireEvent.change(await screen.findByLabelText('No.1 패널명'), { target: { value: 'DRIFT-B' } });
    fireEvent.change(await screen.findByLabelText('수정사유*'), { target: { value: '패널명만 변경' } });
    fireEvent.click(screen.getByRole('button', { name: '직접 입력 저장' }));

    await screen.findByRole('heading', { name: 'TASK-003A Demo' });
    const savedBody = savedRequests[0];
    expect(savedBody.panels).toHaveLength(1);
    expect(savedBody.panels[0].panelNameUpdate).toEqual({ isChanged: true, value: 'DRIFT-B' });
    expect(savedBody.panels[0].sizeUpdate).toBeUndefined();
  });

  it('edits drawing numbers and renumbers current panel rows from one after regrouping', async () => {
    const savedRequests: Array<{
      panels: Array<{
        panelId: string;
        drawingNumberUpdate?: { isChanged: boolean; value: string | null };
        groupNumberUpdate?: { isChanged: boolean; value: number | null };
      }>;
    }> = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json(panelInformation(projectId)));
      }
      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    fireEvent.click(await screen.findByRole('button', { name: '패널명·사이즈 수정' }));

    expect(await screen.findByLabelText('이 프로젝트의 설계 필수 입력값')).toHaveTextContent('패널명 · W · H · D');
    expect(screen.getAllByRole('button', { name: '사이즈 입력 안내' })[0]).toHaveAttribute('title', '포장 업무에 필요한 패널의 최외곽 사이즈를 기재해주세요.');
    fireEvent.change(screen.getByLabelText('No.1 도번'), { target: { value: 'DWG-101' } });
    fireEvent.click(screen.getByLabelText('No.1 열반 선택'));
    fireEvent.click(screen.getByLabelText('No.2 열반 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 패널 열반' }));
    expect(screen.getAllByText('열반 1 · 사이즈 입력 필요').length).toBeGreaterThanOrEqual(2);

    fireEvent.click(screen.getByLabelText('No.3 열반 선택'));
    fireEvent.click(screen.getByLabelText('No.4 열반 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 패널 열반' }));
    expect(screen.getAllByText('열반 2 · 사이즈 입력 필요').length).toBeGreaterThanOrEqual(2);

    fireEvent.click(screen.getByLabelText('No.1 열반 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 열반 해제' }));
    expect(screen.queryByText('열반 2 · 사이즈 입력 필요')).not.toBeInTheDocument();
    expect(screen.getAllByText('열반 1 · 사이즈 입력 필요').length).toBeGreaterThanOrEqual(2);
    fireEvent.click(screen.getByRole('button', { name: '직접 입력 저장' }));

    await waitFor(() => expect(savedRequests).toHaveLength(1));
    const first = savedRequests[0].panels.find((panel) => panel.panelId === panelIds[0]);
    const third = savedRequests[0].panels.find((panel) => panel.panelId === panelIds[2]);
    const fourth = savedRequests[0].panels.find((panel) => panel.panelId === panelIds[3]);
    expect(first?.drawingNumberUpdate).toEqual({ isChanged: true, value: 'DWG-101' });
    expect(first?.groupNumberUpdate).toBeUndefined();
    expect(third?.groupNumberUpdate).toEqual({ isChanged: true, value: 1 });
    expect(fourth?.groupNumberUpdate).toEqual({ isChanged: true, value: 1 });
  });

  it('shows panel rows with the full combined W H D size in the design tab', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information`) {
        const grouped = panelInformation(projectId);
        Object.assign(grouped.panels[0], {
          panelName: 'GROUP-A', drawingNumber: 'DWG-A', widthMm: 800, heightMm: 1800, depthMm: 400,
          panelGroupNumber: 1, panelInfoCompleted: true, qrEligible: true
        });
        Object.assign(grouped.panels[1], {
          panelName: 'GROUP-B', drawingNumber: 'DWG-B', widthMm: 900, heightMm: 1700, depthMm: 350,
          panelGroupNumber: 1, panelInfoCompleted: true, qrEligible: true
        });
        return Promise.resolve(json(grouped));
      }
      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));

    const designTable = await screen.findByRole('table', { name: '설계' });
    const group = designTable.querySelector('.product-panel-group') as HTMLElement;
    expect(group).toBeInTheDocument();
    expect(group).toHaveTextContent('열반 1');
    expect(group).toHaveTextContent('No.1, No.2');
    expect(group).toHaveTextContent('열반 사이즈 1700 × 1800 × 400 mm');
    expect(group).toHaveTextContent('GROUP-A');
    expect(group).toHaveTextContent('DWG-A');
    expect(group).toHaveTextContent('800 × 1800 × 400 mm');
    expect(group).toHaveTextContent('GROUP-B');
    expect(group).toHaveTextContent('900 × 1700 × 350 mm');
  });

  it('confirms duplicate panel names before direct save', async () => {
    const savedRequests: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/panel-information` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json(panelInformation(projectId)));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    await screen.findByRole('button', { name: '신규 프로젝트' });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    fireEvent.click(await screen.findByRole('button', { name: '패널명·사이즈 수정' }));
    fireEvent.change(await screen.findByLabelText('No.1 패널명'), { target: { value: 'DUP-PANEL' } });
    fireEvent.change(await screen.findByLabelText('No.2 패널명'), { target: { value: ' dup-panel ' } });
    fireEvent.click(screen.getByRole('button', { name: '직접 입력 저장' }));

    const dialog = await screen.findByTestId('duplicate-panel-name-dialog');
    expect(dialog).toHaveTextContent('중복된 패널명이 있습니다.');
    expect(dialog).toHaveTextContent('DUP-PANEL: No.1, No.2');
    expect(savedRequests).toHaveLength(0);

    fireEvent.click(within(dialog).getByRole('button', { name: '취소' }));
    await waitFor(() => expect(screen.queryByTestId('duplicate-panel-name-dialog')).not.toBeInTheDocument());
    expect(savedRequests).toHaveLength(0);

    fireEvent.click(screen.getByRole('button', { name: '직접 입력 저장' }));
    fireEvent.click(within(await screen.findByTestId('duplicate-panel-name-dialog')).getByRole('button', { name: '중복이어도 저장' }));

    await waitFor(() => expect(savedRequests).toHaveLength(1));
  });

  it('shows direct, excel, canonical, original input, and legacy panel audit metadata', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));

    expect(await screen.findByText('전체 이력')).toBeInTheDocument();
    expect(await screen.findByText('직접 입력 · 대상 패널 1면')).toBeInTheDocument();
    expect(await screen.findByText('Excel 입력 · 대상 패널 1면')).toBeInTheDocument();
    expect(await screen.findByText('기존 이력 · 대상 패널 1면')).toBeInTheDocument();
    expect(screen.getByText('입력 파일: panel_information_01.xlsx')).toBeInTheDocument();
    expect(screen.getAllByText('변경항목 1건').length).toBeGreaterThanOrEqual(3);
    fireEvent.click(screen.getAllByText('변경 상세')[0]);
    expect(screen.getByText('원본 입력값: 31.5 inch')).toBeInTheDocument();
    expect(screen.getByText('입력단위: inch')).toBeInTheDocument();
    expect(screen.getByText('W: 700 → 800.1')).toBeInTheDocument();
  });

  it('keeps procurement read-only on project detail and exposes edit only to Procurement', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    expect(await screen.findByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByText('구매정보')).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));

    const procurementSection = (await screen.findByText('구매정보')).closest('section');
    expect(procurementSection).not.toBeNull();
    expect(within(procurementSection as HTMLElement).getByRole('tab', { name: /도급 구매품/ })).toHaveAttribute('aria-selected', 'true');
    expect(within(procurementSection as HTMLElement).getByText('Completed Relay')).toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).getAllByText('Vendor A').length).toBeGreaterThan(0);
    fireEvent.click(within(procurementSection as HTMLElement).getByRole('tab', { name: /사급 자재/ }));
    expect(within(procurementSection as HTMLElement).getByText('Relay')).toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).getAllByText('사급 자재').length).toBeGreaterThanOrEqual(2);
    expect(within(procurementSection as HTMLElement).getByText('100 EA')).toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).getByText('부분 입고 24/100 EA')).toBeInTheDocument();
    fireEvent.click(within(procurementSection as HTMLElement).getByRole('tab', { name: /도급 구매품/ }));
    expect(within(procurementSection as HTMLElement).getByText('입고 확정(6/7 12:30)')).toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByText('출하일')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByText('예정일까지')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByText('D-3')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByDisplayValue('Relay')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByText('입고지연')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).queryByText('미입고')).not.toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).getByRole('button', { name: '구매정보 수정' })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    const materialsProcurementSection = (await screen.findByText('구매정보')).closest('section');
    expect(within(materialsProcurementSection as HTMLElement).queryByRole('button', { name: '구매정보 수정' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '자재' }).length).toBeGreaterThan(0);
  });

  it('renders procurement read-only cards on mobile without horizontal table assumptions', async () => {
    mockMobileViewport(true);
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    const firstCard = within(await screen.findByTestId('project-list-mobile')).getAllByRole('button', { name: '상세 보기' })[0];
    fireEvent.click(firstCard);
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));

    let procurementMobile = await screen.findByTestId('procurement-mobile');
    expect(procurementMobile).toHaveTextContent('Completed Relay');
    expect(procurementMobile).toHaveTextContent('도급 구매품');
    fireEvent.click(screen.getByRole('tab', { name: /사급 자재/ }));
    procurementMobile = await screen.findByTestId('procurement-mobile');
    expect(procurementMobile).toHaveTextContent('Relay');
    expect(procurementMobile).toHaveTextContent('업체Vendor A');
    expect(procurementMobile).toHaveTextContent('기술 담당자Owner A');
    expect(procurementMobile).toHaveTextContent('입고예정2026-06-29');
    expect(procurementMobile).toHaveTextContent('사급 자재');
    expect(procurementMobile).toHaveTextContent('제공 예정100 EA');
    expect(procurementMobile).not.toHaveTextContent('예정일까지');
    expect(procurementMobile).not.toHaveTextContent('D-3');
    expect(procurementMobile).not.toHaveTextContent('입고지연');
    expect(procurementMobile).not.toHaveTextContent('부분입고');
  });

  it('keeps procurement Excel controls on the edit page and saves partial rows', async () => {
    const savedRequests: unknown[] = [];
    let failProcurementRefresh = false;
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/procurement` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        failProcurementRefresh = true;
        return Promise.resolve(json(procurementResponse()));
      }
      if (url.pathname === `/api/projects/${projectId}/procurement` && failProcurementRefresh && !init?.method) {
        failProcurementRefresh = false;
        return Promise.resolve(json({ title: '최신 구매정보를 불러오지 못했습니다.' }, 503));
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    fireEvent.click(await screen.findByRole('button', { name: '구매정보 수정' }));

    const contextSummary = await screen.findByTestId('project-context-summary');
    expect(contextSummary).toHaveTextContent('TASK-003A Demo');
    expect(contextSummary).toHaveTextContent('EMI Test Customer');
    expect(contextSummary).toHaveTextContent('PJT-003A');
    expect(contextSummary).toHaveTextContent('UL67');
    expect(contextSummary).toHaveTextContent('2026-10-10');
    expect(contextSummary).toHaveTextContent('목포장');
    expect(contextSummary).toHaveTextContent('진행');
    expect(contextSummary).not.toHaveTextContent('Active');
    expect(await screen.findByRole('table', { name: '구매정보 수정' })).toBeInTheDocument();
    const editTable = screen.getByRole('table', { name: '구매정보 수정' });
    const legacyCategorySelect = within(editTable).getByLabelText('구매품 구분');
    expect(legacyCategorySelect).toBeEnabled();
    expect(within(legacyCategorySelect).getByRole('option', { name: '기타' })).toBeInTheDocument();
    fireEvent.change(legacyCategorySelect, { target: { value: '67000000-0000-0000-0000-000000000005' } });
    expect(within(editTable).getAllByLabelText('공급 방식')[0]).toHaveValue('Purchased');
    expect(within(editTable).getByLabelText('발주 수량')).toHaveValue('');
    expect(within(editTable).getByLabelText('발주 단위')).toHaveValue('');
    fireEvent.change(within(editTable).getByLabelText('발주 수량'), { target: { value: '25' } });
    fireEvent.change(within(editTable).getByLabelText('발주 단위'), { target: { value: 'EA' } });
    fireEvent.click(screen.getByRole('tab', { name: /사급 자재/ }));
    expect(within(editTable).getAllByLabelText('공급 방식')[0]).toHaveValue('CustomerSupplied');
    expect(within(editTable).getByLabelText('제공 예정 수량')).toHaveValue('100');
    expect(within(editTable).getByLabelText('제공 예정 단위')).toHaveValue('EA');
    expect(editTable).not.toHaveTextContent('PJT Code');
    expect(editTable).not.toHaveTextContent('예정일까지');
    expect(screen.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 업로드' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: /도급 구매품/ }));
    fireEvent.click(screen.getByRole('button', { name: '도급 구매품 행 추가' }));
    const inputs = within(editTable).getAllByRole('textbox');
    fireEvent.change(inputs[0], { target: { value: '8W' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    await screen.findByRole('heading', { name: 'TASK-003A Demo' });
    expect(screen.getByRole('tab', { name: '구매' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByLabelText('최근 저장 결과')).toHaveTextContent('구매정보를 저장했습니다. 최신 화면을 불러오지 못했습니다. 새로고침해 주세요.');
    expect(screen.getByLabelText('최근 저장 결과').querySelector('[data-tone="partial"]')).not.toBeNull();
    expect(JSON.stringify(savedRequests[0])).toContain('8W');
    expect(JSON.stringify(savedRequests[0])).toContain('25');
    expect(JSON.stringify(savedRequests[0])).toContain('67000000-0000-0000-0000-000000000005');
  });

  it('waits for the latest procurement edit load before accepting row input', async () => {
    const editLoadResolvers: Array<(response: Response) => void> = [];
    let deferEditLoads = false;
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (deferEditLoads
          && url.pathname === `/api/projects/${projectId}/procurement`
          && (!init?.method || init.method === 'GET')) {
        return new Promise<Response>((resolve) => editLoadResolvers.push(resolve));
      }

      return mockFetch(input, init);
    }));

    render(<StrictMode><App /></StrictMode>);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    deferEditLoads = true;
    fireEvent.click(await screen.findByRole('button', { name: '구매정보 수정' }));

    await waitFor(() => expect(editLoadResolvers).toHaveLength(2));
    expect(screen.getByRole('status')).toHaveTextContent('프로젝트·구매정보 확인 중에는 입력할 수 없습니다.');
    for (const actionName of ['도급 구매품 행 추가', 'Excel 양식 다운로드', 'Excel 업로드', '저장']) {
      expect(screen.getByRole('button', { name: actionName })).toBeDisabled();
    }
    await act(async () => {
      editLoadResolvers[0](json({ ...procurementResponse(), items: [] }));
      await Promise.resolve();
    });
    expect(screen.queryByRole('table', { name: '구매정보 수정' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '도급 구매품 행 추가' })).toBeDisabled();

    await act(async () => {
      editLoadResolvers[1](json(procurementResponse()));
      await Promise.resolve();
    });
    const editTable = await screen.findByRole('table', { name: '구매정보 수정' });
    expect(screen.queryByText('프로젝트·구매정보 확인 중에는 입력할 수 없습니다.')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '도급 구매품 행 추가' })).toBeEnabled();
    const initialRowCount = editTable.querySelectorAll('.procurement-table-row.editable').length;
    fireEvent.click(screen.getByRole('button', { name: '도급 구매품 행 추가' }));
    expect(editTable.querySelectorAll('.procurement-table-row.editable')).toHaveLength(initialRowCount + 1);
  });

  it('keeps material category required for category-based projects', async () => {
    let patchCount = 0;
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/procurement` && init?.method === 'PATCH') {
        patchCount += 1;
      }
      if (url.pathname === `/api/projects/${projectId}/procurement` && !init?.method) {
        return Promise.resolve(json({ ...procurementResponse(), iqcRoutingPolicy: 'CategoryBased', items: [] }));
      }
      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    fireEvent.click(await screen.findByRole('button', { name: '구매정보 수정' }));
    fireEvent.click(await screen.findByRole('button', { name: '도급 구매품 행 추가' }));
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    const alert = (await screen.findByText('저장하지 못한 위치')).closest('[role="alert"]') as HTMLElement;
    expect(alert).toHaveTextContent('구매품 구분을 선택해 주세요.');
    expect(screen.getByLabelText('구매품 구분')).toBeRequired();
    expect(patchCount).toBe(0);
  });

  it('identifies the exact purchased row, field, and correction when quantity input is incomplete', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    fireEvent.click(await screen.findByRole('button', { name: '구매정보 수정' }));

    const editTable = await screen.findByRole('table', { name: '구매정보 수정' });
    fireEvent.change(within(editTable).getByLabelText('발주 수량'), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    const issuePanel = await screen.findByText('저장하지 못한 위치');
    const alert = issuePanel.closest('[role="alert"]') as HTMLElement;
    expect(alert).toHaveTextContent('도급 구매품 · 1번째 행');
    expect(alert).toHaveTextContent('Completed Relay');
    expect(alert).toHaveTextContent('문제 필드발주 단위');
    expect(alert).toHaveTextContent('발주 수량과 단위는 구매팀이 함께 입력해야');
    expect(within(editTable).getByLabelText('발주 단위')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows project context on product detail and simplifies procurement Excel preview sections', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '설계' }));
    const productTable = await screen.findByRole('table', { name: '설계' });
    fireEvent.click(within(productTable).getAllByRole('row')[1]);

    const productContext = await screen.findByTestId('project-context-summary');
    expect(productContext).toHaveTextContent('TASK-003A Demo');
    expect(productContext).toHaveTextContent('PJT-003A');
    expect(productContext).toHaveTextContent('진행');
    expect(productContext).not.toHaveTextContent('Active');
    expect(screen.getByLabelText('패널 요약')).toHaveTextContent('No.1');
    expect(screen.getByLabelText('패널 요약')).toHaveTextContent('패널 상태');
    expect(screen.queryByText('W/H/D')).not.toBeInTheDocument();
    expect(screen.queryByText('QR 조건')).not.toBeInTheDocument();

    fireEvent.click(screen.getAllByRole('button', { name: '프로젝트' }).at(-1) as HTMLElement);
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));
    fireEvent.click(await screen.findByRole('button', { name: '구매정보 수정' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Excel 업로드' }));

    const dialog = await screen.findByRole('dialog', { name: '구매 Excel 업로드' });
    const fileInput = dialog.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [new File(['xlsx'], 'procurement.xlsx')] } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Preview' }));

    expect(await within(dialog).findByRole('table', { name: '저장 가능한 데이터 목록' })).toBeInTheDocument();
    expect(within(dialog).getByRole('table', { name: '저장 불가능한 데이터 목록' })).toBeInTheDocument();
    expect(within(dialog).getByText('저장 가능한 데이터 목록 1건')).toBeInTheDocument();
    expect(within(dialog).getByText('저장 불가능한 데이터 목록 1건')).toBeInTheDocument();
    expect(within(dialog).getAllByText('Excel 행').length).toBeGreaterThanOrEqual(2);
    expect(within(dialog).getAllByText('통상납기')).toHaveLength(2);
    expect(within(dialog).queryByText('결과')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('해결 방법')).not.toBeInTheDocument();
    expect(within(dialog).getByText('사유')).toBeInTheDocument();
    expect(within(dialog).getByText('필드')).toBeInTheDocument();
    expect(within(dialog).getByText('입력값')).toBeInTheDocument();
    expect(within(dialog).getByText('문제')).toBeInTheDocument();
    expect(within(dialog).getByText('확인할 프로젝트가 있습니다. 프로젝트를 선택해 주세요.')).toBeInTheDocument();
    expect(within(dialog).queryByText(/오류 \d+건/)).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/확인 필요 \d+건/)).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/저장할 수 없는 행/)).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/QMS/i)).not.toBeInTheDocument();

    const previewHeader = dialog.querySelector('.excel-preview-head');
    expect(previewHeader).not.toBeNull();
    expect(previewHeader).toHaveClass('excel-preview-head');

    const dialogContent = dialog.querySelector('.dialog') as HTMLElement;
    fireEvent.mouseDown(dialogContent);
    expect(screen.getByRole('dialog', { name: '구매 Excel 업로드' })).toBeInTheDocument();
    fireEvent.mouseDown(dialog);
    expect(screen.queryByRole('dialog', { name: '구매 Excel 업로드' })).not.toBeInTheDocument();
  });

  it('shows procurement dashboard KPI, project list, selected project details, and global Excel upload', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '구매' }));

    expect(await screen.findByText('입고대기품목')).toBeInTheDocument();
    expect(screen.getByText('입고완료품목')).toBeInTheDocument();
    expect(screen.getByText('입고예정일 경과 품목')).toBeInTheDocument();
    expect(screen.queryByText('전체 구매 프로젝트')).not.toBeInTheDocument();
    expect(screen.queryByText('7일 내 입고예정')).not.toBeInTheDocument();
    expect(screen.queryByText('입고지연')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 업로드' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Excel 양식 다운로드' }));
    expect(await screen.findByText('Excel 양식을 다운로드했습니다.')).toBeInTheDocument();
    const projectTable = screen.getByRole('table', { name: '구매 프로젝트 목록' });
    expect(projectTable).toBeInTheDocument();
    expect(within(projectTable).getByText('TASK-003A Demo')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: '구매정보' })).not.toBeInTheDocument();
    const projectRow = within(projectTable).getByRole('row', { name: /TASK-003A Demo.*PJT-003A/ });
    fireEvent.click(projectRow);
    const expanded = await screen.findByLabelText('TASK-003A Demo 구매정보');
    expect(expanded).toHaveTextContent('Relay');
    expect(expanded).not.toHaveTextContent('출하일');
    fireEvent.click(projectRow);
    await waitFor(() => expect(screen.queryByLabelText('TASK-003A Demo 구매정보')).not.toBeInTheDocument());
  });

  it('shows procurement required item settings to Procurement users', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '구매' }));

    fireEvent.click(await screen.findByRole('button', { name: '구매 필수 항목 설정' }));
    expect(await screen.findByRole('heading', { name: '구매 필수 항목 설정' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'UL67' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('tab', { name: 'TEST-TYPE' })).not.toBeInTheDocument();
    expect(screen.getByText('UL67 필수 구매 항목')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    expect(await screen.findByText('구매 필수 항목 설정을 저장했습니다.')).toBeInTheDocument();
  });

  it('shows production planning workspace, detail section, and edit-only controls for Production Planning', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-production' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산관리' }));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산계획' }));

    const productionSummary = await screen.findByLabelText('생산계획 요약');
    expect(productionSummary).toHaveTextContent('생산계획 미등록');
    expect(productionSummary).toHaveTextContent('작성 중');
    expect(productionSummary).toHaveTextContent('계획 완료');
    expect(screen.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 업로드' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Excel 업로드' }));
    const productionExcelDialog = await screen.findByRole('dialog', { name: '생산계획 Excel 업로드' });
    expect(productionExcelDialog.querySelector('input[type="file"]')).not.toBeNull();
    expect(within(productionExcelDialog).getByRole('button', { name: 'Preview' })).toBeInTheDocument();
    fireEvent.click(within(productionExcelDialog).getByRole('button', { name: '닫기' }));
    expect(screen.queryByRole('dialog', { name: '생산계획 Excel 업로드' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '생산계획 단계 설정' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '생산계획 단계 설정' }));
    expect(await screen.findByRole('heading', { name: '생산계획 단계 설정' })).toBeInTheDocument();
    expect(screen.getByText('생산계획 단계 설정은 이후 새로 작성되는 생산계획부터 적용됩니다. 이미 작성된 프로젝트 생산계획은 자동으로 변경되지 않습니다.')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'UL67' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('tab', { name: 'TEST-TYPE' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: 'UL67 생산계획 단계 설정' })).getByDisplayValue('자재 입고')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '행 추가' })).toBeInTheDocument();
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산계획' }));

    const productionTable = await screen.findByRole('table', { name: '생산계획 프로젝트 목록' });
    expect(productionTable).toHaveTextContent('프로젝트명CodeItem면수납기일생산계획 상태');
    expect(productionTable).not.toHaveTextContent('제품 구분');
    expect(productionTable).not.toHaveTextContent('담당자');
    expect(productionTable).not.toHaveTextContent('작업');
    fireEvent.click(within(productionTable).getByRole('row', { name: /TASK-003A Demo/ }));
    const expanded = await screen.findByLabelText('선택 프로젝트 생산계획');
    expect(within(expanded).getByRole('button', { name: '프로젝트 상세에서 보기' })).toBeInTheDocument();
    expect(within(expanded).getByRole('button', { name: '생산계획 수정' })).toBeInTheDocument();
    expect(await within(expanded).findByText('자재 입고')).toBeInTheDocument();
    expect(within(expanded).getByLabelText('영업 담당자')).toHaveTextContent('정Dev Sales User');
    expect(within(expanded).getByLabelText('설계 담당자')).toHaveTextContent('정Dev Design User');
    expect(within(expanded).getByLabelText('구매 담당자')).toHaveTextContent('정Dev Procurement User');
    expect(within(expanded).queryByLabelText('패널 제조 투입 요청')).not.toBeInTheDocument();
    expect(expanded).not.toHaveTextContent('알림 기준');
    expect(expanded).not.toHaveTextContent('fallback');
    expect(within(expanded).queryByRole('table', { name: '생산계획 캘린더 표' })).not.toBeInTheDocument();
    expect(expanded).not.toHaveTextContent('검수 공휴일');

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '제조 투입' }));
    expect(screen.queryByLabelText('선택 프로젝트 제조 투입')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('생산계획 요약')).not.toBeInTheDocument();
    expect(await screen.findByLabelText('제조 투입 요약')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 업로드' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '생산계획 단계 설정' })).not.toBeInTheDocument();
    const releaseTable = screen.getByRole('table', { name: '제조 투입 프로젝트 목록' });
    expect(releaseTable).toHaveTextContent('프로젝트명CodeItem면수납기일업무');
    expect(releaseTable).not.toHaveTextContent('생산계획 상태');
    fireEvent.click(within(releaseTable).getByRole('row', { name: /TASK-003A Demo/ }));
    const releaseExpanded = await screen.findByLabelText('선택 프로젝트 제조 투입');
    expect(within(releaseExpanded).queryByRole('button', { name: '생산계획 수정' })).not.toBeInTheDocument();
    const releasePanel = within(releaseExpanded).getByLabelText('패널 제조 투입 요청');
    expect(within(releasePanel).getByRole('heading', { name: '제조 투입 요청' })).toBeInTheDocument();
    expect(within(releasePanel).getByText('자재 입고 3/4')).toBeInTheDocument();
    expect(within(releasePanel).getAllByText('키팅 미보고')).toHaveLength(3);
    fireEvent.click(within(releasePanel).getByRole('checkbox', { name: /PANEL-01/u }));
    fireEvent.click(within(releasePanel).getByRole('button', { name: '선택 1면 제조 투입 요청' }));
    expect(await within(releasePanel).findByText('1면을 제조팀에 투입 요청했습니다. 제조 업무 1건이 생성되었습니다.')).toBeInTheDocument();
    await waitFor(() => expect(within(releasePanel).getByText('투입 요청됨')).toBeInTheDocument());

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '프로젝트' }));
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    const detailTabs = await screen.findAllByRole('tab');
    expect(detailTabs.map((tab) => tab.textContent)).toEqual(expect.arrayContaining(['설계', '생산관리', '구매']));
    fireEvent.click(screen.getByRole('tab', { name: '전체 흐름' }));
    const workflowHeading = await screen.findByRole('heading', { name: '프로젝트 전체 흐름' });
    const workflowBoard = workflowHeading.closest('section');
    expect(workflowBoard).not.toBeNull();
    expect(workflowBoard!.querySelectorAll('.workflow-stage-item')).toHaveLength(6);
    expect(workflowBoard).toHaveTextContent('생산관리 / 제조 요청');
    expect(workflowBoard).not.toHaveTextContent('자재 / 제조 요청 (선택)');
    expect(workflowBoard).toHaveTextContent('물류 / 포장');
    expect(workflowBoard).toHaveTextContent('물류 / 납품');
    expect(workflowBoard).toHaveTextContent('영업 / 세금계산서');
    expect(workflowBoard).toHaveTextContent('업무 요청됨');
    expect(workflowBoard).not.toHaveTextContent('내 업무');
    fireEvent.click(screen.getByRole('tab', { name: '생산관리' }));
    expect(await screen.findByText('프로젝트 단위 계획과 담당자 지정')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '프로젝트 전체 흐름' })).not.toBeInTheDocument();
    expect(document.querySelectorAll('.workflow-stage-item')).toHaveLength(0);
    expect(screen.getByRole('button', { name: '생산계획 수정' })).toBeInTheDocument();
    const assigneeSummary = screen.getByLabelText('담당자 지정 현황');
    expect(within(assigneeSummary).getByLabelText('영업 담당자')).toHaveAttribute('data-tone', 'sales');
    expect(within(assigneeSummary).getByLabelText('설계 담당자')).toHaveAttribute('data-tone', 'design');
    expect(within(assigneeSummary).getByLabelText('생산관리 담당자')).toHaveAttribute('data-tone', 'production');
    expect(within(assigneeSummary).getByLabelText('구매 담당자')).toHaveAttribute('data-tone', 'procurement');
    expect(within(assigneeSummary).getByLabelText('자재 담당자')).toHaveAttribute('data-tone', 'materials');
    expect(within(assigneeSummary).getByLabelText('제조 담당자')).toHaveAttribute('data-tone', 'manufacturing');
    expect(within(assigneeSummary).getByLabelText('물류 담당자')).toHaveAttribute('data-tone', 'logistics');
    expect(within(assigneeSummary).getByLabelText('품질 담당자')).toHaveAttribute('data-tone', 'quality');
    expect(within(assigneeSummary).getByLabelText('품질 담당자')).toHaveTextContent('IQC 수입검사');
    expect(within(assigneeSummary).getByLabelText('품질 담당자')).toHaveTextContent('전진검수/FAT');
    expect(assigneeSummary).not.toHaveTextContent('알림 기준');
    expect(assigneeSummary).not.toHaveTextContent('fallback');
    expect(within(assigneeSummary).queryByRole('combobox')).not.toBeInTheDocument();
    const planItemsTable = await screen.findByRole('table', { name: '생산계획표' });
    expect(planItemsTable).toHaveTextContent('계획 항목계획 기간실적 기간연결 실적진행상태담당자필요 인원코멘트');
    expect(planItemsTable).toHaveTextContent('물류 · 포장 완료');
    expect(planItemsTable).toHaveTextContent('Dev Production Planning User');
    expect(planItemsTable).toHaveTextContent('3명');
    expect(planItemsTable).not.toHaveTextContent('No');
    const gantt = await screen.findByLabelText('생산계획 계획 실적 일정표');
    expect(gantt.querySelector('[data-bar="plan"]')).not.toBeNull();
    expect(gantt.querySelector('[data-bar="actual"]')).not.toBeNull();
    expect(screen.queryByRole('table', { name: '생산계획 캘린더 표' })).not.toBeInTheDocument();
    expect(screen.queryByText('검수 공휴일')).not.toBeInTheDocument();
    expect(planItemsTable.compareDocumentPosition(assigneeSummary) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(gantt.compareDocumentPosition(assigneeSummary) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: '생산계획 수정' }));
    const projectContext = await screen.findByTestId('project-context-summary');
    expect(projectContext).toHaveTextContent('TASK-003A Demo');
    expect(projectContext).toHaveTextContent('PJT-003A');
    expect(projectContext).not.toHaveTextContent('Active');
    const planEditSection = (await screen.findByRole('heading', { name: '프로젝트 생산계획표' })).closest('section');
    expect(planEditSection).not.toBeNull();
    expect(planEditSection).toHaveTextContent('계획 시작');
    expect(planEditSection).toHaveTextContent('계획 종료');
    expect(planEditSection).toHaveTextContent('실적 데이터 1:1 연결');
    expect(within(planEditSection!).getAllByRole('option', { name: '전체 구매품' }).length).toBeGreaterThan(0);
    expect(within(planEditSection!).getAllByRole('option', { name: '외함' }).length).toBeGreaterThan(0);
    expect(within(planEditSection!).getAllByLabelText('담당자')[0]).toHaveValue('50000000-0000-0000-0000-000000000003');
    expect(within(planEditSection!).getAllByLabelText('필요 인원')[0]).toHaveValue(3);
    const addPlanRowButton = within(planEditSection!).getByRole('button', { name: '계획 항목 추가' });
    fireEvent.click(addPlanRowButton);
    fireEvent.click(addPlanRowButton);
    let planNameInputs = within(planEditSection!).getAllByLabelText('계획 항목');
    fireEvent.change(planNameInputs.at(-2)!, { target: { value: '추가 계획 유지' } });
    fireEvent.change(planNameInputs.at(-1)!, { target: { value: '추가 계획 삭제' } });
    let connectionSelects = within(planEditSection!).getAllByRole('combobox', { name: /연결할 실적/ });
    fireEvent.change(connectionSelects.at(-2)!, { target: { value: 'PACKED:' } });
    fireEvent.change(connectionSelects.at(-1)!, { target: { value: 'PACKED:' } });
    fireEvent.click(within(planEditSection!).getAllByRole('button', { name: '삭제' }).at(-1)!);
    fireEvent.click(addPlanRowButton);
    planNameInputs = within(planEditSection!).getAllByLabelText('계획 항목');
    fireEvent.change(planNameInputs.at(-1)!, { target: { value: '삭제 후 재추가 계획' } });
    connectionSelects = within(planEditSection!).getAllByRole('combobox', { name: /연결할 실적/ });
    fireEvent.change(connectionSelects.at(-1)!, { target: { value: 'PACKED:' } });
    expect(screen.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 업로드' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Excel 업로드' }));
    const projectProductionExcelDialog = await screen.findByRole('dialog', { name: '생산계획 Excel 업로드' });
    expect(projectProductionExcelDialog.querySelector('input[type="file"]')).not.toBeNull();
    expect(within(projectProductionExcelDialog).getByText('현재 프로젝트: TASK-003A Demo')).toBeInTheDocument();
    expect(within(projectProductionExcelDialog).getByRole('button', { name: 'Preview' })).toBeInTheDocument();
    fireEvent.click(within(projectProductionExcelDialog).getByRole('button', { name: '닫기' }));
    expect(screen.queryByRole('dialog', { name: '생산계획 Excel 업로드' })).not.toBeInTheDocument();
    expect(screen.getByText('프로젝트 담당자 지정')).toBeInTheDocument();
    expect(screen.getByText('부서별 담당자')).toBeInTheDocument();
    expect(screen.getByText('품질 검사 담당자')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '영업' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '설계' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '생산관리' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '구매' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '자재' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '제조' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '물류' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'IQC 수입검사' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'LQC' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'OQC 자체검수' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '전진검수/FAT' })).toBeInTheDocument();
    expect(screen.getByLabelText('영업 정')).toBeInTheDocument();
    expect(screen.getByLabelText('영업 부')).toBeInTheDocument();
    expect(screen.getByLabelText('IQC 정')).toBeInTheDocument();
    expect(screen.getByLabelText('IQC 부')).toBeInTheDocument();
    expect(screen.getByLabelText('영업 담당자 지정')).toHaveAttribute('data-tone', 'sales');
    expect(screen.getByLabelText('설계 담당자 지정')).toHaveAttribute('data-tone', 'design');
    expect(screen.getByLabelText('품질 검사 담당자').querySelectorAll('[data-tone="quality"]').length).toBeGreaterThanOrEqual(4);
    const assigneeEditSection = screen.getByRole('heading', { name: '프로젝트 담당자 지정' }).closest('section');
    expect(assigneeEditSection).not.toBeNull();
    expect(assigneeEditSection!).not.toHaveTextContent('비고');
    expect(assigneeEditSection!).not.toHaveTextContent('fallback');
    expect(assigneeEditSection!).not.toHaveTextContent('알림 기준');
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    const saveCall = vi.mocked(fetch).mock.calls.find(([input, init]) =>
      String(input).includes(`/api/projects/${projectId}/production-planning`) && init?.method === 'PATCH');
    expect(saveCall).toBeDefined();
    const savedPayload = JSON.parse(String(saveCall![1]?.body));
    expect(savedPayload.items.map((item: { sequenceNumber: number }) => item.sequenceNumber))
      .toEqual(savedPayload.items.map((_: unknown, index: number) => index + 1));
    expect(savedPayload.items.map((item: { stepName: string }) => item.stepName)).toContain('추가 계획 유지');
    expect(savedPayload.items.map((item: { stepName: string }) => item.stepName)).toContain('삭제 후 재추가 계획');
    expect(savedPayload.items.map((item: { stepName: string }) => item.stepName)).not.toContain('추가 계획 삭제');
    expect(await screen.findByText('프로젝트 단위 계획과 담당자 지정')).toBeInTheDocument();
    expect(screen.getByLabelText('최근 저장 결과')).toHaveTextContent('생산계획을 저장했습니다.');

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '생산관리' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: '생산계획 수정' })).not.toBeInTheDocument());
  }, 30_000);

  it('hides production planning Excel upload controls from users without Production Planning update permission', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산관리' }));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산계획' }));

    expect(await screen.findByLabelText('생산계획 요약')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 업로드' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
  });

  it('lets a department head edit only their own project assignees', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '생산관리' }));

    fireEvent.click(await screen.findByRole('button', { name: '생산계획 수정' }));
    expect(await screen.findByRole('heading', { name: '설계 담당자 지정' })).toBeInTheDocument();
    expect(screen.getByText('본인 부서 담당자만 지정할 수 있습니다. 생산계획과 다른 부서 담당자는 프로젝트 조회 화면에서 확인하세요.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '설계' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '영업' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '생산관리' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '프로젝트 생산계획표' })).not.toBeInTheDocument();
    expect(screen.queryByText('실적 데이터 1:1 연결')).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('설계 정'), {
      target: { value: '50000000-0000-0000-0000-000000000010' }
    });
    fireEvent.click(screen.getByRole('button', { name: '담당자 저장' }));

    await waitFor(() => {
      const saveCall = vi.mocked(fetch).mock.calls.find(([input, init]) =>
        String(input).includes(`/api/projects/${projectId}/production-planning/department-assignees`)
        && init?.method === 'PATCH');
      expect(saveCall).toBeDefined();
      const payload = JSON.parse(String(saveCall![1]?.body));
      expect(payload.assignees.map((assignee: { responsibilityType: string }) => assignee.responsibilityType))
        .toEqual(['DesignPrimary', 'DesignSecondary']);
    });
    expect(await screen.findByText('프로젝트 단위 계획과 담당자 지정')).toBeInTheDocument();
    expect(screen.getByLabelText('최근 저장 결과')).toHaveTextContent('설계 담당자를 저장했습니다.');
  });

  it('opens UL891 production planning on the all-set default and renders readable schedule marks and assignees', async () => {
    useSetScopedProductionPlan = true;
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-production' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '생산관리' }));

    const gantt = await screen.findByLabelText('생산계획 계획 실적 일정표');
    expect(gantt.querySelector('[data-bar="plan"]')).not.toBeNull();
    expect(gantt.querySelector('[data-bar="actual"]')).not.toBeNull();
    expect(gantt.querySelectorAll('.production-control-gantt-gridline').length).toBeGreaterThan(2);
    const assigneeSummary = screen.getByLabelText('담당자 지정 현황');
    expect(gantt.compareDocumentPosition(assigneeSummary) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(assigneeSummary).toHaveTextContent('Dev Production Planning User');

    fireEvent.click(screen.getByRole('button', { name: '생산계획 수정' }));
    expect(await screen.findByRole('tab', { name: /전체 기본계획/ })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('heading', { name: '전체 세트 기본계획' })).toBeInTheDocument();
    expect(screen.getByText('빈 활성 세트와 이후 추가되는 세트에 적용됩니다.')).toBeInTheDocument();
    expect(screen.getByText('계획 구조에서 수정')).toBeInTheDocument();
    expect(screen.queryByText('담당자 지정')).not.toBeInTheDocument();
    expect(screen.getByText('전체 세트 기본계획 입력')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: /계획 구조/ }));
    const firstConnection = await screen.findByRole('combobox', { name: '자재 입고 연결할 실적' });
    const structureRow = firstConnection.closest('.production-control-project-fields') as HTMLElement | null;
    expect(structureRow).toHaveClass('is-structure-only');
    const requiredCheckbox = within(structureRow!).getByRole('checkbox');
    expect(requiredCheckbox.compareDocumentPosition(firstConnection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    fireEvent.click(screen.getByRole('tab', { name: /전체 기본계획/ }));
    fireEvent.change(screen.getByLabelText('수정사유'), { target: { value: '전체 세트 계획 입력' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining(`/api/projects/${projectId}/production-planning/set-defaults`),
      expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('"overwriteExisting":false') })
    ));
  }, 30_000);

  it('reorganizes project tabs around settlement, receipt confirmation, and panel-level execution status', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      const path = url.pathname;

      if (path === `/api/projects/${projectId}/settlement`) {
        return Promise.resolve(json({
          projectId,
          projectCode: 'PJT-003A',
          projectTitle: 'TASK-003A Demo',
          projectStatus: 'Active',
          settlementStatus: 'Draft',
          version: 3,
          activePanelCount: 4,
          deliveredPanelCount: 2,
          openPendingCount: 0,
          invoiceIssuedDate: '2026-07-18',
          invoiceNumber: 'INV-2026-0718',
          note: '회계 확인 메모',
          completedAtUtc: null,
          completedByName: null,
          allPanelsDelivered: false,
          noOpenPending: true,
          invoiceIssued: true,
          canComplete: false,
          canMutate: true,
          pendingLink: `/pending?projectId=${projectId}`,
          billingRequestStatus: {
            requested: true,
            batchId: '78000000-0000-0000-0000-000000000001',
            requestNumber: 16,
            requestedAtUtc: '2026-07-16T01:00:00Z',
            accountingIssueConfirmed: true
          }
        }));
      }

      if (path === '/api/materials/kitting') {
        return Promise.resolve(json({ projects: [{
          projectId,
          projectCode: 'PJT-003A',
          projectTitle: 'TASK-003A Demo',
          activeItemCount: 2,
          completedItemCount: 1,
          ready: true,
          pendingPanelCount: 3,
          completedPanelCount: 1,
          panels: [{
            panelId: panelIds[0],
            displayCode: 'P01',
            panelName: 'MAIN PANEL',
            panelInfoCompleted: true,
            kittingCompleted: true,
            completedAtUtc: '2026-07-04T01:00:00Z',
            completedByDisplayName: 'Dev Materials User',
            selectable: false
          }]
        }] }));
      }

      const manufacturingPanel = {
        panelId: panelIds[0],
        displayCode: 'P01',
        panelName: 'MAIN PANEL',
        workflowStage: 'ManufacturingCompleted',
        workItemId: '76000000-0000-0000-0000-000000000031',
        workItemStatus: 'Completed',
        executionId: '76000000-0000-0000-0000-000000000032',
        status: 'Completed',
        version: 4,
        checkedStepCount: 1,
        totalStepCount: 1,
        activePendingId: null,
        activePendingNumber: null,
        actionDepartmentCode: null,
        startedAtUtc: '2026-07-05T01:00:00Z',
        completedAtUtc: '2026-07-05T02:00:00Z',
        canMutate: false
      };
      if (path === '/api/manufacturing/queue') {
        return Promise.resolve(json({ projects: [{
          projectId,
          projectCode: 'PJT-003A',
          projectTitle: 'TASK-003A Demo',
          readyCount: 0,
          inProgressCount: 0,
          blockedCount: 0,
          completedCount: 1,
          panels: [manufacturingPanel]
        }] }));
      }
      if (path === `/api/manufacturing/panels/${panelIds[0]}`) {
        return Promise.resolve(json({
          panel: manufacturingPanel,
          steps: [{
            stepId: '76000000-0000-0000-0000-000000000033',
            sequenceNumber: 1,
            stepName: '조립 준비 확인',
            checked: true,
            checkedByDisplayName: 'Dev Manufacturing User',
            checkedAtUtc: '2026-07-05T01:20:00Z'
          }],
          events: [{
            eventId: '76000000-0000-0000-0000-000000000034',
            eventType: 'Completed',
            eventLabel: '제조 완료',
            stopReasonCode: null,
            stopDescription: null,
            pendingId: null,
            actorDisplayName: 'Dev Manufacturing User',
            createdAtUtc: '2026-07-05T02:00:00Z'
          }]
        }));
      }

      const qualityPanel = {
        panelId: panelIds[0],
        displayCode: 'P01',
        panelName: 'MAIN PANEL',
        workflowStage: 'LqcCompleted',
        stageCode: 'LQC',
        stageLabel: 'LQC',
        workItemId: '76000000-0000-0000-0000-000000000041',
        workItemStatus: 'Completed',
        attemptId: '76000000-0000-0000-0000-000000000042',
        attemptNumber: 1,
        status: 'Completed',
        version: 2,
        pendingId: null,
        pendingNumber: null,
        actionDepartmentCode: null,
        canMutate: false
      };
      const oqcPanel = {
        ...qualityPanel,
        workflowStage: 'OQC',
        stageCode: 'OQC',
        stageLabel: 'OQC',
        workItemId: '76000000-0000-0000-0000-000000000046',
        workItemStatus: 'Completed',
        attemptId: '76000000-0000-0000-0000-000000000047',
        status: 'Failed',
        version: 2,
        pendingId: '76000000-0000-0000-0000-000000000048',
        pendingNumber: 48,
        actionDepartmentCode: 'manufacturing'
      };
      if (path === '/api/quality/inspections/queue') {
        const stage = url.searchParams.get('stage');
        const stagePanel = stage === 'LQC' ? qualityPanel : stage === 'OQC' ? oqcPanel : null;
        return Promise.resolve(json({ projects: stagePanel ? [{
          projectId,
          projectCode: 'PJT-003A',
          projectTitle: 'TASK-003A Demo',
          fatRequired: false,
          readyCount: stage === 'LQC' ? 0 : 1,
          inProgressCount: 0,
          blockedCount: stage === 'OQC' ? 1 : 0,
          completedCount: stage === 'LQC' ? 1 : 0,
          panels: [stagePanel]
        }] : [] }));
      }
      if (path === `/api/quality/inspections/panels/${panelIds[0]}`) {
        const oqc = url.searchParams.get('stage') === 'OQC';
        const qualityItems = oqc
          ? [1, 2, 3, 4].map((order) => ({
              itemId: `76000000-0000-0000-0000-00000000006${order}`,
              itemCode: `OQC_${order}`,
              displayOrder: order,
              label: `OQC 단계 ${order}`,
              guidance: null,
              responseType: 'Check',
              isRequired: true,
              maxTextLength: null
            }))
          : [{
              itemId: '76000000-0000-0000-0000-000000000044',
              itemCode: 'VISUAL',
              displayOrder: 1,
              label: '외관 검사',
              guidance: '표면 상태 확인',
              responseType: 'Check',
              isRequired: true,
              maxTextLength: null
            }];
        return Promise.resolve(json({
          panel: oqc ? oqcPanel : qualityPanel,
          decisionMode: 'Checklist',
          reportId: '76000000-0000-0000-0000-000000000043',
          reportStatus: 'Finalized',
          reportVersion: 2,
          result: oqc ? 'Failed' : 'Passed',
          reason: oqc ? 'OQC 부적합 조치 필요' : '기준 충족',
          pdfStatus: oqc ? null : 'Ready',
          items: qualityItems,
          responses: [{
            templateItemId: qualityItems[0].itemId,
            checkResult: 'Pass',
            textValue: null,
            note: '이상 없음'
          }],
          photos: [{
            photoId: '76000000-0000-0000-0000-000000000045',
            templateItemId: '76000000-0000-0000-0000-000000000044',
            displayName: 'quality-proof.jpg',
            normalizedMime: 'image/jpeg',
            byteSize: 2048,
            altText: '외관 검사 증빙',
            createdAtUtc: '2026-07-06T01:00:00Z'
          }],
          history: [{
            attemptId: '76000000-0000-0000-0000-000000000042',
            attemptNumber: 1,
            status: 'Completed',
            pendingId: null,
            pendingNumber: null,
            completedAtUtc: '2026-07-06T01:10:00Z'
          }]
        }));
      }

      if (path === '/api/logistics/queue') {
        return Promise.resolve(json({
          stage: url.searchParams.get('stage'),
          todayCount: 0,
          blockedCount: 0,
          projects: []
        }));
      }
      if (path === `/api/logistics/projects/${projectId}/history`) {
        return Promise.resolve(json({ projectId, items: [{
          targetId: '76000000-0000-0000-0000-000000000051',
          stage: 'packing',
          displayCode: 'PU-001',
          status: 'Finalized',
          version: 3,
          note: '충격 방지 포장',
          specification: '1200×800×900',
          weightText: '850 kg',
          departureDate: null,
          panelCodes: ['P01'],
          unitCodes: [],
          evidence: [{
            evidenceId: '76000000-0000-0000-0000-000000000052',
            ownerType: 'PackingPhoto',
            displayName: 'packing-proof.jpg',
            normalizedMime: 'image/jpeg',
            byteSize: 4096,
            altText: '포장 완료 사진',
            createdAtUtc: '2026-07-07T01:10:00Z'
          }],
          createdByName: 'Dev Logistics User',
          createdAtUtc: '2026-07-07T01:00:00Z',
          finalizedByName: 'Dev Logistics User',
          finalizedAtUtc: '2026-07-07T01:20:00Z',
          cancelledByName: null,
          cancelledAtUtc: null
        }] }));
      }

      if (path === `/api/projects/${projectId}/panel-information`) {
        const response = panelInformation(projectId);
        const historicalSequences = [1, 10, 19, 52];
        response.panels = response.panels.map((panel, index) => ({
          ...panel,
          sequenceNumber: historicalSequences[index],
          panelNumber: `No.${historicalSequences[index]}`,
          displayCode: `P${String(historicalSequences[index]).padStart(2, '0')}`,
          displayName: `No.${historicalSequences[index]} · 패널명 미입력`
        }));
        return Promise.resolve(json(response));
      }

      return mockFetch(input, init);
    }));

    render(<App />);
    fireEvent.click(await screen.findByText('TASK-003A Demo'));

    const projectTabs = await screen.findByRole('tablist', { name: '프로젝트 상세 섹션' });
    expect(within(projectTabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      '전체 흐름', '생산관리', '설계', '구매', '제조', '품질', '물류', '영업'
    ]);
    expect(within(projectTabs).queryByRole('tab', { name: '자재' })).not.toBeInTheDocument();

    fireEvent.click(await screen.findByRole('tab', { name: '영업' }));
    expect(await screen.findByRole('heading', { name: '영업 정산' })).toBeInTheDocument();
    expect(screen.getByText('납품 후 발행 요청과 회계 확인, 프로젝트 완료 상태를 확인합니다.')).toBeInTheDocument();
    expect(screen.queryByText('프로젝트 영업 입력')).not.toBeInTheDocument();

    fireEvent.click(within(projectTabs).getByRole('tab', { name: '구매' }));
    expect(await screen.findByRole('table', { name: '구매정보' })).toHaveTextContent('입고 확정');

    fireEvent.click(within(projectTabs).getByRole('tab', { name: '제조' }));
    const manufacturingTable = await screen.findByRole('table', { name: '제조 패널 현황' });
    expect(within(manufacturingTable).getAllByRole('row')[0]).toHaveTextContent('No패널명핵심정보제조 단계진행률');
    expect(within(manufacturingTable).getAllByRole('row').slice(1).map((row) => row.querySelector('span')?.textContent)).toEqual(['1', '2', '3', '4']);
    expect(within(manufacturingTable).getAllByRole('row')[4]).toHaveTextContent('P52');
    expect(manufacturingTable).toHaveTextContent('P01');
    expect(manufacturingTable).toHaveTextContent('완료 · 단계 1/1');
    expect(manufacturingTable).toHaveTextContent('제조 완료');
    expect(manufacturingTable).toHaveTextContent('미시작');
    expect(within(manufacturingTable).getByLabelText('P01 제조 진행률 100% (1/1)')).toBeInTheDocument();
    const manufacturingSection = manufacturingTable.closest('section');
    expect(manufacturingSection).toHaveTextContent('착수 대기3/4');
    expect(manufacturingSection).toHaveTextContent('완료1/4');
    expect(manufacturingSection).toHaveTextContent('진행률8%');

    fireEvent.click(within(projectTabs).getByRole('tab', { name: '품질' }));
    const qualityTable = await screen.findByRole('table', { name: '품질 패널 현황' });
    expect(within(qualityTable).getAllByRole('row')[0]).toHaveTextContent('No패널명핵심정보품질 단계진행률');
    expect(within(qualityTable).getAllByRole('row').slice(1).map((row) => row.querySelector('span')?.textContent)).toEqual(['1', '2', '3', '4']);
    expect(within(qualityTable).getAllByRole('row')[4]).toHaveTextContent('P52');
    expect(qualityTable).toHaveTextContent('OQC 부적합 · Pending 조치 대기');
    expect(qualityTable).not.toHaveTextContent('전진검수 대기');
    expect(qualityTable).not.toHaveTextContent('FAT 대기');
    expect(qualityTable).toHaveTextContent('OQC');
    expect(qualityTable).toHaveTextContent('Pending');
    expect(within(qualityTable).getByLabelText('P01 품질 진행률 20% (1/5)')).toBeInTheDocument();
    const qualitySection = qualityTable.closest('section');
    expect(qualitySection).toHaveTextContent('LQC 완료1/4');
    expect(qualitySection).toHaveTextContent('FAT 완료없음');
    expect(qualitySection).toHaveTextContent('진행률5%');

    fireEvent.click(within(projectTabs).getByRole('tab', { name: '물류' }));
    const logisticsTable = await screen.findByRole('table', { name: '물류 패널 현황' });
    expect(within(logisticsTable).getAllByRole('row')[0]).toHaveTextContent('No패널명핵심정보물류 단계진행률');
    expect(within(logisticsTable).getAllByRole('row').slice(1).map((row) => row.querySelector('span')?.textContent)).toEqual(['1', '2', '3', '4']);
    expect(within(logisticsTable).getAllByRole('row')[4]).toHaveTextContent('P52');
    expect(logisticsTable).toHaveTextContent('포장 완료 · 출발 대기 · 납품 대기');
    expect(logisticsTable).toHaveTextContent('출발');
    expect(within(logisticsTable).getByLabelText('P01 물류 진행률 33% (1/3)')).toBeInTheDocument();
    const logisticsSection = logisticsTable.closest('section');
    expect(logisticsSection).toHaveTextContent('포장 완료1/4');
    expect(logisticsSection).toHaveTextContent('출발 완료0/4');
    expect(logisticsSection).toHaveTextContent('납품 완료0/4');
    expect(logisticsSection).toHaveTextContent('진행률8%');
    fireEvent.click(within(logisticsTable).getByRole('row', { name: /P01/u }));
    expect(window.location.pathname).toBe(`/projects/${projectId}/panels/${panelIds[0]}`);
    expect(window.location.search).toBe('?tab=logistics');
  });

  it('hides sales and materials project tabs from non-sales users and normalizes legacy links', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-design' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));

    let projectTabs = await screen.findByRole('tablist', { name: '프로젝트 상세 섹션' });
    expect(within(projectTabs).queryByRole('tab', { name: '영업' })).not.toBeInTheDocument();
    expect(within(projectTabs).queryByRole('tab', { name: '자재' })).not.toBeInTheDocument();

    window.history.pushState(null, '', `/projects/${projectId}?section=sales`);
    fireEvent.popState(window);
    projectTabs = await screen.findByRole('tablist', { name: '프로젝트 상세 섹션' });
    await waitFor(() => expect(within(projectTabs).getByRole('tab', { name: '전체 흐름' })).toHaveAttribute('aria-selected', 'true'));

    window.history.pushState(null, '', `/projects/${projectId}?section=materials`);
    fireEvent.popState(window);
    projectTabs = await screen.findByRole('tablist', { name: '프로젝트 상세 섹션' });
    await waitFor(() => expect(within(projectTabs).getByRole('tab', { name: '구매' })).toHaveAttribute('aria-selected', 'true'));
  });

  it('gives Materials the staged receiving workspace instead of a completion checkbox', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '자재' }));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '입고 관리' }));

    expect(await screen.findByRole('heading', { name: '자재 입고 관리' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /TASK-003A Demo/ }));
    expect(screen.getByText('도착부터 IQC, 입고 확정까지 한 흐름으로 관리합니다.')).toBeInTheDocument();
    expect(screen.getByText('Relay')).toBeInTheDocument();
    expect(screen.queryByText('통상납기')).not.toBeInTheDocument();
    expect(screen.getAllByText('입고 완료').length).toBeGreaterThan(0);
    fireEvent.click(screen.getAllByRole('button', { name: '도착입력' })[0]);
    expect(screen.queryByLabelText('발주 수량')).not.toBeInTheDocument();
    expect(screen.getByLabelText('도착 수량')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '취소' }));
    const relayRow = screen.getByText('Relay').closest('.material-purchase-row') as HTMLElement;
    expect(within(relayRow).queryByText('도착·IQC 이력')).not.toBeInTheDocument();
    expect(relayRow).toHaveTextContent('잔여 100 EA');
    fireEvent.click(within(relayRow).getByText('Relay'));
    expect(within(relayRow).getByText('도착·IQC 이력')).toBeInTheDocument();
    fireEvent.click(within(relayRow).getByRole('button', { name: /24 EA/ }));
    expect(await screen.findByText('도착 등록', { selector: '.material-status-badge' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'IQC 요청' })).not.toBeInTheDocument();
  });

  it('separates multi-work menus from their project dashboards', async () => {
    const pendingQueries: URLSearchParams[] = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      const path = url.pathname;
      if (path === '/api/pending') {
        pendingQueries.push(url.searchParams);
        return json({
          summary: { openCount: 1, urgentCount: 0, overdueCount: 0, reinspectionCount: 0, closedCount: 0 },
          items: [{
            pendingId: '88000000-0000-0000-0000-000000000001',
            issueNumber: 1,
            projectId,
            projectCode: 'PJT-003A',
            projectTitle: 'TASK-003A Demo',
            targetType: 'Project',
            targetId: projectId,
            targetLabel: null,
            issueType: 'Other',
            issueTypeLabel: '기타',
            title: 'Synthetic Pending',
            description: '대시보드 진입을 검증하는 테스트 Pending입니다.',
            status: 'Registered',
            statusLabel: '등록',
            priority: 'Normal',
            priorityLabel: '일반',
            actionDepartmentCode: 'sales',
            assigneeUserId: null,
            assigneeDisplayName: null,
            dueDate: null,
            isOverdue: false,
            version: 1,
            createdByUserId: '50000000-0000-0000-0000-000000000002',
            createdByDisplayName: 'dev-sales',
            createdAtUtc: '2026-08-12T00:00:00Z',
            updatedAtUtc: '2026-08-12T00:00:00Z'
          }]
        });
      }
      if (path === '/api/manufacturing/queue') {
        return json({
          projects: [{
            projectId,
            projectCode: 'PJT-003A',
            projectTitle: 'TASK-003A Demo',
            readyCount: 1,
            inProgressCount: 0,
            blockedCount: 0,
            completedCount: 0,
            panels: [{
              panelId: panelIds[0],
              displayCode: 'P01',
              panelName: 'MAIN',
              workflowStage: 'ManufacturingReady',
              kittingCompleted: false,
              workItemId: 'work-manufacturing',
              workItemStatus: 'Requested',
              executionId: null,
              status: 'Ready',
              version: 0,
              checkedStepCount: 0,
              totalStepCount: 4,
              activePendingId: null,
              activePendingNumber: null,
              actionDepartmentCode: null,
              startedAtUtc: null,
              completedAtUtc: null,
              canMutate: true,
              batchSteps: []
            }]
          }]
        });
      }
      return mockFetch(input, init);
    }));
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    const departmentLandingExpectations = [
      { menu: '생산관리', firstTask: '생산계획' },
      { menu: '자재', firstTask: '입고 관리' },
      { menu: '품질', firstTask: '수입검사(IQC)' },
      { menu: '물류', firstTask: '포장' }
    ];

    for (const expectation of departmentLandingExpectations) {
      fireEvent.click(within(commonNavigation).getByRole('button', { name: expectation.menu }));
      expect(within(commonNavigation).getByRole('button', { name: expectation.menu })).toHaveAttribute('aria-expanded', 'true');
      fireEvent.click(within(commonNavigation).getByRole('button', { name: expectation.firstTask }));
      await waitFor(() => expect(within(commonNavigation).getByRole('button', { name: expectation.firstTask })).toHaveAttribute('aria-current', 'page'));
      expect(document.querySelector('[data-testid^="department-work-hub-"]')).toBeNull();
    }

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '제조' }));
    expect(await screen.findByTestId('manufacturing-dashboard')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: '제조 프로젝트 프로젝트 목록' })).toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: 'Pending' }));
    const pendingDashboard = await screen.findByTestId('pending-dashboard');
    await waitFor(() => expect(pendingQueries.length).toBeGreaterThan(0));
    expect(pendingQueries.at(-1)?.get('scope')).toBe('Department');
    expect(pendingQueries.at(-1)?.get('statusGroup')).toBe('Open');
    expect(within(pendingDashboard).getByRole('heading', { name: 'Pending 프로젝트' })).toBeInTheDocument();
    expect(within(pendingDashboard).getByRole('table', { name: 'Pending 프로젝트 프로젝트 목록' })).toBeInTheDocument();
    fireEvent.click(within(pendingDashboard).getByRole('button', { name: /TASK-003A Demo/ }));
    expect(await screen.findByRole('heading', { name: 'TASK-003A Demo' })).toBeInTheDocument();
    expect(screen.getByLabelText('조회 범위')).toHaveValue('All');
    expect(screen.getByRole('button', { name: 'Pending 프로젝트' })).toBeInTheDocument();
  });

  it('opens the Materials workspace for Sales as read-only and keeps input unavailable', async () => {
    render(<App />);

    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '자재' }));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '입고 관리' }));

    expect(await screen.findByRole('heading', { name: '자재 입고 관리' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /TASK-003A Demo/ }));
    expect(screen.getByRole('note')).toHaveTextContent('조회 전용입니다.');
    expect(screen.getAllByRole('button', { name: '도착입력' }).every((button) => button.hasAttribute('disabled'))).toBe(true);
    expect(screen.queryByRole('button', { name: '품목 입고 마감' })).not.toBeInTheDocument();
  });

  it('shows the full selected-project receipt history and can hide completed material cards', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '자재' }));
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '입고 관리' }));

    expect(await screen.findByRole('heading', { name: '자재 입고 관리' })).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('완료 포함'));
    const materialProject = screen.getByRole('button', { name: /TASK-003A Demo/ });
    await waitFor(() => expect(materialProject).toHaveTextContent('구매품2건'));
    fireEvent.click(materialProject);
    expect(screen.getByText('Relay')).toBeInTheDocument();
    expect(screen.getByText('Completed Relay')).toBeInTheDocument();
    expect(screen.getByLabelText('완료 포함')).toBeChecked();
    fireEvent.click(screen.getByText('Completed Relay'));
    expect(screen.getAllByText('입고 확정').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByLabelText('완료 포함'));
    await waitFor(() => expect(screen.queryByText('Completed Relay')).not.toBeInTheDocument());
    expect(screen.queryByRole('checkbox', { name: '입고 완료' })).not.toBeInTheDocument();
  });
});

function fillCreateForm(projectCode: string, projectTitle: string) {
  fireEvent.change(screen.getByLabelText('고객사*'), { target: { value: 'EMI Test Customer' } });
  fireEvent.change(screen.getByLabelText('Item*'), { target: { value: 'UL67' } });
  fireEvent.change(screen.getByLabelText('PJT Code*'), { target: { value: projectCode } });
  fireEvent.change(screen.getByLabelText('PJT Title*'), { target: { value: projectTitle } });
  fireEvent.change(screen.getByLabelText('면수*'), { target: { value: '4' } });
  fireEvent.change(screen.getByLabelText('납기일*'), { target: { value: '2026-10-10' } });
  fireEvent.change(screen.getByLabelText('영업담당자*'), { target: { value: salesOwnerId } });
  fireEvent.change(screen.getByLabelText('포장방식*'), { target: { value: 'WoodenCrate' } });
  fireEvent.change(screen.getByLabelText('판매금액'), { target: { value: '1250000.5' } });
}

async function mockFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const userKey = readDevUser(init);
  const path = url.pathname;

  if (path === '/health/ready') {
    return json({
      name: 'ready',
      status: 'ok',
      database: { isReady: true, reason: 'reachable' },
      checkedAtUtc: '2026-06-25T00:00:00Z'
    });
  }

  if (path === '/api/runtime-mode') {
    return json({
      mode: 'Development',
      reviewSafe: false,
      mutationAllowed: true,
      backgroundWorkersEnabled: true,
      externalProvidersEnabled: true,
      databaseReadOnly: false,
      migrationExecutionEnabled: true,
      environment: 'Development',
      ready: true,
      reason: 'not_applicable',
      expectedMigration: '0027_notification_access_scope_and_manual_work_items',
      actualMigration: null,
      migrationLedgerStatus: null,
      expectedMigrationCount: 27,
      actualMigrationCount: null,
      missingMigrations: [],
      unexpectedMigrations: [],
      approvedLegacyMigrations: [],
      migrationSchemaCompatible: false,
      migrationLedgerReady: false
    });
  }

  if (path === '/api/me') {
    return json(currentUser(userKey));
  }

  if (path === '/api/me/profile-photo') {
    if (init?.method === 'PUT') {
      return json({ profilePhotoVersion: 'test-photo-1', normalizedMime: 'image/png', byteSize: 128 });
    }
    if (init?.method === 'DELETE') {
      return new Response(null, { status: 204 });
    }
    return json({ title: 'not found' }, 404);
  }

  if (path === '/api/form-templates/my-scope') {
    if (userKey === 'dev-admin') {
      return json({ canManage: true, isSystemAdministrator: true, domains: ['Quality', 'Manufacturing', 'ProductionPlanning'] });
    }
    if (userKey === 'dev-production') {
      return json({ canManage: true, isSystemAdministrator: false, domains: ['Manufacturing', 'ProductionPlanning'] });
    }
    if (userKey === 'dev-quality') {
      return json({ canManage: true, isSystemAdministrator: false, domains: ['Quality'] });
    }
    return json({ canManage: false, isSystemAdministrator: false, domains: [] });
  }

  if (path === '/api/form-templates/material-categories') {
    return json({
      items: [
        {
          categoryId: '67000000-0000-0000-0000-000000000001',
          code: 'ENCLOSURE',
          displayName: '외함',
          requiresIqc: true,
          iqcDecisionMode: 'ScanBased',
          isActive: true,
          displayOrder: 10,
          rowVersion: 1
        },
        {
          categoryId: '67000000-0000-0000-0000-000000000005',
          code: 'OTHER',
          displayName: '기타',
          requiresIqc: false,
          iqcDecisionMode: 'ScanBased',
          isActive: true,
          displayOrder: 50,
          rowVersion: 1
        }
      ]
    });
  }

  if (path === '/api/sales/kpi') {
    return json({
      year: 2026,
      currency: 'KRW',
      defaultCurrency: 'KRW',
      availableYears: [2026],
      availableCurrencies: ['KRW'],
      months: Array.from({ length: 12 }, (_, index) => ({
        month: index + 1,
        revenueAmount: (index + 1) * 10000000,
        targetAmount: 100000000,
        settlementCount: 1
      })),
      kpi: {
        currentMonthRevenue: 70000000,
        revenueTotal: 780000000,
        targetTotal: 1200000000,
        registeredTargetMonthCount: 12,
        achievementRate: 65,
        remainingTargetAmount: 420000000,
        exceededTargetAmount: 0
      },
      pipeline: { amount: 300000000, projectCount: 3 },
      missingAmountCount: 0
    });
  }

  if (path === '/api/home/department-metrics') {
    const user = currentUser(userKey);
    if (user.department === 'materials') {
      return json({
        departmentCode: user.department,
        departmentName: user.departmentName,
        metrics: [
          { id: 'materials-customer-supply-overdue', label: '사급 제공 지연', count: 1, tone: 'danger', destinationKey: 'materials-customer-supply-overdue', actionLabel: '지연 잔량 확인' },
          { id: 'materials-iqc', label: 'IQC 판정 대기', count: 0, tone: 'danger', destinationKey: 'materials-receipts', actionLabel: 'IQC 확인' },
          { id: 'materials-kitting', label: '키팅 대기 패널', count: 0, tone: 'neutral', destinationKey: 'materials-kitting', actionLabel: '키팅 열기' }
        ]
      });
    }
    return json({
      departmentCode: user.department,
      departmentName: user.departmentName,
      metrics: [
        { id: 'focus-one', label: '핵심 대기', count: 3, tone: 'warning', destinationKey: 'my-work', actionLabel: '업무 열기' },
        { id: 'focus-two', label: '진행 중', count: 2, tone: 'neutral', destinationKey: 'projects', actionLabel: '프로젝트 열기' },
        { id: 'focus-three', label: '차단', count: 1, tone: 'danger', destinationKey: 'pending', actionLabel: '조치 확인' }
      ]
    });
  }

  if (path === '/api/pending') {
    return json({
      summary: {
        openCount: 0,
        urgentCount: 0,
        overdueCount: 0,
        reinspectionCount: 0,
        closedCount: 0,
        registeredCount: 0,
        actionRequestedCount: 0,
        inProgressCount: 0
      },
      items: []
    });
  }

  if (path.startsWith('/api/admin/users')) {
    const updated = init?.method === 'PATCH';
    const approvalPendingFilter = new URL(String(input), 'http://localhost').searchParams.get('filter') === 'approval-pending';
    if (path.endsWith('/schedule-deletion')) {
      adminUserDeletionScheduled = true;
    }
    const scheduledDeletion = adminUserDeletionScheduled;
    const adminUsers = [
        {
          userId: '50000000-0000-0000-0000-000000000002',
          developmentUserKey: '',
          displayName: 'Entra Sales User',
          email: 'sales@example.invalid',
          authProvider: 'EntraId',
          isActive: scheduledDeletion || updated ? false : true,
          approvalPending: false,
          departmentId: '10000000-0000-0000-0000-000000000002',
          departmentCode: 'sales',
          departmentName: '영업',
          roles: ['sales'],
          isReadOnly: false,
          isDepartmentHead: false,
          deletionRequestedAtUtc: scheduledDeletion ? '2026-07-07T00:00:00Z' : null,
          scheduledHardDeleteAtUtc: scheduledDeletion ? '2026-07-14T00:00:00Z' : null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: scheduledDeletion ? 'DeletionScheduled' : 'Active',
          lifecycleStatusLabel: scheduledDeletion ? '삭제 예정' : '활성',
          scheduledHardDeleteLabel: scheduledDeletion ? '2026-07-14 09:00' : null
        },
        {
          userId: '50000000-0000-0000-0000-000000000001',
          developmentUserKey: 'dev-admin',
          displayName: 'Dev System Administrator',
          email: null,
          authProvider: 'Dev',
          isActive: !adminHolidayDeletionScheduled,
          approvalPending: false,
          departmentId: '10000000-0000-0000-0000-000000000001',
          departmentCode: 'administration',
          departmentName: '관리',
          roles: ['system-administrator'],
          isReadOnly: true,
          isDepartmentHead: false,
          deletionRequestedAtUtc: null,
          scheduledHardDeleteAtUtc: null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        },
        {
          userId: '50000000-0000-0000-0000-000000000003',
          developmentUserKey: '',
          displayName: 'Entra Notification User',
          email: 'notify@example.invalid',
          authProvider: 'EntraId',
          isActive: true,
          approvalPending: false,
          departmentId: '10000000-0000-0000-0000-000000000002',
          departmentCode: 'sales',
          departmentName: '영업',
          roles: ['sales'],
          isReadOnly: false,
          isDepartmentHead: false,
          deletionRequestedAtUtc: null,
          scheduledHardDeleteAtUtc: null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        },
        {
          userId: '50000000-0000-0000-0000-000000000004',
          developmentUserKey: '',
          displayName: 'Entra Pending User',
          email: 'pending@example.invalid',
          authProvider: 'EntraId',
          isActive: true,
          approvalPending: true,
          departmentId: null,
          departmentCode: null,
          departmentName: null,
          roles: [],
          isReadOnly: false,
          isDepartmentHead: false,
          deletionRequestedAtUtc: null,
          scheduledHardDeleteAtUtc: null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        }
      ];
    return json({
      users: approvalPendingFilter ? adminUsers.filter((user) => user.approvalPending) : adminUsers,
      departments: [
        { departmentId: '10000000-0000-0000-0000-000000000001', code: 'administration', name: '관리', defaultRoleCode: 'system-administrator' },
        { departmentId: '10000000-0000-0000-0000-000000000002', code: 'sales', name: '영업', defaultRoleCode: 'sales' }
      ],
      roles: [
        { roleId: '20000000-0000-0000-0000-000000000001', code: 'system-administrator', name: 'System Administrator' },
        { roleId: '20000000-0000-0000-0000-000000000002', code: 'sales', name: 'Sales' }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/dashboard') {
    return json({
      pendingUserCount: 1,
      failedDeliveryCount: 2,
	      pendingDeliveryCount: 3,
      processingDeliveryCount: 1,
	      activeEscalationCount: 4,
	      activeEscalationLevels: [
	        { level: 'L0', label: '예정일 임박', count: 1 },
	        { level: 'L1', label: '예정일 초과', count: 2 },
	        { level: 'L2', label: '초과 +2영업일', count: 1 },
	        { level: 'L3', label: '초과 +3영업일', count: 0 }
	      ]
	    }, userKey === 'dev-admin' ? 200 : 403);
	  }

  if (path === '/api/admin/departments' || path.startsWith('/api/admin/departments/')) {
    if (path === '/api/admin/departments' && init?.method === 'POST') {
      const body = JSON.parse(init.body?.toString() ?? '{}') as { code?: string; name?: string; sortOrder?: number };
      if (body.code?.includes(' ') || !body.name || (body.sortOrder ?? 0) > 9999) {
        return json({
          message: '입력값을 확인해주세요.',
          fieldErrors: {
            code: ['부서 코드는 영문 대문자, 숫자, 하이픈(-), 언더스코어(_)만 사용할 수 있습니다.'],
            name: ['부서명은 필수입니다.'],
            sortOrder: ['정렬 순서는 0 이상 9999 이하로 입력해주세요.']
          }
        }, 400);
      }
    }

    if (init?.method === 'PATCH') {
      adminDepartmentDeletionScheduled = true;
    }
    const scheduledDeletion = adminDepartmentDeletionScheduled;
    const department = {
      departmentId: '10000000-0000-0000-0000-000000000002',
      code: 'sales',
      name: '영업',
      isActive: scheduledDeletion ? false : true,
      sortOrder: 20,
      userCount: 1,
      updatedAtUtc: '2026-07-07T00:00:00Z',
      deletionRequestedAtUtc: scheduledDeletion ? '2026-07-07T00:00:00Z' : null,
      scheduledHardDeleteAtUtc: scheduledDeletion ? '2026-07-14T00:00:00Z' : null,
      purgeBlockedAtUtc: null,
      purgeBlockedReason: null,
      lifecycleStatus: scheduledDeletion ? 'DeletionScheduled' : 'Active',
      lifecycleStatusLabel: scheduledDeletion ? '삭제 예정' : '활성',
      scheduledHardDeleteLabel: scheduledDeletion ? '2026-07-14 09:00' : null
    };
    return json(init?.method === 'POST' || init?.method === 'PUT' || init?.method === 'PATCH' ? department : { departments: [department] }, userKey === 'dev-admin' ? (init?.method === 'POST' ? 201 : 200) : 403);
  }

  if (path === '/api/admin/permissions/matrix') {
    return json({
      roles: [
        { roleId: '20000000-0000-0000-0000-000000000001', code: 'system-administrator', name: 'System Administrator' },
        { roleId: '20000000-0000-0000-0000-000000000002', code: 'sales', name: 'Sales' }
      ],
      permissions: [
        { permissionId: '30000000-0000-0000-0000-000000000025', code: 'admin-history.read', name: 'Read administrator history' }
      ],
      assignments: [
        { roleId: '20000000-0000-0000-0000-000000000001', permissionId: '30000000-0000-0000-0000-000000000025' }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/master-data/change-logs') {
    return json({
      items: [
        {
          changeLogId: '79000000-0000-0000-0000-000000000001',
          entityType: 'Department',
          entityId: '10000000-0000-0000-0000-000000000002',
          action: 'Delete',
          beforeJson: '{}',
          afterJson: '{}',
          reason: '테스트 변경',
          changedByUserId: '50000000-0000-0000-0000-000000000001',
          changedByDisplayName: 'Dev System Administrator',
          changedAtUtc: '2026-07-07T00:00:00Z'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/work-items/history') {
    return json({
      items: [
        {
          workItemId: '76000000-0000-0000-0000-000000000001',
          projectId,
          projectTitle: 'TASK-003A Demo',
          projectCode: 'PJT-003A',
          workflowStageCode: 'ProductionPlanning',
          workflowStageName: '생산계획·담당자',
          title: '생산계획, 담당자 입력',
          status: 'Requested',
          assignedUserId: salesOwnerId,
          assignedDisplayName: 'Dev Sales User',
          startedAtUtc: null,
          completedAtUtc: null,
          cancelledAtUtc: null,
          dueDate: null,
          createdAtUtc: '2026-07-07T00:00:00Z',
          updatedAtUtc: '2026-07-07T00:00:00Z'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path.startsWith('/api/admin/notification-deliveries/') && init?.method !== 'POST') {
    const webPushDetail = path.endsWith('79000000-0000-0000-0000-000000000102');
    return json({
      deliveryId: path.split('/').at(-1),
      categoryLabel: '관리자 수동 발송',
      notificationKindLabel: '프로젝트 생성 알림',
      projectName: 'TASK-003A Demo',
      title: webPushDetail ? '기기 푸시 알림' : '[테스트] 프로젝트 생성 알림',
      message: '실제 업무 알림이 아닙니다.',
      manualRequestedAtUtc: '2026-07-07T00:00:00Z',
      createdAtUtc: '2026-07-07T00:00:00Z',
      channel: webPushDetail ? 'WebPush' : 'Mail',
      channelLabel: webPushDetail ? 'PWA 푸시' : '메일',
      recipient: 'Dev Sales User',
      status: 'Sent',
      statusLabel: webPushDetail ? '푸시 서비스 접수' : '발송 완료',
      attemptCount: 1,
      nextAttemptAtUtc: null,
      lastAttemptAtUtc: '2026-07-07T00:00:00Z',
      sentAtUtc: '2026-07-07T00:00:00Z',
      errorCode: null,
      errorMessage: null,
      actionGuide: '상태를 확인하세요.',
      adminHandlingStatus: 'Open',
      adminHandlingStatusLabel: '미처리',
      adminHandlingNote: null,
      correlationId: 'N003-UNIT-FRONT',
      providerMessageId: 'provider-message',
      claimedAtUtc: null,
      claimExpiresAtUtc: null,
      claimIsStale: false,
      claimedByInstance: null,
      attempts: [
        {
          attemptNumber: 1,
          workerInstance: 'opaque',
          claimedAtUtc: '2026-07-07T00:00:00Z',
          leaseExpiresAtUtc: '2026-07-07T00:05:00Z',
          providerCallStartedAtUtc: '2026-07-07T00:00:01Z',
          completedAtUtc: '2026-07-07T00:00:02Z',
          outcome: 'Sent',
          errorCode: null,
          errorMessage: null,
          providerMessageId: 'provider-message'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

	  if (path === '/api/admin/notification-deliveries') {
	    const status = url.searchParams.get('status') || 'Sent';
	    const handlingStatus = url.searchParams.get('handlingStatus') ?? 'Open';
	    const webPushChannel = url.searchParams.get('channel') === 'WebPush';
	    return json({
	      items: [
	        {
	          deliveryId: webPushChannel ? '79000000-0000-0000-0000-000000000102' : '79000000-0000-0000-0000-000000000101',
	          notificationId: status === 'Pending' ? null : '79000000-0000-0000-0000-000000000301',
	          recipientUserId: salesOwnerId,
	          projectId,
	          workItemId: '76000000-0000-0000-0000-000000000001',
	          channel: webPushChannel ? 'WebPush' : 'Mail',
	          channelLabel: webPushChannel ? 'PWA 푸시' : '메일',
	          deliveryType: webPushChannel ? 'WebPushNotification' : status === 'Pending' ? 'OverdueL1' : 'DailyDigest',
	          deliveryTypeLabel: webPushChannel ? '인앱 연동 PWA 푸시' : status === 'Pending' ? '예정일 초과 L1' : '일일 요약',
	          status,
	          statusLabel: status === 'Pending' ? '발송 대기' : status === 'Processing' ? '발송 처리 중' : status === 'Failed' ? '발송 실패' : webPushChannel ? '푸시 서비스 접수' : '발송 완료',
	          attemptCount: status === 'Pending' ? 0 : 1,
	          nextAttemptAtUtc: status === 'Pending' ? '2026-07-07T01:00:00Z' : null,
	          lastAttemptAtUtc: status === 'Sent' ? '2026-07-07T00:00:00Z' : null,
	          sentAtUtc: status === 'Sent' ? '2026-07-07T00:00:00Z' : null,
	          suppressedAtUtc: null,
	          errorCode: status === 'Failed' ? 'RecipientEmailMissing' : null,
	          errorMessage: status === 'Failed' ? '수신자 이메일이 없습니다.' : null,
	          actionGuide: status === 'Failed' ? '수신자 이메일 또는 사용자 정보를 확인하세요.' : '발송 worker 처리 대기 중입니다.',
	          pendingReason: status === 'Pending' ? '발송 worker 처리 대기 중입니다.' : null,
	          recipientDisplayName: 'Dev Sales User',
	          recipientEmail: null,
	          recipientEmailMasked: null,
	          projectTitle: 'TASK-003A Demo',
	          projectCode: 'PJT-003A',
	          workItemTitle: '생산계획, 담당자 입력',
	          workflowStageName: '생산계획·담당자',
	          notificationTitle: status === 'Pending' ? '예정일 초과 알림' : 'Daily Digest',
	          notificationMessageSummary: status === 'Pending' ? '예정일 초과 알림 대기 중입니다.' : '일일 요약 발송 이력입니다.',
	          displayMessageSummary: status === 'Pending' ? '예정일 초과 알림 대기 중입니다.' : '일일 요약 발송 이력입니다.',
	          displayTitle: webPushChannel ? '기기 푸시 알림' : status === 'Pending' ? '예정일 초과 알림' : status === 'Failed' ? '발송 실패 테스트' : 'Daily Digest',
	          displayRecipient: 'Dev Sales User',
	          displayProject: 'TASK-003A Demo · PJT-003A',
	          displayRecipientKind: 'User',
	          displayChannelTarget: null,
	          manualNotificationKind: null,
	          manualNotificationKindLabel: null,
	          correlationId: null,
	          linkUrl: `/projects/${projectId}`,
	          adminHandlingStatus: handlingStatus,
	          adminHandlingStatusLabel: handlingStatus === 'Acknowledged' ? '확인됨' : handlingStatus === 'Dismissed' ? '제외됨' : '미처리',
	          adminHandledAtUtc: handlingStatus === 'Open' ? null : '2026-07-07T01:30:00Z',
	          adminHandledByUserId: handlingStatus === 'Open' ? null : '50000000-0000-0000-0000-000000000001',
	          adminHandledByDisplayName: handlingStatus === 'Open' ? null : 'Dev System Administrator',
	          adminHandlingNote: handlingStatus === 'Open' ? null : '확인했습니다.',
	          claimedAtUtc: status === 'Processing' ? '2026-07-07T00:00:00Z' : null,
	          claimExpiresAtUtc: status === 'Processing' ? '2026-07-07T00:05:00Z' : null,
	          claimIsStale: false,
	          createdAtUtc: '2026-07-07T00:00:00Z',
	          updatedAtUtc: '2026-07-07T00:00:00Z'
	        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/notification-deliveries/acknowledge' || path === '/api/admin/notification-deliveries/dismiss' || path === '/api/admin/notification-deliveries/retry') {
    return json({
      requestedCount: 1,
      succeededCount: 1,
      failedCount: 0,
      skippedCount: 0,
      items: [
        {
          deliveryId: '79000000-0000-0000-0000-000000000101',
          status: 'Succeeded',
          message: path.endsWith('/retry') ? '재발송 대기열에 등록했습니다.' : '처리했습니다.'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/notification-deliveries/send-manual') {
    return json({
      correlationId: 'N003-UNIT-FRONT',
      requestedCount: 3,
      queuedCount: 3,
      items: [
        {
          channel: 'TeamsChannel',
          channelLabel: 'Teams 채널',
          deliveryId: '79000000-0000-0000-0000-000000000401',
          status: 'Queued',
          errorCode: null,
          errorMessage: null,
          target: 'Teams 채널',
          message: '발송 요청이 접수되었습니다.'
        },
        {
          channel: 'TeamsActivity',
          channelLabel: 'Teams Activity',
          deliveryId: '79000000-0000-0000-0000-000000000402',
          status: 'Queued',
          errorCode: null,
          errorMessage: null,
          target: 'Entra Sales User',
          message: '발송 요청이 접수되었습니다.'
        },
        {
          channel: 'Mail',
          channelLabel: '메일',
          deliveryId: '79000000-0000-0000-0000-000000000403',
          status: 'Queued',
          errorCode: null,
          errorMessage: null,
          target: 's***@example.invalid',
          message: '발송 요청이 접수되었습니다.'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

	  if (path === '/api/admin/work-item-escalations') {
	    const level = url.searchParams.get('level') ?? 'L1';
	    return json({
	      items: [
	        {
          escalationId: '79000000-0000-0000-0000-000000000201',
          workItemId: '76000000-0000-0000-0000-000000000001',
          projectId,
          projectTitle: 'TASK-003A Demo',
          projectCode: 'PJT-003A',
          workflowStageCode: 'ProductionPlanning',
          workflowStageName: '생산계획·담당자',
          workItemTitle: '생산계획, 담당자 입력',
	          dueDate: '2026-07-07',
	          status: 'Active',
	          currentLevel: level,
	          lastEscalatedAtUtc: '2026-07-07T00:00:00Z',
	          nextCheckAtUtc: '2026-07-08T00:00:00Z',
          assignedDisplayName: 'Dev Sales User',
          deliveryStatusSummary: 'Mail:Sent',
          createdAtUtc: '2026-07-07T00:00:00Z',
          updatedAtUtc: '2026-07-07T00:00:00Z'
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/calendar/holidays/template') {
    return Promise.resolve(new Response(new Blob(['xlsx']), {
      status: userKey === 'dev-admin' ? 200 : 403,
      headers: {
        'Content-Disposition': 'attachment; filename="Calendar_Holidays_Template.xlsx"'
      }
    }));
  }

  if (path === '/api/admin/calendar/holidays/preview') {
    return json({
      fileSha256: 'calendar-holiday-test',
      totalRows: 2,
      saveableCount: 2,
      insertCount: 1,
      updateCount: 1,
      errorCount: 1,
      rows: [
        {
          excelRowNumber: 2,
          date: '2026-07-02',
          name: '회사 창립기념 휴일',
          holidayType: 'Company',
          note: '연간 등록',
          resultType: 'Update',
          existingHolidayId: '78000000-0000-0000-0000-000000000001',
          errorMessages: []
        },
        {
          excelRowNumber: 3,
          date: '2026-07-04',
          name: '오류 휴일',
          holidayType: null,
          note: null,
          resultType: 'Error',
          existingHolidayId: null,
          errorMessages: ['휴일유형은 National, Substitute, Temporary, Company 중 하나여야 합니다.']
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/calendar/holidays/apply') {
    return json({
      insertedCount: 1,
      updatedCount: 1,
      skippedCount: 0,
      holidayIds: ['78000000-0000-0000-0000-000000000001']
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/admin/calendar/holidays') {
    if (init?.method === 'POST') {
      const body = JSON.parse(String(init.body));
      return json({
        holidayId: '78000000-0000-0000-0000-000000000099',
        date: body.date,
        name: body.name,
        countryCode: 'KR',
        holidayType: body.holidayType,
        isActive: body.isActive,
        note: body.note,
        source: 'AdminManual',
        createdAtUtc: '2026-07-06T00:00:00Z',
        updatedAtUtc: '2026-07-06T00:00:00Z',
        deletionRequestedAtUtc: null,
        scheduledHardDeleteAtUtc: null,
        purgeBlockedAtUtc: null,
        purgeBlockedReason: null,
        lifecycleStatus: body.isActive ? 'Active' : 'Inactive',
        lifecycleStatusLabel: body.isActive ? '활성' : '비활성'
      }, userKey === 'dev-admin' ? 201 : 403);
    }

    return json({
      year: Number(url.searchParams.get('year') ?? '2026'),
      countryCode: 'KR',
      holidays: [
        {
          holidayId: '78000000-0000-0000-0000-000000000001',
          date: '2026-07-02',
          name: '회사 창립기념 휴일',
          countryCode: 'KR',
          holidayType: 'Company',
          isActive: true,
          note: '연간 등록',
          source: 'AdminManual',
          createdAtUtc: '2026-07-01T00:00:00Z',
          updatedAtUtc: '2026-07-01T00:00:00Z',
          deletionRequestedAtUtc: adminHolidayDeletionScheduled ? '2026-07-07T00:00:00Z' : null,
          scheduledHardDeleteAtUtc: adminHolidayDeletionScheduled ? '2026-07-14T00:00:00Z' : null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: adminHolidayDeletionScheduled ? 'DeletionScheduled' : 'Active',
          lifecycleStatusLabel: adminHolidayDeletionScheduled ? '삭제 예정' : '활성',
          scheduledHardDeleteLabel: adminHolidayDeletionScheduled ? '2026-07-14 09:00' : null
        },
        {
          holidayId: '78000000-0000-0000-0000-000000000002',
          date: '2026-07-03',
          name: '공식 대체공휴일',
          countryCode: 'KR',
          holidayType: 'Substitute',
          isActive: true,
          note: null,
          source: 'OfficialApi',
          createdAtUtc: '2026-07-01T00:00:00Z',
          updatedAtUtc: '2026-07-01T00:00:00Z',
          deletionRequestedAtUtc: null,
          scheduledHardDeleteAtUtc: null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        }
      ]
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path.startsWith('/api/admin/calendar/holidays/')) {
    const pathParts = path.split('/');
    if (init?.method === 'DELETE') {
      adminHolidayDeletionScheduled = true;
    }
    const scheduledDeletion = adminHolidayDeletionScheduled;
    return json({
      holidayId: pathParts[pathParts.length - 1],
      date: '2026-07-02',
      name: '회사 창립기념 휴일',
      countryCode: 'KR',
      holidayType: 'Company',
      isActive: scheduledDeletion ? false : true,
      note: '연간 등록',
      source: 'AdminManual',
      createdAtUtc: '2026-07-01T00:00:00Z',
      updatedAtUtc: '2026-07-06T00:00:00Z',
      deletionRequestedAtUtc: scheduledDeletion ? '2026-07-07T00:00:00Z' : null,
      scheduledHardDeleteAtUtc: scheduledDeletion ? '2026-07-14T00:00:00Z' : null,
      purgeBlockedAtUtc: null,
      purgeBlockedReason: null,
      lifecycleStatus: scheduledDeletion ? 'DeletionScheduled' : 'Active',
      lifecycleStatusLabel: scheduledDeletion ? '삭제 예정' : '활성',
      scheduledHardDeleteLabel: scheduledDeletion ? '2026-07-14 09:00' : null
    }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/sales-owners') {
    return json([{ userId: salesOwnerId, displayName: 'Dev Sales User' }]);
  }

  if (path === '/api/my-work/summary') {
    return json({
      requestedCount: 1,
      inProgressCount: 0,
      completedCount: 0,
      blockingCount: 0,
      assignedProjectCount: 1,
      assignedProjectBreakdown: [
        { responsibilityType: 'ProductionPlanningPrimary', responsibilityLabel: '생산관리 정담당자', projectCount: 1 }
      ]
    });
  }

  if (path === '/api/my-work') {
    return json({
      items: [
        {
          workItemId: '76000000-0000-0000-0000-000000000001',
          projectId,
          projectTitle: 'TASK-003A Demo',
          projectCode: 'PJT-003A',
          projectItem: 'UL67',
          projectDeliveryDate: '2026-07-01',
          workflowStageCode: 'ProductionPlanning',
          workflowStageName: '생산계획·담당자',
          responsibilityType: 'ProductionPlanningPrimary',
          responsibilityLabel: '생산관리 정담당자',
          title: '생산계획, 담당자 입력',
          description: '생산계획 단계 처리가 필요합니다.',
          status: 'Requested',
          statusLabel: '시작 전',
          priority: 'Normal',
          priorityLabel: '일반',
          dueDate: null,
          createdAtUtc: '2026-06-25T00:00:00Z',
          startedAtUtc: null,
          completedAtUtc: null,
          linkUrl: `/projects/${projectId}/production-planning/edit`
        }
      ]
    });
  }

  if (path === '/api/my-work/76000000-0000-0000-0000-000000000001/start' && init?.method === 'POST') {
    return json({
      workItemId: '76000000-0000-0000-0000-000000000001',
      projectId,
      projectTitle: 'TASK-003A Demo',
      projectCode: 'PJT-003A',
      projectItem: 'UL67',
      projectDeliveryDate: '2026-07-01',
      workflowStageCode: 'ProductionPlanning',
      workflowStageName: '생산계획·담당자',
      responsibilityType: 'ProductionPlanningPrimary',
      responsibilityLabel: '생산관리 정담당자',
      title: '생산계획, 담당자 입력',
      description: '생산계획 단계 처리가 필요합니다.',
      status: 'InProgress',
      statusLabel: '진행 중',
      priority: 'Normal',
      priorityLabel: '일반',
      dueDate: null,
      createdAtUtc: '2026-06-25T00:00:00Z',
      startedAtUtc: '2026-06-25T00:10:00Z',
      completedAtUtc: null,
      linkUrl: `/projects/${projectId}/production-planning/edit`
    });
  }

  if (path === '/api/my-work/assigned-projects') {
    return json({
      items: [
        {
          projectId,
          projectTitle: 'TASK-003A Demo',
          projectCode: 'PJT-003A',
          item: 'UL67',
          deliveryDate: '2026-07-01',
          projectStatus: 'Active',
          projectStatusLabel: '진행',
          responsibilities: [
            { responsibilityType: 'ProductionPlanningPrimary', responsibilityLabel: '생산관리 정담당자' }
          ]
        }
      ]
    });
  }

  if (path === '/api/notifications/summary') {
    return json({ unreadCount: 1, blockingCount: 0 });
  }

  if (path === '/api/notices' && init?.method === 'POST') {
    const body = JSON.parse(String(init.body)) as { title: string; body: string };
    return json({
      noticeId,
      title: body.title,
      body: body.body,
      authorDisplayName: 'Dev Sales User',
      authorDepartmentName: '영업',
      createdAtUtc: '2026-07-21T01:00:00Z',
      canDelete: true
    });
  }

  if (path === `/api/notices/${noticeId}` && init?.method === 'DELETE') {
    return json({ noticeId, deleted: true });
  }

  if (path === `/api/notices/${noticeId}`) {
    return json({
      noticeId,
      title: '7월 생산 일정 안내',
      body: '7월 생산 계획 변경사항을 확인해 주세요.',
      authorDisplayName: 'Dev Sales User',
      authorDepartmentName: '영업',
      createdAtUtc: '2026-07-21T01:00:00Z',
      canDelete: true
    });
  }

  if (path === '/api/notices') {
    return json({
      items: [{
        noticeId,
        title: '7월 생산 일정 안내',
        preview: '7월 생산 계획 변경사항을 확인해 주세요.',
        authorDisplayName: 'Dev Sales User',
        authorDepartmentName: '영업',
        createdAtUtc: '2026-07-21T01:00:00Z',
        canDelete: true
      }],
      totalCount: 1,
      page: Number(url.searchParams.get('page') ?? 1),
      pageSize: Number(url.searchParams.get('pageSize') ?? 20)
    });
  }

  if (path.startsWith('/api/notifications/')) {
    const notificationId = path.split('/').at(-1) ?? '77000000-0000-0000-0000-000000000001';
    return json({
      notificationId,
      projectId,
      projectTitle: 'TASK-003A Demo',
      projectCode: 'PJT-003A',
      projectItem: 'UL67',
      notificationType: 'Reference',
      notificationTypeLabel: '참조',
      severity: 'Info',
      severityLabel: '정보',
      title: '프로젝트가 생성되었습니다.',
      message: 'TASK-003A Demo 프로젝트가 생성되었습니다.',
      linkUrl: `/projects/${projectId}`,
      createdAtUtc: '2026-06-25T00:00:00Z',
      readAtUtc: null
    });
  }

  if (path === '/api/notifications') {
    return json({
      items: [
        {
          notificationId: '77000000-0000-0000-0000-000000000001',
          projectId,
          projectTitle: 'TASK-003A Demo',
          projectCode: 'PJT-003A',
          projectItem: 'UL67',
          notificationType: 'Reference',
          notificationTypeLabel: '참조',
          severity: 'Info',
          severityLabel: '정보',
          title: '프로젝트가 생성되었습니다.',
          message: 'TASK-003A Demo 프로젝트가 생성되었습니다.',
          linkUrl: `/projects/${projectId}`,
          createdAtUtc: '2026-06-25T00:00:00Z',
          readAtUtc: null
        }
      ]
    });
  }

  if (path === '/api/projects' && init?.method === 'POST') {
    const body = JSON.parse(String(init.body)) as { projectTitle: string };
    if (body.projectTitle.toLowerCase().includes('duplicate')) {
      return json({ title: '동일한 PJT Title이 이미 존재합니다.' }, 409);
    }

    await new Promise((resolve) => setTimeout(resolve, 50));
    return json(projectDetail(true, 'Active', body.projectTitle), 201);
  }

  if (path === '/api/data-exports/selected' && init?.method === 'POST') {
    const body = JSON.parse(String(init.body)) as { ids: string[] };
    return new Response(new Blob(['selected-xlsx']), {
      status: 200,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': 'attachment; filename="EMI_selected.xlsx"',
        'X-Export-Row-Count': String(body.ids.length)
      }
    });
  }

  if (path === '/api/projects') {
    const status = url.searchParams.get('status');
    const items = [
      projectListItem(userKey, 'Active', 'TASK-003A Demo', projectId),
      projectListItem(userKey, 'OnHold', 'OnHold Project', onHoldProjectId),
      projectListItem(userKey, 'Completed', 'Completed Project', '71000000-0000-0000-0000-000000000013'),
      projectListItem(userKey, 'Cancelled', 'Cancelled Project', cancelledProjectId)
    ].filter((item) => !status || item.status === status);

    return json({
      items,
      page: 1,
      pageSize: 20,
      totalCount: items.length
    });
  }

  if (path === '/api/projects/summary') {
    return json(projectSummaryResponse());
  }

  if (path === '/api/projects/import/template') {
    return Promise.resolve(new Response(new Blob(['xlsx']), {
      status: userKey === 'dev-sales' ? 200 : 403,
      headers: {
        'Content-Disposition': 'attachment; filename="Project_Create_Template.xlsx"'
      }
    }));
  }

  if (path === '/api/projects/import/preview') {
    return json(projectExcelPreviewResponse(), userKey === 'dev-sales' ? 200 : 403);
  }

  if (path === '/api/projects/import/apply') {
    return json({ createdCount: 1, projectIds: [projectId] }, userKey === 'dev-sales' ? 200 : 403);
  }

  if (path === '/api/procurement/import/preview') {
    return json(procurementExcelPreviewResponse(), userKey === 'dev-procurement' ? 200 : 403);
  }

  if (path === '/api/procurement/import/apply') {
    return json({ appliedRowCount: 1 }, userKey === 'dev-procurement' ? 200 : 403);
  }

  if (path === '/api/deleted-projects') {
    return json({
      items: [deletedProjectListItem(userKey)],
      page: 1,
      pageSize: 20,
      totalCount: 1
    }, canReadDeletedProjects(userKey) ? 200 : 403);
  }

  if (path === '/api/deleted-projects/purge-all' && init?.method === 'POST') {
    return json({ deletedProjectCount: 1 }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === `/api/deleted-projects/${projectId}/purge` && init?.method === 'DELETE') {
    return json({ deletedProjectCount: 1 }, userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === `/api/deleted-projects/${projectId}/restore` && init?.method === 'POST') {
    return json(projectDetail(canReadSalesAmount(userKey), 'Cancelled', 'Deleted Project'), userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === `/api/deleted-projects/${projectId}`) {
    return json({
      ...deletedProjectListItem(userKey),
      statusReason: '삭제 전 상태 사유',
      panels: panels(),
      auditHistory: []
    }, canReadDeletedProjects(userKey) ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}` && init?.method === 'PATCH') {
    return json(projectDetail(canReadSalesAmount(userKey), 'Active', 'TASK-003A Demo'));
  }

  if (path === `/api/projects/${projectId}/workflow`) {
    return json(projectWorkflowResponse(projectId));
  }

  if (path === `/api/projects/${projectId}/set-structure`) {
    return json({
      projectId,
      structureMode: 'FlatPanel',
      isLegacyFlat: false,
      canEditOrder: false,
      canEditDesign: false,
      specs: [],
      orderedProcurementItems: [],
      recoveryCases: []
    });
  }

  if (path === `/api/projects/${projectId}`) {
    return json(projectDetail(canReadSalesAmount(userKey), 'Active', 'TASK-003A Demo'));
  }

  if (path === `/api/projects/${onHoldProjectId}/workflow`) {
    return json(projectWorkflowResponse(onHoldProjectId));
  }

  if (path === `/api/projects/${onHoldProjectId}`) {
    return json(projectDetail(canReadSalesAmount(userKey), 'OnHold', 'OnHold Project', onHoldProjectId));
  }

  if (path === `/api/projects/${projectId}/panels`) {
    return json(panels());
  }

  if (path === `/api/projects/${onHoldProjectId}/panels`) {
    return json(panels(onHoldProjectId));
  }

  if (path.startsWith(`/api/projects/${projectId}/panels/`)) {
    return json(panels()[0]);
  }

  if (path === `/api/projects/${projectId}/audit-history`) {
    if (userKey !== 'dev-admin') {
      return json({ title: 'Forbidden' }, 403);
    }

    return json({
      items: [
        {
          auditEventId: '73000000-0000-0000-0000-000000000001',
          entityType: 'Project',
          entityId: projectId,
          projectId,
          action: 'ProjectCreated',
          changedByUserId: salesOwnerId,
          changedByUserName: 'Dev Sales User',
          changedAtUtc: '2026-06-25T00:00:00Z',
          correlationId: 'test'
        }
      ]
    });
  }

  if (path === `/api/projects/${onHoldProjectId}/audit-history`) {
    return json({ items: [] });
  }

  if (path === `/api/projects/${projectId}/panel-information`) {
    return json(panelInformation(projectId));
  }

  if (path === `/api/projects/${onHoldProjectId}/panel-information`) {
    return json(panelInformation(onHoldProjectId));
  }

  if (path === `/api/projects/${projectId}/panel-information/history`
      || path === `/api/projects/${onHoldProjectId}/panel-information/history`) {
    return json(panelInformationHistory(), userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === '/api/production-planning/summary') {
    return json(productionPlanningSummaryResponse());
  }

  if (path === '/api/manufacturing/release-candidates') {
    return json({
      projects: [{
        projectId,
        projectCode: 'PJT-003A',
        projectTitle: 'TASK-003A Demo',
        activeItemCount: 4,
        completedItemCount: 3,
        panels: panelIds.map((panelId, index) => ({
          panelId,
          displayCode: `PANEL-${String(index + 1).padStart(2, '0')}`,
          panelName: `MCC-${String.fromCharCode(65 + index)}`,
          panelInfoCompleted: true,
          kittingCompleted: index === 0,
          released: manufacturingReleasedPanelIds.has(panelId),
          workItemStatus: manufacturingReleasedPanelIds.has(panelId) ? 'Requested' : null,
          releasedAtUtc: manufacturingReleasedPanelIds.has(panelId) ? '2026-07-21T08:00:00Z' : null,
          selectable: !manufacturingReleasedPanelIds.has(panelId)
        }))
      }]
    });
  }

  if (path === '/api/manufacturing/releases' && init?.method === 'POST') {
    if (userKey !== 'dev-production') return json({ title: 'forbidden' }, 403);
    const body = JSON.parse(String(init.body)) as { operationId: string; panelIds: string[] };
    body.panelIds.forEach((panelId) => manufacturingReleasedPanelIds.add(panelId));
    return json({
      operationId: body.operationId,
      releasedPanelCount: body.panelIds.length,
      generatedWorkItemCount: body.panelIds.length,
      replayed: false
    });
  }

  if (path === '/api/production-planning/projects') {
    return json(productionPlanningProjectListResponse());
  }

  if (path === '/api/production-planning/product-types') {
    return json(productionProductTypesResponse());
  }

  if (path === '/api/production-planning/settings/templates' && init?.method === 'PATCH') {
    return json(productionTemplateSettingsResponse(), userKey === 'dev-production' ? 200 : 403);
  }

  if (path === '/api/production-planning/settings/templates') {
    return json(productionTemplateSettingsResponse(), userKey === 'dev-production' ? 200 : 403);
  }

  if (path.startsWith('/api/production-planning/settings/templates/') && init?.method === 'PATCH') {
    return json(productionTemplateSettingsResponse(), userKey === 'dev-production' ? 200 : 403);
  }

  if (path === '/api/calendar/business-days') {
    return json({
      from: '2026-07-01',
      to: '2026-07-03',
      countryCode: 'KR',
      days: [
        {
          date: '2026-07-01',
          isWeekend: false,
          isHoliday: false,
          isCompanyHoliday: false,
          isBusinessDay: true,
          holidayName: null,
          holidayType: null
        },
        {
          date: '2026-07-02',
          isWeekend: false,
          isHoliday: true,
          isCompanyHoliday: true,
          isBusinessDay: false,
          holidayName: '회사 창립기념 휴일',
          holidayType: 'Company'
        },
        {
          date: '2026-07-03',
          isWeekend: false,
          isHoliday: true,
          isCompanyHoliday: false,
          isBusinessDay: false,
          holidayName: '공식 대체공휴일',
          holidayType: 'Substitute'
        }
      ]
    });
  }

  if (path === `/api/projects/${projectId}/production-planning` && init?.method === 'PATCH') {
    return json(productionPlanningResponse('Planned'), userKey === 'dev-production' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning/department-assignees` && init?.method === 'PATCH') {
    return json(departmentAssigneeScopeResponse(), userKey === 'dev-design' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning/department-assignees`) {
    return json(departmentAssigneeScopeResponse(), userKey === 'dev-design' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning/set-defaults` && init?.method === 'PATCH') {
    return json(productionPlanningSetScopedResponse('Planned'), userKey === 'dev-production' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning`) {
    return json(useSetScopedProductionPlan ? productionPlanningSetScopedResponse() : productionPlanningResponse());
  }

  if (path === `/api/projects/${onHoldProjectId}/production-planning`) {
    return json(productionPlanningResponse('NotPlanned', onHoldProjectId));
  }

  if (path === `/api/projects/${projectId}/production-planning/history`) {
    return json(productionPlanningHistory(), userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning/export-template`) {
    return Promise.resolve(new Response(new Blob(['xlsx']), {
      status: userKey === 'dev-production' ? 200 : 403,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': "attachment; filename*=UTF-8''Production_Planning_Template.xlsx"
      }
    }));
  }

  if (path === `/api/projects/${projectId}/production-planning/import/preview`) {
    return json(productionPlanningExcelPreviewResponse(), userKey === 'dev-production' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/production-planning/import/apply`) {
    return json({ appliedRowCount: 1, skippedRowCount: 0, appliedProjectIds: [projectId] }, userKey === 'dev-production' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/procurement` && init?.method === 'PATCH') {
    return json(procurementResponse(), userKey === 'dev-procurement' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/procurement`) {
    return json(procurementResponse());
  }

  if (path === `/api/projects/${projectId}/procurement/history`) {
    return json(procurementHistory(), userKey === 'dev-admin' ? 200 : 403);
  }

  if (path === `/api/projects/${projectId}/procurement/import/template`) {
    return Promise.resolve(new Response(new Blob(['xlsx']), {
      status: userKey === 'dev-procurement' ? 200 : 403,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': "attachment; filename*=UTF-8''Procurement_Plan_Template.xlsx"
      }
    }));
  }

  if (path === '/api/procurement/import/template') {
    return Promise.resolve(new Response(new Blob(['xlsx']), {
      status: userKey === 'dev-procurement' ? 200 : 403,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': "attachment; filename*=UTF-8''Procurement_Plan_Template.xlsx"
      }
    }));
  }

  if (path === '/api/procurement/settings/required-items' && init?.method === 'PATCH') {
    return json(procurementRequiredItemSettings(), userKey === 'dev-procurement' ? 200 : 403);
  }

  if (path === '/api/procurement/settings/required-items') {
    return json(procurementRequiredItemSettings());
  }

  if (path.startsWith('/api/procurement/settings/required-items/') && init?.method === 'PATCH') {
    return json(procurementRequiredItemSettings(), userKey === 'dev-procurement' ? 200 : 403);
  }

  if (path === '/api/materials/receipts' && init?.method === 'PATCH') {
    return json({ title: 'validation', errors: { ReceiptCompleted: ['입고 완료값은 상태 흐름에서 자동 계산됩니다.'] } }, 400);
  }

  if (path === '/api/materials/receipts') {
    return json(materialReceiptResponse(url.searchParams.get('includeCompleted') === 'true'));
  }

  if (path === '/api/quality/iqc/reconcile' && init?.method === 'POST') {
    return json({ recoveredReceiptCount: 0, ensuredAttemptCount: 0 });
  }

  if (path === '/api/quality/iqc') {
    return json({ items: [] });
  }

  if (path === '/api/procurement/dashboard') {
    return json(procurementDashboardResponse());
  }

  if (path.endsWith('/delete')) {
    return json({
      ...deletedProjectListItem(userKey),
      statusReason: null,
      panels: panels(),
      auditHistory: []
    });
  }

  if (path.includes('/change-panel-count') || path.endsWith('/hold') || path.endsWith('/resume') || path.endsWith('/cancel') || path.endsWith('/reactivate')) {
    return json(projectDetail(canReadSalesAmount(userKey), 'OnHold', 'TASK-003A Demo'));
  }

  return json({ title: 'not found' }, 404);
}

function currentUser(userKey: string) {
  const permissions = ['projects.read', 'Project.Read.All'];
  if (userKey === 'dev-sales') {
    permissions.push('Project.Create', 'Project.Update', 'Project.Hold', 'Project.Cancel', 'Project.Delete', 'Project.Deleted.Read', 'Project.SalesAmount.Read', 'PanelInfo.Update');
  }

  if (userKey === 'dev-design' || userKey === 'dev-production') {
    permissions.push('PanelInfo.Update');
  }

  if (userKey === 'dev-production') {
    permissions.push('ProductionPlan.Update');
  }

  if (userKey === 'dev-admin') {
    permissions.push(
      'Project.Deleted.Read',
      'Project.SalesAmount.Read',
      'Audit.Read.All',
      'users.manage',
      'admin-history.read'
    );
  }

  if (userKey === 'dev-procurement') {
    permissions.push('ProcurementPlan.Update', 'MaterialReceipt.Update');
  }

  if (userKey === 'dev-materials') {
    permissions.push('MaterialReceipt.Update');
  }

  if (userKey === 'dev-quality') {
    permissions.push('quality.inspect');
  }

  const departmentByUser: Record<string, [string, string]> = {
    'dev-admin': ['administration', '관리'],
    'dev-sales': ['sales', '영업'],
    'dev-design': ['design', '설계'],
    'dev-production': ['production-planning', '생산관리'],
    'dev-procurement': ['procurement', '구매'],
    'dev-materials': ['materials', '자재'],
    'dev-manufacturing': ['manufacturing', '제조'],
    'dev-quality': ['quality', '품질'],
    'dev-logistics': ['logistics', '물류']
  };
  const [department, departmentName] = departmentByUser[userKey] ?? ['readonly', '조회'];
  const roles = [userKey === 'dev-admin' ? 'system-administrator' : userKey.replace('dev-', '')];
  const principal = {
    userId: `50000000-0000-0000-0000-${userKey === 'dev-admin' ? '000000000001' : '000000000002'}`,
    developmentUserKey: userKey,
    displayName: userKey,
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department,
    departmentName,
    profilePhotoVersion: null,
    roles
  };
  return {
    userId: principal.userId,
    developmentUserKey: userKey,
    displayName: userKey,
    email: null,
    authProvider: 'Dev',
    isActive: true,
    approvalPending: false,
    department,
    departmentName,
    profilePhotoVersion: null,
    roles,
    permissions,
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal
  };
}

function projectListItem(userKey: string, status: 'Active' | 'OnHold' | 'Cancelled' | 'Completed', title: string, id = projectId) {
  const item: Record<string, unknown> = {
    projectId: id,
    customerName: 'EMI Test Customer',
    item: 'UL67',
    projectCode: 'PJT-003A',
    projectTitle: title,
    activePanelCount: 4,
    deliveryDate: '2026-10-10',
    salesOwnerUserId: salesOwnerId,
    salesOwnerName: 'Dev Sales User',
    packagingMethod: 'WoodenCrate',
    deliveryLocation: 'Dock A',
    fatRequired: false,
    status,
    projectWorkStatus: status === 'Active' ? 'ProductionPlanning' : status,
    projectProgressPercent: status === 'Active' ? 6 : null,
    createdAt: '2026-06-25T00:00:00Z',
    updatedAt: '2026-06-25T00:00:00Z'
  };

  if (canReadSalesAmount(userKey)) {
    item.salesAmount = 1250000.5;
    item.currencyCode = 'KRW';
  }

  return item;
}

function deletedProjectListItem(userKey: string) {
  return {
    ...projectListItem(userKey, 'Cancelled', 'Deleted Project', projectId),
    deletedAtUtc: '2026-06-25T01:00:00Z',
    deletedByUserId: salesOwnerId,
    deletedByUserName: 'Dev Sales User',
    deleteReason: '오등록 정리'
  };
}

function projectDetail(
  includeSalesAmount: boolean,
  status: 'Active' | 'OnHold' | 'Cancelled',
  title: string,
  id = projectId
) {
  return {
    ...projectListItem(includeSalesAmount ? 'dev-sales' : 'dev-manufacturing', status, title, id),
    lseTaskNumber: 'LSE-104-105',
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    manufacturingStepCount: 4,
    oqcStepCount: 4,
    statusReason: status === 'Active' ? null : '상태 사유'
  };
}

function panels(id = projectId) {
  return panelIds.map((panelId, index) => ({
    panelId,
    projectId: id,
    sequenceNumber: index + 1,
    displayCode: `P0${index + 1}`,
    panelName: null,
    width: null,
    height: null,
    depth: null,
    panelStatus: 'Active',
    workflowStage: 'BeforeManufacturing',
    panelInfoCompleted: false,
    qrEligible: false,
    createdAt: '2026-06-25T00:00:00Z',
    updatedAt: '2026-06-25T00:00:00Z'
  }));
}

function ul891SetStructure() {
  return {
    projectId,
    structureMode: 'Ul891Set',
    isLegacyFlat: false,
    canEditOrder: false,
    canEditDesign: true,
    specs: [{
      specId: 'spec-1',
      specNo: 1,
      name: 'MCC 메인 세트',
      rowVersion: 1,
      activeInstanceCount: 1,
      currentDesign: [{
        slotId: 'slot-a',
        positionNumber: 1,
        panelName: 'MAIN A',
        panelSpecification: 'UL891 TYPE A',
        widthMm: 800,
        heightMm: 1800,
        depthMm: 400,
        rowVersion: 1
      }],
      versions: [{
        versionId: 'version-1',
        versionNumber: 1,
        status: 'Draft',
        revisionReason: '초기 설계',
        publishedAtUtc: null,
        components: [{
          componentId: 'component-a',
          componentCode: 'A',
          panelName: 'MAIN A',
          panelSpecification: 'UL891 TYPE A',
          widthMm: 800,
          heightMm: 1800,
          depthMm: 400,
          sortOrder: 1
        }]
      }],
      instances: [{
        instanceId: 'instance-1',
        instanceNumber: 1,
        specVersionId: 'version-1',
        specVersionNumber: 1,
        status: 'Active',
        rowVersion: 1,
        hasStarted: false,
        hasDeliveredPanel: false,
        panels: [{
          panelId: panelIds[0],
          sequenceNumber: 1,
          displayCode: 'P01',
          componentCode: 'A',
          designSlotId: 'slot-a',
          positionNumber: 1,
          panelName: 'MAIN A',
          panelSpecification: 'UL891 TYPE A',
          panelStatus: 'Active',
          workflowStage: 'BeforeManufacturing',
          packingUnitLabel: null,
          departureDate: null,
          delivered: false
        }]
      }]
    }],
    orderedProcurementItems: [],
    recoveryCases: []
  };
}

function panelInformation(id = projectId) {
  return {
    projectId: id,
    projectStatus: id === onHoldProjectId ? 'OnHold' : 'Active',
    packagingMethod: 'WoodenCrate',
    activePanelCount: 4,
    panelInfoCompletedCount: 0,
    panelInfoPendingCount: 4,
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
    duplicatePanelNameGroupCount: 0,
    supportsPanelGrouping: true,
    projectPanelInformationCompleted: false,
    panelInformationStatusMessage: null,
    panels: panelIds.map((panelId, index) => ({
      panelId,
      projectId: id,
      sequenceNumber: index + 1,
      panelNumber: `No.${index + 1}`,
      displayCode: `P0${index + 1}`,
      panelName: null,
      drawingNumber: null,
      displayName: `No.${index + 1} · 패널명 미입력`,
      widthMm: null,
      heightMm: null,
      depthMm: null,
      panelStatus: 'Active',
      workflowStage: 'BeforeManufacturing',
      panelInfoCompleted: false,
      qrEligible: false,
      hasDuplicateName: false,
      duplicateNameCount: 0,
      panelGroupNumber: null,
      panelInfoVersion: 0,
      createdAt: '2026-06-25T00:00:00Z',
      updatedAt: '2026-06-25T00:00:00Z',
      panelInfoUpdatedAtUtc: null,
      panelInfoUpdatedByUserId: null,
      panelInfoUpdatedByUserName: null
    }))
  };
}

function panelInformationWithSize(id = projectId, panelName = 'DRIFT-A') {
  const response = panelInformation(id);
  Object.assign(response.panels[0] as unknown as Record<string, unknown>, {
    panelName,
    displayName: `No.1 · ${panelName}`,
    widthMm: 800,
    heightMm: 1800,
    depthMm: 400,
    panelInfoVersion: 2,
    panelInfoCompleted: true,
    qrEligible: true
  });
  return response;
}

function panelInformationHistory() {
  return {
    groups: [
      {
        groupId: 'import:91000000000000000000000000000001',
        actionType: 'PanelInfoUpdated',
        inputSource: 'Excel',
        changedByUserId: salesOwnerId,
        changedByName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:30:00Z',
        reason: 'Excel inch 변경',
        importBatchId: '91000000-0000-0000-0000-000000000001',
        importFileName: 'panel_information_01.xlsx',
        importUploadedAtUtc: '2026-06-26T01:29:00Z',
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[1],
            panelNumber: 'No.2',
            panelDisplayName: 'No.2 · PNL-2',
            displayCode: 'P02',
            fieldName: 'WidthMm',
            oldValue: '700',
            newValue: '800.1',
            inputUnit: 'Inch',
            originalInputValue: '31.5'
          }
        ]
      },
      {
        groupId: 'correlation:corr-direct',
        actionType: 'PanelInfoUpdated',
        inputSource: 'Direct',
        changedByUserId: salesOwnerId,
        changedByName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        reason: '직접 입력',
        importBatchId: null,
        importFileName: null,
        importUploadedAtUtc: null,
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[0],
            panelNumber: 'No.1',
            panelDisplayName: 'No.1 · PNL-1',
            displayCode: 'P01',
            fieldName: 'PanelName',
            oldValue: '',
            newValue: 'PNL-1',
            inputUnit: 'Mm',
            originalInputValue: null
          }
        ]
      },
      {
        groupId: 'correlation:corr-legacy',
        actionType: 'PanelInfoUpdated',
        inputSource: null,
        changedByUserId: null,
        changedByName: null,
        changedAtUtc: '2026-06-26T00:30:00Z',
        reason: null,
        importBatchId: null,
        importFileName: null,
        importUploadedAtUtc: null,
        affectedPanelCount: 1,
        changeCount: 1,
        changes: [
          {
            entityType: 'Panel',
            entityId: panelIds[2],
            panelNumber: 'No.3',
            panelDisplayName: 'No.3 · PNL-LEGACY',
            displayCode: 'P03',
            fieldName: 'PanelName',
            oldValue: '',
            newValue: 'PNL-LEGACY',
            inputUnit: null,
            originalInputValue: null
          }
        ]
      }
    ],
    auditEvents: [
      {
        auditEventId: '90000000-0000-0000-0000-000000000001',
        entityType: 'Panel',
        entityId: panelIds[1],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.2',
        panelDisplayName: 'No.2 · PNL-2',
        displayCode: 'P02',
        fieldName: 'WidthMm',
        oldValue: '700',
        newValue: '800.1',
        reason: 'Excel inch 변경',
        changedByUserId: salesOwnerId,
        changedByUserName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:30:00Z',
        correlationId: 'corr-excel',
        inputSource: 'Excel',
        importBatchId: '91000000-0000-0000-0000-000000000001',
        inputUnit: 'Inch',
        originalInputValue: '31.5',
        importFileName: 'panel_information_01.xlsx',
        importUploadedAtUtc: '2026-06-26T01:29:00Z'
      },
      {
        auditEventId: '90000000-0000-0000-0000-000000000002',
        entityType: 'Panel',
        entityId: panelIds[0],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.1',
        panelDisplayName: 'No.1 · PNL-1',
        displayCode: 'P01',
        fieldName: 'PanelName',
        oldValue: '',
        newValue: 'PNL-1',
        changedByUserId: salesOwnerId,
        changedByUserName: 'Dev Design User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        correlationId: 'corr-direct',
        inputSource: 'Direct',
        inputUnit: 'Mm'
      },
      {
        auditEventId: '90000000-0000-0000-0000-000000000003',
        entityType: 'Panel',
        entityId: panelIds[2],
        projectId,
        action: 'PanelInfoUpdated',
        panelNumber: 'No.3',
        panelDisplayName: 'No.3 · PNL-LEGACY',
        displayCode: 'P03',
        fieldName: 'PanelName',
        oldValue: '',
        newValue: 'PNL-LEGACY',
        changedByUserId: null,
        changedByUserName: null,
        changedAtUtc: '2026-06-26T00:30:00Z',
        correlationId: 'corr-legacy'
      }
    ],
    excelImportBatches: [
      {
        importBatchId: '91000000-0000-0000-0000-000000000001',
        projectId,
        originalFileName: 'panel_information_01.xlsx',
        fileSizeBytes: 1234,
        fileSha256: 'a'.repeat(64),
        inputUnit: 'Inch',
        totalRowCount: 1,
        newPanelCount: 0,
        changedPanelCount: 1,
        unchangedPanelCount: 0,
        skippedPanelCount: 0,
        uploadedByUserId: salesOwnerId,
        uploadedByUserName: 'Dev Design User',
        uploadedAtUtc: '2026-06-26T01:29:00Z',
        reason: 'Excel inch 변경'
      }
    ]
  };
}

function canReadSalesAmount(userKey: string) {
  return userKey === 'dev-sales' || userKey === 'dev-admin';
}

function procurementRequiredItemSettings() {
  return [
    {
      itemCode: 'UL67',
      activeTemplateId: '93000000-0000-0000-0000-000000000001',
      activeTemplateVersion: 1,
      rows: [
        {
          templateRowId: '93000000-0000-0000-0000-000000000101',
          sequenceNumber: 1,
          itemName: '차단기',
          isRequired: true,
          isActive: true
        },
        {
          templateRowId: '93000000-0000-0000-0000-000000000102',
          sequenceNumber: 2,
          itemName: '외함',
          isRequired: true,
          isActive: true
        }
      ]
    },
    {
      itemCode: 'RPP',
      activeTemplateId: null,
      activeTemplateVersion: null,
      rows: []
    }
  ];
}

function procurementResponse() {
  return {
    projectId,
    projectTitle: 'TASK-003A Demo',
    projectCode: 'PJT-003A',
    iqcRoutingPolicy: 'AllReceipts',
    items: [
      {
        itemId: '76000000-0000-0000-0000-000000000001',
        projectId,
        projectTitle: 'TASK-003A Demo',
        projectCode: 'PJT-003A',
        projectDeliveryDate: '2026-10-10',
        shipmentDisplayDate: '2026-10-10',
        sequenceNumber: 1,
        sourceProjectText: 'TASK-003A Demo',
        sourceProjectCodeText: 'PJT-003A',
        standardLeadTime: '4W',
        orderItem: 'Relay',
        supplierName: 'Vendor A',
        technicalOwner: 'Owner A',
        orderDate: '2026-06-20',
        expectedReceiptDate: '2026-06-29',
        issueNote: '확인 필요',
        supplyType: 'CustomerSupplied',
        orderQuantity: 100,
        orderUnit: 'EA',
        receiptCompleted: false,
        receiptCompletedAtUtc: null,
        receiptCompletedByUserId: null,
        receiptCompletedByUserName: null,
        receiptCompletionNote: '부분 입고 24/100 EA',
        rowVersion: 1,
        dDayText: 'D-3'
      },
      {
        itemId: '76000000-0000-0000-0000-000000000003',
        projectId,
        projectTitle: 'TASK-003A Demo',
        projectCode: 'PJT-003A',
        projectDeliveryDate: '2026-10-10',
        shipmentDisplayDate: '2026-10-10',
        sequenceNumber: 2,
        sourceProjectText: 'TASK-003A Demo',
        sourceProjectCodeText: 'PJT-003A',
        standardLeadTime: '2W',
        orderItem: 'Completed Relay',
        supplierName: 'Vendor A',
        technicalOwner: 'Owner A',
        orderDate: '2026-06-20',
        expectedReceiptDate: '2026-06-29',
        issueNote: null,
        supplyType: 'Purchased',
        orderQuantity: null,
        orderUnit: null,
        receiptCompleted: true,
        receiptCompletedAtUtc: '2026-06-07T12:30:00',
        receiptCompletedByUserId: '50000000-0000-0000-0000-000000000012',
        receiptCompletedByUserName: 'Dev Materials User',
        receiptCompletionNote: '완료',
        rowVersion: 1,
        dDayText: 'D-3'
      }
    ]
  };
}

function projectSummaryResponse() {
  return {
    totalProjectCount: 3,
    activeProjectCount: 1,
    onHoldProjectCount: 1,
    completedProjectCount: 1,
    cancelledProjectCount: 1,
    qrEligiblePanelCount: 2,
    manufacturingCompletedCount: 1,
    inspectionCompletedCount: 1,
    manufacturingCompletedProjectCount: 1,
    inspectionCompletedProjectCount: 1
  };
}

function projectWorkflowResponse(id = projectId) {
  return {
    projectId: id,
    generatedWorkItemCount: 1,
    requiredStageCount: 17,
    completedRequiredStageCount: 1,
    progressPercent: 6,
    currentStageCode: 'ProductionPlanning',
    currentStageName: '생산계획·담당자',
    currentDepartmentCode: 'production-planning',
    currentDepartmentLabel: '생산관리',
    stages: [
      {
        stageCode: 'SalesProjectCreated',
        sequenceNumber: 1,
        departmentCode: 'sales',
        departmentLabel: '영업',
        stageName: '프로젝트 생성',
        isOptional: false,
        status: 'Completed',
        statusLabel: '완료',
        workItemCount: 0,
        completedAtUtc: '2026-06-25T00:00:00Z'
      },
      {
        stageCode: 'ProductionPlanning',
        sequenceNumber: 2,
        departmentCode: 'production-planning',
        departmentLabel: '생산관리',
        stageName: '생산계획·담당자',
        isOptional: false,
        status: 'Requested',
        statusLabel: '업무 요청됨',
        workItemCount: 1,
        completedAtUtc: null
      },
      {
        stageCode: 'KittingCompleted',
        sequenceNumber: 8,
        departmentCode: 'materials',
        departmentLabel: '자재',
        stageName: '키팅 완료',
        isOptional: true,
        status: 'NotStarted',
        statusLabel: '미시작',
        workItemCount: 0,
        completedAtUtc: null
      },
      {
        stageCode: 'PackingCompleted',
        sequenceNumber: 15,
        departmentCode: 'logistics',
        departmentLabel: '물류',
        stageName: '포장 완료',
        isOptional: false,
        status: 'NotStarted',
        statusLabel: '미시작',
        workItemCount: 0,
        completedAtUtc: null
      },
      {
        stageCode: 'DeliveryCompleted',
        sequenceNumber: 17,
        departmentCode: 'logistics',
        departmentLabel: '물류',
        stageName: '납품 완료',
        isOptional: false,
        status: 'NotStarted',
        statusLabel: '미시작',
        workItemCount: 0,
        completedAtUtc: null
      },
      {
        stageCode: 'SalesSettlementCompleted',
        sequenceNumber: 18,
        departmentCode: 'sales',
        departmentLabel: '영업',
        stageName: '세금계산서·완료',
        isOptional: false,
        status: 'NotStarted',
        statusLabel: '미시작',
        workItemCount: 0,
        completedAtUtc: null
      }
    ]
  };
}

function projectExcelPreviewResponse() {
  return {
    fileSha256: 'project-excel-sha',
    totalRows: 1,
    newCount: 1,
    needsReviewCount: 0,
    errorCount: 0,
    rows: [
      {
        excelRowNumber: 4,
        resultType: 'New',
        customerName: 'TEST CUSTOMER',
        item: 'TEST PANEL',
        projectCode: 'EXCEL-001',
        projectTitle: 'Excel Project',
        panelCount: 3,
        deliveryDate: '2026-10-10',
        packagingMethod: 'WoodenCrate',
        salesAmount: null,
        currencyCode: null,
        deliveryLocation: null,
        fatRequired: false,
        salesOwnerText: 'dev-sales',
        salesOwnerUserId: salesOwnerId,
        salesOwnerName: 'Dev Sales User',
        errorMessages: []
      }
    ]
  };
}

function productionPlanningSummaryResponse() {
  return {
    notPlannedCount: 1,
    planningCount: 1,
    plannedCount: 1,
    missingAssigneeProjectCount: 1
  };
}

function productionPlanningProjectListResponse() {
  return {
    projects: [
      {
        projectId,
        projectTitle: 'TASK-003A Demo',
        customerName: 'EMI Test Customer',
        projectCode: 'PJT-003A',
        item: 'UL67',
        activePanelCount: 4,
        deliveryDate: '2026-10-10',
        projectStatus: 'Active',
        planStatus: 'Planning',
        planStatusLabel: '작성 중',
        productTypeCode: 'UL67',
        productTypeName: 'UL67',
        requiredStepCount: 3,
        plannedRequiredStepCount: 1,
        assigneeCount: 2
      }
    ]
  };
}

function productionProductTypesResponse() {
  const codes = ['UL67', 'UL891', 'UL508A', 'IEC', 'LLP', 'RPP'];
  return codes.map((code, index) => ({
    productTypeId: `77000000-0000-0000-0000-00000000000${index + 1}`,
    code,
    name: code,
    isActive: true,
    activeTemplateId: `77000000-0000-0000-0000-00000000010${index + 1}`,
    activeTemplateVersion: 1,
    steps: [
      {
        templateStepId: `77000000-0000-0000-0000-00000000020${index * 4 + 1}`,
        sequenceNumber: 1,
        stepName: '자재 입고',
        isRequired: true
      },
      {
        templateStepId: `77000000-0000-0000-0000-00000000020${index * 4 + 2}`,
        sequenceNumber: 2,
        stepName: '조립 시작',
        isRequired: true
      },
      {
        templateStepId: `77000000-0000-0000-0000-00000000020${index * 4 + 3}`,
        sequenceNumber: 3,
        stepName: '배선 시작',
        isRequired: true
      }
    ]
  }));
}

function productionTemplateSettingsResponse() {
  return productionProductTypesResponse().map((productType) => ({
    productTypeId: productType.productTypeId,
    code: productType.code,
    name: productType.name,
    activeTemplateId: productType.activeTemplateId,
    activeTemplateVersion: productType.activeTemplateVersion,
    steps: productType.steps.map((step) => ({
      templateStepId: step.templateStepId,
      sequenceNumber: step.sequenceNumber,
      stepName: step.stepName,
      isRequired: step.isRequired,
      isActive: true
    }))
  }));
}

function productionPlanningResponse(status: 'NotPlanned' | 'Planning' | 'Planned' = 'Planning', id = projectId) {
  const planned = status === 'Planned';
  const productType = productionProductTypesResponse()[0];
  return {
    projectId: id,
    projectTitle: id === onHoldProjectId ? 'OnHold Project' : 'TASK-003A Demo',
    projectCode: 'PJT-003A',
    deliveryDate: '2026-10-10',
    modelVersion: 'LEGACY',
    planId: '77000000-0000-0000-0000-000000000301',
    rowVersion: 1,
    planStatus: status,
    planStatusLabel: status === 'NotPlanned' ? '미등록' : planned ? '계획 완료' : '작성 중',
    productTypeId: productType.productTypeId,
    templateId: productType.activeTemplateId,
    productTypeCode: productType.code,
    productTypeName: productType.name,
    notes: '생산계획 검수',
    manufacturingSteps: [],
    availableSources: [
      {
        code: 'PURCHASE_ORDERED',
        departmentLabel: '구매',
        label: '발주 완료',
        requiresManufacturingDefinition: false,
        definitionKind: 'MaterialCategory',
        definitions: [{ definitionKey: '67000000-0000-0000-0000-000000000001', label: '외함' }],
        isOperational: true,
        operationalMessage: null
      },
      {
        code: 'PACKED',
        departmentLabel: '물류',
        label: '포장 완료',
        requiresManufacturingDefinition: false,
        definitionKind: 'None',
        definitions: [],
        isOperational: true,
        operationalMessage: null
      }
    ],
    items: productType.steps.map((step, index) => {
      const plannedDate = planned || index === 0 || index === 2 ? `2026-07-0${index + 1}` : null;
      return ({
      itemId: `77000000-0000-0000-0000-00000000040${index + 1}`,
      templateStepId: step.templateStepId,
      sequenceNumber: step.sequenceNumber,
      stepName: step.stepName,
      isRequired: step.isRequired,
      isCustom: false,
      definitionKey: `77000000-0000-0000-0000-00000000060${index + 1}`,
      plannedDate,
      plannedStartDate: plannedDate,
      plannedEndDate: plannedDate,
      actualStartDate: index === 0 ? '2026-07-01' : null,
      actualEndDate: index === 0 ? '2026-07-01' : null,
      assignedUserId: index === 0 ? '50000000-0000-0000-0000-000000000003' : null,
      assignedUserName: index === 0 ? 'Dev Production Planning User' : null,
      requiredHeadcount: index === 0 ? 3 : null,
      completedTargetCount: index === 0 ? 1 : 0,
      totalTargetCount: 1,
      progressPercent: index === 0 ? 100 : 0,
      scheduleStatus: index === 0 ? 'Completed' : 'NotStarted',
      scheduleStatusLabel: index === 0 ? '완료' : '대기',
      delayDays: 0,
      isBlocked: false,
      connections: [{ sourceCode: 'PACKED', sourceDefinitionKey: null }],
      evidence: [],
      note: index === 0 ? '입고 확인' : null,
      rowVersion: 0
    });
    }),
    assignees: responsibilityFixtures().map((item, index) => ({
      assigneeId: item.assignedUserId ? `77000000-0000-0000-0000-0000000005${String(index + 1).padStart(2, '0')}` : null,
      responsibilityType: item.responsibilityType,
      responsibilityLabel: item.responsibilityLabel,
      assignedUserId: item.assignedUserId,
      assignedUserName: item.assignedUserName,
      note: item.assignedUserId ? item.responsibilityLabel : null,
      rowVersion: 0
    })),
    assigneeCandidates: responsibilityFixtures().map((item) => ({
      responsibilityType: item.responsibilityType,
      users: item.candidateUserId ? [{ userId: item.candidateUserId, displayName: item.candidateUserName }] : []
    })),
    fallbacks: responsibilityFixtures().map((item) => ({
      responsibilityType: item.responsibilityType,
      responsibilityLabel: item.responsibilityLabel,
      userId: item.assignedUserId ?? salesOwnerId,
      displayName: item.assignedUserName ?? 'Dev Sales User',
      sourceLabel: item.assignedUserId ? '지정 담당자' : '영업담당자'
    }))
  };
}

function departmentAssigneeScopeResponse() {
  const designAssignees = responsibilityFixtures()
    .filter((fixture) => fixture.responsibilityType === 'DesignPrimary' || fixture.responsibilityType === 'DesignSecondary')
    .map((fixture, index) => ({
      assigneeId: `77000000-0000-0000-0000-0000000007${index + 1}`,
      responsibilityType: fixture.responsibilityType,
      responsibilityLabel: fixture.responsibilityLabel,
      assignedUserId: fixture.assignedUserId,
      assignedUserName: fixture.assignedUserName,
      note: null,
      rowVersion: 1
    }));
  return {
    projectId,
    projectTitle: 'TASK-003A Demo',
    projectCode: 'PJT-003A',
    departmentCode: 'design',
    departmentName: '설계',
    assignees: designAssignees,
    assigneeCandidates: designAssignees.map((assignee) => ({
      responsibilityType: assignee.responsibilityType,
      users: [{ userId: '50000000-0000-0000-0000-000000000010', displayName: 'Dev Design User' }]
    }))
  };
}

function productionPlanningSetScopedResponse(status: 'NotPlanned' | 'Planning' | 'Planned' = 'Planning') {
  const base = productionPlanningResponse(status);
  const items = base.items.map((item, index) => ({
    ...item,
    plannedDate: null,
    plannedStartDate: `2026-07-0${index + 1}`,
    plannedEndDate: `2026-07-0${index + 2}`,
    actualStartDate: index === 0 ? '2026-07-02' : null,
    actualEndDate: index === 0 ? '2026-07-03' : null,
    completedTargetCount: index === 0 ? 1 : 0,
    totalTargetCount: 1,
    progressPercent: index === 0 ? 100 : 0,
    scheduleStatus: index === 0 ? 'Completed' : 'NotStarted',
    scheduleStatusLabel: index === 0 ? '완료' : '미시작',
    delayDays: 0,
    isBlocked: false,
    connections: [{ sourceCode: 'MANUFACTURING_STEP_COMPLETED', sourceDefinitionKey: `78000000-0000-0000-0000-00000000020${index + 1}` }],
    evidence: []
  }));
  return {
    ...base,
    modelVersion: 'LINKED_V1',
    isSetScoped: true,
    selectedScope: null,
    scopes: [
      { scopeId: '78000000-0000-0000-0000-000000000001', setInstanceId: '78000000-0000-0000-0000-000000000011', label: 'MCC · 1번 세트', specName: 'MCC', specNumber: 1, instanceNumber: 1, status: 'Active', activePanelCount: 7, requiredItemCount: items.length, plannedRequiredItemCount: items.length, rowVersion: 1 },
      { scopeId: '78000000-0000-0000-0000-000000000002', setInstanceId: '78000000-0000-0000-0000-000000000012', label: 'MCC · 2번 세트', specName: 'MCC', specNumber: 1, instanceNumber: 2, status: 'Active', activePanelCount: 7, requiredItemCount: items.length, plannedRequiredItemCount: items.length, rowVersion: 1 }
    ],
    setDefault: {
      defaultId: '78000000-0000-0000-0000-000000000101',
      rowVersion: 1,
      items: items.map((item) => ({ ...item, connections: [] }))
    },
    items
  };
}

function productionPlanningExcelPreviewResponse() {
  return {
    fileSha256: 'production-planning-project-excel-sha',
    totalRows: 1,
    saveableCount: 1,
    blockedCount: 0,
    rows: [
      {
        excelRowNumber: 4,
        resultType: 'Changed',
        projectId,
        projectTitle: 'TASK-003A Demo',
        projectCode: 'PJT-003A',
        productTypeId: '72000000-0000-0000-0000-000000000001',
        productTypeCode: 'UL67',
        templateStepId: '72000000-0000-0000-0000-000000000101',
        stepName: '자재 도착',
        isCustomStep: false,
        isRequired: true,
        plannedDate: '2026-07-01',
        note: 'Excel preview',
        procurementAssigneeText: null,
        productionPlanningAssigneeText: null,
        manufacturingAssigneeText: null,
        qualityAssigneeText: null,
        logisticsAssigneeText: null,
        errorMessages: []
      }
    ]
  };
}

function responsibilityFixtures() {
  return [
    responsibilityFixture('SalesPrimary', '영업 정', salesOwnerId, 'Dev Sales User'),
    responsibilityFixture('SalesSecondary', '영업 부', salesOwnerId, 'Dev Sales User'),
    responsibilityFixture('DesignPrimary', '설계 정', '50000000-0000-0000-0000-000000000010', 'Dev Design User'),
    responsibilityFixture('DesignSecondary', '설계 부', '50000000-0000-0000-0000-000000000010', 'Dev Design User'),
    responsibilityFixture('ProductionPlanningPrimary', '생산관리 정', '50000000-0000-0000-0000-000000000003', 'Dev Production Planning User'),
    responsibilityFixture('ProductionPlanningSecondary', '생산관리 부', '50000000-0000-0000-0000-000000000003', 'Dev Production Planning User'),
    responsibilityFixture('ProcurementPrimary', '구매 정', '50000000-0000-0000-0000-000000000011', 'Dev Procurement User'),
    responsibilityFixture('ProcurementSecondary', '구매 부', '50000000-0000-0000-0000-000000000011', 'Dev Procurement User'),
    responsibilityFixture('MaterialsPrimary', '자재 정', '50000000-0000-0000-0000-000000000012', 'Dev Materials User'),
    responsibilityFixture('MaterialsSecondary', '자재 부', '50000000-0000-0000-0000-000000000012', 'Dev Materials User'),
    responsibilityFixture('ManufacturingPrimary', '제조 정', '50000000-0000-0000-0000-000000000004', 'Dev Manufacturing User'),
    responsibilityFixture('ManufacturingSecondary', '제조 부', '50000000-0000-0000-0000-000000000004', 'Dev Manufacturing User'),
    responsibilityFixture('LogisticsPrimary', '물류 정', '50000000-0000-0000-0000-000000000006', 'Dev Logistics User'),
    responsibilityFixture('LogisticsSecondary', '물류 부', '50000000-0000-0000-0000-000000000006', 'Dev Logistics User'),
    responsibilityFixture('QualityIQC', 'IQC 정', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityIQCSecondary', 'IQC 부', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityLQC', 'LQC 정', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityLQCSecondary', 'LQC 부', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityOQC', 'OQC 정', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityOQCSecondary', 'OQC 부', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityCustomerInspection', '전진검수/FAT 정', '50000000-0000-0000-0000-000000000005', 'Dev Quality User'),
    responsibilityFixture('QualityCustomerInspectionSecondary', '전진검수/FAT 부', '50000000-0000-0000-0000-000000000005', 'Dev Quality User')
  ];
}

function responsibilityFixture(
  responsibilityType: string,
  responsibilityLabel: string,
  userId: string | null,
  userName: string | null
) {
  return {
    responsibilityType,
    responsibilityLabel,
    assignedUserId: userId,
    assignedUserName: userName,
    candidateUserId: userId,
    candidateUserName: userName ?? ''
  };
}

function productionPlanningHistory() {
  return {
    groups: [
      {
        groupId: 'production-plan-direct',
        inputSource: 'Direct',
        changedByUserId: '50000000-0000-0000-0000-000000000003',
        changedByName: 'Dev Production Planning User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        reason: '생산계획 입력',
        affectedItemCount: 3,
        changeCount: 3,
        changes: [
          {
            entityId: '77000000-0000-0000-0000-000000000401',
            entityType: 'ProductionPlanItem',
            fieldName: 'planned_date',
            oldValue: null,
            newValue: '2026-07-01'
          }
        ]
      }
    ]
  };
}

function procurementDashboardResponse() {
  return {
    summary: {
      pendingReceiptCount: 1,
      receiptCompletedCount: 1,
      pastExpectedReceiptDateCount: 1
    },
    projects: [
      {
        projectId,
        projectTitle: 'TASK-003A Demo',
        customerName: 'Demo Customer',
        projectCode: 'PJT-003A',
        item: 'Demo Item',
        activePanelCount: 2,
        deliveryDate: '2026-10-10',
        procurementItemCount: 2,
        receiptCompletedCount: 1,
        nearestExpectedReceiptDate: '2026-06-29',
        dDayText: 'D-3'
      }
    ]
  };
}

function procurementExcelPreviewResponse() {
  return {
    fileSha256: 'procurement-excel-sha',
    totalRows: 2,
    newCount: 1,
    changedCount: 0,
    unchangedCount: 0,
    skippedCount: 0,
    missingFromUploadCount: 0,
    needsReviewCount: 1,
    errorCount: 0,
    reasonRequired: false,
    projectMatches: [
      {
        sourceGroupSequence: 1,
        excelProjectTitle: 'TASK-003A Demo',
        excelProjectCode: 'PJT-003A',
        matchedProjectId: projectId,
        matchedProjectTitle: 'TASK-003A Demo',
        matchedProjectCode: 'PJT-003A',
        matchStatus: 'Matched',
        candidates: []
      },
      {
        sourceGroupSequence: 2,
        excelProjectTitle: 'Unknown Project',
        excelProjectCode: 'UNKNOWN',
        matchedProjectId: null,
        matchedProjectTitle: null,
        matchedProjectCode: null,
        matchStatus: 'NeedsReview',
        candidates: [
          {
            projectId,
            projectTitle: 'TASK-003A Demo',
            projectCode: 'PJT-003A',
            matchType: 'Code'
          }
        ]
      }
    ],
    expectedVersions: [],
    rows: [
      {
        excelRowNumber: 4,
        sourceGroupSequence: 1,
        projectId,
        itemId: null,
        expectedRowVersion: null,
        resultType: 'New',
        sourceProjectText: 'TASK-003A Demo',
        sourceProjectCodeText: 'PJT-003A',
        standardLeadTime: '4W',
        orderItem: 'Relay',
        supplierName: 'Vendor A',
        technicalOwner: 'Owner A',
        orderDate: '2026-06-20',
        expectedReceiptDate: '2026-06-29',
        shipmentText: '저장 안 함',
        issueNote: '확인 필요',
        receiptCompleted: false,
        errorMessages: []
      },
      {
        excelRowNumber: 5,
        sourceGroupSequence: 2,
        projectId: null,
        itemId: null,
        expectedRowVersion: null,
        resultType: 'NeedsReview',
        sourceProjectText: 'Unknown Project',
        sourceProjectCodeText: 'UNKNOWN',
        standardLeadTime: '5W',
        orderItem: 'Cable',
        supplierName: 'Vendor B',
        technicalOwner: 'Owner B',
        orderDate: '2026-06-21',
        expectedReceiptDate: null,
        shipmentText: '저장 안 함',
        issueNote: null,
        receiptCompleted: null,
        errorMessages: ['확인할 프로젝트가 있습니다. 프로젝트를 선택해 주세요.']
      }
    ]
  };
}

function materialReceiptResponse(includeCompleted: boolean) {
  const pending = {
    itemId: '76000000-0000-0000-0000-000000000001',
    projectId,
    projectTitle: 'TASK-003A Demo',
    projectCode: 'PJT-003A',
    orderItem: 'Relay',
    supplierName: 'Vendor A',
    supplyType: 'Purchased',
    expectedReceiptDate: '2026-06-29',
    orderQuantity: 100,
    orderUnit: 'EA',
    arrivedQuantity: 24,
    confirmedQuantity: 0,
    remainingQuantity: 100,
    processingQuantity: 24,
    customerSupplyOverdue: false,
    arrivalsClosed: false,
    arrivalsClosedAtUtc: null,
    receiptCompleted: false,
    rowVersion: 2,
    receipts: [{
      receiptId: '77000000-0000-0000-0000-000000000001',
      quantity: 24,
      unit: 'EA',
      arrivalDate: '2026-06-27',
      note: '1차 도착',
      status: 'Arrived',
      isLegacy: false,
      version: 1,
      createdAtUtc: '2026-06-27T01:00:00Z',
      confirmedAtUtc: null,
      cancellationReason: null,
      iqcAttempts: []
    }]
  };
  const completed = {
    ...pending,
    itemId: '76000000-0000-0000-0000-000000000002',
    orderItem: 'Completed Relay',
    arrivalsClosed: true,
    arrivalsClosedAtUtc: '2026-06-07T12:30:00Z',
    receiptCompleted: true,
    rowVersion: 4,
    receipts: [{
      ...pending.receipts[0],
      receiptId: '77000000-0000-0000-0000-000000000002',
      quantity: null,
      unit: null,
      arrivalDate: '2026-06-07',
      status: 'Confirmed',
      isLegacy: true,
      version: 1,
      confirmedAtUtc: '2026-06-07T12:30:00Z'
    }]
  };
  return {
    summary: {
      pendingArrivalCount: 0,
      waitingIqcCount: 0,
      failedBlockedCount: 0,
      readyToConfirmCount: 0,
      completedItemCount: includeCompleted ? 1 : 0,
      customerSuppliedItemCount: 0,
      customerSupplyOverdueCount: 0
    },
    items: includeCompleted ? [pending, completed] : [pending]
  };
}

function procurementHistory() {
  return {
    groups: [
      {
        groupId: 'proc-direct',
        inputSource: 'Direct',
        changedByUserId: '50000000-0000-0000-0000-000000000011',
        changedByName: 'Dev Procurement User',
        changedAtUtc: '2026-06-26T01:00:00Z',
        reason: '구매 직접 입력',
        importBatchId: null,
        importFileName: null,
        affectedItemCount: 1,
        changeCount: 1,
        changes: [
          {
            entityId: '76000000-0000-0000-0000-000000000001',
            sequenceNumber: 1,
            fieldName: 'OrderItem',
            oldValue: null,
            newValue: 'Relay'
          }
        ]
      }
    ],
    excelImportBatches: []
  };
}

function canReadDeletedProjects(userKey: string) {
  return userKey === 'dev-sales' || userKey === 'dev-admin';
}

function mockMobileViewport(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn()
    }))
  });
}

function readDevUser(init?: RequestInit) {
  const headers = init?.headers;
  if (headers instanceof Headers) {
    return headers.get('X-Dev-User') ?? 'dev-sales';
  }

  return 'dev-sales';
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function projectListResponse(items: unknown[]) {
  return json({
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length
  });
}

function abortableResponse(signal?: AbortSignal) {
  return new Promise<Response>((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('The operation was aborted.', 'AbortError'));
      return;
    }

    signal?.addEventListener('abort', () => {
      reject(new DOMException('The operation was aborted.', 'AbortError'));
    }, { once: true });

    setTimeout(() => {
      resolve(projectListResponse([projectListItem('dev-sales', 'Active', 'Should Not Render', projectId)]));
    }, 100);
  });
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((innerResolve, innerReject) => {
    resolve = innerResolve;
    reject = innerReject;
  });

  return { promise, resolve, reject };
}
