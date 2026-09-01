import type { ReadyHealth } from './health';
import type {
  MonthlyBilling,
  Ul891MutationResponse,
  Ul891SetStructure,
  UpdateUl891DraftRequest
} from './ul891Sets';
import type {
  CompleteSalesSettlementRequest,
  SaveSalesSettlementDraftRequest,
  SalesSettlementDetail,
  SalesSettlementMutationResponse
} from './salesSettlement';
import type { SalesKpiMonthDetail, SalesKpiResponse, SalesTargetsResponse, SaveSalesTargetsRequest } from './salesKpi';
import type { G2HomeResponse, G2RangeResponse, SaveG2AttendanceRequest, SaveG2OperationsRequest } from './g2';
import type {
  CreateSalesBillingRequest,
  SalesBillingBatch,
  SalesBillingBatchList,
  SalesBillingCandidateList
} from './salesBilling';
import type { FormTemplateCatalog, FormTemplateManagers, FormTemplateScope, FormTemplateVersions, LqcItemTemplates, MaterialCategoryCatalog, MaterialCategoryIqcTemplates } from './formTemplates';
import type {
  ProductionControlManufacturingItem,
  ProductionControlPlanItem,
  ProductionControlTemplateCatalog
} from './productionControlTemplates';
import type { AdminUsersResponse, CurrentUser, ProfilePhotoMetadata, UpdateAdminUserRequest } from './identity';
import type { HomeMetricsResponse } from './home';
import type {
  CreateNoticeRequest,
  NoticeAttachment,
  NoticeAttachmentDeleteResponse,
  NoticeAttachmentDownload,
  NoticeDeleteResponse,
  NoticeDetail,
  NoticeListResponse,
  UpdateNoticeRequest
} from './notices';
import type {
  PanelQrPrintSheet,
  PanelQrBatchIssue,
  PanelQrRecord,
  PanelQrResolve,
  ProjectPanelQrList
} from './panelQr';
import type {
  NotificationPreferenceResponse,
  UpdateNotificationPreferencesRequest
} from './notificationPreferences';
import type { NotificationPreferenceAuditFilters, NotificationPreferenceAuditList } from './notificationPreferenceAudit';
import type { AuditDetail, AuditFilters, AuditList } from './audit';
import type {
  WebPushConfiguration,
  WebPushCurrentSubscriptionStatus,
  WebPushSubscriptionMutation
} from './webPush';
import type {
  CreatePendingRequest,
  EvidencePhotoReference,
  PendingAssignee,
  PendingDetail,
  PendingIssueType,
  PendingListResponse,
  PendingPhotoMutationResponse,
  PendingPriority,
  PendingStatus
} from './pending';
import type { PendingTypeCatalog, PendingTypeOption, ReorderPendingTypeItem } from './pendingTypes';
import { isInteractionRequiredAuthError } from './auth';
import type {
  MaterialIqcQueueResponse,
  MaterialIqcReconciliationResponse,
  MaterialReceiptActionResponse,
  MaterialReceiptListResponse,
  RegisterMaterialArrivalRequest
} from './materials';
import type { IqcReport, SaveIqcItemResponse } from './iqc-report';
import type {
  CompletePanelKittingRequest,
  PanelKittingCompletionResponse,
  PanelKittingQueueResponse
} from './panelKitting';
import type {
  ManufacturingActionDepartment,
  ManufacturingExecutionDetail,
  ManufacturingMutationResponse,
  ManufacturingQueueResponse,
  ManufacturingReleaseQueueResponse,
  ManufacturingReleaseResponse,
  StepBatchManufacturingRequest,
  StepBatchManufacturingResponse,
  StopManufacturingRequest
} from './manufacturing';
import type {
  FinalizeQualityInspectionRequest,
  QualityActionDepartment,
  QualityInspectionDetail,
  QualityInspectionMutationResponse,
  QualityInspectionQueueResponse,
  QualityInspectionStage,
  SaveQualityInspectionResponsesRequest,
  StartQualityInspectionRequest
} from './qualityInspections';
import type {
  CreateLogisticsBatchRequest,
  CreatePackingUnitRequest,
  LogisticsDraftResponse,
  LogisticsProjectHistoryResponse,
  LogisticsMutationResponse,
  LogisticsQueueResponse,
  LogisticsStage
} from './logistics';
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
  AdminNotificationDeliveryReprocessRequest,
  AdminNotificationDeliveryReprocessResponse,
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
  DepartmentAssigneeScopeResponse,
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
  UpdateDepartmentAssigneesRequest,
  UpdateProductionPlanSetDefaultRequest,
  UpdateProductionPlanSetScopeRequest,
  UpdateProductionPlanningRequest,
  UpdateProductionTemplateSettingsRequest,
  UpdateProcurementRequiredItemSettingsRequest,
  UpdateProjectRequest
} from './projects';

const apiBaseUrl = normalizeApiBaseUrl(import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080');
export const defaultDevelopmentUserKey = import.meta.env.DEV
  ? (import.meta.env.VITE_DEV_USER_KEY ?? 'dev-sales')
  : undefined;

export async function listProjectPanelQrs(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProjectPanelQrList> {
  return fetchJson<ProjectPanelQrList>(`/api/projects/${encodeURIComponent(projectId)}/qr`, developmentUserKey);
}

export async function issuePanelQr(
  developmentUserKey: string | undefined,
  projectId: string,
  panelId: string
): Promise<PanelQrRecord> {
  return fetchJson<PanelQrRecord>(
    `/api/projects/${encodeURIComponent(projectId)}/panels/${encodeURIComponent(panelId)}/qr`,
    developmentUserKey,
    { method: 'POST', body: '{}' }
  );
}

export async function rotatePanelQr(
  developmentUserKey: string | undefined,
  projectId: string,
  panelId: string,
  reason: string
): Promise<PanelQrRecord> {
  return fetchJson<PanelQrRecord>(
    `/api/projects/${encodeURIComponent(projectId)}/panels/${encodeURIComponent(panelId)}/qr/rotate`,
    developmentUserKey,
    { method: 'POST', body: JSON.stringify({ reason }) }
  );
}

export async function issuePanelQrBatch(
  developmentUserKey: string | undefined,
  projectId: string,
  panelIds: readonly string[]
): Promise<PanelQrBatchIssue> {
  return fetchJson<PanelQrBatchIssue>(
    `/api/projects/${encodeURIComponent(projectId)}/qr/issue-batch`,
    developmentUserKey,
    { method: 'POST', body: JSON.stringify({ panelIds }) }
  );
}

export async function preparePanelQrPrintSheet(
  developmentUserKey: string | undefined,
  projectId: string,
  panelIds: readonly string[]
): Promise<PanelQrPrintSheet> {
  return fetchJson<PanelQrPrintSheet>(
    `/api/projects/${encodeURIComponent(projectId)}/qr/print-sheet`,
    developmentUserKey,
    { method: 'POST', body: JSON.stringify({ panelIds }) }
  );
}

export async function getPanelQrImage(
  developmentUserKey: string | undefined,
  projectId: string,
  panelId: string,
  format: 'svg' | 'png' = 'svg'
): Promise<Blob> {
  let response: Response;
  try {
    response = await fetchWithAuth(
      `/api/projects/${encodeURIComponent(projectId)}/panels/${encodeURIComponent(panelId)}/qr/image?format=${format}`,
      developmentUserKey
    );
  } catch (error: unknown) {
    if (isInteractionRequiredAuthError(error)) {
      throw new ApiError(401, '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.');
    }
    throw new ApiError(0, '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.');
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
}

export async function resolvePanelQr(
  developmentUserKey: string | undefined,
  token: string
): Promise<PanelQrResolve> {
  return fetchJson<PanelQrResolve>('/api/qr/resolve', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ token })
  });
}

export async function getSalesKpi(
  developmentUserKey: string | undefined,
  options: { year?: number; currency?: string } = {}
): Promise<SalesKpiResponse> {
  const params = new URLSearchParams();
  if (options.year) params.set('year', String(options.year));
  if (options.currency) params.set('currency', options.currency);
  const query = params.toString();
  return fetchJson<SalesKpiResponse>(`/api/sales/kpi${query ? `?${query}` : ''}`, developmentUserKey);
}

export async function getSalesKpiMonth(
  developmentUserKey: string | undefined,
  year: number,
  month: number,
  currency: string
): Promise<SalesKpiMonthDetail> {
  const params = new URLSearchParams({ year: String(year), currency });
  return fetchJson<SalesKpiMonthDetail>(`/api/sales/kpi/months/${month}?${params.toString()}`, developmentUserKey);
}

export async function getSalesTargets(
  developmentUserKey: string | undefined,
  year: number,
  currency: string
): Promise<SalesTargetsResponse> {
  const params = new URLSearchParams({ year: String(year), currency });
  return fetchJson<SalesTargetsResponse>(`/api/sales/targets?${params.toString()}`, developmentUserKey);
}

export async function saveSalesTargets(
  developmentUserKey: string | undefined,
  request: SaveSalesTargetsRequest
): Promise<SalesTargetsResponse> {
  return fetchJson<SalesTargetsResponse>('/api/sales/targets', developmentUserKey, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  });
}

