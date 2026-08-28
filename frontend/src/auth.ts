import {
  EventType,
  InteractionRequiredAuthError,
  InteractionType,
  PublicClientApplication,
  type AccountInfo,
  type AuthenticationResult,
  type Configuration,
  type IPublicClientApplication
} from '@azure/msal-browser';

export type AuthMode = 'Dev' | 'EntraId';
export type MsalCacheLocation = 'localStorage' | 'sessionStorage';
export type PendingInteractiveAuditLogin = { clientInteractionId: string };
export type StoredAuditSession = { loginCorrelationId: string; idempotencyReceipt: string };

export const rememberSessionStorageKey = 'emi-auth-remember-session';

export const authMode: AuthMode = (import.meta.env.VITE_AUTH_MODE ?? (import.meta.env.DEV ? 'Dev' : 'EntraId')).toLowerCase() === 'dev'
  ? 'Dev'
  : 'EntraId';

export const isEntraAuthMode = authMode === 'EntraId';

const tenantId = import.meta.env.VITE_AZURE_TENANT_ID ?? '';
const clientId = import.meta.env.VITE_AZURE_CLIENT_ID ?? '';
const redirectUri = import.meta.env.VITE_AZURE_REDIRECT_URI ?? (typeof window === 'undefined' ? '/' : window.location.origin);
const apiScope = import.meta.env.VITE_AZURE_API_SCOPE ?? '';
const authority = `https://login.microsoftonline.com/${tenantId || 'missing-tenant-id'}`;

export const msalScopes = apiScope ? [apiScope] : [];
export const msalAuthority = authority;

export function getRememberSessionPreference() {
  if (typeof window === 'undefined') {
    return true;
  }

  return window.localStorage.getItem(rememberSessionStorageKey) !== 'false';
}

export function setRememberSessionPreference(rememberSession: boolean) {
  if (typeof window === 'undefined') {
    return;
  }

  window.localStorage.setItem(rememberSessionStorageKey, rememberSession ? 'true' : 'false');
}

export function getMsalCacheLocation(rememberSession = getRememberSessionPreference()): MsalCacheLocation {
  return rememberSession ? 'localStorage' : 'sessionStorage';
}

export function registerInteractiveLoginAuditTracker(
  instance: IPublicClientApplication,
  rememberSession: boolean
) {
  return instance.addEventCallback((event) => {
    if (event.eventType !== EventType.LOGIN_SUCCESS
      || (event.interactionType !== InteractionType.Redirect && event.interactionType !== InteractionType.Popup)) {
      return;
    }

    const account = (event.payload as AuthenticationResult | null)?.account;
    if (!account || typeof window === 'undefined') {
      return;
    }

    auditStorage(rememberSession)?.setItem(
      pendingAuditKey(account),
      JSON.stringify({ clientInteractionId: window.crypto.randomUUID() } satisfies PendingInteractiveAuditLogin)
    );
  });
}

export function readPendingInteractiveAuditLogin(
  account: AccountInfo,
  rememberSession: boolean
): PendingInteractiveAuditLogin | null {
  return readAuditJson<PendingInteractiveAuditLogin>(auditStorage(rememberSession)?.getItem(pendingAuditKey(account)));
}

export function clearPendingInteractiveAuditLogin(account: AccountInfo, rememberSession: boolean) {
  auditStorage(rememberSession)?.removeItem(pendingAuditKey(account));
}

export function readStoredAuditSession(account: AccountInfo, rememberSession: boolean): StoredAuditSession | null {
  return readAuditJson<StoredAuditSession>(auditStorage(rememberSession)?.getItem(sessionAuditKey(account)));
}

export function readAuditStartupState(account: AccountInfo, rememberSession: boolean) {
  const pendingLogin = readPendingInteractiveAuditLogin(account, rememberSession);
  return {
    pendingLogin,
    session: pendingLogin ? null : readStoredAuditSession(account, rememberSession)
  };
}

export function saveStoredAuditSession(
  account: AccountInfo,
  rememberSession: boolean,
  session: StoredAuditSession
) {
  auditStorage(rememberSession)?.setItem(sessionAuditKey(account), JSON.stringify(session));
}

