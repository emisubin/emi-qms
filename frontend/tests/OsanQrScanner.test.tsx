import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import jsQR from 'jsqr';
import { OsanQrScanner } from '../src/OsanQrScanner';
import { OsanMobileTools } from '../src/OsanMobileTools';
import { parseOsanPanelQr } from '../src/osanQrScan';
vi.mock('jsqr', () => ({ default: vi.fn() }));
const projectId = '11111111-1111-1111-1111-111111111111';
const targetId = '22222222-2222-2222-2222-222222222222';
const url = `https://pms.emiinc.co.kr/osan/qr/${projectId}/${targetId}`;
const stop = vi.fn();
const media = vi.fn();
beforeEach(() => {
  vi.clearAllMocks(); vi.mocked(jsQR).mockReturnValue(null);
  vi.stubGlobal('navigator', Object.assign(Object.create(navigator), { mediaDevices: { getUserMedia: media } }));
  media.mockResolvedValue({ getTracks: () => [{ stop }] });
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.open = true; } });
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, 'readyState', 'get').mockReturnValue(4);
  vi.spyOn(HTMLVideoElement.prototype, 'videoWidth', 'get').mockReturnValue(100);
  vi.spyOn(HTMLVideoElement.prototype, 'videoHeight', 'get').mockReturnValue(100);
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn(), getImageData: () => ({ data: new Uint8ClampedArray(40000), width: 100, height: 100 }) } as unknown as CanvasRenderingContext2D);
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it('accepts only local/canonical panel QR and extracts identifiers without navigating to supplied URL', () => {
  expect(parseOsanPanelQr(url)).toEqual({ projectId, targetId });
  expect(parseOsanPanelQr(url.replace('https://pms.emiinc.co.kr', 'https://evil.example'))).toBeNull();
  expect(parseOsanPanelQr(url + '?redirect=https://evil.example')).toBeNull();
  expect(parseOsanPanelQr(url + '?businessUnit=CHEONGJU')).toBeNull();
  expect(parseOsanPanelQr(url.replace('https://', 'https://user:pass@'))).toBeNull();
  expect(parseOsanPanelQr('javascript:alert(1)')).toBeNull();
  expect(parseOsanPanelQr(url.replace('/' + targetId, ''))).toBeNull();
});
it('decodes once, stops camera before opening the existing panel route', async () => {
  vi.mocked(jsQR).mockReturnValue({ data: url } as ReturnType<typeof jsQR>);
  const onScan = vi.fn(() => expect(stop).toHaveBeenCalled());
  render(<OsanQrScanner onClose={vi.fn()} onScan={onScan} />);
  await waitFor(() => expect(onScan).toHaveBeenCalledExactlyOnceWith(projectId, targetId));
  expect(media).toHaveBeenCalledWith(expect.objectContaining({ audio: false }));
});
it('stops an unrecognized QR, shows retry and never navigates', async () => {
  vi.mocked(jsQR).mockReturnValue({ data: 'https://evil.example' } as ReturnType<typeof jsQR>);
  const onScan = vi.fn(); render(<OsanQrScanner onClose={vi.fn()} onScan={onScan} />);
  expect(await screen.findByRole('alert')).toHaveTextContent('PMS 패널 QR 코드가 아닙니다');
  expect(onScan).not.toHaveBeenCalled(); expect(stop).toHaveBeenCalled();
  vi.mocked(jsQR).mockReturnValue(null); fireEvent.click(screen.getByText('다시 시도'));
  await waitFor(() => expect(media).toHaveBeenCalledTimes(2));
});
it('explains denied permission and allows a fresh camera request', async () => {
  media.mockRejectedValueOnce(new DOMException('denied', 'NotAllowedError'));
  render(<OsanQrScanner onClose={vi.fn()} onScan={vi.fn()} />);
  expect(await screen.findByRole('alert')).toHaveTextContent('카메라 사용 권한');
  fireEvent.click(screen.getByText('다시 시도')); await waitFor(() => expect(media).toHaveBeenCalledTimes(2));
});
it('stops camera when permission resolves after the scanner has closed', async () => {
  let resolve!: (value: unknown) => void;
  media.mockReturnValue(new Promise(r => { resolve = r; }));
  const view = render(<OsanQrScanner onClose={vi.fn()} onScan={vi.fn()} />);
  view.unmount(); await act(async () => { resolve({ getTracks: () => [{ stop }] }); });
  expect(stop).toHaveBeenCalled(); expect(jsQR).not.toHaveBeenCalled();
});
it('stops the camera when backgrounded and offers explicit restart', async () => {
  render(<OsanQrScanner onClose={vi.fn()} onScan={vi.fn()} />);
  await screen.findByText('QR 코드를 찾고 있습니다.');
  vi.spyOn(document, 'hidden', 'get').mockReturnValue(true);
  fireEvent(document, new Event('visibilitychange'));
  expect(stop).toHaveBeenCalled(); expect(screen.getByRole('alert')).toHaveTextContent('일시 중지');
});
it('opens floating menu, navigates and dismisses by outside click or Escape', () => {
  const onNavigate = vi.fn(); render(<OsanMobileTools current="home" onNavigate={onNavigate} onScan={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: '메뉴 열기' }));
  fireEvent.click(screen.getByRole('button', { name: '진행 현황' }));
  expect(onNavigate).toHaveBeenCalledWith('osan-progress'); expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '메뉴 열기' })); fireEvent.keyDown(screen.getByRole('button', { name: '홈' }), { key: 'Escape' });
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: '메뉴 열기' })); fireEvent.click(screen.getByRole('button', { name: '메뉴 바깥을 눌러 닫기' }));
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
});

