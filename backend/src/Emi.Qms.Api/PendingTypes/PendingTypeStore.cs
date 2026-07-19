using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.PendingTypes;

public sealed class PendingTypeStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<PendingTypeCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadCatalogAsync(connection, null, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingTypeOptionResponse>> GetManualOptionsAsync(CancellationToken cancellationToken)
        => await GetOptionsAsync(manualOnly: true, cancellationToken);

    public async Task<IReadOnlyList<PendingTypeOptionResponse>> GetFilterOptionsAsync(CancellationToken cancellationToken)
        => await GetOptionsAsync(manualOnly: false, cancellationToken);

    public async Task<PendingTypeMutationResult<PendingTypeCatalogResponse>> CreateAsync(
        CreatePendingTypeRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateText(request.DisplayName, request.Description);
        if (errors.Count > 0)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(errors);
        }

        var code = $"CUSTOM_{Guid.NewGuid():N}".ToUpperInvariant();
        var displayName = request.DisplayName!.Trim();
        var description = NormalizeDescription(request.Description);

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockCatalogAsync(connection, transaction, cancellationToken);

        var sortOrder = await NextSortOrderAsync(connection, transaction, cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into pending_issue_type_catalog (
                    code,display_name,description,sort_order,is_system,is_manual_enabled,is_active)
                values (@code,@display_name,@description,@sort_order,false,true,true);
                """;
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("display_name", displayName);
            AddNullableText(command, "description", description);
            command.Parameters.AddWithValue("sort_order", sortOrder);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateName();
            }
        }

        var next = new CatalogSnapshot(displayName, description, sortOrder, true, true, 1);
        await AppendAuditAsync(connection, transaction, "CustomCreated", code, actorUserId, null, next, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PendingTypeMutationResult<PendingTypeCatalogResponse>.Success(await GetCatalogAsync(cancellationToken));
    }

    public async Task<PendingTypeMutationResult<PendingTypeCatalogResponse>> UpdateAsync(
        string code,
        UpdatePendingTypeRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateText(request.DisplayName, request.Description);
        if (request.ExpectedRowVersion is null or < 1)
        {
            errors[nameof(request.ExpectedRowVersion)] = ["최신 유형 버전을 확인해 주세요."];
        }
        if (request.IsManualEnabled is null)
        {
            errors[nameof(request.IsManualEnabled)] = ["수동 등록 노출 여부를 선택해 주세요."];
        }
        if (errors.Count > 0)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockTypeAsync(connection, transaction, code, cancellationToken);
        if (current is null)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.NotFound();
        }
        if (current.RowVersion != request.ExpectedRowVersion)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형이 변경되었습니다. 새로고침해 주세요.");
        }
        if (string.Equals(code, "Other", StringComparison.Ordinal) && request.IsManualEnabled is false)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.IsManualEnabled)] = ["기타 유형은 수동 등록에 항상 표시해야 합니다."]
            });
        }

        var next = current with
        {
            DisplayName = request.DisplayName!.Trim(),
            Description = NormalizeDescription(request.Description),
            IsManualEnabled = request.IsManualEnabled == true,
            RowVersion = current.RowVersion + 1
        };

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issue_type_catalog
                set display_name=@display_name,description=@description,is_manual_enabled=@manual,
                    row_version=row_version+1,updated_at_utc=now()
                where code=@code and row_version=@expected;
                """;
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("display_name", next.DisplayName);
            AddNullableText(command, "description", next.Description);
            command.Parameters.AddWithValue("manual", next.IsManualEnabled);
            command.Parameters.AddWithValue("expected", current.RowVersion);
            try
            {
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형이 변경되었습니다. 새로고침해 주세요.");
                }
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateName();
            }
        }

        await AppendAuditAsync(connection, transaction, "Updated", code, actorUserId, current, next, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PendingTypeMutationResult<PendingTypeCatalogResponse>.Success(await GetCatalogAsync(cancellationToken));
    }

    public async Task<PendingTypeMutationResult<PendingTypeCatalogResponse>> SetActiveAsync(
        string code,
        SetPendingTypeActiveRequest request,
        bool isActive,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedRowVersion is null or < 1)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ExpectedRowVersion)] = ["최신 유형 버전을 확인해 주세요."]
            });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockTypeAsync(connection, transaction, code, cancellationToken);
        if (current is null)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.NotFound();
        }
        if (current.RowVersion != request.ExpectedRowVersion)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형이 변경되었습니다. 새로고침해 주세요.");
        }
        if (current.IsSystem)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(new Dictionary<string, string[]>
            {
                ["code"] = ["시스템 유형의 활성 상태는 변경할 수 없습니다."]
            });
        }
        if (current.IsActive == isActive)
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict(isActive ? "이미 사용 중인 유형입니다." : "이미 사용 중지된 유형입니다.");
        }

        var next = current with { IsActive = isActive, RowVersion = current.RowVersion + 1 };
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issue_type_catalog
                set is_active=@active,row_version=row_version+1,updated_at_utc=now()
                where code=@code and row_version=@expected;
                """;
            command.Parameters.AddWithValue("active", isActive);
            command.Parameters.AddWithValue("code", code);
            command.Parameters.AddWithValue("expected", current.RowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형이 변경되었습니다. 새로고침해 주세요.");
            }
        }
        await AppendAuditAsync(connection, transaction, isActive ? "Activated" : "Deactivated", code, actorUserId, current, next, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PendingTypeMutationResult<PendingTypeCatalogResponse>.Success(await GetCatalogAsync(cancellationToken));
    }

    public async Task<PendingTypeMutationResult<PendingTypeCatalogResponse>> ReorderAsync(
        ReorderPendingTypesRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return ReorderValidation("전체 유형 순서를 보내 주세요.");
        }
        if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.Code) || item.ExpectedRowVersion is null or < 1 || item.NewSortOrder is null or < 1)
            || request.Items.Select(item => item.Code!.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Items.Count
            || request.Items.Select(item => item.NewSortOrder!.Value).Distinct().Count() != request.Items.Count)
        {
            return ReorderValidation("유형 코드·버전·새 순서를 중복 없이 확인해 주세요.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockCatalogAsync(connection, transaction, cancellationToken);
        var current = await ReadSnapshotsForUpdateAsync(connection, transaction, cancellationToken);
        var requested = request.Items.ToDictionary(item => item.Code!.Trim(), StringComparer.Ordinal);
        if (requested.Count != current.Count || current.Keys.Any(code => !requested.ContainsKey(code)))
        {
            return ReorderValidation("현재 전체 유형을 새로고침한 뒤 다시 정렬해 주세요.");
        }
        if (!request.Items.Select(item => item.NewSortOrder!.Value).Order().SequenceEqual(Enumerable.Range(1, current.Count)))
        {
            return ReorderValidation("유형 순서는 1부터 빠짐없이 지정해 주세요.");
        }
        if (current.Any(pair => pair.Value.RowVersion != requested[pair.Key].ExpectedRowVersion))
        {
            return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형 순서가 변경되었습니다. 새로고침해 주세요.");
        }

        await using (var defer = connection.CreateCommand())
        {
            defer.Transaction = transaction;
            defer.CommandText = "set constraints ux_pending_issue_type_catalog_sort deferred;";
            await defer.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var pair in current)
        {
            var nextOrder = requested[pair.Key].NewSortOrder!.Value;
            if (pair.Value.SortOrder == nextOrder)
            {
                continue;
            }
            var next = pair.Value with { SortOrder = nextOrder, RowVersion = pair.Value.RowVersion + 1 };
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update pending_issue_type_catalog
                set sort_order=@sort_order,row_version=row_version+1,updated_at_utc=now()
                where code=@code and row_version=@expected;
                """;
            command.Parameters.AddWithValue("sort_order", nextOrder);
            command.Parameters.AddWithValue("code", pair.Key);
            command.Parameters.AddWithValue("expected", pair.Value.RowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return PendingTypeMutationResult<PendingTypeCatalogResponse>.Conflict("Pending 유형 순서가 변경되었습니다. 새로고침해 주세요.");
            }
            await AppendAuditAsync(connection, transaction, "Reordered", pair.Key, actorUserId, pair.Value, next, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return PendingTypeMutationResult<PendingTypeCatalogResponse>.Success(await GetCatalogAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<PendingTypeOptionResponse>> GetOptionsAsync(bool manualOnly, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select code,display_name,sort_order,is_system,is_manual_enabled,is_active
            from pending_issue_type_catalog
            where (not @manual_only or (is_active and is_manual_enabled))
            order by sort_order,code;
            """);
        command.Parameters.AddWithValue("manual_only", manualOnly);
        var result = new List<PendingTypeOptionResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5)));
        }
        return result;
    }

    private static async Task<PendingTypeCatalogResponse> ReadCatalogAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select catalog.code,catalog.display_name,catalog.description,catalog.sort_order,
                   catalog.is_system,catalog.is_manual_enabled,catalog.is_active,catalog.row_version,
                   count(issue.id)::int
            from pending_issue_type_catalog catalog
            left join pending_issues issue on issue.issue_type=catalog.code
            group by catalog.code,catalog.display_name,catalog.description,catalog.sort_order,
                     catalog.is_system,catalog.is_manual_enabled,catalog.is_active,catalog.row_version
            order by catalog.sort_order,catalog.code;
            """;
        var result = new List<PendingTypeCatalogItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt32(3),
                reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetInt32(7), reader.GetInt32(8)));
        }
        return new(result);
    }

    private static async Task LockCatalogAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "lock table pending_issue_type_catalog in share row exclusive mode;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> NextSortOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(max(sort_order),0)+1 from pending_issue_type_catalog;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<CatalogSnapshot?> LockTypeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string code, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select display_name,description,sort_order,is_manual_enabled,is_active,is_system,row_version
            from pending_issue_type_catalog where code=@code for update;
            """;
        command.Parameters.AddWithValue("code", code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2),
                reader.GetBoolean(3), reader.GetBoolean(4), reader.GetInt32(6), reader.GetBoolean(5))
            : null;
    }

    private static async Task<Dictionary<string, CatalogSnapshot>> ReadSnapshotsForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select code,display_name,description,sort_order,is_manual_enabled,is_active,is_system,row_version
            from pending_issue_type_catalog order by sort_order for update;
            """;
        var result = new Dictionary<string, CatalogSnapshot>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0), new(
                reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt32(3),
                reader.GetBoolean(4), reader.GetBoolean(5), reader.GetInt32(7), reader.GetBoolean(6)));
        }
        return result;
    }

    private static async Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string action,
        string code,
        Guid actorUserId,
        CatalogSnapshot? previous,
        CatalogSnapshot? next,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into pending_issue_type_audit_events (
                action,issue_type_code,actor_user_id,previous_value,next_value)
            values (@action,@code,@actor,@previous,@next);
            """;
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("actor", actorUserId);
        command.Parameters.Add("previous", NpgsqlDbType.Jsonb).Value = previous is null ? DBNull.Value : JsonSerializer.Serialize(previous);
        command.Parameters.Add("next", NpgsqlDbType.Jsonb).Value = next is null ? DBNull.Value : JsonSerializer.Serialize(next);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateText(string? displayName, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = displayName?.Trim() ?? "";
        if (normalizedName.Length is < 2 or > 80)
        {
            errors[nameof(displayName)] = ["표시명은 2~80자로 입력해 주세요."];
        }
        var normalizedDescription = description?.Trim();
        if (normalizedDescription is { Length: 1 } or { Length: > 300 })
        {
            errors[nameof(description)] = ["설명은 비워 두거나 2~300자로 입력해 주세요."];
        }
        return errors;
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static PendingTypeMutationResult<PendingTypeCatalogResponse> DuplicateName()
        => PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(new Dictionary<string, string[]>
        {
            ["displayName"] = ["이미 사용 중인 Pending 유형 표시명입니다."]
        });

    private static PendingTypeMutationResult<PendingTypeCatalogResponse> ReorderValidation(string message)
        => PendingTypeMutationResult<PendingTypeCatalogResponse>.Validation(new Dictionary<string, string[]>
        {
            ["items"] = [message]
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

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    private sealed record CatalogSnapshot(
        string DisplayName,
        string? Description,
        int SortOrder,
        bool IsManualEnabled,
        bool IsActive,
        int RowVersion,
        bool IsSystem = false);
}
