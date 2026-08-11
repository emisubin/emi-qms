using System.Security.Claims;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Identity;

public static class IdentityEndpointExtensions
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/me", async (
            ClaimsPrincipal principal,
            IIdentityStore identityStore,
            UserProfilePhotoStore profilePhotoStore,
            IConfiguration configuration,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var effectiveProfile = await GetProfileByClaimAsync(principal, identityStore, QmsClaimTypes.UserId, cancellationToken);
            if (effectiveProfile is null)
            {
                return Results.Unauthorized();
            }

            var actualProfile = await GetProfileByClaimAsync(principal, identityStore, QmsClaimTypes.ActualUserId, cancellationToken)
                ?? effectiveProfile;
            var adminUserSwitchEnabled = DevelopmentFeaturePolicy
                .EvaluateAdminUserSwitch(environment, configuration)
                .IsEnabled;
            var effectivePhotoVersion = await profilePhotoStore.GetVersionAsync(effectiveProfile.User.Id, cancellationToken);
            var actualPhotoVersion = actualProfile.User.Id == effectiveProfile.User.Id
                ? effectivePhotoVersion
                : await profilePhotoStore.GetVersionAsync(actualProfile.User.Id, cancellationToken);

            return Results.Ok(CurrentUserResponse.From(
                effectiveProfile,
                actualProfile,
                principal,
                adminUserSwitchEnabled,
                effectivePhotoVersion,
                actualPhotoVersion));
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("GetCurrentUser");

        api.MapGet("/me/profile-photo", async (
            HttpContext context,
            ClaimsPrincipal principal,
            UserProfilePhotoStore profilePhotoStore,
            CancellationToken cancellationToken) =>
        {
            var userId = GetActualUserId(principal);
            if (userId is null) return Results.Unauthorized();
            var photo = await profilePhotoStore.GetAsync(userId.Value, cancellationToken);
            if (photo is null) return Results.NotFound();
            var etag = $"\"{photo.ContentHash}-{photo.Version}\"";
            if (context.Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "private, max-age=300";
            return Results.File(photo.Content, photo.NormalizedMime);
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("GetOwnProfilePhoto");

        api.MapPut("/me/profile-photo", async (
            [FromForm] IFormFile photo,
            ClaimsPrincipal principal,
            IIdentityStore identityStore,
            UserProfilePhotoStore profilePhotoStore,
            CancellationToken cancellationToken) =>
        {
            var userId = GetActualUserId(principal);
            if (userId is null) return Results.Unauthorized();
            var profile = await identityStore.GetProfileByUserIdAsync(userId.Value, cancellationToken);
            if (profile is null || !profile.User.IsActive) return Results.Unauthorized();
            if (IsApprovalPending(profile))
            {
                return Results.Problem(title: "승인 대기 중에는 프로필 사진을 변경할 수 없습니다.", statusCode: StatusCodes.Status403Forbidden);
            }
            if (photo.Length is < 1 or > ProfileImageValidator.MaxBytes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["photo"] = ["프로필 사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다."]
                });
            }
            await using var stream = photo.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            try
            {
                return Results.Ok(await profilePhotoStore.SaveAsync(userId.Value, memory.ToArray(), cancellationToken));
            }
            catch (ProfileImageValidationException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["photo"] = [exception.Message] });
            }
        })
        .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
        .DisableAntiforgery()
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("SaveOwnProfilePhoto");

        api.MapDelete("/me/profile-photo", async (
            ClaimsPrincipal principal,
            IIdentityStore identityStore,
            UserProfilePhotoStore profilePhotoStore,
            CancellationToken cancellationToken) =>
        {
            var userId = GetActualUserId(principal);
            if (userId is null) return Results.Unauthorized();
            var profile = await identityStore.GetProfileByUserIdAsync(userId.Value, cancellationToken);
            if (profile is null || !profile.User.IsActive) return Results.Unauthorized();
            if (IsApprovalPending(profile))
            {
                return Results.Problem(title: "승인 대기 중에는 프로필 사진을 변경할 수 없습니다.", statusCode: StatusCodes.Status403Forbidden);
            }
            await profilePhotoStore.RemoveAsync(userId.Value, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("RemoveOwnProfilePhoto");

        api.MapGet("/projects/{projectId}/overview", async (
            string projectId,
            IIdentityStore identityStore,
            CancellationToken cancellationToken) =>
        {
            var project = await identityStore.GetProjectByKeyAsync(projectId, cancellationToken);
            return project is null
                ? Results.NotFound()
                : Results.Ok(ProjectOverviewResponse.From(project));
        })
        .RequireAuthorization(QmsPolicies.ProjectRead)
        .WithName("GetProjectOverview");

        api.MapGet("/admin/users", async (
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await userAdministrationStore.GetSnapshotAsync(cancellationToken);
            return Results.Ok(AdminUsersResponse.From(snapshot));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("GetAdminUsers");

        api.MapPatch("/admin/users/{userId:guid}", async (
            Guid userId,
            UpdateUserAdministrationRequest request,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await userAdministrationStore.UpdateEntraUserAsync(
                userId,
                request,
                currentUserId.Value,
                cancellationToken);

            return result.Succeeded && result.Snapshot is not null
                ? Results.Ok(AdminUsersResponse.From(result.Snapshot))
                : Results.BadRequest(new { message = result.ErrorMessage ?? "사용자 정보를 저장할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("UpdateAdminUser");

        api.MapPatch("/admin/users/{userId:guid}/schedule-deletion", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await userAdministrationStore.ScheduleEntraUserDeletionAsync(
                userId,
                currentUserId.Value,
                cancellationToken);

            return result.Succeeded && result.Snapshot is not null
                ? Results.Ok(AdminUsersResponse.From(result.Snapshot))
                : Results.BadRequest(new { message = result.ErrorMessage ?? "사용자를 삭제 예약할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ScheduleAdminUserDeletion");

        api.MapPost("/admin/users/{userId:guid}/restore", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await userAdministrationStore.RestoreEntraUserAsync(
                userId,
                currentUserId.Value,
                cancellationToken);

            return result.Succeeded && result.Snapshot is not null
                ? Results.Ok(AdminUsersResponse.From(result.Snapshot))
                : Results.BadRequest(new { message = result.ErrorMessage ?? "사용자를 복구할 수 없습니다." });
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("RestoreAdminUser");

        api.MapDelete("/admin/users/{userId:guid}/purge", async (
            Guid userId,
            ClaimsPrincipal principal,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            var result = await deletionService.PurgeUserNowAsync(userId, currentUserId.Value, cancellationToken);
            return result.Status == "Failed"
                ? Results.BadRequest(new { message = result.Message })
                : Results.Ok(ToSingleBulkActionResponse(userId, result));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("PurgeAdminUser");

        api.MapPost("/admin/users/bulk-delete", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            AdminScheduledDeletionService deletionService,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await userAdministrationStore.BulkDeleteUsersAsync(
                request.Ids,
                currentUserId.Value,
                deletionService,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkDeleteAdminUsers");

        api.MapPost("/admin/users/bulk-restore", async (
            AdminBulkActionRequest request,
            ClaimsPrincipal principal,
            IUserAdministrationStore userAdministrationStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await userAdministrationStore.BulkRestoreUsersAsync(
                request.Ids,
                currentUserId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("BulkRestoreAdminUsers");

        return app;
    }

    private static async Task<UserAuthorizationProfile?> GetProfileByClaimAsync(
        ClaimsPrincipal principal,
        IIdentityStore identityStore,
        string claimType,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirst(claimType)?.Value;

        return Guid.TryParse(userIdValue, out var userId)
            ? await identityStore.GetProfileByUserIdAsync(userId, cancellationToken)
            : null;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static Guid? GetActualUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(QmsClaimTypes.ActualUserId)?.Value
            ?? principal.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static bool IsApprovalPending(UserAuthorizationProfile profile)
        => profile.User.AuthProvider == QmsAuthProviders.EntraId
            && profile.User.IsActive
            && profile.Roles.Count == 0;

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

public sealed record CurrentUserResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    string? Department,
    string? DepartmentName,
    string? ProfilePhotoVersion,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<ProjectAccessResponse> ProjectAccess,
    bool IsTestUserSwitch,
    string? TestUserKey,
    bool CanUseAdminTestUserSwitch,
    CurrentUserPrincipalResponse ActualUser,
    CurrentUserPrincipalResponse EffectiveUser)
{
    public static CurrentUserResponse From(
        UserAuthorizationProfile effectiveProfile,
        UserAuthorizationProfile actualProfile,
        ClaimsPrincipal principal,
        bool adminUserSwitchEnabled,
        string? effectivePhotoVersion,
        string? actualPhotoVersion)
    {
        var effectiveApprovalPending = IsApprovalPending(effectiveProfile);
        var actualApprovalPending = IsApprovalPending(actualProfile);
        var canUseAdminTestUserSwitch = adminUserSwitchEnabled
            && actualProfile.User.IsActive
            && !actualApprovalPending
            && actualProfile.Roles.Any(role => string.Equals(role.Code, QmsRoles.SystemAdministrator, StringComparison.Ordinal));

        return new CurrentUserResponse(
            effectiveProfile.User.Id,
            effectiveProfile.User.DevelopmentUserKey,
            effectiveProfile.User.DisplayName,
            effectiveProfile.User.Email,
            effectiveProfile.User.AuthProvider,
            effectiveProfile.User.IsActive,
            effectiveApprovalPending,
            effectiveProfile.Department?.Code,
            effectiveProfile.Department?.Name,
            effectivePhotoVersion,
            effectiveProfile.Roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal).ToList(),
            effectiveProfile.User.IsActive && !effectiveApprovalPending
                ? effectiveProfile.Permissions.Select(permission => permission.Code).OrderBy(code => code, StringComparer.Ordinal).ToList()
                : [],
            effectiveProfile.ProjectAccess.Select(ProjectAccessResponse.From).ToList(),
            principal.HasClaim(QmsClaimTypes.IsTestUserSwitch, bool.TrueString),
            principal.FindFirst(QmsClaimTypes.TestUserKey)?.Value,
            canUseAdminTestUserSwitch,
            CurrentUserPrincipalResponse.From(actualProfile, actualApprovalPending, actualPhotoVersion),
            CurrentUserPrincipalResponse.From(effectiveProfile, effectiveApprovalPending, effectivePhotoVersion));
    }

    private static bool IsApprovalPending(UserAuthorizationProfile profile)
    {
        return profile.User.AuthProvider == QmsAuthProviders.EntraId
            && profile.User.IsActive
            && profile.Roles.Count == 0;
    }
}

public sealed record CurrentUserPrincipalResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    string? Department,
    string? DepartmentName,
    string? ProfilePhotoVersion,
    IReadOnlyList<string> Roles)
{
    public static CurrentUserPrincipalResponse From(
        UserAuthorizationProfile profile,
        bool approvalPending,
        string? profilePhotoVersion)
    {
        return new CurrentUserPrincipalResponse(
            profile.User.Id,
            profile.User.DevelopmentUserKey,
            profile.User.DisplayName,
            profile.User.Email,
            profile.User.AuthProvider,
            profile.User.IsActive,
            approvalPending,
            profile.Department?.Code,
            profile.Department?.Name,
            profilePhotoVersion,
            profile.Roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal).ToList());
    }
}

public sealed record ProjectAccessResponse(string ProjectKey, string ProjectNumber, string Name)
{
    public static ProjectAccessResponse From(QmsProject project)
    {
        return new ProjectAccessResponse(project.ProjectKey, project.ProjectNumber, project.Name);
    }
}

public sealed record ProjectOverviewResponse(string ProjectKey, string ProjectNumber, string Name, string Status)
{
    public static ProjectOverviewResponse From(QmsProject project)
    {
        return new ProjectOverviewResponse(project.ProjectKey, project.ProjectNumber, project.Name, "authorization-foundation");
    }
}

public sealed record AdminUsersResponse(
    IReadOnlyList<AdminUserResponse> Users,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminRoleResponse> Roles)
{
    public static AdminUsersResponse From(UserAdministrationSnapshot snapshot)
    {
        return new AdminUsersResponse(
            snapshot.Users.Select(AdminUserResponse.From).ToList(),
            snapshot.Departments.Select(AdminDepartmentResponse.From).ToList(),
            snapshot.Roles.Select(AdminRoleResponse.From).ToList());
    }
}

public sealed record AdminUserResponse(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<string> Roles,
    bool IsReadOnly,
    bool IsDepartmentHead,
    DateTimeOffset? DeletionRequestedAtUtc,
    DateTimeOffset? ScheduledHardDeleteAtUtc,
    DateTimeOffset? PurgeBlockedAtUtc,
    string? PurgeBlockedReason,
    bool? PreDeleteIsActive)
{
    public string LifecycleStatus => AdminDeletionLifecycle.Calculate(
        IsActive,
        DeletionRequestedAtUtc,
        ScheduledHardDeleteAtUtc,
        PurgeBlockedAtUtc);

    public string LifecycleStatusLabel => AdminDeletionLifecycle.Label(LifecycleStatus);

    public string? ScheduledHardDeleteLabel => AdminDeletionLifecycle.FormatScheduledHardDeleteLabel(ScheduledHardDeleteAtUtc);

    public static AdminUserResponse From(UserAdministrationUser user)
    {
        return new AdminUserResponse(
            user.UserId,
            user.DevelopmentUserKey,
            user.DisplayName,
            user.Email,
            user.AuthProvider,
            user.IsActive,
            user.ApprovalPending,
            user.DepartmentId,
            user.DepartmentCode,
            user.DepartmentName,
            user.Roles,
            user.IsReadOnly,
            user.IsDepartmentHead,
            user.DeletionRequestedAtUtc,
            user.ScheduledHardDeleteAtUtc,
            user.PurgeBlockedAtUtc,
            user.PurgeBlockedReason,
            user.PreDeleteIsActive);
    }
}

public sealed record AdminDepartmentResponse(Guid DepartmentId, string Code, string Name, string? DefaultRoleCode)
{
    public static AdminDepartmentResponse From(Department department)
    {
        return new AdminDepartmentResponse(
            department.Id,
            department.Code,
            department.Name,
            DepartmentIdentityPolicy.GetDefaultRoleCode(department.Code));
    }
}

public sealed record AdminRoleResponse(Guid RoleId, string Code, string Name)
{
    public static AdminRoleResponse From(Role role)
    {
        return new AdminRoleResponse(role.Id, role.Code, role.Name);
    }
}
