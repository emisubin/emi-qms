using Npgsql;

namespace Emi.Qms.Api.Notifications;

internal static class WebPushAdvisoryLock
{
    public static Task AcquireUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return AcquireAsync(connection, transaction, $"web-push-user:{userId:N}", cancellationToken);
    }

    public static Task AcquireEndpointAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string endpointHash,
        CancellationToken cancellationToken)
    {
        return AcquireAsync(connection, transaction, $"web-push-endpoint:{endpointHash}", cancellationToken);
    }

    private static async Task AcquireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
        command.Parameters.AddWithValue("lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
