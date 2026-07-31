export type MaterialReceiptStatus =
  | 'Arrived'
  | 'IqcRequested'
  | 'Passed'
  | 'InspectionNotRequired'
  | 'FailedBlocked'
  | 'Confirmed'
  | 'Cancelled';

export type MaterialIqcAttempt = {
  attemptId: string;
  attemptNumber: number;
  status: 'Requested' | 'Passed' | 'Failed';
  decisionMode: 'Legacy' | 'Detailed' | 'ScanBased';
  reason: string | null;
  pendingIssueId: string | null;
  requestedAtUtc: string;
  decidedAtUtc: string | null;
  reportId: string | null;
  reportStatus: 'Draft' | 'Finalized' | null;
  pdfStatus: 'Pending' | 'Ready' | 'Failed' | null;
};

export type MaterialReceipt = {
  receiptId: string;
  quantity: number | null;
  unit: string | null;
  arrivalDate: string;
  note: string | null;
  status: MaterialReceiptStatus;
  isLegacy: boolean;
  version: number;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  cancellationReason: string | null;
  iqcAttempts: MaterialIqcAttempt[];
};

export type MaterialReceivingItem = {
  itemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  orderItem: string | null;
  materialCategoryName: string | null;
  materialCategoryRequiresIqc: boolean | null;
  supplierName: string | null;
  supplyType: 'Purchased' | 'CustomerSupplied';
  expectedReceiptDate: string | null;
  orderQuantity: number | null;
  orderUnit: string | null;
  arrivalsClosed: boolean;
  arrivalsClosedAtUtc: string | null;
  receiptCompleted: boolean;
  arrivedQuantity: number | null;
  confirmedQuantity: number | null;
  remainingQuantity: number | null;
  processingQuantity: number | null;
  customerSupplyOverdue: boolean;
  rowVersion: number;
  receipts: MaterialReceipt[];
};

export type MaterialReceiptListResponse = {
  summary: {
    pendingArrivalCount: number;
    waitingIqcCount: number;
    failedBlockedCount: number;
    readyToConfirmCount: number;
    completedItemCount: number;
    customerSuppliedItemCount: number;
    customerSupplyOverdueCount: number;
  };
  items: MaterialReceivingItem[];
};

export type MaterialIqcQueueItem = {
  attemptId: string;
  receiptId: string;
  procurementItemId: string;
  projectId: string;
  projectTitle: string;
  projectCode: string;
  orderItem: string | null;
  quantity: number | null;
  unit: string | null;
  attemptNumber: number;
  receiptVersion: number;
  supplyType: 'Purchased' | 'CustomerSupplied';
  status: 'Requested' | 'Passed' | 'Failed';
  decisionMode: 'Legacy' | 'Detailed' | 'ScanBased';
  requestedAtUtc: string;
  decidedAtUtc: string | null;
  pendingIssueId: string | null;
  pendingIssueNumber: number | null;
  reason: string | null;
  reportId: string | null;
  reportStatus: 'Draft' | 'Finalized' | null;
  pdfStatus: 'Pending' | 'Ready' | 'Failed' | null;
};

export type MaterialIqcQueueResponse = { items: MaterialIqcQueueItem[] };

export type MaterialIqcReconciliationResponse = {
  recoveredReceiptCount: number;
  ensuredAttemptCount: number;
};

export type MaterialReceiptActionResponse = {
  procurementItemId: string;
  receiptId: string | null;
  iqcAttemptId: string | null;
  pendingIssueId: string | null;
  status: string;
  receiptCompleted: boolean;
};

export type RegisterMaterialArrivalRequest = {
  quantity: number;
  unit: string;
  arrivalDate: string;
  note: string | null;
};
