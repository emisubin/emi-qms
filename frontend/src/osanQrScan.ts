export function parseOsanPanelQr(value: string, origin = window.location.origin): { projectId: string; targetId: string } | null {
  try {
    const url = new URL(value);
    if (url.origin !== origin && url.origin !== 'https://pms.emiinc.co.kr') return null;
    if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.hash) return null;
    if ([...url.searchParams].some(([key, value]) => key !== 'businessUnit' || value !== 'OSAN')) return null;
    const id = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';
    const match = url.pathname.match(new RegExp(`^/osan/qr/(${id})/(${id})/?$`, 'i'));
    if (!match || match.slice(1).includes('00000000-0000-0000-0000-000000000000')) return null;
    return { projectId: match[1].toLowerCase(), targetId: match[2].toLowerCase() };
  } catch { return null; }
}
