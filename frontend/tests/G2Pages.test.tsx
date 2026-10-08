import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { G2ProductionDeliveryInventoryChart, G2ShiftProductionChart } from '../src/G2Charts';
import { G2HomePage } from '../src/G2HomePage';
import { applyG2HomePreview } from '../src/G2HomePreview';
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

  it('provides screen-reader tables while keeping future actuals blank and goals visible', () => {
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
    expect(container.querySelector('.g2-line-forecast')).toBeNull();
    expect(container.querySelector('.g2-forecast-zone')).toBeNull();
    expect(container.querySelector('.g2-chart-legend [data-series="forecast"]')).toBeNull();
    const flowData = screen.getByRole('table', { name: '일별 생산·납품·재고 그래프 자료' });
    expect(within(flowData).getByRole('row', { name: /생산 4 미입력/u })).toBeInTheDocument();
    expect(within(screen.getByRole('table', { name: '조별 생산량 그래프 자료' })).getByRole('row', { name: /일 생산목표 미등록 5/u })).toBeInTheDocument();
    expect(container.querySelector('#g2-production-gradient')).toBeInTheDocument();
    expect(container.querySelector('#g2-production-gradient stop')).toHaveAttribute('stop-color', '#60a5fa');
    expect(container.querySelector('#g2-delivery-gradient stop')).toHaveAttribute('stop-color', '#fb923c');
    expect(container.querySelector('#g2-morning-gradient stop')).toHaveAttribute('stop-color', '#bfdbfe');
    expect(container.querySelector('#g2-afternoon-gradient stop')).toHaveAttribute('stop-color', '#60a5fa');
    expect(container.querySelectorAll('.g2-plot-background')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-line-point')).toHaveLength(1);
    expect(container.querySelectorAll('.g2-stack-segments')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-stack-outline')).toHaveLength(1);
    expect(container.querySelectorAll('.g2-baseline')).toHaveLength(2);
    expect(container.querySelector('.g2-bar-production')?.tagName.toLowerCase()).toBe('path');
    expect(container.querySelector('#g2-stack-clip-0 path')).toBeInTheDocument();
    expect(container.querySelectorAll('.g2-line-hit')).toHaveLength(5);
    expect(container.querySelectorAll('.g2-line-halo')).toHaveLength(4);
    expect(container.querySelectorAll('.g2-inventory-value')).toHaveLength(1);
    expect(container.querySelectorAll('.g2-line-point-physical')).toHaveLength(1);
    expect(container.querySelector('.g2-inventory-value-physical')).toHaveTextContent('10');
    expect(container.querySelector('.g2-line-point-physical')).toHaveAttribute('r', '3.2');
    expect(container.querySelector('.g2-line-point:not(.g2-line-point-physical)')).toBeNull();
    expect(container.querySelectorAll('.g2-flow-bar-value')).toHaveLength(2);
    expect(container.querySelectorAll('.g2-chart-scroll')).toHaveLength(2);
    const pairedFlowLabels = container.querySelectorAll('.g2-flow-bar-value[data-series]');
    expect(pairedFlowLabels[0]).not.toHaveAttribute('y', pairedFlowLabels[1].getAttribute('y'));
    expect(container.querySelectorAll('.g2-stack-value-morning')).toHaveLength(1);
    expect(container.querySelectorAll('.g2-stack-value-afternoon')).toHaveLength(1);
    expect(container.querySelectorAll('.g2-stack-total-value')).toHaveLength(0);
    expect(container.querySelectorAll('.g2-stack-hit')).toHaveLength(1);
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
    expect(shiftChart.querySelector('.g2-tooltip-value')).toHaveTextContent('총 생산 평균 4대');
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

  it('does not apply temporary actuals to future dates or treat deleted counts as inventory anchors', () => {
    const days = [
      { ...day('2026-08-01'), physicalCount: { ...metric(100), quantity: 100 }, inventory: 100, defectInventory: 4 },
      { ...day('2026-08-02'), physicalCount: { ...metric(0), quantity: null }, defectInventoryCount: { ...metric(0), quantity: null }, morningProduction: metric(10), productionTotal: 10, delivery: metric(4), defect: metric(2), inventory: 104, defectInventory: 6 },
      { ...day('2026-08-03', true), morningProduction: metric(99), productionTotal: 99, inventory: 999, defectInventory: 999 }
    ];
    const preview = applyG2HomePreview(days, { '2026-08-02': { morningProduction: '20' }, '2026-08-03': { morningProduction: '30' } });
    expect(preview[1].inventory).toBe(114);
    expect(preview[1].defectInventory).toBe(6);
    expect(preview[2]).toBe(days[2]);
  });

  it('applies current delivery today and production with defects to the next day from the cutover', () => {
    const days = [
      { ...day('2026-08-27'), physicalCount: { ...metric(2), quantity: 2 }, morningProduction: metric(34), productionTotal: 34, delivery: metric(99), inventory: 2 },
      { ...day('2026-08-28'), morningProduction: metric(22), afternoonProduction: metric(25), productionTotal: 47, delivery: metric(30), inventory: 6 },
      { ...day('2026-08-29'), delivery: metric(30), inventory: 23 }
    ];

    const preview = applyG2HomePreview(days, {
      '2026-08-28': { morningProduction: '30', delivery: '35', defect: '2' }
    });

    expect(preview[0].inventory).toBe(2);
    expect(preview[1].inventory).toBe(1);
    expect(preview[2].inventory).toBe(24);
  });

  it('recalculates current delivery when a preview range starts on or after the cutover', () => {
    const days = [
      { ...day('2026-08-28'), morningProduction: metric(22), afternoonProduction: metric(25), productionTotal: 47, delivery: metric(30), inventory: 6 },
      { ...day('2026-08-29'), delivery: metric(30), inventory: 23 }
    ];

    const preview = applyG2HomePreview(days, {
      '2026-08-28': { delivery: '35' }
    });

    expect(preview[0].inventory).toBe(1);
    expect(preview[1].inventory).toBe(18);
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
      if (url.pathname === '/cheongju/api/system/holidays') return json([]);
      return json({ today: '2026-08-02', year: 2026, month: 8, hasInventoryBaseline: true, days });
    }));

    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    const productionTable = await screen.findByRole('table', { name: '생산 현황' });
    fireEvent.click(within(productionTable).getByRole('button', { name: '불량 상세 보기' }));
    fireEvent.change(within(productionTable).getByLabelText('8월 2일 신규 불량 임시 계산값'), { target: { value: '3' } });

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

  it('excludes pre-September-10 repairs from available stock while defective stock crosses available counts', () => {
    const days = [
      { ...day('2026-09-01'), inventory: 100, morningProduction: metric(20), productionTotal: 20, defect: metric(4), morningRepair: metric(2), repairTotal: 2, defectInventory: 12 },
      { ...day('2026-09-02'), inventory: 111, delivery: metric(5), defectInventory: 12 },
      { ...day('2026-09-03'), inventory: 50, physicalCount: { ...metric(50), quantity: 50 }, defectInventory: 12 },
      { ...day('2026-09-04'), inventory: 50, defectInventory: 12 }
    ];
    const result = applyG2HomePreview(days, { '2026-09-01': { afternoonRepair: '3' } });
    expect(result.map(item => item.inventory)).toEqual([100, 111, 50, 50]);
    expect(result.map(item => item.defectInventory)).toEqual([9, 9, 9, 9]);
    expect(result[0].repairTotal).toBe(5);
    expect(days[0].afternoonRepair).toBeNull();
    expect(days[0].defectInventory).toBe(12);
    const partial = applyG2HomePreview(days.slice(1), { '2026-09-02': { morningRepair: '2', defect: '1' } });
    expect(partial.map(item => item.defectInventory)).toEqual([11, 11, 11]);
    expect(partial.map(item => item.inventory)).toEqual([111, 50, 50]);
  });

  it('uses all three inventory regimes and starts returning September 9 repairs on September 10', () => {
    const days = [
      { ...day('2026-09-08'), inventory: 100, productionTotal: 20, morningProduction: metric(20), defect: metric(3), repairTotal: 7, morningRepair: metric(7), defectInventory: 20 },
      { ...day('2026-09-09'), inventory: 113, delivery: metric(4), productionTotal: 10, morningProduction: metric(10), defect: metric(2), repairTotal: 5, morningRepair: metric(5), defectInventory: 17 },
      { ...day('2026-09-10'), inventory: 120, delivery: metric(6), defectInventory: 17 }
    ];
    const preview = applyG2HomePreview(days, { '2026-09-08': { morningRepair: '9' }, '2026-09-09': { afternoonRepair: '1' } });
    expect(preview.map(item => item.inventory)).toEqual([100, 113, 121]);
    expect(preview.map(item => item.defectInventory)).toEqual([18, 14, 14]);
    expect(applyG2HomePreview(days.slice(1), { '2026-09-09': { afternoonRepair: '1' } }).map(item => item.inventory)).toEqual([113, 121]);
    expect(applyG2HomePreview(days.slice(2), { '2026-09-10': { delivery: '8' } })[0].inventory).toBe(118);
    const august = [
      { ...day('2026-08-26'), inventory: 50, physicalCount: { ...metric(50), quantity: 50 } },
      { ...day('2026-08-27'), inventory: 56, productionTotal: 10, morningProduction: metric(10), delivery: metric(3), defect: metric(1), repairTotal: 4, morningRepair: metric(4) },
      { ...day('2026-08-28'), inventory: 63, delivery: metric(2) }
    ];
    expect(applyG2HomePreview(august, { '2026-08-27': { morningRepair: '8' } }).map(item => item.inventory)).toEqual([50, 56, 63]);
    expect(applyG2HomePreview(august.slice(1), { '2026-08-27': { morningRepair: '8' } }).map(item => item.inventory)).toEqual([56, 63]);
  });

  it('keeps defective end-of-day counts fixed in full and partial previews, without resetting available stock', () => {
    const days = [
      { ...day('2026-09-10'), inventory: 100, defect: metric(5), defectInventory: 5 },
      { ...day('2026-09-11'), inventory: 95, defect: metric(2), defectInventory: 0, defectInventoryCount: { ...metric(0), quantity: 0 } },
      { ...day('2026-09-12'), inventory: 93, defect: metric(4), morningRepair: metric(1), repairTotal: 1, defectInventory: 3 },
      { ...day('2026-09-13'), inventory: 90, defectInventory: 3 }
    ];
    const preview = applyG2HomePreview(days, { '2026-09-10': { defect: '7' }, '2026-09-11': { defect: '9' }, '2026-09-12': { afternoonRepair: '2' } });
    expect(preview.map(item => item.defectInventory)).toEqual([7, 0, 1, 1]);
    expect(preview.map(item => item.inventory)).toEqual([100, 93, 84, 83]);
    expect(applyG2HomePreview(days.slice(1), { '2026-09-11': { defect: '9' } }).map(item => item.defectInventory)).toEqual([0, 3, 3]);
    expect(days[1].defect?.quantity).toBe(2);
  });

  it('expands each home summary from its header or number and previews repairs without saving', async () => {
    const methods: string[] = [];
    const days = [
      { ...day('2026-09-01'), inventory: 10, morningProduction: metric(7), afternoonProduction: metric(3), productionTotal: 10, defect: metric(2), defectInventory: 5, morningRepair: metric(1), repairTotal: 1 },
      { ...day('2026-09-02'), inventory: 14, delivery: metric(4), defectInventory: 5 }
    ];
    vi.stubGlobal('fetch', vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
      methods.push(init?.method ?? 'GET');
      return json(String(url).includes('/holidays') ? [] : { today: '2026-09-02', year: 2026, month: 9, hasInventoryBaseline: true, days });
    }));
    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    const table = await screen.findByRole('table', { name: '생산 현황' });
    expect(within(table).getAllByRole('row')).toHaveLength(6);
    expect(within(table).queryByRole('spinbutton')).toBeNull();
    fireEvent.click(within(table).getByRole('button', { name: '9월 1일 생산 10대 상세 보기' }));
    expect(within(table).getByLabelText('9월 1일 오전 생산 임시 계산값')).toHaveValue(7);
    fireEvent.click(within(table).getByRole('button', { name: '생산 상세 접기' }));
    expect(within(table).queryByLabelText('9월 1일 오전 생산 임시 계산값')).toBeNull();
    fireEvent.click(within(table).getByRole('button', { name: '수리 상세 보기' }));
    fireEvent.change(within(table).getByLabelText('9월 1일 오후 수리 임시 계산값'), { target: { value: '2' } });
    expect(within(table).getByRole('button', { name: '9월 1일 수리 3대 상세 접기' })).toBeInTheDocument();
    const inventory = within(table).getByRole('rowheader', { name: '재고' }).closest('tr')!;
    expect(within(inventory).getByText('14')).toBeInTheDocument();
    fireEvent.click(within(table).getByRole('button', { name: '납품 상세 보기' }));
    expect(within(table).getByRole('rowheader', { name: '납품 목표' })).toBeInTheDocument();
    expect(within(table).getByLabelText('9월 2일 일일 납품 임시 계산값')).toHaveValue(4);
    fireEvent.click(within(table).getByRole('button', { name: '불량 상세 보기' }));
    const stock = within(table).getByRole('rowheader', { name: '불량재고' }).closest('tr')!;
    expect(within(stock).getAllByText('3')).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: '임시값 초기화' }));
    expect(within(inventory).getByText('14')).toBeInTheDocument();
    expect(within(stock).getAllByText('5')).toHaveLength(2);
    for (let repeat = 0; repeat < 3; repeat += 1) {
      fireEvent.click(within(table).getByRole('button', { name: '수리 상세 접기' }));
      expect(within(table).getAllByRole('row')).toHaveLength(10);
      expect(within(table).getAllByRole('button', { name: '불량 상세 접기' })).toHaveLength(1);
      expect(within(table).getAllByRole('button', { name: '납품 상세 접기' })).toHaveLength(1);
      fireEvent.click(within(table).getByRole('button', { name: '불량 상세 접기' }));
      fireEvent.click(within(table).getByRole('button', { name: '납품 상세 접기' }));
      expect(within(table).getAllByRole('row')).toHaveLength(6);
      fireEvent.click(within(table).getByRole('button', { name: '납품 상세 보기' }));
      fireEvent.click(within(table).getByRole('button', { name: '불량 상세 보기' }));
      fireEvent.click(within(table).getByRole('button', { name: '수리 상세 보기' }));
      expect(within(table).getAllByRole('row')).toHaveLength(12);
    }
    expect(methods.every(method => method === 'GET')).toBe(true);
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

  it('applies the home date range to both charts and visible tables and discloses attendance details by shift', async () => {
    const inventoryTarget = { targetType: 'Inventory' as const, effectiveDate: '2026-08-01', quantity: 100, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' };
    const days = [
      { ...day('2026-08-17'), morningProduction: metric(10), afternoonProduction: metric(20), productionTotal: 30, delivery: metric(25), inventory: 70, inventoryTarget },
      { ...day('2026-08-18'), morningProduction: metric(20), afternoonProduction: metric(30), productionTotal: 50, delivery: metric(35), inventory: 80, inventoryTarget },
      { ...day('2026-08-19', true), morningProduction: metric(99), productionTotal: 99, delivery: metric(88), inventory: 999, morningAttendanceTotal: 9, attendanceTotal: 9, deliveryTarget: { targetType: 'Delivery' as const, effectiveDate: '2026-08-19', quantity: 7, version: 1, updatedAtUtc: '2026-08-17T00:00:00Z', updatedByDisplayName: 'Test User' } }
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/cheongju/api/system/holidays') return json([{ holidayDate: '2026-08-17', name: '광복절 대체공휴일' }]);
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
    expect(within(productionTable).getByRole('button', { name: '생산 상세 보기' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('button', { name: '수리 상세 보기' })).toBeInTheDocument();
    expect(within(productionTable).queryByRole('rowheader', { name: '납품 목표' })).toBeNull();
    expect(within(productionTable).getByRole('button', { name: '납품 상세 보기' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('button', { name: '불량 상세 보기' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '재고' })).toBeInTheDocument();
    expect(within(productionTable).getByRole('rowheader', { name: '재고' }).closest('tr')).toHaveClass('g2-inventory-row');
    expect(within(productionTable).queryByRole('rowheader', { name: '일 생산목표' })).toBeNull();
    expect(within(productionTable).getByText('8월 17일')).toBeInTheDocument();
    expect(within(attendanceTable).getByText('8월 17일')).toBeInTheDocument();
    await waitFor(() => expect(within(productionTable).getByText('8월 17일').closest('th')).toHaveClass('g2-red-day'));
    expect(within(attendanceTable).getByText('8월 17일').closest('th')).toHaveClass('g2-red-day');
    expect(within(productionTable).getByRole('button', { name: '생산 상세 보기' }).closest('tr')?.children[1]).toHaveClass('g2-red-day-column');
    expect(within(attendanceTable).getByRole('button', { name: '8월 17일 오전 합계 미입력 세부 인원 보기' }).closest('td')).toHaveClass('g2-red-day-column');
    expect(container.querySelectorAll('.g2-axis-label.g2-red-day')).toHaveLength(2);
    expect(within(productionTable).queryByText('실적')).toBeNull();
    expect(within(productionTable).queryByText('예상')).toBeNull();
    const futureProduction = within(productionTable).getByRole('button', { name: '8월 19일 생산 미입력 상세 보기' });
    expect(futureProduction).toHaveTextContent('—');
    expect(within(attendanceTable).getByRole('button', { name: '8월 19일 오전 합계 미입력 세부 인원 보기' })).toHaveTextContent('—');
    fireEvent.click(within(productionTable).getByRole('button', { name: '납품 상세 보기' }));
    expect(within(productionTable).getByRole('rowheader', { name: '납품 목표' }).closest('tr')?.children[3]).toHaveTextContent('7');
    fireEvent.click(within(productionTable).getByRole('button', { name: '납품 상세 접기' }));
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
    expect(within(flowKpis).getByText('일일 생산 평균').closest('.g2-chart-kpi')).toHaveTextContent('—');
    expect(within(flowKpis).getByText('일일 재고 평균').closest('.g2-chart-kpi')).toHaveTextContent('—');
  });

  it('blocks blank inventory and target saves while keeping zero available and loads each moved month once', async () => {
    const today = todaySeoul();
    let homeLoads = 0;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname === '/cheongju/api/system/holidays') return json([]);
      if (url.pathname === '/cheongju/api/g2/home') {
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

  it('saves zero defective physical counts, edits and deletes using versions, and keeps errors in the dialog', async () => {
    const date = '2026-09-10';
    let count: G2Day['defectInventoryCount'] = null;
    let conflict = false;
    const writes: Array<{ path: string; method: string; body: unknown; query: string }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.pathname === '/cheongju/api/system/holidays') return json([]);
      if (url.pathname === '/cheongju/api/g2/home') return json({ today: date, year: 2026, month: 9, hasInventoryBaseline: true, days: [
        { ...day(date), inventory: 100, physicalCount: { ...metric(100), quantity: 100, version: 7 }, defectInventory: count?.quantity ?? 3, defectInventoryCount: count }, day('2026-09-11', true)
      ] });
      writes.push({ path: url.pathname, method: init?.method ?? 'GET', body: init?.body ? JSON.parse(String(init.body)) : null, query: url.search });
      if (conflict) return json({ title: '다른 사용자가 변경했습니다. 다시 조회해 주세요.' }, 409);
      if (init?.method === 'DELETE') count = { ...count!, quantity: null, version: count!.version + 1 };
      else { const body = JSON.parse(String(init?.body)); count = { ...metric(body.quantity), quantity: body.quantity, version: (count?.version ?? 0) + 1 }; }
      return json({ saved: true });
    }));
    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory canManageTargets={false} mutationEnabled />);
    fireEvent.click(await screen.findByRole('button', { name: '실사 입력' }));
    expect(screen.getByLabelText('실사 구분')).toHaveValue('available');
    fireEvent.change(screen.getByLabelText('실사 구분'), { target: { value: 'defect' } });
    let dialog = screen.getByRole('dialog', { name: '불량재고 실사 입력' });
    expect(within(dialog).getByLabelText('실사 날짜')).toHaveAttribute('max', date);
    expect(within(dialog).getByRole('button', { name: '저장' })).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('실사 수량'), { target: { value: '0' } });
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(writes[0]).toMatchObject({ path: `/cheongju/api/g2/defect-inventory-counts/${date}`, method: 'PUT', body: { quantity: 0, expectedVersion: null } });
    fireEvent.click(screen.getByRole('button', { name: '불량 상세 보기' }));
    const table = screen.getByRole('table', { name: '생산 현황' });
    expect(within(table).getByText('실사')).toBeInTheDocument();
    expect(within(table).getByRole('rowheader', { name: '재고' }).closest('tr')).toHaveTextContent('100');
    fireEvent.click(screen.getByRole('button', { name: '10일 불량 실사 0대 · 수정' }));
    dialog = screen.getByRole('dialog');
    expect(within(dialog).getByLabelText('실사 수량')).toHaveValue(0);
    fireEvent.change(within(dialog).getByLabelText('실사 구분'), { target: { value: 'available' } });
    expect(within(dialog).getByLabelText('실사 수량')).toHaveValue(100);
    fireEvent.change(within(dialog).getByLabelText('실사 수량'), { target: { value: '99' } });
    fireEvent.change(within(dialog).getByLabelText('실사 구분'), { target: { value: 'defect' } });
    expect(within(dialog).getByLabelText('실사 수량')).toHaveValue(0);
    fireEvent.change(within(dialog).getByLabelText('실사 수량'), { target: { value: '2' } });
    conflict = true;
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(within(dialog).getByRole('status')).toHaveTextContent('다른 사용자가 변경했습니다.'));
    expect(within(dialog).getByLabelText('실사 수량')).toHaveValue(2);
    conflict = false;
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(writes[2].body).toEqual({ quantity: 2, expectedVersion: 1 });
    fireEvent.click(screen.getByRole('button', { name: '10일 불량 실사 2대 · 수정' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '실사 삭제' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(writes[3]).toMatchObject({ method: 'DELETE', query: '?expectedVersion=2' });
    expect(within(table).queryByText('실사')).toBeNull();
    expect(screen.queryByRole('button', { name: '10일 불량 실사 2대 · 수정' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: '실사 입력' }));
    fireEvent.change(screen.getByLabelText('실사 구분'), { target: { value: 'defect' } });
    dialog = screen.getByRole('dialog');
    expect(within(dialog).getByLabelText('실사 수량')).toHaveValue(null);
    expect(within(dialog).queryByRole('button', { name: '실사 삭제' })).toBeNull();
    fireEvent.change(within(dialog).getByLabelText('실사 수량'), { target: { value: '3' } });
    fireEvent.click(within(dialog).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(writes[4]).toMatchObject({ method: 'PUT', body: { quantity: 3, expectedVersion: 3 } });
    expect(writes.every(write => write.path === `/cheongju/api/g2/defect-inventory-counts/${date}`)).toBe(true);
  });

  it('hides defective count actions without permission and disables them in review-safe mode', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => json(String(input).includes('/holidays') ? [] : { today: '2026-09-10', year: 2026, month: 9, hasInventoryBaseline: false, days: [day('2026-09-10')] })));
    const view = render(<G2HomePage developmentUserKey="dev-viewer" canManageInventory={false} canManageTargets={false} mutationEnabled />);
    await screen.findByRole('table', { name: '생산 현황' });
    expect(screen.queryByRole('button', { name: '실사 입력' })).toBeNull();
    view.rerender(<G2HomePage developmentUserKey="dev-viewer" canManageInventory canManageTargets={false} mutationEnabled={false} />);
    expect(screen.getByRole('button', { name: '실사 입력' })).toBeDisabled();
  });

  it('uses one graph entry and saves the selected available count with its own version after switching kinds', async () => {
    const date = '2026-09-10';
    const writes: Array<{ path: string; body: unknown }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      if (init?.method === 'PUT') { writes.push({ path: url.pathname, body: JSON.parse(String(init.body)) }); return json({ saved: true }); }
      return json(url.pathname.includes('/holidays') ? [] : { today: date, year: 2026, month: 9, hasInventoryBaseline: true, days: [
        { ...day(date), inventory: 10, physicalCount: { ...metric(10), quantity: 10, version: 8 }, defectInventory: 0, defectInventoryCount: { ...metric(0), quantity: 0, version: 3 } }
      ] });
    }));
    render(<G2HomePage developmentUserKey="dev-sales" canManageInventory canManageTargets={false} mutationEnabled />);
    const entry = await screen.findByRole('button', { name: '실사 입력' });
    expect(screen.queryByRole('button', { name: '불량재고 실사 입력' })).toBeNull();
    expect(entry.closest('article')).toHaveClass('g2-chart-card');
    expect(screen.getByRole('table', { name: '생산 현황' }).closest('article')).not.toContainElement(entry);
    expect(screen.getByRole('button', { name: '10일 불량 실사 0대 · 수정' }).closest('article')).toBe(entry.closest('article'));
    fireEvent.click(entry);
    fireEvent.change(screen.getByLabelText('실사 구분'), { target: { value: 'defect' } });
    expect(screen.getByLabelText('실사 수량')).toHaveValue(0);
    fireEvent.change(screen.getByLabelText('실사 수량'), { target: { value: '88' } });
    fireEvent.change(screen.getByLabelText('실사 구분'), { target: { value: 'available' } });
    expect(screen.getByLabelText('실사 수량')).toHaveValue(10);
    fireEvent.change(screen.getByLabelText('실사 수량'), { target: { value: '12' } });
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: '저장' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(writes).toEqual([{ path: `/cheongju/api/g2/inventory-counts/${date}`, body: { quantity: 12, expectedVersion: 8 } }]);
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
      if (url.pathname === '/cheongju/api/system/holidays') return json([]);
      if (url.pathname === '/cheongju/api/g2/home') {
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
    fireEvent.click(within(productionTable).getByRole('button', { name: '생산 상세 보기' }));
    expect(within(productionTable).getByLabelText(`${Number(today.slice(5, 7))}월 ${Number(today.slice(8, 10))}일 오전 생산 임시 계산값`)).toHaveValue(22);

    await act(async () => resolveOlder(json({
      today, year, month, hasInventoryBaseline: false,
      days: [{ ...day(today), morningProduction: metric(11), productionTotal: 11 }]
    })));
    expect(within(productionTable).getByLabelText(`${Number(today.slice(5, 7))}월 ${Number(today.slice(8, 10))}일 오전 생산 임시 계산값`)).toHaveValue(22);
  });
});
