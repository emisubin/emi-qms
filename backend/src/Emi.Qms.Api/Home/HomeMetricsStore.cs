using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Npgsql;

namespace Emi.Qms.Api.Home;

public sealed class HomeMetricsStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<HomeMetricsResponse> GetAsync(
        string? departmentCode,
        string? departmentName,
        Guid effectiveUserId,
        IReadOnlySet<string> permissions,
        bool isSystemAdministrator,
        ProjectAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (isSystemAdministrator || string.Equals(departmentCode, "administration", StringComparison.Ordinal))
        {
            return new HomeMetricsResponse(departmentCode, departmentName,
                await ReadAdministrationAsync(cancellationToken));
        }

        var metrics = departmentCode switch
        {
            "sales" when Has(permissions, QmsPermissions.ProjectRead) =>
                await ReadSalesAsync(effectiveUserId, accessScope, cancellationToken),
            "design" when Has(permissions, QmsPermissions.PanelInfoUpdate) =>
                await ReadDesignAsync(accessScope, cancellationToken),
            "production-planning" when Has(permissions, QmsPermissions.ProductionPlanUpdate) =>
                await ReadProductionPlanningAsync(accessScope, cancellationToken),
            "procurement" when Has(permissions, QmsPermissions.ProcurementPlanUpdate) =>
                await ReadProcurementAsync(accessScope, cancellationToken),
            "materials" when Has(permissions, QmsPermissions.MaterialReceiptUpdate) =>
                await ReadMaterialsAsync(accessScope, cancellationToken),
            "manufacturing" when Has(permissions, QmsPermissions.ManufacturingUpdate) =>
                await ReadManufacturingAsync(accessScope, cancellationToken),
            "quality" when Has(permissions, QmsPermissions.QualityInspect) =>
                await ReadQualityAsync(accessScope, cancellationToken),
            "logistics" when Has(permissions, QmsPermissions.LogisticsShip) =>
                await ReadLogisticsAsync(accessScope, cancellationToken),
            _ => []
        };

        return new HomeMetricsResponse(departmentCode, departmentName, metrics);
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadAdministrationAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            select
                (select count(*)::int from qms_users u where u.auth_provider='EntraId' and u.is_active
                    and not exists(select 1 from user_roles ur where ur.user_id=u.id)),
                (select count(*)::int from notification_deliveries where status='Failed'
                    and coalesce(admin_handling_status,'Open')='Open'),
                (select count(*)::int from work_item_escalations where status='Active');
            """;
        var counts = await ReadCountsAsync(sql, null, null, cancellationToken);
        return
        [
            Metric("admin-approval", "사용자 승인 대기", counts[0], "warning", "admin-users", "사용자 관리 열기"),
            Metric("admin-delivery-failed", "알림 발송 실패", counts[1], "danger", "admin-deliveries", "발송 관리 열기"),
            Metric("admin-escalation", "활성 에스컬레이션", counts[2], "danger", "admin-dashboard", "운영 현황 열기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadSalesAsync(
        Guid userId,
        ProjectAccessScope scope,
        CancellationToken cancellationToken)
    {
        const string sql = """
            with visible as (
                select p.id,p.delivery_date,p.status,p.sales_owner_user_id
                from projects p where p.deleted_at_utc is null
                  and (@has_read_all or p.project_key=any(@project_keys))
            )
            select
                count(*) filter(where status='Active' and sales_owner_user_id=@user_id)::int,
                count(*) filter(where status='Active' and sales_owner_user_id=@user_id
                    and delivery_date between current_date and current_date + 14)::int,
                (select count(*)::int from work_items w join visible v on v.id=w.project_id
                    where w.workflow_stage_code='SalesSettlementCompleted' and w.status in ('Requested','InProgress'))
            from visible;
            """;
        var counts = await ReadCountsAsync(sql, scope, userId, cancellationToken);
        return
        [
            Metric("sales-active", "담당 진행 프로젝트", counts[0], "neutral", "projects", "프로젝트 열기"),
            Metric("sales-due", "14일 내 납기", counts[1], "warning", "projects", "납기 확인"),
            Metric("sales-settlement", "정산 대기", counts[2], "danger", "projects", "정산 대상 열기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadDesignAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            with visible as (
                select p.id from projects p where p.deleted_at_utc is null and p.status='Active'
                  and (@has_read_all or p.project_key=any(@project_keys))
            ), incomplete as (
                select pp.project_id,pp.id from panel_placeholders pp join visible v on v.id=pp.project_id
                where pp.status='Active' and not pp.panel_info_completed
            )
            select
                (select count(distinct project_id)::int from incomplete),
                (select count(*)::int from incomplete),
                (select count(*)::int from work_items w join visible v on v.id=w.project_id
                    where w.workflow_stage_code='PanelInfoCompleted' and w.status in ('Requested','InProgress'));
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("design-projects", "설계정보 미완료 프로젝트", counts[0], "warning", "projects", "프로젝트 열기"),
            Metric("design-panels", "패널정보 미완료", counts[1], "danger", "projects", "패널정보 입력"),
            Metric("design-progress", "설계 진행 업무", counts[2], "neutral", "my-work", "내 업무 열기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadProductionPlanningAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            with visible as (
                select p.id from projects p where p.deleted_at_utc is null and p.status='Active'
                  and (@has_read_all or p.project_key=any(@project_keys))
            ), plan_steps as (
                select v.id,pp.product_type_id,
                    count(pi.id) filter(where pi.is_required)::int required_count,
                    count(pi.id) filter(where pi.is_required and pi.planned_date is not null)::int planned_count
                from visible v left join project_production_plans pp on pp.project_id=v.id
                left join project_production_plan_items pi on pi.production_plan_id=pp.id
                group by v.id,pp.product_type_id
            ), assignees as (
                select v.id,count(pa.id) filter(where pa.assigned_user_id is not null)::int assigned_count
                from visible v left join project_assignees pa on pa.project_id=v.id group by v.id
            )
            select
                count(*) filter(where ps.product_type_id is null)::int,
                count(*) filter(where ps.product_type_id is not null and (ps.required_count=0 or ps.planned_count<ps.required_count))::int,
                count(*) filter(where coalesce(a.assigned_count,0)<5)::int
            from plan_steps ps left join assignees a on a.id=ps.id;
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("planning-missing", "생산계획 미등록", counts[0], "danger", "production-planning", "계획 등록"),
            Metric("planning-progress", "생산계획 작성 중", counts[1], "warning", "production-planning", "계획 계속하기"),
            Metric("planning-assignee", "담당 미지정", counts[2], "warning", "production-planning", "담당자 지정")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadProcurementAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            select
                count(*) filter(where not i.receipt_completed)::int,
                count(*) filter(where not i.receipt_completed and i.expected_receipt_date<current_date)::int,
                count(*) filter(where i.receipt_completed)::int
            from project_procurement_items i join projects p on p.id=i.project_id
            where i.status='Active' and p.deleted_at_utc is null and p.status<>'Completed'
              and (@has_read_all or p.project_key=any(@project_keys));
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("procurement-pending", "입고 예정 대기", counts[0], "neutral", "procurement", "구매 현황 열기"),
            Metric("procurement-overdue", "입고 지연", counts[1], "danger", "procurement", "지연 항목 확인"),
            Metric("procurement-complete", "입고 완료", counts[2], "success", "procurement", "완료 항목 보기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadMaterialsAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            with visible as (
                select p.id from projects p where p.deleted_at_utc is null and p.status='Active'
                  and (@has_read_all or p.project_key=any(@project_keys))
            ), readiness as (
                select v.id,count(i.id)::int active_items,count(i.id) filter(where i.receipt_completed)::int completed_items
                from visible v left join project_procurement_items i on i.project_id=v.id and i.status='Active'
                group by v.id
            )
            select
                (select count(*)::int from project_procurement_items i join visible v on v.id=i.project_id
                    where i.status='Active' and not i.receipt_completed and i.material_arrivals_closed_at_utc is null
                    and not exists(select 1 from material_receipts r where r.procurement_item_id=i.id)),
                (select count(*)::int from material_iqc_attempts a join material_receipts r on r.id=a.material_receipt_id
                    join project_procurement_items i on i.id=r.procurement_item_id join visible v on v.id=i.project_id
                    where a.status='Requested'),
                (select count(*)::int from panel_placeholders pp join readiness r on r.id=pp.project_id
                    where pp.status='Active' and pp.panel_info_completed and r.active_items>0
                      and r.active_items=r.completed_items
                      and not exists(select 1 from panel_kitting_completions c where c.panel_id=pp.id));
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("materials-arrival", "도착 등록 대기", counts[0], "warning", "materials-receipts", "입고 등록"),
            Metric("materials-iqc", "IQC 판정 대기", counts[1], "danger", "materials-receipts", "IQC 확인"),
            Metric("materials-kitting", "키팅 대기 패널", counts[2], "neutral", "materials-kitting", "키팅 열기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadManufacturingAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            with visible_panels as (
                select pp.id from panel_placeholders pp join projects p on p.id=pp.project_id
                join panel_kitting_completions c on c.panel_id=pp.id
                where pp.status='Active' and p.deleted_at_utc is null and p.status<>'Cancelled'
                  and (@has_read_all or p.project_key=any(@project_keys))
            ), latest as (
                select distinct on (e.panel_id) e.panel_id,e.status from panel_manufacturing_executions e
                join visible_panels vp on vp.id=e.panel_id order by e.panel_id,e.started_at_utc desc,e.id desc
            )
            select
                (select count(*)::int from visible_panels vp left join latest l on l.panel_id=vp.id
                    where l.status is null or l.status='Cancelled'),
                count(*) filter(where status='InProgress')::int,
                count(*) filter(where status='Blocked')::int
            from latest;
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("manufacturing-ready", "제조 대기", counts[0], "neutral", "manufacturing", "제조 시작"),
            Metric("manufacturing-progress", "제조 진행 중", counts[1], "warning", "manufacturing", "진행 작업 열기"),
            Metric("manufacturing-blocked", "제조 차단", counts[2], "danger", "manufacturing", "차단 원인 확인")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadQualityAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            with visible_work as (
                select w.status,w.workflow_stage_code from work_items w join projects p on p.id=w.project_id
                where p.deleted_at_utc is null and p.status<>'Cancelled'
                  and w.target_type='Panel'
                  and w.workflow_stage_code in ('LQC','OQC','CustomerInspection','FAT')
                  and (@has_read_all or p.project_key=any(@project_keys))
            )
            select
                count(*) filter(where status in ('Requested','InProgress'))::int,
                (select count(*)::int from pending_issues pi join projects p on p.id=pi.project_id
                    where pi.status<>'Closed' and pi.action_department_code='quality'
                      and p.deleted_at_utc is null and (@has_read_all or p.project_key=any(@project_keys))),
                count(*) filter(where status='Completed')::int
            from visible_work;
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("quality-waiting", "검사 대기", counts[0], "warning", "quality", "검사 대기열 열기"),
            Metric("quality-actions", "재검·조치 대기", counts[1], "danger", "pending", "조치 확인"),
            Metric("quality-completed", "판정 완료", counts[2], "success", "quality", "검사 현황 열기")
        ];
    }

    private async Task<IReadOnlyList<HomeMetricResponse>> ReadLogisticsAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            select
                count(*) filter(where w.workflow_stage_code='PackingCompleted')::int,
                count(*) filter(where w.workflow_stage_code='DepartureProcessed')::int,
                count(*) filter(where w.workflow_stage_code='DeliveryCompleted')::int
            from work_items w join projects p on p.id=w.project_id
            where w.target_type='Panel' and w.status in ('Requested','InProgress')
              and w.workflow_stage_code in ('PackingCompleted','DepartureProcessed','DeliveryCompleted')
              and p.deleted_at_utc is null and p.status<>'Cancelled'
              and (@has_read_all or p.project_key=any(@project_keys));
            """;
        var counts = await ReadCountsAsync(sql, scope, null, cancellationToken);
        return
        [
            Metric("logistics-packing", "포장 대기", counts[0], "warning", "logistics-packing", "포장 업무 열기"),
            Metric("logistics-departure", "출발 처리 대기", counts[1], "neutral", "logistics-departure", "출발 업무 열기"),
            Metric("logistics-delivery", "배송 완료 대기", counts[2], "danger", "logistics-delivery", "배송 업무 열기")
        ];
    }

    private async Task<int[]> ReadCountsAsync(
        string sql,
        ProjectAccessScope? scope,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand(sql);
        if (scope is not null)
        {
            command.Parameters.AddWithValue("has_read_all", scope.HasProjectReadAll);
            command.Parameters.AddWithValue("project_keys", scope.ProjectKeys.ToArray());
        }
        if (userId is not null) command.Parameters.AddWithValue("user_id", userId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return [0, 0, 0];
        return [reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)];
    }

    private static bool Has(IReadOnlySet<string> permissions, string permission)
        => permissions.Contains(permission);

    private static HomeMetricResponse Metric(
        string id,
        string label,
        int count,
        string tone,
        string destinationKey,
        string actionLabel)
        => new(id, label, count, tone, destinationKey, actionLabel);

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }
        return NpgsqlDataSource.Create(connectionString);
    }
}
