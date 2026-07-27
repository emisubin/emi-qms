using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.ProductionPlanning;

public static class ProductionControlTemplateEndpointExtensions
{
    public static IEndpointRouteBuilder MapProductionControlTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/production-control/templates")
            .RequireAuthorization("AuthenticatedIdentity");

        group.MapGet("", async (
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.GetCatalogAsync(UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("GetProductionControlTemplates");

        group.MapPost("/manufacturing/{productTypeId:guid}/drafts", async (
            Guid productTypeId,
            CreateProductionControlDraftRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.CreateManufacturingDraftAsync(productTypeId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("CreateProductionControlManufacturingDraft");

        group.MapPut("/manufacturing/{productTypeId:guid}/versions/{versionId:guid}", async (
            Guid productTypeId,
            Guid versionId,
            SaveProductionControlManufacturingVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.SaveManufacturingAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("SaveProductionControlManufacturingDraft");

        group.MapPost("/manufacturing/{productTypeId:guid}/versions/{versionId:guid}/activate", async (
            Guid productTypeId,
            Guid versionId,
            TransitionProductionControlVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.ActivateManufacturingAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("ActivateProductionControlManufacturingVersion");

        group.MapPost("/manufacturing/{productTypeId:guid}/versions/{versionId:guid}/archive", async (
            Guid productTypeId,
            Guid versionId,
            TransitionProductionControlVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.ArchiveManufacturingDraftAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("ArchiveProductionControlManufacturingDraft");

        group.MapPost("/planning/{productTypeId:guid}/drafts", async (
            Guid productTypeId,
            CreateProductionControlDraftRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.CreatePlanDraftAsync(productTypeId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("CreateProductionControlPlanDraft");

        group.MapPut("/planning/{productTypeId:guid}/versions/{versionId:guid}", async (
            Guid productTypeId,
            Guid versionId,
            SaveProductionControlPlanVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.SavePlanAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("SaveProductionControlPlanDraft");

        group.MapPost("/planning/{productTypeId:guid}/versions/{versionId:guid}/activate", async (
            Guid productTypeId,
            Guid versionId,
            TransitionProductionControlVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.ActivatePlanAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("ActivateProductionControlPlanVersion");

        group.MapPost("/planning/{productTypeId:guid}/versions/{versionId:guid}/archive", async (
            Guid productTypeId,
            Guid versionId,
            TransitionProductionControlVersionRequest request,
            ProductionControlTemplateStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
            => await Safe(() => store.ArchivePlanDraftAsync(productTypeId, versionId, request, UserId(user), IsAdmin(user), cancellationToken)))
            .WithName("ArchiveProductionControlPlanDraft");

        return app;
    }

    private static async Task<IResult> Safe<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (ProductionControlTemplateForbiddenException) { return Results.Forbid(); }
        catch (ProductionControlTemplateConflictException exception)
        {
            return Results.Problem(title: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            });
        }
    }

    private static Guid UserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value)
            ? value
            : throw new ProductionControlTemplateForbiddenException();

    private static bool IsAdmin(ClaimsPrincipal user)
        => user.IsInRole(QmsRoles.SystemAdministrator);
}
