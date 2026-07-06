using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Calendar;

public static class BusinessCalendarEndpointExtensions
{
    public static IEndpointRouteBuilder MapBusinessCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/calendar/business-days", async (
            DateOnly? from,
            DateOnly? to,
            string? countryCode,
            BusinessCalendarStore store,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.ProjectRead))
            {
                return Results.Forbid();
            }

            if (from is null || to is null)
            {
                return Results.BadRequest(new { message = "조회 시작일과 종료일을 입력해 주세요." });
            }

            if (to < from)
            {
                return Results.BadRequest(new { message = "종료일은 시작일보다 빠를 수 없습니다." });
            }

            if (to.Value.DayNumber - from.Value.DayNumber > 370)
            {
                return Results.BadRequest(new { message = "영업일 캘린더 조회 범위는 최대 370일입니다." });
            }

            return Results.Ok(await store.GetCalendarAsync(countryCode, from.Value, to.Value, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("GetBusinessCalendar");

        return app;
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode)
    {
        return user.HasClaim(QmsClaimTypes.Permission, permissionCode);
    }
}
