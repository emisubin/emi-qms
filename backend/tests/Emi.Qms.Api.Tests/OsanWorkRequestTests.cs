using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    private static readonly Guid WorkRequestRecipientOne = Guid.Parse("89000000-0000-0000-0000-000000000021");
    private static readonly Guid WorkRequestRecipientTwo = Guid.Parse("89000000-0000-0000-0000-000000000022");

    [Fact]
    public async Task WorkRequest_IsRecipientScopedIdempotentAndHistoryOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration).ApplyAndVerifyAsync(ct);
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,department_id,is_active)
            values(@requester,'work-request-admin','Request Admin',(select id from departments where code='administration'),true),
                  (@one,'work-request-one','Recipient One',(select id from departments where code='manufacturing'),true),
                  (@two,'work-request-two','Recipient Two',(select id from departments where code='quality'),true);
            """, ct, ("requester", UserId), ("one", WorkRequestRecipientOne), ("two", WorkRequestRecipientTwo));

        var projects = new OsanProjectStore(provider);
        var progress = new OsanProgressStore(provider);
        var store = new OsanWorkRequestStore(provider);
        var projectId = (await projects.CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct))
            .Value!.Project.ProjectId;
        var before = (await progress.GetAsync(projectId, ct))!;
        var target = before.Targets[0];
        var operationId = Guid.NewGuid();
        var request = new CreateOsanWorkRequest(
            operationId, target.TargetId, 3, [WorkRequestRecipientTwo, WorkRequestRecipientOne]);

        var candidates = await store.ListRecipientsAsync(projectId, ct);
        Assert.Contains(candidates.Recipients, user =>
            user.UserId == WorkRequestRecipientOne && user.DepartmentName is not null);

        var created = await store.CreateAsync(projectId, request, UserId, ct);
        Assert.Equal(OsanWorkRequestStatus.Success, created.Status);
        Assert.False(created.Value!.Replayed);
        Assert.Equal("배선검사", created.Value.StageName);
        Assert.Equal(2, created.Value.Recipients.Count);

        var after = (await progress.GetAsync(projectId, ct))!;
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(target.Status, after.Targets[0].Status);
        Assert.Equal(target.Version, after.Targets[0].Version);
        Assert.Equal(target.Steps[2].Status, after.Targets[0].Steps[2].Status);

        var replay = await store.CreateAsync(projectId, request, UserId, ct);
        Assert.Equal(OsanWorkRequestStatus.Success, replay.Status);
        Assert.True(replay.Value!.Replayed);
        Assert.Equal(created.Value.RequestedAtUtc, replay.Value.RequestedAtUtc);
        Assert.Equal(OsanWorkRequestStatus.Conflict,
            (await store.CreateAsync(projectId, request with { StageSequence = 4 }, UserId, ct)).Status);

        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_stage_work_requests where operation_id=@operation", ct,
            ("operation", operationId)));
        Assert.Equal(2L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_stage_work_request_recipients where operation_id=@operation", ct,
            ("operation", operationId)));
        Assert.Equal(1L, await database.ReadScalarAsync<long>("""
            select count(*) from osan_notification_events event
            join notifications notification on notification.id=event.notification_id
            where event.event_kind='StepWorkRequested' and notification.project_id=@project;
            """, ct, ("project", projectId)));
        Assert.Equal(2L, await database.ReadScalarAsync<long>("""
            select count(*) from notification_recipients recipient
            join osan_notification_events event on event.notification_id=recipient.notification_id
            where event.event_kind='StepWorkRequested';
            """, ct));
        Assert.Equal(2L, await database.ReadScalarAsync<long>("""
            select count(*) from notification_deliveries delivery
            join osan_notification_events event on event.notification_id=delivery.notification_id
            where event.event_kind='StepWorkRequested' and delivery.channel='Mail';
            """, ct));

        var history = (await progress.HistoryAsync(projectId, target.Steps[2].StepId, ct))!;
        var historyRequest = Assert.Single(history, item => item.EventType == "WorkRequested");
        Assert.Equal(2, historyRequest.Recipients!.Count);
        Assert.Contains(historyRequest.Recipients, user => user.UserId == WorkRequestRecipientTwo);

        var racingOperation = Guid.NewGuid();
        var racingRequest = request with { OperationId = racingOperation };
        var racing = await Task.WhenAll(
            store.CreateAsync(projectId, racingRequest, UserId, ct),
            store.CreateAsync(projectId, racingRequest, UserId, ct));
        Assert.All(racing, result => Assert.Equal(OsanWorkRequestStatus.Success, result.Status));
        Assert.Single(racing, result => result.Value!.Replayed);
        Assert.Single(racing, result => !result.Value!.Replayed);
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_stage_work_requests where operation_id=@operation", ct,
            ("operation", racingOperation)));
    }

    [Fact]
    public async Task WorkRequest_RejectsInvalidOrInactiveRecipients()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration).ApplyAndVerifyAsync(ct);
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values(@requester,'work-request-admin-2','Request Admin',true),
                  (@one,'work-request-inactive','Inactive Recipient',false);
            """, ct, ("requester", UserId), ("one", WorkRequestRecipientOne));
        var projectId = (await new OsanProjectStore(provider)
            .CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct)).Value!.Project.ProjectId;
        var target = (await new OsanProgressStore(provider).GetAsync(projectId, ct))!.Targets[0];
        var store = new OsanWorkRequestStore(provider);

        var invalid = await store.CreateAsync(projectId,
            new(Guid.Empty, Guid.Empty, 8, []), UserId, ct);
        Assert.Equal(OsanWorkRequestStatus.Validation, invalid.Status);
        Assert.Equal(4, invalid.Errors!.Count);

        var inactive = await store.CreateAsync(projectId,
            new(Guid.NewGuid(), target.TargetId, 1, [WorkRequestRecipientOne]), UserId, ct);
        Assert.Equal(OsanWorkRequestStatus.Validation, inactive.Status);
        Assert.Contains("recipientIds", inactive.Errors!.Keys);
        Assert.Equal(0L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_stage_work_requests", ct));
    }
}
