using System.Data;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.G2;

public static class G2OperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapG2OperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/g2").RequireAuthorization(QmsPolicies.G2Read);
        group.MapGet("/home", async (int? year, int? month, G2OperationsStore store, CancellationToken token) => await Safe(() => store.GetHomeAsync(year, month, token))).WithName("GetG2Home");
        group.MapGet("/days", async (DateOnly from, DateOnly to, G2OperationsStore store, CancellationToken token) => await Safe(() => store.GetRangeAsync(from, to, token))).WithName("GetG2Days");

        group.MapPut("/operations/{date}", async (DateOnly date, SaveG2OperationsRequest request, G2OperationsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var changes = Changes(request);
            if (changes.Count == 0) return Validation("request", "저장할 생산 또는 납품 항목을 하나 이상 입력해 주세요.");
            if (changes.Any(c => c.MetricCode is G2MetricCodes.MorningProduction or G2MetricCodes.AfternoonProduction && !Has(user, QmsPermissions.G2ProductionUpdate))
                || changes.Any(c => c.MetricCode == G2MetricCodes.Delivery && !Has(user, QmsPermissions.G2DeliveryUpdate))) return Results.Forbid();
            var actor = UserId(user);
            return actor is null ? Results.Unauthorized() : await Safe(() => store.SaveMetricsAsync(date, changes, actor.Value, token));
        }).WithName("SaveG2Operations");

        group.MapPut("/attendance/{date}", async (DateOnly date, SaveG2AttendanceRequest request, G2OperationsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var changes = Changes(request);
            if (changes.Count == 0) return Validation("request", "저장할 출근 인원 항목을 하나 이상 입력해 주세요.");
            if (!Has(user, QmsPermissions.G2AttendanceUpdate)) return Results.Forbid();
            var actor = UserId(user);
            return actor is null ? Results.Unauthorized() : await Safe(() => store.SaveMetricsAsync(date, changes, actor.Value, token));
        }).WithName("SaveG2Attendance");

        group.MapPut("/inventory-counts/{date}", async (DateOnly date, SaveG2InventoryCountRequest request, G2OperationsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user);
            return actor is null ? Results.Unauthorized() : await Safe(() => store.SaveInventoryCountAsync(date, request, actor.Value, token));
        }).RequireAuthorization(QmsPolicies.G2InventoryManage).WithName("SaveG2InventoryCount");

        group.MapDelete("/inventory-counts/{date}", async (DateOnly date, int? expectedVersion, G2OperationsStore store, CancellationToken token) => await Safe(() => store.DeleteInventoryCountAsync(date, expectedVersion, token)))
            .RequireAuthorization(QmsPolicies.G2InventoryManage).WithName("DeleteG2InventoryCount");

        group.MapPut("/targets/{targetType}/{effectiveDate}", async (string targetType, DateOnly effectiveDate, SaveG2TargetRequest request, G2OperationsStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actor = UserId(user);
            return actor is null ? Results.Unauthorized() : await Safe(() => store.SaveTargetAsync(targetType, effectiveDate, request, actor.Value, token));
        }).RequireAuthorization(QmsPolicies.G2TargetManage).WithName("SaveG2Target");
        return app;
    }

    private static IReadOnlyList<G2MetricChange> Changes(SaveG2OperationsRequest request)
    {
        var result = new List<G2MetricChange>(); Add(result, G2MetricCodes.MorningProduction, request.MorningProduction); Add(result, G2MetricCodes.AfternoonProduction, request.AfternoonProduction); Add(result, G2MetricCodes.Delivery, request.Delivery); return result;
    }
    private static IReadOnlyList<G2MetricChange> Changes(SaveG2AttendanceRequest request)
    {
        var result = new List<G2MetricChange>(); Add(result, G2MetricCodes.MorningEmiAttendance, request.MorningEmiAttendance); Add(result, G2MetricCodes.MorningContractorAttendance, request.MorningContractorAttendance); Add(result, G2MetricCodes.AfternoonEmiAttendance, request.AfternoonEmiAttendance); Add(result, G2MetricCodes.AfternoonContractorAttendance, request.AfternoonContractorAttendance); return result;
    }
    private static void Add(List<G2MetricChange> values, string code, G2MetricChangeRequest? request) { if (request is not null) values.Add(new(code, request.Quantity, request.ExpectedVersion)); }
    private static bool Has(ClaimsPrincipal user, string permission) => user.HasClaim(QmsClaimTypes.Permission, permission);
    private static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;
    private static async Task<IResult> Safe<T>(Func<Task<T>> action) { try { return Results.Ok(await action()); } catch (DBConcurrencyException e) { return Results.Problem(title: e.Message, statusCode: 409); } catch (ArgumentException e) { return Validation(string.IsNullOrWhiteSpace(e.ParamName) ? "request" : e.ParamName, e.Message); } }
    private static async Task<IResult> Safe(Func<Task> action) { try { await action(); return Results.Ok(new { saved = true }); } catch (DBConcurrencyException e) { return Results.Problem(title: e.Message, statusCode: 409); } catch (ArgumentException e) { return Validation(string.IsNullOrWhiteSpace(e.ParamName) ? "request" : e.ParamName, e.Message); } }
    private static IResult Validation(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
