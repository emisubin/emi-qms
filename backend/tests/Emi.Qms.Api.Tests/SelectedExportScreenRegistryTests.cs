using Emi.Qms.Api.DataExports;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class SelectedExportScreenRegistryTests
{
    [Fact]
    public void Registry_CoversTwentyTwoUniqueSelectedExportScreens()
    {
        Assert.Equal(22, SelectedExportScreens.All.Count);
        Assert.Contains(SelectedExportScreens.Projects, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.QualityInspections, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AdminUsers, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AdminNotificationPreferenceAudit, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AdminWorkItemEscalations, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AuditLedger, SelectedExportScreens.All);
        Assert.Equal(SelectedExportScreens.All.Count, SelectedExportScreens.AuditKinds.Count);
        Assert.True(SelectedExportScreens.All.SetEquals(SelectedExportScreens.AuditKinds.Keys));
    }

    [Fact]
    public void Registry_SeparatesAdminPolicyFamilies()
    {
        Assert.True(SelectedExportScreens.RequiresAdminUsersRead(SelectedExportScreens.AdminUsers));
        Assert.True(SelectedExportScreens.RequiresAdminUsersRead(SelectedExportScreens.AdminNotificationPreferenceAudit));
        Assert.True(SelectedExportScreens.RequiresAdminHistoryRead(SelectedExportScreens.AdminMasterHistory));
        Assert.False(SelectedExportScreens.RequiresAdminUsersRead(SelectedExportScreens.Projects));
        Assert.False(SelectedExportScreens.RequiresAdminHistoryRead(SelectedExportScreens.Notifications));
    }

    [Fact]
    public void Registry_UsesCanonicalSelectedAuditKinds()
    {
        Assert.Equal("ProcurementDashboardSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.Procurement]);
        Assert.Equal("PanelKittingSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.MaterialKitting]);
        Assert.Equal("QualityIqcSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.MaterialIqc]);
        Assert.Equal("AdminPermissionMatrixSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.AdminPermissions]);
        Assert.Equal("AdminMasterChangeLogsSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.AdminMasterHistory]);
        Assert.Equal("AuditLedgerSelected", SelectedExportScreens.AuditKinds[SelectedExportScreens.AuditLedger]);
    }

    [Fact]
    public void ColumnRegistry_CoversEveryScreenWithStableRequiredKeys()
    {
        var labelsByScreen = SelectedExcelExportService.GetColumnLabelsForContract();

        Assert.Equal(SelectedExportScreens.All.Count, labelsByScreen.Count);
        Assert.Equal(SelectedExportScreens.All.Count, SelectedExportColumnRegistry.RequiredContract.Count);
        foreach (var screen in SelectedExportScreens.All)
        {
            var columns = SelectedExportColumnRegistry.Describe(screen, labelsByScreen[screen]);
            Assert.NotEmpty(columns);
            Assert.Equal(columns.Count, columns.Select(column => column.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.All(columns, column => Assert.True(SelectedExportColumnRegistry.IsValidKey(column.Key)));
            Assert.Contains(columns, column => column.Required);
            Assert.All(
                SelectedExportColumnRegistry.RequiredContract[screen],
                required => Assert.Contains(columns, column =>
                    column.Required && column.Label == required.Key && column.Key == required.Value));
        }
    }

    [Fact]
    public void ColumnRegistry_ResolvesInServerOrderAndRejectsInvalidSubsets()
    {
        var columns = SelectedExportColumnRegistry.Describe(
            SelectedExportScreens.MaterialReceipts,
            SelectedExcelExportService.GetColumnLabelsForContract()[SelectedExportScreens.MaterialReceipts]);
        var requiredKeys = columns.Where(column => column.Required).Select(column => column.Key).ToList();
        var optionalKey = columns.First(column => !column.Required).Key;

        Assert.True(SelectedExportColumnRegistry.TryResolve(
            columns,
            [optionalKey, .. requiredKeys.AsEnumerable().Reverse()],
            out var selected));
        Assert.Equal(
            columns.Where(column => requiredKeys.Contains(column.Key) || column.Key == optionalKey).Select(column => column.Key),
            selected.Select(column => column.Key));
        Assert.False(SelectedExportColumnRegistry.TryResolve(columns, [], out _));
        Assert.False(SelectedExportColumnRegistry.TryResolve(columns, [requiredKeys[0]], out _));
        Assert.False(SelectedExportColumnRegistry.TryResolve(columns, [.. requiredKeys, "UNKNOWN"], out _));
        Assert.False(SelectedExportColumnRegistry.TryResolve(columns, [.. requiredKeys, requiredKeys[0]], out _));
    }
}
