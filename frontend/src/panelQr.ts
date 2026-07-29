export type PanelQrRecord = {
  qrCodeId: string;
  projectId: string;
  panelId: string;
  status: 'Active' | 'Revoked';
  scanUrl: string;
  issuedByName: string;
  issuedAtUtc: string;
};

export type ProjectPanelQrItem = {
  panelId: string;
  sequenceNumber: number;
  displayCode: string;
  displayName: string;
  qrEligible: boolean;
  hasActiveQr: boolean;
  qr: PanelQrRecord | null;
};

export type ProjectPanelQrList = {
  projectId: string;
  eligibleCount: number;
  issuedCount: number;
  panels: ProjectPanelQrItem[];
};

export type PanelQrPrintSheet = {
  projectId: string;
  itemCount: number;
  items: Array<{
    panelId: string;
    displayCode: string;
    displayName: string;
    imageUrl: string;
  }>;
};

export type PanelQrBatchIssue = {
  projectId: string;
  requestedCount: number;
  newlyIssuedCount: number;
  alreadyIssuedCount: number;
};

export type PanelQrResolveStatus =
  | 'Ok'
  | 'OkCompletedProject'
  | 'PanelInactiveOrProjectHold'
  | 'Revoked'
  | 'ProjectDeleted'
  | 'NotFound';

export type PanelQrResolve = {
  status: PanelQrResolveStatus;
  message: string;
  projectId: string | null;
  panelId: string | null;
  projectCode: string | null;
  projectTitle: string | null;
  panelDisplayName: string | null;
  currentStageCode: string | null;
  currentStageName: string | null;
  currentDepartmentCode: string | null;
  currentDepartmentName: string | null;
  canEditCurrentStage: boolean;
  primaryActionLabel: string | null;
  primaryActionPath: string | null;
  overviewPath: string | null;
};
