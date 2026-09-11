using Emi.Qms.Api.OsanProjects;
using Xunit;
namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task StageRecords_ResetRejectHistoryAndSharedOneUseEdit_PreserveEvidence()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var database=await PostgreSqlTestDatabase.CreateAsync(ct);
        var config=database.CreateConfiguration();var provider=new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot,provider,config).ApplyAndVerifyAsync(ct);
        var other=Guid.NewGuid();
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'stage-admin','Admin',true),(@other,'stage-worker','Worker',true)",ct,("actor",UserId),("other",other));
        var projects=new OsanProjectStore(provider);var progress=new OsanProgressStore(provider);var edits=new OsanPhotoEditStore(provider);
        var created=await projects.CreateAsync(Normalize(ValidRequest(quantity:1)),UserId,ct);var id=created.Value!.Project.ProjectId;
        var detail=(await progress.GetAsync(id,ct))!;var target=detail.Targets[0];
        var empty=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(target.TargetId,target.Version)],[],"verified");
        Assert.Equal(OsanProgressMutationStatus.Validation,(await progress.CompleteAsync(id,empty,other,ct)).Status);
        Assert.Equal(OsanProgressMutationStatus.Validation,(await progress.CompleteAsync(id,empty with{Comment="  "},UserId,ct,true)).Status);
        for(var stage=1;stage<=7;stage++)
        {
            target=(await progress.GetAsync(id,ct))!.Targets[0];
            var complete=empty with{OperationId=Guid.NewGuid(),StageSequence=stage,Targets=[new(target.TargetId,target.Version)]};
            Assert.Equal(OsanProgressMutationStatus.Success,(await progress.CompleteAsync(id,complete,UserId,ct,true)).Status);
        }
        detail=(await progress.GetAsync(id,ct))!;Assert.Equal("Completed",detail.Status);target=detail.Targets[0];var step=target.Steps[0];
        var action=new OsanStageActionRequest(Guid.NewGuid(),"redo",target.Version);
        Assert.Equal(200,(await progress.StageActionAsync(id,step.StepId,action,"Reset",UserId,ct)).Status);
        Assert.Equal(200,(await progress.StageActionAsync(id,step.StepId,action,"Reset",UserId,ct)).Status);
        detail=(await progress.GetAsync(id,ct))!;Assert.Equal(6,detail.CompletedStepCount);Assert.Equal("InProgress",detail.Status);
        Assert.All(detail.Targets[0].Steps.Skip(1),s=>Assert.Equal("Completed",s.Status));Assert.Empty(detail.Targets[0].Steps[0].Comment);
        Assert.Equal(2,(await progress.HistoryAsync(id,step.StepId,ct))!.Count);
        target=detail.Targets[0];
        var photo=new OsanProgressPhotoInput("example.png","image/png",[1,2,3],new string('a',64));
        var completion=empty with{OperationId=Guid.NewGuid(),Targets=[new(target.TargetId,target.Version)],Photos=[photo],Comment="first"};
        Assert.Equal(OsanProgressMutationStatus.Success,(await progress.CompleteAsync(id,completion,other,ct)).Status);
        detail=(await progress.GetAsync(id,ct))!;target=detail.Targets[0];step=target.Steps[0];var original=Assert.Single(step.Photos).PhotoId;
        Assert.Equal("Completed",detail.Status);
        Assert.Equal(200,(await progress.StageActionAsync(id,step.StepId,new(Guid.NewGuid(),"fix",target.Version),"Reject",UserId,ct)).Status);
        var active=(await edits.ListAsync(id,ct)).Single(r=>r.UsedAt is null && r.InvalidatedAt is null);
        Assert.Equal(409,(await progress.StageActionAsync(id,step.StepId,new(Guid.NewGuid(),"again",target.Version+1),"Reject",UserId,ct)).Status);
        var edit=completion with{Photos=[],RetainedPhotoIds=[original],Comment="updated"};
        var saves=await Task.WhenAll(edits.SaveAsync(id,active.RequestId,edit,other,ct),edits.SaveAsync(id,active.RequestId,edit,UserId,ct));
        Assert.Single(saves,r=>r.Status==200);Assert.Single(saves,r=>r.Status==409);
        detail=(await progress.GetAsync(id,ct))!;step=detail.Targets[0].Steps[0];Assert.Equal("updated",step.Comment);Assert.False(step.EditOpen);Assert.False(step.Rejected);
        Assert.NotNull(await progress.GetPhotoAsync(id,original,ct));Assert.Single(step.Photos);
        Assert.Contains((await progress.HistoryAsync(id,step.StepId,ct))!,r=>r.EventType=="Reject" && r.Reason=="fix");
        var req=new OsanPhotoEditRequest(Guid.NewGuid(),target.TargetId,1);await edits.RequestAsync(id,req,other,ct);await edits.ApproveAsync(id,req.RequestId,UserId,ct);
        Assert.Equal(400,(await edits.SaveAsync(id,req.RequestId,edit with{RetainedPhotoIds=[Guid.NewGuid()]},other,ct)).Status);
        Assert.Equal(400,(await edits.SaveAsync(id,req.RequestId,edit with{RetainedPhotoIds=[]},other,ct)).Status);
        Assert.Equal(200,(await edits.SaveAsync(id,req.RequestId,edit with{RetainedPhotoIds=[],Comment="admin only"},UserId,ct,true)).Status);
        step=(await progress.GetAsync(id,ct))!.Targets[0].Steps[0];Assert.Empty(step.Photos);Assert.Equal(UserId,step.CompletedByUserId);Assert.Equal("admin only",step.Comment);
        Assert.NotNull(await progress.GetPhotoAsync(id,original,ct));
    }
}
