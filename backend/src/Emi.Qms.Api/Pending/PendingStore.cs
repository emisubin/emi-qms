using Emi.Qms.Api.Materials;
using Emi.Qms.Api.QualityInspections;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Pending;

public sealed class PendingStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private static readonly IReadOnlyDictionary<string, string> NextStatuses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PendingStatuses.Registered] = PendingStatuses.ActionRequested,
        [PendingStatuses.ActionRequested] = PendingStatuses.InProgress,
        [PendingStatuses.InProgress] = PendingStatuses.ReinspectionRequested,
        [PendingStatuses.ReinspectionRequested] = PendingStatuses.Closed
    };

    public async Task<PendingListResponse> ListAsync(
        string? status,
        string? issueType,
        string? priority,
        Guid? assigneeUserId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = PendingStatuses.All.Contains(status ?? "") ? status : null;
        var normalizedType = string.IsNullOrWhiteSpace(issueType) ? null : issueType.Trim();
        var normalizedPriority = PendingPriorities.All.Contains(priority ?? "") ? priority : null;

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                pi.id,
                pi.issue_number,
                pi.project_id,
                p.project_code,
                p.project_title,
                pi.target_type,
                pi.target_id,
                case when pi.target_type = 'Panel' then panel.display_code else null end,
                pi.issue_type,
                issue_type.display_name,
                pi.title,
                pi.description,
                pi.status,
                pi.priority,
                pi.action_department_code,
                pi.assignee_user_id,
                assignee.display_name,
                pi.due_date,
                pi.version,
                pi.created_by_user_id,
                creator.display_name,
                pi.created_at_utc,
                pi.updated_at_utc
            from pending_issues pi
            join projects p on p.id = pi.project_id and p.deleted_at_utc is null
            join qms_users creator on creator.id = pi.created_by_user_id
            join pending_issue_type_catalog issue_type on issue_type.code = pi.issue_type
            left join qms_users assignee on assignee.id = pi.assignee_user_id
            left join panel_placeholders panel on panel.id = pi.target_id and pi.target_type = 'Panel'
            where (@status is null or pi.status = @status)
              and (@issue_type is null or pi.issue_type = @issue_type)
              and (@priority is null or pi.priority = @priority)
              and (@assignee_user_id is null or pi.assignee_user_id = @assignee_user_id)
              and (@project_id is null or pi.project_id = @project_id)
            order by
                case when pi.status = 'Closed' then 1 else 0 end,
                case pi.priority when 'Urgent' then 0 else 1 end,
                pi.due_date nulls last,
                pi.updated_at_utc desc;
            """);
        AddNullableText(command, "status", normalizedStatus);
        AddNullableText(command, "issue_type", normalizedType);
        AddNullableText(command, "priority", normalizedPriority);
        AddNullableUuid(command, "assignee_user_id", assigneeUserId);
        AddNullableUuid(command, "project_id", projectId);

        var items = new List<PendingListItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadIssue(reader));
        }

        await reader.DisposeAsync();
        var summary = await ReadSummaryAsync(dataSource, projectId, cancellationToken);
        return new PendingListResponse(summary, items);
    }

    public async Task<PendingDetailResponse?> GetDetailAsync(
        Guid pendingId,
        PendingActor actor,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var issue = await ReadIssueAsync(connection, pendingId, cancellationToken);
        if (issue is null)
        {
            return null;
        }

        var comments = await ReadCommentsAsync(connection, pendingId, cancellationToken);
        var history = await ReadHistoryAsync(connection, pendingId, cancellationToken);
        var isInspectionPending = await IsQualityInspectionPendingAsync(connection, null, pendingId, cancellationToken);
        var reinspection = await ReadPendingReinspectionAsync(connection, pendingId, cancellationToken);
        return BuildDetail(issue, comments, history, actor, isInspectionPending, reinspection);
    }

    public async Task<IReadOnlyList<PendingAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select distinct u.id, u.display_name, d.code
            from qms_users u
            join departments d on d.id = u.department_id
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            join role_permissions rp on rp.role_id = r.id
            join permissions permission on permission.id = rp.permission_id
            where u.is_active = true
              and permission.code = 'Pending.Manage'
            order by u.display_name;
            """);

        var users = new List<PendingAssigneeResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new PendingAssigneeResponse(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return users;
    }

    public async Task<PendingMutationResult<PendingDetailResponse>> CreateAsync(
        CreatePendingRequest request,
        PendingActor actor,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreate(request);
        if (errors.Count > 0)
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(errors);
        }

        var projectId = request.ProjectId!.Value;
        var issueType = request.IssueType!.Trim();
        var title = request.Title!.Trim();
        var description = request.Description!.Trim();
        var priority = request.Priority!.Trim();
        var actionDepartmentCode = string.IsNullOrWhiteSpace(request.ActionDepartmentCode)
            ? null
            : request.ActionDepartmentCode.Trim();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var projectStatus = await LockProjectForPendingCreationAsync(connection, transaction, projectId, cancellationToken);
        if (projectStatus is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ProjectId)] = ["진행 중인 프로젝트를 선택해 주세요."]
            });
        }
        if (projectStatus is not ("Active" or "OnHold"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PendingMutationResult<PendingDetailResponse>.Conflict("완료·취소된 프로젝트에는 Pending을 등록할 수 없습니다.");
        }

        if (!await IsAvailableManualTypeAsync(connection, transaction, issueType, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.IssueType)] = ["현재 수동 등록에 사용할 수 있는 Pending 유형을 선택해 주세요."]
            });
        }

        if (request.AssigneeUserId is not null
            && !await IsValidAssigneeAsync(connection, transaction, request.AssigneeUserId.Value, cancellationToken))
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.AssigneeUserId)] = ["Pending 조치 권한이 있는 활성 사용자를 선택해 주세요."]
            });
        }

        if (actionDepartmentCode is not null
            && !await IsActiveDepartmentAsync(connection, transaction, actionDepartmentCode, cancellationToken))
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ActionDepartmentCode)] = ["활성 조치 담당 부서를 선택해 주세요."]
            });
        }

        if (actionDepartmentCode is not null
            && request.AssigneeUserId is not null
            && !await IsValidAssigneeInDepartmentAsync(
                connection,
                transaction,
                request.AssigneeUserId.Value,
                actionDepartmentCode,
                cancellationToken))
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.AssigneeUserId)] = ["선택한 부서에서 Pending 조치 권한이 있는 활성 담당자를 선택해 주세요."]
            });
        }

        var automaticAssignees = request.AssigneeUserId is null && actionDepartmentCode is not null
            ? await ResolveProjectActionAssigneesAsync(connection, transaction, projectId, actionDepartmentCode, cancellationToken)
            : new PendingAssigneePair(request.AssigneeUserId, null);
        var effectiveAssigneeUserId = request.AssigneeUserId ?? automaticAssignees.PrimaryUserId;
        var status = effectiveAssigneeUserId is null ? PendingStatuses.Registered : PendingStatuses.ActionRequested;

        var pendingId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_issues (
                    id, project_id, target_type, target_id, issue_type, title, description,
                    status, priority, action_department_code, assignee_user_id, due_date, created_by_user_id,
                    updated_by_user_id
                )
                values (
                    @id, @project_id, 'Project', @project_id, @issue_type, @title, @description,
                    @status, @priority, @action_department_code, @assignee_user_id, @due_date, @actor_id,
                    @actor_id
                );
                """;
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("issue_type", issueType);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("priority", priority);
            AddNullableText(command, "action_department_code", actionDepartmentCode);
            AddNullableUuid(command, "assignee_user_id", effectiveAssigneeUserId);
            AddNullableDate(command, "due_date", request.DueDate);
            command.Parameters.AddWithValue("actor_id", actor.UserId);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException exception) when (
                exception.SqlState == PostgresErrorCodes.CheckViolation
                && exception.ConstraintName == "ck_pending_issues_project_lifecycle")
            {
                await transaction.RollbackAsync(cancellationToken);
                return PendingMutationResult<PendingDetailResponse>.Conflict("완료·취소된 프로젝트에는 Pending을 등록할 수 없습니다.");
            }
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "Created",
            null,
            status,
            null,
            effectiveAssigneeUserId,
            "Pending 등록",
            actor.UserId,
            correlationId,
            cancellationToken);

        if (effectiveAssigneeUserId is not null)
        {
            await CreateAssignmentArtifactsAsync(
                connection,
                transaction,
                pendingId,
                projectId,
                title,
                description,
                priority,
                request.DueDate,
                effectiveAssigneeUserId.Value,
                automaticAssignees.SecondaryUserId,
                actionDepartmentCode,
                actor.UserId,
                1,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var detail = await GetDetailAsync(pendingId, actor, cancellationToken);
        return detail is null
            ? PendingMutationResult<PendingDetailResponse>.NotFound()
            : PendingMutationResult<PendingDetailResponse>.Success(detail);
    }

    public async Task<PendingMutationResult<PendingDetailResponse>> TransitionAsync(
        Guid pendingId,
        TransitionPendingRequest request,
        PendingActor actor,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var toStatus = request.ToStatus?.Trim();
        var reason = request.Reason?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (toStatus is null || !PendingStatuses.All.Contains(toStatus))
        {
            errors[nameof(request.ToStatus)] = ["변경할 상태를 선택해 주세요."];
        }
        if (request.ExpectedVersion is null or < 1)
        {
            errors[nameof(request.ExpectedVersion)] = ["최신 version 정보가 필요합니다."];
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 3 or > 500)
        {
            errors[nameof(request.Reason)] = ["변경 사유를 3~500자로 입력해 주세요."];
        }
        if (errors.Count > 0)
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken);
        if (issue is null)
        {
            return PendingMutationResult<PendingDetailResponse>.NotFound();
        }
        if (issue.Version != request.ExpectedVersion)
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
        if (!NextStatuses.TryGetValue(issue.Status, out var expectedStatus)
            || !string.Equals(expectedStatus, toStatus, StringComparison.Ordinal))
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("현재 상태에서 요청한 상태로 변경할 수 없습니다.");
        }
        if (string.Equals(toStatus, PendingStatuses.Closed, StringComparison.Ordinal)
            && await IsQualityInspectionPendingAsync(connection, transaction, pendingId, cancellationToken))
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("품질검사 Pending은 재검사 합격 처리에서만 종결할 수 있습니다.");
        }
        if (toStatus != PendingStatuses.Registered && issue.AssigneeUserId is null)
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("먼저 조치 담당자를 지정해 주세요.");
        }
        if (!CanTransition(issue, actor, toStatus!))
        {
            return PendingMutationResult<PendingDetailResponse>.Forbidden();
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issues
                set status = @status,
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now(),
                    closed_by_user_id = case when @status = 'Closed' then @actor_id else null end,
                    closed_at_utc = case when @status = 'Closed' then now() else null end
                where id = @id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("status", toStatus!);
            command.Parameters.AddWithValue("actor_id", actor.UserId);
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedVersion!.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return PendingMutationResult<PendingDetailResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        await SyncWorkItemStatusAsync(connection, transaction, pendingId, toStatus!, cancellationToken);
        if (string.Equals(toStatus, PendingStatuses.ReinspectionRequested, StringComparison.Ordinal))
        {
            var materialAttemptId = await MaterialsStore.EnsurePendingReinspectionAsync(
                connection, transaction, pendingId, actor.UserId, cancellationToken);
            if (materialAttemptId is null)
            {
                await QualityInspectionStore.EnsurePendingReinspectionAsync(
                    connection, transaction, pendingId, actor.UserId, cancellationToken);
            }
        }
        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "StatusChanged",
            issue.Status,
            toStatus,
            issue.AssigneeUserId,
            issue.AssigneeUserId,
            reason,
            actor.UserId,
            correlationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await GetDetailAsync(pendingId, actor, cancellationToken);
        return detail is null
            ? PendingMutationResult<PendingDetailResponse>.NotFound()
            : PendingMutationResult<PendingDetailResponse>.Success(detail);
    }

    public async Task<PendingMutationResult<PendingDetailResponse>> AssignAsync(
        Guid pendingId,
        AssignPendingRequest request,
        PendingActor actor,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (request.AssigneeUserId is null)
        {
            errors[nameof(request.AssigneeUserId)] = ["조치 담당자를 선택해 주세요."];
        }
        if (request.ExpectedVersion is null or < 1)
        {
            errors[nameof(request.ExpectedVersion)] = ["최신 version 정보가 필요합니다."];
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 3 or > 500)
        {
            errors[nameof(request.Reason)] = ["담당 변경 사유를 3~500자로 입력해 주세요."];
        }
        if (errors.Count > 0)
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(errors);
        }
        if (!actor.IsCoordinator)
        {
            return PendingMutationResult<PendingDetailResponse>.Forbidden();
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken);
        if (issue is null)
        {
            return PendingMutationResult<PendingDetailResponse>.NotFound();
        }
        if (issue.Version != request.ExpectedVersion)
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
        }
        if (issue.Status == PendingStatuses.Closed)
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("종결된 Pending의 담당자는 변경할 수 없습니다.");
        }
        if (issue.AssigneeUserId == request.AssigneeUserId)
        {
            return PendingMutationResult<PendingDetailResponse>.Conflict("현재 담당자와 같은 사용자입니다.");
        }
        if (!await IsValidAssigneeAsync(connection, transaction, request.AssigneeUserId!.Value, cancellationToken))
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.AssigneeUserId)] = ["Pending 조치 권한이 있는 활성 사용자를 선택해 주세요."]
            });
        }

        var nextStatus = issue.Status == PendingStatuses.Registered ? PendingStatuses.ActionRequested : issue.Status;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issues
                set assignee_user_id = @assignee_user_id,
                    status = @status,
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now()
                where id = @id and version = @expected_version;
                """;
            command.Parameters.AddWithValue("assignee_user_id", request.AssigneeUserId.Value);
            command.Parameters.AddWithValue("status", nextStatus);
            command.Parameters.AddWithValue("actor_id", actor.UserId);
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("expected_version", request.ExpectedVersion!.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return PendingMutationResult<PendingDetailResponse>.Conflict("다른 사용자가 먼저 변경했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        await CancelOpenWorkItemsAsync(connection, transaction, pendingId, cancellationToken);
        var assignmentPeers = issue.ActionDepartmentCode is null
            ? new PendingAssigneePair(request.AssigneeUserId.Value, null)
            : await ResolveProjectActionAssigneesAsync(
                connection,
                transaction,
                issue.ProjectId,
                issue.ActionDepartmentCode,
                cancellationToken);
        await CreateAssignmentArtifactsAsync(
            connection,
            transaction,
            pendingId,
            issue.ProjectId,
            issue.Title,
            issue.Description,
            issue.Priority,
            issue.DueDate,
            request.AssigneeUserId.Value,
            assignmentPeers.SecondaryUserId == request.AssigneeUserId.Value ? null : assignmentPeers.SecondaryUserId,
            issue.ActionDepartmentCode,
            actor.UserId,
            issue.Version + 1,
            cancellationToken);
        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "AssigneeChanged",
            issue.Status,
            nextStatus,
            issue.AssigneeUserId,
            request.AssigneeUserId,
            reason,
            actor.UserId,
            correlationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await GetDetailAsync(pendingId, actor, cancellationToken);
        return detail is null
            ? PendingMutationResult<PendingDetailResponse>.NotFound()
            : PendingMutationResult<PendingDetailResponse>.Success(detail);
    }

    public async Task<PendingMutationResult<PendingDetailResponse>> AddCommentAsync(
        Guid pendingId,
        AddPendingCommentRequest request,
        PendingActor actor,
        CancellationToken cancellationToken)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
        {
            return PendingMutationResult<PendingDetailResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Body)] = ["코멘트를 1~2,000자로 입력해 주세요."]
            });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken);
        if (issue is null)
        {
            return PendingMutationResult<PendingDetailResponse>.NotFound();
        }
        var isInspectionPending = await IsQualityInspectionPendingAsync(connection, transaction, pendingId, cancellationToken);
        if (issue.Status == PendingStatuses.Closed || !CanParticipate(issue, actor, isInspectionPending))
        {
            return PendingMutationResult<PendingDetailResponse>.Forbidden();
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_comments (pending_issue_id, body, created_by_user_id)
                values (@pending_id, @body, @actor_id);

                update pending_issues
                set updated_at_utc = now(), updated_by_user_id = @actor_id
                where id = @pending_id;
                """;
            command.Parameters.AddWithValue("pending_id", pendingId);
            command.Parameters.AddWithValue("body", body);
            command.Parameters.AddWithValue("actor_id", actor.UserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        var detail = await GetDetailAsync(pendingId, actor, cancellationToken);
        return detail is null
            ? PendingMutationResult<PendingDetailResponse>.NotFound()
            : PendingMutationResult<PendingDetailResponse>.Success(detail);
    }

    public async Task<Guid> CreateOrReuseMaterialNonconformanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid procurementItemId,
        Guid materialReceiptId,
        string title,
        string description,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                select pi.id
                from pending_issues pi
                join material_iqc_attempts attempt on attempt.pending_issue_id = pi.id
                where attempt.material_receipt_id = @receipt_id
                  and pi.status <> 'Closed'
                order by pi.created_at_utc desc
                limit 1
                for update of pi;
                """;
            existingCommand.Parameters.AddWithValue("receipt_id", materialReceiptId);
            var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        const string actionDepartmentCode = "procurement";
        var automaticAssignees = await ResolveProjectActionAssigneesAsync(
            connection,
            transaction,
            projectId,
            actionDepartmentCode,
            cancellationToken);
        var pendingId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_issues (
                    id, project_id, target_type, target_id, issue_type, title, description,
                    status, priority, action_department_code, assignee_user_id, due_date, created_by_user_id,
                    updated_by_user_id
                )
                values (
                    @id, @project_id, 'ProcurementItem', @target_id, 'Nonconformance', @title, @description,
                    @status, 'Urgent', @action_department_code, @assignee_user_id, null, @actor_id, @actor_id
                );
                """;
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("target_id", procurementItemId);
            command.Parameters.AddWithValue("title", title.Trim());
            command.Parameters.AddWithValue("description", description.Trim());
            command.Parameters.AddWithValue("status", automaticAssignees.PrimaryUserId is null ? PendingStatuses.Registered : PendingStatuses.ActionRequested);
            command.Parameters.AddWithValue("action_department_code", actionDepartmentCode);
            AddNullableUuid(command, "assignee_user_id", automaticAssignees.PrimaryUserId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "Created",
            null,
            automaticAssignees.PrimaryUserId is null ? PendingStatuses.Registered : PendingStatuses.ActionRequested,
            null,
            automaticAssignees.PrimaryUserId,
            "IQC 부적합 자동 등록",
            actorUserId,
            correlationId,
            cancellationToken);
        if (automaticAssignees.PrimaryUserId is not null)
        {
            await CreateAssignmentArtifactsAsync(
                connection,
                transaction,
                pendingId,
                projectId,
                title,
                description,
                PendingPriorities.Urgent,
                null,
                automaticAssignees.PrimaryUserId.Value,
                automaticAssignees.SecondaryUserId,
                actionDepartmentCode,
                actorUserId,
                1,
                cancellationToken);
        }
        return pendingId;
    }

    public async Task<ManufacturingStopPendingResult> CreateManufacturingStopAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        string panelDisplayCode,
        string reasonCode,
        string description,
        string actionDepartmentCode,
        Guid? assigneeUserId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var automaticAssignees = assigneeUserId is null
            ? await ResolveProjectActionAssigneesAsync(connection, transaction, projectId, actionDepartmentCode, cancellationToken)
            : new PendingAssigneePair(assigneeUserId, null);
        assigneeUserId ??= automaticAssignees.PrimaryUserId;
        var pendingId = Guid.NewGuid();
        var status = assigneeUserId is null ? PendingStatuses.Registered : PendingStatuses.ActionRequested;
        var title = $"제조 중단 · {panelDisplayCode}";
        var pendingDescription = $"{StopReasonLabel(reasonCode)} · {description.Trim()}";
        long issueNumber;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_issues (
                    id, project_id, target_type, target_id, issue_type, title, description,
                    status, priority, action_department_code, assignee_user_id, due_date,
                    created_by_user_id, updated_by_user_id
                )
                values (
                    @id, @project_id, 'Panel', @panel_id, 'ManufacturingStop', @title, @description,
                    @status, 'Urgent', @action_department_code, @assignee_user_id, null,
                    @actor_id, @actor_id
                )
                returning issue_number;
                """;
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("panel_id", panelId);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", pendingDescription);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("action_department_code", actionDepartmentCode);
            AddNullableUuid(command, "assignee_user_id", assigneeUserId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            issueNumber = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "Created",
            null,
            status,
            null,
            assigneeUserId,
            "제조 중단 자동 등록",
            actorUserId,
            correlationId,
            cancellationToken);

        if (assigneeUserId is not null)
        {
            await CreateAssignmentArtifactsAsync(
                connection,
                transaction,
                pendingId,
                projectId,
                title,
                pendingDescription,
                PendingPriorities.Urgent,
                null,
                assigneeUserId.Value,
                automaticAssignees.SecondaryUserId,
                actionDepartmentCode,
                actorUserId,
                1,
                cancellationToken);
        }

        return new ManufacturingStopPendingResult(pendingId, issueNumber);
    }

    public async Task<PanelQualityPendingResult> CreatePanelQualityIssueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid panelId,
        string panelDisplayCode,
        string stageLabel,
        string issueType,
        string description,
        string actionDepartmentCode,
        Guid? assigneeUserId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (issueType is not (PendingIssueTypes.Nonconformance or PendingIssueTypes.Punch))
        {
            throw new InvalidOperationException("지원하지 않는 품질 Pending 유형입니다.");
        }

        var automaticAssignees = assigneeUserId is null
            ? await ResolveProjectActionAssigneesAsync(connection, transaction, projectId, actionDepartmentCode, cancellationToken)
            : new PendingAssigneePair(assigneeUserId, null);
        assigneeUserId ??= automaticAssignees.PrimaryUserId;
        var pendingId = Guid.NewGuid();
        var status = assigneeUserId is null ? PendingStatuses.Registered : PendingStatuses.ActionRequested;
        var title = $"{stageLabel} {(issueType == PendingIssueTypes.Punch ? "PUNCH" : "부적합")} · {panelDisplayCode}";
        var pendingDescription = $"{stageLabel} 검사 결과 · {description.Trim()}";
        long issueNumber;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_issues (
                    id, project_id, target_type, target_id, issue_type, title, description,
                    status, priority, action_department_code, assignee_user_id, due_date,
                    created_by_user_id, updated_by_user_id
                )
                values (
                    @id, @project_id, 'Panel', @panel_id, @issue_type, @title, @description,
                    @status, 'Urgent', @action_department_code, @assignee_user_id, null,
                    @actor_id, @actor_id
                )
                returning issue_number;
                """;
            command.Parameters.AddWithValue("id", pendingId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("panel_id", panelId);
            command.Parameters.AddWithValue("issue_type", issueType);
            command.Parameters.AddWithValue("title", title);
            command.Parameters.AddWithValue("description", pendingDescription);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("action_department_code", actionDepartmentCode);
            AddNullableUuid(command, "assignee_user_id", assigneeUserId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            issueNumber = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "Created",
            null,
            status,
            null,
            assigneeUserId,
            $"{stageLabel} 판정 자동 등록",
            actorUserId,
            correlationId,
            cancellationToken);

        if (assigneeUserId is not null)
        {
            await CreateAssignmentArtifactsAsync(
                connection,
                transaction,
                pendingId,
                projectId,
                title,
                pendingDescription,
                PendingPriorities.Urgent,
                null,
                assigneeUserId.Value,
                automaticAssignees.SecondaryUserId,
                actionDepartmentCode,
                actorUserId,
                1,
                cancellationToken);
        }

        return new PanelQualityPendingResult(pendingId, issueNumber);
    }

    public static async Task<string?> ReadMaterialNonconformanceStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select status from pending_issues where id = @id for update;";
        command.Parameters.AddWithValue("id", pendingId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task CloseMaterialNonconformanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid actorUserId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken)
            ?? throw new InvalidOperationException("연결된 Pending을 찾을 수 없습니다.");
        if (!string.Equals(issue.Status, PendingStatuses.ReinspectionRequested, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("재검사 요청 상태의 Pending만 IQC 합격으로 종결할 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issues
                set status = 'Closed',
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now(),
                    closed_by_user_id = @actor_id,
                    closed_at_utc = now()
                where id = @id;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("id", pendingId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SyncWorkItemStatusAsync(connection, transaction, pendingId, PendingStatuses.Closed, cancellationToken);
        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "StatusChanged",
            issue.Status,
            PendingStatuses.Closed,
            issue.AssigneeUserId,
            issue.AssigneeUserId,
            reason.Trim(),
            actorUserId,
            correlationId,
            cancellationToken);
    }

    public async Task ClosePanelQualityIssueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid actorUserId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken)
            ?? throw new InvalidOperationException("연결된 Pending을 찾을 수 없습니다.");
        if (!string.Equals(issue.Status, PendingStatuses.ReinspectionRequested, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("재검사 요청 상태의 Pending만 품질검사 합격으로 종결할 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issues
                set status = 'Closed',
                    version = version + 1,
                    updated_by_user_id = @actor_id,
                    updated_at_utc = now(),
                    closed_by_user_id = @actor_id,
                    closed_at_utc = now()
                where id = @id;
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("id", pendingId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SyncWorkItemStatusAsync(connection, transaction, pendingId, PendingStatuses.Closed, cancellationToken);
        await InsertHistoryAsync(
            connection,
            transaction,
            pendingId,
            "StatusChanged",
            issue.Status,
            PendingStatuses.Closed,
            issue.AssigneeUserId,
            issue.AssigneeUserId,
            reason.Trim(),
            actorUserId,
            correlationId,
            cancellationToken);
    }

    public async Task ReopenQualityIssueAfterFailedReinspectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid actorUserId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var issue = await ReadIssueForUpdateAsync(connection, transaction, pendingId, cancellationToken)
            ?? throw new InvalidOperationException("연결된 Pending을 찾을 수 없습니다.");
        if (!string.Equals(issue.Status, PendingStatuses.ReinspectionRequested, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("재검사 요청 상태의 Pending만 재조치 상태로 되돌릴 수 있습니다.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issues
                set status = 'InProgress', version = version + 1,
                    updated_by_user_id = @actor_id, updated_at_utc = now(),
                    closed_by_user_id = null, closed_at_utc = null
                where id = @id;
                update work_items
                set status = 'InProgress', started_at_utc = coalesce(started_at_utc, now()),
                    completed_at_utc = null, cancelled_at_utc = null
                where target_type = 'Pending' and target_id = @id
                  and status <> 'Cancelled';
                """;
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("id", pendingId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertHistoryAsync(
            connection, transaction, pendingId, "StatusChanged",
            issue.Status, PendingStatuses.InProgress,
            issue.AssigneeUserId, issue.AssigneeUserId,
            reason.Trim(), actorUserId, correlationId, cancellationToken);
        await NotifyActionAssigneesOfFailedReinspectionAsync(
            connection,
            transaction,
            issue,
            reason.Trim(),
            issue.Version + 1,
            cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateCreate(CreatePendingRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ProjectId is null)
        {
            errors[nameof(request.ProjectId)] = ["프로젝트를 선택해 주세요."];
        }
        if (string.IsNullOrWhiteSpace(request.IssueType) || request.IssueType.Trim().Length > 80)
        {
            errors[nameof(request.IssueType)] = ["Pending 유형을 선택해 주세요."];
        }
        var title = request.Title?.Trim() ?? "";
        if (title.Length is < 3 or > 160)
        {
            errors[nameof(request.Title)] = ["제목을 3~160자로 입력해 주세요."];
        }
        var description = request.Description?.Trim() ?? "";
        if (description.Length is < 10 or > 2000)
        {
            errors[nameof(request.Description)] = ["상세 내용을 10~2,000자로 입력해 주세요."];
        }
        if (request.Priority is null || !PendingPriorities.All.Contains(request.Priority.Trim()))
        {
            errors[nameof(request.Priority)] = ["긴급도를 선택해 주세요."];
        }
        if (string.Equals(request.IssueType?.Trim(), PendingIssueTypes.ManufacturingStop, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(request.ActionDepartmentCode))
        {
            errors[nameof(request.ActionDepartmentCode)] = ["제조 중단의 조치 담당 부서를 선택해 주세요."];
        }
        return errors;
    }

    private static async Task<PendingSummaryResponse> ReadSummaryAsync(
        NpgsqlDataSource dataSource,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            select
                count(*) filter (where status <> 'Closed')::int,
                count(*) filter (where status <> 'Closed' and priority = 'Urgent')::int,
                count(*) filter (where status <> 'Closed' and due_date < current_date)::int,
                count(*) filter (where status = 'ReinspectionRequested')::int,
                count(*) filter (where status = 'Closed')::int
            from pending_issues
            where (@project_id is null or project_id = @project_id);
            """);
        AddNullableUuid(command, "project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PendingSummaryResponse(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4))
            : new PendingSummaryResponse(0, 0, 0, 0, 0);
    }

    private static async Task<PendingListItemResponse?> ReadIssueAsync(
        NpgsqlConnection connection,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                pi.id,
                pi.issue_number,
                pi.project_id,
                p.project_code,
                p.project_title,
                pi.target_type,
                pi.target_id,
                case when pi.target_type = 'Panel' then panel.display_code else null end,
                pi.issue_type,
                issue_type.display_name,
                pi.title,
                pi.description,
                pi.status,
                pi.priority,
                pi.action_department_code,
                pi.assignee_user_id,
                assignee.display_name,
                pi.due_date,
                pi.version,
                pi.created_by_user_id,
                creator.display_name,
                pi.created_at_utc,
                pi.updated_at_utc
            from pending_issues pi
            join projects p on p.id = pi.project_id and p.deleted_at_utc is null
            join qms_users creator on creator.id = pi.created_by_user_id
            join pending_issue_type_catalog issue_type on issue_type.code = pi.issue_type
            left join qms_users assignee on assignee.id = pi.assignee_user_id
            left join panel_placeholders panel on panel.id = pi.target_id and pi.target_type = 'Panel'
            where pi.id = @id;
            """;
        command.Parameters.AddWithValue("id", pendingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadIssue(reader) : null;
    }

    private static PendingListItemResponse ReadIssue(NpgsqlDataReader reader)
    {
        var status = reader.GetString(12);
        var priority = reader.GetString(13);
        var dueDate = reader.IsDBNull(17) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(17);
        return new PendingListItemResponse(
            reader.GetGuid(0),
            reader.GetInt64(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            status,
            StatusLabel(status),
            priority,
            PriorityLabel(priority),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            dueDate,
            status != PendingStatuses.Closed && dueDate is not null && dueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow),
            reader.GetInt32(18),
            reader.GetGuid(19),
            reader.GetString(20),
            reader.GetFieldValue<DateTimeOffset>(21),
            reader.GetFieldValue<DateTimeOffset>(22));
    }

    private static async Task<IReadOnlyList<PendingCommentResponse>> ReadCommentsAsync(
        NpgsqlConnection connection,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select pc.id, pc.body, pc.created_by_user_id, u.display_name, pc.created_at_utc
            from pending_comments pc
            join qms_users u on u.id = pc.created_by_user_id
            where pc.pending_issue_id = @id
            order by pc.created_at_utc, pc.id;
            """;
        command.Parameters.AddWithValue("id", pendingId);
        var rows = new List<PendingCommentResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PendingCommentResponse(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<PendingHistoryResponse>> ReadHistoryAsync(
        NpgsqlConnection connection,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                ph.id, ph.event_type, ph.from_status, ph.to_status,
                from_user.display_name, to_user.display_name, ph.reason,
                ph.changed_by_user_id, changed_user.display_name, ph.created_at_utc
            from pending_history ph
            join qms_users changed_user on changed_user.id = ph.changed_by_user_id
            left join qms_users from_user on from_user.id = ph.from_assignee_user_id
            left join qms_users to_user on to_user.id = ph.to_assignee_user_id
            where ph.pending_issue_id = @id
            order by ph.created_at_utc, ph.id;
            """;
        command.Parameters.AddWithValue("id", pendingId);
        var rows = new List<PendingHistoryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fromStatus = reader.IsDBNull(2) ? null : reader.GetString(2);
            var toStatus = reader.IsDBNull(3) ? null : reader.GetString(3);
            var eventType = reader.GetString(1);
            rows.Add(new PendingHistoryResponse(
                reader.GetGuid(0),
                eventType,
                EventLabel(eventType),
                fromStatus,
                fromStatus is null ? null : StatusLabel(fromStatus),
                toStatus,
                toStatus is null ? null : StatusLabel(toStatus),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetGuid(7),
                reader.GetString(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }
        return rows;
    }

    private static PendingDetailResponse BuildDetail(
        PendingListItemResponse issue,
        IReadOnlyList<PendingCommentResponse> comments,
        IReadOnlyList<PendingHistoryResponse> history,
        PendingActor actor,
        bool isInspectionPending,
        PendingReinspectionResponse? reinspection)
    {
        var allowed = AllowedTransitions(issue, actor)
            .Where(status => !isInspectionPending || status != PendingStatuses.Closed)
            .ToList();
        return new PendingDetailResponse(
            issue,
            comments,
            history,
            allowed,
            issue.Status != PendingStatuses.Closed && CanParticipate(issue, actor, isInspectionPending),
            issue.Status != PendingStatuses.Closed && actor.IsCoordinator,
            reinspection);
    }

    private static async Task<PendingReinspectionResponse?> ReadPendingReinspectionAsync(
        NpgsqlConnection connection,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select attempt.id, attempt.attempt_number, item.order_item, receipt.quantity, receipt.unit
            from material_iqc_attempts attempt
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            join project_procurement_items item on item.id = receipt.procurement_item_id
            where attempt.pending_issue_id = @pending_id
              and attempt.status = 'Requested'
            order by attempt.attempt_number desc, attempt.requested_at_utc desc
            limit 1;
            """;
        command.Parameters.AddWithValue("pending_id", pendingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var attemptId = reader.GetGuid(0);
        return new PendingReinspectionResponse(
            attemptId,
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            $"/quality/iqc?request={attemptId}");
    }

    private static IReadOnlyList<string> AllowedTransitions(PendingListItemResponse issue, PendingActor actor)
    {
        if (!NextStatuses.TryGetValue(issue.Status, out var next) || !CanTransition(issue, actor, next))
        {
            return [];
        }
        if (next == PendingStatuses.ActionRequested && issue.AssigneeUserId is null)
        {
            return [];
        }
        return [next];
    }

    private static bool CanTransition(PendingListItemResponse issue, PendingActor actor, string toStatus)
    {
        if (actor.IsCoordinator)
        {
            return true;
        }
        return toStatus switch
        {
            PendingStatuses.ActionRequested => issue.CreatedByUserId == actor.UserId,
            PendingStatuses.InProgress => issue.AssigneeUserId == actor.UserId,
            PendingStatuses.ReinspectionRequested => issue.AssigneeUserId == actor.UserId,
            PendingStatuses.Closed => issue.CreatedByUserId == actor.UserId,
            _ => false
        };
    }

    private static bool CanParticipate(PendingListItemResponse issue, PendingActor actor, bool isInspectionPending = false)
    {
        return actor.IsCoordinator
            || (isInspectionPending && actor.IsQuality)
            || issue.CreatedByUserId == actor.UserId
            || issue.AssigneeUserId == actor.UserId;
    }

    private static async Task<PendingListItemResponse?> ReadIssueForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                pi.id,
                pi.issue_number,
                pi.project_id,
                p.project_code,
                p.project_title,
                pi.target_type,
                pi.target_id,
                case when pi.target_type = 'Panel' then panel.display_code else null end,
                pi.issue_type,
                issue_type.display_name,
                pi.title,
                pi.description,
                pi.status,
                pi.priority,
                pi.action_department_code,
                pi.assignee_user_id,
                assignee.display_name,
                pi.due_date,
                pi.version,
                pi.created_by_user_id,
                creator.display_name,
                pi.created_at_utc,
                pi.updated_at_utc
            from pending_issues pi
            join projects p on p.id = pi.project_id and p.deleted_at_utc is null
            join qms_users creator on creator.id = pi.created_by_user_id
            join pending_issue_type_catalog issue_type on issue_type.code = pi.issue_type
            left join qms_users assignee on assignee.id = pi.assignee_user_id
            left join panel_placeholders panel on panel.id = pi.target_id and pi.target_type = 'Panel'
            where pi.id = @id
            for update of pi;
            """;
        command.Parameters.AddWithValue("id", pendingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadIssue(reader) : null;
    }

    private static async Task<string?> LockProjectForPendingCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select status from projects where id = @id and deleted_at_utc is null for update;";
        command.Parameters.AddWithValue("id", projectId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<bool> IsValidAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from qms_users u
                join user_roles ur on ur.user_id = u.id
                join role_permissions rp on rp.role_id = ur.role_id
                join permissions p on p.id = rp.permission_id
                where u.id = @id
                  and u.is_active = true
                  and p.code = 'Pending.Manage'
            );
            """;
        command.Parameters.AddWithValue("id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsAvailableManualTypeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string issueType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1 from pending_issue_type_catalog
                where code=@code and is_active and is_manual_enabled
            );
            """;
        command.Parameters.AddWithValue("code", issueType);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsActiveDepartmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists (select 1 from departments where code = @code and is_active = true);";
        command.Parameters.AddWithValue("code", departmentCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsQualityInspectionPendingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from panel_quality_inspection_attempts
                where linked_pending_issue_id = @pending_id
                  and status = 'Failed'
                union all
                select 1
                from material_iqc_attempts
                where pending_issue_id = @pending_id
                  and status = 'Failed'
            );
            """;
        command.Parameters.AddWithValue("pending_id", pendingId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsValidAssigneeInDepartmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from qms_users users
                join departments department on department.id = users.department_id
                join user_roles user_role on user_role.user_id = users.id
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where users.id = @user_id
                  and users.is_active = true
                  and department.code = @department_code
                  and permission.code = 'Pending.Manage'
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("department_code", departmentCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<PendingAssigneePair> ResolveProjectActionAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        var (primaryTypes, secondaryTypes) = PendingResponsibilityTypes(departmentCode);
        var primaryUserId = await ReadProjectPendingAssigneeAsync(
            connection,
            transaction,
            projectId,
            primaryTypes,
            cancellationToken);
        primaryUserId ??= await ReadDepartmentPendingAssigneeAsync(
            connection,
            transaction,
            departmentCode,
            cancellationToken);
        var secondaryUserId = await ReadProjectPendingAssigneeAsync(
            connection,
            transaction,
            projectId,
            secondaryTypes,
            cancellationToken);
        return new PendingAssigneePair(primaryUserId, secondaryUserId == primaryUserId ? null : secondaryUserId);
    }

    private static async Task<Guid?> ReadProjectPendingAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<string> responsibilityTypes,
        CancellationToken cancellationToken)
    {
        if (responsibilityTypes.Count == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pa.assigned_user_id
            from project_assignees pa
            join qms_users users on users.id = pa.assigned_user_id and users.is_active = true
            where pa.project_id = @project_id
              and pa.responsibility_type = any(@responsibility_types)
              and exists (
                  select 1
                  from user_roles user_role
                  join role_permissions role_permission on role_permission.role_id = user_role.role_id
                  join permissions permission on permission.id = role_permission.permission_id
                  where user_role.user_id = users.id
                    and permission.code = 'Pending.Manage'
              )
            order by array_position(@responsibility_types, pa.responsibility_type)
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_types", responsibilityTypes.ToArray());
        return (Guid?)(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<Guid?> ReadDepartmentPendingAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select users.id
            from qms_users users
            join departments department on department.id = users.department_id
            where users.is_active = true
              and department.code = @department_code
              and exists (
                  select 1
                  from user_roles user_role
                  join role_permissions role_permission on role_permission.role_id = user_role.role_id
                  join permissions permission on permission.id = role_permission.permission_id
                  where user_role.user_id = users.id
                    and permission.code = 'Pending.Manage'
              )
            order by users.display_name
            limit 1;
            """;
        command.Parameters.AddWithValue("department_code", departmentCode);
        return (Guid?)(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static (IReadOnlyList<string> Primary, IReadOnlyList<string> Secondary) PendingResponsibilityTypes(string departmentCode)
    {
        return departmentCode switch
        {
            "sales" => (["SalesPrimary"], ["SalesSecondary"]),
            "design" => (["DesignPrimary"], ["DesignSecondary"]),
            "production-planning" => (["ProductionPlanningPrimary", "ProductionPlanning"], ["ProductionPlanningSecondary"]),
            "procurement" => (["ProcurementPrimary", "Procurement"], ["ProcurementSecondary"]),
            "materials" => (["MaterialsPrimary"], ["MaterialsSecondary"]),
            "manufacturing" => (["ManufacturingPrimary", "Manufacturing"], ["ManufacturingSecondary"]),
            "logistics" => (["LogisticsPrimary", "Logistics"], ["LogisticsSecondary"]),
            "quality" => (
                ["QualityIQC", "QualityLQC", "QualityOQC", "QualityCustomerInspection", "Quality"],
                ["QualityIQCSecondary", "QualityLQCSecondary", "QualityOQCSecondary", "QualityCustomerInspectionSecondary"]),
            _ => ([], [])
        };
    }

    private static async Task CreateAssignmentArtifactsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        Guid projectId,
        string title,
        string description,
        string priority,
        DateOnly? dueDate,
        Guid assigneeUserId,
        Guid? secondaryUserId,
        string? actionDepartmentCode,
        Guid createdByUserId,
        int version,
        CancellationToken cancellationToken)
    {
        _ = actionDepartmentCode;
        var stageCode = await ReadCurrentWorkflowStageCodeAsync(connection, transaction, projectId, cancellationToken);
        var workItemId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into work_items (
                    id, project_id, target_type, target_id, workflow_stage_code,
                    responsibility_type, assigned_user_id, title, description, status,
                    priority, due_date, idempotency_key, created_by_user_id
                )
                values (
                    @id, @project_id, 'Pending', @pending_id, @stage_code,
                    'PendingAction', @assignee_user_id, @title, @description, 'Requested',
                    @priority, @due_date, @idempotency_key, @created_by_user_id
                )
                on conflict (idempotency_key) do update set title = excluded.title
                returning id;
                """;
            command.Parameters.AddWithValue("id", workItemId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("pending_id", pendingId);
            command.Parameters.AddWithValue("stage_code", stageCode);
            command.Parameters.AddWithValue("assignee_user_id", assigneeUserId);
            command.Parameters.AddWithValue("title", $"Pending 조치 · {title}");
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("priority", priority == PendingPriorities.Urgent ? "Blocking" : "Normal");
            AddNullableDate(command, "due_date", dueDate);
            command.Parameters.AddWithValue("idempotency_key", $"pending:{pendingId}:assignment:{assigneeUserId}:v{version}");
            command.Parameters.AddWithValue("created_by_user_id", createdByUserId);
            workItemId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? workItemId);
        }

        var notificationId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    id, project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind, work_item_id
                )
                values (
                    @id, @project_id, @notification_type, @severity, @title, @message, @link_url,
                    @idempotency_key, 'RecipientOnly', 'PendingAssignment', @work_item_id
                )
                on conflict (idempotency_key) do update
                set title = excluded.title,
                    message = excluded.message,
                    work_item_id = excluded.work_item_id
                returning id;
                """;
            command.Parameters.AddWithValue("id", notificationId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("notification_type", priority == PendingPriorities.Urgent ? "Blocking" : "Info");
            command.Parameters.AddWithValue("severity", priority == PendingPriorities.Urgent ? "Critical" : "Info");
            command.Parameters.AddWithValue("title", priority == PendingPriorities.Urgent ? $"긴급 Pending · {title}" : $"Pending 조치 요청 · {title}");
            command.Parameters.AddWithValue("message", description.Length > 200 ? description[..200] : description);
            command.Parameters.AddWithValue("link_url", $"/pending/{pendingId}");
            command.Parameters.AddWithValue("idempotency_key", $"pending:{pendingId}:notification:{assigneeUserId}:v{version}");
            command.Parameters.AddWithValue("work_item_id", workItemId);
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? notificationId);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                select @notification_id, @user_id
                where exists (select 1 from notifications where id = @notification_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            command.Parameters.AddWithValue("notification_id", notificationId);
            command.Parameters.AddWithValue("user_id", assigneeUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (secondaryUserId is not null && secondaryUserId != assigneeUserId)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                select @notification_id, @user_id
                where exists (select 1 from notifications where id = @notification_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            command.Parameters.AddWithValue("notification_id", notificationId);
            command.Parameters.AddWithValue("user_id", secondaryUserId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task NotifyActionAssigneesOfFailedReinspectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PendingListItemResponse issue,
        string reason,
        int version,
        CancellationToken cancellationToken)
    {
        var resolvedAssignees = !string.IsNullOrWhiteSpace(issue.ActionDepartmentCode)
            ? await ResolveProjectActionAssigneesAsync(
                connection,
                transaction,
                issue.ProjectId,
                issue.ActionDepartmentCode,
                cancellationToken)
            : new PendingAssigneePair(null, null);
        var primaryUserId = issue.AssigneeUserId ?? resolvedAssignees.PrimaryUserId;
        if (primaryUserId is null)
        {
            return;
        }

        Guid? workItemId;
        await using (var workItemCommand = connection.CreateCommand())
        {
            workItemCommand.Transaction = transaction;
            workItemCommand.CommandText = """
                select id
                from work_items
                where target_type = 'Pending'
                  and target_id = @pending_id
                  and status = 'InProgress'
                order by created_at_utc desc, id
                limit 1;
                """;
            workItemCommand.Parameters.AddWithValue("pending_id", issue.PendingId);
            workItemId = (Guid?)(await workItemCommand.ExecuteScalarAsync(cancellationToken));
        }

        var notificationId = Guid.NewGuid();
        await using (var notificationCommand = connection.CreateCommand())
        {
            notificationCommand.Transaction = transaction;
            notificationCommand.CommandText = """
                insert into notifications (
                    id, project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind, work_item_id
                )
                values (
                    @id, @project_id, @notification_type, @severity, @title, @message, @link_url,
                    @idempotency_key, 'RecipientOnly', 'PendingAssignment', @work_item_id
                )
                on conflict (idempotency_key) do update
                set title = excluded.title,
                    message = excluded.message,
                    work_item_id = excluded.work_item_id
                returning id;
                """;
            notificationCommand.Parameters.AddWithValue("id", notificationId);
            notificationCommand.Parameters.AddWithValue("project_id", issue.ProjectId);
            notificationCommand.Parameters.AddWithValue("notification_type", issue.Priority == PendingPriorities.Urgent ? "Blocking" : "Info");
            notificationCommand.Parameters.AddWithValue("severity", issue.Priority == PendingPriorities.Urgent ? "Critical" : "Warning");
            notificationCommand.Parameters.AddWithValue("title", $"재조치 필요 · {issue.Title}");
            notificationCommand.Parameters.AddWithValue("message", reason.Length > 200 ? reason[..200] : reason);
            notificationCommand.Parameters.AddWithValue("link_url", $"/pending/{issue.PendingId}");
            notificationCommand.Parameters.AddWithValue("idempotency_key", $"pending:{issue.PendingId}:reopened:v{version}");
            notificationCommand.Parameters.AddWithValue("work_item_id", (object?)workItemId ?? DBNull.Value);
            notificationId = (Guid)(await notificationCommand.ExecuteScalarAsync(cancellationToken) ?? notificationId);
        }

        var recipients = new HashSet<Guid> { primaryUserId.Value };
        if (resolvedAssignees.SecondaryUserId is not null)
        {
            recipients.Add(resolvedAssignees.SecondaryUserId.Value);
        }
        foreach (var recipientId in recipients)
        {
            await using var recipientCommand = connection.CreateCommand();
            recipientCommand.Transaction = transaction;
            recipientCommand.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            recipientCommand.Parameters.AddWithValue("notification_id", notificationId);
            recipientCommand.Parameters.AddWithValue("user_id", recipientId);
            await recipientCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<string> ReadCurrentWorkflowStageCodeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select ws.stage_code
            from workflow_stages ws
            where ws.is_active = true
              and not exists (
                  select 1
                  from project_workflow_events event
                  where event.project_id = @project_id
                    and event.stage_code = ws.stage_code
                    and event.event_type = 'StageCompleted'
                    and event.event_status = 'Succeeded'
              )
            order by ws.sequence_number
            limit 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "SalesProjectCreated";
    }

    private static async Task CancelOpenWorkItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items
            set status = 'Cancelled', cancelled_at_utc = now()
            where target_type = 'Pending'
              and target_id = @pending_id
              and status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("pending_id", pendingId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SyncWorkItemStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        string pendingStatus,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = pendingStatus switch
        {
            PendingStatuses.InProgress => """
                update work_items
                set status = 'InProgress', started_at_utc = coalesce(started_at_utc, now())
                where target_type = 'Pending' and target_id = @pending_id and status = 'Requested';
                """,
            PendingStatuses.Closed => """
                update work_items
                set status = 'Completed', completed_at_utc = coalesce(completed_at_utc, now())
                where target_type = 'Pending' and target_id = @pending_id and status in ('Requested', 'InProgress');
                """,
            PendingStatuses.ReinspectionRequested => """
                update work_items
                set status = 'Completed', completed_at_utc = coalesce(completed_at_utc, now())
                where target_type = 'Pending' and target_id = @pending_id and status in ('Requested', 'InProgress');
                """,
            _ => "select 1;"
        };
        command.Parameters.AddWithValue("pending_id", pendingId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid pendingId,
        string eventType,
        string? fromStatus,
        string? toStatus,
        Guid? fromAssigneeUserId,
        Guid? toAssigneeUserId,
        string? reason,
        Guid changedByUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pending_history (
                pending_issue_id, event_type, from_status, to_status,
                from_assignee_user_id, to_assignee_user_id, reason,
                changed_by_user_id, correlation_id
            )
            values (
                @pending_id, @event_type, @from_status, @to_status,
                @from_assignee_user_id, @to_assignee_user_id, @reason,
                @changed_by_user_id, @correlation_id
            );
            """;
        command.Parameters.AddWithValue("pending_id", pendingId);
        command.Parameters.AddWithValue("event_type", eventType);
        AddNullableText(command, "from_status", fromStatus);
        AddNullableText(command, "to_status", toStatus);
        AddNullableUuid(command, "from_assignee_user_id", fromAssigneeUserId);
        AddNullableUuid(command, "to_assignee_user_id", toAssigneeUserId);
        AddNullableText(command, "reason", reason);
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
        AddNullableText(command, "correlation_id", correlationId);
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

    private sealed record PendingAssigneePair(Guid? PrimaryUserId, Guid? SecondaryUserId);

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;
    }

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = value ?? (object)DBNull.Value;
    }

    private static void AddNullableDate(NpgsqlCommand command, string name, DateOnly? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Date).Value = value ?? (object)DBNull.Value;
    }

    private static string StatusLabel(string value) => value switch
    {
        PendingStatuses.Registered => "등록",
        PendingStatuses.ActionRequested => "조치 요청",
        PendingStatuses.InProgress => "조치 중",
        PendingStatuses.ReinspectionRequested => "재검사 요청",
        PendingStatuses.Closed => "종결",
        _ => value
    };

    private static string PriorityLabel(string value) => value == PendingPriorities.Urgent ? "긴급" : "일반";

    private static string StopReasonLabel(string value) => value switch
    {
        "Material" => "자재 문제",
        "Staffing" => "인원 문제",
        "WorkUnavailable" => "작업 불가",
        _ => "기타"
    };

    private static string EventLabel(string value) => value switch
    {
        "Created" => "Pending 등록",
        "StatusChanged" => "상태 변경",
        "AssigneeChanged" => "담당 변경",
        _ => value
    };
}
