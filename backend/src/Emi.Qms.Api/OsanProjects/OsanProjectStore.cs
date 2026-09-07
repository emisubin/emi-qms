using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Projects;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanProjectStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private const string OsanProjectCodeConstraint = "ux_projects_osan_project_code";

    public async Task<OsanProjectListResponse> ListAsync(
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        var where = new List<string>
        {
            "projects.project_profile = 'Osan'",
            "projects.deleted_at_utc is null"
        };
        var parameters = new List<NpgsqlParameter>();
        AddAccessScope(where, parameters, accessScope);

        await using var command = dataSource.CreateCommand($"""
            select
                projects.id,
                projects.project_title,
                projects.project_code,
                projects.customer_name,
                projects.osan_po_number,
                projects.osan_work_order_number,
                projects.delivery_date,
                projects.osan_product_name,
                projects.osan_quantity,
                projects.status,
                projects.created_at_utc
            from projects
            where {string.Join(" and ", where)}
            order by projects.delivery_date, projects.project_code, projects.id;
            """);
        command.Parameters.AddRange(parameters.ToArray());

        var items = new List<OsanProjectListItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadListItem(reader));
        }

        return new OsanProjectListResponse(items);
    }

    public async Task<OsanProjectAccessRecord?> GetAccessRecordAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, project_key
            from projects
            where id = @project_id
              and project_profile = 'Osan'
              and deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OsanProjectAccessRecord(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    public async Task<OsanProjectDetailResponse?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadDetailAsync(connection, null, projectId, cancellationToken);
    }

    public async Task<OsanProjectCreateResult> CreateAsync(
        NormalizedCreateOsanProjectInput input,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var fingerprint = CreateFingerprint(input);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var operationCreated = await TryCreateOperationAsync(
                connection,
                transaction,
                input.OperationId,
                fingerprint,
                userId,
                cancellationToken);

            if (!operationCreated)
            {
                var existing = await ReadOperationAsync(
                    connection,
                    transaction,
                    input.OperationId,
                    cancellationToken);
                if (existing is null
                    || existing.CreatedByUserId != userId
                    || !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                    || existing.ProjectId is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OsanProjectCreateResult(OsanProjectCreateStatus.OperationConflict);
                }

                var replayedProject = await ReadDetailAsync(
                    connection,
                    transaction,
                    existing.ProjectId.Value,
                    cancellationToken);
                if (replayedProject is null)
                {
                    throw new InvalidOperationException("A completed Osan project operation has no project result.");
                }

                await transaction.CommitAsync(cancellationToken);
                return new OsanProjectCreateResult(
                    OsanProjectCreateStatus.Success,
                    new OsanProjectCreateResponse(input.OperationId, true, replayedProject));
            }

            var projectId = Guid.NewGuid();
            await InsertProjectAsync(connection, transaction, projectId, input, userId, cancellationToken);
            await InsertCreatorAccessAsync(connection, transaction, projectId, userId, cancellationToken);
            await InsertTargetsAndStepsAsync(connection, transaction, projectId, input, cancellationToken);
            await InsertProjectEventAsync(connection, transaction, projectId, userId, cancellationToken);
            await CompleteOperationAsync(
                connection,
                transaction,
                input.OperationId,
                projectId,
                cancellationToken);

            var project = await ReadDetailAsync(connection, transaction, projectId, cancellationToken)
                ?? throw new InvalidOperationException("The Osan project was not readable before commit.");
            await transaction.CommitAsync(cancellationToken);
            return new OsanProjectCreateResult(
                OsanProjectCreateStatus.Success,
                new OsanProjectCreateResponse(input.OperationId, false, project));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                  && string.Equals(exception.ConstraintName, OsanProjectCodeConstraint, StringComparison.Ordinal))
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            return new OsanProjectCreateResult(OsanProjectCreateStatus.ProjectCodeConflict);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    private static async Task<bool> TryCreateOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        string fingerprint,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_project_create_operations (
                operation_id,
                request_fingerprint,
                created_by_user_id
            )
            values (@operation_id, @request_fingerprint, @user_id)
            on conflict (operation_id) do nothing
            returning true;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("request_fingerprint", fingerprint);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<OsanProjectOperation?> ReadOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select request_fingerprint, created_by_user_id, project_id
            from osan_project_create_operations
            where operation_id = @operation_id
            for update;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OsanProjectOperation(
                reader.GetString(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2))
            : null;
    }

    private static async Task InsertProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        NormalizedCreateOsanProjectInput input,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into projects (
                id,
                project_key,
                project_number,
                name,
                customer_name,
                item,
                project_code,
                project_title,
                project_title_normalized,
                delivery_date,
                status,
                created_by_user_id,
                updated_at_utc,
                project_profile,
                osan_po_number,
                osan_work_order_number,
                osan_product_name,
                osan_quantity
            )
            values (
                @project_id,
                @project_key,
                @project_code,
                @title,
                @customer_name,
                '',
                @project_code,
                @title,
                null,
                @delivery_date,
                'Active',
                @user_id,
                now(),
                'Osan',
                @po_number,
                @work_order_number,
                @product_name,
                @quantity
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_key", $"osan-{projectId:N}");
        command.Parameters.AddWithValue("project_code", input.ProjectCode);
        command.Parameters.AddWithValue("title", input.Title);
        command.Parameters.AddWithValue("customer_name", input.CustomerName);
        command.Parameters.AddWithValue("delivery_date", input.DeliveryDate);
        command.Parameters.Add(new NpgsqlParameter("po_number", NpgsqlDbType.Text)
        {
            Value = input.PoNumber is null ? DBNull.Value : input.PoNumber
        });
        command.Parameters.Add(new NpgsqlParameter("work_order_number", NpgsqlDbType.Text)
        {
            Value = input.WorkOrderNumber is null ? DBNull.Value : input.WorkOrderNumber
        });
        command.Parameters.AddWithValue("product_name", input.ProductName);
        command.Parameters.AddWithValue("quantity", input.Quantity);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCreatorAccessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into user_project_access (user_id, project_id)
            values (@user_id, @project_id);
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertTargetsAndStepsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        NormalizedCreateOsanProjectInput input,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with created_targets as (
                insert into osan_project_targets (
                    project_id,
                    sequence_number,
                    display_name
                )
                select
                    @project_id,
                    sequence_number,
                    @product_name || ' ' || sequence_number::text
                from generate_series(1, @quantity) sequence_number
                returning id, sequence_number
            )
            insert into osan_project_target_steps (
                project_id,
                target_id,
                sequence_number,
                step_code,
                step_name
            )
            select
                @project_id,
                created_targets.id,
                step_snapshot.sequence_number,
                step_snapshot.step_code,
                step_snapshot.step_name
            from created_targets
            cross join (values
                (1, 'INCOMING_INSPECTION', '입고검사'),
                (2, 'BATCH_INSPECTION', '배치검사'),
                (3, 'WIRING_INSPECTION', '배선검사'),
                (4, 'EIGHT_SYSTEM', '8계통'),
                (5, 'OPERATION_INSPECTION', '동작검사'),
                (6, 'SHIPPING_INSPECTION', '출하검사'),
                (7, 'PACKING', '포장')
            ) step_snapshot(sequence_number, step_code, step_name)
            order by created_targets.sequence_number, step_snapshot.sequence_number;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("product_name", input.ProductName);
        command.Parameters.AddWithValue("quantity", input.Quantity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertProjectEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into osan_project_events (
                project_id,
                event_type,
                actor_user_id,
                recipient_user_id
            )
            values (@project_id, 'ProjectCreated', @user_id, @user_id);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteOperationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update osan_project_create_operations
            set project_id = @project_id,
                completed_at_utc = now()
            where operation_id = @operation_id;
            """;
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OsanProjectDetailResponse?> ReadDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        OsanProjectListItemResponse project;
        await using (var projectCommand = connection.CreateCommand())
        {
            projectCommand.Transaction = transaction;
            projectCommand.CommandText = """
                select
                    projects.id,
                    projects.project_title,
                    projects.project_code,
                    projects.customer_name,
                    projects.osan_po_number,
                    projects.osan_work_order_number,
                    projects.delivery_date,
                    projects.osan_product_name,
                    projects.osan_quantity,
                    projects.status,
                    projects.created_at_utc
                from projects
                where projects.id = @project_id
                  and projects.project_profile = 'Osan'
                  and projects.deleted_at_utc is null;
                """;
            projectCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await projectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            project = ReadListItem(reader);
        }

        var targets = new List<OsanProjectTargetResponse>();
        TargetBuilder? currentTarget = null;
        await using (var targetCommand = connection.CreateCommand())
        {
            targetCommand.Transaction = transaction;
            targetCommand.CommandText = """
                select
                    targets.id,
                    targets.sequence_number,
                    targets.display_name,
                    targets.status,
                    steps.id,
                    steps.sequence_number,
                    steps.step_code,
                    steps.step_name,
                    steps.status
                from osan_project_targets targets
                join osan_project_target_steps steps on steps.target_id = targets.id
                where targets.project_id = @project_id
                order by targets.sequence_number, steps.sequence_number;
                """;
            targetCommand.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await targetCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var targetId = reader.GetGuid(0);
                if (currentTarget?.TargetId != targetId)
                {
                    if (currentTarget is not null)
                    {
                        targets.Add(currentTarget.ToResponse());
                    }
                    currentTarget = new TargetBuilder(
                        targetId,
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3));
                }

                currentTarget.Steps.Add(new OsanProjectStepResponse(
                    reader.GetGuid(4),
                    reader.GetInt32(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8)));
            }
        }

        if (currentTarget is not null)
        {
            targets.Add(currentTarget.ToResponse());
        }

        return new OsanProjectDetailResponse(
            project.ProjectId,
            project.Title,
            project.ProjectCode,
            project.CustomerName,
            project.PoNumber,
            project.WorkOrderNumber,
            project.DeliveryDate,
            project.ProductName,
            project.Quantity,
            project.Status,
            project.CreatedAtUtc,
            targets);
    }

    private static OsanProjectListItemResponse ReadListItem(NpgsqlDataReader reader)
    {
        return new OsanProjectListItemResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetFieldValue<DateOnly>(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10));
    }

    private static void AddAccessScope(
        ICollection<string> where,
        ICollection<NpgsqlParameter> parameters,
        ProjectAccessScope accessScope)
    {
        if (accessScope.HasProjectReadAll)
        {
            return;
        }

        if (accessScope.ProjectKeys.Count == 0)
        {
            where.Add("false");
            return;
        }

        where.Add("projects.project_key = any(@project_keys)");
        parameters.Add(new NpgsqlParameter<string[]>("project_keys", accessScope.ProjectKeys.ToArray()));
    }

    private static string CreateFingerprint(NormalizedCreateOsanProjectInput input)
    {
        var payload = JsonSerializer.Serialize(new
        {
            input.Title,
            input.ProjectCode,
            input.CustomerName,
            input.PoNumber,
            input.WorkOrderNumber,
            DeliveryDate = input.DeliveryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            input.ProductName,
            input.Quantity
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
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

    private static async Task RollbackQuietlyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record OsanProjectOperation(
        string RequestFingerprint,
        Guid CreatedByUserId,
        Guid? ProjectId);

    private sealed class TargetBuilder(
        Guid targetId,
        int sequenceNumber,
        string displayName,
        string status)
    {
        public Guid TargetId { get; } = targetId;
        public List<OsanProjectStepResponse> Steps { get; } = [];

        public OsanProjectTargetResponse ToResponse() =>
            new(TargetId, sequenceNumber, displayName, status, Steps);
    }
}
