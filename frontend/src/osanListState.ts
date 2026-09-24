export type OsanListFilters = {
  search: string;
  customers: string[];
  statuses: string[];
  dueFrom: string;
  dueTo: string;
  kpi: string | null;
  page: number;
};

export const emptyOsanListFilters = (): OsanListFilters => ({
  search: '', customers: [], statuses: [], dueFrom: '', dueTo: '', kpi: null, page: 1
});

type Snapshot = { filters: OsanListFilters; draft: string; scrollY: number };
const snapshots = new Map<string, Snapshot>();

export function clearOsanListSnapshots() { snapshots.clear(); }

export function readOsanListSnapshot(key: string): Snapshot | undefined {
  const snapshot = snapshots.get(key);
  return snapshot && { ...snapshot, filters: { ...snapshot.filters, customers: [...snapshot.filters.customers], statuses: [...snapshot.filters.statuses] } };
}

export function saveOsanListSnapshot(key: string, snapshot: Snapshot) {
  snapshots.set(key, snapshot);
}

export function toggleOsanListValue(values: string[], value: string) {
  return values.includes(value) ? values.filter(item => item !== value) : [...values, value];
}

export function osanDueDateMatches(deliveryDate: string, dueFrom: string, dueTo: string) {
  return (!dueFrom || deliveryDate >= dueFrom) && (!dueTo || deliveryDate <= dueTo);
}
