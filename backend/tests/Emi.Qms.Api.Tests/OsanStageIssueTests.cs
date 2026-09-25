using System.Security.Claims;
using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task StageIssues_SequenceResolutionReplayAndPackaging_KeepIndependentEvidence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var config = database.CreateConfiguration(); var provider = new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot, provider, config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,department_id,is_active) values(@actor,'issue-worker','Worker',(select id from departments where code='manufacturing'),true)", ct, ("actor", UserId));
        var projects = new OsanProjectStore(provider); var store = new OsanProgressStore(provider); var edits = new OsanPhotoEditStore(provider);
        var created = await projects.CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct);
        var id = created.Value!.Project.ProjectId;
        var photo = new OsanProgressPhotoInput("evidence.png", "image/png", [1,2,3], new string('a', 64));
        async Task<CompleteOsanProgressInput> Input(int stage, string comment = "Synthetic issue")
        {
            var target = (await store.GetAsync(id, ct))!.Targets[0];
            return new(Guid.NewGuid(), "individual", stage, [new(target.TargetId, target.Version)], [], comment);
        }
        var initial = (await store.GetAsync(id, ct))!.Targets[0];
        Assert.True(initial.Steps[4].CanCompleteIndividual);
        Assert.False(initial.Steps[1].CanCompleteIndividual);
        Assert.Equal(OsanProgressMutationStatus.Success, (await store.CompleteAsync(id, await Input(5), UserId, ct, true)).Status);
        var registration = (await Input(1)) with { Photos = [photo] };
        Assert.Equal(OsanProgressMutationStatus.Validation, (await store.RecordIssueAsync(id, registration with { Comment = " " }, UserId, false, ct)).Status);
        using var auditScope = AuditRequestContext.Push(new AuditMutationContext(
            UserId, null, Guid.NewGuid(), null, "OsanProjects", "RegisterStageIssue", "RegisterStageIssue"));
        var registered = await store.RecordIssueAsync(id, registration, UserId, false, ct);
        Assert.True(await database.ReadScalarAsync<long>(
            "select count(*) from audit_event_changes where target_type='osan_stage_issues'", ct) > 0);
        Assert.Equal(OsanProgressMutationStatus.Success, registered.Status);
        var issue = registered.Value!.Project.Targets[0].Steps[0].OpenIssue!;
        Assert.Single(issue.Photos); var issuePhoto = issue.Photos[0].PhotoId;
        Assert.Equal(1, registered.Value.Project.OpenIssueCount);
        Assert.Equal(1, registered.Value.Project.CompletedStepCount);
        Assert.True(registered.Value.Project.Targets[0].Steps[1].CanCompleteIndividual);
        Assert.False(registered.Value.Project.Targets[0].Steps[0].CanCompleteIndividual);
        Assert.True((await store.RecordIssueAsync(id, registration, UserId, false, ct)).Value!.Replayed);
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.RecordIssueAsync(id, registration with { Comment = "changed" }, UserId, false, ct)).Status);
        var append = await store.RecordIssueAsync(id, await Input(1, "Additional record"), UserId, false, ct, requireOpen: true);
        Assert.Equal(issue.IssueId, append.Value!.Project.Targets[0].Steps[0].OpenIssue!.IssueId);
        Assert.Equal("Additional record", append.Value.Project.Targets[0].Steps[0].OpenIssue!.Comment);
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from osan_stage_issues where status='Open'", ct));
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from osan_notification_events where event_kind='StepIssueRegistered'", ct));
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.CompleteAsync(id, await Input(1), UserId, ct, true)).Status);
        var target = (await store.GetAsync(id, ct))!.Targets[0];
        Assert.Equal(409, (await edits.RequestAsync(id,
            new(Guid.NewGuid(), target.TargetId, 1, "미해결 이상 단계 사진 정정"), UserId, ct)).Status);
        Assert.Equal(409, (await store.StageActionAsync(id, target.Steps[0].StepId, new(Guid.NewGuid(), "reject", target.Version), "Reject", UserId, ct)).Status);
        for (var stage = 2; stage <= 4; stage++)
            Assert.Equal(OsanProgressMutationStatus.Success, (await store.CompleteAsync(id, await Input(stage), UserId, ct, true)).Status);
        Assert.Equal(OsanProgressMutationStatus.Success, (await store.CompleteAsync(id, await Input(6), UserId, ct, true)).Status);
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.CompleteAsync(id, await Input(7), UserId, ct, true)).Status);
        var resolution = await Input(1, "Resolved");
        Assert.Equal(OsanProgressMutationStatus.Validation, (await store.RecordIssueAsync(id, resolution, UserId, true, ct)).Status);
        resolution = resolution with { Photos = [photo with { Sha256 = new string('b', 64) }] };
        var resolved = await store.RecordIssueAsync(id, resolution, UserId, true, ct);
        Assert.Equal(OsanProgressMutationStatus.Success, resolved.Status);
        Assert.Equal(0, resolved.Value!.Project.OpenIssueCount);
        Assert.Equal(6, resolved.Value.Project.CompletedStepCount);
        Assert.True(resolved.Value.Project.Targets[0].Steps[6].CanCompleteIndividual);
        Assert.True((await store.RecordIssueAsync(id, resolution, UserId, true, ct)).Value!.Replayed);
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from osan_stage_issues where status='Resolved'", ct));
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from osan_notification_events where event_kind='StepIssueResolved'", ct));
        Assert.Equal(0L, await database.ReadScalarAsync<long>("select count(*) from osan_notification_events where event_kind='StepCompleted' and stage_sequence=1", ct));
        Assert.NotNull(await store.GetPhotoAsync(id, issuePhoto, ct));
        var history = (await store.HistoryAsync(id, target.Steps[0].StepId, ct))!;
        Assert.Single(history, r => r.EventType == "IssueRegistered");
        Assert.Single(history, r => r.EventType == "IssueRecorded");
        Assert.Contains(history, r => r.EventType == "IssueResolved" && r.Comment == "Resolved");
        Assert.Contains(history, r => r.Photos.Any(p => p.PhotoId == issuePhoto));
        Assert.Equal(OsanProgressMutationStatus.Success, (await store.RecordIssueAsync(id, await Input(7), UserId, false, ct)).Status);
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.CompleteAsync(id, await Input(7), UserId, ct, true)).Status);
        var packed = await store.RecordIssueAsync(id, await Input(7, "Packaging resolved"), UserId, true, ct, true);
        Assert.Equal(OsanProgressMutationStatus.Success, packed.Status);
        Assert.Equal("Completed", packed.Value!.Project.Status);
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.RecordIssueAsync(id, await Input(1), UserId, false, ct)).Status);
    }

    [Fact]
    public async Task StageIssues_CompletedStageConcurrentRegistrationResetAndDashboard_PreserveFollowingSteps()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var config = database.CreateConfiguration(); var provider = new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot, provider, config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,department_id,is_active) values(@actor,'issue-admin','Admin',(select id from departments where code='manufacturing'),true)", ct, ("actor", UserId));
        var projects = new OsanProjectStore(provider); var store = new OsanProgressStore(provider); var edits = new OsanPhotoEditStore(provider);
        var id = (await projects.CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct)).Value!.Project.ProjectId;
        async Task<CompleteOsanProgressInput> Input(int stage)
        {
            var t = (await store.GetAsync(id, ct))!.Targets[0];
            return new(Guid.NewGuid(), "individual", stage, [new(t.TargetId, t.Version)], [], "Synthetic");
        }
        for (var stage = 1; stage <= 3; stage++) await store.CompleteAsync(id, await Input(stage), UserId, ct, true);
        var before = (await store.GetAsync(id, ct))!.Targets[0];
        var request = new OsanPhotoEditRequest(Guid.NewGuid(), before.TargetId, 1, "이상 등록 전 사진 정정");
        Assert.Equal(200, (await edits.RequestAsync(id, request, UserId, ct)).Status);
        Assert.Equal(200, (await edits.ApproveAsync(id, request.RequestId, UserId, ct)).Status);
        var input = await Input(1);
        var racing = await Task.WhenAll(store.RecordIssueAsync(id, input, UserId, false, ct),
            store.RecordIssueAsync(id, input with { OperationId = Guid.NewGuid() }, UserId, false, ct));
        Assert.Single(racing, r => r.Status == OsanProgressMutationStatus.Success);
        Assert.Single(racing, r => r.ErrorCode == "osan_progress_stale_version");
        Assert.Equal(409, (await edits.ApproveAsync(id, request.RequestId, UserId, ct)).Status);
        Assert.Equal(409, (await edits.SaveAsync(id, request.RequestId, input, UserId, ct, true)).Status);
        var after = (await store.GetAsync(id, ct))!.Targets[0];
        Assert.Equal("NotStarted", after.Steps[0].Status);
        Assert.Equal("Synthetic", after.Steps[0].Comment);
        Assert.Equal(before.Steps[1].CompletedAtUtc, after.Steps[1].CompletedAtUtc);
        Assert.Equal(before.Steps[2].CompletedAtUtc, after.Steps[2].CompletedAtUtc);
        var dashboard = await new OsanDashboardStore(provider, TimeProvider.System).GetAsync(new("", "All", 1, 20), new(true, []), ct);
        var row = Assert.Single(dashboard.Items);
        Assert.Equal(1, row.OpenIssueCount); Assert.Equal(1, row.Stages[0].OpenIssueTargetCount);
        Assert.Equal(0, row.Stages[0].AvailableTargetCount); Assert.Equal(1, row.Stages[3].AvailableTargetCount);
        Assert.Equal(1, row.Stages[4].AvailableTargetCount); Assert.Equal(0, row.Stages[6].AvailableTargetCount);
        var notificationsBeforeReset = await database.ReadScalarAsync<long>("select count(*) from osan_notification_events", ct);
        var action = new OsanStageActionRequest(Guid.NewGuid(), "Reset synthetic issue", after.Version);
        Assert.Equal(200, (await store.StageActionAsync(id, after.Steps[0].StepId, action, "Reset", UserId, ct)).Status);
        var reset = (await store.GetAsync(id, ct))!.Targets[0];
        Assert.Null(reset.Steps[0].OpenIssue); Assert.Equal("NotStarted", reset.Steps[0].Status);
        Assert.Equal(before.Steps[1].CompletedAtUtc, reset.Steps[1].CompletedAtUtc);
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from osan_stage_issues where status='Reset'", ct));
        Assert.Equal(notificationsBeforeReset, await database.ReadScalarAsync<long>("select count(*) from osan_notification_events", ct));
        var records = (await store.HistoryAsync(id, after.Steps[0].StepId, ct))!;
        Assert.Contains(records, r => r.EventType == "Complete");
        Assert.Contains(records, r => r.EventType == "IssueRegistered");
        Assert.Contains(records, r => r.EventType == "Reset");
        Assert.DoesNotContain(records, r => r.EventType == "IssueResolved");
        Assert.Equal(OsanProgressMutationStatus.Conflict, (await store.RecordIssueAsync(id, await Input(1), UserId, false, ct, requireOpen: true)).Status);
    }

    [Fact]
    public async Task StageIssues_FreshShippingIssueCannotResolveAndConcurrentReadsKeepOneVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var config = database.CreateConfiguration(); var provider = new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot, provider, config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,department_id,is_active) values(@actor,'issue-snapshot','Synthetic',(select id from departments where code='manufacturing'),true)", ct, ("actor", UserId));
        var projects = new OsanProjectStore(provider); var store = new OsanProgressStore(provider);
        var id = (await projects.CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct)).Value!.Project.ProjectId;
        var initial = (await store.GetAsync(id, ct))!.Targets[0];
        var registration = new CompleteOsanProgressInput(Guid.NewGuid(), "individual", 6,
            [new(initial.TargetId, initial.Version)], [], (initial.Version + 1).ToString());
        var registered = await store.RecordIssueAsync(id, registration, UserId, false, ct);
        Assert.Equal(OsanProgressMutationStatus.Success, registered.Status);
        Assert.Equal("InProgress", registered.Value!.Project.Status);
        Assert.Equal("InProgress", registered.Value.Project.Targets[0].Status);
        Assert.False(registered.Value.Project.Targets[0].Steps[5].CanResolveIssue);
        var failed = await store.RecordIssueAsync(id, registration with { OperationId = Guid.NewGuid(), Comment = "Resolved",
            Targets = [new(initial.TargetId, initial.Version + 1)] }, UserId, true, ct, true);
        Assert.Equal("osan_progress_prerequisite_incomplete", failed.ErrorCode);
        Assert.Equal(0L, await database.ReadScalarAsync<long>("select count(*) from osan_stage_records where event_type='IssueResolved'", ct));

        async Task RecordMore()
        {
            for (var i = 0; i < 15; i++)
            {
                var target = (await store.GetAsync(id, ct))!.Targets[0];
                var result = await store.RecordIssueAsync(id, registration with { OperationId = Guid.NewGuid(),
                    Targets = [new(target.TargetId, target.Version)], Comment = (target.Version + 1).ToString() }, UserId, false, ct, requireOpen: true);
                Assert.Equal(OsanProgressMutationStatus.Success, result.Status);
            }
        }
        async Task ReadConsistent()
        {
            for (var i = 0; i < 40; i++)
            {
                var target = (await store.GetAsync(id, ct))!.Targets[0];
                Assert.Equal(target.Version.ToString(), target.Steps[5].OpenIssue!.Comment);
            }
        }
        await Task.WhenAll(RecordMore(), ReadConsistent());
        var row = Assert.Single((await new OsanDashboardStore(provider, TimeProvider.System).GetAsync(
            new("", "All", 1, 20), new(true, []), ct)).Items);
        Assert.Equal("InProgress", row.Status);
        Assert.Equal(1, row.OpenIssueCount);
        Assert.Equal(0, row.Stages[5].AvailableTargetCount);
    }

    [Fact]
    public async Task StageIssues_ProjectGuardRejectsWrongBusinessUnitAndReadOnlyActorBeforeDatabaseAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var emptyConfig = new ConfigurationBuilder().Build();
        var legacy = new DatabaseConnectionStringProvider(emptyConfig);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(QmsClaimTypes.Permission, QmsPermissions.ManufacturingUpdate)], "test"));
        var wrongUnit = await OsanProgressEndpointExtensions.AuthorizeProjectAsync(Guid.NewGuid(), QmsPermissions.ManufacturingUpdate,
            new OsanProjectStore(legacy), legacy, user, ct);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(wrongUnit).StatusCode);
        var context = new DefaultHttpContext();
        var osan = legacy.GetCurrentBusinessUnit()! with { Code = BusinessUnitCodes.Osan };
        BusinessUnitRequestContextFeature.Set(context, new(BusinessUnitAccessStatuses.Selected, UserId, osan, [BusinessUnitCodes.Osan], false, "synthetic"));
        var selected = new DatabaseConnectionStringProvider(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["BusinessUnits:Enabled"] = "true" }).Build(), new HttpContextAccessor { HttpContext = context });
        var readonlyUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim(QmsClaimTypes.Permission, QmsPermissions.ProjectRead)], "test"));
        Assert.IsType<ForbidHttpResult>(await OsanProgressEndpointExtensions.AuthorizeProjectAsync(Guid.NewGuid(), QmsPermissions.ManufacturingUpdate,
            new OsanProjectStore(legacy), selected, readonlyUser, ct));
    }
}
