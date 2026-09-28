import { useId, useState, type ReactNode } from 'react';
import { OsanButton } from './OsanButton';
import filterIcon from './assets/osan-dashboard-filter.png';
import './osan-dashboard.css';
import './OsanListFrame.css';

export function OsanFilterToolbar({ search, onSearchChange, onSearch, searchLabel, placeholder = searchLabel, active, onReset, children }: {
  search: string; onSearchChange: (value: string) => void; onSearch: () => void;
  searchLabel: string; placeholder?: string; active: boolean; onReset: () => void; children: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const id = useId();
  return <div className="osan-filter-toolbar">
    <div className="osan-dashboard-toolbar">
      <form className="osan-dashboard-search" onSubmit={event => { event.preventDefault(); onSearch(); }}>
        <input aria-label={searchLabel} placeholder={placeholder} value={search} maxLength={200} onChange={event => onSearchChange(event.target.value)} />
        <OsanButton type="submit">검색</OsanButton>
      </form>
      <OsanButton type="button" className="osan-dashboard-filter" aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}>
        <img src={filterIcon} alt="" />필터{active && <span className="osan-dashboard-filter-active" aria-label="적용됨" />}
      </OsanButton>
    </div>
    {open && <div className="osan-dashboard-filter-options" id={id}>
      {children}
      <OsanButton type="button" onClick={onReset}>초기화</OsanButton>
      <OsanButton type="button" onClick={() => setOpen(false)}>닫기</OsanButton>
    </div>}
  </div>;
}
