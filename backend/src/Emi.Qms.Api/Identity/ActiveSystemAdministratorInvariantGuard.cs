using Npgsql;

namespace Emi.Qms.Api.Identity;

internal static class ActiveSystemAdministratorInvariantGuard
{
    internal const string LastAdministratorErrorMessage =
        "마지막 System Administrator는 비활성화하거나 역할을 제거하거나 삭제할 수 없습니다.";

    internal static async Task<ActiveSystemAdministratorGuardResult> CheckRemovalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (!await IsCanonicalActiveAdministratorAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken))
        {
            return ActiveSystemAdministratorGuardResult.NotApplicable;
        }

        await LockCanonicalRoleAsync(connection, transaction, cancellationToken);

        if (!await IsCanonicalActiveAdministratorAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken))
        {
            return ActiveSystemAdministratorGuardResult.NotApplicable;
        }

        return await CountOtherCanonicalActiveAdministratorsAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken) == 0
            ? ActiveSystemAdministratorGuardResult.Rejected
            : ActiveSystemAdministratorGuardResult.Allowed;
    }

    internal static async Task<ActiveSystemAdministratorGuardResult> CheckPurgeRemovalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveEntraSystemAdministratorAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken))
        {
            return ActiveSystemAdministratorGuardResult.NotApplicable;
        }

        await LockCanonicalRoleAsync(connection, transaction, cancellationToken);

        if (!await IsActiveEntraSystemAdministratorAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken))
        {
            return ActiveSystemAdministratorGuardResult.NotApplicable;
        }

        return await CountOtherCanonicalActiveAdministratorsAsync(
                connection,
                transaction,
                targetUserId,
                cancellationToken) == 0
            ? ActiveSystemAdministratorGuardResult.Rejected
            : ActiveSystemAdministratorGuardResult.Allowed;
    }

    private static async Task LockCanonicalRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id
            from roles
            where code = @role_code
            for update;
            """;
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Canonical System Administrator role is not configured.");
        }

        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Canonical System Administrator role is not unique.");
        }
    }

    private static async Task<bool> IsCanonicalActiveAdministratorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from qms_users u
                where u.id = @user_id
                  and u.is_active = true
                  and u.auth_provider = 'EntraId'
                  and u.deletion_requested_at_utc is null
                  and u.scheduled_hard_delete_at_utc is null
                  and u.purge_blocked_at_utc is null
                  and exists (
                      select 1
                      from user_roles ur
                      join roles r on r.id = ur.role_id
                      where ur.user_id = u.id
                        and r.code = @role_code
                  )
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> IsActiveEntraSystemAdministratorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from qms_users u
                where u.id = @user_id
                  and u.is_active = true
                  and u.auth_provider = 'EntraId'
                  and exists (
                      select 1
                      from user_roles ur
                      join roles r on r.id = ur.role_id
                      where ur.user_id = u.id
                        and r.code = @role_code
                  )
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<long> CountOtherCanonicalActiveAdministratorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid excludedUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)
            from qms_users u
            where u.id <> @excluded_user_id
              and u.is_active = true
              and u.auth_provider = 'EntraId'
              and u.deletion_requested_at_utc is null
              and u.scheduled_hard_delete_at_utc is null
              and u.purge_blocked_at_utc is null
              and exists (
                  select 1
                  from user_roles ur
                  join roles r on r.id = ur.role_id
                  where ur.user_id = u.id
                    and r.code = @role_code
              );
            """;
        command.Parameters.AddWithValue("excluded_user_id", excludedUserId);
        command.Parameters.AddWithValue("role_code", QmsRoles.SystemAdministrator);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }
}

internal enum ActiveSystemAdministratorGuardResult
{
    NotApplicable,
    Allowed,
    Rejected
}
