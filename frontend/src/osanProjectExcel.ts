import { fetchBlob, fetchJson } from './api';

export interface OsanProjectExcelRow {
  rowNumber: number;
  title: string;
  projectCode: string;
  customerName: string;
  poNumber: string | null;
  workOrderNumber: string | null;
  deliveryDate: string | null;
  productName: string;
  quantity: number | null;
  errors: string[];
  duplicateKind?: 'identical' | 'code' | null;
}
export interface OsanProjectExcelPreview {
  supportsRowEditing?: boolean;
  fileSha256: string;
  totalRowCount: number;
  totalQuantity: number;
  errorCount: number;
  rows: OsanProjectExcelRow[];
  errors: string[];
}
export interface OsanProjectExcelResult {
  operationId: string;
  replayed: boolean;
  createdCount: number;
  projectIds: string[];
  createdRowNumbers: number[];
}
const importPath = '/api/osan/projects/import';
export function downloadOsanProjectTemplate(userKey: string, signal?: AbortSignal) {
  return fetchBlob(`${importPath}/template`, userKey, signal);
}
export function previewOsanProjectExcel(userKey: string, file: File, signal?: AbortSignal, rows?: OsanProjectExcelRow[]) {
  const body = new FormData();
  body.set('file', file);
  if (rows) body.set('rows', JSON.stringify(rows));
  return fetchJson<OsanProjectExcelPreview>(`${importPath}/preview`, userKey, { method: 'POST', body, signal });
}
export function applyOsanProjectExcel(userKey: string, file: File, expectedFileSha256: string, operationId: string, signal?: AbortSignal, rows?: OsanProjectExcelRow[], confirmedDuplicateRowNumbers: number[] = []) {
  const body = new FormData();
  body.set('file', file);
  body.set('expectedFileSha256', expectedFileSha256);
  body.set('operationId', operationId);
  if (rows) body.set('rows', JSON.stringify(rows));
  body.set('confirmedDuplicateRowNumbers', JSON.stringify(confirmedDuplicateRowNumbers));
  return fetchJson<OsanProjectExcelResult>(`${importPath}/apply`, userKey, { method: 'POST', body, signal });
}
