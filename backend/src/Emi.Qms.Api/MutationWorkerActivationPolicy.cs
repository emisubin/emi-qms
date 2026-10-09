using Emi.Qms.Api.Admin;

namespace Emi.Qms.Api;

public static class MutationWorkerActivationPolicy
{
    public static MutationWorkerActivation Evaluate(IConfiguration configuration, bool reviewSafeEnabled)
    {
        if (reviewSafeEnabled)
        {
            return new MutationWorkerActivation(false, false, false, false);
        }

        return new MutationWorkerActivation(
            configuration.GetValue<bool>("Notifications:Dispatch:Enabled"),
            configuration.GetValue<bool>("Notifications:Escalation:Enabled"),
            AdminDeletionPurgePolicy.ResolveEnabled(configuration),
            configuration.GetValue<bool?>("G2Materials:Retention:Enabled") ?? true);
    }
}

public sealed record MutationWorkerActivation(
    bool NotificationDeliveryWorkerEnabled,
    bool NotificationEscalationWorkerEnabled,
    bool AdminDeletionPurgeWorkerEnabled,
    bool G2MaterialRetentionWorkerEnabled)
{
    public bool MutationWorkersEnabled =>
        NotificationDeliveryWorkerEnabled
        || NotificationEscalationWorkerEnabled
        || AdminDeletionPurgeWorkerEnabled
        || G2MaterialRetentionWorkerEnabled;
}
