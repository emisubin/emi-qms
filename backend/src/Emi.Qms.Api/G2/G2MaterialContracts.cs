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
    public static Dictionary<string,decimal> Calculate(G2MaterialValues v, decimal perSet)
    {
        if(perSet<=0)throw new ArgumentException("1SET 수량을 확인해 주세요.");
        var q=(v.Working??0)+(v.Kitting??0)+(v.Rack??0);
        var material=(v.Materials??0)+(v.Erp??0)+(v.Transit??0);
        var total=q+material;
        return new(){["Q"]=q,["R"]=decimal.Truncate(q/perSet),["S"]=q/perSet,["W"]=decimal.Truncate(material/perSet),["X"]=material/perSet,["Y"]=total,["Z"]=decimal.Truncate(total/perSet),["AA"]=decimal.Truncate(total/perSet)};
    }
    private static string ExcelNumber(decimal value) => ((double)value).ToString("G15",System.Globalization.CultureInfo.InvariantCulture);
    public static void ValidateCalculations(G2MaterialValues values, G2MaterialCatalog item, string? sheet=null)
    {
        var expected=Calculate(values,item.SetQuantity);
        foreach(var pair in values.CalculationOverrides??[])
            if(!expected.TryGetValue(pair.Key,out var number) || ExcelNumber(pair.Value)!=ExcelNumber(number))
                throw new ArgumentException($"{(sheet is null?"":$"시트 [{sheet}] ")}{pair.Key}{item.SourceRow} · {item.Id}번 {item.Name}: 합계가 맞지 않습니다. 입력값 {pair.Value}, 정상 계산값 {expected.GetValueOrDefault(pair.Key)}. 엑셀 값을 수정한 뒤 다시 가져와 주세요. 저장할 수 없습니다.");
    }
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
                    if (value < 0)
                        throw new ArgumentException($"{item.Id}번 계산값은 0 이상이어야 합니다.");
            }
        }
    }
}
