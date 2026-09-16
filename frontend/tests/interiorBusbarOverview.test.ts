import { describe, expect, it } from "vitest";
import { busbarOverview } from "../src/interiorBusbarOverview";
import type { BusbarWorkspace } from "../src/interiorBusbar";
const family = (id: string, balance = 0) => ({id,code:id,name:id,isActive:true,balance,plannedQuantity:9999,producedQuantity:9999});
function data(): BusbarWorkspace {
  return {canWrite:false, settings:{commonProjectCode:"SYN"}, productFamilies:[family("A",5),family("B"),family("unused")],
    materials:[{...family("M",4),unit:"m"},{...family("negative",-2),unit:"ea"}], workers:[], purchases:[], products:[], ledger:[],ledgerLines:[],shipments:[],receipts:[],
    plans:[{id:"old",productFamilyId:"A",planDate:"2026-09-14",quantity:10,actualQuantity:7},
      {id:"today",productFamilyId:"A",planDate:"2026-09-15",quantity:8,actualQuantity:2},
      {id:"future",productFamilyId:"A",planDate:"2026-09-16",quantity:999,actualQuantity:0},
      {id:"missing",productFamilyId:"B",planDate:"2026-09-15",quantity:1,actualQuantity:0}],
    overview:{asOfDate:"2026-09-15",productionToday:[{productFamilyId:"A",quantity:4}]},
    boms:[{id:"v1",productFamilyId:"A",version:1,createdAtUtc:""},{id:"v2",productFamilyId:"A",version:2,createdAtUtc:""}],
    bomLines:[{bomId:"v1",materialId:"M",quantity:100},{bomId:"v2",materialId:"M",quantity:2}],
    projects:["2026-09-14","2026-09-22","2026-09-23"].map((dueDate,i)=>({id:String(i),name:String(i),customerJobNumber:"",commonProjectCode:"SYN",productFamilyId:"A",requestedQuantity:10,shippedQuantity:2,destination:"",dueDate}))};
}
describe("home operational metrics",()=>{
  it("limits demand to outstanding overdue and seven-day projects, ignoring cumulative totals and future plans",()=>{
    const d=data(); d.projects.push({...d.projects[0],id:"complete",requestedQuantity:2});
    const h=busbarOverview(d,"2026-09-15");
    expect(h.projects.map(p=>p.id)).toEqual(["0","1"]);
    expect(h.families).toEqual([
      {id:"A",name:"A",planned:8,produced:4,overdue:3,stock:5,deliveries:16,needed:11},
      {id:"B",name:"B",planned:1,produced:0,overdue:0,stock:0,deliveries:0,needed:0}]);
    expect(h.materials.find(m=>m.id==="M")).toMatchObject({required:18,stock:4,shortage:14});
    expect(h.materials.find(m=>m.id==="negative")).toMatchObject({required:0,stock:-2,shortage:2});
    expect(h.missingBoms).toEqual(["B"]);
  });
  it("combines shared-material demand and reflects completed work and replenishment",()=>{
    const d=data(); d.boms.push({id:"B",productFamilyId:"B",version:1,createdAtUtc:""});d.bomLines.push({bomId:"B",materialId:"M",quantity:3});
    expect(busbarOverview(d,"2026-09-15").materials.find(m=>m.id==="M")?.required).toBe(21);
    d.plans.forEach(p=>p.actualQuantity=p.quantity); d.materials.forEach(m=>m.balance=10);
    expect(busbarOverview(d,"2026-09-15").materials).toEqual([]);
    expect(busbarOverview(d,"2026-09-15").missingBoms).toEqual([]);
  });
});
