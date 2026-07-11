using Emi.Qms.Api.Admin;

namespace Emi.Qms.Api;

public static class MutationWorkerActivationPolicy
{
    public static MutationWorkerActivation Evaluate(IConfiguration configuration, bool reviewSafeEnabled)
    {
        if (reviewSafeEnabled)
        {
            return new MutationWorkerActivation(false, false, false);
        }

        return new MutationWorkerActivation(
            configuration.GetValue<bool>("Notifications:Dispatch:Enabled"),
            configuration.GetValue<bool>("Notifications:Escalation:Enabled"),
            AdminDeletionPurgePolicy.ResolveEnabled(configuration));
    }
}

public sealed record MutationWorkerActivation(
    bool NotificationDeliveryWorkerEnabled,
    bool NotificationEscalationWorkerEnabled,
    bool AdminDeletionPurgeWorkerEnabled)
{
    public bool MutationWorkersEnabled =>
        NotificationDeliveryWorkerEnabled
        || NotificationEscalationWorkerEnabled
        || AdminDeletionPurgeWorkerEnabled;
}
