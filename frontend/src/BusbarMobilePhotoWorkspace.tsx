import { useEffect, useRef, useState } from "react";
import {
  busbarApi,
  busbarPhotoAccept,
  isBusbarHeicPhoto,
  validateBusbarPhoto,
  type BusbarMaster,
  type BusbarProduct,
} from "./interiorBusbar";

type PhotoSide = "front" | "back";
type Run = (
  action: () => Promise<unknown>,
  message?: string,
) => Promise<boolean>;

const sideName = (side: PhotoSide) => side === "front" ? "앞면" : "뒷면";

export function BusbarMobilePhotoWorkspace({
  user,
  product,
  workers,
  canWrite,
  busy,
  run,
}: {
  user: string;
  product: BusbarProduct;
  workers: BusbarMaster[];
  canWrite: boolean;
  busy: boolean;
  run: Run;
}) {
  const [workerId, setWorkerId] = useState(product.workerId ?? "");
  const [side, setSide] = useState<PhotoSide>("front");
  const [drafts, setDrafts] = useState<Partial<Record<PhotoSide, File>>>({});
  const [draftSrc, setDraftSrc] = useState<Partial<Record<PhotoSide, string>>>({});
  const [savedSrc, setSavedSrc] = useState<Partial<Record<PhotoSide, string>>>({});
  const [photoError, setPhotoError] = useState("");
  const [previewing, setPreviewing] = useState<Partial<Record<PhotoSide, boolean>>>({});
  const [zoomOpen, setZoomOpen] = useState(false);
  const cameraRef = useRef<HTMLInputElement>(null);
  const albumRef = useRef<HTMLInputElement>(null);
  const previewButtonRef = useRef<HTMLButtonElement>(null);
  const zoomCloseRef = useRef<HTMLButtonElement>(null);

  useEffect(() => setWorkerId(product.workerId ?? ""), [product.workerId]);
  useEffect(() => { if (zoomOpen) zoomCloseRef.current?.focus(); }, [zoomOpen]);
  useEffect(() => {
    let active = true;
    const urls: string[] = [];
    setSavedSrc({});
    setPhotoError("");
    void Promise.all(((["front", "back"] as PhotoSide[]).map(async (photoSide) => {
      const exists = photoSide === "front" ? product.hasFront : product.hasBack;
      if (!exists) return;
      try {
        const blob = await busbarApi.photo(user, product.id, photoSide);
        if (!active) return;
        const url = URL.createObjectURL(blob);
        urls.push(url);
        setSavedSrc((current) => ({ ...current, [photoSide]: url }));
      } catch (error) {
        if (active) setPhotoError(error instanceof Error ? error.message : "사진을 불러오지 못했습니다.");
      }
    })));
    return () => {
      active = false;
      urls.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [user, product.id, product.revision, product.hasFront, product.hasBack]);

  const draftUrls = useRef<Partial<Record<PhotoSide, string>>>({});
  const previewGeneration = useRef<Record<PhotoSide, number>>({ front: 0, back: 0 });
  useEffect(() => () => {
    previewGeneration.current.front += 1;
    previewGeneration.current.back += 1;
    Object.values(draftUrls.current).forEach((url) => URL.revokeObjectURL(url));
  }, []);

  const readOnly = !canWrite || product.status !== "Draft";
  const src = draftSrc[side] ?? savedSrc[side];
  const hasSavedPhoto = side === "front" ? product.hasFront : product.hasBack;
  const chooseFile = async (file?: File) => {
    if (!file || readOnly) return;
    const error = validateBusbarPhoto(file);
    if (error) { setPhotoError(error); return; }
    const selectedSide = side;
    const generation = ++previewGeneration.current[selectedSide];
    const previous = draftUrls.current[selectedSide];
    if (previous) URL.revokeObjectURL(previous);
    setDrafts((current) => ({ ...current, [selectedSide]: file }));
    setDraftSrc((current) => ({ ...current, [selectedSide]: undefined }));
    setPhotoError("");
    setPreviewing((current) => ({ ...current, [selectedSide]: true }));
    try {
      const preview = isBusbarHeicPhoto(file) ? await busbarApi.previewPhoto(user, product.id, file) : file;
      if (generation !== previewGeneration.current[selectedSide]) return;
      const url = URL.createObjectURL(preview);
      draftUrls.current[selectedSide] = url;
      setDraftSrc((current) => ({ ...current, [selectedSide]: url }));
    } catch (reason) {
      if (generation === previewGeneration.current[selectedSide]) setPhotoError(reason instanceof Error ? reason.message : "사진 미리보기를 준비하지 못했습니다. 다시 선택해 주세요.");
    } finally {
      if (generation === previewGeneration.current[selectedSide]) setPreviewing((current) => ({ ...current, [selectedSide]: false }));
    }
  };
  const save = async () => {
    const file = drafts[side];
    if (!file || !workerId || readOnly || busy) return;
    const saved = await run(
      () => busbarApi.uploadPhoto(user, product.id, side, file, workerId),
      `${sideName(side)} 사진을 저장했습니다. 앞면과 뒷면이 모두 저장되면 자동으로 생산 완료됩니다.`,
    );
    if (saved) {
      const url = draftUrls.current[side];
      if (url) URL.revokeObjectURL(url);
      draftUrls.current[side] = undefined;
      setDrafts((current) => ({ ...current, [side]: undefined }));
      setDraftSrc((current) => ({ ...current, [side]: undefined }));
    }
  };
  const closeZoom = () => {
    setZoomOpen(false);
    requestAnimationFrame(() => previewButtonRef.current?.focus({ preventScroll: true }));
  };

  return (
    <section className="busbar-mobile-photo-workspace" aria-label="모바일 생산 사진">
      {!readOnly && (
        <label className="busbar-mobile-worker">
          실제 제조 작업자
          <select value={workerId} disabled={busy} onChange={(event) => setWorkerId(event.target.value)}>
            <option value="">작업자를 선택하세요</option>
            {workers
              .filter((worker) => !worker.isDeleted && (worker.isActive || worker.id === product.workerId))
              .map((worker) => <option key={worker.id} value={worker.id}>{worker.name}{worker.isActive ? "" : " · 사용 중지"}</option>)}
          </select>
        </label>
      )}
      <div className="busbar-mobile-photo-tabs" role="tablist" aria-label="사진 면 선택">
        {(["front", "back"] as PhotoSide[]).map((photoSide) => {
          const saved = photoSide === "front" ? product.hasFront : product.hasBack;
          return <button key={photoSide} type="button" role="tab" aria-selected={side === photoSide}
            className={side === photoSide ? "active" : ""} onClick={() => { setSide(photoSide); setZoomOpen(false); }}>
            {sideName(photoSide)} <small>{drafts[photoSide] ? "저장 전" : saved ? "등록됨" : "미등록"}</small>
          </button>;
        })}
      </div>
      <button ref={previewButtonRef} type="button" className="busbar-mobile-photo-preview" disabled={!src}
        aria-label={src ? `${sideName(side)} 사진 크게 보기` : `${sideName(side)} 사진 미등록`}
        onClick={() => setZoomOpen(true)}>
        {src ? <img src={src} alt={`${sideName(side)} ${drafts[side] ? "저장 전 미리보기" : "등록 사진"}`} onError={() => { setDraftSrc((current) => ({ ...current, [side]: undefined })); setPhotoError("이 사진의 미리보기를 표시할 수 없습니다. 다른 사진을 선택해 주세요."); }} /> :
          <span><svg viewBox="0 0 48 48" aria-hidden="true"><rect x="5" y="12" width="38" height="29" rx="4" /><path d="m15 12 4-6h10l4 6" /><circle cx="24" cy="26" r="8" /></svg>{previewing[side] ? "사진 미리보기 준비 중…" : "등록된 사진이 없습니다."}</span>}
      </button>
      {src && <p className="busbar-mobile-photo-hint">사진을 누르면 크게 볼 수 있습니다.</p>}
      {!readOnly && (
        <>
          <div className="busbar-mobile-photo-actions">
            <button type="button" disabled={busy || !workerId} onClick={() => cameraRef.current?.click()}>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 6l2-3h4l2 3h4v14H4V6z" /><circle cx="12" cy="12" r="4" /></svg>카메라 촬영
            </button>
            <button type="button" disabled={busy || !workerId} onClick={() => albumRef.current?.click()}>
              <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2" /><circle cx="8" cy="8" r="2" /><path d="m3 18 6-6 4 4 3-3 5 5" /></svg>사진 선택
            </button>
          </div>
          <input ref={cameraRef} className="busbar-hidden-file" type="file" aria-label={`모바일 ${sideName(side)} 카메라 촬영`}
            accept={busbarPhotoAccept} capture="environment" disabled={busy || !workerId} onChange={(event) => { void chooseFile(event.target.files?.[0]); event.target.value = ""; }} />
          <input ref={albumRef} className="busbar-hidden-file" type="file" aria-label={`모바일 ${sideName(side)} 사진 선택`}
            accept={busbarPhotoAccept} disabled={busy || !workerId} onChange={(event) => { void chooseFile(event.target.files?.[0]); event.target.value = ""; }} />
          <button type="button" className="busbar-mobile-photo-save" disabled={busy || !workerId || !drafts[side] || !draftSrc[side] || previewing[side]} onClick={() => void save()}>
            {busy ? "저장 중…" : `${sideName(side)} 사진 저장`}
          </button>
          <p className="busbar-note" aria-live="polite">
            {!workerId ? "작업자를 선택하면 촬영과 사진 선택이 가능합니다." : drafts[side] ? "저장 전 미리보기입니다. 사진을 확인한 뒤 저장하세요." : "앞면과 뒷면 사진을 각각 저장하면 자동으로 생산 완료됩니다."}
          </p>
        </>
      )}
      {readOnly && <p className="busbar-note">완료된 생산 사진은 변경할 수 없습니다.</p>}
      {photoError && <p className="busbar-negative" role="alert">{photoError}</p>}
      {zoomOpen && src && (
        <div className="busbar-mobile-photo-zoom" role="dialog" aria-modal="true" aria-label={`${sideName(side)} 사진 크게 보기`}
          onMouseDown={(event) => { if (event.target === event.currentTarget) closeZoom(); }}
          onKeyDown={(event) => {
            event.stopPropagation();
            if (event.key === "Escape") { event.preventDefault(); closeZoom(); }
            if (event.key === "Tab") { event.preventDefault(); zoomCloseRef.current?.focus(); }
          }}>
          <div><header><strong>{sideName(side)} 사진</strong><button ref={zoomCloseRef} type="button" aria-label="확대 사진 닫기" onClick={closeZoom}>닫기</button></header><img src={src} alt={`${sideName(side)} ${hasSavedPhoto ? "등록 사진" : "저장 전 미리보기"}`} /></div>
        </div>
      )}
    </section>
  );
}
