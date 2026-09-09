import { fetchJson } from './api';

export type OsanDashboardStatus = 'All' | 'NotStarted' | 'InProgress' | 'Completed';
export interface OsanDashboardStage {
  sequenceNumber: number; stepCode: string; stepName: string;
  completedTargetCount: number; totalTargetCount: number;
}
export interface OsanDashboardProject {
  projectId: string; title: string; projectCode: string; customerName: string;
  productName: string; poNumber: string | null; workOrderNumber: string | null;
  quantity: number; deliveryDate: string; status: string;
  completedStepCount: number; totalStepCount: number; progressPercent: number;
  stages: OsanDashboardStage[];
}
export interface OsanDashboardResponse {
  summary: { totalCount: number; notStartedCount: number; inProgressCount: number; completedCount: number };
  items: OsanDashboardProject[]; totalCount: number; page: number; pageSize: number;
}
export function getOsanDashboard(userKey: string | undefined, query: { search: string; status: OsanDashboardStatus; page: number }, signal: AbortSignal) {
  const params = new URLSearchParams({ search: query.search, status: query.status, page: String(query.page), pageSize: '11' });
  return fetchJson<OsanDashboardResponse>(`/api/osan/dashboard?${params}`, userKey, { signal });
}
