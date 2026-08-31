import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MsalProvider } from '@azure/msal-react';
import {
  EventType,
  InteractionType,
  type AccountInfo,
  type EventCallbackFunction,
  type EventMessage,
  type IPublicClientApplication
} from '@azure/msal-browser';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';

describe('authentication modes', () => {
  afterEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    window.history.replaceState(null, '', '/');
    vi.restoreAllMocks();
    vi.doUnmock('@azure/msal-react');
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it.each([InteractionType.Redirect, InteractionType.Popup])(
    'tracks one MSAL v5 interactive login correlation across tabs from %s',
    async (interactionType) => {
      const account = { homeAccountId: `audit-account-${interactionType}` } as AccountInfo;
      const eventCallbacks: EventCallbackFunction[] = [];
      vi.spyOn(window.crypto, 'randomUUID')
        .mockReturnValue('44444444-4444-4444-8444-444444444444');
      const storageSetItem = vi.spyOn(Storage.prototype, 'setItem');
      const {
        beginInteractiveLoginAudit,
        readAuditStartupState,
        readPendingInteractiveAuditLogin,
        registerInteractiveLoginAuditTracker
      } = await import('../src/auth');

      for (const callbackId of ['first-tab', 'second-tab']) {
        const instance = {
          addEventCallback: vi.fn((callback: EventCallbackFunction) => {
            eventCallbacks.push(callback);
            return callbackId;
          })
        } as unknown as IPublicClientApplication;
        registerInteractiveLoginAuditTracker(instance, true);
      }
      expect(beginInteractiveLoginAudit()).toBe('44444444-4444-4444-8444-444444444444');
      const loginEvent: EventMessage = {
        eventType: EventType.LOGIN_SUCCESS,
        interactionType,
        payload: account,
        error: null,
        timestamp: Date.now(),
        correlationId: '44444444-4444-4444-8444-444444444444'
      };
      for (const eventCallback of eventCallbacks) {
        eventCallback(loginEvent);
      }

      expect(readPendingInteractiveAuditLogin(account)).toEqual({
        clientInteractionId: '44444444-4444-4444-8444-444444444444'
      });
      expect(storageSetItem.mock.calls.filter(([key]) => (
        key === `emi-audit-login-pending:${account.homeAccountId}`
      ))).toHaveLength(2);

      window.sessionStorage.removeItem(`emi-audit-login-pending:${account.homeAccountId}`);
      expect(readAuditStartupState(account, true)).toEqual({
        pendingLogin: null,
        session: null
      });
    }
  );

  it('does not consume the interactive login owner from a silent success event', async () => {
    const account = { homeAccountId: 'audit-account-silent' } as AccountInfo;
    let eventCallback: EventCallbackFunction = () => undefined;
    const instance = {
      addEventCallback: vi.fn((callback: EventCallbackFunction) => {
        eventCallback = callback;
        return 'silent-callback';
      })
    } as unknown as IPublicClientApplication;
    vi.spyOn(window.crypto, 'randomUUID')
      .mockReturnValue('55555555-5555-4555-8555-555555555555');
    const {
      beginInteractiveLoginAudit,
      readPendingInteractiveAuditLogin,
      registerInteractiveLoginAuditTracker
    } = await import('../src/auth');

    expect(beginInteractiveLoginAudit()).toBe('55555555-5555-4555-8555-555555555555');
    registerInteractiveLoginAuditTracker(instance, true);
    eventCallback({
      eventType: EventType.LOGIN_SUCCESS,
      interactionType: InteractionType.Silent,
      payload: account,
      error: null,
      timestamp: Date.now(),
      correlationId: '55555555-5555-4555-8555-555555555555'
    });

    expect(readPendingInteractiveAuditLogin(account)).toBeNull();
    expect(window.sessionStorage.getItem('emi-audit-login-owner'))
      .toBe('55555555-5555-4555-8555-555555555555');
  });

  it('does not restore a previous audit session while a new interactive login is pending', async () => {
    const account = { homeAccountId: 'audit-account-1' } as AccountInfo;
    const { readAuditStartupState, saveStoredAuditSession } = await import('../src/auth');
    saveStoredAuditSession(account, true, {
      loginCorrelationId: '11111111-1111-1111-1111-111111111111',
      idempotencyReceipt: '22222222-2222-2222-2222-222222222222'
    });

    expect(readAuditStartupState(account, true).session?.loginCorrelationId)
      .toBe('11111111-1111-1111-1111-111111111111');

    const pendingLogin = JSON.stringify({ clientInteractionId: '33333333-3333-3333-3333-333333333333' });
    window.localStorage.setItem('emi-audit-login-pending:audit-account-1', pendingLogin);
    window.sessionStorage.setItem('emi-audit-login-pending:audit-account-1', pendingLogin);
    const pendingState = readAuditStartupState(account, true);

    expect(pendingState.pendingLogin?.clientInteractionId).toBe('33333333-3333-3333-3333-333333333333');
    expect(pendingState.session).toBeNull();
  });

  it('clears an old shared audit session while another tab logs in and then adopts the new session', async () => {
    const account = { homeAccountId: 'audit-account-multitab' } as AccountInfo;
    const { saveStoredAuditSession, subscribeStoredAuditSession } = await import('../src/auth');
    const onSessionChange = vi.fn();
    saveStoredAuditSession(account, true, {
      loginCorrelationId: '11111111-1111-1111-1111-111111111111',
      idempotencyReceipt: '22222222-2222-2222-2222-222222222222'
    });
    const unsubscribe = subscribeStoredAuditSession(account, true, onSessionChange);
    const pendingKey = 'emi-audit-login-pending:audit-account-multitab';
    const sessionKey = 'emi-audit-session:audit-account-multitab';

    window.localStorage.setItem(
      pendingKey,
      JSON.stringify({ clientInteractionId: '33333333-3333-3333-3333-333333333333' })
    );
    window.dispatchEvent(new StorageEvent('storage', { key: pendingKey, storageArea: window.localStorage }));
    expect(onSessionChange).toHaveBeenLastCalledWith(null);

    const newSession = {
      loginCorrelationId: '44444444-4444-4444-4444-444444444444',
      idempotencyReceipt: '55555555-5555-5555-5555-555555555555'
    };
    window.localStorage.setItem(sessionKey, JSON.stringify(newSession));
    window.localStorage.removeItem(pendingKey);
    window.dispatchEvent(new StorageEvent('storage', { key: pendingKey, storageArea: window.localStorage }));
    expect(onSessionChange).toHaveBeenLastCalledWith(newSession);

    unsubscribe();
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

  it('authenticates the protected runtime mode endpoint in Dev API mode', async () => {
    const fetchMock = vi.fn((...args: Parameters<typeof fetch>) => {
      void args;
      return Promise.resolve(json({ mode: 'Development', mutationAllowed: true }));
    });
    vi.stubGlobal('fetch', fetchMock);
    const { getRuntimeMode } = await import('../src/api');

    await getRuntimeMode('dev-sales');

    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Headers;
    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:5080/api/runtime-mode');
    expect(headers.get('X-Dev-User')).toBe('dev-sales');
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

  it('attaches the owned audit session only to business mutation requests', async () => {
    const fetchMock = vi.fn((...args: Parameters<typeof fetch>) => {
      void args;
      return Promise.resolve(json({}));
    });
    vi.stubGlobal('fetch', fetchMock);
    const {
      issuePanelQr,
      listProjectPanelQrs,
      setAccessTokenProvider,
      setAuditSessionHeaders,
      setRuntimeMutationAllowed
    } = await import('../src/api');

    setAccessTokenProvider(async () => 'entra-access-token');
    setRuntimeMutationAllowed(true);
    setAuditSessionHeaders({
      loginCorrelationId: '11111111-1111-1111-1111-111111111111',
      idempotencyReceipt: '22222222-2222-2222-2222-222222222222'
    });

    await listProjectPanelQrs('', 'project-1');
    await issuePanelQr('', 'project-1', 'panel-1');

    const getHeaders = fetchMock.mock.calls[0][1]?.headers as Headers;
    const mutationHeaders = fetchMock.mock.calls[1][1]?.headers as Headers;
    expect(getHeaders.get('X-Qms-Audit-Correlation')).toBeNull();
    expect(getHeaders.get('X-Qms-Audit-Receipt')).toBeNull();
    expect(mutationHeaders.get('X-Qms-Audit-Correlation')).toBe('11111111-1111-1111-1111-111111111111');
    expect(mutationHeaders.get('X-Qms-Audit-Receipt')).toBe('22222222-2222-2222-2222-222222222222');
  });

  it('records login and logout without test-user or inherited audit headers', async () => {
    const loginSession = {
      eventId: '30000000-0000-0000-0000-000000000001',
      loginCorrelationId: '30000000-0000-0000-0000-000000000002',
      idempotencyReceipt: '30000000-0000-0000-0000-000000000003'
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(json(loginSession))
      .mockResolvedValueOnce(json({}));
    vi.stubGlobal('fetch', fetchMock);
    const {
      recordExplicitLogoutAudit,
      recordInteractiveLoginAudit,
      setAccessTokenProvider,
      setAdminTestUserKey,
      setAuditSessionHeaders
    } = await import('../src/api');

    setAccessTokenProvider(async () => 'entra-access-token');
    setAdminTestUserKey('dev-production');
    setAuditSessionHeaders({
      loginCorrelationId: 'old-correlation',
      idempotencyReceipt: 'old-receipt'
    });

    expect(await recordInteractiveLoginAudit('40000000-0000-0000-0000-000000000001')).toEqual(loginSession);
    setAuditSessionHeaders(loginSession);
    await recordExplicitLogoutAudit();

    const loginInit = fetchMock.mock.calls[0][1];
    const logoutInit = fetchMock.mock.calls[1][1];
    for (const init of [loginInit, logoutInit]) {
      const headers = init?.headers as Headers;
      expect(headers.get('Authorization')).toBe('Bearer entra-access-token');
      expect(headers.get('X-Qms-Test-User')).toBeNull();
      expect(headers.get('X-Qms-Audit-Correlation')).toBeNull();
      expect(headers.get('X-Qms-Audit-Receipt')).toBeNull();
    }
    expect(logoutInit?.keepalive).toBe(true);
    expect(JSON.parse(String(logoutInit?.body))).toEqual({
      loginCorrelationId: loginSession.loginCorrelationId,
      idempotencyReceipt: loginSession.idempotencyReceipt
    });
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
    const { PwaInstallProvider } = await import('../src/PwaInstallExperience');
    const { msalInstance } = await import('../src/auth');
    await msalInstance.initialize();
    const onRememberSessionChange = vi.fn();

    render(
      <PwaInstallProvider>
        <MsalProvider instance={msalInstance}>
          <App onRememberSessionChange={onRememberSessionChange} />
        </MsalProvider>
      </PwaInstallProvider>
    );

    expect(await screen.findByRole('heading', { name: 'EMI PMS' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'login'));
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'LOGIN' })).toBeInTheDocument();
    expect(screen.getByText('회사 Microsoft 365 계정으로 로그인해 주세요.')).toBeInTheDocument();
    expect(screen.queryByText('회사 계정이 아닌 경우 로그인할 수 없습니다.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '다른 계정으로 로그인' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: '로그인 상태 유지' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'EMI PMS 설치 안내' })).toBeInTheDocument();
    expect(screen.getByLabelText('정보 보안 안내')).toHaveTextContent('계정 및 화면 정보를 외부에 공유하지 마시고');
    const companyInformation = screen.getByLabelText('회사 정보');
    expect(companyInformation).toHaveTextContent('(주) 이엠아이');
    expect(companyInformation).toHaveTextContent('경기도 오산시 세남로길 14-11');
    expect(companyInformation).toHaveTextContent('이엠아이 청주캠퍼스');
    expect(screen.queryByLabelText('개발 사용자')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('checkbox', { name: '로그인 상태 유지' }));
    expect(onRememberSessionChange).toHaveBeenCalledWith(false);
  });

  it('renders the common branded shell while MSAL is initializing', async () => {
    const { AuthInitializationScreen } = await import('../src/App');

    render(<AuthInitializationScreen rememberSession={false} />);

    expect(screen.getByRole('main')).toHaveAttribute('data-auth-state', 'loading');
    expect(screen.getByRole('main')).toHaveAttribute('data-auth-layout', 'login');
    expect(screen.getByRole('heading', { name: 'EMI PMS' })).toBeInTheDocument();
    expect(screen.getByAltText('EMI Electric Modular Innovation')).toBeInTheDocument();
    expect(screen.getByAltText('Microsoft')).toBeInTheDocument();
    expect(screen.getByText('Microsoft 365 로그인 정보를 확인하고 있습니다.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'LOGIN' })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: '로그인 상태 유지' })).not.toBeInTheDocument();
    expect(screen.getByRole('status', { name: '로그인 확인 중' })).toHaveClass('auth-loading-indicator');
    expect(screen.getByLabelText('정보 보안 안내')).toBeInTheDocument();
    expect(screen.getByLabelText('회사 정보')).toHaveTextContent('충북 청주시 청원구 오창읍 서오창산단3로 110');
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
    expect(screen.queryByLabelText('정보 보안 안내')).not.toBeInTheDocument();
    expect(screen.getByLabelText('회사 정보')).toHaveTextContent('(주) 이엠아이');
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
    expect(screen.getByRole('heading', { name: 'EMI PMS' })).toBeInTheDocument();
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
    window.history.pushState(null, '', '/projects');
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
    const redirectRequest = fakeInstance.loginRedirect.mock.calls[0]?.[0];
    expect(redirectRequest?.correlationId).toMatch(/^[0-9a-f-]{36}$/i);
    expect(window.sessionStorage.getItem('emi-audit-login-owner')).toBe(redirectRequest?.correlationId);
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
