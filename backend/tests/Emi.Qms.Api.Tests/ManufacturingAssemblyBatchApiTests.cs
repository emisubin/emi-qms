using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class ProjectRegistrationApiTests
{
    [Fact]
    public async Task ManufacturingStepBatch_AllowsEveryStepAndChecksOnlyTheSelectedStep()
    {
        await using var context = await ProjectApiTestContext.CreateAsync();
        using var salesClient = context.CreateClient("dev-sales");
        using var manufacturingClient = context.CreateClient("dev-manufacturing");
        var unique = Guid.NewGuid().ToString("N")[..8];
        var projectId = await CreateProjectAndReadIdAsync(
            salesClient,
            $"MFG-BATCH-{unique}",
            $"Manufacturing Batch {unique}",
            3);
        using var panelsResponse = await salesClient.GetAsync(
            $"/api/projects/{projectId}/panels",
            TestContext.Current.CancellationToken);
        using var panelsJson = await ReadJsonAsync(panelsResponse);
        var panelIds = panelsJson.RootElement
            .EnumerateArray()
            .Select(panel => panel.GetProperty("panelId").GetGuid())
            .OrderBy(id => id)
            .ToArray();
        var executionIds = panelIds.ToDictionary(panelId => panelId, _ => Guid.NewGuid());
        var manufacturingUserId = new Guid("50000000-0000-0000-0000-000000000004");

        await context.ExecuteSqlAsync($"""
            insert into user_project_access (user_id, project_id)
            values ('{manufacturingUserId}', '{projectId}')
            on conflict do nothing;

            insert into project_assignees (
                project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc
            )
            values (
                '{projectId}', 'ManufacturingPrimary', '{manufacturingUserId}', '{SalesOwnerUserId}', now()
            )
            on conflict (project_id, responsibility_type)
            do update set assigned_user_id = excluded.assigned_user_id;

            update panel_placeholders
            set workflow_stage = 'ManufacturingInProgress'
            where id = any(array[
                '{panelIds[0]}'::uuid, '{panelIds[1]}'::uuid, '{panelIds[2]}'::uuid
            ]);

            insert into work_items (
                project_id, target_type, target_id, workflow_stage_code, responsibility_type,
                assigned_user_id, assigned_role_code, title, description, status, priority,
                idempotency_key, created_by_user_id, started_at_utc
            )
            select
                '{projectId}', 'Panel', panel.id, 'ManufacturingWork', 'ManufacturingPrimary',
                '{manufacturingUserId}', 'manufacturing', '제조 작업 · ' || panel.display_code,
                '일괄 조립 테스트', 'InProgress', 'Normal',
                'kitting:panel:' || panel.id::text || ':manufacturing', '{SalesOwnerUserId}', now()
            from panel_placeholders panel
            where panel.id = any(array[
                '{panelIds[0]}'::uuid, '{panelIds[1]}'::uuid, '{panelIds[2]}'::uuid
            ]);

            insert into panel_manufacturing_executions (
                id, project_id, panel_id, status, started_by_user_id, template_version_id
            )
            values
                ('{executionIds[panelIds[0]]}', '{projectId}', '{panelIds[0]}', 'InProgress', '{manufacturingUserId}', '44000000-0000-0000-0000-000000000002'),
                ('{executionIds[panelIds[1]]}', '{projectId}', '{panelIds[1]}', 'InProgress', '{manufacturingUserId}', '44000000-0000-0000-0000-000000000002'),
                ('{executionIds[panelIds[2]]}', '{projectId}', '{panelIds[2]}', 'InProgress', '{manufacturingUserId}', '44000000-0000-0000-0000-000000000002');

            insert into panel_manufacturing_execution_steps (
                execution_id, sequence_number, step_name
            )
            select execution.id, item.display_order, item.label
            from panel_manufacturing_executions execution
            join manufacturing_step_template_items item
              on item.template_version_id = execution.template_version_id
            where execution.id = any(array[
                '{executionIds[panelIds[0]]}'::uuid,
                '{executionIds[panelIds[1]]}'::uuid,
                '{executionIds[panelIds[2]]}'::uuid
            ]);
            """);

        int targetStepSequence;
        string targetStepName;
        (int Sequence, string Name)[] availableSteps;
        using (var queueResponse = await manufacturingClient.GetAsync(
                   $"/api/manufacturing/queue?projectId={projectId}",
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
            using var queueJson = await ReadJsonAsync(queueResponse);
            var queuePanels = queueJson.RootElement
                .GetProperty("projects")[0]
                .GetProperty("panels")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(3, queuePanels.Length);
            Assert.All(queuePanels, panel =>
            {
                Assert.NotEmpty(panel.GetProperty("batchSteps").EnumerateArray());
            });
            availableSteps = queuePanels[0].GetProperty("batchSteps").EnumerateArray()
                .Select(step => (
                    step.GetProperty("sequenceNumber").GetInt32(),
                    step.GetProperty("stepName").GetString()!))
                .ToArray();
            Assert.True(availableSteps.Length >= 2);
            targetStepSequence = availableSteps[0].Sequence;
            targetStepName = availableSteps[0].Name;
        }

        var operationId = Guid.NewGuid();
        var successfulPanels = panelIds.Take(2)
            .Select(panelId => new
            {
                panelId,
                executionId = executionIds[panelId],
                expectedVersion = 1
            })
            .ToArray();
        using (var forbidden = await salesClient.PostAsJsonAsync(
                   "/api/manufacturing/executions/step-batch",
                   new { operationId = Guid.NewGuid(), projectId, targetStepSequence, targetStepName, panels = successfulPanels },
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using var successful = await manufacturingClient.PostAsJsonAsync(
            "/api/manufacturing/executions/step-batch",
            new { operationId, projectId, targetStepSequence, targetStepName, panels = successfulPanels },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, successful.StatusCode);
        using (var successfulJson = await ReadJsonAsync(successful))
        {
            Assert.Equal(2, successfulJson.RootElement.GetProperty("completedPanelCount").GetInt32());
            Assert.Equal(2, successfulJson.RootElement.GetProperty("checkedStepCount").GetInt32());
            Assert.False(successfulJson.RootElement.GetProperty("replayed").GetBoolean());
        }

        Assert.Equal(2L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_manufacturing_execution_steps
            where execution_id = any(array[
                '{executionIds[panelIds[0]]}'::uuid,
                '{executionIds[panelIds[1]]}'::uuid
            ])
              and sequence_number = {targetStepSequence}
              and checked_at_utc is not null;
            """));
        Assert.Equal(0L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_manufacturing_execution_steps
            where execution_id = any(array[
                '{executionIds[panelIds[0]]}'::uuid,
                '{executionIds[panelIds[1]]}'::uuid
            ])
              and sequence_number <> {targetStepSequence}
              and checked_at_utc is not null;
            """));
        Assert.Equal(2L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_manufacturing_events
            where batch_operation_id = '{operationId}';
            """));
        Assert.Equal(2, await context.ReadScalarAsync<int>($"""
            select min(version)
            from panel_manufacturing_executions
            where id = any(array[
                '{executionIds[panelIds[0]]}'::uuid,
                '{executionIds[panelIds[1]]}'::uuid
            ]);
            """));

        using var replay = await manufacturingClient.PostAsJsonAsync(
            "/api/manufacturing/executions/step-batch",
            new { operationId, projectId, targetStepSequence, targetStepName, panels = successfulPanels },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using (var replayJson = await ReadJsonAsync(replay))
        {
            Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
        }
        Assert.Equal(2L, await context.ReadScalarAsync<long>($"""
            select count(*) from panel_manufacturing_events where batch_operation_id = '{operationId}';
            """));

        var expectedVersion = 2;
        foreach (var step in availableSteps.Skip(1))
        {
            var nextOperationId = Guid.NewGuid();
            var nextPanels = panelIds.Take(2)
                .Select(panelId => new
                {
                    panelId,
                    executionId = executionIds[panelId],
                    expectedVersion
                })
                .ToArray();
            using var nextResponse = await manufacturingClient.PostAsJsonAsync(
                "/api/manufacturing/executions/step-batch",
                new
                {
                    operationId = nextOperationId,
                    projectId,
                    targetStepSequence = step.Sequence,
                    targetStepName = step.Name,
                    panels = nextPanels
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
            expectedVersion += 1;
        }
        Assert.Equal(availableSteps.Length * 2L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_manufacturing_execution_steps
            where execution_id = any(array[
                '{executionIds[panelIds[0]]}'::uuid,
                '{executionIds[panelIds[1]]}'::uuid
            ])
              and checked_at_utc is not null;
            """));

        using var conflict = await manufacturingClient.PostAsJsonAsync(
            "/api/manufacturing/executions/step-batch",
            new
            {
                operationId = Guid.NewGuid(),
                projectId,
                targetStepSequence,
                targetStepName,
                panels = new object[]
                {
                    new { panelId = panelIds[0], executionId = executionIds[panelIds[0]], expectedVersion },
                    new { panelId = panelIds[2], executionId = executionIds[panelIds[2]], expectedVersion = 1 }
                }
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(0L, await context.ReadScalarAsync<long>($"""
            select count(*)
            from panel_manufacturing_execution_steps
            where execution_id = '{executionIds[panelIds[2]]}'
              and checked_at_utc is not null;
            """));
        Assert.Equal(1, await context.ReadScalarAsync<int>($"""
            select version
            from panel_manufacturing_executions
            where id = '{executionIds[panelIds[2]]}';
            """));
    }
}
