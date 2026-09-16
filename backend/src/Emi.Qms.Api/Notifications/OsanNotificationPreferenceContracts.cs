namespace Emi.Qms.Api.Notifications;

public sealed record OsanNotificationPreferenceItem(
    string Kind,
    string Label,
    bool MailEnabled,
    bool PushEnabled);

public sealed record OsanStepCompletedPreferenceItem(
    int Sequence,
    string Label,
    bool MailEnabled,
    bool PushEnabled);

public sealed record OsanNotificationPreferenceResponse(
    long Version,
    IReadOnlyList<OsanNotificationPreferenceItem> Items,
    IReadOnlyList<OsanStepCompletedPreferenceItem> StepCompletedStages);

public sealed record UpdateOsanNotificationPreferencesRequest(
    long ExpectedVersion,
    IReadOnlyList<OsanNotificationPreferenceItem> Items,
    IReadOnlyList<OsanStepCompletedPreferenceItem> StepCompletedStages);

public enum OsanNotificationPreferenceResultStatus
{
    Success,
    Conflict,
    Invalid
}

public sealed record OsanNotificationPreferenceResult(
    OsanNotificationPreferenceResultStatus Status,
    OsanNotificationPreferenceResponse? Response,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static OsanNotificationPreferenceResult Success(OsanNotificationPreferenceResponse response) =>
        new(OsanNotificationPreferenceResultStatus.Success, response, null, null);

    public static OsanNotificationPreferenceResult Failure(
        OsanNotificationPreferenceResultStatus status,
        string errorCode,
        string errorMessage) =>
        new(status, null, errorCode, errorMessage);
}
