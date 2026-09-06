using System.Security.Claims;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.AspNetCore.Authentication;

namespace Emi.Qms.Api.Authorization;

public sealed class EntraClaimsTransformation(
    DbIdentityStore dbIdentityStore,
    InMemoryIdentityStore developmentIdentityStore,
    IConfiguration configuration,
    IHostEnvironment environment,
    IHttpContextAccessor httpContextAccessor,
    BusinessUnitResolver businessUnitResolver)
    : IClaimsTransformation
{
    private const string MicrosoftObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string UpnClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(claim => claim.Type == QmsClaimTypes.UserId)
            && (!businessUnitResolver.IsEnabled
                || BusinessUnitRequestContextFeature.Get(httpContextAccessor.HttpContext) is not null))
        {
            return principal;
        }

        var objectId = FindFirstValue(principal, "oid", MicrosoftObjectIdClaimType);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return principal;
        }

        var displayName = FindFirstValue(principal, "name", ClaimTypes.Name)
            ?? "Microsoft 365 사용자";
        var email = FindFirstValue(principal, "preferred_username", ClaimTypes.Email, "email", UpnClaimType);

        BusinessUnitRequestContext? businessUnit = null;
        if (businessUnitResolver.IsEnabled)
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new BusinessUnitContextUnavailableException("http_context_missing");
            businessUnit = await businessUnitResolver.ResolveAsync(
                httpContext,
                QmsAuthProviders.EntraId,
                objectId,
                httpContext.RequestAborted);
            BusinessUnitRequestContextFeature.Set(httpContext, businessUnit);
            if (!businessUnit.IsSelected || businessUnit.DirectoryUserId is null)
            {
                principal.AddIdentity(new ClaimsIdentity(
                    BuildPendingClaims(businessUnit, displayName),
                    QmsAuthenticationSchemes.EntraBearer));
                return principal;
            }
        }

        var profile = businessUnitResolver.IsEnabled
            ? await dbIdentityStore.GetProfileByEntraObjectIdAsync(
                objectId,
                httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None)
            : ReviewSafeMode.IsEnabled(configuration)
                ? await dbIdentityStore.GetProfileByEntraObjectIdAsync(objectId, CancellationToken.None)
                : await dbIdentityStore.GetOrCreateEntraProfileAsync(
                    objectId,
                    displayName,
                    email,
                    CancellationToken.None);
        if (profile is null
            || (businessUnit?.DirectoryUserId is Guid directoryUserId
                && profile.User.Id != directoryUserId))
        {
            if (businessUnit is not null)
            {
                principal.AddIdentity(new ClaimsIdentity(
                    BuildPendingClaims(
                        businessUnit with
                        {
                            Status = BusinessUnitAccessStatuses.LocalProfilePending,
                            Reason = "business_unit_local_profile_pending"
                        },
                        displayName),
                    QmsAuthenticationSchemes.EntraBearer));
            }
            return principal;
        }

        var requestedTestUserKey = ReadRequestedTestUserKey();
        if (string.IsNullOrWhiteSpace(requestedTestUserKey))
        {
            var actualClaims = BuildClaims(profile, includeAuthorizationClaims: true, businessUnit);
            actualClaims.Add(new Claim(QmsClaimTypes.IsTestUserSwitch, bool.FalseString));
            principal.AddIdentity(new ClaimsIdentity(actualClaims, QmsAuthenticationSchemes.EntraBearer));
            return principal;
        }

        var denialReason = businessUnitResolver.IsEnabled
            ? AdminUserSwitchDefaults.ReasonDisabled
            : await ValidateTestUserSwitchAsync(profile, requestedTestUserKey);
        if (denialReason is not null)
        {
            var deniedClaims = BuildClaims(profile, includeAuthorizationClaims: true, businessUnit);
            deniedClaims.Add(new Claim(QmsClaimTypes.IsTestUserSwitch, bool.FalseString));
            deniedClaims.Add(new Claim(QmsClaimTypes.TestUserSwitchDeniedReason, denialReason));
            principal.AddIdentity(new ClaimsIdentity(deniedClaims, QmsAuthenticationSchemes.EntraBearer));
            return principal;
        }

        var effectiveProfile = await developmentIdentityStore.GetProfileByDevelopmentUserKeyAsync(
            requestedTestUserKey,
            CancellationToken.None);
        if (effectiveProfile is null || !effectiveProfile.User.IsActive)
        {
            var deniedClaims = BuildClaims(profile, includeAuthorizationClaims: true, businessUnit);
            deniedClaims.Add(new Claim(QmsClaimTypes.IsTestUserSwitch, bool.FalseString));
            deniedClaims.Add(new Claim(QmsClaimTypes.TestUserSwitchDeniedReason, AdminUserSwitchDefaults.ReasonInvalidTestUser));
            principal.AddIdentity(new ClaimsIdentity(deniedClaims, QmsAuthenticationSchemes.EntraBearer));
            return principal;
        }

        var switchedClaims = BuildClaims(effectiveProfile, includeAuthorizationClaims: true, businessUnit);
        switchedClaims.Add(new Claim(QmsClaimTypes.IsTestUserSwitch, bool.TrueString));
        switchedClaims.Add(new Claim(QmsClaimTypes.TestUserKey, requestedTestUserKey));
        switchedClaims.Add(new Claim(QmsClaimTypes.ActualUserId, profile.User.Id.ToString("D")));
        switchedClaims.Add(new Claim(QmsClaimTypes.ActualAuthProvider, profile.User.AuthProvider));
        switchedClaims.AddRange(profile.Roles.Select(role => new Claim(QmsClaimTypes.ActualRole, role.Code)));

        principal.AddIdentity(new ClaimsIdentity(switchedClaims, QmsAuthenticationSchemes.EntraBearer));
        return principal;
    }

    private async Task<string?> ValidateTestUserSwitchAsync(UserAuthorizationProfile actualProfile, string requestedTestUserKey)
    {
        var decision = DevelopmentFeaturePolicy.EvaluateAdminUserSwitch(environment, configuration);
        if (!decision.IsEnabled)
        {
            return AdminUserSwitchDefaults.ReasonDisabled;
        }

        if (!actualProfile.User.IsActive
            || IsApprovalPending(actualProfile)
            || !actualProfile.Roles.Any(role => string.Equals(role.Code, QmsRoles.SystemAdministrator, StringComparison.Ordinal)))
        {
            return AdminUserSwitchDefaults.ReasonNotAllowed;
        }

        if (!AdminUserSwitchDefaults.AllowedDevelopmentUserKeys.Contains(requestedTestUserKey))
        {
            return AdminUserSwitchDefaults.ReasonInvalidTestUser;
        }

        var effectiveProfile = await developmentIdentityStore.GetProfileByDevelopmentUserKeyAsync(
            requestedTestUserKey,
            CancellationToken.None);

        return effectiveProfile is null || !effectiveProfile.User.IsActive
            ? AdminUserSwitchDefaults.ReasonInvalidTestUser
            : null;
    }

    private string? ReadRequestedTestUserKey()
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[AdminUserSwitchDefaults.HeaderName].ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<Claim> BuildClaims(
        UserAuthorizationProfile profile,
        bool includeAuthorizationClaims,
        BusinessUnitRequestContext? businessUnit)
    {
        var approvalPending = IsApprovalPending(profile);
        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, profile.User.Id.ToString("D")),
            new(QmsClaimTypes.AuthProvider, profile.User.AuthProvider),
            new(QmsClaimTypes.ApprovalPending, approvalPending.ToString()),
            new(QmsClaimTypes.Inactive, (!profile.User.IsActive).ToString()),
            new(ClaimTypes.NameIdentifier, profile.User.Id.ToString("D")),
            new(ClaimTypes.Name, profile.User.DisplayName)
        };

        if (businessUnit is not null)
        {
            claims.Add(new Claim(QmsClaimTypes.BusinessUnitAccessStatus, businessUnit.Status));
            claims.Add(new Claim(QmsClaimTypes.IsOverallAdministrator, businessUnit.IsOverallAdministrator.ToString()));
            if (businessUnit.Target is not null)
            {
                claims.Add(new Claim(QmsClaimTypes.BusinessUnit, businessUnit.Target.Code));
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.User.DevelopmentUserKey))
        {
            claims.Add(new Claim(QmsClaimTypes.DevelopmentUserKey, profile.User.DevelopmentUserKey));
        }

        if (!includeAuthorizationClaims || !profile.User.IsActive || approvalPending)
        {
            return claims;
        }

        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim(QmsClaimTypes.Permission, permission.Code)));
        claims.AddRange(profile.ProjectAccess.Select(project => new Claim(QmsClaimTypes.Project, project.ProjectKey)));
        return claims;
    }

    private static IReadOnlyList<Claim> BuildPendingClaims(
        BusinessUnitRequestContext businessUnit,
        string displayName)
    {
        var claims = new List<Claim>
        {
            new(QmsClaimTypes.ApprovalPending, bool.TrueString),
            new(QmsClaimTypes.Inactive, bool.FalseString),
            new(QmsClaimTypes.AuthProvider, QmsAuthProviders.EntraId),
            new(QmsClaimTypes.BusinessUnitAccessStatus, businessUnit.Status),
            new(QmsClaimTypes.IsOverallAdministrator, businessUnit.IsOverallAdministrator.ToString()),
            new(ClaimTypes.Name, displayName)
        };
        if (businessUnit.DirectoryUserId is Guid userId)
        {
            claims.Add(new Claim(QmsClaimTypes.UserId, userId.ToString("D")));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")));
        }
        if (businessUnit.Target is not null)
        {
            claims.Add(new Claim(QmsClaimTypes.BusinessUnit, businessUnit.Target.Code));
        }
        return claims;
    }

    private static bool IsApprovalPending(UserAuthorizationProfile profile)
    {
        return profile.User.AuthProvider == QmsAuthProviders.EntraId
            && profile.User.IsActive
            && profile.Roles.Count == 0;
    }

    private static string? FindFirstValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
