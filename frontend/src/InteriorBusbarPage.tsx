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
  busbarDateTime,
  busbarNumber as n,
  type BusbarImport,
  type BusbarLedger,
  type BusbarMaster,
  type BusbarProduct,
  type BusbarWorkspace,
  type BusbarProductFilters,
  type BusbarCommercialPreview,
  type BusbarEcountStatus,
} from "./interiorBusbar";
import "./interior-busbar.css";
import { busbarSections, type BusbarSection } from "./interiorBusbarNavigation";
import { OsanPageHeading } from "./OsanListFrame";

type QrLabel = { productId: string; number: string; revision: number; url: string };
type Values = Record<string, string>;
type Field = {
  key: string;
  label: string;
  type?: "text" | "number" | "date" | "select";
  options?: { value: string; label: string }[];
  value?: string;
  disabled?: boolean;
  min?: number;
  max?: number;
  step?: string;
  optional?: boolean;
};
type EditorSpec = {
  title: string;
  fields: Field[];
  path: string;
  method?: string;
  makeBody: (values: Values) => unknown;
  after?: (result: { id: string }) => void;
  note?: string;
};
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
const operationLabel = (kind: string) =>
  ({
    Opening: "기초재고",
    Adjustment: "재고 보정",
    Receipt: "입고",
    Shipment: "출하",
    Production: "생산",
    Reversal: "취소·복원",
  })[kind] ?? "재고 변경";

