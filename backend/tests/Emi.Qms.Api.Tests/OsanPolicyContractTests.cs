using Emi.Qms.Api.OsanProjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanPolicyContractTests
{
    [Fact]
    public void CustomerMatchingRequiresActualEnteredNameOverlap()
    {
        Assert.True(OsanPolicyStore.CustomerMatches("엘 이 엠", "엘이엠전자"));
        Assert.False(OsanPolicyStore.CustomerMatches("엘이엠", "삼성전자"));
        Assert.False(OsanPolicyStore.CustomerMatches("  ", "엘이엠전자"));
        Assert.False(OsanPolicyStore.CustomerMatches("엘이엠전자 안성", "엘이엠전자 오산"));
    }

    [Fact]
    public void CustomerAndGatePolicyRoutesRequireAuthentication()
    {
        using var factory=new QmsWebApplicationFactory();
        var routes=factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Where(e=>e.RoutePattern.RawText?.StartsWith("/api/osan/",StringComparison.Ordinal)==true)
            .ToArray();
        foreach(var path in new[] {
            "/api/osan/customers", "/api/osan/admin/customers",
            "/api/osan/admin/customer-assignments",
            "/api/osan/admin/gates", "/api/osan/gate-approvals" })
        {
            var route=Assert.Single(routes,e=>e.RoutePattern.RawText==path &&
                e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Get)==true);
            Assert.NotEmpty(route.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }
    }
}
