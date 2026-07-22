namespace Emi.Qms.Api.Ul891Sets;

public sealed record CreateUl891SetSpecRequest(
    string? Name,
    int? Quantity,
    IReadOnlyList<CreateUl891ComponentRequest>? Components);

public sealed record CreateUl891ComponentRequest(string? ComponentCode);

public sealed record AddUl891SetSpecRequest(
    Guid OperationId,
    int? ExpectedSpecCount,
    string? Name,
    int? Quantity,
    IReadOnlyList<CreateUl891ComponentRequest>? Components,
    string? Reason);

public sealed record Ul891SetStructureResponse(
    Guid ProjectId,
    string StructureMode,
    bool IsLegacyFlat,
    bool CanEditOrder,
    bool CanEditDesign,
    IReadOnlyList<Ul891SetSpecResponse> Specs,
    IReadOnlyList<Ul891OrderedProcurementItemResponse> OrderedProcurementItems,
    IReadOnlyList<Ul891RecoveryCaseResponse> RecoveryCases);

public sealed record Ul891SetSpecResponse(
    Guid SpecId,
    int SpecNo,
    string Name,
    int RowVersion,
    int ActiveInstanceCount,
    IReadOnlyList<Ul891SetVersionResponse> Versions,
    IReadOnlyList<Ul891SetInstanceResponse> Instances);

public sealed record Ul891SetVersionResponse(
    Guid VersionId,
    int VersionNumber,
    string Status,
    string? RevisionReason,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<Ul891SetComponentResponse> Components);

public sealed record Ul891SetComponentResponse(
    Guid ComponentId,
    string ComponentCode,
    string? PanelName,
    string? PanelSpecification,
    decimal? WidthMm,
    decimal? HeightMm,
    decimal? DepthMm,
    int SortOrder);

public sealed record Ul891SetInstanceResponse(
    Guid InstanceId,
    int InstanceNumber,
    Guid SpecVersionId,
    int SpecVersionNumber,
    string Status,
    int RowVersion,
    bool HasStarted,
    bool HasDeliveredPanel,
    IReadOnlyList<Ul891SetPanelResponse> Panels);

public sealed record Ul891SetPanelResponse(
    Guid PanelId,
    int SequenceNumber,
    string DisplayCode,
    string ComponentCode,
    string? PanelName,
    string? PanelSpecification,
    string PanelStatus,
    string WorkflowStage,
    string? PackingUnitLabel,
    DateOnly? DepartureDate,
    bool Delivered);

public sealed record Ul891OrderedProcurementItemResponse(
    Guid ProcurementItemId,
    int SequenceNumber,
    string OrderItem,
    DateOnly OrderDate);

public sealed record Ul891RecoveryCaseResponse(
    Guid RecoveryCaseId,
    Guid SetInstanceId,
    int SetInstanceNumber,
    Guid ProcurementItemId,
    string ProcurementItemName,
    DateOnly OrderDate,
    string Status,
    string? Note,
    int RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RecoveredAtUtc);

public sealed record UpdateUl891DraftRequest(
    int? ExpectedSpecVersion,
    string? SpecName,
    string? RevisionReason,
    IReadOnlyList<UpdateUl891ComponentRequest>? Components);

public sealed record UpdateUl891ComponentRequest(
    string? ComponentCode,
    string? PanelName,
    string? PanelSpecification,
    decimal? WidthMm,
    decimal? HeightMm,
    decimal? DepthMm);

public sealed record PublishUl891VersionRequest(Guid OperationId, string? Reason);

public sealed record CreateUl891VersionRequest(Guid OperationId, string? Reason);

public sealed record ApplyUl891VersionRequest(
    Guid OperationId,
    int? ExpectedActiveInstanceCount,
    Guid? VersionId,
    IReadOnlyList<Guid>? InstanceIds,
    string? Reason);

public sealed record IncreaseUl891InstancesRequest(
    Guid OperationId,
    int? ExpectedActiveInstanceCount,
    int? Quantity,
    string? Reason);

public sealed record CancelUl891InstancesRequest(
    Guid OperationId,
    IReadOnlyList<Guid>? InstanceIds,
    IReadOnlyList<Guid>? ProcurementItemIds,
    string? Reason,
    bool? ExceptionAcknowledged);

public sealed record RecoverUl891CaseRequest(
    Guid OperationId,
    int? ExpectedVersion,
    string? Note);

public sealed record Ul891MutationResponse(
    Guid OperationId,
    Guid ProjectId,
    string Action,
    bool Replayed);

public sealed record OpenMonthlyBillingLedgerRequest(
    Guid OperationId,
    DateOnly? BillingMonth,
    IReadOnlyList<Guid>? RecoveryCaseIds);

public sealed record CreateMonthlyBillingRevisionRequest(
    Guid OperationId,
    int? ExpectedLedgerVersion,
    decimal? Amount,
    string? Note,
    IReadOnlyList<Guid>? RecoveryCaseIds,
    string? AdjustmentReason);

public sealed record ConfirmMonthlyBillingRequest(
    Guid OperationId,
    int? ExpectedLedgerVersion,
    DateOnly? InvoiceConfirmedDate,
    string? InvoiceNumber,
    string? Note);

public sealed record MonthlyBillingResponse(
    Guid ProjectId,
    string StructureMode,
    decimal? SalesAmount,
    string? CurrencyCode,
    decimal ConfirmedAmount,
    decimal CurrentRequestedAmount,
    decimal? RemainingAmount,
    bool CanReadAmounts,
    bool CanMutate,
    IReadOnlyList<MonthlyBillingLedgerResponse> Ledgers,
    IReadOnlyList<MonthlyBillingEvidenceMonthResponse> UnbilledMonths);

public sealed record MonthlyBillingLedgerResponse(
    Guid LedgerId,
    DateOnly BillingMonth,
    string Kind,
    string Status,
    int RowVersion,
    IReadOnlyList<MonthlyBillingRevisionResponse> Revisions,
    IReadOnlyList<MonthlyBillingPanelEvidenceResponse> CurrentShipmentEvidence,
    IReadOnlyList<Ul891RecoveryCaseResponse> AvailableRecoveryCases);

public sealed record MonthlyBillingRevisionResponse(
    Guid RevisionId,
    int RevisionNumber,
    decimal? Amount,
    string? Note,
    bool IsAdjustment,
    string? AdjustmentReason,
    DateTimeOffset CreatedAtUtc,
    DateOnly? InvoiceConfirmedDate,
    string? InvoiceNumber,
    IReadOnlyList<MonthlyBillingPanelEvidenceResponse> Panels,
    IReadOnlyList<Guid> RecoveryCaseIds);

public sealed record MonthlyBillingPanelEvidenceResponse(
    Guid PanelId,
    string DisplayCode,
    string? SetLabel,
    string? PackingUnitLabel,
    DateOnly DepartureDate);

public sealed record MonthlyBillingEvidenceMonthResponse(
    DateOnly BillingMonth,
    int PanelCount,
    bool HasLedger,
    string StatusLabel);
