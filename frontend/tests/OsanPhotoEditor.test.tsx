import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, fetchJson } from '../src/api';
import { OsanPhotoEditor } from '../src/OsanPhotoEditor';
import { getOsanProgressPhoto, type OsanProgressTarget } from '../src/osanProgress';

vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
vi.mock('../src/osanProgress', async original => ({ ...await original<typeof import('../src/osanProgress')>(), getOsanProgressPhoto: vi.fn() }));
const target: OsanProgressTarget = {
  targetId: 'target-a', sequenceNumber: 1, displayName: '합성 대상', status: 'InProgress', version: 2,
  startedAtUtc: null, startedByUserId: null, startedByDisplayName: null,
  steps: [{ stepId: 'step-a', sequenceNumber: 1, stepCode: 'INCOMING', stepName: '입고검사', status: 'Completed',
    startedAtUtc: null, completedByUserId: 'worker', canCompleteIndividual: false, canCompleteBatch: false,
    guidanceDescription: null, guidancePhotos: [], completedAtUtc: '2026-09-10T00:00:00Z', completedByDisplayName: '합성 작업자', photos: [] }]
};
const path = '/api/osan/projects/project-a/progress/photo-edits';
function editRequest() {
  return { requestId: 'request-a', targetId: target.targetId, stepId: 'step-a', requestedBy: 'worker', requestedByName: '합성 작업자',
    requestedAt: '2026-09-10T00:00:00Z', approvedAt: null as string | null, approvedByName: null as string | null,
    usedAt: null as string | null, photoIds: ['previous-photo'] };
}
function state(items = [editRequest()], currentUserId = 'worker', canApprove = false) { return { items, currentUserId, canApprove }; }
function fileContent(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}
function show(userKey = 'worker', mutationAllowed = true) {
  const onSaved = vi.fn();
  return { ...render(<OsanPhotoEditor projectId="project-a" target={target} stage={1} userKey={userKey} mutationAllowed={mutationAllowed} onSaved={onSaved} />), onSaved };
}
beforeEach(() => {
  vi.resetAllMocks();
  URL.createObjectURL = vi.fn(() => 'blob:synthetic-photo');
  URL.revokeObjectURL = vi.fn();
  vi.mocked(getOsanProgressPhoto).mockResolvedValue(new Blob(['old photo'], { type: 'image/jpeg' }));
  vi.mocked(fetchJson).mockResolvedValue(state([]));
});

