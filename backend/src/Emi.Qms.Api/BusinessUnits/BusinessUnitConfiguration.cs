using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public enum BusinessUnitDatabaseKind
{
    Business,
    Directory
}

public enum BusinessUnitConnectionPurpose
{
    Runtime,
    Migration,
    Administrator
}

public sealed record BusinessUnitDatabaseTarget(
    string Code,
    BusinessUnitDatabaseKind Kind,
    string RuntimeConnectionName,
    string MigrationConnectionName,
    string AdministratorConnectionName,
    string ExpectedDatabaseName,
    string MigrationRoleName,
    string RuntimeRoleName,
    string ExpectedSchemaVersion,
    bool ExternalNotificationsEnabled,
    bool EscalationWorkerEnabled,
    bool AdminDeletionWorkerEnabled,
    bool IsLegacy = false);

public sealed class BusinessUnitConfiguration
{
    public const string SectionName = "BusinessUnits";
    public const string BusinessSchemaVersion = "0086_business_unit_database_identity";
    public const string DirectorySchemaVersion = "0001_business_unit_directory";

    private BusinessUnitConfiguration(
        bool enabled,
        BusinessUnitDatabaseTarget? directory,
        IReadOnlyList<BusinessUnitDatabaseTarget> businesses,
        IReadOnlyList<string> errors)
    {
        Enabled = enabled;
        Directory = directory;
        Businesses = businesses;
        Errors = errors;
    }

    public bool Enabled { get; }
    public BusinessUnitDatabaseTarget? Directory { get; }
    public IReadOnlyList<BusinessUnitDatabaseTarget> Businesses { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public static BusinessUnitConfiguration Read(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>($"{SectionName}:Enabled"))
        {
            return new BusinessUnitConfiguration(
                false,
                null,
                [new BusinessUnitDatabaseTarget(
                    BusinessUnitCodes.Cheongju,
                    BusinessUnitDatabaseKind.Business,
                    "QmsDatabase",
                    "QmsDatabase",
                    "QmsDatabaseAdmin",
                    string.Empty,
                    configuration["Database:MigrationRoleName"] ?? string.Empty,
                    configuration["Database:RuntimeRoleName"] ?? string.Empty,
                    BusinessSchemaVersion,
                    true,
                    true,
                    true,
                    IsLegacy: true)],
                []);
        }

        var errors = new List<string>();
        var directory = ReadTarget(
            configuration,
            "Directory",
            "DIRECTORY",
            BusinessUnitDatabaseKind.Directory,
            DirectorySchemaVersion,
            externalNotificationsEnabled: false,
            escalationWorkerEnabled: false,
            adminDeletionWorkerEnabled: false,
            errors);
        var cheongju = ReadTarget(
            configuration,
            "Units:Cheongju",
            BusinessUnitCodes.Cheongju,
            BusinessUnitDatabaseKind.Business,
            BusinessSchemaVersion,
            externalNotificationsEnabled: true,
            escalationWorkerEnabled: true,
            adminDeletionWorkerEnabled: true,
            errors);
        var osan = ReadTarget(
            configuration,
            "Units:Osan",
            BusinessUnitCodes.Osan,
            BusinessUnitDatabaseKind.Business,
            BusinessSchemaVersion,
            externalNotificationsEnabled: true,
            escalationWorkerEnabled: false,
            adminDeletionWorkerEnabled: false,
            errors);

        var targets = new[] { directory, cheongju, osan }.Where(target => target is not null).Cast<BusinessUnitDatabaseTarget>().ToList();
        ValidateTargetMetadata(targets, errors);

        return new BusinessUnitConfiguration(
            true,
            directory,
            [.. new[] { cheongju, osan }.Where(target => target is not null).Cast<BusinessUnitDatabaseTarget>()],
            errors.Distinct(StringComparer.Ordinal).ToList());
    }

