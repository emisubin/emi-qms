type State='saving'|'unsaved'|null;
const guards=new Set<{state:()=>State;discard:()=>void}>();
export function registerMaterialNavigationGuard(state:()=>State,discard:()=>void){const guard={state,discard};guards.add(guard);return()=>{guards.delete(guard);};}
export function canLeaveMaterialDraft(){
 const pending=[...guards].filter(guard=>guard.state()!==null);
 if(pending.some(guard=>guard.state()==='saving')){
  window.alert('자재 실사값을 저장 중입니다. 저장이 끝난 후 이동해 주세요.');return false;
 }
 if(pending.length){
  if(!window.confirm('저장하지 못한 자재 실사값이 있습니다. 입력값을 포기하고 이동할까요?'))return false;
  pending.forEach(guard=>guard.discard());
 }
 return true;
}
