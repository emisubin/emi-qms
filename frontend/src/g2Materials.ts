import { fetchBlob, fetchJson } from './api';
export const quantityKeys = ['working','kitting','rack','materials','erp','transit'] as const;
export type QuantityKey = typeof quantityKeys[number];
export type CalculationColumn = 'Q'|'R'|'S'|'W'|'X'|'Y'|'Z'|'AA';
export type MaterialValues = Record<QuantityKey, number|null> & {calculationOverrides?:Partial<Record<CalculationColumn,number>>|null};
export type MaterialItem = { id:number; sourceRow:number; category:string; vendor:string; name:string; code:string; spec:string; unit:string; setQuantity:number; packBox:string|null; packPalletEa:string|null; packPalletBox:string|null; packNote:string|null; imageKeys:string[]; cellFills:Record<string,string>; values:MaterialValues; updatedAtUtc:string|null };
export type MaterialDay = {date:string; canEdit:boolean; items:MaterialItem[]};
export type MaterialInput = MaterialValues & {id:number};
export type MaterialImport = {sheets:{name:string;date:string|null;error?:string|null}[];selectedSheet:string|null;selectedDate:string|null;items:MaterialInput[];warnings?:string[]|null};
// Quantities are stored at four decimal places. Integer arithmetic prevents a
// decimal sum just below an exact unit from losing a whole producible unit.
function scaledQuantity(value:number|null):bigint {
  return BigInt((value??0).toFixed(4).replace('.', ''));
}
export function editMaterialQuantity(values:MaterialValues,key:QuantityKey,value:number|null):MaterialValues {
  return {...values,[key]:value,calculationOverrides:null};
}
export function calculateMaterial(v:MaterialValues,perSet:number){
  const manufacture=scaledQuantity(v.working)+scaledQuantity(v.kitting)+scaledQuantity(v.rack);
  const material=scaledQuantity(v.materials)+scaledQuantity(v.erp)+scaledQuantity(v.transit);
  const total=manufacture+material;
  const setText=perSet.toLocaleString('en-US',{useGrouping:false,maximumFractionDigits:20});
  const places=setText.split('.')[1]?.length??0;
  const setNumerator=perSet>0?BigInt(setText.replace('.', '')):0n;
  const denominator=10000n*setNumerator;
  const unitNumerator=(quantity:bigint)=>quantity*10n**BigInt(places);
  const rawUnits=(quantity:bigint)=>setNumerator>0?Number(unitNumerator(quantity))/Number(denominator):null;
  const units=(quantity:bigint)=>setNumerator>0?Number(unitNumerator(quantity)/denominator):null;
  const S=rawUnits(manufacture),X=rawUnits(material),AA=units(total);
  return {Q:Number(manufacture)/10000,R:units(manufacture),S,W:units(material),X,Y:Number(total)/10000,Z:AA,AA};
}
export const getMaterialDay=(user:string|undefined,date:string)=>fetchJson<MaterialDay>(`/api/g2/materials?date=${encodeURIComponent(date)}`,user);
export const saveMaterialDay=(user:string|undefined,date:string,items:MaterialInput[])=>fetchJson<{saved:boolean}>(`/api/g2/materials/${date}`,user,{method:'PUT',body:JSON.stringify({items})});
export function importMaterialFile(user:string|undefined,date:string,file:File,sheet?:string){const form=new FormData();form.append('file',file);form.append('date',date);if(sheet)form.append('sheet',sheet);return fetchJson<MaterialImport>('/api/g2/materials/import',user,{method:'POST',body:form});}
export const getMaterialImage=(user:string|undefined,id:number,index:number,signal:AbortSignal)=>fetchBlob(`/api/g2/materials/${id}/images/${index}`,user,signal);
