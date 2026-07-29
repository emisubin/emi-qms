using Emi.Qms.Api.Projects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class ProjectBottleneckDomainTests
{
    [Fact]
    public void Build_UsesOpenPendingAsNextAttentionWithoutReplacingProjectStage()
    {
        var result = ProjectBottleneckDomain.Build(
            "Active",
            "ProductionPlanning",
            [3, 0, 0, 0, 0, 0, 0],
            0,
            new PendingBottleneckCounts(2, 1, 1));

        Assert.Equal("ProjectStage", result.Kind);
        Assert.Equal("생산관리 단계", result.Label);
        Assert.Equal("ProductionPlanning", result.StageCode);
        Assert.Equal("Pending", result.NextAction);
        Assert.Equal("open-pending", result.SortReason);
        Assert.Equal(2, result.OpenPendingCount);
        Assert.Equal(1, result.ReinspectionPendingCount);
        Assert.Equal(1, result.UrgentPendingCount);
    }

    [Fact]
    public void Build_SelectsEarliestPanelStageAndKeepsTheFullDistribution()
    {
        var result = ProjectBottleneckDomain.Build(
            "Active",
            "MaterialArrived",
            [0, 2, 2, 1, 0, 0, 0],
            0,
            null);

        Assert.Equal("PanelStage", result.Kind);
        Assert.Equal("제조 중 · 2면", result.Label);
        Assert.Equal("ManufacturingInProgress", result.StageCode);
        Assert.Equal(2, result.PanelCount);
        Assert.Equal("Panels", result.NextAction);
        Assert.Equal(7, result.PanelDistribution.Count);
        Assert.Single(result.PanelDistribution, stage => stage.IsBottleneck);
        Assert.True(result.PanelDistribution[1].IsBottleneck);
        Assert.Null(result.OpenPendingCount);
    }

    [Theory]
    [InlineData("Completed", "Completed", "병목 없음 · 프로젝트 완료", "None")]
    [InlineData("Cancelled", "Inactive", "프로젝트 취소", "None")]
    [InlineData("OnHold", "Inactive", "프로젝트 보류", "Workflow")]
    public void Build_UsesLifecycleStateBeforePanelProgress(
        string projectStatus,
        string expectedKind,
        string expectedLabel,
        string expectedAction)
    {
        var result = ProjectBottleneckDomain.Build(
            projectStatus,
            "MaterialArrived",
            [4, 0, 0, 0, 0, 0, 0],
            0,
            null);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedLabel, result.Label);
        Assert.Equal(expectedAction, result.NextAction);
    }

    [Fact]
    public void Build_ReportsUncertainWhenAnActivePanelHasAnUnknownStage()
    {
        var result = ProjectBottleneckDomain.Build(
            "Active",
            "MaterialArrived",
            [1, 0, 0, 0, 0, 0, 0],
            1,
            new PendingBottleneckCounts(0, 0, 0));

        Assert.Equal("Uncertain", result.Kind);
        Assert.Equal("일부 계산 불가", result.Label);
        Assert.Equal("Panels", result.NextAction);
        Assert.Equal("uncertain", result.SortReason);
    }
}
