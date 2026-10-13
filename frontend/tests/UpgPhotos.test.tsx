import {fireEvent,render,screen,waitFor} from '@testing-library/react';
import {beforeEach,expect,it,vi} from 'vitest';
import {UpgSavedPhoto,UpgPhotoPreview} from '../src/UpgPhotos';
import * as api from '../src/g2Nonconformances';
vi.mock('../src/g2Nonconformances',()=>({getUpgPhoto:vi.fn(),previewUpgPhoto:vi.fn()}));
beforeEach(()=>{URL.createObjectURL=vi.fn(()=>'blob:photo');URL.revokeObjectURL=vi.fn();vi.clearAllMocks();});
it('one failed saved photo does not hide another, and retries only the failed one',async()=>{
 vi.mocked(api.getUpgPhoto).mockImplementation(async(_u,_i,id)=>{if(id==='bad')throw new Error('failed');return new Blob(['image']);});
 render(<><UpgSavedPhoto user="quality" itemId="case" photo={{id:'bad',fileName:'bad.png',contentType:'image/png'}}/><UpgSavedPhoto user="quality" itemId="case" photo={{id:'ok',fileName:'ok.png',contentType:'image/png'}}/></>);
 expect(await screen.findByRole('img',{name:'ok.png'})).toBeVisible();
 fireEvent.click(await screen.findByRole('button',{name:'사진 다시 불러오기'}));
 await waitFor(()=>expect(api.getUpgPhoto).toHaveBeenCalledTimes(3));
 expect(vi.mocked(api.getUpgPhoto).mock.calls.filter(c=>c[2]==='ok')).toHaveLength(1);
});
it('HEIC selection uses the quality preview API and aborts when dismissed',async()=>{
 let signal:AbortSignal|undefined;
 vi.mocked(api.previewUpgPhoto).mockImplementation((_u,_f,s)=>{signal=s;return new Promise(()=>{});});
 const file=new File(['heic'],'phone.heic',{type:'image/heic'});
 const {unmount}=render(<UpgPhotoPreview user="quality" file={file}/>);
 expect(api.previewUpgPhoto).toHaveBeenCalledWith('quality',file,expect.any(AbortSignal));
 unmount();expect(signal?.aborted).toBe(true);
});
it('saved HEIC requests a display preview without changing stored photo identity',async()=>{
 vi.mocked(api.getUpgPhoto).mockResolvedValue(new Blob(['jpeg']));
 render(<UpgSavedPhoto user="quality" itemId="case" photo={{id:'heic',fileName:'phone.heic',contentType:'image/heic'}}/>);
 await screen.findByRole('img');expect(api.getUpgPhoto).toHaveBeenCalledWith('quality','case','heic',expect.any(AbortSignal),true);
});

it('blank browser MIME is normalized for the shared server decoder',async()=>{
 const actual=await vi.importActual<typeof import('../src/g2Nonconformances')>('../src/g2Nonconformances');
 const [photo]=await actual.readUpgFiles([new File(['photo'],'camera.heic')]);
 expect(photo.contentType).toBe('application/octet-stream');expect(photo.base64).toBeTruthy();
});
