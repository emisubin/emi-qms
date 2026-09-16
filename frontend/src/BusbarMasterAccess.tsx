import { useEffect, useRef, useState } from "react";
import { busbarApi } from "./interiorBusbar";
import { DsActionFeedback, DsSurface, DsToolbar } from "./design-system";
import { BusbarDialog, Table } from "./InteriorBusbarPage";

type Entry = { userId: string; displayName: string; departmentName?: string; access: string; automatic: boolean };
const accessLabel = (entry: Entry) => entry.automatic ? "관리자 · 조회·수정" : entry.access === "Edit" ? "조회·수정" : "조회";
export function BusbarMasterAccess({user}: {user:string}) {
  const [entries,setEntries] = useState<Entry[]>([]);
  const [query,setQuery] = useState("");
  const [open,setOpen] = useState(false);
  const [busy,setBusy] = useState(false);
  const [ready,setReady] = useState(false);
  const [error,setError] = useState("");
  const [notice,setNotice] = useState("");
  const heading = useRef<HTMLHeadingElement>(null);
  const locked = useRef(false);
  const mounted = useRef(false);
  const generation = useRef(0);
  useEffect(() => {
    mounted.current=true;
    let active=true;
    const current=++generation.current;
    void busbarApi.masterAccess(user).then(rows=>{if(active && current===generation.current){setEntries(rows);setReady(true);}},()=>{if(active && current===generation.current)setError("사용자 권한을 조회하지 못했습니다. 권한 설정 수정을 눌러 다시 확인하세요.");});
    return ()=>{active=false;mounted.current=false;};
  },[user]);
  async function refresh() {
    if(locked.current)return;
    locked.current=true;setBusy(true);setReady(false);setError("");
    const current=++generation.current;
    try {const rows=await busbarApi.masterAccess(user);if(mounted.current && current===generation.current){setEntries(rows);setReady(true);}}
    catch {if(mounted.current)setError("사용자 권한을 조회하지 못했습니다. 다시 불러와 주세요.");}
    finally {locked.current=false;if(mounted.current)setBusy(false);}
  }
  async function toggle(entry:Entry,field:"Read"|"Edit") {
    if(locked.current || !ready || entry.automatic)return;
    // Removing view removes all access; removing edit retains view.
    const access=field === "Read" ? entry.access === "None" ? "Read" : "None" : entry.access === "Edit" ? "Read" : "Edit";
    generation.current++;
    locked.current=true;setBusy(true);setError("");setNotice("");
    try {
      await busbarApi.write(user,"/master-access",{userId:entry.userId,access,reason:`권한 설정에서 ${access === "None" ? "접근 해제" : access === "Edit" ? "조회·수정 지정" : "조회 지정"}`},"PUT");
      if(mounted.current){setEntries(rows=>rows.map(row=>row.userId === entry.userId ? {...row,access} : row));setNotice(`${entry.displayName} · ${access === "None" ? "권한 해제" : access === "Edit" ? "조회·수정 적용" : "조회 적용"}`);}
    } catch(e) {
      // A failed response can follow a committed write. Re-read before allowing another toggle.
      if(mounted.current)setReady(false);
      try {const rows=await busbarApi.masterAccess(user);if(mounted.current){setEntries(rows);setReady(true);}}
      catch { /* Keep toggles blocked until an explicit refresh succeeds. */ }
      if(mounted.current)setError(e instanceof Error ? e.message : "저장 결과를 확인하지 못했습니다. 다시 불러와 현재 권한을 확인하세요.");
    } finally {locked.current=false;if(mounted.current)setBusy(false);}
  }
  const granted=entries.filter(e=>e.automatic || e.access !== "None");
  return <DsSurface label="기준정보 권한 설정">
    <DsToolbar><h3>기준정보 접근 사용자</h3><button type="button" disabled={busy} onClick={()=>{setOpen(true);setQuery("");setNotice("");void refresh();}}>권한 설정 수정</button></DsToolbar>
    <p className="busbar-note">청주 관리자·총괄 관리자는 항상 접근합니다.</p>
    {!open && error && <DsActionFeedback tone="error" message={error} />}
    <ul className="busbar-access-granted" aria-label="기준정보 접근 사용자">
      {granted.map(entry=><li key={entry.userId}><strong>{entry.displayName}</strong><span>{accessLabel(entry)}</span></li>)}
    </ul>
    {ready && granted.length === 0 && <p className="busbar-note">지정된 사용자가 없습니다.</p>}
    {open && <BusbarDialog label="기준정보 권한 설정 수정" busy={busy} heading={heading} onClose={()=>setOpen(false)} closeLabel="권한 설정 닫기" className="busbar-access-dialog">
      <p className="busbar-note">선택하면 즉시 적용되고 다시 선택하면 해제됩니다. 수정은 조회를 포함하며, 조회를 해제하면 수정 권한도 해제됩니다.</p>
      <label>사용자 검색<input type="search" value={query} onChange={e=>setQuery(e.target.value)} placeholder="이름 또는 부서" /></label>
      {error && <DsActionFeedback tone="error" message={error} />}
      <p role="status" className="busbar-note">{busy ? "권한 확인·적용 중…" : notice}</p>
      {!ready && !busy && <button type="button" onClick={()=>void refresh()}>다시 불러오기</button>}
      <div className="busbar-access-list">
        <Table headings={["사용자","부서","조회","수정"]} rows={entries.filter(e=>`${e.displayName} ${e.departmentName ?? ""}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase())).map(entry=>[
          <span>{entry.displayName}{entry.automatic && <small className="busbar-note"> · 항상 접근</small>}</span>,entry.departmentName || "미지정",
          <label className="busbar-access-toggle"><input type="checkbox" aria-label={`${entry.displayName} 조회 권한`} checked={entry.automatic || entry.access !== "None"} disabled={busy || !ready || entry.automatic} onChange={()=>void toggle(entry,"Read")} /></label>,
          <label className="busbar-access-toggle"><input type="checkbox" aria-label={`${entry.displayName} 수정 권한`} checked={entry.automatic || entry.access === "Edit"} disabled={busy || !ready || entry.automatic} onChange={()=>void toggle(entry,"Edit")} /></label>
        ])} />
      </div>
    </BusbarDialog>}
  </DsSurface>;
}
