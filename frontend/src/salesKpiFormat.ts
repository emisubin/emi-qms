export function formatMoney(value: number | null, currency: string) {
  if (value === null) return '목표 미등록';
  return new Intl.NumberFormat('ko-KR', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);
}

export function formatCompactMoney(value: number, currency: string) {
  return `${new Intl.NumberFormat('ko-KR', { notation: 'compact', maximumFractionDigits: 1 }).format(value)} ${currency}`;
}
