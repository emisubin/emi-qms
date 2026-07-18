using Npgsql;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationPreferenceStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider)
{
    private readonly record struct PreferenceKey(string DeliveryType, string Channel);
    private sealed record UserState(Guid UserId, string DisplayName, bool IsActive);

    public async Task<NotificationPreferenceResult> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadResponseAsync(connection, null, userId, changed: false, cancellationToken);
    }

    public Task<NotificationPreferenceResult> SaveAsync(
        Guid actorUserId,
        Guid targetUserId,
        UpdateNotificationPreferencesRequest request,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var validation = ValidateItems(request.Items);
        return validation is not null
            ? Task.FromResult(validation)
            : MutateAsync(
                actorUserId,
                targetUserId,
                request.ExpectedVersion,
                request.Items.Where(item => !item.Enabled)
                    .Select(item => new PreferenceKey(item.DeliveryType.Trim(), item.Channel.Trim()))
                    .ToHashSet(),
                isAdmin ? "AdminSave" : "Save",
                cancellationToken);
    }

    public Task<NotificationPreferenceResult> ResetAsync(
        Guid actorUserId,
        Guid targetUserId,
        long expectedVersion,
        bool isAdmin,
        CancellationToken cancellationToken) =>
        MutateAsync(
            actorUserId,
            targetUserId,
            expectedVersion,
            [],
            isAdmin ? "AdminReset" : "Reset",
            cancellationToken);

    private async Task<NotificationPreferenceResult> MutateAsync(
        Guid actorUserId,
        Guid targetUserId,
        long expectedVersion,
        HashSet<PreferenceKey> desiredOverrides,
        string action,
        CancellationToken cancellationToken)
    {
        if (expectedVersion < 0)
        {
            return Invalid("InvalidVersion", "설정 버전을 확인해 주세요.", "expectedVersion");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var user = await ReadUserAsync(connection, transaction, targetUserId, cancellationToken);
        if (user is null)
        {
            return NotificationPreferenceResult.Failure(
                NotificationPreferenceResultStatus.NotFound,
                "NotificationPreferenceUserNotFound",
                "알림 설정 대상 사용자를 찾을 수 없습니다.");
        }

        if (!user.IsActive)
        {
            return NotificationPreferenceResult.Failure(
                NotificationPreferenceResultStatus.Inactive,
                "NotificationPreferenceUserInactive",
                "비활성 사용자의 알림 설정은 변경할 수 없습니다.");
        }

        await EnsureProfileAsync(connection, transaction, targetUserId, cancellationToken);
        var currentVersion = await LockAndReadVersionAsync(connection, transaction, targetUserId, cancellationToken);
        if (currentVersion != expectedVersion)
        {
            return NotificationPreferenceResult.Failure(
                NotificationPreferenceResultStatus.Conflict,
                "NotificationPreferenceVersionConflict",
                "다른 곳에서 설정이 변경되었습니다. 다시 불러온 뒤 저장해 주세요.");
        }

        var currentOverrides = await ReadOverridesAsync(connection, transaction, targetUserId, forUpdate: true, cancellationToken);
        if (currentOverrides.SetEquals(desiredOverrides))
        {
            await transaction.CommitAsync(cancellationToken);
            return await ReadResponseAsync(connection, null, targetUserId, changed: false, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        await ReplaceOverridesAsync(connection, transaction, targetUserId, desiredOverrides, now, cancellationToken);
        var nextVersion = currentVersion + 1;
        await UpdateVersionAsync(connection, transaction, targetUserId, nextVersion, now, cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction,
            actorUserId,
            targetUserId,
            action,
            currentOverrides,
            desiredOverrides,
            nextVersion,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ReadResponseAsync(connection, null, targetUserId, changed: true, cancellationToken);
    }

    private static NotificationPreferenceResult? ValidateItems(IReadOnlyList<NotificationPreferenceUpdateItem>? items)
    {
        if (items is null)
        {
            return Invalid("NotificationPreferenceItemsRequired", "변경할 알림 설정을 확인해 주세요.", "items");
        }

        var seen = new HashSet<PreferenceKey>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var definition = NotificationPreferenceCatalog.Find(item.DeliveryType, item.Channel);
            if (definition is null)
            {
                return Invalid("UnsupportedNotificationPreference", "지원하지 않는 알림 설정이 포함되어 있습니다.", $"items[{index}]");
            }

            if (!definition.CanChange)
            {
                return Invalid("LockedNotificationPreference", "업무상 필수 알림은 변경할 수 없습니다.", $"items[{index}]");
            }

            if (!seen.Add(new PreferenceKey(definition.DeliveryType, definition.Channel)))
            {
                return Invalid("DuplicateNotificationPreference", "같은 알림 설정이 중복되어 있습니다.", $"items[{index}]");
            }
        }

        return null;
    }

    private static NotificationPreferenceResult Invalid(string code, string message, string field) =>
        NotificationPreferenceResult.Failure(
            NotificationPreferenceResultStatus.Invalid,
            code,
            message,
            new Dictionary<string, string[]> { [field] = [message] });

    private async Task<NotificationPreferenceResult> ReadResponseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        bool changed,
        CancellationToken cancellationToken)
    {
        var user = await ReadUserAsync(connection, transaction, userId, cancellationToken);
        if (user is null)
        {
            return NotificationPreferenceResult.Failure(
                NotificationPreferenceResultStatus.NotFound,
                "NotificationPreferenceUserNotFound",
                "알림 설정 대상 사용자를 찾을 수 없습니다.");
        }

        if (!user.IsActive)
        {
            return NotificationPreferenceResult.Failure(
                NotificationPreferenceResultStatus.Inactive,
                "NotificationPreferenceUserInactive",
                "비활성 사용자의 알림 설정을 조회할 수 없습니다.");
        }

        var version = await ReadVersionAsync(connection, transaction, userId, cancellationToken);
        var overrides = await ReadOverridesAsync(connection, transaction, userId, forUpdate: false, cancellationToken);
        var items = NotificationPreferenceCatalog.Items
            .Select(definition =>
            {
                var key = new PreferenceKey(definition.DeliveryType, definition.Channel);
                var isOverridden = overrides.Contains(key);
                return new NotificationPreferenceItemResponse(
                    definition.DeliveryType,
                    definition.Channel,
                    definition.EventLabel,
                    definition.ChannelLabel,
                    definition.Description,
                    !isOverridden,
                    definition.CanChange,
                    isOverridden,
                    definition.LockReason);
            })
            .ToArray();

        return NotificationPreferenceResult.Success(new NotificationPreferenceResponse(
            user.UserId,
            user.DisplayName,
            NotificationPreferenceCatalog.TaxonomyVersion,
            version,
            overrides.Count == 0,
            changed,
            items));
    }

    private static async Task<UserState?> ReadUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id, display_name, is_active from qms_users where id = @user_id;";
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UserState(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2))
            : null;
    }

    private static async Task EnsureProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into user_notification_preference_profiles (user_id) values (@user_id) on conflict do nothing;";
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> LockAndReadVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select version from user_notification_preference_profiles where user_id = @user_id for update;";
        command.Parameters.AddWithValue("user_id", userId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Notification preference profile was not created."));
    }

    private static async Task<long> ReadVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce((select version from user_notification_preference_profiles where user_id = @user_id), 0);";
        command.Parameters.AddWithValue("user_id", userId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<HashSet<PreferenceKey>> ReadOverridesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select delivery_type, channel from user_notification_preferences where user_id = @user_id{(forUpdate ? " for update" : string.Empty)};";
        command.Parameters.AddWithValue("user_id", userId);
        var overrides = new HashSet<PreferenceKey>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overrides.Add(new PreferenceKey(reader.GetString(0), reader.GetString(1)));
        }

        return overrides;
    }

    private static async Task ReplaceOverridesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        HashSet<PreferenceKey> desiredOverrides,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from user_notification_preferences where user_id = @user_id;";
            delete.Parameters.AddWithValue("user_id", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var key in desiredOverrides)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into user_notification_preferences (
                    user_id, delivery_type, channel, is_enabled, updated_at_utc
                ) values (
                    @user_id, @delivery_type, @channel, false, @now
                );
                """;
            insert.Parameters.AddWithValue("user_id", userId);
            insert.Parameters.AddWithValue("delivery_type", key.DeliveryType);
            insert.Parameters.AddWithValue("channel", key.Channel);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        long nextVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update user_notification_preference_profiles set version = @version, updated_at_utc = @now where user_id = @user_id;";
        command.Parameters.AddWithValue("version", nextVersion);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorUserId,
        Guid targetUserId,
        string action,
        HashSet<PreferenceKey> before,
        HashSet<PreferenceKey> after,
        long resultingVersion,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var definition in NotificationPreferenceCatalog.ChangeableItems)
        {
            var key = new PreferenceKey(definition.DeliveryType, definition.Channel);
            var oldEnabled = !before.Contains(key);
            var newEnabled = !after.Contains(key);
            if (oldEnabled == newEnabled)
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into user_notification_preference_audit_events (
                    actor_user_id, target_user_id, action, delivery_type, channel,
                    old_value, new_value, resulting_version, occurred_at_utc
                ) values (
                    @actor_user_id, @target_user_id, @action, @delivery_type, @channel,
                    @old_value, @new_value, @resulting_version, @now
                );
                """;
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            command.Parameters.AddWithValue("target_user_id", targetUserId);
            command.Parameters.AddWithValue("action", action);
            command.Parameters.AddWithValue("delivery_type", key.DeliveryType);
            command.Parameters.AddWithValue("channel", key.Channel);
            command.Parameters.AddWithValue("old_value", oldEnabled);
            command.Parameters.AddWithValue("new_value", newEnabled);
            command.Parameters.AddWithValue("resulting_version", resultingVersion);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
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
}