describe('오산 사진 수정 승인', () => {
  it('승인 권한이 없는 사용자와 다른 요청자에게 승인·편집 동작을 노출하지 않는다', async () => {
    vi.mocked(fetchJson).mockResolvedValue(state([editRequest()], 'other-worker'));
    const view = show('other-worker');
    await screen.findByText('합성 작업자 · 관리자 승인 대기');
    expect(screen.queryByRole('button', { name: '사진 수정 1회 승인' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '사진 수정' })).not.toBeInTheDocument();
    view.unmount();
    vi.mocked(fetchJson).mockResolvedValue(state([{ ...editRequest(), approvedAt: '2026-09-10T01:00:00Z' }], 'other-worker', true));
    show('other-worker');
    await screen.findByText(/수정 승인됨/);
    expect(screen.queryByRole('button', { name: '사진 수정' })).not.toBeInTheDocument();
  });

  it('요청→관리자 승인→요청자 1회 저장 후 재잠금과 이전 사진 이력을 연결한다', async () => {
    let items: ReturnType<typeof editRequest>[] = [];
    vi.mocked(fetchJson).mockImplementation(async (url, user, init) => {
      if (!init?.method) return state(items.map(item => ({ ...item })), user, user === 'admin');
      if (url === path) {
        const body = JSON.parse(init.body as string);
        expect(body).toMatchObject({ targetId: 'target-a', stageSequence: 1 });
        items = [{ ...editRequest(), requestId: body.requestId }];
      } else if (url.endsWith('/approve')) {
        expect(user).toBe('admin');
        items = [{ ...items[0], approvedAt: '2026-09-10T01:00:00Z', approvedByName: '합성 관리자' }];
      } else if (url.endsWith('/save')) {
        expect(user).toBe('worker');
        const body = init.body as FormData;
        expect(body.get('operationId')).toBe(items[0].requestId);
        expect(body.get('completionMode')).toBe('individual');
        expect(body.get('stageSequence')).toBe('1');
        expect(JSON.parse(body.get('targets') as string)).toEqual([{ targetId: 'target-a', expectedVersion: 2 }]);
        expect((body.get('photos') as File).name).toBe('replacement.jpg');
        items = [{ ...items[0], usedAt: '2026-09-10T02:00:00Z' }];
      } else throw new Error(`Unexpected request ${url}`);
      return {};
    });
    const requester = show();
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정 승인 요청' }));
    await screen.findByText('합성 작업자 · 관리자 승인 대기');
    expect(screen.queryByLabelText('사진 선택')).not.toBeInTheDocument();
    requester.unmount();
    const admin = show('admin');
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정 1회 승인' }));
    await screen.findByText(/수정 승인됨/);
    expect(screen.queryByRole('button', { name: '사진 수정' })).not.toBeInTheDocument();
    admin.unmount();
    const approvedRequester = show();
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정' }));
    fireEvent.change(screen.getByLabelText('사진 선택'), { target: { files: [new File(['jpeg'], 'replacement.jpg', { type: 'image/jpeg' })] } });
    fireEvent.click(screen.getByRole('button', { name: '사진 변경 저장' }));
    await waitFor(() => expect(approvedRequester.onSaved).toHaveBeenCalledOnce());
    expect(screen.queryByLabelText('사진 선택')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '사진 수정' })).not.toBeInTheDocument();
    await screen.findByRole('button', { name: '사진 수정 승인 요청' });
    fireEvent.click(screen.getByRole('button', { name: '사진 변경 이력' }));
    expect(await screen.findByRole('img', { name: '이전 수정 사진' })).toBeInTheDocument();
    expect(getOsanProgressPhoto).toHaveBeenCalledWith('project-a', 'previous-photo', 'worker', expect.any(AbortSignal));
    expect(vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method === 'POST')).toHaveLength(3);
  });

  it('읽기 전용으로 전환되면 이미 열린 사진 입력과 저장을 잠근다', async () => {
    vi.mocked(fetchJson).mockResolvedValue(state([{ ...editRequest(), approvedAt: '2026-09-10T01:00:00Z' }]));
    const view = show();
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정' }));
    view.rerender(<OsanPhotoEditor projectId="project-a" target={target} stage={1} userKey="worker" mutationAllowed={false} onSaved={view.onSaved} />);
    expect(screen.getByLabelText('사진 선택')).toBeDisabled();
    expect(screen.getByLabelText('카메라 촬영')).toBeDisabled();
    expect(screen.getByRole('button', { name: '사진 변경 저장' })).toBeDisabled();
    expect(vi.mocked(fetchJson).mock.calls.filter(([, , init]) => init?.method)).toHaveLength(0);
  });

  it('저장 중과 결과 불확실 시 파일·취소를 잠그고 같은 요청과 원본으로 재시도한다', async () => {
    const approved = state([{ ...editRequest(), approvedAt: '2026-09-10T01:00:00Z' }]);
    vi.mocked(fetchJson).mockResolvedValue(approved);
    const view = show();
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정' }));
    const file = new File(['jpeg'], 'retry.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('사진 선택'), { target: { files: [file] } });
    let reject!: (error: Error) => void;
    vi.mocked(fetchJson).mockImplementationOnce(() => new Promise((_, fail) => { reject = fail; }));
    fireEvent.click(screen.getByRole('button', { name: '사진 변경 저장' }));
    expect(screen.getByLabelText('사진 선택')).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '저장 중…' })).toBeDisabled();
    await act(async () => reject(new ApiError(503, '등록 결과 확인 실패')));
    const first = vi.mocked(fetchJson).mock.calls[1];
    expect(await screen.findByRole('button', { name: '같은 사진으로 저장 재시도' })).toBeEnabled();
    expect(screen.getByLabelText('사진 선택')).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    vi.mocked(fetchJson).mockRejectedValueOnce(new ApiError(403, '현재 권한으로 결과를 확인할 수 없습니다.'));
    fireEvent.click(screen.getByRole('button', { name: '같은 사진으로 저장 재시도' }));
    await screen.findByText('현재 권한으로 결과를 확인할 수 없습니다.');
    expect(screen.getByLabelText('사진 선택')).toBeDisabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '같은 사진으로 저장 재시도' }));
    await waitFor(() => expect(view.onSaved).toHaveBeenCalledOnce());
    const retried = vi.mocked(fetchJson).mock.calls[3];
    expect(retried[0]).toBe(first[0]);
    const originalBody = first[2]!.body as FormData, retriedBody = retried[2]!.body as FormData;
    expect(retriedBody.get('operationId')).toBe(originalBody.get('operationId'));
    expect(retriedBody.get('targets')).toBe(originalBody.get('targets'));
    expect((retriedBody.get('photos') as File).name).toBe('retry.jpg');
    expect(await fileContent(retriedBody.get('photos') as File)).toBe(await fileContent(originalBody.get('photos') as File));
  });

  it('잘못된 파일은 요청 전에 차단하고 서버 입력 거부는 재선택을 허용한다', async () => {
    vi.mocked(fetchJson).mockResolvedValue(state([{ ...editRequest(), approvedAt: '2026-09-10T01:00:00Z' }]));
    show();
    fireEvent.click(await screen.findByRole('button', { name: '사진 수정' }));
    fireEvent.change(screen.getByLabelText('사진 선택'), { target: { files: [new File(['heic'], 'camera.heic', { type: 'image/heic' })] } });
    expect(screen.getByRole('button', { name: '사진 변경 저장' })).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('HEIC는 지원하지 않습니다.');
    expect(fetchJson).toHaveBeenCalledTimes(1);
    fireEvent.change(screen.getByLabelText('사진 선택'), { target: { files: [new File(['jpeg'], 'camera.jpg', { type: 'image/jpeg' })] } });
    vi.mocked(fetchJson).mockRejectedValueOnce(new ApiError(422, '손상된 사진입니다.'));
    fireEvent.click(screen.getByRole('button', { name: '사진 변경 저장' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('손상된 사진입니다.');
    expect(screen.getByLabelText('사진 선택')).toBeEnabled();
    expect(screen.getByRole('button', { name: '취소' })).toBeEnabled();
  });
});
