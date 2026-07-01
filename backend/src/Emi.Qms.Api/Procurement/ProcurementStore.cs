using System.Data;
using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.ProductionPlanning;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Procurement;

public sealed class ProcurementStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    ProcurementExcelParser excelParser,
    TimeProvider timeProvider)
{
    private static readonly string[] UpdatableFields =
    [
        "StandardLeadTime",
        "OrderItem",
        "TechnicalOwner",
        "OrderDate",
        "ExpectedReceiptDate",
        "IssueNote",
        "ReceiptCompleted",
        "ReceiptCompletedAtUtc",
        "ReceiptCompletionNote"
    ];

    public async Task<ProcurementResponse?> GetProjectProcurementAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var project = await ReadProjectAsync(dataSource, projectId, includeDeleted: false, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var items = await ReadItemsForProjectsAsync(dataSource, [projectId], cancellationToken);
        return BuildProjectResponse(project, items);
    }

    public async Task<ProcurementMutationResult<ProcurementResponse>> UpdateProjectProcurementAsync(
        Guid projectId,
        ProcurementBulkUpdateRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedItems = request.Items?.ToList() ?? [];
        if (normalizedItems.Count == 0)
        {
            return ProcurementMutationResult<ProcurementResponse>.Validation(
                new Dictionary<string, string[]> { ["Items"] = ["저장할 구매정보가 필요합니다."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
            if (project is null || project.DeletedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProcurementMutationResult<ProcurementResponse>.NotFound();
            }

            if (project.Status != "Active")
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProcurementMutationResult<ProcurementResponse>.Conflict("현재 프로젝트 상태에서는 구매정보를 수정할 수 없습니다.");
            }

            var existing = await LockProjectItemsAsync(connection, transaction, projectId, cancellationToken);
            var result = await PersistDirectUpdatesAsync(
                connection,
                transaction,
                project,
                existing,
                normalizedItems,
                request.Reason,
                changedByUserId,
                correlationId,
                cancellationToken);
            if (result.Status != ProcurementMutationStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }

        var response = await GetProjectProcurementAsync(projectId, cancellationToken);
        return response is null
            ? ProcurementMutationResult<ProcurementResponse>.NotFound()
            : ProcurementMutationResult<ProcurementResponse>.Success(response);
    }

    public async Task<ProcurementDashboardResponse> GetProcurementDashboardAsync(
        string? search,
        DateOnly? expectedReceiptDateFrom,
        DateOnly? expectedReceiptDateTo,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        command.Parameters.AddWithValue("today", today);
        var where = "where p.deleted_at_utc is null and p.status <> 'Completed'";
        if (!string.IsNullOrWhiteSpace(search))
        {
            where += " and (p.project_title ilike @search or p.customer_name ilike @search or p.project_code ilike @search or p.item ilike @search or exists (select 1 from project_procurement_items si where si.project_id = p.id and si.status = 'Active' and (si.order_item ilike @search or si.technical_owner ilike @search)))";
            command.Parameters.AddWithValue("search", $"%{search.Trim()}%");
        }

        if (expectedReceiptDateFrom is not null)
        {
            where += " and exists (select 1 from project_procurement_items fi where fi.project_id = p.id and fi.status = 'Active' and fi.expected_receipt_date >= @expected_receipt_date_from)";
            command.Parameters.AddWithValue("expected_receipt_date_from", expectedReceiptDateFrom.Value);
        }

        if (expectedReceiptDateTo is not null)
        {
            where += " and exists (select 1 from project_procurement_items ti where ti.project_id = p.id and ti.status = 'Active' and ti.expected_receipt_date <= @expected_receipt_date_to)";
            command.Parameters.AddWithValue("expected_receipt_date_to", expectedReceiptDateTo.Value);
        }

        command.CommandText = $"""
            with active_panels as (
                select project_id, count(*)::int as active_panel_count
                from panel_placeholders
                where status = 'Active'
                group by project_id
            ),
            procurement_by_project as (
                select project_id,
                       count(*)::int as procurement_item_count,
                       count(*) filter (where receipt_completed)::int as receipt_completed_count,
                       count(*) filter (where not receipt_completed and expected_receipt_date is not null and expected_receipt_date < @today)::int as past_expected_receipt_date_count,
                       min(expected_receipt_date) filter (where not receipt_completed and expected_receipt_date is not null) as nearest_expected_receipt_date
                from project_procurement_items
                where status = 'Active'
                group by project_id
            )
            select p.id, p.project_title, p.customer_name, p.project_code, p.item,
                   coalesce(ap.active_panel_count, 0) as active_panel_count, p.delivery_date,
                   coalesce(pb.procurement_item_count, 0) as procurement_item_count,
                   coalesce(pb.receipt_completed_count, 0) as receipt_completed_count,
                   coalesce(pb.past_expected_receipt_date_count, 0) as past_expected_receipt_date_count,
                   pb.nearest_expected_receipt_date
            from projects p
            left join active_panels ap on ap.project_id = p.id
            left join procurement_by_project pb on pb.project_id = p.id
            {where}
            order by p.project_title, p.id
            limit 500;
            """;

        var projects = new List<ProcurementProjectSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DateOnly? nearest = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10);
            projects.Add(new ProcurementProjectSummaryResponse
            {
                ProjectId = reader.GetGuid(0),
                ProjectTitle = reader.GetString(1),
                CustomerName = reader.GetString(2),
                ProjectCode = reader.GetString(3),
                Item = reader.GetString(4),
                ActivePanelCount = reader.GetInt32(5),
                DeliveryDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                ProcurementItemCount = reader.GetInt32(7),
                ReceiptCompletedCount = reader.GetInt32(8),
                PastExpectedReceiptDateCount = reader.GetInt32(9),
                NearestExpectedReceiptDate = nearest,
                DDayText = ProcurementDomain.BuildDDayText(nearest, today)
            });
        }

        await reader.DisposeAsync();

        var summary = new ProcurementDashboardSummaryResponse(
            projects.Sum(project => project.ProcurementItemCount - project.ReceiptCompletedCount),
            projects.Sum(project => project.ReceiptCompletedCount),
            projects.Sum(project => project.PastExpectedReceiptDateCount));

        return new ProcurementDashboardResponse(summary, projects);
    }

    public async Task<ProcurementListResponse> GetMaterialReceiptsAsync(
        string? search,
        bool includeCompleted,
        DateOnly? expectedReceiptDateFrom,
        DateOnly? expectedReceiptDateTo,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = "where p.deleted_at_utc is null and i.status = 'Active'";
        if (!includeCompleted)
        {
            where += " and not i.receipt_completed";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += " and (p.project_title ilike @search or p.project_code ilike @search or i.order_item ilike @search)";
            command.Parameters.AddWithValue("search", $"%{search.Trim()}%");
        }

        if (expectedReceiptDateFrom is not null)
        {
            where += " and i.expected_receipt_date >= @expected_receipt_date_from";
            command.Parameters.AddWithValue("expected_receipt_date_from", expectedReceiptDateFrom.Value);
        }

        if (expectedReceiptDateTo is not null)
        {
            where += " and i.expected_receipt_date <= @expected_receipt_date_to";
            command.Parameters.AddWithValue("expected_receipt_date_to", expectedReceiptDateTo.Value);
        }

        command.CommandText = $"""
            select i.id, i.project_id, p.project_title, p.project_code, p.delivery_date, i.sequence_number,
                   i.source_project_text, i.source_project_code_text, i.standard_lead_time, i.order_item,
                   i.technical_owner, i.order_date, i.expected_receipt_date, i.issue_note,
                   i.receipt_completed, i.receipt_completed_at_utc, i.receipt_completed_by_user_id,
                   u.display_name, i.receipt_completion_note, i.row_version, i.source_excel_row_number,
                   i.source_group_sequence, i.row_match_key, i.status
            from project_procurement_items i
            join projects p on p.id = i.project_id
            left join qms_users u on u.id = i.receipt_completed_by_user_id
            {where}
            order by i.expected_receipt_date nulls last, p.project_title, i.sequence_number
            limit 500;
            """;

        var items = new List<ProcurementItemSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return new ProcurementListResponse(items.Select(ToResponse).ToList());
    }

    public async Task<ProcurementMutationResult<ProcurementListResponse>> UpdateMaterialReceiptsAsync(
        ProcurementReceiptBulkUpdateRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var items = request.Items?.Where(item => item.ItemId is not null).ToList() ?? [];
        if (items.Count == 0)
        {
            return ProcurementMutationResult<ProcurementListResponse>.Validation(
                new Dictionary<string, string[]> { ["Items"] = ["저장할 입고 완료 항목이 필요합니다."] });
        }

        if (items.Select(item => item.ItemId!.Value).Distinct().Count() != items.Count)
        {
            return ProcurementMutationResult<ProcurementListResponse>.Validation(
                new Dictionary<string, string[]> { ["Items"] = ["중복된 구매 항목이 있습니다."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var existing = await LockItemsByIdAsync(connection, transaction, items.Select(item => item.ItemId!.Value).ToArray(), cancellationToken);
            if (existing.Count != items.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProcurementMutationResult<ProcurementListResponse>.Validation(
                    new Dictionary<string, string[]> { ["Items"] = ["대상 구매 항목을 찾을 수 없습니다."] });
            }

            foreach (var item in items)
            {
                var current = existing.Single(snapshot => snapshot.ItemId == item.ItemId!.Value);
                if (current.Status != "Active")
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProcurementMutationResult<ProcurementListResponse>.Validation(
                        new Dictionary<string, string[]> { ["Items"] = ["취소된 구매 항목은 수정할 수 없습니다."] });
                }

                if (item.ExpectedRowVersion is not null && current.RowVersion != item.ExpectedRowVersion)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProcurementMutationResult<ProcurementListResponse>.Conflict(ProcurementDomain.StaleVersionMessage);
                }

                var normalizedItem = item with { ReceiptCompletedAtUtc = NormalizeUtc(item.ReceiptCompletedAtUtc) };
                var changes = CollectReceiptChanges(current, normalizedItem);
                if (changes.Count == 0)
                {
                    continue;
                }

                var completed = normalizedItem.ReceiptCompleted ?? current.ReceiptCompleted;
                DateTimeOffset? completedAt = completed
                    ? normalizedItem.ReceiptCompletedAtUtc ?? NormalizeUtc(current.ReceiptCompletedAtUtc) ?? timeProvider.GetUtcNow()
                    : null;
                Guid? completedBy = completed ? changedByUserId : null;

                var receiptReason = string.IsNullOrWhiteSpace(request.Reason) && current.ReceiptCompleted && normalizedItem.ReceiptCompleted == false
                    ? "입고 완료 체크 해제"
                    : request.Reason;
                var receiptCompletionNote = normalizedItem.ReceiptCompletionNote is null
                    ? current.ReceiptCompletionNote
                    : ProcurementDomain.TrimToNull(normalizedItem.ReceiptCompletionNote);
                await UpdateReceiptAsync(connection, transaction, current.ItemId, completed, NormalizeUtc(completedAt), completedBy, receiptCompletionNote, changedByUserId, cancellationToken);
                foreach (var change in changes)
                {
                    await InsertAuditAsync(connection, transaction, current.ProjectId, current.ItemId, change.FieldName, change.OldValue, change.NewValue, receiptReason, changedByUserId, correlationId, "Direct", null, null, null, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }

        return ProcurementMutationResult<ProcurementListResponse>.Success(await GetMaterialReceiptsAsync(null, includeCompleted: false, null, null, cancellationToken));
    }

    public async Task<ProcurementMutationResult<ProcurementTemplateDownload>> CreateTemplateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var project = await ReadProjectAsync(dataSource, projectId, includeDeleted: false, cancellationToken);
        if (project is null)
        {
            return ProcurementMutationResult<ProcurementTemplateDownload>.NotFound();
        }

        var items = await ReadItemsForProjectsAsync(dataSource, [projectId], cancellationToken);
        return ProcurementMutationResult<ProcurementTemplateDownload>.Success(CreateTemplateWorkbook(project.ProjectTitle, project.ProjectCode, items));
    }

    public ProcurementTemplateDownload CreateTemplate()
    {
        return CreateTemplateWorkbook(null, null, []);
    }

    public async Task<IReadOnlyList<ProcurementRequiredItemSettingsResponse>> ListRequiredItemSettingsAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadRequiredItemSettingsAsync(connection, null, cancellationToken);
    }

    public async Task<ProcurementMutationResult<IReadOnlyList<ProcurementRequiredItemSettingsResponse>>> UpdateRequiredItemSettingsAsync(
        string itemCode,
        UpdateProcurementRequiredItemSettingsRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var normalizedItemCode = NormalizeItemCode(itemCode);
        var errors = ValidateRequiredItemSettings(request);
        if (errors.Count > 0)
        {
            return ProcurementMutationResult<IReadOnlyList<ProcurementRequiredItemSettingsResponse>>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        if (!await ActiveProductTypeExistsAsync(connection, transaction, normalizedItemCode, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProcurementMutationResult<IReadOnlyList<ProcurementRequiredItemSettingsResponse>>.NotFound();
        }

        Guid templateId;
        await using (var readTemplate = connection.CreateCommand())
        {
            readTemplate.Transaction = transaction;
            readTemplate.CommandText = """
                select id
                from procurement_required_item_templates
                where upper(btrim(item_code)) = @item_code
                  and is_active = true;
                """;
            readTemplate.Parameters.AddWithValue("item_code", normalizedItemCode);
            templateId = (Guid?)await readTemplate.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty;
        }

        if (templateId == Guid.Empty)
        {
            await using var insertTemplate = connection.CreateCommand();
            insertTemplate.Transaction = transaction;
            insertTemplate.CommandText = """
                insert into procurement_required_item_templates (
                    item_code,
                    version,
                    is_active,
                    created_by_user_id
                )
                values (
                    @item_code,
                    1,
                    true,
                    @created_by_user_id
                )
                returning id;
                """;
            insertTemplate.Parameters.AddWithValue("item_code", normalizedItemCode);
            insertTemplate.Parameters.AddWithValue("created_by_user_id", changedByUserId);
            templateId = (Guid)(await insertTemplate.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }
        else
        {
            await using var updateTemplate = connection.CreateCommand();
            updateTemplate.Transaction = transaction;
            updateTemplate.CommandText = """
                update procurement_required_item_templates
                set item_code = @item_code,
                    is_active = true
                where id = @template_id;

                delete from procurement_required_item_template_rows
                where template_id = @template_id;
                """;
            updateTemplate.Parameters.AddWithValue("item_code", normalizedItemCode);
            updateTemplate.Parameters.AddWithValue("template_id", templateId);
            await updateTemplate.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var row in request.Rows!.OrderBy(row => row.SequenceNumber!.Value))
        {
            await using var insertRow = connection.CreateCommand();
            insertRow.Transaction = transaction;
            insertRow.CommandText = """
                insert into procurement_required_item_template_rows (
                    template_id,
                    sequence_number,
                    item_name,
                    normalized_item_name,
                    is_required,
                    is_active
                )
                values (
                    @template_id,
                    @sequence_number,
                    @item_name,
                    @normalized_item_name,
                    @is_required,
                    @is_active
                );
                """;
            var itemName = row.ItemName!.Trim();
            insertRow.Parameters.AddWithValue("template_id", templateId);
            insertRow.Parameters.AddWithValue("sequence_number", row.SequenceNumber!.Value);
            insertRow.Parameters.AddWithValue("item_name", itemName);
            insertRow.Parameters.AddWithValue("normalized_item_name", NormalizeRequiredItemName(itemName));
            insertRow.Parameters.AddWithValue("is_required", row.IsRequired ?? true);
            insertRow.Parameters.AddWithValue("is_active", row.IsActive ?? true);
            await insertRow.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ProcurementMutationResult<IReadOnlyList<ProcurementRequiredItemSettingsResponse>>.Success(
            await ReadRequiredItemSettingsAsync(connection, null, cancellationToken));
    }

    private static ProcurementTemplateDownload CreateTemplateWorkbook(
        string? projectTitle,
        string? projectCode,
        IReadOnlyList<ProcurementItemSnapshot> items)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Procurement Plan");
        worksheet.Cell(1, 1).Value = "PS 사업부 PJT 발주 관리";
        worksheet.Range(1, 1, 1, 10).Merge().Style.Font.Bold = true;
        worksheet.Cell(2, 1).Value = "구매정보는 필수 입력값이 없습니다. 일부 값만 입력해도 저장할 수 있습니다.";
        worksheet.Range(2, 1, 2, 10).Merge().Style.Font.Italic = true;
        var headers = new[] { "PJT", "PJT CODE", "통상납기", "발주품목", "기술 담당자", "발주일", "입고일", "출하일", "이슈사항", "입고 완료" };
        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(3, i + 1).Value = headers[i];
            worksheet.Cell(3, i + 1).Style.Font.Bold = true;
        }

        worksheet.SheetView.FreezeRows(3);
        worksheet.Range(3, 1, 3, headers.Length).SetAutoFilter();
        var rowNumber = 4;
        foreach (var item in items.OrderBy(item => item.SequenceNumber))
        {
            worksheet.Cell(rowNumber, 1).Value = item.SourceProjectText ?? projectTitle ?? "";
            worksheet.Cell(rowNumber, 2).Value = item.SourceProjectCodeText ?? projectCode ?? "";
            worksheet.Cell(rowNumber, 3).Value = item.StandardLeadTime ?? "";
            worksheet.Cell(rowNumber, 4).Value = item.OrderItem ?? "";
            worksheet.Cell(rowNumber, 5).Value = item.TechnicalOwner ?? "";
            if (item.OrderDate is not null)
            {
                worksheet.Cell(rowNumber, 6).Value = item.OrderDate.Value.ToDateTime(TimeOnly.MinValue);
                worksheet.Cell(rowNumber, 6).Style.DateFormat.Format = "yyyy-mm-dd";
            }

            if (item.ExpectedReceiptDate is not null)
            {
                worksheet.Cell(rowNumber, 7).Value = item.ExpectedReceiptDate.Value.ToDateTime(TimeOnly.MinValue);
                worksheet.Cell(rowNumber, 7).Style.DateFormat.Format = "yyyy-mm-dd";
            }

            if (item.ProjectDeliveryDate is not null)
            {
                worksheet.Cell(rowNumber, 8).Value = item.ProjectDeliveryDate.Value.ToDateTime(TimeOnly.MinValue);
                worksheet.Cell(rowNumber, 8).Style.DateFormat.Format = "yyyy-mm-dd";
            }

            worksheet.Cell(rowNumber, 9).Value = item.IssueNote ?? "";
            worksheet.Cell(rowNumber, 10).Value = item.ReceiptCompleted ? "Y" : "";
            rowNumber++;
        }

        worksheet.Columns(1, headers.Length).AdjustToContents();
        for (var column = 1; column <= headers.Length; column++)
        {
            var min = column switch
            {
                4 or 9 => 20,
                6 or 7 or 8 => 13,
                _ => 14
            };
            var max = column switch
            {
                4 => 36,
                9 => 42,
                _ => 24
            };
            worksheet.Column(column).Width = Math.Clamp(worksheet.Column(column).Width + 2, min, max);
        }
        worksheet.Columns(1, headers.Length).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ProcurementTemplateDownload(
            stream.ToArray(),
            "Procurement_Plan_Template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<ProcurementMutationResult<ProcurementExcelPreviewResponse>> PreviewExcelAsync(
        UploadedExcelFile file,
        IReadOnlyList<ProcurementExcelProjectSelection> selections,
        CancellationToken cancellationToken)
    {
        var parsed = await excelParser.ParseAsync(file, cancellationToken);
        if (parsed.FileErrors.Count > 0)
        {
            return ProcurementMutationResult<ProcurementExcelPreviewResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = parsed.FileErrors.ToArray() });
        }

        await using var dataSource = CreateDataSource();
        var projects = await ReadProjectsForMatchingAsync(dataSource, cancellationToken);
        var selected = selections.ToDictionary(item => item.SourceGroupSequence, item => item.ProjectId);
        var preview = await BuildExcelPreviewAsync(dataSource, parsed, file, projects, selected, cancellationToken);
        return ProcurementMutationResult<ProcurementExcelPreviewResponse>.Success(preview);
    }

    public async Task<ProcurementMutationResult<ProcurementListResponse>> ApplyExcelAsync(
        UploadedExcelFile file,
        string expectedFileSha256,
        IReadOnlyList<ProcurementExcelProjectSelection> selections,
        IReadOnlyDictionary<Guid, int> expectedVersions,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(file.FileSha256, expectedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return ProcurementMutationResult<ProcurementListResponse>.Conflict("Excel 파일이 미리보기 때와 다릅니다. 새 Preview를 실행해 주세요.");
        }

        var parsed = await excelParser.ParseAsync(file, cancellationToken);
        if (parsed.FileErrors.Count > 0)
        {
            return ProcurementMutationResult<ProcurementListResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = parsed.FileErrors.ToArray() });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var projects = await ReadProjectsForMatchingAsync(connection, transaction, cancellationToken);
            var selected = selections.ToDictionary(item => item.SourceGroupSequence, item => item.ProjectId);
            var preview = await BuildExcelPreviewAsync(connection, transaction, parsed, file, projects, selected, cancellationToken);

            if (preview.ReasonRequired && string.IsNullOrWhiteSpace(reason))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProcurementMutationResult<ProcurementListResponse>.Validation(
                    new Dictionary<string, string[]> { ["Reason"] = ["기존 구매정보 또는 입고 완료를 변경하려면 수정사유가 필요합니다."] });
            }

            var affectedProjectIds = preview.Rows
                .Where(row => row.ResultType is "New" or "Changed")
                .Select(row => row.ProjectId)
                .OfType<Guid>()
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            var matchedProjectIds = preview.ProjectMatches
                .Select(match => match.MatchedProjectId)
                .OfType<Guid>()
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (matchedProjectIds.Length > 0)
            {
                await LockProjectsAsync(connection, transaction, matchedProjectIds, cancellationToken);
            }

            if (affectedProjectIds.Length == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (preview.ErrorCount > 0 || preview.NeedsReviewCount > 0)
                {
                    return ProcurementMutationResult<ProcurementListResponse>.Validation(
                        new Dictionary<string, string[]> { ["Rows"] = ["저장 가능한 항목이 없습니다. 오류 행 또는 확인 필요 항목을 처리해 주세요."] });
                }

                return ProcurementMutationResult<ProcurementListResponse>.Success(await GetMaterialReceiptsAsync(null, includeCompleted: false, null, null, cancellationToken));
            }

            await LockProjectItemsAsync(connection, transaction, affectedProjectIds, cancellationToken);
            foreach (var version in preview.ExpectedVersions)
            {
                if (!expectedVersions.TryGetValue(version.ItemId, out var expected) || expected != version.ExpectedRowVersion)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ProcurementMutationResult<ProcurementListResponse>.Conflict(ProcurementDomain.StaleVersionMessage);
                }
            }

            var appliedRows = preview.Rows.Where(row => row.ResultType is "New" or "Changed").ToArray();
            var batchId = await InsertImportBatchAsync(connection, transaction, file, appliedRows, changedByUserId, reason, cancellationToken);
            foreach (var projectId in affectedProjectIds)
            {
                await InsertImportBatchProjectAsync(connection, transaction, batchId, projectId, cancellationToken);
            }

            foreach (var row in preview.Rows.Where(row => row.ResultType is "New" or "Changed"))
            {
                var parsedRow = parsed.Rows.First(item => item.ExcelRowNumber == row.ExcelRowNumber);
                if (row.ProjectId is null)
                {
                    continue;
                }

                if (row.ItemId is null)
                {
                    var inserted = await InsertItemAsync(connection, transaction, row.ProjectId.Value, parsedRow, changedByUserId, cancellationToken);
                    foreach (var change in CollectNewItemChanges(inserted))
                    {
                        await InsertAuditAsync(connection, transaction, inserted.ProjectId, inserted.ItemId, change.FieldName, null, change.NewValue, reason, changedByUserId, correlationId, "Excel", batchId, null, change.OriginalInputValue, cancellationToken);
                    }
                }
                else
                {
                    var current = await LockItemByIdAsync(connection, transaction, row.ItemId.Value, cancellationToken);
                    if (current is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ProcurementMutationResult<ProcurementListResponse>.Conflict(ProcurementDomain.StaleVersionMessage);
                    }

                    var changes = CollectExcelChanges(current, parsedRow);
                    if (changes.Count == 0)
                    {
                        continue;
                    }

                    await UpdateItemFromExcelAsync(connection, transaction, current, parsedRow, changedByUserId, cancellationToken);
                    foreach (var change in changes)
                    {
                        await InsertAuditAsync(connection, transaction, current.ProjectId, current.ItemId, change.FieldName, change.OldValue, change.NewValue, reason, changedByUserId, correlationId, "Excel", batchId, null, change.OriginalInputValue, cancellationToken);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }

        return ProcurementMutationResult<ProcurementListResponse>.Success(await GetMaterialReceiptsAsync(null, includeCompleted: false, null, null, cancellationToken));
    }

    public async Task<ProcurementHistoryResponse?> GetHistoryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        if (await ReadProjectAsync(dataSource, projectId, includeDeleted: false, cancellationToken) is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select a.id, a.entity_id, a.field_name, a.old_value, a.new_value, a.reason, a.changed_by_user_id,
                   u.display_name, a.changed_at_utc, a.correlation_id, coalesce(a.input_source, 'Direct') as input_source,
                   a.procurement_import_batch_id, b.original_file_name, i.sequence_number
            from project_audit_events a
            left join qms_users u on u.id = a.changed_by_user_id
            left join procurement_excel_import_batch_projects bp on bp.import_batch_id = a.procurement_import_batch_id and bp.project_id = a.project_id
            left join procurement_excel_import_batches b on b.id = bp.import_batch_id
            left join project_procurement_items i on i.id = a.entity_id
            where a.project_id = @project_id and a.entity_type = 'ProcurementItem'
            order by a.changed_at_utc desc, a.id desc;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        var events = new List<HistoryEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new HistoryEvent(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetGuid(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetInt32(13)));
        }
        await reader.DisposeAsync();

        var groups = events
            .GroupBy(item => item.ImportBatchId?.ToString("D", CultureInfo.InvariantCulture) ?? (string.IsNullOrWhiteSpace(item.CorrelationId) ? item.AuditId.ToString("D", CultureInfo.InvariantCulture) : item.CorrelationId))
            .Select(group =>
            {
                var first = group.OrderByDescending(item => item.ChangedAtUtc).ThenByDescending(item => item.AuditId).First();
                return new ProcurementHistoryGroupResponse
                {
                    GroupId = group.Key,
                    InputSource = first.InputSource,
                    ChangedByUserId = first.ChangedByUserId,
                    ChangedByName = first.ChangedByUserName,
                    ChangedAtUtc = first.ChangedAtUtc,
                    Reason = first.Reason,
                    ImportBatchId = first.ImportBatchId,
                    ImportFileName = first.ImportFileName,
                    AffectedItemCount = group.Select(item => item.EntityId).Distinct().Count(),
                    ChangeCount = group.Count(),
                    Changes = group.Select(item => new ProcurementHistoryChangeResponse
                    {
                        EntityId = item.EntityId,
                        SequenceNumber = item.SequenceNumber,
                        FieldName = item.FieldName,
                        OldValue = item.OldValue,
                        NewValue = item.NewValue
                    }).ToList()
                };
            })
            .OrderByDescending(group => group.ChangedAtUtc)
            .ToList();

        var batches = await ReadImportBatchesAsync(connection, projectId, cancellationToken);
        return new ProcurementHistoryResponse(groups, batches);
    }

    private async Task<ProcurementMutationResult<ProcurementResponse>> PersistDirectUpdatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProcurementProjectSnapshot project,
        IReadOnlyList<ProcurementItemSnapshot> existing,
        IReadOnlyList<ProcurementItemUpdateRequest> updates,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var byId = existing.ToDictionary(item => item.ItemId);
        if (updates.Where(item => item.ItemId is not null).Select(item => item.ItemId!.Value).Distinct().Count()
            != updates.Count(item => item.ItemId is not null))
        {
            return ProcurementMutationResult<ProcurementResponse>.Validation(
                new Dictionary<string, string[]> { ["Items"] = ["중복된 구매 항목이 있습니다."] });
        }

        var nextSequence = existing.Count == 0 ? 1 : existing.Max(item => item.SequenceNumber) + 1;
        foreach (var update in updates)
        {
            if (update.ItemId is null)
            {
                if (IsEmptyNewDirectRow(update))
                {
                    continue;
                }

                var row = new ParsedProcurementExcelRow(0, 0, project.ProjectTitle, project.ProjectCode, update.StandardLeadTime, update.OrderItem, update.TechnicalOwner, update.OrderDate, update.ExpectedReceiptDate, null, update.IssueNote, update.ReceiptCompleted, false, []);
                var inserted = await InsertItemAsync(connection, transaction, project.ProjectId, row, changedByUserId, cancellationToken, nextSequence++);
                foreach (var change in CollectNewItemChanges(inserted))
                {
                    await InsertAuditAsync(connection, transaction, project.ProjectId, inserted.ItemId, change.FieldName, null, change.NewValue, reason, changedByUserId, correlationId, "Direct", null, null, null, cancellationToken);
                }

                continue;
            }

            if (!byId.TryGetValue(update.ItemId.Value, out var current))
            {
                return ProcurementMutationResult<ProcurementResponse>.Validation(
                    new Dictionary<string, string[]> { ["Items"] = ["대상 구매 항목은 해당 프로젝트에 속해야 합니다."] });
            }

            if (update.ExpectedRowVersion is not null && current.RowVersion != update.ExpectedRowVersion)
            {
                return ProcurementMutationResult<ProcurementResponse>.Conflict(ProcurementDomain.StaleVersionMessage);
            }

            var changes = CollectDirectChanges(current, update);
            if (changes.Count == 0)
            {
                continue;
            }

            await UpdateItemDirectAsync(connection, transaction, current, update, changedByUserId, cancellationToken);
            var auditReason = string.IsNullOrWhiteSpace(reason) && current.ReceiptCompleted && update.ReceiptCompleted == false
                ? "입고 완료 체크 해제"
                : reason;
            foreach (var change in changes)
            {
                await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId, change.FieldName, change.OldValue, change.NewValue, auditReason, changedByUserId, correlationId, "Direct", null, null, null, cancellationToken);
            }
        }

        return ProcurementMutationResult<ProcurementResponse>.Success(new ProcurementResponse(project.ProjectId, project.ProjectTitle, project.ProjectCode, project.DeliveryDate, []));
    }

    private static bool IsEmptyNewDirectRow(ProcurementItemUpdateRequest update)
    {
        return string.IsNullOrWhiteSpace(update.StandardLeadTime)
            && string.IsNullOrWhiteSpace(update.OrderItem)
            && string.IsNullOrWhiteSpace(update.TechnicalOwner)
            && update.OrderDate is null
            && update.ExpectedReceiptDate is null
            && string.IsNullOrWhiteSpace(update.IssueNote)
            && update.ReceiptCompleted is null
            && update.ReceiptCompletedAtUtc is null
            && string.IsNullOrWhiteSpace(update.ReceiptCompletionNote);
    }

    private async Task<ProcurementExcelPreviewResponse> BuildExcelPreviewAsync(
        NpgsqlDataSource dataSource,
        ParsedProcurementExcelFile parsed,
        UploadedExcelFile file,
        IReadOnlyList<ProcurementProjectSnapshot> projects,
        IReadOnlyDictionary<int, Guid> selections,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await BuildExcelPreviewAsync(connection, null, parsed, file, projects, selections, cancellationToken);
    }

    private async Task<ProcurementExcelPreviewResponse> BuildExcelPreviewAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ParsedProcurementExcelFile parsed,
        UploadedExcelFile file,
        IReadOnlyList<ProcurementProjectSnapshot> projects,
        IReadOnlyDictionary<int, Guid> selections,
        CancellationToken cancellationToken)
    {
        var matchByGroup = BuildProjectMatches(parsed, projects, selections);
        var matchedProjectIds = matchByGroup.Values.Select(item => item.MatchedProjectId).OfType<Guid>().Distinct().ToArray();
        var existing = await ReadItemsForProjectsAsync(connection, transaction, matchedProjectIds, cancellationToken);
        var parsedRowsByProjectGroup = parsed.Rows
            .Where(row => !row.IsSkipped)
            .GroupBy(row => row.SourceGroupSequence)
            .ToDictionary(group => group.Key, group => group.Count());
        var existingByProjectGroup = existing
            .Where(item => item.SourceGroupSequence is not null)
            .GroupBy(item => (item.ProjectId, item.SourceGroupSequence!.Value))
            .Where(group => group.Count() == 1
                && parsedRowsByProjectGroup.TryGetValue(group.Key.Item2, out var parsedRowCount)
                && parsedRowCount == 1)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SequenceNumber).First());
        var uniqueMatchKey = existing
            .Where(item => item.RowMatchKey is not null)
            .GroupBy(item => (item.ProjectId, item.RowMatchKey!))
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());

        var rows = new List<ProcurementExcelPreviewRowResponse>();
        var touchedItemIds = new HashSet<Guid>();
        var reasonRequired = false;
        foreach (var row in parsed.Rows)
        {
            if (row.IsSkipped)
            {
                rows.Add(ToPreviewRow(row, "Skipped", null, null, row.ErrorMessages));
                continue;
            }

            if (!matchByGroup.TryGetValue(row.SourceGroupSequence, out var match) || match.MatchedProjectId is null || match.MatchStatus != "Matched")
            {
                var message = match?.MatchStatus == "Unmatched"
                    ? "등록되지 않은 프로젝트입니다."
                    : "확인할 프로젝트가 있습니다. 프로젝트를 선택해 주세요.";
                rows.Add(ToPreviewRow(row, match?.MatchStatus == "Unmatched" ? "Error" : "NeedsReview", null, null, [.. row.ErrorMessages, message]));
                continue;
            }

            if (row.ErrorMessages.Count > 0)
            {
                rows.Add(ToPreviewRow(row, "Error", match.MatchedProjectId, null, row.ErrorMessages));
                continue;
            }

            ProcurementItemSnapshot? current = null;
            if (existingByProjectGroup.TryGetValue((match.MatchedProjectId.Value, row.SourceGroupSequence), out var byGroup))
            {
                current = byGroup;
            }
            else if (row.RowMatchKey is not null && uniqueMatchKey.TryGetValue((match.MatchedProjectId.Value, row.RowMatchKey), out var byKey))
            {
                current = byKey;
            }

            if (current is null)
            {
                rows.Add(ToPreviewRow(row, "New", match.MatchedProjectId, null, []));
                continue;
            }

            touchedItemIds.Add(current.ItemId);
            var changes = CollectExcelChanges(current, row);
            if (changes.Count > 0 && !IsReceiptCompletedUncheckOnly(changes))
            {
                reasonRequired = true;
            }
            rows.Add(ToPreviewRow(row, changes.Count == 0 ? "Unchanged" : "Changed", match.MatchedProjectId, current, []));
        }

        foreach (var missing in existing.Where(item => !touchedItemIds.Contains(item.ItemId)))
        {
            rows.Add(new ProcurementExcelPreviewRowResponse
            {
                ExcelRowNumber = 0,
                SourceGroupSequence = missing.SourceGroupSequence ?? 0,
                ProjectId = missing.ProjectId,
                ItemId = missing.ItemId,
                ExpectedRowVersion = missing.RowVersion,
                ResultType = "MissingFromUpload",
                SourceProjectText = missing.SourceProjectText,
                SourceProjectCodeText = missing.SourceProjectCodeText,
                StandardLeadTime = missing.StandardLeadTime,
                OrderItem = missing.OrderItem,
                TechnicalOwner = missing.TechnicalOwner,
                OrderDate = missing.OrderDate,
                ExpectedReceiptDate = missing.ExpectedReceiptDate,
                IssueNote = missing.IssueNote,
                ReceiptCompleted = missing.ReceiptCompleted
            });
        }

        var expectedVersions = rows
            .Where(row => row.ResultType == "Changed" && row.ItemId is not null && row.ExpectedRowVersion is not null)
            .Select(row => new ProcurementExcelExpectedVersion(row.ItemId!.Value, row.ExpectedRowVersion!.Value))
            .Distinct()
            .ToList();

        return new ProcurementExcelPreviewResponse
        {
            FileSha256 = file.FileSha256,
            TotalRows = parsed.TotalRows,
            NewCount = rows.Count(row => row.ResultType == "New"),
            ChangedCount = rows.Count(row => row.ResultType == "Changed"),
            UnchangedCount = rows.Count(row => row.ResultType == "Unchanged"),
            SkippedCount = rows.Count(row => row.ResultType == "Skipped"),
            MissingFromUploadCount = rows.Count(row => row.ResultType == "MissingFromUpload"),
            NeedsReviewCount = rows.Count(row => row.ResultType == "NeedsReview"),
            ErrorCount = rows.Count(row => row.ResultType == "Error"),
            ReasonRequired = reasonRequired,
            ProjectMatches = matchByGroup.Values.OrderBy(match => match.SourceGroupSequence).ToList(),
            Rows = rows.OrderBy(row => row.ExcelRowNumber == 0 ? int.MaxValue : row.ExcelRowNumber).ToList(),
            ExpectedVersions = expectedVersions
        };
    }

    private static bool IsReceiptCompletedUncheckOnly(IReadOnlyList<Change> changes)
    {
        return changes.Count == 1
            && changes[0].FieldName == "ReceiptCompleted"
            && string.Equals(changes[0].NewValue, "False", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<int, ProcurementExcelProjectMatchResponse> BuildProjectMatches(
        ParsedProcurementExcelFile parsed,
        IReadOnlyList<ProcurementProjectSnapshot> projects,
        IReadOnlyDictionary<int, Guid> selections)
    {
        var groupRows = parsed.Rows
            .Where(row => row.SourceGroupSequence > 0)
            .GroupBy(row => row.SourceGroupSequence);
        var result = new Dictionary<int, ProcurementExcelProjectMatchResponse>();
        foreach (var group in groupRows)
        {
            var first = group.First();
            var candidates = new List<ProcurementProjectCandidateResponse>();
            var selectedProject = selections.TryGetValue(group.Key, out var selectedProjectId)
                ? projects.FirstOrDefault(project => project.ProjectId == selectedProjectId)
                : null;
            if (selectedProject is not null)
            {
                result[group.Key] = Match(group.Key, first, selectedProject, "Matched", [new ProcurementProjectCandidateResponse(selectedProject.ProjectId, selectedProject.ProjectTitle, selectedProject.ProjectCode, "Selected")]);
                continue;
            }

            var normalizedCode = ProcurementDomain.NormalizeProjectKey(first.SourceProjectCodeText);
            var codeMatches = projects
                .Where(project => !string.IsNullOrEmpty(normalizedCode) && ProcurementDomain.NormalizeProjectKey(project.ProjectCode) == normalizedCode)
                .ToList();
            if (codeMatches.Count == 1)
            {
                result[group.Key] = Match(group.Key, first, codeMatches[0], "Matched", [new ProcurementProjectCandidateResponse(codeMatches[0].ProjectId, codeMatches[0].ProjectTitle, codeMatches[0].ProjectCode, "Code")]);
                continue;
            }

            if (codeMatches.Count > 1)
            {
                result[group.Key] = new ProcurementExcelProjectMatchResponse
                {
                    SourceGroupSequence = group.Key,
                    ExcelProjectTitle = first.SourceProjectText,
                    ExcelProjectCode = first.SourceProjectCodeText,
                    MatchStatus = "NeedsReview",
                    Candidates = codeMatches
                        .Select(project => new ProcurementProjectCandidateResponse(project.ProjectId, project.ProjectTitle, project.ProjectCode, "Code"))
                        .Take(10)
                        .ToList()
                };
                continue;
            }

            var normalizedTitle = ProcurementDomain.NormalizeProjectKey(first.SourceProjectText);
            var exact = projects.Where(project => ProcurementDomain.NormalizeProjectKey(project.ProjectTitle) == normalizedTitle).ToList();
            if (exact.Count == 1)
            {
                result[group.Key] = Match(group.Key, first, exact[0], "Matched", [new ProcurementProjectCandidateResponse(exact[0].ProjectId, exact[0].ProjectTitle, exact[0].ProjectCode, "ExactTitle")]);
                continue;
            }

            candidates.AddRange(exact
                .Select(project => new ProcurementProjectCandidateResponse(project.ProjectId, project.ProjectTitle, project.ProjectCode, "ExactTitle")));
            candidates.AddRange(projects
                .Where(project => !string.IsNullOrEmpty(normalizedTitle) && ProcurementDomain.NormalizeProjectKey(project.ProjectTitle).Contains(normalizedTitle, StringComparison.Ordinal))
                .Select(project => new ProcurementProjectCandidateResponse(project.ProjectId, project.ProjectTitle, project.ProjectCode, "TitleCandidate")));
            candidates = candidates
                .GroupBy(candidate => candidate.ProjectId)
                .Select(grouped => grouped.First())
                .Take(5)
                .ToList();

            result[group.Key] = new ProcurementExcelProjectMatchResponse
            {
                SourceGroupSequence = group.Key,
                ExcelProjectTitle = first.SourceProjectText,
                ExcelProjectCode = first.SourceProjectCodeText,
                MatchStatus = candidates.Count == 0 ? "Unmatched" : "NeedsReview",
                Candidates = candidates
            };
        }

        return result;
    }

    private static ProcurementExcelProjectMatchResponse Match(
        int group,
        ParsedProcurementExcelRow row,
        ProcurementProjectSnapshot project,
        string status,
        IReadOnlyList<ProcurementProjectCandidateResponse> candidates)
    {
        return new ProcurementExcelProjectMatchResponse
        {
            SourceGroupSequence = group,
            ExcelProjectTitle = row.SourceProjectText,
            ExcelProjectCode = row.SourceProjectCodeText,
            MatchedProjectId = project.ProjectId,
            MatchedProjectTitle = project.ProjectTitle,
            MatchedProjectCode = project.ProjectCode,
            MatchStatus = status,
            Candidates = candidates
        };
    }

    private static ProcurementExcelPreviewRowResponse ToPreviewRow(
        ParsedProcurementExcelRow row,
        string resultType,
        Guid? projectId,
        ProcurementItemSnapshot? current,
        IReadOnlyList<string> errors)
    {
        return new ProcurementExcelPreviewRowResponse
        {
            ExcelRowNumber = row.ExcelRowNumber,
            SourceGroupSequence = row.SourceGroupSequence,
            ProjectId = projectId,
            ItemId = current?.ItemId,
            ExpectedRowVersion = current?.RowVersion,
            ResultType = resultType,
            SourceProjectText = row.SourceProjectText,
            SourceProjectCodeText = row.SourceProjectCodeText,
            StandardLeadTime = row.StandardLeadTime,
            OrderItem = row.OrderItem,
            TechnicalOwner = row.TechnicalOwner,
            OrderDate = row.OrderDate,
            ExpectedReceiptDate = row.ExpectedReceiptDate,
            ShipmentText = row.ShipmentText,
            IssueNote = row.IssueNote,
            ReceiptCompleted = row.ReceiptCompleted,
            ErrorMessages = errors
        };
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

    private static ProcurementResponse BuildProjectResponse(ProcurementProjectSnapshot project, IReadOnlyList<ProcurementItemSnapshot> items)
    {
        return new ProcurementResponse(
            project.ProjectId,
            project.ProjectTitle,
            project.ProjectCode,
            project.DeliveryDate,
            items.OrderBy(item => item.SequenceNumber).Select(ToResponse).ToList());
    }

    private ProcurementItemSnapshot ToSnapshotWithComputedReceipt(ParsedProcurementExcelRow row, Guid projectId, int sequence, Guid changedByUserId, Guid itemId)
    {
        var completed = row.ReceiptCompleted == true;
        return new ProcurementItemSnapshot(
            itemId,
            projectId,
            "",
            "",
            null,
            sequence,
            row.SourceProjectText,
            row.SourceProjectCodeText,
            row.StandardLeadTime,
            row.OrderItem,
            row.TechnicalOwner,
            row.OrderDate,
            row.ExpectedReceiptDate,
            row.IssueNote,
            completed,
            completed ? timeProvider.GetUtcNow() : null,
            completed ? changedByUserId : null,
            null,
            null,
            1,
            row.ExcelRowNumber == 0 ? null : row.ExcelRowNumber,
            row.SourceGroupSequence == 0 ? null : row.SourceGroupSequence,
            row.RowMatchKey,
            "Active");
    }

    private static ProcurementItemResponse ToResponse(ProcurementItemSnapshot item)
    {
        return new ProcurementItemResponse
        {
            ItemId = item.ItemId,
            ProjectId = item.ProjectId,
            ProjectTitle = item.ProjectTitle,
            ProjectCode = item.ProjectCode,
            ProjectDeliveryDate = item.ProjectDeliveryDate,
            ShipmentDisplayDate = item.ProjectDeliveryDate,
            SequenceNumber = item.SequenceNumber,
            SourceProjectText = item.SourceProjectText,
            SourceProjectCodeText = item.SourceProjectCodeText,
            StandardLeadTime = item.StandardLeadTime,
            OrderItem = item.OrderItem,
            TechnicalOwner = item.TechnicalOwner,
            OrderDate = item.OrderDate,
            ExpectedReceiptDate = item.ExpectedReceiptDate,
            IssueNote = item.IssueNote,
            ReceiptCompleted = item.ReceiptCompleted,
            ReceiptCompletedAtUtc = item.ReceiptCompletedAtUtc,
            ReceiptCompletedByUserId = item.ReceiptCompletedByUserId,
            ReceiptCompletedByUserName = item.ReceiptCompletedByUserName,
            ReceiptCompletionNote = item.ReceiptCompletionNote,
            RowVersion = item.RowVersion,
            DDayText = ProcurementDomain.BuildDDayText(item.ExpectedReceiptDate, DateOnly.FromDateTime(DateTime.UtcNow))
        };
    }

    private async Task<ProcurementProjectSnapshot?> ReadProjectAsync(NpgsqlDataSource dataSource, Guid projectId, bool includeDeleted, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProjectAsync(connection, null, projectId, includeDeleted, cancellationToken);
    }

    private static async Task<ProcurementProjectSnapshot?> ReadProjectAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, bool includeDeleted, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, project_title, project_code, project_key, delivery_date, status, deleted_at_utc
            from projects
            where id = @project_id and (@include_deleted or deleted_at_utc is null);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("include_deleted", includeDeleted);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadProject(reader)
            : null;
    }

    private static async Task<ProcurementProjectSnapshot?> LockProjectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, project_title, project_code, project_key, delivery_date, status, deleted_at_utc
            from projects
            where id = @project_id
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    private static async Task<IReadOnlyList<ProcurementProjectSnapshot>> ReadProjectsForMatchingAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProjectsForMatchingAsync(connection, null, cancellationToken);
    }

    private static async Task<IReadOnlyList<ProcurementProjectSnapshot>> ReadProjectsForMatchingAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, project_title, project_code, project_key, delivery_date, status, deleted_at_utc
            from projects
            where deleted_at_utc is null
            order by project_title;
            """;
        var projects = new List<ProcurementProjectSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(ReadProject(reader));
        }

        return projects;
    }

    private static async Task LockProjectsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from projects where id = any(@ids) order by id for update;";
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("ids", projectIds));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ProcurementProjectSnapshot ReadProject(NpgsqlDataReader reader)
    {
        return new ProcurementProjectSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateOnly>(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
    }

    private async Task<IReadOnlyList<ProcurementItemSnapshot>> ReadItemsForProjectsAsync(NpgsqlDataSource dataSource, Guid[] projectIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadItemsForProjectsAsync(connection, null, projectIds, cancellationToken);
    }

    private static async Task<IReadOnlyList<ProcurementItemSnapshot>> ReadItemsForProjectsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select i.id, i.project_id, p.project_title, p.project_code, p.delivery_date, i.sequence_number,
                   i.source_project_text, i.source_project_code_text, i.standard_lead_time, i.order_item,
                   i.technical_owner, i.order_date, i.expected_receipt_date, i.issue_note,
                   i.receipt_completed, i.receipt_completed_at_utc, i.receipt_completed_by_user_id,
                   u.display_name, i.receipt_completion_note, i.row_version, i.source_excel_row_number,
                   i.source_group_sequence, i.row_match_key, i.status
            from project_procurement_items i
            join projects p on p.id = i.project_id
            left join qms_users u on u.id = i.receipt_completed_by_user_id
            where i.project_id = any(@project_ids) and i.status = 'Active'
            order by i.project_id, i.sequence_number;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("project_ids", projectIds));
        var items = new List<ProcurementItemSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static async Task<IReadOnlyList<ProcurementItemSnapshot>> LockProjectItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        return await LockProjectItemsAsync(connection, transaction, [projectId], cancellationToken);
    }

    private static async Task<IReadOnlyList<ProcurementItemSnapshot>> LockProjectItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select i.id, i.project_id, p.project_title, p.project_code, p.delivery_date, i.sequence_number,
                   i.source_project_text, i.source_project_code_text, i.standard_lead_time, i.order_item,
                   i.technical_owner, i.order_date, i.expected_receipt_date, i.issue_note,
                   i.receipt_completed, i.receipt_completed_at_utc, i.receipt_completed_by_user_id,
                   u.display_name, i.receipt_completion_note, i.row_version, i.source_excel_row_number,
                   i.source_group_sequence, i.row_match_key, i.status
            from project_procurement_items i
            join projects p on p.id = i.project_id
            left join qms_users u on u.id = i.receipt_completed_by_user_id
            where i.project_id = any(@project_ids) and i.status = 'Active'
            order by i.project_id, i.sequence_number
            for update of i;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("project_ids", projectIds));
        var items = new List<ProcurementItemSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static async Task<IReadOnlyList<ProcurementItemSnapshot>> LockItemsByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select i.id, i.project_id, p.project_title, p.project_code, p.delivery_date, i.sequence_number,
                   i.source_project_text, i.source_project_code_text, i.standard_lead_time, i.order_item,
                   i.technical_owner, i.order_date, i.expected_receipt_date, i.issue_note,
                   i.receipt_completed, i.receipt_completed_at_utc, i.receipt_completed_by_user_id,
                   u.display_name, i.receipt_completion_note, i.row_version, i.source_excel_row_number,
                   i.source_group_sequence, i.row_match_key, i.status
            from project_procurement_items i
            join projects p on p.id = i.project_id
            left join qms_users u on u.id = i.receipt_completed_by_user_id
            where i.id = any(@item_ids)
            order by i.project_id, i.sequence_number
            for update of i;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("item_ids", itemIds));
        var items = new List<ProcurementItemSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static async Task<ProcurementItemSnapshot?> LockItemByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, CancellationToken cancellationToken)
    {
        return (await LockItemsByIdAsync(connection, transaction, [itemId], cancellationToken)).FirstOrDefault();
    }

    private async Task<ProcurementItemSnapshot> InsertItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        ParsedProcurementExcelRow row,
        Guid changedByUserId,
        CancellationToken cancellationToken,
        int? sequenceNumber = null)
    {
        var itemId = Guid.NewGuid();
        var sequence = sequenceNumber ?? await NextSequenceAsync(connection, transaction, projectId, cancellationToken);
        var snapshot = ToSnapshotWithComputedReceipt(row, projectId, sequence, changedByUserId, itemId);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_procurement_items (
                id, project_id, sequence_number, source_project_text, source_project_code_text,
                standard_lead_time, order_item, technical_owner, order_date, expected_receipt_date,
                issue_note, receipt_completed, receipt_completed_at_utc,
                receipt_completed_by_user_id, receipt_completion_note, row_version,
                source_excel_row_number, source_group_sequence, row_match_key,
                source_type, is_confirmed, created_by_user_id, updated_by_user_id)
            values (
                @id, @project_id, @sequence_number, @source_project_text, @source_project_code_text,
                @standard_lead_time, @order_item, @technical_owner, @order_date, @expected_receipt_date,
                @issue_note, @receipt_completed, @receipt_completed_at_utc,
                @receipt_completed_by_user_id, @receipt_completion_note, 1,
                @source_excel_row_number, @source_group_sequence, @row_match_key,
                @source_type, true, @user_id, @user_id);
            """;
        AddItemParameters(command, snapshot, changedByUserId);
        command.Parameters.AddWithValue("source_type", row.ExcelRowNumber > 0 ? "Excel" : "Direct");
        await command.ExecuteNonQueryAsync(cancellationToken);
        return snapshot;
    }

    private static async Task<int> NextSequenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(max(sequence_number), 0) + 1 from project_procurement_items where project_id = @project_id;";
        command.Parameters.AddWithValue("project_id", projectId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task UpdateItemDirectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ProcurementItemSnapshot current, ProcurementItemUpdateRequest update, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var parsed = new ParsedProcurementExcelRow(
            current.SourceExcelRowNumber ?? 0,
            current.SourceGroupSequence ?? 0,
            current.SourceProjectText,
            current.SourceProjectCodeText,
            ProcurementDomain.TrimToNull(update.StandardLeadTime),
            ProcurementDomain.TrimToNull(update.OrderItem),
            ProcurementDomain.TrimToNull(update.TechnicalOwner),
            update.OrderDate,
            update.ExpectedReceiptDate,
            null,
            ProcurementDomain.TrimToNull(update.IssueNote),
            update.ReceiptCompleted,
            false,
            []);
        await UpdateItemFromParsedAsync(connection, transaction, current, parsed, NormalizeUtc(update.ReceiptCompletedAtUtc), update.ReceiptCompletionNote, keepExistingWhenNull: false, changedByUserId, cancellationToken);
    }

    private static async Task UpdateItemFromExcelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ProcurementItemSnapshot current, ParsedProcurementExcelRow row, Guid changedByUserId, CancellationToken cancellationToken)
    {
        await UpdateItemFromParsedAsync(connection, transaction, current, row, null, null, keepExistingWhenNull: true, changedByUserId, cancellationToken);
    }

    private static async Task UpdateItemFromParsedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ProcurementItemSnapshot current, ParsedProcurementExcelRow row, DateTimeOffset? explicitReceiptAt, string? explicitReceiptNote, bool keepExistingWhenNull, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var completed = row.ReceiptCompleted ?? current.ReceiptCompleted;
        DateTimeOffset? completedAt = completed
            ? NormalizeUtc(explicitReceiptAt) ?? NormalizeUtc(current.ReceiptCompletedAtUtc) ?? DateTimeOffset.UtcNow
            : null;
        Guid? completedBy = completed ? changedByUserId : null;
        var standardLeadTime = keepExistingWhenNull && row.StandardLeadTime is null ? current.StandardLeadTime : row.StandardLeadTime;
        var orderItem = keepExistingWhenNull && row.OrderItem is null ? current.OrderItem : row.OrderItem;
        var technicalOwner = keepExistingWhenNull && row.TechnicalOwner is null ? current.TechnicalOwner : row.TechnicalOwner;
        var orderDate = keepExistingWhenNull && row.OrderDate is null ? current.OrderDate : row.OrderDate;
        var expectedReceiptDate = keepExistingWhenNull && row.ExpectedReceiptDate is null ? current.ExpectedReceiptDate : row.ExpectedReceiptDate;
        var issueNote = keepExistingWhenNull && row.IssueNote is null ? current.IssueNote : row.IssueNote;
        var receiptCompletionNote = keepExistingWhenNull && explicitReceiptNote is null ? current.ReceiptCompletionNote : ProcurementDomain.TrimToNull(explicitReceiptNote);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_procurement_items
            set standard_lead_time = @standard_lead_time,
                order_item = @order_item,
                technical_owner = @technical_owner,
                order_date = @order_date,
                expected_receipt_date = @expected_receipt_date,
                issue_note = @issue_note,
                receipt_completed = @receipt_completed,
                receipt_completed_at_utc = @receipt_completed_at_utc,
                receipt_completed_by_user_id = @receipt_completed_by_user_id,
                receipt_completion_note = @receipt_completion_note,
                source_excel_row_number = coalesce(@source_excel_row_number, source_excel_row_number),
                source_group_sequence = coalesce(@source_group_sequence, source_group_sequence),
                row_match_key = @row_match_key,
                source_type = case when @source_excel_row_number is null then 'Direct' else 'Excel' end,
                is_confirmed = true,
                row_version = row_version + 1,
                updated_at_utc = now(),
                updated_by_user_id = @user_id
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", current.ItemId);
        command.Parameters.AddWithValue("standard_lead_time", (object?)standardLeadTime ?? DBNull.Value);
        command.Parameters.AddWithValue("order_item", (object?)orderItem ?? DBNull.Value);
        command.Parameters.AddWithValue("technical_owner", (object?)technicalOwner ?? DBNull.Value);
        AddDateParameter(command, "order_date", orderDate);
        AddDateParameter(command, "expected_receipt_date", expectedReceiptDate);
        command.Parameters.AddWithValue("issue_note", (object?)issueNote ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completed", completed);
        command.Parameters.AddWithValue("receipt_completed_at_utc", (object?)NormalizeUtc(completedAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completed_by_user_id", (object?)completedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completion_note", (object?)receiptCompletionNote ?? DBNull.Value);
        command.Parameters.AddWithValue("source_excel_row_number", (object?)(row.ExcelRowNumber == 0 ? null : row.ExcelRowNumber) ?? DBNull.Value);
        command.Parameters.AddWithValue("source_group_sequence", (object?)(row.SourceGroupSequence == 0 ? null : row.SourceGroupSequence) ?? DBNull.Value);
        command.Parameters.AddWithValue("row_match_key", (object?)row.RowMatchKey ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", changedByUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, bool completed, DateTimeOffset? completedAt, Guid? completedBy, string? note, Guid changedByUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_procurement_items
            set receipt_completed = @receipt_completed,
                receipt_completed_at_utc = @receipt_completed_at_utc,
                receipt_completed_by_user_id = @receipt_completed_by_user_id,
                receipt_completion_note = @receipt_completion_note,
                row_version = row_version + 1,
                updated_at_utc = now(),
                updated_by_user_id = @user_id
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("receipt_completed", completed);
        command.Parameters.AddWithValue("receipt_completed_at_utc", (object?)NormalizeUtc(completedAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completed_by_user_id", (object?)completedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completion_note", (object?)note ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", changedByUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> InsertImportBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        UploadedExcelFile file,
        IReadOnlyList<ProcurementExcelPreviewRowResponse> appliedRows,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into procurement_excel_import_batches (
                id, original_file_name, file_size_bytes, file_sha256,
                total_row_count, new_item_count, changed_item_count, unchanged_item_count,
                skipped_item_count, missing_from_upload_count, uploaded_by_user_id, reason)
            values (
                @id, @file_name, @file_size, @sha,
                @total, @new_count, @changed_count, @unchanged_count,
                @skipped_count, @missing_count, @user_id, @reason);
            """;
        command.Parameters.AddWithValue("id", batchId);
        command.Parameters.AddWithValue("file_name", file.OriginalFileName);
        command.Parameters.AddWithValue("file_size", file.FileSizeBytes);
        command.Parameters.AddWithValue("sha", file.FileSha256);
        command.Parameters.AddWithValue("total", appliedRows.Count);
        command.Parameters.AddWithValue("new_count", appliedRows.Count(row => row.ResultType == "New"));
        command.Parameters.AddWithValue("changed_count", appliedRows.Count(row => row.ResultType == "Changed"));
        command.Parameters.AddWithValue("unchanged_count", 0);
        command.Parameters.AddWithValue("skipped_count", 0);
        command.Parameters.AddWithValue("missing_count", 0);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("reason", (object?)ProcurementDomain.TrimToNull(reason) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return batchId;
    }

    private static async Task InsertImportBatchProjectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid batchId, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into procurement_excel_import_batch_projects (import_batch_id, project_id)
            values (@import_batch_id, @project_id)
            on conflict do nothing;
            """;
        command.Parameters.AddWithValue("import_batch_id", batchId);
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> HasDuplicateSuccessfulBatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] projectIds, string fileSha256, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from procurement_excel_import_batches b
                join procurement_excel_import_batch_projects bp on bp.import_batch_id = b.id
                where b.file_sha256 = @sha and bp.project_id = any(@project_ids)
            );
            """;
        command.Parameters.AddWithValue("sha", fileSha256);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("project_ids", projectIds));
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid itemId, string fieldName, string? oldValue, string? newValue, string? reason, Guid userId, string correlationId, string source, Guid? importBatchId, string? inputUnit, string? originalInputValue, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (
                project_id, entity_type, entity_id, action, field_name, old_value, new_value,
                reason, changed_by_user_id, correlation_id, input_source, procurement_import_batch_id,
                input_unit, original_input_value)
            values (
                @project_id, 'ProcurementItem', @entity_id, 'ProcurementItemUpdated', @field_name, @old_value, @new_value,
                @reason, @user_id, @correlation_id, @input_source, @import_batch_id,
                @input_unit, @original_input_value);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("entity_id", itemId);
        command.Parameters.AddWithValue("field_name", fieldName);
        command.Parameters.AddWithValue("old_value", (object?)oldValue ?? DBNull.Value);
        command.Parameters.AddWithValue("new_value", (object?)newValue ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)ProcurementDomain.TrimToNull(reason) ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("input_source", source);
        command.Parameters.AddWithValue("import_batch_id", (object?)importBatchId ?? DBNull.Value);
        command.Parameters.AddWithValue("input_unit", (object?)inputUnit ?? DBNull.Value);
        command.Parameters.AddWithValue("original_input_value", (object?)originalInputValue ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<Change> CollectDirectChanges(ProcurementItemSnapshot current, ProcurementItemUpdateRequest update)
    {
        var next = new ParsedProcurementExcelRow(0, current.SourceGroupSequence ?? 0, current.SourceProjectText, current.SourceProjectCodeText, update.StandardLeadTime, update.OrderItem, update.TechnicalOwner, update.OrderDate, update.ExpectedReceiptDate, null, update.IssueNote, update.ReceiptCompleted, false, []);
        return CollectChanges(current, next, NormalizeUtc(update.ReceiptCompletedAtUtc), update.ReceiptCompletionNote, keepExistingWhenNull: false);
    }

    private static List<Change> CollectExcelChanges(ProcurementItemSnapshot current, ParsedProcurementExcelRow row)
    {
        return CollectChanges(current, row, null, null, keepExistingWhenNull: true);
    }

    private static List<Change> CollectReceiptChanges(ProcurementItemSnapshot current, ProcurementReceiptUpdateRequest update)
    {
        var changes = new List<Change>();
        if (update.ReceiptCompleted is not null && current.ReceiptCompleted != update.ReceiptCompleted)
        {
            changes.Add(new Change("ReceiptCompleted", current.ReceiptCompleted.ToString(CultureInfo.InvariantCulture), update.ReceiptCompleted.Value.ToString(CultureInfo.InvariantCulture), null));
        }

        var receiptCompletedAtUtc = NormalizeUtc(update.ReceiptCompletedAtUtc);
        if (receiptCompletedAtUtc is not null && NormalizeUtc(current.ReceiptCompletedAtUtc) != receiptCompletedAtUtc)
        {
            changes.Add(new Change("ReceiptCompletedAtUtc", NormalizeUtc(current.ReceiptCompletedAtUtc)?.ToString("O", CultureInfo.InvariantCulture), receiptCompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture), null));
        }

        var note = update.ReceiptCompletionNote is null
            ? current.ReceiptCompletionNote
            : ProcurementDomain.TrimToNull(update.ReceiptCompletionNote);
        if (!string.Equals(current.ReceiptCompletionNote, note, StringComparison.Ordinal))
        {
            changes.Add(new Change("ReceiptCompletionNote", current.ReceiptCompletionNote, note, null));
        }

        return changes;
    }

    private static List<Change> CollectChanges(ProcurementItemSnapshot current, ParsedProcurementExcelRow row, DateTimeOffset? receiptAt, string? receiptNote, bool keepExistingWhenNull)
    {
        var changes = new List<Change>();
        AddText(changes, "StandardLeadTime", current.StandardLeadTime, row.StandardLeadTime, row.StandardLeadTime, keepExistingWhenNull);
        AddText(changes, "OrderItem", current.OrderItem, row.OrderItem, row.OrderItem, keepExistingWhenNull);
        AddText(changes, "TechnicalOwner", current.TechnicalOwner, row.TechnicalOwner, row.TechnicalOwner, keepExistingWhenNull);
        AddDate(changes, "OrderDate", current.OrderDate, row.OrderDate, keepExistingWhenNull);
        AddDate(changes, "ExpectedReceiptDate", current.ExpectedReceiptDate, row.ExpectedReceiptDate, keepExistingWhenNull);
        AddText(changes, "IssueNote", current.IssueNote, row.IssueNote, row.IssueNote, keepExistingWhenNull);
        if (row.ReceiptCompleted is not null && current.ReceiptCompleted != row.ReceiptCompleted)
        {
            changes.Add(new Change("ReceiptCompleted", current.ReceiptCompleted.ToString(CultureInfo.InvariantCulture), row.ReceiptCompleted.Value.ToString(CultureInfo.InvariantCulture), row.ReceiptCompleted.Value.ToString(CultureInfo.InvariantCulture)));
        }

        var receiptAtUtc = NormalizeUtc(receiptAt);
        if (receiptAtUtc is not null && NormalizeUtc(current.ReceiptCompletedAtUtc) != receiptAtUtc)
        {
            changes.Add(new Change("ReceiptCompletedAtUtc", NormalizeUtc(current.ReceiptCompletedAtUtc)?.ToString("O", CultureInfo.InvariantCulture), receiptAtUtc.Value.ToString("O", CultureInfo.InvariantCulture), null));
        }

        var note = ProcurementDomain.TrimToNull(receiptNote);
        if (receiptNote is not null && !string.Equals(current.ReceiptCompletionNote, note, StringComparison.Ordinal))
        {
            changes.Add(new Change("ReceiptCompletionNote", current.ReceiptCompletionNote, note, null));
        }

        return changes;
    }

    private static List<Change> CollectNewItemChanges(ProcurementItemSnapshot item)
    {
        var changes = new List<Change>();
        AddNew(changes, "StandardLeadTime", item.StandardLeadTime);
        AddNew(changes, "OrderItem", item.OrderItem);
        AddNew(changes, "TechnicalOwner", item.TechnicalOwner);
        AddNew(changes, "OrderDate", item.OrderDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddNew(changes, "ExpectedReceiptDate", item.ExpectedReceiptDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddNew(changes, "IssueNote", item.IssueNote);
        if (item.ReceiptCompleted)
        {
            AddNew(changes, "ReceiptCompleted", "True");
        }

        return changes;
    }

    private static void AddText(List<Change> changes, string field, string? oldValue, string? newValue, string? original, bool keepExistingWhenNull)
    {
        var normalized = ProcurementDomain.TrimToNull(newValue);
        if (keepExistingWhenNull && normalized is null)
        {
            return;
        }

        if (!string.Equals(oldValue, normalized, StringComparison.Ordinal))
        {
            changes.Add(new Change(field, oldValue, normalized, original));
        }
    }

    private static void AddDate(List<Change> changes, string field, DateOnly? oldValue, DateOnly? newValue, bool keepExistingWhenNull)
    {
        if (keepExistingWhenNull && newValue is null)
        {
            return;
        }

        if (oldValue != newValue)
        {
            changes.Add(new Change(field, oldValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), newValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), newValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AddNew(List<Change> changes, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            changes.Add(new Change(field, null, value, value));
        }
    }

    private static ProcurementItemSnapshot ReadItem(NpgsqlDataReader reader)
    {
        return new ProcurementItemSnapshot(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateOnly>(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<DateOnly>(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetBoolean(14),
            reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.GetInt32(19),
            reader.IsDBNull(20) ? null : reader.GetInt32(20),
            reader.IsDBNull(21) ? null : reader.GetInt32(21),
            reader.IsDBNull(22) ? null : reader.GetString(22),
            reader.GetString(23));
    }

    private static void AddItemParameters(NpgsqlCommand command, ProcurementItemSnapshot item, Guid userId)
    {
        command.Parameters.AddWithValue("id", item.ItemId);
        command.Parameters.AddWithValue("project_id", item.ProjectId);
        command.Parameters.AddWithValue("sequence_number", item.SequenceNumber);
        command.Parameters.AddWithValue("source_project_text", (object?)item.SourceProjectText ?? DBNull.Value);
        command.Parameters.AddWithValue("source_project_code_text", (object?)item.SourceProjectCodeText ?? DBNull.Value);
        command.Parameters.AddWithValue("standard_lead_time", (object?)item.StandardLeadTime ?? DBNull.Value);
        command.Parameters.AddWithValue("order_item", (object?)item.OrderItem ?? DBNull.Value);
        command.Parameters.AddWithValue("technical_owner", (object?)item.TechnicalOwner ?? DBNull.Value);
        AddDateParameter(command, "order_date", item.OrderDate);
        AddDateParameter(command, "expected_receipt_date", item.ExpectedReceiptDate);
        command.Parameters.AddWithValue("issue_note", (object?)item.IssueNote ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completed", item.ReceiptCompleted);
        command.Parameters.AddWithValue("receipt_completed_at_utc", (object?)NormalizeUtc(item.ReceiptCompletedAtUtc) ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completed_by_user_id", (object?)item.ReceiptCompletedByUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("receipt_completion_note", (object?)item.ReceiptCompletionNote ?? DBNull.Value);
        command.Parameters.AddWithValue("source_excel_row_number", (object?)item.SourceExcelRowNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("source_group_sequence", (object?)item.SourceGroupSequence ?? DBNull.Value);
        command.Parameters.AddWithValue("row_match_key", (object?)item.RowMatchKey ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", userId);
    }

    private static void AddDateParameter(NpgsqlCommand command, string name, DateOnly? value)
    {
        command.Parameters.Add(new NpgsqlParameter<DateOnly?>(name, NpgsqlDbType.Date) { TypedValue = value });
    }

    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value)
    {
        return value?.ToUniversalTime();
    }

    private static async Task<IReadOnlyList<ProcurementExcelImportBatchResponse>> ReadImportBatchesAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select b.id, b.original_file_name, b.file_size_bytes, b.file_sha256, b.total_row_count,
                   b.new_item_count, b.changed_item_count, b.unchanged_item_count, b.skipped_item_count,
                   b.missing_from_upload_count, b.uploaded_by_user_id, u.display_name, b.uploaded_at_utc, b.reason
            from procurement_excel_import_batches b
            join procurement_excel_import_batch_projects bp on bp.import_batch_id = b.id
            left join qms_users u on u.id = b.uploaded_by_user_id
            where bp.project_id = @project_id
            order by b.uploaded_at_utc desc;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var batches = new List<ProcurementExcelImportBatchResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            batches.Add(new ProcurementExcelImportBatchResponse
            {
                ImportBatchId = reader.GetGuid(0),
                OriginalFileName = reader.GetString(1),
                FileSizeBytes = reader.GetInt64(2),
                FileSha256 = reader.GetString(3),
                TotalRowCount = reader.GetInt32(4),
                NewItemCount = reader.GetInt32(5),
                ChangedItemCount = reader.GetInt32(6),
                UnchangedItemCount = reader.GetInt32(7),
                SkippedItemCount = reader.GetInt32(8),
                MissingFromUploadCount = reader.GetInt32(9),
                UploadedByUserId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
                UploadedByUserName = reader.IsDBNull(11) ? null : reader.GetString(11),
                UploadedAtUtc = reader.GetFieldValue<DateTimeOffset>(12),
                Reason = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
        }

        return batches;
    }

    private static async Task<IReadOnlyList<ProcurementRequiredItemSettingsResponse>> ReadRequiredItemSettingsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var productTypesCommand = connection.CreateCommand();
        productTypesCommand.Transaction = transaction;
        productTypesCommand.CommandText = """
            select product_types.code,
                   templates.id,
                   templates.version
            from production_product_types product_types
            left join procurement_required_item_templates templates
              on upper(btrim(templates.item_code)) = upper(btrim(product_types.code))
             and templates.is_active = true
            where product_types.is_active = true
              and product_types.code = any(@canonical_codes)
            order by array_position(array['UL67','UL891','UL508A','IEC','LLP','RPP'], product_types.code), product_types.code;
            """;
        productTypesCommand.Parameters.AddWithValue("canonical_codes", ProductionPlanningDomain.CanonicalProductTypeCodes.ToArray());

        var templates = new List<(string ItemCode, Guid? TemplateId, int? Version)>();
        await using (var reader = await productTypesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                templates.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2)));
            }
        }

        var result = new List<ProcurementRequiredItemSettingsResponse>();
        foreach (var template in templates)
        {
            result.Add(new ProcurementRequiredItemSettingsResponse(
                template.ItemCode,
                template.TemplateId,
                template.Version,
                template.TemplateId is null
                    ? []
                    : await ReadRequiredItemSettingRowsAsync(connection, transaction, template.TemplateId.Value, cancellationToken)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ProcurementRequiredItemSettingsRowResponse>> ReadRequiredItemSettingRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, sequence_number, item_name, is_required, is_active
            from procurement_required_item_template_rows
            where template_id = @template_id
            order by sequence_number, item_name;
            """;
        command.Parameters.AddWithValue("template_id", templateId);

        var rows = new List<ProcurementRequiredItemSettingsRowResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProcurementRequiredItemSettingsRowResponse(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4)));
        }

        return rows;
    }

    private static async Task<bool> ActiveProductTypeExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string itemCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from production_product_types
                where upper(btrim(code)) = @item_code
                  and is_active = true
                  and code = any(@canonical_codes)
            );
            """;
        command.Parameters.AddWithValue("item_code", itemCode);
        command.Parameters.AddWithValue("canonical_codes", ProductionPlanningDomain.CanonicalProductTypeCodes.ToArray());
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static IReadOnlyDictionary<string, string[]> ValidateRequiredItemSettings(UpdateProcurementRequiredItemSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Rows is null || request.Rows.Count == 0)
        {
            errors["rows"] = ["최소 1개 이상의 구매 항목이 필요합니다."];
            return errors;
        }

        var activeRows = request.Rows
            .Select((row, index) => (Row: row, Index: index))
            .Where(item => item.Row.IsActive != false)
            .ToList();
        if (activeRows.Count == 0)
        {
            errors["rows"] = ["최소 1개 이상의 사용 항목이 필요합니다."];
        }

        var activeNames = new Dictionary<string, int>(StringComparer.Ordinal);
        var activeSequences = new Dictionary<int, int>();
        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            if (row.SequenceNumber is null || row.SequenceNumber < 1)
            {
                errors[$"rows[{index}].sequenceNumber"] = ["순서는 1 이상의 정수여야 합니다."];
            }

            if (string.IsNullOrWhiteSpace(row.ItemName))
            {
                errors[$"rows[{index}].itemName"] = ["구매 항목명은 필수입니다."];
            }

            if (row.IsActive == false)
            {
                continue;
            }

            if (row.SequenceNumber is { } sequence)
            {
                if (activeSequences.TryGetValue(sequence, out var firstIndex))
                {
                    errors[$"rows[{index}].sequenceNumber"] = [$"{firstIndex + 1}행과 순서가 중복됩니다."];
                }
                else
                {
                    activeSequences[sequence] = index;
                }
            }

            if (!string.IsNullOrWhiteSpace(row.ItemName))
            {
                var normalized = NormalizeRequiredItemName(row.ItemName);
                if (activeNames.TryGetValue(normalized, out var firstIndex))
                {
                    errors[$"rows[{index}].itemName"] = [$"{firstIndex + 1}행과 구매 항목명이 중복됩니다."];
                }
                else
                {
                    activeNames[normalized] = index;
                }
            }
        }

        return errors;
    }

    private static string NormalizeItemCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized == "RRP" ? "RPP" : normalized;
    }

    public static string NormalizeRequiredItemName(string value)
    {
        return string.Concat(value.Where(ch => !char.IsWhiteSpace(ch))).ToUpperInvariant();
    }

    public static IReadOnlyDictionary<Guid, int> ParseExpectedVersions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, int>();
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<ProcurementExcelExpectedVersion>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?.ToDictionary(item => item.ItemId, item => item.ExpectedRowVersion)
                ?? new Dictionary<Guid, int>();
        }
        catch
        {
            return new Dictionary<Guid, int>();
        }
    }

    public static IReadOnlyList<ProcurementExcelProjectSelection> ParseProjectSelections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<ProcurementExcelProjectSelection>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task RollbackQuietlyAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
        }
    }

    private sealed record Change(string FieldName, string? OldValue, string? NewValue, string? OriginalInputValue);

    private sealed record HistoryEvent(
        Guid AuditId,
        Guid EntityId,
        string? FieldName,
        string? OldValue,
        string? NewValue,
        string? Reason,
        Guid? ChangedByUserId,
        string? ChangedByUserName,
        DateTimeOffset ChangedAtUtc,
        string CorrelationId,
        string InputSource,
        Guid? ImportBatchId,
        string? ImportFileName,
        int? SequenceNumber);
}
