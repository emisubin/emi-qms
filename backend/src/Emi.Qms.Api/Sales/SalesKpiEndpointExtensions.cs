using System.Data;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.Sales;

public static class SalesKpiEndpointExtensions
{
    public static IEndpointRouteBuilder MapSalesKpiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales");

        group.MapGet("/kpi", async (int? year, string? currency, SalesKpiStore store, ClaimsPrincipal user, CancellationToken token)
            => await Safe(() => store.GetAsync(year, currency, Scope(user), token)))
            .RequireAuthorization(QmsPolicies.ProjectSalesAmountRead)
            .WithName("GetSalesKpi");

        group.MapGet("/kpi/months/{month:int}", async (int month, int year, string currency, SalesKpiStore store, ClaimsPrincipal user, CancellationToken token)
            => await Safe(() => store.GetMonthAsync(year, month, currency, Scope(user), token)))
            .RequireAuthorization(QmsPolicies.ProjectSalesAmountRead)
            .WithName("GetSalesKpiMonth");

        group.MapGet("/targets", async (int year, string currency, SalesKpiStore store, CancellationToken token)
            => await Safe(() => store.GetTargetsAsync(year, currency, token)))
            .RequireAuthorization(QmsPolicies.ProjectSalesAmountRead)
            .WithName("GetSalesTargets");

        group.MapPut("/targets", async (SaveSalesTargetsRequest request, SalesKpiStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user);
            return actor is null ? Results.Unauthorized() : await Safe(() => store.SaveTargetsAsync(request, actor.Value, token));
        })
            .RequireAuthorization(QmsPolicies.SalesTargetManage)
            .WithName("SaveSalesTargets");

        return app;
    }

    private static async Task<IResult> Safe<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (DBConcurrencyException exception)
        {
            return Results.Problem(title: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            var key = string.IsNullOrWhiteSpace(exception.ParamName) ? "request" : exception.ParamName;
            return Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [exception.Message] });
        }
    }

    private static Guid? UserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;

    private static ProjectAccessScope Scope(ClaimsPrincipal user) => new(
        user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
        user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
}
