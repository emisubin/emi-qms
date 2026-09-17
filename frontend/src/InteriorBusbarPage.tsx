import { BusbarMasterAccess } from "./BusbarMasterAccess";
import { projectEditorSpec, type Values, type Field, type EditorSpec } from "./interiorBusbarProjectEditor";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ReactNode,
  type FormEvent,
  type RefObject,
} from "react";
import { createPortal, flushSync } from "react-dom";
import { ApiError } from "./api";
import { OsanPageHeading } from "./OsanListFrame";
import {
  DsActionFeedback,
  DsBadge,
  DsDialog,
  DsPageHeader,
  DsReadOnlyBanner,
  DsStatePanel,
  DsSurface,
  DsToolbar,
} from "./design-system";
import {
  busbarApi,
  busbarPhotoAccept,
  busbarDateTime,
  busbarNumber as n,
  type BusbarImport,
  type BusbarMaster,
  type BusbarProduct,
  type BusbarWorkspace,
  type BusbarProductFilters,
  type BusbarCommercialPreview,
  type BusbarEcountStatus,
} from "./interiorBusbar";
import "./interior-busbar.css";
import { busbarOverview } from "./interiorBusbarOverview";
import { busbarSections, type BusbarSection } from "./interiorBusbarNavigation";

type QrLabel = { productId: string; number: string; revision: number; url: string };
const today = () =>
  new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Seoul" }).format(
    new Date(),
  );
