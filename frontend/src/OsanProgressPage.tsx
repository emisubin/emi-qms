import { dismissOnBackdrop } from './dialogBackdrop';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { OsanPhotoEditor } from './OsanPhotoEditor';
import { OsanPhotoGallery } from './OsanPhotoGallery';
import { OsanStageHistory } from './OsanStageHistory';
import { OsanStageGuidance } from './OsanStageGuidance';
import {
  completeOsanProgress, completionUnavailable, getOsanProgress, osanStageNames,
  validateOsanPhotos, validateOsanRecord,
  type OsanCompletionRequest, type OsanProgressDetail, type OsanProgressTarget
} from './osanProgress';

export interface OsanProgressPageProps {
  projectId: string; initialTargetId?: string; initialStage?: string; developmentUserKey: string | undefined; mutationAllowed: boolean; onBack?: () => void;
}
export function OsanProgressPage(props: OsanProgressPageProps) {
  return <OsanProgressWorkspace key={`${props.projectId}:${props.initialTargetId ?? ''}:${props.initialStage ?? ''}:${props.developmentUserKey ?? ''}`} {...props} />;
}
function message(error: unknown) { return error instanceof Error ? error.message : '요청을 처리하지 못했습니다. 다시 시도해 주세요.'; }
function Preview({ file }: { file: File }) {
  const [url, setUrl] = useState<string>();
  useEffect(() => { const objectUrl = URL.createObjectURL(file); setUrl(objectUrl); return () => URL.revokeObjectURL(objectUrl); }, [file]);
  return url ? <img src={url} alt={`${file.name} 미리보기`} /> : null;
}
function OsanProgressWorkspace({ projectId, initialTargetId, initialStage, developmentUserKey, mutationAllowed, onBack }: OsanProgressPageProps) {
  const [project, setProject] = useState<OsanProgressDetail>();
  const [loadError, setLoadError] = useState('');
  const [loadStatus, setLoadStatus] = useState<number>();
  const [reload, setReload] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>(initialTargetId ? [initialTargetId] : []);
  const [stage, setStage] = useState(() => { const byName = osanStageNames.indexOf(initialStage as typeof osanStageNames[number]); const n = Number(initialStage); return byName >= 0 ? byName + 1 : n >= 1 && n <= 7 && Number.isInteger(n) ? n : 1; });
  const [desktop, setDesktop] = useState(() => window.matchMedia?.('(min-width: 861px)').matches ?? false);
  const [stageOpen, setStageOpen] = useState(!!initialStage && (window.matchMedia?.('(min-width: 861px)').matches ?? false));
  const stageDialog = useRef<HTMLDialogElement>(null);
  const [mode, setMode] = useState<'individual' | 'batch'>('individual');
  const [selectorOpen, setSelectorOpen] = useState(false);
  const [photoTargetId, setPhotoTargetId] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [files, setFiles] = useState<File[]>([]);
  const [comment, setComment] = useState('');
  const [fileError, setFileError] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const alive = useRef(true);
  const pendingCompletion = useRef<OsanCompletionRequest | null>(null);
  const albumInput = useRef<HTMLInputElement>(null);
  const cameraInput = useRef<HTMLInputElement>(null);
  const modal = useRef<HTMLDialogElement>(null);
  const selector = useRef<HTMLDivElement>(null);
  const selectorTrigger = useRef<HTMLButtonElement>(null);
  useEffect(() => { alive.current = true; return () => { alive.current = false; }; }, []);
  useEffect(() => {
    let current = true;
    const controller = new AbortController();
    getOsanProgress(projectId, developmentUserKey, controller.signal).then(result => {
      if (!current) return;
      setProject(result); setLoadError(''); setLoadStatus(undefined); setRefreshing(false);
      setSelectedIds(ids => ids.length ? ids.filter(id => result.targets.some(target => target.targetId === id)) : result.targets.slice(0, 1).map(target => target.targetId));
    }).catch(error => {
      if (!current) return;
      setRefreshing(false); setLoadError(message(error));
      setLoadStatus(error instanceof ApiError ? error.status : undefined);
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
        setProject(undefined); setSelectedIds([]); setFiles([]);
        setModalOpen(false); setStageOpen(false); setSelectorOpen(false); setPhotoTargetId(null);
        pendingCompletion.current = null;
      }
    });
    return () => { current = false; controller.abort(); };
  }, [projectId, developmentUserKey, reload]);
  useEffect(() => { if (modalOpen) modal.current?.showModal(); else modal.current?.close(); }, [modalOpen]);
  useEffect(() => {
    const media = window.matchMedia?.('(min-width: 861px)');
    if (!media) return;
    const changed = () => setDesktop(media.matches);
    media.addEventListener('change', changed);
    return () => media.removeEventListener('change', changed);
  }, []);
  useEffect(() => {
    if (stageOpen) stageDialog.current?.showModal(); else stageDialog.current?.close();
  }, [stageOpen, project]);
  useEffect(() => { if (!desktop && !modalOpen && !busy) setStageOpen(false); }, [desktop, modalOpen, busy]);

  useEffect(() => {
    if (!selectorOpen) return;
    function dismiss(event: PointerEvent) {
      if (!selector.current?.contains(event.target as Node)) setSelectorOpen(false);
    }
    function escape(event: KeyboardEvent) {
      if (event.key === 'Escape') { setSelectorOpen(false); selectorTrigger.current?.focus(); }
    }
    document.addEventListener('pointerdown', dismiss);
    document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', dismiss); document.removeEventListener('keydown', escape); };
  }, [selectorOpen]);
  const selected = project?.targets.filter(target => selectedIds.includes(target.targetId)) ?? [];
  const unavailable = completionUnavailable(selected, stage, mode);
  const selectedStageCompleted = selected.length > 0 && selected.every(target => target.steps.find(step => step.sequenceNumber === stage)?.status === 'Completed');
  const selectionLabel = selected.length === 1 ? selected[0].displayName : `${selected.length}개 대상 선택`;
  function changeSelection(ids: string[], nextMode: 'individual' | 'batch') {
    if (busyRef.current) return;
    setSelectedIds(ids); setMode(nextMode); setError(''); setSuccess('');
    setPhotoTargetId(null);
    pendingCompletion.current = null; setFiles([]); setComment(''); setFileError('');
  }
  function selectFiles(next: FileList | null, append = false) {
    if (busyRef.current || !next) return;
    const chosen = [...(append ? files : []), ...Array.from(next)];
    const validation = validateOsanPhotos(chosen);
    if (validation) { setError(validation); setFileError(validation); return; }
    setFileError('');
    pendingCompletion.current = null; setFiles(chosen); setError('');
  }
  async function complete() {
    if (busyRef.current || refreshing || !mutationAllowed || unavailable || fileError) return;
    const validation = validateOsanRecord(files, comment, !!project?.canManageStages);
    if (validation) { setError(validation); return; }
    const request = pendingCompletion.current ?? { operationId: crypto.randomUUID(), completionMode: mode, stageSequence: stage, targets: selected.map(target => ({ targetId: target.targetId, expectedVersion: target.version })), photos: files, comment };
    pendingCompletion.current = request; busyRef.current = true; setBusy(true); setError(''); setSuccess('');
    try {
      const result = await completeOsanProgress(projectId, request, developmentUserKey);
      if (alive.current) { setProject(result.project); pendingCompletion.current = null; setFiles([]); setComment(''); setModalOpen(false); setSuccess('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.'); }
    } catch (error) { if (alive.current) setError(`${message(error)} 저장 결과가 확인되지 않았습니다. 같은 요청을 다시 시도할 수 있습니다.`); }
    finally { busyRef.current = false; if (alive.current) setBusy(false); }
  }
  function refresh() { if (busyRef.current) return; pendingCompletion.current = null; setError(''); setSuccess(''); setRefreshing(true); setReload(value => value + 1); }
  function targetHistory(target: OsanProgressTarget) {
    const step = target.steps.find(item => item.sequenceNumber === stage);
    const showPhotos = selected.length === 1 || photoTargetId === target.targetId;
    return <section className="osan-progress-history" key={target.targetId} aria-label={`${target.displayName} 완료 기록`}>
      {selected.length > 1 && <h3>{target.displayName}</h3>}
      {step?.status === 'Completed' ? <>
        <p className="osan-progress-completed-title">작업 완료</p>
        {step.photos.length ? <>
          {selected.length > 1 && <button type="button" className="osan-progress-show-photos" aria-expanded={showPhotos} onClick={() => setPhotoTargetId(showPhotos ? null : target.targetId)}>{target.displayName} 사진 {showPhotos ? '닫기' : '보기'} ({step.photos.length}장)</button>}
          {showPhotos && <OsanPhotoGallery key={`${target.targetId}:${stage}`} projectId={projectId} photos={step.photos} userKey={developmentUserKey} />}
        </> : <div className="osan-progress-photo-region osan-progress-photo-region--empty"><p>등록된 완료 사진이 없습니다.</p></div>}
        <div className="osan-record-comment"><span>코멘트</span><p>{step.comment || '등록된 코멘트가 없습니다.'}</p></div>
        <div className="osan-progress-completed"><span>{step.completedByDisplayName ?? '작업자 정보 없음'}</span><time dateTime={step.completedAtUtc ?? undefined}>{step.completedAtUtc ? new Date(step.completedAtUtc).toLocaleString('ko-KR') : '완료 일시 정보 없음'}</time></div>
        <div className="osan-record-actions"><OsanStageHistory projectId={projectId} stepId={step.stepId} title={`${target.displayName} · ${step.stepName}`} userKey={developmentUserKey}/><OsanPhotoEditor key={`${target.targetId}:${stage}:${developmentUserKey}:${reload}`} projectId={projectId} target={target} stage={stage} userKey={developmentUserKey} mutationAllowed={mutationAllowed} onSaved={refresh}/></div>
      </> : step && <><p className="osan-progress-not-completed">{step.rejected ? '반려 · 수정 후 저장해 주세요.' : '미완료'}</p><div className="osan-record-actions"><OsanStageHistory projectId={projectId} stepId={step.stepId} title={`${target.displayName} · ${step.stepName}`} userKey={developmentUserKey}/>{step.editOpen && <OsanPhotoEditor key={`${target.targetId}:${stage}:${reload}`} projectId={projectId} target={target} stage={stage} userKey={developmentUserKey} mutationAllowed={mutationAllowed} onSaved={refresh}/>}</div></>}
    </section>;
  }
  if (!project) return <section className="osan-progress-page">{loadError ? <><p role="alert">{loadStatus === 403 ? '이 프로젝트의 진행 정보를 조회할 권한이 없습니다.' : loadStatus === 404 ? '프로젝트를 찾을 수 없습니다.' : loadError}</p><button type="button" onClick={refresh}>다시 불러오기</button></> : <p role="status">진행 정보를 불러오는 중…</p>}</section>;
  const stageContent = <>
    {desktop ? <details className="osan-guidance-toggle"><summary>단계 설명</summary><OsanStageGuidance stage={stage}/></details> : !selectedStageCompleted && <OsanStageGuidance stage={stage}/>}
    <div className="osan-progress-histories">{selected.map(targetHistory)}</div>
    {!selectedStageCompleted && !selected.some(t => t.steps.find(s => s.sequenceNumber === stage)?.editOpen) && <div className="osan-progress-actions"><button type="button" disabled={busy || refreshing || !mutationAllowed || !!unavailable} onClick={() => { setModalOpen(true); setError(''); }}>완료</button>{unavailable && <p>{unavailable}</p>}</div>}
  </>;
  return <section className="osan-progress-page" aria-label="오산 진행 상세">
    <header className="osan-progress-header"><button type="button" onClick={onBack} disabled={busy} aria-label="진행 현황으로 돌아가기">‹</button><h1>진행 현황</h1></header>
    <div className="osan-progress-project"><h2>{project.title}</h2><p>{project.projectCode}</p><span>{project.status === 'Completed' ? '완료' : project.status === 'InProgress' ? '진행 중' : '시작 전'}</span></div>
    {loadError && <p role="alert">{loadError}</p>}
    {!mutationAllowed && <p className="osan-progress-readonly">읽기 전용입니다. 완료 기록을 조회할 수 있습니다.</p>}
    {project.targets.length === 0 ? <p>등록된 수량 대상이 없습니다.</p> : <>
      <div ref={selector} className="osan-progress-target-selector" onBlur={event => { if (event.relatedTarget && !event.currentTarget.contains(event.relatedTarget as Node)) setSelectorOpen(false); }}>
        <button ref={selectorTrigger} type="button" className="osan-progress-target-trigger" disabled={busy} aria-expanded={selectorOpen} aria-controls="osan-target-options" onClick={() => setSelectorOpen(value => !value)}>{selectorOpen ? '패널 선택' : selectionLabel}<span className="osan-progress-selector-arrow" aria-hidden="true" /></button>
        {selectorOpen && <div id="osan-target-options" className="osan-progress-selector" role="group" aria-label="패널 선택">
          <label><input type="checkbox" aria-label="전체 선택" checked={selectedIds.length === project.targets.length && selectedIds.length > 0} onChange={event => changeSelection(event.target.checked ? project.targets.map(target => target.targetId) : [], project.targets.length === 1 ? 'individual' : 'batch')} /><span>(모두 선택)</span></label>
          {project.targets.map(target => <label key={target.targetId}><input type="checkbox" checked={selectedIds.includes(target.targetId)} onChange={event => { const ids = event.target.checked ? [...selectedIds, target.targetId] : selectedIds.filter(id => id !== target.targetId); changeSelection(ids, ids.length === 1 ? 'individual' : 'batch'); }} /><span>{target.displayName}</span></label>)}
        </div>}
      </div>
      {desktop ? <nav className="osan-progress-stage-overview" aria-label="전체 진행 단계">
        {osanStageNames.map((name, index) => {
          const completed = selected.filter(target => target.steps.find(step => step.sequenceNumber === index + 1)?.status === 'Completed').length;
          const done = selected.length > 0 && completed === selected.length;
          return <button type="button" key={name} aria-haspopup="dialog" aria-pressed={stageOpen && stage === index + 1}
            disabled={busy || refreshing} className={done ? 'is-complete' : ''}
            onClick={() => { setStage(index + 1); setError(''); setSuccess(''); setPhotoTargetId(null); pendingCompletion.current = null; setFiles([]); setComment(''); setFileError(''); setStageOpen(true); }}>
            <span className="osan-stage-number">{index + 1}</span><strong>{name}</strong>
            <span>{selected.length === 0 ? '대상을 선택하세요' : done ? '완료' : completed > 0 ? '일부 완료' : '미완료'}</span>
            <small>{completed} / {selected.length} 대상 완료</small>
          </button>;
        })}
      </nav> : <div className="osan-progress-stage-card">
      <nav className="osan-progress-stages" aria-label="진행 단계"><button type="button" aria-label="이전 단계" disabled={stage === 1 || busy} onClick={() => { setStage(value => value - 1); setError(''); pendingCompletion.current = null; setFiles([]); setComment(''); setFileError(''); }}>‹</button><h2>{osanStageNames[stage - 1]}</h2><button type="button" aria-label="다음 단계" disabled={stage === 7 || busy} onClick={() => { setStage(value => value + 1); setError(''); pendingCompletion.current = null; setFiles([]); setComment(''); setFileError(''); }}>›</button></nav>
      <div className="osan-progress-stage-index">{stage} / 7</div>
        {stageContent}
      </div>}
      <dialog ref={stageDialog} className="osan-stage-dialog" aria-labelledby="osan-stage-dialog-title"
        onClick={event => dismissOnBackdrop(event, () => { if (!busy && !modalOpen) setStageOpen(false); })}
        onCancel={event => { if (busy || modalOpen) event.preventDefault(); else setStageOpen(false); }}>
        {stageOpen && <>
          <header className="osan-stage-dialog-header"><div><h2 id="osan-stage-dialog-title">{osanStageNames[stage - 1]}</h2><p>{project.title} · {selectionLabel}</p></div>
            <button type="button" disabled={busy || modalOpen} onClick={() => setStageOpen(false)} aria-label="단계 상세 닫기">닫기</button></header>
          {stageContent}
          {success && <p role="status">{success}</p>}{error && !modalOpen && <p role="alert">{error}</p>}
        </>}
      </dialog>
      {!stageOpen && success && <p role="status">{success}</p>}{!stageOpen && error && !modalOpen && <p role="alert">{error}</p>}
      <button type="button" className="osan-progress-refresh" disabled={busy} onClick={refresh}>새로고침</button>
    </>}
    <dialog ref={modal} className="osan-progress-completion-modal" aria-labelledby="osan-completion-title" onClick={event => dismissOnBackdrop(event, () => { if (!busy) setModalOpen(false); })} onCancel={event => { if (busy) event.preventDefault(); else setModalOpen(false); }}>
      <h2 id="osan-completion-title">해당 진행 단계를 완료하셨나요?</h2>
      {files.length === 0 && <div className="osan-progress-photo-placeholder" aria-label="완료 사진을 선택할 영역"><span aria-hidden="true">+</span></div>}
      <div className="osan-progress-previews">{files.map((file, index) => <figure key={`${file.name}:${file.lastModified}:${index}`}><Preview file={file} /><figcaption>{file.name}</figcaption><button type="button" disabled={busy} onClick={() => { pendingCompletion.current = null; setFiles(current => current.filter((_, position) => position !== index)); setError(''); setFileError(''); }}>사진 제거</button></figure>)}</div>
      <div className="osan-progress-photo-inputs"><button type="button" disabled={busy} onClick={() => cameraInput.current?.click()}>촬영</button><button type="button" disabled={busy} onClick={() => albumInput.current?.click()}>업로드</button></div>
      <input ref={cameraInput} hidden type="file" accept="image/*" capture="environment" aria-label="카메라 사진 선택" disabled={busy} onChange={event => { selectFiles(event.target.files, true); event.target.value = ''; }} />
      <input ref={albumInput} hidden type="file" accept="image/jpeg,image/png" multiple aria-label="기존 사진 선택" disabled={busy} onChange={event => { selectFiles(event.target.files); event.target.value = ''; }} />
      <p className="osan-progress-photo-instruction">{project.canManageStages ? '관리자는 사진 없이 코멘트만으로 저장할 수 있습니다.' : '사진을 1장 이상 첨부해 주세요.'} {selectionLabel} · {osanStageNames[stage - 1]}</p>
      <label className="osan-comment-input">코멘트 <small>{!files.length && project.canManageStages ? '사진 미첨부 시 필수' : '선택'}</small><textarea value={comment} maxLength={1000} disabled={busy} onChange={e => {setComment(e.target.value);pendingCompletion.current=null;}}/><span>{comment.length} / 1000자</span></label>
      {mode === 'batch' && <p>같은 사진과 코멘트가 선택한 {selected.length}개 대상에 모두 적용됩니다.</p>}
      <p className="osan-progress-photo-limits">JPEG·PNG 최대 5장, 장당 5MiB, 전체 15MiB. 원본을 저장합니다.</p>
      {(error || fileError) && <p role="alert">{error || fileError}</p>}
      {fileError && <button type="button" disabled={busy} onClick={() => { setFiles([]); setFileError(''); setError(''); pendingCompletion.current = null; }}>선택한 사진 비우기</button>}
      <button className="osan-progress-submit" type="button" disabled={busy || refreshing || !mutationAllowed || !!fileError} aria-label="업로드하고 단계 완료" onClick={() => void complete()}>{busy ? '저장 중…' : '업로드'}</button>
      <button className="osan-progress-close" type="button" disabled={busy} onClick={() => setModalOpen(false)}>닫기</button>
    </dialog>
  </section>;
}
