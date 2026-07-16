namespace Emi.Qms.Api.Projects;

public static class ProjectBottleneckDomain
{
    private static readonly IReadOnlyList<PanelStageDefinition> PanelStages =
    [
        new("BeforeManufacturing", "제조 전", 5),
        new("ManufacturingInProgress", "제조 중", 9),
        new("ManufacturingCompleted", "제조 완료", 11),
        new("InspectionInProgress", "검사 중", 12),
        new("InspectionCompleted", "검사 완료", 14),
        new("PackingCompleted", "포장 완료", 15),
        new("ShipmentCompleted", "납품 완료", 17)
    ];

    private static readonly IReadOnlyDictionary<string, (string Label, int Rank)> ProjectStages =
        new Dictionary<string, (string Label, int Rank)>(StringComparer.Ordinal)
        {
            ["SalesProjectCreated"] = ("프로젝트 생성", 1),
            ["ProductionPlanning"] = ("생산관리", 2),
            ["DesignPanelInfo"] = ("설계", 3),
            ["ProcurementInfo"] = ("구매정보", 4)
        };

    public static ProjectBottleneckResponse Build(
        string projectStatus,
        string projectWorkStatus,
        IReadOnlyList<int> panelStageCounts,
        int unknownPanelStageCount,
        PendingBottleneckCounts? pending)
    {
        var normalizedCounts = PanelStages
            .Select((stage, index) => new PanelStageDistributionResponse(
                stage.Code,
                stage.Label,
                stage.Rank,
                index < panelStageCounts.Count ? panelStageCounts[index] : 0,
                false))
            .ToList();

        var openPendingCount = pending?.OpenCount;
        var reinspectionPendingCount = pending?.ReinspectionCount;
        var urgentPendingCount = pending?.UrgentCount;

        if (string.Equals(projectStatus, "Completed", StringComparison.Ordinal))
        {
            return Response("Completed", "병목 없음 · 프로젝트 완료", null, null, null, 99, "None", "확인할 병목이 없습니다.", "lifecycle", normalizedCounts, pending);
        }

        if (string.Equals(projectStatus, "Cancelled", StringComparison.Ordinal))
        {
            return Response("Inactive", "프로젝트 취소", null, null, null, 98, "None", "취소된 프로젝트입니다.", "lifecycle", normalizedCounts, pending);
        }

        if (string.Equals(projectStatus, "OnHold", StringComparison.Ordinal))
        {
            return Response("Inactive", "프로젝트 보류", null, null, null, 97, "Workflow", "보류 사유와 현재 단계를 확인하세요.", "lifecycle", normalizedCounts, pending);
        }

        if (ProjectStages.TryGetValue(projectWorkStatus, out var projectStage))
        {
            var nextAction = openPendingCount > 0 ? "Pending" : "Workflow";
            var nextLabel = openPendingCount > 0
                ? $"open Pending {openPendingCount}건을 먼저 확인하세요."
                : $"{projectStage.Label} 단계 진행 상태를 확인하세요.";
            return Response(
                "ProjectStage",
                $"{projectStage.Label} 단계",
                projectWorkStatus,
                projectStage.Label,
                null,
                projectStage.Rank,
                nextAction,
                nextLabel,
                openPendingCount > 0 ? "open-pending" : "stage",
                normalizedCounts,
                pending);
        }

        if (unknownPanelStageCount > 0)
        {
            return Response(
                "Uncertain",
                "일부 계산 불가",
                null,
                null,
                null,
                96,
                openPendingCount > 0 ? "Pending" : "Panels",
                "알 수 없는 패널 구간이 있어 원본 상태를 확인하세요.",
                openPendingCount > 0 ? "open-pending" : "uncertain",
                normalizedCounts,
                pending);
        }

        var bottleneckIndex = normalizedCounts.FindIndex(item => item.PanelCount > 0);
        if (bottleneckIndex < 0)
        {
            return Response(
                "NoData",
                "활성 패널 없음",
                null,
                null,
                null,
                95,
                openPendingCount > 0 ? "Pending" : "Workflow",
                openPendingCount > 0 ? $"open Pending {openPendingCount}건을 먼저 확인하세요." : "패널 구성과 workflow 원본을 확인하세요.",
                openPendingCount > 0 ? "open-pending" : "no-data",
                normalizedCounts,
                pending);
        }

        var bottleneck = normalizedCounts[bottleneckIndex];
        normalizedCounts[bottleneckIndex] = bottleneck with { IsBottleneck = true };
        var action = openPendingCount > 0 ? "Pending" : "Panels";
        var actionLabel = openPendingCount > 0
            ? $"open Pending {openPendingCount}건을 먼저 확인하세요."
            : $"{bottleneck.StageLabel} 구간의 패널 {bottleneck.PanelCount}면을 확인하세요.";

        return Response(
            "PanelStage",
            $"{bottleneck.StageLabel} · {bottleneck.PanelCount}면",
            bottleneck.StageCode,
            bottleneck.StageLabel,
            bottleneck.PanelCount,
            bottleneck.StageRank,
            action,
            actionLabel,
            openPendingCount > 0 ? "open-pending" : "stage",
            normalizedCounts,
            pending);
    }

    private static ProjectBottleneckResponse Response(
        string kind,
        string label,
        string? stageCode,
        string? stageLabel,
        int? panelCount,
        int stageRank,
        string nextAction,
        string nextActionLabel,
        string sortReason,
        IReadOnlyList<PanelStageDistributionResponse> distribution,
        PendingBottleneckCounts? pending)
    {
        return new ProjectBottleneckResponse(
            kind,
            label,
            stageCode,
            stageLabel,
            panelCount,
            stageRank,
            nextAction,
            nextActionLabel,
            sortReason,
            pending?.OpenCount,
            pending?.ReinspectionCount,
            pending?.UrgentCount,
            distribution);
    }

    private sealed record PanelStageDefinition(string Code, string Label, int Rank);
}

public sealed record PendingBottleneckCounts(int OpenCount, int ReinspectionCount, int UrgentCount);
