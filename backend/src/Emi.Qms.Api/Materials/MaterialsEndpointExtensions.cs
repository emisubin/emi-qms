using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

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
            string? supplyType,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var dateRange = ParseDateRange(request);
            if (dateRange.Errors.Count > 0)
            {
                return Results.ValidationProblem(dateRange.Errors);
            }
            var normalizedSupplyType = NormalizeSupplyType(supplyType);
            if (supplyType is not null && normalizedSupplyType is null && !string.Equals(supplyType, "All", StringComparison.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["supplyType"] = ["공급 유형은 전체, 일반 구매 또는 사급이어야 합니다."]
                });
            }
            return Results.Ok(await store.ListAsync(
                search,
                includeCompleted == true,
                normalizedSupplyType,
                dateRange.From,
                dateRange.To,
                GetProjectAccessScope(user),
                cancellationToken));
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
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
        quality.MapGet("/", async (
            bool? includeDecided,
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListIqcAsync(includeDecided == true, GetProjectAccessScope(user), cancellationToken)))
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("ListMaterialIqcRequests");

        quality.MapPost("/reconcile", async (
            MaterialsStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await store.ReconcileIqcHandoffsAsync(
                    userId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("ReconcileMaterialIqcHandoffs");

        quality.MapGet("/{attemptId:guid}/report", async (
            Guid attemptId,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var response = await store.GetAsync(attemptId, GetProjectAccessScope(user), cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("GetMaterialIqcReport");

        quality.MapPost("/{attemptId:guid}/reports", async (
            Guid attemptId,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.InitializeAsync(attemptId, userId.Value, GetProjectAccessScope(user), cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("InitializeMaterialIqcReport");

        quality.MapPut("/reports/{reportId:guid}/responses", async (
            Guid reportId,
            SaveIqcResponsesRequest request,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.SaveResponsesAsync(reportId, request, userId.Value, GetProjectAccessScope(user), cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("SaveMaterialIqcReportResponses");

        quality.MapPost("/reports/{reportId:guid}/photos", async (
            Guid reportId,
            [FromForm] Guid templateItemId,
            [FromForm] int expectedReportVersion,
            [FromForm] string altText,
            [FromForm] IFormFile photo,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }
            if (photo.Length is < 1 or > 5 * 1024 * 1024)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["photo"] = ["사진은 5MB 이하 JPEG 또는 PNG 파일이어야 합니다."]
                });
            }
            await using var stream = photo.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return ToResult(await store.AddPhotoAsync(
                reportId,
                templateItemId,
                expectedReportVersion,
                altText,
                memory.ToArray(),
                userId.Value,
                GetProjectAccessScope(user),
                cancellationToken));
        })
        .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
        .DisableAntiforgery()
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("UploadMaterialIqcReportPhoto");

        quality.MapDelete("/reports/{reportId:guid}/photos/{photoId:guid}", async (
            Guid reportId,
            Guid photoId,
            int? expectedReportVersion,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.DeletePhotoAsync(
                    reportId,
                    photoId,
                    expectedReportVersion,
                    userId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("DeleteMaterialIqcReportPhoto");

        quality.MapPost("/reports/{reportId:guid}/finalize", async (
            Guid reportId,
            FinalizeIqcReportRequest request,
            IqcReportStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.FinalizeAsync(
                    reportId,
                    request,
                    userId.Value,
                    context.TraceIdentifier,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("FinalizeMaterialIqcReport");

        quality.MapPost("/scan-reports/{reportId:guid}/attachments", async (
            Guid reportId,
            [FromForm] int expectedReportVersion,
            [FromForm] IFormFile file,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }
            if (file.Length is < 1 or > 10 * 1024 * 1024)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["파일은 개별 10MB 이하 PDF, JPEG 또는 PNG여야 합니다."]
                });
            }
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return ToResult(await store.AddScanAttachmentAsync(
                reportId,
                expectedReportVersion,
                file.FileName,
                memory.ToArray(),
                userId.Value,
                GetProjectAccessScope(user),
                cancellationToken));
        })
        .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024))
        .DisableAntiforgery()
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("UploadMaterialIqcScanAttachment");

        quality.MapDelete("/scan-reports/{reportId:guid}/attachments/{attachmentId:guid}", async (
            Guid reportId,
            Guid attachmentId,
            int? expectedReportVersion,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.DeleteScanAttachmentAsync(
                    reportId,
                    attachmentId,
                    expectedReportVersion,
                    userId.Value,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("DeleteMaterialIqcScanAttachment");

        quality.MapGet("/scan-reports/{reportId:guid}/attachments/{attachmentId:guid}/content", async (
            Guid reportId,
            Guid attachmentId,
            IqcReportStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetScanAttachmentContentAsync(
                reportId,
                attachmentId,
                GetProjectAccessScope(user),
                cancellationToken);
            if (result.Status == MaterialsMutationStatus.NotFound || result.Value is null)
            {
                return Results.NotFound();
            }
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result.Value.Content, result.Value.NormalizedMime, result.Value.DisplayName);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("DownloadMaterialIqcScanAttachment");

        quality.MapPost("/scan-reports/{reportId:guid}/finalize", async (
            Guid reportId,
            FinalizeIqcReportRequest request,
            IqcReportStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : ToResult(await store.FinalizeScanAsync(
                    reportId,
                    request,
                    userId.Value,
                    context.TraceIdentifier,
                    GetProjectAccessScope(user),
                    cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("FinalizeMaterialIqcScanReport");

        quality.MapGet("/reports/{reportId:guid}/photos/{photoId:guid}/content", async (
            Guid reportId,
            Guid photoId,
            IqcReportStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetPhotoContentAsync(reportId, photoId, GetProjectAccessScope(user), cancellationToken);
            if (result.Status == MaterialsMutationStatus.NotFound || result.Value is null)
            {
                return Results.NotFound();
            }
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result.Value.Content, result.Value.NormalizedMime, result.Value.DisplayName);
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("DownloadMaterialIqcReportPhoto");

        quality.MapGet("/reports/{reportId:guid}/pdf", async (
            Guid reportId,
            IqcReportStore store,
            ClaimsPrincipal user,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await store.GetPdfAsync(reportId, GetProjectAccessScope(user), cancellationToken);
            if (result.Status == MaterialsMutationStatus.NotFound || result.Value is null)
            {
                return Results.NotFound();
            }
            context.Response.Headers.CacheControl = "private, no-store";
            return result.Value.Status switch
            {
                IqcPdfStatuses.Ready when result.Value.Content is not null
                    => Results.File(result.Value.Content, "application/pdf", "iqc-report.pdf"),
                IqcPdfStatuses.Pending
                    => Results.Json(new { status = IqcPdfStatuses.Pending }, statusCode: StatusCodes.Status202Accepted),
                _ => Results.Problem(title: "PDF를 생성하지 못했습니다. 재시도해 주세요.", statusCode: StatusCodes.Status409Conflict)
            };
        })
        .RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(QmsPermissions.ProjectRead)))
        .WithName("DownloadMaterialIqcReportPdf");

        quality.MapPost("/reports/{reportId:guid}/pdf/retry", async (
            Guid reportId,
            IqcReportStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
            ToResult(await store.RetryPdfAsync(reportId, GetProjectAccessScope(user), cancellationToken)))
        .RequireAuthorization(QmsPolicies.QualityInspect)
        .WithName("RetryMaterialIqcReportPdf");

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

    private static ProjectAccessScope GetProjectAccessScope(ClaimsPrincipal user)
    {
        return new ProjectAccessScope(
            user.HasClaim(QmsClaimTypes.Permission, QmsPermissions.ProjectReadAll),
            user.FindAll(QmsClaimTypes.Project).Select(claim => claim.Value).ToList());
    }

    private static string? NormalizeSupplyType(string? value)
    {
        if (string.Equals(value, ProcurementSupplyTypes.Purchased, StringComparison.OrdinalIgnoreCase))
        {
            return ProcurementSupplyTypes.Purchased;
        }
        if (string.Equals(value, ProcurementSupplyTypes.CustomerSupplied, StringComparison.OrdinalIgnoreCase))
        {
            return ProcurementSupplyTypes.CustomerSupplied;
        }
        return null;
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

    private static IResult ToResult(MaterialsMutationResult<IqcReportResponse> result)
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
