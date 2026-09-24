import { fetchJson } from './api';

export type OsanDashboardStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Hold';
export type OsanDashboardKpi = 'All' | OsanDashboardStatus | 'OpenIssue';
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
  summary: { totalCount: number; notStartedCount: number; inProgressCount: number; completedCount: number; holdCount?: number; openIssueProjectCount?: number };
  items: OsanDashboardProject[]; totalCount: number; page: number; pageSize: number;
}
export function getOsanDashboard(userKey: string | undefined, query: {
  customers: string[]; search: string; statuses: string[]; dueFrom: string; dueTo: string;
  kpi: string | null; page: number; view: 'home' | 'progress';
}, signal: AbortSignal) {
  const params = new URLSearchParams({ search: query.search, page: String(query.page), pageSize: '50', view: query.view });
  for (const customer of query.customers) params.append('customer', customer);
  for (const status of query.statuses) params.append('status', status);
  if (query.dueFrom) params.set('dueFrom', query.dueFrom);
  if (query.dueTo) params.set('dueTo', query.dueTo);
  if (query.kpi) params.set('kpi', query.kpi);
  return fetchJson<OsanDashboardResponse>(`/api/osan/dashboard?${params}`, userKey, { signal });
}
