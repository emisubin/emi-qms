// Serializes whole-day snapshots. A newer draft replaces only the queued snapshot,
// never an in-flight request. Failed snapshots remain available for explicit retry.
export function materialAutoSave<T>(write:(value:T)=>Promise<unknown>, notify:(state:'saving'|'saved'|'error', error?:unknown)=>void) {
  let pending:T|undefined;
  let running=false;
  let disposed=false;
  async function drain() {
    if(running||disposed||pending===undefined)return;
    running=true;
    notify('saving');
    try {
      while(!disposed&&pending!==undefined) {
        const snapshot=pending;
        pending=undefined;
        try { await write(snapshot); }
        catch(error) {
          if(!disposed) { if(pending===undefined)pending=snapshot; notify('error',error); }
          return;
        }
      }
      if(!disposed)notify('saved');
    } finally { running=false; }
  }
  return {
    enqueue(value:T){if(disposed)return;pending=value;void drain();},
    retry(){void drain();},
    dispose(){disposed=true;pending=undefined;},
  };
}
