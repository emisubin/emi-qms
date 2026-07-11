using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public static class NotificationDeliveryLeasePolicy
{
    public const int DefaultHttpProviderTimeoutSeconds = 100;
    public const int ProviderTimeoutSafetyMarginSeconds = 30;

    public static TimeSpan GetValidatedLeaseDuration(NotificationOptions options)
    {
        var providerTimeoutSeconds = Math.Max(
            DefaultHttpProviderTimeoutSeconds,
            Math.Max(1, options.Mail.Smtp.TimeoutSeconds));
        var minimumLeaseSeconds = providerTimeoutSeconds + ProviderTimeoutSafetyMarginSeconds;
        if (options.Dispatch.ClaimLeaseSeconds <= minimumLeaseSeconds)
        {
            throw new InvalidOperationException(
                $"Notifications:Dispatch:ClaimLeaseSeconds must be greater than the provider timeout ceiling plus {ProviderTimeoutSafetyMarginSeconds} seconds.");
        }

        return TimeSpan.FromSeconds(options.Dispatch.ClaimLeaseSeconds);
    }
}

public sealed class NotificationWorkerIdentity
{
    public NotificationWorkerIdentity()
        : this(Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant())
    {
    }

    public NotificationWorkerIdentity(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || instanceId.Length > 64)
        {
            throw new ArgumentException("Worker instance id must be a non-empty opaque value of at most 64 characters.", nameof(instanceId));
        }

        InstanceId = instanceId;
    }

    public string InstanceId { get; }
}

public sealed class NotificationOptionsValidator : IValidateOptions<NotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        try
        {
            if (options.Mail.Smtp.TimeoutSeconds <= 0)
            {
                return ValidateOptionsResult.Fail("Notifications:Mail:Smtp:TimeoutSeconds must be greater than zero.");
            }

            _ = NotificationDeliveryLeasePolicy.GetValidatedLeaseDuration(options);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}
