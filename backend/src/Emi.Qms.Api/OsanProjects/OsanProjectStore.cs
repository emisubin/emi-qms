using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.OsanProjects;

public sealed partial class OsanProjectStore
{
    private const string OsanProjectCodeConstraint = "ux_projects_osan_project_code";
    private readonly DatabaseConnectionStringProvider connectionStringProvider;
    private readonly OsanProjectExcelParser excelParser;

    public OsanProjectStore(DatabaseConnectionStringProvider connectionStringProvider)
        : this(connectionStringProvider, new OsanProjectExcelParser())
    {
    }

    public OsanProjectStore(
        DatabaseConnectionStringProvider connectionStringProvider,
        OsanProjectExcelParser excelParser)
    {
        this.connectionStringProvider = connectionStringProvider;
        this.excelParser = excelParser;
    }

    public byte[] CreateExcelTemplate() => excelParser.CreateTemplate();

    public async Task<OsanProjectExcelPreviewResponse> PreviewExcelAsync(
        UploadedExcelFile file,
        IReadOnlyList<OsanProjectExcelRowRequest>? rowOverrides,
        CancellationToken cancellationToken)
    {
        var parsed = await excelParser.ParseAsync(file, cancellationToken);
        var built = BuildExcelPreview(ResolveExcelRows(parsed, rowOverrides));
        if (built.NormalizedRows.Count > 0)
        {
            var existing = await ReadExistingProjectsAsync(
                built.NormalizedRows.Select(row => row.Input.ProjectCode),
                cancellationToken);
            built = AddDuplicateKinds(built, existing);
        }
        return built.Response;
    }

    public Task<OsanProjectExcelPreviewResponse> PreviewExcelAsync(
        UploadedExcelFile file,
        CancellationToken cancellationToken) => PreviewExcelAsync(file, null, cancellationToken);

