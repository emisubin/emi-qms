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

const salesOwnerId = '50000000-0000-0000-0000-000000000002';
const projectId = '71000000-0000-0000-0000-000000000010';
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

describe('App', () => {
  beforeEach(() => {
    adminUserDeletionScheduled = false;
    adminDepartmentDeletionScheduled = false;
    adminHolidayDeletionScheduled = false;
    teamsJsMock.context = null;
    teamsJsMock.initialize.mockClear();
    teamsJsMock.getContext.mockClear();
    teamsJsMock.initialize.mockResolvedValue(undefined);
    teamsJsMock.getContext.mockImplementation(async () => teamsJsMock.context ?? {});
    window.localStorage.clear();
    Object.defineProperty(document, 'referrer', { value: '', configurable: true });
    vi.stubGlobal('fetch', vi.fn(mockFetch));
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:panel-template');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
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

  it('renders the Teams Activity tab route with recent notifications and work summary', async () => {
    window.history.pushState(null, '', '/teams/activity');
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'EMI 프로젝트 통합관리시스템 알림' })).toBeInTheDocument();
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
    expect(await screen.findByRole('tab', { name: 'Workflow' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('현재 상세 화면에서는 설계, 생산관리, 구매 입력 화면을 제공합니다. 나머지 workflow 단계는 전용 입력 화면이 제공되기 전까지 이 요약에서 상태를 확인합니다.')).toBeInTheDocument();

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '알림' }));
    expect(await screen.findByText('자재 도착 단계 알림')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '이동' }));

    expect(await screen.findByRole('tab', { name: 'Workflow' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('heading', { name: '자재 입고 처리' })).not.toBeInTheDocument();
  });

  it('renders project list cards for mobile layout without raw enum values', async () => {
    mockMobileViewport(true);
    render(<App />);

    const mobileList = await screen.findByTestId('project-list-mobile');
    const firstCard = within(mobileList).getAllByTestId('project-list-card')[0];
    expect(firstCard).toHaveTextContent('TASK-003A Demo');
    expect(firstCard).toHaveTextContent('고객사EMI Test Customer');
    expect(firstCard).toHaveTextContent('CodePJT-003A');
    expect(firstCard).toHaveTextContent('ItemUL67');
    expect(firstCard).toHaveTextContent('면수4면');
    expect(firstCard).toHaveTextContent('납기일2026-10-10');
    expect(firstCard).toHaveTextContent('상태생산관리');
    expect(firstCard).toHaveTextContent('진행률6%');
    expect(firstCard).not.toHaveTextContent('BeforeManufacturing');
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
    fireEvent.click(screen.getByRole('button', { name: '실패 알림 보기' }));
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
    expect(screen.getByDisplayValue('Sales')).toBeInTheDocument();
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

  it('validates required fields on the create form', async () => {
    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: '신규 프로젝트' }));
    expect(await screen.findByLabelText('FAT 필요 여부')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: '등록' }));

    expect((await screen.findAllByText('필수 입력값입니다.')).length).toBeGreaterThanOrEqual(5);
    expect(screen.getByText('포장방식은 필수 선택값입니다.')).toBeInTheDocument();
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
    fireEvent.click(await screen.findByRole('button', { name: '목록' }));
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
    expect(screen.getByText('WidthMm: 700 → 800.1')).toBeInTheDocument();
  });

  it('keeps procurement read-only on project detail and exposes edit only to Procurement', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    expect(await screen.findByRole('tab', { name: '설계' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByText('구매정보')).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole('tab', { name: '구매' }));

    const procurementSection = (await screen.findByText('구매정보')).closest('section');
    expect(procurementSection).not.toBeNull();
    expect(within(procurementSection as HTMLElement).getByText('Relay')).toBeInTheDocument();
    expect(within(procurementSection as HTMLElement).getAllByText('Vendor A').length).toBeGreaterThan(0);
    expect(within(procurementSection as HTMLElement).getByText('완료(6/7 12:30)')).toBeInTheDocument();
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

    const procurementMobile = await screen.findByTestId('procurement-mobile');
    expect(procurementMobile).toHaveTextContent('Relay');
    expect(procurementMobile).toHaveTextContent('업체Vendor A');
    expect(procurementMobile).toHaveTextContent('기술 담당자Owner A');
    expect(procurementMobile).toHaveTextContent('입고예정일2026-06-29');
    expect(procurementMobile).not.toHaveTextContent('예정일까지');
    expect(procurementMobile).not.toHaveTextContent('D-3');
    expect(procurementMobile).not.toHaveTextContent('입고지연');
    expect(procurementMobile).not.toHaveTextContent('부분입고');
  });

  it('keeps procurement Excel controls on the edit page and saves partial rows', async () => {
    const savedRequests: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === `/api/projects/${projectId}/procurement` && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json(procurementResponse()));
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
    expect(editTable).not.toHaveTextContent('PJT Code');
    expect(editTable).not.toHaveTextContent('예정일까지');
    expect(screen.getByRole('button', { name: 'Excel 양식 다운로드' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Excel 업로드' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '행 추가' }));
    const inputs = within(editTable).getAllByRole('textbox');
    fireEvent.change(inputs[0], { target: { value: '8W' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));

    await screen.findByRole('heading', { name: 'TASK-003A Demo' });
    expect(screen.getByRole('tab', { name: '구매' })).toHaveAttribute('aria-selected', 'true');
    expect(JSON.stringify(savedRequests[0])).toContain('8W');
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
    await act(async () => {
      editLoadResolvers[0](json({ ...procurementResponse(), items: [] }));
      await Promise.resolve();
    });
    expect(screen.queryByRole('table', { name: '구매정보 수정' })).not.toBeInTheDocument();

    await act(async () => {
      editLoadResolvers[1](json(procurementResponse()));
      await Promise.resolve();
    });
    const editTable = await screen.findByRole('table', { name: '구매정보 수정' });
    const initialRowCount = editTable.querySelectorAll('.procurement-table-row.editable').length;
    fireEvent.click(screen.getByRole('button', { name: '행 추가' }));
    expect(editTable.querySelectorAll('.procurement-table-row.editable')).toHaveLength(initialRowCount + 1);
  });

  it('shows project context on product detail and simplifies procurement Excel preview sections', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-procurement' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
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
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산관리' }));

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
    expect(expanded).not.toHaveTextContent('알림 기준');
    expect(expanded).not.toHaveTextContent('fallback');
    expect(within(expanded).queryByRole('table', { name: '생산계획 캘린더 표' })).not.toBeInTheDocument();
    expect(expanded).not.toHaveTextContent('검수 공휴일');

    fireEvent.click(within(commonNavigation).getByRole('button', { name: '프로젝트' }));
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    const detailTabs = await screen.findAllByRole('tab');
    expect(detailTabs.map((tab) => tab.textContent)).toEqual(expect.arrayContaining(['설계', '생산관리', '구매']));
    fireEvent.click(screen.getByRole('tab', { name: '생산관리' }));
    expect(await screen.findByText('프로젝트 단위 계획과 담당자 지정')).toBeInTheDocument();
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
    const planItemsTable = await screen.findByRole('table', { name: '생산계획 항목' });
    expect(planItemsTable).toHaveTextContent('계획 항목필수예정일비고');
    expect(planItemsTable).not.toHaveTextContent('No');
    const calendarTable = await screen.findByRole('table', { name: '생산계획 캘린더 표' });
    expect(calendarTable).toHaveTextContent('생산단계');
    expect(calendarTable).toHaveAttribute('style', expect.stringContaining('--production-calendar-stage-column-width'));
    expect(within(calendarTable).getByRole('columnheader', { name: '생산단계' })).toHaveClass('production-calendar-stage-cell');
    expect(within(calendarTable).getByRole('rowheader', { name: /자재 입고/ })).toHaveClass('production-calendar-stage-cell');
    expect(within(calendarTable).getByRole('columnheader', { name: /7\/1/ })).toHaveClass('production-calendar-date-cell');
    expect(calendarTable).toHaveTextContent('7/1');
    expect(calendarTable).toHaveTextContent('7/2');
    expect(within(calendarTable).getByRole('columnheader', { name: /7\/2/ })).toHaveClass('calendar-company-holiday');
    expect(within(calendarTable).getByRole('columnheader', { name: /7\/3/ })).toHaveClass('calendar-red-day');
    expect(within(calendarTable).getByRole('row', { name: /자재 입고/ })).toHaveTextContent('✓');
    expect(await screen.findByText(/회사 창립기념 휴일/)).toBeInTheDocument();
    expect(await screen.findByText(/공식 대체공휴일/)).toBeInTheDocument();
    expect(screen.getByLabelText('날짜 미입력 생산단계')).toHaveTextContent('조립 시작');
    expect(screen.getByLabelText('날짜 미입력 생산단계')).toHaveTextContent('필수 미입력');
    expect(screen.queryByText('검수 공휴일')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '생산계획 수정' }));
    const projectContext = await screen.findByTestId('project-context-summary');
    expect(projectContext).toHaveTextContent('TASK-003A Demo');
    expect(projectContext).toHaveTextContent('PJT-003A');
    expect(projectContext).not.toHaveTextContent('Active');
    const planEditTable = await screen.findByRole('table', { name: '생산계획 수정' });
    expect(planEditTable).toHaveTextContent('계획 항목필수예정일비고작업');
    expect(planEditTable).not.toHaveTextContent('No');
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
    expect(await screen.findByText('프로젝트 단위 계획과 담당자 지정')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    fireEvent.click(await screen.findByText('TASK-003A Demo'));
    fireEvent.click(await screen.findByRole('tab', { name: '생산관리' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: '생산계획 수정' })).not.toBeInTheDocument());
  });

  it('hides production planning Excel upload controls from users without Production Planning update permission', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-admin' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '생산관리' }));

    expect(await screen.findByLabelText('생산계획 요약')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 업로드' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Excel 양식 다운로드' })).not.toBeInTheDocument();
  });

  it('allows Materials to use only the receipt completion page', async () => {
    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '자재' }));

    expect(await screen.findByRole('table', { name: 'TASK-003A Demo 자재 입고 처리' })).toBeInTheDocument();
    expect(screen.queryByText('통상납기')).not.toBeInTheDocument();
    const receiptTable = screen.getByRole('table', { name: 'TASK-003A Demo 자재 입고 처리' });
    const checkbox = within(receiptTable).getByRole('checkbox');
    fireEvent.click(checkbox);
    fireEvent.change(screen.getByLabelText('수정사유'), { target: { value: '입고 확인' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    expect(await screen.findByRole('heading', { name: '프로젝트 목록' })).toBeInTheDocument();
  });

  it('filters completed material receipt rows by default and shows them with the include toggle', async () => {
    const savedRequests: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/materials/receipts' && init?.method === 'PATCH') {
        savedRequests.push(JSON.parse(String(init.body)));
        return Promise.resolve(json({ items: materialReceiptItems(url.searchParams.get('includeCompleted') === 'true') }));
      }

      return mockFetch(input, init);
    }));

    render(<App />);

    fireEvent.change(await screen.findByLabelText('개발 사용자'), { target: { value: 'dev-materials' } });
    const commonNavigation = (await screen.findAllByRole('navigation', { name: '공통 메뉴' }))[0];
    fireEvent.click(within(commonNavigation).getByRole('button', { name: '자재' }));

    expect(await screen.findByText('현재 구매품목 입고 처리 대상만 표시됩니다. 완료된 항목은 저장 후 기본 목록에서 사라집니다.')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'TASK-003A Demo 자재 입고 처리' })).toHaveTextContent('Relay');
    expect(screen.queryByText('Completed Relay')).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('완료 항목 포함'));
    expect(await screen.findByText('Completed Relay')).toBeInTheDocument();
    const receiptTable = screen.getByRole('table', { name: 'TASK-003A Demo 자재 입고 처리' });
    expect(within(receiptTable).getByText('완료(6/7 12:30)')).toBeInTheDocument();
    expect(within(receiptTable).getAllByRole('checkbox')).toHaveLength(2);
    const completedCheckbox = within(receiptTable).getAllByRole('checkbox')[1];
    expect(completedCheckbox).toBeEnabled();
    fireEvent.click(completedCheckbox);
    const noteInputs = within(receiptTable).getAllByRole('textbox');
    fireEvent.change(noteInputs[noteInputs.length - 1], { target: { value: '완료 비고 수정' } });
    fireEvent.click(screen.getByRole('button', { name: '저장' }));
    await screen.findByRole('heading', { name: '프로젝트 목록' });
    expect(JSON.stringify(savedRequests[0])).toContain('완료 비고 수정');
    expect(JSON.stringify(savedRequests[0])).toContain('"receiptCompleted":false');
    expect(screen.queryByText('One or more validation errors occurred')).not.toBeInTheDocument();
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

  if (path.startsWith('/api/admin/users')) {
    const updated = init?.method === 'PATCH';
    if (path.endsWith('/schedule-deletion')) {
      adminUserDeletionScheduled = true;
    }
    const scheduledDeletion = adminUserDeletionScheduled;
    return json({
      users: [
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
          departmentName: 'Sales',
          roles: ['sales'],
          isReadOnly: false,
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
          departmentName: 'Administration',
          roles: ['system-administrator'],
          isReadOnly: true,
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
          departmentName: 'Sales',
          roles: ['sales'],
          isReadOnly: false,
          deletionRequestedAtUtc: null,
          scheduledHardDeleteAtUtc: null,
          purgeBlockedAtUtc: null,
          purgeBlockedReason: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        }
      ],
      departments: [
        { departmentId: '10000000-0000-0000-0000-000000000001', code: 'administration', name: 'Administration' },
        { departmentId: '10000000-0000-0000-0000-000000000002', code: 'sales', name: 'Sales' }
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
      sentDeliveryCount: 8,
	      lastDailyDigestSentAtUtc: '2026-07-07T07:30:00Z',
	      activeEscalationCount: 4,
	      recentMasterChangeCount: 5,
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
      name: 'Sales',
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
    return json({
      deliveryId: path.split('/').at(-1),
      categoryLabel: '관리자 수동 발송',
      notificationKindLabel: '프로젝트 생성 알림',
      projectName: 'TASK-003A Demo',
      title: '[테스트] 프로젝트 생성 알림',
      message: '실제 업무 알림이 아닙니다.',
      manualRequestedAtUtc: '2026-07-07T00:00:00Z',
      createdAtUtc: '2026-07-07T00:00:00Z',
      channel: 'Mail',
      channelLabel: '메일',
      recipient: 'Dev Sales User',
      status: 'Sent',
      statusLabel: '발송 완료',
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
	    const status = url.searchParams.get('status') ?? 'Sent';
	    const handlingStatus = url.searchParams.get('handlingStatus') ?? 'Open';
	    return json({
	      items: [
	        {
	          deliveryId: '79000000-0000-0000-0000-000000000101',
	          notificationId: status === 'Pending' ? null : '79000000-0000-0000-0000-000000000301',
	          recipientUserId: salesOwnerId,
	          projectId,
	          workItemId: '76000000-0000-0000-0000-000000000001',
	          channel: 'Mail',
	          channelLabel: '메일',
	          deliveryType: status === 'Pending' ? 'OverdueL1' : 'DailyDigest',
	          deliveryTypeLabel: status === 'Pending' ? '예정일 초과 L1' : '일일 요약',
	          status,
	          statusLabel: status === 'Pending' ? '발송 대기' : status === 'Processing' ? '발송 처리 중' : status === 'Failed' ? '발송 실패' : '발송 완료',
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
	          displayTitle: status === 'Pending' ? '예정일 초과 알림' : status === 'Failed' ? '발송 실패 테스트' : 'Daily Digest',
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

  if (path === `/api/projects/${projectId}/production-planning`) {
    return json(productionPlanningResponse());
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
    return json({ items: materialReceiptItems(false) }, userKey === 'dev-procurement' || userKey === 'dev-materials' ? 200 : 403);
  }

  if (path === '/api/materials/receipts') {
    return json({ items: materialReceiptItems(url.searchParams.get('includeCompleted') === 'true') }, userKey === 'dev-procurement' || userKey === 'dev-materials' ? 200 : 403);
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

  return {
    developmentUserKey: userKey,
    displayName: userKey,
    department: 'test',
    roles: [userKey.replace('dev-', '')],
    permissions,
    projectAccess: []
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
    qrEligibleCount: 0,
    manufacturingCompletedCount: 0,
    inspectionCompletedCount: 0,
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
    projectPanelInformationCompleted: false,
    panelInformationStatusMessage: null,
    panels: panelIds.map((panelId, index) => ({
      panelId,
      projectId: id,
      sequenceNumber: index + 1,
      panelNumber: `No.${index + 1}`,
      displayCode: `P0${index + 1}`,
      panelName: null,
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
        receiptCompleted: false,
        receiptCompletedAtUtc: null,
        receiptCompletedByUserId: null,
        receiptCompletedByUserName: null,
        receiptCompletionNote: null,
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
        statusLabel: '내 업무 생성됨',
        workItemCount: 1,
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
    planId: '77000000-0000-0000-0000-000000000301',
    rowVersion: 1,
    planStatus: status,
    planStatusLabel: status === 'NotPlanned' ? '미등록' : planned ? '계획 완료' : '작성 중',
    productTypeId: productType.productTypeId,
    templateId: productType.activeTemplateId,
    productTypeCode: productType.code,
    productTypeName: productType.name,
    notes: '생산계획 검수',
    items: productType.steps.map((step, index) => ({
      itemId: `77000000-0000-0000-0000-00000000040${index + 1}`,
      templateStepId: step.templateStepId,
      sequenceNumber: step.sequenceNumber,
      stepName: step.stepName,
      isRequired: step.isRequired,
      plannedDate: planned || index === 0 || index === 2 ? `2026-07-0${index + 1}` : null,
      note: index === 0 ? '입고 확인' : null,
      rowVersion: 0
    })),
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

function materialReceiptItems(includeCompleted: boolean) {
  const pending = procurementResponse().items[0];
  const completed = {
    ...pending,
    itemId: '76000000-0000-0000-0000-000000000002',
    orderItem: 'Completed Relay',
    receiptCompleted: true,
    receiptCompletedAtUtc: '2026-06-07T12:30:00',
    rowVersion: 2
  };
  return includeCompleted ? [pending, completed] : [pending];
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
