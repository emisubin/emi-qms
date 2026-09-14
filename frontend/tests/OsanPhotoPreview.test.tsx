import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { OsanPhotoPreview } from '../src/OsanPhotoPreview';
import * as api from '../src/osanProgress';
vi.mock('../src/osanProgress', async original => ({ ...await original<typeof import('../src/osanProgress')>(), previewOsanPhoto: vi.fn() }));
beforeEach(() => { URL.createObjectURL = vi.fn(() => 'blob:preview'); URL.revokeObjectURL = vi.fn(); });
afterEach(() => vi.restoreAllMocks());
it('HEIC 원본은 그대로 미리보기 API에 보내고 표시용 JPEG가 준비되면 보여준다', async () => {
  let resolve!: (blob: Blob) => void;
  vi.mocked(api.previewOsanPhoto).mockImplementation(() => new Promise(r => { resolve = r; }));
  const file = new File(['original'], 'phone.HEIC', {type:'image/heic'});
  render(<OsanPhotoPreview file={file} projectId="project" userKey="user"/>);
  expect(screen.getByRole('status')).toHaveTextContent('준비 중');
  expect(api.previewOsanPhoto).toHaveBeenCalledWith('project', file, 'user', expect.any(AbortSignal));
  resolve(new Blob(['jpeg'], {type:'image/jpeg'}));
  const img = await screen.findByRole('img'); fireEvent.load(img);
  expect(screen.queryByRole('status')).toBeNull();
});
it('HEIC 처리 실패와 이미지 표시 실패에 이유를 보여준다', async () => {
  vi.mocked(api.previewOsanPhoto).mockRejectedValue(new Error('사진 부가정보를 읽을 수 없습니다.'));
  const {unmount} = render(<OsanPhotoPreview file={new File(['bad'], 'bad.heic')} projectId="project"/>);
  expect(await screen.findByRole('alert')).toHaveTextContent('부가정보'); unmount();
  render(<OsanPhotoPreview file={new File(['bad'], 'bad.jpg', {type:'image/jpeg'})} projectId="project"/>);
  fireEvent.error(await screen.findByRole('img'));
  expect(screen.getByRole('alert')).toHaveTextContent('표시할 수 없습니다');
});
it('사진을 바꾸면 이전 늦은 응답을 무시한다', async () => {
  let resolve!: (blob: Blob) => void;
  vi.mocked(api.previewOsanPhoto).mockImplementation(() => new Promise(r => {resolve=r;}));
  const {rerender} = render(<OsanPhotoPreview file={new File(['one'], 'one.heic')} projectId="project"/>);
  rerender(<OsanPhotoPreview file={new File(['two'], 'two.jpg', {type:'image/jpeg'})} projectId="project"/>);
  await screen.findByRole('img',{name:'two.jpg 미리보기'});
  resolve(new Blob(['late']));
  await waitFor(()=>expect(URL.createObjectURL).toHaveBeenCalledTimes(1));
});
it('파일 URL이 준비돼도 이미지 로딩이 멈추면 지연 오류를 표시한다', async () => {
  const { act } = await import('@testing-library/react');
  vi.useFakeTimers();
  try {
    await act(async () => { render(<OsanPhotoPreview file={new File(['jpeg'], 'stalled.jpg', {type:'image/jpeg'})} projectId="project"/>); });
    expect(screen.getByRole('img')).toBeInTheDocument();
    await act(async () => { vi.advanceTimersByTime(60_000); });
    expect(screen.getByRole('alert')).toHaveTextContent('지연');
  } finally { vi.useRealTimers(); }
});
