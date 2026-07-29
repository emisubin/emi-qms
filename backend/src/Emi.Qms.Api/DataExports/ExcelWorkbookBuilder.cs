using System.Globalization;
using ClosedXML.Excel;

namespace Emi.Qms.Api.DataExports;

public sealed record ExcelColumn<T>(string Header, Func<T, object?> ValueSelector);

public sealed class ExcelWorkbookBuilder(TimeProvider timeProvider)
{
    public byte[] Build<T>(
        string title,
        string sheetName,
        string filterSummary,
        IReadOnlyList<T> rows,
        IReadOnlyList<ExcelColumn<T>> columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentOutOfRangeException.ThrowIfZero(columns.Count);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        var lastColumn = columns.Count;

        var titleRange = worksheet.Range(1, 1, 1, lastColumn).Merge();
        WriteText(titleRange.FirstCell(), title);
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#17324D");
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        titleRange.Style.Alignment.Indent = 1;
        worksheet.Row(1).Height = 30;

        WriteText(worksheet.Cell(2, 1), "생성일시");
        worksheet.Cell(2, 2).Value = timeProvider.GetUtcNow().UtcDateTime;
        worksheet.Cell(2, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        WriteText(worksheet.Cell(3, 1), "적용 필터");
        WriteText(worksheet.Cell(3, 2), BoundText(filterSummary, 240));

        const int headerRow = 5;
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var cell = worksheet.Cell(headerRow, columnIndex + 1);
            WriteText(cell, columns[columnIndex].Header);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C6E73");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                WriteValue(
                    worksheet.Cell(headerRow + rowIndex + 1, columnIndex + 1),
                    columns[columnIndex].ValueSelector(rows[rowIndex]));
            }
        }

        var finalRow = Math.Max(headerRow, headerRow + rows.Count);
        worksheet.Range(headerRow, 1, finalRow, lastColumn).SetAutoFilter();
        worksheet.SheetView.FreezeRows(headerRow);
        worksheet.Range(headerRow, 1, finalRow, lastColumn).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        worksheet.Range(headerRow, 1, finalRow, lastColumn).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var measureThroughRow = Math.Min(finalRow, headerRow + 200);
        foreach (var column in worksheet.Columns(1, lastColumn))
        {
            column.AdjustToContents(1, measureThroughRow);
            column.Width = Math.Clamp(column.Width + 1, 10, 40);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    internal static string BoundText(string? value, int maximumLength)
    {
        var normalized = new string((value ?? "-")
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray())
            .Trim();
        if (normalized.Length == 0)
        {
            return "-";
        }

        return normalized.Length <= maximumLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, maximumLength - 1), "…");
    }

    private static void WriteValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                WriteText(cell, "");
                break;
            case string text:
                WriteText(cell, text);
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case DateTimeOffset dateTimeOffset:
                cell.Value = dateTimeOffset.UtcDateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                break;
            case int integer:
                cell.Value = integer;
                cell.Style.NumberFormat.Format = "0";
                break;
            case long longInteger:
                cell.Value = longInteger;
                cell.Style.NumberFormat.Format = "0";
                break;
            case decimal decimalNumber:
                cell.Value = decimalNumber;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double doubleNumber:
                cell.Value = doubleNumber;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case bool boolean:
                cell.Value = boolean;
                break;
            default:
                WriteText(cell, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                break;
        }
    }

    private static void WriteText(IXLCell cell, string value)
    {
        cell.SetValue(value);
        cell.Style.IncludeQuotePrefix = StartsLikeFormula(value);
    }

    private static bool StartsLikeFormula(string value)
    {
        return value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r';
    }
}
