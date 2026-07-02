using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Emi.Qms.Api.PanelInformation;

namespace Emi.Qms.Api.Procurement;

public sealed class ProcurementExcelParser
{
    private static readonly IReadOnlyDictionary<string, string> HeaderAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["PJT"] = "pjt",
        ["PJT CODE"] = "pjt code",
        ["통상납기"] = "통상납기",
        ["발주품목"] = "발주품목",
        ["업체"] = "업체",
        ["기술 담당자"] = "기술 담당자",
        ["발주일"] = "발주일",
        ["입고일"] = "입고일",
        ["입고예정일"] = "입고일",
        ["출하일"] = "납품예정일",
        ["납품예정일"] = "납품예정일",
        ["이슈사항"] = "이슈사항",
        ["입고 완료"] = "입고 완료"
    };

    private readonly SemaphoreSlim parseSemaphore;

    public ProcurementExcelParser()
        : this(new SemaphoreSlim(2, 2))
    {
    }

    internal ProcurementExcelParser(SemaphoreSlim parseSemaphore)
    {
        this.parseSemaphore = parseSemaphore;
    }

    internal async Task<ParsedProcurementExcelFile> ParseAsync(UploadedExcelFile file, CancellationToken cancellationToken)
    {
        var errors = ValidateFile(file);
        if (errors.Count > 0)
        {
            return new ParsedProcurementExcelFile(file.FileSha256, 0, [], errors);
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

        if (file.Length > ProcurementDomain.MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
        }

        return errors;
    }

    public static async Task<UploadedExcelFile> ReadUploadedFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream(capacity: (int)Math.Min(file.Length, ProcurementDomain.MaxExcelFileSizeBytes));
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
            if (totalRead > ProcurementDomain.MaxExcelFileSizeBytes)
            {
                throw new InvalidDataException("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
            }

            memoryStream.Write(buffer, 0, read);
        }

        var content = memoryStream.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new UploadedExcelFile(Path.GetFileName(file.FileName), file.Length, sha, content);
    }

    private static ParsedProcurementExcelFile ParseCore(UploadedExcelFile file)
    {
        var zipErrors = ValidateZipPackage(file.Content);
        if (zipErrors.Count > 0)
        {
            return new ParsedProcurementExcelFile(file.FileSha256, 0, [], zipErrors);
        }

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault(sheet => sheet.Visibility == XLWorksheetVisibility.Visible);
            if (worksheet is null)
            {
                return new ParsedProcurementExcelFile(file.FileSha256, 0, [], ["표시된 Worksheet가 없습니다."]);
            }

            var header = FindHeader(worksheet);
            if (header.Errors.Count > 0)
            {
                return new ParsedProcurementExcelFile(file.FileSha256, 0, [], header.Errors);
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;
            var rows = new List<ParsedProcurementExcelRow>();
            string? currentProject = null;
            string? currentCode = null;
            var sourceGroupSequence = 0;

            for (var rowNumber = header.RowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                var rowErrors = new List<string>();
                var pjt = ReadText(worksheet, rowNumber, header.Columns, "pjt", rowErrors);
                var pjtCode = ReadText(worksheet, rowNumber, header.Columns, "pjt code", rowErrors);
                var standardLeadTime = ReadText(worksheet, rowNumber, header.Columns, "통상납기", rowErrors);
                var orderItem = ReadText(worksheet, rowNumber, header.Columns, "발주품목", rowErrors);
                var supplierName = ReadText(worksheet, rowNumber, header.Columns, "업체", rowErrors);
                var owner = ReadText(worksheet, rowNumber, header.Columns, "기술 담당자", rowErrors);
                var shipmentText = ReadText(worksheet, rowNumber, header.Columns, "납품예정일", rowErrors);
                var issue = ReadText(worksheet, rowNumber, header.Columns, "이슈사항", rowErrors);
                var receiptRaw = ReadText(worksheet, rowNumber, header.Columns, "입고 완료", rowErrors);
                var orderDate = ReadOptionalDate(worksheet, rowNumber, header.Columns, "발주일", rowErrors);
                var expectedReceiptDate = ReadOptionalDate(worksheet, rowNumber, header.Columns, "입고일", rowErrors);

                if (!string.IsNullOrWhiteSpace(pjt))
                {
                    currentProject = ProcurementDomain.TrimToNull(pjt);
                    currentCode = ProcurementDomain.TrimToNull(pjtCode);
                    sourceGroupSequence++;
                }

                var hasData = new[]
                {
                    pjt, pjtCode, standardLeadTime, orderItem, supplierName, owner, shipmentText, issue, receiptRaw
                }.Any(value => !string.IsNullOrWhiteSpace(value)) || orderDate is not null || expectedReceiptDate is not null;

                if (!hasData)
                {
                    rows.Add(new ParsedProcurementExcelRow(
                        rowNumber,
                        Math.Max(sourceGroupSequence, 0),
                        currentProject,
                        currentCode,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        IsSkipped: true,
                        []));
                    continue;
                }

                if (sourceGroupSequence == 0 || string.IsNullOrWhiteSpace(currentProject))
                {
                    rowErrors.Add("PJT 그룹을 확인할 수 없습니다.");
                }

                bool? receiptCompleted = null;
                if (!ProcurementDomain.TryParseReceiptCompleted(receiptRaw, out receiptCompleted))
                {
                    rowErrors.Add("입고 완료 값은 Y/YES/TRUE/1/완료 또는 N/NO/FALSE/0/미완료만 사용할 수 있습니다.");
                }

                rows.Add(new ParsedProcurementExcelRow(
                    rowNumber,
                    sourceGroupSequence,
                    currentProject,
                    currentCode,
                    ProcurementDomain.TrimToNull(standardLeadTime),
                    ProcurementDomain.TrimToNull(orderItem),
                    ProcurementDomain.TrimToNull(supplierName),
                    ProcurementDomain.TrimToNull(owner),
                    orderDate,
                    expectedReceiptDate,
                    ProcurementDomain.TrimToNull(shipmentText),
                    ProcurementDomain.TrimToNull(issue),
                    receiptCompleted,
                    IsSkipped: false,
                    rowErrors));
            }

            if (rows.Count > ProcurementDomain.MaxExcelRows)
            {
                return new ParsedProcurementExcelFile(file.FileSha256, rows.Count, rows, [$"Excel 데이터 행은 최대 {ProcurementDomain.MaxExcelRows}행까지 허용됩니다."]);
            }

            return new ParsedProcurementExcelFile(file.FileSha256, rows.Count, rows, []);
        }
        catch
        {
            return new ParsedProcurementExcelFile(file.FileSha256, 0, [], ["올바른 .xlsx 파일을 읽을 수 없습니다."]);
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

        if (file.FileSizeBytes > ProcurementDomain.MaxExcelFileSizeBytes || file.Content.Length > ProcurementDomain.MaxExcelFileSizeBytes)
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
        for (var row = 1; row <= 10; row++)
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
                var required = new[] { "pjt", "pjt code", "통상납기", "발주품목", "기술 담당자", "발주일", "입고일", "이슈사항", "입고 완료" };
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
        return ProcurementDomain.NormalizeProjectKey(value.Trim().TrimEnd('*').Trim());
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

        return ProcurementDomain.TrimToNull(cell.GetFormattedString());
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

        var text = ProcurementDomain.TrimToNull(cell.GetFormattedString());
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

    private sealed record HeaderResult(
        int RowNumber,
        IReadOnlyDictionary<string, int> Columns,
        IReadOnlyList<string> Errors);
}
