using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Ul891Sets;

public sealed class MonthlyBillingStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<ProjectMutationResult<MonthlyBillingResponse>> GetAsync(
        Guid projectId,
        bool canReadAmounts,
        bool canMutate,
        CancellationToken token)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var project = await ReadProjectAsync(connection, null, projectId, false, token);
        if (project is null) return ProjectMutationResult<MonthlyBillingResponse>.NotFound();
        if (project.StructureMode != "Ul891Set")
        {
            return ProjectMutationResult<MonthlyBillingResponse>.Conflict("이 프로젝트는 기존 반월 세금계산서 발행요청 경로를 사용합니다.");
        }

        var evidenceByMonth = await ReadShipmentEvidenceByMonthAsync(connection, null, projectId, token);
        var recoveryCases = await ReadRecoveryCasesAsync(connection, null, projectId, token);
        var ledgers = await ReadLedgersAsync(connection, projectId, canReadAmounts, evidenceByMonth, recoveryCases, token);
        var currentRequested = ledgers.Sum(ledger => ledger.Revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault()?.Amount ?? 0m);
        var confirmed = ledgers.Where(ledger => ledger.Status == "InvoiceConfirmed")
            .Sum(ledger => ledger.Revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault()?.Amount ?? 0m);
        var unbilled = evidenceByMonth.OrderBy(pair => pair.Key).Select(pair =>
        {
            var ledger = ledgers.SingleOrDefault(item => item.BillingMonth == pair.Key);
            var latestCount = ledger?.Revisions.OrderByDescending(item => item.RevisionNumber).FirstOrDefault()?.Panels.Count ?? 0;
            return new MonthlyBillingEvidenceMonthResponse(
                pair.Key,
                pair.Value.Count,
                ledger is not null,
                ledger is null ? "발행요청 필요" : pair.Value.Count > latestCount ? "조정 필요" : "반영 완료");
        }).ToList();
        return ProjectMutationResult<MonthlyBillingResponse>.Success(new MonthlyBillingResponse(
            projectId,
            "Ul891Set",
            canReadAmounts ? project.SalesAmount : null,
            canReadAmounts ? project.CurrencyCode : null,
            canReadAmounts ? confirmed : 0,
            canReadAmounts ? currentRequested : 0,
            canReadAmounts && project.SalesAmount is not null ? project.SalesAmount.Value - currentRequested : null,
            canReadAmounts,
            canMutate,
            ledgers,
            unbilled));
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> OpenAsync(
        Guid projectId,
        OpenMonthlyBillingLedgerRequest request,
        Guid actorId,
        CancellationToken token)
    {
        var recoveryIds = request.RecoveryCaseIds?.Distinct().ToList() ?? [];
        if (request.OperationId == Guid.Empty || request.BillingMonth is null || request.BillingMonth.Value.Day != 1)
        {
            return Validation("BillingMonth", "청구 월은 해당 월 1일과 operationId로 입력해 주세요.");
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await ReadProjectAsync(connection, transaction, projectId, true, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.StructureMode != "Ul891Set") return await RollbackConflict(transaction, "UL891 세트 프로젝트만 월별 원장을 사용할 수 있습니다.", token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트에는 월별 청구 원장을 추가할 수 없습니다.", token);
        var fingerprint = Fingerprint("Open", projectId, request.BillingMonth, string.Join(',', recoveryIds.Order()));
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "Open", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        if (recoveryIds.Count > 0 && !await RecoveryCasesBelongAsync(connection, transaction, projectId, recoveryIds, token))
            return await RollbackValidation(transaction, "RecoveryCaseIds", "선택한 회수 사례를 사용할 수 없습니다.", token);
        var shipmentEvidence = await ReadShipmentEvidenceAsync(connection, transaction, projectId, request.BillingMonth.Value, token);
        if (shipmentEvidence.Count == 0 && recoveryIds.Count == 0)
            return await RollbackValidation(transaction, "BillingMonth", "해당 월의 출하 패널 또는 발주 후 취소 회수 사례가 있어야 발행요청을 만들 수 있습니다.", token);

        var ledgerId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into sales_monthly_billing_ledgers (
                    id,project_id,billing_month,kind,status,created_by_user_id,updated_by_user_id
                ) values (@id,@project_id,@billing_month,@kind,'Open',@actor_id,@actor_id)
                on conflict (project_id,billing_month) do nothing;
                """;
            command.Parameters.AddWithValue("id", ledgerId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("billing_month", request.BillingMonth.Value);
            command.Parameters.AddWithValue("kind", recoveryIds.Count > 0 ? "RecoveryOnly" : "Shipment");
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(token);
        }
        var response = new Ul891MutationResponse(request.OperationId, projectId, "MonthlyLedgerOpened", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "Open", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> CreateRevisionAsync(
        Guid projectId,
        Guid ledgerId,
        CreateMonthlyBillingRevisionRequest request,
        Guid actorId,
        CancellationToken token)
    {
        var note = Normalize(request.Note);
        var adjustmentReason = Normalize(request.AdjustmentReason);
        var recoveryIds = request.RecoveryCaseIds?.Distinct().ToList() ?? [];
        if (request.OperationId == Guid.Empty || request.ExpectedLedgerVersion is null || request.Amount is null or < 0)
            return Validation("Amount", "발행요청 금액·현재 버전·operationId를 확인해 주세요.");

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await ReadProjectAsync(connection, transaction, projectId, true, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.StructureMode != "Ul891Set") return await RollbackConflict(transaction, "UL891 세트 프로젝트만 월별 발행요청을 사용할 수 있습니다.", token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트의 발행요청은 변경할 수 없습니다.", token);
        if (project.SalesAmount is null or <= 0) return await RollbackValidation(transaction, "Amount", "프로젝트 판매액을 먼저 입력해 주세요.", token);
        var fingerprint = Fingerprint("Revision", projectId, ledgerId, request.ExpectedLedgerVersion, request.Amount, note, string.Join(',', recoveryIds.Order()), adjustmentReason);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "Revision", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        var ledger = await LockLedgerAsync(connection, transaction, projectId, ledgerId, token);
        if (ledger is null) return await RollbackNotFound(transaction, token);
        if (ledger.RowVersion != request.ExpectedLedgerVersion) return await RollbackConflict(transaction, "다른 사용자가 월별 발행요청을 변경했습니다. 최신 내용을 다시 불러와 주세요.", token);
        var latestConfirmed = await HasLatestConfirmationAsync(connection, transaction, ledgerId, token);
        var isAdjustment = latestConfirmed;
        if (isAdjustment && adjustmentReason is null) return await RollbackValidation(transaction, "AdjustmentReason", "회계 확인 뒤 추가 근거를 반영하려면 조정 사유가 필요합니다.", token);
        if (recoveryIds.Count > 0 && !await RecoveryCasesBelongAsync(connection, transaction, projectId, recoveryIds, token))
            return await RollbackValidation(transaction, "RecoveryCaseIds", "선택한 회수 사례를 사용할 수 없습니다.", token);
        var evidence = await ReadShipmentEvidenceAsync(connection, transaction, projectId, ledger.BillingMonth, token);
        if (evidence.Count == 0 && recoveryIds.Count == 0)
            return await RollbackValidation(transaction, "BillingMonth", "해당 월의 출하 패널 또는 발주 후 취소 회수 사례가 있어야 발행요청을 저장할 수 있습니다.", token);

        var otherLatestAmount = await ReadOtherLatestAmountAsync(connection, transaction, projectId, ledgerId, token);
        if (otherLatestAmount + request.Amount.Value > project.SalesAmount.Value)
            return await RollbackValidation(transaction, "Amount", $"누적 발행요청 금액은 프로젝트 판매액 {project.SalesAmount.Value:N0}을 초과할 수 없습니다.", token);

        var revisionNumber = await ReadNextRevisionNumberAsync(connection, transaction, ledgerId, token);
        var revisionId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into sales_monthly_billing_revisions (
                    id,ledger_id,revision_number,amount,note,is_adjustment,adjustment_reason,created_by_user_id
                ) values (@id,@ledger_id,@revision_number,@amount,@note,@is_adjustment,@adjustment_reason,@actor_id);
                """;
            command.Parameters.AddWithValue("id", revisionId);
            command.Parameters.AddWithValue("ledger_id", ledgerId);
            command.Parameters.AddWithValue("revision_number", revisionNumber);
            command.Parameters.AddWithValue("amount", request.Amount.Value);
            AddText(command, "note", note);
            command.Parameters.AddWithValue("is_adjustment", isAdjustment);
            AddText(command, "adjustment_reason", isAdjustment ? adjustmentReason : null);
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(token);
        }

        foreach (var panel in evidence)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = """
                insert into sales_monthly_billing_revision_panels (
                    revision_id,panel_id,panel_display_code,packing_unit_label,departure_date
                ) values (@revision_id,@panel_id,@display_code,@packing_unit_label,@departure_date);
                """;
            command.Parameters.AddWithValue("revision_id", revisionId); command.Parameters.AddWithValue("panel_id", panel.PanelId);
            command.Parameters.AddWithValue("display_code", panel.DisplayCode); AddText(command, "packing_unit_label", panel.PackingUnitLabel);
            command.Parameters.AddWithValue("departure_date", panel.DepartureDate); await command.ExecuteNonQueryAsync(token);
        }
        foreach (var caseId in recoveryIds)
        {
            await ExecuteAsync(connection, transaction, """
                insert into sales_monthly_billing_revision_cases (revision_id,recovery_case_id) values (@revision_id,@case_id);
                update ul891_recovery_cases set status='AppliedToRequest',row_version=row_version+1
                where id=@case_id and status='BillingRequired';
                insert into ul891_recovery_case_events (id,case_id,from_status,to_status,actor_user_id,reason)
                select uuid_generate_v4(),@case_id,'BillingRequired','AppliedToRequest',@actor_id,'월별 발행요청 반영'
                where exists(select 1 from ul891_recovery_cases where id=@case_id and status='AppliedToRequest')
                  and not exists(select 1 from ul891_recovery_case_events where case_id=@case_id and to_status='AppliedToRequest');
                """, token, ("revision_id", revisionId), ("case_id", caseId), ("actor_id", actorId));
        }
        await ExecuteAsync(connection, transaction, """
            update sales_monthly_billing_ledgers set status='Requested',row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now() where id=@ledger_id;
            """, token, ("actor_id", actorId), ("ledger_id", ledgerId));
        var response = new Ul891MutationResponse(request.OperationId, projectId, "MonthlyRevisionCreated", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "Revision", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> ConfirmAsync(
        Guid projectId,
        Guid ledgerId,
        ConfirmMonthlyBillingRequest request,
        Guid actorId,
        CancellationToken token)
    {
        var invoiceNumber = Normalize(request.InvoiceNumber);
        var note = Normalize(request.Note);
        var today = SeoulToday();
        if (request.OperationId == Guid.Empty || request.ExpectedLedgerVersion is null || request.InvoiceConfirmedDate is null || invoiceNumber is null || invoiceNumber.Length > 64)
            return Validation("InvoiceNumber", "회계 발행 확인일·번호·현재 버전·operationId를 확인해 주세요.");
        if (request.InvoiceConfirmedDate > today) return Validation("InvoiceConfirmedDate", "회계 발행 확인일은 오늘 이후일 수 없습니다.");

        await using var dataSource = CreateDataSource(); await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await ReadProjectAsync(connection, transaction, projectId, true, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트의 발행요청은 변경할 수 없습니다.", token);
        var fingerprint = Fingerprint("Confirm", projectId, ledgerId, request.ExpectedLedgerVersion, request.InvoiceConfirmedDate, invoiceNumber, note);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "Confirm", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }
        var ledger = await LockLedgerAsync(connection, transaction, projectId, ledgerId, token);
        if (ledger is null) return await RollbackNotFound(transaction, token);
        if (ledger.RowVersion != request.ExpectedLedgerVersion) return await RollbackConflict(transaction, "다른 사용자가 월별 발행요청을 변경했습니다. 최신 내용을 다시 불러와 주세요.", token);
        var latestRevisionId = await ReadLatestRevisionIdAsync(connection, transaction, ledgerId, token);
        if (latestRevisionId is null) return await RollbackConflict(transaction, "먼저 발행요청 revision을 생성해 주세요.", token);
        if (await HasConfirmationAsync(connection, transaction, latestRevisionId.Value, token)) return await RollbackConflict(transaction, "최신 발행요청은 이미 회계 발행 확인되었습니다.", token);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction=transaction; command.CommandText="""
                insert into sales_monthly_billing_confirmations (
                    id,ledger_id,revision_id,invoice_confirmed_date,invoice_number,note,confirmed_by_user_id
                ) values (@id,@ledger_id,@revision_id,@confirmed_date,@invoice_number,@note,@actor_id);
                update sales_monthly_billing_ledgers set status='InvoiceConfirmed',row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now() where id=@ledger_id;
                """;
            command.Parameters.AddWithValue("id",Guid.NewGuid()); command.Parameters.AddWithValue("ledger_id",ledgerId); command.Parameters.AddWithValue("revision_id",latestRevisionId.Value);
            command.Parameters.AddWithValue("confirmed_date",request.InvoiceConfirmedDate.Value); command.Parameters.AddWithValue("invoice_number",invoiceNumber); AddText(command,"note",note); command.Parameters.AddWithValue("actor_id",actorId);
            await command.ExecuteNonQueryAsync(token);
        }
        await ExecuteAsync(connection, transaction, """
            with changed as (
                update ul891_recovery_cases recovery set status='InvoiceConfirmed',row_version=row_version+1
                from sales_monthly_billing_revision_cases link
                where link.revision_id=@revision_id and link.recovery_case_id=recovery.id and recovery.status='AppliedToRequest'
                returning recovery.id
            )
            insert into ul891_recovery_case_events (id,case_id,from_status,to_status,actor_user_id,reason)
            select uuid_generate_v4(),id,'AppliedToRequest','InvoiceConfirmed',@actor_id,'회계 발행 확인' from changed;
            """, token, ("revision_id",latestRevisionId.Value),("actor_id",actorId));
        var response=new Ul891MutationResponse(request.OperationId,projectId,"MonthlyInvoiceConfirmed",false);
        await InsertOperationAsync(connection,transaction,request.OperationId,projectId,actorId,"Confirm",fingerprint,response,token);
        await transaction.CommitAsync(token); return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    internal static async Task<Ul891CompletionGate> ReadCompletionGateAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,decimal salesAmount,CancellationToken token)
    {
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="""
            with latest as (
                select distinct on (ledger.id)
                       ledger.id,ledger.billing_month,ledger.status,revision.amount,revision.id revision_id
                from sales_monthly_billing_ledgers ledger
                left join sales_monthly_billing_revisions revision on revision.ledger_id=ledger.id
                where ledger.project_id=@project_id
                order by ledger.id,revision.revision_number desc nulls last
            ), evidence as (
                select date_trunc('month',batch.departure_date)::date billing_month,
                       count(distinct panel.id)::integer panel_count
                from panel_placeholders panel
                join logistics_batch_panels batch_panel on batch_panel.panel_id=panel.id
                    and batch_panel.active and batch_panel.stage_code='DepartureProcessed'
                join logistics_batches batch on batch.id=batch_panel.batch_id
                    and batch.status='Finalized' and batch.departure_date is not null
                where panel.project_id=@project_id and panel.status='Active'
                group by date_trunc('month',batch.departure_date)::date
            ), latest_panel_counts as (
                select latest.id ledger_id,count(distinct revision_panel.panel_id)::integer panel_count
                from latest
                left join sales_monthly_billing_revision_panels revision_panel
                    on revision_panel.revision_id=latest.revision_id
                group by latest.id
            )
            select exists(select 1 from evidence)
                   and not exists(select 1 from latest where revision_id is null or status<>'InvoiceConfirmed')
                   and not exists(
                       select 1
                       from evidence
                       left join latest on latest.billing_month=evidence.billing_month
                       left join latest_panel_counts panel_counts on panel_counts.ledger_id=latest.id
                       where latest.revision_id is null
                          or latest.status<>'InvoiceConfirmed'
                          or coalesce(panel_counts.panel_count,0)<>evidence.panel_count
                   ) as all_confirmed,
                   coalesce(sum(amount),0)::numeric,
                   not exists(select 1 from ul891_recovery_cases where project_id=@project_id and status<>'Recovered') as recoveries_done
            from latest;
            """;
        command.Parameters.AddWithValue("project_id",projectId); await using var reader=await command.ExecuteReaderAsync(token); await reader.ReadAsync(token);
        var allConfirmed=reader.GetBoolean(0); var total=reader.GetDecimal(1); var recoveries=reader.GetBoolean(2);
        return new(allConfirmed,recoveries,total==salesAmount,total);
    }

    private static async Task<IReadOnlyList<MonthlyBillingLedgerResponse>> ReadLedgersAsync(NpgsqlConnection connection,Guid projectId,bool canReadAmounts,Dictionary<DateOnly,List<MonthlyBillingPanelEvidenceResponse>> evidenceByMonth,IReadOnlyList<Ul891RecoveryCaseResponse> recoveryCases,CancellationToken token)
    {
        var ledgers=new List<MonthlyBillingLedgerResponse>();
        await using var command=connection.CreateCommand(); command.CommandText="select id,billing_month,kind,status,row_version from sales_monthly_billing_ledgers where project_id=@project_id order by billing_month desc;"; command.Parameters.AddWithValue("project_id",projectId);
        var rows=new List<LedgerSnapshot>(); await using(var reader=await command.ExecuteReaderAsync(token)){while(await reader.ReadAsync(token))rows.Add(new(reader.GetGuid(0),reader.GetFieldValue<DateOnly>(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4)));}
        foreach(var row in rows)
        {
            var revisions=await ReadRevisionsAsync(connection,row.Id,canReadAmounts,token);
            var evidence=evidenceByMonth.GetValueOrDefault(row.Month) ?? [];
            var latestCount=revisions.OrderByDescending(item=>item.RevisionNumber).FirstOrDefault()?.Panels.Count ?? 0;
            var status=row.Status=="InvoiceConfirmed" && evidence.Count>latestCount ? "AdjustmentRequired" : row.Status;
            var available=recoveryCases.Where(item=>item.Status is "BillingRequired" or "AppliedToRequest").ToList();
            ledgers.Add(new(row.Id,row.Month,row.Kind,status,row.Version,revisions,evidence,available));
        }
        return ledgers;
    }

    private static async Task<IReadOnlyList<MonthlyBillingRevisionResponse>> ReadRevisionsAsync(NpgsqlConnection connection,Guid ledgerId,bool canReadAmounts,CancellationToken token)
    {
        var revisions=new List<RevisionBuilder>(); await using(var command=connection.CreateCommand())
        {
            command.CommandText="""
                select revision.id,revision.revision_number,revision.amount,revision.note,revision.is_adjustment,revision.adjustment_reason,revision.created_at_utc,
                       confirmation.invoice_confirmed_date,confirmation.invoice_number
                from sales_monthly_billing_revisions revision left join sales_monthly_billing_confirmations confirmation on confirmation.revision_id=revision.id
                where revision.ledger_id=@ledger_id order by revision.revision_number desc;
                """; command.Parameters.AddWithValue("ledger_id",ledgerId); await using var reader=await command.ExecuteReaderAsync(token);
            while(await reader.ReadAsync(token))revisions.Add(new(reader.GetGuid(0),reader.GetInt32(1),canReadAmounts?reader.GetDecimal(2):null,GetString(reader,3),reader.GetBoolean(4),GetString(reader,5),reader.GetFieldValue<DateTimeOffset>(6),GetDateOnly(reader,7),GetString(reader,8)));
        }
        foreach(var revision in revisions)
        {
            await using var panelCommand=connection.CreateCommand(); panelCommand.CommandText="select panel_id,panel_display_code,packing_unit_label,departure_date from sales_monthly_billing_revision_panels where revision_id=@revision_id order by panel_display_code;"; panelCommand.Parameters.AddWithValue("revision_id",revision.Id);
            await using(var reader=await panelCommand.ExecuteReaderAsync(token)){while(await reader.ReadAsync(token))revision.Panels.Add(new(reader.GetGuid(0),reader.GetString(1),null,GetString(reader,2),reader.GetFieldValue<DateOnly>(3)));}
            await using var caseCommand=connection.CreateCommand(); caseCommand.CommandText="select recovery_case_id from sales_monthly_billing_revision_cases where revision_id=@revision_id order by recovery_case_id;"; caseCommand.Parameters.AddWithValue("revision_id",revision.Id);
            await using(var reader=await caseCommand.ExecuteReaderAsync(token)){while(await reader.ReadAsync(token))revision.Cases.Add(reader.GetGuid(0));}
        }
        return revisions.Select(item=>item.Build()).ToList();
    }

    private static async Task<Dictionary<DateOnly,List<MonthlyBillingPanelEvidenceResponse>>> ReadShipmentEvidenceByMonthAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,Guid projectId,CancellationToken token)
    {
        var all=await ReadShipmentEvidenceAsync(connection,transaction,projectId,null,token); return all.GroupBy(item=>new DateOnly(item.DepartureDate.Year,item.DepartureDate.Month,1)).ToDictionary(group=>group.Key,group=>group.ToList());
    }

    private static async Task<List<MonthlyBillingPanelEvidenceResponse>> ReadShipmentEvidenceAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,Guid projectId,DateOnly? billingMonth,CancellationToken token)
    {
        var result=new List<MonthlyBillingPanelEvidenceResponse>(); await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="""
            select distinct panel.id,panel.display_code,
                   case when spec.id is null then null else spec.name || '-' || instance.instance_number::text || '-' || panel.component_code end,
                   'PKG-' || unit.unit_number::text,batch.departure_date
            from panel_placeholders panel
            join logistics_packing_unit_panels membership on membership.panel_id=panel.id and membership.active
            join logistics_packing_units unit on unit.id=membership.packing_unit_id
            join logistics_batch_panels batch_panel on batch_panel.panel_id=panel.id
                and batch_panel.packing_unit_id=unit.id and batch_panel.active and batch_panel.stage_code='DepartureProcessed'
            join logistics_batches batch on batch.id=batch_panel.batch_id and batch.status='Finalized' and batch.departure_date is not null
            left join ul891_set_instances instance on instance.id=panel.set_instance_id
            left join ul891_set_specs spec on spec.id=instance.spec_id
            where panel.project_id=@project_id
              and (@month is null or (batch.departure_date>=@month and batch.departure_date<(@month + interval '1 month')::date))
            order by batch.departure_date,panel.display_code;
            """; command.Parameters.AddWithValue("project_id",projectId); command.Parameters.Add("month",NpgsqlDbType.Date).Value=billingMonth ?? (object)DBNull.Value;
        await using var reader=await command.ExecuteReaderAsync(token); while(await reader.ReadAsync(token))result.Add(new(reader.GetGuid(0),reader.GetString(1),GetString(reader,2),reader.GetString(3),reader.GetFieldValue<DateOnly>(4))); return result;
    }

    private static async Task<IReadOnlyList<Ul891RecoveryCaseResponse>> ReadRecoveryCasesAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,Guid projectId,CancellationToken token)
    {
        var result=new List<Ul891RecoveryCaseResponse>(); await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="""
            select recovery.id,recovery.set_instance_id,instance.instance_number,recovery.procurement_item_id,coalesce(item.order_item,'품목 '||item.sequence_number::text),item.order_date,recovery.status,recovery.note,recovery.row_version,recovery.created_at_utc,recovery.recovered_at_utc
            from ul891_recovery_cases recovery join ul891_set_instances instance on instance.id=recovery.set_instance_id join project_procurement_items item on item.id=recovery.procurement_item_id
            where recovery.project_id=@project_id order by recovery.created_at_utc;
            """; command.Parameters.AddWithValue("project_id",projectId); await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token))result.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetInt32(2),reader.GetGuid(3),reader.GetString(4),reader.GetFieldValue<DateOnly>(5),reader.GetString(6),GetString(reader,7),reader.GetInt32(8),reader.GetFieldValue<DateTimeOffset>(9),GetDateTimeOffset(reader,10))); return result;
    }

    private static async Task<ProjectSnapshot?> ReadProjectAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,Guid projectId,bool lockRow,CancellationToken token)
    {
        await using var command=connection.CreateCommand(); command.Transaction=transaction; command.CommandText="select status,structure_mode,sales_amount,currency_code from projects where id=@project_id and deleted_at_utc is null"+(lockRow?" for update":"")+";"; command.Parameters.AddWithValue("project_id",projectId);
        await using var reader=await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token)?new(reader.GetString(0),GetString(reader,1),GetDecimal(reader,2),GetString(reader,3)):null;
    }

    private static async Task<LedgerSnapshot?> LockLedgerAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,Guid ledgerId,CancellationToken token)
    {await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select id,billing_month,kind,status,row_version from sales_monthly_billing_ledgers where id=@ledger_id and project_id=@project_id for update;";command.Parameters.AddWithValue("ledger_id",ledgerId);command.Parameters.AddWithValue("project_id",projectId);await using var reader=await command.ExecuteReaderAsync(token);return await reader.ReadAsync(token)?new(reader.GetGuid(0),reader.GetFieldValue<DateOnly>(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4)):null;}
    private static async Task<bool> RecoveryCasesBelongAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,IReadOnlyList<Guid> ids,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select count(*)::integer from ul891_recovery_cases where project_id=@project_id and id=any(@ids) and status in ('BillingRequired','AppliedToRequest');";command.Parameters.AddWithValue("project_id",projectId);command.Parameters.Add(new NpgsqlParameter<Guid[]>("ids",ids.ToArray()));return (int)(await command.ExecuteScalarAsync(token)??0)==ids.Count;}
    private static async Task<decimal> ReadOtherLatestAmountAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,Guid excluded,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select coalesce(sum(latest.amount),0)::numeric from sales_monthly_billing_ledgers ledger join lateral(select amount from sales_monthly_billing_revisions where ledger_id=ledger.id order by revision_number desc limit 1) latest on true where ledger.project_id=@project_id and ledger.id<>@excluded;";command.Parameters.AddWithValue("project_id",projectId);command.Parameters.AddWithValue("excluded",excluded);return (decimal)(await command.ExecuteScalarAsync(token)??0m);}
    private static async Task<int> ReadNextRevisionNumberAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid ledgerId,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select coalesce(max(revision_number),0)::integer+1 from sales_monthly_billing_revisions where ledger_id=@ledger_id;";command.Parameters.AddWithValue("ledger_id",ledgerId);return (int)(await command.ExecuteScalarAsync(token)??1);}
    private static async Task<Guid?> ReadLatestRevisionIdAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid ledgerId,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select id from sales_monthly_billing_revisions where ledger_id=@ledger_id order by revision_number desc limit 1;";command.Parameters.AddWithValue("ledger_id",ledgerId);var value=await command.ExecuteScalarAsync(token);return value is Guid id?id:null;}
    private static async Task<bool> HasConfirmationAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid revisionId,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from sales_monthly_billing_confirmations where revision_id=@revision_id);";command.Parameters.AddWithValue("revision_id",revisionId);return (bool)(await command.ExecuteScalarAsync(token)??false);}
    private static async Task<bool> HasLatestConfirmationAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid ledgerId,CancellationToken token){var id=await ReadLatestRevisionIdAsync(connection,transaction,ledgerId,token);return id is not null&&await HasConfirmationAsync(connection,transaction,id.Value,token);}

    private static async Task<ProjectMutationResult<Ul891MutationResponse>?> CheckReplayAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid operationId,Guid projectId,Guid actorId,string action,string fingerprint,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select project_id,actor_user_id,action,payload_fingerprint from sales_monthly_billing_operations where operation_id=@operation_id;";command.Parameters.AddWithValue("operation_id",operationId);await using var reader=await command.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))return null;return reader.GetGuid(0)==projectId&&reader.GetGuid(1)==actorId&&reader.GetString(2)==action&&reader.GetString(3)==fingerprint?ProjectMutationResult<Ul891MutationResponse>.Success(new(operationId,projectId,action,true)):ProjectMutationResult<Ul891MutationResponse>.Conflict("같은 operationId에 다른 요청 내용이 사용되었습니다.");}
    private static async Task InsertOperationAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid operationId,Guid projectId,Guid actorId,string action,string fingerprint,Ul891MutationResponse response,CancellationToken token){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into sales_monthly_billing_operations(operation_id,project_id,actor_user_id,action,payload_fingerprint,result_projection) values(@operation_id,@project_id,@actor_id,@action,@fingerprint,@result::jsonb);";command.Parameters.AddWithValue("operation_id",operationId);command.Parameters.AddWithValue("project_id",projectId);command.Parameters.AddWithValue("actor_id",actorId);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("fingerprint",fingerprint);command.Parameters.AddWithValue("result",JsonSerializer.Serialize(response));await command.ExecuteNonQueryAsync(token);}
    private static async Task ExecuteAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string sql,CancellationToken token,params(string Name,object Value)[] parameters){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;foreach(var item in parameters)command.Parameters.AddWithValue(item.Name,item.Value);await command.ExecuteNonQueryAsync(token);}
    private NpgsqlDataSource CreateDataSource(){var value=connectionStringProvider.GetConnectionString();if(string.IsNullOrWhiteSpace(value))throw new InvalidOperationException("QMS database connection string is not configured.");return NpgsqlDataSource.Create(value);}
    private static ProjectMutationResult<Ul891MutationResponse> Validation(string field,string message)=>ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string,string[]>{{field,[message]}});
    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackNotFound(NpgsqlTransaction tx,CancellationToken token){await tx.RollbackAsync(token);return ProjectMutationResult<Ul891MutationResponse>.NotFound();}
    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackConflict(NpgsqlTransaction tx,string message,CancellationToken token){await tx.RollbackAsync(token);return ProjectMutationResult<Ul891MutationResponse>.Conflict(message);}
    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackValidation(NpgsqlTransaction tx,string field,string message,CancellationToken token){await tx.RollbackAsync(token);return Validation(field,message);}
    private static void AddText(NpgsqlCommand command,string name,string? value)=>command.Parameters.Add(name,NpgsqlDbType.Text).Value=value??(object)DBNull.Value;
    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static string Fingerprint(params object?[] values)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",values.Select(value=>Convert.ToString(value,CultureInfo.InvariantCulture)??""))))).ToLowerInvariant();
    private static string? GetString(NpgsqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
    private static decimal? GetDecimal(NpgsqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetDecimal(ordinal);
    private static DateOnly? GetDateOnly(NpgsqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetFieldValue<DateOnly>(ordinal);
    private static DateTimeOffset? GetDateTimeOffset(NpgsqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetFieldValue<DateTimeOffset>(ordinal);
    private static DateOnly SeoulToday(){var zone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).DateTime);}

    private sealed record ProjectSnapshot(string Status,string? StructureMode,decimal? SalesAmount,string? CurrencyCode);
    private sealed record LedgerSnapshot(Guid Id,DateOnly Month,string Kind,string Status,int Version){public DateOnly BillingMonth=>Month;public int RowVersion=>Version;}
    private sealed class RevisionBuilder(Guid id,int number,decimal? amount,string? note,bool adjustment,string? adjustmentReason,DateTimeOffset createdAt,DateOnly? confirmedDate,string? invoiceNumber){public Guid Id{get;}=id;public int Number{get;}=number;public List<MonthlyBillingPanelEvidenceResponse> Panels{get;}=[];public List<Guid> Cases{get;}=[];public MonthlyBillingRevisionResponse Build()=>new(Id,Number,amount,note,adjustment,adjustmentReason,createdAt,confirmedDate,invoiceNumber,Panels,Cases);}
}

public sealed record Ul891CompletionGate(bool AllLedgersConfirmed,bool AllRecoveriesConfirmed,bool RequestedAmountMatchesSalesAmount,decimal RequestedAmount);
