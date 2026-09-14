export const busbarSections = [
  { key: "overview", label: "홈" },
  { key: "projects", label: "프로젝트" },
  { key: "plans", label: "생산계획" },
  { key: "purchases", label: "발주, 입고관리" },
  { key: "production", label: "생산" },
  { key: "masters", label: "기준정보" },
] as const;
export type BusbarSection = typeof busbarSections[number]["key"];
