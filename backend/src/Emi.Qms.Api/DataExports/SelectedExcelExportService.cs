using System.Security.Claims;
using Emi.Qms.Api.Admin;
using Emi.Qms.Api.Calendar;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Logistics;
using Emi.Qms.Api.Manufacturing;
using Emi.Qms.Api.Materials;
using Emi.Qms.Api.Notifications;
using Emi.Qms.Api.Pending;
using Emi.Qms.Api.Procurement;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.QualityInspections;
using Emi.Qms.Api.Workflow;

namespace Emi.Qms.Api.DataExports;

public static class SelectedExportScreens
{
    public const string Projects = "projects";
    public const string MyWork = "my-work";
    public const string ProductionPlanning = "production-planning";
    public const string Procurement = "procurement";
    public const string MaterialReceipts = "material-receipts";
    public const string MaterialKitting = "material-kitting";
    public const string Manufacturing = "manufacturing";
    public const string MaterialIqc = "material-iqc";
    public const string QualityInspections = "quality-inspections";
    public const string Logistics = "logistics";
    public const string Pending = "pending";
    public const string Notifications = "notifications";
    public const string AdminUsers = "admin-users";
    public const string AdminDepartments = "admin-departments";
    public const string AdminCalendarHolidays = "admin-calendar-holidays";
    public const string AdminPermissions = "admin-permissions";
    public const string AdminMasterHistory = "admin-master-history";
    public const string AdminWorkHistory = "admin-work-history";
    public const string AdminNotificationDeliveries = "admin-notification-deliveries";
    public const string AdminNotificationPreferenceAudit = "admin-notification-preference-audit";
    public const string AdminWorkItemEscalations = "admin-work-item-escalations";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Projects, MyWork, ProductionPlanning, Procurement, MaterialReceipts, MaterialKitting,
        Manufacturing, MaterialIqc, QualityInspections, Logistics, Pending, Notifications,
        AdminUsers, AdminDepartments, AdminCalendarHolidays, AdminPermissions,
        AdminMasterHistory, AdminWorkHistory, AdminNotificationDeliveries,
        AdminNotificationPreferenceAudit, AdminWorkItemEscalations
    };

    public static readonly IReadOnlyDictionary<string, string> AuditKinds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Projects] = "ProjectsSelected",
        [MyWork] = "MyWorkSelected",
        [ProductionPlanning] = "ProductionPlanningSelected",
        [Procurement] = "ProcurementDashboardSelected",
        [MaterialReceipts] = "MaterialReceiptsSelected",
        [MaterialKitting] = "PanelKittingSelected",
        [Manufacturing] = "ManufacturingSelected",
        [MaterialIqc] = "QualityIqcSelected",
        [QualityInspections] = "QualityInspectionsSelected",
        [Logistics] = "LogisticsSelected",
        [Pending] = "PendingSelected",
        [Notifications] = "NotificationsSelected",
        [AdminUsers] = "AdminUsersSelected",
        [AdminDepartments] = "AdminDepartmentsSelected",
        [AdminCalendarHolidays] = "AdminCalendarHolidaysSelected",
        [AdminPermissions] = "AdminPermissionMatrixSelected",
        [AdminMasterHistory] = "AdminMasterChangeLogsSelected",
        [AdminWorkHistory] = "AdminWorkHistorySelected",
        [AdminNotificationDeliveries] = "AdminNotificationDeliveriesSelected",
        [AdminNotificationPreferenceAudit] = "AdminNotificationPreferenceAuditSelected",
        [AdminWorkItemEscalations] = "AdminWorkItemEscalationsSelected"
    };

    public static bool RequiresAdminUsersRead(string screen) => screen is
        AdminUsers or AdminDepartments or AdminCalendarHolidays or
        AdminNotificationDeliveries or AdminNotificationPreferenceAudit or AdminWorkItemEscalations;

    public static bool RequiresAdminHistoryRead(string screen) => screen is
        AdminPermissions or AdminMasterHistory or AdminWorkHistory;
}

