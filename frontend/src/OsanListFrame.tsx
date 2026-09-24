import { useState, type ReactNode } from 'react';
import filterIcon from './assets/osan-dashboard-filter.png';
import './osan-dashboard.css';

const statusOptions = [
  ['NotStarted', '공정 시작 전'], ['InProgress', '공정 진행 중'],
  ['Completed', '포장완료'], ['Hold', 'HOLD']
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
    <OsanPageHeading title={title} description={description} actions={actions} />
    <div className="osan-dashboard-summary" data-count={summaryLabels.length} aria-label="프로젝트 요약">
      {summaryLabels.map((label, i) => <button type="button" key={label} className={kpi === summaryValues[i] ? 'is-selected' : ''}
        aria-pressed={kpi === summaryValues[i]} onClick={() => onKpiChange(kpi === summaryValues[i] ? null : summaryValues[i])}>
        <span>{label}</span><strong>{counts ? counts[i].toLocaleString() : '—'}</strong>
      </button>)}
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
      <fieldset><legend>고객사별</legend>
        {customers.map(name => <label key={name}><input type="checkbox" checked={selectedCustomers.includes(name)} onChange={() => onCustomersChange(selectedCustomers.includes(name) ? selectedCustomers.filter(item => item !== name) : [...selectedCustomers, name])} />{name}</label>)}
        {customers.length === 0 && <span>고객사가 없습니다.</span>}
      </fieldset>
      <fieldset><legend>상태별</legend>
        {statusOptions.map(([value, label]) => <label key={value}><input type="checkbox" checked={statuses.includes(value)} onChange={() => onStatusesChange(statuses.includes(value) ? statuses.filter(item => item !== value) : [...statuses, value])} />{label}</label>)}
      </fieldset>
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
