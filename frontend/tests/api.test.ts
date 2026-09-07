import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  BusinessUnitRequestInvalidatedError,
  exportSelectedProjectsExcel,
  getAdminUsers,
  getBusinessUnitRequestState,
  getCurrentUser,
  getOwnProfilePhoto,
  removeOwnProfilePhoto,
  resetBusinessUnitRequestContext,
  selectBusinessUnit,
  setAccessTokenProvider,
  setRuntimeMutationAllowed,
  updateAdminUser,
  updateBusinessUnitUserAccess
} from '../src/api';

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

describe('business-unit API request context', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
    window.localStorage.clear();
    resetBusinessUnitRequestContext(true);
    setRuntimeMutationAllowed(true);
  });

  afterEach(() => {
    resetBusinessUnitRequestContext(true);
    setAccessTokenProvider(null);
    setRuntimeMutationAllowed(false);
    vi.unstubAllGlobals();
  });

  it('stores the selection per tab and attaches it to authenticated API requests', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      void input;
      void init;
      return json({ userId: 'synthetic-user' });
    });
    vi.stubGlobal('fetch', fetchMock);

    selectBusinessUnit('OSAN');
    await getCurrentUser('dev-admin');

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    expect(window.localStorage.getItem('emi.qms.business-unit')).toBeNull();
    const request = fetchMock.mock.calls[0];
    const headers = new Headers(request[1]?.headers);
    expect(headers.get('X-Qms-Business-Unit')).toBe('OSAN');
    expect(headers.get('X-Dev-User')).toBe('dev-admin');
  });

  it('binds an implicit single-membership /api/me result before accepting the shell', async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => json({
      businessUnitAccess: {
        status: 'selected',
        selectedBusinessUnit: 'OSAN',
        allowedBusinessUnits: ['OSAN'],
        isOverallAdministrator: false,
        errorCode: null
      },
      observedHeader: new Headers(init?.headers).get('X-Qms-Business-Unit')
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(getCurrentUser('dev-admin')).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('X-Qms-Business-Unit')).toBeNull();

    await expect(getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: { selectedBusinessUnit: 'OSAN' }
    });
    expect(new Headers(fetchMock.mock.calls[1][1]?.headers).get('X-Qms-Business-Unit')).toBe('OSAN');
  });

  it('does not silently move an old Cheongju tab to Osan after reassignment', async () => {
    let membership: 'CHEONGJU' | 'OSAN' = 'CHEONGJU';
    const observedHeaders: Array<string | null> = [];
    vi.stubGlobal('fetch', vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      const selectedHeader = new Headers(init?.headers).get('X-Qms-Business-Unit');
      observedHeaders.push(selectedHeader);
      if (selectedHeader && selectedHeader !== membership) {
        return json({
          businessUnitAccess: {
            status: 'selection_denied',
            selectedBusinessUnit: null,
            allowedBusinessUnits: [membership],
            isOverallAdministrator: false,
            errorCode: 'business_unit_membership_denied'
          }
        });
      }
      return json({
        businessUnitAccess: {
          status: 'selected',
          selectedBusinessUnit: membership,
          allowedBusinessUnits: [membership],
          isOverallAdministrator: false,
          errorCode: null
        }
      });
    }));

    await expect(getCurrentUser('dev-admin')).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
    await expect(getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: { selectedBusinessUnit: 'CHEONGJU' }
    });
    membership = 'OSAN';

    await expect(getCurrentUser('dev-admin')).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(observedHeaders.at(-1)).toBe('CHEONGJU');

    await expect(getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: {
        status: 'selection_denied',
        selectedBusinessUnit: null,
        errorCode: 'business_unit_context_reconfirmation_required'
      }
    });
    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(observedHeaders.at(-1)).toBeNull();

    const requestCountBeforeBlockedMutation = observedHeaders.length;
    await expect(updateAdminUser(
      'dev-admin',
      '50000000-0000-0000-0000-000000000001',
      {
        departmentId: null,
        roleCodes: [],
        isActive: true,
        isDepartmentHead: false
      }
    )).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
    expect(observedHeaders).toHaveLength(requestCountBeforeBlockedMutation);

    selectBusinessUnit('OSAN');
    await expect(getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: { selectedBusinessUnit: 'OSAN' }
    });
    expect(observedHeaders.at(-1)).toBe('OSAN');
  });

  it('locks switching for the full mutation response lifecycle', async () => {
    let resolveFetch!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => {
      resolveFetch = resolve;
    })));
    selectBusinessUnit('CHEONGJU');

    const mutation = updateBusinessUnitUserAccess(
      'dev-admin',
      '50000000-0000-0000-0000-000000000001',
      '70000000-0000-0000-0000-000000000001',
      0,
      [{
        businessUnitCode: 'CHEONGJU',
        departmentId: '10000000-0000-0000-0000-000000000001',
        roleCodes: ['system-administrator'],
        isActive: true,
        isDepartmentHead: false
      }]);

    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(1);
    expect(() => selectBusinessUnit('OSAN')).toThrowError('저장 작업이 끝난 뒤 사업부를 변경해 주세요.');

    resolveFetch(json({
      changed: true,
      accessVersion: 1,
      snapshot: { users: [], availableBusinessUnits: ['CHEONGJU', 'OSAN'], businessUnits: [] }
    }));
    await mutation;

    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(0);
    expect(() => selectBusinessUnit('OSAN')).not.toThrow();
  });

  it('also locks switching while a profile-photo removal is in flight', async () => {
    let resolveFetch!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => {
      resolveFetch = resolve;
    })));
    selectBusinessUnit('CHEONGJU');

    const mutation = removeOwnProfilePhoto('dev-admin');

    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(1);
    expect(() => selectBusinessUnit('OSAN')).toThrowError('저장 작업이 끝난 뒤 사업부를 변경해 주세요.');

    resolveFetch(new Response(null, { status: 204 }));
    await mutation;

    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(0);
  });

  it('aborts reads and rejects a stale response with a bounded invalidation error after a switch', async () => {
    let resolveFetch!: (response: Response) => void;
    let observedSignal: AbortSignal | undefined;
    vi.stubGlobal('fetch', vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      observedSignal = init?.signal ?? undefined;
      return new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      });
    }));
    selectBusinessUnit('CHEONGJU');
    const staleRead = getCurrentUser('dev-admin');
    await Promise.resolve();

    selectBusinessUnit('OSAN');
    expect(observedSignal?.aborted).toBe(true);
    resolveFetch(json({ userId: 'stale-cheongju-user' }));

    await expect(staleRead).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
  });

  it('invalidates a safe request during deferred token acquisition before an old business header can be sent', async () => {
    let resolveToken!: (token: string | null) => void;
    const fetchMock = vi.fn(async () => json({ userId: 'stale-cheongju-user' }));
    vi.stubGlobal('fetch', fetchMock);
    setAccessTokenProvider(() => new Promise<string | null>((resolve) => {
      resolveToken = resolve;
    }));
    selectBusinessUnit('CHEONGJU');

    const staleRead = getCurrentUser();
    await Promise.resolve();
    selectBusinessUnit('OSAN');
    resolveToken('synthetic-access-token');

    await expect(staleRead).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('invalidates a raw GET body when switching after response headers arrive', async () => {
    let releaseBody!: () => void;
    let markBodyReadStarted!: () => void;
    const bodyReadStarted = new Promise<void>((resolve) => {
      markBodyReadStarted = resolve;
    });
    const response = new Response(null, { status: 200 });
    Object.defineProperty(response, 'blob', {
      configurable: true,
      value: () => {
        markBodyReadStarted();
        return new Promise<Blob>((resolve) => {
          releaseBody = () => resolve(new Blob([new Uint8Array([1, 2, 3])]));
        });
      }
    });
    vi.stubGlobal('fetch', vi.fn(async () => response));
    selectBusinessUnit('CHEONGJU');

    const stalePhoto = getOwnProfilePhoto('dev-admin');
    await bodyReadStarted;
    selectBusinessUnit('OSAN');
    releaseBody();

    await expect(stalePhoto).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
  });

  it('consumes a missing profile-photo response before returning null', async () => {
    const consumeBody = vi.fn(async () => new ArrayBuffer(0));
    const response = new Response(null, { status: 404 });
    Object.defineProperty(response, 'arrayBuffer', {
      configurable: true,
      value: consumeBody
    });
    vi.stubGlobal('fetch', vi.fn(async () => response));
    selectBusinessUnit('CHEONGJU');

    await expect(getOwnProfilePhoto('dev-admin')).resolves.toBeNull();

    expect(consumeBody).toHaveBeenCalledOnce();
  });

  it('locks switching until a raw POST export has finished reading its response', async () => {
    let resolveFetch!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => {
      resolveFetch = resolve;
    })));
    selectBusinessUnit('CHEONGJU');

    const exportRequest = exportSelectedProjectsExcel('dev-admin', ['synthetic-project']);

    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(1);
    expect(() => selectBusinessUnit('OSAN')).toThrowError('저장 작업이 끝난 뒤 사업부를 변경해 주세요.');
    resolveFetch(new Response(new Uint8Array([1, 2, 3]), {
      status: 200,
      headers: {
        'Content-Disposition': 'attachment; filename="synthetic.xlsx"',
        'X-Export-Row-Count': '1'
      }
    }));
    const result = await exportRequest;

    expect(result.rowCount).toBe(1);
    expect(getBusinessUnitRequestState().inFlightMutationCount).toBe(0);
  });

  it('clears tab selection and invalidates outstanding reads on auth reset', async () => {
    let observedSignal: AbortSignal | undefined;
    vi.stubGlobal('fetch', vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      observedSignal = init?.signal ?? undefined;
      return new Promise<Response>(() => undefined);
    }));
    selectBusinessUnit('OSAN');
    void getCurrentUser('dev-admin');
    await Promise.resolve();

    resetBusinessUnitRequestContext(true);

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(observedSignal?.aborted).toBe(true);
  });

  it('clears a selected business after any API reports a membership-context denial', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json({
      errorCode: 'directory_membership_required',
      message: 'synthetic revoked membership'
    }, 403)));
    selectBusinessUnit('OSAN');

    await expect(getAdminUsers('dev-admin')).rejects.toMatchObject({ status: 403 });

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
  });

  it('invalidates the shell generation for membership revocation even when storage is empty', async () => {
    const fetchMock = vi.fn(async () => json({
      errorCode: 'directory_membership_required',
      message: 'synthetic last membership revoked'
    }, 403));
    vi.stubGlobal('fetch', fetchMock);
    const generationBefore = getBusinessUnitRequestState().generation;

    await expect(getAdminUsers('dev-admin')).rejects.toMatchObject({ status: 403 });

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
    expect(getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    expect(getBusinessUnitRequestState().generation).toBeGreaterThan(generationBefore);

    const generationAfterFirstDenial = getBusinessUnitRequestState().generation;
    await expect(getAdminUsers('dev-admin')).rejects.toMatchObject({ status: 403 });
    expect(getBusinessUnitRequestState().generation).toBe(generationAfterFirstDenial);

    await expect(updateAdminUser(
      'dev-admin',
      '50000000-0000-0000-0000-000000000001',
      {
        departmentId: null,
        roleCodes: [],
        isActive: true,
        isDepartmentHead: false
      }
    )).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('does not clear a selected business for an ordinary capability denial', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json({
      errorCode: 'business_unit_capability_disabled',
      message: 'synthetic restricted capability'
    }, 403)));
    selectBusinessUnit('OSAN');

    await expect(getAdminUsers('dev-admin')).rejects.toMatchObject({ status: 403 });

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    expect(getBusinessUnitRequestState().selectedBusinessUnit).toBe('OSAN');
  });

  it.each(['selection_denied', 'no_membership'] as const)(
    'clears a stored selection when /api/me returns a successful %s envelope',
    async (status) => {
      vi.stubGlobal('fetch', vi.fn(async () => json({
        businessUnitAccess: {
          status,
          selectedBusinessUnit: null,
          allowedBusinessUnits: [],
          isOverallAdministrator: false,
          errorCode: status
        }
      })));
      selectBusinessUnit('OSAN');

      await expect(getCurrentUser('dev-admin')).rejects.toBeInstanceOf(BusinessUnitRequestInvalidatedError);

      expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBeNull();
      expect(getBusinessUnitRequestState().selectedBusinessUnit).toBeNull();
    }
  );

  it('preserves the selected unit for a local-profile-pending /api/me envelope', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json({
      businessUnitAccess: {
        status: 'local_profile_pending',
        selectedBusinessUnit: 'OSAN',
        allowedBusinessUnits: ['OSAN'],
        isOverallAdministrator: false,
        errorCode: 'business_unit_local_profile_pending'
      }
    })));
    selectBusinessUnit('OSAN');

    await expect(getCurrentUser('dev-admin')).resolves.toMatchObject({
      businessUnitAccess: { status: 'local_profile_pending' }
    });

    expect(window.sessionStorage.getItem('emi.qms.business-unit')).toBe('OSAN');
    expect(getBusinessUnitRequestState().selectedBusinessUnit).toBe('OSAN');
  });
});
