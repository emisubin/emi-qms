export type G2MetricValue = {
  quantity: number | null;
  version: number;
  updatedAtUtc: string;
  updatedByDisplayName: string;
};

export type G2InventoryCount = {
  quantity: number;
  version: number;
  updatedAtUtc: string;
  updatedByDisplayName: string;
};

export type G2Target = {
  targetType: 'DailyProduction' | 'Inventory';
  effectiveDate: string;
  quantity: number;
  version: number;
  updatedAtUtc: string;
  updatedByDisplayName: string;
};

export type G2Day = {
  date: string;
  isForecast: boolean;
  morningProduction: G2MetricValue | null;
  afternoonProduction: G2MetricValue | null;
  delivery: G2MetricValue | null;
  morningEmiAttendance: G2MetricValue | null;
  morningContractorAttendance: G2MetricValue | null;
  afternoonEmiAttendance: G2MetricValue | null;
  afternoonContractorAttendance: G2MetricValue | null;
  productionTotal: number | null;
  morningAttendanceTotal: number | null;
  afternoonAttendanceTotal: number | null;
  attendanceTotal: number | null;
  inventory: number | null;
  physicalCount: G2InventoryCount | null;
  dailyProductionTarget: G2Target | null;
  inventoryTarget: G2Target | null;
};

export type G2RangeResponse = { today: string; from: string; to: string; days: G2Day[] };
export type G2HomeResponse = { today: string; year: number; month: number; hasInventoryBaseline: boolean; days: G2Day[] };
export type G2MetricChange = { quantity: number | null; expectedVersion: number | null };
export type SaveG2OperationsRequest = { morningProduction?: G2MetricChange; afternoonProduction?: G2MetricChange; delivery?: G2MetricChange };
export type SaveG2AttendanceRequest = {
  morningEmiAttendance?: G2MetricChange;
  morningContractorAttendance?: G2MetricChange;
  afternoonEmiAttendance?: G2MetricChange;
  afternoonContractorAttendance?: G2MetricChange;
};

export function monthBounds(date: string) {
  const [year, month] = date.split('-').map(Number);
  const from = `${year}-${String(month).padStart(2, '0')}-01`;
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return { from, to: `${year}-${String(month).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}` };
}

export function todaySeoul(now = new Date()) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Seoul',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(now);
  const part = (type: 'year' | 'month' | 'day') => parts.find(item => item.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export function formatG2Date(date: string) {
  const [, month, day] = date.split('-');
  return `${Number(month)}월 ${Number(day)}일`;
}

export function formatG2Modified(value: G2MetricValue | G2InventoryCount | G2Target | null) {
  if (!value) return '아직 입력하지 않음';
  return `${value.updatedByDisplayName} · ${new Intl.DateTimeFormat('ko-KR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value.updatedAtUtc))}`;
}
