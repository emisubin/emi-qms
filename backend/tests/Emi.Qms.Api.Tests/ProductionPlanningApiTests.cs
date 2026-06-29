using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.ProductionPlanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProductionPlanningApiTests
{
    [Fact]
    public async Task ProductionPlanning_UpdateAssigneesStatusAndHistory_AreRoleScoped()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var viewerClient = context.CreateClient("dev-viewer");

        var projectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-TEST", "Plan Test");

        Assert.Equal(HttpStatusCode.OK, (await salesClient.GetAsync($"/api/projects/{projectId}/production-planning", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await procurementClient.PatchAsJsonAsync($"/api/projects/{projectId}/production-planning", new { }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PatchAsJsonAsync($"/api/projects/{projectId}/production-planning", new { }, TestContext.Current.CancellationToken)).StatusCode);

        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var steps = productType.GetProperty("steps").EnumerateArray().ToList();

        var partial = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                notes = "초기 계획",
                reason = (string?)null,
                items = new object[]
                {
                    new { itemId = (Guid?)null, templateStepId = steps[0].GetProperty("templateStepId").GetGuid(), sequenceNumber = 1, expectedRowVersion = 0, plannedDate = "2026-07-01", note = "입고 확인" },
                    new { itemId = (Guid?)null, templateStepId = steps[1].GetProperty("templateStepId").GetGuid(), sequenceNumber = 2, expectedRowVersion = 0, plannedDate = (string?)null, note = (string?)null },
                    new { itemId = (Guid?)null, templateStepId = steps[2].GetProperty("templateStepId").GetGuid(), sequenceNumber = 3, expectedRowVersion = 0, plannedDate = (string?)null, note = (string?)null }
                },
                assignees = new object[]
                {
                    new { responsibilityType = "Procurement", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000011"), note = "구매" },
                    new { responsibilityType = "ProductionPlanning", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000003"), note = "생산관리" },
                    new { responsibilityType = "Manufacturing", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = (Guid?)null, note = (string?)null }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        using var partialJson = await ReadJsonAsync(partial);
        Assert.Equal("Planning", partialJson.RootElement.GetProperty("planStatus").GetString());
        Assert.Equal("영업담당자", partialJson.RootElement.GetProperty("fallbacks").EnumerateArray().First(item => item.GetProperty("responsibilityType").GetString() == "Manufacturing").GetProperty("sourceLabel").GetString());

        var currentItems = partialJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        var currentAssignees = partialJson.RootElement.GetProperty("assignees").EnumerateArray().ToList();
        var missingReason = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = partialJson.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = "담당자 변경",
                reason = "",
                items = currentItems.Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = item.GetProperty("plannedDate").ValueKind == JsonValueKind.Null ? null : item.GetProperty("plannedDate").GetString(),
                    note = item.GetProperty("note").ValueKind == JsonValueKind.Null ? null : item.GetProperty("note").GetString()
                }).ToArray(),
                assignees = new[]
                {
                    new
                    {
                        responsibilityType = "Procurement",
                        assigneeId = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "Procurement").GetProperty("assigneeId").GetGuid(),
                        expectedRowVersion = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "Procurement").GetProperty("rowVersion").GetInt32(),
                        assignedUserId = (Guid?)null,
                        note = (string?)null
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var completed = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = partialJson.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = "계획 완료",
                reason = "필수 일정 확정",
                items = currentItems.Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = $"2026-07-0{item.GetProperty("sequenceNumber").GetInt32()}",
                    note = "확정"
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var completedJson = await ReadJsonAsync(completed);
        Assert.Equal("Planned", completedJson.RootElement.GetProperty("planStatus").GetString());

        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"/api/projects/{projectId}/production-planning/history", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync($"/api/projects/{projectId}/production-planning/history", TestContext.Current.CancellationToken)).StatusCode);

        await context.ExecuteSqlAsync($"update projects set status = 'Completed' where id = '{projectId}';");
        var blocked = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new { productTypeId, expectedRowVersion = completedJson.RootElement.GetProperty("rowVersion").GetInt32(), reason = "완료 차단", items = Array.Empty<object>(), assignees = Array.Empty<object>() },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task ProductionPlanningDashboard_ExcludesCompletedAndSummarizesPlans()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var activeId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-ACTIVE", "Plan Active");
        var completedId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-COMPLETE", "Plan Complete");
        await context.ExecuteSqlAsync($"update projects set status = 'Completed' where id = '{completedId}';");

        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var steps = productType.GetProperty("steps").EnumerateArray().ToList();
        var patch = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{activeId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                reason = (string?)null,
                items = steps.Select(step => new
                {
                    itemId = (Guid?)null,
                    templateStepId = step.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = step.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = 0,
                    plannedDate = "2026-07-01",
                    note = (string?)null
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        using var summary = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/summary", TestContext.Current.CancellationToken));
        Assert.True(summary.RootElement.GetProperty("plannedCount").GetInt32() >= 1);
        using var projects = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/projects?search=Plan", TestContext.Current.CancellationToken));
        var titles = projects.RootElement.GetProperty("projects").EnumerateArray().Select(item => item.GetProperty("projectTitle").GetString()).ToList();
        Assert.Contains("Plan Active", titles);
        Assert.Contains("Plan Complete", titles);
    }

    [Fact]
    public async Task ProductionPlanning_CustomItems_CanBeAddedUpdatedAndDeleted()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var projectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-CUSTOM", "Plan Custom");
        var (productTypeId, steps) = await ReadProductTypeAsync(productionClient, "UL67");

        var create = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                notes = (string?)null,
                reason = (string?)null,
                items = new object[]
                {
                    new { itemId = (Guid?)null, templateStepId = steps[0], stepName = (string?)null, sequenceNumber = 1, expectedRowVersion = 0, plannedDate = "2026-07-01", note = (string?)null, isDeleted = false },
                    new { itemId = (Guid?)null, templateStepId = (Guid?)null, stepName = "사용자 추가 항목", sequenceNumber = 5, expectedRowVersion = 0, plannedDate = "2026-07-09", note = "custom", isDeleted = false }
                },
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var createdJson = await ReadJsonAsync(create);
        var custom = createdJson.RootElement.GetProperty("items").EnumerateArray().First(item => item.GetProperty("stepName").GetString() == "사용자 추가 항목");
        Assert.True(custom.GetProperty("isCustom").GetBoolean());

        var delete = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = createdJson.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = (string?)null,
                reason = "custom 삭제",
                items = createdJson.RootElement.GetProperty("items").EnumerateArray().Select(item => new
                {
                    itemId = item.GetProperty("itemId").ValueKind == JsonValueKind.Null ? (Guid?)null : item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").ValueKind == JsonValueKind.Null ? (Guid?)null : item.GetProperty("templateStepId").GetGuid(),
                    stepName = item.GetProperty("stepName").GetString(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = item.GetProperty("plannedDate").ValueKind == JsonValueKind.Null ? null : item.GetProperty("plannedDate").GetString(),
                    note = item.GetProperty("note").ValueKind == JsonValueKind.Null ? null : item.GetProperty("note").GetString(),
                    isDeleted = item.GetProperty("stepName").GetString() == "사용자 추가 항목"
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        using var deletedJson = await ReadJsonAsync(delete);
        Assert.DoesNotContain(deletedJson.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("stepName").GetString() == "사용자 추가 항목");
    }

    [Fact]
    public async Task ProductionPlanningExcel_PreviewsAndAppliesMultipleProjectsWithCustomSteps()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");

        var firstProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-XLS-1", "Plan Excel One");
        var secondProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-XLS-2", "Plan Excel Two");

        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/production-planning/import/template", TestContext.Current.CancellationToken)).StatusCode);
        var templateResponse = await productionClient.GetAsync("/api/production-planning/import/template", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        await AssertTemplateWidthsAsync(templateResponse, wideColumn: 4);

        var file = CreateProductionPlanningExcel([
            ["Plan Excel One", "PLAN-XLS-1", "UL67", "자재 입고", "2026-07-01", "template", "dev-procurement", "dev-production", "dev-manufacturing", "dev-quality", "dev-logistics"],
            ["Plan Excel Two", "PLAN-XLS-2", "UL67", "사용자 추가 항목", "2026-07-09", "custom", "", "", "", "", ""],
            ["Unknown", "NO-SUCH", "UL67", "자재 입고", "2026-07-01", "", "", "", "", "", ""]
        ]);

        using var preview = await PreviewProductionPlanningExcelAsync(productionClient, file, "planning.xlsx");
        var root = preview.RootElement;
        Assert.Equal(2, root.GetProperty("saveableCount").GetInt32());
        Assert.Equal(1, root.GetProperty("blockedCount").GetInt32());
        Assert.Contains(root.GetProperty("rows").EnumerateArray(), row => row.GetProperty("resultType").GetString() == "CustomStep");
        Assert.Contains(root.GetProperty("rows").EnumerateArray(), row => row.GetProperty("errorMessages").EnumerateArray().Any(message => message.GetString() == "등록되지 않은 프로젝트입니다."));

        using var apply = await ApplyProductionPlanningExcelAsync(productionClient, file, "planning.xlsx", root.GetProperty("fileSha256").GetString()!);
        Assert.Equal(2, apply.RootElement.GetProperty("appliedRowCount").GetInt32());
        Assert.Contains(apply.RootElement.GetProperty("projectIds").EnumerateArray(), item => item.GetGuid() == firstProjectId);
        Assert.Contains(apply.RootElement.GetProperty("projectIds").EnumerateArray(), item => item.GetGuid() == secondProjectId);

        using var secondPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{secondProjectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.Contains(secondPlan.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("stepName").GetString() == "사용자 추가 항목" && item.GetProperty("isCustom").GetBoolean());
    }

    [Fact]
    public async Task ProductionTemplateSettings_AffectNewTemplatesOnlyAndValidateRows()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");

        var existingProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-SET-OLD", "Plan Settings Old");
        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var originalSteps = productType.GetProperty("steps").EnumerateArray().ToList();

        var createExisting = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{existingProjectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                reason = (string?)null,
                items = originalSteps.Select(step => new
                {
                    itemId = (Guid?)null,
                    templateStepId = step.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = step.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = 0,
                    plannedDate = $"2026-07-0{step.GetProperty("sequenceNumber").GetInt32()}",
                    note = (string?)null
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, createExisting.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PatchAsJsonAsync($"/api/production-planning/settings/templates/{productTypeId}", new { steps = Array.Empty<object>() }, TestContext.Current.CancellationToken)).StatusCode);

        var duplicate = await productionClient.PatchAsJsonAsync(
            $"/api/production-planning/settings/templates/{productTypeId}",
            new
            {
                steps = new object[]
                {
                    new { templateStepId = (Guid?)null, sequenceNumber = 1, stepName = "중복", isRequired = true, isActive = true },
                    new { templateStepId = (Guid?)null, sequenceNumber = 1, stepName = "중복", isRequired = true, isActive = true }
                },
                reason = "검증"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var update = await productionClient.PatchAsJsonAsync(
            $"/api/production-planning/settings/templates/{productTypeId}",
            new
            {
                steps = originalSteps.Select(step => (object)new
                {
                    templateStepId = (Guid?)step.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = step.GetProperty("sequenceNumber").GetInt32(),
                    stepName = step.GetProperty("stepName").GetString(),
                    isRequired = step.GetProperty("isRequired").GetBoolean(),
                    isActive = true
                }).Concat(new object[]
                {
                    new { templateStepId = (Guid?)null, sequenceNumber = 99, stepName = "최종 확인", isRequired = false, isActive = true }
                }).ToArray(),
                reason = "새 단계 추가"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updatedSettings = await ReadJsonAsync(update);
        var updatedUl67 = updatedSettings.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        Assert.Contains(updatedUl67.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepName").GetString() == "최종 확인");

        using var existingPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{existingProjectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.DoesNotContain(existingPlan.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("stepName").GetString() == "최종 확인");

        using var refreshedProductTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var refreshedUl67 = refreshedProductTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        Assert.Contains(refreshedUl67.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepName").GetString() == "최종 확인");
    }

    [Fact]
    public async Task ExistingProductionPlan_KeepsTemplateSnapshotAfterTemplateSettingsChange()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var projectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-SNAPSHOT", "Plan Snapshot");
        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var originalTemplateId = productType.GetProperty("activeTemplateId").GetGuid();
        var originalSteps = ReadSteps(productType).ToList();

        var create = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                reason = (string?)null,
                items = originalSteps.Select(step => new
                {
                    itemId = (Guid?)null,
                    templateStepId = step.TemplateStepId,
                    sequenceNumber = step.SequenceNumber,
                    expectedRowVersion = 0,
                    plannedDate = $"2026-07-{step.SequenceNumber:D2}",
                    note = step.StepName
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var created = await ReadJsonAsync(create);
        Assert.Equal(originalTemplateId, created.RootElement.GetProperty("templateId").GetGuid());

        var updateTemplate = await productionClient.PatchAsJsonAsync(
            $"/api/production-planning/settings/templates/{productTypeId}",
            new
            {
                steps = originalSteps.Select(step => (object)new
                {
                    templateStepId = (Guid?)step.TemplateStepId,
                    sequenceNumber = step.SequenceNumber,
                    stepName = step.StepName,
                    isRequired = true,
                    isActive = true
                }).Concat(new object[]
                {
                    new { templateStepId = (Guid?)null, sequenceNumber = 99, stepName = "최신 template 전용", isRequired = false, isActive = true }
                }).ToArray(),
                reason = "template 변경"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateTemplate.StatusCode);

        var existingItems = created.RootElement.GetProperty("items").EnumerateArray().ToList();
        var saveExisting = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = created.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = "기존 snapshot 수정",
                reason = "기존 snapshot 유지",
                items = existingItems.Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = item.GetProperty("sequenceNumber").GetInt32() == 1 ? "2026-08-01" : item.GetProperty("plannedDate").GetString(),
                    note = item.GetProperty("sequenceNumber").GetInt32() == 1 ? "과거 template step 수정" : item.GetProperty("note").GetString()
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saveExisting.StatusCode);
        using var saved = await ReadJsonAsync(saveExisting);

        Assert.Equal(originalTemplateId, saved.RootElement.GetProperty("templateId").GetGuid());
        Assert.DoesNotContain(saved.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("stepName").GetString() == "최신 template 전용");
        var firstItem = saved.RootElement.GetProperty("items").EnumerateArray().First(item => item.GetProperty("sequenceNumber").GetInt32() == 1);
        Assert.Equal(originalSteps[0].TemplateStepId, firstItem.GetProperty("templateStepId").GetGuid());
        Assert.Equal("2026-08-01", firstItem.GetProperty("plannedDate").GetString());
        Assert.Equal("과거 template step 수정", firstItem.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ProductionPlanningExcel_BlocksItemMismatchAndKeepsExistingSnapshot()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var mismatchProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-XLS-MISMATCH", "Plan Excel Mismatch");
        var mismatchFile = CreateProductionPlanningExcel([
            ["Plan Excel Mismatch", "PLAN-XLS-MISMATCH", "RRP", "자재 입고", "2026-07-01", "", "", "", "", "", ""]
        ]);
        using var mismatchPreview = await PreviewProductionPlanningExcelAsync(productionClient, mismatchFile, "mismatch.xlsx");
        Assert.Equal(0, mismatchPreview.RootElement.GetProperty("saveableCount").GetInt32());
        Assert.Equal(1, mismatchPreview.RootElement.GetProperty("blockedCount").GetInt32());
        Assert.Contains(
            mismatchPreview.RootElement.GetProperty("rows").EnumerateArray().Single().GetProperty("errorMessages").EnumerateArray(),
            message => message.GetString()!.Contains("Excel의 Item이 프로젝트 Item과 일치하지 않습니다.", StringComparison.Ordinal));

        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(mismatchFile), "file", "mismatch.xlsx");
            form.Add(new StringContent(mismatchPreview.RootElement.GetProperty("fileSha256").GetString()!), "expectedFileSha256");
            var applyMismatch = await productionClient.PostAsync("/api/production-planning/import/apply", form, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, applyMismatch.StatusCode);
        }
        using var mismatchPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{mismatchProjectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null, mismatchPlan.RootElement.GetProperty("planId").ValueKind);

        var existingProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-XLS-SNAPSHOT", "Plan Excel Snapshot");
        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var originalTemplateId = productType.GetProperty("activeTemplateId").GetGuid();
        var originalSteps = ReadSteps(productType).ToList();

        using var createdPlan = await ReadJsonAsync(await productionClient.PatchAsJsonAsync(
            $"/api/projects/{existingProjectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                items = originalSteps.Select(step => new
                {
                    itemId = (Guid?)null,
                    templateStepId = step.TemplateStepId,
                    sequenceNumber = step.SequenceNumber,
                    expectedRowVersion = 0,
                    plannedDate = $"2026-07-{step.SequenceNumber:D2}",
                    note = (string?)null
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken));
        Assert.Equal(originalTemplateId, createdPlan.RootElement.GetProperty("templateId").GetGuid());

        var updateTemplate = await productionClient.PatchAsJsonAsync(
            $"/api/production-planning/settings/templates/{productTypeId}",
            new
            {
                steps = originalSteps.Select(step => (object)new
                {
                    templateStepId = (Guid?)step.TemplateStepId,
                    sequenceNumber = step.SequenceNumber,
                    stepName = step.StepName,
                    isRequired = true,
                    isActive = true
                }).Concat(new object[]
                {
                    new { templateStepId = (Guid?)null, sequenceNumber = 99, stepName = "Excel 신규 template step", isRequired = false, isActive = true }
                }).ToArray(),
                reason = "Excel snapshot 검증"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateTemplate.StatusCode);
        using var updatedSettings = await ReadJsonAsync(updateTemplate);
        var updatedUl67 = updatedSettings.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var latestTemplateId = updatedUl67.GetProperty("activeTemplateId").GetGuid();
        Assert.NotEqual(originalTemplateId, latestTemplateId);

        var existingFile = CreateProductionPlanningExcel([
            ["Plan Excel Snapshot", "PLAN-XLS-SNAPSHOT", "UL67", originalSteps[0].StepName, "2026-08-10", "Excel 수정", "", "", "", "", ""]
        ]);
        using var existingPreview = await PreviewProductionPlanningExcelAsync(productionClient, existingFile, "existing.xlsx");
        Assert.Equal(1, existingPreview.RootElement.GetProperty("saveableCount").GetInt32());
        using var existingApply = await ApplyProductionPlanningExcelAsync(productionClient, existingFile, "existing.xlsx", existingPreview.RootElement.GetProperty("fileSha256").GetString()!);
        Assert.Equal(1, existingApply.RootElement.GetProperty("appliedRowCount").GetInt32());

        using var existingPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{existingProjectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.Equal(originalTemplateId, existingPlan.RootElement.GetProperty("templateId").GetGuid());
        Assert.DoesNotContain(existingPlan.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("stepName").GetString() == "Excel 신규 template step");
        var updatedOldItem = existingPlan.RootElement.GetProperty("items").EnumerateArray().First(item => item.GetProperty("stepName").GetString() == originalSteps[0].StepName);
        Assert.Equal(originalSteps[0].TemplateStepId, updatedOldItem.GetProperty("templateStepId").GetGuid());
        Assert.Equal("2026-08-10", updatedOldItem.GetProperty("plannedDate").GetString());

        var newProjectId = await CreateProjectAndReadIdAsync(salesClient, "PLAN-XLS-NEW", "Plan Excel New");
        var newFile = CreateProductionPlanningExcel([
            ["Plan Excel New", "PLAN-XLS-NEW", "UL67", "Excel 신규 template step", "2026-09-01", "", "", "", "", "", ""]
        ]);
        using var newPreview = await PreviewProductionPlanningExcelAsync(productionClient, newFile, "new.xlsx");
        Assert.Equal(1, newPreview.RootElement.GetProperty("saveableCount").GetInt32());
        using var newApply = await ApplyProductionPlanningExcelAsync(productionClient, newFile, "new.xlsx", newPreview.RootElement.GetProperty("fileSha256").GetString()!);
        Assert.Equal(1, newApply.RootElement.GetProperty("appliedRowCount").GetInt32());

        using var newPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{newProjectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.Equal(latestTemplateId, newPlan.RootElement.GetProperty("templateId").GetGuid());
        var newTemplateItem = newPlan.RootElement.GetProperty("items").EnumerateArray().First(item => item.GetProperty("stepName").GetString() == "Excel 신규 template step");
        Assert.False(newTemplateItem.GetProperty("isCustom").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, newTemplateItem.GetProperty("templateStepId").ValueKind);
    }

    [Fact]
    public async Task SystemHolidays_ReadFromDatabaseAndSyncNoOpsWithoutServiceKey()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var adminClient = context.CreateClient("dev-admin");

        await context.ExecuteSqlAsync("""
            insert into system_holidays (holiday_date, name, country_code, source, source_key, is_active)
            values
                ('2026-07-06', '공식 대체공휴일', 'KR', 'Test', 'test-20260706', true),
                ('2026-07-07', '비활성 공휴일', 'KR', 'Test', 'test-20260707', false);
            """);

        using var holidays = await ReadJsonAsync(await salesClient.GetAsync("/api/system/holidays?countryCode=KR&dateFrom=2026-07-01&dateTo=2026-07-31", TestContext.Current.CancellationToken));
        var holidayItems = holidays.RootElement.EnumerateArray().ToList();
        Assert.Contains(holidayItems, item => item.GetProperty("holidayDate").GetString() == "2026-07-06" && item.GetProperty("name").GetString() == "공식 대체공휴일");
        Assert.DoesNotContain(holidayItems, item => item.GetProperty("name").GetString() == "비활성 공휴일");
        Assert.DoesNotContain(holidayItems, item => item.GetProperty("name").GetString() == "검수공휴일");

        Assert.Equal(HttpStatusCode.Forbidden, (await salesClient.PostAsJsonAsync("/api/system/holidays/sync/kr", new { year = 2026 }, TestContext.Current.CancellationToken)).StatusCode);

        using var sync = await ReadJsonAsync(await adminClient.PostAsJsonAsync("/api/system/holidays/sync/kr", new { year = 2026 }, TestContext.Current.CancellationToken));
        Assert.False(sync.RootElement.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(0, sync.RootElement.GetProperty("upsertedCount").GetInt32());
        Assert.Contains("인증키", sync.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        using var productionSync = await ReadJsonAsync(await adminClient.PostAsync("/api/production-planning/holidays/sync?year=2026", null, TestContext.Current.CancellationToken));
        Assert.False(productionSync.RootElement.GetProperty("isConfigured").GetBoolean());
        Assert.Contains("인증키", productionSync.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SystemHolidays_SyncsOfficialProviderRowsIntoDatabase()
    {
        var fakeProvider = new FakeKoreanHolidayProvider([
            new SystemHolidayUpsert(new DateOnly(2026, 7, 17), "제헌절", "KR", "OfficialApi:NationalHoliday", "OfficialApi:NationalHoliday:20260717:제헌절"),
            new SystemHolidayUpsert(new DateOnly(2026, 12, 25), "기독탄신일", "KR", "OfficialApi:PublicHoliday", "OfficialApi:PublicHoliday:20261225:기독탄신일")
        ]);
        await using var context = await ProductionPlanningApiTestContext.CreateAsync(services =>
        {
            services.AddSingleton<IKoreanHolidayProvider>(fakeProvider);
        });
        using var adminClient = context.CreateClient("dev-admin");

        using var sync = await ReadJsonAsync(await adminClient.PostAsync("/api/production-planning/holidays/sync?year=2026", null, TestContext.Current.CancellationToken));
        Assert.True(sync.RootElement.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(2, sync.RootElement.GetProperty("upsertedCount").GetInt32());

        using var holidays = await ReadJsonAsync(await adminClient.GetAsync("/api/system/holidays?countryCode=KR&dateFrom=2026-07-01&dateTo=2026-12-31", TestContext.Current.CancellationToken));
        var holidayItems = holidays.RootElement.EnumerateArray().ToList();
        Assert.Contains(holidayItems, item =>
            item.GetProperty("holidayDate").GetString() == "2026-07-17"
            && item.GetProperty("name").GetString() == "제헌절"
            && item.GetProperty("source").GetString() == "OfficialApi:NationalHoliday");
        Assert.Contains(holidayItems, item =>
            item.GetProperty("holidayDate").GetString() == "2026-12-25"
            && item.GetProperty("name").GetString() == "기독탄신일"
            && item.GetProperty("source").GetString() == "OfficialApi:PublicHoliday");

        using var duplicateSync = await ReadJsonAsync(await adminClient.PostAsync("/api/production-planning/holidays/sync?year=2026", null, TestContext.Current.CancellationToken));
        Assert.True(duplicateSync.RootElement.GetProperty("isConfigured").GetBoolean());

        using var duplicatedHolidays = await ReadJsonAsync(await adminClient.GetAsync("/api/system/holidays?countryCode=KR&dateFrom=2026-07-01&dateTo=2026-12-31", TestContext.Current.CancellationToken));
        Assert.Single(duplicatedHolidays.RootElement.EnumerateArray(), item => item.GetProperty("holidayDate").GetString() == "2026-07-17");
        Assert.Single(duplicatedHolidays.RootElement.EnumerateArray(), item => item.GetProperty("holidayDate").GetString() == "2026-12-25");
    }

    [Fact]
    public async Task OfficialKoreanHolidayProvider_SyncsPublicHolidaysAndNationalHolidays()
    {
        var publicHolidayXml = """
            <response>
              <body>
                <items>
                  <item><dateName>공식 대체공휴일</dateName><locdate>20260706</locdate><isHoliday>Y</isHoliday></item>
                  <item><dateName>국군의 날</dateName><locdate>20261001</locdate><isHoliday>N</isHoliday></item>
                </items>
              </body>
            </response>
            """;
        var nationalHolidayXml = """
            <response>
              <body>
                <items>
                  <item><dateName>제헌절</dateName><locdate>20260717</locdate><isHoliday>N</isHoliday></item>
                </items>
              </body>
            </response>
            """;
        using var httpClient = new HttpClient(new FakeHolidayHttpHandler(publicHolidayXml, nationalHolidayXml));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KOREA_HOLIDAY_API_SERVICE_KEY"] = "test-key",
                ["KoreaHolidayApi:PublicHolidayEndpoint"] = "https://example.test/getRestDeInfo",
                ["KoreaHolidayApi:NationalHolidayEndpoint"] = "https://example.test/getHoliDeInfo"
            })
            .Build();
        var provider = new OfficialKoreanHolidayProvider(httpClient, configuration);

        var result = await provider.FetchAsync(2026, TestContext.Current.CancellationToken);

        Assert.True(result.IsConfigured);
        Assert.Equal(2, result.Holidays.Count);
        Assert.Contains(result.Holidays, holiday =>
            holiday.HolidayDate == new DateOnly(2026, 7, 6)
            && holiday.Name == "공식 대체공휴일"
            && holiday.Source == "OfficialApi:PublicHoliday");
        Assert.Contains(result.Holidays, holiday =>
            holiday.HolidayDate == new DateOnly(2026, 7, 17)
            && holiday.Name == "제헌절"
            && holiday.Source == "OfficialApi:NationalHoliday");
        Assert.DoesNotContain(result.Holidays, holiday => holiday.Name == "국군의 날");
    }

    private static async Task<Guid> CreateProjectAndReadIdAsync(HttpClient client, string code, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                CustomerName = "Production Planning Test Customer",
                Item = "UL67",
                ProjectCode = code,
                ProjectTitle = title,
                PanelCount = 2,
                DeliveryDate = "2026-10-10",
                SalesOwnerUserId = Guid.Parse("50000000-0000-0000-0000-000000000002"),
                PackagingMethod = "WoodenCrate",
                SalesAmount = (decimal?)null,
                CurrencyCode = (string?)null,
                DeliveryLocation = (string?)null
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("projectId").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task AssertTemplateWidthsAsync(HttpResponseMessage response, int wideColumn)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.True(worksheet.SheetView.SplitRow > 0);
        Assert.True(worksheet.AutoFilter.IsEnabled);
        Assert.Contains("필수 입력값", worksheet.Cell(1, 12).GetString());
        Assert.Equal("Item *", worksheet.Cell(1, 3).GetString());
        Assert.Equal(XLColor.LightYellow, worksheet.Cell(1, 3).Style.Fill.BackgroundColor);
        for (var column = 1; column <= worksheet.LastColumnUsed()!.ColumnNumber(); column++)
        {
            Assert.True(worksheet.Column(column).Width >= 10);
            Assert.True(worksheet.Column(column).Width <= 42);
        }
        Assert.True(worksheet.Column(wideColumn).Width >= 18);
    }

    private static async Task<(Guid ProductTypeId, IReadOnlyList<Guid> Steps)> ReadProductTypeAsync(HttpClient client, string code)
    {
        using var productTypes = await ReadJsonAsync(await client.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == code);
        return (
            productType.GetProperty("productTypeId").GetGuid(),
            productType.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("templateStepId").GetGuid()).ToList());
    }

    private static IReadOnlyList<ProductionStepFixture> ReadSteps(JsonElement productType)
    {
        return productType.GetProperty("steps").EnumerateArray()
            .Select(step => new ProductionStepFixture(
                step.GetProperty("templateStepId").GetGuid(),
                step.GetProperty("sequenceNumber").GetInt32(),
                step.GetProperty("stepName").GetString()!))
            .ToList();
    }

    private static byte[] CreateProductionPlanningExcel(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Production Planning");
        var headers = new[] { "프로젝트명", "PJT Code", "Item", "계획 항목", "예정일", "비고", "구매 담당자", "생산관리 담당자", "제조 담당자", "품질 담당자", "물류 담당자" };
        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = rows[rowIndex][columnIndex];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<JsonDocument> PreviewProductionPlanningExcelAsync(HttpClient client, byte[] file, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(file), "file", fileName);
        var response = await client.PostAsync("/api/production-planning/import/preview", form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonDocument> ApplyProductionPlanningExcelAsync(HttpClient client, byte[] file, string fileName, string fileSha256)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(file), "file", fileName);
        form.Add(new StringContent(fileSha256), "expectedFileSha256");
        var response = await client.PostAsync("/api/production-planning/import/apply", form, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await ReadJsonAsync(response);
    }

    private sealed class FakeKoreanHolidayProvider(IReadOnlyList<SystemHolidayUpsert> holidays) : IKoreanHolidayProvider
    {
        public Task<KoreanHolidayProviderResult> FetchAsync(int year, CancellationToken cancellationToken)
        {
            return Task.FromResult(new KoreanHolidayProviderResult(true, holidays, "fake provider"));
        }
    }

    private sealed class FakeHolidayHttpHandler(string publicHolidayXml, string nationalHolidayXml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri?.Query ?? "";
            var path = request.RequestUri?.AbsolutePath ?? "";
            var content = query.Contains("solMonth=07", StringComparison.Ordinal)
                ? path.Contains("getHoliDeInfo", StringComparison.Ordinal) ? nationalHolidayXml : publicHolidayXml
                : "<response><body><items></items></body></response>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }

    private sealed record ProductionStepFixture(Guid TemplateStepId, int SequenceNumber, string StepName);

    private sealed class ProductionPlanningApiTestContext : IAsyncDisposable
    {
        private ProductionPlanningApiTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public static async Task<ProductionPlanningApiTestContext> CreateAsync(Action<IServiceCollection>? configureTestServices = null)
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
                includeDefaultDevelopmentAuthentication: true,
                configureTestServices: configureTestServices);

            return new ProductionPlanningApiTestContext(database, factory);
        }

        public HttpClient CreateClient(string developmentUserKey)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
            return client;
        }

        public async Task ExecuteSqlAsync(string sql)
        {
            await Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
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

        public IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> overrides)
        {
            var values = BaseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            values["DATABASE_NAME"] = DatabaseName;
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        public async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var drop = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)} with (force);");
            await drop.ExecuteNonQueryAsync();
        }

        private static IConfiguration BuildBaseDatabaseConfiguration(string repositoryRoot)
        {
            var values = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));
            return TestConfigurationIsolation.BuildBaseDatabaseConfiguration(values);
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
                    && Directory.Exists(Path.Combine(directory.FullName, "database")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root not found.");
        }

        private static string QuoteIdentifier(string value)
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
    }
}
