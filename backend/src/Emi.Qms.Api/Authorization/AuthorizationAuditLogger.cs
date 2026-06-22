using System.Security.Claims;
using Npgsql;

namespace Emi.Qms.Api.Authorization;

public interface IAuthorizationAuditLogger
{
    Task LogDeniedAsync(
        ClaimsPrincipal user,
        HttpContext? httpContext,
        string reason,
        string? targetProjectKey,
        CancellationToken cancellationToken);
}

public sealed class AuthorizationAuditLogger(
    DatabaseConnectionStringProvider connectionStringProvider,
    ILogger<AuthorizationAuditLogger> logger)
    : IAuthorizationAuditLogger
{
    public async Task LogDeniedAsync(
        ClaimsPrincipal user,
        HttpContext? httpContext,
        string reason,
        string? targetProjectKey,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(QmsClaimTypes.UserId)?.Value ?? "anonymous";
        var endpoint = httpContext?.GetEndpoint()?.DisplayName ?? "unknown";

        logger.LogWarning(
            "Authorization denied. userId={UserId} reason={Reason} endpoint={Endpoint} targetProject={TargetProject}",
            userId,
            reason,
            endpoint,
            targetProjectKey ?? "none");

        await TryWriteDatabaseEventAsync(userId, reason, endpoint, targetProjectKey, cancellationToken);
    }

    private async Task TryWriteDatabaseEventAsync(
        string userId,
        string reason,
        string endpoint,
        string? targetProjectKey,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var command = dataSource.CreateCommand("""
                insert into authorization_audit_events (user_id, reason, endpoint, target_project_key)
                values (@user_id, @reason, @endpoint, @target_project_key);
                """);

            command.Parameters.AddWithValue(
                "user_id",
                Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : DBNull.Value);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("endpoint", endpoint);
            command.Parameters.AddWithValue("target_project_key", targetProjectKey ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception, "Authorization denial database audit write was skipped.");
        }
    }
}
