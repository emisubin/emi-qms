using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task PersonalHome_GuardRejectsWrongBusinessUnitMissingPermissionAndMissingActorBeforeQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var legacy = new DatabaseConnectionStringProvider(new ConfigurationBuilder().Build());
        var reader = new ClaimsPrincipal(new ClaimsIdentity([new Claim(QmsClaimTypes.Permission, QmsPermissions.ProjectRead)], "test"));
        var wrong = await OsanProjectEndpointExtensions.GetPersonalHomeAsync(legacy,TimeProvider.System,reader,ct);
        Assert.Equal(403,Assert.IsAssignableFrom<IStatusCodeHttpResult>(wrong).StatusCode);
        var context = new DefaultHttpContext();
        var osan = legacy.GetCurrentBusinessUnit()! with { Code = BusinessUnitCodes.Osan };
        BusinessUnitRequestContextFeature.Set(context,new(BusinessUnitAccessStatuses.Selected,UserId,osan,[BusinessUnitCodes.Osan],false,"synthetic"));
        var provider = new DatabaseConnectionStringProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["BusinessUnits:Enabled"]="true" }).Build(),new HttpContextAccessor { HttpContext=context });
        Assert.IsType<ForbidHttpResult>(await OsanProjectEndpointExtensions.GetPersonalHomeAsync(provider,TimeProvider.System,
            new ClaimsPrincipal(new ClaimsIdentity([],"test")),ct));
        Assert.IsType<UnauthorizedHttpResult>(await OsanProjectEndpointExtensions.GetPersonalHomeAsync(provider,TimeProvider.System,reader,ct));
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(QmsClaimTypes.BusinessUnitAccessStatus, BusinessUnitAccessStatuses.Selected)], "test"));
        context.Response.Body = new MemoryStream();
        foreach (var (method, path, allowed) in new[] {
            ("GET", "/api/osan/my/home", true), ("GET", "/api/osan/my/home/", true),
            ("POST", "/api/osan/my/home", false), ("GET", "/api/osan/my/home/other", false) })
        {
            var reachedEndpoint = false;
            context.Request.Method = method;
            context.Request.Path = path;
            await new BusinessUnitCapabilityMiddleware(_ => { reachedEndpoint = true; return Task.CompletedTask; })
                .InvokeAsync(context, provider);
            Assert.Equal(allowed, reachedEndpoint);
        }

    }

    [Fact]
    public async Task PersonalHome_RestrictsAssignmentsAndScope_AndResolvesPersonalTasksFromStageHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values(@actor,'home-actor','Home Actor',true),(@other,'home-other','Other Actor',true);
            delete from osan_customer_assignments where user_id=@actor;
            """, ct, ("actor", UserId), ("other", WorkRequestRecipientOne));
        var projects = new OsanProjectStore(provider);
        var progress = new OsanProgressStore(provider);
        var requests = new OsanWorkRequestStore(provider);
        var home = new OsanPersonalHomeStore(provider, TimeProvider.System);
        var p = (await projects.CreateAsync(Normalize(ValidRequest(quantity:1)), UserId, ct)).Value!.Project;
        var target = (await progress.GetAsync(p.ProjectId, ct))!.Targets[0];
        var step = target.Steps[0];
        await requests.CreateAsync(p.ProjectId, new(Guid.NewGuid(),target.TargetId,1,[UserId]), WorkRequestRecipientOne, ct);
        var all = new ProjectAccessScope(true, []);
        var unassigned = await home.GetAsync(UserId, all, ct);
        Assert.Empty(unassigned.Customers);
        Assert.Empty(unassigned.News);
        Assert.Empty(unassigned.Deadlines);
        Assert.Equal(0, unassigned.Summary.TotalCount);
        Assert.Equal("request",Assert.Single(unassigned.Tasks).Kind);
        var denied = await home.GetAsync(UserId,new(false,[]),ct);
        Assert.Empty(denied.Tasks);
        await database.ExecuteAsync("""
            insert into osan_customer_assignments(user_id,customer_id) values(@actor,@customer);
            update projects set delivery_date=current_date-2 where id=@project;
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,occurred_at_utc)
            values(@complete,@complete,@project,@target,@step,'Complete',@actor,now());
            update osan_project_target_steps set status='Completed',rejected=false where id=@step;
            """,ct,("actor",UserId),("customer",DefaultCustomerId),("project",p.ProjectId),
            ("target",target.TargetId),("step",step.StepId),("complete",Guid.NewGuid()));
        var assigned = await home.GetAsync(UserId,all,ct);
        Assert.Equal(1,assigned.Summary.TotalCount);
        Assert.Equal(1,assigned.Summary.InProgressCount);
        Assert.Equal(1,assigned.Summary.OverdueCount);
        Assert.Single(assigned.Deadlines);
        Assert.Single(assigned.News);
        Assert.Empty(assigned.Tasks);
        await database.ExecuteAsync("""
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id)
            select gen_random_uuid(),gen_random_uuid(),project_id,target_id,id,'IssueRegistered',@actor
            from osan_project_target_steps where project_id=@project and sequence_number in (2,3);
            insert into osan_stage_issues(id,project_id,target_id,step_id,registered_by_user_id,latest_record_id)
            select gen_random_uuid(),project_id,target_id,step_id,@actor,id from osan_stage_records
            where project_id=@project and event_type='IssueRegistered';
            """,ct,("actor",UserId),("project",p.ProjectId));
        var issues = await home.GetAsync(UserId,all,ct);
        Assert.Equal(1,issues.Summary.OpenIssueCount);
        Assert.Equal(1,Assert.Single(issues.Customers).OpenIssueCount);
        var scopedOut = await home.GetAsync(UserId,new(false,["unrelated-project-key"]),ct);
        Assert.Equal(0,scopedOut.Summary.TotalCount);
        Assert.Empty(scopedOut.News);
        Assert.Empty(scopedOut.Deadlines);
        await database.ExecuteAsync("""
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,reason)
            values(@reject,@reject,@project,@target,@step,'Reject',@other,'Fix input');
            update osan_project_target_steps set status='NotStarted',rejected=true where id=@step;
            update projects set osan_delivery_hold=true where id=@project;
            """,ct,("project",p.ProjectId),("target",target.TargetId),("step",step.StepId),
            ("other",WorkRequestRecipientOne),("reject",Guid.NewGuid()));
        var rejected = await home.GetAsync(UserId,all,ct);
        Assert.Equal("rejection",Assert.Single(rejected.Tasks).Kind);
        Assert.Equal("Fix input",rejected.Tasks[0].Comment);
        Assert.Equal(1,rejected.Summary.HoldCount);
        Assert.Equal(0,rejected.Summary.OverdueCount);
        Assert.Empty(rejected.Deadlines);
        Assert.Empty((await home.GetAsync(WorkRequestRecipientOne,all,ct)).Tasks);
        await database.ExecuteAsync("""
            update osan_project_target_steps set status='Completed',rejected=false where id=@step;
            update projects set osan_delivery_hold=false,status='Completed' where id=@project;
            """,ct,("step",step.StepId),("project",p.ProjectId));
        var completed = await home.GetAsync(UserId,all,ct);
        Assert.Empty(completed.Tasks);
        Assert.Equal(0,completed.TaskTotalCount);
        Assert.Equal(0,completed.Summary.OverdueCount);
        Assert.Equal(0,completed.Summary.TotalCount);
    }
}
