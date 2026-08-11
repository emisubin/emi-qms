using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.IO.Compression;
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
        Assert.Equal("영업", created.AuthorDepartmentName);
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

    [Fact]
    public async Task NoticeEditor_AuthorCanEditWithRevisionAndReadersCanDownloadAttachments()
    {
        await using var context = await NoticeApiTestContext.CreateAsync();
        using var author = context.CreateClient("dev-sales");
        using var reader = context.CreateClient("dev-quality");
        var notificationCountBefore = await context.ReadCountAsync("notifications");
        var workItemCountBefore = await context.ReadCountAsync("work_items");

        var createResponse = await author.PostAsJsonAsync(
            "/api/notices",
            new CreateNoticeRequest(Guid.NewGuid(), "설비 점검", "**중요** 안내", NoticeBodyFormats.BoldMarkupV1),
            TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<NoticeDetailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(1, created.Version);
        Assert.Equal(NoticeBodyFormats.BoldMarkupV1, created.BodyFormat);
        Assert.True(created.CanEdit);

        var forbiddenEdit = await reader.PutAsJsonAsync(
            $"/api/notices/{created.NoticeId}",
            new UpdateNoticeRequest(1, "타인 수정", "차단", NoticeBodyFormats.BoldMarkupV1),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);

        var updateResponse = await author.PutAsJsonAsync(
            $"/api/notices/{created.NoticeId}",
            new UpdateNoticeRequest(1, "설비 점검 변경", "**변경된 중요 안내**", NoticeBodyFormats.BoldMarkupV1),
            TestContext.Current.CancellationToken);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<NoticeDetailResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Equal(1L, await context.ReadCountAsync("notice_post_revisions"));

        var staleEdit = await author.PutAsJsonAsync(
            $"/api/notices/{created.NoticeId}",
            new UpdateNoticeRequest(1, "오래된 수정", "차단", NoticeBodyFormats.BoldMarkupV1),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleEdit.StatusCode);

        using var pdfUpload = CreateUpload("점검안내.pdf", "%PDF-1.7\nsynthetic notice attachment"u8.ToArray());
        var uploadResponse = await author.PostAsync(
            $"/api/notices/{created.NoticeId}/attachments",
            pdfUpload,
            TestContext.Current.CancellationToken);
        uploadResponse.EnsureSuccessStatusCode();
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<NoticeAttachmentResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(attachment);
        Assert.Equal("application/pdf", attachment.ContentType);

        var readerDetail = await reader.GetFromJsonAsync<NoticeDetailResponse>(
            $"/api/notices/{created.NoticeId}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(readerDetail);
        Assert.False(readerDetail.CanEdit);
        Assert.Single(readerDetail.Attachments);
        Assert.False(readerDetail.Attachments[0].CanDelete);

        var download = await reader.GetAsync(
            $"/api/notices/{created.NoticeId}/attachments/{attachment.AttachmentId}/content",
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());

        for (var index = 2; index <= NoticeAttachmentValidator.MaximumAttachments; index++)
        {
            using var additionalUpload = CreateUpload($"점검안내-{index}.pdf", "%PDF-1.7\nsynthetic additional attachment"u8.ToArray());
            var additionalUploadResponse = await author.PostAsync(
                $"/api/notices/{created.NoticeId}/attachments",
                additionalUpload,
                TestContext.Current.CancellationToken);
            additionalUploadResponse.EnsureSuccessStatusCode();
        }

        using var sixthUpload = CreateUpload("점검안내-6.pdf", "%PDF-1.7\nblocked sixth attachment"u8.ToArray());
        var sixthUploadResponse = await author.PostAsync(
            $"/api/notices/{created.NoticeId}/attachments",
            sixthUpload,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, sixthUploadResponse.StatusCode);

        using var forbiddenUpload = CreateUpload("타인.pdf", "%PDF-1.7\nforbidden"u8.ToArray());
        var uploadByReader = await reader.PostAsync(
            $"/api/notices/{created.NoticeId}/attachments",
            forbiddenUpload,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, uploadByReader.StatusCode);

        var deleteAttachment = await author.DeleteAsync(
            $"/api/notices/{created.NoticeId}/attachments/{attachment.AttachmentId}",
            TestContext.Current.CancellationToken);
        deleteAttachment.EnsureSuccessStatusCode();
        var missingDownload = await reader.GetAsync(
            $"/api/notices/{created.NoticeId}/attachments/{attachment.AttachmentId}/content",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingDownload.StatusCode);

        Assert.Equal(notificationCountBefore, await context.ReadCountAsync("notifications"));
        Assert.Equal(workItemCountBefore, await context.ReadCountAsync("work_items"));
    }

    [Fact]
    public void NoticeAttachmentValidator_UsesActualContentAndOfficePackageStructure()
    {
        Assert.Equal("application/pdf", NoticeAttachmentValidator.Validate("안내.pdf", "%PDF-1.7\nsynthetic"u8.ToArray()).ContentType);
        Assert.Equal("image/jpeg", NoticeAttachmentValidator.Validate("사진.jpeg", [0xFF, 0xD8, 0xFF, 0x00]).ContentType);
        Assert.Equal("image/png", NoticeAttachmentValidator.Validate("도면.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).ContentType);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            NoticeAttachmentValidator.Validate("안내.docx", CreateOpenXml("word/document.xml")).ContentType);
        var workbook = NoticeAttachmentValidator.Validate("안내.xlsx", CreateOpenXml("xl/workbook.xml"));
        Assert.True(workbook.IsValid);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ContentType);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            NoticeAttachmentValidator.Validate("안내.pptx", CreateOpenXml("ppt/presentation.xml")).ContentType);

        var mismatched = NoticeAttachmentValidator.Validate("안내.exe", "%PDF-1.7\nsynthetic"u8.ToArray());
        Assert.False(mismatched.IsValid);
        Assert.Contains("일치", mismatched.Error);

        var oversized = NoticeAttachmentValidator.Validate(
            "초과.pdf",
            new byte[NoticeAttachmentValidator.MaximumFileBytes + 1]);
        Assert.False(oversized.IsValid);
        Assert.Contains("10MB", oversized.Error);

        var arbitraryZip = NoticeAttachmentValidator.Validate("안내.docx", CreateOpenXml("custom/data.xml"));
        Assert.False(arbitraryZip.IsValid);
    }

    private static MultipartFormDataContent CreateUpload(string fileName, byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(file, "file", fileName);
        return multipart;
    }

    private static byte[] CreateOpenXml(string packageEntry)
    {
        var mainContentType = packageEntry switch
        {
            "word/document.xml" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            "xl/workbook.xml" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            "ppt/presentation.xml" => "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            _ => "application/octet-stream"
        };
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var types = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open()))
            {
                types.Write($"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override ContentType=\"{mainContentType}\" /></Types>");
            }
            using var entry = new StreamWriter(archive.CreateEntry(packageEntry).Open());
            entry.Write("synthetic");
        }
        return stream.ToArray();
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

        public async Task<long> ReadCountAsync(string tableName)
        {
            var allowed = tableName switch
            {
                "notifications" => "notifications",
                "work_items" => "work_items",
                "notice_post_revisions" => "notice_post_revisions",
                _ => throw new ArgumentOutOfRangeException(nameof(tableName))
            };
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var command = dataSource.CreateCommand($"select count(*) from {allowed};");
            return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
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
