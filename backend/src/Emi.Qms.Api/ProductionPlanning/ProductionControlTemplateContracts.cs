namespace Emi.Qms.Api.ProductionPlanning;

public static class ProductionControlModelVersions
{
    public const string Legacy = "LEGACY";
    public const string LinkedV1 = "LINKED_V1";
}

public static class ProductionControlSourceCodes
{
    public const string PurchaseOrdered = "PURCHASE_ORDERED";
    public const string MaterialReceiptConfirmed = "MATERIAL_RECEIPT_CONFIRMED";
    public const string IqcPassed = "IQC_PASSED";
    public const string ManufacturingStepCompleted = "MANUFACTURING_STEP_COMPLETED";
    public const string LqcPassed = "LQC_PASSED";
    public const string OqcPassed = "OQC_PASSED";
    public const string CustomerInspectionPassed = "CUSTOMER_INSPECTION_PASSED";
    public const string FatPassed = "FAT_PASSED";
    public const string Packed = "PACKED";
    public const string Departed = "DEPARTED";
    public const string Delivered = "DELIVERED";

    public static readonly IReadOnlyList<ProductionControlSourceCatalogItemResponse> Catalog =
    [
        new(PurchaseOrdered, "구매", "발주 완료", false),
        new(MaterialReceiptConfirmed, "자재", "전체 입고 확정", false),
        new(IqcPassed, "품질", "IQC 합격", false),
        new(ManufacturingStepCompleted, "제조", "제조 단계 완료", true),
        new(LqcPassed, "품질", "LQC 합격", true),
        new(OqcPassed, "품질", "OQC 합격", false),
        new(CustomerInspectionPassed, "품질", "전진검수 합격", false),
        new(FatPassed, "품질", "FAT 합격", false),
        new(Packed, "물류", "포장 완료", false),
        new(Departed, "물류", "출발 처리", false),
        new(Delivered, "물류", "납품 완료", false)
    ];

    public static bool IsSupported(string value) => Catalog.Any(item => item.Code == value);
    public static bool RequiresManufacturingDefinition(string value)
        => value is ManufacturingStepCompleted or LqcPassed;
}

public sealed record ProductionControlSourceCatalogItemResponse(
    string Code,
    string DepartmentLabel,
    string Label,
    bool RequiresManufacturingDefinition);

public sealed record ProductionControlTemplateCatalogResponse(
    bool CanManageManufacturing,
    bool CanManageProductionPlanning,
    IReadOnlyList<ProductionControlSourceCatalogItemResponse> Sources,
    IReadOnlyList<ProductionControlItemTemplateResponse> Items);

public sealed record ProductionControlItemTemplateResponse(
    Guid ProductTypeId,
    string ProductTypeCode,
    string ProductTypeName,
    IReadOnlyList<ProductionControlManufacturingVersionResponse> ManufacturingVersions,
    IReadOnlyList<ProductionControlPlanVersionResponse> PlanVersions);

public sealed record ProductionControlManufacturingVersionResponse(
    Guid VersionId,
    int VersionNumber,
    string LifecycleStatus,
    int RowVersion,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<ProductionControlManufacturingItemResponse> Items);

public sealed record ProductionControlManufacturingItemResponse(
    Guid DefinitionKey,
    int DisplayOrder,
    string Label,
    string StepRole);

public sealed record ProductionControlPlanVersionResponse(
    Guid VersionId,
    int VersionNumber,
    string LifecycleStatus,
    int RowVersion,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<ProductionControlPlanTemplateItemResponse> Items);

public sealed record ProductionControlPlanTemplateItemResponse(
    Guid DefinitionKey,
    int DisplayOrder,
    string Label,
    bool IsRequired,
    IReadOnlyList<ProductionControlConnectionResponse> Connections);

public sealed record ProductionControlConnectionResponse(
    string SourceCode,
    Guid? SourceDefinitionKey);

public sealed record CreateProductionControlDraftRequest(int? ExpectedActiveRowVersion);

public sealed record SaveProductionControlManufacturingVersionRequest(
    int ExpectedRowVersion,
    IReadOnlyList<SaveProductionControlManufacturingItemRequest> Items);

public sealed record SaveProductionControlManufacturingItemRequest(
    Guid? DefinitionKey,
    int DisplayOrder,
    string Label,
    string StepRole);

public sealed record SaveProductionControlPlanVersionRequest(
    int ExpectedRowVersion,
    IReadOnlyList<SaveProductionControlPlanItemRequest> Items);

public sealed record SaveProductionControlPlanItemRequest(
    Guid? DefinitionKey,
    int DisplayOrder,
    string Label,
    bool IsRequired,
    IReadOnlyList<ProductionControlConnectionResponse> Connections);

public sealed record TransitionProductionControlVersionRequest(int ExpectedRowVersion);

public sealed class ProductionControlTemplateForbiddenException : Exception;
public sealed class ProductionControlTemplateConflictException(string message) : Exception(message);
