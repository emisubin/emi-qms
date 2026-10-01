import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  window.history.replaceState(null, '', '/');
  window.sessionStorage.clear();
  vi.resetModules();
  vi.unstubAllGlobals();
});

describe('notification campus deep links', () => {
  it.each(['OSAN', 'CHEONGJU'])('selects the explicit %s campus before any request', async (unit) => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', unit === 'OSAN' ? 'CHEONGJU' : 'OSAN');
    window.history.replaceState(null, '', `/teams/activity/notifications/example?businessUnit=${unit}`);
    const api = await import('../src/api');
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBe(unit);
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe(unit);
  });

  it('gives an explicit valid business selector priority over a notificationId and other context', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    const notificationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const context = encodeURIComponent(JSON.stringify({
      subEntityId: `notification:OSAN:${notificationId}`
    }));
    window.history.replaceState(
      null,
      '',
      `/teams/activity?notificationId=${notificationId}&businessUnit=CHEONGJU&context=${context}`);

    const api = await import('../src/api');

    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBe('CHEONGJU');
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('CHEONGJU');
  });

  it.each([
    '/notifications?businessUnit=OTHER',
    '/notifications?businessUnit=CHEONGJU&businessUnit=OSAN',
    '/notifications?businessUnit=CHEONGJU&businessUnit=CHEONGJU'
  ])('rejects malformed or duplicate selectors instead of falling back to the saved campus: %s', async (path) => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    window.history.replaceState(null, '', path);
    const api = await import('../src/api');
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
  });

  it('selects the business encoded in a nested Teams notification context before requests', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    const notificationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const context = encodeURIComponent(JSON.stringify({
      page: { subEntityId: `notification:CHEONGJU:${notificationId}` }
    }));
    window.history.replaceState(null, '', `/teams/activity?context=${context}`);

    const api = await import('../src/api');

    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBe('CHEONGJU');
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('CHEONGJU');
  });

  it('rejects a legacy Teams notification context as ambiguous instead of using the saved campus', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    const notificationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const context = encodeURIComponent(JSON.stringify({ subEntityId: `notification:${notificationId}` }));
    window.history.replaceState(null, '', `/teams/activity?context=${context}`);

    const api = await import('../src/api');

    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
  });

  it.each([true, false])('rejects a notificationId query as ambiguous before storage fallback (saved=%s)', async (saved) => {
    vi.resetModules();
    if (saved) {
      window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    }
    const notificationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const context = encodeURIComponent(JSON.stringify({
      subEntityId: `notification:CHEONGJU:${notificationId}`
    }));
    window.history.replaceState(
      null,
      '',
      `/teams/activity?notificationId=${notificationId}&context=${context}`);

    const api = await import('../src/api');

    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
  });

  it('routes a legacy Cheongju QR before requests when Osan was saved', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    const path = '/q/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA';
    window.history.replaceState(null, '', path);
    const requests: Array<{ pathname: string; businessUnit: string | null }> = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input));
      const businessUnit = new Headers(init?.headers).get('X-Qms-Business-Unit');
      requests.push({ pathname: url.pathname, businessUnit });
      if (url.pathname.endsWith('/api/me')) {
        return new Response(JSON.stringify({
          userId: 'synthetic-dual-business-user',
          businessUnitAccess: {
            status: 'selected',
            selectedBusinessUnit: 'CHEONGJU',
            allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
            isOverallAdministrator: true,
            errorCode: null
          }
        }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      return new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      });
    }));

    const api = await import('../src/api');
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBe('CHEONGJU');
    api.setRuntimeMutationAllowed(true);
    await api.getCurrentUser('dev-admin');
    await api.resolvePanelQr('dev-admin', path.slice('/q/'.length));

    expect(requests.some((request) => request.pathname === '/cheongju/api/qr/resolve')).toBe(true);
    expect(requests.every((request) => request.businessUnit === 'CHEONGJU')).toBe(true);
    expect(requests.some((request) => request.pathname.startsWith('/osan/'))).toBe(false);
  });

  it('rejects a hintless notification detail as ambiguous instead of using the saved campus', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    window.history.replaceState(null, '', '/teams/activity/notifications/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    const requests: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      requests.push(url.pathname);
      return new Response(JSON.stringify({
        userId: 'synthetic-dual-business-user',
        businessUnitAccess: {
          status: 'selection_required',
          selectedBusinessUnit: null,
          allowedBusinessUnits: ['CHEONGJU', 'OSAN'],
          isOverallAdministrator: true,
          errorCode: 'business_unit_selection_required'
        }
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));

    const api = await import('../src/api');
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    await expect(api.getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: { status: 'selection_required' }
    });
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(requests).toEqual(['/access/api/me']);
  });

  it('clears an explicit campus that the server rejects without trying the saved campus', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'OSAN');
    window.history.replaceState(null, '', '/teams/activity/notifications/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?businessUnit=CHEONGJU');
    const requests: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      requests.push(url.pathname);
      return new Response(JSON.stringify({
        businessUnitAccess: {
          status: 'selection_denied',
          selectedBusinessUnit: null,
          allowedBusinessUnits: ['OSAN'],
          isOverallAdministrator: false,
          errorCode: 'business_unit_membership_denied'
        }
      }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));

    const api = await import('../src/api');
    await expect(api.getCurrentUser('dev-admin'))
      .rejects.toBeInstanceOf(api.BusinessUnitRequestInvalidatedError);
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(requests).toEqual(['/cheongju/api/me']);
  });
});
