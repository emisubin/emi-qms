import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MsalProvider } from '@azure/msal-react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';

describe('authentication modes', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.doUnmock('@azure/msal-react');
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it('uses X-Dev-User in Dev API mode', async () => {
    const fetchMock = vi.fn((...args: Parameters<typeof fetch>) => {
      void args;
      return Promise.resolve(json({ displayName: 'Dev Sales' }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const { getCurrentUser, setAccessTokenProvider } = await import('../src/api');

    setAccessTokenProvider(async () => 'bearer-token');
    await getCurrentUser('dev-sales');

    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Headers;
    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:5080/api/me');
    expect(headers.get('X-Dev-User')).toBe('dev-sales');
    expect(headers.get('Authorization')).toBeNull();
  });

  it('uses Authorization Bearer in EntraId API mode', async () => {
    const fetchMock = vi.fn((...args: Parameters<typeof fetch>) => {
      void args;
      return Promise.resolve(json({ displayName: 'Entra User' }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const { getCurrentUser, setAccessTokenProvider } = await import('../src/api');

    setAccessTokenProvider(async () => 'entra-access-token');
    await getCurrentUser('');

    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Headers;
    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:5080/api/me');
    expect(headers.get('Authorization')).toBe('Bearer entra-access-token');
  });

  it('adds X-Qms-Test-User only when an EntraId admin test user is selected', async () => {
    const fetchMock = vi.fn((...args: Parameters<typeof fetch>) => {
      void args;
      return Promise.resolve(json({ displayName: 'Effective Production User' }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const { getCurrentUser, setAccessTokenProvider, setAdminTestUserKey } = await import('../src/api');

    setAccessTokenProvider(async () => 'entra-access-token');
    setAdminTestUserKey('dev-production');
    await getCurrentUser('');

    let headers = fetchMock.mock.calls[0][1]?.headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer entra-access-token');
    expect(headers.get('X-Qms-Test-User')).toBe('dev-production');
    expect(headers.get('X-Dev-User')).toBeNull();

    setAdminTestUserKey(null);
    await getCurrentUser('');

    headers = fetchMock.mock.calls[1][1]?.headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer entra-access-token');
    expect(headers.get('X-Qms-Test-User')).toBeNull();
    expect(headers.get('X-Dev-User')).toBeNull();
  });

  it('maps interaction-required token acquisition failures to a re-login API error', async () => {
    vi.stubGlobal('fetch', vi.fn());
    const { getCurrentUser, setAccessTokenProvider } = await import('../src/api');

    setAccessTokenProvider(async () => {
      throw { errorCode: 'login_required' };
    });

    await expect(getCurrentUser('')).rejects.toMatchObject({
      status: 401,
      message: '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.'
    });
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  it('does not render the development user selector in EntraId mode', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');
    const { App } = await import('../src/App');
    const { msalInstance } = await import('../src/auth');
    await msalInstance.initialize();
    const onRememberSessionChange = vi.fn();

    render(
      <MsalProvider instance={msalInstance}>
        <App onRememberSessionChange={onRememberSessionChange} />
      </MsalProvider>
    );

    expect(await screen.findByRole('heading', { name: 'EMI 프로젝트 통합관리시스템' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'login'));
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'LOGIN' })).toBeInTheDocument();
    expect(screen.getByText('회사 Microsoft 365 계정으로 로그인해 주세요.')).toBeInTheDocument();
    expect(screen.queryByText('회사 계정이 아닌 경우 로그인할 수 없습니다.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '다른 계정으로 로그인' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: '로그인 상태 유지' })).toBeChecked();
    expect(screen.queryByLabelText('개발 사용자')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('checkbox', { name: '로그인 상태 유지' }));
    expect(onRememberSessionChange).toHaveBeenCalledWith(false);
  });

  it('renders the common branded shell while MSAL is initializing', async () => {
    const { AuthInitializationScreen } = await import('../src/App');

    render(<AuthInitializationScreen rememberSession={false} />);

    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'loading');
    expect(screen.getByRole('main')).toHaveAttribute('data-auth-layout', 'login');
    expect(screen.getByRole('heading', { name: 'EMI 프로젝트 통합관리시스템' })).toBeInTheDocument();
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
    expect(screen.getByText('Microsoft 365 로그인 정보를 확인하고 있습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'LOGIN' })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: '로그인 상태 유지' })).not.toBeInTheDocument();
    expect(screen.getByRole('status', { name: '로그인 확인 중' })).toHaveClass('auth-loading-indicator');
  });

  it('renders the common branded shell when Microsoft configuration is missing', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    const { App } = await import('../src/App');
    const { msalInstance } = await import('../src/auth');
    await msalInstance.initialize();

    render(
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    );

    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'configuration');
    expect(screen.getByRole('heading', { name: 'Microsoft 로그인 설정이 필요합니다.' })).toBeInTheDocument();
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
  });

  it('renders the common branded shell while an interactive login is in progress', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');
    vi.doMock('@azure/msal-react', () => ({
      MsalProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
      useMsal: () => ({
        accounts: [],
        inProgress: 'startup',
        instance: {}
      })
    }));

    const { App } = await import('../src/App');
    render(<App />);

    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'loading');
    expect(screen.getByRole('main')).toHaveAttribute('data-auth-layout', 'login');
    expect(screen.getByRole('heading', { name: 'EMI 프로젝트 통합관리시스템' })).toBeInTheDocument();
    expect(screen.getByText('Microsoft 365 로그인 정보를 확인하고 있습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'LOGIN' })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: '로그인 상태 유지' })).not.toBeInTheDocument();
    expect(screen.getByRole('status', { name: '로그인 확인 중' })).toHaveClass('auth-loading-indicator');
  });

  it('uses tenant-specific Microsoft authority and leaves account selection to the Microsoft login screen', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');

    const { loginRequest, msalAuthority, msalScopes } = await import('../src/auth');

    expect(msalAuthority).toBe('https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111');
    expect(msalAuthority).not.toContain('/common');
    expect(msalAuthority).not.toContain('/organizations');
    expect(msalScopes).toEqual(['api://33333333-3333-3333-3333-333333333333/access_as_user']);
    expect('prompt' in loginRequest).toBe(false);
  });

  it('uses the remember-session preference for MSAL cacheLocation', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');

    const { getMsalCacheLocation, setRememberSessionPreference } = await import('../src/auth');

    setRememberSessionPreference(true);
    expect(getMsalCacheLocation()).toBe('localStorage');

    setRememberSessionPreference(false);
    expect(getMsalCacheLocation()).toBe('sessionStorage');
  });

  it('restores a cached MSAL account as the active account', async () => {
    const account = testAccount('cached-account');
    const setActiveAccount = vi.fn();
    const { restoreActiveAccount } = await import('../src/auth');

    const result = restoreActiveAccount({
      getActiveAccount: () => null,
      getAllAccounts: () => [account],
      setActiveAccount
    } as never);

    expect(result).toEqual({ kind: 'single', account });
    expect(setActiveAccount).toHaveBeenCalledWith(account);
  });

  it('does not guess an active account when multiple MSAL accounts are cached', async () => {
    const setActiveAccount = vi.fn();
    const { restoreActiveAccount } = await import('../src/auth');

    const result = restoreActiveAccount({
      getActiveAccount: () => null,
      getAllAccounts: () => [testAccount('account-a'), testAccount('account-b')],
      setActiveAccount
    } as never);

    expect(result).toEqual({ kind: 'multiple' });
    expect(setActiveAccount).not.toHaveBeenCalled();
  });

  it('renders the project list after cached Entra account restoration and /api/me approval', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');

    const account = testAccount('cached-admin');
    const setActiveAccount = vi.fn();
    const acquireTokenSilent = vi.fn().mockResolvedValue({ accessToken: 'restored-access-token' });
    const fakeInstance = {
      getActiveAccount: () => null,
      getAllAccounts: () => [account],
      setActiveAccount,
      acquireTokenSilent,
      loginRedirect: vi.fn(),
      logoutRedirect: vi.fn()
    };
    vi.doMock('@azure/msal-react', () => ({
      MsalProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
      useMsal: () => ({
        accounts: [],
        inProgress: 'none',
        instance: fakeInstance
      })
    }));
    vi.stubGlobal('fetch', vi.fn(approvedEntraFetch));

    const { App } = await import('../src/App');
    render(<App />);

    expect(await screen.findByText('TASK-INFRA Project')).toBeInTheDocument();
    expect(setActiveAccount).toHaveBeenCalledWith(account);
    expect(acquireTokenSilent).toHaveBeenCalledWith(expect.objectContaining({ account }));
    expect(screen.queryByText('회사 Microsoft 365 계정으로 로그인해 주세요.')).not.toBeInTheDocument();
  });

  it('shows the re-login screen when silent token acquisition requires interaction', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');

    const fakeInstance = {
      getActiveAccount: () => null,
      getAllAccounts: () => [testAccount('cached-user')],
      setActiveAccount: vi.fn(),
      acquireTokenSilent: vi.fn().mockRejectedValue({ errorCode: 'interaction_required' }),
      loginRedirect: vi.fn(),
      logoutRedirect: vi.fn()
    };
    vi.doMock('@azure/msal-react', () => ({
      MsalProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
      useMsal: () => ({
        accounts: [],
        inProgress: 'none',
        instance: fakeInstance
      })
    }));

    const { App } = await import('../src/App');
    render(<App />);

    expect(await screen.findByRole('heading', { name: '다시 로그인이 필요합니다.' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'reauth');
    expect(screen.getByText('로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.')).toBeInTheDocument();
    expect(screen.queryByText('TASK-INFRA Project')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Microsoft 365로 다시 로그인' }));

    expect(screen.queryByRole('button', { name: '다른 계정으로 로그인' })).not.toBeInTheDocument();
    expect(fakeInstance.loginRedirect).toHaveBeenCalledTimes(1);
    expect(fakeInstance.loginRedirect).toHaveBeenCalledWith(expect.not.objectContaining({ prompt: 'select_account' }));
  });

  it('renders the common branded error shell for a non-interaction token failure', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'EntraId');
    vi.stubEnv('VITE_AZURE_TENANT_ID', '11111111-1111-1111-1111-111111111111');
    vi.stubEnv('VITE_AZURE_CLIENT_ID', '22222222-2222-2222-2222-222222222222');
    vi.stubEnv('VITE_AZURE_API_SCOPE', 'api://33333333-3333-3333-3333-333333333333/access_as_user');

    const fakeInstance = {
      getActiveAccount: () => null,
      getAllAccounts: () => [testAccount('cached-user')],
      setActiveAccount: vi.fn(),
      acquireTokenSilent: vi.fn().mockRejectedValue(new Error('synthetic token failure')),
      loginRedirect: vi.fn(),
      logoutRedirect: vi.fn()
    };
    vi.doMock('@azure/msal-react', () => ({
      MsalProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
      useMsal: () => ({
        accounts: [],
        inProgress: 'none',
        instance: fakeInstance
      })
    }));

    const { App } = await import('../src/App');
    render(<App />);

    expect(await screen.findByRole('heading', { name: '인증 정보를 확인할 수 없습니다.' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'error');
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
  });
});

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function testAccount(homeAccountId: string) {
  return {
    homeAccountId,
    environment: 'login.microsoftonline.com',
    tenantId: '11111111-1111-1111-1111-111111111111',
    username: `${homeAccountId}@example.test`,
    localAccountId: homeAccountId,
    name: homeAccountId
  };
}

async function approvedEntraFetch(input: RequestInfo | URL): Promise<Response> {
  const url = new URL(String(input));
  if (url.pathname === '/health/ready') {
    return json({
      name: 'ready',
      status: 'ok',
      database: { isReady: true, reason: 'reachable' },
      checkedAtUtc: '2026-07-02T00:00:00Z'
    });
  }

  if (url.pathname === '/api/me') {
    return json({
      userId: '90000000-0000-0000-0000-000000000001',
      developmentUserKey: '',
      displayName: 'Entra Admin',
      email: null,
      authProvider: 'EntraId',
      isActive: true,
      approvalPending: false,
      department: null,
      roles: ['system-administrator'],
      permissions: ['projects.read', 'Project.Read.All', 'users.manage'],
      projectAccess: [],
      isTestUserSwitch: false,
      testUserKey: null,
      canUseAdminTestUserSwitch: true,
      actualUser: {
        userId: '90000000-0000-0000-0000-000000000001',
        developmentUserKey: '',
        displayName: 'Entra Admin',
        email: null,
        authProvider: 'EntraId',
        isActive: true,
        approvalPending: false,
        department: null,
        roles: ['system-administrator']
      },
      effectiveUser: {
        userId: '90000000-0000-0000-0000-000000000001',
        developmentUserKey: '',
        displayName: 'Entra Admin',
        email: null,
        authProvider: 'EntraId',
        isActive: true,
        approvalPending: false,
        department: null,
        roles: ['system-administrator']
      }
    });
  }

  if (url.pathname === '/api/my-work/summary') {
    return json({ requestedCount: 0, inProgressCount: 0, completedCount: 0, blockingCount: 0, assignedProjectCount: 0, assignedProjectBreakdown: [] });
  }

  if (url.pathname === '/api/notifications/summary') {
    return json({ unreadCount: 0, blockingCount: 0 });
  }

  if (url.pathname === '/api/projects/summary') {
    return json({
      totalProjects: 1,
      activeProjects: 1,
      onHoldProjects: 0,
      completedProjects: 0,
      cancelledProjects: 0,
      deletedProjects: 0,
      qrReadyPanels: 0,
      manufacturingCompletedProjects: 0,
      inspectionCompletedProjects: 0
    });
  }

  if (url.pathname === '/api/projects') {
    return json({
      items: [
        {
          projectId: '91000000-0000-0000-0000-000000000001',
          customerName: 'EMI Customer',
          item: 'UL67',
          projectCode: 'INFRA-001',
          projectTitle: 'TASK-INFRA Project',
          activePanelCount: 1,
          deliveryDate: '2026-08-01',
          salesOwnerUserId: null,
          salesOwnerName: null,
          status: 'Active',
          statusLabel: '진행',
          workflowStageCode: 'ProjectCreated',
          workflowStageName: '프로젝트 생성',
          progressPercent: 6,
          fatRequired: false,
          createdAtUtc: '2026-07-02T00:00:00Z',
          updatedAtUtc: '2026-07-02T00:00:00Z'
        }
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1
    });
  }

  return json({ title: 'not found' }, 404);
}
