using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Materials;

public static class MaterialsEndpointExtensions
{
    public static IEndpointRouteBuilder MapMaterialsEndpoints(this IEndpointRouteBuilder app)
    {
        var materials = app.MapGroup("/api/materials");

        materials.MapGet("/receipts", async (
            HttpRequest request,
            string? search,
            bool? includeCompleted,
            MaterialsStore store,
            CancellationToken cancellationToken) =>
        {
            var dateRange = ParseDateRange(request);
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(dateRange.Errors);
            }
            return Results.Ok(await store.ListAsync(search, includeCompleted == true, dateRange.From, dateRange.To, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("ListMaterialReceivingItems");

        materials.MapPatch("/receipts", () => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ReceiptCompleted"] = ["입고 완료값은 도착 등록, IQC 판정, 입고 확정 흐름에서 자동 계산됩니다."]
        }))
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("RejectLegacyMaterialReceiptUpdate");

        materials.MapPost("/items/{itemId:guid}/receipts", async (
            Guid itemId,
            RegisterMaterialArrivalRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.RegisterArrivalAsync(itemId, request, userId.Value, context.TraceIdentifier, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("RegisterMaterialArrival");

        materials.MapPost("/receipts/{receiptId:guid}/iqc-requests", async (
            Guid receiptId,
            MaterialReceiptVersionRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.RequestIqcAsync(receiptId, request, userId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("RequestMaterialIqc");

        materials.MapPost("/receipts/{receiptId:guid}/reinspection", async (
            Guid receiptId,
            MaterialReceiptVersionRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.RequestReinspectionAsync(receiptId, request, userId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("RequestMaterialIqcReinspection");

        materials.MapPost("/receipts/{receiptId:guid}/confirm", async (
            Guid receiptId,
            MaterialReceiptVersionRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.ConfirmAsync(receiptId, request, userId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("ConfirmMaterialReceipt");

        materials.MapPost("/receipts/{receiptId:guid}/cancel", async (
            Guid receiptId,
            CancelMaterialReceiptRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.CancelAsync(receiptId, request, userId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("CancelMaterialArrival");

        materials.MapPost("/items/{itemId:guid}/close-arrivals", async (
            Guid itemId,
            CloseMaterialArrivalsRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.CloseArrivalsAsync(itemId, request, userId.Value, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.MaterialReceiptUpdate)
        .WithName("CloseMaterialArrivals");

        var quality = app.MapGroup("/api/quality/iqc");
        quality.MapGet("/", async (bool? includeDecided, MaterialsStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListIqcAsync(includeDecided == true, cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("ListMaterialIqcRequests");

        quality.MapPost("/{attemptId:guid}/result", async (
            Guid attemptId,
            MaterialIqcResultRequest request,
            MaterialsStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.RecordIqcResultAsync(attemptId, request, userId.Value, context.TraceIdentifier, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("RecordMaterialIqcResult");

        return app;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(QmsClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult ToResult(MaterialsMutationResult<MaterialReceiptActionResponse> result)
    {
        return result.Status switch
        {
            MaterialsMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            MaterialsMutationStatus.NotFound => Results.NotFound(),
            MaterialsMutationStatus.Validation => Results.ValidationProblem(result.Errors),
            MaterialsMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "요청한 작업을 수행할 수 없습니다.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static DateRange ParseDateRange(HttpRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var fromRaw = request.Query["expectedReceiptDateFrom"].ToString();
        var toRaw = request.Query["expectedReceiptDateTo"].ToString();
        DateOnly? from = null;
        DateOnly? to = null;
        if (!string.IsNullOrWhiteSpace(fromRaw) && !DateOnly.TryParse(fromRaw, out _))
        {
            errors["expectedReceiptDateFrom"] = ["올바른 날짜 형식이 아닙니다."];
        }
        else if (!string.IsNullOrWhiteSpace(fromRaw))
        {
            from = DateOnly.Parse(fromRaw, System.Globalization.CultureInfo.InvariantCulture);
        }
        if (!string.IsNullOrWhiteSpace(toRaw) && !DateOnly.TryParse(toRaw, out _))
        {
            errors["expectedReceiptDateTo"] = ["올바른 날짜 형식이 아닙니다."];
        }
        else if (!string.IsNullOrWhiteSpace(toRaw))
        {
            to = DateOnly.Parse(toRaw, System.Globalization.CultureInfo.InvariantCulture);
        }
        if (from is not null && to is not null && from > to)
        {
            errors["expectedReceiptDateFrom"] = ["시작일은 종료일보다 늦을 수 없습니다."];
        }
        return new DateRange(from, to, errors);
    }

    private sealed record DateRange(DateOnly? From, DateOnly? To, IReadOnlyDictionary<string, string[]> Errors);
}
