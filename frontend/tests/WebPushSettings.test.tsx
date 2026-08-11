import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setRuntimeMutationAllowed } from '../src/api';
import { WebPushFirstRunPrompt, WebPushSettings } from '../src/WebPushSettings';
import { deactivateCurrentWebPushForLogout } from '../src/webPushLogout';
import { webPushGuideDismissedStorageKey } from '../src/webPush';

describe('WebPushSettings', () => {
  beforeEach(() => {
    setRuntimeMutationAllowed(true);
    Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true });
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: vi.fn(async () => ({
          pushManager: { getSubscription: vi.fn(async () => null) }
        }))
      }
    });
    vi.stubGlobal('PushManager', class PushManager {});
    window.localStorage.clear();
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
      matches: query === '(display-mode: standalone)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn()
    })));
  });

  afterEach(() => {
    setRuntimeMutationAllowed(false);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('does not request permission automatically and explains browser-level blocking', async () => {
    const requestPermission = vi.fn(async () => 'denied' as NotificationPermission);
    vi.stubGlobal('Notification', { permission: 'denied', requestPermission });
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({
      enabled: true,
      dryRun: true,
      configured: true,
      publicKey: 'test-public-key',
      activeDeviceCount: 0,
      lastChangedAtUtc: null
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByText(/브라우저에서 알림이 차단되었습니다/)).toBeInTheDocument();
    expect(requestPermission).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: '이 기기 켜기' }));
    expect(await screen.findByText(/브라우저 알림 권한이 허용되지 않았습니다/)).toBeInTheDocument();
    expect(requestPermission).toHaveBeenCalledTimes(1);
  });

  it('keeps the lost-device reset available while the channel is globally disabled', async () => {
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/api/my/web-push/current-status')) {
        return new Response(JSON.stringify({ active: false }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify({
        enabled: false,
        dryRun: true,
        configured: false,
        publicKey: null,
        activeDeviceCount: 2,
        lastChangedAtUtc: '2026-08-11T01:00:00Z'
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByRole('button', { name: '모든 기기 연결 해제' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '이 기기 켜기' })).not.toBeInTheDocument();
  });

  it('keeps server-side all-device reset available when local service worker access is blocked', async () => {
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: { register: vi.fn(async () => { throw new Error('service worker blocked'); }) }
    });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/api/my/web-push/subscriptions/deactivate-all')) {
        return new Response(JSON.stringify({
          active: false,
          activeDeviceCount: 0,
          changedAtUtc: '2026-08-11T01:05:00Z'
        }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      return new Response(JSON.stringify({
        enabled: true,
        dryRun: true,
        configured: true,
        publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        activeDeviceCount: 2,
        lastChangedAtUtc: '2026-08-11T01:00:00Z'
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByText(/푸시 알림 상태를 확인할 수 없습니다/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '모든 기기 연결 해제' }));
    expect(await screen.findByText(/서버의 모든 기기 푸시 연결을 해제했습니다/)).toBeInTheDocument();
    expect(screen.getByText('0개 기기 연결')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/my/web-push/subscriptions/deactivate-all'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('shows exactly one prioritized state and distinguishes undecided permission', async () => {
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({
      enabled: true,
      dryRun: true,
      configured: true,
      publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
      activeDeviceCount: 0,
      lastChangedAtUtc: null
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByText('이 기기에서 알림 권한을 아직 선택하지 않았습니다.')).toBeInTheDocument();
    expect(screen.getAllByRole('status')).toHaveLength(1);
    expect(screen.queryByText(/홈 화면에 설치/)).not.toBeInTheDocument();
  });

  it('retries local service worker status without losing the loaded server configuration', async () => {
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    const register = vi.fn()
      .mockRejectedValueOnce(new Error('temporarily blocked'))
      .mockResolvedValue({ pushManager: { getSubscription: vi.fn(async () => null) } });
    Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: { register } });
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      enabled: true,
      dryRun: true,
      configured: true,
      publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
      activeDeviceCount: 0,
      lastChangedAtUtc: null
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByText('이 브라우저에서 푸시 알림 상태를 확인할 수 없습니다.')).toBeInTheDocument();
    expect(screen.queryByText(/아래에서 모두 해제/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
    expect(await screen.findByText('이 기기에서 알림 권한을 아직 선택하지 않았습니다.')).toBeInTheDocument();
    expect(register).toHaveBeenCalledTimes(2);
  });

  it('keeps current-device deactivation successful when local unsubscribe fails', async () => {
    const unsubscribe = vi.fn(async () => { throw new Error('local unsubscribe blocked'); });
    const subscription = { endpoint: 'https://push.example.test/current-device', unsubscribe };
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: vi.fn(async () => ({
          pushManager: { getSubscription: vi.fn(async () => subscription) }
        }))
      }
    });
    vi.stubGlobal('Notification', { permission: 'granted', requestPermission: vi.fn() });
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/api/my/web-push/current-status')) {
        return new Response(JSON.stringify({ active: true }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      if (url.endsWith('/api/my/web-push/subscriptions/deactivate-current')) {
        return new Response(JSON.stringify({ active: false, activeDeviceCount: 0, changedAtUtc: '2026-08-11T01:05:00Z' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify({
        enabled: true,
        dryRun: true,
        configured: true,
        publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        activeDeviceCount: 1,
        lastChangedAtUtc: null
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    fireEvent.click(await screen.findByRole('button', { name: '이 기기 끄기' }));
    expect(await screen.findByText(/서버에서 이 기기의 푸시 연결을 해제했습니다/)).toBeInTheDocument();
    expect(screen.getByText('0개 기기 연결')).toBeInTheDocument();
    expect(unsubscribe).toHaveBeenCalledTimes(1);
  });

  it('offers an explicit retry after a configuration load error', async () => {
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    const fetchMock = vi.fn()
      .mockRejectedValueOnce(new TypeError('temporary network failure'))
      .mockResolvedValue(new Response(JSON.stringify({
        enabled: false,
        dryRun: true,
        configured: false,
        publicKey: null,
        activeDeviceCount: 0,
        lastChangedAtUtc: null
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    render(<WebPushSettings developmentUserKey="dev-quality" />);

    expect(await screen.findByRole('button', { name: '다시 시도' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다시 시도' }));
    expect(await screen.findByText(/푸시 알림은 현재 준비 중입니다/)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('shows the installed-PWA guide once without requesting permission automatically', async () => {
    const requestPermission = vi.fn(async () => 'granted' as NotificationPermission);
    vi.stubGlobal('Notification', { permission: 'default', requestPermission });
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      enabled: true,
      dryRun: true,
      configured: true,
      publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
      activeDeviceCount: 0,
      lastChangedAtUtc: null
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    const first = render(<WebPushFirstRunPrompt developmentUserKey="dev-quality" />);

    expect(await screen.findByRole('dialog', { name: '이 기기에서 업무 알림 받기' })).toBeInTheDocument();
    expect(requestPermission).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: '나중에' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(window.localStorage.getItem(webPushGuideDismissedStorageKey)).toBe('true');

    first.unmount();
    render(<WebPushFirstRunPrompt developmentUserKey="dev-quality" />);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the guide when a local subscription is not active for the signed-in user', async () => {
    const localSubscription = { endpoint: 'https://push.example.test/previous-user' };
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: vi.fn(async () => ({
          pushManager: { getSubscription: vi.fn(async () => localSubscription) }
        }))
      }
    });
    vi.stubGlobal('Notification', { permission: 'default', requestPermission: vi.fn() });
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith('/api/my/web-push/current-status')) {
        return new Response(JSON.stringify({ active: false }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      return new Response(JSON.stringify({
        enabled: true,
        dryRun: true,
        configured: true,
        publicKey: 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        activeDeviceCount: 0,
        lastChangedAtUtc: null
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<WebPushFirstRunPrompt developmentUserKey="dev-quality" />);

    expect(await screen.findByRole('dialog', { name: '이 기기에서 업무 알림 받기' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/my/web-push/current-status'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('deactivates and unsubscribes only the current browser before logout', async () => {
    const unsubscribe = vi.fn(async () => true);
    const subscription = { endpoint: 'https://push.example.test/logout-device', unsubscribe };
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: vi.fn(async () => ({
          pushManager: { getSubscription: vi.fn(async () => subscription) }
        }))
      }
    });
    vi.stubGlobal('Notification', { permission: 'granted', requestPermission: vi.fn() });
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      active: false,
      activeDeviceCount: 1,
      changedAtUtc: '2026-08-11T01:00:00Z'
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetchMock);

    await deactivateCurrentWebPushForLogout('dev-quality');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/my/web-push/subscriptions/deactivate-current'),
      expect.objectContaining({ method: 'POST' })
    );
    expect(unsubscribe).toHaveBeenCalledTimes(1);
  });
});
