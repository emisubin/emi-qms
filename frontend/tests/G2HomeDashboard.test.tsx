import { act, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { G2HomePage } from '../src/G2HomePage';
import { achievement, achievementColor, deliveryForecast, materialShortages, recentWorkday } from '../src/g2Dashboard';
import type { G2Day } from '../src/g2';
import type { MaterialItem } from '../src/g2Materials';
import * as api from '../src/api';
import * as materialsApi from '../src/g2Materials';
const metric=(quantity:number)=>({quantity,version:1,updatedAtUtc:'',updatedByDisplayName:''});
const day=(date:string,extra:Partial<G2Day>={}):G2Day=>({date,isForecast:false,morningProduction:null,afternoonProduction:null,morningRepair:null,afternoonRepair:null,delivery:null,defect:null,morningEmiAttendance:null,morningContractorAttendance:null,afternoonEmiAttendance:null,afternoonContractorAttendance:null,productionTotal:null,repairTotal:null,defectInventory:null,morningAttendanceTotal:null,afternoonAttendanceTotal:null,attendanceTotal:null,inventory:null,physicalCount:null,defectInventoryCount:null,dailyProductionTarget:null,deliveryTarget:null,inventoryTarget:null,...extra});
const actual=day('2026-10-10',{productionTotal:160,productionPlan:metric(180),delivery:metric(150),deliveryPlan:metric(150),defect:metric(4),inventory:420,morningAttendanceTotal:12,afternoonAttendanceTotal:11});
const item=(id:number,extra:Partial<MaterialItem>={}):MaterialItem=>({id,sourceRow:id,category:'사급',vendor:'',name:`자재 ${id}`,code:'',spec:'',unit:'개',setQuantity:2,packBox:null,packPalletEa:null,packPalletBox:null,packNote:null,imageKeys:[],cellFills:{},updatedAtUtc:null,values:{working:240,kitting:0,rack:0,materials:0,erp:0,transit:0},...extra});
const props={developmentUserKey:'dev-sales',canManageInventory:true,canManageTargets:true,mutationEnabled:true};
function mocks(days:G2Day[]=[actual]) {
 vi.spyOn(api,'getG2Home').mockResolvedValue({today:'2026-10-11',year:2026,month:10,hasInventoryBaseline:true,days});
 vi.spyOn(api,'getG2Days').mockResolvedValue({today:'2026-10-11',from:'2026-09-10',to:'2026-11-10',days});
 vi.spyOn(api,'listSystemHolidays').mockResolvedValue([]);
 vi.spyOn(materialsApi,'getLatestMaterialDay').mockResolvedValue({date:'2026-10-10',canEdit:true,items:[item(1)]});
}
afterEach(()=>vi.restoreAllMocks());
describe('approved G2 home',()=>{
 it('uses the latest material count independently of the production workday',async()=>{
  mocks();vi.mocked(materialsApi.getLatestMaterialDay).mockResolvedValue({date:'2026-10-07',canEdit:false,items:[item(7)]});
  render(<G2HomePage {...props}/>);const table=await screen.findByRole('table',{name:'안전재고 미달 자재'});
  expect(within(table).getByText('자재 7')).toBeInTheDocument();expect(screen.getByText(/실사일 10.07/)).toBeInTheDocument();expect(screen.getByText('2026.10.10')).toBeInTheDocument();
 });
 it('shows no saved count instead of inventing safe stock',async()=>{
  mocks();vi.mocked(materialsApi.getLatestMaterialDay).mockResolvedValue(null);render(<G2HomePage {...props}/>);
  await screen.findByText('저장된 자재 실사 데이터가 없습니다.');expect(screen.queryByText('안전재고 미달 자재가 없습니다.')).toBeNull();
 });

 it('keeps a missing Saturday as the recent workday instead of silently using older data',()=>{
  expect(recentWorkday('2026-10-12',[day('2026-10-09',{productionTotal:80})],new Set())).toBe('2026-10-10');
  expect(recentWorkday('2026-10-10',[],new Set(['2026-10-09']))).toBe('2026-10-08');
 });
 it('distinguishes missing and zero actuals and applies the exact 90 percent boundary',()=>{
  expect(achievement(null,100)).toBeNull(); expect(achievement(0,100)).toBe(0); expect(achievement(10,0)).toBeNull();
  expect(achievementColor(89.99)).toBe('#d9232d'); expect(achievementColor(90)).toBe('#278C82');
 });
 it('computes shortages in material units and excludes unknown inventories',()=>{
  const missing=item(2); missing.values={working:null,kitting:null,rack:null,materials:null,erp:null,transit:null};
  const withHiddenBlank=item(1); withHiddenBlank.values.erp=null;
  const result=materialShortages([withHiddenBlank,missing,item(3,{values:{working:360,kitting:0,rack:0,materials:0,erp:0,transit:0}})]);
  expect(result.rows.map(r=>[r.item.id,r.capacity,r.shortage])).toEqual([[1,120,120]]);expect(result.incomplete).toBe(1);
 });
 it('starts at today, adds previous working day production and stops at month end',()=>{
  const today=day('2026-10-29',{inventory:88,productionPlan:metric(50),deliveryPlan:metric(999)});
  const result=deliveryForecast(today,[today,day('2026-10-30',{deliveryPlan:metric(48),productionPlan:metric(0)}),day('2026-10-31',{deliveryPlan:metric(30)}),day('2026-11-01',{deliveryPlan:metric(900)})],'2026-10-31',40);
  expect(result.map(p=>[p.date,p.quantity])).toEqual([['2026-10-29',88],['2026-10-30',80],['2026-10-31',50]]);
  expect(deliveryForecast(day('2026-10-29'),[],'2026-10-31',40)).toEqual([]);
 });
 it('includes all dates and distinguishes zero/missing averages with Sunday and holiday schedules',()=>{
  const start=day('2026-10-10',{inventory:100});
  const result=deliveryForecast(start,[],'2026-10-14',10,new Set(['2026-10-12']));
  expect(result.map(p=>p.quantity)).toEqual([100,110,110,110,120]);
  expect(deliveryForecast(start,[day('2026-10-11',{productionPlan:metric(5)})],'2026-10-12',10).map(p=>p.quantity)).toEqual([100,110,110]);
  expect(deliveryForecast(start,[],'2026-10-11',0).map(p=>p.quantity)).toEqual([100,100]);
  expect(deliveryForecast(start,[],'2026-10-11',null).map(p=>p.quantity)).toEqual([100,100]);
 });
 it('uses today stock independently of the latest workday and labels the average forecast',async()=>{
  mocks([actual,day('2026-10-11',{inventory:88}),day('2026-10-12',{deliveryPlan:metric(30)})]);
  vi.mocked(api.getG2Home).mockResolvedValue({today:'2026-10-11',year:2026,month:10,hasInventoryBaseline:true,days:[],averageDailyProduction:40,productionSampleDays:20});
  render(<G2HomePage {...props}/>);
  const graph=await screen.findByRole('img',{name:/납품 전망:/});
  expect(graph).toHaveAttribute('aria-label',expect.stringContaining('2026-10-11 88대, 2026-10-12 58대'));
  expect(graph).toHaveAttribute('aria-label',expect.stringContaining('2026-10-17'));
  expect(graph.getAttribute('aria-label')).not.toContain('2026-10-18');
  expect(screen.getByText('일 평균 40대 생산 반영')).toBeInTheDocument();
  expect(api.getG2Days).toHaveBeenCalledWith('dev-sales','2026-09-10','2026-10-17');
 });
 it('keeps seven calendar dates when the forecast crosses a month boundary',async()=>{
  mocks([day('2026-10-29',{inventory:180})]);
  vi.mocked(api.getG2Home).mockResolvedValue({today:'2026-10-29',year:2026,month:10,hasInventoryBaseline:true,days:[],averageDailyProduction:0,productionSampleDays:1});
  render(<G2HomePage {...props}/>);
  const graph=await screen.findByRole('img',{name:/납품 전망:/});
  expect(graph.querySelectorAll('circle')).toHaveLength(7);
  expect(graph).toHaveAttribute('aria-label',expect.stringContaining('2026-11-04'));
  expect(graph.getAttribute('aria-label')).not.toContain('2026-11-05');
  expect(api.getG2Days).toHaveBeenCalledWith('dev-sales','2026-09-28','2026-11-04');
 });
 it('renders the approved dashboard with live fields and no old home input features',async()=>{
  mocks();const {container}=render(<G2HomePage {...props}/>);
  await screen.findByRole('heading',{name:'EMI 생산'});
  expect(screen.getByRole('img',{name:'실적 160대 / 계획 180대, 달성률 88.9%'})).toBeInTheDocument();
  expect(container.querySelector('.gauge-actual')).toHaveStyle({fill:'#d9232d'});
  expect(container.querySelector('.actual-unit')).toHaveAttribute('dx','5');
  expect(screen.getByText('(계획 180대)')).toBeInTheDocument();
  expect(screen.getByText('2.5')).toBeInTheDocument();
  expect(within(screen.getByRole('table',{name:'안전재고 미달 자재'})).getByText('자재 1')).toBeInTheDocument();
  expect(screen.queryByRole('button',{name:'실사 입력'})).not.toBeInTheDocument();
  expect(screen.queryByText('목표 관리')).not.toBeInTheDocument();expect(container.querySelectorAll('input')).toHaveLength(0);
 });
 it('renders missing values without claiming zero or safe material stock on failure',async()=>{
  mocks([day('2026-10-10')]);vi.mocked(materialsApi.getLatestMaterialDay).mockRejectedValue(new Error('offline'));
  render(<G2HomePage {...props}/>);await screen.findByRole('heading',{name:'EMI 생산'});
  expect(screen.getByRole('alert')).toHaveTextContent('자재 실사 데이터를 불러오지 못했습니다.');
  expect(screen.getAllByText('미입력').length).toBeGreaterThan(0);expect(screen.queryByText('안전재고 미달 자재가 없습니다.')).not.toBeInTheDocument();
 });
 it('does not present an empty catalog as safe inventory',async()=>{
  mocks();vi.mocked(materialsApi.getLatestMaterialDay).mockResolvedValue({date:'2026-10-10',canEdit:false,items:[]});
  render(<G2HomePage {...props}/>);await screen.findByText('등록된 자재 품목이 없습니다. 관리자에게 문의해 주세요.');
  expect(screen.queryByText('안전재고 미달 자재가 없습니다.')).not.toBeInTheDocument();
 });
 it('discards responses from a previous user and handles forbidden access',async()=>{
  mocks();let resolveOld!:(value:Awaited<ReturnType<typeof api.getG2Home>>)=>void;
  vi.mocked(api.getG2Home).mockImplementationOnce(()=>new Promise(resolve=>{resolveOld=resolve;})).mockRejectedValueOnce(new api.ApiError(403,'denied'));
  const view=render(<G2HomePage {...props}/>);view.rerender(<G2HomePage {...props} developmentUserKey="dev-viewer"/>);
  await screen.findByRole('alert');
  await act(async()=>{resolveOld({today:'2026-10-11',year:2026,month:10,hasInventoryBaseline:true,days:[actual]});});
  await waitFor(()=>expect(screen.getByRole('alert')).toHaveTextContent('권한이 없습니다'));
  expect(screen.queryByRole('heading',{name:'EMI 생산'})).not.toBeInTheDocument();
 });
});
