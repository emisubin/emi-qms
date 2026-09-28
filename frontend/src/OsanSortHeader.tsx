import type { ReactNode } from 'react';
import './OsanSortHeader.css';

export type OsanSort = { key: string; direction: 'ascending' | 'descending' } | null;
export function nextOsanSort(current: OsanSort, key: string): OsanSort {
  if (current?.key !== key) return { key, direction: 'ascending' };
  return current.direction === 'ascending' ? { key, direction: 'descending' } : null;
}
export function sortOsanRows<T>(rows: T[], sort: OsanSort, value: (row: T, key: string) => string | number | null | undefined): T[] {
  if (!sort) return rows;
  return [...rows].sort((a, b) => {
    const left = value(a, sort.key) ?? ''; const right = value(b, sort.key) ?? '';
    const result = typeof left === 'number' && typeof right === 'number' ? left - right : String(left).localeCompare(String(right), 'ko', { numeric: true });
    return sort.direction === 'ascending' ? result : -result;
  });
}
export function OsanSortHeader({ field, sort, onSort, children, label, className = '' }: {
  field: string; sort: OsanSort; onSort: (sort: OsanSort) => void; children?: ReactNode; label?: string; className?: string;
}) {
  const activate = () => onSort(nextOsanSort(sort, field));
  return <span role="columnheader" aria-sort={sort?.key === field ? sort.direction : 'none'}
    className={`osan-sort-header ${className}`} tabIndex={0} onClick={activate}
    onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); activate(); } }}>{children ?? label}</span>;
}
