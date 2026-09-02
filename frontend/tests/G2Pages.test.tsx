import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { G2ProductionDeliveryInventoryChart, G2ShiftProductionChart } from '../src/G2Charts';
import { G2HomePage } from '../src/G2HomePage';
import { applyG2HomePreview } from '../src/G2HomePreview';
import { G2AttendancePage, G2OperationsPage } from '../src/G2ManagementPages';
import { todaySeoul, type G2Day, type G2MetricValue, type G2RangeResponse } from '../src/g2';
import { isG2RedDay } from '../src/useG2Holidays';

function day(date = todaySeoul(), forecast = false): G2Day {
  return {
    date, isForecast: forecast,
    morningProduction: null, afternoonProduction: null, delivery: null, defect: null,
    morningEmiAttendance: null, morningContractorAttendance: null,
    afternoonEmiAttendance: null, afternoonContractorAttendance: null,
    productionTotal: null, morningAttendanceTotal: null, afternoonAttendanceTotal: null, attendanceTotal: null,
    inventory: null, physicalCount: null, dailyProductionTarget: null, deliveryTarget: null, inventoryTarget: null
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

  it('provides screen-reader tables and marks forecast inventory with a separate dotted line', () => {
    const deliveryTarget = { targetType: 'Delivery' as const, effectiveDate: '2026-08-17', quantity: 6, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' };
    const days = [
      { ...day('2026-08-17'), inventory: 10, physicalCount: { ...metric(0), quantity: 0 }, morningProduction: metric(2), afternoonProduction: metric(2), productionTotal: 4, delivery: metric(2), deliveryTarget },
      { ...day('2026-08-18', true), inventory: 12, morningProduction: metric(1), afternoonProduction: metric(2), productionTotal: 3, dailyProductionTarget: { targetType: 'DailyProduction' as const, effectiveDate: '2026-08-18', quantity: 5, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' } }
    ];
    const holidays = new Map([['2026-08-17', '광복절 대체공휴일']]);
    const { container } = render(<><G2ProductionDeliveryInventoryChart days={days} holidays={holidays} /><G2ShiftProductionChart days={days} holidays={holidays} /></>);

    const flowChart = screen.getByRole('img', { name: /일별 생산, 납품, 재고 추이/u });
    expect(flowChart).toBeInTheDocument();
    expect(within(flowChart).getAllByText('100대')).toHaveLength(2);
    expect(within(flowChart).queryByText('80대')).toBeNull();
    expect(within(flowChart).getAllByText('180대')).toHaveLength(2);
    expect(within(flowChart).queryByText('-70대')).toBeNull();
    const sharedSixtyTicks = within(flowChart).getAllByText('60대');
    expect(sharedSixtyTicks).toHaveLength(2);
    expect(sharedSixtyTicks[0]).toHaveAttribute('y', sharedSixtyTicks[1].getAttribute('y'));
    expect(within(flowChart).queryByText('0~60 확대 · 60~180 압축')).toBeNull();
    expect(flowChart.querySelector('.g2-axis-break')).toBeNull();
    expect(within(screen.getByRole('img', { name: /오전조와 오후조 생산량/u })).getByText('60대')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: '일별 생산·납품·재고 그래프 자료' })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: '조별 생산량 그래프 자료' })).toBeInTheDocument();
    expect(container.querySelector('.g2-line-forecast')).toBeInTheDocument();
    expect(container.querySelector('#g2-production-gradient')).toBeInTheDocument();
    expect(container.querySelector('#g2-production-gradient stop')).toHaveAttribute('stop-color', '#60a5fa');
    expect(container.querySelector('#g2-delivery-gradient stop')).toHaveAttribute('stop-color', '#fb923c');
    expect(container.querySelector('#g2-morning-gradient stop')).toHaveAttribute('stop-color', '#bfdbfe');
    expect(container.querySelector('#g2-afternoon-gradient stop')).toHaveAttribute('stop-color', '#60a5fa');
    expect(container.querySelectorAll('.g2-plot-background')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-line-point')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-stack-segments')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-stack-outline')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-baseline')).toHaveLength(2);
    expect(container.querySelector('.g2-bar-production')?.tagName.toLowerCase()).toBe('path');
    expect(container.querySelector('#g2-stack-clip-0 path')).toBeInTheDocument();
    expect(container.querySelectorAll('.g2-line-hit')).toHaveLength(5);
    expect(container.querySelectorAll('.g2-line-halo')).toHaveLength(5);
    expect(container.querySelectorAll('.g2-inventory-value')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-line-point-physical')).toHaveLength(1);
    expect(container.querySelector('.g2-inventory-value-physical')).toHaveTextContent('10');
    expect(container.querySelector('.g2-line-point-physical')).toHaveAttribute('r', '3.2');
    expect(container.querySelector('.g2-line-point:not(.g2-line-point-physical)')).toHaveAttribute('r', '2.5');
    expect(container.querySelectorAll('.g2-flow-bar-value')).toHaveLength(3);
    expect(container.querySelectorAll('.g2-chart-scroll')).toHaveLength(2);
    const pairedFlowLabels = container.querySelectorAll('.g2-flow-bar-value[data-series]');
    expect(pairedFlowLabels[0]).not.toHaveAttribute('y', pairedFlowLabels[1].getAttribute('y'));
    expect(container.querySelectorAll('.g2-stack-value-morning')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-stack-value-afternoon')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-stack-total-value')).toHaveLength(0);
    expect(container.querySelectorAll('.g2-stack-hit')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-line-production-average')).toHaveLength(2);
    const visibleAverageLine = container.querySelector('.g2-line-production-average:not(.g2-line-halo)');
    const firstStack = container.querySelector('.g2-stack-segments');
    expect(visibleAverageLine).not.toBeNull();
    expect(firstStack).not.toBeNull();
    expect(visibleAverageLine!.compareDocumentPosition(firstStack!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(container.querySelectorAll('.g2-axis-label')).toHaveLength(4);
    expect(container.querySelectorAll('.g2-axis-label.g2-red-day')).toHaveLength(2);

    const productionBar = container.querySelector('.g2-bar-production');
    expect(productionBar).not.toBeNull();
    fireEvent.mouseEnter(productionBar!);
    expect(flowChart.querySelector('.g2-tooltip-date')).toHaveTextContent('8월 17일');
    expect(flowChart.querySelector('.g2-tooltip-value')).toHaveTextContent('생산 4대');
    fireEvent.mouseLeave(productionBar!);

    const inventoryLine = container.querySelector('.g2-line-hit[data-series="inventory"]');
    expect(inventoryLine).not.toBeNull();
    fireEvent.mouseEnter(inventoryLine!);
    expect(flowChart.querySelector('.g2-tooltip-value')).toHaveTextContent('재고 10대');

    const physicalPoint = container.querySelector('.g2-physical-point-hit');
    expect(physicalPoint).not.toBeNull();
    fireEvent.mouseEnter(physicalPoint!);
    expect(flowChart.querySelector('.g2-tooltip-value')).toHaveTextContent('실사 0대');

    const dailyProductionBar = container.querySelector('.g2-stack-hit[data-date="2026-08-17"]');
    expect(dailyProductionBar).not.toBeNull();
    fireEvent.mouseEnter(dailyProductionBar!);
    const shiftChart = screen.getByRole('img', { name: /오전조와 오후조 생산량/u });
    expect(shiftChart.querySelector('.g2-tooltip-date')).toHaveTextContent('8월 17일');
    expect(Array.from(shiftChart.querySelectorAll('.g2-tooltip-value')).map(item => item.textContent)).toEqual(['오전: 2대', '오후: 2대', '전체: 4대']);

    const productionTargetLine = container.querySelector('.g2-line-hit[data-series="production-target"]');
    expect(productionTargetLine).not.toBeNull();
    fireEvent.mouseEnter(productionTargetLine!);
    expect(shiftChart.querySelector('.g2-tooltip-date')).toHaveTextContent('8월 18일');
    expect(shiftChart.querySelector('.g2-tooltip-value')).toHaveTextContent('일 생산목표 5대');

    const deliveryTargetLine = container.querySelector('.g2-line-hit[data-series="delivery-target"]');
    expect(deliveryTargetLine).not.toBeNull();
    fireEvent.mouseEnter(deliveryTargetLine!);
    expect(flowChart.querySelector('.g2-tooltip-value')).toHaveTextContent('납품 목표 6대');

    const productionAverageLine = container.querySelector('.g2-line-hit[data-series="production-average"]');
    expect(productionAverageLine).not.toBeNull();
    fireEvent.mouseEnter(productionAverageLine!);
    expect(shiftChart.querySelector('.g2-tooltip-date')).toHaveTextContent('선택 기간');
    expect(shiftChart.querySelector('.g2-tooltip-value')).toHaveTextContent('총 생산 평균 3.5대');
  });

  it('marks weekends and configured public holidays as red days', () => {
    const holidays = new Map([['2026-08-17', '광복절 대체공휴일']]);
    expect(isG2RedDay('2026-08-15')).toBe(true);
    expect(isG2RedDay('2026-08-16')).toBe(true);
    expect(isG2RedDay('2026-08-17')).toBe(false);
    expect(isG2RedDay('2026-08-17', holidays)).toBe(true);
    expect(isG2RedDay('2026-08-18', holidays)).toBe(false);
  });

  it('recalculates preview production and inventory without changing physical-count boundaries', () => {
    const days = [
      { ...day('2026-08-01'), physicalCount: { ...metric(100), quantity: 100 }, inventory: 100 },
      { ...day('2026-08-02'), morningProduction: metric(10), productionTotal: 10, delivery: metric(4), defect: metric(1), inventory: 105 }
    ];

    const preview = applyG2HomePreview(days, {
      '2026-08-02': { morningProduction: '20', delivery: '10', defect: '3' }
    });

    expect(preview[0].inventory).toBe(100);
    expect(preview[1].productionTotal).toBe(20);
    expect(preview[1].inventory).toBe(107);
  });

  it('applies preview movements to the next day from the available-inventory cutover', () => {
    const days = [
      { ...day('2026-08-27'), morningProduction: metric(34), productionTotal: 34, delivery: metric(30), inventory: 2 },
      { ...day('2026-08-28'), morningProduction: metric(22), afternoonProduction: metric(25), productionTotal: 47, delivery: metric(30), inventory: 6 },
      { ...day('2026-08-29'), inventory: 23 }
    ];

    const preview = applyG2HomePreview(days, {
      '2026-08-28': { morningProduction: '30', delivery: '35', defect: '2' }
    });

    expect(preview[0].inventory).toBe(2);
    expect(preview[1].inventory).toBe(6);
    expect(preview[2].inventory).toBe(24);
  });

  it('updates the home graph source from table preview inputs without sending a mutation', async () => {
    const methods: string[] = [];
    const days = [
      { ...day('2026-08-01'), physicalCount: { ...metric(100), quantity: 100 }, inventory: 100 },
      { ...day('2026-08-02'), morningProduction: metric(10), productionTotal: 10, delivery: metric(4), defect: metric(1), inventory: 105 }
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      methods.push(init?.method ?? 'GET');
      const url = new URL(String(input));
      if (url.pathname === '/api/system/holidays') return json([]);
      return json({ today: '2026-08-02', year: 2026, month: 8, hasInventoryBaseline: true, days });
    }));

    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    const productionTable = await screen.findByRole('table', { name: '생산 현황' });
    fireEvent.change(within(productionTable).getByLabelText('8월 2일 불량 임시 예상값'), { target: { value: '3' } });

    const inventoryRow = within(productionTable).getByRole('rowheader', { name: '재고' }).closest('tr');
    expect(inventoryRow).not.toBeNull();
    expect(within(inventoryRow!).getByText('103')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '임시값 초기화' })).toBeInTheDocument();
    expect(methods.every(method => method === 'GET')).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: '임시값 초기화' }));
    expect(within(inventoryRow!).getByText('105')).toBeInTheDocument();
  });

  it('uses the Seoul calendar date even when UTC is still on the previous month', () => {
    expect(todaySeoul(new Date('2026-08-31T15:05:00Z'))).toBe('2026-09-01');
  });

  it('keeps both axes fixed around a five-day mobile data window and scrolls only the inner dates', () => {
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
      matches: query === '(max-width: 700px)', media: query, onchange: null,
      addEventListener: vi.fn(), removeEventListener: vi.fn(), addListener: vi.fn(), removeListener: vi.fn(), dispatchEvent: vi.fn()
    })));
    const days = Array.from({ length: 31 }, (_, index) => day(`2026-08-${String(index + 1).padStart(2, '0')}`));
    const { container } = render(<G2ProductionDeliveryInventoryChart days={days} />);
    const chart = container.querySelector<SVGSVGElement>('.g2-chart');
    const scrollArea = container.querySelector<HTMLElement>('.g2-chart-scroll');

    expect(chart).not.toBeNull();
    expect(chart).toHaveAttribute('viewBox', '0 0 3769.6 340');
    expect(chart!.style.getPropertyValue('--g2-mobile-chart-width')).toBe('620%');
    expect(container.querySelector('.g2-mobile-chart-frame-svg')).toBeInTheDocument();
    const labelsInFirstFixedViewport = Array.from(chart!.querySelectorAll('.g2-axis-label'))
      .filter(label => Number(label.getAttribute('x')) <= 608);
    expect(labelsInFirstFixedViewport).toHaveLength(5);
    expect(scrollArea).toHaveAttribute('tabindex', '0');
  });

  it('saves only the changed authorized metric and preserves zero as a real value', async () => {
    let savedBody: Record<string, unknown> | null = null;
    const response: G2RangeResponse = { today: todaySeoul(), from: todaySeoul(), to: todaySeoul(), days: [day()] };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/g2/days') return json(response);
      if (url.pathname.startsWith('/api/g2/operations/')) { savedBody = JSON.parse(String(init?.body)) as Record<string, unknown>; return json({ saved: true }); }
      return json({ title: 'not found' }, 404);
    }));

    render(<G2OperationsPage developmentUserKey="dev-manufacturing" canEditProduction canEditDelivery={false} mutationEnabled />);
    const morning = await screen.findByLabelText(/^오전 생산량/u);
    expect(screen.getByLabelText(/^일일 납품량/u)).toBeDisabled();
    fireEvent.change(morning, { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: '변경한 값 저장' }));

    await waitFor(() => expect(savedBody).not.toBeNull());
    expect(savedBody).toEqual({ morningProduction: { quantity: 0, expectedVersion: null } });
    expect(await screen.findByText('생산·납품 수량을 저장했습니다.')).toBeInTheDocument();
  });

  it('saves a defect quantity with the production permission', async () => {
    let savedBody: Record<string, unknown> | null = null;
    const response: G2RangeResponse = { today: todaySeoul(), from: todaySeoul(), to: todaySeoul(), days: [day()] };
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/g2/days') return json(response);
      if (url.pathname.startsWith('/api/g2/operations/')) { savedBody = JSON.parse(String(init?.body)) as Record<string, unknown>; return json({ saved: true }); }
      return json([]);
    }));

    render(<G2OperationsPage developmentUserKey="dev-manufacturing" canEditProduction canEditDelivery={false} mutationEnabled />);
    fireEvent.change(await screen.findByLabelText(/^불량 수량/u), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: '변경한 값 저장' }));

    await waitFor(() => expect(savedBody).toEqual({ defect: { quantity: 2, expectedVersion: null } }));
  });

  it('uses dates as horizontal columns, keeps forecast labels, and filters the visible range', async () => {
    const response: G2RangeResponse = { today: todaySeoul(), from: '2099-12-29', to: '2099-12-31', days: [day('2099-12-29', true), day('2099-12-30', true), day('2099-12-31', true)] };
    vi.stubGlobal('fetch', vi.fn(async () => json(response)));
    render(<G2OperationsPage developmentUserKey="dev-sales" canEditProduction canEditDelivery mutationEnabled />);
    const table = await screen.findByRole('table', { name: '생산·납품·재고 월간 입력 현황' });
    expect(within(table).getByRole('rowheader', { name: '오전 생산' })).toBeInTheDocument();
    expect(within(table).queryByRole('rowheader', { name: '구분' })).toBeNull();
    expect(within(table).getAllByText('예상')).toHaveLength(3);
    expect(within(table).getByText('12월 29일')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('시작일'), { target: { value: '2099-12-30' } });
    await waitFor(() => expect(within(table).queryByText('12월 29일')).toBeNull());
    expect(within(table).getByText('12월 30일')).toBeInTheDocument();
    expect(within(table).getByText('12월 31일')).toBeInTheDocument();
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
      if (url.pathname === '/api/g2/days') return json(response);
      if (url.pathname === '/api/system/holidays') return json([{ holidayDate: '2026-08-17', name: '광복절 대체공휴일' }]);
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

  it('applies the home date range to both charts and visible tables and discloses attendance details by shift', async () => {
    const inventoryTarget = { targetType: 'Inventory' as const, effectiveDate: '2026-08-01', quantity: 100, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' };
    const days = [
      { ...day('2026-08-17'), morningProduction: metric(10), afternoonProduction: metric(20), productionTotal: 30, delivery: metric(25), inventory: 70, inventoryTarget },
      { ...day('2026-08-18'), morningProduction: metric(20), afternoonProduction: metric(30), productionTotal: 50, delivery: metric(35), inventory: 80, inventoryTarget },
      day('2026-08-19', true)
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/system/holidays') return json([{ holidayDate: '2026-08-17', name: '광복절 대체공휴일' }]);
      return json({ today: '2026-08-18', year: 2026, month: 8, hasInventoryBaseline: true, days });
    }));
    const { container } = render(<G2HomePage developmentUserKey="dev-sales" canManageInventory canManageTargets mutationEnabled />);

    const flowTable = await screen.findByRole('table', { name: '일별 생산·납품·재고 그래프 자료' });
    const productionTable = screen.getByRole('table', { name: '생산 현황' });
    const attendanceTable = screen.getByRole('table', { name: '제조 인원 출근 현황' });
    const flowKpis = screen.getByLabelText('생산·납품·재고 핵심 지표');
    const shiftKpis = screen.getByLabelText('조별 생산 핵심 지표');
    expect(within(flowKpis).getByText('일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('40대');
    expect(within(flowKpis).getByText('일일 납품 평균').closest('.g2-chart-kpi')).toHaveTextContent('30대');
    expect(within(flowKpis).getByText('일일 재고 평균').closest('.g2-chart-kpi')).toHaveTextContent('75대');
    expect(within(flowKpis).getByText('재고 부족분').closest('.g2-chart-kpi')).toHaveTextContent('20대');
    expect(within(flowKpis).getByLabelText('재고 부족분 계산 안내')).toHaveAttribute('aria-describedby');
    expect(within(flowKpis).getByRole('tooltip')).toHaveTextContent('오늘(8월 18일) 기준 재고목표 - 재고');
    expect(within(shiftKpis).getByText('오전조 일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('15대');
    expect(within(shiftKpis).getByText('오후조 일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('25대');
    expect(within(flowTable).getByText('2026-08-17')).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '생산 합계' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '납품 목표' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '납품' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '불량' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '재고' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '재고' }).closest('tr')).toHaveClass('g2-inventory-row');
    expect(within(productionTable).queryByRole('rowheader', { name: '일 생산목표' })).toBeNull();
    expect(within(productionTable).getByText('8월 17일')).toBeInTheDocument();
    expect(within(attendanceTable).getByText('8월 17일')).toBeInTheDocument();
    await waitFor(() => expect(within(productionTable).getByText('8월 17일').closest('th')).toHaveClass('g2-red-day'));
    expect(within(attendanceTable).getByText('8월 17일').closest('th')).toHaveClass('g2-red-day');
    expect(within(productionTable).getByRole('rowheader', { name: '생산 합계' }).closest('tr')?.children[1]).toHaveClass('g2-red-day-column');
    expect(within(attendanceTable).getByRole('button', { name: '8월 17일 오전 합계 미입력 세부 인원 보기' }).closest('td')).toHaveClass('g2-red-day-column');
    expect(container.querySelectorAll('.g2-axis-label.g2-red-day')).toHaveLength(2);
    expect(within(productionTable).queryByText('실적')).toBeNull();
    expect(within(productionTable).getByText('예상')).toBeInTheDocument();
    expect(within(attendanceTable).queryByRole('rowheader', { name: '오전 EMI' })).toBeNull();
    expect(within(attendanceTable).queryByRole('rowheader', { name: '오후 EMI' })).toBeNull();
    expect(within(attendanceTable).getByRole('rowheader', { name: '오전·오후 전체 합계' })).toBeInTheDocument();

    const targetCard = screen.getByRole('heading', { name: '적용 시작일별 목표' }).closest('article');
    const productionCard = screen.getByRole('heading', { name: '생산 현황' }).closest('article');
    expect(targetCard).not.toBeNull();
    expect(productionCard).not.toBeNull();
    expect(productionCard!.compareDocumentPosition(targetCard!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    const morningDisclosure = within(attendanceTable).getByRole('button', { name: '오전 합계 세부 인원 보기' });
    const afternoonDisclosure = within(attendanceTable).getByRole('button', { name: '오후 합계 세부 인원 보기' });
    const morningTotal = within(attendanceTable).getByRole('button', { name: '8월 17일 오전 합계 미입력 세부 인원 보기' });
    expect(morningDisclosure).not.toHaveTextContent('+');
    expect(morningDisclosure.closest('tr')).toHaveClass('g2-summary-row');
    expect(morningTotal).toHaveClass('g2-table-total-button');
    expect(morningDisclosure).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(morningTotal);
    expect(morningTotal).toHaveAttribute('aria-expanded', 'true');
    expect(within(attendanceTable).getByRole('rowheader', { name: '오전 EMI' })).toBeInTheDocument();
    expect(within(attendanceTable).getByRole('rowheader', { name: '오전 도급' })).toBeInTheDocument();
    expect(afternoonDisclosure).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(within(attendanceTable).getByRole('button', { name: '오전 합계 세부 인원 접기' }));
    expect(within(attendanceTable).queryByRole('rowheader', { name: '오전 EMI' })).toBeNull();

    fireEvent.change(screen.getByLabelText('시작일'), { target: { value: '2026-08-18' } });
    await waitFor(() => expect(within(flowTable).queryByText('2026-08-17')).toBeNull());
    expect(within(productionTable).queryByText('8월 17일')).toBeNull();
    expect(within(attendanceTable).queryByText('8월 17일')).toBeNull();
    expect(screen.getByText('2일 표시')).toBeInTheDocument();
    expect(within(flowKpis).getByText('일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('50대');
    expect(within(flowKpis).getByText('일일 납품 평균').closest('.g2-chart-kpi')).toHaveTextContent('35대');
    expect(within(shiftKpis).getByText('오전조 일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('20대');
    expect(within(shiftKpis).getByText('오후조 일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('30대');

    fireEvent.change(screen.getByLabelText('시작일'), { target: { value: '2026-08-19' } });
    await waitFor(() => expect(screen.getByText('1일 표시')).toBeInTheDocument());
    expect(within(flowKpis).getByText('재고 부족분').closest('.g2-chart-kpi')).toHaveTextContent('20대');
  });

  it('blocks blank inventory and target saves while keeping zero available and loads each moved month once', async () => {
    const today = todaySeoul();
    let homeLoads = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/system/holidays') return json([]);
      if (url.pathname === '/api/g2/home') {
        homeLoads += 1;
        const year = Number(url.searchParams.get('year'));
        const month = Number(url.searchParams.get('month'));
        const date = `${year}-${String(month).padStart(2, '0')}-01`;
        return json({ today, year, month, hasInventoryBaseline: false, days: [day(date, date > today)] });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory canManageTargets mutationEnabled />);
    const targetQuantity = await screen.findByLabelText('목표 수량');
    const targetSave = screen.getByRole('button', { name: '목표 저장' });
    expect(targetSave).toBeDisabled();
    fireEvent.change(targetQuantity, { target: { value: '0' } });
    expect(targetSave).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: '실사 입력' }));
    const countSave = screen.getByRole('button', { name: '저장' });
    expect(countSave).toBeDisabled();
    fireEvent.change(screen.getByLabelText('실사 수량'), { target: { value: '0' } });
    expect(countSave).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: '닫기' }));

    expect(homeLoads).toBe(1);
    fireEvent.click(screen.getByRole('button', { name: '다음 달' }));
    await screen.findByLabelText('목표 수량');
    expect(homeLoads).toBe(2);
  });

  it('reuses the loaded month for same-month dates and fetches another month only once', async () => {
    const today = todaySeoul();
    const sameMonthDate = `${today.slice(0, 8)}${today.endsWith('-01') ? '02' : '01'}`;
    const nextMonth = new Date(`${today.slice(0, 7)}-01T00:00:00Z`);
    nextMonth.setUTCMonth(nextMonth.getUTCMonth() + 1);
    const nextMonthDate = nextMonth.toISOString().slice(0, 7) + '-01';
    let dayLoads = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/system/holidays') return json([]);
      if (url.pathname === '/api/g2/days') {
        dayLoads += 1;
        const from = url.searchParams.get('from')!;
        const to = url.searchParams.get('to')!;
        const days = from.slice(0, 7) === today.slice(0, 7)
          ? [day(today), day(sameMonthDate)]
          : [day(nextMonthDate, nextMonthDate > today)];
        return json({ today, from, to, days });
      }
      return json({ title: 'not found' }, 404);
    }));

    render(<G2OperationsPage developmentUserKey="dev-sales" canEditProduction canEditDelivery mutationEnabled />);
    const dateInput = await screen.findByLabelText('입력 날짜');
    expect(dayLoads).toBe(1);
    fireEvent.change(dateInput, { target: { value: sameMonthDate } });
    await waitFor(() => expect(screen.getByLabelText('입력 날짜')).toHaveValue(sameMonthDate));
    expect(dayLoads).toBe(1);

    fireEvent.change(screen.getByLabelText('입력 날짜'), { target: { value: nextMonthDate } });
    await waitFor(() => expect(dayLoads).toBe(2));
    expect(await screen.findByLabelText('입력 날짜')).toHaveValue(nextMonthDate);
  });

  it('ignores an older home response that finishes after the current request', async () => {
    const today = todaySeoul();
    const year = Number(today.slice(0, 4));
    const month = Number(today.slice(5, 7));
    let resolveOlder!: (response: Response) => void;
    let resolveCurrent!: (response: Response) => void;
    const olderResponse = new Promise<Response>(resolve => { resolveOlder = resolve; });
    const currentResponse = new Promise<Response>(resolve => { resolveCurrent = resolve; });
    let homeLoads = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/api/system/holidays') return json([]);
      if (url.pathname === '/api/g2/home') {
        homeLoads += 1;
        return homeLoads === 1 ? olderResponse : currentResponse;
      }
      return json({ title: 'not found' }, 404);
    }));

    const view = render(<G2HomePage developmentUserKey="dev-sales" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    await waitFor(() => expect(homeLoads).toBe(1));
    view.rerender(<G2HomePage developmentUserKey="dev-admin" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    await waitFor(() => expect(homeLoads).toBe(2));

    await act(async () => resolveCurrent(json({
      today, year, month, hasInventoryBaseline: false,
      days: [{ ...day(today), morningProduction: metric(22), productionTotal: 22 }]
    })));
    const productionTable = await screen.findByRole('table', { name: '생산 현황' });
    expect(within(productionTable).getByLabelText(`${Number(today.slice(5, 7))}월 ${Number(today.slice(8, 10))}일 오전 생산 임시 예상값`)).toHaveValue(22);

    await act(async () => resolveOlder(json({
      today, year, month, hasInventoryBaseline: false,
      days: [{ ...day(today), morningProduction: metric(11), productionTotal: 11 }]
    })));
    expect(within(productionTable).getByLabelText(`${Number(today.slice(5, 7))}월 ${Number(today.slice(8, 10))}일 오전 생산 임시 예상값`)).toHaveValue(22);
  });
});
