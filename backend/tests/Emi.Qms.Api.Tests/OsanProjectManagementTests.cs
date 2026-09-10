using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task Management_PreservesEvidenceLocksQuantityAndConsumesOnlyApprovedPhotoRequest()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var database=await PostgreSqlTestDatabase.CreateAsync(ct);
        var config=database.CreateConfiguration();var provider=new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot,provider,config).ApplyAndVerifyAsync(ct);
        var managementMigration = await File.ReadAllTextAsync(
            Path.Combine(database.RepositoryRoot, "database", "migrations", "0094_osan_management_photo_revisions.sql"),
            ct);
        await database.ExecuteAsync(managementMigration, ct);
        var other=Guid.NewGuid();
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'mgmt-test','Admin',true),(@other,'mgmt-other','Worker',true);
            """,ct,("actor",UserId),("other",other));
        var projects=new OsanProjectStore(provider);var progress=new OsanProgressStore(provider);var edits=new OsanPhotoEditStore(provider);
        var created=await projects.CreateAsync(Normalize(ValidRequest(quantity:1)),UserId,ct);
        var id=created.Value!.Project.ProjectId;var p=(await projects.GetAsync(id,ct))!;
        var token=OsanProjectStore.EditToken(p);
        Assert.Equal(200,(await projects.ManageAsync(id,token,Normalize(ValidRequest(title:"Equipment",quantity:2)),null,UserId,ct)).Status);
        Assert.Equal(409,(await projects.ManageAsync(id,token,Normalize(ValidRequest(title:"Stale")),null,UserId,ct)).Status);
        p=(await projects.GetAsync(id,ct))!;Assert.Equal(2,p.Targets.Count);Assert.Equal("Equipment",p.Title);
        var originalTargets=p.Targets.Select(t=>t.TargetId).ToArray();
        Assert.Equal(200,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:1)),null,UserId,ct)).Status);
        p=(await projects.GetAsync(id,ct))!;Assert.Single(p.Targets);
        Assert.Equal(2L,await database.ReadScalarAsync<long>("select count(*) from osan_project_targets",ct));
        Assert.Equal(7,(await progress.GetAsync(id,ct))!.TotalStepCount);
        Assert.Equal(200,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:2)),null,UserId,ct)).Status);
        p=(await projects.GetAsync(id,ct))!;Assert.Equal(originalTargets,p.Targets.Select(t=>t.TargetId));
        var detail=(await progress.GetAsync(id,ct))!;var target=detail.Targets[0];
        var completed=await progress.CompleteAsync(id,new(Guid.NewGuid(),"individual",1,[new(target.TargetId,target.Version)],[]),UserId,ct);
        Assert.Equal(OsanProgressMutationStatus.Success,completed.Status);
        Assert.Equal(409,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),Normalize(ValidRequest(quantity:3)),null,UserId,ct)).Status);
        var request=new OsanPhotoEditRequest(Guid.NewGuid(),target.TargetId,1);
        Assert.Equal(200,(await edits.RequestAsync(id,request,other,ct)).Status);
        var photo=new OsanProgressPhotoInput("color.png","image/png",[1,2,3],new string('a',64));
        var input=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(target.TargetId,2)],[photo]);
        Assert.Equal(403,(await edits.SaveAsync(id,request.RequestId,input,other,ct)).Status);
        Assert.Equal(200,(await edits.ApproveAsync(id,request.RequestId,UserId,ct)).Status);
        Assert.Equal(403,(await edits.SaveAsync(id,request.RequestId,input,UserId,ct)).Status);
        Assert.Equal(403,(await edits.SaveAsync(id,request.RequestId,input with{StageSequence=2},other,ct)).Status);
        var results=await Task.WhenAll(edits.SaveAsync(id,request.RequestId,input,other,ct),edits.SaveAsync(id,request.RequestId,input,other,ct));
        Assert.All(results,r=>Assert.Equal(200,r.Status));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_revision_files",ct));
        Assert.Equal(409,(await edits.SaveAsync(id,request.RequestId,input with{Photos=[]},other,ct)).Status);
        detail=(await progress.GetAsync(id,ct))!;var saved=Assert.Single(detail.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Photos);
        Assert.Equal(photo.Content,(await progress.GetPhotoAsync(id,saved.PhotoId,ct))!.Content);
        Assert.Equal("Completed",detail.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Status);
        Assert.Equal(1,detail.CompletedStepCount);
        var next=new OsanPhotoEditRequest(Guid.NewGuid(),target.TargetId,1);
        Assert.Equal(200,(await edits.RequestAsync(id,next,other,ct)).Status);
        await edits.ApproveAsync(id,next.RequestId,UserId,ct);
        Assert.Equal(200,(await edits.SaveAsync(id,next.RequestId,input with{Photos=[]},other,ct)).Status);
        Assert.Empty((await progress.GetAsync(id,ct))!.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Photos);
        Assert.NotNull(await progress.GetPhotoAsync(id,saved.PhotoId,ct));
        Assert.Equal(2,(await edits.ListAsync(id,ct)).Count);
        p=(await projects.GetAsync(id,ct))!;
        Assert.Equal(200,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),null,"duplicate",UserId,ct)).Status);
        Assert.Null(await projects.GetAsync(id,ct));Assert.Null(await projects.GetAccessRecordAsync(id,ct));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_revision_files",ct));
        Assert.Equal(4L,await database.ReadScalarAsync<long>("select count(*) from osan_project_management_history",ct));
        Assert.Equal(404,(await edits.RequestAsync(id,new(Guid.NewGuid(),target.TargetId,1),other,ct)).Status);
    }
}
