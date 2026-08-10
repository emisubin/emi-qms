export type ProductionControlSource = {
  code: string;
  departmentLabel: string;
  label: string;
  requiresManufacturingDefinition: boolean;
  definitionKind: 'None' | 'Manufacturing' | 'Iqc' | 'Oqc';
  definitions: Array<{ definitionKey: string; label: string }>;
  isOperational?: boolean;
  operationalMessage?: string | null;
};

export type ProductionControlConnection = {
  sourceCode: string;
  sourceDefinitionKey: string | null;
};

export type ProductionControlManufacturingItem = {
  definitionKey: string | null;
  displayOrder: number;
  label: string;
};

export type ProductionControlPlanItem = {
  definitionKey: string | null;
  displayOrder: number;
  label: string;
  isRequired: boolean;
  connections: ProductionControlConnection[];
};

export type ProductionControlManufacturingVersion = {
  versionId: string;
  versionNumber: number;
  lifecycleStatus: 'Draft' | 'Active' | 'Archived';
  rowVersion: number;
  activatedAtUtc: string | null;
  archivedAtUtc: string | null;
  items: ProductionControlManufacturingItem[];
};

export type ProductionControlPlanVersion = {
  versionId: string;
  versionNumber: number;
  lifecycleStatus: 'Draft' | 'Active' | 'Archived';
  rowVersion: number;
  activatedAtUtc: string | null;
  archivedAtUtc: string | null;
  items: ProductionControlPlanItem[];
};

export type ProductionControlItemTemplate = {
  productTypeId: string;
  productTypeCode: string;
  productTypeName: string;
  lqcOperational: boolean;
  manufacturingVersions: ProductionControlManufacturingVersion[];
  planVersions: ProductionControlPlanVersion[];
};

export type ProductionControlTemplateCatalog = {
  canManageManufacturing: boolean;
  canManageProductionPlanning: boolean;
  sources: ProductionControlSource[];
  items: ProductionControlItemTemplate[];
};
