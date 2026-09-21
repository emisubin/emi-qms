import { useCallback, useEffect, useRef, useState } from "react";
import type { IScannerControls } from "@zxing/browser";
import { busbarApi, busbarDateTime, busbarLabelState, type BusbarProduct, type BusbarLabelEvent } from "./interiorBusbar";
import { BusbarDialog } from "./InteriorBusbarPage";

const message = (error: unknown) => error instanceof Error ? error.message : "처리하지 못했습니다. 다시 시도해 주세요.";

export function BusbarLabelHistory({ user, product, onClose }: { user: string; product: BusbarProduct; onClose: () => void }) {
  const heading = useRef<HTMLHeadingElement>(null);
  const [events, setEvents] = useState<BusbarLabelEvent[]>();
  const [error, setError] = useState("");
  useEffect(() => { let active = true; void busbarApi.labelHistory(user, product.id).then(rows => { if (active) setEvents(rows); }, err => { if (active) setError(message(err)); }); return () => { active = false; }; }, [user, product.id]);
  return <BusbarDialog label={`${product.number} 라벨 이력`} heading={heading} busy={false} onClose={onClose} closeLabel="라벨 이력 닫기">
    {error && <p role="alert">{error}</p>}
    {!events && !error && <p role="status">이력을 불러오는 중입니다.</p>}
    {events?.length === 0 && <p>기록된 출력·부착 이력이 없습니다. 기존 라벨은 실물을 확인해 주세요.</p>}
    <ol className="busbar-label-history">{events?.map(event => <li key={event.id}><strong>{event.action === "Printed" ? "출력 확인 / 재출력 확인" : "부착 완료"}</strong><span>{event.actorDisplayName} · {busbarDateTime(event.createdAtUtc)}</span></li>)}</ol>
  </BusbarDialog>;
}

