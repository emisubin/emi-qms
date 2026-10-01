/** Business URLs bind the server module; the selection header cannot change their database. */
export function businessApiRoute(path: string, businessUnit: 'CHEONGJU' | 'OSAN' | null): string {
  if (!path.startsWith('/api/')) return path;
  const pathname = path.split('?')[0];
  const common = ((pathname === '/api/me' || pathname === '/api/runtime-mode') && businessUnit === null)
    || /^\/api\/(business-units|admin\/user-access|admin\/business-unit-access)(\/|$)/.test(pathname);
  if (common) return `/access${path}`;
  const module = businessUnit === 'OSAN' ? 'osan' : 'cheongju';
  return `/${module}${path}`;
}
