import { fetchJson } from './api';

export type OsanDashboardStatus = 'All' | 'NotStarted' | 'InProgress' | 'Completed' | 'Hold';
export interface OsanDashboardStage {
  sequenceNumber: number; stepCode: string; stepName: string;
  completedTargetCount: number; totalTargetCount: number; openIssueTargetCount?: number; availableTargetCount?: number;
}
export interface OsanDashboardProject {
  deliveryHold?: boolean;
  projectId: string; title: string; projectCode: string; customerName: string;
  productName: string; poNumber: string | null; workOrderNumber: string | null;
  quantity: number; deliveryDate: string; status: string;
  completedStepCount: number; totalStepCount: number; progressPercent: number;
  stages: OsanDashboardStage[];
}
export interface OsanDashboardResponse {
  customers?: string[];
  summary: { totalCount: number; notStartedCount: number; inProgressCount: number; completedCount: number; holdCount?: number };
  items: OsanDashboardProject[]; totalCount: number; page: number; pageSize: number;
}
export function getOsanDashboard(userKey: string | undefined, query: { customer?: string; search: string; status: OsanDashboardStatus; page: number; view?: 'home' | 'progress' }, signal: AbortSignal) {
  const params = new URLSearchParams({ search: query.search, customer: query.customer ?? '', status: query.status, page: String(query.page), pageSize: '11', view: query.view ?? 'progress' });
  return fetchJson<OsanDashboardResponse>(`/api/osan/dashboard?${params}`, userKey, { signal });
}
