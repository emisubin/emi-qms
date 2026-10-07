using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task DirectPhotoEdit_RequiresAndTrimsReasonWithoutCreatingApprovalRequests()
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

        var current=(await progress.GetAsync(project.ProjectId,ct))!.Targets[0];
        var step=current.Steps[0].StepId;
        var input=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(current.TargetId,current.Version)],[],"정정",Reason:"   ");
        Assert.Equal(400,(await edits.SaveAsync(project.ProjectId,step,input,UserId,ct,true)).Status);
        Assert.Equal(0L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_edit_requests",ct));
        input=input with{Reason="  흐린 사진 기록 정정  "};
        Assert.Equal(200,(await edits.SaveAsync(project.ProjectId,step,input,UserId,ct,true)).Status);
        Assert.Equal("흐린 사진 기록 정정",Assert.Single((await progress.HistoryAsync(project.ProjectId,step,ct))!,r=>r.EventType=="Edit").Reason);
        Assert.Equal(0L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_edit_requests",ct));

    }

    [Fact]
    public async Task DirectPhotoEdit_PreservesLegacyApprovalEvidenceAndUsesGateDepartment()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var database=await PostgreSqlTestDatabase.CreateAsync(ct);
        var config=database.CreateConfiguration();var provider=new DatabaseConnectionStringProvider(config);
        await CreateMigrationRunner(database.RepositoryRoot,provider,config).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database,ct);
        await database.ExecuteAsync("insert into qms_users(id,development_user_key,display_name,is_active,department_id) values(@actor,'legacy-evidence','Quality',true,(select id from departments where code='quality'))",ct,("actor",UserId));
        var projects=new OsanProjectStore(provider);var progress=new OsanProgressStore(provider);var edits=new OsanPhotoEditStore(provider);
        var project=(await projects.CreateAsync(Normalize(ValidRequest(quantity:1)),UserId,ct)).Value!.Project;
        var target=(await progress.GetAsync(project.ProjectId,ct))!.Targets[0];
        await progress.CompleteAsync(project.ProjectId,new(Guid.NewGuid(),"individual",1,[new(target.TargetId,target.Version)],[],"legacy original"),UserId,ct,true);
        target=(await progress.GetAsync(project.ProjectId,ct))!.Targets[0];var step=target.Steps[0].StepId;
        var request=Guid.NewGuid();var file=Guid.NewGuid();var record=Guid.NewGuid();
        await database.ExecuteAsync("""
            insert into osan_photo_edit_requests(id,project_id,target_id,step_id,requested_by,approved_by,approved_at,used_at,fingerprint,saved_by,reason)
            values(@request,@project,@target,@step,@actor,@actor,now(),now(),'legacy',@actor,'old reason');
            insert into osan_photo_revision_files(id,request_id,display_order,original_file_name,normalized_mime,sha256,content)
            values(@file,@request,1,'legacy.png','image/png',repeat('a',64),decode('010203','hex'));
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,comment,reason,photo_ids)
            values(@record,@request,@project,@target,@step,'Edit',@actor,'old comment',null,array[@file]);
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,comment,reason,photo_ids)
            values(gen_random_uuid(),@request,@project,@target,@step,'Request',@actor,'',null,array[]::uuid[]),
                  (gen_random_uuid(),@request,@project,@target,@step,'Approve',@actor,'',null,array[]::uuid[]);
            update osan_project_target_steps set current_record_id=@record where id=@step;
            """,ct,("request",request),("project",project.ProjectId),("target",target.TargetId),("step",step),("actor",UserId),("file",file),("record",record));
        var input=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(target.TargetId,target.Version)],[],"new comment",[file],"new reason");
        Assert.Equal(200,(await edits.SaveAsync(project.ProjectId,step,input,UserId,ct)).Status);
        Assert.NotNull(await progress.GetPhotoAsync(project.ProjectId,file,ct));
        var history=(await progress.HistoryAsync(project.ProjectId,step,ct))!;
        Assert.Contains(history,item=>item.EventType=="Request" && item.Reason=="old reason");
        Assert.Contains(history,item=>item.EventType=="Approve" && item.Reason=="old reason");
        Assert.Equal(3L,await database.ReadScalarAsync<long>("select count(*) from osan_stage_records where operation_id=@request and reason is null",ct,("request",request)));
        Assert.Contains(history,item=>item.Comment=="old comment" && item.Reason=="old reason" && item.Photos.Single().PhotoId==file);
        Assert.Contains(history,item=>item.Comment=="new comment" && item.Reason=="new reason" && item.Photos.Single().PhotoId==file);
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_photo_edit_requests",ct));
        await database.ExecuteAsync("delete from osan_gate_departments where stage_sequence=1 and department_id=(select department_id from qms_users where id=@actor)",ct,("actor",UserId));
        target=(await progress.GetAsync(project.ProjectId,ct))!.Targets[0];
        input=input with{OperationId=Guid.NewGuid(),Targets=[new(target.TargetId,target.Version)]};
        Assert.Equal(403,(await edits.SaveAsync(project.ProjectId,step,input,UserId,ct)).Status);
        Assert.Equal(200,(await edits.SaveAsync(project.ProjectId,step,input,UserId,ct,true)).Status);
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
    public async Task Management_PreservesEvidenceLocksQuantityAndAllowsRepeatedAuthorizedEdits()
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
        var step=completed.Value!.Project.Targets[0].Steps[0].StepId;
        var photo=new OsanProgressPhotoInput("color.png","image/png",[1,2,3],new string('a',64));
        var input=new CompleteOsanProgressInput(Guid.NewGuid(),"individual",1,[new(target.TargetId,2)],[photo],Reason:"사진 정정");
        Assert.Equal(403,(await edits.SaveAsync(id,step,input,other,ct)).Status);
        await database.ExecuteAsync("update qms_users set department_id=(select id from departments where code='manufacturing') where id=@id",ct,("id",other));
        Assert.Equal(404,(await edits.SaveAsync(id,step,input with{StageSequence=2},other,ct)).Status);
        var results=await Task.WhenAll(edits.SaveAsync(id,step,input,other,ct),edits.SaveAsync(id,step,input,other,ct));
        Assert.All(results,r=>Assert.Equal(200,r.Status));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_direct_edit_files",ct));
        Assert.Equal(409,(await edits.SaveAsync(id,step,input with{OperationId=Guid.NewGuid()},other,ct)).Status);
        detail=(await progress.GetAsync(id,ct))!;var current=detail.Targets.Single(t=>t.TargetId==target.TargetId);
        var saved=Assert.Single(current.Steps[0].Photos);
        Assert.Equal(photo.Content,(await progress.GetPhotoAsync(id,saved.PhotoId,ct))!.Content);
        Assert.Equal(1,detail.CompletedStepCount);
        Assert.Equal(200,(await edits.SaveAsync(id,step,input with{OperationId=Guid.NewGuid(),Targets=[new(target.TargetId,current.Version)],Photos=[],Comment="admin checked"},UserId,ct,true)).Status);
        Assert.Empty((await progress.GetAsync(id,ct))!.Targets.Single(t=>t.TargetId==target.TargetId).Steps[0].Photos);
        Assert.NotNull(await progress.GetPhotoAsync(id,saved.PhotoId,ct));
        Assert.Equal(2L,await database.ReadScalarAsync<long>("select count(*) from osan_stage_records where event_type='Edit'",ct));
        p=(await projects.GetAsync(id,ct))!;
        Assert.Equal(200,(await projects.ManageAsync(id,OsanProjectStore.EditToken(p),null,"duplicate",UserId,ct)).Status);
        Assert.Null(await projects.GetAsync(id,ct));Assert.Null(await projects.GetAccessRecordAsync(id,ct));
        Assert.Equal(1L,await database.ReadScalarAsync<long>("select count(*) from osan_direct_edit_files",ct));
        Assert.Equal(2L,await database.ReadScalarAsync<long>("select count(*) from osan_project_management_history",ct));
        Assert.Equal(404,(await edits.SaveAsync(id,step,input,other,ct)).Status);
    }
}
