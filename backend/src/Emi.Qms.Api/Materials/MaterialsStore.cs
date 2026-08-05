using System.Globalization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Materials;

public sealed class MaterialsStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    PendingStore pendingStore,
    TimeProvider timeProvider)
{
    public async Task<MaterialReceiptListResponse> ListAsync(
        string? search,
        bool includeCompleted,
        string? supplyType,
        DateOnly? expectedReceiptDateFrom,
        DateOnly? expectedReceiptDateTo,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                item.id, item.project_id, project.project_title, project.project_code,
                item.order_item, item.supplier_name, item.expected_receipt_date,
                item.order_quantity, item.order_unit, item.material_arrivals_closed_at_utc,
                item.receipt_completed, item.row_version, item.supply_type,
                item.material_category_name_snapshot, item.material_category_requires_iqc_snapshot
            from project_procurement_items item
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            where item.status = 'Active'
              and (@has_read_all or project.project_key = any(@project_keys))
              and (@include_completed or not item.receipt_completed)
              and (@supply_type is null or item.supply_type = @supply_type)
              and (@date_from is null or item.expected_receipt_date >= @date_from)
              and (@date_to is null or item.expected_receipt_date <= @date_to)
              and (
                  @search is null
                  or project.project_title ilike '%' || @search || '%'
                  or project.project_code ilike '%' || @search || '%'
                  or coalesce(item.order_item, '') ilike '%' || @search || '%'
                  or coalesce(item.supplier_name, '') ilike '%' || @search || '%'
              )
            order by project.project_code, item.sequence_number;
            """;
        AddNullableText(command, "search", string.IsNullOrWhiteSpace(search) ? null : search.Trim());
        command.Parameters.AddWithValue("include_completed", includeCompleted);
        AddNullableText(command, "supply_type", supplyType);
        AddNullableDate(command, "date_from", expectedReceiptDateFrom);
        AddNullableDate(command, "date_to", expectedReceiptDateTo);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());

        var items = new List<MutableMaterialItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new MutableMaterialItem
                {
                    ItemId = reader.GetGuid(0),
                    ProjectId = reader.GetGuid(1),
                    ProjectTitle = reader.GetString(2),
                    ProjectCode = reader.GetString(3),
                    OrderItem = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SupplierName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ExpectedReceiptDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                    OrderQuantity = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    OrderUnit = reader.IsDBNull(8) ? null : reader.GetString(8),
                    ArrivalsClosedAtUtc = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                    ReceiptCompleted = reader.GetBoolean(10),
                    RowVersion = reader.GetInt32(11),
                    SupplyType = reader.GetString(12),
                    MaterialCategoryName = reader.IsDBNull(13) ? null : reader.GetString(13),
                    MaterialCategoryRequiresIqc = reader.IsDBNull(14) ? null : reader.GetBoolean(14)
                });
            }
        }

        if (items.Count > 0)
        {
            await LoadReceiptsAsync(connection, items, cancellationToken);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var responseItems = items.Select(item => item.ToResponse(today)).ToList();
        var receipts = responseItems.SelectMany(item => item.Receipts).ToList();
        return new MaterialReceiptListResponse(
            new MaterialReceiptSummaryResponse(
                responseItems.Count(item => !item.ArrivalsClosed && item.Receipts.Count == 0),
                receipts.Count(receipt => receipt.Status == MaterialReceiptStatuses.IqcRequested),
                receipts.Count(receipt => receipt.Status == MaterialReceiptStatuses.FailedBlocked),
                receipts.Count(receipt => receipt.Status is MaterialReceiptStatuses.Passed or MaterialReceiptStatuses.InspectionNotRequired),
                responseItems.Count(item => item.ReceiptCompleted),
                responseItems.Count(item => item.SupplyType == ProcurementSupplyTypes.CustomerSupplied),
                responseItems.Count(item => item.CustomerSupplyOverdue)),
            responseItems);
    }

    public async Task<MaterialIqcQueueResponse> ListIqcAsync(
        bool includeDecided,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                attempt.id, receipt.id, item.id, project.id, project.project_title, project.project_code,
                item.order_item, receipt.quantity, receipt.unit, attempt.attempt_number, receipt.version,
                attempt.status, attempt.decision_mode, attempt.requested_at_utc, attempt.decided_at_utc,
                attempt.pending_issue_id, pending.issue_number, item.supply_type, attempt.reason,
                coalesce(report.id, scan_report.id),
                coalesce(report.status, scan_report.status),
                report.pdf_status
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id
            join projects project on project.id = item.project_id and project.deleted_at_utc is null
            left join pending_issues pending on pending.id = attempt.pending_issue_id
            left join iqc_reports report on report.attempt_id = attempt.id
            left join material_iqc_scan_reports scan_report on scan_report.attempt_id = attempt.id
            where (@include_decided or attempt.status = 'Requested')
              and (@has_read_all or project.project_key = any(@project_keys))
            order by case when attempt.status = 'Requested' then 0 else 1 end,
                     attempt.requested_at_utc desc;
            """);
        command.Parameters.AddWithValue("include_decided", includeDecided);
        command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
        command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
        var items = new List<MaterialIqcQueueItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MaterialIqcQueueItemResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(17),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetFieldValue<DateTimeOffset>(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.IsDBNull(15) ? null : reader.GetGuid(15),
                reader.IsDBNull(16) ? null : reader.GetInt64(16),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.IsDBNull(19) ? null : reader.GetGuid(19),
                reader.IsDBNull(20) ? null : reader.GetString(20),
                reader.IsDBNull(21) ? null : reader.GetString(21)));
        }
        return new MaterialIqcQueueResponse(items);
    }

    public async Task<MaterialIqcReconciliationResponse> ReconcileIqcHandoffsAsync(
        Guid actorUserId,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var orphanReceipts = new List<ReceiptSnapshot>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select receipt.id, receipt.procurement_item_id, item.project_id, receipt.status, receipt.version,
                       item.receipt_completed, item.order_item
                from material_receipts receipt
                join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
                join projects project on project.id=item.project_id and project.deleted_at_utc is null
                where receipt.status='Arrived'
                  and (@has_read_all or project.project_key=any(@project_keys))
                  and not exists (
                      select 1 from material_iqc_attempts attempt
                      where attempt.material_receipt_id=receipt.id
                  )
                order by receipt.created_at_utc, receipt.id
                for update of receipt, item;
                """;
            command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
            command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orphanReceipts.Add(new ReceiptSnapshot(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
                    reader.GetInt32(4), reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }

        foreach (var receipt in orphanReceipts)
        {
            await CreateIqcAttemptAsync(connection, transaction, receipt, actorUserId, cancellationToken);
        }

        var requestedAttempts = new List<(Guid ProjectId, Guid ItemId, Guid ReceiptId, Guid AttemptId, string? OrderItem)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select item.project_id, item.id, receipt.id, attempt.id, item.order_item
                from material_iqc_attempts attempt
                join material_receipts receipt on receipt.id=attempt.material_receipt_id and receipt.status='IqcRequested'
                join project_procurement_items item on item.id=receipt.procurement_item_id and item.status='Active'
                join projects project on project.id=item.project_id and project.deleted_at_utc is null
                where attempt.status='Requested'
                  and (@has_read_all or project.project_key=any(@project_keys))
                order by attempt.requested_at_utc, attempt.id;
                """;
            command.Parameters.AddWithValue("has_read_all", accessScope.HasProjectReadAll);
            command.Parameters.AddWithValue("project_keys", accessScope.ProjectKeys.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                requestedAttempts.Add((
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
            }
        }

        foreach (var attempt in requestedAttempts)
        {
            await CreateIqcWorkItemAsync(
                connection,
                transaction,
                attempt.ProjectId,
                attempt.ItemId,
                attempt.ReceiptId,
                attempt.AttemptId,
                attempt.OrderItem,
                actorUserId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new MaterialIqcReconciliationResponse(orphanReceipts.Count, requestedAttempts.Count);
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> RegisterArrivalAsync(
        Guid itemId,
        RegisterMaterialArrivalRequest request,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateArrival(request);
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var item = await ReadItemForUpdateAsync(connection, transaction, itemId, cancellationToken);
        if (item is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (item.ArrivalsClosedAtUtc is not null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("입고 마감된 품목에는 도착분을 추가할 수 없습니다.");
        }

        var unit = request.Unit!.Trim();
        if (item.OrderUnit is not null && !string.Equals(item.OrderUnit, unit, StringComparison.OrdinalIgnoreCase))
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Unit)] = [$"{MeasurementLabel(item.SupplyType)} 단위({item.OrderUnit})와 같은 단위를 입력해 주세요."]
            });
        }

        if (item.OrderQuantity is null || string.IsNullOrWhiteSpace(item.OrderUnit))
        {
            var measurementName = item.SupplyType == ProcurementSupplyTypes.CustomerSupplied ? "제공 예정 수량·단위" : "발주 수량·단위";
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(
                $"{measurementName}가 없습니다. 구매팀이 구매 탭에서 먼저 입력해야 도착 등록을 할 수 있습니다.");
        }

        var requiresIqc = item.IqcRoutingPolicy == ProjectIqcRoutingPolicies.AllReceipts
            || item.MaterialCategoryRequiresIqc == true;
        if (item.IqcRoutingPolicy == ProjectIqcRoutingPolicies.CategoryBased
            && item.MaterialCategoryRequiresIqc is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(
                "구매품 구분이 없습니다. 구매팀이 구매 탭에서 구분을 선택한 뒤 다시 시도해 주세요.");
        }

        if (requiresIqc
            && (await ResolveQualityIqcAssigneesAsync(connection, transaction, item.ProjectId, cancellationToken)).Count == 0)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(
                "IQC 담당자가 없어 도착분을 품질 검사로 인계할 수 없습니다. 생산관리에서 IQC 정·부 담당자를 지정한 뒤 다시 시도해 주세요.");
        }

        var effectiveOrderQuantity = item.OrderQuantity.Value;
        await using (var quantityCommand = connection.CreateCommand())
        {
            quantityCommand.Transaction = transaction;
            quantityCommand.CommandText = """
                select coalesce(sum(quantity) filter (where status <> 'Cancelled'), 0)
                from material_receipts
                where procurement_item_id = @item_id;
                """;
            quantityCommand.Parameters.AddWithValue("item_id", itemId);
            var arrivedQuantity = Convert.ToDecimal(await quantityCommand.ExecuteScalarAsync(cancellationToken));
            if (arrivedQuantity + request.Quantity!.Value > effectiveOrderQuantity)
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.Quantity)] = [$"누적 도착 수량은 {MeasurementLabel(item.SupplyType)}({effectiveOrderQuantity:0.###} {unit})을 초과할 수 없습니다."]
                });
            }
        }

        var receiptId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into material_receipts (
                    id, procurement_item_id, quantity, unit, arrival_date, note, status,
                    created_by_user_id, updated_by_user_id
                )
                values (
                    @id, @item_id, @quantity, @unit, @arrival_date, @note, 'Arrived',
                    @actor_id, @actor_id
                );
                """;
            command.Parameters.AddWithValue("id", receiptId);
            command.Parameters.AddWithValue("item_id", itemId);
            command.Parameters.AddWithValue("quantity", request.Quantity!.Value);
            command.Parameters.AddWithValue("unit", unit);
            command.Parameters.AddWithValue("arrival_date", request.ArrivalDate!.Value);
            AddNullableText(command, "note", NormalizeOptional(request.Note));
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertEventAsync(connection, transaction, itemId, receiptId, "Arrived", null, MaterialReceiptStatuses.Arrived, request.Note, actorUserId, cancellationToken);
        Guid? attemptId = null;
        string nextStatus;
        if (requiresIqc)
        {
            attemptId = await CreateIqcAttemptAsync(
                connection,
                transaction,
                new ReceiptSnapshot(receiptId, itemId, item.ProjectId, MaterialReceiptStatuses.Arrived, 1, item.ReceiptCompleted, item.OrderItem),
                actorUserId,
                cancellationToken,
                decisionMode: item.IqcRoutingPolicy == ProjectIqcRoutingPolicies.CategoryBased
                    ? IqcDecisionModes.ScanBased
                    : IqcDecisionModes.Detailed);
            nextStatus = MaterialReceiptStatuses.IqcRequested;
        }
        else
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update material_receipts
                    set status = 'InspectionNotRequired',
                        version = version + 1,
                        updated_by_user_id = @actor_id,
                        updated_at_utc = now()
                    where id = @receipt_id
                      and version = 1;
                    """;
                command.Parameters.AddWithValue("receipt_id", receiptId);
                command.Parameters.AddWithValue("actor_id", actorUserId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertEventAsync(
                connection,
                transaction,
                itemId,
                receiptId,
                "IqcNotRequired",
                MaterialReceiptStatuses.Arrived,
                MaterialReceiptStatuses.InspectionNotRequired,
                $"{item.MaterialCategoryName ?? "비검사 구분"} · IQC 대상 아님",
                actorUserId,
                cancellationToken);
            await CreateConfirmationWorkItemAsync(
                connection,
                transaction,
                item.ProjectId,
                item.ItemId,
                receiptId,
                item.OrderItem,
                actorUserId,
                cancellationToken,
                iqcNotRequired: true);
            nextStatus = MaterialReceiptStatuses.InspectionNotRequired;
        }
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(itemId, receiptId, attemptId, null, nextStatus, false));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> RequestIqcAsync(
        Guid receiptId,
        MaterialReceiptVersionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var receipt = await ReadReceiptForUpdateAsync(connection, transaction, receiptId, cancellationToken);
        if (receipt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (receipt.Status == MaterialReceiptStatuses.IqcRequested)
        {
            await using var replayCommand = connection.CreateCommand();
            replayCommand.Transaction = transaction;
            replayCommand.CommandText = """
                select id, pending_issue_id
                from material_iqc_attempts
                where material_receipt_id = @receipt_id and status = 'Requested'
                order by attempt_number desc limit 1;
                """;
            replayCommand.Parameters.AddWithValue("receipt_id", receiptId);
            await using var replayReader = await replayCommand.ExecuteReaderAsync(cancellationToken);
            if (await replayReader.ReadAsync(cancellationToken))
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
                    new MaterialReceiptActionResponse(
                        receipt.ItemId, receiptId, replayReader.GetGuid(0),
                        replayReader.IsDBNull(1) ? null : replayReader.GetGuid(1),
                        MaterialReceiptStatuses.IqcRequested, receipt.ReceiptCompleted));
            }
        }
        var versionError = ValidateExpectedVersion(request.ExpectedVersion, receipt.Version);
        if (versionError is not null)
        {
            return versionError;
        }
        if (receipt.Status != MaterialReceiptStatuses.Arrived)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("도착 등록 상태에서만 IQC를 요청할 수 있습니다.");
        }

        var attemptId = await CreateIqcAttemptAsync(connection, transaction, receipt, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(receipt.ItemId, receiptId, attemptId, null, MaterialReceiptStatuses.IqcRequested, receipt.ReceiptCompleted));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> RecordIqcResultAsync(
        Guid attemptId,
        MaterialIqcResultRequest request,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var result = request.Result?.Trim();
        var reason = NormalizeOptional(request.Reason);
        var errors = new Dictionary<string, string[]>();
        if (result is not ("Passed" or "Failed"))
        {
            errors[nameof(request.Result)] = ["합격 또는 부적합을 선택해 주세요."];
        }
        if (reason is null || reason.Length is < 3 or > 1000)
        {
            errors[nameof(request.Reason)] = ["판정 사유를 3~1,000자로 입력해 주세요."];
        }
        if (result == "Failed" && reason is not null && reason.Length < 30)
        {
            errors[nameof(request.Reason)] = ["사진이 없는 부적합 판정은 구체적인 근거를 30자 이상 입력해 주세요."];
        }
        if (request.ExpectedReceiptVersion is null or < 1)
        {
            errors[nameof(request.ExpectedReceiptVersion)] = ["최신 입고 version이 필요합니다."];
        }
        if (errors.Count > 0)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var attempt = await ReadAttemptForUpdateAsync(connection, transaction, attemptId, cancellationToken);
        if (attempt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (attempt.Status != "Requested" || attempt.ReceiptStatus != MaterialReceiptStatuses.IqcRequested)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("이미 판정되었거나 IQC 요청 상태가 아닙니다.");
        }
        if (attempt.DecisionMode != IqcDecisionModes.Legacy)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Result)] = ["상세 검사성적서를 작성하고 최종화해 주세요."]
            });
        }
        if (attempt.ReceiptVersion != request.ExpectedReceiptVersion)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }

        Guid? pendingId = null;
        if (result == "Failed")
        {
            pendingId = await pendingStore.CreateOrReuseMaterialNonconformanceAsync(
                connection,
                transaction,
                attempt.ProjectId,
                attempt.ItemId,
                attempt.ReceiptId,
                $"IQC 부적합 · {attempt.OrderItem ?? "발주품목"}",
                $"도착분 IQC {attempt.AttemptNumber}차 검사에서 부적합 판정되었습니다. 사유: {reason}",
                actorUserId,
                correlationId,
                cancellationToken);
            if (attempt.LinkedPendingId is not null)
            {
                await pendingStore.ReopenQualityIssueAfterFailedReinspectionAsync(
                    connection, transaction, attempt.LinkedPendingId.Value, actorUserId,
                    $"IQC {attempt.AttemptNumber}차 재검사 부적합: {reason}", correlationId, cancellationToken);
            }
        }
        else
        {
            pendingId = await ReadLatestPendingIdAsync(connection, transaction, attempt.ReceiptId, cancellationToken);
            if (pendingId is not null)
            {
                try
                {
                    await pendingStore.CloseMaterialNonconformanceAsync(
                        connection,
                        transaction,
                        pendingId.Value,
                        actorUserId,
                        $"IQC {attempt.AttemptNumber}차 재검사 합격: {reason}",
                        correlationId,
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(exception.Message);
                }
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update material_iqc_attempts
                set status = @attempt_status,
                    reason = @reason,
                    pending_issue_id = @pending_id,
                    decided_by_user_id = @actor_id,
                    decided_at_utc = now()
                where id = @attempt_id and status = 'Requested';

                update material_receipts
                set status = @receipt_status,
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now()
                where id = @receipt_id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("attempt_status", result!);
            command.Parameters.AddWithValue("reason", reason!);
            AddNullableUuid(command, "pending_id", pendingId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("receipt_status", result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked);
            command.Parameters.AddWithValue("receipt_id", attempt.ReceiptId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedReceiptVersion!.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await CompleteWorkItemsByPrefixAsync(connection, transaction, $"materials:iqc:{attemptId}", cancellationToken);
        if (result == "Passed")
        {
            await CreateConfirmationWorkItemAsync(connection, transaction, attempt.ProjectId, attempt.ItemId, attempt.ReceiptId, attempt.OrderItem, actorUserId, cancellationToken);
        }
        await InsertEventAsync(
            connection,
            transaction,
            attempt.ItemId,
            attempt.ReceiptId,
            result == "Passed" ? "IqcPassed" : "IqcFailed",
            MaterialReceiptStatuses.IqcRequested,
            result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
            reason,
            actorUserId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(
                attempt.ItemId,
                attempt.ReceiptId,
                attemptId,
                pendingId,
                result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
                false));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> RequestReinspectionAsync(
        Guid receiptId,
        MaterialReceiptVersionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var receipt = await ReadReceiptForUpdateAsync(connection, transaction, receiptId, cancellationToken);
        if (receipt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (receipt.Status == MaterialReceiptStatuses.IqcRequested)
        {
            await using var replayCommand = connection.CreateCommand();
            replayCommand.Transaction = transaction;
            replayCommand.CommandText = """
                select id, pending_issue_id
                from material_iqc_attempts
                where material_receipt_id = @receipt_id and status = 'Requested'
                order by attempt_number desc limit 1;
                """;
            replayCommand.Parameters.AddWithValue("receipt_id", receiptId);
            await using var replayReader = await replayCommand.ExecuteReaderAsync(cancellationToken);
            if (await replayReader.ReadAsync(cancellationToken))
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
                    new MaterialReceiptActionResponse(
                        receipt.ItemId, receiptId, replayReader.GetGuid(0),
                        replayReader.IsDBNull(1) ? null : replayReader.GetGuid(1),
                        MaterialReceiptStatuses.IqcRequested, receipt.ReceiptCompleted));
            }
        }
        var versionError = ValidateExpectedVersion(request.ExpectedVersion, receipt.Version);
        if (versionError is not null)
        {
            return versionError;
        }
        if (receipt.Status != MaterialReceiptStatuses.FailedBlocked)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("부적합 차단 상태에서만 재검사를 요청할 수 있습니다.");
        }
        var pendingId = await ReadLatestPendingIdAsync(connection, transaction, receiptId, cancellationToken);
        if (pendingId is null
            || await PendingStore.ReadMaterialNonconformanceStatusAsync(connection, transaction, pendingId.Value, cancellationToken) != PendingStatuses.ReinspectionRequested)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("연결된 Pending을 재검사 요청 상태로 전환한 뒤 진행해 주세요.");
        }

        var attemptId = await EnsurePendingReinspectionAsync(connection, transaction, pendingId.Value, actorUserId, cancellationToken)
            ?? throw new InvalidOperationException("연결된 IQC Pending을 찾을 수 없습니다.");
        await InsertEventAsync(connection, transaction, receipt.ItemId, receiptId, "ReinspectionRequested", MaterialReceiptStatuses.FailedBlocked, MaterialReceiptStatuses.IqcRequested, "Pending 조치 후 재검사 요청", actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(receipt.ItemId, receiptId, attemptId, pendingId, MaterialReceiptStatuses.IqcRequested, false));
    }

    internal async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> FinalizeDetailedIqcAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        Guid reportId,
        FinalizeIqcReportRequest request,
        string snapshotText,
        string snapshotSha256,
        DateTimeOffset finalizedAtUtc,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var attempt = await ReadAttemptForUpdateAsync(connection, transaction, attemptId, cancellationToken);
        if (attempt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (attempt.DecisionMode != IqcDecisionModes.Detailed)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("기존 간편 판정 건에는 상세 성적서를 최종화할 수 없습니다.");
        }
        if (attempt.Status != "Requested" || attempt.ReceiptStatus != MaterialReceiptStatuses.IqcRequested)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("이미 판정되었거나 IQC 요청 상태가 아닙니다.");
        }
        if (attempt.ReceiptVersion != request.ExpectedReceiptVersion)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }

        var result = request.Result!;
        var reason = request.Reason!.Trim();
        Guid? pendingId;
        if (result == "Failed")
        {
            pendingId = await pendingStore.CreateOrReuseMaterialNonconformanceAsync(
                connection,
                transaction,
                attempt.ProjectId,
                attempt.ItemId,
                attempt.ReceiptId,
                $"IQC 부적합 · {attempt.OrderItem ?? "발주품목"}",
                $"도착분 IQC {attempt.AttemptNumber}차 검사에서 부적합 판정되었습니다. 사유: {reason}",
                actorUserId,
                correlationId,
                cancellationToken);
            if (attempt.LinkedPendingId is not null)
            {
                await pendingStore.ReopenQualityIssueAfterFailedReinspectionAsync(
                    connection, transaction, attempt.LinkedPendingId.Value, actorUserId,
                    $"IQC {attempt.AttemptNumber}차 재검사 부적합: {reason}", correlationId, cancellationToken);
            }
        }
        else
        {
            pendingId = await ReadLatestPendingIdAsync(connection, transaction, attempt.ReceiptId, cancellationToken);
            if (pendingId is not null)
            {
                try
                {
                    await pendingStore.CloseMaterialNonconformanceAsync(
                        connection,
                        transaction,
                        pendingId.Value,
                        actorUserId,
                        $"IQC {attempt.AttemptNumber}차 재검사 합격: {reason}",
                        correlationId,
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(exception.Message);
                }
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update iqc_reports
                set status = 'Finalized',
                    version = version + 1,
                    result = @result,
                    reason = @reason,
                    finalized_by_user_id = @actor_id,
                    finalized_at_utc = @finalized_at,
                    snapshot_text = @snapshot_text,
                    snapshot_sha256 = @snapshot_sha256,
                    pdf_status = 'Pending',
                    updated_by_user_id = @actor_id,
                    updated_at_utc = @finalized_at
                where id = @report_id
                  and attempt_id = @attempt_id
                  and status = 'Draft'
                  and version = @expected_report_version;

                update material_iqc_attempts
                set status = @attempt_status,
                    reason = @reason,
                    pending_issue_id = @pending_id,
                    decided_by_user_id = @actor_id,
                    decided_at_utc = @finalized_at
                where id = @attempt_id and status = 'Requested' and decision_mode = 'Detailed';

                update material_receipts
                set status = @receipt_status,
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = @finalized_at
                where id = @receipt_id and version = @expected_receipt_version;
                """;
            command.Parameters.AddWithValue("result", result);
            command.Parameters.AddWithValue("attempt_status", result);
            command.Parameters.AddWithValue("receipt_status", result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked);
            command.Parameters.AddWithValue("reason", reason);
            AddNullableUuid(command, "pending_id", pendingId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("finalized_at", finalizedAtUtc);
            command.Parameters.AddWithValue("snapshot_text", snapshotText);
            command.Parameters.AddWithValue("snapshot_sha256", snapshotSha256);
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("receipt_id", attempt.ReceiptId);
            command.Parameters.AddWithValue("expected_report_version", request.ExpectedReportVersion!.Value);
            command.Parameters.AddWithValue("expected_receipt_version", request.ExpectedReceiptVersion!.Value);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected != 3)
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        await CompleteWorkItemsByPrefixAsync(connection, transaction, $"materials:iqc:{attemptId}", cancellationToken);
        if (result == "Passed")
        {
            await CreateConfirmationWorkItemAsync(connection, transaction, attempt.ProjectId, attempt.ItemId, attempt.ReceiptId, attempt.OrderItem, actorUserId, cancellationToken);
        }
        await InsertEventAsync(
            connection,
            transaction,
            attempt.ItemId,
            attempt.ReceiptId,
            result == "Passed" ? "IqcPassed" : "IqcFailed",
            MaterialReceiptStatuses.IqcRequested,
            result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
            reason,
            actorUserId,
            cancellationToken);

        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(
                attempt.ItemId,
                attempt.ReceiptId,
                attemptId,
                pendingId,
                result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
                false));
    }

    internal async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> FinalizeScanIqcAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        Guid reportId,
        FinalizeIqcReportRequest request,
        string snapshotSha256,
        DateTimeOffset finalizedAtUtc,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var attempt = await ReadAttemptForUpdateAsync(connection, transaction, attemptId, cancellationToken);
        if (attempt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (attempt.DecisionMode != IqcDecisionModes.ScanBased)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("스캔형 외함 IQC만 이 방식으로 확정할 수 있습니다.");
        }
        if (attempt.Status != "Requested" || attempt.ReceiptStatus != MaterialReceiptStatuses.IqcRequested)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("이미 판정되었거나 IQC 요청 상태가 아닙니다.");
        }
        if (attempt.ReceiptVersion != request.ExpectedReceiptVersion)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }

        var result = request.Result!;
        var reason = request.Reason!.Trim();
        Guid? pendingId;
        if (result == "Failed")
        {
            pendingId = await pendingStore.CreateOrReuseMaterialNonconformanceAsync(
                connection,
                transaction,
                attempt.ProjectId,
                attempt.ItemId,
                attempt.ReceiptId,
                $"IQC 부적합 · {attempt.OrderItem ?? "발주품목"}",
                $"도착분 외함 IQC {attempt.AttemptNumber}차 검사에서 부적합 판정되었습니다. 사유: {reason}",
                actorUserId,
                correlationId,
                cancellationToken);
            if (attempt.LinkedPendingId is not null)
            {
                await pendingStore.ReopenQualityIssueAfterFailedReinspectionAsync(
                    connection,
                    transaction,
                    attempt.LinkedPendingId.Value,
                    actorUserId,
                    $"외함 IQC {attempt.AttemptNumber}차 재검사 부적합: {reason}",
                    correlationId,
                    cancellationToken);
            }
        }
        else
        {
            pendingId = await ReadLatestPendingIdAsync(connection, transaction, attempt.ReceiptId, cancellationToken);
            if (pendingId is not null)
            {
                try
                {
                    await pendingStore.CloseMaterialNonconformanceAsync(
                        connection,
                        transaction,
                        pendingId.Value,
                        actorUserId,
                        $"외함 IQC {attempt.AttemptNumber}차 재검사 합격: {reason}",
                        correlationId,
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(exception.Message);
                }
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update material_iqc_scan_reports
                set status='Finalized',
                    version=version + 1,
                    result=@result,
                    reason=@reason,
                    finalized_by_user_id=@actor_id,
                    finalized_at_utc=@finalized_at,
                    snapshot_sha256=@snapshot_sha256,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=@finalized_at
                where id=@report_id
                  and attempt_id=@attempt_id
                  and status='Draft'
                  and version=@expected_report_version;

                update material_iqc_attempts
                set status=@attempt_status,
                    reason=@reason,
                    pending_issue_id=@pending_id,
                    decided_by_user_id=@actor_id,
                    decided_at_utc=@finalized_at
                where id=@attempt_id
                  and status='Requested'
                  and decision_mode='ScanBased';

                update material_receipts
                set status=@receipt_status,
                    version=version + 1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=@finalized_at
                where id=@receipt_id
                  and version=@expected_receipt_version;
                """;
            command.Parameters.AddWithValue("result", result);
            command.Parameters.AddWithValue("attempt_status", result);
            command.Parameters.AddWithValue("receipt_status", result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked);
            command.Parameters.AddWithValue("reason", reason);
            AddNullableUuid(command, "pending_id", pendingId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("finalized_at", finalizedAtUtc);
            command.Parameters.AddWithValue("snapshot_sha256", snapshotSha256);
            command.Parameters.AddWithValue("report_id", reportId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("receipt_id", attempt.ReceiptId);
            command.Parameters.AddWithValue("expected_report_version", request.ExpectedReportVersion!.Value);
            command.Parameters.AddWithValue("expected_receipt_version", request.ExpectedReceiptVersion!.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 3)
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        await CompleteWorkItemsByPrefixAsync(connection, transaction, $"materials:iqc:{attemptId}", cancellationToken);
        if (result == "Passed")
        {
            await CreateConfirmationWorkItemAsync(
                connection,
                transaction,
                attempt.ProjectId,
                attempt.ItemId,
                attempt.ReceiptId,
                attempt.OrderItem,
                actorUserId,
                cancellationToken);
        }
        await InsertEventAsync(
            connection,
            transaction,
            attempt.ItemId,
            attempt.ReceiptId,
            result == "Passed" ? "IqcPassed" : "IqcFailed",
            MaterialReceiptStatuses.IqcRequested,
            result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
            reason,
            actorUserId,
            cancellationToken);

        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(
                attempt.ItemId,
                attempt.ReceiptId,
                attemptId,
                pendingId,
                result == "Passed" ? MaterialReceiptStatuses.Passed : MaterialReceiptStatuses.FailedBlocked,
                false));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> ConfirmAsync(
        Guid receiptId,
        MaterialReceiptVersionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var receipt = await ReadReceiptForUpdateAsync(connection, transaction, receiptId, cancellationToken);
        if (receipt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        var versionError = ValidateExpectedVersion(request.ExpectedVersion, receipt.Version);
        if (versionError is not null)
        {
            return versionError;
        }
        if (receipt.Status is not (MaterialReceiptStatuses.Passed or MaterialReceiptStatuses.InspectionNotRequired))
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("IQC 합격 또는 검사 불필요 상태에서만 입고를 확정할 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update material_receipts
                set status = 'Confirmed',
                    version = version + 1,
                    confirmed_by_user_id = @actor_id,
                    confirmed_at_utc = now(),
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now()
                where id = @id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("id", receiptId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedVersion!.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await CompleteWorkItemsByPrefixAsync(connection, transaction, $"materials:receipt:{receiptId}:confirm", cancellationToken);
        await InsertEventAsync(
            connection,
            transaction,
            receipt.ItemId,
            receiptId,
            "Confirmed",
            receipt.Status,
            MaterialReceiptStatuses.Confirmed,
            receipt.Status == MaterialReceiptStatuses.InspectionNotRequired
                ? "IQC 비대상 도착분 입고 확정"
                : "IQC 합격 도착분 입고 확정",
            actorUserId,
            cancellationToken);
        await EnsureManufacturingReadinessAssignmentsAsync(
            connection,
            transaction,
            receipt.ProjectId,
            actorUserId,
            cancellationToken);
        await TryAutoCloseArrivalsAsync(connection, transaction, receipt.ItemId, actorUserId, cancellationToken);
        var completed = await RefreshDerivedProjectionAsync(connection, transaction, receipt.ItemId, actorUserId, cancellationToken);
        if (completed)
        {
            await EnsureMaterialWorkflowStageEventsAsync(
                connection,
                transaction,
                receipt.ProjectId,
                receipt.ItemId,
                actorUserId,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(receipt.ItemId, receiptId, null, null, MaterialReceiptStatuses.Confirmed, completed));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> CancelAsync(
        Guid receiptId,
        CancelMaterialReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeOptional(request.Reason);
        if (reason is null || reason.Length is < 3 or > 500)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] = ["취소 사유를 3~500자로 입력해 주세요."]
            });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var receipt = await ReadReceiptForUpdateAsync(connection, transaction, receiptId, cancellationToken);
        if (receipt is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        var versionError = ValidateExpectedVersion(request.ExpectedVersion, receipt.Version);
        if (versionError is not null)
        {
            return versionError;
        }
        if (receipt.Status is not (MaterialReceiptStatuses.Arrived or MaterialReceiptStatuses.InspectionNotRequired))
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("검사가 시작되지 않은 도착 등록만 취소할 수 있습니다.");
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update material_receipts
                set status = 'Cancelled', version = version + 1,
                    cancelled_by_user_id = @actor_id, cancelled_at_utc = now(), cancellation_reason = @reason,
                    updated_by_user_id = @actor_id, updated_at_utc = now()
                where id = @id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("id", receiptId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedVersion!.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertEventAsync(connection, transaction, receipt.ItemId, receiptId, "Cancelled", receipt.Status, MaterialReceiptStatuses.Cancelled, reason, actorUserId, cancellationToken);
        var completed = await RefreshDerivedProjectionAsync(connection, transaction, receipt.ItemId, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(receipt.ItemId, receiptId, null, null, MaterialReceiptStatuses.Cancelled, completed));
    }

    public async Task<MaterialsMutationResult<MaterialReceiptActionResponse>> CloseArrivalsAsync(
        Guid itemId,
        CloseMaterialArrivalsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeOptional(request.Reason);
        if (reason is null || reason.Length is < 3 or > 500)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] = ["입고 마감 사유를 3~500자로 입력해 주세요."]
            });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var item = await ReadItemForUpdateAsync(connection, transaction, itemId, cancellationToken);
        if (item is null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.NotFound();
        }
        if (request.ExpectedRowVersion != item.RowVersion)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
        if (item.ArrivalsClosedAtUtc is not null)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("이미 입고 마감된 발주품목입니다.");
        }
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = """
                select
                    count(*) filter (where status <> 'Cancelled')::int,
                    count(*) filter (where status not in ('Confirmed', 'Cancelled'))::int,
                    coalesce(sum(quantity) filter (where status <> 'Cancelled'), 0)
                from material_receipts
                where procurement_item_id = @item_id;
                """;
            checkCommand.Parameters.AddWithValue("item_id", itemId);
            await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            if (reader.GetInt32(0) == 0)
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("유효한 도착분이 하나 이상 있어야 입고를 마감할 수 있습니다.");
            }
            if (reader.GetInt32(1) > 0)
            {
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("모든 도착분을 확정하거나 취소한 뒤 입고를 마감해 주세요.");
            }
            var arrivedQuantity = reader.GetDecimal(2);
            if (item.SupplyType == ProcurementSupplyTypes.CustomerSupplied
                && item.OrderQuantity is not null
                && arrivedQuantity != item.OrderQuantity.Value)
            {
                var remaining = item.OrderQuantity.Value - arrivedQuantity;
                return MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict(
                    $"사급 제공 예정량을 모두 도착 등록한 뒤 마감해 주세요. 미도착 잔량 {remaining:0.###} {item.OrderUnit}");
            }
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update project_procurement_items
                set material_arrivals_closed_at_utc = now(),
                    material_arrivals_closed_by_user_id = @actor_id,
                    row_version = row_version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now()
                where id = @id and row_version = @expected_version;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("id", itemId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedRowVersion!.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertEventAsync(connection, transaction, itemId, null, "ArrivalsClosed", null, null, reason, actorUserId, cancellationToken);
        var completed = await RefreshDerivedProjectionAsync(connection, transaction, itemId, actorUserId, cancellationToken);
        await EnsureMaterialWorkflowStageEventsAsync(
            connection,
            transaction,
            item.ProjectId,
            itemId,
            actorUserId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MaterialsMutationResult<MaterialReceiptActionResponse>.Success(
            new MaterialReceiptActionResponse(itemId, null, null, null, "ArrivalsClosed", completed));
    }

    private static async Task EnsureMaterialWorkflowStageEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid sourceItemId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        int activeItemCount;
        int closedItemCount;
        int itemWithoutReceiptCount;
        int iqcIncompleteItemCount;
        int receiptIncompleteItemCount;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    count(*)::int,
                    count(*) filter (where item.material_arrivals_closed_at_utc is not null)::int,
                    count(*) filter (where not exists (
                        select 1 from material_receipts receipt
                        where receipt.procurement_item_id = item.id and receipt.status <> 'Cancelled'
                    ))::int,
                    count(*) filter (where exists (
                        select 1 from material_receipts receipt
                        where receipt.procurement_item_id = item.id
                          and receipt.status not in ('Passed', 'Confirmed', 'Cancelled')
                    ))::int,
                    count(*) filter (where item.receipt_completed = false)::int
                from project_procurement_items item
                where item.project_id = @project_id and item.status = 'Active';
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            activeItemCount = reader.GetInt32(0);
            closedItemCount = reader.GetInt32(1);
            itemWithoutReceiptCount = reader.GetInt32(2);
            iqcIncompleteItemCount = reader.GetInt32(3);
            receiptIncompleteItemCount = reader.GetInt32(4);
        }

        if (activeItemCount == 0 || closedItemCount != activeItemCount || itemWithoutReceiptCount > 0)
        {
            return;
        }

        await EnsureMaterialStageCompletedEventAsync(
            connection, transaction, projectId, "MaterialArrived", sourceItemId,
            "모든 활성 구매품목 도착 등록 및 입고 마감", actorUserId, cancellationToken);
        if (iqcIncompleteItemCount > 0)
        {
            return;
        }

        await EnsureMaterialStageCompletedEventAsync(
            connection, transaction, projectId, "IQC", sourceItemId,
            "모든 유효 도착분 IQC 판정 완료", actorUserId, cancellationToken);
        if (receiptIncompleteItemCount > 0)
        {
            return;
        }

        await EnsureMaterialStageCompletedEventAsync(
            connection, transaction, projectId, "ReceiptConfirmed", sourceItemId,
            "모든 활성 구매품목 입고 확정 완료", actorUserId, cancellationToken);
    }

    private static async Task EnsureMaterialStageCompletedEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string stageCode,
        Guid sourceItemId,
        string note,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_workflow_events (
                project_id, stage_code, event_type, event_status, source_type, source_id,
                created_by_user_id, note
            )
            select
                @project_id, @stage_code, 'StageCompleted', 'Succeeded', 'ProcurementItem', @source_id,
                @actor_id, @note
            where not exists (
                select 1
                from project_workflow_events
                where project_id = @project_id
                  and stage_code = @stage_code
                  and event_type = 'StageCompleted'
                  and event_status = 'Succeeded'
            );

            update work_items
            set status = 'Completed',
                started_at_utc = coalesce(started_at_utc, now()),
                completed_at_utc = coalesce(completed_at_utc, now())
            where project_id = @project_id
              and workflow_stage_code = @stage_code
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("stage_code", stageCode);
        command.Parameters.AddWithValue("source_id", sourceItemId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("note", note);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateArrival(RegisterMaterialArrivalRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Quantity is null or <= 0)
        {
            errors[nameof(request.Quantity)] = ["도착 수량은 0보다 커야 합니다."];
        }
        var unit = request.Unit?.Trim() ?? "";
        if (unit.Length is < 1 or > 20)
        {
            errors[nameof(request.Unit)] = ["단위를 1~20자로 입력해 주세요."];
        }
        if (request.ArrivalDate is null)
        {
            errors[nameof(request.ArrivalDate)] = ["도착일을 입력해 주세요."];
        }
        if (request.Note?.Trim().Length > 1000)
        {
            errors[nameof(request.Note)] = ["비고는 1,000자 이하여야 합니다."];
        }
        return errors;
    }

    private static MaterialsMutationResult<MaterialReceiptActionResponse>? ValidateExpectedVersion(int? expected, int actual)
    {
        if (expected is null or < 1)
        {
            return MaterialsMutationResult<MaterialReceiptActionResponse>.Validation(new Dictionary<string, string[]>
            {
                ["ExpectedVersion"] = ["최신 version이 필요합니다."]
            });
        }
        return expected == actual
            ? null
            : MaterialsMutationResult<MaterialReceiptActionResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
    }

    private static async Task LoadReceiptsAsync(NpgsqlConnection connection, IReadOnlyList<MutableMaterialItem> items, CancellationToken cancellationToken)
    {
        var byId = items.ToDictionary(item => item.ItemId);
        var receiptsById = new Dictionary<Guid, MutableMaterialReceipt>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select id, procurement_item_id, quantity, unit, arrival_date, note, status, is_legacy,
                       version, created_at_utc, confirmed_at_utc, cancellation_reason
                from material_receipts
                where procurement_item_id = any(@item_ids)
                order by arrival_date desc, created_at_utc desc;
                """;
            command.Parameters.AddWithValue("item_ids", items.Select(item => item.ItemId).ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var receipt = new MutableMaterialReceipt
                {
                    ReceiptId = reader.GetGuid(0),
                    Quantity = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ArrivalDate = reader.GetFieldValue<DateOnly>(4),
                    Note = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Status = reader.GetString(6),
                    IsLegacy = reader.GetBoolean(7),
                    Version = reader.GetInt32(8),
                    CreatedAtUtc = reader.GetFieldValue<DateTimeOffset>(9),
                    ConfirmedAtUtc = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                    CancellationReason = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
                byId[reader.GetGuid(1)].Receipts.Add(receipt);
                receiptsById[receipt.ReceiptId] = receipt;
            }
        }
        if (receiptsById.Count == 0)
        {
            return;
        }
        await using var attemptCommand = connection.CreateCommand();
        attemptCommand.CommandText = """
            select attempt.id, attempt.material_receipt_id, attempt.attempt_number, attempt.status,
                   attempt.decision_mode, attempt.reason, attempt.pending_issue_id,
                   attempt.requested_at_utc, attempt.decided_at_utc,
                   coalesce(report.id, scan_report.id),
                   coalesce(report.status, scan_report.status),
                   report.pdf_status
            from material_iqc_attempts attempt
            left join iqc_reports report on report.attempt_id = attempt.id
            left join material_iqc_scan_reports scan_report on scan_report.attempt_id = attempt.id
            where attempt.material_receipt_id = any(@receipt_ids)
            order by attempt.attempt_number;
            """;
        attemptCommand.Parameters.AddWithValue("receipt_ids", receiptsById.Keys.ToArray());
        await using var attemptReader = await attemptCommand.ExecuteReaderAsync(cancellationToken);
        while (await attemptReader.ReadAsync(cancellationToken))
        {
            receiptsById[attemptReader.GetGuid(1)].Attempts.Add(new MaterialIqcAttemptResponse(
                attemptReader.GetGuid(0),
                attemptReader.GetInt32(2),
                attemptReader.GetString(3),
                attemptReader.GetString(4),
                attemptReader.IsDBNull(5) ? null : attemptReader.GetString(5),
                attemptReader.IsDBNull(6) ? null : attemptReader.GetGuid(6),
                attemptReader.GetFieldValue<DateTimeOffset>(7),
                attemptReader.IsDBNull(8) ? null : attemptReader.GetFieldValue<DateTimeOffset>(8),
                attemptReader.IsDBNull(9) ? null : attemptReader.GetGuid(9),
                attemptReader.IsDBNull(10) ? null : attemptReader.GetString(10),
                attemptReader.IsDBNull(11) ? null : attemptReader.GetString(11)));
        }
    }

    private static async Task<ItemSnapshot?> ReadItemForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select item.id, item.project_id, item.order_quantity, item.order_unit, item.material_arrivals_closed_at_utc,
                   item.receipt_completed, item.row_version, item.order_item, item.supply_type,
                   project.iqc_routing_policy, item.material_category_name_snapshot,
                   item.material_category_requires_iqc_snapshot
            from project_procurement_items item
            join projects project on project.id=item.project_id and project.deleted_at_utc is null
            where item.id = @id and item.status = 'Active'
            for update of item;
            """;
        command.Parameters.AddWithValue("id", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ItemSnapshot(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetBoolean(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetBoolean(11))
            : null;
    }

    private static async Task<ReceiptSnapshot?> ReadReceiptForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid receiptId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select receipt.id, receipt.procurement_item_id, item.project_id, receipt.status, receipt.version,
                   item.receipt_completed, item.order_item
            from material_receipts receipt
            join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
            where receipt.id = @id
            for update of receipt, item;
            """;
        command.Parameters.AddWithValue("id", receiptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReceiptSnapshot(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6))
            : null;
    }

    private static async Task<AttemptSnapshot?> ReadAttemptForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid attemptId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select attempt.id, attempt.material_receipt_id, attempt.attempt_number, attempt.status,
                   receipt.procurement_item_id, receipt.status, receipt.version,
                   item.project_id, item.order_item, attempt.decision_mode, attempt.pending_issue_id
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
            where attempt.id = @id
            for update of attempt, receipt, item;
            """;
        command.Parameters.AddWithValue("id", attemptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AttemptSnapshot(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetGuid(4), reader.GetString(5), reader.GetInt32(6), reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetGuid(10))
            : null;
    }

    internal static async Task<Guid> CreateIqcAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ReceiptSnapshot receipt,
        Guid actorUserId,
        CancellationToken cancellationToken,
        Guid? linkedPendingId = null,
        string decisionMode = IqcDecisionModes.Detailed)
    {
        var attemptId = Guid.NewGuid();
        int attemptNumber;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select coalesce(max(attempt_number), 0) + 1
                from material_iqc_attempts
                where material_receipt_id = @receipt_id;
                """;
            command.Parameters.AddWithValue("receipt_id", receipt.ReceiptId);
            attemptNumber = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into material_iqc_attempts (
                    id, material_receipt_id, attempt_number, status, decision_mode, pending_issue_id, requested_by_user_id
                )
                values (@id, @receipt_id, @attempt_number, 'Requested', @decision_mode, @pending_id, @actor_id);

                update material_receipts
                set status = 'IqcRequested', version = version + 1,
                    updated_by_user_id = @actor_id, updated_at_utc = now()
                where id = @receipt_id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("id", attemptId);
            command.Parameters.AddWithValue("receipt_id", receipt.ReceiptId);
            command.Parameters.AddWithValue("attempt_number", attemptNumber);
            command.Parameters.AddWithValue("decision_mode", decisionMode);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            AddNullableUuid(command, "pending_id", linkedPendingId);
            command.Parameters.AddWithValue("expected_version", receipt.Version);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await CreateIqcWorkItemAsync(connection, transaction, receipt.ProjectId, receipt.ItemId, receipt.ReceiptId, attemptId, receipt.OrderItem, actorUserId, cancellationToken);
        await InsertEventAsync(connection, transaction, receipt.ItemId, receipt.ReceiptId, "IqcRequested", receipt.Status, MaterialReceiptStatuses.IqcRequested, $"IQC {attemptNumber}차 요청", actorUserId, cancellationToken);
        return attemptId;
    }

    internal static async Task<Guid?> EnsurePendingReinspectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                select attempt.id
                from material_iqc_attempts attempt
                where attempt.pending_issue_id = @pending_id
                  and attempt.status = 'Requested'
                order by attempt.attempt_number desc
                limit 1;
                """;
            existingCommand.Parameters.AddWithValue("pending_id", pendingId);
            if (await existingCommand.ExecuteScalarAsync(cancellationToken) is Guid existingAttemptId)
            {
                return existingAttemptId;
            }
        }

        ReceiptSnapshot? receipt = null;
        var decisionMode = IqcDecisionModes.Detailed;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select receipt.id, receipt.procurement_item_id, item.project_id, receipt.status, receipt.version,
                       item.receipt_completed, item.order_item, failed_attempt.decision_mode
                from material_iqc_attempts failed_attempt
                join material_receipts receipt on receipt.id = failed_attempt.material_receipt_id
                join project_procurement_items item on item.id = receipt.procurement_item_id and item.status = 'Active'
                where failed_attempt.pending_issue_id = @pending_id
                  and failed_attempt.status = 'Failed'
                order by failed_attempt.attempt_number desc
                limit 1
                for update of receipt, item;
                """;
            command.Parameters.AddWithValue("pending_id", pendingId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                receipt = new ReceiptSnapshot(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
                    reader.GetInt32(4), reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6));
                decisionMode = reader.GetString(7);
            }
        }
        if (receipt is null) return null;
        if (!string.Equals(receipt.Status, MaterialReceiptStatuses.FailedBlocked, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("IQC 부적합 차단 상태에서만 재검사 업무를 생성할 수 있습니다.");
        }
        return await CreateIqcAttemptAsync(
            connection,
            transaction,
            receipt,
            actorUserId,
            cancellationToken,
            pendingId,
            decisionMode);
    }

    private static async Task CreateIqcWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid itemId,
        Guid receiptId,
        Guid attemptId,
        string? orderItem,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var assignees = await ResolveQualityIqcAssigneesAsync(connection, transaction, projectId, cancellationToken);
        if (assignees.Count == 0)
        {
            return;
        }
        var presentation = await ReadIqcWorkPresentationAsync(connection, transaction, attemptId, cancellationToken);
        var pendingLabel = presentation.PendingIssueNumber is null
            ? null
            : $"P-{presentation.PendingIssueNumber.Value:0000}";
        var quantityLabel = presentation.Quantity is null
            ? "수량 미입력"
            : $"{presentation.Quantity.Value.ToString("0.###", CultureInfo.InvariantCulture)}{(string.IsNullOrWhiteSpace(presentation.Unit) ? "" : $" {presentation.Unit}")}";
        var title = pendingLabel is null
            ? $"IQC 판정 · {orderItem ?? "발주품목"}"
            : $"재검사 · {pendingLabel} · {orderItem ?? "발주품목"} · {quantityLabel} ({presentation.AttemptNumber}차)";
        var description = pendingLabel is null
            ? $"도착분 {receiptId}의 수입검사를 판정해 주세요."
            : $"{pendingLabel} 조치 완료 건의 {presentation.AttemptNumber}차 IQC를 판정해 주세요.";
        var linkUrl = $"/quality/iqc?request={attemptId}";
        var workItemIds = new List<Guid>();
        for (var index = 0; index < assignees.Count; index += 1)
        {
            var assignee = assignees[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    link_url, idempotency_key, created_by_user_id
                )
                values (
                    @project_id, 'Inspection', @attempt_id, 'IQC', @responsibility_type,
                    @assignee_id, null, @title, @description, 'Requested', @priority,
                    @link_url, @idempotency_key, @actor_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title,
                    description=excluded.description,
                    priority=excluded.priority,
                    link_url=excluded.link_url
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("attempt_id", attemptId);
            command.Parameters.AddWithValue("responsibility_type", assignee.ResponsibilityType);
            command.Parameters.AddWithValue("assignee_id", assignee.UserId);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("priority", pendingLabel is null ? "Normal" : "Blocking");
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue("idempotency_key", index == 0
                ? $"materials:iqc:{attemptId}"
                : $"materials:iqc:{attemptId}:{assignee.UserId:N}");
            command.Parameters.AddWithValue("actor_id", actorUserId);
            workItemIds.Add((Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty));
        }

        await WorkAssignmentNotificationWriter.UpsertAsync(
            connection,
            transaction,
            projectId,
            workItemIds[0],
            assignees[0].UserId,
            ["QualityIQCSecondary"],
            title,
            description,
            linkUrl,
            $"materials:iqc:{attemptId}:notification",
            cancellationToken,
            pendingLabel is null ? NotificationSourceKinds.WorkAssignment : NotificationSourceKinds.ReinspectionRequested);
    }

    private static async Task<IqcWorkPresentation> ReadIqcWorkPresentationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select attempt.attempt_number, pending.issue_number, receipt.quantity, receipt.unit
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            left join pending_issues pending on pending.id = attempt.pending_issue_id
            where attempt.id = @attempt_id;
            """;
        command.Parameters.AddWithValue("attempt_id", attemptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new IqcWorkPresentation(1, null, null, null);
        }
        return new IqcWorkPresentation(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private static async Task<IReadOnlyList<IqcHandoffAssignee>> ResolveQualityIqcAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var responsibilityTypes = new[] { "QualityIQC", "QualityIQCSecondary" };
        var assignees = new List<IqcHandoffAssignee>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select selected.assigned_user_id, selected.responsibility_type
                from (
                    select distinct on (assignee.assigned_user_id)
                           assignee.assigned_user_id, assignee.responsibility_type,
                           array_position(@responsibility_types, assignee.responsibility_type) as responsibility_order
                    from project_assignees assignee
                    join qms_users users on users.id=assignee.assigned_user_id and users.is_active=true
                    where assignee.project_id=@project_id
                      and assignee.responsibility_type=any(@responsibility_types)
                    order by assignee.assigned_user_id,
                             array_position(@responsibility_types, assignee.responsibility_type)
                ) selected
                order by selected.responsibility_order, selected.assigned_user_id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_types", responsibilityTypes);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignees.Add(new IqcHandoffAssignee(reader.GetGuid(0), reader.GetString(1)));
            }
        }

        if (assignees.Count > 0)
        {
            return assignees;
        }

        var fallback = await ResolveAssigneeAsync(connection, transaction, projectId, "QualityIQC", QmsPermissions.QualityInspect, cancellationToken);
        return fallback is null
            ? []
            : [new IqcHandoffAssignee(fallback.Value.UserId, "QualityIQC")];
    }

    private static async Task CreateConfirmationWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid itemId,
        Guid receiptId,
        string? orderItem,
        Guid actorUserId,
        CancellationToken cancellationToken,
        bool iqcNotRequired = false)
    {
        var assignees = await ResolveMaterialConfirmationAssigneesAsync(connection, transaction, projectId, cancellationToken);
        if (assignees.Count == 0)
        {
            return;
        }
        var presentation = await ReadConfirmationPresentationAsync(connection, transaction, receiptId, cancellationToken);
        var title = $"입고 확정 · {orderItem ?? "발주품목"}";
        var notificationMessage = iqcNotRequired
            ? $"{presentation.ProjectCode} · {presentation.OrderItem ?? orderItem ?? "발주품목"} · {presentation.Quantity:0.###} {presentation.Unit ?? ""} · {presentation.ArrivalDate:M/d} 도착분은 IQC 비대상입니다. 입고 확정을 진행해 주세요."
            : $"{presentation.ProjectCode} · {presentation.OrderItem ?? orderItem ?? "발주품목"} · {presentation.Quantity:0.###} {presentation.Unit ?? ""} · {presentation.ArrivalDate:M/d} 도착분이 IQC 합격했습니다. 입고 확정을 진행해 주세요.";
        var quantity = $"{presentation.Quantity:0.###} {presentation.Unit ?? ""}".Trim();
        var description = iqcNotRequired
            ? $"IQC 비대상 도착분의 입고 확정을 진행해 주세요. ({presentation.OrderItem ?? orderItem ?? "발주품목"} {quantity})"
            : $"IQC 합격 도착분의 입고 확정을 진행해 주세요. ({presentation.OrderItem ?? orderItem ?? "발주품목"} {quantity})";
        var linkUrl = $"/materials/receipts?receipt={receiptId}";
        var workItemIds = new List<Guid>();
        foreach (var assignee in assignees)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    link_url, idempotency_key, created_by_user_id
                )
                values (
                    @project_id, 'ProcurementItem', @item_id, 'ReceiptConfirmed', @responsibility_type,
                    @assignee_id, null, @title, @description, 'Requested', 'Normal',
                    @link_url, @idempotency_key, @actor_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title,
                    description=excluded.description,
                    link_url=excluded.link_url
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("item_id", itemId);
            command.Parameters.AddWithValue("responsibility_type", assignee.ResponsibilityType);
            command.Parameters.AddWithValue("assignee_id", assignee.UserId);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue("idempotency_key", $"materials:receipt:{receiptId}:confirm:{assignee.UserId:N}");
            command.Parameters.AddWithValue("actor_id", actorUserId);
            workItemIds.Add((Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty));
        }

        await CreateAssignmentNotificationAsync(
            connection, transaction, projectId, workItemIds[0], assignees[0].UserId,
            "MaterialsSecondary", title, notificationMessage, linkUrl,
            $"materials:receipt:{receiptId}:confirm:notification", cancellationToken);
    }

    private static async Task<IReadOnlyList<IqcHandoffAssignee>> ResolveMaterialConfirmationAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var responsibilityTypes = new[] { "MaterialsPrimary", "MaterialsSecondary" };
        var assignees = new List<IqcHandoffAssignee>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select selected.assigned_user_id, selected.responsibility_type
                from (
                    select distinct on (assignee.assigned_user_id)
                           assignee.assigned_user_id, assignee.responsibility_type,
                           array_position(@responsibility_types, assignee.responsibility_type) as responsibility_order
                    from project_assignees assignee
                    join qms_users users on users.id=assignee.assigned_user_id and users.is_active=true
                    where assignee.project_id=@project_id
                      and assignee.responsibility_type=any(@responsibility_types)
                    order by assignee.assigned_user_id,
                             array_position(@responsibility_types, assignee.responsibility_type)
                ) selected
                order by selected.responsibility_order, selected.assigned_user_id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_types", responsibilityTypes);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignees.Add(new IqcHandoffAssignee(reader.GetGuid(0), reader.GetString(1)));
            }
        }

        if (assignees.Count > 0)
        {
            return assignees;
        }

        var fallback = await ResolveAssigneeAsync(connection, transaction, projectId, "MaterialsPrimary", QmsPermissions.MaterialReceiptUpdate, cancellationToken);
        return fallback is null
            ? []
            : [new IqcHandoffAssignee(fallback.Value.UserId, "MaterialsPrimary")];
    }

    private static async Task EnsureManufacturingReadinessAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var productionAssignees = await ResolveProjectResponsibilityAssigneesAsync(
            connection,
            transaction,
            projectId,
            ["ProductionPlanningPrimary", "ProductionPlanningSecondary"],
            "ProductionPlanningPrimary",
            QmsPermissions.ProductionPlanUpdate,
            cancellationToken);
        await UpsertManufacturingReadinessWorkGroupAsync(
            connection,
            transaction,
            projectId,
            actorUserId,
            productionAssignees,
            "production-release",
            "제조 투입 검토·요청",
            "첫 입고 확정분이 생겼습니다. 투입 가능한 패널을 확인하고 제조 요청을 진행해 주세요.",
            $"/production-planning/releases?project={projectId}",
            ["ProductionPlanningSecondary"],
            cancellationToken);

        var materialAssignees = await ResolveProjectResponsibilityAssigneesAsync(
            connection,
            transaction,
            projectId,
            ["MaterialsPrimary", "MaterialsSecondary"],
            "MaterialsPrimary",
            QmsPermissions.MaterialReceiptUpdate,
            cancellationToken);
        await UpsertManufacturingReadinessWorkGroupAsync(
            connection,
            transaction,
            projectId,
            actorUserId,
            materialAssignees,
            "optional-kitting",
            "키팅 검토(선택)",
            "첫 입고 확정분이 생겼습니다. 제조 투입에 필요한 패널만 선택적으로 키팅해 주세요.",
            $"/materials/kitting?project={projectId}",
            ["MaterialsSecondary"],
            cancellationToken);
    }

    private static async Task UpsertManufacturingReadinessWorkGroupAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        IReadOnlyList<IqcHandoffAssignee> assignees,
        string groupKey,
        string title,
        string description,
        string linkUrl,
        IReadOnlyList<string> secondaryResponsibilityTypes,
        CancellationToken cancellationToken)
    {
        if (assignees.Count == 0)
        {
            return;
        }

        var workItemIds = new List<Guid>();
        foreach (var assignee in assignees)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, description, status, priority,
                    link_url, idempotency_key, created_by_user_id
                )
                values (
                    @project_id, 'Project', @project_id, 'KittingCompleted', @responsibility_type,
                    @assignee_id, null, @title, @description, 'Requested', 'Normal',
                    @link_url, @idempotency_key, @actor_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title,
                    description=excluded.description,
                    link_url=excluded.link_url
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_type", assignee.ResponsibilityType);
            command.Parameters.AddWithValue("assignee_id", assignee.UserId);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue(
                "idempotency_key",
                $"materials:project:{projectId:N}:{groupKey}:{assignee.UserId:N}");
            command.Parameters.AddWithValue("actor_id", actorUserId);
            workItemIds.Add((Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty));
        }

        await WorkAssignmentNotificationWriter.UpsertAsync(
            connection,
            transaction,
            projectId,
            workItemIds[0],
            assignees[0].UserId,
            secondaryResponsibilityTypes,
            title,
            description,
            linkUrl,
            $"materials:project:{projectId:N}:{groupKey}:notification",
            cancellationToken);
    }

    private static async Task<IReadOnlyList<IqcHandoffAssignee>> ResolveProjectResponsibilityAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string[] responsibilityTypes,
        string fallbackResponsibilityType,
        string fallbackPermission,
        CancellationToken cancellationToken)
    {
        var assignees = new List<IqcHandoffAssignee>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select selected.assigned_user_id, selected.responsibility_type
                from (
                    select distinct on (assignee.assigned_user_id)
                           assignee.assigned_user_id,
                           assignee.responsibility_type,
                           array_position(@responsibility_types, assignee.responsibility_type) as responsibility_order
                    from project_assignees assignee
                    join qms_users users on users.id=assignee.assigned_user_id and users.is_active=true
                    where assignee.project_id=@project_id
                      and assignee.responsibility_type=any(@responsibility_types)
                    order by assignee.assigned_user_id,
                             array_position(@responsibility_types, assignee.responsibility_type)
                ) selected
                order by selected.responsibility_order, selected.assigned_user_id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_types", responsibilityTypes);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignees.Add(new IqcHandoffAssignee(reader.GetGuid(0), reader.GetString(1)));
            }
        }

        if (assignees.Count > 0)
        {
            return assignees;
        }

        var fallback = await ResolveAssigneeAsync(
            connection,
            transaction,
            projectId,
            fallbackResponsibilityType,
            fallbackPermission,
            cancellationToken);
        return fallback is null
            ? []
            : [new IqcHandoffAssignee(fallback.Value.UserId, fallbackResponsibilityType)];
    }

    private static async Task<ConfirmationPresentation> ReadConfirmationPresentationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project.project_code, item.order_item, receipt.quantity, receipt.unit, receipt.arrival_date
            from material_receipts receipt
            join project_procurement_items item on item.id=receipt.procurement_item_id
            join projects project on project.id=item.project_id
            where receipt.id=@receipt_id;
            """;
        command.Parameters.AddWithValue("receipt_id", receiptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ConfirmationPresentation("-", null, 0, null, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        return new ConfirmationPresentation(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetFieldValue<DateOnly>(4));
    }

    private static async Task CreateAssignmentNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid workItemId,
        Guid primaryUserId,
        string secondaryResponsibilityType,
        string title,
        string message,
        string linkUrl,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Guid notificationId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind, work_item_id
                ) values (
                    @project_id, 'Info', 'Info', @title, @message, @link_url,
                    @idempotency_key, 'RecipientOnly', 'WorkAssignment', @work_item_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title, message=excluded.message, link_url=excluded.link_url, work_item_id=excluded.work_item_id
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("title", $"새 업무 · {title}");
            command.Parameters.AddWithValue("message", message);
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            command.Parameters.AddWithValue("work_item_id", workItemId);
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }

        var recipientIds = new List<Guid> { primaryUserId };
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select assigned_user_id
                from project_assignees
                where project_id=@project_id and responsibility_type=@responsibility_type;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibility_type", secondaryResponsibilityType);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is Guid secondaryUserId) recipientIds.Add(secondaryUserId);
        }

        foreach (var recipientId in recipientIds.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            command.Parameters.AddWithValue("notification_id", notificationId);
            command.Parameters.AddWithValue("user_id", recipientId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<(Guid UserId, string? RoleCode)?> ResolveAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string responsibilityType,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select candidate.user_id, candidate.role_code
            from (
                select pa.assigned_user_id as user_id, role.code as role_code, 0 as priority
                from project_assignees pa
                join qms_users users on users.id = pa.assigned_user_id and users.is_active = true
                left join user_roles user_role on user_role.user_id = users.id
                left join roles role on role.id = user_role.role_id
                where pa.project_id = @project_id and pa.responsibility_type = @responsibility_type
                union all
                select users.id, role.code, 1
                from qms_users users
                join user_roles user_role on user_role.user_id = users.id
                join roles role on role.id = user_role.role_id
                join role_permissions role_permission on role_permission.role_id = role.id
                join permissions permission on permission.id = role_permission.permission_id
                where users.is_active = true and permission.code = @permission_code
            ) candidate
            order by candidate.priority, candidate.role_code nulls last, candidate.user_id
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_type", responsibilityType);
        command.Parameters.AddWithValue("permission_code", permissionCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1))
            : null;
    }

    private static async Task CompleteWorkItemsByPrefixAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items
            set status='Completed', completed_at_utc=coalesce(completed_at_utc, now())
            where (idempotency_key=@key or idempotency_key like @prefix)
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("prefix", $"{idempotencyKey}:%");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid?> ReadLatestPendingIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid receiptId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pending_issue_id
            from material_iqc_attempts
            where material_receipt_id = @receipt_id and pending_issue_id is not null
            order by attempt_number desc
            limit 1;
            """;
        command.Parameters.AddWithValue("receipt_id", receiptId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid pendingId ? pendingId : null;
    }

    private static async Task TryAutoCloseArrivalsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid itemId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        decimal? orderQuantity;
        bool alreadyClosed;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select order_quantity, material_arrivals_closed_at_utc is not null
                from project_procurement_items
                where id=@item_id
                for update;
                """;
            command.Parameters.AddWithValue("item_id", itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return;
            }
            orderQuantity = reader.IsDBNull(0) ? null : reader.GetDecimal(0);
            alreadyClosed = reader.GetBoolean(1);
        }

        if (alreadyClosed || orderQuantity is null)
        {
            return;
        }

        decimal confirmedQuantity;
        int processingCount;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    coalesce(sum(quantity) filter (where status='Confirmed'), 0),
                    count(*) filter (where status not in ('Confirmed', 'Cancelled'))::int
                from material_receipts
                where procurement_item_id=@item_id;
                """;
            command.Parameters.AddWithValue("item_id", itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            confirmedQuantity = reader.GetDecimal(0);
            processingCount = reader.GetInt32(1);
        }

        if (confirmedQuantity < orderQuantity.Value || processingCount > 0)
        {
            return;
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update project_procurement_items
                set material_arrivals_closed_at_utc=now(),
                    material_arrivals_closed_by_user_id=@actor_id,
                    row_version=row_version + 1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=now()
                where id=@item_id and material_arrivals_closed_at_utc is null;
                """;
            command.Parameters.AddWithValue("item_id", itemId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertEventAsync(connection, transaction, itemId, null, "ArrivalsClosed", null, null, "발주·제공 예정 수량 전량 입고 자동 마감", actorUserId, cancellationToken);
    }

    private static async Task<bool> RefreshDerivedProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid itemId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        bool previous;
        bool completed;
        string? previousNote;
        string? nextNote;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    item.receipt_completed,
                    item.receipt_completion_note,
                    item.material_arrivals_closed_at_utc is not null
                    and exists (
                        select 1 from material_receipts receipt
                        where receipt.procurement_item_id = item.id and receipt.status <> 'Cancelled'
                    )
                    and not exists (
                        select 1 from material_receipts receipt
                        where receipt.procurement_item_id = item.id
                          and receipt.status not in ('Confirmed', 'Cancelled')
                    ) as derived_completed,
                    item.order_quantity,
                    item.order_unit,
                    coalesce((
                        select sum(receipt.quantity)
                        from material_receipts receipt
                        where receipt.procurement_item_id=item.id and receipt.status='Confirmed'
                    ), 0) as confirmed_quantity
                from project_procurement_items item
                where item.id = @id
                for update;
                """;
            command.Parameters.AddWithValue("id", itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            previous = reader.GetBoolean(0);
            previousNote = reader.IsDBNull(1) ? null : reader.GetString(1);
            completed = reader.GetBoolean(2);
            decimal? orderQuantity = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
            var orderUnit = reader.IsDBNull(4) ? null : reader.GetString(4);
            var confirmedQuantity = reader.GetDecimal(5);
            nextNote = completed
                ? "도착분 IQC 및 입고 확정 완료"
                : confirmedQuantity > 0 && orderQuantity is not null
                    ? $"부분 입고 {confirmedQuantity:0.###}/{orderQuantity:0.###} {orderUnit}".TrimEnd()
                    : null;
        }
        if (previous == completed && string.Equals(previousNote, nextNote, StringComparison.Ordinal))
        {
            return completed;
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select set_config('emi_qms.material_receipt_write', 'allowed', true);
                update project_procurement_items
                set receipt_completed = @completed,
                    receipt_completed_at_utc = case when @completed then now() else null end,
                    receipt_completed_by_user_id = case when @completed then @actor_id else null end,
                    receipt_completion_note = @completion_note,
                    row_version = row_version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now()
                where id = @id;
                """;
            command.Parameters.AddWithValue("completed", completed);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            AddNullableText(command, "completion_note", nextNote);
            command.Parameters.AddWithValue("id", itemId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertEventAsync(connection, transaction, itemId, null, "DerivedCompletionChanged", previous.ToString(), completed.ToString(), nextNote ?? "상태 흐름 기반 완료값 재계산", actorUserId, cancellationToken);
        return completed;
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid itemId,
        Guid? receiptId,
        string eventType,
        string? fromStatus,
        string? toStatus,
        string? reason,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into material_receipt_events (
                procurement_item_id, material_receipt_id, event_type, from_status, to_status,
                reason, changed_by_user_id
            )
            values (@item_id, @receipt_id, @event_type, @from_status, @to_status, @reason, @actor_id);
            """;
        command.Parameters.AddWithValue("item_id", itemId);
        AddNullableUuid(command, "receipt_id", receiptId);
        command.Parameters.AddWithValue("event_type", eventType);
        AddNullableText(command, "from_status", fromStatus);
        AddNullableText(command, "to_status", toStatus);
        AddNullableText(command, "reason", NormalizeOptional(reason));
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MeasurementLabel(string supplyType)
        => supplyType == ProcurementSupplyTypes.CustomerSupplied ? "제공 예정량" : "발주 수량";

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value)
        => command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = value ?? (object)DBNull.Value;

    private static void AddNullableDate(NpgsqlCommand command, string name, DateOnly? value)
        => command.Parameters.Add(name, NpgsqlDbType.Date).Value = value ?? (object)DBNull.Value;

    private sealed class MutableMaterialItem
    {
        public Guid ItemId { get; init; }
        public Guid ProjectId { get; init; }
        public string ProjectTitle { get; init; } = "";
        public string ProjectCode { get; init; } = "";
        public string? OrderItem { get; init; }
        public string? MaterialCategoryName { get; init; }
        public bool? MaterialCategoryRequiresIqc { get; init; }
        public string? SupplierName { get; init; }
        public string SupplyType { get; init; } = ProcurementSupplyTypes.Purchased;
        public DateOnly? ExpectedReceiptDate { get; init; }
        public decimal? OrderQuantity { get; init; }
        public string? OrderUnit { get; init; }
        public DateTimeOffset? ArrivalsClosedAtUtc { get; init; }
        public bool ReceiptCompleted { get; init; }
        public int RowVersion { get; init; }
        public List<MutableMaterialReceipt> Receipts { get; } = [];

        public MaterialReceivingItemResponse ToResponse(DateOnly today)
        {
            var arrivedQuantity = Receipts
                .Where(receipt => receipt.Status != MaterialReceiptStatuses.Cancelled)
                .Sum(receipt => receipt.Quantity ?? 0);
            var confirmedQuantity = Receipts
                .Where(receipt => receipt.Status == MaterialReceiptStatuses.Confirmed)
                .Sum(receipt => receipt.Quantity ?? 0);
            decimal? remainingQuantity = OrderQuantity is null ? null : Math.Max(0, OrderQuantity.Value - confirmedQuantity);
            decimal? arrivalRemainingQuantity = OrderQuantity is null ? null : Math.Max(0, OrderQuantity.Value - arrivedQuantity);
            var processingQuantity = Math.Max(0, arrivedQuantity - confirmedQuantity);
            return new MaterialReceivingItemResponse
            {
                ItemId = ItemId,
                ProjectId = ProjectId,
                ProjectTitle = ProjectTitle,
                ProjectCode = ProjectCode,
                OrderItem = OrderItem,
                MaterialCategoryName = MaterialCategoryName,
                MaterialCategoryRequiresIqc = MaterialCategoryRequiresIqc,
                SupplierName = SupplierName,
                SupplyType = SupplyType,
                ExpectedReceiptDate = ExpectedReceiptDate,
                OrderQuantity = OrderQuantity,
                OrderUnit = OrderUnit,
                ArrivalsClosed = ArrivalsClosedAtUtc is not null,
                ArrivalsClosedAtUtc = ArrivalsClosedAtUtc,
                ReceiptCompleted = ReceiptCompleted,
                ArrivedQuantity = OrderQuantity is null ? null : arrivedQuantity,
                ConfirmedQuantity = OrderQuantity is null ? null : confirmedQuantity,
                RemainingQuantity = remainingQuantity,
                ProcessingQuantity = OrderQuantity is null ? null : processingQuantity,
                CustomerSupplyOverdue = SupplyType == ProcurementSupplyTypes.CustomerSupplied
                    && ExpectedReceiptDate is not null
                    && ExpectedReceiptDate.Value < today
                    && arrivalRemainingQuantity > 0,
                RowVersion = RowVersion,
                Receipts = Receipts.Select(receipt => receipt.ToResponse()).ToList()
            };
        }
    }

    private sealed class MutableMaterialReceipt
    {
        public Guid ReceiptId { get; init; }
        public decimal? Quantity { get; init; }
        public string? Unit { get; init; }
        public DateOnly ArrivalDate { get; init; }
        public string? Note { get; init; }
        public string Status { get; init; } = "";
        public bool IsLegacy { get; init; }
        public int Version { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset? ConfirmedAtUtc { get; init; }
        public string? CancellationReason { get; init; }
        public List<MaterialIqcAttemptResponse> Attempts { get; } = [];

        public MaterialReceiptResponse ToResponse() => new()
        {
            ReceiptId = ReceiptId,
            Quantity = Quantity,
            Unit = Unit,
            ArrivalDate = ArrivalDate,
            Note = Note,
            Status = Status,
            IsLegacy = IsLegacy,
            Version = Version,
            CreatedAtUtc = CreatedAtUtc,
            ConfirmedAtUtc = ConfirmedAtUtc,
            CancellationReason = CancellationReason,
            IqcAttempts = Attempts
        };
    }

    private sealed record ItemSnapshot(
        Guid ItemId,
        Guid ProjectId,
        decimal? OrderQuantity,
        string? OrderUnit,
        DateTimeOffset? ArrivalsClosedAtUtc,
        bool ReceiptCompleted,
        int RowVersion,
        string? OrderItem,
        string SupplyType,
        string IqcRoutingPolicy,
        string? MaterialCategoryName,
        bool? MaterialCategoryRequiresIqc);

    internal sealed record ReceiptSnapshot(
        Guid ReceiptId,
        Guid ItemId,
        Guid ProjectId,
        string Status,
        int Version,
        bool ReceiptCompleted,
        string? OrderItem);

    private sealed record AttemptSnapshot(
        Guid AttemptId,
        Guid ReceiptId,
        int AttemptNumber,
        string Status,
        Guid ItemId,
        string ReceiptStatus,
        int ReceiptVersion,
        Guid ProjectId,
        string? OrderItem,
        string DecisionMode,
        Guid? LinkedPendingId);

    private sealed record IqcHandoffAssignee(Guid UserId, string ResponsibilityType);

    private sealed record IqcWorkPresentation(
        int AttemptNumber,
        long? PendingIssueNumber,
        decimal? Quantity,
        string? Unit);

    private sealed record ConfirmationPresentation(
        string ProjectCode,
        string? OrderItem,
        decimal Quantity,
        string? Unit,
        DateOnly ArrivalDate);
}
