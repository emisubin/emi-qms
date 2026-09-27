import { Fragment, useEffect, useMemo, useRef, useState } from 'react';
import { exportOsanNotificationsExcel, getNotificationSummary, listNotifications, markAllNotificationsRead, markNotificationRead } from './api';
import type { NotificationItem } from './projects';
import { OsanMenuHeading } from './OsanMenuHeading';
import { OsanFilterToolbar } from './OsanFilterToolbar';
import { OsanMultiSelectFilter } from './OsanMultiSelectFilter';
import { OsanButton } from './OsanButton';
import { SelectedExportTray, SelectionCheckbox } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';
import { osanNotificationKinds as kinds, osanNotificationPresentation } from './osanNotificationPresentation';
import './OsanNotificationsPage.css';

const iconModules = import.meta.glob('./assets/notification-icons/*.svg', { eager: true, query: '?raw', import: 'default' }) as Record<string, string>;
const icons = ['folder-plus', 'clipboard-check', 'undo-2', 'file-pen-line', 'triangle-alert', 'wrench', 'badge-check', 'user-round-check'].map(name => iconModules[`./assets/notification-icons/${name}.svg`]);
type Tab = 'unread' | 'read' | 'all';
type Snapshot = { tab: Tab; query: string; kinds: string[]; scroll: number };
const snapshots = new Map<string, Snapshot>();
const day = (date: string) => new Intl.DateTimeFormat('ko-KR', { timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(date));
const time = (date: string) => new Intl.DateTimeFormat('ko-KR', { timeZone: 'Asia/Seoul', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(date));
export function OsanNotificationsPage({ developmentUserKey, scopeKey, onOpen, onBadgeRefresh }: {
  developmentUserKey: string | undefined; scopeKey: string;
  onOpen: (projectId: string, linkUrl: string | null) => void; onBadgeRefresh: () => void;
}) {
  const [initial] = useState(() => snapshots.get(scopeKey));
  const [tab, setTab] = useState<Tab>(initial?.tab ?? 'unread');
  const [query, setQuery] = useState(initial?.query ?? '');
  const [selectedKinds, setSelectedKinds] = useState<string[]>(initial?.kinds ?? []);
  const [searchDraft, setSearchDraft] = useState(initial?.query ?? '');
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unread, setUnread] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const opening = useRef(false);
  const mounted = useRef(false);
  const restored = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    let cancelled = false;
    setLoading(true); setItems([]); setError('');
    Promise.all([listNotifications(developmentUserKey, tab === 'all' ? undefined : tab), getNotificationSummary(developmentUserKey)]).then(([result, summary]) => {
      if (cancelled) return;
      setItems(result.items); setUnread(summary.unreadCount); setLoading(false);
    }).catch(() => { if (!cancelled) { setError('알림을 불러오지 못했습니다. 다시 시도해 주세요.'); setLoading(false); } });
    return () => { cancelled = true; };
  }, [developmentUserKey, scopeKey, tab, attempt]);
  useEffect(() => {
    if (!loading && !restored.current) {
      restored.current = true;
      const handle = requestAnimationFrame(() => window.scrollTo(0, initial?.scroll ?? 0));
      return () => cancelAnimationFrame(handle);
    }
  }, [loading, initial]);
  const visible = useMemo(() => items.filter(item => {
    const presentation = osanNotificationPresentation(item);
    return (selectedKinds.length === 0 || selectedKinds.includes(String(presentation.kind))) && [item.projectTitle, item.projectItem, item.title, item.message].join(' ').toLowerCase().includes(query.trim().toLowerCase());
  }), [items, query, selectedKinds]);
  const ids = visible.map(item => item.notificationId);
  const selection = useSelectedRows(ids);
  async function open(item: NotificationItem) {
    if (opening.current || busy) return;
    if (!item.projectId) { setError('연결된 프로젝트를 확인할 수 없습니다.'); return; }
    opening.current = true; setError('');
    try {
      if (!item.readAtUtc) await markNotificationRead(developmentUserKey, item.notificationId);
      if (!mounted.current) return;
      snapshots.set(scopeKey, { tab, query, kinds: selectedKinds, scroll: window.scrollY });
      onBadgeRefresh(); onOpen(item.projectId, item.linkUrl);
    } catch { if (mounted.current) setError('읽음 처리에 실패했습니다. 다시 시도해 주세요.'); }
    finally { opening.current = false; }
  }
  async function readAll() {
    if (busy || opening.current) return;
    setBusy(true); setError('');
    try { await markAllNotificationsRead(developmentUserKey); if (mounted.current) { onBadgeRefresh(); setAttempt(value => value + 1); } }
    catch { if (mounted.current) setError('전체 읽음 처리에 실패했습니다. 다시 시도해 주세요.'); }
    finally { if (mounted.current) setBusy(false); }
  }
  return <section className="osan-page osan-notification-feed">
    <OsanMenuHeading title="알림" description="프로젝트와 공정의 새로운 소식을 확인하세요." actions={<><OsanButton onClick={() => setAttempt(value => value + 1)} disabled={busy || loading}>새로고침</OsanButton><OsanButton onClick={() => void readAll()} disabled={busy || loading}>전체 읽음</OsanButton></>} />
    <div className="onf-tabs" role="tablist" aria-label="알림 읽음 상태">{(['unread', 'read', 'all'] as const).map(value => <button key={value} type="button" role="tab" aria-selected={tab === value} onClick={() => setTab(value)}>{value === 'unread' ? '읽지 않음' : value === 'read' ? '읽음' : '전체'}{value === 'unread' && <span>{unread}</span>}</button>)}</div>
    <OsanFilterToolbar search={searchDraft} onSearchChange={setSearchDraft} onSearch={() => setQuery(searchDraft)} searchLabel="알림 검색" placeholder="프로젝트·내용 검색" active={selectedKinds.length > 0} onReset={() => { setSelectedKinds([]); setQuery(''); setSearchDraft(''); }}>
      <OsanMultiSelectFilter label="알림 유형" options={kinds.map((label, index) => ({ value: String(index), label }))} values={selectedKinds} onApply={setSelectedKinds} />
    </OsanFilterToolbar>
    <span className="onf-count">{visible.length}건</span>
    {!loading && items.length > 0 && <SelectedExportTray compact developmentUserKey={developmentUserKey} screen="notifications" label="선택 내보내기" visibleIds={ids} selectedIds={selection.selectedIds} allSelected={selection.allSelected} busy={selection.busy} filters={{ readStatus: tab === 'all' ? undefined : tab }} exportFile={() => exportOsanNotificationsExcel(developmentUserKey, [...selection.selectedIds], tab === 'all' ? undefined : tab)} onBusyChange={selection.setBusy} onToggleAll={selection.toggleAll} onClear={selection.clear} />}
    {error && <div role="alert" className="onf-error">{error} <OsanButton onClick={() => setAttempt(value => value + 1)}>다시 시도</OsanButton></div>}
    {loading ? <p role="status">알림을 불러오는 중입니다.</p> : !error && visible.length === 0 ? <p className="onf-empty">표시할 알림이 없습니다.</p> : visible.map((item, index) => {
      const presentation = osanNotificationPresentation(item);
      return <Fragment key={item.notificationId}>{(index === 0 || day(visible[index - 1].createdAtUtc) !== day(item.createdAtUtc)) && <div className="onf-date">{day(item.createdAtUtc)}</div>}<div className={`onf-row ${item.readAtUtc ? 'is-read' : 'is-unread'}`}>
        <SelectionCheckbox label={`${item.projectTitle ?? item.title} 알림 선택`} checked={selection.selectedIds.has(item.notificationId)} disabled={selection.busy} onChange={checked => selection.toggle(item.notificationId, checked)} />
        <button type="button" className="onf-open" onClick={() => void open(item)}>
          <span className={`onf-icon ${presentation.tone}`} aria-hidden="true"><span dangerouslySetInnerHTML={{ __html: icons[presentation.kind] }} /></span>
          <span className="onf-copy"><span className="onf-title"><strong>{item.projectTitle ?? '프로젝트'}</strong>{item.projectItem && <span>{item.projectItem}</span>}</span><span className="onf-event"><b className={presentation.tone}>{presentation.label}</b>{presentation.stage && <span>{presentation.stage}</span>}</span><span className="onf-excerpt">{presentation.excerpt}</span></span>
          <time className="onf-time" dateTime={item.createdAtUtc}>{!item.readAtUtc && <i aria-label="읽지 않음" />}{time(item.createdAtUtc)}</time>
        </button></div></Fragment>;
    })}
  </section>;
}
