import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import type { IScannerControls } from "@zxing/browser";
import { ApiError } from "./api";
import {
  DsActionFeedback,
  DsBadge,
  DsPageHeader,
  DsReadOnlyBanner,
  DsStatePanel,
  DsSurface,
  DsToolbar,
} from "./design-system";
import {
  busbarApi,
  busbarDateTime,
  busbarNumber as n,
  type BusbarProduct,
  type BusbarProjectDetail,
  type BusbarProjectShipment,
  type BusbarWorkspace,
} from "./interiorBusbar";
import {
  BusbarDialog,
  CommercialPreview,
  EcountStatus,
  PhotoPreview,
  Table,
} from "./InteriorBusbarPage";
import "./interior-busbar.css";

const failure = (error: unknown) =>
  error instanceof Error
    ? error.message
    : "처리하지 못했습니다. 다시 시도해 주세요.";

export function InteriorBusbarProjectDetailPage({
  developmentUserKey: user,
  projectId,
  onBack,
}: {
  developmentUserKey: string;
  projectId: string;
  onBack: () => void;
}) {
  const [detail, setDetail] = useState<BusbarProjectDetail>();
  const [workspace, setWorkspace] = useState<BusbarWorkspace>();
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [scanOpen, setScanOpen] = useState(false);
  const [reverseShipment, setReverseShipment] = useState<BusbarProjectShipment>();
  const [viewPanel, setViewPanel] = useState<BusbarProduct>();
  const [feedback, setFeedback] = useState("");
  const [loadRevision, setLoadRevision] = useState(0);
  const generation = useRef(0);

  const load = useCallback(async () => {
    const current = ++generation.current;
    setError("");
    setDenied(false);
    setNotFound(false);
    try {
      const [nextDetail, nextWorkspace] = await Promise.all([
        busbarApi.projectDetail(user, projectId),
        busbarApi.workspace(user),
      ]);
      if (current !== generation.current) return;
      setDetail(nextDetail);
      setWorkspace(nextWorkspace);
      setError("");
      setDenied(false);
      setNotFound(false);
      setLoadRevision((value) => value + 1);
    } catch (loadError) {
      if (current !== generation.current) return;
      setDetail(undefined);
      setWorkspace(undefined);
      setError(failure(loadError));
      setDenied(loadError instanceof ApiError && loadError.status === 403);
      setNotFound(loadError instanceof ApiError && loadError.status === 404);
    }
  }, [projectId, user]);

  useEffect(() => {
    void load();
    return () => {
      generation.current += 1;
    };
  }, [load]);

  if (!detail || !workspace) {
    return (
      <div className="busbar-page page-surface">
        <DsPageHeader
          title="납품 프로젝트 상세"
          actions={<button type="button" onClick={onBack}>프로젝트 목록</button>}
        />
        <DsStatePanel
          kind={error ? (denied ? "forbidden" : notFound ? "not-found" : "error") : "loading"}
          title={error ? (notFound ? "프로젝트를 찾을 수 없습니다." : "프로젝트를 열 수 없습니다.") : "프로젝트를 불러오는 중"}
          description={error || undefined}
          action={error ? <button type="button" onClick={() => void load()}>다시 불러오기</button> : undefined}
        />
      </div>
    );
  }

  const { project, shipments, panels } = detail;
  const family = workspace.productFamilies.find((item) => item.id === project.productFamilyId);
  const stock = family?.balance ?? 0;
  const remaining = Math.max(0, project.requestedQuantity - project.shippedQuantity);
  const canShip = workspace.permissions?.projects ?? false;
  const canAdminister = workspace.permissions?.administration ?? false;
  const revision = `${loadRevision}:${family?.standardUnitPrice}:${project.requestedQuantity}:${workspace.settings.ecountCustomerCode}:${workspace.settings.ecountWarehouseCode}:${family?.ecountProductCode}`;

  return (
    <div className="busbar-page page-surface">
      <DsPageHeader
        title={project.name}
        description="납품 프로젝트 상세"
        actions={
          <>
            <button type="button" onClick={onBack}>프로젝트 목록</button>
            <button type="button" onClick={() => void load()}>새로고침</button>
          </>
        }
      />
      {!canShip && (
        <DsReadOnlyBanner description="이 프로젝트는 조회만 가능합니다. 출하는 담당 팀 또는 관리자에게 요청하세요." />
      )}
      {feedback && <DsActionFeedback message={feedback} tone="success" focusOnAttention />}

      <DsSurface label="프로젝트 기본 정보">
        <DsToolbar>
          <h3>프로젝트 정보</h3>
          {canShip && remaining > 0 && stock > 0 && (
            <button type="button" className="button primary" onClick={() => setScanOpen(true)}>
              패널 QR로 분할 출하
            </button>
          )}
        </DsToolbar>
        <dl className="busbar-project-fields">
          <div><dt>프로젝트명</dt><dd>{project.name}</dd></div>
          <div><dt>LSE Task No</dt><dd>{project.customerJobNumber || "미입력"}</dd></div>
          <div><dt>도착지</dt><dd>{project.destination}</dd></div>
          <div><dt>납품예정일</dt><dd>{project.dueDate.slice(0, 10)}</dd></div>
          <div><dt>제품군</dt><dd>{family?.name ?? "삭제된 제품군"}</dd></div>
        </dl>
        <dl className="busbar-shipping-summary" aria-label="납품 수량 현황">
          <div><dt>요청 수량</dt><dd>{n(project.requestedQuantity)}개</dd></div>
          <div><dt>누적 출하</dt><dd>{n(project.shippedQuantity)}개</dd></div>
          <div><dt>납품 잔여</dt><dd>{n(remaining)}개</dd></div>
          <div><dt>제품군 공용 재고</dt><dd>{n(stock)}개</dd></div>
        </dl>
        <p className="busbar-note">공용 재고는 이 프로젝트에 예약된 수량이 아닙니다. 생산 예정 수량은 현재고에 포함하지 않습니다.</p>
      </DsSurface>

      <DsSurface label="생산계획과 주문 금액">
        <div className="busbar-project-summary">
          <div>
            <h3>해당 제품군 생산계획</h3>
            <Table
              headings={["생산일", "계획", "완료", "미완료", "현재고"]}
              rows={workspace.plans
                .filter((plan) => plan.productFamilyId === project.productFamilyId)
                .sort((left, right) => left.planDate.localeCompare(right.planDate))
                .map((plan) => [
                  plan.planDate.slice(0, 10),
                  `${n(plan.quantity)}개`,
                  `${n(plan.actualQuantity)}개`,
                  `${n(Math.max(0, plan.quantity - (plan.actualQuantity ?? 0)))}개`,
                  `${n(stock)}개`,
                ])}
            />
          </div>
          <CommercialPreview
            key={`${user}:${project.id}:${revision}`}
            userId={user}
            projectId={project.id}
            revision={revision}
          />
        </div>
      </DsSurface>

      <DsSurface label="이카운트 전송 상태">
        <EcountStatus
          key={`${user}:${project.id}:${project.shippedQuantity}`}
          userId={user}
          projectId={project.id}
          canWrite={canAdminister}
        />
      </DsSurface>

      <DsSurface label="출하 이력">
        <h3>출하 이력</h3>
        {shipments.length === 0 ? (
          <DsStatePanel kind="empty" title="출하 이력이 없습니다." description="패널 QR로 출하하면 이곳에서 개별 패널을 확인할 수 있습니다." />
        ) : (
          <div className="busbar-shipment-history">
            {shipments.map((shipment) => {
              const shipmentPanels = panels.filter((panel) => panel.shipmentId === shipment.id);
              return (
                <article className="busbar-shipment-card" key={shipment.id}>
                  <DsToolbar>
                    <div>
                      <h4>{busbarDateTime(shipment.createdAtUtc)} · {n(shipment.quantity)}개</h4>
                      <p className="busbar-note">처리자 {shipment.shippedByName || "확인 불가"}</p>
                    </div>
                    <DsBadge tone={shipment.reversed ? "danger" : "success"}>{shipment.reversed ? "출하 취소" : "출하 반영"}</DsBadge>
                    {!shipment.reversed && canAdminister && (
                      <button type="button" onClick={() => setReverseShipment(shipment)}>출하 취소</button>
                    )}
                  </DsToolbar>
                  <dl className="busbar-shipment-snapshot">
                    <div><dt>프로젝트명</dt><dd>{shipment.projectNameSnapshot ?? "기록 없음 (기존 출하)"}</dd></div>
                    <div><dt>LSE Task No</dt><dd>{shipment.taskNumberSnapshot == null ? "기록 없음 (기존 출하)" : shipment.taskNumberSnapshot || "미입력"}</dd></div>
                    <div><dt>도착지</dt><dd>{shipment.destinationSnapshot ?? "기록 없음 (기존 출하)"}</dd></div>
                  </dl>
                  {shipmentPanels.length === 0 ? (
                    <p className="busbar-legacy-shipment">패널 연결 기록 없음</p>
                  ) : (
                    <Table
                      headings={["제품번호", "제조일시", "작업자", "사진"]}
                      rows={shipmentPanels.map((panel) => [
                        panel.number || "제품번호 확인 필요",
                        busbarDateTime(panel.manufacturedAtUtc),
                        panel.workerName || "확인 불가",
                        <button type="button" onClick={() => setViewPanel(panel)}>앞·뒤 사진 보기</button>,
                      ])}
                    />
                  )}
                </article>
              );
            })}
          </div>
        )}
      </DsSurface>

      {scanOpen && (
        <ShipmentScanDialog
          user={user}
          projectId={project.id}
          projectName={project.name}
          familyName={family?.name ?? "삭제된 제품군"}
          remaining={remaining}
          stock={stock}
          onClose={() => setScanOpen(false)}
          onShipped={async (quantity) => {
            setScanOpen(false);
            await load();
            setFeedback(`${n(quantity)}개 패널을 출하했습니다.`);
          }}
        />
      )}
      {reverseShipment && (
        <ReverseShipmentDialog
          user={user}
          shipment={reverseShipment}
          onClose={() => setReverseShipment(undefined)}
          onReversed={async () => {
            setReverseShipment(undefined);
            await load();
            setFeedback("출하를 취소하고 패널을 재고로 복원했습니다.");
          }}
        />
      )}
      {viewPanel && (
        <PanelPhotosDialog user={user} panel={viewPanel} onClose={() => setViewPanel(undefined)} />
      )}
    </div>
  );
}

