using System.Globalization;

namespace Emi.Qms.Api.Admin;

public static class AdminDeletionLifecycle
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string DeletionScheduled = "DeletionScheduled";
    public const string PurgeBlocked = "PurgeBlocked";

    public static string Calculate(
        bool isActive,
        DateTimeOffset? deletionRequestedAtUtc,
        DateTimeOffset? scheduledHardDeleteAtUtc,
        DateTimeOffset? purgeBlockedAtUtc)
    {
        if (purgeBlockedAtUtc is not null)
        {
            return PurgeBlocked;
        }

        if (deletionRequestedAtUtc is not null && scheduledHardDeleteAtUtc is not null)
        {
            return DeletionScheduled;
        }

        return isActive ? Active : Inactive;
    }

    public static string Label(string lifecycleStatus)
    {
        return lifecycleStatus switch
        {
            Active => "활성",
            Inactive => "비활성",
            DeletionScheduled => "삭제 예정",
            PurgeBlocked => "삭제 보류",
            _ => lifecycleStatus
        };
    }

    public static string? FormatScheduledHardDeleteLabel(DateTimeOffset? scheduledHardDeleteAtUtc)
    {
        if (scheduledHardDeleteAtUtc is null)
        {
            return null;
        }

        var seoulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
        var seoulTime = TimeZoneInfo.ConvertTime(scheduledHardDeleteAtUtc.Value, seoulTimeZone);
        return seoulTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
