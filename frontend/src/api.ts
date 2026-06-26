import type { ReadyHealth } from './health';
import type { CurrentUser } from './identity';
import type {
  AuditHistoryResponse,
  ChangePanelCountRequest,
  CreateProjectRequest,
  DeletedProjectDetail,
  DeletedProjectListResponse,
  DeleteProjectRequest,
  PanelPlaceholder,
  PanelInformationBulkUpdateRequest,
  PanelInformationExcelPreviewResponse,
  PanelInformationHistoryResponse,
  PanelInformationResponse,
  PanelInputUnit,
  ProjectDetail,
  ProjectListResponse,
  ProjectListTab,
  ProjectStatusChangeRequest,
  SalesOwner,
  UpdateProjectRequest
} from './projects';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
export const defaultDevelopmentUserKey = import.meta.env.DEV
  ? (import.meta.env.VITE_DEV_USER_KEY ?? 'dev-sales')
  : undefined;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly errors?: Record<string, string[]>
  ) {
    super(message);
  }
}

export async function getReadyHealth(): Promise<ReadyHealth> {
  return fetchJson<ReadyHealth>('/health/ready');
}

export async function getCurrentUser(developmentUserKey?: string): Promise<CurrentUser> {
  return fetchJson<CurrentUser>('/api/me', developmentUserKey);
}

export async function getSalesOwners(developmentUserKey?: string): Promise<SalesOwner[]> {
  return fetchJson<SalesOwner[]>('/api/sales-owners', developmentUserKey);
}

export async function listProjects(
  developmentUserKey: string | undefined,
  search = '',
  status: ProjectListTab = 'Active',
  options: { signal?: AbortSignal } = {}
): Promise<ProjectListResponse> {
  const params = new URLSearchParams();
  if (search.trim()) {
    params.set('search', search.trim());
  }
  if (status !== 'Deleted') {
    params.set('status', status);
  }
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchJson<ProjectListResponse>(`/api/projects${query}`, developmentUserKey, { signal: options.signal });
}

export async function listDeletedProjects(
  developmentUserKey: string | undefined,
  search = '',
  options: { signal?: AbortSignal } = {}
): Promise<DeletedProjectListResponse> {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
  return fetchJson<DeletedProjectListResponse>(`/api/deleted-projects${query}`, developmentUserKey, { signal: options.signal });
}

