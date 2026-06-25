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
  entityType: 'Project' | 'PanelPlaceholder';
  entityId: string;
  projectId: string;
  action: string;
  fieldName?: string;
  oldValue?: string;
  newValue?: string;
  reason?: string;
  changedByUserId: string | null;
  changedByUserName: string | null;
  changedAtUtc: string;
  correlationId: string;
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
