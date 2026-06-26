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
        insert into departments (id, code, name)
        values
            ('10000000-0000-0000-0000-000000000001', 'administration', 'Administration'),
            ('10000000-0000-0000-0000-000000000002', 'sales', 'Sales'),
            ('10000000-0000-0000-0000-000000000003', 'production-planning', 'Production Planning'),
            ('10000000-0000-0000-0000-000000000004', 'manufacturing', 'Manufacturing'),
            ('10000000-0000-0000-0000-000000000005', 'quality', 'Quality'),
            ('10000000-0000-0000-0000-000000000006', 'logistics', 'Logistics'),
            ('10000000-0000-0000-0000-000000000007', 'readonly', 'Read Only'),
            ('10000000-0000-0000-0000-000000000008', 'design', 'Design')
        on conflict (code) do update set name = excluded.name;

        insert into qms_users (id, development_user_key, display_name, department_id, is_active)
        values
            ('50000000-0000-0000-0000-000000000001', 'dev-admin', 'Dev System Administrator', '10000000-0000-0000-0000-000000000001', true),
            ('50000000-0000-0000-0000-000000000002', 'dev-sales', 'Dev Sales User', '10000000-0000-0000-0000-000000000002', true),
            ('50000000-0000-0000-0000-000000000003', 'dev-production', 'Dev Production Planning User', '10000000-0000-0000-0000-000000000003', true),
            ('50000000-0000-0000-0000-000000000004', 'dev-manufacturing', 'Dev Manufacturing User', '10000000-0000-0000-0000-000000000004', true),
            ('50000000-0000-0000-0000-000000000005', 'dev-quality', 'Dev Quality User', '10000000-0000-0000-0000-000000000005', true),
            ('50000000-0000-0000-0000-000000000006', 'dev-logistics', 'Dev Logistics User', '10000000-0000-0000-0000-000000000006', true),
            ('50000000-0000-0000-0000-000000000007', 'dev-viewer', 'Dev Read Only User', '10000000-0000-0000-0000-000000000007', true),
            ('50000000-0000-0000-0000-000000000008', 'dev-no-role', 'Dev User Without Role', '10000000-0000-0000-0000-000000000007', true),
            ('50000000-0000-0000-0000-000000000009', 'dev-disabled', 'Dev Disabled User', '10000000-0000-0000-0000-000000000007', false),
            ('50000000-0000-0000-0000-000000000010', 'dev-design', 'Dev Design User', '10000000-0000-0000-0000-000000000008', true)
        on conflict (development_user_key) do update
        set display_name = excluded.display_name,
            department_id = excluded.department_id,
            is_active = excluded.is_active;

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
        """;
}
