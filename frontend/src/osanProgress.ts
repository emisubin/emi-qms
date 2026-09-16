import { fetchBlob, fetchJson } from './api';

export const osanStageNames = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'] as const;
export interface OsanProgressPhoto {
  photoId: string; displayOrder: number; fileName: string; contentType: string; sizeBytes: number;
  sha256: string; uploadedAtUtc: string; uploadedByUserId: string; uploadedByDisplayName: string;
}
export interface OsanOpenIssue {
  issueId: string; registeredAtUtc: string; registeredByUserId: string; registeredByDisplayName: string;
  comment: string; photos: OsanProgressPhoto[]; lastRecordedAtUtc: string; lastRecordedByDisplayName: string;
}
export interface OsanProgressStep {
  openIssue?: OsanOpenIssue | null; canRegisterIssue?: boolean; canResolveIssue?: boolean;
  stepId: string; sequenceNumber: number; stepCode: string; stepName: string; status: string;
  startedAtUtc: string | null; completedByUserId: string | null; canCompleteIndividual: boolean; canCompleteBatch: boolean;
  guidanceDescription: string | null; guidancePhotos: { photoId: string; altText: string }[];
  completedAtUtc: string | null; completedByDisplayName: string | null; photos: OsanProgressPhoto[]; comment?: string | null; editOpen?: boolean; rejected?: boolean;
}
export interface OsanProgressTarget {
  targetId: string; sequenceNumber: number; displayName: string; status: string; version: number; steps: OsanProgressStep[];
  startedAtUtc: string | null; startedByUserId: string | null; startedByDisplayName: string | null;
}
export interface OsanProgressDetail {
  projectId: string; title: string; projectCode: string; status: string; productName?: string; workOrderNumber?: string;
  canManageStages?: boolean; completedStepCount: number; totalStepCount: number; targets: OsanProgressTarget[];
}
export interface OsanRelatedPanel {
  projectId: string; projectCode: string; projectTitle: string;
  targetId: string; sequenceNumber: number; displayName: string; status: string;
}
export interface OsanRelatedPanelsResponse {
  sourceProjectId: string; workOrderNumber: string | null; panels: OsanRelatedPanel[];
}
export interface OsanProgressMutation { operationId: string; replayed: boolean; project: OsanProgressDetail }
export interface OsanProgressSelection { targetId: string; expectedVersion: number }
export interface OsanCompletionRequest {
  operationId: string; completionMode: 'individual' | 'batch'; stageSequence: number;
  targets: OsanProgressSelection[]; photos: File[]; comment?: string;
}
const projectPath = (projectId: string) => `/api/osan/projects/${encodeURIComponent(projectId)}/progress`;
export function getOsanProgress(projectId: string, userKey?: string, signal?: AbortSignal) {
  return fetchJson<OsanProgressDetail>(projectPath(projectId), userKey, { signal });
}
export function getOsanRelatedPanels(projectId: string, userKey?: string, signal?: AbortSignal) {
  return fetchJson<OsanRelatedPanelsResponse>(`${projectPath(projectId)}/related-panels`, userKey, { signal });
}
export function completeOsanProgress(projectId: string, request: OsanCompletionRequest, userKey?: string) {
  const body = new FormData();
  body.set('operationId', request.operationId);
  body.set('completionMode', request.completionMode);
  body.set('stageSequence', String(request.stageSequence));
  body.set('targets', JSON.stringify(request.targets));
  body.set('comment', request.comment ?? '');
  request.photos.forEach(file => body.append('photos', file, file.name));
  return fetchJson<OsanProgressMutation>(`${projectPath(projectId)}/completions`, userKey, { method: 'POST', body });
}
export function getOsanProgressPhoto(projectId: string, photoId: string, userKey?: string, signal?: AbortSignal, preview = false) {
  return fetchBlob(`${projectPath(projectId)}/photos/${encodeURIComponent(photoId)}${preview ? "?preview=true" : ""}`, userKey, signal);
}
export function isHeicPhoto(file: File): boolean {
  return ['image/heic', 'image/heif'].includes(file.type.toLowerCase()) || /\.hei[cf]$/i.test(file.name);
}
export async function previewOsanPhoto(projectId: string, file: File, userKey?: string, signal?: AbortSignal): Promise<Blob> {
  const body = new FormData(); body.append('photos', file, file.name);
  const result = await fetchJson<{ contentType: string; base64: string }>(`${projectPath(projectId)}/photo-preview`, userKey, { method: 'POST', body, signal });
  const bytes = Uint8Array.from(atob(result.base64), c => c.charCodeAt(0));
  return new Blob([bytes], { type: result.contentType });
}
export function validateOsanPhotos(files: readonly File[]): string | null {
  if (files.length > 5) return '사진은 최대 5장까지 선택할 수 있습니다.';
  if (files.some(file => !['image/jpeg', 'image/png', 'image/heic', 'image/heif'].includes(file.type.toLowerCase()) && !(['', 'application/octet-stream'].includes(file.type) && /\.(jpe?g|png|hei[cf])$/i.test(file.name)))) return 'JPEG·PNG·HEIC 사진을 선택해 주세요.';
  if (files.some(file => file.size === 0)) return '빈 파일은 첨부할 수 없습니다.';
  if (files.reduce((total, file) => total + file.size, 0) > 40 * 1024 * 1024) return '선택한 사진의 전체 용량은 40MiB 이하여야 합니다.';
  return null;
}
export function completionUnavailable(targets: OsanProgressTarget[], stageSequence: number, mode: 'individual' | 'batch'): string | null {
  if (targets.length === 0) return '진행할 대상을 선택해 주세요.';
  if (mode === 'individual' && targets.length !== 1) return '개별 완료는 대상 한 개를 선택해 주세요.';
  for (const target of targets) {
    if (target.steps.find(step => step.sequenceNumber === stageSequence)?.status === 'Completed') return `${target.displayName}: 이미 완료한 단계입니다.`;
    if (target.steps.find(step => step.sequenceNumber === stageSequence)?.openIssue) return `${target.displayName}: 조치 완료로 해당 단계를 완료해 주세요.`;
    if (stageSequence !== 5 && target.steps.some(step => step.sequenceNumber < stageSequence && step.status !== 'Completed' && (stageSequence === 7 || !step.openIssue))) return `${target.displayName}: 이전 단계를 모두 완료하거나 이상을 등록한 후 입력할 수 있습니다.`;
    if (stageSequence === 7 && target.steps.some(step => step.openIssue)) return `${target.displayName}: 미조치 이상을 모두 해소한 후 포장할 수 있습니다.`;
    const step = target.steps.find(item => item.sequenceNumber === stageSequence);
    if (!step || !(mode === 'individual' ? step.canCompleteIndividual : step.canCompleteBatch)) return `${target.displayName}: 현재 이 단계를 완료할 수 없습니다. 새로고침하여 상태를 확인해 주세요.`;
  }
  return null;
}

