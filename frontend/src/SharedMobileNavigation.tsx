import { Fragment, useEffect, useId, useRef, useState, type ReactNode, type RefObject } from 'react';
import { NavigationIcon } from './NavigationIcon';
import './osan-mobile-tools.css';

export interface MobileNavigationItem {
  key: string;
  label: string;
  active?: boolean;
  badge?: number;
  group?: string;
  icon?: ReactNode;
  onSelect?: () => void;
  children?: MobileNavigationItem[];
}

export function MenuIcon({ name }: { name: 'scan' | 'menu' | 'close' }) {
  const paths = { scan: 'M8 3H3v5M16 3h5v5M3 16v5h5M21 16v5h-5M8 8h8v8H8Z', menu: 'M4 6h16M4 12h16M4 18h16', close: 'm6 6 12 12M6 18 18 6' };
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>;
}

/** The approved Osan floating menu, shared by both campuses. Items remain caller-authorized. */
export function SharedMobileNavigation({ items, footer, triggerRef }: {
  items: MobileNavigationItem[];
  footer?: ReactNode;
  triggerRef?: RefObject<HTMLButtonElement | null>;
}) {
  const [open, setOpen] = useState(false);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const ownTrigger = useRef<HTMLButtonElement>(null);
  const trigger = triggerRef ?? ownTrigger;
  const menu = useRef<HTMLElement>(null);
  const menuId = useId();
  const close = () => { setOpen(false); trigger.current?.focus(); };
  useEffect(() => { if (open) menu.current?.querySelector('button')?.focus(); }, [open]);
  function itemButton(item: MobileNavigationItem, child = false) {
    const expanded = expandedKey === item.key;
    return <Fragment key={item.key}>
      <button type="button" className={child ? 'shared-mobile-child' : undefined}
        aria-label={item.badge ? `${item.label} ${item.badge}건` : item.label}
        aria-current={item.active ? 'page' : undefined}
        aria-expanded={item.children ? expanded : undefined}
        aria-controls={item.children ? `${menuId}-${encodeURIComponent(item.key)}` : undefined}
        onClick={() => {
          if (item.children) setExpandedKey(expanded ? null : item.key);
          else { close(); item.onSelect?.(); }
        }}>
        {item.icon ?? <NavigationIcon label={item.label} />}<span>{item.label}</span>
        {item.children ? <span className="shared-mobile-disclosure" aria-hidden="true">{expanded ? '⌃' : '⌄'}</span>
          : !!item.badge && <span className="nav-badge" aria-hidden="true">{item.badge > 99 ? '99+' : item.badge}</span>}
      </button>
      {item.children && expanded && <div id={`${menuId}-${encodeURIComponent(item.key)}`} className="shared-mobile-children">{item.children.map(childItem => itemButton(childItem, true))}</div>}
    </Fragment>;
  }
  return <>
    {open && <button type="button" className="osan-fab-shade" aria-label="메뉴 바깥을 눌러 닫기" onClick={close} />}
    <div className="osan-mobile-tools" onKeyDown={event => {
      if (event.key === 'Escape') { event.preventDefault(); close(); }
      if (event.key === 'Tab' && open) {
        const controls = [...(menu.current?.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex="0"]') ?? []), trigger.current].filter((item): item is HTMLElement => !!item);
        if (event.shiftKey && document.activeElement === controls[0]) { event.preventDefault(); trigger.current?.focus(); }
        else if (!event.shiftKey && document.activeElement === trigger.current) { event.preventDefault(); controls[0]?.focus(); }
      }
    }}>
      {open && <nav ref={menu} id={menuId} aria-label="빠른 이동">
        {items.map((item, index) => <Fragment key={item.key}>
          {item.group && item.group !== items[index - 1]?.group && <div className="osan-mobile-admin-group"><span>{item.group}</span></div>}
          {itemButton(item)}
        </Fragment>)}
        {footer && <footer className="shared-mobile-footer">{footer}</footer>}
      </nav>}
      <button ref={trigger} type="button" className="osan-menu-fab" aria-label={open ? '메뉴 닫기' : '메뉴 열기'} aria-expanded={open} aria-controls={menuId}
        onClick={() => { if (!open) setExpandedKey(null); setOpen(value => !value); }}><MenuIcon name={open ? 'close' : 'menu'} /></button>
    </div>
  </>;
}
