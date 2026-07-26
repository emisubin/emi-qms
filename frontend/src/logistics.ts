export type LogisticsStage = 'packing' | 'departure' | 'delivery';

export interface LogisticsQueueResponse {
  stage: LogisticsStage;
  todayCount: number;
  blockedCount: number;
  projects: LogisticsProjectQueue[];
  drafts: LogisticsDraftSummary[];
}

export interface LogisticsDraftSummary {
  targetId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  stage: LogisticsStage;
  displayCode: string;
  version: number;
  evidenceCount: number;
  createdAtUtc: string;
}

export interface LogisticsProjectQueue {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  items: LogisticsQueueItem[];
}

export interface LogisticsQueueItem {
  targetId: string;
  targetType: 'Panel' | 'PackingUnit';
  displayCode: string;
  title: string;
  supportingText: string;
  panelIds: string[];
  panelCodes: string[];
  version: number;
  status: string;
  hasOpenPending: boolean;
  canMutate: boolean;
}

export interface LogisticsMutationResponse {
  operationId: string;
  projectId: string;
  targetId: string;
  stage: LogisticsStage;
  status: string;
  version: number;
  nextStage: string;
  replayed: boolean;
}

export interface LogisticsEvidence {
  evidenceId: string;
  ownerType: string;
  displayName: string;
  normalizedMime: string;
  byteSize: number;
  altText: string | null;
  createdAtUtc: string;
}

export interface LogisticsDraftResponse {
  targetId: string;
  projectId: string;
  stage: LogisticsStage;
  displayCode: string;
  status: string;
  version: number;
  departureDate: string | null;
  panelIds: string[];
  unitIds: string[];
  evidence: LogisticsEvidence[];
}

export interface LogisticsProjectHistoryResponse {
  projectId: string;
  items: LogisticsProjectHistoryItem[];
}

export interface LogisticsProjectHistoryItem {
  targetId: string;
  stage: LogisticsStage;
  displayCode: string;
  status: string;
  version: number;
  note: string | null;
  specification: string | null;
  weightText: string | null;
  departureDate: string | null;
  panelCodes: string[];
  unitCodes: string[];
  evidence: LogisticsEvidence[];
  createdByName: string;
  createdAtUtc: string;
  finalizedByName: string | null;
  finalizedAtUtc: string | null;
  cancelledByName: string | null;
  cancelledAtUtc: string | null;
}

export interface CreatePackingUnitRequest {
  operationId: string;
  projectId: string;
  panelIds: string[];
  note: string | null;
  specification: string | null;
  weightText: string | null;
}

export interface CreateLogisticsBatchRequest {
  operationId: string;
  projectId: string;
  unitIds: string[];
  departureDate: string | null;
}
