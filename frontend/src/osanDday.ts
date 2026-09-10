import { useEffect, useState } from 'react';

const dayMilliseconds = 86_400_000;
const koreaDateFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit'
});

export function getKoreaDate(now = new Date()): string {
  const parts = koreaDateFormatter.formatToParts(now);
  const value = (type: Intl.DateTimeFormatPartTypes) => parts.find(part => part.type === type)!.value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}

export function formatOsanDday(deliveryDate: string, today: string): string {
  const parseDate = (value: string) => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return NaN;
    const timestamp = Date.parse(`${value}T00:00:00Z`);
    return Number.isFinite(timestamp) && new Date(timestamp).toISOString().slice(0, 10) === value ? timestamp : NaN;
  };
  const days = (parseDate(deliveryDate) - parseDate(today)) / dayMilliseconds;
  if (!Number.isFinite(days)) return '—';
  return days === 0 ? 'D-Day' : days > 0 ? `D-${days}` : `D+${Math.abs(days)}`;
}

export function useKoreaDate(): string {
  const [today, setToday] = useState(getKoreaDate);
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    const refresh = () => {
      clearTimeout(timer);
      const currentDate = getKoreaDate();
      setToday(currentDate);
      const nextMidnight = Date.parse(`${currentDate}T00:00:00+09:00`) + dayMilliseconds;
      timer = setTimeout(refresh, Math.max(1, nextMidnight - Date.now()));
    };
    refresh();
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
    return () => {
      clearTimeout(timer);
      window.removeEventListener('focus', refresh);
      document.removeEventListener('visibilitychange', refresh);
    };
  }, []);
  return today;
}
