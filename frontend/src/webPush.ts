export type WebPushConfiguration = {
  enabled: boolean;
  dryRun: boolean;
  configured: boolean;
  publicKey: string | null;
  activeDeviceCount: number;
  lastChangedAtUtc: string | null;
};

export type WebPushCurrentSubscriptionStatus = {
  active: boolean;
};

export type WebPushSubscriptionMutation = {
  active: boolean;
  activeDeviceCount: number;
  changedAtUtc: string;
};

export const webPushGuideDismissedStorageKey = 'emi-pms:web-push-guide-dismissed-v1';
export const webPushServiceWorkerPath = '/web-push-service-worker.js';

export function supportsWebPush() {
  return typeof window !== 'undefined'
    && window.isSecureContext
    && 'serviceWorker' in navigator
    && 'PushManager' in window
    && 'Notification' in window;
}

export function isInstalledPwa() {
  if (typeof window === 'undefined') return false;
  const standaloneNavigator = navigator as Navigator & { standalone?: boolean };
  return window.matchMedia?.('(display-mode: standalone)').matches === true
    || standaloneNavigator.standalone === true;
}

export async function getWebPushRegistration() {
  if (!supportsWebPush()) return null;
  return navigator.serviceWorker.register(webPushServiceWorkerPath, { scope: '/' });
}

export async function getCurrentBrowserSubscription() {
  const registration = await getWebPushRegistration();
  return registration ? registration.pushManager.getSubscription() : null;
}

export function toWebPushRequest(subscription: PushSubscription) {
  const json = subscription.toJSON();
  if (!json.endpoint || !json.keys?.p256dh || !json.keys?.auth) {
    throw new Error('브라우저의 푸시 구독 정보를 확인할 수 없습니다.');
  }
  return {
    endpoint: json.endpoint,
    keys: { p256dh: json.keys.p256dh, auth: json.keys.auth }
  };
}

export function decodeVapidPublicKey(value: string) {
  const padding = '='.repeat((4 - value.length % 4) % 4);
  const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = window.atob(base64);
  return Uint8Array.from([...raw].map((character) => character.charCodeAt(0)));
}

export async function getOrCreateBrowserSubscription(
  registration: ServiceWorkerRegistration,
  publicKey: string
) {
  const expectedKey = decodeVapidPublicKey(publicKey);
  const existing = await registration.pushManager.getSubscription();
  if (existing) {
    const currentKey = existing.options.applicationServerKey
      ? new Uint8Array(existing.options.applicationServerKey)
      : null;
    const sameKey = currentKey?.length === expectedKey.length
      && currentKey.every((value, index) => value === expectedKey[index]);
    if (sameKey) return existing;
    await existing.unsubscribe();
  }

  return registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: expectedKey
  });
}
