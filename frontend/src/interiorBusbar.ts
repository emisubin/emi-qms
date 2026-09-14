import { fetchBlob, fetchJson } from "./api";

export type BusbarMaster = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  unit?: string;
  supplyType?: string;
  balance?: number;
  producedQuantity?: number;
  plannedQuantity?: number;
  ecountProductCode?: string | null;
  standardUnitPrice?: number | null;
};
export type BusbarProject = {
  id: string;
  name: string;
  customerJobNumber: string;
  commonProjectCode: string;
  productFamilyId: string;
  requestedQuantity: number;
  shippedQuantity: number;
  destination: string;
  dueDate: string;
  unitPrice?: number | null;
};
export type BusbarPlan = {
  id: string;
  productFamilyId: string;
  planDate: string;
  quantity: number;
  actualQuantity?: number;
  productsInitialized?: boolean;
};
export type BusbarPurchase = {
  id: string;
  orderNumber: string;
  materialId: string;
  quantity: number;
  receivedQuantity: number;
  orderDate: string;
  source?: string;
  status?: string;
};
export type BusbarProduct = {
  id: string;
  productFamilyId: string;
  planId?: string | null;
  planSequence?: number | null;
  workerId: string | null;
  workerName: string | null;
  registeredByDisplayName?: string;
  number?: string;
  manufacturedAtUtc?: string;
  status: "Draft" | "Complete" | "Cancelled";
  hasFront: boolean;
  hasBack: boolean;
  publicationState?: "Pending" | "Published" | "Failed";
  revision: number;
  publishedRevision?: number;
  qrState?: "Ready" | "ConfigurationPending" | "AwaitingCompletion";
};
export type BusbarLedger = {
  id: string;
  reason: string;
  kind: string;
  createdAtUtc: string;
  reversed?: boolean;
  reversalOfId?: string;
  referenceId?: string;
};
export type BusbarWorkspace = {
  pagination?: {
    page: number;
    pageSize: number;
    productCount: number;
    ledgerCount: number;
  };
  publicationOutstandingCount?: number;
  canWrite: boolean;
  settings: { commonProjectCode: string; ecountCustomerCode?: string; ecountWarehouseCode?: string };
  productFamilies: BusbarMaster[];
  materials: BusbarMaster[];
  workers: BusbarMaster[];
  boms: Array<{
    id: string;
    productFamilyId: string;
    version: number;
    createdAtUtc: string;
  }>;
  bomLines: Array<{ bomId: string; materialId: string; quantity: number }>;
  ledgerLines: Array<{
    operationId: string;
    stockKind: string;
    itemId: string;
    quantity: number;
  }>;
  projects: BusbarProject[];
  plans: BusbarPlan[];
  purchases: BusbarPurchase[];
  products: BusbarProduct[];
  ledger: BusbarLedger[];
  shipments: Array<{
    id: string;
    projectId: string;
    quantity: number;
    createdAtUtc: string;
    reversed?: boolean;
  }>;
  receipts: Array<{
    id: string;
    purchaseId: string;
    quantity: number;
    createdAtUtc: string;
    reversed?: boolean;
  }>;
};
export type BusbarImport = {
  rows: Array<Record<string, unknown>>;
  errors: unknown[];
};
export type BusbarCommercialPreview = {
  unitPrice: number | null; quantity: number; supplyAmount: number | null;
  vatAmount: number | null; totalAmount: number | null; missingFields: string[];
  customerCode: string; warehouseCode: string; productCode: string | null;
  transmissionEnabled: boolean;
};
export type BusbarProductFilters = { productFamilyId?: string; planDateFrom?: string; planDateTo?: string; status?: string };
const root = "/api/interior-busbar";
export const busbarApi = {
  commercialPreview: (user: string, id: string) => fetchJson<BusbarCommercialPreview>(`${root}/projects/${id}/commercial-preview`, user),
  workspace: (user: string, page = 1, filters: BusbarProductFilters = {}) => {
    const query = new URLSearchParams({ page: String(page), pageSize: "100" });
    Object.entries(filters).forEach(([key, value]) => { if (value) query.set(key, value); });
    return fetchJson<BusbarWorkspace>(`${root}/workspace?${query}`, user);
  },
  product: (user: string, id: string) => fetchJson<BusbarProduct>(`${root}/products/${id}`, user),
  write: <T = { id: string }>(
    user: string,
    path: string,
    body: unknown,
    method = "POST",
  ) =>
    fetchJson<T>(`${root}${path}`, user, {
      method,
      body: JSON.stringify(body),
    }),
  upload: <T = { id: string }>(
    user: string,
    path: string,
    file: File,
    reason = "",
    method = "POST",
    workerId?: string,
  ) => {
    const body = new FormData();
    body.append("file", file);
    if (reason) body.append("reason", reason);
    if (workerId) body.append("workerId", workerId);
    return fetchJson<T>(`${root}${path}`, user, { method, body });
  },
  template: (user: string, kind: string) =>
    fetchBlob(`${root}/${kind}/import/template`, user),
  photo: (user: string, id: string, side: string) =>
    fetchBlob(`${root}/products/${id}/photos/${side}`, user),
  qr: (user: string, id: string) =>
    fetchBlob(`${root}/products/${id}/qr`, user),
};
export const busbarDateTime = (value?: string) =>
  value
    ? new Intl.DateTimeFormat("ko-KR", {
        timeZone: "Asia/Seoul",
        dateStyle: "medium",
        timeStyle: "medium",
      }).format(new Date(value))
    : "미완료";
export const busbarNumber = (value: number = 0) =>
  new Intl.NumberFormat("ko-KR", { maximumFractionDigits: 6 }).format(value);
export function busbarStock(
  data: BusbarWorkspace,
  kind: string,
  itemId: string,
) {
  return data.ledgerLines
    .filter((x) => x.stockKind === kind && x.itemId === itemId)
    .reduce((sum, x) => sum + x.quantity, 0);
}
