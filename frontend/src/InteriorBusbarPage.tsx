import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ReactNode,
  type FormEvent,
} from "react";
import { ApiError } from "./api";
import {
  DsActionFeedback,
  DsBadge,
  DsKpiCard,
  DsKpiGrid,
  DsPageHeader,
  DsReadOnlyBanner,
  DsStatePanel,
  DsSurface,
  DsTabs,
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
} from "./interiorBusbar";
import "./interior-busbar.css";

type Values = Record<string, string>;
type Field = {
  key: string;
  label: string;
  type?: "text" | "number" | "date" | "select";
  options?: { value: string; label: string }[];
  value?: string;
  min?: number;
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
const tabs = [
  { key: "overview", label: "종합 현황" },
  { key: "projects", label: "납품 프로젝트" },
  { key: "plans", label: "생산계획" },
  { key: "production", label: "생산·사진·QR" },
  { key: "purchases", label: "구매·자재" },
  { key: "masters", label: "기준정보" },
];
const today = () =>
  new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Seoul" }).format(
    new Date(),
  );
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
}: {
  developmentUserKey: string;
}) {
  const [data, setData] = useState<BusbarWorkspace | null>(null);
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  const [page, setPage] = useState(1);
  const [tab, setTab] = useState("overview");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  const [feedbackError, setFeedbackError] = useState(false);
  const [editor, setEditor] = useState<EditorSpec | null>(null);
  const [editorKey, setEditorKey] = useState(0);
  const [activeProduct, setActiveProduct] = useState("");
  const [activeProject, setActiveProject] = useState("");
  const [bomFamily, setBomFamily] = useState("");
  const [qr, setQr] = useState<{
    url: string;
    number: string;
    productId: string;
    revision: number;
  } | null>(null);
  const generation = useRef(0);
  const locked = useRef(false);
  const load = useCallback(async () => {
    const current = ++generation.current;
    try {
      const next = await busbarApi.workspace(user, page);
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
  }, [user, page]);
  useEffect(() => {
    void load();
    const invalidate = () => {
      generation.current++;
    };
    return invalidate;
  }, [load]);
  useEffect(
    () => () => {
      if (qr) URL.revokeObjectURL(qr.url);
    },
    [qr],
  );
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
      await load();
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
      }),
    });
  }
  function projectEditor(id?: string) {
    const row = data?.projects.find((x) => x.id === id);
    open({
      title: row ? "납품 프로젝트 정정" : "납품 프로젝트 등록",
      path: "/projects",
      note: `공통 프로젝트 코드: ${data?.settings.commonProjectCode || "기준정보에서 먼저 설정하세요."}`,
      fields: [
        { key: "name", label: "프로젝트명", value: row?.name },
        {
          key: "customerJobNumber",
          label: "고객 업무번호",
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
  function planEditor(id?: string) {
    const row = data?.plans.find((x) => x.id === id);
    open({
      title: row ? "생산계획 수정" : "생산계획 등록",
      path: "/plans",
      fields: [
        familyField(row?.productFamilyId),
        {
          key: "planDate",
          label: "생산일",
          type: "date",
          value: row?.planDate?.slice(0, 10) ?? today(),
        },
        { ...quantityField("목표 수량", row?.quantity), min: 0, step: "1" },
      ],
      makeBody: (v) => ({
        ...v,
        id: row?.id ?? null,
        quantity: Number(v.quantity),
      }),
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
  const selectedProduct = data.products.find((x) => x.id === activeProduct);
  const selectedProject = data.projects.find((x) => x.id === activeProject);
  const matches = (...values: unknown[]) =>
    values.join(" ").toLocaleLowerCase().includes(query.toLocaleLowerCase());
  const stock = (item?: BusbarMaster) => item?.balance ?? 0;
  const canWrite = data.canWrite;
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
    <div className="busbar-page" aria-busy={busy}>
      <DsPageHeader
        eyebrow="청주 PMS"
        title="인테리어 부스바"
        description="제품군별 생산과 공용 재고, 프로젝트별 분할 출하를 관리합니다."
        actions={
          <button type="button" disabled={busy} onClick={() => void load()}>
            새로고침
          </button>
        }
      />
      {!canWrite && (
        <DsReadOnlyBanner description="조회 권한으로 접속했습니다. 입력과 정정은 인테리어 부스바 담당자가 처리합니다." />
      )}
      {error && <DsActionFeedback message={error} tone="error" />}
      <DsTabs
        items={tabs}
        selectedKey={tab}
        onSelect={(key) => {
          setTab(key);
          setQuery("");
          setEditor(null);
          setBomFamily("");
          setFeedback("");
        }}
        label="인테리어 부스바 업무"
      />
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
      {feedback && (
        <DsActionFeedback
          message={feedback}
          tone={feedbackError ? "error" : "success"}
          focusOnAttention
        />
      )}
      {editor && canWrite && (
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
          <DsKpiGrid label="부스바 업무 요약">
            <DsKpiCard
              label="완제품 공용 재고"
              value={n(data.productFamilies.reduce((a, x) => a + stock(x), 0))}
            />
            <DsKpiCard
              label="납품 잔여"
              value={n(
                data.projects.reduce(
                  (a, x) => a + x.requestedQuantity - x.shippedQuantity,
                  0,
                ),
              )}
            />
            <DsKpiCard
              label="부족 자재"
              value={`${data.materials.filter((x) => stock(x) < 0).length}종`}
              tone="danger"
            />
            <DsKpiCard
              label="게시 대기·실패"
              value={data.publicationOutstandingCount ?? 0}
            />
          </DsKpiGrid>
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
            <DsToolbar label="납품 프로젝트 도구">
              <Search value={query} onChange={setQuery} />
              {writeButton("프로젝트 등록", () => projectEditor())}
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
              rows={data.projects
                .filter((p) =>
                  matches(
                    p.name,
                    p.customerJobNumber,
                    p.destination,
                    familyName(p.productFamilyId),
                  ),
                )
                .map((p) => [
                  <button type="button" onClick={() => setActiveProject(p.id)}>
                    {p.name}
                  </button>,
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
              <DsToolbar>
                <h3>{selectedProject.name}</h3>
                {writeButton("분할 출하", () => {
                  const requestId = crypto.randomUUID();
                  open({
                    title: "분할 출하",
                    path: "/shipments",
                    fields: [
                      { ...quantityField("이번 출하 수량"), min: 1, step: "1" },
                    ],
                    note: `납품 잔여 ${n(selectedProject.requestedQuantity - selectedProject.shippedQuantity)}개 · 공용 현재고 ${n(stock(data.productFamilies.find((x) => x.id === selectedProject.productFamilyId)!))}개`,
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
              <Table
                headings={["예정일", "생산계획 수량"]}
                rows={data.plans
                  .filter(
                    (p) =>
                      p.productFamilyId === selectedProject.productFamilyId,
                  )
                  .map((p) => [p.planDate.slice(0, 10), n(p.quantity)])}
              />
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
        <DsSurface>
          <DsToolbar>
            <Search value={query} onChange={setQuery} />
            {writeButton("생산계획 등록", () => planEditor())}
          </DsToolbar>
          <Table
            headings={["생산일", "제품군", "목표 수량", "실적", "차이", "작업"]}
            rows={data.plans
              .filter((p) => matches(p.planDate, familyName(p.productFamilyId)))
              .map((p) => {
                const actual = p.actualQuantity ?? 0;
                return [
                  p.planDate.slice(0, 10),
                  familyName(p.productFamilyId),
                  n(p.quantity),
                  n(actual),
                  n(actual - p.quantity),
                  writeButton("수정", () => planEditor(p.id)),
                ];
              })}
          />
          <p className="busbar-note">
            계획이 없어도 생산할 수 있습니다. 목표를 초과한 실적도 그대로
            기록합니다.
          </p>
        </DsSurface>
      )}
      {tab === "production" && (
        <>
          <DsSurface>
            <DsToolbar>
              <Search value={query} onChange={setQuery} />
              {writeButton("새 제품 사진 등록", () => {
                const requestId = crypto.randomUUID();
                open({
                  title: "새 제품 사진 등록",
                  path: "/products",
                  fields: [
                    familyField(),
                    {
                      key: "workerId",
                      label: "실제 제조 작업자",
                      type: "select",
                      options: options(data.workers),
                    },
                  ],
                  note: "작업자는 외주 작업자 명단에서 선택합니다. 사진 등록자는 로그인한 담당자 계정으로 별도 기록합니다.",
                  makeBody: (v) => ({ ...v, requestId }),
                  after: (r) => {
                    setActiveProduct(r.id);
                    setPage(1);
                  },
                });
              })}
            </DsToolbar>
            <Table
              headings={[
                "제품번호",
                "제품군",
                "실제 작업자",
                "제조일시 (한국 시간)",
                "생산 상태",
                "외부 게시",
                "작업",
              ]}
              rows={data.products
                .filter((p) =>
                  matches(
                    p.number,
                    familyName(p.productFamilyId),
                    p.workerName,
                  ),
                )
                .map((p) => [
                  p.number ?? "번호 발급 전",
                  familyName(p.productFamilyId),
                  p.workerName,
                  busbarDateTime(p.manufacturedAtUtc),
                  statusLabel(p.status),
                  <DsBadge
                    tone={
                      p.publicationState === "Failed"
                        ? "danger"
                        : p.publicationState === "Published"
                          ? "success"
                          : "neutral"
                    }
                  >
                    {statusLabel(p.publicationState)}
                  </DsBadge>,
                  <button
                    type="button"
                    onClick={() => {
                      setActiveProduct(p.id);
                      setFeedback("");
                    }}
                  >
                    사진·QR 보기
                  </button>,
                ])}
            />
          </DsSurface>
          {selectedProduct && (
            <DsSurface label="제품 사진과 QR">
              <h3>
                {selectedProduct.number ?? "새 제품"} ·{" "}
                {familyName(selectedProduct.productFamilyId)}
              </h3>
              <p>
                제조 작업자: {selectedProduct.workerName} ·{" "}
                {busbarDateTime(selectedProduct.manufacturedAtUtc)}
              </p>
              <p className="busbar-note">
                사진 등록 담당자:{" "}
                {selectedProduct.registeredByDisplayName ??
                  "사진 두 장 등록 후 표시"}
              </p>
              <p className="busbar-note">
                앞·뒤 사진 두 장의 등록이 끝나면 자동으로 생산 완료합니다.
                제조일시는 두 번째 사진이 PMS에 등록된 시각이며 사진 파일의
                촬영시간과 다를 수 있습니다.
              </p>
              <PhotoWorkspace
                key={selectedProduct.id}
                user={user}
                product={selectedProduct}
                canWrite={canWrite}
                busy={busy}
                run={run}
              />
              <DsToolbar>
                {canWrite &&
                  selectedProduct.manufacturedAtUtc &&
                  selectedProduct.status !== "Draft" && (
                    <button
                      disabled={busy}
                      onClick={() =>
                        void run(
                          () =>
                            busbarApi.write(
                              user,
                              `/products/${selectedProduct.id}/publication/retry`,
                              {},
                            ),
                          "외부 페이지 게시를 다시 요청했습니다.",
                        )
                      }
                    >
                      게시 다시 요청
                    </button>
                  )}
                {canWrite && selectedProduct.status !== "Cancelled" && (
                  <>
                    {writeButton("작업자 정정", () =>
                      open({
                        title: "작업자 정정",
                        path: `/products/${selectedProduct.id}`,
                        method: "PATCH",
                        fields: [
                          {
                            key: "workerId",
                            label: "실제 제조 작업자",
                            type: "select",
                            options: options(
                              data.workers,
                              selectedProduct.workerId,
                            ),
                            value: selectedProduct.workerId,
                          },
                          reasonField,
                        ],
                        makeBody: (v) => v,
                      }),
                    )}
                    {writeButton("생산 취소", () => {
                      const requestId = crypto.randomUUID();
                      open({
                        title: "생산 취소",
                        path: `/products/${selectedProduct.id}/cancel`,
                        fields: [reasonField],
                        note:
                          selectedProduct.status === "Draft"
                            ? "미완료 등록을 취소합니다. 재고는 변경되지 않습니다."
                            : "당시 소요량 기준으로 자재를 되돌리고 완제품 1개를 차감합니다.",
                        makeBody: (v) => ({ ...v, requestId }),
                      });
                    })}
                  </>
                )}
                <button
                  disabled={
                    busy ||
                    selectedProduct.publicationState !== "Published" ||
                    selectedProduct.status !== "Complete"
                  }
                  onClick={() =>
                    void run(async () => {
                      const blob = await busbarApi.qr(user, selectedProduct.id);
                      setQr({
                        url: URL.createObjectURL(blob),
                        number: selectedProduct.number ?? "",
                        productId: selectedProduct.id,
                        revision: selectedProduct.revision,
                      });
                    }, "QR 인쇄 미리보기를 준비했습니다.")
                  }
                >
                  QR 인쇄 준비
                </button>
              </DsToolbar>
              {selectedProduct.publicationState !== "Published" && (
                <p className="busbar-note">
                  최신 사진과 작업자 정보의 외부 게시가 완료되면 QR을 출력할 수
                  있습니다.
                </p>
              )}
            </DsSurface>
          )}
          {qr &&
            data.products.some(
              (p) =>
                p.id === qr.productId &&
                p.revision === qr.revision &&
                p.publicationState === "Published" &&
                p.status === "Complete",
            ) && (
              <DsSurface label="QR 인쇄 미리보기">
                <img
                  src={qr.url}
                  alt={`${qr.number} QR`}
                  width="180"
                  height="180"
                />
                <p>{qr.number}</p>
                <button onClick={() => window.print()}>인쇄</button>
                <button onClick={() => setQr(null)}>닫기</button>
                <div className="busbar-qr-print">
                  <img src={qr.url} alt="제품 조회 QR" />
                  <p>{qr.number}</p>
                </div>
              </DsSurface>
            )}
        </>
      )}
      {tab === "purchases" && (
        <>
          <DsActionFeedback
            tone="info"
            message="이카운트 자동 연동은 아직 준비되지 않았습니다. 현재는 발주를 직접 등록하거나 엑셀로 가져와 주세요."
          />
          <DsSurface>
            <DsToolbar>
              <Search value={query} onChange={setQuery} />
              {writeButton("발주 등록", () => purchaseEditor())}
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
              <h3>공통 프로젝트 코드</h3>
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
                  ],
                  makeBody: (v) => v,
                }),
              )}
            </DsToolbar>
            <p>{data.settings.commonProjectCode || "설정 전"}</p>
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
                  ...(section.key === "materials" ? ["단위", "공급 구분"] : []),
                  "상태",
                  "작업",
                ]}
                rows={data[section.key].map((x) => [
                  x.code,
                  x.name,
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
}: {
  headings: string[];
  rows: ReactNode[][];
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
            <tr key={i}>
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
  const first = useRef<HTMLInputElement | null>(null);
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
                    required={!f.optional}
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
                    ref={i === 0 ? first : undefined}
                    required={!f.optional}
                    type={f.type ?? "text"}
                    value={values[f.key]}
                    min={f.min}
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
  canWrite,
  busy,
  run,
}: {
  user: string;
  product: BusbarProduct;
  canWrite: boolean;
  busy: boolean;
  run: Run;
}) {
  const [reason, setReason] = useState("");
  return (
    <>
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
                    disabled={
                      busy || (product.status === "Complete" && !reason.trim())
                    }
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
                    disabled={
                      busy || (product.status === "Complete" && !reason.trim())
                    }
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
    customerJobNumber: "고객 업무번호",
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
