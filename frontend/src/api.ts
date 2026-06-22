import type { ReadyHealth } from './health';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export async function getReadyHealth(): Promise<ReadyHealth> {
  const response = await fetch(`${apiBaseUrl}/health/ready`);

  if (!response.ok) {
    throw new Error(`API 상태 확인 실패: ${response.status}`);
  }

  return response.json() as Promise<ReadyHealth>;
}
