import type { BusbarWorkspace } from "./interiorBusbar";

export type Values = Record<string, string>;
export type Field = {
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
export type EditorSpec = {
  title: string;
  fields: Field[];
  path: string;
  method?: string;
  makeBody: (values: Values) => unknown;
  after?: (result: { id: string }) => void;
  note?: string;
};

export function projectEditorSpec(data: BusbarWorkspace, id?: string): EditorSpec {
  const row = data.projects.find(x => x.id === id);
  return {
      title: row ? "프로젝트 수정" : "납품 프로젝트 등록",
      path: "/projects",
      note: `공통 프로젝트 코드: ${data?.settings.commonProjectCode || "기준정보에서 먼저 설정하세요."} · 원화, 부가세 별도 10%. 단가는 제품군 기준정보에서 관리합니다.`,
      fields: [
        { key: "name", label: "프로젝트명", value: row?.name },
        {
          key: "customerJobNumber",
          label: "LSE Task No",
          value: row?.customerJobNumber,
          optional: true,
        },
        { key: "productFamilyId", label: "제품군", type: "select", value: row?.productFamilyId,
          options: data.productFamilies.filter(f => f.isActive || f.id === row?.productFamilyId).map(f => ({ value: f.id, label: `${f.name}${f.code ? ` (${f.code})` : ""}${f.isActive ? "" : " · 비활성"}` })) },
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
          value: row?.dueDate?.slice(0, 10) ?? new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Seoul" }).format(new Date()),
        },
        ...(row ? [{ key: "reason", label: "정정 사유" }] : []),
      ],
      makeBody: (v) => ({
        ...v,
        id: row?.id ?? null,
        requestedQuantity: Number(v.requestedQuantity),
      }),
    };
}
