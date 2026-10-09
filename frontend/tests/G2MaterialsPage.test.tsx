import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { canLeaveMaterialDraft } from '../src/materialNavigationGuard';
import { G2MaterialsPage } from '../src/G2MaterialsPage';
import { calculateMaterial, getMaterialDay, importMaterialFile, saveMaterialDay, type MaterialDay, type MaterialImport } from '../src/g2Materials';
vi.mock('../src/g2Materials', async original => ({...await original<typeof import('../src/g2Materials')>(), getMaterialDay:vi.fn(), saveMaterialDay:vi.fn(), importMaterialFile:vi.fn()}));
const empty = {working:null,kitting:null,rack:null,materials:null,erp:null,transit:null};
const fixture = ():MaterialDay => ({date:'2026-10-09',canEdit:true,items:Array.from({length:92},(_,i)=>({id:i+1,sourceRow:i+4,category:i<40?'사급':i<80?'도급':'부자재',vendor:'공급사',name:`품목 ${i+1}`,code:`ITEM${i+1}`,spec:'규격',unit:'EA',setQuantity:2,packBox:'10',packPalletEa:null,packPalletBox:null,packNote:null,imageKeys:[],cellFills:{},values:{...empty,erp:i===0?7:null},updatedAtUtc:null}))});
beforeEach(()=>{
 vi.mocked(getMaterialDay).mockResolvedValue(fixture());
 vi.mocked(saveMaterialDay).mockReset().mockResolvedValue({saved:true});
 HTMLDialogElement.prototype.showModal=function(){this.open=true;};
 HTMLDialogElement.prototype.close=function(){this.open=false;};
});
afterEach(()=>{cleanup();vi.clearAllMocks();vi.unstubAllGlobals();});
async function open(){render(<G2MaterialsPage developmentUserKey="materials" mutationEnabled/>);await screen.findByLabelText('1번 제작중');}
describe('material inventory workflows',()=>{
 it('saves all 92 rows including hidden ERP and distinguishes blank from zero',async()=>{
  await open();
  fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'3'}});
  fireEvent.change(screen.getByLabelText('2번 제작중'),{target:{value:'0'}});
  fireEvent.change(screen.getByLabelText('자재 검색'),{target:{value:'품목 1'}});
  fireEvent.click(screen.getByRole('button',{name:'저장'}));
  await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(1));
  const payload=vi.mocked(saveMaterialDay).mock.calls[0][2];
  expect(payload).toHaveLength(92);
  expect(payload[0]).toMatchObject({id:1,working:3,erp:7});
  expect(payload[1].working).toBe(0);expect(payload[2].working).toBeNull();
 });
 it('imports to draft only, clears existing values on blank import, then saves latest edits',async()=>{
  const preview:MaterialImport={sheets:[{name:'26-10-09오전',date:'2026-10-09'},{name:'26-10-09오후',date:'2026-10-09'}],selectedSheet:'26-10-09오전',selectedDate:'2026-10-09',items:fixture().items.map(r=>({id:r.id,...empty}))};
  vi.mocked(importMaterialFile).mockResolvedValue(preview);
  await open();fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'3'}});
  fireEvent.click(screen.getByRole('button',{name:'엑셀 가져오기'}));
  fireEvent.change(screen.getByLabelText('엑셀 파일'),{target:{files:[new File(['synthetic'],'inventory.xlsx')]}});
  fireEvent.click(screen.getByRole('button',{name:'파일 확인'}));
  await screen.findByLabelText('대상 시트');
  fireEvent.change(screen.getByLabelText('대상 시트'),{target:{value:'26-10-09오후'}});
  expect(screen.getByLabelText('대상 시트')).toBeInTheDocument();
  expect(screen.getByRole('button',{name:'표에 가져오기'})).toBeDisabled();
  vi.mocked(importMaterialFile).mockResolvedValue({...preview,selectedSheet:'26-10-09오후'});
  fireEvent.click(screen.getByRole('button',{name:'파일 확인'}));
  await waitFor(()=>expect(screen.getByRole('button',{name:'표에 가져오기'})).toBeEnabled());
  fireEvent.click(screen.getByRole('button',{name:'표에 가져오기'}));
  expect(saveMaterialDay).not.toHaveBeenCalled();
  expect(screen.getByLabelText('1번 제작중')).toHaveValue(null);
  fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'5'}});
  fireEvent.click(screen.getByRole('button',{name:'저장'}));
  await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(1));
  expect(vi.mocked(saveMaterialDay).mock.calls[0][2][0]).toMatchObject({working:5,erp:null});
 });
 it('keeps the edited draft on save failure and supports retry',async()=>{
  vi.mocked(saveMaterialDay).mockRejectedValueOnce(new Error('저장 실패'));
  await open();fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'3'}});
  fireEvent.click(screen.getByRole('button',{name:'저장'}));
  await screen.findByRole('alert');expect(screen.getByLabelText('1번 제작중')).toHaveValue(3);
  expect(screen.getByRole('button',{name:'저장'})).toBeEnabled();
 });
 it('does not expose entry actions to read-only users',async()=>{
  vi.mocked(getMaterialDay).mockResolvedValue({...fixture(),canEdit:false});
  render(<G2MaterialsPage developmentUserKey="sales" mutationEnabled/>);
  await screen.findByRole('button',{name:'품목 1'});
  expect(screen.queryByRole('button',{name:'저장'})).toBeNull();
  expect(screen.queryByRole('button',{name:'엑셀 가져오기'})).toBeNull();
  expect(screen.queryByLabelText('1번 제작중')).toBeNull();
 });
 it('rejects oversized entry without crashing the page',async()=>{await open();fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'1e21'}});expect(screen.getByRole('heading',{name:'자재 재고 현황'})).toBeInTheDocument();expect(screen.getByLabelText('1번 제작중')).toHaveValue(null);});
 it('clearing an existing quantity persists null',async()=>{
  const data=fixture();data.items[0].values.working=3;vi.mocked(getMaterialDay).mockResolvedValue(data);
  await open();fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:''}});
  fireEvent.click(screen.getByRole('button',{name:'저장'}));
  await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(1));
  expect(vi.mocked(saveMaterialDay).mock.calls[0][2][0].working).toBeNull();
 });
 it('calculates combined possible units from quantities before truncation',()=>{
  expect(calculateMaterial({...empty,working:.3,kitting:.6,rack:.1},1)).toMatchObject({Q:1,R:1,Z:1,AA:1});
  expect(calculateMaterial({...empty,working:3,materials:3},2)).toMatchObject({Q:3,R:1,W:1,Y:6,Z:3,AA:3});
 });
});

