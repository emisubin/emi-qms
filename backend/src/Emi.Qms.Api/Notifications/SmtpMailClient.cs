using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Emi.Qms.Api.Notifications;

public interface ISmtpMailClient
{
    Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken);
}

public interface ISmtpMailTransport
{
    Task<string> SendAsync(SmtpMailSendRequest request, CancellationToken cancellationToken);
}

public sealed record SmtpMailSendRequest(
    string Host,
    int Port,
    string Security,
    string? Username,
    string? Password,
    string SenderAddress,
    string SenderDisplayName,
    string RecipientEmail,
    string Subject,
    string Body,
    string? CorrelationId,
    int TimeoutSeconds);

public sealed class SmtpMailClient(
    IOptionsMonitor<NotificationOptions> options,
    ISmtpMailTransport transport)
    : ISmtpMailClient
{
    public async Task<NotificationChannelResult> SendAsync(MailDeliveryPayload payload, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue.Mail;
        var smtp = mail.Smtp;
        var validationError = Validate(mail, smtp, payload);
        if (validationError is not null)
        {
            return validationError;
        }

        var senderAddress = !string.IsNullOrWhiteSpace(payload.SenderAddress)
            ? payload.SenderAddress.Trim()
            : mail.SenderAddress!.Trim();

        var request = new SmtpMailSendRequest(
            smtp.Host!.Trim(),
            smtp.Port,
            smtp.Security,
            smtp.Username,
            smtp.Password,
            senderAddress,
            mail.SenderDisplayName,
            payload.RecipientEmail.Trim(),
            payload.Subject,
            payload.Body,
            payload.CorrelationId,
            Math.Clamp(smtp.TimeoutSeconds, 5, 120));

        try
        {
            var providerMessageId = await transport.SendAsync(request, cancellationToken);
            return NotificationChannelResult.Sent(providerMessageId);
        }
        catch (SmtpMailTransportException exception)
        {
            return NotificationChannelResult.Failed(exception.ErrorCode, exception.SafeMessage);
        }
    }

    private static NotificationChannelResult? Validate(
        NotificationMailOptions mail,
        NotificationSmtpOptions smtp,
        MailDeliveryPayload payload)
    {
        var senderAddress = !string.IsNullOrWhiteSpace(payload.SenderAddress)
            ? payload.SenderAddress
            : mail.SenderAddress;

        if (string.IsNullOrWhiteSpace(smtp.Host)
            || smtp.Port <= 0
            || string.IsNullOrWhiteSpace(smtp.Username)
            || string.IsNullOrWhiteSpace(smtp.Password)
            || string.IsNullOrWhiteSpace(senderAddress)
            || string.IsNullOrWhiteSpace(payload.RecipientEmail))
        {
            return NotificationChannelResult.Failed(
                "SmtpConfigMissing",
                "SMTP 메일 발송 설정이 누락되었습니다.");
        }

        return null;
    }
}

public sealed class MailKitSmtpMailTransport : ISmtpMailTransport
{
    public async Task<string> SendAsync(SmtpMailSendRequest request, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(request.SenderDisplayName, request.SenderAddress));
        message.To.Add(MailboxAddress.Parse(request.RecipientEmail));
        message.Subject = request.Subject;
        message.Body = new TextPart("plain")
        {
            Text = request.Body
        };
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            message.Headers.Add("x-emi-notification-test-id", request.CorrelationId);
        }

        using var client = new SmtpClient
        {
            Timeout = request.TimeoutSeconds * 1000
        };

        try
        {
            await client.ConnectAsync(
                request.Host,
                request.Port,
                ToSecureSocketOptions(request.Security),
                cancellationToken);
            await client.AuthenticateAsync(request.Username!, request.Password!, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return string.IsNullOrWhiteSpace(request.CorrelationId)
                ? "smtp-sent"
                : $"smtp-sent;client-request-id={request.CorrelationId}";
        }
        catch (MailKit.Security.AuthenticationException exception)
        {
            throw new SmtpMailTransportException(
                "SmtpAuthenticationFailed",
                "SMTP 인증에 실패했습니다.",
                exception);
        }
        catch (SmtpCommandException exception)
        {
            throw new SmtpMailTransportException(
                "SmtpSendFailed",
                $"SMTP 발송 요청이 실패했습니다. StatusCode {(int)exception.StatusCode}",
                exception);
        }
        catch (SmtpProtocolException exception)
        {
            throw new SmtpMailTransportException(
                "SmtpConnectionFailed",
                "SMTP 서버와 통신하는 중 오류가 발생했습니다.",
                exception);
        }
        catch (SocketException exception)
        {
            throw new SmtpMailTransportException(
                "SmtpConnectionFailed",
                "SMTP 서버에 연결하지 못했습니다.",
                exception);
        }
        catch (IOException exception)
        {
            throw new SmtpMailTransportException(
                "SmtpConnectionFailed",
                "SMTP 연결 중 오류가 발생했습니다.",
                exception);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private static SecureSocketOptions ToSecureSocketOptions(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.StartTls
        };
    }
}

public sealed class SmtpMailTransportException(
    string errorCode,
    string safeMessage,
    Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public string SafeMessage { get; } = safeMessage;
}
