import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { getOsanDashboard, type OsanDashboardResponse, type OsanDashboardStatus } from './osanDashboard';
import filterIcon from './assets/osan-dashboard-filter.png';
import backIcon from './assets/osan-dashboard-back.png';
import forwardIcon from './assets/osan-dashboard-forward.png';
import './osan-dashboard.css';

const statuses: { value: OsanDashboardStatus; label: string }[] = [
  { value: 'All', label: '전체' }, { value: 'NotStarted', label: '시작 전' },
  { value: 'InProgress', label: '진행 중' }, { value: 'Completed', label: '완료' }
];
type State = { kind: 'loading' } | { kind: 'ready'; data: OsanDashboardResponse } | { kind: 'error'; message: string; forbidden: boolean };

export function OsanDashboardPage(props: { developmentUserKey?: string; onOpen: (projectId: string) => void }) {
  return <Workspace key={props.developmentUserKey ?? ''} {...props} />;
}
function Workspace({ developmentUserKey, onOpen }: { developmentUserKey?: string; onOpen: (projectId: string) => void }) {
  const [draft, setDraft] = useState('');
  const [query, setQuery] = useState({ search: '', status: 'All' as OsanDashboardStatus, page: 1 });
  const [filterOpen, setFilterOpen] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ query: typeof query; state: State }>({ query, state: { kind: 'loading' } });
  const state: State = result.query === query ? result.state : { kind: 'loading' };
  useEffect(() => {
    let current = true;
    const controller = new AbortController();
    getOsanDashboard(developmentUserKey, query, controller.signal).then(data => {
      if (current) setResult({ query, state: { kind: 'ready', data } });
    }).catch((error: unknown) => {
      if (current) setResult({ query, state: { kind: 'error', forbidden: error instanceof ApiError && error.status === 403,
        message: error instanceof Error ? error.message : '진행 현황을 불러오지 못했습니다.' } });
    });
    return () => { current = false; controller.abort(); };
  }, [developmentUserKey, query, attempt]);
  const data = state.kind === 'ready' ? state.data : undefined;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;
  const counts = data ? [data.summary.totalCount, data.summary.notStartedCount, data.summary.inProgressCount, data.summary.completedCount] : null;
  const reset = () => { setDraft(''); setQuery({ search: '', status: 'All', page: 1 }); };
  return <section className="osan-dashboard" aria-labelledby="osan-dashboard-title">
    <h1 id="osan-dashboard-title">진행 현황</h1>
    <p className="osan-dashboard-description">프로젝트를 선택하면 해당 프로젝트의 진행 작업만 표시됩니다.</p>
    <div className="osan-dashboard-summary" aria-label="프로젝트 요약">
      {statuses.map((status, index) => <div key={status.value}><span>{status.label}</span><strong>{counts ? counts[index].toLocaleString() : '—'}</strong></div>)}
    </div>
    <h2>프로젝트 목록</h2>
    <div className="osan-dashboard-toolbar">
      <form className="osan-dashboard-search" onSubmit={event => { event.preventDefault(); setQuery({ ...query, search: draft.trim(), page: 1 }); }}>
        <input aria-label="프로젝트 검색" placeholder="프로젝트 검색" value={draft} maxLength={200} onChange={event => setDraft(event.target.value)} />
        <button type="submit">검색</button>
      </form>
      <button type="button" className="osan-dashboard-filter" aria-expanded={filterOpen} aria-controls="osan-dashboard-filter-options" onClick={() => setFilterOpen(!filterOpen)}>
        <img src={filterIcon} alt="" />필터{query.status !== 'All' && <span className="osan-dashboard-filter-active" aria-label="적용됨" />}
      </button>
    </div>
    {filterOpen && <div className="osan-dashboard-filter-options" id="osan-dashboard-filter-options">
      <label>상태 <select value={query.status} onChange={event => setQuery({ ...query, status: event.target.value as OsanDashboardStatus, page: 1 })}>
        {statuses.map(status => <option key={status.value} value={status.value}>{status.label}</option>)}
      </select></label>
      <button type="button" onClick={reset}>초기화</button>
      <button type="button" onClick={() => setFilterOpen(false)}>닫기</button>
    </div>}
    <div className="osan-dashboard-results" aria-busy={state.kind === 'loading'}>
      {state.kind === 'loading' && <p role="status">진행 현황을 불러오는 중입니다.</p>}
      {state.kind === 'error' && <div role="alert"><p>{state.forbidden ? '진행 현황을 볼 권한이 없습니다.' : state.message}</p>
        {!state.forbidden && <button type="button" onClick={() => { setResult({ query, state: { kind: 'loading' } }); setAttempt(attempt + 1); }}>다시 시도</button>}
      </div>}
      {data && data.items.length === 0 && <div role="status"><p>{query.search || query.status !== 'All' ? '조건에 맞는 프로젝트가 없습니다.' : '등록된 프로젝트가 없습니다.'}</p>
        {(query.search || query.status !== 'All') && <button type="button" onClick={reset}>검색 조건 초기화</button>}
      </div>}
      {data && <ul className="osan-dashboard-list" aria-label="프로젝트 진행 목록">{data.items.map(project => <li key={project.projectId}>
        <button type="button" className="osan-dashboard-project" onClick={() => onOpen(project.projectId)} aria-label={`${project.title} 진행 상세 열기`}>
          <span className="osan-dashboard-project-title" title={project.title}>{project.title}</span>
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
  </section>;
}
