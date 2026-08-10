using Npgsql;

namespace Emi.Qms.Api.Workflow;

internal static class WorkflowStageOperations
{
    internal sealed record ProjectLqcSnapshot(bool IsOperational, Guid TemplateVersionId);

    internal static async Task<bool> IsActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string stageCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce((select is_active from workflow_stages where stage_code = @stage_code), false);";
        command.Parameters.AddWithValue("stage_code", stageCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    internal static async Task<bool> IsStageOperationalForProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string stageCode,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(stageCode, WorkflowStageCodes.LQC, StringComparison.Ordinal))
        {
            return await IsActiveAsync(connection, transaction, stageCode, cancellationToken);
        }

        return (await ReadProjectLqcSnapshotAsync(
            connection,
            transaction,
            projectId,
            cancellationToken))?.IsOperational == true;
    }

    internal static async Task<ProjectLqcSnapshot?> ReadProjectLqcSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select lqc_operational_snapshot, lqc_template_version_id
            from projects
            where id = @project_id
              and deleted_at_utc is null;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectLqcSnapshot(reader.GetBoolean(0), reader.GetGuid(1))
            : null;
    }
}
