using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Authorization;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIdentityStore identityStore,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    BusinessUnitResolver businessUnitResolver)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly EventId AuthenticationFailedEventId = new(2001, "DevelopmentAuthenticationFailed");

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(DevelopmentAuthenticationDefaults.UserHeader, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        var developmentUserKey = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(developmentUserKey))
        {
            LogAuthenticationFailed("empty_development_user", developmentUserKey);
            return AuthenticateResult.NoResult();
        }

        var decision = DevelopmentFeaturePolicy.EvaluateDevelopmentAuthentication(environment, configuration);
        if (!decision.IsAllowedEnvironment)
        {
            LogAuthenticationFailed("development_authentication_environment_not_allowed", developmentUserKey);
            return AuthenticateResult.NoResult();
        }

        if (!decision.IsEnabled)
        {
            LogAuthenticationFailed("development_authentication_not_enabled", developmentUserKey);
            return AuthenticateResult.NoResult();
        }

        BusinessUnitRequestContext? businessUnit = null;
        if (businessUnitResolver.IsEnabled)
        {
            businessUnit = await businessUnitResolver.ResolveAsync(
                Context,
                QmsAuthProviders.Dev,
                developmentUserKey,
                Context.RequestAborted);
            BusinessUnitRequestContextFeature.Set(Context, businessUnit);
            if (!businessUnit.IsSelected || businessUnit.DirectoryUserId is null)
            {
                return PendingAuthentication(developmentUserKey, businessUnit);
            }
        }

        var profile = businessUnitResolver.IsEnabled
            ? await identityStore.GetProfileByUserIdAsync(
                businessUnit!.DirectoryUserId!.Value,
                Context.RequestAborted)
            : await identityStore.GetProfileByDevelopmentUserKeyAsync(
                developmentUserKey,
                Context.RequestAborted);

        if (profile is null
            || (businessUnitResolver.IsEnabled
                && (!string.Equals(profile.User.AuthProvider, QmsAuthProviders.Dev, StringComparison.Ordinal)
                    || !string.Equals(profile.User.DevelopmentUserKey, developmentUserKey, StringComparison.Ordinal))))
        {
            return businessUnit is null
                ? UnknownDevelopmentUser(developmentUserKey)
                : PendingAuthentication(
                    developmentUserKey,
                    businessUnit with
                    {
                        Status = BusinessUnitAccessStatuses.LocalProfilePending,
                        Reason = "business_unit_local_profile_pending"
                    });
        }

        if (!profile.User.IsActive)
        {
            LogAuthenticationFailed("inactive_development_user", developmentUserKey);
            return AuthenticateResult.Fail("Development authentication failed.");
        }

        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, profile.User.Id.ToString("D")),
            new(QmsClaimTypes.DevelopmentUserKey, profile.User.DevelopmentUserKey),
            new(ClaimTypes.NameIdentifier, profile.User.Id.ToString("D")),
            new(ClaimTypes.Name, profile.User.DevelopmentUserKey)
        };

        if (businessUnit is not null)
        {
            claims.Add(new Claim(QmsClaimTypes.BusinessUnitAccessStatus, businessUnit.Status));
            claims.Add(new Claim(QmsClaimTypes.BusinessUnit, businessUnit.Target!.Code));
            claims.Add(new Claim(QmsClaimTypes.IsOverallAdministrator, businessUnit.IsOverallAdministrator.ToString()));
        }

        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim(QmsClaimTypes.Permission, permission.Code)));
        claims.AddRange(profile.ProjectAccess.Select(project => new Claim(QmsClaimTypes.Project, project.ProjectKey)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private AuthenticateResult UnknownDevelopmentUser(string developmentUserKey)
    {
        LogAuthenticationFailed("unknown_development_user", developmentUserKey);
        return AuthenticateResult.Fail("Development authentication failed.");
    }

    private AuthenticateResult PendingAuthentication(
        string developmentUserKey,
        BusinessUnitRequestContext businessUnit)
    {
        var claims = new List<Claim>
        {
            new(QmsClaimTypes.DevelopmentUserKey, developmentUserKey),
            new(QmsClaimTypes.AuthProvider, QmsAuthProviders.Dev),
            new(QmsClaimTypes.ApprovalPending, bool.TrueString),
            new(QmsClaimTypes.Inactive, bool.FalseString),
            new(QmsClaimTypes.BusinessUnitAccessStatus, businessUnit.Status),
            new(QmsClaimTypes.IsOverallAdministrator, businessUnit.IsOverallAdministrator.ToString()),
            new(ClaimTypes.Name, developmentUserKey)
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

        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
            Scheme.Name));
    }

    private void LogAuthenticationFailed(string reasonCode, string developmentUserKey)
    {
        Logger.LogWarning(
            AuthenticationFailedEventId,
            "Development authentication failed. reason={ReasonCode} path={Path} method={Method} occurredAtUtc={OccurredAtUtc} developmentUserKeyHash={DevelopmentUserKeyHash}",
            reasonCode,
            Request.Path.Value ?? "/",
            Request.Method,
            timeProvider.GetUtcNow(),
            HashDevelopmentUserKey(developmentUserKey));
    }

    private static string HashDevelopmentUserKey(string developmentUserKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(developmentUserKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
