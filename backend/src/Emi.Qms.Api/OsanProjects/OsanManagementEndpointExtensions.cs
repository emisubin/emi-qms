using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.OsanProjects;

public static class OsanManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/osan/projects/{projectId:guid}").RequireAuthorization();
        api.MapGet("/management", async (Guid projectId, OsanProjectStore store,
            DatabaseConnectionStringProvider db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, store, db, user, false, ct);
            if (denied is not null) return denied;
            var p = await store.GetAsync(projectId, ct);
            return p is null ? Results.NotFound() : Results.Ok(new {
                canManage = user.IsInRole(QmsRoles.SystemAdministrator), editToken = OsanProjectStore.EditToken(p)
            });
        });
        api.MapPut("", async (Guid projectId, UpdateOsanProjectRequest request, OsanProjectStore store,
            DatabaseConnectionStringProvider db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, store, db, user, true, ct);
            if (denied is not null) return denied;
            if (request.Fields is null) return Results.BadRequest();
            var (input, errors) = OsanProjectInputNormalizer.Normalize(request.Fields);
            if (input is null) return Results.ValidationProblem(errors);
            return Result(await store.ManageAsync(projectId, request.ExpectedToken, input, null,
                ProjectEndpointExtensions.GetCurrentUserId(user)!.Value, ct));
        }).RequireAuthorization(QmsPolicies.ProjectUpdate)
          .WithName("UpdateOsanProject");
        api.MapDelete("", async (Guid projectId, [FromBody] DeleteOsanProjectRequest request,
            OsanProjectStore store, DatabaseConnectionStringProvider db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, store, db, user, true, ct);
            if (denied is not null) return denied;
            return Result(await store.ManageAsync(projectId, request.ExpectedToken, null, request.Reason,
                ProjectEndpointExtensions.GetCurrentUserId(user)!.Value, ct));
        }).RequireAuthorization(QmsPolicies.ProjectDelete)
          .WithName("DeleteOsanProject");

        api.MapGet("/progress/photo-edits", async (Guid projectId, OsanProjectStore projects,
            OsanPhotoEditStore store, DatabaseConnectionStringProvider db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, projects, db, user, false, ct);
            return denied ?? Results.Ok(new { canApprove = user.IsInRole(QmsRoles.SystemAdministrator),
                currentUserId = ProjectEndpointExtensions.GetCurrentUserId(user), items = await store.ListAsync(projectId, ct) });
        });
        api.MapPost("/progress/photo-edits", async (Guid projectId, OsanPhotoEditRequest request,
            OsanProjectStore projects, OsanPhotoEditStore store, DatabaseConnectionStringProvider db,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, projects, db, user, false, ct);
            return denied ?? Result(await store.RequestAsync(projectId, request,
                ProjectEndpointExtensions.GetCurrentUserId(user)!.Value, ct));
        }).RequireAuthorization(QmsPolicies.ManufacturingUpdate)
          .WithName("RequestOsanProgressPhotoEdit");
        api.MapPost("/progress/photo-edits/{requestId:guid}/approve", async (Guid projectId, Guid requestId,
            OsanProjectStore projects, OsanPhotoEditStore store, DatabaseConnectionStringProvider db,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, projects, db, user, true, ct);
            return denied ?? Result(await store.ApproveAsync(projectId, requestId,
                ProjectEndpointExtensions.GetCurrentUserId(user)!.Value, ct));
        }).WithName("ApproveOsanProgressPhotoEdit");
        api.MapPost("/progress/photo-edits/{requestId:guid}/save", async (Guid projectId, Guid requestId,
            HttpRequest request, OsanProjectStore projects, OsanPhotoEditStore store,
            DatabaseConnectionStringProvider db, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var denied = await Guard(projectId, projects, db, user, false, ct);
            if (denied is not null) return denied;
            var parsed = await OsanProgressEndpointExtensions.ReadCompletionAsync(request, ct);
            if (parsed.Input is null) return Results.ValidationProblem(parsed.Errors);
            return Result(await store.SaveAsync(projectId, requestId, parsed.Input,
                ProjectEndpointExtensions.GetCurrentUserId(user)!.Value, ct));
        }).RequireAuthorization(QmsPolicies.ManufacturingUpdate)
          .WithMetadata(new SanitizeImageMetadataAfterScanAttribute())
          .WithMetadata(new RequestSizeLimitAttribute(OsanProgressPhotoValidator.MaximumMultipartBytes))
          .WithName("SaveOsanProgressPhotoEdit");
        return app;
    }

    private static Task<IResult?> Guard(Guid id, OsanProjectStore store, DatabaseConnectionStringProvider db,
        ClaimsPrincipal user, bool admin, CancellationToken ct)
    {
        if (ProjectEndpointExtensions.GetCurrentUserId(user) is null) return Task.FromResult<IResult?>(Results.Unauthorized());
        if (admin && !user.IsInRole(QmsRoles.SystemAdministrator)) return Task.FromResult<IResult?>(Results.Forbid());
        return OsanProgressEndpointExtensions.AuthorizeProjectAsync(id, QmsPermissions.ProjectRead, store, db, user, ct);
    }
    private static IResult Result(OsanManagementResult r) => r.Status == 200 ? Results.Ok(r.Value)
        : Results.Json(new OsanProjectErrorResponse("osan_management_rejected", r.Message ?? "요청 대상이나 권한을 확인해 주세요."), statusCode: r.Status);
}
