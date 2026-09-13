import { useEffect, useRef, useState } from 'react';
import { OsanQrScanner } from './OsanQrScanner';
import './osan-mobile-tools.css';

export type OsanMobileDestination = 'home' | 'list' | 'osan-progress' | 'notifications';
function MenuIcon({ name }: { name: string }) {
  const paths: Record<string, string> = { home: 'M3 10 12 3 21 10M5 9v12h14V9M9 21v-7h6v7', list: 'M3 7h7l2 2h9v11H3ZM3 7V4h7l2 3h7', 'osan-progress': 'm3 6 2 2 3-4M11 6h10M3 13l2 2 3-4M11 13h10M11 20h10', notifications: 'M6 8a6 6 0 0 1 12 0c0 7 3 7 3 9H3c0-2 3-2 3-9M10 21h4', scan: 'M8 3H3v5M16 3h5v5M3 16v5h5M21 16v5h-5M8 8h8v8H8Z', menu: 'M4 6h16M4 12h16M4 18h16', close: 'm6 6 12 12M6 18 18 6' };
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>;
}
export function OsanMobileTools({ current, onNavigate, onScan }: { current: string; onNavigate: (destination: OsanMobileDestination) => void; onScan: (projectId: string, targetId: string) => void }) {
  const [open, setOpen] = useState(false);
  const [scanning, setScanning] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLElement>(null);
  const close = () => { setOpen(false); trigger.current?.focus(); };
  useEffect(() => { if (open) menu.current?.querySelector('button')?.focus(); }, [open]);
  return <>
    {open && <button type="button" className="osan-fab-shade" aria-label="메뉴 바깥을 눌러 닫기" onClick={close} />}
    <div className="osan-mobile-tools" onKeyDown={event => {
      if (event.key === 'Escape') { event.preventDefault(); close(); }
      if (event.key === 'Tab' && open) {
        const buttons = [...(menu.current?.querySelectorAll('button') ?? []), trigger.current].filter((item): item is HTMLButtonElement => !!item);
        if (event.shiftKey && document.activeElement === buttons[0]) { event.preventDefault(); trigger.current?.focus(); }
        else if (!event.shiftKey && document.activeElement === trigger.current) { event.preventDefault(); buttons[0]?.focus(); }
      }
    }}>
      {open && <nav ref={menu} id="osan-floating-navigation" aria-label="빠른 이동">
        {([['home', '홈'], ['list', '프로젝트'], ['osan-progress', '진행 현황'], ['notifications', '알림']] as const).map(([key, label]) => <button type="button" key={key} aria-current={current === key ? 'page' : undefined} onClick={() => { close(); onNavigate(key); }}><MenuIcon name={key}/>{label}</button>)}
        <button type="button" onClick={() => { setOpen(false); setScanning(true); }}><MenuIcon name="scan"/>QR 스캔</button>
      </nav>}
      <button ref={trigger} type="button" className="osan-menu-fab" aria-label={open ? '메뉴 닫기' : '메뉴 열기'} aria-expanded={open} aria-controls="osan-floating-navigation" onClick={() => setOpen(value => !value)}><MenuIcon name={open ? 'close' : 'menu'} /></button>
    </div>
    {scanning && <OsanQrScanner onClose={() => { setScanning(false); trigger.current?.focus(); }} onScan={(projectId, targetId) => { setScanning(false); onScan(projectId, targetId); }} />}
  </>;
}
