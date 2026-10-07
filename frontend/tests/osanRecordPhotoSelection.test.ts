import { describe, it, expect } from 'vitest';
import { selectOsanRecordPhotos } from '../src/osanRecordPhotoSelection';
function input(files: File[]) { return { files, value:'selected.jpg' } as unknown as HTMLInputElement; }
describe('공통 사진 선택',()=>{
 it('선택을 비워 제거한 동일 파일을 다시 선택할 수 있게 한다',()=>{
  const file=new File(['a'],'a.jpg',{type:'image/jpeg'});const element=input([file]);
  expect(selectOsanRecordPhotos(element,[]).files).toEqual([file]);expect(element.value).toBe('');
  expect(selectOsanRecordPhotos(element,[]).files).toEqual([file]);
 });
 it('촬영을 누적하고 앨범 선택은 기존 선택을 교체한다',()=>{
  const a=new File(['a'],'a.jpg',{type:'image/jpeg'}), b=new File(['b'],'b.jpg',{type:'image/jpeg'});
  expect(selectOsanRecordPhotos(input([b]),[a],true).files).toEqual([a,b]);
  expect(selectOsanRecordPhotos(input([b]),[a]).files).toEqual([b]);
 });
 it('제한을 넘는 촬영도 입력을 비우고 오류를 반환한다',()=>{
  const files=Array.from({length:5},(_,i)=>new File(['a'],`${i}.jpg`,{type:'image/jpeg'}));const element=input([files[0]]);
  expect(selectOsanRecordPhotos(element,files,true).error).toBeTruthy();expect(element.value).toBe('');
 });
});
