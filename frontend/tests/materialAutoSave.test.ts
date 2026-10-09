import {describe,it,expect,vi} from 'vitest';
import {materialAutoSave} from '../src/materialAutoSave';
const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
describe('material snapshot autosave',()=>{
 it('serializes requests and coalesces rapid edits to the latest complete snapshot',async()=>{
  let finish!:()=>void;const write=vi.fn().mockImplementationOnce(()=>new Promise<void>(r=>finish=r)).mockResolvedValue(undefined);const state=vi.fn();const queue=materialAutoSave(write,state);
  queue.enqueue({a:1,b:null});queue.enqueue({a:12,b:null});queue.enqueue({a:123,b:0});expect(write).toHaveBeenCalledTimes(1);finish();await tick();expect(write.mock.calls.map(c=>c[0])).toEqual([{a:1,b:null},{a:123,b:0}]);expect(state.mock.calls.map(c=>c[0])).toEqual(['saving','saved']);
 });
 it('retains latest queued input on failure and retries only on request',async()=>{
  let fail!:(e:Error)=>void;const write=vi.fn().mockImplementationOnce(()=>new Promise((_,r)=>fail=r)).mockResolvedValue(undefined);const state=vi.fn();const queue=materialAutoSave(write,state);queue.enqueue(1);queue.enqueue(2);fail(new Error('offline'));await tick();expect(write).toHaveBeenCalledTimes(1);expect(state).toHaveBeenLastCalledWith('error',expect.any(Error));queue.retry();await tick();expect(write).toHaveBeenLastCalledWith(2);expect(state).toHaveBeenLastCalledWith('saved');
 });
 it('does not send queued writes or report stale success after leaving the scope',async()=>{
  let finish!:()=>void;const write=vi.fn(()=>new Promise<void>(r=>finish=r));const state=vi.fn();const queue=materialAutoSave(write,state);queue.enqueue(1);queue.enqueue(2);queue.dispose();finish();await tick();expect(write).toHaveBeenCalledTimes(1);expect(state.mock.calls.map(c=>c[0])).toEqual(['saving']);
 });
});