function ShipmentScanDialog({
  user,
  projectId,
  projectName,
  familyName,
  remaining,
  stock,
  onClose,
  onShipped,
}: {
  user: string;
  projectId: string;
  projectName: string;
  familyName: string;
  remaining: number;
  stock: number;
  onClose: () => void;
  onShipped: (quantity: number) => Promise<void>;
}) {
  const [code, setCode] = useState("");
  const [selected, setSelected] = useState<BusbarProduct[]>([]);
  const [error, setError] = useState("");
  const [checking, setChecking] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [cameraStarting, setCameraStarting] = useState(false);
  const [cameraActive, setCameraActive] = useState(false);
  const [selectionFrozen, setSelectionFrozen] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const controlsRef = useRef<IScannerControls | undefined>(undefined);
  const cameraGeneration = useRef(0);
  const selectedIds = useRef(new Set<string>());
  const codesInFlight = useRef(new Set<string>());
  const acceptedCodes = useRef(new Map<string, { id: string; number: string }>());
  const lastCameraDetection = useRef({ code: "", at: 0 });
  const mounted = useRef(true);
  const selectionFrozenRef = useRef(false);
  const submitLocked = useRef(false);
  const payloadRef = useRef<{ quantity: number; productIds: string[] } | undefined>(undefined);
  const requestId = useRef(crypto.randomUUID());
  const maxPanels = Math.max(0, Math.min(remaining, stock));

  const stopCamera = useCallback(() => {
    cameraGeneration.current += 1;
    controlsRef.current?.stop();
    controlsRef.current = undefined;
    setCameraActive(false);
    setCameraStarting(false);
  }, []);

  useEffect(() => {
    mounted.current = true;
    selectionFrozenRef.current = false;
    inputRef.current?.focus();
    return () => {
      mounted.current = false;
      selectionFrozenRef.current = true;
      cameraGeneration.current += 1;
      controlsRef.current?.stop();
      controlsRef.current = undefined;
    };
  }, [stopCamera]);

  const addCode = useCallback(async (rawCode: string, source: "manual" | "camera" = "manual") => {
    const normalized = rawCode.trim();
    if (!normalized || selectionFrozenRef.current || codesInFlight.current.has(normalized)) return;
    const acceptedPanel = acceptedCodes.current.get(normalized);
    if (acceptedPanel) {
      if (source === "manual") setError(`${acceptedPanel.number}은 이미 선택했습니다.`);
      return;
    }
    if (selectedIds.current.size >= maxPanels) {
      setError(`이번 출하는 최대 ${n(maxPanels)}개까지 선택할 수 있습니다.`);
      return;
    }
    codesInFlight.current.add(normalized);
    setChecking(true);
    setError("");
    try {
      const panel = await busbarApi.scanProjectPanel(user, projectId, normalized);
      if (!mounted.current || selectionFrozenRef.current) return;
      if (selectedIds.current.has(panel.id)) {
        acceptedCodes.current.set(normalized, { id: panel.id, number: panel.number || "이 패널" });
        if (source === "manual") setError(`${panel.number || "이 패널"}은 이미 선택했습니다.`);
        return;
      }
      if (selectedIds.current.size >= maxPanels) {
        setError(`이번 출하는 최대 ${n(maxPanels)}개까지 선택할 수 있습니다.`);
        return;
      }
      selectedIds.current.add(panel.id);
      acceptedCodes.current.set(normalized, { id: panel.id, number: panel.number || "이 패널" });
      setSelected((current) => [...current, panel]);
      setCode("");
    } catch (scanError) {
      if (mounted.current && !selectionFrozenRef.current) setError(failure(scanError));
    } finally {
      codesInFlight.current.delete(normalized);
      if (mounted.current) {
        setChecking(codesInFlight.current.size > 0);
        inputRef.current?.focus();
      }
    }
  }, [maxPanels, projectId, user]);

  async function startCamera() {
    if (cameraActive || cameraStarting || selectionFrozen || !videoRef.current) return;
    const current = ++cameraGeneration.current;
    setCameraStarting(true);
    setError("");
    try {
      const { BrowserQRCodeReader } = await import("@zxing/browser");
      if (current !== cameraGeneration.current || !videoRef.current) return;
      const reader = new BrowserQRCodeReader();
      const controls = await reader.decodeFromConstraints(
        { audio: false, video: { facingMode: { ideal: "environment" } } },
        videoRef.current,
        (result) => {
          if (!result || current !== cameraGeneration.current || selectionFrozenRef.current) return;
          const nextCode = result.getText();
          const now = Date.now();
          if (lastCameraDetection.current.code === nextCode && now - lastCameraDetection.current.at < 2000) return;
          lastCameraDetection.current = { code: nextCode, at: now };
          void addCode(nextCode, "camera");
        },
      );
      if (current !== cameraGeneration.current) {
        controls.stop();
        return;
      }
      controlsRef.current = controls;
      setCameraActive(true);
    } catch {
      if (current === cameraGeneration.current) {
        setError("카메라를 시작하지 못했습니다. 권한을 확인하거나 아래 입력란에 제품번호를 입력하세요.");
      }
    } finally {
      if (current === cameraGeneration.current) setCameraStarting(false);
    }
  }

  function removePanel(panel: BusbarProduct) {
    if (submitting || selectionFrozen) return;
    selectedIds.current.delete(panel.id);
    acceptedCodes.current.forEach((value, acceptedCode) => {
      if (value.id === panel.id) acceptedCodes.current.delete(acceptedCode);
    });
    setSelected((current) => current.filter((item) => item.id !== panel.id));
    setError("");
    inputRef.current?.focus();
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitLocked.current || selected.length === 0 || selected.length > maxPanels || codesInFlight.current.size > 0) return;
    submitLocked.current = true;
    setSubmitting(true);
    selectionFrozenRef.current = true;
    setSelectionFrozen(true);
    setError("");
    stopCamera();
    try {
      const payload = payloadRef.current ?? {
        quantity: selected.length,
        productIds: selected.map((panel) => panel.id),
      };
      payloadRef.current = payload;
      await busbarApi.write(user, "/shipments", {
        requestId: requestId.current,
        projectId,
        ...payload,
      });
      await onShipped(payload.quantity);
    } catch (submitError) {
      setError(failure(submitError));
    } finally {
      submitLocked.current = false;
      setSubmitting(false);
    }
  }

  return (
    <BusbarDialog label="패널 QR 분할 출하" busy={submitting} onClose={onClose} heading={headingRef} closeLabel="패널 QR 출하 닫기" className="busbar-shipment-dialog">
      <form className="busbar-scan-dialog" onSubmit={submit}>
        <p className="busbar-note">{projectName} · {familyName}</p>
        <p className="busbar-note">실제 출하할 패널의 QR을 하나씩 스캔하세요. 선택한 패널 수가 이번 출하 수량이 됩니다.</p>
        <dl className="busbar-shipping-summary" aria-label="출하 가능 현황">
          <div><dt>납품 잔여</dt><dd>{n(remaining)}개</dd></div>
          <div><dt>공용 현재고</dt><dd>{n(stock)}개</dd></div>
          <div><dt>선택 수량</dt><dd>{n(selected.length)}개</dd></div>
        </dl>
        <div className="busbar-scanner">
          <video className={cameraActive || cameraStarting ? "is-active" : ""} ref={videoRef} muted playsInline aria-label="QR 카메라 미리보기" />
          <div className="busbar-inline">
            {!cameraActive ? (
              <button type="button" disabled={cameraStarting || submitting || selectionFrozen} onClick={() => void startCamera()}>
                {cameraStarting ? "카메라 시작 중…" : "카메라 시작"}
              </button>
            ) : (
              <button type="button" disabled={submitting} onClick={stopCamera}>카메라 끄기</button>
            )}
          </div>
        </div>
        <div className="busbar-scan-input">
          <label>
            QR 스캐너 또는 제품번호
            <input
              ref={inputRef}
              value={code}
              disabled={checking || submitting || selectionFrozen || selected.length >= maxPanels}
              autoComplete="off"
              placeholder="스캔 후 Enter 또는 제품번호 입력"
              onChange={(event) => setCode(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  void addCode(code);
                }
              }}
            />
          </label>
          <button
            type="button"
            disabled={!code.trim() || checking || submitting || selectionFrozen || selected.length >= maxPanels}
            onClick={() => void addCode(code)}
          >
            {checking ? "확인 중…" : "패널 추가"}
          </button>
        </div>
        {error && <DsActionFeedback message={error} tone="error" focusOnAttention />}
        <div className="busbar-selected-panels" role="region" aria-label="출하 선택 패널">
          <h4>선택 패널 {n(selected.length)}개</h4>
          {selected.length === 0 ? (
            <p className="busbar-note">아직 선택한 패널이 없습니다.</p>
          ) : (
            <ol>
              {selected.map((panel) => (
                <li key={panel.id}>
                  <span><strong>{panel.number || "제품번호 확인 필요"}</strong> · {panel.workerName || "작업자 확인 불가"} · {busbarDateTime(panel.manufacturedAtUtc)}</span>
                  <button type="button" disabled={submitting || selectionFrozen} onClick={() => removePanel(panel)}>선택 해제</button>
                </li>
              ))}
            </ol>
          )}
        </div>
        <div className="busbar-form-actions">
          <button className="button primary" disabled={selected.length === 0 || submitting || checking}>
            {submitting ? "출하 처리 중…" : selectionFrozen ? `같은 ${n(selected.length)}개 다시 출하` : `선택한 ${n(selected.length)}개 출하`}
          </button>
          <button type="button" disabled={submitting} onClick={onClose}>취소</button>
        </div>
      </form>
    </BusbarDialog>
  );
}

