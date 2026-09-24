import { useEffect, useState } from 'react';
import { fetchJson } from './api';
import './OsanAdminPage.css';

type Approval = {
  projectId: string; projectCode: string; projectTitle: string; requestId: string; targetId: string;
  stageSequence: number; requestedByName: string; requestedAt: string;
};
type State = { kind: 'loading' } | { kind: 'ready'; items: Approval[] } | { kind: 'error'; message: string };
const stages = ['입고검사', '배치검사', '배선검사', '8계통', '동작검사', '출하검사', '포장'];

export function OsanGateApprovalsPage({ developmentUserKey, mutationAllowed = true, onOpenProject }: {
  developmentUserKey?: string; mutationAllowed?: boolean; onOpenProject: (projectId: string) => void;
}) {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [revision, setRevision] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    fetchJson<{ items: Approval[] }>('/api/osan/gate-approvals', developmentUserKey, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setState({ kind: 'ready', items: data.items }); })
      .catch(error => { if (!controller.signal.aborted) setState({ kind: 'error', message: error instanceof Error ? error.message : '승인 대기를 불러오지 못했습니다.' }); });
    return () => controller.abort();
  }, [developmentUserKey, revision]);
  async function approve(item: Approval) {
    if (busyId || !mutationAllowed) return;
    setBusyId(item.requestId); setFeedback('');
    try {
      await fetchJson(`/api/osan/projects/${encodeURIComponent(item.projectId)}/progress/photo-edits/${encodeURIComponent(item.requestId)}/approve`,
        developmentUserKey, { method: 'POST', body: JSON.stringify({}) });
      setFeedback(`${item.projectTitle} 승인 요청을 처리했습니다.`);
      setState(current => current.kind === 'ready' ? { ...current, items: current.items.filter(value => value.requestId !== item.requestId) } : current);
    } catch (error) {
      setFeedback(error instanceof Error ? error.message : '승인을 처리하지 못했습니다.');
      setRevision(value => value + 1);
    } finally { setBusyId(null); }
  }
  return <section className="osan-admin-page osan-gate-approvals-page" aria-label="Gate 승인 대기">
    <header className="osan-admin-heading"><div><h1>Gate 승인 대기</h1><p>사진·코멘트 수정 승인 요청 중 대기 건만 표시합니다.</p></div>
      <button type="button" onClick={() => setRevision(value => value + 1)} disabled={!!busyId}>새로고침</button></header>
    {state.kind === 'loading' && <p role="status">승인 대기를 불러오는 중입니다.</p>}
    {state.kind === 'error' && <p role="alert">{state.message} <button type="button" onClick={() => setRevision(value => value + 1)}>다시 시도</button></p>}
    {state.kind === 'ready' && (state.items.length ? <div className="osan-admin-approvals" role="table" aria-label="Gate 승인 요청 목록">
      <div role="row" className="osan-admin-approval-head"><span role="columnheader">프로젝트</span><span role="columnheader">Gate</span><span role="columnheader">요청자</span><span role="columnheader">요청일</span><span role="columnheader">처리</span></div>
      {state.items.map(item => <div role="row" className="osan-admin-approval-row" key={item.requestId}>
        <div role="cell"><button type="button" onClick={() => onOpenProject(item.projectId)}>{item.projectTitle}</button><small>{item.projectCode}</small></div>
        <span role="cell">{stages[item.stageSequence - 1] ?? 'Gate'}</span>
        <span role="cell">{item.requestedByName}</span>
        <time role="cell" dateTime={item.requestedAt}>{new Date(item.requestedAt).toLocaleString('ko-KR', { timeZone: 'Asia/Seoul' })}</time>
        <div role="cell"><button type="button" className="osan-admin-primary" disabled={!!busyId || !mutationAllowed} onClick={() => void approve(item)}>{busyId === item.requestId ? '처리 중…' : '사진 수정 1회 승인'}</button></div>
      </div>)}</div> : <p role="status">현재 승인 대기 건이 없습니다.</p>)}
    {feedback && <p role="status">{feedback}</p>}
  </section>;
}
