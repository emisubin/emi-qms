import { useEffect, useRef, useState } from 'react';
import { previewOsanPhoto, isHeicPhoto } from './osanProgress';

export function OsanPhotoPreview({ file, projectId, userKey, alt }: { file: File; projectId: string; userKey?: string; alt?: string }) {
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState(false);
  const watchdog = useRef<number | undefined>(undefined);
  useEffect(() => {
    const controller = new AbortController(); let objectUrl = '';
    setUrl(''); setError(''); setLoaded(false);
    const timer = window.setTimeout(() => {
      if (!controller.signal.aborted) { setError('사진 미리보기 처리가 지연되고 있습니다. 사진을 제거한 뒤 다시 선택해 주세요.'); controller.abort(); }
    }, 60_000);
    watchdog.current = timer;
    if (!isHeicPhoto(file)) { objectUrl = URL.createObjectURL(file); setUrl(objectUrl); }
    else previewOsanPhoto(projectId, file, userKey, controller.signal).then(blob => {
      if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setUrl(objectUrl); }
    }).catch((reason: unknown) => {
      window.clearTimeout(timer);
      if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : '사진을 읽지 못했습니다. 다시 선택해 주세요.');
    });
    return () => { controller.abort(); window.clearTimeout(timer); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [file, projectId, userKey]);
  return <>
    {!loaded && !error && <p role="status">사진 미리보기 준비 중…</p>}
    {error && <p role="alert">{error}</p>}
    {url && !error && <img src={url} alt={alt ?? `${file.name} 미리보기`} onLoad={() => { window.clearTimeout(watchdog.current); setLoaded(true); }} onError={() => { window.clearTimeout(watchdog.current); setError('이 사진의 미리보기를 표시할 수 없습니다. 파일 형식을 확인하거나 다른 사진을 선택해 주세요.'); }} />}
  </>;
}
