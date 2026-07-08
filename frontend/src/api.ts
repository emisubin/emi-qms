import type { ReadyHealth } from './health';
import type { AdminUsersResponse, CurrentUser, UpdateAdminUserRequest } from './identity';
import { isInteractionRequiredAuthError } from './auth';
import type {
  AuditHistoryResponse,
  AdminDashboardResponse,
  AdminCalendarHoliday,
  AdminCalendarHolidayListResponse,
  AdminBulkActionRequest,
  AdminBulkActionResponse,
  AdminDepartmentListResponse,
  AdminMasterChangeLogListResponse,
  AdminManualNotificationSendRequest,
  AdminManualNotificationSendResponse,
  AdminNotificationDeliveryActionRequest,
  AdminNotificationDeliveryActionResponse,
  AdminNotificationDeliveryDetail,
  AdminNotificationDeliveryListResponse,
  AdminReorderRequest,
  AdminWorkItemEscalationListResponse,
  AdminWorkItemHistoryListResponse,
  BusinessCalendarResponse,
  CalendarHolidayExcelApplyResponse,
  CalendarHolidayExcelPreviewResponse,
  ChangePanelCountRequest,
  CreateAdminDepartmentRequest,
  CreateProjectRequest,
  DeletedProjectDetail,
  DeletedProjectListResponse,
  DeleteProjectRequest,
  PanelPlaceholder,
  PanelInformationBulkUpdateRequest,
  PanelInformationExcelPreviewResponse,
  PanelInformationHistoryResponse,
  PanelInformationResponse,
  PanelInputUnit,
  ProcurementBulkUpdateRequest,
  ProcurementDashboardResponse,
  ProcurementExcelPreviewResponse,
  ProcurementHistoryResponse,
  ProcurementListResponse,
  ProcurementReceiptBulkUpdateRequest,
  ProcurementResponse,
  ProcurementRequiredItemSettings,
  CreateProductionProductTypeRequest,
  PermissionMatrixResponse,
  ProductionPlanningHistoryResponse,
  ProductionPlanningExcelApplyResponse,
  ProductionPlanningExcelPreviewResponse,
  ProductionPlanningProjectListResponse,
  ProductionPlanningResponse,
  ProductionPlanningSummary,
  MyWorkItem,
  MyAssignedProjectsResponse,
  MyWorkListResponse,
  MyWorkSummary,
  NotificationItem,
  NotificationListResponse,
  NotificationSummary,
  ProductionTemplateSettings,
  ProductionProductType,
  ProjectWorkflowResponse,
  ProjectExcelApplyResponse,
  ProjectExcelPreviewResponse,
  ProjectDetail,
  ProjectDashboardSummary,
  PurgeDeletedProjectsResponse,
  ProjectListResponse,
  ProjectListTab,
  ProjectStatusChangeRequest,
  SalesOwner,
  SystemHoliday,
  UpsertAdminCalendarHolidayRequest,
  UpdateAdminDepartmentRequest,
  UpdateProductionPlanningRequest,
  UpdateProductionTemplateSettingsRequest,
  UpdateProcurementRequiredItemSettingsRequest,
  UpdateProjectRequest
} from './projects';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
export const defaultDevelopmentUserKey = import.meta.env.DEV
  ? (import.meta.env.VITE_DEV_USER_KEY ?? 'dev-sales')
  : undefined;

let accessTokenProvider: (() => Promise<string | null>) | null = null;
let adminTestUserKey: string | null = null;

export function setAccessTokenProvider(provider: (() => Promise<string | null>) | null) {
  accessTokenProvider = provider;
}

export function setAdminTestUserKey(testUserKey: string | null) {
  adminTestUserKey = testUserKey?.trim() || null;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly errors?: Record<string, string[]>
  ) {
    super(message);
  }
}

export async function getReadyHealth(): Promise<ReadyHealth> {
  return fetchJson<ReadyHealth>('/health/ready');
}

export async function getCurrentUser(developmentUserKey?: string): Promise<CurrentUser> {
  return fetchJson<CurrentUser>('/api/me', developmentUserKey);
}

export async function getAdminUsers(developmentUserKey?: string): Promise<AdminUsersResponse> {
  return fetchJson<AdminUsersResponse>('/api/admin/users', developmentUserKey);
}

