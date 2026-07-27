import { useAdaptiveLayout } from './adaptive-layout';
import type { DepartmentWorkspaceOption } from './DepartmentProjectHub';

export function DepartmentWorkHub({
  department,
  title,
  description,
  workspaces,
  onOpenWorkspace
}: {
  department: string;
  title: string;
  description: string;
  workspaces: DepartmentWorkspaceOption[];
  onOpenWorkspace: (workspace: string) => void;
}) {
  const { isMobile } = useAdaptiveLayout();

  return (
    <section
      className={isMobile ? 'page-surface department-work-hub department-work-hub--mobile' : 'page-surface department-work-hub'}
      data-testid={`department-work-hub-${department.toLowerCase()}`}
    >
      <header className="department-hub-header">
        <div>
          <p className="eyebrow">{department.toUpperCase()} WORKSPACE</p>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <span className="department-work-hub-count">{workspaces.length}<small>업무</small></span>
      </header>

      <section className="department-work-only" aria-labelledby={`${department}-work-title`}>
        <div className="department-workspace-heading">
          <span>01</span>
          <div>
            <h3 id={`${department}-work-title`}>처리할 업무를 선택하세요</h3>
            <p>업무를 선택하면 해당 업무의 KPI와 프로젝트 목록으로 이동합니다.</p>
          </div>
        </div>
        <ul className="department-work-list" aria-label={`${title} 업무 목록`}>
          {workspaces.map((option, index) => (
            <li key={option.key}>
              <button
                type="button"
                className="department-work-row"
                data-shape={option.shape}
                onClick={() => onOpenWorkspace(option.key)}
              >
                <span className="department-work-index">{String(index + 1).padStart(2, '0')}</span>
                <i aria-hidden="true" />
                <span className="department-work-copy">
                  <strong>{option.label}</strong>
                  <small>{option.description}</small>
                </span>
                <span className="department-work-open">업무 열기 <b aria-hidden="true">→</b></span>
              </button>
            </li>
          ))}
        </ul>
      </section>
    </section>
  );
}
