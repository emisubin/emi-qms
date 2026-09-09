using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ClosedXML.Excel;
using Emi.Qms.Api.PanelInformation;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanProjectExcelParser
{
    public const long MaximumFileBytes = 5 * 1024 * 1024;
    public const long MaximumMultipartBytes = 6 * 1024 * 1024;
    public const int MaximumRows = 100;
    public const int MaximumTotalQuantity = 1000;

    private const int MaximumZipEntries = 2000;
    private const long MaximumUncompressedBytes = 50 * 1024 * 1024;
    private const long MaximumEntryBytes = 20 * 1024 * 1024;
    private const int MaximumWorksheets = 20;
    private const int MaximumColumns = 64;
    private const int MaximumCells = 10000;
    private const int MaximumHeaderSearchRows = 20;

    private static readonly IReadOnlyDictionary<string, string> HeaderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["프로젝트명"] = "title",
            ["프로젝트 title"] = "title",
            ["title"] = "title",
            ["프로젝트 코드"] = "project_code",
            ["project code"] = "project_code",
            ["pjt code"] = "project_code",
            ["거래처"] = "customer_name",
            ["고객사"] = "customer_name",
            ["customer"] = "customer_name",
            ["po"] = "po_number",
            ["po no"] = "po_number",
            ["po 번호"] = "po_number",
            ["w/o"] = "work_order_number",
            ["w/o no"] = "work_order_number",
            ["wo"] = "work_order_number",
            ["작업지시번호"] = "work_order_number",
            ["납기일"] = "delivery_date",
            ["delivery date"] = "delivery_date",
            ["제품명"] = "product_name",
            ["product"] = "product_name",
            ["수량"] = "quantity",
            ["quantity"] = "quantity"
        };

    private static readonly SemaphoreSlim ParseSemaphore = new(2, 2);

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Osan Projects");
        worksheet.Cell(1, 1).Value = "오산 프로젝트 일괄 등록";
        worksheet.Range(1, 1, 1, 8).Merge().Style.Font.SetBold();
        worksheet.Row(1).Height = 24;
        worksheet.Cell(2, 1).Value =
            "* 필수. 한 행에 프로젝트 한 건, 최대 100행/전체 수량 1,000개. 수량은 프로젝트별 1~500, 납기는 YYYY-MM-DD. 코드·PO·W/O 열은 텍스트로 입력하면 앞자리 0이 보존됩니다.";
        worksheet.Range(2, 1, 2, 8).Merge().Style.Font.SetItalic();
        worksheet.Range(2, 1, 2, 8).Style.Alignment.WrapText = true;
        worksheet.Row(2).Height = 42;

        var headers = new (string Text, bool Required)[]
        {
            ("프로젝트 Title", true),
            ("프로젝트 코드", true),
            ("거래처", true),
            ("PO No", false),
            ("W/O No", false),
            ("납기일", true),
            ("제품명", true),
            ("수량", true)
        };
        for (var index = 0; index < headers.Length; index++)
        {
            var cell = worksheet.Cell(3, index + 1);
            cell.Value = headers[index].Required ? $"{headers[index].Text} *" : headers[index].Text;
            cell.Style.Font.SetBold();
            if (headers[index].Required)
            {
                cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        worksheet.SheetView.FreezeRows(3);
        worksheet.Range(3, 1, 3, headers.Length).SetAutoFilter();
        worksheet.Columns(1, headers.Length).Style.Alignment.WrapText = true;
        foreach (var column in new[] { 1, 2, 3, 4, 5, 7 })
        {
            worksheet.Range(4, column, MaximumRows + 3, column).Style.NumberFormat.Format = "@";
        }
        worksheet.Range(4, 6, MaximumRows + 3, 6).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Range(4, 8, MaximumRows + 3, 8).Style.NumberFormat.Format = "0";
        worksheet.Column(1).Width = 28;
        worksheet.Column(2).Width = 20;
        worksheet.Column(3).Width = 22;
        worksheet.Columns(4, 5).Width = 18;
        worksheet.Column(6).Width = 14;
        worksheet.Column(7).Width = 24;
        worksheet.Column(8).Width = 10;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<string> ValidateUploadMetadata(IFormFile file)
    {
        var errors = new List<string>();
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(".xlsx 파일만 업로드할 수 있습니다.");
        }
        if (file.Length <= 0)
        {
            errors.Add("빈 Excel 파일은 업로드할 수 없습니다.");
        }
        if (file.Length > MaximumFileBytes)
        {
            errors.Add("Excel 파일은 5MiB 이하만 업로드할 수 있습니다.");
        }
        return errors;
    }

    public static async Task<UploadedExcelFile> ReadUploadedFileAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream((int)Math.Min(file.Length, MaximumFileBytes));
        var buffer = new byte[81920];
        long totalRead = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
            if (totalRead > MaximumFileBytes)
            {
                throw new InvalidDataException("Excel 파일은 5MiB 이하만 업로드할 수 있습니다.");
            }
            output.Write(buffer, 0, read);
        }

        var content = output.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new UploadedExcelFile(Path.GetFileName(file.FileName), totalRead, sha256, content);
    }

    public async Task<ParsedOsanProjectExcelFile> ParseAsync(
        UploadedExcelFile file,
        CancellationToken cancellationToken)
    {
        var fileErrors = ValidateFile(file);
        if (fileErrors.Count > 0)
        {
            return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], fileErrors);
        }

        await ParseSemaphore.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ParseCore(file, cancellationToken);
        }
        finally
        {
            ParseSemaphore.Release();
        }
    }

    private static ParsedOsanProjectExcelFile ParseCore(
        UploadedExcelFile file,
        CancellationToken cancellationToken)
    {
        var zipErrors = ValidateZipPackage(file.Content);
        if (zipErrors.Count > 0)
        {
            return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], zipErrors);
        }

        try
        {
            using var stream = new MemoryStream(file.Content, writable: false);
            using var workbook = new XLWorkbook(stream);
            if (workbook.Worksheets.Count > MaximumWorksheets)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [],
                    [$"Worksheet는 최대 {MaximumWorksheets}개까지 허용됩니다."]);
            }
            if (workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()).Any(cell => cell.HasFormula))
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], ["Excel Formula는 사용할 수 없습니다."]);
            }

            var selection = SelectWorksheet(workbook);
            if (selection.Worksheet is null || selection.Header is null)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], selection.Errors);
            }

            var worksheet = selection.Worksheet;
            var header = selection.Header;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? header.RowNumber;
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (lastRow - header.RowNumber > MaximumRows)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, lastRow - header.RowNumber, [],
                    [$"Excel 데이터 행은 최대 {MaximumRows}행까지 허용됩니다."]);
            }
            if (lastColumn > MaximumColumns || worksheet.CellsUsed().Count() > MaximumCells)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], ["Excel 사용 범위가 허용값을 초과했습니다."]);
            }

            var rows = new List<ParsedOsanProjectExcelRow>();
            for (var rowNumber = header.RowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsEmptyRow(worksheet, rowNumber, header.Columns))
                {
                    continue;
                }

                var errors = new List<string>();
                var title = ReadText(worksheet, rowNumber, header.Columns, "title");
                var projectCode = ReadText(worksheet, rowNumber, header.Columns, "project_code");
                var customerName = ReadText(worksheet, rowNumber, header.Columns, "customer_name");
                var poNumber = ReadText(worksheet, rowNumber, header.Columns, "po_number");
                var workOrderNumber = ReadText(worksheet, rowNumber, header.Columns, "work_order_number");
                var deliveryDate = ReadDate(worksheet, rowNumber, header.Columns, errors);
                var productName = ReadText(worksheet, rowNumber, header.Columns, "product_name");
                var quantity = ReadQuantity(worksheet, rowNumber, header.Columns, errors);
                rows.Add(new ParsedOsanProjectExcelRow(
                    rowNumber,
                    title,
                    projectCode,
                    customerName,
                    poNumber,
                    workOrderNumber,
                    deliveryDate,
                    productName,
                    quantity,
                    errors));
            }

            if (rows.Count == 0)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], ["등록할 프로젝트 행이 없습니다."]);
            }
            if (rows.Count > MaximumRows)
            {
                return new ParsedOsanProjectExcelFile(file.FileSha256, rows.Count, rows,
                    [$"Excel 데이터 행은 최대 {MaximumRows}행까지 허용됩니다."]);
            }
            return new ParsedOsanProjectExcelFile(file.FileSha256, rows.Count, rows, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new ParsedOsanProjectExcelFile(file.FileSha256, 0, [], ["올바른 .xlsx 파일을 읽을 수 없습니다."]);
        }
    }

    private static IReadOnlyList<string> ValidateFile(UploadedExcelFile file)
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
        if (file.FileSizeBytes > MaximumFileBytes || file.Content.Length > MaximumFileBytes)
        {
            errors.Add("Excel 파일은 5MiB 이하만 업로드할 수 있습니다.");
        }
        return errors;
    }

    private static IReadOnlyList<string> ValidateZipPackage(byte[] content)
    {
        var errors = new List<string>();
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (archive.Entries.Count > MaximumZipEntries)
            {
                return [$"Excel ZIP entry는 최대 {MaximumZipEntries}개까지 허용됩니다."];
            }

            long totalLength = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                totalLength = checked(totalLength + entry.Length);
                if (!names.Add(name) || name.StartsWith('/') || name.Split('/').Contains("..", StringComparer.Ordinal))
                {
                    errors.Add("Excel ZIP entry 경로가 올바르지 않습니다.");
                }
                if (entry.Length > MaximumEntryBytes)
                {
                    errors.Add("Excel ZIP entry가 허용 크기를 초과했습니다.");
                }
                if (totalLength > MaximumUncompressedBytes)
                {
                    errors.Add("Excel 압축 해제 예상 크기가 허용값을 초과했습니다.");
                }
                if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/externalLinks/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/oleObjects/", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Macro, 외부 링크, OLE 개체가 포함된 Excel은 업로드할 수 없습니다.");
                }
            }
            if (errors.Count > 0)
            {
                return errors.Distinct(StringComparer.Ordinal).ToArray();
            }

            var worksheetParts = ReadWorksheetPartNames(archive);
            if (worksheetParts.Count == 0)
            {
                return ["Excel Worksheet part를 확인할 수 없습니다."];
            }
            var worksheetCount = 0;
            var workbookCellCount = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    using var entryStream = entry.Open();
                    using var reader = XmlReader.Create(entryStream, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = MaximumEntryBytes
                    });
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element
                            && string.Equals(
                                reader.GetAttribute("TargetMode"),
                                "External",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add("외부 링크가 포함된 Excel은 업로드할 수 없습니다.");
                            break;
                        }
                    }
                    if (errors.Count > 0)
                    {
                        return errors.Distinct(StringComparer.Ordinal).ToArray();
                    }
                }
                if (!worksheetParts.Contains(name))
                {
                    continue;
                }

                using (var entryStream = entry.Open())
                using (var reader = XmlReader.Create(entryStream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumEntryBytes
                }))
                {
                    while (reader.Read() && reader.NodeType != XmlNodeType.Element)
                    {
                    }
                    if (reader.NodeType != XmlNodeType.Element
                        || !string.Equals(reader.LocalName, "worksheet", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    worksheetCount += 1;
                    if (worksheetCount > MaximumWorksheets)
                    {
                        return [$"Worksheet는 최대 {MaximumWorksheets}개까지 허용됩니다."];
                    }
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }
                        if (string.Equals(reader.LocalName, "f", StringComparison.Ordinal))
                        {
                            return ["Excel Formula는 사용할 수 없습니다."];
                        }
                        if (!string.Equals(reader.LocalName, "c", StringComparison.Ordinal))
                        {
                            continue;
                        }
                        workbookCellCount += 1;
                        if (workbookCellCount > MaximumCells)
                        {
                            return ["Excel 사용 범위가 허용값을 초과했습니다."];
                        }
                        var reference = reader.GetAttribute("r");
                        if (reference is not null && ReadColumnNumber(reference) > MaximumColumns)
                        {
                            return ["Excel 사용 범위가 허용값을 초과했습니다."];
                        }
                    }
                }
            }
        }
        catch (Exception exception)
            when (exception is InvalidDataException or IOException or NotSupportedException or XmlException)
        {
            errors.Add("올바른 .xlsx ZIP 구조가 아닙니다.");
        }
        catch (OverflowException)
        {
            errors.Add("Excel 압축 해제 예상 크기가 허용값을 초과했습니다.");
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlySet<string> ReadWorksheetPartNames(ZipArchive archive)
    {
        const string worksheetContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        var contentTypes = archive.Entries.SingleOrDefault(entry =>
            string.Equals(entry.FullName, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Content types part is missing.");
        var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var stream = contentTypes.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumEntryBytes
        }))
        {
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element
                    || !string.Equals(
                        reader.GetAttribute("ContentType"),
                        worksheetContentType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (string.Equals(reader.LocalName, "Override", StringComparison.Ordinal))
                {
                    var partName = reader.GetAttribute("PartName")?.TrimStart('/').Replace('\\', '/');
                    if (!string.IsNullOrWhiteSpace(partName)) parts.Add(partName);
                }
                else if (string.Equals(reader.LocalName, "Default", StringComparison.Ordinal))
                {
                    var extension = reader.GetAttribute("Extension")?.TrimStart('.');
                    if (!string.IsNullOrWhiteSpace(extension)) defaultExtensions.Add(extension);
                }
            }
        }

        foreach (var extension in defaultExtensions)
        {
            foreach (var entry in archive.Entries.Where(entry =>
                         string.Equals(
                             Path.GetExtension(entry.FullName).TrimStart('.'),
                             extension,
                             StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add(entry.FullName.Replace('\\', '/'));
            }
        }
        return parts;
    }

    private static int ReadColumnNumber(string cellReference)
    {
        var number = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }
            number = checked(number * 26 + char.ToUpperInvariant(character) - 'A' + 1);
            if (number > MaximumColumns)
            {
                break;
            }
        }
        return number;
    }

    private static WorksheetSelection SelectWorksheet(XLWorkbook workbook)
    {
        var candidates = new List<(IXLWorksheet Worksheet, Header Header)>();
        foreach (var worksheet in workbook.Worksheets.Where(sheet => sheet.Visibility == XLWorksheetVisibility.Visible))
        {
            var header = FindHeader(worksheet);
            if (header is not null)
            {
                candidates.Add((worksheet, header));
            }
        }
        return candidates.Count switch
        {
            1 => new WorksheetSelection(candidates[0].Worksheet, candidates[0].Header, []),
            0 => new WorksheetSelection(null, null, ["20행 안에서 필수 Header가 있는 Worksheet를 찾을 수 없습니다."]),
            _ => new WorksheetSelection(null, null, ["인식 가능한 Header를 가진 Worksheet가 여러 개입니다."])
        };
    }

    private static Header? FindHeader(IXLWorksheet worksheet)
    {
        var maximumRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, MaximumHeaderSearchRows);
        var maximumColumn = Math.Min(worksheet.LastColumnUsed()?.ColumnNumber() ?? 0, MaximumColumns);
        for (var row = 1; row <= maximumRow; row++)
        {
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicate = false;
            for (var column = 1; column <= maximumColumn; column++)
            {
                var normalized = NormalizeHeader(worksheet.Cell(row, column).GetString());
                if (!HeaderAliases.TryGetValue(normalized, out var canonical))
                {
                    continue;
                }
                if (!columns.TryAdd(canonical, column))
                {
                    duplicate = true;
                }
            }
            if (duplicate)
            {
                return null;
            }
            var required = new[] { "title", "project_code", "customer_name", "delivery_date", "product_name", "quantity" };
            if (required.All(columns.ContainsKey))
            {
                return new Header(row, columns);
            }
        }
        return null;
    }

    private static string NormalizeHeader(string value) =>
        Regex.Replace(value.Trim().TrimEnd('*').Trim(), @"\s+", " ").ToLowerInvariant();

    private static bool IsEmptyRow(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> columns) =>
        columns.Values.All(column => worksheet.Cell(rowNumber, column).IsEmpty());

    private static string? ReadText(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> columns,
        string field)
    {
        if (!columns.TryGetValue(field, out var column))
        {
            return null;
        }
        var value = worksheet.Cell(rowNumber, column).GetFormattedString(CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateOnly? ReadDate(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> columns,
        ICollection<string> errors)
    {
        var cell = worksheet.Cell(rowNumber, columns["delivery_date"]);
        if (cell.IsEmpty())
        {
            return null;
        }
        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }
        var text = cell.GetFormattedString(CultureInfo.InvariantCulture).Trim();
        var formats = new[] { "yyyy-MM-dd", "yyyy.M.d", "yyyy/MM/dd", "yyyy.MM.dd" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }
        errors.Add("납기일 날짜 형식을 확인할 수 없습니다.");
        return null;
    }

    private static int? ReadQuantity(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyDictionary<string, int> columns,
        ICollection<string> errors)
    {
        var cell = worksheet.Cell(rowNumber, columns["quantity"]);
        if (cell.DataType == XLDataType.Number)
        {
            var numeric = cell.GetDouble();
            if (double.IsFinite(numeric)
                && numeric == Math.Truncate(numeric)
                && numeric >= int.MinValue
                && numeric <= int.MaxValue)
            {
                return (int)numeric;
            }
            errors.Add("수량은 정수여야 합니다.");
            return null;
        }

        var text = cell.GetFormattedString(CultureInfo.InvariantCulture).Trim();
        if (text.Length == 0)
        {
            return null;
        }
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            return quantity;
        }
        errors.Add("수량은 정수여야 합니다.");
        return null;
    }

    private sealed record Header(int RowNumber, IReadOnlyDictionary<string, int> Columns);
    private sealed record WorksheetSelection(IXLWorksheet? Worksheet, Header? Header, IReadOnlyList<string> Errors);
}

public sealed record ParsedOsanProjectExcelFile(
    string FileSha256,
    int TotalRowCount,
    IReadOnlyList<ParsedOsanProjectExcelRow> Rows,
    IReadOnlyList<string> Errors);

public sealed record ParsedOsanProjectExcelRow(
    int RowNumber,
    string? Title,
    string? ProjectCode,
    string? CustomerName,
    string? PoNumber,
    string? WorkOrderNumber,
    DateOnly? DeliveryDate,
    string? ProductName,
    int? Quantity,
    IReadOnlyList<string> Errors);
