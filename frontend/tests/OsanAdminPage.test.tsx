import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { OsanCustomerAdminPage, OsanGateSettingsPage } from '../src/OsanAdminPage';
import { OsanGateApprovalsPage } from '../src/OsanGateApprovalsPage';
import * as api from '../src/api';

vi.mock('../src/api', () => ({ fetchJson: vi.fn() }));
const customer = { customerId: 'c1', name: '한빛전자', version: 1 };
const user = { userId: 'u1', displayName: '김담당', departmentName: '제조', version: 2, customerIds: [] as string[] };
const gates = Array.from({ length: 7 }, (_, i) => ({ stageSequence: i + 1, name: `Gate ${i + 1}`, departmentIds: [] as string[] }));

beforeEach(() => {
  vi.clearAllMocks();
  HTMLDialogElement.prototype.showModal = function () { this.open = true; };
  HTMLDialogElement.prototype.close = function () { this.open = false; };
  vi.mocked(api.fetchJson).mockImplementation(async path => {
    if (path === '/api/osan/admin/customer-assignments') return { customers: [customer], users: [user] };
    if (path === '/api/osan/admin/gates') return { version: 3, departments: [{ departmentId: 'd1', name: '제조' }], gates };
    if (path === '/api/osan/gate-approvals') return { items: [{
      projectId: 'p1', projectCode: 'OS-1', projectTitle: '프로젝트', requestId: 'r1', targetId: 't1',
      stageSequence: 1, requestedByName: '김담당', requestedAt: '2026-09-24T00:00:00Z'
    }] };
    return {};
  });
});

it('신규 고객사를 등록하며 담당자가 없는 상태를 안내한다', async () => {
  render(<OsanCustomerAdminPage developmentUserKey="admin" />);
  await screen.findByRole('button', { name: '한빛전자 더보기' });
  fireEvent.click(screen.getByRole('button', { name: '고객사 등록' }));
  fireEvent.change(screen.getByPlaceholderText('정식 고객사명을 입력해 주세요'), { target: { value: '새 고객사' } });
  fireEvent.click(screen.getByRole('button', { name: '등록' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith('/api/osan/admin/customers', 'admin',
    expect.objectContaining({ method: 'POST', body: JSON.stringify({ name: '새 고객사' }) })));
  expect(await screen.findByText('새 고객사 등록 · 담당자 미배정입니다.')).toBeInTheDocument();
});

it('사용자별 고객사 선택은 버전과 전체 선택 목록을 저장한다', async () => {
  render(<OsanCustomerAdminPage developmentUserKey="admin" />);
  await screen.findByRole('button', { name: '한빛전자 더보기' });
  fireEvent.click(screen.getByRole('tab', { name: '사용자별 고객사' }));
  fireEvent.click(screen.getByRole('button', { name: '김담당 더보기' }));
  fireEvent.click(screen.getByRole('button', { name: '고객사 설정' }));
  fireEvent.click(screen.getByRole('checkbox', { name: '한빛전자' }));
  fireEvent.click(screen.getByRole('button', { name: '저장' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith('/api/osan/admin/customer-assignments/u1', 'admin',
    expect.objectContaining({ method: 'PUT', body: JSON.stringify({ customerIds: ['c1'], expectedVersion: 2 }) })));
});

it('Gate 표는 부서 세로·Gate 가로로 편집하고 버전과 일괄 저장한다', async () => {
  render(<OsanGateSettingsPage developmentUserKey="admin" />);
  await screen.findByRole('table', { name: '부서별 Gate 완료 가능 여부' });
  expect(screen.getByRole('rowheader', { name: '제조' })).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '설정 변경' }));
  fireEvent.click(screen.getByRole('checkbox', { name: '제조 · Gate 1 완료 허용' }));
  fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith('/api/osan/admin/gates', 'admin',
    expect.objectContaining({ method: 'PUT', body: expect.stringContaining('"departmentIds":["d1"]') })));
});

it('승인 대기는 바로 승인하지 않고 요청된 프로젝트·대상·단계로 이동한다', async () => {
  const open = vi.fn();
  render(<OsanGateApprovalsPage developmentUserKey="admin" onOpenStage={open} />);
  fireEvent.click(await screen.findByRole('button', { name: '단계 확인' }));
  expect(open).toHaveBeenCalledWith('p1', 't1', 1);
  expect(api.fetchJson).not.toHaveBeenCalledWith(expect.stringContaining('/approve'), expect.anything(), expect.anything());
});


it('고객사 작업은 더보기 안에 모이고 삭제는 보존 안내 후 버전으로 요청한다', async () => {
  render(<OsanCustomerAdminPage developmentUserKey="admin" />);
  const more = await screen.findByRole('button', { name: '한빛전자 더보기' });
  expect(screen.queryByRole('button', { name: '이름 변경' })).not.toBeInTheDocument();
  fireEvent.click(more);
  expect(screen.getByRole('button', { name: '이름 변경' })).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '고객사 삭제' }));
  expect(screen.getByText(/기존 프로젝트와 이력의 고객사 정보는 그대로/)).toBeInTheDocument();
  expect(api.fetchJson).not.toHaveBeenCalledWith(expect.stringContaining('expectedVersion='), expect.anything(), expect.anything());
  fireEvent.click(screen.getByRole('button', { name: '삭제' }));
  await waitFor(() => expect(api.fetchJson).toHaveBeenCalledWith('/api/osan/admin/customers/c1?expectedVersion=1', 'admin', { method: 'DELETE' }));
});

it('삭제 실패는 팝업에 남아 재시도할 수 있고 취소는 삭제하지 않는다', async () => {
  vi.mocked(api.fetchJson).mockImplementation(async (_path, _key, options) => {
    if (options?.method === 'DELETE') throw new Error('다른 사용자가 변경했습니다.');
    return { customers: [customer], users: [user] };
  });
  render(<OsanCustomerAdminPage developmentUserKey="admin" />);
  fireEvent.click(await screen.findByRole('button', { name: '한빛전자 더보기' }));
  fireEvent.click(screen.getByRole('button', { name: '고객사 삭제' }));
  fireEvent.click(screen.getByRole('button', { name: '삭제' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('다른 사용자가 변경했습니다.');
  expect(screen.getByRole('button', { name: '삭제' })).toBeEnabled();
  fireEvent.click(screen.getByRole('button', { name: '취소' }));
  expect(screen.queryByRole('button', { name: '삭제' })).not.toBeInTheDocument();
});
