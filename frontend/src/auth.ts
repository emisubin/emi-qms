import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type Configuration,
  type IPublicClientApplication
} from '@azure/msal-browser';

export type AuthMode = 'Dev' | 'EntraId';
export type MsalCacheLocation = 'localStorage' | 'sessionStorage';

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
