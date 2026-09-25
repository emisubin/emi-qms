import { OsanMenuHeading } from './OsanMenuHeading';
import { useCallback, useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react';
import filterIcon from './assets/osan-dashboard-filter.png';
import './osan-dashboard.css';
import './OsanListFrame.css';

const statusOptions = [
  { value: 'NotStarted', label: '공정 시작 전' }, { value: 'InProgress', label: '공정 진행 중' },
  { value: 'Completed', label: '포장완료' }, { value: 'Hold', label: 'HOLD' }
];

type FilterOption = { value: string; label: string };

function MultiSelectFilter({ label, options, values, onApply, align = 'start' }: {
  label: string; options: FilterOption[]; values: string[]; onApply: (values: string[]) => void; align?: 'start' | 'end';
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState<string[]>(values);
  const rootRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popupId = useId();
  const normalizedQuery = query.trim().toLocaleLowerCase('ko');
  const visibleOptions = useMemo(() => normalizedQuery
    ? options.filter(option => option.label.toLocaleLowerCase('ko').includes(normalizedQuery))
    : options, [normalizedQuery, options]);
  const selectedLabels = options.filter(option => values.includes(option.value)).map(option => option.label);
  const summary = selectedLabels.length === 0 ? '전체' : selectedLabels.length === 1 ? selectedLabels[0] : `${selectedLabels.length}개 선택`;

  const openDropdown = () => {
    setDraft(values);
    setQuery('');
    setOpen(true);
  };
  const dismiss = useCallback((returnFocus = false) => {
    setDraft(values);
    setQuery('');
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  }, [values]);

  useEffect(() => {
    if (!open) return;
    searchRef.current?.focus();
    const handlePointerDown = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) dismiss();
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      dismiss(true);
    };
    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [dismiss, open]);

  const toggle = (value: string) => setDraft(current => current.includes(value)
    ? current.filter(item => item !== value)
    : [...current, value]);
  const selectVisible = () => setDraft(current => [...new Set([...current, ...visibleOptions.map(option => option.value)])]);
  const clearVisible = () => {
    const visible = new Set(visibleOptions.map(option => option.value));
    setDraft(current => current.filter(value => !visible.has(value)));
  };

  return <div className={`osan-filter-select is-${align}`} ref={rootRef}>
    <button type="button" className="osan-filter-select-trigger" ref={triggerRef}
      aria-haspopup="dialog" aria-expanded={open} aria-controls={open ? popupId : undefined}
      aria-label={`${label} 필터: ${summary}`} onClick={() => open ? dismiss() : openDropdown()}>
      <span>{label}</span><strong>{summary}</strong><span className="osan-filter-select-chevron" aria-hidden="true" />
    </button>
    {open && <div className="osan-filter-select-popup" id={popupId} role="dialog" aria-label={`${label} 선택`}>
      <label className="osan-filter-select-search"><span>{label} 검색</span>
        <input ref={searchRef} type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder={`${label} 검색`} />
      </label>
      <div className="osan-filter-select-bulk" aria-label={`${label} 검색 결과 선택`}>
        <button type="button" onClick={selectVisible} disabled={visibleOptions.length === 0}>검색 결과 전체 선택</button>
        <button type="button" onClick={clearVisible} disabled={visibleOptions.length === 0}>검색 결과 선택 해제</button>
      </div>
      <div className="osan-filter-select-list" role="group" aria-label={`${label} 목록`}>
        {visibleOptions.map(option => <label key={option.value} className={draft.includes(option.value) ? 'is-selected' : ''}>
          <input type="checkbox" checked={draft.includes(option.value)} onChange={() => toggle(option.value)} />
          <span>{option.label}</span>
        </label>)}
        {visibleOptions.length === 0 && <p>검색 결과가 없습니다.</p>}
      </div>
      {draft.length === 0 && <p className="osan-filter-select-all-note" role="status">선택 없음은 전체로 적용됩니다.</p>}
      <div className="osan-filter-select-actions">
        <button type="button" onClick={() => dismiss()}>취소</button>
        <button type="button" className="is-primary" onClick={() => { onApply(draft); setOpen(false); setQuery(''); }}>적용</button>
      </div>
    </div>}
  </div>;
}

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
      <MultiSelectFilter label="고객사" options={customers.map(name => ({ value: name, label: name }))}
        values={selectedCustomers} onApply={onCustomersChange} />
      <MultiSelectFilter label="상태" options={statusOptions} values={statuses} onApply={onStatusesChange} align="end" />
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