export function InteriorBusbarPage({
  developmentUserKey: user,
  section: tab,
  onNavigate: setTab,
}: {
  developmentUserKey: string;
  section: BusbarSection;
  onNavigate: (section: BusbarSection) => void;
}) {
  const [data, setData] = useState<BusbarWorkspace | null>(null);
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  const [page, setPage] = useState(1);
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
  const [activeProject, setActiveProject] = useState("");
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
  const projectDetailRef = useRef<HTMLDivElement>(null);
  useEffect(() => { if (activeProject) projectDetailRef.current?.scrollIntoView({ behavior: "smooth", block: "start" }); }, [activeProject]);
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
      const next = await busbarApi.workspace(user, page, filters);
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
  }, [user, page, filters]);
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
      .filter((x) => x.isActive || x.id === include)
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
    const row = data?.projects.find((x) => x.id === id);
    open({
      title: row ? "납품 프로젝트 정정" : "납품 프로젝트 등록",
      path: "/projects",
      note: `공통 프로젝트 코드: ${data?.settings.commonProjectCode || "기준정보에서 먼저 설정하세요."} · 원화, 부가세 별도 10%. 단가는 제품군 기준정보에서 관리합니다.`,
      fields: [
        { key: "name", label: "프로젝트명", value: row?.name },
        {
          key: "customerJobNumber",
          label: "W/O No (고객 업무번호)",
          value: row?.customerJobNumber,
          optional: true,
        },
        familyField(row?.productFamilyId),
        {
          key: "requestedQuantity",
          label: "요청 수량",
          type: "number",
          min: 1,
          step: "1",
          value: String(row?.requestedQuantity ?? ""),
        },
        {
          key: "destination",
          label: "도착지 / 업체명",
          value: row?.destination,
        },
        {
          key: "dueDate",
          label: "납품예정일",
          type: "date",
          value: row?.dueDate?.slice(0, 10) ?? today(),
        },
        ...(row ? [reasonField] : []),
      ],
      makeBody: (v) => ({
        ...v,
        id: row?.id ?? null,
        requestedQuantity: Number(v.requestedQuantity),
      }),
    });
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
  function reverse(row: BusbarLedger) {
    const requestId = crypto.randomUUID();
    open({
      title: `${operationLabel(row.kind)} 취소`,
      path: `/ledger/${row.id}/reverse`,
      fields: [reasonField],
      note: "원래 기록은 보존하고 반대 수량을 기록합니다. 생산 취소는 생산 화면에서 처리하세요.",
      makeBody: (v) => ({ ...v, requestId }),
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
  const selectedProject = data.projects.find((x) => x.id === activeProject);
  const matches = (...values: unknown[]) =>
    values.join(" ").toLocaleLowerCase().includes(query.toLocaleLowerCase());
  const pendingProjects = data.projects.filter((project) => project.requestedQuantity > project.shippedQuantity)
    .sort((a, b) => a.dueDate.localeCompare(b.dueDate) || a.name.localeCompare(b.name) || a.id.localeCompare(b.id));
  const visibleProjects = data.projects.filter((p) => matches(p.name, p.customerJobNumber, p.destination, familyName(p.productFamilyId)) &&
    (!projectStatus || (projectStatus === "Complete" ? p.requestedQuantity === p.shippedQuantity : p.requestedQuantity > p.shippedQuantity)));
  const selectedStock = data.productFamilies.find((x) => x.id === selectedProject?.productFamilyId)?.balance ?? 0;
  const selectedRemaining = selectedProject ? selectedProject.requestedQuantity - selectedProject.shippedQuantity : 0;
  const maxShipment = Math.max(0, Math.min(selectedStock, selectedRemaining));
  const monthFirst = `${month}-01`;
  const monthLast = datePlus(`${monthPlus(month, 1)}-01`, -1);
  const calendarStart = datePlus(monthFirst, -new Date(`${monthFirst}T12:00:00Z`).getUTCDay());
  const calendarDays = Math.ceil((new Date(`${monthFirst}T12:00:00Z`).getUTCDay() + Number(monthLast.slice(8))) / 7) * 7;
  const visiblePlanFamilies = data.productFamilies.filter((f) => !planFamily || f.id === planFamily);
  function changeMonth(next: string) {
    setMonth(next); setSelectedPlanDate(`${next}-01`); setEditor(null);
  }
  function changeFilter(key: keyof BusbarProductFilters, value: string) {
    setFilters((current) => ({ ...current, [key]: value }));
    setPage(1); setActiveProduct("");
  }
  const stock = (item?: BusbarMaster) => item?.balance ?? 0;
  const canWrite = data.canWrite;
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
  const writeButton = (label: string, action: () => void) =>
    canWrite ? (
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
      {!canWrite && (
        <DsReadOnlyBanner description="조회 권한으로 접속했습니다. 입력과 정정은 인테리어 부스바 담당자가 처리합니다." />
      )}
      {error && <DsActionFeedback message={error} tone="error" />}
      {["production", "purchases"].includes(tab) && data.pagination && (
        <DsToolbar label="기록 페이지">
          <button
            disabled={busy || page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            이전 기록
          </button>
          <span>
            {page}페이지 ·{" "}
            {tab === "production"
              ? data.pagination.productCount
              : data.pagination.ledgerCount}
            건
          </span>
          <button
            disabled={
              busy ||
              page * data.pagination.pageSize >=
                (tab === "production"
                  ? data.pagination.productCount
                  : data.pagination.ledgerCount)
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
          <DsSurface label="진행 중 납품 프로젝트">
            <h3>진행 중 납품 프로젝트 · 납기순</h3>
            <Table headings={["프로젝트명", "제품군", "납품예정일", "요청", "누적 출하", "잔여"]}
              rowClasses={pendingProjects.map((project) => project.dueDate.slice(0, 10) >= today() && project.dueDate.slice(0, 10) <= datePlus(today(), 3) ? "busbar-due-soon" : "")}
              rows={pendingProjects.map((project) => [project.name, familyName(project.productFamilyId), project.dueDate.slice(0, 10), n(project.requestedQuantity), n(project.shippedQuantity), n(project.requestedQuantity - project.shippedQuantity)])} />
          </DsSurface>
          <DsSurface label="제품군별 현황">
            <h3>제품군별 생산·납품 현황</h3>
            <Table
              headings={[
                "제품군",
                "전체 계획",
                "생산 실적",
                "완제품 현재고",
                "납품 잔여",
              ]}
              rows={data.productFamilies.map((x) => [
                x.name,
                n(x.plannedQuantity),
                n(x.producedQuantity),
                n(stock(x)),
                n(
                  data.projects
                    .filter((p) => p.productFamilyId === x.id)
                    .reduce(
                      (a, p) => a + p.requestedQuantity - p.shippedQuantity,
                      0,
                    ),
                ),
              ])}
            />
            <p className="busbar-note">
              현재고는 모든 프로젝트가 함께 사용하는 수량입니다. 프로젝트별 예약
              수량을 의미하지 않습니다.
            </p>
          </DsSurface>
          <DsSurface label="부족 자재">
            <h3>부족 자재</h3>
            <Table
              headings={["품목", "단위", "현재고", "부족 수량"]}
              rows={data.materials
                .filter((x) => stock(x) < 0)
                .map((x) => [
                  x.name,
                  x.unit,
                  <span className="busbar-negative">{n(stock(x))}</span>,
                  n(-stock(x)),
                ])}
            />
          </DsSurface>
        </>
      )}
      {tab === "projects" && (
        <>
          <DsSurface>
            <DsToolbar label="납품 프로젝트 도구" className="busbar-filterbar">
              <Search value={query} onChange={setQuery} />
              <label>프로젝트 상태<select aria-label="프로젝트 상태" value={projectStatus} onChange={(event) => { setProjectStatus(event.target.value); setActiveProject(""); }}><option value="">전체</option><option value="InProgress">진행 중</option><option value="Complete">완료</option></select></label>
              <div className="busbar-filter-actions">{writeButton("프로젝트 등록", () => projectEditor())}</div>
            </DsToolbar>
            <Table
              headings={[
                "프로젝트명",
                "고객 업무번호",
                "제품군",
                "납품예정일",
                "도착지",
                "요청",
                "누적 출하",
                "잔여",
                "상태",
                "작업",
              ]}
              rowActions={visibleProjects.map((p) => ({ expanded: activeProject === p.id, toggle: () => setActiveProject(activeProject === p.id ? "" : p.id) }))}
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
                    {p.requestedQuantity === p.shippedQuantity
                      ? "완료"
                      : "진행 중"}
                  </DsBadge>,
                  writeButton("정정", () => projectEditor(p.id)),
                ])}
            />
          </DsSurface>
          {selectedProject && (
            <DsSurface label="선택 프로젝트 상세">
              <div ref={projectDetailRef} className="busbar-project-anchor" />
              <DsToolbar>
                <h3>{selectedProject.name}</h3>
                {writeButton("분할 출하", () => {
                  const requestId = crypto.randomUUID();
                  open({
                    title: "분할 출하",
                    path: "/shipments",
                    fields: [
                      { ...quantityField("이번 출하 수량"), min: 1, max: maxShipment, step: "1" },
                    ],
                    note: `납품 잔여 ${n(selectedRemaining)}개 · 공용 현재고 ${n(selectedStock)}개 · 현재 최대 출하 가능 ${n(maxShipment)}개`,
                    makeBody: (v) => ({
                      requestId,
                      projectId: selectedProject.id,
                      quantity: Number(v.quantity),
                    }),
                  });
                })}
              </DsToolbar>
              <p className="busbar-note">
                공통 코드 {selectedProject.commonProjectCode} ·{" "}
                {familyName(selectedProject.productFamilyId)} · 제품번호를 출하
                수량에 임의 연결하지 않습니다.
              </p>
              <div className="busbar-project-summary">
                <dl className="busbar-shipping-summary">
                  <div><dt>제품군 공용 재고</dt><dd>{n(selectedStock)}개</dd></div>
                  <div><dt>요청 수량</dt><dd>{n(selectedProject.requestedQuantity)}개</dd></div>
                  <div><dt>누적 출하</dt><dd>{n(selectedProject.shippedQuantity)}개</dd></div>
                  <div><dt>납품 잔여</dt><dd>{n(selectedRemaining)}개</dd></div>
                </dl>
                <CommercialPreview key={`${user}:${selectedProject.id}:${data.productFamilies.find((f) => f.id === selectedProject.productFamilyId)?.standardUnitPrice}:${selectedProject.requestedQuantity}:${data.settings.commonProjectCode}:${data.settings.ecountCustomerCode}:${data.settings.ecountWarehouseCode}:${data.productFamilies.find((f) => f.id === selectedProject.productFamilyId)?.ecountProductCode}`} userId={user} projectId={selectedProject.id} revision={`${data.productFamilies.find((f) => f.id === selectedProject.productFamilyId)?.standardUnitPrice}:${selectedProject.requestedQuantity}:${data.settings.ecountCustomerCode}:${data.settings.ecountWarehouseCode}:${data.productFamilies.find((f) => f.id === selectedProject.productFamilyId)?.ecountProductCode}`} />
                <div>
                  <h4>해당 제품군 날짜별 생산계획</h4>
                  <Table headings={["생산일", "계획", "완료", "미완료", "재고"]}
                    rows={data.plans.filter((p) => p.productFamilyId === selectedProject.productFamilyId)
                      .sort((a, b) => a.planDate.localeCompare(b.planDate))
                      .map((p) => [p.planDate.slice(0, 10), `${n(p.quantity)}개`, `${n(p.actualQuantity)}개`, `${n(Math.max(0, p.quantity - (p.actualQuantity ?? 0)))}개`, `${n(selectedStock)}개`])} />
                </div>
              </div>
              <p className="busbar-note">공용 재고는 이 프로젝트에 예약된 수량이 아닙니다. 생산 예정 수량은 현재 출하 가능 수량에 포함하지 않습니다.</p>
                <EcountStatus key={`${user}:${selectedProject.id}:${selectedProject.shippedQuantity}:${selectedProject.requestedQuantity}`} userId={user} projectId={selectedProject.id} canWrite={canWrite} />
              <h3>출하 이력</h3>
              <Table
                headings={["처리 시각", "출하 수량", "상태", "작업"]}
                rows={data.shipments
                  .filter((x) => x.projectId === selectedProject.id)
                  .map((x) => {
                    const op: BusbarLedger = {
                      id: x.id,
                      kind: "Shipment",
                      createdAtUtc: x.createdAtUtc,
                      reason: "",
                      reversed: x.reversed,
                    };
                    return [
                      busbarDateTime(x.createdAtUtc),
                      n(x.quantity),
                      x.reversed ? "취소" : "반영",
                      !x.reversed && op
                        ? writeButton("출하 취소", () => reverse(op))
                        : null,
                    ];
                  })}
              />
            </DsSurface>
          )}
          {canWrite && (
            <ImportBox
              data={data}
              user={user}
              kind="projects"
              busy={busy}
              run={run}
            />
          )}
        </>
      )}
      {tab === "plans" && (
        <>
          <DsSurface label="제품군별 월간 생산계획">
            <DsToolbar className="busbar-filterbar" label="생산계획 필터">
              <label>
                계획 제품군
                <select aria-label="계획 제품군" value={planFamily} onChange={(e) => setPlanFamily(e.target.value)}>
                  <option value="">전체 제품군</option>
                  {data.productFamilies.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
              </label>
              <label>계획 월<input type="month" value={month} onChange={(e) => { if (e.target.value) changeMonth(e.target.value); }} /></label>
              <div className="busbar-filter-actions">
                <button onClick={() => changeMonth(monthPlus(month, -1))}>이전 달</button>
                <button onClick={() => { changeMonth(today().slice(0, 7)); setSelectedPlanDate(today()); }}>이번 달</button>
                <button onClick={() => changeMonth(monthPlus(month, 1))}>다음 달</button>
              </div>
            </DsToolbar>
            <h3 ref={calendarHeadingRef} tabIndex={-1}>{Number(month.slice(0, 4))}년 {Number(month.slice(5))}월 생산계획</h3>
            <p className="busbar-note">날짜를 선택하면 팝업에서 제품군별 계획을 입력·수정할 수 있습니다. 날짜 칸에는 제품군별 계획과 완료 수량을 표시합니다.</p>
            <div className="busbar-month-calendar" role="region" aria-label="월간 생산계획 달력" tabIndex={0}>
              <div className="busbar-calendar-weekdays" aria-hidden="true">{["일", "월", "화", "수", "목", "금", "토"].map((day) => <span key={day}>{day}</span>)}</div>
              <div className="busbar-calendar-grid">
                {Array.from({ length: calendarDays }, (_, index) => {
                  const date = datePlus(calendarStart, index);
                  if (!date.startsWith(month)) return <div key={date} className="busbar-calendar-outside" aria-hidden="true">{Number(date.slice(8))}</div>;
                  const plans = data.plans.filter((p) => p.planDate.slice(0, 10) === date && (!planFamily || p.productFamilyId === planFamily));
                  return <button key={date} type="button" className="busbar-calendar-day" aria-label={`${date} 생산계획 선택`}
                    aria-pressed={selectedPlanDate === date} aria-current={date === today() ? "date" : undefined}
                    onClick={() => { setSelectedPlanDate(date); setEditor(null); setFeedback(""); setPlanDialogOpen(true); }}>
                    <span className="busbar-calendar-date">{Number(date.slice(8))}{date === today() && <small>오늘</small>}</span>
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
                const plan = data.plans.find((p) => p.productFamilyId === family.id && p.planDate.slice(0, 10) === selectedPlanDate);
                return [family.name, plan ? `${n(plan.quantity)}개` : "미등록", plan ? `${n(plan.actualQuantity)}개` : "—",
                  plan ? `${n(Math.max(0, plan.quantity - (plan.actualQuantity ?? 0)))}개` : "—",
                  <>
                    {canWrite && (family.isActive || plan) && <button disabled={busy} aria-label={`${family.name} ${selectedPlanDate} 계획 ${plan ? "수정" : "등록"}`} onClick={() => planEditor(family.id, selectedPlanDate, plan?.id)}>{plan ? "계획 수정" : "계획 등록"}</button>}
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
              <label>제품군 필터<select aria-label="제품군 필터" value={filters.productFamilyId ?? ""} onChange={(e) => changeFilter("productFamilyId", e.target.value)}><option value="">전체 제품군</option>{data.productFamilies.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
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
                <button disabled={busy} onClick={() => { setActiveProduct(product.id); setEditor(null); setFeedback(""); }}>{product.status === "Draft" ? "사진등록" : "사진보기"}</button>,
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
          <DsSurface>
            <DsToolbar className="busbar-filterbar busbar-filterbar--single" label="발주 검색">
              <Search value={query} onChange={setQuery} />
              <div className="busbar-filter-actions">{writeButton("발주 등록", () => purchaseEditor())}</div>
            </DsToolbar>
            <Table
              headings={[
                "발주번호",
                "자재",
                "발주일",
                "발주 수량",
                "누적 입고",
                "작업",
              ]}
              rows={data.purchases
                .filter((p) =>
                  matches(p.orderNumber, materialName(p.materialId)),
                )
                .map((p) => [
                  p.orderNumber,
                  materialName(p.materialId),
                  p.orderDate.slice(0, 10),
                  n(p.quantity),
                  n(p.receivedQuantity),
                  <>
                    {writeButton("분할 입고", () => {
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
                    {writeButton("발주 정정", () => purchaseEditor(p.id))}
                  </>,
                ])}
            />
            <p className="busbar-note">
              발주 수신은 재고를 증가시키지 않습니다. 실제 도착한 수량만 입고
              처리하세요.
            </p>
          </DsSurface>
          {canWrite && (
            <ImportBox
              data={data}
              user={user}
              kind="purchases"
              busy={busy}
              run={run}
            />
          )}
          <DsSurface label="자재 재고">
            <DsToolbar>
              <h3>자재 재고</h3>
              {writeButton("자재 기초재고", () => adjustment("Material", true))}
              {writeButton("자재 재고 보정", () => adjustment("Material"))}
            </DsToolbar>
            <Table
              headings={["품목 코드", "자재", "공급 구분", "단위", "현재고"]}
              rows={data.materials.map((x) => [
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
                adjustment("Finished", true),
              )}
              {writeButton("완제품 재고 보정", () => adjustment("Finished"))}
            </DsToolbar>
            <Table
              headings={["제품군", "현재고"]}
              rows={data.productFamilies.map((x) => [x.name, n(stock(x))])}
            />
          </DsSurface>
          <DsSurface label="재고 변경 이력">
            <h3>입고·생산·출하·재고 정정 이력</h3>
            <Table
              headings={[
                "처리 시각",
                "구분",
                "증감 내역",
                "사유",
                "상태",
                "작업",
              ]}
              rows={data.ledger.map((x) => [
                busbarDateTime(x.createdAtUtc),
                operationLabel(x.kind),
                data.ledgerLines
                  .filter((l) => l.operationId === x.id)
                  .map((l, i) => (
                    <div key={i}>
                      {l.stockKind === "Material"
                        ? materialName(l.itemId)
                        : familyName(l.itemId)}{" "}
                      {l.quantity > 0 ? "+" : ""}
                      {n(l.quantity)}
                    </div>
                  )),
                x.reason,
                x.reversed ? "취소됨" : "반영",
                !x.reversed && !["Production", "Reversal"].includes(x.kind)
                  ? writeButton("취소·복원", () => reverse(x))
                  : null,
              ])}
            />
          </DsSurface>
        </>
      )}
      {tab === "masters" && (
        <>
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
            <h4>이카운트 담당자 연결</h4>
            <p>프로젝트 최초 등록자의 담당자 코드를 주문서·판매 전표에 사용합니다.</p>
            <Table headings={["PMS 사용자", "이카운트 담당자 코드", "설정"]}
              rows={(data.ecountEmployees ?? []).map((employee) => [
                employee.displayName, employee.employeeCode || "연결 필요",
                writeButton("담당자 연결", () => open({
                  title: `${employee.displayName} 담당자 연결`, path: "/ecount-employees", method: "PUT",
                  fields: [{ key: "employeeCode", label: "이카운트 담당자 코드", value: employee.employeeCode }, reasonField],
                  makeBody: (v) => ({ ...v, userId: employee.userId }),
                })),
              ])} />
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
                rows={data[section.key].map((x) => [
                  x.code,
                  x.name,
                  ...(section.key === "productFamilies" ? [x.ecountProductCode || "미설정", x.standardUnitPrice == null ? "미설정" : n(x.standardUnitPrice)] : []),
                  ...(section.key === "materials"
                    ? [x.unit, x.supplyType]
                    : []),
                  x.isActive ? "사용" : "사용 중지",
                  writeButton("수정", () =>
                    editMaster(section.path, `${section.title} 수정`, x),
                  ),
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
                  value={bomFamily}
                  onChange={(e) => setBomFamily(e.target.value)}
                >
                  <option value="">제품군 선택</option>
                  {data.productFamilies.map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
            </DsToolbar>
            {bomFamily && (
              <BomEditor
                key={`${bomFamily}:${data.boms
                  .filter((b) => b.productFamilyId === bomFamily)
                  .map((b) => b.version)
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

function Table({
  headings,
  rows,
  rowActions,
  rowClasses,
}: {
  headings: string[];
  rows: ReactNode[][];
  rowClasses?: string[];
  rowActions?: { expanded: boolean; toggle: () => void }[];
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
function EcountStatus({userId, projectId, canWrite}: {userId: string; projectId: string; canWrite: boolean}) {
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
    <p className="busbar-note">{data?.transmissionEnabled ? `${data.environment === "Test" ? "테스트" : "운영"} 연결 · ${data.paused ? "자동 전송 중지" : "자동 전송 사용"}` : "실제 전송 연결 전입니다. 등록·납품 완료 시 전송 대기 내역을 보관합니다."}</p>
    {data?.connectionMessage && <p role="status">{data.connectionMessage}</p>}
    {canWrite && data?.transmissionEnabled && data.paused && <button disabled={saving} onClick={() => { setRetry("connection");setOutcome(undefined);setReason(""); }}>자동 전송 재개</button>}
    {error && <p role="alert">{error}</p>}
    {!data ? <p role="status">전송 상태 확인 중…</p> : <Table headings={["구분", "상태", "전표번호", "안내", "작업"]} rows={data.jobs.map(job => [
      job.kind === "Order" ? "주문서" : "판매", job.needsReview ? `${names[job.state]} · 변경 확인 필요` : names[job.state], job.slipNumber || "—", job.message || "—",
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

function CommercialPreview({ userId, projectId, revision }: { userId: string; projectId: string; revision: string }) {
  const [state, setState] = useState<{ data?: BusbarCommercialPreview; error?: string }>({});
  useEffect(() => {
    let active = true;
    void busbarApi.commercialPreview(userId, projectId).then(
      (data) => { if (active) setState({ data }); },
      () => { if (active) setState({ error: "금액 정보를 조회하지 못했습니다. 프로젝트를 다시 펼쳐 주세요." }); },
    );
    return () => { active = false; };
  }, [userId, projectId, revision]);
  if (!state.data) return <p role="status">{state.error || "금액 확인 중…"}</p>;
  const p = state.data;
  return <div><h4>주문·판매 금액 확인</h4>
    <p>담당자: {p.registeredByName || "최초 등록자 확인 필요"} · {p.employeeCode || "담당자 코드 연결 필요"}</p>
    <Table headings={["제품군 단가", "공급가액", "부가세 (10%)", "합계"]}
      rows={[[p.unitPrice, p.supplyAmount, p.vatAmount, p.totalAmount].map((v) => v == null ? "미설정" : `${n(v)}원`)]} />
    <p className="busbar-note">원화·부가세 별도{!p.transmissionEnabled && " · 실제 전송 연결 전입니다."}{p.missingFields.length > 0 ? ` 설정 필요: ${p.missingFields.join(", ")}` : " 전송에 필요한 코드와 단가가 입력되어 있습니다."}</p>
  </div>;
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
function BusbarDialog({ label, busy, onClose, children, heading, closeLabel = "생산계획 팝업 닫기", className = "", fallbackFocus }: { label: string; busy: boolean; onClose: () => void; children: ReactNode; heading: RefObject<HTMLHeadingElement | null>; closeLabel?: string; className?: string; fallbackFocus?: RefObject<HTMLElement | null> }) {
  const panel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const original = document.activeElement as HTMLElement | null;
    const fallback = fallbackFocus?.current;
    heading.current?.focus();
    return () => { if (original?.isConnected) original.focus({ preventScroll: true }); else fallback?.focus({ preventScroll: true }); };
  }, [heading, fallbackFocus]);
  return <DsDialog label={label} onClose={onClose} closeDisabled={busy} className={`busbar-plan-dialog ${className}`}>
    <div className="dialog" ref={panel} onKeyDown={(event) => {
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
function Editor({
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
  const [reason, setReason] = useState("");
  const [workerId, setWorkerId] = useState(product.workerId ?? "");
  useEffect(() => setWorkerId(product.workerId ?? ""), [product.workerId]);
  const cannotUpload =
    busy ||
    (product.status === "Draft" && !workerId) ||
    (product.status === "Complete" && !reason.trim());
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
                (worker) => worker.isActive || worker.id === product.workerId,
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
            {canWrite && product.status !== "Cancelled" && (
              <>
                <label>
                  카메라로 {side === "front" ? "앞면" : "뒷면"} 촬영
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    capture="environment"
                    disabled={cannotUpload}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file)
                        void run(
                          () =>
                            busbarApi.upload(
                              user,
                              `/products/${product.id}/photos/${side}`,
                              file,
                              reason,
                              "PUT",
                              product.status === "Draft" ? workerId : undefined,
                            ),
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
                    accept="image/jpeg,image/png,image/webp"
                    disabled={cannotUpload}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file)
                        void run(
                          () =>
                            busbarApi.upload(
                              user,
                              `/products/${product.id}/photos/${side}`,
                              file,
                              reason,
                              "PUT",
                              product.status === "Draft" ? workerId : undefined,
                            ),
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
      {canWrite && product.status === "Complete" && (
        <label>
          사진 정정 사유
          <input
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="사유를 입력한 뒤 교체할 사진을 선택하세요."
          />
        </label>
      )}
    </>
  );
}
function PhotoPreview({
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
  const latest = history[0];
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
              .filter((m) => m.isActive || Number(values[m.id]) > 0)
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
  const [preview, setPreview] = useState<BusbarImport | null>(null);
  const [fileName, setFileName] = useState("");
  const project = kind === "projects";
  const labels: Record<string, string> = {
    id: "등록 식별자",
    name: "프로젝트명",
    customerJobNumber: "W/O No",
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
  return (
    <DsSurface label={`${project ? "프로젝트" : "발주"} 엑셀 업로드`}>
      <details>
        <summary>{project ? "프로젝트" : "발주"} 엑셀 업로드</summary>
        <p className="busbar-note">
          적용 전에 오류와 신규·갱신 대상을 확인하세요. 등록 식별자가 있는 행만
          기존 항목을 갱신하며 이름으로 합치지 않습니다.
        </p>
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
          엑셀 양식 다운로드
        </button>
        <label>
          엑셀 파일 선택
          <input
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
        {preview && (
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
        )}
      </details>
    </DsSurface>
  );
}
