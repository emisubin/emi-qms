import {useCallback,useRef,useState} from 'react';
import {getOsanProgressPhoto,type OsanProgressPhoto} from './osanProgress';
import {SavedPhotoView} from './SavedPhotoView';
export function SavedPhoto({projectId,photo,userKey,preview=false}:{projectId:string;photo:OsanProgressPhoto;userKey?:string;preview?:boolean}) {
 const loadPhoto=useCallback((signal:AbortSignal)=>getOsanProgressPhoto(projectId,photo.photoId,userKey,signal,photo.contentType==='image/heic'),[projectId,photo.photoId,photo.contentType,userKey]);
 return <SavedPhotoView loadPhoto={loadPhoto} alt={`사진 ${photo.displayOrder}`} preview={preview}/>;
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
