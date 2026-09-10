using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Admin;

// Reuse the same snapshot and scope as the linked user administration page.
public sealed class ApprovalPendingUserCountService(
    IUserAdministrationStore localUsers,
    BusinessUnitAccessAdministrationStore integratedUsers)
{
    public async Task<int> GetCountAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (BusinessUnitAccessEndpointExtensions.TryGetOverallAdministrator(
                context, context.User, out var actorUserId))
        {
            var snapshot = await integratedUsers.GetSnapshotAsync(actorUserId, cancellationToken);
            return snapshot.Users.Count(user => user.ApprovalPending);
        }

        var localSnapshot = await localUsers.GetSnapshotAsync(cancellationToken);
        return localSnapshot.Users.Count(user => user.ApprovalPending);
    }
}
