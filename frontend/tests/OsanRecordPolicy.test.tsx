import { describe,it,expect,vi,beforeEach } from 'vitest';
import { render,screen,fireEvent,waitFor } from '@testing-library/react';
import { OsanPhotoEditor } from '../src/OsanPhotoEditor';
import { fetchJson } from '../src/api';
import { validateOsanRecord, type OsanProgressTarget } from '../src/osanProgress';
vi.mock('../src/api',()=>({fetchJson:vi.fn(),ApiError:class extends Error{status=0}}));
const target:OsanProgressTarget={targetId:'target',sequenceNumber:1,displayName:'패널 01',status:'InProgress',version:4,startedAtUtc:null,startedByUserId:null,startedByDisplayName:null,steps:[{stepId:'step',sequenceNumber:1,stepCode:'IQC',stepName:'입고검사',status:'Completed',startedAtUtc:null,completedAtUtc:null,completedByUserId:'first',completedByDisplayName:'최초 작업자',canCompleteIndividual:false,canCompleteBatch:false,guidanceDescription:null,guidancePhotos:[],photos:[],comment:'기존 코멘트'}]};
beforeEach(()=>{vi.resetAllMocks();});
it('승인을 요청하지 않은 사용자도 승인된 단계 수정 버튼을 사용할 수 있다',async()=>{
 vi.mocked(fetchJson).mockResolvedValue({canApprove:false,currentUserId:'other',items:[{requestId:'r',targetId:'target',stepId:'step',requestedBy:'requester',requestedByName:'요청자',approvedAt:'2026-09-11',usedAt:null,invalidatedAt:null}]});
 render(<OsanPhotoEditor projectId="project" target={target} stage={1} mutationAllowed onSaved={()=>{}}/>);
 fireEvent.click(await screen.findByRole('button',{name:'사진 수정'}));
 expect(screen.getByRole('textbox')).toHaveValue('기존 코멘트');
 expect(screen.getByRole('button',{name:'사진 변경 저장'})).toBeDisabled();
});
it('승인 중에는 반려가 없고 관리자는 초기화를 사유와 함께 제출한다',async()=>{
 vi.mocked(fetchJson).mockResolvedValue({canApprove:true,currentUserId:'admin',items:[{requestId:'r',stepId:'step',requestedBy:'other',requestedByName:'요청자',approvedAt:'2026-09-11',usedAt:null}]});
 const saved=vi.fn();render(<OsanPhotoEditor projectId="project" target={target} stage={1} mutationAllowed onSaved={saved}/>);
 fireEvent.click(await screen.findByRole('button',{name:'초기화'}));
 expect(screen.queryByRole('button',{name:'반려'})).not.toBeInTheDocument();
 expect(screen.getByRole('button',{name:'초기화 처리'})).toBeDisabled();
 fireEvent.change(screen.getByRole('textbox'),{target:{value:'다른 패널 자료 등록'}});
 fireEvent.click(screen.getByRole('button',{name:'초기화 처리'}));
 await waitFor(()=>expect(saved).toHaveBeenCalledOnce());
 expect(vi.mocked(fetchJson).mock.calls[1][0]).toContain('/steps/step/reset');
 expect(JSON.parse(vi.mocked(fetchJson).mock.calls[1][2]!.body as string)).toMatchObject({reason:'다른 패널 자료 등록',expectedVersion:4});
});
describe('사진 및 코멘트 정책',()=>{
 it('사진 없이 관리자 비공백 코멘트만 허용',()=>{expect(validateOsanRecord([],'확인',true)).toBeNull();expect(validateOsanRecord([],'  ',true)).not.toBeNull();expect(validateOsanRecord([],'확인',false)).not.toBeNull();});
 it('코멘트 1001자 및 유지사진 포함 6장을 거부',()=>{expect(validateOsanRecord([],'가'.repeat(1001),true)).not.toBeNull();expect(validateOsanRecord(Array.from({length:5},()=>new File(['x'],'a.png',{type:'image/png'})),'',false,[{photoId:'p',sizeBytes:1} as never])).not.toBeNull();});
});
