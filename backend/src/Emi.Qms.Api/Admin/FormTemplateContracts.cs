namespace Emi.Qms.Api.Admin;

public sealed record FormTemplateScopeResponse(
    bool CanManage,
    bool IsSystemAdministrator,
    IReadOnlyList<string> Domains);

public sealed record FormTemplateCatalogResponse(IReadOnlyList<FormTemplateCatalogItemResponse> Templates);

public sealed record FormTemplateCatalogItemResponse(
    string Family,
    string TemplateKey,
    string DisplayName,
    string Domain,
    int? ActiveVersionNumber,
    DateTimeOffset? ActivatedAtUtc,
    int DraftCount);

public sealed record FormTemplateVersionsResponse(
    string Family,
    string TemplateKey,
    string DisplayName,
    string Domain,
    IReadOnlyList<FormTemplateVersionResponse> Versions);

public sealed record FormTemplateVersionResponse(
    Guid VersionId,
    int VersionNumber,
    string DisplayName,
    string LifecycleStatus,
    int RowVersion,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<FormTemplateItemResponse> Items);

public sealed record FormTemplateItemResponse(
    Guid ItemId,
    string ItemCode,
    int DisplayOrder,
    string Label,
    string? Guidance,
    string ResponseType,
    bool IsRequired,
    bool RequiresPhoto,
    int? MaxTextLength,
    Guid DefinitionKey);

public sealed record CreateFormTemplateDraftRequest(int ExpectedActiveRowVersion);

public sealed record SaveFormTemplateItemsRequest(
    int ExpectedRowVersion,
    IReadOnlyList<SaveFormTemplateItemRequest> Items);

public sealed record SaveFormTemplateItemRequest(
    string ItemCode,
    int DisplayOrder,
    string Label,
    string? Guidance,
    string ResponseType,
    bool IsRequired,
    bool RequiresPhoto,
    int? MaxTextLength,
    Guid? DefinitionKey = null);

public sealed record TransitionFormTemplateVersionRequest(int ExpectedRowVersion);

public sealed record FormTemplateManagersResponse(
    IReadOnlyList<FormTemplateManagerBindingResponse> Bindings,
    IReadOnlyList<FormTemplateManagerCandidateResponse> Candidates);

public sealed record FormTemplateManagerBindingResponse(
    Guid BindingId,
    Guid UserId,
    string DisplayName,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    string Domain,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record FormTemplateManagerCandidateResponse(
    Guid UserId,
    string DisplayName,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName);

public sealed record AssignFormTemplateManagerRequest(Guid UserId, string Domain);

public sealed record ExportFormTemplateVersionsRequest(
    string Family,
    string TemplateKey,
    IReadOnlyList<Guid> VersionIds);

public sealed record LqcItemTemplatesResponse(
    bool CanManageItems,
    bool CanChangeOperatingStatus,
    IReadOnlyList<LqcItemTemplateResponse> Items);

public sealed record LqcItemTemplateResponse(
    Guid ProductTypeId,
    string ProductTypeCode,
    string ProductTypeName,
    bool IsOperational,
    int SettingRowVersion,
    Guid TemplateVersionId,
    int TemplateVersionNumber,
    int TemplateRowVersion,
    IReadOnlyList<FormTemplateItemResponse> Items);

public sealed record UpdateLqcItemOperatingStatusRequest(
    bool IsOperational,
    int ExpectedRowVersion);

public sealed record SaveLqcItemTemplateRequest(
    int ExpectedTemplateRowVersion,
    IReadOnlyList<SaveFormTemplateItemRequest> Items);

public sealed record MaterialCategoryIqcTemplatesResponse(
    bool CanManage,
    IReadOnlyList<MaterialCategoryIqcTemplateResponse> Items);

public sealed record MaterialCategoryIqcTemplateResponse(
    Guid MaterialCategoryId,
    string MaterialCategoryCode,
    string MaterialCategoryName,
    bool IsCategoryActive,
    bool IsEnabled,
    string DecisionMode,
    int SettingRowVersion,
    Guid TemplateVersionId,
    int TemplateVersionNumber,
    int TemplateRowVersion,
    IReadOnlyList<FormTemplateItemResponse> Items);

public sealed record UpdateMaterialCategoryIqcSettingRequest(
    bool IsEnabled,
    string? DecisionMode,
    int ExpectedRowVersion);

public sealed record SaveMaterialCategoryIqcTemplateRequest(
    int ExpectedTemplateRowVersion,
    IReadOnlyList<SaveFormTemplateItemRequest> Items);
