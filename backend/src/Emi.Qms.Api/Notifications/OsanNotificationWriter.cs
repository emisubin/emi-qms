using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notifications;

public enum OsanNotificationKind { ProjectCreated, StepCompleted, StepRejected, StepEdited, ProjectCompleted }

public sealed record OsanNotificationSnapshot(
    OsanNotificationKind Kind, string ProjectName, string ProjectCode, string PartCategory,
    string CustomerName, int Quantity, DateOnly? DueDate, string ActorName,
    DateTimeOffset OccurredAt, string? StepName, string[] Targets, string? Comment, int PhotoCount);

/// <summary>Writes immutable in-app and mail snapshots in the caller's Osan business transaction.</summary>
public static class OsanNotificationWriter
{
    public static string IdempotencyKey(Guid projectId, Guid operationId, OsanNotificationKind kind) =>
        kind == OsanNotificationKind.ProjectCompleted
            ? $"osan:project:{projectId:D}:first-completed"
            : $"osan:project:{projectId:D}:{kind}:{operationId:D}";

    public static async Task WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid operationId,
        OsanNotificationKind kind, Guid actorId, DateTimeOffset occurredAt, CancellationToken cancellationToken,
        string? stepName = null, IReadOnlyList<Guid>? targetIds = null, string? comment = null,
        int photoCount = 0, IReadOnlyList<Guid>? recipientIds = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind == OsanNotificationKind.StepRejected && recipientIds is null)
            throw new ArgumentException("Rejection requires explicit contributor recipients.", nameof(recipientIds));
        OsanNotificationSnapshot snapshot;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                select p.project_title, p.project_code, coalesce(p.osan_product_name,''),
                    p.customer_name, p.osan_quantity, p.delivery_date, u.display_name
                from projects p cross join qms_users u
                where p.id=@project and p.project_profile='Osan' and u.id=@actor;
                """;
            read.Parameters.AddWithValue("project", projectId);
            read.Parameters.AddWithValue("actor", actorId);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Osan notification project/actor boundary mismatch.");
            snapshot = new(kind, reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
                reader.GetString(6), occurredAt, stepName, [], comment, photoCount);
        }
        var targets = new List<string>();
        if (targetIds is { Count: > 0 })
        {
            await using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = "select display_name from osan_project_targets where project_id=@project and id=any(@ids) order by sequence_number;";
            read.Parameters.AddWithValue("project", projectId);
            read.Parameters.AddWithValue("ids", targetIds.Distinct().ToArray());
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) targets.Add(reader.GetString(0));
            if (targets.Count != targetIds.Distinct().Count())
                throw new InvalidOperationException("Osan notification target boundary mismatch.");
        }
        snapshot = snapshot with { Targets = targets.ToArray() };
        if (kind == OsanNotificationKind.ProjectCompleted)
        {
            await using var marker = connection.CreateCommand();
            marker.Transaction = transaction;
            marker.CommandText = """
                insert into osan_project_completion_notifications(project_id,first_completed_at)
                values(@project,@time) on conflict(project_id) do nothing returning project_id;
                """;
            marker.Parameters.AddWithValue("project",projectId);
            marker.Parameters.AddWithValue("time",occurredAt);
            if (await marker.ExecuteScalarAsync(cancellationToken) is not Guid) return;
        }
        var content = OsanNotificationTemplates.Render(snapshot);
        var link = targetIds is { Count: > 0 }
            ? $"/progress?projectId={projectId:D}&targetId={targetIds[0]:D}&stage={Uri.EscapeDataString(stepName ?? "")}&businessUnit=OSAN"
            : $"/projects/{projectId:D}?businessUnit=OSAN";
        Guid notificationId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into notifications(project_id,notification_type,severity,title,message,link_url,
                    idempotency_key,visibility_scope,source_kind,created_at_utc)
                values(@project,'Info',@severity,@title,@message,@link,@key,'RecipientOnly','OsanWorkflow',@time)
                on conflict(idempotency_key) do nothing returning id;
                """;
            insert.Parameters.AddWithValue("project", projectId);
            insert.Parameters.AddWithValue("severity", kind == OsanNotificationKind.StepRejected ? "Warning" : "Info");
            insert.Parameters.AddWithValue("title", content.Title);
            insert.Parameters.AddWithValue("message", content.Message);
            insert.Parameters.AddWithValue("link", link);
            insert.Parameters.AddWithValue("key", IdempotencyKey(projectId, operationId, kind));
            insert.Parameters.AddWithValue("time", occurredAt);
            if (await insert.ExecuteScalarAsync(cancellationToken) is not Guid id) return;
            notificationId = id;
        }
        await using var write = connection.CreateCommand();
        write.Transaction = transaction;
        write.CommandText = """
            insert into notification_recipients(notification_id,user_id)
            select @notification,id from qms_users
            where is_active=true and (@all or id=any(@recipients))
                and (auth_provider <> 'EntraId' or exists(select 1 from user_roles ur where ur.user_id=qms_users.id))
            on conflict(notification_id,user_id) do nothing;
            insert into notification_deliveries(notification_id,notification_recipient_id,recipient_user_id,project_id,
                channel,delivery_type,dedupe_key,group_key,next_attempt_at_utc,display_title,display_message,
                display_project_name,display_recipient_name,display_recipient_email,display_recipient_kind,manual_payload_json)
            select @notification,r.id,r.user_id,@project,'Mail','OsanWorkflow',
                'osan-mail:'||@notification::text||':'||r.user_id::text,@notification::text,@time,
                @subject,@message,@project_name,u.display_name,u.email,'User',@payload
            from notification_recipients r join qms_users u on u.id=r.user_id
            where r.notification_id=@notification
            on conflict do nothing;
            """;
        write.Parameters.AddWithValue("notification", notificationId);
        write.Parameters.AddWithValue("project", projectId);
        write.Parameters.AddWithValue("all", recipientIds is null);
        write.Parameters.AddWithValue("recipients", recipientIds?.Distinct().ToArray() ?? []);
        write.Parameters.AddWithValue("time", occurredAt);
        write.Parameters.AddWithValue("subject", content.Subject);
        write.Parameters.AddWithValue("message", content.Message);
        write.Parameters.AddWithValue("project_name", snapshot.ProjectName);
        write.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(snapshot));
        await write.ExecuteNonQueryAsync(cancellationToken);
    }
}
