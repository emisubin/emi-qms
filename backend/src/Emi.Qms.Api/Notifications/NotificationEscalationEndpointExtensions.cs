using Emi.Qms.Api.Authorization;

namespace Emi.Qms.Api.Notifications;

public static class NotificationEscalationEndpointExtensions
{
    public static IEndpointRouteBuilder MapNotificationEscalationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/work-item-escalations", async (
            string? status,
            string? level,
            WorkItemEscalationStore escalationStore,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await escalationStore.ListEscalationsAsync(status, level, cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListWorkItemEscalations");

        return app;
    }
}