    public BusinessUnitDatabaseTarget GetBusiness(string code)
    {
        return Businesses.SingleOrDefault(target => string.Equals(target.Code, code, StringComparison.Ordinal))
            ?? throw new BusinessUnitContextUnavailableException("business_unit_unknown");
    }

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                $"Business-unit database configuration is invalid ({Errors.Count} validation error(s)).");
        }
    }

    private static BusinessUnitDatabaseTarget? ReadTarget(
        IConfiguration configuration,
        string section,
        string expectedCode,
        BusinessUnitDatabaseKind kind,
        string expectedSchemaVersion,
        bool externalNotificationsEnabled,
        bool escalationWorkerEnabled,
        bool adminDeletionWorkerEnabled,
        ICollection<string> errors)
    {
        var prefix = $"{SectionName}:{section}";
        var code = configuration[$"{prefix}:Code"]?.Trim().ToUpperInvariant();
        var runtimeConnection = configuration[$"{prefix}:RuntimeConnection"]?.Trim();
        var migrationConnection = configuration[$"{prefix}:MigrationConnection"]?.Trim();
        var administratorConnection = configuration[$"{prefix}:AdministratorConnection"]?.Trim();
        var expectedDatabaseName = configuration[$"{prefix}:ExpectedDatabaseName"]?.Trim();
        var migrationRoleName = configuration[$"{prefix}:MigrationRoleName"]?.Trim();
        var runtimeRoleName = configuration[$"{prefix}:RuntimeRoleName"]?.Trim();
        var schemaVersion = configuration[$"{prefix}:ExpectedSchemaVersion"]?.Trim();

        if (!string.Equals(code, expectedCode, StringComparison.Ordinal)) errors.Add($"{section}:code_invalid");
        if (string.IsNullOrWhiteSpace(runtimeConnection)) errors.Add($"{section}:runtime_connection_missing");
        if (string.IsNullOrWhiteSpace(migrationConnection)) errors.Add($"{section}:migration_connection_missing");
        if (string.IsNullOrWhiteSpace(administratorConnection)) errors.Add($"{section}:administrator_connection_missing");
        if (string.IsNullOrWhiteSpace(expectedDatabaseName)) errors.Add($"{section}:database_name_missing");
        if (string.IsNullOrWhiteSpace(migrationRoleName)) errors.Add($"{section}:migration_role_missing");
        if (string.IsNullOrWhiteSpace(runtimeRoleName)) errors.Add($"{section}:runtime_role_missing");
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal)) errors.Add($"{section}:schema_version_invalid");

        if (string.IsNullOrWhiteSpace(runtimeConnection)
            || string.IsNullOrWhiteSpace(migrationConnection)
            || string.IsNullOrWhiteSpace(administratorConnection)
            || string.IsNullOrWhiteSpace(expectedDatabaseName)
            || string.IsNullOrWhiteSpace(migrationRoleName)
            || string.IsNullOrWhiteSpace(runtimeRoleName))
        {
            return null;
        }

        if (string.Equals(migrationRoleName, runtimeRoleName, StringComparison.Ordinal))
        {
            errors.Add($"{section}:database_roles_not_distinct");
        }

        return new BusinessUnitDatabaseTarget(
            expectedCode,
            kind,
            runtimeConnection,
            migrationConnection,
            administratorConnection,
            expectedDatabaseName,
            migrationRoleName,
            runtimeRoleName,
            expectedSchemaVersion,
            externalNotificationsEnabled,
            escalationWorkerEnabled,
            adminDeletionWorkerEnabled);
    }

    public IReadOnlyList<string> ValidateOperationConnections(
        IConfiguration configuration,
        BusinessUnitConnectionPurpose purpose,
        IEnumerable<BusinessUnitDatabaseTarget>? selectedTargets = null,
        bool requireSsl = false)
    {
        var errors = new List<string>();
        foreach (var target in selectedTargets ?? AllTargets())
        {
            ValidateConnection(configuration, target, purpose, errors, requireSsl);
        }
        return errors;
    }

    public IReadOnlyList<BusinessUnitDatabaseTarget> AllTargets() =>
        Directory is null ? Businesses : [Directory, .. Businesses];

    private static void ValidateTargetMetadata(
        IReadOnlyList<BusinessUnitDatabaseTarget> targets,
        ICollection<string> errors)
    {
        if (targets.Select(target => target.ExpectedDatabaseName).Distinct(StringComparer.Ordinal).Count() != targets.Count)
        {
            errors.Add("database_names_not_distinct");
        }
        if (targets.Select(target => target.RuntimeRoleName).Distinct(StringComparer.Ordinal).Count() != targets.Count)
        {
            errors.Add("runtime_roles_not_distinct");
        }
        if (targets.Select(target => target.MigrationRoleName).Distinct(StringComparer.Ordinal).Count() != targets.Count)
        {
            errors.Add("migration_roles_not_distinct");
        }
        var allBoundedRoles = targets.SelectMany(target => new[] { target.RuntimeRoleName, target.MigrationRoleName }).ToList();
        if (allBoundedRoles.Distinct(StringComparer.Ordinal).Count() != allBoundedRoles.Count)
        {
            errors.Add("database_roles_cross_category_not_distinct");
        }
    }

    public IReadOnlyList<string> ValidateSameServer(
        IConfiguration configuration,
        BusinessUnitConnectionPurpose purpose,
        IEnumerable<BusinessUnitDatabaseTarget>? selectedTargets = null)
    {
        var targets = (selectedTargets ?? AllTargets()).ToList();
        var errors = new List<string>();
        var parsed = targets
            .Select(target => ValidateConnection(configuration, target, purpose, errors, requireSsl: false))
            .Where(builder => builder is not null)
            .Cast<NpgsqlConnectionStringBuilder>()
            .ToList();
        if (parsed.Select(builder => (builder.Host!.ToLowerInvariant(), builder.Port)).Distinct().Count() > 1)
        {
            errors.Add("database_server_endpoint_mismatch");
        }
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> ValidateSameServerAcrossPurposes(
        IConfiguration configuration,
        IEnumerable<BusinessUnitConnectionPurpose> purposes,
        IEnumerable<BusinessUnitDatabaseTarget>? selectedTargets = null)
    {
        var targets = (selectedTargets ?? AllTargets()).ToList();
        var errors = new List<string>();
        var parsed = purposes
            .SelectMany(purpose => targets.Select(target =>
                ValidateConnection(configuration, target, purpose, errors, requireSsl: false)))
            .Where(builder => builder is not null)
            .Cast<NpgsqlConnectionStringBuilder>()
            .ToList();
        if (parsed.Select(builder => (builder.Host!.ToLowerInvariant(), builder.Port)).Distinct().Count() > 1)
        {
            errors.Add("database_server_endpoint_mismatch");
        }
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static NpgsqlConnectionStringBuilder? ValidateConnection(
        IConfiguration configuration,
        BusinessUnitDatabaseTarget target,
        BusinessUnitConnectionPurpose purpose,
        ICollection<string> errors,
        bool requireSsl)
    {
        var name = purpose switch
        {
            BusinessUnitConnectionPurpose.Runtime => target.RuntimeConnectionName,
            BusinessUnitConnectionPurpose.Migration => target.MigrationConnectionName,
            BusinessUnitConnectionPurpose.Administrator => target.AdministratorConnectionName,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{target.Code}:{purpose.ToString().ToLowerInvariant()}_connection_not_configured");
            return null;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(builder.Host)
                || string.IsNullOrWhiteSpace(builder.Database)
                || string.IsNullOrWhiteSpace(builder.Username)
                || string.IsNullOrWhiteSpace(builder.Password))
            {
                errors.Add($"{target.Code}:{purpose.ToString().ToLowerInvariant()}_connection_incomplete");
                return null;
            }
            if (!string.Equals(builder.Database, target.ExpectedDatabaseName, StringComparison.Ordinal))
            {
                errors.Add($"{target.Code}:{purpose.ToString().ToLowerInvariant()}_database_mismatch");
            }
            if (purpose == BusinessUnitConnectionPurpose.Runtime
                && !string.Equals(builder.Username, target.RuntimeRoleName, StringComparison.Ordinal))
            {
                errors.Add($"{target.Code}:runtime_role_mismatch");
            }
            if (purpose == BusinessUnitConnectionPurpose.Migration
                && !string.Equals(builder.Username, target.MigrationRoleName, StringComparison.Ordinal))
            {
                errors.Add($"{target.Code}:migration_role_mismatch");
            }
            if (requireSsl && builder.SslMode != SslMode.VerifyFull)
            {
                errors.Add($"{target.Code}:{purpose.ToString().ToLowerInvariant()}_ssl_invalid");
            }
            return builder;
        }
        catch (ArgumentException)
        {
            errors.Add($"{target.Code}:{purpose.ToString().ToLowerInvariant()}_connection_invalid");
            return null;
        }
    }
}

