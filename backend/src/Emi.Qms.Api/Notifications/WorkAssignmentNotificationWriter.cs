using Npgsql;

namespace Emi.Qms.Api.Notifications;

internal static class WorkAssignmentNotificationWriter
{
    public static async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid workItemId,
        Guid primaryUserId,
        IReadOnlyList<string> secondaryResponsibilityTypes,
        string title,
        string message,
        string linkUrl,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Guid notificationId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into notifications (
                    project_id, notification_type, severity, title, message, link_url,
                    idempotency_key, visibility_scope, source_kind, work_item_id
                ) values (
                    @project_id, 'Info', 'Info', @title, @message, @link_url,
                    @idempotency_key, 'RecipientOnly', 'WorkAssignment', @work_item_id
                )
                on conflict (idempotency_key) do update
                set title=excluded.title, message=excluded.message, link_url=excluded.link_url, work_item_id=excluded.work_item_id
                returning id;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("title", $"새 업무 · {title}");
            command.Parameters.AddWithValue("message", message);
            command.Parameters.AddWithValue("link_url", linkUrl);
            command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            command.Parameters.AddWithValue("work_item_id", workItemId);
            notificationId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
        }

        var recipientIds = new List<Guid> { primaryUserId };
        if (secondaryResponsibilityTypes.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select distinct assigned_user_id
                from project_assignees
                where project_id=@project_id and responsibility_type=any(@responsibilities);
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("responsibilities", secondaryResponsibilityTypes.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) recipientIds.Add(reader.GetGuid(0));
        }

        foreach (var recipientId in recipientIds.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into notification_recipients (notification_id, user_id)
                values (@notification_id, @user_id)
                on conflict (notification_id, user_id) do nothing;
                """;
            command.Parameters.AddWithValue("notification_id", notificationId);
            command.Parameters.AddWithValue("user_id", recipientId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
