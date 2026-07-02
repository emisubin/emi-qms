using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.PanelInformation;

public sealed class PanelInformationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    PanelInformationExcelParser excelParser)
{
    public async Task<PanelInformationResponse?> GetPanelInformationAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var project = await ReadProjectSnapshotAsync(dataSource, projectId, includeDeleted: false, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var panels = await ReadPanelSnapshotsAsync(dataSource, projectId, includeDeletedProject: false, cancellationToken);
        return BuildResponse(project, panels);
    }

    public async Task<PanelInformationMutationResult<PanelInformationResponse>> UpdatePanelInformationAsync(
        Guid projectId,
        NormalizedPanelInformationBulkUpdateInput input,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return await PersistPanelUpdatesAsync(
            projectId,
            input,
            changedByUserId,
            correlationId,
            "Direct",
            null,
            cancellationToken);
    }

    public async Task<PanelInformationHistoryResponse?> GetHistoryAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        if (await ReadProjectSnapshotAsync(dataSource, projectId, includeDeleted: false, cancellationToken) is null)
        {
            return null;
        }

        var auditEvents = await ReadPanelAuditEventsAsync(dataSource, projectId, cancellationToken);
        var batches = await ReadExcelImportBatchesAsync(dataSource, projectId, cancellationToken);
        return new PanelInformationHistoryResponse(BuildHistoryGroups(auditEvents), auditEvents, batches);
    }

    public async Task<PanelInformationMutationResult<PanelInformationTemplateDownload>> CreateTemplateAsync(
        Guid projectId,
        string? unit,
        CancellationToken cancellationToken)
    {
        var validation = new ProjectValidationResult();
        var normalizedUnit = PanelInformationRequestValidator.NormalizeInputUnit(
            string.IsNullOrWhiteSpace(unit) ? "Mm" : unit,
            validation,
            requireWhenMissing: false);
        if (validation.HasErrors || normalizedUnit is null)
        {
            return PanelInformationMutationResult<PanelInformationTemplateDownload>.Validation(validation.Errors);
        }

        await using var dataSource = CreateDataSource();
        var project = await ReadProjectSnapshotAsync(dataSource, projectId, includeDeleted: false, cancellationToken);
        if (project is null)
        {
            return PanelInformationMutationResult<PanelInformationTemplateDownload>.NotFound();
        }

        var panels = await ReadPanelSnapshotsAsync(dataSource, projectId, includeDeletedProject: false, cancellationToken);
        var activePanels = panels
            .Where(panel => panel.PanelStatus == "Active")
            .OrderBy(panel => panel.SequenceNumber)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Panel Information");
        var sizeCompletionRequired = project.PackagingMethod == "WoodenCrate";
        worksheet.Cell(1, 8).Value = sizeCompletionRequired
            ? "No는 업로드 식별값입니다. * 표시는 완료 필수값입니다. 목포장은 패널명과 W/H/D 입력 시 설계 단계가 완료됩니다. 저장은 일부 입력 상태에서도 가능합니다."
            : "No는 업로드 식별값입니다. * 표시는 완료 필수값입니다. 일반 포장은 패널명 입력 시 설계 단계가 완료됩니다. 저장은 일부 입력 상태에서도 가능합니다.";
        worksheet.Cell(1, 8).Style.Font.Italic = true;
        worksheet.Cell(1, 8).Style.Alignment.WrapText = true;
        worksheet.Column(8).Width = 72;
        var headers = new (string Text, bool Required)[]
        {
            ("No", true),
            ("도번", false),
            ("패널명", true),
            ("W", sizeCompletionRequired),
            ("H", sizeCompletionRequired),
            ("D", sizeCompletionRequired)
        };
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column].Required ? $"{headers[column].Text} *" : headers[column].Text;
            if (headers[column].Required)
            {
                worksheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        var rowNumber = 2;
        foreach (var panel in activePanels)
        {
            worksheet.Cell(rowNumber, 1).Value = panel.SequenceNumber;
            worksheet.Cell(rowNumber, 2).Value = "";
            worksheet.Cell(rowNumber, 3).Value = panel.PanelName ?? "";
            SetDimensionCell(worksheet.Cell(rowNumber, 4), panel.WidthMm, normalizedUnit);
            SetDimensionCell(worksheet.Cell(rowNumber, 5), panel.HeightMm, normalizedUnit);
            SetDimensionCell(worksheet.Cell(rowNumber, 6), panel.DepthMm, normalizedUnit);
            rowNumber++;
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, Math.Max(rowNumber - 1, 1), headers.Length).SetAutoFilter();
        worksheet.Columns(1, headers.Length).AdjustToContents();
        worksheet.Column(1).Width = Math.Clamp(worksheet.Column(1).Width + 2, 8, 12);
        worksheet.Column(2).Width = Math.Clamp(worksheet.Column(2).Width + 2, 18, 28);
        worksheet.Column(3).Width = Math.Clamp(worksheet.Column(3).Width + 2, 24, 36);
        for (var column = 4; column <= 6; column++)
        {
            worksheet.Column(column).Width = Math.Clamp(worksheet.Column(column).Width + 2, 12, 16);
        }
        worksheet.Columns(1, headers.Length).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var suffix = normalizedUnit == "Inch" ? "inch" : "mm";
        return PanelInformationMutationResult<PanelInformationTemplateDownload>.Success(
            new PanelInformationTemplateDownload(
                stream.ToArray(),
                BuildTemplateFileName(project.ProjectTitle, suffix),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    public async Task<PanelInformationMutationResult<PanelInformationExcelPreviewResponse>> PreviewExcelImportAsync(
        Guid projectId,
        UploadedExcelFile file,
        string? inputUnit,
        CancellationToken cancellationToken)
    {
        var parsed = await excelParser.ParseAsync(file, cancellationToken);
        if (parsed.FileErrors.Count > 0)
        {
            return PanelInformationMutationResult<PanelInformationExcelPreviewResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = parsed.FileErrors.ToArray() });
        }

        await using var dataSource = CreateDataSource();
        var project = await ReadProjectSnapshotAsync(dataSource, projectId, includeDeleted: false, cancellationToken);
        if (project is null)
        {
            return PanelInformationMutationResult<PanelInformationExcelPreviewResponse>.NotFound();
        }

        var panels = await ReadPanelSnapshotsAsync(dataSource, projectId, includeDeletedProject: false, cancellationToken);
        var preview = BuildExcelPreview(project, panels, parsed, inputUnit);
        return PanelInformationMutationResult<PanelInformationExcelPreviewResponse>.Success(preview);
    }

    public async Task<PanelInformationMutationResult<PanelInformationResponse>> ApplyExcelImportAsync(
        Guid projectId,
        UploadedExcelFile file,
        string? inputUnit,
        string expectedFileSha256,
        string? expectedPackagingMethod,
        string? reason,
        IReadOnlyDictionary<Guid, int> expectedVersions,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(file.FileSha256, expectedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Conflict("Excel 파일이 미리보기 때와 다릅니다. 새 Preview를 실행해 주세요.");
        }

        var parsed = await excelParser.ParseAsync(file, cancellationToken);
        if (parsed.FileErrors.Count > 0)
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                new Dictionary<string, string[]> { ["File"] = parsed.FileErrors.ToArray() });
        }

        var validation = new ProjectValidationResult();
        var normalizedReason = PanelInformationRequestValidator.OptionalReason(reason, validation);
        var normalizedUnit = PanelInformationRequestValidator.NormalizeInputUnit(inputUnit, validation, requireWhenMissing: false);
        if (validation.HasErrors)
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Validation(validation.Errors);
        }

        return await PersistExcelImportAsync(
            projectId,
            parsed,
            file,
            normalizedUnit,
            expectedPackagingMethod,
            normalizedReason,
            expectedVersions,
            changedByUserId,
            correlationId,
            cancellationToken);
    }

    private async Task<PanelInformationMutationResult<PanelInformationResponse>> PersistPanelUpdatesAsync(
        Guid projectId,
        NormalizedPanelInformationBulkUpdateInput input,
        Guid changedByUserId,
        string correlationId,
        string inputSource,
        ExcelBatchMetadata? excelBatch,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
            if (project is null || project.DeletedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.NotFound();
            }

            if (project.Status != "Active")
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Conflict("현재 프로젝트 상태에서는 설계 정보를 수정할 수 없습니다.");
            }

            var persistResult = await PersistLockedUpdatesAsync(
                connection,
                transaction,
                projectId,
                project,
                input,
                lockedPanels: null,
                changedByUserId,
                correlationId,
                inputSource,
                excelBatch,
                cancellationToken);
            if (persistResult.Status != PanelInformationMutationStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                return persistResult;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }

        var response = await GetPanelInformationAsync(projectId, cancellationToken);
        return response is null
            ? PanelInformationMutationResult<PanelInformationResponse>.NotFound()
            : PanelInformationMutationResult<PanelInformationResponse>.Success(response);
    }

    private async Task<PanelInformationMutationResult<PanelInformationResponse>> PersistExcelImportAsync(
        Guid projectId,
        ParsedPanelInformationExcelFile parsed,
        UploadedExcelFile file,
        string? inputUnit,
        string? expectedPackagingMethod,
        string? reason,
        IReadOnlyDictionary<Guid, int> expectedVersions,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
            if (project is null || project.DeletedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.NotFound();
            }

            if (project.Status != "Active")
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Conflict("현재 프로젝트 상태에서는 설계 정보를 수정할 수 없습니다.");
            }

            var normalizedExpectedPackaging = ProjectInputNormalizer.TrimToNull(expectedPackagingMethod);
            if (!string.Equals(project.PackagingMethod, normalizedExpectedPackaging, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Conflict("프로젝트 포장방식이 변경되었습니다. Excel 미리보기를 다시 실행해 주세요.");
            }

            if (project.PackagingMethod is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Conflict("포장방식을 먼저 지정한 후 Excel을 적용해 주세요.");
            }

            var sequenceNumbers = parsed.Rows
                .Where(row => row.No is not null)
                .Select(row => row.No!.Value)
                .Distinct()
                .ToArray();
            var panels = await LockPanelsBySequenceAsync(connection, transaction, projectId, sequenceNumbers, cancellationToken);
            var preview = BuildExcelPreview(project, panels, parsed, inputUnit);
            if (preview.ErrorCount > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                    new Dictionary<string, string[]> { ["File"] = ["Excel 오류가 있는 경우 전체 적용할 수 없습니다."] });
            }

            if (preview.ReasonRequired && string.IsNullOrWhiteSpace(reason))
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                    new Dictionary<string, string[]> { ["Reason"] = ["기존 설계 정보를 변경하려면 수정사유가 필요합니다."] });
            }

            var updateItems = new List<NormalizedPanelInformationUpdateItem>();
            foreach (var row in preview.Rows.Where(row => row.ResultType is "New" or "Changed"))
            {
                if (row.PanelId is null || row.ExpectedPanelInfoVersion is null)
                {
                    continue;
                }

                if (!expectedVersions.TryGetValue(row.PanelId.Value, out var expectedVersion)
                    || expectedVersion != row.ExpectedPanelInfoVersion.Value)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return PanelInformationMutationResult<PanelInformationResponse>.Conflict(PanelInformationDomain.StaleVersionMessage);
                }

                updateItems.Add(new NormalizedPanelInformationUpdateItem(
                    row.PanelId.Value,
                    expectedVersion,
                    PanelNameChanged: row.PanelName is not null,
                    PanelInformationDomain.NormalizePanelName(row.PanelName),
                    SizeChanged: row.WidthMm is not null && row.HeightMm is not null && row.DepthMm is not null,
                    row.WidthMm,
                    row.HeightMm,
                    row.DepthMm,
                    inputUnit,
                    row.Width,
                    row.Height,
                    row.Depth));
            }

            var input = new NormalizedPanelInformationBulkUpdateInput(reason, updateItems);
            var batch = new ExcelBatchMetadata(
                file.OriginalFileName,
                file.FileSizeBytes,
                file.FileSha256,
                inputUnit,
                preview.TotalRows,
                preview.NewCount,
                preview.ChangedCount,
                preview.UnchangedCount,
                preview.SkippedCount,
                reason);

            var result = await PersistLockedUpdatesAsync(
                connection,
                transaction,
                projectId,
                project,
                input,
                panels,
                changedByUserId,
                correlationId,
                "Excel",
                batch,
                cancellationToken);
            if (result.Status != PanelInformationMutationStatus.Success)
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

        var response = await GetPanelInformationAsync(projectId, cancellationToken);
        return response is null
            ? PanelInformationMutationResult<PanelInformationResponse>.NotFound()
            : PanelInformationMutationResult<PanelInformationResponse>.Success(response);
    }

    private async Task<PanelInformationMutationResult<PanelInformationResponse>> PersistLockedUpdatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        PanelInformationProjectSnapshot project,
        NormalizedPanelInformationBulkUpdateInput input,
        IReadOnlyList<PanelInformationPanelSnapshot>? lockedPanels,
        Guid changedByUserId,
        string correlationId,
        string inputSource,
        ExcelBatchMetadata? excelBatch,
        CancellationToken cancellationToken)
    {
        if (excelBatch is not null && project.PackagingMethod is null)
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Conflict("포장방식을 먼저 지정한 후 Excel을 적용해 주세요.");
        }

        var panels = lockedPanels ?? await LockPanelsAsync(
            connection,
            transaction,
            projectId,
            input.Panels.Select(panel => panel.PanelId).ToArray(),
            cancellationToken);
        if (panels.Count < input.Panels.Select(panel => panel.PanelId).Distinct().Count())
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                new Dictionary<string, string[]> { ["Panels"] = ["대상 패널은 해당 프로젝트에 속해야 합니다."] });
        }

        var panelById = panels.ToDictionary(panel => panel.PanelId);
        foreach (var item in input.Panels)
        {
            if (!panelById.TryGetValue(item.PanelId, out var panel))
            {
                return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                    new Dictionary<string, string[]> { ["Panels"] = ["대상 패널은 해당 프로젝트에 속해야 합니다."] });
            }

            if (panel.PanelStatus != "Active")
            {
                return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                    new Dictionary<string, string[]> { ["Panels"] = ["취소된 패널은 수정할 수 없습니다."] });
            }

            if (panel.PanelInfoVersion != item.ExpectedPanelInfoVersion)
            {
                return PanelInformationMutationResult<PanelInformationResponse>.Conflict(PanelInformationDomain.StaleVersionMessage);
            }
        }

        var reasonRequired = input.Panels.Any(item => RequiresReason(panelById[item.PanelId], item));
        if (reasonRequired && string.IsNullOrWhiteSpace(input.Reason))
        {
            return PanelInformationMutationResult<PanelInformationResponse>.Validation(
                new Dictionary<string, string[]> { ["Reason"] = ["기존 설계 정보를 변경하려면 수정사유가 필요합니다."] });
        }

        Guid? importBatchId = null;
        if (excelBatch is not null)
        {
            importBatchId = await InsertExcelImportBatchAsync(
                connection,
                transaction,
                projectId,
                excelBatch,
                changedByUserId,
                cancellationToken);
        }

        var anyChanges = false;
        foreach (var item in input.Panels)
        {
            var panel = panelById[item.PanelId];
            var changes = CollectChanges(panel, item);
            if (changes.Count == 0)
            {
                continue;
            }

            anyChanges = true;
            var values = EffectiveValues(panel, item);
            var completed = PanelInformationDomain.IsPanelInfoCompleted(
                project.PackagingMethod,
                values.PanelName,
                values.WidthMm,
                values.HeightMm,
                values.DepthMm);
            var qrEligible = PanelInformationDomain.IsQrEligible(false, project.Status, panel.PanelStatus, values.PanelName);

            await UpdatePanelAsync(
                connection,
                transaction,
                item.PanelId,
                values,
                completed,
                qrEligible,
                changedByUserId,
                cancellationToken);

            foreach (var change in changes)
            {
                await InsertPanelAuditEventAsync(
                    connection,
                    transaction,
                    projectId,
                    panel.PanelId,
                    change.FieldName,
                    change.OldValue,
                    change.NewValue,
                    input.Reason,
                    changedByUserId,
                    correlationId,
                    inputSource,
                    importBatchId,
                    change.InputUnit,
                    change.OriginalInputValue,
                    cancellationToken);
            }
        }

        if (anyChanges)
        {
            await using var updateProjectCommand = connection.CreateCommand();
            updateProjectCommand.Transaction = transaction;
            updateProjectCommand.CommandText = "update projects set updated_at_utc = now() where id = @project_id;";
            updateProjectCommand.Parameters.AddWithValue("project_id", projectId);
            await updateProjectCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return PanelInformationMutationResult<PanelInformationResponse>.Success(new PanelInformationResponse());
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

    private static PanelInformationResponse BuildResponse(
        PanelInformationProjectSnapshot project,
        IReadOnlyList<PanelInformationPanelSnapshot> snapshots)
    {
        var activeSnapshots = snapshots.Where(panel => panel.PanelStatus == "Active").ToList();
        var duplicateCounts = activeSnapshots
            .Select(panel => PanelInformationDomain.NormalizeDuplicateName(panel.PanelName))
            .Where(normalized => normalized is not null)
            .GroupBy(normalized => normalized!)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var duplicateGroupCount = duplicateCounts.Count(item => item.Value > 1);
        var panels = snapshots
            .OrderBy(panel => panel.SequenceNumber)
            .Select(panel =>
            {
                var normalized = PanelInformationDomain.NormalizeDuplicateName(panel.PanelName);
                var duplicateCount = normalized is not null && duplicateCounts.TryGetValue(normalized, out var count) ? count : 0;
                var completed = PanelInformationDomain.IsPanelInfoCompleted(
                    project.PackagingMethod,
                    panel.PanelName,
                    panel.WidthMm,
                    panel.HeightMm,
                    panel.DepthMm);
                var qrEligible = PanelInformationDomain.IsQrEligible(
                    project.DeletedAtUtc is not null,
                    project.Status,
                    panel.PanelStatus,
                    panel.PanelName);

                return new PanelInformationPanelResponse
                {
                    PanelId = panel.PanelId,
                    ProjectId = panel.ProjectId,
                    SequenceNumber = panel.SequenceNumber,
                    PanelNumber = PanelInformationDomain.PanelNumber(panel.SequenceNumber),
                    DisplayCode = panel.DisplayCode,
                    PanelName = panel.PanelName,
                    DisplayName = PanelInformationDomain.DisplayName(panel.SequenceNumber, panel.PanelName),
                    WidthMm = panel.WidthMm,
                    HeightMm = panel.HeightMm,
                    DepthMm = panel.DepthMm,
                    PanelStatus = panel.PanelStatus,
                    WorkflowStage = panel.WorkflowStage,
                    PanelInfoCompleted = completed,
                    QrEligible = qrEligible,
                    HasDuplicateName = duplicateCount > 1,
                    DuplicateNameCount = duplicateCount,
                    PanelInfoVersion = panel.PanelInfoVersion,
                    CreatedAt = panel.CreatedAt,
                    UpdatedAt = panel.UpdatedAt,
                    PanelInfoUpdatedAtUtc = panel.PanelInfoUpdatedAtUtc,
                    PanelInfoUpdatedByUserId = panel.PanelInfoUpdatedByUserId,
                    PanelInfoUpdatedByUserName = panel.PanelInfoUpdatedByUserName
                };
            })
            .ToList();

        var activePanels = panels.Where(panel => panel.PanelStatus == "Active").ToList();
        var completedCount = activePanels.Count(panel => panel.PanelInfoCompleted);
        var qrEligibleCount = activePanels.Count(panel => panel.QrEligible);
        var manufacturingCompletedCount = activePanels.Count(panel =>
            PanelInformationDomain.IsManufacturingCompletedStage(panel.WorkflowStage));
        var inspectionCompletedCount = activePanels.Count(panel =>
            PanelInformationDomain.IsInspectionCompletedStage(panel.WorkflowStage));

        return new PanelInformationResponse
        {
            ProjectId = project.ProjectId,
            ProjectStatus = project.Status,
            PackagingMethod = project.PackagingMethod,
            ActivePanelCount = activePanels.Count,
            PanelInfoCompletedCount = completedCount,
            PanelInfoPendingCount = activePanels.Count - completedCount,
            QrEligibleCount = qrEligibleCount,
            ManufacturingCompletedCount = manufacturingCompletedCount,
            InspectionCompletedCount = inspectionCompletedCount,
            DuplicatePanelNameGroupCount = duplicateGroupCount,
            ProjectPanelInformationCompleted = activePanels.Count > 0 && activePanels.All(panel => panel.PanelInfoCompleted),
            PanelInformationStatusMessage = project.PackagingMethod is null ? "포장방식 미지정" : null,
            Panels = panels
        };
    }

    private PanelInformationExcelPreviewResponse BuildExcelPreview(
        PanelInformationProjectSnapshot project,
        IReadOnlyList<PanelInformationPanelSnapshot> panels,
        ParsedPanelInformationExcelFile parsed,
        string? inputUnit)
    {
        var panelBySequence = panels.ToDictionary(panel => panel.SequenceNumber);
        var responsePanels = BuildResponse(project, panels).Panels.ToDictionary(panel => panel.PanelId);
        var rows = new List<PanelInformationExcelPreviewRowResponse>();
        var reasonRequired = false;

        foreach (var parsedRow in parsed.Rows)
        {
            var errors = parsedRow.ErrorMessages.ToList();
            PanelInformationPanelSnapshot? current = null;
            if (parsedRow.No is not null)
            {
                if (!panelBySequence.TryGetValue(parsedRow.No.Value, out current))
                {
                    errors.Add("현재 프로젝트에 존재하지 않는 No입니다.");
                }
                else if (current.PanelStatus == "Cancelled")
                {
                    errors.Add("취소된 패널 No는 Excel로 수정할 수 없습니다.");
                }
            }

            if (project.PackagingMethod is null)
            {
                errors.Add("포장방식을 먼저 지정한 후 Excel을 적용해 주세요.");
            }

            var panelName = PanelInformationDomain.NormalizePanelName(parsedRow.PanelName);
            var panelNameChanged = panelName is not null;
            var suppliedSizeCount = new[] { parsedRow.Width, parsedRow.Height, parsedRow.Depth }.Count(value => value is not null);
            var sizeChanged = suppliedSizeCount > 0;
            var hasEditableInput = panelNameChanged || sizeChanged || parsedRow.ErrorMessages.Count > 0;

            if (panelName is not null && panelName.Length > PanelInformationDomain.PanelNameMaxLength)
            {
                errors.Add($"panel name은 최대 {PanelInformationDomain.PanelNameMaxLength}자까지 입력할 수 있습니다.");
            }

            var unitValidation = new ProjectValidationResult();
            var normalizedUnit = PanelInformationRequestValidator.NormalizeInputUnit(inputUnit, unitValidation, requireWhenMissing: false);
            foreach (var error in unitValidation.Errors.Values.SelectMany(value => value))
            {
                errors.Add(error);
            }

            NormalizedPanelSize? size = new(null, null, null);
            if (sizeChanged)
            {
                var sizeValidation = new ProjectValidationResult();
                size = PanelInformationRequestValidator.NormalizeSize(
                    parsedRow.Width,
                    parsedRow.Height,
                    parsedRow.Depth,
                    normalizedUnit,
                    $"Rows[{parsedRow.ExcelRowNumber}]",
                    sizeValidation);
                foreach (var error in sizeValidation.Errors.Values.SelectMany(value => value))
                {
                    errors.Add(error);
                }
            }

            var resultType = errors.Count > 0 ? "Error" : "Skipped";
            var currentResponse = current is not null && responsePanels.TryGetValue(current.PanelId, out var responsePanel)
                ? responsePanel
                : null;
            var expectedVersion = current?.PanelInfoVersion;
            if (errors.Count == 0 && current is not null)
            {
                var item = new NormalizedPanelInformationUpdateItem(
                    current.PanelId,
                    current.PanelInfoVersion,
                    PanelNameChanged: panelNameChanged,
                    panelName,
                    SizeChanged: sizeChanged,
                    size?.WidthMm,
                    size?.HeightMm,
                    size?.DepthMm,
                    normalizedUnit,
                    parsedRow.Width,
                    parsedRow.Height,
                    parsedRow.Depth);
                var changes = CollectChanges(current, item);
                if (changes.Count > 0 && RequiresReason(current, item))
                {
                    reasonRequired = true;
                }

                resultType = hasEditableInput
                    ? changes.Count == 0
                        ? "Unchanged"
                        : IsInitialPanelInput(current) ? "New" : "Changed"
                    : "Skipped";
            }

            rows.Add(new PanelInformationExcelPreviewRowResponse
            {
                ExcelRowNumber = parsedRow.ExcelRowNumber,
                No = parsedRow.No,
                PanelId = current?.PanelId,
                PanelName = panelName,
                Width = parsedRow.Width,
                Height = parsedRow.Height,
                Depth = parsedRow.Depth,
                WidthMm = size?.WidthMm,
                HeightMm = size?.HeightMm,
                DepthMm = size?.DepthMm,
                CurrentValue = currentResponse,
                ResultType = resultType,
                ErrorMessages = errors,
                ExpectedPanelInfoVersion = expectedVersion
            });
        }

        return new PanelInformationExcelPreviewResponse
        {
            FileSha256 = parsed.FileSha256,
            ExpectedPackagingMethod = project.PackagingMethod,
            ExpectedProjectStatus = project.Status,
            TotalRows = parsed.TotalRows,
            NewCount = rows.Count(row => row.ResultType == "New"),
            ChangedCount = rows.Count(row => row.ResultType == "Changed"),
            UnchangedCount = rows.Count(row => row.ResultType == "Unchanged"),
            SkippedCount = rows.Count(row => row.ResultType == "Skipped"),
            ErrorCount = rows.Count(row => row.ResultType == "Error"),
            ReasonRequired = reasonRequired,
            ExpectedPanelInfoVersions = rows
                .Where(row => row.ResultType is "New" or "Changed")
                .Where(row => row.PanelId is not null && row.ExpectedPanelInfoVersion is not null)
                .Select(row => new PanelInformationExcelExpectedVersion(row.PanelId!.Value, row.ExpectedPanelInfoVersion!.Value))
                .ToList(),
            Rows = rows
        };
    }

    private static bool IsInitialPanelInput(PanelInformationPanelSnapshot panel)
    {
        return panel.PanelName is null
            && panel.WidthMm is null
            && panel.HeightMm is null
            && panel.DepthMm is null;
    }

    private static bool RequiresReason(
        PanelInformationPanelSnapshot panel,
        NormalizedPanelInformationUpdateItem item)
    {
        return CollectChanges(panel, item).Any(change => !string.IsNullOrEmpty(change.OldValue));
    }

    private static IReadOnlyList<PanelInfoFieldChange> CollectChanges(
        PanelInformationPanelSnapshot panel,
        NormalizedPanelInformationUpdateItem item)
    {
        var changes = new List<PanelInfoFieldChange>();
        if (item.PanelNameChanged)
        {
            Add(changes, "PanelName", panel.PanelName, item.PanelName, item.PanelName, null);
        }

        if (item.SizeChanged)
        {
            Add(changes, "WidthMm", panel.WidthMm, item.WidthMm, item.OriginalWidth, item.SizeInputUnit);
            Add(changes, "HeightMm", panel.HeightMm, item.HeightMm, item.OriginalHeight, item.SizeInputUnit);
            Add(changes, "DepthMm", panel.DepthMm, item.DepthMm, item.OriginalDepth, item.SizeInputUnit);
        }

        return changes;
    }

    private static EffectivePanelValues EffectiveValues(
        PanelInformationPanelSnapshot panel,
        NormalizedPanelInformationUpdateItem item)
    {
        return new EffectivePanelValues(
            item.PanelNameChanged ? item.PanelName : panel.PanelName,
            item.SizeChanged ? item.WidthMm : panel.WidthMm,
            item.SizeChanged ? item.HeightMm : panel.HeightMm,
            item.SizeChanged ? item.DepthMm : panel.DepthMm);
    }

    private static void Add<T>(
        List<PanelInfoFieldChange> changes,
        string fieldName,
        T oldValue,
        T newValue,
        object? originalInputValue,
        string? inputUnit)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        changes.Add(new PanelInfoFieldChange(
            fieldName,
            FormatPanelAuditValue(oldValue),
            FormatPanelAuditValue(newValue),
            originalInputValue is null ? null : FormatPanelAuditValue(originalInputValue),
            inputUnit));
    }

    private static string FormatPanelAuditValue(object? value)
    {
        return value switch
        {
            null => "",
            decimal number => PanelInformationDomain.FormatDecimal(number),
            _ => ProjectInputNormalizer.FormatAuditValue(value)
        };
    }

    private static void SetDimensionCell(IXLCell cell, decimal? valueMm, string unit)
    {
        if (valueMm is null)
        {
            cell.Value = "";
            return;
        }

        var value = unit == "Inch"
            ? decimal.Round(valueMm.Value / PanelInformationDomain.InchToMm, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(valueMm.Value, 3, MidpointRounding.AwayFromZero);
        cell.SetValue(value);
        cell.Style.NumberFormat.Format = unit == "Inch" ? "0.00" : "0.###";
    }

    private static string BuildTemplateFileName(string projectTitle, string unitSuffix)
    {
        var invalidChars = Path.GetInvalidFileNameChars().Concat(['/', '\\', '\r', '\n']).ToHashSet();
        var builder = new StringBuilder(projectTitle.Length);
        foreach (var character in projectTitle)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        var safeTitle = builder.ToString().Trim();
        safeTitle = string.IsNullOrWhiteSpace(safeTitle) ? "Project" : safeTitle;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{safeTitle}_Panel_Information_{unitSuffix}.xlsx");
    }

    private static async Task<PanelInformationProjectSnapshot?> ReadProjectSnapshotAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            select id, project_title, status, packaging_method, deleted_at_utc
            from projects
            where id = @project_id
              {(includeDeleted ? "" : "and deleted_at_utc is null")};
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PanelInformationProjectSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4));
    }

    private static async Task<PanelInformationProjectSnapshot?> LockProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, project_title, status, packaging_method, deleted_at_utc
            from projects
            where id = @project_id
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PanelInformationProjectSnapshot(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4));
    }

    private static async Task<IReadOnlyList<PanelInformationPanelSnapshot>> ReadPanelSnapshotsAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        bool includeDeletedProject,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            select panel_placeholders.id,
                   panel_placeholders.project_id,
                   panel_placeholders.sequence_number,
                   panel_placeholders.display_code,
                   panel_placeholders.panel_name,
                   panel_placeholders.width_mm,
                   panel_placeholders.height_mm,
                   panel_placeholders.depth_mm,
                   panel_placeholders.status,
                   panel_placeholders.workflow_stage,
                   panel_placeholders.created_at_utc,
                   panel_placeholders.updated_at_utc,
                   panel_placeholders.panel_info_version,
                   panel_placeholders.panel_info_updated_at_utc,
                   panel_placeholders.panel_info_updated_by_user_id,
                   qms_users.display_name
            from panel_placeholders
            join projects on projects.id = panel_placeholders.project_id
            left join qms_users on qms_users.id = panel_placeholders.panel_info_updated_by_user_id
            where panel_placeholders.project_id = @project_id
              {(includeDeletedProject ? "" : "and projects.deleted_at_utc is null")}
            order by panel_placeholders.sequence_number;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        return await ReadPanelSnapshotsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<PanelInformationPanelSnapshot>> LockPanelsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid[] panelIds,
        CancellationToken cancellationToken)
    {
        if (panelIds.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   project_id,
                   sequence_number,
                   display_code,
                   panel_name,
                   width_mm,
                   height_mm,
                   depth_mm,
                   status,
                   workflow_stage,
                   created_at_utc,
                   updated_at_utc,
                   panel_info_version,
                   panel_info_updated_at_utc,
                   panel_info_updated_by_user_id,
                   null::text as display_name
            from panel_placeholders
            where project_id = @project_id
              and id = any(@panel_ids)
            order by sequence_number
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("panel_ids", panelIds));

        return await ReadPanelSnapshotsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<PanelInformationPanelSnapshot>> LockPanelsBySequenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        int[] sequenceNumbers,
        CancellationToken cancellationToken)
    {
        if (sequenceNumbers.Length == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   project_id,
                   sequence_number,
                   display_code,
                   panel_name,
                   width_mm,
                   height_mm,
                   depth_mm,
                   status,
                   workflow_stage,
                   created_at_utc,
                   updated_at_utc,
                   panel_info_version,
                   panel_info_updated_at_utc,
                   panel_info_updated_by_user_id,
                   null::text as display_name
            from panel_placeholders
            where project_id = @project_id
              and sequence_number = any(@sequence_numbers)
            order by sequence_number
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<int[]>("sequence_numbers", sequenceNumbers));

        return await ReadPanelSnapshotsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<PanelInformationPanelSnapshot>> ReadPanelSnapshotsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var panels = new List<PanelInformationPanelSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            panels.Add(new PanelInformationPanelSnapshot(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetFieldValue<DateTimeOffset>(10),
                reader.GetFieldValue<DateTimeOffset>(11),
                reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return panels;
    }

    private static async Task UpdatePanelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid panelId,
        EffectivePanelValues values,
        bool completed,
        bool qrEligible,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update panel_placeholders
            set panel_name = @panel_name,
                width_mm = @width_mm,
                height_mm = @height_mm,
                depth_mm = @depth_mm,
                panel_info_completed = @panel_info_completed,
                qr_eligible = @qr_eligible,
                panel_info_version = panel_info_version + 1,
                panel_info_updated_at_utc = now(),
                panel_info_updated_by_user_id = @changed_by_user_id,
                updated_at_utc = now()
            where id = @panel_id;
            """;
        command.Parameters.Add("panel_name", NpgsqlDbType.Text).Value = values.PanelName ?? (object)DBNull.Value;
        command.Parameters.Add("width_mm", NpgsqlDbType.Numeric).Value = values.WidthMm ?? (object)DBNull.Value;
        command.Parameters.Add("height_mm", NpgsqlDbType.Numeric).Value = values.HeightMm ?? (object)DBNull.Value;
        command.Parameters.Add("depth_mm", NpgsqlDbType.Numeric).Value = values.DepthMm ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("panel_info_completed", completed);
        command.Parameters.AddWithValue("qr_eligible", qrEligible);
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
        command.Parameters.AddWithValue("panel_id", panelId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> InsertExcelImportBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        ExcelBatchMetadata batch,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_information_excel_import_batches (
                project_id,
                original_file_name,
                file_size_bytes,
                file_sha256,
                input_unit,
            total_row_count,
            new_panel_count,
            changed_panel_count,
            unchanged_panel_count,
            skipped_panel_count,
            uploaded_by_user_id,
            reason
            )
            values (
                @project_id,
                @original_file_name,
                @file_size_bytes,
                @file_sha256,
                @input_unit,
                @total_row_count,
                @new_panel_count,
                @changed_panel_count,
                @unchanged_panel_count,
                @skipped_panel_count,
                @uploaded_by_user_id,
                @reason
            )
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("original_file_name", batch.OriginalFileName);
        command.Parameters.AddWithValue("file_size_bytes", batch.FileSizeBytes);
        command.Parameters.AddWithValue("file_sha256", batch.FileSha256);
        command.Parameters.Add("input_unit", NpgsqlDbType.Text).Value = batch.InputUnit ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("total_row_count", batch.TotalRowCount);
        command.Parameters.AddWithValue("new_panel_count", batch.NewPanelCount);
        command.Parameters.AddWithValue("changed_panel_count", batch.ChangedPanelCount);
        command.Parameters.AddWithValue("unchanged_panel_count", batch.UnchangedPanelCount);
        command.Parameters.AddWithValue("skipped_panel_count", batch.SkippedPanelCount);
        command.Parameters.AddWithValue("uploaded_by_user_id", changedByUserId);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = batch.Reason ?? (object)DBNull.Value;
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Import batch id was not returned."));
    }

    private static async Task InsertPanelAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        string fieldName,
        string? oldValue,
        string? newValue,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        string inputSource,
        Guid? importBatchId,
        string? inputUnit,
        string? originalInputValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (
                project_id,
                entity_type,
                entity_id,
                action,
                field_name,
                old_value,
                new_value,
                reason,
                changed_by_user_id,
                correlation_id,
                is_sensitive,
                input_source,
                import_batch_id,
                input_unit,
                original_input_value
            )
            values (
                @project_id,
                'Panel',
                @panel_id,
                'PanelInfoUpdated',
                @field_name,
                @old_value,
                @new_value,
                @reason,
                @changed_by_user_id,
                @correlation_id,
                false,
                @input_source,
                @import_batch_id,
                @input_unit,
                @original_input_value
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("field_name", fieldName);
        command.Parameters.Add("old_value", NpgsqlDbType.Text).Value = oldValue ?? (object)DBNull.Value;
        command.Parameters.Add("new_value", NpgsqlDbType.Text).Value = newValue ?? (object)DBNull.Value;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("input_source", inputSource);
        command.Parameters.Add("import_batch_id", NpgsqlDbType.Uuid).Value = importBatchId ?? (object)DBNull.Value;
        command.Parameters.Add("input_unit", NpgsqlDbType.Text).Value = inputUnit ?? (object)DBNull.Value;
        command.Parameters.Add("original_input_value", NpgsqlDbType.Text).Value = originalInputValue ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<PanelAuditEventResponse>> ReadPanelAuditEventsAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select project_audit_events.id,
                   project_audit_events.entity_type,
                   project_audit_events.entity_id,
                   project_audit_events.project_id,
                   project_audit_events.action,
                   project_audit_events.field_name,
                   project_audit_events.old_value,
                   project_audit_events.new_value,
                   project_audit_events.reason,
                   project_audit_events.changed_by_user_id,
                   qms_users.display_name,
                   project_audit_events.changed_at_utc,
                   project_audit_events.correlation_id,
                   project_audit_events.input_source,
                   project_audit_events.import_batch_id,
                   project_audit_events.input_unit,
                   project_audit_events.original_input_value,
                   panel_information_excel_import_batches.original_file_name,
                   panel_information_excel_import_batches.uploaded_at_utc,
                   panel_placeholders.sequence_number,
                   panel_placeholders.display_code,
                   panel_placeholders.panel_name
            from project_audit_events
            left join qms_users on qms_users.id = project_audit_events.changed_by_user_id
            left join panel_information_excel_import_batches
                on panel_information_excel_import_batches.id = project_audit_events.import_batch_id
               and panel_information_excel_import_batches.project_id = project_audit_events.project_id
            left join panel_placeholders on panel_placeholders.id = project_audit_events.entity_id
               and panel_placeholders.project_id = project_audit_events.project_id
            where project_audit_events.project_id = @project_id
              and project_audit_events.entity_type = 'Panel'
            order by project_audit_events.changed_at_utc desc, project_audit_events.id desc;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var events = new List<PanelAuditEventResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int? sequenceNumber = reader.IsDBNull(19) ? null : reader.GetInt32(19);
            var displayCode = reader.IsDBNull(20) ? null : reader.GetString(20);
            var panelName = reader.IsDBNull(21) ? null : reader.GetString(21);
            events.Add(new PanelAuditEventResponse
            {
                AuditEventId = reader.GetGuid(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                ProjectId = reader.GetGuid(3),
                Action = reader.GetString(4),
                FieldName = reader.IsDBNull(5) ? null : reader.GetString(5),
                OldValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                NewValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                Reason = reader.IsDBNull(8) ? null : reader.GetString(8),
                ChangedByUserId = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                ChangedByUserName = reader.IsDBNull(10) ? null : reader.GetString(10),
                ChangedAtUtc = reader.GetFieldValue<DateTimeOffset>(11),
                CorrelationId = reader.GetString(12),
                InputSource = reader.IsDBNull(13) ? null : reader.GetString(13),
                ImportBatchId = reader.IsDBNull(14) ? null : reader.GetGuid(14),
                InputUnit = reader.IsDBNull(15) ? null : reader.GetString(15),
                OriginalInputValue = reader.IsDBNull(16) ? null : reader.GetString(16),
                ImportFileName = reader.IsDBNull(17) ? null : reader.GetString(17),
                ImportUploadedAtUtc = reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18),
                PanelNumber = sequenceNumber is null ? null : PanelInformationDomain.PanelNumber(sequenceNumber.Value),
                PanelDisplayName = sequenceNumber is null ? null : PanelInformationDomain.DisplayName(sequenceNumber.Value, panelName),
                DisplayCode = displayCode
            });
        }

        return events;
    }

    private static async Task<IReadOnlyList<PanelInformationExcelImportBatchResponse>> ReadExcelImportBatchesAsync(
        NpgsqlDataSource dataSource,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select panel_information_excel_import_batches.id,
                   panel_information_excel_import_batches.project_id,
                   panel_information_excel_import_batches.original_file_name,
                   panel_information_excel_import_batches.file_size_bytes,
                   panel_information_excel_import_batches.file_sha256,
                   panel_information_excel_import_batches.input_unit,
                   panel_information_excel_import_batches.total_row_count,
                   panel_information_excel_import_batches.new_panel_count,
                   panel_information_excel_import_batches.changed_panel_count,
                   panel_information_excel_import_batches.unchanged_panel_count,
                   panel_information_excel_import_batches.skipped_panel_count,
                   panel_information_excel_import_batches.uploaded_by_user_id,
                   qms_users.display_name,
                   panel_information_excel_import_batches.uploaded_at_utc,
                   panel_information_excel_import_batches.reason
            from panel_information_excel_import_batches
            left join qms_users on qms_users.id = panel_information_excel_import_batches.uploaded_by_user_id
            where panel_information_excel_import_batches.project_id = @project_id
            order by panel_information_excel_import_batches.uploaded_at_utc desc,
                     panel_information_excel_import_batches.id desc;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var batches = new List<PanelInformationExcelImportBatchResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            batches.Add(new PanelInformationExcelImportBatchResponse
            {
                ImportBatchId = reader.GetGuid(0),
                ProjectId = reader.GetGuid(1),
                OriginalFileName = reader.GetString(2),
                FileSizeBytes = reader.GetInt64(3),
                FileSha256 = reader.GetString(4),
                InputUnit = reader.IsDBNull(5) ? null : reader.GetString(5),
                TotalRowCount = reader.GetInt32(6),
                NewPanelCount = reader.GetInt32(7),
                ChangedPanelCount = reader.GetInt32(8),
                UnchangedPanelCount = reader.GetInt32(9),
                SkippedPanelCount = reader.GetInt32(10),
                UploadedByUserId = reader.IsDBNull(11) ? null : reader.GetGuid(11),
                UploadedByUserName = reader.IsDBNull(12) ? null : reader.GetString(12),
                UploadedAtUtc = reader.GetFieldValue<DateTimeOffset>(13),
                Reason = reader.IsDBNull(14) ? null : reader.GetString(14)
            });
        }

        return batches;
    }

    private static IReadOnlyList<PanelInformationHistoryGroupResponse> BuildHistoryGroups(
        IReadOnlyList<PanelAuditEventResponse> auditEvents)
    {
        return auditEvents
            .GroupBy(HistoryGroupKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(item => item.ChangedAtUtc)
                    .ThenByDescending(item => item.AuditEventId)
                    .ToList();
                var representative = ordered[0];
                var changes = ordered
                    .OrderBy(item => item.PanelNumber, StringComparer.Ordinal)
                    .ThenBy(item => item.FieldName, StringComparer.Ordinal)
                    .Select(item => new PanelInformationHistoryChangeResponse
                    {
                        EntityType = item.EntityType,
                        EntityId = item.EntityId,
                        PanelNumber = item.PanelNumber,
                        PanelDisplayName = item.PanelDisplayName,
                        DisplayCode = item.DisplayCode,
                        FieldName = item.FieldName,
                        OldValue = item.OldValue,
                        NewValue = item.NewValue,
                        InputUnit = item.InputUnit,
                        OriginalInputValue = item.OriginalInputValue
                    })
                    .ToList();

                return new PanelInformationHistoryGroupResponse
                {
                    GroupId = group.Key,
                    ActionType = representative.Action,
                    InputSource = representative.InputSource,
                    ChangedByUserId = representative.ChangedByUserId,
                    ChangedByName = representative.ChangedByUserName,
                    ChangedAtUtc = representative.ChangedAtUtc,
                    Reason = representative.Reason,
                    ImportBatchId = representative.ImportBatchId,
                    ImportFileName = representative.ImportFileName,
                    ImportUploadedAtUtc = representative.ImportUploadedAtUtc,
                    AffectedPanelCount = ordered
                        .Where(item => item.EntityType == "Panel")
                        .Select(item => item.EntityId)
                        .Distinct()
                        .Count(),
                    ChangeCount = ordered.Count,
                    Changes = changes
                };
            })
            .OrderByDescending(group => group.ChangedAtUtc)
            .ThenByDescending(group => group.GroupId, StringComparer.Ordinal)
            .ToList();
    }

    private static string HistoryGroupKey(PanelAuditEventResponse auditEvent)
    {
        if (auditEvent.ImportBatchId is not null)
        {
            return $"import:{auditEvent.ImportBatchId.Value:N}";
        }

        return string.IsNullOrWhiteSpace(auditEvent.CorrelationId)
            ? $"event:{auditEvent.AuditEventId:N}"
            : $"correlation:{auditEvent.CorrelationId}";
    }

    private static async Task RollbackQuietlyAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Keep the original failure path.
        }
    }

    public static IReadOnlyDictionary<Guid, int> ParseExpectedVersions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<Guid, int>();
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<IReadOnlyList<PanelInformationExcelExpectedVersion>>(value, options) ?? [];
        return items
            .GroupBy(item => item.PanelId)
            .ToDictionary(group => group.Key, group => group.First().ExpectedPanelInfoVersion);
    }

    private sealed record PanelInformationProjectSnapshot(
        Guid ProjectId,
        string ProjectTitle,
        string Status,
        string? PackagingMethod,
        DateTimeOffset? DeletedAtUtc);

    private sealed record PanelInformationPanelSnapshot(
        Guid PanelId,
        Guid ProjectId,
        int SequenceNumber,
        string DisplayCode,
        string? PanelName,
        decimal? WidthMm,
        decimal? HeightMm,
        decimal? DepthMm,
        string PanelStatus,
        string WorkflowStage,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        int PanelInfoVersion,
        DateTimeOffset? PanelInfoUpdatedAtUtc,
        Guid? PanelInfoUpdatedByUserId,
        string? PanelInfoUpdatedByUserName);

    private sealed record PanelInfoFieldChange(
        string FieldName,
        string OldValue,
        string NewValue,
        string? OriginalInputValue,
        string? InputUnit);

    private sealed record EffectivePanelValues(
        string? PanelName,
        decimal? WidthMm,
        decimal? HeightMm,
        decimal? DepthMm);

    private sealed record ExcelBatchMetadata(
        string OriginalFileName,
        long FileSizeBytes,
        string FileSha256,
        string? InputUnit,
        int TotalRowCount,
        int NewPanelCount,
        int ChangedPanelCount,
        int UnchangedPanelCount,
        int SkippedPanelCount,
        string? Reason);
}
