import { dismissOnBackdrop } from './dialogBackdrop';
import { useEffect, useRef, useState } from 'react';
import { getOsanProgressPhoto, type OsanProgressPhoto } from './osanProgress';

export function SavedPhoto({ projectId, photo, userKey }: { projectId: string; photo: OsanProgressPhoto; userKey?: string }) {
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [open, setOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const controller = new AbortController(); let objectUrl = '';
    setUrl(''); setError('');
    getOsanProgressPhoto(projectId, photo.photoId, userKey, controller.signal).then(blob => {
      if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setUrl(objectUrl); }
    }).catch(() => { if (!controller.signal.aborted) setError('사진을 불러오지 못했습니다.'); });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [projectId, photo.photoId, userKey, attempt]);
  useEffect(() => { if (open) dialog.current?.showModal(); else dialog.current?.close(); }, [open]);
  return <figure className="osan-progress-saved-photo">
    {url ? <button type="button" className="osan-photo-zoom-trigger" aria-label="사진 크게 보기" onClick={() => setOpen(true)}><img src={url} alt={`사진 ${photo.displayOrder}`} /></button>
      : error ? <><p role="alert">{error}</p><button type="button" onClick={() => setAttempt(n => n + 1)}>사진 다시 불러오기</button></> : <p role="status">사진 불러오는 중…</p>}
    <dialog ref={dialog} className="osan-image-dialog" aria-label="사진 크게 보기" onCancel={() => setOpen(false)} onClick={e => dismissOnBackdrop(e, () => setOpen(false))}>
      {open && <><header><span>사진 크게 보기</span><button type="button" onClick={() => setOpen(false)}>닫기</button></header><img src={url} alt="확대된 완료 사진" /></>}
    </dialog>
  </figure>;
}
export function OsanPhotoGallery({ projectId, photos, userKey, history = false }: { projectId: string; photos: OsanProgressPhoto[]; userKey?: string; history?: boolean }) {
  const track = useRef<HTMLDivElement>(null); const [index, setIndex] = useState(0);
  function move(next: number) { track.current?.scrollTo({ left: next * track.current.clientWidth, behavior: 'smooth' }); }
  return <div className={history ? 'osan-history-photo-grid' : 'osan-progress-photo-region'}>
    {history ? photos.map(photo => <SavedPhoto key={photo.photoId} {...{projectId, photo, userKey}} />) : <>
      <p className="osan-photo-count">사진 {photos.length}장 등록</p>
      <div ref={track} className="osan-photo-track" aria-label="사진 목록" onScroll={e => setIndex(Math.round(e.currentTarget.scrollLeft / Math.max(1, e.currentTarget.clientWidth)))}>
        {photos.map(photo => <SavedPhoto key={photo.photoId} {...{projectId, photo, userKey}} />)}
      </div>
      <div className="osan-photo-controls"><button type="button" disabled={index <= 0} onClick={() => move(index - 1)}>이전 사진</button><span aria-live="polite">{Math.min(index + 1, photos.length)} / {photos.length}</span><button type="button" disabled={index >= photos.length - 1} onClick={() => move(index + 1)}>다음 사진</button><button type="button" onClick={() => (track.current?.children[index]?.querySelector('.osan-photo-zoom-trigger') as HTMLButtonElement | null)?.click()}>크게 보기</button></div>
    </>}
  </div>;
}
