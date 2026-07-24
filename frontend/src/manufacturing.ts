export type ManufacturingStatus = 'Ready' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';

export interface ManufacturingQueueResponse {
  projects: ManufacturingProject[];
}

export interface ManufacturingReleaseQueueResponse {
  projects: ManufacturingReleaseProject[];
}

export interface ManufacturingReleaseProject {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  activeItemCount: number;
  completedItemCount: number;
  panels: ManufacturingReleasePanel[];
}

export interface ManufacturingReleasePanel {
  panelId: string;
  displayCode: string;
  panelName: string | null;
  panelInfoCompleted: boolean;
  kittingCompleted: boolean;
  released: boolean;
  workItemStatus: string | null;
  releasedAtUtc: string | null;
  selectable: boolean;
}

export interface ManufacturingReleaseResponse {
  operationId: string;
  releasedPanelCount: number;
  generatedWorkItemCount: number;
  replayed: boolean;
}

export interface ManufacturingProject {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  readyCount: number;
  inProgressCount: number;
  blockedCount: number;
  completedCount: number;
  panels: ManufacturingPanel[];
}

export interface ManufacturingPanel {
  panelId: string;
  displayCode: string;
  panelName: string | null;
  workflowStage: string;
  kittingCompleted: boolean;
  workItemId: string;
  workItemStatus: string;
  executionId: string | null;
  status: ManufacturingStatus;
  version: number;
  checkedStepCount: number;
  totalStepCount: number;
  activePendingId: string | null;
  activePendingNumber: number | null;
  actionDepartmentCode: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  canMutate: boolean;
}

export interface ManufacturingExecutionDetail {
  panel: ManufacturingPanel;
  steps: ManufacturingStep[];
  events: ManufacturingEvent[];
}

export interface ManufacturingStep {
  stepId: string;
  sequenceNumber: number;
  stepName: string;
  checked: boolean;
  checkedByDisplayName: string | null;
  checkedAtUtc: string | null;
}

export interface ManufacturingEvent {
  eventId: string;
  eventType: string;
  eventLabel: string;
  stopReasonCode: string | null;
  stopDescription: string | null;
  pendingId: string | null;
  actorDisplayName: string;
  createdAtUtc: string | null;
}

export interface ManufacturingActionDepartment {
  departmentCode: string;
  departmentName: string;
  assignees: ManufacturingActionOwner[];
}

export interface ManufacturingActionOwner {
  userId: string;
  displayName: string;
}

export interface ManufacturingMutationResponse {
  operationId: string;
  projectId: string;
  panelId: string;
  executionId: string;
  status: ManufacturingStatus;
  version: number;
  checkedStepCount: number;
  totalStepCount: number;
  pendingId: string | null;
  pendingNumber: number | null;
  panelLqcWorkCreated: boolean;
  projectManufacturingCompleted: boolean;
  replayed: boolean;
}

export interface StopManufacturingRequest {
  operationId: string;
  reasonCode: 'Material' | 'Staffing' | 'WorkUnavailable' | 'Other';
  description: string;
  actionDepartmentCode: string;
  assigneeUserId: string | null;
  expectedVersion: number;
}
