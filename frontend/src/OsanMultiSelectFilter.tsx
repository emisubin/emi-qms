import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import './OsanListFrame.css';

type FilterOption = { value: string; label: string };

export function OsanMultiSelectFilter({ label, options, values, onApply, align = 'start' }: {
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
