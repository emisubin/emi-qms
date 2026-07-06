using Microsoft.Extensions.Options;

namespace Emi.Qms.Api.Notifications;

public interface IGraphMailClient
{
    Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken);
}

public sealed class ConfiguredMailClient(
    IOptionsMonitor<NotificationOptions> options,
    ISmtpMailClient smtpMailClient,
    IGraphMailClient graphMailClient)
    : IMailClient
{
    public Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
    {
        var provider = options.CurrentValue.Mail.Provider;
        if (string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, "DryRun", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(NotificationChannelResult.DryRunSent());
        }

        return provider.Trim().ToLowerInvariant() switch
        {
            "smtp" => smtpMailClient.SendAsync(payload, cancellationToken),
            "graph" => graphMailClient.SendAsync(payload, cancellationToken),
            _ => Task.FromResult(NotificationChannelResult.Disabled(
                "MailProviderUnsupported",
                "지원하지 않는 메일 provider입니다."))
        };
    }
}
