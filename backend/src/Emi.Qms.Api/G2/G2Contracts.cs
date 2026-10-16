using System.Text.Json;

namespace Emi.Qms.Api.G2;

public static class G2MetricCodes
{
    public const string MorningProduction = "MorningProduction";
    public const string AfternoonProduction = "AfternoonProduction";
    public const string MorningRepair = "MorningRepair";
    public const string AfternoonRepair = "AfternoonRepair";
    public const string Delivery = "Delivery";
    public const string Defect = "Defect";
    public const string MorningEmiAttendance = "MorningEmiAttendance";
    public const string MorningContractorAttendance = "MorningContractorAttendance";
    public const string AfternoonEmiAttendance = "AfternoonEmiAttendance";
    public const string AfternoonContractorAttendance = "AfternoonContractorAttendance";
    public static readonly IReadOnlyList<string> All = [MorningProduction, AfternoonProduction, MorningRepair, AfternoonRepair, Delivery, Defect, MorningEmiAttendance, MorningContractorAttendance, AfternoonEmiAttendance, AfternoonContractorAttendance];
}

public static class G2DailyValueCodes
{
    public const string ProductionPlan = "ProductionPlan";
    public const string DeliveryPlan = "DeliveryPlan";
    public static readonly IReadOnlyList<string> All = [ProductionPlan, DeliveryPlan];
}

public static class G2NoteCodes
{
    public const string Plan = "PlanNote";
    public const string Actual = "ActualNote";
    public const string Row = "RowNote";
    public static readonly IReadOnlyList<string> All = [Plan, Actual, Row];
}

public static class G2TargetTypes
{
    public const string DailyProduction = "DailyProduction";
    public const string Delivery = "Delivery";
    public const string Inventory = "Inventory";
    public static bool IsValid(string value) => value is DailyProduction or Delivery or Inventory;
}

public sealed record G2MetricValueResponse(int? Quantity, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
public sealed record G2InventoryCountResponse(int? Quantity, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
public sealed record G2TextValueResponse(string? Text, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
public sealed record G2TargetResponse(string TargetType, DateOnly EffectiveDate, int Quantity, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
public sealed record G2DayResponse(
    DateOnly Date,
    bool IsForecast,
    G2MetricValueResponse? MorningProduction,
    G2MetricValueResponse? AfternoonProduction,
    G2MetricValueResponse? Delivery,
    G2MetricValueResponse? Defect,
    G2MetricValueResponse? MorningEmiAttendance,
    G2MetricValueResponse? MorningContractorAttendance,
    G2MetricValueResponse? AfternoonEmiAttendance,
    G2MetricValueResponse? AfternoonContractorAttendance,
    long? ProductionTotal,
    long? MorningAttendanceTotal,
    long? AfternoonAttendanceTotal,
    long? AttendanceTotal,
    long? Inventory,
    G2InventoryCountResponse? PhysicalCount,
    G2TargetResponse? DailyProductionTarget,
    G2TargetResponse? DeliveryTarget,
    G2TargetResponse? InventoryTarget,
    G2MetricValueResponse? MorningRepair = null,
    G2MetricValueResponse? AfternoonRepair = null,
    long? RepairTotal = null,
    long? DefectInventory = 0,
    G2InventoryCountResponse? DefectInventoryCount = null,
    G2MetricValueResponse? ProductionPlan = null,
    G2MetricValueResponse? DeliveryPlan = null,
    G2TextValueResponse? PlanNote = null,
    G2TextValueResponse? ActualNote = null,
    G2TextValueResponse? RowNote = null,
    long? SimpleDefectInventory = null,
    long? ComplexLongDefectInventory = null);
public sealed record G2RangeResponse(DateOnly Today, DateOnly From, DateOnly To, IReadOnlyList<G2DayResponse> Days);
public sealed record G2HomeResponse(DateOnly Today, int Year, int Month, bool HasInventoryBaseline, IReadOnlyList<G2DayResponse> Days, decimal? AverageDailyProduction = null, int ProductionSampleDays = 0);
public sealed record G2MetricChangeRequest(int? Quantity, int? ExpectedVersion);
public sealed record SaveG2OperationsRequest(
    G2MetricChangeRequest? MorningProduction,
    G2MetricChangeRequest? AfternoonProduction,
    G2MetricChangeRequest? Delivery,
    G2MetricChangeRequest? Defect,
    G2MetricChangeRequest? MorningRepair = null,
    G2MetricChangeRequest? AfternoonRepair = null);
public sealed record SaveG2AttendanceRequest(G2MetricChangeRequest? MorningEmiAttendance, G2MetricChangeRequest? MorningContractorAttendance, G2MetricChangeRequest? AfternoonEmiAttendance, G2MetricChangeRequest? AfternoonContractorAttendance);
public sealed record SaveG2InventoryCountRequest(int Quantity, int? ExpectedVersion);
public sealed record SaveG2TargetRequest(int Quantity, int? ExpectedVersion);
public sealed record G2TextChangeRequest(string? Text, int? ExpectedVersion);
public sealed record SaveG2WorkspaceRequest(
    G2MetricChangeRequest? ProductionPlan,
    G2MetricChangeRequest? DeliveryPlan,
    G2MetricChangeRequest? MorningProduction,
    G2MetricChangeRequest? AfternoonProduction,
    G2MetricChangeRequest? MorningRepair,
    G2MetricChangeRequest? AfternoonRepair,
    G2MetricChangeRequest? Delivery,
    G2MetricChangeRequest? Defect,
    G2MetricChangeRequest? PhysicalCount,
    G2MetricChangeRequest? DefectInventoryCount,
    G2TextChangeRequest? PlanNote,
    G2TextChangeRequest? ActualNote,
    G2TextChangeRequest? RowNote);
public sealed record SaveG2PlansRequest(
    DateOnly From,
    DateOnly To,
    bool ExcludeSundays,
    bool Overwrite,
    JsonElement ProductionPlan,
    JsonElement DeliveryPlan,
    JsonElement PlanNote);
internal sealed record G2MetricChange(string MetricCode, int? Quantity, int? ExpectedVersion);
internal sealed record G2DailyValueChange(string ValueCode, int? Quantity, int? ExpectedVersion);
internal sealed record G2TextChange(string NoteCode, string? Text, int? ExpectedVersion);
