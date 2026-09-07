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
  getBusinessUnitRequestState,
  resetBusinessUnitRequestContext,
  selectBusinessUnit,
  setRuntimeMutationAllowed
} from '../src/api';
import type { BusinessUnitAccess, CurrentUser } from '../src/identity';

const adminUserId = '50000000-0000-0000-0000-000000000001';
const defaultRuntimeMode = {
  mode: 'Development',
  reviewSafe: false,
  mutationAllowed: true,
  databaseReadOnly: false,
  ready: true,
  reason: 'development'
};

function membershipAdministrationResponse(newUserMemberships: Array<'CHEONGJU' | 'OSAN'> = ['CHEONGJU']) {
  return {
    users: [
      {
        userId: adminUserId,
        authProvider: 'Dev',
        displayName: 'Synthetic Overall Admin',
        email: null,
        memberships: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: true
      },
      {
        userId: '50000000-0000-0000-0000-000000000002',
        authProvider: 'EntraId',
        displayName: 'Synthetic New User',
        email: 'new-user@example.invalid',
        memberships: newUserMemberships,
        isOverallAdministrator: false
      }
    ],
    availableBusinessUnits: ['CHEONGJU', 'OSAN']
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function selectedUser(access: BusinessUnitAccess): CurrentUser {
  const principal = {
    userId: adminUserId,
    developmentUserKey: 'dev-admin',
    displayName: access.selectedBusinessUnit === 'OSAN' ? 'Osan Admin' : 'Cheongju Admin',
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
    permissions: ['users.manage'],
    projectAccess: [],
    isTestUserSwitch: false,
    testUserKey: null,
    canUseAdminTestUserSwitch: false,
    actualUser: principal,
    effectiveUser: principal,
    businessUnitAccess: access
  };
}

function shellFetch(
  me: unknown,
  calls: Array<{ path: string; headers: Headers }> = [],
  runtimeMode: unknown | Response | Promise<Response> = defaultRuntimeMode
) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    calls.push({ path: url.pathname, headers: new Headers(init?.headers) });
    if (url.pathname === '/health/ready') {
      return json({ status: 'ready', database: { reason: 'ready' } });
    }
    if (url.pathname === '/api/runtime-mode') {
      if (runtimeMode instanceof Response || runtimeMode instanceof Promise) {
        return runtimeMode;
      }
      return json(runtimeMode);
    }
    if (url.pathname === '/api/me') {
      return json(typeof me === 'function' ? (me as (headers: Headers) => unknown)(new Headers(init?.headers)) : me);
    }
    if (url.pathname === '/api/admin/users') {
      return json({
        users: [{
          userId: '50000000-0000-0000-0000-000000000002',
          developmentUserKey: 'entra:synthetic-user',
          displayName: 'Synthetic Local User',
          email: 'local-user@example.invalid',
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
          preDeleteIsActive: null,
          lifecycleStatus: 'Active',
          lifecycleStatusLabel: '활성',
          scheduledHardDeleteLabel: null
        }],
        departments: [{
          departmentId: '10000000-0000-0000-0000-000000000001',
          code: 'management-support',
          name: '경영지원',
          defaultRoleCode: 'management-support'
        }],
        roles: [{
          roleId: '20000000-0000-0000-0000-000000000001',
          code: 'management-support',
          name: '경영지원'
        }]
      });
    }
    if (url.pathname === '/api/admin/business-unit-access/users') {
      return json(membershipAdministrationResponse());
    }
    return json({ title: 'unexpected test request' }, 404);
  });
}

