using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProjectRegistrationApiTests
{
    private static readonly Guid SalesOwnerUserId = new("50000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task SalesUser_CreatesProjectAndPanelPlaceholders()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");

        var project = await CreateProjectAsync(client, "TASK-003A-CREATE", "TASK 003A Create", 4);

        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        using var body = await ReadJsonAsync(project);
        var root = body.RootElement;
        var projectId = root.GetProperty("projectId").GetGuid();
        Assert.Equal("TASK-003A-CREATE", root.GetProperty("projectCode").GetString());
        Assert.Equal("TASK 003A Create", root.GetProperty("projectTitle").GetString());
        Assert.Equal("WoodenCrate", root.GetProperty("packagingMethod").GetString());
        Assert.Equal(4, root.GetProperty("activePanelCount").GetInt32());
        Assert.Equal(1250000.50m, root.GetProperty("salesAmount").GetDecimal());
        Assert.Equal("KRW", root.GetProperty("currencyCode").GetString());

        var panelsResponse = await client.GetAsync($"/api/projects/{projectId}/panels", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, panelsResponse.StatusCode);
        using var panels = await ReadJsonAsync(panelsResponse);
        var panelItems = panels.RootElement.EnumerateArray().ToList();
        Assert.Equal(["P01", "P02", "P03", "P04"], panelItems.Select(item => item.GetProperty("displayCode").GetString()).ToList());
        Assert.All(panelItems, panel =>
        {
            Assert.Equal("Active", panel.GetProperty("panelStatus").GetString());
            Assert.False(panel.GetProperty("panelInfoCompleted").GetBoolean());
            Assert.False(panel.GetProperty("qrEligible").GetBoolean());
            Assert.True(panel.GetProperty("panelName").ValueKind is JsonValueKind.Null);
        });
    }

    [Fact]
    public async Task CreateProject_ValidatesRequiredFieldsAndPanelCount()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");

        var missingResponse = await client.PostAsJsonAsync(
            "/api/projects",
            new { ProjectCode = "REQ-001" },
            TestContext.Current.CancellationToken);
        var invalidPanelCount = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("REQ-002", "Required Field Test") with { PanelCount = 0 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPanelCount.StatusCode);
    }

    [Fact]
    public async Task CreateProject_AllowsDuplicateProjectCodeButRejectsNormalizedProjectTitleDuplicates()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");

        var first = await CreateProjectAsync(client, "DUP-CODE", "Normalized Title", 1);
        var duplicateCode = await CreateProjectAsync(client, "DUP-CODE", "Different Title", 1);
        var exactDuplicate = await CreateProjectAsync(client, "OTHER-CODE", "Normalized Title", 1);
        var normalizedDuplicate = await CreateProjectAsync(client, "OTHER-CODE-2", " normalized   title ", 1);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicateCode.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, exactDuplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, normalizedDuplicate.StatusCode);
    }

    [Theory]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-admin")]
    public async Task CreateProject_AllowsOnlySales(string developmentUserKey)
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient(developmentUserKey);

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest($"NO-WRITE-{developmentUserKey}", $"No Write {developmentUserKey}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("dev-sales", true)]
    [InlineData("dev-admin", true)]
    [InlineData("dev-production", false)]
    [InlineData("dev-manufacturing", false)]
    [InlineData("dev-quality", false)]
    [InlineData("dev-logistics", false)]
    [InlineData("dev-viewer", false)]
    public async Task ProjectResponses_FilterSalesAmountByPermission(string developmentUserKey, bool expectSensitiveFields)
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var readClient = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"SENSITIVE-{developmentUserKey}", $"Sensitive {developmentUserKey}", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var listResponse = await readClient.GetAsync("/api/projects?search=Sensitive", TestContext.Current.CancellationToken);
        var detailResponse = await readClient.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var listJson = await ReadJsonAsync(listResponse);
        var listItem = listJson.RootElement.GetProperty("items").EnumerateArray().First(item =>
            item.GetProperty("projectId").GetGuid() == projectId);
        using var detailJson = await ReadJsonAsync(detailResponse);

        Assert.Equal(expectSensitiveFields, listItem.TryGetProperty("salesAmount", out _));
        Assert.Equal(expectSensitiveFields, listItem.TryGetProperty("currencyCode", out _));
        Assert.Equal(expectSensitiveFields, detailJson.RootElement.TryGetProperty("salesAmount", out _));
        Assert.Equal(expectSensitiveFields, detailJson.RootElement.TryGetProperty("currencyCode", out _));
    }

    [Fact]
    public async Task AuditHistory_HidesSensitiveSalesAmountChangesFromUnauthorizedRoles()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        using var created = await CreateProjectAsync(salesClient, "AUDIT-001", "Audit Sensitive", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var updateResponse = await salesClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}",
            new
            {
                CustomerName = "Audit Customer",
                Item = "Audit Item",
                ProjectCode = "AUDIT-001",
                ProjectTitle = "Audit Sensitive",
                DeliveryDate = "2026-10-10",
                SalesOwnerUserId,
                PackagingMethod = "WoodenCrate",
                SalesAmount = 777m,
                CurrencyCode = "KRW",
                DeliveryLocation = "Audit Dock",
                Reason = "판매금액 정정"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var salesHistory = await salesClient.GetAsync($"/api/projects/{projectId}/audit-history", TestContext.Current.CancellationToken);
        var manufacturingHistory = await manufacturingClient.GetAsync($"/api/projects/{projectId}/audit-history", TestContext.Current.CancellationToken);

        using var salesJson = await ReadJsonAsync(salesHistory);
        using var manufacturingJson = await ReadJsonAsync(manufacturingHistory);

        Assert.Contains(salesJson.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.TryGetProperty("fieldName", out var fieldName) && fieldName.GetString() == "SalesAmount");
        Assert.DoesNotContain(manufacturingJson.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.TryGetProperty("fieldName", out var fieldName) && fieldName.GetString() == "SalesAmount");
    }

    [Fact]
    public async Task CreateAndUpdateProject_ValidatePackagingMethodAndAuditChanges()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");

        var missing = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("PACK-MISSING", "Packaging Missing") with { PackagingMethod = null },
            TestContext.Current.CancellationToken);
        var invalid = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("PACK-BAD", "Packaging Bad") with { PackagingMethod = "LooseBox" },
            TestContext.Current.CancellationToken);
        var wooden = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("PACK-WOOD", "Packaging Wood") with { PackagingMethod = "WoodenCrate" },
            TestContext.Current.CancellationToken);
        var wrap = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("PACK-WRAP", "Packaging Wrap") with { PackagingMethod = "StretchWrap" },
            TestContext.Current.CancellationToken);
        var box = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("PACK-BOX", "Packaging Box") with { PackagingMethod = "HeavyDutyBox" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, wooden.StatusCode);
        Assert.Equal(HttpStatusCode.Created, wrap.StatusCode);
        Assert.Equal(HttpStatusCode.Created, box.StatusCode);

        using var createdJson = await ReadJsonAsync(wooden);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var updateByManufacturing = await manufacturingClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}",
            NewUpdateProjectRequest("PACK-WOOD", "Packaging Wood") with { PackagingMethod = "StretchWrap" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, updateByManufacturing.StatusCode);

        var update = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}",
            NewUpdateProjectRequest("PACK-WOOD", "Packaging Wood") with { PackagingMethod = "StretchWrap" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(1, await CountAuditFieldChangesAsync(client, projectId, "PackagingMethod"));
    }

    [Fact]
    public async Task ExistingPackagingNullProject_CanBeReadButRequiresPackagingOnGeneralUpdate()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");

        await context.ExecuteSqlAsync("""
            insert into projects (
                id,
                project_key,
                project_number,
                name,
                customer_name,
                item,
                project_code,
                project_title,
                project_title_normalized,
                delivery_date,
                sales_owner_user_id,
                status
            )
            values (
                '73000000-0000-0000-0000-000000000001',
                'legacy-null-packaging',
                'LEGACY-PACK',
                'Legacy Packaging Null',
                'Legacy Customer',
                'Legacy Item',
                'LEGACY-PACK',
                'Legacy Packaging Null',
                'LEGACY PACKAGING NULL',
                '2026-10-10',
                '50000000-0000-0000-0000-000000000002',
                'Active'
            );
            """);

        var detail = await client.GetAsync(
            "/api/projects/73000000-0000-0000-0000-000000000001",
            TestContext.Current.CancellationToken);
        var updateWithoutPackaging = await client.PatchAsJsonAsync(
            "/api/projects/73000000-0000-0000-0000-000000000001",
            NewUpdateProjectRequest("LEGACY-PACK", "Legacy Packaging Null") with { PackagingMethod = null },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var json = await ReadJsonAsync(detail);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("packagingMethod").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, updateWithoutPackaging.StatusCode);
    }

    [Fact]
    public async Task ChangePanelCount_IncreasesCancelsAndDoesNotReuseCancelledNumbers()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "PANEL-001", "Panel Count Change", 3);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var increaseResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(5, 3, [], "면수 증가"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, increaseResponse.StatusCode);

        var panelsAfterIncrease = await ReadPanelsAsync(client, projectId);
        Assert.Equal(["P01", "P02", "P03", "P04", "P05"], panelsAfterIncrease.Select(panel => panel.DisplayCode).ToList());

        var panelToCancel = panelsAfterIncrease.Single(panel => panel.DisplayCode == "P02").PanelId;
        var decreaseMismatch = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(3, 5, [panelToCancel], "선택 수 부족"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, decreaseMismatch.StatusCode);

        var decreaseResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(4, 5, [panelToCancel], "면수 감소"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, decreaseResponse.StatusCode);

        var increaseAgain = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(5, 4, [], "면수 재증가"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, increaseAgain.StatusCode);

        var panels = await ReadPanelsAsync(client, projectId);
        Assert.Contains(panels, panel => panel.DisplayCode == "P02" && panel.PanelStatus == "Cancelled");
        Assert.Contains(panels, panel => panel.DisplayCode == "P06" && panel.PanelStatus == "Active");
    }

    [Fact]
    public async Task ChangePanelCount_RejectsConcurrentRequestsWithStaleExpectedPanelCount()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var firstClient = context.CreateClient("dev-sales");
        using var secondClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "CONCURRENT-PANEL", "Concurrent Panel Count", 4);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var first = firstClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(6, 4, [], "동시 증가 A"),
            TestContext.Current.CancellationToken);
        var second = secondClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(7, 4, [], "동시 증가 B"),
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(first, second);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var panels = await ReadPanelsAsync(createClient, projectId);
        var activePanels = panels.Where(panel => panel.PanelStatus == "Active").ToList();
        Assert.True(activePanels.Count is 6 or 7);
        Assert.Equal(activePanels.Count, activePanels.Select(panel => panel.DisplayCode).Distinct().Count());
        Assert.Equal(1, await CountAuditActionsAsync(createClient, projectId, "PanelCountIncreased"));

        var retry = await createClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(activePanels.Count + 1, activePanels.Count, [], "최신 기준 재요청"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task ChangePanelCount_DefensiveUniqueViolationReturnsConflict()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "UNIQUE-PANEL", "Unique Panel Defense", 4);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        await context.ExecuteSqlAsync("""
            create or replace function force_duplicate_panel_placeholder()
            returns trigger
            language plpgsql
            as $$
            begin
                if new.display_code = 'P05' and pg_trigger_depth() = 1 then
                    insert into panel_placeholders (
                        id,
                        project_id,
                        sequence_number,
                        display_code,
                        status,
                        panel_info_completed,
                        qr_eligible
                    )
                    values (
                        uuid_generate_v4(),
                        new.project_id,
                        new.sequence_number,
                        new.display_code,
                        'Active',
                        false,
                        false
                    );
                end if;

                return new;
            end;
            $$;

            create trigger force_duplicate_panel_placeholder_trigger
            before insert on panel_placeholders
            for each row
            execute function force_duplicate_panel_placeholder();
            """);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(5, 4, [], "UniqueViolation 방어"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangePanelCount_DifferentProjectsCanChangeConcurrently()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var firstClient = context.CreateClient("dev-sales");
        using var secondClient = context.CreateClient("dev-sales");
        using var firstCreated = await CreateProjectAsync(firstClient, "PARALLEL-A", "Parallel Project A", 4);
        using var secondCreated = await CreateProjectAsync(secondClient, "PARALLEL-B", "Parallel Project B", 4);
        using var firstJson = await ReadJsonAsync(firstCreated);
        using var secondJson = await ReadJsonAsync(secondCreated);
        var firstProjectId = firstJson.RootElement.GetProperty("projectId").GetGuid();
        var secondProjectId = secondJson.RootElement.GetProperty("projectId").GetGuid();

        var first = firstClient.PostAsJsonAsync(
            $"/api/projects/{firstProjectId}/change-panel-count",
            NewPanelCountRequest(5, 4, [], "프로젝트 A 증가"),
            TestContext.Current.CancellationToken);
        var second = secondClient.PostAsJsonAsync(
            $"/api/projects/{secondProjectId}/change-panel-count",
            NewPanelCountRequest(5, 4, [], "프로젝트 B 증가"),
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task CreateAndChangePanelCount_EnforceMaxActivePanels()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");

        Assert.Equal(HttpStatusCode.Created, (await CreateProjectAsync(client, "MAX-001", "Max One", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateProjectAsync(client, "MAX-500", "Max Five Hundred", 500)).StatusCode);

        var zero = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("MAX-000", "Max Zero") with { PanelCount = 0 },
            TestContext.Current.CancellationToken);
        var overMax = await client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest("MAX-501", "Max Five Hundred One") with { PanelCount = 501 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overMax.StatusCode);

        using var created = await CreateProjectAsync(client, "MAX-CHANGE", "Max Change", 499);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var toMax = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(500, 499, [], "500면까지 증가"),
            TestContext.Current.CancellationToken);
        var beyondMax = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(501, 500, [], "501면 증가 시도"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, toMax.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, beyondMax.StatusCode);

        var panelsAtMax = await ReadPanelsAsync(client, projectId);
        var panelToCancel = panelsAtMax.First(panel => panel.PanelStatus == "Active").PanelId;
        var decrease = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(499, 500, [panelToCancel], "활성 면수 감소"),
            TestContext.Current.CancellationToken);
        var increaseAfterCancel = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(500, 499, [], "취소 후 500면 복귀"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, decrease.StatusCode);
        Assert.Equal(HttpStatusCode.OK, increaseAfterCancel.StatusCode);

        var panels = await ReadPanelsAsync(client, projectId);
        Assert.Equal(500, panels.Count(panel => panel.PanelStatus == "Active"));
        Assert.Contains(panels, panel => panel.DisplayCode == "P501" && panel.PanelStatus == "Active");
    }

    [Fact]
    public async Task ProjectStatusTransitions_RequireReasonsAndEnforceAllowedSources()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "STATUS-001", "Status Transitions", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var missingReason = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "" },
            TestContext.Current.CancellationToken);
        var hold = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "고객 확인 대기" },
            TestContext.Current.CancellationToken);
        var invalidHoldAgain = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "중복 보류" },
            TestContext.Current.CancellationToken);
        var resume = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/resume",
            new { Reason = "확인 완료" },
            TestContext.Current.CancellationToken);
        var cancel = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/cancel",
            new { Reason = "수주 취소" },
            TestContext.Current.CancellationToken);
        var panelChangeWhileCancelled = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(2, 1, [], "취소 상태 변경"),
            TestContext.Current.CancellationToken);
        var reactivate = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/reactivate",
            new { Reason = "수주 재개" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hold.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, invalidHoldAgain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, panelChangeWhileCancelled.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
    }

    [Fact]
    public async Task ProjectStatusTransitions_UseLockedLatestStatusForConcurrentHoldAndCancel()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var holdClient = context.CreateClient("dev-sales");
        using var cancelClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "STATUS-CONCURRENT", "Status Concurrent", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var hold = holdClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "동시 보류" },
            TestContext.Current.CancellationToken);
        var cancel = cancelClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/cancel",
            new { Reason = "동시 취소" },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(hold, cancel);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Equal("Cancelled", await ReadProjectStatusAsync(createClient, projectId));
        Assert.Equal(1, await CountAuditActionsAsync(createClient, projectId, "ProjectCancelled"));
    }

    [Fact]
    public async Task ProjectStatusTransitions_ConcurrentDuplicateHoldCreatesOneAuditEvent()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var firstClient = context.CreateClient("dev-sales");
        using var secondClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "STATUS-HOLD-TWICE", "Status Hold Twice", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var first = firstClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "첫 번째 보류" },
            TestContext.Current.CancellationToken);
        var second = secondClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "두 번째 보류" },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(first, second);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("OnHold", await ReadProjectStatusAsync(createClient, projectId));
        Assert.Equal(1, await CountAuditActionsAsync(createClient, projectId, "ProjectHeld"));
    }

    [Fact]
    public async Task ProjectStatusTransitions_ConcurrentReactivateAndHoldDoNotCreateInvalidTransition()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var reactivateClient = context.CreateClient("dev-sales");
        using var holdClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "STATUS-REACTIVATE-HOLD", "Status Reactivate Hold", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var cancel = await createClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/cancel",
            new { Reason = "초기 취소" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var reactivate = reactivateClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/reactivate",
            new { Reason = "동시 재활성" },
            TestContext.Current.CancellationToken);
        var hold = holdClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "동시 보류" },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(reactivate, hold);

        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.True((await ReadProjectStatusAsync(createClient, projectId)) is "Active" or "OnHold");
        Assert.Equal(1, await CountAuditActionsAsync(createClient, projectId, "ProjectReactivated"));
    }

    [Fact]
    public async Task CancelledProjects_AreShownInCancelledListButDeletedCancelledProjectsAreExcluded()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        using var created = await CreateProjectAsync(salesClient, "CANCEL-LIST", "Cancelled List Project", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var cancel = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/cancel",
            new { Reason = "취소 목록 검증" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var cancelledList = await manufacturingClient.GetAsync("/api/projects?status=Cancelled", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancelledList.StatusCode);
        using (var json = await ReadJsonAsync(cancelledList))
        {
            Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("projectId").GetGuid() == projectId);
        }

        var delete = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "오등록 정리", ConfirmProjectTitle = " cancelled   list project " },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var afterDelete = await manufacturingClient.GetAsync("/api/projects?status=Cancelled", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode);
        using var afterJson = await ReadJsonAsync(afterDelete);
        Assert.DoesNotContain(afterJson.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("projectId").GetGuid() == projectId);
    }

    [Theory]
    [InlineData("dev-admin")]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-viewer")]
    public async Task DeleteProject_AllowsOnlySales(string developmentUserKey)
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var client = context.CreateClient(developmentUserKey);
        using var created = await CreateProjectAsync(salesClient, $"DELETE-FORBID-{developmentUserKey}", $"Delete Forbidden {developmentUserKey}", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "권한 없음", ConfirmProjectTitle = $"Delete Forbidden {developmentUserKey}" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_SoftDeletesKeepsPanelsAndAllowsTitleReuse()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var adminClient = context.CreateClient("dev-admin");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        using var created = await CreateProjectAsync(salesClient, "DELETE-001", "Delete Reusable Title", 2);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var missingReason = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "", ConfirmProjectTitle = "Delete Reusable Title" },
            TestContext.Current.CancellationToken);
        var mismatchTitle = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "오등록", ConfirmProjectTitle = "Wrong Title" },
            TestContext.Current.CancellationToken);
        var delete = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "오등록 프로젝트 정리", ConfirmProjectTitle = " delete   reusable   title " },
            TestContext.Current.CancellationToken);
        var duplicateDelete = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "중복 삭제", ConfirmProjectTitle = "Delete Reusable Title" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchTitle.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.True(duplicateDelete.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict);

        Assert.Equal(HttpStatusCode.NotFound, (await salesClient.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await salesClient.GetAsync($"/api/projects/{projectId}/panels", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manufacturingClient.GetAsync("/api/deleted-projects", TestContext.Current.CancellationToken)).StatusCode);

        var deletedBySales = await salesClient.GetAsync("/api/deleted-projects?search=Reusable", TestContext.Current.CancellationToken);
        var deletedByAdmin = await adminClient.GetAsync($"/api/deleted-projects/{projectId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deletedBySales.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deletedByAdmin.StatusCode);

        using (var deletedJson = await ReadJsonAsync(deletedByAdmin))
        {
            Assert.Equal("오등록 프로젝트 정리", deletedJson.RootElement.GetProperty("deleteReason").GetString());
            Assert.Equal(2, deletedJson.RootElement.GetProperty("panels").GetArrayLength());
            Assert.Contains(deletedJson.RootElement.GetProperty("auditHistory").EnumerateArray(), item =>
                item.GetProperty("action").GetString() == "ProjectDeleted");
        }

        var updateDeleted = await salesClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}",
            NewUpdateProjectRequest("DELETE-001", "Delete Reusable Title"),
            TestContext.Current.CancellationToken);
        var changePanelDeleted = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(3, 2, [], "삭제 후 면수변경"),
            TestContext.Current.CancellationToken);
        var holdDeleted = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/hold",
            new { Reason = "삭제 후 보류" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, updateDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changePanelDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, holdDeleted.StatusCode);

        var reuse = await CreateProjectAsync(salesClient, "DELETE-REUSE", "Delete Reusable Title", 1);
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_AllowedGuardRunsInsideDeletionTransactionAndUsesDatabaseTimestamp()
    {
        var guard = new RecordingDeletionGuard(ProjectDeletionGuardResult.Allowed());
        await using var context = await ProjectApiTestContext.CreateAsync(services =>
            services.AddSingleton<IProjectDeletionGuard>(guard));
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "DELETE-GUARD-ALLOW", "Delete Guard Allow", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var delete = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "Guard 허용", ConfirmProjectTitle = "Delete Guard Allow" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.True(guard.WasCalled);
        Assert.True(guard.ReceivedOpenConnection);
        Assert.True(guard.ReceivedOpenTransaction);
        Assert.Equal(projectId, guard.ObservedProjectId);
        var snapshot = await context.ReadDeletionSnapshotAsync(projectId);
        var auditDeletedAt = await context.ReadDeletedAtAuditValueAsync(projectId);
        using var deleteJson = await ReadJsonAsync(delete);
        var responseDeletedAt = deleteJson.RootElement.GetProperty("deletedAtUtc").GetDateTimeOffset();

        Assert.NotNull(snapshot.DeletedAtUtc);
        Assert.Equal(snapshot.DeletedAtUtc.Value.ToUniversalTime(), auditDeletedAt!.Value.ToUniversalTime());
        Assert.Equal(snapshot.DeletedAtUtc.Value.ToUniversalTime(), responseDeletedAt.ToUniversalTime());
        Assert.Equal(1, await context.CountAuditActionAsync(projectId, "ProjectDeleted"));
    }

    [Fact]
    public async Task DeleteProject_BlockedGuardRollsBackAndKeepsProjectData()
    {
        await using var context = await ProjectApiTestContext.CreateAsync(services =>
            services.AddSingleton<IProjectDeletionGuard>(
                new RecordingDeletionGuard(ProjectDeletionGuardResult.Blocked("test-blocked"))));
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "DELETE-GUARD-BLOCK", "Delete Guard Block", 2);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var delete = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "Guard 차단", ConfirmProjectTitle = "Delete Guard Block" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var snapshot = await context.ReadDeletionSnapshotAsync(projectId);
        Assert.Null(snapshot.DeletedAtUtc);
        Assert.Null(snapshot.DeletedByUserId);
        Assert.Null(snapshot.DeleteReason);
        Assert.Equal(2, snapshot.ActivePanelCount);
        Assert.Equal(2, snapshot.TotalPanelCount);
        Assert.Equal(0, await context.CountAuditActionAsync(projectId, "ProjectDeleted"));
    }

    [Fact]
    public async Task DeleteProject_GuardExceptionDoesNotCommitDeletionMetadataOrAudit()
    {
        await using var context = await ProjectApiTestContext.CreateAsync(services =>
            services.AddSingleton<IProjectDeletionGuard>(new ThrowingDeletionGuard()));
        using var client = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(client, "DELETE-GUARD-THROW", "Delete Guard Throw", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var exception = await Record.ExceptionAsync(async () =>
        {
            using var delete = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/delete",
                new { Reason = "Guard 예외", ConfirmProjectTitle = "Delete Guard Throw" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            var body = await delete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("test guard failure", body, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Null(exception);
        var snapshot = await context.ReadDeletionSnapshotAsync(projectId);
        Assert.Null(snapshot.DeletedAtUtc);
        Assert.Null(snapshot.DeletedByUserId);
        Assert.Null(snapshot.DeleteReason);
        Assert.Equal(0, await context.CountAuditActionAsync(projectId, "ProjectDeleted"));
    }

    [Fact]
    public async Task DeleteProject_BlocksCompletedAndNonDeletedTitleDuplicatesRemainBlocked()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-sales");
        using var cancelled = await CreateProjectAsync(client, "TITLE-CANCEL", "Title Still Reserved Cancelled", 1);
        using var completed = await CreateProjectAsync(client, "TITLE-COMPLETE", "Title Still Reserved Completed", 1);
        using var cancelledJson = await ReadJsonAsync(cancelled);
        using var completedJson = await ReadJsonAsync(completed);
        var cancelledId = cancelledJson.RootElement.GetProperty("projectId").GetGuid();
        var completedId = completedJson.RootElement.GetProperty("projectId").GetGuid();

        await context.ExecuteSqlAsync($"""
            update projects
            set status = 'Completed'
            where id = '{completedId}';
            """);

        var cancel = await client.PostAsJsonAsync(
            $"/api/projects/{cancelledId}/cancel",
            new { Reason = "취소 title 예약" },
            TestContext.Current.CancellationToken);
        var deleteCompleted = await client.PostAsJsonAsync(
            $"/api/projects/{completedId}/delete",
            new { Reason = "완료 삭제", ConfirmProjectTitle = "Title Still Reserved Completed" },
            TestContext.Current.CancellationToken);
        var duplicateCancelled = await CreateProjectAsync(client, "TITLE-CANCEL-DUP", " title still   reserved cancelled ", 1);
        var duplicateCompleted = await CreateProjectAsync(client, "TITLE-COMP-DUP", "title still reserved completed", 1);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteCompleted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCompleted.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_ConcurrentDeleteAndUpdateCommitsOneConsistentResult()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var deleteClient = context.CreateClient("dev-sales");
        using var updateClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "DELETE-CONCURRENT", "Delete Concurrent", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var delete = deleteClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "동시 삭제", ConfirmProjectTitle = "Delete Concurrent" },
            TestContext.Current.CancellationToken);
        var update = updateClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}",
            NewUpdateProjectRequest("DELETE-CONCURRENT", "Delete Concurrent Updated"),
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(delete, update);

        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.True(
            (await createClient.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken)).StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_ConcurrentDeleteAndCancelDoesNotCreateLostUpdate()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var deleteClient = context.CreateClient("dev-sales");
        using var cancelClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "DELETE-CANCEL", "Delete Cancel Concurrent", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var delete = deleteClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "동시 삭제", ConfirmProjectTitle = "Delete Cancel Concurrent" },
            TestContext.Current.CancellationToken);
        var cancel = cancelClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/cancel",
            new { Reason = "동시 취소" },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(delete, cancel);

        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);

        var normalDetailStatus = (await createClient.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken)).StatusCode;
        if (normalDetailStatus == HttpStatusCode.NotFound)
        {
            var deletedDetail = await createClient.GetAsync($"/api/deleted-projects/{projectId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, deletedDetail.StatusCode);
        }
        else
        {
            Assert.Equal("Cancelled", await ReadProjectStatusAsync(createClient, projectId));
        }
    }

    [Fact]
    public async Task DeleteProject_ConcurrentDeleteAndPanelIncreaseCommitsOneConsistentResult()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var deleteClient = context.CreateClient("dev-sales");
        using var panelClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "DELETE-PANEL", "Delete Panel Concurrent", 4);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var delete = deleteClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "동시 삭제", ConfirmProjectTitle = "Delete Panel Concurrent" },
            TestContext.Current.CancellationToken);
        var panelIncrease = panelClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/change-panel-count",
            NewPanelCountRequest(6, 4, [], "동시 면수 증가"),
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(delete, panelIncrease);
        var deleteResponse = responses[0];
        var panelResponse = responses[1];

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.True(panelResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Conflict);
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var snapshot = await context.ReadDeletionSnapshotAsync(projectId);
        Assert.NotNull(snapshot.DeletedAtUtc);
        Assert.Equal(SalesOwnerUserId, snapshot.DeletedByUserId);
        Assert.Equal("동시 삭제", snapshot.DeleteReason);
        Assert.Equal(0, snapshot.DuplicateSequenceCount);
        Assert.Equal(1, await context.CountAuditActionAsync(projectId, "ProjectDeleted"));

        var panelIncreaseAuditCount = await context.CountAuditActionAsync(projectId, "PanelCountIncreased");
        if (panelIncreaseAuditCount == 1)
        {
            Assert.Equal(6, snapshot.ActivePanelCount);
            Assert.Equal(6, snapshot.TotalPanelCount);
            Assert.Equal(6, await context.CountAuditActionAsync(projectId, "PanelCreated"));
        }
        else
        {
            Assert.Equal(4, snapshot.ActivePanelCount);
            Assert.Equal(4, snapshot.TotalPanelCount);
            Assert.Equal(0, panelIncreaseAuditCount);
            Assert.Equal(4, await context.CountAuditActionAsync(projectId, "PanelCreated"));
        }
    }

    [Fact]
    public async Task DeleteProject_ConcurrentDuplicateDeleteCreatesOneDeletedAuditSet()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var createClient = context.CreateClient("dev-sales");
        using var firstClient = context.CreateClient("dev-sales");
        using var secondClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(createClient, "DELETE-TWICE", "Delete Twice Concurrent", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();

        var first = firstClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "첫 번째 삭제", ConfirmProjectTitle = "Delete Twice Concurrent" },
            TestContext.Current.CancellationToken);
        var second = secondClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/delete",
            new { Reason = "두 번째 삭제", ConfirmProjectTitle = "Delete Twice Concurrent" },
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound));
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var snapshot = await context.ReadDeletionSnapshotAsync(projectId);
        Assert.NotNull(snapshot.DeletedAtUtc);
        Assert.Equal(SalesOwnerUserId, snapshot.DeletedByUserId);
        Assert.True(snapshot.DeleteReason is "첫 번째 삭제" or "두 번째 삭제");
        Assert.Equal(1, await context.CountAuditActionAsync(projectId, "ProjectDeleted"));
        Assert.Equal(3, await context.CountAuditActionAsync(projectId, "ProjectDeletedSnapshot"));
    }

    [Theory]
    [InlineData("dev-admin")]
    [InlineData("dev-sales")]
    [InlineData("dev-production")]
    [InlineData("dev-manufacturing")]
    [InlineData("dev-quality")]
    [InlineData("dev-logistics")]
    [InlineData("dev-viewer")]
    public async Task EveryActiveInternalRole_CanListAndReadProjectDetails(string developmentUserKey)
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var created = await CreateProjectAsync(salesClient, $"READ-{developmentUserKey}", $"Read {developmentUserKey}", 1);
        using var createdJson = await ReadJsonAsync(created);
        var projectId = createdJson.RootElement.GetProperty("projectId").GetGuid();
        using var client = context.CreateClient(developmentUserKey);

        var list = await client.GetAsync("/api/projects", TestContext.Current.CancellationToken);
        var detail = await client.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task InactiveUser_CannotReadProjects()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var client = context.CreateClient("dev-disabled");

        var response = await client.GetAsync("/api/projects", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateProjectAsync(
        HttpClient client,
        string projectCode,
        string projectTitle,
        int panelCount)
    {
        return client.PostAsJsonAsync(
            "/api/projects",
            NewProjectRequest(projectCode, projectTitle) with { PanelCount = panelCount },
            TestContext.Current.CancellationToken);
    }

    private static CreateProjectPayload NewProjectRequest(string projectCode, string projectTitle)
    {
        return new CreateProjectPayload(
            "EMI Demo Customer",
            "Control Panel",
            projectCode,
            projectTitle,
            2,
            "2026-10-10",
            SalesOwnerUserId,
            "WoodenCrate",
            1250000.50m,
            "KRW",
            "Dock A");
    }

    private static UpdateProjectPayload NewUpdateProjectRequest(string projectCode, string projectTitle)
    {
        return new UpdateProjectPayload(
            "EMI Demo Customer",
            "Control Panel",
            projectCode,
            projectTitle,
            "2026-10-10",
            SalesOwnerUserId,
            "WoodenCrate",
            1250000.50m,
            "KRW",
            "Dock A",
            "기본정보 수정");
    }

    private static object NewPanelCountRequest(
        int panelCount,
        int expectedActivePanelCount,
        IReadOnlyList<Guid> cancelPanelIds,
        string reason)
    {
        return new
        {
            PanelCount = panelCount,
            ExpectedActivePanelCount = expectedActivePanelCount,
            CancelPanelIds = cancelPanelIds,
            Reason = reason
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<PanelSnapshot>> ReadPanelsAsync(HttpClient client, Guid projectId)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}/panels", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.EnumerateArray()
            .Select(panel => new PanelSnapshot(
                panel.GetProperty("panelId").GetGuid(),
                panel.GetProperty("displayCode").GetString() ?? "",
                panel.GetProperty("panelStatus").GetString() ?? ""))
            .ToList();
    }

    private static async Task<string> ReadProjectStatusAsync(HttpClient client, Guid projectId)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("status").GetString() ?? "";
    }

    private static async Task<int> CountAuditActionsAsync(HttpClient client, Guid projectId, string action)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}/audit-history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Count(item => item.GetProperty("action").GetString() == action);
    }

    private static async Task<int> CountAuditFieldChangesAsync(HttpClient client, Guid projectId, string fieldName)
    {
        var response = await client.GetAsync($"/api/projects/{projectId}/audit-history", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Count(item =>
                item.GetProperty("action").GetString() == "ProjectFieldUpdated"
                && item.TryGetProperty("fieldName", out var field)
                && field.GetString() == fieldName);
    }

    private sealed record CreateProjectPayload(
        string CustomerName,
        string Item,
        string ProjectCode,
        string ProjectTitle,
        int PanelCount,
        string DeliveryDate,
        Guid SalesOwnerUserId,
        string? PackagingMethod,
        decimal? SalesAmount,
        string? CurrencyCode,
        string? DeliveryLocation);

    private sealed record UpdateProjectPayload(
        string CustomerName,
        string Item,
        string ProjectCode,
        string ProjectTitle,
        string DeliveryDate,
        Guid SalesOwnerUserId,
        string? PackagingMethod,
        decimal? SalesAmount,
        string? CurrencyCode,
        string? DeliveryLocation,
        string Reason);

    private sealed record PanelSnapshot(Guid PanelId, string DisplayCode, string PanelStatus);

    private sealed record ProjectDeletionSnapshot(
        DateTimeOffset? DeletedAtUtc,
        Guid? DeletedByUserId,
        string? DeleteReason,
        int ActivePanelCount,
        int TotalPanelCount,
        int DuplicateSequenceCount);

    private sealed class RecordingDeletionGuard(ProjectDeletionGuardResult result) : IProjectDeletionGuard
    {
        public bool WasCalled { get; private set; }
        public bool ReceivedOpenConnection { get; private set; }
        public bool ReceivedOpenTransaction { get; private set; }
        public Guid ObservedProjectId { get; private set; }

        public async Task<ProjectDeletionGuardResult> CanDeleteAsync(
            ProjectDeletionContext context,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ObservedProjectId = context.ProjectId;
            ReceivedOpenConnection = context.Connection.State == ConnectionState.Open;
            ReceivedOpenTransaction = context.Transaction.Connection == context.Connection;

            await using var command = context.Connection.CreateCommand();
            command.Transaction = context.Transaction;
            command.CommandText = """
                select count(*)::integer
                from projects
                where id = @project_id
                  and deleted_at_utc is null;
                """;
            command.Parameters.AddWithValue("project_id", context.ProjectId);
            Assert.Equal(1, await command.ExecuteScalarAsync(cancellationToken));

            return result;
        }
    }

    private sealed class ThrowingDeletionGuard : IProjectDeletionGuard
    {
        public Task<ProjectDeletionGuardResult> CanDeleteAsync(
            ProjectDeletionContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("test guard failure");
        }
    }

    private sealed class ProjectApiTestContext : IAsyncDisposable
    {
        private ProjectApiTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public static async Task<ProjectApiTestContext> CreateAsync(Action<IServiceCollection>? configureTestServices = null)
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

            return new ProjectApiTestContext(database, factory);
        }

        public HttpClient CreateClient(string developmentUserKey)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, developmentUserKey);
            return client;
        }

        public async Task ExecuteSqlAsync(string commandText)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand(commandText);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<ProjectDeletionSnapshot> ReadDeletionSnapshotAsync(Guid projectId)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand("""
                select deleted_at_utc,
                       deleted_by_user_id,
                       delete_reason,
                       (
                           select count(*)
                           from panel_placeholders
                           where project_id = @project_id
                             and status = 'Active'
                       )::integer,
                       (
                           select count(*)
                           from panel_placeholders
                           where project_id = @project_id
                       )::integer,
                       (
                           select count(*)
                           from (
                               select sequence_number
                               from panel_placeholders
                               where project_id = @project_id
                               group by sequence_number
                               having count(*) > 1
                           ) duplicates
                       )::integer
                from projects
                where id = @project_id;
                """);
            command.Parameters.AddWithValue("project_id", projectId);

            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            return new ProjectDeletionSnapshot(
                reader.IsDBNull(0) ? null : reader.GetFieldValue<DateTimeOffset>(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5));
        }

        public async Task<int> CountAuditActionAsync(Guid projectId, string action)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand("""
                select count(*)::integer
                from project_audit_events
                where project_id = @project_id
                  and action = @action;
                """);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("action", action);
            return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0);
        }

        public async Task<DateTimeOffset?> ReadDeletedAtAuditValueAsync(Guid projectId)
        {
            await using var dataSource = NpgsqlDataSource.Create(Database.ConnectionString);
            await using var command = dataSource.CreateCommand("""
                select new_value
                from project_audit_events
                where project_id = @project_id
                  and action = 'ProjectDeleted'
                  and field_name = 'DeletedAtUtc'
                order by changed_at_utc desc
                limit 1;
                """);
            command.Parameters.AddWithValue("project_id", projectId);
            var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return value is string text && DateTimeOffset.TryParse(text, out var parsed)
                ? parsed
                : null;
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

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .AddEnvironmentVariables()
                .Build();
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