    public async Task<OsanProjectExcelApplyResult> ApplyExcelAsync(
        UploadedExcelFile file,
        string expectedFileSha256,
        Guid operationId,
        Guid userId,
        IReadOnlyList<OsanProjectExcelRowRequest>? rowOverrides,
        IReadOnlySet<int> confirmedDuplicateRowNumbers,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(file.FileSha256, expectedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return new OsanProjectExcelApplyResult(OsanProjectExcelApplyStatus.FileChanged);
        }

        var parsed = ResolveExcelRows(
            await excelParser.ParseAsync(file, cancellationToken),
            rowOverrides);
        var built = BuildExcelPreview(parsed);
        if (built.Response.ErrorCount > 0)
        {
            return new OsanProjectExcelApplyResult(
                OsanProjectExcelApplyStatus.Validation,
                Errors: ToApplyErrors(built.Response));
        }

        var rows = built.NormalizedRows;
        var selectedRowNumbers = rows.Select(row => row.RowNumber).ToHashSet();
        if (confirmedDuplicateRowNumbers.Any(rowNumber => !selectedRowNumbers.Contains(rowNumber)))
        {
            return new OsanProjectExcelApplyResult(
                OsanProjectExcelApplyStatus.Validation,
                Errors: new Dictionary<string, string[]>
                {
                    ["confirmedDuplicateRowNumbers"] = ["선택한 행 번호만 중복 확인할 수 있습니다."]
                });
        }
        var batchFingerprint = CreateExcelBatchFingerprint(file.FileSha256, operationId, rows);
        var fingerprints = rows.Select((row, index) =>
            CreateExcelFingerprint(batchFingerprint, index, row.RowNumber, row.Input)).ToArray();
        var operationIds = rows.Select((_, index) =>
            index == 0 ? operationId : CreateDerivedOperationId(operationId, index)).ToArray();

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var rootCreated = await TryCreateOperationAsync(
                connection,
                transaction,
                operationIds[0],
                fingerprints[0],
                userId,
                cancellationToken);
            if (!rootCreated)
            {
                var replay = await ReadExcelReplayAsync(
                    connection,
                    transaction,
                    operationIds,
                    fingerprints,
                    userId,
                    cancellationToken);
                if (replay is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OsanProjectExcelApplyResult(OsanProjectExcelApplyStatus.OperationConflict);
                }

                await transaction.CommitAsync(cancellationToken);
                return new OsanProjectExcelApplyResult(
                    OsanProjectExcelApplyStatus.Success,
                    new OsanProjectExcelApplyResponse(
                        operationId,
                        true,
                        replay.Count,
                        replay,
                        rows.Select(row => row.RowNumber).ToArray()));
            }

            await AcquireProjectCodeLocksAsync(
                connection,
                transaction,
                rows.Select(row => row.Input.ProjectCode),
                cancellationToken);
            var existing = await ReadExistingProjectsAsync(
                connection,
                transaction,
                rows.Select(row => row.Input.ProjectCode),
                cancellationToken);
            var duplicates = FindDuplicateKinds(rows, existing);
            var confirmationRequired = duplicates.Keys
                .Where(rowNumber => !confirmedDuplicateRowNumbers.Contains(rowNumber))
                .Order()
                .ToArray();
            if (confirmationRequired.Length > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OsanProjectExcelApplyResult(
                    OsanProjectExcelApplyStatus.ConfirmationRequired,
                    ConfirmationRowNumbers: confirmationRequired);
            }

            var projectIds = new Guid?[rows.Count];
            var insertionOrder = Enumerable.Range(0, rows.Count)
                .OrderBy(index => rows[index].Input.ProjectCode, StringComparer.Ordinal)
                .ThenBy(index => index)
                .ToArray();
            foreach (var index in insertionOrder)
            {
                if (index > 0 && !await TryCreateOperationAsync(
                        connection,
                        transaction,
                        operationIds[index],
                        fingerprints[index],
                        userId,
                        cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OsanProjectExcelApplyResult(OsanProjectExcelApplyStatus.OperationConflict);
                }

                var row = rows[index];
                var projectId = Guid.NewGuid();
                await InsertProjectAsync(connection, transaction, projectId, row.Input, userId, cancellationToken);
                await InsertCreatorAccessAsync(connection, transaction, projectId, userId, cancellationToken);
                await InsertTargetsAndStepsAsync(connection, transaction, projectId, row.Input, cancellationToken);
                await InsertProjectEventAsync(connection, transaction, projectId, userId, cancellationToken);
                await CompleteOperationAsync(
                    connection,
                    transaction,
                    operationIds[index],
                    projectId,
                    cancellationToken);
                projectIds[index] = projectId;
            }

            await transaction.CommitAsync(cancellationToken);
            var orderedProjectIds = projectIds.Select(projectId => projectId!.Value).ToArray();
            return new OsanProjectExcelApplyResult(
                OsanProjectExcelApplyStatus.Success,
                new OsanProjectExcelApplyResponse(
                    operationId,
                    false,
                    orderedProjectIds.Length,
                    orderedProjectIds,
                    rows.Select(row => row.RowNumber).ToArray()));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                  && string.Equals(exception.ConstraintName, OsanProjectCodeConstraint, StringComparison.Ordinal))
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return new OsanProjectExcelApplyResult(OsanProjectExcelApplyStatus.ProjectCodeConflict);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public Task<OsanProjectExcelApplyResult> ApplyExcelAsync(
        UploadedExcelFile file,
        string expectedFileSha256,
        Guid operationId,
        Guid userId,
        CancellationToken cancellationToken) => ApplyExcelAsync(
            file,
            expectedFileSha256,
            operationId,
            userId,
            null,
            new HashSet<int>(),
            cancellationToken);

    public async Task<OsanProjectListResponse> ListAsync(
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var where = new List<string>
        {
            "projects.project_profile = 'Osan'",
            "projects.deleted_at_utc is null"
        };
        var parameters = new List<NpgsqlParameter>();
        AddAccessScope(where, parameters, accessScope);

        await using var command = dataSource.CreateCommand($"""
            select
                projects.id,
                projects.project_title,
                projects.project_code,
                projects.customer_name,
                projects.osan_po_number,
                projects.osan_work_order_number,
                projects.delivery_date,
                projects.osan_product_name,
                projects.osan_quantity,
                case
                    when projects.status = 'Completed' then 'Completed'
                    when progress.completed_step_count > 0 then 'InProgress'
                    else 'NotStarted'
                end,
                progress.completed_step_count,
                progress.total_step_count,
                projects.created_at_utc
            from projects
            cross join lateral (
                select
                    count(*) filter (where steps.status = 'Completed')::integer as completed_step_count,
                    count(*)::integer as total_step_count
                from osan_active_project_target_steps steps
                where steps.project_id = projects.id
            ) progress
            where {string.Join(" and ", where)}
            order by projects.delivery_date, projects.project_code, projects.id;
            """);
        command.Parameters.AddRange(parameters.ToArray());

        var items = new List<OsanProjectListItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadListItem(reader));
        }

