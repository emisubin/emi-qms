using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.BusinessUnits;

public static class BusinessUnitAccessEndpointExtensions
{
    public static IEndpointRouteBuilder MapBusinessUnitAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/user-access")
            .RequireAuthorization("AuthenticatedIdentity");

        group.MapGet("/users", async (
            HttpContext context,
            ClaimsPrincipal principal,
            BusinessUnitAccessAdministrationStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetOverallAdministrator(context, principal, out var actorUserId))
            {
                return Denied();
            }

            return Results.Ok(await store.GetSnapshotAsync(actorUserId, cancellationToken));
        })
        .WithName("GetIntegratedUserAccessUsers");

        group.MapPut("/users/{userId:guid}/access", async (
            Guid userId,
            BusinessUnitUserAccessUpdateRequest request,
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
                var result = await store.UpdateAccessAsync(userId, request, actorUserId, cancellationToken);
                return Results.Ok(new BusinessUnitUserAccessUpdateResponse(
                    result.Changed,
                    result.AccessVersion,
                    result.Snapshot));
            }
            catch (BusinessUnitAccessAdministrationException exception)
            {
                return Results.Json(
                    new BusinessUnitAccessAdministrationErrorResponse(
                        exception.ErrorCode,
                        ErrorMessage(exception.ErrorCode),
                        exception.OperationId),
                    statusCode: exception.StatusCode);
            }
        })
        .WithName("UpdateIntegratedUserAccess");

        app.MapPut(
            "/api/admin/business-unit-access/users/{userId:guid}/memberships",
            (Guid userId) => Results.Json(
                new BusinessUnitAccessAdministrationErrorResponse(
                    "integrated_user_access_required",
                    "사업부 소속은 관리자 메뉴의 사용자 관리에서 부서와 역할을 함께 저장해 주세요.",
                    null),
                statusCode: StatusCodes.Status410Gone))
            .RequireAuthorization("AuthenticatedIdentity")
            .WithName("RejectLegacyBusinessUnitMembershipUpdate");

        return app;
    }

    internal static bool TryGetOverallAdministrator(
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
            "통합 사용자 접근 관리는 지정된 총괄 관리자만 사용할 수 있습니다.",
            null),
        statusCode: StatusCodes.Status403Forbidden);

    private static string ErrorMessage(string errorCode) => errorCode switch
    {
        "directory_identity_not_found" => "승인하거나 수정할 사용자를 찾을 수 없습니다.",
        "directory_identity_read_only" => "개발 사용자는 이 화면에서 수정할 수 없습니다.",
        "business_unit_unknown" => "선택할 수 없는 사업부가 포함되어 있습니다.",
        "active_profile_incomplete" => "활성 사업부에는 부서와 역할을 한 개 이상 지정해 주세요.",
        "department_not_found" => "선택한 사업부에 없는 부서입니다.",
        "department_default_role_missing" => "선택한 부서의 기본 역할을 확인할 수 없습니다.",
        "role_not_found" => "선택한 사업부에 없는 역할이 포함되어 있습니다.",
        "business_unit_users_manage_required" => "해당 사업부의 사용자 관리 권한이 필요합니다.",
        "ordinary_user_multiple_memberships_forbidden" => "일반 사용자는 한 사업부에만 소속될 수 있습니다.",
        "last_overall_administrator" => "마지막 총괄 관리자는 해제할 수 없습니다.",
        "overall_administrator_department_missing" => "총괄 관리자를 위한 관리 부서를 준비할 수 없습니다.",
        "user_access_version_conflict" or "user_access_operation_in_progress" =>
            "다른 변경이 먼저 반영되었습니다. 목록을 새로 불러온 뒤 다시 저장해 주세요.",
        "user_access_idempotency_mismatch" => "같은 작업 번호로 다른 내용을 저장할 수 없습니다.",
        "cross_database_identity_mismatch" => "사업부 사용자 식별 정보가 Directory와 일치하지 않습니다.",
        "last_system_administrator" => "마지막 System Administrator는 비활성화하거나 역할을 제거할 수 없습니다.",
        "user_access_retry_required" => "사업부 프로필 저장이 완료되지 않았습니다. 같은 작업을 다시 시도해 주세요.",
        _ => "사용자 접근 정보를 저장할 수 없습니다."
    };
}

public sealed record BusinessUnitUserAccessUpdateResponse(
    bool Changed,
    long AccessVersion,
    BusinessUnitAccessAdministrationSnapshot Snapshot);

public sealed record BusinessUnitAccessAdministrationErrorResponse(
    string ErrorCode,
    string Message,
    Guid? OperationId);
