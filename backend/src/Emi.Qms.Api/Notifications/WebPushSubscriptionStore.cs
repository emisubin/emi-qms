using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notifications;

public sealed class WebPushSubscriptionStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider) : IWebPushSubscriptionDeliveryStore
{
    public async Task<WebPushConfigurationResponse> GetConfigurationAsync(
        Guid userId,
        NotificationWebPushOptions options,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                count(*) filter (where is_active = true),
                max(greatest(activated_at_utc, coalesce(deactivated_at_utc, activated_at_utc)))
            from web_push_subscriptions
            where user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var activeDeviceCount = checked((int)reader.GetInt64(0));
        DateTimeOffset? lastChangedAtUtc = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        var configured = options.Enabled
            && !string.IsNullOrWhiteSpace(options.PublicKey)
            && WebPushEndpointPolicy.HasValidAllowedHosts(options);
        return new WebPushConfigurationResponse(
            options.Enabled,
            options.DryRun,
            configured,
            configured ? options.PublicKey!.Trim() : null,
            activeDeviceCount,
            lastChangedAtUtc);
    }

    public async Task<WebPushCurrentSubscriptionResponse> GetCurrentStatusAsync(
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var normalizedEndpoint = ValidateEndpoint(endpoint);
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from web_push_subscriptions subscription
                join qms_users users on users.id = subscription.user_id
                where subscription.user_id = @user_id
                  and subscription.endpoint_hash = @endpoint_hash
                  and subscription.is_active = true
                  and users.is_active = true
            );
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("endpoint_hash", HashEndpoint(normalizedEndpoint));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return new WebPushCurrentSubscriptionResponse(value is true);
    }

    public async Task<WebPushSubscriptionMutationResponse> UpsertAsync(
        Guid userId,
        WebPushSubscriptionRequest request,
        NotificationWebPushOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = ValidateEndpoint(request.Endpoint);
        WebPushEndpointPolicy.EnsureAllowed(endpoint, options);
        var p256dh = ValidateKey(request.Keys?.P256dh, "p256dh", 1024);
        var auth = ValidateKey(request.Keys?.Auth, "auth", 512);
        WebPushKeyValidation.EnsureValidSubscriptionKeys(p256dh, auth);
        var endpointHash = HashEndpoint(endpoint);
        var now = timeProvider.GetUtcNow();

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await WebPushAdvisoryLock.AcquireUserAsync(connection, transaction, userId, cancellationToken);
        await WebPushAdvisoryLock.AcquireEndpointAsync(connection, transaction, endpointHash, cancellationToken);

        if (!await IsActiveUserAsync(connection, transaction, userId, cancellationToken))
        {
            throw new InvalidOperationException("비활성 사용자는 푸시 알림을 켤 수 없습니다.");
        }

        Guid subscriptionId;
        string? eventType = "Registered";
        var consumesActiveDeviceSlot = true;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = """
                select id, user_id, is_active, p256dh_key, auth_key
                from web_push_subscriptions
                where endpoint_hash = @endpoint_hash
                for update;
                """;
            existing.Parameters.AddWithValue("endpoint_hash", endpointHash);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                subscriptionId = reader.GetGuid(0);
                var existingUserId = reader.GetGuid(1);
                var wasActive = reader.GetBoolean(2);
                var keysChanged = !string.Equals(reader.GetString(3), p256dh, StringComparison.Ordinal)
                    || !string.Equals(reader.GetString(4), auth, StringComparison.Ordinal);
                consumesActiveDeviceSlot = existingUserId != userId || !wasActive;
                eventType = existingUserId != userId || !wasActive || keysChanged ? "Reactivated" : null;
            }
            else
            {
                subscriptionId = Guid.NewGuid();
            }
        }

        if (consumesActiveDeviceSlot
            && await CountActiveAsync(connection, transaction, userId, cancellationToken) >= options.MaxActiveDevicesPerUser)
        {
            throw new InvalidOperationException(
                $"푸시 알림은 계정당 최대 {options.MaxActiveDevicesPerUser}개 기기에서만 켤 수 있습니다. 모든 기기 연결을 해제한 뒤 다시 시도해 주세요.");
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                insert into web_push_subscriptions (
                    id, user_id, endpoint, endpoint_hash, p256dh_key, auth_key,
                    is_active, activated_at_utc, created_at_utc, updated_at_utc
                )
                values (
                    @id, @user_id, @endpoint, @endpoint_hash, @p256dh_key, @auth_key,
                    true, @now, @now, @now
                )
                on conflict (endpoint_hash) do update
                set user_id = excluded.user_id,
                    endpoint = excluded.endpoint,
                    p256dh_key = excluded.p256dh_key,
                    auth_key = excluded.auth_key,
                    is_active = true,
                    activated_at_utc = case
                        when web_push_subscriptions.user_id <> excluded.user_id
                          or web_push_subscriptions.is_active = false
                          or web_push_subscriptions.p256dh_key <> excluded.p256dh_key
                          or web_push_subscriptions.auth_key <> excluded.auth_key
                        then excluded.activated_at_utc
                        else web_push_subscriptions.activated_at_utc
                    end,
                    generation = case
                        when web_push_subscriptions.user_id <> excluded.user_id
                          or web_push_subscriptions.is_active = false
                          or web_push_subscriptions.p256dh_key <> excluded.p256dh_key
                          or web_push_subscriptions.auth_key <> excluded.auth_key
                        then web_push_subscriptions.generation + 1
                        else web_push_subscriptions.generation
                    end,
                    deactivated_at_utc = null,
                    deactivation_reason = null,
                    consecutive_failure_count = 0,
                    last_failure_at_utc = null,
                    last_failure_code = null,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            upsert.Parameters.AddWithValue("id", subscriptionId);
            upsert.Parameters.AddWithValue("user_id", userId);
            upsert.Parameters.AddWithValue("endpoint", endpoint);
            upsert.Parameters.AddWithValue("endpoint_hash", endpointHash);
            upsert.Parameters.AddWithValue("p256dh_key", p256dh);
            upsert.Parameters.AddWithValue("auth_key", auth);
            upsert.Parameters.AddWithValue("now", now);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        if (eventType is not null)
        {
            await InsertEventAsync(connection, transaction, subscriptionId, userId, eventType, null, now, cancellationToken);
        }
        var activeDeviceCount = await CountActiveAsync(connection, transaction, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WebPushSubscriptionMutationResponse(true, activeDeviceCount, now);
    }

    public async Task<WebPushSubscriptionMutationResponse> DeactivateCurrentAsync(
        Guid userId,
        string endpoint,
        string reason,
        CancellationToken cancellationToken)
    {
        var normalizedEndpoint = ValidateEndpoint(endpoint);
        var endpointHash = HashEndpoint(normalizedEndpoint);
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await WebPushAdvisoryLock.AcquireUserAsync(connection, transaction, userId, cancellationToken);
        await WebPushAdvisoryLock.AcquireEndpointAsync(connection, transaction, endpointHash, cancellationToken);

        Guid? subscriptionId = null;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update web_push_subscriptions
                set is_active = false,
                    deactivated_at_utc = @now,
                    deactivation_reason = @reason,
                    updated_at_utc = @now
                where user_id = @user_id
                  and endpoint_hash = @endpoint_hash
                  and is_active = true
                returning id;
                """;
            update.Parameters.AddWithValue("user_id", userId);
            update.Parameters.AddWithValue("endpoint_hash", endpointHash);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("reason", NormalizeReason(reason));
            var value = await update.ExecuteScalarAsync(cancellationToken);
            subscriptionId = value is Guid returnedId ? returnedId : null;
        }

        if (subscriptionId is { } id)
        {
            await InsertEventAsync(connection, transaction, id, userId, "CurrentDeviceDeactivated", NormalizeReason(reason), now, cancellationToken);
        }

        var activeDeviceCount = await CountActiveAsync(connection, transaction, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WebPushSubscriptionMutationResponse(false, activeDeviceCount, now);
    }

    public async Task<WebPushSubscriptionMutationResponse> DeactivateAllAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await WebPushAdvisoryLock.AcquireUserAsync(connection, transaction, userId, cancellationToken);

        var ids = new List<Guid>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select id
                from web_push_subscriptions
                where user_id = @user_id
                  and is_active = true
                for update;
                """;
            select.Parameters.AddWithValue("user_id", userId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }

        if (ids.Count > 0)
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update web_push_subscriptions
                set is_active = false,
                    deactivated_at_utc = @now,
                    deactivation_reason = 'AllDevicesReset',
                    updated_at_utc = @now
                where id = any(@ids);
                """;
            update.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() });
            update.Parameters.AddWithValue("now", now);
            await update.ExecuteNonQueryAsync(cancellationToken);
            foreach (var id in ids)
            {
                await InsertEventAsync(connection, transaction, id, userId, "AllDevicesDeactivated", "AllDevicesReset", now, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new WebPushSubscriptionMutationResponse(false, 0, now);
    }

    public async Task<WebPushDeliveryTarget?> GetDeliveryTargetAsync(
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select
                subscription.id,
                subscription.generation,
                subscription.endpoint,
                subscription.p256dh_key,
                subscription.auth_key,
                subscription.is_active,
                users.is_active
            from notification_deliveries delivery
            join web_push_subscriptions subscription on subscription.id = delivery.web_push_subscription_id
            join qms_users users on users.id = subscription.user_id
            where delivery.id = @delivery_id
              and delivery.channel = 'WebPush'
              and delivery.web_push_subscription_generation = subscription.generation
              and delivery.recipient_user_id = subscription.user_id;
            """);
        command.Parameters.AddWithValue("delivery_id", deliveryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new WebPushDeliveryTarget(
            reader.GetGuid(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6));
    }

    public Task RecordProviderAcceptedAsync(Guid subscriptionId, long expectedGeneration, CancellationToken cancellationToken)
    {
        return UpdateProviderStateAsync(subscriptionId, expectedGeneration, true, null, false, cancellationToken);
    }

    public Task RecordProviderFailureAsync(Guid subscriptionId, long expectedGeneration, string failureCode, CancellationToken cancellationToken)
    {
        return UpdateProviderStateAsync(subscriptionId, expectedGeneration, false, failureCode, false, cancellationToken);
    }

    public Task DeactivateForProviderAsync(Guid subscriptionId, long expectedGeneration, string reason, CancellationToken cancellationToken)
    {
        return UpdateProviderStateAsync(subscriptionId, expectedGeneration, false, reason, true, cancellationToken);
    }

    private async Task UpdateProviderStateAsync(
        Guid subscriptionId,
        long expectedGeneration,
        bool accepted,
        string? failureCode,
        bool deactivate,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid? userId = null;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update web_push_subscriptions
                set last_success_at_utc = case when @accepted then @now else last_success_at_utc end,
                    last_failure_at_utc = case when @accepted then last_failure_at_utc else @now end,
                    last_failure_code = case when @accepted then null else @failure_code end,
                    consecutive_failure_count = case when @accepted then 0 else consecutive_failure_count + 1 end,
                    is_active = case when @deactivate then false else is_active end,
                    deactivated_at_utc = case when @deactivate then @now else deactivated_at_utc end,
                    deactivation_reason = case when @deactivate then @failure_code else deactivation_reason end,
                    updated_at_utc = @now
                where id = @subscription_id
                  and generation = @expected_generation
                returning user_id;
                """;
            update.Parameters.AddWithValue("subscription_id", subscriptionId);
            update.Parameters.AddWithValue("expected_generation", expectedGeneration);
            update.Parameters.AddWithValue("accepted", accepted);
            update.Parameters.AddWithValue("deactivate", deactivate);
            update.Parameters.AddWithValue("failure_code", (object?)failureCode ?? DBNull.Value);
            update.Parameters.AddWithValue("now", now);
            var value = await update.ExecuteScalarAsync(cancellationToken);
            userId = value is Guid id ? id : null;
        }

        if (userId is { } actorUserId)
        {
            await InsertEventAsync(
                connection,
                transaction,
                subscriptionId,
                actorUserId,
                accepted ? "ProviderAccepted" : deactivate ? "ProviderDeactivated" : "ProviderFailed",
                failureCode,
                now,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> IsActiveUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists (select 1 from qms_users where id = @user_id and is_active = true);";
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<int> CountActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*) from web_push_subscriptions where user_id = @user_id and is_active = true;";
        command.Parameters.AddWithValue("user_id", userId);
        return checked((int)(long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L));
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid subscriptionId,
        Guid userId,
        string eventType,
        string? reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into web_push_subscription_events (
                subscription_id, user_id, event_type, reason, created_at_utc
            )
            values (@subscription_id, @user_id, @event_type, @reason, @now);
            """;
        command.Parameters.AddWithValue("subscription_id", subscriptionId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ValidateEndpoint(string? value)
    {
        var endpoint = value?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint)
            || endpoint.Length > 4096
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new ArgumentException("올바른 Web Push 구독 주소가 필요합니다.");
        }

        return endpoint;
    }

    private static string ValidateKey(string? value, string fieldName, int maxLength)
    {
        var key = value?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} 값이 올바르지 않습니다.");
        }

        return key;
    }

    private static string HashEndpoint(string endpoint)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = string.IsNullOrWhiteSpace(reason) ? "UserRequest" : reason.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
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
