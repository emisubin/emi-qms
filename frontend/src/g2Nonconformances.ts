import {normalizePhotoType} from './photoSelection';
import { fetchBlob, fetchJson } from './api';
export type NonconformanceType = 'simple'|'complexLong';
export const nonconformanceTypes: {value:NonconformanceType;label:string}[] = [{value:'simple',label:'단순 부적합'},{value:'complexLong',label:'복합·장기 부적합'}];
export const nonconformanceTypeOf = (item:UpgItem):NonconformanceType => item.nonconformanceType ?? 'simple';
export const nonconformanceTypeName = (item:UpgItem) => nonconformanceTypes.find(type=>type.value===nonconformanceTypeOf(item))!.label;
export type UpgPhoto = { id:string; fileName:string; contentType:string; byteSize?:number };
export type UpgPhotoInput = { id?:string; fileName:string; contentType:string; base64?:string; byteSize?:number };
export type UpgAction = {completionDate:string;content:string;photos:UpgPhoto[]};
export type UpgItem = {nonconformanceType?:NonconformanceType;id:string;serial:string;occurrenceDate:string;occurrenceType:'production'|'supplier';content:string;initialStock:boolean;photos:UpgPhoto[];action:UpgAction|null;version:number};
export type UpgList = {canEdit:boolean;items:UpgItem[]};
export type UpgFirstInput = {nonconformanceType:NonconformanceType;serial:string;occurrenceDate:string;occurrenceType:'production'|'supplier';content:string;initialStock:boolean;photos:UpgPhotoInput[];expectedVersion?:number};
export type UpgActionInput = {completionDate:string;content:string;photos:UpgPhotoInput[];expectedVersion:number};
const root='/api/g2/nonconformances';
export const getUpg=(user:string|undefined,signal?:AbortSignal)=>fetchJson<UpgList>(root,user,{signal});
export const saveUpg=(user:string|undefined,id:string|null,input:UpgFirstInput)=>fetchJson<UpgItem>(id?`${root}/${id}`:root,user,{method:id?'PUT':'POST',body:JSON.stringify(input)});
export const saveUpgAction=(user:string|undefined,id:string,input:UpgActionInput)=>fetchJson<UpgItem>(`${root}/${id}/action`,user,{method:'PUT',body:JSON.stringify(input)});
export const deleteUpg=(user:string|undefined,item:UpgItem,second:boolean)=>fetchJson(`${root}/${item.id}${second?'/action':''}?expectedVersion=${item.version}`,user,{method:'DELETE'});
export const getUpgPhoto=(user:string|undefined,id:string,photoId:string,signal:AbortSignal,preview=false)=>fetchBlob(`${root}/${id}/photos/${photoId}${preview?"?preview=true":""}`,user,signal);
export type UpgFilter={nonconformanceType:'all'|NonconformanceType;basis:'occurrenceDate'|'completionDate';from:string;to:string;type:string;q:string};
export const emptyUpgFilter:UpgFilter={nonconformanceType:'all',basis:'occurrenceDate',from:'',to:'',type:'all',q:''};
export function filterUpg(items:UpgItem[],filter:UpgFilter,completed:boolean){return items.filter(x=>{const date=filter.basis==='occurrenceDate'?x.occurrenceDate:x.action?.completionDate;return Boolean(x.action)===completed&&(filter.nonconformanceType==='all'||nonconformanceTypeOf(x)===filter.nonconformanceType)&&(filter.type==='all'||x.occurrenceType===filter.type)&&(!filter.from||(!!date&&date>=filter.from))&&(!filter.to||(!!date&&date<=filter.to))&&[x.serial,x.content,x.action?.content??''].join(' ').toLocaleLowerCase().includes(filter.q.trim().toLocaleLowerCase());});}
export async function previewUpgPhoto(user:string|undefined,file:File,signal:AbortSignal):Promise<Blob>{
 const body=new FormData();body.append('photo',file,file.name);
 const result=await fetchJson<{contentType:string;base64:string}>(`${root}/photos/preview`,user,{method:'POST',body,signal});
 return new Blob([Uint8Array.from(atob(result.base64),c=>c.charCodeAt(0))],{type:result.contentType});
}
export async function readUpgFiles(files:File[]):Promise<UpgPhotoInput[]>{return Promise.all(files.map(async file=>{
 const base64=await new Promise<string>((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(String(r.result).split(',')[1]);r.onerror=()=>reject(new Error('사진을 읽지 못했습니다.'));r.onabort=()=>reject(new Error('사진 읽기를 취소했습니다.'));r.readAsDataURL(file);});
 return{fileName:file.name,contentType:normalizePhotoType(file.type)||'application/octet-stream',base64};
}));}
