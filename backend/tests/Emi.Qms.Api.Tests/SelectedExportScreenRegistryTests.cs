using Emi.Qms.Api.DataExports;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class SelectedExportScreenRegistryTests
{
    [Fact]
    public void Registry_CoversTwentyUniqueSelectedExportScreens()
    {
        Assert.Equal(20, SelectedExportScreens.All.Count);
        Assert.Contains(SelectedExportScreens.Projects, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.QualityInspections, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AdminUsers, SelectedExportScreens.All);
        Assert.Contains(SelectedExportScreens.AdminWorkItemEscalations, SelectedExportScreens.All);
        Assert.Equal(SelectedExportScreens.All.Count, SelectedExportScreens.AuditKinds.Count);
        Assert.True(SelectedExportScreens.All.SetEquals(SelectedExportScreens.AuditKinds.Keys));
    }

    [Fact]
    public void Registry_SeparatesAdminPolicyFamilies()
    {
        Assert.True(SelectedExportScreens.RequiresAdminUsersRead(SelectedExportScreens.AdminUsers));
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
    }
}
