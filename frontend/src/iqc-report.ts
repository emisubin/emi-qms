export type IqcCheckResult = 'Pass' | 'Fail' | 'NotApplicable';
export type IqcReportStatus = 'Draft' | 'Finalized';
export type IqcPdfStatus = 'Pending' | 'Ready' | 'Failed';

export type IqcTemplateItem = {
  itemId: string;
  itemCode: string;
  displayOrder: number;
  label: string;
  guidance: string | null;
  responseType: 'Check' | 'Text';
  isRequired: boolean;
  requiresPhoto: boolean;
  maxTextLength: number | null;
};

export type IqcItemResponse = {
  templateItemId: string;
  checkResult: IqcCheckResult | null;
  textValue: string | null;
  note: string | null;
};

export type IqcPhoto = {
  photoId: string;
  templateItemId: string;
  displayName: string;
  normalizedMime: 'image/jpeg' | 'image/png';
  byteSize: number;
  altText: string;
  createdAtUtc: string;
};

export type IqcReport = {
  attemptId: string;
  receiptId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  orderItem: string | null;
  quantity: number | null;
  unit: string | null;
  attemptNumber: number;
  receiptVersion: number;
  attemptStatus: 'Requested' | 'Passed' | 'Failed';
  decisionMode: 'Legacy' | 'Detailed';
  reportId: string | null;
  reportStatus: IqcReportStatus | null;
  reportVersion: number | null;
  result: 'Passed' | 'Failed' | null;
  reason: string | null;
  pdfStatus: IqcPdfStatus | null;
  pdfErrorCode: string | null;
  templateVersion: number;
  canEdit: boolean;
  items: IqcTemplateItem[];
  responses: IqcItemResponse[];
  photos: IqcPhoto[];
  finalizedAtUtc: string | null;
  finalizedBy: string | null;
};

export type SaveIqcItemResponse = {
  templateItemId: string;
  checkResult: IqcCheckResult | null;
  textValue: string | null;
  note: string | null;
};
