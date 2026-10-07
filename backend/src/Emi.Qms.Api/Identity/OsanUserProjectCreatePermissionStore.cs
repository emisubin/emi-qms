using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.Identity;

public sealed record OsanUserProjectCreatePermission(
    Guid UserId,
    string DisplayName,
    string? DepartmentCode,
    string? DepartmentName,
    bool Allowed,
    bool IsAdministrator,
    long Version);

public sealed record OsanUserProjectCreatePermissionUpdate(
    Guid UserId,
    bool Allowed,
    long ExpectedVersion);

public sealed record OsanUserProjectCreatePermissionWriteResult(
    int Status,
    string? Code = null,
    string? Message = null,
    IReadOnlyList<OsanUserProjectCreatePermission>? Items = null);

public sealed class OsanUserProjectCreatePermissionStore(OsanDatabase database)
{
    private RuntimeDataSourceLease Source() => database.RentDataSource(database.GetConnectionString()
        ?? throw new InvalidOperationException("QMS database connection string is not configured."));

    public async Task<IReadOnlyList<OsanUserProjectCreatePermission>> ListAsync(CancellationToken cancellationToken)
    {
        await using var source = Source();
        await using var command = source.CreateCommand(ListSql);
        return await ReadRowsAsync(command, cancellationToken);
    }

    public async Task<bool> IsAllowedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var source = Source();
        await using var command = source.CreateCommand("""
            select coalesce(permission.allowed, false)
            from qms_users user_account
            left join osan_user_project_create_permissions permission on permission.user_id = user_account.id
            where user_account.id = @user_id
              and user_account.is_active = true;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<OsanUserProjectCreatePermissionWriteResult> SetAsync(
        IReadOnlyList<OsanUserProjectCreatePermissionUpdate?>? updates,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (updates is null || updates.Count == 0
            || updates.Any(update => update is null || update.UserId == Guid.Empty || update.ExpectedVersion < 0)
            || updates.Where(update => update is not null).Select(update => update!.UserId).Distinct().Count() != updates.Count)
        {
            return new(400, "osan_user_permission_invalid", "변경할 사용자와 버전을 확인해 주세요.");
        }

        var ordered = updates.Select(update => update!).OrderBy(update => update.UserId).ToArray();
        var ids = ordered.Select(update => update.UserId).ToArray();
        await using var source = Source();
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("ids", ids);

        command.CommandText = """
            select user_account.id,
                   exists (
                       select 1
                       from user_roles assignment
                       join roles role on role.id = assignment.role_id
                       where assignment.user_id = user_account.id
                         and role.code = 'system-administrator'
                   ) as is_administrator
            from qms_users user_account
            where user_account.id = any(@ids)
              and user_account.is_active = true
            order by user_account.id
            for update of user_account;
            """;
        var targets = new Dictionary<Guid, bool>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                targets.Add(reader.GetGuid(0), reader.GetBoolean(1));
            }
        }

        if (targets.Count != ids.Length)
        {
            return new(404, "osan_user_not_found", "활성 사용자를 찾을 수 없습니다.");
        }
        if (targets.Values.Any(isAdministrator => isAdministrator))
        {
            return new(400, "osan_administrator_permission_fixed", "관리자의 프로젝트 생성 권한은 항상 허용됩니다.");
        }

        command.CommandText = """
            select user_id, allowed, version
            from osan_user_project_create_permissions
            where user_id = any(@ids)
            order by user_id
            for update;
            """;
        var current = new Dictionary<Guid, (bool Allowed, long Version)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                current.Add(reader.GetGuid(0), (reader.GetBoolean(1), reader.GetInt64(2)));
            }
        }

        foreach (var update in ordered)
        {
            var before = current.GetValueOrDefault(update.UserId, (false, 0L));
            if (before.Item2 != update.ExpectedVersion)
            {
                return new(409, "osan_user_permission_stale", "사용자 권한이 변경되었습니다. 다시 조회해 주세요.");
            }
        }

        foreach (var update in ordered)
        {
            var before = current.GetValueOrDefault(update.UserId, (false, 0L));
            if (before.Item1 == update.Allowed)
            {
                continue;
            }

            var nextVersion = before.Item2 + 1;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("user_id", update.UserId);
            command.Parameters.AddWithValue("allowed", update.Allowed);
            command.Parameters.AddWithValue("version", nextVersion);
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            command.Parameters.AddWithValue("before_allowed", before.Item1);
            command.CommandText = """
                insert into osan_user_project_create_permissions(
                    user_id, allowed, version, updated_at_utc, updated_by_user_id)
                values (@user_id, @allowed, @version, now(), @actor_user_id)
                on conflict (user_id) do update
                set allowed = excluded.allowed,
                    version = excluded.version,
                    updated_at_utc = excluded.updated_at_utc,
                    updated_by_user_id = excluded.updated_by_user_id;

                insert into osan_user_project_create_permission_events(
                    user_id, actor_user_id, before_allowed, after_allowed, version)
                values (@user_id, @actor_user_id, @before_allowed, @allowed, @version);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(200, Items: await ListAsync(cancellationToken));
    }

    private const string ListSql = """
        select user_account.id,
               user_account.display_name,
               department.code,
               department.name,
               case when administrator.is_administrator then true else coalesce(permission.allowed, false) end,
               administrator.is_administrator,
               coalesce(permission.version, 0)
        from qms_users user_account
        left join departments department on department.id = user_account.department_id
        left join osan_user_project_create_permissions permission on permission.user_id = user_account.id
        cross join lateral (
            select exists (
                select 1
                from user_roles assignment
                join roles role on role.id = assignment.role_id
                where assignment.user_id = user_account.id
                  and role.code = 'system-administrator'
            ) as is_administrator
        ) administrator
        where user_account.is_active = true
        order by user_account.display_name, user_account.id;
        """;

    private static async Task<IReadOnlyList<OsanUserProjectCreatePermission>> ReadRowsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<OsanUserProjectCreatePermission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetInt64(6)));
        }
        return rows;
    }
}
