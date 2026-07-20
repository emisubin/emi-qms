export interface SalesSettlementDetail {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  settlementStatus: 'NotStarted' | 'Draft' | 'Completed' | 'Cancelled';
  version: number;
  activePanelCount: number;
  deliveredPanelCount: number;
  openPendingCount: number;
  invoiceIssuedDate: string | null;
  invoiceNumber: string | null;
  note: string | null;
  completedAtUtc: string | null;
  completedByName: string | null;
  allPanelsDelivered: boolean;
  noOpenPending: boolean;
  invoiceIssued: boolean;
  canComplete: boolean;
  canMutate: boolean;
  pendingLink: string;
  billingRequestStatus: import('./salesBilling').SalesBillingProjectStatus;
}

export interface SaveSalesSettlementDraftRequest {
  expectedVersion: number;
  invoiceIssuedDate: string | null;
  invoiceNumber: string | null;
  note: string | null;
}

export interface CompleteSalesSettlementRequest extends SaveSalesSettlementDraftRequest {
  operationId: string;
}

export interface SalesSettlementMutationResponse {
  operationId: string;
  projectId: string;
  settlementId: string;
  status: 'Draft' | 'Completed';
  version: number;
  replayed: boolean;
}