export function validateOsanRecord(photos: readonly File[], comment: string, canManage: boolean, retained: readonly OsanProgressPhoto[] = []): string | null {
  const error = validateOsanPhotos(photos);
  if (error) return error;
  if (comment.length > 1000) return '코멘트는 최대 1000자입니다.';
  if (photos.length + retained.length > 5) return '사진은 최대 5장입니다.';
  if (photos.reduce((sum, f) => sum + f.size, 0) + retained.reduce((sum, f) => sum + f.sizeBytes, 0) > 40 * 1024 * 1024) return '사진의 전체 용량은 40MiB 이하여야 합니다.';
  if (!photos.length && !retained.length && !(canManage && comment.trim())) return canManage ? '사진이 없으면 코멘트를 입력해 주세요.' : '사진을 1장 이상 첨부해 주세요.';
  return null;
}

export function mutateOsanIssue(projectId: string, action: 'register' | 'record' | 'resolve', request: OsanCompletionRequest, userKey?: string) {
  const body = new FormData();
  body.set('operationId', request.operationId); body.set('completionMode', 'individual'); body.set('stageSequence', String(request.stageSequence));
  body.set('targets', JSON.stringify(request.targets)); body.set('comment', request.comment ?? '');
  request.photos.forEach(file => body.append('photos', file, file.name));
  const suffix = action === 'register' ? '' : action === 'record' ? '/records' : '/resolve';
  return fetchJson<OsanProgressMutation>(`${projectPath(projectId)}/issues${suffix}`, userKey, { method: 'POST', body });
}
