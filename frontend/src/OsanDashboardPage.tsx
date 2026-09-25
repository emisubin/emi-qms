import { OsanStepper } from './OsanStepper';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { getOsanDashboard, type OsanDashboardResponse } from './osanDashboard';
import { emptyOsanListFilters, readOsanListSnapshot, saveOsanListSnapshot } from './osanListState';
import { OsanListFrame } from './OsanListFrame';
import { formatOsanDday, useKoreaDate, isOsanOverdue } from './osanDday';
import backIcon from './assets/osan-dashboard-back.png';
import forwardIcon from './assets/osan-dashboard-forward.png';
import './osan-dashboard.css';

type State = { kind: 'loading' } | { kind: 'ready'; data: OsanDashboardResponse } | { kind: 'error'; message: string; forbidden: boolean };

export function OsanDashboardPage(props: { view?: 'home' | 'progress'; developmentUserKey?: string; stateScopeKey?: string; onOpen: (projectId: string) => void }) {
  return <Workspace key={`${props.stateScopeKey ?? props.developmentUserKey ?? ''}:${props.view ?? 'progress'}`} {...props} />;
}
function Workspace({ developmentUserKey, stateScopeKey, onOpen, view = 'progress' }: { view?: 'home' | 'progress'; developmentUserKey?: string; stateScopeKey?: string; onOpen: (projectId: string) => void }) {
  const isHome = view === 'home';
  const today = useKoreaDate();
  const snapshotKey = `${stateScopeKey ?? developmentUserKey ?? ''}:dashboard:${view}`;
  const [initial] = useState(() => readOsanListSnapshot(snapshotKey));
  const [draft, setDraft] = useState(initial?.draft ?? '');
  const [query, setQuery] = useState(initial?.filters ?? emptyOsanListFilters());
  const scrollRestored = useRef(false);
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ query: typeof query; state: State }>({ query, state: { kind: 'loading' } });
  const state: State = result.query === query ? result.state : { kind: 'loading' };
  useEffect(() => {
    let current = true;
    const controller = new AbortController();
    getOsanDashboard(developmentUserKey, { ...query, view }, controller.signal).then(data => {
      if (!current) return;
      const lastPage = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
      if (query.page > lastPage) {
        setQuery({ ...query, page: lastPage });
        return;
      }
      setResult({ query, state: { kind: 'ready', data } });
    }).catch((error: unknown) => {
      if (current) setResult({ query, state: { kind: 'error', forbidden: error instanceof ApiError && error.status === 403,
        message: error instanceof Error ? error.message : '진행 현황을 불러오지 못했습니다.' } });
    });
    return () => { current = false; controller.abort(); };
  }, [developmentUserKey, query, attempt, isHome, view]);
  useEffect(() => {
    const refresh = () => setAttempt(value => value + 1);
    const timer = window.setInterval(refresh, 60_000);
    window.addEventListener('focus', refresh);
    return () => { window.clearInterval(timer); window.removeEventListener('focus', refresh); };
  }, [isHome]);
  const data = state.kind === 'ready' ? state.data : undefined;
  useEffect(() => {
    saveOsanListSnapshot(snapshotKey, { filters: query, draft, scrollY: window.scrollY });
  }, [snapshotKey, query, draft]);
  useEffect(() => {
    if (!data || scrollRestored.current) return;
    scrollRestored.current = true;
    if (initial?.scrollY) window.requestAnimationFrame(() => window.scrollTo(0, initial.scrollY));
  }, [data, initial]);
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;
  const counts = data ? [data.summary.totalCount, data.summary.notStartedCount, data.summary.inProgressCount, data.summary.openIssueProjectCount ?? 0, data.summary.holdCount ?? 0] : null;
  const reset = () => { setDraft(''); setQuery(emptyOsanListFilters()); };
  const open = (projectId: string) => {
    saveOsanListSnapshot(snapshotKey, { filters: query, draft, scrollY: window.scrollY });
    onOpen(projectId);
  };
  return <OsanListFrame
    className="osan-home-dashboard"
    title={isHome ? '오산 홈' : '진행 현황'}
    description="납기가 빠른 순서입니다. HOLD는 하단에 표시하며, 납기가 지난 완료 프로젝트는 제외됩니다."
    summaryLabels={['관리 대상', '공정 시작 전', '공정 진행 중', '공정 이상', 'HOLD']}
    kpiValues={['All', 'NotStarted', 'InProgress', 'OpenIssue', 'Hold']}
    counts={counts} search={draft} onSearchChange={setDraft}
    onSearch={() => setQuery({ ...query, search: draft.trim(), page: 1 })}
    statuses={query.statuses} onStatusesChange={statuses => setQuery({ ...query, statuses, page: 1 })}
    selectedCustomers={query.customers} customers={result.state.kind === 'ready' ? result.state.data.customers ?? [] : []} onCustomersChange={customers => setQuery({ ...query, customers, page: 1 })}
    dueFrom={query.dueFrom} dueTo={query.dueTo} onDueChange={(dueFrom, dueTo) => setQuery({ ...query, dueFrom, dueTo, page: 1 })}
    kpi={query.kpi} onKpiChange={kpi => setQuery({ ...query, kpi, page: 1 })}
    onReset={reset}
  >
    <div className="osan-dashboard-results" aria-busy={state.kind === 'loading'}>
      {state.kind === 'loading' && <p role="status">진행 현황을 불러오는 중입니다.</p>}
      {state.kind === 'error' && <div role="alert"><p>{state.forbidden ? '진행 현황을 볼 권한이 없습니다.' : state.message}</p>
        {!state.forbidden && <button type="button" onClick={() => { setResult({ query, state: { kind: 'loading' } }); setAttempt(attempt + 1); }}>다시 시도</button>}
      </div>}
      {data && data.items.length === 0 && <div role="status"><p>{query.customers.length || query.search || query.statuses.length || query.dueFrom || query.dueTo || query.kpi ? '조건에 맞는 프로젝트가 없습니다.' : isHome ? '홈에 표시할 프로젝트가 없습니다.' : '등록된 프로젝트가 없습니다.'}</p>
        {!!(query.customers.length || query.search || query.statuses.length || query.dueFrom || query.dueTo || query.kpi) && <button type="button" onClick={reset}>검색 조건 초기화</button>}
      </div>}
      {data && <ul className="osan-dashboard-list" aria-label="프로젝트 진행 목록">{data.items.map(project => <li key={project.projectId}>
        <button type="button" className={`osan-dashboard-project${isOsanOverdue(project, today) ? ' is-overdue' : ''}`} onClick={() => open(project.projectId)} aria-label={`${project.title} ${isHome ? '프로젝트 상세' : '진행 상세'} 열기`}>
          <span className="osan-dashboard-project-title" title={project.title}><span className="osan-dashboard-project-name">{project.title}</span><span className="osan-dashboard-part" title={project.productName}>{project.productName}</span><span className={`osan-dashboard-dday${project.deliveryHold ? ' is-hold' : ''}`}>{project.deliveryHold ? 'HOLD' : formatOsanDday(project.deliveryDate, today, project.status)}</span></span>
          {<span className="osan-home-deadline"><strong>W/O {project.workOrderNumber || '—'}</strong><span>납기 {project.deliveryDate}</span></span>}
          <OsanStepper stages={project.stages}/>
          <span className="osan-dashboard-percent" role="progressbar" aria-label={`${project.title} 진행률`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={project.progressPercent}>{project.progressPercent}%</span>
        </button>

      </li>)}</ul>}
    </div>
    {data && <nav className="osan-dashboard-pagination" aria-label="진행 현황 페이지">
      <button type="button" aria-label="이전 페이지" disabled={data.page <= 1} onClick={() => setQuery({ ...query, page: data.page - 1 })}><img src={backIcon} alt="" /></button>
      <span aria-live="polite">{data.page} / {totalPages}</span>
      <button type="button" aria-label="다음 페이지" disabled={data.page >= totalPages} onClick={() => setQuery({ ...query, page: data.page + 1 })}><img src={forwardIcon} alt="" /></button>
    </nav>}
  </OsanListFrame>;
}