public sealed class SelectedExcelExportService(
    ExcelExportService legacyExportService,
    WorkflowStore workflowStore,
    ProductionPlanningStore productionPlanningStore,
    ProcurementStore procurementStore,
    MaterialsStore materialsStore,
    PanelKittingStore panelKittingStore,
    ManufacturingStore manufacturingStore,
    QualityInspectionStore qualityInspectionStore,
    LogisticsStore logisticsStore,
    PendingStore pendingStore,
    IUserAdministrationStore userAdministrationStore,
    AdminCalendarHolidayStore calendarHolidayStore,
    AdminMasterDataStore adminMasterDataStore,
    NotificationDeliveryStore notificationDeliveryStore,
    NotificationPreferenceAuditStore notificationPreferenceAuditStore,
    WorkItemEscalationStore workItemEscalationStore,
    ExcelWorkbookBuilder workbookBuilder,
    DataExportAuditStore auditStore,
    ExcelExportConcurrencyGate concurrencyGate,
    TimeProvider timeProvider)
{
    public const int MaximumSelectedRows = 1_000;

    internal IReadOnlyList<SelectedExportColumn> GetEffectiveColumns(string screen, ClaimsPrincipal user)
    {
        var labels = screen switch
        {
            SelectedExportScreens.Projects => ExcelExportService.ProjectColumns(
                ProjectEndpointExtensions.CanReadSalesAmount(user)).Select(column => column.Header),
            SelectedExportScreens.MyWork => MyWorkColumns.Select(column => column.Header),
            SelectedExportScreens.ProductionPlanning => ProductionPlanningColumns.Select(column => column.Header),
            SelectedExportScreens.Procurement => ProcurementColumns.Select(column => column.Header),
            SelectedExportScreens.MaterialReceipts => MaterialReceiptColumns.Select(column => column.Header),
            SelectedExportScreens.MaterialKitting => KittingColumns.Select(column => column.Header),
            SelectedExportScreens.Manufacturing => ManufacturingColumns.Select(column => column.Header),
            SelectedExportScreens.MaterialIqc => MaterialIqcColumns.Select(column => column.Header),
            SelectedExportScreens.QualityInspections => QualityColumns.Select(column => column.Header),
            SelectedExportScreens.Logistics => LogisticsColumns.Select(column => column.Header),
            SelectedExportScreens.Pending => PendingColumns.Select(column => column.Header),
            SelectedExportScreens.Notifications => NotificationColumns.Select(column => column.Header),
            SelectedExportScreens.AdminUsers => AdminUserColumns.Select(column => column.Header),
            SelectedExportScreens.AdminDepartments => AdminDepartmentColumns.Select(column => column.Header),
            SelectedExportScreens.AdminCalendarHolidays => CalendarHolidayColumns.Select(column => column.Header),
            SelectedExportScreens.AdminPermissions => PermissionColumns.Select(column => column.Header),
            SelectedExportScreens.AdminMasterHistory => AdminMasterHistoryColumns.Select(column => column.Header),
            SelectedExportScreens.AdminWorkHistory => AdminWorkHistoryColumns.Select(column => column.Header),
            SelectedExportScreens.AdminNotificationDeliveries => NotificationDeliveryColumns.Select(column => column.Header),
            SelectedExportScreens.AdminNotificationPreferenceAudit => NotificationPreferenceAuditColumns.Select(column => column.Header),
            SelectedExportScreens.AdminWorkItemEscalations => EscalationColumns.Select(column => column.Header),
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, "Unsupported selected export screen.")
        };

        return SelectedExportColumnRegistry.Describe(screen, labels);
    }

    internal bool TryResolveColumns(
        string screen,
        ClaimsPrincipal user,
        IReadOnlyList<string?>? requestedKeys,
        out IReadOnlyList<SelectedExportColumn> selectedColumns) =>
        SelectedExportColumnRegistry.TryResolve(GetEffectiveColumns(screen, user), requestedKeys, out selectedColumns);

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> GetColumnLabelsForContract() =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [SelectedExportScreens.Projects] = ExcelExportService.ProjectColumns(true).Select(column => column.Header).ToList(),
            [SelectedExportScreens.MyWork] = MyWorkColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.ProductionPlanning] = ProductionPlanningColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.Procurement] = ProcurementColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.MaterialReceipts] = MaterialReceiptColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.MaterialKitting] = KittingColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.Manufacturing] = ManufacturingColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.MaterialIqc] = MaterialIqcColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.QualityInspections] = QualityColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.Logistics] = LogisticsColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.Pending] = PendingColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.Notifications] = NotificationColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminUsers] = AdminUserColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminDepartments] = AdminDepartmentColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminCalendarHolidays] = CalendarHolidayColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminPermissions] = PermissionColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminMasterHistory] = AdminMasterHistoryColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminWorkHistory] = AdminWorkHistoryColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminNotificationDeliveries] = NotificationDeliveryColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminNotificationPreferenceAudit] = NotificationPreferenceAuditColumns.Select(column => column.Header).ToList(),
            [SelectedExportScreens.AdminWorkItemEscalations] = EscalationColumns.Select(column => column.Header).ToList()
        };

    internal async Task<ExcelExportResult> ExportAsync(
        string screen,
        IReadOnlyList<Guid> ids,
        IReadOnlyDictionary<string, string?> filters,
        Guid actorUserId,
        ClaimsPrincipal user,
        IReadOnlyList<SelectedExportColumn> selectedColumns,
        CancellationToken cancellationToken)
    {
        var scope = ProjectEndpointExtensions.GetProjectAccessScope(user);
        return screen switch
        {
            SelectedExportScreens.Projects => await legacyExportService.ExportSelectedProjectsAsync(
                actorUserId,
                ids,
                scope,
                ProjectEndpointExtensions.CanReadSalesAmount(user),
                selectedColumns,
                cancellationToken),
            SelectedExportScreens.MyWork => await ExportRowsAsync(
                actorUserId, ids, filters, "내 업무", "내업무", SelectedExportScreens.MyWork,
                () => workflowStore.GetMyWorkItemsAsync(actorUserId, Filter(filters, "status"), cancellationToken),
                response => response.Items, row => row.WorkItemId, FilterColumns(MyWorkColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.ProductionPlanning => await ExportRowsAsync(
                actorUserId, ids, filters, "생산계획", "생산계획", SelectedExportScreens.ProductionPlanning,
                () => productionPlanningStore.ListProjectsAsync(Filter(filters, "search"), cancellationToken),
                response => response.Projects, row => row.ProjectId, FilterColumns(ProductionPlanningColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.Procurement => await ExportRowsAsync(
                actorUserId, ids, filters, "구매", "구매", SelectedExportScreens.Procurement,
                () => procurementStore.GetProcurementDashboardAsync(
                    Filter(filters, "search"), DateFilter(filters, "expectedReceiptDateFrom"), DateFilter(filters, "expectedReceiptDateTo"),
                    scope, ExcelExportService.MaximumRows, cancellationToken),
                response => response.Projects, row => row.ProjectId, FilterColumns(ProcurementColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.MaterialReceipts => await ExportRowsAsync(
                actorUserId, ids, filters, "자재 입고", "자재입고", SelectedExportScreens.MaterialReceipts,
                () => materialsStore.ListAsync(
                    Filter(filters, "search"), BoolFilter(filters, "includeCompleted"), Filter(filters, "supplyType"),
                    DateFilter(filters, "expectedReceiptDateFrom"), DateFilter(filters, "expectedReceiptDateTo"), scope, cancellationToken),
                response => response.Items, row => row.ItemId, FilterColumns(MaterialReceiptColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.MaterialKitting => await ExportRowsAsync(
                actorUserId, ids, filters, "키팅", "키팅", SelectedExportScreens.MaterialKitting,
                () => panelKittingStore.ListAsync(scope, GuidFilter(filters, "projectId"), cancellationToken),
                response => response.Projects.SelectMany(project => project.Panels.Select(panel => new KittingExportRow(project, panel))).ToList(),
                row => row.Panel.PanelId, FilterColumns(KittingColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.Manufacturing => await ExportRowsAsync(
                actorUserId, ids, filters, "제조 작업", "제조", SelectedExportScreens.Manufacturing,
                () => manufacturingStore.ListAsync(
                    scope, actorUserId,
                    ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ManufacturingWorkTimeRead),
                    ProjectEndpointExtensions.HasPermission(user, QmsPermissions.ManufacturingUpdate),
                    GuidFilter(filters, "projectId"), cancellationToken),
                response => response.Projects.SelectMany(project => project.Panels.Select(panel => new ManufacturingExportRow(project, panel))).ToList(),
                row => row.Panel.PanelId, FilterColumns(ManufacturingColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.MaterialIqc => await ExportRowsAsync(
                actorUserId, ids, filters, "수입검사", "IQC", SelectedExportScreens.MaterialIqc,
                () => materialsStore.ListIqcAsync(BoolFilter(filters, "includeDecided"), scope, cancellationToken),
                response => response.Items, row => row.AttemptId, FilterColumns(MaterialIqcColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.QualityInspections => await ExportRowsAsync(
                actorUserId, ids, filters, "품질 검사", "품질", SelectedExportScreens.QualityInspections,
                () => qualityInspectionStore.ListAsync(
                    scope, actorUserId, ProjectEndpointExtensions.HasPermission(user, QmsPermissions.QualityInspect),
                    Filter(filters, "stage"), GuidFilter(filters, "projectId"), cancellationToken),
                response => response.Projects.SelectMany(project => project.Panels.Select(panel => new QualityExportRow(project, panel))).ToList(),
                row => row.Panel.PanelId, FilterColumns(QualityColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.Logistics => await ExportRowsAsync(
                actorUserId, ids, filters, "물류", "물류", SelectedExportScreens.Logistics,
                () => logisticsStore.ListAsync(
                    Filter(filters, "stage"), GuidFilter(filters, "projectId"), scope, actorUserId,
                    ProjectEndpointExtensions.HasPermission(user, QmsPermissions.LogisticsShip), cancellationToken),
                response => response.Projects.SelectMany(project => project.Items.Select(item => new LogisticsExportRow(response.Stage, project, item))).ToList(),
                row => row.Item.TargetId, FilterColumns(LogisticsColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.Pending => await ExportRowsAsync(
                actorUserId, ids, filters, "Pending", "Pending", SelectedExportScreens.Pending,
                () => pendingStore.ListAsync(
                    Filter(filters, "status"), Filter(filters, "issueType"), Filter(filters, "priority"),
                    GuidFilter(filters, "assigneeUserId"), GuidFilter(filters, "projectId"), cancellationToken),
                response => response.Items, row => row.PendingId, FilterColumns(PendingColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.Notifications => await ExportRowsAsync(
                actorUserId, ids, filters, "알림", "알림", SelectedExportScreens.Notifications,
                () => workflowStore.GetNotificationsAsync(actorUserId, Filter(filters, "readStatus"), cancellationToken),
                response => response.Items, row => row.NotificationId, FilterColumns(NotificationColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminUsers => await ExportRowsAsync(
                actorUserId, ids, filters, "사용자 관리", "사용자", SelectedExportScreens.AdminUsers,
                () => userAdministrationStore.GetSnapshotAsync(cancellationToken),
                response => response.Users, row => row.UserId, FilterColumns(AdminUserColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminDepartments => await ExportRowsAsync(
                actorUserId, ids, filters, "부서 관리", "부서", SelectedExportScreens.AdminDepartments,
                () => adminMasterDataStore.ListDepartmentsAsync(cancellationToken),
                response => response.Departments, row => row.DepartmentId, FilterColumns(AdminDepartmentColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminCalendarHolidays => await ExportRowsAsync(
                actorUserId, ids, filters, "휴일 관리", "휴일", SelectedExportScreens.AdminCalendarHolidays,
                () => calendarHolidayStore.ListAsync(IntFilter(filters, "year") ?? timeProvider.GetUtcNow().Year, "KR", cancellationToken),
                response => response.Holidays, row => row.HolidayId, FilterColumns(CalendarHolidayColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminPermissions => await ExportRowsAsync(
                actorUserId, ids, filters, "권한 매트릭스", "권한", SelectedExportScreens.AdminPermissions,
                () => adminMasterDataStore.GetPermissionMatrixAsync(cancellationToken),
                response => response.Permissions.Select(permission => new PermissionExportRow(
                    permission,
                    string.Join(", ", response.Assignments.Where(item => item.PermissionId == permission.PermissionId)
                        .Join(response.Roles, item => item.RoleId, role => role.RoleId, (_, role) => role.Name)))).ToList(),
                row => row.Permission.PermissionId, FilterColumns(PermissionColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminMasterHistory => await ExportRowsAsync(
                actorUserId, ids, filters, "기준정보 변경이력", "기준정보이력", SelectedExportScreens.AdminMasterHistory,
                () => adminMasterDataStore.ListChangeLogsAsync(
                    Filter(filters, "entityType"), GuidFilter(filters, "changedByUserId"),
                    DateTimeFilter(filters, "from"), DateTimeFilter(filters, "to"), cancellationToken),
                response => response.Items, row => row.ChangeLogId, FilterColumns(AdminMasterHistoryColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminWorkHistory => await ExportRowsAsync(
                actorUserId, ids, filters, "업무 이력", "업무이력", SelectedExportScreens.AdminWorkHistory,
                () => adminMasterDataStore.ListWorkItemHistoryAsync(
                    GuidFilter(filters, "projectId"), GuidFilter(filters, "userId"), Filter(filters, "stage"), Filter(filters, "status"),
                    DateTimeFilter(filters, "from"), DateTimeFilter(filters, "to"), cancellationToken),
                response => response.Items, row => row.WorkItemId, FilterColumns(AdminWorkHistoryColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminNotificationDeliveries => await ExportRowsAsync(
                actorUserId, ids, filters, "알림 전송 관리", "알림전송", SelectedExportScreens.AdminNotificationDeliveries,
                () => notificationDeliveryStore.ListDeliveriesAsync(
                    Filter(filters, "status"), Filter(filters, "channel"), Filter(filters, "deliveryType"), Filter(filters, "handlingStatus"), cancellationToken),
                response => response.Items, row => row.DeliveryId, FilterColumns(NotificationDeliveryColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminNotificationPreferenceAudit => await ExportRowsAsync(
                actorUserId, ids, filters, "알림 설정 변경 이력", "알림설정이력", SelectedExportScreens.AdminNotificationPreferenceAudit,
                () => notificationPreferenceAuditStore.ListByIdsAsync(ids, cancellationToken),
                response => response, row => row.AuditEventId, FilterColumns(NotificationPreferenceAuditColumns, selectedColumns), cancellationToken),
            SelectedExportScreens.AdminWorkItemEscalations => await ExportRowsAsync(
                actorUserId, ids, filters, "업무 에스컬레이션", "에스컬레이션", SelectedExportScreens.AdminWorkItemEscalations,
                () => workItemEscalationStore.ListEscalationsAsync(Filter(filters, "status"), Filter(filters, "level"), cancellationToken),
                response => response.Items, row => row.EscalationId, FilterColumns(EscalationColumns, selectedColumns), cancellationToken),
            _ => new ExcelExportResult(ExcelExportStatus.SelectionUnavailable)
        };
    }

    private async Task<ExcelExportResult> ExportRowsAsync<TResponse, TRow>(
        Guid actorUserId,
        IReadOnlyList<Guid> ids,
        IReadOnlyDictionary<string, string?> filters,
        string title,
        string screenName,
        string screen,
        Func<Task<TResponse>> load,
        Func<TResponse, IReadOnlyList<TRow>> rows,
        Func<TRow, Guid> key,
        IReadOnlyList<ExcelColumn<TRow>> columns,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!concurrencyGate.TryAcquire(out var lease))
        {
            return new ExcelExportResult(ExcelExportStatus.Busy);
        }

        using (lease)
        {
            var available = rows(await load());
            var byId = available.GroupBy(key).ToDictionary(group => group.Key, group => group.First());
            if (ids.Any(id => !byId.ContainsKey(id)))
            {
                return new ExcelExportResult(ExcelExportStatus.SelectionUnavailable);
            }

            var selected = ids.Select(id => byId[id]).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            var content = workbookBuilder.Build(title, screenName, $"현재 화면에서 선택한 {selected.Count}건", selected, columns);
            cancellationToken.ThrowIfCancellationRequested();
            await auditStore.AppendSuccessAsync(
                actorUserId, SelectedExportScreens.AuditKinds[screen], selected.Count,
                false, false, cancellationToken);

            var timestamp = timeProvider.GetUtcNow().ToLocalTime().ToString("yyyyMMdd_HHmmss");
            return new ExcelExportResult(
                ExcelExportStatus.Success,
                new ExcelExportFile(content, $"EMI_{screenName}_선택_{timestamp}.xlsx", selected.Count));
        }
    }

    private static IReadOnlyList<ExcelColumn<TRow>> FilterColumns<TRow>(
        IReadOnlyList<ExcelColumn<TRow>> availableColumns,
        IReadOnlyList<SelectedExportColumn> selectedColumns)
    {
        var selectedLabels = selectedColumns.Select(column => column.Label).ToHashSet(StringComparer.Ordinal);
        var filtered = availableColumns.Where(column => selectedLabels.Contains(column.Header)).ToList();
        if (filtered.Count == 0 || filtered.Count != selectedColumns.Count)
        {
            throw new InvalidOperationException("Selected export metadata and workbook columns are out of sync.");
        }

        return filtered;
    }

    private static string? Filter(IReadOnlyDictionary<string, string?> filters, string key) =>
        filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static bool BoolFilter(IReadOnlyDictionary<string, string?> filters, string key) =>
        bool.TryParse(Filter(filters, key), out var value) && value;

    private static Guid? GuidFilter(IReadOnlyDictionary<string, string?> filters, string key) =>
        Guid.TryParse(Filter(filters, key), out var value) ? value : null;

    private static int? IntFilter(IReadOnlyDictionary<string, string?> filters, string key) =>
        int.TryParse(Filter(filters, key), out var value) ? value : null;

    private static DateOnly? DateFilter(IReadOnlyDictionary<string, string?> filters, string key) =>
        DateOnly.TryParse(Filter(filters, key), out var value) ? value : null;

    private static DateTimeOffset? DateTimeFilter(IReadOnlyDictionary<string, string?> filters, string key) =>
        DateTimeOffset.TryParse(Filter(filters, key), out var value) ? value : null;

    private sealed record KittingExportRow(PanelKittingProjectResponse Project, PanelKittingPanelResponse Panel);
    private sealed record ManufacturingExportRow(ManufacturingProjectResponse Project, ManufacturingPanelResponse Panel);
    private sealed record QualityExportRow(QualityInspectionProjectResponse Project, QualityInspectionPanelSummary Panel);
    private sealed record LogisticsExportRow(string Stage, LogisticsProjectQueue Project, LogisticsQueueItem Item);
    private sealed record PermissionExportRow(PermissionMatrixPermissionResponse Permission, string Roles);

    private static readonly IReadOnlyList<ExcelColumn<MyWorkItemResponse>> MyWorkColumns =
    [
        new("업무", row => row.Title), new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle),
        new("단계", row => row.WorkflowStageName), new("담당", row => row.ResponsibilityLabel), new("상태", row => row.StatusLabel),
        new("우선순위", row => row.PriorityLabel), new("마감일", row => row.DueDate)
    ];

    private static readonly IReadOnlyList<ExcelColumn<ProductionPlanningProjectSummaryResponse>> ProductionPlanningColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("고객사", row => row.CustomerName),
        new("Item", row => row.Item), new("면수", row => row.ActivePanelCount), new("납기일", row => row.DeliveryDate),
        new("계획 상태", row => row.PlanStatusLabel), new("제품 유형", row => row.ProductTypeName),
        new("필수 공정", row => row.RequiredStepCount), new("계획 공정", row => row.PlannedRequiredStepCount), new("담당자", row => row.AssigneeCount)
    ];

    private static readonly IReadOnlyList<ExcelColumn<ProcurementProjectSummaryResponse>> ProcurementColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("고객사", row => row.CustomerName),
        new("Item", row => row.Item), new("면수", row => row.ActivePanelCount), new("납기일", row => row.DeliveryDate),
        new("구매품목", row => row.ProcurementItemCount), new("입고완료", row => row.ReceiptCompletedCount),
        new("입고지연", row => row.PastExpectedReceiptDateCount), new("최근 입고예정일", row => row.NearestExpectedReceiptDate), new("D-Day", row => row.DDayText)
    ];

    private static readonly IReadOnlyList<ExcelColumn<MaterialReceivingItemResponse>> MaterialReceiptColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("발주 품목", row => row.OrderItem),
        new("공급사", row => row.SupplierName), new("공급 유형", row => row.SupplyType), new("입고 예정일", row => row.ExpectedReceiptDate),
        new("발주 수량", row => row.OrderQuantity), new("단위", row => row.OrderUnit), new("입고 수량", row => row.ArrivedQuantity),
        new("확정 수량", row => row.ConfirmedQuantity), new("잔여 수량", row => row.RemainingQuantity), new("입고 완료", row => row.ReceiptCompleted)
    ];

    private static readonly IReadOnlyList<ExcelColumn<KittingExportRow>> KittingColumns =
    [
        new("PJT Code", row => row.Project.ProjectCode), new("PJT Title", row => row.Project.ProjectTitle),
        new("면 코드", row => row.Panel.DisplayCode), new("면 이름", row => row.Panel.PanelName),
        new("면 정보 완료", row => row.Panel.PanelInfoCompleted), new("키팅 완료", row => row.Panel.KittingCompleted),
        new("완료자", row => row.Panel.CompletedByDisplayName), new("완료일시", row => row.Panel.CompletedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<ManufacturingExportRow>> ManufacturingColumns =
    [
        new("PJT Code", row => row.Project.ProjectCode), new("PJT Title", row => row.Project.ProjectTitle),
        new("면 코드", row => row.Panel.DisplayCode), new("면 이름", row => row.Panel.PanelName), new("상태", row => row.Panel.Status),
        new("완료 공정", row => row.Panel.CheckedStepCount), new("전체 공정", row => row.Panel.TotalStepCount),
        new("시작일시", row => row.Panel.StartedAtUtc), new("완료일시", row => row.Panel.CompletedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<MaterialIqcQueueItemResponse>> MaterialIqcColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("품목", row => row.OrderItem),
        new("수량", row => row.Quantity), new("단위", row => row.Unit), new("차수", row => row.AttemptNumber),
        new("공급 유형", row => row.SupplyType), new("상태", row => row.Status), new("요청일시", row => row.RequestedAtUtc), new("판정일시", row => row.DecidedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<QualityExportRow>> QualityColumns =
    [
        new("PJT Code", row => row.Project.ProjectCode), new("PJT Title", row => row.Project.ProjectTitle),
        new("면 코드", row => row.Panel.DisplayCode), new("면 이름", row => row.Panel.PanelName),
        new("검사 단계", row => row.Panel.StageLabel), new("상태", row => row.Panel.Status), new("검사 차수", row => row.Panel.AttemptNumber),
        new("Pending 번호", row => row.Panel.PendingNumber)
    ];

    private static readonly IReadOnlyList<ExcelColumn<LogisticsExportRow>> LogisticsColumns =
    [
        new("단계", row => row.Stage), new("PJT Code", row => row.Project.ProjectCode), new("PJT Title", row => row.Project.ProjectTitle),
        new("대상 코드", row => row.Item.DisplayCode), new("제목", row => row.Item.Title),
        new("면 코드", row => string.Join(", ", row.Item.PanelCodes)), new("상태", row => row.Item.Status), new("Pending", row => row.Item.HasOpenPending)
    ];

    private static readonly IReadOnlyList<ExcelColumn<PendingListItemResponse>> PendingColumns =
    [
        new("번호", row => row.IssueNumber), new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle),
        new("유형", row => row.IssueTypeLabel), new("제목", row => row.Title), new("상태", row => row.StatusLabel),
        new("우선순위", row => row.PriorityLabel), new("담당부서", row => row.ActionDepartmentCode),
        new("담당자", row => row.AssigneeDisplayName), new("마감일", row => row.DueDate), new("지연", row => row.IsOverdue)
    ];

    private static readonly IReadOnlyList<ExcelColumn<NotificationResponse>> NotificationColumns =
    [
        new("제목", row => row.Title), new("유형", row => row.NotificationTypeLabel),
        new("중요도", row => row.SeverityLabel), new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle),
        new("업무", row => row.WorkItemTitle), new("단계", row => row.WorkflowStageName), new("생성일시", row => row.CreatedAtUtc),
        new("읽음일시", row => row.ReadAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<UserAdministrationUser>> AdminUserColumns =
    [
        new("이름", row => row.DisplayName), new("업무 이메일", row => row.Email), new("인증 방식", row => row.AuthProvider),
        new("부서", row => row.DepartmentName), new("역할", row => string.Join(", ", row.Roles)),
        new("활성", row => row.IsActive), new("승인 대기", row => row.ApprovalPending), new("읽기 전용", row => row.IsReadOnly)
    ];

    private static readonly IReadOnlyList<ExcelColumn<AdminDepartmentMasterResponse>> AdminDepartmentColumns =
    [
        new("코드", row => row.Code), new("부서명", row => row.Name), new("정렬", row => row.SortOrder),
        new("상태", row => row.LifecycleStatusLabel), new("활성", row => row.IsActive), new("사용자 수", row => row.UserCount), new("수정일시", row => row.UpdatedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<AdminCalendarHolidayResponse>> CalendarHolidayColumns =
    [
        new("날짜", row => row.Date), new("휴일명", row => row.Name), new("유형", row => row.HolidayType),
        new("국가", row => row.CountryCode), new("상태", row => row.LifecycleStatusLabel), new("활성", row => row.IsActive)
    ];

    private static readonly IReadOnlyList<ExcelColumn<PermissionExportRow>> PermissionColumns =
    [
        new("권한 코드", row => row.Permission.Code), new("권한명", row => row.Permission.Name), new("부여 역할", row => row.Roles)
    ];

    private static readonly IReadOnlyList<ExcelColumn<AdminMasterChangeLogResponse>> AdminMasterHistoryColumns =
    [
        new("대상 유형", row => row.EntityType), new("작업", row => row.Action),
        new("변경자", row => row.ChangedByDisplayName), new("변경일시", row => row.ChangedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<AdminWorkItemHistoryResponse>> AdminWorkHistoryColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("업무", row => row.Title),
        new("단계", row => row.WorkflowStageName), new("상태", row => row.Status), new("담당자", row => row.AssignedDisplayName),
        new("마감일", row => row.DueDate), new("시작일시", row => row.StartedAtUtc), new("완료일시", row => row.CompletedAtUtc),
        new("수정일시", row => row.UpdatedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<NotificationDeliveryResponse>> NotificationDeliveryColumns =
    [
        new("제목", row => row.DisplayTitle), new("프로젝트", row => row.DisplayProject),
        new("채널", row => row.ChannelLabel), new("유형", row => row.DeliveryTypeLabel), new("상태", row => row.StatusLabel),
        new("시도 횟수", row => row.AttemptCount), new("처리 상태", row => row.AdminHandlingStatusLabel),
        new("다음 시도", row => row.NextAttemptAtUtc), new("발송일시", row => row.SentAtUtc), new("생성일시", row => row.CreatedAtUtc)
    ];

    private static readonly IReadOnlyList<ExcelColumn<NotificationPreferenceAuditItemResponse>> NotificationPreferenceAuditColumns =
    [
        new("변경일시", row => row.OccurredAtUtc), new("대상 사용자", row => row.TargetDisplayName),
        new("변경 주체", row => row.ActorDisplayName), new("행동", row => row.ActionLabel),
        new("알림 종류", row => row.DeliveryTypeLabel), new("변경 결과", row => row.ChangeLabel),
        new("채널", row => row.ChannelLabel), new("변경 전", row => row.OldValue ? "켬" : "끔"),
        new("변경 후", row => row.NewValue ? "켬" : "끔"), new("결과 버전", row => row.ResultingVersion),
        new("대상 부서(현재)", row => row.TargetDepartmentName)
    ];

    private static readonly IReadOnlyList<ExcelColumn<WorkItemEscalationResponse>> EscalationColumns =
    [
        new("PJT Code", row => row.ProjectCode), new("PJT Title", row => row.ProjectTitle), new("업무", row => row.WorkItemTitle),
        new("단계", row => row.WorkflowStageName), new("담당자", row => row.AssignedDisplayName), new("마감일", row => row.DueDate),
        new("상태", row => row.Status), new("현재 레벨", row => row.CurrentLevel), new("최근 에스컬레이션", row => row.LastEscalatedAtUtc),
        new("다음 확인", row => row.NextCheckAtUtc), new("전송 요약", row => row.DeliveryStatusSummary)
    ];
}
