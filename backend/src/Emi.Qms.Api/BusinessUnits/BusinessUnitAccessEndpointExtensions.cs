using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.BusinessUnits;

public static class BusinessUnitAccessEndpointExtensions
{
    public static IEndpointRouteBuilder MapBusinessUnitAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/business-unit-access")
            .RequireAuthorization("AuthenticatedIdentity");

        group.MapGet("/users", async (
            HttpContext context,
            ClaimsPrincipal principal,
            BusinessUnitAccessAdministrationStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetOverallAdministrator(context, principal, out _))
            {
                return Denied();
            }

            return Results.Ok(await store.GetSnapshotAsync(cancellationToken));
        })
        .WithName("GetBusinessUnitAccessUsers");

        group.MapPut("/users/{userId:guid}/memberships", async (
            Guid userId,
            UpdateBusinessUnitMembershipsRequest request,
            HttpContext context,
            ClaimsPrincipal principal,
            BusinessUnitAccessAdministrationStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetOverallAdministrator(context, principal, out var actorUserId))
            {
                return Denied();
            }

            try
            {
                var result = await store.SetMembershipsAsync(
                    userId,
                    request.BusinessUnitCodes ?? [],
                    actorUserId,
                    cancellationToken);
                return Results.Ok(new BusinessUnitMembershipUpdateResponse(
                    result.Changed,
                    result.Snapshot));
            }
            catch (BusinessUnitAccessAdministrationException exception)
            {
                return Results.Json(
                    new BusinessUnitAccessAdministrationErrorResponse(
                        exception.ErrorCode,
                        ErrorMessage(exception.ErrorCode)),
                    statusCode: exception.StatusCode);
            }
        })
        .WithName("UpdateBusinessUnitAccessMemberships");

        return app;
    }

    private static bool TryGetOverallAdministrator(
        HttpContext context,
        ClaimsPrincipal principal,
        out Guid actorUserId)
    {
        actorUserId = Guid.Empty;
        var requestContext = BusinessUnitRequestContextFeature.Get(context);
        if (requestContext?.IsOverallAdministrator != true
            || requestContext.DirectoryUserId is not Guid directoryUserId
            || !principal.HasClaim(QmsClaimTypes.IsOverallAdministrator, bool.TrueString)
            || !Guid.TryParse(principal.FindFirst(QmsClaimTypes.UserId)?.Value, out var claimedUserId)
            || claimedUserId != directoryUserId)
        {
            return false;
        }

        actorUserId = directoryUserId;
        return true;
    }

    private static IResult Denied() => Results.Json(
        new BusinessUnitAccessAdministrationErrorResponse(
            "overall_administrator_required",
            "사업부 소속 관리는 지정된 총괄 관리자만 사용할 수 있습니다."),
        statusCode: StatusCodes.Status403Forbidden);

    private static string ErrorMessage(string errorCode) => errorCode switch
    {
        "directory_identity_not_found" => "소속을 변경할 사용자를 찾을 수 없습니다.",
        "business_unit_unknown" => "선택할 수 없는 사업부가 포함되어 있습니다.",
        _ => "사업부 소속을 변경할 수 없습니다."
    };
}

public sealed record UpdateBusinessUnitMembershipsRequest(
    IReadOnlyList<string>? BusinessUnitCodes);

public sealed record BusinessUnitMembershipUpdateResponse(
    bool Changed,
    BusinessUnitAccessAdministrationSnapshot Snapshot);

public sealed record BusinessUnitAccessAdministrationErrorResponse(
    string ErrorCode,
    string Message);
