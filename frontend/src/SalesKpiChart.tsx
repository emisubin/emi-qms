import type { SalesKpiMonth } from './salesKpi';
import { formatCompactMoney } from './salesKpiFormat';

export function SalesKpiChart({
  months,
  currency,
  year,
  selectedMonth,
  compact = false,
  mobile = false,
  onSelectMonth
}: {
  months: SalesKpiMonth[];
  currency: string;
  year?: number;
  selectedMonth?: number;
  compact?: boolean;
  mobile?: boolean;
  onSelectMonth?: (month: number) => void;
}) {
  const now = new Date();
  const elapsedMonth = year === undefined || year < now.getFullYear()
    ? 12
    : year === now.getFullYear()
      ? now.getMonth() + 1
      : 0;
  const moneyValues = months.flatMap((month) => [month.revenueAmount, month.targetAmount ?? 0]);
  const maxMoney = Math.max(...moneyValues, 1);
  const attainmentValues = months.map((month) => (
    month.month <= elapsedMonth && month.targetAmount !== null && month.targetAmount > 0
      ? (month.revenueAmount / month.targetAmount) * 100
      : null
  ));
  const rawMaxAttainment = Math.max(100, ...attainmentValues.map((value) => value ?? 0));
  const maxAttainment = Math.min(200, Math.ceil(rawMaxAttainment / 25) * 25);

  const width = mobile ? 360 : 940;
  const height = mobile ? 226 : compact ? 220 : 302;
  const margin = mobile
    ? { top: 25, right: 30, bottom: 36, left: 30 }
    : { top: 30, right: 54, bottom: 40, left: 58 };
  const plotWidth = width - margin.left - margin.right;
  const plotHeight = height - margin.top - margin.bottom;
  const baseline = margin.top + plotHeight;
  const slot = plotWidth / 12;
  const barWidth = mobile ? 7 : compact ? 13 : 17;
  const groupGap = mobile ? 1.5 : 3;
  const monthX = (index: number) => margin.left + slot * index + slot / 2;
  const attainmentY = (value: number) => baseline - (Math.min(value, maxAttainment) / maxAttainment) * plotHeight;
  const selected = selectedMonth ? months.find((month) => month.month === selectedMonth) : undefined;
  const selectedAttainment = selected?.targetAmount && selected.targetAmount > 0
    ? (selected.revenueAmount / selected.targetAmount) * 100
    : null;

  let attainmentPath = '';
  let isOpen = false;
  attainmentValues.forEach((value, index) => {
    if (value === null) {
      isOpen = false;
      return;
    }
    attainmentPath += `${isOpen ? ' L' : ' M'} ${monthX(index)} ${attainmentY(value)}`;
    isOpen = true;
  });

  return (
    <div
      className={`sales-chart ${compact ? 'sales-chart--compact' : ''} ${mobile ? 'sales-chart--mobile' : ''}`.trim()}
      role="group"
      aria-label="12개월 확정 매출·목표·달성률 그래프"
    >
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="월별 확정 매출과 목표 막대, 달성률 선 비교">
        {[0, 0.5, 1].map((ratio) => {
          const y = baseline - plotHeight * ratio;
          return (
            <g key={ratio}>
              <line x1={margin.left} x2={width - margin.right} y1={y} y2={y} className="sales-chart-grid" />
              {ratio > 0 ? <text x={margin.left - 5} y={y + 3} textAnchor="end" className="sales-chart-axis-label">{formatCompactMoney(maxMoney * ratio, currency)}</text> : null}
            </g>
          );
        })}

        <line
          x1={margin.left}
          x2={width - margin.right}
          y1={attainmentY(100)}
          y2={attainmentY(100)}
          className="sales-chart-attainment-reference"
        />
        <text x={width - margin.right + 4} y={attainmentY(100) + 3} className="sales-chart-axis-label sales-chart-axis-label--percent">100%</text>

        {months.map((month, index) => {
          const center = monthX(index);
          const revenueHeight = Math.max((month.revenueAmount / maxMoney) * plotHeight, month.revenueAmount > 0 ? 2 : 0);
          const targetHeight = month.targetAmount === null ? 0 : Math.max((month.targetAmount / maxMoney) * plotHeight, month.targetAmount > 0 ? 2 : 0);
          const label = `${month.month}월 확정 매출 ${formatCompactMoney(month.revenueAmount, currency)}, ${month.targetAmount === null ? '목표 미등록' : `목표 ${formatCompactMoney(month.targetAmount, currency)}`}, ${attainmentValues[index] === null ? '달성률 계산 안 함' : `달성률 ${attainmentValues[index]!.toFixed(1)}%`}`;
          return (
            <g key={month.month} className={month.month === selectedMonth ? 'is-selected' : undefined}>
              <rect
                x={center - groupGap / 2 - barWidth}
                y={baseline - revenueHeight}
                width={barWidth}
                height={Math.max(revenueHeight, 1)}
                rx={mobile ? 1.5 : 3}
                className="sales-chart-bar sales-chart-bar--revenue"
              />
              {month.targetAmount !== null ? (
                <rect
                  x={center + groupGap / 2}
                  y={baseline - targetHeight}
                  width={barWidth}
                  height={Math.max(targetHeight, 1)}
                  rx={mobile ? 1.5 : 3}
                  className="sales-chart-bar sales-chart-bar--target"
                />
              ) : null}
              <text x={center} y={baseline + (mobile ? 17 : 23)} textAnchor="middle" className="sales-chart-label">{month.month}</text>
              {onSelectMonth && !mobile ? (
                <rect
                  x={center - slot / 2}
                  y={margin.top - 12}
                  width={slot}
                  height={plotHeight + 38}
                  tabIndex={0}
                  role="button"
                  aria-label={label}
                  className="sales-chart-hit"
                  onClick={() => onSelectMonth(month.month)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') onSelectMonth(month.month);
                  }}
                />
              ) : null}
            </g>
          );
        })}

        {attainmentPath ? <path d={attainmentPath} className="sales-chart-attainment-line" /> : null}
        {attainmentValues.map((value, index) => value === null ? null : (
          <circle
            key={`attainment-${months[index].month}`}
            cx={monthX(index)}
            cy={attainmentY(value)}
            r={mobile ? 2.4 : 4}
            className="sales-chart-attainment-dot"
          />
        ))}
      </svg>

      <div className="sales-chart-legend" aria-hidden="true">
        <span><i data-kind="revenue" />확정 매출</span>
        <span><i data-kind="target" />월 목표</span>
        <span><i data-kind="attainment" />월 달성률</span>
      </div>

      {selected ? (
        <div className="sales-chart-selection" aria-live="polite">
          <strong>{selected.month}월</strong>
          <span>매출 {formatCompactMoney(selected.revenueAmount, currency)}</span>
          <span>{selected.targetAmount === null ? '목표 미등록' : `목표 ${formatCompactMoney(selected.targetAmount, currency)}`}</span>
          <b>{selectedAttainment === null ? '달성률 -' : `달성률 ${selectedAttainment.toFixed(1)}%`}</b>
        </div>
      ) : null}

      <table className="sr-only">
        <caption>월별 확정 매출, 목표와 달성률</caption>
        <thead><tr><th>월</th><th>확정 매출</th><th>목표</th><th>달성률</th></tr></thead>
        <tbody>{months.map((month, index) => <tr key={month.month}><td>{month.month}월</td><td>{month.revenueAmount}</td><td>{month.targetAmount ?? '미등록'}</td><td>{attainmentValues[index] === null ? '계산 안 함' : `${attainmentValues[index]!.toFixed(1)}%`}</td></tr>)}</tbody>
      </table>
    </div>
  );
}
