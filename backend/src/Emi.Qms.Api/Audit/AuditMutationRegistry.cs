using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace Emi.Qms.Api.Audit;

public sealed record AuditMutationDefinition(
    bool Included,
    string Domain,
    string Action,
    string RouteKey,
    string TargetType,
    string? TargetKey,
    string ConflictFailureReason);

public static partial class AuditMutationRegistry
{
    private static readonly IReadOnlySet<string> MutationMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    // This closed set is populated from the runtime endpoint catalog and locked by a contract test.
    // Any new mutation endpoint must be deliberately classified before application startup succeeds.
    internal static readonly IReadOnlySet<string> KnownMutationRouteKeys = ParseRouteKeys("""
        DELETE /api/admin/calendar/holidays/{holidayId:guid}
        DELETE /api/admin/calendar/holidays/{holidayId:guid}/purge
        DELETE /api/admin/departments/{departmentId:guid}/purge
        DELETE /api/admin/users/{userId:guid}/purge
        DELETE /api/deleted-projects/{projectId:guid}/purge
        DELETE /api/g2/inventory-counts/{date}
        DELETE /api/logistics/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/evidence/{evidenceId:guid}
        DELETE /api/me/profile-photo
        DELETE /api/notices/{noticeId:guid}
        DELETE /api/notices/{noticeId:guid}/attachments/{attachmentId:guid}
        DELETE /api/pending/{pendingId:guid}/photos/{photoId:guid}
        DELETE /api/quality/inspections/reports/{reportId:guid}/photos/{photoId:guid}
        DELETE /api/quality/iqc/reports/{reportId:guid}/photos/{photoId:guid}
        DELETE /api/quality/iqc/scan-reports/{reportId:guid}/attachments/{attachmentId:guid}
        PATCH /api/admin/departments/reorder
        PATCH /api/admin/departments/{departmentId:guid}/deactivate
        PATCH /api/admin/users/{userId:guid}
        PATCH /api/admin/users/{userId:guid}/schedule-deletion
        PATCH /api/materials/receipts
        PATCH /api/procurement/settings/required-items/{itemCode}
        PATCH /api/production-planning/settings/templates/{productTypeId:guid}
        PATCH /api/projects/{projectId:guid}
        PATCH /api/projects/{projectId:guid}/panel-information/
        PATCH /api/projects/{projectId:guid}/procurement/
        PATCH /api/projects/{projectId:guid}/production-planning/
        PATCH /api/projects/{projectId:guid}/production-planning/department-assignees
        PATCH /api/projects/{projectId:guid}/production-planning/set-defaults
        PATCH /api/projects/{projectId:guid}/production-planning/set-scopes/{setInstanceId:guid}
        POST /api/admin/calendar/holidays
        POST /api/admin/calendar/holidays/apply
        POST /api/admin/calendar/holidays/bulk-delete
        POST /api/admin/calendar/holidays/bulk-restore
        POST /api/admin/calendar/holidays/preview
        POST /api/admin/calendar/holidays/{holidayId:guid}/restore
        POST /api/admin/departments
        POST /api/admin/departments/bulk-delete
        POST /api/admin/departments/bulk-restore
        POST /api/admin/departments/{departmentId:guid}/restore
        POST /api/admin/notification-deliveries/acknowledge
        POST /api/admin/notification-deliveries/dismiss
        POST /api/admin/notification-deliveries/reprocess-failed
        POST /api/admin/notification-deliveries/retry
        POST /api/admin/notification-deliveries/send-manual
        POST /api/admin/notification-deliveries/test-mail
        POST /api/admin/notification-deliveries/test-teams-activity
        POST /api/admin/users/bulk-delete
        POST /api/admin/users/bulk-restore
        POST /api/admin/users/{userId:guid}/notification-preferences/reset
        POST /api/admin/users/{userId:guid}/restore
        POST /api/audit/sessions/interactive-login
        POST /api/audit/sessions/logout
        POST /api/data-exports/selected
        POST /api/deleted-projects/purge-all
        POST /api/deleted-projects/{projectId:guid}/restore
        POST /api/form-templates/export
        POST /api/form-templates/managers
        POST /api/form-templates/managers/{bindingId:guid}/revoke
        POST /api/form-templates/material-categories
        POST /api/form-templates/{family}/{templateKey}/versions
        POST /api/form-templates/{family}/{templateKey}/versions/{versionId:guid}/activate
        POST /api/form-templates/{family}/{templateKey}/versions/{versionId:guid}/cancel
        POST /api/logistics/packing-units
        POST /api/logistics/{stage:regex(^(departure|delivery)$)}-batches
        POST /api/logistics/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/cancel
        POST /api/logistics/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/evidence
        POST /api/logistics/{stage:regex(^(packing|departure|delivery)$)}/{targetId:guid}/finalize
        POST /api/manufacturing/executions/start
        POST /api/manufacturing/executions/step-batch
        POST /api/manufacturing/executions/{executionId:guid}/check-step
        POST /api/manufacturing/executions/{executionId:guid}/complete
        POST /api/manufacturing/executions/{executionId:guid}/resume
        POST /api/manufacturing/executions/{executionId:guid}/stop
        POST /api/manufacturing/releases
        POST /api/materials/items/{itemId:guid}/close-arrivals
        POST /api/materials/items/{itemId:guid}/receipts
        POST /api/materials/kitting/complete
        POST /api/materials/receipts/{receiptId:guid}/cancel
        POST /api/materials/receipts/{receiptId:guid}/confirm
        POST /api/materials/receipts/{receiptId:guid}/iqc-requests
        POST /api/materials/receipts/{receiptId:guid}/reinspection
        POST /api/my-work/{workItemId:guid}/cancel
        POST /api/my-work/{workItemId:guid}/complete
        POST /api/my-work/{workItemId:guid}/start
        POST /api/my/notification-preferences/reset
        POST /api/my/web-push/current-status
        POST /api/my/web-push/subscriptions/deactivate-all
        POST /api/my/web-push/subscriptions/deactivate-current
        POST /api/notices/
        POST /api/notices/{noticeId:guid}/attachments
        POST /api/notifications/projects/{projectId:guid}/read-all
        POST /api/notifications/read-all
        POST /api/notifications/{notificationId:guid}/read
        POST /api/pending-types/
        POST /api/pending-types/{code}/activate
        POST /api/pending-types/{code}/deactivate
        POST /api/pending/
        POST /api/pending/{pendingId:guid}/assign
        POST /api/pending/{pendingId:guid}/comments
        POST /api/pending/{pendingId:guid}/photos
        POST /api/pending/{pendingId:guid}/transition
        POST /api/procurement/import/apply
        POST /api/procurement/import/preview
        POST /api/production-control/templates/manufacturing/{productTypeId:guid}/current
        POST /api/production-control/templates/planning/{productTypeId:guid}/current
        POST /api/production-planning/holidays/sync
        POST /api/production-planning/import/apply
        POST /api/production-planning/import/preview
        POST /api/production-planning/product-types
        POST /api/projects
        POST /api/projects/export/selected
        POST /api/projects/import/apply
        POST /api/projects/import/preview
        POST /api/projects/{projectId:guid}/cancel
        POST /api/projects/{projectId:guid}/change-panel-count
        POST /api/projects/{projectId:guid}/delete
        POST /api/projects/{projectId:guid}/hold
        POST /api/projects/{projectId:guid}/monthly-billing/open
        POST /api/projects/{projectId:guid}/monthly-billing/{ledgerId:guid}/confirm
        POST /api/projects/{projectId:guid}/monthly-billing/{ledgerId:guid}/revisions
        POST /api/projects/{projectId:guid}/panel-information/import/apply
        POST /api/projects/{projectId:guid}/panel-information/import/preview
        POST /api/projects/{projectId:guid}/panels/{panelId:guid}/qr
        POST /api/projects/{projectId:guid}/panels/{panelId:guid}/qr/rotate
        POST /api/projects/{projectId:guid}/production-planning/import/apply
        POST /api/projects/{projectId:guid}/production-planning/import/preview
        POST /api/projects/{projectId:guid}/qr/issue-batch
        POST /api/projects/{projectId:guid}/qr/print-sheet
        POST /api/projects/{projectId:guid}/reactivate
        POST /api/projects/{projectId:guid}/recovery-cases/{caseId:guid}/recover
        POST /api/projects/{projectId:guid}/resume
        POST /api/projects/{projectId:guid}/set-instances/cancel
        POST /api/projects/{projectId:guid}/set-specs
        POST /api/projects/{projectId:guid}/set-specs/{specId:guid}/apply-version
        POST /api/projects/{projectId:guid}/set-specs/{specId:guid}/instances/increase
        POST /api/projects/{projectId:guid}/set-specs/{specId:guid}/versions
        POST /api/projects/{projectId:guid}/set-specs/{specId:guid}/versions/{versionId:guid}/publish
        POST /api/projects/{projectId:guid}/settlement/complete
        POST /api/qr/resolve
        POST /api/quality/inspections/manufacturing-completed/confirm
        POST /api/quality/inspections/reconcile
        POST /api/quality/inspections/reinspection
        POST /api/quality/inspections/reports/{reportId:guid}/finalize
        POST /api/quality/inspections/reports/{reportId:guid}/pdf/retry
        POST /api/quality/inspections/reports/{reportId:guid}/photos
        POST /api/quality/inspections/start
        POST /api/quality/iqc/reconcile
        POST /api/quality/iqc/reports/{reportId:guid}/finalize
        POST /api/quality/iqc/reports/{reportId:guid}/pdf/retry
        POST /api/quality/iqc/reports/{reportId:guid}/photos
        POST /api/quality/iqc/scan-reports/{reportId:guid}/attachments
        POST /api/quality/iqc/scan-reports/{reportId:guid}/finalize
        POST /api/quality/iqc/{attemptId:guid}/reports
        POST /api/quality/iqc/{attemptId:guid}/result
        POST /api/sales/billing-requests/
        POST /api/system/holidays/sync/kr
        PUT /api/admin/calendar/holidays/{holidayId:guid}
        PUT /api/admin/departments/{departmentId:guid}
        PUT /api/admin/users/{userId:guid}/notification-preferences
        PUT /api/form-templates/lqc-items/{productTypeId:guid}/current
        PUT /api/form-templates/lqc-items/{productTypeId:guid}/operating-status
        PUT /api/form-templates/material-categories/{categoryId:guid}
        PUT /api/form-templates/material-category-iqc/{categoryId:guid}/current
        PUT /api/form-templates/material-category-iqc/{categoryId:guid}/setting
        PUT /api/form-templates/{family}/{templateKey}/current
        PUT /api/form-templates/{family}/{templateKey}/versions/{versionId:guid}/items
        PUT /api/g2/attendance/{date}
        PUT /api/g2/inventory-counts/{date}
        PUT /api/g2/operations/{date}
        PUT /api/g2/targets/{targetType}/{effectiveDate}
        PUT /api/logistics/packing-units/{unitId:guid}/panels
        PUT /api/logistics/{stage:regex(^(departure|delivery)$)}-batches/{batchId:guid}/units
        PUT /api/me/profile-photo
        PUT /api/my/notification-preferences
        PUT /api/my/web-push/subscriptions
        PUT /api/notices/{noticeId:guid}
        PUT /api/pending-types/reorder
        PUT /api/pending-types/{code}
        PUT /api/production-control/templates/manufacturing/{productTypeId:guid}/versions/{versionId:guid}
        PUT /api/production-control/templates/planning/{productTypeId:guid}/versions/{versionId:guid}
        PUT /api/projects/{projectId:guid}/set-specs/{specId:guid}/design
        PUT /api/projects/{projectId:guid}/set-specs/{specId:guid}/versions/{versionId:guid}
        PUT /api/projects/{projectId:guid}/settlement/draft
        PUT /api/quality/inspections/reports/{reportId:guid}/responses
        PUT /api/quality/iqc/reports/{reportId:guid}/responses
        PUT /api/sales/targets
        """);

