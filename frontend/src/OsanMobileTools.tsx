import { useRef, useState } from 'react';
import { OsanQrScanner } from './OsanQrScanner';
import { MenuIcon, SharedMobileNavigation, type MobileNavigationItem } from './SharedMobileNavigation';

export type OsanMobileDestination = 'home' | 'list' | 'osan-progress' | 'notifications' | 'notice-board' | 'osan-customer-admin' | 'osan-gate-settings' | 'osan-user-permissions';
const primaryDestinations = [['home', '홈'], ['notifications', '알림'], ['notice-board', '공지사항'], ['list', '프로젝트'], ['osan-progress', '진행 현황']] as const;
const adminDestinations = [['osan-customer-admin', '고객사 관리'], ['osan-gate-settings', '부서별 권한 설정'], ['osan-user-permissions', '개인별 권한 설정']] as const;
export function OsanMobileTools({ current, onNavigate, onScan, admin = false }: { current: string; admin?: boolean; onNavigate: (destination: OsanMobileDestination) => void; onScan: (projectId: string, targetId: string) => void }) {
  const [scanning, setScanning] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);
  const items: MobileNavigationItem[] = [
    ...primaryDestinations.map(([key, label]) => ({ key, label, active: current === key, onSelect: () => onNavigate(key) })),
    { key: 'scan', label: 'QR 스캔', icon: <MenuIcon name="scan" />, onSelect: () => setScanning(true) },
    ...(admin ? adminDestinations.map(([key, label]) => ({ key, label, group: '관리자', active: current === key, onSelect: () => onNavigate(key) })) : [])
  ];
  return <>
    <SharedMobileNavigation items={items} triggerRef={trigger} />
    {scanning && <OsanQrScanner onClose={() => { setScanning(false); trigger.current?.focus(); }} onScan={(projectId, targetId) => { setScanning(false); onScan(projectId, targetId); }} />}
  </>;
}
