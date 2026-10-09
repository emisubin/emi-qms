import {it,expect,vi} from 'vitest';
import {registerMaterialNavigationGuard,canLeaveMaterialDraft} from '../src/materialNavigationGuard';
it('blocks in-flight writes, allows explicit discard after failure, and releases after unmount',()=>{
 const alert=vi.spyOn(window,'alert').mockImplementation(()=>{});const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);let state:'saving'|'unsaved'|null='saving';const discard=vi.fn(()=>{state=null});const release=registerMaterialNavigationGuard(()=>state,discard);
 expect(canLeaveMaterialDraft()).toBe(false);expect(alert).toHaveBeenCalledTimes(1);expect(confirm).not.toHaveBeenCalled();state='unsaved';expect(canLeaveMaterialDraft()).toBe(false);expect(discard).not.toHaveBeenCalled();confirm.mockReturnValue(true);expect(canLeaveMaterialDraft()).toBe(true);expect(discard).toHaveBeenCalledTimes(1);state='saving';release();expect(canLeaveMaterialDraft()).toBe(true);vi.restoreAllMocks();
});
