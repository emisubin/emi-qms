namespace Emi.Qms.Api.G2;

public static class G2MetricCodes
{
    public const string MorningProduction = "MorningProduction";
    public const string AfternoonProduction = "AfternoonProduction";
    public const string Delivery = "Delivery";
    public const string Defect = "Defect";
    public const string MorningEmiAttendance = "MorningEmiAttendance";
    public const string MorningContractorAttendance = "MorningContractorAttendance";
    public const string AfternoonEmiAttendance = "AfternoonEmiAttendance";
    public const string AfternoonContractorAttendance = "AfternoonContractorAttendance";
    public static readonly IReadOnlyList<string> All = [MorningProduction, AfternoonProduction, Delivery, Defect, MorningEmiAttendance, MorningContractorAttendance, AfternoonEmiAttendance, AfternoonContractorAttendance];
}

public static class G2TargetTypes
{
    public const string DailyProduction = "DailyProduction";
    public const string Delivery = "Delivery";
    public const string Inventory = "Inventory";
    public static bool IsValid(string value) => value is DailyProduction or Delivery or Inventory;
}

public sealed record G2MetricValueResponse(int? Quantity, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
public sealed record G2InventoryCountResponse(int Quantity, int Version, DateTimeOffset UpdatedAtUtc, string UpdatedByDisplayName);
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
    G2TargetResponse? InventoryTarget);
public sealed record G2RangeResponse(DateOnly Today, DateOnly From, DateOnly To, IReadOnlyList<G2DayResponse> Days);
public sealed record G2HomeResponse(DateOnly Today, int Year, int Month, bool HasInventoryBaseline, IReadOnlyList<G2DayResponse> Days);
public sealed record G2MetricChangeRequest(int? Quantity, int? ExpectedVersion);
public sealed record SaveG2OperationsRequest(G2MetricChangeRequest? MorningProduction, G2MetricChangeRequest? AfternoonProduction, G2MetricChangeRequest? Delivery, G2MetricChangeRequest? Defect);
public sealed record SaveG2AttendanceRequest(G2MetricChangeRequest? MorningEmiAttendance, G2MetricChangeRequest? MorningContractorAttendance, G2MetricChangeRequest? AfternoonEmiAttendance, G2MetricChangeRequest? AfternoonContractorAttendance);
public sealed record SaveG2InventoryCountRequest(int Quantity, int? ExpectedVersion);
public sealed record SaveG2TargetRequest(int Quantity, int? ExpectedVersion);
internal sealed record G2MetricChange(string MetricCode, int? Quantity, int? ExpectedVersion);