it('tries camera-resolution centre before full-frame fallback', async () => {
  vi.spyOn(HTMLVideoElement.prototype, 'videoWidth', 'get').mockReturnValue(1920);
  vi.spyOn(HTMLVideoElement.prototype, 'videoHeight', 'get').mockReturnValue(1080);
  const drawImage = vi.fn();
  vi.mocked(HTMLCanvasElement.prototype.getContext).mockReturnValue({ drawImage, getImageData: () => ({ data: new Uint8ClampedArray(4), width: 1, height: 1 }) } as unknown as CanvasRenderingContext2D);
  vi.mocked(jsQR).mockReturnValueOnce(null).mockReturnValueOnce({ data: url } as ReturnType<typeof jsQR>);
  const onScan = vi.fn(); render(<OsanQrScanner onClose={vi.fn()} onScan={onScan} />);
  await waitFor(() => expect(onScan).toHaveBeenCalledExactlyOnceWith(projectId, targetId));
  expect(drawImage.mock.calls[0].slice(1)).toEqual([609, 189, 702, 702, 0, 0, 702, 702]);
  expect(drawImage.mock.calls[1].slice(1)).toEqual([0, 0, 1920, 1080, 0, 0, 960, 540]);
  expect(stop).toHaveBeenCalled();
});

it('a late stopped camera cannot clear the new camera zoom controls', async () => {
  let finishFirst!: (value: MediaStream) => void;
  media.mockReturnValueOnce(new Promise<MediaStream>(resolve => { finishFirst = resolve; }));
  const applyConstraints = vi.fn().mockResolvedValue(undefined);
  const secondTrack = { stop: vi.fn(), getCapabilities: () => ({ zoom: { min: 1, max: 3, step: 0.1 } }), getSettings: () => ({ zoom: 1 }), getConstraints: () => ({}), applyConstraints };
  media.mockResolvedValueOnce({ getTracks: () => [secondTrack], getVideoTracks: () => [secondTrack] });
  render(<OsanQrScanner onClose={vi.fn()} onScan={vi.fn()} />);
  await waitFor(() => expect(media).toHaveBeenCalledTimes(1));
  vi.spyOn(document, 'hidden', 'get').mockReturnValue(true);
  fireEvent(document, new Event('visibilitychange'));
  fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
  await screen.findByRole('slider', { name: '카메라 배율' });
  const oldStop = vi.fn();
  await act(async () => finishFirst({ getTracks: () => [{ stop: oldStop }] } as unknown as MediaStream));
  expect(oldStop).toHaveBeenCalled(); expect(secondTrack.stop).not.toHaveBeenCalled();
  fireEvent.change(screen.getByRole('slider', { name: '카메라 배율' }), { target: { value: '2' } });
  await waitFor(() => expect(applyConstraints).toHaveBeenCalledWith({ advanced: [{ zoom: 2 }] }));
});
