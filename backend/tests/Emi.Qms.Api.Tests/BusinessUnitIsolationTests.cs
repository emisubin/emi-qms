using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using ClosedXML.Excel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    private static readonly Guid AdminUserId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid SalesUserId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid ManufacturingUserId = Guid.Parse("50000000-0000-0000-0000-000000000004");
    private static readonly Guid NoRoleUserId = Guid.Parse("50000000-0000-0000-0000-000000000008");
    private static readonly Guid CollisionUserId = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly Guid ConflictingLocalUserId = Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid PurgeUserId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid NoMembershipUserId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid LocalProfileUserId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid ConcurrencyActorAUserId = Guid.Parse("75000000-0000-0000-0000-000000000001");
    private static readonly Guid ConcurrencyActorBUserId = Guid.Parse("75000000-0000-0000-0000-000000000002");
    private static readonly Guid BoundaryProjectId = Guid.Parse("74000000-0000-0000-0000-000000000001");
    private static readonly Guid BoundaryNoticeId = Guid.Parse("74000000-0000-0000-0000-000000000002");
    private static readonly Guid BoundaryAttachmentId = Guid.Parse("74000000-0000-0000-0000-000000000003");

    [Fact]
    public void Configuration_SeparatesRuntimeSecretsFromPrivilegedOperationsAndPreservesLegacyFallback()
    {
        var dockerfile = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "backend", "Dockerfile.production"));
        Assert.Contains(
            "COPY database/directory-migrations database/directory-migrations",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "COPY --from=build /src/database/directory-migrations ./database/directory-migrations",
            dockerfile,
            StringComparison.Ordinal);

        var values = IsolationDatabaseSet.BuildConfigurationValues(
            "dir_db",
            "cheongju_db",
            "osan_db",
            "dir_migrator",
            "dir_runtime",
            "cheongju_migrator",
            "cheongju_runtime",
            "osan_migrator",
            "osan_runtime",
            "Host=db.internal;Port=5432;Database=dir_db;Username=admin;Password=test-only",
            "test-only");
        foreach (var key in values.Keys
                     .Where(key => key.StartsWith("ConnectionStrings:", StringComparison.Ordinal)
                                   && (key.EndsWith("Migration", StringComparison.Ordinal)
                                       || key.EndsWith("Admin", StringComparison.Ordinal)))
                     .ToList())
        {
            values.Remove(key);
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var businessUnits = BusinessUnitConfiguration.Read(configuration);

        Assert.True(businessUnits.IsValid);
        var osan = businessUnits.GetBusiness(BusinessUnitCodes.Osan);
        Assert.False(osan.ExternalNotificationsEnabled);
        Assert.False(osan.EscalationWorkerEnabled);
        Assert.False(osan.AdminDeletionWorkerEnabled);
        Assert.Empty(businessUnits.ValidateOperationConnections(
            configuration,
            BusinessUnitConnectionPurpose.Runtime));
        Assert.Equal(
            3,
            businessUnits.ValidateOperationConnections(
                configuration,
                BusinessUnitConnectionPurpose.Migration).Count);

        var wrongDatabaseValues = new Dictionary<string, string?>(values);
        var wrongDatabase = new NpgsqlConnectionStringBuilder(
            wrongDatabaseValues["ConnectionStrings:OsanRuntime"])
        {
            Database = "cheongju_db"
        };
        wrongDatabaseValues["ConnectionStrings:OsanRuntime"] = wrongDatabase.ConnectionString;
        var wrongDatabaseConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(wrongDatabaseValues)
            .Build();
        Assert.Contains(
            "OSAN:runtime_database_mismatch",
            BusinessUnitConfiguration.Read(wrongDatabaseConfiguration)
                .ValidateOperationConnections(
                    wrongDatabaseConfiguration,
                    BusinessUnitConnectionPurpose.Runtime));

        var wrongSchemaValues = new Dictionary<string, string?>(values)
        {
            ["BusinessUnits:Units:Osan:ExpectedSchemaVersion"] = "0085_site_access_sessions"
        };
        Assert.Contains(
            "Units:Osan:schema_version_invalid",
            BusinessUnitConfiguration.Read(
                new ConfigurationBuilder().AddInMemoryCollection(wrongSchemaValues).Build()).Errors);

        values["BusinessUnits:Units:Osan:RuntimeRoleName"] = "cheongju_migrator";
        var invalidRoles = BusinessUnitConfiguration.Read(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        Assert.Contains("database_roles_cross_category_not_distinct", invalidRoles.Errors);

        var managedValues = IsolationDatabaseSet.BuildConfigurationValues(
            "dir_db",
            "cheongju_db",
            "osan_db",
            "dir_migrator",
            "dir_runtime",
            "cheongju_migrator",
            "cheongju_runtime",
            "osan_migrator",
            "osan_runtime",
            new NpgsqlConnectionStringBuilder
            {
                Host = "db.internal",
                Port = 5432,
                Database = "dir_db",
                Username = "multi_admin",
                Password = new string('a', 40),
                SslMode = SslMode.VerifyFull
            }.ConnectionString,
            new string('z', 40));
        var credentialIndex = 0;
        foreach (var key in managedValues.Keys
                     .Where(key => key.StartsWith("ConnectionStrings:", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal)
                     .ToList())
        {
            var builder = new NpgsqlConnectionStringBuilder(managedValues[key]);
            builder.SslMode = SslMode.VerifyFull;
            if (!key.EndsWith("Admin", StringComparison.Ordinal))
            {
                builder.Password = $"{credentialIndex++:D2}{new string('p', 38)}";
            }
            managedValues[key] = builder.ConnectionString;
        }
        Assert.Empty(DatabaseOperationSecurityPolicy.Evaluate(
            new ConfigurationBuilder().AddInMemoryCollection(managedValues).Build(),
            DatabaseOperationMode.RoleBootstrap));

        var sharedPassword = new NpgsqlConnectionStringBuilder(
            managedValues["ConnectionStrings:DirectoryMigration"]).Password;
        var sharedRuntime = new NpgsqlConnectionStringBuilder(
            managedValues["ConnectionStrings:OsanRuntime"])
        {
            Password = sharedPassword
        };
        managedValues["ConnectionStrings:OsanRuntime"] = sharedRuntime.ConnectionString;
        Assert.Contains(
            "database_credentials_passwords_not_distinct",
            DatabaseOperationSecurityPolicy.Evaluate(
                new ConfigurationBuilder().AddInMemoryCollection(managedValues).Build(),
                DatabaseOperationMode.RoleBootstrap));

        var legacyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_HOST"] = "127.0.0.1",
                ["DATABASE_PORT"] = "35432",
                ["DATABASE_NAME"] = "legacy_database",
                ["DATABASE_USER"] = "legacy_runtime",
                ["DATABASE_PASSWORD"] = "synthetic-local-only"
            })
            .Build();
        var legacyProvider = new DatabaseConnectionStringProvider(legacyConfiguration);
        var implicitLegacy = new NpgsqlConnectionStringBuilder(legacyProvider.GetConnectionString());
        var explicitLegacy = new NpgsqlConnectionStringBuilder(
            legacyProvider.GetConnectionString(
                legacyProvider.BusinessUnits.Businesses.Single(),
                BusinessUnitConnectionPurpose.Runtime));
        Assert.Equal(implicitLegacy.ConnectionString, explicitLegacy.ConnectionString);
    }

    [Fact]
    public async Task OsanExcelImport_ActualHttpPipelineEnforcesGuardPermissionAndAtomicCreation()
    {
        await using var databases = await IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var migrationCatalog = new DatabaseMigrationCatalog(environment);
        var inspector = new MigrationLedgerInspector(migrationCatalog);

        await new DatabaseRoleBootstrapper(
                databases.Configuration,
                new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance)
            .BootstrapAsync(TestContext.Current.CancellationToken);
        await ApplyExistingCheongjuSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await ApplyPartialOsanSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await new DatabaseMigrationRunner(
                provider,
                migrationCatalog,
                new DatabaseRuntimePrivilegeManager(),
                databases.Configuration,
                NullLogger<DatabaseMigrationRunner>.Instance)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);
        await new DevelopmentIdentitySeeder(
                provider,
                databases.Configuration,
                environment,
                NullLogger<DevelopmentIdentitySeeder>.Instance,
                inspector)
            .SeedAsync(TestContext.Current.CancellationToken);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, is_active)
            values
                ('{AdminUserId:D}', 'Dev', 'dev-admin', 'Dev System Administrator', true),
                ('{SalesUserId:D}', 'Dev', 'dev-sales', 'Dev Sales User', true),
                ('{ManufacturingUserId:D}', 'Dev', 'dev-manufacturing', 'Dev Manufacturing User', true),
                ('50000000-0000-0000-0000-000000000007', 'Dev', 'dev-viewer', 'Dev Read Only User', true)
            on conflict (user_id) do nothing;

            insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
            values
                ('{AdminUserId:D}', 'CHEONGJU', true),
                ('{SalesUserId:D}', 'OSAN', true),
                ('{ManufacturingUserId:D}', 'CHEONGJU', true),
                ('50000000-0000-0000-0000-000000000007', 'OSAN', true)
            on conflict (user_id, business_unit_code) do update
            set is_active = true, updated_at_utc = now();
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into user_roles (user_id, role_id)
            select '{AdminUserId:D}', id from roles where code='sales'
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            delete from role_permissions
            where role_id = (select id from roles where code='sales')
              and permission_id = (select id from permissions where code='Project.Read.All');
            """,
            TestContext.Current.CancellationToken);

        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        using (var templateRequest = Request(
                   HttpMethod.Get,
                   "/api/osan/projects/import/template",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var templateResponse = await client.SendAsync(
                   templateRequest,
                   TestContext.Current.CancellationToken))
        {
            Assert.True(
                templateResponse.StatusCode == HttpStatusCode.OK,
                await templateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                templateResponse.Content.Headers.ContentType?.MediaType);
        }

        var workbookBytes = CreateOsanImportWorkbook();
        string fileSha256;
        OsanProjectExcelRowRequest selectedRow;
        OsanProjectExcelRowRequest secondSourceRow;
        using (var previewRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/preview",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            previewRequest.Content = CreateOsanImportContent(workbookBytes);
            using var previewResponse = await client.SendAsync(
                previewRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            var preview = await previewResponse.Content.ReadFromJsonAsync<OsanProjectExcelPreviewResponse>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(preview);
            Assert.Equal(0, preview.ErrorCount);
            Assert.True(preview.SupportsRowEditing);
            Assert.Equal(2, preview.TotalRowCount);
            Assert.Equal(3, preview.TotalQuantity);
            fileSha256 = preview.FileSha256;
            var sourceRow = preview.Rows[0];
            selectedRow = new OsanProjectExcelRowRequest(
                sourceRow.RowNumber,
                "HTTP Edited Title",
                "00Http  Edited",
                sourceRow.CustomerName,
                sourceRow.PoNumber,
                sourceRow.WorkOrderNumber,
                sourceRow.DeliveryDate,
                sourceRow.ProductName,
                sourceRow.Quantity);
            secondSourceRow = new OsanProjectExcelRowRequest(
                preview.Rows[1].RowNumber,
                preview.Rows[1].Title,
                preview.Rows[1].ProjectCode,
                preview.Rows[1].CustomerName,
                preview.Rows[1].PoNumber,
                preview.Rows[1].WorkOrderNumber,
                preview.Rows[1].DeliveryDate,
                preview.Rows[1].ProductName,
                preview.Rows[1].Quantity);
        }

        foreach (var (deliveryDate, quantity, expectedField) in new (string?, decimal?, string)[]
                 {
                     ("", 1, "deliveryDate"),
                     ("2027-13-40", 1, "deliveryDate"),
                     ("2027-01-03", 1.4m, "quantity")
                 })
        {
            var invalidRow = secondSourceRow with { DeliveryDate = deliveryDate, Quantity = quantity };
            using var invalidEditRequest = Request(
                HttpMethod.Post,
                "/api/osan/projects/import/preview",
                "dev-sales",
                BusinessUnitCodes.Osan);
            invalidEditRequest.Content = CreateOsanImportContent(
                workbookBytes,
                rows: [selectedRow, invalidRow]);
            using var invalidEditResponse = await client.SendAsync(
                invalidEditRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, invalidEditResponse.StatusCode);
            var invalidEditPreview = await invalidEditResponse.Content
                .ReadFromJsonAsync<OsanProjectExcelPreviewResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(invalidEditPreview);
            Assert.Empty(invalidEditPreview.Rows[0].Errors);
            Assert.Contains(expectedField, invalidEditPreview.Rows[1].FieldErrors!.Keys);
        }

        using (var editedPreviewRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/preview",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            editedPreviewRequest.Content = CreateOsanImportContent(
                workbookBytes,
                rows: [selectedRow]);
            using var editedPreviewResponse = await client.SendAsync(
                editedPreviewRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, editedPreviewResponse.StatusCode);
            var editedPreview = await editedPreviewResponse.Content
                .ReadFromJsonAsync<OsanProjectExcelPreviewResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(editedPreview);
            var editedRow = Assert.Single(editedPreview.Rows);
            Assert.Equal(selectedRow.RowNumber, editedRow.RowNumber);
            Assert.Equal("HTTP Edited Title", editedRow.Title);
            Assert.Equal(0, editedPreview.ErrorCount);
        }

        OsanProjectExcelApplyResponse applied;
        var applyOperationId = Guid.NewGuid();
        using (var applyRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            applyRequest.Content = CreateOsanImportContent(
                workbookBytes,
                fileSha256,
                applyOperationId,
                [selectedRow],
                []);
            using var applyResponse = await client.SendAsync(applyRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
            applied = Assert.IsType<OsanProjectExcelApplyResponse>(
                await applyResponse.Content.ReadFromJsonAsync<OsanProjectExcelApplyResponse>(
                    TestContext.Current.CancellationToken));
            Assert.Equal(1, applied.CreatedCount);
            Assert.Equal([selectedRow.RowNumber], applied.CreatedRowNumbers);
        }

        using (var replayRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            replayRequest.Content = CreateOsanImportContent(
                workbookBytes,
                fileSha256,
                applyOperationId,
                [selectedRow],
                []);
            using var replayResponse = await client.SendAsync(replayRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            var replayed = await replayResponse.Content.ReadFromJsonAsync<OsanProjectExcelApplyResponse>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(replayed);
            Assert.True(replayed.Replayed);
            Assert.Equal(applied.ProjectIds, replayed.ProjectIds);
            Assert.Equal(applied.CreatedRowNumbers, replayed.CreatedRowNumbers);
        }

        using (var detailRequest = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{applied.ProjectIds[0]:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var detailResponse = await client.SendAsync(detailRequest, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            var detail = await detailResponse.Content.ReadFromJsonAsync<OsanProjectDetailResponse>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(detail);
            Assert.Equal("HTTP Edited Title", detail.Title);
            Assert.Equal("00Http  Edited", detail.ProjectCode);
            Assert.Equal(2, detail.Targets.Count);
            Assert.All(detail.Targets, target => Assert.Equal(7, target.Steps.Count));
        }
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from projects where project_code='HTTP-EXCEL-3';",
            TestContext.Current.CancellationToken));

        var duplicateRow = secondSourceRow with
        {
            Title = "HTTP Same Code Other Project",
            ProjectCode = selectedRow.ProjectCode,
            Quantity = 1
        };
        using (var duplicatePreviewRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/preview",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            duplicatePreviewRequest.Content = CreateOsanImportContent(workbookBytes, rows: [duplicateRow]);
            using var duplicatePreviewResponse = await client.SendAsync(
                duplicatePreviewRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, duplicatePreviewResponse.StatusCode);
            var duplicatePreview = await duplicatePreviewResponse.Content
                .ReadFromJsonAsync<OsanProjectExcelPreviewResponse>(TestContext.Current.CancellationToken);
            Assert.Equal("code", Assert.Single(Assert.IsType<OsanProjectExcelPreviewResponse>(duplicatePreview).Rows).DuplicateKind);
        }

        var duplicateOperationId = Guid.NewGuid();
        using (var unconfirmedDuplicateRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            unconfirmedDuplicateRequest.Content = CreateOsanImportContent(
                workbookBytes, fileSha256, duplicateOperationId, [duplicateRow], []);
            using var unconfirmedDuplicateResponse = await client.SendAsync(
                unconfirmedDuplicateRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, unconfirmedDuplicateResponse.StatusCode);
            var confirmation = await unconfirmedDuplicateResponse.Content
                .ReadFromJsonAsync<OsanProjectExcelConfirmationRequiredResponse>(TestContext.Current.CancellationToken);
            Assert.Equal([duplicateRow.RowNumber], Assert.IsType<OsanProjectExcelConfirmationRequiredResponse>(confirmation).RowNumbers);
        }

        OsanProjectExcelApplyResponse duplicateApplied;
        using (var confirmedDuplicateRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            confirmedDuplicateRequest.Content = CreateOsanImportContent(
                workbookBytes,
                fileSha256,
                duplicateOperationId,
                [duplicateRow],
                [duplicateRow.RowNumber]);
            using var confirmedDuplicateResponse = await client.SendAsync(
                confirmedDuplicateRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, confirmedDuplicateResponse.StatusCode);
            duplicateApplied = Assert.IsType<OsanProjectExcelApplyResponse>(
                await confirmedDuplicateResponse.Content.ReadFromJsonAsync<OsanProjectExcelApplyResponse>(
                    TestContext.Current.CancellationToken));
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"delete from user_project_access where user_id='{SalesUserId:D}' and project_id='{duplicateApplied.ProjectIds[0]:D}';",
            TestContext.Current.CancellationToken);
        using (var inaccessibleDuplicateRequest = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{duplicateApplied.ProjectIds[0]:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var inaccessibleDuplicateResponse = await client.SendAsync(
                   inaccessibleDuplicateRequest,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, inaccessibleDuplicateResponse.StatusCode);
        }
        using (var accessibleOriginalRequest = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{applied.ProjectIds[0]:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var accessibleOriginalResponse = await client.SendAsync(
                   accessibleOriginalRequest,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, accessibleOriginalResponse.StatusCode);
        }

        var batchRows = new[]
        {
            selectedRow with { Title = "HTTP Batch A", ProjectCode = "HTTP-BATCH-A" },
            secondSourceRow with { Title = "HTTP Batch B", ProjectCode = "HTTP-BATCH-B" }
        };
        var batchOperationId = Guid.NewGuid();
        using (var batchApplyRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            batchApplyRequest.Content = CreateOsanImportContent(
                workbookBytes, fileSha256, batchOperationId, batchRows, []);
            using var batchApplyResponse = await client.SendAsync(
                batchApplyRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, batchApplyResponse.StatusCode);
        }
        using (var narrowedBatchRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            narrowedBatchRequest.Content = CreateOsanImportContent(
                workbookBytes, fileSha256, batchOperationId, [batchRows[0]], []);
            using var narrowedBatchResponse = await client.SendAsync(
                narrowedBatchRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, narrowedBatchResponse.StatusCode);
        }


        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from user_project_access
            where user_id='{SalesUserId:D}'
              and project_id = any(array[{string.Join(",", applied.ProjectIds.Select(id => $"'{id:D}'::uuid"))}]);
            """,
            TestContext.Current.CancellationToken);
        using (var revokedReplayRequest = Request(
                   HttpMethod.Post,
                   "/api/osan/projects/import/apply",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            revokedReplayRequest.Content = CreateOsanImportContent(
                workbookBytes,
                fileSha256,
                applyOperationId,
                [selectedRow],
                []);
            using var revokedReplayResponse = await client.SendAsync(
                revokedReplayRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, revokedReplayResponse.StatusCode);
            var body = await revokedReplayResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.All(applied.ProjectIds, projectId =>
                Assert.DoesNotContain(projectId.ToString("D"), body, StringComparison.OrdinalIgnoreCase));
        }

        using (var deniedPermission = Request(
                   HttpMethod.Get,
                   "/api/osan/projects/import/template",
                   "dev-viewer",
                   BusinessUnitCodes.Osan))
        using (var deniedResponse = await client.SendAsync(deniedPermission, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        }

        using (var wrongBusinessUnit = Request(
                   HttpMethod.Get,
                   "/api/osan/projects/import/template",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        using (var wrongBusinessUnitResponse = await client.SendAsync(
                   wrongBusinessUnit,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, wrongBusinessUnitResponse.StatusCode);
            Assert.Contains(
                "business_unit_capability_disabled",
                await wrongBusinessUnitResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }

        foreach (var (method, path) in new[]
                 {
                     (HttpMethod.Post, "/api/osan/projects/import/template"),
                     (HttpMethod.Get, "/api/osan/projects/import/preview"),
                     (HttpMethod.Get, "/api/osan/projects/import/apply"),
                     (HttpMethod.Post, "/api/osan/projects/import/unknown")
                 })
        {
            using var rejected = Request(method, path, "dev-sales", BusinessUnitCodes.Osan);
            using var rejectedResponse = await client.SendAsync(rejected, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, rejectedResponse.StatusCode);
        }

        using var reviewSafeFactory = QmsWebApplicationFactory.Create(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["ReviewSafe:Enabled"] = "true",
                ["DevAuthentication:Enabled"] = "true",
                ["DevelopmentData:SeedEnabled"] = "false",
                ["Database:ApplyMigrationsOnStartup"] = "false"
            },
            includeDefaultDevelopmentAuthentication: true);
        using var reviewSafeClient = reviewSafeFactory.CreateClient();
        using var lockedRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/osan/projects/import/apply");
        using var lockedResponse = await reviewSafeClient.SendAsync(
            lockedRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)423, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task UnifiedUserAccess_FreshDirectoryAndThreeDatabases_EnforcesSafeWorkflow()
    {
        await using var databases = await IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var migrationCatalog = new DatabaseMigrationCatalog(environment);
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(migrationCatalog);
        var inspector = new MigrationLedgerInspector(migrationCatalog);

        await new DatabaseRoleBootstrapper(
                databases.Configuration,
                new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance)
            .BootstrapAsync(TestContext.Current.CancellationToken);
        await ApplyExistingCheongjuSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await ApplyPartialOsanSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await new DatabaseMigrationRunner(
                provider,
                migrationCatalog,
                new DatabaseRuntimePrivilegeManager(),
                databases.Configuration,
                NullLogger<DatabaseMigrationRunner>.Instance)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["0001_business_unit_directory", "0002_business_unit_access_administration", "0003_unified_user_access_administration", "0004_overall_administrator_access"],
            await databases.ReadColumnAsync(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select version from schema_migrations order by version;",
                TestContext.Current.CancellationToken));

        var seeder = new DevelopmentIdentitySeeder(
            provider,
            databases.Configuration,
            environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance,
            inspector);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";
        databases.Configuration["BusinessUnits:MembershipBackfill:ApprovedUserIdsDelimited"] =
            $"{AdminUserId:D};{NoRoleUserId:D};{SalesUserId:D}";
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, is_active)
            values (
                '{NoRoleUserId:D}', 'Dev', 'dev-no-role', 'Dev User Without Role', true)
            on conflict (user_id) do nothing;

            insert into directory_business_unit_memberships (
                user_id, business_unit_code, is_active)
            values ('{NoRoleUserId:D}', 'CHEONGJU', true)
            on conflict (user_id, business_unit_code) do update
            set is_active = true, updated_at_utc = now();
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update qms_users
            set is_department_head = true
            where id = '{SalesUserId:D}';

            insert into user_roles (user_id, role_id, assignment_source)
            select '{SalesUserId:D}', role.id, 'explicit'
            from roles role
            where role.code in ('design', 'system-administrator')
            on conflict (user_id, role_id) do update set assignment_source = 'explicit';
            """,
            TestContext.Current.CancellationToken);
        var runner = new BusinessUnitMembershipBackfillRunner(
            provider,
            databases.Configuration,
            NullLogger<BusinessUnitMembershipBackfillRunner>.Instance,
            inspector,
            directoryCatalog);
        var directoryBeforeInspection = await databases.ReadScalarAsync<string>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            """
            select (select count(*) from directory_business_unit_memberships)::text || ':'
                || (select count(*) from directory_membership_audit_events)::text;
            """,
            TestContext.Current.CancellationToken);
        var cheongjuBeforeInspection = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select (select count(*) from user_roles
                    where user_id in ('{AdminUserId:D}', '{NoRoleUserId:D}', '{SalesUserId:D}'))::text || ':'
                || (select count(*) from qms_users
                    where id in ('{AdminUserId:D}', '{NoRoleUserId:D}', '{SalesUserId:D}')
                      and is_department_head = true)::text;
            """,
            TestContext.Current.CancellationToken);
        var osanBeforeInspection = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select (select count(*) from qms_users where id = '{AdminUserId:D}')::text || ':'
                || (select count(*) from user_roles where user_id = '{AdminUserId:D}')::text;
            """,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            3,
            await runner.ApplyAsync(
                TestContext.Current.CancellationToken,
                dryRun: true));
        Assert.Equal(
            directoryBeforeInspection,
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                """
                select (select count(*) from directory_business_unit_memberships)::text || ':'
                    || (select count(*) from directory_membership_audit_events)::text;
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            cheongjuBeforeInspection,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select (select count(*) from user_roles
                        where user_id in ('{AdminUserId:D}', '{NoRoleUserId:D}', '{SalesUserId:D}'))::text || ':'
                    || (select count(*) from qms_users
                        where id in ('{AdminUserId:D}', '{NoRoleUserId:D}', '{SalesUserId:D}')
                          and is_department_head = true)::text;
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            osanBeforeInspection,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select (select count(*) from qms_users where id = '{AdminUserId:D}')::text || ':'
                    || (select count(*) from user_roles where user_id = '{AdminUserId:D}')::text;
                """,
                TestContext.Current.CancellationToken));

        var reconciled = await runner
            .ApplyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, reconciled);
        Assert.Equal(
            2L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_business_unit_memberships where user_id = '{AdminUserId:D}' and is_active = true;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_business_unit_memberships where user_id = '{NoRoleUserId:D}' and is_active = true;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_membership_audit_events where user_id = '{NoRoleUserId:D}' and action = 'AccessRevoked';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "false:design:explicit,sales:department-default",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select user_account.is_department_head::text || ':'
                    || string_agg(role.code || ':' || user_role.assignment_source, ',' order by role.code)
                from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join roles role on role.id = user_role.role_id
                where user_account.id = '{SalesUserId:D}'
                group by user_account.is_department_head;
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from directory_business_unit_memberships
                where user_id = '{SalesUserId:D}'
                  and business_unit_code = 'CHEONGJU'
                  and is_active = true;
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join roles role on role.id = user_role.role_id
                where user_account.id = '{AdminUserId:D}'
                  and user_account.is_active = true
                  and role.code = 'system-administrator';
                """,
                TestContext.Current.CancellationToken));
        var backfillAuditCount = await databases.ReadScalarAsync<long>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from directory_membership_audit_events;",
            TestContext.Current.CancellationToken);
        // The bootstrap list is only a seed; current Directory designation remains authoritative.
        databases.Configuration[
            "BusinessUnits:MembershipBackfill:OverallAdministratorUserIdsDelimited"] = string.Empty;
        await new BusinessUnitMembershipBackfillRunner(
                provider,
                databases.Configuration,
                NullLogger<BusinessUnitMembershipBackfillRunner>.Instance,
                inspector,
                directoryCatalog)
            .ApplyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            backfillAuditCount,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from directory_membership_audit_events;",
                TestContext.Current.CancellationToken));
        await PrepareDirectoryAndBusinessFixturesAsync(databases);

        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();
        try
        {
            await AssertIntegratedUserAccessAdministrationAsync(databases, client);
        }
        catch (Exception exception)
        {
            var errors = string.Join(
                Environment.NewLine,
                factory.Logs.Entries
                    .Where(entry => entry.LogLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                    .Select(entry => $"{entry.Category}: {entry.Message} {entry.Exception}"));
            throw new InvalidOperationException(errors, exception);
        }
    }

    [Fact]
    public async Task OverallAdministratorAccess_ExistingDirectory0001Through0003_Applies0004Only()
    {
        await using var databases = await IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var migrationCatalog = new DatabaseMigrationCatalog(environment);
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(migrationCatalog);

        await new DatabaseRoleBootstrapper(
                databases.Configuration,
                new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance)
            .BootstrapAsync(TestContext.Current.CancellationToken);
        await ApplyDirectoryMigrationPrefixAsync(
            databases,
            directoryCatalog,
            count: 3,
            TestContext.Current.CancellationToken);
        await new DatabaseMigrationRunner(
                new DatabaseConnectionStringProvider(databases.Configuration),
                migrationCatalog,
                new DatabaseRuntimePrivilegeManager(),
                databases.Configuration,
                NullLogger<DatabaseMigrationRunner>.Instance)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["0001_business_unit_directory", "0002_business_unit_access_administration", "0003_unified_user_access_administration", "0004_overall_administrator_access"],
            await databases.ReadColumnAsync(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select version from schema_migrations order by version;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.columns where table_schema = 'public' and table_name = 'directory_identities' and column_name = 'access_version';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            2L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.columns where table_schema = 'public' and table_name = 'directory_user_access_operations' and column_name in ('requested_is_overall_administrator', 'overall_administrator_before');",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            BusinessUnitConfiguration.DirectorySchemaVersion,
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select schema_contract from qms_database_identity where singleton = true;",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThreeDatabaseBoundary_EnforcesRoutingRolesWorkersAndPendingLogin()
    {
        await using var databases = await IsolationDatabaseSet.CreateAsync(TestContext.Current.CancellationToken);
        var configuration = databases.Configuration;
        var provider = new DatabaseConnectionStringProvider(configuration);
        var environment = new TestEnvironment(databases.RepositoryRoot);
        var migrationCatalog = new DatabaseMigrationCatalog(environment);
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(migrationCatalog);
        var inspector = new MigrationLedgerInspector(migrationCatalog);

        await new DatabaseRoleBootstrapper(
                configuration,
                new DatabaseRuntimePrivilegeManager(),
                NullLogger<DatabaseRoleBootstrapper>.Instance)
            .BootstrapAsync(TestContext.Current.CancellationToken);
        await AssertInheritedRoleMembershipFailsBeforeBootstrapMutationAsync(databases);
        await ApplyExistingCheongjuSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await ApplyPartialOsanSchemaAsync(
            databases,
            migrationCatalog,
            TestContext.Current.CancellationToken);
        await new DatabaseMigrationRunner(
                provider,
                migrationCatalog,
                new DatabaseRuntimePrivilegeManager(),
                configuration,
                NullLogger<DatabaseMigrationRunner>.Instance)
            .ApplyAndVerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            migrationCatalog.GetMigrationFiles().Count + 1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from schema_migrations where version = '{MigrationLedgerCompatibilityPolicy.LegacyTeamsActivityVersion}';",
                TestContext.Current.CancellationToken));

        await AssertRuntimeRoleBoundariesAsync(databases);
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from user_roles ur
                join roles r on r.id = ur.role_id
                where ur.user_id = '{AdminUserId:D}' and r.code = 'system-administrator';
                """,
                TestContext.Current.CancellationToken));
        var seeder = new DevelopmentIdentitySeeder(
            provider,
            configuration,
            environment,
            NullLogger<DevelopmentIdentitySeeder>.Instance,
            inspector);
        Assert.True(seeder.IsEnabled());
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        databases.ConfigurationValues["DevelopmentData:SeedEnabled"] = "false";

        var roleCountBeforeBackfill = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from user_roles where user_id = '{AdminUserId:D}';",
            TestContext.Current.CancellationToken);
        var backfilled = await new BusinessUnitMembershipBackfillRunner(
                provider,
                configuration,
                NullLogger<BusinessUnitMembershipBackfillRunner>.Instance,
                inspector,
                directoryCatalog)
            .ApplyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, backfilled);
        await AssertReviewSafeBackfillDoesNotMutateAsync(
            databases,
            inspector,
            directoryCatalog);
        Assert.Equal(
            roleCountBeforeBackfill,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from user_roles where user_id = '{AdminUserId:D}';",
                TestContext.Current.CancellationToken));

        await PrepareDirectoryAndBusinessFixturesAsync(databases);
        await AssertMembershipAdministrationAsync(databases);
        await AssertEntraOnboardingAsync(
            databases,
            directoryCatalog,
            inspector);
        await AssertReviewSafeEntraAuthenticationDoesNotMutateAsync(
            databases,
            directoryCatalog,
            inspector);
        await PrepareEntityIsolationFixturesAsync(databases);
        await AssertUserAdministrationUsesOnlySelectedBusinessDatabaseAsync(databases);
        await AssertHealthAndReviewSafeCheckEveryTargetAsync(
            databases,
            migrationCatalog,
            directoryCatalog,
            inspector);
        await AssertEntityAndAttachmentIsolationAsync(databases);
        await AssertPublicRequestRoutingAsync(databases);
        await AssertEntraSubjectCollisionIsPendingAsync(
            databases,
            migrationCatalog,
            directoryCatalog,
            inspector);
        await AssertConcurrentResolutionAndCancellationAsync(
            databases,
            migrationCatalog,
            directoryCatalog,
            inspector);
        await AssertDatabaseContractFailuresBlockRequestsAsync(databases);
        await AssertOsanNotificationBoundariesAsync(databases);
        await AssertWorkerFailureDoesNotRunDisabledUnitAsync(databases);
        await AssertMigrationPreflightRejectsBeforeMutationAsync(
            databases,
            migrationCatalog);
    }

    private static async Task AssertInheritedRoleMembershipFailsBeforeBootstrapMutationAsync(
        IsolationDatabaseSet databases)
    {
        var cheongju = databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju);
        var osan = databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan);
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Administrator,
            $"""
            grant {QuoteIdentifier(osan.MigrationRoleName)} to {QuoteIdentifier(cheongju.RuntimeRoleName)};
            alter role {QuoteIdentifier(cheongju.RuntimeRoleName)} noinherit;
            """,
            TestContext.Current.CancellationToken);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DatabaseRoleBootstrapper(
                        databases.Configuration,
                        new DatabaseRuntimePrivilegeManager(),
                        NullLogger<DatabaseRoleBootstrapper>.Instance)
                    .BootstrapAsync(TestContext.Current.CancellationToken));
            Assert.Contains("role memberships", exception.Message, StringComparison.Ordinal);
            Assert.False(await databases.ReadScalarAsync<bool>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Administrator,
                $"select rolinherit from pg_roles where rolname = '{cheongju.RuntimeRoleName}';",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await databases.ExecuteAsync(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Administrator,
                $"""
                revoke {QuoteIdentifier(osan.MigrationRoleName)} from {QuoteIdentifier(cheongju.RuntimeRoleName)};
                alter role {QuoteIdentifier(cheongju.RuntimeRoleName)} inherit;
                """,
                CancellationToken.None);
        }
    }

    private static async Task AssertReviewSafeBackfillDoesNotMutateAsync(
        IsolationDatabaseSet databases,
        MigrationLedgerInspector inspector,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog)
    {
        var before = await databases.ReadScalarAsync<long>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from directory_membership_audit_events;",
            TestContext.Current.CancellationToken);
        var reviewValues = new Dictionary<string, string?>(databases.ConfigurationValues)
        {
            ["ReviewSafe:Enabled"] = "true"
        };
        var reviewConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(reviewValues)
            .Build();
        var runner = new BusinessUnitMembershipBackfillRunner(
            new DatabaseConnectionStringProvider(reviewConfiguration),
            reviewConfiguration,
            NullLogger<BusinessUnitMembershipBackfillRunner>.Instance,
            inspector,
            directoryCatalog);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ApplyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            before,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from directory_membership_audit_events;",
                TestContext.Current.CancellationToken));
    }

    private static async Task AssertHealthAndReviewSafeCheckEveryTargetAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        MigrationLedgerInspector inspector)
    {
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var health = await new DatabaseHealthChecker(provider, inspector, directoryCatalog)
            .CheckAsync(TestContext.Current.CancellationToken);
        Assert.True(health.IsReady);

        var reviewValues = new Dictionary<string, string?>(databases.ConfigurationValues)
        {
            ["ReviewSafe:Enabled"] = "true"
        };
        var reviewConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(reviewValues)
            .Build();
        var reviewEnvironment = new TestEnvironment(databases.RepositoryRoot)
        {
            EnvironmentName = "UAT"
        };
        var reviewProvider = new DatabaseConnectionStringProvider(reviewConfiguration);
        var reviewStatus = await new ReviewSafeStatusService(
                reviewProvider,
                migrationCatalog,
                new MigrationLedgerInspector(migrationCatalog),
                directoryCatalog,
                reviewConfiguration,
                reviewEnvironment)
            .CheckAsync(TestContext.Current.CancellationToken);
        Assert.True(reviewStatus.Ready);
        Assert.True(reviewStatus.DatabaseReadOnly);
        Assert.False(reviewStatus.MutationAllowed);
    }

    private static async Task ApplyExistingCheongjuSchemaAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog,
        CancellationToken cancellationToken)
    {
        await using var connection = await databases.OpenAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            cancellationToken);
        await using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText = """
                create table schema_migrations (
                    version text primary key,
                    applied_at_utc timestamptz not null default now());
                """;
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in migrationCatalog.GetMigrationFiles()
                     .TakeWhile(file => !Path.GetFileName(file).StartsWith("0086_", StringComparison.Ordinal)))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "insert into schema_migrations(version) values (@version);";
                record.Parameters.AddWithValue("version", Path.GetFileNameWithoutExtension(migrationFile));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        await using var fixture = connection.CreateCommand();
        fixture.CommandText = $"""
            insert into departments (id, code, name, is_active, sort_order)
            values ('73000000-0000-0000-0000-000000000001', 'existing', 'Existing', true, 1);
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active, auth_provider)
            values (
                '{AdminUserId:D}', 'dev-admin', 'Existing Cheongju Admin',
                '73000000-0000-0000-0000-000000000001', true, 'Dev');
            insert into user_roles (user_id, role_id)
            select '{AdminUserId:D}', id from roles where code = 'system-administrator';
            """;
        await fixture.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyDirectoryMigrationPrefixAsync(
        IsolationDatabaseSet databases,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        int count,
        CancellationToken cancellationToken)
    {
        await using var connection = await databases.OpenAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            cancellationToken);
        await using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText = """
                create table schema_migrations (
                    version text primary key,
                    applied_at_utc timestamptz not null default now());
                """;
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in directoryCatalog.Catalog.GetMigrationFiles().Take(count))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "insert into schema_migrations(version) values (@version);";
                record.Parameters.AddWithValue("version", Path.GetFileNameWithoutExtension(migrationFile));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task ApplyPartialOsanSchemaAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog,
        CancellationToken cancellationToken)
    {
        await using var connection = await databases.OpenAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            cancellationToken);
        await using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText = """
                create table schema_migrations (
                    version text primary key,
                    applied_at_utc timestamptz not null default now());
                """;
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migrationFile in migrationCatalog.GetMigrationFiles())
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = await File.ReadAllTextAsync(migrationFile, cancellationToken);
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "insert into schema_migrations(version) values (@version);";
                record.Parameters.AddWithValue("version", Path.GetFileNameWithoutExtension(migrationFile));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            if (string.Equals(
                    Path.GetFileNameWithoutExtension(migrationFile),
                    MigrationLedgerCompatibilityPolicy.CanonicalTeamsActivitySuccessor,
                    StringComparison.Ordinal))
            {
                break;
            }
        }

        await using var approvedLegacy = connection.CreateCommand();
        approvedLegacy.CommandText = "insert into schema_migrations(version) values (@version);";
        approvedLegacy.Parameters.AddWithValue(
            "version",
            MigrationLedgerCompatibilityPolicy.LegacyTeamsActivityVersion);
        await approvedLegacy.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AssertRuntimeRoleBoundariesAsync(IsolationDatabaseSet databases)
    {
        foreach (var code in new[] { "DIRECTORY", BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            var target = databases.BusinessUnits.AllTargets().Single(target => target.Code == code);
            await using var connection = await databases.OpenAsync(
                code,
                BusinessUnitConnectionPurpose.Runtime,
                TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls
                from pg_roles
                where rolname = @role_name;
                """;
            command.Parameters.AddWithValue("role_name", target.RuntimeRoleName);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
            Assert.False(reader.GetBoolean(4));
        }

        await AssertCrossDatabaseConnectionDeniedAsync(
            databases,
            BusinessUnitCodes.Cheongju,
            BusinessUnitCodes.Osan);
        await AssertCrossDatabaseConnectionDeniedAsync(
            databases,
            BusinessUnitCodes.Osan,
            BusinessUnitCodes.Cheongju);
        await AssertCrossDatabaseConnectionDeniedAsync(
            databases,
            "DIRECTORY",
            BusinessUnitCodes.Cheongju);

        await using (var directory = await databases.OpenAsync(
                         "DIRECTORY",
                         BusinessUnitConnectionPurpose.Runtime,
                         TestContext.Current.CancellationToken))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = directory.CreateCommand();
                command.CommandText = $"""
                    insert into directory_business_unit_memberships (user_id, business_unit_code)
                    values ('{Guid.NewGuid():D}', 'CHEONGJU');
                    """;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);

            await using (var functionPrivilege = directory.CreateCommand())
            {
                functionPrivilege.CommandText = """
                    select has_function_privilege(
                               current_user,
                               'set_directory_business_unit_memberships(uuid,uuid,text[],uuid)',
                               'execute')
                       and has_function_privilege(
                               current_user,
                               'register_or_update_pending_entra_directory_identity(uuid,text,text,text)',
                               'execute');
                    """;
                Assert.True(await functionPrivilege.ExecuteScalarAsync(TestContext.Current.CancellationToken) is true);
            }

            await using (var publicPrivilege = directory.CreateCommand())
            {
                publicPrivilege.CommandText = """
                    select not exists (
                        select 1
                        from pg_proc function_row
                        cross join lateral aclexplode(
                            coalesce(function_row.proacl, acldefault('f', function_row.proowner))) privilege
                        where function_row.oid in (
                            'set_directory_business_unit_memberships(uuid,uuid,text[],uuid)'::regprocedure,
                            'register_or_update_pending_entra_directory_identity(uuid,text,text,text)'::regprocedure)
                          and privilege.grantee = 0
                          and privilege.privilege_type = 'EXECUTE');
                    """;
                Assert.True(await publicPrivilege.ExecuteScalarAsync(TestContext.Current.CancellationToken) is true);
            }

            var auditMutation = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = directory.CreateCommand();
                command.CommandText = "update directory_membership_audit_events set action = action;";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, auditMutation.SqlState);
        }

        await using (var cheongju = await databases.OpenAsync(
                         BusinessUnitCodes.Cheongju,
                         BusinessUnitConnectionPurpose.Runtime,
                         TestContext.Current.CancellationToken))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = cheongju.CreateCommand();
                command.CommandText = "update qms_database_identity set business_unit_code = 'OSAN';";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);

            var createRoleException = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = cheongju.CreateCommand();
                command.CommandText = "create role qms_isolation_forbidden_role;";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, createRoleException.SqlState);
        }
    }

    private static async Task AssertCrossDatabaseConnectionDeniedAsync(
        IsolationDatabaseSet databases,
        string sourceCode,
        string destinationCode)
    {
        var source = databases.GetBuilder(sourceCode, BusinessUnitConnectionPurpose.Runtime);
        source.Database = databases.BusinessUnits.AllTargets()
            .Single(target => target.Code == destinationCode)
            .ExpectedDatabaseName;
        await using var dataSource = NpgsqlDataSource.Create(source.ConnectionString);
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private static async Task AssertApprovalKpisMatchAsync(HttpClient client, int expectedCount)
    {
        using var dashboardRequest = Request(HttpMethod.Get, "/api/admin/dashboard", "dev-admin", BusinessUnitCodes.Cheongju);
        using var dashboardResponse = await client.SendAsync(dashboardRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        using var dashboard = JsonDocument.Parse(await dashboardResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedCount, dashboard.RootElement.GetProperty("pendingUserCount").GetInt32());
        using var homeRequest = Request(HttpMethod.Get, "/api/home/department-metrics", "dev-admin", BusinessUnitCodes.Cheongju);
        using var homeResponse = await client.SendAsync(homeRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        using var home = JsonDocument.Parse(await homeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedCount, Assert.Single(home.RootElement.GetProperty("metrics").EnumerateArray(),
            metric => metric.GetProperty("id").GetString() == "admin-approval").GetProperty("count").GetInt32());
    }

    private static async Task PrepareDirectoryAndBusinessFixturesAsync(IsolationDatabaseSet databases)
    {
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Runtime,
            $"""
            update qms_users set display_name = 'Cheongju Admin' where id = '{AdminUserId:D}';
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active,
                entra_object_id, email, auth_provider)
            values (
                '{LocalProfileUserId:D}', 'entra:local-profile-user', 'Cheongju Local Profile',
                null, true, 'local-profile-user', 'local-profile@example.invalid', 'EntraId');
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Runtime,
            $"""
            update qms_users set display_name = 'Osan Admin' where id = '{AdminUserId:D}';
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active, auth_provider,
                deletion_requested_at_utc, scheduled_hard_delete_at_utc)
            values (
                '{PurgeUserId:D}', 'worker-purge-user', 'Worker Purge User', null, false, 'Dev',
                now() - interval '10 days', now() - interval '1 day');
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active,
                entra_object_id, email, auth_provider)
            values (
                '{LocalProfileUserId:D}', 'entra:local-profile-user', 'Osan Local Profile',
                null, true, 'local-profile-user', 'local-profile@example.invalid', 'EntraId');
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
            values ('{AdminUserId:D}', 'OSAN', true)
            on conflict (user_id, business_unit_code) do update
            set is_active = true, updated_at_utc = now();

            insert into directory_identities (user_id, auth_provider, external_subject, is_active)
            values
                ('{SalesUserId:D}', 'Dev', 'dev-sales', true),
                ('{NoMembershipUserId:D}', 'Dev', 'dev-no-membership', true),
                ('{LocalProfileUserId:D}', 'EntraId', 'local-profile-user', true),
                ('{CollisionUserId:D}', 'EntraId', 'entra-collision-subject', true)
            on conflict (user_id) do nothing;

            insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
            values
                ('{SalesUserId:D}', 'CHEONGJU', true),
                ('{LocalProfileUserId:D}', 'CHEONGJU', true),
                ('{LocalProfileUserId:D}', 'OSAN', true),
                ('{CollisionUserId:D}', 'OSAN', true)
            on conflict (user_id, business_unit_code) do update
            set is_active = excluded.is_active, updated_at_utc = now();
            """,
            TestContext.Current.CancellationToken);
    }

    private static async Task PrepareEntityIsolationFixturesAsync(IsolationDatabaseSet databases)
    {
        foreach (var (code, label, attachmentBytes) in new[]
                 {
                     (
                         BusinessUnitCodes.Cheongju,
                         "Cheongju",
                         Encoding.UTF8.GetBytes("%PDF-1.4\nsynthetic-cheongju-boundary\n")),
                     (
                         BusinessUnitCodes.Osan,
                         "Osan",
                         Encoding.UTF8.GetBytes("%PDF-1.4\nsynthetic-osan-boundary\n"))
                 })
        {
            var attachmentHex = Convert.ToHexString(attachmentBytes).ToLowerInvariant();
            var attachmentHash = Convert.ToHexString(SHA256.HashData(attachmentBytes)).ToLowerInvariant();
            await databases.ExecuteAsync(
                code,
                BusinessUnitConnectionPurpose.Runtime,
                $"""
                insert into projects (
                    id, project_key, project_number, name, customer_name, item,
                    project_code, project_title, project_title_normalized, delivery_date,
                    sales_owner_user_id, status, created_by_user_id)
                values (
                    '{BoundaryProjectId:D}', 'boundary-{label.ToLowerInvariant()}',
                    'BOUNDARY-{label.ToUpperInvariant()}', '{label} Boundary Project',
                    '{label} Customer', 'UL891', 'BOUNDARY-{label.ToUpperInvariant()}',
                    '{label} Boundary Project', '{label.ToUpperInvariant()} BOUNDARY PROJECT',
                    current_date + 30, '{SalesUserId:D}', 'Active', '{SalesUserId:D}');

                insert into notice_posts (
                    id, title, body, author_user_id, author_display_name_snapshot, request_id)
                values (
                    '{BoundaryNoticeId:D}', '{label} Boundary Notice', 'Synthetic boundary attachment',
                    '{AdminUserId:D}', '{label} Admin', '{BoundaryNoticeId:D}');

                insert into notice_attachments (
                    id, notice_post_id, original_file_name, normalized_mime, byte_size,
                    sha256, content, created_by_user_id)
                values (
                    '{BoundaryAttachmentId:D}', '{BoundaryNoticeId:D}', '{label.ToLowerInvariant()}-boundary.pdf',
                    'application/pdf', {attachmentBytes.Length}, '{attachmentHash}',
                    decode('{attachmentHex}', 'hex'), '{AdminUserId:D}');
                """,
                TestContext.Current.CancellationToken);
        }
    }

    private static async Task AssertMembershipAdministrationAsync(IsolationDatabaseSet databases)
    {
        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        await AssertIntegratedUserAccessAdministrationAsync(databases, client);
        if (databases.BusinessUnits.Enabled)
        {
            // The integrated flow above replaces the legacy membership-only scenarios below.
            // Keep the old assertions compile-visible until their remaining onboarding coverage
            // is moved to a focused fixture.
            return;
        }

        using (var directoryRuntime = await databases.OpenAsync(
                   "DIRECTORY",
                   BusinessUnitConnectionPurpose.Runtime,
                   TestContext.Current.CancellationToken))
        {
            var nonOverallActor = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = directoryRuntime.CreateCommand();
                command.CommandText = """
                    select set_directory_business_unit_memberships(
                        @event_id, @target_user_id, @business_units, @actor_user_id);
                    """;
                command.Parameters.AddWithValue("event_id", Guid.NewGuid());
                command.Parameters.AddWithValue("target_user_id", NoMembershipUserId);
                command.Parameters.AddWithValue(
                    "business_units",
                    NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
                    new[] { BusinessUnitCodes.Cheongju });
                command.Parameters.AddWithValue("actor_user_id", SalesUserId);
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, nonOverallActor.SqlState);
        }

        using (var list = Request(
                   HttpMethod.Get,
                   "/api/admin/business-unit-access/users",
                   "dev-admin"))
        {
            var response = await client.SendAsync(list, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<MembershipSnapshotResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(body!.Users, user => user.UserId == AdminUserId && user.IsOverallAdministrator);
            Assert.Contains(body.Users, user => user.UserId == LocalProfileUserId
                && user.DisplayName == "Cheongju Local Profile"
                && user.AccountId == "local-profile@example.invalid");
            Assert.Contains(
                body.Users,
                user => user.UserId == CollisionUserId
                    && user.DisplayName == "Microsoft 365 사용자 (정보 확인 필요)");
            Assert.DoesNotContain(
                "entra-collision-subject",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
            Assert.Equal(
                [BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan],
                body.AvailableBusinessUnits);
        }

        using (var denied = Request(
                   HttpMethod.Get,
                   "/api/admin/business-unit-access/users",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(denied, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            """
            insert into role_permissions (role_id, permission_id)
            select role.id, permission.id
            from roles role
            cross join permissions permission
            where role.code = 'sales' and permission.code = 'users.manage'
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);
        try
        {
            using var localUsersManagerOnly = Request(
                HttpMethod.Get,
                "/api/admin/business-unit-access/users",
                "dev-sales",
                BusinessUnitCodes.Cheongju);
            var response = await client.SendAsync(
                localUsersManagerOnly,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await databases.ExecuteAsync(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                """
                delete from role_permissions
                where role_id = (select id from roles where code = 'sales')
                  and permission_id = (select id from permissions where code = 'users.manage');
                """,
                CancellationToken.None);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_overall_administrators
            set is_active = false
            where user_id = '{AdminUserId:D}';
            update directory_business_unit_memberships
            set is_active = false
            where user_id = '{AdminUserId:D}' and business_unit_code = 'OSAN';
            """,
            TestContext.Current.CancellationToken);
        try
        {
            using var systemAdministratorOnly = Request(
                HttpMethod.Get,
                "/api/admin/business-unit-access/users",
                "dev-admin",
                BusinessUnitCodes.Cheongju);
            var response = await client.SendAsync(
                systemAdministratorOnly,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var localListRequest = Request(HttpMethod.Get, "/api/admin/users?filter=approval-pending",
                "dev-admin", BusinessUnitCodes.Cheongju);
            using var localListResponse = await client.SendAsync(localListRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, localListResponse.StatusCode);
            using var localList = JsonDocument.Parse(await localListResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var pendingUsers = localList.RootElement.GetProperty("users").EnumerateArray().ToArray();
            Assert.Contains(pendingUsers, user => user.GetProperty("userId").GetGuid() == LocalProfileUserId);
            Assert.DoesNotContain(pendingUsers, user => user.GetProperty("userId").GetGuid() == NoMembershipUserId);
            await AssertApprovalKpisMatchAsync(client, pendingUsers.Length);
        }
        finally
        {
            await databases.ExecuteAsync(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                update directory_overall_administrators
                set is_active = true
                where user_id = '{AdminUserId:D}';
                update directory_business_unit_memberships
                set is_active = true
                where user_id = '{AdminUserId:D}' and business_unit_code = 'OSAN';
                """,
                CancellationToken.None);
        }

        var overallDesignationCount = await databases.ReadScalarAsync<long>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from directory_overall_administrators where is_active = true;",
            TestContext.Current.CancellationToken);
        var auditCountBefore = await databases.ReadScalarAsync<long>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from directory_membership_audit_events where user_id = '{NoMembershipUserId:D}' and action = 'MembershipsUpdated';",
            TestContext.Current.CancellationToken);

        var grant = await SendMembershipUpdateAsync(
            client,
            NoMembershipUserId,
            [BusinessUnitCodes.Cheongju]);
        Assert.True(grant.Changed);
        Assert.Contains(
            grant.Snapshot.Users,
            user => user.UserId == NoMembershipUserId
                && user.Memberships.SequenceEqual([BusinessUnitCodes.Cheongju]));

        using (var localProfilePending = Request(
                   HttpMethod.Get,
                   "/api/me",
                   "dev-no-membership",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(localProfilePending, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.LocalProfilePending, body?.BusinessUnitAccessStatus);
            Assert.Equal(BusinessUnitAccessStatuses.LocalProfilePending, body?.BusinessUnitAccess?.Status);
            Assert.Equal(BusinessUnitCodes.Cheongju, body?.BusinessUnitAccess?.SelectedBusinessUnit);
        }

        var auditCountAfterGrant = await databases.ReadScalarAsync<long>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from directory_membership_audit_events where user_id = '{NoMembershipUserId:D}' and action = 'MembershipsUpdated';",
            TestContext.Current.CancellationToken);
        Assert.Equal(auditCountBefore + 1, auditCountAfterGrant);
        Assert.Equal(
            $"{AdminUserId:D}:{{}}:{{CHEONGJU}}",
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select actor_user_id::text || ':' || memberships_before::text || ':' || memberships_after::text
                from directory_membership_audit_events
                where user_id = '{NoMembershipUserId:D}' and action = 'MembershipsUpdated';
                """,
                TestContext.Current.CancellationToken));

        var noChange = await SendMembershipUpdateAsync(
            client,
            NoMembershipUserId,
            [BusinessUnitCodes.Cheongju]);
        Assert.False(noChange.Changed);
        Assert.Equal(
            auditCountAfterGrant,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_membership_audit_events where user_id = '{NoMembershipUserId:D}' and action = 'MembershipsUpdated';",
                TestContext.Current.CancellationToken));

        await AssertMembershipMutationLockOrderingAsync(databases);

        using (var invalid = Request(
                   HttpMethod.Put,
                   $"/api/admin/business-unit-access/users/{NoMembershipUserId:D}/memberships",
                   "dev-admin"))
        {
            invalid.Content = JsonContent.Create(new { businessUnitCodes = new[] { "UNKNOWN" } });
            var response = await client.SendAsync(invalid, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using (var blank = Request(
                   HttpMethod.Put,
                   $"/api/admin/business-unit-access/users/{NoMembershipUserId:D}/memberships",
                   "dev-admin"))
        {
            blank.Content = JsonContent.Create(new { businessUnitCodes = new string?[] { null, " " } });
            var response = await client.SendAsync(blank, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var concurrentUpdates = await Task.WhenAll(
            SendMembershipUpdateAsync(client, NoMembershipUserId, [BusinessUnitCodes.Osan]),
            SendMembershipUpdateAsync(
                client,
                NoMembershipUserId,
                [BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan]));
        Assert.All(concurrentUpdates, update => Assert.True(update.Changed));
        Assert.True(await databases.ReadScalarAsync<bool>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            with concurrent_events as (
                select memberships_before, memberships_after
                from directory_membership_audit_events
                where user_id = '{NoMembershipUserId:D}'
                  and action = 'MembershipsUpdated'
                  and memberships_before <> array[]::text[]
            ), current_memberships as (
                select coalesce(array_agg(business_unit_code order by business_unit_code), array[]::text[]) memberships
                from directory_business_unit_memberships
                where user_id = '{NoMembershipUserId:D}' and is_active = true
            )
            select count(*) = 2
               and exists (
                   select 1
                   from concurrent_events first_update
                   join concurrent_events second_update
                     on first_update.memberships_after = second_update.memberships_before
                   cross join current_memberships current_state
                   where first_update.memberships_before = array['CHEONGJU']::text[]
                     and second_update.memberships_after = current_state.memberships)
            from concurrent_events;
            """,
            TestContext.Current.CancellationToken));
        var remove = await SendMembershipUpdateAsync(client, NoMembershipUserId, []);
        Assert.True(remove.Changed);
        Assert.DoesNotContain(
            remove.Snapshot.Users.Single(user => user.UserId == NoMembershipUserId).Memberships,
            code => code == BusinessUnitCodes.Cheongju);
        Assert.Equal(
            overallDesignationCount,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from directory_overall_administrators where is_active = true;",
                TestContext.Current.CancellationToken));

        using (var osanLocalUsers = Request(
                   HttpMethod.Get,
                   "/api/admin/users",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(osanLocalUsers, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "Osan Admin",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }

        var cheongjuLocalProfileBefore = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select u.display_name || ':' || u.is_active::text || ':'
                || coalesce(u.department_id::text, '') || ':' || u.is_department_head::text || ':'
                || coalesce((
                    select string_agg(r.code, ',' order by r.code)
                    from user_roles ur
                    join roles r on r.id = ur.role_id
                    where ur.user_id = u.id), '')
            from qms_users u
            where u.id = '{LocalProfileUserId:D}';
            """,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from user_roles ur
                join roles r on r.id = ur.role_id
                where ur.user_id = '{LocalProfileUserId:D}' and r.code = 'quality';
                """,
                TestContext.Current.CancellationToken));

        using (var osanLocalRoleMutation = Request(
                   HttpMethod.Patch,
                   $"/api/admin/users/{LocalProfileUserId:D}",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            osanLocalRoleMutation.Content = JsonContent.Create(new
            {
                departmentId = (Guid?)null,
                roleCodes = new[] { "quality" },
                isActive = true,
                isDepartmentHead = false
            });
            var response = await client.SendAsync(
                osanLocalRoleMutation,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "quality",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from qms_users u
                join user_roles ur on ur.user_id = u.id
                join roles r on r.id = ur.role_id
                where u.id = '{LocalProfileUserId:D}'
                  and u.is_active = true
                  and r.code = 'quality';
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            cheongjuLocalProfileBefore,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select u.display_name || ':' || u.is_active::text || ':'
                    || coalesce(u.department_id::text, '') || ':' || u.is_department_head::text || ':'
                    || coalesce((
                        select string_agg(r.code, ',' order by r.code)
                        from user_roles ur
                        join roles r on r.id = ur.role_id
                        where ur.user_id = u.id), '')
                from qms_users u
                where u.id = '{LocalProfileUserId:D}';
                """,
                TestContext.Current.CancellationToken));

        using (var osanLifecycleMutation = Request(
                   HttpMethod.Patch,
                   $"/api/admin/users/{AdminUserId:D}/schedule-deletion",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            osanLifecycleMutation.Content = JsonContent.Create(new { });
            var response = await client.SendAsync(
                osanLifecycleMutation,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static async Task AssertIntegratedUserAccessAdministrationAsync(
        IsolationDatabaseSet databases,
        HttpClient client)
    {
        using (var directoryRuntime = await databases.OpenAsync(
                   "DIRECTORY",
                   BusinessUnitConnectionPurpose.Runtime,
                   TestContext.Current.CancellationToken))
        {
            var legacyMutation = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = directoryRuntime.CreateCommand();
                command.CommandText = """
                    select set_directory_business_unit_memberships(
                        @event_id, @target_user_id, @business_units, @actor_user_id);
                    """;
                command.Parameters.AddWithValue("event_id", Guid.NewGuid());
                command.Parameters.AddWithValue("target_user_id", NoMembershipUserId);
                command.Parameters.AddWithValue(
                    "business_units",
                    NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
                    new[] { BusinessUnitCodes.Cheongju });
                command.Parameters.AddWithValue("actor_user_id", AdminUserId);
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, legacyMutation.SqlState);
            Assert.Equal("integrated_user_access_required", legacyMutation.MessageText);
        }

        using (var legacyApi = Request(
                   HttpMethod.Put,
                   $"/api/admin/business-unit-access/users/{NoMembershipUserId:D}/memberships",
                   "dev-admin"))
        {
            legacyApi.Content = JsonContent.Create(new { businessUnitCodes = new[] { BusinessUnitCodes.Cheongju } });
            var response = await client.SendAsync(legacyApi, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        }

        using (var list = Request(HttpMethod.Get, "/api/admin/user-access/users", "dev-admin"))
        {
            var response = await client.SendAsync(list, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<MembershipSnapshotResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(body!.Users, user => user.UserId == AdminUserId && user.IsOverallAdministrator);
            Assert.Contains(body.Users, user => user.UserId == LocalProfileUserId
                && user.DisplayName == "Cheongju Local Profile"
                && user.AccountId == "local-profile@example.invalid");
            Assert.Contains(body.Users, user => user.UserId == CollisionUserId
                && user.DisplayName == "Microsoft 365 사용자 (정보 확인 필요)");
            Assert.DoesNotContain(
                "entra-collision-subject",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
            Assert.Equal([BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan], body.AvailableBusinessUnits);
            Assert.All(body.BusinessUnits, unit => Assert.True(unit.CanManage));
            Assert.Contains(body.Users, user => user.UserId == NoMembershipUserId && user.ApprovalPending);
            await AssertApprovalKpisMatchAsync(client, body.Users.Count(user => user.ApprovalPending));
        }

        using (var denied = Request(
                   HttpMethod.Get,
                   "/api/admin/user-access/users",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_identities
            set auth_provider = 'EntraId', external_subject = 'synthetic-no-membership',
                display_name = 'Synthetic Pending User', email = 'pending@example.invalid'
            where user_id = '{NoMembershipUserId:D}';
            """,
            TestContext.Current.CancellationToken);

        var grantOperation = Guid.NewGuid();
        var grant = await SendUserAccessUpdateAsync(
            client,
            NoMembershipUserId,
            grantOperation,
            0,
            [Profile(BusinessUnitCodes.Cheongju, "sales")]);
        Assert.True(grant.Changed);
        Assert.Equal(1, grant.AccessVersion);
        Assert.Contains(grant.Snapshot.Users, user => user.UserId == NoMembershipUserId
            && user.Memberships.SequenceEqual([BusinessUnitCodes.Cheongju])
            && !user.ApprovalPending);
        await AssertApprovalKpisMatchAsync(client, grant.Snapshot.Users.Count(user => user.ApprovalPending));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*) from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join roles role on role.id = user_role.role_id
                where user_account.id = '{NoMembershipUserId:D}'
                  and user_account.is_active = true and role.code = 'sales';
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from audit_events where request_correlation_id = '{grantOperation:D}';",
                TestContext.Current.CancellationToken));

        var idempotentRetry = await SendUserAccessUpdateAsync(
            client,
            NoMembershipUserId,
            grantOperation,
            0,
            [Profile(BusinessUnitCodes.Cheongju, "sales")]);
        Assert.False(idempotentRetry.Changed);
        Assert.Equal(1, idempotentRetry.AccessVersion);
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_user_access_operations where operation_id = '{grantOperation:D}';",
                TestContext.Current.CancellationToken));

        using (var stale = CreateUserAccessUpdateRequest(
                   NoMembershipUserId,
                   Guid.NewGuid(),
                   0,
                   [Profile(BusinessUnitCodes.Osan, "quality")]))
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await client.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);
        }

        using (var ordinaryMultiple = CreateUserAccessUpdateRequest(
                   NoMembershipUserId,
                   Guid.NewGuid(),
                   1,
                   [Profile(BusinessUnitCodes.Cheongju, "sales"), Profile(BusinessUnitCodes.Osan, "quality")]))
        {
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await client.SendAsync(ordinaryMultiple, TestContext.Current.CancellationToken)).StatusCode);
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update qms_users set is_department_head = true where id = '{NoMembershipUserId:D}';
            insert into user_roles (user_id, role_id, assignment_source)
            select '{NoMembershipUserId:D}', role.id, 'explicit'
            from roles role where role.code = 'read-only'
            on conflict (user_id, role_id) do update set assignment_source = 'explicit';
            """,
            TestContext.Current.CancellationToken);
        var departmentMove = await SendUserAccessUpdateAsync(
            client,
            NoMembershipUserId,
            Guid.NewGuid(),
            1,
            [Profile(BusinessUnitCodes.Cheongju, "quality")]);
        Assert.Equal(2, departmentMove.AccessVersion);
        Assert.Equal(
            "false:quality:department-default,read-only:explicit",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select user_account.is_department_head::text || ':'
                    || string_agg(role.code || ':' || user_role.assignment_source, ',' order by role.code)
                from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join roles role on role.id = user_role.role_id
                where user_account.id = '{NoMembershipUserId:D}'
                group by user_account.is_department_head;
                """,
                TestContext.Current.CancellationToken));

        var overallTargetId = Guid.NewGuid();
        var secondOverallTargetId = Guid.NewGuid();
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, email, is_active)
            values
                ('{overallTargetId:D}', 'EntraId', 'overall-target-subject',
                 'Synthetic Overall Target', 'overall@example.invalid', true),
                ('{secondOverallTargetId:D}', 'EntraId', 'second-overall-target-subject',
                 'Synthetic Second Overall Target', 'second-overall@example.invalid', true);
            """,
            TestContext.Current.CancellationToken);
        var overallUpdate = await SendUserAccessUpdateAsync(
            client,
            overallTargetId,
            Guid.NewGuid(),
            0,
            [Profile(BusinessUnitCodes.Cheongju, "system-administrator"),
             Profile(BusinessUnitCodes.Osan, "system-administrator")],
            isOverallAdministrator: true);
        var overallUser = overallUpdate.Snapshot.Users.Single(user => user.UserId == overallTargetId);
        Assert.True(overallUser.IsOverallAdministrator);
        Assert.Equal(2, overallUser.Memberships.Count);
        Assert.Equal(
            2L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from directory_overall_administrators where is_active = true;",
                TestContext.Current.CancellationToken));
        var secondOverallUpdate = await SendUserAccessUpdateAsync(
            client,
            secondOverallTargetId,
            Guid.NewGuid(),
            0,
            [Profile(BusinessUnitCodes.Cheongju, "system-administrator"),
             Profile(BusinessUnitCodes.Osan, "system-administrator")],
            isOverallAdministrator: true);
        Assert.True(secondOverallUpdate.Snapshot.Users.Single(
            user => user.UserId == secondOverallTargetId).IsOverallAdministrator);
        Assert.Equal(
            3L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from directory_overall_administrators where is_active = true;",
                TestContext.Current.CancellationToken));
        foreach (var businessUnit in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            Assert.Equal(
                1L,
                await databases.ReadScalarAsync<long>(
                    businessUnit,
                    BusinessUnitConnectionPurpose.Migration,
                    $"""
                    select count(*) from qms_users user_account
                    join user_roles user_role on user_role.user_id = user_account.id
                    join roles role on role.id = user_role.role_id
                    where user_account.id = '{overallTargetId:D}'
                      and user_account.is_active = true
                      and role.code = 'system-administrator';
                    """,
                    TestContext.Current.CancellationToken));
            Assert.True(await databases.ReadScalarAsync<bool>(
                businessUnit,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(distinct permission.id) = (select count(*) from permissions)
                from user_roles user_role
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where user_role.user_id = '{overallTargetId:D}';
                """,
                TestContext.Current.CancellationToken));
        }

        var removeOverallOperation = Guid.NewGuid();
        var removeOverall = await SendUserAccessUpdateAsync(
            client,
            overallTargetId,
            removeOverallOperation,
            1,
            [Profile(BusinessUnitCodes.Cheongju, "sales"), InactiveProfile(BusinessUnitCodes.Osan)]);
        var removedOverallUser = removeOverall.Snapshot.Users.Single(user => user.UserId == overallTargetId);
        Assert.False(removedOverallUser.IsOverallAdministrator);
        Assert.Equal([BusinessUnitCodes.Cheongju], removedOverallUser.Memberships);
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select count(*)
                from directory_membership_audit_events
                where correlation_id = '{removeOverallOperation:D}'
                  and action = 'OverallAdministratorChanged';
                """,
                TestContext.Current.CancellationToken));

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_overall_administrators
            set is_active = false
            where user_id = '{secondOverallTargetId:D}';
            update directory_business_unit_memberships
            set is_active = false
            where user_id = '{secondOverallTargetId:D}' and business_unit_code = 'OSAN';
            """,
            TestContext.Current.CancellationToken);

        await using (var directoryRuntime = await databases.OpenAsync(
                         "DIRECTORY",
                         BusinessUnitConnectionPurpose.Runtime,
                         TestContext.Current.CancellationToken))
        {
            var lastOverall = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var command = directoryRuntime.CreateCommand();
                command.CommandText = """
                    select * from begin_directory_user_access_operation(
                        @operation_id, @target_user_id, @memberships, @actor_user_id,
                        @expected_version, @request_hash, @profiles, false);
                    """;
                command.Parameters.AddWithValue("operation_id", Guid.NewGuid());
                command.Parameters.AddWithValue("target_user_id", AdminUserId);
                command.Parameters.AddWithValue(
                    "memberships",
                    NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
                    new[] { BusinessUnitCodes.Cheongju });
                command.Parameters.AddWithValue("actor_user_id", AdminUserId);
                command.Parameters.AddWithValue("expected_version", 0L);
                command.Parameters.AddWithValue("request_hash", new string('a', 64));
                command.Parameters.AddWithValue("profiles", NpgsqlTypes.NpgsqlDbType.Jsonb, "[]");
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            Assert.Equal("last_overall_administrator", lastOverall.MessageText);
        }

        var revokeOperation = Guid.NewGuid();
        var revoke = await SendUserAccessUpdateAsync(
            client,
            NoMembershipUserId,
            revokeOperation,
            2,
            [InactiveProfile(BusinessUnitCodes.Cheongju)]);
        Assert.True(revoke.Changed);
        Assert.Empty(revoke.Snapshot.Users.Single(user => user.UserId == NoMembershipUserId).Memberships);
        Assert.Equal(
            "false:quality,read-only",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select user_account.is_active::text || ':' || string_agg(role.code, ',' order by role.code)
                from qms_users user_account
                join user_roles user_role on user_role.user_id = user_account.id
                join roles role on role.id = user_role.role_id
                where user_account.id = '{NoMembershipUserId:D}'
                group by user_account.is_active;
                """,
                TestContext.Current.CancellationToken));
        Assert.True(await databases.ReadScalarAsync<bool>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select count(*) >= 2 and bool_and(correlation_id = '{revokeOperation:D}')
            from directory_membership_audit_events
            where correlation_id = '{revokeOperation:D}';
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from audit_events where request_correlation_id = '{revokeOperation:D}';",
                TestContext.Current.CancellationToken));

        var failureUserId = Guid.NewGuid();
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, email, is_active)
            values ('{failureUserId:D}', 'EntraId', 'failure-subject',
                    'Synthetic Failure User', 'failure@example.invalid', true);
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Runtime,
            $"""
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active,
                entra_object_id, email, auth_provider)
            values ('{failureUserId:D}', 'entra:wrong-subject', 'Synthetic Collision', null,
                    true, 'wrong-subject', 'collision@example.invalid', 'EntraId');
            """,
            TestContext.Current.CancellationToken);
        var failureOperation = Guid.NewGuid();
        using (var localFailure = CreateUserAccessUpdateRequest(
                   failureUserId,
                   failureOperation,
                   0,
                   [Profile(BusinessUnitCodes.Osan, "quality")]))
        {
            Assert.Equal(
                HttpStatusCode.ServiceUnavailable,
                (await client.SendAsync(localFailure, TestContext.Current.CancellationToken)).StatusCode);
        }
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_business_unit_memberships where user_id = '{failureUserId:D}' and is_active = true;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "RetryRequired:local_profile_commit_failed",
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select status || ':' || failure_code
                from directory_user_access_operations
                where operation_id = '{failureOperation:D}';
                """,
                TestContext.Current.CancellationToken));

        using (var retrySnapshotRequest = Request(HttpMethod.Get, "/api/admin/user-access/users", "dev-admin"))
        {
            var retrySnapshotResponse = await client.SendAsync(
                retrySnapshotRequest,
                TestContext.Current.CancellationToken);
            var retrySnapshot = await retrySnapshotResponse.Content.ReadFromJsonAsync<MembershipSnapshotResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            var pendingUser = retrySnapshot!.Users.Single(user => user.UserId == failureUserId);
            Assert.True(pendingUser.ApprovalPending);
            Assert.Equal(failureOperation.ToString("D"), pendingUser.PendingOperationId);
            Assert.Equal("RetryRequired", pendingUser.PendingOperationStatus);
            Assert.Contains(pendingUser.PendingProfiles, profile =>
                profile.BusinessUnitCode == BusinessUnitCodes.Osan
                && profile.IsActive
                && profile.RoleCodes.SequenceEqual(["quality"]));
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Runtime,
            $"""
            update qms_users
            set development_user_key = 'entra:failure-subject', entra_object_id = 'failure-subject'
            where id = '{failureUserId:D}';
            """,
            TestContext.Current.CancellationToken);
        var recovered = await SendUserAccessUpdateAsync(
            client,
            failureUserId,
            failureOperation,
            0,
            [Profile(BusinessUnitCodes.Osan, "quality")]);
        Assert.True(recovered.Changed);
        Assert.Equal(1, recovered.AccessVersion);
        Assert.Contains(recovered.Snapshot.Users, user => user.UserId == failureUserId
            && user.Memberships.SequenceEqual([BusinessUnitCodes.Osan]));

        var concurrentUserId = Guid.NewGuid();
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, email, is_active)
            values ('{concurrentUserId:D}', 'EntraId', 'concurrent-subject',
                    'Synthetic Concurrent User', 'concurrent@example.invalid', true);
            """,
            TestContext.Current.CancellationToken);
        using var concurrentCheongju = CreateUserAccessUpdateRequest(
            concurrentUserId,
            Guid.NewGuid(),
            0,
            [Profile(BusinessUnitCodes.Cheongju, "sales")]);
        using var concurrentOsan = CreateUserAccessUpdateRequest(
            concurrentUserId,
            Guid.NewGuid(),
            0,
            [Profile(BusinessUnitCodes.Osan, "quality")]);
        var concurrentResponses = await Task.WhenAll(
            client.SendAsync(concurrentCheongju, TestContext.Current.CancellationToken),
            client.SendAsync(concurrentOsan, TestContext.Current.CancellationToken));
        try
        {
            Assert.Single(concurrentResponses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(concurrentResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in concurrentResponses) response.Dispose();
        }
    }

    private static async Task AssertMembershipMutationLockOrderingAsync(
        IsolationDatabaseSet databases)
    {
        var selfEventA = Guid.NewGuid();
        var selfEventB = Guid.NewGuid();
        var crossEventA = Guid.NewGuid();
        var crossEventB = Guid.NewGuid();
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, is_active)
            values
                ('{ConcurrencyActorAUserId:D}', 'Dev', 'dev-concurrency-a', 'Synthetic Concurrency A', true),
                ('{ConcurrencyActorBUserId:D}', 'Dev', 'dev-concurrency-b', 'Synthetic Concurrency B', true);
            insert into directory_business_unit_memberships (
                user_id, business_unit_code, is_active)
            values
                ('{ConcurrencyActorAUserId:D}', 'CHEONGJU', true),
                ('{ConcurrencyActorBUserId:D}', 'OSAN', true);
            insert into directory_overall_administrators (user_id, is_active)
            values
                ('{ConcurrencyActorAUserId:D}', true),
                ('{ConcurrencyActorBUserId:D}', true);
            """,
            TestContext.Current.CancellationToken);

        var selfResults = await RunBlockedConcurrentMembershipMutationsAsync(
            databases,
            [ConcurrencyActorAUserId],
            new MembershipMutation(
                selfEventA,
                ConcurrencyActorAUserId,
                ConcurrencyActorAUserId,
                [BusinessUnitCodes.Osan]),
            new MembershipMutation(
                selfEventB,
                ConcurrencyActorAUserId,
                ConcurrencyActorAUserId,
                [BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan]));
        Assert.All(selfResults, Assert.True);
        Assert.True(await databases.ReadScalarAsync<bool>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            with events as (
                select id, memberships_before, memberships_after
                from directory_membership_audit_events
                where id in ('{selfEventA:D}', '{selfEventB:D}')
            ), current_memberships as (
                select coalesce(
                    array_agg(business_unit_code order by business_unit_code),
                    array[]::text[]) memberships
                from directory_business_unit_memberships
                where user_id = '{ConcurrencyActorAUserId:D}' and is_active = true
            )
            select count(*) = 2
               and exists (
                   select 1
                   from events first_event
                   join events second_event
                     on first_event.memberships_after = second_event.memberships_before
                    and first_event.id <> second_event.id
                   cross join current_memberships current_state
                   where first_event.memberships_before = array['CHEONGJU']::text[]
                     and second_event.memberships_after = current_state.memberships)
            from events;
            """,
            TestContext.Current.CancellationToken));

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_business_unit_memberships
            set is_active = false, updated_at_utc = now()
            where user_id in ('{ConcurrencyActorAUserId:D}', '{ConcurrencyActorBUserId:D}');
            insert into directory_business_unit_memberships (
                user_id, business_unit_code, is_active)
            values
                ('{ConcurrencyActorAUserId:D}', 'CHEONGJU', true),
                ('{ConcurrencyActorBUserId:D}', 'OSAN', true)
            on conflict (user_id, business_unit_code) do update
            set is_active = true, updated_at_utc = now();
            """,
            TestContext.Current.CancellationToken);

        var crossResults = await RunBlockedConcurrentMembershipMutationsAsync(
            databases,
            [ConcurrencyActorAUserId, ConcurrencyActorBUserId],
            new MembershipMutation(
                crossEventA,
                ConcurrencyActorAUserId,
                ConcurrencyActorBUserId,
                [BusinessUnitCodes.Cheongju]),
            new MembershipMutation(
                crossEventB,
                ConcurrencyActorBUserId,
                ConcurrencyActorAUserId,
                [BusinessUnitCodes.Osan]));
        Assert.All(crossResults, Assert.True);
        Assert.True(await databases.ReadScalarAsync<bool>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select
                (select count(*) = 2
                 from directory_membership_audit_events
                 where id in ('{crossEventA:D}', '{crossEventB:D}'))
                and exists (
                    select 1
                    from directory_membership_audit_events
                    where id = '{crossEventA:D}'
                      and user_id = '{ConcurrencyActorBUserId:D}'
                      and actor_user_id = '{ConcurrencyActorAUserId:D}'
                      and memberships_before = array['OSAN']::text[]
                      and memberships_after = array['CHEONGJU']::text[])
                and exists (
                    select 1
                    from directory_membership_audit_events
                    where id = '{crossEventB:D}'
                      and user_id = '{ConcurrencyActorAUserId:D}'
                      and actor_user_id = '{ConcurrencyActorBUserId:D}'
                      and memberships_before = array['CHEONGJU']::text[]
                      and memberships_after = array['OSAN']::text[])
                and (select array_agg(business_unit_code order by business_unit_code)
                     from directory_business_unit_memberships
                     where user_id = '{ConcurrencyActorAUserId:D}' and is_active = true)
                    = array['OSAN']::text[]
                and (select array_agg(business_unit_code order by business_unit_code)
                     from directory_business_unit_memberships
                     where user_id = '{ConcurrencyActorBUserId:D}' and is_active = true)
                    = array['CHEONGJU']::text[];
            """,
            TestContext.Current.CancellationToken));

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_overall_administrators
            set is_active = false
            where user_id in ('{ConcurrencyActorAUserId:D}', '{ConcurrencyActorBUserId:D}');
            """,
            TestContext.Current.CancellationToken);
    }

    private static async Task<bool[]> RunBlockedConcurrentMembershipMutationsAsync(
        IsolationDatabaseSet databases,
        IReadOnlyCollection<Guid> blockedAdministratorIds,
        MembershipMutation firstMutation,
        MembershipMutation secondMutation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;

        await using var blocker = await databases.OpenAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            cancellationToken);
        await using var blockerTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await using (var blockAdministrators = blocker.CreateCommand())
        {
            blockAdministrators.Transaction = blockerTransaction;
            blockAdministrators.CommandText = """
                select user_id
                from directory_overall_administrators
                where user_id = any(@user_ids)
                order by user_id
                for update;
                """;
            blockAdministrators.Parameters.AddWithValue(
                "user_ids",
                NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid,
                blockedAdministratorIds.ToArray());
            await blockAdministrators.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var firstConnection = await OpenMembershipConcurrencyConnectionAsync(
            databases,
            "membership-lock-first",
            cancellationToken);
        await using var secondConnection = await OpenMembershipConcurrencyConnectionAsync(
            databases,
            "membership-lock-second",
            cancellationToken);
        var firstTask = ExecuteMembershipMutationAsync(
            firstConnection,
            firstMutation,
            cancellationToken);
        var secondTask = ExecuteMembershipMutationAsync(
            secondConnection,
            secondMutation,
            cancellationToken);

        await WaitUntilSessionsAreBlockedAsync(
            databases,
            [firstConnection.ProcessID, secondConnection.ProcessID],
            cancellationToken);
        await blockerTransaction.CommitAsync(cancellationToken);
        return await Task.WhenAll(firstTask, secondTask);
    }

    private static async Task<NpgsqlConnection> OpenMembershipConcurrencyConnectionAsync(
        IsolationDatabaseSet databases,
        string applicationName,
        CancellationToken cancellationToken)
    {
        var builder = databases.GetBuilder(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Runtime);
        builder.ApplicationName = applicationName;
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<bool> ExecuteMembershipMutationAsync(
        NpgsqlConnection connection,
        MembershipMutation mutation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var timeouts = connection.CreateCommand())
        {
            timeouts.Transaction = transaction;
            timeouts.CommandText = "set local lock_timeout = '10s'; set local statement_timeout = '20s';";
            await timeouts.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select set_directory_business_unit_memberships(
                @event_id, @target_user_id, @business_units, @actor_user_id);
            """;
        command.Parameters.AddWithValue("event_id", mutation.EventId);
        command.Parameters.AddWithValue("target_user_id", mutation.TargetUserId);
        command.Parameters.AddWithValue(
            "business_units",
            NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text,
            mutation.BusinessUnitCodes);
        command.Parameters.AddWithValue("actor_user_id", mutation.ActorUserId);
        var changed = (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Membership mutation returned null."));
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    private static async Task WaitUntilSessionsAreBlockedAsync(
        IsolationDatabaseSet databases,
        IReadOnlyCollection<int> processIds,
        CancellationToken cancellationToken)
    {
        await using var observer = await databases.OpenAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Administrator,
            cancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var command = observer.CreateCommand();
            command.CommandText = """
                select count(*)
                from pg_stat_activity
                where pid = any(@process_ids)
                  and cardinality(pg_blocking_pids(pid)) > 0;
                """;
            command.Parameters.AddWithValue(
                "process_ids",
                NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer,
                processIds.ToArray());
            if ((long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) == processIds.Count)
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        throw new TimeoutException("Synthetic membership transactions did not reach the lock barrier.");
    }

    private sealed record MembershipMutation(
        Guid EventId,
        Guid ActorUserId,
        Guid TargetUserId,
        string[] BusinessUnitCodes);

    private static async Task<MembershipUpdateResponse> SendMembershipUpdateAsync(
        HttpClient client,
        Guid userId,
        IReadOnlyCollection<string> businessUnitCodes)
    {
        var update = await SendUserAccessUpdateAsync(
            client,
            userId,
            Guid.NewGuid(),
            0,
            businessUnitCodes.Select(code => Profile(code, "quality")).ToArray());
        return new MembershipUpdateResponse(update.Changed, update.Snapshot);
    }

    private static UserAccessProfileRequest Profile(string businessUnitCode, string roleCode) =>
        new(
            businessUnitCode,
            roleCode switch
            {
                "system-administrator" => Guid.Parse("10000000-0000-0000-0000-000000000001"),
                "sales" => Guid.Parse("10000000-0000-0000-0000-000000000002"),
                _ => Guid.Parse("10000000-0000-0000-0000-000000000005")
            },
            [roleCode],
            true,
            false);

    private static UserAccessProfileRequest InactiveProfile(string businessUnitCode) =>
        new(businessUnitCode, null, [], false, false);

    private static HttpRequestMessage CreateUserAccessUpdateRequest(
        Guid userId,
        Guid operationId,
        long expectedVersion,
        IReadOnlyCollection<UserAccessProfileRequest> profiles,
        bool isOverallAdministrator = false)
    {
        var request = Request(
            HttpMethod.Put,
            $"/api/admin/user-access/users/{userId:D}/access",
            "dev-admin");
        request.Content = JsonContent.Create(new { operationId, expectedVersion, isOverallAdministrator, profiles });
        return request;
    }

    private static async Task<UserAccessUpdateResponse> SendUserAccessUpdateAsync(
        HttpClient client,
        Guid userId,
        Guid operationId,
        long expectedVersion,
        IReadOnlyCollection<UserAccessProfileRequest> profiles,
        bool isOverallAdministrator = false)
    {
        using var request = CreateUserAccessUpdateRequest(
            userId, operationId, expectedVersion, profiles, isOverallAdministrator);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {responseBody}");
        return JsonSerializer.Deserialize<UserAccessUpdateResponse>(
            responseBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task AssertUserAdministrationUsesOnlySelectedBusinessDatabaseAsync(
        IsolationDatabaseSet databases)
    {
        var accessor = new HttpContextAccessor();
        var provider = new DatabaseConnectionStringProvider(databases.Configuration, accessor);
        var administration = new UserAdministrationStore(
            provider,
            new DbIdentityStore(provider, databases.Configuration),
            TimeProvider.System);

        foreach (var (code, expectedDisplayName, excludedDisplayName) in new[]
                 {
                     (BusinessUnitCodes.Cheongju, "Cheongju Admin", "Osan Admin"),
                     (BusinessUnitCodes.Osan, "Osan Admin", "Cheongju Admin")
                 })
        {
            accessor.HttpContext = new DefaultHttpContext();
            var target = databases.BusinessUnits.GetBusiness(code);
            BusinessUnitRequestContextFeature.Set(
                accessor.HttpContext,
                new BusinessUnitRequestContext(
                    BusinessUnitAccessStatuses.Selected,
                    AdminUserId,
                    target,
                    [code],
                    true,
                    "test_selected"));

            var snapshot = await administration.GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            Assert.Contains(
                snapshot.Users,
                user => user.UserId == AdminUserId
                    && string.Equals(user.DisplayName, expectedDisplayName, StringComparison.Ordinal));
            Assert.DoesNotContain(
                snapshot.Users,
                user => string.Equals(user.DisplayName, excludedDisplayName, StringComparison.Ordinal)
                    || string.Equals(user.DisplayName, "Dev System Administrator", StringComparison.Ordinal));
        }
    }

    private static async Task AssertPublicRequestRoutingAsync(IsolationDatabaseSet databases)
    {
        using (var cheongjuFactory = QmsWebApplicationFactory.Create(
                   DevelopmentFeaturePolicy.TestingEnvironmentName,
                   databases.ConfigurationValues,
                   includeDefaultDevelopmentAuthentication: true))
        using (var cheongjuClient = cheongjuFactory.CreateClient())
        using (var cheongjuCreatesOsanProject = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            cheongjuCreatesOsanProject.Content = JsonContent.Create(new
            {
                title = "Rejected Cheongju project",
                projectCode = "CJ-OSAN-REJECT",
                customerName = "Customer",
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Product",
                quantity = 1,
                operationId = Guid.NewGuid()
            });
            var response = await cheongjuClient.SendAsync(
                cheongjuCreatesOsanProject,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains(
                "business_unit_capability_disabled",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_business_unit_memberships
            set is_active = false
            where user_id = '{SalesUserId:D}' and business_unit_code = 'CHEONGJU';
            insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
            values ('{SalesUserId:D}', 'OSAN', true);
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            delete from role_permissions
            where role_id = (select id from roles where code = 'sales')
              and permission_id = (select id from permissions where code = 'Project.Read.All');
            """,
            TestContext.Current.CancellationToken);
        var uploadScanner = new CapturingCleanUploadMalwareScanner();
        var publicRequestConfiguration = new Dictionary<string, string?>(
            databases.ConfigurationValues,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Qr:ScanOrigin"] = "https://qms.example.test",
            ["UploadSecurity:Enabled"] = "true",
            ["UploadSecurity:FailClosed"] = "true",
            ["UploadSecurity:RejectImageMetadata"] = "true"
        };
        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            publicRequestConfiguration,
            includeDefaultDevelopmentAuthentication: true,
            configureTestServices: services =>
            {
                var descriptor = services.Single(
                    service => service.ServiceType == typeof(IUploadMalwareScanner));
                services.Remove(descriptor);
                services.AddSingleton<IUploadMalwareScanner>(uploadScanner);
            });
        using var client = factory.CreateClient();

        using (var selectionRequired = Request(HttpMethod.Get, "/api/me", "dev-admin"))
        {
            var response = await client.SendAsync(selectionRequired, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.SelectionRequired, body?.BusinessUnitAccessStatus);
        }

        using (var cheongju = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(cheongju, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<MeResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Cheongju Admin", body?.DisplayName);
        }

        using (var osan = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(osan, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<MeResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Osan Admin", body?.DisplayName);
        }

        Guid osanProjectId;
        Guid[] osanTargetIds;
        var osanCreateOperationId = Guid.NewGuid();
        var osanCreatePayload = new
        {
            title = "  Osan routed project  ",
            projectCode = " OSAN-ROUTED-001 ",
            customerName = " Routed customer ",
            poNumber = " 001-PO ",
            workOrderNumber = " WO/001 ",
            deliveryDate = new DateOnly(2026, 12, 31),
            productName = " Routed product ",
            quantity = 2,
            operationId = osanCreateOperationId
        };
        using (var createOsanProject = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            createOsanProject.Content = JsonContent.Create(osanCreatePayload);
            var response = await client.SendAsync(createOsanProject, TestContext.Current.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"Expected Osan project create to return Created, got {response.StatusCode}. Body: {responseBody}");
            using var body = JsonDocument.Parse(responseBody);
            osanProjectId = body.RootElement.GetProperty("project").GetProperty("projectId").GetGuid();
            osanTargetIds = body.RootElement.GetProperty("project").GetProperty("targets")
                .EnumerateArray()
                .Select(target => target.GetProperty("targetId").GetGuid())
                .ToArray();
            Assert.Equal(2, osanTargetIds.Length);
        }

        Guid mismatchedTargetId;
        using (var createOtherOsanProject = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            createOtherOsanProject.Content = JsonContent.Create(osanCreatePayload with
            {
                title = "Other Osan routed project",
                projectCode = "OSAN-ROUTED-002",
                quantity = 1,
                operationId = Guid.NewGuid()
            });
            var response = await client.SendAsync(
                createOtherOsanProject,
                TestContext.Current.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"Expected second Osan project create to return Created, got {response.StatusCode}. Body: {responseBody}");
            using var body = JsonDocument.Parse(responseBody);
            mismatchedTargetId = body.RootElement.GetProperty("project").GetProperty("targets")[0]
                .GetProperty("targetId").GetGuid();
        }

        using (var anonymousQr = await client.GetAsync(
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[0]:D}/qr?format=png",
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousQr.StatusCode);
        }

        using (var wrongBusinessUnitQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[0]:D}/qr?format=png",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        using (var response = await client.SendAsync(
                   wrongBusinessUnitQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var missingProjectQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{Guid.NewGuid():D}/targets/{osanTargetIds[0]:D}/qr?format=png",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   missingProjectQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from user_project_access
            where user_id = '{SalesUserId:D}' and project_id = '{osanProjectId:D}';
            """,
            TestContext.Current.CancellationToken);
        using (var unassignedQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[0]:D}/qr?format=png",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   unassignedQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into user_project_access (user_id, project_id)
            values ('{SalesUserId:D}', '{osanProjectId:D}')
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);

        var decodedTargetUrls = new List<string>();
        foreach (var targetId in osanTargetIds)
        {
            using var targetQr = Request(
                HttpMethod.Get,
                $"/api/osan/projects/{osanProjectId:D}/targets/{targetId:D}/qr?format=png",
                "dev-sales",
                BusinessUnitCodes.Osan);
            using var response = await client.SendAsync(
                targetQr,
                TestContext.Current.CancellationToken);
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
                Assert.True(response.Headers.CacheControl?.Private);
                Assert.True(response.Headers.CacheControl?.NoStore);
                Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
                using var image = Image.Load<Rgba32>(await response.Content.ReadAsByteArrayAsync(
                    TestContext.Current.CancellationToken));
                var decoded = new ZXing.ImageSharp.BarcodeReader<Rgba32>
                {
                    Options =
                    {
                        PossibleFormats = [ZXing.BarcodeFormat.QR_CODE],
                        TryHarder = true
                    }
                }.Decode(image);
                Assert.NotNull(decoded);
                Assert.Equal(
                    $"https://qms.example.test/osan/qr/{osanProjectId:D}/{targetId:D}",
                    decoded.Text);
                decodedTargetUrls.Add(decoded.Text);
            }
        }
        Assert.Equal(2, decodedTargetUrls.Distinct(StringComparer.Ordinal).Count());

        using (var targetQrDefaultSvg = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[0]:D}/qr",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   targetQrDefaultSvg,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(
                "<svg",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }

        using (var mismatchedTargetQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{mismatchedTargetId:D}/qr?format=png",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   mismatchedTargetQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"update osan_project_targets set is_active=false where id='{osanTargetIds[1]:D}';",
            TestContext.Current.CancellationToken);
        using (var inactiveTargetQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[1]:D}/qr?format=png",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   inactiveTargetQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"update osan_project_targets set is_active=true where id='{osanTargetIds[1]:D}';",
            TestContext.Current.CancellationToken);

        using (var listOsanProjects = Request(
                   HttpMethod.Get,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(listOsanProjects, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "OSAN-ROUTED-001",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                StringComparison.Ordinal);
        }

        using (var dashboard = Request(
                   HttpMethod.Get,
                   "/api/osan/dashboard?page=1&pageSize=10",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(dashboard, TestContext.Current.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected Osan dashboard to return OK, got {response.StatusCode}. Body: {responseBody}");
            Assert.Contains("OSAN-ROUTED-001", responseBody, StringComparison.Ordinal);
        }

        using (var dashboardTrailingSlash = Request(
                   HttpMethod.Get,
                   "/api/osan/dashboard/",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(
                dashboardTrailingSlash,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var dashboardPost = Request(
                   HttpMethod.Post,
                   "/api/osan/dashboard",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(dashboardPost, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var adjacentDashboardPath = Request(
                   HttpMethod.Get,
                   "/api/osan/dashboard/export",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(adjacentDashboardPath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var wrongBusinessUnitDashboard = Request(
                   HttpMethod.Get,
                   "/api/osan/dashboard",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(
                wrongBusinessUnitDashboard,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            delete from role_permissions
            where role_id = (select id from roles where code = 'sales')
              and permission_id = (select id from permissions where code = 'projects.read');
            """,
            TestContext.Current.CancellationToken);
        using (var missingReadPermissionDashboard = Request(
                   HttpMethod.Get,
                   "/api/osan/dashboard",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(
                missingReadPermissionDashboard,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            insert into role_permissions (role_id, permission_id)
            select role.id, permission.id
            from roles role
            cross join permissions permission
            where role.code = 'sales' and permission.code = 'projects.read'
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);

        using (var getOsanProject = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(getOsanProject, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into directory_identities (
                user_id, auth_provider, external_subject, display_name, is_active)
            values (
                '{ManufacturingUserId:D}', 'Dev', 'dev-manufacturing',
                'Dev Manufacturing User', true)
            on conflict (user_id) do update set is_active = true;

            insert into directory_business_unit_memberships (user_id, business_unit_code, is_active)
            values ('{ManufacturingUserId:D}', 'OSAN', true)
            on conflict (user_id, business_unit_code) do update set is_active = true;
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into user_project_access (user_id, project_id)
            values ('{ManufacturingUserId:D}', '{osanProjectId:D}')
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);

        using (var assignedProjectQr = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/targets/{osanTargetIds[0]:D}/qr?format=png",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(
                   assignedProjectQr,
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        }

        Guid[] progressTargetIds;
        using (var getProgress = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/progress",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(getProgress, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            progressTargetIds = body.RootElement.GetProperty("targets")
                .EnumerateArray()
                .Select(target => target.GetProperty("targetId").GetGuid())
                .ToArray();
            Assert.Equal(2, progressTargetIds.Length);
            Assert.All(body.RootElement.GetProperty("targets").EnumerateArray(), target =>
            {
                Assert.Equal(1, target.GetProperty("version").GetInt32());
                var steps = target.GetProperty("steps").EnumerateArray().ToArray();
                Assert.True(steps[0].GetProperty("canCompleteIndividual").GetBoolean());
                Assert.True(steps[0].GetProperty("canCompleteBatch").GetBoolean());
                Assert.All(steps.Skip(1), step =>
                {
                    Assert.False(step.GetProperty("canCompleteIndividual").GetBoolean());
                    Assert.False(step.GetProperty("canCompleteBatch").GetBoolean());
                });
            });
        }

        using (var wrongBusinessUnitProgress = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/progress",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(
                wrongBusinessUnitProgress,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var progressStateBeforeForbiddenRequests = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select concat_ws(':',
                (select count(*) from osan_progress_operations where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_target_steps where project_id = '{osanProjectId:D}' and status = 'Completed'),
                (select string_agg(status || '/' || version, ',' order by id) from osan_project_targets where project_id = '{osanProjectId:D}'));
            """,
            TestContext.Current.CancellationToken);
        using (var removedStart = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{osanProjectId:D}/progress/start",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            removedStart.Content = JsonContent.Create(new
            {
                operationId = Guid.NewGuid(),
                targets = progressTargetIds.Select(targetId => new { targetId, expectedVersion = 1 })
            });
            var response = await client.SendAsync(removedStart, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using (var unauthorizedCompletion = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{osanProjectId:D}/progress/completions",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            unauthorizedCompletion.Content = CreateProgressCompletionContent(
                Guid.NewGuid(),
                "batch",
                1,
                JsonSerializer.Serialize(
                    progressTargetIds.Select(targetId => new { targetId, expectedVersion = 1 })),
                null);
            var response = await client.SendAsync(
                unauthorizedCompletion,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var nullTargetCompletion = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{osanProjectId:D}/progress/completions",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            nullTargetCompletion.Content = CreateProgressCompletionContent(
                Guid.NewGuid(),
                "batch",
                1,
                "[null]",
                null);
            var response = await client.SendAsync(nullTargetCompletion, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(progressStateBeforeForbiddenRequests, await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select concat_ws(':',
                (select count(*) from osan_progress_operations where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_target_steps where project_id = '{osanProjectId:D}' and status = 'Completed'),
                (select string_agg(status || '/' || version, ',' order by id) from osan_project_targets where project_id = '{osanProjectId:D}'));
            """,
            TestContext.Current.CancellationToken));

        var progressPhotoBytes = CreateJpegWithSensitiveExif();
        var expectedSanitizedPhoto = (await OsanProgressPhotoValidator.ValidateAsync(
            "evidence.jpg",
            "image/jpeg",
            progressPhotoBytes,
            TestContext.Current.CancellationToken)).Photo;
        Assert.NotNull(expectedSanitizedPhoto);
        foreach (var blockedStatus in new[]
                 {
                     UploadMalwareScanStatus.Infected,
                     UploadMalwareScanStatus.Unavailable
                 })
        {
            uploadScanner.Status = blockedStatus;
            using var blockedCompletion = Request(
                HttpMethod.Post,
                $"/api/osan/projects/{osanProjectId:D}/progress/completions",
                "dev-manufacturing",
                BusinessUnitCodes.Osan);
            blockedCompletion.Content = CreateProgressCompletionContent(
                Guid.NewGuid(),
                "batch",
                1,
                JsonSerializer.Serialize(
                    progressTargetIds.Select(targetId => new { targetId, expectedVersion = 1 })),
                progressPhotoBytes,
                "image/jpeg",
                "evidence.jpg");
            using var blockedResponse = await client.SendAsync(
                blockedCompletion,
                TestContext.Current.CancellationToken);
            Assert.Equal(
                blockedStatus == UploadMalwareScanStatus.Infected
                    ? HttpStatusCode.UnprocessableEntity
                    : HttpStatusCode.ServiceUnavailable,
                blockedResponse.StatusCode);
            Assert.Equal(progressStateBeforeForbiddenRequests, await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select concat_ws(':',
                    (select count(*) from osan_progress_operations where project_id = '{osanProjectId:D}'),
                    (select count(*) from osan_project_target_steps where project_id = '{osanProjectId:D}' and status = 'Completed'),
                    (select string_agg(status || '/' || version, ',' order by id) from osan_project_targets where project_id = '{osanProjectId:D}'));
                """,
                TestContext.Current.CancellationToken));
        }
        uploadScanner.Status = UploadMalwareScanStatus.Clean;
        var scanCountBeforePhoto = uploadScanner.ScannedFiles.Count;
        Guid progressPhotoId;
        using (var completeProgress = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{osanProjectId:D}/progress/completions",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            completeProgress.Content = CreateProgressCompletionContent(
                Guid.NewGuid(),
                "batch",
                1,
                JsonSerializer.Serialize(
                    progressTargetIds.Select(targetId => new { targetId, expectedVersion = 1 })),
                progressPhotoBytes,
                "image/jpeg",
                "evidence.jpg");
            var response = await client.SendAsync(completeProgress, TestContext.Current.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected progress completion to return OK, got {response.StatusCode}. Body: {responseBody}");
            using var body = JsonDocument.Parse(responseBody);
            progressPhotoId = body.RootElement.GetProperty("project").GetProperty("targets")[0]
                .GetProperty("steps")[0].GetProperty("photos")[0].GetProperty("photoId").GetGuid();
        }
        Assert.Equal(scanCountBeforePhoto + 1, uploadScanner.ScannedFiles.Count);
        Assert.Equal(progressPhotoBytes, uploadScanner.ScannedFiles[scanCountBeforePhoto]);
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_progress_photos where project_id = '{osanProjectId:D}';",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_progress_step_photos where project_id = '{osanProjectId:D}';",
            TestContext.Current.CancellationToken));

        using (var downloadPhoto = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/progress/photos/{progressPhotoId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(downloadPhoto, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(
                expectedSanitizedPhoto.Content,
                await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.NotEqual(progressPhotoBytes, expectedSanitizedPhoto.Content);
            Assert.DoesNotContain(
                "SyntheticCamera",
                Encoding.ASCII.GetString(expectedSanitizedPhoto.Content),
                StringComparison.Ordinal);
        }

        await AssertOsanManagementHttpAsync(
            databases,
            client,
            osanProjectId,
            progressTargetIds[0],
            uploadScanner);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from user_project_access
            where user_id = '{SalesUserId:D}' and project_id = '{osanProjectId:D}';
            """,
            TestContext.Current.CancellationToken);
        Assert.Equal(0L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from user_project_access where user_id = '{SalesUserId:D}' and project_id = '{osanProjectId:D}';",
            TestContext.Current.CancellationToken));

        using (var getRevokedOsanProject = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(getRevokedOsanProject, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var getRevokedProgress = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/progress",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(getRevokedProgress, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var getRevokedPhoto = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{osanProjectId:D}/progress/photos/{progressPhotoId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(getRevokedPhoto, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var stateBeforeRevokedReplay = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select concat_ws(':',
                (select count(*) from projects where id = '{osanProjectId:D}'),
                (select count(*) from user_project_access where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_targets where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_target_steps where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_events where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_create_operations where project_id = '{osanProjectId:D}'));
            """,
            TestContext.Current.CancellationToken);
        using (var replayWithoutAccess = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            replayWithoutAccess.Content = JsonContent.Create(osanCreatePayload);
            var response = await client.SendAsync(replayWithoutAccess, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(stateBeforeRevokedReplay, await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select concat_ws(':',
                (select count(*) from projects where id = '{osanProjectId:D}'),
                (select count(*) from user_project_access where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_targets where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_target_steps where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_events where project_id = '{osanProjectId:D}'),
                (select count(*) from osan_project_create_operations where project_id = '{osanProjectId:D}'));
            """,
            TestContext.Current.CancellationToken));

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into user_project_access (user_id, project_id)
            values ('{SalesUserId:D}', '{osanProjectId:D}');
            """,
            TestContext.Current.CancellationToken);
        using (var replayWithRestoredAccess = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            replayWithRestoredAccess.Content = JsonContent.Create(osanCreatePayload);
            var response = await client.SendAsync(replayWithRestoredAccess, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(body.RootElement.GetProperty("replayed").GetBoolean());
            Assert.Equal(
                osanProjectId,
                body.RootElement.GetProperty("project").GetProperty("projectId").GetGuid());
        }

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from user_project_access
            where user_id = '{SalesUserId:D}' and project_id = '{osanProjectId:D}';
            insert into role_permissions (role_id, permission_id)
            select roles.id, permissions.id
            from roles
            cross join permissions
            where roles.code = 'sales' and permissions.code = 'Project.Read.All'
            on conflict do nothing;
            """,
            TestContext.Current.CancellationToken);
        using (var replayWithProjectReadAll = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            replayWithProjectReadAll.Content = JsonContent.Create(osanCreatePayload);
            var response = await client.SendAsync(replayWithProjectReadAll, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(body.RootElement.GetProperty("replayed").GetBoolean());
            Assert.Equal(
                osanProjectId,
                body.RootElement.GetProperty("project").GetProperty("projectId").GetGuid());
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into user_project_access (user_id, project_id)
            values ('{SalesUserId:D}', '{osanProjectId:D}');
            """,
            TestContext.Current.CancellationToken);

        using (var cheongjuCallsOsanRoute = Request(
                   HttpMethod.Get,
                   "/api/osan/projects",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(cheongjuCallsOsanRoute, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var decimalQuantity = Request(
                   HttpMethod.Post,
                   "/api/osan/projects",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            decimalQuantity.Content = JsonContent.Create(new
            {
                title = "Invalid quantity",
                projectCode = "OSAN-INVALID-DECIMAL",
                customerName = "Customer",
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Product",
                quantity = 1.5,
                operationId = Guid.NewGuid()
            });
            var response = await client.SendAsync(decimalQuantity, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from directory_business_unit_memberships
            where user_id = '{SalesUserId:D}' and business_unit_code = 'OSAN';
            update directory_business_unit_memberships
            set is_active = true
            where user_id = '{SalesUserId:D}' and business_unit_code = 'CHEONGJU';
            """,
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from directory_business_unit_memberships
            where user_id = '{ManufacturingUserId:D}' and business_unit_code = 'OSAN';
            delete from directory_identities
            where user_id = '{ManufacturingUserId:D}';
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            delete from user_project_access
            where user_id = '{ManufacturingUserId:D}' and project_id = '{osanProjectId:D}';
            """,
            TestContext.Current.CancellationToken);

        foreach (var (method, route) in new[]
                 {
                     (HttpMethod.Get, "/api/projects"),
                     (HttpMethod.Get, "/api/projects/export"),
                     (HttpMethod.Get, "/api/g2/home"),
                     (HttpMethod.Get, "/api/pending"),
                     (HttpMethod.Put, "/api/osan/projects"),
                     (HttpMethod.Delete, $"/api/osan/projects/{Guid.NewGuid():D}/unknown"),
                     (HttpMethod.Post, $"/api/projects/{Guid.NewGuid():D}/hold"),
                     (HttpMethod.Post, $"/api/projects/{Guid.NewGuid():D}/cancel")
                 })
        {
            using var request = Request(method, route, "dev-admin", BusinessUnitCodes.Osan);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var revokedFactory = QmsWebApplicationFactory.Create(
                   DevelopmentFeaturePolicy.TestingEnvironmentName,
                   databases.ConfigurationValues,
                   includeDefaultDevelopmentAuthentication: true))
        using (var revokedClient = revokedFactory.CreateClient())
        using (var forged = Request(HttpMethod.Get, "/api/me", "dev-sales", BusinessUnitCodes.Osan))
        {
            var response = await revokedClient.SendAsync(forged, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.SelectionDenied, body?.BusinessUnitAccessStatus);
            Assert.Equal(BusinessUnitAccessStatuses.SelectionDenied, body?.BusinessUnitAccess?.Status);
            Assert.Null(body?.BusinessUnitAccess?.SelectedBusinessUnit);
        }

        var noBusinessConnections = new Dictionary<string, string?>(databases.ConfigurationValues);
        foreach (var target in databases.BusinessUnits.Businesses)
        {
            var builder = databases.GetBuilder(target.Code, BusinessUnitConnectionPurpose.Runtime);
            builder.Password = "intentionally-wrong-synthetic-password";
            noBusinessConnections[$"ConnectionStrings:{target.RuntimeConnectionName}"] = builder.ConnectionString;
        }
        using (var pendingFactory = QmsWebApplicationFactory.Create(
                   DevelopmentFeaturePolicy.TestingEnvironmentName,
                   noBusinessConnections,
                   includeDefaultDevelopmentAuthentication: true))
        using (var pendingClient = pendingFactory.CreateClient())
        using (var noMembership = Request(HttpMethod.Get, "/api/me", "dev-no-membership"))
        {
            var response = await pendingClient.SendAsync(noMembership, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.NoMembership, body?.BusinessUnitAccessStatus);
        }

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_business_unit_memberships
            set is_active = false
            where user_id = '{AdminUserId:D}' and business_unit_code = 'OSAN';
            """,
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'CHEONGJU';",
            TestContext.Current.CancellationToken);
        using (var revoked = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(revoked, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.SelectionDenied, body?.BusinessUnitAccessStatus);
            Assert.Null(body?.BusinessUnitAccess?.SelectedBusinessUnit);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'OSAN';",
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            update directory_business_unit_memberships
            set is_active = true
            where user_id = '{AdminUserId:D}' and business_unit_code = 'OSAN';
            """,
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "update directory_business_units set is_active = false where code = 'OSAN';",
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'CHEONGJU';",
            TestContext.Current.CancellationToken);
        using (var inactiveUnit = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(inactiveUnit, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.SelectionDenied, body?.BusinessUnitAccessStatus);
            Assert.Null(body?.BusinessUnitAccess?.SelectedBusinessUnit);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'OSAN';",
            TestContext.Current.CancellationToken);
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "update directory_business_units set is_active = true where code = 'OSAN';",
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Runtime,
            $"update qms_users set development_user_key = 'different-local-subject' where id = '{SalesUserId:D}';",
            TestContext.Current.CancellationToken);
        using (var mismatch = Request(HttpMethod.Get, "/api/me", "dev-sales", BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(mismatch, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PendingResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BusinessUnitAccessStatuses.LocalProfilePending, body?.BusinessUnitAccessStatus);
        }
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete })
        {
            using var photo = Request(
                method,
                "/api/me/profile-photo",
                "dev-sales",
                BusinessUnitCodes.Cheongju);
            var response = await client.SendAsync(photo, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static async Task AssertEntityAndAttachmentIsolationAsync(IsolationDatabaseSet databases)
    {
        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        using (var readCheongju = Request(
                   HttpMethod.Get,
                   $"/api/projects/{BoundaryProjectId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(
                readCheongju,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var project = await response.Content.ReadFromJsonAsync<ProjectBoundaryResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(BoundaryProjectId, project?.ProjectId);
            Assert.Equal("Cheongju Boundary Project", project?.ProjectTitle);
            Assert.Equal("Active", project?.Status);
        }

        using (var readOsan = Request(
                   HttpMethod.Get,
                   $"/api/projects/{BoundaryProjectId:D}",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(
                readOsan,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var osanAuditCountBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from project_audit_events where project_id = '{BoundaryProjectId:D}';",
            TestContext.Current.CancellationToken);
        using (var holdCheongju = Request(
                   HttpMethod.Post,
                   $"/api/projects/{BoundaryProjectId:D}/hold",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            holdCheongju.Content = JsonContent.Create(new { reason = "Synthetic isolation proof" });
            var response = await client.SendAsync(
                holdCheongju,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var holdOsan = Request(
                   HttpMethod.Post,
                   $"/api/projects/{BoundaryProjectId:D}/hold",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            holdOsan.Content = JsonContent.Create(new { reason = "Forged cross-unit mutation" });
            var response = await client.SendAsync(
                holdOsan,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(
            "OnHold",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select status from projects where id = '{BoundaryProjectId:D}';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "Osan Boundary Project:Active",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select project_title || ':' || status from projects where id = '{BoundaryProjectId:D}';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            osanAuditCountBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from project_audit_events where project_id = '{BoundaryProjectId:D}';",
                TestContext.Current.CancellationToken));

        var expectedCheongjuBytes = Encoding.UTF8.GetBytes(
            "%PDF-1.4\nsynthetic-cheongju-boundary\n");
        var expectedOsanBytes = Encoding.UTF8.GetBytes(
            "%PDF-1.4\nsynthetic-osan-boundary\n");
        using (var downloadCheongju = Request(
                   HttpMethod.Get,
                   $"/api/notices/{BoundaryNoticeId:D}/attachments/{BoundaryAttachmentId:D}/content",
                   "dev-sales",
                   BusinessUnitCodes.Cheongju))
        {
            var response = await client.SendAsync(
                downloadCheongju,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                expectedCheongjuBytes,
                await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        using (var downloadOsan = Request(
                   HttpMethod.Get,
                   $"/api/notices/{BoundaryNoticeId:D}/attachments/{BoundaryAttachmentId:D}/content",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(
                downloadOsan,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(
            expectedOsanBytes,
            await databases.ReadScalarAsync<byte[]>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select content from notice_attachments where id = '{BoundaryAttachmentId:D}';",
                TestContext.Current.CancellationToken));
    }

    private static async Task AssertEntraOnboardingAsync(
        IsolationDatabaseSet databases,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        MigrationLedgerInspector inspector)
    {
        const string objectId = "00000000-0000-0000-0000-00000000e001";
        const string displayName = "Synthetic New Entra User";
        const string email = "new-entra@example.invalid";
        var values = new Dictionary<string, string?>(databases.ConfigurationValues)
        {
            ["Authentication:BootstrapAdminEmails"] = email
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var provider = new DatabaseConnectionStringProvider(configuration, accessor);
        var directoryStore = new BusinessUnitDirectoryStore(provider, directoryCatalog);
        var resolver = new BusinessUnitResolver(
            provider,
            directoryStore,
            new BusinessUnitDatabaseBoundaryValidator(provider, inspector, directoryCatalog));
        var transformation = new EntraClaimsTransformation(
            new DbIdentityStore(provider, configuration),
            new InMemoryIdentityStore(),
            configuration,
            new TestEnvironment(databases.RepositoryRoot),
            accessor,
            resolver,
            directoryStore);

        var firstLogin = await transformation.TransformAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", objectId),
                new Claim("name", displayName),
                new Claim("preferred_username", email)
            ],
            QmsAuthenticationSchemes.EntraBearer)));

        Assert.Equal(
            BusinessUnitAccessStatuses.NoMembership,
            firstLogin.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
        Assert.DoesNotContain(firstLogin.Claims, claim => claim.Type == ClaimTypes.Role);
        var directoryUserId = Guid.Parse(await databases.ReadScalarAsync<string>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"select user_id::text from directory_identities where external_subject = '{objectId}';",
            TestContext.Current.CancellationToken));
        Assert.Equal(
            directoryUserId.ToString("D"),
            firstLogin.FindFirst(QmsClaimTypes.UserId)?.Value);
        Assert.Equal(
            $"{displayName}:{email}:true",
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select display_name || ':' || email || ':' || is_active::text
                from directory_identities
                where user_id = '{directoryUserId:D}';
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_business_unit_memberships where user_id = '{directoryUserId:D}';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_overall_administrators where user_id = '{directoryUserId:D}';",
                TestContext.Current.CancellationToken));
        foreach (var businessUnit in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            Assert.Equal(
                0L,
                await databases.ReadScalarAsync<long>(
                    businessUnit,
                    BusinessUnitConnectionPurpose.Migration,
                    $"select count(*) from qms_users where entra_object_id = '{objectId}';",
                    TestContext.Current.CancellationToken));
        }

        using (var factory = QmsWebApplicationFactory.Create(
                   DevelopmentFeaturePolicy.TestingEnvironmentName,
                   databases.ConfigurationValues,
                   includeDefaultDevelopmentAuthentication: true))
        using (var client = factory.CreateClient())
        {
            var membership = await SendMembershipUpdateAsync(
                client,
                directoryUserId,
                [BusinessUnitCodes.Osan]);
            Assert.True(membership.Changed);
            Assert.Contains(
                membership.Snapshot.Users,
                user => user.UserId == directoryUserId
                    && user.DisplayName == displayName
                    && user.Email == email);
        }

        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Headers[BusinessUnitHeaderNames.Selection] = BusinessUnitCodes.Osan;
        var selectedLogin = await transformation.TransformAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", objectId),
                new Claim("name", displayName),
                new Claim("preferred_username", email)
            ],
            QmsAuthenticationSchemes.EntraBearer)));

        Assert.Equal(
            BusinessUnitAccessStatuses.Selected,
            selectedLogin.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
        Assert.Equal(directoryUserId.ToString("D"), selectedLogin.FindFirst(QmsClaimTypes.UserId)?.Value);
        Assert.DoesNotContain(selectedLogin.Claims, claim =>
            claim.Type == QmsClaimTypes.ApprovalPending && claim.Value == bool.TrueString);
        Assert.Contains(selectedLogin.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "quality");
        Assert.Equal(
            $"{directoryUserId:D}:{displayName}:{email}:EntraId:true:false",
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select id::text || ':' || display_name || ':' || email || ':' || auth_provider
                    || ':' || is_active::text || ':' || (department_id is null)::text
                from qms_users
                where entra_object_id = '{objectId}';
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from user_roles where user_id = '{directoryUserId:D}';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Cheongju,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from qms_users where id = '{directoryUserId:D}';",
                TestContext.Current.CancellationToken));

        using var administrationFactory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var administrationClient = administrationFactory.CreateClient();
        using var list = Request(
            HttpMethod.Get,
            "/api/admin/users",
            "dev-admin",
            BusinessUnitCodes.Osan);
        var listResponse = await administrationClient.SendAsync(
            list,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(displayName, listJson, StringComparison.Ordinal);
        Assert.Contains(email, listJson, StringComparison.Ordinal);
    }

    private static async Task AssertEntraSubjectCollisionIsPendingAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        MigrationLedgerInspector inspector)
    {
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active,
                entra_object_id, email, auth_provider)
            values (
                '{ConflictingLocalUserId:D}', 'entra:entra-collision-subject', 'Conflicting Local User',
                null, true, 'entra-collision-subject', 'collision@example.invalid', 'EntraId');
            """,
            TestContext.Current.CancellationToken);

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers[BusinessUnitHeaderNames.Selection] = BusinessUnitCodes.Osan;
        var provider = new DatabaseConnectionStringProvider(databases.Configuration, accessor);
        var directoryStore = new BusinessUnitDirectoryStore(provider, directoryCatalog);
        var validator = new BusinessUnitDatabaseBoundaryValidator(provider, inspector, directoryCatalog);
        var resolver = new BusinessUnitResolver(provider, directoryStore, validator);
        var transformation = new EntraClaimsTransformation(
            new DbIdentityStore(provider, databases.Configuration),
            new InMemoryIdentityStore(),
            databases.Configuration,
            new TestEnvironment(databases.RepositoryRoot),
            accessor,
            resolver,
            directoryStore);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("oid", "entra-collision-subject"), new Claim("name", "Collision")],
            QmsAuthenticationSchemes.EntraBearer));

        var transformed = await transformation.TransformAsync(principal);

        Assert.Equal(
            BusinessUnitAccessStatuses.LocalProfilePending,
            transformed.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
        Assert.DoesNotContain(
            transformed.Claims,
            claim => claim.Type == ClaimTypes.Role);
        Assert.Equal(
            "Collision:true",
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select display_name || ':' || (email is null)::text
                from directory_identities
                where user_id = '{CollisionUserId:D}';
                """,
                TestContext.Current.CancellationToken));

        var feature = BusinessUnitRequestContextFeature.Get(accessor.HttpContext);
        Assert.NotNull(feature);
        foreach (var method in new[] { HttpMethods.Get, HttpMethods.Put, HttpMethods.Delete })
        {
            var nextCalled = false;
            var context = new DefaultHttpContext
            {
                User = transformed
            };
            context.Request.Method = method;
            context.Request.Path = "/api/me/profile-photo";
            BusinessUnitRequestContextFeature.Set(context, feature);
            await new BusinessUnitCapabilityMiddleware(_ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                })
                .InvokeAsync(context, provider);
            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }
    }

    private static async Task AssertReviewSafeEntraAuthenticationDoesNotMutateAsync(
        IsolationDatabaseSet databases,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        MigrationLedgerInspector inspector)
    {
        const string existingObjectId = "00000000-0000-0000-0000-00000000e001";
        const string unknownObjectId = "00000000-0000-0000-0000-00000000e099";
        var directoryBefore = await databases.ReadScalarAsync<string>(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select user_id::text || ':' || display_name || ':' || email || ':' || updated_at_utc::text
            from directory_identities
            where external_subject = '{existingObjectId}';
            """,
            TestContext.Current.CancellationToken);
        var localBefore = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select id::text || ':' || display_name || ':' || email || ':' || is_active::text
            from qms_users
            where entra_object_id = '{existingObjectId}';
            """,
            TestContext.Current.CancellationToken);
        var reviewValues = new Dictionary<string, string?>(databases.ConfigurationValues)
        {
            ["ReviewSafe:Enabled"] = "true"
        };
        var reviewConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(reviewValues)
            .Build();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers[BusinessUnitHeaderNames.Selection] = BusinessUnitCodes.Osan;
        var provider = new DatabaseConnectionStringProvider(reviewConfiguration, accessor);
        var directoryStore = new BusinessUnitDirectoryStore(provider, directoryCatalog);
        var resolver = new BusinessUnitResolver(
            provider,
            directoryStore,
            new BusinessUnitDatabaseBoundaryValidator(provider, inspector, directoryCatalog));
        var transformation = new EntraClaimsTransformation(
            new DbIdentityStore(provider, reviewConfiguration),
            new InMemoryIdentityStore(),
            reviewConfiguration,
            new TestEnvironment(databases.RepositoryRoot) { EnvironmentName = "UAT" },
            accessor,
            resolver,
            directoryStore);

        var existing = await transformation.TransformAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", existingObjectId),
                new Claim("name", "ReviewSafe Must Not Update"),
                new Claim("preferred_username", "review-safe-change@example.invalid")
            ],
            QmsAuthenticationSchemes.EntraBearer)));

        Assert.Equal(
            BusinessUnitAccessStatuses.Selected,
            existing.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
        Assert.Equal(
            directoryBefore.Split(':', 2)[0],
            existing.FindFirst(QmsClaimTypes.UserId)?.Value);
        Assert.Equal(bool.FalseString, existing.FindFirst(QmsClaimTypes.ApprovalPending)?.Value);
        Assert.Equal(
            directoryBefore,
            await databases.ReadScalarAsync<string>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select user_id::text || ':' || display_name || ':' || email || ':' || updated_at_utc::text
                from directory_identities
                where external_subject = '{existingObjectId}';
                """,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            localBefore,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"""
                select id::text || ':' || display_name || ':' || email || ':' || is_active::text
                from qms_users
                where entra_object_id = '{existingObjectId}';
                """,
                TestContext.Current.CancellationToken));

        accessor.HttpContext = new DefaultHttpContext();
        var unknown = await transformation.TransformAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("oid", unknownObjectId), new Claim("name", "ReviewSafe Unknown")],
            QmsAuthenticationSchemes.EntraBearer)));

        Assert.Equal(
            BusinessUnitAccessStatuses.NoMembership,
            unknown.FindFirst(QmsClaimTypes.BusinessUnitAccessStatus)?.Value);
        Assert.Null(unknown.FindFirst(QmsClaimTypes.UserId));
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                "DIRECTORY",
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from directory_identities where external_subject = '{unknownObjectId}';",
                TestContext.Current.CancellationToken));
        foreach (var businessUnit in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            Assert.Equal(
                0L,
                await databases.ReadScalarAsync<long>(
                    businessUnit,
                    BusinessUnitConnectionPurpose.Migration,
                    $"select count(*) from qms_users where entra_object_id = '{unknownObjectId}';",
                    TestContext.Current.CancellationToken));
        }
    }

    private static async Task AssertConcurrentResolutionAndCancellationAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog,
        BusinessUnitDirectoryMigrationCatalog directoryCatalog,
        MigrationLedgerInspector inspector)
    {
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var resolver = new BusinessUnitResolver(
            provider,
            new BusinessUnitDirectoryStore(provider, directoryCatalog),
            new BusinessUnitDatabaseBoundaryValidator(provider, inspector, directoryCatalog));
        var resolutions = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(async index =>
            {
                var code = index % 2 == 0 ? BusinessUnitCodes.Cheongju : BusinessUnitCodes.Osan;
                var context = new DefaultHttpContext();
                context.Request.Headers[BusinessUnitHeaderNames.Selection] = code;
                return await resolver.ResolveAsync(
                    context,
                    QmsAuthProviders.Dev,
                    "dev-admin",
                    TestContext.Current.CancellationToken);
            }));
        Assert.Equal(10, resolutions.Count(item => item.Target?.Code == BusinessUnitCodes.Cheongju));
        Assert.Equal(10, resolutions.Count(item => item.Target?.Code == BusinessUnitCodes.Osan));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(
                new DefaultHttpContext(),
                QmsAuthProviders.Dev,
                "dev-admin",
                cancelled.Token));
    }

    private static async Task AssertDatabaseContractFailuresBlockRequestsAsync(IsolationDatabaseSet databases)
    {
        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        using var client = factory.CreateClient();

        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "delete from schema_migrations where version = '0001_business_unit_directory';",
            TestContext.Current.CancellationToken);
        using (var missingDirectoryLedger = Request(HttpMethod.Get, "/api/me", "dev-admin"))
        {
            var response = await client.SendAsync(
                missingDirectoryLedger,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Migration,
            "insert into schema_migrations(version) values ('0001_business_unit_directory');",
            TestContext.Current.CancellationToken);

        var directory = databases.BusinessUnits.Directory!;
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Administrator,
            $"""
            revoke connect on database {QuoteIdentifier(directory.ExpectedDatabaseName)}
            from {QuoteIdentifier(directory.RuntimeRoleName)};
            """,
            TestContext.Current.CancellationToken);
        using (var directoryUnavailable = Request(HttpMethod.Get, "/api/me", "dev-admin"))
        {
            var response = await client.SendAsync(
                directoryUnavailable,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        await databases.ExecuteAsync(
            "DIRECTORY",
            BusinessUnitConnectionPurpose.Administrator,
            $"""
            grant connect on database {QuoteIdentifier(directory.ExpectedDatabaseName)}
            to {QuoteIdentifier(directory.RuntimeRoleName)};
            """,
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "delete from schema_migrations where version = '0086_business_unit_database_identity';",
            TestContext.Current.CancellationToken);
        using (var missingLedger = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(missingLedger, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var responseText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(databases.RuntimePasswords, password => responseText.Contains(password, StringComparison.Ordinal));
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "insert into schema_migrations(version) values ('0086_business_unit_database_identity');",
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'CHEONGJU';",
            TestContext.Current.CancellationToken);
        using (var wrongIdentity = Request(HttpMethod.Get, "/api/me", "dev-admin", BusinessUnitCodes.Osan))
        {
            var response = await client.SendAsync(wrongIdentity, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'OSAN';",
            TestContext.Current.CancellationToken);

        foreach (var log in factory.Logs.Entries)
        {
            var rendered = $"{log.Message} {log.Exception}";
            Assert.DoesNotContain(databases.RuntimePasswords, password => rendered.Contains(password, StringComparison.Ordinal));
        }
    }

    private static async Task AssertOsanNotificationBoundariesAsync(IsolationDatabaseSet databases)
    {
        var osan = databases.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan);
        var provider = new DatabaseConnectionStringProvider(databases.Configuration);
        var deliveryStore = new NotificationDeliveryStore(
            provider,
            TimeProvider.System,
            databases.Configuration);
        Assert.Equal(
            0,
            await deliveryStore.CreateImmediateDeliveriesAsync(
                new NotificationOptions(),
                TestContext.Current.CancellationToken,
                osan));

        var escalationStore = new WorkItemEscalationStore(provider, TimeProvider.System);
        var candidate = new WorkItemEscalationCandidate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Synthetic Project",
            "SYNTHETIC",
            "ManufacturingWork",
            "Manufacturing",
            "ManufacturingPrimary",
            AdminUserId,
            "Osan Admin",
            true,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Synthetic Work",
            "Requested",
            null,
            null,
            null,
            null,
            null);
        var escalation = await escalationStore.CreateEscalationAsync(
            candidate,
            WorkItemEscalationLevels.L1,
            new NotificationEscalationOptions { Enabled = true },
            TestContext.Current.CancellationToken,
            osan);
        Assert.Equal(0, escalation.NotificationCount);
        Assert.Equal(0, escalation.DeliveryCount);
        Assert.Equal(
            0L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from notifications where notification_type like 'WorkItemEscalation%';",
                TestContext.Current.CancellationToken));

        await using var osanRuntime = await databases.OpenAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Runtime,
            TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = osanRuntime.CreateCommand();
            command.CommandText = """
                insert into notification_deliveries (channel, delivery_type, dedupe_key)
                values ('Mail', 'ManualTest', 'osan-forbidden-enqueue');
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        });
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        Assert.Equal("external_notification_delivery_disabled_for_business_unit", exception.MessageText);

        var requestContext = new DefaultHttpContext();
        BusinessUnitRequestContextFeature.Set(
            requestContext,
            new BusinessUnitRequestContext(
                BusinessUnitAccessStatuses.Selected,
                AdminUserId,
                osan,
                [BusinessUnitCodes.Osan],
                true,
                "test_selected"));
        var requestProvider = new DatabaseConnectionStringProvider(
            databases.Configuration,
            new HttpContextAccessor { HttpContext = requestContext });
        var handler = new CountingExternalNotificationHandler();
        var migrationCatalog = new DatabaseMigrationCatalog(
            new TestEnvironment(databases.RepositoryRoot));
        var directoryCatalog = new BusinessUnitDirectoryMigrationCatalog(migrationCatalog);
        var dispatcher = new NotificationDispatcher(
            new NotificationDeliveryStore(
                requestProvider,
                TimeProvider.System,
                databases.Configuration),
            [handler],
            new StaticOptionsMonitor<NotificationOptions>(new NotificationOptions()),
            new NotificationWorkerIdentity("osan-boundary-test"),
            requestProvider,
            new BusinessUnitDatabaseBoundaryValidator(
                requestProvider,
                new MigrationLedgerInspector(migrationCatalog),
                directoryCatalog),
            NullLogger<NotificationDispatcher>.Instance);
        var dispatch = await dispatcher.DispatchDeliveryAsync(
            Guid.NewGuid(),
            preparedMessage: null,
            retryCount: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(NotificationDeliveryStatuses.Disabled, dispatch.Status);
        Assert.Equal(0, handler.CallCount);
    }

    private static async Task AssertWorkerFailureDoesNotRunDisabledUnitAsync(IsolationDatabaseSet databases)
    {
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'OSAN';",
            TestContext.Current.CancellationToken);
        using var factory = QmsWebApplicationFactory.Create(
            DevelopmentFeaturePolicy.TestingEnvironmentName,
            databases.ConfigurationValues,
            includeDefaultDevelopmentAuthentication: true);
        _ = factory.CreateClient();
        var purge = factory.Services.GetRequiredService<IAdminDeletionPurgeService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => purge.PurgeDueAsync(TestContext.Current.CancellationToken));
        Assert.Contains("1 target(s)", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            1L,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select count(*) from qms_users where id = '{PurgeUserId:D}';",
                TestContext.Current.CancellationToken));
        await databases.ExecuteAsync(
            BusinessUnitCodes.Cheongju,
            BusinessUnitConnectionPurpose.Migration,
            "update qms_database_identity set business_unit_code = 'CHEONGJU';",
            TestContext.Current.CancellationToken);
    }

    private static async Task AssertMigrationPreflightRejectsBeforeMutationAsync(
        IsolationDatabaseSet databases,
        DatabaseMigrationCatalog migrationCatalog)
    {
        var runner = new DatabaseMigrationRunner(
            new DatabaseConnectionStringProvider(databases.Configuration),
            migrationCatalog,
            new DatabaseRuntimePrivilegeManager(),
            databases.Configuration,
            NullLogger<DatabaseMigrationRunner>.Instance);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"delete from schema_migrations where version = '{MigrationLedgerCompatibilityPolicy.CanonicalTeamsActivitySuccessor}';",
            TestContext.Current.CancellationToken);
        var missingSuccessorLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            missingSuccessorLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"insert into schema_migrations(version) values ('{MigrationLedgerCompatibilityPolicy.CanonicalTeamsActivitySuccessor}');",
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "insert into schema_migrations(version) values ('0099_unknown_migration');",
            TestContext.Current.CancellationToken);
        var unknownLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            unknownLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "delete from schema_migrations where version = '0099_unknown_migration';",
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "delete from schema_migrations where version = '0085_site_access_sessions';",
            TestContext.Current.CancellationToken);
        var nonPrefixLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            nonPrefixLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "insert into schema_migrations(version) values ('0085_site_access_sessions');",
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            alter table notification_deliveries drop constraint ck_notification_deliveries_channel;
            alter table notification_deliveries add constraint ck_notification_deliveries_channel
                check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'TeamsActivity', 'Mail'));
            """,
            TestContext.Current.CancellationToken);
        var schemaMismatchLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            schemaMismatchLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        Assert.False((await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            select pg_get_constraintdef(constraint_row.oid)
            from pg_constraint constraint_row
            join pg_class table_row on table_row.oid = constraint_row.conrelid
            where table_row.relname = 'notification_deliveries'
              and constraint_row.conname = 'ck_notification_deliveries_channel';
            """,
            TestContext.Current.CancellationToken)).Contains("WebPush", StringComparison.Ordinal));
        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            alter table notification_deliveries drop constraint ck_notification_deliveries_channel;
            alter table notification_deliveries add constraint ck_notification_deliveries_channel
                check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'TeamsActivity', 'Mail', 'WebPush'));
            """,
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            update qms_database_identity set business_unit_code = 'CHEONGJU';
            delete from schema_migrations where version = '0086_business_unit_database_identity';
            """,
            TestContext.Current.CancellationToken);
        var wrongBindingLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        var wrongBindingSchemaBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from information_schema.tables where table_schema = 'public';",
            TestContext.Current.CancellationToken);
        var wrongBindingProjectBefore = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select project_title || ':' || status from projects where id = '{BoundaryProjectId:D}';",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            wrongBindingLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            wrongBindingSchemaBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.tables where table_schema = 'public';",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            wrongBindingProjectBefore,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select project_title || ':' || status from projects where id = '{BoundaryProjectId:D}';",
                TestContext.Current.CancellationToken));

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            update qms_database_identity set business_unit_code = 'OSAN';
            insert into schema_migrations(version)
            values ('0086_business_unit_database_identity');
            """,
            TestContext.Current.CancellationToken);

        await databases.ExecuteAsync(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            drop trigger if exists trg_prevent_osan_external_notification_delivery on notification_deliveries;
            drop function if exists prevent_osan_external_notification_delivery();
            drop table qms_database_identity;
            delete from schema_migrations where version = '0086_business_unit_database_identity';
            """,
            TestContext.Current.CancellationToken);
        var unboundLedgerBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from schema_migrations;",
            TestContext.Current.CancellationToken);
        var unboundSchemaBefore = await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select count(*) from information_schema.tables where table_schema = 'public';",
            TestContext.Current.CancellationToken);
        var unboundProjectBefore = await databases.ReadScalarAsync<string>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select project_title || ':' || status from projects where id = '{BoundaryProjectId:D}';",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ApplyAndVerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            unboundLedgerBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from schema_migrations;",
                TestContext.Current.CancellationToken));
        Assert.Equal(
            unboundSchemaBefore,
            await databases.ReadScalarAsync<long>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                "select count(*) from information_schema.tables where table_schema = 'public';",
                TestContext.Current.CancellationToken));
        Assert.True(await databases.ReadScalarAsync<bool>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            "select to_regclass('public.qms_database_identity') is null;",
            TestContext.Current.CancellationToken));
        Assert.Equal(
            unboundProjectBefore,
            await databases.ReadScalarAsync<string>(
                BusinessUnitCodes.Osan,
                BusinessUnitConnectionPurpose.Migration,
                $"select project_title || ':' || status from projects where id = '{BoundaryProjectId:D}';",
                TestContext.Current.CancellationToken));
    }

    private static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        string developmentUser,
        string? businessUnit = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUser);
        if (businessUnit is not null)
        {
            request.Headers.Add(BusinessUnitHeaderNames.Selection, businessUnit);
        }
        return request;
    }

    private static byte[] CreateOsanImportWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Projects");
        var headers = new[]
        {
            "프로젝트 Title", "프로젝트 코드", "거래처", "PO No", "W/O No", "납기일", "제품명", "수량"
        };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
        for (var row = 2; row <= 3; row++)
        {
            sheet.Cell(row, 1).Value = $"HTTP Excel {row}";
            sheet.Cell(row, 2).Value = $"HTTP-EXCEL-{row}";
            sheet.Cell(row, 3).Value = "Synthetic Customer";
            sheet.Cell(row, 4).Value = $"00{row}-PO";
            sheet.Cell(row, 5).Value = $"00{row}-W/O";
            sheet.Cell(row, 6).Value = new DateTime(2026, 12, row);
            sheet.Cell(row, 7).Value = "Synthetic Product";
            sheet.Cell(row, 8).Value = row == 2 ? 2 : 1;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent CreateOsanImportContent(
        byte[] workbook,
        string? expectedFileSha256 = null,
        Guid? operationId = null,
        IReadOnlyList<OsanProjectExcelRowRequest>? rows = null,
        IReadOnlyList<int>? confirmedDuplicateRowNumbers = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "osan-import.xlsx");
        if (expectedFileSha256 is not null)
        {
            content.Add(new StringContent(expectedFileSha256), "expectedFileSha256");
        }
        if (operationId is not null)
        {
            content.Add(new StringContent(operationId.Value.ToString("D")), "operationId");
        }
        if (rows is not null)
        {
            content.Add(new StringContent(JsonSerializer.Serialize(rows)), "rows");
        }
        if (confirmedDuplicateRowNumbers is not null)
        {
            content.Add(new StringContent(JsonSerializer.Serialize(confirmedDuplicateRowNumbers)),
                "confirmedDuplicateRowNumbers");
        }
        return content;
    }

    private static MultipartFormDataContent CreateProgressCompletionContent(
        Guid operationId,
        string completionMode,
        int stageSequence,
        string targets,
        byte[]? photo,
        string photoContentType = "image/png",
        string photoFileName = "evidence.png")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(operationId.ToString("D")), "operationId");
        content.Add(new StringContent(completionMode), "completionMode");
        content.Add(new StringContent(stageSequence.ToString()), "stageSequence");
        content.Add(new StringContent(targets), "targets");
        if (photo is not null)
        {
            var photoContent = new ByteArrayContent(photo);
            photoContent.Headers.ContentType = new(photoContentType);
            content.Add(photoContent, "photos", photoFileName);
        }
        return content;
    }

    private static byte[] CreateValidPng()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateJpegWithSensitiveExif()
    {
        using var image = new Image<Rgba32>(2, 1);
        image[0, 0] = new Rgba32(255, 0, 0);
        image[1, 0] = new Rgba32(0, 128, 255);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        var jpeg = stream.ToArray();

        var make = "SyntheticCamera\0"u8.ToArray();
        var tiff = new byte[50 + make.Length];
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4, 4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8, 2), 2);
        WriteTiffEntry(tiff, 10, 0x0112, 3, 1, 6);
        WriteTiffEntry(tiff, 22, 0x010f, 2, (uint)make.Length, 50);
        make.CopyTo(tiff, 50);
        var exif = new byte[6 + tiff.Length];
        "Exif\0\0"u8.CopyTo(exif);
        tiff.CopyTo(exif, 6);

        var result = new byte[jpeg.Length + exif.Length + 4];
        jpeg.AsSpan(0, 2).CopyTo(result);
        result[2] = 0xff;
        result[3] = 0xe1;
        BinaryPrimitives.WriteUInt16BigEndian(
            result.AsSpan(4, 2), checked((ushort)(exif.Length + 2)));
        exif.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(exif.Length + 6));
        return result;
    }

    private static void WriteTiffEntry(
        byte[] tiff, int offset, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset, 2), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset + 2, 2), type);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 4, 4), count);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 8, 4), value);
    }

    private sealed class CapturingCleanUploadMalwareScanner : IUploadMalwareScanner
    {
        public List<byte[]> ScannedFiles { get; } = [];
        public UploadMalwareScanStatus Status { get; set; } = UploadMalwareScanStatus.Clean;

        public async Task<UploadMalwareScanResult> ScanAsync(
            Stream content,
            CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            ScannedFiles.Add(copy.ToArray());
            return new UploadMalwareScanResult(Status, Status.ToString());
        }
    }

    private static string QuoteIdentifier(string value) =>
        new NpgsqlCommandBuilder().QuoteIdentifier(value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "database", "migrations"))
                && Directory.Exists(Path.Combine(directory.FullName, "database", "directory-migrations")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed record PendingResponse(
        string BusinessUnitAccessStatus,
        BusinessUnitAccessEnvelope? BusinessUnitAccess = null);
    private sealed record BusinessUnitAccessEnvelope(
        string Status,
        string? SelectedBusinessUnit,
        IReadOnlyList<string> AllowedBusinessUnits,
        bool IsOverallAdministrator,
        string? ErrorCode);
    private sealed record MembershipUserResponse(
        Guid UserId,
        string AuthProvider,
        string DisplayName,
        string? AccountId,
        string? Email,
        IReadOnlyList<string> Memberships,
        bool IsOverallAdministrator,
        long AccessVersion,
        bool ApprovalPending,
        bool PendingOperationStale,
        string? PendingOperationId,
        string? PendingOperationStatus,
        IReadOnlyList<UserAccessProfileRequest> PendingProfiles);
    private sealed record MembershipBusinessUnitResponse(string Code, bool CanManage);
    private sealed record MembershipSnapshotResponse(
        IReadOnlyList<MembershipUserResponse> Users,
        IReadOnlyList<string> AvailableBusinessUnits,
        IReadOnlyList<MembershipBusinessUnitResponse> BusinessUnits);
    private sealed record MembershipUpdateResponse(
        bool Changed,
        MembershipSnapshotResponse Snapshot);
    private sealed record UserAccessUpdateResponse(
        bool Changed,
        long AccessVersion,
        MembershipSnapshotResponse Snapshot);
    private sealed record UserAccessProfileRequest(
        string BusinessUnitCode,
        Guid? DepartmentId,
        IReadOnlyList<string> RoleCodes,
        bool IsActive,
        bool IsDepartmentHead);
    private sealed record MeResponse(string DisplayName);
    private sealed record ProjectBoundaryResponse(Guid ProjectId, string ProjectTitle, string Status);

    private sealed class CountingExternalNotificationHandler : INotificationChannelHandler
    {
        private int callCount;

        public string Channel => NotificationDeliveryChannels.Mail;
        public int CallCount => Volatile.Read(ref callCount);
        public bool WillCallExternalProvider(NotificationDeliveryMessage message) => true;

        public Task<NotificationChannelResult> SendAsync(
            NotificationDeliveryMessage message,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(NotificationChannelResult.Sent("synthetic-provider"));
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class IsolationDatabaseSet : IAsyncDisposable
    {
        private readonly NpgsqlConnectionStringBuilder admin;
        private readonly IReadOnlyDictionary<(string Code, BusinessUnitConnectionPurpose Purpose), string> connections;

        private IsolationDatabaseSet(
            string repositoryRoot,
            NpgsqlConnectionStringBuilder admin,
            Dictionary<string, string?> configurationValues,
            IConfiguration configuration,
            IReadOnlyDictionary<(string Code, BusinessUnitConnectionPurpose Purpose), string> connections,
            IReadOnlyList<string> databaseNames,
            IReadOnlyList<string> runtimePasswords)
        {
            RepositoryRoot = repositoryRoot;
            this.admin = admin;
            ConfigurationValues = configurationValues;
            Configuration = configuration;
            this.connections = connections;
            DatabaseNames = databaseNames;
            RuntimePasswords = runtimePasswords;
            BusinessUnits = BusinessUnitConfiguration.Read(configuration);
        }

        public string RepositoryRoot { get; }
        public Dictionary<string, string?> ConfigurationValues { get; }
        public IConfiguration Configuration { get; }
        public BusinessUnitConfiguration BusinessUnits { get; }
        public IReadOnlyList<string> DatabaseNames { get; }
        public IReadOnlyList<string> RuntimePasswords { get; }

        public static async Task<IsolationDatabaseSet> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var admin = ReadSyntheticHarnessConnection();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var directoryDatabase = $"emi_qms_e2e_{suffix}_directory";
            var cheongjuDatabase = $"emi_qms_e2e_{suffix}_cheongju";
            var osanDatabase = $"emi_qms_e2e_{suffix}_osan";
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            var rolePrefix = $"iso_{suffix}";
            var values = BuildConfigurationValues(
                directoryDatabase,
                cheongjuDatabase,
                osanDatabase,
                $"{rolePrefix}_dir_m",
                $"{rolePrefix}_dir_r",
                $"{rolePrefix}_cj_m",
                $"{rolePrefix}_cj_r",
                $"{rolePrefix}_osan_m",
                $"{rolePrefix}_osan_r",
                admin.ConnectionString,
                password);

            var connections = BuildConnections(values);
            await using var dataSource = NpgsqlDataSource.Create(admin.ConnectionString);
            foreach (var databaseName in new[] { directoryDatabase, cheongjuDatabase, osanDatabase })
            {
                await using var command = dataSource.CreateCommand(
                    $"create database {QuoteIdentifier(databaseName)};");
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            return new IsolationDatabaseSet(
                repositoryRoot,
                admin,
                values,
                configuration,
                connections,
                [directoryDatabase, cheongjuDatabase, osanDatabase],
                connections
                    .Where(item => item.Key.Purpose == BusinessUnitConnectionPurpose.Runtime)
                    .Select(item => new NpgsqlConnectionStringBuilder(item.Value).Password!)
                    .ToList());
        }

        public static Dictionary<string, string?> BuildConfigurationValues(
            string directoryDatabase,
            string cheongjuDatabase,
            string osanDatabase,
            string directoryMigrator,
            string directoryRuntime,
            string cheongjuMigrator,
            string cheongjuRuntime,
            string osanMigrator,
            string osanRuntime,
            string adminConnectionString,
            string password)
        {
            var admin = new NpgsqlConnectionStringBuilder(adminConnectionString);
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["BusinessUnits:Enabled"] = "true",
                ["BusinessUnits:Directory:Code"] = "DIRECTORY",
                ["BusinessUnits:Directory:RuntimeConnection"] = "DirectoryRuntime",
                ["BusinessUnits:Directory:MigrationConnection"] = "DirectoryMigration",
                ["BusinessUnits:Directory:AdministratorConnection"] = "DirectoryAdmin",
                ["BusinessUnits:Directory:ExpectedDatabaseName"] = directoryDatabase,
                ["BusinessUnits:Directory:MigrationRoleName"] = directoryMigrator,
                ["BusinessUnits:Directory:RuntimeRoleName"] = directoryRuntime,
                ["BusinessUnits:Directory:ExpectedSchemaVersion"] = BusinessUnitConfiguration.DirectorySchemaVersion,
                ["BusinessUnits:Units:Cheongju:Code"] = BusinessUnitCodes.Cheongju,
                ["BusinessUnits:Units:Cheongju:RuntimeConnection"] = "CheongjuRuntime",
                ["BusinessUnits:Units:Cheongju:MigrationConnection"] = "CheongjuMigration",
                ["BusinessUnits:Units:Cheongju:AdministratorConnection"] = "CheongjuAdmin",
                ["BusinessUnits:Units:Cheongju:ExpectedDatabaseName"] = cheongjuDatabase,
                ["BusinessUnits:Units:Cheongju:MigrationRoleName"] = cheongjuMigrator,
                ["BusinessUnits:Units:Cheongju:RuntimeRoleName"] = cheongjuRuntime,
                ["BusinessUnits:Units:Cheongju:ExpectedSchemaVersion"] = BusinessUnitConfiguration.BusinessSchemaVersion,
                ["BusinessUnits:Units:Osan:Code"] = BusinessUnitCodes.Osan,
                ["BusinessUnits:Units:Osan:RuntimeConnection"] = "OsanRuntime",
                ["BusinessUnits:Units:Osan:MigrationConnection"] = "OsanMigration",
                ["BusinessUnits:Units:Osan:AdministratorConnection"] = "OsanAdmin",
                ["BusinessUnits:Units:Osan:ExpectedDatabaseName"] = osanDatabase,
                ["BusinessUnits:Units:Osan:MigrationRoleName"] = osanMigrator,
                ["BusinessUnits:Units:Osan:RuntimeRoleName"] = osanRuntime,
                ["BusinessUnits:Units:Osan:ExpectedSchemaVersion"] = BusinessUnitConfiguration.BusinessSchemaVersion,
                ["BusinessUnits:DevelopmentSeedUnits:0"] = BusinessUnitCodes.Cheongju,
                ["BusinessUnits:DevelopmentSeedUnits:1"] = BusinessUnitCodes.Osan,
                ["BusinessUnits:MembershipBackfill:ApprovedUserIdsDelimited"] = $" {AdminUserId:D};{AdminUserId:D} ",
                ["BusinessUnits:MembershipBackfill:OverallAdministratorUserIdsDelimited"] = AdminUserId.ToString("D"),
                ["DevelopmentData:SeedEnabled"] = "true",
                ["DevAuthentication:Enabled"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Notifications:Dispatch:Enabled"] = "false",
                ["Notifications:Escalation:Enabled"] = "false",
                ["Notifications:Teams:Enabled"] = "false",
                ["Notifications:Teams:DryRun"] = "true",
                ["Notifications:TeamsActivity:Enabled"] = "false",
                ["Notifications:TeamsActivity:DryRun"] = "true",
                ["Notifications:Mail:Enabled"] = "false",
                ["Notifications:Mail:DryRun"] = "true",
                ["Notifications:WebPush:Enabled"] = "false",
                ["Notifications:WebPush:DryRun"] = "true",
                ["AdminDeletionPurge:Enabled"] = "false",
                ["RateLimiting:Enabled"] = "false"
            };

            AddConnections(values, "Directory", directoryDatabase, directoryMigrator, directoryRuntime, admin, password);
            AddConnections(values, "Cheongju", cheongjuDatabase, cheongjuMigrator, cheongjuRuntime, admin, password);
            AddConnections(values, "Osan", osanDatabase, osanMigrator, osanRuntime, admin, password);
            return values;
        }

        private static void AddConnections(
            IDictionary<string, string?> values,
            string prefix,
            string database,
            string migrator,
            string runtime,
            NpgsqlConnectionStringBuilder admin,
            string password)
        {
            values[$"ConnectionStrings:{prefix}Admin"] = WithDatabase(admin, database).ConnectionString;
            values[$"ConnectionStrings:{prefix}Migration"] =
                WithRole(admin, database, migrator, $"{password}-m").ConnectionString;
            values[$"ConnectionStrings:{prefix}Runtime"] =
                WithRole(admin, database, runtime, $"{password}-r").ConnectionString;
        }

        private static Dictionary<(string Code, BusinessUnitConnectionPurpose Purpose), string> BuildConnections(
            IReadOnlyDictionary<string, string?> values)
        {
            return new Dictionary<(string Code, BusinessUnitConnectionPurpose Purpose), string>
            {
                [("DIRECTORY", BusinessUnitConnectionPurpose.Administrator)] = values["ConnectionStrings:DirectoryAdmin"]!,
                [("DIRECTORY", BusinessUnitConnectionPurpose.Migration)] = values["ConnectionStrings:DirectoryMigration"]!,
                [("DIRECTORY", BusinessUnitConnectionPurpose.Runtime)] = values["ConnectionStrings:DirectoryRuntime"]!,
                [(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator)] = values["ConnectionStrings:CheongjuAdmin"]!,
                [(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Migration)] = values["ConnectionStrings:CheongjuMigration"]!,
                [(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Runtime)] = values["ConnectionStrings:CheongjuRuntime"]!,
                [(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Administrator)] = values["ConnectionStrings:OsanAdmin"]!,
                [(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Migration)] = values["ConnectionStrings:OsanMigration"]!,
                [(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Runtime)] = values["ConnectionStrings:OsanRuntime"]!
            };
        }

        public NpgsqlConnectionStringBuilder GetBuilder(
            string code,
            BusinessUnitConnectionPurpose purpose) =>
            new(connections[(code, purpose)]);

        public async Task<NpgsqlConnection> OpenAsync(
            string code,
            BusinessUnitConnectionPurpose purpose,
            CancellationToken cancellationToken)
        {
            var connection = new NpgsqlConnection(connections[(code, purpose)]);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public async Task ExecuteAsync(
            string code,
            BusinessUnitConnectionPurpose purpose,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var connection = await OpenAsync(code, purpose, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<T> ReadScalarAsync<T>(
            string code,
            BusinessUnitConnectionPurpose purpose,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var connection = await OpenAsync(code, purpose, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Synthetic test query returned null."));
        }

        public async Task<IReadOnlyList<string>> ReadColumnAsync(
            string code,
            BusinessUnitConnectionPurpose purpose,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var connection = await OpenAsync(code, purpose, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var values = new List<string>();
            while (await reader.ReadAsync(cancellationToken)) values.Add(reader.GetString(0));
            return values;
        }

        public async ValueTask DisposeAsync()
        {
            var cleanup = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
            {
                Database = "postgres",
                Pooling = false
            };
            await using var dataSource = NpgsqlDataSource.Create(cleanup.ConnectionString);
            foreach (var databaseName in DatabaseNames)
            {
                await using var command = dataSource.CreateCommand(
                    $"drop database if exists {QuoteIdentifier(databaseName)} with (force);");
                await command.ExecuteNonQueryAsync();
            }
        }

        private static NpgsqlConnectionStringBuilder ReadSyntheticHarnessConnection()
        {
            var host = Environment.GetEnvironmentVariable("DATABASE_HOST");
            var port = Environment.GetEnvironmentVariable("DATABASE_PORT");
            var database = Environment.GetEnvironmentVariable("DATABASE_NAME");
            var username = Environment.GetEnvironmentVariable("DATABASE_USER");
            var password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
            if (string.IsNullOrWhiteSpace(host)
                || !int.TryParse(port, out var portNumber)
                || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(password)
                || !database.StartsWith("emi_qms_e2e_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Business-unit database tests require the owned isolated PostgreSQL harness.");
            }

            return new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = portNumber,
                Database = "postgres",
                Username = username,
                Password = password,
                Pooling = false,
                Timeout = 5
            };
        }

        private static NpgsqlConnectionStringBuilder WithDatabase(
            NpgsqlConnectionStringBuilder source,
            string database) =>
            new(source.ConnectionString)
            {
                Database = database,
                Pooling = false
            };

        private static NpgsqlConnectionStringBuilder WithRole(
            NpgsqlConnectionStringBuilder source,
            string database,
            string username,
            string password) =>
            new(source.ConnectionString)
            {
                Database = database,
                Username = username,
                Password = password,
                Pooling = false
            };

        private static string QuoteIdentifier(string value) =>
            new NpgsqlCommandBuilder().QuoteIdentifier(value);

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "database", "migrations"))
                    && Directory.Exists(Path.Combine(directory.FullName, "database", "directory-migrations")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = DevelopmentFeaturePolicy.TestingEnvironmentName;
        public string ApplicationName { get; set; } = "Emi.Qms.Api.Tests";
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