    private static readonly IReadOnlySet<string> ExcludedMutationRouteKeys = ParseRouteKeys("""
        POST /api/admin/calendar/holidays/preview
        POST /api/admin/notification-deliveries/acknowledge
        POST /api/admin/notification-deliveries/dismiss
        POST /api/admin/notification-deliveries/reprocess-failed
        POST /api/admin/notification-deliveries/retry
        POST /api/admin/notification-deliveries/send-manual
        POST /api/admin/notification-deliveries/test-mail
        POST /api/admin/notification-deliveries/test-teams-activity
        POST /api/audit/sessions/interactive-login
        POST /api/audit/sessions/logout
        POST /api/data-exports/selected
        POST /api/form-templates/export
        POST /api/my/web-push/current-status
        POST /api/my/web-push/subscriptions/deactivate-all
        POST /api/my/web-push/subscriptions/deactivate-current
        POST /api/notifications/projects/{projectId:guid}/read-all
        POST /api/notifications/read-all
        POST /api/notifications/{notificationId:guid}/read
        POST /api/procurement/import/preview
        POST /api/production-planning/import/preview
        POST /api/projects/export/selected
        POST /api/projects/import/preview
        POST /api/projects/{projectId:guid}/panel-information/import/preview
        POST /api/projects/{projectId:guid}/production-planning/import/preview
        POST /api/projects/{projectId:guid}/qr/print-sheet
        POST /api/qr/resolve
        POST /api/quality/inspections/reports/{reportId:guid}/pdf/retry
        POST /api/quality/iqc/reports/{reportId:guid}/pdf/retry
        PUT /api/my/web-push/subscriptions
        """);

