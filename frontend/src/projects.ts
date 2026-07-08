export const maxPanelsPerProject = 500;

export interface ProjectListResponse {
  items: ProjectListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ProjectDashboardSummary {
  totalProjectCount: number;
  activeProjectCount: number;
  onHoldProjectCount: number;
  completedProjectCount: number;
  cancelledProjectCount: number;
  qrEligiblePanelCount: number;
  manufacturingCompletedCount: number;
  inspectionCompletedCount: number;
  manufacturingCompletedProjectCount: number;
  inspectionCompletedProjectCount: number;
}

export interface ProjectExcelPreviewResponse {
  fileSha256: string;
  totalRows: number;
  newCount: number;
  needsReviewCount: number;
  errorCount: number;
  rows: ProjectExcelPreviewRow[];
}

export interface ProjectExcelPreviewRow {
  excelRowNumber: number;
  resultType: 'New' | 'NeedsReview' | 'Error';
  customerName: string | null;
  item: string | null;
  projectCode: string | null;
  projectTitle: string | null;
  panelCount: number | null;
  deliveryDate: string | null;
  packagingMethod: PackagingMethod | null;
  salesAmount: number | null;
  currencyCode: string | null;
  deliveryLocation: string | null;
  fatRequired: boolean | null;
  salesOwnerText: string | null;
  salesOwnerUserId: string | null;
  salesOwnerName: string | null;
  errorMessages: string[];
}

export interface ProjectExcelApplyResponse {
  createdCount: number;
  projectIds: string[];
}

export interface ProjectListItem {
  projectId: string;
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  activePanelCount: number;
  deliveryDate: string;
  salesOwnerUserId: string;
  salesOwnerName: string;
  packagingMethod: PackagingMethod | null;
  deliveryLocation: string | null;
  fatRequired: boolean;
  status: ProjectStatus;
  projectWorkStatus: ProjectWorkStatus;
  projectProgressPercent: number | null;
  createdAt: string;
  updatedAt: string;
  salesAmount?: number;
  currencyCode?: string;
}

export interface ProjectDetail extends ProjectListItem {
  statusReason: string | null;
  panelInfoCompletedCount: number;
  panelInfoPendingCount: number;
  qrEligibleCount: number;
  manufacturingCompletedCount: number;
  inspectionCompletedCount: number;
  duplicatePanelNameGroupCount: number;
  projectPanelInformationCompleted: boolean;
}

export type ProjectStatus = 'Active' | 'OnHold' | 'Cancelled' | 'Completed';
export type ProjectWorkStatus =
  | 'SalesProjectCreated'
  | 'ProductionPlanning'
  | 'DesignPanelInfo'
  | 'ProcurementInfo'
  | 'MaterialArrived'
  | 'IQC'
  | 'ReceiptConfirmed'
  | 'KittingCompleted'
  | 'ManufacturingWork'
  | 'LQC'
  | 'ManufacturingCompleted'
  | 'OQC'
  | 'CustomerInspection'
  | 'FAT'
  | 'PackingCompleted'
  | 'DepartureProcessed'
  | 'DeliveryCompleted'
  | 'SalesSettlementCompleted'
  | 'BeforeManufacturing'
  | 'ManufacturingInProgress'
  | 'InspectionInProgress'
  | 'InspectionCompleted'
  | 'ReadyForShipment'
  | 'ShipmentCompleted'
  | 'OnHold'
  | 'Cancelled'
  | 'Completed';
export type ProjectListTab = 'All' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled' | 'Deleted';
export type PackagingMethod = 'WoodenCrate' | 'StretchWrap' | 'HeavyDutyBox';

export interface AdminDashboardResponse {
  pendingUserCount: number;
  failedDeliveryCount: number;
  pendingDeliveryCount: number;
  lastDailyDigestSentAtUtc: string | null;
  activeEscalationCount: number;
  recentMasterChangeCount: number;
  activeEscalationLevels: AdminDashboardEscalationLevel[];
}

export interface AdminDashboardEscalationLevel {
  level: string;
  label: string;
  count: number;
}

export interface AdminDepartmentListResponse {
  departments: AdminDepartmentMaster[];
}

export interface AdminDepartmentMaster {
  departmentId: string;
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
  userCount: number;
  updatedAtUtc: string | null;
  deletionRequestedAtUtc: string | null;
  scheduledHardDeleteAtUtc: string | null;
  purgeBlockedAtUtc: string | null;
  purgeBlockedReason: string | null;
  preDeleteIsActive: boolean | null;
  lifecycleStatus: string;
  lifecycleStatusLabel: string;
  scheduledHardDeleteLabel: string | null;
}

export interface CreateAdminDepartmentRequest {
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
  reason: string | null;
}

export interface UpdateAdminDepartmentRequest {
  name: string;
  isActive: boolean;
  sortOrder: number;
  reason: string | null;
}

export interface AdminReorderRequest {
  items: Array<{ id: string; sortOrder: number }>;
  reason: string | null;
}

export interface AdminBulkActionRequest {
  ids: string[];
  reason: string | null;
}

export interface AdminBulkActionResponse {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  skippedCount: number;
  items: AdminBulkActionItem[];
}

export interface AdminBulkActionItem {
  id: string;
  status: string;
  message: string;
}

export interface PermissionMatrixResponse {
  roles: PermissionMatrixRole[];
  permissions: PermissionMatrixPermission[];
  assignments: PermissionMatrixAssignment[];
}

export interface PermissionMatrixRole {
  roleId: string;
  code: string;
  name: string;
}

export interface PermissionMatrixPermission {
  permissionId: string;
  code: string;
  name: string;
}

export interface PermissionMatrixAssignment {
  roleId: string;
  permissionId: string;
}

export interface AdminMasterChangeLogListResponse {
  items: AdminMasterChangeLog[];
}

export interface AdminMasterChangeLog {
  changeLogId: string;
  entityType: string;
  entityId: string | null;
  action: string;
  beforeJson: string | null;
  afterJson: string | null;
  reason: string | null;
  changedByUserId: string | null;
  changedByDisplayName: string | null;
  changedAtUtc: string;
}

export interface AdminWorkItemHistoryListResponse {
  items: AdminWorkItemHistory[];
}

export interface AdminWorkItemHistory {
  workItemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  workflowStageCode: string;
  workflowStageName: string;
  title: string;
  status: string;
  assignedUserId: string | null;
  assignedDisplayName: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  cancelledAtUtc: string | null;
  dueDate: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminNotificationDeliveryListResponse {
  items: AdminNotificationDelivery[];
}

export interface AdminNotificationDelivery {
  deliveryId: string;
  notificationId: string | null;
  recipientUserId: string | null;
  projectId: string | null;
  workItemId: string | null;
  channel: string;
  channelLabel: string;
  deliveryType: string;
  deliveryTypeLabel: string;
  status: string;
  statusLabel: string;
  attemptCount: number;
  nextAttemptAtUtc: string | null;
  lastAttemptAtUtc: string | null;
  sentAtUtc: string | null;
  suppressedAtUtc: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  actionGuide: string;
  pendingReason: string | null;
  recipientDisplayName: string | null;
  recipientEmail: string | null;
  recipientEmailMasked: string | null;
  projectTitle: string | null;
  projectCode: string | null;
  workItemTitle: string | null;
  workflowStageName: string | null;
  notificationTitle: string | null;
  notificationMessageSummary: string | null;
  displayMessageSummary: string | null;
  displayTitle: string;
  displayRecipient: string;
  displayProject: string;
  displayRecipientKind: string | null;
  displayChannelTarget: string | null;
  manualNotificationKind: string | null;
  manualNotificationKindLabel: string | null;
  correlationId: string | null;
  linkUrl: string | null;
  adminHandlingStatus: string;
  adminHandlingStatusLabel: string;
  adminHandledAtUtc: string | null;
  adminHandledByUserId: string | null;
  adminHandledByDisplayName: string | null;
  adminHandlingNote: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminNotificationDeliveryActionRequest {
  ids: string[];
  note: string | null;
}

export interface AdminNotificationDeliveryActionResponse {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  skippedCount: number;
  items: AdminNotificationDeliveryActionItem[];
}

export interface AdminNotificationDeliveryActionItem {
  deliveryId: string;
  status: string;
  message: string;
}

export interface AdminManualNotificationSendRequest {
  sendMode: string;
  notificationKind: string;
  projectId: string | null;
  projectSelectionType: string | null;
  title: string;
  projectName: string | null;
  message: string;
  channels: string[];
  teamsActivityRecipientUserIds: string[];
  mailRecipientUserIds: string[];
  mailRecipientEmails: string[];
  workAssigneeUserIds: string[];
  workflowStageCode: string | null;
  dueDate: string | null;
}

export interface AdminManualNotificationSendResponse {
  correlationId: string;
  requestedCount: number;
  queuedCount: number;
  items: AdminManualNotificationSendChannelResult[];
}

export interface AdminManualNotificationSendChannelResult {
  channel: string;
  channelLabel: string;
  deliveryId: string | null;
  status: string;
  errorCode: string | null;
  errorMessage: string | null;
  target: string;
  message: string;
}

export interface AdminNotificationDeliveryDetail {
  deliveryId: string;
  categoryLabel: string;
  notificationKindLabel: string | null;
  projectName: string | null;
  title: string;
  message: string | null;
  manualRequestedAtUtc: string | null;
  createdAtUtc: string;
  channel: string;
  channelLabel: string;
  recipient: string;
  status: string;
  statusLabel: string;
  attemptCount: number;
  nextAttemptAtUtc: string | null;
  lastAttemptAtUtc: string | null;
  sentAtUtc: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  actionGuide: string;
  adminHandlingStatus: string;
  adminHandlingStatusLabel: string;
  adminHandlingNote: string | null;
  correlationId: string | null;
  providerMessageId: string | null;
}

export interface AdminWorkItemEscalationListResponse {
  items: AdminWorkItemEscalation[];
}

export interface AdminWorkItemEscalation {
  escalationId: string;
  workItemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  workflowStageCode: string;
  workflowStageName: string;
  workItemTitle: string;
  dueDate: string;
  status: string;
  currentLevel: string;
  lastEscalatedAtUtc: string | null;
  nextCheckAtUtc: string | null;
  assignedDisplayName: string | null;
  deliveryStatusSummary: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DeletedProjectListResponse {
  items: DeletedProjectListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DeletedProjectListItem extends ProjectListItem {
  deletedAtUtc: string;
  deletedByUserId: string | null;
  deletedByUserName: string | null;
  deleteReason: string;
}

export interface DeletedProjectDetail extends DeletedProjectListItem {
  statusReason: string | null;
  panels: PanelPlaceholder[];
  auditHistory: AuditEvent[];
}

export interface PurgeDeletedProjectsResponse {
  deletedProjectCount: number;
}

export interface PanelPlaceholder {
  panelId: string;
  projectId: string;
  sequenceNumber: number;
  displayCode: string;
  panelName: string | null;
  width: number | null;
  height: number | null;
  depth: number | null;
  panelStatus: 'Active' | 'Cancelled';
  workflowStage: ProductWorkflowStage;
  panelInfoCompleted: boolean;
  qrEligible: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AuditHistoryResponse {
  items: AuditEvent[];
}

export interface AuditEvent {
  auditEventId: string;
  entityType: 'Project' | 'PanelPlaceholder' | 'Panel';
  entityId: string;
  projectId: string;
  action: string;
  panelNumber?: string;
  panelDisplayName?: string;
  displayCode?: string;
  fieldName?: string;
  oldValue?: string;
  newValue?: string;
  reason?: string;
  changedByUserId: string | null;
  changedByUserName: string | null;
  changedAtUtc: string;
  correlationId: string;
  inputSource?: 'Direct' | 'Excel';
  importBatchId?: string;
  inputUnit?: PanelInputUnit;
  originalInputValue?: string;
  importFileName?: string;
  importUploadedAtUtc?: string;
}

export interface SalesOwner {
  userId: string;
  displayName: string;
}

export interface CreateProjectRequest {
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  panelCount: number;
  deliveryDate: string;
  salesOwnerUserId: string;
  packagingMethod: PackagingMethod | null;
  salesAmount: number | null;
  currencyCode: string | null;
  deliveryLocation: string | null;
  fatRequired: boolean;
}

export interface UpdateProjectRequest {
  customerName: string;
  item: string;
  projectCode: string;
  projectTitle: string;
  deliveryDate: string;
  salesOwnerUserId: string;
  packagingMethod: PackagingMethod | null;
  salesAmount: number | null;
  currencyCode: string | null;
  deliveryLocation: string | null;
  fatRequired: boolean;
  reason: string;
}

export interface ChangePanelCountRequest {
  panelCount: number;
  expectedActivePanelCount: number;
  cancelPanelIds: string[];
  reason: string;
}

export interface ProjectStatusChangeRequest {
  reason: string;
}

export interface DeleteProjectRequest {
  reason: string;
  confirmProjectTitle: string;
}

export type PanelInputUnit = 'Mm' | 'Inch';
export type ProductWorkflowStage =
  | 'BeforeManufacturing'
  | 'ManufacturingInProgress'
  | 'ManufacturingCompleted'
  | 'InspectionInProgress'
  | 'InspectionCompleted'
  | 'PackingCompleted'
  | 'ShipmentCompleted';

export interface PanelInformationResponse {
  projectId: string;
  projectStatus: ProjectStatus;
  packagingMethod: PackagingMethod | null;
  activePanelCount: number;
  panelInfoCompletedCount: number;
  panelInfoPendingCount: number;
  qrEligibleCount: number;
  manufacturingCompletedCount: number;
  inspectionCompletedCount: number;
  duplicatePanelNameGroupCount: number;
  projectPanelInformationCompleted: boolean;
  panelInformationStatusMessage: string | null;
  panels: PanelInformationPanel[];
}

export interface PanelInformationPanel {
  panelId: string;
  projectId: string;
  sequenceNumber: number;
  panelNumber: string;
  displayCode: string;
  panelName: string | null;
  displayName: string;
  widthMm: number | null;
  heightMm: number | null;
  depthMm: number | null;
  panelStatus: 'Active' | 'Cancelled';
  workflowStage: ProductWorkflowStage;
  panelInfoCompleted: boolean;
  qrEligible: boolean;
  hasDuplicateName: boolean;
  duplicateNameCount: number;
  panelInfoVersion: number;
  createdAt: string;
  updatedAt: string;
  panelInfoUpdatedAtUtc: string | null;
  panelInfoUpdatedByUserId: string | null;
  panelInfoUpdatedByUserName: string | null;
}

export interface PanelInformationBulkUpdateRequest {
  reason: string | null;
  panels: PanelInformationUpdateItemRequest[];
}

export interface PanelInformationUpdateItemRequest {
  panelId: string;
  expectedPanelInfoVersion: number;
  panelNameUpdate?: {
    isChanged: boolean;
    value: string | null;
  };
  sizeUpdate?: {
    isChanged: boolean;
    clear: boolean;
    inputUnit: PanelInputUnit | null;
    width: number | null;
    height: number | null;
    depth: number | null;
  };
}

export interface PanelInformationExcelExpectedVersion {
  panelId: string;
  expectedPanelInfoVersion: number;
}

export interface PanelInformationLegacySizeFields {
  width: number | null;
  height: number | null;
  depth: number | null;
}

export interface PanelInformationHistoryResponse {
  groups: PanelInformationHistoryGroup[];
  auditEvents: AuditEvent[];
  excelImportBatches: PanelInformationExcelImportBatch[];
}

export interface PanelInformationHistoryGroup {
  groupId: string;
  actionType: string;
  inputSource?: 'Direct' | 'Excel';
  changedByUserId: string | null;
  changedByName: string | null;
  changedAtUtc: string;
  reason?: string;
  importBatchId?: string;
  importFileName?: string;
  importUploadedAtUtc?: string;
  affectedPanelCount: number;
  changeCount: number;
  changes: PanelInformationHistoryChange[];
}

export interface PanelInformationHistoryChange {
  entityType: 'Project' | 'PanelPlaceholder' | 'Panel';
  entityId: string;
  panelNumber?: string;
  panelDisplayName?: string;
  displayCode?: string;
  fieldName?: string;
  oldValue?: string;
  newValue?: string;
  inputUnit?: PanelInputUnit;
  originalInputValue?: string;
}

export interface PanelInformationExcelImportBatch {
  importBatchId: string;
  projectId: string;
  originalFileName: string;
  fileSizeBytes: number;
  fileSha256: string;
  inputUnit: PanelInputUnit | null;
  totalRowCount: number;
  newPanelCount: number;
  changedPanelCount: number;
  unchangedPanelCount: number;
  skippedPanelCount: number;
  uploadedByUserId: string | null;
  uploadedByUserName: string | null;
  uploadedAtUtc: string;
  reason: string | null;
}

export interface PanelInformationExcelPreviewResponse {
  fileSha256: string;
  expectedPackagingMethod: PackagingMethod | null;
  expectedProjectStatus: ProjectStatus;
  totalRows: number;
  newCount: number;
  changedCount: number;
  unchangedCount: number;
  skippedCount: number;
  errorCount: number;
  reasonRequired: boolean;
  expectedPanelInfoVersions: PanelInformationExcelExpectedVersion[];
  rows: PanelInformationExcelPreviewRow[];
}

export interface PanelInformationExcelPreviewRow {
  excelRowNumber: number;
  no: number | null;
  panelId: string | null;
  panelName: string | null;
  width: number | null;
  height: number | null;
  depth: number | null;
  widthMm: number | null;
  heightMm: number | null;
  depthMm: number | null;
  currentValue: PanelInformationPanel | null;
  resultType: 'New' | 'Changed' | 'Unchanged' | 'Skipped' | 'Error';
  errorMessages: string[];
  expectedPanelInfoVersion: number | null;
}

export interface ProcurementResponse {
  projectId: string;
  projectTitle: string;
  projectCode: string;
  projectDeliveryDate: string | null;
  items: ProcurementItem[];
}

export interface ProcurementItem {
  itemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  projectDeliveryDate: string | null;
  shipmentDisplayDate: string | null;
  sequenceNumber: number;
  sourceProjectText: string | null;
  sourceProjectCodeText: string | null;
  standardLeadTime: string | null;
  orderItem: string | null;
  supplierName: string | null;
  technicalOwner: string | null;
  orderDate: string | null;
  expectedReceiptDate: string | null;
  issueNote: string | null;
  receiptCompleted: boolean;
  receiptCompletedAtUtc: string | null;
  receiptCompletedByUserId: string | null;
  receiptCompletedByUserName: string | null;
  receiptCompletionNote: string | null;
  rowVersion: number;
  dDayText: string;
}

export interface ProcurementBulkUpdateRequest {
  reason: string | null;
  items: ProcurementItemUpdateRequest[];
}

export interface ProcurementItemUpdateRequest {
  itemId: string | null;
  expectedRowVersion: number | null;
  standardLeadTime: string | null;
  orderItem: string | null;
  supplierName: string | null;
  technicalOwner: string | null;
  orderDate: string | null;
  expectedReceiptDate: string | null;
  issueNote: string | null;
  receiptCompleted: boolean | null;
  receiptCompletedAtUtc: string | null;
  receiptCompletionNote: string | null;
}

export interface ProcurementReceiptBulkUpdateRequest {
  reason: string | null;
  items: ProcurementReceiptUpdateRequest[];
}

export interface ProcurementReceiptUpdateRequest {
  itemId: string;
  expectedRowVersion: number;
  receiptCompleted: boolean;
  receiptCompletedAtUtc: string | null;
  receiptCompletionNote: string | null;
}

export interface ProcurementExcelProjectSelection {
  sourceGroupSequence: number;
  projectId: string;
}

export interface ProcurementExcelExpectedVersion {
  itemId: string;
  expectedRowVersion: number;
}

export interface ProcurementExcelPreviewResponse {
  fileSha256: string;
  totalRows: number;
  newCount: number;
  changedCount: number;
  unchangedCount: number;
  skippedCount: number;
  missingFromUploadCount: number;
  needsReviewCount: number;
  errorCount: number;
  reasonRequired: boolean;
  projectMatches: ProcurementExcelProjectMatch[];
  rows: ProcurementExcelPreviewRow[];
  expectedVersions: ProcurementExcelExpectedVersion[];
}

export interface ProcurementExcelProjectMatch {
  sourceGroupSequence: number;
  excelProjectTitle: string | null;
  excelProjectCode: string | null;
  matchedProjectId: string | null;
  matchedProjectTitle: string | null;
  matchedProjectCode: string | null;
  matchStatus: 'Matched' | 'NeedsReview' | 'Unmatched' | 'Error';
  candidates: ProcurementProjectCandidate[];
}

export interface ProcurementProjectCandidate {
  projectId: string;
  projectTitle: string;
  projectCode: string;
  matchType: string;
}

export interface ProcurementExcelPreviewRow {
  excelRowNumber: number;
  sourceGroupSequence: number;
  projectId: string | null;
  itemId: string | null;
  expectedRowVersion: number | null;
  resultType: 'New' | 'Changed' | 'Unchanged' | 'Skipped' | 'MissingFromUpload' | 'NeedsReview' | 'Error';
  sourceProjectText: string | null;
  sourceProjectCodeText: string | null;
  standardLeadTime: string | null;
  orderItem: string | null;
  supplierName: string | null;
  technicalOwner: string | null;
  orderDate: string | null;
  expectedReceiptDate: string | null;
  shipmentText: string | null;
  issueNote: string | null;
  receiptCompleted: boolean | null;
  errorMessages: string[];
}

export interface ProcurementHistoryResponse {
  groups: ProcurementHistoryGroup[];
  excelImportBatches: ProcurementExcelImportBatch[];
}

export interface ProcurementHistoryGroup {
  groupId: string;
  inputSource: 'Direct' | 'Excel';
  changedByUserId: string | null;
  changedByName: string | null;
  changedAtUtc: string;
  reason?: string;
  importBatchId?: string;
  importFileName?: string;
  affectedItemCount: number;
  changeCount: number;
  changes: ProcurementHistoryChange[];
}

export interface ProcurementHistoryChange {
  entityId: string;
  sequenceNumber: number | null;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
}

export interface ProcurementExcelImportBatch {
  importBatchId: string;
  originalFileName: string;
  fileSizeBytes: number;
  fileSha256: string;
  totalRowCount: number;
  newItemCount: number;
  changedItemCount: number;
  unchangedItemCount: number;
  skippedItemCount: number;
  missingFromUploadCount: number;
  uploadedByUserId: string | null;
  uploadedByUserName: string | null;
  uploadedAtUtc: string;
  reason: string | null;
}

export interface ProcurementListResponse {
  items: ProcurementItem[];
}

export interface ProcurementDashboardResponse {
  summary: ProcurementDashboardSummary;
  projects: ProcurementProjectSummary[];
}

export interface ProcurementDashboardSummary {
  pendingReceiptCount: number;
  receiptCompletedCount: number;
  pastExpectedReceiptDateCount: number;
}

export interface ProcurementProjectSummary {
  projectId: string;
  projectTitle: string;
  customerName: string;
  projectCode: string;
  item: string;
  activePanelCount: number;
  deliveryDate: string | null;
  procurementItemCount: number;
  receiptCompletedCount: number;
  pastExpectedReceiptDateCount: number;
  nearestExpectedReceiptDate: string | null;
  dDayText: string;
}

export interface ProductionPlanningSummary {
  notPlannedCount: number;
  planningCount: number;
  plannedCount: number;
  missingAssigneeProjectCount: number;
}

export interface ProductionPlanningProjectListResponse {
  projects: ProductionPlanningProjectSummary[];
}

export interface ProductionPlanningProjectSummary {
  projectId: string;
  projectTitle: string;
  customerName: string;
  projectCode: string;
  item: string;
  activePanelCount: number;
  deliveryDate: string | null;
  projectStatus: ProjectStatus;
  planStatus: ProductionPlanStatus;
  planStatusLabel: string;
  productTypeCode: string | null;
  productTypeName: string | null;
  requiredStepCount: number;
  plannedRequiredStepCount: number;
  assigneeCount: number;
}

export interface ProductionPlanningResponse {
  projectId: string;
  projectTitle: string;
  projectCode: string;
  deliveryDate: string | null;
  planId: string | null;
  rowVersion: number;
  planStatus: ProductionPlanStatus;
  planStatusLabel: string;
  productTypeId: string | null;
  templateId: string | null;
  productTypeCode: string | null;
  productTypeName: string | null;
  notes: string | null;
  items: ProductionPlanItem[];
  assignees: ProjectAssignee[];
  assigneeCandidates: AssigneeCandidate[];
  fallbacks: NotificationFallback[];
}

export type ProductionPlanStatus = 'NotPlanned' | 'Planning' | 'Planned';

export interface ProductionPlanItem {
  itemId: string | null;
  templateStepId: string | null;
  sequenceNumber: number;
  stepName: string;
  isRequired: boolean;
  isCustom: boolean;
  plannedDate: string | null;
  note: string | null;
  rowVersion: number;
}

export interface ProjectAssignee {
  assigneeId: string | null;
  responsibilityType: ResponsibilityType;
  responsibilityLabel: string;
  assignedUserId: string | null;
  assignedUserName: string | null;
  note: string | null;
  rowVersion: number;
}

export type ResponsibilityType =
  | 'SalesPrimary'
  | 'SalesSecondary'
  | 'DesignPrimary'
  | 'DesignSecondary'
  | 'ProductionPlanningPrimary'
  | 'ProductionPlanningSecondary'
  | 'ProcurementPrimary'
  | 'ProcurementSecondary'
  | 'MaterialsPrimary'
  | 'MaterialsSecondary'
  | 'ManufacturingPrimary'
  | 'ManufacturingSecondary'
  | 'LogisticsPrimary'
  | 'LogisticsSecondary'
  | 'QualityIQC'
  | 'QualityIQCSecondary'
  | 'QualityLQC'
  | 'QualityLQCSecondary'
  | 'QualityOQC'
  | 'QualityOQCSecondary'
  | 'QualityCustomerInspection'
  | 'QualityCustomerInspectionSecondary';

export interface AssigneeCandidate {
  responsibilityType: ResponsibilityType;
  users: UserOption[];
}

export interface UserOption {
  userId: string;
  displayName: string;
}

export interface NotificationFallback {
  responsibilityType: ResponsibilityType;
  responsibilityLabel: string;
  userId: string | null;
  displayName: string | null;
  sourceLabel: string;
}

export interface ProductionProductType {
  productTypeId: string;
  code: string;
  name: string;
  isActive: boolean;
  activeTemplateId: string | null;
  activeTemplateVersion: number | null;
  steps: ProductionTemplateStep[];
}

export interface ProductionTemplateSettings {
  productTypeId: string;
  code: string;
  name: string;
  activeTemplateId: string;
  activeTemplateVersion: number;
  steps: ProductionTemplateSettingsStep[];
}

export interface ProductionTemplateSettingsStep {
  templateStepId: string | null;
  sequenceNumber: number;
  stepName: string;
  isRequired: boolean;
  isActive: boolean;
}

export interface SystemHoliday {
  holidayDate: string;
  name: string;
  countryCode: string;
  source: string;
  holidayType: string;
}

export interface BusinessCalendarResponse {
  from: string;
  to: string;
  countryCode: string;
  days: BusinessCalendarDay[];
}

export interface BusinessCalendarDay {
  date: string;
  isWeekend: boolean;
  isHoliday: boolean;
  isCompanyHoliday: boolean;
  isBusinessDay: boolean;
  holidayName: string | null;
  holidayType: string | null;
}

export type HolidayType = 'National' | 'Substitute' | 'Temporary' | 'Company';

export interface AdminCalendarHoliday {
  holidayId: string;
  date: string;
  name: string;
  countryCode: string;
  holidayType: HolidayType;
  isActive: boolean;
  note: string | null;
  source: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  deletionRequestedAtUtc: string | null;
  scheduledHardDeleteAtUtc: string | null;
  purgeBlockedAtUtc: string | null;
  purgeBlockedReason: string | null;
  preDeleteIsActive: boolean | null;
  lifecycleStatus: string;
  lifecycleStatusLabel: string;
  scheduledHardDeleteLabel: string | null;
}

export interface AdminCalendarHolidayListResponse {
  year: number;
  countryCode: string;
  holidays: AdminCalendarHoliday[];
}

export interface UpsertAdminCalendarHolidayRequest {
  date: string;
  name: string;
  holidayType: HolidayType;
  isActive: boolean;
  note: string | null;
}

export interface CalendarHolidayExcelPreviewResponse {
  fileSha256: string;
  totalRows: number;
  saveableCount: number;
  insertCount: number;
  updateCount: number;
  errorCount: number;
  rows: CalendarHolidayExcelPreviewRow[];
}

export interface CalendarHolidayExcelPreviewRow {
  excelRowNumber: number;
  date: string | null;
  name: string | null;
  holidayType: HolidayType | null;
  note: string | null;
  resultType: 'Insert' | 'Update' | 'Error';
  existingHolidayId: string | null;
  errorMessages: string[];
}

export interface CalendarHolidayExcelApplyResponse {
  insertedCount: number;
  updatedCount: number;
  skippedCount: number;
  holidayIds: string[];
}

export interface UpdateProductionTemplateSettingsRequest {
  steps: ProductionTemplateSettingsStep[];
  reason: string | null;
}

export interface ProductionTemplateStep {
  templateStepId: string;
  sequenceNumber: number;
  stepName: string;
  isRequired: boolean;
}

export interface UpdateProductionPlanningRequest {
  productTypeId: string | null;
  expectedRowVersion: number;
  notes: string | null;
  reason: string | null;
  items: ProductionPlanItemUpdateRequest[];
  assignees: ProjectAssigneeUpdateRequest[];
}

export interface ProductionPlanItemUpdateRequest {
  itemId: string | null;
  templateStepId: string | null;
  stepName: string | null;
  sequenceNumber: number;
  isRequired: boolean;
  expectedRowVersion: number;
  plannedDate: string | null;
  note: string | null;
  isDeleted: boolean;
}

export interface ProjectAssigneeUpdateRequest {
  responsibilityType: ResponsibilityType;
  assigneeId: string | null;
  expectedRowVersion: number;
  assignedUserId: string | null;
  note: string | null;
}

export interface CreateProductionProductTypeRequest {
  code: string;
  name: string;
  steps: Array<{
    sequenceNumber: number;
    stepName: string;
    isRequired: boolean;
  }>;
}

export interface ProductionPlanningExcelPreviewResponse {
  fileSha256: string;
  totalRows: number;
  saveableCount: number;
  blockedCount: number;
  rows: ProductionPlanningExcelPreviewRow[];
}

export interface ProductionPlanningExcelPreviewRow {
  excelRowNumber: number;
  resultType: 'New' | 'Changed' | 'Unchanged' | 'CustomStep' | 'NeedsReview' | 'Error' | 'Skipped';
  projectId: string | null;
  projectTitle: string | null;
  projectCode: string | null;
  productTypeId: string | null;
  productTypeCode: string | null;
  templateStepId: string | null;
  stepName: string | null;
  isCustomStep: boolean;
  isRequired: boolean | null;
  plannedDate: string | null;
  note: string | null;
  procurementAssigneeText: string | null;
  productionPlanningAssigneeText: string | null;
  manufacturingAssigneeText: string | null;
  qualityAssigneeText: string | null;
  logisticsAssigneeText: string | null;
  errorMessages: string[];
}

export interface ProductionPlanningExcelApplyResponse {
  appliedRowCount: number;
  blockedRowCount: number;
  projectIds: string[];
}

export interface ProductionPlanningHistoryResponse {
  groups: ProductionPlanningHistoryGroup[];
}

export interface ProductionPlanningHistoryGroup {
  groupId: string;
  inputSource: string;
  changedByUserId: string | null;
  changedByName: string | null;
  changedAtUtc: string;
  reason: string | null;
  affectedItemCount: number;
  changeCount: number;
  changes: ProductionPlanningHistoryChange[];
}

export interface ProductionPlanningHistoryChange {
  entityId: string;
  entityType: 'ProductionPlan' | 'ProductionPlanItem' | 'ProjectAssignee';
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
}

export interface WorkflowStage {
  stageCode: string;
  sequenceNumber: number;
  departmentCode: string;
  departmentLabel: string;
  stageName: string;
  isOptional: boolean;
  isActive: boolean;
}

export interface ProjectWorkflowResponse {
  projectId: string;
  stages: ProjectWorkflowStage[];
  generatedWorkItemCount: number;
  requiredStageCount: number;
  completedRequiredStageCount: number;
  progressPercent: number;
  currentStageCode: string;
  currentStageName: string;
  currentDepartmentCode: string;
  currentDepartmentLabel: string;
}

export interface ProjectWorkflowStage {
  stageCode: string;
  sequenceNumber: number;
  departmentCode: string;
  departmentLabel: string;
  stageName: string;
  isOptional: boolean;
  status: string;
  statusLabel: string;
  workItemCount: number;
  completedAtUtc: string | null;
}

export interface MyWorkSummary {
  requestedCount: number;
  inProgressCount: number;
  completedCount: number;
  blockingCount: number;
  assignedProjectCount: number;
  assignedProjectBreakdown: MyAssignedProjectBreakdown[];
}

export interface MyAssignedProjectBreakdown {
  responsibilityType: string;
  responsibilityLabel: string;
  projectCount: number;
}

export interface MyWorkListResponse {
  items: MyWorkItem[];
}

export interface MyAssignedProjectsResponse {
  items: MyAssignedProject[];
}

export interface MyAssignedProject {
  projectId: string;
  projectTitle: string;
  projectCode: string;
  item: string;
  deliveryDate: string | null;
  projectStatus: ProjectStatus;
  projectStatusLabel: string;
  responsibilities: MyAssignedProjectResponsibility[];
}

export interface MyAssignedProjectResponsibility {
  responsibilityType: string;
  responsibilityLabel: string;
}

export interface MyWorkItem {
  workItemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  projectItem: string;
  projectDeliveryDate: string | null;
  workflowStageCode: string;
  workflowStageName: string;
  responsibilityType: string;
  responsibilityLabel: string;
  title: string;
  description: string | null;
  status: string;
  statusLabel: string;
  priority: string;
  priorityLabel: string;
  dueDate: string | null;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  linkUrl: string;
}

export interface NotificationSummary {
  unreadCount: number;
  blockingCount: number;
}

export interface NotificationListResponse {
  items: NotificationItem[];
}

export interface NotificationItem {
  notificationId: string;
  projectId: string | null;
  projectTitle: string | null;
  projectCode: string | null;
  projectItem: string | null;
  workItemId: string | null;
  workItemTitle: string | null;
  workflowStageCode: string | null;
  workflowStageName: string | null;
  notificationType: string;
  notificationTypeLabel: string;
  severity: string;
  severityLabel: string;
  visibilityScope: string;
  visibilityScopeLabel: string;
  sourceKind: string;
  sourceKindLabel: string;
  title: string;
  message: string;
  linkUrl: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface ProcurementRequiredItemSettings {
  itemCode: string;
  activeTemplateId: string | null;
  activeTemplateVersion: number | null;
  rows: ProcurementRequiredItemSettingsRow[];
}

export interface ProcurementRequiredItemSettingsRow {
  templateRowId: string | null;
  sequenceNumber: number;
  itemName: string;
  isRequired: boolean;
  isActive: boolean;
}

export interface UpdateProcurementRequiredItemSettingsRequest {
  rows: ProcurementRequiredItemSettingsRow[];
  reason: string | null;
}
