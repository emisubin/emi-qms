using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task PhotoEditRequest_RequiresTrimsAndReturnsReasonWhilePreservingLegacyNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var config = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot, provider, config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync(
            "insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'photo-reason','Worker',true)",
            ct,
            ("actor", UserId));
        var projects = new OsanProjectStore(provider);
        var progress = new OsanProgressStore(provider);
        var edits = new OsanPhotoEditStore(provider);
        var project = (await projects.CreateAsync(Normalize(ValidRequest(quantity: 1)), UserId, ct)).Value!.Project;
        var target = project.Targets[0];
        Assert.Equal(OsanProgressMutationStatus.Success, (await progress.CompleteAsync(project.ProjectId,
            new(Guid.NewGuid(), "individual", 1, [new(target.TargetId, 1)], [], "최초 완료"), UserId, ct, true)).Status);

        Assert.Equal(400, (await edits.RequestAsync(project.ProjectId,
            new(Guid.NewGuid(), target.TargetId, 1, "   "), UserId, ct)).Status);
        Assert.Equal(0L, await database.ReadScalarAsync<long>("select count(*) from osan_photo_edit_requests", ct));

        var requestId = Guid.NewGuid();
        Assert.Equal(200, (await edits.RequestAsync(project.ProjectId,
            new(requestId, target.TargetId, 1, "  흐린 사진을 선명한 사진으로 교체합니다.  "), UserId, ct)).Status);
        const string expected = "흐린 사진을 선명한 사진으로 교체합니다.";
        Assert.Equal(expected, await database.ReadScalarAsync<string>(
            "select reason from osan_photo_edit_requests where id=@id", ct, ("id", requestId)));
        Assert.Equal(expected, Assert.Single(await edits.ListAsync(project.ProjectId, ct)).Reason);
        Assert.Equal(expected, Assert.Single(await new OsanPolicyStore(provider).PendingApprovalsAsync(ct)).Reason);

        await database.ExecuteAsync("update osan_photo_edit_requests set reason=null where id=@id", ct, ("id", requestId));
        Assert.Null(Assert.Single(await new OsanPolicyStore(provider).PendingApprovalsAsync(ct)).Reason);
    }

    [Fact]
    public async Task DeliveryHold_PreservesWorkAndDateIncludesHomeAndUsesConcurrentEditToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var config = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot, provider, config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'hold-test','Admin',true)", ct, ("actor", UserId));
        var projects = new OsanProjectStore(provider);
        var input = Normalize(ValidRequest(quantity: 1));
        var created = (await projects.CreateAsync(input, UserId, ct)).Value!.Project;
        var id = created.ProjectId;
        var token = created.EditToken;
        Assert.Equal(400, (await projects.ManageAsync(id, token, input, null, UserId, ct, true, " ")).Status);
        Assert.False((await projects.GetAsync(id, ct))!.DeliveryHold);
        Assert.Equal(200, (await projects.ManageAsync(id, token, input, null, UserId, ct, true, "고객 납기 보류")).Status);
        var held = (await projects.GetAsync(id, ct))!;
        Assert.True(held.DeliveryHold);
        Assert.Equal(created.DeliveryDate, held.DeliveryDate);
        Assert.NotEqual(token, held.EditToken);
        Assert.Equal(409, (await projects.ManageAsync(id, token, input, null, UserId, ct, false, "stale")).Status);
        var dashboard = new OsanDashboardStore(provider, TimeProvider.System);
        var scope = new Emi.Qms.Api.Projects.ProjectAccessScope(true, []);
        var home = await dashboard.GetAsync(new("", "All", 1, 10, "home"), scope, ct);
        Assert.Equal("Hold", Assert.Single(home.Items).Status); Assert.Equal(1, home.Summary.TotalCount); Assert.Equal(1, home.Summary.HoldCount); Assert.Equal(0, home.Summary.NotStartedCount);
        var list = await dashboard.GetAsync(new("", "All", 1, 10), scope, ct);
        Assert.True(Assert.Single(list.Items).DeliveryHold);
        var progress = new OsanProgressStore(provider);
        var target = (await progress.GetAsync(id, ct))!.Targets[0];
        Assert.Equal(OsanProgressMutationStatus.Success, (await progress.CompleteAsync(id,
            new(Guid.NewGuid(), "individual", 1, [new(target.TargetId, target.Version)], [], "admin checked"), UserId, ct, true)).Status);
        Assert.Equal(200, (await projects.ManageAsync(id, held.EditToken, input, null, UserId, ct)).Status);
        Assert.True((await projects.GetAsync(id, ct))!.DeliveryHold);
        Assert.Equal(200, (await projects.ManageAsync(id, held.EditToken, input, null, UserId, ct, false, "납기 재개")).Status);
        home = await dashboard.GetAsync(new("", "All", 1, 10, "home"), scope, ct);
        Assert.Single(home.Items); Assert.Equal(1, home.Summary.TotalCount);
        Assert.Equal(1, (await progress.GetAsync(id, ct))!.CompletedStepCount);
        Assert.Equal(2L, await database.ReadScalarAsync<long>("select count(*) from osan_project_management_history where after_json->>'HoldReason' is not null", ct));
    }

    [Fact]
    public async Task Management_PreservesEvidenceLocksQuantityAndConsumesOnlyApprovedPhotoRequest()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var database=await PostgreSqlTestDatabase.CreateAsync(ct);
        var config=database.CreateConfiguration();var provider=new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot,provider,config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        var other=Guid.NewGuid();
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'mgmt-test','Admin',true),(@other,'mgmt-other','Worker',true);
            """,ct,("actor",UserId),("other",other));
        var projects=new OsanProjectStore(provider);var progress=new OsanProgressStore(provider);var edits=new OsanPhotoEditStore(provider);
        var created=await projects.CreateAsync(Normalize(ValidRequest(quantity:3)),UserId,ct);
        var id=created.Value!.Project.ProjectId;var p=(await projects.GetAsync(id,ct))!;
        var token=OsanProjectStore.EditToken(p);
        Assert.Equal(200,(await projects.ManageAsync(id,token,Normalize(ValidRequest(title:"Equipment",quantity:3)),null,UserId,ct)).Status);
        Assert.Equal(409,(await projects.ManageAsync(id,token,Normalize(ValidRequest(title:"Stale")),null,UserId,ct)).Status);
        p=(await projects.GetAsync(id,ct))!;Assert.Equal(3,p.Targets.Count);Assert.Equal("Equipment",p.Title);
        var originalTargets=p.Targets.Select(t=>t.TargetId).ToArray();
        Assert.Equal(400,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:1)),null,UserId,ct)).Status);
        Assert.Equal(400,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:2)),null,UserId,ct)).Status);
        p=(await projects.GetAsync(id,ct))!;Assert.Equal(originalTargets,p.Targets.Select(t=>t.TargetId));
        Assert.Equal(3L,await database.ReadScalarAsync<long>("select count(*) from osan_project_targets",ct));
        Assert.Equal(21,(await progress.GetAsync(id,ct))!.TotalStepCount);
        var detail=(await progress.GetAsync(id,ct))!;var target=detail.Targets[0];
        var completed=await progress.CompleteAsync(id,new(Guid.NewGuid(),"individual",1,[new(target.TargetId,target.Version)],[],"admin checked"),UserId,ct,true);
        Assert.Equal(OsanProgressMutationStatus.Success,completed.Status);
        Assert.Equal(400,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:4)),null,UserId,ct)).Status);
        var request=new OsanPhotoEditRequest(Guid.NewGuid(),target.TargetId,1,"  흐린 사진을 선명한 사진으로 교체합니다.  ");
        Assert.Equal(200,(await edits.RequestAsync(id,request,other,ct)).Status);
        var photo=new OsanProgressPhotoInput("color.png","image/png",[1,2,3],new string('a',64));
        var input=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(target.TargetId,2)],[photo]);
        Assert.Equal(403,(await edits.SaveAsync(id,request.RequestId,input,other,ct)).Status);
        Assert.Equal(200,(await edits.ApproveAsync(id,request.RequestId,UserId,ct)).Status);

        Assert.Equal(403,(await edits.SaveAsync(id,request.RequestId,input with{StageSequence=2},other,ct)).Status);
        var results=await Task.WhenAll(edits.SaveAsync(id,request.RequestId,input,other,ct),edits.SaveAsync(id,request.RequestId,input,other,ct));
        Assert.All(results,r=>Assert.Equal(200,r.Status));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_revision_files",ct));
        Assert.Equal(400,(await edits.SaveAsync(id,request.RequestId,input with{Photos=[]},other,ct)).Status);
        detail=(await progress.GetAsync(id,ct))!;var saved=Assert.Single(detail.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Photos);
        Assert.Equal(photo.Content,(await progress.GetPhotoAsync(id,saved.PhotoId,ct))!.Content);
        Assert.Equal("Completed",detail.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Status);
        Assert.Equal(1,detail.CompletedStepCount);
        var next=new OsanPhotoEditRequest(Guid.NewGuid(),target.TargetId,1,"코멘트를 바로잡습니다.");
        Assert.Equal(200,(await edits.RequestAsync(id,next,other,ct)).Status);
        await edits.ApproveAsync(id,next.RequestId,UserId,ct);
        Assert.Equal(200,(await edits.SaveAsync(id,next.RequestId,input with{Photos=[],Comment="admin checked"},UserId,ct,true)).Status);
        Assert.Empty((await progress.GetAsync(id,ct))!.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Photos);
        Assert.NotNull(await progress.GetPhotoAsync(id,saved.PhotoId,ct));
        Assert.Equal(2,(await edits.ListAsync(id,ct)).Count);
        p=(await projects.GetAsync(id,ct))!;
        Assert.Equal(200,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),null,"duplicate",UserId,ct)).Status);
        Assert.Null(await projects.GetAsync(id,ct));Assert.Null(await projects.GetAccessRecordAsync(id,ct));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_revision_files",ct));
        Assert.Equal(2L,await database.ReadScalarAsync<long>("select count(*) from osan_project_management_history",ct));
        Assert.Equal(404,(await edits.RequestAsync(id,new(Guid.NewGuid(),target.TargetId,1,"삭제 프로젝트 정정"),other,ct)).Status);
    }
}