    public static bool IsMutationMethod(string method) => MutationMethods.Contains(method);

    public static bool TryResolve(HttpContext context, out AuditMutationDefinition definition)
    {
        definition = default!;
        if (!IsMutationMethod(context.Request.Method)
            || context.GetEndpoint() is not RouteEndpoint endpoint)
        {
            return false;
        }

        var routeKey = BuildRouteKey(context.Request.Method, endpoint.RoutePattern.RawText);
        if (!KnownMutationRouteKeys.Contains(routeKey))
        {
            throw new InvalidOperationException($"Unclassified mutation endpoint reached runtime: {routeKey}");
        }

        var endpointName = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        var included = !ExcludedMutationRouteKeys.Contains(routeKey);
        if (!included)
        {
            definition = new AuditMutationDefinition(
                false, "Excluded", "Excluded", "Excluded", "excluded", null, AuditFailureReasons.Conflict);
            return true;
        }

        if (string.IsNullOrWhiteSpace(endpointName) || !SafeCode().IsMatch(endpointName))
        {
            throw new InvalidOperationException($"Included mutation endpoint needs a fixed safe name: {routeKey}");
        }

        var domain = ResolveDomain(endpoint.RoutePattern.RawText ?? string.Empty);
        var target = ResolveTarget(context, domain);
        definition = new AuditMutationDefinition(
            true,
            domain,
            endpointName,
            endpointName,
            target.Type,
            target.Key,
            ResolveConflictReason(routeKey));
        return true;
    }

