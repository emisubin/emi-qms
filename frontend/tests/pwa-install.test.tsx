import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useEffect } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PwaInstallProvider } from '../src/PwaInstallExperience';
import { usePwaInstallExperience } from '../src/pwa-install';

function InstallEntry({ automaticGuideReady = true }: { automaticGuideReady?: boolean }) {
  const { available, entryLabel, openGuide, setAutomaticGuideReady } = usePwaInstallExperience();

  useEffect(() => {
    setAutomaticGuideReady(automaticGuideReady);
    return () => setAutomaticGuideReady(false);
  }, [automaticGuideReady, setAutomaticGuideReady]);

  return available
    ? <button type="button" onClick={openGuide}>{entryLabel}</button>
    : <span>설치 대상 아님</span>;
}

function stubMatchMedia(matches: (query: string) => boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: matches(query),
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn()
  }));
}

describe('PWA install experience', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    stubMatchMedia(() => false);
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 Chrome/140 Safari/537.36');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('MacIntel');
    Object.defineProperty(window.navigator, 'maxTouchPoints', {
      configurable: true,
      value: 0
    });
  });

  it('waits for the authenticated app shell before opening the mobile guide', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36');

    const { rerender } = render(
      <PwaInstallProvider>
        <InstallEntry automaticGuideReady={false} />
      </PwaInstallProvider>
    );

    expect(screen.queryByRole('dialog', { name: 'Android 설치 안내' })).not.toBeInTheDocument();

    rerender(
      <PwaInstallProvider>
        <InstallEntry automaticGuideReady />
      </PwaInstallProvider>
    );

    expect(await screen.findByRole('dialog', { name: 'Android 설치 안내' })).toBeInTheDocument();
  });

  it('does not treat a narrow desktop browser as an iPhone or Android device', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    await act(() => new Promise((resolve) => window.setTimeout(resolve, 0)));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  afterEach(() => {
    Object.defineProperty(window.navigator, 'clipboard', {
      configurable: true,
      value: undefined
    });
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('opens the browser install prompt only after a user action', async () => {
    const prompt = vi.fn().mockResolvedValue(undefined);
    const installEvent = Object.assign(new Event('beforeinstallprompt'), {
      prompt,
      userChoice: Promise.resolve({ outcome: 'accepted', platform: 'web' })
    });

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    act(() => window.dispatchEvent(installEvent));
    const entry = await screen.findByRole('button', { name: 'EMI PMS 설치' });
    expect(prompt).not.toHaveBeenCalled();

    fireEvent.click(entry);
    const dialog = await screen.findByRole('dialog', { name: 'EMI PMS 설치 안내' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'EMI PMS 설치' }));

    await waitFor(() => expect(prompt).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'EMI PMS 설치 안내' })).not.toBeInTheDocument());
  });

  it('shows the iPhone Home Screen instructions automatically on mobile Safari', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('iPhone');

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    const dialog = await screen.findByRole('dialog', { name: 'iPhone 설치 안내' });
    expect(within(dialog).getByText('홈 화면에 추가')).toBeInTheDocument();
    expect(within(dialog).getByText('공유')).toBeInTheDocument();
    expect(within(dialog).getByText('웹 앱으로 열기')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'iPhone 설치 안내' })).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByRole('button', { name: '확인' })).toHaveFocus());

    fireEvent.keyDown(dialog, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'iPhone 설치 안내' })).not.toBeInTheDocument());
  });

  it('offers a root-address copy fallback on non-Safari iPhone browsers', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 CriOS/140.0.0.0 Mobile/15E148 Safari/604.1');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('iPhone');
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(window.navigator, 'clipboard', {
      configurable: true,
      value: { writeText }
    });

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    const dialog = await screen.findByRole('dialog', { name: 'iPhone 설치 안내' });
    expect(dialog.querySelector('.pwa-install-note')).toHaveTextContent('현재 브라우저의 공유 메뉴에서 먼저 홈 화면에 추가를 확인해 주세요.');
    fireEvent.click(within(dialog).getByRole('button', { name: 'PMS 주소 복사' }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(new URL('/', window.location.origin).toString()));
    expect(within(dialog).getByRole('status')).toHaveTextContent('PMS 주소를 복사했습니다.');
  });

  it('explains manual copying when the iPhone clipboard is unavailable', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 EdgiOS/140.0 Mobile/15E148 Safari/604.1');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('iPhone');
    Object.defineProperty(window.navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockRejectedValue(new Error('clipboard unavailable')) }
    });

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    const dialog = await screen.findByRole('dialog', { name: 'iPhone 설치 안내' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'PMS 주소 복사' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('현재 주소창을 길게 눌러 주소를 복사해 주세요.');
  });

  it('labels the one-tap install guide for Android devices', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('Linux armv8l');
    const installEvent = Object.assign(new Event('beforeinstallprompt'), {
      prompt: vi.fn().mockResolvedValue(undefined),
      userChoice: Promise.resolve({ outcome: 'dismissed', platform: 'web' })
    });

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    act(() => window.dispatchEvent(installEvent));
    const dialog = await screen.findByRole('dialog', { name: 'Android 설치 안내' });
    expect(within(dialog).getByRole('heading', { name: '설치 방법' })).toBeInTheDocument();
    expect(within(dialog).getByRole('heading', { name: '이용 안내' })).toBeInTheDocument();
    expect(within(dialog).getByText(/Teams Activity와 메일로 전달될 수 있습니다/)).toBeInTheDocument();
    expect(within(dialog).getByText(/첫 로그인 안내 또는 내 알림 설정에서 이 기기의 푸시를 켤 수 있습니다/)).toBeInTheDocument();
    expect(within(dialog).getByText(/푸시를 켜지 않아도 인앱 알림과 모든 업무 기능/)).toBeInTheDocument();
    expect(within(dialog).getByText('설치를 누르면 브라우저의 설치 확인 창이 열립니다.')).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'EMI PMS 설치' })).toHaveFocus());
  });

  it('opens the Android guide before Chrome exposes its install prompt and enables the same button later', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('Linux armv8l');

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    const dialog = await screen.findByRole('dialog', { name: 'Android 설치 안내' });
    const installButton = within(dialog).getByRole('button', { name: 'EMI PMS 설치' });
    expect(installButton).toBeDisabled();
    expect(within(dialog).getByText(/설치 버튼을 준비하고 있습니다/)).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByRole('button', { name: '확인' })).toHaveFocus());

    const installEvent = Object.assign(new Event('beforeinstallprompt'), {
      prompt: vi.fn().mockResolvedValue(undefined),
      userChoice: Promise.resolve({ outcome: 'dismissed', platform: 'web' })
    });
    act(() => window.dispatchEvent(installEvent));

    await waitFor(() => expect(installButton).toBeEnabled());
    expect(within(dialog).getByText('설치를 누르면 브라우저의 설치 확인 창이 열립니다.')).toBeInTheDocument();
  });

  it('remembers dismissal only for the current tab session and ignores the former permanent flag', async () => {
    stubMatchMedia((query) => query === '(max-width: 767px)');
    vi.spyOn(window.navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1');
    vi.spyOn(window.navigator, 'platform', 'get').mockReturnValue('iPhone');
    window.localStorage.setItem('emi-pms:pwa-install-guide-dismissed', 'true');

    const firstRender = render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    const dialog = await screen.findByRole('dialog', { name: 'iPhone 설치 안내' });
    fireEvent.click(within(dialog).getByRole('button', { name: '확인' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'iPhone 설치 안내' })).not.toBeInTheDocument());
    firstRender.unmount();

    const secondRender = render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );
    expect(screen.queryByRole('dialog', { name: 'iPhone 설치 안내' })).not.toBeInTheDocument();
    secondRender.unmount();

    window.sessionStorage.clear();
    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );
    expect(await screen.findByRole('dialog', { name: 'iPhone 설치 안내' })).toBeInTheDocument();
  });

  it('hides install controls after the app is already running standalone', () => {
    stubMatchMedia((query) => query === '(display-mode: standalone)');

    render(
      <PwaInstallProvider>
        <InstallEntry />
      </PwaInstallProvider>
    );

    expect(screen.getByText('설치 대상 아님')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
