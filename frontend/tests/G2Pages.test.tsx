import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ApiError, getG2Days, listSystemHolidays, saveG2Attendance } from '../src/api';
import { G2AttendancePage } from '../src/G2ManagementPages';
import { todaySeoul, type G2Day } from '../src/g2';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), getG2Days: vi.fn(), listSystemHolidays: vi.fn(), saveG2Attendance: vi.fn() }));
const today = todaySeoul();
const metric = (quantity: number | null) => ({ quantity, version: 7, updatedAtUtc: '2026-10-01T00:00:00Z', updatedByDisplayName: '검수' });
const day = (date = today): G2Day => ({date, isForecast: date > today, morningProduction: null, afternoonProduction: null, morningRepair: null, afternoonRepair: null, delivery: null, defect: null, productionTotal: null, repairTotal: null, defectInventory: null, inventory: null, physicalCount: null, defectInventoryCount: null, dailyProductionTarget: null, deliveryTarget: null, inventoryTarget: null, morningEmiAttendance: metric(7), morningContractorAttendance: metric(5), afternoonEmiAttendance: metric(6), afternoonContractorAttendance: metric(5), morningAttendanceTotal: 12, afternoonAttendanceTotal: 11, attendanceTotal: 23});
function mockDays(rows = [day()]) { vi.mocked(getG2Days).mockImplementation(async (_user, from, to) => ({today, from, to, days: rows.filter(d => d.date >= from && d.date <= to)})); }
function page(canEdit = true, mutationEnabled = true) { return render(<div className="app-shell"><G2AttendancePage developmentUserKey="synthetic" canEdit={canEdit} mutationEnabled={mutationEnabled}/></div>); }
async function open() { fireEvent.click(await screen.findByRole('button', {name: `${today} 출근 인원 수정`})); await waitFor(() => expect(screen.getByRole('spinbutton', {name: 'EMI'})).toHaveValue(7)); }
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {configurable: true, value: function () { this.open = true; }});
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {configurable: true, value: function () { this.open = false; }});
  mockDays(); vi.mocked(listSystemHolidays).mockResolvedValue([]); vi.mocked(saveG2Attendance).mockResolvedValue({saved: true});
});
afterEach(() => { vi.clearAllMocks(); vi.unstubAllGlobals(); });
it('uses Seoul date across UTC month boundaries', () => expect(todaySeoul(new Date('2026-08-31T15:05:00Z'))).toBe('2026-09-01'));
it('renders the approved vertical table and daily sum', async () => {
  page(); const table = await screen.findByRole('table', {name: '월간 출근 현황'});
  const row = within(table).getByRole('button', {name: `${today} 출근 인원 수정`}).closest('tr')!;
  expect(within(row).getAllByRole('cell').slice(-7).map(cell => cell.textContent)).toEqual(['7','5','12','6','5','11','23']);
  expect(screen.queryByRole('spinbutton')).toBeNull();
});
it('preserves tab drafts and sends only changed fields with versions, zero and null', async () => {
  page(); await open(); fireEvent.change(screen.getByRole('spinbutton', {name:'EMI'}), {target:{value:'0'}});
  fireEvent.click(screen.getByRole('tab', {name:'오후'})); expect(screen.getByRole('spinbutton', {name:'EMI'})).toHaveValue(6);
  fireEvent.change(screen.getByRole('spinbutton', {name:'도급'}), {target:{value:''}});
  fireEvent.click(screen.getByRole('tab', {name:'오전'})); expect(screen.getByRole('spinbutton', {name:'EMI'})).toHaveValue(0);
  fireEvent.click(screen.getByRole('button', {name:'저장'}));
  await waitFor(() => expect(saveG2Attendance).toHaveBeenCalledWith('synthetic',today,{morningEmiAttendance:{quantity:0,expectedVersion:7},afternoonContractorAttendance:{quantity:null,expectedVersion:7}}));
  expect(await screen.findByText('출근 인원을 저장했습니다.')).toBeInTheDocument();
});
it('preserves input on conflict and prevents duplicate submissions while saving', async () => {
  let reject!: (error: Error) => void; vi.mocked(saveG2Attendance).mockImplementation(() => new Promise((_, fail) => {reject=fail;}));
  page(); await open(); fireEvent.change(screen.getByRole('spinbutton',{name:'EMI'}),{target:{value:'8'}}); fireEvent.click(screen.getByRole('button',{name:'저장'}));
  expect(screen.getByRole('button',{name:'저장 중…'})).toBeDisabled(); expect(screen.getByRole('button',{name:'취소'})).toBeDisabled();
  await act(async()=>reject(new Error('다른 사용자가 수정했습니다. 다시 조회해 주세요.')));
  expect(screen.getByRole('alert')).toHaveTextContent('다른 사용자가 수정'); expect(screen.getByRole('spinbutton',{name:'EMI'})).toHaveValue(8); expect(saveG2Attendance).toHaveBeenCalledTimes(1);
});
it('does not save unchanged inputs', async () => {page(); await open(); fireEvent.click(screen.getByRole('button',{name:'저장'})); expect(await screen.findByRole('alert')).toHaveTextContent('변경한 값'); expect(saveG2Attendance).not.toHaveBeenCalled();});
it('hides entry for viewers and disables mutation in read-only runtime', async () => {
  const view=page(false); await screen.findByRole('table',{name:'월간 출근 현황'}); expect(screen.queryByRole('button',{name:'데이터 입력'})).toBeNull(); expect(screen.queryByRole('button',{name:/출근 인원 수정/})).toBeNull();
  view.rerender(<G2AttendancePage developmentUserKey="synthetic" canEdit mutationEnabled={false}/>); await screen.findByRole('table',{name:'월간 출근 현황'}); expect(screen.getByRole('button',{name:'데이터 입력'})).toBeDisabled(); fireEvent.click(screen.getByText('23',{selector:'td'})); expect(screen.queryByRole('dialog')).toBeNull();
});
it('opens mobile detail with EMI/contractor breakdown and total then the shared form', async () => {
  vi.stubGlobal('matchMedia',()=>({matches:true,addEventListener:vi.fn(),removeEventListener:vi.fn()})); page(); fireEvent.click(await screen.findByRole('button',{name:`${today} 출근 상세`}));
  const detail=screen.getByRole('dialog'); expect(detail).toHaveAttribute('id','day-popup'); expect(within(detail).getByText('23명')).toBeInTheDocument(); expect(within(detail).getByRole('table')).toHaveTextContent('EMI'); fireEvent.click(within(detail).getByRole('button',{name:'입력·수정'})); expect(screen.getByRole('dialog')).toHaveAttribute('id','layer'); await waitFor(()=>expect(screen.getByRole('spinbutton',{name:'EMI'})).toHaveValue(7));
});
it('blanks future data and blocks future writes', async () => {
  const future='2099-10-08'; mockDays([day(future)]); page(); await screen.findByRole('table',{name:'월간 출근 현황'}); fireEvent.click(screen.getByRole('button',{name:'조회 연월 선택'})); fireEvent.change(screen.getByLabelText('조회 연도'),{target:{value:'2099'}}); fireEvent.change(screen.getByLabelText('조회 월'),{target:{value:'10'}}); fireEvent.click(screen.getByRole('button',{name:'조회'}));
  const table=await screen.findByRole('table',{name:'월간 출근 현황'}); await waitFor(()=>expect(within(table).getByText('10.08 (목)')).toBeInTheDocument()); const row=within(table).getByText('10.08 (목)').closest('tr')!; expect(within(row).getAllByRole('cell').slice(-7).every(cell=>cell.textContent==='')).toBe(true); fireEvent.click(row); expect(screen.queryByRole('dialog')).toBeNull();
});
it('ignores stale date response after switching input date', async () => {
  const previous=new Date(`${today}T00:00:00Z`); previous.setUTCDate(previous.getUTCDate()-1); const other=previous.toISOString().slice(0,10); let finish!:(data:Awaited<ReturnType<typeof getG2Days>>)=>void;
  page(); await screen.findByRole('table',{name:'월간 출근 현황'});
  vi.mocked(getG2Days).mockImplementation(async(_user,from,to)=>from===today&&to===today?new Promise(resolve=>{finish=resolve;}):{today,from,to,days:[{...day(other),morningEmiAttendance:metric(33)}]});
  fireEvent.click(screen.getByRole('button',{name:`${today} 출근 인원 수정`})); fireEvent.change(screen.getByLabelText('입력 날짜'),{target:{value:other}}); await waitFor(()=>expect(screen.getByRole('spinbutton',{name:'EMI'})).toHaveValue(33));
  await act(async()=>finish({today,from:today,to:today,days:[day()]})); expect(screen.getByRole('spinbutton',{name:'EMI'})).toHaveValue(33);
});
it('shows read errors and supports retry without entry',async()=>{
  vi.mocked(getG2Days).mockRejectedValueOnce(new ApiError(403,'forbidden'));page(); expect(await screen.findByRole('alert')).toHaveTextContent('조회할 권한'); expect(screen.getByRole('button',{name:'데이터 입력'})).toBeDisabled(); fireEvent.click(screen.getByRole('button',{name:'다시 시도'})); expect(await screen.findByRole('table',{name:'월간 출근 현황'})).toBeInTheDocument();
});
