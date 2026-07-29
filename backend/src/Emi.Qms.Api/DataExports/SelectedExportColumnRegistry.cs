using System.Security.Cryptography;
using System.Text;

namespace Emi.Qms.Api.DataExports;

internal sealed record SelectedExportColumn(
    string Key,
    string Label,
    bool Required,
    bool SensitiveSalesAmountIncluded = false);

internal static class SelectedExportColumnRegistry
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> RequiredKeysByLabel =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [SelectedExportScreens.Projects] = Required(("PJT Code", "project-code")),
            [SelectedExportScreens.MyWork] = Required(("업무", "work-title"), ("PJT Code", "project-code")),
            [SelectedExportScreens.ProductionPlanning] = Required(("PJT Code", "project-code")),
            [SelectedExportScreens.Procurement] = Required(("PJT Code", "project-code")),
            [SelectedExportScreens.MaterialReceipts] = Required(("PJT Code", "project-code"), ("발주 품목", "order-item")),
            [SelectedExportScreens.MaterialKitting] = Required(("PJT Code", "project-code"), ("면 코드", "panel-code")),
            [SelectedExportScreens.Manufacturing] = Required(("PJT Code", "project-code"), ("면 코드", "panel-code")),
            [SelectedExportScreens.MaterialIqc] = Required(("PJT Code", "project-code"), ("품목", "order-item"), ("차수", "attempt-number")),
            [SelectedExportScreens.QualityInspections] = Required(("PJT Code", "project-code"), ("면 코드", "panel-code")),
            [SelectedExportScreens.Logistics] = Required(("PJT Code", "project-code"), ("대상 코드", "target-code")),
            [SelectedExportScreens.Pending] = Required(("번호", "issue-number")),
            [SelectedExportScreens.Notifications] = Required(("제목", "notification-title"), ("생성일시", "created-at")),
            [SelectedExportScreens.AdminUsers] = Required(("이름", "display-name"), ("업무 이메일", "email")),
            [SelectedExportScreens.AdminDepartments] = Required(("코드", "department-code")),
            [SelectedExportScreens.AdminCalendarHolidays] = Required(("날짜", "holiday-date"), ("휴일명", "holiday-name")),
            [SelectedExportScreens.AdminPermissions] = Required(("권한 코드", "permission-code")),
            [SelectedExportScreens.AdminMasterHistory] = Required(("대상 유형", "entity-type"), ("변경일시", "changed-at")),
            [SelectedExportScreens.AdminWorkHistory] = Required(("PJT Code", "project-code"), ("업무", "work-title")),
            [SelectedExportScreens.AdminNotificationDeliveries] = Required(("제목", "delivery-title"), ("생성일시", "created-at")),
            [SelectedExportScreens.AdminNotificationPreferenceAudit] = Required(
                ("변경일시", "occurred-at"), ("대상 사용자", "target-user"), ("변경 주체", "actor-user"),
                ("행동", "action"), ("알림 종류", "delivery-type"), ("변경 결과", "change")),
            [SelectedExportScreens.AdminWorkItemEscalations] = Required(("PJT Code", "project-code"), ("업무", "work-title"))
        };

    internal static IReadOnlyList<SelectedExportColumn> Describe(string screen, IEnumerable<string> labels)
    {
        if (!RequiredKeysByLabel.TryGetValue(screen, out var requiredKeys))
        {
            throw new InvalidOperationException($"Selected export column contract is missing for screen '{screen}'.");
        }

        var labelList = labels.ToList();
        if (labelList.Count == 0 || labelList.Count != labelList.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException($"Selected export columns must be non-empty and unique for screen '{screen}'.");
        }

        var missingRequired = requiredKeys.Keys.Except(labelList, StringComparer.Ordinal).ToArray();
        if (missingRequired.Length > 0)
        {
            throw new InvalidOperationException($"Selected export required columns are missing for screen '{screen}'.");
        }

        return labelList.Select(label => new SelectedExportColumn(
            requiredKeys.TryGetValue(label, out var requiredKey) ? requiredKey : StableKey(screen, label),
            label,
            requiredKeys.ContainsKey(label),
            screen == SelectedExportScreens.Projects && label is "매출액" or "통화"))
            .ToList();
    }

    internal static bool TryResolve(
        IReadOnlyList<SelectedExportColumn> effectiveColumns,
        IReadOnlyList<string?>? requestedKeys,
        out IReadOnlyList<SelectedExportColumn> selectedColumns)
    {
        if (requestedKeys is null)
        {
            selectedColumns = effectiveColumns;
            return true;
        }

        if (requestedKeys.Count == 0 || requestedKeys.Count > effectiveColumns.Count)
        {
            selectedColumns = [];
            return false;
        }

        var normalizedKeys = new List<string>(requestedKeys.Count);
        foreach (var key in requestedKeys)
        {
            if (key is null || !IsValidKey(key))
            {
                selectedColumns = [];
                return false;
            }

            normalizedKeys.Add(key);
        }

        if (normalizedKeys.Distinct(StringComparer.Ordinal).Count() != normalizedKeys.Count)
        {
            selectedColumns = [];
            return false;
        }

        var requested = normalizedKeys.ToHashSet(StringComparer.Ordinal);
        if (effectiveColumns.Any(column => column.Required && !requested.Contains(column.Key))
            || requested.Any(key => effectiveColumns.All(column => !string.Equals(column.Key, key, StringComparison.Ordinal))))
        {
            selectedColumns = [];
            return false;
        }

        selectedColumns = effectiveColumns.Where(column => requested.Contains(column.Key)).ToList();
        return selectedColumns.Count > 0;
    }

    internal static bool IsValidKey(string key)
    {
        if (key.Length == 0 || key.Length > 64 || key[0] == '-' || key[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in key)
        {
            if (character == '-')
            {
                if (previousHyphen)
                {
                    return false;
                }

                previousHyphen = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
            {
                return false;
            }

            previousHyphen = false;
        }

        return true;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> RequiredContract => RequiredKeysByLabel;

    private static IReadOnlyDictionary<string, string> Required(params (string Label, string Key)[] entries) =>
        entries.ToDictionary(entry => entry.Label, entry => entry.Key, StringComparer.Ordinal);

    private static string StableKey(string screen, string label)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{screen}\u001f{label}"));
        return $"column-{Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant()}";
    }
}