const datePlus = (date: string, days: number) => {
  const value = new Date(`${date}T12:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
};
const monthPlus = (month: string, count: number) => {
  const value = new Date(`${month}-01T12:00:00Z`);
  value.setUTCMonth(value.getUTCMonth() + count);
  return value.toISOString().slice(0, 7);
};
const failure = (e: unknown) =>
  e instanceof Error ? e.message : "처리하지 못했습니다. 다시 시도해 주세요.";
const statusLabel = (status?: string) =>
  ({
    Draft: "사진 등록 중",
    Complete: "생산 완료",
    Cancelled: "취소",
    Pending: "게시 대기",
    Published: "게시 완료",
    Failed: "게시 실패",
  })[status ?? ""] ?? "미게시";
const productionStatusLabel = (product: BusbarProduct) => product.status === "Draft"
  ? product.hasFront || product.hasBack ? "1차 사진 등록 완료" : "사진 등록 전"
  : product.status === "Complete" ? "생산 완료" : "취소";
const canPrintQr = (product: BusbarProduct) => product.status === "Complete" && product.publicationState === "Published" &&
  product.revision === product.publishedRevision && Boolean(product.number);

export function InteriorBusbarPage({
  developmentUserKey: user,
  section: tab,
  onNavigate: setTab,
  onOpenProject,
}: {
  developmentUserKey: string;
  section: BusbarSection;
  onNavigate: (section: BusbarSection) => void;
  onOpenProject: (projectId: string) => void;
}) {
  const [data, setData] = useState<BusbarWorkspace | null>(null);
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  const [page, setPage] = useState(1);
  const ledgerHeadingRef = useRef<HTMLHeadingElement>(null);
  const deletedFilterRef = useRef<HTMLInputElement>(null);
  const [showDeleted, setShowDeleted] = useState(false);
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  const [feedbackError, setFeedbackError] = useState(false);
  const [editor, setEditor] = useState<EditorSpec | null>(null);
  const [editorKey, setEditorKey] = useState(0);
  const [filters, setFilters] = useState<BusbarProductFilters>({});
  const [month, setMonth] = useState(() => today().slice(0, 7));
  const [selectedPlanDate, setSelectedPlanDate] = useState(today);
  const [planDialogOpen, setPlanDialogOpen] = useState(false);
  const [planFamily, setPlanFamily] = useState("");
  const [activeProduct, setActiveProduct] = useState("");
  const [productDetail, setProductDetail] = useState<BusbarProduct | null>(null);
  const [projectStatus, setProjectStatus] = useState("");
  const [bomFamily, setBomFamily] = useState("");
  const [selectedQrIds, setSelectedQrIds] = useState<string[]>([]);
  const [qrDialogOpen, setQrDialogOpen] = useState(false);
  const [printArmed, setPrintArmed] = useState(false);
  useEffect(() => {
    const finishPrint = () => setPrintArmed(false);
    window.addEventListener("afterprint", finishPrint);
    return () => window.removeEventListener("afterprint", finishPrint);
  }, []);
  const [previousSection, setPreviousSection] = useState(tab);
  if (previousSection !== tab) {
    setPreviousSection(tab);
    setEditor(null);
    setPlanDialogOpen(false);
    setActiveProduct("");
    setProductDetail(null);
    setQrDialogOpen(false);
    setFeedback("");
    setQuery("");
    setShowDeleted(false);
    setPage(1);
  }
  const [qrLabels, setQrLabels] = useState<QrLabel[]>([]);
  const qrHeadingRef = useRef<HTMLHeadingElement>(null);
  const printRootRef = useRef<HTMLDivElement>(null);
  useEffect(() => { setSelectedQrIds([]); }, [filters, page, query, user]);
  useEffect(() => () => { qrLabels.forEach((label) => URL.revokeObjectURL(label.url)); }, [qrLabels]);
  const planHeadingRef = useRef<HTMLHeadingElement>(null);
  const calendarHeadingRef = useRef<HTMLHeadingElement>(null);
  const completionRef = useRef<HTMLDivElement>(null);
  const productionHeadingRef = useRef<HTMLHeadingElement>(null);
  const photoHeadingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    let current = true;
    if (!activeProduct) { setProductDetail(null); return; }
    void busbarApi.product(user, activeProduct).then((product) => {
      if (current) setProductDetail(product);
    }).catch((e) => { if (current) { setProductDetail(null); setError(failure(e)); } });
    return () => { current = false; };
  }, [user, activeProduct, data]);
  const selectedProduct = productDetail?.id === activeProduct
    ? productDetail
    : data?.products.find((product) => product.id === activeProduct);
  const completedProductId = selectedProduct?.status === "Complete" ? selectedProduct.id : undefined;
  useEffect(() => {
    if (!completedProductId) return;
    completionRef.current?.focus({ preventScroll: true });
    completionRef.current?.scrollIntoView({
      block: "center",
      behavior: "smooth",
    });
  }, [completedProductId]);
  const generation = useRef(0);
  const locked = useRef(false);
  const load = useCallback(async () => {
    const current = ++generation.current;
    try {
      const next = await (tab === "masters" ? busbarApi.masters(user) : busbarApi.workspace(user, page, filters));
      if (current !== generation.current) return;
      setData(next);
      setError("");
      setDenied(false);
    } catch (e) {
      if (current !== generation.current) return;
      setError(failure(e));
      setDenied(e instanceof ApiError && e.status === 403);
      if (e instanceof ApiError && [401, 403].includes(e.status)) setData(null);
    }
  }, [user, page, filters, tab]);
  const latestLoad = useRef(load);
  useEffect(() => {
    latestLoad.current = load;
    void load();
    const invalidate = () => {
      generation.current++;
    };
    return invalidate;
  }, [load]);
  async function run(
    action: () => Promise<unknown>,
    message = "저장했습니다.",
  ) {
    if (locked.current) return false;
    locked.current = true;
    setBusy(true);
    setFeedback("");
    setFeedbackError(false);
    try {
      await action();
      // Uploads may finish after the user changes the visible plan or page.
      await latestLoad.current();
      setFeedback(message);
      return true;
    } catch (e) {
      setFeedback(failure(e));
      setFeedbackError(true);
      return false;
    } finally {
      locked.current = false;
      setBusy(false);
    }
  }
  function open(spec: EditorSpec) {
    setEditor(spec);
    setEditorKey((x) => x + 1);
    setFeedback("");
  }
  const familyName = (id: string) =>
    data?.productFamilies.find((x) => x.id === id)?.name ?? "삭제된 제품군";
  const materialName = (id: string) =>
    data?.materials.find((x) => x.id === id)?.name ?? "삭제된 자재";
  const options = (rows: BusbarMaster[], include?: string) =>
    rows
      .filter((x) => !x.isDeleted && (x.isActive || x.id === include))
      .map((x) => ({
        value: x.id,
        label: `${x.name}${x.code ? ` (${x.code})` : ""}${x.isActive ? "" : " · 비활성"}`,
      }));
  const familyField = (value?: string): Field => ({
    key: "productFamilyId",
    label: "제품군",
    type: "select",
    options: options(data?.productFamilies ?? [], value),
    value,
  });
  const reasonField: Field = { key: "reason", label: "정정 사유" };
  const quantityField = (label = "수량", value?: number): Field => ({
    key: "quantity",
    label,
    type: "number",
    min: 0.000001,
    step: "any",
    value: value === undefined ? undefined : String(value),
  });
  function editMaster(path: string, title: string, row?: BusbarMaster) {
    open({
      title,
      path,
      fields: [
        { key: "code", label: "코드", value: row?.code },
        { key: "name", label: "명칭", value: row?.name },
        ...(path === "/product-families" ? [
          { key: "ecountProductCode", label: "이카운트 품목 코드", value: row?.ecountProductCode ?? "", optional: true },
          { key: "standardUnitPrice", label: "기준 단가 (원·부가세 별도)", type: "number" as const, min: 0, step: "0.0001", value: row?.standardUnitPrice == null ? "" : String(row.standardUnitPrice), optional: true },
        ] : []),
        ...(path === "/materials"
          ? [
              { key: "unit", label: "단위", value: row?.unit },
              {
                key: "supplyType",
                label: "공급 구분",
                type: "select" as const,
                value: row?.supplyType,
                options: [
                  { value: "사급", label: "사급" },
                  { value: "도급", label: "도급" },
                ],
              },
            ]
          : []),
        {
          key: "isActive",
          label: "사용 상태",
          type: "select",
          value: String(row?.isActive ?? true),
          options: [
            { value: "true", label: "사용" },
            { value: "false", label: "사용 중지" },
          ],
        },
      ],
      makeBody: (v) => ({
        ...v,
        id: row?.id ?? null,
        isActive: v.isActive === "true",
        ...(path === "/product-families" ? { standardUnitPrice: v.standardUnitPrice === "" ? null : Number(v.standardUnitPrice) } : {}),
      }),
    });
  }
  function projectEditor(id?: string) {
    if (data) open(projectEditorSpec(data, id));
  }
  function openPlanProducts(productFamilyId: string, planDate: string) {
    setPlanDialogOpen(false);
    setFilters({ productFamilyId, planDateFrom: planDate, planDateTo: planDate });
    setPage(1);
    setActiveProduct("");
    setQuery("");
    setTab("production");
    setEditor(null);
  }
  function planEditor(productFamilyId: string, planDate: string, id?: string) {
    const row = data?.plans.find((x) => x.id === id);
    const planId = row?.id ?? crypto.randomUUID();
    open({
      title: row ? "생산계획 수정" : "생산계획 등록",
      path: "/plans",
      note: "목표 수량만큼 사진 등록 대기 항목을 만듭니다. 수량을 줄이면 작업자와 사진을 등록하지 않은 대기 항목만 취소합니다.",
      fields: [
        { ...familyField(row?.productFamilyId ?? productFamilyId), disabled: true },
        {
          key: "planDate",
          label: "생산일",
          type: "date",
          value: row?.planDate?.slice(0, 10) ?? planDate,
          disabled: true,
        },
        { ...quantityField("목표 수량", row?.quantity), min: 0, step: "1" },
      ],
      makeBody: (v) => ({ ...v, id: planId, quantity: Number(v.quantity) }),
    });
  }
  function purchaseEditor(id?: string) {
    const row = data?.purchases.find((x) => x.id === id);
    open({
      title: row ? "발주 정정" : "발주 등록",
      path: "/purchases",
      note: "발주만 등록하면 재고는 변하지 않습니다. 실제 도착한 수량을 분할 입고하세요.",
      fields: [
        { key: "orderNumber", label: "발주번호", value: row?.orderNumber },
        {
          key: "materialId",
          label: "자재",
          type: "select",
          options: options(data?.materials ?? [], row?.materialId),
          value: row?.materialId,
        },
        quantityField("발주 수량", row?.quantity),
        {
          key: "orderDate",
          label: "발주일",
          type: "date",
          value: row?.orderDate?.slice(0, 10) ?? today(),
        },
        ...(row ? [reasonField] : []),
      ],
      makeBody: (v) => ({
        ...v,
        id: row?.id ?? null,
        quantity: Number(v.quantity),
      }),
    });
  }
  function adjustment(kind: string, isOpening = false) {
    const rows = kind === "Material" ? data?.materials : data?.productFamilies;
    const requestId = crypto.randomUUID();
    open({
      title: `${kind === "Material" ? "자재" : "완제품"} ${isOpening ? "기초재고 등록" : "재고 보정"}`,
      path: "/adjustments",
      fields: [
        {
          key: "itemId",
          label: kind === "Material" ? "자재" : "제품군",
          type: "select",
          options: options(rows ?? []),
        },
        {
          ...quantityField(isOpening ? "기초 수량" : "증감 수량"),
          min: isOpening ? 0 : undefined,
          step: kind === "Material" ? "any" : "1",
        },
        reasonField,
      ],
      note: isOpening
        ? "현재고에 입력한 기초 수량을 더합니다. 기존 완제품에 가상의 개별 제품번호를 만들지 않습니다."
        : "증가는 양수, 감소는 음수를 입력합니다. 완제품 현재고는 음수가 될 수 없습니다.",
      makeBody: (v) => ({
        ...v,
        stockKind: kind,
        isOpening,
        requestId,
        quantity: Number(v.quantity),
      }),
    });
  }
  if (!data)
    return (
      <div className="busbar-page">
        <DsPageHeader title="인테리어 부스바" />
        <DsStatePanel
          kind={error ? (denied ? "forbidden" : "error") : "loading"}
          title={error ? "목록을 열 수 없습니다." : "업무 현황을 불러오는 중"}
          description={error || undefined}
          action={
            error ? (
              <button onClick={() => void load()}>다시 불러오기</button>
            ) : undefined
          }
        />
      </div>
    );
  const productLabel = (product: BusbarProduct) =>
    product.number ??
    (product.planId
      ? `${data.plans.find((plan) => plan.id === product.planId)?.planDate.slice(0, 10) ?? "계획"} · 대기 ${product.planSequence ?? ""}번`
      : "기존 사진 등록 대기");
  const matches = (...values: unknown[]) =>
    values.join(" ").toLocaleLowerCase().includes(query.toLocaleLowerCase());
  const overviewDate = data.overview?.asOfDate ?? today();
  const home = busbarOverview(data, overviewDate);
  const visibleProjects = data.projects.filter((p) => Boolean(p.isDeleted) === showDeleted && matches(p.name, p.customerJobNumber, p.destination, familyName(p.productFamilyId)) &&
    (!projectStatus || (projectStatus === "Complete" ? p.requestedQuantity === p.shippedQuantity : p.requestedQuantity > p.shippedQuantity)));
  const monthFirst = `${month}-01`;
  const monthLast = datePlus(`${monthPlus(month, 1)}-01`, -1);
  const calendarStart = datePlus(monthFirst, -new Date(`${monthFirst}T12:00:00Z`).getUTCDay());
  const calendarDays = Math.ceil((new Date(`${monthFirst}T12:00:00Z`).getUTCDay() + Number(monthLast.slice(8))) / 7) * 7;
  const visiblePlanFamilies = data.productFamilies.filter((f) => !f.isDeleted && (!planFamily || f.id === planFamily));
  const deletionFilter = <label className="busbar-check-label busbar-deleted-filter"><input ref={deletedFilterRef} type="checkbox" checked={showDeleted} onChange={event => setShowDeleted(event.target.checked)} />삭제된 항목 보기</label>;
  const recordAction = (entity: string, row: { id: string; isDeleted?: boolean }, label: string) => canWrite && <BusbarRecordAction key={`${entity}:${row.id}:${row.isDeleted}`} user={user} path={`/${entity}/${row.id}/${row.isDeleted ? "restore" : "delete"}`} label={label} action={row.isDeleted ? "복원" : "삭제"} disabled={busy} note={entity === "plans" && !row.isDeleted ? "이 계획의 작업자·사진이 없는 미착수 패널도 모두 취소됩니다. 계획을 복원해도 취소된 패널은 자동 복구되지 않습니다. 복원 후 계획을 다시 저장해야 대기 패널이 준비됩니다." : undefined} onChanged={async () => { await load(); setFeedback(`${label} ${row.isDeleted ? "복원" : "삭제"}했습니다.`); setFeedbackError(false); requestAnimationFrame(() => (planDialogOpen ? planHeadingRef.current : deletedFilterRef.current)?.focus()); }} />;
  function changeMonth(next: string) {
    setMonth(next); setSelectedPlanDate(`${next}-01`); setEditor(null);
  }
  function changeFilter(key: keyof BusbarProductFilters, value: string) {
    setFilters((current) => ({ ...current, [key]: value }));
    setPage(1); setActiveProduct("");
  }
  const selectedBomFamily = data.productFamilies.find(f => f.id === bomFamily && !f.isDeleted);
  const stock = (item?: BusbarMaster) => item?.balance ?? 0;
  const access = data.permissions;
  const canAdminister = access?.administration ?? false;
  const canWrite = access ? ({ overview: false, projects: access.projects, plans: access.planning,
    production: access.production, purchases: access.purchases, masters: access.mastersWrite === true }[tab]) : false;
  const visibleProducts = data.products.filter((product) => matches(productLabel(product), familyName(product.productFamilyId), product.workerName));
  const eligibleProducts = visibleProducts.filter(canPrintQr);
  const selectedIds = selectedQrIds.filter((id) => eligibleProducts.some((product) => product.id === id));
  async function validateLabels(labels: Array<{ productId: string; revision: number; number: string }>) {
    const fresh = await Promise.all(labels.map((label) => busbarApi.product(user, label.productId)));
    fresh.forEach((product, index) => {
      if (!canPrintQr(product) || product.revision !== labels[index].revision || product.number !== labels[index].number)
        throw new Error("선택 제품의 정보 또는 게시 상태가 변경됐습니다. 목록을 새로고침한 뒤 다시 선택하세요.");
    });
  }
  function prepareQrLabels() {
    const products = eligibleProducts.filter((product) => selectedIds.includes(product.id));
    if (!products.length || busy) return;
    setQrLabels([]); setQrDialogOpen(true);
    void run(async () => {
      const expected = products.map((product) => ({ productId: product.id, revision: product.revision, number: product.number! }));
      await validateLabels(expected);
      const blobs = await Promise.all(products.map((product) => busbarApi.qr(user, product.id)));
      const labels = expected.map((product, index) => ({ ...product, url: URL.createObjectURL(blobs[index]) }));
      try {
        await Promise.all(labels.map((label) => new Promise<void>((resolve, reject) => {
          const img = new Image(); img.onload = () => resolve(); img.onerror = () => reject(new Error("QR 이미지를 불러오지 못했습니다. 다시 준비해 주세요.")); img.src = label.url;
        })));
        await validateLabels(expected);
        setQrLabels(labels);
      } catch (error) { labels.forEach((label) => URL.revokeObjectURL(label.url)); throw error; }
    }, "선택한 제품의 QR을 모두 준비했습니다. 번호를 확인한 뒤 인쇄하세요.");
  }
  const writeButton = (label: string, action: () => void, allowed = canWrite) =>
    allowed ? (
      <button
        type="button"
        className="button secondary"
        disabled={busy}
        onClick={action}
      >
        {label}
      </button>
    ) : null;
  return (
    <div className="busbar-page page-surface" aria-busy={busy}>
      <header className="busbar-page-heading osan-dashboard osan-list-frame">
      <OsanPageHeading
        title={busbarSections.find((item) => item.key === tab)!.label}
        description="인테리어 부스바 · 제품군별 생산과 재고, 납품을 관리합니다."
        actions={
          <button type="button" disabled={busy} onClick={() => void load()}>
            새로고침
          </button>
        }
      />
      </header>
      {!canWrite && tab !== "overview" && (
        <DsReadOnlyBanner description="이 화면은 조회만 가능합니다. 입력은 담당 팀 또는 관리자에게 요청하세요." />
      )}
      {error && <DsActionFeedback message={error} tone="error" />}
      {tab === "production" && data.pagination && (
        <DsToolbar label="기록 페이지">
          <button
            disabled={busy || page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            이전 기록
          </button>
          <span>
            {page}페이지 ·{" "}
            {data.pagination.productCount}
            건
          </span>
          <button
            disabled={
              busy ||
              page * data.pagination.pageSize >=
                data.pagination.productCount
            }
            onClick={() => setPage((p) => p + 1)}
          >
            다음 기록
          </button>
        </DsToolbar>
      )}
      {feedback && !planDialogOpen && !activeProduct && !qrDialogOpen && (
        <DsActionFeedback
          message={feedback}
          tone={feedbackError ? "error" : "success"}
          focusOnAttention
        />
      )}
      {editor && canWrite && !planDialogOpen && !activeProduct && !qrDialogOpen && (
        <Editor
          key={editorKey}
          spec={editor}
          busy={busy}
          onClose={() => setEditor(null)}
          onSave={async (values) => {
            const ok = await run(async () => {
              const result = await busbarApi.write(
                user,
                editor.path,
                editor.makeBody(values),
                editor.method,
              );
              editor.after?.(result);
            });
            if (ok) setEditor(null);
          }}
        />
      )}
      {tab === "overview" && (
        <>
          <p className="busbar-note">{overviewDate} 기준 · 한국 시간 · 새로고침 시 갱신</p>
          <DsSurface label="진행중인 프로젝트">
            <h3>진행중인 프로젝트</h3>
            <p className="busbar-note">미출하 잔여가 있는 프로젝트 중 {home.through}까지 납기인 건입니다. 지연은 빨강, 오늘부터 3일 이내는 노랑으로 표시합니다.</p>
            <Table headings={["납기 상태", "프로젝트명", "도착지", "제품군", "납품예정일", "요청 수량", "누적 출하", "납품 잔여"]}
              rowClasses={home.projects.map(p => p.dueDate.slice(0,10) < overviewDate ? "busbar-overdue" : p.dueDate.slice(0,10) <= datePlus(overviewDate,3) ? "busbar-due-soon" : "")}
              rows={home.projects.map(p => [p.dueDate.slice(0,10) < overviewDate ? "납기 지연" : p.dueDate.slice(0,10) === overviewDate ? "오늘 납품" : "납품 예정", p.name, p.destination, familyName(p.productFamilyId), p.dueDate.slice(0,10), n(p.requestedQuantity), n(p.shippedQuantity), n(p.requestedQuantity-p.shippedQuantity)])} />
            {home.projects.length === 0 && <p className="busbar-note">지연되거나 7일 이내 납품할 프로젝트가 없습니다.</p>}
          </DsSurface>
          <DsSurface label="제품군별 현황">
            <h3>제품군별 오늘 생산·납품 준비</h3>
            <Table headings={["제품군", "오늘 계획", "오늘 생산 완료", "이전 계획 미완료", "완제품 현재고", "지연·7일 내 납품 잔여", "추가 생산 필요"]}
              rows={home.families.map(f => [f.name,n(f.planned),n(f.produced),n(f.overdue),n(f.stock),n(f.deliveries),<span className={f.needed > 0 ? "busbar-negative" : ""}>{n(f.needed)}</span>])} />
            <p className="busbar-note">오늘 생산 완료는 실제 제조일 기준입니다. 이전 계획 미완료는 오늘 이전 계획의 남은 생산량입니다. 추가 생산 필요 = 지연·7일 내 납품 잔여 − 현재고(최소 0). 계획 수량은 재고에 포함하지 않으며, 재고는 프로젝트별 예약 없이 공용으로 사용합니다.</p>
            {home.families.length === 0 && <p className="busbar-note">오늘 작업·미완료 계획·재고·가까운 납품이 있는 제품군이 없습니다.</p>}
          </DsSurface>
          <DsSurface label="부족 자재">
            <h3>자재 부족·확인 필요</h3>
            <p className="busbar-note">오늘 및 이전 미완료 계획에 최신 표준 소요량을 적용합니다. 추가 확보 필요 = 남은 생산 소요량 − 현재고(최소 0). 미입고 발주는 재고에 포함하지 않습니다.</p>
            {home.missingBoms.length > 0 && <p className="busbar-negative" role="status">소요량 미설정: {home.missingBoms.join(", ")} · 해당 제품군의 필요 자재는 계산하지 못했습니다. 아래 결과는 설정된 제품군 기준입니다.</p>}
            <Table headings={["품목", "단위", "현재고", "남은 생산 소요량", "추가 확보 필요"]}
              rows={home.materials.map(m => [m.name,m.unit,<span className={m.stock < 0 ? "busbar-negative" : ""}>{n(m.stock)}</span>,n(m.required),<span className="busbar-negative">{n(m.shortage)}</span>])} />
            {home.materials.length === 0 && <p className="busbar-note">{home.missingBoms.length ? "계산 가능한 제품군 기준으로 부족 자재가 없습니다. 소요량 미설정 건을 먼저 확인하세요." : "현재고와 남은 생산 소요량 기준으로 부족 자재가 없습니다."}</p>}
          </DsSurface>
        </>
      )}
      {tab === "projects" && (
        <>
          <DsSurface>
            <DsToolbar label="납품 프로젝트 도구" className="busbar-filterbar">
              <Search value={query} onChange={setQuery} />
              {deletionFilter}
              <label>프로젝트 상태<select aria-label="프로젝트 상태" value={projectStatus} onChange={(event) => setProjectStatus(event.target.value)}><option value="">전체</option><option value="InProgress">진행 중</option><option value="Complete">완료</option></select></label>
              <div className="busbar-filter-actions">{writeButton("프로젝트 등록", () => projectEditor())}
                {canWrite && <ImportBox data={data} user={user} kind="projects" busy={busy} run={run} />}
              </div>
            </DsToolbar>
            <Table
              headings={[
                "프로젝트명",
                "LSE Task No",
                "제품군",
                "납품예정일",
                "도착지",
                "요청",
                "누적 출하",
                "잔여",
                "상태",
                "관리",
              ]}
              rowActions={visibleProjects.map((p) => ({ toggle: () => onOpenProject(p.id) }))}
              rows={visibleProjects.map((p) => [
                  p.name,
                  p.customerJobNumber,
                  familyName(p.productFamilyId),
                  p.dueDate.slice(0, 10),
                  p.destination,
                  n(p.requestedQuantity),
                  n(p.shippedQuantity),
                  n(p.requestedQuantity - p.shippedQuantity),
                  <DsBadge
                    tone={
                      p.requestedQuantity === p.shippedQuantity
                        ? "success"
                        : "neutral"
                    }
                  >
                    {p.isDeleted ? "삭제됨" : p.requestedQuantity === p.shippedQuantity
                      ? "완료"
                      : "진행 중"}
                  </DsBadge>,
                  <span onClick={event => event.stopPropagation()}>{recordAction("projects", p, p.name)}</span>,
                ])}
            />
          </DsSurface>

        </>
      )}
      {tab === "plans" && (
        <>
          <DsSurface label="제품군별 월간 생산계획">
            <DsToolbar className="busbar-filterbar busbar-plan-filter" label="생산계획 필터">
              {deletionFilter}
              <label>
                계획 제품군
                <select aria-label="계획 제품군" value={planFamily} onChange={(e) => setPlanFamily(e.target.value)}>
                  <option value="">전체 제품군</option>
                  {data.productFamilies.filter(f => !f.isDeleted).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
              </label>
              <label>계획 월<input type="month" value={month} onChange={(e) => { if (e.target.value) changeMonth(e.target.value); }} /></label>
              <div className="busbar-month-totals" aria-label="선택 월 제품군별 생산 현황">
                <span className="busbar-note">선택 월 계획 기준 · 계획 / 생산 완료</span>
                {visiblePlanFamilies.map((family) => {
                  const plans = data.plans.filter((plan) => !plan.isDeleted && plan.productFamilyId === family.id && plan.planDate.slice(0, 7) === month);
                  return <div key={family.id}><strong>{family.name}</strong><span>계획 <b>{n(plans.reduce((sum, plan) => sum + plan.quantity, 0))}</b>대</span><span>생산 완료 <b>{n(plans.reduce((sum, plan) => sum + (plan.actualQuantity ?? 0), 0))}</b>대</span></div>;
                })}
              </div>
              <div className="busbar-filter-actions">
                <button onClick={() => changeMonth(monthPlus(month, -1))}>이전 달</button>
                <button onClick={() => { changeMonth(today().slice(0, 7)); setSelectedPlanDate(today()); }}>이번 달</button>
                <button onClick={() => changeMonth(monthPlus(month, 1))}>다음 달</button>
              </div>
            </DsToolbar>
            <h3 ref={calendarHeadingRef} tabIndex={-1}>{Number(month.slice(0, 4))}년 {Number(month.slice(5))}월 생산계획</h3>
            <p className="busbar-note">날짜를 선택하면 팝업에서 제품군별 계획을 입력·수정할 수 있습니다. 날짜 칸에는 제품군별 계획과 완료 수량을 표시합니다.</p>
            {showDeleted && <Table headings={["제품군", "생산일", "계획 수량", "관리"]} rows={data.plans.filter(p => p.isDeleted && p.planDate.startsWith(month) && (!planFamily || p.productFamilyId === planFamily)).map(p => [familyName(p.productFamilyId), p.planDate.slice(0,10), n(p.quantity), recordAction("plans", p, `${familyName(p.productFamilyId)} ${p.planDate.slice(0,10)} 계획`)])} />}
            <div className="busbar-month-calendar" role="region" aria-label="월간 생산계획 달력" tabIndex={0}>
              <div className="busbar-calendar-weekdays" aria-hidden="true">{["일", "월", "화", "수", "목", "금", "토"].map((day) => <span key={day} className={day === "일" ? "busbar-sunday" : day === "토" ? "busbar-saturday" : ""}>{day}</span>)}</div>
              <div className="busbar-calendar-grid">
                {Array.from({ length: calendarDays }, (_, index) => {
                  const date = datePlus(calendarStart, index);
                  if (!date.startsWith(month)) return <div key={date} className="busbar-calendar-outside" aria-hidden="true">{Number(date.slice(8))}</div>;
                  const plans = data.plans.filter((p) => !p.isDeleted && p.planDate.slice(0, 10) === date && (!planFamily || p.productFamilyId === planFamily));
                  return <button key={date} type="button" className="busbar-calendar-day" aria-label={`${date} 생산계획 선택`}
                    aria-pressed={selectedPlanDate === date} aria-current={date === today() ? "date" : undefined}
                    onClick={() => { setSelectedPlanDate(date); setEditor(null); setFeedback(""); setPlanDialogOpen(true); }}>
                    <span className={`busbar-calendar-date ${index % 7 === 0 ? "busbar-sunday" : index % 7 === 6 ? "busbar-saturday" : ""}`}>{Number(date.slice(8))}{date === today() && <small>오늘</small>}</span>
                    {plans.length ? plans.map((plan) => <span className="busbar-calendar-plan" key={plan.id}>
                      <strong>{familyName(plan.productFamilyId)}</strong>
                      <span>계획 {n(plan.quantity)}개 · 완료 {n(plan.actualQuantity)}개</span>
                    </span>) : <span className="busbar-calendar-empty">계획 없음</span>}
                  </button>;
                })}
              </div>
            </div>
          </DsSurface>
          {planDialogOpen && <BusbarDialog label={`${selectedPlanDate} 제품군별 생산계획`} busy={busy} heading={planHeadingRef}
            onClose={() => { if (!busy) { setPlanDialogOpen(false); setEditor(null); setFeedback(""); } }}>
            {feedback && <DsActionFeedback message={feedback} tone={feedbackError ? "error" : "success"} focusOnAttention />}
            {error && <DsActionFeedback message={error} tone="error" />}
            {editor?.path === "/plans" && canWrite && <Editor key={editorKey} spec={editor} busy={busy}
              onClose={() => { if (!busy) setEditor(null); }}
              onSave={async (values) => {
                const ok = await run(() => busbarApi.write(user, editor.path, editor.makeBody(values), editor.method));
                if (ok) { setEditor(null); requestAnimationFrame(() => planHeadingRef.current?.focus()); }
              }} />}
            <p className="busbar-note">제품군의 계획을 저장한 뒤 다른 제품군도 이어서 입력할 수 있습니다. 제품 보기에서 작업자와 사진을 등록하세요.</p>
            <Table headings={["제품군", "계획 수량", "완료 수량", "미완료", "작업"]}
              rows={visiblePlanFamilies.map((family) => {
                const plan = data.plans.find((p) => !p.isDeleted && p.productFamilyId === family.id && p.planDate.slice(0, 10) === selectedPlanDate);
                return [family.name, plan ? `${n(plan.quantity)}개` : "미등록", plan ? `${n(plan.actualQuantity)}개` : "—",
                  plan ? `${n(Math.max(0, plan.quantity - (plan.actualQuantity ?? 0)))}개` : "—",
                  <>
                    {canWrite && (family.isActive || plan) && <button disabled={busy} aria-label={`${family.name} ${selectedPlanDate} 계획 ${plan ? "수정" : "등록"}`} onClick={() => planEditor(family.id, selectedPlanDate, plan?.id)}>{plan ? "계획 수정" : "계획 등록"}</button>}
                    {plan && recordAction("plans", plan, `${family.name} ${selectedPlanDate} 계획`)}
                    {plan && <button disabled={busy} aria-label={`${family.name} ${selectedPlanDate} 제품 보기`} onClick={() => openPlanProducts(family.id, selectedPlanDate)}>제품 보기</button>}
                    {plan?.productsInitialized === false && <small className="busbar-note">계획을 저장해 대기 제품을 준비하세요.</small>}
                  </>];
              })} />
          </BusbarDialog>}
        </>
      )}
      {tab === "production" && (
        <>
          <DsSurface>
            <h3 ref={productionHeadingRef} tabIndex={-1}>생산 제품 목록</h3>
            <DsToolbar className="busbar-filterbar busbar-filterbar--production" label="생산 사진 필터">
              <Search value={query} onChange={setQuery} />
              <label>제품군 필터<select aria-label="제품군 필터" value={filters.productFamilyId ?? ""} onChange={(e) => changeFilter("productFamilyId", e.target.value)}><option value="">전체 제품군</option>{data.productFamilies.filter(f => !f.isDeleted).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
              <div className="busbar-filter-date-range">
                <label>계획 시작일<input type="date" value={filters.planDateFrom ?? ""} max={filters.planDateTo || undefined} onChange={(e) => changeFilter("planDateFrom", e.target.value)} /></label>
                <label>계획 종료일<input type="date" value={filters.planDateTo ?? ""} min={filters.planDateFrom || undefined} onChange={(e) => changeFilter("planDateTo", e.target.value)} /></label>
              </div>
              <label>생산 상태 필터<select aria-label="생산 상태 필터" value={filters.status ?? ""} onChange={(e) => changeFilter("status", e.target.value)}><option value="">전체 상태</option><option value="Draft">미완료</option><option value="Complete">생산 완료</option><option value="Cancelled">취소</option></select></label>
              <div className="busbar-filter-actions">
                <button onClick={() => { setFilters({}); setPage(1); setActiveProduct(""); setQuery(""); }}>필터 초기화</button>

                <button
                  type="button"
                  onClick={() => {
                    setTab("plans");
                    setQuery("");
                    setEditor(null);
                  }}
                >
                  생산계획으로 이동
                </button>
              </div>
            </DsToolbar>
            <DsToolbar label="제품 QR 선택">
              <label className="busbar-check-label"><input type="checkbox" aria-label="현재 목록 출력 가능 제품 모두 선택"
                checked={eligibleProducts.length > 0 && selectedIds.length === eligibleProducts.length}
                disabled={busy || eligibleProducts.length === 0}
                onChange={(event) => setSelectedQrIds(event.target.checked ? eligibleProducts.map((product) => product.id) : [])} />현재 목록 전체 선택</label>
              <span>{selectedIds.length}개 선택</span>
              <button disabled={busy || selectedIds.length === 0} onClick={prepareQrLabels}>선택 QR 인쇄</button>
            </DsToolbar>
            <Table headings={["선택", "사진등록", "제품군", "작업자", "생산일시", "생산 상태", "제품번호", "외부게시"]}
              rows={visibleProducts.map((product) => [
                <input type="checkbox" aria-label={`${productLabel(product)} QR 선택`} checked={selectedIds.includes(product.id)} disabled={busy || !canPrintQr(product)}
                  onChange={(event) => setSelectedQrIds((ids) => event.target.checked ? [...new Set([...ids, product.id])] : ids.filter((id) => id !== product.id))} />,
                <button disabled={busy} onClick={() => { setActiveProduct(product.id); setEditor(null); setFeedback(""); }}>{product.status === "Draft" && canWrite ? "사진등록" : "사진보기"}</button>,
                familyName(product.productFamilyId), product.workerName ?? "작업자 선택 전", busbarDateTime(product.manufacturedAtUtc), productionStatusLabel(product),
                productLabel(product),
                <><DsBadge tone={product.publicationState === "Failed" ? "danger" : product.publicationState === "Published" ? "success" : "neutral"}>{statusLabel(product.publicationState)}</DsBadge>
                  {product.qrState === "ConfigurationPending" && <small className="busbar-note"> · QR 공개 주소 설정 대기</small>}
                  {canWrite && product.status === "Complete" && product.publicationState !== "Published" && <button disabled={busy} onClick={() => void run(() => busbarApi.write(user, `/products/${product.id}/publication/retry`, {}), "외부 페이지 게시를 다시 요청했습니다.")}>게시 재시도</button>}</>,
              ])} />
          </DsSurface>
          {selectedProduct && (
            <BusbarDialog label={`${productLabel(selectedProduct)} · ${familyName(selectedProduct.productFamilyId)}`} busy={busy} heading={photoHeadingRef}
              closeLabel="사진 팝업 닫기" className="busbar-photo-dialog" fallbackFocus={productionHeadingRef}
              onClose={() => { if (!busy) { setActiveProduct(""); setProductDetail(null); setEditor(null); setFeedback(""); } }}>
              {feedback && <DsActionFeedback message={feedback} tone={feedbackError ? "error" : "success"} focusOnAttention />}
              {error && <DsActionFeedback message={error} tone="error" />}
              {editor && canWrite && <Editor key={editorKey} spec={editor} busy={busy}
                onClose={() => { if (!busy) setEditor(null); }}
                onSave={async (values) => {
                  const ok = await run(() => busbarApi.write(user, editor.path, editor.makeBody(values), editor.method));
                  if (ok) { setEditor(null); requestAnimationFrame(() => photoHeadingRef.current?.focus()); }
                }} />}
              {selectedProduct.status === "Complete" && (
                <div
                  ref={completionRef}
                  tabIndex={-1}
                  className="busbar-completion"
                  role="status"
                >
                  <span>생산 완료 · 제품번호</span>
                  <strong>{selectedProduct.number}</strong>
                  <p>
                    이 번호를 임시 스티커에 표시하세요. QR은 외부 게시가 완료된
                    뒤 같은 번호로 출력할 수 있습니다.
                  </p>
                </div>
              )}
              {selectedProduct.workerName && <p className="busbar-note">제조 작업자: {selectedProduct.workerName}</p>}
              {canWrite && selectedProduct.status !== "Cancelled" && writeButton("작업자 정정", () => open({
                title: "작업자 정정", path: `/products/${selectedProduct.id}`, method: "PATCH",
                fields: [{ key: "workerId", label: "실제 제조 작업자", type: "select", options: options(data.workers, selectedProduct.workerId ?? undefined), value: selectedProduct.workerId ?? undefined }, reasonField],
                makeBody: (values) => values,
              }))}
              {canAdminister && selectedProduct.status !== "Cancelled" && <BusbarRecordAction user={user} path={`/products/${selectedProduct.id}/cancel`} label={productLabel(selectedProduct)} action="생산 취소" disabled={busy} reversal onChanged={async () => { await load(); setFeedback("생산을 취소했습니다. 사진과 처리 이력은 보존했습니다."); setFeedbackError(false); requestAnimationFrame(() => photoHeadingRef.current?.focus()); }} />}
              <PhotoWorkspace
                key={selectedProduct.id}
                user={user}
                product={selectedProduct}
                workers={data.workers}
                canWrite={canWrite && selectedProduct.status === "Draft"}
                busy={busy}
                run={run}
              />
              {selectedProduct.qrState === "ConfigurationPending" && <p className="busbar-note">QR 공개 주소 설정 후 출력할 수 있습니다. 생산 기록은 정상 반영되었습니다.</p>}
            </BusbarDialog>
          )}
          {qrDialogOpen && <BusbarDialog label="선택 제품 QR 인쇄" busy={busy} heading={qrHeadingRef} closeLabel="QR 인쇄 팝업 닫기" className="busbar-print-dialog" fallbackFocus={productionHeadingRef}
            onClose={() => { if (!busy) { setQrDialogOpen(false); setPrintArmed(false); setQrLabels([]); setFeedback(""); } }}>
            {feedback && <DsActionFeedback message={feedback} tone={feedbackError ? "error" : "success"} focusOnAttention />}
            {busy && qrLabels.length === 0 && <p>선택 제품의 게시 상태와 QR 이미지를 확인하는 중입니다.</p>}
            {qrLabels.length > 0 && <>
              <p>{qrLabels.length}개 제품 · 제품번호와 실물을 맞춰 부착하세요.</p>
              <div className="busbar-qr-preview">{qrLabels.map((label) => <div key={label.productId} className="busbar-qr-label"><img src={label.url} alt={`${label.number} QR`} /><strong>{label.number}</strong></div>)}</div>
              <button disabled={busy} onClick={() => void run(async () => {
                try {
                  await Promise.all(Array.from(printRootRef.current?.querySelectorAll("img") ?? []).map((img) => img.decode()));
                  await validateLabels(qrLabels);
                } catch (error) { setQrLabels([]); throw error; }
                flushSync(() => setPrintArmed(true));
                try { window.print(); } catch (error) { setPrintArmed(false); throw error; }
              }, "QR 인쇄를 요청했습니다.")}>인쇄</button>
              {createPortal(<div ref={printRootRef} className={printArmed ? "busbar-print-sheet" : "busbar-print-buffer"}>{qrLabels.map((label) => <div key={label.productId} className="busbar-qr-label"><img src={label.url} alt={`${label.number} 인쇄 QR`} /><p>{label.number}</p></div>)}</div>, document.body)}
            </>}
          </BusbarDialog>}
        </>
      )}
      {tab === "purchases" && (
        <>
          <DsActionFeedback
            tone="info"
            message="이카운트 자동 연동은 아직 준비되지 않았습니다. 현재는 발주를 직접 등록하거나 엑셀로 가져와 주세요."
          />
          <DsSurface label="발주·입고 현황">
            <h3>발주·입고 현황</h3>
            <DsToolbar className="busbar-filterbar busbar-filterbar--single" label="발주 검색">
              <Search value={query} onChange={setQuery} />
              {deletionFilter}
              <div className="busbar-filter-actions">{writeButton("발주 등록", () => purchaseEditor())}
                {canWrite && <ImportBox data={data} user={user} kind="purchases" busy={busy} run={run} />}
              </div>
            </DsToolbar>
            <Table
              headings={[
                "발주번호",
                "자재",
                "발주일",
                "발주 수량",
                "누적 입고",
                "미입고 잔여",
                "작업",
              ]}
              rows={data.purchases
                .filter((p) =>
                  Boolean(p.isDeleted) === showDeleted && matches(p.orderNumber, materialName(p.materialId)),
                )
                .map((p) => [
                  p.orderNumber,
                  materialName(p.materialId),
                  p.orderDate.slice(0, 10),
                  n(p.quantity),
                  n(p.receivedQuantity),
                  n(Math.max(0, p.quantity - p.receivedQuantity)),
                  <>
                    {!p.isDeleted && writeButton("분할 입고", () => {
                      const requestId = crypto.randomUUID();
                      open({
                        title: `${p.orderNumber} 분할 입고`,
                        path: "/receipts",
                        fields: [quantityField("실제 입고 수량")],
                        makeBody: (v) => ({
                          requestId,
                          purchaseId: p.id,
                          quantity: Number(v.quantity),
                        }),
                      });
                    })}
                    {!p.isDeleted && writeButton("발주 정정", () => purchaseEditor(p.id))}
                    {recordAction("purchases", p, p.orderNumber)}
                  </>,
                ])}
            />
            <p className="busbar-note">
              발주 수신은 재고를 증가시키지 않습니다. 실제 도착한 수량만 입고
              처리하세요.
            </p>
          </DsSurface>
          <DsSurface label="입고·재고 처리 이력">
            <h3 ref={ledgerHeadingRef} tabIndex={-1}>입고·재고 처리 이력</h3>
            {data.pagination && <DsToolbar label="재고 이력 페이지"><button disabled={busy || page <= 1} onClick={() => setPage(value => value - 1)}>이전 이력</button><span>{page}페이지</span><button disabled={busy || page * data.pagination.pageSize >= data.pagination.ledgerCount} onClick={() => setPage(value => value + 1)}>다음 이력</button></DsToolbar>}
            <p className="busbar-note">취소하면 재고에 반대 수량을 반영합니다. 처리 기록은 남으며 취소 자체는 복원되지 않습니다.</p>
            <Table headings={["처리일", "구분", "사유", "상태", "관리"]} rows={data.ledger.filter(entry => ["Receipt", "Adjustment", "Opening"].includes(entry.kind)).map(entry => [busbarDateTime(entry.createdAtUtc), entry.kind === "Receipt" ? "입고" : "재고 보정", entry.reason, entry.reversed ? "취소됨" : "반영", canAdminister && !entry.reversed && <BusbarRecordAction key={entry.id} user={user} path={`/ledger/${entry.id}/reverse`} label={`${busbarDateTime(entry.createdAtUtc)} ${entry.kind === "Receipt" ? "입고" : "재고 보정"}`} action="처리 취소" reversal disabled={busy} onChanged={async () => { await load(); setFeedback("재고 처리를 취소했습니다. 기존 기록은 보존했습니다."); setFeedbackError(false); requestAnimationFrame(() => ledgerHeadingRef.current?.focus()); }} />])} />
          </DsSurface>
          <DsSurface label="자재 재고">
            <DsToolbar>
              <h3>자재 재고</h3>
              {writeButton("자재 기초재고", () => adjustment("Material", true), canAdminister)}
              {writeButton("자재 재고 보정", () => adjustment("Material"), canAdminister)}
            </DsToolbar>
            <Table
              headings={["품목 코드", "자재", "공급 구분", "단위", "현재고"]}
              rows={data.materials.filter(x => !x.isDeleted).map((x) => [
                x.code,
                x.name,
                x.supplyType,
                x.unit,
                <span className={stock(x) < 0 ? "busbar-negative" : ""}>
                  {n(stock(x))}
                  {stock(x) < 0 ? " · 부족" : ""}
                </span>,
              ])}
            />
          </DsSurface>
          <DsSurface label="완제품 재고">
            <DsToolbar>
              <h3>완제품 공용 재고</h3>
              {writeButton("완제품 기초재고", () =>
                adjustment("Finished", true), canAdminister,
              )}
              {writeButton("완제품 재고 보정", () => adjustment("Finished"), canAdminister)}
            </DsToolbar>
            <Table
              headings={["제품군", "현재고"]}
              rows={data.productFamilies.filter(x => !x.isDeleted).map((x) => [x.name, n(stock(x))])}
            />
          </DsSurface>

        </>
      )}
      {tab === "masters" && access?.mastersRead && (
        <>
          {access.manageMasterPermissions && <BusbarMasterAccess key={user} user={user} />}
          <DsToolbar>{deletionFilter}</DsToolbar>
          <DsSurface>
            <DsToolbar>
              <h3>공통 프로젝트·이카운트 설정</h3>
              {writeButton("공통 코드 설정", () =>
                open({
                  title: "공통 프로젝트 코드 설정",
                  path: "/settings",
                  method: "PUT",
                  fields: [
                    {
                      key: "commonProjectCode",
                      label: "공통 프로젝트 코드",
                      value: data.settings.commonProjectCode,
                    },
                    { key: "ecountCustomerCode", label: "고정 거래처 코드 (엘에스일렉트릭)", value: data.settings.ecountCustomerCode, optional: true },
                    { key: "ecountWarehouseCode", label: "고정 출하창고 코드 (청주캠퍼스)", value: data.settings.ecountWarehouseCode, optional: true },
                  ],
                  makeBody: (v) => v,
                }),
              )}
            </DsToolbar>
            <p>{data.settings.commonProjectCode || "설정 전"}</p>
            <p>주문서·판매 담당자는 프로젝트 최초 등록자의 PMS 이름으로 자동 입력됩니다.</p>
          </DsSurface>
          {(
            [
              {
                key: "productFamilies",
                title: "제품군",
                path: "/product-families",
              },
              { key: "materials", title: "자재", path: "/materials" },
              { key: "workers", title: "외주 작업자", path: "/workers" },
            ] as const
          ).map((section) => (
            <DsSurface key={section.key} label={section.title}>
              <DsToolbar>
                <h3>{section.title}</h3>
                {writeButton(`${section.title} 등록`, () =>
                  editMaster(section.path, `${section.title} 등록`),
                )}
              </DsToolbar>
              <Table
                headings={[
                  "코드",
                  "명칭",
                  ...(section.key === "productFamilies" ? ["이카운트 품목 코드", "기준 단가 (원)"] : []),
                  ...(section.key === "materials" ? ["단위", "공급 구분"] : []),
                  "상태",
                  "작업",
                ]}
                rows={data[section.key].filter(x => Boolean(x.isDeleted) === showDeleted).map((x) => [
                  x.code,
                  x.name,
                  ...(section.key === "productFamilies" ? [x.ecountProductCode || "미설정", x.standardUnitPrice == null ? "미설정" : n(x.standardUnitPrice)] : []),
                  ...(section.key === "materials"
                    ? [x.unit, x.supplyType]
                    : []),
                  x.isDeleted ? "삭제됨" : x.isActive ? "사용" : "사용 중지",
                  <>{!x.isDeleted && writeButton("수정", () => editMaster(section.path, `${section.title} 수정`, x))}{recordAction(section.path.slice(1), x, x.name)}</>,
                ])}
              />
            </DsSurface>
          ))}
          <DsSurface label="표준 자재 소요량">
            <h3>제품 1개당 표준 자재 소요량</h3>
            <p className="busbar-note">
              수정하면 새 버전을 만듭니다. 과거 생산에는 생산 당시의 소요량을
              보존합니다.
            </p>
            <DsToolbar className="busbar-filterbar busbar-filterbar--single" label="소요량 제품군 선택">
              <label>
                소요량을 관리할 제품군
                <select
                  value={selectedBomFamily?.id ?? ""}
                  onChange={(e) => setBomFamily(e.target.value)}
                >
                  <option value="">제품군 선택</option>
                  {data.productFamilies.filter(x => !x.isDeleted).map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
            </DsToolbar>
            {selectedBomFamily && <Table headings={["버전", "등록일", "관리"]} rows={data.boms.filter(b => b.productFamilyId === bomFamily && Boolean(b.isDeleted) === showDeleted).map(b => [`버전 ${b.version}`, busbarDateTime(b.createdAtUtc), recordAction("boms", b, `${familyName(bomFamily)} 소요량 버전 ${b.version}`)])} />}
            {selectedBomFamily && !showDeleted && (
              <BomEditor
                key={`${bomFamily}:${data.boms
                  .filter((b) => b.productFamilyId === bomFamily)
                  .map((b) => `${b.version}:${b.isDeleted}`)
                  .join(",")}`}
                data={data}
                familyId={bomFamily}
                canWrite={canWrite}
                busy={busy}
                onSave={(lines) =>
                  run(
                    () =>
                      busbarApi.write(user, "/boms", {
                        productFamilyId: bomFamily,
                        lines,
                      }),
                    "새 소요량 버전을 저장했습니다.",
                  )
                }
              />
            )}
          </DsSurface>
        </>
      )}
    </div>
  );
}

export function Table({
  headings,
  rows,
  rowActions,
  rowClasses,
}: {
  headings: string[];
  rows: ReactNode[][];
  rowClasses?: string[];
  rowActions?: { expanded?: boolean; toggle: () => void }[];
}) {
  return rows.length ? (
    <div
      className="busbar-table-wrap"
      tabIndex={0}
      role="region"
      aria-label={`${headings[0]} 목록`}
    >
      <table>
        <thead>
          <tr>
            {headings.map((h, i) => (
              <th key={i} scope="col">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i} className={[rowActions ? "busbar-clickable-row" : "", rowClasses?.[i]].filter(Boolean).join(" ")}
              tabIndex={rowActions ? 0 : undefined} aria-expanded={rowActions?.[i].expanded}
              onClick={(event) => { if (!(event.target as HTMLElement).closest("button, a, input, select")) rowActions?.[i].toggle(); }}
              onKeyDown={(event) => { if (event.target === event.currentTarget && ["Enter", " "].includes(event.key) && rowActions) { event.preventDefault(); rowActions[i].toggle(); } }}>

              {row.map((cell, j) => (
                <td key={j}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <DsStatePanel
      kind="empty"
      title="등록된 항목이 없습니다."
      description="조회 조건을 확인하거나 새 항목을 등록하세요."
    />
  );
}
export function EcountStatus({userId, projectId, canWrite}: {userId: string; projectId: string; canWrite: boolean}) {
  const [data, setData] = useState<BusbarEcountStatus>();
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [retry, setRetry] = useState<string>();
  const [outcome, setOutcome] = useState<string>();
  const [slip, setSlip] = useState("");
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    let active = true;
    void busbarApi.ecountStatus(userId, projectId).then(value => { if(active) { setData(value); setError(""); } }, () => { if(active) setError("전송 상태를 조회하지 못했습니다."); });
    return () => { active = false; };
  }, [userId, projectId, revision]);
  const names = { Pending: "전송 대기", Held: "보류", InFlight: "전송 중", Succeeded: "전송 완료", Failed: "전송 실패", Unknown: "결과 확인 필요" };
  return <div className="busbar-ecount-status"><h4>이카운트 주문·판매</h4>
    <p className="busbar-note">{data?.transmissionEnabled ? `${data.environment === "Test" ? "테스트" : "운영"} 연결 · ${data.paused ? "자동 전송 중지" : "자동 전송 사용"}` : "실제 전송 연결 전입니다. 프로젝트 등록·분할 출하 시 전송 대기 내역을 보관합니다."}</p>
    {data?.connectionMessage && <p role="status">{data.connectionMessage}</p>}
    {canWrite && data?.transmissionEnabled && data.paused && <button disabled={saving} onClick={() => { setRetry("connection");setOutcome(undefined);setReason(""); }}>자동 전송 재개</button>}
    {error && <p role="alert">{error}</p>}
    {!data ? <p role="status">전송 상태 확인 중…</p> : <Table headings={["구분", "출하일시", "출하 수량", "상태", "전표번호", "안내", "작업"]} rows={data.jobs.map(job => [
      job.kind === "Order" ? "주문서" : job.shipmentId ? "출하별 판매" : "기존 판매", job.shippedAtUtc ? new Date(job.shippedAtUtc).toLocaleString("ko-KR", { timeZone: "Asia/Seoul" }) : "—", job.shipmentQuantity == null ? "—" : n(job.shipmentQuantity), job.needsReview ? `${names[job.state]} · 변경 확인 필요` : names[job.state], job.slipNumber || "—", job.message || "—",
      canWrite && !job.needsReview && (job.state === "Held" || job.state === "Failed") ? <button disabled={saving} onClick={() => {setRetry(job.id);setOutcome(undefined);setReason("");}}>다시 대기</button> : canWrite && (job.state === "Unknown" || job.state === "Succeeded" && job.needsReview) ? <button disabled={saving} onClick={() => {setRetry(job.id);setOutcome(job.state === "Unknown" ? "Recorded" : "Reviewed");setSlip("");setReason("");}}>전표 확인 반영</button> : "—"
    ])} />}
    <button disabled={saving} onClick={() => setRevision(value => value + 1)}>전송 상태 새로고침</button>
    {retry && canWrite && <form onSubmit={async event => {
      event.preventDefault(); if(saving || !reason.trim()) return;
      setSaving(true); setError("");
      try { await busbarApi.write(userId, retry === "connection" ? "/ecount/resume" : `/ecount-jobs/${retry}/${outcome ? "reconcile" : "retry"}`, outcome ? {reason, outcome, slipNumber: outcome === "Recorded" ? slip : null} : {reason}); setRetry(undefined); setRevision(value => value + 1); }
      catch { setError(retry === "connection" ? "자동 전송을 재개하지 못했습니다. 연결 설정과 처리 상태를 확인하세요." : outcome ? "확인 결과를 반영하지 못했습니다. 현재 전송 상태를 확인하세요." : "다시 대기하지 못했습니다. 납품 상태와 전송 결과를 확인하세요."); }
      finally { setSaving(false); }
    }}>{outcome && <><p className="busbar-note">이카운트에서 실제 전표를 확인한 결과를 입력하세요. 이미 보낸 전표의 변경은 이카운트에서 정정한 후 반영합니다.</p>{outcome !== "Reviewed" && <label>전표 확인 결과<select value={outcome} onChange={event => setOutcome(event.target.value)}><option value="Recorded">전표 있음</option><option value="NotRecorded">전표 없음</option></select></label>}{outcome === "Recorded" && <label>확인한 전표번호<input required maxLength={200} value={slip} onChange={event => setSlip(event.target.value)} /></label>}</>}<label>{retry === "connection" ? "재개 사유" : outcome ? "확인 사유" : "다시 대기 사유"}<input required maxLength={200} value={reason} onChange={event => setReason(event.target.value)} /></label><div className="busbar-ecount-actions"><button disabled={saving}>{retry === "connection" ? "재개 요청" : outcome ? "확인 반영" : "대기 등록"}</button><button type="button" disabled={saving} onClick={() => setRetry(undefined)}>닫기</button></div></form>}
  </div>;
}

export function CommercialPreview({ userId, projectId, revision }: { userId: string; projectId: string; revision: string }) {
  const [state, setState] = useState<{ data?: BusbarCommercialPreview; error?: string }>({});
  useEffect(() => {
    let active = true;
    void busbarApi.commercialPreview(userId, projectId).then(
      (data) => { if (active) setState({ data }); },
      () => { if (active) setState({ error: "금액 정보를 조회하지 못했습니다. 잠시 후 새로고침해 주세요." }); },
    );
    return () => { active = false; };
  }, [userId, projectId, revision]);
  if (!state.data) return <p role="status">{state.error || "금액 확인 중…"}</p>;
  const p = state.data;
  return <dl className="busbar-project-fields busbar-project-prices" aria-label="프로젝트 금액">
    <div><dt>제품군 단가</dt><dd>{p.unitPrice == null ? "미설정" : `${n(p.unitPrice)}원`}</dd></div>
    <div><dt>공급가액</dt><dd>{p.supplyAmount == null ? "미설정" : `${n(p.supplyAmount)}원`}</dd></div>
  </dl>;
}

function Search({
  value,
  onChange,
}: {
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <label>
      검색
      <input
        type="search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="이름, 제품군, 번호 검색"
      />
    </label>
  );
}
export function BusbarDialog({ label, busy, onClose, children, heading, closeLabel = "생산계획 팝업 닫기", className = "", fallbackFocus }: { label: string; busy: boolean; onClose: () => void; children: ReactNode; heading: RefObject<HTMLHeadingElement | null>; closeLabel?: string; className?: string; fallbackFocus?: RefObject<HTMLElement | null> }) {
  const panel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const original = document.activeElement as HTMLElement | null;
    const fallback = fallbackFocus?.current;
    heading.current?.focus();
    return () => { if (original?.isConnected) original.focus({ preventScroll: true }); else fallback?.focus({ preventScroll: true }); };
  }, [heading, fallbackFocus]);
  return <DsDialog label={label} onClose={onClose} closeDisabled={busy} className={`busbar-plan-dialog ${className}`}>
    <div className="dialog" ref={panel} onKeyDown={(event) => {
      if ((event.target as HTMLElement).closest(".dialog") !== panel.current) return;
      if (event.key === "Escape") { event.preventDefault(); if (!busy) onClose(); }
      if (event.key === "Tab") {
        const controls = Array.from(panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), [tabindex="0"]') ?? []).filter((element) => element.getClientRects().length > 0);
        const first = controls[0], last = controls.at(-1);
        if (!first) { event.preventDefault(); heading.current?.focus(); }
        else if (event.shiftKey && (document.activeElement === first || document.activeElement === heading.current)) { event.preventDefault(); last?.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
      }
    }}>
      <DsToolbar><h3 ref={heading} tabIndex={-1}>{label}</h3><button disabled={busy} onClick={onClose} aria-label={closeLabel}>닫기</button></DsToolbar>
      {children}
    </div>
  </DsDialog>;
}
export function Editor({
  spec,
  busy,
  onClose,
  onSave,
}: {
  spec: EditorSpec;
  busy: boolean;
  onClose: () => void;
  onSave: (values: Values) => Promise<void>;
}) {
  const [values, setValues] = useState<Values>(() =>
    Object.fromEntries(spec.fields.map((f) => [f.key, f.value ?? ""])),
  );
  const first = useRef<HTMLInputElement | HTMLSelectElement | null>(null);
  const firstEditable = spec.fields.findIndex((field) => !field.disabled);
  useEffect(() => first.current?.focus(), []);
  return (
    <DsSurface className="busbar-editor" label={spec.title}>
      <h3>{spec.title}</h3>
      {spec.note && <p className="busbar-note">{spec.note}</p>}
      <form
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          void onSave(values);
        }}
      >
        <fieldset disabled={busy}>
          <div className="busbar-form">
            {spec.fields.map((f, i) => (
              <label key={f.key}>
                {f.label}
                {f.optional ? " (선택)" : ""}
                {f.type === "select" ? (
                  <select
                    ref={i === firstEditable ? (element) => { first.current = element; } : undefined}
                    required={!f.optional}
                    disabled={f.disabled}
                    value={values[f.key]}
                    onChange={(e) =>
                      setValues({ ...values, [f.key]: e.target.value })
                    }
                  >
                    <option value="">선택하세요</option>
                    {f.options?.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    ref={i === firstEditable ? (element) => { first.current = element; } : undefined}
                    required={!f.optional}
                    disabled={f.disabled}
                    type={f.type ?? "text"}
                    value={values[f.key]}
                    min={f.min}
                    max={f.max}
                    step={f.step}
                    onChange={(e) =>
                      setValues({ ...values, [f.key]: e.target.value })
                    }
                  />
                )}
              </label>
            ))}
            <div className="busbar-form-actions">
              <button className="button primary" type="submit">
                {busy ? "저장 중…" : "저장"}
              </button>
              <button type="button" onClick={onClose}>
                닫기
              </button>
            </div>
          </div>
        </fieldset>
      </form>
    </DsSurface>
  );
}

type Run = (
  action: () => Promise<unknown>,
  message?: string,
) => Promise<boolean>;
function PhotoWorkspace({
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
  useEffect(() => setWorkerId(product.workerId ?? ""), [product.workerId]);
  const cannotUpload =
    busy ||
    (product.status === "Draft" && !workerId);
  return (
    <>
      {canWrite && product.status === "Draft" && (
        <label>
          촬영 제품의 실제 제조 작업자
          <select
            value={workerId}
            disabled={busy}
            onChange={(event) => setWorkerId(event.target.value)}
          >
            <option value="">작업자를 먼저 선택하세요</option>
            {workers
              .filter(
                (worker) => !worker.isDeleted && (worker.isActive || worker.id === product.workerId),
              )
              .map((worker) => (
                <option key={worker.id} value={worker.id}>
                  {worker.name}
                  {worker.isActive ? "" : " · 사용 중지"}
                </option>
              ))}
          </select>
        </label>
      )}
      {product.status === "Draft" && !workerId && (
        <p className="busbar-note">
          실제 제조 작업자를 선택하면 사진 촬영과 앨범 선택을 사용할 수
          있습니다.
        </p>
      )}
      {canWrite && product.status === "Draft" && <p className="busbar-note">JPEG·PNG·HEIC·WebP · 앞면·뒷면 합계 40MiB까지 등록할 수 있습니다. 위치·촬영기기 정보는 자동으로 제거됩니다.</p>}
      <div className="busbar-photogrid">
        {(["front", "back"] as const).map((side) => (
          <div className="busbar-photobox" key={side}>
            <h4>{side === "front" ? "앞면" : "뒷면"} 사진</h4>
            <PhotoPreview
              user={user}
              id={product.id}
              side={side}
              revision={product.revision}
              exists={side === "front" ? product.hasFront : product.hasBack}
            />
            {canWrite && product.status === "Draft" && (
              <>
                <label>
                  카메라로 {side === "front" ? "앞면" : "뒷면"} 촬영
                  <input
                    type="file"
                    accept={busbarPhotoAccept}
                    capture="environment"
                    disabled={cannotUpload}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file)
                        void run(
                          () =>
                            busbarApi.uploadPhoto(user, product.id, side, file, workerId),
                          "사진을 등록했습니다. 두 장이 모두 등록되면 자동으로 생산 완료됩니다.",
                        );
                      e.target.value = "";
                    }}
                  />
                </label>
                <label>
                  앨범에서 {side === "front" ? "앞면" : "뒷면"} 선택
                  <input
                    type="file"
                    accept={busbarPhotoAccept}
                    disabled={cannotUpload}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file)
                        void run(
                          () =>
                            busbarApi.uploadPhoto(user, product.id, side, file, workerId),
                          "사진을 등록했습니다.",
                        );
                      e.target.value = "";
                    }}
                  />
                </label>
              </>
            )}
          </div>
        ))}
      </div>
    </>
  );
}
export function PhotoPreview({
  user,
  id,
  side,
  revision,
  exists,
}: {
  user: string;
  id: string;
  side: string;
  revision: number;
  exists: boolean;
}) {
  const [src, setSrc] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    let url = "";
    setSrc("");
    setError("");
    if (exists)
      void busbarApi
        .photo(user, id, side)
        .then((blob) => {
          if (!active) return;
          url = URL.createObjectURL(blob);
          setSrc(url);
        })
        .catch((e) => {
          if (active) setError(failure(e));
        });
    return () => {
      active = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [user, id, side, revision, exists]);
  return src ? (
    <img src={src} alt={`${side === "front" ? "앞면" : "뒷면"} 등록 사진`} />
  ) : (
    <p className="busbar-note" role={error ? "alert" : undefined}>
      {error || (exists ? "사진을 불러오는 중…" : "아직 등록하지 않았습니다.")}
    </p>
  );
}
function BomEditor({
  data,
  familyId,
  canWrite,
  busy,
  onSave,
}: {
  data: BusbarWorkspace;
  familyId: string;
  canWrite: boolean;
  busy: boolean;
  onSave: (
    lines: { materialId: string; quantity: number }[],
  ) => Promise<boolean>;
}) {
  const history = data.boms
    .filter((x) => x.productFamilyId === familyId)
    .sort((a, b) => b.version - a.version);
  const latest = history[0]?.isDeleted ? undefined : history[0];
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      data.materials.map((m) => [
        m.id,
        String(
          data.bomLines.find(
            (x) => x.bomId === latest?.id && x.materialId === m.id,
          )?.quantity ?? "",
        ),
      ]),
    ),
  );
  return (
    <>
      <p>
        현재 버전: {latest ? latest.version : "미설정 · 생산 확정 전 설정 필요"}
      </p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void onSave(
            Object.entries(values)
              .filter(([, q]) => Number(q) > 0)
              .map(([materialId, q]) => ({ materialId, quantity: Number(q) })),
          );
        }}
      >
        <fieldset disabled={busy || !canWrite}>
          <div className="busbar-form">
            {data.materials
              .filter((m) => !m.isDeleted && (m.isActive || Number(values[m.id]) > 0))
              .map((m) => (
                <label key={m.id}>
                  {m.name} ({m.unit})
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={values[m.id] ?? ""}
                    onChange={(e) =>
                      setValues({ ...values, [m.id]: e.target.value })
                    }
                  />
                </label>
              ))}
            {canWrite && (
              <div className="busbar-form-actions">
                <button
                  type="submit"
                  disabled={!Object.values(values).some((x) => Number(x) > 0)}
                >
                  새 버전 저장
                </button>
              </div>
            )}
          </div>
        </fieldset>
      </form>
      <details>
        <summary>이전 소요량 버전</summary>
        {history.map((b) => (
          <div key={b.id}>
            <h4>버전 {b.version}</h4>
            <Table
              headings={["자재", "1개당 소요량", "단위"]}
              rows={data.bomLines
                .filter((x) => x.bomId === b.id)
                .map((l) => {
                  const m = data.materials.find((x) => x.id === l.materialId);
                  return [m?.name, n(l.quantity), m?.unit];
                })}
            />
          </div>
        ))}
      </details>
    </>
  );
}
function ImportBox({
  data,
  user,
  kind,
  busy,
  run,
}: {
  data: BusbarWorkspace;
  user: string;
  kind: "projects" | "purchases";
  busy: boolean;
  run: Run;
}) {
  const [expanded, setExpanded] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const [preview, setPreview] = useState<BusbarImport | null>(null);
  const [fileName, setFileName] = useState("");
  const project = kind === "projects";
  const labels: Record<string, string> = {
    id: "등록 식별자",
    name: "프로젝트명",
    customerJobNumber: "LSE Task No",
    productFamilyId: "제품군",
    requestedQuantity: "요청 수량",
    destination: "도착지",
    dueDate: "납품예정일",
    reason: "정정 사유",
    orderNumber: "발주번호",
    materialId: "자재",
    quantity: "발주 수량",
    orderDate: "발주일",
  };
  const download = (
        <button
          disabled={busy}
          onClick={() =>
            void run(async () => {
              const blob = await busbarApi.template(user, kind);
              const url = URL.createObjectURL(blob);
              const a = document.createElement("a");
              a.href = url;
              a.download = `부스바-${project ? "프로젝트" : "발주"}-양식.xlsx`;
              a.click();
              setTimeout(() => URL.revokeObjectURL(url), 1000);
            }, "엑셀 양식을 다운로드했습니다.")
          }
        >
          양식 다운로드
        </button>
  );
  const fileSelector = (
        <label>
          엑셀 파일 선택
          <input
            ref={fileInput}
            type="file"
            accept=".xlsx"
            disabled={busy}
            onChange={(e) => {
              const file = e.target.files?.[0];
              setPreview(null);
              if (file) {
                setFileName(file.name);
                void run(
                  async () =>
                    setPreview(
                      await busbarApi.upload<BusbarImport>(
                        user,
                        `/${kind}/import/preview`,
                        file,
                      ),
                    ),
                  "엑셀 미리보기를 확인하세요.",
                );
              }
              e.target.value = "";
            }}
          />
        </label>
  );
  const previewContent = (<>
        <p className="busbar-note">적용 전에 오류와 신규·갱신 대상을 확인하세요. 등록 식별자가 있는 행만 갱신하며 이름으로 합치지 않습니다.</p>        {preview && (
          <>
            <p>
              {fileName} · {preview.rows.length}행 · 오류{" "}
              {preview.errors.length}건
            </p>
            {preview.errors.length > 0 && (
              <ul role="alert">
                {preview.errors.map((x, i) => (
                  <li key={i}>
                    {typeof x === "string" ? x : "이 행의 형식을 확인하세요."}
                  </li>
                ))}
              </ul>
            )}
            <Table
              headings={[
                "처리",
                ...Object.keys(preview.rows[0] ?? {})
                  .filter((k) => k !== "id")
                  .map((k) => labels[k] ?? "항목"),
              ]}
              rows={preview.rows.map((row) => [
                row.id ? "갱신" : "신규",
                ...Object.entries(row)
                  .filter(([k]) => k !== "id")
                  .map(([k, v]) =>
                    k === "productFamilyId"
                      ? (data.productFamilies.find((x) => x.id === v)?.name ??
                        "제품군 확인 필요")
                      : k === "materialId"
                        ? (data.materials.find((x) => x.id === v)?.name ??
                          "자재 확인 필요")
                        : v === null
                          ? ""
                          : String(v),
                  ),
              ])}
            />
            <button
              disabled={
                busy || preview.errors.length > 0 || preview.rows.length === 0
              }
              onClick={() =>
                void run(async () => {
                  await busbarApi.write(user, `/${kind}/import/apply`, {
                    rows: preview.rows,
                  });
                  setPreview(null);
                }, "엑셀 내용을 적용했습니다.")
              }
            >
              검토한 내용 적용
            </button>
          </>
        )}</>);
  const importLabel = `${project ? "프로젝트" : "발주"} 엑셀`;
  return <div className="busbar-import-menu">
    <button type="button" disabled={busy} aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>{importLabel} {expanded ? "▴" : "▾"}</button>
    {expanded && <div className="busbar-import-actions">{download}<button type="button" disabled={busy} onClick={() => fileInput.current?.click()}>업로드</button></div>}
    <div hidden>{fileSelector}</div>
    {preview && <BusbarDialog label={`${importLabel} 미리보기`} busy={busy} heading={heading} closeLabel={`${importLabel} 미리보기 닫기`} onClose={() => setPreview(null)}>{previewContent}</BusbarDialog>}
  </div>;
}


export function BusbarRecordAction({ user, path, label, action, disabled, reversal = false, note, onChanged }: {
  user: string; path: string; label: string; action: string; disabled?: boolean; reversal?: boolean; note?: string; onChanged: () => Promise<unknown>;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const heading = useRef<HTMLHeadingElement>(null);
  const locked = useRef(false);
  const requestId = useRef("");
  const submittedReason = useRef<string | undefined>(undefined);
  const [reasonLocked, setReasonLocked] = useState(false);
  return <>
    <button type="button" disabled={disabled || busy} aria-label={`${label} ${action}`} onClick={() => { setReason(""); setError(""); setDone(false); setReasonLocked(false); submittedReason.current = undefined; requestId.current = crypto.randomUUID(); setOpen(true); }}>{action}</button>
    {open && <BusbarDialog className="busbar-record-dialog" label={`${action} 확인`} busy={busy} heading={heading} closeLabel="확인창 닫기" onClose={() => setOpen(false)}>
      <div className="busbar-record-description"><strong>{label}</strong></div>
      <div className="busbar-record-description">{reversal ? "재고에 반대 수량을 반영하고 처리 기록과 사진은 보존합니다. 취소 자체는 복원할 수 없습니다." : action === "복원" ? "일반 목록에서 다시 사용할 수 있도록 복원합니다. 취소된 생산 제품은 다시 만들지 않으며 이카운트 전송은 자동 재개하지 않습니다." : "일반 목록과 집계에서 제외합니다. 거래·사진·처리 이력은 보존하며 이카운트 전표는 자동 취소하지 않습니다. 삭제된 항목 보기에서 복원할 수 있습니다."}</div>
      {note && <div className="busbar-record-description" role="note">{note}</div>}
      {error && <DsActionFeedback message={error} tone="error" focusOnAttention />}
      {done ? <DsActionFeedback message={`${action}했습니다.`} tone="success" focusOnAttention /> : <form onSubmit={async event => {
        event.preventDefault(); if (locked.current || !reason.trim()) return;
        locked.current = true; setBusy(true); setError("");
        try { if (reversal) { submittedReason.current ??= reason.trim(); setReasonLocked(true); } await busbarApi.write(user, path, { reason: submittedReason.current ?? reason.trim(), ...(reversal ? { requestId: requestId.current } : {}) }); setDone(true); await onChanged(); }
        catch (cause) { setError(failure(cause)); }
        finally { locked.current = false; setBusy(false); }
      }}>
        <label>{action} 사유<input value={reason} onChange={event => setReason(event.target.value)} maxLength={200} required disabled={busy || reasonLocked} /></label>
        <div className="busbar-form-actions"><button type="button" disabled={busy} onClick={() => setOpen(false)}>돌아가기</button><button type="submit" disabled={busy || !reason.trim()}>{busy ? "처리 중…" : `${action} 확인`}</button></div>
      </form>}
    </BusbarDialog>}
  </>;
}
