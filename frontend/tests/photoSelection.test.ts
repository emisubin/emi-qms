import {expect,it} from 'vitest';
import {validatePhotoSelection} from '../src/photoSelection';
const policy={count:10,total:20_000_000,types:['image/png','image/jpeg'],extensions:/\.png$/i,formatMessage:'format',totalMessage:'total'};
const file=(size:number,type='image/png')=>new File([new Uint8Array(size)],'photo.png',{type});
it('counts retained photos and bytes with new selection',()=>{
 expect(validatePhotoSelection([file(1)],policy,Array.from({length:10},()=>({byteSize:1})))).toContain('10');
 expect(validatePhotoSelection([file(1)],policy,[{byteSize:20_000_000}])).toBe('total');
 expect(validatePhotoSelection([file(1)],policy,[{byteSize:19_999_999}])).toBeNull();
});
it('accepts photos over 5MB while rejecting empty, aggregate excess and non-photo input',()=>{
 expect(validatePhotoSelection([file(0)],policy)).toContain('빈 파일');
 expect(validatePhotoSelection([file(5_000_001)],policy)).toBeNull();
 expect(validatePhotoSelection([file(20_000_001)],policy)).toBe('total');
 expect(validatePhotoSelection([file(1,'application/pdf')],policy)).toBe('format');
});

it('accepts JPG MIME alias and normalizes it without relaxing format validation',()=>{
 expect(validatePhotoSelection([new File(['jpg'],'camera.JPG',{type:'image/jpg'})],policy)).toBeNull();
 expect(validatePhotoSelection([new File(['jpg'],'camera.JPG',{type:'application/pdf'})],policy)).toBe('format');
});
