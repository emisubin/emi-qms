import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OsanProgressPage } from '../src/OsanProgressPage';
import * as api from '../src/osanProgress';
import { ApiError, fetchJson } from '../src/api';
vi.mock('../src/api', async original => ({ ...await original<typeof import('../src/api')>(), fetchJson: vi.fn() }));
vi.mock('../src/osanProgress', async importOriginal => ({ ...await importOriginal<typeof import('../src/osanProgress')>(), getOsanProgress: vi.fn(), completeOsanProgress: vi.fn(), getOsanProgressPhoto: vi.fn() }));
function project(id = 'project-a'): api.OsanProgressDetail {
  return { projectId: id, title: id, projectCode: 'TEST', completedStepCount: 0, totalStepCount: 14, status: 'Active', targets: [1, 2].map(index => ({ targetId: `target-${index}`, sequenceNumber: index, displayName: `제품 ${index}`, status: 'InProgress', version: 1, startedAtUtc: null, startedByUserId: null, startedByDisplayName: null, steps: api.osanStageNames.map((stepName, i) => ({ stepId: `${index}-${i}`, stepCode: String(i), canCompleteIndividual: true, canCompleteBatch: true, guidanceDescription: null, guidancePhotos: [], startedAtUtc: null, completedByUserId: null, sequenceNumber: i + 1, stepName, status: 'NotStarted', completedAtUtc: null, completedByDisplayName: null, photos: [] })) })) };
}
const renderPage = (id = 'project-a') => render(<OsanProgressPage projectId={id} developmentUserKey="dev-user" mutationAllowed />);
async function openCompletion() { await screen.findByRole('button', { name: '완료' }); fireEvent.click(screen.getByRole('button', { name: '완료' })); }
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(fetchJson).mockResolvedValue({ canApprove: false, currentUserId: 'dev-user', items: [] });
  vi.mocked(api.getOsanProgress).mockResolvedValue(project());
  vi.mocked(api.completeOsanProgress).mockResolvedValue({ operationId: 'operation', replayed: false, project: project() });
  HTMLDialogElement.prototype.showModal = function() { this.open = true; };
  HTMLDialogElement.prototype.close = function() { this.open = false; };
  URL.createObjectURL = vi.fn(() => 'blob:preview');
  URL.revokeObjectURL = vi.fn();
});
afterEach(() => vi.restoreAllMocks());
describe('오산 진행 상세', () => {
  it('앞 단계를 완료한 대상은 별도 시작 없이 다음 단계 완료를 저장한다', async () => {
    const data = project(); data.targets.forEach(target => { target.steps[0].status = 'Completed'; target.steps[1].status = 'Completed'; });
    vi.mocked(api.getOsanProgress).mockResolvedValue(data);
    renderPage(); await screen.findByRole('button', { name: '다음 단계' });
    expect(screen.queryByRole('button', { name: '작업 시작' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다음 단계' }));
    fireEvent.click(screen.getByRole('button', { name: '다음 단계' }));
    await openCompletion();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await waitFor(() => expect(api.completeOsanProgress).toHaveBeenCalled());
    expect(vi.mocked(api.completeOsanProgress).mock.calls[0][1]).toMatchObject({ stageSequence: 3, completionMode: 'individual' });
  });
  it('앞 단계가 미완료면 이후 단계 완료 화면을 열지 않는다', async () => {
    renderPage(); await screen.findByRole('button', { name: '완료' });
    fireEvent.click(screen.getByRole('button', { name: '다음 단계' }));
    expect(screen.getByRole('button', { name: '완료' })).toBeDisabled();
    expect(screen.getByText(/이전 단계를 모두 완료한 후/)).toBeInTheDocument();
    expect(api.completeOsanProgress).not.toHaveBeenCalled();
  });
  it('대상 링크로 진입한 두 번째 대상만 선택하고 저장한다', async () => {
    render(<OsanProgressPage projectId="project-a" initialTargetId="target-2" developmentUserKey="dev-user" mutationAllowed />);
    await screen.findByRole('button', { name: '제품 2' });
    await openCompletion();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await waitFor(() => expect(api.completeOsanProgress).toHaveBeenCalled());
    expect(vi.mocked(api.completeOsanProgress).mock.calls[0][1].targets).toEqual([{ targetId: 'target-2', expectedVersion: 1 }]);
  });
  it('촬영 버튼은 카메라 전용 입력을 직접 열고 촬영 원본을 재선택 없이 저장한다', async () => {
    renderPage(); await openCompletion();
    const camera = screen.getByLabelText('카메라 사진 선택');
    expect(camera).toHaveAttribute('accept', 'image/*');
    expect(camera).toHaveAttribute('capture', 'environment');
    const clicked = vi.spyOn(camera, 'click');
    fireEvent.click(screen.getByRole('button', { name: '촬영' }));
    expect(clicked).toHaveBeenCalledOnce();
    const original = new File(['jpeg'], 'camera.jpg', { type: 'image/jpeg' });
    fireEvent.change(camera, { target: { files: [original] } });
    expect(await screen.findByRole('img', { name: 'camera.jpg 미리보기' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '업로드하고 단계 완료' }));
    await waitFor(() => expect(api.completeOsanProgress).toHaveBeenCalled());
    expect(vi.mocked(api.completeOsanProgress).mock.calls[0][1].photos[0]).toBe(original);
  });
  it('선택 행의 이름을 누를 때 발생하는 일시적 focus 해제에도 선택을 적용한다', async () => {
    renderPage();
    const trigger = await screen.findByRole('button', { name: '제품 1' });
    fireEvent.click(trigger);
    fireEvent.blur(trigger, { relatedTarget: null });
    fireEvent.click(screen.getByText('제품 2', { exact: true }));
    expect(screen.getByLabelText('제품 2')).toBeChecked();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.getByRole('button', { name: '2개 대상 선택' })).toHaveAttribute('aria-expanded', 'false');
  });
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
    await waitFor(() => expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview'));
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
    fireEvent.click(screen.getByLabelText('전체 선택')); fireEvent.click(screen.getByRole('button', { name: '패널 선택' }));
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
    fireEvent.click(screen.getByRole('button', { name: '패널 선택' }));
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

describe('PC 단계 상세 팝업', () => {
  beforeEach(() => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
  });
  afterEach(() => vi.unstubAllGlobals());
  it('7단계를 표시하고 완료된 단계에서도 설명과 완료 기록을 팝업에 표시한다', async () => {
    const data = project();
    data.targets[0].steps[0].status = 'Completed';
    data.targets[0].steps[0].completedByDisplayName = '검수 작업자';
    data.targets[0].steps[0].completedAtUtc = '2026-09-10T10:00:00Z';
    vi.mocked(api.getOsanProgress).mockResolvedValue(data);
    renderPage();
    const overview = await screen.findByRole('navigation', { name: '전체 진행 단계' });
    expect(within(overview).getAllByRole('button')).toHaveLength(7);
    expect(screen.queryByRole('button', { name: '다음 단계' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: '단계 설명' })).not.toBeInTheDocument();
    fireEvent.click(within(overview).getByRole('button', { name: /입고검사/ }));
    const dialog = await screen.findByRole('dialog', { name: '입고검사' });
    expect(within(dialog).getByRole('region', { name: '단계 설명' })).toHaveTextContent('60~150㎛');
    expect(within(dialog).getByText('검수 작업자')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: '완료' })).not.toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: '단계 상세 닫기' }));
    expect(screen.queryByRole('dialog', { name: '입고검사' })).not.toBeInTheDocument();
  });
  it('선행 단계 제한을 유지하고 완료 팝업을 닫으면 단계 상세를 유지한다', async () => {
    const data = project();
    data.targets[0].steps[1].canCompleteIndividual = false;
    vi.mocked(api.getOsanProgress).mockResolvedValue(data);
    renderPage();
    const overview = await screen.findByRole('navigation', { name: '전체 진행 단계' });
    fireEvent.click(within(overview).getByRole('button', { name: /배치검사/ }));
    expect(within(screen.getByRole('dialog', { name: '배치검사' })).getByRole('button', { name: '완료' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '단계 상세 닫기' }));
    fireEvent.click(within(overview).getByRole('button', { name: /입고검사/ }));
    fireEvent.click(within(screen.getByRole('dialog', { name: '입고검사' })).getByRole('button', { name: '완료' }));
    const completion = screen.getByRole('dialog', { name: '해당 진행 단계를 완료하셨나요?' });
    expect(completion).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '단계 상세 닫기' })).toBeDisabled();
    fireEvent(completion, new Event('cancel', { bubbles: false, cancelable: true }));
    expect(screen.getByRole('dialog', { name: '입고검사' })).toBeInTheDocument();
    expect(api.completeOsanProgress).not.toHaveBeenCalled();
  });
});
