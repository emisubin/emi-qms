using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;

namespace Emi.Qms.Api.DataExports;

public enum ExcelExportStatus
{
    Success,
    TooManyRows,
    Busy
}

public sealed record ExcelExportFile(byte[] Content, string FileName, int RowCount);

public sealed record ExcelExportResult(ExcelExportStatus Status, ExcelExportFile? File = null);

public sealed class ExcelExportService(
    ProjectStore projectStore,
    ProcurementStore procurementStore,
    WorkflowStore workflowStore,
    ExcelWorkbookBuilder workbookBuilder,
    DataExportAuditStore auditStore,
    ExcelExportConcurrencyGate concurrencyGate,
    TimeProvider timeProvider)
{
    public const int MaximumRows = 10_000;
    private const int ReadRows = MaximumRows + 1;

    public async Task<ExcelExportResult> ExportProjectsAsync(
        Guid actorUserId,
        ProjectListQuery query,
        ProjectAccessScope accessScope,
        bool includeSalesAmount,
        bool filtersApplied,
        string filterSummary,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!concurrencyGate.TryAcquire(out var lease))
        {
            return new ExcelExportResult(ExcelExportStatus.Busy);
        }

        using (lease)
        {
            var response = await projectStore.ListProjectsForExportAsync(
                query,
                accessScope,
                includeSalesAmount,
                ReadRows,
                cancellationToken);
            if (response.TotalCount > MaximumRows || response.Items.Count > MaximumRows)
            {
                return new ExcelExportResult(ExcelExportStatus.TooManyRows);
            }

            var columns = ProjectColumns(includeSalesAmount);
            cancellationToken.ThrowIfCancellationRequested();
            var content = workbookBuilder.Build(
                "프로젝트 목록",
                "프로젝트",
                filterSummary,
                response.Items,
                columns);
            cancellationToken.ThrowIfCancellationRequested();
            await auditStore.AppendSuccessAsync(
                actorUserId,
                "Projects",
                response.Items.Count,
                filtersApplied,
                includeSalesAmount,
                cancellationToken);

            return Success(content, "프로젝트", response.Items.Count);
        }
    }

    public async Task<ExcelExportResult> ExportProcurementDashboardAsync(
        Guid actorUserId,
        string? search,
        DateOnly? expectedReceiptDateFrom,
        DateOnly? expectedReceiptDateTo,
        ProjectAccessScope accessScope,
        bool filtersApplied,
        string filterSummary,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!concurrencyGate.TryAcquire(out var lease))
        {
            return new ExcelExportResult(ExcelExportStatus.Busy);
        }

        using (lease)
        {
            var response = await procurementStore.GetProcurementDashboardAsync(
                search,
                expectedReceiptDateFrom,
                expectedReceiptDateTo,
                accessScope,
                MaximumRows,
                cancellationToken);
            if (response.Truncated)
            {
                return new ExcelExportResult(ExcelExportStatus.TooManyRows);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var content = workbookBuilder.Build(
                "구매 프로젝트 요약",
                "구매",
                filterSummary,
                response.Projects,
                ProcurementColumns);
            cancellationToken.ThrowIfCancellationRequested();
            await auditStore.AppendSuccessAsync(
                actorUserId,
                "ProcurementDashboard",
                response.Projects.Count,
                filtersApplied,
                false,
                cancellationToken);

            return Success(content, "구매", response.Projects.Count);
        }
    }

    public async Task<ExcelExportResult> ExportMyWorkAsync(
        Guid actorUserId,
        string? status,
        bool filtersApplied,
        string filterSummary,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!concurrencyGate.TryAcquire(out var lease))
        {
            return new ExcelExportResult(ExcelExportStatus.Busy);
        }

        using (lease)
        {
            var response = await workflowStore.GetMyWorkItemsForExportAsync(
                actorUserId,
                status,
                ReadRows,
                cancellationToken);
            if (response.Items.Count > MaximumRows)
            {
                return new ExcelExportResult(ExcelExportStatus.TooManyRows);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var content = workbookBuilder.Build(
                "내 업무",
                "내 업무",
                filterSummary,
                response.Items,
                MyWorkColumns);
            cancellationToken.ThrowIfCancellationRequested();
            await auditStore.AppendSuccessAsync(
                actorUserId,
                "MyWork",
                response.Items.Count,
                filtersApplied,
                false,
                cancellationToken);

            return Success(content, "내업무", response.Items.Count);
        }
    }

    private ExcelExportResult Success(byte[] content, string screenName, int rowCount)
    {
        var timestamp = timeProvider.GetUtcNow().ToLocalTime().ToString("yyyyMMdd_HHmmss");
        return new ExcelExportResult(
            ExcelExportStatus.Success,
            new ExcelExportFile(content, $"EMI_{screenName}_{timestamp}.xlsx", rowCount));
    }

    private static IReadOnlyList<ExcelColumn<ProjectListItemResponse>> ProjectColumns(bool includeSalesAmount)
    {
        var columns = new List<ExcelColumn<ProjectListItemResponse>>
        {
            new("PJT Code", row => row.ProjectCode),
            new("PJT Title", row => row.ProjectTitle),
            new("고객사", row => row.CustomerName),
            new("Item", row => row.Item),
            new("상태", row => ProjectStatusLabel(row.Status)),
            new("진행 단계", row => ProjectWorkStatusLabel(row.ProjectWorkStatus)),
            new("진행률(%)", row => row.ProjectProgressPercent),
            new("면수", row => row.ActivePanelCount),
            new("납기일", row => row.DeliveryDate),
            new("영업담당", row => row.SalesOwnerName),
            new("포장방법", row => PackagingMethodLabel(row.PackagingMethod)),
            new("납품장소", row => row.DeliveryLocation),
            new("FAT", row => row.FatRequired ? "필요" : "미필요")
        };

        if (includeSalesAmount)
        {
            columns.Add(new ExcelColumn<ProjectListItemResponse>("매출액", row => row.SalesAmount));
            columns.Add(new ExcelColumn<ProjectListItemResponse>("통화", row => row.CurrencyCode));
        }

        return columns;
    }

    private static readonly IReadOnlyList<ExcelColumn<ProcurementProjectSummaryResponse>> ProcurementColumns =
    [
        new("PJT Code", row => row.ProjectCode),
        new("PJT Title", row => row.ProjectTitle),
        new("고객사", row => row.CustomerName),
        new("Item", row => row.Item),
        new("면수", row => row.ActivePanelCount),
        new("납기일", row => row.DeliveryDate),
        new("구매품목", row => row.ProcurementItemCount),
        new("입고완료", row => row.ReceiptCompletedCount),
        new("입고지연", row => row.PastExpectedReceiptDateCount),
        new("가장 가까운 입고예정일", row => row.NearestExpectedReceiptDate),
        new("입고 D-Day", row => row.DDayText)
    ];

    private static readonly IReadOnlyList<ExcelColumn<MyWorkItemResponse>> MyWorkColumns =
    [
        new("업무 제목", row => row.Title),
        new("PJT Code", row => row.ProjectCode),
        new("PJT Title", row => row.ProjectTitle),
        new("Item", row => row.ProjectItem),
        new("단계", row => row.WorkflowStageName),
        new("담당 유형", row => row.ResponsibilityLabel),
        new("상태", row => row.StatusLabel),
        new("우선순위", row => row.PriorityLabel),
        new("납기일", row => row.DueDate),
        new("생성일시", row => row.CreatedAtUtc),
        new("완료일시", row => row.CompletedAtUtc)
    ];

    private static string ProjectStatusLabel(string status)
    {
        return status switch
        {
            "Active" => "진행 중",
            "OnHold" => "보류",
            "Completed" => "완료",
            "Cancelled" => "취소",
            _ => status
        };
    }

    private static string ProjectWorkStatusLabel(string status)
    {
        return status switch
        {
            "SalesProjectCreated" => "프로젝트 생성",
            "ProductionPlanning" => "생산계획",
            "DesignPanelInfo" => "설계·Panel 정보",
            "ProcurementInfo" => "구매정보",
            "MaterialArrived" => "자재 입고",
            "OnHold" => "보류",
            "Completed" => "완료",
            "Cancelled" => "취소",
            _ => status
        };
    }

    private static string PackagingMethodLabel(string? method)
    {
        return method switch
        {
            "WoodenCrate" => "목포장",
            "StretchWrap" => "청랩포장",
            "HeavyDutyBox" => "고강도박스포장",
            null => "-",
            _ => method
        };
    }
}
