using System.Security.Claims;
using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Audit;

public static class AuditEndpointExtensions
{
    private static readonly IReadOnlySet<string> EventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        AuditEventTypes.Login,
        AuditEventTypes.Logout,
        AuditEventTypes.MutationSucceeded,
        AuditEventTypes.MutationFailed,
        AuditEventTypes.AuthorizationDenied
    };

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit/sessions/interactive-login", async (
            RecordInteractiveLoginRequest request,
            HttpContext context,
            ClaimsPrincipal user,
            AuditStore store,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId))
            {
                return Results.Unauthorized();
            }

            var appAccessOutcome = user.HasClaim(QmsClaimTypes.Inactive, bool.TrueString)
                ? "Inactive"
                : user.HasClaim(QmsClaimTypes.ApprovalPending, bool.TrueString)
                    ? "ApprovalPending"
                    : "Allowed";
            var userAgent = context.Request.Headers.UserAgent.ToString();

            try
            {
                var response = await store.AppendInteractiveLoginAsync(
                    actorUserId,
                    request.ClientInteractionId,
                    appAccessOutcome,
                    context.Connection.RemoteIpAddress,
                    ResolveBrowserFamily(userAgent),
                    ResolveOsFamily(userAgent),
                    cancellationToken);
                return Results.Ok(response);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                loggerFactory.CreateLogger("Emi.Qms.Api.Audit.Login")
                    .LogError(exception, "Best-effort interactive login audit write failed.");
                return Results.Problem(
                    title: "로그인 기록을 남기지 못했습니다.",
                    detail: "로그인은 계속 사용할 수 있습니다.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("RecordInteractiveLogin");

        app.MapPost("/api/audit/sessions/logout", async (
            RecordAuditLogoutRequest request,
            ClaimsPrincipal user,
            AuditStore store,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(user, out var actorUserId))
            {
                return Results.Unauthorized();
            }

            try
            {
                return await store.AppendLogoutAsync(
                    actorUserId,
                    request.LoginCorrelationId,
                    request.IdempotencyReceipt,
                    cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                loggerFactory.CreateLogger("Emi.Qms.Api.Audit.Logout")
                    .LogError(exception, "Best-effort explicit logout audit write failed.");
                return Results.Problem(
                    title: "로그아웃 기록을 남기지 못했습니다.",
                    detail: "로그아웃은 계속 진행할 수 있습니다.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .RequireAuthorization("AuthenticatedIdentity")
        .WithName("RecordExplicitLogout");

        app.MapGet("/api/admin/audit-events", async (
            DateOnly? from,
            DateOnly? to,
            Guid? actorUserId,
            string? domain,
            string? action,
            string? eventType,
            string? failureReason,
            string? search,
            int? page,
            int? pageSize,
            AuditStore store,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), koreaTimeZone).DateTime);
            var fromDate = from ?? today.AddDays(-29);
            var toDate = to ?? today;
            var resolvedPage = page ?? 1;
            var resolvedPageSize = pageSize ?? 50;
            var normalizedDomain = Normalize(domain);
            var normalizedAction = Normalize(action);
            var normalizedEventType = Normalize(eventType);
            var normalizedFailureReason = Normalize(failureReason);
            var normalizedSearch = Normalize(search);

            var errors = ValidateQuery(
                fromDate, toDate, normalizedDomain, normalizedAction, normalizedEventType,
                normalizedFailureReason, normalizedSearch, resolvedPage, resolvedPageSize);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(
                    errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "감사 조회 조건을 확인해 주세요.");
            }

            var query = new AuditQuery(
                ToUtc(fromDate, koreaTimeZone),
                ToUtc(toDate.AddDays(1), koreaTimeZone),
                actorUserId,
                normalizedDomain,
                normalizedAction,
                normalizedEventType,
                normalizedFailureReason,
                normalizedSearch,
                resolvedPage,
                resolvedPageSize);
            return Results.Ok(await store.ListAsync(fromDate, toDate, query, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("ListGlobalAuditEvents");

        app.MapGet("/api/admin/audit-events/{eventId:guid}", async (
            Guid eventId,
            string? source,
            AuditStore store,
            CancellationToken cancellationToken) =>
        {
            var normalizedSource = Normalize(source) ?? "Global";
            if (normalizedSource is not "Global" and not "Authorization")
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["source"] = ["지원하지 않는 감사 원본입니다."] },
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "감사 상세 요청을 확인해 주세요.");
            }

            var detail = await store.GetDetailAsync(eventId, normalizedSource, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
        .RequireAuthorization(QmsPolicies.AuditReadAll)
        .WithName("GetGlobalAuditEventDetail");

        return app;
    }

    private static Dictionary<string, string[]> ValidateQuery(
        DateOnly from,
        DateOnly to,
        string? domain,
        string? action,
        string? eventType,
        string? failureReason,
        string? search,
        int page,
        int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (to < from || to.DayNumber - from.DayNumber > 365)
        {
            errors["dateRange"] = ["조회 기간은 시작일이 종료일보다 늦지 않은 최대 366일이어야 합니다."];
        }
        if (page < 1)
        {
            errors["page"] = ["페이지 번호는 1 이상이어야 합니다."];
        }
        if (pageSize is not 20 and not 50 and not 100)
        {
            errors["pageSize"] = ["페이지 크기는 20, 50, 100 중 하나여야 합니다."];
        }
        if (domain is { Length: > 80 })
        {
            errors["domain"] = ["업무영역은 80자 이하여야 합니다."];
        }
        if (action is { Length: > 120 })
        {
            errors["action"] = ["행동은 120자 이하여야 합니다."];
        }
        if (eventType is not null && !EventTypes.Contains(eventType))
        {
            errors["eventType"] = ["지원하지 않는 사건 종류입니다."];
        }
        if (failureReason is not null && !AuditFailureReasons.All.Contains(failureReason))
        {
            errors["failureReason"] = ["지원하지 않는 실패 종류입니다."];
        }
        if (search is { Length: > 100 })
        {
            errors["search"] = ["검색어는 100자 이하여야 합니다."];
        }
        return errors;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out userId);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    internal static string ResolveBrowserFamily(string userAgent)
    {
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("CriOS/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("FxiOS/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return "Other";
    }

    internal static string ResolveOsFamily(string userAgent)
    {
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "macOS";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Other";
    }
}
