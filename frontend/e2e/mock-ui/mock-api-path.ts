import { expect, type Request } from '@playwright/test';

type BusinessUnit = 'CHEONGJU' | 'OSAN';

/** Validate the public URL boundary before dispatching an existing synthetic response. */
export function mockApiPath(request: Request, allowedUnits: readonly BusinessUnit[] = ['CHEONGJU']): string {
  const pathname = new URL(request.url()).pathname;
  if (pathname === '/health/ready') return pathname;

  const match = /^\/(access|cheongju|osan)(\/api\/.*)$/.exec(pathname);
  expect(match, `Expected a fixed business or access URL: ${pathname}`).not.toBeNull();
  const [, scope, path] = match!;
  const selectedUnit = request.headers()['x-qms-business-unit'];
  expect([undefined, 'CHEONGJU', 'OSAN']).toContain(selectedUnit);

  const sharedAdministration = /^\/api\/(business-units|admin\/user-access|admin\/business-unit-access)(\/|$)/.test(path);
  const unselectedIdentity = !selectedUnit && (path === '/api/me' || path === '/api/runtime-mode');
  if (sharedAdministration || unselectedIdentity) {
    expect(scope, `Shared access request must use /access: ${pathname}`).toBe('access');
  } else {
    const unit = selectedUnit ?? 'CHEONGJU';
    expect(allowedUnits, `Unexpected business unit in this fixture: ${pathname}`).toContain(unit);
    expect(scope, `URL must match the selected business unit: ${pathname}`).toBe(unit === 'OSAN' ? 'osan' : 'cheongju');
  }
  return path;
}
