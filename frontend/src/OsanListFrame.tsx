import { useState, type ReactNode } from 'react';
import filterIcon from './assets/osan-dashboard-filter.png';
import './osan-dashboard.css';

export function OsanListFrame({ title, description, counts, search, onSearchChange, onSearch, status, onStatusChange, onReset, customer = '', customers = [], onCustomerChange, actions, className = '', children }: {
  title: string; description: string; counts: number[] | null; search: string;
  onSearchChange: (value: string) => void; onSearch: () => void; status: string;
  onStatusChange: (value: string) => void; onReset: () => void;
  customer?: string; customers?: string[]; onCustomerChange?: (value: string) => void; actions?: ReactNode; className?: string; children: ReactNode;
}) {
  const [filterOpen, setFilterOpen] = useState(false);
  return <section className={`osan-dashboard osan-list-frame ${className}`} aria-labelledby="osan-dashboard-title" data-presentation-contract="osan-list-frame">
    <OsanPageHeading title={title} description={description} actions={actions} />
    <div className="osan-dashboard-summary" aria-label="프로젝트 요약">
      {['전체', '시작 전', '진행 중', '완료'].map((label, i) => <div key={label}><span>{label}</span><strong>{counts ? counts[i].toLocaleString() : '—'}</strong></div>)}
    </div>
    <div className="osan-dashboard-toolbar">
      <form className="osan-dashboard-search" onSubmit={event => { event.preventDefault(); onSearch(); }}>
        <input aria-label="프로젝트 검색" placeholder="프로젝트 검색" value={search} maxLength={200} onChange={event => onSearchChange(event.target.value)} />
        <button type="submit">검색</button>
      </form>
      <button type="button" className="osan-dashboard-filter" aria-expanded={filterOpen} aria-controls="osan-dashboard-filter-options" onClick={() => setFilterOpen(!filterOpen)}>
        <img src={filterIcon} alt="" />필터{(status !== 'All' || customer !== '') && <span className="osan-dashboard-filter-active" aria-label="적용됨" />}
      </button>
    </div>
    {filterOpen && <div className="osan-dashboard-filter-options" id="osan-dashboard-filter-options">
      <label>고객사별 <select value={customer} onChange={event => onCustomerChange?.(event.target.value)}><option value="">전체</option>{customers.map(name=><option key={name} value={name}>{name}</option>)}</select></label>
      <label>상태별 <select value={status} onChange={event => onStatusChange(event.target.value)}>
        {['All', 'NotStarted', 'InProgress', 'Completed'].map((value, i) => <option key={value} value={value}>{['전체', '시작 전', '진행 중', '완료'][i]}</option>)}
      </select></label>
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
