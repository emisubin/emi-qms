export type HomeMetricsResponse = {
  departmentCode: string | null;
  departmentName: string | null;
  metrics: HomeDepartmentMetric[];
};

export type HomeDepartmentMetric = {
  id: string;
  label: string;
  count: number;
  tone: 'neutral' | 'warning' | 'danger' | 'success';
  destinationKey: string;
  actionLabel: string;
};
