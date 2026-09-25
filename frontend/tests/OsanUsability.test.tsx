import { fireEvent, render, screen, within } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { OsanMobileTools } from '../src/OsanMobileTools';
import { OsanCustomerAdminPage } from '../src/OsanAdminPage';
import { isOsanOverdue } from '../src/osanDday';
import { fetchJson } from '../src/api';
vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));

it('모바일 일반 메뉴와 관리자 전용 하위 메뉴를 구분하고 정확한 경로로 이동한다', () => {
 const onNavigate = vi.fn();
 const page = render(<OsanMobileTools current="home" onNavigate={onNavigate} onScan={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button', {name:'메뉴 열기'}));
 expect(screen.getByRole('button', {name:'공지사항'})).toBeInTheDocument();
 expect(screen.queryByRole('button', {name:'고객사 관리'})).not.toBeInTheDocument();
 fireEvent.click(screen.getByRole('button', {name:'공지사항'}));
 expect(onNavigate).toHaveBeenLastCalledWith('notice-board');
 page.rerender(<OsanMobileTools admin current="home" onNavigate={onNavigate} onScan={vi.fn()}/>);
 fireEvent.click(screen.getByRole('button', {name:'메뉴 열기'}));
 expect(screen.getByRole('button', {name:'Gate 설정'})).toBeInTheDocument();
 fireEvent.click(screen.getByRole('button', {name:'Gate 승인 대기'}));
 expect(onNavigate).toHaveBeenLastCalledWith('osan-gate-approvals');
 expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
});

it('납기 초과만 강조하며 HOLD와 포장완료를 제외하고 HOLD 해제 시 재평가한다', () => {
 const p = {deliveryDate:'2026-09-24',status:'InProgress',deliveryHold:false};
 expect(isOsanOverdue(p,'2026-09-25')).toBe(true);
 expect(isOsanOverdue({...p,deliveryHold:true},'2026-09-25')).toBe(false);
 expect(isOsanOverdue({...p,status:'Completed'},'2026-09-25')).toBe(false);
 expect(isOsanOverdue(p,'2026-09-24')).toBe(false);
 expect(isOsanOverdue(p,'2026-09-23')).toBe(false);
 expect(isOsanOverdue({...p,deliveryDate:'invalid'},'2026-09-25')).toBe(false);
});

it('고객사별 품질·제조 인원 수와 사용자 부서·배정 필터를 함께 적용한다', async () => {
 vi.mocked(fetchJson).mockResolvedValue({customers:[{customerId:'c1',name:'고객A',version:1}],users:[
  {userId:'q1',displayName:'품질담당1',departmentName:'품질',customerIds:['c1'],version:1},
  {userId:'q2',displayName:'품질담당2',departmentName:'품질',customerIds:[],version:1},
  {userId:'m1',displayName:'제조담당',departmentName:'제조',customerIds:['c1'],version:1},
  {userId:'o1',displayName:'구매담당',departmentName:'구매',customerIds:['c1'],version:1}
 ]});
 render(<OsanCustomerAdminPage/>);
 await screen.findByRole('button',{name:'고객A 더보기'});
 expect(screen.queryByRole('columnheader',{name:'알림 담당자'})).not.toBeInTheDocument();
 expect(screen.getByRole('columnheader',{name:'품질 담당자'})).toBeInTheDocument();
 const row=screen.getByRole('button',{name:'고객A 더보기'}).closest('[role="row"]')!;
 expect(within(row as HTMLElement).getAllByText('1명')).toHaveLength(2);
 fireEvent.click(screen.getByRole('tab',{name:'사용자별 고객사'}));
 fireEvent.change(screen.getByLabelText('부서 필터'),{target:{value:'품질'}});
 fireEvent.change(screen.getByLabelText('배정 여부 필터'),{target:{value:'unassigned'}});
 expect(screen.getByRole('button',{name:'품질담당2 더보기'})).toBeInTheDocument();
 expect(screen.queryByRole('button',{name:'품질담당1 더보기'})).not.toBeInTheDocument();
 expect(screen.queryByRole('button',{name:'제조담당 더보기'})).not.toBeInTheDocument();
 fireEvent.change(screen.getByLabelText('배정 여부 필터'),{target:{value:'assigned'}});
 expect(screen.getByRole('button',{name:'품질담당1 더보기'})).toBeInTheDocument();
 expect(screen.queryByRole('button',{name:'품질담당2 더보기'})).not.toBeInTheDocument();
});
