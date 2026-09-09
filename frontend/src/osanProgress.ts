import { fetchBlob, fetchJson } from './api';

export const osanStageNames = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'] as const;
export interface OsanProgressPhoto {
  photoId: string; displayOrder: number; fileName: string; contentType: string; sizeBytes: number;
  sha256: string; uploadedAtUtc: string; uploadedByUserId: string; uploadedByDisplayName: string;
}
export interface OsanProgressStep {
  stepId: string; sequenceNumber: number; stepCode: string; stepName: string; status: string;
  startedAtUtc: string | null; completedByUserId: string | null; canCompleteIndividual: boolean; canCompleteBatch: boolean;
  guidanceDescription: string | null; guidancePhotos: { photoId: string; altText: string }[];
  completedAtUtc: string | null; completedByDisplayName: string | null; photos: OsanProgressPhoto[];
}
export interface OsanProgressTarget {
  targetId: string; sequenceNumber: number; displayName: string; status: string; version: number; steps: OsanProgressStep[];
  startedAtUtc: string | null; startedByUserId: string | null; startedByDisplayName: string | null; canStart: boolean;
}
export interface OsanProgressDetail {
  projectId: string; title: string; projectCode: string; status: string;
  completedStepCount: number; totalStepCount: number; targets: OsanProgressTarget[];
}
export interface OsanProgressMutation { operationId: string; replayed: boolean; project: OsanProgressDetail }
export interface OsanProgressSelection { targetId: string; expectedVersion: number }
export interface OsanCompletionRequest {
  operationId: string; completionMode: 'individual' | 'batch'; stageSequence: number;
  targets: OsanProgressSelection[]; photos: File[];
}
const projectPath = (projectId: string) => `/api/osan/projects/${encodeURIComponent(projectId)}/progress`;
export function getOsanProgress(projectId: string, userKey?: string, signal?: AbortSignal) {
  return fetchJson<OsanProgressDetail>(projectPath(projectId), userKey, { signal });
}
export function startOsanProgress(projectId: string, operationId: string, targets: OsanProgressSelection[], userKey?: string) {
  return fetchJson<OsanProgressMutation>(`${projectPath(projectId)}/start`, userKey, {
    method: 'POST', body: JSON.stringify({ operationId, targets })
  });
}
export function completeOsanProgress(projectId: string, request: OsanCompletionRequest, userKey?: string) {
  const body = new FormData();
  body.set('operationId', request.operationId);
  body.set('completionMode', request.completionMode);
  body.set('stageSequence', String(request.stageSequence));
  body.set('targets', JSON.stringify(request.targets));
  request.photos.forEach(file => body.append('photos', file, file.name));
  return fetchJson<OsanProgressMutation>(`${projectPath(projectId)}/completions`, userKey, { method: 'POST', body });
}
export function getOsanProgressPhoto(projectId: string, photoId: string, userKey?: string, signal?: AbortSignal) {
  return fetchBlob(`${projectPath(projectId)}/photos/${encodeURIComponent(photoId)}`, userKey, signal);
}
export function validateOsanPhotos(files: readonly File[]): string | null {
  if (files.length > 5) return '사진은 최대 5장까지 선택할 수 있습니다.';
  if (files.some(file => !['image/jpeg', 'image/png'].includes(file.type))) return 'JPEG 또는 PNG 사진을 선택해 주세요. HEIC는 지원하지 않습니다.';
  if (files.some(file => file.size === 0 || file.size > 5 * 1024 * 1024)) return '사진은 빈 파일이 아닌 장당 5MiB 이하 파일이어야 합니다.';
  if (files.reduce((total, file) => total + file.size, 0) > 15 * 1024 * 1024) return '선택한 사진의 전체 용량은 15MiB 이하여야 합니다.';
  return null;
}
export function completionUnavailable(targets: OsanProgressTarget[], stageSequence: number, mode: 'individual' | 'batch'): string | null {
  if (targets.length === 0) return '진행할 대상을 선택해 주세요.';
  if (mode === 'individual' && targets.length !== 1) return '개별 완료는 대상 한 개를 선택해 주세요.';
  for (const target of targets) {
    if (target.status === 'NotStarted') return `${target.displayName}: 작업 시작 후 완료할 수 있습니다.`;
    if (target.steps.find(step => step.sequenceNumber === stageSequence)?.status === 'Completed') return `${target.displayName}: 이미 완료한 단계입니다.`;
    if (stageSequence === 7 && target.steps.some(step => step.sequenceNumber < 7 && step.status !== 'Completed')) return `${target.displayName}: 앞 6단계 완료 후 포장할 수 있습니다.`;
    if (mode === 'individual' && target.steps.find(step => step.status !== 'Completed')?.sequenceNumber !== stageSequence) return `${target.displayName}: 다음 미완료 단계만 개별 완료할 수 있습니다.`;
    const step = target.steps.find(item => item.sequenceNumber === stageSequence);
    if (!step || !(mode === 'individual' ? step.canCompleteIndividual : step.canCompleteBatch)) return `${target.displayName}: 현재 이 단계를 완료할 수 없습니다. 새로고침하여 상태를 확인해 주세요.`;
  }
  return null;
}
