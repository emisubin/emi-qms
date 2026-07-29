import { useMemo, useState } from 'react';
import { useAdaptiveLayout } from './adaptive-layout';
import { DsBreadcrumbs, DsEmptyState, DsReadOnlyBanner } from './design-system';

export type OperationalProjectDashboardMetric = {
  label: string;
  value: number | string;
  helper: string;
  tone?: 'default' | 'warning' | 'positive';
};

export type OperationalProjectDashboardRow = {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  totalCount: number;
  readyCount: number;
  inProgressCount: number;
  blockedCount: number;
  completedCount: number;
  detail: string;
  readyTone?: 'default' | 'warning';
  inProgressTone?: 'default' | 'warning';
};

export function OperationalProjectDashboard({
  testId,
  eyebrow,
  title,
  description,
  unitLabel,
  columnLabels,
  metrics,
  projects,
  emptyMessage,
  onBack,
  primaryAction,
  readOnlyDescription,
  onOpenProject
}: {
  testId: string;
  eyebrow: string;
  title: string;
  description: string;
  unitLabel: string;
  columnLabels?: {
    total?: string;
    ready?: string;
    inProgress?: string;
    blocked?: string;
    completed?: string;
  };
  metrics: OperationalProjectDashboardMetric[];
  projects: OperationalProjectDashboardRow[];
  emptyMessage: string;
  onBack?: () => void;
  primaryAction?: {
    label: string;
    disabled?: boolean;
    onClick: () => void;
  };
  readOnlyDescription?: string;
  onOpenProject: (projectId: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();
  const [search, setSearch] = useState('');
  const visibleProjects = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    if (!keyword) return projects;
    return projects.filter((project) =>
      `${project.projectCode} ${project.projectTitle} ${project.detail}`.toLowerCase().includes(keyword));
  }, [projects, search]);

  return (
    <section className={isMobile ? 'page-surface operational-dashboard operational-dashboard--mobile' : 'page-surface operational-dashboard'} data-testid={testId}>
      {!isMobile && onBack ? <DsBreadcrumbs items={[{ label: '업무 선택', onClick: onBack }]} current={title} /> : null}
      <header className="operational-dashboard-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        {onBack || primaryAction ? (
          <div className="operational-dashboard-actions">
            {onBack ? <button type="button" onClick={onBack}>업무 선택</button> : null}
            {primaryAction ? <button type="button" className="primary-button" disabled={primaryAction.disabled} onClick={primaryAction.onClick}>{primaryAction.label}</button> : null}
          </div>
        ) : null}
      </header>

      {readOnlyDescription ? <DsReadOnlyBanner description={readOnlyDescription} /> : null}

      <div className="operational-dashboard-kpis" aria-label={`${title} KPI`}>
        {metrics.map((metric) => (
          <article key={metric.label} data-tone={metric.tone ?? 'default'}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
            <small>{metric.helper}</small>
          </article>
        ))}
      </div>

      <div className="operational-dashboard-toolbar">
        <div><strong>프로젝트 목록</strong><span>{visibleProjects.length}개 프로젝트</span></div>
        <label>
          <span className="sr-only">프로젝트 검색</span>
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="프로젝트 코드·이름 검색" />
        </label>
      </div>

      {visibleProjects.length === 0 ? (
        <DsEmptyState
          title={emptyMessage}
          description={search ? '검색어를 지우면 전체 프로젝트를 다시 확인할 수 있습니다.' : '앞 단계 완료 상태를 확인하거나 잠시 후 다시 조회해 주세요.'}
          primaryAction={search ? { label: '검색 초기화', onClick: () => setSearch('') } : undefined}
        />
      ) : (
        <div className="operational-project-table" role="table" aria-label={`${title} 프로젝트 목록`}>
          <div className="operational-project-head" role="row">
            <span>프로젝트</span>
            <span>{columnLabels?.total ?? `전체 ${unitLabel}`}</span>
            <span>{columnLabels?.ready ?? '대기'}</span>
            <span>{columnLabels?.inProgress ?? '진행'}</span>
            <span>{columnLabels?.blocked ?? '차단'}</span>
            <span>{columnLabels?.completed ?? '완료'}</span>
            <span>업무</span>
          </div>
          {visibleProjects.map((project) => (
            <div role="row" key={project.projectId}>
              <button type="button" className="operational-project-row" onClick={() => onOpenProject(project.projectId)}>
                <span className="operational-project-name"><small>{project.projectCode}</small><strong>{project.projectTitle}</strong><em>{project.detail}</em></span>
                <span><small className="mobile-only-label">전체</small><strong>{project.totalCount}</strong></span>
                <span data-status={project.readyTone === 'warning' && project.readyCount > 0 ? 'blocked' : 'normal'}><small className="mobile-only-label">{columnLabels?.ready ?? '대기'}</small>{project.readyCount}</span>
                <span data-status={project.inProgressTone === 'warning' && project.inProgressCount > 0 ? 'blocked' : 'normal'}><small className="mobile-only-label">{columnLabels?.inProgress ?? '진행'}</small>{project.inProgressCount}</span>
                <span data-status={project.blockedCount > 0 ? 'blocked' : 'normal'}><small className="mobile-only-label">{columnLabels?.blocked ?? '차단'}</small>{project.blockedCount}</span>
                <span data-status={project.completedCount === project.totalCount && project.totalCount > 0 ? 'completed' : 'normal'}><small className="mobile-only-label">완료</small>{project.completedCount}</span>
                <span className="operational-project-open">열기 <b aria-hidden="true">→</b></span>
              </button>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
