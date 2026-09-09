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
}
export interface OsanProjectExcelPreview {
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
}
const importPath = '/api/osan/projects/import';
export function downloadOsanProjectTemplate(userKey: string, signal?: AbortSignal) {
  return fetchBlob(`${importPath}/template`, userKey, signal);
}
export function previewOsanProjectExcel(userKey: string, file: File, signal?: AbortSignal) {
  const body = new FormData();
  body.set('file', file);
  return fetchJson<OsanProjectExcelPreview>(`${importPath}/preview`, userKey, { method: 'POST', body, signal });
}
export function applyOsanProjectExcel(userKey: string, file: File, expectedFileSha256: string, operationId: string, signal?: AbortSignal) {
  const body = new FormData();
  body.set('file', file);
  body.set('expectedFileSha256', expectedFileSha256);
  body.set('operationId', operationId);
  return fetchJson<OsanProjectExcelResult>(`${importPath}/apply`, userKey, { method: 'POST', body, signal });
}
