import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, fetchJson } from '../src/api';
import { OsanPhotoEditor } from '../src/OsanPhotoEditor';
import { OsanStageHistory } from '../src/OsanStageHistory';
import { getOsanProgressPhoto, type OsanProgressTarget } from '../src/osanProgress';

vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
vi.mock('../src/osanProgress', async original => ({ ...await original<typeof import('../src/osanProgress')>(), getOsanProgressPhoto: vi.fn() }));
const target: OsanProgressTarget = {
  targetId: 'target-a', sequenceNumber: 1, displayName: '합성 대상', status: 'InProgress', version: 2,
  startedAtUtc: null, startedByUserId: null, startedByDisplayName: null,
  steps: [{ stepId: 'step-a', sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', status: 'Completed', canEdit: true,
    startedAtUtc: null, completedByUserId: 'worker', canCompleteIndividual: false, canCompleteBatch: false,
    guidanceDescription: null, guidancePhotos: [], completedAtUtc: '2026-09-10T00:00:00Z', completedByDisplayName: '합성 작업자', photos: [] }]
};
function show(canEdit=true,canManageStages=false,mutationAllowed=true){
 const onSaved=vi.fn();const current={...target,steps:target.steps.map(s=>({...s,canEdit}))};
 return {...render(<OsanPhotoEditor projectId="project-a" target={current} stage={1} userKey="worker" mutationAllowed={mutationAllowed} canManageStages={canManageStages} onSaved={onSaved}/>),onSaved,current};
}
function enter(){fireEvent.click(screen.getByRole('button',{name:'사진 수정'}));fireEvent.change(screen.getByLabelText('사진 선택'),{target:{files:[new File(['jpeg'],'replacement.jpg',{type:'image/jpeg'})]}});}
function reason(value='사진 초점을 보정합니다.'){fireEvent.change(screen.getByRole('textbox',{name:/수정 사유/}),{target:{value}});}
beforeEach(()=>{vi.resetAllMocks();HTMLDialogElement.prototype.showModal=function(){this.open=true};HTMLDialogElement.prototype.close=function(){this.open=false};URL.createObjectURL=vi.fn(()=>'blob:synthetic');URL.revokeObjectURL=vi.fn();vi.mocked(getOsanProgressPhoto).mockResolvedValue(new Blob(['old'],{type:'image/jpeg'}));vi.mocked(fetchJson).mockResolvedValue({});});
describe('오산 사진 직접 수정',()=>{
 it('Gate 권한이 있으면 승인 조회 없이 수정하며 매 저장에 사유가 필수다',async()=>{
  const view=show();expect(fetchJson).not.toHaveBeenCalled();expect(screen.queryByText(/승인 요청/)).not.toBeInTheDocument();enter();
  expect(screen.getByRole('button',{name:'사진 변경 저장'})).toBeDisabled();reason('   ');expect(screen.getByRole('button',{name:'사진 변경 저장'})).toBeDisabled();reason();
  fireEvent.click(screen.getByRole('button',{name:'사진 변경 저장'}));await waitFor(()=>expect(view.onSaved).toHaveBeenCalledOnce());
  const [url,,options]=vi.mocked(fetchJson).mock.calls[0];expect(url).toBe('/api/osan/projects/project-a/progress/steps/step-a/edit');const body=options!.body as FormData;
  expect(body.get('reason')).toBe('사진 초점을 보정합니다.');expect(JSON.parse(body.get('targets') as string)).toEqual([{targetId:'target-a',expectedVersion:2}]);
  expect(screen.getByRole('button',{name:'사진 수정'})).toBeEnabled();fireEvent.click(screen.getByRole('button',{name:'사진 수정'}));expect(screen.getByRole('textbox',{name:/수정 사유/})).toHaveValue('');
 });
 it('Gate 권한이 없으면 수정 버튼을 숨긴다',()=>{show(false);expect(screen.queryByRole('button',{name:'사진 수정'})).not.toBeInTheDocument();expect(fetchJson).not.toHaveBeenCalled();});
 it('열린 입력도 읽기 전용 전환 시 잠근다',()=>{const view=show();enter();view.rerender(<OsanPhotoEditor projectId="project-a" target={view.current} stage={1} mutationAllowed={false} onSaved={view.onSaved}/>);expect(screen.getByLabelText('사진 선택')).toBeDisabled();expect(screen.getByRole('textbox',{name:/수정 사유/})).toBeDisabled();expect(screen.getByRole('button',{name:'사진 변경 저장'})).toBeDisabled();});
 it('응답 불확실 시 같은 본문/식별자로 재시도하고 입력을 잠근다',async()=>{
  const view=show();enter();reason();vi.mocked(fetchJson).mockRejectedValueOnce(new ApiError(503,'결과 확인 실패'));fireEvent.click(screen.getByRole('button',{name:'사진 변경 저장'}));
  await screen.findByText('결과 확인 실패');expect(screen.getByLabelText('사진 선택')).toBeDisabled();expect(screen.getByRole('textbox',{name:/수정 사유/})).toBeDisabled();expect(screen.getByRole('button',{name:'취소'})).toBeDisabled();
  const body=vi.mocked(fetchJson).mock.calls[0][2]!.body;fireEvent.click(screen.getByRole('button',{name:'같은 사진으로 저장 재시도'}));await waitFor(()=>expect(view.onSaved).toHaveBeenCalledOnce());expect(vi.mocked(fetchJson).mock.calls[1][2]!.body).toBe(body);
 });
 it('서버 입력 거부는 수정하여 재시도할 수 있다',async()=>{show();enter();reason();vi.mocked(fetchJson).mockRejectedValueOnce(new ApiError(422,'손상된 사진입니다.'));fireEvent.click(screen.getByRole('button',{name:'사진 변경 저장'}));await screen.findByText('손상된 사진입니다.');expect(screen.getByLabelText('사진 선택')).toBeEnabled();expect(screen.getByRole('textbox',{name:/수정 사유/})).toBeEnabled();});
 it('관리자는 사진 없이 코멘트와 수정 사유로 저장한다',async()=>{const view=show(true,true);fireEvent.click(screen.getByRole('button',{name:'사진 수정'}));fireEvent.change(screen.getByRole('textbox',{name:/^코멘트/}),{target:{value:'검사 확인'}});reason();fireEvent.click(screen.getByRole('button',{name:'사진 변경 저장'}));await waitFor(()=>expect(view.onSaved).toHaveBeenCalledOnce());});
});
describe('오산 단계 저장 이력', () => {
  it('별도 이력 조회에서 이전 사진·코멘트·등록자와 초기화 사유를 보존해 표시한다', async () => {
    HTMLDialogElement.prototype.showModal = function() { this.open = true; };
    HTMLDialogElement.prototype.close = function() { this.open = false; };
    vi.mocked(fetchJson).mockResolvedValue([
      { id: 'reset', eventType: 'Reset', actorDisplayName: '초기화 관리자', occurredAtUtc: '2026-09-10T02:00:00Z', comment: null, reason: '재검사 필요', photos: [] },
      { id: 'original', eventType: 'Completed', actorDisplayName: '최초 작업자', occurredAtUtc: '2026-09-10T00:00:00Z', comment: '최초 검사 기록', reason: null,
        photos: [{ photoId: 'previous-photo', displayOrder: 1, fileName: 'original.jpg', contentType: 'image/jpeg', sizeBytes: 9 }] }
    ]);
    render(<OsanStageHistory projectId="project-a" stepId="step-a" title="합성 대상 · 입고검사" userKey="other-worker" />);
    expect(fetchJson).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: '이력 보기' }));
    expect(await screen.findByText('재검사 필요')).toBeVisible();
    const original = screen.getByText(/최초 작업자/).closest('details')!;
    expect(original.open).toBe(false);
    fireEvent.click(original.querySelector('summary')!);
    expect(screen.getByText('최초 검사 기록')).toBeVisible();
    expect(await screen.findByRole('img', { name: '사진 1' })).toBeVisible();
    expect(getOsanProgressPhoto).toHaveBeenCalledWith('project-a', 'previous-photo', 'other-worker', expect.any(AbortSignal), false);
    expect(fetchJson).toHaveBeenCalledWith('/api/osan/projects/project-a/progress/steps/step-a/history', 'other-worker', expect.objectContaining({ signal: expect.any(AbortSignal) }));
  });
});


it('이력에서 수정 사유와 당시 코멘트를 함께 표시한다',async()=>{vi.mocked(fetchJson).mockResolvedValue([{id:'edited',eventType:'Edited',actorDisplayName:'수정자',occurredAtUtc:'2026-10-07T00:00:00Z',comment:'변경된 검사 기록',reason:'사진 교체 필요',photos:[]}]);render(<OsanStageHistory projectId="p" stepId="s" title="입고검사"/>);fireEvent.click(screen.getByRole('button',{name:'이력 보기'}));expect(await screen.findByText('사진 교체 필요')).toBeVisible();expect(screen.getByText('변경된 검사 기록')).toBeVisible();});