export function BusbarAttachmentDialog({ user, products: initial, entry = false, onClose, onChanged }: {
  user: string; products: BusbarProduct[]; entry?: boolean; onClose: () => void; onChanged: () => void;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  const [products, setProducts] = useState(initial);
  const [selected, setSelected] = useState(initial.slice(0, 200).map(p => p.id));
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const lock = useRef(false);
  const mounted = useRef(true);
  const request = useRef<{ key: string; id: string } | undefined>(undefined);
  const video = useRef<HTMLVideoElement>(null);
  const controls = useRef<IScannerControls | undefined>(undefined);
  const generation = useRef(0);
  const [camera, setCamera] = useState(false);
  const stop = useCallback(() => { generation.current++; controls.current?.stop(); controls.current = undefined; setCamera(false); }, []);
  useEffect(() => { const cameraGeneration = generation; mounted.current = true; const hidden = () => { if (document.hidden) stop(); }; document.addEventListener("visibilitychange", hidden); return () => { mounted.current = false; cameraGeneration.current++; controls.current?.stop(); document.removeEventListener("visibilitychange", hidden); }; }, [stop]);
  async function resolve(raw: string) {
    if (!raw.trim() || lock.current) return;
    lock.current = true; setBusy(true); setError(""); setNotice("");
    try {
      const row = await busbarApi.resolveLabel(user, raw.trim());
      if (!mounted.current) return;
      if (row.labelState === "Attached") { setNotice(`${row.number}은 이미 부착 완료되었습니다.`); return; }
      if (entry && !products.some(p => p.id === row.id)) { setError("이번 안내 목록에 없는 패널입니다. 생산 메뉴에서 부착 확인해 주세요."); return; }
      if (!selected.includes(row.id) && selected.length >= 200) { setError("한 번에 최대 200개까지 선택할 수 있습니다. 현재 선택을 저장하거나 일부 해제해 주세요."); return; }
      setProducts(rows => rows.some(p => p.id === row.id) ? rows : [...rows, row]);
      setSelected(ids => [...new Set([...ids, row.id])].slice(0, 200)); setCode(""); setNotice(`${row.number} 선택됨 · 실물 부착을 확인한 뒤 저장하세요.`);
    } catch (err) { if (mounted.current) setError(message(err)); }
    finally { lock.current = false; if (mounted.current) setBusy(false); }
  }
  const resolveRef = useRef(resolve); useEffect(() => { resolveRef.current = resolve; });
  async function start() {
    if (camera || !video.current) return;
    const current = ++generation.current; setCamera(true); setError("");
    try {
      const { BrowserQRCodeReader } = await import("@zxing/browser");
      if (current !== generation.current || !video.current) return;
      const scanner = await new BrowserQRCodeReader().decodeFromConstraints({ audio: false, video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } } }, video.current, result => {
        if (!result || current !== generation.current) return;
        stop(); void resolveRef.current(result.getText());
      });
      if (current !== generation.current) scanner.stop(); else controls.current = scanner;
    } catch { if (mounted.current && current === generation.current) { stop(); setError("카메라를 사용할 수 없습니다. 권한을 확인하거나 패널 번호를 입력해 주세요."); } }
  }
  async function save() {
    if (!selected.length || lock.current) return;
    lock.current = true; setBusy(true); setError(""); stop();
    const key = [...selected].sort().join(",");
    if (request.current?.key !== key) request.current = { key, id: crypto.randomUUID() };
    try {
      await busbarApi.write(user, "/labels/attached", { requestId: request.current.id, productIds: selected });
      if (!mounted.current) return;
      const remaining = products.filter(p => !selected.includes(p.id));
      setProducts(remaining); setSelected([]); request.current = undefined;
      setNotice(`${selected.length}개 패널의 부착을 확인했습니다.`); onChanged();
      if (!remaining.length) onClose();
    } catch (err) { if (mounted.current) setError(message(err)); }
    finally { lock.current = false; if (mounted.current) setBusy(false); }
  }
  const dates = [...new Set(products.map(p => p.planDate ?? "계획일 미지정"))].sort();
  return <BusbarDialog label={entry ? "출력한 QR, 부착하셨나요?" : "QR 부착 확인"} busy={busy} heading={heading} onClose={() => { stop(); onClose(); }} closeLabel={entry ? "나중에 확인" : "부착 확인 닫기"} className="busbar-label-dialog">
    <p>{entry ? "내 계정으로 출력 확인한 미부착 패널입니다." : "패널 번호와 실물 라벨을 맞춰 확인하세요."} {products.length}개</p>
    <form className="busbar-label-lookup" onSubmit={event => { event.preventDefault(); void resolve(code); }}><label>패널 번호 또는 QR 주소<input value={code} onChange={e => setCode(e.target.value)} placeholder="예: 1 또는 IB-00000001" disabled={busy} /></label><button disabled={busy || !code.trim()}>찾기</button><button type="button" disabled={busy || camera} onClick={() => void start()}>QR 스캔</button></form>
    <video ref={video} playsInline muted hidden={!camera} className="busbar-label-camera" />
    {camera && <button type="button" onClick={stop}>카메라 닫기</button>}
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {products.length > 200 && <p>한 번에 최대 200개씩 부착 확인합니다. 나머지는 저장 후 이어서 선택해 주세요.</p>}
    <button type="button" disabled={busy} onClick={() => setSelected(selected.length ? [] : products.slice(0, 200).map(p => p.id))}>{selected.length ? "선택 해제" : "모두 선택 (최대 200개)"}</button>
    <div className="busbar-label-groups">{dates.map(date => <section key={date}><h4>생산계획 {date}</h4>{products.filter(p => (p.planDate ?? "계획일 미지정") === date).map(p => <label className="busbar-label-row" key={p.id}><input type="checkbox" checked={selected.includes(p.id)} disabled={busy || (!selected.includes(p.id) && selected.length >= 200)} onChange={e => setSelected(ids => e.target.checked ? [...ids, p.id] : ids.filter(id => id !== p.id))} /><span><strong>{p.number}</strong><small>{p.productFamilyName} · {busbarLabelState(p.labelState)}</small></span></label>)}</section>)}</div>
    <div className="busbar-label-footer"><button disabled={busy} onClick={() => { stop(); onClose(); }}>{entry ? "나중에 확인" : "취소"}</button><button disabled={busy || !selected.length} onClick={() => void save()}>선택 {selected.length}개 부착 완료</button></div>
  </BusbarDialog>;
}

export function BusbarLabelEntryPrompt({ user, scope, mobile }: { user: string; scope: string; mobile: boolean }) {
  const [pending, setPending] = useState<BusbarProduct[]>([]);
  const [open, setOpen] = useState(false);
  const [entryVersion, setEntryVersion] = useState(0);
  useEffect(() => {
    if (!mobile) return;
    let active = true; let checked = false; let hiddenAt = 0;
    const check = async () => {
      if (checked || document.hidden) return;
      checked = true;
      try {
        const access = await busbarApi.access(user);
        if (!active || !access.production) return;
        const rows = await busbarApi.pendingLabels(user);
        if (active) { setPending(rows); setEntryVersion(version => version + 1); setOpen(rows.length > 0); }
      } catch { /* Entry is supplementary; explicit production controls retain errors/retry. */ }
    };
    const visibility = () => {
      if (document.hidden) hiddenAt = Date.now();
      else if (hiddenAt && Date.now() - hiddenAt >= 60_000) { checked = false; void check(); }
    };
    void check(); document.addEventListener("visibilitychange", visibility);
    return () => { active = false; document.removeEventListener("visibilitychange", visibility); };
  }, [user, scope, mobile]);
  return open ? <BusbarAttachmentDialog key={`${scope}:${entryVersion}`} user={user} products={pending} entry onClose={() => setOpen(false)} onChanged={() => window.dispatchEvent(new Event("busbar-labels-changed"))} /> : null;
}
