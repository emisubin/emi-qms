using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Materials;
using Emi.Qms.Api.Projects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProcurementApiTests
{
    private static readonly Guid SalesOwnerUserId = new("50000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task DetailedIqcReport_RequiresChecklistPhotoAndReturnsStoredPdfBytes()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        var projectId = await context.CreateLegacyProjectAsync("PROC-IQC-REPORT", "IQC Report Flow");
        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Synthetic Enclosure", orderQuantity = 1, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        var item = (await ReadProcurementAsync(procurementClient, projectId)).RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();
        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-07-17" },
            TestContext.Current.CancellationToken);
        using var arrivalJson = await ReadJsonAsync(arrival);
        var receiptId = arrivalJson.RootElement.GetProperty("receiptId").GetGuid();
        var autoAttemptId = arrivalJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal("IqcRequested", arrivalJson.RootElement.GetProperty("status").GetString());
        var iqcRequest = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/iqc-requests",
            new { expectedVersion = 1 },
            TestContext.Current.CancellationToken);
        using var iqcRequestJson = await ReadJsonAsync(iqcRequest);
        var attemptId = iqcRequestJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal(autoAttemptId, attemptId);

        var bypass = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{attemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "사진 없는 우회 판정" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, bypass.StatusCode);

        var preview = await qualityClient.GetAsync($"/api/quality/iqc/{attemptId}/report", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = await ReadJsonAsync(preview);
        Assert.Equal("Detailed", previewJson.RootElement.GetProperty("decisionMode").GetString());
        Assert.Equal(JsonValueKind.Null, previewJson.RootElement.GetProperty("reportId").ValueKind);

        Assert.True(await context.IsIqcReportHiddenFromScopeAsync(attemptId, ["demo-project-alpha"]));
        using var readOnlyViewer = context.CreateClient("dev-viewer");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await readOnlyViewer.PostAsync($"/api/quality/iqc/{attemptId}/reports", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await readOnlyViewer.GetAsync($"/api/quality/iqc/{attemptId}/report", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await materialsClient.GetAsync($"/api/quality/iqc/{attemptId}/report", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await materialsClient.PostAsync($"/api/quality/iqc/{attemptId}/reports", null, TestContext.Current.CancellationToken)).StatusCode);

        var initialize = await qualityClient.PostAsync($"/api/quality/iqc/{attemptId}/reports", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        using var initializedJson = await ReadJsonAsync(initialize);
        var reportId = initializedJson.RootElement.GetProperty("reportId").GetGuid();
        var reportVersion = initializedJson.RootElement.GetProperty("reportVersion").GetInt32();
        var reinitialize = await qualityClient.PostAsync($"/api/quality/iqc/{attemptId}/reports", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reinitialize.StatusCode);
        using var reinitializedJson = await ReadJsonAsync(reinitialize);
        Assert.Equal(reportId, reinitializedJson.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(reportVersion, reinitializedJson.RootElement.GetProperty("reportVersion").GetInt32());
        var templateItems = initializedJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        var enclosureItemId = templateItems.Single(candidate => candidate.GetProperty("itemCode").GetString() == "ENCLOSURE").GetProperty("itemId").GetGuid();
        var responses = templateItems.Select(candidate => new
        {
            templateItemId = candidate.GetProperty("itemId").GetGuid(),
            checkResult = candidate.GetProperty("responseType").GetString() == "Check" ? "Pass" : null,
            textValue = candidate.GetProperty("responseType").GetString() == "Text" ? "합성 측정값 정상" : null,
            note = (string?)null
        }).ToArray();
        var save = await qualityClient.PutAsJsonAsync(
            $"/api/quality/iqc/reports/{reportId}/responses",
            new { expectedReportVersion = reportVersion, responses },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var saveJson = await ReadJsonAsync(save);
        reportVersion = saveJson.RootElement.GetProperty("reportVersion").GetInt32();

        var missingPhoto = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/reports/{reportId}/finalize",
            new { expectedReportVersion = reportVersion, expectedReceiptVersion = 2, result = "Passed", reason = "필수 사진 누락 확인" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingPhoto.StatusCode);

        using (var invalidPhotoForm = new MultipartFormDataContent())
        {
            invalidPhotoForm.Add(new StringContent(enclosureItemId.ToString("D")), "templateItemId");
            invalidPhotoForm.Add(new StringContent(reportVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
            invalidPhotoForm.Add(new StringContent("잘못된 이미지 확인"), "altText");
            var invalidContent = new ByteArrayContent("not-a-png"u8.ToArray());
            invalidContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            invalidPhotoForm.Add(invalidContent, "photo", "ignored.png");
            Assert.Equal(
                HttpStatusCode.BadRequest,
                (await qualityClient.PostAsync(
                    $"/api/quality/iqc/reports/{reportId}/photos",
                    invalidPhotoForm,
                    TestContext.Current.CancellationToken)).StatusCode);
        }

        var png = await File.ReadAllBytesAsync(
            Path.Combine(context.RepositoryRoot, "..", "frontend", "src", "assets", "emi-logo.png"),
            TestContext.Current.CancellationToken);
        using var photoForm = new MultipartFormDataContent();
        photoForm.Add(new StringContent(enclosureItemId.ToString("D")), "templateItemId");
        photoForm.Add(new StringContent(reportVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
        photoForm.Add(new StringContent("합성 외함 전체 상태"), "altText");
        var photoContent = new ByteArrayContent(png);
        photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        photoForm.Add(photoContent, "photo", "ignored-original-name.png");
        var upload = await qualityClient.PostAsync(
            $"/api/quality/iqc/reports/{reportId}/photos",
            photoForm,
            TestContext.Current.CancellationToken);
        Assert.True(
            upload.StatusCode == HttpStatusCode.OK,
            string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        using var uploadJson = await ReadJsonAsync(upload);
        reportVersion = uploadJson.RootElement.GetProperty("reportVersion").GetInt32();
        Assert.Equal("photo-1.png", uploadJson.RootElement.GetProperty("photos")[0].GetProperty("displayName").GetString());

        var finalize = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/reports/{reportId}/finalize",
            new { expectedReportVersion = reportVersion, expectedReceiptVersion = 2, result = "Passed", reason = "모든 필수 항목과 외함 사진 확인" },
            TestContext.Current.CancellationToken);
        Assert.True(
            finalize.StatusCode == HttpStatusCode.OK,
            string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        using var finalizeJson = await ReadJsonAsync(finalize);
        Assert.Equal("Finalized", finalizeJson.RootElement.GetProperty("reportStatus").GetString());
        Assert.Equal("Ready", finalizeJson.RootElement.GetProperty("pdfStatus").GetString());
        reportVersion = finalizeJson.RootElement.GetProperty("reportVersion").GetInt32();

        var immutable = await qualityClient.PutAsJsonAsync(
            $"/api/quality/iqc/reports/{reportId}/responses",
            new { expectedReportVersion = reportVersion, responses },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);

        var firstPdfResponse = await qualityClient.GetAsync($"/api/quality/iqc/reports/{reportId}/pdf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPdfResponse.StatusCode);
        var cacheControl = firstPdfResponse.Headers.CacheControl?.ToString() ?? "";
        Assert.Contains("private", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);
        var firstPdf = await firstPdfResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(await context.IsIqcPdfHiddenFromScopeAsync(reportId, ["demo-project-alpha"]));
        Assert.Equal(
            HttpStatusCode.OK,
            (await materialsClient.GetAsync($"/api/quality/iqc/reports/{reportId}/pdf", TestContext.Current.CancellationToken)).StatusCode);
        var secondPdf = await qualityClient.GetByteArrayAsync($"/api/quality/iqc/reports/{reportId}/pdf", TestContext.Current.CancellationToken);
        Assert.True(firstPdf.AsSpan().StartsWith("%PDF"u8));
        Assert.Equal(firstPdf, secondPdf);
    }

    [Fact]
    public async Task DetailedIqcReinspection_OnlyReturnsAndAcceptsPreviouslyFailedItems()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var coordinatorClient = context.CreateClient("dev-production");
        var projectId = await context.CreateLegacyProjectAsync("PROC-IQC-RECHECK", "IQC Reinspection Scope");
        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Nameplate", orderQuantity = 2, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        var procurementItem = (await ReadProcurementAsync(procurementClient, projectId)).RootElement.GetProperty("items")[0];
        var itemId = procurementItem.GetProperty("itemId").GetGuid();
        using var arrival = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 2, unit = "EA", arrivalDate = "2026-07-21" },
            TestContext.Current.CancellationToken));
        var receiptId = arrival.RootElement.GetProperty("receiptId").GetGuid();
        var firstAttemptId = arrival.RootElement.GetProperty("iqcAttemptId").GetGuid();

        using var first = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{firstAttemptId}/reports", null, TestContext.Current.CancellationToken));
        var firstReportId = first.RootElement.GetProperty("reportId").GetGuid();
        var firstReportVersion = first.RootElement.GetProperty("reportVersion").GetInt32();
        var firstItems = first.RootElement.GetProperty("items").EnumerateArray().ToList();
        var enclosureItemId = firstItems.Single(candidate => candidate.GetProperty("itemCode").GetString() == "ENCLOSURE").GetProperty("itemId").GetGuid();
        var failedItemId = firstItems.Single(candidate => candidate.GetProperty("itemCode").GetString() == "ITEM_SPEC").GetProperty("itemId").GetGuid();
        var firstResponses = firstItems.Select(candidate => new
        {
            templateItemId = candidate.GetProperty("itemId").GetGuid(),
            checkResult = candidate.GetProperty("responseType").GetString() == "Check"
                ? candidate.GetProperty("itemCode").GetString() == "ITEM_SPEC" ? "Fail" : "Pass"
                : null,
            textValue = candidate.GetProperty("responseType").GetString() == "Text" ? "측정값 정상" : null,
            note = candidate.GetProperty("itemCode").GetString() == "ITEM_SPEC" ? "명판 규격이 발주서와 다름" : null
        }).ToArray();
        using var savedFirst = await ReadJsonAsync(await qualityClient.PutAsJsonAsync(
            $"/api/quality/iqc/reports/{firstReportId}/responses",
            new { expectedReportVersion = firstReportVersion, responses = firstResponses },
            TestContext.Current.CancellationToken));
        firstReportVersion = savedFirst.RootElement.GetProperty("reportVersion").GetInt32();

        var png = await File.ReadAllBytesAsync(
            Path.Combine(context.RepositoryRoot, "..", "frontend", "src", "assets", "emi-logo.png"),
            TestContext.Current.CancellationToken);
        using (var photoForm = new MultipartFormDataContent())
        {
            photoForm.Add(new StringContent(enclosureItemId.ToString("D")), "templateItemId");
            photoForm.Add(new StringContent(firstReportVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
            photoForm.Add(new StringContent("최초 검사 외함"), "altText");
            var photoContent = new ByteArrayContent(png);
            photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            photoForm.Add(photoContent, "photo", "evidence.png");
            using var uploaded = await ReadJsonAsync(await qualityClient.PostAsync(
                $"/api/quality/iqc/reports/{firstReportId}/photos", photoForm, TestContext.Current.CancellationToken));
            firstReportVersion = uploaded.RootElement.GetProperty("reportVersion").GetInt32();
        }

        var failed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/reports/{firstReportId}/finalize",
            new { expectedReportVersion = firstReportVersion, expectedReceiptVersion = 2, result = "Failed", reason = "명판 규격 불일치로 부적합" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        using var queue = await ReadJsonAsync(await qualityClient.GetAsync("/api/quality/iqc?includeDecided=true", TestContext.Current.CancellationToken));
        var pendingId = queue.RootElement.GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("attemptId").GetGuid() == firstAttemptId)
            .GetProperty("pendingIssueId").GetGuid();
        using var assignees = await ReadJsonAsync(await coordinatorClient.GetAsync("/api/pending/assignees", TestContext.Current.CancellationToken));
        var assigneeId = assignees.RootElement[0].GetProperty("userId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/assign",
            new { assigneeUserId = assigneeId, expectedVersion = 1, reason = "명판 교체 담당 지정" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 2, reason = "명판 교체 시작" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "ReinspectionRequested", expectedVersion = 3, reason = "올바른 명판으로 교체 완료" },
            TestContext.Current.CancellationToken)).StatusCode);

        using var reinspection = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/reinspection",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken));
        var secondAttemptId = reinspection.RootElement.GetProperty("iqcAttemptId").GetGuid();
        using var second = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{secondAttemptId}/reports", null, TestContext.Current.CancellationToken));
        var source = second.RootElement.GetProperty("reinspectionSource");
        Assert.Equal("명판 규격 불일치로 부적합", source.GetProperty("failureReason").GetString());
        Assert.Equal("올바른 명판으로 교체 완료", source.GetProperty("actionReason").GetString());
        Assert.Equal("ITEM_SPEC", Assert.Single(source.GetProperty("failures").EnumerateArray()).GetProperty("itemCode").GetString());
        var onlyItem = Assert.Single(second.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(failedItemId, onlyItem.GetProperty("itemId").GetGuid());
        var secondReportId = second.RootElement.GetProperty("reportId").GetGuid();
        var secondReportVersion = second.RootElement.GetProperty("reportVersion").GetInt32();
        var secondReceiptVersion = second.RootElement.GetProperty("receiptVersion").GetInt32();

        var notApplicable = await qualityClient.PutAsJsonAsync(
            $"/api/quality/iqc/reports/{secondReportId}/responses",
            new { expectedReportVersion = secondReportVersion, responses = new[] { new { templateItemId = failedItemId, checkResult = "NotApplicable", textValue = (string?)null, note = "재검사 우회" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, notApplicable.StatusCode);

        using var savedSecond = await ReadJsonAsync(await qualityClient.PutAsJsonAsync(
            $"/api/quality/iqc/reports/{secondReportId}/responses",
            new { expectedReportVersion = secondReportVersion, responses = new[] { new { templateItemId = failedItemId, checkResult = "Pass", textValue = (string?)null, note = (string?)null } } },
            TestContext.Current.CancellationToken));
        secondReportVersion = savedSecond.RootElement.GetProperty("reportVersion").GetInt32();
        var contradictoryFailure = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/reports/{secondReportId}/finalize",
            new
            {
                expectedReportVersion = secondReportVersion,
                expectedReceiptVersion = secondReceiptVersion,
                result = "Failed",
                reason = "모든 재검사 항목이 적합인데도 부적합으로 확정하는 우회 요청을 차단합니다."
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, contradictoryFailure.StatusCode);
        var passed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/reports/{secondReportId}/finalize",
            new { expectedReportVersion = secondReportVersion, expectedReceiptVersion = secondReceiptVersion, result = "Passed", reason = "교체 명판 규격 적합 확인" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, passed.StatusCode);
    }

    [Fact]
    public async Task CategoryBasedIqc_RoutesOnlySnapshottedIqcItemsAndKeepsSignedScanImmutable()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "PROC-CATEGORY-IQC", "Category IQC Routing");

        var missingCategory = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { orderItem = "구분 누락 품목", orderQuantity = 1, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingCategory.StatusCode);

        var save = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new object[]
                {
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000001",
                        orderItem = "외함",
                        orderQuantity = 1,
                        orderUnit = "EA"
                    },
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000005",
                        orderItem = "차단기",
                        orderQuantity = 1,
                        orderUnit = "EA"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        using var procurement = await ReadProcurementAsync(procurementClient, projectId);
        Assert.Equal("CategoryBased", procurement.RootElement.GetProperty("iqcRoutingPolicy").GetString());
        var enclosure = procurement.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("orderItem").GetString() == "외함");
        var breaker = procurement.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("orderItem").GetString() == "차단기");
        Assert.True(enclosure.GetProperty("materialCategoryRequiresIqc").GetBoolean());
        Assert.False(breaker.GetProperty("materialCategoryRequiresIqc").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await qualityClient.GetAsync(
            "/api/form-templates/material-category-iqc",
            TestContext.Current.CancellationToken)).StatusCode);
        using (var createdCategory = await ReadJsonAsync(await qualityClient.PostAsJsonAsync(
                   "/api/form-templates/material-categories",
                   new { displayName = "신규 기본 비검사", displayOrder = 60, requiresIqc = true },
                   TestContext.Current.CancellationToken)))
        {
            var created = createdCategory.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("displayName").GetString() == "신규 기본 비검사");
            Assert.False(created.GetProperty("requiresIqc").GetBoolean());
            Assert.Equal("ScanBased", created.GetProperty("iqcDecisionMode").GetString());
        }

        using (var settings = await ReadJsonAsync(await adminClient.GetAsync(
                   "/api/form-templates/material-category-iqc",
                   TestContext.Current.CancellationToken)))
        {
            Assert.True(settings.RootElement.GetProperty("canManage").GetBoolean());
            var enclosureCategory = settings.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("materialCategoryCode").GetString() == "ENCLOSURE");
            var masterUpdate = await adminClient.PutAsJsonAsync(
                $"/api/form-templates/material-category-iqc/{enclosureCategory.GetProperty("materialCategoryId").GetGuid()}/setting",
                new
                {
                    expectedRowVersion = enclosureCategory.GetProperty("settingRowVersion").GetInt32(),
                    isEnabled = false,
                    decisionMode = "ScanBased"
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, masterUpdate.StatusCode);
        }

        var sameCategoryEdit = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "발주 수량과 단위 입력",
                items = new[]
                {
                    new
                    {
                        itemId = enclosure.GetProperty("itemId").GetGuid(),
                        expectedRowVersion = enclosure.GetProperty("rowVersion").GetInt32(),
                        materialCategoryId = enclosure.GetProperty("materialCategoryId").GetGuid(),
                        orderItem = "외함",
                        orderQuantity = 1,
                        orderUnit = "EA",
                        issueNote = "기존 IQC 스냅샷 보존 확인"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, sameCategoryEdit.StatusCode);

        using var unchangedSnapshot = await ReadProcurementAsync(procurementClient, projectId);
        Assert.True(unchangedSnapshot.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("orderItem").GetString() == "외함")
            .GetProperty("materialCategoryRequiresIqc").GetBoolean());

        using var breakerArrival = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{breaker.GetProperty("itemId").GetGuid()}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-07-30" },
            TestContext.Current.CancellationToken));
        Assert.Equal("InspectionNotRequired", breakerArrival.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, breakerArrival.RootElement.GetProperty("iqcAttemptId").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{breakerArrival.RootElement.GetProperty("receiptId").GetGuid()}/confirm",
            new { expectedVersion = 2 },
            TestContext.Current.CancellationToken)).StatusCode);

        using var enclosureArrival = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{enclosure.GetProperty("itemId").GetGuid()}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-07-30" },
            TestContext.Current.CancellationToken));
        Assert.Equal("IqcRequested", enclosureArrival.RootElement.GetProperty("status").GetString());
        var attemptId = enclosureArrival.RootElement.GetProperty("iqcAttemptId").GetGuid();
        using var initialized = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{attemptId}/reports",
            null,
            TestContext.Current.CancellationToken));
        Assert.Equal("ScanBased", initialized.RootElement.GetProperty("decisionMode").GetString());
        Assert.Empty(initialized.RootElement.GetProperty("items").EnumerateArray());
        var scanReportId = initialized.RootElement.GetProperty("reportId").GetGuid();
        var scanVersion = initialized.RootElement.GetProperty("reportVersion").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/scan-reports/{scanReportId}/finalize",
            new
            {
                expectedReportVersion = scanVersion,
                expectedReceiptVersion = 2,
                result = "Passed",
                reason = "서명 검사서 확인 완료"
            },
            TestContext.Current.CancellationToken)).StatusCode);

        var png = await File.ReadAllBytesAsync(
            Path.Combine(context.RepositoryRoot, "..", "frontend", "src", "assets", "emi-logo.png"),
            TestContext.Current.CancellationToken);
        Guid attachmentId;
        using (var scanForm = new MultipartFormDataContent())
        {
            scanForm.Add(new StringContent(scanVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
            var scanContent = new ByteArrayContent(png);
            scanContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            scanForm.Add(scanContent, "file", "signed-enclosure-iqc.png");
            using var uploaded = await ReadJsonAsync(await qualityClient.PostAsync(
                $"/api/quality/iqc/scan-reports/{scanReportId}/attachments",
                scanForm,
                TestContext.Current.CancellationToken));
            scanVersion = uploaded.RootElement.GetProperty("reportVersion").GetInt32();
            attachmentId = Assert.Single(uploaded.RootElement.GetProperty("scanAttachments").EnumerateArray())
                .GetProperty("attachmentId").GetGuid();
        }
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.GetAsync(
            $"/api/quality/iqc/scan-reports/{scanReportId}/attachments/{attachmentId}/content",
            TestContext.Current.CancellationToken)).StatusCode);

        using var finalized = await ReadJsonAsync(await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/scan-reports/{scanReportId}/finalize",
            new
            {
                expectedReportVersion = scanVersion,
                expectedReceiptVersion = 2,
                result = "Passed",
                reason = "서명 검사서와 외함 상태 적합 확인"
            },
            TestContext.Current.CancellationToken));
        Assert.Equal("Finalized", finalized.RootElement.GetProperty("reportStatus").GetString());
        Assert.False(finalized.RootElement.GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await qualityClient.DeleteAsync(
            $"/api/quality/iqc/scan-reports/{scanReportId}/attachments/{attachmentId}?expectedReportVersion={finalized.RootElement.GetProperty("reportVersion").GetInt32()}",
            TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task CategoryBasedIqc_ExcelEditKeepsTheStoredCategorySnapshot()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "PROC-CATEGORY-EXCEL", "Category Excel Snapshot");

        var initialFile = CreateProcurementExcel(
            "Category Excel Snapshot",
            "PROC-CATEGORY-EXCEL",
            ["Category Excel Snapshot", "PROC-CATEGORY-EXCEL", "4W", "외함", "외함", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "최초 저장", ""]);
        using var initialPreview = await PreviewExcelAsync(procurementClient, initialFile, "category-initial.xlsx");
        Assert.Equal(1, initialPreview.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await ApplyExcelAsync(
            procurementClient,
            initialFile,
            "category-initial.xlsx",
            initialPreview,
            reason: null)).StatusCode);

        using var beforeSettingChange = await ReadProcurementAsync(procurementClient, projectId);
        var storedItem = Assert.Single(beforeSettingChange.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(storedItem.GetProperty("materialCategoryRequiresIqc").GetBoolean());
        Assert.Equal("ScanBased", storedItem.GetProperty("materialCategoryIqcDecisionMode").GetString());

        using (var settings = await ReadJsonAsync(await adminClient.GetAsync(
                   "/api/form-templates/material-category-iqc",
                   TestContext.Current.CancellationToken)))
        {
            var enclosureCategory = settings.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("materialCategoryCode").GetString() == "ENCLOSURE");
            Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
                $"/api/form-templates/material-category-iqc/{enclosureCategory.GetProperty("materialCategoryId").GetGuid()}/setting",
                new
                {
                    expectedRowVersion = enclosureCategory.GetProperty("settingRowVersion").GetInt32(),
                    isEnabled = false,
                    decisionMode = "ScanBased"
                },
                TestContext.Current.CancellationToken)).StatusCode);
        }

        using (var categories = await ReadJsonAsync(await adminClient.GetAsync(
                   "/api/form-templates/material-categories?includeInactive=true",
                   TestContext.Current.CancellationToken)))
        {
            var enclosureCategory = categories.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("code").GetString() == "ENCLOSURE");
            Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync(
                $"/api/form-templates/material-categories/{enclosureCategory.GetProperty("categoryId").GetGuid()}",
                new
                {
                    expectedRowVersion = enclosureCategory.GetProperty("rowVersion").GetInt32(),
                    displayName = "외함 변경",
                    isActive = true,
                    displayOrder = enclosureCategory.GetProperty("displayOrder").GetInt32()
                },
                TestContext.Current.CancellationToken)).StatusCode);
        }

        var changedFile = CreateProcurementExcel(
            "Category Excel Snapshot",
            "PROC-CATEGORY-EXCEL",
            ["Category Excel Snapshot", "PROC-CATEGORY-EXCEL", "4W", "외함", "외함 변경", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "일반 정보만 변경", ""]);
        using var changedPreview = await PreviewExcelAsync(procurementClient, changedFile, "category-changed.xlsx");
        Assert.Equal(1, changedPreview.RootElement.GetProperty("changedCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await ApplyExcelAsync(
            procurementClient,
            changedFile,
            "category-changed.xlsx",
            changedPreview,
            "일반 구매 정보 변경")).StatusCode);

        using var afterExcelEdit = await ReadProcurementAsync(procurementClient, projectId);
        var unchangedSnapshot = Assert.Single(afterExcelEdit.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(unchangedSnapshot.GetProperty("materialCategoryRequiresIqc").GetBoolean());
        Assert.Equal("ScanBased", unchangedSnapshot.GetProperty("materialCategoryIqcDecisionMode").GetString());
        Assert.Equal("외함", unchangedSnapshot.GetProperty("materialCategoryName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "발주 수량과 단위 입력",
                items = new[]
                {
                    new
                    {
                        itemId = unchangedSnapshot.GetProperty("itemId").GetGuid(),
                        expectedRowVersion = unchangedSnapshot.GetProperty("rowVersion").GetInt32(),
                        materialCategoryId = unchangedSnapshot.GetProperty("materialCategoryId").GetGuid(),
                        orderItem = "외함",
                        supplierName = "Vendor X",
                        technicalOwner = "Owner A",
                        orderDate = "2026-07-01",
                        expectedReceiptDate = "2026-07-10",
                        issueNote = "일반 정보만 변경",
                        supplyType = "Purchased",
                        orderQuantity = 1,
                        orderUnit = "EA"
                    }
                }
            },
            TestContext.Current.CancellationToken)).StatusCode);
        using var readyForArrival = await ReadProcurementAsync(procurementClient, projectId);
        var arrivalItem = Assert.Single(readyForArrival.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(arrivalItem.GetProperty("materialCategoryRequiresIqc").GetBoolean());

        var arrivalResponse = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{arrivalItem.GetProperty("itemId").GetGuid()}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-07-30" },
            TestContext.Current.CancellationToken);
        var arrivalBody = await arrivalResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(arrivalResponse.StatusCode == HttpStatusCode.OK, arrivalBody);
        using var arrival = JsonDocument.Parse(arrivalBody);
        Assert.Equal("IqcRequested", arrival.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CategoryBasedDetailedIqc_UsesTheCategoryTemplateCurrentAtFirstReportOpen()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var adminClient = context.CreateClient("dev-admin");

        using var managers = await ReadJsonAsync(await adminClient.GetAsync(
            "/api/form-templates/managers",
            TestContext.Current.CancellationToken));
        var qualityManager = managers.RootElement.GetProperty("candidates").EnumerateArray()
            .Single(candidate => candidate.GetProperty("departmentCode").GetString() == "quality");
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsJsonAsync(
            "/api/form-templates/managers",
            new { userId = qualityManager.GetProperty("userId").GetGuid(), domain = "Quality" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.GetAsync(
            "/api/form-templates/material-category-iqc",
            TestContext.Current.CancellationToken)).StatusCode);

        using var initialCatalog = await ReadJsonAsync(await adminClient.GetAsync(
            "/api/form-templates/material-category-iqc",
            TestContext.Current.CancellationToken));
        var other = initialCatalog.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("materialCategoryCode").GetString() == "OTHER");
        var categoryId = other.GetProperty("materialCategoryId").GetGuid();

        var emptyDetailed = await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/setting",
            new
            {
                expectedRowVersion = other.GetProperty("settingRowVersion").GetInt32(),
                isEnabled = true,
                decisionMode = "Detailed"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, emptyDetailed.StatusCode);

        using var firstTemplateCatalog = await ReadJsonAsync(await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/current",
            new
            {
                expectedTemplateRowVersion = other.GetProperty("templateRowVersion").GetInt32(),
                items = new[]
                {
                    new
                    {
                        itemCode = "DETAIL_CHECK",
                        displayOrder = 1,
                        label = "저장 전 상세검사",
                        guidance = "구매품 상태를 확인해 주세요.",
                        responseType = "Check",
                        isRequired = true,
                        requiresPhoto = false,
                        maxTextLength = (int?)null
                    }
                }
            },
            TestContext.Current.CancellationToken));
        var firstTemplate = firstTemplateCatalog.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("materialCategoryId").GetGuid() == categoryId);
        var staleTemplateSave = await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/current",
            new
            {
                expectedTemplateRowVersion = other.GetProperty("templateRowVersion").GetInt32(),
                items = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleTemplateSave.StatusCode);
        using var enabledCatalog = await ReadJsonAsync(await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/setting",
            new
            {
                expectedRowVersion = firstTemplate.GetProperty("settingRowVersion").GetInt32(),
                isEnabled = true,
                decisionMode = "Detailed"
            },
            TestContext.Current.CancellationToken));
        var enabled = enabledCatalog.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("materialCategoryId").GetGuid() == categoryId);

        var projectId = await CreateProjectAsync(salesClient, "PROC-CATEGORY-DETAILED", "Category Detailed IQC");
        var saved = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        materialCategoryId = categoryId,
                        orderItem = "상세 검사 구매품",
                        orderQuantity = 1,
                        orderUnit = "EA"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var savedJson = await ReadJsonAsync(saved);
        var savedItem = savedJson.RootElement.GetProperty("items")[0];
        Assert.True(savedItem.GetProperty("materialCategoryRequiresIqc").GetBoolean());
        Assert.Equal("Detailed", savedItem.GetProperty("materialCategoryIqcDecisionMode").GetString());

        var preservedDefinitionKey = enabled.GetProperty("items")[0].GetProperty("definitionKey").GetGuid();
        using var arrivalTemplateCatalog = await ReadJsonAsync(await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/current",
            new
            {
                expectedTemplateRowVersion = enabled.GetProperty("templateRowVersion").GetInt32(),
                items = new[]
                {
                    new
                    {
                        itemCode = "DETAIL_CHECK",
                        displayOrder = 1,
                        label = "성적서 최초 열기 시점 양식",
                        guidance = "최초 성적서 생성 시점에 고정됩니다.",
                        responseType = "Check",
                        isRequired = true,
                        requiresPhoto = false,
                        maxTextLength = (int?)null,
                        definitionKey = preservedDefinitionKey
                    }
                }
            },
            TestContext.Current.CancellationToken));
        var arrivalTemplate = arrivalTemplateCatalog.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("materialCategoryId").GetGuid() == categoryId);

        using var arrival = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{savedItem.GetProperty("itemId").GetGuid()}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-08-05" },
            TestContext.Current.CancellationToken));
        Assert.Equal("IqcRequested", arrival.RootElement.GetProperty("status").GetString());
        var attemptId = arrival.RootElement.GetProperty("iqcAttemptId").GetGuid();
        using var initialized = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{attemptId}/reports",
            null,
            TestContext.Current.CancellationToken));
        Assert.Equal("Detailed", initialized.RootElement.GetProperty("decisionMode").GetString());
        Assert.Equal("성적서 최초 열기 시점 양식", initialized.RootElement.GetProperty("items")[0].GetProperty("label").GetString());

        using var latestCatalog = await ReadJsonAsync(await adminClient.PutAsJsonAsync(
            $"/api/form-templates/material-category-iqc/{categoryId}/current",
            new
            {
                expectedTemplateRowVersion = arrivalTemplate.GetProperty("templateRowVersion").GetInt32(),
                items = new[]
                {
                    new
                    {
                        itemCode = "DETAIL_CHECK",
                        displayOrder = 1,
                        label = "성적서 생성 후 새 양식",
                        guidance = "이미 생성된 성적서에는 소급되지 않습니다.",
                        responseType = "Check",
                        isRequired = true,
                        requiresPhoto = false,
                        maxTextLength = (int?)null,
                        definitionKey = preservedDefinitionKey
                    }
                }
            },
            TestContext.Current.CancellationToken));
        Assert.Equal("성적서 생성 후 새 양식", latestCatalog.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("materialCategoryId").GetGuid() == categoryId)
            .GetProperty("items")[0].GetProperty("label").GetString());

        using var preservedReport = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/quality/iqc/{attemptId}/report",
            TestContext.Current.CancellationToken));
        Assert.Equal("성적서 최초 열기 시점 양식", preservedReport.RootElement.GetProperty("items")[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task CategoryBasedScanIqc_FailureCreatesPendingAndReinspectionPreservesEverySignedScan()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var coordinatorClient = context.CreateClient("dev-production");
        var projectId = await CreateProjectAsync(salesClient, "PROC-SCAN-REINSPECTION", "Scan IQC Reinspection");

        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000001",
                        orderItem = "외함 재검사품",
                        orderQuantity = 1,
                        orderUnit = "EA"
                    }
                }
            },
            TestContext.Current.CancellationToken)).StatusCode);
        using var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var itemId = procurement.RootElement.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        using var arrival = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = "2026-07-30" },
            TestContext.Current.CancellationToken));
        var receiptId = arrival.RootElement.GetProperty("receiptId").GetGuid();
        var firstAttemptId = arrival.RootElement.GetProperty("iqcAttemptId").GetGuid();
        using var firstReport = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{firstAttemptId}/reports",
            null,
            TestContext.Current.CancellationToken));
        var firstReportId = firstReport.RootElement.GetProperty("reportId").GetGuid();
        var firstReportVersion = firstReport.RootElement.GetProperty("reportVersion").GetInt32();
        var png = await File.ReadAllBytesAsync(
            Path.Combine(context.RepositoryRoot, "..", "frontend", "src", "assets", "emi-logo.png"),
            TestContext.Current.CancellationToken);

        using (var firstScanForm = new MultipartFormDataContent())
        {
            firstScanForm.Add(new StringContent(firstReportVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
            var firstScanContent = new ByteArrayContent(png);
            firstScanContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            firstScanForm.Add(firstScanContent, "file", "first-signed-iqc.png");
            using var uploaded = await ReadJsonAsync(await qualityClient.PostAsync(
                $"/api/quality/iqc/scan-reports/{firstReportId}/attachments",
                firstScanForm,
                TestContext.Current.CancellationToken));
            firstReportVersion = uploaded.RootElement.GetProperty("reportVersion").GetInt32();
        }

        Assert.Equal(HttpStatusCode.OK, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/scan-reports/{firstReportId}/finalize",
            new
            {
                expectedReportVersion = firstReportVersion,
                expectedReceiptVersion = 2,
                result = "Failed",
                reason = "외함 도어 변형으로 재조치가 필요합니다."
            },
            TestContext.Current.CancellationToken)).StatusCode);
        var pendingId = Guid.Parse(await context.ReadTextAsync(
            "select id::text from pending_issues where project_id=@project_id;",
            projectId));

        using var assignees = await ReadJsonAsync(await coordinatorClient.GetAsync(
            "/api/pending/assignees",
            TestContext.Current.CancellationToken));
        var assigneeId = assignees.RootElement[0].GetProperty("userId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/assign",
            new { assigneeUserId = assigneeId, expectedVersion = 1, reason = "외함 변형 조치 담당 지정" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 2, reason = "외함 변형 조치 시작" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "ReinspectionRequested", expectedVersion = 3, reason = "외함 도어 교정 완료" },
            TestContext.Current.CancellationToken)).StatusCode);

        using var reinspection = await ReadJsonAsync(await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/reinspection",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken));
        var secondAttemptId = reinspection.RootElement.GetProperty("iqcAttemptId").GetGuid();
        using var secondReport = await ReadJsonAsync(await qualityClient.PostAsync(
            $"/api/quality/iqc/{secondAttemptId}/reports",
            null,
            TestContext.Current.CancellationToken));
        var secondReportId = secondReport.RootElement.GetProperty("reportId").GetGuid();
        var secondReportVersion = secondReport.RootElement.GetProperty("reportVersion").GetInt32();
        var receiptVersion = secondReport.RootElement.GetProperty("receiptVersion").GetInt32();
        var history = Assert.Single(secondReport.RootElement.GetProperty("scanHistory").EnumerateArray());
        Assert.Equal(firstReportId, history.GetProperty("reportId").GetGuid());
        Assert.Equal("Failed", history.GetProperty("result").GetString());
        Assert.Equal("외함 도어 교정 완료", history.GetProperty("actionReason").GetString());
        var historicAttachment = Assert.Single(history.GetProperty("attachments").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.GetAsync(
            $"/api/quality/iqc/scan-reports/{firstReportId}/attachments/{historicAttachment.GetProperty("attachmentId").GetGuid()}/content",
            TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/scan-reports/{secondReportId}/finalize",
            new
            {
                expectedReportVersion = secondReportVersion,
                expectedReceiptVersion = receiptVersion,
                result = "Passed",
                reason = "재검사 적합"
            },
            TestContext.Current.CancellationToken)).StatusCode);
        using (var secondScanForm = new MultipartFormDataContent())
        {
            secondScanForm.Add(new StringContent(secondReportVersion.ToString(CultureInfo.InvariantCulture)), "expectedReportVersion");
            var secondScanContent = new ByteArrayContent(png);
            secondScanContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            secondScanForm.Add(secondScanContent, "file", "second-signed-iqc.png");
            using var uploaded = await ReadJsonAsync(await qualityClient.PostAsync(
                $"/api/quality/iqc/scan-reports/{secondReportId}/attachments",
                secondScanForm,
                TestContext.Current.CancellationToken));
            secondReportVersion = uploaded.RootElement.GetProperty("reportVersion").GetInt32();
        }
        using var passed = await ReadJsonAsync(await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/scan-reports/{secondReportId}/finalize",
            new
            {
                expectedReportVersion = secondReportVersion,
                expectedReceiptVersion = receiptVersion,
                result = "Passed",
                reason = "외함 도어 교정과 재검사 결과 적합합니다."
            },
            TestContext.Current.CancellationToken));
        Assert.Equal("Passed", passed.RootElement.GetProperty("result").GetString());
        Assert.Single(passed.RootElement.GetProperty("scanHistory").EnumerateArray());
        using var closedPending = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/pending/{pendingId}",
            TestContext.Current.CancellationToken));
        Assert.Equal("Closed", closedPending.RootElement.GetProperty("issue").GetProperty("status").GetString());
    }

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
                items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "MCCB" } }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, procurementUpdate.StatusCode);
        var created = await ReadProcurementAsync(procurementClient, projectId);
        var item = created.RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();
        var rowVersion = item.GetProperty("rowVersion").GetInt32();

        foreach (var userKey in new[]
        {
            "dev-sales", "dev-design", "dev-procurement", "dev-materials", "dev-production",
            "dev-manufacturing", "dev-quality", "dev-logistics", "dev-viewer", "dev-admin"
        })
        {
            using var client = context.CreateClient(userKey);
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/api/materials/receipts", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/api/quality/iqc", TestContext.Current.CancellationToken)).StatusCode);
        }

        foreach (var userKey in new[]
        {
            "dev-sales", "dev-design", "dev-production", "dev-manufacturing",
            "dev-quality", "dev-logistics", "dev-viewer", "dev-admin"
        })
        {
            using var client = context.CreateClient(userKey);
            var denied = await client.PostAsJsonAsync(
                $"/api/materials/items/{itemId}/receipts",
                new { quantity = 1, unit = "EA", orderQuantity = 1, orderUnit = "EA", arrivalDate = "2026-07-19" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        foreach (var userKey in new[]
        {
            "dev-sales", "dev-design", "dev-materials", "dev-production",
            "dev-manufacturing", "dev-quality", "dev-logistics", "dev-viewer", "dev-admin"
        })
        {
            using var client = context.CreateClient(userKey);
            var denied = await client.PatchAsJsonAsync(
                $"/api/projects/{projectId}/procurement",
                new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "SHOULD-NOT-SAVE" } } },
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
            Assert.Equal(HttpStatusCode.BadRequest, receipt.StatusCode);
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
        var projectId = await context.CreateLegacyProjectAsync("PROC-DIRECT", "Proc Direct");

        var response = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "single field rows",
                items = new object[]
                {
                    new { standardLeadTime = "4W" },
                    new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Cable" },
                    new { supplierName = "Vendor A" },
                    new { technicalOwner = "Engineer A" },
                    new { orderDate = "2026-07-01" },
                    new { expectedReceiptDate = "2026-07-05" },
                    new { issueNote = "확인 필요" },
                    new { }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var items = procurement.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(7, items.Count);
        Assert.Equal("2026-10-10", procurement.RootElement.GetProperty("projectDeliveryDate").GetString());
        Assert.Equal("4W", items[0].GetProperty("standardLeadTime").GetString());
        Assert.Equal("Cable", items[1].GetProperty("orderItem").GetString());
        Assert.Equal("Vendor A", items[2].GetProperty("supplierName").GetString());
        Assert.Equal("Engineer A", items[3].GetProperty("technicalOwner").GetString());
        Assert.Equal("2026-07-01", items[4].GetProperty("orderDate").GetString());
        Assert.Equal("2026-07-05", items[5].GetProperty("expectedReceiptDate").GetString());
        Assert.Equal("2026-10-10", items[5].GetProperty("shipmentDisplayDate").GetString());
        Assert.Equal("확인 필요", items[6].GetProperty("issueNote").GetString());

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
    public async Task PurchasedQuantity_LinksProcurementToEveryArrivalIqcWorkAndRecipients()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        var projectId = await context.CreateLegacyProjectAsync("PROC-PURCHASED-TRACE", "Purchased Material Trace");
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
            values
              ('{projectId}', 'MaterialsPrimary', '50000000-0000-0000-0000-000000000012', '50000000-0000-0000-0000-000000000002', now()),
              ('{projectId}', 'MaterialsSecondary', '50000000-0000-0000-0000-000000000011', '50000000-0000-0000-0000-000000000002', now()),
              ('{projectId}', 'QualityIQC', '50000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000002', now()),
              ('{projectId}', 'QualityIQCSecondary', '50000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', now());
            """);

        var missingUnit = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Incomplete Purchased Item", supplyType = "Purchased", orderQuantity = 10m } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingUnit.StatusCode);

        var create = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Purchased Busbar",
                        supplierName = "Vendor P",
                        supplyType = "Purchased",
                        orderQuantity = 10m,
                        orderUnit = "EA",
                        expectedReceiptDate = "2026-07-20"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var created = await ReadJsonAsync(create);
        var item = created.RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();
        Assert.Equal("Purchased", item.GetProperty("supplyType").GetString());
        Assert.Equal(10m, item.GetProperty("orderQuantity").GetDecimal());
        Assert.Equal("EA", item.GetProperty("orderUnit").GetString());
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id=recipient.notification_id
            where notification.project_id=@project_id
              and notification.idempotency_key like 'procurement:materials:%:notification';
            """,
            projectId));
        var initialHandoff = await context.ReadTextAsync(
            "select message from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification' order by created_at_utc desc limit 1;",
            projectId);
        Assert.Contains("발주품목 Purchased Busbar", initialHandoff, StringComparison.Ordinal);
        Assert.Contains("입고예정일 7/20", initialHandoff, StringComparison.Ordinal);
        Assert.Contains("발주·제공 예정 수량 10", initialHandoff, StringComparison.Ordinal);

        var changed = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "납기 정보 보정",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                        standardLeadTime = "3W",
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Purchased Busbar",
                        supplierName = "Vendor P",
                        supplyType = "Purchased",
                        orderQuantity = 10m,
                        orderUnit = "EA",
                        expectedReceiptDate = "2026-07-23",
                        issueNote = "하루 늦게 들어오기로 함"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(4L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification';",
            projectId));
        var changedHandoff = await context.ReadTextAsync(
            "select message from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification' order by created_at_utc desc limit 1;",
            projectId);
        Assert.StartsWith("PROC-PURCHASED-TRACE 구매품 변경 내용을 확인하고 입고 계획에 반영해 주세요.", changedHandoff, StringComparison.Ordinal);
        Assert.Contains("\n\n상세 내용\n", changedHandoff, StringComparison.Ordinal);
        Assert.Contains("입고예정일 변경 7/20 → 7/23", changedHandoff, StringComparison.Ordinal);
        Assert.Contains("이슈사항 변경 - → 하루 늦게 들어오기로 함", changedHandoff, StringComparison.Ordinal);
        var changedWork = await context.ReadTextAsync(
            "select description from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%' order by created_at_utc desc limit 1;",
            projectId);
        Assert.Contains("\n\n상세 내용\n", changedWork, StringComparison.Ordinal);
        Assert.Contains("입고예정일 변경 7/20 → 7/23", changedWork, StringComparison.Ordinal);

        var arrivals = new List<(Guid ReceiptId, Guid AttemptId)>();
        foreach (var input in new[] { (Quantity: 4m, Date: "2026-07-15"), (Quantity: 6m, Date: "2026-07-18") })
        {
            var response = await materialsClient.PostAsJsonAsync(
                $"/api/materials/items/{itemId}/receipts",
                new { quantity = input.Quantity, unit = "EA", arrivalDate = input.Date, note = $"{input.Date} 도착분" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            arrivals.Add((json.RootElement.GetProperty("receiptId").GetGuid(), json.RootElement.GetProperty("iqcAttemptId").GetGuid()));
        }

        Assert.Equal(2, arrivals.Select(entry => entry.ReceiptId).Distinct().Count());
        Assert.Equal(2, arrivals.Select(entry => entry.AttemptId).Distinct().Count());
        var materialItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Equal(10m, materialItem.GetProperty("orderQuantity").GetDecimal());
        Assert.Equal(10m, materialItem.GetProperty("arrivedQuantity").GetDecimal());
        Assert.Equal(0m, materialItem.GetProperty("confirmedQuantity").GetDecimal());
        Assert.Equal(10m, materialItem.GetProperty("remainingQuantity").GetDecimal());
        var receipts = materialItem.GetProperty("receipts").EnumerateArray().ToList();
        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, receipt => Assert.Single(receipt.GetProperty("iqcAttempts").EnumerateArray()));
        Assert.Equal(4L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'IQC' and idempotency_key like 'materials:iqc:%';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id = @project_id and idempotency_key like 'materials:iqc:%:notification';",
            projectId));
        Assert.Equal(4L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id = recipient.notification_id
            where notification.project_id = @project_id
              and notification.idempotency_key like 'materials:iqc:%:notification';
            """,
            projectId));

        var afterArrivals = await ReadProcurementAsync(procurementClient, projectId);
        var current = afterArrivals.RootElement.GetProperty("items")[0];
        var unsafeReduction = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "도착 후 수량 축소 검증",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = current.GetProperty("rowVersion").GetInt32(),
                        orderQuantity = 9m
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeReduction.StatusCode);

        await context.MarkAttemptLegacyAsync(arrivals[0].AttemptId);
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{arrivals[0].AttemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "첫 도착분 IQC 합격" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'materials:receipt:%:confirm:%';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id=recipient.notification_id
            where notification.project_id=@project_id
              and notification.idempotency_key like 'materials:receipt:%:confirm:notification';
            """,
            projectId));
        var confirmationWork = await context.ReadTextAsync(
            "select description from work_items where project_id=@project_id and idempotency_key like 'materials:receipt:%:confirm:%' order by created_at_utc desc limit 1;",
            projectId);
        Assert.Equal("IQC 합격 도착분의 입고 확정을 진행해 주세요. (Purchased Busbar 4 EA)", confirmationWork);
        Assert.DoesNotContain("\n", confirmationWork, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{arrivals[0].ReceiptId}/confirm",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal("부분 입고 4/10 EA", await context.ReadTextAsync(
            "select receipt_completion_note from project_procurement_items where id=@project_id;",
            itemId));
        await context.ExecuteSqlAsync($"""
            select set_config('emi_qms.material_receipt_write', 'allowed', false);
            update project_procurement_items set receipt_completion_note=null where id='{itemId}';
            """);
        using (var partial = await ReadProcurementAsync(procurementClient, projectId))
        {
            var partialItem = partial.RootElement.GetProperty("items")[0];
            Assert.False(partialItem.GetProperty("receiptCompleted").GetBoolean());
            Assert.Equal("부분 입고 4/10 EA", partialItem.GetProperty("receiptCompletionNote").GetString());
        }
        var partiallyConfirmedMaterial = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Equal(4m, partiallyConfirmedMaterial.GetProperty("confirmedQuantity").GetDecimal());
        Assert.Equal(6m, partiallyConfirmedMaterial.GetProperty("remainingQuantity").GetDecimal());

        await context.MarkAttemptLegacyAsync(arrivals[1].AttemptId);
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{arrivals[1].AttemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "두 번째 도착분 IQC 합격" },
            TestContext.Current.CancellationToken)).StatusCode);
        var finalConfirm = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{arrivals[1].ReceiptId}/confirm",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken);
        Assert.True(finalConfirm.StatusCode == HttpStatusCode.OK, await finalConfirm.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var completedMaterial = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.True(completedMaterial.GetProperty("arrivalsClosed").GetBoolean());
        Assert.True(completedMaterial.GetProperty("receiptCompleted").GetBoolean());
    }

    [Fact]
    public async Task PurchaseOwnerSetsQuantityAndFallbackHandoffBeforeAutomaticIqc()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        var projectId = await context.CreateLegacyProjectAsync("PROC-OWNER-QTY", "Purchase Owner Quantity");

        var draft = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Purchased Draft" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        using var draftJson = await ReadJsonAsync(draft);
        var draftItem = draftJson.RootElement.GetProperty("items")[0];
        var itemId = draftItem.GetProperty("itemId").GetGuid();
        Assert.Equal(JsonValueKind.Null, draftItem.GetProperty("orderQuantity").ValueKind);
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and assigned_user_id='50000000-0000-0000-0000-000000000012' and idempotency_key like 'procurement:materials:%';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from notification_recipients recipient join notifications notification on notification.id=recipient.notification_id where notification.project_id=@project_id and recipient.user_id='50000000-0000-0000-0000-000000000012' and notification.idempotency_key like 'procurement:materials:%:notification';",
            projectId));

        var rejectedArrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 2, unit = "EA", orderQuantity = 2, orderUnit = "EA", arrivalDate = "2026-07-21" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, rejectedArrival.StatusCode);
        Assert.Contains("구매팀이 구매 탭에서 먼저 입력", await rejectedArrival.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from material_receipts receipt join project_procurement_items item on item.id=receipt.procurement_item_id where item.project_id=@project_id;",
            projectId));

        var completedPurchase = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "구매팀 발주 수량 확정",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = draftItem.GetProperty("rowVersion").GetInt32(),
                        orderQuantity = 2,
                        orderUnit = "EA"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completedPurchase.StatusCode);

        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 2, unit = "EA", arrivalDate = "2026-07-21" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using var arrivalJson = await ReadJsonAsync(arrival);
        Assert.Equal("IqcRequested", arrivalJson.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, arrivalJson.RootElement.GetProperty("iqcAttemptId").GetGuid());
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and assigned_user_id='50000000-0000-0000-0000-000000000005' and workflow_stage_code='IQC';",
            projectId));
    }

    [Fact]
    public async Task IqcReconciliation_RecoversOrphanArrivalAndBothQualityAssignmentsWithoutDuplicates()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var qualityClient = context.CreateClient("dev-quality");
        var projectId = await CreateProjectAsync(salesClient, "PROC-IQC-RECOVER", "Recover Missing IQC");
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
            values
              ('{projectId}', 'QualityIQC', '50000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000002', now()),
              ('{projectId}', 'QualityIQCSecondary', '50000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', now());
            """);
        var procurementSave = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Legacy Orphan", supplyType = "Purchased", orderQuantity = 2m, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, procurementSave.StatusCode);
        using var procurement = await ReadJsonAsync(procurementSave);
        var itemId = procurement.RootElement.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        var receiptId = Guid.NewGuid();
        await context.ExecuteSqlAsync($"""
            insert into material_receipts (
                id, procurement_item_id, quantity, unit, arrival_date, note, status,
                created_by_user_id, updated_by_user_id)
            values (
                '{receiptId}', '{itemId}', 2, 'EA', '2026-07-20', '이전 runtime에서 누락된 도착분', 'Arrived',
                '50000000-0000-0000-0000-000000000012', '50000000-0000-0000-0000-000000000012');
            """);

        var firstResponse = await qualityClient.PostAsync(
            "/api/quality/iqc/reconcile",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        using var first = await ReadJsonAsync(firstResponse);
        Assert.Equal(1, first.RootElement.GetProperty("recoveredReceiptCount").GetInt32());

        var queueResponse = await qualityClient.GetAsync("/api/quality/iqc", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        using var queue = await ReadJsonAsync(queueResponse);
        var recoveredAttempt = Assert.Single(
            queue.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("receiptId").GetGuid() == receiptId);
        Assert.Equal("Requested", recoveredAttempt.GetProperty("status").GetString());
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'materials:iqc:%';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id=@project_id and idempotency_key like 'materials:iqc:%:notification';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id=recipient.notification_id
            where notification.project_id=@project_id
              and notification.idempotency_key like 'materials:iqc:%:notification';
            """,
            projectId));

        var secondResponse = await qualityClient.PostAsync(
            "/api/quality/iqc/reconcile",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var second = await ReadJsonAsync(secondResponse);
        Assert.Equal(0, second.RootElement.GetProperty("recoveredReceiptCount").GetInt32());
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'materials:iqc:%';",
            projectId));
    }

    [Fact]
    public async Task MaterialReceipt_FollowsArrivalIqcConfirmationAndDerivedCompletion()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        var projectId = await context.CreateLegacyProjectAsync("PROC-RECEIPT", "Proc Receipt");
        var procurementSave = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Terminal Block", orderQuantity = 10.5m, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, procurementSave.StatusCode);
        var item = (await ReadProcurementAsync(procurementClient, projectId)).RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();

        var legacyWrite = await materialsClient.PatchAsJsonAsync(
            "/api/materials/receipts",
            new { items = new[] { new { itemId, receiptCompleted = true } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, legacyWrite.StatusCode);

        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new
            {
                quantity = 10.5m,
                unit = "EA",
                arrivalDate = "2026-07-15",
                note = "dock A"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using var arrivalJson = await ReadJsonAsync(arrival);
        var receiptId = arrivalJson.RootElement.GetProperty("receiptId").GetGuid();
        var autoAttemptId = arrivalJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal("IqcRequested", arrivalJson.RootElement.GetProperty("status").GetString());

        using (var afterMeasurement = await ReadProcurementAsync(procurementClient, projectId))
        {
            var measuredItem = afterMeasurement.RootElement.GetProperty("items")[0];
            var ordinaryEdit = await procurementClient.PatchAsJsonAsync(
                $"/api/projects/{projectId}/procurement",
                new
                {
                    items = new[]
                    {
                        new
                        {
                            itemId,
                            expectedRowVersion = measuredItem.GetProperty("rowVersion").GetInt32(),
                            standardLeadTime = "5W"
                        }
                    }
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ordinaryEdit.StatusCode);
            using var ordinaryEditJson = await ReadJsonAsync(ordinaryEdit);
            var preservedItem = ordinaryEditJson.RootElement.GetProperty("items")[0];
            Assert.Equal("Purchased", preservedItem.GetProperty("supplyType").GetString());
            Assert.Equal(10.5m, preservedItem.GetProperty("orderQuantity").GetDecimal());
            Assert.Equal("EA", preservedItem.GetProperty("orderUnit").GetString());
        }

        var overReceipt = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 0.1m, unit = "EA", arrivalDate = "2026-07-15" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, overReceipt.StatusCode);
        using var overReceiptJson = await ReadJsonAsync(overReceipt);
        Assert.True(overReceiptJson.RootElement.GetProperty("errors").TryGetProperty("Quantity", out _));

        var iqcRequest = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/iqc-requests",
            new { expectedVersion = 1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, iqcRequest.StatusCode);
        using var iqcRequestJson = await ReadJsonAsync(iqcRequest);
        var attemptId = iqcRequestJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal(autoAttemptId, attemptId);
        await context.MarkAttemptLegacyAsync(attemptId);

        var iqcPass = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{attemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "외관과 수량 확인 완료" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, iqcPass.StatusCode);

        var confirm = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/confirm",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        using var afterConfirm = await ReadJsonAsync(await materialsClient.GetAsync("/api/materials/receipts?includeCompleted=true", TestContext.Current.CancellationToken));
        var afterConfirmItem = afterConfirm.RootElement.GetProperty("items").EnumerateArray().Single(candidate => candidate.GetProperty("itemId").GetGuid() == itemId);
        Assert.True(afterConfirmItem.GetProperty("arrivalsClosed").GetBoolean());
        Assert.True(afterConfirmItem.GetProperty("receiptCompleted").GetBoolean());

        var procurementProjection = await ReadProcurementAsync(procurementClient, projectId);
        Assert.True(procurementProjection.RootElement.GetProperty("items")[0].GetProperty("receiptCompleted").GetBoolean());

        var bodyEdit = await materialsClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { itemId, standardLeadTime = "SHOULD-NOT" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, bodyEdit.StatusCode);
    }

    [Fact]
    public async Task CustomerSuppliedMaterial_TracksExpectedRemainingAndBlocksUnsafeChangesAndEarlyClose()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await context.CreateLegacyProjectAsync("PROC-CUSTOMER-SUPPLY", "Customer Supply");
        const string pastDate = "2020-01-01";

        var missingPair = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Invalid Customer Supply", supplyType = "CustomerSupplied" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingPair.StatusCode);

        var create = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Customer Busbar",
                        supplierName = "Reference Vendor",
                        supplyType = "CustomerSupplied",
                        orderQuantity = 10m,
                        orderUnit = "EA",
                        expectedReceiptDate = pastDate
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var created = await ReadJsonAsync(create);
        var createdItem = created.RootElement.GetProperty("items")[0];
        var itemId = createdItem.GetProperty("itemId").GetGuid();
        Assert.Equal("CustomerSupplied", createdItem.GetProperty("supplyType").GetString());
        Assert.Equal(10m, createdItem.GetProperty("orderQuantity").GetDecimal());
        Assert.Equal("EA", createdItem.GetProperty("orderUnit").GetString());
        Assert.Equal(pastDate, createdItem.GetProperty("expectedReceiptDate").GetString());

        var missingReason = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = createdItem.GetProperty("rowVersion").GetInt32(),
                        orderQuantity = 12m,
                        expectedReceiptDate = pastDate
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var measuredUpdate = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "고객 제공 수량 변경",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = createdItem.GetProperty("rowVersion").GetInt32(),
                        orderQuantity = 12m,
                        expectedReceiptDate = pastDate
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, measuredUpdate.StatusCode);
        using (var measuredJson = await ReadJsonAsync(measuredUpdate))
        {
            var measuredItem = measuredJson.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("itemId").GetGuid() == itemId);
            Assert.Equal("CustomerSupplied", measuredItem.GetProperty("supplyType").GetString());
            Assert.Equal(12m, measuredItem.GetProperty("orderQuantity").GetDecimal());
        }
        using (var history = await ReadJsonAsync(await adminClient.GetAsync(
            $"/api/projects/{projectId}/procurement/history",
            TestContext.Current.CancellationToken)))
        {
            var latest = history.RootElement.GetProperty("groups")[0];
            Assert.Equal("고객 제공 수량 변경", latest.GetProperty("reason").GetString());
            Assert.Contains(latest.GetProperty("changes").EnumerateArray(), change =>
                change.GetProperty("fieldName").GetString() == "OrderQuantity"
                && change.GetProperty("oldValue").GetString() == "10"
                && change.GetProperty("newValue").GetString() == "12");
        }

        using (var filtered = await ReadJsonAsync(await materialsClient.GetAsync(
            "/api/materials/receipts?supplyType=CustomerSupplied",
            TestContext.Current.CancellationToken)))
        {
            var material = Assert.Single(filtered.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(itemId, material.GetProperty("itemId").GetGuid());
            Assert.Equal(0m, material.GetProperty("arrivedQuantity").GetDecimal());
            Assert.Equal(12m, material.GetProperty("remainingQuantity").GetDecimal());
            Assert.Equal(0m, material.GetProperty("processingQuantity").GetDecimal());
            Assert.Equal(pastDate, material.GetProperty("expectedReceiptDate").GetString());
            Assert.True(material.GetProperty("customerSupplyOverdue").GetBoolean());
            Assert.Equal(1, filtered.RootElement.GetProperty("summary").GetProperty("customerSuppliedItemCount").GetInt32());
            Assert.Equal(1, filtered.RootElement.GetProperty("summary").GetProperty("customerSupplyOverdueCount").GetInt32());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await materialsClient.GetAsync(
            "/api/materials/receipts?supplyType=Untrusted",
            TestContext.Current.CancellationToken)).StatusCode);

        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 4m, unit = "EA", arrivalDate = pastDate },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using var arrivalJson = await ReadJsonAsync(arrival);
        var receiptId = arrivalJson.RootElement.GetProperty("receiptId").GetGuid();

        var afterArrivalItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Equal(4m, afterArrivalItem.GetProperty("arrivedQuantity").GetDecimal());
        Assert.Equal(12m, afterArrivalItem.GetProperty("remainingQuantity").GetDecimal());
        Assert.Equal(4m, afterArrivalItem.GetProperty("processingQuantity").GetDecimal());

        var unsafeSupplyChange = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "공급 방식 재분류",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = afterArrivalItem.GetProperty("rowVersion").GetInt32(),
                        supplyType = "Purchased"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, unsafeSupplyChange.StatusCode);

        var iqcRequest = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/iqc-requests",
            new { expectedVersion = 1 },
            TestContext.Current.CancellationToken);
        using var iqcRequestJson = await ReadJsonAsync(iqcRequest);
        var attemptId = iqcRequestJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        await context.MarkAttemptLegacyAsync(attemptId);
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{attemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "고객 제공품 검사 합격" },
            TestContext.Current.CancellationToken)).StatusCode);
        var completedConfirm = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/confirm",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken);
        Assert.True(completedConfirm.StatusCode == HttpStatusCode.OK, await completedConfirm.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var beforeEarlyClose = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Equal(4m, beforeEarlyClose.GetProperty("confirmedQuantity").GetDecimal());
        Assert.Equal(0m, beforeEarlyClose.GetProperty("processingQuantity").GetDecimal());
        var earlyClose = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/close-arrivals",
            new
            {
                expectedRowVersion = beforeEarlyClose.GetProperty("rowVersion").GetInt32(),
                reason = "부분 제공 상태 마감 시도"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, earlyClose.StatusCode);

        var currentProcurement = await procurementClient.GetAsync(
            $"/api/projects/{projectId}/procurement",
            TestContext.Current.CancellationToken);
        using var currentProcurementJson = await ReadJsonAsync(currentProcurement);
        var currentProcurementItem = currentProcurementJson.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("itemId").GetGuid() == itemId);
        var correctedQuantity = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "실제 고객 제공 예정량 정정",
                items = new[]
                {
                    new
                    {
                        itemId,
                        expectedRowVersion = currentProcurementItem.GetProperty("rowVersion").GetInt32(),
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Customer Busbar",
                        supplierName = "Reference Vendor",
                        supplyType = "CustomerSupplied",
                        orderQuantity = 4m,
                        orderUnit = "EA",
                        expectedReceiptDate = pastDate
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.True(
            correctedQuantity.StatusCode == HttpStatusCode.OK,
            await correctedQuantity.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            + Environment.NewLine
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        using (var correctedQuantityJson = await ReadJsonAsync(correctedQuantity))
        {
            var correctedItem = correctedQuantityJson.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("itemId").GetGuid() == itemId);
            Assert.Equal(4m, correctedItem.GetProperty("orderQuantity").GetDecimal());
            Assert.Equal("Customer Busbar", correctedItem.GetProperty("orderItem").GetString());
            Assert.Equal("Reference Vendor", correctedItem.GetProperty("supplierName").GetString());
            Assert.Equal("CustomerSupplied", correctedItem.GetProperty("supplyType").GetString());
            Assert.Equal("EA", correctedItem.GetProperty("orderUnit").GetString());

            var correctedClose = await materialsClient.PostAsJsonAsync(
                $"/api/materials/items/{itemId}/close-arrivals",
                new
                {
                    expectedRowVersion = correctedItem.GetProperty("rowVersion").GetInt32(),
                    reason = "정정 수량 전량 입고 완료"
                },
                TestContext.Current.CancellationToken);
            Assert.True(
                correctedClose.StatusCode == HttpStatusCode.OK,
                await correctedClose.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        var correctedMaterial = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Equal(4m, correctedMaterial.GetProperty("orderQuantity").GetDecimal());
        Assert.Equal(4m, correctedMaterial.GetProperty("arrivedQuantity").GetDecimal());
        Assert.Equal(0m, correctedMaterial.GetProperty("remainingQuantity").GetDecimal());
        Assert.Equal(0m, correctedMaterial.GetProperty("processingQuantity").GetDecimal());
        Assert.True(correctedMaterial.GetProperty("arrivalsClosed").GetBoolean());
        Assert.True(correctedMaterial.GetProperty("receiptCompleted").GetBoolean());
        Assert.False(correctedMaterial.GetProperty("customerSupplyOverdue").GetBoolean());
    }

    [Fact]
    public async Task MaterialReceipt_ConcurrentArrivalsCannotExceedOrderQuantity()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var firstMaterialsClient = context.CreateClient("dev-materials");
        using var secondMaterialsClient = context.CreateClient("dev-materials");
        var projectId = await CreateProjectAsync(salesClient, "PROC-RECEIPT-RACE", "Proc Receipt Race");
        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Busbar Support", orderQuantity = 10m, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        var itemId = (await ReadProcurementAsync(procurementClient, projectId)).RootElement
            .GetProperty("items")[0].GetProperty("itemId").GetGuid();
        var payload = new { quantity = 6m, unit = "EA", arrivalDate = "2026-07-15" };

        var responses = await Task.WhenAll(
            firstMaterialsClient.PostAsJsonAsync($"/api/materials/items/{itemId}/receipts", payload, TestContext.Current.CancellationToken),
            secondMaterialsClient.PostAsJsonAsync($"/api/materials/items/{itemId}/receipts", payload, TestContext.Current.CancellationToken));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        var materialItem = await FindMaterialItemAsync(firstMaterialsClient, itemId);
        Assert.Single(materialItem.GetProperty("receipts").EnumerateArray());
        Assert.Equal(6m, materialItem.GetProperty("receipts")[0].GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task CustomerSupplyChangeAndFirstArrival_SerializeOnTheProcurementItemLock()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        var projectId = await CreateProjectAsync(salesClient, "PROC-CUSTOMER-SUPPLY-RACE", "Customer Supply Race");
        var create = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Race Item", orderQuantity = 10m, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken);
        using var createJson = await ReadJsonAsync(create);
        var item = createJson.RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();

        var responses = await Task.WhenAll(
            procurementClient.PatchAsJsonAsync(
                $"/api/projects/{projectId}/procurement",
                new
                {
                    reason = "고객 제공 방식 지정",
                    items = new[]
                    {
                        new
                        {
                            itemId,
                            expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                            supplyType = "CustomerSupplied"
                        }
                    }
                },
                TestContext.Current.CancellationToken),
            materialsClient.PostAsJsonAsync(
                $"/api/materials/items/{itemId}/receipts",
                new { quantity = 6m, unit = "EA", arrivalDate = "2026-07-15" },
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        var allowedProcurementStatuses = new[]
        {
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Conflict
        };
        Assert.True(
            allowedProcurementStatuses.Contains(responses[0].StatusCode),
            $"Unexpected procurement status {responses[0].StatusCode}: {await responses[0].Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}{Environment.NewLine}"
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));

        using var finalProcurement = await ReadProcurementAsync(procurementClient, projectId);
        var finalItem = finalProcurement.RootElement.GetProperty("items")[0];
        Assert.Contains(finalItem.GetProperty("supplyType").GetString(), new[] { "Purchased", "CustomerSupplied" });
        Assert.Equal(10m, finalItem.GetProperty("orderQuantity").GetDecimal());
        Assert.Equal("EA", finalItem.GetProperty("orderUnit").GetString());
        var materialItem = await FindMaterialItemAsync(materialsClient, itemId);
        Assert.Single(materialItem.GetProperty("receipts").EnumerateArray());
        Assert.Equal(6m, materialItem.GetProperty("arrivedQuantity").GetDecimal());
        Assert.Equal(10m, materialItem.GetProperty("remainingQuantity").GetDecimal());
    }

    [Fact]
    public async Task MaterialReceipt_IqcFailureCreatesPendingAndReinspectionPassClosesIt()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var qualityClient = context.CreateClient("dev-quality");
        using var coordinatorClient = context.CreateClient("dev-production");
        var projectId = await context.CreateLegacyProjectAsync("PROC-IQC-FAIL", "Proc IQC Failure");
        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Contactor", orderQuantity = 4, orderUnit = "EA" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        var item = (await ReadProcurementAsync(procurementClient, projectId)).RootElement.GetProperty("items")[0];
        var itemId = item.GetProperty("itemId").GetGuid();

        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{itemId}/receipts",
            new { quantity = 4, unit = "EA", arrivalDate = "2026-07-15" },
            TestContext.Current.CancellationToken);
        using var arrivalJson = await ReadJsonAsync(arrival);
        var receiptId = arrivalJson.RootElement.GetProperty("receiptId").GetGuid();
        var autoAttemptId = arrivalJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        var request = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/iqc-requests",
            new { expectedVersion = 1 },
            TestContext.Current.CancellationToken);
        using var requestJson = await ReadJsonAsync(request);
        var firstAttemptId = requestJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal(autoAttemptId, firstAttemptId);
        await context.MarkAttemptLegacyAsync(firstAttemptId);

        using (var qualityWork = await ReadJsonAsync(await qualityClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken)))
        {
            var iqcWork = Assert.Single(qualityWork.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("linkUrl").GetString() == $"/quality/iqc?request={firstAttemptId}");
            Assert.Equal($"/quality/iqc?request={firstAttemptId}", iqcWork.GetProperty("linkUrl").GetString());
        }

        var failed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{firstAttemptId}/result",
            new { expectedReceiptVersion = 2, result = "Failed", reason = "단자 외관의 균열과 눌림 흔적이 확인되어 조립 안전성 검토가 필요합니다." },
            TestContext.Current.CancellationToken);
        var failedBody = await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(
            failed.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {failed.StatusCode}: {failedBody}{Environment.NewLine}"
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        using var failedJson = await ReadJsonAsync(failed);
        var pendingId = failedJson.RootElement.GetProperty("pendingIssueId").GetGuid();

        using var pending = await ReadJsonAsync(await coordinatorClient.GetAsync($"/api/pending/{pendingId}", TestContext.Current.CancellationToken));
        Assert.Equal("ActionRequested", pending.RootElement.GetProperty("issue").GetProperty("status").GetString());
        Assert.Equal("Urgent", pending.RootElement.GetProperty("issue").GetProperty("priority").GetString());
        Assert.True(await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id = recipient.notification_id
            where notification.project_id = @project_id
              and notification.idempotency_key like 'pending:%:notification:%';
            """,
            projectId) >= 4L);
        using var assignees = await ReadJsonAsync(await coordinatorClient.GetAsync("/api/pending/assignees", TestContext.Current.CancellationToken));
        var assigneeId = assignees.RootElement[0].GetProperty("userId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/assign",
            new { assigneeUserId = assigneeId, expectedVersion = 1, reason = "부적합 조치 담당 지정" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 2, reason = "부적합 조치 시작" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "ReinspectionRequested", expectedVersion = 3, reason = "조치 완료 후 재검사 요청" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(1L, await context.ReadCountAsync(
            """
            select count(*)
            from work_items
            where project_id = @project_id and target_type = 'Pending'
              and target_id = (select id from pending_issues where project_id = @project_id limit 1)
              and status = 'Completed';
            """,
            projectId));

        var reinspection = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/reinspection",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reinspection.StatusCode);
        using var reinspectionJson = await ReadJsonAsync(reinspection);
        var secondAttemptId = reinspectionJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        Assert.Equal(1L, await context.ReadCountAsync(
            """
            select count(*)
            from notifications
            where project_id = @project_id
              and source_kind = 'ReinspectionRequested';
            """,
            projectId));
        using (var qualityWork = await ReadJsonAsync(await qualityClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken)))
        {
            var reinspectionWork = Assert.Single(qualityWork.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("linkUrl").GetString() == $"/quality/iqc?request={secondAttemptId}");
            Assert.Equal($"/quality/iqc?request={secondAttemptId}", reinspectionWork.GetProperty("linkUrl").GetString());
            Assert.Contains("재검사 · P-", reinspectionWork.GetProperty("title").GetString(), StringComparison.Ordinal);
            Assert.Contains("Contactor · 4 EA (2차)", reinspectionWork.GetProperty("title").GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("/quality/iqc", reinspectionWork.GetProperty("description").GetString(), StringComparison.Ordinal);
        }
        using (var reinspectionPending = await ReadJsonAsync(await qualityClient.GetAsync($"/api/pending/{pendingId}", TestContext.Current.CancellationToken)))
        {
            var navigation = reinspectionPending.RootElement.GetProperty("reinspection");
            Assert.Equal(secondAttemptId, navigation.GetProperty("attemptId").GetGuid());
            Assert.Equal(2, navigation.GetProperty("attemptNumber").GetInt32());
            Assert.Equal($"/quality/iqc?request={secondAttemptId}", navigation.GetProperty("linkUrl").GetString());
        }
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notifications
            where project_id = @project_id
              and idempotency_key like 'materials:iqc:%:notification';
            """,
            projectId));
        await context.MarkAttemptLegacyAsync(secondAttemptId);
        var secondFailed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{secondAttemptId}/result",
            new { expectedReceiptVersion = 4, result = "Failed", reason = "재검사에서도 단자 균열과 눌림 흔적이 그대로 남아 있어 교체 재조치가 필요합니다." },
            TestContext.Current.CancellationToken);
        var secondFailedBody = await secondFailed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(secondFailed.StatusCode == HttpStatusCode.OK, $"Expected OK but received {secondFailed.StatusCode}: {secondFailedBody}");
        using (var failedAgainJson = JsonDocument.Parse(secondFailedBody))
        {
            Assert.Equal(pendingId, failedAgainJson.RootElement.GetProperty("pendingIssueId").GetGuid());
        }
        using (var reopenedPending = await ReadJsonAsync(await coordinatorClient.GetAsync($"/api/pending/{pendingId}", TestContext.Current.CancellationToken)))
        {
            Assert.Equal("ActionRequested", reopenedPending.RootElement.GetProperty("issue").GetProperty("status").GetString());
            Assert.Contains(reopenedPending.RootElement.GetProperty("history").EnumerateArray(), history =>
                history.GetProperty("reason").GetString()!.Contains("재검사 부적합", StringComparison.Ordinal));
        }
        Assert.Equal(1L, await context.ReadCountAsync(
            """
            select count(*)
            from work_items
            where project_id = @project_id and target_type = 'Pending'
              and target_id = (select id from pending_issues where project_id = @project_id limit 1)
              and status = 'Requested';
            """,
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            """
            select count(*)
            from notifications
            where project_id = @project_id
              and idempotency_key like 'pending:%:reopened:v%';
            """,
            projectId));
        Assert.True(await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id = recipient.notification_id
            where notification.project_id = @project_id
              and notification.idempotency_key like 'pending:%:reopened:v%';
            """,
            projectId) >= 3L);

        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 5, reason = "단자 교체 재조치 시작" },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinatorClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "ReinspectionRequested", expectedVersion = 6, reason = "단자 교체 조치 완료 후 2차 재검사 요청" },
            TestContext.Current.CancellationToken)).StatusCode);
        var thirdReinspection = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/reinspection",
            new { expectedVersion = 6 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, thirdReinspection.StatusCode);
        using var thirdReinspectionJson = await ReadJsonAsync(thirdReinspection);
        var thirdAttemptId = thirdReinspectionJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        await context.MarkAttemptLegacyAsync(thirdAttemptId);
        var passed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{thirdAttemptId}/result",
            new { expectedReceiptVersion = 6, result = "Passed", reason = "교체 조치와 재검사 결과가 모두 정상입니다." },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, passed.StatusCode);

        using var closedPending = await ReadJsonAsync(await coordinatorClient.GetAsync($"/api/pending/{pendingId}", TestContext.Current.CancellationToken));
        Assert.Equal("Closed", closedPending.RootElement.GetProperty("issue").GetProperty("status").GetString());
        Assert.Contains(closedPending.RootElement.GetProperty("history").EnumerateArray(), history =>
            history.GetProperty("toStatus").GetString() == "Closed");
    }

    [Fact]
    public async Task ProcurementResponse_BuildsDDayTextWithoutReceiptStatusWords()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        var projectId = await context.CreateLegacyProjectAsync("PROC-DDAY", "Proc Dday");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var save = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                items = new object[]
                {
                    new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "No date" },
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
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
            values
              ('{projectId}', 'MaterialsPrimary', '50000000-0000-0000-0000-000000000012', '50000000-0000-0000-0000-000000000002', now()),
              ('{projectId}', 'MaterialsSecondary', '50000000-0000-0000-0000-000000000011', '50000000-0000-0000-0000-000000000002', now());
            """);
        var firstFile = CreateProcurementExcel("Proc Excel", "PROC-EXCEL",
            ["Proc Excel", "PROC-EXCEL", "4W", "MCCB", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "First", ""],
            ["", "", "5W", "Cable", "", "Owner B", "2026.07.02", "2026.07.11", "", ""],
            [" ", "", "", "", "", "", "", "", "", ""]);

        var preview = await PreviewExcelAsync(procurementClient, firstFile, "procurement.xlsx");
        Assert.Equal(2, preview.RootElement.GetProperty("newCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("skippedCount").GetInt32());
        Assert.Equal("Matched", preview.RootElement.GetProperty("projectMatches")[0].GetProperty("matchStatus").GetString());

        var apply = await ApplyExcelAsync(procurementClient, firstFile, "procurement.xlsx", preview, reason: null);
        Assert.True(
            apply.StatusCode == HttpStatusCode.OK,
            $"{await apply.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}{Environment.NewLine}"
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        var saved = await ReadProcurementAsync(procurementClient, projectId);
        Assert.Equal(2, saved.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("Vendor X", saved.RootElement.GetProperty("items")[0].GetProperty("supplierName").GetString());
        Assert.Equal("2026-07-10", saved.RootElement.GetProperty("items")[0].GetProperty("expectedReceiptDate").GetString());
        Assert.Equal("2026-10-10", saved.RootElement.GetProperty("items")[0].GetProperty("shipmentDisplayDate").GetString());
        Assert.DoesNotContain(saved.RootElement.GetProperty("items")[0].EnumerateObject(), property => property.NameEquals("shipmentText"));
        Assert.Equal(4L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification';",
            projectId));

        var duplicatePreview = await PreviewExcelAsync(procurementClient, firstFile, "procurement.xlsx");
        var duplicate = await ApplyExcelAsync(procurementClient, firstFile, "procurement.xlsx", duplicatePreview, reason: null);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(0, duplicatePreview.RootElement.GetProperty("newCount").GetInt32() + duplicatePreview.RootElement.GetProperty("changedCount").GetInt32());
        Assert.Equal(4L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%';",
            projectId));

        var secondFile = CreateProcurementExcel("Proc Excel", "PROC-EXCEL",
            ["Proc Excel", "PROC-EXCEL", "4W", "MCCB", "Vendor X", "Owner A", "2026-07-01", "2026-07-10", "First changed", ""],
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
        Assert.Equal(8L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id=@project_id and idempotency_key like 'procurement:materials:%';",
            projectId));
        Assert.Equal(4L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id=@project_id and idempotency_key like 'procurement:materials:%:notification';",
            projectId));
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
        Assert.True(
            apply.StatusCode == HttpStatusCode.OK,
            $"{await apply.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}{Environment.NewLine}"
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
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
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Relay",
                        technicalOwner = "Owner",
                        orderDate = "2026-07-01",
                        expectedReceiptDate = "2026-07-10",
                        issueNote = "none"
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
        Assert.Equal(new[] { "PJT", "PJT CODE", "통상납기", "발주품목", "구분", "업체", "기술 담당자", "발주일", "입고예정일", "이슈사항", "입고 완료" },
            Enumerable.Range(1, 11).Select(column => worksheet.Cell(3, column).GetString()).ToArray());
        Assert.Equal("Proc Template", worksheet.Cell(4, 1).GetString());
        Assert.Equal("Relay", worksheet.Cell(4, 4).GetString());
        Assert.Equal("기타", worksheet.Cell(4, 5).GetString());
        Assert.Equal("none", worksheet.Cell(4, 10).GetString());
        Assert.Equal("", worksheet.Cell(4, 11).GetString());
        Assert.True(worksheet.SheetView.SplitRow >= 3);
        Assert.True(worksheet.AutoFilter.IsEnabled);
        Assert.True(worksheet.Column(4).Width >= 18);
        Assert.True(worksheet.Column(11).Width >= worksheet.Column(7).Width);
        for (var column = 1; column <= 11; column++)
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
        using var qualityClient = context.CreateClient("dev-quality");
        var projectId = await context.CreateLegacyProjectAsync("PROC-DASH", "Proc Dashboard");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureReceiptDate = today.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var pastReceiptDate = today.AddDays(-30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var save = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "dashboard setup",
                items = new object[]
                {
                    new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Pending Item", expectedReceiptDate = futureReceiptDate },
                    new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Past Pending Item", expectedReceiptDate = pastReceiptDate },
                    new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Completed Item", expectedReceiptDate = pastReceiptDate, orderQuantity = 1, orderUnit = "EA" }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        using var procurement = await ReadProcurementAsync(procurementClient, projectId);
        var items = procurement.RootElement.GetProperty("items").EnumerateArray().ToList();
        var completedItem = items.Single(item => item.GetProperty("orderItem").GetString() == "Completed Item");
        var completedItemId = completedItem.GetProperty("itemId").GetGuid();
        var arrival = await materialsClient.PostAsJsonAsync(
            $"/api/materials/items/{completedItemId}/receipts",
            new { quantity = 1, unit = "EA", arrivalDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using var arrivalJson = await ReadJsonAsync(arrival);
        var receiptId = arrivalJson.RootElement.GetProperty("receiptId").GetGuid();
        var iqcRequest = await materialsClient.PostAsJsonAsync($"/api/materials/receipts/{receiptId}/iqc-requests", new { expectedVersion = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, iqcRequest.StatusCode);
        using var iqcRequestJson = await ReadJsonAsync(iqcRequest);
        var attemptId = iqcRequestJson.RootElement.GetProperty("iqcAttemptId").GetGuid();
        await context.MarkAttemptLegacyAsync(attemptId);
        Assert.Equal(HttpStatusCode.OK, (await qualityClient.PostAsJsonAsync(
            $"/api/quality/iqc/{attemptId}/result",
            new { expectedReceiptVersion = 2, result = "Passed", reason = "검사 합격" },
            TestContext.Current.CancellationToken)).StatusCode);
        var dashboardConfirm = await materialsClient.PostAsJsonAsync(
            $"/api/materials/receipts/{receiptId}/confirm",
            new { expectedVersion = 3 },
            TestContext.Current.CancellationToken);
        Assert.True(
            dashboardConfirm.StatusCode == HttpStatusCode.OK,
            $"{await dashboardConfirm.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}{Environment.NewLine}{string.Join(Environment.NewLine, context.Logs.Select(log => log.Exception?.ToString() ?? log.Message))}");
        using var materialAfterConfirm = await ReadJsonAsync(await materialsClient.GetAsync("/api/materials/receipts?includeCompleted=true", TestContext.Current.CancellationToken));
        var materialItem = materialAfterConfirm.RootElement.GetProperty("items").EnumerateArray().Single(candidate => candidate.GetProperty("itemId").GetGuid() == completedItemId);
        Assert.True(materialItem.GetProperty("arrivalsClosed").GetBoolean());
        Assert.True(materialItem.GetProperty("receiptCompleted").GetBoolean());

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
        Assert.Equal(pastReceiptDate, project.GetProperty("nearestExpectedReceiptDate").GetString());
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
            new
            {
                reason = "partial required procurement",
                items = new[]
                {
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = " 차단기 ",
                        supplierName = "필수품목 공급사",
                        orderDate = "2026-07-01",
                        expectedReceiptDate = "2026-07-15"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, oneRequired.StatusCode);
        using var partialWorkflow = await ReadJsonAsync(await procurementClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var partialProcurementStage = partialWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "ProcurementInfo");
        Assert.Equal("PartiallyCompleted", partialProcurementStage.GetProperty("status").GetString());

        var current = await ReadProcurementAsync(procurementClient, projectId);
        var existing = current.RootElement.GetProperty("items")[0];
        var completedRequired = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new
            {
                reason = "all required procurement",
                items = new object[]
                {
                    new
                    {
                        itemId = existing.GetProperty("itemId").GetGuid(),
                        expectedRowVersion = existing.GetProperty("rowVersion").GetInt32(),
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "차단기",
                        supplierName = "필수품목 공급사",
                        orderDate = "2026-07-01",
                        expectedReceiptDate = "2026-07-15"
                    },
                    new
                    {
                        materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "외함",
                        supplierName = "필수품목 공급사",
                        orderDate = "2026-07-02",
                        expectedReceiptDate = "2026-07-16"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completedRequired.StatusCode);
        using var completedWorkflow = await ReadJsonAsync(await procurementClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        var completedProcurementStage = completedWorkflow.RootElement.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stageCode").GetString() == "ProcurementInfo");
        Assert.Equal("Completed", completedProcurementStage.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PanelKitting_CompletesSelectedPanelsExactlyOnceAndClosesProjectOnLastPanel()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var adminClient = context.CreateClient("dev-admin");
        var projectId = await CreateProjectAsync(salesClient, "KIT-010A", "Panel Kitting", 2);

        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Kitting Material" } } },
            TestContext.Current.CancellationToken)).StatusCode);

        using (var blockedQueue = await ReadJsonAsync(await materialsClient.GetAsync(
                   $"/api/materials/kitting?projectId={projectId}",
                   TestContext.Current.CancellationToken)))
        {
            var blockedProject = Assert.Single(blockedQueue.RootElement.GetProperty("projects").EnumerateArray());
            Assert.False(blockedProject.GetProperty("ready").GetBoolean());
            Assert.All(blockedProject.GetProperty("panels").EnumerateArray(), panel =>
                Assert.False(panel.GetProperty("selectable").GetBoolean()));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { Guid.NewGuid() } },
            TestContext.Current.CancellationToken)).StatusCode);

        await context.ExecuteSqlAsync($"""
            update panel_placeholders
            set panel_info_completed = true,
                panel_name = coalesce(panel_name, display_code),
                width_mm = coalesce(width_mm, 600),
                height_mm = coalesce(height_mm, 1800),
                depth_mm = coalesce(depth_mm, 400)
            where project_id = '{projectId}' and status = 'Active';
            """);
        using var readyQueue = await ReadJsonAsync(await materialsClient.GetAsync(
            $"/api/materials/kitting?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var readyProject = Assert.Single(readyQueue.RootElement.GetProperty("projects").EnumerateArray());
        Assert.False(readyProject.GetProperty("ready").GetBoolean());
        var panels = readyProject.GetProperty("panels").EnumerateArray().ToList();
        Assert.Equal(2, panels.Count);
        Assert.All(panels, panel => Assert.True(panel.GetProperty("selectable").GetBoolean()));

        var firstPanelId = panels[0].GetProperty("panelId").GetGuid();
        var firstOperationId = Guid.NewGuid();
        var first = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = firstOperationId, projectId, panelIds = new[] { firstPanelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using (var firstJson = await ReadJsonAsync(first))
        {
            Assert.Equal(1, firstJson.RootElement.GetProperty("completedPanelCount").GetInt32());
            Assert.Equal(0, firstJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
            Assert.False(firstJson.RootElement.GetProperty("projectKittingCompleted").GetBoolean());
            Assert.False(firstJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        using (var partialWorkflow = await ReadJsonAsync(await materialsClient.GetAsync(
                   $"/api/projects/{projectId}/workflow",
                   TestContext.Current.CancellationToken)))
        {
            var kittingStage = partialWorkflow.RootElement.GetProperty("stages").EnumerateArray()
                .Single(stage => stage.GetProperty("stageCode").GetString() == "KittingCompleted");
            Assert.Equal("NotStarted", kittingStage.GetProperty("status").GetString());
            Assert.Equal("미시작", kittingStage.GetProperty("statusLabel").GetString());
        }

        var replay = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = firstOperationId, projectId, panelIds = new[] { firstPanelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using (var replayJson = await ReadJsonAsync(replay))
        {
            Assert.Equal(0, replayJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
            Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
        }

        Assert.Equal(HttpStatusCode.Conflict, (await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { firstPanelId } },
            TestContext.Current.CancellationToken)).StatusCode);

        var secondPanelId = panels[1].GetProperty("panelId").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = firstOperationId, projectId, panelIds = new[] { secondPanelId } },
            TestContext.Current.CancellationToken)).StatusCode);

        var cancelFirstPanel = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            new
            {
                panelCount = 1,
                expectedActivePanelCount = 2,
                cancelPanelIds = new[] { firstPanelId },
                reason = "완료 패널 제조 업무 취소 회귀"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancelFirstPanel.StatusCode);
        Assert.Equal(0L, await context.ReadCountAsync(
            $"select count(*) from work_items where project_id = @project_id and target_id = '{firstPanelId}' and workflow_stage_code = 'ManufacturingWork' and status = 'Cancelled';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from panel_kitting_completions where project_id = @project_id;",
            projectId));

        var last = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { secondPanelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        using (var lastJson = await ReadJsonAsync(last))
        {
            Assert.True(lastJson.RootElement.GetProperty("projectKittingCompleted").GetBoolean());
            Assert.Equal(0, lastJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
        }

        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from panel_kitting_completions where project_id = @project_id;",
            projectId));
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'ManufacturingWork';",
            projectId));
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from project_workflow_events where project_id = @project_id and stage_code = 'KittingCompleted' and event_status = 'Succeeded';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from notifications where project_id = @project_id and idempotency_key like 'kitting:operation:%:reference';",
            projectId));
        using (var completedWorkflow = await ReadJsonAsync(await materialsClient.GetAsync(
                   $"/api/projects/{projectId}/workflow",
                   TestContext.Current.CancellationToken)))
        {
            var kittingStage = completedWorkflow.RootElement.GetProperty("stages").EnumerateArray()
                .Single(stage => stage.GetProperty("stageCode").GetString() == "KittingCompleted");
            Assert.Equal("NotStarted", kittingStage.GetProperty("status").GetString());
        }

        await context.ExecuteSqlAsync($"update projects set status='Completed' where id='{projectId}';");
        using var completedProjectQueue = await ReadJsonAsync(await materialsClient.GetAsync(
            $"/api/materials/kitting?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var completedProject = Assert.Single(completedProjectQueue.RootElement.GetProperty("projects").EnumerateArray());
        Assert.Equal(projectId, completedProject.GetProperty("projectId").GetGuid());
        Assert.Contains(completedProject.GetProperty("panels").EnumerateArray(), panel =>
            panel.GetProperty("kittingCompleted").GetBoolean());
    }

    [Fact]
    public async Task WorkflowAndQualityReconciliation_DerivePartialLqcAndRecoverMissingOqcOnce()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var qualityClient = context.CreateClient("dev-quality");
        await context.ExecuteSqlAsync("""
            update lqc_item_settings setting
            set is_operational=true, row_version=row_version+1, updated_at_utc=now()
            from production_product_types product_type
            where product_type.id=setting.product_type_id and product_type.code='UL67';
            """);
        var projectId = await CreateProjectAsync(salesClient, "QUALITY-RECONCILE", "Quality Reconcile", 2);

        await context.ExecuteSqlAsync($"""
            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            ) values
                ('{projectId}', 'QualityOQC', '50000000-0000-0000-0000-000000000005', '{SalesOwnerUserId}', now()),
                ('{projectId}', 'QualityOQCSecondary', '50000000-0000-0000-0000-000000000001', '{SalesOwnerUserId}', now())
            on conflict (project_id, responsibility_type) do update
            set assigned_user_id = excluded.assigned_user_id,
                assigned_by_user_id = excluded.assigned_by_user_id,
                assigned_at_utc = excluded.assigned_at_utc;

            with selected_panel as (
                select id
                from panel_placeholders
                where project_id = '{projectId}' and status = 'Active'
                order by display_code
                limit 1
            ),
            inserted_work as (
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, title, description, status, priority, idempotency_key,
                    created_by_user_id, started_at_utc, completed_at_utc
                )
                select
                    '{projectId}', 'Panel', id, 'LQC', 'QualityLQC',
                    '50000000-0000-0000-0000-000000000005',
                    'LQC 완료 fixture', '재조정 회귀 fixture', 'Completed', 'Normal',
                    'test:quality-reconcile:' || id || ':lqc',
                    '50000000-0000-0000-0000-000000000005', now(), now()
                from selected_panel
                returning id, target_id
            )
            insert into panel_quality_inspection_attempts (
                project_id, panel_id, stage_code, attempt_number, status, work_item_id,
                decision_mode, version, started_by_user_id, started_at_utc,
                completed_by_user_id, completed_at_utc
            )
            select
                '{projectId}', target_id, 'LQC', 1, 'Passed', id,
                'Checklist', 1,
                '50000000-0000-0000-0000-000000000005', now(),
                '50000000-0000-0000-0000-000000000005', now()
            from inserted_work;

            insert into panel_manufacturing_executions (
                project_id, panel_id, status, started_by_user_id, started_at_utc,
                completed_by_user_id, completed_at_utc, version, updated_at_utc
            )
            select
                '{projectId}', id, 'Completed',
                '50000000-0000-0000-0000-000000000004', now(),
                '50000000-0000-0000-0000-000000000004', now(), 1, now()
            from panel_placeholders
            where project_id = '{projectId}' and status = 'Active';
            """);

        using (var workflow = await ReadJsonAsync(await qualityClient.GetAsync(
                   $"/api/projects/{projectId}/workflow",
                   TestContext.Current.CancellationToken)))
        {
            var lqcStage = workflow.RootElement.GetProperty("stages").EnumerateArray()
                .Single(stage => stage.GetProperty("stageCode").GetString() == "LQC");
            Assert.Equal("PartiallyCompleted", lqcStage.GetProperty("status").GetString());
            Assert.Equal("부분 완료", lqcStage.GetProperty("statusLabel").GetString());
        }

        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'OQC';",
            projectId));

        var reconciled = await qualityClient.PostAsync(
            "/api/quality/inspections/reconcile",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        using (var reconciledJson = await ReadJsonAsync(reconciled))
        {
            Assert.Equal(1, reconciledJson.RootElement.GetProperty("recoveredLqcHandoffCount").GetInt32());
            Assert.Equal(1, reconciledJson.RootElement.GetProperty("recoveredOqcHandoffCount").GetInt32());
            Assert.Equal(0, reconciledJson.RootElement.GetProperty("unresolvedAssigneeCount").GetInt32());
        }
        Assert.Equal(2L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'LQC';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from panel_manufacturing_completion_confirmations where project_id = @project_id;",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'OQC';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id = recipient.notification_id
            join work_items work on work.id = notification.work_item_id
            where work.project_id = @project_id
              and work.workflow_stage_code = 'OQC';
            """,
            projectId));

        using (var replay = await ReadJsonAsync(await qualityClient.PostAsync(
                   "/api/quality/inspections/reconcile",
                   null,
                   TestContext.Current.CancellationToken)))
        {
            Assert.Equal(0, replay.RootElement.GetProperty("recoveredLqcHandoffCount").GetInt32());
            Assert.Equal(0, replay.RootElement.GetProperty("recoveredOqcHandoffCount").GetInt32());
            Assert.Equal(0, replay.RootElement.GetProperty("recoveredInspectionHandoffCount").GetInt32());
            Assert.Equal(0, replay.RootElement.GetProperty("recoveredPackingHandoffCount").GetInt32());
        }
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'OQC';",
            projectId));
    }

    [Theory]
    [InlineData("LQC", "QualityLQC")]
    [InlineData("OQC", "QualityOQC")]
    public async Task PanelQualityReinspection_OnlyExposesFailedItemsAndAllowsOtherDepartmentsToComment(
        string stageCode,
        string responsibility)
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var qualityClient = context.CreateClient("dev-quality");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        using var readOnlyClient = context.CreateClient("dev-viewer");
        if (stageCode == "LQC")
        {
            await context.ExecuteSqlAsync("""
                update lqc_item_settings setting
                set is_operational=true, row_version=row_version+1, updated_at_utc=now()
                from production_product_types product_type
                where product_type.id=setting.product_type_id and product_type.code='UL67';
                """);
        }
        var projectId = await CreateProjectAsync(salesClient, $"REINSPECT-{stageCode}", $"{stageCode} Reinspection", 1);

        using var panelList = await ReadJsonAsync(await salesClient.GetAsync(
            $"/api/projects/{projectId}/panels",
            TestContext.Current.CancellationToken));
        var panelId = Assert.Single(panelList.RootElement.EnumerateArray()).GetProperty("panelId").GetGuid();
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            ) values (
                '{projectId}', '{responsibility}', '50000000-0000-0000-0000-000000000005',
                '{SalesOwnerUserId}', now()
            )
            on conflict (project_id, responsibility_type) do update
            set assigned_user_id = excluded.assigned_user_id,
                assigned_by_user_id = excluded.assigned_by_user_id,
                assigned_at_utc = excluded.assigned_at_utc;

            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id
            ) values (
                '{projectId}', 'Panel', '{panelId}', '{stageCode}', '{responsibility}',
                '50000000-0000-0000-0000-000000000005', 'quality',
                '{stageCode} 검사', '재검사 범위 회귀', 'Requested', 'Normal',
                'test:reinspection:{stageCode}:{panelId}', '{SalesOwnerUserId}'
            );
            """);
        if (stageCode == "LQC")
        {
            await context.ExecuteSqlAsync($"""
                with execution as (
                    insert into panel_manufacturing_executions (
                        project_id, panel_id, status, started_by_user_id, started_at_utc,
                        completed_by_user_id, completed_at_utc, version, updated_at_utc
                    ) values (
                        '{projectId}', '{panelId}', 'Completed',
                        '50000000-0000-0000-0000-000000000004', now(),
                        '50000000-0000-0000-0000-000000000004', now(), 1, now()
                    )
                    returning id
                )
                insert into panel_manufacturing_execution_steps (execution_id, sequence_number, step_name, checked_by_user_id, checked_at_utc)
                select id, step_number, '제조 ' || step_number || '단계',
                       '50000000-0000-0000-0000-000000000004', now()
                from execution cross join generate_series(1, 4) step_number;
                """);
        }

        var start = await qualityClient.PostAsJsonAsync(
            "/api/quality/inspections/start",
            new { operationId = Guid.NewGuid(), projectId, panelId, stageCode },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var startJson = await ReadJsonAsync(start);
        var reportId = startJson.RootElement.GetProperty("reportId").GetGuid();

        using var detail = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/quality/inspections/panels/{panelId}?stage={stageCode}",
            TestContext.Current.CancellationToken));
        var reportVersion = detail.RootElement.GetProperty("reportVersion").GetInt32();
        var requiredItems = detail.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("isRequired").GetBoolean())
            .ToList();
        Assert.True(requiredItems.Count >= 2);
        var failedItemId = requiredItems[0].GetProperty("itemId").GetGuid();
        var nonTargetItemId = requiredItems[1].GetProperty("itemId").GetGuid();
        var responses = requiredItems.Select((item, index) => new
        {
            templateItemId = item.GetProperty("itemId").GetGuid(),
            checkResult = index == 0 ? "Fail" : "Pass",
            textValue = (string?)null,
            note = index == 0 ? "체결 상태가 검사 기준과 일치하지 않아 재조치가 필요합니다." : null
        }).ToArray();

        var failed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/inspections/reports/{reportId}/finalize",
            new
            {
                operationId = Guid.NewGuid(),
                expectedReportVersion = reportVersion,
                result = "Failed",
                reason = "검사 항목에서 기준 미달이 확인되어 제조 부서 재조치가 필요합니다.",
                actionDepartmentCode = "manufacturing",
                assigneeUserId = Guid.Parse("50000000-0000-0000-0000-000000000004"),
                responses
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        using var failedJson = await ReadJsonAsync(failed);
        var pendingId = failedJson.RootElement.GetProperty("pendingId").GetGuid();

        using (var initialFailedDetail = await ReadJsonAsync(await qualityClient.GetAsync(
                   $"/api/quality/inspections/panels/{panelId}?stage={stageCode}",
                   TestContext.Current.CancellationToken)))
        {
            var initialItems = initialFailedDetail.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.True(initialItems.Count >= requiredItems.Count);
            Assert.All(initialItems, item => Assert.False(item.GetProperty("isReinspectionTarget").GetBoolean()));
        }

        var comment = await salesClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/comments",
            new { body = "영업 부서에서 고객 일정 영향을 확인했습니다." },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);
        using (var commentJson = await ReadJsonAsync(comment))
        {
            Assert.True(commentJson.RootElement.GetProperty("canComment").GetBoolean());
            Assert.Contains(
                commentJson.RootElement.GetProperty("comments").EnumerateArray(),
                item => item.GetProperty("body").GetString() == "영업 부서에서 고객 일정 영향을 확인했습니다.");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await salesClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 1, reason = "권한 경계 확인" },
            TestContext.Current.CancellationToken)).StatusCode);
        using (var readOnlyDetail = await ReadJsonAsync(await readOnlyClient.GetAsync(
                   $"/api/pending/{pendingId}",
                   TestContext.Current.CancellationToken)))
        {
            Assert.False(readOnlyDetail.RootElement.GetProperty("canComment").GetBoolean());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await readOnlyClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/comments",
            new { body = "조회 전용 계정은 코멘트를 등록할 수 없습니다." },
            TestContext.Current.CancellationToken)).StatusCode);

        var inProgress = await manufacturingClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "InProgress", expectedVersion = 1, reason = "부적합 항목 조치를 시작합니다." },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, inProgress.StatusCode);

        var actionPhotoBytes = await File.ReadAllBytesAsync(
            Path.Combine(context.RepositoryRoot, "..", "frontend", "src", "assets", "emi-logo.png"),
            TestContext.Current.CancellationToken);
        Guid actionPhotoId;
        using (var actionPhotoForm = new MultipartFormDataContent())
        {
            actionPhotoForm.Add(new StringContent(Guid.NewGuid().ToString("D")), "operationId");
            actionPhotoForm.Add(new StringContent("2"), "expectedPendingVersion");
            actionPhotoForm.Add(new StringContent("부적합 조치 완료 사진"), "altText");
            var actionPhotoContent = new ByteArrayContent(actionPhotoBytes);
            actionPhotoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            actionPhotoForm.Add(actionPhotoContent, "photo", "action-evidence.png");
            using var actionPhoto = await ReadJsonAsync(await manufacturingClient.PostAsync(
                $"/api/pending/{pendingId}/photos",
                actionPhotoForm,
                TestContext.Current.CancellationToken));
            actionPhotoId = actionPhoto.RootElement.GetProperty("photoId").GetGuid();
            var actionDetail = actionPhoto.RootElement.GetProperty("detail");
            Assert.Equal(3, actionDetail.GetProperty("issue").GetProperty("version").GetInt32());
            Assert.Single(actionDetail.GetProperty("actionEvidence").GetProperty("draftPhotos").EnumerateArray());
        }
        using (var qualityPendingBeforeConfirmation = await ReadJsonAsync(await qualityClient.GetAsync(
                   $"/api/pending/{pendingId}",
                   TestContext.Current.CancellationToken)))
        {
            Assert.Equal(
                JsonValueKind.Null,
                qualityPendingBeforeConfirmation.RootElement.GetProperty("actionEvidence").GetProperty("draftPhotos").ValueKind);
        }

        var requested = await manufacturingClient.PostAsJsonAsync(
            $"/api/pending/{pendingId}/transition",
            new { toStatus = "ReinspectionRequested", expectedVersion = 3, reason = "부적합 항목 조치를 완료했습니다." },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.Equal(1L, await context.ReadCountAsync(
            """
            select count(*)
            from notifications
            where project_id = @project_id
              and source_kind = 'ReinspectionRequested';
            """,
            projectId));
        using (var requestedJson = await ReadJsonAsync(requested))
        {
            var round = Assert.Single(requestedJson.RootElement
                .GetProperty("actionEvidence")
                .GetProperty("confirmedRounds")
                .EnumerateArray());
            Assert.Equal("부적합 항목 조치를 완료했습니다.", round.GetProperty("actionReasonSnapshot").GetString());
            Assert.Equal(actionPhotoId, Assert.Single(round.GetProperty("photos").EnumerateArray()).GetProperty("photoId").GetGuid());
        }
        Assert.Equal(
            HttpStatusCode.OK,
            (await qualityClient.GetAsync(
                $"/api/pending/{pendingId}/photos/{actionPhotoId}/content",
                TestContext.Current.CancellationToken)).StatusCode);

        using var reinspection = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/quality/inspections/panels/{panelId}?stage={stageCode}",
            TestContext.Current.CancellationToken));
        var reinspectionReportId = reinspection.RootElement.GetProperty("reportId").GetGuid();
        var reinspectionVersion = reinspection.RootElement.GetProperty("reportVersion").GetInt32();
        var reinspectionItem = Assert.Single(reinspection.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(failedItemId, reinspectionItem.GetProperty("itemId").GetGuid());
        Assert.True(reinspectionItem.GetProperty("isReinspectionTarget").GetBoolean());
        Assert.Contains("재조치", reinspectionItem.GetProperty("previousFailureEvidence").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            actionPhotoId,
            Assert.Single(reinspection.RootElement
                .GetProperty("reinspectionEvidence")
                .GetProperty("latestActionRound")
                .GetProperty("photos")
                .EnumerateArray()).GetProperty("photoId").GetGuid());

        var outOfScope = await qualityClient.PutAsJsonAsync(
            $"/api/quality/inspections/reports/{reinspectionReportId}/responses",
            new
            {
                operationId = Guid.NewGuid(),
                expectedReportVersion = reinspectionVersion,
                responses = new[]
                {
                    new { templateItemId = failedItemId, checkResult = "Pass", textValue = (string?)null, note = (string?)null },
                    new { templateItemId = nonTargetItemId, checkResult = "Pass", textValue = (string?)null, note = (string?)null }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, outOfScope.StatusCode);

        if (stageCode == "OQC")
        {
            var failedAgain = await qualityClient.PostAsJsonAsync(
                $"/api/quality/inspections/reports/{reinspectionReportId}/finalize",
                new
                {
                    operationId = Guid.NewGuid(),
                    expectedReportVersion = reinspectionVersion,
                    result = "Failed",
                    reason = "재검사에서도 동일 항목이 기준에 미달하여 추가 재조치가 필요합니다.",
                    actionDepartmentCode = (string?)null,
                    assigneeUserId = (Guid?)null,
                    responses = new[]
                    {
                        new { templateItemId = failedItemId, checkResult = "Fail", textValue = (string?)null, note = "동일 부적합이 남아 있습니다." }
                    }
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, failedAgain.StatusCode);
            using (var reopened = await ReadJsonAsync(await qualityClient.GetAsync(
                       $"/api/pending/{pendingId}",
                       TestContext.Current.CancellationToken)))
            {
                Assert.Equal("ActionRequested", reopened.RootElement.GetProperty("issue").GetProperty("status").GetString());
                Assert.Equal(5, reopened.RootElement.GetProperty("issue").GetProperty("version").GetInt32());
            }

            var inProgressAgain = await manufacturingClient.PostAsJsonAsync(
                $"/api/pending/{pendingId}/transition",
                new { toStatus = "InProgress", expectedVersion = 5, reason = "추가 재조치를 시작합니다." },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, inProgressAgain.StatusCode);
            var requestedAgain = await manufacturingClient.PostAsJsonAsync(
                $"/api/pending/{pendingId}/transition",
                new { toStatus = "ReinspectionRequested", expectedVersion = 6, reason = "추가 재조치를 완료했습니다." },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, requestedAgain.StatusCode);
            using var secondReinspection = await ReadJsonAsync(await qualityClient.GetAsync(
                $"/api/quality/inspections/panels/{panelId}?stage={stageCode}",
                TestContext.Current.CancellationToken));
            reinspectionReportId = secondReinspection.RootElement.GetProperty("reportId").GetGuid();
            reinspectionVersion = secondReinspection.RootElement.GetProperty("reportVersion").GetInt32();
            var secondItem = Assert.Single(secondReinspection.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(failedItemId, secondItem.GetProperty("itemId").GetGuid());
            Assert.True(secondItem.GetProperty("isReinspectionTarget").GetBoolean());
        }

        var passed = await qualityClient.PostAsJsonAsync(
            $"/api/quality/inspections/reports/{reinspectionReportId}/finalize",
            new
            {
                operationId = Guid.NewGuid(),
                expectedReportVersion = reinspectionVersion,
                result = "Passed",
                reason = "재조치 결과 적합합니다.",
                actionDepartmentCode = (string?)null,
                assigneeUserId = (Guid?)null,
                responses = new[]
                {
                    new { templateItemId = failedItemId, checkResult = "Pass", textValue = (string?)null, note = (string?)null }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.True(
            passed.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {passed.StatusCode}: {await passed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}{Environment.NewLine}"
            + string.Join(Environment.NewLine, context.Logs.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        using var closedPending = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/pending/{pendingId}",
            TestContext.Current.CancellationToken));
        Assert.Equal("Closed", closedPending.RootElement.GetProperty("issue").GetProperty("status").GetString());

        using (var completedDetail = await ReadJsonAsync(await qualityClient.GetAsync(
                   $"/api/quality/inspections/panels/{panelId}?stage={stageCode}",
                   TestContext.Current.CancellationToken)))
        {
            var completedItems = completedDetail.RootElement.GetProperty("items").EnumerateArray().ToList();
            var completedResponses = completedDetail.RootElement.GetProperty("responses").EnumerateArray().ToList();
            Assert.True(completedItems.Count >= requiredItems.Count);
            Assert.Contains(completedItems, item => item.GetProperty("itemId").GetGuid() == nonTargetItemId);
            Assert.Contains(completedItems, item =>
                item.GetProperty("itemId").GetGuid() == failedItemId
                && item.GetProperty("isReinspectionTarget").GetBoolean());
            Assert.Contains(completedResponses, item =>
                item.GetProperty("templateItemId").GetGuid() == nonTargetItemId
                && item.GetProperty("checkResult").GetString() == "Pass");
            Assert.Contains(completedResponses, item =>
                item.GetProperty("templateItemId").GetGuid() == failedItemId
                && item.GetProperty("checkResult").GetString() == "Pass");
        }

        using var passedQueue = await ReadJsonAsync(await qualityClient.GetAsync(
            $"/api/quality/inspections/queue?stage={stageCode}&projectId={projectId}",
            TestContext.Current.CancellationToken));
        var passedProject = Assert.Single(passedQueue.RootElement.GetProperty("projects").EnumerateArray());
        var passedPanel = Assert.Single(passedProject.GetProperty("panels").EnumerateArray());
        Assert.Equal("Passed", passedPanel.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, passedPanel.GetProperty("pendingId").ValueKind);
        Assert.Equal(JsonValueKind.Null, passedPanel.GetProperty("pendingNumber").ValueKind);
    }

    [Fact]
    public async Task PanelKitting_RemainsAvailableWhenNoManufacturingAssigneeExists()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        var projectId = await CreateProjectAsync(salesClient, "KIT-NO-OWNER", "Panel Kitting No Owner", 1);

        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Kitting Material" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        await context.PreparePanelKittingAsync(projectId);
        await context.ExecuteSqlAsync("""
            update qms_users
            set is_active = false
            where id in (
                select user_role.user_id
                from user_roles user_role
                join role_permissions role_permission on role_permission.role_id = user_role.role_id
                join permissions permission on permission.id = role_permission.permission_id
                where permission.code = 'manufacturing.update'
            );
            """);

        using var queue = await ReadJsonAsync(await materialsClient.GetAsync(
            $"/api/materials/kitting?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var panelId = Assert.Single(
            Assert.Single(queue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray()).GetProperty("panelId").GetGuid();
        var response = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var responseJson = await ReadJsonAsync(response))
        {
            Assert.Equal(0, responseJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
        }
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from panel_kitting_batches where project_id = @project_id;",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from panel_kitting_completions where project_id = @project_id;",
            projectId));
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'ManufacturingWork';",
            projectId));
    }

    [Fact]
    public async Task ManufacturingRelease_AfterKittingCreatesOneWorkAndNotifiesBothAssignees()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var materialsClient = context.CreateClient("dev-materials");
        using var productionClient = context.CreateClient("dev-production");
        var projectId = await CreateProjectAsync(salesClient, "MFG-KIT-FIRST", "Kitting Before Release", 1);
        await context.PreparePanelKittingAsync(projectId);
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            ) values
                ('{projectId}', 'ManufacturingPrimary', '50000000-0000-0000-0000-000000000004', '{SalesOwnerUserId}', now()),
                ('{projectId}', 'ManufacturingSecondary', '50000000-0000-0000-0000-000000000001', '{SalesOwnerUserId}', now());
            """);

        using var kittingQueue = await ReadJsonAsync(await materialsClient.GetAsync(
            $"/api/materials/kitting?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var panelId = Assert.Single(
            Assert.Single(kittingQueue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray()).GetProperty("panelId").GetGuid();

        var kitting = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kitting.StatusCode);
        using (var kittingJson = await ReadJsonAsync(kitting))
        {
            Assert.Equal(0, kittingJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
        }

        var release = await productionClient.PostAsJsonAsync(
            "/api/manufacturing/releases",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'ManufacturingWork';",
            projectId));
        Assert.Equal(2L, await context.ReadCountAsync(
            """
            select count(*)
            from notification_recipients recipient
            join notifications notification on notification.id = recipient.notification_id
            where notification.project_id = @project_id
              and notification.idempotency_key like 'kitting:panel:%:manufacturing:notification';
            """,
            projectId));
    }

    [Fact]
    public async Task ManufacturingExecution_EnforcesChecklistStopPendingResumeAndDirectOqcHandoffWhenLqcSuspended()
    {
        await using var context = await ProcurementApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var materialsClient = context.CreateClient("dev-materials");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        using var productionClient = context.CreateClient("dev-production");
        using var qualityClient = context.CreateClient("dev-quality");
        using var viewerClient = context.CreateClient("dev-viewer");
        var projectId = await CreateProjectAsync(salesClient, "MFG-011A", "Manufacturing Execution", 1);

        Assert.Equal(HttpStatusCode.OK, (await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { items = new[] { new { materialCategoryId = "67000000-0000-0000-0000-000000000005", orderItem = "Manufacturing Material" } } },
            TestContext.Current.CancellationToken)).StatusCode);
        await context.PreparePanelKittingAsync(projectId);
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            ) values
                ('{projectId}', 'ManufacturingPrimary', '50000000-0000-0000-0000-000000000004', '{SalesOwnerUserId}', now()),
                ('{projectId}', 'ManufacturingSecondary', '50000000-0000-0000-0000-000000000001', '{SalesOwnerUserId}', now()),
                ('{projectId}', 'QualityOQC', '50000000-0000-0000-0000-000000000005', '{SalesOwnerUserId}', now()),
                ('{projectId}', 'QualityOQCSecondary', '50000000-0000-0000-0000-000000000001', '{SalesOwnerUserId}', now());
            """);

        using (var lqcQueue = await ReadJsonAsync(await qualityClient.GetAsync(
                   $"/api/quality/inspections/queue?stage=LQC&projectId={projectId}",
                   TestContext.Current.CancellationToken)))
        {
            Assert.False(lqcQueue.RootElement.GetProperty("isOperational").GetBoolean());
            Assert.Contains("운영 중지", lqcQueue.RootElement.GetProperty("operationalMessage").GetString());
        }

        using var releaseQueue = await ReadJsonAsync(await productionClient.GetAsync(
            $"/api/manufacturing/release-candidates?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var panelId = Assert.Single(
            Assert.Single(releaseQueue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray()).GetProperty("panelId").GetGuid();
        Assert.False(Assert.Single(
            Assert.Single(releaseQueue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray()).GetProperty("kittingCompleted").GetBoolean());

        await context.ExecuteSqlAsync($"""
            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, status, priority,
                idempotency_key, created_by_user_id
            ) values (
                '{projectId}', 'Panel', '{panelId}', 'LQC', 'QualityLQC',
                '50000000-0000-0000-0000-000000000005', 'quality',
                '운영 중지 LQC mutation fence', 'Requested', 'Normal',
                'test:lqc-suspended:{panelId}', '{SalesOwnerUserId}'
            );
            """);
        var suspendedLqcStart = await qualityClient.PostAsJsonAsync(
            "/api/quality/inspections/start",
            new { operationId = Guid.NewGuid(), projectId, panelId, stageCode = "LQC" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, suspendedLqcStart.StatusCode);
        using var suspendedLqcProblem = await ReadJsonAsync(suspendedLqcStart);
        Assert.Contains("운영 중지", suspendedLqcProblem.RootElement.GetProperty("title").GetString());
        await context.ExecuteSqlAsync($"""
            update work_items
            set status='Cancelled', cancelled_at_utc=now()
            where idempotency_key='test:lqc-suspended:{panelId}';
            """);

        Assert.Equal(HttpStatusCode.Forbidden, (await qualityClient.PostAsJsonAsync(
            "/api/manufacturing/releases",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken)).StatusCode);

        var releaseOperationId = Guid.NewGuid();
        var released = await productionClient.PostAsJsonAsync(
            "/api/manufacturing/releases",
            new { operationId = releaseOperationId, projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        using (var releasedJson = await ReadJsonAsync(released))
        {
            Assert.Equal(1, releasedJson.RootElement.GetProperty("releasedPanelCount").GetInt32());
            Assert.Equal(1, releasedJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
            Assert.False(releasedJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        var releaseReplay = await productionClient.PostAsJsonAsync(
            "/api/manufacturing/releases",
            new { operationId = releaseOperationId, projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, releaseReplay.StatusCode);
        using (var releaseReplayJson = await ReadJsonAsync(releaseReplay))
        {
            Assert.True(releaseReplayJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        using (var workflow = await ReadJsonAsync(await productionClient.GetAsync(
                   $"/api/projects/{projectId}/workflow",
                   TestContext.Current.CancellationToken)))
        {
            var kittingStage = workflow.RootElement.GetProperty("stages").EnumerateArray()
                .Single(stage => stage.GetProperty("stageCode").GetString() == "KittingCompleted");
            Assert.Equal("Completed", kittingStage.GetProperty("status").GetString());
        }
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from project_workflow_events where project_id = @project_id and stage_code = 'KittingCompleted' and event_status = 'Succeeded';",
            projectId));

        using var readyQueue = await ReadJsonAsync(await manufacturingClient.GetAsync(
            $"/api/manufacturing/queue?projectId={projectId}",
            TestContext.Current.CancellationToken));
        var readyPanel = Assert.Single(
            Assert.Single(readyQueue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray());
        Assert.Equal("Ready", readyPanel.GetProperty("status").GetString());
        Assert.False(readyPanel.GetProperty("kittingCompleted").GetBoolean());
        Assert.True(readyPanel.GetProperty("canMutate").GetBoolean());
        var workItemId = readyPanel.GetProperty("workItemId").GetGuid();

        using var viewerQueue = await ReadJsonAsync(await viewerClient.GetAsync(
            $"/api/manufacturing/queue?projectId={projectId}",
            TestContext.Current.CancellationToken));
        Assert.False(Assert.Single(
            Assert.Single(viewerQueue.RootElement.GetProperty("projects").EnumerateArray())
                .GetProperty("panels").EnumerateArray()).GetProperty("canMutate").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await qualityClient.PostAsJsonAsync(
            "/api/manufacturing/executions/start",
            new { operationId = Guid.NewGuid(), projectId, panelId },
            TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manufacturingClient.PostAsync(
            $"/api/my-work/{workItemId}/start",
            null,
            TestContext.Current.CancellationToken)).StatusCode);

        var startOperationId = Guid.NewGuid();
        var started = await manufacturingClient.PostAsJsonAsync(
            "/api/manufacturing/executions/start",
            new { operationId = startOperationId, projectId, panelId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var startedJson = await ReadJsonAsync(started);
        var executionId = startedJson.RootElement.GetProperty("executionId").GetGuid();
        Assert.Equal(1, startedJson.RootElement.GetProperty("version").GetInt32());
        Assert.False(startedJson.RootElement.GetProperty("panelLqcWorkCreated").GetBoolean());
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'LQC' and responsibility_type = 'QualityLQC' and status in ('Requested', 'InProgress');",
            projectId));
        await context.ExecuteSqlAsync($"""
            with historical_work as (
                insert into work_items (
                    project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                    assigned_user_id, assigned_role_code, title, status, priority,
                    idempotency_key, created_by_user_id, started_at_utc, completed_at_utc
                ) values (
                    '{projectId}', 'Panel', '{panelId}', 'LQC', 'QualityLQC',
                    '50000000-0000-0000-0000-000000000005', 'quality',
                    '운영 중지 전 보존 LQC 합격', 'Completed', 'Normal',
                    'test:lqc:historical-pass:{panelId}', '{SalesOwnerUserId}', now(), now()
                ) returning id
            )
            insert into panel_quality_inspection_attempts (
                project_id, panel_id, stage_code, attempt_number, status, work_item_id,
                version, started_by_user_id, started_at_utc, completed_by_user_id, completed_at_utc
            )
            select '{projectId}', '{panelId}', 'LQC', 1, 'Passed', id, 1,
                   '50000000-0000-0000-0000-000000000005', now(),
                   '50000000-0000-0000-0000-000000000005', now()
            from historical_work;
            """);

        var kittingAfterStart = await materialsClient.PostAsJsonAsync(
            "/api/materials/kitting/complete",
            new { operationId = Guid.NewGuid(), projectId, panelIds = new[] { panelId } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kittingAfterStart.StatusCode);
        using (var kittingAfterStartJson = await ReadJsonAsync(kittingAfterStart))
        {
            Assert.Equal(0, kittingAfterStartJson.RootElement.GetProperty("generatedWorkItemCount").GetInt32());
        }
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'ManufacturingWork';",
            projectId));

        var replay = await manufacturingClient.PostAsJsonAsync(
            "/api/manufacturing/executions/start",
            new { operationId = startOperationId, projectId, panelId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using (var replayJson = await ReadJsonAsync(replay))
        {
            Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
        }

        using var startedDetail = await ReadJsonAsync(await manufacturingClient.GetAsync(
            $"/api/manufacturing/panels/{panelId}",
            TestContext.Current.CancellationToken));
        var steps = startedDetail.RootElement.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(
            ["작업지시·도면 확인", "자재·부품 확인", "제조 작업 수행", "자체 확인"],
            steps.Select(step => step.GetProperty("stepName").GetString()!).ToArray());

        Assert.Equal(HttpStatusCode.Conflict, (await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/check-step",
            new { operationId = Guid.NewGuid(), stepId = steps[1].GetProperty("stepId").GetGuid(), expectedVersion = 1 },
            TestContext.Current.CancellationToken)).StatusCode);

        var version = 1;
        foreach (var step in steps)
        {
            var checkedResponse = await manufacturingClient.PostAsJsonAsync(
                $"/api/manufacturing/executions/{executionId}/check-step",
                new { operationId = Guid.NewGuid(), stepId = step.GetProperty("stepId").GetGuid(), expectedVersion = version },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, checkedResponse.StatusCode);
            using var checkedJson = await ReadJsonAsync(checkedResponse);
            version = checkedJson.RootElement.GetProperty("version").GetInt32();
        }
        Assert.Equal(5, version);

        Assert.Equal(HttpStatusCode.BadRequest, (await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/stop",
            new
            {
                operationId = Guid.NewGuid(),
                reasonCode = "Material",
                description = "필수 부서가 없는 제조 중단 요청입니다.",
                actionDepartmentCode = "",
                assigneeUserId = (Guid?)null,
                expectedVersion = version
            },
            TestContext.Current.CancellationToken)).StatusCode);

        using var actionDepartments = await ReadJsonAsync(await manufacturingClient.GetAsync(
            "/api/manufacturing/action-departments",
            TestContext.Current.CancellationToken));
        var actionDepartment = actionDepartments.RootElement.EnumerateArray()
            .First(department => department.GetProperty("assignees").GetArrayLength() > 0);
        var actionDepartmentCode = actionDepartment.GetProperty("departmentCode").GetString()!;
        var assigneeUserId = actionDepartment.GetProperty("assignees").EnumerateArray().First()
            .GetProperty("userId").GetGuid();

        var stopped = await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/stop",
            new
            {
                operationId = Guid.NewGuid(),
                reasonCode = "Material",
                description = "제조 자재 규격을 다시 확인해야 작업을 계속할 수 있습니다.",
                actionDepartmentCode,
                assigneeUserId,
                expectedVersion = version
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        using var stoppedJson = await ReadJsonAsync(stopped);
        var pendingId = stoppedJson.RootElement.GetProperty("pendingId").GetGuid();
        version = stoppedJson.RootElement.GetProperty("version").GetInt32();
        Assert.Equal("Blocked", stoppedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from pending_issues where project_id = @project_id and target_type = 'Panel' and issue_type = 'ManufacturingStop' and priority = 'Urgent';",
            projectId));

        Assert.Equal(HttpStatusCode.Conflict, (await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/resume",
            new { operationId = Guid.NewGuid(), expectedVersion = version },
            TestContext.Current.CancellationToken)).StatusCode);

        var pendingVersion = 1;
        foreach (var nextStatus in new[] { "InProgress", "ReinspectionRequested", "Closed" })
        {
            var transition = await productionClient.PostAsJsonAsync(
                $"/api/pending/{pendingId}/transition",
                new { toStatus = nextStatus, expectedVersion = pendingVersion, reason = $"제조 중단 조치 {nextStatus} 확인" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
            using var transitionJson = await ReadJsonAsync(transition);
            pendingVersion = transitionJson.RootElement.GetProperty("issue").GetProperty("version").GetInt32();
        }

        var resumed = await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/resume",
            new { operationId = Guid.NewGuid(), expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        using (var resumedJson = await ReadJsonAsync(resumed))
        {
            version = resumedJson.RootElement.GetProperty("version").GetInt32();
            Assert.Equal("InProgress", resumedJson.RootElement.GetProperty("status").GetString());
        }

        var completed = await manufacturingClient.PostAsJsonAsync(
            $"/api/manufacturing/executions/{executionId}/complete",
            new { operationId = Guid.NewGuid(), expectedVersion = version },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using (var completedJson = await ReadJsonAsync(completed))
        {
            Assert.False(completedJson.RootElement.GetProperty("panelLqcWorkCreated").GetBoolean());
            Assert.True(completedJson.RootElement.GetProperty("projectManufacturingCompleted").GetBoolean());
        }
        Assert.Equal(0L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'LQC' and responsibility_type = 'QualityLQC' and status in ('Requested', 'InProgress');",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from work_items where project_id = @project_id and workflow_stage_code = 'OQC' and responsibility_type = 'QualityOQC';",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from panel_manufacturing_completion_confirmations where project_id = @project_id and handoff_basis='ManufacturingOnly' and lqc_attempt_id is null;",
            projectId));
        Assert.Equal(1L, await context.ReadCountAsync(
            "select count(*) from project_workflow_events where project_id = @project_id and stage_code = 'ManufacturingWork' and event_type = 'StageCompleted' and event_status = 'Succeeded';",
            projectId));
    }

    private static async Task<Guid> CreateProjectAsync(
        HttpClient client,
        string projectCode,
        string projectTitle,
        int panelCount = 1)
    {
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                CustomerName = "Procurement Test Customer",
                Item = "UL67",
                ProjectCode = projectCode,
                ProjectTitle = projectTitle,
                PanelCount = panelCount,
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
        var headers = new[] { "PJT", "PJT CODE", "통상납기", "발주품목", "구분", "업체", "기술 담당자", "발주일", "입고예정일", "이슈사항", "입고 완료" };
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
        if (row.Length >= 11)
        {
            return row;
        }

        var normalized = Enumerable.Repeat(string.Empty, 11).ToArray();
        for (var index = 0; index < row.Length && index < 10; index++)
        {
            normalized[index <= 3 ? index : index + 1] = row[index];
        }
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            normalized[4] = "기타";
        }
        return normalized;
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

        public IReadOnlyList<TestLogEntry> Logs => Factory.Logs.Entries;
        public string RepositoryRoot => Database.RepositoryRoot;

        public async Task MarkAttemptLegacyAsync(Guid attemptId)
        {
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var command = dataSource.CreateCommand("update material_iqc_attempts set decision_mode = 'Legacy' where id = @id;");
            command.Parameters.AddWithValue("id", attemptId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        public async Task<bool> IsIqcReportHiddenFromScopeAsync(Guid attemptId, IReadOnlyList<string> projectKeys)
        {
            using var scope = Factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IqcReportStore>();
            return await store.GetAsync(
                attemptId,
                new ProjectAccessScope(false, projectKeys),
                TestContext.Current.CancellationToken) is null;
        }

        public async Task<bool> IsIqcPdfHiddenFromScopeAsync(Guid reportId, IReadOnlyList<string> projectKeys)
        {
            using var scope = Factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IqcReportStore>();
            var result = await store.GetPdfAsync(
                reportId,
                new ProjectAccessScope(false, projectKeys),
                TestContext.Current.CancellationToken);
            return result.Status == MaterialsMutationStatus.NotFound;
        }

        public async Task PreparePanelKittingAsync(Guid projectId)
        {
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "select set_config('emi_qms.material_receipt_write', 'allowed', true);";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update project_procurement_items
                    set receipt_completed = true,
                        receipt_completed_at_utc = now(),
                        receipt_completed_by_user_id = '50000000-0000-0000-0000-000000000012'
                    where project_id = @project_id and status = 'Active';

                    update panel_placeholders
                    set panel_info_completed = true,
                        panel_name = coalesce(panel_name, display_code),
                        width_mm = coalesce(width_mm, 600),
                        height_mm = coalesce(height_mm, 1800),
                        depth_mm = coalesce(depth_mm, 400)
                    where project_id = @project_id and status = 'Active';
                    """;
                command.Parameters.AddWithValue("project_id", projectId);
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        public async Task<long> ReadCountAsync(string sql, Guid projectId)
        {
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("project_id", projectId);
            return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
        }

        public async Task<string> ReadTextAsync(string sql, Guid projectId)
        {
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("project_id", projectId);
            return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture) ?? "";
        }

        public async Task ExecuteSqlAsync(string sql)
        {
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var command = dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<Guid> CreateLegacyProjectAsync(string projectCode, string projectTitle)
        {
            var projectId = Guid.NewGuid();
            var provider = new DatabaseConnectionStringProvider(Database.CreateConfiguration());
            var connectionString = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            await using var dataSource = NpgsqlDataSource.Create(connectionString!);
            await using var command = dataSource.CreateCommand("""
                insert into projects (
                    id, project_key, project_number, name, customer_name, item,
                    project_code, project_title, project_title_normalized,
                    packaging_method, delivery_date, sales_owner_user_id,
                    iqc_routing_policy, status
                )
                values (
                    @id, @project_key, @project_code, @project_title,
                    'Procurement Test Customer', 'UL67',
                    @project_code, @project_title, upper(@project_title),
                    'StretchWrap', '2026-10-10',
                    '50000000-0000-0000-0000-000000000002',
                    'AllReceipts', 'Active'
                );
                """);
            command.Parameters.AddWithValue("id", projectId);
            command.Parameters.AddWithValue("project_key", projectId.ToString("N"));
            command.Parameters.AddWithValue("project_code", projectCode);
            command.Parameters.AddWithValue("project_title", projectTitle);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            return projectId;
        }

        public async ValueTask DisposeAsync()
        {
            Factory.Dispose();
            await Database.DisposeAsync();
        }
    }

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string databaseName, IConfiguration baseConfiguration, string repositoryRoot)
        {
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
            RepositoryRoot = repositoryRoot;
        }

        private string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }
        public string RepositoryRoot { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");

            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new PostgreSqlTestDatabase(databaseName, baseConfiguration, repositoryRoot);
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
