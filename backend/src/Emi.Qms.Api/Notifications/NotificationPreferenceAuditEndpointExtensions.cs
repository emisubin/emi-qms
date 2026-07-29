using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Notifications;

public static class NotificationPreferenceAuditEndpointExtensions
{
    private static readonly IReadOnlySet<string> Actions = new HashSet<string>(StringComparer.Ordinal)
    {
        "Save", "Reset", "AdminSave", "AdminReset"
    };

    private static readonly IReadOnlySet<string> DeliveryTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        NotificationDeliveryTypes.WorkItemCreated,
        NotificationDeliveryTypes.DueSoonL0,
        NotificationDeliveryTypes.DailyDigest
    };

    public static IEndpointRouteBuilder MapNotificationPreferenceAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/notification-preference-audit", async (
            DateOnly? from,
            DateOnly? to,
            string? action,
            string? deliveryType,
            string? search,
            int? page,
            int? pageSize,
            NotificationPreferenceAuditStore store,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), koreaTimeZone).DateTime);
            var fromDate = from ?? today.AddDays(-29);
            var toDate = to ?? today;
            var resolvedPage = page ?? 1;
            var resolvedPageSize = pageSize ?? 50;
            var normalizedAction = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
            var normalizedDeliveryType = string.IsNullOrWhiteSpace(deliveryType) ? null : deliveryType.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var errors = new Dictionary<string, string[]>();
            if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 365)
            {
                errors["dateRange"] = ["조회 기간은 시작일이 종료일보다 늦지 않은 최대 366일이어야 합니다."];
            }
            if (resolvedPage < 1)
            {
                errors["page"] = ["페이지 번호는 1 이상이어야 합니다."];
            }
            if (resolvedPageSize is not 20 and not 50 and not 100)
            {
                errors["pageSize"] = ["페이지 크기는 20, 50, 100 중 하나여야 합니다."];
            }
            if (normalizedAction is not null && !Actions.Contains(normalizedAction))
            {
                errors["action"] = ["지원하지 않는 변경 행동입니다."];
            }
            if (normalizedDeliveryType is not null && !DeliveryTypes.Contains(normalizedDeliveryType))
            {
                errors["deliveryType"] = ["지원하지 않는 알림 종류입니다."];
            }
            if (normalizedSearch is { Length: > 100 })
            {
                errors["search"] = ["사용자 검색어는 100자 이하로 입력해 주세요."];
            }
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "감사 조회 조건을 확인해 주세요.");
            }

            var fromUtc = ToUtc(fromDate, koreaTimeZone);
            var toUtc = ToUtc(toDate.AddDays(1), koreaTimeZone);
            return Results.Ok(await store.ListAsync(
                fromDate, toDate, fromUtc, toUtc, normalizedAction, normalizedDeliveryType,
                normalizedSearch, resolvedPage, resolvedPageSize, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListNotificationPreferenceAuditEvents");

        return app;
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
