import type { G2Day, G2MetricValue } from './g2';

export type G2PreviewField = 'morningProduction' | 'afternoonProduction' | 'delivery' | 'defect';
export type G2PreviewInputs = Record<string, Partial<Record<G2PreviewField, string>>>;

const availableInventoryStartDate = '2026-08-28';

function previewQuantity(value: string | undefined, fallback: number | null | undefined) {
  if (value === undefined) return fallback ?? null;
  if (value.trim() === '') return null;
  const quantity = Number(value);
  return Number.isInteger(quantity) && quantity >= 0 ? quantity : null;
}

function previewMetric(metric: G2MetricValue | null, rawValue: string | undefined): G2MetricValue | null {
  if (rawValue === undefined) return metric;
  const quantity = previewQuantity(rawValue, metric?.quantity);
  if (!metric && quantity === null) return null;
  return metric
    ? { ...metric, quantity }
    : { quantity, version: 0, updatedAtUtc: '', updatedByDisplayName: '임시 예상' };
}

export function applyG2HomePreview(days: G2Day[], inputs: G2PreviewInputs): G2Day[] {
  if (days.length === 0) return [];
  if (Object.keys(inputs).length === 0) return days;
  const first = days[0];
  let balance: number | null = first.physicalCount === null && first.inventory !== null
    ? first.date < availableInventoryStartDate
      ? first.inventory - (first.productionTotal ?? 0) + (first.delivery?.quantity ?? 0) + (first.defect?.quantity ?? 0)
      : first.inventory
    : null;
  let previousMovement: { production: number; delivery: number; defect: number } | null = null;

  return days.map((day, index) => {
    const edit = inputs[day.date] ?? {};
    const morningProduction = previewMetric(day.morningProduction, edit.morningProduction);
    const afternoonProduction = previewMetric(day.afternoonProduction, edit.afternoonProduction);
    const delivery = previewMetric(day.delivery, edit.delivery);
    const defect = previewMetric(day.defect, edit.defect);
    const morning = morningProduction?.quantity ?? null;
    const afternoon = afternoonProduction?.quantity ?? null;
    const productionTotal = morning !== null || afternoon !== null ? (morning ?? 0) + (afternoon ?? 0) : null;
    const currentMovement = {
      production: productionTotal ?? 0,
      delivery: delivery?.quantity ?? 0,
      defect: defect?.quantity ?? 0
    };

    if (day.physicalCount !== null) balance = day.physicalCount.quantity;
    else if (balance !== null && day.date < availableInventoryStartDate) {
      balance += currentMovement.production - currentMovement.delivery - currentMovement.defect;
    } else if (balance !== null && index > 0 && previousMovement !== null) {
      balance += previousMovement.production - previousMovement.delivery - previousMovement.defect;
    }

    previousMovement = currentMovement;
    return { ...day, morningProduction, afternoonProduction, delivery, defect, productionTotal, inventory: balance };
  });
}
