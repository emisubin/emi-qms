export interface PhotoPolicy { count:number; total:number; perFile?:number; types:string[]; extensions:RegExp; formatMessage:string; totalMessage:string }
export function validatePhotoSelection(files:readonly File[],policy:PhotoPolicy,retained:readonly {byteSize?:number}[]=[]):string|null {
 if(files.length+retained.length>policy.count)return `사진은 최대 ${policy.count}장까지 선택할 수 있습니다.`;
 if(files.some(f=>!policy.types.includes(f.type.toLowerCase())&&!(['','application/octet-stream'].includes(f.type)&&policy.extensions.test(f.name))))return policy.formatMessage;
 if(files.some(f=>f.size===0))return '빈 파일은 첨부할 수 없습니다.';
 if(policy.perFile&&files.some(f=>f.size>policy.perFile!))return '사진 한 장은 5MB 이하여야 합니다.';
 if(files.reduce((n,f)=>n+f.size,0)+retained.reduce((n,f)=>n+(f.byteSize??0),0)>policy.total)return policy.totalMessage;
 return null;
}