export async function getG2Home(developmentUserKey: string | undefined, year?: number, month?: number): Promise<G2HomeResponse> {
  const params = new URLSearchParams();
  if (year) params.set('year', String(year));
  if (month) params.set('month', String(month));
  const query = params.toString();
  return fetchJson<G2HomeResponse>(`/api/g2/home${query ? `?${query}` : ''}`, developmentUserKey);
}

export async function getG2Days(developmentUserKey: string | undefined, from: string, to: string): Promise<G2RangeResponse> {
  return fetchJson<G2RangeResponse>(`/api/g2/days?${new URLSearchParams({ from, to }).toString()}`, developmentUserKey);
}

export async function saveG2Operations(developmentUserKey: string | undefined, date: string, request: SaveG2OperationsRequest): Promise<{ saved: boolean }> {
  return fetchJson<{ saved: boolean }>(`/api/g2/operations/${date}`, developmentUserKey, { method: 'PUT', body: JSON.stringify(request) });
}

export async function saveG2Attendance(developmentUserKey: string | undefined, date: string, request: SaveG2AttendanceRequest): Promise<{ saved: boolean }> {
  return fetchJson<{ saved: boolean }>(`/api/g2/attendance/${date}`, developmentUserKey, { method: 'PUT', body: JSON.stringify(request) });
}

export async function saveG2InventoryCount(developmentUserKey: string | undefined, date: string, quantity: number, expectedVersion: number | null): Promise<{ saved: boolean }> {
  return fetchJson<{ saved: boolean }>(`/api/g2/inventory-counts/${date}`, developmentUserKey, { method: 'PUT', body: JSON.stringify({ quantity, expectedVersion }) });
}

export async function deleteG2InventoryCount(developmentUserKey: string | undefined, date: string, expectedVersion: number): Promise<{ saved: boolean }> {
  return fetchJson<{ saved: boolean }>(`/api/g2/inventory-counts/${date}?expectedVersion=${expectedVersion}`, developmentUserKey, { method: 'DELETE' });
}

export async function saveG2Target(developmentUserKey: string | undefined, type: 'DailyProduction' | 'Delivery' | 'Inventory', date: string, quantity: number, expectedVersion: number | null): Promise<{ saved: boolean }> {
  return fetchJson<{ saved: boolean }>(`/api/g2/targets/${type}/${date}`, developmentUserKey, { method: 'PUT', body: JSON.stringify({ quantity, expectedVersion }) });
}

export async function getSalesBillingCandidates(
  developmentUserKey: string | undefined,
  periodStart?: string,
  periodEnd?: string
): Promise<SalesBillingCandidateList> {
  const params = new URLSearchParams();
  if (periodStart) params.set('periodStart', periodStart);
  if (periodEnd) params.set('periodEnd', periodEnd);
  const query = params.toString();
  return fetchJson<SalesBillingCandidateList>(`/api/sales/billing-requests/candidates${query ? `?${query}` : ''}`, developmentUserKey);
}

export async function listSalesBillingBatches(
  developmentUserKey: string | undefined
): Promise<SalesBillingBatchList> {
  return fetchJson<SalesBillingBatchList>('/api/sales/billing-requests', developmentUserKey);
}

export async function createSalesBillingBatch(
  developmentUserKey: string | undefined,
  request: CreateSalesBillingRequest
): Promise<SalesBillingBatch> {
  return fetchJson<SalesBillingBatch>('/api/sales/billing-requests', developmentUserKey, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  });
}

export async function downloadSalesBillingWorkbook(
  developmentUserKey: string | undefined,
  batchId: string
): Promise<{ blob: Blob; fileName: string }> {
  const file = await downloadExcelExport(
    `/api/sales/billing-requests/${encodeURIComponent(batchId)}/file`,
    developmentUserKey,
    'EMI_세금계산서_발행요청.xlsx'
  );
  return { blob: file.blob, fileName: file.fileName };
}

export async function getFormTemplateScope(developmentUserKey?: string): Promise<FormTemplateScope> {
  return fetchJson<FormTemplateScope>('/api/form-templates/my-scope', developmentUserKey);
}

export async function getFormTemplateCatalog(developmentUserKey?: string): Promise<FormTemplateCatalog> {
  return fetchJson<FormTemplateCatalog>('/api/form-templates', developmentUserKey);
}

export async function getFormTemplateVersions(developmentUserKey: string | undefined, family: string, templateKey: string): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/versions`, developmentUserKey);
}

export async function getCurrentFormTemplate(developmentUserKey: string | undefined, family: string, templateKey: string): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/current`, developmentUserKey);
}

export async function saveCurrentFormTemplate(developmentUserKey: string | undefined, family: string, templateKey: string, expectedRowVersion: number, items: FormTemplateVersions['versions'][number]['items']): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/current`, developmentUserKey, {
    method: 'PUT', body: JSON.stringify({ expectedRowVersion, items })
  });
}

export async function getLqcItemTemplates(developmentUserKey?: string): Promise<LqcItemTemplates> {
  return fetchJson<LqcItemTemplates>('/api/form-templates/lqc-items', developmentUserKey);
}

export async function updateLqcItemOperatingStatus(
  developmentUserKey: string | undefined,
  productTypeId: string,
  isOperational: boolean,
  expectedRowVersion: number
): Promise<LqcItemTemplates> {
  return fetchJson<LqcItemTemplates>(`/api/form-templates/lqc-items/${productTypeId}/operating-status`, developmentUserKey, {
    method: 'PUT', body: JSON.stringify({ isOperational, expectedRowVersion })
  });
}

export async function saveLqcItemTemplate(
  developmentUserKey: string | undefined,
  productTypeId: string,
  expectedTemplateRowVersion: number,
  items: FormTemplateVersions['versions'][number]['items']
): Promise<LqcItemTemplates> {
  return fetchJson<LqcItemTemplates>(`/api/form-templates/lqc-items/${productTypeId}/current`, developmentUserKey, {
    method: 'PUT', body: JSON.stringify({ expectedTemplateRowVersion, items })
  });
}

export async function createFormTemplateDraft(developmentUserKey: string | undefined, family: string, templateKey: string, expectedActiveRowVersion: number): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/versions`, developmentUserKey, { method: 'POST', body: JSON.stringify({ expectedActiveRowVersion }) });
}

export async function saveFormTemplateItems(developmentUserKey: string | undefined, family: string, templateKey: string, versionId: string, expectedRowVersion: number, items: FormTemplateVersions['versions'][number]['items']): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/versions/${versionId}/items`, developmentUserKey, {
    method: 'PUT', body: JSON.stringify({ expectedRowVersion, items })
  });
}

export async function activateFormTemplateVersion(developmentUserKey: string | undefined, family: string, templateKey: string, versionId: string, expectedRowVersion: number): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/versions/${versionId}/activate`, developmentUserKey, { method: 'POST', body: JSON.stringify({ expectedRowVersion }) });
}

export async function cancelFormTemplateDraft(developmentUserKey: string | undefined, family: string, templateKey: string, versionId: string, expectedRowVersion: number): Promise<FormTemplateVersions> {
  return fetchJson<FormTemplateVersions>(`/api/form-templates/${family}/${templateKey}/versions/${versionId}/cancel`, developmentUserKey, { method: 'POST', body: JSON.stringify({ expectedRowVersion }) });
}

export async function getFormTemplateManagers(developmentUserKey?: string): Promise<FormTemplateManagers> {
  return fetchJson<FormTemplateManagers>('/api/form-templates/managers', developmentUserKey);
}

export async function assignFormTemplateManager(developmentUserKey: string | undefined, userId: string, domain: string): Promise<FormTemplateManagers> {
  return fetchJson<FormTemplateManagers>('/api/form-templates/managers', developmentUserKey, { method: 'POST', body: JSON.stringify({ userId, domain }) });
}

export async function revokeFormTemplateManager(developmentUserKey: string | undefined, bindingId: string): Promise<FormTemplateManagers> {
  return fetchJson<FormTemplateManagers>(`/api/form-templates/managers/${bindingId}/revoke`, developmentUserKey, { method: 'POST' });
}

export async function getMaterialCategories(
  developmentUserKey?: string,
  includeInactive = false
): Promise<MaterialCategoryCatalog> {
  return fetchJson<MaterialCategoryCatalog>(
    `/api/form-templates/material-categories?includeInactive=${includeInactive}`,
    developmentUserKey
  );
}

export async function createMaterialCategory(
  developmentUserKey: string | undefined,
  displayName: string,
  displayOrder: number
): Promise<MaterialCategoryCatalog> {
  return fetchJson<MaterialCategoryCatalog>('/api/form-templates/material-categories', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ displayName, displayOrder })
  });
}