export function clearStoredAuditSession(account: AccountInfo, rememberSession: boolean) {
  auditStorage(rememberSession)?.removeItem(sessionAuditKey(account));
}

export function subscribeStoredAuditSession(
  account: AccountInfo,
  rememberSession: boolean,
  onSessionChange: (session: StoredAuditSession | null) => void
) {
  if (typeof window === 'undefined' || !rememberSession) {
    return () => undefined;
  }

  const pendingKey = pendingAuditKey(account);
  const sessionKey = sessionAuditKey(account);
  const handleStorage = (event: StorageEvent) => {
    if ((event.storageArea && event.storageArea !== window.localStorage)
      || (event.key !== null && event.key !== pendingKey && event.key !== sessionKey)) {
      return;
    }

    onSessionChange(readAuditStartupState(account, true).session);
  };

  window.addEventListener('storage', handleStorage);
  return () => window.removeEventListener('storage', handleStorage);
}

function auditStorage(rememberSession: boolean): Storage | null {
  if (typeof window === 'undefined') return null;
  return rememberSession ? window.localStorage : window.sessionStorage;
}

function pendingAuditKey(account: AccountInfo) {
  return `emi-audit-login-pending:${account.homeAccountId}`;
}

function sessionAuditKey(account: AccountInfo) {
  return `emi-audit-session:${account.homeAccountId}`;
}

function readAuditJson<T>(value: string | null | undefined): T | null {
  if (!value) return null;
  try {
    return JSON.parse(value) as T;
  } catch {
    return null;
  }
}

export function hasMsalConfiguration() {
  return Boolean(tenantId && clientId && apiScope);
}

export function createMsalInstance(rememberSession = getRememberSessionPreference()) {
  const msalConfiguration: Configuration = {
    auth: {
      clientId: clientId || 'missing-client-id',
      authority,
      redirectUri,
      postLogoutRedirectUri: typeof window === 'undefined' ? '/' : window.location.origin
    },
    cache: {
      cacheLocation: getMsalCacheLocation(rememberSession)
    }
  };

  return new PublicClientApplication(msalConfiguration);
}

export const msalInstance = createMsalInstance();

export const loginRequest = {
  scopes: msalScopes
};

export type RestoredAccountResult =
  | { kind: 'none' }
  | { kind: 'single'; account: AccountInfo }
  | { kind: 'multiple' };

export function restoreActiveAccount(
  instance: IPublicClientApplication,
  hookAccounts: AccountInfo[] = []
): RestoredAccountResult {
  const activeAccount = instance.getActiveAccount();
  if (activeAccount) {
    return { kind: 'single', account: activeAccount };
  }

  const accountsById = new Map<string, AccountInfo>();
  for (const account of [...hookAccounts, ...instance.getAllAccounts()]) {
    accountsById.set(account.homeAccountId, account);
  }

  const accounts = [...accountsById.values()];
  if (accounts.length === 0) {
    return { kind: 'none' };
  }

  if (accounts.length > 1) {
    return { kind: 'multiple' };
  }

  const account = accounts[0];
  if (!account) {
    return { kind: 'none' };
  }

  instance.setActiveAccount(account);
  return { kind: 'single', account };
}

export function isInteractionRequiredAuthError(error: unknown) {
  if (error instanceof InteractionRequiredAuthError) {
    return true;
  }

  const authError = error as { errorCode?: string; subError?: string } | null;
  const errorCode = authError?.errorCode?.toLowerCase();
  const subError = authError?.subError?.toLowerCase();
  return [
    'interaction_required',
    'login_required',
    'consent_required',
    'no_account',
    'no_account_error'
  ].some((code) => errorCode === code || subError === code);
}

export async function acquireAccessToken(instance: IPublicClientApplication, account: AccountInfo): Promise<string | null> {
  if (msalScopes.length === 0) {
    return null;
  }

  const response = await instance.acquireTokenSilent({
    account,
    scopes: msalScopes
  });
  return response.accessToken;
}
