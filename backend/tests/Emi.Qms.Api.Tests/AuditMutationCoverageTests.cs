using Emi.Qms.Api.Audit;
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
}
