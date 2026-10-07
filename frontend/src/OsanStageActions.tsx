import { OsanButton } from './OsanButton';
import { createContext, useContext, useRef, useState, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { dismissOnBackdrop } from './dialogBackdrop';
import './OsanStageActions.css';

type Placement = 'primary' | 'visible' | 'secondary' | 'management';
const Slots = createContext<{ slots: Record<Placement, HTMLDivElement | null>; close: () => void; compact: boolean } | null>(null);

/** Only the trigger moves; forms, requests and permission checks stay in their owning component. */
export function OsanStageAction({ placement = 'secondary', tone, onClick, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { placement?: Placement; tone?: 'neutral' | 'primary' }) {
  const context = useContext(Slots);
  const slot = context?.compact && (placement === 'visible' || (placement === 'primary' && tone === 'neutral')) ? 'secondary' : placement;
  const handleClick: ButtonHTMLAttributes<HTMLButtonElement>['onClick'] = event => { context?.close(); onClick?.(event); };
  const button = slot === 'primary' || slot === 'visible'
    ? <OsanButton {...props} size="stage" tone={tone ?? (placement === 'primary' ? 'primary' : 'neutral')} onClick={handleClick} />
    : <button {...props} onClick={handleClick} />;
  return context ? (context.slots[slot] ? createPortal(button, context.slots[slot]) : null) : button;
}

export function OsanStageActions({ title, children, compact = false, triggerContainer }: { title: string; children: ReactNode; compact?: boolean; triggerContainer?: HTMLElement | null }) {
  const [primary, setPrimary] = useState<HTMLDivElement | null>(null);
  const [visible, setVisible] = useState<HTMLDivElement | null>(null);
  const [secondary, setSecondary] = useState<HTMLDivElement | null>(null);
  const [management, setManagement] = useState<HTMLDivElement | null>(null);
  const [open, setOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  function close() { dialog.current?.close(); setOpen(false); }
  function show() {
    const menu = dialog.current, anchor = trigger.current;
    if (!menu || !anchor) return;
    const r = anchor.getBoundingClientRect();
    menu.showModal();
    const height = menu.getBoundingClientRect().height;
    menu.style.left = `${Math.max(8, Math.min(r.right - 224, window.innerWidth - 232))}px`;
    menu.style.top = `${r.bottom + height + 8 < window.innerHeight ? r.bottom + 8 : Math.max(8, r.top - height - 8)}px`;
    setOpen(true);
  }
  const moreButton = compact
    ? <button ref={trigger} className="osan-stage-more osan-stage-more--compact" type="button" aria-label={`${title} 더보기`} aria-haspopup="dialog" aria-expanded={open} onClick={show}><span aria-hidden="true">⋮</span></button>
    : <OsanButton size="stage" ref={trigger} className="osan-stage-more" type="button" aria-haspopup="dialog" aria-expanded={open} onClick={show}>더보기 <span aria-hidden="true">⋯</span></OsanButton>;
  return <Slots.Provider value={{ slots: { primary, visible, secondary, management }, close, compact }}>
    <div className="osan-record-actions osan-stage-actions">
      <div className="osan-stage-actionbar">
        <div className="osan-stage-primary" ref={setPrimary}/><div className="osan-stage-visible" ref={setVisible}/>
        {triggerContainer ? createPortal(moreButton, triggerContainer) : moreButton}
      </div>
      <div className="osan-stage-action-details">{children}</div>
      <dialog ref={dialog} className="osan-stage-more-dialog" aria-label={`${title} 더보기`} onCancel={close} onClose={() => setOpen(false)} onClick={event => dismissOnBackdrop(event, close)}>
        <h2>{title} · 더보기</h2><div ref={setSecondary}/><div className="osan-stage-management-menu" ref={setManagement}/>
        <button className="osan-stage-menu-close" type="button" onClick={close}>닫기</button>
      </dialog>
    </div>
  </Slots.Provider>;
}