    public static void ValidateCoverage(IEnumerable<EndpointDataSource> dataSources)
    {
        var mutationEndpoints = dataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Where(IsMutationMethod)
                .Select(method => (Method: method.ToUpperInvariant(), Endpoint: endpoint))
                ?? [])
            .ToArray();
        var discovered = mutationEndpoints
            .Select(item => BuildRouteKey(item.Method, item.Endpoint.RoutePattern.RawText))
            .ToHashSet(StringComparer.Ordinal);

        var missing = discovered.Except(KnownMutationRouteKeys, StringComparer.Ordinal).Order().ToArray();
        var stale = KnownMutationRouteKeys.Except(discovered, StringComparer.Ordinal).Order().ToArray();
        if (missing.Length > 0 || stale.Length > 0)
        {
            throw new InvalidOperationException(
                $"Audit mutation coverage registry mismatch. Missing=[{string.Join(" | ", missing)}] Stale=[{string.Join(" | ", stale)}]");
        }

        var invalidExclusions = ExcludedMutationRouteKeys.Except(KnownMutationRouteKeys, StringComparer.Ordinal).ToArray();
        if (invalidExclusions.Length > 0)
        {
            throw new InvalidOperationException("Audit mutation exclusion registry contains an unknown endpoint.");
        }

        var unnamedIncluded = mutationEndpoints
            .Where(item => !ExcludedMutationRouteKeys.Contains(
                BuildRouteKey(item.Method, item.Endpoint.RoutePattern.RawText)))
            .Where(item => item.Endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName is not string name
                || !SafeCode().IsMatch(name))
            .Select(item => BuildRouteKey(item.Method, item.Endpoint.RoutePattern.RawText))
            .Order()
            .ToArray();
        if (unnamedIncluded.Length > 0)
        {
            throw new InvalidOperationException(
                $"Included audit mutation endpoints need fixed safe names: [{string.Join(" | ", unnamedIncluded)}]");
        }
    }

    internal static string BuildRouteKey(string method, string? rawPattern) =>
        $"{method.ToUpperInvariant()} {rawPattern ?? string.Empty}";

    private static IReadOnlySet<string> ParseRouteKeys(string routes) => routes
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);

    private static string ResolveDomain(string pattern)
    {
        if (pattern.Contains("/g2/", StringComparison.Ordinal)) return "G2";
        if (pattern.Contains("/production-planning", StringComparison.Ordinal)) return "ProductionPlanning";
        if (pattern.Contains("/procurement", StringComparison.Ordinal)) return "Procurement";
        if (pattern.Contains("/materials", StringComparison.Ordinal)) return "Materials";
        if (pattern.Contains("/manufacturing", StringComparison.Ordinal)) return "Manufacturing";
        if (pattern.Contains("/quality", StringComparison.Ordinal)) return "Quality";
        if (pattern.Contains("/logistics", StringComparison.Ordinal)) return "Logistics";
        if (pattern.Contains("/pending", StringComparison.Ordinal)) return "Pending";
        if (pattern.Contains("/notices", StringComparison.Ordinal)) return "Notices";
        if (pattern.Contains("/notification", StringComparison.Ordinal)) return "Notifications";
        if (pattern.Contains("/admin", StringComparison.Ordinal)) return "Administration";
        if (pattern.Contains("/projects", StringComparison.Ordinal)) return "Projects";
        if (pattern.Contains("/my-work", StringComparison.Ordinal)) return "Workflow";
        return "Operations";
    }

    private static (string Type, string? Key) ResolveTarget(HttpContext context, string domain)
    {
        var routeValue = new[]
            {
                "projectId", "panelId", "pendingId", "noticeId", "userId", "departmentId",
                "holidayId", "reportId", "attemptId", "receiptId", "workItemId", "specId",
                "versionId", "batchId", "unitId", "targetId", "itemId", "photoId",
                "attachmentId", "executionId", "ledgerId", "caseId", "bindingId", "code", "date"
            }
            .Select(key => context.Request.RouteValues.TryGetValue(key, out var value)
                ? value?.ToString()
                : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var targetType = Regex.Replace(domain.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return (string.IsNullOrEmpty(targetType) ? "operation" : targetType, BoundTargetKey(routeValue));
    }

    private static string? BoundTargetKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (Guid.TryParse(normalized, out var guid))
        {
            return guid.ToString("D");
        }
        if (DateOnly.TryParse(normalized, out var date))
        {
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        return SafeTargetKey().IsMatch(normalized) ? normalized : null;
    }

    // A 409 only proves that the server rejected the current state. Endpoint names such as
    // CreatePendingIssue do not prove a duplicate, and response bodies are deliberately not read.
    // Keep the classification conservative until an endpoint supplies fixed server-owned metadata.
    internal static string ResolveConflictReason(string routeKey)
    {
        if (!KnownMutationRouteKeys.Contains(routeKey))
        {
            throw new InvalidOperationException($"Cannot classify an unknown mutation route: {routeKey}");
        }

        return AuditFailureReasons.Conflict;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCode();

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTargetKey();
}