public static class BusinessUnitDatabaseIdentity
{
    public static async Task PreflightMigrationAsync(
        NpgsqlConnection connection,
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        ThrowIfConfiguredDatabaseNameDoesNotMatch(connection, target);

        var identityTableExists = await RelationExistsAsync(
            connection,
            "public.qms_database_identity",
            cancellationToken);
        if (identityTableExists)
        {
            var identity = await ReadIdentityAsync(connection, cancellationToken);
            if (identity is not null)
            {
                var expectedKind = target.Kind == BusinessUnitDatabaseKind.Directory
                    ? "directory"
                    : "business";
                var expectedCode = target.Kind == BusinessUnitDatabaseKind.Directory
                    ? null
                    : target.Code;
                if (!string.Equals(identity.Value.Kind, expectedKind, StringComparison.Ordinal)
                    || !string.Equals(identity.Value.Code, expectedCode, StringComparison.Ordinal)
                    || !string.Equals(
                        identity.Value.SchemaContract,
                        target.ExpectedSchemaVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        target.Kind == BusinessUnitDatabaseKind.Directory
                            ? "directory_database_identity_mismatch"
                            : "business_database_identity_mismatch");
                }
                return;
            }
        }

        var unsafeUnboundTarget = target.Kind == BusinessUnitDatabaseKind.Directory
            ? await HasExistingApplicationSchemaAsync(connection, cancellationToken)
            : !string.Equals(target.Code, BusinessUnitCodes.Cheongju, StringComparison.Ordinal)
              && await HasBusinessRowsAsync(connection, cancellationToken);
        if (unsafeUnboundTarget)
        {
            throw new InvalidOperationException(
                target.Kind == BusinessUnitDatabaseKind.Directory
                    ? "directory_database_identity_unbound_populated"
                    : "business_database_identity_unbound_populated");
        }
    }

