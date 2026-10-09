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
            var sheets=workbook.Worksheets.Select(x=>(Sheet:x,Date:SheetDate(x.Name))).Where(x=>x.Date is not null).ToArray();
            var selected=requestedSheet is null ? sheets.FirstOrDefault(x=>x.Date==date) : sheets.FirstOrDefault(x=>x.Sheet.Name==requestedSheet);
            if(selected.Sheet is null || selected.Date!=date) throw new ArgumentException("선택한 실사일과 일치하는 날짜의 시트를 선택해 주세요.");
            var ws=selected.Sheet;var result=new List<G2MaterialValues>();
            foreach(var (column,title) in new (int,string)[]{(14,"제작중"),(15,"키팅대차"),(16,"적재 랙"),(20,"자재실 합계"),(21,"ERP"),(22,"이동중")})
                if(ws.Cell(3,column).GetString().Trim()!=title)throw new ArgumentException($"실사 양식의 {title} 열 제목을 확인해 주세요.");
            if(catalog.Count!=92)throw new ArgumentException("고정 품목 설정이 필요합니다.");
            foreach(var item in catalog)
            {
                var row=item.SourceRow;
                string Text(int col)=>ws.Cell(row,col).GetString().Trim();
                if(Text(3)!=item.Vendor.Trim() || Text(4)!=item.Name.Trim() || Text(5)!=item.Code.Trim() || Text(6)!=item.Spec.Trim() || Text(7)!=item.Unit.Trim() || !ws.Cell(row,8).TryGetValue<decimal>(out var set) || set!=item.SetQuantity)
                    throw new ArgumentException($"엑셀 {row}행의 품목·규격·단위·1SET 수량이 고정 품목과 다릅니다.");
                decimal? Number(int col)
                {
                    var cell=ws.Cell(row,col); if(cell.HasFormula)throw new ArgumentException($"엑셀 {cell.Address} 실사 입력칸에는 수식을 사용할 수 없습니다.");
                    if(cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetString()))return null;
                    if(cell.DataType!=XLDataType.Number || !cell.TryGetValue<decimal>(out var value))throw new ArgumentException($"엑셀 {cell.Address}에 유효한 숫자를 입력해 주세요.");
                    return value;
                }
                result.Add(new(item.Id,Number(14),Number(15),Number(16),Number(20),Number(21),Number(22)));
            }
            var last=ws.LastRowUsed()?.RowNumber()??0;
            for(var r=catalog.Max(x=>x.SourceRow)+1;r<=last;r++)if(!ws.Cell(r,5).IsEmpty())throw new ArgumentException("고정 92개 이외의 품목이 있습니다.");
            G2MaterialRules.Validate(result);
            return new(sheets.Select(x=>new G2MaterialSheet(x.Sheet.Name,x.Date!.Value)).ToArray(),selected.Sheet.Name,selected.Date!.Value,result);
        }
        catch(ArgumentException){throw;}
        catch(Exception e) when(e is not OutOfMemoryException and not OperationCanceledException){throw new ArgumentException("엑셀 파일을 읽을 수 없습니다. 원본 양식의 .xlsx 파일인지 확인해 주세요.");}
    }
    public static DateOnly? SheetDate(string name)
    {
        var m=DatePattern().Match(name.Trim());if(!m.Success)return null;
        var year=int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);if(year<100)year+=2000;
        return DateOnly.TryParseExact($"{year:D4}-{m.Groups[2].Value}-{m.Groups[3].Value}","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
    }
    [GeneratedRegex(@"^(\d{2}|\d{4})-(\d{2})-(\d{2})(?:\D|$)")]private static partial Regex DatePattern();
}
