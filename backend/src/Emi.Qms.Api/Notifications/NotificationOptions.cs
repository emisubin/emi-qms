namespace Emi.Qms.Api.Notifications;

public sealed class NotificationOptions
{
    public NotificationDispatchOptions Dispatch { get; init; } = new();
    public NotificationLinkOptions Links { get; init; } = new();
    public NotificationDailyDigestOptions DailyDigest { get; init; } = new();
    public NotificationEscalationOptions Escalation { get; init; } = new();
    public NotificationTeamsOptions Teams { get; init; } = new();
    public NotificationTeamsActivityOptions TeamsActivity { get; init; } = new();
    public NotificationMailOptions Mail { get; init; } = new();
    public NotificationGraphOptions Graph { get; init; } = new();
}

public sealed class NotificationLinkOptions
{
    public string? BaseUrl { get; init; }
}

public sealed class NotificationDispatchOptions
{
    public bool Enabled { get; init; }
    public int WorkerIntervalSeconds { get; init; } = 60;
    public int RetryCount { get; init; } = 3;
    public int ClaimLeaseSeconds { get; init; } = 300;
    public int DedupeWindowHours { get; init; } = 24;
    public int BatchWindowSeconds { get; init; } = 120;
    public int MaxBatchSize { get; init; } = 50;
}

public sealed class NotificationDailyDigestOptions
{
    public bool Enabled { get; init; }
    public string Time { get; init; } = "07:30";
    public string TimeZone { get; init; } = "Asia/Seoul";
}

public sealed class NotificationEscalationOptions
{
    public bool Enabled { get; init; }
    public int WorkerIntervalSeconds { get; init; } = 300;
    public string TimeZone { get; init; } = "Asia/Seoul";
    public bool TeamsPersonalDryRun { get; init; } = true;
    public string TeamsPersonalChannelStrategy { get; init; } = "TeamsDirectMessageDryRun";
    public bool UseTeamsChannelFallback { get; init; }
    public bool MailEnabled { get; init; } = true;
    public int MaxBatchSize { get; init; } = 100;
}

public sealed class NotificationTeamsOptions
{
    public bool Enabled { get; init; }
    public bool DryRun { get; init; } = true;
    public string? WebhookUrl { get; init; }
    public string PayloadMode { get; init; } = "AdaptiveCardRoot";
}

public sealed class NotificationTeamsActivityOptions
{
    public bool Enabled { get; init; }
    public bool DryRun { get; init; } = true;
    public string AuthorityHost { get; init; } = "https://login.microsoftonline.com";
    public string BaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";
    public string Scope { get; init; } = "https://graph.microsoft.com/.default";
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? ManifestId { get; init; }
    public string? TeamsManifestExternalId { get; init; }
    public string? TeamsAppId { get; init; }
    public string? TeamsCatalogAppId { get; init; }
    public string? InstalledAppId { get; init; }
    public string? TopicWebUrl { get; init; }
    public string DeepLinkEntityId { get; init; } = "home";
    public string TeamsStaticTabEntityId { get; init; } = "home";
    public bool UseTeamsAppIdForTextTopic { get; init; }
    public bool UseUserPrincipalNameFallback { get; init; }
    public bool RequireEntraUser { get; init; } = true;
    public string PersonalChannelStrategy { get; init; } = "TeamsDirectMessageDryRun";
    public NotificationTeamsActivityTypeOptions ActivityTypes { get; init; } = new();
}

public sealed class NotificationTeamsActivityTypeOptions
{
    public string WorkItemAssigned { get; init; } = "workItemAssigned";
    public string DeadlineApproaching { get; init; } = "deadlineApproaching";
    public string DeadlineOverdue { get; init; } = "deadlineOverdue";
    public string UrgentPending { get; init; } = "urgentPending";
    public string DailyDigest { get; init; } = "dailyDigest";
    public string ProjectCompleted { get; init; } = "projectCompleted";
}

public sealed class NotificationMailOptions
{
    public bool Enabled { get; init; }
    public bool DryRun { get; init; } = true;
    public string Provider { get; init; } = "DryRun";
    public string? SenderUserId { get; init; }
    public string? SenderAddress { get; init; }
    public string SenderDisplayName { get; init; } = "EMI 프로젝트 통합관리시스템 알림";
    public string? TestRecipientEmail { get; init; }
    public bool SaveTestMailToSentItems { get; init; }
    public NotificationSmtpOptions Smtp { get; init; } = new();
}

public sealed class NotificationSmtpOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string Security { get; init; } = "StartTls";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed class NotificationGraphOptions
{
    public string AuthorityHost { get; init; } = "https://login.microsoftonline.com";
    public string BaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";
    public string Scope { get; init; } = "https://graph.microsoft.com/.default";
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}