export async function updateMaterialCategory(
  developmentUserKey: string | undefined,
  categoryId: string,
  request: {
    expectedRowVersion: number;
    displayName: string;
    isActive: boolean;
    displayOrder: number;
  }
): Promise<MaterialCategoryCatalog> {
  return fetchJson<MaterialCategoryCatalog>(`/api/form-templates/material-categories/${categoryId}`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function getMaterialCategoryIqcTemplates(
  developmentUserKey?: string
): Promise<MaterialCategoryIqcTemplates> {
  return fetchJson<MaterialCategoryIqcTemplates>('/api/form-templates/material-category-iqc', developmentUserKey);
}

export async function updateMaterialCategoryIqcSetting(
  developmentUserKey: string | undefined,
  categoryId: string,
  isEnabled: boolean,
  decisionMode: 'ScanBased' | 'Detailed',
  expectedRowVersion: number
): Promise<MaterialCategoryIqcTemplates> {
  return fetchJson<MaterialCategoryIqcTemplates>(`/api/form-templates/material-category-iqc/${categoryId}/setting`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify({ isEnabled, decisionMode, expectedRowVersion })
  });
}

export async function saveMaterialCategoryIqcTemplate(
  developmentUserKey: string | undefined,
  categoryId: string,
  expectedTemplateRowVersion: number,
  items: MaterialCategoryIqcTemplates['items'][number]['items']
): Promise<MaterialCategoryIqcTemplates> {
  return fetchJson<MaterialCategoryIqcTemplates>(`/api/form-templates/material-category-iqc/${categoryId}/current`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify({ expectedTemplateRowVersion, items })
  });
}

export async function exportFormTemplateVersionsExcel(
  developmentUserKey: string | undefined,
  family: string,
  templateKey: string,
  versionIds: readonly string[]
): Promise<ExcelExportDownload> {
  return downloadExcelExport('/api/form-templates/export', developmentUserKey, 'EMI_양식버전_선택.xlsx', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ family, templateKey, versionIds })
  });
}

let accessTokenProvider: (() => Promise<string | null>) | null = null;
let adminTestUserKey: string | null = null;
let mutationAllowed = false;
let auditSession: AuditSessionHeaders | null = null;

export type AuditSessionHeaders = {
  loginCorrelationId: string;
  idempotencyReceipt: string;
};

export type AuditSessionResponse = AuditSessionHeaders & {
  eventId: string;
};

export type RuntimeMode = {
  mode: string;
  reviewSafe: boolean;
  mutationAllowed: boolean;
  backgroundWorkersEnabled: boolean;
  externalProvidersEnabled: boolean;
  databaseReadOnly: boolean;
  migrationExecutionEnabled: boolean;
  environment: string;
  ready: boolean;
  reason: string;
  expectedMigration: string;
  actualMigration: string | null;
  migrationLedgerStatus: string | null;
  expectedMigrationCount: number;
  actualMigrationCount: number | null;
  missingMigrations: string[];
  unexpectedMigrations: string[];
  approvedLegacyMigrations: string[];
  migrationSchemaCompatible: boolean;
  migrationLedgerReady: boolean;
};

export function setAccessTokenProvider(provider: (() => Promise<string | null>) | null) {
  accessTokenProvider = provider;
}

export function setAuditSessionHeaders(session: AuditSessionHeaders | null) {
  auditSession = session;
}

export async function recordInteractiveLoginAudit(clientInteractionId: string): Promise<AuditSessionResponse> {
  const response = await fetchWithAuth(
    '/api/audit/sessions/interactive-login',
    undefined,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientInteractionId })
    },
    { includeAdminSwitch: false, includeAuditSession: false }
  );
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.json() as Promise<AuditSessionResponse>;
}

export async function recordExplicitLogoutAudit(): Promise<void> {
  if (!auditSession) return;
  const response = await fetchWithAuth(
    '/api/audit/sessions/logout',
    undefined,
    {
      method: 'POST',
      keepalive: true,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        loginCorrelationId: auditSession.loginCorrelationId,
        idempotencyReceipt: auditSession.idempotencyReceipt
      })
    },
    { includeAdminSwitch: false, includeAuditSession: false }
  );
  if (!response.ok && response.status !== 404) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
}

export function setAdminTestUserKey(testUserKey: string | null) {
  adminTestUserKey = testUserKey?.trim() || null;
}

export function setRuntimeMutationAllowed(allowed: boolean) {
  mutationAllowed = allowed;
}

function normalizeApiBaseUrl(value: string) {
  const normalized = value.trim();
  if (!normalized || normalized === '/' || normalized === '.') {
    return '';
  }

  return normalized.endsWith('/') ? normalized.slice(0, -1) : normalized;
}

function buildApiUrl(path: string) {
  if (!apiBaseUrl) {
    return path;
  }

  if (apiBaseUrl === '/api' && (path === '/api' || path.startsWith('/api/'))) {
    return path;
  }

  if (apiBaseUrl === '/api' && (path === '/health' || path.startsWith('/health/'))) {
    return path;
  }

  return `${apiBaseUrl}${path.startsWith('/') ? '' : '/'}${path}`;
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

export async function getRuntimeMode(developmentUserKey?: string): Promise<RuntimeMode> {
  return fetchJson<RuntimeMode>('/api/runtime-mode', developmentUserKey);
}

export async function getCurrentUser(developmentUserKey?: string): Promise<CurrentUser> {
  return fetchJson<CurrentUser>('/api/me', developmentUserKey);
}

export async function getOwnProfilePhoto(developmentUserKey?: string): Promise<Blob | null> {
  const response = await fetchWithAuth('/api/me/profile-photo', developmentUserKey);
  if (response.status === 404) return null;
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
}

export async function saveOwnProfilePhoto(
  developmentUserKey: string | undefined,
  photo: File
): Promise<ProfilePhotoMetadata> {
  const form = new FormData();
  form.append('photo', photo);
  return fetchJson<ProfilePhotoMetadata>('/api/me/profile-photo', developmentUserKey, {
    method: 'PUT',
    body: form
  });
}

export async function removeOwnProfilePhoto(developmentUserKey?: string): Promise<void> {
  if (!mutationAllowed) {
    throw new ApiError(423, '현재 UAT는 검수 전용 읽기 모드입니다. 프로필 사진을 변경할 수 없습니다.');
  }
  const response = await fetchWithAuth('/api/me/profile-photo', developmentUserKey, { method: 'DELETE' });
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
}

export async function getHomeDepartmentMetrics(
  developmentUserKey?: string
): Promise<HomeMetricsResponse> {
  return fetchJson<HomeMetricsResponse>('/api/home/department-metrics', developmentUserKey);
}

export async function getAdminUsers(
  developmentUserKey?: string,
  filter?: 'approval-pending'
): Promise<AdminUsersResponse> {
  return fetchJson<AdminUsersResponse>(
    `/api/admin/users${filter === 'approval-pending' ? '?filter=approval-pending' : ''}`,
    developmentUserKey
  );
}

export async function getNotificationPreferences(
  developmentUserKey: string | undefined,
  targetUserId?: string
): Promise<NotificationPreferenceResponse> {
  const path = targetUserId
    ? `/api/admin/users/${targetUserId}/notification-preferences`
    : '/api/my/notification-preferences';
  return fetchJson<NotificationPreferenceResponse>(path, developmentUserKey);
}

export async function saveNotificationPreferences(
  developmentUserKey: string | undefined,
  targetUserId: string | undefined,
  request: UpdateNotificationPreferencesRequest
): Promise<NotificationPreferenceResponse> {
  const path = targetUserId
    ? `/api/admin/users/${targetUserId}/notification-preferences`
    : '/api/my/notification-preferences';
  return fetchJson<NotificationPreferenceResponse>(path, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function resetNotificationPreferences(
  developmentUserKey: string | undefined,
  targetUserId: string | undefined,
  expectedVersion: number
): Promise<NotificationPreferenceResponse> {
  const path = targetUserId
    ? `/api/admin/users/${targetUserId}/notification-preferences/reset`
    : '/api/my/notification-preferences/reset';
  return fetchJson<NotificationPreferenceResponse>(path, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedVersion })
  });
}

export async function getWebPushConfiguration(
  developmentUserKey?: string
): Promise<WebPushConfiguration> {
  return fetchJson<WebPushConfiguration>('/api/my/web-push', developmentUserKey);
}

export async function getCurrentWebPushStatus(
  developmentUserKey: string | undefined,
  endpoint: string
): Promise<WebPushCurrentSubscriptionStatus> {
  return fetchJson<WebPushCurrentSubscriptionStatus>('/api/my/web-push/current-status', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ endpoint })
  });
}

export async function saveCurrentWebPushSubscription(
  developmentUserKey: string | undefined,
  request: { endpoint: string; keys: { p256dh: string; auth: string } }
): Promise<WebPushSubscriptionMutation> {
  return fetchJson<WebPushSubscriptionMutation>('/api/my/web-push/subscriptions', developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function deactivateCurrentWebPushSubscription(
  developmentUserKey: string | undefined,
  endpoint: string,
  reason: 'UserRequest' | 'Logout' = 'UserRequest'
): Promise<WebPushSubscriptionMutation> {
  return fetchJson<WebPushSubscriptionMutation>('/api/my/web-push/subscriptions/deactivate-current', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ endpoint, reason })
  });
}

