import { useCallback, useEffect, useId, useRef, type ReactNode } from 'react';
import { useNativeDialog } from './useNativeDialog';
import './data-entry-dialog.css';

/** Approved shared mobile input sheet/form; callers provide business fields and permissions. */
export function DataEntryDialog({ title, onClose, children, footer, variant = 'form', busy = false }: {
  title: string; onClose: () => void; children: ReactNode; footer?: ReactNode;
  variant?: 'form' | 'detail' | 'menu' | 'compact'; busy?: boolean;
}) {
  const titleId = useId();
  const close = useRef(onClose);
  const saving = useRef(busy);
  useEffect(() => { close.current = onClose; saving.current = busy; }, [onClose, busy]);
  const setOpen = useCallback((open: boolean) => { if (!open && !saving.current) close.current(); }, []);
  const dialog = useNativeDialog(true, setOpen);
  useEffect(() => {
    const before = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = before; };
  }, []);
  return <dialog ref={dialog} className={`pms-entry-dialog pms-entry-dialog--${variant}`} aria-labelledby={titleId}
    onCancel={event => { event.preventDefault(); if (!busy) onClose(); }}>
    <header className="pms-entry-head"><h2 id={titleId}>{title}</h2><button type="button" aria-label={`${title} 닫기`} disabled={busy} onClick={onClose}>×</button></header>
    <div className="pms-entry-body">{children}</div>
    {footer && <footer className="pms-entry-foot">{footer}</footer>}
  </dialog>;
}
