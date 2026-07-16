using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Pending;

public static class PendingEndpointExtensions
{
    public static IEndpointRouteBuilder MapPendingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/pending").RequireAuthorization();

        api.MapGet("", async (
            string? status,
            string? issueType,
            string? priority,
            Guid? assigneeUserId,
            PendingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.PendingRead)
                ? Results.Ok(await store.ListAsync(status, issueType, priority, assigneeUserId, cancellationToken))
                : Results.Forbid();
        })
        .WithName("ListPendingIssues");

        api.MapGet("/assignees", async (
            PendingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.PendingManage)
                ? Results.Ok(await store.ListAssigneesAsync(cancellationToken))
                : Results.Forbid();
        })
        .WithName("ListPendingAssignees");

        api.MapGet("/{pendingId:guid}", async (
            Guid pendingId,
            PendingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.PendingRead))
            {
                return Results.Forbid();
            }
            var actor = GetActor(user);
            if (actor is null)
            {
                return Results.Unauthorized();
            }
            var detail = await store.GetDetailAsync(pendingId, actor, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
        .WithName("GetPendingIssue");

        api.MapPost("", async (
            CreatePendingRequest request,
            PendingStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var denied = AuthorizeMutation(user);
            if (denied is not null)
            {
                return denied;
            }
            var result = await store.CreateAsync(request, GetActor(user)!, context.TraceIdentifier, cancellationToken);
            return ToResult(result, detail => Results.Created($"/api/pending/{detail.Issue.PendingId}", detail));
        })
        .WithName("CreatePendingIssue");

        api.MapPost("/{pendingId:guid}/transition", async (
            Guid pendingId,
            TransitionPendingRequest request,
            PendingStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var denied = AuthorizeMutation(user);
            if (denied is not null)
            {
                return denied;
            }
            var result = await store.TransitionAsync(pendingId, request, GetActor(user)!, context.TraceIdentifier, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .WithName("TransitionPendingIssue");

        api.MapPost("/{pendingId:guid}/assign", async (
            Guid pendingId,
            AssignPendingRequest request,
            PendingStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var denied = AuthorizeMutation(user);
            if (denied is not null)
            {
                return denied;
            }
            var result = await store.AssignAsync(pendingId, request, GetActor(user)!, context.TraceIdentifier, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .WithName("AssignPendingIssue");

        api.MapPost("/{pendingId:guid}/comments", async (
            Guid pendingId,
            AddPendingCommentRequest request,
            PendingStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = AuthorizeMutation(user);
            if (denied is not null)
            {
                return denied;
            }
            var result = await store.AddCommentAsync(pendingId, request, GetActor(user)!, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .WithName("AddPendingComment");

        return app;
    }

    private static IResult? AuthorizeMutation(ClaimsPrincipal user)
    {
        if (!HasPermission(user, QmsPermissions.PendingManage))
        {
            return Results.Forbid();
        }
        return GetActor(user) is null ? Results.Unauthorized() : null;
    }

    private static PendingActor? GetActor(ClaimsPrincipal user)
    {
        var rawUserId = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return null;
        }
        var isCoordinator = user.FindAll(ClaimTypes.Role)
            .Any(claim => string.Equals(claim.Value, QmsRoles.ProductionPlanning, StringComparison.Ordinal));
        return new PendingActor(userId, isCoordinator);
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        return user.Identity?.IsAuthenticated == true
            && user.HasClaim(QmsClaimTypes.Permission, permissionCode);
    }

    private static IResult ToResult<T>(PendingMutationResult<T> result, Func<T, IResult> success)
    {
        return result.Status switch
        {
            PendingMutationStatus.Success when result.Value is not null => success(result.Value),
            PendingMutationStatus.NotFound => Results.NotFound(),
            PendingMutationStatus.Forbidden => Results.Forbid(),
            PendingMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "현재 상태에서 요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            PendingMutationStatus.Validation when result.Errors is not null => Results.ValidationProblem(result.Errors),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
