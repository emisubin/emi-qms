using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Emi.Qms.Api.PanelInformation;

namespace Emi.Qms.Api.Calendar;

public sealed class CalendarHolidayExcelParser
{
    public const long MaxExcelFileSizeBytes = 5 * 1024 * 1024;
    public const long MaxExcelMultipartRequestBytes = 6 * 1024 * 1024;
    public const int MaxExcelRows = 400;

    private static readonly IReadOnlyDictionary<string, string> HeaderAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["날짜"] = "date",
        ["휴일명"] = "name",
        ["휴일유형"] = "holiday_type",
        ["비고"] = "note"
    };

    private readonly SemaphoreSlim parseSemaphore = new(2, 2);

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Holidays");
        worksheet.Cell(1, 1).Value = "EMI 프로젝트 통합관리시스템 휴일 일괄 등록";
        worksheet.Range(1, 1, 1, 4).Merge().Style.Font.SetBold();
        worksheet.Cell(2, 1).Value = "휴일유형은 National, Substitute, Temporary, Company 중 하나를 입력합니다.";
        worksheet.Range(2, 1, 2, 4).Merge().Style.Font.SetItalic();

        var headers = new[] { "날짜 *", "휴일명 *", "휴일유형 *", "비고" };
        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(3, index + 1).Value = headers[index];
            worksheet.Cell(3, index + 1).Style.Font.SetBold();
            if (headers[index].Contains('*', StringComparison.Ordinal))
            {
                worksheet.Cell(3, index + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        worksheet.Cell(4, 1).Value = new DateTime(2026, 1, 1);
        worksheet.Cell(4, 1).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Cell(4, 2).Value = "신정";
        worksheet.Cell(4, 3).Value = SystemHolidayTypes.National;
        worksheet.Cell(4, 4).Value = "예시 행입니다. 실제 업로드 전 수정해 주세요.";
        worksheet.SheetView.FreezeRows(3);
        worksheet.Range(3, 1, 3, 4).SetAutoFilter();
        worksheet.Columns(1, 4).AdjustToContents();
        worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 14);
        worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 24);
        worksheet.Column(3).Width = Math.Max(worksheet.Column(3).Width, 16);
        worksheet.Column(4).Width = Math.Max(worksheet.Column(4).Width, 36);
        worksheet.Columns(1, 4).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ParsedCalendarHolidayExcelFile> ParseAsync(UploadedExcelFile file, CancellationToken cancellationToken)
    {
        var fileErrors = ValidateFile(file);
        if (fileErrors.Count > 0)
        {
            return new ParsedCalendarHolidayExcelFile(file.FileSha256, 0, [], fileErrors);
        }

        await parseSemaphore.WaitAsync(cancellationToken);
        try
        {
            return ParseCore(file);
        }
        finally
        {
            parseSemaphore.Release();
        }
    }

    public static IReadOnlyList<string> ValidateUploadMetadata(IFormFile file)
    {
        var errors = new List<string>();
        var originalFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName)
            || !string.Equals(Path.GetExtension(originalFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(".xlsx 파일만 업로드할 수 있습니다.");
        }

        if (file.Length <= 0)
        {
            errors.Add("빈 Excel 파일은 업로드할 수 없습니다.");
        }

        if (file.Length > MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 5MB 이하만 업로드할 수 있습니다.");
        }

        return errors;
    }

    public static async Task<UploadedExcelFile> ReadUploadedFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream(capacity: (int)Math.Min(file.Length, MaxExcelFileSizeBytes));
        var buffer = new byte[81920];
        long totalRead = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > MaxExcelFileSizeBytes)
            {
                throw new InvalidDataException("Excel 파일은 5MB 이하만 업로드할 수 있습니다.");
            }

            memoryStream.Write(buffer, 0, read);
        }

        var content = memoryStream.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new UploadedExcelFile(Path.GetFileName(file.FileName), file.Length, sha, content);
    }

    private static ParsedCalendarHolidayExcelFile ParseCore(UploadedExcelFile file)
    {
        var zipErrors = ValidateZipPackage(file.Content);
        if (zipErrors.Count > 0)
        {
            return new ParsedCalendarHolidayExcelFile(file.FileSha256, 0, [], zipErrors);
        }

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault(sheet => sheet.Visibility == XLWorksheetVisibility.Visible);
            if (worksheet is null)
            {
                return new ParsedCalendarHolidayExcelFile(file.FileSha256, 0, [], ["표시된 Worksheet가 없습니다."]);
            }

            var header = FindHeader(worksheet);
            if (header.Errors.Count > 0)
            {
                return new ParsedCalendarHolidayExcelFile(file.FileSha256, 0, [], header.Errors);
            }

            var rows = new List<ParsedCalendarHolidayExcelRow>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;
            for (var rowNumber = header.RowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                var rowErrors = new List<string>();
                var date = ReadDate(worksheet.Cell(rowNumber, header.Columns["date"]), "날짜", rowErrors);
                var name = TrimToNull(ReadText(worksheet.Cell(rowNumber, header.Columns["name"]), "휴일명", rowErrors));
                var holidayTypeRaw = TrimToNull(ReadText(worksheet.Cell(rowNumber, header.Columns["holiday_type"]), "휴일유형", rowErrors));
                var note = header.Columns.TryGetValue("note", out var noteColumn)
                    ? TrimToNull(ReadText(worksheet.Cell(rowNumber, noteColumn), "비고", rowErrors))
                    : null;

                if (date is null && name is null && holidayTypeRaw is null && note is null)
                {
                    continue;
                }

                if (date is null)
                {
                    rowErrors.Add("날짜는 필수입니다.");
                }

                if (name is null)
                {
                    rowErrors.Add("휴일명은 필수입니다.");
                }

                string? holidayType = null;
                if (!SystemHolidayTypes.TryNormalize(holidayTypeRaw, out var normalizedType))
                {
                    rowErrors.Add("휴일유형은 National, Substitute, Temporary, Company 중 하나여야 합니다.");
                }
                else
                {
                    holidayType = normalizedType;
                }

                if (date is not null && holidayType is not null)
                {
                    var key = $"{date:yyyy-MM-dd}|{holidayType}";
                    if (!seenKeys.Add(key))
                    {
                        rowErrors.Add("같은 파일 안에 같은 날짜와 휴일유형이 중복되었습니다.");
                    }
                }

                rows.Add(new ParsedCalendarHolidayExcelRow(rowNumber, date, name, holidayType, note, rowErrors));
            }

            if (rows.Count > MaxExcelRows)
            {
                return new ParsedCalendarHolidayExcelFile(file.FileSha256, rows.Count, rows, [$"Excel 데이터 행은 최대 {MaxExcelRows}행까지 허용됩니다."]);
            }

            return new ParsedCalendarHolidayExcelFile(file.FileSha256, rows.Count, rows, []);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException)
        {
            return new ParsedCalendarHolidayExcelFile(file.FileSha256, 0, [], ["올바른 .xlsx 파일을 읽을 수 없습니다."]);
        }
    }

    private static IReadOnlyList<string> ValidateFile(UploadedExcelFile file)
    {
        var errors = new List<string>();
        if (file.Content.Length == 0 || file.FileSizeBytes <= 0)
        {
            errors.Add("빈 Excel 파일은 업로드할 수 없습니다.");
        }

        if (file.FileSizeBytes > MaxExcelFileSizeBytes || file.Content.Length > MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 5MB 이하만 업로드할 수 있습니다.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateZipPackage(byte[] content)
    {
        var errors = new List<string>();
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if (name.Equals("xl/vbaProject.bin", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Macro, 외부 링크, OLE 개체가 포함된 Excel은 업로드할 수 없습니다.");
                    break;
                }
            }
        }
        catch (InvalidDataException)
        {
            errors.Add("올바른 .xlsx 파일을 읽을 수 없습니다.");
        }

        return errors;
    }

    private static HeaderResult FindHeader(IXLWorksheet worksheet)
    {
        for (var row = 1; row <= Math.Min(20, worksheet.LastRowUsed()?.RowNumber() ?? 1); row++)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column <= Math.Min(16, worksheet.LastColumnUsed()?.ColumnNumber() ?? 1); column++)
            {
                var headerText = NormalizeHeader(worksheet.Cell(row, column).GetString());
                if (HeaderAliases.TryGetValue(headerText, out var key))
                {
                    columns[key] = column;
                }
            }

            var missing = new[] { "date", "name", "holiday_type" }
                .Where(key => !columns.ContainsKey(key))
                .ToArray();
            if (missing.Length == 0)
            {
                return new HeaderResult(row, columns, []);
            }
        }

        return new HeaderResult(0, new Dictionary<string, int>(), ["날짜, 휴일명, 휴일유형 Header를 찾을 수 없습니다."]);
    }

    private static string NormalizeHeader(string value)
    {
        return value.Replace("*", "", StringComparison.Ordinal).Trim();
    }

    private static string? ReadText(IXLCell cell, string name, List<string> errors)
    {
        if (cell.HasFormula)
        {
            errors.Add($"{name}에는 Formula를 사용할 수 없습니다.");
            return null;
        }

        return cell.GetFormattedString().Trim();
    }

    private static DateOnly? ReadDate(IXLCell cell, string name, List<string> errors)
    {
        if (cell.HasFormula)
        {
            errors.Add($"{name}에는 Formula를 사용할 수 없습니다.");
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        var text = TrimToNull(cell.GetFormattedString());
        if (text is null)
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd", "yyyy.M.d", "yyyy/M/d", "yyyy.MM.dd", "yyyy/MM/dd" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{name} 날짜 형식을 확인할 수 없습니다.");
        return null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record HeaderResult(
        int RowNumber,
        IReadOnlyDictionary<string, int> Columns,
        IReadOnlyList<string> Errors);
}

public sealed record ParsedCalendarHolidayExcelFile(
    string FileSha256,
    int TotalRows,
    IReadOnlyList<ParsedCalendarHolidayExcelRow> Rows,
    IReadOnlyList<string> FileErrors);

public sealed record ParsedCalendarHolidayExcelRow(
    int ExcelRowNumber,
    DateOnly? Date,
    string? Name,
    string? HolidayType,
    string? Note,
    IReadOnlyList<string> ErrorMessages);
