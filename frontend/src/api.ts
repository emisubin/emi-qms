import type { ReadyHealth } from './health';
import type { CurrentUser } from './identity';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const developmentUserKey = import.meta.env.DEV ? (import.meta.env.VITE_DEV_USER_KEY ?? 'dev-admin') : undefined;

export async function getReadyHealth(): Promise<ReadyHealth> {
  return fetchJson<ReadyHealth>('/health/ready', false);
}

export async function getCurrentUser(): Promise<CurrentUser> {
  return fetchJson<CurrentUser>('/api/me', true);
}

async function fetchJson<T>(path: string, includeDevelopmentUser: boolean): Promise<T> {
  const headers = new Headers();

  if (includeDevelopmentUser && developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, { headers });

  if (!response.ok) {
    throw new Error(`API 상태 확인 실패: ${response.status}`);
  }

  return response.json() as Promise<T>;
}
