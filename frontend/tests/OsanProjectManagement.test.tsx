import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProjectManagement } from '../src/OsanProjectManagement';
import { ApiError, fetchJson } from '../src/api';
import type { OsanProjectDetail } from '../src/projects';

vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
const project: OsanProjectDetail = {
  editToken: 'project-snapshot-token',
  projectId: 'project-a', title: '합성 장비', projectCode: '001 CODE', productName: '분류 A', quantity: 1,
  customerName: '합성 고객사', customerId: 'customer-a', poNumber: '001-PO', workOrderNumber: null, deliveryDate: '2026-12-31',
  status: 'NotStarted', completedStepCount: 0, totalStepCount: 7, createdAtUtc: '2026-09-10T00:00:00Z',
  targets: [{ targetId: 'target-a', sequenceNumber: 1, displayName: '분류 A 1', status: 'NotStarted',
    steps: [{ stepId: 'step-a', sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', status: 'NotStarted' }] }]
};
const path = '/api/osan/projects/project-a';
const customers = { items: [{ customerId: 'customer-a', name: '합성 고객사' }, { customerId: 'customer-b', name: '합성 고객사 연구소' }] };
const writes = () => vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(fetchJson).mockImplementation(async url => url === '/api/osan/customers'
    ? customers : { canManage: true, editToken: 'current-token' });
});
async function openEdit() {
  fireEvent.click(await screen.findByRole('button', { name: '프로젝트 정보 수정' }));
  await screen.findByText('연결된 고객사: 합성 고객사');
}
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
    await openEdit();
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
    await openEdit();
    expect(Array.from(document.querySelectorAll('.osan-management-fields input')).map(input => input.getAttribute('aria-label')))
      .toEqual(['장비명 수정', '프로젝트 코드 수정', 'part 분류 수정', '고객사', 'PO No 수정', 'W/O No 수정', '납기일 수정']);
    expect(screen.queryByLabelText('수량 수정')).not.toBeInTheDocument();
    expect(screen.getByLabelText('프로젝트 코드 수정')).toHaveValue('001 CODE');
    fireEvent.change(screen.getByLabelText('장비명 수정'), { target: { value: '수정 장비' } });
    let resolve!: (value: unknown) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    fireEvent.submit(screen.getByRole('form', { name: '프로젝트 정보 수정' }));
    expect(writes()).toHaveLength(1);
    expect(screen.getByLabelText('장비명 수정')).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    const [url, user, init] = writes()[0];
    expect([url, user, init?.method]).toEqual([path, 'admin', 'PUT']);
    expect(JSON.parse(init!.body as string)).toMatchObject({ expectedToken: 'project-snapshot-token', fields: {
      title: '수정 장비', projectCode: '001 CODE', productName: '분류 A',
      customerName: '합성 고객사', customerId: 'customer-a', poNumber: '001-PO', workOrderNumber: '', deliveryDate: '2026-12-31'
    } });
    await act(async () => resolve({}));
    expect(onSaved).toHaveBeenCalledOnce();
    expect(onDeleted).not.toHaveBeenCalled();
  });
  it.each([false, true])('납기 HOLD 상태 %s를 전환할 때 사유를 받아 기존 납기일과 함께 저장한다', async held => {
    const { onSaved } = show({ ...project, deliveryHold: held });
    await openEdit();
    const checkbox = screen.getByRole('checkbox', { name: '납기 HOLD' });
    expect(checkbox).toHaveProperty('checked', held);
    expect(screen.queryByLabelText('납기 HOLD 변경 사유')).not.toBeInTheDocument();
    fireEvent.click(checkbox);
    const reason = screen.getByLabelText('납기 HOLD 변경 사유');
    expect(reason).toBeRequired();
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    expect(writes()).toHaveLength(0);
    fireEvent.change(reason, { target: { value: held ? '납기 재개' : '고객 요청 보류' } });
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    const payload = JSON.parse(writes()[0][2]!.body as string);
    expect(payload).toMatchObject({ deliveryHold: !held, holdReason: held ? '납기 재개' : '고객 요청 보류',
      expectedToken: project.editToken, fields: { deliveryDate: project.deliveryDate } });
  });
  it('HOLD 상태를 유지하며 다른 정보를 수정할 때 변경 사유를 요구하지 않는다', async () => {
    const { onSaved } = show({ ...project, deliveryHold: true });
    await openEdit();
    fireEvent.change(screen.getByLabelText('장비명 수정'), { target: { value: '장비명 보정' } });
    expect(screen.queryByLabelText('납기 HOLD 변경 사유')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(JSON.parse(writes()[0][2]!.body as string)).toMatchObject({
      deliveryHold: true, fields: { title: '장비명 보정', deliveryDate: project.deliveryDate } });
  });
  it('기존 다중 대상 프로젝트도 수량 입력 없이 다른 정보를 수정할 수 있다', async () => {
    show({ ...project, quantity: 2, targets: [
      project.targets[0],
      { ...project.targets[0], targetId: 'target-b', sequenceNumber: 2, displayName: '분류 A 2' }
    ] });
    await openEdit();
    expect(screen.queryByLabelText('수량 수정')).not.toBeInTheDocument();
    expect(screen.getByLabelText('장비명 수정')).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    await waitFor(() => expect(writes()).toHaveLength(1));
    const payload = JSON.parse(writes()[0][2]!.body as string);
    expect(payload.fields).not.toHaveProperty('quantity');
  });
  it('선택한 등록 고객사의 ID를 프로젝트 수정 요청에 연결한다', async () => {
    const { onSaved } = show();
    await openEdit();
    fireEvent.click(document.querySelector('.customer-picker-button')!);
    fireEvent.click(screen.getByRole('button', { name: '합성 고객사 연구소' }));
    expect(screen.getByLabelText('고객사')).toHaveValue('합성 고객사 연구소');
    expect(screen.getByText('연결된 고객사: 합성 고객사 연구소')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    expect(JSON.parse(writes()[0][2]!.body as string).fields).toMatchObject({
      customerName: '합성 고객사 연구소', customerId: 'customer-b'
    });
  });
  it('삭제 사유 입력과 확인을 거쳐 삭제하며 처리 중 사유와 취소를 잠근다', async () => {
    const { onDeleted, onSaved } = show();
    fireEvent.click(await screen.findByRole('button', { name: '프로젝트 삭제' }));
    const reason = screen.getByLabelText('삭제 사유');
    expect(reason).toBeRequired();
    fireEvent.click(screen.getByRole('button', { name: '삭제 확인' }));
    expect(writes()).toHaveLength(0);
    fireEvent.change(reason, { target: { value: '중복 등록 정리' } });
    let resolve!: (value: unknown) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise(done => { resolve = done; }));
    fireEvent.click(screen.getByRole('button', { name: '삭제 확인' }));
    expect(reason).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    const call = writes()[0];
    expect(call[2]?.method).toBe('DELETE');
    expect(JSON.parse(call[2]!.body as string)).toEqual({ expectedToken: 'project-snapshot-token', reason: '중복 등록 정리' });
    await act(async () => resolve({}));
    expect(onDeleted).toHaveBeenCalledOnce();
    expect(onSaved).not.toHaveBeenCalled();
  });
  it('저장이 실패하면 수정값을 보존하고 실패 안내 후 재시도할 수 있다', async () => {
    const { onSaved } = show();
    await openEdit();
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
    await openEdit();
    fireEvent.click(screen.getByRole('button', { name: '변경 저장' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('프로젝트를 새로고침한 후 다시 수정해 주세요.');
    expect(vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method)).toHaveLength(0);
  });
});
