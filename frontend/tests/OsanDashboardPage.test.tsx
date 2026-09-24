import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanDashboardPage } from '../src/OsanDashboardPage';
import { clearOsanListSnapshots } from '../src/osanListState';
import * as api from '../src/osanDashboard';

vi.mock('../src/osanDashboard', async original => ({ ...await original<typeof import('../src/osanDashboard')>(), getOsanDashboard: vi.fn() }));

const fixture = (totalCount = 51): api.OsanDashboardResponse => ({
  summary: { totalCount, notStartedCount: 15, inProgressCount: 10, completedCount: 1, holdCount: 2, openIssueProjectCount: 3 },
  totalCount, page: 1, pageSize: 50, customers: ['고객 A', '고객 B'],
  items: [{ projectId: 'p1', title: '검수 프로젝트', projectCode: 'OS-1', customerName: '고객 A', productName: '패널',
    poNumber: 'PO-KEY', workOrderNumber: 'WO-KEY', quantity: 2, deliveryDate: '2026-10-01', status: 'InProgress',
    completedStepCount: 1, totalStepCount: 14, progressPercent: 7,
    stages: [{ sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', completedTargetCount: 1, totalTargetCount: 2 }] }]
});

beforeEach(() => {
  clearOsanListSnapshots();
  vi.clearAllMocks();
  vi.mocked(api.getOsanDashboard).mockResolvedValue(fixture());
});

describe('오산 홈·진행 현황 공용 목록', () => {
  it.each(['home', 'progress'] as const)('%s에서 공정 이상 프로젝트 수와 50개 페이지를 표시한다', async view => {
    render(<OsanDashboardPage view={view} stateScopeKey="user-1" onOpen={vi.fn()} />);
    await screen.findByRole('button', { name: '검수 프로젝트 ' + (view === 'home' ? '프로젝트' : '진행') + ' 상세 열기' });
    const summary = screen.getByLabelText('프로젝트 요약');
    expect(within(summary).getByRole('button', { name: /공정 이상/ })).toHaveTextContent('3');
    expect(screen.getByText('1 / 2')).toBeInTheDocument();
    expect(api.getOsanDashboard).toHaveBeenCalledWith(undefined,
      expect.objectContaining({ view, page: 1, customers: [], statuses: [], kpi: null }), expect.any(AbortSignal));
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '7');
  });

  it('고객사와 상태는 각각 복수 선택하고 날짜·검색·KPI는 기존 조건을 유지한다', async () => {
    render(<OsanDashboardPage view="home" stateScopeKey="user-1" onOpen={vi.fn()} />);
    await screen.findByRole('button', { name: '검수 프로젝트 프로젝트 상세 열기' });
    fireEvent.click(screen.getByRole('button', { name: '필터' }));
    fireEvent.click(screen.getByRole('checkbox', { name: '고객 A' }));
    fireEvent.click(screen.getByRole('checkbox', { name: '고객 B' }));
    fireEvent.click(screen.getByRole('checkbox', { name: '공정 시작 전' }));
    fireEvent.click(screen.getByRole('checkbox', { name: '공정 진행 중' }));
    fireEvent.change(screen.getByLabelText('납기 시작일'), { target: { value: '2026-09-01' } });
    fireEvent.change(screen.getByLabelText('납기 종료일'), { target: { value: '2026-10-31' } });
    fireEvent.change(screen.getByRole('textbox', { name: '프로젝트 검색' }), { target: { value: ' WO-KEY ' } });
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    fireEvent.click(screen.getByRole('button', { name: /공정 이상/ }));
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined,
      expect.objectContaining({ customers: ['고객 A', '고객 B'], statuses: ['NotStarted', 'InProgress'],
        dueFrom: '2026-09-01', dueTo: '2026-10-31', search: 'WO-KEY', kpi: 'OpenIssue', page: 1, view: 'home' }),
      expect.any(AbortSignal)));
    expect(screen.getByRole('button', { name: /공정 이상/ })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: /공정 이상/ }));
    await waitFor(() => expect(vi.mocked(api.getOsanDashboard).mock.lastCall?.[1]).toMatchObject({
      customers: ['고객 A', '고객 B'], statuses: ['NotStarted', 'InProgress'], kpi: null }));
  });

  it('상세 왕복 후 검색어·페이지·선택 조건을 복원하고 사용자를 분리한다', async () => {
    const open = vi.fn();
    const first = render(<OsanDashboardPage view="progress" stateScopeKey="user-1" onOpen={open} />);
    await screen.findByRole('button', { name: '다음 페이지' });
    fireEvent.click(screen.getByRole('button', { name: '다음 페이지' }));
    fireEvent.change(screen.getByRole('textbox', { name: '프로젝트 검색' }), { target: { value: '패널' } });
    fireEvent.click(screen.getByRole('button', { name: '검색' }));
    fireEvent.click(screen.getByRole('button', { name: '필터' }));
    fireEvent.click(screen.getByRole('checkbox', { name: '고객 A' }));
    fireEvent.click(await screen.findByRole('button', { name: '검수 프로젝트 진행 상세 열기' }));
    expect(open).toHaveBeenCalledWith('p1');
    first.unmount();
    const second = render(<OsanDashboardPage view="progress" stateScopeKey="user-1" onOpen={vi.fn()} />);
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined,
      expect.objectContaining({ search: '패널', customers: ['고객 A'] }), expect.any(AbortSignal)));
    expect(screen.getByRole('textbox', { name: '프로젝트 검색' })).toHaveValue('패널');
    second.unmount();
    render(<OsanDashboardPage view="progress" stateScopeKey="user-2" onOpen={vi.fn()} />);
    await waitFor(() => expect(api.getOsanDashboard).toHaveBeenLastCalledWith(undefined,
      expect.objectContaining({ search: '', customers: [] }), expect.any(AbortSignal)));
  });

  it('이전 요청의 늦은 응답은 새 사용자 목록을 덮지 않는다', async () => {
    let resolveOld!: (value: api.OsanDashboardResponse) => void;
    vi.mocked(api.getOsanDashboard).mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
    const page = render(<OsanDashboardPage stateScopeKey="old" onOpen={vi.fn()} />);
    page.rerender(<OsanDashboardPage stateScopeKey="new" onOpen={vi.fn()} />);
    await screen.findByRole('button', { name: '검수 프로젝트 진행 상세 열기' });
    await act(async () => resolveOld(fixture()));
    expect(vi.mocked(api.getOsanDashboard).mock.calls[0][2].aborted).toBe(true);
  });
});
