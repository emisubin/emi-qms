using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Admin;

public sealed class MaterialCategoryStore(
    DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<MaterialCategoryCatalogResponse> ListAsync(
        Guid userId,
        bool isSystemAdministrator,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var canManage = await CanManageAsync(userId, isSystemAdministrator, cancellationToken);
        if (includeInactive && !canManage)
        {
            throw new FormTemplateForbiddenException();
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id, code, display_name, requires_iqc, is_active, display_order, row_version
            from material_categories
            where @include_inactive or is_active
            order by is_active desc, display_order, display_name, id;
            """);
        command.Parameters.AddWithValue("include_inactive", includeInactive);
        var items = new List<MaterialCategoryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }
        return new(canManage, items);
    }

    public async Task<MaterialCategoryCatalogResponse> CreateAsync(
        CreateMaterialCategoryRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        var displayName = NormalizeDisplayName(request.DisplayName);
        var displayOrder = ValidateOrder(request.DisplayOrder);
        if (request.RequiresIqc is null)
        {
            throw new ArgumentException("IQC 필요 여부를 선택해 주세요.", nameof(request.RequiresIqc));
        }
        await DemandManageAsync(actorUserId, isSystemAdministrator, cancellationToken);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var categoryId = Guid.NewGuid();
        var code = $"CUSTOM_{categoryId:N}"[..15].ToUpperInvariant();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into material_categories (
                    id, code, display_name, requires_iqc, display_order,
                    created_by_user_id, updated_by_user_id
                )
                values (
                    @id, @code, @display_name, @requires_iqc, @display_order,
                    @actor_id, @actor_id
                );
                """;
            command.Parameters.AddWithValue("id", categoryId);
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("display_name", displayName);
            command.Parameters.AddWithValue("requires_iqc", request.RequiresIqc.Value);
            command.Parameters.AddWithValue("display_order", displayOrder);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await AppendAuditAsync(
                connection,
                transaction,
                categoryId,
                "Created",
                actorUserId,
                null,
                Snapshot(displayName, request.RequiresIqc.Value, true, displayOrder),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new FormTemplateConflictException("같은 이름의 구매품 구분이 이미 있습니다.");
        }
        return await ListAsync(actorUserId, isSystemAdministrator, true, cancellationToken);
    }

    public async Task<MaterialCategoryCatalogResponse> UpdateAsync(
        Guid categoryId,
        UpdateMaterialCategoryRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedRowVersion is null or < 1)
        {
            throw new ArgumentException("최신 구분 version이 필요합니다.", nameof(request.ExpectedRowVersion));
        }
        var displayName = NormalizeDisplayName(request.DisplayName);
        var displayOrder = ValidateOrder(request.DisplayOrder);
        if (request.RequiresIqc is null || request.IsActive is null)
        {
            throw new ArgumentException("IQC 필요 여부와 사용 상태를 선택해 주세요.", "request");
        }
        await DemandManageAsync(actorUserId, isSystemAdministrator, cancellationToken);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        MaterialCategoryResponse current;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                select id, code, display_name, requires_iqc, is_active, display_order, row_version
                from material_categories
                where id=@id
                for update;
                """;
            read.Parameters.AddWithValue("id", categoryId);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("구매품 구분을 찾을 수 없습니다.", nameof(categoryId));
            }
            current = Map(reader);
        }
        if (current.RowVersion != request.ExpectedRowVersion)
        {
            throw new FormTemplateConflictException("구매품 구분이 변경되었습니다. 새로고침해 주세요.");
        }

        try
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update material_categories
                set display_name=@display_name,
                    requires_iqc=@requires_iqc,
                    is_active=@is_active,
                    display_order=@display_order,
                    row_version=row_version+1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=now()
                where id=@id and row_version=@expected_version;
                """;
            update.Parameters.AddWithValue("display_name", displayName);
            update.Parameters.AddWithValue("requires_iqc", request.RequiresIqc.Value);
            update.Parameters.AddWithValue("is_active", request.IsActive.Value);
            update.Parameters.AddWithValue("display_order", displayOrder);
            update.Parameters.AddWithValue("actor_id", actorUserId);
            update.Parameters.AddWithValue("id", categoryId);
            update.Parameters.AddWithValue("expected_version", request.ExpectedRowVersion.Value);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new FormTemplateConflictException("구매품 구분이 변경되었습니다. 새로고침해 주세요.");
            }
            var action = current.IsActive == request.IsActive
                ? "Updated"
                : request.IsActive.Value ? "Reactivated" : "Deactivated";
            await AppendAuditAsync(
                connection,
                transaction,
                categoryId,
                action,
                actorUserId,
                Snapshot(current.DisplayName, current.RequiresIqc, current.IsActive, current.DisplayOrder),
                Snapshot(displayName, request.RequiresIqc.Value, request.IsActive.Value, displayOrder),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new FormTemplateConflictException("같은 이름의 구매품 구분이 이미 있습니다.");
        }
        return await ListAsync(actorUserId, isSystemAdministrator, true, cancellationToken);
    }

    private async Task DemandManageAsync(
        Guid userId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(userId, isSystemAdministrator, cancellationToken))
        {
            throw new FormTemplateForbiddenException();
        }
    }

    private async Task<bool> CanManageAsync(
        Guid userId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        if (isSystemAdministrator)
        {
            return true;
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from qms_users users
                join departments department on department.id=users.department_id
                where users.id=@user_id
                  and users.is_active
                  and department.code='quality'
            );
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static string NormalizeDisplayName(string? value)
    {
        var normalized = value?.Trim();
        if (normalized is null || normalized.Length is < 1 or > 80)
        {
            throw new ArgumentException("구분 이름은 1~80자로 입력해 주세요.", nameof(value));
        }
        return normalized;
    }

    private static int ValidateOrder(int? value)
    {
        if (value is null or < 1 or > 1000)
        {
            throw new ArgumentException("표시 순서는 1~1,000 사이여야 합니다.", nameof(value));
        }
        return value.Value;
    }

    private static MaterialCategoryResponse Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetBoolean(3),
        reader.GetBoolean(4),
        reader.GetInt32(5),
        reader.GetInt32(6));

    private static object Snapshot(string name, bool requiresIqc, bool isActive, int displayOrder)
        => new { displayName = name, requiresIqc, isActive, displayOrder };

    private static async Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid categoryId,
        string action,
        Guid actorUserId,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into material_category_audit_events (
                category_id, action, actor_user_id, old_value, new_value
            )
            values (@category_id, @action, @actor_id, @old_value::jsonb, @new_value::jsonb);
            """;
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.Add("old_value", NpgsqlDbType.Text).Value =
            oldValue is null ? (object)DBNull.Value : JsonSerializer.Serialize(oldValue);
        command.Parameters.Add("new_value", NpgsqlDbType.Text).Value =
            newValue is null ? (object)DBNull.Value : JsonSerializer.Serialize(newValue);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
