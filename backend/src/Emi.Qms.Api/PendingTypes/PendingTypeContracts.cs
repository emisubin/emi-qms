namespace Emi.Qms.Api.PendingTypes;

public sealed record PendingTypeCatalogResponse(IReadOnlyList<PendingTypeCatalogItemResponse> Items);

public sealed record PendingTypeCatalogItemResponse(
    string Code,
    string DisplayName,
    string? Description,
    int SortOrder,
    bool IsSystem,
    bool IsManualEnabled,
    bool IsActive,
    int RowVersion,
    int UsageCount);

public sealed record PendingTypeOptionResponse(
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsSystem,
    bool IsManualEnabled,
    bool IsActive);

public sealed record CreatePendingTypeRequest(string? DisplayName, string? Description);

public sealed record UpdatePendingTypeRequest(
    int? ExpectedRowVersion,
    string? DisplayName,
    string? Description,
    bool? IsManualEnabled);

public sealed record SetPendingTypeActiveRequest(int? ExpectedRowVersion);

public sealed record ReorderPendingTypeItemRequest(
    string? Code,
    int? ExpectedRowVersion,
    int? NewSortOrder);

public sealed record ReorderPendingTypesRequest(IReadOnlyList<ReorderPendingTypeItemRequest>? Items);

public sealed record PendingTypeMutationResult<T>(
    PendingTypeMutationStatus Status,
    T? Value = default,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PendingTypeMutationResult<T> Success(T value) => new(PendingTypeMutationStatus.Success, value);
    public static PendingTypeMutationResult<T> NotFound() => new(PendingTypeMutationStatus.NotFound);
    public static PendingTypeMutationResult<T> Conflict(string message) => new(PendingTypeMutationStatus.Conflict, Message: message);
    public static PendingTypeMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors)
        => new(PendingTypeMutationStatus.Validation, Errors: errors);
}

public enum PendingTypeMutationStatus
{
    Success,
    NotFound,
    Conflict,
    Validation
}
