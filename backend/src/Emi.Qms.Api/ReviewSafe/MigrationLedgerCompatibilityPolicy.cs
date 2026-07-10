namespace Emi.Qms.Api.ReviewSafe;

public static class MigrationLedgerCompatibilityPolicy
{
    public const string LegacyTeamsActivityVersion = "0020_teams_activity_delivery_channel";
    public const string CanonicalTeamsActivitySuccessor = "0023_teams_activity_delivery_channel";
    public const string IntroducedByTask = "TASK-DB-MIGRATION-001";
    public const string TeamsActivitySchemaProbe = "notification_deliveries_channel";

    public static IReadOnlyList<ApprovedLegacyMigration> ApprovedLegacyMigrations { get; } =
    [
        new ApprovedLegacyMigration(
            LegacyTeamsActivityVersion,
            CanonicalTeamsActivitySuccessor,
            "NOTIFY-003의 merge 전 WIP에서 적용된 marker이며 canonical successor와 SQL blob이 동일함.",
            IntroducedByTask,
            TeamsActivitySchemaProbe)
    ];
}

public sealed record ApprovedLegacyMigration(
    string LegacyVersion,
    string CanonicalSuccessor,
    string Reason,
    string IntroducedByTask,
    string RequiredSchemaProbe);
