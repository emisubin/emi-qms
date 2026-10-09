import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { G2MaterialsPage } from '../src/G2MaterialsPage';
import { calculateMaterial, getMaterialDay, importMaterialFile, saveMaterialDay, type MaterialDay, type MaterialImport } from '../src/g2Materials';
vi.mock('../src/g2Materials', async original => ({...await original<typeof import('../src/g2Materials')>(), getMaterialDay:vi.fn(), saveMaterialDay:vi.fn(), importMaterialFile:vi.fn()}));
const empty = {working:null,kitting:null,rack:null,materials:null,erp:null,transit:null};
const fixture = ():MaterialDay => ({date:'2026-10-09',canEdit:true,items:Array.from({length:92},(_,i)=>({id:i+1,sourceRow:i+4,category:i<40?'사급':i<80?'도급':'부자재',vendor:'공급사',name:`품목 ${i+1}`,code:`ITEM${i+1}`,spec:'규격',unit:'EA',setQuantity:2,packBox:'10',packPalletEa:null,packPalletBox:null,packNote:null,imageKeys:[],cellFills:{},values:{...empty,erp:i===0?7:null},updatedAtUtc:null}))});
beforeEach(()=>{
 vi.mocked(getMaterialDay).mockResolvedValue(fixture());
 vi.mocked(saveMaterialDay).mockResolvedValue({saved:true});
 HTMLDialogElement.prototype.showModal=function(){this.open=true;};
 HTMLDialogElement.prototype.close=function(){this.open=false;};
});
afterEach(()=>{cleanup();vi.clearAllMocks();});
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
