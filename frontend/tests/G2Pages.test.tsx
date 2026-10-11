import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { G2AttendancePage } from '../src/G2ManagementPages';
import { todaySeoul, type G2Day, type G2MetricValue, type G2RangeResponse } from '../src/g2';
import { isG2RedDay } from '../src/useG2Holidays';

function day(date = todaySeoul(), forecast = false): G2Day {
  return {
    date, isForecast: forecast,
    morningProduction: null, afternoonProduction: null, delivery: null, defect: null,
    morningRepair: null, afternoonRepair: null, repairTotal: null, defectInventory: 0,
    morningEmiAttendance: null, morningContractorAttendance: null,
    afternoonEmiAttendance: null, afternoonContractorAttendance: null,
    productionTotal: null, morningAttendanceTotal: null, afternoonAttendanceTotal: null, attendanceTotal: null,
    inventory: null, physicalCount: null, defectInventoryCount: null, dailyProductionTarget: null, deliveryTarget: null, inventoryTarget: null
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function metric(quantity: number): G2MetricValue {
  return { quantity, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' };
}

describe('G2 charts and daily management', () => {
  beforeEach(() => setRuntimeMutationAllowed(true));
  afterEach(() => { setRuntimeMutationAllowed(false); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it('marks weekends and configured public holidays as red days', () => {
    const holidays = new Map([['2026-08-17', '광복절 대체공휴일']]);
    expect(isG2RedDay('2026-08-15')).toBe(true);
    expect(isG2RedDay('2026-08-16')).toBe(true);
    expect(isG2RedDay('2026-08-17')).toBe(false);
    expect(isG2RedDay('2026-08-17', holidays)).toBe(true);
    expect(isG2RedDay('2026-08-18', holidays)).toBe(false);
  });

  it('uses the Seoul calendar date even when UTC is still on the previous month', () => {
    expect(todaySeoul(new Date('2026-08-31T15:05:00Z'))).toBe('2026-09-01');
  });

  it('discloses EMI and contractor attendance independently from management totals', async () => {
    const attendanceDay = {
      ...day('2026-08-17'),
      morningEmiAttendance: metric(15),
      morningContractorAttendance: metric(5),
      afternoonEmiAttendance: metric(14),
      afternoonContractorAttendance: metric(4),
      morningAttendanceTotal: 20,
      afternoonAttendanceTotal: 18,
      attendanceTotal: 38
    };
    const response: G2RangeResponse = { today: '2026-08-19', from: '2026-08-01', to: '2026-08-31', days: [attendanceDay] };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/cheongju/api/g2/days') return json(response);
      if (url.pathname === '/cheongju/api/system/holidays') return json([{ holidayDate: '2026-08-17', name: '광복절 대체공휴일' }]);
      return json({ title: 'not found' }, 404);
    }));

    render(<G2AttendancePage developmentUserKey="dev-manufacturing" canEdit mutationEnabled />);
    const table = await screen.findByRole('table', { name: '제조 인원 월간 출근 현황' });
    expect(within(table).queryByRole('rowheader', { name: '구분' })).toBeNull();
    expect(within(table).queryByRole('rowheader', { name: '오전 EMI' })).toBeNull();
    expect(within(table).queryByRole('rowheader', { name: '오전 도급' })).toBeNull();
    expect(within(table).queryByRole('rowheader', { name: '오후 EMI' })).toBeNull();
    expect(within(table).queryByRole('rowheader', { name: '오후 도급' })).toBeNull();

    const morningTotal = within(table).getByRole('button', { name: '8월 17일 오전 합계 20명 세부 인원 보기' });
    fireEvent.click(morningTotal);
    expect(within(table).getByRole('rowheader', { name: '오전 EMI' })).toBeInTheDocument();
    expect(within(table).getByRole('rowheader', { name: '오전 도급' })).toBeInTheDocument();
    expect(within(table).queryByRole('rowheader', { name: '오후 EMI' })).toBeNull();
    fireEvent.click(within(table).getByRole('button', { name: '오전 합계 세부 인원 접기' }));
    expect(within(table).queryByRole('rowheader', { name: '오전 EMI' })).toBeNull();

    fireEvent.click(within(table).getByRole('button', { name: '오후 합계 세부 인원 보기' }));
    expect(within(table).getByRole('rowheader', { name: '오후 EMI' })).toBeInTheDocument();
    expect(within(table).getByRole('rowheader', { name: '오후 도급' })).toBeInTheDocument();
    expect(within(table).getByRole('rowheader', { name: '하루 총원' }).closest('tr')).toHaveClass('g2-grand-total-row');
  });
});
