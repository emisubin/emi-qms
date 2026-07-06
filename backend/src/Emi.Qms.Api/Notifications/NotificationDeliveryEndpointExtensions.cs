using Emi.Qms.Api.Authorization;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;

namespace Emi.Qms.Api.Notifications;

public static class NotificationDeliveryEndpointExtensions
{
    public static IEndpointRouteBuilder MapNotificationDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/notification-deliveries", async (
            NotificationDeliveryStore deliveryStore,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await deliveryStore.ListDeliveriesAsync(cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("ListNotificationDeliveries");

        app.MapPost("/api/admin/notification-deliveries/test-mail", async (
            NotificationTestMailRequest request,
            NotificationDeliveryStore deliveryStore,
            IEnumerable<INotificationChannelHandler> channelHandlers,
            IOptionsMonitor<NotificationOptions> options,
            CancellationToken cancellationToken) =>
        {
            var recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
                ? options.CurrentValue.Mail.TestRecipientEmail
                : request.RecipientEmail;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return Results.BadRequest(new { message = "테스트 수신자 이메일이 설정되지 않았습니다." });
            }

            if (!IsValidEmail(recipientEmail))
            {
                return Results.BadRequest(new { message = "수신자 이메일 형식이 올바르지 않습니다." });
            }

            var mailOptions = options.CurrentValue.Mail;
            var sender = !string.IsNullOrWhiteSpace(mailOptions.SenderUserId)
                ? mailOptions.SenderUserId
                : mailOptions.SenderAddress;
            var senderSource = !string.IsNullOrWhiteSpace(mailOptions.SenderUserId)
                ? "SenderUserId"
                : !string.IsNullOrWhiteSpace(mailOptions.SenderAddress)
                    ? "SenderAddress"
                    : "missing";
            var recipientSource = string.IsNullOrWhiteSpace(request.RecipientEmail)
                ? "TestRecipientEmail"
                : "Request";
            var correlationId = CreateCorrelationId();
            var subject = BuildSubject(request.Subject, request.SubjectSuffix, correlationId);
            var saveToSentItems = request.SaveToSentItems ?? mailOptions.SaveTestMailToSentItems;
            var mailHandler = channelHandlers.Single(handler => handler.Channel == NotificationDeliveryChannels.Mail);
            var deliveryId = await deliveryStore.CreateManualTestMailDeliveryAsync(cancellationToken);
            var result = await mailHandler.SendAsync(
                new NotificationDeliveryMessage(
                    deliveryId,
                    NotificationDeliveryChannels.Mail,
                    NotificationDeliveryTypes.ManualTest,
                    subject,
                    BuildTestMailBody(request.Message, correlationId),
                    null,
                    "테스트 수신자",
                    recipientEmail.Trim(),
                    saveToSentItems,
                    correlationId,
                    mailOptions.SenderUserId,
                    mailOptions.SenderAddress),
                cancellationToken);
            await deliveryStore.MarkDeliveryResultAsync(deliveryId, result, retryCount: 1, cancellationToken);

            return Results.Ok(new NotificationTestMailResponse(
                deliveryId,
                result.Status,
                result.ErrorCode,
                result.ErrorMessage,
                string.IsNullOrWhiteSpace(mailOptions.Provider) ? "DryRun" : mailOptions.Provider,
                correlationId,
                senderSource,
                MaskAddress(sender),
                recipientSource,
                MaskAddress(recipientEmail),
                1,
                !string.IsNullOrWhiteSpace(sender) && string.Equals(sender, recipientEmail, StringComparison.OrdinalIgnoreCase),
                saveToSentItems));
        })
        .RequireAuthorization(QmsPolicies.AdminUsersRead)
        .WithName("SendNotificationTestMail");

        return app;
    }

    private static string BuildSubject(string? subject, string? suffix, string correlationId)
    {
        var baseSubject = string.IsNullOrWhiteSpace(subject)
            ? "TASK-NOTIFY-001 Graph Mail 테스트"
            : subject.Trim();
        var suffixText = string.IsNullOrWhiteSpace(suffix)
            ? ""
            : $" {suffix.Trim()}";
        return $"{baseSubject}{suffixText} [{correlationId}]";
    }

    private static string BuildTestMailBody(string? message, string correlationId)
    {
        var body = string.IsNullOrWhiteSpace(message)
            ? "EMI 프로젝트 통합관리시스템 UAT 테스트 메일입니다. 실제 업무 알림이 아닙니다."
            : message.Trim();

        return $"""
            EMI 프로젝트 통합관리시스템

            {body}

            환경: UAT
            채널: Mail
            Correlation ID: {correlationId}
            발송 시각: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }

    private static string CreateCorrelationId()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    }

    private static string MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1)
        {
            return value.Length <= 4 ? "***" : value[..4] + "***";
        }

        return value[..1] + "***" + value[at..];
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
