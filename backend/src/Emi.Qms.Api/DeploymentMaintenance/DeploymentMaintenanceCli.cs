using System.Globalization;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.DeploymentMaintenance;

/// <summary>Internal deployment-job command. No HTTP administrator mutation surface is exposed.</summary>
public static class DeploymentMaintenanceCli
{
    public static readonly IReadOnlySet<string> Commands = new HashSet<string>(StringComparer.Ordinal)
    {
        "--maintenance-prepare", "--maintenance-activate", "--maintenance-delay",
        "--maintenance-fail", "--maintenance-complete"
    };

    public static async Task RunAsync(string command, IConfiguration configuration,
        DatabaseConnectionStringProvider provider, DatabaseHealthChecker databaseHealthChecker,
        ILogger logger, CancellationToken ct)
    {
        if (!Commands.Contains(command)) throw new ArgumentException("Unknown maintenance command.", nameof(command));
        var releaseId = RequiredGuid(configuration, "Maintenance:ReleaseId");
        var actor = RequiredGuid(configuration, "Maintenance:ActorUserId");
        var targets = provider.BusinessUnits.Businesses;
        if (targets.Count == 0) throw new InvalidOperationException("No business database is configured.");

        // The release script cannot query the internal readiness route through the public access gate.
        // Check the live runtime connections and exact migration ledgers from this job's image immediately
        // before any site is released. Azure revision health may reflect an earlier probe.
        if (command == "--maintenance-complete")
        {
            if (!string.Equals(configuration["Maintenance:Verified"], "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Maintenance completion requires verified release status.");
            var health = await databaseHealthChecker.CheckAsync(ct);
            if (!health.IsReady)
                throw new InvalidOperationException("Maintenance completion requires all business databases to be ready.");
        }

        // A partial multi-database transition fails closed: already activated sites remain blocked,
        // and the job exits non-zero for the operator to inspect before deployment continues.
        foreach (var target in targets.OrderBy(item => item.Code, StringComparer.Ordinal))
        {
            var store = DeploymentMaintenanceStore.ForTarget(provider, target);
            DeploymentMaintenanceCommandResult result;
            if (command == "--maintenance-prepare")
            {
                result = await store.PrepareAsync(new PrepareDeploymentMaintenance(
                    releaseId, actor,
                    Required(configuration, "Maintenance:Title"),
                    Required(configuration, "Maintenance:Body"),
                    RequiredUtc(configuration, "Maintenance:StartsAtUtc"),
                    RequiredUtc(configuration, "Maintenance:ExpectedEndsAtUtc")), ct);
            }
            else
            {
                var state = await store.ReadAsync(actor, ct);
                if (state.ReleaseId != releaseId)
                    throw new InvalidOperationException($"Maintenance release does not match configured database {target.Code}.");
                var action = command["--maintenance-".Length..];
                var revisedEnd = action == "delay"
                    ? RequiredUtc(configuration, "Maintenance:ExpectedEndsAtUtc") : (DateTimeOffset?)null;
                var verified = action == "complete"
                    && string.Equals(configuration["Maintenance:Verified"], "true", StringComparison.OrdinalIgnoreCase);
                result = await store.TransitionAsync(releaseId, state.Version, action,
                    revisedEnd, verified, actor, ct);
            }
            if (result.Status != 200)
                throw new InvalidOperationException($"Maintenance {command} failed for {target.Code}: {result.ErrorCode}.");
            logger.LogInformation("Maintenance command completed for {BusinessUnit}. State={State} Version={Version}.",
                target.Code, result.Value?.State, result.Value?.Version);
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"Missing {key}.") : configuration[key]!;

    private static Guid RequiredGuid(IConfiguration configuration, string key) =>
        Guid.TryParse(Required(configuration, key), out var value) && value != Guid.Empty
            ? value : throw new InvalidOperationException($"Invalid {key}.");

    private static DateTimeOffset RequiredUtc(IConfiguration configuration, string key) =>
        DateTimeOffset.TryParse(Required(configuration, key), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime() : throw new InvalidOperationException($"Invalid {key}.");
}
