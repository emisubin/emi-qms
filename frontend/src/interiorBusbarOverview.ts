import type { BusbarWorkspace } from "./interiorBusbar";

export function busbarOverview(data: BusbarWorkspace, date: string) {
  const end = new Date(`${date}T12:00:00Z`);
  end.setUTCDate(end.getUTCDate() + 7);
  const through = end.toISOString().slice(0, 10);
  const projects = data.projects.filter(p => p.requestedQuantity > p.shippedQuantity && p.dueDate.slice(0, 10) <= through)
    .sort((a,b) => a.dueDate.localeCompare(b.dueDate) || a.name.localeCompare(b.name) || a.id.localeCompare(b.id));
  const materialsNeeded = new Map<string, number>();
  const missingBoms: string[] = [];
  const families = data.productFamilies.map(f => {
    const plans = data.plans.filter(p => p.productFamilyId === f.id);
    const todayPlans = plans.filter(p => p.planDate.slice(0,10) === date);
    const overdue = plans.filter(p => p.planDate.slice(0,10) < date).reduce((n,p) => n + Math.max(0,p.quantity-(p.actualQuantity ?? 0)),0);
    const remainingProduction = overdue + todayPlans.reduce((n,p) => n + Math.max(0,p.quantity-(p.actualQuantity ?? 0)),0);
    if (remainingProduction > 0) {
      const bom = data.boms.filter(b => b.productFamilyId === f.id).sort((a,b) => b.version-a.version)[0];
      const lines = bom ? data.bomLines.filter(l => l.bomId === bom.id) : [];
      if (!lines.length) missingBoms.push(f.name);
      else for (const line of lines) materialsNeeded.set(line.materialId, (materialsNeeded.get(line.materialId) ?? 0) + remainingProduction * line.quantity);
    }
    const planned = todayPlans.reduce((n,p) => n+p.quantity,0);
    const produced = data.overview?.productionToday.find(p => p.productFamilyId === f.id)?.quantity ?? 0;
    const stock = f.balance ?? 0;
    const deliveries = projects.filter(p => p.productFamilyId === f.id).reduce((n,p) => n+p.requestedQuantity-p.shippedQuantity,0);
    return {id:f.id,name:f.name,planned,produced,overdue,stock,deliveries,needed:Math.max(0,deliveries-stock)};
  }).filter(f => f.planned || f.produced || f.overdue || f.stock || f.deliveries);
  const materials = data.materials.map(m => {
    const stock = m.balance ?? 0, required = materialsNeeded.get(m.id) ?? 0;
    return {id:m.id,name:m.name,unit:m.unit,stock,required,shortage:Math.max(0,required-stock)};
  }).filter(m => m.shortage > 0).sort((a,b) => a.name.localeCompare(b.name));
  return {through,projects,families,materials,missingBoms};
}
