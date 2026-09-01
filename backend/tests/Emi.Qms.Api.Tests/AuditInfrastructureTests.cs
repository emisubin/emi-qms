using Emi.Qms.Api.Audit;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.RegularExpressions;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class AuditInfrastructureTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ExplicitRelationExclusions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["GlobalAuditInfrastructure"] = ParseRelationNames("""
                audit_coverage_state audit_event_changes audit_events
                site_access_coverage_state site_access_sessions
                """),
            ["ExistingCanonicalLedger"] = ParseRelationNames("""
                admin_master_change_logs authorization_audit_events data_export_events
                form_template_audit_events lqc_item_setting_audit_events material_category_audit_events
                material_category_iqc_setting_audit_events material_receipt_events notice_post_revisions
                panel_manufacturing_events panel_qr_events pending_history pending_issue_type_audit_events
                production_plan_template_audit_events project_audit_events project_workflow_events
                sales_monthly_target_audit_events ul891_recovery_case_events
                user_notification_preference_audit_events user_profile_photo_audit_events
                """),
            ["ProviderWorkerOrGeneratedArtifact"] = ParseRelationNames("""
                iqc_report_pdf_artifacts notification_deliveries notification_delivery_attempts
                notification_delivery_reprocess_events notification_recipients notifications
                panel_quality_report_pdf_artifacts web_push_subscription_events web_push_subscriptions
                work_item_escalations
                """),
            ["OperationImportOrIdempotency"] = ParseRelationNames("""
                logistics_operations panel_information_excel_import_batches panel_kitting_batches
                panel_manufacturing_assembly_batch_operations panel_manufacturing_operations
                panel_manufacturing_release_operations panel_quality_operations pending_photo_operations
                procurement_excel_import_batch_projects procurement_excel_import_batches
                production_planning_excel_import_batches sales_billing_request_download_events
                sales_billing_request_operations sales_monthly_billing_operations
                sales_settlement_operations ul891_set_operations
                """),
            ["SeedReferenceData"] = ParseRelationNames("""
                permissions roles
                """)
        };

    [Fact]
    public void DatabaseConnectionString_ReceivesOnlyFixedAuditContextInsideScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:QmsDatabase"] = "Host=localhost;Port=5432;Database=qms;Username=runtime;Password=test-password"
            })
            .Build();
        var provider = new DatabaseConnectionStringProvider(configuration);
        var actorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var requestId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var loginId = Guid.Parse("10000000-0000-0000-0000-000000000003");

        Assert.DoesNotContain("qms.audit_", provider.GetConnectionString(), StringComparison.Ordinal);

        using (AuditRequestContext.Push(new AuditMutationContext(
                   actorId, null, requestId, loginId, "Projects", "UpdateProject", "UpdateProject")))
        {
            var builder = new NpgsqlConnectionStringBuilder(provider.GetConnectionString());
            Assert.False(builder.Pooling);
            Assert.Contains($"qms.audit_actor_id={actorId:D}", builder.Options, StringComparison.Ordinal);
            Assert.Contains($"qms.audit_request_id={requestId:D}", builder.Options, StringComparison.Ordinal);
            Assert.Contains($"qms.audit_login_id={loginId:D}", builder.Options, StringComparison.Ordinal);
            Assert.Contains("qms.audit_domain=Projects", builder.Options, StringComparison.Ordinal);
            Assert.DoesNotContain("test-password", builder.Options, StringComparison.Ordinal);
        }

        var baseBuilder = new NpgsqlConnectionStringBuilder(provider.GetConnectionString());
        Assert.True(baseBuilder.Pooling);
        Assert.DoesNotContain("qms.audit_", baseBuilder.ConnectionString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Mozilla/5.0 Chrome/125.0 Safari/537.36", "Chrome")]
    [InlineData("Mozilla/5.0 Chrome/125.0 Edg/125.0", "Edge")]
    [InlineData("Mozilla/5.0 Version/17.0 Safari/605.1", "Safari")]
    [InlineData("custom-agent", "Other")]
    public void LoginUserAgent_IsReducedToFixedBrowserFamily(string userAgent, string expected)
    {
        Assert.Equal(expected, AuditEndpointExtensions.ResolveBrowserFamily(userAgent));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0)", "Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)", "iOS")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X)", "macOS")]
    [InlineData("custom-agent", "Other")]
    public void LoginUserAgent_IsReducedToFixedOperatingSystemFamily(string userAgent, string expected)
    {
        Assert.Equal(expected, AuditEndpointExtensions.ResolveOsFamily(userAgent));
    }

    [Fact]
    public void MigrationContract_IsAppendOnlyAndCannotProjectForbiddenValues()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "database",
            "migrations",
            "0083_global_access_change_audit.sql"));

        Assert.Contains("Global audit records are append-only.", migration, StringComparison.Ordinal);
        Assert.Contains("password|token|authorization|cookie|secret|payload|binary", migration, StringComparison.Ordinal);
        Assert.Contains("(request|response|exception|raw)_?body", migration, StringComparison.Ordinal);
        Assert.Contains("projection_kind in ('ExactScalar', 'MetadataOnly')", migration, StringComparison.Ordinal);
        Assert.Contains("'g2_daily_metrics', 'g2_inventory_counts', 'g2_targets'", migration, StringComparison.Ordinal);
        Assert.Contains("'procurement_required_item_template_rows', 'procurement_required_item_templates'", migration, StringComparison.Ordinal);
        Assert.Contains("'projects.status', 'projects.structure_mode'", migration, StringComparison.Ordinal);
        Assert.Contains("'notice_posts.body_format'", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("cookie|secret|body|payload", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("when field_name ~ '(^|_)(status|state|type|priority|code", migration, StringComparison.Ordinal);
        Assert.Contains("on conflict (request_correlation_id) do nothing", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("request_body", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("response_body", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'Duplicate'", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationRelationCoverage_ExactlyClassifiesEverySchemaRelation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationDirectory = Path.Combine(repositoryRoot, "database", "migrations");
        var schemaRelations = Directory.GetFiles(migrationDirectory, "*.sql")
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    @"create\s+table\s+(?:if\s+not\s+exists\s+)?([a-z_][a-z0-9_]*)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value.ToLowerInvariant()))
            .ToHashSet(StringComparer.Ordinal);

        var auditMigration = File.ReadAllText(Path.Combine(
            migrationDirectory,
            "0083_global_access_change_audit.sql"));
        var registryStart = auditMigration.IndexOf(
            "foreach relation_name in array array[",
            StringComparison.Ordinal);
        var registryEnd = auditMigration.IndexOf(
            "]\n    loop",
            registryStart,
            StringComparison.Ordinal);
        Assert.True(registryStart >= 0 && registryEnd > registryStart);
        var trackedRelations = Regex.Matches(
                auditMigration[registryStart..registryEnd],
                "'([a-z_][a-z0-9_]*)'",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var excludedRelations = ExplicitRelationExclusions.Values
            .SelectMany(relations => relations)
            .ToArray();

        Assert.Equal(excludedRelations.Length, excludedRelations.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(trackedRelations.Intersect(excludedRelations, StringComparer.Ordinal));
        var classifiedRelations = trackedRelations
            .Concat(excludedRelations)
            .ToHashSet(StringComparer.Ordinal);
        var missing = schemaRelations.Except(classifiedRelations, StringComparer.Ordinal).Order().ToArray();
        var stale = classifiedRelations.Except(schemaRelations, StringComparer.Ordinal).Order().ToArray();
        Assert.True(
            missing.Length == 0 && stale.Length == 0,
            $"Missing=[{string.Join(" | ", missing)}] Stale=[{string.Join(" | ", stale)}]");
        Assert.Equal(94, trackedRelations.Count);
        Assert.Equal(53, excludedRelations.Length);
    }

    [Fact]
    public void SiteAccessMigration_UsesFixedMenusDatabaseTimeAndGuardedUpdates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "database",
            "migrations",
            "0085_site_access_sessions.sql"));

        Assert.Equal(19, SiteAccessMenuCodes.Labels.Count);
        Assert.Contains("observed_at_utc := clock_timestamp()", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("p_observed_at_utc", migration, StringComparison.Ordinal);
        Assert.Contains("access.last_activity_at_utc > observed_at_utc - interval '30 minutes'", migration, StringComparison.Ordinal);
        Assert.Contains("Site access menu history is append-only.", migration, StringComparison.Ordinal);
        Assert.Contains("Only explicit logout can end a site access record.", migration, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("http", migration, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> ParseRelationNames(string names) => names
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "database", "migrations")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
