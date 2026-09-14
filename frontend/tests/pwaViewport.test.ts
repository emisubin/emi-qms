import { afterEach, expect, it, vi } from 'vitest';
import { installPwaViewport } from '../src/pwaViewport';
let dispose: (() => void) | undefined;
afterEach(() => { dispose?.(); vi.unstubAllGlobals(); document.head.querySelector('meta[name="viewport"]')?.remove(); });
it('allows browser zoom, restricts standalone gestures, restores browser mode and preserves one-finger scrolling', () => {
  const meta = document.createElement('meta'); meta.name = 'viewport'; meta.content = 'width=device-width, initial-scale=1'; document.head.append(meta);
  let change = () => {};
  const query = { matches: false, addEventListener: (_: string, fn: () => void) => { change = fn; }, removeEventListener: vi.fn() };
  vi.stubGlobal('matchMedia', vi.fn(() => query)); dispose = installPwaViewport();
  const pinch = () => { const e = new Event('gesturestart', { cancelable: true }); document.dispatchEvent(e); return e; };
  expect(pinch().defaultPrevented).toBe(false); expect(meta.content).not.toContain('user-scalable');
  query.matches = true; change(); expect(pinch().defaultPrevented).toBe(true); expect(meta.content).toContain('user-scalable=no');
  const scroll = new Event('touchmove', { cancelable: true }); Object.defineProperty(scroll, 'touches', { value: [{}] }); document.dispatchEvent(scroll); expect(scroll.defaultPrevented).toBe(false);
  query.matches = false; change(); expect(pinch().defaultPrevented).toBe(false); expect(meta.content).toBe('width=device-width, initial-scale=1');
});
it('detects iOS home-screen launch independently of standalone media support', () => {
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
  vi.stubGlobal('navigator', Object.assign(Object.create(navigator), { standalone: true }));
  dispose = installPwaViewport(); expect(document.documentElement).toHaveClass('pms-installed');
});
