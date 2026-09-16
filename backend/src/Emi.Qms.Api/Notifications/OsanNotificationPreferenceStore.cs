using Npgsql;
using System.Data;

namespace Emi.Qms.Api.Notifications;

public sealed class OsanNotificationPreferenceStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private sealed record Definition(string Kind, string Label);
    private sealed record StageDefinition(int Sequence, string Label);
    private readonly record struct PreferenceKey(string Kind, string Channel, int StageSequence);

    private static readonly Definition[] Definitions =
    [
        new("ProjectCreated", "프로젝트 생성"),
        new("StepCompleted", "Gate 완료"),
        new("StepRejected", "진행단계 반려"),
        new("StepEdited", "진행단계 수정 완료"),
        new("StepIssueRegistered", "공정 이상 발생"),
        new("StepIssueResolved", "이상 조치 완료"),
        new("ProjectCompleted", "프로젝트 완료"),
        new("StepWorkRequested", "공정 진행 요청")
    ];

    private static readonly StageDefinition[] Stages =
    [
        new(1, "입고검사"),
        new(2, "배치검사"),
        new(3, "배선검사"),
        new(4, "8계통"),
        new(5, "동작검사"),
        new(6, "출하검사"),
        new(7, "포장")
    ];

    public async Task<OsanNotificationPreferenceResult> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var result = await ReadAsync(connection, transaction, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<OsanNotificationPreferenceResult> SaveAsync(
        Guid userId,
        UpdateOsanNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0 || !TryNormalize(request, out var disabled))
        {
            return OsanNotificationPreferenceResult.Failure(
                OsanNotificationPreferenceResultStatus.Invalid,
                "InvalidOsanNotificationPreferences",
                "알림 설정 항목을 확인해 주세요.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        await using (var ensure = connection.CreateCommand())
        {
            ensure.Transaction = transaction;
            ensure.CommandText = "insert into osan_notification_global_preference_profiles(scope_id) values(@scope_id) on conflict do nothing;";
            ensure.Parameters.AddWithValue("scope_id", (short)1);
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }

        long version;
        await using (var readVersion = connection.CreateCommand())
        {
            readVersion.Transaction = transaction;
            readVersion.CommandText = "select version from osan_notification_global_preference_profiles where scope_id=@scope_id for update;";
            readVersion.Parameters.AddWithValue("scope_id", (short)1);
            version = (long)(await readVersion.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Osan notification preference profile was not created."));
        }

        if (version != request.ExpectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OsanNotificationPreferenceResult.Failure(
                OsanNotificationPreferenceResultStatus.Conflict,
                "OsanNotificationPreferenceVersionConflict",
                "다른 곳에서 설정이 변경되었습니다. 다시 불러온 뒤 저장해 주세요.");
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from osan_notification_global_preferences where scope_id=@scope_id;";
            delete.Parameters.AddWithValue("scope_id", (short)1);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var key in disabled)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into osan_notification_global_preferences(
                    scope_id,event_kind,channel,stage_sequence,is_enabled,updated_at_utc)
                values(@scope_id,@kind,@channel,@stage,false,@now);
                """;
            insert.Parameters.AddWithValue("scope_id", (short)1);
            insert.Parameters.AddWithValue("kind", key.Kind);
            insert.Parameters.AddWithValue("channel", key.Channel);
            insert.Parameters.AddWithValue("stage", (short)key.StageSequence);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "update osan_notification_global_preference_profiles set version=version+1,updated_at_utc=@now where scope_id=@scope_id;";
            update.Parameters.AddWithValue("scope_id", (short)1);
            update.Parameters.AddWithValue("now", now);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await using var readTransaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var result = await ReadAsync(connection, readTransaction, userId, cancellationToken);
        await readTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool TryNormalize(
        UpdateOsanNotificationPreferencesRequest request,
        out HashSet<PreferenceKey> disabled)
    {
        disabled = [];
        if (request.Items is null || request.StepCompletedStages is null
            || request.Items.Count != Definitions.Length || request.StepCompletedStages.Count != Stages.Length)
        {
            return false;
        }

        var definitionByKind = Definitions.ToDictionary(item => item.Kind, StringComparer.Ordinal);
        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Kind)
                || !definitionByKind.ContainsKey(item.Kind)
                || !seenKinds.Add(item.Kind)) return false;
            if (!item.MailEnabled) disabled.Add(new(item.Kind, "Mail", 0));
            if (!item.PushEnabled) disabled.Add(new(item.Kind, "WebPush", 0));
        }

        var stageBySequence = Stages.ToDictionary(item => item.Sequence);
        var seenStages = new HashSet<int>();
        foreach (var stage in request.StepCompletedStages)
        {
            if (stage is null || !stageBySequence.ContainsKey(stage.Sequence) || !seenStages.Add(stage.Sequence)) return false;
            if (!stage.MailEnabled) disabled.Add(new("StepCompleted", "Mail", stage.Sequence));
            if (!stage.PushEnabled) disabled.Add(new("StepCompleted", "WebPush", stage.Sequence));
        }

        return true;
    }

    private static async Task<OsanNotificationPreferenceResult> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        long version;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select coalesce((select version from osan_notification_global_preference_profiles where scope_id=@scope_id),0);";
            command.Parameters.AddWithValue("scope_id", (short)1);
            version = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        }

        var disabled = new HashSet<PreferenceKey>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select event_kind,channel,stage_sequence from osan_notification_global_preferences where scope_id=@scope_id and is_enabled=false;";
            command.Parameters.AddWithValue("scope_id", (short)1);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                disabled.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt16(2)));
        }

        var items = Definitions.Select(item => new OsanNotificationPreferenceItem(
            item.Kind,
            item.Label,
            !disabled.Contains(new(item.Kind, "Mail", 0)),
            !disabled.Contains(new(item.Kind, "WebPush", 0)))).ToArray();
        var stages = Stages.Select(stage => new OsanStepCompletedPreferenceItem(
            stage.Sequence,
            stage.Label,
            !disabled.Contains(new("StepCompleted", "Mail", stage.Sequence)),
            !disabled.Contains(new("StepCompleted", "WebPush", stage.Sequence)))).ToArray();
        return OsanNotificationPreferenceResult.Success(new(version, items, stages));
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connectionStringProvider.GetConnectionString());
        return builder.Build();
    }
}
