import { useEffect, useState } from "react";
import { busbarApi } from "./interiorBusbar";
import { DsActionFeedback, DsSurface } from "./design-system";
import { Table } from "./InteriorBusbarPage";

type Entry = { userId: string; displayName: string; departmentName?: string; access: string; automatic: boolean };
export function BusbarMasterAccess({user}: {user:string}) {
  const [entries,setEntries] = useState<Entry[]>([]);
  const [query,setQuery] = useState("");
  const [selected,setSelected] = useState<Entry>();
  const [access,setAccess] = useState("Read");
  const [reason,setReason] = useState("");
  const [busy,setBusy] = useState(false);
  const [error,setError] = useState("");
  const [revision,setRevision] = useState(0);
  useEffect(() => {
    let active=true;
    void busbarApi.masterAccess(user).then(rows=>{if(active)setEntries(rows);},()=>{if(active)setError("사용자 권한을 조회하지 못했습니다.");});
    return ()=>{active=false;};
  },[user,revision]);
  return <DsSurface label="기준정보 권한 설정">
    <h3>기준정보 권한 설정</h3>
    <p className="busbar-note">청주 관리자·총괄 관리자는 항상 접근합니다. 그 외 사용자는 지정된 권한으로만 기준정보를 이용할 수 있습니다.</p>
    {error && <DsActionFeedback tone="error" message={error} />}
    <label>사용자 검색<input value={query} onChange={e=>setQuery(e.target.value)} placeholder="이름 또는 부서" /></label>
    <Table headings={["사용자","부서","기준정보 권한","설정"]} rows={entries.filter(e=>`${e.displayName} ${e.departmentName ?? ""}`.includes(query)).map(e=>[
      e.displayName,e.departmentName || "미지정",e.automatic ? "관리자 · 조회·수정" : {None:"접근 불가",Read:"조회만",Edit:"조회·수정"}[e.access],
      e.automatic ? "항상 접근" : <button type="button" disabled={busy} onClick={()=>{setSelected(e);setAccess(e.access);setReason("");setError("");}}>권한 설정</button>
    ])} />
    {selected && <form className="busbar-form" onSubmit={async e=>{
      e.preventDefault();if(busy)return;setBusy(true);setError("");
      try {await busbarApi.write(user,"/master-access",{userId:selected.userId,access,reason},"PUT");setSelected(undefined);setRevision(r=>r+1);}
      catch(e){setError(e instanceof Error ? e.message : "저장하지 못했습니다.");}
      finally{setBusy(false);}
    }}>
      <h4>{selected.displayName} · 기준정보 권한</h4>
      <label>접근 권한<select disabled={busy} value={access} onChange={e=>setAccess(e.target.value)}><option value="None">접근 불가</option><option value="Read">조회만</option><option value="Edit">조회·수정</option></select></label>
      <label>변경 사유<input required maxLength={200} disabled={busy} value={reason} onChange={e=>setReason(e.target.value)} /></label>
      <div className="busbar-form-actions"><button disabled={busy || !reason.trim()}>저장</button><button type="button" disabled={busy} onClick={()=>setSelected(undefined)}>닫기</button></div>
    </form>}
  </DsSurface>;
}
