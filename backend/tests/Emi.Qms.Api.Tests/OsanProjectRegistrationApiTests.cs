using System.Buffers.Binary;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
using ImageMagick;
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
using Npgsql;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanProjectRegistrationApiTests
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

        Assert.Equal(6, endpoints.Length);
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
            endpoint.RoutePattern.RawText?.Contains("/progress/", StringComparison.Ordinal) == true
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
        Assert.Equal(OsanProgressMutationStatus.Success, skipPredecessor.Status);
        Assert.Equal("Completed", skipPredecessor.Value!.Project.Targets[0].Steps[1].Status);
        Assert.Equal("NotStarted", skipPredecessor.Value.Project.Targets[0].Steps[0].Status);
        Assert.Equal("InProgress", skipPredecessor.Value.Project.Status);
        Assert.Equal(1, skipPredecessor.Value.Project.CompletedStepCount);
        Assert.Equal(14, skipPredecessor.Value.Project.TotalStepCount);

        var listedAfterFirstCompletion = Assert.Single((await projectStore.ListAsync(
            new ProjectAccessScope(true, []),
            TestContext.Current.CancellationToken)).Items);
        var detailedAfterFirstCompletion = await projectStore.GetAsync(
            projectId,
            TestContext.Current.CancellationToken);
        Assert.Equal("InProgress", listedAfterFirstCompletion.Status);
        Assert.Equal(1, listedAfterFirstCompletion.CompletedStepCount);
        Assert.Equal(14, listedAfterFirstCompletion.TotalStepCount);
        Assert.NotNull(detailedAfterFirstCompletion);
        Assert.Equal("InProgress", detailedAfterFirstCompletion.Status);
        Assert.Equal(1, detailedAfterFirstCompletion.CompletedStepCount);
        Assert.Equal(14, detailedAfterFirstCompletion.TotalStepCount);

        var outOfOrderIndividual = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Individual,
                6,
                [new OsanProgressTargetRequest(targetIds[0], 2)],
                []),
            UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OsanProgressMutationStatus.Success, outOfOrderIndividual.Status);
        Assert.Equal("Completed", outOfOrderIndividual.Value!.Project.Targets[0].Steps[5].Status);

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
                new OsanProgressTargetRequest(targetIds[0], 3),
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
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_photos where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_progress_step_photos where project_id=@project_id;",
            TestContext.Current.CancellationToken,
            ("project_id", projectId)));

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
            [targetIds[0]] = 4,
            [targetIds[1]] = 2
        };
        var secondTargetStageTwo = await progressStore.CompleteAsync(
            projectId,
            new CompleteOsanProgressInput(
                Guid.NewGuid(),
                OsanCompletionModes.Batch,
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
        Guid? operationId = null) =>
        new(
            title,
            projectCode,
            customerName,
            poNumber,
            workOrderNumber,
            new DateOnly(2026, 12, 31),
            "Product",
            quantity,
            operationId ?? Guid.NewGuid());

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
}