export async function getProject(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}`, developmentUserKey);
}

export async function getDeletedProject(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<DeletedProjectDetail> {
  return fetchJson<DeletedProjectDetail>(`/api/deleted-projects/${projectId}`, developmentUserKey);
}

export async function createProject(
  developmentUserKey: string | undefined,
  request: CreateProjectRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>('/api/projects', developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function updateProject(
  developmentUserKey: string | undefined,
  projectId: string,
  request: UpdateProjectRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function changePanelCount(
  developmentUserKey: string | undefined,
  projectId: string,
  request: ChangePanelCountRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}/change-panel-count`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function changeProjectStatus(
  developmentUserKey: string | undefined,
  projectId: string,
  action: 'hold' | 'resume' | 'cancel' | 'reactivate',
  request: ProjectStatusChangeRequest
): Promise<ProjectDetail> {
  return fetchJson<ProjectDetail>(`/api/projects/${projectId}/${action}`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function deleteProject(
  developmentUserKey: string | undefined,
  projectId: string,
  request: DeleteProjectRequest
): Promise<DeletedProjectDetail> {
  return fetchJson<DeletedProjectDetail>(`/api/projects/${projectId}/delete`, developmentUserKey, {
    method: 'POST',
    body: JSON.stringify(request)
  });
}

export async function listPanels(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelPlaceholder[]> {
  return fetchJson<PanelPlaceholder[]>(`/api/projects/${projectId}/panels`, developmentUserKey);
}

export async function getPanel(
  developmentUserKey: string | undefined,
  projectId: string,
  panelId: string
): Promise<PanelPlaceholder> {
  return fetchJson<PanelPlaceholder>(`/api/projects/${projectId}/panels/${panelId}`, developmentUserKey);
}

export async function getAuditHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<AuditHistoryResponse> {
  return fetchJson<AuditHistoryResponse>(`/api/projects/${projectId}/audit-history`, developmentUserKey);
}

export async function getPanelInformation(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelInformationResponse> {
  return fetchJson<PanelInformationResponse>(`/api/projects/${projectId}/panel-information`, developmentUserKey);
}

export async function updatePanelInformation(
  developmentUserKey: string | undefined,
  projectId: string,
  request: PanelInformationBulkUpdateRequest
): Promise<PanelInformationResponse> {
  return fetchJson<PanelInformationResponse>(`/api/projects/${projectId}/panel-information`, developmentUserKey, {
    method: 'PATCH',
    body: JSON.stringify(request)
  });
}

export async function getPanelInformationHistory(
  developmentUserKey: string | undefined,
  projectId: string
): Promise<PanelInformationHistoryResponse> {
  return fetchJson<PanelInformationHistoryResponse>(`/api/projects/${projectId}/panel-information/history`, developmentUserKey);
}

export async function downloadPanelInformationTemplate(
  developmentUserKey: string | undefined,
  projectId: string,
  inputUnit: PanelInputUnit
): Promise<{ blob: Blob; fileName: string }> {
  const query = inputUnit === 'Inch' ? 'inch' : 'mm';
  const response = await fetchWithAuth(
    `/api/projects/${projectId}/panel-information/import/template?unit=${query}`,
    developmentUserKey);

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return {
    blob: await response.blob(),
    fileName: readContentDispositionFileName(response.headers.get('Content-Disposition')) ?? `Panel_Information_${query}.xlsx`
  };
}

export async function previewPanelInformationExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File,
  inputUnit: PanelInputUnit | null
): Promise<PanelInformationExcelPreviewResponse> {
  const form = new FormData();
  form.append('file', file);
  if (inputUnit) {
    form.append('inputUnit', inputUnit);
  }

  return fetchJson<PanelInformationExcelPreviewResponse>(
    `/api/projects/${projectId}/panel-information/import/preview`,
    developmentUserKey,
    {
      method: 'POST',
      body: form
    });
}

export async function applyPanelInformationExcel(
  developmentUserKey: string | undefined,
  projectId: string,
  file: File,
  inputUnit: PanelInputUnit | null,
  expectedFileSha256: string,
  expectedPackagingMethod: string | null,
  reason: string | null,
  expectedVersions: Array<{ panelId: string; expectedPanelInfoVersion: number }>
): Promise<PanelInformationResponse> {
  const form = new FormData();
  form.append('file', file);
  if (inputUnit) {
    form.append('inputUnit', inputUnit);
  }
  form.append('expectedFileSha256', expectedFileSha256);
  if (expectedPackagingMethod) {
    form.append('expectedPackagingMethod', expectedPackagingMethod);
  }
  form.append('expectedVersions', JSON.stringify(expectedVersions));
  if (reason) {
    form.append('reason', reason);
  }

  return fetchJson<PanelInformationResponse>(
    `/api/projects/${projectId}/panel-information/import/apply`,
    developmentUserKey,
    {
      method: 'POST',
      body: form
    });
}

async function fetchWithAuth(path: string, developmentUserKey?: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);

  if (developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  }

  return fetch(`${apiBaseUrl}${path}`, { ...init, headers });
}

async function fetchJson<T>(path: string, developmentUserKey?: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);

  if (developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  }

  if (init?.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetchWithAuth(path, undefined, { ...init, headers });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return response.json() as Promise<T>;
}

function readContentDispositionFileName(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(value);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const quotedMatch = /filename="([^"]+)"/i.exec(value);
  if (quotedMatch?.[1]) {
    return quotedMatch[1];
  }

  const plainMatch = /filename=([^;]+)/i.exec(value);
  return plainMatch?.[1]?.trim() ?? null;
}

async function readProblem(response: Response): Promise<{ message: string; errors?: Record<string, string[]> }> {
  try {
    const payload = await response.json() as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    return {
      message: payload.title ?? payload.detail ?? `API 요청 실패: ${response.status}`,
      errors: payload.errors
    };
  } catch {
    return { message: `API 요청 실패: ${response.status}` };
  }
}
