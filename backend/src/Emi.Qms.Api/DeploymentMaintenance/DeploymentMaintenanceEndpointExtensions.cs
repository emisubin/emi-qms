using System.Security.Claims;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.DeploymentMaintenance;

public static class DeploymentMaintenanceEndpointExtensions
{
    public static IEndpointRouteBuilder MapDeploymentMaintenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var api=app.MapGroup("/api/maintenance").RequireAuthorization();
        api.MapGet("",async(DeploymentMaintenanceStore store,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var actor=ProjectEndpointExtensions.GetCurrentUserId(user);
            return actor is null ? Results.Unauthorized() : Results.Ok(await store.ReadAsync(actor,ct));
        }).WithName("GetDeploymentMaintenance");
        api.MapPost("/{releaseId:guid}/popup/{version:int}/claim",async(Guid releaseId,int version,
            DeploymentMaintenanceStore store,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var actor=ProjectEndpointExtensions.GetCurrentUserId(user);
            return actor is null ? Results.Unauthorized() : Results.Ok(new {
                claimed=await store.ClaimPopupAsync(releaseId,version,actor.Value,ct) });
        }).WithName("ClaimDeploymentMaintenancePopup");
        return app;
    }
}