function ReverseShipmentDialog({
  user,
  shipment,
  onClose,
  onReversed,
}: {
  user: string;
  shipment: BusbarProjectShipment;
  onClose: () => void;
  onReversed: () => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [reasonFrozen, setReasonFrozen] = useState(false);
  const locked = useRef(false);
  const requestId = useRef(crypto.randomUUID());
  const reasonPayload = useRef<string | undefined>(undefined);
  const headingRef = useRef<HTMLHeadingElement>(null);
  return (
    <BusbarDialog label="출하 취소" busy={busy} onClose={onClose} heading={headingRef} closeLabel="출하 취소 팝업 닫기">
      <form className="busbar-form" onSubmit={async (event) => {
        event.preventDefault();
        if (locked.current || !reason.trim()) return;
        locked.current = true;
        setBusy(true);
        setReasonFrozen(true);
        setError("");
        try {
          reasonPayload.current ??= reason;
          await busbarApi.write(user, `/ledger/${shipment.id}/reverse`, { requestId: requestId.current, reason: reasonPayload.current });
          await onReversed();
        } catch (reverseError) {
          setError(failure(reverseError));
        } finally {
          locked.current = false;
          setBusy(false);
        }
      }}>
        <p className="busbar-note">출하 기록은 보존하고 연결된 패널을 재고로 복원합니다.</p>
        <label>취소 사유<input required maxLength={200} value={reason} disabled={busy || reasonFrozen} onChange={(event) => setReason(event.target.value)} /></label>
        {error && <DsActionFeedback message={error} tone="error" />}
        <div className="busbar-form-actions">
          <button className="button primary" disabled={busy || !reason.trim()}>{busy ? "취소 처리 중…" : "출하 취소"}</button>
          <button type="button" disabled={busy} onClick={onClose}>닫기</button>
        </div>
      </form>
    </BusbarDialog>
  );
}

function PanelPhotosDialog({
  user,
  panel,
  onClose,
}: {
  user: string;
  panel: BusbarProduct;
  onClose: () => void;
}) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  return (
    <BusbarDialog label={`${panel.number || "패널"} 출하 사진`} busy={false} onClose={onClose} heading={headingRef} closeLabel="출하 패널 사진 닫기" className="busbar-shipment-dialog">
      <div className="busbar-scan-dialog">
        <p className="busbar-note">제조 {busbarDateTime(panel.manufacturedAtUtc)} · 작업자 {panel.workerName || "확인 불가"}</p>
        <p className="busbar-note">생산 완료 사진은 조회만 가능합니다. 작업자 정정은 생산 메뉴의 사진 보기에서 처리하세요.</p>
        <div className="busbar-photogrid">
          <div className="busbar-photobox">
            <h4>앞면 사진</h4>
            <PhotoPreview user={user} id={panel.id} side="front" revision={panel.revision} exists={panel.hasFront} />
          </div>
          <div className="busbar-photobox">
            <h4>뒷면 사진</h4>
            <PhotoPreview user={user} id={panel.id} side="back" revision={panel.revision} exists={panel.hasBack} />
          </div>
        </div>
      </div>
    </BusbarDialog>
  );
}
