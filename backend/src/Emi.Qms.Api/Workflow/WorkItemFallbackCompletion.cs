using Npgsql;

namespace Emi.Qms.Api.Workflow;

internal static class WorkItemFallbackCompletion
{
    public static Task SynchronizeForWorkItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workItemId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAsync(
            connection,
            transaction,
            "work_item.id = @work_item_id",
            command => command.Parameters.AddWithValue("work_item_id", workItemId),
            actorUserId,
            cancellationToken);
    }

    public static Task SynchronizeForProjectStageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string stageCode,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAsync(
            connection,
            transaction,
            "work_item.project_id = @project_id and work_item.workflow_stage_code = @stage_code",
            command =>
            {
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("stage_code", stageCode);
            },
            actorUserId,
            cancellationToken);
    }

    public static Task SynchronizeForPanelStageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<Guid> panelIds,
        string stageCode,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAsync(
            connection,
            transaction,
            "work_item.project_id = @project_id and work_item.target_type = 'Panel' and work_item.target_id = any(@panel_ids) and work_item.workflow_stage_code = @stage_code",
            command =>
            {
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("panel_ids", panelIds.ToArray());
                command.Parameters.AddWithValue("stage_code", stageCode);
            },
            actorUserId,
            cancellationToken);
    }

    public static Task SynchronizeForIdempotencyPrefixAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string idempotencyKey,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        return SynchronizeAsync(
            connection,
            transaction,
            "(work_item.idempotency_key = @idempotency_key or work_item.idempotency_key like @idempotency_prefix)",
            command =>
            {
                command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
                command.Parameters.AddWithValue("idempotency_prefix", $"{idempotencyKey}:%");
            },
            actorUserId,
            cancellationToken);
    }

    private static async Task SynchronizeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string targetPredicate,
        Action<NpgsqlCommand> addTargetParameters,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var groupKeys = new List<string>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = $"""
                select distinct work_item.fallback_group_key
                from work_items work_item
                where {targetPredicate}
                  and work_item.fallback_group_key is not null
                  and work_item.status = 'Completed'
                order by work_item.fallback_group_key;
                """;
            addTargetParameters(read);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groupKeys.Add(reader.GetString(0));
            }
        }

        foreach (var groupKey in groupKeys)
        {
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText = "select pg_advisory_xact_lock(hashtextextended(@fallback_group_key, 0));";
                lockCommand.Parameters.AddWithValue("fallback_group_key", groupKey);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            Guid winnerUserId;
            await using (var winner = connection.CreateCommand())
            {
                winner.Transaction = transaction;
                winner.CommandText = """
                    select coalesce(
                        (
                            select fallback_completed_by_user_id
                            from work_items
                            where fallback_group_key = @fallback_group_key
                              and fallback_completed_by_user_id is not null
                            order by completed_at_utc nulls last, id
                            limit 1
                        ),
                        (
                            select assigned_user_id
                            from work_items
                            where fallback_group_key = @fallback_group_key
                              and assigned_user_id = @actor_user_id
                            limit 1
                        ),
                        (
                            select assigned_user_id
                            from work_items
                            where fallback_group_key = @fallback_group_key
                              and status = 'Completed'
                            order by completed_at_utc nulls last, id
                            limit 1
                        ),
                        @actor_user_id);
                    """;
                winner.Parameters.AddWithValue("fallback_group_key", groupKey);
                winner.Parameters.AddWithValue("actor_user_id", actorUserId);
                winnerUserId = (Guid)(await winner.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Fallback completion winner could not be determined."));
            }

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update work_items
                set status = 'Completed',
                    started_at_utc = coalesce(started_at_utc, now()),
                    completed_at_utc = coalesce(completed_at_utc, now()),
                    fallback_completed_by_user_id = @winner_user_id,
                    fallback_auto_closed_at_utc = case
                        when assigned_user_id <> @winner_user_id
                            then coalesce(fallback_auto_closed_at_utc, now())
                        else fallback_auto_closed_at_utc
                    end
                where fallback_group_key = @fallback_group_key
                  and status in ('Requested', 'InProgress', 'Completed');
                """;
            update.Parameters.AddWithValue("fallback_group_key", groupKey);
            update.Parameters.AddWithValue("winner_user_id", winnerUserId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
