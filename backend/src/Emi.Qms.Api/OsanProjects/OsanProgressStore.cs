using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanProgressStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<OsanProgressResponse?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProgressAsync(connection, null, projectId, cancellationToken);
    }

    public async Task<OsanProgressPhotoDownload?> GetPhotoAsync(
        Guid projectId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select photo.original_file_name, photo.normalized_mime, photo.content
            from osan_progress_photos photo
            where photo.project_id = @project_id
              and photo.id = @photo_id
              and exists (
                  select 1
                  from osan_progress_step_photos link
                  where link.project_id = photo.project_id
                    and link.photo_id = photo.id
              );
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("photo_id", photoId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OsanProgressPhotoDownload(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<byte[]>(2))
            : null;
    }

    public async Task<OsanProgressMutationResult> StartAsync(
        Guid projectId,
        StartOsanProgressRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateStart(request);
        if (errors.Count > 0)
        {
            return OsanProgressMutationResult.Validation(errors);
        }

        var targets = request.Targets!
            .Where(target => target is not null)
            .Select(target => target!)
            .OrderBy(target => target.TargetId)
            .ToArray();
        var fingerprint = Fingerprint(new
        {
            Action = "Start",
            ProjectId = projectId,
            Targets = targets.Select(target => new { target.TargetId, target.ExpectedVersion })
        });

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var projectStatus = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
            if (projectStatus is null)
            {
                return await RollbackNotFoundAsync(transaction, cancellationToken);
            }

            var replay = await ReadOperationAsync(
                connection,
                transaction,
                request.OperationId,
                cancellationToken);
            if (replay is not null)
            {
                if (!MatchesOperation(replay, projectId, "Start", fingerprint, actorUserId))
                {
                    return await RollbackConflictAsync(
                        transaction,
                        "osan_progress_operation_conflict",
                        "같은 요청 식별자가 다른 입력에 사용되었습니다.",
                        cancellationToken);
                }

                var replayed = await ReadProgressAsync(connection, transaction, projectId, cancellationToken)
                    ?? throw new InvalidOperationException("A completed Osan progress operation has no project result.");
                await transaction.CommitAsync(cancellationToken);
                return OsanProgressMutationResult.Success(
                    new OsanProgressMutationResponse(request.OperationId, true, replayed));
            }

            if (string.Equals(projectStatus, "Completed", StringComparison.Ordinal))
            {
                return await RollbackConflictAsync(
                    transaction,
                    "osan_project_completed",
                    "완료된 프로젝트는 진행을 시작할 수 없습니다.",
                    cancellationToken);
            }

            var snapshots = await LockTargetsAsync(
                connection,
                transaction,
                projectId,
                targets.Select(target => target.TargetId).ToArray(),
                cancellationToken);
            var conflict = ValidateTargetSnapshots(targets, snapshots, requireNotStarted: true);
            if (conflict is not null)
            {
                return await RollbackConflictAsync(
                    transaction,
                    conflict.Value.Code,
                    conflict.Value.Message,
                    cancellationToken);
            }

            foreach (var target in targets)
            {
                await StartTargetAsync(
                    connection,
                    transaction,
                    projectId,
                    target.TargetId,
                    actorUserId,
                    cancellationToken);
            }

            await InsertOperationAsync(
                connection,
                transaction,
                request.OperationId,
                projectId,
                "Start",
                null,
                null,
                targets.Select(target => target.TargetId).ToArray(),
                fingerprint,
                actorUserId,
                cancellationToken);

            var progress = await ReadProgressAsync(connection, transaction, projectId, cancellationToken)
                ?? throw new InvalidOperationException("The started Osan project was not readable before commit.");
            await transaction.CommitAsync(cancellationToken);
            return OsanProgressMutationResult.Success(
                new OsanProgressMutationResponse(request.OperationId, false, progress));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return OsanProgressMutationResult.Conflict(
                "osan_progress_operation_conflict",
                "같은 요청 식별자가 다른 입력에 사용되었습니다.");
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<OsanProgressMutationResult> CompleteAsync(
        Guid projectId,
        CompleteOsanProgressInput input,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCompletion(input);
        if (errors.Count > 0)
        {
            return OsanProgressMutationResult.Validation(errors);
        }

        var targets = input.Targets
            .Where(target => target is not null)
            .Select(target => target!)
            .OrderBy(target => target.TargetId)
            .ToArray();
        var fingerprint = Fingerprint(new
        {
            Action = "Complete",
            ProjectId = projectId,
            input.CompletionMode,
            input.StageSequence,
            Targets = targets.Select(target => new { target.TargetId, target.ExpectedVersion }),
            Photos = input.Photos.Select((photo, index) => new
            {
                DisplayOrder = index + 1,
                photo.FileName,
                photo.NormalizedMime,
                photo.Sha256
            })
        });

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var projectStatus = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
            if (projectStatus is null)
            {
                return await RollbackNotFoundAsync(transaction, cancellationToken);
            }

            var replay = await ReadOperationAsync(
                connection,
                transaction,
                input.OperationId,
                cancellationToken);
            if (replay is not null)
            {
                if (!MatchesOperation(replay, projectId, "Complete", fingerprint, actorUserId))
                {
                    return await RollbackConflictAsync(
                        transaction,
                        "osan_progress_operation_conflict",
                        "같은 요청 식별자가 다른 입력에 사용되었습니다.",
                        cancellationToken);
                }

                var replayed = await ReadProgressAsync(connection, transaction, projectId, cancellationToken)
                    ?? throw new InvalidOperationException("A completed Osan progress operation has no project result.");
                await transaction.CommitAsync(cancellationToken);
                return OsanProgressMutationResult.Success(
                    new OsanProgressMutationResponse(input.OperationId, true, replayed));
            }

            if (string.Equals(projectStatus, "Completed", StringComparison.Ordinal))
            {
                return await RollbackConflictAsync(
                    transaction,
                    "osan_project_completed",
                    "이미 완료된 프로젝트입니다.",
                    cancellationToken);
            }

            var snapshots = await LockTargetsAsync(
                connection,
                transaction,
                projectId,
                targets.Select(target => target.TargetId).ToArray(),
                cancellationToken);
            var targetConflict = ValidateTargetSnapshots(targets, snapshots, requireNotStarted: false);
            if (targetConflict is not null)
            {
                return await RollbackConflictAsync(
                    transaction,
                    targetConflict.Value.Code,
                    targetConflict.Value.Message,
                    cancellationToken);
            }

            var steps = await LockStepsAsync(
                connection,
                transaction,
                projectId,
                targets.Select(target => target.TargetId).ToArray(),
                cancellationToken);
            foreach (var target in targets)
            {
                var targetSteps = steps
                    .Where(step => step.TargetId == target.TargetId)
                    .OrderBy(step => step.SequenceNumber)
                    .ToArray();
                if (targetSteps.Length != 7)
                {
                    return await RollbackConflictAsync(
                        transaction,
                        "osan_progress_snapshot_invalid",
                        "대상의 7단계 진행 기록을 확인할 수 없습니다.",
                        cancellationToken);
                }

                var selectedStep = targetSteps.Single(step => step.SequenceNumber == input.StageSequence);
                if (string.Equals(selectedStep.Status, "Completed", StringComparison.Ordinal))
                {
                    return await RollbackConflictAsync(
                        transaction,
                        "osan_progress_step_already_completed",
                        $"{snapshots[target.TargetId].DisplayName}의 선택 단계가 이미 완료되었습니다.",
                        cancellationToken);
                }

                if (string.Equals(input.CompletionMode, OsanCompletionModes.Individual, StringComparison.Ordinal))
                {
                    var nextIncomplete = targetSteps.First(step => !string.Equals(
                        step.Status,
                        "Completed",
                        StringComparison.Ordinal));
                    if (nextIncomplete.SequenceNumber != input.StageSequence)
                    {
                        return await RollbackConflictAsync(
                            transaction,
                            "osan_progress_individual_order_invalid",
                            $"{snapshots[target.TargetId].DisplayName}은(는) 다음 미완료 단계만 완료할 수 있습니다.",
                            cancellationToken);
                    }
                }

                if (input.StageSequence == 7
                    && targetSteps.Take(6).Any(step => !string.Equals(
                        step.Status,
                        "Completed",
                        StringComparison.Ordinal)))
                {
                    return await RollbackConflictAsync(
                        transaction,
                        "osan_progress_packing_prerequisite_incomplete",
                        $"{snapshots[target.TargetId].DisplayName}의 포장 전 6단계가 모두 완료되지 않았습니다.",
                        cancellationToken);
                }
            }

            await InsertOperationAsync(
                connection,
                transaction,
                input.OperationId,
                projectId,
                "Complete",
                input.CompletionMode,
                input.StageSequence,
                targets.Select(target => target.TargetId).ToArray(),
                fingerprint,
                actorUserId,
                cancellationToken);

            var completedSteps = new List<(Guid TargetId, Guid StepId)>(targets.Length);
            foreach (var target in targets)
            {
                var stepId = await CompleteStepAsync(
                    connection,
                    transaction,
                    projectId,
                    target.TargetId,
                    input.StageSequence,
                    actorUserId,
                    cancellationToken);
                completedSteps.Add((target.TargetId, stepId));
                await RefreshTargetProjectionAsync(
                    connection,
                    transaction,
                    projectId,
                    target.TargetId,
                    cancellationToken);
            }

            for (var photoIndex = 0; photoIndex < input.Photos.Count; photoIndex += 1)
            {
                var photoId = await InsertPhotoAsync(
                    connection,
                    transaction,
                    projectId,
                    input.OperationId,
                    input.Photos[photoIndex],
                    photoIndex + 1,
                    actorUserId,
                    cancellationToken);
                foreach (var completedStep in completedSteps)
                {
                    await LinkPhotoAsync(
                        connection,
                        transaction,
                        projectId,
                        completedStep.TargetId,
                        completedStep.StepId,
                        photoId,
                        input.StageSequence,
                        cancellationToken);
                }
            }

            if (input.StageSequence == 7)
            {
                await CompleteProjectWhenAllTargetsCompletedAsync(
                    connection,
                    transaction,
                    projectId,
                    cancellationToken);
            }

            var progress = await ReadProgressAsync(connection, transaction, projectId, cancellationToken)
                ?? throw new InvalidOperationException("The updated Osan project was not readable before commit.");
            await transaction.CommitAsync(cancellationToken);
            return OsanProgressMutationResult.Success(
                new OsanProgressMutationResponse(input.OperationId, false, progress));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return OsanProgressMutationResult.Conflict(
                "osan_progress_operation_conflict",
                "같은 요청 식별자가 다른 입력에 사용되었거나 같은 사진이 중복 선택되었습니다.");
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    private static Dictionary<string, string[]> ValidateStart(StartOsanProgressRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.OperationId == Guid.Empty)
        {
            errors[nameof(request.OperationId)] = ["작업 식별자가 필요합니다."];
        }
        ValidateTargets(request.Targets, errors);
        return errors;
    }

    private static Dictionary<string, string[]> ValidateCompletion(CompleteOsanProgressInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.OperationId == Guid.Empty)
        {
            errors[nameof(input.OperationId)] = ["작업 식별자가 필요합니다."];
        }
        if (input.CompletionMode is not (OsanCompletionModes.Individual or OsanCompletionModes.Batch))
        {
            errors[nameof(input.CompletionMode)] = ["완료 방식은 individual 또는 batch여야 합니다."];
        }
        if (input.StageSequence is < 1 or > 7)
        {
            errors[nameof(input.StageSequence)] = ["단계 번호는 1~7이어야 합니다."];
        }
        ValidateTargets(input.Targets, errors);
        if (input.CompletionMode == OsanCompletionModes.Individual && input.Targets.Count != 1)
        {
            errors[nameof(input.Targets)] = ["개별 완료는 대상 하나만 선택해야 합니다."];
        }
        if (input.Photos.Count > OsanProgressPhotoValidator.MaximumPhotoCount)
        {
            errors[nameof(input.Photos)] = ["사진은 최대 5장까지 첨부할 수 있습니다."];
        }
        if (input.Photos.Sum(photo => (long)photo.Content.Length) > OsanProgressPhotoValidator.MaximumTotalBytes)
        {
            errors[nameof(input.Photos)] = ["사진 전체 크기는 15MiB 이하여야 합니다."];
        }
        if (input.Photos.Select(photo => photo.Sha256).Distinct(StringComparer.Ordinal).Count() != input.Photos.Count)
        {
            errors[nameof(input.Photos)] = ["같은 사진을 중복해서 첨부할 수 없습니다."];
        }
        return errors;
    }

    private static void ValidateTargets(
        IReadOnlyList<OsanProgressTargetRequest?>? targets,
        IDictionary<string, string[]> errors)
    {
        if (targets is null || targets.Count is < 1 or > 500)
        {
            errors[nameof(targets)] = ["대상을 1~500개 선택해야 합니다."];
            return;
        }
        if (targets.Any(target => target is null
            || target.TargetId == Guid.Empty
            || target.ExpectedVersion < 1))
        {
            errors[nameof(targets)] = ["각 대상의 식별자와 최신 버전이 필요합니다."];
        }
        if (targets.Where(target => target is not null)
            .Select(target => target!.TargetId).Distinct().Count() != targets.Count)
        {
            errors[nameof(targets)] = ["같은 대상을 중복해서 선택할 수 없습니다."];
        }
    }

    private static async Task<string?> LockProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select status
            from projects
            where id = @project_id
              and project_profile = 'Osan'
              and deleted_at_utc is null
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<Dictionary<Guid, TargetSnapshot>> LockTargetsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid[] targetIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, display_name, status, version
            from osan_project_targets
            where project_id = @project_id
              and id = any(@target_ids)
            order by id
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("target_ids", targetIds));
        var result = new Dictionary<Guid, TargetSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var snapshot = new TargetSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3));
            result.Add(snapshot.TargetId, snapshot);
        }
        return result;
    }

    private static (string Code, string Message)? ValidateTargetSnapshots(
        IReadOnlyList<OsanProgressTargetRequest> requested,
        IReadOnlyDictionary<Guid, TargetSnapshot> snapshots,
        bool requireNotStarted)
    {
        foreach (var target in requested)
        {
            if (!snapshots.TryGetValue(target.TargetId, out var snapshot))
            {
                return ("osan_progress_target_not_found", "선택 대상이 프로젝트에 없거나 접근할 수 없습니다.");
            }
            if (snapshot.Version != target.ExpectedVersion)
            {
                return ("osan_progress_stale_version", $"{snapshot.DisplayName}의 진행 상태가 변경되었습니다. 새로고침 후 다시 시도해 주세요.");
            }
            if (requireNotStarted && !string.Equals(snapshot.Status, "NotStarted", StringComparison.Ordinal))
            {
                return ("osan_progress_target_already_started", $"{snapshot.DisplayName}은(는) 이미 시작되었거나 완료되었습니다.");
            }
            if (!requireNotStarted && !string.Equals(snapshot.Status, "InProgress", StringComparison.Ordinal))
            {
                return ("osan_progress_target_not_in_progress", $"{snapshot.DisplayName}을(를) 먼저 시작해야 합니다.");
            }
        }
        return null;
    }

    private static async Task StartTargetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid targetId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update osan_project_targets
            set status = 'InProgress',
                version = version + 1,
                started_at_utc = now(),
                started_by_user_id = @actor_id,
                updated_at_utc = now()
            where project_id = @project_id and id = @target_id;

            update osan_project_target_steps
            set status = 'InProgress',
                started_at_utc = coalesce(started_at_utc, now()),
                updated_at_utc = now()
            where project_id = @project_id
              and target_id = @target_id
              and sequence_number = 1;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<StepSnapshot>> LockStepsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid[] targetIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, target_id, sequence_number, status
            from osan_project_target_steps
            where project_id = @project_id
              and target_id = any(@target_ids)
            order by target_id, sequence_number
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("target_ids", targetIds));
        var result = new List<StepSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StepSnapshot(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3)));
        }
        return result;
    }

    private static async Task<Guid> CompleteStepAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid targetId,
        int stageSequence,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update osan_project_target_steps
            set status = 'Completed',
                started_at_utc = coalesce(started_at_utc, now()),
                completed_at_utc = now(),
                completed_by_user_id = @actor_id,
                updated_at_utc = now()
            where project_id = @project_id
              and target_id = @target_id
              and sequence_number = @stage_sequence
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("stage_sequence", stageSequence);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Osan progress step disappeared while locked."));
    }

    private static async Task RefreshTargetProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with incomplete as (
                select min(sequence_number) as next_sequence,
                       count(*) as incomplete_count
                from osan_project_target_steps
                where project_id = @project_id
                  and target_id = @target_id
                  and status <> 'Completed'
            ), normalized_steps as (
                update osan_project_target_steps step
                set status = case
                        when step.sequence_number = incomplete.next_sequence then 'InProgress'
                        else 'NotStarted'
                    end,
                    started_at_utc = case
                        when step.sequence_number = incomplete.next_sequence
                            then coalesce(step.started_at_utc, now())
                        else step.started_at_utc
                    end,
                    updated_at_utc = now()
                from incomplete
                where step.project_id = @project_id
                  and step.target_id = @target_id
                  and step.status <> 'Completed'
                returning 1
            )
            update osan_project_targets target
            set status = case
                    when incomplete.incomplete_count = 0 then 'Completed'
                    else 'InProgress'
                end,
                version = target.version + 1,
                updated_at_utc = now()
            from incomplete
            where target.project_id = @project_id
              and target.id = @target_id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("target_id", targetId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> InsertPhotoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid operationId,
        OsanProgressPhotoInput photo,
        int displayOrder,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_progress_photos (
                project_id, operation_id, original_file_name, display_order,
                normalized_mime, byte_size, sha256, content, uploaded_by_user_id)
            values (
                @project_id, @operation_id, @file_name, @display_order,
                @normalized_mime, @byte_size, @sha256, @content, @actor_id)
            returning id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("file_name", photo.FileName);
        command.Parameters.AddWithValue("display_order", displayOrder);
        command.Parameters.AddWithValue("normalized_mime", photo.NormalizedMime);
        command.Parameters.AddWithValue("byte_size", photo.Content.Length);
        command.Parameters.AddWithValue("sha256", photo.Sha256);
        command.Parameters.Add(new NpgsqlParameter("content", NpgsqlDbType.Bytea) { Value = photo.Content });
        command.Parameters.AddWithValue("actor_id", actorUserId);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Osan progress photo was not inserted."));
    }

    private static async Task LinkPhotoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid targetId,
        Guid stepId,
        Guid photoId,
        int stageSequence,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_progress_step_photos (
                project_id, target_id, step_id, photo_id, stage_sequence)
            values (@project_id, @target_id, @step_id, @photo_id, @stage_sequence);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("target_id", targetId);
        command.Parameters.AddWithValue("step_id", stepId);
        command.Parameters.AddWithValue("photo_id", photoId);
        command.Parameters.AddWithValue("stage_sequence", stageSequence);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteProjectWhenAllTargetsCompletedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update projects project
            set status = 'Completed', updated_at_utc = now()
            where project.id = @project_id
              and project.project_profile = 'Osan'
              and not exists (
                  select 1
                  from osan_project_targets target
                  where target.project_id = project.id
                    and target.status <> 'Completed'
              );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OperationSnapshot?> ReadOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select project_id, action, request_fingerprint, requested_by_user_id
            from osan_progress_operations
            where operation_id = @operation_id;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OperationSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetGuid(3))
            : null;
    }

    private static bool MatchesOperation(
        OperationSnapshot operation,
        Guid projectId,
        string action,
        string fingerprint,
        Guid actorUserId) =>
        operation.ProjectId == projectId
        && operation.RequestedByUserId == actorUserId
        && string.Equals(operation.Action, action, StringComparison.Ordinal)
        && string.Equals(operation.RequestFingerprint, fingerprint, StringComparison.Ordinal);

    private static async Task InsertOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        string action,
        string? completionMode,
        int? stageSequence,
        Guid[] targetIds,
        string fingerprint,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_progress_operations (
                operation_id, project_id, action, completion_mode, stage_sequence,
                target_ids, request_fingerprint, requested_by_user_id)
            values (
                @operation_id, @project_id, @action, @completion_mode, @stage_sequence,
                @target_ids, @request_fingerprint, @actor_id);
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add(new NpgsqlParameter("completion_mode", NpgsqlDbType.Text)
        {
            Value = completionMode is null ? DBNull.Value : completionMode
        });
        command.Parameters.Add(new NpgsqlParameter("stage_sequence", NpgsqlDbType.Integer)
        {
            Value = stageSequence is null ? DBNull.Value : stageSequence.Value
        });
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("target_ids", targetIds));
        command.Parameters.AddWithValue("request_fingerprint", fingerprint);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OsanProgressResponse?> ReadProgressAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        Guid id;
        string projectCode;
        string title;
        string storedStatus;
        await using (var projectCommand = connection.CreateCommand())
        {
            projectCommand.Transaction = transaction;
            projectCommand.CommandText = """
                select id, project_code, project_title, status
                from projects
                where id = @project_id
                  and project_profile = 'Osan'
                  and deleted_at_utc is null;
                """;
            projectCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await projectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            id = reader.GetGuid(0);
            projectCode = reader.GetString(1);
            title = reader.GetString(2);
            storedStatus = reader.GetString(3);
        }

        var targets = new List<TargetBuilder>();
        TargetBuilder? currentTarget = null;
        await using (var stepCommand = connection.CreateCommand())
        {
            stepCommand.Transaction = transaction;
            stepCommand.CommandText = """
                select
                    target.id, target.sequence_number, target.display_name, target.status,
                    target.version, target.started_at_utc, target.started_by_user_id,
                    starter.display_name,
                    step.id, step.sequence_number, step.step_code, step.step_name, step.status,
                    step.started_at_utc, step.completed_at_utc, step.completed_by_user_id,
                    completer.display_name
                from osan_project_targets target
                join osan_project_target_steps step on step.target_id = target.id
                left join qms_users starter on starter.id = target.started_by_user_id
                left join qms_users completer on completer.id = step.completed_by_user_id
                where target.project_id = @project_id
                order by target.sequence_number, step.sequence_number;
                """;
            stepCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await stepCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var targetId = reader.GetGuid(0);
                if (currentTarget?.TargetId != targetId)
                {
                    currentTarget = new TargetBuilder(
                        targetId,
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                        reader.IsDBNull(6) ? null : reader.GetGuid(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7));
                    targets.Add(currentTarget);
                }

                currentTarget.Steps.Add(new StepBuilder(
                    reader.GetGuid(8),
                    reader.GetInt32(9),
                    reader.GetString(10),
                    reader.GetString(11),
                    reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                    reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                    reader.IsDBNull(15) ? null : reader.GetGuid(15),
                    reader.IsDBNull(16) ? null : reader.GetString(16)));
            }
        }

        var stepsById = targets.SelectMany(target => target.Steps).ToDictionary(step => step.StepId);
        await using (var photoCommand = connection.CreateCommand())
        {
            photoCommand.Transaction = transaction;
            photoCommand.CommandText = """
                select link.step_id, photo.id, photo.display_order, photo.original_file_name,
                       photo.normalized_mime, photo.byte_size, photo.sha256,
                       photo.uploaded_at_utc, photo.uploaded_by_user_id, uploader.display_name
                from osan_progress_step_photos link
                join osan_progress_photos photo
                  on photo.project_id = link.project_id and photo.id = link.photo_id
                join qms_users uploader on uploader.id = photo.uploaded_by_user_id
                where link.project_id = @project_id
                order by link.step_id, photo.display_order;
                """;
            photoCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await photoCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (stepsById.TryGetValue(reader.GetGuid(0), out var step))
                {
                    step.Photos.Add(new OsanProgressPhotoResponse(
                        reader.GetGuid(1),
                        reader.GetInt32(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetInt32(5),
                        reader.GetString(6),
                        reader.GetFieldValue<DateTimeOffset>(7),
                        reader.GetGuid(8),
                        reader.GetString(9)));
                }
            }
        }

        var targetResponses = targets.Select(target => target.ToResponse()).ToArray();
        var completedStepCount = targetResponses.Sum(target => target.Steps.Count(step => step.Status == "Completed"));
        var totalStepCount = targetResponses.Sum(target => target.Steps.Count);
        var progressStatus = storedStatus == "Completed"
            ? "Completed"
            : targetResponses.All(target => target.Status == "NotStarted")
                ? "NotStarted"
                : "InProgress";
        return new OsanProgressResponse(
            id,
            projectCode,
            title,
            progressStatus,
            completedStepCount,
            totalStepCount,
            targetResponses);
    }

    private static string Fingerprint<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
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

    private static async Task<OsanProgressMutationResult> RollbackNotFoundAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return OsanProgressMutationResult.NotFound();
    }

    private static async Task<OsanProgressMutationResult> RollbackConflictAsync(
        NpgsqlTransaction transaction,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return OsanProgressMutationResult.Conflict(code, message);
    }

    private static async Task RollbackQuietlyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record TargetSnapshot(Guid TargetId, string DisplayName, string Status, int Version);
    private sealed record StepSnapshot(Guid StepId, Guid TargetId, int SequenceNumber, string Status);
    private sealed record OperationSnapshot(
        Guid ProjectId,
        string Action,
        string RequestFingerprint,
        Guid RequestedByUserId);

    private sealed class TargetBuilder(
        Guid targetId,
        int sequenceNumber,
        string displayName,
        string status,
        int version,
        DateTimeOffset? startedAtUtc,
        Guid? startedByUserId,
        string? startedByDisplayName)
    {
        public Guid TargetId { get; } = targetId;
        public List<StepBuilder> Steps { get; } = [];

        public OsanProgressTargetResponse ToResponse()
        {
            var nextIncomplete = Steps.FirstOrDefault(step => step.Status != "Completed");
            var priorSixComplete = Steps.Take(6).All(step => step.Status == "Completed");
            return new OsanProgressTargetResponse(
                TargetId,
                sequenceNumber,
                displayName,
                status,
                version,
                startedAtUtc,
                startedByUserId,
                startedByDisplayName,
                status == "NotStarted",
                Steps.Select(step => step.ToResponse(
                    status == "InProgress" && ReferenceEquals(step, nextIncomplete),
                    status == "InProgress"
                        && step.Status != "Completed"
                        && (step.SequenceNumber < 7 || priorSixComplete)))
                    .ToArray());
        }
    }

    private sealed class StepBuilder(
        Guid stepId,
        int sequenceNumber,
        string stepCode,
        string stepName,
        string status,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        Guid? completedByUserId,
        string? completedByDisplayName)
    {
        public Guid StepId { get; } = stepId;
        public int SequenceNumber { get; } = sequenceNumber;
        public string Status { get; } = status;
        public List<OsanProgressPhotoResponse> Photos { get; } = [];

        public OsanProgressStepResponse ToResponse(
            bool canCompleteIndividual,
            bool canCompleteBatch) =>
            new(
                StepId,
                SequenceNumber,
                stepCode,
                stepName,
                Status,
                startedAtUtc,
                completedAtUtc,
                completedByUserId,
                completedByDisplayName,
                canCompleteIndividual,
                canCompleteBatch,
                null,
                [],
                Photos);
    }
}
