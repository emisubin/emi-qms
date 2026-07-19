export type FormTemplateScope = { canManage: boolean; isSystemAdministrator: boolean; domains: string[] };
export type FormTemplateCatalog = { templates: FormTemplateCatalogItem[] };
export type FormTemplateCatalogItem = {
  family: string; templateKey: string; displayName: string; domain: string;
  activeVersionNumber: number | null; activatedAtUtc: string | null; draftCount: number;
};
export type FormTemplateItem = {
  itemId: string; itemCode: string; displayOrder: number; label: string; guidance: string | null;
  responseType: 'Check' | 'Text'; isRequired: boolean; requiresPhoto: boolean; maxTextLength: number | null;
};
export type FormTemplateVersion = {
  versionId: string; versionNumber: number; displayName: string; lifecycleStatus: 'Draft' | 'Active' | 'Archived';
  rowVersion: number; activatedAtUtc: string | null; archivedAtUtc: string | null; items: FormTemplateItem[];
};
export type FormTemplateVersions = {
  family: string; templateKey: string; displayName: string; domain: string; versions: FormTemplateVersion[];
};
export type FormTemplateManagers = {
  bindings: Array<{
    bindingId: string; userId: string; displayName: string; departmentId: string; departmentCode: string;
    departmentName: string; domain: string; assignedAtUtc: string; revokedAtUtc: string | null;
  }>;
  candidates: Array<{
    userId: string; displayName: string; departmentId: string; departmentCode: string; departmentName: string;
  }>;
};
