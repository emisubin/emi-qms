import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProgressPage } from '../src/OsanProgressPage';
import * as api from '../src/osanProgress';
import { ApiError } from '../src/api';
vi.mock('../src/osanProgress', async importOriginal => ({ ...await importOriginal<typeof import('../src/osanProgress')>(), getOsanProgress: vi.fn(), startOsanProgress: vi.fn(), completeOsanProgress: vi.fn(), getOsanProgressPhoto: vi.fn() }));
function project(id = 'project-a'): api.OsanProgressDetail {
  return { projectId: id, title: id, projectCode: 'TEST', completedStepCount: 0, totalStepCount: 14, status: 'Active', targets: [1, 2].map(index => ({ targetId: `target-${index}`, sequenceNumber: index, displayName: `제품 ${index}`, status: 'InProgress', version: 1, canStart: false, startedAtUtc: null, startedByUserId: null, startedByDisplayName: null, steps: api.osanStageNames.map((stepName, i) => ({ stepId: `${index}-${i}`, stepCode: String(i), canCompleteIndividual: true, canCompleteBatch: true, guidanceDescription: null, guidancePhotos: [], startedAtUtc: null, completedByUserId: null, sequenceNumber: i + 1, stepName, status: 'NotStarted', completedAtUtc: null, completedByDisplayName: null, photos: [] })) })) };
}
const renderPage = (id = 'project-a') => render(<OsanProgressPage projectId={id} developmentUserKey="dev-user" mutationAllowed />);
async function openCompletion() { await screen.findByRole('button', { name: '완료' }); fireEvent.click(screen.getByRole('button', { name: '완료' })); }
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.getOsanProgress).mockResolvedValue(project());
  vi.mocked(api.completeOsanProgress).mockResolvedValue({ operationId: 'operation', replayed: false, project: project() });
  HTMLDialogElement.prototype.showModal = function() { this.open = true; };
  HTMLDialogElement.prototype.close = function() { this.open = false; };
  URL.createObjectURL = vi.fn(() => 'blob:preview');
  URL.revokeObjectURL = vi.fn();
});
afterEach(() => vi.restoreAllMocks());
describe('오산 진행 상세', () => {
  it('재조회에서 권한이 거부되면 이전 프로젝트와 사진을 화면에서 제거한다', async () => {
    renderPage();
    await screen.findByRole('heading', { name: 'project-a' });
    vi.mocked(api.getOsanProgress).mockRejectedValueOnce(new ApiError(403, '권한 없음'));
    fireEvent.click(screen.getByRole('button', { name: '새로고침' }));
    await screen.findByText('이 프로젝트의 진행 정보를 조회할 권한이 없습니다.');
    expect(screen.queryByRole('heading', { name: 'project-a' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '완료' })).not.toBeInTheDocument();
  });
  it('늦게 도착한 이전 프로젝트 조회를 새 프로젝트에 표시하지 않는다', async () => {
    let resolveOld!: (value: api.OsanProgressDetail) => void;
    vi.mocked(api.getOsanProgress).mockImplementation(id => id === 'old' ? new Promise(resolve => { resolveOld = resolve; }) : Promise.resolve(project(id)));
    const view = renderPage('old');
    view.rerender(<OsanProgressPage projectId="new" developmentUserKey="dev-user" mutationAllowed />);
    await screen.findByRole('heading', { name: 'new' });
    await act(async () => resolveOld(project('old')));
    expect(screen.queryByRole('heading', { name: 'old' })).not.toBeInTheDocument();
  });
  it('응답 유실 후 같은 operation ID와 원래 대상 version으로 재시도한다', async () => {
    vi.mocked(api.completeOsanProgress).mockRejectedValueOnce(new Error('연결 실패'));
    renderPage(); await openCompletion();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await screen.findByRole('alert');
    const first = vi.mocked(api.completeOsanProgress).mock.calls[0][1];
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await screen.findByText('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.');
    expect(vi.mocked(api.completeOsanProgress).mock.calls[1][1]).toBe(first);
  });
  it('실패 후 사진을 변경하면 새 operation ID를 사용한다', async () => {
    vi.mocked(api.completeOsanProgress).mockRejectedValueOnce(new Error('연결 실패'));
    renderPage(); await openCompletion();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await screen.findByRole('alert');
    const first = vi.mocked(api.completeOsanProgress).mock.calls[0][1];
    fireEvent.change(screen.getByLabelText('기존 사진 선택'), { target: { files: [new File(['png'], 'new.png', { type: 'image/png' })] } });
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await screen.findByText('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.');
    expect(vi.mocked(api.completeOsanProgress).mock.calls[1][1].operationId).not.toBe(first.operationId);
    expect(vi.mocked(api.completeOsanProgress).mock.calls[1][1].photos[0].name).toBe('new.png');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview');
  });
  it('프로젝트를 떠난 뒤 도착한 저장 응답을 새 프로젝트에 적용하지 않는다', async () => {
    let resolveSave!: (value: api.OsanProgressMutation) => void;
    vi.mocked(api.completeOsanProgress).mockReturnValue(new Promise(resolve => { resolveSave = resolve; }));
    const view = renderPage(); await openCompletion();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    vi.mocked(api.getOsanProgress).mockResolvedValue(project('new'));
    view.rerender(<OsanProgressPage projectId="new" developmentUserKey="dev-user" mutationAllowed />);
    await screen.findByRole('heading', { name: 'new' });
    await act(async () => resolveSave({ operationId: 'old', replayed: false, project: project('old') }));
    expect(screen.queryByRole('heading', { name: 'old' })).not.toBeInTheDocument();
    expect(screen.queryByText('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.')).not.toBeInTheDocument();
  });
  it('저장 중 중복 제출과 사진 교체를 막는다', async () => {
    vi.mocked(api.completeOsanProgress).mockReturnValue(new Promise(() => {}));
    renderPage(); await openCompletion();
    const submit = screen.getByRole('button', { name: '업로드하고 단계 완료' });
    fireEvent.click(submit); fireEvent.click(submit);
    expect(api.completeOsanProgress).toHaveBeenCalledTimes(1);
    expect(submit).toBeDisabled();
    expect(screen.getByLabelText('기존 사진 선택')).toBeDisabled();
    expect(screen.getByRole('button', { name: '닫기' })).toBeDisabled();
  });
  it('카메라 사진을 누적하고 6번째 촬영을 거부한 뒤 앨범 재선택으로 복구한다', async () => {
    renderPage(); await openCompletion();
    const camera = screen.getByLabelText('카메라 사진 선택');
    for (let index = 1; index <= 5; index++) {
      fireEvent.change(camera, { target: { files: [new File(['png'], `capture-${index}.png`, { type: 'image/png' })] } });
    }
    expect(screen.getAllByRole('img', { name: /미리보기/ })).toHaveLength(5);
    fireEvent.change(camera, { target: { files: [new File(['png'], 'capture-6.png', { type: 'image/png' })] } });
    expect(screen.getByRole('alert')).toHaveTextContent('최대 5장');
    expect(screen.getByRole('button', { name: '업로드하고 단계 완료' })).toBeDisabled();
    expect(screen.getAllByRole('img', { name: /미리보기/ })).toHaveLength(5);
    fireEvent.change(screen.getByLabelText('기존 사진 선택'), { target: { files: [new File(['png'], 'album.png', { type: 'image/png' })] } });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getAllByRole('img', { name: /미리보기/ })).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await screen.findByText('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.');
    expect(vi.mocked(api.completeOsanProgress).mock.calls[0][1].photos.map(file => file.name)).toEqual(['album.png']);
  });
  it('잘못된 형식은 사진을 변경하지 않고 재선택을 안내한다', async () => {
    renderPage(); await openCompletion();
    fireEvent.change(screen.getByLabelText('기존 사진 선택'), { target: { files: [new File(['x'], 'photo.heic', { type: 'image/heic' })] } });
    expect(screen.getByRole('alert')).toHaveTextContent('HEIC');
    expect(api.completeOsanProgress).not.toHaveBeenCalled();
  });
  it('복수 선택의 완료 기록을 대상별로 표시하고 사진을 권한 API로 읽는다', async () => {
    const result = project();
    result.targets[0].steps[0] = { ...result.targets[0].steps[0], status: 'Completed', completedByDisplayName: '작업자 A', completedAtUtc: '2026-09-09T00:00:00Z', photos: [{ photoId: 'photo-1', fileName: 'evidence.png', contentType: 'image/png', sizeBytes: 1, displayOrder: 1, sha256: 'hash', uploadedAtUtc: '2026-09-09T00:00:00Z', uploadedByUserId: 'user', uploadedByDisplayName: '작업자 A' }] };
    vi.mocked(api.getOsanProgress).mockResolvedValue(result);
    vi.mocked(api.getOsanProgressPhoto).mockResolvedValue(new Blob(['photo'], { type: 'image/png' }));
    renderPage(); fireEvent.click(await screen.findByRole('button', { name: /제품 1/ }));
    fireEvent.click(screen.getByLabelText('전체 선택')); fireEvent.click(screen.getByRole('button', { name: '선택 완료' }));
    expect(within(screen.getByRole('region', { name: '제품 1 완료 기록' })).getByText('작업자 A')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: '제품 2 완료 기록' })).getByText('미완료')).toBeInTheDocument();
    await waitFor(() => expect(api.getOsanProgressPhoto).toHaveBeenCalledWith('project-a', 'photo-1', 'dev-user', expect.any(AbortSignal)));
  });
  it('일괄 선택 때 모든 원본을 중복 다운로드하지 않고 한 대상의 사진만 펼친다', async () => {
    const result = project();
    const photo = { photoId: 'shared-photo', fileName: 'evidence.png', contentType: 'image/png', sizeBytes: 1, displayOrder: 1, sha256: 'hash', uploadedAtUtc: '2026-09-09T00:00:00Z', uploadedByUserId: 'user', uploadedByDisplayName: '작업자' };
    result.targets.forEach(target => {
      target.steps[0] = { ...target.steps[0], status: 'Completed', photos: [photo] };
    });
    vi.mocked(api.getOsanProgress).mockResolvedValue(result);
    vi.mocked(api.getOsanProgressPhoto).mockResolvedValue(new Blob(['photo'], { type: 'image/png' }));
    renderPage();
    await screen.findByRole('img', { name: 'evidence.png' });
    expect(api.getOsanProgressPhoto).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: /^제품 1$/ }));
    fireEvent.click(screen.getByLabelText('전체 선택'));
    fireEvent.click(screen.getByRole('button', { name: '선택 완료' }));
    expect(screen.queryAllByRole('img', { name: 'evidence.png' })).toHaveLength(0);
    expect(api.getOsanProgressPhoto).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: '제품 2 사진 보기 (1장)' }));
    await screen.findByRole('img', { name: 'evidence.png' });
    expect(api.getOsanProgressPhoto).toHaveBeenCalledTimes(2);
    fireEvent.click(screen.getByRole('button', { name: '제품 1 사진 보기 (1장)' }));
    await screen.findByRole('img', { name: 'evidence.png' });
    expect(screen.getAllByRole('img', { name: 'evidence.png' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: '제품 2 사진 보기 (1장)' })).toHaveAttribute('aria-expanded', 'false');
    expect(URL.revokeObjectURL).toHaveBeenCalled();
  });
});
