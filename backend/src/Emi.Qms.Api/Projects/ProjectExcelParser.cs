using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Emi.Qms.Api.PanelInformation;

namespace Emi.Qms.Api.Projects;

public sealed class ProjectExcelParser
{
    public const long MaxExcelFileSizeBytes = 10 * 1024 * 1024;
    public const long MaxExcelMultipartRequestBytes = 11 * 1024 * 1024;
    public const int MaxExcelRows = 500;

    private static readonly IReadOnlyDictionary<string, string> HeaderAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["고객사"] = "customer",
        ["ITEM"] = "item",
        ["PJT CODE"] = "code",
        ["PJT TITLE"] = "title",
        ["프로젝트명"] = "title",
        ["패널 수"] = "panel_count",
        ["면수"] = "panel_count",
        ["납기일"] = "delivery_date",
        ["포장방식"] = "packaging",
        ["판매금액"] = "sales_amount",
        ["통화"] = "currency",
        ["납품장소"] = "delivery_location",
        ["FAT 필요 여부"] = "fat_required",
        ["FAT 필요"] = "fat_required",
        ["FAT"] = "fat_required",
        ["영업담당자"] = "sales_owner"
    };

    private readonly SemaphoreSlim parseSemaphore = new(2, 2);

    public async Task<ParsedProjectExcelFile> ParseAsync(UploadedExcelFile file, CancellationToken cancellationToken)
    {
        var errors = ValidateFile(file);
        if (errors.Count > 0)
        {
            return new ParsedProjectExcelFile(file.FileSha256, 0, [], errors);
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

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Projects");
        worksheet.Cell(1, 1).Value = "프로젝트 일괄 등록";
        worksheet.Range(1, 1, 1, 12).Merge().Style.Font.SetBold();
        worksheet.Cell(2, 1).Value = "* 표시 항목은 필수 입력값입니다.";
        worksheet.Range(2, 1, 2, 12).Merge().Style.Font.SetItalic();

        var headers = new (string Text, bool Required)[]
        {
            ("고객사", true),
            ("Item", true),
            ("PJT Code", true),
            ("프로젝트명", true),
            ("패널 수", true),
            ("납기일", true),
            ("포장방식", true),
            ("FAT 필요 여부", false),
            ("영업담당자", true),
            ("판매금액", false),
            ("통화", false),
            ("납품장소", false)
        };

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(3, index + 1).Value = headers[index].Required ? $"{headers[index].Text} *" : headers[index].Text;
            worksheet.Cell(3, index + 1).Style.Font.SetBold();
            if (headers[index].Required)
            {
                worksheet.Cell(3, index + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        ApplyTemplateLayout(worksheet, headers.Length);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void ApplyTemplateLayout(IXLWorksheet worksheet, int columnCount)
    {
        worksheet.SheetView.FreezeRows(3);
        worksheet.Range(3, 1, 3, columnCount).SetAutoFilter();
        worksheet.Columns(1, columnCount).AdjustToContents();
        for (var column = 1; column <= columnCount; column++)
        {
            var min = column switch
            {
                5 => 10,
                6 => 14,
                8 => 16,
                9 => 14,
                11 => 10,
                _ => 14
            };
            var max = column switch
            {
                4 => 34,
                12 => 32,
                _ => 24
            };
            worksheet.Column(column).Width = Math.Clamp(worksheet.Column(column).Width + 2, min, max);
        }
        worksheet.Columns(1, columnCount).Style.Alignment.WrapText = true;
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
            errors.Add("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
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
                throw new InvalidDataException("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
            }

            memoryStream.Write(buffer, 0, read);
        }

        var content = memoryStream.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new UploadedExcelFile(Path.GetFileName(file.FileName), file.Length, sha, content);
    }

    private static ParsedProjectExcelFile ParseCore(UploadedExcelFile file)
    {
        var zipErrors = ValidateZipPackage(file.Content);
        if (zipErrors.Count > 0)
        {
            return new ParsedProjectExcelFile(file.FileSha256, 0, [], zipErrors);
        }

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var workbook = new XLWorkbook(stream);
            var visibleWorksheets = workbook.Worksheets.Where(sheet => sheet.Visibility == XLWorksheetVisibility.Visible).ToList();
            if (visibleWorksheets.Count == 0)
            {
                return new ParsedProjectExcelFile(file.FileSha256, 0, [], ["표시된 Worksheet가 없습니다."]);
            }

            HeaderResult? selectedHeader = null;
            IXLWorksheet? selectedWorksheet = null;
            foreach (var worksheet in visibleWorksheets)
            {
                var header = FindHeader(worksheet);
                if (header.Errors.Count == 0)
                {
                    if (selectedHeader is not null)
                    {
                        return new ParsedProjectExcelFile(file.FileSha256, 0, [], ["인식 가능한 Header를 가진 Worksheet가 여러 개입니다."]);
                    }

                    selectedHeader = header;
                    selectedWorksheet = worksheet;
                }
            }

            if (selectedHeader is null || selectedWorksheet is null)
            {
                return new ParsedProjectExcelFile(file.FileSha256, 0, [], [FindHeader(visibleWorksheets[0]).Errors.FirstOrDefault() ?? "Header 행을 찾을 수 없습니다."]);
            }

            var lastRow = selectedWorksheet.LastRowUsed()?.RowNumber() ?? selectedHeader.RowNumber;
            var rows = new List<ParsedProjectExcelRow>();
            for (var rowNumber = selectedHeader.RowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                var rowErrors = new List<string>();
                var customer = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "customer", rowErrors);
                var item = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "item", rowErrors);
                var code = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "code", rowErrors);
                var title = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "title", rowErrors);
                var panelCount = ReadOptionalInt(selectedWorksheet, rowNumber, selectedHeader.Columns, "panel_count", rowErrors);
                var deliveryDate = ReadOptionalDate(selectedWorksheet, rowNumber, selectedHeader.Columns, "delivery_date", rowErrors);
                var packaging = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "packaging", rowErrors);
                var salesAmount = ReadOptionalDecimal(selectedWorksheet, rowNumber, selectedHeader.Columns, "sales_amount", rowErrors);
                var currency = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "currency", rowErrors);
                var deliveryLocation = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "delivery_location", rowErrors);
                var fatRequired = ReadOptionalBoolean(selectedWorksheet, rowNumber, selectedHeader.Columns, "fat_required", rowErrors);
                var salesOwner = ReadText(selectedWorksheet, rowNumber, selectedHeader.Columns, "sales_owner", rowErrors);

                var hasData = new[] { customer, item, code, title, packaging, currency, deliveryLocation, salesOwner }.Any(value => !string.IsNullOrWhiteSpace(value))
                    || panelCount is not null
                    || deliveryDate is not null
                    || salesAmount is not null
                    || fatRequired is not null;
                if (!hasData)
                {
                    continue;
                }

                rows.Add(new ParsedProjectExcelRow(
                    rowNumber,
                    customer,
                    item,
                    code,
                    title,
                    panelCount,
                    deliveryDate,
                    packaging,
                    salesAmount,
                    currency,
                    deliveryLocation,
                    fatRequired,
                    salesOwner,
                    rowErrors));
            }

            if (rows.Count > MaxExcelRows)
            {
                return new ParsedProjectExcelFile(file.FileSha256, rows.Count, rows, [$"Excel 데이터 행은 최대 {MaxExcelRows}행까지 허용됩니다."]);
            }

            return new ParsedProjectExcelFile(file.FileSha256, rows.Count, rows, []);
        }
        catch
        {
            return new ParsedProjectExcelFile(file.FileSha256, 0, [], ["올바른 .xlsx 파일을 읽을 수 없습니다."]);
        }
    }

    private static List<string> ValidateFile(UploadedExcelFile file)
    {
        var errors = new List<string>();
        if (!string.Equals(Path.GetExtension(file.OriginalFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(".xlsx 파일만 업로드할 수 있습니다.");
        }

        if (file.FileSizeBytes <= 0 || file.Content.Length == 0)
        {
            errors.Add("빈 Excel 파일은 업로드할 수 없습니다.");
        }

        if (file.FileSizeBytes > MaxExcelFileSizeBytes || file.Content.Length > MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
        }

        return errors;
    }

    private static List<string> ValidateZipPackage(byte[] content)
    {
        var errors = new List<string>();
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/externalLinks/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/oleObjects/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Macro, 외부 링크, OLE 개체가 포함된 Excel은 업로드할 수 없습니다.");
                    break;
                }
            }
        }
        catch
        {
            errors.Add("올바른 .xlsx ZIP 구조가 아닙니다.");
        }

        return errors;
    }

    private static HeaderResult FindHeader(IXLWorksheet worksheet)
    {
        for (var row = 1; row <= 20; row++)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCell = worksheet.Row(row).LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var column = 1; column <= lastCell; column++)
            {
                var normalized = NormalizeHeader(worksheet.Cell(row, column).GetString());
                if (HeaderAliases.TryGetValue(normalized, out var canonical))
                {
                    if (map.ContainsKey(canonical))
                    {
                        return new HeaderResult(row, map, [$"중복 Header가 있습니다: {canonical}"]);
                    }

                    map[canonical] = column;
                }
            }

            if (map.Count > 0)
            {
                var required = new[] { "customer", "item", "code", "title", "panel_count", "delivery_date", "packaging", "sales_owner" };
                var missing = required.Where(item => !map.ContainsKey(item)).ToList();
                if (missing.Count > 0)
                {
                    return new HeaderResult(row, map, [$"필수 Header가 없습니다: {string.Join(", ", missing)}"]);
                }

                return new HeaderResult(row, map, []);
            }
        }

        return new HeaderResult(0, new Dictionary<string, int>(), ["Header 행을 찾을 수 없습니다."]);
    }

    private static string NormalizeHeader(string value)
    {
        var trimmed = value.Trim().TrimEnd('*').Trim();
        return ProjectInputNormalizer.NormalizeProjectTitle(trimmed);
    }

    private static string? ReadText(IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns, string name, List<string> errors)
    {
        if (!columns.TryGetValue(name, out var column))
        {
            return null;
        }

        var cell = worksheet.Cell(row, column);
        if (cell.HasFormula)
        {
            errors.Add($"{name}에는 Formula를 사용할 수 없습니다.");
            return null;
        }

        return ProjectInputNormalizer.TrimToNull(cell.GetFormattedString());
    }

    private static int? ReadOptionalInt(IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns, string name, List<string> errors)
    {
        var text = ReadText(worksheet, row, columns, name, errors);
        if (text is null)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{name}은 정수여야 합니다.");
        return null;
    }

    private static decimal? ReadOptionalDecimal(IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns, string name, List<string> errors)
    {
        var text = ReadText(worksheet, row, columns, name, errors);
        if (text is null)
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{name}은 숫자여야 합니다.");
        return null;
    }

    private static DateOnly? ReadOptionalDate(IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns, string name, List<string> errors)
    {
        if (!columns.TryGetValue(name, out var column))
        {
            return null;
        }

        var cell = worksheet.Cell(row, column);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.HasFormula)
        {
            errors.Add($"{name}에는 Formula를 사용할 수 없습니다.");
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        var text = ProjectInputNormalizer.TrimToNull(cell.GetFormattedString());
        if (text is null)
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd", "yyyy.M.d", "yyyy/MM/dd", "yyyy.MM.dd" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{name} 날짜 형식을 확인할 수 없습니다.");
        return null;
    }

    private static bool? ReadOptionalBoolean(IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns, string name, List<string> errors)
    {
        var text = ReadText(worksheet, row, columns, name, errors);
        if (text is null)
        {
            return null;
        }

        var normalized = ProjectInputNormalizer.NormalizeProjectTitle(text);
        if (normalized is "Y" or "YES" or "TRUE" or "1" or "예" or "필요")
        {
            return true;
        }

        if (normalized is "N" or "NO" or "FALSE" or "0" or "아니오" or "불필요")
        {
            return false;
        }

        errors.Add($"{name} 값은 예/아니오, Y/N, TRUE/FALSE만 사용할 수 있습니다.");
        return null;
    }

    private sealed record HeaderResult(
        int RowNumber,
        IReadOnlyDictionary<string, int> Columns,
        IReadOnlyList<string> Errors);
}

public sealed record ParsedProjectExcelFile(
    string FileSha256,
    int TotalRows,
    IReadOnlyList<ParsedProjectExcelRow> Rows,
    IReadOnlyList<string> FileErrors);

public sealed record ParsedProjectExcelRow(
    int ExcelRowNumber,
    string? CustomerName,
    string? Item,
    string? ProjectCode,
    string? ProjectTitle,
    int? PanelCount,
    DateOnly? DeliveryDate,
    string? PackagingMethod,
    decimal? SalesAmount,
    string? CurrencyCode,
    string? DeliveryLocation,
    bool? FatRequired,
    string? SalesOwnerText,
    IReadOnlyList<string> ErrorMessages);
