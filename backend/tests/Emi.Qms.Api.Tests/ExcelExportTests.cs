using ClosedXML.Excel;
using Emi.Qms.Api.DataExports;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ExcelExportTests
{
    [Fact]
    public void WorkbookBuilder_WritesLeadingMarkersAsTextWithoutFormulas()
    {
        var values = new[] { "=1+1", "+2", "-3", "@SUM(A1)", "\tcommand", "\rcommand" };
        var builder = new ExcelWorkbookBuilder(TimeProvider.System);

        var bytes = builder.Build(
            "Synthetic export",
            "Synthetic",
            "검색 =formula",
            values,
            [new ExcelColumn<string>("값", value => value)]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet("Synthetic");
        Assert.DoesNotContain(worksheet.CellsUsed(), cell => cell.HasFormula);
        var reparsedValues = Enumerable.Range(6, values.Length).Select(row => worksheet.Cell(row, 1).GetString()).ToArray();
        Assert.Equal(values[..5], reparsedValues[..5]);
        Assert.Equal("\ncommand", reparsedValues[5]);
        Assert.All(Enumerable.Range(6, values.Length), row => Assert.Equal(XLDataType.Text, worksheet.Cell(row, 1).DataType));
        Assert.True(worksheet.AutoFilter.IsEnabled);
        Assert.True(worksheet.SheetView.SplitRow >= 5);
    }

    [Fact]
    public void WorkbookBuilder_CreatesHeaderOnlyWorkbookForZeroRows()
    {
        var builder = new ExcelWorkbookBuilder(TimeProvider.System);

        var bytes = builder.Build<object>(
            "Empty export",
            "Empty",
            "전체",
            [],
            [new ExcelColumn<object>("헤더", _ => "unused")]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet("Empty");
        Assert.Equal("헤더", worksheet.Cell(5, 1).GetString());
        Assert.True(worksheet.Cell(6, 1).IsEmpty());
        Assert.True(worksheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public void ConcurrencyGate_RejectsThirdLeaseAndReleasesSlotsExactlyOnce()
    {
        var gate = new ExcelExportConcurrencyGate();

        Assert.True(gate.TryAcquire(out var first));
        Assert.True(gate.TryAcquire(out var second));
        Assert.False(gate.TryAcquire(out var rejected));
        rejected.Dispose();

        first.Dispose();
        first.Dispose();
        Assert.True(gate.TryAcquire(out var replacement));
        Assert.False(gate.TryAcquire(out var stillRejected));

        second.Dispose();
        replacement.Dispose();
        stillRejected.Dispose();
        Assert.True(gate.TryAcquire(out var finalFirst));
        Assert.True(gate.TryAcquire(out var finalSecond));
        finalFirst.Dispose();
        finalSecond.Dispose();
    }
}
