namespace Emi.Qms.Api.InteriorBusbar;

public sealed record BusbarMasterRequest(Guid? Id, string Code, string Name, string? Unit = null, string? SupplyType = null, bool IsActive = true, string? EcountProductCode = null, decimal? StandardUnitPrice = null);
public sealed record BusbarSettingsRequest(string CommonProjectCode, string? EcountCustomerCode = null, string? EcountWarehouseCode = null);
public sealed record BusbarBomLine(Guid MaterialId, decimal Quantity);
public sealed record BusbarBomRequest(Guid ProductFamilyId, IReadOnlyList<BusbarBomLine> Lines);
public sealed record BusbarProjectRequest(Guid? Id, string Name, string CustomerJobNumber, Guid ProductFamilyId, int RequestedQuantity, string Destination, DateOnly DueDate, string? Reason = null);
public sealed record BusbarPlanRequest(Guid? Id, Guid ProductFamilyId, DateOnly PlanDate, int Quantity);
public sealed record BusbarPurchaseRequest(Guid? Id, string OrderNumber, Guid MaterialId, decimal Quantity, DateOnly OrderDate, string? Reason = null);
public sealed record BusbarReceiptRequest(Guid RequestId, Guid PurchaseId, decimal Quantity);
public sealed record BusbarAdjustmentRequest(Guid RequestId, string StockKind, Guid ItemId, decimal Quantity, string Reason, bool IsOpening = false);
public sealed record BusbarShipmentRequest(Guid RequestId, Guid ProjectId, int Quantity);
public sealed record BusbarReverseRequest(Guid RequestId, string Reason);
public sealed record BusbarProductRequest(Guid RequestId, Guid ProductFamilyId, Guid WorkerId);
public sealed record BusbarProductCorrectionRequest(Guid WorkerId, string Reason);
public sealed record BusbarImportApplyRequest(IReadOnlyList<BusbarProjectRequest> Rows);
public sealed record BusbarPurchaseImportApplyRequest(IReadOnlyList<BusbarPurchaseRequest> Rows);
public sealed class BusbarException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record BusbarEcountRetryRequest(string Reason);
public sealed record BusbarEcountReconcileRequest(string Outcome, string Reason, string? SlipNumber = null);
