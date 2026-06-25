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

async function fetchJson<T>(path: string, developmentUserKey?: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);

  if (developmentUserKey) {
    headers.set('X-Dev-User', developmentUserKey);
  }

  if (init?.body) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem.message, problem.errors);
  }

  return response.json() as Promise<T>;
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
