import { useEffect, useState } from 'react';
import { fetchJson } from './api';
import './OsanAdminPage.css';

type Approval = {
  projectId: string; projectCode: string; projectTitle: string; requestId: string; targetId: string;
  stageSequence: number; requestedByName: string; requestedAt: string; reason: string | null;
};
type State = { kind: 'loading' } | { kind: 'ready'; items: Approval[] } | { kind: 'error'; message: string };
const stages = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];

export function OsanGateApprovalsPage({ developmentUserKey, onOpenStage }: {
  developmentUserKey?: string; onOpenStage: (projectId: string, targetId: string, stage: number) => void;
}) {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    fetchJson<{ items: Approval[] }>('/api/osan/gate-approvals', developmentUserKey, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setState({ kind: 'ready', items: data.items }); })
      .catch(error => { if (!controller.signal.aborted) setState({ kind: 'error', message: error instanceof Error ? error.message : '승인 대기를 불러오지 못했습니다.' }); });
    return () => controller.abort();
  }, [developmentUserKey, revision]);
  return <section className="osan-admin-page osan-gate-approvals-page" aria-label="Gate 승인 대기">
    <header className="osan-admin-heading"><div><h1>Gate 승인 대기</h1><p>사진·코멘트 수정 승인 요청 중 대기 건만 표시합니다.</p></div>
      <button type="button" onClick={() => setRevision(value => value + 1)}>새로고침</button></header>
    {state.kind === 'loading' && <p role="status">승인 대기를 불러오는 중입니다.</p>}
    {state.kind === 'error' && <p role="alert">{state.message} <button type="button" onClick={() => setRevision(value => value + 1)}>다시 시도</button></p>}
    {state.kind === 'ready' && (state.items.length ? <div className="osan-admin-approvals" role="table" aria-label="Gate 승인 요청 목록">
      <div role="row" className="osan-admin-approval-head"><span role="columnheader">프로젝트</span><span role="columnheader">Gate</span><span role="columnheader">요청자</span><span role="columnheader">요청일</span><span className="osan-approval-reason" role="columnheader">요청 사유</span></div>
      {state.items.map(item => <div role="row" className="osan-admin-approval-row" key={item.requestId} tabIndex={0}
        aria-label={`${item.projectTitle} · ${stages[item.stageSequence - 1] ?? 'Gate'} 단계 상세 열기`}
        onClick={() => onOpenStage(item.projectId, item.targetId, item.stageSequence)}
        onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onOpenStage(item.projectId, item.targetId, item.stageSequence); } }}>
        <div role="cell"><span className="osan-approval-project-title">{item.projectTitle}</span><small>{item.projectCode}</small></div>
        <span role="cell">{stages[item.stageSequence - 1] ?? 'Gate'}</span>
        <span role="cell">{item.requestedByName}</span>
        <time role="cell" dateTime={item.requestedAt}>{new Date(item.requestedAt).toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' })}</time>
        <span className="osan-approval-reason" role="cell">{item.reason?.trim() || '—'}</span>
      </div>)}</div> : <p role="status">현재 승인 대기 건이 없습니다.</p>)}
  </section>;
}
