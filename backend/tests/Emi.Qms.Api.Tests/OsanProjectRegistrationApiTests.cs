using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanProjectRegistrationApiTests
{
    private static readonly Guid UserId = Guid.Parse("89000000-0000-0000-0000-000000000001");

    [Fact]
    public void EndpointCatalog_ExposesOnlyDedicatedAuthorizedCreateListAndDetailRoutes()
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/osan/projects",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(3, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        var create = Assert.Single(endpoints, endpoint =>
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Post,
                StringComparer.OrdinalIgnoreCase) == true);
        Assert.Contains(
            create.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => string.Equals(authorization.Policy, QmsPolicies.ProjectCreate, StringComparison.Ordinal));
        Assert.Equal(
            ["/api/osan/projects/", "/api/osan/projects/{projectId:guid}"],
            endpoints
                .Where(endpoint => endpoint != create)
                .Select(endpoint => endpoint.RoutePattern.RawText!)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void InputNormalizer_TrimsOnlyOuterWhitespaceAndValidatesEveryBoundary()
    {
        var operationId = Guid.NewGuid();
        var (input, errors) = OsanProjectInputNormalizer.Normalize(new CreateOsanProjectRequest(
            "  Title  with  spaces  ",
            " AbC  001 ",
            " Customer ",
            " 001-PO/+ ",
            " 000-W/O ",
            new DateOnly(2026, 12, 31),
            " Product  name ",
            500,
            operationId));

        Assert.Empty(errors);
        Assert.NotNull(input);
        Assert.Equal("Title  with  spaces", input.Title);
        Assert.Equal("AbC  001", input.ProjectCode);
        Assert.Equal("001-PO/+", input.PoNumber);
        Assert.Equal("000-W/O", input.WorkOrderNumber);
        Assert.Equal("Product  name", input.ProductName);
        Assert.Equal(500, input.Quantity);
        Assert.Equal(operationId, input.OperationId);

        foreach (var quantity in new int?[] { null, 0, -1, 501 })
        {
            var (_, invalidErrors) = OsanProjectInputNormalizer.Normalize(ValidRequest(quantity: quantity));
            Assert.Contains(nameof(CreateOsanProjectRequest.Quantity), invalidErrors.Keys);
        }

        var (_, missingErrors) = OsanProjectInputNormalizer.Normalize(new CreateOsanProjectRequest(
            " ", " ", " ", " ", " ", null, " ", 1, Guid.Empty));
        Assert.Equal(6, missingErrors.Count);
        Assert.Contains(nameof(CreateOsanProjectRequest.Title), missingErrors.Keys);
        Assert.Contains(nameof(CreateOsanProjectRequest.ProjectCode), missingErrors.Keys);
        Assert.Contains(nameof(CreateOsanProjectRequest.CustomerName), missingErrors.Keys);
        Assert.Contains(nameof(CreateOsanProjectRequest.DeliveryDate), missingErrors.Keys);
        Assert.Contains(nameof(CreateOsanProjectRequest.ProductName), missingErrors.Keys);
        Assert.Contains(nameof(CreateOsanProjectRequest.OperationId), missingErrors.Keys);

        var (_, lengthErrors) = OsanProjectInputNormalizer.Normalize(new CreateOsanProjectRequest(
            new string('T', OsanProjectInputNormalizer.TitleMaxLength + 1),
            new string('C', OsanProjectInputNormalizer.ProjectCodeMaxLength + 1),
            new string('U', OsanProjectInputNormalizer.CustomerNameMaxLength + 1),
            new string('P', OsanProjectInputNormalizer.ReferenceNumberMaxLength + 1),
            new string('W', OsanProjectInputNormalizer.ReferenceNumberMaxLength + 1),
            new DateOnly(2026, 12, 31),
            new string('I', OsanProjectInputNormalizer.ProductNameMaxLength + 1),
            1,
            Guid.NewGuid()));
        Assert.Equal(6, lengthErrors.Count);
    }

    [Fact]
    public async Task Store_CreatesAtomicSnapshotSupportsReplayAndEnforcesCodeContract()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);
        await database.ExecuteAsync($"""
            insert into departments (id, code, name, is_active, sort_order)
            values ('89000000-0000-0000-0000-000000000010', 'osan-test', 'Osan Test', true, 1);
            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values ('{UserId:D}', 'osan-project-test', 'Osan Project Test',
                    '89000000-0000-0000-0000-000000000010', true);
            """, TestContext.Current.CancellationToken);

        var store = new OsanProjectStore(provider);
        var operationId = Guid.NewGuid();
        var input = Normalize(ValidRequest(
            title: " Shared title ",
            projectCode: " OSAN-001 ",
            poNumber: " 001-PO/+ ",
            workOrderNumber: " 000-W/O ",
            quantity: 3,
            operationId: operationId));

        var created = await store.CreateAsync(input, UserId, TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.Success, created.Status);
        Assert.NotNull(created.Value);
        Assert.False(created.Value.Replayed);
        Assert.Equal("001-PO/+", created.Value.Project.PoNumber);
        Assert.Equal("000-W/O", created.Value.Project.WorkOrderNumber);
        Assert.Equal(3, created.Value.Project.Targets.Count);
        Assert.All(created.Value.Project.Targets, target =>
        {
            Assert.Equal($"Product {target.SequenceNumber}", target.DisplayName);
            Assert.Equal("NotStarted", target.Status);
            Assert.Equal(
                ["입고검사", "배치검사", "배선검사", "8계통", "동작검사", "출하검사", "포장"],
                target.Steps.Select(step => step.StepName));
            Assert.All(target.Steps, step => Assert.Equal("NotStarted", step.Status));
        });

        var projectId = created.Value.Project.ProjectId;
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from user_project_access where user_id=@user_id and project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("user_id", UserId), ("project_id", projectId)));
        Assert.Equal(3L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_targets where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(21L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_target_steps where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_events where project_id=@project_id and event_type='ProjectCreated';",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            """
            select (select count(*) from panel_placeholders where project_id=@project_id)
                 + (select count(*) from project_production_plans where project_id=@project_id)
                 + (select count(*) from project_procurement_items where project_id=@project_id)
                 + (select count(*) from work_items where project_id=@project_id)
                 + (select count(*) from pending_issues where project_id=@project_id)
                 + (select count(*) from notification_deliveries where project_id=@project_id);
            """,
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

        var replay = await store.CreateAsync(input, UserId, TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.Success, replay.Status);
        Assert.True(replay.Value?.Replayed);
        Assert.Equal(projectId, replay.Value?.Project.ProjectId);
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='OSAN-001' and project_profile='Osan';",
            TestContext.Current.CancellationToken));

        var concurrentReplayInput = Normalize(ValidRequest(
            title: "Concurrent replay",
            projectCode: "CONCURRENT-REPLAY",
            operationId: Guid.NewGuid()));
        var concurrentReplays = await Task.WhenAll(
            store.CreateAsync(concurrentReplayInput, UserId, TestContext.Current.CancellationToken),
            store.CreateAsync(concurrentReplayInput, UserId, TestContext.Current.CancellationToken));
        Assert.All(concurrentReplays, result => Assert.Equal(OsanProjectCreateStatus.Success, result.Status));
        Assert.Equal(1, concurrentReplays.Count(result => result.Value?.Replayed == false));
        Assert.Equal(1, concurrentReplays.Count(result => result.Value?.Replayed == true));
        Assert.Single(concurrentReplays.Select(result => result.Value!.Project.ProjectId).Distinct());
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='CONCURRENT-REPLAY' and project_profile='Osan';",
            TestContext.Current.CancellationToken));

        var operationConflict = await store.CreateAsync(
            input with { Title = "Different title" },
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.OperationConflict, operationConflict.Status);

        await database.ExecuteAsync(
            "update projects set status='Completed' where id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId));
        var completedCodeConflict = await store.CreateAsync(
            Normalize(ValidRequest(title: "Different title", projectCode: "OSAN-001")),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.ProjectCodeConflict, completedCodeConflict.Status);

        foreach (var distinctCode in new[] { "osan-001", "OSAN- 001", "OSAN-  001" })
        {
            var distinct = await store.CreateAsync(
                Normalize(ValidRequest(title: "Shared title", projectCode: distinctCode)),
                UserId,
                TestContext.Current.CancellationToken);
            Assert.Equal(OsanProjectCreateStatus.Success, distinct.Status);
        }

        var outerTrimConflict = await store.CreateAsync(
            Normalize(ValidRequest(title: "Another title", projectCode: " OSAN-001 ")),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.ProjectCodeConflict, outerTrimConflict.Status);

        var quantityOne = await store.CreateAsync(
            Normalize(ValidRequest(projectCode: "BOUNDARY-1", quantity: 1)),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Single(quantityOne.Value!.Project.Targets);

        var quantityFiveHundred = await store.CreateAsync(
            Normalize(ValidRequest(projectCode: "BOUNDARY-500", quantity: 500)),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(500, quantityFiveHundred.Value!.Project.Targets.Count);
        Assert.Equal(3500, quantityFiveHundred.Value.Project.Targets.Sum(target => target.Steps.Count));

        var concurrentResults = await Task.WhenAll(
            store.CreateAsync(
                Normalize(ValidRequest(title: "Concurrent A", projectCode: "CONCURRENT-CODE")),
                UserId,
                TestContext.Current.CancellationToken),
            store.CreateAsync(
                Normalize(ValidRequest(title: "Concurrent B", projectCode: "CONCURRENT-CODE")),
                UserId,
                TestContext.Current.CancellationToken));
        Assert.Equal(1, concurrentResults.Count(result => result.Status == OsanProjectCreateStatus.Success));
        Assert.Equal(1, concurrentResults.Count(result => result.Status == OsanProjectCreateStatus.ProjectCodeConflict));
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='CONCURRENT-CODE' and project_profile='Osan';",
            TestContext.Current.CancellationToken));

        var scoped = await store.ListAsync(
            new Emi.Qms.Api.Projects.ProjectAccessScope(false, [$"osan-{projectId:N}"]),
            TestContext.Current.CancellationToken);
        Assert.Single(scoped.Items);
        Assert.Equal(projectId, scoped.Items[0].ProjectId);
        Assert.Empty((await store.ListAsync(
            new Emi.Qms.Api.Projects.ProjectAccessScope(false, []),
            TestContext.Current.CancellationToken)).Items);
    }

    [Fact]
    public async Task Store_RollsBackEveryRowWhenSnapshotCreationFails()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);
        await database.ExecuteAsync($"""
            insert into departments (id, code, name, is_active, sort_order)
            values ('89000000-0000-0000-0000-000000000010', 'osan-test', 'Osan Test', true, 1);
            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            values ('{UserId:D}', 'osan-project-test', 'Osan Project Test',
                    '89000000-0000-0000-0000-000000000010', true);

            create function fail_osan_step_snapshot_for_test()
            returns trigger language plpgsql as $$
            begin
                if exists (
                    select 1 from projects
                    where id = new.project_id and project_code = 'ROLLBACK-TEST'
                ) then
                    raise exception 'synthetic_osan_step_failure';
                end if;
                return new;
            end $$;
            create trigger trg_fail_osan_step_snapshot_for_test
            before insert on osan_project_target_steps
            for each row execute function fail_osan_step_snapshot_for_test();
            """, TestContext.Current.CancellationToken);

        var operationId = Guid.NewGuid();
        var store = new OsanProjectStore(provider);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => store.CreateAsync(
            Normalize(ValidRequest(projectCode: "ROLLBACK-TEST", operationId: operationId)),
            UserId,
            TestContext.Current.CancellationToken));
        Assert.Contains("synthetic_osan_step_failure", exception.MessageText, StringComparison.Ordinal);

        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='ROLLBACK-TEST';",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_create_operations where operation_id=@operation_id;",
            TestContext.Current.CancellationToken,
            ("operation_id", operationId)));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            """
            select (select count(*) from osan_project_targets)
                 + (select count(*) from osan_project_target_steps)
                 + (select count(*) from osan_project_events)
                 + (select count(*) from user_project_access);
            """,
            TestContext.Current.CancellationToken));
    }

    private static CreateOsanProjectRequest ValidRequest(
        string title = "Project title",
        string projectCode = "OSAN-TEST",
        string customerName = "Customer",
        string? poNumber = null,
        string? workOrderNumber = null,
        int? quantity = 1,
        Guid? operationId = null) =>
        new(
            title,
            projectCode,
            customerName,
            poNumber,
            workOrderNumber,
            new DateOnly(2026, 12, 31),
            "Product",
            quantity,
            operationId ?? Guid.NewGuid());

    private static NormalizedCreateOsanProjectInput Normalize(CreateOsanProjectRequest request)
    {
        var (input, errors) = OsanProjectInputNormalizer.Normalize(request);
        Assert.Empty(errors);
        return Assert.IsType<NormalizedCreateOsanProjectInput>(input);
    }

    private static DatabaseMigrationRunner CreateMigrationRunner(
        string repositoryRoot,
        DatabaseConnectionStringProvider provider,
        IConfiguration configuration) =>
        new(
            provider,
            new DatabaseMigrationCatalog(new TestWebHostEnvironment(repositoryRoot)),
            new DatabaseRuntimePrivilegeManager(),
            configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private readonly IConfiguration baseConfiguration;
        private readonly string databaseName;

        private PostgreSqlTestDatabase(string repositoryRoot, string databaseName, IConfiguration baseConfiguration)
        {
            RepositoryRoot = repositoryRoot;
            this.databaseName = databaseName;
            this.baseConfiguration = baseConfiguration;
        }

        public string RepositoryRoot { get; }
        private string ConnectionString => BuildConnectionString(baseConfiguration, databaseName);

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var envValues = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));
            var baseConfiguration = TestConfigurationIsolation.BuildBaseDatabaseConfiguration(envValues);
            var databaseName = $"emi_qms_osan_project_test_{Guid.NewGuid():N}";
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(baseConfiguration, "postgres"));
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new PostgreSqlTestDatabase(repositoryRoot, databaseName, baseConfiguration);
        }

        public IConfiguration CreateConfiguration()
        {
            var values = baseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            values["DATABASE_NAME"] = databaseName;
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        public async Task ExecuteAsync(
            string sql,
            CancellationToken cancellationToken,
            params (string Name, object Value)[] parameters)
        {
            await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
            await using var command = dataSource.CreateCommand(sql);
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<T> ReadScalarAsync<T>(
            string sql,
            CancellationToken cancellationToken,
            params (string Name, object Value)[] parameters)
        {
            await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
            await using var command = dataSource.CreateCommand(sql);
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.NotNull(value);
            return (T)value;
        }

        public async ValueTask DisposeAsync()
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(baseConfiguration, "postgres"));
            await using var command = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(databaseName)} with (force);");
            await command.ExecuteNonQueryAsync();
        }

        private static string BuildConnectionString(IConfiguration configuration, string targetDatabase)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var builder = new NpgsqlConnectionStringBuilder(provider.GetConnectionString())
            {
                Database = targetDatabase,
                Pooling = false
            };
            return builder.ConnectionString;
        }

        private static string QuoteIdentifier(string value) =>
            new NpgsqlCommandBuilder().QuoteIdentifier(value);

        private static Dictionary<string, string?> LoadDotEnv(string path)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return values;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    values[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
                }
            }
            return values;
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "README.md"))
                    && Directory.Exists(Path.Combine(current.FullName, "database", "migrations")))
                {
                    return current.FullName;
                }
                current = current.Parent;
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
