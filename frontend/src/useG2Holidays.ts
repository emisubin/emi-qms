import { useEffect, useState } from 'react';
import { listSystemHolidays } from './api';
import type { G2Day } from './g2';

export type G2HolidayMap = ReadonlyMap<string, string>;
export const EMPTY_G2_HOLIDAYS: G2HolidayMap = new Map();

function weekday(date: string) {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day)).getUTCDay();
}

export function isG2RedDay(date: string, holidays: G2HolidayMap = EMPTY_G2_HOLIDAYS) {
  const day = weekday(date);
  return day === 0 || day === 6 || holidays.has(date);
}

export function g2RedDayTitle(date: string, holidays: G2HolidayMap = EMPTY_G2_HOLIDAYS) {
  const labels: string[] = [];
  const holiday = holidays.get(date);
  if (holiday) labels.push(holiday);
  const day = weekday(date);
  if (day === 0) labels.push('일요일');
  if (day === 6) labels.push('토요일');
  return labels.join(' · ');
}

export function useG2Holidays(developmentUserKey: string | undefined, days: G2Day[]): G2HolidayMap {
  const from = days[0]?.date ?? '';
  const to = days[days.length - 1]?.date ?? '';
  const [holidays, setHolidays] = useState<G2HolidayMap>(EMPTY_G2_HOLIDAYS);

  useEffect(() => {
    let active = true;
    if (!from || !to) {
      queueMicrotask(() => { if (active) setHolidays(EMPTY_G2_HOLIDAYS); });
      return () => { active = false; };
    }
    const controller = new AbortController();
    void listSystemHolidays(developmentUserKey, { dateFrom: from, dateTo: to, signal: controller.signal })
      .then(items => {
        if (!active) return;
        const next = new Map<string, string>();
        items.forEach(item => next.set(item.holidayDate, item.name));
        setHolidays(next);
      })
      .catch(error => {
        if (!active || (error instanceof DOMException && error.name === 'AbortError')) return;
        setHolidays(EMPTY_G2_HOLIDAYS);
      });
    return () => { active = false; controller.abort(); };
  }, [developmentUserKey, from, to]);

  return holidays;
}
