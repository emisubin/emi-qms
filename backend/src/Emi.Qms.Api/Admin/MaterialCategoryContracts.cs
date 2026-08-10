namespace Emi.Qms.Api.Admin;

public sealed record MaterialCategoryResponse(
    Guid CategoryId,
    string Code,
    string DisplayName,
    bool RequiresIqc,
    string IqcDecisionMode,
    bool IsActive,
    int DisplayOrder,
    int RowVersion);

public sealed record MaterialCategoryCatalogResponse(
    bool CanManage,
    IReadOnlyList<MaterialCategoryResponse> Items);

public sealed record CreateMaterialCategoryRequest(
    string? DisplayName,
    int? DisplayOrder);

public sealed record UpdateMaterialCategoryRequest(
    int? ExpectedRowVersion,
    string? DisplayName,
    bool? IsActive,
    int? DisplayOrder);