describe('approved mobile material workflow',()=>{
 beforeEach(()=>vi.stubGlobal('matchMedia',vi.fn(()=>({matches:true,addEventListener:vi.fn(),removeEventListener:vi.fn()}))));
 async function editFirst(){render(<G2MaterialsPage developmentUserKey="materials" mutationEnabled/>);fireEvent.click((await screen.findByText('품목 1',{exact:true})).closest('button')!);fireEvent.click(screen.getByRole('button',{name:'실사 입력'}));}
 it('autosaves full-day snapshots immediately, preserves hidden ERP and serializes rapid input',async()=>{
  let resolve!:(v:{saved:boolean})=>void;vi.mocked(saveMaterialDay).mockImplementationOnce(()=>new Promise(r=>resolve=r));await editFirst();
  expect(screen.queryByRole('button',{name:'데이터 입력'})).toBeNull();expect(screen.queryByRole('button',{name:'엑셀 가져오기'})).toBeNull();expect(screen.getByText('제조 Part')).toBeInTheDocument();expect(screen.getByText('자재 Part')).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('제작중'),{target:{value:'1'}});fireEvent.change(screen.getByLabelText('제작중'),{target:{value:'12'}});fireEvent.change(screen.getByLabelText('키팅대차'),{target:{value:'0'}});
  expect(saveMaterialDay).toHaveBeenCalledTimes(1);expect(screen.getByLabelText('제작중')).toBeEnabled();resolve({saved:true});await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(2));
  const saved=vi.mocked(saveMaterialDay).mock.calls[1][2];expect(saved).toHaveLength(92);expect(saved[0]).toMatchObject({working:12,kitting:0,erp:7});expect(saved[1].working).toBeNull();
  await waitFor(()=>expect(screen.getByLabelText('실사일')).toBeEnabled());
  fireEvent.change(screen.getByLabelText('제작중'),{target:{value:''}});await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(3));expect(vi.mocked(saveMaterialDay).mock.calls[2][2][0].working).toBeNull();
 });
 it('keeps values on failed autosave and retries latest input without a normal save button',async()=>{
  vi.mocked(saveMaterialDay).mockRejectedValueOnce(new Error('offline'));await editFirst();fireEvent.change(screen.getByLabelText('제작중'),{target:{value:'3'}});
  await waitFor(()=>expect(screen.getAllByRole('button',{name:'다시 시도'}).at(-1)).toBeEnabled());expect(screen.getByLabelText('제작중')).toHaveValue(3);expect(screen.getByLabelText('실사일')).toBeDisabled();fireEvent.click(screen.getAllByRole('button',{name:'다시 시도'}).at(-1)!);await waitFor(()=>expect(saveMaterialDay).toHaveBeenCalledTimes(2));await waitFor(()=>expect(screen.getByLabelText('실사일')).toBeEnabled());
 });
 it('read-only user sees detail but no input action and blank totals are not marked low',async()=>{
  const d=fixture();d.canEdit=false;d.items[0].values.erp=null;vi.mocked(getMaterialDay).mockResolvedValue(d);const {container}=render(<G2MaterialsPage developmentUserKey="sales" mutationEnabled/>);fireEvent.click((await screen.findByText('품목 1',{exact:true})).closest('button')!);expect(screen.queryByRole('button',{name:'실사 입력'})).toBeNull();expect(container.querySelector('.possible.low')).toBeNull();expect(saveMaterialDay).not.toHaveBeenCalled();
 });
 it('uses a strict 180-unit threshold',async()=>{
  const d=fixture();d.items[0].values={...empty,working:358};d.items[1].values={...empty,working:360};vi.mocked(getMaterialDay).mockResolvedValue(d);const {container}=render(<G2MaterialsPage developmentUserKey="materials" mutationEnabled/>);(await screen.findByText('품목 1',{exact:true})).closest('button')!;expect(container.querySelectorAll('.possible')[0]).toHaveClass('low');expect(container.querySelectorAll('.possible')[1]).not.toHaveClass('low');
 });
});

