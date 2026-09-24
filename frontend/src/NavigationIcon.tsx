export function NavigationIcon({ label }: { label: string }) {
  const common = {
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true
  };

  switch (label) {
    case '홈':
      return <svg {...common}><path d="m3.5 10 8.5-7 8.5 7" /><path d="M5.5 9.5V21h13V9.5" /><path d="M9.5 21v-6h5v6" /></svg>;
    case '공지사항':
      return <svg {...common}><path d="m3 11 18-5v12L3 14v-3ZM7 15l1 6h3l-2-5M21 3v18" /></svg>;
    case '진행 현황':
      return <svg {...common}><path d="M5 20v-5M12 20V9M19 20V3" /></svg>;
    case '내 업무':
      return <svg {...common}><rect x="5" y="3" width="14" height="18" rx="2" /><path d="M9 3.5h6v3H9zM9 11h6M9 15h4" /></svg>;
    case '프로젝트':
      return <svg {...common}><path d="M3 7.5h7l2 2h9v10.5H3z" /><path d="M3 7.5V5h7l2 2h7" /></svg>;
    case 'Pending':
      return <svg {...common}><path d="M12 3 2.8 20h18.4z" /><path d="M12 9v5M12 17.5h.01" /></svg>;
    case '생산관리':
      return <svg {...common}><path d="M4 19V8l5 3V8l5 3V5h6v14z" /><path d="M7 15h2M12 15h2M17 15h1" /></svg>;
    case '구매':
      return <svg {...common}><path d="M3 5h2l2 11h10l2-8H6" /><circle cx="9" cy="20" r="1" /><circle cx="17" cy="20" r="1" /></svg>;
    case '자재':
      return <svg {...common}><path d="m4 7 8-4 8 4-8 4z" /><path d="m4 7 8 4 8-4v10l-8 4-8-4zM12 11v10" /></svg>;
    case '제조':
      return <svg {...common}><circle cx="12" cy="12" r="3" /><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9 7 7M17 17l2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1" /></svg>;
    case '품질':
      return <svg {...common}><path d="m12 3 7 3v5c0 4.6-2.8 8-7 10-4.2-2-7-5.4-7-10V6z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>;
    case '물류':
      return <svg {...common}><path d="M3 6h11v10H3zM14 9h4l3 3v4h-7z" /><circle cx="7" cy="18" r="2" /><circle cx="18" cy="18" r="2" /></svg>;
    case '알림':
      return <svg {...common}><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" /></svg>;
    case '고객사 관리':
      return <svg {...common}><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M18 8v6M15 11h6" /></svg>;
    case 'Gate 설정':
      return <svg {...common}><path d="M4 6h16M4 12h16M4 18h16M8 3v6M16 9v6M10 15v6" /></svg>;
    case 'Gate 승인 대기':
      return <svg {...common}><path d="M5 3h14v18H5ZM8 12l3 3 5-6" /></svg>;
    case '관리자':
      return <svg {...common}><circle cx="12" cy="8" r="4" /><path d="M4 21c.8-4.2 3.4-6.5 8-6.5s7.2 2.3 8 6.5" /><path d="m17.5 4.5 1 1 2-2" /></svg>;
    default:
      return <svg {...common}><circle cx="12" cy="12" r="8" /></svg>;
  }
}