export async function deactivateAllWebPushSubscriptions(
  developmentUserKey?: string
): Promise<WebPushSubscriptionMutation> {
  return fetchJson<WebPushSubscriptionMutation>('/api/my/web-push/subscriptions/deactivate-all', developmentUserKey, {
    method: 'POST'
  });
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

export async function reprocessFailedAdminNotificationDeliveries(
  developmentUserKey: string | undefined,
  request: AdminNotificationDeliveryReprocessRequest
): Promise<AdminNotificationDeliveryReprocessResponse> {
  return fetchJson<AdminNotificationDeliveryReprocessResponse>('/api/admin/notification-deliveries/reprocess-failed', developmentUserKey, {
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

export async function getAdminNotificationPreferenceAudit(
  developmentUserKey: string | undefined,
  filters: NotificationPreferenceAuditFilters
): Promise<NotificationPreferenceAuditList> {
  const params = new URLSearchParams({
    from: filters.from,
    to: filters.to,
    page: String(filters.page),
    pageSize: String(filters.pageSize)
  });
  if (filters.action) params.set('action', filters.action);
  if (filters.deliveryType) params.set('deliveryType', filters.deliveryType);
  if (filters.search.trim()) params.set('search', filters.search.trim());
  return fetchJson<NotificationPreferenceAuditList>(`/api/admin/notification-preference-audit?${params.toString()}`, developmentUserKey);
}

export async function getAdminAuditEvents(
  developmentUserKey: string | undefined,
  filters: AuditFilters
): Promise<AuditList> {
  const params = new URLSearchParams({
    from: filters.from,
    to: filters.to,
    page: String(filters.page),
    pageSize: String(filters.pageSize)
  });
  if (filters.domain) params.set('domain', filters.domain);
  if (filters.action.trim()) params.set('action', filters.action.trim());
  if (filters.eventType) params.set('eventType', filters.eventType);
  if (filters.failureReason) params.set('failureReason', filters.failureReason);
  if (filters.search.trim()) params.set('search', filters.search.trim());
  return fetchJson<AuditList>(`/api/admin/audit-events?${params.toString()}`, developmentUserKey);
}

export async function getAdminAuditEventDetail(
  developmentUserKey: string | undefined,
  eventId: string,
  source: 'Global' | 'Authorization'
): Promise<AuditDetail> {
  return fetchJson<AuditDetail>(
    `/api/admin/audit-events/${encodeURIComponent(eventId)}?source=${encodeURIComponent(source)}`,
    developmentUserKey
  );
}

export async function getMyTeamsActivityDelivery(
  developmentUserKey: string | undefined,
  deliveryId: string
): Promise<AdminNotificationDeliveryDetail> {
  return fetchJson<AdminNotificationDeliveryDetail>(`/api/my/teams-activity/deliveries/${deliveryId}`, developmentUserKey);
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
  options: { signal?: AbortSignal; deliveryDateFrom?: string; deliveryDateTo?: string; pageSize?: number } = {}
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
  if (options.pageSize) {
    params.set('pageSize', String(options.pageSize));
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<ProjectListResponse>(`/api/projects${query}`, developmentUserKey, { signal: options.signal });
}

export async function exportProjectsExcel(
  developmentUserKey: string | undefined,
  search = '',
  status: Exclude<ProjectListTab, 'Deleted'> = 'All',
  deliveryDateFrom = '',
  deliveryDateTo = ''
): Promise<ExcelExportDownload> {
  const params = new URLSearchParams();
  if (search.trim()) {
    params.set('search', search.trim());
  }
  if (status !== 'All') {
    params.set('status', status);
  }
  if (deliveryDateFrom) {
    params.set('deliveryDateFrom', deliveryDateFrom);
  }
  if (deliveryDateTo) {
    params.set('deliveryDateTo', deliveryDateTo);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return downloadExcelExport(`/api/projects/export${query}`, developmentUserKey, 'EMI_프로젝트.xlsx');
}

export async function exportSelectedProjectsExcel(
  developmentUserKey: string | undefined,
  projectIds: readonly string[]
): Promise<ExcelExportDownload> {
  return downloadExcelExport(
    '/api/projects/export/selected',
    developmentUserKey,
    'EMI_프로젝트선택.xlsx',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ projectIds })
    }
  );
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

export async function getUl891SetStructure(developmentUserKey: string | undefined, projectId: string): Promise<Ul891SetStructure> {
  return fetchJson<Ul891SetStructure>(`/api/projects/${projectId}/set-structure`, developmentUserKey);
}

export async function addUl891SetSpec(developmentUserKey: string | undefined, projectId: string, request: { expectedSpecCount: number; name: string; quantity: number; panelCount: number; reason: string }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({
      operationId: crypto.randomUUID(),
      expectedSpecCount: request.expectedSpecCount,
      name: request.name,
      quantity: request.quantity,
      panelCount: request.panelCount,
      reason: request.reason
    })
  });
}

export async function updateUl891CurrentDesign(
  developmentUserKey: string | undefined,
  projectId: string,
  specId: string,
  request: {
    expectedSpecVersion: number;
    specName: string;
    reason: string;
    slots: Array<{
      slotId: string | null;
      panelName: string | null;
      panelSpecification: string | null;
      widthMm: number | null;
      heightMm: number | null;
      depthMm: number | null;
    }>;
  }
): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/design`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function updateUl891Draft(developmentUserKey: string | undefined, projectId: string, specId: string, versionId: string, request: UpdateUl891DraftRequest): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/versions/${versionId}`, developmentUserKey, { method: 'PUT', body: JSON.stringify(request) });
}

export async function publishUl891Version(developmentUserKey: string | undefined, projectId: string, specId: string, versionId: string, reason: string): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/versions/${versionId}/publish`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), reason }) });
}

export async function createUl891Version(developmentUserKey: string | undefined, projectId: string, specId: string, reason: string): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/versions`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), reason }) });
}

export async function applyUl891Version(developmentUserKey: string | undefined, projectId: string, specId: string, request: { expectedActiveInstanceCount: number; versionId: string; instanceIds: string[]; reason: string }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/apply-version`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), ...request }) });
}

export async function increaseUl891Instances(developmentUserKey: string | undefined, projectId: string, specId: string, request: { expectedActiveInstanceCount: number; quantity: number; reason: string }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-specs/${specId}/instances/increase`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), ...request }) });
}

export async function cancelUl891Instances(developmentUserKey: string | undefined, projectId: string, request: { instanceIds: string[]; procurementItemIds: string[]; reason: string; exceptionAcknowledged: boolean }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/set-instances/cancel`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), ...request }) });
}

export async function recoverUl891Case(developmentUserKey: string | undefined, projectId: string, recoveryCaseId: string, expectedVersion: number, note: string): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/recovery-cases/${recoveryCaseId}/recover`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), expectedVersion, note }) });
}

export async function getUl891MonthlyBilling(developmentUserKey: string | undefined, projectId: string): Promise<MonthlyBilling> {
  return fetchJson<MonthlyBilling>(`/api/projects/${projectId}/monthly-billing`, developmentUserKey);
}

export async function openUl891MonthlyBilling(developmentUserKey: string | undefined, projectId: string, billingMonth: string, recoveryCaseIds: string[]): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/monthly-billing/open`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), billingMonth, recoveryCaseIds }) });
}

export async function createUl891MonthlyBillingRevision(developmentUserKey: string | undefined, projectId: string, ledgerId: string, request: { expectedLedgerVersion: number; amount: number; note: string | null; recoveryCaseIds: string[]; adjustmentReason: string | null }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/monthly-billing/${ledgerId}/revisions`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), ...request }) });
}

export async function confirmUl891MonthlyBilling(developmentUserKey: string | undefined, projectId: string, ledgerId: string, request: { expectedLedgerVersion: number; invoiceConfirmedDate: string; invoiceNumber: string; note: string | null }): Promise<Ul891MutationResponse> {
  return fetchJson<Ul891MutationResponse>(`/api/projects/${projectId}/monthly-billing/${ledgerId}/confirm`, developmentUserKey, { method: 'POST', body: JSON.stringify({ operationId: crypto.randomUUID(), ...request }) });
}

