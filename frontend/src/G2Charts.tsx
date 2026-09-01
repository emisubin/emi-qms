import { useEffect, useState, type CSSProperties, type MouseEvent as ReactMouseEvent } from 'react';
import type { G2Day } from './g2';
import { EMPTY_G2_HOLIDAYS, isG2RedDay, type G2HolidayMap } from './useG2Holidays';

const WIDTH = 720;
const HEIGHT = 340;
const LEFT = 54;
const TOP = 38;
const BOTTOM = 58;
const PLOT_RIGHT = WIDTH - 58;
const X_LABEL_Y = HEIGHT - BOTTOM + 18;

type ChartScale = {
  min: number;
  max: number;
  ticks: number[];
  hasValues: boolean;
  y: (value: number) => number;
};

type ChartTooltip = {
  x: number;
  y: number;
  date: string;
  label?: string;
  value?: string;
  lines?: Array<{ label: string; value: string }>;
};

function fixedScale(min: number, max: number, step: number): ChartScale {
  const ticks = Array.from({ length: Math.round((max - min) / step) + 1 }, (_, index) => min + index * step);
  const plotHeight = HEIGHT - TOP - BOTTOM;
  return {
    min,
    max,
    ticks,
    hasValues: true,
    y: (value: number) => {
      const clamped = Math.max(min, Math.min(max, value));
      return TOP + ((max - clamped) / (max - min)) * plotHeight;
    }
  };
}

function sharedFlowScale(): ChartScale {
  const min = 0;
  const max = 180;
  const breakValue = 60;
  const lowerHeightRatio = 0.7;
  const ticks = [0, 20, 40, 60, 100, 140, 180];
  const plotHeight = HEIGHT - TOP - BOTTOM;
  const bottom = HEIGHT - BOTTOM;
  const lowerHeight = plotHeight * lowerHeightRatio;
  const upperHeight = plotHeight - lowerHeight;
  return {
    min,
    max,
    ticks,
    hasValues: true,
    y: (value: number) => {
      const clamped = Math.max(min, Math.min(max, value));
      if (clamped <= breakValue) return bottom - (clamped / breakValue) * lowerHeight;
      return bottom - lowerHeight - ((clamped - breakValue) / (max - breakValue)) * upperHeight;
    }
  };
}

function topRoundedBarPath(x: number, top: number, width: number, bottom: number, radius: number) {
  const height = Math.max(0, bottom - top);
  if (height === 0) return '';
  const rounded = Math.min(radius, width / 2, height);
  return `M ${x} ${bottom} V ${top + rounded} Q ${x} ${top} ${x + rounded} ${top} H ${x + width - rounded} Q ${x + width} ${top} ${x + width} ${top + rounded} V ${bottom} Z`;
}

function useMobileChartLayout() {
  const read = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(max-width: 700px)').matches;
  const [mobile, setMobile] = useState(read);
  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return undefined;
    const query = window.matchMedia('(max-width: 700px)');
    const update = () => setMobile(query.matches);
    update();
    query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  }, []);
  return mobile;
}

function chartLayout(dayCount: number, mobile: boolean) {
  if (!mobile || dayCount <= 5) {
    return { width: WIDTH, plotLeft: LEFT, plotRight: PLOT_RIGHT, style: { '--g2-mobile-chart-width': '100%' } as CSSProperties };
  }
  const widthFactor = dayCount / 5;
  const width = (PLOT_RIGHT - LEFT) * widthFactor;
  return { width, plotLeft: 0, plotRight: width, style: { '--g2-mobile-chart-width': `${widthFactor * 100}%` } as CSSProperties };
}

function xAt(index: number, count: number, plotRight = PLOT_RIGHT, plotLeft = LEFT) {
  return plotLeft + ((index + 0.5) / Math.max(1, count)) * (plotRight - plotLeft);
}

function axisValue(value: number) {
  return `${new Intl.NumberFormat('ko-KR', { notation: 'compact', maximumFractionDigits: 1 }).format(value)}대`;
}

function chartQuantity(value: number) {
  return `${new Intl.NumberFormat('ko-KR', { maximumFractionDigits: 1 }).format(value)}대`;
}

