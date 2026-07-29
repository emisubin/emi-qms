using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Notices;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class NoticeApiTests
{
    [Fact]
    public async Task NoticeBoard_AllOperationalUsersCanCreateReadAndOnlyAuthorCanDelete()
    {
        await using var context = await NoticeApiTestContext.CreateAsync();
        using var sales = context.CreateClient("dev-sales");
        using var quality = context.CreateClient("dev-quality");
        var requestId = Guid.NewGuid();

        var createResponse = await sales.PostAsJsonAsync(
            "/api/notices",
            new CreateNoticeRequest(requestId, "  7월 생산 일정 안내  ", "  첫 줄\n\n  둘째 줄  "),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<NoticeDetailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal("7월 생산 일정 안내", created.Title);
        Assert.Equal("첫 줄\n\n  둘째 줄", created.Body);
        Assert.Equal("Dev Sales User", created.AuthorDisplayName);
        Assert.Equal("Sales", created.AuthorDepartmentName);
        Assert.True(created.CanDelete);

        var repeatedResponse = await sales.PostAsJsonAsync(
            "/api/notices",
            new CreateNoticeRequest(requestId, "다른 제목", "다른 내용"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<NoticeDetailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(repeated);
        Assert.Equal(created.NoticeId, repeated.NoticeId);
        Assert.Equal(created.Title, repeated.Title);

        var listDocument = await sales.GetFromJsonAsync<JsonDocument>(
            "/api/notices?page=1&pageSize=20",
            TestContext.Current.CancellationToken);
        Assert.NotNull(listDocument);
        var items = listDocument.RootElement.GetProperty("items");
        Assert.Single(items.EnumerateArray());
        var item = items[0];
        Assert.Equal("첫 줄 둘째 줄", item.GetProperty("preview").GetString());
        Assert.False(item.TryGetProperty("body", out _));

        var qualityDetail = await quality.GetFromJsonAsync<NoticeDetailResponse>(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(qualityDetail);
        Assert.False(qualityDetail.CanDelete);
        Assert.Equal(created.Body, qualityDetail.Body);

        var forbiddenDelete = await quality.DeleteAsync(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        var deleteResponse = await sales.DeleteAsync(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var repeatedDelete = await sales.DeleteAsync(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, repeatedDelete.StatusCode);

        var deletedDetail = await sales.GetAsync(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deletedDetail.StatusCode);
    }

    [Fact]
    public async Task NoticeBoard_RejectsInvalidPagingAndBlankContent()
    {
        await using var context = await NoticeApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-materials");

        var invalidPage = await client.GetAsync("/api/notices?page=0&pageSize=20", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        var invalidContent = await client.PostAsJsonAsync(
            "/api/notices",
            new CreateNoticeRequest(Guid.Empty, " ", " "),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidContent.StatusCode);
        var problem = await invalidContent.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("requestId", out _));
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("body", out _));
    }

    private sealed class NoticeApiTestContext : IAsyncDisposable
    {
        private NoticeApiTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public static async Task<NoticeApiTestContext> CreateAsync()
        {
            var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
            var configuration = database.CreateConfiguration(new Dictionary<string, string?>
            {
                ["DevAuthentication:Enabled"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["DevelopmentData:SeedEnabled"] = "true"
            });
            var values = configuration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            var factory = QmsWebApplicationFactory.Create(
                "Testing",
                values,
                includeDefaultDevelopmentAuthentication: true);
            return new NoticeApiTestContext(database, factory);
        }

        public HttpClient CreateClient(string developmentUserKey)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            Factory.Dispose();
            await Database.DisposeAsync();
        }
    }

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string databaseName, IConfiguration baseConfiguration)
        {
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        private string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = TestConfigurationIsolation.BuildBaseDatabaseConfiguration(
                LoadDotEnv(Path.Combine(repositoryRoot, ".env")));
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new PostgreSqlTestDatabase(databaseName, baseConfiguration);
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
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)} with (force);");
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string value) => new NpgsqlCommandBuilder().QuoteIdentifier(value);

        private static string BuildConnectionString(IConfiguration configuration, string databaseName)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var configured = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(configured));
            return new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = false }.ConnectionString;
        }

        private static Dictionary<string, string?> LoadDotEnv(string envPath)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(envPath)) return values;
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                var separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;
                values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
            }
            return values;
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }
}
