export type QualityInspectionStage = 'LQC' | 'OQC' | 'CustomerInspection' | 'FAT';
export type QualityInspectionStatus = 'Ready' | 'Requested' | 'InProgress' | 'Passed' | 'Failed' | 'Completed';
export type QualityCheckResult = 'Pass' | 'Fail' | 'NotApplicable';

export interface QualityInspectionQueueResponse {
  projects: QualityInspectionProject[];
  isOperational?: boolean;
  operationalMessage?: string | null;
}

export interface QualityInspectionReconciliationResponse {
  recoveredLqcHandoffCount: number;
  recoveredOqcHandoffCount: number;
  recoveredInspectionHandoffCount: number;
  recoveredPackingHandoffCount: number;
  unresolvedAssigneeCount: number;
}

export interface QualityInspectionProject {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  fatRequired: boolean;
  readyCount: number;
  inProgressCount: number;
  blockedCount: number;
  completedCount: number;
  panels: QualityInspectionPanel[];
}

export interface QualityInspectionPanel {
  panelId: string;
  displayCode: string;
  panelName: string | null;
  workflowStage: string;
  stageCode: string;
  stageLabel: string;
  workItemId: string;
  workItemStatus: string;
  attemptId: string | null;
  attemptNumber: number;
  status: QualityInspectionStatus | 'Confirmed';
  version: number;
  pendingId: string | null;
  pendingNumber: number | null;
  actionDepartmentCode: string | null;
  canMutate: boolean;
}

export interface QualityInspectionDetail {
  panel: QualityInspectionPanel;
  decisionMode: 'Checklist' | 'Aggregate';
  reportId: string | null;
  reportStatus: string | null;
  reportVersion: number | null;
  result: 'Passed' | 'Failed' | null;
  reason: string | null;
  pdfStatus: 'Pending' | 'Ready' | 'Failed' | null;
  items: QualityInspectionTemplateItem[];
  responses: QualityInspectionItemValue[];
  photos: QualityInspectionPhoto[];
  history: QualityInspectionAttemptHistory[];
  reinspectionEvidence?: ReinspectionEvidence | null;
}

export interface QualityInspectionTemplateItem {
  itemId: string;
  itemCode: string;
  displayOrder: number;
  label: string;
  guidance: string | null;
  responseType: 'Check' | 'Text';
  isRequired: boolean;
  requiresPhoto?: boolean;
  maxTextLength: number | null;
  isAvailable: boolean;
  availabilityMessage: string | null;
  isReinspectionTarget: boolean;
  previousFailureEvidence: string | null;
}

export interface QualityInspectionItemValue {
  templateItemId: string;
  checkResult: QualityCheckResult | null;
  textValue: string | null;
  note: string | null;
}

export interface QualityInspectionPhoto {
  photoId: string;
  templateItemId: string;
  displayName: string;
  normalizedMime: string;
  byteSize: number;
  altText: string;
  createdAtUtc: string;
}

export interface QualityInspectionAttemptHistory {
  attemptId: string;
  attemptNumber: number;
  status: string;
  pendingId: string | null;
  pendingNumber: number | null;
  completedAtUtc: string | null;
}

export interface QualityActionDepartment {
  departmentCode: string;
  departmentName: string;
  assignees: QualityActionOwner[];
}

export interface QualityActionOwner {
  userId: string;
  displayName: string;
}

export interface QualityInspectionMutationResponse {
  operationId: string;
  projectId: string;
  panelId: string;
  stageCode: string;
  attemptId: string | null;
  reportId: string | null;
  status: string;
  version: number;
  pendingId: string | null;
  pendingNumber: number | null;
  nextStageCode: string | null;
  replayed: boolean;
}

export interface StartQualityInspectionRequest {
  operationId: string;
  projectId: string;
  panelId: string;
  stageCode: QualityInspectionStage;
}

export interface SaveQualityInspectionResponsesRequest {
  operationId: string;
  expectedReportVersion: number;
  responses: Array<{
    templateItemId: string;
    checkResult: QualityCheckResult | null;
    textValue: string | null;
    note: string | null;
  }>;
}

export interface FinalizeQualityInspectionRequest {
  operationId: string;
  expectedReportVersion: number;
  result: 'Passed' | 'Failed';
  reason: string;
  actionDepartmentCode: string | null;
  assigneeUserId: string | null;
  responses: QualityInspectionItemValue[] | null;
}
import type { ReinspectionEvidence } from './pending';
