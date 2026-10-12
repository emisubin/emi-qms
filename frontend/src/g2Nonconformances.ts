import { fetchBlob, fetchJson } from './api';
export type UpgPhoto = { id:string; fileName:string; contentType:string };
export type UpgPhotoInput = { id?:string; fileName:string; contentType:string; base64?:string };
export type UpgAction = {completionDate:string;content:string;photos:UpgPhoto[]};
export type UpgItem = {id:string;serial:string;occurrenceDate:string;occurrenceType:'production'|'supplier';content:string;initialStock:boolean;photos:UpgPhoto[];action:UpgAction|null;version:number};
export type UpgList = {canEdit:boolean;items:UpgItem[]};
export type UpgFirstInput = {serial:string;occurrenceDate:string;occurrenceType:'production'|'supplier';content:string;initialStock:boolean;photos:UpgPhotoInput[];expectedVersion?:number};
export type UpgActionInput = {completionDate:string;content:string;photos:UpgPhotoInput[];expectedVersion:number};
const root='/api/g2/nonconformances';
export const getUpg=(user:string|undefined,signal?:AbortSignal)=>fetchJson<UpgList>(root,user,{signal});
export const saveUpg=(user:string|undefined,id:string|null,input:UpgFirstInput)=>fetchJson<UpgItem>(id?`${root}/${id}`:root,user,{method:id?'PUT':'POST',body:JSON.stringify(input)});
export const saveUpgAction=(user:string|undefined,id:string,input:UpgActionInput)=>fetchJson<UpgItem>(`${root}/${id}/action`,user,{method:'PUT',body:JSON.stringify(input)});
export const deleteUpg=(user:string|undefined,item:UpgItem,second:boolean)=>fetchJson(`${root}/${item.id}${second?'/action':''}?expectedVersion=${item.version}`,user,{method:'DELETE'});
export const getUpgPhoto=(user:string|undefined,id:string,photoId:string,signal:AbortSignal)=>fetchBlob(`${root}/${id}/photos/${photoId}`,user,signal);
export type UpgFilter={basis:'occurrenceDate'|'completionDate';from:string;to:string;type:string;q:string};
export const emptyUpgFilter:UpgFilter={basis:'occurrenceDate',from:'',to:'',type:'all',q:''};
export function filterUpg(items:UpgItem[],filter:UpgFilter,completed:boolean){return items.filter(x=>{const date=filter.basis==='occurrenceDate'?x.occurrenceDate:x.action?.completionDate;return Boolean(x.action)===completed&&(filter.type==='all'||x.occurrenceType===filter.type)&&(!filter.from||(!!date&&date>=filter.from))&&(!filter.to||(!!date&&date<=filter.to))&&[x.serial,x.content,x.action?.content??''].join(' ').toLocaleLowerCase().includes(filter.q.trim().toLocaleLowerCase());});}
export async function readUpgFiles(files:File[]):Promise<UpgPhotoInput[]>{return Promise.all(files.map(async file=>{if(!['image/jpeg','image/png','image/webp'].includes(file.type))throw new Error('JPG, PNG, WEBP 사진을 선택해 주세요.');if(file.size>5_000_000)throw new Error('사진 한 장은 5MB 이하여야 합니다.');const base64=await new Promise<string>((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(String(r.result).split(',')[1]);r.onerror=()=>reject(new Error('사진을 읽지 못했습니다.'));r.readAsDataURL(file);});return{fileName:file.name,contentType:file.type,base64};}));}
