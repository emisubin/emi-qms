import { OsanStageAction } from './OsanStageActions';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { dismissOnBackdrop } from './dialogBackdrop';
import { OsanPhotoPreview } from './OsanPhotoPreview';
import { mutateOsanIssue, validateOsanPhotos, validateOsanRecord, type OsanCompletionRequest, type OsanProgressTarget } from './osanProgress';

type Action = 'register' | 'record' | 'resolve';
const labels = { register: '공정 이상 발생', record: '기록 추가', resolve: '조치 완료' };
export function OsanStageIssueActions({ projectId, target, stage, userKey, mutationAllowed, canManage, disabled, onBusy, onSaved, resolveOnly = false }: {
  projectId: string; target: OsanProgressTarget; stage: number; userKey?: string; mutationAllowed: boolean;
  canManage: boolean; disabled: boolean; resolveOnly?: boolean; onBusy: (busy: boolean) => void; onSaved: () => void;
}) {
  const step = target.steps.find(s => s.sequenceNumber === stage)!;
  const [action, setAction] = useState<Action | null>(null);
  const [comment, setComment] = useState(''); const [files, setFiles] = useState<File[]>([]);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [submitted, setSubmitted] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null); const camera = useRef<HTMLInputElement>(null); const album = useRef<HTMLInputElement>(null);
  const pending = useRef<OsanCompletionRequest | null>(null); const lock = useRef(false); const alive = useRef(true);
  useEffect(() => { alive.current = true; return () => { alive.current = false; }; }, []);
  useEffect(() => { if (action) dialog.current?.showModal(); else dialog.current?.close(); }, [action]);
  function open(next: Action) {
    setAction(next); setComment(''); setFiles([]); setError(''); setSubmitted(false); pending.current = null;
  }
  function select(next: FileList | null, append: boolean) {
    if (!next) return;
    const chosen = [...(append ? files : []), ...Array.from(next)]; const error = validateOsanPhotos(chosen);
    if (error) { setError(error); return; } setFiles(chosen); setError('');
  }
  async function save() {
    if (lock.current || !action || !mutationAllowed) return;
    const validation = !comment.trim() ? '코멘트를 입력해 주세요.' : comment.length > 1000 ? '코멘트는 최대 1000자입니다.' : action === 'resolve' ? validateOsanRecord(files, comment, canManage) : validateOsanPhotos(files);
    if (validation) { setError(validation); return; }
    const request = pending.current ?? { operationId: crypto.randomUUID(), completionMode: 'individual', stageSequence: stage,
      targets: [{ targetId: target.targetId, expectedVersion: target.version }], comment, photos: files };
    pending.current = request; lock.current = true; setBusy(true); setSubmitted(true); onBusy(true); setError('');
    try {
      await mutateOsanIssue(projectId, action, request, userKey);
      if (alive.current) { setAction(null); pending.current = null; onBusy(false); onSaved(); }
    } catch (e) {
      if (alive.current) {
        setError(e instanceof Error ? e.message : '저장을 확인하지 못했습니다. 같은 요청으로 다시 시도해 주세요.');
        if (e instanceof ApiError && [400,403,413,422].includes(e.status)) { pending.current = null; setSubmitted(false); }
      }
    } finally { lock.current = false; if (alive.current) { setBusy(false); onBusy(false); } }
  }
  if (!mutationAllowed) return null;
  return <>
    {resolveOnly ? step.openIssue && <OsanStageAction placement="primary" type="button" disabled={disabled || busy || !step.canResolveIssue} onClick={() => open('resolve')}>조치 완료</OsanStageAction> : step.openIssue ?
      <OsanStageAction placement="secondary" type="button" disabled={disabled || busy || !step.canRegisterIssue} onClick={() => open('record')}>기록 추가</OsanStageAction>
      : step.canRegisterIssue && <OsanStageAction placement={step.status === 'Completed' ? 'secondary' : 'visible'} type="button" className="osan-issue-register" disabled={disabled || busy} onClick={() => open('register')}>공정 이상 발생</OsanStageAction>}
    <dialog ref={dialog} className="osan-progress-completion-modal osan-issue-modal" aria-labelledby={`issue-title-${target.targetId}-${resolveOnly ? 'resolve' : 'record'}`}
      onCancel={e => { if (busy) e.preventDefault(); else setAction(null); }} onClick={e => dismissOnBackdrop(e, () => { if (!busy) setAction(null); })}>
      <h2 id={`issue-title-${target.targetId}-${resolveOnly ? 'resolve' : 'record'}`}>{action ? labels[action] : '이상 처리'}</h2>
      <p>{target.displayName} · {step.stepName}</p>
      <label className="osan-comment-input">{action === 'resolve' ? '조치 내용' : action === 'register' ? '이상 내용' : '추가 내용'} <small>필수</small>
        <textarea value={comment} maxLength={1000} disabled={busy || submitted} onChange={e => setComment(e.target.value)}/><span>{comment.length} / 1000자</span></label>
      {!files.length && <div className="osan-progress-photo-placeholder"><span aria-hidden="true">+</span></div>}
      <div className="osan-progress-previews">{files.map((file, index) => <figure key={`${file.name}:${index}`}><OsanPhotoPreview file={file} projectId={projectId} userKey={userKey}/><button type="button" disabled={busy || submitted} onClick={() => setFiles(value => value.filter((_, i) => i !== index))}>사진 제거</button></figure>)}</div>
      <div className="osan-progress-photo-inputs"><button type="button" disabled={busy || submitted} onClick={() => camera.current?.click()}>촬영</button><button type="button" disabled={busy || submitted} onClick={() => album.current?.click()}>업로드</button></div>
      <input hidden ref={camera} type="file" accept="image/*" capture="environment" aria-label="이상 처리 카메라 사진" disabled={busy || submitted} onChange={e => { select(e.target.files, true); e.target.value = ''; }}/>
      <input hidden ref={album} type="file" multiple accept="image/jpeg,image/png,image/heic,image/heif,.heic,.heif" aria-label="이상 처리 사진 선택" disabled={busy || submitted} onChange={e => { select(e.target.files, false); e.target.value = ''; }}/>
      <p>{action === 'resolve' ? (canManage ? '관리자는 사진 없이 코멘트만으로 저장할 수 있습니다.' : '완료 사진을 1장 이상 첨부해 주세요.') : '이상 사진은 선택 첨부입니다.'}</p>
      <p>JPEG·PNG·HEIC 최대 5장, 전체 40MiB. 원본을 저장합니다.</p>
      <p>{action === 'resolve' ? '저장하면 이상 조치와 해당 Gate 완료가 함께 처리됩니다.' : '다음 단계는 진행할 수 있습니다. 포장 전에는 이상 조치를 완료해야 합니다.'}</p>
      {error && <p role="alert">{error}</p>}
      <button type="button" className="osan-progress-submit" disabled={busy || !mutationAllowed} onClick={() => void save()}>{busy ? '저장 중…' : submitted ? '저장 재시도' : action ? labels[action] : '저장'}</button>
      <button type="button" className="osan-progress-close" disabled={busy} onClick={() => setAction(null)}>닫기</button>
    </dialog>
  </>;
}
