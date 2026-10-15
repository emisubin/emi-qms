import { useCallback } from 'react';
import { PhotoPreview } from './PhotoPreview';
import { previewOsanPhoto } from './osanProgress';
export function OsanPhotoPreview({file,projectId,userKey,alt}:{file:File;projectId:string;userKey?:string;alt?:string}) {
 const loadPreview=useCallback((selected:File,signal:AbortSignal)=>previewOsanPhoto(projectId,selected,userKey,signal),[projectId,userKey]);
 return <PhotoPreview file={file} loadPreview={loadPreview} alt={alt}/>;
}
