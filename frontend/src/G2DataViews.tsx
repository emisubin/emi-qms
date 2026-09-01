import type { ReactNode } from 'react';
import { formatG2Date, type G2Day } from './g2';
import { useG2DateRange } from './useG2DateRange';
import { EMPTY_G2_HOLIDAYS, g2RedDayTitle, isG2RedDay, type G2HolidayMap } from './useG2Holidays';

export type G2HorizontalRow = {
  key?: string;
  label: ReactNode;
  value: (day: G2Day) => ReactNode;
  rowClassName?: string;
  cellClassName?: (day: G2Day) => string | undefined;
};

export function G2DateRangeFilter({
  label,
  from,
  to,
  minimum,
  maximum,
  filteredCount,
  totalCount,
  onFromChange,
  onToChange,
  onReset
}: {
  label: string;
  from: string;
  to: string;
  minimum: string;
  maximum: string;
  filteredCount: number;
  totalCount: number;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  onReset: () => void;
}) {
  return <div className="g2-date-range" role="group" aria-label={label}>
    <div><strong>{label}</strong><span aria-live="polite">{filteredCount}일 표시</span></div>
    <label>시작일<input type="date" min={minimum} max={to || maximum} value={from} disabled={!totalCount} onChange={event => onFromChange(event.target.value)} /></label>
    <span aria-hidden="true">–</span>
    <label>종료일<input type="date" min={from || minimum} max={maximum} value={to} disabled={!totalCount} onChange={event => onToChange(event.target.value)} /></label>
    <button type="button" disabled={!totalCount || (from === minimum && to === maximum)} onClick={onReset}>전체 기간</button>
  </div>;
}

export function G2HorizontalTable({ days, caption, rows, holidays = EMPTY_G2_HOLIDAYS }: { days: G2Day[]; caption: string; rows: G2HorizontalRow[]; holidays?: G2HolidayMap }) {
  return <div className="table-scroll g2-horizontal-table-scroll">
    <table className="g2-horizontal-table">
      <caption className="sr-only">{caption}</caption>
      <thead><tr><th scope="col">항목</th>{days.map(day => {
        const redDay = isG2RedDay(day.date, holidays);
        return <th key={day.date} scope="col" className={redDay ? 'g2-red-day' : undefined} title={redDay ? g2RedDayTitle(day.date, holidays) : undefined}><span>{formatG2Date(day.date)}</span>{day.isForecast ? <small>예상</small> : null}</th>;
      })}</tr></thead>
      <tbody>{rows.map((row, index) => <tr key={row.key ?? (typeof row.label === 'string' ? row.label : index)} className={row.rowClassName}><th scope="row">{row.label}</th>{days.map(day => {
        const classes = [row.cellClassName?.(day), day.isForecast ? 'g2-forecast-column' : null, isG2RedDay(day.date, holidays) ? 'g2-red-day-column' : null].filter(Boolean).join(' ');
        return <td key={day.date} className={classes || undefined}>{row.value(day)}</td>;
      })}</tr>)}</tbody>
    </table>
  </div>;
}

export function G2FilteredHorizontalTable({
  title,
  filterLabel,
  caption,
  days,
  rows,
  holidays = EMPTY_G2_HOLIDAYS
}: {
  title: string;
  filterLabel: string;
  caption: string;
  days: G2Day[];
  rows: G2HorizontalRow[];
  holidays?: G2HolidayMap;
}) {
  const range = useG2DateRange(days, days[0]?.date.slice(0, 7) ?? 'empty');
  return <article className="g2-card">
    <header><h3>{title}</h3></header>
    <G2DateRangeFilter
      label={filterLabel}
      from={range.from}
      to={range.to}
      minimum={range.firstDate}
      maximum={range.lastDate}
      filteredCount={range.filteredDays.length}
      totalCount={days.length}
      onFromChange={range.setFrom}
      onToChange={range.setTo}
      onReset={range.reset}
    />
    <G2HorizontalTable days={range.filteredDays} caption={caption} rows={rows} holidays={holidays} />
  </article>;
}
