import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { getOsanDashboard, type OsanDashboardResponse, type OsanDashboardStatus } from './osanDashboard';
import { OsanListFrame } from './OsanListFrame';
import backIcon from './assets/osan-dashboard-back.png';
import forwardIcon from './assets/osan-dashboard-forward.png';
import './osan-dashboard.css';

const statuses: { value: OsanDashboardStatus; label: string }[] = [
  { value: 'All', label: '전체' }, { value: 'NotStarted', label: '시작 전' },
  { value: 'InProgress', label: '진행 중' }, { value: 'Completed', label: '완료' }
];
type State = { kind: 'loading' } | { kind: 'ready'; data: OsanDashboardResponse } | { kind: 'error'; message: string; forbidden: boolean };

export function OsanDashboardPage(props: { view?: 'home' | 'progress'; developmentUserKey?: string; onOpen: (projectId: string) => void }) {
  return <Workspace key={`${props.developmentUserKey ?? ''}:${props.view ?? 'progress'}`} {...props} />;
}
function Workspace({ developmentUserKey, onOpen, view = 'progress' }: { view?: 'home' | 'progress'; developmentUserKey?: string; onOpen: (projectId: string) => void }) {
  const isHome = view === 'home';
  const [draft, setDraft] = useState('');
  const [query, setQuery] = useState({ search: '', status: 'All' as OsanDashboardStatus, page: 1 });
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ query: typeof query; state: State }>({ query, state: { kind: 'loading' } });
  const state: State = result.query === query ? result.state : { kind: 'loading' };
  useEffect(() => {
    let current = true;
    const controller = new AbortController();
    getOsanDashboard(developmentUserKey, isHome ? { ...query, view } : query, controller.signal).then(data => {
      if (!current) return;
      const lastPage = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
      if (isHome && query.page > lastPage) {
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
    if (!isHome) return;
    const refresh = () => setAttempt(value => value + 1);
    const timer = window.setInterval(refresh, 60_000);
    window.addEventListener('focus', refresh);
    return () => { window.clearInterval(timer); window.removeEventListener('focus', refresh); };
  }, [isHome]);
  const data = state.kind === 'ready' ? state.data : undefined;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;
  const counts = data ? [data.summary.totalCount, data.summary.notStartedCount, data.summary.inProgressCount, data.summary.completedCount] : null;
  const reset = () => { setDraft(''); setQuery({ search: '', status: 'All', page: 1 }); };
  return <OsanListFrame
    className={isHome ? 'osan-home-dashboard' : ''}
    title={isHome ? '오산 홈' : '진행 현황'}
    description={isHome ? '납기가 빠른 순서입니다. 납기가 지난 완료 프로젝트는 홈에서 자동으로 제외됩니다.' : '프로젝트를 선택하면 해당 프로젝트의 진행 작업만 표시됩니다.'}
    counts={counts} search={draft} onSearchChange={setDraft}
    onSearch={() => setQuery({ ...query, search: draft.trim(), page: 1 })}
    status={query.status} onStatusChange={value => setQuery({ ...query, status: value as OsanDashboardStatus, page: 1 })}
    onReset={reset}
  >
    <div className="osan-dashboard-results" aria-busy={state.kind === 'loading'}>
      {state.kind === 'loading' && <p role="status">진행 현황을 불러오는 중입니다.</p>}
      {state.kind === 'error' && <div role="alert"><p>{state.forbidden ? '진행 현황을 볼 권한이 없습니다.' : state.message}</p>
        {!state.forbidden && <button type="button" onClick={() => { setResult({ query, state: { kind: 'loading' } }); setAttempt(attempt + 1); }}>다시 시도</button>}
      </div>}
      {data && data.items.length === 0 && <div role="status"><p>{query.search || query.status !== 'All' ? '조건에 맞는 프로젝트가 없습니다.' : isHome ? '홈에 표시할 프로젝트가 없습니다.' : '등록된 프로젝트가 없습니다.'}</p>
        {(query.search || query.status !== 'All') && <button type="button" onClick={reset}>검색 조건 초기화</button>}
      </div>}
      {data && <ul className="osan-dashboard-list" aria-label="프로젝트 진행 목록">{data.items.map(project => <li key={project.projectId}>
        <button type="button" className="osan-dashboard-project" onClick={() => onOpen(project.projectId)} aria-label={`${project.title} ${isHome ? '프로젝트 상세' : '진행 상세'} 열기`}>
          <span className="osan-dashboard-project-title" title={project.title}>{project.title}</span>
          {isHome && <span className="osan-home-deadline">납기 {project.deliveryDate} · {statuses.find(status => status.value === project.status)?.label}</span>}
          <span className="osan-dashboard-stages">{project.stages.map(stage => <span key={stage.sequenceNumber} aria-label={`${stage.stepName} ${stage.completedTargetCount}/${stage.totalTargetCount} 완료`}>
            <span>{stage.stepName}</span><span>{stage.completedTargetCount}/{stage.totalTargetCount}</span>
          </span>)}</span>
          <span className="osan-dashboard-bar" role="progressbar" aria-label={`${project.title} 진행률`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={project.progressPercent}><span style={{ width: `${project.progressPercent}%` }} /></span>
          <span className="osan-dashboard-percent">{project.progressPercent}%</span>
        </button>
        <dl className="osan-dashboard-project-meta">
          <div><dt>코드</dt><dd>{project.projectCode}</dd></div><div><dt>거래처</dt><dd>{project.customerName}</dd></div>
          <div><dt>제품명</dt><dd>{project.productName}</dd></div><div><dt>수량</dt><dd>{project.quantity}</dd></div>
          <div><dt>납기일</dt><dd>{project.deliveryDate}</dd></div><div><dt>상태</dt><dd>{statuses.find(status => status.value === project.status)?.label ?? '시작 전'}</dd></div>
        </dl>
      </li>)}</ul>}
    </div>
    {data && <nav className="osan-dashboard-pagination" aria-label="진행 현황 페이지">
      <button type="button" aria-label="이전 페이지" disabled={data.page <= 1} onClick={() => setQuery({ ...query, page: data.page - 1 })}><img src={backIcon} alt="" /></button>
      <span aria-live="polite">{data.page} / {totalPages}</span>
      <button type="button" aria-label="다음 페이지" disabled={data.page >= totalPages} onClick={() => setQuery({ ...query, page: data.page + 1 })}><img src={forwardIcon} alt="" /></button>
    </nav>}
  </OsanListFrame>;
}
