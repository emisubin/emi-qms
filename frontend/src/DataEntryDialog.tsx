import { useCallback, useEffect, useId, useRef, type ReactNode } from 'react';
import { useNativeDialog } from './useNativeDialog';

/** Approved markup, with native focus trapping and application save lifecycle. */
export function DataEntryDialog({ title, onClose, children, footer, variant = 'form', busy = false }: {
  title: string; onClose: () => void; children: ReactNode; footer?: ReactNode;
  variant?: 'form' | 'detail' | 'menu' | 'compact'; busy?: boolean;
}) {
  const titleId = useId();
  const close = useRef(onClose);
  const saving = useRef(busy);
  useEffect(() => { close.current = onClose; saving.current = busy; }, [onClose, busy]);
  const setOpen = useCallback((open:boolean) => { if (!open && !saving.current) close.current(); }, []);
  const dialog = useNativeDialog(true, setOpen);
  useEffect(() => {
    const before = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = before; };
  }, []);
  const header=<header className="dialoghead"><h2 id={titleId}>{title}</h2><button type="button" className="close" aria-label={`${title} 닫기`} disabled={busy} onClick={onClose}>×</button></header>;
  return <dialog ref={dialog} id={variant==='form'?'layer':variant==='detail'?'day-popup':variant==='menu'?'input-sheet':'month-picker'} className={variant==='form'?'modal-layer open approved-native-layer':'approved-native-dialog'} aria-labelledby={titleId} onCancel={event=>{event.preventDefault();if(!busy)onClose();}}>
    {variant==='form'?<section className="dialog">{header}<div id="entry-form"><div className="dialogbody">{children}</div>{footer&&<footer className="dialogfoot">{footer}</footer>}</div></section>:variant==='menu'?<><h2 id={titleId}>{title}</h2>{children}{footer}</>:<>{header}<div id={variant==='detail'?'day-popup-body':undefined} className={variant==='detail'?undefined:'dialogbody'}>{children}</div>{footer&&<footer className="dialogfoot">{footer}</footer>}</>}
  </dialog>;
}