export async function updateAdminUser(
  developmentUserKey: string | undefined,
  userId: string,
  request: UpdateAdminUserRequest
): Promise<AdminUsersResponse> {
  return fetchJson<AdminUsersResponse>(`/api/admin/users/${userId}`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function scheduleAdminUserDeletion(
  developmentUserKey: string | undefined,
  userId: string
): Promise<AdminUsersResponse> {
  return fetchJson<AdminUsersResponse>(`/api/admin/users/${userId}/schedule-deletion`, developmentUserKey, {
    method: 'PATCH'
  });
}

export async function restoreAdminUser(
  developmentUserKey: string | undefined,
  userId: string
): Promise<AdminUsersResponse> {
  return fetchJson<AdminUsersResponse>(`/api/admin/users/${userId}/restore`, developmentUserKey, {
    method: 'POST'
  });
}

export async function purgeAdminUser(
  developmentUserKey: string | undefined,
  userId: string
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>(`/api/admin/users/${userId}/purge`, developmentUserKey, {
    method: 'DELETE'
  });
}

export async function bulkDeleteAdminUsers(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/users/bulk-delete', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function bulkRestoreAdminUsers(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/users/bulk-restore', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function getAdminDashboard(developmentUserKey?: string): Promise<AdminDashboardResponse> {
  return fetchJson<AdminDashboardResponse>('/api/admin/dashboard', developmentUserKey);
}

export async function getAdminDepartments(developmentUserKey?: string): Promise<AdminDepartmentListResponse> {
  return fetchJson<AdminDepartmentListResponse>('/api/admin/departments', developmentUserKey);
}

export async function createAdminDepartment(
  developmentUserKey: string | undefined,
  request: CreateAdminDepartmentRequest
) {
  return fetchJson('/api/admin/departments', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function updateAdminDepartment(
  developmentUserKey: string | undefined,
  departmentId: string,
  request: UpdateAdminDepartmentRequest
) {
  return fetchJson(`/api/admin/departments/${departmentId}`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function deactivateAdminDepartment(
  developmentUserKey: string | undefined,
  departmentId: string,
  request: UpdateAdminDepartmentRequest
) {
  return fetchJson(`/api/admin/departments/${departmentId}/deactivate`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function restoreAdminDepartment(
  developmentUserKey: string | undefined,
  departmentId: string,
  request: UpdateAdminDepartmentRequest
) {
  return fetchJson(`/api/admin/departments/${departmentId}/restore`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function purgeAdminDepartment(
  developmentUserKey: string | undefined,
  departmentId: string
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>(`/api/admin/departments/${departmentId}/purge`, developmentUserKey, {
    method: 'DELETE'
  });
}

export async function bulkDeleteAdminDepartments(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/departments/bulk-delete', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function bulkRestoreAdminDepartments(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/departments/bulk-restore', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function reorderAdminDepartments(
  developmentUserKey: string | undefined,
  request: AdminReorderRequest
): Promise<AdminDepartmentListResponse> {
  return fetchJson<AdminDepartmentListResponse>('/api/admin/departments/reorder', developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getPermissionMatrix(developmentUserKey?: string): Promise<PermissionMatrixResponse> {
  return fetchJson<PermissionMatrixResponse>('/api/admin/permissions/matrix', developmentUserKey);
}

export async function getAdminMasterChangeLogs(developmentUserKey?: string): Promise<AdminMasterChangeLogListResponse> {
  return fetchJson<AdminMasterChangeLogListResponse>('/api/admin/master-data/change-logs', developmentUserKey);
}

export async function getAdminWorkItemHistory(developmentUserKey?: string): Promise<AdminWorkItemHistoryListResponse> {
  return fetchJson<AdminWorkItemHistoryListResponse>('/api/admin/work-items/history', developmentUserKey);
}

export async function getAdminNotificationDeliveries(
  developmentUserKey?: string,
  filters: { status?: string | null; channel?: string | null; deliveryType?: string | null; handlingStatus?: string | null } = {}
): Promise<AdminNotificationDeliveryListResponse> {
  const params = new URLSearchParams();
  if (filters.status) {
    params.set('status', filters.status);
  }
  if (filters.channel) {
    params.set('channel', filters.channel);
  }
  if (filters.deliveryType) {
    params.set('deliveryType', filters.deliveryType);
  }
  if (filters.handlingStatus) {
    params.set('handlingStatus', filters.handlingStatus);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<AdminNotificationDeliveryListResponse>(`/api/admin/notification-deliveries${query}`, developmentUserKey);
}

export async function acknowledgeAdminNotificationDeliveries(
  developmentUserKey: string | undefined,
  request: AdminNotificationDeliveryActionRequest
): Promise<AdminNotificationDeliveryActionResponse> {
  return fetchJson<AdminNotificationDeliveryActionResponse>('/api/admin/notification-deliveries/acknowledge', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function dismissAdminNotificationDeliveries(
  developmentUserKey: string | undefined,
  request: AdminNotificationDeliveryActionRequest
): Promise<AdminNotificationDeliveryActionResponse> {
  return fetchJson<AdminNotificationDeliveryActionResponse>('/api/admin/notification-deliveries/dismiss', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function retryAdminNotificationDeliveries(
  developmentUserKey: string | undefined,
  request: AdminNotificationDeliveryActionRequest
): Promise<AdminNotificationDeliveryActionResponse> {
  return fetchJson<AdminNotificationDeliveryActionResponse>('/api/admin/notification-deliveries/retry', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function sendAdminManualNotification(
  developmentUserKey: string | undefined,
  request: AdminManualNotificationSendRequest
): Promise<AdminManualNotificationSendResponse> {
  return fetchJson<AdminManualNotificationSendResponse>('/api/admin/notification-deliveries/send-manual', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function getAdminNotificationDelivery(
  developmentUserKey: string | undefined,
  deliveryId: string
): Promise<AdminNotificationDeliveryDetail> {
  return fetchJson<AdminNotificationDeliveryDetail>(`/api/admin/notification-deliveries/${deliveryId}`, developmentUserKey);
}

export async function getAdminWorkItemEscalations(
  developmentUserKey?: string,
  filters: { status?: string | null; level?: string | null } = {}
): Promise<AdminWorkItemEscalationListResponse> {
  const params = new URLSearchParams();
  if (filters.status) {
    params.set('status', filters.status);
  }
  if (filters.level) {
    params.set('level', filters.level);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<AdminWorkItemEscalationListResponse>(`/api/admin/work-item-escalations${query}`, developmentUserKey);
}

export async function getSalesOwners(developmentUserKey?: string): Promise<SalesOwner[]> {
  return fetchJson<SalesOwner[]>('/api/sales-owners', developmentUserKey);
}

export async function listProjects(
  developmentUserKey: string | undefined,
  search = '',
  status: ProjectListTab = 'All',
  options: { signal?: AbortSignal; deliveryDateFrom?: string; deliveryDateTo?: string } = {}
): Promise<ProjectListResponse> {
  const params = new URLSearchParams();
  if (search.trim()) {
    params.set('search', search.trim());
  }
  if (status !== 'Deleted' && status !== 'All') {
    params.set('status', status);
  }
  if (options.deliveryDateFrom) {
    params.set('deliveryDateFrom', options.deliveryDateFrom);
  }
  if (options.deliveryDateTo) {
    params.set('deliveryDateTo', options.deliveryDateTo);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<ProjectListResponse>(`/api/projects${query}`, developmentUserKey, { signal: options.signal });
}

export async function getProjectSummary(
  developmentUserKey: string | undefined,
  options: { signal?: AbortSignal } = {}
): Promise<ProjectDashboardSummary> {
  return fetchJson<ProjectDashboardSummary>('/api/projects/summary', developmentUserKey, { signal: options.signal });
}

export async function listDeletedProjects(
  developmentUserKey: string | undefined,
  search = '',
  options: { signal?: AbortSignal } = {}
): Promise<DeletedProjectListResponse> {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
  return fetchJson<DeletedProjectListResponse>(`/api/deleted-projects${query}`, developmentUserKey, { signal: options.signal });
}

export async function getProject(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}`, developmentUserKey);
}

export async function getProjectWorkflow(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProjectWorkflowResponse> {
  return fetchJson<ProjectWorkflowResponse>(`/api/projects/${projectId}/workflow`, developmentUserKey);
}

export async function getMyWorkSummary(
  developmentUserKey: string | undefined
): Promise<MyWorkSummary> {
  return fetchJson<MyWorkSummary>('/api/my-work/summary', developmentUserKey);
}

export async function listMyWorkItems(
  developmentUserKey: string | undefined,
  status?: 'Requested' | 'InProgress' | 'Completed'
): Promise<MyWorkListResponse> {
  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  return fetchJson<MyWorkListResponse>(`/api/my-work${query}`, developmentUserKey);
}

export async function listMyAssignedProjects(
  developmentUserKey: string | undefined
): Promise<MyAssignedProjectsResponse> {
  return fetchJson<MyAssignedProjectsResponse>('/api/my-work/assigned-projects', developmentUserKey);
}

export async function startMyWorkItem(
  developmentUserKey: string | undefined,
  workItemId: string
): Promise<MyWorkItem> {
  return fetchJson<MyWorkItem>(`/api/my-work/${workItemId}/start`, developmentUserKey, { method: 'POST' });
}

export async function completeMyWorkItem(
  developmentUserKey: string | undefined,
  workItemId: string
): Promise<MyWorkItem> {
  return fetchJson<MyWorkItem>(`/api/my-work/${workItemId}/complete`, developmentUserKey, { method: 'POST' });
}

export async function getNotificationSummary(
  developmentUserKey: string | undefined
): Promise<NotificationSummary> {
  return fetchJson<NotificationSummary>('/api/notifications/summary', developmentUserKey);
}

export async function listNotifications(
  developmentUserKey: string | undefined,
  readStatus?: 'read' | 'unread'
): Promise<NotificationListResponse> {
  const query = readStatus ? `?readStatus=${encodeURIComponent(readStatus)}` : '';
  return fetchJson<NotificationListResponse>(`/api/notifications${query}`, developmentUserKey);
}

export async function markNotificationRead(
  developmentUserKey: string | undefined,
  notificationId: string
): Promise<NotificationItem> {
  return fetchJson<NotificationItem>(`/api/notifications/${notificationId}/read`, developmentUserKey, { method: 'POST' });
}

export async function markAllNotificationsRead(
  developmentUserKey: string | undefined
): Promise<NotificationSummary> {
  return fetchJson<NotificationSummary>('/api/notifications/read-all', developmentUserKey, { method: 'POST' });
}

export async function getDeletedProject(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<DeletedProjectDetail> {
  return fetchJson<DeletedProjectDetail>(`/api/deleted-projects/${projectId}`, developmentUserKey);
}

export async function purgeDeletedProject(
  developmentUserKey: string | undefined,
  projectId: string,
  confirmText: string
): Promise<PurgeDeletedProjectsResponse> {
  return fetchJson<PurgeDeletedProjectsResponse>(`/api/deleted-projects/${projectId}/purge`, developmentUserKey, {
    method: 'DELETE',
    body: JSON.stringify({ confirmText })
  });
}

export async function restoreDeletedProject(
  developmentUserKey: string | undefined,
  projectId: string,
  reason: string | null = null
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/deleted-projects/${projectId}/restore`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ reason })
  });
}

export async function purgeAllDeletedProjects(
  developmentUserKey: string | undefined,
  confirmText: string
): Promise<PurgeDeletedProjectsResponse> {
  return fetchJson<PurgeDeletedProjectsResponse>('/api/deleted-projects/purge-all', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ confirmText })
  });
}

export async function createProject(
  developmentUserKey: string | undefined,
  request: CreateProjectRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>('/api/projects', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function downloadProjectExcelTemplate(
  developmentUserKey: string | undefined
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth('/api/projects/import/template', developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Project_Create_Template.xlsx'
  };
}

export async function previewProjectExcel(
  developmentUserKey: string | undefined,
  file: File
): Promise<ProjectExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  return fetchJson<ProjectExcelPreviewResponse>('/api/projects/import/preview', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function applyProjectExcel(
  developmentUserKey: string | undefined,
  file: File,
  expectedFileSha256: string
): Promise<ProjectExcelApplyResponse> {
  const form = new FormData();
  form.append('file', file);
  form.append('expectedFileSha256', expectedFileSha256);
  return fetchJson<ProjectExcelApplyResponse>('/api/projects/import/apply', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function updateProject(
  developmentUserKey: string | undefined,
  projectId: string,
  request: UpdateProjectRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function changePanelCount(
  developmentUserKey: string | undefined,
  projectId: string,
  request: ChangePanelCountRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}/change-panel-count`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function changeProjectStatus(
  developmentUserKey: string | undefined,
  projectId: string,
  action: 'hold' | 'resume' | 'cancel' | 'reactivate',
  request: ProjectStatusChangeRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}/${action}`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function deleteProject(
  developmentUserKey: string | undefined,
  projectId: string,
  request: DeleteProjectRequest
): Promise<DeletedProjectDetail> {
  return fetchJson<DeletedProjectDetail>(`/api/projects/${projectId}/delete`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function listPanels(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelPlaceholder[]> {
  return fetchJson<PanelPlaceholder[]>(`/api/projects/${projectId}/panels`, developmentUserKey);
}

export async function getPanel(
  developmentUserKey: string | undefined,
  projectId: string,
  panelId: string
): Promise<PanelPlaceholder> {
  return fetchJson<PanelPlaceholder>(`/api/projects/${projectId}/panels/${panelId}`, developmentUserKey);
}

export async function getAuditHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<AuditHistoryResponse> {
  return fetchJson<AuditHistoryResponse>(`/api/projects/${projectId}/audit-history`, developmentUserKey);
}

export async function getPanelInformation(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelInformationResponse> {
  return fetchJson<PanelInformationResponse>(`/api/projects/${projectId}/panel-information`, developmentUserKey);
}

export async function updatePanelInformation(
  developmentUserKey: string | undefined,
  projectId: string,
  request: PanelInformationBulkUpdateRequest
): Promise<PanelInformationResponse> {
  return fetchJson<PanelInformationResponse>(`/api/projects/${projectId}/panel-information`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getPanelInformationHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelInformationHistoryResponse> {
  return fetchJson<PanelInformationHistoryResponse>(`/api/projects/${projectId}/panel-information/history`, developmentUserKey);
}

export async function downloadPanelInformationTemplate(
  developmentUserKey: string | undefined,
  projectId: string,
  inputUnit: PanelInputUnit
): Promise<{ blob: Blob; fileName: string }> {
  const query = inputUnit === 'Inch' ? 'inch' : 'mm';
  const response = await fetchWithAuth(
    `/api/projects/${projectId}/panel-information/import/template?unit=${query}`,
    developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? `Panel_Information_${query}.xlsx`
  };
}

export async function previewPanelInformationExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File,
  inputUnit: PanelInputUnit | null
): Promise<PanelInformationExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  if (inputUnit) {
    form.append('inputUnit', inputUnit);
  }

  return fetchJson<PanelInformationExcelPreviewResponse>(
    `/api/projects/${projectId}/panel-information/import/preview`,
    developmentUserKey,
    {
      method: 'POST',
      body: form
    });
}

export async function applyPanelInformationExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File,
  inputUnit: PanelInputUnit | null,
  expectedFileSha256: string,
  expectedPackagingMethod: string | null,
  reason: string | null,
  expectedVersions: Array<{ panelId: string; expectedPanelInfoVersion: number }>
): Promise<PanelInformationResponse> {
  const form = new FormData();
  form.append('file', file);
  if (inputUnit) {
    form.append('inputUnit', inputUnit);
  }
  form.append('expectedFileSha256', expectedFileSha256);
  if (expectedPackagingMethod) {
    form.append('expectedPackagingMethod', expectedPackagingMethod);
  }
  form.append('expectedVersions', JSON.stringify(expectedVersions));
  if (reason) {
    form.append('reason', reason);
  }

  return fetchJson<PanelInformationResponse>(
    `/api/projects/${projectId}/panel-information/import/apply`,
    developmentUserKey,
    {
      method: 'POST',
      body: form
    });
}

export async function getProjectProcurement(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProcurementResponse> {
  return fetchJson<ProcurementResponse>(`/api/projects/${projectId}/procurement`, developmentUserKey);
}

export async function updateProjectProcurement(
  developmentUserKey: string | undefined,
  projectId: string,
  request: ProcurementBulkUpdateRequest
): Promise<ProcurementResponse> {
  return fetchJson<ProcurementResponse>(`/api/projects/${projectId}/procurement`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getProjectProcurementHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProcurementHistoryResponse> {
  return fetchJson<ProcurementHistoryResponse>(`/api/projects/${projectId}/procurement/history`, developmentUserKey);
}

export async function downloadProcurementTemplate(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth(`/api/projects/${projectId}/procurement/import/template`, developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Procurement_Plan_Template.xlsx'
  };
}

export async function downloadProcurementDashboardTemplate(
  developmentUserKey: string | undefined
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth('/api/procurement/import/template', developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Procurement_Plan_Template.xlsx'
  };
}

export async function previewProcurementExcel(
  developmentUserKey: string | undefined,
  file: File,
  projectSelections: Array<{ sourceGroupSequence: number; projectId: string }>
): Promise<ProcurementExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  form.append('projectSelections', JSON.stringify(projectSelections));
  return fetchJson<ProcurementExcelPreviewResponse>('/api/procurement/import/preview', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function applyProcurementExcel(
  developmentUserKey: string | undefined,
  file: File,
  expectedFileSha256: string,
  reason: string | null,
  projectSelections: Array<{ sourceGroupSequence: number; projectId: string }>,
  expectedVersions: Array<{ itemId: string; expectedRowVersion: number }>
): Promise<ProcurementListResponse> {
  const form = new FormData();
  form.append('file', file);
  form.append('expectedFileSha256', expectedFileSha256);
  form.append('projectSelections', JSON.stringify(projectSelections));
  form.append('expectedVersions', JSON.stringify(expectedVersions));
  if (reason) {
    form.append('reason', reason);
  }

  return fetchJson<ProcurementListResponse>('/api/procurement/import/apply', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function getProcurementDashboard(
  developmentUserKey: string | undefined,
  search = '',
  expectedReceiptDateFrom = '',
  expectedReceiptDateTo = ''
): Promise<ProcurementDashboardResponse> {
  const params = new URLSearchParams();
  if (search.trim()) {
    params.set('search', search.trim());
  }
  if (expectedReceiptDateFrom) {
    params.set('expectedReceiptDateFrom', expectedReceiptDateFrom);
  }
  if (expectedReceiptDateTo) {
    params.set('expectedReceiptDateTo', expectedReceiptDateTo);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<ProcurementDashboardResponse>(`/api/procurement/dashboard${query}`, developmentUserKey);
}

export async function listProcurementRequiredItemSettings(
  developmentUserKey: string | undefined
): Promise<ProcurementRequiredItemSettings[]> {
  return fetchJson<ProcurementRequiredItemSettings[]>('/api/procurement/settings/required-items', developmentUserKey);
}

export async function updateProcurementRequiredItemSettings(
  developmentUserKey: string | undefined,
  itemCode: string,
  request: UpdateProcurementRequiredItemSettingsRequest
): Promise<ProcurementRequiredItemSettings[]> {
  return fetchJson<ProcurementRequiredItemSettings[]>(`/api/procurement/settings/required-items/${encodeURIComponent(itemCode)}`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getMaterialReceipts(
  developmentUserKey: string | undefined,
  search = '',
  includeCompleted = false,
  expectedReceiptDateFrom = '',
  expectedReceiptDateTo = ''
): Promise<ProcurementListResponse> {
  const params = new URLSearchParams();
  if (search.trim()) {
    params.set('search', search.trim());
  }

  if (includeCompleted) {
    params.set('includeCompleted', 'true');
  }
  if (expectedReceiptDateFrom) {
    params.set('expectedReceiptDateFrom', expectedReceiptDateFrom);
  }
  if (expectedReceiptDateTo) {
    params.set('expectedReceiptDateTo', expectedReceiptDateTo);
  }

  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<ProcurementListResponse>(`/api/materials/receipts${query}`, developmentUserKey);
}

export async function updateMaterialReceipts(
  developmentUserKey: string | undefined,
  request: ProcurementReceiptBulkUpdateRequest
): Promise<ProcurementListResponse> {
  return fetchJson<ProcurementListResponse>('/api/materials/receipts', developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getProductionPlanningSummary(
  developmentUserKey: string | undefined
): Promise<ProductionPlanningSummary> {
  return fetchJson<ProductionPlanningSummary>('/api/production-planning/summary', developmentUserKey);
}

export async function listProductionPlanningProjects(
  developmentUserKey: string | undefined,
  search = ''
): Promise<ProductionPlanningProjectListResponse> {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
  return fetchJson<ProductionPlanningProjectListResponse>(`/api/production-planning/projects${query}`, developmentUserKey);
}

export async function listProductionProductTypes(
  developmentUserKey: string | undefined
): Promise<ProductionProductType[]> {
  return fetchJson<ProductionProductType[]>('/api/production-planning/product-types', developmentUserKey);
}

export async function createProductionProductType(
  developmentUserKey: string | undefined,
  request: CreateProductionProductTypeRequest
): Promise<ProductionProductType[]> {
  return fetchJson<ProductionProductType[]>('/api/production-planning/product-types', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function listProductionTemplateSettings(
  developmentUserKey: string | undefined
): Promise<ProductionTemplateSettings[]> {
  return fetchJson<ProductionTemplateSettings[]>('/api/production-planning/settings/templates', developmentUserKey);
}

export async function updateProductionTemplateSettings(
  developmentUserKey: string | undefined,
  productTypeId: string,
  request: UpdateProductionTemplateSettingsRequest
): Promise<ProductionTemplateSettings[]> {
  return fetchJson<ProductionTemplateSettings[]>(`/api/production-planning/settings/templates/${productTypeId}`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function listSystemHolidays(
  developmentUserKey: string | undefined,
  options: { countryCode?: string; dateFrom?: string; dateTo?: string; signal?: AbortSignal } = {}
): Promise<SystemHoliday[]> {
  const params = new URLSearchParams();
  params.set('countryCode', options.countryCode ?? 'KR');
  if (options.dateFrom) {
    params.set('dateFrom', options.dateFrom);
  }
  if (options.dateTo) {
    params.set('dateTo', options.dateTo);
  }

  return fetchJson<SystemHoliday[]>(`/api/system/holidays?${params.toString()}`, developmentUserKey, {
    signal: options.signal
  });
}

export async function getAdminCalendarHolidays(
  developmentUserKey: string | undefined,
  year: number,
  signal?: AbortSignal
): Promise<AdminCalendarHolidayListResponse> {
  const params = new URLSearchParams();
  params.set('year', String(year));
  return fetchJson<AdminCalendarHolidayListResponse>(`/api/admin/calendar/holidays?${params.toString()}`, developmentUserKey, {
    signal
  });
}

export async function createAdminCalendarHoliday(
  developmentUserKey: string | undefined,
  request: UpsertAdminCalendarHolidayRequest
): Promise<AdminCalendarHoliday> {
  return fetchJson<AdminCalendarHoliday>('/api/admin/calendar/holidays', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function updateAdminCalendarHoliday(
  developmentUserKey: string | undefined,
  holidayId: string,
  request: UpsertAdminCalendarHolidayRequest
): Promise<AdminCalendarHoliday> {
  return fetchJson<AdminCalendarHoliday>(`/api/admin/calendar/holidays/${holidayId}`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function deactivateAdminCalendarHoliday(
  developmentUserKey: string | undefined,
  holidayId: string
): Promise<AdminCalendarHoliday> {
  return fetchJson<AdminCalendarHoliday>(`/api/admin/calendar/holidays/${holidayId}`, developmentUserKey, {
    method: 'DELETE'
  });
}

export async function restoreAdminCalendarHoliday(
  developmentUserKey: string | undefined,
  holidayId: string
): Promise<AdminCalendarHoliday> {
  return fetchJson<AdminCalendarHoliday>(`/api/admin/calendar/holidays/${holidayId}/restore`, developmentUserKey, {
    method: 'POST'
  });
}

export async function purgeAdminCalendarHoliday(
  developmentUserKey: string | undefined,
  holidayId: string
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>(`/api/admin/calendar/holidays/${holidayId}/purge`, developmentUserKey, {
    method: 'DELETE'
  });
}

export async function bulkDeleteAdminCalendarHolidays(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/calendar/holidays/bulk-delete', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function bulkRestoreAdminCalendarHolidays(
  developmentUserKey: string | undefined,
  request: AdminBulkActionRequest
): Promise<AdminBulkActionResponse> {
  return fetchJson<AdminBulkActionResponse>('/api/admin/calendar/holidays/bulk-restore', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function downloadAdminCalendarHolidayTemplate(
  developmentUserKey: string | undefined
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth('/api/admin/calendar/holidays/template', developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Calendar_Holidays_Template.xlsx'
  };
}

export async function previewAdminCalendarHolidayExcel(
  developmentUserKey: string | undefined,
  file: File
): Promise<CalendarHolidayExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);

  return fetchJson<CalendarHolidayExcelPreviewResponse>('/api/admin/calendar/holidays/preview', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function applyAdminCalendarHolidayExcel(
  developmentUserKey: string | undefined,
  file: File
): Promise<CalendarHolidayExcelApplyResponse> {
  const form = new FormData();
  form.append('file', file);

  return fetchJson<CalendarHolidayExcelApplyResponse>('/api/admin/calendar/holidays/apply', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function getBusinessCalendar(
  developmentUserKey: string | undefined,
  options: { countryCode?: string; from: string; to: string; signal?: AbortSignal }
): Promise<BusinessCalendarResponse> {
  const params = new URLSearchParams();
  params.set('countryCode', options.countryCode ?? 'KR');
  params.set('from', options.from);
  params.set('to', options.to);

  return fetchJson<BusinessCalendarResponse>(`/api/calendar/business-days?${params.toString()}`, developmentUserKey, {
    signal: options.signal
  });
}

export async function getProjectProductionPlanning(
  developmentUserKey: string | undefined,
  projectId: string,
  signal?: AbortSignal
): Promise<ProductionPlanningResponse> {
  return fetchJson<ProductionPlanningResponse>(`/api/projects/${projectId}/production-planning`, developmentUserKey, { signal });
}

export async function updateProjectProductionPlanning(
  developmentUserKey: string | undefined,
  projectId: string,
  request: UpdateProductionPlanningRequest
): Promise<ProductionPlanningResponse> {
  return fetchJson<ProductionPlanningResponse>(`/api/projects/${projectId}/production-planning`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getProjectProductionPlanningHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProductionPlanningHistoryResponse> {
  return fetchJson<ProductionPlanningHistoryResponse>(`/api/projects/${projectId}/production-planning/history`, developmentUserKey);
}

export async function downloadProductionPlanningTemplate(
  developmentUserKey: string | undefined,
  projectId: string,
  productTypeId: string
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth(`/api/projects/${projectId}/production-planning/export-template?productTypeId=${encodeURIComponent(productTypeId)}`, developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Production_Plan_Template.xlsx'
  };
}

export async function downloadProductionPlanningBulkTemplate(
  developmentUserKey: string | undefined
): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetchWithAuth('/api/production-planning/import/template', developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'Production_Planning_Bulk_Template.xlsx'
  };
}

export async function previewProductionPlanningExcel(
  developmentUserKey: string | undefined,
  file: File
): Promise<ProductionPlanningExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  return fetchJson<ProductionPlanningExcelPreviewResponse>('/api/production-planning/import/preview', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function applyProductionPlanningExcel(
  developmentUserKey: string | undefined,
  file: File,
  expectedFileSha256: string,
  reason: string | null
): Promise<ProductionPlanningExcelApplyResponse> {
  const form = new FormData();
  form.append('file', file);
  form.append('expectedFileSha256', expectedFileSha256);
  if (reason) {
    form.append('reason', reason);
  }
  return fetchJson<ProductionPlanningExcelApplyResponse>('/api/production-planning/import/apply', developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function previewProjectProductionPlanningExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File
): Promise<ProductionPlanningExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  return fetchJson<ProductionPlanningExcelPreviewResponse>(`/api/projects/${projectId}/production-planning/import/preview`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function applyProjectProductionPlanningExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File,
  expectedFileSha256: string,
  reason: string | null
): Promise<ProductionPlanningExcelApplyResponse> {
  const form = new FormData();
  form.append('file', file);
  form.append('expectedFileSha256', expectedFileSha256);
  if (reason) {
    form.append('reason', reason);
  }
  return fetchJson<ProductionPlanningExcelApplyResponse>(`/api/projects/${projectId}/production-planning/import/apply`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

async function fetchWithAuth(path: string, developmentUserKey?: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);

  if (developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  } else if (accessTokenProvider && !headers.has('Authorization')) {
    const accessToken = await accessTokenProvider();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }

    if (adminTestUserKey) {
      headers.set('X-Qms-Test-User', adminTestUserKey);
    }
  }

  return fetch(`${apiBaseUrl}${path}`, { ...init, headers });
}

async function fetchJson<T>(path: string, developmentUserKey?: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);

  if (init?.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  let response: Response;
  try {
    response = await fetchWithAuth(path, developmentUserKey, { ...init, headers });
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      throw error;
    }

    if (isInteractionRequiredAuthError(error)) {
      throw new ApiError(401, '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.');
    }

    throw new ApiError(0, '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.');
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return response.json() as Promise<T>;
}

function readContentDispositionFileName(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(value);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const quotedMatch = /filename="([^"]+)"/i.exec(value);
  if (quotedMatch?.[1]) {
    return quotedMatch[1];
  }

  const plainMatch = /filename=([^;]+)/i.exec(value);
  return plainMatch?.[1]?.trim() ?? null;
}

async function readProblem(response: Response): Promise<{ message: string; errors?: Record<string, string[]> }> {
  try {
    const payload = await response.json() as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
      fieldErrors?: Record<string, string[]>;
      message?: string;
    };
    const errors = localizeProblemErrors(payload.fieldErrors ?? payload.errors);
    return {
      message: chooseProblemMessage(response.status, payload.detail ?? payload.message, payload.title, errors),
      errors
    };
  } catch {
    return { message: statusMessage(response.status) };
  }
}

function chooseProblemMessage(status: number, detail?: string, title?: string, errors?: Record<string, string[]>) {
  const firstError = errors ? Object.values(errors).flat().find(Boolean) : undefined;
  if (detail && !isEnglishProblemTitle(detail)) {
    return detail;
  }

  if (firstError) {
    return status === 400 ? '입력값을 확인해 주세요.' : firstError;
  }

  if (title && !isEnglishProblemTitle(title)) {
    return title;
  }

  return statusMessage(status);
}

function localizeProblemErrors(errors?: Record<string, string[]>) {
  if (!errors) {
    return undefined;
  }

  return Object.fromEntries(Object.entries(errors).map(([key, values]) => [
    normalizeProblemFieldKey(key),
    values.map(localizeErrorMessage)
  ]));
}

function normalizeProblemFieldKey(key: string) {
  const normalized = key.replace(/^\$\.?/u, '').replace(/^request\./iu, '');
  return normalized
    .replace(/\.([A-Z])/gu, (_, value: string) => `.${value.toLowerCase()}`)
    .replace(/^([A-Z])/u, (_, value: string) => value.toLowerCase());
}

function localizeErrorMessage(message: string) {
  if (message.includes('could not be converted') || message.includes('is not valid')) {
    return '입력 형식이 올바르지 않습니다.';
  }

  if (isEnglishProblemTitle(message)) {
    return '입력값을 확인해 주세요.';
  }

  return message.replaceAll('QMS', '시스템');
}

function isEnglishProblemTitle(message: string) {
  return [
    'One or more validation errors occurred',
    'Internal Server Error',
    'Bad Request',
    'Unauthorized',
    'Forbidden',
    'Conflict'
  ].some((text) => message.includes(text));
}

function statusMessage(status: number) {
  if (status === 0) {
    return '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.';
  }
  if (status === 400) {
    return '입력값을 확인해 주세요.';
  }
  if (status === 401) {
    return '인증이 필요합니다.';
  }
  if (status === 403) {
    return '이 작업을 수행할 권한이 없습니다.';
  }
  if (status === 404) {
    return '대상을 찾을 수 없습니다.';
  }
  if (status === 409) {
    return '다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.';
  }
  if (status >= 500) {
    return '처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.';
  }
  return '요청을 처리할 수 없습니다.';
}
