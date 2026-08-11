namespace Emi.Qms.Api.Notifications;

public static class NotificationPreferenceCatalog
{
    public const string TaxonomyVersion = "2026-08-v1";
    public const string TeamsPersonalPreferenceChannel = NotificationDeliveryChannels.TeamsDirectMessage;

    public static readonly IReadOnlyList<NotificationPreferenceItemDefinition> Items =
    [
        new(
            NotificationDeliveryTypes.WorkItemCreated,
            TeamsPersonalPreferenceChannel,
            "자동 단계 업무 생성",
            "Teams 개인 알림",
            "프로젝트 단계가 바뀌며 자동 생성된 내 업무를 알려줍니다.",
            true,
            null),
        new(
            NotificationDeliveryTypes.DueSoonL0,
            TeamsPersonalPreferenceChannel,
            "예정일 임박 D-1",
            "Teams 개인 알림",
            "예정일 직전 영업일에 미리 알려줍니다.",
            true,
            null),
        new(
            NotificationDeliveryTypes.DailyDigest,
            NotificationDeliveryChannels.Mail,
            "일일 업무 요약",
            "메일",
            "열린 업무와 읽지 않은 알림을 하루 한 번 요약합니다.",
            true,
            null),
        new(
            NotificationDeliveryTypes.UrgentBlocking,
            NotificationDeliveryChannels.Mail,
            "긴급·차단",
            "메일",
            "즉시 확인이 필요한 차단 상황을 알려줍니다.",
            false,
            "업무상 필수 알림은 해제할 수 없습니다."),
        new(
            NotificationDeliveryTypes.OverdueL1,
            TeamsPersonalPreferenceChannel,
            "예정일 초과 L1",
            "Teams 개인 알림 · 메일",
            "예정일을 넘긴 업무의 1단계 에스컬레이션입니다.",
            false,
            "업무상 필수 알림은 해제할 수 없습니다.")
    ];

    public static readonly IReadOnlyList<NotificationPreferenceItemDefinition> ChangeableItems =
        Items.Where(item => item.CanChange).ToArray();

    public static NotificationPreferenceItemDefinition? Find(string? deliveryType, string? channel) =>
        Items.SingleOrDefault(item =>
            string.Equals(item.DeliveryType, deliveryType?.Trim(), StringComparison.Ordinal)
            && string.Equals(item.Channel, channel?.Trim(), StringComparison.Ordinal));
}
