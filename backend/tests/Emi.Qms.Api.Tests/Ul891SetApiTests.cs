using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Ul891Sets;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class ProjectRegistrationApiTests
{
    [Fact]
    public async Task Ul891MonthlyBilling_UsesShipmentCalendarMonth_AndKeepsConfirmedRevisionAppendOnly()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        var projectId = await CreateUl891ProjectAsync(salesClient, context, "MONTHLY", 1_000_000m, new[] { "A", "B" });
        var panelIds = new[]
        {
            await context.ReadScalarAsync<Guid>($"select id from panel_placeholders where project_id='{projectId}' order by sequence_number limit 1;"),
            await context.ReadScalarAsync<Guid>($"select id from panel_placeholders where project_id='{projectId}' order by sequence_number offset 1 limit 1;")
        };
        using var emptyMonthResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/monthly-billing/open", new
        {
            operationId = Guid.NewGuid(), billingMonth = new DateOnly(2026, 8, 1), recoveryCaseIds = Array.Empty<Guid>()
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(emptyMonthResponse, HttpStatusCode.BadRequest, context);
        var packingId = Guid.NewGuid();
        var departureId = Guid.NewGuid();
        await context.ExecuteSqlAsync($"""
            insert into logistics_packing_units(id,project_id,unit_number,status,version,note,created_by_user_id)
            values ('{packingId}','{projectId}',1,'Draft',1,'월별 원장 검증','{SalesOwnerUserId}');
            insert into logistics_packing_unit_panels(packing_unit_id,panel_id,active,added_by_user_id)
            values ('{packingId}','{panelIds[0]}',true,'{SalesOwnerUserId}'),('{packingId}','{panelIds[1]}',true,'{SalesOwnerUserId}');
            update logistics_packing_units set status='Finalized',finalized_by_user_id='{SalesOwnerUserId}',finalized_at_utc=now() where id='{packingId}';
            insert into logistics_batches(id,project_id,stage_code,batch_number,status,version,departure_date,created_by_user_id)
            values ('{departureId}','{projectId}','DepartureProcessed',1,'Draft',1,date '2026-07-20','{SalesOwnerUserId}');
            insert into logistics_batch_units(batch_id,packing_unit_id,stage_code,active,added_by_user_id)
            values ('{departureId}','{packingId}','DepartureProcessed',true,'{SalesOwnerUserId}');
            insert into logistics_batch_panels(batch_id,packing_unit_id,panel_id,stage_code,active,added_by_user_id)
            values ('{departureId}','{packingId}','{panelIds[0]}','DepartureProcessed',true,'{SalesOwnerUserId}'),
                   ('{departureId}','{packingId}','{panelIds[1]}','DepartureProcessed',true,'{SalesOwnerUserId}');
            update logistics_batches set status='Finalized',finalized_by_user_id='{SalesOwnerUserId}',finalized_at_utc=now() where id='{departureId}';
            """);

        using var beforeResponse = await salesClient.GetAsync($"/api/projects/{projectId}/monthly-billing", TestContext.Current.CancellationToken);
        await AssertStatusAsync(beforeResponse, HttpStatusCode.OK, context);
        using var beforeJson = await ReadJsonAsync(beforeResponse);
        var unbilled = beforeJson.RootElement.GetProperty("unbilledMonths").EnumerateArray().Single();
        Assert.Equal("2026-07-01", unbilled.GetProperty("billingMonth").GetString());
        Assert.Equal(2, unbilled.GetProperty("panelCount").GetInt32());

        using var openResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/monthly-billing/open", new
        {
            operationId = Guid.NewGuid(), billingMonth = new DateOnly(2026, 7, 1), recoveryCaseIds = Array.Empty<Guid>()
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(openResponse, HttpStatusCode.OK, context);
        var (ledgerId, ledgerVersion) = await ReadLedgerAsync(salesClient, projectId);
        using var revisionResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/monthly-billing/{ledgerId}/revisions", new
        {
            operationId = Guid.NewGuid(), expectedLedgerVersion = ledgerVersion, amount = 1_000_000m,
            note = "7월 1일~말일 출하분", recoveryCaseIds = Array.Empty<Guid>(), adjustmentReason = (string?)null
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(revisionResponse, HttpStatusCode.OK, context);
        (ledgerId, ledgerVersion) = await ReadLedgerAsync(salesClient, projectId);
        using var confirmResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/monthly-billing/{ledgerId}/confirm", new
        {
            operationId = Guid.NewGuid(), expectedLedgerVersion = ledgerVersion,
            invoiceConfirmedDate = new DateOnly(2026, 7, 22), invoiceNumber = "INV-202607-001", note = "회계 발행 확인"
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(confirmResponse, HttpStatusCode.OK, context);

        using var afterResponse = await salesClient.GetAsync($"/api/projects/{projectId}/monthly-billing", TestContext.Current.CancellationToken);
        using var afterJson = await ReadJsonAsync(afterResponse);
        Assert.Equal(1_000_000m, afterJson.RootElement.GetProperty("confirmedAmount").GetDecimal());
        var latest = afterJson.RootElement.GetProperty("ledgers")[0].GetProperty("revisions")[0];
        Assert.Equal(2, latest.GetProperty("panels").GetArrayLength());
        Assert.Equal("INV-202607-001", latest.GetProperty("invoiceNumber").GetString());
        await using (var dataSource = NpgsqlDataSource.Create(context.ConnectionString))
        await using (var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var gate = await MonthlyBillingStore.ReadCompletionGateAsync(connection, transaction, projectId, 1_000_000m, TestContext.Current.CancellationToken);
            Assert.True(gate.AllLedgersConfirmed);
            Assert.True(gate.AllRecoveriesConfirmed);
            Assert.True(gate.RequestedAmountMatchesSalesAmount);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        var revisionId = latest.GetProperty("revisionId").GetGuid();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => context.ExecuteSqlAsync($"update sales_monthly_billing_revisions set note='변조' where id='{revisionId}';"));
        Assert.Equal(PostgresErrorCodes.RaiseException, immutable.SqlState);
    }

    [Fact]
    public async Task Ul891Set_ProjectCreation_DraftPublishAndQuantityChanges_PreservePhysicalPanelIdentity()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var createResponse = await salesClient.PostAsJsonAsync("/api/projects", new
        {
            customerName = "UL891 세트 고객",
            item = "UL891",
            projectCode = $"UL891-SET-{suffix}",
            projectTitle = $"UL891 SET {suffix}",
            panelCount = (int?)null,
            deliveryDate = new DateOnly(2026, 12, 31),
            salesOwnerUserId = SalesOwnerUserId,
            packagingMethod = "WoodenCrate",
            salesAmount = 12_000_000m,
            currencyCode = "KRW",
            deliveryLocation = "A동",
            fatRequired = true,
            ul891SetSpecs = new[]
            {
                new { name = "MCC 세트", quantity = 2, components = new[] { new { componentCode = "A" }, new { componentCode = "B" }, new { componentCode = "C" } } },
                new { name = "PCC 세트", quantity = 1, components = new[] { new { componentCode = "A" }, new { componentCode = "B" } } }
            }
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(createResponse, HttpStatusCode.Created, context);
        using var createJson = await ReadJsonAsync(createResponse);
        var projectId = createJson.RootElement.GetProperty("projectId").GetGuid();

        using var structureResponse = await salesClient.GetAsync($"/api/projects/{projectId}/set-structure", TestContext.Current.CancellationToken);
        await AssertStatusAsync(structureResponse, HttpStatusCode.OK, context);
        using var structureJson = await ReadJsonAsync(structureResponse);
        Assert.Equal("Ul891Set", structureJson.RootElement.GetProperty("structureMode").GetString());
        Assert.Equal(2, structureJson.RootElement.GetProperty("specs").GetArrayLength());
        Assert.Equal(8L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where project_id='{projectId}' and status='Active';"));
        Assert.Equal(3L, await context.ReadScalarAsync<long>($"select count(*) from ul891_set_instances instance join ul891_set_specs spec on spec.id=instance.spec_id where spec.project_id='{projectId}' and instance.status='Active';"));

        var addSpecOperationId = Guid.NewGuid();
        var addSpecRequest = new
        {
            operationId = addSpecOperationId,
            expectedSpecCount = 2,
            name = "AUX 세트",
            quantity = 1,
            components = new[] { new { componentCode = "A" }, new { componentCode = "B" } },
            reason = "고객 추가 세트 주문"
        };
        using var addSpecResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/set-specs", addSpecRequest, TestContext.Current.CancellationToken);
        await AssertStatusAsync(addSpecResponse, HttpStatusCode.OK, context);
        using var addSpecReplayResponse = await salesClient.PostAsJsonAsync($"/api/projects/{projectId}/set-specs", addSpecRequest, TestContext.Current.CancellationToken);
        await AssertStatusAsync(addSpecReplayResponse, HttpStatusCode.OK, context);
        using var addSpecReplayJson = await ReadJsonAsync(addSpecReplayResponse);
        Assert.True(addSpecReplayJson.RootElement.GetProperty("replayed").GetBoolean());
        Assert.Equal(3L, await context.ReadScalarAsync<long>($"select count(*) from ul891_set_specs where project_id='{projectId}';"));
        Assert.Equal(10L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where project_id='{projectId}' and status='Active';"));

        var firstSpec = structureJson.RootElement.GetProperty("specs")[0];
        var specId = firstSpec.GetProperty("specId").GetGuid();
        var version = firstSpec.GetProperty("versions").EnumerateArray().Single();
        var versionId = version.GetProperty("versionId").GetGuid();
        var firstPanelId = firstSpec.GetProperty("instances")[0].GetProperty("panels")[0].GetProperty("panelId").GetGuid();

        using var designClient = context.CreateClient("dev-design");
        using var updateResponse = await designClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/versions/{versionId}",
            new
            {
                expectedSpecVersion = firstSpec.GetProperty("rowVersion").GetInt32(),
                specName = "MCC 메인 세트",
                revisionReason = "초도 설계 입력",
                components = new[]
                {
                    new { componentCode = "A", panelName = "MAIN A", panelSpecification = "800x2000", widthMm = 800m, heightMm = 2000m, depthMm = 600m },
                    new { componentCode = "B", panelName = "MAIN B", panelSpecification = "700x2000", widthMm = 700m, heightMm = 2000m, depthMm = 600m },
                    new { componentCode = "C", panelName = "MAIN C", panelSpecification = "600x2000", widthMm = 600m, heightMm = 2000m, depthMm = 600m }
                }
            }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(updateResponse, HttpStatusCode.OK, context);

        using var publishResponse = await designClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/versions/{versionId}/publish",
            new { operationId = Guid.NewGuid(), reason = "설계 확정" },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(publishResponse, HttpStatusCode.OK, context);
        Assert.Equal("MAIN A", await context.ReadScalarAsync<string>($"select panel_name from panel_placeholders where id='{firstPanelId}';"));

        using var publishedStructureResponse = await salesClient.GetAsync($"/api/projects/{projectId}/set-structure", TestContext.Current.CancellationToken);
        using var publishedJson = await ReadJsonAsync(publishedStructureResponse);
        var publishedSpec = publishedJson.RootElement.GetProperty("specs")[0];
        Assert.Equal("Published", publishedSpec.GetProperty("versions").EnumerateArray().Single().GetProperty("status").GetString());
        using var increaseResponse = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/instances/increase",
            new { operationId = Guid.NewGuid(), expectedActiveInstanceCount = 2, quantity = 1, reason = "고객 추가 주문" },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(increaseResponse, HttpStatusCode.OK, context);

        using var increasedResponse = await salesClient.GetAsync($"/api/projects/{projectId}/set-structure", TestContext.Current.CancellationToken);
        using var increasedJson = await ReadJsonAsync(increasedResponse);
        var increasedSpec = increasedJson.RootElement.GetProperty("specs")[0];
        Assert.Equal(3, increasedSpec.GetProperty("activeInstanceCount").GetInt32());
        var addedInstanceId = increasedSpec.GetProperty("instances").EnumerateArray().Single(item => item.GetProperty("instanceNumber").GetInt32() == 3).GetProperty("instanceId").GetGuid();
        Assert.Equal(firstPanelId, increasedSpec.GetProperty("instances")[0].GetProperty("panels")[0].GetProperty("panelId").GetGuid());

        using var cancelResponse = await salesClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/set-instances/cancel",
            new { operationId = Guid.NewGuid(), instanceIds = new[] { addedInstanceId }, procurementItemIds = Array.Empty<Guid>(), reason = "추가 수량 철회", exceptionAcknowledged = false },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(cancelResponse, HttpStatusCode.OK, context);
        Assert.Equal(2L, await context.ReadScalarAsync<long>($"select count(*) from ul891_set_instances where spec_id='{specId}' and status='Active';"));
        Assert.Equal(10L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where project_id='{projectId}' and status='Active';"));
        Assert.Equal(3L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where set_instance_id='{addedInstanceId}' and status='Cancelled';"));
    }

    private static async Task<Guid> CreateUl891ProjectAsync(HttpClient client, ProjectApiTestContext context, string label, decimal salesAmount, IReadOnlyList<string> codes)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var response = await client.PostAsJsonAsync("/api/projects", new
        {
            customerName = "UL891 월별 고객", item = "UL891", projectCode = $"UL891-{label}-{suffix}", projectTitle = $"UL891 {label} {suffix}", panelCount = (int?)null,
            deliveryDate = new DateOnly(2026, 12, 31), salesOwnerUserId = SalesOwnerUserId, packagingMethod = "WoodenCrate",
            salesAmount, currencyCode = "KRW", deliveryLocation = "A동", fatRequired = false,
            ul891SetSpecs = new[] { new { name = "월별 세트", quantity = 1, components = codes.Select(code => new { componentCode = code }).ToArray() } }
        }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(response, HttpStatusCode.Created, context);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("projectId").GetGuid();
    }

    private static async Task<(Guid LedgerId, int Version)> ReadLedgerAsync(HttpClient client, Guid projectId)
    {
        using var response = await client.GetAsync($"/api/projects/{projectId}/monthly-billing", TestContext.Current.CancellationToken);
        using var json = await ReadJsonAsync(response);
        var ledger = json.RootElement.GetProperty("ledgers")[0];
        return (ledger.GetProperty("ledgerId").GetGuid(), ledger.GetProperty("rowVersion").GetInt32());
    }
}
