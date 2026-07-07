using Emi.Qms.Api.Authorization;
using Npgsql;

namespace Emi.Qms.Api.Identity;

public sealed class DevelopmentIdentitySeeder(
    DatabaseConnectionStringProvider connectionStringProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentIdentitySeeder> logger)
{
    public bool IsEnabled()
    {
        var decision = DevelopmentFeaturePolicy.EvaluateDevelopmentDataSeeding(environment, configuration);
        DevelopmentFeaturePolicy.ThrowIfInvalidActivation(decision, environment);
        return decision.IsEnabled;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var decision = DevelopmentFeaturePolicy.EvaluateDevelopmentDataSeeding(environment, configuration);
        DevelopmentFeaturePolicy.ThrowIfInvalidActivation(decision, environment);

        if (!decision.IsEnabled)
        {
            logger.LogInformation("Development identity seed is disabled.");
            return;
        }

        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction, SeedSql, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Development identity seed completed.");
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string SeedSql = """
        insert into departments (id, code, name, is_active, sort_order)
        values
            ('10000000-0000-0000-0000-000000000001', 'administration', 'Administration', true, 10),
            ('10000000-0000-0000-0000-000000000002', 'sales', 'Sales', true, 20),
            ('10000000-0000-0000-0000-000000000003', 'production-planning', 'Production Planning', true, 40),
            ('10000000-0000-0000-0000-000000000004', 'manufacturing', 'Manufacturing', true, 70),
            ('10000000-0000-0000-0000-000000000005', 'quality', 'Quality', true, 80),
            ('10000000-0000-0000-0000-000000000006', 'logistics', 'Logistics', true, 90),
            ('10000000-0000-0000-0000-000000000007', 'readonly', 'Read Only', true, 100),
            ('10000000-0000-0000-0000-000000000008', 'design', 'Design', true, 30),
            ('10000000-0000-0000-0000-000000000009', 'procurement', 'Procurement', true, 50),
            ('10000000-0000-0000-0000-000000000010', 'materials', 'Materials', true, 60)
        on conflict (code) do update
        set name = excluded.name,
            is_active = true,
            sort_order = excluded.sort_order,
            updated_at_utc = now();

        insert into qms_users (id, development_user_key, display_name, department_id, is_active, auth_provider, entra_object_id, email)
        values
            ('50000000-0000-0000-0000-000000000001', 'dev-admin', 'Dev System Administrator', '10000000-0000-0000-0000-000000000001', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000002', 'dev-sales', 'Dev Sales User', '10000000-0000-0000-0000-000000000002', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000003', 'dev-production', 'Dev Production Planning User', '10000000-0000-0000-0000-000000000003', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000004', 'dev-manufacturing', 'Dev Manufacturing User', '10000000-0000-0000-0000-000000000004', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000005', 'dev-quality', 'Dev Quality User', '10000000-0000-0000-0000-000000000005', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000006', 'dev-logistics', 'Dev Logistics User', '10000000-0000-0000-0000-000000000006', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000007', 'dev-viewer', 'Dev Read Only User', '10000000-0000-0000-0000-000000000007', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000008', 'dev-no-role', 'Dev User Without Role', '10000000-0000-0000-0000-000000000007', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000009', 'dev-disabled', 'Dev Disabled User', '10000000-0000-0000-0000-000000000007', false, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000010', 'dev-design', 'Dev Design User', '10000000-0000-0000-0000-000000000008', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000011', 'dev-procurement', 'Dev Procurement User', '10000000-0000-0000-0000-000000000009', true, 'Dev', null, null),
            ('50000000-0000-0000-0000-000000000012', 'dev-materials', 'Dev Materials User', '10000000-0000-0000-0000-000000000010', true, 'Dev', null, null)
        on conflict (development_user_key) do update
        set display_name = excluded.display_name,
            department_id = excluded.department_id,
            is_active = excluded.is_active,
            auth_provider = 'Dev',
            entra_object_id = null,
            email = null;

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
            sales_owner_user_id,
            status
        )
        values
            (
                '40000000-0000-0000-0000-000000000001',
                'demo-project-alpha',
                'DEMO-24001',
                'Demo Project Alpha',
                'Demo Customer Alpha',
                'Demo Item Alpha',
                'DEMO-24001',
                'Demo Project Alpha',
                'DEMO PROJECT ALPHA',
                'WoodenCrate',
                '2026-07-31',
                '50000000-0000-0000-0000-000000000002',
                'Active'
            ),
            (
                '40000000-0000-0000-0000-000000000002',
                'demo-project-beta',
                'DEMO-24002',
                'Demo Project Beta',
                'Demo Customer Beta',
                'Demo Item Beta',
                'DEMO-24002',
                'Demo Project Beta',
                'DEMO PROJECT BETA',
                'StretchWrap',
                '2026-08-15',
                '50000000-0000-0000-0000-000000000002',
                'Active'
            )
        on conflict (project_key) do update
        set project_number = excluded.project_number,
            name = excluded.name,
            customer_name = excluded.customer_name,
            item = excluded.item,
            project_code = excluded.project_code,
            project_title = excluded.project_title,
            project_title_normalized = excluded.project_title_normalized,
            packaging_method = excluded.packaging_method,
            delivery_date = excluded.delivery_date,
            sales_owner_user_id = excluded.sales_owner_user_id,
            status = excluded.status;

        insert into user_roles (user_id, role_id)
        select qms_users.id, roles.id
        from qms_users
        join roles on roles.code = case qms_users.development_user_key
            when 'dev-admin' then 'system-administrator'
            when 'dev-sales' then 'sales'
            when 'dev-production' then 'production-planning'
            when 'dev-manufacturing' then 'manufacturing'
            when 'dev-quality' then 'quality'
            when 'dev-logistics' then 'logistics'
            when 'dev-viewer' then 'read-only'
            when 'dev-disabled' then 'read-only'
            when 'dev-design' then 'design'
            when 'dev-procurement' then 'procurement'
            when 'dev-materials' then 'materials'
            else null
        end
        where qms_users.development_user_key <> 'dev-no-role'
        on conflict do nothing;

        insert into user_project_access (user_id, project_id)
        select qms_users.id, projects.id
        from qms_users
        join projects on projects.project_key = any(
            case qms_users.development_user_key
                when 'dev-sales' then array['demo-project-alpha']
                when 'dev-production' then array['demo-project-alpha', 'demo-project-beta']
                when 'dev-manufacturing' then array['demo-project-alpha']
                when 'dev-quality' then array['demo-project-alpha', 'demo-project-beta']
                when 'dev-logistics' then array['demo-project-beta']
                when 'dev-viewer' then array['demo-project-alpha']
                when 'dev-no-role' then array['demo-project-alpha']
                else array[]::text[]
            end
        )
        on conflict do nothing;

        insert into permissions (id, code, name)
        values ('30000000-0000-0000-0000-000000000020', 'Audit.Read.All', 'Read all audit history')
        on conflict (code) do update set name = excluded.name;

        insert into role_permissions (role_id, permission_id)
        select roles.id, permissions.id
        from roles
        join permissions on permissions.code = 'Audit.Read.All'
        where roles.code = 'system-administrator'
        on conflict do nothing;

        delete from role_permissions
        using roles, permissions
        where role_permissions.role_id = roles.id
          and role_permissions.permission_id = permissions.id
          and permissions.code = 'Audit.Read.All'
          and roles.code <> 'system-administrator';

        insert into permissions (id, code, name)
        values
            ('30000000-0000-0000-0000-000000000021', 'ProcurementPlan.Update', 'Update procurement plan'),
            ('30000000-0000-0000-0000-000000000022', 'MaterialReceipt.Update', 'Update material receipt completion'),
            ('30000000-0000-0000-0000-000000000023', 'ProductionPlan.Update', 'Update production planning')
        on conflict (code) do update set name = excluded.name;

        insert into role_permissions (role_id, permission_id)
        select roles.id, permissions.id
        from roles
        join permissions on permissions.code in ('ProcurementPlan.Update', 'MaterialReceipt.Update')
        where roles.code = 'procurement'
        on conflict do nothing;

        insert into role_permissions (role_id, permission_id)
        select roles.id, permissions.id
        from roles
        join permissions on permissions.code = 'MaterialReceipt.Update'
        where roles.code = 'materials'
        on conflict do nothing;

        delete from role_permissions
        using roles, permissions
        where role_permissions.role_id = roles.id
          and role_permissions.permission_id = permissions.id
          and permissions.code = 'ProcurementPlan.Update'
          and roles.code <> 'procurement';

        delete from role_permissions
        using roles, permissions
        where role_permissions.role_id = roles.id
          and role_permissions.permission_id = permissions.id
          and permissions.code = 'MaterialReceipt.Update'
          and roles.code not in ('procurement', 'materials');

        insert into role_permissions (role_id, permission_id)
        select roles.id, permissions.id
        from roles
        join permissions on permissions.code = 'ProductionPlan.Update'
        where roles.code = 'production-planning'
        on conflict do nothing;

        delete from role_permissions
        using roles, permissions
        where role_permissions.role_id = roles.id
          and role_permissions.permission_id = permissions.id
          and permissions.code = 'ProductionPlan.Update'
          and roles.code <> 'production-planning';

        insert into permissions (id, code, name)
        values
            ('30000000-0000-0000-0000-000000000025', 'admin-history.read', 'Read administrator history')
        on conflict (code) do update set name = excluded.name;

        insert into role_permissions (role_id, permission_id)
        select roles.id, permissions.id
        from roles
        join permissions on permissions.code = 'admin-history.read'
        where roles.code = 'system-administrator'
        on conflict do nothing;

        delete from role_permissions
        using roles, permissions
        where role_permissions.role_id = roles.id
          and role_permissions.permission_id = permissions.id
          and permissions.code = 'admin-history.read'
          and roles.code <> 'system-administrator';

        insert into production_product_types (id, code, name)
        values ('60000000-0000-0000-0000-000000000001', 'TEST-TYPE', 'TEST-TYPE')
        on conflict (code) do update
        set name = excluded.name,
            is_active = true;

        insert into production_plan_templates (id, product_type_id, version, is_active)
        values ('60000000-0000-0000-0000-000000000101', '60000000-0000-0000-0000-000000000001', 1, true)
        on conflict (id) do update
        set is_active = true;

        insert into production_plan_template_steps (id, template_id, sequence_number, step_name, is_required, is_active)
        values
            ('60000000-0000-0000-0000-000000000201', '60000000-0000-0000-0000-000000000101', 1, 'INT 입고', true, true),
            ('60000000-0000-0000-0000-000000000202', '60000000-0000-0000-0000-000000000101', 2, 'INT 조립 시작', true, true),
            ('60000000-0000-0000-0000-000000000203', '60000000-0000-0000-0000-000000000101', 3, '배선 시작', true, true)
        on conflict (id) do update
        set sequence_number = excluded.sequence_number,
            step_name = excluded.step_name,
            is_required = excluded.is_required,
            is_active = true;
        """;
}
