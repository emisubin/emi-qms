using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var manufacturingTemplateId = Guid.NewGuid();
        var manufacturingVersionId = Guid.NewGuid();
        var manufacturingDefinitionKey = Guid.NewGuid();
        var planTemplateId = Guid.NewGuid();
        var planVersionId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();
        var planDefinitionKey = Guid.NewGuid();
        await context.ExecuteSqlAsync($"""
            insert into production_control_manufacturing_templates (id, product_type_id)
            select '{manufacturingTemplateId}', id from production_product_types where code='UL891';
            insert into production_control_manufacturing_versions (
                id, template_id, version_number, lifecycle_status, activated_at_utc
            )
            values ('{manufacturingVersionId}', '{manufacturingTemplateId}', 1, 'Active', now());
            insert into production_control_manufacturing_items (
                template_version_id, definition_key, display_order, label, step_role
            )
            values ('{manufacturingVersionId}', '{manufacturingDefinitionKey}', 1, '조립', 'Assembly');

            insert into production_control_plan_templates (id, product_type_id)
            select '{planTemplateId}', id from production_product_types where code='UL891';
            insert into production_control_plan_versions (
                id, template_id, version_number, lifecycle_status, activated_at_utc
            )
            values ('{planVersionId}', '{planTemplateId}', 1, 'Active', now());
            insert into production_control_plan_items (
                id, template_version_id, definition_key, display_order, label, is_required
            )
            values ('{planItemId}', '{planVersionId}', '{planDefinitionKey}', 1, '제조 착수', true);
            insert into production_control_plan_connections (
                plan_item_id, source_code, source_definition_key
            )
            values ('{planItemId}', 'MANUFACTURING_STEP_COMPLETED', '{manufacturingDefinitionKey}');
            """);
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
                new { name = "MCC 세트", quantity = 2, panelCount = 3 },
                new { name = "PCC 세트", quantity = 1, panelCount = 2 }
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
        Assert.Equal(3L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from project_production_plan_set_scopes scope
            join project_production_plans plan on plan.id=scope.production_plan_id
            where plan.project_id='{projectId}';
            """));

        using var productionClient = context.CreateClient("dev-production");
        using var aggregatePlanResponse = await productionClient.GetAsync($"/api/projects/{projectId}/production-planning", TestContext.Current.CancellationToken);
        await AssertStatusAsync(aggregatePlanResponse, HttpStatusCode.OK, context);
        using var aggregatePlan = await ReadJsonAsync(aggregatePlanResponse);
        Assert.True(aggregatePlan.RootElement.GetProperty("isSetScoped").GetBoolean());
        Assert.Equal(3, aggregatePlan.RootElement.GetProperty("scopes").GetArrayLength());
        var planScopes = aggregatePlan.RootElement.GetProperty("scopes").EnumerateArray().ToList();
        var firstSetId = planScopes[0].GetProperty("setInstanceId").GetGuid();
        var secondSetId = planScopes[1].GetProperty("setInstanceId").GetGuid();
        var aggregateItem = aggregatePlan.RootElement.GetProperty("items")[0];

        using var forbiddenProjectScheduleResponse = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning",
            new
            {
                productTypeId = aggregatePlan.RootElement.GetProperty("productTypeId").GetGuid(),
                expectedRowVersion = aggregatePlan.RootElement.GetProperty("rowVersion").GetInt32(),
                notes = (string?)null,
                reason = "세트 일정 우회 입력 차단 테스트",
                items = new[]
                {
                    new
                    {
                        itemId = aggregateItem.GetProperty("itemId").GetGuid(),
                        templateStepId = (Guid?)null,
                        stepName = aggregateItem.GetProperty("stepName").GetString(),
                        sequenceNumber = aggregateItem.GetProperty("sequenceNumber").GetInt32(),
                        isRequired = aggregateItem.GetProperty("isRequired").GetBoolean(),
                        expectedRowVersion = aggregateItem.GetProperty("rowVersion").GetInt32(),
                        plannedDate = (DateOnly?)null,
                        plannedStartDate = new DateOnly(2026, 7, 1),
                        plannedEndDate = new DateOnly(2026, 7, 2),
                        assignedUserId = (Guid?)null,
                        requiredHeadcount = (int?)null,
                        note = (string?)null,
                        isDeleted = false,
                        definitionKey = aggregateItem.GetProperty("definitionKey").GetGuid(),
                        connections = aggregateItem.GetProperty("connections")
                    }
                },
                assignees = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(forbiddenProjectScheduleResponse, HttpStatusCode.BadRequest, context);
        using var forbiddenProjectSchedule = await ReadJsonAsync(forbiddenProjectScheduleResponse);
        Assert.True(forbiddenProjectSchedule.RootElement
            .GetProperty("errors")
            .TryGetProperty("items[0].plannedStartDate", out _));

        var setDefault = aggregatePlan.RootElement.GetProperty("setDefault");
        var defaultItems = setDefault.GetProperty("items").EnumerateArray().ToList();
        var saveDefaultRequest = new
        {
            expectedRowVersion = setDefault.GetProperty("rowVersion").GetInt32(),
            overwriteExisting = false,
            reason = "전체 세트 기본계획 입력",
            items = defaultItems.Select(item => new
            {
                itemId = item.GetProperty("itemId").GetGuid(),
                expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                plannedStartDate = new DateOnly(2026, 8, 1),
                plannedEndDate = new DateOnly(2026, 8, 5),
                assignedUserId = (Guid?)null,
                requiredHeadcount = 2,
                note = "모든 세트 기본계획"
            }).ToArray()
        };
        using var forbiddenDefaultResponse = await salesClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning/set-defaults",
            saveDefaultRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(forbiddenDefaultResponse, HttpStatusCode.Forbidden, context);
        using var saveDefaultResponse = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning/set-defaults",
            saveDefaultRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(saveDefaultResponse, HttpStatusCode.OK, context);
        using var staleDefaultResponse = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning/set-defaults",
            saveDefaultRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(staleDefaultResponse, HttpStatusCode.Conflict, context);

        using var firstSetPlanResponse = await productionClient.GetAsync(
            $"/api/projects/{projectId}/production-planning?setInstanceId={firstSetId}",
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(firstSetPlanResponse, HttpStatusCode.OK, context);
        using var firstSetPlan = await ReadJsonAsync(firstSetPlanResponse);
        var firstSetItems = firstSetPlan.RootElement.GetProperty("items").EnumerateArray().ToList();
        using var saveFirstSetResponse = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning/set-scopes/{firstSetId}",
            new
            {
                expectedRowVersion = firstSetPlan.RootElement.GetProperty("selectedScope").GetProperty("rowVersion").GetInt32(),
                reason = "첫 번째 세트 개별 수정",
                items = firstSetItems.Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedStartDate = new DateOnly(2026, 8, 10),
                    plannedEndDate = new DateOnly(2026, 8, 12),
                    assignedUserId = (Guid?)null,
                    requiredHeadcount = 2,
                    note = "첫 번째 실물 세트"
                }).ToArray()
            },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(saveFirstSetResponse, HttpStatusCode.OK, context);

        using var aggregateAfterSaveResponse = await productionClient.GetAsync(
            $"/api/projects/{projectId}/production-planning",
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(aggregateAfterSaveResponse, HttpStatusCode.OK, context);
        using var aggregateAfterSave = await ReadJsonAsync(aggregateAfterSaveResponse);
        var aggregateAfterSaveItem = aggregateAfterSave.RootElement.GetProperty("items")[0];
        Assert.Equal("2026-08-01", aggregateAfterSaveItem.GetProperty("plannedStartDate").GetString());
        Assert.Equal("2026-08-12", aggregateAfterSaveItem.GetProperty("plannedEndDate").GetString());

        using var secondSetPlanResponse = await productionClient.GetAsync(
            $"/api/projects/{projectId}/production-planning?setInstanceId={secondSetId}",
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(secondSetPlanResponse, HttpStatusCode.OK, context);
        using var secondSetPlan = await ReadJsonAsync(secondSetPlanResponse);
        Assert.All(secondSetPlan.RootElement.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("2026-08-01", item.GetProperty("plannedStartDate").GetString());
            Assert.Equal("2026-08-05", item.GetProperty("plannedEndDate").GetString());
        });

        var refreshedDefault = aggregateAfterSave.RootElement.GetProperty("setDefault");
        using var overwriteDefaultResponse = await productionClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/production-planning/set-defaults",
            new
            {
                expectedRowVersion = refreshedDefault.GetProperty("rowVersion").GetInt32(),
                overwriteExisting = true,
                reason = "개별 수정분 포함 전체 일정 재적용",
                items = refreshedDefault.GetProperty("items").EnumerateArray().Select(item => new
                {
                    itemId = item.GetProperty("itemId").GetGuid(),
                    expectedRowVersion = item.GetProperty("rowVersion").GetInt32(),
                    plannedStartDate = new DateOnly(2026, 9, 1),
                    plannedEndDate = new DateOnly(2026, 9, 5),
                    assignedUserId = (Guid?)null,
                    requiredHeadcount = 3,
                    note = "명시적 전체 덮어쓰기"
                }).ToArray()
            },
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(overwriteDefaultResponse, HttpStatusCode.OK, context);
        Assert.Equal(3L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from project_production_plan_set_item_values value
            join project_production_plan_set_scopes scope on scope.id=value.set_scope_id
            join project_production_plans plan on plan.id=scope.production_plan_id
            where plan.project_id='{projectId}'
              and value.planned_start_date=date '2026-09-01';
            """));

        var addSpecOperationId = Guid.NewGuid();
        var addSpecRequest = new
        {
            operationId = addSpecOperationId,
            expectedSpecCount = 2,
            name = "AUX 세트",
            quantity = 1,
            panelCount = 2,
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
        Assert.Equal(4L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from project_production_plan_set_scopes scope
            join project_production_plans plan on plan.id=scope.production_plan_id
            where plan.project_id='{projectId}';
            """));
        Assert.Equal(4L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from project_production_plan_set_item_values value
            join project_production_plan_set_scopes scope on scope.id=value.set_scope_id
            join project_production_plans plan on plan.id=scope.production_plan_id
            where plan.project_id='{projectId}'
              and value.planned_start_date=date '2026-09-01';
            """));

        var firstSpec = structureJson.RootElement.GetProperty("specs")[0];
        var specId = firstSpec.GetProperty("specId").GetGuid();
        var currentSlots = firstSpec.GetProperty("currentDesign").EnumerateArray().ToList();
        var firstPanelId = firstSpec.GetProperty("instances")[0].GetProperty("panels")[0].GetProperty("panelId").GetGuid();
        var updateDesignRequest = new
        {
            expectedSpecVersion = firstSpec.GetProperty("rowVersion").GetInt32(),
            specName = "MCC 메인 세트",
            reason = "동일 사양 반복 패널 저장",
            slots = currentSlots.Select(slot => new
            {
                slotId = slot.GetProperty("slotId").GetGuid(),
                panelName = "동일 패널",
                panelSpecification = "UL891 공통 사양",
                widthMm = 800m,
                heightMm = 2000m,
                depthMm = 600m
            }).ToArray()
        };

        using var viewerClient = context.CreateClient("dev-viewer");
        using var forbiddenDesignResponse = await viewerClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/design",
            updateDesignRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(forbiddenDesignResponse, HttpStatusCode.Forbidden, context);

        using var designClient = context.CreateClient("dev-design");
        using var updateResponse = await designClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/design",
            updateDesignRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(updateResponse, HttpStatusCode.OK, context);
        using var staleDesignResponse = await designClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/design",
            updateDesignRequest,
            TestContext.Current.CancellationToken);
        await AssertStatusAsync(staleDesignResponse, HttpStatusCode.Conflict, context);

        Assert.Equal(10L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where project_id='{projectId}' and status='Active';"));
        Assert.Equal("동일 패널", await context.ReadScalarAsync<string>($"select panel_name from panel_placeholders where id='{firstPanelId}';"));
        Assert.True(await context.ReadScalarAsync<bool>($"select panel_info_completed from panel_placeholders where id='{firstPanelId}';"));

        using var savedStructureResponse = await salesClient.GetAsync($"/api/projects/{projectId}/set-structure", TestContext.Current.CancellationToken);
        using var savedJson = await ReadJsonAsync(savedStructureResponse);
        var savedSpec = savedJson.RootElement.GetProperty("specs")[0];
        Assert.All(savedSpec.GetProperty("currentDesign").EnumerateArray(), slot =>
            Assert.Equal("동일 패널", slot.GetProperty("panelName").GetString()));
        Assert.Equal("Draft", savedSpec.GetProperty("versions").EnumerateArray().Single().GetProperty("status").GetString());

        var savedSlots = savedSpec.GetProperty("currentDesign").EnumerateArray().ToList();
        var kittingBatchId = Guid.NewGuid();
        await context.ExecuteSqlAsync($"""
            insert into panel_kitting_batches (
                id,project_id,operation_id,requested_by_user_id,panel_set_fingerprint,
                completed_panel_count,generated_work_item_count,project_kitting_completed,
                readiness_active_item_count,readiness_completed_item_count,
                readiness_predicate_version,readiness_verified_at_utc
            ) values (
                '{kittingBatchId}','{projectId}','{Guid.NewGuid()}','{SalesOwnerUserId}',repeat('a',64),
                1,0,false,1,1,1,now()
            );
            insert into panel_kitting_completions (batch_id,project_id,panel_id,completed_by_user_id)
            values ('{kittingBatchId}','{projectId}','{firstPanelId}','{SalesOwnerUserId}');
            """);
        using var removeStartedPositionResponse = await designClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/design",
            new
            {
                expectedSpecVersion = savedSpec.GetProperty("rowVersion").GetInt32(),
                specName = "MCC 메인 세트",
                reason = "착수 위치 삭제 차단 확인",
                slots = savedSlots.Skip(1).Select(slot => new
                {
                    slotId = (Guid?)slot.GetProperty("slotId").GetGuid(),
                    panelName = slot.GetProperty("panelName").GetString(),
                    panelSpecification = slot.GetProperty("panelSpecification").GetString(),
                    widthMm = slot.GetProperty("widthMm").GetDecimal(),
                    heightMm = slot.GetProperty("heightMm").GetDecimal(),
                    depthMm = slot.GetProperty("depthMm").GetDecimal()
                }).ToArray()
            }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(removeStartedPositionResponse, HttpStatusCode.Conflict, context);

        using var replacePositionResponse = await designClient.PutAsJsonAsync(
            $"/api/projects/{projectId}/set-specs/{specId}/design",
            new
            {
                expectedSpecVersion = savedSpec.GetProperty("rowVersion").GetInt32(),
                specName = "MCC 메인 세트",
                reason = "세 번째 위치를 실제로 교체",
                slots = new object[]
                {
                    new { slotId = (Guid?)savedSlots[0].GetProperty("slotId").GetGuid(), panelName = "동일 패널", panelSpecification = "UL891 공통 사양", widthMm = 800m, heightMm = 2000m, depthMm = 600m },
                    new { slotId = (Guid?)savedSlots[1].GetProperty("slotId").GetGuid(), panelName = "동일 패널", panelSpecification = "UL891 공통 사양", widthMm = 800m, heightMm = 2000m, depthMm = 600m },
                    new { slotId = (Guid?)null, panelName = "동일 패널", panelSpecification = "UL891 공통 사양", widthMm = 800m, heightMm = 2000m, depthMm = 600m }
                }
            }, TestContext.Current.CancellationToken);
        await AssertStatusAsync(replacePositionResponse, HttpStatusCode.OK, context);
        Assert.Equal(10L, await context.ReadScalarAsync<long>($"select count(*) from panel_placeholders where project_id='{projectId}' and status='Active';"));
        Assert.Equal(2L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_placeholders panel
            join ul891_set_instances instance on instance.id=panel.set_instance_id
            where instance.spec_id='{specId}' and panel.status='Cancelled';
            """));
        Assert.Equal(firstPanelId, await context.ReadScalarAsync<Guid>($"select id from panel_placeholders where id='{firstPanelId}' and status='Active';"));

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
        using var afterCancelResponse = await salesClient.GetAsync($"/api/projects/{projectId}/set-structure", TestContext.Current.CancellationToken);
        using var afterCancelJson = await ReadJsonAsync(afterCancelResponse);
        var cancelledInstance = afterCancelJson.RootElement.GetProperty("specs")[0].GetProperty("instances")
            .EnumerateArray().Single(item => item.GetProperty("instanceId").GetGuid() == addedInstanceId);
        Assert.Equal(0, cancelledInstance.GetProperty("panels").GetArrayLength());
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
