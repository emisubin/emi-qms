export type PendingIssueType = string;
export type PendingStatus = 'Registered' | 'ActionRequested' | 'InProgress' | 'ReinspectionRequested' | 'Closed';
export type PendingPriority = 'Normal' | 'Urgent';

export interface PendingSummary {
  openCount: number;
  urgentCount: number;
  overdueCount: number;
  reinspectionCount: number;
  closedCount: number;
}

export interface PendingListResponse {
  summary: PendingSummary;
  items: PendingIssue[];
}

export interface PendingIssue {
  pendingId: string;
  issueNumber: number;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  targetType: string;
  targetId: string;
  targetLabel: string | null;
  issueType: PendingIssueType;
  issueTypeLabel: string;
  title: string;
  description: string;
  status: PendingStatus;
  statusLabel: string;
  priority: PendingPriority;
  priorityLabel: string;
  actionDepartmentCode: string | null;
  assigneeUserId: string | null;
  assigneeDisplayName: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  version: number;
  createdByUserId: string;
  createdByDisplayName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PendingDetail {
  issue: PendingIssue;
  comments: PendingComment[];
  history: PendingHistory[];
  allowedTransitions: PendingStatus[];
  canComment: boolean;
  canAssign: boolean;
  reinspection?: PendingReinspection | null;
  // Kept optional at the client boundary so a mixed-version Development
  // handover cannot blank the rest of an otherwise valid Pending detail.
  actionEvidence?: PendingActionEvidence;
}

export interface PendingActionEvidence {
  canManageDraft: boolean;
  maxPhotosPerRound: number;
  maxBytesPerRound: number;
  maxPhotosPerPending: number;
  remainingPhotosThisRound: number;
  remainingBytesThisRound: number;
  remainingPhotosForPending: number;
  draftPhotos: PendingActionPhoto[] | null;
  confirmedRounds: PendingActionPhotoRound[];
}

export interface PendingActionPhotoRound {
  actionRound: number;
  actionReasonSnapshot: string;
  confirmedByUserId: string;
  confirmedByDisplayName: string;
  confirmedAtUtc: string;
  photos: PendingActionPhoto[];
}

export interface PendingActionPhoto {
  photoId: string;
  displayName: string;
  normalizedMime: 'image/jpeg' | 'image/png';
  byteSize: number;
  altText: string;
  createdByUserId: string;
  createdByDisplayName: string;
  createdAtUtc: string;
}

export interface EvidencePhotoReference {
  sourceKind: 'PanelQualityReport' | 'IqcReport' | 'PendingAction';
  sourceId: string;
  photoId: string;
  displayName: string;
  normalizedMime: 'image/jpeg' | 'image/png';
  byteSize: number;
  altText: string;
}

export interface PendingActionRoundEvidence {
  actionRound: number;
  actionReasonSnapshot: string;
  confirmedByDisplayName: string;
  confirmedAtUtc: string;
  photos: EvidencePhotoReference[];
}

export interface ReinspectionEvidence {
  originalFailurePhotos: EvidencePhotoReference[];
  latestActionRound: PendingActionRoundEvidence | null;
}

export interface PendingPhotoMutationResponse {
  operationId: string;
  resultingPendingVersion: number;
  photoId: string | null;
  replayed: boolean;
  detail: PendingDetail;
}

export interface PendingReinspection {
  attemptId: string;
  attemptNumber: number;
  orderItem: string | null;
  quantity: number | null;
  unit: string | null;
  linkUrl: string;
}

export interface PendingComment {
  commentId: string;
  body: string;
  createdByUserId: string;
  createdByDisplayName: string;
  createdAtUtc: string;
}

export interface PendingHistory {
  historyId: string;
  eventType: string;
  eventLabel: string;
  fromStatus: PendingStatus | null;
  fromStatusLabel: string | null;
  toStatus: PendingStatus | null;
  toStatusLabel: string | null;
  fromAssigneeDisplayName: string | null;
  toAssigneeDisplayName: string | null;
  reason: string | null;
  changedByUserId: string;
  changedByDisplayName: string;
  createdAtUtc: string;
}

export interface PendingAssignee {
  userId: string;
  displayName: string;
  departmentCode: string;
}

export interface CreatePendingRequest {
  projectId: string;
  issueType: PendingIssueType;
  title: string;
  description: string;
  priority: PendingPriority;
  assigneeUserId: string | null;
  dueDate: string | null;
  actionDepartmentCode?: string | null;
}
