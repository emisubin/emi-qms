import { OsanStageAction } from './OsanStageActions';
import { createPortal } from 'react-dom';
import { useEffect, useRef, useState } from 'react';
import { ApiError, fetchJson } from './api';
import { dismissOnBackdrop } from './dialogBackdrop';
import './OsanNotificationSettings.css';
import './OsanWorkRequest.css';

type Recipient = { userId: string; displayName: string; departmentName: string | null };
type RequestInput = { operationId: string; targetId: string; stageSequence: number; recipientIds: string[] };
export function OsanWorkRequest({ projectId, targetId, targetName, stage, stageName, title, part, workOrder, userKey, disabled, onSaved }: {
  projectId: string; targetId: string; targetName: string; stage: number; stageName: string;
  title: string; part?: string; workOrder?: string; userKey?: string; disabled: boolean; onSaved: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [people, setPeople] = useState<Recipient[]>();
  const [selected, setSelected] = useState<string[]>([]);
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [success, setSuccess] = useState('');
  const [retry, setRetry] = useState(0);
  const dialog = useRef<HTMLDialogElement>(null);
  const pending = useRef<RequestInput | null>(null);
  const sending = useRef(false);
  const saveController = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!open) { dialog.current?.close(); return; }
    dialog.current?.showModal();
    const controller = new AbortController();
    setPeople(undefined); setError('');
    fetchJson<{ recipients: Recipient[] }>(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/work-request-recipients`, userKey, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setPeople(data.recipients); })
      .catch(e => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : '담당자를 불러오지 못했습니다.'); });
    return () => controller.abort();
  }, [open, projectId, userKey, retry]);
  useEffect(() => () => { saveController.current?.abort(); }, []);
  const visible = people?.filter(p => `${p.displayName} ${p.departmentName ?? ''}`.includes(search.trim()));
  const chosen = people?.filter(p => selected.includes(p.userId)) ?? [];
  function close() { if (!sending.current) setOpen(false); }
  async function send() {
    if (sending.current || disabled || !chosen.length) return;
    const controller = new AbortController(); saveController.current = controller;
    const request = pending.current ?? { operationId: crypto.randomUUID(), targetId, stageSequence: stage, recipientIds: chosen.map(p => p.userId) };
    pending.current = request; sending.current = true; setBusy(true); setSubmitted(true); setError('');
    try {
      await fetchJson(`/api/osan/projects/${encodeURIComponent(projectId)}/progress/work-requests`, userKey, { method: 'POST', body: JSON.stringify(request), signal: controller.signal });
      if (!controller.signal.aborted) { setOpen(false); setSuccess(`${chosen.length}명에게 공정 진행을 요청했습니다.`); onSaved(); }
    } catch (e) {
      if (!controller.signal.aborted) {
        setError(e instanceof Error ? e.message : '요청 결과를 확인하지 못했습니다. 다시 시도해 주세요.');
        if (e instanceof ApiError && [400,403,404,409].includes(e.status)) { pending.current = null; setSubmitted(false); }
      }
    } finally { sending.current = false; if (!controller.signal.aborted) setBusy(false); }
  }
  return <><OsanStageAction placement="secondary" type="button" disabled={disabled || busy} onClick={() => { setSelected([]); setSearch(''); setSuccess(''); setSubmitted(false); pending.current = null; setOpen(true); }}>공정 진행 요청</OsanStageAction>
    {success && <p role="status">{success}</p>}
    {createPortal(<dialog ref={dialog} className="osan-notification-dialog osan-work-request" aria-labelledby={`work-request-${targetId}`} onCancel={e => { if (busy) e.preventDefault(); else close(); }} onClick={e => dismissOnBackdrop(e, close)}>
      <header><div className="osan-notification-heading"><h2 id={`work-request-${targetId}`}>공정 진행 요청</h2><button type="button" className="osan-notification-close" aria-label="공정 진행 요청 닫기" disabled={busy} onClick={close}>×</button></div><p>진행을 요청할 오산 담당자를 선택해 주세요.</p></header>
      <div className="osan-notification-content">
        <section className="osan-work-request-project"><strong>{title}{part ? ` · ${part}` : ''}</strong>{workOrder && <p><b>W/O {workOrder}</b></p>}<p>{targetName}</p><span>{stageName}</span></section>
        <label className="osan-work-request-label" htmlFor={`recipient-search-${targetId}`}>오산 담당자 <small>여러 명 선택 가능</small></label>
        <input id={`recipient-search-${targetId}`} className="osan-work-request-search" type="search" placeholder="이름 또는 부서 검색" value={search} disabled={busy || submitted} onChange={e => setSearch(e.target.value)}/>
        {!people && !error && <p role="status">담당자를 불러오는 중입니다.</p>}
        {people && <div className="osan-work-request-people">{visible?.map(p => <label key={p.userId}><input type="checkbox" aria-label={`${p.displayName} ${p.departmentName ?? '부서 미지정'}`} checked={selected.includes(p.userId)} disabled={busy || submitted} onChange={e => setSelected(ids => e.target.checked ? [...ids, p.userId] : ids.filter(id => id !== p.userId))}/><strong>{p.displayName}</strong><small>{p.departmentName ?? '부서 미지정'}</small></label>)}{!visible?.length && <p>{people.length ? '검색 결과가 없습니다.' : '선택할 오산 담당자가 없습니다.'}</p>}</div>}
        <section className="osan-work-request-chosen"><p>선택한 담당자 {chosen.length}명</p><div>{chosen.map(p => <button key={p.userId} type="button" disabled={busy || submitted} aria-label={`${p.displayName} 선택 해제`} onClick={() => setSelected(ids => ids.filter(id => id !== p.userId))}>{p.displayName} ×</button>)}</div></section>
        <p className="osan-notification-note">선택한 담당자에게 진행 요청 알림을 보냅니다.<br/>작업 담당자를 지정하지 않으며, 기존 권한이 있는 누구나 완료할 수 있습니다.</p><p className="osan-notification-note">메일·푸시는 오산 공통 알림 설정에 따라 전달됩니다.</p>
        {error && <div className="osan-notification-save-error"><p role="alert">{error}</p>{!people && <button type="button" onClick={() => setRetry(v => v + 1)}>다시 불러오기</button>}</div>}
      </div><footer><span aria-live="polite">{chosen.length}명 선택</span><div><button type="button" className="osan-notification-button" disabled={busy} onClick={close}>취소</button><button type="button" className="osan-notification-button osan-notification-button--primary" disabled={disabled || busy || !chosen.length} onClick={() => void send()}>{busy ? '요청 중…' : submitted ? '같은 요청 재시도' : '진행 요청 보내기'}</button></div></footer>
    </dialog>, document.body)}</>;
}
