export function normalizePhotoType(type:string):string { const normalized=type.trim().toLowerCase(); return normalized==='image/jpg'?'image/jpeg':normalized; }
export interface PhotoPolicy { count:number; total:number; types:string[]; extensions:RegExp; formatMessage:string; totalMessage:string }
export function validatePhotoSelection(files:readonly File[],policy:PhotoPolicy,retained:readonly {byteSize?:number}[]=[]):string|null {
 if(files.length+retained.length>policy.count)return `사진은 최대 ${policy.count}장까지 선택할 수 있습니다.`;
 if(files.some(f=>!policy.types.includes(normalizePhotoType(f.type))&&!(['','application/octet-stream'].includes(normalizePhotoType(f.type))&&policy.extensions.test(f.name))))return policy.formatMessage;
 if(files.some(f=>f.size===0))return '빈 파일은 첨부할 수 없습니다.';
 if(files.reduce((n,f)=>n+f.size,0)+retained.reduce((n,f)=>n+(f.byteSize??0),0)>policy.total)return policy.totalMessage;
 return null;
}
