using System.Net;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Home;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Sales;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class PostgreSqlMigrationTests
{
    [Fact]
    public async Task ReviewSafeDatabaseSession_IsReadOnlyAcrossPoolReuse_AndReportsSchemaState()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var normalConfiguration = database.CreateConfiguration();
        var normalProvider = new DatabaseConnectionStringProvider(normalConfiguration);
        await CreateMigrationRunner(database.RepositoryRoot, normalProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        await using (var normalDataSource = NpgsqlDataSource.Create(normalProvider.GetConnectionString()!))
        await using (var createCommand = normalDataSource.CreateCommand(
            "create table review_safe_write_probe (id integer primary key, value text not null);"))
        {
            await createCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var reviewConfiguration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["ReviewSafe:Enabled"] = "true" });
        var reviewProvider = new DatabaseConnectionStringProvider(reviewConfiguration);
        var environment = new TestWebHostEnvironment(database.RepositoryRoot)
        {
            EnvironmentName = "Development"
        };
        var catalog = new DatabaseMigrationCatalog(environment);
        var statusService = new ReviewSafeStatusService(
            reviewProvider,
            catalog,
            new MigrationLedgerInspector(catalog),
            reviewConfiguration,
            environment);

        await using var reviewDataSource = NpgsqlDataSource.Create(reviewProvider.GetConnectionString()!);
        await using (var firstConnection = await reviewDataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal("on", await ReadConnectionScalarAsync(firstConnection, "show transaction_read_only;"));
            Assert.Equal(
                Emi.Qms.Api.ReviewSafe.ReviewSafeMode.DatabaseApplicationName,
                await ReadConnectionScalarAsync(firstConnection, "show application_name;"));
            Assert.Equal("1", await ReadConnectionScalarAsync(firstConnection, "select 1;"));
            await AssertReadOnlyFailureAsync(
                firstConnection,
                "insert into review_safe_write_probe (id, value) values (1, 'blocked');");

            await using var transaction = await firstConnection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await AssertReadOnlyFailureAsync(
                firstConnection,
                "update review_safe_write_probe set value = 'blocked' where id = 1;",
                transaction);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using (var pooledConnection = await reviewDataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal("on", await ReadConnectionScalarAsync(pooledConnection, "show transaction_read_only;"));
            await AssertReadOnlyFailureAsync(
                pooledConnection,
                "delete from review_safe_write_probe where id = 1;");
        }

        var status = await statusService.CheckAsync(TestContext.Current.CancellationToken);
        Assert.True(status.Ready);
        Assert.True(status.DatabaseReadOnly);
        Assert.Equal(status.ExpectedMigration, status.ActualMigration);
        Assert.Equal(MigrationLedgerInspector.ExactStatus, status.MigrationLedgerStatus);
        Assert.True(status.MigrationLedgerReady);
        Assert.Equal(0L, await ReadScalarAsync<long>(
            normalProvider,
            "select count(*) from review_safe_write_probe;",
            TestContext.Current.CancellationToken));

        var identityStore = new DbIdentityStore(reviewProvider, reviewConfiguration);
        Assert.Null(await identityStore.GetProfileByEntraObjectIdAsync(
            "review-safe-missing-user",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(
            normalProvider,
            "select count(*) from qms_users where entra_object_id = 'review-safe-missing-user';",
            TestContext.Current.CancellationToken));

        await using (var normalDataSource = NpgsqlDataSource.Create(normalProvider.GetConnectionString()!))
        await using (var removeLatest = normalDataSource.CreateCommand(
            "delete from schema_migrations where version = (select max(version) from schema_migrations);"))
        {
            Assert.Equal(1, await removeLatest.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        var mismatch = await statusService.CheckAsync(TestContext.Current.CancellationToken);
        Assert.False(mismatch.Ready);
        Assert.Equal("migration_ledger_missing", mismatch.Reason);
        Assert.NotEqual(mismatch.ExpectedMigration, mismatch.ActualMigration);
    }

    [Theory]
    [InlineData("Exact", true, MigrationLedgerInspector.ExactStatus, "ready", 200)]
    [InlineData("HistoricalCompatible", true, MigrationLedgerInspector.CompatibleStatus, "ready", 200)]
    [InlineData("UnknownExtra", false, MigrationLedgerInspector.MismatchStatus, "migration_ledger_unexpected", 503)]
    [InlineData("MissingCanonical", false, MigrationLedgerInspector.MismatchStatus, "migration_ledger_missing", 503)]
    [InlineData("LegacySuccessorMissing", false, MigrationLedgerInspector.MismatchStatus, "migration_ledger_legacy_successor_missing", 503)]
    [InlineData("LegacySchemaMismatch", false, MigrationLedgerInspector.MismatchStatus, "migration_ledger_legacy_schema_mismatch", 503)]
    [InlineData("SimilarUnapprovedName", false, MigrationLedgerInspector.MismatchStatus, "migration_ledger_unexpected", 503)]
    public async Task MigrationLedger_FixturesAreFailClosed(
        string fixture,
        bool expectedReady,
        string expectedStatus,
        string expectedReason,
        int expectedHttpStatus)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var normalConfiguration = database.CreateConfiguration();
        var normalProvider = new DatabaseConnectionStringProvider(normalConfiguration);
        await CreateMigrationRunner(database.RepositoryRoot, normalProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        switch (fixture)
        {
            case "HistoricalCompatible":
                await ExecuteSqlAsync(
                    normalProvider,
                    "insert into schema_migrations (version) values ('0020_teams_activity_delivery_channel');",
                    TestContext.Current.CancellationToken);
                break;
            case "UnknownExtra":
                await ExecuteSqlAsync(
                    normalProvider,
                    "insert into schema_migrations (version) values ('0099_unknown_migration');",
                    TestContext.Current.CancellationToken);
                break;
            case "MissingCanonical":
                await ExecuteSqlAsync(
                    normalProvider,
                    "delete from schema_migrations where version = '0026_notification_delivery_manual_payload';",
                    TestContext.Current.CancellationToken);
                break;
            case "LegacySuccessorMissing":
                await ExecuteSqlAsync(
                    normalProvider,
                    """
                    insert into schema_migrations (version) values ('0020_teams_activity_delivery_channel');
                    delete from schema_migrations where version = '0023_teams_activity_delivery_channel';
                    """,
                    TestContext.Current.CancellationToken);
                break;
            case "LegacySchemaMismatch":
                await ExecuteSqlAsync(
                    normalProvider,
                    """
                    insert into schema_migrations (version) values ('0020_teams_activity_delivery_channel');
                    alter table notification_deliveries drop constraint ck_notification_deliveries_channel;
                    alter table notification_deliveries add constraint ck_notification_deliveries_channel
                        check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'Mail'));
                    """,
                    TestContext.Current.CancellationToken);
                break;
            case "SimilarUnapprovedName":
                await ExecuteSqlAsync(
                    normalProvider,
                    "insert into schema_migrations (version) values ('0020_teams_activity_delivery_channels');",
                    TestContext.Current.CancellationToken);
                break;
        }

        var reviewConfiguration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["ReviewSafe:Enabled"] = "true" });
        var reviewProvider = new DatabaseConnectionStringProvider(reviewConfiguration);
        var environment = new TestWebHostEnvironment(database.RepositoryRoot)
        {
            EnvironmentName = Environments.Development
        };
        var catalog = new DatabaseMigrationCatalog(environment);
        var statusService = new ReviewSafeStatusService(
            reviewProvider,
            catalog,
            new MigrationLedgerInspector(catalog),
            reviewConfiguration,
            environment);

        var status = await statusService.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedReady, status.Ready);
        Assert.Equal(expectedStatus, status.MigrationLedgerStatus);
        Assert.Equal(expectedReason, status.Reason);
        Assert.Equal(expectedReady, status.MigrationLedgerReady);
        Assert.Equal(catalog.GetSnapshot().ExpectedCount, status.ExpectedMigrationCount);
        if (fixture == "HistoricalCompatible")
        {
            Assert.Equal(catalog.GetSnapshot().ExpectedCount + 1, status.ActualMigrationCount);
            Assert.Equal([MigrationLedgerCompatibilityPolicy.LegacyTeamsActivityVersion], status.ApprovedLegacyMigrations);
        }

        await using var factory = QmsWebApplicationFactory.Create(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["DevAuthentication:Enabled"] = "true",
                ["DevelopmentData:SeedEnabled"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["ConnectionStrings:QmsDatabase"] = normalProvider.GetConnectionString()
            },
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)expectedHttpStatus, response.StatusCode);
    }

    [Fact]
    public async Task SchemaMigration_AppliesWithoutFakeDevelopmentData()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var runner = CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider);

        await runner.ApplyAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(0, counts.Users);
        Assert.Equal(3, counts.Departments);
        Assert.Equal(0, counts.Projects);
        Assert.Equal(0, counts.ProjectAccess);
        Assert.Equal(10, counts.Roles);
        Assert.Equal(29, counts.Permissions);
        Assert.True(counts.RolePermissions > 0);
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from departments where code = 'design';",
            TestContext.Current.CancellationToken));

        await AssertCoreConstraintsExistAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertSystemHolidaySchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertWorkflowSchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertProcurementRequiredItemSchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertWorkflowAlignmentSchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertMicrosoft365IdentitySchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertAdminMasterDataSchemaAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_class
            where oid in (
                to_regclass('public.material_receipts'),
                to_regclass('public.material_iqc_attempts'),
                to_regclass('public.material_receipt_events')
            );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'project_procurement_items'
              and column_name in (
                  'order_quantity', 'order_unit',
                  'material_arrivals_closed_at_utc', 'material_arrivals_closed_by_user_id'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'user_profile_photos',
                  'user_profile_photo_audit_events'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_trigger
            where tgname = 'trg_guard_user_profile_photo_audit'
              and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_trigger
            where tgname = 'trg_guard_material_receipt_projection_write'
              and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from information_schema.tables
            where table_schema='public'
              and table_name in ('sales_monthly_targets','sales_monthly_target_audit_events');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(5L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from information_schema.tables
            where table_schema='public' and table_name in (
              'manufacturing_step_templates','manufacturing_step_template_versions',
              'manufacturing_step_template_items','form_template_manager_bindings','form_template_audit_events');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from information_schema.tables
            where table_schema='public' and table_name='logistics_batch_panels';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from pg_indexes
            where schemaname='public' and indexname='ux_logistics_batch_panels_active_stage';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from pg_indexes
            where schemaname='public' and indexname='ux_logistics_batch_units_active_stage';
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PendingTypeCatalogMigration_AddsLeastPrivilegeCatalogAndDatabaseFences()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4L, await ReadScalarAsync<long>(
            provider,
            "select count(*) from pending_issue_type_catalog where is_system and is_active;",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from role_permissions rp
            join roles r on r.id=rp.role_id
            join permissions p on p.id=rp.permission_id
            where p.code='PendingType.Manage' and r.code='system-administrator';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from role_permissions rp
            join roles r on r.id=rp.role_id
            join permissions p on p.id=rp.permission_id
            where p.code='PendingType.Manage' and r.code<>'system-administrator';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            provider,
            "select count(*) from pg_constraint where conname='fk_pending_issues_issue_type_catalog';",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*) from pg_trigger
            where tgname in ('trg_guard_pending_issue_type_catalog','trg_guard_pending_issue_type_audit')
              and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));

        var disableOther = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            provider,
            "update pending_issue_type_catalog set is_manual_enabled=false,row_version=row_version+1 where code='Other';",
            TestContext.Current.CancellationToken));
        Assert.IsType<PostgresException>(disableOther);

        var deleteSystem = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            provider,
            "delete from pending_issue_type_catalog where code='Punch';",
            TestContext.Current.CancellationToken));
        Assert.IsType<PostgresException>(deleteSystem);
    }

    [Fact]
    public async Task PendingTypeCatalogMigration_Upgrades0044DataWithoutChangingIssueTypeCodes()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var provider = new DatabaseConnectionStringProvider(configuration);
        var previousMigrations = Directory.CreateTempSubdirectory("emi-qms-migrations-through-0044-");
        try
        {
            var migrationSource = Path.Combine(database.RepositoryRoot, "database", "migrations");
            foreach (var source in Directory.GetFiles(migrationSource, "*.sql").Where(path => string.CompareOrdinal(Path.GetFileName(path), "0045_") < 0))
            {
                File.Copy(source, Path.Combine(previousMigrations.FullName, Path.GetFileName(source)));
            }

            var previousRunner = new DatabaseMigrationRunner(
                provider,
                Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog.FromPath(previousMigrations.FullName),
                new ConfigurationBuilder().Build(),
                NullLogger<DatabaseMigrationRunner>.Instance);
            await previousRunner.ApplyAsync(TestContext.Current.CancellationToken);
            await CreateSeeder(database.RepositoryRoot, "Testing", configuration, provider)
                .SeedAsync(TestContext.Current.CancellationToken);
            await ExecuteSqlAsync(
                provider,
                """
                insert into pending_issues (
                    id,project_id,target_type,target_id,issue_type,title,description,status,priority,
                    created_by_user_id,updated_by_user_id)
                values (
                    '85000000-0000-0000-0000-000000000045',
                    '40000000-0000-0000-0000-000000000001',
                    'Project','40000000-0000-0000-0000-000000000001','Punch',
                    '0045 upgrade PUNCH','기존 Pending 유형 코드를 그대로 보존하는 마이그레이션 검증입니다.',
                    'Registered','Normal',
                    '50000000-0000-0000-0000-000000000005','50000000-0000-0000-0000-000000000005');
                """,
                TestContext.Current.CancellationToken);

            await CreateMigrationRunner(database.RepositoryRoot, provider)
                .ApplyAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Punch", await ReadScalarAsync<string>(
                provider,
                "select issue_type from pending_issues where id='85000000-0000-0000-0000-000000000045';",
                TestContext.Current.CancellationToken));
            Assert.Equal("PUNCH", await ReadScalarAsync<string>(
                provider,
                """
                select catalog.display_name
                from pending_issues issue
                join pending_issue_type_catalog catalog on catalog.code=issue.issue_type
                where issue.id='85000000-0000-0000-0000-000000000045';
                """,
                TestContext.Current.CancellationToken));
            Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
                provider,
                "select max(version) from schema_migrations;",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            previousMigrations.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task SalesAndFormTemplateMigrations_UpgradeFrom0042()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        var previousMigrations = Directory.CreateTempSubdirectory("emi-qms-migrations-through-0042-");
        try
        {
            var migrationSource = Path.Combine(database.RepositoryRoot, "database", "migrations");
            foreach (var source in Directory.GetFiles(migrationSource, "*.sql").Where(path => string.CompareOrdinal(Path.GetFileName(path), "0043_") < 0))
            {
                File.Copy(source, Path.Combine(previousMigrations.FullName, Path.GetFileName(source)));
            }

            var configuration = new ConfigurationBuilder().Build();
            var previousRunner = new DatabaseMigrationRunner(
                provider,
                Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog.FromPath(previousMigrations.FullName),
                configuration,
                NullLogger<DatabaseMigrationRunner>.Instance);
            await previousRunner.ApplyAsync(TestContext.Current.CancellationToken);

            await CreateMigrationRunner(database.RepositoryRoot, provider)
                .ApplyAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Directory.GetFiles(migrationSource, "*.sql").LongLength, await ReadScalarAsync<long>(
                provider,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
            Assert.Equal(7L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*) from information_schema.tables
                where table_schema='public' and table_name in (
                  'sales_monthly_targets','sales_monthly_target_audit_events',
                  'manufacturing_step_templates','manufacturing_step_template_versions',
                  'manufacturing_step_template_items','form_template_manager_bindings','form_template_audit_events');
                """,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            previousMigrations.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProductionControlSingleCurrentMigration_CollapsesVersionsAndPlanConnections()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var provider = new DatabaseConnectionStringProvider(configuration);
        var previousMigrations = Directory.CreateTempSubdirectory("emi-qms-migrations-through-0059-");
        try
        {
            var migrationSource = Path.Combine(database.RepositoryRoot, "database", "migrations");
            foreach (var source in Directory.GetFiles(migrationSource, "*.sql").Where(path => string.CompareOrdinal(Path.GetFileName(path), "0060_") < 0))
            {
                File.Copy(source, Path.Combine(previousMigrations.FullName, Path.GetFileName(source)));
            }

            var previousRunner = new DatabaseMigrationRunner(
                provider,
                Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog.FromPath(previousMigrations.FullName),
                new ConfigurationBuilder().Build(),
                NullLogger<DatabaseMigrationRunner>.Instance);
            await previousRunner.ApplyAsync(TestContext.Current.CancellationToken);
            await CreateSeeder(database.RepositoryRoot, "Testing", configuration, provider)
                .SeedAsync(TestContext.Current.CancellationToken);

            await ExecuteSqlAsync(
                provider,
                """
                insert into production_control_manufacturing_templates (id, product_type_id)
                select '91000000-0000-0000-0000-000000000001', id
                from production_product_types where code='UL67';
                insert into production_control_manufacturing_versions (
                    id,template_id,version_number,lifecycle_status,activated_at_utc
                )
                values
                    ('91000000-0000-0000-0000-000000000011','91000000-0000-0000-0000-000000000001',1,'Active',now()),
                    ('91000000-0000-0000-0000-000000000012','91000000-0000-0000-0000-000000000001',2,'Draft',null);
                insert into production_control_manufacturing_items (
                    template_version_id,definition_key,display_order,label,step_role
                )
                values
                    ('91000000-0000-0000-0000-000000000011','91000000-0000-0000-0000-000000000021',1,'이전 제조','Assembly'),
                    ('91000000-0000-0000-0000-000000000012','91000000-0000-0000-0000-000000000022',1,'현재 제조','Assembly');

                insert into production_control_plan_templates (id, product_type_id)
                select '92000000-0000-0000-0000-000000000001', id
                from production_product_types where code='UL67';
                insert into production_control_plan_versions (
                    id,template_id,version_number,lifecycle_status,activated_at_utc
                )
                values
                    ('92000000-0000-0000-0000-000000000011','92000000-0000-0000-0000-000000000001',1,'Active',now()),
                    ('92000000-0000-0000-0000-000000000012','92000000-0000-0000-0000-000000000001',2,'Draft',null);
                insert into production_control_plan_items (
                    id,template_version_id,definition_key,display_order,label,is_required
                )
                values
                    ('92000000-0000-0000-0000-000000000021','92000000-0000-0000-0000-000000000011','92000000-0000-0000-0000-000000000031',1,'이전 계획',true),
                    ('92000000-0000-0000-0000-000000000022','92000000-0000-0000-0000-000000000012','92000000-0000-0000-0000-000000000032',1,'현재 계획',true);
                insert into production_control_plan_connections (
                    plan_item_id,source_code,source_definition_key
                )
                values
                    ('92000000-0000-0000-0000-000000000022','PURCHASE_ORDERED',null),
                    ('92000000-0000-0000-0000-000000000022','OQC_PASSED',null);
                """,
                TestContext.Current.CancellationToken);

            await CreateMigrationRunner(database.RepositoryRoot, provider)
                .ApplyAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                "select count(*) from production_control_manufacturing_versions where template_id='91000000-0000-0000-0000-000000000001';",
                TestContext.Current.CancellationToken));
            Assert.Equal("91000000-0000-0000-0000-000000000012", await ReadScalarAsync<string>(
                provider,
                "select id::text from production_control_manufacturing_versions where lifecycle_status='Active' and template_id='91000000-0000-0000-0000-000000000001';",
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                "select count(*) from production_control_plan_versions where template_id='92000000-0000-0000-0000-000000000001';",
                TestContext.Current.CancellationToken));
            Assert.Equal("OQC_PASSED", await ReadScalarAsync<string>(
                provider,
                """
                select connection.source_code
                from production_control_plan_connections connection
                join production_control_plan_items item on item.id=connection.plan_item_id
                where item.template_version_id='92000000-0000-0000-0000-000000000012';
                """,
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                "select count(*) from pg_indexes where indexname='ux_production_control_plan_connections_one_to_one';",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            previousMigrations.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProductionControlOqcAggregateMigration_NormalizesCurrentTemplateAndPreservesProjectSnapshot()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var provider = new DatabaseConnectionStringProvider(configuration);
        var migrationsThrough0061 = Directory.CreateTempSubdirectory("emi-qms-migrations-through-0061-");
        try
        {
            var migrationSource = Path.Combine(database.RepositoryRoot, "database", "migrations");
            foreach (var source in Directory.GetFiles(migrationSource, "*.sql")
                         .Where(path => string.CompareOrdinal(Path.GetFileName(path), "0062_") < 0))
            {
                File.Copy(source, Path.Combine(migrationsThrough0061.FullName, Path.GetFileName(source)));
            }

            var previousRunner = new DatabaseMigrationRunner(
                provider,
                Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog.FromPath(migrationsThrough0061.FullName),
                new ConfigurationBuilder().Build(),
                NullLogger<DatabaseMigrationRunner>.Instance);
            await previousRunner.ApplyAsync(TestContext.Current.CancellationToken);
            await CreateSeeder(database.RepositoryRoot, "Testing", configuration, provider)
                .SeedAsync(TestContext.Current.CancellationToken);

            await ExecuteSqlAsync(
                provider,
                """
                insert into production_control_plan_templates (id, product_type_id)
                select '93000000-0000-0000-0000-000000000001', id
                from production_product_types where code='UL67';
                insert into production_control_plan_versions (
                    id,template_id,version_number,lifecycle_status,activated_at_utc
                )
                values (
                    '93000000-0000-0000-0000-000000000011',
                    '93000000-0000-0000-0000-000000000001',
                    1,'Active',now()
                );
                insert into production_control_plan_items (
                    id,template_version_id,definition_key,display_order,label,is_required
                )
                values (
                    '93000000-0000-0000-0000-000000000021',
                    '93000000-0000-0000-0000-000000000011',
                    '93000000-0000-0000-0000-000000000031',
                    1,'OQC 완료',true
                );
                insert into production_control_plan_connections (
                    plan_item_id,source_code,source_definition_key
                )
                values (
                    '93000000-0000-0000-0000-000000000021',
                    'OQC_PASSED',
                    '93000000-0000-0000-0000-000000000041'
                );

                insert into project_production_plans (
                    id,project_id,model_version
                )
                select
                    '93000000-0000-0000-0000-000000000051',
                    project.id,
                    'LEGACY'
                from projects project
                where not exists (
                    select 1 from project_production_plans plan where plan.project_id=project.id
                )
                order by project.id
                limit 1;
                insert into project_production_plan_items (
                    id,production_plan_id,sequence_number,step_name_snapshot,is_required,is_active
                )
                values (
                    '93000000-0000-0000-0000-000000000052',
                    '93000000-0000-0000-0000-000000000051',
                    1,'기존 프로젝트 OQC 연결',true,true
                );
                insert into project_production_plan_connections (
                    production_plan_item_id,source_code,source_definition_key
                )
                values (
                    '93000000-0000-0000-0000-000000000052',
                    'OQC_PASSED',
                    '93000000-0000-0000-0000-000000000042'
                );
                """,
                TestContext.Current.CancellationToken);

            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*)
                from project_production_plan_connections
                where source_code='OQC_PASSED'
                  and source_definition_key='93000000-0000-0000-0000-000000000042';
                """,
                TestContext.Current.CancellationToken));

            await CreateMigrationRunner(database.RepositoryRoot, provider)
                .ApplyAsync(TestContext.Current.CancellationToken);

            Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
                provider,
                "select max(version) from schema_migrations;",
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*)
                from production_control_plan_connections
                where plan_item_id='93000000-0000-0000-0000-000000000021'
                  and source_code='OQC_PASSED'
                  and source_definition_key is null;
                """,
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*)
                from project_production_plan_connections
                where source_code='OQC_PASSED'
                  and source_definition_key='93000000-0000-0000-0000-000000000042';
                """,
                TestContext.Current.CancellationToken));

            var invalidCurrentOqcDetail = await Record.ExceptionAsync(() => ExecuteSqlAsync(
                provider,
                """
                update production_control_plan_connections
                set source_definition_key='93000000-0000-0000-0000-000000000043'
                where plan_item_id='93000000-0000-0000-0000-000000000021';
                """,
                TestContext.Current.CancellationToken));
            Assert.IsType<PostgresException>(invalidCurrentOqcDetail);
        }
        finally
        {
            migrationsThrough0061.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProductionPlanItemStaffingMigration_AddsNullableStaffingAndValidatesHeadcount()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var provider = new DatabaseConnectionStringProvider(configuration);
        var migrationsThrough0062 = Directory.CreateTempSubdirectory("emi-qms-migrations-through-0062-");
        try
        {
            var migrationSource = Path.Combine(database.RepositoryRoot, "database", "migrations");
            foreach (var source in Directory.GetFiles(migrationSource, "*.sql")
                         .Where(path => string.CompareOrdinal(Path.GetFileName(path), "0063_") < 0))
            {
                File.Copy(source, Path.Combine(migrationsThrough0062.FullName, Path.GetFileName(source)));
            }

            var previousRunner = new DatabaseMigrationRunner(
                provider,
                Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog.FromPath(migrationsThrough0062.FullName),
                new ConfigurationBuilder().Build(),
                NullLogger<DatabaseMigrationRunner>.Instance);
            await previousRunner.ApplyAsync(TestContext.Current.CancellationToken);
            await CreateSeeder(database.RepositoryRoot, "Testing", configuration, provider)
                .SeedAsync(TestContext.Current.CancellationToken);
            await ExecuteSqlAsync(
                provider,
                """
                insert into project_production_plans (id,project_id,model_version)
                select
                    '94000000-0000-0000-0000-000000000001',
                    project.id,
                    'LEGACY'
                from projects project
                where not exists (
                    select 1 from project_production_plans plan where plan.project_id=project.id
                )
                order by project.id
                limit 1;
                insert into project_production_plan_items (
                    id,production_plan_id,sequence_number,step_name_snapshot,is_required,is_active
                )
                select
                    '94000000-0000-0000-0000-000000000002',
                    plan.id,
                    1,
                    '기존 생산계획 항목',
                    true,
                    true
                from project_production_plans plan
                order by plan.id
                limit 1;
                """,
                TestContext.Current.CancellationToken);

            Assert.True(await ReadScalarAsync<long>(
                provider,
                "select count(*) from project_production_plan_items;",
                TestContext.Current.CancellationToken) > 0);

            await CreateMigrationRunner(database.RepositoryRoot, provider)
                .ApplyAsync(TestContext.Current.CancellationToken);

            Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
                provider,
                "select max(version) from schema_migrations;",
                TestContext.Current.CancellationToken));
            Assert.Equal(2L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*)
                from information_schema.columns
                where table_schema='public'
                  and table_name='project_production_plan_items'
                  and column_name in ('assigned_user_id','required_headcount');
                """,
                TestContext.Current.CancellationToken));
            Assert.Equal(0L, await ReadScalarAsync<long>(
                provider,
                """
                select count(*)
                from project_production_plan_items
                where assigned_user_id is not null or required_headcount is not null;
                """,
                TestContext.Current.CancellationToken));

            await ExecuteSqlAsync(
                provider,
                """
                update project_production_plan_items
                set assigned_user_id=(select id from qms_users where is_active order by id limit 1),
                    required_headcount=3
                where id=(select id from project_production_plan_items order by id limit 1);
                """,
                TestContext.Current.CancellationToken);
            Assert.Equal(3, await ReadScalarAsync<int>(
                provider,
                """
                select required_headcount
                from project_production_plan_items
                where required_headcount is not null
                limit 1;
                """,
                TestContext.Current.CancellationToken));

            var invalidHeadcount = await Record.ExceptionAsync(() => ExecuteSqlAsync(
                provider,
                """
                update project_production_plan_items
                set required_headcount=0
                where id=(select id from project_production_plan_items order by id limit 1);
                """,
                TestContext.Current.CancellationToken));
            Assert.IsType<PostgresException>(invalidHeadcount);
        }
        finally
        {
            migrationsThrough0062.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Ul891SetProductionPlanMigration_AddsSetScopeAndValueOverlays()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
            provider,
            "select max(version) from schema_migrations;",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from information_schema.tables
            where table_schema='public'
              and table_name in (
                'project_production_plan_set_scopes',
                'project_production_plan_set_item_values'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from pg_indexes
            where schemaname='public'
              and indexname in (
                'ux_project_production_plan_set_scopes_plan_instance',
                'ux_project_production_plan_set_item_values_scope_item'
              );
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SalesKpiStore_AggregatesMonthlyRevenueTargetsAndPipeline()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var actorId = new Guid("76000000-0000-0000-0000-000000000043");
        await ExecuteSqlAsync(provider, $"""
            insert into departments (id,code,name,sort_order)
            values ('76000000-0000-0000-0000-000000000051','sales','Sales',20)
            on conflict (code) do nothing;

            insert into qms_users (id,development_user_key,display_name,department_id,is_active)
            values ('{actorId}','sales-kpi-test','Sales KPI Test',(select id from departments where code='sales'),true);

            insert into projects (
                id,project_key,project_number,name,project_code,project_title,project_title_normalized,
                status,sales_amount,currency_code)
            values
              ('76000000-0000-0000-0000-000000000044','sales-kpi-jan','SALES-KPI-001','January Revenue','SALES-KPI-001','January Revenue','JANUARY REVENUE','Completed',120000000,'KRW'),
              ('76000000-0000-0000-0000-000000000045','sales-kpi-mar','SALES-KPI-002','March Revenue','SALES-KPI-002','March Revenue','MARCH REVENUE','Completed',80000000,'KRW'),
              ('76000000-0000-0000-0000-000000000046','sales-kpi-pipeline','SALES-KPI-003','Active Pipeline','SALES-KPI-003','Active Pipeline','ACTIVE PIPELINE','Active',300000000,'KRW');

            insert into sales_settlements (
                id,project_id,status,invoice_issued_date,version,created_by_user_id,updated_by_user_id,
                completed_by_user_id,completed_at_utc)
            values
              ('76000000-0000-0000-0000-000000000047','76000000-0000-0000-0000-000000000044','Completed','2026-01-15',1,'{actorId}','{actorId}','{actorId}',now()),
              ('76000000-0000-0000-0000-000000000048','76000000-0000-0000-0000-000000000045','Completed','2026-03-20',1,'{actorId}','{actorId}','{actorId}',now());
            """, TestContext.Current.CancellationToken);

        var store = new SalesKpiStore(provider, TimeProvider.System);
        var targets = await store.SaveTargetsAsync(
            new SaveSalesTargetsRequest(2026, "krw", [
                new SaveSalesTargetMonthRequest(1, 100000000m, null),
                new SaveSalesTargetMonthRequest(2, 100000000m, null),
                new SaveSalesTargetMonthRequest(3, 100000000m, null)
            ]), actorId, TestContext.Current.CancellationToken);
        var response = await store.GetAsync(2026, "KRW", new ProjectAccessScope(true, []), TestContext.Current.CancellationToken);

        Assert.Equal(12, response.Months.Count);
        Assert.Equal(120000000m, response.Months[0].RevenueAmount);
        Assert.Equal(0m, response.Months[1].RevenueAmount);
        Assert.Equal(80000000m, response.Months[2].RevenueAmount);
        Assert.Equal(200000000m, response.Kpi.RevenueTotal);
        Assert.Equal(300000000m, response.Kpi.TargetTotal);
        Assert.Equal(66.7m, response.Kpi.AchievementRate);
        Assert.Equal(100000000m, response.Kpi.RemainingTargetAmount);
        Assert.Equal(300000000m, response.Pipeline.Amount);
        Assert.Equal(3, targets.Months.Count(month => month.Amount.HasValue));
        Assert.Equal(3L, await ReadScalarAsync<long>(provider,
            "select count(*) from sales_monthly_target_audit_events;",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FormTemplateStore_SavesOneCurrentQualityFormAndKeepsDepartmentManagerFence()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var adminId = new Guid("76000000-0000-0000-0000-000000000049");
        var qualityManagerId = new Guid("76000000-0000-0000-0000-000000000050");
        await ExecuteSqlAsync(provider, $"""
            insert into departments (id,code,name,sort_order)
            values
              ('76000000-0000-0000-0000-000000000052','quality','Quality',80),
              ('76000000-0000-0000-0000-000000000053','manufacturing','Manufacturing',70)
            on conflict (code) do nothing;

            insert into qms_users (id,development_user_key,display_name,department_id,is_active)
            values
              ('{adminId}','form-admin-test','Form Admin Test',(select id from departments where code='design'),true),
              ('{qualityManagerId}','form-quality-test','Quality Manager Test',(select id from departments where code='quality'),true);
            """, TestContext.Current.CancellationToken);

        var store = new FormTemplateStore(provider);
        var catalog = await store.GetCatalogAsync(adminId, true, TestContext.Current.CancellationToken);
        Assert.Equal(3, catalog.Templates.Count);
        Assert.DoesNotContain(catalog.Templates, template => template.TemplateKey is "CustomerInspection" or "FAT" or "PANEL_MANUFACTURING");
        var original = await store.GetCurrentAsync("IqcReport", "MATERIAL_IQC", adminId, true, TestContext.Current.CancellationToken);
        var active = Assert.Single(original.Versions);
        var edited = active.Items.Select((item, index) => new SaveFormTemplateItemRequest(
            item.ItemCode, index + 1, index == 0 ? "관리 화면에서 변경한 검사 항목" : item.Label,
            item.Guidance, item.ResponseType, item.IsRequired, item.RequiresPhoto, item.MaxTextLength,
            item.DefinitionKey)).ToArray();
        var saved = await store.SaveCurrentAsync(
            "IqcReport", "MATERIAL_IQC",
            new SaveFormTemplateItemsRequest(active.RowVersion, edited), adminId, true,
            TestContext.Current.CancellationToken);
        var current = Assert.Single(saved.Versions);
        Assert.Equal("Active", current.LifecycleStatus);
        Assert.NotEqual(active.VersionId, current.VersionId);
        Assert.Equal(active.Items[0].DefinitionKey, current.Items[0].DefinitionKey);
        Assert.Equal("관리 화면에서 변경한 검사 항목", current.Items[0].Label);
        Assert.Equal(2, (await store.GetVersionsAsync(
            "IqcReport", "MATERIAL_IQC", adminId, true,
            TestContext.Current.CancellationToken)).Versions.Count);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(provider,
            $"update iqc_report_template_items set label='forbidden' where template_version_id='{active.VersionId}';",
            TestContext.Current.CancellationToken));

        var managers = await store.AssignManagerAsync(
            new AssignFormTemplateManagerRequest(qualityManagerId, "Quality"), adminId, true,
            TestContext.Current.CancellationToken);
        Assert.Single(managers.Bindings, binding => binding.UserId == qualityManagerId && binding.RevokedAtUtc is null);
        Assert.True((await store.GetScopeAsync(qualityManagerId, false, TestContext.Current.CancellationToken)).CanManage);
        await ExecuteSqlAsync(provider,
            $"update qms_users set department_id=(select id from departments where code='manufacturing') where id='{qualityManagerId}';",
            TestContext.Current.CancellationToken);
        Assert.False((await store.GetScopeAsync(qualityManagerId, false, TestContext.Current.CancellationToken)).CanManage);
        await Assert.ThrowsAsync<FormTemplateForbiddenException>(() => store.RecordExportAsync(
            "IqcReport", "MATERIAL_IQC", qualityManagerId, false, 1,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProfilePhotoStoreAndDepartmentHomeQueries_RunAgainstFreshSchema()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);
        var userId = new Guid("76000000-0000-0000-0000-000000000042");
        await ExecuteSqlAsync(
            connectionStringProvider,
            $"""
            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values (
                '{userId}',
                'profile-home-query-test',
                'Profile Home Query Test',
                (select id from departments where code='sales'),
                true
            );
            """,
            TestContext.Current.CancellationToken);

        var profileStore = new UserProfilePhotoStore(connectionStringProvider);
        var firstPhoto = CreateStructurallyValidPng(64, 32);
        var secondPhoto = CreateStructurallyValidPng(96, 48);
        var first = await profileStore.SaveAsync(userId, firstPhoto, TestContext.Current.CancellationToken);
        var second = await profileStore.SaveAsync(userId, secondPhoto, TestContext.Current.CancellationToken);
        var stored = await profileStore.GetAsync(userId, TestContext.Current.CancellationToken);

        Assert.Equal("image/png", first.NormalizedMime);
        Assert.NotEqual(first.ProfilePhotoVersion, second.ProfilePhotoVersion);
        Assert.NotNull(stored);
        Assert.Equal(secondPhoto, stored.Content);
        Assert.True(await profileStore.RemoveAsync(userId, TestContext.Current.CancellationToken));
        Assert.Null(await profileStore.GetAsync(userId, TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from user_profile_photo_audit_events where profile_user_id='76000000-0000-0000-0000-000000000042';",
            TestContext.Current.CancellationToken));

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into projects (id, project_key, project_number, name)
            values ('76000000-0000-0000-0000-000000000052', 'home-material-risk', 'HOME-MATERIAL-001', 'Home Material Risk');

            insert into project_procurement_items (
                id, project_id, sequence_number, supply_type, order_quantity, order_unit, expected_receipt_date)
            values (
                '76000000-0000-0000-0000-000000000053',
                '76000000-0000-0000-0000-000000000052',
                1, 'CustomerSupplied', 12, 'EA', current_date - 3);

            insert into material_receipts (
                id, procurement_item_id, quantity, unit, arrival_date, status)
            values (
                '76000000-0000-0000-0000-000000000054',
                '76000000-0000-0000-0000-000000000053',
                6, 'EA', current_date, 'Arrived');
            """,
            TestContext.Current.CancellationToken);

        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            QmsPermissions.ProjectRead,
            QmsPermissions.PanelInfoUpdate,
            QmsPermissions.ProductionPlanUpdate,
            QmsPermissions.ProcurementPlanUpdate,
            QmsPermissions.MaterialReceiptUpdate,
            QmsPermissions.ManufacturingUpdate,
            QmsPermissions.QualityInspect,
            QmsPermissions.LogisticsShip
        };
        var metricsStore = new HomeMetricsStore(connectionStringProvider);
        var scope = new ProjectAccessScope(true, []);
        string[] departments =
        [
            "administration", "sales", "design", "production-planning", "procurement",
            "materials", "manufacturing", "quality", "logistics"
        ];

        foreach (var department in departments)
        {
            var response = await metricsStore.GetAsync(
                department,
                department,
                userId,
                permissions,
                department == "administration",
                scope,
                TestContext.Current.CancellationToken);

            Assert.Equal(3, response.Metrics.Count);
            Assert.All(response.Metrics, metric => Assert.True(metric.Count >= 0));
            if (department == "materials")
            {
                var riskMetric = Assert.Single(response.Metrics, metric => metric.Id == "materials-customer-supply-overdue");
                Assert.Equal(1, riskMetric.Count);
                Assert.Equal("materials-customer-supply-overdue", riskMetric.DestinationKey);
            }
        }
    }

    [Fact]
    public async Task PendingActionPhotoMigration_AddsBoundedEvidenceAndAppendOnlyGuards()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
            provider,
            "select max(version) from schema_migrations;",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from information_schema.tables
            where table_schema='public'
              and table_name in ('pending_action_photos','pending_photo_operations');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from pg_trigger
            where not tgisinternal
              and tgname in (
                'trg_guard_pending_action_photo_evidence',
                'trg_guard_pending_photo_operation_append_only'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from pg_constraint
            where conname='uq_pending_action_photos_content';
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MaterialCategoryScanIqcMigration_PreservesExistingProjectsAndAddsImmutableEvidence()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
            provider,
            "select max(version) from schema_migrations;",
            TestContext.Current.CancellationToken));
        Assert.Equal(5L, await ReadScalarAsync<long>(
            provider,
            "select count(*) from material_categories where is_active;",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            provider,
            "select count(*) from material_categories where code='ENCLOSURE' and requires_iqc;",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(
            provider,
            "select count(*) from projects where iqc_routing_policy <> 'AllReceipts';",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from information_schema.tables
            where table_schema='public'
              and table_name in ('material_iqc_scan_reports','material_iqc_scan_attachments');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            provider,
            """
            select count(*)
            from pg_trigger
            where not tgisinternal
              and tgname in (
                'trg_guard_project_iqc_routing_policy_immutable',
                'trg_guard_finalized_material_iqc_scan_report',
                'trg_guard_finalized_material_iqc_scan_attachment'
              );
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NotificationDeliveryClaimLeaseMigration_AddsProcessingClaimsAttemptsAndIndexes()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var runner = CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider);

        await runner.ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            Directory.GetFiles(Path.Combine(database.RepositoryRoot, "database", "migrations"), "*.sql").LongLength,
            await ReadScalarAsync<long>(
                connectionStringProvider,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        Assert.Equal("0067_material_category_scan_iqc", await ReadScalarAsync<string>(
            connectionStringProvider,
            "select max(version) from schema_migrations;",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from information_schema.tables where table_schema='public' and table_name='notification_delivery_reprocess_events';",
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema='public'
              and (
                (table_name='notification_deliveries' and column_name in ('current_generation','generation_attempt_count'))
                or (table_name='notification_delivery_attempts' and column_name='generation')
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'user_notification_preference_profiles',
                  'user_notification_preferences',
                  'user_notification_preference_audit_events'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_constraint
            where conname = 'ck_user_notification_preferences_sparse_opt_out';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name = 'data_export_events';
            """,
            TestContext.Current.CancellationToken));
        Assert.Contains("ProjectsSelected", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_data_export_events_kind';
            """,
            TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("QualityInspectionsSelected", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_data_export_events_kind';
            """,
            TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_trigger
            where tgname = 'trg_guard_data_export_event'
              and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'panel_manufacturing_executions',
                  'panel_manufacturing_execution_steps',
                  'panel_manufacturing_events',
                  'panel_manufacturing_operations'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name = 'panel_manufacturing_assembly_batch_operations';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'panel_manufacturing_events'
              and column_name = 'batch_operation_id';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'pending_issues'
              and column_name = 'action_department_code';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(18L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from panel_quality_template_items;",
            TestContext.Current.CancellationToken));
        Assert.Equal(9L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'panel_quality_template_versions', 'panel_quality_template_items',
                  'panel_quality_inspection_attempts', 'panel_quality_reports',
                  'panel_quality_report_responses', 'panel_quality_report_photos',
                  'panel_quality_report_pdf_artifacts',
                  'panel_manufacturing_completion_confirmations', 'panel_quality_operations'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*) from pg_trigger
            where tgname in (
                'trg_guard_panel_quality_pdf_artifact_immutable',
                'trg_guard_finalized_panel_quality_responses',
                'trg_guard_finalized_panel_quality_photos'
            ) and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(6L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from iqc_report_template_items;",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'material_iqc_attempts'
              and column_name = 'decision_mode'
              and is_nullable = 'NO'
              and column_default like '%Detailed%';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(7L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'iqc_report_templates', 'iqc_report_template_versions', 'iqc_report_template_items',
                  'iqc_reports', 'iqc_report_responses', 'iqc_report_photos', 'iqc_report_pdf_artifacts'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_trigger
            where tgname = 'trg_guard_iqc_report_pdf_artifact_immutable'
              and not tgisinternal;
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'project_procurement_items'
              and column_name = 'supply_type'
              and is_nullable = 'NO'
              and column_default like '%Purchased%';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_constraint
            where conrelid = 'public.project_procurement_items'::regclass
              and conname in (
                  'ck_project_procurement_items_supply_type',
                  'ck_project_procurement_items_customer_supply_measurement'
              );
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where schemaname = 'public'
              and indexname = 'ix_project_procurement_items_supply_type';
            """,
            TestContext.Current.CancellationToken));

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into projects (id, project_key, project_number, name)
            values ('88000000-0000-0000-0000-000000000031', 'migration-0031', 'migration-0031', 'Migration 0031');
            """,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into project_procurement_items (project_id, sequence_number, supply_type)
            values ('88000000-0000-0000-0000-000000000031', 1, 'CustomerSupplied');
            """,
            TestContext.Current.CancellationToken));
        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into project_procurement_items (
                project_id, sequence_number, supply_type, order_quantity, order_unit)
            values (
                '88000000-0000-0000-0000-000000000031', 1, 'CustomerSupplied', 5, 'EA');
            """,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
            update project_procurement_items
            set order_unit = null
            where project_id = '88000000-0000-0000-0000-000000000031';
            """,
            TestContext.Current.CancellationToken));

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into notification_deliveries (channel, delivery_type, status, dedupe_key)
            values ('TeamsChannel', 'ManualTest', 'Pending', 'migration-0028-preserved-row');

            update notification_deliveries
            set status = 'Processing',
                attempt_count = 1,
                claim_token = uuid_generate_v4(),
                claimed_at_utc = '2026-07-11T00:00:00Z',
                claim_expires_at_utc = '2026-07-11T00:05:00Z',
                claimed_by_instance_id = 'opaque-test-worker'
            where dedupe_key = 'migration-0028-preserved-row';

            insert into notification_delivery_attempts (
                delivery_id, attempt_no, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, outcome
            )
            select id, attempt_count, claim_token, claimed_by_instance_id,
                   claimed_at_utc, claim_expires_at_utc, 'Processing'
            from notification_deliveries
            where dedupe_key = 'migration-0028-preserved-row';
            """,
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from notification_delivery_attempts where outcome = 'Processing';",
            TestContext.Current.CancellationToken));
        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where schemaname = 'public'
              and indexname in (
                'ix_notification_deliveries_claim_due',
                'ix_notification_deliveries_claim_owner',
                'ix_notification_delivery_attempts_delivery',
                'ix_notification_delivery_attempts_processing_lease'
              );
            """,
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into notification_deliveries (channel, delivery_type, status, dedupe_key)
            values ('TeamsChannel', 'ManualTest', 'Processing', 'migration-0028-invalid-claim');
            """,
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into notification_delivery_attempts (
                delivery_id, attempt_no, claim_token, worker_instance_id,
                claimed_at_utc, lease_expires_at_utc, outcome
            )
            select id, 1, uuid_generate_v4(), 'opaque-test-worker',
                   '2026-07-11T00:00:00Z', '2026-07-11T00:05:00Z', 'Processing'
            from notification_deliveries
            where dedupe_key = 'migration-0028-preserved-row';
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LogisticsMigration_AddsExecutionEvidenceConcurrencyAndImmutabilitySchema()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        var runner = CreateMigrationRunner(database.RepositoryRoot, provider);

        await runner.ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(8L, await ReadScalarAsync<long>(provider, """
            select count(*) from information_schema.tables
            where table_schema='public' and table_name in (
              'logistics_packing_units','logistics_packing_unit_panels','logistics_batches',
              'logistics_batch_units','logistics_batch_panels','logistics_evidence','logistics_delivery_results','logistics_operations'
            );
            """, TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(provider, """
            select count(*) from pg_indexes where schemaname='public'
              and indexname in ('ux_logistics_packing_unit_panels_active_panel','ux_logistics_batch_panels_active_stage');
            """, TestContext.Current.CancellationToken));
        Assert.Equal(6L, await ReadScalarAsync<long>(provider, """
            select count(*) from pg_trigger where not tgisinternal and tgname in (
              'trg_guard_finalized_logistics_packing_unit','trg_guard_finalized_logistics_batch',
              'trg_guard_logistics_packing_unit_panel','trg_guard_logistics_batch_unit',
              'trg_guard_logistics_batch_panel','trg_guard_logistics_evidence'
            );
            """, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SalesSettlementMigration_AddsLeastPrivilegeLifecycleAndRaceFences()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(database.CreateConfiguration());
        await CreateMigrationRunner(database.RepositoryRoot, provider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2L, await ReadScalarAsync<long>(provider, """
            select count(*) from information_schema.tables
            where table_schema='public' and table_name in ('sales_settlements','sales_settlement_operations');
            """, TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ReadScalarAsync<long>(provider, """
            select count(*) from information_schema.columns
            where table_schema='public' and table_name='projects'
              and column_name in ('completed_by_user_id','completed_at_utc');
            """, TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(provider, """
            select count(*) from pg_trigger where not tgisinternal and tgname in (
              'trg_guard_completed_sales_settlement','trg_guard_sales_settlement_operation','trg_guard_pending_project_lifecycle');
            """, TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ReadScalarAsync<long>(provider, """
            select count(*) from roles role
            join role_permissions assignment on assignment.role_id=role.id
            join permissions permission on permission.id=assignment.permission_id
            where permission.code='sales.settle' and role.code='sales';
            """, TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(provider, """
            select count(*) from roles role
            join role_permissions assignment on assignment.role_id=role.id
            join permissions permission on permission.id=assignment.permission_id
            where permission.code='sales.settle' and role.code<>'sales';
            """, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SchemaMigration_AssignsConfirmedProjectAndSensitivePermissions()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var runner = CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider);

        await runner.ApplyAsync(TestContext.Current.CancellationToken);

        await AssertPermissionScopeAlignmentAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PendingListFoundationMigration_AddsSchemaAndLeastPrivilegePermissions()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in ('pending_issues', 'pending_comments', 'pending_history');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(10L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Pending.Read';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(8L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Pending.Manage';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Pending.Manage'
              and roles.code in ('system-administrator', 'read-only');
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where schemaname = 'public'
              and indexname in ('ix_pending_issues_open_priority', 'ix_pending_issues_project', 'ix_pending_issues_assignee');
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PermissionScopeAlignmentMigration_AppliesAfterExisting0001WithoutDataLossOrDuplicates()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);

        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        {
            await using var command = dataSource.CreateCommand("""
                insert into departments (id, code, name)
                values ('70000000-0000-0000-0000-000000000001', 'existing-test', 'Existing Test')
                on conflict (code) do nothing;

                insert into qms_users (id, development_user_key, display_name, department_id, is_active)
                values (
                    '70000000-0000-0000-0000-000000000002',
                    'existing-user',
                    'Existing User',
                    '70000000-0000-0000-0000-000000000001',
                    true
                )
                on conflict (development_user_key) do nothing;

                insert into projects (id, project_key, project_number, name)
                values (
                    '70000000-0000-0000-0000-000000000003',
                    'existing-project',
                    'EXISTING-001',
                    'Existing Project'
                )
                on conflict (project_key) do nothing;

                insert into user_project_access (user_id, project_id)
                values (
                    '70000000-0000-0000-0000-000000000002',
                    '70000000-0000-0000-0000-000000000003'
                )
                on conflict do nothing;
                """);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);

        await AssertPermissionScopeAlignmentAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertExistingRowsPreservedAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertNoDuplicateRolePermissionsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PermissionScopeAlignmentMigration_RemovesSingleSensitivePermissionFromDisallowedRoles()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into roles (id, code, name)
            values ('70000000-0000-0000-0000-000000000004', 'production-management', 'Production Management')
            on conflict (code) do nothing;
            """,
            TestContext.Current.CancellationToken);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await AssertPermissionScopeAlignmentAsync(connectionStringProvider, TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into role_permissions (role_id, permission_id)
            select roles.id, permissions.id
            from roles
            join permissions on permissions.code = 'Project.SalesAmount.Read'
            where roles.code = 'manufacturing'
            on conflict do nothing;

            insert into role_permissions (role_id, permission_id)
            select roles.id, permissions.id
            from roles
            join permissions on permissions.code = 'Manufacturing.WorkTime.Read'
            where roles.code = 'production-management'
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);

        var contaminated = await ReadDisallowedSensitivePermissionAssignmentsAsync(
            connectionStringProvider,
            TestContext.Current.CancellationToken);
        Assert.Contains(
            new SensitivePermissionAssignment("manufacturing", "Project.SalesAmount.Read"),
            contaminated);
        Assert.Contains(
            new SensitivePermissionAssignment("production-management", "Manufacturing.WorkTime.Read"),
            contaminated);

        var exception = await Record.ExceptionAsync(() =>
            AssertNoDisallowedSensitivePermissionsAsync(
                connectionStringProvider,
                TestContext.Current.CancellationToken));
        Assert.NotNull(exception);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);

        await AssertPermissionScopeAlignmentAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertNoDuplicateRolePermissionsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProjectPanelFoundationMigration_AllowsDuplicateProjectCodeButRejectsNormalizedTitleDuplicates()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);

        await AssertProjectPanelFoundationAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertNoDuplicateRolePermissionsAsync(connectionStringProvider, TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into departments (id, code, name)
            values ('71000000-0000-0000-0000-000000000001', 'sales-test', 'Sales Test')
            on conflict (code) do nothing;

            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values (
                '71000000-0000-0000-0000-000000000002',
                'migration-sales',
                'Migration Sales',
                '71000000-0000-0000-0000-000000000001',
                true
            )
            on conflict (development_user_key) do nothing;

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
                sales_owner_user_id
            )
            values
                (
                    '71000000-0000-0000-0000-000000000003',
                    'migration-project-a',
                    'DUP-CODE',
                    'Migration Project A',
                    'Migration Customer',
                    'Migration Item',
                    'DUP-CODE',
                    'Migration Project A',
                    'MIGRATION PROJECT A',
                    '2026-09-01',
                    '71000000-0000-0000-0000-000000000002'
                ),
                (
                    '71000000-0000-0000-0000-000000000004',
                    'migration-project-b',
                    'DUP-CODE',
                    'Migration Project B',
                    'Migration Customer',
                    'Migration Item',
                    'DUP-CODE',
                    'Migration Project B',
                    'MIGRATION PROJECT B',
                    '2026-09-02',
                    '71000000-0000-0000-0000-000000000002'
                );
            """,
            TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
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
                sales_owner_user_id
            )
            values (
                '71000000-0000-0000-0000-000000000005',
                'migration-project-c',
                'OTHER-CODE',
                ' migration   project   a ',
                'Migration Customer',
                'Migration Item',
                'OTHER-CODE',
                'migration project a',
                'MIGRATION PROJECT A',
                '2026-09-03',
                '71000000-0000-0000-0000-000000000002'
            );
            """,
            TestContext.Current.CancellationToken));

        Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception!).SqlState);
    }

    [Fact]
    public async Task ProjectPanelFoundationMigration_FailsClearlyForLegacyNormalizedTitleDuplicatesBeforeSchemaChanges()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into projects (id, project_key, project_number, name)
            values
                ('72000000-0000-0000-0000-000000000001', 'legacy-duplicate-a', 'LEGACY-A', 'Legacy Project'),
                ('72000000-0000-0000-0000-000000000002', 'legacy-duplicate-b', 'LEGACY-B', ' legacy   project ');
            """,
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken));

        Assert.Contains(
            "Project Title normalized duplicates were found. Resolve duplicate legacy titles before applying migration 0003.",
            exception.MessageText,
            StringComparison.Ordinal);
        Assert.Equal(2L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from projects where project_key like 'legacy-duplicate-%';",
            TestContext.Current.CancellationToken));
        Assert.False(await ReadScalarAsync<bool>(
            connectionStringProvider,
            """
            select exists (
                select 1
                from information_schema.columns
                where table_name = 'projects'
                  and column_name = 'customer_name'
            );
            """,
            TestContext.Current.CancellationToken));

        await ExecuteSqlAsync(
            connectionStringProvider,
            "update projects set name = 'Legacy Project B' where project_key = 'legacy-duplicate-b';",
            TestContext.Current.CancellationToken);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);

        await AssertProjectPanelFoundationAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProjectPackagingSoftDeleteMigration_AddsNullablePackagingAndPartialTitleUniqueness()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into departments (id, code, name)
            values ('74000000-0000-0000-0000-000000000001', 'sales-test-0004', 'Sales Test 0004')
            on conflict (code) do nothing;

            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values (
                '74000000-0000-0000-0000-000000000002',
                'migration-sales-0004',
                'Migration Sales 0004',
                '74000000-0000-0000-0000-000000000001',
                true
            )
            on conflict (development_user_key) do nothing;

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
                sales_owner_user_id
            )
            values (
                '74000000-0000-0000-0000-000000000003',
                'migration-0004-existing',
                'MIG-0004',
                'Migration 0004 Existing',
                'Migration Customer',
                'Migration Item',
                'MIG-0004',
                'Migration 0004 Existing',
                'MIGRATION 0004 EXISTING',
                '2026-09-01',
                '74000000-0000-0000-0000-000000000002'
            );
            """,
            TestContext.Current.CancellationToken);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0004_project_packaging_soft_delete.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0004_project_packaging_soft_delete.sql"),
            TestContext.Current.CancellationToken);

        await AssertProjectPackagingSoftDeleteAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        await AssertNoDuplicateRolePermissionsAsync(connectionStringProvider, TestContext.Current.CancellationToken);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from projects where project_key = 'migration-0004-existing' and packaging_method is null and deleted_at_utc is null;",
            TestContext.Current.CancellationToken));

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            update projects
            set deleted_at_utc = now(),
                delete_reason = 'migration test'
            where project_key = 'migration-0004-existing';

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
                packaging_method,
                delivery_date,
                sales_owner_user_id
            )
            values (
                '74000000-0000-0000-0000-000000000004',
                'migration-0004-reuse',
                'MIG-0004-REUSE',
                ' migration   0004   existing ',
                'Migration Customer',
                'Migration Item',
                'MIG-0004-REUSE',
                'Migration 0004 Existing',
                'MIGRATION 0004 EXISTING',
                'WoodenCrate',
                '2026-09-02',
                '74000000-0000-0000-0000-000000000002'
            );
            """,
            TestContext.Current.CancellationToken);

        var duplicateActive = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
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
                packaging_method,
                delivery_date,
                sales_owner_user_id
            )
            values (
                '74000000-0000-0000-0000-000000000005',
                'migration-0004-duplicate-active',
                'MIG-0004-DUP',
                'Migration 0004 Existing',
                'Migration Customer',
                'Migration Item',
                'MIG-0004-DUP',
                'Migration 0004 Existing',
                'MIGRATION 0004 EXISTING',
                'StretchWrap',
                '2026-09-03',
                '74000000-0000-0000-0000-000000000002'
            );
            """,
            TestContext.Current.CancellationToken));

        Assert.IsType<PostgresException>(duplicateActive);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicateActive!).SqlState);
    }

    [Fact]
    public async Task PanelInformationExcelImportMigration_AddsDesignPermissionAndPreservesExistingPanels()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0004_project_packaging_soft_delete.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into departments (id, code, name)
            values ('75000000-0000-0000-0000-000000000001', 'sales-test-0005', 'Sales Test 0005')
            on conflict (code) do nothing;

            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values (
                '75000000-0000-0000-0000-000000000002',
                'migration-sales-0005',
                'Migration Sales 0005',
                '75000000-0000-0000-0000-000000000001',
                true
            )
            on conflict (development_user_key) do nothing;

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
                packaging_method,
                delivery_date,
                sales_owner_user_id
            )
            values (
                '75000000-0000-0000-0000-000000000003',
                'migration-0005-existing',
                'MIG-0005',
                'Migration 0005 Existing',
                'Migration Customer',
                'Migration Item',
                'MIG-0005',
                'Migration 0005 Existing',
                'MIGRATION 0005 EXISTING',
                'StretchWrap',
                '2026-09-01',
                '75000000-0000-0000-0000-000000000002'
            );

            insert into panel_placeholders (
                id,
                project_id,
                sequence_number,
                display_code,
                panel_name,
                status,
                panel_info_completed,
                qr_eligible
            )
            values (
                '75000000-0000-0000-0000-000000000004',
                '75000000-0000-0000-0000-000000000003',
                1,
                'P01',
                'Existing Panel',
                'Active',
                false,
                false
            );
            """,
            TestContext.Current.CancellationToken);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0005_panel_information_excel_import.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0005_panel_information_excel_import.sql"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from panel_placeholders
            where id = '75000000-0000-0000-0000-000000000004'
              and panel_name = 'Existing Panel'
              and panel_info_version = 0
              and panel_info_completed = true
              and qr_eligible = true;
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'PanelInfo.Update'
              and roles.code in ('design', 'sales', 'production-planning');
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(0L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'PanelInfo.Update'
              and roles.code not in ('design', 'sales', 'production-planning');
            """,
            TestContext.Current.CancellationToken));

        Assert.True(await ReadScalarAsync<bool>(
            connectionStringProvider,
            """
            select exists (
                select 1
                from information_schema.tables
                where table_name = 'panel_information_excel_import_batches'
            );
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_name = 'project_audit_events'
              and column_name in ('input_source', 'import_batch_id', 'input_unit', 'original_input_value');
            """,
            TestContext.Current.CancellationToken));

        await AssertNoDuplicateRolePermissionsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PanelWorkflowStageMigration_AddsDefaultConstraintAndIndexAfterExisting0006()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0001_identity_authorization_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0002_permission_scope_alignment.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0003_project_panel_foundation.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0004_project_packaging_soft_delete.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0005_panel_information_excel_import.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0006_admin_audit_access.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteSqlAsync(
            connectionStringProvider,
            """
            insert into departments (id, code, name)
            values ('76000000-0000-0000-0000-000000000001', 'sales-test-0007', 'Sales Test 0007')
            on conflict (code) do nothing;

            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values (
                '76000000-0000-0000-0000-000000000002',
                'migration-sales-0007',
                'Migration Sales 0007',
                '76000000-0000-0000-0000-000000000001',
                true
            )
            on conflict (development_user_key) do nothing;

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
                packaging_method,
                delivery_date,
                sales_owner_user_id
            )
            values (
                '76000000-0000-0000-0000-000000000003',
                'migration-0007-existing',
                'MIG-0007',
                'Migration 0007 Existing',
                'Migration Customer',
                'Migration Item',
                'MIG-0007',
                'Migration 0007 Existing',
                'MIGRATION 0007 EXISTING',
                'WoodenCrate',
                '2026-09-01',
                '76000000-0000-0000-0000-000000000002'
            );

            insert into panel_placeholders (
                id,
                project_id,
                sequence_number,
                display_code,
                panel_name,
                status
            )
            values (
                '76000000-0000-0000-0000-000000000004',
                '76000000-0000-0000-0000-000000000003',
                1,
                'P01',
                'Workflow Existing',
                'Cancelled'
            );

            insert into project_audit_events (
                project_id,
                entity_type,
                entity_id,
                action,
                field_name,
                old_value,
                new_value,
                changed_by_user_id,
                correlation_id
            )
            values (
                '76000000-0000-0000-0000-000000000003',
                'Panel',
                '76000000-0000-0000-0000-000000000004',
                'PanelInfoUpdated',
                'PanelName',
                null,
                'Workflow Existing',
                '76000000-0000-0000-0000-000000000002',
                'migration-0007-audit'
            );
            """,
            TestContext.Current.CancellationToken);

        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0007_panel_workflow_stage.sql"),
            TestContext.Current.CancellationToken);
        await ApplyMigrationFileAsync(
            connectionStringProvider,
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0007_panel_workflow_stage.sql"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from panel_placeholders
            where id = '76000000-0000-0000-0000-000000000004'
              and panel_name = 'Workflow Existing'
              and status = 'Cancelled'
              and workflow_stage = 'BeforeManufacturing';
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from project_audit_events
            where project_id = '76000000-0000-0000-0000-000000000003';
            """,
            TestContext.Current.CancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where indexname = 'ix_panel_placeholders_project_workflow_stage';
            """,
            TestContext.Current.CancellationToken));

        var exception = await Record.ExceptionAsync(() => ExecuteSqlAsync(
            connectionStringProvider,
            """
            update panel_placeholders
            set workflow_stage = 'InvalidStage'
            where id = '76000000-0000-0000-0000-000000000004';
            """,
            TestContext.Current.CancellationToken));

        Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.CheckViolation, ((PostgresException)exception!).SqlState);
    }

    [Theory]
    [InlineData("Development", "DevelopmentData:SeedEnabled")]
    [InlineData("Testing", "DevelopmentData:SeedEnabled")]
    [InlineData("Testing", "DEV_DATA_SEED_ENABLED")]
    public async Task DevelopmentDataSeeder_CreatesFakeDataOnlyWhenExplicitlyEnabled_AndIsIdempotent(
        string environment,
        string enabledSettingKey)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { [enabledSettingKey] = "true" });
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var seeder = CreateSeeder(database.RepositoryRoot, environment, configuration, connectionStringProvider);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(12, counts.Users);
        Assert.Equal(10, counts.Departments);
        Assert.Equal(2, counts.Projects);
        Assert.Equal(9, counts.ProjectAccess);
        Assert.Equal(1, counts.DisabledUsers);
    }

    [Fact]
    public async Task DevelopmentDataSeeder_DoesNotSeedWhenSettingIsMissing()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);

        await CreateMigrationRunner(database.RepositoryRoot, connectionStringProvider)
            .ApplyAsync(TestContext.Current.CancellationToken);

        var seeder = CreateSeeder(database.RepositoryRoot, "Development", configuration, connectionStringProvider);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var counts = await ReadCountsAsync(connectionStringProvider, TestContext.Current.CancellationToken);
        Assert.Equal(0, counts.Users);
        Assert.Equal(0, counts.Projects);
        Assert.Equal(0, counts.ProjectAccess);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public async Task DevelopmentDataSeeder_FailsWhenExplicitlyEnabledOutsideAllowedEnvironments(string environment)
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration(
            new Dictionary<string, string?> { ["DevelopmentData:SeedEnabled"] = "true" });
        var connectionStringProvider = new DatabaseConnectionStringProvider(configuration);
        var seeder = CreateSeeder(database.RepositoryRoot, environment, configuration, connectionStringProvider);

        var exception = Assert.Throws<InvalidOperationException>(() => seeder.IsEnabled());
        Assert.Contains("development data seeding cannot be enabled", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static DatabaseMigrationRunner CreateMigrationRunner(
        string repositoryRoot,
        DatabaseConnectionStringProvider connectionStringProvider)
    {
        var environment = new TestWebHostEnvironment(repositoryRoot);
        return new DatabaseMigrationRunner(
            connectionStringProvider,
            new Emi.Qms.Api.ReviewSafe.DatabaseMigrationCatalog(environment),
            new ConfigurationBuilder().Build(),
            NullLogger<DatabaseMigrationRunner>.Instance);
    }

    private static async Task<string?> ReadConnectionScalarAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }

    private static async Task AssertReadOnlyFailureAsync(
        NpgsqlConnection connection,
        string sql,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ReadOnlySqlTransaction, exception.SqlState);
    }

    private static DevelopmentIdentitySeeder CreateSeeder(
        string repositoryRoot,
        string environment,
        IConfiguration configuration,
        DatabaseConnectionStringProvider connectionStringProvider)
    {
        return new DevelopmentIdentitySeeder(
            connectionStringProvider,
            configuration,
            new TestWebHostEnvironment(repositoryRoot) { EnvironmentName = environment },
            NullLogger<DevelopmentIdentitySeeder>.Instance);
    }

    private static async Task<DatabaseCounts> ReadCountsAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select
                (select count(*) from qms_users) as user_count,
                (select count(*) from departments) as department_count,
                (select count(*) from projects) as project_count,
                (select count(*) from user_project_access) as project_access_count,
                (select count(*) from roles) as role_count,
                (select count(*) from permissions) as permission_count,
                (select count(*) from role_permissions) as role_permission_count,
                (select count(*) from qms_users where development_user_key = 'dev-disabled' and is_active = false) as disabled_user_count;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));

        return new DatabaseCounts(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7));
    }

    private static async Task AssertCoreConstraintsExistAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select count(*)
            from pg_constraint
            where conname in (
                'qms_users_development_user_key_key',
                'roles_code_key',
                'permissions_code_key',
                'user_project_access_pkey',
                'user_project_access_user_id_fkey',
                'user_project_access_project_id_fkey'
            );
            """);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        Assert.Equal(6L, value);
    }

    private static async Task AssertSystemHolidaySchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.True(await ReadScalarAsync<bool>(
            connectionStringProvider,
            """
            select exists (
                select 1
                from information_schema.tables
                where table_schema = 'public'
                  and table_name = 'system_holidays'
            );
            """,
            cancellationToken));

        Assert.True(await ReadScalarAsync<bool>(
            connectionStringProvider,
            """
            select exists (
                select 1
                from information_schema.columns
                where table_schema = 'public'
                  and table_name = 'system_holidays'
                  and column_name = 'holiday_type'
                  and is_nullable = 'NO'
            );
            """,
            cancellationToken));

        Assert.True(await ReadScalarAsync<bool>(
            connectionStringProvider,
            """
            select exists (
                select 1
                from information_schema.columns
                where table_schema = 'public'
                  and table_name = 'system_holidays'
                  and column_name = 'note'
                  and is_nullable = 'YES'
            );
            """,
            cancellationToken));

        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where tablename = 'system_holidays'
              and indexname in (
                  'ux_system_holidays_country_date_source_key',
                  'ix_system_holidays_active_lookup',
                  'ix_system_holidays_active_type_lookup',
                  'ix_system_holidays_year_type_lookup'
              );
            """,
            cancellationToken));
    }

    private static async Task AssertWorkflowSchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.Equal(18L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from workflow_stages where is_active = true;",
            cancellationToken));

        Assert.Equal("ProductionPlanning", await ReadScalarAsync<string>(
            connectionStringProvider,
            "select stage_code from workflow_stages where sequence_number = 2;",
            cancellationToken));

        Assert.Equal("DesignPanelInfo", await ReadScalarAsync<string>(
            connectionStringProvider,
            "select stage_code from workflow_stages where sequence_number = 3;",
            cancellationToken));

        Assert.Equal(7L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'workflow_stages',
                  'project_workflow_events',
                  'work_items',
                  'notifications',
                  'notification_recipients',
                  'notification_deliveries',
                  'work_item_escalations'
              );
            """,
            cancellationToken));

        var deliveryConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notification_deliveries_channel';
            """,
            cancellationToken);
        Assert.Contains("TeamsChannel", deliveryConstraint, StringComparison.Ordinal);
        Assert.Contains("TeamsActivity", deliveryConstraint, StringComparison.Ordinal);
        Assert.Contains("Mail", deliveryConstraint, StringComparison.Ordinal);

        var deliveryTypeConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notification_deliveries_delivery_type';
            """,
            cancellationToken);
        Assert.Contains("DueSoonL0", deliveryTypeConstraint, StringComparison.Ordinal);
        Assert.Contains("OverdueL3", deliveryTypeConstraint, StringComparison.Ordinal);

        var escalationStatusConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_work_item_escalations_status';
            """,
            cancellationToken);
        Assert.Contains("Active", escalationStatusConstraint, StringComparison.Ordinal);
        Assert.Contains("Resolved", escalationStatusConstraint, StringComparison.Ordinal);

        var escalationLevelConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_work_item_escalations_current_level';
            """,
            cancellationToken);
        Assert.Contains("L0", escalationLevelConstraint, StringComparison.Ordinal);
        Assert.Contains("L3", escalationLevelConstraint, StringComparison.Ordinal);

        var assigneeConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_project_assignees_responsibility_type';
            """,
            cancellationToken);
        Assert.Contains("DesignPrimary", assigneeConstraint, StringComparison.Ordinal);
        Assert.Contains("QualityCustomerInspection", assigneeConstraint, StringComparison.Ordinal);
    }

    private static async Task AssertProcurementRequiredItemSchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.Equal(2L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in (
                  'procurement_required_item_templates',
                  'procurement_required_item_template_rows'
              );
            """,
            cancellationToken));

        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where tablename in ('procurement_required_item_templates', 'procurement_required_item_template_rows')
              and indexname in (
                  'ux_procurement_required_item_templates_active_item',
                  'ux_procurement_required_item_template_rows_sequence',
                  'ux_procurement_required_item_template_rows_active_name'
              );
            """,
            cancellationToken));
    }

    private static async Task AssertWorkflowAlignmentSchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.Equal("boolean", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select data_type
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'projects'
              and column_name = 'fat_required';
            """,
            cancellationToken));

        Assert.Equal("NO", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select is_nullable
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'projects'
              and column_name = 'fat_required';
            """,
            cancellationToken));

        Assert.Equal("text", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select data_type
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'project_procurement_items'
              and column_name = 'supplier_name';
            """,
            cancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_constraint
            where conname = 'ck_project_procurement_items_supplier_name_not_blank';
            """,
            cancellationToken));
    }

    private static async Task AssertMicrosoft365IdentitySchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'qms_users'
              and column_name in ('entra_object_id', 'email', 'auth_provider');
            """,
            cancellationToken));

        Assert.Equal("YES", await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select is_nullable
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'qms_users'
              and column_name = 'department_id';
            """,
            cancellationToken));

        var authProviderConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_qms_users_auth_provider';
            """,
            cancellationToken);
        Assert.Contains("Dev", authProviderConstraint, StringComparison.Ordinal);
        Assert.Contains("EntraId", authProviderConstraint, StringComparison.Ordinal);

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where tablename = 'qms_users'
              and indexname = 'ux_qms_users_entra_object_id'
              and indexdef ilike '%unique%';
            """,
            cancellationToken));

        Assert.Equal(0L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from pg_indexes
            where tablename = 'qms_users'
              and indexname = 'ix_qms_users_email'
              and indexdef ilike '%unique%';
            """,
            cancellationToken));
    }

    private static async Task AssertAdminMasterDataSchemaAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        Assert.Equal(8L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'departments'
              and column_name in (
                  'is_active',
                  'sort_order',
                  'updated_at_utc',
                  'deletion_requested_at_utc',
                  'scheduled_hard_delete_at_utc',
                  'purge_blocked_at_utc',
                  'purge_blocked_reason',
                  'pre_delete_is_active'
              );
            """,
            cancellationToken));

        Assert.Equal(5L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'qms_users'
              and column_name in (
                  'deletion_requested_at_utc',
                  'scheduled_hard_delete_at_utc',
                  'purge_blocked_at_utc',
                  'purge_blocked_reason',
                  'pre_delete_is_active'
              );
            """,
            cancellationToken));

        Assert.Equal(5L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'system_holidays'
              and column_name in (
                  'deletion_requested_at_utc',
                  'scheduled_hard_delete_at_utc',
                  'purge_blocked_at_utc',
                  'purge_blocked_reason',
                  'pre_delete_is_active'
              );
            """,
            cancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name = 'admin_master_change_logs';
            """,
            cancellationToken));

        Assert.Equal(0L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from production_product_types
            where code = 'RRP';
            """,
            cancellationToken));

        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from schema_migrations
            where version = '0027_notification_access_scope_and_manual_work_items';
            """,
            cancellationToken));

        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'notification_deliveries'
              and column_name in (
                  'admin_handling_status',
                  'admin_handled_at_utc',
                  'admin_handled_by_user_id',
                  'admin_handling_note'
              );
            """,
            cancellationToken));

        var handlingConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notification_deliveries_admin_handling_status';
            """,
            cancellationToken);
        Assert.Contains("Acknowledged", handlingConstraint, StringComparison.Ordinal);
        Assert.Contains("Dismissed", handlingConstraint, StringComparison.Ordinal);

        Assert.Equal(10L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'notification_deliveries'
              and column_name in (
                  'display_title',
                  'display_message',
                  'display_project_name',
                  'display_work_item_title',
                  'display_recipient_name',
                  'display_recipient_email',
                  'display_recipient_kind',
                  'display_channel_target',
                  'manual_notification_kind',
                  'correlation_id'
              );
            """,
            cancellationToken));

        var manualKindConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notification_deliveries_manual_notification_kind';
            """,
            cancellationToken);
        Assert.Contains("ProjectCreated", manualKindConstraint, StringComparison.Ordinal);
        Assert.Contains("Custom", manualKindConstraint, StringComparison.Ordinal);

        Assert.Equal(3L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'notification_deliveries'
              and column_name in (
                  'manual_payload_json',
                  'manual_requested_by_user_id',
                  'manual_requested_at_utc'
              );
            """,
            cancellationToken));

        Assert.Equal(4L, await ReadScalarAsync<long>(
            connectionStringProvider,
            """
            select count(*)
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'notifications'
              and column_name in (
                  'visibility_scope',
                  'source_kind',
                  'work_item_id',
                  'manual_requested_by_user_id'
              );
            """,
            cancellationToken));

        var visibilityConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notifications_visibility_scope';
            """,
            cancellationToken);
        Assert.Contains("Authenticated", visibilityConstraint, StringComparison.Ordinal);

        var sourceKindConstraint = await ReadScalarAsync<string>(
            connectionStringProvider,
            """
            select pg_get_constraintdef(oid)
            from pg_constraint
            where conname = 'ck_notifications_source_kind';
            """,
            cancellationToken);
        Assert.Contains("WorkAssignment", sourceKindConstraint, StringComparison.Ordinal);
    }

    private static async Task ApplyMigrationFileAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        string migrationFile,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(await File.ReadAllTextAsync(migrationFile, cancellationToken));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static byte[] CreateStructurallyValidPng(byte width, byte height)
    {
        var content = new byte[45];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(content, 0);
        content[11] = 13;
        "IHDR"u8.CopyTo(content.AsSpan(12, 4));
        content[19] = width;
        content[23] = height;
        "IEND"u8.CopyTo(content.AsSpan(content.Length - 8, 4));
        return content;
    }

    private static async Task ExecuteSqlAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        string commandText,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(commandText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ReadScalarAsync<T>(
        DatabaseConnectionStringProvider connectionStringProvider,
        string commandText,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(commandText);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<T>(value);
    }

    private static async Task AssertPermissionScopeAlignmentAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var command = dataSource.CreateCommand("""
            select roles.code
            from roles
            where roles.code in (
                'system-administrator',
                'sales',
                'production-planning',
                'manufacturing',
                'quality',
                'logistics',
                'read-only'
            )
            except
            select roles.code
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Project.Read.All'
            order by code;
            """))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            Assert.False(await reader.ReadAsync(cancellationToken));
        }

        await using (var command = dataSource.CreateCommand("""
            select roles.code
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code in ('Project.SalesAmount.Read', 'Manufacturing.WorkTime.Read')
            group by roles.code
            having count(distinct permissions.code) = 2
            order by roles.code;
            """))
        {
            var allowedRoles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                allowedRoles.Add(reader.GetString(0));
            }

            Assert.Equal(["sales", "system-administrator"], allowedRoles);
        }

        await AssertNoDisallowedSensitivePermissionsAsync(connectionStringProvider, cancellationToken);

        await using (var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from permissions
                where code = 'Audit.Read.All'
            );
            """))
        {
            var auditReadAllExists = Assert.IsType<bool>(await command.ExecuteScalarAsync(cancellationToken));
            if (auditReadAllExists)
            {
                await using var rolesCommand = dataSource.CreateCommand("""
                    select roles.code
                    from roles
                    join role_permissions on role_permissions.role_id = roles.id
                    join permissions on permissions.id = role_permissions.permission_id
                    where permissions.code = 'Audit.Read.All'
                    order by roles.code;
                    """);
                var roles = new List<string>();
                await using var reader = await rolesCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    roles.Add(reader.GetString(0));
                }

                Assert.Equal(["system-administrator"], roles);
            }
        }

        await using (var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from permissions
                where code = 'ProductionPlan.Update'
            );
            """))
        {
            var productionPlanUpdateExists = Assert.IsType<bool>(await command.ExecuteScalarAsync(cancellationToken));
            if (productionPlanUpdateExists)
            {
                await using var rolesCommand = dataSource.CreateCommand("""
                    select roles.code
                    from roles
                    join role_permissions on role_permissions.role_id = roles.id
                    join permissions on permissions.id = role_permissions.permission_id
                    where permissions.code = 'ProductionPlan.Update'
                    order by roles.code;
                    """);
                var roles = new List<string>();
                await using var reader = await rolesCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    roles.Add(reader.GetString(0));
                }

                Assert.Equal(["production-planning"], roles);
            }
        }

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from qms_users
            where development_user_key like 'dev-%';
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(0L, value);
        }

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from permissions
            where permissions.code = 'admin-history.read';
            """))
        {
            var permissionCount = Assert.IsType<long>(await command.ExecuteScalarAsync(cancellationToken));
            if (permissionCount == 1)
            {
                await using var rolesCommand = dataSource.CreateCommand("""
                    select roles.code
                    from roles
                    join role_permissions on role_permissions.role_id = roles.id
                    join permissions on permissions.id = role_permissions.permission_id
                    where permissions.code = 'admin-history.read'
                    order by roles.code;
                    """);
                var roles = new List<string>();
                await using var reader = await rolesCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    roles.Add(reader.GetString(0));
                }

                Assert.Equal(["system-administrator"], roles);
            }
        }

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from permissions
            where permissions.code = 'master-data.manage';
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(0L, value);
        }

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from projects
            where project_key like 'demo-%';
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(0L, value);
        }
    }

    private static async Task AssertNoDisallowedSensitivePermissionsAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var disallowed = await ReadDisallowedSensitivePermissionAssignmentsAsync(
            connectionStringProvider,
            cancellationToken);

        Assert.Empty(disallowed);
    }

    private static async Task AssertProjectPanelFoundationAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from permissions
            where code in ('Project.Create', 'Project.Update', 'Project.Hold', 'Project.Cancel');
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(4L, value);
        }

        await using (var command = dataSource.CreateCommand("""
            select roles.code
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code in ('Project.Create', 'Project.Update', 'Project.Hold', 'Project.Cancel')
            group by roles.code
            having count(distinct permissions.code) = 4
            order by roles.code;
            """))
        {
            var roles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(reader.GetString(0));
            }

            Assert.Equal(["sales"], roles);
        }

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from pg_indexes
            where indexname in (
                'ux_projects_project_title_normalized',
                'ux_panel_placeholders_project_sequence',
                'ux_panel_placeholders_project_display_code'
            );
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(3L, value);
        }

        await using (var command = dataSource.CreateCommand("""
            select exists (
                select 1
                from pg_constraint
                where conname = 'projects_project_number_key'
            );
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(false, value);
        }
    }

    private static async Task AssertProjectPackagingSoftDeleteAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (var command = dataSource.CreateCommand("""
            select count(*)
            from information_schema.columns
            where table_name = 'projects'
              and column_name in (
                  'packaging_method',
                  'deleted_at_utc',
                  'deleted_by_user_id',
                  'delete_reason',
                  'deleted_correlation_id'
              );
            """))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(5L, value);
        }

        await using (var command = dataSource.CreateCommand("""
            select indexdef
            from pg_indexes
            where indexname = 'ux_projects_project_title_normalized_active';
            """))
        {
            var value = Assert.IsType<string>(await command.ExecuteScalarAsync(cancellationToken));
            Assert.Contains("deleted_at_utc IS NULL", value, StringComparison.OrdinalIgnoreCase);
        }

        await using (var command = dataSource.CreateCommand("""
            select roles.code
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Project.Delete'
            order by roles.code;
            """))
        {
            var roles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(reader.GetString(0));
            }

            Assert.Equal(["sales"], roles);
        }

        await using (var command = dataSource.CreateCommand("""
            select roles.code
            from roles
            join role_permissions on role_permissions.role_id = roles.id
            join permissions on permissions.id = role_permissions.permission_id
            where permissions.code = 'Project.Deleted.Read'
            order by roles.code;
            """))
        {
            var roles = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(reader.GetString(0));
            }

            Assert.Equal(["sales", "system-administrator"], roles);
        }
    }

    private static async Task<IReadOnlyList<SensitivePermissionAssignment>> ReadDisallowedSensitivePermissionAssignmentsAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select roles.code, permissions.code
            from role_permissions
            join roles on roles.id = role_permissions.role_id
            join permissions on permissions.id = role_permissions.permission_id
            where roles.code not in ('system-administrator', 'sales')
              and permissions.code in (
                  'Project.SalesAmount.Read',
                  'Manufacturing.WorkTime.Read'
              )
            order by roles.code, permissions.code;
            """);

        var assignments = new List<SensitivePermissionAssignment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new SensitivePermissionAssignment(reader.GetString(0), reader.GetString(1)));
        }

        return assignments;
    }

    private static async Task AssertExistingRowsPreservedAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select
                (select count(*) from qms_users where development_user_key = 'existing-user') as user_count,
                (select count(*) from projects where project_key = 'existing-project') as project_count,
                (select count(*) from user_project_access) as project_access_count;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
    }

    private static async Task AssertNoDuplicateRolePermissionsAsync(
        DatabaseConnectionStringProvider connectionStringProvider,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            select count(*)
            from (
                select role_id, permission_id
                from role_permissions
                group by role_id, permission_id
                having count(*) > 1
            ) duplicated_role_permissions;
            """);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        Assert.Equal(0L, value);
    }

    private sealed record DatabaseCounts(
        long Users,
        long Departments,
        long Projects,
        long ProjectAccess,
        long Roles,
        long Permissions,
        long RolePermissions,
        long DisabledUsers);

    private sealed record SensitivePermissionAssignment(string RoleCode, string PermissionCode);

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string repositoryRoot, string databaseName, IConfiguration baseConfiguration)
        {
            RepositoryRoot = repositoryRoot;
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        public string RepositoryRoot { get; }
        public string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");

            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new PostgreSqlTestDatabase(repositoryRoot, databaseName, baseConfiguration);
        }

        public IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var values = BaseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            values["DATABASE_NAME"] = DatabaseName;

            if (overrides is not null)
            {
                foreach (var item in overrides)
                {
                    values[item.Key] = item.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)} with (force);");
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string value)
        {
            return new NpgsqlCommandBuilder().QuoteIdentifier(value);
        }

        private static string BuildConnectionString(IConfiguration configuration, string databaseName)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var configured = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(configured));

            var builder = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = databaseName,
                Pooling = false
            };

            return builder.ConnectionString;
        }

        private static IConfiguration BuildBaseDatabaseConfiguration(string repositoryRoot)
        {
            var values = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));

            return TestConfigurationIsolation.BuildBaseDatabaseConfiguration(values);
        }

        private static Dictionary<string, string?> LoadDotEnv(string envPath)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(envPath))
            {
                return values;
            }

            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                values[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
            }

            return values;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = DevelopmentFeaturePolicy.TestingEnvironmentName;
        public string ApplicationName { get; set; } = "Emi.Qms.Api.Tests";
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
