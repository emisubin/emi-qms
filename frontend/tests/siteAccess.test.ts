import { afterEach, describe, expect, it, vi } from 'vitest';

describe('site access browser identity', () => {
  afterEach(() => {
    window.localStorage.clear();
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it('reuses one browser client id across tabs that share localStorage', async () => {
    vi.spyOn(window.crypto, 'randomUUID')
      .mockReturnValue('11111111-1111-4111-8111-111111111111');
    const { getSiteAccessBrowserClientId } = await import('../src/siteAccess');

    const first = await getSiteAccessBrowserClientId();
    const second = await getSiteAccessBrowserClientId();

    expect(first).toBe('11111111-1111-4111-8111-111111111111');
    expect(second).toBe(first);
  });

  it('keeps a stable in-memory identity when storage writes are blocked', async () => {
    const blockedStorage = {
      getItem: vi.fn(() => null),
      setItem: vi.fn(() => { throw new DOMException('blocked', 'SecurityError'); })
    } as unknown as Storage;
    const { siteAccessTesting } = await import('../src/siteAccess');
    siteAccessTesting.resetInMemoryBrowserClientId();

    const first = siteAccessTesting.createWithStorageFallback(blockedStorage);
    const second = siteAccessTesting.createWithStorageFallback(blockedStorage);

    expect(first).toBe(second);
  });
});

describe('site access best-effort API', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it('sends only the fixed menu code and browser client id', async () => {
    const fetchMock = vi.fn(async (...args: Parameters<typeof fetch>) => {
      void args;
      return json({
        sessionId: '33333333-3333-4333-8333-333333333333',
        idempotencyReceipt: '44444444-4444-4444-8444-444444444444',
        startedAtUtc: '2026-09-01T00:00:00Z',
        lastActivityAtUtc: '2026-09-01T00:00:00Z',
        created: true
      });
    });
    vi.stubGlobal('fetch', fetchMock);
    const { signalSiteAccess, siteAccessApiTesting } = await import('../src/api');
    siteAccessApiTesting.reset();

    await signalSiteAccess(
      'dev-sales',
      '55555555-5555-4555-8555-555555555555',
      'Projects'
    );

    const [, init] = fetchMock.mock.calls[0];
    expect(init).toBeDefined();
    const requestInit = init!;
    expect(JSON.parse(String(requestInit.body))).toEqual({
      browserClientId: '55555555-5555-4555-8555-555555555555',
      menuCode: 'Projects'
    });
    expect(String(requestInit.body)).not.toContain('/projects/');
    expect((requestInit.headers as Headers).get('X-Dev-User')).toBe('dev-sales');
  });

  it('lets logout continue when the end request never resolves', async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(json({
        sessionId: '33333333-3333-4333-8333-333333333333',
        idempotencyReceipt: '44444444-4444-4444-8444-444444444444',
        startedAtUtc: '2026-09-01T00:00:00Z',
        lastActivityAtUtc: '2026-09-01T00:00:00Z',
        created: true
      }))
      .mockImplementationOnce(() => new Promise<Response>(() => undefined));
    vi.stubGlobal('fetch', fetchMock);
    const { endCurrentSiteAccess, signalSiteAccess, siteAccessApiTesting } = await import('../src/api');
    siteAccessApiTesting.reset();
    await signalSiteAccess(
      'dev-sales',
      '55555555-5555-4555-8555-555555555555',
      'Home'
    );

    const ending = endCurrentSiteAccess();
    await vi.advanceTimersByTimeAsync(siteAccessApiTesting.deadlineMs);

    await expect(ending).resolves.toBeUndefined();
  });

  it('lets logout continue when a preceding signal never resolves', async () => {
    vi.useFakeTimers();
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)));
    const { endCurrentSiteAccess, signalSiteAccess, siteAccessApiTesting } = await import('../src/api');
    siteAccessApiTesting.reset();
    const signal = signalSiteAccess(
      'dev-sales',
      '55555555-5555-4555-8555-555555555555',
      'Home'
    ).catch(() => undefined);

    const ending = endCurrentSiteAccess();
    await vi.advanceTimersByTimeAsync(siteAccessApiTesting.deadlineMs);

    await expect(ending).resolves.toBeUndefined();
    await expect(signal).resolves.toBeUndefined();
  });
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
