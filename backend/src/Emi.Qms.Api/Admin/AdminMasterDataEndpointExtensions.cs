using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Admin;

public static class AdminMasterDataEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin");

        admin.MapGet("/dashboard", async (
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.GetDashboardAsync(cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminHistoryRead)
        .WithName("GetAdminDashboard");

        admin.MapGet("/departments", async (
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.ListDepartmentsAsync(cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListAdminDepartments");

        admin.MapPost("/departments", async (
            CreateAdminDepartmentRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.CreateDepartmentAsync(request, currentUserId.Value, cancellationToken);
            return ToCreatedMutationResult(result, value => $"/api/admin/departments/{value.DepartmentId}");
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("CreateAdminDepartment");

        admin.MapPut("/departments/{departmentId:guid}", async (
            Guid departmentId,
            UpdateAdminDepartmentRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.UpdateDepartmentAsync(departmentId, request, currentUserId.Value, cancellationToken);
            return ToMutationResult(result);
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("UpdateAdminDepartment");

        admin.MapPatch("/departments/{departmentId:guid}/deactivate", async (
            Guid departmentId,
            UpdateAdminDepartmentRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.ScheduleDepartmentDeletionAsync(
                departmentId,
                request.Reason,
                currentUserId.Value,
                cancellationToken);
            return ToMutationResult(result);
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("DeactivateAdminDepartment");

        admin.MapPost("/departments/{departmentId:guid}/restore", async (
            Guid departmentId,
            UpdateAdminDepartmentRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.RestoreDepartmentAsync(
                departmentId,
                request.Reason,
                currentUserId.Value,
                cancellationToken);
            return ToMutationResult(result);
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("RestoreAdminDepartment");

        admin.MapDelete("/departments/{departmentId:guid}/purge", async (
            Guid departmentId,
            ClaimsPrincipal user,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await deletionService.PurgeDepartmentNowAsync(departmentId, currentUserId.Value, cancellationToken);
            return result.Status == "Failed"
                ? Results.BadRequest(new { message = result.Message })
                : Results.Ok(ToSingleBulkActionResponse(departmentId, result));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("PurgeAdminDepartment");

        admin.MapPost("/departments/bulk-delete", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await store.BulkDeleteDepartmentsAsync(
                request.Ids,
                currentUserId.Value,
                deletionService,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkDeleteAdminDepartments");

        admin.MapPost("/departments/bulk-restore", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await store.BulkRestoreDepartmentsAsync(
                request.Ids,
                currentUserId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkRestoreAdminDepartments");

        admin.MapPatch("/departments/reorder", async (
            AdminReorderRequest request,
            ClaimsPrincipal user,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await store.ReorderDepartmentsAsync(request, currentUserId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ReorderAdminDepartments");

        admin.MapGet("/permissions/matrix", async (
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.GetPermissionMatrixAsync(cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminHistoryRead)
        .WithName("GetAdminPermissionMatrix");

        admin.MapGet("/master-data/change-logs", async (
            string? entityType,
            Guid? changedByUserId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.ListChangeLogsAsync(entityType, changedByUserId, from, to, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminHistoryRead)
        .WithName("ListAdminMasterChangeLogs");

        admin.MapGet("/work-items/history", async (
            Guid? projectId,
            Guid? userId,
            string? stage,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            AdminMasterDataStore store,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await store.ListWorkItemHistoryAsync(projectId, userId, stage, status, from, to, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminHistoryRead)
        .WithName("ListAdminWorkItemHistory");

        return app;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue(QmsClaimTypes.UserId), out var userId) ? userId : null;
    }

    private static IResult ToMutationResult<T>(AdminMasterMutationResult<T> result)
    {
        return result.Succeeded
            ? Results.Ok(result.Value)
            : Results.BadRequest(new { message = result.ErrorMessage, fieldErrors = result.FieldErrors });
    }

    private static IResult ToCreatedMutationResult<T>(AdminMasterMutationResult<T> result, Func<T, string> location)
    {
        return result.Succeeded && result.Value is not null
            ? Results.Created(location(result.Value), result.Value)
            : Results.BadRequest(new { message = result.ErrorMessage, fieldErrors = result.FieldErrors });
    }

    private static AdminBulkActionResponse ToSingleBulkActionResponse(Guid id, AdminPurgeActionResult result)
    {
        return new AdminBulkActionResponse(
            1,
            result.Status == "Failed" ? 0 : 1,
            result.Status == "Failed" ? 1 : 0,
            0,
            [new AdminBulkActionItemResponse(id, result.Status, result.Message)]);
    }
}
