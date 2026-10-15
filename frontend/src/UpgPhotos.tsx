import {useCallback} from 'react';
import {SavedPhotoView} from './SavedPhotoView';
import {PhotoPreview} from './PhotoPreview';
import {getUpgPhoto,previewUpgPhoto,type UpgPhotoInput} from './g2Nonconformances';
export function UpgSavedPhoto({user,itemId,photo,preview=false}:{user:string|undefined;itemId:string;photo:UpgPhotoInput;preview?:boolean}){
 const loadPhoto=useCallback((signal:AbortSignal)=>getUpgPhoto(user,itemId,photo.id!,signal,['image/heic','image/heif'].includes(photo.contentType)),[user,itemId,photo.id,photo.contentType]);
 return <SavedPhotoView loadPhoto={loadPhoto} alt={photo.fileName} preview={preview}/>;
}
export function UpgPhotoPreview({user,file}:{user:string|undefined;file:File}){
 const loadPreview=useCallback((selected:File,signal:AbortSignal)=>previewUpgPhoto(user,selected,signal),[user]);
 return <PhotoPreview file={file} loadPreview={loadPreview}/>;
}
