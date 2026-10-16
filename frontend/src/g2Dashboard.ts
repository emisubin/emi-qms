import type { G2Day } from './g2';
import { calculateMaterial, quantityKeys, type MaterialItem } from './g2Materials';
export const SAFETY_STOCK = 180;
export const shiftDate = (date: string, days: number) => { const d = new Date(`${date}T00:00:00Z`); d.setUTCDate(d.getUTCDate()+days); return d.toISOString().slice(0,10); };
export const displayQuantity = (value: number | null | undefined) => value == null ? '미입력' : value.toLocaleString('ko-KR', {maximumFractionDigits:4});
export const achievement = (actual: number | null | undefined, plan: number | null | undefined) => actual == null || plan == null || plan <= 0 ? null : actual / plan * 100;
export const achievementColor = (rate: number | null) => rate == null ? '#737373' : rate < 90 ? '#d9232d' : '#278C82';
// Calendar days remain visible even when the previous workday has no input.
// An explicit production plan/actual permits holiday and Sunday operations.
export function recentWorkday(today: string, days: G2Day[], holidays: ReadonlySet<string>) {
  const byDate = new Map(days.map(day => [day.date, day]));
  for (let n=1; n<=31; n++) {
    const date = shiftDate(today,-n), day = byDate.get(date);
    const explicit = (day?.productionPlan?.quantity ?? 0)>0 || (day?.productionTotal ?? 0)>0;
    if (explicit || (new Date(`${date}T00:00:00Z`).getUTCDay()!==0 && !holidays.has(date))) return date;
  }
  return null;
}
export function materialShortages(items: MaterialItem[]) {
  let incomplete=0;
  const rows=items.flatMap(item=>{
    if (quantityKeys.every(key=>item.values[key]==null) || item.setQuantity<=0) {incomplete++;return [];}
    const calculated=calculateMaterial(item.values,item.setQuantity);
    if(calculated.AA==null || calculated.AA>=SAFETY_STOCK)return [];
    return [{item,capacity:calculated.AA,shortage:Math.max(0,Math.round((SAFETY_STOCK*item.setQuantity-calculated.Y)*10000)/10000)}];
  }).sort((a,b)=>a.capacity-b.capacity||a.item.id-b.item.id);
  return {rows,incomplete};
}
// Forecast production applies on the planned date; the actual inventory ledger is unchanged.
export function deliveryForecast(day: G2Day | undefined, days: G2Day[], through: string, average: number | null) {
  if (!day || day.inventory == null) return [];
  const byDate = new Map(days.map(d => [d.date, d]));
  const produces = (date: string) => {
    if (new Date(`${date}T00:00:00Z`).getUTCDay() === 0) return false;
    const plan = byDate.get(date)?.productionPlan?.quantity;
    return plan != null && plan > 0;
  };
  let remaining = day.inventory;
  const points = [{date: day.date, quantity: remaining, delivery: 0, production: 0}];
  for (let date = shiftDate(day.date, 1); date <= through; date = shiftDate(date, 1)) {
    const production = produces(date) ? average ?? 0 : 0;
    const delivery = byDate.get(date)?.deliveryPlan?.quantity ?? 0;
    remaining += production - delivery;
    points.push({date, quantity: Math.round(remaining * 100) / 100, delivery, production});
  }
  return points;
}
