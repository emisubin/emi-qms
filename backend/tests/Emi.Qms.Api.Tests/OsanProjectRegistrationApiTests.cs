using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.PanelInformation;
using Emi.Qms.Api.PanelQr;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
using ImageMagick;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZXing;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    private static readonly Guid UserId = Guid.Parse("89000000-0000-0000-0000-000000000001");

    [Fact]
    public void EndpointCatalog_ExposesAuthorizedProjectAndProgressRoutes()
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/osan/projects",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(17, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        var projectCreate = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/osan/projects/"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Post,
                StringComparer.OrdinalIgnoreCase) == true);
        Assert.Contains(
            projectCreate.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => string.Equals(authorization.Policy, QmsPolicies.ProjectCreate, StringComparison.Ordinal));
        var progressMutations = endpoints.Where(endpoint =>
            endpoint.RoutePattern.RawText == "/api/osan/projects/{projectId:guid}/progress/completions"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Post,
                StringComparer.OrdinalIgnoreCase) == true).ToArray();
        Assert.Single(progressMutations);
        Assert.All(progressMutations, endpoint => Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => string.Equals(
                authorization.Policy,
                QmsPolicies.ManufacturingUpdate,
                StringComparison.Ordinal)));
        var completion = Assert.Single(progressMutations);
        Assert.Equal(
            OsanProgressPhotoValidator.MaximumMultipartBytes,
            completion.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/osan/projects/{projectId:guid}/progress/photos/{photoId:guid}"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Get,
                StringComparer.OrdinalIgnoreCase) == true);
        Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/osan/projects/{projectId:guid}/targets/{targetId:guid}/qr"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Get,
                StringComparer.OrdinalIgnoreCase) == true);
        var excelRoutes = endpoints.Where(endpoint =>
            endpoint.RoutePattern.RawText?.StartsWith(
                "/api/osan/projects/import/",
                StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(3, excelRoutes.Length);
        Assert.All(excelRoutes, endpoint => Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => string.Equals(
                authorization.Policy,
                QmsPolicies.ProjectCreate,
                StringComparison.Ordinal)));
        Assert.All(excelRoutes.Where(endpoint =>
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Post,
                StringComparer.OrdinalIgnoreCase) == true), endpoint => Assert.Equal(
                    OsanProjectExcelParser.MaximumMultipartBytes,
                    endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize));

        var dashboard = Assert.Single(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>(), endpoint =>
                endpoint.RoutePattern.RawText == "/api/osan/dashboard"
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Get,
                    StringComparer.OrdinalIgnoreCase) == true);
        Assert.NotEmpty(dashboard.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Fact]
    public void TargetQr_PngDecodesToStableTargetPathAndConfiguredOrigin()
    {
        var projectId = Guid.Parse("89000000-0000-0000-0000-000000000099");
        var targetId = Guid.Parse("89000000-0000-0000-0000-000000000098");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qr:ScanOrigin"] = "https://qms.example.test/, https://unused.example.test"
            })
            .Build();
        var urlBuilder = new QrScanUrlBuilder(configuration, new TestWebHostEnvironment("/tmp"));
        var scanUrl = urlBuilder.BuildForPath($"/osan/qr/{projectId:D}/{targetId:D}");
        var renderer = new PanelQrRenderer();
        using var image = Image.Load<Rgba32>(renderer.RenderPng(scanUrl));
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32>
        {
            Options =
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true
            }
        };

        var decoded = reader.Decode(image);

        Assert.NotNull(decoded);
        Assert.Equal(BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
        Assert.Equal(
            $"https://qms.example.test/osan/qr/{projectId:D}/{targetId:D}",
            decoded.Text);
    }

    [Fact]
    public void TargetQr_UsesLocalhostFallbackOnlyOutsideProduction()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestWebHostEnvironment("/tmp");
        var builder = new QrScanUrlBuilder(configuration, environment);

        Assert.Equal(
            "https://localhost:5174/osan/qr/project-id",
            builder.BuildForPath("/osan/qr/project-id"));

        environment.EnvironmentName = Environments.Production;
        Assert.Throws<InvalidOperationException>(() => builder.BuildForPath("/osan/qr/project-id"));
    }

    [Theory]
    [InlineData("ftp://qms.example.test")]
    [InlineData("https://user:password@qms.example.test")]
    [InlineData("https://qms.example.test/app")]
    [InlineData("https://qms.example.test?tenant=osan")]
    [InlineData("https://qms.example.test#fragment")]
    public void TargetQr_RejectsOriginValuesThatAreNotHttpOriginOnly(string origin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Qr:ScanOrigin"] = origin })
            .Build();
        var builder = new QrScanUrlBuilder(configuration, new TestWebHostEnvironment("/tmp"));

        Assert.Throws<InvalidOperationException>(() => builder.BuildForPath("/osan/qr/project-id"));
    }

    [Fact]
    public void TargetQr_RequiresHttpsOriginInProduction()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qr:ScanOrigin"] = "http://qms.example.test"
            })
            .Build();
        var environment = new TestWebHostEnvironment("/tmp") { EnvironmentName = Environments.Production };
        var builder = new QrScanUrlBuilder(configuration, environment);

        Assert.Throws<InvalidOperationException>(() => builder.BuildForPath("/osan/qr/project-id"));
    }

    [Fact]
    public void DashboardQuery_DefaultsAndRejectsInvalidValues()
    {
        var validValues = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["search"] = "  panel  ",
            ["status"] = OsanDashboardStatuses.InProgress,
            ["page"] = "2",
            ["pageSize"] = "11",
            ["view"] = OsanDashboardViews.Home,
            ["customer"] = "  Beta Customer  "
        });
        var (query, errors) = OsanProjectEndpointExtensions.ParseDashboardQuery(validValues);
        Assert.Empty(errors);
        Assert.Equal(new OsanDashboardQuery(
            "panel", OsanDashboardStatuses.InProgress, 2, 11, OsanDashboardViews.Home, "Beta Customer"), query);

        var (defaults, defaultErrors) = OsanProjectEndpointExtensions.ParseDashboardQuery(
            new QueryCollection());
        Assert.Empty(defaultErrors);
        Assert.Equal(new OsanDashboardQuery(string.Empty, OsanDashboardStatuses.All, 1, 10), defaults);

        var (invalid, invalidErrors) = OsanProjectEndpointExtensions.ParseDashboardQuery(
            new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["customer"] = new string('c', 201),
                ["search"] = new string('a', 201),
                ["status"] = "Paused",
                ["page"] = "0",
                ["pageSize"] = "101",
                ["view"] = "archive"
            }));
        Assert.Null(invalid);
        Assert.Equal(["customer", "search", "status", "page", "pageSize", "view"], invalidErrors.Keys);
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
    public async Task ExcelParser_TemplatePreservesTextAndRejectsUnsafeOrLossyWorkbooks()
    {
        var parser = new OsanProjectExcelParser();
        var template = parser.CreateTemplate();
        using (var workbook = new XLWorkbook(new MemoryStream(template)))
        {
            var sheet = Assert.Single(workbook.Worksheets);
            Assert.Equal(
                ["장비명 *", "프로젝트 코드 *", "part 분류 *", "수량 *", "고객사 *", "PO No", "W/O No", "납기일 *"],
                Enumerable.Range(1, 8).Select(column => sheet.Cell(3, column).GetString()));
            Assert.DoesNotContain(sheet.RowsUsed(), row => row.RowNumber() > 3);
        }

        var valid = CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            workbook.Worksheet(1).Cell(1, 3).Value = "고객사";
            workbook.Worksheet(1).Cell(1, 7).Value = "part 분류";
            AddExcelRow(workbook.Worksheet(1), 2, "  Title  Case ", " 00Ab  01 ", " Customer ",
                " 001-PO ", " 000-W/O ", new DateOnly(2026, 12, 31), " Product  X ", 2);
        });
        var parsed = await parser.ParseAsync(Upload("valid.xlsx", valid), TestContext.Current.CancellationToken);
        Assert.Empty(parsed.Errors);
        var parsedRow = Assert.Single(parsed.Rows);
        Assert.Equal("00Ab  01", parsedRow.ProjectCode);
        Assert.Equal("001-PO", parsedRow.PoNumber);
        Assert.Equal("000-W/O", parsedRow.WorkOrderNumber);
        Assert.Equal("Title  Case", parsedRow.Title);

        var fractional = CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            foreach (var (row, value) in new[] { (2, 1.4), (3, -0.4), (4, 500.4) })
            {
                AddExcelRow(workbook.Worksheet(1), row, $"Fraction {row}", $"FRACTION-{row}", "Customer",
                    null, null, new DateOnly(2026, 12, 31), "Product", value);
                workbook.Worksheet(1).Cell(row, 8).Style.NumberFormat.Format = "0";
            }
        });
        var fractionalParsed = await parser.ParseAsync(
            Upload("fractional.xlsx", fractional), TestContext.Current.CancellationToken);
        Assert.All(fractionalParsed.Rows, row => Assert.Contains("수량은 정수여야 합니다.", row.Errors));

        var formula = CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            AddExcelRow(workbook.Worksheet(1), 2, "Formula", "FORMULA-1", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", 1);
            workbook.Worksheet(1).Cell(2, 8).FormulaA1 = "=1";
        });
        Assert.Contains("Excel Formula는 사용할 수 없습니다.",
            (await parser.ParseAsync(Upload("formula.xlsx", formula), TestContext.Current.CancellationToken)).Errors);

        var external = AddExternalRelationship(valid);
        Assert.Contains("외부 링크가 포함된 Excel은 업로드할 수 없습니다.",
            (await parser.ParseAsync(Upload("external.xlsx", external), TestContext.Current.CancellationToken)).Errors);

        var hiddenLarge = CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            AddExcelRow(workbook.Worksheet(1), 2, "Visible", "VISIBLE-1", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", 1);
            var hidden = workbook.AddWorksheet("Hidden");
            hidden.Visibility = XLWorksheetVisibility.Hidden;
            for (var row = 1; row <= 10001; row++) hidden.Cell(row, 1).Value = row;
        });
        var relocatedHiddenLarge = RelocateWorksheetPart(
            hiddenLarge,
            "xl/worksheets/sheet2.xml",
            "xl/extra/hidden.dat");
        Assert.Contains("Excel 사용 범위가 허용값을 초과했습니다.",
            (await parser.ParseAsync(
                Upload("hidden.xlsx", relocatedHiddenLarge), TestContext.Current.CancellationToken)).Errors);

        var hugeQuantities = CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            AddExcelRow(workbook.Worksheet(1), 2, "Huge A", "HUGE-A", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", int.MaxValue);
            AddExcelRow(workbook.Worksheet(1), 3, "Huge B", "HUGE-B", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", int.MaxValue);
        });
        var preview = await new OsanProjectStore(
                new DatabaseConnectionStringProvider(new ConfigurationBuilder().Build()), parser)
            .PreviewExcelAsync(Upload("huge.xlsx", hugeQuantities), TestContext.Current.CancellationToken);
        Assert.Equal(0, preview.TotalQuantity);
        Assert.True(preview.ErrorCount > 0);
        Assert.All(preview.Rows, row => Assert.Contains("quantity", row.FieldErrors!.Keys));
    }

    [Fact]
    public async Task ExcelStore_AppliesWholeBatchReplaysAndSerializesReversedCodeCompetition()
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
            values ('{UserId:D}', 'osan-excel-test', 'Osan Excel Test',
                    '89000000-0000-0000-0000-000000000010', true);
            """, TestContext.Current.CancellationToken);

        var parser = new OsanProjectExcelParser();
        var store = new OsanProjectStore(provider, parser);
        var file = Upload("batch.xlsx", CreateBatchExcel([("Excel A", "00Aa  01", 2), ("Excel B", "Bb-002", 3)]));
        var preview = await store.PreviewExcelAsync(file, TestContext.Current.CancellationToken);
        Assert.Equal(2, preview.TotalRowCount);
        Assert.Equal(5, preview.TotalQuantity);
        Assert.Equal(0, preview.ErrorCount);
        Assert.Equal(["00Aa  01", "Bb-002"], preview.Rows.Select(row => row.ProjectCode));

        var operationId = Guid.NewGuid();
        var created = await store.ApplyExcelAsync(
            file, file.FileSha256, operationId, UserId, TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.Success, created.Status);
        Assert.False(created.Value!.Replayed);
        Assert.Equal(2, created.Value.CreatedCount);
        Assert.Equal(5L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_targets;", TestContext.Current.CancellationToken));
        Assert.Equal(35L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_target_steps;", TestContext.Current.CancellationToken));
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_events where event_type='ProjectCreated';",
            TestContext.Current.CancellationToken));
        var firstDetail = await store.GetAsync(created.Value.ProjectIds[0], TestContext.Current.CancellationToken);
        Assert.NotNull(firstDetail);
        Assert.Equal(2, firstDetail.Quantity);
        Assert.Equal(2, firstDetail.Targets.Count);
        Assert.All(firstDetail.Targets, target => Assert.Equal(7, target.Steps.Count));

        var replayed = await store.ApplyExcelAsync(
            file, file.FileSha256, operationId, UserId, TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.Success, replayed.Status);
        Assert.True(replayed.Value!.Replayed);
        Assert.Equal(created.Value.ProjectIds, replayed.Value.ProjectIds);

        var narrowedReplay = await store.ApplyExcelAsync(
            file,
            file.FileSha256,
            operationId,
            UserId,
            [ToExcelRowRequest(preview.Rows[0])],
            new HashSet<int>(),
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.OperationConflict, narrowedReplay.Status);

        var changed = Upload("changed.xlsx", CreateBatchExcel([("Changed", "CHANGED-1", 1)]));
        Assert.Equal(OsanProjectExcelApplyStatus.FileChanged,
            (await store.ApplyExcelAsync(changed, file.FileSha256, Guid.NewGuid(), UserId,
                TestContext.Current.CancellationToken)).Status);
        Assert.Equal(OsanProjectExcelApplyStatus.OperationConflict,
            (await store.ApplyExcelAsync(changed, changed.FileSha256, operationId, UserId,
                TestContext.Current.CancellationToken)).Status);

        var duplicateFile = Upload("duplicate.xlsx", CreateBatchExcel([("Dup A", "DUP-1", 1), ("Dup B", "DUP-1", 1)]));
        var duplicatePreview = await store.PreviewExcelAsync(duplicateFile, TestContext.Current.CancellationToken);
        Assert.Equal(0, duplicatePreview.ErrorCount);
        Assert.All(duplicatePreview.Rows, row => Assert.Equal("code", row.DuplicateKind));
        var duplicateOperationId = Guid.NewGuid();
        var confirmationRequired = await store.ApplyExcelAsync(
            duplicateFile,
            duplicateFile.FileSha256,
            duplicateOperationId,
            UserId,
            duplicatePreview.Rows.Select(ToExcelRowRequest).ToArray(),
            new HashSet<int>(),
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.ConfirmationRequired, confirmationRequired.Status);
        Assert.Equal([2, 3], confirmationRequired.ConfirmationRowNumbers);
        var confirmedDuplicate = await store.ApplyExcelAsync(
            duplicateFile,
            duplicateFile.FileSha256,
            duplicateOperationId,
            UserId,
            duplicatePreview.Rows.Select(ToExcelRowRequest).ToArray(),
            new HashSet<int> { 2, 3 },
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.Success, confirmedDuplicate.Status);
        Assert.Equal([2, 3], confirmedDuplicate.Value!.CreatedRowNumbers);
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='DUP-1';",
            TestContext.Current.CancellationToken));

        var existing = Upload("existing.xlsx", CreateBatchExcel([("Existing", "00Aa  01", 1)]));
        var existingPreview = await store.PreviewExcelAsync(existing, TestContext.Current.CancellationToken);
        Assert.Equal(0, existingPreview.ErrorCount);
        Assert.Equal("code", Assert.Single(existingPreview.Rows).DuplicateKind);
        var identical = Upload("identical.xlsx", CreateBatchExcel([("Excel A", "00Aa  01", 2)]));
        Assert.Equal("identical", Assert.Single((await store.PreviewExcelAsync(
            identical,
            TestContext.Current.CancellationToken)).Rows).DuplicateKind);

        var invalid = Upload("invalid.xlsx", CreateOsanExcel(workbook =>
        {
            AddExcelHeaders(workbook.Worksheet(1));
            AddExcelRow(workbook.Worksheet(1), 2, "Valid", "ATOMIC-VALID", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", 1);
            AddExcelRow(workbook.Worksheet(1), 3, " ", "ATOMIC-INVALID", "Customer", null, null,
                new DateOnly(2026, 12, 31), "Product", 1);
        }));
        Assert.Equal(OsanProjectExcelApplyStatus.Validation,
            (await store.ApplyExcelAsync(invalid, invalid.FileSha256, Guid.NewGuid(), UserId,
                TestContext.Current.CancellationToken)).Status);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code like 'ATOMIC-%';",
            TestContext.Current.CancellationToken));

        var editable = Upload("editable.xlsx", CreateBatchExcel([
            ("Edit A", "EDIT-A", 1),
            ("Edit B", "EDIT-B", 1),
            ("Edit C", "EDIT-C", 1),
            ("Edit D", "EDIT-D", 1)
        ]));
        var editedRows = new[]
        {
            new OsanProjectExcelRowRequest(2, "Edited  Title", "00Edit  A", "Customer", "001-PO", null,
                "2027-01-02", "Edited Product", 2),
            new OsanProjectExcelRowRequest(3, " ", "EDIT-B", "Customer", null, null,
                "2027-01-03", "Product", 1)
        };
        var editedPreview = await store.PreviewExcelAsync(
            editable,
            editedRows,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, editedPreview.Rows.Count);
        Assert.Empty(editedPreview.Rows[0].Errors);
        Assert.Equal("Edited  Title", editedPreview.Rows[0].Title);
        Assert.Contains("title", editedPreview.Rows[1].FieldErrors!.Keys);
        var rawInvalidPreview = await store.PreviewExcelAsync(
            editable,
            [
                editedRows[0],
                editedRows[1] with { Title = "Invalid date", DeliveryDate = "", Quantity = 1 },
                new OsanProjectExcelRowRequest(4, "Invalid date", "EDIT-C", "Customer", null, null,
                    "2027-13-40", "Product", 1),
                new OsanProjectExcelRowRequest(5, "Invalid quantity", "EDIT-D", "Customer", null, null,
                    "2027-01-04", "Product", 1.4m)
            ],
            TestContext.Current.CancellationToken);
        Assert.Equal(2, rawInvalidPreview.TotalQuantity);
        Assert.Empty(rawInvalidPreview.Rows[0].Errors);
        Assert.Contains("deliveryDate", rawInvalidPreview.Rows[1].FieldErrors!.Keys);
        Assert.Contains("deliveryDate", rawInvalidPreview.Rows[2].FieldErrors!.Keys);
        Assert.Contains("quantity", rawInvalidPreview.Rows[3].FieldErrors!.Keys);
        var partial = await store.ApplyExcelAsync(
            editable,
            editable.FileSha256,
            Guid.NewGuid(),
            UserId,
            [editedRows[0]],
            new HashSet<int>(),
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectExcelApplyStatus.Success, partial.Status);
        Assert.Equal([2], partial.Value!.CreatedRowNumbers);
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code='00Edit  A' and project_title='Edited  Title';",
            TestContext.Current.CancellationToken));
        var remainingPreview = await store.PreviewExcelAsync(
            editable,
            [editedRows[1] with { Title = "Fixed B" }],
            TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.Single(remainingPreview.Rows).RowNumber);
        Assert.Equal(0, remainingPreview.ErrorCount);

        var reversedA = Upload("reverse-a.xlsx", CreateBatchExcel([("R A", "LOCK-A", 1), ("R B", "LOCK-B", 1)]));
        var reversedB = Upload("reverse-b.xlsx", CreateBatchExcel([("R B", "LOCK-B", 1), ("R A", "LOCK-A", 1)]));
        var reverseOperationA = Guid.NewGuid();
        var reverseOperationB = Guid.NewGuid();
        var competing = await Task.WhenAll(
            store.ApplyExcelAsync(reversedA, reversedA.FileSha256, reverseOperationA, UserId,
                TestContext.Current.CancellationToken),
            store.ApplyExcelAsync(reversedB, reversedB.FileSha256, reverseOperationB, UserId,
                TestContext.Current.CancellationToken));
        Assert.Single(competing, result => result.Status == OsanProjectExcelApplyStatus.Success);
        Assert.Single(competing, result => result.Status == OsanProjectExcelApplyStatus.ConfirmationRequired);
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code in ('LOCK-A','LOCK-B');",
            TestContext.Current.CancellationToken));
        var retryFile = competing[0].Status == OsanProjectExcelApplyStatus.ConfirmationRequired
            ? reversedA
            : reversedB;
        var retryOperation = competing[0].Status == OsanProjectExcelApplyStatus.ConfirmationRequired
            ? reverseOperationA
            : reverseOperationB;
        Assert.Equal(OsanProjectExcelApplyStatus.Success, (await store.ApplyExcelAsync(
            retryFile,
            retryFile.FileSha256,
            retryOperation,
            UserId,
            null,
            new HashSet<int> { 2, 3 },
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(4L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code in ('LOCK-A','LOCK-B');",
            TestContext.Current.CancellationToken));

        var writeCountsBefore = await database.ReadScalarAsync<long>(
            """
            select (select count(*) from projects)
                 + (select count(*) from osan_project_targets)
                 + (select count(*) from osan_project_target_steps)
                 + (select count(*) from user_project_access)
                 + (select count(*) from osan_project_events)
                 + (select count(*) from osan_project_create_operations);
            """,
            TestContext.Current.CancellationToken);
        await database.ExecuteAsync(
            """
            create function fail_second_osan_excel_project_for_test()
            returns trigger language plpgsql as $$
            begin
                if new.project_code = 'MID-B' then
                    raise exception 'synthetic_second_excel_project_failure';
                end if;
                return new;
            end $$;
            create trigger trg_fail_second_osan_excel_project_for_test
            before insert on projects
            for each row execute function fail_second_osan_excel_project_for_test();
            """,
            TestContext.Current.CancellationToken);
        var middleFailureOperationId = Guid.NewGuid();
        var middleFailure = Upload(
            "middle-failure.xlsx",
            CreateBatchExcel([("Middle A", "MID-A", 1), ("Middle B", "MID-B", 1)]));
        var middleFailureException = await Assert.ThrowsAsync<PostgresException>(() => store.ApplyExcelAsync(
            middleFailure,
            middleFailure.FileSha256,
            middleFailureOperationId,
            UserId,
            TestContext.Current.CancellationToken));
        Assert.Contains("synthetic_second_excel_project_failure", middleFailureException.MessageText, StringComparison.Ordinal);
        Assert.Equal(writeCountsBefore, await database.ReadScalarAsync<long>(
            """
            select (select count(*) from projects)
                 + (select count(*) from osan_project_targets)
                 + (select count(*) from osan_project_target_steps)
                 + (select count(*) from user_project_access)
                 + (select count(*) from osan_project_events)
                 + (select count(*) from osan_project_create_operations);
            """,
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from projects where project_code in ('MID-A','MID-B');",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_create_operations where operation_id=@operation_id;",
            TestContext.Current.CancellationToken,
            ("operation_id", middleFailureOperationId)));
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
        Assert.Equal("NotStarted", created.Value.Project.Status);
        Assert.Equal(0, created.Value.Project.CompletedStepCount);
        Assert.Equal(21, created.Value.Project.TotalStepCount);
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

        await database.ExecuteAsync(
            "update projects set deleted_at_utc=now() where id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId));
        var softDeletedCodeConflict = await store.CreateAsync(
            Normalize(ValidRequest(title: "Deleted code remains reserved", projectCode: "OSAN-001")),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProjectCreateStatus.ProjectCodeConflict, softDeletedCodeConflict.Status);
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

    [Fact]
    public async Task ProjectViews_ProjectLegacyStartOnlyHistoryFromCompletedStepCountsWithoutMutation()
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
            values ('{UserId:D}', 'osan-legacy-start-test', 'Osan Legacy Start Test',
                    '89000000-0000-0000-0000-000000000010', true);
            """, TestContext.Current.CancellationToken);

        var projectStore = new OsanProjectStore(provider);
        var created = await projectStore.CreateAsync(
            Normalize(ValidRequest(projectCode: "OSAN-LEGACY-START")),
            UserId,
            TestContext.Current.CancellationToken);
        var projectId = created.Value!.Project.ProjectId;
        var targetId = created.Value.Project.Targets[0].TargetId;
        var operationId = Guid.NewGuid();
        await database.ExecuteAsync($"""
            update osan_project_targets
            set status='InProgress', version=2, started_at_utc='2026-09-01T00:00:00Z',
                started_by_user_id='{UserId:D}'
            where id='{targetId:D}';
            update osan_project_target_steps
            set status='InProgress', started_at_utc='2026-09-01T00:00:00Z'
            where target_id='{targetId:D}' and sequence_number=1;
            insert into osan_progress_operations (
                operation_id, project_id, action, completion_mode, stage_sequence,
                target_ids, request_fingerprint, requested_by_user_id)
            values (
                '{operationId:D}', '{projectId:D}', 'Start', null, null,
                array['{targetId:D}'::uuid], repeat('0', 64), '{UserId:D}');
            """, TestContext.Current.CancellationToken);

        var sourceState = await database.ReadScalarAsync<string>(
            """
            select concat_ws(':', target.status, target.version,
                target.started_at_utc is not null, target.started_by_user_id,
                step.status, step.started_at_utc is not null,
                (select count(*) from osan_progress_operations operation
                 where operation.project_id=target.project_id and operation.action='Start'))
            from osan_project_targets target
            join osan_project_target_steps step on step.target_id=target.id and step.sequence_number=1
            where target.id=@target_id;
            """,
            TestContext.Current.CancellationToken,
            ("target_id", targetId));

        var listItem = Assert.Single((await projectStore.ListAsync(
            new ProjectAccessScope(true, []),
            TestContext.Current.CancellationToken)).Items);
        var detail = await projectStore.GetAsync(projectId, TestContext.Current.CancellationToken);
        var progress = await new OsanProgressStore(provider).GetAsync(
            projectId,
            TestContext.Current.CancellationToken);

        Assert.Equal("NotStarted", listItem.Status);
        Assert.Equal(0, listItem.CompletedStepCount);
        Assert.Equal(7, listItem.TotalStepCount);
        Assert.NotNull(detail);
        Assert.Equal("NotStarted", detail.Status);
        Assert.Equal("NotStarted", Assert.Single(detail.Targets).Status);
        Assert.Equal(0, detail.CompletedStepCount);
        Assert.Equal(7, detail.TotalStepCount);
        Assert.NotNull(progress);
        Assert.Equal("NotStarted", progress.Status);
        Assert.Equal("NotStarted", Assert.Single(progress.Targets).Status);
        Assert.Equal(0, progress.CompletedStepCount);
        Assert.Equal(7, progress.TotalStepCount);
        Assert.Equal(sourceState, await database.ReadScalarAsync<string>(
            """
            select concat_ws(':', target.status, target.version,
                target.started_at_utc is not null, target.started_by_user_id,
                step.status, step.started_at_utc is not null,
                (select count(*) from osan_progress_operations operation
                 where operation.project_id=target.project_id and operation.action='Start'))
            from osan_project_targets target
            join osan_project_target_steps step on step.target_id=target.id and step.sequence_number=1
            where target.id=@target_id;
            """,
            TestContext.Current.CancellationToken,
            ("target_id", targetId)));
    }

    [Fact]
    public async Task DashboardStore_AggregatesOnlyScopedSearchAndStatusAcrossPages()
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
            values ('{UserId:D}', 'osan-dashboard-test', 'Osan Dashboard Test',
                    '89000000-0000-0000-0000-000000000010', true);
            """, TestContext.Current.CancellationToken);

        var projectStore = new OsanProjectStore(provider);
        var notStarted = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Not started panel",
                projectCode: "DASH-002",
                customerName: "고객 AB",
                deliveryDate: new DateOnly(2026, 10, 2))),
            UserId,
            TestContext.Current.CancellationToken);
        var partial = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Partial panel",
                projectCode: "DASH-001",
                customerName: "고객 A",
                poNumber: "PO-FIND-ME",
                quantity: 2,
                deliveryDate: new DateOnly(2026, 10, 1))),
            UserId,
            TestContext.Current.CancellationToken);
        var completed = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Completed panel",
                projectCode: "DASH-003",
                workOrderNumber: "WO-COMPLETE",
                deliveryDate: new DateOnly(2026, 9, 8))),
            UserId,
            TestContext.Current.CancellationToken);
        var hidden = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "LEAKTOKEN hidden",
                projectCode: "DASH-HIDDEN",
                customerName: "Secret Customer",
                poNumber: "LEAKTOKEN",
                deliveryDate: new DateOnly(2026, 8, 1))),
            UserId,
            TestContext.Current.CancellationToken);
        var pastUnfinished = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Past unfinished",
                projectCode: "HOME-PAST-ACTIVE",
                deliveryDate: new DateOnly(2026, 9, 8))),
            UserId,
            TestContext.Current.CancellationToken);
        var todayCompleted = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Today completed",
                projectCode: "HOME-TODAY-DONE",
                deliveryDate: new DateOnly(2026, 9, 9))),
            UserId,
            TestContext.Current.CancellationToken);
        var futureCompleted = await projectStore.CreateAsync(
            Normalize(ValidRequest(
                title: "Future completed",
                projectCode: "HOME-FUTURE-DONE",
                deliveryDate: new DateOnly(2026, 9, 10))),
            UserId,
            TestContext.Current.CancellationToken);
        var cheongjuId = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            insert into projects (
                id, project_key, project_number, name, customer_name, item, project_code,
                project_title, delivery_date, status, created_by_user_id, updated_at_utc,
                project_profile)
            values (
                @project_id, @project_key, @project_number, 'MIXEDPROFILE project',
                'Cheongju Customer', '', @project_code, 'MIXEDPROFILE project',
                '2026-07-01', 'Active', @user_id, now(), 'Cheongju');
            """,
            TestContext.Current.CancellationToken,
            ("project_id", cheongjuId),
            ("project_key", $"cheongju-{cheongjuId:N}"),
            ("project_number", $"CJ-{cheongjuId:N}"),
            ("project_code", $"CJ-{cheongjuId:N}"),
            ("user_id", UserId));

        var progressStore = new OsanProgressStore(provider);
        var partialTargets = partial.Value!.Project.Targets.Select(target => target.TargetId).ToArray();
        var partialResult = await progressStore.CompleteAsync(
            partial.Value.Project.ProjectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Batch,
                1,
                partialTargets.Select(id => new OsanProgressTargetRequest(id, 1)).ToArray(),
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, partialResult.Status);

        async Task CompleteProjectAsync(OsanProjectCreateResponse value)
        {
            var targetId = Assert.Single(value.Project.Targets).TargetId;
            var version = 1;
            foreach (var stage in Enumerable.Range(1, 7))
            {
                var result = await progressStore.CompleteAsync(
                    value.Project.ProjectId,
                    new CompleteOsanProgressInput(
                        Guid.NewGuid(),
                        OsanCompletionModes.Individual,
                        stage,
                        [new OsanProgressTargetRequest(targetId, version)],
                        []),
                    UserId,
                    TestContext.Current.CancellationToken);
                Assert.Equal(OsanProgressMutationStatus.Success, result.Status);
                version += 1;
            }
        }
        await CompleteProjectAsync(completed.Value!);
        await CompleteProjectAsync(todayCompleted.Value!);
        await CompleteProjectAsync(futureCompleted.Value!);

        var visibleIds = new[]
        {
            notStarted.Value!.Project.ProjectId,
            partial.Value.Project.ProjectId,
            completed.Value!.Project.ProjectId
        };
        var scope = new ProjectAccessScope(
            false,
            visibleIds.Select(id => $"osan-{id:N}").ToArray());
        var store = new OsanDashboardStore(
            provider,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 8, 15, 0, 0, TimeSpan.Zero)));

        var firstPage = await store.GetAsync(
            new OsanDashboardQuery(string.Empty, OsanDashboardStatuses.All, 1, 2),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(new OsanDashboardSummaryResponse(3, 1, 1, 1), firstPage.Summary);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(["Customer", "고객 A", "고객 AB"], firstPage.Customers);
        Assert.DoesNotContain("Secret Customer", firstPage.Customers!);
        Assert.Equal([partial.Value.Project.ProjectId, notStarted.Value.Project.ProjectId],
            firstPage.Items.Select(item => item.ProjectId));
        var partialItem = firstPage.Items[0];
        Assert.Equal(2, partialItem.CompletedStepCount);
        Assert.Equal(14, partialItem.TotalStepCount);
        Assert.Equal(14, partialItem.ProgressPercent);
        Assert.Equal(7, partialItem.Stages.Count);
        var partialStage = Assert.Single(partialItem.Stages, stage => stage.SequenceNumber == 1);
        Assert.Equal(2, partialStage.CompletedTargetCount);
        Assert.Equal(2, partialStage.TotalTargetCount);

        var secondPage = await store.GetAsync(
            new OsanDashboardQuery(string.Empty, OsanDashboardStatuses.All, 2, 2),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(firstPage.Summary, secondPage.Summary);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(firstPage.Customers, secondPage.Customers);
        var completedItem = Assert.Single(secondPage.Items);
        Assert.Equal(completed.Value.Project.ProjectId, completedItem.ProjectId);
        Assert.Equal(7, completedItem.CompletedStepCount);
        Assert.Equal(7, completedItem.TotalStepCount);
        Assert.Equal(100, completedItem.ProgressPercent);
        Assert.All(completedItem.Stages, stage =>
        {
            Assert.Equal(1, stage.CompletedTargetCount);
            Assert.Equal(1, stage.TotalTargetCount);
        });

        var filtered = await store.GetAsync(
            new OsanDashboardQuery(string.Empty, OsanDashboardStatuses.InProgress, 1, 10),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(firstPage.Summary, filtered.Summary);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(partial.Value.Project.ProjectId, Assert.Single(filtered.Items).ProjectId);

        var searched = await store.GetAsync(
            new OsanDashboardQuery("PO-FIND-ME", OsanDashboardStatuses.All, 1, 10),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(new OsanDashboardSummaryResponse(1, 0, 1, 0), searched.Summary);
        Assert.Equal(partial.Value.Project.ProjectId, Assert.Single(searched.Items).ProjectId);

        var customerFiltered = await store.GetAsync(
            new OsanDashboardQuery(
                "PO-FIND-ME",
                OsanDashboardStatuses.InProgress,
                1,
                1,
                OsanDashboardViews.Progress,
                "고객 A"),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(new OsanDashboardSummaryResponse(1, 0, 1, 0), customerFiltered.Summary);
        Assert.Equal(1, customerFiltered.TotalCount);
        Assert.Equal(partial.Value.Project.ProjectId, Assert.Single(customerFiltered.Items).ProjectId);
        Assert.Equal(firstPage.Customers, customerFiltered.Customers);

        var nonExactCustomer = await store.GetAsync(
            new OsanDashboardQuery(
                string.Empty,
                OsanDashboardStatuses.All,
                1,
                10,
                OsanDashboardViews.Progress,
                "고객"),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, nonExactCustomer.Summary.TotalCount);
        Assert.Equal(0, nonExactCustomer.TotalCount);
        Assert.Empty(nonExactCustomer.Items);
        Assert.Equal(firstPage.Customers, nonExactCustomer.Customers);

        var noLeak = await store.GetAsync(
            new OsanDashboardQuery("LEAKTOKEN", OsanDashboardStatuses.All, 1, 10),
            scope,
            TestContext.Current.CancellationToken);
        Assert.Equal(new OsanDashboardSummaryResponse(0, 0, 0, 0), noLeak.Summary);
        Assert.Equal(0, noLeak.TotalCount);
        Assert.Empty(noLeak.Items);
        Assert.NotEqual(hidden.Value!.Project.ProjectId, partial.Value.Project.ProjectId);

        var noCheongjuLeak = await store.GetAsync(
            new OsanDashboardQuery("MIXEDPROFILE", OsanDashboardStatuses.All, 1, 10),
            new ProjectAccessScope(true, []),
            TestContext.Current.CancellationToken);
        Assert.Equal(0, noCheongjuLeak.Summary.TotalCount);
        Assert.Equal(0, noCheongjuLeak.TotalCount);
        Assert.Empty(noCheongjuLeak.Items);
        Assert.DoesNotContain("Cheongju Customer", noCheongjuLeak.Customers!);

        var emptyScope = await store.GetAsync(
            new OsanDashboardQuery(string.Empty, OsanDashboardStatuses.All, 1, 10),
            new ProjectAccessScope(false, []),
            TestContext.Current.CancellationToken);
        Assert.Equal(0, emptyScope.Summary.TotalCount);
        Assert.Equal(0, emptyScope.TotalCount);
        Assert.Empty(emptyScope.Items);
        Assert.Empty(emptyScope.Customers!);

        var homeScope = new ProjectAccessScope(
            false,
            new[]
            {
                notStarted.Value.Project.ProjectId,
                partial.Value.Project.ProjectId,
                completed.Value.Project.ProjectId,
                pastUnfinished.Value!.Project.ProjectId,
                todayCompleted.Value!.Project.ProjectId,
                futureCompleted.Value!.Project.ProjectId
            }.Select(id => $"osan-{id:N}").ToArray());
        var beforeKoreaMidnight = await new OsanDashboardStore(
            provider,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 8, 14, 59, 0, TimeSpan.Zero)))
            .GetAsync(
                new OsanDashboardQuery(
                    string.Empty, OsanDashboardStatuses.All, 1, 10, OsanDashboardViews.Home),
                new ProjectAccessScope(
                    false,
                    [$"osan-{completed.Value!.Project.ProjectId:N}"]),
                TestContext.Current.CancellationToken);
        Assert.Equal(completed.Value!.Project.ProjectId, Assert.Single(beforeKoreaMidnight.Items).ProjectId);

        var homeFirstPage = await store.GetAsync(
            new OsanDashboardQuery(
                string.Empty, OsanDashboardStatuses.All, 1, 2, OsanDashboardViews.Home),
            homeScope,
            TestContext.Current.CancellationToken);
        Assert.Equal(new OsanDashboardSummaryResponse(5, 2, 1, 2), homeFirstPage.Summary);
        Assert.Equal(5, homeFirstPage.TotalCount);
        Assert.Equal(
            [pastUnfinished.Value.Project.ProjectId, todayCompleted.Value.Project.ProjectId],
            homeFirstPage.Items.Select(item => item.ProjectId));

        var homeSecondPage = await store.GetAsync(
            new OsanDashboardQuery(
                string.Empty, OsanDashboardStatuses.All, 2, 2, OsanDashboardViews.Home),
            homeScope,
            TestContext.Current.CancellationToken);
        Assert.Equal(homeFirstPage.Summary, homeSecondPage.Summary);
        Assert.Equal(
            [futureCompleted.Value.Project.ProjectId, partial.Value.Project.ProjectId],
            homeSecondPage.Items.Select(item => item.ProjectId));

        var homeThirdPage = await store.GetAsync(
            new OsanDashboardQuery(
                string.Empty, OsanDashboardStatuses.All, 3, 2, OsanDashboardViews.Home),
            homeScope,
            TestContext.Current.CancellationToken);
        Assert.Equal(notStarted.Value.Project.ProjectId, Assert.Single(homeThirdPage.Items).ProjectId);

        var homeCompleted = await store.GetAsync(
            new OsanDashboardQuery(
                string.Empty, OsanDashboardStatuses.Completed, 1, 10, OsanDashboardViews.Home),
            homeScope,
            TestContext.Current.CancellationToken);
        Assert.Equal(homeFirstPage.Summary, homeCompleted.Summary);
        Assert.Equal(2, homeCompleted.TotalCount);
        Assert.Equal(
            [todayCompleted.Value.Project.ProjectId, futureCompleted.Value.Project.ProjectId],
            homeCompleted.Items.Select(item => item.ProjectId));
    }

    [Fact]
    public async Task ProgressStore_EnforcesAtomicStaleReplayPhotoAndLastPackingContracts()
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
            values ('{UserId:D}', 'osan-progress-test', 'Osan Progress Test',
                    '89000000-0000-0000-0000-000000000010', true);
            """, TestContext.Current.CancellationToken);

        var projectStore = new OsanProjectStore(provider);
        var created = await projectStore.CreateAsync(
            Normalize(ValidRequest(projectCode: "OSAN-PROGRESS", quantity: 2)),
            UserId,
            TestContext.Current.CancellationToken);
        var projectId = created.Value!.Project.ProjectId;
        var targetIds = created.Value.Project.Targets.Select(target => target.TargetId).ToArray();
        var progressStore = new OsanProgressStore(provider);

        var staleOperation = Guid.NewGuid();
        var stale = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                staleOperation,
                OsanCompletionModes.Batch,
                1,
                [
                    new OsanProgressTargetRequest(targetIds[0], 1),
                    new OsanProgressTargetRequest(targetIds[1], 2)
                ],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, stale.Status);
        Assert.Equal("osan_progress_stale_version", stale.ErrorCode);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_target_steps where project_id=@project_id and status='Completed';",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_operations where operation_id=@operation_id;",
            TestContext.Current.CancellationToken,
            ("operation_id", staleOperation)));

        var skipPredecessor = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Batch,
                2,
                [new OsanProgressTargetRequest(targetIds[0], 1)],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, skipPredecessor.Status);
        Assert.Equal("osan_progress_prerequisite_incomplete", skipPredecessor.ErrorCode);
        var initialProgress = await progressStore.GetAsync(
            projectId,
            TestContext.Current.CancellationToken);
        Assert.NotNull(initialProgress);
        Assert.All(initialProgress.Targets, target =>
        {
            Assert.True(target.Steps[0].CanCompleteIndividual);
            Assert.True(target.Steps[0].CanCompleteBatch);
            Assert.All(target.Steps.Skip(1), step =>
            {
                Assert.False(step.CanCompleteIndividual);
                Assert.False(step.CanCompleteBatch);
            });
        });

        var png = CreateStructurallyValidPng();
        var (photo, photoError) = await OsanProgressPhotoValidator.ValidateAsync(
            "evidence.png",
            "image/png",
            png,
            TestContext.Current.CancellationToken);
        Assert.Null(photoError);
        Assert.NotNull(photo);
        var stageOneOperation = Guid.NewGuid();
        var stageOneInput = new CompleteOsanProgressInput(
            stageOneOperation,
            OsanCompletionModes.Batch,
            1,
            [
                new OsanProgressTargetRequest(targetIds[0], 1),
                new OsanProgressTargetRequest(targetIds[1], 1)
            ],
            [photo]);
        var stageOne = await progressStore.CompleteAsync(
            projectId,
            stageOneInput,
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, stageOne.Status);
        Assert.False(stageOne.Value!.Replayed);
        Assert.All(stageOne.Value.Project.Targets, target =>
        {
            Assert.False(target.Steps[0].CanCompleteIndividual);
            Assert.True(target.Steps[1].CanCompleteIndividual);
            Assert.True(target.Steps[1].CanCompleteBatch);
            Assert.All(target.Steps.Skip(2), step =>
            {
                Assert.False(step.CanCompleteIndividual);
                Assert.False(step.CanCompleteBatch);
            });
        });
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_photos where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_step_photos where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

        var listedAfterFirstCompletion = Assert.Single((await projectStore.ListAsync(
            new ProjectAccessScope(true, []),
            TestContext.Current.CancellationToken)).Items);
        var detailedAfterFirstCompletion = await projectStore.GetAsync(
            projectId,
            TestContext.Current.CancellationToken);
        Assert.Equal("InProgress", listedAfterFirstCompletion.Status);
        Assert.Equal(2, listedAfterFirstCompletion.CompletedStepCount);
        Assert.Equal(14, listedAfterFirstCompletion.TotalStepCount);
        Assert.NotNull(detailedAfterFirstCompletion);
        Assert.Equal("InProgress", detailedAfterFirstCompletion.Status);
        Assert.Equal(2, detailedAfterFirstCompletion.CompletedStepCount);
        Assert.Equal(14, detailedAfterFirstCompletion.TotalStepCount);

        var replay = await progressStore.CompleteAsync(
            projectId,
            stageOneInput,
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, replay.Status);
        Assert.True(replay.Value!.Replayed);
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_photos where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

        var conflictingReplay = await progressStore.CompleteAsync(
            projectId,
            stageOneInput with { StageSequence = 2 },
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, conflictingReplay.Status);
        Assert.Equal("osan_progress_operation_conflict", conflictingReplay.ErrorCode);

        var versions = new Dictionary<Guid, int>
        {
            [targetIds[0]] = 2,
            [targetIds[1]] = 2
        };
        var firstTargetStageTwo = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                2,
                [new OsanProgressTargetRequest(targetIds[0], versions[targetIds[0]])],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, firstTargetStageTwo.Status);
        versions[targetIds[0]] += 1;

        var mixedPrerequisiteOperation = Guid.NewGuid();
        var mixedPrerequisiteBatch = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                mixedPrerequisiteOperation,
                OsanCompletionModes.Batch,
                3,
                targetIds.Select(id => new OsanProgressTargetRequest(id, versions[id])).ToArray(),
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, mixedPrerequisiteBatch.Status);
        Assert.Equal("osan_progress_prerequisite_incomplete", mixedPrerequisiteBatch.ErrorCode);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_target_steps where project_id=@project_id and sequence_number=3 and status='Completed';",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_operations where operation_id=@operation_id;",
            TestContext.Current.CancellationToken,
            ("operation_id", mixedPrerequisiteOperation)));

        var secondTargetStageTwo = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                2,
                [new OsanProgressTargetRequest(targetIds[1], versions[targetIds[1]])],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, secondTargetStageTwo.Status);
        versions[targetIds[1]] += 1;

        var prematurePacking = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Batch,
                7,
                targetIds.Select(id => new OsanProgressTargetRequest(id, versions[id])).ToArray(),
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, prematurePacking.Status);
        Assert.Equal("osan_progress_packing_prerequisite_incomplete", prematurePacking.ErrorCode);

        for (var stage = 3; stage <= 5; stage += 1)
        {
            var result = await progressStore.CompleteAsync(
                projectId,
                new CompleteOsanProgressInput(
                    Guid.NewGuid(),
                    OsanCompletionModes.Batch,
                    stage,
                    targetIds.Select(id => new OsanProgressTargetRequest(id, versions[id])).ToArray(),
                    []),
                UserId,
                TestContext.Current.CancellationToken);
            Assert.Equal(OsanProgressMutationStatus.Success, result.Status);
            foreach (var targetId in targetIds)
            {
                versions[targetId] += 1;
            }
        }

        var firstTargetStageSix = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                6,
                [new OsanProgressTargetRequest(targetIds[0], versions[targetIds[0]])],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, firstTargetStageSix.Status);
        versions[targetIds[0]] += 1;

        var mixedPackingOperation = Guid.NewGuid();
        var mixedPacking = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                mixedPackingOperation,
                OsanCompletionModes.Batch,
                7,
                targetIds.Select(id => new OsanProgressTargetRequest(id, versions[id])).ToArray(),
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Conflict, mixedPacking.Status);
        Assert.Equal("osan_progress_packing_prerequisite_incomplete", mixedPacking.ErrorCode);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_project_target_steps where project_id=@project_id and sequence_number=7 and status='Completed';",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_operations where operation_id=@operation_id;",
            TestContext.Current.CancellationToken,
            ("operation_id", mixedPackingOperation)));

        var secondTargetStageSix = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                6,
                [new OsanProgressTargetRequest(targetIds[1], versions[targetIds[1]])],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, secondTargetStageSix.Status);
        versions[targetIds[1]] += 1;

        var firstPacking = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                7,
                [new OsanProgressTargetRequest(targetIds[0], versions[targetIds[0]])],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, firstPacking.Status);
        Assert.Equal("InProgress", firstPacking.Value!.Project.Status);
        Assert.Equal("Active", await database.ReadScalarAsync<string>(
            "select status from projects where id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

        var finalPackingInput = new CompleteOsanProgressInput(
            Guid.NewGuid(),
            OsanCompletionModes.Individual,
            7,
            [new OsanProgressTargetRequest(targetIds[1], versions[targetIds[1]])],
            []);
        var concurrentLastPacking = await Task.WhenAll(
            progressStore.CompleteAsync(
                projectId,
                finalPackingInput,
                UserId,
                TestContext.Current.CancellationToken),
            progressStore.CompleteAsync(
                projectId,
                finalPackingInput with { OperationId = Guid.NewGuid() },
                UserId,
                TestContext.Current.CancellationToken));
        Assert.Single(concurrentLastPacking, result => result.Status == OsanProgressMutationStatus.Success);
        Assert.Single(concurrentLastPacking, result => result.Status == OsanProgressMutationStatus.Conflict);
        var lastPacking = concurrentLastPacking.Single(result => result.Status == OsanProgressMutationStatus.Success);
        Assert.Equal("Completed", lastPacking.Value!.Project.Status);
        Assert.All(lastPacking.Value.Project.Targets, target => Assert.Equal("Completed", target.Status));
        Assert.Equal("Completed", await database.ReadScalarAsync<string>(
            "select status from projects where id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        var listedCompleted = Assert.Single((await projectStore.ListAsync(
            new ProjectAccessScope(true, []),
            TestContext.Current.CancellationToken)).Items);
        var detailedCompleted = await projectStore.GetAsync(projectId, TestContext.Current.CancellationToken);
        Assert.Equal("Completed", listedCompleted.Status);
        Assert.Equal(14, listedCompleted.CompletedStepCount);
        Assert.Equal(14, listedCompleted.TotalStepCount);
        Assert.NotNull(detailedCompleted);
        Assert.Equal("Completed", detailedCompleted.Status);
        Assert.Equal(14, detailedCompleted.CompletedStepCount);
        Assert.Equal(14, detailedCompleted.TotalStepCount);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            """
            select (select count(*) from pending_issues where project_id=@project_id)
                 + (select count(*) from notification_deliveries where project_id=@project_id)
                 + (select count(*) from logistics_packing_units where project_id=@project_id)
                 + (select count(*) from panel_quality_inspection_attempts where project_id=@project_id);
            """,
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

        var legacy = await projectStore.CreateAsync(
            Normalize(ValidRequest(projectCode: "OSAN-LEGACY-ORDER", quantity: 1)),
            UserId,
            TestContext.Current.CancellationToken);
        var legacyProjectId = legacy.Value!.Project.ProjectId;
        var legacyTargetId = Assert.Single(legacy.Value.Project.Targets).TargetId;
        await database.ExecuteAsync(
            """
            update osan_project_target_steps
            set status='Completed',
                started_at_utc='2026-09-01T01:00:00Z',
                completed_at_utc='2026-09-01T02:00:00Z',
                completed_by_user_id=@user_id
            where project_id=@project_id and target_id=@target_id and sequence_number=4;
            """,
            TestContext.Current.CancellationToken,
            ("user_id", UserId),
            ("project_id", legacyProjectId),
            ("target_id", legacyTargetId));
        var legacyHistoryBefore = await database.ReadScalarAsync<string>(
            """
            select string_agg(
                sequence_number || ':' || status || ':'
                || coalesce(completed_at_utc::text, '') || ':'
                || coalesce(completed_by_user_id::text, ''),
                ',' order by sequence_number)
            from osan_project_target_steps
            where project_id=@project_id and target_id=@target_id;
            """,
            TestContext.Current.CancellationToken,
            ("project_id", legacyProjectId),
            ("target_id", legacyTargetId));

        var legacyProgress = await progressStore.GetAsync(
            legacyProjectId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(legacyProgress);
        Assert.Equal("InProgress", legacyProgress.Status);
        var legacyTarget = Assert.Single(legacyProgress.Targets);
        Assert.Equal("InProgress", legacyTarget.Status);
        Assert.True(legacyTarget.Steps[0].CanCompleteIndividual);
        Assert.False(legacyTarget.Steps[1].CanCompleteIndividual);
        Assert.Equal("Completed", legacyTarget.Steps[3].Status);
        Assert.False(legacyTarget.Steps[3].CanCompleteIndividual);
        Assert.False(legacyTarget.Steps[4].CanCompleteIndividual);
        Assert.Equal(legacyHistoryBefore, await database.ReadScalarAsync<string>(
            """
            select string_agg(
                sequence_number || ':' || status || ':'
                || coalesce(completed_at_utc::text, '') || ':'
                || coalesce(completed_by_user_id::text, ''),
                ',' order by sequence_number)
            from osan_project_target_steps
            where project_id=@project_id and target_id=@target_id;
            """,
            TestContext.Current.CancellationToken,
            ("project_id", legacyProjectId),
            ("target_id", legacyTargetId)));
    }

    [Fact]
    public async Task ProgressPhotoValidator_RejectsMismatchCorruptionAndLimits()
    {
        var valid = CreateStructurallyValidPng();
        Assert.NotNull((await ValidatePhotoAsync("photo.png", "image/png", valid)).Photo);
        Assert.NotNull((await ValidatePhotoAsync("photo.png", "application/octet-stream", valid)).Photo);
        Assert.NotNull((await ValidatePhotoAsync(
            "photo.jpg", "image/jpeg", CreateStructurallyValidJpeg())).Photo);
        var validJpeg = CreateStructurallyValidJpeg();
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "missing-entropy.jpg", "image/jpeg", RemoveJpegEntropy(validJpeg, keepOneByte: false))).Error,
            StringComparison.Ordinal);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "corrupt-entropy.jpg", "image/jpeg", RemoveJpegEntropy(validJpeg, keepOneByte: true))).Error,
            StringComparison.Ordinal);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "fake.jpg",
            "image/jpeg",
            Convert.FromHexString("FFD8FFDB0002FFC40002FFC00008080001000100FFDA000200FFD9"))).Error,
            StringComparison.Ordinal);
        Assert.Contains("일치하지", (await ValidatePhotoAsync(
            "photo.jpg", "image/jpeg", valid)).Error, StringComparison.Ordinal);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "photo.png", "image/png", [1, 2, 3])).Error, StringComparison.Ordinal);
        Assert.Contains("5MiB", (await ValidatePhotoAsync(
            "large.png", "image/png", new byte[OsanProgressPhotoValidator.MaximumPhotoBytes + 1])).Error,
            StringComparison.Ordinal);

        var corruptCrc = valid.ToArray();
        corruptCrc[corruptCrc.Length - 5] ^= 0x01;
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "corrupt.png", "image/png", corruptCrc)).Error, StringComparison.Ordinal);

        var corruptImageData = RewritePngChunk(valid, "IDAT"u8, data => data[0] ^= 0xff);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "invalid-zlib.png", "image/png", corruptImageData)).Error, StringComparison.Ordinal);
        Assert.NotNull((await ValidatePhotoAsync(
            "empty-idat.png", "image/png", InsertEmptyIdatBeforeFirst(valid))).Photo);

        var oversizedDimension = RewritePngChunk(valid, "IHDR"u8, data =>
            BinaryPrimitives.WriteUInt32BigEndian(data[..4], uint.MaxValue));
        var exception = await Record.ExceptionAsync(async () => await ValidatePhotoAsync(
            "dimension.png", "image/png", oversizedDimension));
        Assert.Null(exception);
        Assert.Null((await ValidatePhotoAsync(
            "dimension.png", "image/png", oversizedDimension)).Photo);

        using var wideImage = new Image<Rgba32>(9000, 1);
        using var wideStream = new MemoryStream();
        wideImage.SaveAsPng(wideStream);
        Assert.NotNull((await ValidatePhotoAsync(
            "wide.png", "image/png", wideStream.ToArray())).Photo);

        using var interlacedImage = new MagickImage(MagickColors.Red, 1, 1);
        interlacedImage.Settings.Interlace = Interlace.Png;
        var interlacedBytes = interlacedImage.ToByteArray(MagickFormat.Png);
        Assert.NotNull((await ValidatePhotoAsync(
            "interlaced.png", "image/png", interlacedBytes)).Photo);
    }

    [Fact]
    public async Task ProgressPhotoValidator_StripsSensitiveMetadataWithoutReencodingPixelsOrColor()
    {
        var jpegBase = CreateColorJpeg();
        var exif = CreateExifWithOrientationAndSensitiveMetadata(6);
        var jpegWithMetadata = InsertJpegApp1(jpegBase, exif);

        var (jpegPhoto, jpegError) = await ValidatePhotoAsync(
            "camera.jpg", "image/jpeg", jpegWithMetadata);

        Assert.Null(jpegError);
        Assert.NotNull(jpegPhoto);
        Assert.Equal((ushort)6, OsanProgressPhotoMetadataSanitizer.ReadOrientation(
            jpegPhoto.Content, "image/jpeg"));
        Assert.DoesNotContain("SyntheticCamera", Encoding.ASCII.GetString(jpegPhoto.Content));
        Assert.Equal(ReadJpegScanBytes(jpegBase), ReadJpegScanBytes(jpegPhoto.Content));
        Assert.NotEmpty(ReadJpegSegments(jpegBase, 0xe2));
        Assert.Equal(ReadJpegSegments(jpegBase, 0xe2), ReadJpegSegments(jpegPhoto.Content, 0xe2));
        Assert.True(jpegPhoto.Content.Length <= jpegWithMetadata.Length);
        var alternateMetadata = jpegWithMetadata.ToArray();
        var cameraOffset = alternateMetadata.AsSpan().IndexOf("SyntheticCamera"u8);
        Assert.True(cameraOffset >= 0);
        "AlternateCamera"u8.CopyTo(alternateMetadata.AsSpan(cameraOffset));
        var alternatePhoto = (await ValidatePhotoAsync(
            "alternate.jpg", "image/jpeg", alternateMetadata)).Photo;
        Assert.NotNull(alternatePhoto);
        Assert.Equal(jpegPhoto.Content, alternatePhoto.Content);
        Assert.Equal(jpegPhoto.Sha256, alternatePhoto.Sha256);

        var pngBase = CreateColorPng();
        var gamma = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(gamma, 45455);
        var pngWithColor = InsertPngChunkBefore(pngBase, "sRGB"u8, [0]);
        pngWithColor = InsertPngChunkBefore(pngWithColor, "gAMA"u8, gamma);
        var pngWithExif = InsertPngChunkBefore(pngWithColor, "eXIf"u8, exif.AsSpan(6));
        var pngWithMetadata = InsertPngChunkBefore(
            pngWithExif, "tEXt"u8, "Device\0SyntheticCamera"u8);
        var originalIdat = ReadPngChunks(pngWithMetadata, "IDAT");

        var (pngPhoto, pngError) = await ValidatePhotoAsync(
            "camera.png", "image/png", pngWithMetadata);

        Assert.Null(pngError);
        Assert.NotNull(pngPhoto);
        Assert.Equal((ushort)6, OsanProgressPhotoMetadataSanitizer.ReadOrientation(
            pngPhoto.Content, "image/png"));
        Assert.DoesNotContain("SyntheticCamera", Encoding.ASCII.GetString(pngPhoto.Content));
        Assert.Equal(originalIdat, ReadPngChunks(pngPhoto.Content, "IDAT"));
        Assert.Equal(ReadPngChunks(pngWithMetadata, "sRGB"), ReadPngChunks(pngPhoto.Content, "sRGB"));
        Assert.Equal(ReadPngChunks(pngWithMetadata, "gAMA"), ReadPngChunks(pngPhoto.Content, "gAMA"));

        var malformedExif = InsertJpegApp1(jpegBase, "Exif\0\0II*\0"u8.ToArray());
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "malformed-exif.jpg", "image/jpeg", malformedExif)).Error,
            StringComparison.Ordinal);
        var zeroRootTiff = new byte[14];
        zeroRootTiff[0] = (byte)'I';
        zeroRootTiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(zeroRootTiff.AsSpan(2, 2), 42);
        var zeroRootIfd = new byte[6 + zeroRootTiff.Length];
        "Exif\0\0"u8.CopyTo(zeroRootIfd);
        zeroRootTiff.CopyTo(zeroRootIfd, 6);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "zero-root-ifd.jpg", "image/jpeg", InsertJpegApp1(jpegBase, zeroRootIfd))).Error,
            StringComparison.Ordinal);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "zero-root-ifd.png",
            "image/png",
            InsertPngChunkBefore(pngBase, "eXIf"u8, zeroRootIfd.AsSpan(6)))).Error,
            StringComparison.Ordinal);

        var invalidOrientationCount = exif.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidOrientationCount.AsSpan(20, 4), 3);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "invalid-orientation-count.jpg",
            "image/jpeg",
            InsertJpegApp1(jpegBase, invalidOrientationCount))).Error,
            StringComparison.Ordinal);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "invalid-orientation-count.png",
            "image/png",
            InsertPngChunkBefore(pngBase, "eXIf"u8, invalidOrientationCount.AsSpan(6)))).Error,
            StringComparison.Ordinal);

        var zeroGpsPointer = exif.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(zeroGpsPointer.AsSpan(48, 4), 0);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "zero-gps-pointer.png",
            "image/png",
            InsertPngChunkBefore(pngBase, "eXIf"u8, zeroGpsPointer.AsSpan(6)))).Error,
            StringComparison.Ordinal);

        var duplicateExif = InsertJpegApp1(InsertJpegApp1(jpegBase, exif), exif);
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "duplicate-exif.jpg", "image/jpeg", duplicateExif)).Error,
            StringComparison.Ordinal);
        var duplicatePngExif = InsertPngChunkBefore(
            InsertPngChunkBefore(pngBase, "eXIf"u8, exif.AsSpan(6)),
            "eXIf"u8,
            exif.AsSpan(6));
        Assert.Contains("올바른", (await ValidatePhotoAsync(
            "duplicate-exif.png", "image/png", duplicatePngExif)).Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProgressStore_RejectsNullTargetElementsBeforeDatabaseAccess()
    {
        var provider = new DatabaseConnectionStringProvider(new ConfigurationBuilder().Build());
        var store = new OsanProgressStore(provider);
        var completion = await store.CompleteAsync(
            Guid.NewGuid(),
            new CompleteOsanProgressInput(
                Guid.NewGuid(), OsanCompletionModes.Batch, 1, [null], []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Validation, completion.Status);
        Assert.Contains("Targets", completion.Errors!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private static byte[] CreateStructurallyValidPng()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static Task<(OsanProgressPhotoInput? Photo, string? Error)> ValidatePhotoAsync(
        string fileName,
        string contentType,
        byte[] content) =>
        OsanProgressPhotoValidator.ValidateAsync(
            fileName,
            contentType,
            content,
            TestContext.Current.CancellationToken);

    private static byte[] RewritePngChunk(
        byte[] source,
        ReadOnlySpan<byte> chunkType,
        PngChunkRewrite rewrite)
    {
        var result = source.ToArray();
        var offset = 8;
        while (offset <= result.Length - 12)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(result.AsSpan(offset, 4));
            if (length < 0 || (long)offset + length + 12 > result.Length)
            {
                throw new InvalidOperationException("Invalid source PNG fixture.");
            }
            if (result.AsSpan(offset + 4, 4).SequenceEqual(chunkType))
            {
                var data = result.AsSpan(offset + 8, length);
                rewrite(data);
                BinaryPrimitives.WriteUInt32BigEndian(
                    result.AsSpan(offset + 8 + length, 4),
                    ComputePngCrc(result.AsSpan(offset + 4, 4), data));
                return result;
            }
            offset += length + 12;
        }
        throw new InvalidOperationException("PNG fixture chunk was not found.");
    }

    private static byte[] InsertEmptyIdatBeforeFirst(byte[] source)
    {
        var offset = 8;
        while (offset <= source.Length - 12)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            if (length < 0 || (long)offset + length + 12 > source.Length)
            {
                throw new InvalidOperationException("Invalid source PNG fixture.");
            }
            if (source.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8))
            {
                var result = new byte[source.Length + 12];
                source.AsSpan(0, offset).CopyTo(result);
                BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(offset, 4), 0);
                "IDAT"u8.CopyTo(result.AsSpan(offset + 4, 4));
                BinaryPrimitives.WriteUInt32BigEndian(
                    result.AsSpan(offset + 8, 4),
                    ComputePngCrc("IDAT"u8, []));
                source.AsSpan(offset).CopyTo(result.AsSpan(offset + 12));
                return result;
            }
            offset += length + 12;
        }
        throw new InvalidOperationException("PNG fixture IDAT chunk was not found.");
    }

    private static uint ComputePngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = UpdatePngCrc(crc, value);
        }
        foreach (var value in data)
        {
            crc = UpdatePngCrc(crc, value);
        }
        return crc ^ uint.MaxValue;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit += 1)
        {
            crc = (crc & 1) == 0 ? crc >> 1 : 0xedb88320U ^ (crc >> 1);
        }
        return crc;
    }

    private delegate void PngChunkRewrite(Span<byte> data);

    private static byte[] CreateStructurallyValidJpeg()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    private static byte[] CreateColorJpeg()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 1);
        image.SetProfile(ColorProfiles.SRGB);
        return image.ToByteArray(MagickFormat.Jpeg);
    }

    private static byte[] CreateColorPng()
    {
        using var image = new Image<Rgba32>(2, 1);
        image[0, 0] = new Rgba32(255, 0, 0);
        image[1, 0] = new Rgba32(0, 128, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateExifWithOrientationAndSensitiveMetadata(ushort orientation)
    {
        var make = "SyntheticCamera\0"u8.ToArray();
        const int ifdOffset = 8;
        const int ifdSize = 2 + (3 * 12) + 4;
        var makeOffset = ifdOffset + ifdSize;
        var gpsOffset = makeOffset + make.Length;
        var tiff = new byte[gpsOffset + 18];
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4, 4), ifdOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(ifdOffset, 2), 3);
        WriteTiffEntry(tiff, 10, 0x0112, 3, 1, orientation);
        WriteTiffEntry(tiff, 22, 0x010f, 2, (uint)make.Length, (uint)makeOffset);
        WriteTiffEntry(tiff, 34, 0x8825, 4, 1, (uint)gpsOffset);
        make.CopyTo(tiff, makeOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(gpsOffset, 2), 1);
        WriteTiffEntry(tiff, gpsOffset + 2, 0x0001, 2, 2, (uint)'N');
        return [.. "Exif\0\0"u8, .. tiff];
    }

    private static void WriteTiffEntry(
        byte[] tiff, int offset, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset, 2), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset + 2, 2), type);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 4, 4), count);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 8, 4), value);
    }

    private static byte[] InsertJpegApp1(byte[] source, byte[] payload)
    {
        var result = new byte[source.Length + payload.Length + 4];
        source.AsSpan(0, 2).CopyTo(result);
        result[2] = 0xff;
        result[3] = 0xe1;
        BinaryPrimitives.WriteUInt16BigEndian(
            result.AsSpan(4, 2), checked((ushort)(payload.Length + 2)));
        payload.CopyTo(result, 6);
        source.AsSpan(2).CopyTo(result.AsSpan(payload.Length + 6));
        return result;
    }

    private static byte[] ReadJpegScanBytes(byte[] source)
    {
        var start = source.AsSpan().IndexOf(new byte[] { 0xff, 0xda });
        Assert.True(start >= 0);
        return source.AsSpan(start).ToArray();
    }

    private static byte[] ReadJpegSegments(byte[] source, byte requestedMarker)
    {
        using var output = new MemoryStream();
        var offset = 2;
        while (offset < source.Length - 1 && source[offset] == 0xff)
        {
            var marker = source[offset + 1];
            if (marker is 0xda or 0xd9)
            {
                break;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset + 2, 2));
            var end = offset + 2 + length;
            if (marker == requestedMarker)
            {
                output.Write(source, offset, end - offset);
            }
            offset = end;
        }
        return output.ToArray();
    }

    private static byte[] InsertPngChunkBefore(
        byte[] source, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var offset = 8;
        while (offset <= source.Length - 12)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            if (length < 0 || (long)offset + length + 12 > source.Length)
            {
                throw new InvalidOperationException("Invalid source PNG fixture.");
            }
            if (source.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8))
            {
                var chunkLength = data.Length + 12;
                var result = new byte[source.Length + chunkLength];
                source.AsSpan(0, offset).CopyTo(result);
                BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(offset, 4), data.Length);
                type.CopyTo(result.AsSpan(offset + 4, 4));
                data.CopyTo(result.AsSpan(offset + 8));
                BinaryPrimitives.WriteUInt32BigEndian(
                    result.AsSpan(offset + 8 + data.Length, 4),
                    ComputePngCrc(type, data));
                source.AsSpan(offset).CopyTo(result.AsSpan(offset + chunkLength));
                return result;
            }
            offset += length + 12;
        }
        throw new InvalidOperationException("PNG fixture IDAT chunk was not found.");
    }

    private static byte[] ReadPngChunks(byte[] source, string type)
    {
        using var output = new MemoryStream();
        var offset = 8;
        while (offset <= source.Length - 12)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            if (length < 0 || (long)offset + length + 12 > source.Length)
            {
                throw new InvalidOperationException("Invalid source PNG fixture.");
            }
            if (Encoding.ASCII.GetString(source, offset + 4, 4) == type)
            {
                output.Write(source, offset + 8, length);
            }
            offset += length + 12;
        }
        return output.ToArray();
    }

    private static byte[] RemoveJpegEntropy(byte[] source, bool keepOneByte)
    {
        var startOfScan = source.AsSpan().IndexOf(new byte[] { 0xff, 0xda });
        Assert.True(startOfScan >= 0);
        var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(startOfScan + 2, 2));
        var entropyStart = startOfScan + 2 + segmentLength;
        return keepOneByte
            ? [.. source.AsSpan(0, entropyStart), 0x00, 0xff, 0xd9]
            : [.. source.AsSpan(0, entropyStart), 0xff, 0xd9];
    }

    private static CreateOsanProjectRequest ValidRequest(
        string title = "Project title",
        string projectCode = "OSAN-TEST",
        string customerName = "Customer",
        string? poNumber = null,
        string? workOrderNumber = null,
        int? quantity = 1,
        Guid? operationId = null,
        DateOnly? deliveryDate = null,
        string productName = "Product") =>
        new(
            title,
            projectCode,
            customerName,
            poNumber,
            workOrderNumber,
            deliveryDate ?? new DateOnly(2026, 12, 31),
            productName,
            quantity,
            operationId ?? Guid.NewGuid());

    private static UploadedExcelFile Upload(string fileName, byte[] content) =>
        new(
            fileName,
            content.Length,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            content);

    private static OsanProjectExcelRowRequest ToExcelRowRequest(OsanProjectExcelPreviewRowResponse row) =>
        new(
            row.RowNumber,
            row.Title,
            row.ProjectCode,
            row.CustomerName,
            row.PoNumber,
            row.WorkOrderNumber,
            row.DeliveryDate,
            row.ProductName,
            row.Quantity);

    private static byte[] CreateBatchExcel(IReadOnlyList<(string Title, string Code, int Quantity)> rows) =>
        CreateOsanExcel(workbook =>
        {
            var sheet = workbook.Worksheet(1);
            AddExcelHeaders(sheet);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                AddExcelRow(
                    sheet,
                    index + 2,
                    row.Title,
                    row.Code,
                    "Customer",
                    $"00{index + 1}-PO",
                    $"00{index + 1}-W/O",
                    new DateOnly(2026, 12, 31),
                    "Product",
                    row.Quantity);
            }
        });

    private static byte[] CreateOsanExcel(Action<XLWorkbook> configure)
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Projects");
        configure(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddExcelHeaders(IXLWorksheet sheet)
    {
        var headers = new[]
        {
            "프로젝트 Title", "프로젝트 코드", "거래처", "PO No", "W/O No", "납기일", "제품명", "수량"
        };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(1, index + 1).Value = headers[index];
    }

    private static void AddExcelRow(
        IXLWorksheet sheet,
        int row,
        string title,
        string code,
        string customer,
        string? po,
        string? workOrder,
        DateOnly deliveryDate,
        string product,
        double quantity)
    {
        sheet.Cell(row, 1).Value = title;
        sheet.Cell(row, 2).Value = code;
        sheet.Cell(row, 3).Value = customer;
        sheet.Cell(row, 4).Value = po;
        sheet.Cell(row, 5).Value = workOrder;
        sheet.Cell(row, 6).Value = deliveryDate.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(row, 7).Value = product;
        sheet.Cell(row, 8).Value = quantity;
    }

    private static byte[] AddExternalRelationship(byte[] source)
    {
        using var output = new MemoryStream();
        output.Write(source);
        output.Position = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = Assert.Single(archive.Entries, item =>
                string.Equals(item.FullName, "_rels/.rels", StringComparison.OrdinalIgnoreCase));
            string xml;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) xml = reader.ReadToEnd();
            entry.Delete();
            var replacement = archive.CreateEntry("_rels/.rels");
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(xml.Replace(
                "</Relationships>",
                "<Relationship Id=\"external-test\" Type=\"urn:test\" Target=\"https://example.invalid\" TargetMode = \"Exter&#110;al\"/></Relationships>",
                StringComparison.Ordinal));
        }
        return output.ToArray();
    }

    private static byte[] RelocateWorksheetPart(byte[] source, string originalPath, string replacementPath)
    {
        using var output = new MemoryStream();
        output.Write(source);
        output.Position = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var original = Assert.Single(archive.Entries, item =>
                string.Equals(item.FullName, originalPath, StringComparison.OrdinalIgnoreCase));
            byte[] content;
            using (var input = original.Open())
            using (var copied = new MemoryStream())
            {
                input.CopyTo(copied);
                content = copied.ToArray();
            }
            original.Delete();
            var replacement = archive.CreateEntry(replacementPath);
            using (var replacementStream = replacement.Open()) replacementStream.Write(content);

            RewriteZipTextEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                text => text.Replace(
                    "worksheets/sheet2.xml",
                    replacementPath["xl/".Length..],
                    StringComparison.Ordinal));
            RewriteZipTextEntry(
                archive,
                "[Content_Types].xml",
                text => text.Replace(
                    "/xl/worksheets/sheet2.xml",
                    $"/{replacementPath}",
                    StringComparison.Ordinal));
        }
        return output.ToArray();
    }

    private static void RewriteZipTextEntry(
        ZipArchive archive,
        string path,
        Func<string, string> rewrite)
    {
        var entry = Assert.Single(archive.Entries, item =>
            string.Equals(item.FullName, path, StringComparison.OrdinalIgnoreCase));
        string text;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) text = reader.ReadToEnd();
        entry.Delete();
        var replacement = archive.CreateEntry(path);
        using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
        writer.Write(rewrite(text));
    }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
