export type SalesKpiMonth = {
  month: number;
  revenueAmount: number;
  targetAmount: number | null;
  settlementCount: number;
};
export type SalesKpiResponse = {
  year: number;
  currency: string;
  defaultCurrency: string;
  availableYears: number[];
  availableCurrencies: string[];
  months: SalesKpiMonth[];
  kpi: {
    currentMonthRevenue: number;
    revenueTotal: number;
    targetTotal: number | null;
    registeredTargetMonthCount: number;
    achievementRate: number | null;
    remainingTargetAmount: number | null;
    exceededTargetAmount: number | null;
  };
  pipeline: { amount: number; projectCount: number };
  missingAmountCount: number;
};

export type SalesKpiMonthDetail = {
  year: number;
  month: number;
  currency: string;
  projects: Array<{
    projectId: string;
    projectCode: string;
    projectName: string;
    invoiceIssuedDate: string;
    amount: number;
  }>;
};

export type SalesTargetsResponse = {
  year: number;
  currency: string;
  months: Array<{ month: number; amount: number | null; version: number | null }>;
};

export type SaveSalesTargetsRequest = {
  year: number;
  currency: string;
  months: Array<{ month: number; amount: number; expectedVersion: number | null }>;
};