        return new OsanProjectListResponse(items);
    }

    public async Task<OsanProjectAccessRecord?> GetAccessRecordAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, project_key
            from projects
            where id = @project_id
              and project_profile = 'Osan'
              and deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OsanProjectAccessRecord(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    public async Task<OsanProjectDetailResponse?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadDetailAsync(connection, null, projectId, cancellationToken);
    }

    public async Task<OsanProjectCreateResult> CreateAsync(
        NormalizedCreateOsanProjectInput input,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var fingerprint = CreateFingerprint(input);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var operationCreated = await TryCreateOperationAsync(
                connection,
                transaction,
                input.OperationId,
                fingerprint,
                userId,
                cancellationToken);

            if (!operationCreated)
            {
                var existing = await ReadOperationAsync(
                    connection,
                    transaction,
                    input.OperationId,
                    cancellationToken);
                if (existing is null
                    || existing.CreatedByUserId != userId
                    || !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                    || existing.ProjectId is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OsanProjectCreateResult(OsanProjectCreateStatus.OperationConflict);
                }

                var replayedProject = await ReadDetailAsync(
                    connection,
                    transaction,
                    existing.ProjectId.Value,
                    cancellationToken);
                if (replayedProject is null)
                {
                    throw new InvalidOperationException("A completed Osan project operation has no project result.");
                }

                await transaction.CommitAsync(cancellationToken);
                return new OsanProjectCreateResult(
                    OsanProjectCreateStatus.Success,
                    new OsanProjectCreateResponse(input.OperationId, true, replayedProject));
            }

            await AcquireProjectCodeLocksAsync(
                connection,
                transaction,
                [input.ProjectCode],
                cancellationToken);
            if (await ProjectCodeExistsAsync(
                    connection,
                    transaction,
                    input.ProjectCode,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OsanProjectCreateResult(OsanProjectCreateStatus.ProjectCodeConflict);
            }

            var projectId = Guid.NewGuid();
            await InsertProjectAsync(connection, transaction, projectId, input, userId, cancellationToken);
            await InsertCreatorAccessAsync(connection, transaction, projectId, userId, cancellationToken);
            await InsertTargetsAndStepsAsync(connection, transaction, projectId, input, cancellationToken);
            await InsertProjectEventAsync(connection, transaction, projectId, userId, cancellationToken);
            await CompleteOperationAsync(
                connection,
                transaction,
                input.OperationId,
                projectId,
                cancellationToken);

            var project = await ReadDetailAsync(connection, transaction, projectId, cancellationToken)
                ?? throw new InvalidOperationException("The Osan project was not readable before commit.");
            await transaction.CommitAsync(cancellationToken);
            return new OsanProjectCreateResult(
                OsanProjectCreateStatus.Success,
                new OsanProjectCreateResponse(input.OperationId, false, project));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                  && string.Equals(exception.ConstraintName, OsanProjectCodeConstraint, StringComparison.Ordinal))
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return new OsanProjectCreateResult(OsanProjectCreateStatus.ProjectCodeConflict);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    private static ExcelPreviewBuild BuildExcelPreview(ParsedOsanProjectExcelFile parsed)
    {
        var normalizedRows = new List<ExcelNormalizedRow>();
        var responseRows = new List<OsanProjectExcelPreviewRowResponse>(parsed.Rows.Count);
        foreach (var row in parsed.Rows)
        {
            var errors = new List<string>(row.Errors);
            var (input, normalizationErrors) = OsanProjectInputNormalizer.Normalize(
                new CreateOsanProjectRequest(
                    row.Title,
                    row.ProjectCode,
                    row.CustomerName,
                    row.PoNumber,
                    row.WorkOrderNumber,
                    row.DeliveryDate,
                    row.ProductName,
                    row.Quantity,
                    Guid.NewGuid()));
            foreach (var (field, messages) in normalizationErrors)
            {
                errors.AddRange(messages.Select(message => $"{GetExcelFieldName(field)}: {message}"));
            }
            var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (row.FieldErrors is not null)
            {
                foreach (var (field, messages) in row.FieldErrors)
                {
                    fieldErrors[field] = messages;
                }
            }
            foreach (var (field, messages) in normalizationErrors)
            {
                var jsonField = GetExcelJsonFieldName(field);
                fieldErrors[jsonField] = fieldErrors.TryGetValue(jsonField, out var existing)
                    ? existing.Concat(messages).Distinct(StringComparer.Ordinal).ToArray()
                    : messages;
            }

            var response = new OsanProjectExcelPreviewRowResponse(
                row.RowNumber,
                input?.Title ?? row.Title,
                input?.ProjectCode ?? row.ProjectCode,
                input?.CustomerName ?? row.CustomerName,
                input?.PoNumber ?? row.PoNumber,
                input?.WorkOrderNumber ?? row.WorkOrderNumber,
                input?.DeliveryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                    ?? row.RawDeliveryDate
                    ?? row.DeliveryDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                input?.ProductName ?? row.ProductName,
                input?.Quantity ?? row.RawQuantity ?? row.Quantity,
                errors.Distinct(StringComparer.Ordinal).ToArray(),
                FieldErrors: fieldErrors.Count == 0 ? null : fieldErrors);
            responseRows.Add(response);
            if (input is not null && response.Errors.Count == 0)
            {
                normalizedRows.Add(new ExcelNormalizedRow(responseRows.Count - 1, row.RowNumber, input));
            }
        }

        var fileErrors = parsed.Errors.ToList();
        var totalQuantityValue = normalizedRows.Sum(row => (long)row.Input.Quantity);
        var totalQuantity = (int)Math.Min(totalQuantityValue, int.MaxValue);
        if (totalQuantity > OsanProjectExcelParser.MaximumTotalQuantity)
        {
            fileErrors.Add($"전체 수량은 최대 {OsanProjectExcelParser.MaximumTotalQuantity}개까지 허용됩니다.");
            normalizedRows.Clear();
        }

        var responseValue = CreateExcelPreviewResponse(
            parsed.FileSha256,
            parsed.TotalRowCount,
            totalQuantity,
            responseRows,
            fileErrors);
        return new ExcelPreviewBuild(responseValue, normalizedRows);
    }

    private static ExcelPreviewBuild AddDuplicateKinds(
        ExcelPreviewBuild built,
        IReadOnlyList<ExistingOsanProject> existing)
    {
        var duplicateKinds = FindDuplicateKinds(built.NormalizedRows, existing);
        if (duplicateKinds.Count == 0)
        {
            return built;
        }

        var rows = built.Response.Rows.ToArray();
        foreach (var row in built.NormalizedRows.Where(row => duplicateKinds.ContainsKey(row.RowNumber)))
        {
            rows[row.ResponseIndex] = rows[row.ResponseIndex] with
            {
                DuplicateKind = duplicateKinds[row.RowNumber]
            };
        }
        return new ExcelPreviewBuild(
            CreateExcelPreviewResponse(
                built.Response.FileSha256,
                built.Response.TotalRowCount,
                built.Response.TotalQuantity,
                rows,
                built.Response.Errors),
            built.NormalizedRows);
    }

    private static OsanProjectExcelPreviewResponse CreateExcelPreviewResponse(
        string fileSha256,
        int totalRowCount,
        int totalQuantity,
        IReadOnlyList<OsanProjectExcelPreviewRowResponse> rows,
        IReadOnlyList<string> errors) =>
        new(
            fileSha256,
            true,
            totalRowCount,
            totalQuantity,
            rows.Count(row => row.Errors.Count > 0) + errors.Count,
            rows,
            errors.Distinct(StringComparer.Ordinal).ToArray());

    private static IReadOnlyDictionary<string, string[]> ToApplyErrors(
        OsanProjectExcelPreviewResponse preview)
    {
        var errors = new Dictionary<string, string[]>();
        if (preview.Errors.Count > 0)
        {
            errors["file"] = preview.Errors.ToArray();
        }
        foreach (var row in preview.Rows.Where(row => row.Errors.Count > 0))
        {
            errors[$"rows[{row.RowNumber}]"] = row.Errors.ToArray();
        }
        return errors;
    }

    private static string GetExcelFieldName(string field) => field switch
    {
        nameof(CreateOsanProjectRequest.Title) => "장비명",
        nameof(CreateOsanProjectRequest.ProjectCode) => "프로젝트 코드",
        nameof(CreateOsanProjectRequest.CustomerName) => "거래처",
        nameof(CreateOsanProjectRequest.PoNumber) => "PO",
        nameof(CreateOsanProjectRequest.WorkOrderNumber) => "W/O",
        nameof(CreateOsanProjectRequest.DeliveryDate) => "납기일",
        nameof(CreateOsanProjectRequest.ProductName) => "part분류",
        nameof(CreateOsanProjectRequest.Quantity) => "수량",
        _ => field
    };

    private static string GetExcelJsonFieldName(string field) => field switch
    {
        nameof(CreateOsanProjectRequest.Title) => "title",
        nameof(CreateOsanProjectRequest.ProjectCode) => "projectCode",
        nameof(CreateOsanProjectRequest.CustomerName) => "customerName",
        nameof(CreateOsanProjectRequest.PoNumber) => "poNumber",
        nameof(CreateOsanProjectRequest.WorkOrderNumber) => "workOrderNumber",
        nameof(CreateOsanProjectRequest.DeliveryDate) => "deliveryDate",
        nameof(CreateOsanProjectRequest.ProductName) => "productName",
        nameof(CreateOsanProjectRequest.Quantity) => "quantity",
        _ => field
    };

    private static ParsedOsanProjectExcelFile ResolveExcelRows(
        ParsedOsanProjectExcelFile parsed,
        IReadOnlyList<OsanProjectExcelRowRequest>? rowOverrides)
    {
        if (rowOverrides is null || parsed.Errors.Count > 0)
        {
            return parsed;
        }

        var errors = new List<string>();
        if (rowOverrides.Count == 0)
        {
            errors.Add("등록하거나 미리보기할 행을 하나 이상 선택해 주세요.");
        }
        if (rowOverrides.Count > OsanProjectExcelParser.MaximumRows)
        {
            errors.Add($"Excel 데이터 행은 최대 {OsanProjectExcelParser.MaximumRows}행까지 허용됩니다.");
        }
        var duplicateRowNumbers = rowOverrides
            .GroupBy(row => row.RowNumber)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();
        if (duplicateRowNumbers.Length > 0)
        {
            errors.Add("행 번호는 중복될 수 없습니다.");
        }
        var originalRowNumbers = parsed.Rows.Select(row => row.RowNumber).ToHashSet();
        if (rowOverrides.Any(row => !originalRowNumbers.Contains(row.RowNumber)))
        {
            errors.Add("원본 Excel에 없는 행 번호는 사용할 수 없습니다.");
        }
        if (errors.Count > 0)
        {
            return new ParsedOsanProjectExcelFile(
                parsed.FileSha256,
                rowOverrides.Count,
                [],
                errors);
        }

        var rows = rowOverrides
            .OrderBy(row => row.RowNumber)
            .Select(ToParsedOverrideRow)
            .ToArray();
        return new ParsedOsanProjectExcelFile(parsed.FileSha256, rows.Length, rows, []);
    }

    private static ParsedOsanProjectExcelRow ToParsedOverrideRow(OsanProjectExcelRowRequest row)
    {
        var errors = new List<string>();
        var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        DateOnly? deliveryDate = null;
        DateOnly parsedDeliveryDate = default;
        if (!string.IsNullOrWhiteSpace(row.DeliveryDate)
            && !DateOnly.TryParseExact(
                row.DeliveryDate,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out parsedDeliveryDate))
        {
            const string message = "납기일은 YYYY-MM-DD 형식의 올바른 날짜여야 합니다.";
            errors.Add($"납기일: {message}");
            fieldErrors["deliveryDate"] = [message];
        }
        else if (!string.IsNullOrWhiteSpace(row.DeliveryDate))
        {
            deliveryDate = parsedDeliveryDate;
        }

        int? quantity = null;
        if (row.Quantity is not null)
        {
            if (decimal.Truncate(row.Quantity.Value) != row.Quantity.Value
                || row.Quantity.Value < int.MinValue
                || row.Quantity.Value > int.MaxValue)
            {
                const string message = "수량은 정수여야 합니다.";
                errors.Add($"수량: {message}");
                fieldErrors["quantity"] = [message];
            }
            else
            {
                quantity = decimal.ToInt32(row.Quantity.Value);
            }
        }

        return new ParsedOsanProjectExcelRow(
            row.RowNumber,
            row.Title,
            row.ProjectCode,
            row.CustomerName,
            row.PoNumber,
            row.WorkOrderNumber,
            deliveryDate,
            row.ProductName,
            quantity,
            errors,
            row.DeliveryDate,
            row.Quantity,
            fieldErrors);
    }

    private static IReadOnlyDictionary<int, string> FindDuplicateKinds(
        IReadOnlyList<ExcelNormalizedRow> rows,
        IReadOnlyList<ExistingOsanProject> existing)
    {
        var result = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            var sameCodeRows = rows.Where(candidate =>
                candidate.RowNumber != row.RowNumber
                && string.Equals(candidate.Input.ProjectCode, row.Input.ProjectCode, StringComparison.Ordinal));
            var sameCodeProjects = existing.Where(candidate =>
                string.Equals(candidate.ProjectCode, row.Input.ProjectCode, StringComparison.Ordinal));
            var identical = sameCodeRows.Any(candidate => IsIdentical(row.Input, candidate.Input))
                || sameCodeProjects.Any(candidate => IsIdentical(row.Input, candidate));
            if (identical)
            {
                result[row.RowNumber] = "identical";
            }
            else if (sameCodeRows.Any() || sameCodeProjects.Any())
            {
                result[row.RowNumber] = "code";
            }
        }
        return result;
    }

    private static bool IsIdentical(
        NormalizedCreateOsanProjectInput left,
        NormalizedCreateOsanProjectInput right) =>
        string.Equals(left.Title, right.Title, StringComparison.Ordinal)
        && string.Equals(left.ProjectCode, right.ProjectCode, StringComparison.Ordinal)
        && string.Equals(left.CustomerName, right.CustomerName, StringComparison.Ordinal)
        && string.Equals(left.ProductName, right.ProductName, StringComparison.Ordinal)
        && left.Quantity == right.Quantity;

    private static bool IsIdentical(
        NormalizedCreateOsanProjectInput left,
        ExistingOsanProject right) =>
        string.Equals(left.Title, right.Title, StringComparison.Ordinal)
        && string.Equals(left.ProjectCode, right.ProjectCode, StringComparison.Ordinal)
        && string.Equals(left.CustomerName, right.CustomerName, StringComparison.Ordinal)
        && string.Equals(left.ProductName, right.ProductName, StringComparison.Ordinal)
        && left.Quantity == right.Quantity;

    private async Task<IReadOnlyList<ExistingOsanProject>> ReadExistingProjectsAsync(
        IEnumerable<string> projectCodes,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadExistingProjectsAsync(connection, null, projectCodes, cancellationToken);
    }

    private static async Task<IReadOnlyList<ExistingOsanProject>> ReadExistingProjectsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IEnumerable<string> projectCodes,
        CancellationToken cancellationToken)
    {
        var codes = projectCodes.Distinct(StringComparer.Ordinal).ToArray();
        if (codes.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project_title, project_code, customer_name, osan_product_name, osan_quantity
            from projects
            where project_profile = 'Osan'
              and deleted_at_utc is null
              and project_code = any(@project_codes);
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>("project_codes", codes));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var existing = new List<ExistingOsanProject>();
        while (await reader.ReadAsync(cancellationToken))
        {
            existing.Add(new ExistingOsanProject(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)));
        }
        return existing;
    }

    private static async Task<bool> ProjectCodeExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string projectCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from projects
                where project_profile = 'Osan'
                  and project_code = @project_code
            );
            """;
        command.Parameters.AddWithValue("project_code", projectCode);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Project code lookup returned no value."));
    }

    private static async Task AcquireProjectCodeLocksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<string> projectCodes,
        CancellationToken cancellationToken)
    {
        foreach (var projectCode in projectCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "select pg_advisory_xact_lock(hashtextextended(@project_code, 0));";
            command.Parameters.AddWithValue("project_code", projectCode);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<Guid>?> ReadExcelReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Guid> operationIds,
        IReadOnlyList<string> fingerprints,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var projectIds = new List<Guid>(operationIds.Count);
        for (var index = 0; index < operationIds.Count; index++)
        {
            var existing = await ReadOperationAsync(
                connection,
                transaction,
                operationIds[index],
                cancellationToken);
            if (existing is null
                || existing.CreatedByUserId != userId
                || !string.Equals(existing.RequestFingerprint, fingerprints[index], StringComparison.Ordinal)
                || existing.ProjectId is null)
            {
                return null;
            }
            projectIds.Add(existing.ProjectId.Value);
        }
        return projectIds;
    }

    private static string CreateExcelBatchFingerprint(
        string fileSha256,
        Guid rootOperationId,
        IReadOnlyList<ExcelNormalizedRow> rows)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Kind = "OsanProjectExcelImportBatch",
            FileSha256 = fileSha256,
            RootOperationId = rootOperationId,
            Rows = rows.Select(row => new
            {
                row.RowNumber,
                row.Input.Title,
                row.Input.ProjectCode,
                row.Input.CustomerName,
                row.Input.PoNumber,
                row.Input.WorkOrderNumber,
                DeliveryDate = row.Input.DeliveryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                row.Input.ProductName,
                row.Input.Quantity
            })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string CreateExcelFingerprint(
        string batchFingerprint,
        int rowIndex,
        int rowNumber,
        NormalizedCreateOsanProjectInput input)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Kind = "OsanProjectExcelImport",
            BatchFingerprint = batchFingerprint,
            RowIndex = rowIndex,
            RowNumber = rowNumber,
            input.Title,
            input.ProjectCode,
            input.CustomerName,
            input.PoNumber,
            input.WorkOrderNumber,
            DeliveryDate = input.DeliveryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            input.ProductName,
            input.Quantity
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static Guid CreateDerivedOperationId(Guid rootOperationId, int rowIndex)
    {
        var input = Encoding.UTF8.GetBytes($"osan-project-excel-operation|{rootOperationId:D}|{rowIndex}");
        var bytes = SHA256.HashData(input).AsSpan(0, 16).ToArray();
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static async Task<bool> TryCreateOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        string fingerprint,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_project_create_operations (
                operation_id,
                request_fingerprint,
                created_by_user_id
            )
            values (@operation_id, @request_fingerprint, @user_id)
            on conflict (operation_id) do nothing
            returning true;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("request_fingerprint", fingerprint);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<OsanProjectOperation?> ReadOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select request_fingerprint, created_by_user_id, project_id
            from osan_project_create_operations
            where operation_id = @operation_id
            for update;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OsanProjectOperation(
                reader.GetString(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2))
            : null;
    }

    private static async Task InsertProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        NormalizedCreateOsanProjectInput input,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into projects (
                id,
                project_key,
                project_number,
                name,
                customer_name,
                item,
                project_code,
                project_title,
                project_title_normalized,
                delivery_date,
                status,
                created_by_user_id,
                updated_at_utc,
                project_profile,
                osan_po_number,
                osan_work_order_number,
                osan_product_name,
                osan_quantity
            )
            values (
                @project_id,
                @project_key,
                @project_code,
                @title,
                @customer_name,
                '',
                @project_code,
                @title,
                null,
                @delivery_date,
                'Active',
                @user_id,
                now(),
                'Osan',
                @po_number,
                @work_order_number,
                @product_name,
                @quantity
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_key", $"osan-{projectId:N}");
        command.Parameters.AddWithValue("project_code", input.ProjectCode);
        command.Parameters.AddWithValue("title", input.Title);
        command.Parameters.AddWithValue("customer_name", input.CustomerName);
        command.Parameters.AddWithValue("delivery_date", input.DeliveryDate);
        command.Parameters.Add(new NpgsqlParameter("po_number", NpgsqlDbType.Text)
        {
            Value = input.PoNumber is null ? DBNull.Value : input.PoNumber
        });
        command.Parameters.Add(new NpgsqlParameter("work_order_number", NpgsqlDbType.Text)
        {
            Value = input.WorkOrderNumber is null ? DBNull.Value : input.WorkOrderNumber
        });
        command.Parameters.AddWithValue("product_name", input.ProductName);
        command.Parameters.AddWithValue("quantity", input.Quantity);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCreatorAccessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into user_project_access (user_id, project_id)
            values (@user_id, @project_id);
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertTargetsAndStepsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        NormalizedCreateOsanProjectInput input,
        CancellationToken cancellationToken, int firstSequence = 1)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with created_targets as (
                insert into osan_project_targets (
                    project_id,
                    sequence_number,
                    display_name
                )
                select
                    @project_id,
                    sequence_number,
                    @product_name || ' ' || sequence_number::text
                from generate_series(@first_sequence, @quantity) sequence_number
                on conflict (project_id, sequence_number) do update set is_active=true
                returning id, sequence_number
            )
            insert into osan_project_target_steps (
                project_id,
                target_id,
                sequence_number,
                step_code,
                step_name
            )
            select
                @project_id,
                created_targets.id,
                step_snapshot.sequence_number,
                step_snapshot.step_code,
                step_snapshot.step_name
            from created_targets
            cross join (values
                (1, 'INCOMING_INSPECTION', '입고검사'),
                (2, 'BATCH_INSPECTION', '배치검사'),
                (3, 'WIRING_INSPECTION', '배선검사'),
                (4, 'EIGHT_SYSTEM', '8계통'),
                (5, 'OPERATION_INSPECTION', '동작검사'),
                (6, 'SHIPPING_INSPECTION', '출하검사'),
                (7, 'PACKING', '포장')
            ) step_snapshot(sequence_number, step_code, step_name)
            order by created_targets.sequence_number, step_snapshot.sequence_number
            on conflict (target_id, sequence_number) do nothing;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("product_name", input.ProductName);
        command.Parameters.AddWithValue("quantity", input.Quantity);
        command.Parameters.AddWithValue("first_sequence", firstSequence);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertProjectEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_project_events (
                project_id,
                event_type,
                actor_user_id,
                recipient_user_id
            )
            values (@project_id, 'ProjectCreated', @user_id, @user_id);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update osan_project_create_operations
            set project_id = @project_id,
                completed_at_utc = now()
            where operation_id = @operation_id;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OsanProjectDetailResponse?> ReadDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        OsanProjectListItemResponse project;
        await using (var projectCommand = connection.CreateCommand())
        {
            projectCommand.Transaction = transaction;
            projectCommand.CommandText = """
                select
                    projects.id,
                    projects.project_title,
                    projects.project_code,
                    projects.customer_name,
                    projects.osan_po_number,
                    projects.osan_work_order_number,
                    projects.delivery_date,
                    projects.osan_product_name,
                    projects.osan_quantity,
                    case
                        when projects.status = 'Completed' then 'Completed'
                        when progress.completed_step_count > 0 then 'InProgress'
                        else 'NotStarted'
                    end,
                    progress.completed_step_count,
                    progress.total_step_count,
                    projects.created_at_utc
                from projects
                cross join lateral (
                    select
                        count(*) filter (where steps.status = 'Completed')::integer as completed_step_count,
                        count(*)::integer as total_step_count
                    from osan_active_project_target_steps steps
                    where steps.project_id = projects.id
                ) progress
                where projects.id = @project_id
                  and projects.project_profile = 'Osan'
                  and projects.deleted_at_utc is null;
                """;
            projectCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await projectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            project = ReadListItem(reader);
        }

        var targets = new List<OsanProjectTargetResponse>();
        TargetBuilder? currentTarget = null;
        await using (var targetCommand = connection.CreateCommand())
        {
            targetCommand.Transaction = transaction;
            targetCommand.CommandText = """
                select
                    targets.id,
                    targets.sequence_number,
                    targets.display_name,
                    steps.id,
                    steps.sequence_number,
                    steps.step_code,
                    steps.step_name,
                    steps.status
                from osan_active_project_targets targets
                join osan_active_project_target_steps steps on steps.target_id = targets.id
                where targets.project_id = @project_id
                order by targets.sequence_number, steps.sequence_number;
                """;
            targetCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await targetCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var targetId = reader.GetGuid(0);
                if (currentTarget?.TargetId != targetId)
                {
                    if (currentTarget is not null)
                    {
                        targets.Add(currentTarget.ToResponse());
                    }
                    currentTarget = new TargetBuilder(
                        targetId,
                        reader.GetInt32(1),
                        reader.GetString(2));
                }

                currentTarget.Steps.Add(new OsanProjectStepResponse(
                    reader.GetGuid(3),
                    reader.GetInt32(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7)));
            }
        }

        if (currentTarget is not null)
        {
            targets.Add(currentTarget.ToResponse());
        }

        return new OsanProjectDetailResponse(
            project.ProjectId,
            project.Title,
            project.ProjectCode,
            project.CustomerName,
            project.PoNumber,
            project.WorkOrderNumber,
            project.DeliveryDate,
            project.ProductName,
            project.Quantity,
            project.Status,
            project.CompletedStepCount,
            project.TotalStepCount,
            project.CreatedAtUtc,
            targets);
    }

    private static OsanProjectListItemResponse ReadListItem(NpgsqlDataReader reader)
    {
        return new OsanProjectListItemResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetFieldValue<DateOnly>(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetFieldValue<DateTimeOffset>(12));
    }

    private static void AddAccessScope(
        ICollection<string> where,
        ICollection<NpgsqlParameter> parameters,
        ProjectAccessScope accessScope)
    {
        if (accessScope.HasProjectReadAll)
        {
            return;
        }

        if (accessScope.ProjectKeys.Count == 0)
        {
            where.Add("false");
            return;
        }

        where.Add("projects.project_key = any(@project_keys)");
        parameters.Add(new NpgsqlParameter<string[]>("project_keys", accessScope.ProjectKeys.ToArray()));
    }

    private static string CreateFingerprint(NormalizedCreateOsanProjectInput input)
    {
        var payload = JsonSerializer.Serialize(new
        {
            input.Title,
            input.ProjectCode,
            input.CustomerName,
            input.PoNumber,
            input.WorkOrderNumber,
            DeliveryDate = input.DeliveryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            input.ProductName,
            input.Quantity
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }

    private static async Task RollbackQuietlyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record OsanProjectOperation(
        string RequestFingerprint,
        Guid CreatedByUserId,
        Guid? ProjectId);

    private sealed record ExcelNormalizedRow(
        int ResponseIndex,
        int RowNumber,
        NormalizedCreateOsanProjectInput Input);

    private sealed record ExistingOsanProject(
        string Title,
        string ProjectCode,
        string CustomerName,
        string ProductName,
        int Quantity);

    private sealed record ExcelPreviewBuild(
        OsanProjectExcelPreviewResponse Response,
        IReadOnlyList<ExcelNormalizedRow> NormalizedRows);

    private sealed class TargetBuilder(
        Guid targetId,
        int sequenceNumber,
        string displayName)
    {
        public Guid TargetId { get; } = targetId;
        public List<OsanProjectStepResponse> Steps { get; } = [];

        public OsanProjectTargetResponse ToResponse()
        {
            var completedStepCount = Steps.Count(step => step.Status == "Completed");
            var projectedStatus = completedStepCount == 0
                ? "NotStarted"
                : completedStepCount == Steps.Count
                    ? "Completed"
                    : "InProgress";
            return new(TargetId, sequenceNumber, displayName, projectedStatus, Steps);
        }
    }
}
