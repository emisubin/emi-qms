export interface PendingTypeCatalogItem {
  code: string;
  displayName: string;
  description: string | null;
  sortOrder: number;
  isSystem: boolean;
  isManualEnabled: boolean;
  isActive: boolean;
  rowVersion: number;
  usageCount: number;
}

export interface PendingTypeCatalog {
  items: PendingTypeCatalogItem[];
}

export interface PendingTypeOption {
  code: string;
  displayName: string;
  sortOrder: number;
  isSystem: boolean;
  isManualEnabled: boolean;
  isActive: boolean;
}

export interface ReorderPendingTypeItem {
  code: string;
  expectedRowVersion: number;
  newSortOrder: number;
}
