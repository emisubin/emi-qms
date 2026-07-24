import { FormEvent, useEffect, useMemo, useState } from 'react';
import { addPendingComment, getPendingIssue } from './api';
import type { PendingDetail } from './pending';
import { buildPendingTimeline } from './pendingTimeline';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; detail: PendingDetail }
  | { kind: 'error'; message: string };

export function PendingInspectionContext({
  pendingId,
  developmentUserKey
}: {
  pendingId: string;
  developmentUserKey: string;
}) {
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [comment, setComment] = useState('');
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState('');

  useEffect(() => {
    let active = true;
    setState({ kind: 'loading' });
    void getPendingIssue(developmentUserKey, pendingId)
      .then((detail) => active && setState({ kind: 'ready', detail }))
      .catch((error: unknown) => active && setState({ kind: 'error', message: errorMessage(error) }));
    return () => { active = false; };
  }, [developmentUserKey, pendingId]);

  const detail = state.kind === 'ready' ? state.detail : null;
  const timeline = useMemo(() => detail ? buildPendingTimeline(detail) : [], [detail]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!comment.trim() || saving) return;
    setSaving(true);
    setFeedback('');
    try {
      const next = await addPendingComment(developmentUserKey, pendingId, comment.trim());
      setState({ kind: 'ready', detail: next });
      setComment('');
      setFeedback('재검사 코멘트를 Pending 이력에 추가했습니다.');
    } catch (error) {
      setFeedback(errorMessage(error));
    } finally {
      setSaving(false);
    }
  }

  if (state.kind === 'loading') {
    return <section className="pending-inspection-context" aria-label="Pending 조치 내용"><p role="status">조치 내용과 코멘트를 불러오는 중입니다…</p></section>;
  }
  if (state.kind === 'error') {
    return <section className="pending-inspection-context" aria-label="Pending 조치 내용"><p role="alert">{state.message}</p></section>;
  }
  const readyDetail = state.detail;
  const latestAction = [...readyDetail.history]
    .reverse()
    .find((event) => event.toStatus === 'ReinspectionRequested' && event.reason?.trim());

  return (
    <section className="pending-inspection-context" aria-labelledby={`pending-inspection-${pendingId}`}>
      <header>
        <div><span>REINSPECTION CONTEXT</span><h4 id={`pending-inspection-${pendingId}`}>조치 내용과 재검사 코멘트</h4></div>
        <strong>{readyDetail.issue.statusLabel}</strong>
      </header>
      <p className="pending-inspection-summary"><strong>{readyDetail.issue.title}</strong><span>조치 담당 {readyDetail.issue.assigneeDisplayName ?? '미지정'} · 활동 {timeline.length}건</span></p>
      <dl className="pending-inspection-action-summary">
        <div><dt>조치 내용</dt><dd>{latestAction?.reason ?? '조치 완료 내용이 입력되지 않았습니다.'}</dd></div>
      </dl>
      {readyDetail.canComment ? (
        <form className="pending-inspection-comment" onSubmit={submit}>
          <label><span>재검사 코멘트</span><textarea aria-label="재검사 코멘트" value={comment} maxLength={2000} onChange={(event) => setComment(event.target.value)} placeholder="조치 내용을 확인한 결과나 재검사 메모를 남겨 주세요." /></label>
          <button type="submit" disabled={saving || !comment.trim()}>{saving ? '등록 중…' : '코멘트 등록'}</button>
        </form>
      ) : <p className="pending-inspection-readonly">조치·검사 담당자만 코멘트를 추가할 수 있습니다.</p>}
      {feedback ? <p className="pending-inspection-feedback" role="status">{feedback}</p> : null}
      <ol className="pending-inspection-timeline">
        {timeline.map((event) => (
          <li key={event.key}>
            <span aria-hidden="true" />
            <div><strong>{event.title}</strong>{event.summary ? <p>{event.summary}</p> : null}{event.detail ? <p>{event.detail}</p> : null}{event.note ? <p>{event.note}</p> : null}<small>{event.actor} · {formatDateTime(event.createdAtUtc)}</small></div>
          </li>
        ))}
      </ol>
    </section>
  );
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('ko-KR', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Pending 조치 내용을 불러오지 못했습니다.';
}
