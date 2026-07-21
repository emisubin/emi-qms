import { FormEvent, useCallback, useEffect, useState } from 'react';
import { ApiError, listProjects } from './api';
import { useAdaptiveLayout } from './adaptive-layout';
import type { ProjectListItem } from './projects';

export type DepartmentWorkspaceOption = {
  key: string;
  label: string;
  description: string;
  shape: 'square' | 'round' | 'pill' | 'cut';
};

type HubState =
  | { kind: 'loading' }
  | { kind: 'ready'; projects: ProjectListItem[] }
  | { kind: 'error'; message: string };

export function DepartmentProjectHub({
  developmentUserKey,
  department,
  title,
  description,
  workspaces,
  requireWorkspaceChoice = false,
  onOpenProject
}: {
  developmentUserKey: string;
  department: string;
  title: string;
  description: string;
  workspaces: DepartmentWorkspaceOption[];
  requireWorkspaceChoice?: boolean;
  onOpenProject: (workspace: string, project: ProjectListItem) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [state, setState] = useState<HubState>({ kind: 'loading' });
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [workspace, setWorkspace] = useState(requireWorkspaceChoice ? '' : workspaces[0]?.key ?? '');

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    try {
      const response = await listProjects(developmentUserKey, appliedSearch, 'All', { pageSize: 500 });
      setState({ kind: 'ready', projects: response.items.filter((project) => project.status !== 'Cancelled') });
    } catch (error) {
      setState({
        kind: 'error',
        message: error instanceof ApiError ? error.message : '프로젝트 목록을 불러오지 못했습니다.'
      });
    }
  }, [appliedSearch, developmentUserKey]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  function searchProjects(event: FormEvent) {
    event.preventDefault();
    const next = search.trim();
    if (next === appliedSearch) void load();
    else setAppliedSearch(next);
  }

  const selectedWorkspace = workspaces.find((item) => item.key === workspace);
  return (
    <section className={isMobile ? 'page-surface department-project-hub department-project-hub--mobile' : 'page-surface department-project-hub'} data-testid={`department-project-hub-${department.toLowerCase()}`}>
      <header className="department-hub-header">
        <div>
          <p className="eyebrow">{department.toUpperCase()} WORKSPACE</p>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <span className="department-hub-project-count">{state.kind === 'ready' ? state.projects.length : '-'}<small>프로젝트</small></span>
      </header>

      <section className="department-workspace-choice" aria-labelledby={`${department}-workspace-title`}>
        <div className="department-workspace-heading">
          <span>01</span>
          <div><h3 id={`${department}-workspace-title`}>업무 선택</h3><p>{requireWorkspaceChoice && !workspace ? '먼저 처리할 업무를 선택하세요.' : '업무를 바꾸면 같은 프로젝트 목록에서 바로 이어집니다.'}</p></div>
        </div>
        <div className="department-workspace-options" role="radiogroup" aria-label={`${title} 업무 선택`}>
          {workspaces.map((option) => (
            <button
              type="button"
              role="radio"
              aria-checked={workspace === option.key}
              className="department-workspace-option"
              data-shape={option.shape}
              data-selected={workspace === option.key}
              key={option.key}
              onClick={() => setWorkspace(option.key)}
            >
              <i aria-hidden="true" />
              <span><strong>{option.label}</strong><small>{option.description}</small></span>
              <b aria-hidden="true">→</b>
            </button>
          ))}
        </div>
      </section>

      <section className="department-project-choice" aria-labelledby={`${department}-project-title`} data-disabled={!workspace}>
        <div className="department-workspace-heading">
          <span>02</span>
          <div><h3 id={`${department}-project-title`}>프로젝트 선택</h3><p>{selectedWorkspace ? `${selectedWorkspace.label} 업무를 진행할 프로젝트를 선택하세요.` : '업무를 선택하면 프로젝트를 열 수 있습니다.'}</p></div>
        </div>
        <form className="department-project-search" onSubmit={searchProjects}>
          <input aria-label="프로젝트 검색" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="PJT 코드, 프로젝트명, 고객사 검색" />
          <button type="submit">검색</button>
        </form>

        {state.kind === 'loading' ? <div className="department-hub-loading" role="status">프로젝트를 불러오는 중입니다.</div> : null}
        {state.kind === 'error' ? <div className="action-feedback" data-tone="error" role="alert">{state.message}<button type="button" onClick={() => void load()}>다시 시도</button></div> : null}
        {state.kind === 'ready' && state.projects.length === 0 ? <div className="department-hub-empty"><strong>표시할 프로젝트가 없습니다.</strong><span>검색어를 바꾸거나 프로젝트 등록 상태를 확인해 주세요.</span></div> : null}
        {state.kind === 'ready' ? (
          <div className="department-project-grid">
            {state.projects.map((project, index) => (
              <button
                type="button"
                className="department-project-card"
                key={project.projectId}
                disabled={!workspace}
                onClick={() => workspace && onOpenProject(workspace, project)}
              >
                <span className="department-project-index">{String(index + 1).padStart(2, '0')}</span>
                <span className="department-project-main"><small>{project.projectCode}</small><strong>{project.projectTitle}</strong><em>{project.customerName}</em></span>
                <span className="department-project-meta"><small>납기</small><b>{project.deliveryDate}</b><em data-status={project.status}>{projectStatusLabel(project.status)}</em></span>
                <span className="department-project-open" aria-hidden="true">열기</span>
              </button>
            ))}
          </div>
        ) : null}
      </section>
    </section>
  );
}

function projectStatusLabel(status: ProjectListItem['status']) {
  return ({ Active: '진행', OnHold: '보류', Completed: '완료', Cancelled: '취소' } as const)[status];
}
