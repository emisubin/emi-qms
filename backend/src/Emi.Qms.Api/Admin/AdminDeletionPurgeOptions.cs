using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Admin;

public sealed class AdminDeletionPurgeOptions
{
    public const string SectionName = "AdminDeletionPurge";

    public bool Enabled { get; init; } = true;
}

public static class AdminDeletionPurgePolicy
{
    public static bool ResolveEnabled(IConfiguration configuration)
    {
        var configured = configuration[$"{AdminDeletionPurgeOptions.SectionName}:Enabled"];
        if (configured is null)
        {
            return true;
        }

        if (bool.TryParse(configured, out var enabled))
        {
            return enabled;
        }

        throw new InvalidOperationException(
            $"{AdminDeletionPurgeOptions.SectionName}:Enabled must be either true or false.");
    }
}

public sealed class AdminDeletionPurgeOptionsValidator : IValidateOptions<AdminDeletionPurgeOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminDeletionPurgeOptions options)
    {
        return ValidateOptionsResult.Success;
    }
}

public interface IAdminDeletionPurgeService
{
    Task<AdminScheduledDeletionPurgeResult> PurgeDueAsync(CancellationToken cancellationToken);
}
