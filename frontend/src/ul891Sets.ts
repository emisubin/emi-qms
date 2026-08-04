export interface CreateUl891SetSpecInput {
  name: string;
  quantity: number;
  panelCount: number;
}

export interface Ul891SetStructure {
  projectId: string;
  structureMode: 'Ul891Set' | 'FlatPanel';
  isLegacyFlat: boolean;
  canEditOrder: boolean;
  canEditDesign: boolean;
  specs: Ul891SetSpec[];
  orderedProcurementItems: Ul891OrderedProcurementItem[];
  recoveryCases: Ul891RecoveryCase[];
}

export interface Ul891SetSpec {
  specId: string;
  specNo: number;
  name: string;
  rowVersion: number;
  activeInstanceCount: number;
  currentDesign: Ul891SetDesignSlot[];
  versions: Ul891SetVersion[];
  instances: Ul891SetInstance[];
}

export interface Ul891SetDesignSlot {
  slotId: string;
  positionNumber: number;
  panelName: string | null;
  panelSpecification: string | null;
  widthMm: number | null;
  heightMm: number | null;
  depthMm: number | null;
  rowVersion: number;
}

export interface Ul891SetVersion {
  versionId: string;
  versionNumber: number;
  status: 'Draft' | 'Published' | 'Superseded';
  revisionReason: string | null;
  publishedAtUtc: string | null;
  components: Ul891SetComponent[];
}

export interface Ul891SetComponent {
  componentId: string;
  componentCode: string;
  panelName: string | null;
  panelSpecification: string | null;
  widthMm: number | null;
  heightMm: number | null;
  depthMm: number | null;
  sortOrder: number;
}

export interface Ul891SetInstance {
  instanceId: string;
  instanceNumber: number;
  specVersionId: string;
  specVersionNumber: number;
  status: 'Active' | 'Cancelled';
  rowVersion: number;
  hasStarted: boolean;
  hasDeliveredPanel: boolean;
  panels: Ul891SetPanel[];
}

export interface Ul891SetPanel {
  panelId: string;
  sequenceNumber: number;
  displayCode: string;
  componentCode: string;
  designSlotId: string | null;
  positionNumber: number | null;
  panelName: string | null;
  panelSpecification: string | null;
  panelStatus: 'Active' | 'Cancelled';
  workflowStage: string;
  packingUnitLabel: string | null;
  departureDate: string | null;
  delivered: boolean;
}

export interface Ul891OrderedProcurementItem {
  procurementItemId: string;
  sequenceNumber: number;
  orderItem: string;
  orderDate: string;
}

export interface Ul891RecoveryCase {
  recoveryCaseId: string;
  setInstanceId: string;
  setInstanceNumber: number;
  procurementItemId: string;
  procurementItemName: string;
  orderDate: string;
  status: 'BillingRequired' | 'AppliedToRequest' | 'InvoiceConfirmed' | 'Recovered';
  note: string | null;
  rowVersion: number;
  createdAtUtc: string;
  recoveredAtUtc: string | null;
}

export interface Ul891MutationResponse {
  operationId: string;
  projectId: string;
  action: string;
  replayed: boolean;
}

export interface MonthlyBilling {
  projectId: string;
  structureMode: string;
  salesAmount: number | null;
  currencyCode: string | null;
  confirmedAmount: number;
  currentRequestedAmount: number;
  remainingAmount: number | null;
  canReadAmounts: boolean;
  canMutate: boolean;
  ledgers: MonthlyBillingLedger[];
  unbilledMonths: MonthlyBillingEvidenceMonth[];
}

export interface MonthlyBillingLedger {
  ledgerId: string;
  billingMonth: string;
  kind: 'Shipment' | 'RecoveryOnly';
  status: 'Open' | 'Requested' | 'InvoiceConfirmed' | 'AdjustmentRequired';
  rowVersion: number;
  revisions: MonthlyBillingRevision[];
  currentShipmentEvidence: MonthlyBillingPanelEvidence[];
  availableRecoveryCases: Ul891RecoveryCase[];
}

export interface MonthlyBillingRevision {
  revisionId: string;
  revisionNumber: number;
  amount: number | null;
  note: string | null;
  isAdjustment: boolean;
  adjustmentReason: string | null;
  createdAtUtc: string;
  invoiceConfirmedDate: string | null;
  invoiceNumber: string | null;
  panels: MonthlyBillingPanelEvidence[];
  recoveryCaseIds: string[];
}

export interface MonthlyBillingPanelEvidence {
  panelId: string;
  displayCode: string;
  setLabel: string | null;
  packingUnitLabel: string | null;
  departureDate: string;
}

export interface MonthlyBillingEvidenceMonth {
  billingMonth: string;
  panelCount: number;
  hasLedger: boolean;
  statusLabel: string;
}

export interface UpdateUl891DraftRequest {
  expectedSpecVersion: number;
  specName: string;
  revisionReason: string;
  components: Array<{
    componentCode: string;
    panelName: string | null;
    panelSpecification: string | null;
    widthMm: number | null;
    heightMm: number | null;
    depthMm: number | null;
  }>;
}
