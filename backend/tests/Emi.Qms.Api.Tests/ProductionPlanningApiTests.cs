using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProductionPlanningApiTests
{
    private static readonly Guid TestDesignPrimaryUserId = new("60000000-0000-0000-0000-000000000101");
    private static readonly Guid TestDesignSecondaryUserId = new("60000000-0000-0000-0000-000000000102");
    private static readonly Guid TestSalesPrimaryUserId = new("60000000-0000-0000-0000-000000000201");
    private static readonly Guid TestSalesSecondaryUserId = new("60000000-0000-0000-0000-000000000202");
    private static readonly Guid DevAdminUserId = new("50000000-0000-0000-0000-000000000001");
    private static readonly Guid DevSalesUserId = new("50000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Workflow_ProjectCreation_GeneratesMyWorkAndReferenceNotification()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var adminClient = context.CreateClient("dev-admin");
        using var designClient = context.CreateClient("dev-design");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-CREATE", "Workflow Create");

        using var myWork = await ReadJsonAsync(await adminClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken));
        var generated = myWork.RootElement.GetProperty("items").EnumerateArray()
            .FirstOrDefault(item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("workflowStageCode").GetString() == "ProductionPlanning");
        Assert.NotEqual(JsonValueKind.Undefined, generated.ValueKind);
        Assert.Equal("시작 전", generated.GetProperty("statusLabel").GetString());
        Assert.Equal("system-administrator", await context.ReadScalarAsync<string>($"""
            select assigned_role_code
            from work_items
            where id = '{generated.GetProperty("workItemId").GetGuid()}';
            """));
        Assert.Equal($"/projects/{projectId}/production-planning/edit", generated.GetProperty("linkUrl").GetString());
        using var requestedWork = await ReadJsonAsync(await adminClient.GetAsync("/api/my-work?status=Requested", TestContext.Current.CancellationToken));
        Assert.Contains(requestedWork.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("projectId").GetGuid() == projectId
            && item.GetProperty("workflowStageCode").GetString() == "ProductionPlanning");

        var workItemId = generated.GetProperty("workItemId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await designClient.PostAsync($"/api/my-work/{workItemId}/start", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await salesClient.PostAsync($"/api/my-work/{workItemId}/start", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsync($"/api/my-work/{workItemId}/start", null, TestContext.Current.CancellationToken)).StatusCode);
        using var completed = await ReadJsonAsync(await adminClient.PostAsync($"/api/my-work/{workItemId}/complete", null, TestContext.Current.CancellationToken));
        Assert.Equal("완료", completed.RootElement.GetProperty("statusLabel").GetString());
        using var completedWork = await ReadJsonAsync(await adminClient.GetAsync("/api/my-work?status=Completed", TestContext.Current.CancellationToken));
        Assert.Contains(completedWork.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("workItemId").GetGuid() == workItemId);

        using var notifications = await ReadJsonAsync(await designClient.GetAsync("/api/notifications?readStatus=unread", TestContext.Current.CancellationToken));
        var notification = notifications.RootElement.GetProperty("items").EnumerateArray()
            .First(item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("message").GetString()!.Contains("Workflow Create", StringComparison.Ordinal));
        Assert.Contains(notifications.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("projectId").GetGuid() == projectId
            && item.GetProperty("message").GetString()!.Contains("Workflow Create", StringComparison.Ordinal));
        var notificationId = notification.GetProperty("notificationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await designClient.PostAsync($"/api/notifications/{notificationId}/read", null, TestContext.Current.CancellationToken)).StatusCode);
        using var readNotifications = await ReadJsonAsync(await designClient.GetAsync("/api/notifications?readStatus=read", TestContext.Current.CancellationToken));
        Assert.Contains(readNotifications.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("notificationId").GetGuid() == notificationId);
    }

    [Fact]
    public async Task Workflow_ProductionPlanningSave_GeneratesProcurementWorkOnce()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var designClient = context.CreateClient("dev-design");
        using var productionClient = context.CreateClient("dev-production");
        using var procurementClient = context.CreateClient("dev-procurement");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-PLAN", "Workflow Plan");
        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        Assert.DoesNotContain(productTypes.RootElement.EnumerateArray(), item => item.GetProperty("code").GetString() == "TEST-TYPE");
        Assert.Contains(productTypes.RootElement.EnumerateArray(), item => item.GetProperty("code").GetString() == "RPP");
        Assert.DoesNotContain(productTypes.RootElement.EnumerateArray(), item => item.GetProperty("code").GetString() == "RRP");
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var steps = productType.GetProperty("steps").EnumerateArray().ToList();

        var create = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                notes = "workflow 생성",
                reason = (string?)null,
                items = steps.Select(step => new
                {
                    itemId = (Guid?)null,
                    templateStepId = step.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = step.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = 0,
                    plannedDate = "2026-07-01",
                    note = (string?)null,
                    isDeleted = false
                }).ToArray(),
                assignees = new object[]
                {
                    new { responsibilityType = "DesignPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000010"), note = "설계" },
                    new { responsibilityType = "ProcurementPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000011"), note = "구매" }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var created = await ReadJsonAsync(create);

        var saveAgain = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = created.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = "workflow 재저장",
                reason = (string?)null,
                items = created.RootElement.GetProperty("items").EnumerateArray().Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").GetGuid(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = item.GetProperty("plannedDate").GetString(),
                    note = (string?)null,
                    isDeleted = false
                }).ToArray(),
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, saveAgain.StatusCode);

        using var myWork = await ReadJsonAsync(await procurementClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken));
        var procurementWork = myWork.RootElement.GetProperty("items").EnumerateArray()
            .Where(item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("workflowStageCode").GetString() == "ProcurementInfo")
            .ToList();
        Assert.Single(procurementWork);
        Assert.Equal("구매정보 입력", procurementWork[0].GetProperty("title").GetString());
        Assert.Equal("ProcurementPrimary", procurementWork[0].GetProperty("responsibilityType").GetString());
        Assert.Equal($"/projects/{projectId}/procurement/edit", procurementWork[0].GetProperty("linkUrl").GetString());
        var procurementWorkItemId = procurementWork[0].GetProperty("workItemId").GetGuid();
        using var designWork = await ReadJsonAsync(await designClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken));
        var generatedDesignWork = designWork.RootElement.GetProperty("items").EnumerateArray()
            .Where(item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("workflowStageCode").GetString() == "DesignPanelInfo")
            .ToList();
        Assert.Single(generatedDesignWork);
        Assert.Equal("패널명, 사이즈 입력", generatedDesignWork[0].GetProperty("title").GetString());
        Assert.Equal("DesignPrimary", generatedDesignWork[0].GetProperty("responsibilityType").GetString());
        Assert.Equal($"/projects/{projectId}/panel-information/edit", generatedDesignWork[0].GetProperty("linkUrl").GetString());
        var designWorkItemId = generatedDesignWork[0].GetProperty("workItemId").GetGuid();

        using var panelInfo = await ReadJsonAsync(await designClient.GetAsync($"/api/projects/{projectId}/panel-information", TestContext.Current.CancellationToken));
        var panelRows = panelInfo.RootElement.GetProperty("panels").EnumerateArray().ToList();
        var partialPanel = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                panels = new[]
                {
                    new
                    {
                        panelId = panelRows[0].GetProperty("panelId").GetGuid(),
                        expectedPanelInfoVersion = panelRows[0].GetProperty("panelInfoVersion").GetInt32(),
                        panelNameUpdate = new { isChanged = true, value = "PNL-1" }
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, partialPanel.StatusCode);
        Assert.Equal("InProgress", await context.ReadScalarAsync<string>($"""
            select status from work_items where id = '{designWorkItemId}';
            """));
        Assert.True(await context.ReadScalarAsync<bool>($"""
            select started_at_utc is not null and completed_at_utc is null
            from work_items
            where id = '{designWorkItemId}';
            """));

        using var partialPanelJson = await ReadJsonAsync(partialPanel);
        var partialPanelRows = partialPanelJson.RootElement.GetProperty("panels").EnumerateArray().ToList();
        var completePanel = await designClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/panel-information",
            new
            {
                panels = partialPanelRows.Select((row, index) => new
                {
                    panelId = row.GetProperty("panelId").GetGuid(),
                    expectedPanelInfoVersion = row.GetProperty("panelInfoVersion").GetInt32(),
                    panelNameUpdate = new { isChanged = true, value = $"PNL-{index + 1}" },
                    sizeUpdate = new { isChanged = true, clear = false, inputUnit = "mm", width = 100m + index, height = 200m + index, depth = 300m + index }
                }).ToArray()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completePanel.StatusCode);
        Assert.Equal("Completed", await context.ReadScalarAsync<string>($"""
            select status from work_items where id = '{designWorkItemId}';
            """));
        Assert.True(await context.ReadScalarAsync<bool>($"""
            select started_at_utc is not null and completed_at_utc is not null
            from work_items
            where id = '{designWorkItemId}';
            """));

        var completeProcurement = await procurementClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/procurement",
            new { reason = "workflow procurement complete", items = new[] { new { orderItem = "차단기", supplierName = "테스트 업체" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeProcurement.StatusCode);
        Assert.Equal("Completed", await context.ReadScalarAsync<string>($"""
            select status from work_items where id = '{procurementWorkItemId}';
            """));
        Assert.True(await context.ReadScalarAsync<bool>($"""
            select started_at_utc is not null and completed_at_utc is not null
            from work_items
            where id = '{procurementWorkItemId}';
            """));

        using var salesWork = await ReadJsonAsync(await salesClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken));
        Assert.DoesNotContain(salesWork.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("projectId").GetGuid() == projectId
            && item.GetProperty("workflowStageCode").GetString() == "DesignPanelInfo");
        using var assigneeNotifications = await ReadJsonAsync(await procurementClient.GetAsync("/api/notifications?readStatus=unread", TestContext.Current.CancellationToken));
        Assert.Contains(assigneeNotifications.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("projectId").GetGuid() == projectId
            && item.GetProperty("title").GetString() == "프로젝트 담당자로 지정되었습니다.");

        using var workflow = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{projectId}/workflow", TestContext.Current.CancellationToken));
        Assert.Equal(18, workflow.RootElement.GetProperty("stages").GetArrayLength());
        var workflowStages = workflow.RootElement.GetProperty("stages").EnumerateArray().ToList();
        Assert.Equal("ProductionPlanning", workflowStages.First(item => item.GetProperty("sequenceNumber").GetInt32() == 2).GetProperty("stageCode").GetString());
        Assert.Equal("DesignPanelInfo", workflowStages.First(item => item.GetProperty("sequenceNumber").GetInt32() == 3).GetProperty("stageCode").GetString());
        Assert.True(workflow.RootElement.GetProperty("generatedWorkItemCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Workflow_QualityStage_UsesQualitySecondaryWhenPrimaryIsMissing()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var qualityClient = context.CreateClient("dev-quality");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-IQC-SEC", "Workflow IQC Secondary");
        await context.ExecuteSqlAsync($"""
            insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
            values ('{projectId}', 'QualityIQCSecondary', '50000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000002', now());
            """);

        await context.WorkflowStore.CompleteStageAsync(
            projectId,
            WorkflowStageCodes.MaterialArrived,
            "Test",
            null,
            Guid.Parse("50000000-0000-0000-0000-000000000002"),
            "test-quality-secondary",
            "품질 부 담당자 fallback 검증",
            TestContext.Current.CancellationToken);

        using var qualityWork = await ReadJsonAsync(await qualityClient.GetAsync("/api/my-work", TestContext.Current.CancellationToken));
        var iqcWork = qualityWork.RootElement.GetProperty("items").EnumerateArray()
            .Where(item =>
                item.GetProperty("projectId").GetGuid() == projectId
                && item.GetProperty("workflowStageCode").GetString() == "IQC")
            .ToList();
        Assert.Single(iqcWork);
        Assert.Equal("수입검사 입력", iqcWork[0].GetProperty("title").GetString());
        Assert.Equal($"/projects/{projectId}?section=workflow", iqcWork[0].GetProperty("linkUrl").GetString());
    }

    [Fact]
    public async Task Workflow_Fallback_UsesPrimarySecondarySalesAndSystemAdministratorOrder()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");

        await InsertRoleUserAsync(context, TestDesignPrimaryUserId, "test-design-primary", "Test Design Primary", "design", "design");
        await InsertRoleUserAsync(context, TestDesignSecondaryUserId, "test-design-secondary", "Test Design Secondary", "design", "design");
        await InsertRoleUserAsync(context, TestSalesPrimaryUserId, "test-sales-primary", "Test Sales Primary", "sales", "sales");
        await InsertRoleUserAsync(context, TestSalesSecondaryUserId, "test-sales-secondary", "Test Sales Secondary", "sales", "sales");

        var primaryProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-FB-PRI", "Workflow Fallback Primary");
        await InsertAssigneeAsync(context, primaryProjectId, "DesignPrimary", TestDesignPrimaryUserId);
        await InsertAssigneeAsync(context, primaryProjectId, "DesignSecondary", TestDesignSecondaryUserId);
        await InsertAssigneeAsync(context, primaryProjectId, "SalesPrimary", TestSalesPrimaryUserId);
        await InsertAssigneeAsync(context, primaryProjectId, "SalesSecondary", TestSalesSecondaryUserId);
        await CompleteProductionPlanningForFallbackAsync(context, primaryProjectId, "fallback-primary");
        await AssertGeneratedDesignWorkAsync(context, primaryProjectId, TestDesignPrimaryUserId, "design");

        var secondaryProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-FB-SEC", "Workflow Fallback Secondary");
        await InsertAssigneeAsync(context, secondaryProjectId, "DesignSecondary", TestDesignSecondaryUserId);
        await InsertAssigneeAsync(context, secondaryProjectId, "SalesPrimary", TestSalesPrimaryUserId);
        await CompleteProductionPlanningForFallbackAsync(context, secondaryProjectId, "fallback-secondary");
        await AssertGeneratedDesignWorkAsync(context, secondaryProjectId, TestDesignSecondaryUserId, "design");

        var salesPrimaryProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-FB-SALES1", "Workflow Fallback Sales Primary");
        await InsertAssigneeAsync(context, salesPrimaryProjectId, "SalesPrimary", TestSalesPrimaryUserId);
        await InsertAssigneeAsync(context, salesPrimaryProjectId, "SalesSecondary", TestSalesSecondaryUserId);
        await CompleteProductionPlanningForFallbackAsync(context, salesPrimaryProjectId, "fallback-sales-primary");
        await AssertGeneratedDesignWorkAsync(context, salesPrimaryProjectId, TestSalesPrimaryUserId, "sales");

        var salesSecondaryProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-FB-SALES2", "Workflow Fallback Sales Secondary");
        await InsertAssigneeAsync(context, salesSecondaryProjectId, "SalesSecondary", TestSalesSecondaryUserId);
        await CompleteProductionPlanningForFallbackAsync(context, salesSecondaryProjectId, "fallback-sales-secondary");
        await AssertGeneratedDesignWorkAsync(context, salesSecondaryProjectId, TestSalesSecondaryUserId, "sales");
        Assert.NotEqual(DevSalesUserId, await ReadGeneratedWorkAssigneeIdAsync(context, salesSecondaryProjectId, WorkflowStageCodes.DesignPanelInfo));

        var adminProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-FB-ADMIN", "Workflow Fallback Admin");
        await CompleteProductionPlanningForFallbackAsync(context, adminProjectId, "fallback-admin");
        await AssertGeneratedDesignWorkAsync(context, adminProjectId, DevAdminUserId, "system-administrator");
    }

    [Fact]
    public async Task Workflow_CompleteStageEvent_CompletesCurrentStageWorkItemAcrossWorkflow()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "WF-STAGE-COMPLETE", "Workflow Stage Complete");
        await context.ExecuteSqlAsync($"update projects set fat_required = true where id = '{projectId}';");

        var stages = new[]
        {
            WorkflowStageCodes.ProductionPlanning,
            WorkflowStageCodes.DesignPanelInfo,
            WorkflowStageCodes.ProcurementInfo,
            WorkflowStageCodes.MaterialArrived,
            WorkflowStageCodes.IQC,
            WorkflowStageCodes.ReceiptConfirmed,
            WorkflowStageCodes.KittingCompleted,
            WorkflowStageCodes.ManufacturingWork,
            WorkflowStageCodes.LQC,
            WorkflowStageCodes.ManufacturingCompleted,
            WorkflowStageCodes.OQC,
            WorkflowStageCodes.CustomerInspection,
            WorkflowStageCodes.FAT,
            WorkflowStageCodes.PackingCompleted,
            WorkflowStageCodes.DepartureProcessed,
            WorkflowStageCodes.DeliveryCompleted,
            WorkflowStageCodes.SalesSettlementCompleted
        };

        foreach (var stageCode in stages)
        {
            Assert.True(await WorkItemExistsAsync(context, projectId, stageCode), $"Expected work item before completing {stageCode}.");

            await context.WorkflowStore.CompleteStageAsync(
                projectId,
                stageCode,
                "Test",
                null,
                DevSalesUserId,
                $"complete-{stageCode}",
                "stage complete event work item sync",
                TestContext.Current.CancellationToken);

            Assert.True(await HasCompletedWorkItemAsync(context, projectId, stageCode), $"Expected completed work item for {stageCode}.");
        }
    }

    [Fact]
    public async Task ProductionPlanning_UpdateAssigneesStatusAndHistory_AreRoleScoped()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");
        using var procurementClient = context.CreateClient("dev-procurement");
        using var viewerClient = context.CreateClient("dev-viewer");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-TEST", "Plan Test");

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
                    new { responsibilityType = "SalesPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000002"), note = "영업 정" },
                    new { responsibilityType = "SalesSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000002"), note = "영업 부" },
                    new { responsibilityType = "DesignPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000010"), note = "설계 정" },
                    new { responsibilityType = "DesignSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000010"), note = "설계 부" },
                    new { responsibilityType = "ProductionPlanningPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000003"), note = "생산관리 정" },
                    new { responsibilityType = "ProductionPlanningSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000003"), note = "생산관리 부" },
                    new { responsibilityType = "ProcurementPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000011"), note = "구매 정" },
                    new { responsibilityType = "ProcurementSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000011"), note = "구매 부" },
                    new { responsibilityType = "MaterialsPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000012"), note = "자재 정" },
                    new { responsibilityType = "MaterialsSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000012"), note = "자재 부" },
                    new { responsibilityType = "ManufacturingPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000004"), note = "제조 정" },
                    new { responsibilityType = "ManufacturingSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000004"), note = "제조 부" },
                    new { responsibilityType = "LogisticsPrimary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000006"), note = "물류 정" },
                    new { responsibilityType = "LogisticsSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000006"), note = "물류 부" },
                    new { responsibilityType = "QualityIQC", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "IQC" },
                    new { responsibilityType = "QualityIQCSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "IQC 부" },
                    new { responsibilityType = "QualityLQC", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "LQC" },
                    new { responsibilityType = "QualityLQCSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "LQC 부" },
                    new { responsibilityType = "QualityOQC", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "OQC" },
                    new { responsibilityType = "QualityOQCSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "OQC 부" },
                    new { responsibilityType = "QualityCustomerInspection", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "전진검수/FAT" },
                    new { responsibilityType = "QualityCustomerInspectionSecondary", assigneeId = (Guid?)null, expectedRowVersion = 0, assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000005"), note = "전진검수/FAT 부" }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        using var partialJson = await ReadJsonAsync(partial);
        Assert.Equal("Planning", partialJson.RootElement.GetProperty("planStatus").GetString());
        Assert.Equal("InProgress", await context.ReadScalarAsync<string>($"""
            select status
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.ProductionPlanning}'
            order by created_at_utc desc
            limit 1;
            """));
        Assert.True(await context.ReadScalarAsync<bool>($"""
            select started_at_utc is not null and completed_at_utc is null
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.ProductionPlanning}'
            order by created_at_utc desc
            limit 1;
            """));
        Assert.Contains(partialJson.RootElement.GetProperty("assignees").EnumerateArray(), item =>
            item.GetProperty("responsibilityType").GetString() == "QualityCustomerInspection"
            && item.GetProperty("assignedUserId").GetGuid() == Guid.Parse("50000000-0000-0000-0000-000000000005"));

        var currentItems = partialJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        var currentAssignees = partialJson.RootElement.GetProperty("assignees").EnumerateArray().ToList();
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "SalesPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "DesignPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "ProductionPlanningPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "ProcurementPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "MaterialsPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "ManufacturingPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "LogisticsPrimary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityIQC");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityIQCSecondary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityLQC");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityLQCSecondary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityOQC");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityOQCSecondary");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityCustomerInspection");
        Assert.Contains(currentAssignees, item => item.GetProperty("responsibilityType").GetString() == "QualityCustomerInspectionSecondary");

        var roleMismatch = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = partialJson.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = "역할 불일치",
                reason = "역할 불일치 확인",
                items = currentItems.Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    templateStepId = item.GetProperty("templateStepId").GetGuid(),
                    stepName = item.GetProperty("stepName").GetString(),
                    sequenceNumber = item.GetProperty("sequenceNumber").GetInt32(),
                    isRequired = item.GetProperty("isRequired").GetBoolean(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedDate = item.GetProperty("plannedDate").ValueKind == JsonValueKind.Null ? null : item.GetProperty("plannedDate").GetString(),
                    note = item.GetProperty("note").ValueKind == JsonValueKind.Null ? null : item.GetProperty("note").GetString(),
                    isDeleted = false
                }).ToArray(),
                assignees = new[]
                {
                    new
                    {
                        responsibilityType = "DesignPrimary",
                        assigneeId = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "DesignPrimary").GetProperty("assigneeId").GetGuid(),
                        expectedRowVersion = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "DesignPrimary").GetProperty("rowVersion").GetInt32(),
                        assignedUserId = Guid.Parse("50000000-0000-0000-0000-000000000011"),
                        note = "구매 사용자를 설계에 지정"
                    }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, roleMismatch.StatusCode);

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
                        responsibilityType = "ProcurementPrimary",
                        assigneeId = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "ProcurementPrimary").GetProperty("assigneeId").GetGuid(),
                        expectedRowVersion = currentAssignees.First(item => item.GetProperty("responsibilityType").GetString() == "ProcurementPrimary").GetProperty("rowVersion").GetInt32(),
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
        Assert.Equal("Completed", await context.ReadScalarAsync<string>($"""
            select status
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.ProductionPlanning}'
            order by created_at_utc desc
            limit 1;
            """));
        Assert.True(await context.ReadScalarAsync<bool>($"""
            select started_at_utc is not null and completed_at_utc is not null
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.ProductionPlanning}'
            order by created_at_utc desc
            limit 1;
            """));

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

        var activeId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-ACTIVE", "Plan Active");
        var completedId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-COMPLETE", "Plan Complete");
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

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-CUSTOM", "Plan Custom");
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
    public async Task ProductionPlanning_TemplateItems_CanOverrideStepNameAndRequiredPerProject()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-OVERRIDE", "Plan Override");
        using var productTypes = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var productType = productTypes.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var productTypeId = productType.GetProperty("productTypeId").GetGuid();
        var firstStep = productType.GetProperty("steps").EnumerateArray().First();
        var originalStepName = firstStep.GetProperty("stepName").GetString();
        var firstStepId = firstStep.GetProperty("templateStepId").GetGuid();

        var update = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId,
                expectedRowVersion = 0,
                notes = (string?)null,
                reason = (string?)null,
                items = new object[]
                {
                    new
                    {
                        itemId = (Guid?)null,
                        templateStepId = firstStepId,
                        stepName = "프로젝트 전용 자재 입고",
                        isRequired = false,
                        sequenceNumber = firstStep.GetProperty("sequenceNumber").GetInt32(),
                        expectedRowVersion = 0,
                        plannedDate = (string?)null,
                        note = (string?)null,
                        isDeleted = false
                    }
                },
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var updatedJson = await ReadJsonAsync(update);
        var overridden = updatedJson.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("templateStepId").GetGuid() == firstStepId);
        Assert.Equal("프로젝트 전용 자재 입고", overridden.GetProperty("stepName").GetString());
        Assert.False(overridden.GetProperty("isRequired").GetBoolean());

        using var productTypesAfter = await ReadJsonAsync(await productionClient.GetAsync("/api/production-planning/product-types", TestContext.Current.CancellationToken));
        var masterStep = productTypesAfter.RootElement.EnumerateArray()
            .First(item => item.GetProperty("code").GetString() == "UL67")
            .GetProperty("steps").EnumerateArray()
            .First(step => step.GetProperty("templateStepId").GetGuid() == firstStepId);
        Assert.Equal(originalStepName, masterStep.GetProperty("stepName").GetString());
        Assert.True(masterStep.GetProperty("isRequired").GetBoolean());
    }

    [Fact]
    public async Task ProductionPlanningExcel_PreviewsAndAppliesMultipleProjectsWithCustomSteps()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");

        var firstProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-1", "Plan Excel One");
        var secondProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-2", "Plan Excel Two");

        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/production-planning/import/template", TestContext.Current.CancellationToken)).StatusCode);
        var templateResponse = await productionClient.GetAsync("/api/production-planning/import/template", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        await AssertTemplateWidthsAsync(templateResponse, wideColumn: 4);

        var file = CreateProductionPlanningExcel([
            ["Plan Excel One", "PLAN-XLS-1", "UL67", "자재 도착", "예", "2026-07-01", "template"],
            ["Plan Excel Two", "PLAN-XLS-2", "UL67", "사용자 추가 항목", "아니오", "2026-07-09", "custom"],
            ["Unknown", "NO-SUCH", "UL67", "자재 도착", "예", "2026-07-01", ""]
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
    public async Task ProductionPlanningProjectExcel_PreviewsAndAppliesCurrentProjectTemplate()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-PROJECT", "Plan Excel Project");
        var file = CreateProjectProductionPlanningExcel([
            ["자재 도착", "예", "2026-07-01", "project template"]
        ]);

        using var forbiddenForm = new MultipartFormDataContent();
        forbiddenForm.Add(new ByteArrayContent(file), "file", "project-planning.xlsx");
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PostAsync($"/api/projects/{projectId}/production-planning/import/preview", forbiddenForm, TestContext.Current.CancellationToken)).StatusCode);

        using var preview = await PreviewProjectProductionPlanningExcelAsync(productionClient, projectId, file, "project-planning.xlsx");
        var root = preview.RootElement;
        Assert.Equal(1, root.GetProperty("saveableCount").GetInt32());
        var row = root.GetProperty("rows").EnumerateArray().Single();
        Assert.Equal(projectId, row.GetProperty("projectId").GetGuid());
        Assert.Equal("PLAN-XLS-PROJECT", row.GetProperty("projectCode").GetString());
        Assert.Equal("UL67", row.GetProperty("productTypeCode").GetString());
        Assert.Equal("자재 도착", row.GetProperty("stepName").GetString());

        using var apply = await ApplyProjectProductionPlanningExcelAsync(productionClient, projectId, file, "project-planning.xlsx", root.GetProperty("fileSha256").GetString()!);
        Assert.Equal(1, apply.RootElement.GetProperty("appliedRowCount").GetInt32());
        Assert.Contains(apply.RootElement.GetProperty("projectIds").EnumerateArray(), item => item.GetGuid() == projectId);

        using var projectPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{projectId}/production-planning", TestContext.Current.CancellationToken));
        Assert.Contains(projectPlan.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("stepName").GetString() == "자재 도착"
            && item.GetProperty("plannedDate").GetString() == "2026-07-01"
            && item.GetProperty("note").GetString() == "project template");
    }

    [Fact]
    public async Task ProductionTemplateSettings_AffectNewTemplatesOnlyAndValidateRows()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");
        using var adminClient = context.CreateClient("dev-admin");

        var existingProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-SET-OLD", "Plan Settings Old");
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

        var updateAgain = await productionClient.PatchAsJsonAsync(
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
                    new { templateStepId = (Guid?)null, sequenceNumber = 99, stepName = "최신 설정 단계", isRequired = false, isActive = true }
                }).ToArray(),
                reason = "최신 설정 유지"
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateAgain.StatusCode);
        using var updateAgainJson = await ReadJsonAsync(updateAgain);
        var latestUl67 = updateAgainJson.RootElement.EnumerateArray().First(item => item.GetProperty("code").GetString() == "UL67");
        var latestSteps = latestUl67.GetProperty("steps").EnumerateArray().ToList();
        Assert.Contains(latestSteps, step => step.GetProperty("stepName").GetString() == "최신 설정 단계");
        Assert.DoesNotContain(latestSteps, step => step.GetProperty("stepName").GetString() == "최종 확인");

        var newProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-SET-NEW", "Plan Settings New");
        using var newPlan = await ReadJsonAsync(await productionClient.GetAsync($"/api/projects/{newProjectId}/production-planning", TestContext.Current.CancellationToken));
        var generatedStep = newPlan.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("stepName").GetString() == "최신 설정 단계");
        Assert.Equal(JsonValueKind.Null, generatedStep.GetProperty("plannedDate").ValueKind);
    }

    [Fact]
    public async Task ExistingProductionPlan_KeepsTemplateSnapshotAfterTemplateSettingsChange()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var productionClient = context.CreateClient("dev-production");

        var projectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-SNAPSHOT", "Plan Snapshot");
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

        var mismatchProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-MISMATCH", "Plan Excel Mismatch");
        var mismatchFile = CreateProductionPlanningExcel([
            ["Plan Excel Mismatch", "PLAN-XLS-MISMATCH", "RRP", "자재 도착", "예", "2026-07-01", ""]
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
        Assert.Equal(JsonValueKind.String, mismatchPlan.RootElement.GetProperty("planId").ValueKind);
        Assert.DoesNotContain(
            mismatchPlan.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("plannedDate").GetString() == "2026-07-01");

        var existingProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-SNAPSHOT", "Plan Excel Snapshot");
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
        Assert.Equal(originalTemplateId, latestTemplateId);
        Assert.Contains(
            updatedUl67.GetProperty("steps").EnumerateArray(),
            step => step.GetProperty("stepName").GetString() == "Excel 신규 template step");

        var existingFile = CreateProductionPlanningExcel([
            ["Plan Excel Snapshot", "PLAN-XLS-SNAPSHOT", "UL67", originalSteps[0].StepName, "예", "2026-08-10", "Excel 수정"]
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

        var newProjectId = await CreateProjectAndReadIdAsync(context, salesClient, "PLAN-XLS-NEW", "Plan Excel New");
        var newFile = CreateProductionPlanningExcel([
            ["Plan Excel New", "PLAN-XLS-NEW", "UL67", "Excel 신규 template step", "아니오", "2026-09-01", ""]
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
        Assert.Contains(holidayItems, item =>
            item.GetProperty("holidayDate").GetString() == "2026-07-06"
            && item.GetProperty("name").GetString() == "공식 대체공휴일"
            && item.GetProperty("holidayType").GetString() == "National");
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
            && item.GetProperty("source").GetString() == "OfficialApi:NationalHoliday"
            && item.GetProperty("holidayType").GetString() == "National");
        Assert.Contains(holidayItems, item =>
            item.GetProperty("holidayDate").GetString() == "2026-12-25"
            && item.GetProperty("name").GetString() == "기독탄신일"
            && item.GetProperty("source").GetString() == "OfficialApi:PublicHoliday"
            && item.GetProperty("holidayType").GetString() == "National");

        using var duplicateSync = await ReadJsonAsync(await adminClient.PostAsync("/api/production-planning/holidays/sync?year=2026", null, TestContext.Current.CancellationToken));
        Assert.True(duplicateSync.RootElement.GetProperty("isConfigured").GetBoolean());

        using var duplicatedHolidays = await ReadJsonAsync(await adminClient.GetAsync("/api/system/holidays?countryCode=KR&dateFrom=2026-07-01&dateTo=2026-12-31", TestContext.Current.CancellationToken));
        Assert.Single(duplicatedHolidays.RootElement.EnumerateArray(), item => item.GetProperty("holidayDate").GetString() == "2026-07-17");
        Assert.Single(duplicatedHolidays.RootElement.EnumerateArray(), item => item.GetProperty("holidayDate").GetString() == "2026-12-25");
    }

    [Fact]
    public async Task BusinessCalendar_ReturnsWeekendHolidayAndCompanyHolidayInfo()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");

        await context.ExecuteSqlAsync("""
            insert into system_holidays (holiday_date, name, country_code, source, source_key, holiday_type, is_active)
            values
                ('2026-07-03', '임시공휴일', 'KR', 'Test', 'test-20260703', 'Temporary', true),
                ('2026-07-06', '공식 대체공휴일', 'KR', 'Test', 'test-20260706', 'Substitute', true),
                ('2026-07-07', '회사 창립기념 휴일', 'KR', 'TestCompany', 'test-20260707', 'Company', true),
                ('2026-07-08', '비활성 공휴일', 'KR', 'Test', 'test-20260708', 'National', false);
            """);

        using var calendarResponse = await salesClient.GetAsync("/api/calendar/business-days?countryCode=KR&from=2026-07-02&to=2026-07-08", TestContext.Current.CancellationToken);
        var calendarBody = await calendarResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(calendarResponse.StatusCode == HttpStatusCode.OK, $"Expected OK but got {calendarResponse.StatusCode}. Body: {calendarBody}. Logs: {context.ErrorLogs()}");
        using var calendar = JsonDocument.Parse(calendarBody);

        Assert.Equal("2026-07-02", calendar.RootElement.GetProperty("from").GetString());
        Assert.Equal("2026-07-08", calendar.RootElement.GetProperty("to").GetString());
        Assert.Equal("KR", calendar.RootElement.GetProperty("countryCode").GetString());
        var days = calendar.RootElement.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);

        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-02"
            && day.GetProperty("isBusinessDay").GetBoolean()
            && !day.GetProperty("isHoliday").GetBoolean());
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-03"
            && !day.GetProperty("isBusinessDay").GetBoolean()
            && day.GetProperty("holidayType").GetString() == "Temporary");
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-04"
            && day.GetProperty("isWeekend").GetBoolean()
            && !day.GetProperty("isBusinessDay").GetBoolean());
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-06"
            && day.GetProperty("holidayType").GetString() == "Substitute");
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-07"
            && day.GetProperty("isCompanyHoliday").GetBoolean()
            && day.GetProperty("holidayType").GetString() == "Company");
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-08"
            && day.GetProperty("isBusinessDay").GetBoolean()
            && day.GetProperty("holidayName").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task AdminCalendarHolidays_ManagesManualHolidaysAndBusinessCalendarReflectsChanges()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");
        using var salesClient = context.CreateClient("dev-sales");

        Assert.Equal(HttpStatusCode.Forbidden, (await salesClient.GetAsync("/api/admin/calendar/holidays?year=2026", TestContext.Current.CancellationToken)).StatusCode);

        var invalid = await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays",
            new { date = "2026-07-09", name = "잘못된 휴일", holidayType = "Weekend", isActive = true, note = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var companyHoliday = await CreateAdminHolidayAsync(adminClient, "2026-07-09", "회사 창립기념 휴일", "Company", "회사 지정");
        var companyHolidayId = companyHoliday.RootElement.GetProperty("holidayId").GetGuid();
        using var nationalHoliday = await CreateAdminHolidayAsync(adminClient, "2026-07-10", "국가 지정 테스트 휴일", "National", null);
        using var substituteHoliday = await CreateAdminHolidayAsync(adminClient, "2026-07-13", "대체공휴일 테스트", "Substitute", null);
        using var temporaryHoliday = await CreateAdminHolidayAsync(adminClient, "2026-07-14", "임시공휴일 테스트", "Temporary", null);

        var duplicate = await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays",
            new { date = "2026-07-09", name = "중복 회사휴일", holidayType = "Company", isActive = true, note = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        using var updated = await ReadJsonAsync(await adminClient.PutAsJsonAsync(
            $"/api/admin/calendar/holidays/{companyHolidayId}",
            new { date = "2026-07-09", name = "회사 창립기념일", holidayType = "Company", isActive = true, note = "연간 등록" },
            TestContext.Current.CancellationToken));
        Assert.Equal("회사 창립기념일", updated.RootElement.GetProperty("name").GetString());
        Assert.Equal("연간 등록", updated.RootElement.GetProperty("note").GetString());

        using var calendar = await ReadJsonAsync(await adminClient.GetAsync("/api/calendar/business-days?countryCode=KR&from=2026-07-09&to=2026-07-14", TestContext.Current.CancellationToken));
        var days = calendar.RootElement.GetProperty("days").EnumerateArray().ToList();
        Assert.Contains(days, day =>
            day.GetProperty("date").GetString() == "2026-07-09"
            && day.GetProperty("holidayType").GetString() == "Company"
            && day.GetProperty("isCompanyHoliday").GetBoolean()
            && !day.GetProperty("isBusinessDay").GetBoolean());
        Assert.Contains(days, day => day.GetProperty("date").GetString() == "2026-07-10" && day.GetProperty("holidayType").GetString() == "National");
        Assert.Contains(days, day => day.GetProperty("date").GetString() == "2026-07-13" && day.GetProperty("holidayType").GetString() == "Substitute");
        Assert.Contains(days, day => day.GetProperty("date").GetString() == "2026-07-14" && day.GetProperty("holidayType").GetString() == "Temporary");

        using var deactivated = await ReadJsonAsync(await adminClient.DeleteAsync($"/api/admin/calendar/holidays/{companyHolidayId}", TestContext.Current.CancellationToken));
        Assert.False(deactivated.RootElement.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, deactivated.RootElement.GetProperty("deletionRequestedAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, deactivated.RootElement.GetProperty("scheduledHardDeleteAtUtc").ValueKind);
        Assert.Equal("DeletionScheduled", deactivated.RootElement.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("삭제 예정", deactivated.RootElement.GetProperty("lifecycleStatusLabel").GetString());
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}", deactivated.RootElement.GetProperty("scheduledHardDeleteLabel").GetString());

        using var afterDeactivate = await ReadJsonAsync(await adminClient.GetAsync("/api/calendar/business-days?countryCode=KR&from=2026-07-09&to=2026-07-09", TestContext.Current.CancellationToken));
        var dayAfterDeactivate = afterDeactivate.RootElement.GetProperty("days").EnumerateArray().Single();
        Assert.True(dayAfterDeactivate.GetProperty("isBusinessDay").GetBoolean());
        Assert.False(dayAfterDeactivate.GetProperty("isHoliday").GetBoolean());

        using var list = await ReadJsonAsync(await adminClient.GetAsync("/api/admin/calendar/holidays?year=2026", TestContext.Current.CancellationToken));
        Assert.Contains(list.RootElement.GetProperty("holidays").EnumerateArray(), item =>
            item.GetProperty("holidayId").GetGuid() == companyHolidayId
            && !item.GetProperty("isActive").GetBoolean()
            && item.GetProperty("deletionRequestedAtUtc").ValueKind != JsonValueKind.Null);

        using var restoredHoliday = await ReadJsonAsync(await adminClient.PostAsync(
            $"/api/admin/calendar/holidays/{companyHolidayId}/restore",
            null,
            TestContext.Current.CancellationToken));
        Assert.True(restoredHoliday.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal("Active", restoredHoliday.RootElement.GetProperty("lifecycleStatus").GetString());

        using var afterRestore = await ReadJsonAsync(await adminClient.GetAsync("/api/calendar/business-days?countryCode=KR&from=2026-07-09&to=2026-07-09", TestContext.Current.CancellationToken));
        var dayAfterRestore = afterRestore.RootElement.GetProperty("days").EnumerateArray().Single();
        Assert.False(dayAfterRestore.GetProperty("isBusinessDay").GetBoolean());
        Assert.True(dayAfterRestore.GetProperty("isHoliday").GetBoolean());

        using var bulkDelete = await ReadJsonAsync(await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays/bulk-delete",
            new { ids = new[] { companyHolidayId }, reason = "bulk delete" },
            TestContext.Current.CancellationToken));
        Assert.Equal("DeleteScheduled", bulkDelete.RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        using var bulkRestore = await ReadJsonAsync(await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays/bulk-restore",
            new { ids = new[] { companyHolidayId }, reason = "bulk restore" },
            TestContext.Current.CancellationToken));
        Assert.Equal("Restored", bulkRestore.RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        using var bulkDeleteAgain = await ReadJsonAsync(await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays/bulk-delete",
            new { ids = new[] { companyHolidayId }, reason = "bulk delete" },
            TestContext.Current.CancellationToken));
        Assert.Equal("DeleteScheduled", bulkDeleteAgain.RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        using var immediatePurge = await ReadJsonAsync(await adminClient.PostAsJsonAsync(
            "/api/admin/calendar/holidays/bulk-delete",
            new { ids = new[] { companyHolidayId }, reason = "bulk purge" },
            TestContext.Current.CancellationToken));
        Assert.Equal("Purged", immediatePurge.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        using var afterPurgeList = await ReadJsonAsync(await adminClient.GetAsync("/api/admin/calendar/holidays?year=2026", TestContext.Current.CancellationToken));
        Assert.DoesNotContain(afterPurgeList.RootElement.GetProperty("holidays").EnumerateArray(), item => item.GetProperty("holidayId").GetGuid() == companyHolidayId);
        Assert.True(await context.ReadScalarAsync<long>($"""
            select count(*)
            from admin_master_change_logs
            where entity_type = 'Holiday'
              and entity_id = '{companyHolidayId}'
              and action in ('DeleteScheduled', 'Restored', 'Purged');
            """) >= 3);
    }

    [Fact]
    public async Task AdminCalendarHolidays_ExcelTemplatePreviewAndApplyUpsertsRows()
    {
        await using var context = await ProductionPlanningApiTestContext.CreateAsync();
        using var adminClient = context.CreateClient("dev-admin");

        using var template = await adminClient.GetAsync("/api/admin/calendar/holidays/template", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        await AssertCalendarHolidayTemplateAsync(template);

        using var existingHoliday = await CreateAdminHolidayAsync(adminClient, "2026-10-03", "기존 개천절", "National", null);
        var excelBytes = CreateCalendarHolidayExcel([
            new CalendarHolidayExcelTestRow("2026-10-03", "개천절", "National", "기존 갱신"),
            new CalendarHolidayExcelTestRow("2026-10-05", "대체공휴일", "Substitute", "신규"),
            new CalendarHolidayExcelTestRow("2026-10-06", "오류 휴일", "Invalid", "오류")
        ]);

        using var previewResponse = await adminClient.PostAsync("/api/admin/calendar/holidays/preview", CreateExcelMultipartContent(excelBytes), TestContext.Current.CancellationToken);
        var previewBody = await previewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, $"Expected OK but got {previewResponse.StatusCode}. Body: {previewBody}. Logs: {context.ErrorLogs()}");
        using var preview = JsonDocument.Parse(previewBody);
        Assert.Equal(3, preview.RootElement.GetProperty("totalRows").GetInt32());
        Assert.Equal(2, preview.RootElement.GetProperty("saveableCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("insertCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("updateCount").GetInt32());
        Assert.Equal(1, preview.RootElement.GetProperty("errorCount").GetInt32());
        Assert.Contains(preview.RootElement.GetProperty("rows").EnumerateArray(), row =>
            row.GetProperty("resultType").GetString() == "Update"
            && row.GetProperty("date").GetString() == "2026-10-03");
        Assert.Contains(preview.RootElement.GetProperty("rows").EnumerateArray(), row =>
            row.GetProperty("resultType").GetString() == "Error"
            && row.GetProperty("errorMessages").EnumerateArray().Any(message => message.GetString()!.Contains("휴일유형", StringComparison.Ordinal)));

        using var applyResponse = await adminClient.PostAsync("/api/admin/calendar/holidays/apply", CreateExcelMultipartContent(excelBytes), TestContext.Current.CancellationToken);
        var applyBody = await applyResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(applyResponse.StatusCode == HttpStatusCode.OK, $"Expected OK but got {applyResponse.StatusCode}. Body: {applyBody}. Logs: {context.ErrorLogs()}");
        using var apply = JsonDocument.Parse(applyBody);
        Assert.Equal(1, apply.RootElement.GetProperty("insertedCount").GetInt32());
        Assert.Equal(1, apply.RootElement.GetProperty("updatedCount").GetInt32());
        Assert.Equal(1, apply.RootElement.GetProperty("skippedCount").GetInt32());

        using var holidays = await ReadJsonAsync(await adminClient.GetAsync("/api/admin/calendar/holidays?year=2026", TestContext.Current.CancellationToken));
        var holidayItems = holidays.RootElement.GetProperty("holidays").EnumerateArray().ToList();
        Assert.Contains(holidayItems, item =>
            item.GetProperty("date").GetString() == "2026-10-03"
            && item.GetProperty("name").GetString() == "개천절"
            && item.GetProperty("note").GetString() == "기존 갱신");
        Assert.Contains(holidayItems, item =>
            item.GetProperty("date").GetString() == "2026-10-05"
            && item.GetProperty("holidayType").GetString() == "Substitute");
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
            && holiday.Source == "OfficialApi:PublicHoliday"
            && holiday.HolidayType == "Substitute");
        Assert.Contains(result.Holidays, holiday =>
            holiday.HolidayDate == new DateOnly(2026, 7, 17)
            && holiday.Name == "제헌절"
            && holiday.Source == "OfficialApi:NationalHoliday");
        Assert.DoesNotContain(result.Holidays, holiday => holiday.Name == "국군의 날");
    }

    private static async Task<Guid> CreateProjectAndReadIdAsync(ProductionPlanningApiTestContext context, HttpClient client, string code, string title)
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
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected Created but got {response.StatusCode}. Body: {body}. Logs: {context.ErrorLogs()}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("projectId").GetGuid();
    }

    private static Task InsertRoleUserAsync(
        ProductionPlanningApiTestContext context,
        Guid userId,
        string developmentUserKey,
        string displayName,
        string departmentCode,
        string roleCode)
    {
        return context.ExecuteSqlAsync($"""
            insert into qms_users (id, development_user_key, display_name, department_id, is_active)
            select '{userId}', '{developmentUserKey}', '{displayName}', departments.id, true
            from departments
            where departments.code = '{departmentCode}'
            on conflict (id) do update
            set development_user_key = excluded.development_user_key,
                display_name = excluded.display_name,
                department_id = excluded.department_id,
                is_active = true;

            insert into user_roles (user_id, role_id)
            select '{userId}', roles.id
            from roles
            where roles.code = '{roleCode}'
            on conflict (user_id, role_id) do nothing;
            """);
    }

    private static Task InsertAssigneeAsync(ProductionPlanningApiTestContext context, Guid projectId, string responsibilityType, Guid assignedUserId)
    {
        return context.ExecuteSqlAsync($"""
            insert into project_assignees (project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
            values ('{projectId}', '{responsibilityType}', '{assignedUserId}', '{DevSalesUserId}', now())
            on conflict (project_id, responsibility_type) do update
            set assigned_user_id = excluded.assigned_user_id,
                assigned_by_user_id = excluded.assigned_by_user_id,
                assigned_at_utc = excluded.assigned_at_utc;
            """);
    }

    private static Task CompleteProductionPlanningForFallbackAsync(ProductionPlanningApiTestContext context, Guid projectId, string correlationId)
    {
        return context.WorkflowStore.CompleteStageAsync(
            projectId,
            WorkflowStageCodes.ProductionPlanning,
            "Test",
            null,
            DevSalesUserId,
            correlationId,
            "fallback 순서 검증",
            TestContext.Current.CancellationToken);
    }

    private static async Task AssertGeneratedDesignWorkAsync(
        ProductionPlanningApiTestContext context,
        Guid projectId,
        Guid expectedAssignedUserId,
        string expectedAssignedRoleCode)
    {
        var assignedUserId = await ReadGeneratedWorkAssigneeIdAsync(context, projectId, WorkflowStageCodes.DesignPanelInfo);
        Assert.Equal(expectedAssignedUserId, assignedUserId);

        Assert.Equal(expectedAssignedRoleCode, await context.ReadScalarAsync<string>($"""
            select assigned_role_code
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.DesignPanelInfo}'
            order by created_at_utc desc
            limit 1;
            """));

        Assert.Equal(1, await context.ReadScalarAsync<int>($"""
            select count(*)::int
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{WorkflowStageCodes.DesignPanelInfo}';
            """));
    }

    private static Task<Guid> ReadGeneratedWorkAssigneeIdAsync(
        ProductionPlanningApiTestContext context,
        Guid projectId,
        string stageCode)
    {
        return context.ReadScalarAsync<Guid>($"""
            select assigned_user_id
            from work_items
            where project_id = '{projectId}'
              and workflow_stage_code = '{stageCode}'
            order by created_at_utc desc
            limit 1;
            """);
    }

    private static Task<bool> WorkItemExistsAsync(ProductionPlanningApiTestContext context, Guid projectId, string stageCode)
    {
        return context.ReadScalarAsync<bool>($"""
            select exists (
                select 1
                from work_items
                where project_id = '{projectId}'
                  and workflow_stage_code = '{stageCode}'
            );
            """);
    }

    private static Task<bool> HasCompletedWorkItemAsync(ProductionPlanningApiTestContext context, Guid projectId, string stageCode)
    {
        return context.ReadScalarAsync<bool>($"""
            select exists (
                select 1
                from work_items
                where project_id = '{projectId}'
                  and workflow_stage_code = '{stageCode}'
                  and status = 'Completed'
                  and started_at_utc is not null
                  and completed_at_utc is not null
            );
            """);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> CreateAdminHolidayAsync(
        HttpClient client,
        string date,
        string name,
        string holidayType,
        string? note)
    {
        var response = await client.PostAsJsonAsync(
            "/api/admin/calendar/holidays",
            new { date, name, holidayType, isActive = true, note },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected Created but got {response.StatusCode}. Body: {body}.");
        return JsonDocument.Parse(body);
    }

    private static async Task AssertCalendarHolidayTemplateAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Contains("EMI 프로젝트 통합관리시스템", worksheet.Cell(1, 1).GetString());
        Assert.Equal("날짜 *", worksheet.Cell(3, 1).GetString());
        Assert.Equal("휴일명 *", worksheet.Cell(3, 2).GetString());
        Assert.Equal("휴일유형 *", worksheet.Cell(3, 3).GetString());
        Assert.Equal("비고", worksheet.Cell(3, 4).GetString());
        Assert.Equal("National", worksheet.Cell(4, 3).GetString());
        Assert.Contains("Substitute", worksheet.Cell(2, 1).GetString());
    }

    private static MultipartFormDataContent CreateExcelMultipartContent(byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "holidays.xlsx");
        return content;
    }

    private static byte[] CreateCalendarHolidayExcel(IReadOnlyList<CalendarHolidayExcelTestRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Holidays");
        worksheet.Cell(1, 1).Value = "날짜";
        worksheet.Cell(1, 2).Value = "휴일명";
        worksheet.Cell(1, 3).Value = "휴일유형";
        worksheet.Cell(1, 4).Value = "비고";
        for (var index = 0; index < rows.Count; index += 1)
        {
            var row = rows[index];
            var rowNumber = index + 2;
            worksheet.Cell(rowNumber, 1).Value = row.Date;
            worksheet.Cell(rowNumber, 2).Value = row.Name;
            worksheet.Cell(rowNumber, 3).Value = row.HolidayType;
            worksheet.Cell(rowNumber, 4).Value = row.Note;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record CalendarHolidayExcelTestRow(
        string Date,
        string Name,
        string HolidayType,
        string? Note);

    private static async Task AssertTemplateWidthsAsync(HttpResponseMessage response, int wideColumn)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.True(worksheet.SheetView.SplitRow > 0);
        Assert.True(worksheet.AutoFilter.IsEnabled);
        Assert.Contains("필수 입력값", worksheet.Cell(1, 8).GetString());
        Assert.Equal("Item *", worksheet.Cell(1, 3).GetString());
        Assert.Equal("생산단계 *", worksheet.Cell(1, 4).GetString());
        Assert.Equal("필수 여부", worksheet.Cell(1, 5).GetString());
        Assert.Equal(XLColor.LightYellow, worksheet.Cell(1, 3).Style.Fill.BackgroundColor);
        for (var column = 1; column <= 7; column++)
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
        var headers = new[] { "프로젝트명", "PJT Code", "Item", "생산단계", "필수 여부", "예정일", "비고" };
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

    private static byte[] CreateProjectProductionPlanningExcel(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Production Plan");
        worksheet.Cell(1, 1).Value = "생산계획 입력 양식";
        worksheet.Cell(2, 1).Value = "Plan Excel Project";
        worksheet.Cell(3, 1).Value = "생산단계 *";
        worksheet.Cell(3, 2).Value = "필수 여부";
        worksheet.Cell(3, 3).Value = "예정일";
        worksheet.Cell(3, 4).Value = "비고";

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                worksheet.Cell(rowIndex + 4, columnIndex + 1).Value = rows[rowIndex][columnIndex];
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

    private static async Task<JsonDocument> PreviewProjectProductionPlanningExcelAsync(HttpClient client, Guid projectId, byte[] file, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(file), "file", fileName);
        var response = await client.PostAsync($"/api/projects/{projectId}/production-planning/import/preview", form, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonDocument> ApplyProjectProductionPlanningExcelAsync(HttpClient client, Guid projectId, byte[] file, string fileName, string fileSha256)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(file), "file", fileName);
        form.Add(new StringContent(fileSha256), "expectedFileSha256");
        var response = await client.PostAsync($"/api/projects/{projectId}/production-planning/import/apply", form, TestContext.Current.CancellationToken);
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

        public Task<T> ReadScalarAsync<T>(string sql)
        {
            return Database.ReadScalarAsync<T>(sql, TestContext.Current.CancellationToken);
        }

        public WorkflowStore WorkflowStore => Factory.Services.GetRequiredService<WorkflowStore>();

        public string ErrorLogs()
        {
            var entries = Factory.Logs.Entries
                .Where(entry => entry.LogLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                .TakeLast(5)
                .Select(entry => $"{entry.Category}: {entry.Message} {entry.Exception}");
            return string.Join(" | ", entries);
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

        public async Task<T> ReadScalarAsync<T>(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.NotNull(value);
            return (T)value;
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
