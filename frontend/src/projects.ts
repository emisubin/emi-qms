export const maxPanelsPerProject = 500;

export interface ProjectListResponse {
  items: ProjectListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
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
  status: ProjectStatus;
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
  duplicatePanelNameGroupCount: number;
  projectPanelInformationCompleted: boolean;
}

export type ProjectStatus = 'Active' | 'OnHold' | 'Cancelled' | 'Completed';
export type ProjectListTab = 'Active' | 'OnHold' | 'Completed' | 'Cancelled' | 'Deleted';
export type PackagingMethod = 'WoodenCrate' | 'StretchWrap' | 'HeavyDutyBox';

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

export interface PanelInformationResponse {
  projectId: string;
  projectStatus: ProjectStatus;
  packagingMethod: PackagingMethod | null;
  activePanelCount: number;
  panelInfoCompletedCount: number;
  panelInfoPendingCount: number;
  qrEligibleCount: number;
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
  auditEvents: AuditEvent[];
  excelImportBatches: PanelInformationExcelImportBatch[];
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
  resultType: 'New' | 'Changed' | 'Unchanged' | 'Error';
  errorMessages: string[];
  expectedPanelInfoVersion: number | null;
}
