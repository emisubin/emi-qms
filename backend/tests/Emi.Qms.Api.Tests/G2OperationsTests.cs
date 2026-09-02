using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class G2OperationsTests(QmsWebApplicationFactory factory) : IClassFixture<QmsWebApplicationFactory>
{
    [Fact]
    public void InventoryCalculator_StartsAtFirstPhysicalCountAndPreservesNegativeValues()
    {
        var from = new DateOnly(2026, 8, 1);
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(4),
            null,
            new Dictionary<DateOnly, int> { [from.AddDays(1)] = 5 },
            new Dictionary<DateOnly, long> { [from] = 20, [from.AddDays(2)] = 3 },
            new Dictionary<DateOnly, long> { [from.AddDays(2)] = 10, [from.AddDays(3)] = 2 },
            new Dictionary<DateOnly, long> { [from.AddDays(2)] = 1 });

        Assert.Null(result[from]);
        Assert.Equal(5, result[from.AddDays(1)]);
        Assert.Equal(-3, result[from.AddDays(2)]);
        Assert.Equal(-5, result[from.AddDays(3)]);
        Assert.Equal(-5, result[from.AddDays(4)]);
    }

    [Fact]
    public void InventoryCalculator_LaterPhysicalCountIsAnImmutableBoundaryForEarlierCorrections()
    {
        var from = new DateOnly(2026, 8, 1);
        var counts = new Dictionary<DateOnly, int> { [from] = 100, [from.AddDays(3)] = 80 };
        var before = G2InventoryCalculator.Calculate(from, from.AddDays(4), null, counts,
            new Dictionary<DateOnly, long> { [from.AddDays(1)] = 10, [from.AddDays(4)] = 5 },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long>());
        var corrected = G2InventoryCalculator.Calculate(from, from.AddDays(4), null, counts,
            new Dictionary<DateOnly, long> { [from.AddDays(1)] = 25, [from.AddDays(4)] = 5 },
            new Dictionary<DateOnly, long>(),
            new Dictionary<DateOnly, long>());

        Assert.NotEqual(before[from.AddDays(2)], corrected[from.AddDays(2)]);
        Assert.Equal(before[from.AddDays(3)], corrected[from.AddDays(3)]);
        Assert.Equal(before[from.AddDays(4)], corrected[from.AddDays(4)]);
    }

    [Fact]
    public void InventoryCalculator_UsesPreviousDayMovementsFromAvailableInventoryStartDate()
    {
        var from = G2InventoryCalculator.AvailableInventoryStartDate;
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(1),
            2,
            new Dictionary<DateOnly, int>(),
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 34,
                [from] = 47
            },
            new Dictionary<DateOnly, long>
            {
                [from.AddDays(-1)] = 30,
                [from] = 30
            },
            new Dictionary<DateOnly, long>());

        Assert.Equal(6, result[from]);
        Assert.Equal(23, result[from.AddDays(1)]);
    }

    [Fact]
    public void InventoryCalculator_PhysicalCountRemainsTheBoundaryForNextDayMovements()
    {
        var from = G2InventoryCalculator.AvailableInventoryStartDate;
        var result = G2InventoryCalculator.Calculate(
            from,
            from.AddDays(1),
            2,
            new Dictionary<DateOnly, int> { [from] = 10 },
            new Dictionary<DateOnly, long> { [from] = 47 },
            new Dictionary<DateOnly, long> { [from] = 30 },
            new Dictionary<DateOnly, long> { [from] = 2 });

        Assert.Equal(10, result[from]);
        Assert.Equal(25, result[from.AddDays(1)]);
    }

    [Theory]
    [InlineData("dev-admin", true, true, true, true, true)]
    [InlineData("dev-sales", true, true, true, true, true)]
    [InlineData("dev-manufacturing", true, false, true, true, true)]
    [InlineData("dev-logistics", false, true, false, false, false)]
    [InlineData("dev-quality", false, false, false, false, false)]
    [InlineData("dev-viewer", false, false, false, false, false)]
    public async Task G2Policies_UseTheApprovedRoleMatrix(
        string userKey,
        bool production,
        bool delivery,
        bool attendance,
        bool inventory,
        bool target)
    {
        var profile = await new InMemoryIdentityStore().GetProfileByDevelopmentUserKeyAsync(userKey, TestContext.Current.CancellationToken);
        Assert.NotNull(profile);
        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, profile.User.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier, profile.User.Id.ToString("D"))
        };
        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim(QmsClaimTypes.Permission, permission.Code)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.Scheme));
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2Read)).Succeeded);
        Assert.Equal(production, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2ProductionUpdate)).Succeeded);
        Assert.Equal(delivery, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2DeliveryUpdate)).Succeeded);
        Assert.Equal(attendance, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2AttendanceUpdate)).Succeeded);
        Assert.Equal(inventory, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2InventoryManage)).Succeeded);
        Assert.Equal(target, (await authorization.AuthorizeAsync(principal, null, QmsPolicies.G2TargetManage)).Succeeded);
    }

    [Fact]
    public async Task OperationsEndpoint_RejectsEntireMixedPermissionRequestBeforeDatabaseWrite()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-manufacturing");
        using var response = await client.PutAsJsonAsync(
            "/api/g2/operations/2026-08-18",
            new
            {
                morningProduction = new { quantity = 10, expectedVersion = (int?)null },
                delivery = new { quantity = 4, expectedVersion = (int?)null }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperationsEndpoint_RejectsDefectForLogisticsRole()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-logistics");
        using var response = await client.PutAsJsonAsync(
            "/api/g2/operations/2026-08-18",
            new { defect = new { quantity = 2, expectedVersion = (int?)null } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
