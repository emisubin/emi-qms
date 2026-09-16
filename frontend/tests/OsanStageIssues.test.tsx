import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { OsanStageIssueActions } from '../src/OsanStageIssueActions';
import { OsanStepper } from '../src/OsanStepper';
import { completionUnavailable, mutateOsanIssue, osanStageNames, type OsanProgressTarget } from '../src/osanProgress';
import { nextOsanWork } from '../src/osanNextWork';
vi.mock('../src/osanProgress', async original => ({ ...await original<typeof import('../src/osanProgress')>(), mutateOsanIssue: vi.fn() }));
const issue = { issueId:'i',registeredAtUtc:'2026-09-16T01:00:00Z', registeredByUserId:'u', registeredByDisplayName:'작업자', comment:'배선 이상',photos:[],lastRecordedAtUtc:'2026-09-16T01:00:00Z',lastRecordedByDisplayName:'작업자' };
function target(): OsanProgressTarget { return {targetId:'t',displayName:'패널 1',sequenceNumber:1,status:'InProgress',version:3,startedAtUtc:null,startedByUserId:null,startedByDisplayName:null,steps:osanStageNames.map((stepName,i)=>({stepId:`s${i+1}`,sequenceNumber:i+1,stepCode:String(i),stepName,status:i<2?'Completed':'NotStarted',startedAtUtc:null,completedAtUtc:null,completedByUserId:null,completedByDisplayName:null,photos:[],guidancePhotos:[],guidanceDescription:null,canCompleteIndividual:i===2||i===4,canCompleteBatch:i===2||i===4,canRegisterIssue:true,canResolveIssue:true}))}; }
beforeEach(()=> {vi.resetAllMocks();HTMLDialogElement.prototype.showModal=function(){this.open=true};HTMLDialogElement.prototype.close=function(){this.open=false};});
it('배선검사 미완료 이상은 8계통을 허용하지만 포장은 막는다',()=>{
 const t=target();t.steps[2].openIssue=issue;t.steps[3].canCompleteIndividual=true;
 expect(completionUnavailable([t],4,'individual')).toBeNull();
 t.steps.forEach((s,i)=>{if(i!==2)s.status=i<6?'Completed':'NotStarted'});t.steps[6].canCompleteIndividual=true;
 expect(completionUnavailable([t],7,'individual')).not.toBeNull();
 expect(completionUnavailable([t],3,'individual')).toContain('조치 완료');
});
it('동작검사는 처음부터 가능하지만 출하검사는 앞 단계가 필요하다',()=>{
 const t=target();t.steps.forEach(s=>{s.status='NotStarted';s.canCompleteIndividual=true});
 expect(completionUnavailable([t],5,'individual')).toBeNull();
 t.steps[4].status='Completed';expect(completionUnavailable([t],6,'individual')).not.toBeNull();
});
it('자동 진입은 미조치 이상 다음의 실행 가능한 단계를 우선한다',()=>{
 const t=target();t.steps[2].openIssue=issue;t.steps[2].canCompleteIndividual=false;t.steps[3].canCompleteIndividual=true;
 expect(nextOsanWork([t])).toEqual({targetId:'t',stage:4});
 t.steps.forEach(s=>s.canCompleteIndividual=false);expect(nextOsanWork([t])).toEqual({targetId:'t',stage:3});
});
it('집계에 미조치 이상이 하나라도 있으면 빨강이 우선이고 가능 수량을 표시한다',()=>{
 render(<OsanStepper stages={[{sequenceNumber:3,stepCode:'WIRING',stepName:'배선검사',completedTargetCount:1,totalTargetCount:2,openIssueTargetCount:1,availableTargetCount:0},{sequenceNumber:4,stepCode:'EIGHT',stepName:'8계통',completedTargetCount:0,totalTargetCount:2,openIssueTargetCount:0,availableTargetCount:2}]}/>);
 expect(screen.getByLabelText(/배선검사.*미조치 이상/).querySelector('i')).toHaveClass('is-issue');
 expect(screen.getByText('가능 2대')).toBeInTheDocument();expect(document.querySelector('.is-next')).toBeInTheDocument();
 expect(screen.queryByText('✓')).not.toBeInTheDocument();
});
function renderActions(t=target(), canManage=false){const onSaved=vi.fn();render(<OsanStageIssueActions projectId="p" target={t} stage={3} mutationAllowed canManage={canManage} disabled={false} onBusy={vi.fn()} onSaved={onSaved}/>);return onSaved}
it('등록 코멘트는 필수이고 사진 없이 등록하며 저장 중 중복 제출을 막는다',async()=>{
 let resolve!: (value:never)=>void;vi.mocked(mutateOsanIssue).mockImplementation(()=>new Promise(r=>{resolve=r}));const onSaved=renderActions();
 fireEvent.click(screen.getByRole('button',{name:'이상 등록'}));const dialog=screen.getByRole('dialog');
 fireEvent.click(within(dialog).getByRole('button',{name:'이상 등록'}));expect(screen.getByRole('alert')).toHaveTextContent('코멘트');expect(mutateOsanIssue).not.toHaveBeenCalled();
 fireEvent.change(screen.getByRole('textbox'),{target:{value:'단자 체결 불량'}});fireEvent.click(within(dialog).getByRole('button',{name:'이상 등록'}));
 expect(within(dialog).getByRole('button',{name:'저장 중…'})).toBeDisabled();expect(mutateOsanIssue).toHaveBeenCalledTimes(1);
 expect(vi.mocked(mutateOsanIssue).mock.calls[0][2]).toMatchObject({comment:'단자 체결 불량',photos:[],targets:[{targetId:'t',expectedVersion:3}]});
 await act(async()=>resolve({} as never));expect(onSaved).toHaveBeenCalledOnce();
});
it('조치 완료는 일반 사용자 사진 필수, 관리자 코멘트만 허용',async()=>{
 const t=target();t.steps[2].openIssue=issue;const view=render(<OsanStageIssueActions projectId="p" target={t} stage={3} mutationAllowed canManage={false} disabled={false} onBusy={vi.fn()} onSaved={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button',{name:'조치 완료'}));fireEvent.change(screen.getByRole('textbox'),{target:{value:'재체결 완료'}});fireEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:'조치 완료'}));
 expect(screen.getByRole('alert')).toHaveTextContent('사진을 1장');expect(mutateOsanIssue).not.toHaveBeenCalled();view.unmount();
 vi.mocked(mutateOsanIssue).mockResolvedValue({} as never);renderActions(t,true);fireEvent.click(screen.getByRole('button',{name:'조치 완료'}));fireEvent.change(screen.getByRole('textbox'),{target:{value:'관리자 검증 완료'}});fireEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:'조치 완료'}));
 await waitFor(()=>expect(mutateOsanIssue).toHaveBeenCalledWith('p','resolve',expect.objectContaining({comment:'관리자 검증 완료'}),undefined));
});
it('응답이 불명확할 때 사진/코멘트를 고정하고 같은 요청 번호로 재시도한다',async()=>{
 vi.mocked(mutateOsanIssue).mockRejectedValueOnce(new Error('네트워크 오류')).mockResolvedValueOnce({} as never);renderActions();fireEvent.click(screen.getByRole('button',{name:'이상 등록'}));fireEvent.change(screen.getByRole('textbox'),{target:{value:'배선 확인 필요'}});fireEvent.click(within(screen.getByRole('dialog')).getByRole('button',{name:'이상 등록'}));
 await screen.findByText('네트워크 오류');expect(screen.getByRole('textbox')).toBeDisabled();const request=vi.mocked(mutateOsanIssue).mock.calls[0][2];fireEvent.click(screen.getByRole('button',{name:'저장 재시도'}));await waitFor(()=>expect(mutateOsanIssue).toHaveBeenCalledTimes(2));expect(vi.mocked(mutateOsanIssue).mock.calls[1][2]).toBe(request);
});
