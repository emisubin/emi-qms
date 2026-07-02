using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProcurementApiTests
{
    private static readonly Guid SalesOwnerUserId = new("50000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ProcurementAuthorization_EnforcesReadUpdateReceiptAndHistoryPolicies()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "PROC-AUTH", "Proc Auth");

        foreach (var userKey in new[]
        {
            "dev-sales", "dev-design", "dev-procurement", "dev-materials", "dev-production",
            "dev-manufacturing", "dev-quality", "dev-logistics", "dev-viewer", "dev-admin"
        })
        {
            using var client = context.CreateClient(userKey);
            var read = await client.GetAsync($"/api/projects/{projectId}/procurement", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        var procurementUpdate = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "initial procurement",
                items = new[] { new { orderItem = "MCCB" } }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, procurementUpdate.StatusCode);
        var created = await ReadProcurementAsync(procurementClient, projectId);
        var item = created.RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();
        var rowVersion = item.GetProperty("rowVersion").GetInt32();

        foreach (var userKey in new[]
        {
            "dev-sales", "dev-design", "dev-materials", "dev-production",
            "dev-manufacturing", "dev-quality", "dev-logistics", "dev-viewer", "dev-admin"
        })
        {
            using var client = context.CreateClient(userKey);
            var denied = await client.PatchAsJsonAsync(
                $"/api/projects/{projectId}/procurement",
                new { items = new[] { new { orderItem = "SHOULD-NOT-SAVE" } } },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        foreach (var userKey in new[] { "dev-procurement", "dev-materials" })
        {
            using var client = context.CreateClient(userKey);
            var latest = await ReadProcurementAsync(procurementClient, projectId);
            var latestItem = latest.RootElement.GetProperty("items")[0];
            var receipt = await client.PatchAsJsonAsync(
                "/api/materials/receipts",
                new
                {
                    reason = "receipt update",
                    items = new[]
                    {
                        new
                        {
                            itemId,
                            expectedRowVersion = latestItem.GetProperty("rowVersion").GetInt32(),
                            receiptCompleted = true,
                            receiptCompletionNote = $"received by {userKey}"
                        }
                    }
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        }

        foreach (var userKey in new[] { "dev-sales", "dev-design", "dev-manufacturing", "dev-quality", "dev-viewer", "dev-admin" })
        {
            using var client = context.CreateClient(userKey);
            var denied = await client.PatchAsJsonAsync(
                "/api/materials/receipts",
                new { items = new[] { new { itemId, expectedRowVersion = rowVersion, receiptCompleted = false } }, reason = "deny" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"/api/projects/{projectId}/procurement/history", TestContext.Current.CancellationToken)).StatusCode);
        foreach (var userKey in new[] { "dev-procurement", "dev-materials", "dev-sales", "dev-viewer" })
        {
            using var client = context.CreateClient(userKey);
            var denied = await client.GetAsync($"/api/projects/{projectId}/procurement/history", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [Fact]
    public async Task ProcurementDirectInput_AllowsOptionalFieldsSkipsEmptyRowsAndAuditsChangedFields()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "PROC-DIRECT", "Proc Direct");

        var response = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "single field rows",
                items = new object[]
                {
                    new { standardLeadTime = "4W" },
                    new { orderItem = "Cable" },
                    new { supplierName = "Vendor A" },
                    new { technicalOwner = "Engineer A" },
                    new { orderDate = "2026-07-01" },
                    new { expectedReceiptDate = "2026-07-05" },
                    new { issueNote = "확인 필요" },
                    new { receiptCompleted = true },
                    new { }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var items = procurement.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(8, items.Count);
        Assert.Equal("2026-10-10", procurement.RootElement.GetProperty("projectDeliveryDate").GetString());
        Assert.Equal("4W", items[0].GetProperty("standardLeadTime").GetString());
        Assert.Equal("Cable", items[1].GetProperty("orderItem").GetString());
        Assert.Equal("Vendor A", items[2].GetProperty("supplierName").GetString());
        Assert.Equal("Engineer A", items[3].GetProperty("technicalOwner").GetString());
        Assert.Equal("2026-07-01", items[4].GetProperty("orderDate").GetString());
        Assert.Equal("2026-07-05", items[5].GetProperty("expectedReceiptDate").GetString());
        Assert.Equal("2026-10-10", items[5].GetProperty("shipmentDisplayDate").GetString());
        Assert.Equal("확인 필요", items[6].GetProperty("issueNote").GetString());
        Assert.True(items[7].GetProperty("receiptCompleted").GetBoolean());

        var first = items[0];
        var edit = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "change one field",
                items = new[]
                {
                    new
                    {
                        itemId = first.GetProperty("itemId").GetGuid(),
                        expectedRowVersion = first.GetProperty("rowVersion").GetInt32(),
                        standardLeadTime = "6W"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var history = await ReadJsonAsync(await adminClient.GetAsync($"/api/projects/{projectId}/procurement/history", TestContext.Current.CancellationToken));
        var latestGroup = history.RootElement.GetProperty("groups")[0];
        Assert.Equal(1, latestGroup.GetProperty("affectedItemCount").GetInt32());
        Assert.Equal(1, latestGroup.GetProperty("changeCount").GetInt32());
        Assert.Equal("StandardLeadTime", latestGroup.GetProperty("changes")[0].GetProperty("fieldName").GetString());
    }

    [Fact]
    public async Task MaterialReceipt_CheckManualDateKeepsCompletedAndBlocksUncheck()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        var projectId = await CreateProjectAsync(salesClient, "PROC-RECEIPT", "Proc Receipt");
        await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { orderItem = "Terminal Block" } } },
            TestContext.Current.CancellationToken);
        var item = (await ReadProcurementAsync(procurementClient, projectId)).RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();

        var manualCompletedAt = new DateTimeOffset(2026, 7, 15, 3, 0, 0, TimeSpan.Zero);
        var receipt = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new
            {
                reason = "materials receipt",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                        receiptCompleted = true,
                        receiptCompletedAtUtc = manualCompletedAt,
                        receiptCompletionNote = "dock A"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var receivedItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.True(receivedItem.GetProperty("receiptCompleted").GetBoolean());
        Assert.Equal(manualCompletedAt, receivedItem.GetProperty("receiptCompletedAtUtc").GetDateTimeOffset());
        Assert.Equal("Dev Materials User", receivedItem.GetProperty("receiptCompletedByUserName").GetString());
        Assert.Equal("dock A", receivedItem.GetProperty("receiptCompletionNote").GetString());

        var localOffsetCompletedAt = new DateTimeOffset(2026, 7, 16, 9, 30, 0, TimeSpan.FromHours(9));
        var editCompleted = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new
            {
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = receivedItem.GetProperty("rowVersion").GetInt32(),
                        receiptCompleted = true,
                        receiptCompletedAtUtc = localOffsetCompletedAt,
                        receiptCompletionNote = "dock B"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, editCompleted.StatusCode);
        var editedCompletedItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.True(editedCompletedItem.GetProperty("receiptCompleted").GetBoolean());
        Assert.Equal(localOffsetCompletedAt.ToUniversalTime(), editedCompletedItem.GetProperty("receiptCompletedAtUtc").GetDateTimeOffset());
        Assert.Equal("dock B", editedCompletedItem.GetProperty("receiptCompletionNote").GetString());

        var uncheckWithoutReason = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new
            {
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = editedCompletedItem.GetProperty("rowVersion").GetInt32(),
                        receiptCompleted = false
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, uncheckWithoutReason.StatusCode);
        var uncheckedItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.False(uncheckedItem.GetProperty("receiptCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, uncheckedItem.GetProperty("receiptCompletedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, uncheckedItem.GetProperty("receiptCompletedByUserId").ValueKind);
        Assert.Equal("dock B", uncheckedItem.GetProperty("receiptCompletionNote").GetString());

        var uncheckWithReason = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new
            {
                reason = "received again",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = uncheckedItem.GetProperty("rowVersion").GetInt32(),
                        receiptCompleted = true,
                        receiptCompletionNote = "dock C"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, uncheckWithReason.StatusCode);
        var completedAgainItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.True(completedAgainItem.GetProperty("receiptCompleted").GetBoolean());
        Assert.Equal("dock C", completedAgainItem.GetProperty("receiptCompletionNote").GetString());

        var bodyEdit = await materialsClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { itemId, standardLeadTime = "SHOULD-NOT" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, bodyEdit.StatusCode);
    }

    [Fact]
    public async Task ProcurementResponse_BuildsDDayTextWithoutReceiptStatusWords()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        var projectId = await CreateProjectAsync(salesClient, "PROC-DDAY", "Proc Dday");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var save = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new object[]
                {
                    new { orderItem = "No date" },
                    new { expectedReceiptDate = today.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                    new { expectedReceiptDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                    new { expectedReceiptDate = today.AddDays(-2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var ddayTexts = procurement.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("dDayText").GetString())
            .ToList();
        Assert.Equal(["-", "D-3", "D-Day", "예정일 2일 경과"], ddayTexts);
        var json = procurement.RootElement.GetRawText();
        Assert.DoesNotContain("미입고", json, StringComparison.Ordinal);
        Assert.DoesNotContain("입고지연", json, StringComparison.Ordinal);
        Assert.DoesNotContain("부분입고", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcurementExcel_PreviewsAppliesReuploadsAndAllowsDuplicateFileWhenComparedWithCurrentData()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        var projectId = await CreateProjectAsync(salesClient, "PROC-EXCEL", "Proc Excel");
        var firstFile = CreateProcurementExcel("Proc Excel", "PROC-EXCEL",
            ["Proc Excel", "PROC-EXCEL", "4W", "MCCB", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "First", "Y"],
            ["", "", "5W", "Cable", "", "Owner B", "2026.07.02", "2026.07.11", "", ""],
            [" ", "", "", "", "", "", "", "", "", ""]);

        var preview = await PreviewExcelAsync(procurementClient, firstFile, "procurement.xlsx");
        Assert.Equal(2, preview.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("skippedCount").GetInt32());
        Assert.Equal("Matched", preview.RootElement.GetProperty("projectMatches")[0].GetProperty("matchStatus").GetString());

        var apply = await ApplyExcelAsync(procurementClient, firstFile, "procurement.xlsx", preview, reason: null);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        var saved = await ReadProcurementAsync(procurementClient, projectId);
        Assert.Equal(2, saved.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("Vendor X", saved.RootElement.GetProperty("items")[0].GetProperty("supplierName").GetString());
        Assert.Equal("2026-07-10", saved.RootElement.GetProperty("items")[0].GetProperty("expectedReceiptDate").GetString());
        Assert.Equal("2026-10-10", saved.RootElement.GetProperty("items")[0].GetProperty("shipmentDisplayDate").GetString());
        Assert.DoesNotContain(saved.RootElement.GetProperty("items")[0].EnumerateObject(), property => property.NameEquals("shipmentText"));

        var duplicatePreview = await PreviewExcelAsync(procurementClient, firstFile, "procurement.xlsx");
        var duplicate = await ApplyExcelAsync(procurementClient, firstFile, "procurement.xlsx", duplicatePreview, reason: null);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(0, duplicatePreview.RootElement.GetProperty("newCount").GetInt32() + duplicatePreview.RootElement.GetProperty("changedCount").GetInt32());

        var secondFile = CreateProcurementExcel("Proc Excel", "PROC-EXCEL",
            ["Proc Excel", "PROC-EXCEL", "4W", "MCCB", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "First changed", "Y"],
            ["", "", "6W", "New item", "", "Owner C", "2026/07/03", "2026/07/12", "New", "N"]);
        var reuploadPreview = await PreviewExcelAsync(procurementClient, secondFile, "procurement-updated.xlsx");
        Assert.Equal(1, reuploadPreview.RootElement.GetProperty("changedCount").GetInt32());
        Assert.Equal(1, reuploadPreview.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(1, reuploadPreview.RootElement.GetProperty("missingFromUploadCount").GetInt32());
        Assert.Contains(reuploadPreview.RootElement.GetProperty("rows").EnumerateArray(), row => row.GetProperty("resultType").GetString() == "MissingFromUpload");

        var reapply = await ApplyExcelAsync(procurementClient, secondFile, "procurement-updated.xlsx", reuploadPreview, "changed receipt date");
        Assert.Equal(HttpStatusCode.OK, reapply.StatusCode);
        var afterReapply = await ReadProcurementAsync(procurementClient, projectId);
        Assert.Equal(3, afterReapply.RootElement.GetProperty("items").GetArrayLength());
        Assert.Contains(afterReapply.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("orderItem").GetString() == "Cable");
    }

    [Fact]
    public async Task ProcurementExcel_ProjectMatchingDateReceiptAndSecurityRules_AreEnforced()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        await CreateProjectAsync(salesClient, "PROC-MATCH", "Exact Procurement Match");
        var codeMatchedProjectId = await CreateProjectAsync(salesClient, "PROC-CODE", "Different Title");

        var codeCandidateFile = CreateProcurementExcel("Unknown By Code", "PROC-CODE",
            ["Unknown By Code", "PROC-CODE", "4W", "Candidate", "", "", "", "", "", ""]);
        var codePreview = await PreviewExcelAsync(procurementClient, codeCandidateFile, "code.xlsx");
        var match = codePreview.RootElement.GetProperty("projectMatches")[0];
        Assert.Equal("Matched", match.GetProperty("matchStatus").GetString());
        Assert.Equal(codeMatchedProjectId, match.GetProperty("matchedProjectId").GetGuid());
        Assert.Equal(1, codePreview.RootElement.GetProperty("newCount").GetInt32());

        await CreateProjectAsync(salesClient, "PROC-DUP-CODE", "Duplicate Code A");
        await CreateProjectAsync(salesClient, "PROC-DUP-CODE", "Duplicate Code B");
        var duplicateCodeFile = CreateProcurementExcel("Unknown Duplicate Code", "PROC-DUP-CODE",
            ["Unknown Duplicate Code", "PROC-DUP-CODE", "4W", "Needs Choice", "", "", "", "", "", ""]);
        var duplicateCodePreview = await PreviewExcelAsync(procurementClient, duplicateCodeFile, "duplicate-code.xlsx");
        var duplicateCodeMatch = duplicateCodePreview.RootElement.GetProperty("projectMatches")[0];
        Assert.Equal("NeedsReview", duplicateCodeMatch.GetProperty("matchStatus").GetString());
        Assert.Equal(2, duplicateCodeMatch.GetProperty("candidates").GetArrayLength());

        var unknownFile = CreateProcurementExcel("No Such Project", "NO-SUCH",
            ["No Such Project", "NO-SUCH", "", "Unknown", "", "", "", "", "", ""]);
        var unknownPreview = await PreviewExcelAsync(procurementClient, unknownFile, "unknown.xlsx");
        Assert.Equal("Unmatched", unknownPreview.RootElement.GetProperty("projectMatches")[0].GetProperty("matchStatus").GetString());
        Assert.Equal(1, unknownPreview.RootElement.GetProperty("errorCount").GetInt32());
        var blocked = await ApplyExcelAsync(procurementClient, unknownFile, "unknown.xlsx", unknownPreview, reason: null);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        var badDate = CreateProcurementExcel("Exact Procurement Match", "PROC-MATCH",
            ["Exact Procurement Match", "PROC-MATCH", "", "Bad date", "", "", "06/07/2026", "", "", ""]);
        var badDatePreview = await PreviewExcelAsync(procurementClient, badDate, "bad-date.xlsx");
        Assert.Equal(1, badDatePreview.RootElement.GetProperty("errorCount").GetInt32());

        var formula = CreateProcurementExcel(
            "Exact Procurement Match",
            "PROC-MATCH",
            [["Exact Procurement Match", "PROC-MATCH", "", "Formula", "", "", "", "", "", ""]],
            configure: worksheet => worksheet.Cell(4, 4).FormulaA1 = "\"Formula Order\"");
        var formulaPreviewResponse = await PreviewExcelRawAsync(procurementClient, formula, "formula.xlsx");
        Assert.Equal(HttpStatusCode.OK, formulaPreviewResponse.StatusCode);
        var formulaPreview = await ReadJsonAsync(formulaPreviewResponse);
        Assert.Equal(1, formulaPreview.RootElement.GetProperty("errorCount").GetInt32());

        using var csv = new MultipartFormDataContent { { new ByteArrayContent("not,xlsx"u8.ToArray()), "file", "procurement.csv" } };
        var csvResponse = await procurementClient.PostAsync("/api/procurement/import/preview", csv, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, csvResponse.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, codeCandidateFile, "procurement.xls")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, codeCandidateFile, "procurement.xlsm")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, new byte[10 * 1024 * 1024 + 1], "too-large.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, "not a zip"u8.ToArray(), "broken.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, CreateZipWithEntry("xl/vbaProject.bin"), "macro.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, CreateZipWithEntry("xl/externalLinks/externalLink1.xml"), "external.xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PreviewExcelRawAsync(procurementClient, CreateZipWithEntry("xl/embeddings/oleObject1.bin"), "ole.xlsx")).StatusCode);
    }

    [Fact]
    public async Task ProcurementExcel_AllowsPartialApplyForSaveableRowsAndKeepsBlockedRowsUnchanged()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        var projectId = await CreateProjectAsync(salesClient, "PROC-PARTIAL", "Proc Partial");

        var mixedFile = CreateProcurementExcel("Proc Partial", "PROC-PARTIAL",
            ["Proc Partial", "PROC-PARTIAL", "4W", "Saveable Item", "", "Owner A", "", "2026-07-10", "", ""],
            ["Missing Project", "NO-SUCH-PARTIAL", "5W", "Blocked Item", "", "Owner B", "", "2026-07-11", "", ""]);

        var preview = await PreviewExcelAsync(procurementClient, mixedFile, "partial.xlsx");
        Assert.Equal(1, preview.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("errorCount").GetInt32());
        Assert.Contains(preview.RootElement.GetProperty("rows").EnumerateArray(), row =>
            row.GetProperty("resultType").GetString() == "Error"
            && row.GetProperty("errorMessages").EnumerateArray().Any(message => message.GetString() == "등록되지 않은 프로젝트입니다."));

        var apply = await ApplyExcelAsync(procurementClient, mixedFile, "partial.xlsx", preview, reason: null);
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        using var applyJson = await ReadJsonAsync(apply);
        Assert.Single(applyJson.RootElement.GetProperty("items").EnumerateArray());

        var saved = await ReadProcurementAsync(procurementClient, projectId);
        var savedItems = saved.RootElement.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(savedItems);
        Assert.Equal("Saveable Item", item.GetProperty("orderItem").GetString());
        Assert.DoesNotContain(saved.RootElement.GetRawText(), "Blocked Item", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcurementTemplate_DownloadsXlsxWithHeaderAndExistingRowsWithoutAudit()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "PROC-TEMPLATE", "Proc Template");
        await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        standardLeadTime = "3W",
                        orderItem = "Relay",
                        technicalOwner = "Owner",
                        orderDate = "2026-07-01",
                        expectedReceiptDate = "2026-07-10",
                        issueNote = "none",
                        receiptCompleted = true
                    }
                }
            },
            TestContext.Current.CancellationToken);

        foreach (var deniedClient in new[] { materialsClient, salesClient, adminClient })
        {
            var denied = await deniedClient.GetAsync($"/api/projects/{projectId}/procurement/import/template", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var globalDenied = await deniedClient.GetAsync("/api/procurement/import/template", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, globalDenied.StatusCode);
        }

        var globalResponse = await procurementClient.GetAsync("/api/procurement/import/template", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", globalResponse.Content.Headers.ContentType?.MediaType);

        var response = await procurementClient.GetAsync($"/api/projects/{projectId}/procurement/import/template", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet("Procurement Plan");
        Assert.Equal("PS 사업부 PJT 발주 관리", worksheet.Cell(1, 1).GetString());
        Assert.Contains("필수 입력값이 없습니다", worksheet.Cell(2, 1).GetString());
        Assert.Equal(new[] { "PJT", "PJT CODE", "통상납기", "발주품목", "업체", "기술 담당자", "발주일", "입고예정일", "이슈사항", "입고 완료" },
            Enumerable.Range(1, 10).Select(column => worksheet.Cell(3, column).GetString()).ToArray());
        Assert.Equal("Proc Template", worksheet.Cell(4, 1).GetString());
        Assert.Equal("Relay", worksheet.Cell(4, 4).GetString());
        Assert.Equal("none", worksheet.Cell(4, 9).GetString());
        Assert.Equal("Y", worksheet.Cell(4, 10).GetString());
        Assert.True(worksheet.SheetView.SplitRow >= 3);
        Assert.True(worksheet.AutoFilter.IsEnabled);
        Assert.True(worksheet.Column(4).Width >= 18);
        Assert.True(worksheet.Column(10).Width >= worksheet.Column(6).Width);
        for (var column = 1; column <= 10; column++)
        {
            Assert.True(worksheet.Column(column).Width >= 12);
            Assert.True(worksheet.Column(column).Width <= 42);
        }

        var history = await ReadJsonAsync(await adminClient.GetAsync($"/api/projects/{projectId}/procurement/history", TestContext.Current.CancellationToken));
        Assert.Single(history.RootElement.GetProperty("groups").EnumerateArray());
    }

    [Fact]
    public async Task ProcurementDashboardAndMaterialReceipts_FilterCompletedItemsByDefault()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        var projectId = await CreateProjectAsync(salesClient, "PROC-DASH", "Proc Dashboard");

        var save = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "dashboard setup",
                items = new[]
                {
                    new { orderItem = "Pending Item", expectedReceiptDate = "2026-07-10" },
                    new { orderItem = "Past Pending Item", expectedReceiptDate = "2026-06-20" },
                    new { orderItem = "Completed Item", expectedReceiptDate = "2026-06-20" }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        using var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var items = procurement.RootElement.GetProperty("items").EnumerateArray().ToList();
        var completedItem = items.Single(item => item.GetProperty("orderItem").GetString() == "Completed Item");
        var receipt = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new
            {
                reason = "received",
                items = new[]
                {
                    new
                    {
                        itemId = completedItem.GetProperty("itemId").GetGuid(),
                        expectedRowVersion = completedItem.GetProperty("rowVersion").GetInt32(),
                        receiptCompleted = true
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);

        using var defaultList = await ReadJsonAsync(await materialsClient.GetAsync("/api/materials/receipts?search=Proc%20Dashboard", TestContext.Current.CancellationToken));
        var defaultItems = defaultList.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, defaultItems.Count);
        Assert.Contains(defaultItems, item => item.GetProperty("orderItem").GetString() == "Pending Item");
        Assert.Contains(defaultItems, item => item.GetProperty("orderItem").GetString() == "Past Pending Item");

        using var includeCompletedList = await ReadJsonAsync(await materialsClient.GetAsync("/api/materials/receipts?search=Proc%20Dashboard&includeCompleted=true", TestContext.Current.CancellationToken));
        var allItems = includeCompletedList.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, allItems.Count);
        Assert.Contains(allItems, item => item.GetProperty("orderItem").GetString() == "Completed Item");

        using var dashboard = await ReadJsonAsync(await procurementClient.GetAsync("/api/procurement/dashboard?search=Proc%20Dashboard", TestContext.Current.CancellationToken));
        var summary = dashboard.RootElement.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("pendingReceiptCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("receiptCompletedCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("pastExpectedReceiptDateCount").GetInt32());
        Assert.False(summary.TryGetProperty("overdueStatus", out _));
        Assert.False(summary.GetRawText().Contains("입고지연", StringComparison.Ordinal));

        var project = Assert.Single(dashboard.RootElement.GetProperty("projects").EnumerateArray());
        Assert.Equal(projectId, project.GetProperty("projectId").GetGuid());
        Assert.Equal(3, project.GetProperty("procurementItemCount").GetInt32());
        Assert.Equal(1, project.GetProperty("receiptCompletedCount").GetInt32());
        Assert.Equal(1, project.GetProperty("pastExpectedReceiptDateCount").GetInt32());
        Assert.Equal("2026-06-20", project.GetProperty("nearestExpectedReceiptDate").GetString());
    }

    [Fact]
    public async Task ProcurementRequiredItemSettings_AreProcurementScopedAndDriveWorkflowCompletion()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var adminClient = context.CreateClient("dev-admin");
        using var viewerClient = context.CreateClient("dev-viewer");
        var projectId = await CreateProjectAsync(salesClient, "PROC-REQ", "Proc Required");

        using var settings = await ReadJsonAsync(await procurementClient.GetAsync("/api/procurement/settings/required-items", TestContext.Current.CancellationToken));
        Assert.Contains(settings.RootElement.EnumerateArray(), item => item.GetProperty("itemCode").GetString() == "UL67");
        Assert.Contains(settings.RootElement.EnumerateArray(), item => item.GetProperty("itemCode").GetString() == "RPP");
        Assert.DoesNotContain(settings.RootElement.EnumerateArray(), item => item.GetProperty("itemCode").GetString() == "RRP");
        Assert.DoesNotContain(settings.RootElement.EnumerateArray(), item => item.GetProperty("itemCode").GetString() == "TEST-TYPE");

        var deniedAdmin = await adminClient.PatchAsJsonAsync(
            "/api/procurement/settings/required-items/UL67",
            new { reason = "admin denied", rows = new[] { new { sequenceNumber = 1, itemName = "차단기", isRequired = true, isActive = true } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAdmin.StatusCode);

        var deniedViewer = await viewerClient.PatchAsJsonAsync(
            "/api/procurement/settings/required-items/UL67",
            new { reason = "viewer denied", rows = new[] { new { sequenceNumber = 1, itemName = "차단기", isRequired = true, isActive = true } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedViewer.StatusCode);

        var invalid = await procurementClient.PatchAsJsonAsync(
            "/api/procurement/settings/required-items/UL67",
            new { reason = "invalid", rows = new[] { new { sequenceNumber = 1, itemName = "   ", isRequired = true, isActive = true } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var savedSettings = await procurementClient.PatchAsJsonAsync(
            "/api/procurement/settings/required-items/UL67",
            new
            {
                reason = "required procurement items",
                rows = new[]
                {
                    new { sequenceNumber = 1, itemName = "차단기", isRequired = true, isActive = true },
                    new { sequenceNumber = 2, itemName = "외함", isRequired = true, isActive = true },
                    new { sequenceNumber = 3, itemName = "부자재", isRequired = false, isActive = true }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, savedSettings.StatusCode);
        using var savedJson = await ReadJsonAsync(savedSettings);
        var ul67 = savedJson.RootElement.EnumerateArray().Single(item => item.GetProperty("itemCode").GetString() == "UL67");
        Assert.Equal(3, ul67.GetProperty("rows").GetArrayLength());

        var savedAgain = await procurementClient.PatchAsJsonAsync(
            "/api/procurement/settings/required-items/UL67",
            new
            {
                reason = "required procurement items latest",
                rows = new[]
                {
                    new { sequenceNumber = 1, itemName = "차단기", isRequired = true, isActive = true },
                    new { sequenceNumber = 2, itemName = "외함", isRequired = true, isActive = true }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, savedAgain.StatusCode);
        using var savedAgainJson = await ReadJsonAsync(savedAgain);
        var latestUl67 = savedAgainJson.RootElement.EnumerateArray().Single(item => item.GetProperty("itemCode").GetString() == "UL67");
        var latestRows = latestUl67.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(2, latestRows.Count);
        Assert.DoesNotContain(latestRows, row => row.GetProperty("itemName").GetString() == "부자재");

        var newProjectId = await CreateProjectAsync(salesClient, "PROC-REQ-NEW", "Proc Required New");
        using var newProjectProcurement = await ReadProcurementAsync(procurementClient, newProjectId);
        var generatedItems = newProjectProcurement.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(generatedItems, item => item.GetProperty("orderItem").GetString() == "차단기");
        Assert.Contains(generatedItems, item => item.GetProperty("orderItem").GetString() == "외함");
        using var newProjectWorkflow = await ReadJsonAsync(await procurementClient.GetAsync($"/api/projects/{newProjectId}/workflow", TestContext.Current.CancellationToken));
        var generatedProcurementStage = newProjectWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "ProcurementInfo");
        Assert.NotEqual("Completed", generatedProcurementStage.GetProperty("status").GetString());

        var oneRequired = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { reason = "partial required procurement", items = new[] { new { orderItem = " 차단기 " } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, oneRequired.StatusCode);
        using var partialWorkflow = await ReadJsonAsync(await procurementClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var partialProcurementStage = partialWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "ProcurementInfo");
        Assert.Equal("InProgress", partialProcurementStage.GetProperty("status").GetString());

        var current = await ReadProcurementAsync(procurementClient, projectId);
        var existing = current.RootElement.GetProperty("items")[0];
        var completedRequired = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "all required procurement",
                items = new object[]
                {
                    new { itemId = existing.GetProperty("itemId").GetGuid(), expectedRowVersion = existing.GetProperty("rowVersion").GetInt32(), orderItem = "차단기" },
                    new { orderItem = "외함" }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completedRequired.StatusCode);
        using var completedWorkflow = await ReadJsonAsync(await procurementClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var completedProcurementStage = completedWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "ProcurementInfo");
        Assert.Equal("Completed", completedProcurementStage.GetProperty("status").GetString());
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string projectCode, string projectTitle)
    {
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                CustomerName = "Procurement Test Customer",
                Item = "UL67",
                ProjectCode = projectCode,
                ProjectTitle = projectTitle,
                PanelCount = 1,
                DeliveryDate = "2026-10-10",
                SalesOwnerUserId,
                PackagingMethod = "StretchWrap",
                SalesAmount = (decimal?)null,
                CurrencyCode = (string?)null,
                DeliveryLocation = (string?)null
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("projectId").GetGuid();
    }

    private static async Task<JsonDocument> ReadProcurementAsync(HttpClient client, Guid projectId)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}/procurement", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> FindMaterialItemAsync(HttpClient client, Guid itemId)
    {
        var response = await client.GetAsync("/api/materials/receipts?includeCompleted=true", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("itemId").GetGuid() == itemId);
    }

    private static async Task<JsonDocument> PreviewExcelAsync(HttpClient client, byte[] file, string fileName)
    {
        var response = await PreviewExcelRawAsync(client, file, fileName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<HttpResponseMessage> PreviewExcelRawAsync(HttpClient client, byte[] file, string fileName)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(file), "file", fileName } };
        return await client.PostAsync("/api/procurement/import/preview", content, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> ApplyExcelAsync(HttpClient client, byte[] file, string fileName, JsonDocument preview, string? reason)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(file), "file", fileName },
            { new StringContent(preview.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256" },
            { new StringContent(preview.RootElement.GetProperty("expectedVersions").GetRawText()), "expectedVersions" }
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            content.Add(new StringContent(reason), "reason");
        }

        return await client.PostAsync("/api/procurement/import/apply", content, TestContext.Current.CancellationToken);
    }

    private static byte[] CreateProcurementExcel(
        string projectTitle,
        string projectCode,
        params string[][] rows)
    {
        return CreateProcurementExcel(projectTitle, projectCode, rows, null);
    }

    private static byte[] CreateProcurementExcel(
        string projectTitle,
        string projectCode,
        string[][] rows,
        Action<IXLWorksheet>? configure)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Procurement Plan");
        worksheet.Cell(1, 1).Value = "PS 사업부 PJT 발주 관리";
        var headers = new[] { "PJT", "PJT CODE", "통상납기", "발주품목", "업체", "기술 담당자", "발주일", "입고예정일", "이슈사항", "입고 완료" };
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(3, column + 1).Value = headers[column];
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = NormalizeProcurementExcelRow(rows[rowIndex]);
            if (rowIndex == 0 && string.IsNullOrWhiteSpace(row[0]))
            {
                row[0] = projectTitle;
                row[1] = projectCode;
            }

            for (var column = 0; column < row.Length; column++)
            {
                worksheet.Cell(rowIndex + 4, column + 1).Value = row[column];
            }
        }

        configure?.Invoke(worksheet);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[] NormalizeProcurementExcelRow(string[] row)
    {
        return row;
    }

    private static byte[] CreateZipWithEntry(string entryName)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            entryStream.WriteByte(1);
        }

        return stream.ToArray();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class ProcurementApiTestContext : IAsyncDisposable
    {
        private ProcurementApiTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public static async Task<ProcurementApiTestContext> CreateAsync()
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

            return new ProcurementApiTestContext(database, factory);
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
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
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
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }
}