    public static async Task BindOrVerifyAsync(
        NpgsqlConnection connection,
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        ThrowIfConfiguredDatabaseNameDoesNotMatch(connection, target);

        if (target.Kind == BusinessUnitDatabaseKind.Directory)
        {
            if (!await IsExpectedAsync(connection, target, cancellationToken))
            {
                throw new InvalidOperationException("directory_database_identity_mismatch");
            }
            return;
        }

        var existing = await ReadBusinessIdentityAsync(connection, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Value.Code, target.Code, StringComparison.Ordinal)
                || !string.Equals(existing.Value.SchemaContract, target.ExpectedSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("business_database_identity_mismatch");
            }
            return;
        }

        await using (var populationCommand = connection.CreateCommand())
        {
            populationCommand.CommandText = "select (select count(*) from qms_users) + (select count(*) from projects);";
            var populatedCount = Convert.ToInt64(await populationCommand.ExecuteScalarAsync(cancellationToken));
            if (populatedCount > 0 && !string.Equals(target.Code, BusinessUnitCodes.Cheongju, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("business_database_identity_unbound_populated");
            }
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            insert into qms_database_identity (
                singleton, database_kind, business_unit_code, schema_contract)
            values (true, 'business', @business_unit_code, @schema_contract)
            on conflict (singleton) do nothing;
            """;
        insert.Parameters.AddWithValue("business_unit_code", target.Code);
        insert.Parameters.AddWithValue("schema_contract", target.ExpectedSchemaVersion);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (!await IsExpectedAsync(connection, target, cancellationToken))
        {
            throw new InvalidOperationException("business_database_identity_bind_race");
        }
    }

    public static async Task<bool> IsExpectedAsync(
        NpgsqlConnection connection,
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists (
                select 1
                from qms_database_identity
                where singleton = true
                  and database_kind = @database_kind
                  and business_unit_code is not distinct from @business_unit_code
                  and schema_contract = @schema_contract);
            """;
        command.Parameters.AddWithValue(
            "database_kind",
            target.Kind == BusinessUnitDatabaseKind.Directory ? "directory" : "business");
        command.Parameters.Add(
            new NpgsqlParameter(
                "business_unit_code",
                NpgsqlDbType.Text)
            {
                Value = target.Kind == BusinessUnitDatabaseKind.Directory ? DBNull.Value : target.Code
            });
        command.Parameters.AddWithValue("schema_contract", target.ExpectedSchemaVersion);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<(string Code, string SchemaContract)?> ReadBusinessIdentityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select business_unit_code, schema_contract
            from qms_database_identity
            where singleton = true and database_kind = 'business';
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static void ThrowIfConfiguredDatabaseNameDoesNotMatch(
        NpgsqlConnection connection,
        BusinessUnitDatabaseTarget target)
    {
        if (!string.Equals(connection.Database, target.ExpectedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("database_identity_configured_name_mismatch");
        }
    }

    private static async Task<bool> RelationExistsAsync(
        NpgsqlConnection connection,
        string relationName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select to_regclass(@relation_name) is not null;";
        command.Parameters.AddWithValue("relation_name", relationName);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<(string Kind, string? Code, string SchemaContract)?> ReadIdentityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select database_kind, business_unit_code, schema_contract
            from qms_database_identity
            where singleton = true;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2))
            : null;
    }

    private static async Task<bool> HasExistingApplicationSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists (
                select 1
                from pg_class relation
                join pg_namespace namespace on namespace.oid = relation.relnamespace
                where namespace.nspname = 'public'
                  and relation.relkind in ('r', 'p')
                  and relation.relname not in ('schema_migrations', 'qms_database_identity'));
            """;
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> HasBusinessRowsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        foreach (var relationName in new[] { "qms_users", "projects" })
        {
            if (!await RelationExistsAsync(
                    connection,
                    $"public.{relationName}",
                    cancellationToken))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"select exists (select 1 from {relationName} limit 1);";
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                return true;
            }
        }

        return false;
    }
}
