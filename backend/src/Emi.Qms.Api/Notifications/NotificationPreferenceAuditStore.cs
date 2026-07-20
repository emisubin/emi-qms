using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notifications;

public sealed class NotificationPreferenceAuditStore(
    DatabaseConnectionStringProvider connectionStringProvider)
{
    private const string FilterPredicate = """
        event.occurred_at_utc >= @from_utc
        and event.occurred_at_utc < @to_utc
        and (@action is null or event.action = @action)
        and (@delivery_type is null or event.delivery_type = @delivery_type)
        and (
            @search_pattern is null
            or target_user.display_name ilike '%' || @search_pattern || '%' escape E'\\'
            or actor_user.display_name ilike '%' || @search_pattern || '%' escape E'\\'
            or target_department.name ilike '%' || @search_pattern || '%' escape E'\\'
            or actor_department.name ilike '%' || @search_pattern || '%' escape E'\\'
        )
        """;

    public async Task<NotificationPreferenceAuditListResponse> ListAsync(
        DateOnly fromDate,
        DateOnly toDate,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? action,
        string? deliveryType,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var normalizedSearch = EscapeLikePattern(search);
        var summary = await ReadSummaryAsync(
            connection, fromUtc, toUtc, action, deliveryType, normalizedSearch, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select event.id,
                   event.occurred_at_utc,
                   coalesce(target_user.display_name, '알 수 없는 사용자'),
                   target_department.name,
                   coalesce(target_user.is_active, false),
                   coalesce(actor_user.display_name, '알 수 없는 사용자'),
                   actor_department.name,
                   coalesce(actor_user.is_active, false),
                   event.action,
                   event.delivery_type,
                   event.channel,
                   event.old_value,
                   event.new_value,
                   event.resulting_version
            from user_notification_preference_audit_events event
            left join qms_users target_user on target_user.id = event.target_user_id
            left join departments target_department on target_department.id = target_user.department_id
            left join qms_users actor_user on actor_user.id = event.actor_user_id
            left join departments actor_department on actor_department.id = actor_user.department_id
            where {FilterPredicate}
            order by event.occurred_at_utc desc, event.id desc
            offset @offset_rows
            limit @page_size;
            """;
        AddFilterParameters(command, fromUtc, toUtc, action, deliveryType, normalizedSearch);
        command.Parameters.AddWithValue("offset_rows", (page - 1) * pageSize);
        command.Parameters.AddWithValue("page_size", pageSize);

        var items = new List<NotificationPreferenceAuditItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return new NotificationPreferenceAuditListResponse(
            items,
            page,
            pageSize,
            summary.TotalChanges,
            summary,
            "사용자명과 부서는 변경 당시 snapshot이 아니라 현재 계정 정보입니다.",
            fromDate,
            toDate);
    }

    public async Task<IReadOnlyList<NotificationPreferenceAuditItemResponse>> ListByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Count > 500)
        {
            return [];
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select event.id,
                   event.occurred_at_utc,
                   coalesce(target_user.display_name, '알 수 없는 사용자'),
                   target_department.name,
                   coalesce(target_user.is_active, false),
                   coalesce(actor_user.display_name, '알 수 없는 사용자'),
                   actor_department.name,
                   coalesce(actor_user.is_active, false),
                   event.action,
                   event.delivery_type,
                   event.channel,
                   event.old_value,
                   event.new_value,
                   event.resulting_version
            from user_notification_preference_audit_events event
            left join qms_users target_user on target_user.id = event.target_user_id
            left join departments target_department on target_department.id = target_user.department_id
            left join qms_users actor_user on actor_user.id = event.actor_user_id
            left join departments actor_department on actor_department.id = actor_user.department_id
            where event.id = any(@ids)
            order by event.occurred_at_utc desc, event.id desc;
            """);
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() });

        var items = new List<NotificationPreferenceAuditItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static async Task<NotificationPreferenceAuditSummaryResponse> ReadSummaryAsync(
        NpgsqlConnection connection,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? action,
        string? deliveryType,
        string? searchPattern,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select count(*)::integer,
                   count(*) filter (where event.action in ('Save', 'Reset'))::integer,
                   count(*) filter (where event.action in ('AdminSave', 'AdminReset'))::integer,
                   count(*) filter (where event.new_value = false)::integer
            from user_notification_preference_audit_events event
            left join qms_users target_user on target_user.id = event.target_user_id
            left join departments target_department on target_department.id = target_user.department_id
            left join qms_users actor_user on actor_user.id = event.actor_user_id
            left join departments actor_department on actor_department.id = actor_user.department_id
            where {FilterPredicate};
            """;
        AddFilterParameters(command, fromUtc, toUtc, action, deliveryType, searchPattern);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new NotificationPreferenceAuditSummaryResponse(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    private static void AddFilterParameters(
        NpgsqlCommand command,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? action,
        string? deliveryType,
        string? searchPattern)
    {
        command.Parameters.AddWithValue("from_utc", fromUtc);
        command.Parameters.AddWithValue("to_utc", toUtc);
        command.Parameters.Add(new NpgsqlParameter("action", NpgsqlDbType.Text) { Value = (object?)action ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("delivery_type", NpgsqlDbType.Text) { Value = (object?)deliveryType ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("search_pattern", NpgsqlDbType.Text) { Value = (object?)searchPattern ?? DBNull.Value });
    }

    private static NotificationPreferenceAuditItemResponse ReadItem(NpgsqlDataReader reader)
    {
        var action = reader.GetString(8);
        var deliveryType = reader.GetString(9);
        var channel = reader.GetString(10);
        var oldValue = reader.GetBoolean(11);
        var newValue = reader.GetBoolean(12);
        return new NotificationPreferenceAuditItemResponse(
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetBoolean(7),
            action,
            ActionLabel(action),
            deliveryType,
            DeliveryTypeLabel(deliveryType),
            channel,
            ChannelLabel(channel),
            oldValue,
            newValue,
            $"{ValueLabel(oldValue)} → {ValueLabel(newValue)}",
            reader.GetInt64(13));
    }

    private static string ActionLabel(string action) => action switch
    {
        "Save" => "사용자 직접 변경",
        "Reset" => "사용자 기본값 복원",
        "AdminSave" => "관리자 대리 변경",
        "AdminReset" => "관리자 기본값 복원",
        _ => action
    };

    private static string DeliveryTypeLabel(string deliveryType) => deliveryType switch
    {
        NotificationDeliveryTypes.WorkItemCreated => "업무 배정",
        NotificationDeliveryTypes.DueSoonL0 => "예정일 임박 D-1",
        NotificationDeliveryTypes.DailyDigest => "일일 업무 요약",
        _ => deliveryType
    };

    private static string ChannelLabel(string channel) => channel switch
    {
        NotificationDeliveryChannels.TeamsDirectMessage => "Teams 개인 알림",
        NotificationDeliveryChannels.Mail => "메일",
        _ => channel
    };

    private static string ValueLabel(bool value) => value ? "켬" : "끔";

    private static string? EscapeLikePattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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
