export interface SalesBillingPeriod {
  periodStart: string;
  periodEnd: string;
  isRecommended: boolean;
}

export interface SalesBillingCandidate {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  customerName: string;
  item: string;
  deliveryLocation: string | null;
  firstDepartureDate: string;
  lastDepartureDate: string;
  activePanelCount: number;
  departedPanelCount: number;
  openPendingCount: number;
  salesAmount: number | null;
  currencyCode: string;
  salesOwnerName: string;
  requested: boolean;
  requestBatchId: string | null;
  requestNumber: number | null;
  requestedAtUtc: string | null;
  canSelect: boolean;
  blockedReason: string | null;
}

export interface SalesBillingCandidateList {
  period: SalesBillingPeriod;
  candidateCount: number;
  selectableCount: number;
  requestedCount: number;
  items: SalesBillingCandidate[];
}

export interface CreateSalesBillingRequest {
  operationId: string;
  periodStart: string;
  periodEnd: string;
  projectIds: string[];
  note: string | null;
}

export interface SalesBillingBatch {
  batchId: string;
  requestNumber: number;
  periodStart: string;
  periodEnd: string;
  projectCount: number;
  fileName: string;
  sha256: string;
  note: string | null;
  createdByName: string;
  createdAtUtc: string;
  replayed: boolean;
}

export interface SalesBillingBatchList {
  items: SalesBillingBatch[];
}

export interface SalesBillingProjectStatus {
  requested: boolean;
  batchId: string | null;
  requestNumber: number | null;
  requestedAtUtc: string | null;
  accountingIssueConfirmed: boolean;
}
