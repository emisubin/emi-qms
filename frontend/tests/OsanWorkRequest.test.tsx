import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { OsanWorkRequest } from '../src/OsanWorkRequest';
import { fetchJson } from '../src/api';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
const recipients=[{userId:'a',displayName:'제조 담당',departmentName:'제조팀'},{userId:'b',displayName:'품질 담당',departmentName:'품질팀'}];
beforeEach(()=>{vi.resetAllMocks();HTMLDialogElement.prototype.showModal=function(){this.open=true;};HTMLDialogElement.prototype.close=function(){this.open=false;};vi.mocked(fetchJson).mockResolvedValue({recipients});});
function show(){render(<OsanWorkRequest projectId="p" targetId="t" targetName="Rack 1" title="예시 장비" part="Rack" workOrder="WO-001" stage={3} stageName="배선검사" disabled={false} onSaved={vi.fn()}/>);fireEvent.click(screen.getByRole('button',{name:'공정 진행 요청'}));}
it('검색해도 이전 선택을 유지하며 선택한 담당자만 요청한다',async()=>{
 show();fireEvent.click(await screen.findByRole('checkbox',{name:'제조 담당 제조팀'}));
 fireEvent.change(screen.getByRole('searchbox'),{target:{value:'품질'}});
 fireEvent.click(screen.getByRole('checkbox',{name:'품질 담당 품질팀'}));
 expect(screen.getByText('2명 선택')).toBeInTheDocument();
 fireEvent.click(screen.getByRole('button',{name:'진행 요청 보내기'}));
 await waitFor(()=>expect(fetchJson).toHaveBeenCalledTimes(2));
 const call=vi.mocked(fetchJson).mock.calls[1];expect(call[0]).toContain('/progress/work-requests');
 expect(JSON.parse(call[2]!.body as string)).toMatchObject({targetId:'t',stageSequence:3,recipientIds:['a','b']});
});
it('미선택 전송을 막고 응답 불명확 시 같은 요청으로 재시도한다',async()=>{
 show();await screen.findByRole('checkbox',{name:'제조 담당 제조팀'});expect(screen.getByRole('button',{name:'진행 요청 보내기'})).toBeDisabled();
 fireEvent.click(screen.getByRole('checkbox',{name:'제조 담당 제조팀'}));
 vi.mocked(fetchJson).mockRejectedValueOnce(new Error('응답 확인 불가')).mockResolvedValueOnce({});
 fireEvent.click(screen.getByRole('button',{name:'진행 요청 보내기'}));await screen.findByRole('alert');
 const first=vi.mocked(fetchJson).mock.calls[1][2]!.body;
 expect(screen.getByRole('checkbox',{name:'품질 담당 품질팀'})).toBeDisabled();
 fireEvent.click(screen.getByRole('button',{name:'같은 요청 재시도'}));await waitFor(()=>expect(fetchJson).toHaveBeenCalledTimes(3));expect(vi.mocked(fetchJson).mock.calls[2][2]!.body).toBe(first);
});
it('저장 중 중복 전송과 창 닫기를 막는다',async()=>{
 show();fireEvent.click(await screen.findByRole('checkbox',{name:'제조 담당 제조팀'}));
 let resolve!:(v:unknown)=>void;vi.mocked(fetchJson).mockImplementationOnce(()=>new Promise(r=>{resolve=r;}));
 fireEvent.click(screen.getByRole('button',{name:'진행 요청 보내기'}));
 expect(screen.getByRole('button',{name:'요청 중…'})).toBeDisabled();
 expect(within(screen.getByRole('dialog')).getByRole('button',{name:'취소'})).toBeDisabled();
 await act(async()=>resolve({}));
});
