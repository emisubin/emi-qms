import { FormEvent, useEffect, useMemo, useState } from 'react';
import { ApiError, getAdminAuditEventDetail, getAdminAuditEvents } from './api';
import type { AuditDetail, AuditFilters, AuditList, AuditListItem } from './audit';
import { DsPageHeader } from './design-system';
import { SelectionCheckbox, SelectedExportTray } from './SelectedExcelExport';
import { useSelectedRows } from './useSelectedRows';

type Props = { developmentUserKey: string };
type State = { kind: 'loading' } | { kind: 'ready'; data: AuditList } | { kind: 'error'; message: string };

const kstFormatter = new Intl.DateTimeFormat('ko-KR', {
  timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit'
});

function kstDate(offsetDays = 0) {
  const source = new Date(Date.now() + offsetDays * 86_400_000);
  const parts = new Intl.DateTimeFormat('en', {
    timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit'
  }).formatToParts(source);
  const value = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${value.year}-${value.month}-${value.day}`;
}

const defaultFilters: AuditFilters = {
  from: kstDate(-29), to: kstDate(), domain: '', action: '', eventType: '', failureReason: '', search: '', page: 1, pageSize: 50
};

export function AuditPage({ developmentUserKey }: Props) {
  const [draft, setDraft] = useState(defaultFilters);
  const [filters, setFilters] = useState(defaultFilters);
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [detail, setDetail] = useState<AuditDetail | null>(null);
  const [detailError, setDetailError] = useState('');
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let active = true;
    setState({ kind: 'loading' });
    getAdminAuditEvents(developmentUserKey, filters)
      .then((data) => { if (active) setState({ kind: 'ready', data }); })
      .catch((error: unknown) => {
        if (active) setState({ kind: 'error', message: error instanceof ApiError ? error.message : '전체 감사 이력을 불러올 수 없습니다.' });
      });
    return () => { active = false; };
  }, [developmentUserKey, filters]);

  const items = useMemo(() => state.kind === 'ready' ? state.data.items : [], [state]);
  const visibleIds = useMemo(() => [...new Set(items.map((item) => item.eventId))], [items]);
  const selection = useSelectedRows(visibleIds);
  const totalPages = state.kind === 'ready' ? Math.max(1, Math.ceil(state.data.totalCount / state.data.pageSize)) : 1;

  function submit(event: FormEvent) {
    event.preventDefault();
    selection.clear();
    setFilters({ ...draft, page: 1 });
  }

  function reset() {
    selection.clear();
    setDraft(defaultFilters);
    setFilters(defaultFilters);
  }

  async function openDetail(item: AuditListItem) {
    setDetail(null);
    setDetailError('');
    setDetailLoading(true);
    try {
      setDetail(await getAdminAuditEventDetail(developmentUserKey, item.eventId, item.source));
    } catch (error) {
      setDetailError(error instanceof ApiError ? error.message : '감사 상세를 불러올 수 없습니다.');
    } finally {
      setDetailLoading(false);
    }
  }

  return (
    <section className="panel-section notification-audit-page global-audit-page">
      <DsPageHeader
        className="page-header"
        eyebrow="System · Audit"
        title="전체 감사 이력"
        description="누가 사이트에 들어왔고 실제로 어떤 값을 저장하거나 저장하지 못했는지 확인합니다."
        actions={<button type="button" onClick={() => setFilters({ ...filters })}>새로고침</button>}
      />

      {state.kind === 'ready' ? (
        <>
          <div className="audit-summary-grid" aria-label="감사 이력 요약">
            <article><span>전체</span><strong>{state.data.summary.totalEvents}</strong></article>
            <article><span>로그인</span><strong>{state.data.summary.loginEvents}</strong></article>
            <article><span>사이트 접속</span><strong>{state.data.summary.siteAccessEvents}</strong></article>
            <article><span>저장 완료</span><strong>{state.data.summary.successfulChanges}</strong></article>
            <article><span>저장 실패·권한 거절</span><strong>{state.data.summary.failedChanges + state.data.summary.authorizationDenials}</strong></article>
          </div>
          <p className="audit-identity-notice"><strong>변경·인증 기록 범위</strong> {state.data.coverage.completenessNotice}</p>
          <p className="audit-identity-notice"><strong>사이트 접속 기록 범위</strong> {state.data.coverage.siteAccessCompletenessNotice}</p>
          <p className="audit-identity-notice">{state.data.coverage.lastActivityNotice}</p>
        </>
      ) : null}

      <form className="audit-filter-bar" onSubmit={submit}>
        <label>시작일<input type="date" value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} /></label>
        <label>종료일<input type="date" value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} /></label>
        <label>사건<select value={draft.eventType} onChange={(event) => setDraft({ ...draft, eventType: event.target.value })}>
          <option value="">전체</option><option value="Login">로그인</option><option value="Logout">로그아웃</option>
          <option value="SiteAccess">사이트 접속</option><option value="MutationSucceeded">저장 완료</option><option value="MutationFailed">저장 실패</option><option value="AuthorizationDenied">권한 거절</option>
        </select></label>
        <label>업무영역<select value={draft.domain} onChange={(event) => setDraft({ ...draft, domain: event.target.value })}>
          <option value="">전체</option><option value="Projects">프로젝트</option><option value="ProductionPlanning">생산관리</option>
          <option value="Procurement">구매</option><option value="Materials">자재</option><option value="Manufacturing">제조</option>
          <option value="Quality">품질</option><option value="Logistics">물류</option><option value="Pending">Pending</option>
          <option value="Administration">관리자</option><option value="G2">G2</option><option value="Identity">로그인</option><option value="Authorization">권한</option>
        </select></label>
        <label>실패 종류<select value={draft.failureReason} onChange={(event) => setDraft({ ...draft, failureReason: event.target.value })}>
          <option value="">전체</option><option value="Validation">입력값 확인</option><option value="Conflict">동시 수정·상태 충돌</option>
        </select></label>
        <label>행동<input maxLength={120} value={draft.action} onChange={(event) => setDraft({ ...draft, action: event.target.value })} placeholder="예: UpdateProject" /></label>
        <label className="audit-search-field">사용자·부서·대상 검색<input maxLength={100} value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} /></label>
        <div className="button-row"><button type="submit">조회</button><button type="button" className="secondary-button" onClick={reset}>초기화</button></div>
      </form>

      {state.kind === 'loading' ? <p role="status">전체 감사 이력을 불러오는 중입니다.</p> : null}
      {state.kind === 'error' ? <p role="alert" className="error-text">{state.message}</p> : null}
      {state.kind === 'ready' ? (
        <>
          <SelectedExportTray
            developmentUserKey={developmentUserKey}
            screen="admin-audit-events"
            visibleIds={visibleIds}
            selectedIds={selection.selectedIds}
            allSelected={selection.allSelected}
            busy={selection.busy}
            filters={{ from: filters.from, to: filters.to, domain: filters.domain || undefined, eventType: filters.eventType || undefined }}
            onBusyChange={selection.setBusy}
            onToggleAll={selection.toggleAll}
            onClear={selection.clear}
          />
          {items.length === 0 ? <div className="empty-state"><strong>조건에 맞는 감사 이력이 없습니다.</strong></div> : (<>
            <div className="audit-desktop-table table-scroll">
              <table className="admin-table">
                <thead><tr><th>선택</th><th>최초 발생</th><th>마지막 활동</th><th>사용자</th><th>사건</th><th>업무영역·행동</th><th>접속 메뉴</th><th>대상</th><th>결과</th><th>변경</th></tr></thead>
                <tbody>{items.map((item) => (
                  <tr key={`${item.source}:${item.eventId}`}>
                    <td><SelectionCheckbox checked={selection.selectedIds.has(item.eventId)} disabled={selection.busy} label={`${item.actorDisplayName} 감사 이력 선택`} onChange={(checked) => selection.toggle(item.eventId, checked)} /></td>
                    <td><button type="button" className="table-link-button" onClick={() => void openDetail(item)}>{kstFormatter.format(new Date(item.occurredAtUtc))}</button></td>
                    <td>{item.lastActivityAtUtc ? kstFormatter.format(new Date(item.lastActivityAtUtc)) : '-'}</td>
                    <td><strong>{item.actorDisplayName}</strong><br /><small>{item.actorDepartmentName ?? '부서 없음'}</small>{item.actualActorDisplayName ? <><br /><small>실제 사용자: {item.actualActorDisplayName}</small></> : null}</td>
                    <td>{eventTypeLabel(item.eventType)}</td>
                    <td>{domainLabel(item.domain)}<br /><small>{item.action}</small></td>
                    <td>{item.eventType === 'SiteAccess' ? siteAccessMenuSummary(item) : '-'}</td>
                    <td>{item.targetKey ?? '-'}</td>
                    <td>{outcomeLabel(item)}</td>
                    <td>{item.changeCount}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
            <div className="audit-mobile-list" aria-label="감사 이력 모바일 목록">{items.map((item) => (
              <article key={`${item.source}:${item.eventId}`} className="audit-mobile-card">
                <div className="audit-mobile-card__top">
                  <SelectionCheckbox checked={selection.selectedIds.has(item.eventId)} disabled={selection.busy} label={`${item.actorDisplayName} 감사 이력 선택`} onChange={(checked) => selection.toggle(item.eventId, checked)} />
                  <span>{kstFormatter.format(new Date(item.occurredAtUtc))}</span>
                  <strong>{eventTypeLabel(item.eventType)}</strong>
                </div>
                {item.lastActivityAtUtc ? <small>마지막 활동 {kstFormatter.format(new Date(item.lastActivityAtUtc))}</small> : null}
                <h3>{item.actorDisplayName} <span>{outcomeLabel(item)}</span></h3>
                {item.actualActorDisplayName ? <small>실제 사용자: {item.actualActorDisplayName}</small> : null}
                <p>{domainLabel(item.domain)} · {item.action}</p>
                {item.eventType === 'SiteAccess' ? <small>접속 메뉴 {siteAccessMenuSummary(item)}</small> : null}
                <div><small>{item.targetKey ?? '대상 없음'} · 변경 {item.changeCount}건</small><button type="button" className="table-link-button" onClick={() => void openDetail(item)}>상세 보기</button></div>
              </article>
            ))}</div>
          </>)}
          <nav className="audit-pagination" aria-label="감사 이력 페이지">
            <button type="button" disabled={filters.page <= 1} onClick={() => { selection.clear(); setFilters({ ...filters, page: filters.page - 1 }); }}>이전</button>
            <span>{filters.page} / {totalPages} · 전체 {state.data.totalCount}건</span>
            <button type="button" disabled={filters.page >= totalPages} onClick={() => { selection.clear(); setFilters({ ...filters, page: filters.page + 1 }); }}>다음</button>
            <select aria-label="페이지당 표시 건수" value={filters.pageSize} onChange={(event) => { selection.clear(); setFilters({ ...filters, page: 1, pageSize: Number(event.target.value) }); }}><option value={20}>20개</option><option value={50}>50개</option><option value={100}>100개</option></select>
          </nav>
        </>
      ) : null}

      {(detailLoading || detail || detailError) ? (
        <aside className="audit-detail-panel" aria-label="감사 사건 상세">
          <div className="subsection-header"><h3>감사 사건 상세</h3><button type="button" onClick={() => { setDetail(null); setDetailError(''); setDetailLoading(false); }}>닫기</button></div>
          {detailLoading ? <p role="status">상세를 불러오는 중입니다.</p> : null}
          {detailError ? <p role="alert" className="error-text">{detailError}</p> : null}
          {detail ? <AuditDetailView detail={detail} /> : null}
        </aside>
      ) : null}
    </section>
  );
}

function AuditDetailView({ detail }: { detail: AuditDetail }) {
  const item = detail.event;
  return <>
    <dl className="audit-detail-summary">
      <div><dt>발생일시</dt><dd>{kstFormatter.format(new Date(item.occurredAtUtc))}</dd></div>
      <div><dt>사용자</dt><dd>{item.actorDisplayName} · {item.actorDepartmentName ?? '부서 없음'}</dd></div>
      {item.actualActorDisplayName ? <div><dt>실제 사용자</dt><dd>{item.actualActorDisplayName}</dd></div> : null}
      <div><dt>사건</dt><dd>{eventTypeLabel(item.eventType)} · {outcomeLabel(item)}</dd></div>
      <div><dt>업무</dt><dd>{domainLabel(item.domain)} · {item.action}</dd></div>
      <div><dt>대상</dt><dd>{item.targetType ?? '-'} · {item.targetKey ?? '-'}</dd></div>
      {item.eventType === 'SiteAccess' ? <>
        <div><dt>최초 접속</dt><dd>{kstFormatter.format(new Date(item.occurredAtUtc))}</dd></div>
        <div><dt>마지막 활동</dt><dd>{item.lastActivityAtUtc ? kstFormatter.format(new Date(item.lastActivityAtUtc)) : '-'}</dd></div>
        <div><dt>종료</dt><dd>{item.endedAtUtc ? kstFormatter.format(new Date(item.endedAtUtc)) : '-'}</dd></div>
        <div><dt>접속 상태</dt><dd>{siteAccessStatusLabel(item.siteAccessStatus)}</dd></div>
        <div><dt>접속 메뉴</dt><dd>{item.menuLabels.length > 0 ? item.menuLabels.join(' → ') : '-'}</dd></div>
        <div><dt>앱 접근 결과</dt><dd>{appAccessOutcomeLabel(item.appAccessOutcome)}</dd></div>
        <div><dt>접속 환경</dt><dd>{item.clientIp ?? '-'} · {item.browserFamily ?? '-'} · {item.osFamily ?? '-'}</dd></div>
      </> : null}
      {item.eventType === 'Login' ? <div><dt>로그인 환경</dt><dd>{item.clientIp ?? '-'} · {item.browserFamily ?? '-'} · {item.osFamily ?? '-'}</dd></div> : null}
      {item.eventType !== 'Login' && detail.loginContext ? <div><dt>연결 로그인</dt><dd>{kstFormatter.format(new Date(detail.loginContext.occurredAtUtc))} · {detail.loginContext.clientIp ?? '-'} · {detail.loginContext.browserFamily ?? '-'} · {detail.loginContext.osFamily ?? '-'}</dd></div> : null}
      {item.source === 'Global' && item.eventType !== 'Login' && !detail.loginContext ? <div><dt>연결 로그인</dt><dd>로그인 연결 없음</dd></div> : null}
    </dl>
    <p className="muted-text">{detail.valueNotice}</p>
    {detail.changes.length > 0 ? <div className="table-scroll"><table className="admin-table">
      <thead><tr><th>대상</th><th>항목</th><th>처리</th><th>변경 전</th><th>변경 후</th></tr></thead>
      <tbody>{detail.changes.map((change) => <tr key={change.changeId}>
        <td>{change.targetType}<br /><small>{change.targetKey}</small></td><td>{change.fieldCode}</td><td>{change.rowAction}</td>
        <td>{change.projectionKind === 'ExactScalar' ? change.beforeValue ?? '-' : `${change.beforeLength ?? 0}자`}</td>
        <td>{change.projectionKind === 'ExactScalar' ? change.afterValue ?? '-' : `${change.afterLength ?? 0}자`}</td>
      </tr>)}</tbody>
    </table></div> : <p>이 사건에는 표시할 항목별 변경이 없습니다.</p>}
  </>;
}

function eventTypeLabel(value: AuditListItem['eventType']) {
  return ({ Login: '로그인', Logout: '로그아웃', SiteAccess: '사이트 접속', MutationSucceeded: '저장 완료', MutationFailed: '저장 실패', AuthorizationDenied: '권한 거절' } as const)[value];
}

function domainLabel(value: string) {
  return ({ Projects: '프로젝트', ProductionPlanning: '생산관리', Procurement: '구매', Materials: '자재', Manufacturing: '제조', Quality: '품질', Logistics: '물류', Pending: 'Pending', Administration: '관리자', G2: 'G2', Identity: '인증·접속', Authorization: '권한' } as Record<string, string>)[value] ?? value;
}

function outcomeLabel(item: AuditListItem) {
  if (item.eventType === 'SiteAccess') return siteAccessStatusLabel(item.siteAccessStatus);
  if (item.failureReason === 'Validation') return '입력값 확인 실패';
  if (item.failureReason === 'Conflict') return '동시 수정·상태 충돌';
  if (item.eventType === 'AuthorizationDenied') return '권한 거절';
  if (item.appAccessOutcome === 'ApprovalPending') return '승인 대기';
  if (item.appAccessOutcome === 'Inactive') return '비활성 계정';
  return item.outcome === 'Succeeded' ? '완료' : item.outcome === 'Ended' ? '종료' : '거절';
}

function siteAccessStatusLabel(value: AuditListItem['siteAccessStatus']) {
  return value === 'RecentSignal' ? '최근 활동'
    : value === 'ExplicitLogout' ? '직접 로그아웃'
      : value === 'TimedOut' ? '30분 경과'
        : '-';
}

function appAccessOutcomeLabel(value: AuditListItem['appAccessOutcome']) {
  return value === 'Allowed' ? '허용'
    : value === 'ApprovalPending' ? '승인 대기'
      : value === 'Inactive' ? '비활성 계정'
        : '-';
}

function siteAccessMenuSummary(item: AuditListItem) {
  if (item.menuLabels.length <= 3) return item.menuLabels.join(' → ') || '-';
  return `${item.menuLabels.slice(0, 3).join(' → ')} 외 ${item.menuLabels.length - 3}개`;
}
