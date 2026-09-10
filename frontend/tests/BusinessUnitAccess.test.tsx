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

function membershipAdministrationResponse(
  newUserMemberships: Array<'CHEONGJU' | 'OSAN'> = ['CHEONGJU'],
  newUserIsOverallAdministrator = false
) {
  const departmentId = '10000000-0000-0000-0000-000000000001';
  const unitProfile = (businessUnitCode: 'CHEONGJU' | 'OSAN', active: boolean, canManage = true) => ({
    businessUnitCode,
    membershipActive: active,
    localProfileExists: active,
    isActive: active,
    departmentId: active ? departmentId : null,
    departmentCode: active ? 'management-support' : null,
    departmentName: active ? '경영지원' : null,
    roles: active ? ['management-support'] : [],
    explicitRoles: [] as string[],
    isDepartmentHead: false,
    canManage,
    localProfileReady: active
  });
  return {
    users: [
      {
        userId: adminUserId,
        authProvider: 'Dev',
        displayName: 'Synthetic Overall Admin',
        accountId: 'dev-admin',
        email: null,
        memberships: ['CHEONGJU', 'OSAN'],
        isOverallAdministrator: true,
        accessVersion: 1,
        approvalPending: false,
        pendingOperationId: null,
        pendingOperationStatus: null,
        pendingOperationStale: false,
        pendingFailureCode: null,
        pendingIsOverallAdministrator: null,
        pendingProfiles: [],
        profiles: [unitProfile('CHEONGJU', true), unitProfile('OSAN', true)]
      },
      {
        userId: '50000000-0000-0000-0000-000000000002',
        authProvider: 'EntraId',
        displayName: 'Synthetic New User',
        accountId: 'new-user@example.invalid',
        email: 'new-user@example.invalid',
        memberships: newUserMemberships,
        isOverallAdministrator: newUserIsOverallAdministrator,
        accessVersion: 0,
        approvalPending: newUserMemberships.length === 0,
        pendingOperationId: null,
        pendingOperationStatus: null,
        pendingOperationStale: false,
        pendingFailureCode: null,
        pendingIsOverallAdministrator: null,
        pendingProfiles: [],
        profiles: [
          unitProfile('CHEONGJU', newUserMemberships.includes('CHEONGJU')),
          unitProfile('OSAN', newUserMemberships.includes('OSAN'))
        ]
      }
    ],
    availableBusinessUnits: ['CHEONGJU', 'OSAN'],
    businessUnits: ['CHEONGJU', 'OSAN'].map((code) => ({
      code,
      canManage: true,
      departments: [
        {
          departmentId: '10000000-0000-0000-0000-000000000000',
          code: 'administration',
          name: '관리',
          defaultRoleCode: 'system-administrator'
        },
        { departmentId, code: 'management-support', name: '경영지원', defaultRoleCode: 'management-support' },
        {
          departmentId: '10000000-0000-0000-0000-000000000005',
          code: 'quality',
          name: '품질',
          defaultRoleCode: 'quality'
        }
      ],
      roles: [
        { roleId: '20000000-0000-0000-0000-000000000001', code: 'management-support', name: '경영지원' },
        { roleId: '20000000-0000-0000-0000-000000000005', code: 'quality', name: '품질' },
        { roleId: '20000000-0000-0000-0000-000000000009', code: 'system-administrator', name: '시스템 관리자' }
      ]
    }))
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
    if (url.pathname === '/api/osan/dashboard') {
      return json({ summary: { totalCount: 0, notStartedCount: 0, inProgressCount: 0, completedCount: 0 }, items: [], totalCount: 0, page: 1, pageSize: 11 });
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
    if (url.pathname === '/api/admin/user-access/users') {
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
    ['no_membership', '사용자 승인이 필요합니다.'],
    ['local_profile_pending', '사용자 승인이 필요합니다.']
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
    expect(screen.getByLabelText('개발 사용자')).toBeInTheDocument();
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

    expect(await screen.findByRole('heading', { name: '사용자 승인이 필요합니다.' })).toBeInTheDocument();
    await waitFor(() => expect(calls.filter((call) => call.path === '/api/me')).toHaveLength(1));
    expect(calls.filter((call) => call.path === '/api/runtime-mode')).toHaveLength(0);
    expect(calls.filter((call) => call.path.startsWith('/api/') && call.path !== '/api/me')).toHaveLength(0);
    expect(getBusinessUnitRequestState().generation).toBe(generationBefore);
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
  });

  it('automatically enters the deterministic Cheongju fallback for an overall administrator', async () => {
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
    vi.stubGlobal('fetch', shellFetch((headers: Headers) => headers.get('X-Qms-Business-Unit')
      ? selectedUser({
          status: 'selected',
          selectedBusinessUnit: headers.get('X-Qms-Business-Unit') as 'CHEONGJU' | 'OSAN',
          allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
          isOverallAdministrator: true,
          errorCode: null
        })
      : selectionRequired, calls));

    render(<App />);

    await waitFor(() => expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('CHEONGJU'));
    expect(screen.queryByRole('heading', { name: '이 탭에서 사용할 사업부를 선택해 주세요.' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect((await screen.findAllByLabelText('사업부 선택'))[0]).toHaveValue('CHEONGJU');
    expect(calls.some((call) => call.path === '/api/me'
      && call.headers.get('X-Qms-Business-Unit') === 'CHEONGJU')).toBe(true);
  });

  it.each([
    ['CHEONGJU', '사용자 승인이 필요합니다.'],
    ['OSAN', '사용자 승인이 필요합니다.']
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
    ['OSAN', '오산 홈']
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
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['OSAN'],
      isOverallAdministrator: false,
      errorCode: null
    }), calls));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '오산 홈' })).toBeInTheDocument();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    const meCalls = calls.filter((call) => call.path === '/api/me');
    expect(meCalls.length).toBeGreaterThanOrEqual(2);
    expect(meCalls[0].headers.get('X-Qms-Business-Unit')).toBeNull();
    expect(meCalls.at(-1)?.headers.get('X-Qms-Business-Unit')).toBe('OSAN');
    expect(calls.filter((call) => call.path === '/api/admin/users')).toHaveLength(0);
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

    expect(await screen.findByRole('heading', { name: '사용자 승인이 필요합니다.' })).toBeInTheDocument();
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

    expect(await screen.findByRole('heading', { name: '오산 홈' })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/'));
    const navigation = screen.getAllByRole('navigation', { name: '공통 메뉴' })[0];
    expect(within(navigation).getByRole('button', { name: '프로젝트' })).toBeInTheDocument();
    expect(within(navigation).getByRole('button', { name: '진행 현황' })).toBeInTheDocument();
    expect(within(navigation).queryByRole('button', { name: 'Pending' })).not.toBeInTheDocument();
    expect(within(navigation).queryByRole('button', { name: 'G2' })).not.toBeInTheDocument();
    expect(within(navigation).queryByRole('button', { name: '사용자 관리' })).not.toBeInTheDocument();
  });

  it('integrates membership and business-unit profiles in user management', async () => {
    const calls: Array<{ path: string; headers: Headers }> = [];
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/admin/business-unit-access');
    const fallbackFetch = shellFetch((headers: Headers) => selectedUser({
      status: 'selected',
      selectedBusinessUnit: (headers.get('X-Qms-Business-Unit') ?? 'CHEONGJU') as 'CHEONGJU' | 'OSAN',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }), calls);
    let submittedAccess: {
      isOverallAdministrator: boolean;
      profiles: Array<{ businessUnitCode: string; roleCodes: string[]; isActive: boolean }>;
    } | null = null;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/access') && init?.method === 'PUT') {
        submittedAccess = JSON.parse(String(init.body));
        return json({
          changed: true,
          accessVersion: 1,
          snapshot: membershipAdministrationResponse(['CHEONGJU', 'OSAN'], true)
        });
      }
      return fallbackFetch(input, init);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사용자 관리' })).toBeInTheDocument();
    await waitFor(() => expect(calls.some((call) => call.path === '/api/admin/user-access/users')).toBe(true));
    const adminRow = (await screen.findByText('Synthetic Overall Admin')).closest('tr');
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('CHEONGJU');
    expect(calls.filter((call) => call.path === '/api/admin/users'
      && call.headers.get('X-Qms-Business-Unit') === 'OSAN')).toHaveLength(0);
    expect(screen.queryByText('사업부 소속 관리')).not.toBeInTheDocument();
    expect(screen.getAllByRole('columnheader').map((header) => header.textContent)).toEqual([
      '', '활성 상태', '사업부', '부서', '역할', '부서장', '총괄 관리자', ''
    ]);
    expect(adminRow).not.toBeNull();
    expect(within(adminRow!).getByText('총괄')).toBeInTheDocument();

    const newUserRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(newUserRow).not.toBeNull();
    expect(within(newUserRow!).getByText('new-user@example.invalid')).toBeInTheDocument();
    const businessUnit = within(newUserRow!).getByRole('combobox', { name: 'Synthetic New User 사업부' });
    fireEvent.change(businessUnit, { target: { value: 'OSAN' } });
    const active = within(newUserRow!).getByRole('checkbox', { name: 'Synthetic New User 활성 상태' });
    await waitFor(() => expect(active).toBeEnabled());
    fireEvent.click(active);
    fireEvent.change(within(newUserRow!).getByRole('combobox', { name: 'Synthetic New User 부서' }), {
      target: { value: '10000000-0000-0000-0000-000000000001' }
    });
    expect(within(newUserRow!).getByLabelText('Synthetic New User 역할')).toHaveTextContent('경영지원');
    fireEvent.click(within(newUserRow!).getByRole('checkbox', { name: 'Synthetic New User 총괄 관리자' }));
    expect(within(newUserRow!).getByLabelText('Synthetic New User 역할')).toHaveTextContent('경영지원, 시스템 관리자');
    const saveMembership = within(newUserRow!).getByRole('button', { name: '저장' });
    expect(saveMembership).toBeEnabled();
    fireEvent.click(saveMembership);
    expect(await screen.findByText('사용자 접근 정보를 저장했습니다.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/access') && init?.method === 'PUT'
    ))).toHaveLength(1);
    expect(submittedAccess).toMatchObject({
      isOverallAdministrator: true,
      profiles: expect.arrayContaining([
        expect.objectContaining({ businessUnitCode: 'CHEONGJU', isActive: true }),
        expect.objectContaining({
          businessUnitCode: 'OSAN',
          isActive: true,
          roleCodes: expect.arrayContaining(['management-support', 'system-administrator'])
        })
      ])
    });
  });

  it('shows the integrated approval-pending title and an explicit empty state when the filter has no matches', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users?filter=approval-pending');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    })));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '승인 대기 사용자' })).toBeInTheDocument();
    expect(await screen.findByText('현재 승인 대기 중인 사용자가 없습니다.')).toBeInTheDocument();
    expect(screen.getByText('승인 준비가 필요한 사용자가 생기면 여기에 표시됩니다.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows only approval-pending users in the integrated filtered list', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users?filter=approval-pending');
    const snapshot = membershipAdministrationResponse([]);
    const fallbackFetch = shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }));
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/admin/user-access/users') {
        return json(snapshot);
      }
      return fallbackFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '승인 대기 사용자' })).toBeInTheDocument();
    expect(await screen.findByText('Synthetic New User')).toBeInTheDocument();
    expect(screen.queryByText('Synthetic Overall Admin')).not.toBeInTheDocument();
    expect(screen.queryByText('현재 승인 대기 중인 사용자가 없습니다.')).not.toBeInTheDocument();
  });

  it('auto-fills a department role while preserving special roles', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    const snapshot = membershipAdministrationResponse(['CHEONGJU']);
    snapshot.users[1].profiles[0].roles = ['management-support', 'system-administrator'];
    snapshot.users[1].profiles[0].explicitRoles = ['system-administrator'];
    const fallbackFetch = shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }));
    let submittedProfiles: Array<{ businessUnitCode: string; roleCodes: string[] }> = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/admin/user-access/users') {
        return json(snapshot);
      }
      if (url.pathname.endsWith('/access') && init?.method === 'PUT') {
        submittedProfiles = JSON.parse(String(init.body)).profiles;
        return json({ changed: true, accessVersion: 1, snapshot });
      }
      return fallbackFetch(input, init);
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<App />);

    const userRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(userRow).not.toBeNull();
    fireEvent.change(within(userRow!).getByRole('combobox', { name: 'Synthetic New User 부서' }), {
      target: { value: '10000000-0000-0000-0000-000000000005' }
    });
    expect(within(userRow!).getByLabelText('Synthetic New User 역할')).toHaveTextContent('품질, 시스템 관리자');
    fireEvent.click(within(userRow!).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(submittedProfiles).not.toHaveLength(0));
    const cheongju = submittedProfiles.find((profile) => profile.businessUnitCode === 'CHEONGJU');
    expect(cheongju?.roleCodes).toEqual(['quality', 'system-administrator']);
  });

  it('fails closed when an active department has no default role', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    const snapshot = membershipAdministrationResponse(['CHEONGJU']);
    (snapshot.businessUnits[0].departments[0] as { defaultRoleCode: string | null }).defaultRoleCode = null;
    snapshot.users[1].profiles[0].roles = [];
    const fallbackFetch = shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
    }));
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (new URL(String(input)).pathname === '/api/admin/user-access/users') {
        return json(snapshot);
      }
      return fallbackFetch(input, init);
    }));

    render(<App />);

    const userRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(userRow).not.toBeNull();
    const feedbackId = userRow!.getAttribute('aria-describedby');
    expect(feedbackId).not.toBeNull();
    expect(document.getElementById(feedbackId!)).toHaveTextContent('활성 사용자는 기본 역할이 있는 부서를 선택해야 합니다.');
    expect(within(userRow!).getByRole('button', { name: '저장' })).toBeDisabled();
  });

  it('blocks integrated membership mutations in ReviewSafe even when disabled controls are invoked', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    const access: BusinessUnitAccess = {
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: true,
      errorCode: null
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

    const newUserRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(newUserRow).not.toBeNull();
    expect(screen.getByText('검수 전용 읽기 모드에서는 사업부 소속을 변경할 수 없습니다.')).toBeInTheDocument();
    const checkbox = within(newUserRow!).getByRole('checkbox', { name: 'Synthetic New User 활성 상태' });
    const save = within(newUserRow!).getByRole('button', { name: '저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();

    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);

    expect(checkbox).toBeChecked();
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/access') && init?.method === 'PUT'
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

    const newUserRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(newUserRow).not.toBeNull();
    expect(screen.getByText('실행 모드를 확인하는 동안에는 사업부 소속을 변경할 수 없습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
    const checkbox = within(newUserRow!).getByRole('checkbox', { name: 'Synthetic New User 활성 상태' });
    const save = within(newUserRow!).getByRole('button', { name: '저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();
    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/access') && init?.method === 'PUT'
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

    const newUserRow = (await screen.findByText('Synthetic New User')).closest('tr');
    expect(newUserRow).not.toBeNull();
    expect(await screen.findByText('실행 모드를 확인할 수 없어 사업부 소속 변경을 차단했습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /사업부로 이동/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('사업부 선택')).not.toBeInTheDocument();
    const checkbox = within(newUserRow!).getByRole('checkbox', { name: 'Synthetic New User 활성 상태' });
    const save = within(newUserRow!).getByRole('button', { name: '저장' });
    expect(checkbox).toBeDisabled();
    expect(save).toBeDisabled();
    checkbox.removeAttribute('disabled');
    fireEvent.click(checkbox);
    save.removeAttribute('disabled');
    fireEvent.click(save);
    expect(fetchMock.mock.calls.filter(([input, init]) => (
      new URL(String(input)).pathname.endsWith('/access') && init?.method === 'PUT'
    ))).toHaveLength(0);
  });

  it('fails closed to Osan home when a Cheongju-ineligible user opens an admin URL', async () => {
    const calls: Array<{ path: string; headers: Headers }> = [];
    selectBusinessUnit('OSAN');
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'OSAN',
      allowedBusinessUnits: ['OSAN'],
      isOverallAdministrator: false,
      errorCode: null
    }), calls));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '오산 홈' })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/'));
    expect(calls.filter((call) => call.path === '/api/admin/users')).toHaveLength(0);
    const navigation = screen.getAllByRole('navigation', { name: '공통 메뉴' })[0];
    expect(within(navigation).queryByRole('button', { name: '사용자 관리' })).not.toBeInTheDocument();
  });

  it('retains the full user administration controls in Cheongju', async () => {
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    vi.stubGlobal('fetch', shellFetch(selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
      allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
      isOverallAdministrator: false,
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
    selectBusinessUnit('CHEONGJU');
    window.history.replaceState(null, '', '/admin/users');
    const selected = selectedUser({
      status: 'selected',
      selectedBusinessUnit: 'CHEONGJU',
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
      headers.get('X-Qms-Business-Unit') === 'CHEONGJU' ? selected : revoked
    ), calls);
    let selectedBusinessRequestCount = 0;
    const generationBeforeRevocation = getBusinessUnitRequestState().generation;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/admin/user-access/users') {
        selectedBusinessRequestCount += 1;
        return json({
          errorCode: 'directory_membership_required',
          message: 'synthetic membership revoked'
        }, 403);
      }
      return fallbackFetch(input, init);
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: '사용자 승인이 필요합니다.' })).toBeInTheDocument();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(screen.queryByText('Synthetic Local User')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: '현재 사업부 사용자 관리' })).not.toBeInTheDocument();
    await waitFor(() => expect(calls.filter((call) => call.path === '/api/me')).toHaveLength(2));
    expect(calls.filter((call) => call.path === '/api/runtime-mode')).toHaveLength(1);
    expect(selectedBusinessRequestCount).toBe(1);
    expect(getBusinessUnitRequestState().generation).toBe(generationBeforeRevocation + 1);
  });
});
