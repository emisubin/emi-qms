using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class PostgreSqlMigrationTests
{
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
        Assert.Equal(1, counts.Departments);
        Assert.Equal(0, counts.Projects);
        Assert.Equal(0, counts.ProjectAccess);
        Assert.Equal(8, counts.Roles);
        Assert.Equal(20, counts.Permissions);
        Assert.True(counts.RolePermissions > 0);
        Assert.Equal(1L, await ReadScalarAsync<long>(
            connectionStringProvider,
            "select count(*) from departments where code = 'design';",
            TestContext.Current.CancellationToken));

        await AssertCoreConstraintsExistAsync(connectionStringProvider, TestContext.Current.CancellationToken);
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
        Assert.Equal(10, counts.Users);
        Assert.Equal(8, counts.Departments);
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
        return new DatabaseMigrationRunner(
            connectionStringProvider,
            new TestWebHostEnvironment(repositoryRoot),
            NullLogger<DatabaseMigrationRunner>.Instance);
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
