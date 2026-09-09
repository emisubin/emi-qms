import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import {
  completeOsanProgress, completionUnavailable, getOsanProgress, getOsanProgressPhoto, osanStageNames,
  startOsanProgress, validateOsanPhotos,
  type OsanCompletionRequest, type OsanProgressDetail, type OsanProgressPhoto, type OsanProgressTarget
} from './osanProgress';

export interface OsanProgressPageProps {
  projectId: string; developmentUserKey: string | undefined; mutationAllowed: boolean; onBack?: () => void;
}
export function OsanProgressPage(props: OsanProgressPageProps) {
  return <OsanProgressWorkspace key={`${props.projectId}:${props.developmentUserKey ?? ''}`} {...props} />;
}
function message(error: unknown) { return error instanceof Error ? error.message : '요청을 처리하지 못했습니다. 다시 시도해 주세요.'; }
function SavedPhoto({ projectId, photo, userKey }: { projectId: string; photo: OsanProgressPhoto; userKey?: string }) {
  const [url, setUrl] = useState<string>();
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let current = true;
    let objectUrl: string | undefined;
    const controller = new AbortController();
    getOsanProgressPhoto(projectId, photo.photoId, userKey, controller.signal).then(blob => {
      if (!current) return;
      objectUrl = URL.createObjectURL(blob);
      setUrl(objectUrl);
    }).catch(error => { if (current) setError(message(error)); });
    return () => { current = false; controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [projectId, photo.photoId, userKey, attempt]);
  return <figure className="osan-progress-saved-photo">{url ? <img src={url} alt={photo.fileName} /> : error ? <><p role="alert">{error}</p><button type="button" onClick={() => { setError(''); setAttempt(value => value + 1); }}>사진 다시 불러오기</button></> : <p role="status">사진 불러오는 중…</p>}</figure>;
}
function Preview({ file }: { file: File }) {
  const [url, setUrl] = useState<string>();
  useEffect(() => { const objectUrl = URL.createObjectURL(file); setUrl(objectUrl); return () => URL.revokeObjectURL(objectUrl); }, [file]);
  return url ? <img src={url} alt={`${file.name} 미리보기`} /> : null;
}
function OsanProgressWorkspace({ projectId, developmentUserKey, mutationAllowed, onBack }: OsanProgressPageProps) {
  const [project, setProject] = useState<OsanProgressDetail>();
  const [loadError, setLoadError] = useState('');
  const [loadStatus, setLoadStatus] = useState<number>();
  const [reload, setReload] = useState(0);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [stage, setStage] = useState(1);
  const [mode, setMode] = useState<'individual' | 'batch'>('individual');
  const [selectorOpen, setSelectorOpen] = useState(false);
  const [photoTargetId, setPhotoTargetId] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [files, setFiles] = useState<File[]>([]);
  const [fileError, setFileError] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const alive = useRef(true);
  const pendingCompletion = useRef<OsanCompletionRequest | null>(null);
  const pendingStart = useRef<{ operationId: string; targets: { targetId: string; expectedVersion: number }[] } | null>(null);
  const albumInput = useRef<HTMLInputElement>(null);
  const cameraInput = useRef<HTMLInputElement>(null);
  const modal = useRef<HTMLDialogElement>(null);
  const selector = useRef<HTMLDialogElement>(null);
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
        setModalOpen(false); setSelectorOpen(false); setPhotoTargetId(null);
        pendingStart.current = null; pendingCompletion.current = null;
      }
    });
    return () => { current = false; controller.abort(); };
  }, [projectId, developmentUserKey, reload]);
  useEffect(() => { if (modalOpen) modal.current?.showModal(); else modal.current?.close(); }, [modalOpen]);
  useEffect(() => { if (selectorOpen) selector.current?.showModal(); else selector.current?.close(); }, [selectorOpen]);
  const selected = project?.targets.filter(target => selectedIds.includes(target.targetId)) ?? [];
  const unavailable = completionUnavailable(selected, stage, mode);
  const canStart = selected.length > 0 && selected.every(target => target.canStart);
  const selectedStageCompleted = selected.length > 0 && selected.every(target => target.steps.find(step => step.sequenceNumber === stage)?.status === 'Completed');
  const selectionLabel = selected.length === 1 ? selected[0].displayName : `${selected.length}개 대상 선택`;
  function changeSelection(ids: string[], nextMode: 'individual' | 'batch') {
    if (busyRef.current) return;
    setSelectedIds(ids); setMode(nextMode); setError(''); setSuccess('');
    setPhotoTargetId(null);
    pendingStart.current = null; pendingCompletion.current = null; setFiles([]); setFileError('');
  }
  function selectFiles(next: FileList | null, append = false) {
    if (busyRef.current || !next) return;
    const chosen = [...(append ? files : []), ...Array.from(next)];
    const validation = validateOsanPhotos(chosen);
    if (validation) { setError(validation); setFileError(validation); return; }
    setFileError('');
    pendingCompletion.current = null; setFiles(chosen); setError('');
  }
  async function start() {
    if (busyRef.current || refreshing || !mutationAllowed || !canStart) return;
    const request = pendingStart.current ?? { operationId: crypto.randomUUID(), targets: selected.map(target => ({ targetId: target.targetId, expectedVersion: target.version })) };
    pendingStart.current = request; busyRef.current = true; setBusy(true); setError(''); setSuccess('');
    try {
      const result = await startOsanProgress(projectId, request.operationId, request.targets, developmentUserKey);
      if (alive.current) { setProject(result.project); pendingStart.current = null; setSuccess('작업을 시작했습니다.'); }
    } catch (error) { if (alive.current) setError(`${message(error)} 같은 요청을 다시 시도하거나 새로고침하여 상태를 확인해 주세요.`); }
    finally { busyRef.current = false; if (alive.current) setBusy(false); }
  }
  async function complete() {
    if (busyRef.current || refreshing || !mutationAllowed || unavailable || fileError) return;
    const validation = validateOsanPhotos(files);
    if (validation) { setError(validation); return; }
    const request = pendingCompletion.current ?? { operationId: crypto.randomUUID(), completionMode: mode, stageSequence: stage, targets: selected.map(target => ({ targetId: target.targetId, expectedVersion: target.version })), photos: files };
    pendingCompletion.current = request; busyRef.current = true; setBusy(true); setError(''); setSuccess('');
    try {
      const result = await completeOsanProgress(projectId, request, developmentUserKey);
      if (alive.current) { setProject(result.project); pendingCompletion.current = null; setFiles([]); setModalOpen(false); setSuccess('선택한 대상의 단계 완료와 사진 저장을 확인했습니다.'); }
    } catch (error) { if (alive.current) setError(`${message(error)} 저장 결과가 확인되지 않았습니다. 같은 요청을 다시 시도할 수 있습니다.`); }
    finally { busyRef.current = false; if (alive.current) setBusy(false); }
  }
  function refresh() { if (busyRef.current) return; pendingStart.current = null; pendingCompletion.current = null; setError(''); setRefreshing(true); setReload(value => value + 1); }
  function targetHistory(target: OsanProgressTarget) {
    const step = target.steps.find(item => item.sequenceNumber === stage);
    const showPhotos = selected.length === 1 || photoTargetId === target.targetId;
    return <section className="osan-progress-history" key={target.targetId} aria-label={`${target.displayName} 완료 기록`}>
      {selected.length > 1 && <h3>{target.displayName}</h3>}
      {step?.status === 'Completed' ? <>
        <p className="osan-progress-completed-title">작업 완료</p>
        {step.photos.length ? <>
          {selected.length > 1 && <button type="button" className="osan-progress-show-photos" aria-expanded={showPhotos} onClick={() => setPhotoTargetId(showPhotos ? null : target.targetId)}>{target.displayName} 사진 {showPhotos ? '닫기' : '보기'} ({step.photos.length}장)</button>}
          {showPhotos && <div className="osan-progress-photo-region">{step.photos.map(photo => <SavedPhoto key={`${target.targetId}:${stage}:${photo.photoId}`} projectId={projectId} photo={photo} userKey={developmentUserKey} />)}</div>}
        </> : <div className="osan-progress-photo-region osan-progress-photo-region--empty"><p>등록된 완료 사진이 없습니다.</p></div>}
        <div className="osan-progress-completed"><span>{step.completedByDisplayName ?? '작업자 정보 없음'}</span><time dateTime={step.completedAtUtc ?? undefined}>{step.completedAtUtc ? new Date(step.completedAtUtc).toLocaleString('ko-KR') : '완료 일시 정보 없음'}</time></div>
      </> : <p className="osan-progress-not-completed">미완료</p>}
    </section>;
  }
  if (!project) return <section className="osan-progress-page">{loadError ? <><p role="alert">{loadStatus === 403 ? '이 프로젝트의 진행 정보를 조회할 권한이 없습니다.' : loadStatus === 404 ? '프로젝트를 찾을 수 없습니다.' : loadError}</p><button type="button" onClick={refresh}>다시 불러오기</button></> : <p role="status">진행 정보를 불러오는 중…</p>}</section>;
  return <section className="osan-progress-page" aria-label="오산 진행 상세">
    <header className="osan-progress-header"><button type="button" onClick={onBack} disabled={busy} aria-label="진행 현황으로 돌아가기">‹</button><h1>진행 현황</h1></header>
    <div className="osan-progress-project"><h2>{project.title}</h2><p>{project.projectCode}</p><span>{project.status === 'Completed' ? '완료' : project.targets.some(target => target.status !== 'NotStarted') ? '진행 중' : '시작 전'}</span></div>
    {loadError && <p role="alert">{loadError}</p>}
    {!mutationAllowed && <p className="osan-progress-readonly">읽기 전용입니다. 완료 기록을 조회할 수 있습니다.</p>}
    {project.targets.length === 0 ? <p>등록된 수량 대상이 없습니다.</p> : <>
      <button type="button" className="osan-progress-target-trigger" disabled={busy} onClick={() => setSelectorOpen(true)}>{selectionLabel}<span aria-hidden="true">⌄</span></button>
      <div className="osan-progress-stage-card">
      <nav className="osan-progress-stages" aria-label="진행 단계"><button type="button" aria-label="이전 단계" disabled={stage === 1 || busy} onClick={() => { setStage(value => value - 1); setError(''); pendingCompletion.current = null; setFiles([]); setFileError(''); }}>‹</button><h2>{osanStageNames[stage - 1]}</h2><button type="button" aria-label="다음 단계" disabled={stage === 7 || busy} onClick={() => { setStage(value => value + 1); setError(''); pendingCompletion.current = null; setFiles([]); setFileError(''); }}>›</button></nav>
      <div className="osan-progress-stage-index">{stage} / 7</div>
      {!selectedStageCompleted && <div className="osan-progress-guidance"><div className="osan-progress-guidance-photo">안내 사진이 들어갈 영역</div><p>단계 설명이 들어갈 영역</p></div>}
      <div className="osan-progress-histories">{selected.map(targetHistory)}</div>
      {!selectedStageCompleted && <div className="osan-progress-actions">{canStart ? <button type="button" disabled={busy || refreshing || !mutationAllowed} onClick={() => void start()}>{busy ? '저장 중…' : '작업 시작'}</button> : <><button type="button" disabled={busy || refreshing || !mutationAllowed || !!unavailable} onClick={() => { setModalOpen(true); setError(''); }}>완료</button>{unavailable && <p>{unavailable}</p>}</>}</div>}
      </div>
      {success && <p role="status">{success}</p>}{error && !modalOpen && <p role="alert">{error}</p>}
      <button type="button" className="osan-progress-refresh" disabled={busy} onClick={refresh}>새로고침</button>
    </>}
    <dialog ref={selector} className="osan-progress-selector" aria-labelledby="osan-selection-title" onCancel={() => setSelectorOpen(false)}>
      <h2 id="osan-selection-title">대상 선택</h2>
      <label><input type="checkbox" checked={selectedIds.length === project.targets.length && selectedIds.length > 0} onChange={event => changeSelection(event.target.checked ? project.targets.map(target => target.targetId) : [], 'batch')} />전체 선택</label>
      {project.targets.map(target => <label key={target.targetId}><input type="checkbox" checked={selectedIds.includes(target.targetId)} onChange={event => { const ids = event.target.checked ? [...selectedIds, target.targetId] : selectedIds.filter(id => id !== target.targetId); changeSelection(ids, ids.length === 1 ? 'individual' : 'batch'); }} />{target.displayName}</label>)}
      <label><input type="checkbox" checked={mode === 'batch'} onChange={event => { setMode(event.target.checked ? 'batch' : 'individual'); pendingCompletion.current = null; }} disabled={selectedIds.length !== 1} />선택 단계 일괄 완료</label>
      <button type="button" onClick={() => setSelectorOpen(false)}>선택 완료</button>
    </dialog>
    <dialog ref={modal} className="osan-progress-completion-modal" aria-labelledby="osan-completion-title" onCancel={event => { if (busy) event.preventDefault(); else setModalOpen(false); }}>
      <h2 id="osan-completion-title">해당 진행 단계를 완료하셨나요?</h2>
      {files.length === 0 && <div className="osan-progress-photo-placeholder" aria-label="완료 사진을 선택할 영역"><span aria-hidden="true">+</span></div>}
      <div className="osan-progress-previews">{files.map((file, index) => <figure key={`${file.name}:${file.lastModified}:${index}`}><Preview file={file} /><figcaption>{file.name}</figcaption><button type="button" disabled={busy} onClick={() => { pendingCompletion.current = null; setFiles(current => current.filter((_, position) => position !== index)); setError(''); setFileError(''); }}>사진 제거</button></figure>)}</div>
      <div className="osan-progress-photo-inputs"><button type="button" disabled={busy} onClick={() => cameraInput.current?.click()}>촬영</button><button type="button" disabled={busy} onClick={() => albumInput.current?.click()}>업로드</button></div>
      <input ref={cameraInput} hidden type="file" accept="image/jpeg,image/png" capture="environment" aria-label="카메라 사진 선택" disabled={busy} onChange={event => { selectFiles(event.target.files, true); event.target.value = ''; }} />
      <input ref={albumInput} hidden type="file" accept="image/jpeg,image/png" multiple aria-label="기존 사진 선택" disabled={busy} onChange={event => { selectFiles(event.target.files); event.target.value = ''; }} />
      <p className="osan-progress-photo-instruction">사진은 선택 사항입니다. {selectionLabel} · {osanStageNames[stage - 1]}</p>
      {mode === 'batch' && <p>같은 사진이 선택한 {selected.length}개 대상에 모두 적용됩니다.</p>}
      <p className="osan-progress-photo-limits">JPEG·PNG 최대 5장, 장당 5MiB, 전체 15MiB. 원본을 저장합니다.</p>
      {(error || fileError) && <p role="alert">{error || fileError}</p>}
      {fileError && <button type="button" disabled={busy} onClick={() => { setFiles([]); setFileError(''); setError(''); pendingCompletion.current = null; }}>사진 없이 완료하기로 변경</button>}
      <button className="osan-progress-submit" type="button" disabled={busy || refreshing || !mutationAllowed || !!fileError} aria-label="업로드하고 단계 완료" onClick={() => void complete()}>{busy ? '저장 중…' : '업로드'}</button>
      <button className="osan-progress-close" type="button" disabled={busy} onClick={() => setModalOpen(false)}>닫기</button>
    </dialog>
  </section>;
}
