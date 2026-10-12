using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class CheongjuMenuCapabilityTests
{
    [Theory]
    [InlineData("GET", "/api/g2/home", false, true)]
    [InlineData("PUT", "/api/g2/materials/2026-10-12", false, true)]
    [InlineData("POST", "/api/g2/materials/import", false, true)]
    [InlineData("GET", "/api/system/holidays", false, true)]
    [InlineData("PUT", "/api/system/holidays", false, false)]
    [InlineData("GET", "/api/projects", false, false)]
    [InlineData("POST", "/api/projects", false, false)]
    [InlineData("DELETE", "/api/projects/123", false, false)]
    [InlineData("GET", "/api/g2-other", false, false)]
    [InlineData("GET", "/api/admin/users", false, false)]
    [InlineData("GET", "/api/projects", true, true)]
    [InlineData("POST", "/api/projects", true, true)]
    [InlineData("GET", "/api/admin/users", true, true)]
    [InlineData("GET", "/api/me", false, true)]
    [InlineData("PUT", "/api/me/profile-photo", false, true)]
    [InlineData("GET", "/api/runtime-mode", false, true)]
    [InlineData("GET", "/api/business-units", false, true)]
    [InlineData("GET", "/api/maintenance", false, true)]
    [InlineData("POST", "/api/maintenance/release/popup/1/claim", false, true)]
    [InlineData("GET", "/api/notices/123", false, true)]
    [InlineData("POST", "/api/notices", false, false)]
    [InlineData("POST", "/api/audit/sessions/logout", false, true)]
    [InlineData("POST", "/api/audit/site-access/signals", false, true)]
    [InlineData("GET", "/api/my/web-push", false, true)]
    [InlineData("PUT", "/api/my/web-push/subscriptions", false, true)]
    [InlineData("DELETE", "/api/my/web-push", false, false)]
    public async Task Cheongju_OnlyEffectiveAdministratorsBypassMenuRestriction(
        string method, string path, bool administrator, bool allowed)
    {
        var context = CreateContext(BusinessUnitCodes.Cheongju, method, path, administrator);
        // A directory-wide administrator flag alone must not bypass an effective local role.
        var called = await InvokeAsync(context);
        Assert.Equal(allowed, called);
        Assert.Equal(allowed ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/osan/projects", true)]
    [InlineData("/api/g2/materials/latest", false)]
    public async Task Osan_KeepsExistingCapabilityBoundary(string path, bool allowed)
    {
        Assert.Equal(allowed, await InvokeAsync(CreateContext(BusinessUnitCodes.Osan, "GET", path, true)));
    }

    [Fact]
    public async Task Administrator_WithPendingLocalProfile_IsStillDenied()
    {
        var context = CreateContext(BusinessUnitCodes.Cheongju, "GET", "/api/g2/home", true, BusinessUnitAccessStatuses.LocalProfilePending);
        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ActualAdministratorSwitchedToOrdinaryUser_CannotBypassRestriction()
    {
        var context = CreateContext(BusinessUnitCodes.Cheongju, "GET", "/api/projects", false);
        ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(QmsClaimTypes.ActualRole, "system-administrator"));
        Assert.False(await InvokeAsync(context));
    }

    [Fact]
    public async Task RevokedEffectiveAdministratorRole_IsDeniedOnNextRequest()
    {
        var context = CreateContext(BusinessUnitCodes.Cheongju, "GET", "/api/projects", true);
        Assert.True(await InvokeAsync(context));
        var identity = (ClaimsIdentity)context.User.Identity!;
        identity.RemoveClaim(identity.FindFirst(ClaimTypes.Role)!);
        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("CHEONGJU", false, true)]
    [InlineData("CHEONGJU", true, false)]
    [InlineData("OSAN", false, false)]
    public void ReportedMenuPolicy_UsesRealCampusContextAndExcludesLegacy(string campus, bool legacy, bool expected)
    {
        var context = BusinessUnitRequestContextFeature.Get(CreateContext(campus, "GET", "/api/me", true))!;
        context = context with { Target = context.Target! with { IsLegacy = legacy } };
        Assert.Equal(expected, BusinessUnitAccessResponse.From(context).G2OnlyForNonAdministrators);
        Assert.False(BusinessUnitAccessResponse.From(null).G2OnlyForNonAdministrators);
        Assert.False(BusinessUnitAccessResponse.From(context with { Status = BusinessUnitAccessStatuses.LocalProfilePending }).G2OnlyForNonAdministrators);
    }

    private static DefaultHttpContext CreateContext(string campus, string method, string path, bool administrator,
        string claimStatus = BusinessUnitAccessStatuses.Selected)
    {
        var claims = new List<Claim> { new(QmsClaimTypes.BusinessUnitAccessStatus, claimStatus) };
        if (administrator) claims.Add(new(ClaimTypes.Role, "system-administrator"));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        var target = new BusinessUnitDatabaseTarget(campus, BusinessUnitDatabaseKind.Business,
            "runtime", "migration", "administrator", "unused_test_database", "migration", "runtime", "test", false, false, false);
        BusinessUnitRequestContextFeature.Set(context, new BusinessUnitRequestContext(
            BusinessUnitAccessStatuses.Selected, Guid.NewGuid(), target, [campus], true, "test_selected"));
        return context;
    }

    private static async Task<bool> InvokeAsync(DefaultHttpContext context)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["BusinessUnits:Enabled"] = "true" }).Build();
        using var provider = new DatabaseConnectionStringProvider(configuration);
        var called = false;
        await new BusinessUnitCapabilityMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, provider);
        return called;
    }
}
