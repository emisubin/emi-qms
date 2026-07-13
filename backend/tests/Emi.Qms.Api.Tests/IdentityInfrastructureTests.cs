using System.Security.Claims;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class IdentityInfrastructureTests
{
    [Theory]
    [InlineData(AdminDecreaseOperation.Deactivate, AdminDecreaseOperation.Deactivate)]
    [InlineData(AdminDecreaseOperation.RemoveRole, AdminDecreaseOperation.RemoveRole)]
    [InlineData(AdminDecreaseOperation.Deactivate, AdminDecreaseOperation.RemoveRole)]
    [InlineData(AdminDecreaseOperation.ScheduleDeletion, AdminDecreaseOperation.RemoveRole)]
    public async Task ConcurrentDifferentAdministratorDecreasesPreserveTheCanonicalInvariant(
        AdminDecreaseOperation firstOperation,
        AdminDecreaseOperation secondOperation)
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "first.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var first = await identityStore.GetOrCreateEntraProfileAsync(
            "concurrency-admin-first",
            "Concurrency Admin First",
            "first.admin@example.com",
            TestContext.Current.CancellationToken);
        var second = await identityStore.GetOrCreateEntraProfileAsync(
            "concurrency-admin-second",
            "Concurrency Admin Second",
            "second.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);

        var promotion = await administration.UpdateEntraUserAsync(
            second.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
            first.User.Id,
            TestContext.Current.CancellationToken);
        Assert.True(promotion.Succeeded, promotion.ErrorMessage);

        await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
        var firstTask = ExecuteDecreaseAsync(administration, first.User.Id, first.User.Id, firstOperation);
        var secondTask = ExecuteDecreaseAsync(administration, second.User.Id, first.User.Id, secondOperation);

        await context.WaitForLockWaitersAsync(2);
        Assert.False(firstTask.IsCompleted);
        Assert.False(secondTask.IsCompleted);
        await roleLock.ReleaseAsync();

        var results = await Task.WhenAll(firstTask, secondTask);
        Assert.Single(results, result => result.Succeeded);
        var rejection = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal(ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage, rejection.ErrorMessage);
        Assert.Equal(1L, await context.CountCanonicalActiveAdministratorsAsync());
    }

    [Fact]
    public async Task DuePurgeAndRoleRemovalUseTheSameCanonicalRoleBoundary()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "remaining.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var deletionService = context.Services.GetRequiredService<AdminScheduledDeletionService>();
        var remaining = await identityStore.GetOrCreateEntraProfileAsync(
            "purge-race-remaining",
            "Purge Race Remaining",
            "remaining.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(remaining);
        var malformedTargetId = Guid.NewGuid();

        await context.ExecuteSqlAsync($"""
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active, auth_provider,
                entra_object_id, email, deletion_requested_at_utc, scheduled_hard_delete_at_utc
            )
            values (
                '{malformedTargetId}', 'purge-race-target', 'Synthetic Purge Race Target', null,
                true, 'EntraId', 'purge-race-target', 'purge-race-target@example.invalid',
                now() - interval '8 days', now() - interval '1 day'
            );
            insert into user_roles (user_id, role_id)
            select '{malformedTargetId}', id
            from roles
            where code = 'system-administrator';
            """);

        await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
        var purgeTask = deletionService.PurgeDueAsync(TestContext.Current.CancellationToken);
        var roleRemovalTask = administration.UpdateEntraUserAsync(
            remaining.User.Id,
            new UpdateUserAdministrationRequest(null, [], true),
            remaining.User.Id,
            TestContext.Current.CancellationToken);

        await context.WaitForLockWaitersAsync(2);
        Assert.False(purgeTask.IsCompleted);
        Assert.False(roleRemovalTask.IsCompleted);
        await roleLock.ReleaseAsync();

        var purge = await purgeTask;
        var roleRemoval = await roleRemovalTask;
        Assert.Equal(0, purge.PurgedUserCount);
        Assert.Equal(1, purge.BlockedCount);
        Assert.False(roleRemoval.Succeeded);
        Assert.Equal(ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage, roleRemoval.ErrorMessage);
        Assert.Equal(1L, await context.CountCanonicalActiveAdministratorsAsync());
    }

    [Fact]
    public async Task CanonicalRoleLockCancellationRollsBackWithoutDomainErrorNormalization()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var admin = await identityStore.GetOrCreateEntraProfileAsync(
            "lock-cancellation-admin",
            "Lock Cancellation Admin",
            "admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(admin);

        await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
        using var cancellation = new CancellationTokenSource();
        var mutation = administration.UpdateEntraUserAsync(
            admin.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], false),
            admin.User.Id,
            cancellation.Token);
        await context.WaitForLockWaitersAsync(1);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await mutation);
        await roleLock.ReleaseAsync();
        Assert.Equal(1L, await context.CountCanonicalActiveAdministratorsAsync());
    }

    [Fact]
    public async Task PurgeRoleLockCancellationPreservesTheMalformedTarget()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var deletionService = context.Services.GetRequiredService<AdminScheduledDeletionService>();
        var targetUserId = Guid.NewGuid();
        await context.ExecuteSqlAsync($"""
            insert into qms_users (
                id, development_user_key, display_name, department_id, is_active, auth_provider,
                entra_object_id, email, deletion_requested_at_utc, scheduled_hard_delete_at_utc
            )
            values (
                '{targetUserId}', 'purge-cancellation-target', 'Synthetic Purge Cancellation Target', null,
                true, 'EntraId', 'purge-cancellation-target', 'purge-cancellation-target@example.invalid',
                now() - interval '8 days', now() - interval '1 day'
            );
            insert into user_roles (user_id, role_id)
            select '{targetUserId}', id
            from roles
            where code = 'system-administrator';
            """);

        await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
        using var cancellation = new CancellationTokenSource();
        var purge = deletionService.PurgeUserNowAsync(
            targetUserId,
            Guid.NewGuid(),
            cancellation.Token);
        await context.WaitForLockWaitersAsync(1);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await purge);
        await roleLock.ReleaseAsync();
        Assert.Equal(1L, await context.ReadScalarAsync<long>($"select count(*) from qms_users where id = '{targetUserId}';"));
        Assert.Equal(1L, await context.ReadScalarAsync<long>($"select count(*) from user_roles where user_id = '{targetUserId}';"));
    }

    [Fact]
    public async Task FailureAfterInvariantGuardRollsBackTheEntireAdministratorMutation()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "first.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var first = await identityStore.GetOrCreateEntraProfileAsync(
            "rollback-admin-first",
            "Rollback Admin First",
            "first.admin@example.com",
            TestContext.Current.CancellationToken);
        var second = await identityStore.GetOrCreateEntraProfileAsync(
            "rollback-admin-second",
            "Rollback Admin Second",
            "second.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);
        var promotion = await administration.UpdateEntraUserAsync(
            second.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
            first.User.Id,
            TestContext.Current.CancellationToken);
        Assert.True(promotion.Succeeded, promotion.ErrorMessage);

        await context.ExecuteSqlAsync("""
            create function reject_auth_harden_update() returns trigger language plpgsql as $$
            begin
                raise exception 'synthetic transaction failure';
            end;
            $$;
            create trigger reject_auth_harden_update
            before update on qms_users
            for each row execute function reject_auth_harden_update();
            """);

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await administration.UpdateEntraUserAsync(
                first.User.Id,
                new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], false),
                second.User.Id,
                TestContext.Current.CancellationToken));
        Assert.Equal(2L, await context.CountCanonicalActiveAdministratorsAsync());
    }

    [Fact]
    public async Task ConcurrentDuplicateTargetMutationKeepsTheExistingIdempotentOutcome()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "first.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var first = await identityStore.GetOrCreateEntraProfileAsync(
            "duplicate-target-first",
            "Duplicate Target First",
            "first.admin@example.com",
            TestContext.Current.CancellationToken);
        var second = await identityStore.GetOrCreateEntraProfileAsync(
            "duplicate-target-second",
            "Duplicate Target Second",
            "second.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True((await administration.UpdateEntraUserAsync(
            second.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
            first.User.Id,
            TestContext.Current.CancellationToken)).Succeeded);

        var firstMutation = ExecuteDecreaseAsync(
            administration,
            first.User.Id,
            second.User.Id,
            AdminDecreaseOperation.Deactivate);
        var duplicateMutation = ExecuteDecreaseAsync(
            administration,
            first.User.Id,
            second.User.Id,
            AdminDecreaseOperation.Deactivate);
        var results = await Task.WhenAll(firstMutation, duplicateMutation);

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorMessage));
        Assert.Equal(1L, await context.CountCanonicalActiveAdministratorsAsync());
    }

    [Fact]
    public async Task ConcurrentAdministratorPromotionAndLastAdministratorRemovalAlwaysCommitAValidState()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "existing.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var existing = await identityStore.GetOrCreateEntraProfileAsync(
            "promotion-race-existing",
            "Promotion Race Existing",
            "existing.admin@example.com",
            TestContext.Current.CancellationToken);
        var pending = await identityStore.GetOrCreateEntraProfileAsync(
            "promotion-race-pending",
            "Promotion Race Pending",
            "pending.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.NotNull(pending);

        await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
        var removal = ExecuteDecreaseAsync(
            administration,
            existing.User.Id,
            existing.User.Id,
            AdminDecreaseOperation.RemoveRole);
        var promotion = administration.UpdateEntraUserAsync(
            pending.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
            existing.User.Id,
            TestContext.Current.CancellationToken);
        await context.WaitForLockWaitersAsync(2);
        await roleLock.ReleaseAsync();

        var results = await Task.WhenAll(removal, promotion);
        Assert.True(results[1].Succeeded, results[1].ErrorMessage);
        Assert.True(results[0].Succeeded
                    || results[0].ErrorMessage == ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage);
        Assert.True(await context.CountCanonicalActiveAdministratorsAsync() >= 1L);
    }

    [Fact]
    public async Task RepeatedConcurrentAdministratorDecreasesNeverCommitZeroActiveAdministrators()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = "first.admin@example.com"
        });
        var identityStore = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var first = await identityStore.GetOrCreateEntraProfileAsync(
            "stress-admin-first",
            "Stress Admin First",
            "first.admin@example.com",
            TestContext.Current.CancellationToken);
        var second = await identityStore.GetOrCreateEntraProfileAsync(
            "stress-admin-second",
            "Stress Admin Second",
            "second.admin@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);

        for (var iteration = 0; iteration < 20; iteration += 1)
        {
            Assert.True((await administration.UpdateEntraUserAsync(
                first.User.Id,
                new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
                first.User.Id,
                TestContext.Current.CancellationToken)).Succeeded);
            Assert.True((await administration.UpdateEntraUserAsync(
                second.User.Id,
                new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], true),
                first.User.Id,
                TestContext.Current.CancellationToken)).Succeeded);

            await using var roleLock = await context.LockCanonicalAdministratorRoleAsync();
            var firstMutation = ExecuteDecreaseAsync(
                administration,
                iteration % 2 == 0 ? first.User.Id : second.User.Id,
                first.User.Id,
                AdminDecreaseOperation.Deactivate);
            var secondMutation = ExecuteDecreaseAsync(
                administration,
                iteration % 2 == 0 ? second.User.Id : first.User.Id,
                first.User.Id,
                AdminDecreaseOperation.RemoveRole);
            await context.WaitForLockWaitersAsync(2);
            await roleLock.ReleaseAsync();

            var results = await Task.WhenAll(firstMutation, secondMutation);
            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => !result.Succeeded);
            Assert.Equal(1L, await context.CountCanonicalActiveAdministratorsAsync());
        }
    }

    [Fact]
    public async Task EntraJitCreatesPendingUserAndDoesNotMergeByEmail()
    {
        await using var context = await IdentityTestContext.CreateAsync();
        var store = context.Services.GetRequiredService<DbIdentityStore>();

        await context.ExecuteSqlAsync("""
            update qms_users
            set email = 'same.user@example.com'
            where development_user_key = 'dev-admin';
            """);

        var created = await store.GetOrCreateEntraProfileAsync(
            "entra-object-1",
            " First Entra User ",
            "Same.User@Example.com",
            TestContext.Current.CancellationToken);
        var updated = await store.GetOrCreateEntraProfileAsync(
            "entra-object-1",
            "Updated Entra User",
            "updated.user@example.com",
            TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.NotNull(updated);
        Assert.Equal(created.User.Id, updated.User.Id);
        Assert.Equal(QmsAuthProviders.EntraId, updated.User.AuthProvider);
        Assert.Equal("Updated Entra User", updated.User.DisplayName);
        Assert.Equal("updated.user@example.com", updated.User.Email);
        Assert.Null(updated.Department);
        Assert.Empty(updated.Roles);
        Assert.Empty(updated.Permissions);

        Assert.Equal(2L, await context.ReadScalarAsync<long>("""
            select count(*)
            from qms_users
            where email in ('same.user@example.com', 'updated.user@example.com');
            """));
    }

    [Fact]
    public async Task BootstrapAdminAndUserAdministrationRespectPendingAndLastAdminProtection()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = " admin@example.com "
        });
        var store = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();

        var admin = await store.GetOrCreateEntraProfileAsync(
            "bootstrap-admin-oid",
            "Bootstrap Admin",
            "Admin@Example.com",
            TestContext.Current.CancellationToken);
        var pending = await store.GetOrCreateEntraProfileAsync(
            "pending-user-oid",
            "Pending User",
            "pending@example.com",
            TestContext.Current.CancellationToken);
        var existingPendingBootstrap = await store.GetOrCreateEntraProfileAsync(
            "existing-bootstrap-oid",
            "Existing Pending",
            "not-admin@example.com",
            TestContext.Current.CancellationToken);

        Assert.NotNull(admin);
        Assert.NotNull(pending);
        Assert.NotNull(existingPendingBootstrap);
        Assert.Contains(admin.Roles, role => role.Code == QmsRoles.SystemAdministrator);
        Assert.Contains(admin.Permissions, permission => permission.Code == QmsPermissions.UsersManage);
        Assert.Empty(pending.Roles);
        Assert.Empty(pending.Permissions);
        Assert.Empty(existingPendingBootstrap.Roles);

        var snapshot = await administration.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var salesDepartment = Assert.Single(snapshot.Departments, department => department.Code == "sales");
        var entraPendingUser = Assert.Single(snapshot.Users, user => user.UserId == pending.User.Id);
        Assert.True(entraPendingUser.ApprovalPending);
        Assert.False(entraPendingUser.IsReadOnly);

        var updated = await administration.UpdateEntraUserAsync(
            pending.User.Id,
            new UpdateUserAdministrationRequest(salesDepartment.Id, [QmsRoles.Sales], true),
            admin.User.Id,
            TestContext.Current.CancellationToken);

        Assert.True(updated.Succeeded, updated.ErrorMessage);
        var activated = Assert.Single(updated.Snapshot!.Users, user => user.UserId == pending.User.Id);
        Assert.False(activated.ApprovalPending);
        Assert.Equal(salesDepartment.Id, activated.DepartmentId);
        Assert.Contains(QmsRoles.Sales, activated.Roles);

        var lastAdminRoleRemoval = await administration.UpdateEntraUserAsync(
            admin.User.Id,
            new UpdateUserAdministrationRequest(null, [], true),
            admin.User.Id,
            TestContext.Current.CancellationToken);
        var lastAdminDeactivation = await administration.UpdateEntraUserAsync(
            admin.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], false),
            admin.User.Id,
            TestContext.Current.CancellationToken);

        Assert.False(lastAdminRoleRemoval.Succeeded);
        Assert.Equal(ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage, lastAdminRoleRemoval.ErrorMessage);
        Assert.False(lastAdminDeactivation.Succeeded);
        Assert.Equal(ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage, lastAdminDeactivation.ErrorMessage);
        var lastAdminDeletion = await administration.ScheduleEntraUserDeletionAsync(
            admin.User.Id,
            admin.User.Id,
            TestContext.Current.CancellationToken);
        Assert.False(lastAdminDeletion.Succeeded);
        Assert.Equal(ActiveSystemAdministratorInvariantGuard.LastAdministratorErrorMessage, lastAdminDeletion.ErrorMessage);

        var promotedExistingBootstrap = await store.GetOrCreateEntraProfileAsync(
            "existing-bootstrap-oid",
            "Existing Bootstrap Admin",
            "ADMIN@Example.com",
            TestContext.Current.CancellationToken);

        Assert.NotNull(promotedExistingBootstrap);
        Assert.Equal(existingPendingBootstrap.User.Id, promotedExistingBootstrap.User.Id);
        Assert.Contains(promotedExistingBootstrap.Roles, role => role.Code == QmsRoles.SystemAdministrator);
        Assert.Contains(promotedExistingBootstrap.Permissions, permission => permission.Code == QmsPermissions.UsersManage);

        var scheduledDeletion = await administration.ScheduleEntraUserDeletionAsync(
            admin.User.Id,
            promotedExistingBootstrap.User.Id,
            TestContext.Current.CancellationToken);
        Assert.True(scheduledDeletion.Succeeded, scheduledDeletion.ErrorMessage);
        var scheduledUser = Assert.Single(scheduledDeletion.Snapshot!.Users, user => user.UserId == admin.User.Id);
        Assert.False(scheduledUser.IsActive);
        Assert.NotNull(scheduledUser.DeletionRequestedAtUtc);
        Assert.NotNull(scheduledUser.ScheduledHardDeleteAtUtc);
    }

    [Fact]
    public async Task EntraClaimsTransformationUsesMicrosoftObjectIdClaimAndAddsQmsClaims()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = " admin@example.com "
        });
        var transformation = context.Services.GetRequiredService<IClaimsTransformation>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "mapped-object-id"),
            new Claim(ClaimTypes.Name, "Mapped Bootstrap Admin"),
            new Claim("preferred_username", "Admin@Example.com")
        ], QmsAuthenticationSchemes.EntraBearer));

        var transformed = await transformation.TransformAsync(principal);

        Assert.NotNull(transformed.FindFirst(QmsClaimTypes.UserId));
        Assert.Contains(transformed.Claims, claim =>
            claim.Type == QmsClaimTypes.Permission
            && claim.Value == QmsPermissions.UsersManage);
        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ClaimTypes.Role
            && claim.Value == QmsRoles.SystemAdministrator);
    }

    [Fact]
    public async Task EntraSystemAdministratorCanSwitchToAllowedDevelopmentPersona()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = " admin@example.com ",
            ["AdminUserSwitch:Enabled"] = "true"
        });
        var accessor = context.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Headers[AdminUserSwitchDefaults.HeaderName] = "dev-production";
        var transformation = context.Services.GetRequiredService<IClaimsTransformation>();
        var developmentStore = context.Services.GetRequiredService<InMemoryIdentityStore>();
        var productionProfile = await developmentStore.GetProfileByDevelopmentUserKeyAsync(
            "dev-production",
            TestContext.Current.CancellationToken);
        Assert.NotNull(productionProfile);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", "switch-admin-oid"),
            new Claim("name", "Switch Admin"),
            new Claim("preferred_username", "admin@example.com")
        ], QmsAuthenticationSchemes.EntraBearer));

        var transformed = await transformation.TransformAsync(principal);

        Assert.Equal(productionProfile.User.Id.ToString("D"), transformed.FindFirst(QmsClaimTypes.UserId)?.Value);
        Assert.NotNull(transformed.FindFirst(QmsClaimTypes.ActualUserId));
        Assert.Equal(QmsAuthProviders.EntraId, transformed.FindFirst(QmsClaimTypes.ActualAuthProvider)?.Value);
        Assert.Equal("dev-production", transformed.FindFirst(QmsClaimTypes.TestUserKey)?.Value);
        Assert.Equal(bool.TrueString, transformed.FindFirst(QmsClaimTypes.IsTestUserSwitch)?.Value);
        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ClaimTypes.Role
            && claim.Value == QmsRoles.ProductionPlanning);
        Assert.Contains(transformed.Claims, claim =>
            claim.Type == QmsClaimTypes.Permission
            && claim.Value == QmsPermissions.ProductionPlanUpdate);
        Assert.DoesNotContain(transformed.Claims, claim =>
            claim.Type == QmsClaimTypes.Permission
            && claim.Value == QmsPermissions.UsersManage);
    }

    [Fact]
    public async Task EntraTestUserSwitchRequiresActualSystemAdministratorAndAllowedPersona()
    {
        await using var context = await IdentityTestContext.CreateAsync(new Dictionary<string, string?>
        {
            ["Authentication:BootstrapAdminEmails"] = " admin@example.com ",
            ["AdminUserSwitch:Enabled"] = "true"
        });
        var store = context.Services.GetRequiredService<DbIdentityStore>();
        var administration = context.Services.GetRequiredService<IUserAdministrationStore>();
        var admin = await store.GetOrCreateEntraProfileAsync(
            "switch-policy-admin-oid",
            "Switch Policy Admin",
            "admin@example.com",
            TestContext.Current.CancellationToken);
        var nonAdmin = await store.GetOrCreateEntraProfileAsync(
            "switch-policy-sales-oid",
            "Switch Policy Sales",
            "sales@example.com",
            TestContext.Current.CancellationToken);
        Assert.NotNull(admin);
        Assert.NotNull(nonAdmin);

        var update = await administration.UpdateEntraUserAsync(
            nonAdmin.User.Id,
            new UpdateUserAdministrationRequest(null, [QmsRoles.Sales], true),
            admin.User.Id,
            TestContext.Current.CancellationToken);
        Assert.True(update.Succeeded, update.ErrorMessage);

        var nonAdminResult = await TransformWithTestUserHeaderAsync(
            context,
            "switch-policy-sales-oid",
            "sales@example.com",
            "dev-production");
        Assert.Equal(AdminUserSwitchDefaults.ReasonNotAllowed, nonAdminResult.FindFirst(QmsClaimTypes.TestUserSwitchDeniedReason)?.Value);
        Assert.Equal(bool.FalseString, nonAdminResult.FindFirst(QmsClaimTypes.IsTestUserSwitch)?.Value);

        var invalidUserResult = await TransformWithTestUserHeaderAsync(
            context,
            "switch-policy-admin-oid",
            "admin@example.com",
            "dev-design");
        Assert.Equal(AdminUserSwitchDefaults.ReasonInvalidTestUser, invalidUserResult.FindFirst(QmsClaimTypes.TestUserSwitchDeniedReason)?.Value);
        Assert.Equal(bool.FalseString, invalidUserResult.FindFirst(QmsClaimTypes.IsTestUserSwitch)?.Value);
    }

    private static async Task<ClaimsPrincipal> TransformWithTestUserHeaderAsync(
        IdentityTestContext context,
        string objectId,
        string email,
        string testUserKey)
    {
        var accessor = context.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Headers[AdminUserSwitchDefaults.HeaderName] = testUserKey;
        var transformation = context.Services.GetRequiredService<IClaimsTransformation>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", objectId),
            new Claim("name", email),
            new Claim("preferred_username", email)
        ], QmsAuthenticationSchemes.EntraBearer));

        return await transformation.TransformAsync(principal);
    }

    private static Task<UserAdministrationMutationResult> ExecuteDecreaseAsync(
        IUserAdministrationStore administration,
        Guid targetUserId,
        Guid currentUserId,
        AdminDecreaseOperation operation)
    {
        return operation switch
        {
            AdminDecreaseOperation.Deactivate => administration.UpdateEntraUserAsync(
                targetUserId,
                new UpdateUserAdministrationRequest(null, [QmsRoles.SystemAdministrator], false),
                currentUserId,
                TestContext.Current.CancellationToken),
            AdminDecreaseOperation.RemoveRole => administration.UpdateEntraUserAsync(
                targetUserId,
                new UpdateUserAdministrationRequest(null, [], true),
                currentUserId,
                TestContext.Current.CancellationToken),
            AdminDecreaseOperation.ScheduleDeletion => administration.ScheduleEntraUserDeletionAsync(
                targetUserId,
                currentUserId,
                TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    public enum AdminDecreaseOperation
    {
        Deactivate,
        RemoveRole,
        ScheduleDeletion
    }

    private sealed class IdentityTestContext : IAsyncDisposable
    {
        private IdentityTestContext(PostgreSqlTestDatabase database, QmsWebApplicationFactory factory)
        {
            Database = database;
            Factory = factory;
        }

        private PostgreSqlTestDatabase Database { get; }
        private QmsWebApplicationFactory Factory { get; }

        public IServiceProvider Services => Factory.Services;

        public static async Task<IdentityTestContext> CreateAsync(IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var database = await PostgreSqlTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
            var configuration = database.CreateConfiguration(new Dictionary<string, string?>
            {
                ["DevAuthentication:Enabled"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["DevelopmentData:SeedEnabled"] = "true"
            });
            var values = configuration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            if (overrides is not null)
            {
                foreach (var item in overrides)
                {
                    values[item.Key] = item.Value;
                }
            }

            var factory = QmsWebApplicationFactory.Create(
                "Testing",
                values,
                includeDefaultDevelopmentAuthentication: true);

            _ = factory.Services;
            return new IdentityTestContext(database, factory);
        }

        public Task ExecuteSqlAsync(string sql)
        {
            return Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
        }

        public Task<T> ReadScalarAsync<T>(string sql)
        {
            return Database.ReadScalarAsync<T>(sql, TestContext.Current.CancellationToken);
        }

        public Task<long> CountCanonicalActiveAdministratorsAsync()
        {
            return ReadScalarAsync<long>("""
                select count(*)
                from qms_users u
                where u.is_active = true
                  and u.auth_provider = 'EntraId'
                  and u.deletion_requested_at_utc is null
                  and u.scheduled_hard_delete_at_utc is null
                  and u.purge_blocked_at_utc is null
                  and exists (
                      select 1
                      from user_roles ur
                      join roles r on r.id = ur.role_id
                      where ur.user_id = u.id
                        and r.code = 'system-administrator'
                  );
                """);
        }

        public async Task<CanonicalRoleLock> LockCanonicalAdministratorRoleAsync()
        {
            var connection = await Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select id
                from roles
                where code = 'system-administrator'
                for update;
                """;
            Assert.NotNull(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            return new CanonicalRoleLock(connection, transaction);
        }

        public async Task WaitForLockWaitersAsync(int expectedCount)
        {
            for (var attempt = 0; attempt < 100; attempt += 1)
            {
                var waiters = await ReadScalarAsync<long>("""
                    select count(*)
                    from pg_stat_activity
                    where datname = current_database()
                      and wait_event_type = 'Lock';
                    """);
                if (waiters >= expectedCount)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
            }

            throw new TimeoutException("Expected PostgreSQL lock waiters were not observed.");
        }

        public async ValueTask DisposeAsync()
        {
            Factory.Dispose();
            await Database.DisposeAsync();
        }
    }

    private sealed class CanonicalRoleLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IAsyncDisposable
    {
        private bool _released;

        public async Task ReleaseAsync()
        {
            if (_released)
            {
                return;
            }

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            _released = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_released)
            {
                await transaction.RollbackAsync();
            }

            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private PostgreSqlTestDatabase(string databaseName, IConfiguration baseConfiguration)
        {
            DatabaseName = databaseName;
            BaseConfiguration = baseConfiguration;
        }

        private string DatabaseName { get; }
        private IConfiguration BaseConfiguration { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var baseConfiguration = BuildBaseDatabaseConfiguration(repositoryRoot);
            var databaseName = $"emi_qms_test_{Guid.NewGuid():N}";
            var adminConnectionString = BuildConnectionString(baseConfiguration, "postgres");

            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"create database {QuoteIdentifier(databaseName)};");
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new PostgreSqlTestDatabase(databaseName, baseConfiguration);
        }

        public IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> overrides)
        {
            var values = BaseConfiguration.AsEnumerable()
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            values["DATABASE_NAME"] = DatabaseName;
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        public async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<T> ReadScalarAsync<T>(string sql, CancellationToken cancellationToken)
        {
            await using var dataSource = NpgsqlDataSource.Create(BuildConnectionString(BaseConfiguration, DatabaseName));
            await using var command = dataSource.CreateCommand(sql);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            Assert.NotNull(value);
            return (T)value;
        }

        public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new NpgsqlConnection(BuildConnectionString(BaseConfiguration, DatabaseName));
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            var adminConnectionString = BuildConnectionString(BaseConfiguration, "postgres");
            await using var dataSource = NpgsqlDataSource.Create(adminConnectionString);
            await using var command = dataSource.CreateCommand($"drop database if exists {QuoteIdentifier(DatabaseName)} with (force);");
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string value)
        {
            return new NpgsqlCommandBuilder().QuoteIdentifier(value);
        }

        private static IConfiguration BuildBaseDatabaseConfiguration(string repositoryRoot)
        {
            var values = LoadDotEnv(Path.Combine(repositoryRoot, ".env"));
            return TestConfigurationIsolation.BuildBaseDatabaseConfiguration(values);
        }

        private static string BuildConnectionString(IConfiguration configuration, string databaseName)
        {
            var provider = new DatabaseConnectionStringProvider(configuration);
            var configured = provider.GetConnectionString();
            Assert.False(string.IsNullOrWhiteSpace(configured));

            var builder = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = databaseName,
                Pooling = false
            };

            return builder.ConnectionString;
        }

        private static Dictionary<string, string?> LoadDotEnv(string envPath)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(envPath))
            {
                return values;
            }

            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    values[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
                }
            }

            return values;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "database", "migrations")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root.");
        }
    }
}