describe('business-unit access shell', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    window.history.replaceState(null, '', '/');
    resetBusinessUnitRequestContext(true);
    setRuntimeMutationAllowed(false);
  });

  afterEach(() => {
    resetBusinessUnitRequestContext(true);
    setRuntimeMutationAllowed(false);
    vi.unstubAllGlobals();
  });

  it.each([
    ['no_membership', '사용할 수 있는 사업부가 없습니다.'],
    ['local_profile_pending', '청주 사용자 등록이 필요합니다.']
  ] as const)('renders the %s access state without loading business data', async (status, title) => {
    vi.stubGlobal('fetch', shellFetch({
      userId: adminUserId,
      developmentUserKey: 'dev-admin',
      displayName: 'Pending User',
      email: null,
      businessUnitAccess: {
        status,
        selectedBusinessUnit: status === 'local_profile_pending' ? 'CHEONGJU' : null,
        allowedBusinessUnits: status === 'local_profile_pending' ? ['CHEONGJU'] : [],
        isOverallAdministrator: false,
        errorCode: status
      }
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: '공통 메뉴' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
  });

  it('settles a normal no-membership gate without requesting denied runtime or business data', async () => {
    const calls: Array<{ path: string; headers: Headers }> = [];
    const generationBefore = getBusinessUnitRequestState().generation;
    vi.stubGlobal('fetch', shellFetch({
      userId: '50000000-0000-0000-0000-000000000003',
      developmentUserKey: 'dev-pending',
      displayName: 'Pending Directory User',
      email: null,
      businessUnitAccess: {
        status: 'no_membership',
        selectedBusinessUnit: null,
        allowedBusinessUnits: [],
        isOverallAdministrator: false,
        errorCode: 'directory_membership_required'
      }
    }, calls, json({
      errorCode: 'directory_membership_required',
      message: 'synthetic runtime membership denial'
    }, 403)));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사용할 수 있는 사업부가 없습니다.' })).toBeInTheDocument();
    await waitFor(() => expect(calls.filter((call) => call.path === '/api/me')).toHaveLength(1));
    expect(calls.filter((call) => call.path === '/api/runtime-mode')).toHaveLength(0);
    expect(calls.filter((call) => call.path.startsWith('/api/') && call.path !== '/api/me')).toHaveLength(0);
    expect(getBusinessUnitRequestState().generation).toBe(generationBefore);
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
  });

  it('lets only an overall administrator resolve a selection-required state', async () => {
    const calls: Array<{ path: string; headers: Headers }> = [];
    const selectionRequired = {
      userId: adminUserId,
      developmentUserKey: 'dev-admin',
      displayName: 'Overall Admin',
      email: null,
      businessUnitAccess: {
        status: 'selection_required',
        selectedBusinessUnit: null,
        allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: true,
        errorCode: 'business_unit_selection_required'
      }
    };
    vi.stubGlobal('fetch', shellFetch((headers: Headers) => headers.get('X-Qms-Business-Unit') === 'OSAN'
      ? selectedUser({
          status: 'selected',
          selectedBusinessUnit: 'OSAN',
          allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
          isOverallAdministrator: true,
          errorCode: null
        })
      : selectionRequired, calls));

    render(<App />);
    fireEvent.click(await screen.findByRole('button', { name: '오산 사업부로 이동' }));

    expect(await screen.findByRole('heading', { name: '오산 사업부 홈' })).toBeInTheDocument();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    expect(calls.some((call) => call.path === '/api/me'
      && call.headers.get('X-Qms-Business-Unit') === 'OSAN')).toBe(true);
  });

  it.each([
    ['CHEONGJU', '청주 사용자 등록이 필요합니다.'],
    ['OSAN', '오산 사용자 등록이 필요합니다.']
  ] as const)('does not show a single-option business-unit choice for a %s-only overall administrator at the access gate', async (
    businessUnit,
    title
  ) => {
    vi.stubGlobal('fetch', shellFetch({
      userId: adminUserId,
      developmentUserKey: 'dev-admin',
      displayName: 'Single Membership Overall Admin',
      email: null,
      businessUnitAccess: {
        status: 'local_profile_pending',
        selectedBusinessUnit: businessUnit,
        allowedBusinessUnits: [businessUnit],
        isOverallAdministrator: true,
        errorCode: 'business_unit_local_profile_pending'
      }
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
  });

  it.each([
    ['CHEONGJU', '사용자 관리'],
    ['OSAN', '현재 사업부 사용자 관리']
  ] as const)('does not render a header selector for a %s-only overall administrator', async (
    businessUnit,
    title
  ) => {
    selectBusinessUnit(businessUnit);
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: businessUnit,
      allowedBusinessUnits: [businessUnit],
      isOverallAdministrator: true,
      errorCode: null
    })));

    render(<App />);

    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
  });

  it('binds an implicit single membership before loading selected-business data', async () => {
    const calls: Array<{ path: string; headers: Headers }> = [];
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['OSAN'],
      isOverallAdministrator: false,
      errorCode: null
    }), calls));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '현재 사업부 사용자 관리' })).toBeInTheDocument();
    expect(await screen.findByText('Synthetic Local User')).toBeInTheDocument();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    const meCalls = calls.filter((call) => call.path === '/api/me');
    expect(meCalls.length).toBeGreaterThanOrEqual(2);
    expect(meCalls[0].headers.get('X-Qms-Business-Unit')).toBeNull();
    expect(meCalls.at(-1)?.headers.get('X-Qms-Business-Unit')).toBe('OSAN');
    expect(calls.filter((call) => call.path === '/api/admin/users')).toEqual([
      expect.objectContaining({
        headers: expect.objectContaining({})
      })
    ]);
    expect(calls.find((call) => call.path === '/api/admin/users')?.headers.get('X-Qms-Business-Unit')).toBe('OSAN');
  });

  it('does not offer alternate selection to a normal user', async () => {
    vi.stubGlobal('fetch', shellFetch({
      userId: '50000000-0000-0000-0000-000000000002',
      developmentUserKey: 'dev-sales',
      displayName: 'Normal User',
      email: null,
      businessUnitAccess: {
        status: 'selection_required',
        selectedBusinessUnit: null,
        allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: false,
        errorCode: 'business_unit_selection_required'
      }
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
  });

  it('keeps the Osan shell restricted and redirects a closed direct route', async () => {
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/pending');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    })));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '오산 사업부 홈' })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/'));
    const navigation = screen.getAllByRole('navigation', { name: '공통 메뉴' })[0];
    expect(within(navigation).getByRole('button', { name: '프로젝트' })).toBeInTheDocument();
    expect(within(navigation).getByRole('button', { name: '진행 관리' })).toBeInTheDocument();
    expect(within(navigation).queryByRole('button', { name: 'Pending' })).not.toBeInTheDocument();
    expect(within(navigation).queryByRole('button', { name: 'G2' })).not.toBeInTheDocument();
  });

  it('separates overall membership management from selected-unit user roles', async () => {
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/admin/business-unit-access');
    const fallbackFetch = shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }));
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/memberships') && init?.method === 'PUT') {
        return json({
          changed: true,
          snapshot: membershipAdministrationResponse(['CHEONGJU', 'OSAN'])
        });
      }
      return fallbackFetch(input, init);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사업부 소속 관리' })).toBeInTheDocument();
    expect(screen.getByText(/총괄 관리자 지정은 여기서 변경할 수 없습니다/)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '현재 사업부 사용자 관리' }).length).toBeGreaterThan(0);
    const adminCard = (await screen.findByText('총괄 관리자')).closest('article');
    expect(adminCard).not.toBeNull();
    expect(within(adminCard!).getByText('총괄 관리자')).toBeInTheDocument();
    expect(within(adminCard!).getByLabelText('청주')).toBeChecked();
    expect(within(adminCard!).getByLabelText('오산')).toBeChecked();

    const newUserCard = (await screen.findByText('Synthetic New User')).closest('article');
    expect(newUserCard).not.toBeNull();
    const osanMembership = within(newUserCard!).getByLabelText('오산');
    await waitFor(() => expect(osanMembership).toBeEnabled());
    fireEvent.click(osanMembership);
    const saveMembership = within(newUserCard!).getByRole('button', { name: '소속 저장' });
    expect(saveMembership).toBeEnabled();
    fireEvent.click(saveMembership);
    expect(await screen.findByRole('status')).toHaveTextContent('사업부 소속을 저장했습니다.');
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/memberships') && init?.method === 'PUT'
    ))).toHaveLength(1);
  });

  it('blocks gate membership mutations in ReviewSafe even when disabled controls are invoked', async () => {
    const access: BusinessUnitAccess = {
      status: 'selection_required',
      selectedBusinessUnit: null,
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: 'business_unit_selection_required'
    };
    const fetchMock = shellFetch(selectedUser(access), [], {
      ...defaultRuntimeMode,
      mode: 'ReviewSafe',
      reviewSafe: true,
      mutationAllowed: false,
      databaseReadOnly: true,
      reason: 'review-safe'
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const newUserCard = (await screen.findByText('Synthetic New User')).closest('article');
    expect(newUserCard).not.toBeNull();
    expect(screen.getByText('검수 전용 읽기 모드에서는 사업부 소속을 변경할 수 없습니다.')).toBeInTheDocument();
    const checkbox = within(newUserCard!).getByLabelText('오산');
    const save = within(newUserCard!).getByRole('button', { name: '소속 저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();

    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);

    expect(checkbox).not.toBeChecked();
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/memberships') && init?.method === 'PUT'
    ))).toHaveLength(0);
  });

  it('blocks local-profile-pending gate membership mutations while runtime status is loading', async () => {
    selectBusinessUnit('CHEONGJU');
    const runtimePending = new Promise<Response>(() => undefined);
    const access: BusinessUnitAccess = {
      status: 'local_profile_pending',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU'],
      isOverallAdministrator: true,
      errorCode: 'business_unit_local_profile_pending'
    };
    const fetchMock = shellFetch(selectedUser(access), [], runtimePending);
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const newUserCard = (await screen.findByText('Synthetic New User')).closest('article');
    expect(newUserCard).not.toBeNull();
    expect(screen.getByText('실행 모드를 확인하는 동안에는 사업부 소속을 변경할 수 없습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
    const checkbox = within(newUserCard!).getByLabelText('오산');
    const save = within(newUserCard!).getByRole('button', { name: '소속 저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();
    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/memberships') && init?.method === 'PUT'
    ))).toHaveLength(0);
  });

  it('blocks no-membership gate mutations when runtime status is unavailable', async () => {
    const access: BusinessUnitAccess = {
      status: 'no_membership',
      selectedBusinessUnit: null,
      allowedBusinessUnits: [],
      isOverallAdministrator: true,
      errorCode: 'directory_membership_required'
    };
    const fetchMock = shellFetch(selectedUser(access), [], json({ reason: 'synthetic unavailable' }, 503));
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const newUserCard = (await screen.findByText('Synthetic New User')).closest('article');
    expect(newUserCard).not.toBeNull();
    expect(await screen.findByText('실행 모드를 확인할 수 없어 사업부 소속 변경을 차단했습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
    const checkbox = within(newUserCard!).getByLabelText('오산');
    const save = within(newUserCard!).getByRole('button', { name: '소속 저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();
    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/memberships') && init?.method === 'PUT'
    ))).toHaveLength(0);
  });

  it('limits Osan user administration to local profile fields', async () => {
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    })));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '현재 사업부 사용자 관리' })).toBeInTheDocument();
    expect(await screen.findByText('Synthetic Local User')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '수정' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '알림 설정' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '선택 삭제' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '선택 복구' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '삭제' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사용자 전체 선택')).not.toBeInTheDocument();
  });

  it('retains the full user administration controls in Cheongju', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    })));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사용자 관리' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: '알림 설정' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '선택 삭제' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '선택 복구' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '삭제' })).toBeInTheDocument();
    expect(screen.getByLabelText('사용자 전체 선택')).toBeInTheDocument();
  });

  it('unmounts selected-business data when a page reports membership revocation', async () => {
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/admin/users');
    const selected = selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    });
    const revoked = {
      userId: adminUserId,
      developmentUserKey: 'dev-admin',
      displayName: 'Revoked User',
      email: null,
      businessUnitAccess: {
        status: 'no_membership',
        selectedBusinessUnit: null,
        allowedBusinessUnits: [],
        isOverallAdministrator: false,
        errorCode: 'directory_membership_required'
      }
    };
    const calls: Array<{ path: string; headers: Headers }> = [];
    const fallbackFetch = shellFetch((headers: Headers) => (
      headers.get('X-Qms-Business-Unit') === 'OSAN' ? selected : revoked
    ), calls);
    let selectedBusinessRequestCount = 0;
    const generationBeforeRevocation = getBusinessUnitRequestState().generation;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/admin/users') {
        selectedBusinessRequestCount += 1;
        return json({
          errorCode: 'directory_membership_required',
          message: 'synthetic membership revoked'
        }, 403);
      }
      return fallbackFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사용할 수 있는 사업부가 없습니다.' })).toBeInTheDocument();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(screen.queryByText('Synthetic Local User')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '현재 사업부 사용자 관리' })).not.toBeInTheDocument();
    await waitFor(() => expect(calls.filter((call) => call.path === '/api/me')).toHaveLength(2));
    expect(calls.filter((call) => call.path === '/api/runtime-mode')).toHaveLength(1);
    expect(selectedBusinessRequestCount).toBe(1);
    expect(getBusinessUnitRequestState().generation).toBe(generationBeforeRevocation + 1);
  });
});
