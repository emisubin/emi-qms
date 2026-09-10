import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { formatOsanDday, getKoreaDate, useKoreaDate } from '../src/osanDday';

afterEach(() => { cleanup(); vi.useRealTimers(); });

describe('오산 한국 날짜 D-day', () => {
  it.each([
    ['2026-09-10', '2026-09-10', 'D-Day'],
    ['2026-09-11', '2026-09-10', 'D-1'],
    ['2026-09-09', '2026-09-10', 'D+1'],
    ['2027-01-01', '2026-12-31', 'D-1'],
    ['2028-03-01', '2028-02-28', 'D-2'],
    ['2026-03-01', '2026-02-28', 'D-1'],
    ['2026-02-30', '2026-02-28', '—'],
    ['', '2026-09-10', '—']
  ])('%s 납기와 %s 기준일을 %s로 표시한다', (deliveryDate, today, expected) => {
    expect(formatOsanDday(deliveryDate, today)).toBe(expected);
  });

  it('실행 기기 날짜와 관계없이 한국 자정을 기준으로 바뀐다', () => {
    expect(getKoreaDate(new Date('2026-09-10T14:59:59.999Z'))).toBe('2026-09-10');
    expect(getKoreaDate(new Date('2026-09-10T15:00:00.000Z'))).toBe('2026-09-11');
  });

  it('열어 둔 화면은 한국 자정에 갱신하고 unmount 시 타이머를 해제한다', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-10T14:59:59.999Z'));
    const { result, unmount } = renderHook(useKoreaDate);
    expect(result.current).toBe('2026-09-10');
    act(() => vi.advanceTimersByTime(1));
    expect(result.current).toBe('2026-09-11');
    unmount();
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each(['focus', 'visibilitychange'])('절전 후 %s 시 지난 날짜를 갱신한다', event => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-10T14:00:00Z'));
    const { result } = renderHook(useKoreaDate);
    vi.setSystemTime(new Date('2026-09-12T01:00:00Z'));
    act(() => (event === 'focus' ? window : document).dispatchEvent(new Event(event)));
    expect(result.current).toBe('2026-09-12');
  });
});
