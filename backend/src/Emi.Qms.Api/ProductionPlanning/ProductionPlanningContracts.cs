namespace Emi.Qms.Api.ProductionPlanning;

public sealed record ProductionPlanningSummaryResponse(
    int NotPlannedCount,
    int PlanningCount,
    int PlannedCount,
    int MissingAssigneeProjectCount);

public sealed record ProductionPlanningProjectListResponse(
    IReadOnlyList<ProductionPlanningProjectSummaryResponse> Projects);

public sealed class ProductionPlanningProjectSummaryResponse
{
    public Guid ProjectId { get; init; }
    public string ProjectTitle { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string ProjectCode { get; init; } = "";
    public string Item { get; init; } = "";
    public int ActivePanelCount { get; init; }
    public DateOnly? DeliveryDate { get; init; }
    public string ProjectStatus { get; init; } = "";
    public string PlanStatus { get; init; } = "NotPlanned";
    public string PlanStatusLabel { get; init; } = "미등록";
    public string? ProductTypeCode { get; init; }
    public string? ProductTypeName { get; init; }
    public int RequiredStepCount { get; init; }
    public int PlannedRequiredStepCount { get; init; }
    public int AssigneeCount { get; init; }
}

public sealed record ProductionPlanningResponse(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    DateOnly? DeliveryDate,
    string ModelVersion,
    Guid? PlanId,
    int RowVersion,
    string PlanStatus,
    string PlanStatusLabel,
    Guid? ProductTypeId,
    Guid? TemplateId,
    string? ProductTypeCode,
    string? ProductTypeName,
    string? Notes,
    IReadOnlyList<ProjectManufacturingStepResponse> ManufacturingSteps,
    IReadOnlyList<ProductionControlSourceCatalogItemResponse> AvailableSources,
    IReadOnlyList<ProductionPlanItemResponse> Items,
    IReadOnlyList<ProjectAssigneeResponse> Assignees,
    IReadOnlyList<AssigneeCandidateResponse> AssigneeCandidates,
    IReadOnlyList<NotificationFallbackResponse> Fallbacks,
    bool IsSetScoped,
    ProductionPlanSetScopeResponse? SelectedScope,
    IReadOnlyList<ProductionPlanSetScopeResponse> Scopes,
    ProductionPlanSetDefaultResponse? SetDefault);

public sealed record ProductionPlanSetDefaultResponse(
    Guid DefaultId,
    int RowVersion,
    IReadOnlyList<ProductionPlanItemResponse> Items);

public sealed record ProductionPlanSetScopeResponse(
    Guid ScopeId,
    Guid SetInstanceId,
    string Label,
    string SpecName,
    int SpecNumber,
    int InstanceNumber,
    string Status,
    int ActivePanelCount,
    int RequiredItemCount,
    int PlannedRequiredItemCount,
    int RowVersion);

public sealed record ProjectManufacturingStepResponse(
    Guid DefinitionKey,
    int SequenceNumber,
    string StepName);

public sealed class ProductionPlanItemResponse
{
    public Guid? ItemId { get; init; }
    public Guid? TemplateStepId { get; init; }
    public int SequenceNumber { get; init; }
    public string StepName { get; init; } = "";
    public bool IsRequired { get; init; }
    public bool IsCustom { get; init; }
    public Guid? DefinitionKey { get; init; }
    public DateOnly? PlannedDate { get; init; }
    public DateOnly? PlannedStartDate { get; init; }
    public DateOnly? PlannedEndDate { get; init; }
    public DateOnly? ActualStartDate { get; init; }
    public DateOnly? ActualEndDate { get; init; }
    public Guid? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public int? RequiredHeadcount { get; init; }
    public int CompletedTargetCount { get; init; }
    public int TotalTargetCount { get; init; }
    public int ProgressPercent { get; init; }
    public string ScheduleStatus { get; init; } = "NotConnected";
    public string ScheduleStatusLabel { get; init; } = "연결 안 됨";
    public int? DelayDays { get; init; }
    public bool IsBlocked { get; init; }
    public IReadOnlyList<ProductionControlConnectionResponse> Connections { get; init; } = [];
    public IReadOnlyList<ProductionPlanEvidenceResponse> Evidence { get; init; } = [];
    public string? Note { get; init; }
    public int RowVersion { get; init; }
}

public sealed record ProductionPlanEvidenceResponse(
    string SourceCode,
    string SourceLabel,
    string TargetType,
    string TargetId,
    string TargetLabel,
    DateOnly? StartedDate,
    DateOnly? CompletedDate,
    bool IsCompleted,
    bool IsBlocked,
    string StatusLabel,
    string EvidenceScope = "SetPanel");

public sealed class ProjectAssigneeResponse
{
    public Guid? AssigneeId { get; init; }
    public string ResponsibilityType { get; init; } = "";
    public string ResponsibilityLabel { get; init; } = "";
    public Guid? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public string? Note { get; init; }
    public int RowVersion { get; init; }
}

public sealed record AssigneeCandidateResponse(
    string ResponsibilityType,
    IReadOnlyList<UserOptionResponse> Users);

public sealed record UserOptionResponse(Guid UserId, string DisplayName);

public sealed record NotificationFallbackResponse(
    string ResponsibilityType,
    string ResponsibilityLabel,
    Guid? UserId,
    string? DisplayName,
    string SourceLabel);

public sealed record ProductionProductTypeResponse(
    Guid ProductTypeId,
    string Code,
    string Name,
    bool IsActive,
    Guid? ActiveTemplateId,
    int? ActiveTemplateVersion,
    IReadOnlyList<ProductionTemplateStepResponse> Steps);

public sealed record ProductionTemplateStepResponse(
    Guid TemplateStepId,
    int SequenceNumber,
    string StepName,
    bool IsRequired);

public sealed record ProductionTemplateSettingsResponse(
    Guid ProductTypeId,
    string Code,
    string Name,
    Guid ActiveTemplateId,
    int ActiveTemplateVersion,
    IReadOnlyList<ProductionTemplateSettingsStepResponse> Steps);

public sealed record ProductionTemplateSettingsStepResponse(
    Guid TemplateStepId,
    int SequenceNumber,
    string StepName,
    bool IsRequired,
    bool IsActive);

public sealed record SystemHolidayResponse(
    DateOnly HolidayDate,
    string Name,
    string CountryCode,
    string Source,
    string HolidayType);

public sealed record SyncKoreanHolidaysRequest(int? Year);

public sealed record SyncKoreanHolidaysResponse(
    int Year,
    string CountryCode,
    int UpsertedCount,
    bool IsConfigured,
    string Message);

public sealed record SystemHolidayUpsert(
    DateOnly HolidayDate,
    string Name,
    string CountryCode,
    string Source,
    string SourceKey,
    string HolidayType = "National");

public sealed record KoreanHolidayProviderResult(
    bool IsConfigured,
    IReadOnlyList<SystemHolidayUpsert> Holidays,
    string Message);

public sealed record UpdateProductionTemplateSettingsRequest(
    IReadOnlyList<UpdateProductionTemplateSettingsStepRequest>? Steps,
    string? Reason);

public sealed record UpdateProductionTemplateSettingsStepRequest(
    Guid? TemplateStepId,
    int? SequenceNumber,
    string? StepName,
    bool? IsRequired,
    bool? IsActive);

public sealed record UpsertProductionProductTypeRequest(
    string? Code,
    string? Name,
    IReadOnlyList<UpsertProductionTemplateStepRequest>? Steps);

public sealed record UpsertProductionTemplateStepRequest(
    int? SequenceNumber,
    string? StepName,
    bool? IsRequired);

public sealed record UpdateProductionPlanningRequest(
    Guid? ProductTypeId,
    int? ExpectedRowVersion,
    string? Notes,
    string? Reason,
    IReadOnlyList<ProductionPlanItemUpdateRequest>? Items,
    IReadOnlyList<ProjectAssigneeUpdateRequest>? Assignees);

public sealed record UpdateProductionPlanSetScopeRequest(
    int? ExpectedRowVersion,
    string? Reason,
    IReadOnlyList<ProductionPlanSetItemValueUpdateRequest>? Items);

public sealed record UpdateProductionPlanSetDefaultRequest(
    int? ExpectedRowVersion,
    bool? OverwriteExisting,
    string? Reason,
    IReadOnlyList<ProductionPlanSetItemValueUpdateRequest>? Items);

public sealed record ProductionPlanSetItemValueUpdateRequest(
    Guid? ItemId,
    int? ExpectedRowVersion,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Guid? AssignedUserId,
    int? RequiredHeadcount,
    string? Note);

public sealed record ProductionPlanItemUpdateRequest(
    Guid? ItemId,
    Guid? TemplateStepId,
    string? StepName,
    int? SequenceNumber,
    bool? IsRequired,
    int? ExpectedRowVersion,
    DateOnly? PlannedDate,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Guid? AssignedUserId,
    int? RequiredHeadcount,
    string? Note,
    bool? IsDeleted,
    Guid? DefinitionKey,
    IReadOnlyList<ProductionControlConnectionResponse>? Connections);

public sealed record ProjectAssigneeUpdateRequest(
    string? ResponsibilityType,
    Guid? AssigneeId,
    int? ExpectedRowVersion,
    Guid? AssignedUserId,
    string? Note);

public sealed record ProductionPlanningTemplateDownload(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed record ProductionPlanningExcelPreviewResponse(
    string FileSha256,
    int TotalRows,
    int SaveableCount,
    int BlockedCount,
    IReadOnlyList<ProductionPlanningExcelPreviewRowResponse> Rows);

public sealed class ProductionPlanningExcelPreviewRowResponse
{
    public int ExcelRowNumber { get; init; }
    public string ResultType { get; init; } = "";
    public Guid? ProjectId { get; init; }
    public string? ProjectTitle { get; init; }
    public string? ProjectCode { get; init; }
    public Guid? ProductTypeId { get; init; }
    public string? ProductTypeCode { get; init; }
    public Guid? TemplateStepId { get; init; }
    public string? StepName { get; init; }
    public bool IsCustomStep { get; init; }
    public bool? IsRequired { get; init; }
    public DateOnly? PlannedDate { get; init; }
    public string? Note { get; init; }
    public string? ProcurementAssigneeText { get; init; }
    public string? ProductionPlanningAssigneeText { get; init; }
    public string? ManufacturingAssigneeText { get; init; }
    public string? QualityAssigneeText { get; init; }
    public string? LogisticsAssigneeText { get; init; }
    public IReadOnlyList<string> ErrorMessages { get; init; } = [];
}

public sealed record ProductionPlanningExcelApplyResponse(
    int AppliedRowCount,
    int BlockedRowCount,
    IReadOnlyList<Guid> ProjectIds);

public sealed record ProductionPlanningHistoryResponse(
    IReadOnlyList<ProductionPlanningHistoryGroupResponse> Groups);

public sealed class ProductionPlanningHistoryGroupResponse
{
    public string GroupId { get; init; } = "";
    public string InputSource { get; init; } = "Direct";
    public Guid? ChangedByUserId { get; init; }
    public string? ChangedByName { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; }
    public string? Reason { get; init; }
    public int AffectedItemCount { get; init; }
    public int ChangeCount { get; init; }
    public IReadOnlyList<ProductionPlanningHistoryChangeResponse> Changes { get; init; } = [];
}

public sealed class ProductionPlanningHistoryChangeResponse
{
    public Guid EntityId { get; init; }
    public string EntityType { get; init; } = "";
    public string? FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public sealed record ProductionPlanningMutationResult<T>(
    ProductionPlanningMutationStatus Status,
    T? Value,
    IReadOnlyDictionary<string, string[]> Errors,
    string? Message)
{
    public static ProductionPlanningMutationResult<T> Success(T value) => new(ProductionPlanningMutationStatus.Success, value, new Dictionary<string, string[]>(), null);
    public static ProductionPlanningMutationResult<T> Validation(IReadOnlyDictionary<string, string[]> errors) => new(ProductionPlanningMutationStatus.Validation, default, errors, null);
    public static ProductionPlanningMutationResult<T> NotFound() => new(ProductionPlanningMutationStatus.NotFound, default, new Dictionary<string, string[]>(), null);
    public static ProductionPlanningMutationResult<T> Conflict(string message) => new(ProductionPlanningMutationStatus.Conflict, default, new Dictionary<string, string[]>(), message);
}

public sealed record ProductionPlanningReadResult(
    ProductionPlanningReadStatus Status,
    ProductionPlanningResponse? Value,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public static ProductionPlanningReadResult Success(ProductionPlanningResponse value) =>
        new(ProductionPlanningReadStatus.Success, value, new Dictionary<string, string[]>());

    public static ProductionPlanningReadResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ProductionPlanningReadStatus.Validation, null, errors);

    public static ProductionPlanningReadResult NotFound() =>
        new(ProductionPlanningReadStatus.NotFound, null, new Dictionary<string, string[]>());
}

public enum ProductionPlanningReadStatus
{
    Success,
    Validation,
    NotFound
}

public enum ProductionPlanningMutationStatus
{
    Success,
    Validation,
    NotFound,
    Conflict
}
