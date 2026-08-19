import { useEffect, useMemo, useRef, useState } from 'react';
import type { G2Day } from './g2';

export function useG2DateRange(days: G2Day[], resetKey: string) {
  const firstDate = days[0]?.date ?? '';
  const lastDate = days[days.length - 1]?.date ?? '';
  const [from, setFromState] = useState(firstDate);
  const [to, setToState] = useState(lastDate);
  const previousResetKey = useRef(resetKey);

  useEffect(() => {
    if (previousResetKey.current === resetKey) return;
    previousResetKey.current = resetKey;
    setFromState(firstDate);
    setToState(lastDate);
  }, [firstDate, lastDate, resetKey]);

  const effectiveFrom = from || firstDate;
  const effectiveTo = to || lastDate;
  const filteredDays = useMemo(
    () => days.filter(day => day.date >= effectiveFrom && day.date <= effectiveTo),
    [days, effectiveFrom, effectiveTo]
  );

  function setFrom(next: string) {
    setFromState(next);
    if (effectiveTo && next > effectiveTo) setToState(next);
  }

  function setTo(next: string) {
    setToState(next);
    if (effectiveFrom && next < effectiveFrom) setFromState(next);
  }

  function reset() {
    setFromState(firstDate);
    setToState(lastDate);
  }

  return { from: effectiveFrom, to: effectiveTo, firstDate, lastDate, filteredDays, setFrom, setTo, reset };
}
