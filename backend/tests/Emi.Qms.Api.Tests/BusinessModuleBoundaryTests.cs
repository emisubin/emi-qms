using Emi.Qms.Api.BusinessUnits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class BusinessModuleBoundaryTests
{
    [Theory]
    [InlineData("/cheongju/api/projects", "OSAN")]
    [InlineData("/osan/api/osan/projects", "CHEONGJU")]
    [InlineData("/access/api/projects", "CHEONGJU")]
    [InlineData("/api/osan/projects", "OSAN")]
    [InlineData("/cheongju/api/osan/projects", "CHEONGJU")]
    [InlineData("/osan/api/me", "CHEONGJU")]
    public async Task Wrong_route_or_selector_is_denied_before_any_business_handler(string path, string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers[BusinessUnitHeaderNames.Selection] = header;
        context.Response.Body = new MemoryStream();
        var called = false;
        var middleware = new BusinessUnitRouteMiddleware(_ => { called = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, Provider());
        Assert.Equal(403, context.Response.StatusCode);
        Assert.False(called);
        Assert.Null(context.Features.Get<BusinessUnitRoute>());
    }

    [Theory]
    [InlineData("/cheongju/api/notices", BusinessUnitCodes.Cheongju, "/api/notices")]
    [InlineData("/osan/api/osan/projects", BusinessUnitCodes.Osan, "/api/osan/projects")]
    public async Task URL_alone_fixes_the_business_before_authentication(string path, string code, string endpointPath)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new BusinessUnitRouteMiddleware(ctx =>
        {
            Assert.Equal(code, ctx.Features.Get<BusinessUnitRoute>()!.Code);
            Assert.Equal(endpointPath, ctx.Request.Path.Value);
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, Provider());
        Assert.Equal(path, context.Request.Path.Value);
    }

    [Fact]
    public void Fixed_modules_cannot_be_redirected_to_the_other_database_or_privileged_role()
    {
        var provider = Provider();
        var cheongju = new CheongjuDatabase(provider);
        var osan = new OsanDatabase(provider);
        Assert.Equal("schema_c", new NpgsqlConnectionStringBuilder(cheongju.GetConnectionString()).Database);
        Assert.Equal("schema_o", new NpgsqlConnectionStringBuilder(osan.GetConnectionString()).Database);
        Assert.Throws<BusinessUnitContextUnavailableException>(() =>
            cheongju.GetConnectionString(provider.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan)));
        Assert.Throws<BusinessUnitContextUnavailableException>(() =>
            osan.GetConnectionString(osan.GetCurrentBusinessUnit(), BusinessUnitConnectionPurpose.Administrator));
        Assert.Single(cheongju.BusinessUnits.Businesses);
        Assert.Throws<BusinessUnitContextUnavailableException>(() => cheongju.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan));
    }

    [Fact]
    public void Shared_service_has_one_immutable_target_per_scope_and_no_missing_context_fallback()
    {
        var provider = Provider();
        Assert.Throws<BusinessUnitContextUnavailableException>(() => new BusinessDatabase(provider).GetConnectionString());
        var scope = new BusinessDatabaseScope();
        scope.Bind(provider.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan));
        var database = new BusinessDatabase(provider, scope);
        Assert.Equal("schema_o", new NpgsqlConnectionStringBuilder(database.GetConnectionString()).Database);
        Assert.Throws<BusinessUnitContextUnavailableException>(() => scope.Bind(provider.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju)));
        Assert.Throws<BusinessUnitContextUnavailableException>(() => database.GetConnectionString(provider.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju)));
    }

    [Theory]
    [InlineData("CHEONGJU")]
    [InlineData("OSAN")]
    public async Task Common_login_cannot_select_business_from_a_header(string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/access/api/me";
        context.Request.Headers[BusinessUnitHeaderNames.Selection] = header;
        var middleware = new BusinessUnitRouteMiddleware(ctx =>
        {
            var requested = BusinessUnitResolver.ReadRequestedBusinessUnit(ctx.Request);
            Assert.Null(requested.Code);
            Assert.False(requested.Invalid);
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, Provider());
    }

    [Fact]
    public async Task Multi_database_role_bootstrap_requires_a_target_before_opening_any_connection()
    {
        var bootstrapper = new DatabaseRoleBootstrapper(Configuration(), new DatabaseRuntimePrivilegeManager(),
            NullLogger<DatabaseRoleBootstrapper>.Instance);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrapper.BootstrapAsync(TestContext.Current.CancellationToken));
        Assert.Equal("business_unit_bootstrap_target_required", error.Message);
    }

    [Fact]
    public async Task Concurrent_business_scopes_do_not_share_target_state()
    {
        var provider = Provider();
        async Task<string> Read(string code)
        {
            var scope = new BusinessDatabaseScope();
            scope.Bind(provider.BusinessUnits.GetBusiness(code));
            var database = new BusinessDatabase(provider, scope);
            await Task.Yield();
            return new NpgsqlConnectionStringBuilder(database.GetConnectionString()).Database!;
        }
        var values = await Task.WhenAll(Read(BusinessUnitCodes.Cheongju), Read(BusinessUnitCodes.Osan));
        Assert.Equal(["schema_c", "schema_o"], values);
    }

    private static DatabaseConnectionStringProvider Provider() => new(Configuration());

    private static IConfiguration Configuration()
    {
        var values = BusinessUnitIsolationTests.IsolationDatabaseSet.BuildConfigurationValues(
            "schema_directory", "schema_c", "schema_o", "dir_migrate", "dir_runtime", "c_migrate", "c_runtime",
            "o_migrate", "o_runtime", "Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic-only", "synthetic-only");
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
