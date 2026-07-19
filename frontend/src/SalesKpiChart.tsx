import type { SalesKpiMonth } from './salesKpi';
import { formatCompactMoney } from './salesKpiFormat';

export function SalesKpiChart({
  months,
  currency,
  selectedMonth,
  compact = false,
  mobile = false,
  onSelectMonth
}: {
  months: SalesKpiMonth[];
  currency: string;
  selectedMonth?: number;
  compact?: boolean;
  mobile?: boolean;
  onSelectMonth?: (month: number) => void;
}) {
  const values = months.flatMap((month) => [month.revenueAmount, month.targetAmount ?? 0]);
  const max = Math.max(...values, 1);

  if (mobile) {
    return (
      <div className="sales-chart-mobile" role="group" aria-label="12개월 확정 매출과 목표 비교">
        {months.map((month) => {
          const revenueHeight = Math.max((month.revenueAmount / max) * 100, month.revenueAmount > 0 ? 4 : 0);
          const targetPosition = month.targetAmount === null ? null : (month.targetAmount / max) * 100;
          const content = <>
            <span>{month.month}월</span>
            <div className="sales-chart-mobile-plot" aria-hidden="true">
              <i className="sales-chart-mobile-bar" style={{ height: `${revenueHeight}%` }} />
              {targetPosition !== null ? <i className="sales-chart-mobile-target" style={{ bottom: `${targetPosition}%` }} /> : null}
            </div>
            <strong>{formatCompactMoney(month.revenueAmount, currency)}</strong>
            <small>{month.targetAmount === null ? '목표 -' : `목표 ${formatCompactMoney(month.targetAmount, currency)}`}</small>
          </>;
          const label = `${month.month}월 확정 매출 ${formatCompactMoney(month.revenueAmount, currency)}, ${month.targetAmount === null ? '목표 미등록' : `목표 ${formatCompactMoney(month.targetAmount, currency)}`}`;
          return onSelectMonth ? (
            <button key={month.month} type="button" className={month.month === selectedMonth ? 'is-selected' : ''} aria-label={label} onClick={() => onSelectMonth(month.month)}>{content}</button>
          ) : (
            <article key={month.month} aria-label={label}>{content}</article>
          );
        })}
        <div className="sales-chart-legend" aria-hidden="true">
          <span><i data-kind="revenue" />확정 매출</span>
          <span><i data-kind="target" />월 목표</span>
        </div>
      </div>
    );
  }

  const width = 900;
  const height = compact ? 220 : 300;
  const baseline = height - 42;
  const chartHeight = baseline - 24;
  const slot = 68;
  const start = 46;
  const points = months
    .filter((month) => month.targetAmount !== null)
    .map((month) => {
      const x = start + (month.month - 1) * slot + 18;
      const y = baseline - ((month.targetAmount ?? 0) / max) * chartHeight;
      return `${x},${y}`;
    })
    .join(' ');

  return (
    <div className={compact ? 'sales-chart sales-chart--compact' : 'sales-chart'}>
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-labelledby={`sales-chart-title-${compact ? 'compact' : 'full'}`}>
        <title id={`sales-chart-title-${compact ? 'compact' : 'full'}`}>월별 확정 매출 막대와 목표 선 비교</title>
        {[0, 0.5, 1].map((ratio) => {
          const y = baseline - chartHeight * ratio;
          return <line key={ratio} x1="28" x2="875" y1={y} y2={y} className="sales-chart-grid" />;
        })}
        {months.map((month) => {
          const x = start + (month.month - 1) * slot;
          const barHeight = (month.revenueAmount / max) * chartHeight;
          return (
            <g key={month.month}>
              <rect
                x={x}
                y={baseline - barHeight}
                width="36"
                height={Math.max(barHeight, 2)}
                rx="8"
                className={month.month === selectedMonth ? 'sales-chart-bar is-selected' : 'sales-chart-bar'}
              />
              <text x={x + 18} y={baseline + 24} textAnchor="middle" className="sales-chart-label">{month.month}월</text>
              {onSelectMonth ? (
                <rect
                  x={x - 8}
                  y="12"
                  width="52"
                  height={baseline + 25}
                  tabIndex={0}
                  role="button"
                  aria-label={`${month.month}월 확정 매출 ${formatCompactMoney(month.revenueAmount, currency)}, ${month.targetAmount === null ? '목표 미등록' : `목표 ${formatCompactMoney(month.targetAmount, currency)}`}`}
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
        {points ? <polyline points={points} className="sales-chart-target-line" /> : null}
        {months.filter((month) => month.targetAmount !== null).map((month) => {
          const x = start + (month.month - 1) * slot + 18;
          const y = baseline - ((month.targetAmount ?? 0) / max) * chartHeight;
          return <circle key={`target-${month.month}`} cx={x} cy={y} r="5" className="sales-chart-target-dot" />;
        })}
      </svg>
      <div className="sales-chart-legend" aria-hidden="true">
        <span><i data-kind="revenue" />확정 매출</span>
        <span><i data-kind="target" />월 목표</span>
      </div>
      <table className="sr-only">
        <caption>월별 확정 매출과 목표</caption>
        <thead><tr><th>월</th><th>확정 매출</th><th>목표</th></tr></thead>
        <tbody>{months.map((month) => <tr key={month.month}><td>{month.month}월</td><td>{month.revenueAmount}</td><td>{month.targetAmount ?? '미등록'}</td></tr>)}</tbody>
      </table>
    </div>
  );
}
