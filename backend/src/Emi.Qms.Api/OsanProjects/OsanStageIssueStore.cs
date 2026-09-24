using Emi.Qms.Api.Notifications;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed partial class OsanProgressStore
{
    public async Task<OsanProgressMutationResult> RecordIssueAsync(Guid projectId, CompleteOsanProgressInput input,
        Guid actor, bool resolve, CancellationToken ct, bool isAdministrator = false, bool requireOpen = false)
    {
        var errors = ValidateCompletion(input);
        if (input.CompletionMode != OsanCompletionModes.Individual || input.Targets.Count != 1)
            errors["targets"] = ["이상 기록은 대상 하나를 선택해 주세요."];
        if (string.IsNullOrWhiteSpace(input.Comment) || input.Comment.Length > 1000)
            errors["comment"] = ["코멘트를 1~1000자로 입력해 주세요."];
        if (input.RetainedPhotoIds?.Count > 0) errors["retainedPhotoIds"] = ["새 사진을 등록해 주세요."];
        if (resolve) foreach (var error in OsanStageRecords.ValidateContent(input, isAdministrator)) errors[error.Key] = error.Value;
        if (errors.Count > 0) return OsanProgressMutationResult.Validation(errors);
        var target = input.Targets[0]!;
        var action = resolve ? "IssueResolved" : "IssueRegistered";
        var fingerprint = Fingerprint(new { projectId, actor, action, requireOpen, input.StageSequence, target, input.Comment,
            Photos = input.Photos.Select(p => new { p.FileName, p.NormalizedMime, p.Sha256 }) });
        await using var source = CreateDataSource();
        await using var c = await source.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            if (await LockProjectAsync(c, tx, projectId, ct) is null) return OsanProgressMutationResult.NotFound();
            var replay = await ReadOperationAsync(c, tx, input.OperationId, ct);
            if (replay is not null)
            {
                if (!MatchesOperation(replay, projectId, action, fingerprint, actor))
                    return OsanProgressMutationResult.Conflict("osan_progress_operation_conflict", "같은 요청 식별자가 다른 입력에 사용되었습니다.");
                return OsanProgressMutationResult.Success(new(input.OperationId, true, (await ReadProgressAsync(c, tx, projectId, ct))!));
            }
            if (resolve && !await OsanPolicyStore.CanCompleteAsync(c, tx, actor,
                    input.StageSequence, isAdministrator, ct))
                return OsanProgressMutationResult.Conflict("osan_gate_department_denied",
                    "이 Gate의 이상 조치를 완료할 수 있는 부서로 지정되지 않았습니다.");
            await using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("project", projectId); cmd.Parameters.AddWithValue("target", target.TargetId);
            cmd.Parameters.AddWithValue("stage", input.StageSequence); cmd.Parameters.AddWithValue("actor", actor);
            cmd.Parameters.AddWithValue("operation", input.OperationId);
            cmd.CommandText = "select 1 from osan_stage_records where operation_id=@operation";
            if (await cmd.ExecuteScalarAsync(ct) is not null)
                return OsanProgressMutationResult.Conflict("osan_progress_operation_conflict", "같은 요청 식별자가 다른 처리에 사용되었습니다.");
            var snapshots = await LockTargetsAsync(c, tx, projectId, [target.TargetId], ct);
            if (!snapshots.TryGetValue(target.TargetId, out var snapshot)) return OsanProgressMutationResult.NotFound();
            if (snapshot.Version != target.ExpectedVersion)
                return OsanProgressMutationResult.Conflict("osan_progress_stale_version", "진행 상태가 변경되었습니다. 다시 조회해 주세요.");
            var steps = await LockStepsAsync(c, tx, projectId, [target.TargetId], ct);
            if (steps.Count != 7) return OsanProgressMutationResult.Conflict("osan_progress_snapshot_invalid", "대상의 7단계 진행 기록을 확인할 수 없습니다.");
            if (steps.Any(s => s.SequenceNumber == 7 && s.Status == "Completed"))
                return OsanProgressMutationResult.Conflict("osan_issue_target_packaged", "포장 완료 대상에는 이상을 등록하거나 처리할 수 없습니다.");
            var step = steps.Single(s => s.SequenceNumber == input.StageSequence);
            cmd.Parameters.AddWithValue("step", step.StepId);
            cmd.CommandText = "select id from osan_stage_issues where step_id=@step and status='Open'";
            var issueId = (Guid?)await cmd.ExecuteScalarAsync(ct);
            if ((resolve || requireOpen) && issueId is null)
                return OsanProgressMutationResult.Conflict("osan_issue_not_open", "처리할 미해결 이상이 없습니다.");
            if (resolve && !PrerequisitesMet(input.StageSequence,
                    steps.Select(s => s.StepId == step.StepId ? s with { HasOpenIssue = false } : s).ToArray()))
                return OsanProgressMutationResult.Conflict(
                    input.StageSequence == 7 ? "osan_progress_packing_prerequisite_incomplete" : "osan_progress_prerequisite_incomplete",
                    input.StageSequence == 7 ? "포장 전 6단계 완료와 모든 이상 해결이 필요합니다." : "선행 Gate 완료 또는 공정 이상 발생 등록이 필요합니다.");
            await InsertOperationAsync(c, tx, input.OperationId, projectId, action, input.CompletionMode,
                input.StageSequence, [target.TargetId], fingerprint, actor, ct);
            var photos = new List<Guid>();
            for (var index = 0; index < input.Photos.Count; index++)
            {
                var photo = await InsertPhotoAsync(c, tx, projectId, input.OperationId, input.Photos[index], index + 1, actor, ct);
                photos.Add(photo);
                await LinkPhotoAsync(c, tx, projectId, target.TargetId, step.StepId, photo, input.StageSequence, ct);
            }
            var record = await OsanStageRecords.AddAsync(c, tx, projectId, step.StepId, input.OperationId,
                resolve ? "IssueResolved" : issueId is null ? "IssueRegistered" : "IssueRecorded", actor, input.Comment, null, photos.ToArray(), ct, fingerprint);
            cmd.Parameters.AddWithValue("record", record);
            if (resolve)
            {
                cmd.CommandText = """
                    update osan_stage_issues set status='Resolved',closed_at_utc=now(),closed_by_user_id=@actor,
                      closed_record_id=@record where step_id=@step and status='Open';
                    update osan_project_target_steps set started_at_utc=coalesce(started_at_utc,now()) where id=@step;
                    """;
            }
            else
            {
                cmd.Parameters.AddWithValue("issue", issueId ?? Guid.NewGuid());
                cmd.CommandText = """
                    insert into osan_stage_issues(id,project_id,target_id,step_id,registered_by_user_id,latest_record_id)
                    values(@issue,@project,@target,@step,@actor,@record)
                    on conflict(step_id) where status='Open' do update set latest_record_id=excluded.latest_record_id;
                    update osan_project_target_steps set status='NotStarted',completed_at_utc=null,completed_by_user_id=null,
                      rejected=false,updated_at_utc=now() where id=@step;
                    """;
            }
            await cmd.ExecuteNonQueryAsync(ct);
            cmd.CommandText = "update osan_photo_edit_requests set invalidated_at=now() where step_id=@step and used_at is null and invalidated_at is null";
            await cmd.ExecuteNonQueryAsync(ct);
            await OsanStageRecords.RecalculateAsync(c, tx, projectId, target.TargetId, ct);
            if (resolve || issueId is null) await OsanStageRecords.NotifyAsync(c, tx, projectId, step.StepId, input.OperationId, actor,
                resolve ? OsanNotificationKind.StepIssueResolved : OsanNotificationKind.StepIssueRegistered, input.Comment, photos.Count, ct);
            var progress = (await ReadProgressAsync(c, tx, projectId, ct))!;
            if (resolve && progress.Status == "Completed")
                await OsanNotificationWriter.WriteAsync(c, tx, projectId, input.OperationId, OsanNotificationKind.ProjectCompleted, actor, DateTimeOffset.UtcNow, ct);
            await tx.CommitAsync(ct);
            return OsanProgressMutationResult.Success(new(input.OperationId, false, progress));
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(tx, ct);
            return OsanProgressMutationResult.Conflict("osan_progress_operation_conflict", "같은 요청 식별자가 다른 입력에 사용되었습니다.");
        }
    }
}
