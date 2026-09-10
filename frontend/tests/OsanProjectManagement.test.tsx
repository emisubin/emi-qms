import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProjectManagement } from '../src/OsanProjectManagement';
import { ApiError, fetchJson } from '../src/api';
import type { OsanProjectDetail } from '../src/projects';

vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
const project: OsanProjectDetail = {
  editToken: 'project-snapshot-token',
  projectId: 'project-a', title: '합성 장비', projectCode: '001 CODE', productName: '분류 A', quantity: 1,
  customerName: '합성 거래처', poNumber: '001-PO', workOrderNumber: null, deliveryDate: '2026-12-31',
  status: 'NotStarted', completedStepCount: 0, totalStepCount: 7, createdAtUtc: '2026-09-10T00:00:00Z',
  targets: [{ targetId: 'target-a', sequenceNumber: 1, displayName: '분류 A 1', status: 'NotStarted',
    steps: [{ stepId: 'step-a', sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', status: 'NotStarted' }] }]
};
const path = '/api/osan/projects/project-a';
beforeEach(() => { vi.resetAllMocks(); vi.mocked(fetchJson).mockResolvedValue({ canManage: true, editToken: 'current-token' }); });
function show(value = project) {
  const onSaved = vi.fn(), onDeleted = vi.fn();
  return { ...render(<OsanProjectManagement project={value} userKey="admin" mutationAllowed onSaved={onSaved} onDeleted={onDeleted} />), onSaved, onDeleted };
}
describe('오산 프로젝트 관리', () => {
  it('서버가 관리 권한을 허용하지 않으면 수정과 삭제를 표시하지 않는다', async () => {
    vi.mocked(fetchJson).mockResolvedValue({ canManage: false, editToken: '' });
    const view = show();
    await act(async () => {});
    expect(view.container).toBeEmptyDOMElement();
    expect(fetchJson).toHaveBeenCalledWith(`${path}/management`, 'admin', { signal: expect.any(AbortSignal) });
  });
  it('서버 권한 거부 시 오류를 알리고 관리 동작을 노출하지 않는다', async () => {
    vi.mocked(fetchJson).mockRejectedValue(new ApiError(403, '관리 권한이 없습니다.'));
    show();
    expect(await screen.findByRole('alert')).toHaveTextContent('관리 권한이 없습니다.');
    expect(screen.queryByRole('button', { name: '프로젝트 정보 수정' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '프로젝트 삭제' })).not.toBeInTheDocument();
  });
  it('읽기 전용으로 전환하면 열려 있던 관리 동작을 숨긴다', async () => {
    const view = show();
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
    view.rerender(<OsanProjectManagement project={project} userKey="admin" mutationAllowed={false} onSaved={view.onSaved} onDeleted={view.onDeleted} />);
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '프로젝트 삭제' })).not.toBeInTheDocument();
    expect(vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method)).toHaveLength(0);
  });
  it('프로젝트를 전환한 뒤 이전 관리권한 응답이 도착해도 새 프로젝트에 적용하지 않는다', async () => {
    let resolve!: (value: unknown) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    const view = show();
    const firstSignal = vi.mocked(fetchJson).mock.calls[0][2]!.signal;
    vi.mocked(fetchJson).mockResolvedValueOnce({ canManage: false, editToken: '' });
    view.rerender(<OsanProjectManagement project={{ ...project, projectId: 'project-b' }} userKey="admin" mutationAllowed onSaved={view.onSaved} onDeleted={view.onDeleted} />);
    await act(async () => {});
    await act(async () => resolve({ canManage: true, editToken: 'old-token' }));
    expect(firstSignal?.aborted).toBe(true);
    expect(view.container).toBeEmptyDOMElement();
  });
  it('승인된 필드 순서로 수정하고 저장 중 입력과 중복 제출을 잠근다', async () => {
    const { onSaved, onDeleted } = show();
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
    expect(Array.from(document.querySelectorAll('input')).map(input => input.getAttribute('aria-label')))
      .toEqual(['장비명 수정', '프로젝트 코드 수정', 'part분류 수정', '수량 수정', '거래처 수정', 'PO No 수정', 'W/O No 수정', '납기일 수정']);
    expect(screen.getByLabelText('프로젝트 코드 수정')).toHaveValue('001 CODE');
    fireEvent.change(screen.getByLabelText('장비명 수정'), { target: { value: '수정 장비' } });
    fireEvent.change(screen.getByLabelText('수량 수정'), { target: { value: '2' } });
    let resolve!: (value: unknown) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    fireEvent.submit(screen.getByRole('form', { name: '프로젝트 정보 수정' }));
    expect(fetchJson).toHaveBeenCalledTimes(2);
    expect(screen.getByLabelText('장비명 수정')).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    const [url, user, init] = vi.mocked(fetchJson).mock.calls[1];
    expect([url, user, init?.method]).toEqual([path, 'admin', 'PUT']);
    expect(JSON.parse(init!.body as string)).toMatchObject({ expectedToken: 'project-snapshot-token', fields: {
      title: '수정 장비', projectCode: '001 CODE', productName: '분류 A', quantity: 2,
      customerName: '합성 거래처', poNumber: '001-PO', workOrderNumber: '', deliveryDate: '2026-12-31'
    } });
    await act(async () => resolve({}));
    expect(onSaved).toHaveBeenCalledOnce();
    expect(onDeleted).not.toHaveBeenCalled();
  });
  it('이미 완료된 단계가 있으면 수량을 고정하고 다른 정보는 수정할 수 있다', async () => {
    show({ ...project, targets: [{ ...project.targets[0], steps: [{ ...project.targets[0].steps[0], status: 'Completed' }] }] });
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
    expect(screen.getByLabelText('수량 수정')).toBeDisabled();
    expect(screen.getByLabelText('장비명 수정')).toBeEnabled();
    expect(screen.getByText('진행이 시작된 프로젝트의 수량은 변경할 수 없습니다.')).toBeInTheDocument();
  });
  it('삭제 사유 입력과 확인을 거쳐 삭제하며 처리 중 사유와 취소를 잠근다', async () => {
    const { onDeleted, onSaved } = show();
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 삭제' }));
    const reason = screen.getByLabelText('삭제 사유');
    expect(reason).toBeRequired();
    fireEvent.click(screen.getByRole('button', { name: '삭제 확인' }));
    expect(fetchJson).toHaveBeenCalledTimes(1);
    fireEvent.change(reason, { target: { value: '중복 등록 정리' } });
    let resolve!: (value: unknown) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    fireEvent.click(screen.getByRole('button', { name: '삭제 확인' }));
    expect(reason).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    const call = vi.mocked(fetchJson).mock.calls[1];
    expect(call[2]?.method).toBe('DELETE');
    expect(JSON.parse(call[2]!.body as string)).toEqual({ expectedToken: 'project-snapshot-token', reason: '중복 등록 정리' });
    await act(async () => resolve({}));
    expect(onDeleted).toHaveBeenCalledOnce();
    expect(onSaved).not.toHaveBeenCalled();
  });
  it('저장이 실패하면 수정값을 보존하고 실패 안내 후 재시도할 수 있다', async () => {
    const { onSaved } = show();
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
    fireEvent.change(screen.getByLabelText('장비명 수정'), { target: { value: '실패 후 보존' } });
    vi.mocked(fetchJson).mockRejectedValueOnce(new ApiError(503, '잠시 후 다시 시도해 주세요.'));
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('잠시 후 다시 시도해 주세요.');
    expect(screen.getByLabelText('장비명 수정')).toHaveValue('실패 후 보존');
    expect(screen.getByLabelText('장비명 수정')).toBeEnabled();
    expect(onSaved).not.toHaveBeenCalled();
    vi.mocked(fetchJson).mockResolvedValueOnce({});
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
  });
  it('상세 응답의 수정 토큰이 없으면 나중에 조회한 관리 토큰으로 저장하지 않는다', async () => {
    show({ ...project, editToken: undefined });
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('프로젝트를 새로고침한 후 다시 수정해 주세요.');
    expect(vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method)).toHaveLength(0);
  });
});
