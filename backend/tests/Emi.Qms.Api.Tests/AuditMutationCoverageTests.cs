using Emi.Qms.Api.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class AuditMutationCoverageTests
{
    [Fact]
    public void Registry_ExactlyCoversEveryMutationEndpoint()
    {
        using var factory = new QmsWebApplicationFactory();
        var actual = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Where(AuditMutationRegistry.IsMutationMethod)
                .Select(method => AuditMutationRegistry.BuildRouteKey(method, endpoint.RoutePattern.RawText))
                ?? [])
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expected = AuditMutationRegistry.KnownMutationRouteKeys.Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", actual)}");
        AuditMutationRegistry.ValidateCoverage([
            factory.Services.GetRequiredService<EndpointDataSource>()
        ]);
    }

    [Fact]
    public void FailureClassification_UsesOnlyApprovedValidationAndConflictReasons()
    {
        Assert.Equal(
            [AuditFailureReasons.Conflict, AuditFailureReasons.Validation],
            AuditFailureReasons.All.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            AuditFailureReasons.Conflict,
            AuditMutationRegistry.ResolveConflictReason("POST /api/pending/"));
        Assert.Throws<InvalidOperationException>(() =>
            AuditMutationRegistry.ResolveConflictReason("POST /api/not-a-real-route"));
    }

    [Fact]
    public void DirectoryMembershipMutation_IsKnownAndExcludedFromLocalBusinessAudit()
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoint = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(
                candidate.RoutePattern.RawText,
                "/api/admin/business-unit-access/users/{userId:guid}/memberships",
                StringComparison.Ordinal));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.SetEndpoint(endpoint);

        Assert.True(AuditMutationRegistry.TryResolve(context, out var definition));
        Assert.False(definition.Included);
    }

    [Theory]
    [InlineData("PUT", "/api/g2/materials/{date}")]
    [InlineData("POST", "/api/g2/materials/import")]
    public void G2MaterialMutations_StartAndAreDeliberatelyExcluded(string method, string route)
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>();
        AuditMutationRegistry.ValidateCoverage([endpoints]);
        var endpoint = endpoints.Endpoints.OfType<RouteEndpoint>().Single(x => x.RoutePattern.RawText == route);
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.SetEndpoint(endpoint);
        Assert.True(AuditMutationRegistry.TryResolve(context, out var definition));
        Assert.False(definition.Included);
    }

    [Fact]
    public void OsanProjectCreate_IsKnownAndIncludedInProjectAudit()
    {
        using var factory = new QmsWebApplicationFactory();
        var endpoint = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(
                candidate.RoutePattern.RawText,
                "/api/osan/projects/",
                StringComparison.Ordinal)
                && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Post,
                    StringComparer.OrdinalIgnoreCase) == true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.SetEndpoint(endpoint);

        Assert.True(AuditMutationRegistry.TryResolve(context, out var definition));
        Assert.True(definition.Included);
        Assert.Equal("Projects", definition.Domain);
        Assert.Equal("CreateOsanProject", definition.Action);
    }
}
