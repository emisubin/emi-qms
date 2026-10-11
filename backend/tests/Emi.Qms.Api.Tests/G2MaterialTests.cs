using ClosedXML.Excel;
using Emi.Qms.Api.G2;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Emi.Qms.Api.Tests;
public sealed class G2MaterialTests
{
    private static G2MaterialCatalog[] Catalog()=>Enumerable.Range(1,92).Select(i=>new G2MaterialCatalog(i,i+3,"사급","Vendor","Part",i is 80 or 83?"RIBBON":$"CODE{i}","Spec","EA",2,null,null,null,null,null,null)).ToArray();
    private static MemoryStream Workbook(Action<IXLWorksheet>? edit=null)
    {
        using var book=new XLWorkbook();var sheet=book.AddWorksheet("26-10-07오후 ");
        sheet.Cell("A1").FormulaA1="TODAY()";sheet.Cell("AA1").Value="안전재고 180SET";
        var row2=new Dictionary<string,string>{{"A","구분"},{"B","NO"},{"C","업체명"},{"D","품명"},{"E","품목코드"},{"F","규격명"},{"G","단위"},{"H","1SET\n(수량)"},{"I","포장단위별 인입수량"},{"M","이미지"},{"N","제조 Part"},{"T","자재 Part"},{"Y","합계"}};
        var row3=new Dictionary<string,string>{{"I","BOX"},{"J","PT(EA)"},{"K","PT(BOX)"},{"L","비고"},{"N","제작중"},{"O","키팅대차"},{"P","적재 랙"},{"Q","제조실 합계"},{"R","생산가능대수"},{"S","생산가능대수"},{"T","자재실 합계"},{"U","ERP"},{"V","이동중"},{"W","생산가능대수"},{"X","생산가능대수"},{"Y","총재고 합계"},{"Z","생산가능대수 합계"},{"AA","제조+자재 합계"}};
        foreach(var pair in row2)sheet.Cell(pair.Key+"2").Value=pair.Value;
        foreach(var pair in row3)sheet.Cell(pair.Key+"3").Value=pair.Value;
        foreach(var range in new[]{"A1:C1","A2:A3","B2:B3","C2:C3","D2:D3","E2:E3","F2:F3","G2:G3","H2:H3","I2:L2","M2:M3","N2:S2","T2:X2","Y2:AA2","A4:A95"})sheet.Range(range).Merge();
        sheet.Cell("A4").Value="사급";
        foreach(var item in Catalog())
        {
            var r=item.SourceRow;sheet.Cell(r,3).Value=item.Vendor;sheet.Cell(r,4).Value=item.Name;sheet.Cell(r,5).Value=item.Code;sheet.Cell(r,6).Value=item.Spec;sheet.Cell(r,7).Value=item.Unit;sheet.Cell(r,8).Value=item.SetQuantity;sheet.Cell(r,20).Value=item.Id;
            sheet.Cell($"B{r}").FormulaA1="ROW()-3";sheet.Cell($"Q{r}").FormulaA1=$"N{r}+O{r}+P{r}";sheet.Cell($"R{r}").FormulaA1=$"ROUNDDOWN(S{r},0)";sheet.Cell($"S{r}").FormulaA1=$"Q{r}/H{r}";sheet.Cell($"W{r}").FormulaA1=$"ROUNDDOWN(X{r},0)";sheet.Cell($"X{r}").FormulaA1=$"(T{r}+U{r}+V{r})/H{r}";sheet.Cell($"Y{r}").FormulaA1=$"Q{r}+T{r}+V{r}+U{r}";sheet.Cell($"Z{r}").FormulaA1=$"ROUNDDOWN(AA{r},0)";sheet.Cell($"AA{r}").FormulaA1=$"ROUNDDOWN(S{r}+X{r},0)";
        }
        edit?.Invoke(sheet);var stream=new MemoryStream();book.SaveAs(stream);stream.Position=0;return stream;
    }
    [Fact]public void ImportPreservesNullZeroAndDistinctDuplicateCodes()
    {
        using var stream=Workbook(s=>s.Cell(4,14).Value=0);var parsed=G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog());
        Assert.Equal(92,parsed.Items.Count);Assert.Equal(0,parsed.Items[0].Working);Assert.Null(parsed.Items[1].Working);Assert.Equal(80,parsed.Items[79].Materials);Assert.Equal(83,parsed.Items[82].Materials);Assert.Equal(new DateOnly(2026,10,7),parsed.SelectedDate);
    }
    [Theory][InlineData(7)][InlineData(8)]public void ImportRejectsUnitOrConsumptionChanges(int column)
    {
        using var stream=Workbook(s=>s.Cell(4,column).Value="wrong");Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog()));
    }
    [Fact]public void ImportReturnsDateHintAndRejectsFormulaInputs()
    {
        using var wrongDate=Workbook();Assert.Equal(new DateOnly(2026,10,7),G2MaterialExcel.Parse(wrongDate,null,new(2026,10,8),Catalog()).SelectedDate);
        using var formula=Workbook(s=>s.Cell(4,14).FormulaA1="1+2");Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(formula,null,new(2026,10,7),Catalog()));
    }
    [Fact]public void ImportRejectsObsoleteHeader()
    {
        using var stream=Workbook(s=>s.Cell(3,22).Value="IQC");Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog()));
    }
    [Theory][InlineData("Q",3)][InlineData("R",1)][InlineData("S",1.5)][InlineData("W",4)][InlineData("X",4)][InlineData("Y",11)][InlineData("Z",5)][InlineData("AA",5)]
    public void MatchingLiteralIsAcceptedSilentlyAndMismatchBlocks(string column, decimal expected)
    {
        using var stream=Workbook(s=>{s.Cell("N11").Value=3;s.Cell(column+"11").Clear(XLClearOptions.Contents);s.Cell(column+"11").Value=expected;});
        var result=G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog());
        Assert.Null(result.Items.Single(x=>x.Id==8).CalculationOverrides);Assert.Null(result.Warnings);
        using var mismatch=Workbook(s=>{s.Cell("N11").Value=3;s.Cell(column+"11").Clear(XLClearOptions.Contents);s.Cell(column+"11").Value=expected+1;});
        var error=Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(mismatch,null,new(2026,10,7),Catalog()));
        Assert.Contains(column+"11",error.Message);Assert.Contains("합계가 맞지 않습니다",error.Message);Assert.Contains("정상 계산값",error.Message);
    }
    [Theory][InlineData("0.333333333333333",true)][InlineData("0.333333333333334",false)]
    public void FractionalLiteralUsesExcelPrecision(string literal,bool accepted)
    {
        var catalog=Catalog().Select(x=>x with {SetQuantity=3}).ToArray();
        using var stream=Workbook(s=>{for(var r=4;r<=95;r++)s.Cell(r,8).Value=3;s.Cell("N11").Value=1;s.Cell("S11").Clear(XLClearOptions.Contents);s.Cell("S11").Value=decimal.Parse(literal,System.Globalization.CultureInfo.InvariantCulture);});
        if(accepted)Assert.Null(G2MaterialExcel.Parse(stream,null,new(2026,10,7),catalog).Warnings);
        else Assert.Contains("합계가 맞지 않습니다",Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),catalog)).Message);
    }
    [Fact]public void MissingCalculationIsStillRejected()
    {
        using var stream=Workbook(s=>s.Cell("Q11").Clear(XLClearOptions.Contents));
        Assert.Contains("Q11",Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog())).Message);
    }
    [Fact]public void RenamedSheetAndStyleChangesAreAccepted()
    {
        using var stream=Workbook(s=>{s.Name="Sheet1";s.Column(14).Hide();s.Column(3).Width=30;s.Cell("D4").Style.Fill.BackgroundColor=XLColor.Red;s.Cell("D4").Style.Font.Bold=true;});
        var result=G2MaterialExcel.Parse(stream,null,new(2026,10,9),Catalog());Assert.Equal("Sheet1",result.SelectedSheet);Assert.Null(result.SelectedDate);Assert.Equal(92,result.Items.Count);
    }
    [Fact]public void MultipleSheetsRequireSelectionAndNoRowsAreReturnedUntilSelected()
    {
        using var stream=Workbook(s=>s.CopyTo("Sheet2"));var result=G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog());
        Assert.Null(result.SelectedSheet);Assert.Empty(result.Items);Assert.Equal(2,result.Sheets.Count);
        stream.Position=0;result=G2MaterialExcel.Parse(stream,"Sheet2",new(2026,10,9),Catalog());Assert.Equal(92,result.Items.Count);Assert.Null(result.SelectedDate);
    }
    [Theory][InlineData("A2")][InlineData("D2")][InlineData("AA3")][InlineData("I4")][InlineData("A4")][InlineData("Q4")][InlineData("B4")][InlineData("E95")][InlineData("N96")][InlineData("AB4")]
    public void AnyChangedStructuralCellBlocksImportWithLocation(string address)
    {
        using var stream=Workbook(s=>{s.Cell(address).Clear(XLClearOptions.Contents);s.Cell(address).Value="changed";});
        var error=Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog()));Assert.Contains(address,error.Message);Assert.Contains("시트",error.Message);
    }
    [Fact]public void ChangedFormulaAndMergedRangeAreRejected()
    {
        using var formula=Workbook(s=>s.Cell("AA80").FormulaA1="1+2");Assert.Contains("AA80",Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(formula,null,new(2026,10,7),Catalog())).Message);
        using var merge=Workbook(s=>s.Range("N2:S2").Unmerge());Assert.Contains("N2:S2",Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(merge,null,new(2026,10,7),Catalog())).Message);
    }
    [Fact]public void BrokenSheetIsListedWithProblemAlongsideValidSheet()
    {
        using var stream=Workbook(s=>{var bad=s.CopyTo("broken");bad.Cell("U3").Value="wrong";});var result=G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog());
        Assert.Equal(92,result.Items.Count);Assert.Contains("U3",result.Sheets.Single(x=>x.Name=="broken").Error);
        stream.Position=0;Assert.Contains("U3",Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,"broken",new(2026,10,7),Catalog())).Message);
    }
    [Fact]public void ImportRejectsMalformedFile()
    {
        using var stream=new MemoryStream([1,2,3]);Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog()));
    }
    [Fact]public void ValidationRejectsDuplicatesNegativeAndExcessPrecision()
    {
        var values=Enumerable.Range(1,92).Select(i=>new G2MaterialValues(i,null,null,null,null,null,null)).ToArray();G2MaterialRules.Validate(values);
        values[0]=values[0] with{Working=-1};Assert.Throws<ArgumentException>(()=>G2MaterialRules.Validate(values));values[0]=values[0] with{Working=.00001m};Assert.Throws<ArgumentException>(()=>G2MaterialRules.Validate(values));values[0]=values[1];Assert.Throws<ArgumentException>(()=>G2MaterialRules.Validate(values));
    }
    [Theory][InlineData(false, "false", false)][InlineData(true, "true", false)][InlineData(false, "true", true)]
    public void RetentionActivationMatchesHostedWorker(bool reviewSafe, string enabled, bool expected)
    {
        var settings=new Dictionary<string,string?> { ["ReviewSafe:Enabled"]=reviewSafe.ToString(),["G2Materials:Retention:Enabled"]=enabled };
        using var factory=QmsWebApplicationFactory.Create("Development",settings);
        Assert.Equal(expected,factory.Services.GetServices<IHostedService>().Any(x=>x is G2MaterialRetentionWorker));
        var config=new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.Equal(expected,MutationWorkerActivationPolicy.Evaluate(config,reviewSafe).G2MaterialRetentionWorkerEnabled);
    }
    [Fact]public void RetentionDefaultsOnAndMalformedFailsClosed()
    {
        Assert.True(MutationWorkerActivationPolicy.Evaluate(new ConfigurationBuilder().Build(),false).G2MaterialRetentionWorkerEnabled);
        var bad=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"G2Materials:Retention:Enabled","bad"}}).Build();
        Assert.Throws<InvalidOperationException>(()=>MutationWorkerActivationPolicy.Evaluate(bad,false));
        Assert.False(MutationWorkerActivationPolicy.Evaluate(bad,true).G2MaterialRetentionWorkerEnabled);
    }
    [Fact]public void NullItemFromJsonIsValidationError()
    {
        var body="{\"items\":[null,"+string.Join(",",Enumerable.Range(2,91).Select(i=>$"{{\"id\":{i}}}"))+"]}";
        var request=System.Text.Json.JsonSerializer.Deserialize<SaveG2MaterialsRequest>(body,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Throws<ArgumentException>(()=>G2MaterialRules.Validate(request.Items));
    }
    [Fact]public void RetentionUsesCalendarMonthsAndInclusiveBoundary()
    {
        Assert.Equal(new DateOnly(2026,4,9),G2MaterialRules.Cutoff(new(2026,10,9)));Assert.Equal(new DateOnly(2026,2,28),G2MaterialRules.Cutoff(new(2026,8,31)));G2MaterialRules.ValidateDate(new(2026,4,9),new(2026,10,9));Assert.Throws<ArgumentException>(()=>G2MaterialRules.ValidateDate(new(2026,4,8),new(2026,10,9)));
    }
}
