using Emi.Qms.Api.Notifications;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanWorkRequestStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private const int MaximumRecipients = 100;

    public async Task<OsanWorkRequestRecipientsResponse> ListRecipientsAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        await using var source = CreateDataSource();
        await using var command = source.CreateCommand("""
            select users.id, users.display_name, department.name
            from qms_users users
            left join departments department on department.id=users.department_id
            where users.is_active=true
              and (users.auth_provider <> 'EntraId' or exists(
                    select 1 from user_roles role where role.user_id=users.id))
            order by coalesce(department.sort_order,2147483647),
                     coalesce(department.name,''),users.display_name,users.id;
            """);
        var recipients = new List<OsanWorkRequestRecipientResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            recipients.Add(new(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return new(projectId, recipients);
    }

    public async Task<OsanWorkRequestResult> CreateAsync(
        Guid projectId,
        CreateOsanWorkRequest input,
        Guid requesterUserId,
        CancellationToken cancellationToken)
    {
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return new(OsanWorkRequestStatus.Validation, Errors: errors);
        }

        var recipientIds = input.RecipientIds!.Order().ToArray();
        var fingerprint = OsanStageRecords.Fingerprint(new
        {
            ProjectId = projectId,
            input.TargetId,
            input.StageSequence,
            RecipientIds = recipientIds,
            RequesterUserId = requesterUserId
        });

        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("operation", input.OperationId.ToString("D"));
        command.CommandText = "select pg_advisory_xact_lock(hashtextextended(@operation,0));";
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.Parameters.Clear();
        command.Parameters.AddWithValue("operation", input.OperationId);
        command.CommandText = "select payload_fingerprint from osan_stage_work_requests where operation_id=@operation;";
        if (await command.ExecuteScalarAsync(cancellationToken) is string existingFingerprint)
        {
            if (!string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return new(
                    OsanWorkRequestStatus.Conflict,
                    ErrorCode: "osan_work_request_operation_conflict",
                    Message: "같은 요청 식별자가 다른 공정 요청에 사용되었습니다.");
            }

            var replay = await ReadAsync(connection, transaction, input.OperationId, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(OsanWorkRequestStatus.Success, replay);
        }

        command.Parameters.Clear();
        command.Parameters.AddWithValue("project", projectId);
        command.Parameters.AddWithValue("target", input.TargetId);
        command.Parameters.AddWithValue("stage", input.StageSequence);
        command.Parameters.AddWithValue("requester", requesterUserId);
        command.CommandText = """
            select step.id,target.display_name,step.step_name,requester.display_name
            from projects project
            join osan_active_project_targets target
              on target.project_id=project.id and target.id=@target
            join osan_active_project_target_steps step
              on step.project_id=project.id and step.target_id=target.id and step.sequence_number=@stage
            join qms_users requester on requester.id=@requester and requester.is_active=true
            where project.id=@project and project.project_profile='Osan'
              and project.deleted_at_utc is null;
            """;
        Guid stepId;
        string targetName;
        string stageName;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new(
                    OsanWorkRequestStatus.NotFound,
                    ErrorCode: "osan_work_request_target_not_found",
                    Message: "요청할 프로젝트, 진행 대상 또는 공정을 찾을 수 없습니다.");
            }
            stepId = reader.GetGuid(0);
            targetName = reader.GetString(1);
            stageName = reader.GetString(2);
        }

        command.Parameters.Clear();
        command.Parameters.AddWithValue("recipients", recipientIds);
        command.CommandText = """
            select users.id,users.display_name,department.name
            from qms_users users
            left join departments department on department.id=users.department_id
            where users.id=any(@recipients) and users.is_active=true
              and (users.auth_provider <> 'EntraId' or exists(
                    select 1 from user_roles role where role.user_id=users.id))
            order by users.id;
            """;
        var recipients = new List<OsanWorkRequestRecipientResponse>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                recipients.Add(new(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }
        if (recipients.Count != recipientIds.Length)
        {
            return new(
                OsanWorkRequestStatus.Validation,
                Errors: new Dictionary<string, string[]>
                {
                    ["recipientIds"] = ["활성 상태인 오산 사용자만 수신자로 선택할 수 있습니다."]
                });
        }

        var requestedAt = DateTimeOffset.UtcNow;
        var historyRecordId = await OsanStageRecords.AddAsync(
            connection, transaction, projectId, stepId, input.OperationId,
            "WorkRequested", requesterUserId, "", null, [], cancellationToken, fingerprint);

        command.Parameters.Clear();
        command.Parameters.AddWithValue("operation", input.OperationId);
        command.Parameters.AddWithValue("project", projectId);
        command.Parameters.AddWithValue("target", input.TargetId);
        command.Parameters.AddWithValue("step", stepId);
        command.Parameters.AddWithValue("stage", (short)input.StageSequence);
        command.Parameters.AddWithValue("requester", requesterUserId);
        command.Parameters.AddWithValue("requested_at", requestedAt);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("history", historyRecordId);
        command.CommandText = """
            insert into osan_stage_work_requests(
                operation_id,project_id,target_id,step_id,stage_sequence,
                requested_by_user_id,requested_at_utc,payload_fingerprint,history_record_id)
            values(@operation,@project,@target,@step,@stage,
                @requester,@requested_at,@fingerprint,@history);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("operation", input.OperationId);
            command.Parameters.AddWithValue("recipient", recipient.UserId);
            command.Parameters.AddWithValue("display_name", recipient.DisplayName);
            command.Parameters.AddWithValue("department_name", NpgsqlTypes.NpgsqlDbType.Text,
                (object?)recipient.DepartmentName ?? DBNull.Value);
            command.CommandText = """
                insert into osan_stage_work_request_recipients(
                    operation_id,recipient_user_id,display_name_snapshot,department_name_snapshot)
                values(@operation,@recipient,@display_name,@department_name);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await OsanNotificationWriter.WriteAsync(
            connection, transaction, projectId, input.OperationId,
            OsanNotificationKind.StepWorkRequested, requesterUserId, requestedAt, cancellationToken,
            stageName, [input.TargetId], recipientIds: recipientIds, stageSequence: input.StageSequence);

        var response = new OsanWorkRequestResponse(
            input.OperationId, false, requestedAt, requesterUserId,
            await ReadRequesterNameAsync(connection, transaction, requesterUserId, cancellationToken),
            input.TargetId, targetName, input.StageSequence, stageName, recipients);
        await transaction.CommitAsync(cancellationToken);
        return new(OsanWorkRequestStatus.Success, response);
    }

    private static Dictionary<string, string[]> Validate(CreateOsanWorkRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.OperationId == Guid.Empty)
            errors["operationId"] = ["요청 식별자가 필요합니다."];
        if (input.TargetId == Guid.Empty)
            errors["targetId"] = ["진행 대상을 선택해 주세요."];
        if (input.StageSequence is < 1 or > 7)
            errors["stageSequence"] = ["공정은 1단계부터 7단계까지 선택할 수 있습니다."];
        if (input.RecipientIds is null || input.RecipientIds.Count == 0)
            errors["recipientIds"] = ["수신자를 한 명 이상 선택해 주세요."];
        else if (input.RecipientIds.Count > MaximumRecipients)
            errors["recipientIds"] = [$"수신자는 최대 {MaximumRecipients}명까지 선택할 수 있습니다."];
        else if (input.RecipientIds.Any(id => id == Guid.Empty)
                 || input.RecipientIds.Distinct().Count() != input.RecipientIds.Count)
            errors["recipientIds"] = ["수신자 목록에 잘못되거나 중복된 사용자가 있습니다."];
        return errors;
    }

    private static async Task<OsanWorkRequestResponse> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        bool replayed,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("operation", operationId);
        command.CommandText = """
            select request.requested_at_utc,request.requested_by_user_id,requester.display_name,
                   request.target_id,target.display_name,request.stage_sequence,step.step_name
            from osan_stage_work_requests request
            join qms_users requester on requester.id=request.requested_by_user_id
            join osan_project_targets target on target.id=request.target_id
            join osan_project_target_steps step on step.id=request.step_id
            where request.operation_id=@operation;
            """;
        DateTimeOffset requestedAt;
        Guid requesterId;
        string requesterName;
        Guid targetId;
        string targetName;
        int stageSequence;
        string stageName;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Stored Osan work request could not be read.");
            requestedAt = reader.GetFieldValue<DateTimeOffset>(0);
            requesterId = reader.GetGuid(1);
            requesterName = reader.GetString(2);
            targetId = reader.GetGuid(3);
            targetName = reader.GetString(4);
            stageSequence = reader.GetInt16(5);
            stageName = reader.GetString(6);
        }

        command.CommandText = """
            select recipient_user_id,display_name_snapshot,department_name_snapshot
            from osan_stage_work_request_recipients
            where operation_id=@operation
            order by display_name_snapshot,recipient_user_id;
            """;
        var recipients = new List<OsanWorkRequestRecipientResponse>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                recipients.Add(new(reader.GetGuid(0),reader.GetString(1),reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return new(operationId,replayed,requestedAt,requesterId,requesterName,
            targetId,targetName,stageSequence,stageName,recipients);
    }

    private static async Task<string> ReadRequesterNameAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requesterId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select display_name from qms_users where id=@id;";
        command.Parameters.AddWithValue("id", requesterId);
        return (string)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Osan work request requester was not found."));
    }

    private NpgsqlDataSource CreateDataSource() =>
        NpgsqlDataSource.Create(connectionStringProvider.GetConnectionString()
            ?? throw new InvalidOperationException("QMS database connection string is not configured."));
}