function AxisLabels({ scale, side, grid = false, plotRight = PLOT_RIGHT, plotLeft = LEFT }: { scale: ChartScale; side: 'left' | 'right'; grid?: boolean; plotRight?: number; plotLeft?: number }) {
  if (!scale.hasValues) return null;
  return <g aria-hidden="true">{scale.ticks.map(value => <g key={value}>
    {grid ? <line className="g2-grid" x1={plotLeft} x2={plotRight} y1={scale.y(value)} y2={scale.y(value)} /> : null}
    <text className="g2-y-label" x={side === 'left' ? plotLeft - 8 : plotRight + 8} y={scale.y(value) + 4} textAnchor={side === 'left' ? 'end' : 'start'}>{axisValue(value)}</text>
  </g>)}</g>;
}

function MobileChartFrame({ leftScale, leftTitle, rightScale, rightTitle }: { leftScale: ChartScale; leftTitle: string; rightScale?: ChartScale; rightTitle?: string }) {
  const zero = leftScale.y(0);
  return <svg className="g2-mobile-chart-frame-svg" viewBox={`0 0 ${WIDTH} ${HEIGHT}`} aria-hidden="true">
    <rect className="g2-plot-background" x={LEFT} y={TOP} width={PLOT_RIGHT - LEFT} height={HEIGHT - TOP - BOTTOM} rx="12" />
    <text className="g2-axis-title" x={LEFT} y={18}>{leftTitle}</text>
    {rightTitle ? <text className="g2-axis-title" x={PLOT_RIGHT} y={18} textAnchor="end">{rightTitle}</text> : null}
    <AxisLabels scale={leftScale} side="left" grid />
    {rightScale ? <AxisLabels scale={rightScale} side="right" /> : null}
    <line className="g2-axis g2-baseline" x1={LEFT} x2={PLOT_RIGHT} y1={zero} y2={zero} />
  </svg>;
}

function forecastBoundary(index: number, count: number, plotRight = PLOT_RIGHT, plotLeft = LEFT) {
  return plotLeft + (index / Math.max(1, count)) * (plotRight - plotLeft);
}

function shortDate(date: string) {
  const [, month, day] = date.split('-').map(Number);
  return `${month}월 ${day}일`;
}

function pointerIndex<T extends SVGGraphicsElement>(event: ReactMouseEvent<T>, count: number, fallback: number, chartWidth = WIDTH, plotRight = PLOT_RIGHT, plotLeft = LEFT) {
  const svg = event.currentTarget.ownerSVGElement;
  const bounds = svg?.getBoundingClientRect();
  if (!bounds?.width) return fallback;
  const viewX = ((event.clientX - bounds.left) / bounds.width) * chartWidth;
  const ratio = (viewX - plotLeft) / (plotRight - plotLeft);
  return Math.max(0, Math.min(count - 1, Math.floor(ratio * count)));
}

function SvgChartTooltip({ value, plotRight = PLOT_RIGHT, plotLeft = LEFT }: { value: ChartTooltip | null; plotRight?: number; plotLeft?: number }) {
  if (!value) return null;
  const width = 142;
  const lines = value.lines ?? [{ label: value.label ?? '', value: value.value ?? '' }];
  const height = value.lines ? 94 : 56;
  const preferredX = value.x + 12 + width <= plotRight ? value.x + 12 : value.x - width - 12;
  const preferredY = value.y - height - 10 >= TOP ? value.y - height - 10 : value.y + 10;
  const x = Math.max(plotLeft + 4, Math.min(plotRight - width - 4, preferredX));
  const y = Math.max(TOP + 4, Math.min(HEIGHT - BOTTOM - height - 4, preferredY));
  return <g className="g2-tooltip" transform={`translate(${x} ${y})`} aria-hidden="true">
    <rect width={width} height={height} rx="9" />
    <text className="g2-tooltip-date" x="12" y="20">{value.date}</text>
    {lines.map((line, index) => <text className="g2-tooltip-value" x="12" y={42 + index * 19} key={line.label}>{line.label}{value.lines ? ':' : ''} <tspan>{line.value}</tspan></text>)}
  </g>;
}

