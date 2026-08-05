using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Ul891Sets;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Sales;

public sealed class SalesSettlementStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo SeoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    public static async Task CancelProjectDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update sales_settlements
            set status='Cancelled', version=version+1,
                updated_by_user_id=@actor_id, updated_at_utc=now(),
                cancelled_by_user_id=@actor_id, cancelled_at_utc=now()
            where project_id=@project_id and status='Draft';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SalesSettlementMutationResult<SalesSettlementDetailResponse>> GetAsync(
        Guid projectId,
        ProjectAccessScope scope,
        Guid? actorId,
        bool hasSettlePermission,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var detail = await ReadDetailAsync(connection, null, projectId, scope, actorId, hasSettlePermission, cancellationToken);
        return detail is null
            ? SalesSettlementMutationResult<SalesSettlementDetailResponse>.NotFound()
            : SalesSettlementMutationResult<SalesSettlementDetailResponse>.Success(detail);
    }

    public async Task<SalesSettlementMutationResult<SalesSettlementMutationResponse>> SaveDraftAsync(
        Guid projectId,
        SaveSalesSettlementDraftRequest request,
        Guid actorId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion is null)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var invoice = NormalizeInvoice(request.InvoiceIssuedDate, request.InvoiceNumber, request.Note);
        if (invoice.Error is not null)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation(invoice.Error.Value.Field, invoice.Error.Value.Message);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var project = await LockProjectAsync(connection, transaction, projectId, scope, cancellationToken);
            if (project is null) return await RollbackNotFound<SalesSettlementMutationResponse>(transaction, cancellationToken);
            if (project.Status != "Active") return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "진행 중인 프로젝트에서만 정산 정보를 저장할 수 있습니다.", cancellationToken);
            if (!await ActorCanMutateAsync(connection, transaction, projectId, actorId, cancellationToken))
                return await RollbackForbidden<SalesSettlementMutationResponse>(transaction, cancellationToken);
            var dateError = ValidateInvoiceDate(invoice.Date, project.CreatedDate);
            if (dateError is not null)
                return await RollbackValidation<SalesSettlementMutationResponse>(transaction, "invoiceIssuedDate", dateError, cancellationToken);

            var settlement = await LockSettlementAsync(connection, transaction, projectId, cancellationToken);
            if (settlement is null && request.ExpectedVersion != 0)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "정산 정보가 변경되었습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);
            if (settlement is not null && (settlement.Status != "Draft" || settlement.Version != request.ExpectedVersion.Value))
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "정산 정보가 변경되었습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);

            var settlementId = settlement?.Id ?? Guid.NewGuid();
            var version = settlement is null ? 1 : settlement.Version + 1;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = settlement is null
                    ? """
                        insert into sales_settlements (
                            id,project_id,status,invoice_issued_date,invoice_number,note,version,
                            created_by_user_id,updated_by_user_id)
                        values (@id,@project_id,'Draft',@invoice_date,@invoice_number,@note,1,@actor_id,@actor_id);
                        """
                    : """
                        update sales_settlements
                        set invoice_issued_date=@invoice_date,invoice_number=@invoice_number,note=@note,
                            version=version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                        where id=@id;
                        """;
                command.Parameters.AddWithValue("id", settlementId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("actor_id", actorId);
                AddNullableDate(command, "invoice_date", invoice.Date);
                AddNullableText(command, "invoice_number", invoice.Number);
                AddNullableText(command, "note", invoice.Note);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Success(
                new SalesSettlementMutationResponse(Guid.Empty, projectId, settlementId, "Draft", version, false));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Conflict("다른 사용자가 먼저 정산 정보를 만들었습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch
        {
            await RollbackQuietly(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<SalesSettlementMutationResult<SalesSettlementMutationResponse>> CompleteAsync(
        Guid projectId,
        CompleteSalesSettlementRequest request,
        Guid actorId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation("operationId", "작업 식별자를 입력해 주세요.");
        if (request.ExpectedVersion is null)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation("expectedVersion", "현재 버전을 입력해 주세요.");
        var invoice = NormalizeInvoice(request.InvoiceIssuedDate, request.InvoiceNumber, request.Note);
        if (invoice.Error is not null)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation(invoice.Error.Value.Field, invoice.Error.Value.Message);
        if (invoice.Date is null)
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Validation("invoiceIssuedDate", "회계팀 세금계산서 발행 확인일을 입력해 주세요.");

        var fingerprint = Fingerprint(projectId, request.ExpectedVersion.Value, invoice.Date.Value, invoice.Number, invoice.Note);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var project = await LockProjectAsync(connection, transaction, projectId, scope, cancellationToken);
            if (project is null) return await RollbackNotFound<SalesSettlementMutationResponse>(transaction, cancellationToken);
            var replay = await ReadReplayAsync(connection, transaction, request.OperationId, projectId, actorId, fingerprint, cancellationToken);
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
            if (!await ActorCanMutateAsync(connection, transaction, projectId, actorId, cancellationToken))
                return await RollbackForbidden<SalesSettlementMutationResponse>(transaction, cancellationToken);
            if (project.Status != "Active")
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "진행 중인 프로젝트만 최종 완료할 수 있습니다.", cancellationToken);
            var dateError = ValidateInvoiceDate(invoice.Date, project.CreatedDate);
            if (dateError is not null)
                return await RollbackValidation<SalesSettlementMutationResponse>(transaction, "invoiceIssuedDate", dateError, cancellationToken);

            var settlement = await LockSettlementAsync(connection, transaction, projectId, cancellationToken);
            if (settlement is null && request.ExpectedVersion != 0)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "정산 정보가 변경되었습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);
            if (settlement is not null && (settlement.Status != "Draft" || settlement.Version != request.ExpectedVersion.Value))
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "정산 정보가 변경되었습니다. 새로고침 후 다시 시도해 주세요.", cancellationToken);

            var conditions = await ReadConditionsAsync(connection, transaction, projectId, cancellationToken);
            if (conditions.ActivePanelCount == 0)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "활성 패널이 없는 프로젝트는 완료할 수 없습니다.", cancellationToken);
            if (conditions.DeliveredPanelCount != conditions.ActivePanelCount)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "모든 활성 패널의 납품을 완료한 뒤 다시 시도해 주세요.", cancellationToken);
            if (conditions.OpenPendingCount > 0)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "열린 Pending을 모두 종결한 뒤 다시 시도해 주세요.", cancellationToken);
            if (project.StructureMode == "Ul891Set")
            {
                if (project.SalesAmount is null or <= 0)
                    return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "프로젝트 판매액을 먼저 입력해 주세요.", cancellationToken);
                var monthlyGate = await MonthlyBillingStore.ReadCompletionGateAsync(connection, transaction, projectId, project.SalesAmount.Value, cancellationToken);
                if (!monthlyGate.AllLedgersConfirmed)
                    return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "모든 출하 월의 최신 발행요청을 회계 발행 확인해 주세요.", cancellationToken);
                if (!monthlyGate.AllRecoveriesConfirmed)
                    return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "발주 후 취소된 품목의 회수를 모두 확인해 주세요.", cancellationToken);
                if (!monthlyGate.RequestedAmountMatchesSalesAmount)
                    return await RollbackConflict<SalesSettlementMutationResponse>(transaction, $"월별 발행요청 합계 {monthlyGate.RequestedAmount:N0}이 프로젝트 판매액과 일치해야 합니다.", cancellationToken);
            }
            else if (!await HasBillingRequestAsync(connection, transaction, projectId, cancellationToken))
            {
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "회계팀 세금계산서 발행요청 자료를 먼저 만들어 주세요.", cancellationToken);
            }

            var workItemId = await LockOpenSettlementWorkAsync(connection, transaction, projectId, cancellationToken);
            if (workItemId is null)
                return await RollbackConflict<SalesSettlementMutationResponse>(transaction, "진행할 영업 정산 업무가 없습니다. 내 업무를 새로 확인해 주세요.", cancellationToken);

            var settlementId = settlement?.Id ?? Guid.NewGuid();
            var version = settlement is null ? 1 : settlement.Version + 1;
            await CompleteSettlementRowAsync(connection, transaction, settlementId, projectId, settlement is null, invoice, actorId, cancellationToken);
            await CompleteWorkItemAsync(connection, transaction, workItemId.Value, cancellationToken);
            var eventId = await EnsureCompletionEventAsync(connection, transaction, projectId, settlementId, request.OperationId, actorId, cancellationToken);
            await CompleteProjectAsync(connection, transaction, projectId, actorId, cancellationToken);
            await InsertAuditEventAsync(connection, transaction, projectId, actorId, request.OperationId, cancellationToken);
            await InsertCompletionNotificationAsync(connection, transaction, projectId, eventId, cancellationToken);

            var response = new SalesSettlementMutationResponse(request.OperationId, projectId, settlementId, "Completed", version, false);
            await InsertReceiptAsync(connection, transaction, request.OperationId, settlementId, projectId, actorId, fingerprint, response, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Success(response);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietly(transaction, cancellationToken);
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Conflict("다른 사용자가 먼저 프로젝트 정산을 변경했습니다. 새로고침 후 다시 시도해 주세요.");
        }
        catch
        {
            await RollbackQuietly(transaction, cancellationToken);
            throw;
        }
    }

    private string? ValidateInvoiceDate(DateOnly? value, DateOnly projectCreatedDate)
    {
        if (value is null) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), SeoulTimeZone).DateTime);
        if (value < projectCreatedDate) return "회계팀 발행 확인일은 프로젝트 생성일보다 빠를 수 없습니다.";
        if (value > today) return "회계팀 발행 확인일은 오늘보다 늦을 수 없습니다.";
        return null;
    }

    private static InvoiceProjection NormalizeInvoice(DateOnly? date, string? number, string? note)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNumber is { Length: > 64 }) return new(date, null, null, ("invoiceNumber", "세금계산서 번호는 64자 이하로 입력해 주세요."));
        if (normalizedNote is { Length: > 500 }) return new(date, null, null, ("note", "메모는 500자 이하로 입력해 주세요."));
        return new(date, normalizedNumber, normalizedNote, null);
    }

    private static async Task<ProjectSnapshot?> LockProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project.id,project.status,(project.created_at_utc at time zone 'Asia/Seoul')::date,
                   project.structure_mode,project.sales_amount
            from projects project
            where project.id=@project_id and project.deleted_at_utc is null
              and (@has_read_all or project.project_key=any(@project_keys))
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        AddScope(command, scope);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4))
            : null;
    }

    private static async Task<bool> ActorCanMutateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1 from project_assignees assignee
                where assignee.project_id=@project_id and assignee.assigned_user_id=@actor_id
                  and assignee.responsibility_type in ('SalesPrimary','SalesSecondary')
            ) or exists(
                select 1 from work_items work
                where work.project_id=@project_id and work.target_type='Project' and work.target_id=@project_id
                  and work.workflow_stage_code='SalesSettlementCompleted'
                  and work.status in ('Requested','InProgress') and work.assigned_user_id=@actor_id
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<SettlementSnapshot?> LockSettlementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id,status,version from sales_settlements where project_id=@project_id for update;";
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new SettlementSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2))
            : null;
    }

    private static async Task<ConditionSnapshot> ReadConditionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                count(*)::int,
                count(*) filter (where exists (
                    select 1
                    from logistics_delivery_results result
                    join logistics_batches batch on batch.id=result.batch_id
                      and batch.project_id=@project_id and batch.stage_code='DeliveryCompleted' and batch.status='Finalized'
                    join logistics_batch_panels batch_panel on batch_panel.batch_id=batch.id
                      and batch_panel.packing_unit_id=result.packing_unit_id
                      and batch_panel.panel_id=panel.id and batch_panel.active
                    where result.panel_id=panel.id
                ))::int,
                (select count(*)::int from pending_issues pending where pending.project_id=@project_id and pending.status<>'Closed')
            from panel_placeholders panel
            where panel.project_id=@project_id and panel.status='Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new ConditionSnapshot(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static async Task<bool> HasBillingRequestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from sales_billing_request_items where project_id=@project_id);";
        command.Parameters.AddWithValue("project_id", projectId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<Guid?> LockOpenSettlementWorkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id from work_items
            where project_id=@project_id and target_type='Project' and target_id=@project_id
              and workflow_stage_code='SalesSettlementCompleted' and status in ('Requested','InProgress')
            order by created_at_utc,id limit 1 for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task CompleteSettlementRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid settlementId,
        Guid projectId,
        bool insert,
        InvoiceProjection invoice,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insert
            ? """
                insert into sales_settlements (
                    id,project_id,status,invoice_issued_date,invoice_number,note,version,
                    created_by_user_id,updated_by_user_id,completed_by_user_id,completed_at_utc)
                values (@id,@project_id,'Completed',@invoice_date,@invoice_number,@note,1,
                        @actor_id,@actor_id,@actor_id,now());
                """
            : """
                update sales_settlements
                set status='Completed',invoice_issued_date=@invoice_date,invoice_number=@invoice_number,note=@note,
                    version=version+1,updated_by_user_id=@actor_id,updated_at_utc=now(),
                    completed_by_user_id=@actor_id,completed_at_utc=now()
                where id=@id;
                """;
        command.Parameters.AddWithValue("id", settlementId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("invoice_date", invoice.Date!.Value);
        AddNullableText(command, "invoice_number", invoice.Number);
        AddNullableText(command, "note", invoice.Note);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items set status='Completed',started_at_utc=coalesce(started_at_utc,now()),completed_at_utc=now()
            where id=@id and status in ('Requested','InProgress');
            """;
        command.Parameters.AddWithValue("id", workItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> EnsureCompletionEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid settlementId,
        Guid operationId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into project_workflow_events (
                    project_id,stage_code,event_type,event_status,source_type,source_id,
                    correlation_id,created_by_user_id,note)
                select @project_id,'SalesSettlementCompleted','StageCompleted','Succeeded','SalesSettlement',
                       @settlement_id,@correlation_id,@actor_id,'영업 정산 및 프로젝트 완료'
                where not exists (
                    select 1 from project_workflow_events
                    where project_id=@project_id and stage_code='SalesSettlementCompleted'
                      and event_type='StageCompleted' and event_status='Succeeded'
                );
                """;
            insert.Parameters.AddWithValue("project_id", projectId);
            insert.Parameters.AddWithValue("settlement_id", settlementId);
            insert.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
            insert.Parameters.AddWithValue("actor_id", actorId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = """
            select id from project_workflow_events
            where project_id=@project_id and stage_code='SalesSettlementCompleted'
              and event_type='StageCompleted' and event_status='Succeeded'
            order by created_at_utc,id limit 1;
            """;
        read.Parameters.AddWithValue("project_id", projectId);
        return (Guid)(await read.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Sales settlement completion event was not created."));
    }

    private static async Task CompleteProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update projects
            set status='Completed',status_reason='영업 정산 완료',completed_by_user_id=@actor_id,
                completed_at_utc=now(),updated_at_utc=now()
            where id=@project_id and status='Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Project completion state changed during settlement.");
    }

    private static async Task InsertAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid actorId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (
                project_id,entity_type,entity_id,action,field_name,old_value,new_value,reason,
                changed_by_user_id,correlation_id,is_sensitive)
            values (@project_id,'Project',@project_id,'ProjectCompleted','Status','Active','Completed',
                    '영업 정산 완료',@actor_id,@correlation_id,false);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("correlation_id", operationId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCompletionNotificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var notificationId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    id,project_id,notification_type,severity,title,message,link_url,
                    generated_by_event_id,idempotency_key,source_kind)
                values (@id,@project_id,'Reference','Info','프로젝트 완료','프로젝트가 최종 완료되었습니다.',
                        '/projects/' || @project_id || '/settlement',@event_id,
                        'sales-settlement:project:' || @project_id || ':completed','ProjectCompletion')
                on conflict (idempotency_key) do nothing;
                """;
            command.Parameters.AddWithValue("id", notificationId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("event_id", eventId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var recipients = connection.CreateCommand();
        recipients.Transaction = transaction;
        recipients.CommandText = """
            insert into notification_recipients (notification_id,user_id)
            select notification.id,user_id
            from notifications notification
            cross join lateral (
                select project.sales_owner_user_id as user_id from projects project where project.id=@project_id
                union
                select assignee.assigned_user_id from project_assignees assignee where assignee.project_id=@project_id
            ) target
            join qms_users users on users.id=target.user_id and users.is_active
            where notification.idempotency_key='sales-settlement:project:' || @project_id || ':completed'
            on conflict (notification_id,user_id) do nothing;
            """;
        recipients.Parameters.AddWithValue("project_id", projectId);
        await recipients.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SalesSettlementMutationResult<SalesSettlementMutationResponse>?> ReadReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        Guid actorId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select project_id,actor_user_id,payload_fingerprint,result_projection::text from sales_settlement_operations where operation_id=@operation_id;";
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (reader.GetGuid(0) != projectId || reader.GetGuid(1) != actorId || !string.Equals(reader.GetString(2), fingerprint, StringComparison.Ordinal))
            return SalesSettlementMutationResult<SalesSettlementMutationResponse>.Conflict("같은 operationId에 다른 요청 내용이 사용되었습니다.");
        var response = JsonSerializer.Deserialize<SalesSettlementMutationResponse>(reader.GetString(3), JsonOptions);
        return response is null
            ? SalesSettlementMutationResult<SalesSettlementMutationResponse>.Conflict("이전 정산 결과를 복구할 수 없습니다.")
            : SalesSettlementMutationResult<SalesSettlementMutationResponse>.Success(response with { Replayed = true });
    }

    private static async Task InsertReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid settlementId,
        Guid projectId,
        Guid actorId,
        string fingerprint,
        SalesSettlementMutationResponse response,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into sales_settlement_operations (
                operation_id,settlement_id,project_id,action,actor_user_id,payload_fingerprint,result_projection)
            values (@operation_id,@settlement_id,@project_id,'CompleteSalesSettlement',@actor_id,@fingerprint,@projection::jsonb);
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("settlement_id", settlementId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("projection", JsonSerializer.Serialize(response, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SalesSettlementDetailResponse?> ReadDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        ProjectAccessScope scope,
        Guid? actorId,
        bool hasSettlePermission,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project.id,coalesce(project.project_code,project.project_number),coalesce(project.project_title,project.name),project.status,
                   settlement.status,coalesce(settlement.version,0),settlement.invoice_issued_date,
                   settlement.invoice_number,settlement.note,settlement.completed_at_utc,completed_user.display_name,
                   exists(select 1 from project_assignees assignee where assignee.project_id=project.id and assignee.assigned_user_id=@actor_id and assignee.responsibility_type in ('SalesPrimary','SalesSecondary'))
                     or exists(select 1 from work_items work where work.project_id=project.id and work.target_type='Project' and work.target_id=project.id and work.workflow_stage_code='SalesSettlementCompleted' and work.status in ('Requested','InProgress') and work.assigned_user_id=@actor_id),
                   billing_batch.id, billing_batch.request_number, billing_batch.created_at_utc
            from projects project
            left join sales_settlements settlement on settlement.project_id=project.id
            left join qms_users completed_user on completed_user.id=settlement.completed_by_user_id
            left join sales_billing_request_items billing_item on billing_item.project_id=project.id
            left join sales_billing_request_batches billing_batch on billing_batch.id=billing_item.batch_id
            where project.id=@project_id and project.deleted_at_utc is null
              and (@has_read_all or project.project_key=any(@project_keys));
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId ?? Guid.Empty);
        AddScope(command, scope);

        Guid id;
        string code;
        string title;
        string projectStatus;
        string settlementStatus;
        int version;
        DateOnly? invoiceDate;
        string? invoiceNumber;
        string? note;
        DateTimeOffset? completedAt;
        string? completedBy;
        bool actorMatch;
        Guid? billingBatchId;
        long? billingRequestNumber;
        DateTimeOffset? billingRequestedAt;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            id = reader.GetGuid(0);
            code = reader.GetString(1);
            title = reader.GetString(2);
            projectStatus = reader.GetString(3);
            settlementStatus = reader.IsDBNull(4) ? "NotStarted" : reader.GetString(4);
            version = reader.GetInt32(5);
            invoiceDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6);
            invoiceNumber = reader.IsDBNull(7) ? null : reader.GetString(7);
            note = reader.IsDBNull(8) ? null : reader.GetString(8);
            completedAt = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9);
            completedBy = reader.IsDBNull(10) ? null : reader.GetString(10);
            actorMatch = reader.GetBoolean(11);
            billingBatchId = reader.IsDBNull(12) ? null : reader.GetGuid(12);
            billingRequestNumber = reader.IsDBNull(13) ? null : reader.GetInt64(13);
            billingRequestedAt = reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14);
        }

        var conditions = await ReadConditionsAsync(connection, transaction, projectId, cancellationToken);
        var allDelivered = conditions.ActivePanelCount > 0 && conditions.DeliveredPanelCount == conditions.ActivePanelCount;
        var noOpenPending = conditions.OpenPendingCount == 0;
        var invoiceIssued = invoiceDate is not null;
        var canMutate = hasSettlePermission && actorMatch && projectStatus == "Active" && settlementStatus != "Completed";
        return new SalesSettlementDetailResponse(
            id, code, title, projectStatus, settlementStatus, version,
            conditions.ActivePanelCount, conditions.DeliveredPanelCount, conditions.OpenPendingCount,
            invoiceDate, invoiceNumber, note, completedAt, completedBy,
            allDelivered, noOpenPending, invoiceIssued,
            canMutate && allDelivered && noOpenPending && invoiceIssued && billingBatchId is not null,
            canMutate,
            $"/pending?project={projectId:D}",
            new SalesBillingProjectStatusResponse(
                billingBatchId is not null,
                billingBatchId,
                billingRequestNumber,
                billingRequestedAt,
                settlementStatus == "Completed"));
    }

    private static string Fingerprint(Guid projectId, int expectedVersion, DateOnly date, string? number, string? note)
    {
        var text = $"complete|{projectId:D}|{expectedVersion}|{date:yyyy-MM-dd}|{number ?? ""}|{note ?? ""}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private static void AddScope(NpgsqlCommand command, ProjectAccessScope scope)
    {
        command.Parameters.AddWithValue("has_read_all", scope.HasProjectReadAll);
        command.Parameters.Add(new NpgsqlParameter<string[]>("project_keys", scope.ProjectKeys.ToArray()));
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private static void AddNullableDate(NpgsqlCommand command, string name, DateOnly? value)
        => command.Parameters.Add(name, NpgsqlDbType.Date).Value = value ?? (object)DBNull.Value;

    private static async Task<SalesSettlementMutationResult<T>> RollbackNotFound<T>(NpgsqlTransaction transaction, CancellationToken token)
    {
        await transaction.RollbackAsync(token);
        return SalesSettlementMutationResult<T>.NotFound();
    }

    private static async Task<SalesSettlementMutationResult<T>> RollbackForbidden<T>(NpgsqlTransaction transaction, CancellationToken token)
    {
        await transaction.RollbackAsync(token);
        return SalesSettlementMutationResult<T>.Forbidden();
    }

    private static async Task<SalesSettlementMutationResult<T>> RollbackConflict<T>(NpgsqlTransaction transaction, string message, CancellationToken token)
    {
        await transaction.RollbackAsync(token);
        return SalesSettlementMutationResult<T>.Conflict(message);
    }

    private static async Task<SalesSettlementMutationResult<T>> RollbackValidation<T>(NpgsqlTransaction transaction, string field, string message, CancellationToken token)
    {
        await transaction.RollbackAsync(token);
        return SalesSettlementMutationResult<T>.Validation(field, message);
    }

    private static async Task RollbackQuietly(NpgsqlTransaction transaction, CancellationToken token)
    {
        try { await transaction.RollbackAsync(token); } catch { }
    }

    private sealed record ProjectSnapshot(Guid Id, string Status, DateOnly CreatedDate, string? StructureMode, decimal? SalesAmount);
    private sealed record SettlementSnapshot(Guid Id, string Status, int Version);
    private sealed record ConditionSnapshot(int ActivePanelCount, int DeliveredPanelCount, int OpenPendingCount);
    private sealed record InvoiceProjection(DateOnly? Date, string? Number, string? Note, (string Field, string Message)? Error);
}