export async function getProjectWorkflow(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProjectWorkflowResponse> {
  return fetchJson<ProjectWorkflowResponse>(`/api/projects/${projectId}/workflow`, developmentUserKey);
}

export async function getSalesSettlement(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<SalesSettlementDetail> {
  return fetchJson<SalesSettlementDetail>(`/api/projects/${projectId}/settlement`, developmentUserKey);
}

export async function saveSalesSettlementDraft(
  developmentUserKey: string | undefined,
  projectId: string,
  request: SaveSalesSettlementDraftRequest
): Promise<SalesSettlementMutationResponse> {
  return fetchJson<SalesSettlementMutationResponse>(`/api/projects/${projectId}/settlement/draft`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function completeSalesSettlement(
  developmentUserKey: string | undefined,
  projectId: string,
  request: CompleteSalesSettlementRequest
): Promise<SalesSettlementMutationResponse> {
  return fetchJson<SalesSettlementMutationResponse>(`/api/projects/${projectId}/settlement/complete`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function listPendingIssues(
  developmentUserKey: string | undefined,
  filters: {
    status?: PendingStatus;
    issueType?: PendingIssueType;
    priority?: PendingPriority;
    assigneeUserId?: string;
    projectId?: string;
    scope?: import('./pending').PendingListScope;
    statusGroup?: import('./pending').PendingStatusGroup;
  } = {}
): Promise<PendingListResponse> {
  const params = new URLSearchParams();
  if (filters.status) params.set('status', filters.status);
  if (filters.issueType) params.set('issueType', filters.issueType);
  if (filters.priority) params.set('priority', filters.priority);
  if (filters.assigneeUserId) params.set('assigneeUserId', filters.assigneeUserId);
  if (filters.projectId) params.set('projectId', filters.projectId);
  if (filters.scope) params.set('scope', filters.scope);
  if (filters.statusGroup) params.set('statusGroup', filters.statusGroup);
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<PendingListResponse>(`/api/pending${query}`, developmentUserKey);
}

export async function getPendingIssue(
  developmentUserKey: string | undefined,
  pendingId: string
): Promise<PendingDetail> {
  return fetchJson<PendingDetail>(`/api/pending/${pendingId}`, developmentUserKey);
}

export async function listPendingAssignees(
  developmentUserKey: string | undefined
): Promise<PendingAssignee[]> {
  return fetchJson<PendingAssignee[]>('/api/pending/assignees', developmentUserKey);
}

export async function getPendingTypeCatalog(
  developmentUserKey: string | undefined
): Promise<PendingTypeCatalog> {
  return fetchJson<PendingTypeCatalog>('/api/pending-types', developmentUserKey);
}

export async function listPendingTypeManualOptions(
  developmentUserKey: string | undefined
): Promise<PendingTypeOption[]> {
  return fetchJson<PendingTypeOption[]>('/api/pending-types/manual-options', developmentUserKey);
}

export async function listPendingTypeFilterOptions(
  developmentUserKey: string | undefined
): Promise<PendingTypeOption[]> {
  return fetchJson<PendingTypeOption[]>('/api/pending-types/filter-options', developmentUserKey);
}

export async function createPendingType(
  developmentUserKey: string | undefined,
  request: { displayName: string; description: string | null }
): Promise<PendingTypeCatalog> {
  return fetchJson<PendingTypeCatalog>('/api/pending-types', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function updatePendingType(
  developmentUserKey: string | undefined,
  code: string,
  request: { expectedRowVersion: number; displayName: string; description: string | null; isManualEnabled: boolean }
): Promise<PendingTypeCatalog> {
  return fetchJson<PendingTypeCatalog>(`/api/pending-types/${encodeURIComponent(code)}`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function setPendingTypeActive(
  developmentUserKey: string | undefined,
  code: string,
  expectedRowVersion: number,
  isActive: boolean
): Promise<PendingTypeCatalog> {
  return fetchJson<PendingTypeCatalog>(`/api/pending-types/${encodeURIComponent(code)}/${isActive ? 'activate' : 'deactivate'}`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedRowVersion })
  });
}

export async function reorderPendingTypes(
  developmentUserKey: string | undefined,
  items: ReorderPendingTypeItem[]
): Promise<PendingTypeCatalog> {
  return fetchJson<PendingTypeCatalog>('/api/pending-types/reorder', developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify({ items })
  });
}

export async function createPendingIssue(
  developmentUserKey: string | undefined,
  request: CreatePendingRequest
): Promise<PendingDetail> {
  return fetchJson<PendingDetail>('/api/pending', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function transitionPendingIssue(
  developmentUserKey: string | undefined,
  pendingId: string,
  toStatus: PendingStatus,
  expectedVersion: number,
  reason: string
): Promise<PendingDetail> {
  return fetchJson<PendingDetail>(`/api/pending/${pendingId}/transition`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ toStatus, expectedVersion, reason })
  });
}

export async function assignPendingIssue(
  developmentUserKey: string | undefined,
  pendingId: string,
  assigneeUserId: string,
  expectedVersion: number,
  reason: string
): Promise<PendingDetail> {
  return fetchJson<PendingDetail>(`/api/pending/${pendingId}/assign`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ assigneeUserId, expectedVersion, reason })
  });
}

export async function addPendingComment(
  developmentUserKey: string | undefined,
  pendingId: string,
  body: string
): Promise<PendingDetail> {
  return fetchJson<PendingDetail>(`/api/pending/${pendingId}/comments`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ body })
  });
}

