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
        foreach(var (c,t) in new (int,string)[]{(14,"제작중"),(15,"키팅대차"),(16,"적재 랙"),(20,"자재실 합계"),(21,"ERP"),(22,"이동중")})sheet.Cell(3,c).Value=t;
        foreach(var item in Catalog()){var r=item.SourceRow;sheet.Cell(r,3).Value=item.Vendor;sheet.Cell(r,4).Value=item.Name;sheet.Cell(r,5).Value=item.Code;sheet.Cell(r,6).Value=item.Spec;sheet.Cell(r,7).Value=item.Unit;sheet.Cell(r,8).Value=item.SetQuantity;sheet.Cell(r,20).Value=item.Id;}
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
    [Fact]public void ImportRejectsWrongDateAndFormulaInputs()
    {
        using var wrongDate=Workbook();Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(wrongDate,null,new(2026,10,8),Catalog()));
        using var formula=Workbook(s=>s.Cell(4,14).FormulaA1="1+2");Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(formula,null,new(2026,10,7),Catalog()));
    }
    [Fact]public void ImportRejectsObsoleteHeader()
    {
        using var stream=Workbook(s=>s.Cell(3,22).Value="IQC");Assert.Throws<ArgumentException>(()=>G2MaterialExcel.Parse(stream,null,new(2026,10,7),Catalog()));
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
