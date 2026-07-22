using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.DataExports;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Sales;

public sealed class SalesBillingRequestStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider,
    ExcelWorkbookBuilder workbookBuilder,
    ExcelExportConcurrencyGate concurrencyGate)
{
    private static readonly TimeZoneInfo SeoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public SalesBillingPeriodResponse RecommendedPeriod()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
        if (today.Day >= 16)
        {
            return new SalesBillingPeriodResponse(new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, 15), true);
        }

        var previous = today.AddMonths(-1);
        return new SalesBillingPeriodResponse(new DateOnly(previous.Year, previous.Month, 16), new DateOnly(previous.Year, previous.Month, DateTime.DaysInMonth(previous.Year, previous.Month)), true);
    }

    public async Task<SalesSettlementMutationResult<SalesBillingCandidateListResponse>> ListCandidatesAsync(
        DateOnly? periodStart,
        DateOnly? periodEnd,
        ProjectAccessScope scope,
        bool canReadSalesAmount,
        CancellationToken cancellationToken)
    {
        var recommended = RecommendedPeriod();
        var start = periodStart ?? recommended.PeriodStart;
        var end = periodEnd ?? recommended.PeriodEnd;
        var periodError = ValidatePeriod(start, end);
        if (periodError is not null)
        {
            return SalesSettlementMutationResult<SalesBillingCandidateListResponse>.Validation(periodError.Value.Field, periodError.Value.Message);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var items = await ReadCandidatesAsync(connection, null, start, end, scope, canReadSalesAmount, [], false, cancellationToken);
        return SalesSettlementMutationResult<SalesBillingCandidateListResponse>.Success(new SalesBillingCandidateListResponse(
            new SalesBillingPeriodResponse(start, end, start == recommended.PeriodStart && end == recommended.PeriodEnd),
            items.Count,
            items.Count(item => item.CanSelect),
            items.Count(item => item.Requested),
            items));
    }

    public async Task<SalesSettlementMutationResult<SalesBillingBatchResponse>> CreateAsync(
        CreateSalesBillingRequest request,
        Guid actorUserId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty)
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Validation("operationId", "작업 식별자를 입력해 주세요.");
        }

        var projectIds = (request.ProjectIds ?? []).Distinct().Order().ToArray();
        if (projectIds.Length is < 1 or > 500)
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Validation("projectIds", "1~500개 프로젝트를 선택해 주세요.");
        }

        if (request.PeriodStart is null || request.PeriodEnd is null)
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Validation("periodStart", "요청 기간을 입력해 주세요.");
        }

        var periodError = ValidatePeriod(request.PeriodStart.Value, request.PeriodEnd.Value);
        if (periodError is not null)
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Validation(periodError.Value.Field, periodError.Value.Message);
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Validation("note", "회계팀 전달 메모는 500자 이하로 입력해 주세요.");
        }

        if (!concurrencyGate.TryAcquire(out var lease))
        {
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("다른 Excel을 생성 중입니다. 잠시 후 다시 시도해 주세요.");
        }

        using (lease)
        {
            var fingerprint = Fingerprint(request.PeriodStart.Value, request.PeriodEnd.Value, projectIds, note);
            await using var dataSource = CreateDataSource();
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await LockOperationAsync(connection, transaction, request.OperationId, cancellationToken);
                var replay = await ReadReplayAsync(connection, transaction, request.OperationId, actorUserId, fingerprint, cancellationToken);
                if (replay is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return replay;
                }

                await LockProjectsAsync(connection, transaction, projectIds, scope, cancellationToken);
                var candidates = await ReadCandidatesAsync(
                    connection,
                    transaction,
                    request.PeriodStart.Value,
                    request.PeriodEnd.Value,
                    scope,
                    true,
                    projectIds,
                    true,
                    cancellationToken);
                if (candidates.Count != projectIds.Length)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("선택한 프로젝트 중 출하 완료·조회 범위를 다시 확인해야 하는 항목이 있습니다.");
                }

                var blocked = candidates.FirstOrDefault(candidate => !candidate.CanSelect);
                if (blocked is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict($"{blocked.ProjectCode}: {blocked.BlockedReason ?? "발행요청 대상이 아닙니다."}");
                }

                var batchId = Guid.NewGuid();
                var fileName = $"billing-request-{request.PeriodStart:yyyyMMdd}-{request.PeriodEnd:yyyyMMdd}-{batchId:N}.xlsx";
                var rows = candidates.Select((candidate, index) => new WorkbookRow(index + 1, candidate)).ToList();
                var bytes = workbookBuilder.Build(
                    "회계팀 세금계산서 발행요청",
                    "발행요청",
                    $"요청기간 {request.PeriodStart:yyyy-MM-dd}~{request.PeriodEnd:yyyy-MM-dd} / 회계팀 기입란: 발행일, 세금계산서 번호 / 메모: {note ?? "-"}",
                    rows,
                    WorkbookColumns());
                if (bytes.Length > 10 * 1024 * 1024)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("생성된 Excel이 10MB를 초과했습니다. 선택 프로젝트를 줄여 주세요.");
                }

                var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                long requestNumber;
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
                        insert into sales_billing_request_batches (
                            id, period_start, period_end, note, project_count, workbook_file_name,
                            workbook_size, workbook_sha256, workbook_content, created_by_user_id
                        ) values (
                            @id, @period_start, @period_end, @note, @project_count, @file_name,
                            @file_size, @sha256, @content, @actor_id
                        ) returning request_number;
                        """;
                    command.Parameters.AddWithValue("id", batchId);
                    command.Parameters.AddWithValue("period_start", request.PeriodStart.Value);
                    command.Parameters.AddWithValue("period_end", request.PeriodEnd.Value);
                    AddNullableText(command, "note", note);
                    command.Parameters.AddWithValue("project_count", candidates.Count);
                    command.Parameters.AddWithValue("file_name", fileName);
                    command.Parameters.AddWithValue("file_size", bytes.Length);
                    command.Parameters.AddWithValue("sha256", sha256);
                    command.Parameters.AddWithValue("content", bytes);
                    command.Parameters.AddWithValue("actor_id", actorUserId);
                    requestNumber = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
                }

                for (var index = 0; index < candidates.Count; index++)
                {
                    await InsertItemAsync(connection, transaction, batchId, index + 1, candidates[index], cancellationToken);
                }

                var actorName = await ReadUserNameAsync(connection, transaction, actorUserId, cancellationToken);
                var response = new SalesBillingBatchResponse(
                    batchId, requestNumber, request.PeriodStart.Value, request.PeriodEnd.Value,
                    candidates.Count, fileName, sha256, note, actorName, timeProvider.GetUtcNow(), false);
                await InsertOperationAsync(connection, transaction, request.OperationId, batchId, actorUserId, fingerprint, response, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return SalesSettlementMutationResult<SalesBillingBatchResponse>.Success(response);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("이미 발행요청에 포함된 프로젝트가 있습니다. 목록을 새로고침해 주세요.");
            }
        }
    }

    public async Task<SalesSettlementMutationResult<SalesBillingBatchListResponse>> ListBatchesAsync(
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select batch.id, batch.request_number, batch.period_start, batch.period_end,
                   batch.project_count, batch.workbook_file_name, batch.workbook_sha256,
                   batch.note, users.display_name, batch.created_at_utc
            from sales_billing_request_batches batch
            join qms_users users on users.id=batch.created_by_user_id
            where not exists (
                select 1 from sales_billing_request_items item
                join projects project on project.id=item.project_id
                where item.batch_id=batch.id
                  and not (@has_read_all or project.project_key=any(@project_keys))
            )
            order by batch.created_at_utc desc
            limit 50;
            """);
        AddScope(command, scope);
        var items = new List<SalesBillingBatchResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SalesBillingBatchResponse(
                reader.GetGuid(0), reader.GetInt64(1), reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateOnly>(3),
                reader.GetInt32(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), false));
        }
        return SalesSettlementMutationResult<SalesBillingBatchListResponse>.Success(new SalesBillingBatchListResponse(items));
    }

    public async Task<SalesSettlementMutationResult<SalesBillingFileResponse>> DownloadAsync(
        Guid batchId,
        Guid actorUserId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select batch.workbook_file_name, batch.workbook_content_type, batch.workbook_content, batch.workbook_sha256
            from sales_billing_request_batches batch
            where batch.id=@batch_id
              and not exists (
                  select 1 from sales_billing_request_items item
                  join projects project on project.id=item.project_id
                  where item.batch_id=batch.id
                    and not (@has_read_all or project.project_key=any(@project_keys))
              );
            """;
        command.Parameters.AddWithValue("batch_id", batchId);
        AddScope(command, scope);
        string fileName;
        string contentType;
        byte[] content;
        string sha256;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalesSettlementMutationResult<SalesBillingFileResponse>.NotFound();
            }
            fileName = reader.GetString(0);
            contentType = reader.GetString(1);
            content = reader.GetFieldValue<byte[]>(2);
            sha256 = reader.GetString(3);
        }

        var actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(actual, sha256, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return SalesSettlementMutationResult<SalesBillingFileResponse>.Conflict("저장된 Excel 무결성을 확인할 수 없습니다.");
        }

        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "insert into sales_billing_request_download_events (id,batch_id,downloaded_by_user_id) values (@id,@batch_id,@actor_id);";
            audit.Parameters.AddWithValue("id", Guid.NewGuid());
            audit.Parameters.AddWithValue("batch_id", batchId);
            audit.Parameters.AddWithValue("actor_id", actorUserId);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return SalesSettlementMutationResult<SalesBillingFileResponse>.Success(new SalesBillingFileResponse(batchId, fileName, contentType, content, sha256));
    }

    public async Task<SalesBillingProjectStatusResponse> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select batch.id, batch.request_number, batch.created_at_utc,
                   coalesce(settlement.status='Completed', false)
            from sales_billing_request_items item
            join sales_billing_request_batches batch on batch.id=item.batch_id
            left join sales_settlements settlement on settlement.project_id=item.project_id
            where item.project_id=@project_id;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new SalesBillingProjectStatusResponse(true, reader.GetGuid(0), reader.GetInt64(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetBoolean(3))
            : new SalesBillingProjectStatusResponse(false, null, null, null, false);
    }

    private async Task<IReadOnlyList<SalesBillingCandidateResponse>> ReadCandidatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        DateOnly periodStart,
        DateOnly periodEnd,
        ProjectAccessScope scope,
        bool canReadSalesAmount,
        IReadOnlyList<Guid> projectIds,
        bool filterProjects,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with active_panels as (
                select panel.id, panel.project_id
                from panel_placeholders panel
                where panel.status='Active'
            ), departed_panels as (
                select distinct panel.id as panel_id, panel.project_id, batch.departure_date
                from active_panels panel
                join logistics_packing_unit_panels membership on membership.panel_id=panel.id and membership.active
                join logistics_packing_units unit on unit.id=membership.packing_unit_id and unit.status='Finalized'
                join logistics_batch_units batch_unit on batch_unit.packing_unit_id=unit.id
                  and batch_unit.stage_code='DepartureProcessed' and batch_unit.active
                join logistics_batches batch on batch.id=batch_unit.batch_id
                  and batch.project_id=panel.project_id and batch.stage_code='DepartureProcessed'
                  and batch.status='Finalized' and batch.departure_date is not null
            ), project_departure as (
                select panel.project_id,
                       count(*)::int active_panel_count,
                       count(departed.panel_id)::int departed_panel_count,
                       min(departed.departure_date) first_departure_date,
                       max(departed.departure_date) last_departure_date
                from active_panels panel
                left join departed_panels departed on departed.panel_id=panel.id
                group by panel.project_id
            )
            select project.id, project.project_code, project.project_title, project.customer_name,
                   project.item, project.delivery_location, departure.first_departure_date,
                   departure.last_departure_date, departure.active_panel_count, departure.departed_panel_count,
                   (select count(*)::int from pending_issues pending where pending.project_id=project.id and pending.status<>'Closed'),
                   project.sales_amount, project.currency_code, coalesce(owner.display_name, '-'),
                   request_item.batch_id, request_batch.request_number, request_batch.created_at_utc,
                   coalesce(settlement.status='Completed', false)
            from projects project
            join project_departure departure on departure.project_id=project.id
            left join qms_users owner on owner.id=project.sales_owner_user_id
            left join sales_billing_request_items request_item on request_item.project_id=project.id
            left join sales_billing_request_batches request_batch on request_batch.id=request_item.batch_id
            left join sales_settlements settlement on settlement.project_id=project.id
            where project.deleted_at_utc is null
              and project.structure_mode is distinct from 'Ul891Set'
              and departure.active_panel_count > 0
              and departure.departed_panel_count=departure.active_panel_count
              and departure.last_departure_date between @period_start and @period_end
              and (@has_read_all or project.project_key=any(@project_keys))
              and (not @filter_projects or project.id=any(@project_ids))
            order by departure.last_departure_date, project.project_code;
            """;
        command.Parameters.AddWithValue("period_start", periodStart);
        command.Parameters.AddWithValue("period_end", periodEnd);
        command.Parameters.AddWithValue("filter_projects", filterProjects);
        command.Parameters.AddWithValue("project_ids", projectIds.ToArray());
        AddScope(command, scope);
        var items = new List<SalesBillingCandidateResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var requested = !reader.IsDBNull(14);
            var openPending = reader.GetInt32(10);
            var hasAmount = !reader.IsDBNull(11);
            var blockedReason = requested ? "이미 회계팀 발행요청에 포함되었습니다."
                : openPending > 0 ? "열린 Pending을 먼저 종결해 주세요."
                : !hasAmount ? "판매금액을 입력한 뒤 요청해 주세요."
                : null;
            items.Add(new SalesBillingCandidateResponse(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetFieldValue<DateOnly>(6), reader.GetFieldValue<DateOnly>(7),
                reader.GetInt32(8), reader.GetInt32(9), openPending,
                canReadSalesAmount && hasAmount ? reader.GetDecimal(11) : null,
                reader.GetString(12), reader.GetString(13), requested,
                requested ? reader.GetGuid(14) : null, requested ? reader.GetInt64(15) : null,
                requested ? reader.GetFieldValue<DateTimeOffset>(16) : null,
                blockedReason is null, blockedReason));
        }
        return items;
    }

    private static async Task LockOperationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select pg_advisory_xact_lock(hashtextextended(@operation_id, 0));";
        command.Parameters.AddWithValue("operation_id", operationId.ToString("D"));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task LockProjectsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<Guid> projectIds, ProjectAccessScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project.id from projects project
            where project.id=any(@project_ids) and project.deleted_at_utc is null
              and (@has_read_all or project.project_key=any(@project_keys))
            order by project.id for update;
            """;
        command.Parameters.AddWithValue("project_ids", projectIds.ToArray());
        AddScope(command, scope);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) { }
    }

    private static async Task InsertItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid batchId, int rowNumber, SalesBillingCandidateResponse row, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into sales_billing_request_items (
                batch_id,project_id,row_number,project_code,project_title,customer_name,item_name,
                delivery_location,first_departure_date,last_departure_date,active_panel_count,
                departed_panel_count,sales_amount,currency_code,sales_owner_name
            ) values (
                @batch_id,@project_id,@row_number,@project_code,@project_title,@customer_name,@item_name,
                @delivery_location,@first_departure_date,@last_departure_date,@active_panel_count,
                @departed_panel_count,@sales_amount,@currency_code,@sales_owner_name
            );
            """;
        command.Parameters.AddWithValue("batch_id", batchId);
        command.Parameters.AddWithValue("project_id", row.ProjectId);
        command.Parameters.AddWithValue("row_number", rowNumber);
        command.Parameters.AddWithValue("project_code", row.ProjectCode);
        command.Parameters.AddWithValue("project_title", row.ProjectTitle);
        command.Parameters.AddWithValue("customer_name", row.CustomerName);
        command.Parameters.AddWithValue("item_name", row.Item);
        AddNullableText(command, "delivery_location", row.DeliveryLocation);
        command.Parameters.AddWithValue("first_departure_date", row.FirstDepartureDate);
        command.Parameters.AddWithValue("last_departure_date", row.LastDepartureDate);
        command.Parameters.AddWithValue("active_panel_count", row.ActivePanelCount);
        command.Parameters.AddWithValue("departed_panel_count", row.DepartedPanelCount);
        command.Parameters.AddWithValue("sales_amount", row.SalesAmount!.Value);
        command.Parameters.AddWithValue("currency_code", row.CurrencyCode);
        command.Parameters.AddWithValue("sales_owner_name", row.SalesOwnerName);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<SalesSettlementMutationResult<SalesBillingBatchResponse>?> ReadReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, Guid actorId, string fingerprint, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select actor_user_id,payload_fingerprint,result_projection from sales_billing_request_operations where operation_id=@id;";
        command.Parameters.AddWithValue("id", operationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        if (reader.GetGuid(0) != actorId || !string.Equals(reader.GetString(1), fingerprint, StringComparison.Ordinal))
            return SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("같은 operationId에 다른 요청 내용이 사용되었습니다.");
        var value = JsonSerializer.Deserialize<SalesBillingBatchResponse>(reader.GetString(2), JsonOptions);
        return value is null
            ? SalesSettlementMutationResult<SalesBillingBatchResponse>.Conflict("이전 발행요청 결과를 복구할 수 없습니다.")
            : SalesSettlementMutationResult<SalesBillingBatchResponse>.Success(value with { Replayed = true });
    }

    private static async Task InsertOperationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, Guid batchId, Guid actorId, string fingerprint, SalesBillingBatchResponse response, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into sales_billing_request_operations (operation_id,batch_id,actor_user_id,payload_fingerprint,result_projection) values (@id,@batch_id,@actor_id,@fingerprint,@projection::jsonb);";
        command.Parameters.AddWithValue("id", operationId);
        command.Parameters.AddWithValue("batch_id", batchId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("projection", JsonSerializer.Serialize(response, JsonOptions));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<string> ReadUserNameAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select display_name from qms_users where id=@id;";
        command.Parameters.AddWithValue("id", userId);
        return (string?)await command.ExecuteScalarAsync(token) ?? "-";
    }

    private static IReadOnlyList<ExcelColumn<WorkbookRow>> WorkbookColumns() =>
    [
        new("요청번호", row => row.RowNumber),
        new("프로젝트 코드", row => row.Candidate.ProjectCode),
        new("프로젝트명", row => row.Candidate.ProjectTitle),
        new("고객사", row => row.Candidate.CustomerName),
        new("Item", row => row.Candidate.Item),
        new("납품처", row => row.Candidate.DeliveryLocation ?? ""),
        new("최초 출발일", row => row.Candidate.FirstDepartureDate),
        new("최종 출발일", row => row.Candidate.LastDepartureDate),
        new("활성 패널 수", row => row.Candidate.ActivePanelCount),
        new("출발 완료 패널 수", row => row.Candidate.DepartedPanelCount),
        new("공급가액", row => row.Candidate.SalesAmount),
        new("통화", row => row.Candidate.CurrencyCode),
        new("영업담당", row => row.Candidate.SalesOwnerName),
        new("발행일 (회계팀 기입)", _ => ""),
        new("세금계산서 번호 (회계팀 기입)", _ => "")
    ];

    private (string Field, string Message)? ValidatePeriod(DateOnly start, DateOnly end)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
        if (start > end) return ("periodEnd", "종료일은 시작일보다 빠를 수 없습니다.");
        if (end > today) return ("periodEnd", "오늘 이후 날짜는 요청 기간에 포함할 수 없습니다.");
        if (end.DayNumber - start.DayNumber > 30) return ("periodEnd", "요청 기간은 최대 31일입니다.");
        return null;
    }

    private static string Fingerprint(DateOnly start, DateOnly end, IReadOnlyList<Guid> projectIds, string? note)
    {
        var source = $"{start:yyyy-MM-dd}|{end:yyyy-MM-dd}|{string.Join(',', projectIds)}|{note ?? ""}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private static void AddScope(NpgsqlCommand command, ProjectAccessScope scope)
    {
        command.Parameters.AddWithValue("has_read_all", scope.HasProjectReadAll);
        command.Parameters.Add(new NpgsqlParameter<string[]>("project_keys", scope.ProjectKeys.ToArray()));
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private sealed record WorkbookRow(int RowNumber, SalesBillingCandidateResponse Candidate);
}
