/** Restrict page gestures only in installed mode; browser tabs retain their viewport. */
export function installPwaViewport() {
  const display = window.matchMedia('(display-mode: standalone)');
  const meta = document.querySelector<HTMLMetaElement>('meta[name="viewport"]');
  const original = meta?.content ?? 'width=device-width, initial-scale=1.0, viewport-fit=cover';
  let installed = false;
  const update = () => {
    installed = display.matches || (navigator as Navigator & { standalone?: boolean }).standalone === true;
    document.documentElement.classList.toggle('pms-installed', installed);
    if (meta) meta.content = installed ? original + ', maximum-scale=1, user-scalable=no' : original;
  };
  const gesture = (event: Event) => { if (installed && event.cancelable) event.preventDefault(); };
  const touch = (event: TouchEvent) => { if (event.touches.length > 1) gesture(event); };
  update();
  display.addEventListener('change', update);
  document.addEventListener('gesturestart', gesture, { passive: false });
  document.addEventListener('gesturechange', gesture, { passive: false });
  document.addEventListener('touchmove', touch, { passive: false });
  return () => {
    display.removeEventListener('change', update);
    document.removeEventListener('gesturestart', gesture);
    document.removeEventListener('gesturechange', gesture);
    document.removeEventListener('touchmove', touch);
    document.documentElement.classList.remove('pms-installed');
    if (meta) meta.content = original;
  };
}
