import {useEffect,useState} from 'react';
import {useNativeDialog} from './useNativeDialog';
import {dismissOnBackdrop} from './dialogBackdrop';
export function SavedPhotoView({ loadPhoto, alt, preview = false }: { loadPhoto: (signal: AbortSignal) => Promise<Blob>; alt: string; preview?: boolean }) {
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [open, setOpen] = useState(false);
  const dialog = useNativeDialog(open, setOpen);
  useEffect(() => {
    const controller = new AbortController(); let objectUrl = '';
    setUrl(''); setError('');
    loadPhoto(controller.signal).then(blob => {
      if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setUrl(objectUrl); }
    }).catch(() => { if (!controller.signal.aborted) setError('사진을 불러오지 못했습니다.'); });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [loadPhoto, attempt]);
  const picture = <img src={url} alt={alt} onError={() => { setUrl(''); setError('사진을 표시하지 못했습니다. 다시 불러와 주세요.'); }} />;
  const fallback = error ? <><p role="alert">{error}</p><button type="button" onClick={() => setAttempt(n => n + 1)}>사진 다시 불러오기</button></> : <p role="status">사진 불러오는 중…</p>;
  if (preview) return url ? picture : fallback;
  return <figure className="osan-progress-saved-photo">
    {url ? <button type="button" className="osan-photo-zoom-trigger" aria-label="사진 크게 보기" onClick={() => setOpen(true)}>{picture}</button> : fallback}
    <dialog ref={dialog} className="osan-image-dialog" aria-label="사진 크게 보기" onCancel={() => setOpen(false)} onClick={e => dismissOnBackdrop(e, () => setOpen(false))}>
      {open && <><header><span>사진 크게 보기</span><button type="button" onClick={() => setOpen(false)}>닫기</button></header><img src={url} alt={`${alt} 확대`} /></>}
    </dialog>
  </figure>;
}
