using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.PanelInformation;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class PanelInformationApiTests
{
    private static readonly Guid SalesOwnerUserId = new("50000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task DesignUser_DirectInput_AllowsDuplicateNamesAndCalculatesQrAndCompletion()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-INFO-001", "Panel Info Direct", "WoodenCrate", 2);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var panels = await ReadPanelInformationAsync(designClient, projectId);
        var rows = panels.RootElement.GetProperty("panels").EnumerateArray().ToList();

        var nameOnly = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = rows.Select(row => new
                {
                    PanelId = row.GetProperty("panelId").GetGuid(),
                    ExpectedPanelInfoVersion = row.GetProperty("panelInfoVersion").GetInt32(),
                    PanelNameUpdate = new { IsChanged = true, Value = " PNL-1 " }
                }).ToArray()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, nameOnly.StatusCode);
        using var nameOnlyJson = await ReadJsonAsync(nameOnly);
        var updatedRows = nameOnlyJson.RootElement.GetProperty("panels").EnumerateArray().ToList();
        Assert.Equal(0, nameOnlyJson.RootElement.GetProperty("panelInfoCompletedCount").GetInt32());
        Assert.Equal(2, nameOnlyJson.RootElement.GetProperty("qrEligibleCount").GetInt32());
        Assert.Equal(1, nameOnlyJson.RootElement.GetProperty("duplicatePanelNameGroupCount").GetInt32());
        Assert.All(updatedRows, row =>
        {
            Assert.Equal("PNL-1", row.GetProperty("panelName").GetString());
            Assert.True(row.GetProperty("qrEligible").GetBoolean());
            Assert.False(row.GetProperty("panelInfoCompleted").GetBoolean());
            Assert.True(row.GetProperty("hasDuplicateName").GetBoolean());
            Assert.Equal(2, row.GetProperty("duplicateNameCount").GetInt32());
        });

        var complete = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = updatedRows.Select(row => new
                {
                    PanelId = row.GetProperty("panelId").GetGuid(),
                    ExpectedPanelInfoVersion = row.GetProperty("panelInfoVersion").GetInt32(),
                    SizeUpdate = new
                    {
                        IsChanged = true,
                        Clear = false,
                        InputUnit = "Inch",
                        Width = 10m,
                        Height = 20m,
                        Depth = 30m
                    }
                }).ToArray()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var completeJson = await ReadJsonAsync(complete);
        Assert.Equal(2, completeJson.RootElement.GetProperty("panelInfoCompletedCount").GetInt32());
        Assert.Equal(254m, completeJson.RootElement.GetProperty("panels")[0].GetProperty("widthMm").GetDecimal());
        Assert.Equal(508m, completeJson.RootElement.GetProperty("panels")[0].GetProperty("heightMm").GetDecimal());
        Assert.Equal(762m, completeJson.RootElement.GetProperty("panels")[0].GetProperty("depthMm").GetDecimal());
        Assert.Equal(8, await context.CountPanelAuditEventsAsync(projectId));
    }

    [Fact]
    public async Task DesignCompletion_RequiresPanelNameForGeneralPackagingAndAllowsPartialSave()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-GENERAL-COMP", "Panel General Completion", "StretchWrap", 2);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var panels = await ReadPanelInformationAsync(designClient, projectId);
        var rows = panels.RootElement.GetProperty("panels").EnumerateArray().ToList();

        var partial = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = new[]
                {
                    new
                    {
                        PanelId = rows[0].GetProperty("panelId").GetGuid(),
                        ExpectedPanelInfoVersion = rows[0].GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "GENERAL-1" }
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        using var partialJson = await ReadJsonAsync(partial);
        Assert.Equal(1, partialJson.RootElement.GetProperty("panelInfoCompletedCount").GetInt32());
        using var partialWorkflow = await ReadJsonAsync(await designClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var partialDesignStage = partialWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "DesignPanelInfo");
        Assert.Equal("InProgress", partialDesignStage.GetProperty("status").GetString());

        var updatedRows = partialJson.RootElement.GetProperty("panels").EnumerateArray().ToList();
        var complete = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = new[]
                {
                    new
                    {
                        PanelId = updatedRows[1].GetProperty("panelId").GetGuid(),
                        ExpectedPanelInfoVersion = updatedRows[1].GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "GENERAL-2" }
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var completeJson = await ReadJsonAsync(complete);
        Assert.Equal(2, completeJson.RootElement.GetProperty("panelInfoCompletedCount").GetInt32());
        using var completeWorkflow = await ReadJsonAsync(await designClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var completedDesignStage = completeWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "DesignPanelInfo");
        Assert.Equal("Completed", completedDesignStage.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("dev-sales", HttpStatusCode.OK)]
    [InlineData("dev-production", HttpStatusCode.OK)]
    [InlineData("dev-admin", HttpStatusCode.Forbidden)]
    [InlineData("dev-manufacturing", HttpStatusCode.Forbidden)]
    [InlineData("dev-quality", HttpStatusCode.Forbidden)]
    [InlineData("dev-viewer", HttpStatusCode.Forbidden)]
    public async Task PanelInfoUpdatePermission_IsServerEnforced(string developmentUserKey, HttpStatusCode expectedStatus)
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var client = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"PANEL-AUTH-{developmentUserKey}", $"Panel Auth {developmentUserKey}", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var panels = await ReadPanelInformationAsync(salesClient, projectId);
        var panel = panels.RootElement.GetProperty("panels")[0];

        var response = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = new[]
                {
                    new
                    {
                        PanelId = panel.GetProperty("panelId").GetGuid(),
                        ExpectedPanelInfoVersion = panel.GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "AUTH-PNL" }
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Theory]
    [InlineData("dev-admin", HttpStatusCode.OK)]
    [InlineData("dev-sales", HttpStatusCode.Forbidden)]
    [InlineData("dev-design", HttpStatusCode.Forbidden)]
    [InlineData("dev-production", HttpStatusCode.Forbidden)]
    [InlineData("dev-manufacturing", HttpStatusCode.Forbidden)]
    [InlineData("dev-viewer", HttpStatusCode.Forbidden)]
    public async Task PanelHistory_RequiresAuditReadAll(string developmentUserKey, HttpStatusCode expectedStatus)
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var client = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"PANEL-HISTORY-AUTH-{developmentUserKey}", $"Panel History Auth {developmentUserKey}", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        using var response = await client.GetAsync($"/api/projects/{projectId}/panel-information/history", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task ExcelPreviewAndApply_RevalidatesFileAndStoresBatchWithoutBinary()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-EXCEL-001", "Panel Excel", "StretchWrap", 2);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var file = CreateExcelFile(("No", "panel name", "w", "h", "d"), ["1", "EX-1", "", "", ""], ["2", "EX-2", "", "", ""]);

        using var previewContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "design.xlsx" }
        };
        var preview = await designClient.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/preview",
            previewContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        Assert.Equal(2, previewJson.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorCount").GetInt32());

        var versions = previewJson.RootElement.GetProperty("rows").EnumerateArray()
            .Select(row => new
            {
                PanelId = row.GetProperty("panelId").GetGuid(),
                ExpectedPanelInfoVersion = row.GetProperty("expectedPanelInfoVersion").GetInt32()
            })
            .ToArray();

        using var applyContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "design.xlsx" },
            { new StringContent(previewJson.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(previewJson.RootElement.GetProperty("expectedPackagingMethod").GetString()!), "expectedPackagingMethod" },
            { new StringContent(JsonSerializer.Serialize(versions)), "expectedVersions" }
        };
        var apply = await designClient.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/apply",
            applyContent,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        Assert.Equal(1, await context.CountExcelImportBatchesAsync(projectId));
        Assert.False(await context.ExcelBinaryColumnExistsAsync());
    }

    [Fact]
    public async Task ExcelPartialInput_AppliesOnlyEnteredFieldsAndSkipsBlankRows()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-PARTIAL-001", "Panel Partial Excel", "WoodenCrate", 3);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var file = CreateExcelFile(
            ("No", "panel name", "w", "h", "d"),
            ["1", "PNL-1", "800", "1800", "400"],
            ["2", "", "", "", ""],
            ["3", "PNL-3", "", "", ""]);

        using var previewContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "partial.xlsx" },
            { new StringContent("Mm"), "inputUnit" }
        };
        var preview = await designClient.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/preview",
            previewContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        Assert.Equal(2, previewJson.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(1, previewJson.RootElement.GetProperty("skippedCount").GetInt32());
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorCount").GetInt32());
        var rows = previewJson.RootElement.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal("Skipped", rows.Single(row => row.GetProperty("no").GetInt32() == 2).GetProperty("resultType").GetString());

        var versions = previewJson.RootElement.GetProperty("expectedPanelInfoVersions").EnumerateArray()
            .Select(item => new
            {
                PanelId = item.GetProperty("panelId").GetGuid(),
                ExpectedPanelInfoVersion = item.GetProperty("expectedPanelInfoVersion").GetInt32()
            })
            .ToArray();

        using var applyContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "partial.xlsx" },
            { new StringContent("Mm"), "inputUnit" },
            { new StringContent(previewJson.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(previewJson.RootElement.GetProperty("expectedPackagingMethod").GetString()!), "expectedPackagingMethod" },
            { new StringContent(JsonSerializer.Serialize(versions)), "expectedVersions" }
        };
        var apply = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/apply", applyContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        using var appliedJson = await ReadJsonAsync(apply);
        var panels = appliedJson.RootElement.GetProperty("panels").EnumerateArray().ToList();
        Assert.Equal("PNL-1", panels[0].GetProperty("panelName").GetString());
        Assert.Equal(800m, panels[0].GetProperty("widthMm").GetDecimal());
        Assert.Equal(1, panels[0].GetProperty("panelInfoVersion").GetInt32());
        Assert.True(panels[0].GetProperty("panelInfoCompleted").GetBoolean());
        Assert.True(panels[0].GetProperty("qrEligible").GetBoolean());
        Assert.True(panels[1].GetProperty("panelName").ValueKind == JsonValueKind.Null);
        Assert.Equal(0, panels[1].GetProperty("panelInfoVersion").GetInt32());
        Assert.Equal("PNL-3", panels[2].GetProperty("panelName").GetString());
        Assert.Equal(1, panels[2].GetProperty("panelInfoVersion").GetInt32());
        Assert.False(panels[2].GetProperty("panelInfoCompleted").GetBoolean());
        Assert.True(panels[2].GetProperty("qrEligible").GetBoolean());

        using var adminClient = context.CreateClient("dev-admin");
        using var historyResponse = await adminClient.GetAsync($"/api/projects/{projectId}/panel-information/history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using var history = await ReadJsonAsync(historyResponse);
        var batch = history.RootElement.GetProperty("excelImportBatches").EnumerateArray().Single();
        Assert.Equal(1, batch.GetProperty("skippedPanelCount").GetInt32());
        Assert.Equal(5, history.RootElement.GetProperty("auditEvents").GetArrayLength());

        var badFile = CreateExcelFile(("No", "panel name", "w", "h", "d"), ["2", "", "100", "", ""]);
        using var badPreviewContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(badFile), "file", "partial-bad.xlsx" },
            { new StringContent("Mm"), "inputUnit" }
        };
        var badPreview = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", badPreviewContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, badPreview.StatusCode);
        using var badPreviewJson = await ReadJsonAsync(badPreview);
        Assert.Equal(1, badPreviewJson.RootElement.GetProperty("errorCount").GetInt32());
    }

    [Theory]
    [InlineData("dev-design")]
    [InlineData("dev-sales")]
    [InlineData("dev-production")]
    public async Task ExcelTemplateDownload_AllowsPanelInfoEditors(string developmentUserKey)
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var client = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"PANEL-TPL-AUTH-{developmentUserKey}", $"Panel Template Auth {developmentUserKey}", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template?unit=mm",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(".xlsx", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "");
    }

    [Theory]
    [InlineData("dev-admin")]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-viewer")]
    public async Task ExcelTemplateDownload_DeniesUsersWithoutPanelInfoUpdate(string developmentUserKey)
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var client = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"PANEL-TPL-DENY-{developmentUserKey}", $"Panel Template Deny {developmentUserKey}", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template?unit=mm",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExcelTemplateDownload_ReturnsCurrentActivePanelWorkbookWithoutChangingData()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-TPL-001", "Panel:/Template\r\nOne", "StretchWrap", 3);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var panels = await ReadPanelInformationAsync(designClient, projectId);
        var rows = panels.RootElement.GetProperty("panels").EnumerateArray().ToList();

        var update = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = rows.Take(2).Select((row, index) => new
                {
                    PanelId = row.GetProperty("panelId").GetGuid(),
                    ExpectedPanelInfoVersion = row.GetProperty("panelInfoVersion").GetInt32(),
                    PanelNameUpdate = new { IsChanged = true, Value = index == 0 ? "TPL-1" : "TPL-2" },
                    SizeUpdate = new
                    {
                        IsChanged = true,
                        Clear = false,
                        InputUnit = "Mm",
                        Width = index == 0 ? 254m : 508m,
                        Height = index == 0 ? 508m : 762m,
                        Depth = index == 0 ? 762m : 1016m
                    }
                }).ToArray()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await context.CancelPanelAsync(rows[2].GetProperty("panelId").GetGuid());
        var auditBefore = await context.CountPanelAuditEventsAsync(projectId);

        using var mmResponse = await designClient.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, mmResponse.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            mmResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(".xlsx", mmResponse.Content.Headers.ContentDisposition?.FileNameStar ?? mmResponse.Content.Headers.ContentDisposition?.FileName ?? "");

        var mmBytes = await mmResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using (var archive = new ZipArchive(new MemoryStream(mmBytes), ZipArchiveMode.Read))
        {
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
        }

        using (var workbook = new XLWorkbook(new MemoryStream(mmBytes)))
        {
            var worksheet = workbook.Worksheet("Panel Information");
            Assert.Equal(["No *", "도번", "패널명 *", "W", "H", "D"], Enumerable.Range(1, 6).Select(column => worksheet.Cell(1, column).GetString()).ToArray());
            Assert.Equal(1, worksheet.Cell(2, 1).GetValue<int>());
            Assert.Equal("", worksheet.Cell(2, 2).GetString());
            Assert.Equal("TPL-1", worksheet.Cell(2, 3).GetString());
            Assert.Equal(254m, worksheet.Cell(2, 4).GetValue<decimal>());
            Assert.Equal(508m, worksheet.Cell(2, 5).GetValue<decimal>());
            Assert.Equal(762m, worksheet.Cell(2, 6).GetValue<decimal>());
            Assert.Equal(2, worksheet.Cell(3, 1).GetValue<int>());
            Assert.Equal("TPL-2", worksheet.Cell(3, 3).GetString());
            Assert.True(worksheet.Cell(4, 1).IsEmpty());
            Assert.DoesNotContain(worksheet.CellsUsed(), cell => cell.HasFormula);
            Assert.True(worksheet.SheetView.SplitRow >= 1);
            Assert.True(worksheet.AutoFilter.IsEnabled);
            Assert.Equal("No *", worksheet.Cell(1, 1).GetString());
            Assert.Equal("패널명 *", worksheet.Cell(1, 3).GetString());
            Assert.Contains("완료 필수값", worksheet.Cell(1, 8).GetString());
            Assert.Contains("일부 입력 상태", worksheet.Cell(1, 8).GetString());
            Assert.Equal(XLColor.LightYellow, worksheet.Cell(1, 1).Style.Fill.BackgroundColor);
            Assert.True(worksheet.Column(3).Width >= 24);
            for (var column = 1; column <= 6; column++)
            {
                Assert.True(worksheet.Column(column).Width >= 8);
                Assert.True(worksheet.Column(column).Width <= 36);
            }
        }

        using var inchResponse = await designClient.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template?unit=inch",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, inchResponse.StatusCode);
        using (var workbook = new XLWorkbook(new MemoryStream(await inchResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))))
        {
            var worksheet = workbook.Worksheet("Panel Information");
            Assert.Equal(10.00m, worksheet.Cell(2, 4).GetValue<decimal>());
            Assert.Equal(20.00m, worksheet.Cell(2, 5).GetValue<decimal>());
            Assert.Equal(30.00m, worksheet.Cell(2, 6).GetValue<decimal>());
        }

        using var woodenCreated = await CreateProjectAsync(salesClient, "PANEL-TPL-WOOD", "Panel Template Wood", "WoodenCrate", 1);
        using var woodenCreatedJson = await ReadJsonAsync(woodenCreated);
        var woodenProjectId = woodenCreatedJson.RootElement.GetProperty("projectId").GetGuid();
        using var woodenResponse = await designClient.GetAsync(
            $"/api/projects/{woodenProjectId}/panel-information/import/template",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, woodenResponse.StatusCode);
        using (var workbook = new XLWorkbook(new MemoryStream(await woodenResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))))
        {
            var worksheet = workbook.Worksheet("Panel Information");
            Assert.Equal(["No *", "도번", "패널명 *", "W *", "H *", "D *"], Enumerable.Range(1, 6).Select(column => worksheet.Cell(1, column).GetString()).ToArray());
            Assert.Contains("목포장", worksheet.Cell(1, 8).GetString());
        }

        Assert.Equal(auditBefore, await context.CountPanelAuditEventsAsync(projectId));
    }

    [Fact]
    public async Task ExcelTemplateDownload_RejectsInvalidUnitAndDeletedProjects()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-TPL-002", "Panel Template Deleted", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        using var invalidUnit = await designClient.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template?unit=cm",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUnit.StatusCode);

        await context.SoftDeleteProjectAsync(projectId);
        using var deleted = await designClient.GetAsync(
            $"/api/projects/{projectId}/panel-information/import/template?unit=mm",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task DirectInput_PanelNameOnlyUpdate_DoesNotDriftCanonicalSizeOrAuditSize()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-DRIFT-001", "Panel Drift", "WoodenCrate", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var first = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];

        var initial = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = new[]
                {
                    new
                    {
                        PanelId = first.GetProperty("panelId").GetGuid(),
                        ExpectedPanelInfoVersion = first.GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "DRIFT-A" },
                        SizeUpdate = new
                        {
                            IsChanged = true,
                            Clear = false,
                            InputUnit = "Mm",
                            Width = 800.125m,
                            Height = 1800.375m,
                            Depth = 400.625m
                        }
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        var beforeNameOnly = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];
        var sizeAuditBefore = await context.CountPanelAuditEventsAsync(projectId, "WidthMm")
            + await context.CountPanelAuditEventsAsync(projectId, "HeightMm")
            + await context.CountPanelAuditEventsAsync(projectId, "DepthMm");
        var nameOnly = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Reason = "패널명만 변경",
                Panels = new[]
                {
                    new
                    {
                        PanelId = beforeNameOnly.GetProperty("panelId").GetGuid(),
                        ExpectedPanelInfoVersion = beforeNameOnly.GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "DRIFT-B" }
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, nameOnly.StatusCode);
        var after = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];
        Assert.Equal(800.125m, after.GetProperty("widthMm").GetDecimal());
        Assert.Equal(1800.375m, after.GetProperty("heightMm").GetDecimal());
        Assert.Equal(400.625m, after.GetProperty("depthMm").GetDecimal());
        var sizeAuditAfter = await context.CountPanelAuditEventsAsync(projectId, "WidthMm")
            + await context.CountPanelAuditEventsAsync(projectId, "HeightMm")
            + await context.CountPanelAuditEventsAsync(projectId, "DepthMm");
        Assert.Equal(sizeAuditBefore, sizeAuditAfter);
        Assert.Equal(1, await context.CountPanelAuditEventsAsync(projectId, "PanelName") - 1);
    }

    [Fact]
    public async Task PanelHistory_ReturnsDirectExcelAndLegacyAuditMetadata()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var adminClient = context.CreateClient("dev-admin");
        using var created = await CreateProjectAsync(salesClient, "PANEL-HISTORY-001", "Panel History", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var panel = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];
        var panelId = panel.GetProperty("panelId").GetGuid();

        var direct = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                Panels = new[]
                {
                    new
                    {
                        PanelId = panelId,
                        ExpectedPanelInfoVersion = panel.GetProperty("panelInfoVersion").GetInt32(),
                        PanelNameUpdate = new { IsChanged = true, Value = "HISTORY-A" },
                        SizeUpdate = new
                        {
                            IsChanged = true,
                            Clear = false,
                            InputUnit = "Mm",
                            Width = 800m,
                            Height = 1800m,
                            Depth = 400m
                        }
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);

        await context.InsertLegacyPanelAuditEventAsync(projectId, panelId);

        var beforeExcel = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];
        var file = CreateExcelFile(("No", "panel name", "w", "h", "d"), ["1", "HISTORY-B", "31.5", "70.875", "15.75"]);
        using var previewContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "audit.xlsx" },
            { new StringContent("Inch"), "inputUnit" }
        };
        var preview = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", previewContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        var versions = previewJson.RootElement.GetProperty("expectedPanelInfoVersions").EnumerateArray()
            .Select(item => new
            {
                PanelId = item.GetProperty("panelId").GetGuid(),
                ExpectedPanelInfoVersion = item.GetProperty("expectedPanelInfoVersion").GetInt32()
            })
            .ToArray();
        Assert.Equal(beforeExcel.GetProperty("panelInfoVersion").GetInt32(), versions[0].ExpectedPanelInfoVersion);

        using var applyContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "audit.xlsx" },
            { new StringContent("Inch"), "inputUnit" },
            { new StringContent(previewJson.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(previewJson.RootElement.GetProperty("expectedPackagingMethod").GetString()!), "expectedPackagingMethod" },
            { new StringContent("Excel audit reason"), "reason" },
            { new StringContent(JsonSerializer.Serialize(versions)), "expectedVersions" }
        };
        var apply = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/apply", applyContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);

        using var forbiddenHistoryResponse = await designClient.GetAsync($"/api/projects/{projectId}/panel-information/history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenHistoryResponse.StatusCode);

        using var historyResponse = await adminClient.GetAsync($"/api/projects/{projectId}/panel-information/history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using var history = await ReadJsonAsync(historyResponse);
        var batches = history.RootElement.GetProperty("excelImportBatches").EnumerateArray().ToList();
        Assert.Single(batches);
        var batchId = batches[0].GetProperty("importBatchId").GetGuid();
        Assert.Equal("audit.xlsx", batches[0].GetProperty("originalFileName").GetString());

        var events = history.RootElement.GetProperty("auditEvents").EnumerateArray().ToList();
        var groups = history.RootElement.GetProperty("groups").EnumerateArray().ToList();
        Assert.Contains(groups, item =>
            item.TryGetProperty("inputSource", out var excelSource)
            && excelSource.GetString() == "Excel"
            && item.TryGetProperty("importBatchId", out var importBatchId)
            && importBatchId.GetGuid() == batchId
            && item.GetProperty("changeCount").GetInt32() == 4);
        Assert.Contains(groups, item =>
            item.TryGetProperty("inputSource", out var directSource)
            && directSource.GetString() == "Direct"
            && item.GetProperty("affectedPanelCount").GetInt32() == 1);
        var directName = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "PanelName"
            && item.GetProperty("newValue").GetString() == "HISTORY-A");
        Assert.Equal("Direct", directName.GetProperty("inputSource").GetString());
        Assert.False(directName.TryGetProperty("importBatchId", out _));
        Assert.False(directName.TryGetProperty("importFileName", out _));
        Assert.True(directName.TryGetProperty("correlationId", out var directCorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(directCorrelationId.GetString()));

        var excelName = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "PanelName"
            && item.GetProperty("newValue").GetString() == "HISTORY-B");
        Assert.Equal("Excel", excelName.GetProperty("inputSource").GetString());
        Assert.Equal(batchId, excelName.GetProperty("importBatchId").GetGuid());
        Assert.Equal("audit.xlsx", excelName.GetProperty("importFileName").GetString());
        Assert.False(excelName.TryGetProperty("inputUnit", out _));
        Assert.Equal("HISTORY-A", excelName.GetProperty("oldValue").GetString());

        var width = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "WidthMm"
            && item.GetProperty("newValue").GetString() == "800.1");
        var height = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "HeightMm"
            && item.GetProperty("newValue").GetString() == "1800.225");
        var depth = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "DepthMm"
            && item.GetProperty("newValue").GetString() == "400.05");
        Assert.Equal("31.5", width.GetProperty("originalInputValue").GetString());
        Assert.Equal("Inch", width.GetProperty("inputUnit").GetString());
        Assert.Equal(batchId, width.GetProperty("importBatchId").GetGuid());
        Assert.Equal(width.GetProperty("correlationId").GetString(), height.GetProperty("correlationId").GetString());
        Assert.Equal(width.GetProperty("correlationId").GetString(), depth.GetProperty("correlationId").GetString());
        Assert.Equal(batchId, height.GetProperty("importBatchId").GetGuid());
        Assert.Equal(batchId, depth.GetProperty("importBatchId").GetGuid());

        var legacy = events.Single(item =>
            item.GetProperty("fieldName").GetString() == "PanelName"
            && item.GetProperty("newValue").GetString() == "LEGACY");
        Assert.False(legacy.TryGetProperty("inputSource", out _));
        Assert.False(legacy.TryGetProperty("importBatchId", out _));
        Assert.False(legacy.TryGetProperty("inputUnit", out _));
    }

    [Theory]
    [InlineData("StretchWrap", "WoodenCrate")]
    [InlineData("WoodenCrate", "StretchWrap")]
    public async Task ExcelApply_RejectsWhenPackagingChangedAfterPreview(string previewPackaging, string currentPackaging)
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, $"PANEL-PKG-{previewPackaging}", $"Panel Package {previewPackaging}", previewPackaging, 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var file = previewPackaging == "WoodenCrate"
            ? CreateExcelFile(("No", "panel name", "w", "h", "d"), ["1", "PKG-1", "100", "200", "300"])
            : CreateExcelFile(("No", "panel name", "w", "h", "d"), ["1", "PKG-1", "", "", ""]);

        using var previewContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "design.xlsx" },
            { new StringContent("Mm"), "inputUnit" }
        };
        var preview = await designClient.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/preview",
            previewContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorCount").GetInt32());

        await context.SetPackagingMethodAsync(projectId, currentPackaging);
        var versions = previewJson.RootElement.GetProperty("expectedPanelInfoVersions").EnumerateArray()
            .Select(item => new
            {
                PanelId = item.GetProperty("panelId").GetGuid(),
                ExpectedPanelInfoVersion = item.GetProperty("expectedPanelInfoVersion").GetInt32()
            })
            .ToArray();
        using var applyContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "design.xlsx" },
            { new StringContent("Mm"), "inputUnit" },
            { new StringContent(previewJson.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(previewJson.RootElement.GetProperty("expectedPackagingMethod").GetString()!), "expectedPackagingMethod" },
            { new StringContent(JsonSerializer.Serialize(versions)), "expectedVersions" }
        };

        var apply = await designClient.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/apply",
            applyContent,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, apply.StatusCode);
        Assert.Equal(0, await context.CountExcelImportBatchesAsync(projectId));
        Assert.Equal(0, await context.CountPanelAuditEventsAsync(projectId));
        var panel = (await ReadPanelInformationAsync(designClient, projectId)).RootElement.GetProperty("panels")[0];
        Assert.Equal(JsonValueKind.Null, panel.GetProperty("panelName").ValueKind);
    }

    [Fact]
    public async Task ExcelApply_RejectsStateAndPanelVersionChangesAfterPreview()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-TOCTOU-STATE", "Panel State TocTou", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        var file = CreateExcelFile(("No", "panel name", "w", "h", "d"), ["1", "STATE-1", "", "", ""]);
        using var previewContent = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "design.xlsx" } };
        var preview = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", previewContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        var versions = previewJson.RootElement.GetProperty("expectedPanelInfoVersions").EnumerateArray()
            .Select(item => new
            {
                PanelId = item.GetProperty("panelId").GetGuid(),
                ExpectedPanelInfoVersion = item.GetProperty("expectedPanelInfoVersion").GetInt32()
            })
            .ToArray();

        await context.TouchPanelVersionAsync(versions[0].PanelId);
        using var applyContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", "design.xlsx" },
            { new StringContent(previewJson.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(previewJson.RootElement.GetProperty("expectedPackagingMethod").GetString()!), "expectedPackagingMethod" },
            { new StringContent(JsonSerializer.Serialize(versions)), "expectedVersions" }
        };
        var staleApply = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/apply", applyContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleApply.StatusCode);
        Assert.Equal(0, await context.CountExcelImportBatchesAsync(projectId));
    }

    [Fact]
    public async Task ExcelPreview_SelectsVisibleDataSheetAndRejectsDuplicateHeadersAndLargeRanges()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-PARSER-001", "Panel Parser", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var secondSheetFile = CreateWorkbook(workbook =>
        {
            workbook.AddWorksheet("Guide").Cell(1, 1).Value = "guide only";
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(3, 1).Value = "No";
            worksheet.Cell(3, 2).Value = "도번";
            worksheet.Cell(3, 3).Value = "panel name";
            worksheet.Cell(4, 1).Value = 1;
            worksheet.Cell(4, 3).Value = "SHEET-1";
        });
        using var okContent = new MultipartFormDataContent { { new ByteArrayContent(secondSheetFile), "file", "sheet.xlsx" } };
        var ok = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", okContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var okJson = await ReadJsonAsync(ok);
        Assert.Equal(1, okJson.RootElement.GetProperty("newCount").GetInt32());

        var duplicateHeaderFile = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Cell(1, 2).Value = "No";
            worksheet.Cell(1, 3).Value = "panel name";
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 3).Value = "DUP";
        });
        using var duplicateContent = new MultipartFormDataContent { { new ByteArrayContent(duplicateHeaderFile), "file", "duplicate.xlsx" } };
        var duplicate = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", duplicateContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var tooManyColumns = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Cell(1, 2).Value = "panel name";
            worksheet.Cell(1, 65).Value = "extra";
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = "WIDE";
        });
        using var wideContent = new MultipartFormDataContent { { new ByteArrayContent(tooManyColumns), "file", "wide.xlsx" } };
        var wide = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", wideContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, wide.StatusCode);

        using var corruptContent = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3, 4]), "file", "corrupt.xlsx" } };
        var corrupt = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", corruptContent, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, corrupt.StatusCode);
    }

    [Fact]
    public async Task ExcelPreview_HandlesSheetAndHeaderEdgeCasesDeterministically()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-PARSER-EDGE", "Panel Parser Edge", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var hiddenFirst = CreateWorkbook(workbook =>
        {
            var hidden = workbook.AddWorksheet("Hidden Data");
            hidden.Cell(1, 1).Value = "No";
            hidden.Cell(1, 2).Value = "panel name";
            hidden.Visibility = XLWorksheetVisibility.Hidden;
            AddBasicPanelSheet(workbook.AddWorksheet("Panel Information"), "VISIBLE-HIDDEN");
        });
        Assert.Equal(HttpStatusCode.OK, (await PreviewAsync(designClient, projectId, hiddenFirst, "hidden.xlsx")).StatusCode);

        var veryHidden = CreateWorkbook(workbook =>
        {
            var concealed = workbook.AddWorksheet("Concealed");
            AddBasicPanelSheet(concealed, "VERY-HIDDEN");
            concealed.Visibility = XLWorksheetVisibility.VeryHidden;
            AddBasicPanelSheet(workbook.AddWorksheet("Visible Data"), "VISIBLE-OK");
        });
        Assert.Equal(HttpStatusCode.OK, (await PreviewAsync(designClient, projectId, veryHidden, "very-hidden.xlsx")).StatusCode);

        var ambiguous = CreateWorkbook(workbook =>
        {
            AddBasicPanelSheet(workbook.AddWorksheet("Data A"), "A");
            AddBasicPanelSheet(workbook.AddWorksheet("Data B"), "B");
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, ambiguous, "ambiguous.xlsx")).StatusCode);

        var lateHeader = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(21, 1).Value = "No";
            worksheet.Cell(21, 2).Value = "panel name";
            worksheet.Cell(22, 1).Value = 1;
            worksheet.Cell(22, 2).Value = "LATE";
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, lateHeader, "late.xlsx")).StatusCode);

        var mergedNo = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Range(1, 1, 1, 2).Merge();
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Cell(1, 3).Value = "panel name";
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 3).Value = "MERGED-NO";
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, mergedNo, "merged-no.xlsx")).StatusCode);

        var mergedName = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Range(1, 2, 1, 3).Merge();
            worksheet.Cell(1, 2).Value = "panel name";
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = "MERGED-NAME";
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, mergedName, "merged-name.xlsx")).StatusCode);
        Assert.Equal(0, await context.CountExcelImportBatchesAsync(projectId));
        Assert.Equal(0, await context.CountPanelAuditEventsAsync(projectId));
    }

    [Fact]
    public async Task ExcelPreview_AllowsSequenceNumberGreaterThanFiveHundredWhenRowCountIsWithinLimit()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-NO-501", "Panel No 501", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        await context.AddPanelPlaceholderAsync(projectId, 501);
        var file = CreateExcelFile(("No", "panel name", "w", "h", "d"), ["501", "SEQ-501", "", "", ""]);

        using var content = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "seq501.xlsx" } };
        var preview = await designClient.PostAsync($"/api/projects/{projectId}/panel-information/import/preview", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = await ReadJsonAsync(preview);
        Assert.Equal(0, json.RootElement.GetProperty("errorCount").GetInt32());
        Assert.Equal(501, json.RootElement.GetProperty("rows")[0].GetProperty("no").GetInt32());
    }

    [Fact]
    public async Task ExcelPreview_RejectsResourceLimitViolationsBeforeApply()
    {
        await using var context = await PanelInfoTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var created = await CreateProjectAsync(salesClient, "PANEL-RESOURCE-001", "Panel Resource", "StretchWrap", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var tooManyRows = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Cell(1, 2).Value = "panel name";
            for (var row = 2; row <= 502; row++)
            {
                worksheet.Cell(row, 1).Value = row - 1;
                worksheet.Cell(row, 2).Value = $"ROW-{row}";
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, tooManyRows, "rows.xlsx")).StatusCode);

        var tooManyWorksheets = CreateWorkbook(workbook =>
        {
            for (var index = 1; index <= 21; index++)
            {
                var worksheet = workbook.AddWorksheet(index == 1 ? "Panel Information" : $"Sheet {index}");
                worksheet.Cell(1, 1).Value = "No";
                worksheet.Cell(1, 2).Value = "panel name";
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, tooManyWorksheets, "sheets.xlsx")).StatusCode);

        var formula = CreateWorkbook(workbook =>
        {
            var worksheet = workbook.AddWorksheet("Panel Information");
            worksheet.Cell(1, 1).Value = "No";
            worksheet.Cell(1, 2).Value = "panel name";
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).FormulaA1 = "\"FORMULA\"";
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, formula, "formula.xlsx")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, CreateZipWithEntries(2001), "entries.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, CreateZipWithEntry("xl/externalLinks/externalLink1.xml", [1, 2, 3]), "external.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, CreateZipWithEntry("xl/embeddings/oleObject1.bin", [1, 2, 3]), "ole.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, CreateZipWithEntry("xl/worksheets/sheet1.xml", new byte[(20 * 1024 * 1024) + 1]), "entry-size.xlsx")).StatusCode);

        var tooLargeFile = new byte[PanelInformationDomain.MaxExcelFileSizeBytes + 1];
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewAsync(designClient, projectId, tooLargeFile, "too-large.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(designClient, projectId, tooLargeFile, "too-large-apply.xlsx")).StatusCode);
        Assert.Equal(0, await context.CountExcelImportBatchesAsync(projectId));
        Assert.Equal(0, await context.CountPanelAuditEventsAsync(projectId));
    }

    [Fact]
    public async Task ExcelParser_ParseGateCancellationDoesNotLeakSlots()
    {
        using var gate = new SemaphoreSlim(0, 2);
        var parser = new PanelInformationExcelParser(gate);
        var file = new UploadedExcelFile("valid.xlsx", 4, new string('a', 64), [1, 2, 3, 4]);
        using var cts = new CancellationTokenSource();
        var waiting = parser.ParseAsync(file, cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await waiting);

        gate.Release(2);
        var parsed = await parser.ParseAsync(file, TestContext.Current.CancellationToken);
        Assert.NotEmpty(parsed.FileErrors);
        Assert.True(gate.Wait(TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.True(gate.Wait(TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    private static async Task<HttpResponseMessage> CreateProjectAsync(
        HttpClient client,
        string projectCode,
        string projectTitle,
        string packagingMethod,
        int panelCount)
    {
        return await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                CustomerName = "Panel Info Customer",
                Item = "UL67",
                ProjectCode = projectCode,
                ProjectTitle = projectTitle,
                PanelCount = panelCount,
                DeliveryDate = "2026-10-10",
                SalesOwnerUserId,
                PackagingMethod = packagingMethod,
                SalesAmount = (decimal?)null,
                CurrencyCode = (string?)null,
                DeliveryLocation = (string?)null
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadPanelInformationAsync(HttpClient client, Guid projectId)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}/panel-information", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<HttpResponseMessage> PreviewAsync(
        HttpClient client,
        Guid projectId,
        byte[] file,
        string fileName)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(file), "file", fileName } };
        return await client.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/preview",
            content,
            TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> ApplyAsync(
        HttpClient client,
        Guid projectId,
        byte[] file,
        string fileName)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", fileName },
            { new StringContent(new string('a', 64)), "expectedFileSha256" },
            { new StringContent("StretchWrap"), "expectedPackagingMethod" },
            { new StringContent("[]"), "expectedVersions" }
        };
        return await client.PostAsync(
            $"/api/projects/{projectId}/panel-information/import/apply",
            content,
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static byte[] CreateExcelFile(
        (string No, string PanelName, string Width, string Height, string Depth) header,
        params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Panels");
        worksheet.Cell(1, 1).Value = header.No;
        worksheet.Cell(1, 2).Value = header.PanelName;
        worksheet.Cell(1, 3).Value = header.Width;
        worksheet.Cell(1, 4).Value = header.Height;
        worksheet.Cell(1, 5).Value = header.Depth;
        for (var index = 0; index < rows.Length; index++)
        {
            for (var column = 0; column < rows[index].Length; column++)
            {
                worksheet.Cell(index + 2, column + 1).Value = rows[index][column];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateWorkbook(Action<XLWorkbook> configure)
    {
        using var workbook = new XLWorkbook();
        configure(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddBasicPanelSheet(IXLWorksheet worksheet, string panelName)
    {
        worksheet.Cell(1, 1).Value = "No";
        worksheet.Cell(1, 2).Value = "panel name";
        worksheet.Cell(2, 1).Value = 1;
        worksheet.Cell(2, 2).Value = panelName;
    }

    private static byte[] CreateZipWithEntries(int entryCount)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index < entryCount; index++)
            {
                archive.CreateEntry($"entry-{index}.txt");
            }
        }

        return stream.ToArray();
    }

    private static byte[] CreateZipWithEntry(string entryName, byte[] content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            entryStream.Write(content);
        }

        return stream.ToArray();
    }

    private sealed class PanelInfoTestContext : IAsyncDisposable
    {
        private PanelInfoTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public static async Task<PanelInfoTestContext> CreateAsync()
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

            return new PanelInfoTestContext(database, factory);
        }

        public HttpClient CreateClient(string developmentUserKey)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
            return client;
        }

        public async Task<int> CountPanelAuditEventsAsync(Guid projectId)
        {
            return await ReadScalarAsync<int>(
                """
                select count(*)::integer
                from project_audit_events
                where project_id = @project_id
                  and entity_type = 'Panel'
                  and action = 'PanelInfoUpdated';
                """,
                projectId);
        }

        public async Task<int> CountPanelAuditEventsAsync(Guid projectId, string fieldName)
        {
            return await ReadScalarAsync<int>(
                """
                select count(*)::integer
                from project_audit_events
                where project_id = @project_id
                  and entity_type = 'Panel'
                  and action = 'PanelInfoUpdated'
                  and field_name = @field_name;
                """,
                command =>
                {
                    command.Parameters.AddWithValue("project_id", projectId);
                    command.Parameters.AddWithValue("field_name", fieldName);
                });
        }

        public async Task<int> CountExcelImportBatchesAsync(Guid projectId)
        {
            return await ReadScalarAsync<int>(
                """
                select count(*)::integer
                from panel_information_excel_import_batches
                where project_id = @project_id;
                """,
                projectId);
        }

        public async Task<bool> ExcelBinaryColumnExistsAsync()
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand("""
                select exists (
                    select 1
                    from information_schema.columns
                    where table_name = 'panel_information_excel_import_batches'
                      and column_name in ('content', 'binary', 'file_bytes', 'file_content')
                );
                """);
            return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? false);
        }

        public async Task CancelPanelAsync(Guid panelId)
        {
            await ExecuteAsync(
                """
                update panel_placeholders
                set status = 'Cancelled',
                    updated_at_utc = now()
                where id = @panel_id;
                """,
                command => command.Parameters.AddWithValue("panel_id", panelId));
        }

        public async Task SoftDeleteProjectAsync(Guid projectId)
        {
            await ExecuteAsync(
                """
                update projects
                set deleted_at_utc = now(),
                    delete_reason = 'template download test',
                    updated_at_utc = now()
                where id = @project_id;
                """,
                command => command.Parameters.AddWithValue("project_id", projectId));
        }

        public async Task SetPackagingMethodAsync(Guid projectId, string packagingMethod)
        {
            await ExecuteAsync(
                """
                update projects
                set packaging_method = @packaging_method,
                    updated_at_utc = now()
                where id = @project_id;
                """,
                command =>
                {
                    command.Parameters.AddWithValue("project_id", projectId);
                    command.Parameters.AddWithValue("packaging_method", packagingMethod);
                });
        }

        public async Task TouchPanelVersionAsync(Guid panelId)
        {
            await ExecuteAsync(
                """
                update panel_placeholders
                set panel_info_version = panel_info_version + 1,
                    updated_at_utc = now()
                where id = @panel_id;
                """,
                command => command.Parameters.AddWithValue("panel_id", panelId));
        }

        public async Task AddPanelPlaceholderAsync(Guid projectId, int sequenceNumber)
        {
            await ExecuteAsync(
                """
                insert into panel_placeholders (
                    id,
                    project_id,
                    sequence_number,
                    display_code,
                    status
                )
                values (
                    uuid_generate_v4(),
                    @project_id,
                    @sequence_number,
                    @display_code,
                    'Active'
                );
                """,
                command =>
                {
                    command.Parameters.AddWithValue("project_id", projectId);
                    command.Parameters.AddWithValue("sequence_number", sequenceNumber);
                    command.Parameters.AddWithValue("display_code", $"P{sequenceNumber:000}");
                });
        }

        public async Task InsertLegacyPanelAuditEventAsync(Guid projectId, Guid panelId)
        {
            await ExecuteAsync(
                """
                insert into project_audit_events (
                    project_id,
                    entity_type,
                    entity_id,
                    action,
                    field_name,
                    old_value,
                    new_value,
                    reason,
                    changed_by_user_id,
                    correlation_id,
                    is_sensitive
                )
                values (
                    @project_id,
                    'Panel',
                    @panel_id,
                    'PanelInfoUpdated',
                    'PanelName',
                    null,
                    'LEGACY',
                    null,
                    null,
                    'legacy-correlation',
                    false
                );
                """,
                command =>
                {
                    command.Parameters.AddWithValue("project_id", projectId);
                    command.Parameters.AddWithValue("panel_id", panelId);
                });
        }

        private async Task<T> ReadScalarAsync<T>(string commandText, Guid projectId)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand(commandText);
            command.Parameters.AddWithValue("project_id", projectId);
            return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("No scalar value returned."));
        }

        private async Task<T> ReadScalarAsync<T>(string commandText, Action<NpgsqlCommand> configure)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand(commandText);
            configure(command);
            return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("No scalar value returned."));
        }

        private async Task ExecuteAsync(string commandText, Action<NpgsqlCommand> configure)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand(commandText);
            configure(command);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string repositoryRoot, string databaseName, IConfiguration baseConfiguration)
        {
            RepositoryRoot = repositoryRoot;
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        private string RepositoryRoot { get; }
        private string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }
        public string ConnectionString => BuildConnectionString(BaseConfiguration, DatabaseName);

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

            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
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

            throw new InvalidOperationException("Repository root could not be found.");
        }
    }
}
