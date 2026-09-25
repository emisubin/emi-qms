import { StrictMode } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { resetBusinessUnitRequestContext, selectBusinessUnit, setRuntimeMutationAllowed } from '../src/api';
import { WebPushFirstRunPrompt } from '../src/WebPushSettings';
import { decodeVapidPublicKey, webPushGuideDismissedStorageKey } from '../src/webPush';

const key = 'BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA';
const config = { enabled: true, configured: true, dryRun: false, publicKey: key, activeDeviceCount: 0, hasUserDisabledSubscription: false };
const response = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
let reason: string | null;
let optedOut: boolean;
let local: PushSubscription | null;
let requestPermission: ReturnType<typeof vi.fn>;
let unsubscribe: ReturnType<typeof vi.fn>;
let subscribe: ReturnType<typeof vi.fn>;
let fetchMock: ReturnType<typeof vi.fn>;
const newSubscription = { endpoint: 'https://web.push.apple.com/new', toJSON: () => ({ endpoint: 'https://web.push.apple.com/new', keys: { p256dh: 'test', auth: 'test' } }) } as unknown as PushSubscription;

describe('Osan push recovery', () => {
  beforeEach(() => {
    resetBusinessUnitRequestContext(true); selectBusinessUnit('OSAN'); setRuntimeMutationAllowed(true);
    localStorage.clear(); sessionStorage.clear();
    localStorage.setItem(`${webPushGuideDismissedStorageKey}:OSAN`, 'true');
    reason = 'WebPushHttp410'; optedOut = false;
    requestPermission = vi.fn(async () => 'granted');
    vi.stubGlobal('Notification', { permission: 'granted', requestPermission });
    vi.stubGlobal('PushManager', class {});
    Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true });
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true })));
    unsubscribe = vi.fn(async () => { local = null; return true; });
    local = { endpoint: 'https://web.push.apple.com/expired', options: { applicationServerKey: decodeVapidPublicKey(key).buffer }, unsubscribe } as unknown as PushSubscription;
    subscribe = vi.fn(async () => { local = newSubscription; return newSubscription; });
    Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: { register: vi.fn(async () => ({ pushManager: { getSubscription: vi.fn(async () => local), subscribe } })) } });
    fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/current-status')) return response({ active: false, deactivationReason: reason, lastFailureCode: reason });
      if (init?.method === 'PUT') return response({ active: true, activeDeviceCount: 1 });
      return response({ ...config, hasUserDisabledSubscription: optedOut });
    });
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => { resetBusinessUnitRequestContext(true); setRuntimeMutationAllowed(false); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it('renews an expired endpoint despite the old dismissed guide, only on user action', async () => {
    render(<StrictMode><WebPushFirstRunPrompt developmentUserKey="dev" accountScope="user-a" /></StrictMode>);
    expect(await screen.findByRole('dialog', { name: '푸시 알림 다시 연결' })).toBeInTheDocument();
    expect(unsubscribe).not.toHaveBeenCalled(); expect(subscribe).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: '다시 연결' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(unsubscribe).toHaveBeenCalledTimes(1); expect(subscribe).toHaveBeenCalledTimes(1);
    const save = fetchMock.mock.calls.find(([, options]) => options?.method === 'PUT');
    expect(JSON.parse(save?.[1]?.body as string).endpoint).toBe('https://web.push.apple.com/new');
    expect(new Headers(save?.[1]?.headers).get('X-Qms-Business-Unit')).toBe('OSAN');
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it.each([true, false])('preserves UserRequest even when browser subscription exists=%s', async existing => {
    reason = 'UserRequest'; optedOut = true; if (!existing) local = null;
    render(<WebPushFirstRunPrompt developmentUserKey="dev" />);
    await waitFor(() => expect(navigator.serviceWorker.register).toHaveBeenCalled());
    await new Promise(resolve => setTimeout(resolve, 20));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(subscribe).not.toHaveBeenCalled(); expect(unsubscribe).not.toHaveBeenCalled();
  });

  it('rechecks opt-out before saving if disabled while the prompt was open', async () => {
    render(<WebPushFirstRunPrompt developmentUserKey="dev" />);
    await screen.findByRole('button', { name: '다시 연결' });
    reason = 'UserRequest'; optedOut = true;
    fireEvent.click(screen.getByRole('button', { name: '다시 연결' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(subscribe).not.toHaveBeenCalled(); expect(unsubscribe).not.toHaveBeenCalled();
    expect(fetchMock.mock.calls.some(([, options]) => options?.method === 'PUT')).toBe(false);
  });

  it('offers unregistered users setup again without automatically requesting permission', async () => {
    local = null; reason = null; vi.stubGlobal('Notification', { permission: 'default', requestPermission });
    render(<WebPushFirstRunPrompt developmentUserKey="dev" />);
    expect(await screen.findByRole('dialog', { name: '이 기기에서 업무 알림 받기' })).toBeInTheDocument();
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it('keeps a failed registration retryable without recreating the renewed endpoint', async () => {
    let saves = 0;
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/current-status')) return response({ active: false, deactivationReason: local === newSubscription ? null : 'WebPushHttp410' });
      if (init?.method === 'PUT') { saves++; return saves === 1 ? response({ message: '일시적 저장 실패' }, 503) : response({ active: true }); }
      return response(config);
    });
    // A freshly created subscription reports the same VAPID key on retry.
    Object.assign(newSubscription, { options: { applicationServerKey: decodeVapidPublicKey(key).buffer } });
    render(<WebPushFirstRunPrompt developmentUserKey="dev" />);
    fireEvent.click(await screen.findByRole('button', { name: '다시 연결' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '다시 연결' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(subscribe).toHaveBeenCalledTimes(1); expect(saves).toBe(2);
  });

  it('does not renew when connection checking fails and offers a retry', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('offline'));
    render(<WebPushFirstRunPrompt developmentUserKey="dev" />);
    fireEvent.click(await screen.findByRole('button', { name: '다시 확인' }));
    expect(await screen.findByRole('button', { name: '다시 연결' })).toBeInTheDocument();
    expect(subscribe).not.toHaveBeenCalled();
  });

  it('rechecks on returning to the app but respects the daily snooze', async () => {
    const first = render(<WebPushFirstRunPrompt developmentUserKey="dev" accountScope="a" />);
    fireEvent.click(await screen.findByRole('button', { name: '나중에' }));
    fireEvent(document, new Event('visibilitychange'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    first.unmount();
    sessionStorage.setItem('emi-pms:push-recovery-dismissed:OSAN:a', String(Date.now() - 86400001));
    render(<WebPushFirstRunPrompt developmentUserKey="dev" accountScope="a" />);
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
  });
});

describe('subscription renewal scope boundary', () => {
  it('does not unsubscribe if the account changes while reading the browser subscription', async () => {
    const { getOrCreateBrowserSubscription } = await import('../src/webPush');
    let current = true;
    let release!: (value: unknown) => void;
    const unsubscribe = vi.fn(); const subscribe = vi.fn();
    const registration = { pushManager: { getSubscription: () => new Promise(resolve => { release = resolve; }), subscribe } } as unknown as ServiceWorkerRegistration;
    const pending = getOrCreateBrowserSubscription(registration, key, true, () => current);
    current = false;
    release({ options: { applicationServerKey: decodeVapidPublicKey(key).buffer }, unsubscribe });
    await expect(pending).rejects.toThrow('캠퍼스가 변경');
    expect(unsubscribe).not.toHaveBeenCalled(); expect(subscribe).not.toHaveBeenCalled();
  });
  it('does not subscribe if the account changes during unsubscribe', async () => {
    const { getOrCreateBrowserSubscription } = await import('../src/webPush');
    let current = true;
    const subscribe = vi.fn();
    const registration = { pushManager: { getSubscription: async () => ({ options: { applicationServerKey: decodeVapidPublicKey(key).buffer }, unsubscribe: async () => { current = false; return true; } }), subscribe } } as unknown as ServiceWorkerRegistration;
    await expect(getOrCreateBrowserSubscription(registration, key, true, () => current)).rejects.toThrow('캠퍼스가 변경');
    expect(subscribe).not.toHaveBeenCalled();
  });
});
