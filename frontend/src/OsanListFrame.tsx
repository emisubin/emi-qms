import { OsanMenuHeading } from './OsanMenuHeading';
import { useState, type ReactNode } from 'react';
import { OsanMultiSelectFilter } from './OsanMultiSelectFilter';
import { OsanKpiButton } from './OsanKpiButton';
import filterIcon from './assets/osan-dashboard-filter.png';
import './osan-dashboard.css';
import './OsanListFrame.css';

const statusOptions = [
  { value: 'NotStarted', label: '공정 시작 전' }, { value: 'InProgress', label: '공정 진행 중' },
  { value: 'Completed', label: '포장완료' }, { value: 'Hold', label: 'HOLD' }
];

export function OsanListFrame({ title, description, counts, search, onSearchChange, onSearch,
  statuses, onStatusesChange, onReset, selectedCustomers, customers = [], onCustomersChange,
  dueFrom, dueTo, onDueChange, kpi, onKpiChange, kpiValues,
  actions, className = '', summaryLabels = ['전체', '시작 전', '진행 중', '완료'], children }: {
  summaryLabels?: string[];
  title: string; description: string; counts: number[] | null; search: string;
  onSearchChange: (value: string) => void; onSearch: () => void;
  statuses: string[]; onStatusesChange: (values: string[]) => void; onReset: () => void;
  selectedCustomers: string[]; customers?: string[]; onCustomersChange: (values: string[]) => void;
  dueFrom: string; dueTo: string; onDueChange: (from: string, to: string) => void;
  kpi: string | null; onKpiChange: (value: string | null) => void; kpiValues?: string[];
  actions?: ReactNode; className?: string; children: ReactNode;
}) {
  const [filterOpen, setFilterOpen] = useState(false);
  const active = statuses.length > 0 || selectedCustomers.length > 0 || !!dueFrom || !!dueTo;
  const summaryValues = kpiValues ?? ['All', 'NotStarted', 'InProgress', 'Completed'];
  return <section className={`osan-dashboard osan-list-frame ${className}`} aria-labelledby="osan-dashboard-title" data-presentation-contract="osan-list-frame">
    <OsanMenuHeading id="osan-dashboard-title" title={title} description={description} actions={actions} />
    <div className="osan-dashboard-summary" data-count={summaryLabels.length} aria-label="프로젝트 요약">
      {summaryLabels.map((label, i) => <OsanKpiButton key={label} label={label}
        value={counts ? counts[i].toLocaleString() : '—'} selected={kpi === summaryValues[i]}
        className={kpi === summaryValues[i] ? 'is-selected' : ''}
        onClick={() => onKpiChange(kpi === summaryValues[i] ? null : summaryValues[i])} />)}
    </div>
    <div className="osan-dashboard-toolbar">
      <form className="osan-dashboard-search" onSubmit={event => { event.preventDefault(); onSearch(); }}>
        <input aria-label="프로젝트 검색" placeholder="프로젝트 검색" value={search} maxLength={200} onChange={event => onSearchChange(event.target.value)} />
        <button type="submit">검색</button>
      </form>
      <button type="button" className="osan-dashboard-filter" aria-expanded={filterOpen} aria-controls="osan-dashboard-filter-options" onClick={() => setFilterOpen(!filterOpen)}>
        <img src={filterIcon} alt="" />필터{active && <span className="osan-dashboard-filter-active" aria-label="적용됨" />}
      </button>
    </div>
    {filterOpen && <div className="osan-dashboard-filter-options" id="osan-dashboard-filter-options">
      <OsanMultiSelectFilter label="고객사" options={customers.map(name => ({ value: name, label: name }))}
        values={selectedCustomers} onApply={onCustomersChange} />
      <OsanMultiSelectFilter label="상태" options={statusOptions} values={statuses} onApply={onStatusesChange} align="end" />
      <label>납기 시작일 <input type="date" value={dueFrom} max={dueTo || undefined} onChange={event => onDueChange(event.target.value, dueTo)} /></label>
      <label>납기 종료일 <input type="date" value={dueTo} min={dueFrom || undefined} onChange={event => onDueChange(dueFrom, event.target.value)} /></label>
      <button type="button" onClick={onReset}>초기화</button>
      <button type="button" onClick={() => setFilterOpen(false)}>닫기</button>
    </div>}
    <h2 className="osan-list-heading">프로젝트 목록</h2>
    {children}
  </section>;
}

export function OsanPageHeading({ title, description, actions }: { title: string; description: string; actions?: ReactNode }) {
  return <>
    <h1 id="osan-dashboard-title">{title}</h1>
    {actions && <div className="osan-list-actions">{actions}</div>}
    <p className="osan-dashboard-description">{description}</p>
  </>;
}
