import {fireEvent,render,screen,waitFor,within} from '@testing-library/react';
import {beforeEach,afterEach,expect,it,vi} from 'vitest';
import {G2NonconformancePage} from '../src/G2NonconformancePage';
import {getUpg,getUpgPhoto,saveUpg,saveUpgAction,deleteUpg,filterUpg,emptyUpgFilter,type UpgItem} from '../src/g2Nonconformances';
vi.mock('../src/g2Nonconformances',async original=>({...await original<typeof import('../src/g2Nonconformances')>(),getUpg:vi.fn(),getUpgPhoto:vi.fn(),saveUpg:vi.fn(),saveUpgAction:vi.fn(),deleteUpg:vi.fn()}));
const item:UpgItem={id:'one',serial:'G2-018',occurrenceDate:'2026-10-10',occurrenceType:'supplier',content:'커넥터 확인',initialStock:false,photos:[{id:'photo',fileName:'test.png',contentType:'image/png'}],action:null,version:4};
const done:UpgItem={...item,id:'two',serial:'G2-019',action:{completionDate:'2026-10-11',content:'교체 확인',photos:item.photos}};
beforeEach(()=>{Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,value:function(){this.open=true;}});Object.defineProperty(HTMLDialogElement.prototype,'close',{configurable:true,value:function(){this.open=false;}});vi.stubGlobal('URL',Object.assign(URL,{createObjectURL:vi.fn(()=>'blob:test'),revokeObjectURL:vi.fn()}));vi.mocked(getUpg).mockResolvedValue({canEdit:true,items:[item,done]});vi.mocked(getUpgPhoto).mockResolvedValue(new Blob());});
afterEach(()=>{vi.clearAllMocks();vi.unstubAllGlobals();});
const page=()=>render(<G2NonconformancePage developmentUserKey="quality" mutationEnabled/>);
it('applies search only on submit and supports responsibility and completion dates',async()=>{page();await screen.findByRole('button',{name:item.serial});fireEvent.change(screen.getByLabelText('검색어'),{target:{value:'없는 내용'}});expect(screen.getByRole('button',{name:item.serial})).toBeInTheDocument();fireEvent.click(screen.getByRole('button',{name:'검색'}));expect(screen.queryByRole('button',{name:item.serial})).toBeNull();expect(filterUpg([item,done],{...emptyUpgFilter,basis:'completionDate',from:'2026-10-11',to:'2026-10-11',q:'교체',type:'supplier'},true)).toEqual([done]);});
it('does not expose mutation controls to read-only users',async()=>{vi.mocked(getUpg).mockResolvedValue({canEdit:false,items:[item]});page();fireEvent.click(await screen.findByRole('button',{name:item.serial}));expect(screen.queryByRole('button',{name:'부적합 등록'})).toBeNull();expect(screen.queryByRole('button',{name:'수정'})).toBeNull();expect(screen.queryByRole('button',{name:'조치 등록'})).toBeNull();});
it('retains photos and version while editing first information',async()=>{vi.mocked(saveUpg).mockResolvedValue({...item,content:'수정 확인',version:5});page();fireEvent.click(await screen.findByRole('button',{name:item.serial}));fireEvent.click(screen.getByRole('button',{name:'수정'}));fireEvent.change(screen.getByLabelText('부적합 내용'),{target:{value:'수정 확인'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));await waitFor(()=>expect(saveUpg).toHaveBeenCalledWith('quality','one',expect.objectContaining({photos:item.photos,expectedVersion:4,content:'수정 확인'})));await screen.findByText('저장했습니다.');});
it('blocks first deletion until action is removed',async()=>{page();await screen.findByRole('button',{name:item.serial});fireEvent.click(screen.getByRole('tab',{name:/수리 완료/}));fireEvent.click(screen.getByRole('button',{name:done.serial}));fireEvent.click(screen.getAllByRole('button',{name:'삭제'})[0]);expect(screen.getByRole('dialog',{name:'조치 정보를 먼저 삭제해 주세요'})).toBeInTheDocument();expect(deleteUpg).not.toHaveBeenCalled();});
it('deleting action returns to open list with refreshed server state',async()=>{vi.mocked(deleteUpg).mockResolvedValue({...done,action:null});page();await screen.findByRole('button',{name:item.serial});fireEvent.click(screen.getByRole('tab',{name:/수리 완료/}));fireEvent.click(screen.getByRole('button',{name:done.serial}));fireEvent.click(screen.getAllByRole('button',{name:'삭제'})[1]);vi.mocked(getUpg).mockResolvedValue({canEdit:true,items:[item,{...done,action:null}]});fireEvent.click(within(screen.getByRole('dialog',{name:'조치 정보 삭제'})).getByRole('button',{name:'삭제'}));await waitFor(()=>expect(deleteUpg).toHaveBeenCalledWith('quality',done,true));await screen.findByRole('button',{name:done.serial});expect(screen.getByRole('tab',{name:/미완료/})).toHaveAttribute('aria-selected','true');});
it('retains input and displays server conflict instead of closing editor',async()=>{vi.mocked(saveUpgAction).mockRejectedValue(new Error('다른 사용자가 수정했습니다. 다시 조회해 주세요.'));page();await screen.findByRole('button',{name:item.serial});fireEvent.click(screen.getByRole('tab',{name:/수리 완료/}));fireEvent.click(screen.getByRole('button',{name:done.serial}));fireEvent.click(screen.getAllByRole('button',{name:'수정'})[1]);fireEvent.change(screen.getByLabelText('조치 내용'),{target:{value:'재확인'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));expect(await screen.findByRole('alert')).toHaveTextContent('다른 사용자가 수정');expect(screen.getByLabelText('조치 내용')).toHaveValue('재확인');});
it('requires a photo and routes a saved action to the completed tab',async()=>{vi.mocked(saveUpgAction).mockResolvedValue({...item,action:{completionDate:'2026-10-12',content:'교체 완료',photos:item.photos},version:5});page();fireEvent.click(await screen.findByRole('button',{name:item.serial}));fireEvent.click(screen.getByRole('button',{name:'조치 등록'}));fireEvent.change(screen.getByLabelText('조치 내용'),{target:{value:'교체 완료'}});fireEvent.click(screen.getByRole('button',{name:'저장'}));expect(await screen.findByRole('alert')).toHaveTextContent('사진을 한 장 이상');expect(saveUpgAction).not.toHaveBeenCalled();fireEvent.change(screen.getByLabelText('조치 사진 · 필수'),{target:{files:[new File(['synthetic'], 'action.png',{type:'image/png'})]}});await screen.findByText('action.png');await waitFor(()=>expect(screen.getByRole('button',{name:'저장'})).not.toBeDisabled());fireEvent.click(screen.getByRole('button',{name:'저장'}));await screen.findByText('저장했습니다.');expect(screen.getByRole('tab',{name:/수리 완료/})).toHaveAttribute('aria-selected','true');expect(saveUpgAction).toHaveBeenCalledWith('quality','one',expect.objectContaining({content:'교체 완료',expectedVersion:4,photos:[expect.objectContaining({fileName:'action.png',base64:expect.any(String)})]}));});
it('filters classification independently from responsibility and preserves legacy simple items',async()=>{
 const complex:UpgItem={...item,id:'complex',serial:'G2-020',nonconformanceType:'complexLong',initialStock:true};
 const repaired:UpgItem={...done,nonconformanceType:'complexLong'};
 vi.mocked(getUpg).mockResolvedValue({canEdit:true,items:[item,complex,repaired]});page();await screen.findByRole('button',{name:item.serial});
 fireEvent.change(screen.getByLabelText('부적합 구분'),{target:{value:'complexLong'}});
 expect(screen.getByRole('button',{name:item.serial})).toBeInTheDocument();
 fireEvent.click(screen.getByRole('button',{name:'검색'}));expect(screen.queryByRole('button',{name:item.serial})).toBeNull();
 expect(screen.getByRole('button',{name:complex.serial})).toBeInTheDocument();
 expect(screen.getByRole('tab',{name:/미완료/})).toHaveTextContent('1');expect(screen.getByRole('tab',{name:/수리 완료/})).toHaveTextContent('1');
 fireEvent.click(screen.getByRole('tab',{name:/수리 완료/}));expect(screen.getByRole('button',{name:repaired.serial})).toBeInTheDocument();
 expect(filterUpg([item,complex],{...emptyUpgFilter,nonconformanceType:'simple'},false)).toEqual([item]);
 expect(filterUpg([complex],{...emptyUpgFilter,nonconformanceType:'complexLong',type:'production'},false)).toEqual([]);
});
it('editing classification preserves initial stock and existing photos',async()=>{
 const initial={...item,initialStock:true,nonconformanceType:'simple' as const};
 vi.mocked(getUpg).mockResolvedValue({canEdit:true,items:[initial]});vi.mocked(saveUpg).mockResolvedValue({...initial,nonconformanceType:'complexLong',version:5});
 page();fireEvent.click(await screen.findByRole('button',{name:item.serial}));fireEvent.click(screen.getByRole('button',{name:'수정'}));
 const dialog=screen.getByRole('dialog',{name:'부적합 정보 수정'});fireEvent.change(within(dialog).getByLabelText('부적합 구분'),{target:{value:'complexLong'}});fireEvent.click(within(dialog).getByRole('button',{name:'저장'}));
 await waitFor(()=>expect(saveUpg).toHaveBeenCalledWith('quality','one',expect.objectContaining({nonconformanceType:'complexLong',initialStock:true,photos:item.photos,expectedVersion:4})));
});
it('new and initial registration both submit classification with distinct initial stock flags',async()=>{
 vi.mocked(saveUpg).mockImplementation(async(_u,_id,input)=>({...item,...input,id:'new',photos:item.photos,version:1}));page();await screen.findByRole('button',{name:item.serial});
 for(const initial of [true,false]){
  fireEvent.click(screen.getByRole('button',{name:initial?'초기 재고 등록':'부적합 등록'}));
  const dialog=screen.getByRole('dialog',{name:initial?'초기 재고 등록':'부적합 등록'});
  fireEvent.change(within(dialog).getByLabelText('시리얼 넘버'),{target:{value:'NEW-001'}});fireEvent.change(within(dialog).getByLabelText('부적합 내용'),{target:{value:'실사 확인'}});
  fireEvent.change(within(dialog).getByLabelText('부적합 구분'),{target:{value:'complexLong'}});
  if(initial)expect(within(dialog).getByText(/신규 부적합 실적에서는 제외/)).toBeInTheDocument();
  fireEvent.change(within(dialog).getByLabelText('부적합 사진 · 필수'),{target:{files:[new File(['synthetic'],'initial.png',{type:'image/png'})]}});
  await within(dialog).findByText('initial.png');await waitFor(()=>expect(within(dialog).getByRole('button',{name:'저장'})).not.toBeDisabled());
  fireEvent.click(within(dialog).getByRole('button',{name:'저장'}));
  await waitFor(()=>expect(saveUpg).toHaveBeenLastCalledWith('quality',null,expect.objectContaining({nonconformanceType:'complexLong',initialStock:initial})));
  await waitFor(()=>expect(screen.queryByRole('dialog')).toBeNull());
 }
});

it('keeps current typed inventory independent of search results and includes initial stock',async()=>{
 const complex:UpgItem={...item,id:'complex',serial:'G2-C',nonconformanceType:'complexLong',initialStock:true};
 vi.mocked(getUpg).mockResolvedValue({canEdit:true,items:[item,complex,done]});page();await screen.findByRole('button',{name:item.serial});
 const stock=screen.getByLabelText('현재 전체 부적합 재고');
 expect(stock).toHaveTextContent('합계 2대');expect(stock).toHaveTextContent('단순 부적합 1대');expect(stock).toHaveTextContent('복합·장기 부적합 1대');
 fireEvent.change(screen.getByLabelText('검색어'),{target:{value:'없는 내용'}});fireEvent.click(screen.getByRole('button',{name:'검색'}));
 expect(screen.getByRole('tab',{name:/미완료/})).toHaveTextContent('0');expect(stock).toHaveTextContent('합계 2대');
});
