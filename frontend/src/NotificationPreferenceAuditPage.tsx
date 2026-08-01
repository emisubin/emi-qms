import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, getAdminNotificationPreferenceAudit } from './api';
import { DsPageHeader } from './design-system';
import type {
  NotificationPreferenceAuditFilters,
  NotificationPreferenceAuditList
} from './notificationPreferenceAudit';
import { SelectionCheckbox, SelectedExportTray } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type Props = {
  developmentUserKey: string;
};

type PageState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: NotificationPreferenceAuditList }
  | { kind: 'error'; message: string };

const kstFormatter = new Intl.DateTimeFormat('ko-KR', {
  timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit'
});

function kstDate(offsetDays = 0) {
  const source = new Date(Date.now() + offsetDays * 86_400_000);
  const parts = new Intl.DateTimeFormat('en', {
    timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit'
  }).formatToParts(source);
  const value = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${value.year}-${value.month}-${value.day}`;
}

const defaultFilters: NotificationPreferenceAuditFilters = {
  from: kstDate(-29),
  to: kstDate(),
  action: '',
  deliveryType: '',
  search: '',
  page: 1,
  pageSize: 50
};

export function NotificationPreferenceAuditPage({ developmentUserKey }: Props) {
  const [draft, setDraft] = useState(defaultFilters);
  const [filters, setFilters] = useState(defaultFilters);
  const [state, setState] = useState<PageState>({ kind: 'loading' });

  const load = useCallback(async (nextFilters: NotificationPreferenceAuditFilters) => {
    setState({ kind: 'loading' });
    try {
      setState({ kind: 'ready', data: await getAdminNotificationPreferenceAudit(developmentUserKey, nextFilters) });
    } catch (error) {
      setState({
        kind: 'error',
        message: error instanceof ApiError ? error.message : '알림 설정 변경 이력을 불러올 수 없습니다.'
      });
    }
  }, [developmentUserKey]);

  useEffect(() => {
    let active = true;
    getAdminNotificationPreferenceAudit(developmentUserKey, filters)
      .then((data) => {
        if (active) setState({ kind: 'ready', data });
      })
      .catch((error: unknown) => {
        if (!active) return;
        setState({
          kind: 'error',
          message: error instanceof ApiError ? error.message : '알림 설정 변경 이력을 불러올 수 없습니다.'
        });
      });
    return () => {
      active = false;
    };
  }, [developmentUserKey, filters]);

  const items = useMemo(() => state.kind === 'ready' ? state.data.items : [], [state]);
  const visibleIds = useMemo(() => items.map((item) => item.auditEventId), [items]);
  const selection = useSelectedRows(visibleIds);
  const totalPages = state.kind === 'ready'
    ? Math.max(1, Math.ceil(state.data.totalCount / state.data.pageSize))
    : 1;

  function submit(event: FormEvent) {
    event.preventDefault();
    selection.clear();
    setState({ kind: 'loading' });
    setFilters({ ...draft, page: 1 });
  }

  function reset() {
    selection.clear();
    const nextFilters = { ...defaultFilters };
    setDraft(nextFilters);
    setState({ kind: 'loading' });
    setFilters(nextFilters);
  }

  return (
    <section className="panel-section notification-audit-page">
      <DsPageHeader
        className="page-header"
        eyebrow="System · Audit"
        title="알림 설정 변경 이력"
        description="누가 누구의 알림을 언제 켜거나 껐는지 확인합니다."
        actions={<button type="button" onClick={() => void load(filters)}>새로고침</button>}
      />

      {state.kind === 'ready' ? (
        <div className="audit-summary-grid" aria-label="변경 이력 요약">
          <article><span>전체 변경</span><strong>{state.data.summary.totalChanges}</strong></article>
          <article><span>사용자 직접</span><strong>{state.data.summary.userChanges}</strong></article>
          <article><span>관리자 대리</span><strong>{state.data.summary.adminChanges}</strong></article>
          <article><span>알림 끔 전환</span><strong>{state.data.summary.turnedOffChanges}</strong></article>
        </div>
      ) : null}

      <form className="audit-filter-bar" onSubmit={submit}>
        <label>시작일<input type="date" value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} /></label>
        <label>종료일<input type="date" value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} /></label>
        <label>행동<select value={draft.action} onChange={(event) => setDraft({ ...draft, action: event.target.value })}>
          <option value="">전체</option><option value="Save">사용자 직접 변경</option><option value="Reset">사용자 기본값 복원</option>
          <option value="AdminSave">관리자 대리 변경</option><option value="AdminReset">관리자 기본값 복원</option>
        </select></label>
        <label>알림 종류<select value={draft.deliveryType} onChange={(event) => setDraft({ ...draft, deliveryType: event.target.value })}>
          <option value="">전체</option><option value="WorkItemCreated">업무 배정</option>
          <option value="DueSoonL0">예정일 임박 D-1</option><option value="DailyDigest">일일 업무 요약</option>
        </select></label>
        <label className="audit-search-field">사용자·부서 검색<input maxLength={100} placeholder="이름 또는 부서" value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} /></label>
        <div className="button-row"><button type="submit">조회</button><button type="button" className="secondary-button" onClick={reset}>초기화</button></div>
      </form>

      {state.kind === 'ready' ? (
        <p className="audit-identity-notice"><strong>표시 기준</strong> {state.data.identityNotice}</p>
      ) : null}
      {state.kind === 'loading' ? <p role="status">변경 이력을 불러오는 중입니다.</p> : null}
      {state.kind === 'error' ? <p role="alert" className="error-text">{state.message}</p> : null}

      {state.kind === 'ready' ? (
        <>
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-notification-preference-audit"
            visibleIds={visibleIds}
            selectedIds={selection.selectedIds}
            allSelected={selection.allSelected}
            busy={selection.busy}
            filters={{ from: filters.from, to: filters.to, action: filters.action || undefined, deliveryType: filters.deliveryType || undefined, search: filters.search || undefined }}
            onBusyChange={selection.setBusy}
            onToggleAll={selection.toggleAll}
            onClear={selection.clear}
          />
          {items.length === 0 ? (
            <div className="empty-state"><strong>조건에 맞는 변경 이력이 없습니다.</strong><button type="button" onClick={reset}>필터 초기화</button></div>
          ) : (
            <>
              <div className="audit-desktop-table table-scroll">
                <table className="admin-table">
                  <thead><tr><th>선택</th><th>변경일시</th><th>대상 사용자</th><th>변경 주체</th><th>행동</th><th>알림 종류</th><th>변경 결과</th><th>버전</th></tr></thead>
                  <tbody>{items.map((item) => (
                    <tr key={item.auditEventId}>
                      <td><SelectionCheckbox checked={selection.selectedIds.has(item.auditEventId)} disabled={selection.busy} label={`${item.targetDisplayName} 변경 이력 선택`} onChange={(checked) => selection.toggle(item.auditEventId, checked)} /></td>
                      <td>{kstFormatter.format(new Date(item.occurredAtUtc))}</td>
                      <td><strong>{item.targetDisplayName}</strong><br /><small>{item.targetDepartmentName ?? '부서 없음'} · {item.targetIsActive ? '활성' : '비활성'}</small></td>
                      <td>{item.actorDisplayName}<br /><small>{item.actorDepartmentName ?? '부서 없음'}</small></td>
                      <td><span className={`audit-action-pill${item.action.startsWith('Admin') ? ' audit-action-pill--admin' : ''}`}>{item.actionLabel}</span></td>
                      <td>{item.deliveryTypeLabel}<br /><small>{item.channelLabel}</small></td>
                      <td><strong className={item.newValue ? 'success-text' : 'error-text'}>{item.changeLabel}</strong></td>
                      <td>v{item.resultingVersion}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
              <div className="audit-mobile-list">{items.map((item) => (
                <article key={item.auditEventId} className="audit-mobile-card">
                  <div className="audit-mobile-card__top"><SelectionCheckbox checked={selection.selectedIds.has(item.auditEventId)} disabled={selection.busy} label={`${item.targetDisplayName} 변경 이력 선택`} onChange={(checked) => selection.toggle(item.auditEventId, checked)} /><span>{kstFormatter.format(new Date(item.occurredAtUtc))}</span><strong className={item.newValue ? 'success-text' : 'error-text'}>{item.changeLabel}</strong></div>
                  <h3>{item.actorDisplayName} <span>→</span> {item.targetDisplayName}</h3>
                  <p>{item.deliveryTypeLabel} · {item.channelLabel}</p>
                  <div><span className={`audit-action-pill${item.action.startsWith('Admin') ? ' audit-action-pill--admin' : ''}`}>{item.actionLabel}</span><small>{item.targetDepartmentName ?? '부서 없음'} · v{item.resultingVersion}</small></div>
                </article>
              ))}</div>
            </>
          )}
          <nav className="audit-pagination" aria-label="변경 이력 페이지">
            <button type="button" disabled={filters.page <= 1} onClick={() => { selection.clear(); setState({ kind: 'loading' }); setFilters({ ...filters, page: filters.page - 1 }); }}>이전</button>
            <span>{filters.page} / {totalPages} · 전체 {state.data.totalCount}건</span>
            <button type="button" disabled={filters.page >= totalPages} onClick={() => { selection.clear(); setState({ kind: 'loading' }); setFilters({ ...filters, page: filters.page + 1 }); }}>다음</button>
            <select aria-label="페이지당 표시 건수" value={filters.pageSize} onChange={(event) => { selection.clear(); setState({ kind: 'loading' }); setFilters({ ...filters, page: 1, pageSize: Number(event.target.value) }); }}><option value={20}>20개</option><option value={50}>50개</option><option value={100}>100개</option></select>
          </nav>
        </>
      ) : null}
    </section>
  );
}