describe('mobile transition save protection',()=>{
 it('keeps navigation blocked until the latest queued snapshot is saved',async()=>{
  vi.stubGlobal('matchMedia',()=>({matches:true,addEventListener:vi.fn(),removeEventListener:vi.fn()}));const alert=vi.spyOn(window,'alert').mockImplementation(()=>{});const completions:Array<(v:{saved:boolean})=>void>=[];
  vi.mocked(saveMaterialDay).mockImplementation(()=>new Promise(r=>completions.push(r)));render(<G2MaterialsPage developmentUserKey="materials" mutationEnabled/>);fireEvent.click((await screen.findByText('품목 1',{exact:true})).closest('button')!);fireEvent.click(screen.getByRole('button',{name:'실사 입력'}));fireEvent.change(screen.getByLabelText('제작중'),{target:{value:'1'}});fireEvent.change(screen.getByLabelText('제작중'),{target:{value:'12'}});expect(canLeaveMaterialDraft()).toBe(false);await act(async()=>completions[0]({saved:true}));expect(canLeaveMaterialDraft()).toBe(false);await act(async()=>completions[1]({saved:true}));expect(canLeaveMaterialDraft()).toBe(true);alert.mockRestore();
 });
 it('does not allow a mobile write while the existing PC manual save is in flight',async()=>{
  let onResize=()=>{};const media={matches:false,addEventListener:(_type:string,cb:()=>void)=>{onResize=cb},removeEventListener:vi.fn()};vi.stubGlobal('matchMedia',()=>media);let finish!:(v:{saved:boolean})=>void;vi.mocked(saveMaterialDay).mockImplementationOnce(()=>new Promise(r=>finish=r));await open();fireEvent.change(screen.getByLabelText('1번 제작중'),{target:{value:'3'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));act(()=>{media.matches=true;onResize()});fireEvent.click((await screen.findByText('품목 1',{exact:true})).closest('button')!);expect(screen.queryByRole('button',{name:'실사 입력'})).toBeNull();expect(saveMaterialDay).toHaveBeenCalledTimes(1);await act(async()=>finish({saved:true}));expect(screen.getByRole('button',{name:'실사 입력'})).toBeEnabled();
 });
});
