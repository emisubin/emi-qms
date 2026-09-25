import { NavigationIcon } from './NavigationIcon';
import { useEffect, useRef, useState } from 'react';
import { OsanQrScanner } from './OsanQrScanner';
import './osan-mobile-tools.css';

export type OsanMobileDestination = 'home' | 'list' | 'osan-progress' | 'notifications' | 'notice-board' | 'osan-customer-admin' | 'osan-gate-settings' | 'osan-gate-approvals';
const primaryDestinations = [['home', '홈'], ['notifications', '알림'], ['notice-board', '공지사항'], ['list', '프로젝트'], ['osan-progress', '진행 현황']] as const;
const adminDestinations = [['osan-customer-admin', '고객사 관리'], ['osan-gate-settings', 'Gate 설정'], ['osan-gate-approvals', 'Gate 승인 대기']] as const;
function MenuIcon({ name }: { name: string }) {
  const paths: Record<string, string> = { scan: 'M8 3H3v5M16 3h5v5M3 16v5h5M21 16v5h-5M8 8h8v8H8Z', menu: 'M4 6h16M4 12h16M4 18h16', close: 'm6 6 12 12M6 18 18 6' };
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>;
}
export function OsanMobileTools({ current, onNavigate, onScan, admin = false }: { current: string; admin?: boolean; onNavigate: (destination: OsanMobileDestination) => void; onScan: (projectId: string, targetId: string) => void }) {
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
        {primaryDestinations.map(([key, label]) => <button type="button" key={key} aria-current={current === key ? 'page' : undefined} onClick={() => { close(); onNavigate(key); }}><NavigationIcon label={label}/>{label}</button>)}
        <button type="button" onClick={() => { setOpen(false); setScanning(true); }}><MenuIcon name="scan"/>QR 스캔</button>
        {admin && <div className="osan-mobile-admin-group"><span>관리자</span>{adminDestinations.map(([key, label]) => <button type="button" key={key} aria-current={current === key ? 'page' : undefined} onClick={() => { close(); onNavigate(key); }}><NavigationIcon label={label}/>{label}</button>)}</div>}
      </nav>}
      <button ref={trigger} type="button" className="osan-menu-fab" aria-label={open ? '메뉴 닫기' : '메뉴 열기'} aria-expanded={open} aria-controls="osan-floating-navigation" onClick={() => setOpen(value => !value)}><MenuIcon name={open ? 'close' : 'menu'} /></button>
    </div>
    {scanning && <OsanQrScanner onClose={() => { setScanning(false); trigger.current?.focus(); }} onScan={(projectId, targetId) => { setScanning(false); onScan(projectId, targetId); }} />}
  </>;
}
