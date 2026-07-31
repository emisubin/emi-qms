using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Materials;

public sealed class IqcReportStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    MaterialsStore materialsStore,
    IqcPdfRenderer pdfRenderer,
    TimeProvider timeProvider)
{
    private const int MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxReportPhotoBytes = 15 * 1024 * 1024;
    private const int MaxPhotos = 5;
    private const int MaxScanAttachmentBytes = 10 * 1024 * 1024;
    private const int MaxScanAttachments = 10;

    public async Task<IqcReportResponse?> GetAsync(
        Guid attemptId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var decisionMode = await ReadDecisionModeAsync(connection, attemptId, accessScope, cancellationToken);
        if (decisionMode == IqcDecisionModes.ScanBased)
        {
            return await BuildScanResponseAsync(connection, attemptId, accessScope, cancellationToken);
        }
        var context = await ReadAttemptContextAsync(connection, attemptId, accessScope, cancellationToken);
        return context is null
            ? null
            : await BuildResponseAsync(connection, context, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> InitializeAsync(
        Guid attemptId,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string decisionMode;
        await using (var gate = connection.CreateCommand())
        {
            gate.Transaction = transaction;
            gate.CommandText = """
                select attempt.status, attempt.decision_mode
                from material_iqc_attempts attempt
                join material_receipts receipt on receipt.id = attempt.material_receipt_id
                join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
                join projects project on project.id = item.project_id and project.deleted_at_utc is null
                where attempt.id = @attempt_id
                  and (@has_read_all or project.project_key = any(@project_keys))
                for update of attempt;
                """;
            gate.Parameters.AddWithValue("attempt_id", attemptId);
            AddScope(gate, accessScope);
            await using var reader = await gate.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return MaterialsMutationResult<IqcReportResponse>.NotFound();
            }
            decisionMode = reader.GetString(1);
            if (decisionMode is not (IqcDecisionModes.Detailed or IqcDecisionModes.ScanBased))
            {
                return MaterialsMutationResult<IqcReportResponse>.Conflict("기존 간편 판정 건에는 검사 기록을 만들 수 없습니다.");
            }
            if (reader.GetString(0) != "Requested")
            {
                return MaterialsMutationResult<IqcReportResponse>.Conflict("IQC 요청 상태에서만 성적서를 만들 수 있습니다.");
            }
        }

        if (decisionMode == IqcDecisionModes.ScanBased)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into material_iqc_scan_reports (
                    attempt_id, created_by_user_id, updated_by_user_id
                )
                values (@attempt_id, @actor_id, @actor_id)
                on conflict (attempt_id) do nothing;
                """;
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                    insert into iqc_reports (
                        attempt_id, template_version_id, created_by_user_id, updated_by_user_id
                    )
                    select @attempt_id, version.id, @actor_id, @actor_id
                    from iqc_report_template_versions version
                    join iqc_report_templates template on template.id = version.template_id
                    where template.template_code = 'MATERIAL_IQC' and version.is_active
                    on conflict (attempt_id) do nothing;
                    """;
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await using var existingCommand = connection.CreateCommand();
                existingCommand.Transaction = transaction;
                existingCommand.CommandText = "select exists(select 1 from iqc_reports where attempt_id = @attempt_id);";
                existingCommand.Parameters.AddWithValue("attempt_id", attemptId);
                if (await existingCommand.ExecuteScalarAsync(cancellationToken) is not true)
                {
                    return MaterialsMutationResult<IqcReportResponse>.Conflict("활성 IQC 성적서 양식을 찾을 수 없습니다.");
                }
            }
        }
        await transaction.CommitAsync(cancellationToken);
        var response = await GetAsync(attemptId, accessScope, cancellationToken);
        return response is null
            ? MaterialsMutationResult<IqcReportResponse>.NotFound()
            : MaterialsMutationResult<IqcReportResponse>.Success(response);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> AddScanAttachmentAsync(
        Guid reportId,
        int? expectedReportVersion,
        string originalFileName,
        byte[] content,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(originalFileName).Trim();
        var normalizedMime = DetectScanMime(content);
        var errors = new Dictionary<string, string[]>();
        if (expectedReportVersion is null or < 1)
        {
            errors[nameof(expectedReportVersion)] = ["최신 검사 기록 version이 필요합니다."];
        }
        if (safeName.Length is < 1 or > 180)
        {
            errors["file"] = ["파일명은 1~180자여야 합니다."];
        }
        if (content.Length is < 1 or > MaxScanAttachmentBytes)
        {
            errors["file"] = ["파일은 개별 10MB 이하여야 합니다."];
        }
        if (normalizedMime is null)
        {
            errors["file"] = ["실제 내용이 PDF, JPEG 또는 PNG인 파일만 등록할 수 있습니다."];
        }
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadScanReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateScanDraftState<IqcReportResponse>(context, expectedReportVersion!.Value);
        if (stateError is not null)
        {
            return stateError;
        }

        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = "select count(*)::int from material_iqc_scan_attachments where scan_report_id=@report_id;";
            countCommand.Parameters.AddWithValue("report_id", reportId);
            if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) >= MaxScanAttachments)
            {
                return Validation<IqcReportResponse>("file", $"검사 회차당 파일은 최대 {MaxScanAttachments}개까지 등록할 수 있습니다.");
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into material_iqc_scan_attachments (
                    scan_report_id, original_file_name, normalized_mime, byte_size,
                    sha256, content, created_by_user_id
                )
                values (
                    @report_id, @file_name, @mime, @byte_size,
                    @sha256, @content, @actor_id
                );

                update material_iqc_scan_reports
                set version=version + 1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=now()
                where id=@report_id and status='Draft' and version=@expected_version;
                """;
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("file_name", safeName);
            command.Parameters.AddWithValue("mime", normalizedMime!);
            command.Parameters.AddWithValue("byte_size", content.Length);
            command.Parameters.AddWithValue("sha256", Hash(content));
            command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("expected_version", expectedReportVersion.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 2)
            {
                return MaterialsMutationResult<IqcReportResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> DeleteScanAttachmentAsync(
        Guid reportId,
        Guid attachmentId,
        int? expectedReportVersion,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (expectedReportVersion is null or < 1)
        {
            return Validation<IqcReportResponse>(nameof(expectedReportVersion), "최신 검사 기록 version이 필요합니다.");
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadScanReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateScanDraftState<IqcReportResponse>(context, expectedReportVersion.Value);
        if (stateError is not null)
        {
            return stateError;
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                delete from material_iqc_scan_attachments
                where id=@attachment_id and scan_report_id=@report_id;

                update material_iqc_scan_reports
                set version=version + 1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=now()
                where id=@report_id and status='Draft' and version=@expected_version;
                """;
            command.Parameters.AddWithValue("attachment_id", attachmentId);
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("expected_version", expectedReportVersion.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 2)
            {
                return MaterialsMutationResult<IqcReportResponse>.Conflict("파일을 찾을 수 없거나 다른 사용자가 먼저 변경했습니다.");
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcPhotoContentResult>> GetScanAttachmentContentAsync(
        Guid reportId,
        Guid attachmentId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select attachment.content, attachment.normalized_mime, attachment.original_file_name
            from material_iqc_scan_attachments attachment
            join material_iqc_scan_reports report on report.id=attachment.scan_report_id
            join material_iqc_attempts attempt on attempt.id=report.attempt_id
            join material_receipts receipt on receipt.id=attempt.material_receipt_id
            join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
            join projects project on project.id=item.project_id and project.deleted_at_utc is null
            where report.id=@report_id
              and attachment.id=@attachment_id
              and (@has_read_all or project.project_key=any(@project_keys));
            """);
        command.Parameters.AddWithValue("report_id", reportId);
        command.Parameters.AddWithValue("attachment_id", attachmentId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MaterialsMutationResult<IqcPhotoContentResult>.Success(new IqcPhotoContentResult(
                reader.GetFieldValue<byte[]>(0),
                reader.GetString(1),
                reader.GetString(2)))
            : MaterialsMutationResult<IqcPhotoContentResult>.NotFound();
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> FinalizeScanAsync(
        Guid reportId,
        FinalizeIqcReportRequest request,
        Guid actorUserId,
        string? correlationId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFinalizeRequest(request);
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(errors);
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadScanReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateScanDraftState<IqcReportResponse>(context, request.ExpectedReportVersion!.Value);
        if (stateError is not null)
        {
            return stateError;
        }
        var attachments = await ReadScanAttachmentsAsync(connection, reportId, cancellationToken, transaction);
        if (attachments.Count == 0)
        {
            return Validation<IqcReportResponse>("files", "서명된 외함 수입검사서 스캔본을 1개 이상 등록해 주세요.");
        }
        var attachmentSnapshotRows = await ReadScanAttachmentSnapshotRowsAsync(
            connection,
            reportId,
            cancellationToken,
            transaction);
        var snapshotSource = string.Join(
            "\n",
            attachmentSnapshotRows)
            + $"\n{request.Result}|{request.Reason!.Trim()}";
        var result = await materialsStore.FinalizeScanIqcAsync(
            connection,
            transaction,
            context.AttemptId,
            reportId,
            request,
            Hash(Encoding.UTF8.GetBytes(snapshotSource)),
            timeProvider.GetUtcNow(),
            actorUserId,
            correlationId,
            cancellationToken);
        if (result.Status != MaterialsMutationStatus.Success)
        {
            return result.Status switch
            {
                MaterialsMutationStatus.NotFound => MaterialsMutationResult<IqcReportResponse>.NotFound(),
                MaterialsMutationStatus.Validation => MaterialsMutationResult<IqcReportResponse>.Validation(result.Errors),
                _ => MaterialsMutationResult<IqcReportResponse>.Conflict(result.Message ?? "검사 기록을 확정할 수 없습니다.")
            };
        }
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> SaveResponsesAsync(
        Guid reportId,
        SaveIqcResponsesRequest request,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedReportVersion is null or < 1)
        {
            return Validation<IqcReportResponse>(nameof(request.ExpectedReportVersion), "최신 성적서 version이 필요합니다.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateDraftState<IqcReportResponse>(context, request.ExpectedReportVersion.Value);
        if (stateError is not null)
        {
            return stateError;
        }

        var items = await ReadTemplateItemsAsync(connection, context.TemplateVersionId, cancellationToken, transaction);
        var reinspectionSource = await ReadReinspectionSourceAsync(connection, context, cancellationToken, transaction);
        items = SelectReinspectionItems(items, reinspectionSource);
        var itemById = items.ToDictionary(item => item.ItemId);
        var inputs = request.Responses ?? [];
        var errors = ValidateResponses(inputs, itemById, reinspectionSource?.Failures.Count > 0);
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(errors);
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from iqc_report_responses where report_id = @report_id;";
            delete.Parameters.AddWithValue("report_id", reportId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var input in inputs)
        {
            var item = itemById[input.TemplateItemId];
            var checkResult = Normalize(input.CheckResult);
            var textValue = Normalize(input.TextValue);
            var note = Normalize(input.Note);
            if (item.ResponseType == "Text" && textValue is null)
            {
                continue;
            }
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into iqc_report_responses (
                    report_id, template_item_id, check_result, text_value, note, updated_by_user_id
                ) values (@report_id, @item_id, @check_result, @text_value, @note, @actor_id);
                """;
            insert.Parameters.AddWithValue("report_id", reportId);
            insert.Parameters.AddWithValue("item_id", input.TemplateItemId);
            AddNullableText(insert, "check_result", checkResult);
            AddNullableText(insert, "text_value", textValue);
            AddNullableText(insert, "note", note);
            insert.Parameters.AddWithValue("actor_id", actorUserId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await IncrementReportVersionAsync(connection, transaction, reportId, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> AddPhotoAsync(
        Guid reportId,
        Guid templateItemId,
        int? expectedReportVersion,
        string? altText,
        byte[] content,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var normalizedAlt = Normalize(altText);
        var errors = new Dictionary<string, string[]>();
        if (expectedReportVersion is null or < 1)
        {
            errors[nameof(expectedReportVersion)] = ["최신 성적서 version이 필요합니다."];
        }
        if (normalizedAlt is null || normalizedAlt.Length > 200)
        {
            errors[nameof(altText)] = ["사진 설명을 1~200자로 입력해 주세요."];
        }
        if (content.Length is < 1 or > MaxPhotoBytes)
        {
            errors["photo"] = ["사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다."];
        }
        var normalizedMime = DetectImageMime(content);
        if (normalizedMime is null)
        {
            errors["photo"] = ["파일 내용이 올바른 JPEG 또는 PNG 사진이 아닙니다."];
        }
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateDraftState<IqcReportResponse>(context, expectedReportVersion!.Value);
        if (stateError is not null)
        {
            return stateError;
        }
        var items = await ReadTemplateItemsAsync(connection, context.TemplateVersionId, cancellationToken, transaction);
        var reinspectionSource = await ReadReinspectionSourceAsync(connection, context, cancellationToken, transaction);
        items = SelectReinspectionItems(items, reinspectionSource);
        if (!items.Any(item => item.ItemId == templateItemId))
        {
            return Validation<IqcReportResponse>(nameof(templateItemId), "현재 성적서 양식의 항목을 선택해 주세요.");
        }

        var photos = await ReadPhotosAsync(connection, reportId, cancellationToken, transaction);
        if (photos.Count >= MaxPhotos)
        {
            return Validation<IqcReportResponse>("photo", "성적서에는 사진을 최대 5장까지 등록할 수 있습니다.");
        }
        if (photos.Sum(photo => photo.ByteSize) + content.Length > MaxReportPhotoBytes)
        {
            return Validation<IqcReportResponse>("photo", "성적서 사진 전체 용량은 15MB를 초과할 수 없습니다.");
        }
        var usedSlots = photos.Select(photo => photo.DisplayName).ToHashSet(StringComparer.Ordinal);
        var extension = normalizedMime == "image/jpeg" ? "jpg" : "png";
        var slot = Enumerable.Range(1, MaxPhotos)
            .First(number => !usedSlots.Contains($"photo-{number}.jpg") && !usedSlots.Contains($"photo-{number}.png"));
        var displayName = $"photo-{slot}.{extension}";

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into iqc_report_photos (
                    report_id, template_item_id, display_name, normalized_mime, byte_size,
                    sha256, alt_text, content, created_by_user_id
                ) values (
                    @report_id, @item_id, @display_name, @mime, @byte_size,
                    @sha256, @alt_text, @content, @actor_id
                );
                """;
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("item_id", templateItemId);
            command.Parameters.AddWithValue("display_name", displayName);
            command.Parameters.AddWithValue("mime", normalizedMime!);
            command.Parameters.AddWithValue("byte_size", content.Length);
            command.Parameters.AddWithValue("sha256", Hash(content));
            command.Parameters.AddWithValue("alt_text", normalizedAlt!);
            command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await IncrementReportVersionAsync(connection, transaction, reportId, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> DeletePhotoAsync(
        Guid reportId,
        Guid photoId,
        int? expectedReportVersion,
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (expectedReportVersion is null or < 1)
        {
            return Validation<IqcReportResponse>(nameof(expectedReportVersion), "최신 성적서 version이 필요합니다.");
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateDraftState<IqcReportResponse>(context, expectedReportVersion.Value);
        if (stateError is not null)
        {
            return stateError;
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "delete from iqc_report_photos where id = @photo_id and report_id = @report_id;";
            command.Parameters.AddWithValue("photo_id", photoId);
            command.Parameters.AddWithValue("report_id", reportId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return MaterialsMutationResult<IqcReportResponse>.NotFound();
            }
        }
        await IncrementReportVersionAsync(connection, transaction, reportId, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> FinalizeAsync(
        Guid reportId,
        FinalizeIqcReportRequest request,
        Guid actorUserId,
        string? correlationId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFinalizeRequest(request);
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ReadReportForUpdateAsync(connection, transaction, reportId, accessScope, cancellationToken);
        if (context is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        var stateError = ValidateDraftState<IqcReportResponse>(context, request.ExpectedReportVersion!.Value);
        if (stateError is not null)
        {
            return stateError;
        }
        var items = await ReadTemplateItemsAsync(connection, context.TemplateVersionId, cancellationToken, transaction);
        var reinspectionSource = await ReadReinspectionSourceAsync(connection, context, cancellationToken, transaction);
        items = SelectReinspectionItems(items, reinspectionSource);
        var applicableItemIds = items.Select(item => item.ItemId).ToHashSet();
        var responses = (await ReadResponsesAsync(connection, reportId, cancellationToken, transaction))
            .Where(response => applicableItemIds.Contains(response.TemplateItemId)).ToList();
        var photos = (await ReadPhotosAsync(connection, reportId, cancellationToken, transaction))
            .Where(photo => applicableItemIds.Contains(photo.TemplateItemId)).ToList();
        var snapshotPhotos = await ReadSnapshotPhotosAsync(connection, reportId, cancellationToken, transaction);
        snapshotPhotos = snapshotPhotos.Where(photo => applicableItemIds.Contains(photo.TemplateItemId)).ToList();
        var invariantErrors = ValidateFinalization(items, responses, photos, request.Result!, request.Reason!);
        if (invariantErrors.Count > 0)
        {
            return MaterialsMutationResult<IqcReportResponse>.Validation(invariantErrors);
        }

        var finalizedAtUtc = timeProvider.GetUtcNow();
        var actorName = await ReadActorNameAsync(connection, transaction, actorUserId, cancellationToken);
        var snapshotText = BuildCanonicalSnapshot(
            context,
            items,
            responses,
            snapshotPhotos,
            request.Result!,
            request.Reason!.Trim(),
            actorName,
            finalizedAtUtc);
        var snapshotHash = Hash(Encoding.UTF8.GetBytes(snapshotText));
        var materialResult = await materialsStore.FinalizeDetailedIqcAsync(
            connection,
            transaction,
            context.AttemptId,
            reportId,
            request,
            snapshotText,
            snapshotHash,
            finalizedAtUtc,
            actorUserId,
            correlationId,
            cancellationToken);
        if (materialResult.Status != MaterialsMutationStatus.Success)
        {
            return materialResult.Status switch
            {
                MaterialsMutationStatus.NotFound => MaterialsMutationResult<IqcReportResponse>.NotFound(),
                MaterialsMutationStatus.Validation => MaterialsMutationResult<IqcReportResponse>.Validation(materialResult.Errors),
                MaterialsMutationStatus.Conflict => MaterialsMutationResult<IqcReportResponse>.Conflict(materialResult.Message ?? "성적서를 최종화할 수 없습니다."),
                _ => MaterialsMutationResult<IqcReportResponse>.Conflict("성적서를 최종화할 수 없습니다.")
            };
        }
        await transaction.CommitAsync(cancellationToken);

        await TryGeneratePdfAsync(reportId, cancellationToken);
        return await ReloadResultAsync(context.AttemptId, accessScope, cancellationToken);
    }

    public async Task<MaterialsMutationResult<IqcReportResponse>> RetryPdfAsync(
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var response = await ReadResponseByReportIdAsync(reportId, accessScope, cancellationToken);
        if (response is null)
        {
            return MaterialsMutationResult<IqcReportResponse>.NotFound();
        }
        if (response.ReportStatus != IqcReportStatuses.Finalized)
        {
            return MaterialsMutationResult<IqcReportResponse>.Conflict("최종화된 성적서만 PDF를 만들 수 있습니다.");
        }
        if (response.PdfStatus == IqcPdfStatuses.Ready)
        {
            return MaterialsMutationResult<IqcReportResponse>.Success(response);
        }
        if (response.PdfStatus != IqcPdfStatuses.Failed)
        {
            return MaterialsMutationResult<IqcReportResponse>.Conflict("PDF 생성이 진행 중입니다.");
        }
        await TryGeneratePdfAsync(reportId, cancellationToken);
        var reloaded = await ReadResponseByReportIdAsync(reportId, accessScope, cancellationToken);
        return reloaded is null
            ? MaterialsMutationResult<IqcReportResponse>.NotFound()
            : MaterialsMutationResult<IqcReportResponse>.Success(reloaded);
    }

    public async Task<MaterialsMutationResult<IqcPhotoContentResult>> GetPhotoContentAsync(
        Guid reportId,
        Guid photoId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select photo.content, photo.normalized_mime, photo.display_name
            from iqc_report_photos photo
            join iqc_reports report on report.id = photo.report_id
            join material_iqc_attempts attempt on attempt.id = report.attempt_id
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            where report.id = @report_id and photo.id = @photo_id
              and (@has_read_all or project.project_key = any(@project_keys));
            """);
        command.Parameters.AddWithValue("report_id", reportId);
        command.Parameters.AddWithValue("photo_id", photoId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MaterialsMutationResult<IqcPhotoContentResult>.Success(new IqcPhotoContentResult(
                reader.GetFieldValue<byte[]>(0), reader.GetString(1), reader.GetString(2)))
            : MaterialsMutationResult<IqcPhotoContentResult>.NotFound();
    }

    public async Task<MaterialsMutationResult<IqcPdfDownloadResult>> GetPdfAsync(
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select report.pdf_status, report.pdf_error_code, artifact.content
            from iqc_reports report
            join material_iqc_attempts attempt on attempt.id = report.attempt_id
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            left join iqc_report_pdf_artifacts artifact on artifact.report_id = report.id
            where report.id = @report_id and report.status = 'Finalized'
              and (@has_read_all or project.project_key = any(@project_keys));
            """);
        command.Parameters.AddWithValue("report_id", reportId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return MaterialsMutationResult<IqcPdfDownloadResult>.NotFound();
        }
        return MaterialsMutationResult<IqcPdfDownloadResult>.Success(new IqcPdfDownloadResult(
            reader.GetString(0),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2),
            reader.IsDBNull(1) ? null : reader.GetString(1)));
    }

    private async Task TryGeneratePdfAsync(Guid reportId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            string snapshotText;
            string snapshotHash;
            await using (var source = connection.CreateCommand())
            {
                source.CommandText = """
                    select snapshot_text, snapshot_sha256
                    from iqc_reports
                    where id = @report_id and status = 'Finalized' and pdf_status <> 'Ready';
                    """;
                source.Parameters.AddWithValue("report_id", reportId);
                await using var reader = await source.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return;
                }
                snapshotText = reader.GetString(0);
                snapshotHash = reader.GetString(1);
            }
            var photos = new List<IqcPdfPhoto>();
            await using (var photoCommand = connection.CreateCommand())
            {
                photoCommand.CommandText = """
                    select id, normalized_mime, content
                    from iqc_report_photos
                    where report_id = @report_id
                    order by created_at_utc, id;
                    """;
                photoCommand.Parameters.AddWithValue("report_id", reportId);
                await using var reader = await photoCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    photos.Add(new IqcPdfPhoto(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2)));
                }
            }
            var pdf = pdfRenderer.Render(snapshotText, photos);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into iqc_report_pdf_artifacts (
                        report_id, snapshot_sha256, byte_size, sha256, content, generator
                    ) values (@report_id, @snapshot_sha256, @byte_size, @sha256, @content, 'PDFsharp-6.2.4')
                    on conflict (report_id) do nothing;

                    update iqc_reports
                    set pdf_status = 'Ready', pdf_error_code = null, pdf_last_attempt_at_utc = now(),
                        updated_at_utc = now()
                    where id = @report_id
                      and exists (select 1 from iqc_report_pdf_artifacts where report_id = @report_id);
                    """;
                command.Parameters.AddWithValue("report_id", reportId);
                command.Parameters.AddWithValue("snapshot_sha256", snapshotHash);
                command.Parameters.AddWithValue("byte_size", pdf.Length);
                command.Parameters.AddWithValue("sha256", Hash(pdf));
                command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = pdf;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update iqc_reports
                set pdf_status = 'Failed', pdf_error_code = 'pdf_render_failed',
                    pdf_last_attempt_at_utc = now(), updated_at_utc = now()
                where id = @report_id and status = 'Finalized' and pdf_status <> 'Ready';
                """;
            command.Parameters.AddWithValue("report_id", reportId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<IqcReportResponse?> ReadResponseByReportIdAsync(
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("select attempt_id from iqc_reports where id = @report_id;");
        command.Parameters.AddWithValue("report_id", reportId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid attemptId ? await GetAsync(attemptId, accessScope, cancellationToken) : null;
    }

    private async Task<MaterialsMutationResult<IqcReportResponse>> ReloadResultAsync(
        Guid attemptId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync(attemptId, accessScope, cancellationToken);
        return response is null
            ? MaterialsMutationResult<IqcReportResponse>.NotFound()
            : MaterialsMutationResult<IqcReportResponse>.Success(response);
    }

    private static Dictionary<string, string[]> ValidateResponses(
        IReadOnlyList<SaveIqcItemResponseRequest> inputs,
        IReadOnlyDictionary<Guid, IqcTemplateItemResponse> itemById,
        bool reinspectionOnly)
    {
        var errors = new Dictionary<string, string[]>();
        if (inputs.GroupBy(input => input.TemplateItemId).Any(group => group.Count() > 1))
        {
            errors[nameof(SaveIqcResponsesRequest.Responses)] = ["같은 검사 항목을 두 번 저장할 수 없습니다."];
            return errors;
        }
        foreach (var input in inputs)
        {
            if (!itemById.TryGetValue(input.TemplateItemId, out var item))
            {
                errors[$"responses.{input.TemplateItemId}"] = ["현재 성적서 양식의 항목이 아닙니다."];
                continue;
            }
            var check = Normalize(input.CheckResult);
            var text = Normalize(input.TextValue);
            var note = Normalize(input.Note);
            if (note?.Length > 1000)
            {
                errors[$"responses.{input.TemplateItemId}.note"] = ["비고는 1,000자 이하여야 합니다."];
            }
            if (item.ResponseType == "Check")
            {
                if (check is not ("Pass" or "Fail") && (reinspectionOnly || check != "NotApplicable"))
                {
                    errors[$"responses.{input.TemplateItemId}.checkResult"] = [reinspectionOnly
                        ? "재검사 항목은 적합 또는 부적합을 선택해 주세요."
                        : "적합, 부적합 또는 해당없음을 선택해 주세요."];
                }
                if (check == "NotApplicable" && note is null)
                {
                    errors[$"responses.{input.TemplateItemId}.note"] = ["해당없음 사유를 입력해 주세요."];
                }
                if (text is not null)
                {
                    errors[$"responses.{input.TemplateItemId}.textValue"] = ["체크 항목에는 측정값을 입력할 수 없습니다."];
                }
            }
            else
            {
                if (check is not null)
                {
                    errors[$"responses.{input.TemplateItemId}.checkResult"] = ["값 입력 항목에는 판정을 선택할 수 없습니다."];
                }
                if (text is not null && text.Length > item.MaxTextLength)
                {
                    errors[$"responses.{input.TemplateItemId}.textValue"] = [$"값은 {item.MaxTextLength}자 이하여야 합니다."];
                }
            }
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateFinalizeRequest(FinalizeIqcReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ExpectedReportVersion is null or < 1)
        {
            errors[nameof(request.ExpectedReportVersion)] = ["최신 성적서 version이 필요합니다."];
        }
        if (request.ExpectedReceiptVersion is null or < 1)
        {
            errors[nameof(request.ExpectedReceiptVersion)] = ["최신 입고 version이 필요합니다."];
        }
        if (request.Result is not ("Passed" or "Failed"))
        {
            errors[nameof(request.Result)] = ["합격 또는 부적합을 선택해 주세요."];
        }
        var reason = Normalize(request.Reason);
        if (reason is null || reason.Length is < 3 or > 1000)
        {
            errors[nameof(request.Reason)] = ["판정 사유를 3~1,000자로 입력해 주세요."];
        }
        return errors;
    }

    private static Dictionary<string, string[]> ValidateFinalization(
        IReadOnlyList<IqcTemplateItemResponse> items,
        IReadOnlyList<IqcItemResponseValue> responses,
        IReadOnlyList<IqcPhotoResponse> photos,
        string result,
        string reason)
    {
        var errors = new Dictionary<string, string[]>();
        var responseByItem = responses.ToDictionary(response => response.TemplateItemId);
        foreach (var item in items.Where(item => item.IsRequired))
        {
            if (!responseByItem.TryGetValue(item.ItemId, out var response)
                || (item.ResponseType == "Check" && response.CheckResult is null)
                || (item.ResponseType == "Text" && string.IsNullOrWhiteSpace(response.TextValue)))
            {
                errors[$"items.{item.ItemId}"] = [$"{item.Label} 항목을 완료해 주세요."];
                continue;
            }
            if (response.CheckResult == "NotApplicable" && string.IsNullOrWhiteSpace(response.Note))
            {
                errors[$"items.{item.ItemId}.note"] = ["해당없음 사유를 입력해 주세요."];
            }
            if (item.RequiresPhoto && !photos.Any(photo => photo.TemplateItemId == item.ItemId))
            {
                errors[$"items.{item.ItemId}.photo"] = ["외함 상태 증빙 사진을 등록해 주세요."];
            }
        }
        var hasFail = responses.Any(response => response.CheckResult == "Fail");
        if (result == "Passed" && hasFail)
        {
            errors["Result"] = ["부적합 항목이 있어 합격으로 최종화할 수 없습니다."];
        }
        if (result == "Failed" && !hasFail)
        {
            errors["Result"] = ["부적합 판정에는 하나 이상의 부적합 항목이 필요합니다."];
        }
        if (result == "Failed" && photos.Count == 0 && reason.Trim().Length < 30)
        {
            errors["Reason"] = ["부적합 판정은 사진 1장 이상 또는 구체적인 근거 30자 이상이 필요합니다."];
        }
        return errors;
    }

    private static string BuildCanonicalSnapshot(
        ReportContext context,
        IReadOnlyList<IqcTemplateItemResponse> items,
        IReadOnlyList<IqcItemResponseValue> responses,
        IReadOnlyList<SnapshotPhoto> photos,
        string result,
        string reason,
        string actorName,
        DateTimeOffset finalizedAtUtc)
    {
        var responseByItem = responses.ToDictionary(response => response.TemplateItemId);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "emi-qms-iqc-report-v1");
            writer.WriteString("reportId", context.ReportId!.Value);
            writer.WriteString("attemptId", context.AttemptId);
            writer.WriteString("receiptId", context.ReceiptId);
            writer.WriteString("projectCode", context.ProjectCode);
            writer.WriteString("projectTitle", context.ProjectTitle);
            WriteNullableString(writer, "orderItem", context.OrderItem);
            if (context.Quantity is null) writer.WriteNull("quantity"); else writer.WriteNumber("quantity", context.Quantity.Value);
            WriteNullableString(writer, "unit", context.Unit);
            writer.WriteNumber("attemptNumber", context.AttemptNumber);
            writer.WriteNumber("templateVersion", context.TemplateVersion);
            writer.WriteString("result", result);
            writer.WriteString("reason", reason);
            writer.WriteString("finalizedAtUtc", finalizedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("finalizedBy", actorName);
            writer.WriteStartArray("items");
            foreach (var item in items.OrderBy(item => item.DisplayOrder))
            {
                responseByItem.TryGetValue(item.ItemId, out var response);
                writer.WriteStartObject();
                writer.WriteString("itemId", item.ItemId);
                writer.WriteString("itemCode", item.ItemCode);
                writer.WriteNumber("displayOrder", item.DisplayOrder);
                writer.WriteString("label", item.Label);
                writer.WriteString("responseType", item.ResponseType);
                writer.WriteBoolean("isRequired", item.IsRequired);
                writer.WriteBoolean("requiresPhoto", item.RequiresPhoto);
                WriteNullableString(writer, "checkResult", response?.CheckResult);
                WriteNullableString(writer, "textValue", response?.TextValue);
                WriteNullableString(writer, "note", response?.Note);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("photos");
            foreach (var photo in photos.OrderBy(photo => photo.CreatedAtUtc).ThenBy(photo => photo.PhotoId))
            {
                writer.WriteStartObject();
                writer.WriteString("photoId", photo.PhotoId);
                writer.WriteString("templateItemId", photo.TemplateItemId);
                writer.WriteString("displayName", photo.DisplayName);
                writer.WriteString("normalizedMime", photo.NormalizedMime);
                writer.WriteNumber("byteSize", photo.ByteSize);
                writer.WriteString("sha256", photo.Sha256);
                writer.WriteString("altText", photo.AltText);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task<IqcReportResponse> BuildResponseAsync(
        NpgsqlConnection connection,
        ReportContext context,
        CancellationToken cancellationToken)
    {
        var items = await ReadTemplateItemsAsync(connection, context.TemplateVersionId, cancellationToken);
        var reinspectionSource = await ReadReinspectionSourceAsync(connection, context, cancellationToken);
        items = SelectReinspectionItems(items, reinspectionSource);
        var applicableItemIds = items.Select(item => item.ItemId).ToHashSet();
        var responses = context.ReportId is null
            ? []
            : (await ReadResponsesAsync(connection, context.ReportId.Value, cancellationToken))
                .Where(response => applicableItemIds.Contains(response.TemplateItemId)).ToList();
        var photos = context.ReportId is null
            ? []
            : (await ReadPhotosAsync(connection, context.ReportId.Value, cancellationToken))
                .Where(photo => applicableItemIds.Contains(photo.TemplateItemId)).ToList();
        return new IqcReportResponse(
            context.AttemptId,
            context.ReceiptId,
            context.ProjectId,
            context.ProjectCode,
            context.ProjectTitle,
            context.OrderItem,
            context.Quantity,
            context.Unit,
            context.AttemptNumber,
            context.ReceiptVersion,
            context.AttemptStatus,
            context.DecisionMode,
            context.ReportId,
            context.ReportStatus,
            context.ReportVersion,
            context.Result,
            context.Reason,
            context.PdfStatus,
            context.PdfErrorCode,
            context.TemplateVersion,
            context.DecisionMode == IqcDecisionModes.Detailed
                && context.AttemptStatus == "Requested"
                && context.ReportStatus != IqcReportStatuses.Finalized,
            reinspectionSource,
            items,
            responses,
            photos,
            context.FinalizedAtUtc,
            context.FinalizedBy,
            [],
            []);
    }

    private static IReadOnlyList<IqcTemplateItemResponse> SelectReinspectionItems(
        IReadOnlyList<IqcTemplateItemResponse> items,
        IqcReinspectionSourceResponse? source)
    {
        if (source is null || source.Failures.Count == 0)
        {
            return items;
        }
        var failedCodes = source.Failures.Select(failure => failure.ItemCode).ToHashSet(StringComparer.Ordinal);
        return items.Where(item => failedCodes.Contains(item.ItemCode)).ToList();
    }

    private static async Task<string?> ReadDecisionModeAsync(
        NpgsqlConnection connection,
        Guid attemptId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select attempt.decision_mode
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id=attempt.material_receipt_id
            join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
            join projects project on project.id=item.project_id and project.deleted_at_utc is null
            where attempt.id=@attempt_id
              and (@has_read_all or project.project_key=any(@project_keys));
            """;
        command.Parameters.AddWithValue("attempt_id", attemptId);
        AddScope(command, accessScope);
        return Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IqcReportResponse?> BuildScanResponseAsync(
        NpgsqlConnection connection,
        Guid attemptId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        ScanResponseContext? context = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select attempt.id, receipt.id, project.id, project.project_code, project.project_title,
                       item.order_item, receipt.quantity, receipt.unit, attempt.attempt_number,
                       receipt.version, attempt.status, attempt.decision_mode,
                       report.id, report.status, report.version, report.result,
                       coalesce(report.reason, attempt.reason), report.finalized_at_utc, actor.display_name
                from material_iqc_attempts attempt
                join material_receipts receipt on receipt.id=attempt.material_receipt_id
                join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
                join projects project on project.id=item.project_id and project.deleted_at_utc is null
                left join material_iqc_scan_reports report on report.attempt_id=attempt.id
                left join qms_users actor on actor.id=report.finalized_by_user_id
                where attempt.id=@attempt_id
                  and attempt.decision_mode='ScanBased'
                  and (@has_read_all or project.project_key=any(@project_keys));
                """;
            command.Parameters.AddWithValue("attempt_id", attemptId);
            AddScope(command, accessScope);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            context = new ScanResponseContext(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetGuid(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetInt32(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
                reader.IsDBNull(18) ? null : reader.GetString(18));
        }

        var attachments = context.ReportId is null
            ? []
            : await ReadScanAttachmentsAsync(connection, context.ReportId.Value, cancellationToken);
        var history = await ReadScanHistoryAsync(connection, context.ReceiptId, context.AttemptNumber, cancellationToken);
        return new IqcReportResponse(
            context.AttemptId,
            context.ReceiptId,
            context.ProjectId,
            context.ProjectCode,
            context.ProjectTitle,
            context.OrderItem,
            context.Quantity,
            context.Unit,
            context.AttemptNumber,
            context.ReceiptVersion,
            context.AttemptStatus,
            context.DecisionMode,
            context.ReportId,
            context.ReportStatus,
            context.ReportVersion,
            context.Result,
            context.Reason,
            null,
            null,
            0,
            context.AttemptStatus == "Requested" && context.ReportStatus != IqcReportStatuses.Finalized,
            null,
            [],
            [],
            [],
            context.FinalizedAtUtc,
            context.FinalizedBy,
            attachments,
            history);
    }

    private static async Task<IReadOnlyList<IqcScanAttachmentResponse>> ReadScanAttachmentsAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, original_file_name, normalized_mime, byte_size, created_at_utc
            from material_iqc_scan_attachments
            where scan_report_id=@report_id
            order by created_at_utc, id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        var result = new List<IqcScanAttachmentResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IqcScanAttachmentResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<string>> ReadScanAttachmentSnapshotRowsAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, original_file_name, normalized_mime, byte_size, sha256
            from material_iqc_scan_attachments
            where scan_report_id=@report_id
            order by id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add($"{reader.GetGuid(0):N}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt32(3)}|{reader.GetString(4)}");
        }
        return rows;
    }

    private static async Task<IReadOnlyList<IqcScanAttemptHistoryResponse>> ReadScanHistoryAsync(
        NpgsqlConnection connection,
        Guid receiptId,
        int currentAttemptNumber,
        CancellationToken cancellationToken)
    {
        var histories = new List<(Guid ReportId, int AttemptNumber, string Result, string Reason, string? ActionReason, DateTimeOffset FinalizedAtUtc, string? FinalizedBy)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select report.id, attempt.attempt_number, report.result, report.reason,
                       (
                           select history.reason
                           from pending_history history
                           where history.pending_issue_id=attempt.pending_issue_id
                             and history.to_status='ReinspectionRequested'
                           order by history.created_at_utc desc, history.id desc
                           limit 1
                       ) as action_reason,
                       report.finalized_at_utc, actor.display_name
                from material_iqc_attempts attempt
                join material_iqc_scan_reports report on report.attempt_id=attempt.id and report.status='Finalized'
                left join qms_users actor on actor.id=report.finalized_by_user_id
                where attempt.material_receipt_id=@receipt_id
                  and attempt.attempt_number < @attempt_number
                order by attempt.attempt_number desc;
                """;
            command.Parameters.AddWithValue("receipt_id", receiptId);
            command.Parameters.AddWithValue("attempt_number", currentAttemptNumber);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                histories.Add((
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }
        var result = new List<IqcScanAttemptHistoryResponse>();
        foreach (var history in histories)
        {
            result.Add(new IqcScanAttemptHistoryResponse(
                history.ReportId,
                history.AttemptNumber,
                history.Result,
                history.Reason,
                history.ActionReason,
                history.FinalizedAtUtc,
                history.FinalizedBy,
                await ReadScanAttachmentsAsync(connection, history.ReportId, cancellationToken)));
        }
        return result;
    }

    private static async Task<ScanReportContext?> ReadScanReportForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select report.id, report.attempt_id, report.status, report.version,
                   receipt.version, attempt.status, attempt.decision_mode
            from material_iqc_scan_reports report
            join material_iqc_attempts attempt on attempt.id=report.attempt_id
            join material_receipts receipt on receipt.id=attempt.material_receipt_id
            join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
            join projects project on project.id=item.project_id and project.deleted_at_utc is null
            where report.id=@report_id
              and (@has_read_all or project.project_key=any(@project_keys))
            for update of report, attempt, receipt, item;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ScanReportContext(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6))
            : null;
    }

    private static MaterialsMutationResult<T>? ValidateScanDraftState<T>(ScanReportContext context, int expectedVersion)
    {
        if (context.DecisionMode != IqcDecisionModes.ScanBased || context.AttemptStatus != "Requested")
        {
            return MaterialsMutationResult<T>.Conflict("현재 IQC 요청은 스캔 검사 기록을 수정할 수 없습니다.");
        }
        if (context.Status != IqcReportStatuses.Draft)
        {
            return MaterialsMutationResult<T>.Conflict("확정된 검사 기록은 수정할 수 없습니다.");
        }
        return context.Version == expectedVersion
            ? null
            : MaterialsMutationResult<T>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
    }

    private static async Task<IqcReinspectionSourceResponse?> ReadReinspectionSourceAsync(
        NpgsqlConnection connection,
        ReportContext context,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        if (context.AttemptNumber <= 1)
        {
            return null;
        }

        Guid previousAttemptId;
        Guid? previousReportId;
        Guid? pendingId;
        int previousAttemptNumber;
        string failureReason;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select previous.id, previous.attempt_number,
                       coalesce(previous_report.reason, previous.reason, '이전 검사 부적합'),
                       current.pending_issue_id, previous_report.id
                from material_iqc_attempts current
                join lateral (
                    select candidate.id, candidate.attempt_number, candidate.reason
                    from material_iqc_attempts candidate
                    where candidate.material_receipt_id = current.material_receipt_id
                      and candidate.attempt_number < current.attempt_number
                      and candidate.status = 'Failed'
                    order by candidate.attempt_number desc
                    limit 1
                ) previous on true
                left join iqc_reports previous_report on previous_report.attempt_id = previous.id
                where current.id = @attempt_id;
                """;
            command.Parameters.AddWithValue("attempt_id", context.AttemptId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            previousAttemptId = reader.GetGuid(0);
            previousAttemptNumber = reader.GetInt32(1);
            failureReason = reader.GetString(2);
            pendingId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            previousReportId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
        }

        var failures = new List<IqcReinspectionFailureResponse>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select item.item_code, item.label, response.note
                from iqc_reports report
                join iqc_report_responses response on response.report_id = report.id and response.check_result = 'Fail'
                join iqc_report_template_items item on item.id = response.template_item_id
                where report.attempt_id = @attempt_id
                order by item.display_order;
                """;
            command.Parameters.AddWithValue("attempt_id", previousAttemptId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                failures.Add(new IqcReinspectionFailureResponse(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var originalFailurePhotos = new List<EvidencePhotoReferenceResponse>();
        if (previousReportId is not null)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select id, display_name, normalized_mime, byte_size, alt_text
                from iqc_report_photos
                where report_id = @report_id
                order by created_at_utc, id;
                """;
            command.Parameters.AddWithValue("report_id", previousReportId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                originalFailurePhotos.Add(new EvidencePhotoReferenceResponse(
                    "IqcReport", previousReportId.Value, reader.GetGuid(0), reader.GetString(1),
                    reader.GetString(2), reader.GetInt32(3), reader.GetString(4)));
            }
        }

        string? actionReason = null;
        PendingActionRoundEvidenceResponse? actionRound = null;
        if (pendingId is not null)
        {
            actionRound = await PendingStore.ReadLatestConfirmedActionRoundAsync(
                connection, transaction, pendingId.Value, cancellationToken);
            actionReason = actionRound?.ActionReasonSnapshot;
            if (actionReason is null)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    select reason
                    from pending_history
                    where pending_issue_id = @pending_id
                      and to_status = 'ReinspectionRequested'
                    order by created_at_utc desc, id desc
                    limit 1;
                    """;
                command.Parameters.AddWithValue("pending_id", pendingId.Value);
                actionReason = Convert.ToString(
                    await command.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return new IqcReinspectionSourceResponse(
            previousAttemptNumber,
            failureReason,
            actionReason,
            failures,
            new ReinspectionEvidenceResponse(originalFailurePhotos, actionRound));
    }

    private static async Task<ReportContext?> ReadAttemptContextAsync(
        NpgsqlConnection connection,
        Guid attemptId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select attempt.id, receipt.id, project.id, project.project_code, project.project_title,
                   item.order_item, receipt.quantity, receipt.unit, attempt.attempt_number,
                   receipt.version, attempt.status, attempt.decision_mode,
                   report.id, report.status, report.version, report.result, coalesce(report.reason, attempt.reason),
                   report.pdf_status, report.pdf_error_code, template_version.id,
                   template_version.version_number, report.finalized_at_utc, actor.display_name
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            left join iqc_reports report on report.attempt_id = attempt.id
            join lateral (
                select version.id, version.version_number
                from iqc_report_template_versions version
                join iqc_report_templates template on template.id = version.template_id
                where version.id = report.template_version_id
                   or (report.id is null and template.template_code = 'MATERIAL_IQC' and version.is_active)
                order by case when version.id = report.template_version_id then 0 else 1 end
                limit 1
            ) template_version on true
            left join qms_users actor on actor.id = report.finalized_by_user_id
            where attempt.id = @attempt_id
              and (@has_read_all or project.project_key = any(@project_keys));
            """;
        command.Parameters.AddWithValue("attempt_id", attemptId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapContext(reader) : null;
    }

    private static async Task<ReportContext?> ReadReportForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reportId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select attempt.id, receipt.id, project.id, project.project_code, project.project_title,
                   item.order_item, receipt.quantity, receipt.unit, attempt.attempt_number,
                   receipt.version, attempt.status, attempt.decision_mode,
                   report.id, report.status, report.version, report.result, coalesce(report.reason, attempt.reason),
                   report.pdf_status, report.pdf_error_code, version.id,
                   version.version_number, report.finalized_at_utc, actor.display_name
            from iqc_reports report
            join material_iqc_attempts attempt on attempt.id = report.attempt_id
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            join iqc_report_template_versions version on version.id = report.template_version_id
            left join qms_users actor on actor.id = report.finalized_by_user_id
            where report.id = @report_id
              and (@has_read_all or project.project_key = any(@project_keys))
            for update of attempt, receipt, item, report;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        AddScope(command, accessScope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapContext(reader) : null;
    }

    private static ReportContext MapContext(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetInt32(8), reader.GetInt32(9),
        reader.GetString(10), reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetGuid(12),
        reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetInt32(14),
        reader.IsDBNull(15) ? null : reader.GetString(15), reader.IsDBNull(16) ? null : reader.GetString(16),
        reader.IsDBNull(17) ? null : reader.GetString(17), reader.IsDBNull(18) ? null : reader.GetString(18),
        reader.GetGuid(19), reader.GetInt32(20),
        reader.IsDBNull(21) ? null : reader.GetFieldValue<DateTimeOffset>(21),
        reader.IsDBNull(22) ? null : reader.GetString(22));

    private static async Task<IReadOnlyList<IqcTemplateItemResponse>> ReadTemplateItemsAsync(
        NpgsqlConnection connection,
        Guid templateVersionId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, item_code, display_order, label, guidance, response_type,
                   is_required, requires_photo, max_text_length
            from iqc_report_template_items
            where template_version_id = @version_id
            order by display_order;
            """;
        command.Parameters.AddWithValue("version_id", templateVersionId);
        var result = new List<IqcTemplateItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IqcTemplateItemResponse(
                reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetBoolean(6),
                reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetInt32(8)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<IqcItemResponseValue>> ReadResponsesAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select template_item_id, check_result, text_value, note
            from iqc_report_responses where report_id = @report_id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        var result = new List<IqcItemResponseValue>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IqcItemResponseValue(
                reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<IqcPhotoResponse>> ReadPhotosAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, template_item_id, display_name, normalized_mime, byte_size, alt_text, created_at_utc
            from iqc_report_photos where report_id = @report_id order by created_at_utc, id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        var result = new List<IqcPhotoResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IqcPhotoResponse(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<SnapshotPhoto>> ReadSnapshotPhotosAsync(
        NpgsqlConnection connection,
        Guid reportId,
        CancellationToken cancellationToken,
        NpgsqlTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, template_item_id, display_name, normalized_mime, byte_size, sha256, alt_text, created_at_utc
            from iqc_report_photos where report_id = @report_id order by created_at_utc, id;
            """;
        command.Parameters.AddWithValue("report_id", reportId);
        var result = new List<SnapshotPhoto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SnapshotPhoto(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }
        return result;
    }

    private static async Task<string> ReadActorNameAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select display_name from qms_users where id = @id and is_active;";
        command.Parameters.AddWithValue("id", actorUserId);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture)
            ?? "품질 담당";
    }

    private static async Task IncrementReportVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid reportId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update iqc_reports
            set version = version + 1, updated_by_user_id = @actor_id, updated_at_utc = now()
            where id = @report_id and status = 'Draft';
            """;
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("report_id", reportId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static MaterialsMutationResult<T>? ValidateDraftState<T>(ReportContext context, int expectedVersion)
    {
        if (context.DecisionMode != IqcDecisionModes.Detailed || context.AttemptStatus != "Requested")
        {
            return MaterialsMutationResult<T>.Conflict("현재 IQC 요청은 상세 성적서를 수정할 수 없습니다.");
        }
        if (context.ReportStatus != IqcReportStatuses.Draft)
        {
            return MaterialsMutationResult<T>.Conflict("최종화된 성적서는 수정할 수 없습니다.");
        }
        return context.ReportVersion == expectedVersion
            ? null
            : MaterialsMutationResult<T>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
    }

    private static string? DetectImageMime(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return content.AsSpan().StartsWith(png) ? "image/png" : null;
    }

    private static string? DetectScanMime(byte[] content)
    {
        ReadOnlySpan<byte> pdf = [0x25, 0x50, 0x44, 0x46, 0x2D];
        if (content.AsSpan().StartsWith(pdf))
        {
            return "application/pdf";
        }
        return DetectImageMime(content);
    }

    private static string Hash(byte[] content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MaterialsMutationResult<T> Validation<T>(string field, string message)
        => MaterialsMutationResult<T>.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name); else writer.WriteString(name, value);
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private static void AddScope(NpgsqlCommand command, ProjectAccessScope accessScope)
    {
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
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

    private sealed record ReportContext(
        Guid AttemptId,
        Guid ReceiptId,
        Guid ProjectId,
        string ProjectCode,
        string ProjectTitle,
        string? OrderItem,
        decimal? Quantity,
        string? Unit,
        int AttemptNumber,
        int ReceiptVersion,
        string AttemptStatus,
        string DecisionMode,
        Guid? ReportId,
        string? ReportStatus,
        int? ReportVersion,
        string? Result,
        string? Reason,
        string? PdfStatus,
        string? PdfErrorCode,
        Guid TemplateVersionId,
        int TemplateVersion,
        DateTimeOffset? FinalizedAtUtc,
        string? FinalizedBy);

    private sealed record ScanResponseContext(
        Guid AttemptId,
        Guid ReceiptId,
        Guid ProjectId,
        string ProjectCode,
        string ProjectTitle,
        string? OrderItem,
        decimal? Quantity,
        string? Unit,
        int AttemptNumber,
        int ReceiptVersion,
        string AttemptStatus,
        string DecisionMode,
        Guid? ReportId,
        string? ReportStatus,
        int? ReportVersion,
        string? Result,
        string? Reason,
        DateTimeOffset? FinalizedAtUtc,
        string? FinalizedBy);

    private sealed record ScanReportContext(
        Guid ReportId,
        Guid AttemptId,
        string Status,
        int Version,
        int ReceiptVersion,
        string AttemptStatus,
        string DecisionMode);

    private sealed record SnapshotPhoto(
        Guid PhotoId,
        Guid TemplateItemId,
        string DisplayName,
        string NormalizedMime,
        int ByteSize,
        string Sha256,
        string AltText,
        DateTimeOffset CreatedAtUtc);
}

public sealed record IqcPdfPhoto(Guid PhotoId, string NormalizedMime, byte[] Content);
