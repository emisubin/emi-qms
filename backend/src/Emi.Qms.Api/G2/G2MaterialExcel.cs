using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
namespace Emi.Qms.Api.G2;
public static partial class G2MaterialExcel
{
    public const int MaxBytes=25*1024*1024;
    public static G2MaterialImportResponse Parse(Stream stream, string? requestedSheet, DateOnly date, IReadOnlyList<G2MaterialCatalog> catalog)
    {
        try
        {
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true))
                if(zip.Entries.Count>2000 || zip.Entries.Sum(x=>x.Length)>80L*1024*1024) throw new ArgumentException("엑셀 파일의 압축 해제 크기가 너무 큽니다.");
            stream.Position=0; using var workbook=new XLWorkbook(stream);
            if(catalog.Count!=92)throw new ArgumentException("고정 품목 설정이 필요합니다.");
            var candidates=workbook.Worksheets.Where(ws=>LooksLikeInventory(ws,catalog)).ToArray();
            if(candidates.Length==0)throw new ArgumentException("자재 실사 양식 시트를 찾을 수 없습니다. A2(구분), E2(품목코드), N3(제작중)의 위치가 이동·삭제되었는지 원본 양식과 비교해 주세요.");
            var sheets=candidates.Select(ws=>new G2MaterialSheet(ws.Name,SheetDate(ws.Name),StructureError(ws,catalog))).ToArray();
            var valid=sheets.Where(x=>x.Error is null).ToArray();
            var selectedName=requestedSheet ?? (valid.Length==1?valid[0].Name:null);
            if(selectedName is null)
            {
                if(candidates.Length==1)throw new ArgumentException(sheets[0].Error);
                return new(sheets,null,null,[]);
            }
            var ws=candidates.FirstOrDefault(x=>x.Name==selectedName) ?? throw new ArgumentException("선택한 시트를 찾을 수 없습니다. 파일을 다시 확인해 주세요.");
            var structureError=sheets.Single(x=>x.Name==selectedName).Error;
            if(structureError is not null)throw new ArgumentException(structureError);
            var result=new List<G2MaterialValues>();var warnings=new List<string>();
            foreach(var item in catalog)
            {
                decimal? Number(int col)
                {
                    var cell=ws.Cell(item.SourceRow,col);
                    if(cell.HasFormula)throw Problem(ws,cell.Address.ToString()!,"실사 입력칸에는 수식을 사용할 수 없습니다.");
                    if(cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))return null;
                    if(cell.DataType!=XLDataType.Number || !cell.TryGetValue<decimal>(out var value))throw Problem(ws,cell.Address.ToString()!,"유효한 숫자 또는 빈칸이어야 합니다.");
                    if(value<0 || value>999999999999.9999m || decimal.Round(value,4)!=value)throw Problem(ws,cell.Address.ToString()!,"0 이상, 소수점 4자리 이내의 수량이어야 합니다.");
                    return value;
                }
                var overrides=new Dictionary<string,decimal>();
                foreach(var column in new[]{"Q","R","S","W","X","Y","Z","AA"})
                {
                    var cell=ws.Cell($"{column}{item.SourceRow}");
                    if(!cell.HasFormula)
                    {
                        overrides[column]=Number(cell.Address.ColumnNumber)!.Value;
                        warnings.Add($"시트 [{ws.Name}] {cell.Address}: 계산식 대신 직접 입력된 숫자를 가져옵니다. 관련 실사 수량을 수정하면 해당 합계·생산가능대수는 자동 계산으로 전환됩니다.");
                    }
                }
                result.Add(new(item.Id,Number(14),Number(15),Number(16),Number(20),Number(21),Number(22),overrides.Count==0?null:overrides));
            }
            G2MaterialRules.Validate(result);
            return new(sheets,ws.Name,SheetDate(ws.Name),result,warnings);
        }
        catch(ArgumentException){throw;}
        catch(Exception e) when(e is not OutOfMemoryException and not OperationCanceledException){throw new ArgumentException("엑셀 파일을 읽을 수 없습니다. 원본 양식의 .xlsx 파일인지 확인해 주세요.");}
    }
    private static ArgumentException Problem(IXLWorksheet ws,string address,string reason)=>new($"시트 [{ws.Name}] {address}: {reason} 원본 양식을 사용해 주세요.");
    private static bool LooksLikeInventory(IXLWorksheet ws,IReadOnlyList<G2MaterialCatalog> catalog)=>
        ws.Cell("E4").GetString().Trim()==catalog[0].Code.Trim() ||
        new[]{"A2","D2","E2","N2","T2"}.Count(a=>Headers.TryGetValue(a,out var expected)&&ws.Cell(a).GetString().Trim()==expected)>=2;
    private static readonly Dictionary<string,string> Headers=new()
    {
        ["AA1"]="안전재고 180SET",["A2"]="구분",["B2"]="NO",["C2"]="업체명",["D2"]="품명",["E2"]="품목코드",["F2"]="규격명",["G2"]="단위",["H2"]="1SET\n(수량)",
        ["I2"]="포장단위별 인입수량",["M2"]="이미지",["N2"]="제조 Part",["T2"]="자재 Part",["Y2"]="합계",
        ["I3"]="BOX",["J3"]="PT(EA)",["K3"]="PT(BOX)",["L3"]="비고",["N3"]="제작중",["O3"]="키팅대차",["P3"]="적재 랙",["Q3"]="제조실 합계",
        ["R3"]="생산가능대수",["S3"]="생산가능대수",["T3"]="자재실 합계",["U3"]="ERP",["V3"]="이동중",["W3"]="생산가능대수",["X3"]="생산가능대수",["Y3"]="총재고 합계",["Z3"]="생산가능대수 합계",["AA3"]="제조+자재 합계"
    };
    private static string? StructureError(IXLWorksheet ws,IReadOnlyList<G2MaterialCatalog> catalog)
    {
        try { ValidateStructure(ws,catalog);return null; } catch(ArgumentException e){return e.Message;}
    }
    private static void ValidateStructure(IXLWorksheet ws,IReadOnlyList<G2MaterialCatalog> catalog)
    {
        void Text(string address,string? expected,string label)
        {
            var cell=ws.Cell(address);
            if(cell.HasFormula || cell.GetString().Trim()!=(expected??"").Trim())throw Problem(ws,address,$"{label}이(가) 원본과 다릅니다.");
        }
        void Formula(string address,string expected,bool allowNumber=false)
        {
            var cell=ws.Cell(address);
            if(allowNumber && !cell.HasFormula && cell.DataType==XLDataType.Number && cell.TryGetValue<decimal>(out var literal) && literal>=0 && literal<=999999999999.9999m && decimal.Round(literal,4)==literal)return;
            if(!cell.HasFormula || !string.Equals(cell.FormulaA1,expected,StringComparison.OrdinalIgnoreCase))throw Problem(ws,address,"계산식이 변경되었거나 누락되었습니다.");
        }
        Formula("A1","TODAY()");
        for(var r=1;r<=3;r++)for(var c=1;c<=27;c++)
        {
            var cell=ws.Cell(r,c);var address=cell.Address.ToString()!;
            if(address=="A1")continue;
            Text(address,Headers.GetValueOrDefault(address),"열 제목·헤더 배치");
        }
        var expectedMerges=new HashSet<string>{"A1:C1","A2:A3","B2:B3","C2:C3","D2:D3","E2:E3","F2:F3","G2:G3","H2:H3","I2:L2","M2:M3","N2:S2","T2:X2","Y2:AA2"};
        foreach(var group in catalog.GroupBy(x=>x.Category))expectedMerges.Add($"A{group.Min(x=>x.SourceRow)}:A{group.Max(x=>x.SourceRow)}");
        var actual=ws.MergedRanges.Select(x=>x.RangeAddress.ToString()!).ToHashSet();
        var changed=expectedMerges.Except(actual).Concat(actual.Except(expectedMerges)).FirstOrDefault();
        if(changed is not null)throw Problem(ws,changed,"셀 병합 구조가 다릅니다.");
        foreach(var item in catalog)
        {
            var r=item.SourceRow;
            Text($"A{r}",catalog.Where(x=>x.Category==item.Category).Min(x=>x.SourceRow)==r?item.Category:"","구분");
            Formula($"B{r}","ROW()-3");
            foreach(var (column,value,label) in new[]{("C",item.Vendor,"업체명"),("D",item.Name,"품명"),("E",item.Code,"품목코드"),("F",item.Spec,"규격명"),("G",item.Unit,"단위"),("I",item.PackBox,"BOX"),("J",item.PackPalletEa,"PT(EA)"),("K",item.PackPalletBox,"PT(BOX)"),("L",item.PackNote,"포장 비고")})Text($"{column}{r}",value,label);
            var set=ws.Cell(r,8);
            if(set.HasFormula || !set.TryGetValue<decimal>(out var quantity) || quantity!=item.SetQuantity)throw Problem(ws,$"H{r}","1SET 수량이 다릅니다.");
            Text($"M{r}","","이미지 열의 셀 값");
            foreach(var (column,formula) in new[]{("Q",$"N{r}+O{r}+P{r}"),("R",$"ROUNDDOWN(S{r},0)"),("S",$"Q{r}/H{r}"),("W",$"ROUNDDOWN(X{r},0)"),("X",$"(T{r}+U{r}+V{r})/H{r}"),("Y",$"Q{r}+T{r}+V{r}+U{r}"),("Z",$"ROUNDDOWN(AA{r},0)"),("AA",$"ROUNDDOWN(S{r}+X{r},0)")})Formula($"{column}{r}",formula,true);
        }
        foreach(var cell in ws.CellsUsed(XLCellsUsedOptions.Contents))
        {
            var row=cell.Address.RowNumber;var column=cell.Address.ColumnNumber;
            // The approved workbook contains a stray dot outside the inventory table.
            if(cell.Address.ToString()! =="AD57" && !cell.HasFormula && cell.GetString()==".")continue;
            if(row>95 || column>27)throw Problem(ws,cell.Address.ToString()!,"고정 양식 밖에 추가된 행 또는 열의 값이 있습니다.");
        }
    }
    public static DateOnly? SheetDate(string name)
    {
        var m=DatePattern().Match(name.Trim());if(!m.Success)return null;
        var year=int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);if(year<100)year+=2000;
        return DateOnly.TryParseExact($"{year:D4}-{m.Groups[2].Value}-{m.Groups[3].Value}","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
    }
    [GeneratedRegex(@"^(\d{2}|\d{4})-(\d{2})-(\d{2})(?:\D|$)")]private static partial Regex DatePattern();
}
