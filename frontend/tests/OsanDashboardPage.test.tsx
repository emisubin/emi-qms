import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanDashboardPage } from '../src/OsanDashboardPage';
import { ApiError } from '../src/api';
import * as api from '../src/osanDashboard';
vi.mock('../src/osanDashboard', async original => ({ ...await original<typeof import('../src/osanDashboard')>(), getOsanDashboard: vi.fn() }));
const fixture = (title = '오산 검수 프로젝트'): api.OsanDashboardResponse => ({
  summary: { totalCount: 26, notStartedCount: 15, inProgressCount: 10, completedCount: 1 },
  totalCount: 26, page: 1, pageSize: 11,
  items: [{ projectId: 'p1', title, projectCode: 'OS-1', customerName: '검수 거래처', productName: '패널',
    poNumber: 'PO-KEY', workOrderNumber: 'WO-KEY', quantity: 2, deliveryDate: '2026-10-01', status: 'InProgress',
    completedStepCount: 1, totalStepCount: 14, progressPercent: 7,
    stages: [{ sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', completedTargetCount: 1, totalTargetCount: 2 }] }]
});
beforeEach(() => { vi.clearAllMocks(); vi.mocked(api.getOsanDashboard).mockResolvedValue(fixture()); });
describe('오산 진행 현황', () => {
  it('현재 페이지 행 수가 아닌 서버 전체 집계와 부분 완료 진행률을 표시하고 상세를 연결한다', async () => {
    const open = vi.fn(); render(<OsanDashboardPage developmentUserKey="user" onOpen={open} />);
    const button = await screen.findByRole('button', { name: '오산 검수 프로젝트 진행 상세 열기' });
    expect(within(screen.getByLabelText('프로젝트 요약')).getByText('26')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '7');
    expect(screen.getByLabelText('입고검사 1/2 완료')).toBeInTheDocument();
    fireEvent.click(button); expect(open).toHaveBeenCalledWith('p1');
  });
  it('검색을 제출하며 상태 필터를 변경하면 페이지를 1로 되돌리고 서버 요약을 보존한다', async () => {
    render(<OsanDashboardPage onOpen={vi.fn()} />); await screen.findByRole('button', { name: '다음 페이지' });
    fireEvent.click(screen.getByRole('button', { name: '다음 페이지' }));
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined, { search: '', status: 'All', page: 2 }, expect.any(AbortSignal)));
    fireEvent.change(screen.getByRole('textbox', { name: '프로젝트 검색' }), { target: { value: ' PO-KEY ' } });
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined, { search: 'PO-KEY', status: 'All', page: 1 }, expect.any(AbortSignal)));
    fireEvent.click(screen.getByRole('button', { name: '필터' }));
    fireEvent.change(screen.getByRole('combobox', { name: '상태' }), { target: { value: 'Completed' } });
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined, { search: 'PO-KEY', status: 'Completed', page: 1 }, expect.any(AbortSignal)));
    expect(within(screen.getByLabelText('프로젝트 요약')).getByText('26')).toBeInTheDocument();
  });
  it('늦은 검색 응답과 사용자 전환 전 응답을 표시하지 않는다', async () => {
    let resolveOld!: (value: api.OsanDashboardResponse) => void;
    vi.mocked(api.getOsanDashboard).mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
    const view = render(<OsanDashboardPage developmentUserKey="old-user" onOpen={vi.fn()} />);
    view.rerender(<OsanDashboardPage developmentUserKey="new-user" onOpen={vi.fn()} />);
    await screen.findByRole('button', { name: '오산 검수 프로젝트 진행 상세 열기' });
    await act(async () => { resolveOld(fixture('이전 사용자 프로젝트')); });
    expect(screen.queryByText('이전 사용자 프로젝트')).not.toBeInTheDocument();
    expect(vi.mocked(api.getOsanDashboard).mock.calls[0][2].aborted).toBe(true);
  });
  it('이전 검색이 늦게 성공해도 최신 검색 결과를 덮지 않는다', async () => {
    let resolveOld!: (value: api.OsanDashboardResponse) => void;
    vi.mocked(api.getOsanDashboard).mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
    render(<OsanDashboardPage onOpen={vi.fn()} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'new' } });
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    await screen.findByRole('button', { name: '오산 검수 프로젝트 진행 상세 열기' });
    await act(async () => { resolveOld(fixture('오래된 결과')); });
    expect(screen.queryByText('오래된 결과')).not.toBeInTheDocument();
  });
  it('조회 실패는 재시도하고 권한 거부에는 이전 집계를 노출하지 않는다', async () => {
    vi.mocked(api.getOsanDashboard).mockRejectedValueOnce(new Error('조회 오류'));
    render(<OsanDashboardPage onOpen={vi.fn()} />);
    fireEvent.click(await screen.findByRole('button', { name: '다시 시도' }));
    await screen.findByRole('button', { name: '오산 검수 프로젝트 진행 상세 열기' });
    vi.mocked(api.getOsanDashboard).mockRejectedValueOnce(new ApiError(403, '거부'));
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    await screen.findByText('진행 현황을 볼 권한이 없습니다.');
    expect(screen.queryByText('26')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '다시 시도' })).not.toBeInTheDocument();
  });
  it('빈 검색을 초기화하고 마지막 페이지의 다음 버튼을 막는다', async () => {
    vi.mocked(api.getOsanDashboard).mockResolvedValue({ ...fixture(), totalCount: 0, items: [] });
    render(<OsanDashboardPage onOpen={vi.fn()} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: '없음' } });
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    fireEvent.click(await screen.findByRole('button', { name: '검색 조건 초기화' }));
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined, { search: '', status: 'All', page: 1 }, expect.any(AbortSignal)));
    await screen.findByText('등록된 프로젝트가 없습니다.');
    expect(screen.getByRole('button', { name: '다음 페이지' })).toBeDisabled();
  });
});
