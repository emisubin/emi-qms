import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { G2OperationsPage } from '../src/G2OperationsPage';
import { G2OperationEditor } from '../src/G2OperationEditor';
import { getG2Days, listSystemHolidays, saveG2Plans, saveG2Workspace } from '../src/api';
import { todaySeoul, type G2Day, type G2WriteAccess } from '../src/g2';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), getG2Days:vi.fn(),listSystemHolidays:vi.fn(),saveG2Plans:vi.fn(),saveG2Workspace:vi.fn() }));
const today=todaySeoul();
const metric=(quantity:number|null)=>({quantity,version:2,updatedAtUtc:'2026-10-08T00:00:00Z',updatedByDisplayName:'검수'});
const note=(text:string|null)=>({text,version:3,updatedAtUtc:'2026-10-08T00:00:00Z',updatedByDisplayName:'검수'});
const day=(date=today):G2Day=>({date,isForecast:date>today,morningProduction:metric(100),afternoonProduction:metric(90),morningRepair:metric(2),afternoonRepair:metric(3),productionTotal:190,repairTotal:5,delivery:metric(180),defect:metric(4),inventory:2000,defectInventory:9,physicalCount:metric(2000),defectInventoryCount:metric(9),productionPlan:metric(200),deliveryPlan:metric(180),planNote:note('계획 확인'),actualNote:note('오전 기록'),rowNote:note('행 기록'),morningEmiAttendance:null,morningContractorAttendance:null,afternoonEmiAttendance:null,afternoonContractorAttendance:null,morningAttendanceTotal:null,afternoonAttendanceTotal:null,attendanceTotal:null,dailyProductionTarget:null,deliveryTarget:null,inventoryTarget:null});
const all:G2WriteAccess={production:true,delivery:true,productionPlan:true,deliveryPlan:true,planNote:true,note:true,inventory:true};
const none:G2WriteAccess={production:false,delivery:false,productionPlan:false,deliveryPlan:false,planNote:false,note:false,inventory:false};
function mockDays(rows=[day()]) { vi.mocked(getG2Days).mockImplementation(async(_user,from,to)=>({today,from,to,days:rows.filter(d=>d.date>=from&&d.date<=to)})); }
function page(access=all) { return render(<div className="app-shell"><G2OperationsPage developmentUserKey="synthetic" canEditProduction={access.production} canEditDelivery={access.delivery} access={access} mutationEnabled/></div>); }
function editor(kind:'actual'|'plan'|'count'|'note',access=all){return render(<div className="app-shell"><G2OperationEditor kind={kind} initialDate={today} today={today} user="synthetic" access={access} enabled onClose={vi.fn()} onSaved={async()=>{}}/></div>);}
beforeEach(()=>{Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,value:function(){this.open=true;}});Object.defineProperty(HTMLDialogElement.prototype,'close',{configurable:true,value:function(){this.open=false;}});mockDays();vi.mocked(listSystemHolidays).mockResolvedValue([]);vi.mocked(saveG2Workspace).mockResolvedValue({saved:true});vi.mocked(saveG2Plans).mockResolvedValue({saved:true});});
afterEach(()=>{vi.clearAllMocks();vi.unstubAllGlobals();});
it('expands independent rows and isolates row note clicks',async()=>{
 const other=today.slice(0,8)+(today.endsWith('01')?'02':'01');mockDays([day(other),day()]);page();
 fireEvent.click(await screen.findByRole('button',{name:`${today} 상세 펼치기`}));fireEvent.click(screen.getByRole('button',{name:`${other} 상세 펼치기`}));expect(screen.getAllByRole('button',{name:/상세 접기/})).toHaveLength(2);
 fireEvent.click(screen.getByRole('button',{name:`${today} 행 비고 수정`}));await waitFor(()=>expect(screen.getByRole('textbox',{name:'행 비고'})).toHaveValue('행 기록'));expect(screen.getAllByRole('button',{name:/상세 접기/})).toHaveLength(2);
});
it('saves zero and clear with original versions and shares notes across tabs',async()=>{
 editor('actual');await waitFor(()=>expect(screen.getByLabelText('오전 생산 실적')).toHaveValue(100));fireEvent.change(screen.getByLabelText('오전 생산 실적'),{target:{value:'0'}});fireEvent.change(screen.getByLabelText('공유 실적 비고'),{target:{value:'오전 기록\n오후 이어쓰기'}});
 fireEvent.click(screen.getByRole('tab',{name:'오후'}));expect(screen.getByLabelText('공유 실적 비고')).toHaveValue('오전 기록\n오후 이어쓰기');fireEvent.change(screen.getByLabelText('오후 수리 실적'),{target:{value:''}});fireEvent.click(screen.getByRole('tab',{name:'납품'}));expect(screen.getByLabelText('납품 실적')).toHaveValue(180);fireEvent.click(screen.getByRole('button',{name:'저장'}));
 await waitFor(()=>expect(saveG2Workspace).toHaveBeenCalledWith('synthetic',today,{morningProduction:{quantity:0,expectedVersion:2},afternoonRepair:{quantity:null,expectedVersion:2},actualNote:{text:'오전 기록\n오후 이어쓰기',expectedVersion:3}}));
});
it('shows only authorized fields without inventing per-tab permissions',async()=>{editor('actual',{...none,delivery:true,note:true});await waitFor(()=>expect(screen.getByLabelText('납품 실적')).toHaveValue(180));fireEvent.click(screen.getByRole('tab',{name:'오전'}));expect(screen.queryByRole('spinbutton')).toBeNull();expect(screen.getByLabelText('공유 실적 비고')).toHaveValue('오전 기록');expect(screen.getAllByRole('tab')).toHaveLength(3);});
it('hides input actions for read-only departments',async()=>{page(none);await screen.findByRole('table',{name:'월간 생산·출하 현황'});expect(screen.queryByRole('button',{name:'데이터 입력'})).toBeNull();expect(screen.queryByRole('button',{name:/행 비고 수정/})).toBeNull();});
it('clears counts with versions without a deletion button',async()=>{editor('count');await waitFor(()=>expect(screen.getByLabelText('납품 가능 재고 실사')).toHaveValue(2000));fireEvent.change(screen.getByLabelText('납품 가능 재고 실사'),{target:{value:''}});fireEvent.change(screen.getByLabelText('불량 재고 실사'),{target:{value:'0'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));await waitFor(()=>expect(saveG2Workspace).toHaveBeenCalledWith('synthetic',today,{physicalCount:{quantity:null,expectedVersion:2},defectInventoryCount:{quantity:0,expectedVersion:2}}));expect(screen.queryByRole('button',{name:/삭제|지우기/})).toBeNull();});
it('preserves drafts when another user has saved first',async()=>{vi.mocked(saveG2Workspace).mockRejectedValueOnce(new Error('다른 사용자가 수정했습니다. 다시 조회해 주세요.'));editor('actual');await waitFor(()=>expect(screen.getByLabelText('오전 생산 실적')).toHaveValue(100));fireEvent.change(screen.getByLabelText('오전 생산 실적'),{target:{value:'111'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));expect(await screen.findByRole('alert')).toHaveTextContent('다른 사용자가 수정');expect(screen.getByLabelText('오전 생산 실적')).toHaveValue(111);});
it('sends range plans with Sunday exclusion and preserves blank fields',async()=>{
 const next=new Date(`${today}T00:00:00Z`);next.setUTCDate(next.getUTCDate()+2);const end=next.toISOString().slice(0,10);mockDays([day(),day(end)]);editor('plan');await waitFor(()=>expect(screen.getByLabelText('생산 계획')).toHaveValue(200));fireEvent.change(screen.getByLabelText('계획 종료일'),{target:{value:end}});await screen.findByText(/적용 .*일 · 새 입력/);expect(screen.getByLabelText('일요일 제외')).toBeChecked();expect(screen.getByLabelText('기존 계획도 변경')).not.toBeChecked();fireEvent.change(screen.getByLabelText('납품 계획'),{target:{value:''}});fireEvent.change(screen.getByLabelText('계획 비고'),{target:{value:''}});fireEvent.click(screen.getByRole('button',{name:'저장'}));await waitFor(()=>expect(saveG2Plans).toHaveBeenCalledWith('synthetic',{from:today,to:end,excludeSundays:true,overwrite:false,productionPlan:200}));
});
it('keeps future plans and notes but blanks actuals and inventories',async()=>{
 const date='2099-10-08';mockDays([day(date)]);page();await screen.findByRole('table',{name:'월간 생산·출하 현황'});fireEvent.click(screen.getByRole('button',{name:'조회 연월 선택'}));fireEvent.change(screen.getByLabelText('조회 연도'),{target:{value:'2099'}});fireEvent.change(screen.getByLabelText('조회 월'),{target:{value:'10'}});fireEvent.click(screen.getByRole('button',{name:'조회'}));const toggle=await screen.findByRole('button',{name:`${date} 상세 펼치기`});const cells=within(toggle.closest('tr')!).getAllByRole('cell');expect(cells[2]).toHaveTextContent('180');expect(cells[3]).toBeEmptyDOMElement();expect(cells[6]).toBeEmptyDOMElement();expect(cells[8]).toBeEmptyDOMElement();expect(cells[9]).toHaveTextContent('행 기록');fireEvent.click(toggle);expect(screen.getByText('계획 확인')).toBeInTheDocument();expect(screen.getByText('오전 기록')).toBeInTheDocument();
});
it('uses a floating mobile detail then shared bottom menu and input form',async()=>{
 vi.stubGlobal('matchMedia',()=>({matches:true,addEventListener:vi.fn(),removeEventListener:vi.fn()}));page();fireEvent.click(await screen.findByRole('button',{name:`${today} 상세 보기`}));const detail=screen.getByRole('dialog');expect(detail).toHaveAttribute('id','day-popup');expect(within(detail).getByRole('table',{name:'생산·수리·불량 실적'})).toBeInTheDocument();expect(within(detail).getByRole('table',{name:'재고·실사'})).toBeInTheDocument();fireEvent.click(within(detail).getByRole('button',{name:'데이터 입력'}));expect(screen.getByRole('dialog')).toHaveAttribute('id','input-sheet');fireEvent.click(screen.getByRole('button',{name:'실적 입력'}));expect(screen.getByRole('dialog')).toHaveAttribute('id','layer');await waitFor(()=>expect(screen.getByLabelText('오전 생산 실적')).toHaveValue(100));
});

it('allows future actual notes without sending actual quantities',async()=>{
 const date='2099-10-08';mockDays([day(date)]);render(<div className="app-shell"><G2OperationEditor kind="actual" initialDate={date} today={today} user="synthetic" access={all} enabled onClose={vi.fn()} onSaved={async()=>{}}/></div>);await waitFor(()=>expect(screen.getByLabelText('공유 실적 비고')).toHaveValue('오전 기록'));expect(screen.queryByRole('spinbutton')).toBeNull();fireEvent.change(screen.getByLabelText('공유 실적 비고'),{target:{value:'미리 확인'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));await waitFor(()=>expect(saveG2Workspace).toHaveBeenCalledWith('synthetic',date,{actualNote:{text:'미리 확인',expectedVersion:3}}));
});

it('ignores a previous user response after the workspace identity changes',async()=>{
 let resolve!:(response:Awaited<ReturnType<typeof getG2Days>>)=>void;
 vi.mocked(getG2Days).mockImplementation(user=>user==='old'?new Promise(r=>{resolve=r;}):Promise.resolve({today,from:today,to:today,days:[{...day(),rowNote:note('현재 사용자 자료')}]}));
 const view=render(<G2OperationsPage developmentUserKey="old" canEditProduction={false} canEditDelivery={false} mutationEnabled/>);
 view.rerender(<G2OperationsPage developmentUserKey="new" canEditProduction={false} canEditDelivery={false} mutationEnabled/>);
 await screen.findByText('현재 사용자 자료');
 await act(async()=>resolve({today,from:today,to:today,days:[{...day(),rowNote:note('이전 사용자 자료')}]}));
 expect(screen.queryByText('이전 사용자 자료')).toBeNull();
});
