import { useState, type ReactNode } from 'react';
import { G2FilteredHorizontalTable, G2HorizontalTable, type G2HorizontalRow } from './G2DataViews';
import { formatG2Date, type G2Day } from './g2';
import type { G2PreviewField } from './G2HomePreview';
import type { G2HolidayMap } from './useG2Holidays';

type Group = 'production' | 'repair' | 'delivery' | 'defect';

export function G2ProductionSummaryTable({ days, holidays, input, monthly = false }: {
  days: G2Day[];
  holidays: G2HolidayMap;
  input?: (day: G2Day, field: G2PreviewField, label: string) => ReactNode;
  monthly?: boolean;
}) {
  const [expanded, setExpanded] = useState<Record<Group, boolean>>({ production: false, repair: false, delivery: false, defect: false });
  const toggle = (group: Group) => setExpanded(current => ({ ...current, [group]: !current[group] }));
  function summary(group: Group, label: string, quantity: (day: G2Day) => number | null): G2HorizontalRow {
    const action = expanded[group] ? '접기' : '보기';
    return {
      key: `summary-${group}`,
      label: <button type="button" className="g2-table-disclosure" aria-expanded={expanded[group]} aria-label={`${label} 상세 ${action}`} onClick={() => toggle(group)}>{label}<span aria-hidden="true">{expanded[group] ? '▾' : '▸'}</span></button>,
      rowClassName: 'g2-summary-row',
      value: day => <button type="button" className="g2-table-total-button" aria-expanded={expanded[group]} aria-label={`${formatG2Date(day.date)} ${label} ${quantity(day) === null ? '미입력' : `${quantity(day)}대`} 상세 ${action}`} onClick={() => toggle(group)}><strong>{quantity(day) ?? '—'}</strong></button>
    };
  }
  const detail = (key: G2PreviewField, label: string): G2HorizontalRow => ({ key, label, rowClassName: 'g2-detail-row', value: day => input ? input(day, key, label) : day[key]?.quantity ?? '—' });
  const rows: G2HorizontalRow[] = [
    summary('production', '생산', day => day.productionTotal),
    ...(expanded.production ? [detail('morningProduction', '오전 생산'), detail('afternoonProduction', '오후 생산')] : []),
    summary('repair', '수리', day => day.repairTotal),
    ...(expanded.repair ? [detail('morningRepair', '오전 수리'), detail('afternoonRepair', '오후 수리')] : []),
    summary('delivery', '납품', day => day.delivery?.quantity ?? null),
    ...(expanded.delivery ? [
      { key: 'delivery-target', label: '납품 목표', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.deliveryTarget?.quantity ?? '—' },
      detail('delivery', '일일 납품')
    ] : []),
    summary('defect', '불량', day => day.defect?.quantity ?? null),
    ...(expanded.defect ? [
      detail('defect', monthly ? '일일 불량 수량' : '신규 불량'),
      { key: 'defect-stock', label: '불량재고', rowClassName: 'g2-detail-row', value: (day: G2Day) => day.defectInventory, cellClassName: (day: G2Day) => day.defectInventory < 0 ? 'g2-negative' : undefined }
    ] : []),
    { key: 'inventory', label: '재고', rowClassName: 'g2-inventory-row', value: day => day.inventory ?? '기준 없음', cellClassName: day => day.inventory !== null && day.inventory < 0 ? 'g2-negative' : undefined }
  ];
  return <>
    <p className="g2-table-help">행의 이름이나 숫자를 눌러 상세를 확인하세요. 불량재고는 해당 일자까지의 신규 불량에서 수리 완료량을 뺀 수량입니다.</p>
    {days.some(day => day.defectInventory < 0) ? <p className="g2-warning" role="alert">불량재고보다 수리량이 많은 날짜가 있습니다. 신규 불량과 수리 입력을 확인해 주세요.</p> : null}
    {monthly
      ? <G2FilteredHorizontalTable title="월간 입력 현황" filterLabel="입력 현황 표시 기간" caption="생산·납품·재고 월간 입력 현황" days={days} rows={rows} holidays={holidays} />
      : <G2HorizontalTable days={days} caption="생산 현황" rows={rows} holidays={holidays} />}
  </>;
}
