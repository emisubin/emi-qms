namespace Emi.Qms.Api.G2;
public sealed record G2MaterialValues(int Id, decimal? Working, decimal? Kitting, decimal? Rack, decimal? Materials, decimal? Erp, decimal? Transit, Dictionary<string, decimal>? CalculationOverrides = null);
public sealed record SaveG2MaterialsRequest(IReadOnlyList<G2MaterialValues> Items);
public sealed record G2MaterialCatalog(int Id, int SourceRow, string Category, string Vendor, string Name, string Code, string Spec, string Unit, decimal SetQuantity, string? PackBox, string? PackPalletEa, string? PackPalletBox, string? PackNote, string[]? ImageKeys, Dictionary<string,string>? CellFills);
public sealed record G2MaterialItem(int Id, int SourceRow, string Category, string Vendor, string Name, string Code, string Spec, string Unit, decimal SetQuantity, string? PackBox, string? PackPalletEa, string? PackPalletBox, string? PackNote, string[]? ImageKeys, Dictionary<string,string>? CellFills, G2MaterialValues Values, DateTime? UpdatedAtUtc);
public sealed record G2MaterialResponse(DateOnly Date, bool CanEdit, IReadOnlyList<G2MaterialItem> Items);
public sealed record G2MaterialSheet(string Name, DateOnly? Date, string? Error = null);
public sealed record G2MaterialImportResponse(IReadOnlyList<G2MaterialSheet> Sheets, string? SelectedSheet, DateOnly? SelectedDate, IReadOnlyList<G2MaterialValues> Items, IReadOnlyList<string>? Warnings = null);
public static class G2MaterialRules
{
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")).DateTime);
    public static DateOnly Cutoff(DateOnly today) => today.AddMonths(-6);
    public static void ValidateDate(DateOnly date, DateOnly today) { if (date < Cutoff(today) || date > today) throw new ArgumentException("실사일은 오늘까지 최근 6개월 범위에서 선택해 주세요."); }
    public static void Validate(IReadOnlyList<G2MaterialValues>? items)
    {
        if (items is null || items.Count != 92 || items.Any(x => x is null) || !items.Select(x => x.Id).Order().SequenceEqual(Enumerable.Range(1,92))) throw new ArgumentException("고정된 92개 품목을 모두 포함해야 합니다.");
        foreach (var item in items)
        {
            foreach (var value in new[] {item.Working,item.Kitting,item.Rack,item.Materials,item.Erp,item.Transit})
                if (value is not null && (value < 0 || value > 999999999999.9999m || decimal.Round(value.Value,4) != value)) throw new ArgumentException($"{item.Id}번 수량은 0 이상, 소수점 4자리 이내로 입력해 주세요.");
            if (item.CalculationOverrides is not null)
            {
                if (item.CalculationOverrides.Count > 8 || item.CalculationOverrides.Keys.Any(key => key is not ("Q" or "R" or "S" or "W" or "X" or "Y" or "Z" or "AA")))
                    throw new ArgumentException($"{item.Id}번 계산값은 지정된 8개 열만 포함할 수 있습니다.");
                foreach (var value in item.CalculationOverrides.Values)
                    if (value < 0 || value > 999999999999.9999m || decimal.Round(value, 4) != value)
                        throw new ArgumentException($"{item.Id}번 계산값은 0 이상, 소수점 4자리 이내로 입력해 주세요.");
            }
        }
    }
}