export async function uploadPendingActionPhoto(
  developmentUserKey: string | undefined,
  pendingId: string,
  operationId: string,
  expectedPendingVersion: number,
  altText: string,
  photo: File
): Promise<PendingPhotoMutationResponse> {
  const form = new FormData();
  form.set('operationId', operationId);
  form.set('expectedPendingVersion', String(expectedPendingVersion));
  form.set('altText', altText);
  form.set('photo', photo);
  return fetchJson<PendingPhotoMutationResponse>(`/api/pending/${pendingId}/photos`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function removePendingActionPhoto(
  developmentUserKey: string | undefined,
  pendingId: string,
  photoId: string,
  operationId: string,
  expectedPendingVersion: number
): Promise<PendingPhotoMutationResponse> {
  const query = new URLSearchParams({
    operationId,
    expectedPendingVersion: String(expectedPendingVersion)
  });
  return fetchJson<PendingPhotoMutationResponse>(
    `/api/pending/${pendingId}/photos/${photoId}?${query.toString()}`,
    developmentUserKey,
    { method: 'DELETE' }
  );
}

export async function getPendingActionPhotoBlob(
  developmentUserKey: string | undefined,
  pendingId: string,
  photoId: string
): Promise<Blob> {
  const response = await fetchWithAuth(
    `/api/pending/${pendingId}/photos/${photoId}/content`,
    developmentUserKey
  );
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
}

export async function getEvidencePhotoBlob(
  developmentUserKey: string | undefined,
  photo: EvidencePhotoReference
): Promise<Blob> {
  if (photo.sourceKind === 'IqcReport') {
    return getIqcPhotoBlob(developmentUserKey, photo.sourceId, photo.photoId);
  }
  if (photo.sourceKind === 'PanelQualityReport') {
    return getQualityInspectionPhotoBlob(developmentUserKey, photo.sourceId, photo.photoId);
  }
  return getPendingActionPhotoBlob(developmentUserKey, photo.sourceId, photo.photoId);
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

export async function exportMyWorkExcel(
  developmentUserKey: string | undefined,
  status?: 'Requested' | 'InProgress' | 'Completed'
): Promise<ExcelExportDownload> {
  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  return downloadExcelExport(`/api/my-work/export${query}`, developmentUserKey, 'EMI_내업무.xlsx');
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

export async function getNotificationDetail(
  developmentUserKey: string | undefined,
  notificationId: string
): Promise<NotificationItem> {
  return fetchJson<NotificationItem>(`/api/notifications/${notificationId}`, developmentUserKey);
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

export async function markProjectNotificationsRead(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<NotificationSummary> {
  return fetchJson<NotificationSummary>(`/api/notifications/projects/${projectId}/read-all`, developmentUserKey, { method: 'POST' });
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

export async function exportProcurementDashboardExcel(
  developmentUserKey: string | undefined,
  search = '',
  expectedReceiptDateFrom = '',
  expectedReceiptDateTo = ''
): Promise<ExcelExportDownload> {
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
  return downloadExcelExport(`/api/procurement/dashboard/export${query}`, developmentUserKey, 'EMI_구매.xlsx');
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
  expectedReceiptDateTo = '',
  supplyType: 'All' | 'Purchased' | 'CustomerSupplied' = 'All'
): Promise<MaterialReceiptListResponse> {
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
  if (supplyType !== 'All') {
    params.set('supplyType', supplyType);
  }

  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<MaterialReceiptListResponse>(`/api/materials/receipts${query}`, developmentUserKey);
}

export async function getPanelKittingQueue(
  developmentUserKey: string | undefined,
  projectId?: string
): Promise<PanelKittingQueueResponse> {
  const query = projectId ? `?projectId=${encodeURIComponent(projectId)}` : '';
  return fetchJson<PanelKittingQueueResponse>(`/api/materials/kitting${query}`, developmentUserKey);
}

export async function completePanelKitting(
  developmentUserKey: string | undefined,
  request: CompletePanelKittingRequest
): Promise<PanelKittingCompletionResponse> {
  return fetchJson<PanelKittingCompletionResponse>('/api/materials/kitting/complete', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function getManufacturingQueue(
  developmentUserKey: string | undefined,
  projectId?: string
): Promise<ManufacturingQueueResponse> {
  const query = projectId ? `?projectId=${encodeURIComponent(projectId)}` : '';
  return fetchJson<ManufacturingQueueResponse>(`/api/manufacturing/queue${query}`, developmentUserKey);
}

export async function getManufacturingReleaseCandidates(
  developmentUserKey: string | undefined,
  projectId?: string
): Promise<ManufacturingReleaseQueueResponse> {
  const query = projectId ? `?projectId=${encodeURIComponent(projectId)}` : '';
  return fetchJson<ManufacturingReleaseQueueResponse>(`/api/manufacturing/release-candidates${query}`, developmentUserKey);
}

export async function releaseManufacturingPanels(
  developmentUserKey: string | undefined,
  request: { operationId: string; projectId: string; panelIds: string[] }
): Promise<ManufacturingReleaseResponse> {
  return fetchJson<ManufacturingReleaseResponse>('/api/manufacturing/releases', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function getManufacturingPanel(
  developmentUserKey: string | undefined,
  panelId: string
): Promise<ManufacturingExecutionDetail> {
  return fetchJson<ManufacturingExecutionDetail>(`/api/manufacturing/panels/${panelId}`, developmentUserKey);
}

export async function listManufacturingActionDepartments(
  developmentUserKey: string | undefined
): Promise<ManufacturingActionDepartment[]> {
  return fetchJson<ManufacturingActionDepartment[]>('/api/manufacturing/action-departments', developmentUserKey);
}

export async function startManufacturingExecution(
  developmentUserKey: string | undefined,
  request: { operationId: string; projectId: string; panelId: string }
): Promise<ManufacturingMutationResponse> {
  return fetchJson<ManufacturingMutationResponse>('/api/manufacturing/executions/start', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function checkManufacturingStep(
  developmentUserKey: string | undefined,
  executionId: string,
  request: { operationId: string; stepId: string; expectedVersion: number }
): Promise<ManufacturingMutationResponse> {
  return fetchJson<ManufacturingMutationResponse>(`/api/manufacturing/executions/${executionId}/check-step`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function completeManufacturingStepBatch(
  developmentUserKey: string | undefined,
  request: StepBatchManufacturingRequest
): Promise<StepBatchManufacturingResponse> {
  return fetchJson<StepBatchManufacturingResponse>(
    '/api/manufacturing/executions/step-batch',
    developmentUserKey,
    {
      method: 'POST',
      body: JSON.stringify(request)
    }
  );
}

export async function stopManufacturingExecution(
  developmentUserKey: string | undefined,
  executionId: string,
  request: StopManufacturingRequest
): Promise<ManufacturingMutationResponse> {
  return fetchJson<ManufacturingMutationResponse>(`/api/manufacturing/executions/${executionId}/stop`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function resumeManufacturingExecution(
  developmentUserKey: string | undefined,
  executionId: string,
  request: { operationId: string; expectedVersion: number }
): Promise<ManufacturingMutationResponse> {
  return fetchJson<ManufacturingMutationResponse>(`/api/manufacturing/executions/${executionId}/resume`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function completeManufacturingExecution(
  developmentUserKey: string | undefined,
  executionId: string,
  request: { operationId: string; expectedVersion: number }
): Promise<ManufacturingMutationResponse> {
  return fetchJson<ManufacturingMutationResponse>(`/api/manufacturing/executions/${executionId}/complete`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function getQualityInspectionQueue(
  developmentUserKey: string | undefined,
  stage?: QualityInspectionStage,
  projectId?: string
): Promise<QualityInspectionQueueResponse> {
  const params = new URLSearchParams();
  if (stage) params.set('stage', stage);
  if (projectId) params.set('projectId', projectId);
  const query = params.size ? `?${params.toString()}` : '';
  return fetchJson<QualityInspectionQueueResponse>(`/api/quality/inspections/queue${query}`, developmentUserKey);
}

export async function reconcileQualityInspectionHandoffs(
  developmentUserKey: string | undefined
): Promise<import('./qualityInspections').QualityInspectionReconciliationResponse> {
  return fetchJson<import('./qualityInspections').QualityInspectionReconciliationResponse>(
    '/api/quality/inspections/reconcile',
    developmentUserKey,
    { method: 'POST' }
  );
}

export async function getLogisticsQueue(
  developmentUserKey: string | undefined,
  stage: LogisticsStage,
  projectId?: string
): Promise<LogisticsQueueResponse> {
  const params = new URLSearchParams({ stage });
  if (projectId) params.set('projectId', projectId);
  return fetchJson<LogisticsQueueResponse>(`/api/logistics/queue?${params.toString()}`, developmentUserKey);
}

export async function getLogisticsDraft(
  developmentUserKey: string | undefined,
  stage: LogisticsStage,
  targetId: string
): Promise<LogisticsDraftResponse> {
  return fetchJson<LogisticsDraftResponse>(`/api/logistics/${stage}/${targetId}`, developmentUserKey);
}

export async function getLogisticsProjectHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<LogisticsProjectHistoryResponse> {
  return fetchJson<LogisticsProjectHistoryResponse>(
    `/api/logistics/projects/${projectId}/history`,
    developmentUserKey
  );
}

export async function createPackingUnit(
  developmentUserKey: string | undefined,
  request: CreatePackingUnitRequest
): Promise<LogisticsMutationResponse> {
  return fetchJson<LogisticsMutationResponse>('/api/logistics/packing-units', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function createLogisticsBatch(
  developmentUserKey: string | undefined,
  stage: Exclude<LogisticsStage, 'packing'>,
  request: CreateLogisticsBatchRequest
): Promise<LogisticsMutationResponse> {
  return fetchJson<LogisticsMutationResponse>(`/api/logistics/${stage}-batches`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function uploadLogisticsEvidence(
  developmentUserKey: string | undefined,
  stage: LogisticsStage,
  targetId: string,
  operationId: string,
  expectedVersion: number,
  altText: string,
  file: File
): Promise<LogisticsMutationResponse> {
  const form = new FormData();
  form.set('operationId', operationId);
  form.set('expectedVersion', String(expectedVersion));
  form.set('altText', altText);
  form.set('file', file);
  return fetchJson<LogisticsMutationResponse>(`/api/logistics/${stage}/${targetId}/evidence`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function finalizeLogisticsOperation(
  developmentUserKey: string | undefined,
  stage: LogisticsStage,
  targetId: string,
  operationId: string,
  expectedVersion: number
): Promise<LogisticsMutationResponse> {
  return fetchJson<LogisticsMutationResponse>(`/api/logistics/${stage}/${targetId}/finalize`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ operationId, expectedVersion })
  });
}

export async function cancelLogisticsDraft(
  developmentUserKey: string | undefined,
  stage: LogisticsStage,
  targetId: string,
  operationId: string,
  expectedVersion: number
): Promise<LogisticsMutationResponse> {
  return fetchJson<LogisticsMutationResponse>(`/api/logistics/${stage}/${targetId}/cancel`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ operationId, expectedVersion })
  });
}

export async function getManufacturingCompletionQueue(
  developmentUserKey: string | undefined
): Promise<QualityInspectionQueueResponse> {
  return fetchJson<QualityInspectionQueueResponse>('/api/quality/inspections/queue?stage=ManufacturingCompleted', developmentUserKey);
}

export async function getQualityInspectionPanel(
  developmentUserKey: string | undefined,
  panelId: string,
  stage: QualityInspectionStage
): Promise<QualityInspectionDetail> {
  return fetchJson<QualityInspectionDetail>(
    `/api/quality/inspections/panels/${panelId}?stage=${encodeURIComponent(stage)}`,
    developmentUserKey
  );
}

export async function listQualityActionDepartments(
  developmentUserKey: string | undefined
): Promise<QualityActionDepartment[]> {
  return fetchJson<QualityActionDepartment[]>('/api/quality/inspections/action-departments', developmentUserKey);
}

export async function startQualityInspection(
  developmentUserKey: string | undefined,
  request: StartQualityInspectionRequest
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>('/api/quality/inspections/start', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function saveQualityInspectionResponses(
  developmentUserKey: string | undefined,
  reportId: string,
  request: SaveQualityInspectionResponsesRequest
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>(`/api/quality/inspections/reports/${reportId}/responses`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function uploadQualityInspectionPhoto(
  developmentUserKey: string | undefined,
  reportId: string,
  operationId: string,
  templateItemId: string,
  expectedReportVersion: number,
  altText: string,
  photo: File
): Promise<QualityInspectionMutationResponse> {
  const form = new FormData();
  form.set('operationId', operationId);
  form.set('templateItemId', templateItemId);
  form.set('expectedReportVersion', String(expectedReportVersion));
  form.set('altText', altText);
  form.set('photo', photo);
  return fetchJson<QualityInspectionMutationResponse>(`/api/quality/inspections/reports/${reportId}/photos`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function removeQualityInspectionPhoto(
  developmentUserKey: string | undefined,
  reportId: string,
  photoId: string,
  operationId: string,
  expectedReportVersion: number
): Promise<QualityInspectionMutationResponse> {
  const params = new URLSearchParams({ operationId, expectedReportVersion: String(expectedReportVersion) });
  return fetchJson<QualityInspectionMutationResponse>(
    `/api/quality/inspections/reports/${reportId}/photos/${photoId}?${params.toString()}`,
    developmentUserKey,
    { method: 'DELETE' }
  );
}

export async function getQualityInspectionPhotoBlob(
  developmentUserKey: string | undefined,
  reportId: string,
  photoId: string
): Promise<Blob> {
  const response = await fetchWithAuth(
    `/api/quality/inspections/reports/${reportId}/photos/${photoId}/content`,
    developmentUserKey
  );
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
}

export async function finalizeQualityInspection(
  developmentUserKey: string | undefined,
  reportId: string,
  request: FinalizeQualityInspectionRequest
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>(`/api/quality/inspections/reports/${reportId}/finalize`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function retryQualityInspectionPdf(
  developmentUserKey: string | undefined,
  reportId: string,
  operationId: string
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>(`/api/quality/inspections/reports/${reportId}/pdf/retry`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ operationId })
  });
}

export async function downloadQualityInspectionPdf(
  developmentUserKey: string | undefined,
  reportId: string
): Promise<Blob> {
  const response = await fetchWithAuth(`/api/quality/inspections/reports/${reportId}/pdf`, developmentUserKey);
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  if (!response.headers.get('Content-Type')?.includes('application/pdf')) {
    throw new ApiError(response.status, 'PDF 생성이 아직 완료되지 않았습니다.');
  }
  return response.blob();
}

export async function requestQualityReinspection(
  developmentUserKey: string | undefined,
  request: { operationId: string; pendingId: string; expectedPendingVersion: number }
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>('/api/quality/inspections/reinspection', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function confirmPanelManufacturingCompleted(
  developmentUserKey: string | undefined,
  request: { operationId: string; projectId: string; panelId: string }
): Promise<QualityInspectionMutationResponse> {
  return fetchJson<QualityInspectionMutationResponse>('/api/quality/inspections/manufacturing-completed/confirm', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function registerMaterialArrival(
  developmentUserKey: string | undefined,
  itemId: string,
  request: RegisterMaterialArrivalRequest
): Promise<MaterialReceiptActionResponse> {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/items/${itemId}/receipts`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

/** @deprecated 입고 완료는 상태 흐름에서 파생됩니다. 이전 화면의 컴파일 호환만 유지합니다. */
export async function updateMaterialReceipts(
  developmentUserKey: string | undefined,
  request: ProcurementReceiptBulkUpdateRequest
): Promise<ProcurementListResponse> {
  return fetchJson<ProcurementListResponse>('/api/materials/receipts', developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function requestMaterialIqc(developmentUserKey: string | undefined, receiptId: string, expectedVersion: number) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/receipts/${receiptId}/iqc-requests`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedVersion })
  });
}

export async function requestMaterialReinspection(developmentUserKey: string | undefined, receiptId: string, expectedVersion: number) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/receipts/${receiptId}/reinspection`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedVersion })
  });
}

export async function confirmMaterialReceipt(developmentUserKey: string | undefined, receiptId: string, expectedVersion: number) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/receipts/${receiptId}/confirm`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedVersion })
  });
}

export async function cancelMaterialReceipt(
  developmentUserKey: string | undefined,
  receiptId: string,
  expectedVersion: number,
  reason: string
) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/receipts/${receiptId}/cancel`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedVersion, reason })
  });
}

export async function closeMaterialArrivals(
  developmentUserKey: string | undefined,
  itemId: string,
  expectedRowVersion: number,
  reason: string
) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/materials/items/${itemId}/close-arrivals`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedRowVersion, reason })
  });
}

export async function getMaterialIqcQueue(developmentUserKey: string | undefined, includeDecided = false) {
  const query = includeDecided ? '?includeDecided=true' : '';
  return fetchJson<MaterialIqcQueueResponse>(`/api/quality/iqc${query}`, developmentUserKey);
}

export async function reconcileMaterialIqcQueue(developmentUserKey: string | undefined) {
  return fetchJson<MaterialIqcReconciliationResponse>('/api/quality/iqc/reconcile', developmentUserKey, {
    method: 'POST'
  });
}

export async function recordMaterialIqcResult(
  developmentUserKey: string | undefined,
  attemptId: string,
  expectedReceiptVersion: number,
  result: 'Passed' | 'Failed',
  reason: string
) {
  return fetchJson<MaterialReceiptActionResponse>(`/api/quality/iqc/${attemptId}/result`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedReceiptVersion, result, reason })
  });
}

export async function getIqcReport(developmentUserKey: string | undefined, attemptId: string) {
  return fetchJson<IqcReport>(`/api/quality/iqc/${attemptId}/report`, developmentUserKey);
}

export async function initializeIqcReport(developmentUserKey: string | undefined, attemptId: string) {
  return fetchJson<IqcReport>(`/api/quality/iqc/${attemptId}/reports`, developmentUserKey, { method: 'POST' });
}

export async function saveIqcResponses(
  developmentUserKey: string | undefined,
  reportId: string,
  expectedReportVersion: number,
  responses: SaveIqcItemResponse[]
) {
  return fetchJson<IqcReport>(`/api/quality/iqc/reports/${reportId}/responses`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify({ expectedReportVersion, responses })
  });
}

export async function uploadIqcPhoto(
  developmentUserKey: string | undefined,
  reportId: string,
  templateItemId: string,
  expectedReportVersion: number,
  altText: string,
  photo: File
) {
  const form = new FormData();
  form.set('templateItemId', templateItemId);
  form.set('expectedReportVersion', String(expectedReportVersion));
  form.set('altText', altText);
  form.set('photo', photo);
  return fetchJson<IqcReport>(`/api/quality/iqc/reports/${reportId}/photos`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function deleteIqcPhoto(
  developmentUserKey: string | undefined,
  reportId: string,
  photoId: string,
  expectedReportVersion: number
) {
  return fetchJson<IqcReport>(
    `/api/quality/iqc/reports/${reportId}/photos/${photoId}?expectedReportVersion=${expectedReportVersion}`,
    developmentUserKey,
    { method: 'DELETE' }
  );
}

export async function uploadIqcScanAttachment(
  developmentUserKey: string | undefined,
  reportId: string,
  expectedReportVersion: number,
  file: File
) {
  const form = new FormData();
  form.set('expectedReportVersion', String(expectedReportVersion));
  form.set('file', file);
  return fetchJson<IqcReport>(`/api/quality/iqc/scan-reports/${reportId}/attachments`, developmentUserKey, {
    method: 'POST',
    body: form
  });
}

export async function deleteIqcScanAttachment(
  developmentUserKey: string | undefined,
  reportId: string,
  attachmentId: string,
  expectedReportVersion: number
) {
  return fetchJson<IqcReport>(
    `/api/quality/iqc/scan-reports/${reportId}/attachments/${attachmentId}?expectedReportVersion=${expectedReportVersion}`,
    developmentUserKey,
    { method: 'DELETE' }
  );
}

export async function getIqcScanAttachmentBlob(
  developmentUserKey: string | undefined,
  reportId: string,
  attachmentId: string
) {
  const response = await fetchWithAuth(
    `/api/quality/iqc/scan-reports/${reportId}/attachments/${attachmentId}/content`,
    developmentUserKey
  );
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
}

export async function finalizeIqcScanReport(
  developmentUserKey: string | undefined,
  reportId: string,
  expectedReportVersion: number,
  expectedReceiptVersion: number,
  result: 'Passed' | 'Failed',
  reason: string
) {
  return fetchJson<IqcReport>(`/api/quality/iqc/scan-reports/${reportId}/finalize`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedReportVersion, expectedReceiptVersion, result, reason })
  });
}

export async function finalizeIqcReport(
  developmentUserKey: string | undefined,
  reportId: string,
  expectedReportVersion: number,
  expectedReceiptVersion: number,
  result: 'Passed' | 'Failed',
  reason: string
) {
  return fetchJson<IqcReport>(`/api/quality/iqc/reports/${reportId}/finalize`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify({ expectedReportVersion, expectedReceiptVersion, result, reason })
  });
}

export async function retryIqcPdf(developmentUserKey: string | undefined, reportId: string) {
  return fetchJson<IqcReport>(`/api/quality/iqc/reports/${reportId}/pdf/retry`, developmentUserKey, { method: 'POST' });
}

export async function downloadIqcPdf(developmentUserKey: string | undefined, reportId: string) {
  const response = await fetchWithAuth(`/api/quality/iqc/reports/${reportId}/pdf`, developmentUserKey);
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  if (!response.headers.get('Content-Type')?.includes('application/pdf')) {
    throw new ApiError(response.status, 'PDF 생성이 아직 완료되지 않았습니다.');
  }
  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? 'iqc-report.pdf'
  };
}

export async function getIqcPhotoBlob(
  developmentUserKey: string | undefined,
  reportId: string,
  photoId: string
) {
  const response = await fetchWithAuth(
    `/api/quality/iqc/reports/${reportId}/photos/${photoId}/content`,
    developmentUserKey
  );
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return response.blob();
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
  signal?: AbortSignal,
  setInstanceId?: string | null
): Promise<ProductionPlanningResponse> {
  const query = setInstanceId ? `?setInstanceId=${encodeURIComponent(setInstanceId)}` : '';
  return fetchJson<ProductionPlanningResponse>(`/api/projects/${projectId}/production-planning${query}`, developmentUserKey, { signal });
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

export async function getProjectDepartmentAssignees(
  developmentUserKey: string | undefined,
  projectId: string,
  signal?: AbortSignal
): Promise<DepartmentAssigneeScopeResponse> {
  return fetchJson<DepartmentAssigneeScopeResponse>(
    `/api/projects/${projectId}/production-planning/department-assignees`,
    developmentUserKey,
    { signal }
  );
}

export async function updateProjectDepartmentAssignees(
  developmentUserKey: string | undefined,
  projectId: string,
  request: UpdateDepartmentAssigneesRequest
): Promise<DepartmentAssigneeScopeResponse> {
  return fetchJson<DepartmentAssigneeScopeResponse>(
    `/api/projects/${projectId}/production-planning/department-assignees`,
    developmentUserKey,
    {
      method: 'PATCH',
      body: JSON.stringify(request)
    }
  );
}

export async function updateProjectProductionPlanSetScope(
  developmentUserKey: string | undefined,
  projectId: string,
  setInstanceId: string,
  request: UpdateProductionPlanSetScopeRequest
): Promise<ProductionPlanningResponse> {
  return fetchJson<ProductionPlanningResponse>(
    `/api/projects/${projectId}/production-planning/set-scopes/${setInstanceId}`,
    developmentUserKey,
    {
      method: 'PATCH',
      body: JSON.stringify(request)
    }
  );
}

export async function updateProjectProductionPlanSetDefault(
  developmentUserKey: string | undefined,
  projectId: string,
  request: UpdateProductionPlanSetDefaultRequest
): Promise<ProductionPlanningResponse> {
  return fetchJson<ProductionPlanningResponse>(
    `/api/projects/${projectId}/production-planning/set-defaults`,
    developmentUserKey,
    {
      method: 'PATCH',
      body: JSON.stringify(request)
    }
  );
}

export async function getProductionControlTemplateCatalog(
  developmentUserKey: string | undefined,
  signal?: AbortSignal
): Promise<ProductionControlTemplateCatalog> {
  return fetchJson<ProductionControlTemplateCatalog>('/api/production-control/templates', developmentUserKey, { signal });
}

export async function ensureProductionControlCurrent(
  developmentUserKey: string | undefined,
  domain: 'manufacturing' | 'planning',
  productTypeId: string,
  expectedActiveRowVersion: number | null
): Promise<ProductionControlTemplateCatalog> {
  return fetchJson<ProductionControlTemplateCatalog>(
    `/api/production-control/templates/${domain}/${productTypeId}/current`,
    developmentUserKey,
    { method: 'POST', body: JSON.stringify({ expectedActiveRowVersion }) }
  );
}

export async function saveProductionControlManufacturingCurrent(
  developmentUserKey: string | undefined,
  productTypeId: string,
  versionId: string,
  expectedRowVersion: number,
  items: ProductionControlManufacturingItem[]
): Promise<ProductionControlTemplateCatalog> {
  return fetchJson<ProductionControlTemplateCatalog>(
    `/api/production-control/templates/manufacturing/${productTypeId}/versions/${versionId}`,
    developmentUserKey,
    { method: 'PUT', body: JSON.stringify({ expectedRowVersion, items }) }
  );
}

export async function saveProductionControlPlanCurrent(
  developmentUserKey: string | undefined,
  productTypeId: string,
  versionId: string,
  expectedRowVersion: number,
  items: ProductionControlPlanItem[]
): Promise<ProductionControlTemplateCatalog> {
  return fetchJson<ProductionControlTemplateCatalog>(
    `/api/production-control/templates/planning/${productTypeId}/versions/${versionId}`,
    developmentUserKey,
    { method: 'PUT', body: JSON.stringify({ expectedRowVersion, items }) }
  );
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

export type ExcelExportDownload = {
  blob: Blob;
  fileName: string;
  rowCount: number;
};

export type SelectedExportScreen =
  | 'projects'
  | 'my-work'
  | 'production-planning'
  | 'procurement'
  | 'material-receipts'
  | 'material-kitting'
  | 'manufacturing'
  | 'material-iqc'
  | 'quality-inspections'
  | 'logistics'
  | 'pending'
  | 'notifications'
  | 'admin-users'
  | 'admin-departments'
  | 'admin-calendar-holidays'
  | 'admin-permissions'
  | 'admin-master-history'
  | 'admin-work-history'
  | 'admin-notification-deliveries'
  | 'admin-notification-preference-audit'
  | 'admin-work-item-escalations'
  | 'admin-audit-events'
  | 'form-templates';

export type SelectedExportColumn = {
  key: string;
  label: string;
  required: boolean;
};

export async function getSelectedExportColumns(
  developmentUserKey: string | undefined,
  screen: SelectedExportScreen
): Promise<SelectedExportColumn[]> {
  return fetchJson<SelectedExportColumn[]>(
    `/api/data-exports/selected/columns?screen=${encodeURIComponent(screen)}`,
    developmentUserKey
  );
}

export async function exportSelectedRowsExcel(
  developmentUserKey: string | undefined,
  screen: SelectedExportScreen,
  ids: readonly string[],
  filters: Record<string, string | undefined> = {},
  columns?: readonly string[]
): Promise<ExcelExportDownload> {
  return downloadExcelExport(
    '/api/data-exports/selected',
    developmentUserKey,
    'EMI_선택.xlsx',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ screen, ids, filters, ...(columns ? { columns } : {}) })
    }
  );
}

export async function listNotices(
  developmentUserKey: string | undefined,
  page = 1,
  pageSize = 20
): Promise<NoticeListResponse> {
  return fetchJson<NoticeListResponse>(
    `/api/notices?page=${encodeURIComponent(page)}&pageSize=${encodeURIComponent(pageSize)}`,
    developmentUserKey
  );
}

export async function getNotice(
  developmentUserKey: string | undefined,
  noticeId: string
): Promise<NoticeDetail> {
  return fetchJson<NoticeDetail>(`/api/notices/${encodeURIComponent(noticeId)}`, developmentUserKey);
}

export async function createNotice(
  developmentUserKey: string | undefined,
  request: CreateNoticeRequest
): Promise<NoticeDetail> {
  return fetchJson<NoticeDetail>('/api/notices', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function updateNotice(
  developmentUserKey: string | undefined,
  noticeId: string,
  request: UpdateNoticeRequest
): Promise<NoticeDetail> {
  return fetchJson<NoticeDetail>(`/api/notices/${encodeURIComponent(noticeId)}`, developmentUserKey, {
    method: 'PUT',
    body: JSON.stringify(request)
  });
}

export async function uploadNoticeAttachment(
  developmentUserKey: string | undefined,
  noticeId: string,
  file: File
): Promise<NoticeAttachment> {
  const form = new FormData();
  form.append('file', file);
  return fetchJson<NoticeAttachment>(
    `/api/notices/${encodeURIComponent(noticeId)}/attachments`,
    developmentUserKey,
    { method: 'POST', body: form }
  );
}

export async function deleteNoticeAttachment(
  developmentUserKey: string | undefined,
  noticeId: string,
  attachmentId: string
): Promise<NoticeAttachmentDeleteResponse> {
  return fetchJson<NoticeAttachmentDeleteResponse>(
    `/api/notices/${encodeURIComponent(noticeId)}/attachments/${encodeURIComponent(attachmentId)}`,
    developmentUserKey,
    { method: 'DELETE' }
  );
}

export async function downloadNoticeAttachment(
  developmentUserKey: string | undefined,
  noticeId: string,
  attachmentId: string,
  fallbackFileName: string
): Promise<NoticeAttachmentDownload> {
  let response: Response;
  try {
    response = await fetchWithAuth(
      `/api/notices/${encodeURIComponent(noticeId)}/attachments/${encodeURIComponent(attachmentId)}/content`,
      developmentUserKey
    );
  } catch (error: unknown) {
    if (isInteractionRequiredAuthError(error)) {
      throw new ApiError(401, '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.');
    }
    throw new ApiError(0, '첨부파일 서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.');
  }
  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }
  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? fallbackFileName
  };
}

export async function deleteNotice(
  developmentUserKey: string | undefined,
  noticeId: string
): Promise<NoticeDeleteResponse> {
  return fetchJson<NoticeDeleteResponse>(`/api/notices/${encodeURIComponent(noticeId)}`, developmentUserKey, {
    method: 'DELETE'
  });
}

async function downloadExcelExport(
  path: string,
  developmentUserKey: string | undefined,
  fallbackFileName: string,
  init?: RequestInit
): Promise<ExcelExportDownload> {
  let response: Response;
  try {
    response = await fetchWithAuth(path, developmentUserKey, init);
  } catch (error: unknown) {
    if (isInteractionRequiredAuthError(error)) {
      throw new ApiError(401, '로그인이 만료되었거나 다시 인증이 필요합니다. Microsoft 365로 다시 로그인해 주세요.');
    }
    throw new ApiError(0, '서버에 연결할 수 없습니다. 서버 실행 상태를 확인해 주세요.');
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  const rowCount = Number(response.headers.get('X-Export-Row-Count'));
  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? fallbackFileName,
    rowCount: Number.isSafeInteger(rowCount) && rowCount >= 0 ? rowCount : -1
  };
}

async function fetchWithAuth(
  path: string,
  developmentUserKey?: string,
  init?: RequestInit,
  options: { includeAdminSwitch?: boolean; includeAuditSession?: boolean } = {}
): Promise<Response> {
  const headers = new Headers(init?.headers);

  if (developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  } else if (accessTokenProvider && !headers.has('Authorization')) {
    const accessToken = await accessTokenProvider();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }

    if (adminTestUserKey && options.includeAdminSwitch !== false) {
      headers.set('X-Qms-Test-User', adminTestUserKey);
    }
  }

  const method = (init?.method ?? 'GET').toUpperCase();
  if (auditSession
    && options.includeAuditSession !== false
    && !['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-Qms-Audit-Correlation', auditSession.loginCorrelationId);
    headers.set('X-Qms-Audit-Receipt', auditSession.idempotencyReceipt);
  }

  return fetch(buildApiUrl(path), { ...init, headers });
}

async function fetchJson<T>(path: string, developmentUserKey?: string, init?: RequestInit): Promise<T> {
  const method = (init?.method ?? 'GET').toUpperCase();
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method) && !mutationAllowed) {
    throw new ApiError(423, '현재 UAT는 검수 전용 읽기 모드입니다. 저장, 삭제, 발송 또는 상태 변경을 수행할 수 없습니다.');
  }

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
    return firstError;
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
