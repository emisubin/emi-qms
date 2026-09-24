import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { StrictMode } from 'react';
import { MaintenanceAnnouncement } from '../src/MaintenanceAnnouncement';
import { useMaintenanceStatus, type MaintenanceStatus } from '../src/useMaintenanceStatus';
import { fetchJson } from '../src/api';

vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));
const active: MaintenanceStatus = {
  releaseId: 'release-a', version: 2, popupVersion: 1, title: '오산 업데이트', body: '변경된 기능 안내',
  startsAtUtc: '2026-09-24T00:00:00Z', expectedEndsAtUtc: '2026-09-24T01:00:00Z',
  state: 'Active', writeBlocked: true, noticeId: 'notice-a', popupPending: true
};
const complete: MaintenanceStatus = { ...active, state: 'Completed', writeBlocked: false, popupPending: false };

function Screen() {
  const maintenance = useMaintenanceStatus('user-a', 'scope-a', true);
  return <><label>작성 중인 내용<input defaultValue="저장 전 초안" /></label>
    <MaintenanceAnnouncement status={maintenance.state} unavailable={maintenance.unavailable}
      userKey="user-a" scope="scope-a" onOpenNotice={vi.fn()} /></>;
}

beforeEach(() => { vi.resetAllMocks(); vi.useFakeTimers(); });
afterEach(() => vi.useRealTimers());

it('배포 안내를 계정·버전당 한 번 청구하고, 닫은 뒤 상태가 해제되어도 작성 중인 값을 유지한다', async () => {
  let reads = 0;
  vi.mocked(fetchJson).mockImplementation(async (url) => url === '/api/maintenance'
    ? (++reads === 1 ? active : complete) : { claimed: true });
  render(<Screen />);
  await act(async () => {});
  expect(screen.getByRole('region', { name: '업데이트 안내' })).toBeInTheDocument();
  expect(screen.getByRole('status')).toHaveTextContent('업데이트 중에는 데이터를 저장할 수 없습니다.');
  fireEvent.change(screen.getByLabelText('작성 중인 내용'), { target: { value: '복구할 작업 내용' } });
  fireEvent.click(screen.getByRole('button', { name: '업데이트 안내 닫기' }));
  await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
  expect(screen.queryByRole('region', { name: '업데이트 안내' })).not.toBeInTheDocument();
  expect(screen.getByRole('status')).toHaveTextContent('업데이트가 완료되었습니다. 입력 내용을 확인하고 다시 저장해 주세요.');
  expect(screen.getByLabelText('작성 중인 내용')).toHaveValue('복구할 작업 내용');
  expect(vi.mocked(fetchJson).mock.calls.filter(([url]) => url.includes('/popup/'))).toHaveLength(1);
});

it('StrictMode의 effect 재실행에서도 첫 청구 성공 안내를 잃지 않는다', async () => {
  let claims = 0;
  vi.mocked(fetchJson).mockImplementation(async () => ({ claimed: ++claims === 1 }));
  render(<StrictMode><MaintenanceAnnouncement status={active} unavailable={false}
    userKey="user-a" scope="scope-a" onOpenNotice={vi.fn()} /></StrictMode>);
  await act(async () => {});
  expect(screen.getByRole('region', { name: '업데이트 안내' })).toBeInTheDocument();
  expect(claims).toBe(1);
});

it('일정 변경 때만 새 팝업 버전을 청구하고 상태 전환만으로 재표시하지 않는다', async () => {
  vi.mocked(fetchJson).mockResolvedValue({ claimed: true });
  const props = { unavailable: false, userKey: 'user-a', scope: 'scope-a', onOpenNotice: vi.fn() };
  const { rerender } = render(<MaintenanceAnnouncement {...props} status={active} />);
  await act(async () => {});
  fireEvent.click(screen.getByRole('button', { name: '업데이트 안내 닫기' }));
  rerender(<MaintenanceAnnouncement {...props} status={{ ...active, version: 3, state: 'Delayed' }} />);
  await act(async () => {});
  expect(screen.queryByRole('region', { name: '업데이트 안내' })).not.toBeInTheDocument();
  rerender(<MaintenanceAnnouncement {...props} status={{ ...active, version: 4, popupVersion: 2,
    state: 'Delayed', expectedEndsAtUtc: '2026-09-24T02:00:00Z' }} />);
  await act(async () => {});
  expect(screen.getByRole('region', { name: '업데이트 안내' })).toBeInTheDocument();
  expect(vi.mocked(fetchJson).mock.calls.map(([url]) => url)).toEqual([
    '/api/maintenance/release-a/popup/1/claim',
    '/api/maintenance/release-a/popup/2/claim'
  ]);
});


it('업데이트 팝업은 공지 전문 대신 시간·대상을 안내하고 공지 상세로 연결한다', async () => {
  vi.mocked(fetchJson).mockResolvedValue({ claimed: true });
  const onOpenNotice = vi.fn();
  render(<MaintenanceAnnouncement status={active} unavailable={false}
    userKey="user-a" scope="OSAN:user-a" onOpenNotice={onOpenNotice} />);
  await act(async () => {});
  expect(screen.getByText('오산')).toBeInTheDocument();
  expect(screen.getByText('저장 제한 시간')).toBeInTheDocument();
  expect(screen.queryByText(active.body)).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '업데이트 내용 보기' }));
  expect(onOpenNotice).toHaveBeenCalledWith('notice-a');
  expect(screen.queryByRole('region', { name: '업데이트 안내' })).not.toBeInTheDocument();
});


it('완료 후 새 접속자에게 팝업을 자동 표시하지 않고 수동 조회는 저장 재개를 안내한다', async () => {
  vi.mocked(fetchJson).mockResolvedValue({ claimed: true });
  render(<MaintenanceAnnouncement status={{ ...complete, popupPending: true }} unavailable={false}
    userKey="user-new" scope="OSAN:user-new" onOpenNotice={vi.fn()} />);
  await act(async () => {});
  expect(fetchJson).not.toHaveBeenCalled();
  expect(screen.queryByRole('region', { name: '업데이트 안내' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '자세히 보기' }));
  expect(screen.getByText('저장 제한 해제')).toBeInTheDocument();
  expect(screen.queryByText(/시작 전에 저장/)).not.toBeInTheDocument();
});
