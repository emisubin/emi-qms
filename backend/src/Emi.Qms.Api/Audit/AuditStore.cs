using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Audit;

public sealed class AuditStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    TimeProvider timeProvider,
    ILogger<AuditStore> logger)
{
    private const string UnifiedSelect = """
        select event.id,
               'Global'::text as source,
               event.occurred_at_utc,
               event.event_type,
               event.actor_user_id,
               event.actor_display_name,
               event.actor_department_name,
               event.actual_actor_user_id,
               event.actual_actor_display_name,
               event.domain,
               event.action,
               event.target_type,
               event.target_key,
               event.outcome,
               event.failure_reason,
               event.reason_summary,
               event.login_correlation_id,
               (select count(*)::integer from audit_event_changes change where change.audit_event_id = event.id),
               host(event.client_ip),
               event.browser_family,
               event.os_family,
               event.app_access_outcome,
               null::timestamptz as last_activity_at_utc,
               null::timestamptz as ended_at_utc,
               null::text as site_access_status,
               null::text[] as menu_codes,
               (select coverage_started_at_utc from site_access_coverage_state where singleton)
                   as site_access_coverage_started_at_utc
        from audit_events event
        where event.occurred_at_utc >= (select coverage_started_at_utc from audit_coverage_state where singleton)
        union all
        select denied.id,
               'Authorization'::text,
               denied.occurred_at_utc,
               'AuthorizationDenied'::text,
               denied.user_id,
               coalesce(qms_user.display_name, '알 수 없는 사용자'),
               department.name,
               denied.actual_actor_user_id,
               actual_user.display_name,
               'Authorization'::text,
               denied.endpoint,
               case when denied.target_project_key is null then null else 'project' end,
               denied.target_project_key,
               'Rejected'::text,
               denied.reason,
               '권한 확인 단계에서 요청이 거절되었습니다.'::text,
               null::uuid,
               0::integer,
               null::text,
               null::text,
               null::text,
               null::text,
               null::timestamptz,
               null::timestamptz,
               null::text,
               null::text[],
               (select coverage_started_at_utc from site_access_coverage_state where singleton)
        from authorization_audit_events denied
        left join qms_users qms_user on qms_user.id = denied.user_id
        left join departments department on department.id = qms_user.department_id
        left join qms_users actual_user on actual_user.id = denied.actual_actor_user_id
        where denied.occurred_at_utc >= (select coverage_started_at_utc from audit_coverage_state where singleton)
        union all
        select access.id,
               'SiteAccess'::text,
               access.started_at_utc,
               'SiteAccess'::text,
               access.actor_user_id,
               access.actor_display_name,
               access.actor_department_name,
               null::uuid,
               null::text,
               'Identity'::text,
               'SiteAccess'::text,
               null::text,
               null::text,
               case
                   when access.ended_at_utc is not null then 'ExplicitLogout'
                   when access.last_activity_at_utc > @current_utc - interval '30 minutes' then 'RecentSignal'
                   else 'TimedOut'
               end,
               null::text,
               null::text,
               null::uuid,
               0::integer,
               host(access.client_ip),
               access.browser_family,
               access.os_family,
               access.app_access_outcome,
               access.last_activity_at_utc,
               access.ended_at_utc,
               case
                   when access.ended_at_utc is not null then 'ExplicitLogout'
                   when access.last_activity_at_utc > @current_utc - interval '30 minutes' then 'RecentSignal'
                   else 'TimedOut'
               end,
               access.menu_codes,
               (select coverage_started_at_utc from site_access_coverage_state where singleton)
        from site_access_sessions access
        where access.started_at_utc >= (
            select coverage_started_at_utc from site_access_coverage_state where singleton)
        """;

    private const string FilterPredicate = """
        unified.occurred_at_utc >= @from_utc
        and unified.occurred_at_utc < @to_utc
        and (@actor_id is null or unified.actor_user_id = @actor_id)
        and (@domain is null or unified.domain = @domain)
        and (@action is null or unified.action = @action)
        and (@event_type is null or unified.event_type = @event_type)
        and (@failure_reason is null or unified.failure_reason = @failure_reason)
        and (
            @search_pattern is null
            or unified.actor_display_name ilike '%' || @search_pattern || '%' escape E'\\'
            or coalesce(unified.actor_department_name, '') ilike '%' || @search_pattern || '%' escape E'\\'
            or coalesce(unified.actual_actor_display_name, '') ilike '%' || @search_pattern || '%' escape E'\\'
            or unified.action ilike '%' || @search_pattern || '%' escape E'\\'
            or coalesce(unified.target_key, '') ilike '%' || @search_pattern || '%' escape E'\\'
        )
        """;

    public async Task<AuditSessionResponse> AppendInteractiveLoginAsync(
        Guid actorUserId,
        Guid clientInteractionId,
        string appAccessOutcome,
        System.Net.IPAddress? clientIp,
        string browserFamily,
        string osFamily,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select event_id, login_correlation_id, idempotency_receipt
            from qms_append_audit_login_event(
                @actor_user_id,
                @client_interaction_id,
                @app_access_outcome,
                @client_ip,
                @browser_family,
                @os_family);
            """);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("client_interaction_id", clientInteractionId);
        command.Parameters.AddWithValue("app_access_outcome", appAccessOutcome);
        command.Parameters.Add(new NpgsqlParameter("client_ip", NpgsqlDbType.Inet)
        {
            Value = (object?)clientIp ?? DBNull.Value
        });
        command.Parameters.AddWithValue("browser_family", browserFamily);
        command.Parameters.AddWithValue("os_family", osFamily);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Interactive login audit did not return a receipt.");
        }

        return new AuditSessionResponse(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }

    public async Task<bool> ResolveOwnedSessionAsync(
        Guid actorUserId,
        Guid loginCorrelationId,
        Guid receipt,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select qms_resolve_audit_login_session(
                @actor_user_id,
                @login_correlation_id,
                @idempotency_receipt);
            """);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("login_correlation_id", loginCorrelationId);
        command.Parameters.AddWithValue("idempotency_receipt", receipt);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> AppendLogoutAsync(
        Guid actorUserId,
        Guid loginCorrelationId,
        Guid receipt,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select qms_append_audit_logout_event(
                @actor_user_id,
                @login_correlation_id,
                @idempotency_receipt);
            """);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("login_correlation_id", loginCorrelationId);
        command.Parameters.AddWithValue("idempotency_receipt", receipt);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<SiteAccessSessionResponse> RecordSiteAccessAsync(
        Guid actorUserId,
        Guid browserClientId,
        string menuCode,
        string appAccessOutcome,
        System.Net.IPAddress? clientIp,
        string browserFamily,
        string osFamily,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select session_id, idempotency_receipt, started_at_utc, last_activity_at_utc, created
            from qms_record_site_access(
                @actor_user_id,
                @browser_client_id,
                @menu_code,
                @app_access_outcome,
                @client_ip,
                @browser_family,
                @os_family);
            """);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("browser_client_id", browserClientId);
        command.Parameters.AddWithValue("menu_code", menuCode);
        command.Parameters.AddWithValue("app_access_outcome", appAccessOutcome);
        command.Parameters.Add(new NpgsqlParameter("client_ip", NpgsqlDbType.Inet)
        {
            Value = (object?)clientIp ?? DBNull.Value
        });
        command.Parameters.AddWithValue("browser_family", browserFamily);
        command.Parameters.AddWithValue("os_family", osFamily);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Site access signal did not return a session receipt.");
        }

        return new SiteAccessSessionResponse(
            reader.GetGuid(0),
            reader.GetGuid(1),
            ToUtcDateTimeOffset(reader.GetValue(2)),
            ToUtcDateTimeOffset(reader.GetValue(3)),
            reader.GetBoolean(4));
    }

    public async Task<bool> EndSiteAccessAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid receipt,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select qms_end_site_access(
                @actor_user_id,
                @session_id,
                @idempotency_receipt);
            """);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("idempotency_receipt", receipt);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task TryAppendFailedMutationAsync(
        Guid actorUserId,
        Guid? actualActorUserId,
        AuditMutationDefinition definition,
        string failureReason,
        Guid? loginCorrelationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dataSource = CreateDataSource();
            await using var command = dataSource.CreateCommand("""
                select qms_append_audit_failed_mutation(
                    @actor_user_id,
                    @actual_actor_user_id,
                    @domain,
                    @action,
                    @route_key,
                    @target_type,
                    @target_key,
                    @failure_reason,
                    @login_correlation_id);
                """);
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            AddNullableUuid(command, "actual_actor_user_id", actualActorUserId);
            command.Parameters.AddWithValue("domain", definition.Domain);
            command.Parameters.AddWithValue("action", definition.Action);
            command.Parameters.AddWithValue("route_key", definition.RouteKey);
            command.Parameters.AddWithValue("target_type", definition.TargetType);
            AddNullableText(command, "target_key", definition.TargetKey);
            command.Parameters.AddWithValue("failure_reason", failureReason);
            AddNullableUuid(command, "login_correlation_id", loginCorrelationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Best-effort failed mutation audit write failed. routeKey={RouteKey} reason={FailureReason}",
                definition.RouteKey,
                failureReason);
        }
    }

    public async Task<AuditListResponse> ListAsync(
        DateOnly fromDate,
        DateOnly toDate,
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var coverage = await ReadCoverageAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            with unified as ({UnifiedSelect}),
            filtered as (
                select * from unified where {FilterPredicate}
            )
            select count(*)::integer,
                   count(*) filter (where event_type = 'Login')::integer,
                   count(*) filter (where event_type = 'MutationSucceeded')::integer,
                   count(*) filter (where event_type = 'MutationFailed')::integer,
                   count(*) filter (where event_type = 'AuthorizationDenied')::integer,
                   count(*) filter (where event_type = 'SiteAccess')::integer
            from filtered;

            with unified as ({UnifiedSelect})
            select *
            from unified
            where {FilterPredicate}
            order by occurred_at_utc desc, id desc
            offset @offset_rows
            limit @page_size;
            """;
        AddFilterParameters(command, query);
        command.Parameters.AddWithValue("current_utc", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("offset_rows", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var summary = new AuditSummaryResponse(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5));

        await reader.NextResultAsync(cancellationToken);
        var items = new List<AuditListItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }

        return new AuditListResponse(
            items,
            query.Page,
            query.PageSize,
            summary.TotalEvents,
            summary,
            coverage,
            fromDate,
            toDate);
    }

    public async Task<AuditDetailResponse?> GetDetailAsync(
        Guid eventId,
        string source,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            with unified as ({UnifiedSelect})
            select * from unified where id = @event_id and source = @source;

            select id, row_action, target_type, target_key, field_code, projection_kind,
                   before_value, after_value, before_length, after_length
            from audit_event_changes
            where audit_event_id = @event_id
              and @source = 'Global'
            order by id;

            select login_event.occurred_at_utc,
                   host(login_event.client_ip),
                   login_event.browser_family,
                   login_event.os_family,
                   login_event.app_access_outcome
            from audit_events login_event
            where login_event.event_type = 'Login'
              and login_event.login_correlation_id = (
                  select linked_event.login_correlation_id
                  from audit_events linked_event
                  where linked_event.id = @event_id
                    and @source = 'Global')
            limit 1;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("current_utc", timeProvider.GetUtcNow());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var item = ReadItem(reader);
        await reader.NextResultAsync(cancellationToken);
        var changes = new List<AuditChangeResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            changes.Add(new AuditChangeResponse(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9)));
        }

        await reader.NextResultAsync(cancellationToken);
        AuditLoginContextResponse? loginContext = null;
        if (await reader.ReadAsync(cancellationToken))
        {
            loginContext = new AuditLoginContextResponse(
                ToUtcDateTimeOffset(reader.GetValue(0)),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4));
        }

        return new AuditDetailResponse(
            item,
            changes,
            loginContext,
            "고정 형식 값만 변경 전후를 표시합니다. 본문·댓글·설명 등 자유 입력은 원문 없이 길이 변화만 표시합니다.");
    }

    public async Task<IReadOnlyList<AuditListItemResponse>> ListByIdsAsync(
        IReadOnlyList<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0 || eventIds.Count > 1_000)
        {
            return [];
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            with unified as ({UnifiedSelect})
            select * from unified
            where id = any(@event_ids)
            order by occurred_at_utc desc, id desc;
            """);
        command.Parameters.Add(new NpgsqlParameter("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = eventIds.ToArray()
        });
        command.Parameters.AddWithValue("current_utc", timeProvider.GetUtcNow());

        var items = new List<AuditListItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadItem(reader));
        }
        return items;
    }

    private static AuditListItemResponse ReadItem(NpgsqlDataReader reader)
    {
        var menuCodes = reader.IsDBNull(25) ? [] : reader.GetFieldValue<string[]>(25);
        return new AuditListItemResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            ToUtcDateTimeOffset(reader.GetValue(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.GetInt32(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20),
            reader.IsDBNull(21) ? null : reader.GetString(21),
            reader.IsDBNull(22) ? null : ToUtcDateTimeOffset(reader.GetValue(22)),
            reader.IsDBNull(23) ? null : ToUtcDateTimeOffset(reader.GetValue(23)),
            reader.IsDBNull(24) ? null : reader.GetString(24),
            menuCodes,
            menuCodes.Select(code => SiteAccessMenuCodes.Labels.GetValueOrDefault(code, code)).ToArray(),
            ToUtcDateTimeOffset(reader.GetValue(26)));
    }

    private static async Task<AuditCoverageResponse> ReadCoverageAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select audit.coverage_started_at_utc, site.coverage_started_at_utc
            from audit_coverage_state audit
            cross join site_access_coverage_state site
            where audit.singleton and site.singleton;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Audit coverage state is missing.");
        }
        var startedAt = ToUtcDateTimeOffset(reader.GetValue(0));
        var siteStartedAt = ToUtcDateTimeOffset(reader.GetValue(1));
        return new AuditCoverageResponse(
            startedAt,
            $"{startedAt:yyyy-MM-dd HH:mm:ss} UTC 이후 변경·인증 기록입니다.",
            siteStartedAt,
            $"{siteStartedAt:yyyy-MM-dd HH:mm:ss} UTC 이후 사이트 접속 기록입니다.",
            "마지막 활동 시각은 페이지 진입 또는 새로고침 신호이며 실제 근무시간을 의미하지 않습니다.");
    }

    private static DateTimeOffset ToUtcDateTimeOffset(object value) => value switch
    {
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
        DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
        _ => throw new InvalidOperationException($"Unsupported audit timestamp value type: {value.GetType().Name}.")
    };

    private static void AddFilterParameters(NpgsqlCommand command, AuditQuery query)
    {
        command.Parameters.AddWithValue("from_utc", query.FromUtc);
        command.Parameters.AddWithValue("to_utc", query.ToUtc);
        AddNullableUuid(command, "actor_id", query.ActorUserId);
        AddNullableText(command, "domain", query.Domain);
        AddNullableText(command, "action", query.Action);
        AddNullableText(command, "event_type", query.EventType);
        AddNullableText(command, "failure_reason", query.FailureReason);
        AddNullableText(command, "search_pattern", EscapeLikePattern(query.Search));
    }

    private static string? EscapeLikePattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid)
        {
            Value = (object?)value ?? DBNull.Value
        });

    private static void AddNullableText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = (object?)value ?? DBNull.Value
        });

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
