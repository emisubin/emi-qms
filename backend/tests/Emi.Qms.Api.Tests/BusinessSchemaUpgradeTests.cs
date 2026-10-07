using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task BusinessSchemaSeparation_UpgradePreservesOwnedRowsAndRejectsMissingConsent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration,
            new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        var runner = new DatabaseMigrationRunner(provider, catalog, new DatabaseRuntimePrivilegeManager(),
            databases.Configuration, NullLogger<DatabaseMigrationRunner>.Instance);
        await runner.ApplyAndVerifyAsync("DIRECTORY", ct);
        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            await ApplyCommonSchemaBeforeSeparationAsync(databases, catalog, code, ct);
            await using var connection = await databases.OpenAsync(code, BusinessUnitConnectionPurpose.Migration, ct);
            await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(connection, databases.BusinessUnits.GetBusiness(code), ct);
        }
        var actor = Guid.NewGuid();
        var other = Guid.NewGuid();
        var osanProjectId = Guid.NewGuid();
        var osanCustomerId = Guid.NewGuid();
        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
            await databases.ExecuteAsync(code, BusinessUnitConnectionPurpose.Migration, $"""
                insert into qms_users(id,development_user_key,display_name,is_active)
                values('{actor:D}','upgrade-actor','Synthetic Upgrade Actor',true),
                      ('{other:D}','upgrade-other','Synthetic Upgrade Other',true);
                """, ct);
        await databases.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration, $"""
            insert into projects(id,project_key,project_number,name,project_code,project_title,
                project_title_normalized,customer_name,delivery_date,sales_owner_user_id,created_by_user_id)
            values('{Guid.NewGuid():D}','keep-cj','KEEP-CJ','Synthetic C Project','KEEP-CJ','Synthetic C Project',
                'SYNTHETIC C PROJECT','Synthetic Customer',current_date+7,'{actor:D}','{actor:D}');
            """, ct);
        await databases.ExecuteAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, $"""
            insert into osan_customers(id,name) values('{osanCustomerId:D}','Synthetic Customer');
            insert into osan_customer_assignments(user_id,customer_id) values('{actor:D}','{osanCustomerId:D}');
            insert into projects(id,project_key,project_number,name,project_code,project_title,project_title_normalized,
                customer_name,delivery_date,created_by_user_id,project_profile,osan_product_name,osan_quantity,status,
                deleted_at_utc,deleted_by_user_id,delete_reason,osan_customer_id)
            values('{osanProjectId:D}','keep-osan-1','DUPLICATE','Synthetic O Project','DUPLICATE','Synthetic O Project',null,
                'Synthetic Customer',current_date+7,'{actor:D}','Osan','Rack',2,'Active',null,null,null,'{osanCustomerId:D}'),
                ('{Guid.NewGuid():D}','keep-osan-2','DUPLICATE','Synthetic O Project','DUPLICATE','Synthetic O Project',null,
                'Synthetic Customer',current_date+7,'{actor:D}','Osan','Rack',2,'Completed',now(),'{actor:D}','Synthetic archived project','{osanCustomerId:D}');
            insert into osan_notification_preference_profiles(user_id,version)
            values('{actor:D}',2),('{other:D}',3);
            insert into osan_notification_preferences(user_id,event_kind,channel,stage_sequence,is_enabled)
            select '{actor:D}','StepCompleted','Mail',stage,false from generate_series(1,7) stage;
            insert into osan_notification_preferences(user_id,event_kind,channel,stage_sequence,is_enabled)
            values('{actor:D}','ProjectCreated','Mail',0,false),('{other:D}','ProjectCompleted','WebPush',0,false);
            """, ct);

        await using (var connection = await databases.OpenAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration, ct))
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            await OsanNotificationWriter.WriteAsync(connection, transaction, osanProjectId, Guid.NewGuid(),
                OsanNotificationKind.ProjectCreated, actor, DateTimeOffset.UtcNow, ct, recipientIds: [actor]);
            await transaction.CommitAsync(ct);
        }
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from notification_deliveries where manual_payload_json is not null", ct));

        var snapshots = new Dictionary<string, Dictionary<string, string>>();
        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            snapshots[code] = await SnapshotOwnedDataAsync(databases, code, ct);
            // Direct execution without the exact per-target opt-in must fail before
            // any destructive statement; this does not rely on the runner guard.
            var builder = databases.GetBuilder(code, BusinessUnitConnectionPurpose.Migration);
            builder.Options = "";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = await File.ReadAllTextAsync(
                catalog.GetBusinessMigrationFiles(code)
                    .Single(path => Path.GetFileName(path).StartsWith("0131_", StringComparison.Ordinal)), ct);
            var consentError = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal("P0001", consentError.SqlState);
            Assert.Equal("business_schema_explicit_consent_required", consentError.MessageText);
            await transaction.RollbackAsync(ct);
            Assert.Equal(209L, await databases.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE'", ct));
            // A stale connection option is not consent from the current release.
            var connectionKey = code == BusinessUnitCodes.Cheongju ? "CheongjuMigration" : "OsanMigration";
            var stale = new NpgsqlConnectionStringBuilder(databases.Configuration.GetConnectionString(connectionKey))
            { Options = $"-c emi_qms.business_schema_separation={code}" };
            foreach (var approval in new string?[] { null, "false", "invalid" })
            {
                var deniedConfiguration = new ConfigurationBuilder().AddConfiguration(databases.Configuration)
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:BusinessSchemaSeparationApproved"] = approval,
                        [$"ConnectionStrings:{connectionKey}"] = stale.ConnectionString
                    }).Build();
                var deniedRunner = new DatabaseMigrationRunner(new DatabaseConnectionStringProvider(deniedConfiguration),
                    catalog, new DatabaseRuntimePrivilegeManager(), deniedConfiguration,
                    NullLogger<DatabaseMigrationRunner>.Instance);
                if (approval == "invalid")
                {
                    var invalid = await Assert.ThrowsAsync<InvalidOperationException>(() => deniedRunner.ApplyAndVerifyAsync(code, ct));
                    Assert.Equal("business_schema_approval_invalid", invalid.Message);
                }
                else
                {
                    var denied = await Assert.ThrowsAsync<PostgresException>(() => deniedRunner.ApplyAndVerifyAsync(code, ct));
                    Assert.Equal("business_schema_explicit_consent_required", denied.MessageText);
                }
                Assert.Equal(130L, await databases.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    "select count(*) from schema_migrations", ct));
                var unchanged = await SnapshotOwnedDataAsync(databases, code, ct);
                foreach (var (table, rows) in snapshots[code]) Assert.Equal(rows, unchanged[table]);
            }
            await runner.ApplyAndVerifyAsync(code, ct);
            var after = await SnapshotOwnedDataAsync(databases, code, ct);
            foreach (var (table, beforeRows) in snapshots[code]) Assert.Equal(beforeRows, after[table]);
            await runner.ApplyAndVerifyAsync(code, ct);
        }
        await AssertApprovedSchemaAsync(databases, ct);
        Assert.Equal(11L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select (select count(*) from osan_notification_preference_profiles)+(select count(*) from osan_notification_preferences)", ct));
        Assert.Equal(2L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from projects where project_code='DUPLICATE'", ct));
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration,
            "select count(*) from projects where status='Completed' and deleted_at_utc is not null", ct));

        // The retained Cheongju functions must still work through the runtime role.
        var cheongjuAudit = new AuditStore(new CheongjuDatabase(provider), TimeProvider.System,
            NullLogger<AuditStore>.Instance);
        var access = await cheongjuAudit.RecordSiteAccessAsync(actor, Guid.NewGuid(), "Projects", "Allowed",
            null, "Safari", "macOS", ct);
        Assert.True(access.Created);
        Assert.True(await cheongjuAudit.EndSiteAccessAsync(actor, access.SessionId, access.IdempotencyReceipt, ct));
        // Osan cannot call the retired SECURITY DEFINER functions even directly.
        foreach (var sql in new[]
                 {
                     "select qms_record_site_access(null::uuid,null::uuid,null::text,null::text,null::inet,null::text,null::text)",
                     "select qms_end_site_access(null::uuid,null::uuid,null::uuid)"
                 })
        {
            var removed = await Assert.ThrowsAsync<PostgresException>(() => databases.ExecuteAsync(
                BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Runtime, sql, ct));
            Assert.Equal(PostgresErrorCodes.UndefinedFunction, removed.SqlState);
        }
    }

    private static async Task ApplyCommonSchemaBeforeSeparationAsync(IsolationDatabaseSet databases,
        DatabaseMigrationCatalog catalog, string code, CancellationToken ct)
    {
        await using var connection = await databases.OpenAsync(code, BusinessUnitConnectionPurpose.Migration, ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "create table schema_migrations(version text primary key,applied_at_utc timestamptz not null default now())";
            await command.ExecuteNonQueryAsync(ct);
        }
        foreach (var file in catalog.GetCommonMigrationFiles())
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = await File.ReadAllTextAsync(file, ct);
            await command.ExecuteNonQueryAsync(ct);
            command.CommandText = "insert into schema_migrations(version) values(@version)";
            command.Parameters.AddWithValue("version", Path.GetFileNameWithoutExtension(file));
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private static async Task<Dictionary<string, string>> SnapshotOwnedDataAsync(
        IsolationDatabaseSet databases, string code, CancellationToken ct)
    {
        var tables = code == BusinessUnitCodes.Osan ? OsanOwnedTables : CheongjuOwnedTables;
        var result = new Dictionary<string, string>();
        foreach (var table in tables.Where(table => table != "schema_migrations"))
        {
            var projection = "to_jsonb(row_value)";
            var predicate = "";
            if (table == "projects")
            {
                var columns = code == BusinessUnitCodes.Osan ? OsanProjectColumns : CheongjuProjectColumns;
                projection = "jsonb_build_object(" + string.Join(',', columns.Select(column => $"'{column}',row_value.{column}")) + ")";
            }
            else if (code == BusinessUnitCodes.Osan && table == "notifications")
                projection += " - 'work_item_id' - 'generated_by_event_id'";
            else if (code == BusinessUnitCodes.Osan && table == "notification_deliveries")
                projection += " - 'work_item_id'";
            else if (code == BusinessUnitCodes.Osan && table == "permissions")
                predicate = "where row_value.code = any(array['projects.read','Project.Read.All','Project.Create','Project.Update','Project.Delete','manufacturing.update','users.manage'])";
            else if (code == BusinessUnitCodes.Osan && table == "role_permissions")
                predicate = "where exists (select 1 from permissions retained_permission where retained_permission.id=row_value.permission_id " +
                            "and retained_permission.code = any(array['projects.read','Project.Read.All','Project.Create','Project.Update','Project.Delete','manufacturing.update','users.manage']))";
            else if (code == BusinessUnitCodes.Osan && table == "roles")
                predicate = "where row_value.code <> 'interior-busbar-manager'";
            result[table] = await databases.ReadScalarAsync<string>(code, BusinessUnitConnectionPurpose.Migration,
                $"select coalesce(jsonb_agg(value order by value::text),'[]'::jsonb)::text from (select {projection} value from {table} row_value {predicate}) rows", ct);
        }
        return result;
    }
}
