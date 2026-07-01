using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Workflow;

public static class WorkflowEndpointExtensions
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflow/stages", async (
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            return HasPermission(user, QmsPermissions.ProjectRead)
                ? Results.Ok(await workflowStore.ListStagesAsync(cancellationToken))
                : Results.Forbid();
        })
        .RequireAuthorization()
        .WithName("ListWorkflowStages");

        app.MapGet("/api/projects/{projectId:guid}/workflow", async (
            Guid projectId,
            ProjectStore projectStore,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null)
            {
                return access;
            }

            var workflow = await workflowStore.GetProjectWorkflowAsync(projectId, cancellationToken);
            return workflow is null ? Results.NotFound() : Results.Ok(workflow);
        })
        .RequireAuthorization()
        .WithName("GetProjectWorkflow");

        app.MapGet("/api/my-work/summary", async (
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.GetMyWorkSummaryAsync(userId.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetMyWorkSummary");

        app.MapGet("/api/my-work", async (
            string? status,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.GetMyWorkItemsAsync(userId.Value, status, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListMyWorkItems");

        app.MapGet("/api/my-work/assigned-projects", async (
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.GetMyAssignedProjectsAsync(userId.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListMyAssignedProjects");

        app.MapGet("/api/my-work/{workItemId:guid}", async (
            Guid workItemId,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await workflowStore.GetMyWorkItemAsync(workItemId, userId.Value, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("GetMyWorkItem");

        app.MapPost("/api/my-work/{workItemId:guid}/start", async (
            Guid workItemId,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await workflowStore.StartWorkItemAsync(workItemId, userId.Value, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("StartMyWorkItem");

        app.MapPost("/api/my-work/{workItemId:guid}/complete", async (
            Guid workItemId,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await workflowStore.CompleteWorkItemAsync(workItemId, userId.Value, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("CompleteMyWorkItem");

        app.MapPost("/api/my-work/{workItemId:guid}/cancel", async (
            Guid workItemId,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await workflowStore.CancelWorkItemAsync(workItemId, userId.Value, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("CancelMyWorkItem");

        app.MapGet("/api/notifications/summary", async (
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.GetNotificationSummaryAsync(userId.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetNotificationSummary");

        app.MapGet("/api/notifications", async (
            string? readStatus,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.GetNotificationsAsync(userId.Value, readStatus, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListNotifications");

        app.MapPost("/api/notifications/{notificationId:guid}/read", async (
            Guid notificationId,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await workflowStore.MarkNotificationReadAsync(notificationId, userId.Value, cancellationToken);
            return ToResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("MarkNotificationRead");

        app.MapPost("/api/notifications/read-all", async (
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await workflowStore.MarkAllNotificationsReadAsync(userId.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("MarkAllNotificationsRead");

        return app;
    }

    private static async Task<IResult?> AuthorizeProjectReadAsync(
        ProjectStore projectStore,
        ClaimsPrincipal user,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(user, QmsPermissions.ProjectRead))
        {
            return Results.Forbid();
        }

        var accessRecord = await projectStore.GetProjectAccessRecordAsync(projectId, cancellationToken);
        if (accessRecord is null)
        {
            return Results.NotFound();
        }

        if (!HasPermission(user, QmsPermissions.ProjectReadAll)
            && !user.FindAll(QmsClaimTypes.Project).Any(claim => string.Equals(claim.Value, accessRecord.ProjectKey, StringComparison.Ordinal)))
        {
            return Results.Forbid();
        }

        return null;
    }

    private static IResult ToResult<T>(WorkflowMutationResult<T> result, Func<T, IResult> success)
    {
        return result.Status switch
        {
            WorkflowMutationStatus.Success when result.Value is not null => success(result.Value),
            WorkflowMutationStatus.NotFound => Results.NotFound(),
            WorkflowMutationStatus.Forbidden => Results.Forbid(),
            WorkflowMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        return user.Identity?.IsAuthenticated == true
            && user.HasClaim(QmsClaimTypes.Permission, permissionCode);
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
