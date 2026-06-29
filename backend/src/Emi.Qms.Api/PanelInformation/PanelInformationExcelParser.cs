using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.PanelInformation;

public sealed partial class PanelInformationExcelParser
{
    private readonly SemaphoreSlim parseSemaphore;

    public PanelInformationExcelParser()
        : this(new SemaphoreSlim(2, 2))
    {
    }

    internal PanelInformationExcelParser(SemaphoreSlim parseSemaphore)
    {
        this.parseSemaphore = parseSemaphore;
    }

    public async Task<ParsedPanelInformationExcelFile> ParseAsync(UploadedExcelFile file, CancellationToken cancellationToken)
    {
        var fileErrors = ValidateFile(file);
        if (fileErrors.Count > 0)
        {
            return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], [], fileErrors);
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

        if (file.Length > PanelInformationDomain.MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
        }

        return errors;
    }

    public static async Task<UploadedExcelFile> ReadUploadedFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream(capacity: (int)Math.Min(file.Length, PanelInformationDomain.MaxExcelFileSizeBytes));
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
            if (totalRead > PanelInformationDomain.MaxExcelFileSizeBytes)
            {
                throw new InvalidDataException("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
            }

            memoryStream.Write(buffer, 0, read);
        }

        var content = memoryStream.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new UploadedExcelFile(Path.GetFileName(file.FileName), file.Length, sha, content);
    }

    private static ParsedPanelInformationExcelFile ParseCore(UploadedExcelFile file)
    {
        var zipErrors = ValidateZipPackage(file.Content);
        if (zipErrors.Count > 0)
        {
            return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], [], zipErrors);
        }

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var workbook = new XLWorkbook(stream);
            var workbookErrors = ValidateWorkbook(workbook);
            if (workbookErrors.Count > 0)
            {
                return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], [], workbookErrors);
            }

            var worksheetResult = SelectWorksheet(workbook);
            if (worksheetResult.Errors.Count > 0 || worksheetResult.Worksheet is null || worksheetResult.Headers is null)
            {
                return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], worksheetResult.Headers?.Headers.Keys.ToList() ?? [], worksheetResult.Errors);
            }

            var worksheet = worksheetResult.Worksheet;
            var headerResult = worksheetResult.Headers;
            var rangeErrors = ValidateWorksheetRange(worksheet, headerResult.HeaderRowNumber);
            if (rangeErrors.Count > 0)
            {
                return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], headerResult.Headers.Keys.ToList(), rangeErrors);
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerResult.HeaderRowNumber;
            var rows = new List<ParsedPanelInformationExcelRow>();
            for (var rowNumber = headerResult.HeaderRowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                if (IsRecognizedRowEmpty(worksheet, rowNumber, headerResult.Headers))
                {
                    continue;
                }

                var rowErrors = new List<string>();
                var noText = ReadCellText(worksheet.Cell(rowNumber, headerResult.Headers["no"]), rowErrors, "No");
                var panelName = ReadCellText(worksheet.Cell(rowNumber, headerResult.Headers["panel name"]), rowErrors, "panel name");
                var width = ReadOptionalDecimal(worksheet, rowNumber, headerResult.Headers, "w", rowErrors);
                var height = ReadOptionalDecimal(worksheet, rowNumber, headerResult.Headers, "h", rowErrors);
                var depth = ReadOptionalDecimal(worksheet, rowNumber, headerResult.Headers, "d", rowErrors);
                int? sequenceNumber = null;

                if (string.IsNullOrWhiteSpace(noText))
                {
                    rowErrors.Add("No는 필수값입니다.");
                }
                else if (!int.TryParse(noText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNo) || parsedNo < 1)
                {
                    rowErrors.Add("No는 1 이상의 정수여야 합니다.");
                }
                else
                {
                    sequenceNumber = parsedNo;
                }

                rows.Add(new ParsedPanelInformationExcelRow(
                    rowNumber,
                    sequenceNumber,
                    ProjectInputNormalizer.TrimToNull(panelName),
                    width,
                    height,
                    depth,
                    rowErrors));
            }

            if (rows.Count > PanelInformationDomain.MaxExcelRows)
            {
                return new ParsedPanelInformationExcelFile(file.FileSha256, rows.Count, rows, headerResult.Headers.Keys.ToList(), [$"Excel 데이터 행은 최대 {PanelInformationDomain.MaxExcelRows}행까지 허용됩니다."]);
            }

            var duplicateNos = rows
                .Where(row => row.No is not null)
                .GroupBy(row => row.No!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet();
            if (duplicateNos.Count > 0)
            {
                rows = rows
                    .Select(row => row.No is not null && duplicateNos.Contains(row.No.Value)
                        ? row with { ErrorMessages = [.. row.ErrorMessages, "파일 안에서 No가 중복되었습니다."] }
                        : row)
                    .ToList();
            }

            return new ParsedPanelInformationExcelFile(file.FileSha256, rows.Count, rows, headerResult.Headers.Keys.ToList(), []);
        }
        catch
        {
            return new ParsedPanelInformationExcelFile(file.FileSha256, 0, [], [], ["올바른 .xlsx 파일을 읽을 수 없습니다."]);
        }
    }

    private static IReadOnlyList<string> ValidateFile(UploadedExcelFile file)
    {
        var errors = new List<string>();
        var extension = Path.GetExtension(file.OriginalFileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(".xlsx 파일만 업로드할 수 있습니다.");
        }

        if (file.FileSizeBytes > PanelInformationDomain.MaxExcelFileSizeBytes || file.Content.Length > PanelInformationDomain.MaxExcelFileSizeBytes)
        {
            errors.Add("Excel 파일은 10MB 이하만 업로드할 수 있습니다.");
        }

        if (file.FileSizeBytes == 0 || file.Content.Length == 0)
        {
            errors.Add("빈 Excel 파일은 업로드할 수 없습니다.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateZipPackage(byte[] content)
    {
        var errors = new List<string>();
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (archive.Entries.Count > PanelInformationDomain.MaxExcelZipEntries)
            {
                errors.Add($"Excel ZIP entry는 최대 {PanelInformationDomain.MaxExcelZipEntries}개까지 허용됩니다.");
            }

            long totalUncompressed = 0;
            var worksheetCount = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                totalUncompressed += entry.Length;
                if (entry.Length > PanelInformationDomain.MaxExcelEntryUncompressedBytes)
                {
                    errors.Add("Excel ZIP entry가 허용 크기를 초과했습니다.");
                }

                if (name.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    worksheetCount++;
                }

                if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/externalLinks/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/oleObjects/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Macro, 외부 링크, OLE 개체가 포함된 Excel은 업로드할 수 없습니다.");
                }
            }

            if (totalUncompressed > PanelInformationDomain.MaxExcelUncompressedBytes)
            {
                errors.Add("Excel 압축 해제 예상 크기가 허용값을 초과했습니다.");
            }

            if (worksheetCount > PanelInformationDomain.MaxExcelWorksheets)
            {
                errors.Add($"Worksheet는 최대 {PanelInformationDomain.MaxExcelWorksheets}개까지 허용됩니다.");
            }
        }
        catch (InvalidDataException)
        {
            errors.Add("올바른 .xlsx ZIP 구조가 아닙니다.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateWorkbook(XLWorkbook workbook)
    {
        var errors = new List<string>();
        if (workbook.Worksheets.Count > PanelInformationDomain.MaxExcelWorksheets)
        {
            errors.Add($"Worksheet는 최대 {PanelInformationDomain.MaxExcelWorksheets}개까지 허용됩니다.");
        }

        if (!workbook.Worksheets.Any(worksheet => worksheet.Visibility == XLWorksheetVisibility.Visible))
        {
            errors.Add("표시 상태의 워크시트가 없습니다.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateWorksheetRange(IXLWorksheet worksheet, int headerRowNumber)
    {
        var errors = new List<string>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow - headerRowNumber > PanelInformationDomain.MaxExcelRows)
        {
            errors.Add($"Excel 데이터 행은 최대 {PanelInformationDomain.MaxExcelRows}행까지 허용됩니다.");
        }

        if (lastColumn > PanelInformationDomain.MaxExcelColumns)
        {
            errors.Add($"Excel 사용 열은 최대 {PanelInformationDomain.MaxExcelColumns}열까지 허용됩니다.");
        }

        var usedCellCount = worksheet.CellsUsed().Count();
        if (usedCellCount > PanelInformationDomain.MaxExcelCells)
        {
            errors.Add($"Excel 사용 셀은 최대 {PanelInformationDomain.MaxExcelCells}개까지 허용됩니다.");
        }

        if (worksheet.CellsUsed().Any(cell => cell.HasFormula))
        {
            errors.Add("Excel Formula는 사용할 수 없습니다.");
        }

        return errors;
    }

    private static WorksheetSelectionResult SelectWorksheet(XLWorkbook workbook)
    {
        var visibleWorksheets = workbook.Worksheets
            .Where(worksheet => worksheet.Visibility == XLWorksheetVisibility.Visible)
            .ToList();
        if (visibleWorksheets.Count == 0)
        {
            return new WorksheetSelectionResult(null, null, ["표시 상태의 워크시트가 없습니다."]);
        }

        var namedMatches = visibleWorksheets
            .Where(worksheet => string.Equals(worksheet.Name, "Panel Information", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (namedMatches.Count > 1)
        {
            return new WorksheetSelectionResult(null, null, ["Panel Information 워크시트가 여러 개입니다."]);
        }

        if (namedMatches.Count == 1)
        {
            var headers = ReadHeaders(namedMatches[0]);
            var errors = ValidateHeaders(headers);
            return errors.Count > 0
                ? new WorksheetSelectionResult(namedMatches[0], headers, errors)
                : new WorksheetSelectionResult(namedMatches[0], headers, []);
        }

        var candidates = new List<(IXLWorksheet Worksheet, HeaderReadResult Headers)>();
        foreach (var worksheet in visibleWorksheets)
        {
            var headers = ReadHeaders(worksheet);
            if (headers.Headers.ContainsKey("no") && headers.Headers.ContainsKey("panel name"))
            {
                candidates.Add((worksheet, headers));
            }
        }

        return candidates.Count switch
        {
            0 => new WorksheetSelectionResult(null, null, ["20행 안에서 No와 panel name 헤더가 있는 워크시트를 찾을 수 없습니다."]),
            1 => ValidateHeaders(candidates[0].Headers) is var errors && errors.Count > 0
                ? new WorksheetSelectionResult(candidates[0].Worksheet, candidates[0].Headers, errors)
                : new WorksheetSelectionResult(candidates[0].Worksheet, candidates[0].Headers, []),
            _ => new WorksheetSelectionResult(null, null, ["패널정보로 인식 가능한 워크시트가 여러 개입니다."])
        };
    }

    private static HeaderReadResult ReadHeaders(IXLWorksheet worksheet)
    {
        var firstRow = FindHeaderRow(worksheet);
        if (firstRow is null)
        {
            return new HeaderReadResult(1, new Dictionary<string, int>(StringComparer.Ordinal), [], ["20행 안에서 Header Row를 찾을 수 없습니다."]);
        }

        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        var errors = new List<string>();
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var cell = worksheet.Cell(firstRow.Value, column);
            var normalized = NormalizeHeader(cell.GetString());
            if (normalized is "no" or "도번" or "panel name" or "w" or "h" or "d")
            {
                if (cell.IsMerged())
                {
                    errors.Add($"{normalized} 헤더는 병합 셀을 사용할 수 없습니다.");
                }

                if (!headers.TryAdd(normalized, column))
                {
                    duplicates.Add($"{normalized}({ColumnLetter(headers[normalized])}, {ColumnLetter(column)})");
                }
            }
        }

        foreach (var duplicate in duplicates)
        {
            errors.Add($"중복 Header가 있습니다: {duplicate}");
        }

        return new HeaderReadResult(firstRow.Value, headers, headers.Keys.ToList(), errors);
    }

    private static IReadOnlyList<string> ValidateHeaders(HeaderReadResult headers)
    {
        var errors = headers.Errors.ToList();
        if (!headers.Headers.ContainsKey("no"))
        {
            errors.Add("No 헤더가 필요합니다.");
        }

        if (!headers.Headers.ContainsKey("panel name"))
        {
            errors.Add("panel name 헤더가 필요합니다.");
        }

        return errors;
    }

    private static int? FindHeaderRow(IXLWorksheet worksheet)
    {
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var maxRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, PanelInformationDomain.MaxExcelHeaderSearchRows);
        for (var row = 1; row <= maxRow; row++)
        {
            var hasRecognizedHeader = false;
            for (var column = 1; column <= lastColumn; column++)
            {
                if (NormalizeHeader(worksheet.Cell(row, column).GetString()) is "no" or "도번" or "panel name" or "w" or "h" or "d")
                {
                    hasRecognizedHeader = true;
                    break;
                }
            }

            if (hasRecognizedHeader)
            {
                return row;
            }
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        return WhitespaceRegex().Replace(value.Trim().TrimEnd('*').Trim(), " ").ToLowerInvariant();
    }

    private static bool IsRecognizedRowEmpty(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headerMap)
    {
        foreach (var column in headerMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.Cell(rowNumber, column).GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ReadCellText(IXLCell cell, List<string> errors, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(cell.FormulaA1))
        {
            errors.Add($"{fieldName} 셀에는 Formula를 사용할 수 없습니다.");
            return null;
        }

        return cell.GetString();
    }

    private static decimal? ReadOptionalDecimal(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> headerMap,
        string header,
        List<string> errors)
    {
        if (!headerMap.TryGetValue(header, out var column))
        {
            return null;
        }

        var text = ReadCellText(worksheet.Cell(rowNumber, column), errors, header);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add($"{header} 값은 숫자여야 합니다.");
            return null;
        }

        return value;
    }

    private static string ColumnLetter(int column)
    {
        return XLHelper.GetColumnLetterFromNumber(column);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record WorksheetSelectionResult(
        IXLWorksheet? Worksheet,
        HeaderReadResult? Headers,
        IReadOnlyList<string> Errors);

    private sealed record HeaderReadResult(
        int HeaderRowNumber,
        IReadOnlyDictionary<string, int> Headers,
        IReadOnlyList<string> RecognizedHeaders,
        IReadOnlyList<string> Errors);
}

public sealed record ParsedPanelInformationExcelFile(
    string FileSha256,
    int TotalRows,
    IReadOnlyList<ParsedPanelInformationExcelRow> Rows,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string> FileErrors);

public sealed record ParsedPanelInformationExcelRow(
    int ExcelRowNumber,
    int? No,
    string? PanelName,
    decimal? Width,
    decimal? Height,
    decimal? Depth,
    IReadOnlyList<string> ErrorMessages);
