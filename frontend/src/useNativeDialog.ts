import { useEffect, useRef } from 'react';

/** Shares native modal lifecycle only; each caller keeps its own layout and dismissal rules. */
export function useNativeDialog(open: boolean, setOpen: (open: boolean) => void) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current;
    if (!element || !open) return;
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const onClose = () => { if (!element.open) setOpen(false); };
    element.addEventListener('close', onClose);
    if (!element.open) element.showModal();
    return () => {
      element.removeEventListener('close', onClose);
      // Preserve focus intentionally moved elsewhere (for example when another dialog opens).
      const active = document.activeElement;
      const restoreFocus = active === document.body || active === element || (active !== null && element.contains(active));
      if (element.open) element.close();
      if (restoreFocus && previousFocus?.isConnected && !previousFocus.matches(':disabled')
        && !previousFocus.closest('[inert]')) previousFocus.focus({ preventScroll: true });
    };
  }, [open, setOpen]);
  return dialog;
}
