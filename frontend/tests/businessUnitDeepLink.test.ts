import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  window.history.replaceState(null, '', '/');
  window.sessionStorage.clear();
  vi.resetModules();
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

  it('ignores unrecognized selectors', async () => {
    vi.resetModules();
    window.sessionStorage.setItem('emi.qms.business-unit', 'CHEONGJU');
    window.history.replaceState(null, '', '/notifications?businessUnit=OTHER');
    const api = await import('../src/api');
    expect(api.getBusinessUnitRequestState().selectedBusinessUnit).toBe('CHEONGJU');
  });
});