function linePath(days: G2Day[], read: (day: G2Day) => number | null, y: (value: number) => number, start = 0, end = days.length - 1, plotRight = PLOT_RIGHT, plotLeft = LEFT) {
  let path = '';
  let drawing = false;
  for (let index = start; index <= end; index += 1) {
    const value = read(days[index]);
    if (value === null) { drawing = false; continue; }
    path += `${drawing ? ' L' : 'M'} ${xAt(index, days.length, plotRight, plotLeft)} ${y(value)}`;
    drawing = true;
  }
  return path;
}

function stepPath(days: G2Day[], read: (day: G2Day) => number | null, y: (value: number) => number, plotRight = PLOT_RIGHT, plotLeft = LEFT) {
  let path = '';
  let previous = false;
  days.forEach((day, index) => {
    const value = read(day);
    if (value === null) { previous = false; return; }
    const x = xAt(index, days.length, plotRight, plotLeft);
    path += previous ? ` H ${x} V ${y(value)}` : `M ${x} ${y(value)}`;
    previous = true;
  });
  return path;
}

export function G2ProductionDeliveryInventoryChart({ days, holidays = EMPTY_G2_HOLIDAYS }: { days: G2Day[]; holidays?: G2HolidayMap }) {
  const [tooltip, setTooltip] = useState<ChartTooltip | null>(null);
  const mobile = useMobileChartLayout();
  const layout = chartLayout(days.length, mobile);
  const chartWidth = layout.width;
  const plotLeft = layout.plotLeft;
  const plotRight = layout.plotRight;
  const barScale = sharedFlowScale();
  const inventoryScale = barScale;
  const zero = barScale.y(0);
  const unit = (plotRight - plotLeft) / Math.max(1, days.length);
  const barWidth = Math.max(3.5, Math.min(mobile ? 24 : 10, unit * 0.24));
  const firstFuture = days.findIndex(day => day.isForecast);
  const actualEnd = firstFuture < 0 ? days.length - 1 : firstFuture - 1;
  const futureStart = firstFuture < 0 ? days.length : Math.max(0, firstFuture - 1);
  const inventoryFirst = Math.max(0, days.findIndex(day => day.inventory !== null));
  const targetFirst = Math.max(0, days.findIndex(day => day.inventoryTarget !== null));
  const deliveryTargetFirst = Math.max(0, days.findIndex(day => day.deliveryTarget !== null));
  const showValue = (index: number, label: string, quantity: number, y: number) => setTooltip({
    x: xAt(index, days.length, plotRight, plotLeft), y, date: shortDate(days[index].date), label, value: `${quantity}대`
  });
  const showInventory = (index: number) => {
    const day = days[index];
    if (!day || day.inventory === null) { setTooltip(null); return; }
    showValue(index, day.isForecast ? '예상 재고' : '재고', day.inventory, inventoryScale.y(day.inventory));
  };
  const showInventoryTarget = (index: number) => {
    const day = days[index];
    const quantity = day?.inventoryTarget?.quantity;
    if (quantity === undefined) { setTooltip(null); return; }
    showValue(index, '재고 목표', quantity, inventoryScale.y(quantity));
  };
  const showDeliveryTarget = (index: number) => {
    const day = days[index];
    const quantity = day?.deliveryTarget?.quantity;
    if (quantity === undefined) { setTooltip(null); return; }
    showValue(index, '납품 목표', quantity, barScale.y(quantity));
  };
  const inventoryLabelY = (day: G2Day, index: number) => {
    if (day.inventory === null) return 0;
    const pointY = inventoryScale.y(day.inventory);
    const above = Math.max(27, pointY - 8);
    const below = Math.min(zero - 5, pointY + 12);
    const barLabelYs = [day.productionTotal, day.delivery?.quantity]
      .filter((quantity): quantity is number => quantity !== null && quantity !== undefined && quantity > 0)
      .map(quantity => barScale.y(quantity) - 5);
    if (barLabelYs.some(labelY => Math.abs(labelY - above) < 11)) return below;
    return index % 2 === 1 && barLabelYs.some(labelY => Math.abs(labelY - pointY) < 14) ? below : above;
  };
  const flowBarLabelY = (production: number, delivery: number, series: 'production' | 'delivery') => {
    const quantity = series === 'production' ? production : delivery;
    const baseY = barScale.y(quantity) - 4;
    if (production <= 0 || delivery <= 0) return baseY;
    const labelsAreClose = Math.abs(barScale.y(production) - barScale.y(delivery)) < 8;
    return labelsAreClose && series === 'delivery' ? baseY - 12 : baseY;
  };

  return (
    <div className="g2-chart-wrap">
      <div className="g2-chart-legend" aria-hidden="true"><span data-series="production">생산</span><span data-series="delivery">납품</span><span data-series="delivery-target">납품 목표</span><span data-series="inventory">재고</span><span data-series="target">재고 목표</span><span data-series="forecast">예상 재고(점선)</span></div>
      <div className={`g2-chart-stage${mobile ? ' g2-chart-stage-mobile' : ''}`}>
      {mobile ? <MobileChartFrame leftScale={barScale} leftTitle="생산·납품 (대)" rightScale={inventoryScale} rightTitle="재고 (대)" /> : null}
      <div className="g2-chart-scroll" role="region" aria-label="일별 생산·납품·재고 그래프 가로 탐색" tabIndex={0}>
      <svg className="g2-chart" style={layout.style} viewBox={`0 0 ${chartWidth} ${HEIGHT}`} role="img" aria-labelledby="g2-flow-title g2-flow-desc">
        <title id="g2-flow-title">일별 생산, 납품, 재고 추이</title>
        <desc id="g2-flow-desc">생산·납품 막대와 재고·재고 목표선은 큰 숫자가 항상 더 높게 보이는 공통 축을 사용하며 미래 재고는 점선입니다.</desc>
        <defs>
          <linearGradient id="g2-production-gradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#60a5fa" /><stop offset="1" stopColor="#bfdbfe" /></linearGradient>
          <linearGradient id="g2-delivery-gradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#fb923c" /><stop offset="1" stopColor="#fed7aa" /></linearGradient>
        </defs>
        {!mobile ? <rect className="g2-plot-background" x={plotLeft} y={TOP} width={plotRight - plotLeft} height={HEIGHT - TOP - BOTTOM} rx="12" /> : null}
        {firstFuture >= 0 ? <rect className="g2-forecast-zone" x={forecastBoundary(firstFuture, days.length, plotRight, plotLeft)} y={TOP} width={plotRight - forecastBoundary(firstFuture, days.length, plotRight, plotLeft)} height={HEIGHT - TOP - BOTTOM} /> : null}
        {firstFuture >= 0 ? <text className="g2-forecast-label" x={forecastBoundary(firstFuture, days.length, plotRight, plotLeft) + 8} y={TOP + 16}>예상 구간</text> : null}
        {!mobile ? <>
          <text className="g2-axis-title" x={plotLeft} y={18}>생산·납품 (대)</text>
          <text className="g2-axis-title" x={plotRight} y={18} textAnchor="end">재고 (대)</text>
          <AxisLabels scale={barScale} side="left" grid plotRight={plotRight} plotLeft={plotLeft} />
          <AxisLabels scale={inventoryScale} side="right" plotRight={plotRight} plotLeft={plotLeft} />
          <line className="g2-axis g2-baseline" x1={plotLeft} x2={plotRight} y1={zero} y2={zero} />
        </> : null}
        {days.map((day, index) => {
          const center = xAt(index, days.length, plotRight, plotLeft);
          const production = day.productionTotal ?? 0;
          const delivery = day.delivery?.quantity ?? 0;
          return <g key={day.date}>
            <path className="g2-bar g2-bar-production" d={topRoundedBarPath(center - barWidth - 1, barScale.y(production), barWidth, zero, 3)} onMouseEnter={() => showValue(index, '생산', production, barScale.y(production))} onMouseLeave={() => setTooltip(null)}><title>{Number(day.date.slice(-2))}일 생산 {production}대</title></path>
            <path className="g2-bar g2-bar-delivery" d={topRoundedBarPath(center + 1, barScale.y(delivery), barWidth, zero, 3)} onMouseEnter={() => showValue(index, '납품', delivery, barScale.y(delivery))} onMouseLeave={() => setTooltip(null)}><title>{Number(day.date.slice(-2))}일 납품 {delivery}대</title></path>
            {production > 0 ? <text className="g2-bar-value g2-flow-bar-value" data-series="production" x={center - barWidth / 2 - 1} y={flowBarLabelY(production, delivery, 'production')} textAnchor="middle">{production}</text> : null}
            {delivery > 0 ? <text className="g2-bar-value g2-flow-bar-value" data-series="delivery" x={center + barWidth / 2 + 1} y={flowBarLabelY(production, delivery, 'delivery')} textAnchor="middle">{delivery}</text> : null}
            <text className={`g2-axis-label${isG2RedDay(day.date, holidays) ? ' g2-red-day' : ''}`} x={center} y={X_LABEL_Y} textAnchor="middle">{Number(day.date.slice(-2))}일</text>
          </g>;
        })}
        <path className="g2-line g2-line-halo" d={linePath(days, day => day.inventory, inventoryScale.y, 0, actualEnd, plotRight, plotLeft)} aria-hidden="true" />
        <path className="g2-line g2-line-inventory" d={linePath(days, day => day.inventory, inventoryScale.y, 0, actualEnd, plotRight, plotLeft)} />
        {futureStart < days.length ? <><path className="g2-line g2-line-halo g2-line-forecast" d={linePath(days, day => day.inventory, inventoryScale.y, futureStart, days.length - 1, plotRight, plotLeft)} aria-hidden="true" /><path className="g2-line g2-line-inventory g2-line-forecast" d={linePath(days, day => day.inventory, inventoryScale.y, futureStart, days.length - 1, plotRight, plotLeft)} /></> : null}
        <path className="g2-line g2-line-halo g2-line-delivery-target" d={stepPath(days, day => day.deliveryTarget?.quantity ?? null, barScale.y, plotRight, plotLeft)} aria-hidden="true" />
        <path className="g2-line g2-line-delivery-target" d={stepPath(days, day => day.deliveryTarget?.quantity ?? null, barScale.y, plotRight, plotLeft)} />
        <path className="g2-line g2-line-halo g2-line-target" d={stepPath(days, day => day.inventoryTarget?.quantity ?? null, inventoryScale.y, plotRight, plotLeft)} aria-hidden="true" />
        <path className="g2-line g2-line-target" d={stepPath(days, day => day.inventoryTarget?.quantity ?? null, inventoryScale.y, plotRight, plotLeft)} />
        <path className="g2-line-hit" data-series="inventory" d={linePath(days, day => day.inventory, inventoryScale.y, 0, days.length - 1, plotRight, plotLeft)} onMouseEnter={event => showInventory(pointerIndex(event, days.length, inventoryFirst, chartWidth, plotRight, plotLeft))} onMouseMove={event => showInventory(pointerIndex(event, days.length, inventoryFirst, chartWidth, plotRight, plotLeft))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" />
        <path className="g2-line-hit" data-series="inventory-target" d={stepPath(days, day => day.inventoryTarget?.quantity ?? null, inventoryScale.y, plotRight, plotLeft)} onMouseEnter={event => showInventoryTarget(pointerIndex(event, days.length, targetFirst, chartWidth, plotRight, plotLeft))} onMouseMove={event => showInventoryTarget(pointerIndex(event, days.length, targetFirst, chartWidth, plotRight, plotLeft))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" />
        <path className="g2-line-hit" data-series="delivery-target" d={stepPath(days, day => day.deliveryTarget?.quantity ?? null, barScale.y, plotRight, plotLeft)} onMouseEnter={event => showDeliveryTarget(pointerIndex(event, days.length, deliveryTargetFirst, chartWidth, plotRight, plotLeft))} onMouseMove={event => showDeliveryTarget(pointerIndex(event, days.length, deliveryTargetFirst, chartWidth, plotRight, plotLeft))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" />
        {days.map((day, index) => day.inventory !== null ? <g key={`inventory-${day.date}`}>
          {day.physicalCount !== null ? <circle className="g2-physical-point-hit" cx={xAt(index, days.length, plotRight, plotLeft)} cy={inventoryScale.y(day.inventory)} r="9" onMouseEnter={() => showValue(index, '실사', day.physicalCount!.quantity, inventoryScale.y(day.inventory!))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" /> : null}
          <circle className={`g2-line-point${day.isForecast ? ' g2-line-point-forecast' : ''}${day.physicalCount !== null ? ' g2-line-point-physical' : ''}`} cx={xAt(index, days.length, plotRight, plotLeft)} cy={inventoryScale.y(day.inventory)} r={day.physicalCount !== null ? 3.2 : 2.5} onMouseEnter={() => showValue(index, day.physicalCount !== null ? '실사' : day.isForecast ? '예상 재고' : '재고', day.physicalCount?.quantity ?? day.inventory!, inventoryScale.y(day.inventory!))} onMouseLeave={() => setTooltip(null)}><title>{Number(day.date.slice(-2))}일 {day.physicalCount !== null ? '실사' : '재고'} {day.physicalCount?.quantity ?? day.inventory}대</title></circle>
          <text className={`g2-inventory-value${day.physicalCount !== null ? ' g2-inventory-value-physical' : ''}`} x={xAt(index, days.length, plotRight, plotLeft)} y={inventoryLabelY(day, index)} textAnchor="middle">{day.inventory}</text>
        </g> : null)}
        <SvgChartTooltip value={tooltip} plotRight={plotRight} plotLeft={plotLeft} />
      </svg>
      </div>
      </div>
      <div className="sr-only"><table><caption>일별 생산·납품·재고 그래프 자료</caption><thead><tr><th>항목</th>{days.map(day => <th key={day.date}>{day.date}</th>)}</tr></thead><tbody><tr><th>구분</th>{days.map(day => <td key={day.date}>{day.isForecast ? '예상' : '실적'}</td>)}</tr><tr><th>생산</th>{days.map(day => <td key={day.date}>{day.productionTotal ?? '미입력'}</td>)}</tr><tr><th>납품 목표</th>{days.map(day => <td key={day.date}>{day.deliveryTarget?.quantity ?? '미등록'}</td>)}</tr><tr><th>납품</th>{days.map(day => <td key={day.date}>{day.delivery?.quantity ?? '미입력'}</td>)}</tr><tr><th>불량</th>{days.map(day => <td key={day.date}>{day.defect?.quantity ?? '미입력'}</td>)}</tr><tr><th>재고</th>{days.map(day => <td key={day.date}>{day.inventory ?? '기준 없음'}</td>)}</tr><tr><th>재고 목표</th>{days.map(day => <td key={day.date}>{day.inventoryTarget?.quantity ?? '미등록'}</td>)}</tr><tr><th>실사</th>{days.map(day => <td key={day.date}>{day.physicalCount?.quantity ?? '아님'}</td>)}</tr></tbody></table></div>
    </div>
  );
}

export function G2ShiftProductionChart({ days, holidays = EMPTY_G2_HOLIDAYS }: { days: G2Day[]; holidays?: G2HolidayMap }) {
  const [tooltip, setTooltip] = useState<ChartTooltip | null>(null);
  const mobile = useMobileChartLayout();
  const layout = chartLayout(days.length, mobile);
  const chartWidth = layout.width;
  const plotLeft = layout.plotLeft;
  const plotRight = layout.plotRight;
  const scale = fixedScale(0, 60, 10);
  const zero = scale.y(0);
  const unit = (plotRight - plotLeft) / Math.max(1, days.length);
  const barWidth = Math.max(6, Math.min(mobile ? 40 : 18, unit * 0.55));
  const firstFuture = days.findIndex(day => day.isForecast);
  const targetFirst = Math.max(0, days.findIndex(day => day.dailyProductionTarget !== null));
  const productionValues = days.flatMap(day => day.productionTotal === null ? [] : [day.productionTotal]);
  const productionAverage = productionValues.length === 0 ? null : productionValues.reduce((sum, quantity) => sum + quantity, 0) / productionValues.length;
  const showValue = (index: number, label: string, quantity: number, y: number) => setTooltip({
    x: xAt(index, days.length, plotRight, plotLeft), y, date: shortDate(days[index].date), label, value: chartQuantity(quantity)
  });
  const showProductionTarget = (index: number) => {
    const day = days[index];
    const quantity = day?.dailyProductionTarget?.quantity;
    if (quantity === undefined) { setTooltip(null); return; }
    showValue(index, '일 생산목표', quantity, scale.y(quantity));
  };
  const showDailyProduction = (index: number) => {
    const day = days[index];
    if (!day) { setTooltip(null); return; }
    const morning = day.morningProduction?.quantity ?? 0;
    const afternoon = day.afternoonProduction?.quantity ?? 0;
    const total = day.productionTotal ?? morning + afternoon;
    setTooltip({
      x: xAt(index, days.length, plotRight, plotLeft),
      y: scale.y(total),
      date: shortDate(day.date),
      lines: [
        { label: '오전', value: chartQuantity(morning) },
        { label: '오후', value: chartQuantity(afternoon) },
        { label: '전체', value: chartQuantity(total) }
      ]
    });
  };
  const showProductionAverage = (index: number) => {
    if (productionAverage === null) { setTooltip(null); return; }
    setTooltip({
      x: xAt(index, days.length, plotRight, plotLeft),
      y: scale.y(productionAverage),
      date: '선택 기간',
      label: '총 생산 평균',
      value: chartQuantity(productionAverage)
    });
  };
  return (
    <div className="g2-chart-wrap">
      <div className="g2-chart-legend" aria-hidden="true"><span data-series="morning">오전조</span><span data-series="afternoon">오후조</span><span data-series="production-average">총 생산 평균</span><span data-series="production-target">일 생산목표</span></div>
      <div className={`g2-chart-stage${mobile ? ' g2-chart-stage-mobile' : ''}`}>
      {mobile ? <MobileChartFrame leftScale={scale} leftTitle="조별 생산 (대)" /> : null}
      <div className="g2-chart-scroll" role="region" aria-label="조별 생산량 그래프 가로 탐색" tabIndex={0}>
      <svg className="g2-chart" style={layout.style} viewBox={`0 0 ${chartWidth} ${HEIGHT}`} role="img" aria-labelledby="g2-shift-title g2-shift-desc">
        <title id="g2-shift-title">오전조와 오후조 생산량</title><desc id="g2-shift-desc">오전조 막대 위에 오후조 막대를 누적하고 한 날짜의 막대에 마우스를 올리면 오전, 오후, 전체 생산량을 함께 표시하며 총 생산 평균과 일 생산목표를 선으로 표시합니다.</desc>
        <defs>
          <linearGradient id="g2-morning-gradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#bfdbfe" /><stop offset="1" stopColor="#dbeafe" /></linearGradient>
          <linearGradient id="g2-afternoon-gradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#60a5fa" /><stop offset="1" stopColor="#93c5fd" /></linearGradient>
          {days.map((day, index) => {
            const total = (day.morningProduction?.quantity ?? 0) + (day.afternoonProduction?.quantity ?? 0);
            return <clipPath id={`g2-stack-clip-${index}`} key={day.date}><path d={topRoundedBarPath(xAt(index, days.length, plotRight, plotLeft) - barWidth / 2, scale.y(total), barWidth, zero, 4)} /></clipPath>;
          })}
        </defs>
        {!mobile ? <rect className="g2-plot-background" x={plotLeft} y={TOP} width={plotRight - plotLeft} height={HEIGHT - TOP - BOTTOM} rx="12" /> : null}
        {firstFuture >= 0 ? <rect className="g2-forecast-zone" x={forecastBoundary(firstFuture, days.length, plotRight, plotLeft)} y={TOP} width={plotRight - forecastBoundary(firstFuture, days.length, plotRight, plotLeft)} height={HEIGHT - TOP - BOTTOM} /> : null}
        {firstFuture >= 0 ? <text className="g2-forecast-label" x={forecastBoundary(firstFuture, days.length, plotRight, plotLeft) + 8} y={TOP + 16}>예상 구간</text> : null}
        {!mobile ? <>
          <text className="g2-axis-title" x={plotLeft} y={18}>조별 생산 (대)</text>
          <AxisLabels scale={scale} side="left" grid plotRight={plotRight} plotLeft={plotLeft} />
          <line className="g2-axis g2-baseline" x1={plotLeft} x2={plotRight} y1={zero} y2={zero} />
        </> : null}
        {productionAverage !== null ? <>
          <line className="g2-line g2-line-halo g2-line-production-average" x1={plotLeft} x2={plotRight} y1={scale.y(productionAverage)} y2={scale.y(productionAverage)} aria-hidden="true" />
          <line className="g2-line g2-line-production-average" x1={plotLeft} x2={plotRight} y1={scale.y(productionAverage)} y2={scale.y(productionAverage)} />
          <line className="g2-line-hit" data-series="production-average" x1={plotLeft} x2={plotRight} y1={scale.y(productionAverage)} y2={scale.y(productionAverage)} onMouseEnter={event => showProductionAverage(pointerIndex(event, days.length, 0, chartWidth, plotRight, plotLeft))} onMouseMove={event => showProductionAverage(pointerIndex(event, days.length, 0, chartWidth, plotRight, plotLeft))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" />
        </> : null}
        {days.map((day, index) => {
          const morning = day.morningProduction?.quantity ?? 0;
          const afternoon = day.afternoonProduction?.quantity ?? 0;
          const center = xAt(index, days.length, plotRight, plotLeft);
          const total = morning + afternoon;
          return <g key={day.date}>
            <g className="g2-stack-segments" clipPath={`url(#g2-stack-clip-${index})`}>
              <rect className="g2-bar g2-bar-morning" x={center - barWidth / 2} y={scale.y(morning)} width={barWidth} height={zero - scale.y(morning)} />
              <rect className="g2-bar g2-bar-afternoon" x={center - barWidth / 2} y={scale.y(total)} width={barWidth} height={scale.y(morning) - scale.y(total)} />
            </g>
            {total > 0 ? <path className="g2-stack-outline" d={topRoundedBarPath(center - barWidth / 2, scale.y(total), barWidth, zero, 4)} aria-hidden="true" /> : null}
            {total > 0 ? <rect className="g2-stack-hit" data-date={day.date} x={center - barWidth / 2} y={scale.y(total)} width={barWidth} height={zero - scale.y(total)} onMouseEnter={() => showDailyProduction(index)} onMouseLeave={() => setTooltip(null)}><title>{Number(day.date.slice(-2))}일 오전 {morning}대, 오후 {afternoon}대, 전체 {total}대</title></rect> : null}
            {morning > 0 ? <text className="g2-stack-value g2-stack-value-morning" x={center} y={(scale.y(morning) + zero) / 2} textAnchor="middle" dominantBaseline="middle">{morning}</text> : null}
            {afternoon > 0 ? <text className="g2-stack-value g2-stack-value-afternoon" x={center} y={(scale.y(total) + scale.y(morning)) / 2} textAnchor="middle" dominantBaseline="middle">{afternoon}</text> : null}
            <text className={`g2-axis-label${isG2RedDay(day.date, holidays) ? ' g2-red-day' : ''}`} x={center} y={X_LABEL_Y} textAnchor="middle">{Number(day.date.slice(-2))}일</text>
          </g>;
        })}
        <path className="g2-line g2-line-production-target" d={stepPath(days, day => day.dailyProductionTarget?.quantity ?? null, scale.y, plotRight, plotLeft)} />
        <path className="g2-line-hit" data-series="production-target" d={stepPath(days, day => day.dailyProductionTarget?.quantity ?? null, scale.y, plotRight, plotLeft)} onMouseEnter={event => showProductionTarget(pointerIndex(event, days.length, targetFirst, chartWidth, plotRight, plotLeft))} onMouseMove={event => showProductionTarget(pointerIndex(event, days.length, targetFirst, chartWidth, plotRight, plotLeft))} onMouseLeave={() => setTooltip(null)} aria-hidden="true" />
        <SvgChartTooltip value={tooltip} plotRight={plotRight} plotLeft={plotLeft} />
      </svg>
      </div>
      </div>
      <div className="sr-only"><table><caption>조별 생산량 그래프 자료</caption><thead><tr><th>항목</th>{days.map(day => <th key={day.date}>{day.date}</th>)}</tr></thead><tbody><tr><th>오전조</th>{days.map(day => <td key={day.date}>{day.morningProduction?.quantity ?? '미입력'}</td>)}</tr><tr><th>오후조</th>{days.map(day => <td key={day.date}>{day.afternoonProduction?.quantity ?? '미입력'}</td>)}</tr><tr><th>합계</th>{days.map(day => <td key={day.date}>{day.productionTotal ?? '미입력'}</td>)}</tr><tr><th>총 생산 평균</th><td colSpan={Math.max(1, days.length)}>{productionAverage === null ? '계산할 값 없음' : chartQuantity(productionAverage)}</td></tr><tr><th>일 생산목표</th>{days.map(day => <td key={day.date}>{day.dailyProductionTarget?.quantity ?? '미등록'}</td>)}</tr></tbody></table></div>
    </div>
  );
}
