export const siteAccessMenuCodes = [
  'Home',
  'PrivacyNotice',
  'NoticeBoard',
  'MyWork',
  'TeamsActivity',
  'Projects',
  'Sales',
  'G2',
  'FormTemplates',
  'ProductionPlanning',
  'Procurement',
  'Materials',
  'Manufacturing',
  'Quality',
  'Logistics',
  'Notifications',
  'NotificationSettings',
  'Pending',
  'Administration'
] as const;

export type SiteAccessMenuCode = (typeof siteAccessMenuCodes)[number];

const browserClientStorageKey = 'emi-pms.site-access.browser-client-id';
const browserClientLockName = 'emi-pms.site-access.browser-client-id';
const browserClientDatabaseName = 'emi-pms-site-access';
const browserClientObjectStoreName = 'identity';
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/iu;
let inMemoryBrowserClientId: string | null = null;

function validStoredId(value: string | null): value is string {
  return value !== null && uuidPattern.test(value);
}

function createId() {
  return crypto.randomUUID();
}

function getInMemoryBrowserClientId() {
  inMemoryBrowserClientId ??= createId();
  return inMemoryBrowserClientId;
}

function read(storage: Storage) {
  try {
    const value = storage.getItem(browserClientStorageKey);
    return validStoredId(value) ? value : null;
  } catch {
    return null;
  }
}

function write(storage: Storage, value: string) {
  try {
    storage.setItem(browserClientStorageKey, value);
    return true;
  } catch {
    return false;
  }
}

function createWithStorageFallback(storage: Storage) {
  const existing = read(storage);
  if (existing) return existing;

  const candidate = getInMemoryBrowserClientId();
  if (!write(storage, candidate)) return getInMemoryBrowserClientId();
  return read(storage) ?? candidate;
}

function createWithIndexedDb(storage: Storage | null): Promise<string> {
  return new Promise((resolve, reject) => {
    const openRequest = indexedDB.open(browserClientDatabaseName, 1);

    openRequest.onupgradeneeded = () => {
      const database = openRequest.result;
      if (!database.objectStoreNames.contains(browserClientObjectStoreName)) {
        database.createObjectStore(browserClientObjectStoreName);
      }
    };
    openRequest.onerror = () => reject(openRequest.error ?? new Error('브라우저 접속 식별자 저장소를 열지 못했습니다.'));
    openRequest.onblocked = () => reject(new Error('브라우저 접속 식별자 저장소가 잠겨 있습니다.'));
    openRequest.onsuccess = () => {
      const database = openRequest.result;
      const transaction = database.transaction(browserClientObjectStoreName, 'readwrite');
      const objectStore = transaction.objectStore(browserClientObjectStoreName);
      const getRequest = objectStore.get(browserClientStorageKey);
      let selectedId: string | null = null;

      getRequest.onsuccess = () => {
        const storedId = typeof getRequest.result === 'string' && validStoredId(getRequest.result)
          ? getRequest.result
          : null;
        selectedId = storedId ?? (storage ? read(storage) : null) ?? createId();
        if (!storedId) objectStore.put(selectedId, browserClientStorageKey);
      };
      getRequest.onerror = () => transaction.abort();
      transaction.oncomplete = () => {
        database.close();
        if (!selectedId) {
          reject(new Error('브라우저 접속 식별자를 만들지 못했습니다.'));
          return;
        }
        if (storage) write(storage, selectedId);
        resolve(selectedId);
      };
      transaction.onerror = () => {
        database.close();
        reject(transaction.error ?? new Error('브라우저 접속 식별자를 저장하지 못했습니다.'));
      };
      transaction.onabort = () => {
        database.close();
        reject(transaction.error ?? new Error('브라우저 접속 식별자 저장이 취소됐습니다.'));
      };
    };
  });
}

export async function getSiteAccessBrowserClientId(): Promise<string> {
  if (typeof window === 'undefined') return getInMemoryBrowserClientId();

  let storage: Storage | null = null;
  try {
    storage = window.localStorage;
  } catch { /* IndexedDB 또는 현재 문서 fallback을 사용한다. */ }

  const existing = storage ? read(storage) : null;
  if (existing) return existing;

  const locks = navigator.locks;
  if (locks && storage) {
    try {
      const lockedId = await locks.request(browserClientLockName, async () => {
        const existing = read(storage);
        if (existing) return existing;
        const candidate = createId();
        if (!write(storage, candidate)) return null;
        return read(storage) ?? candidate;
      });
      if (lockedId) return lockedId;
    } catch {
      // Storage bucket 또는 Web Locks 정책이 거부되어도 접속 기록 자체는 계속한다.
    }
  }

  if (typeof indexedDB !== 'undefined') {
    try {
      return await createWithIndexedDb(storage);
    } catch {
      // IndexedDB 정책이 거부되면 current-document fallback으로 기록을 계속한다.
    }
  }

  return storage ? createWithStorageFallback(storage) : getInMemoryBrowserClientId();
}

export const siteAccessTesting = {
  browserClientStorageKey,
  browserClientDatabaseName,
  createWithIndexedDb,
  createWithStorageFallback,
  resetInMemoryBrowserClientId() {
    inMemoryBrowserClientId = null;
  }
};
