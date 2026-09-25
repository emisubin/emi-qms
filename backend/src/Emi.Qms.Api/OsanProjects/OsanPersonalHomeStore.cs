using System.Text.Json;
using Emi.Qms.Api.Projects;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed record OsanHomeSummary(int TotalCount, int InProgressCount, int HoldCount, int OverdueCount, int OpenIssueCount);
public sealed record OsanHomeCustomer(Guid CustomerId, string CustomerName, int TotalCount, int InProgressCount,
    int HoldCount, int OverdueCount, int OpenIssueCount, int ProgressPercent, int NotStartedCount, int CompletedCount);
public sealed record OsanHomeTask(Guid Id, string Kind, Guid ProjectId, Guid TargetId, int StageSequence,
    string ProjectTitle, string ProductName, string StageName, string Comment, string ActorName, DateTimeOffset OccurredAtUtc);
public sealed record OsanHomeDeadline(Guid ProjectId, string Title, string ProjectCode, string CustomerName,
    string ProductName, DateOnly DeliveryDate, string Status, int ProgressPercent);
public sealed record OsanHomeNews(Guid Id, Guid ProjectId, Guid TargetId, int StageSequence,
    string ProjectTitle, string StageName, string Kind, string ActorName, DateTimeOffset OccurredAtUtc);
public sealed record OsanPersonalHomeResponse(IReadOnlyList<OsanHomeCustomer> Customers, OsanHomeSummary Summary,
    IReadOnlyList<OsanHomeTask> Tasks, IReadOnlyList<OsanHomeDeadline> Deadlines, IReadOnlyList<OsanHomeNews> News, int TaskTotalCount, int DeadlineTotalCount);

public sealed class OsanPersonalHomeStore(DatabaseConnectionStringProvider provider, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OsanPersonalHomeResponse> GetAsync(Guid actor, ProjectAccessScope scope, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")).DateTime);
        await using var source = NpgsqlDataSource.Create(provider.GetConnectionString()
            ?? throw new InvalidOperationException("QMS database connection string is not configured."));
        // One statement provides a consistent snapshot across the personal and assigned-customer sections.
        await using var command = source.CreateCommand("""
            with accessible as (
                select p.* from projects p
                where p.project_profile='Osan' and p.deleted_at_utc is null
                  and (@read_all or p.project_key=any(@keys))
            ), assigned as (
                select c.id,c.name from osan_customers c
                join osan_customer_assignments a on a.customer_id=c.id
                where a.user_id=@actor and c.archived_at_utc is null
            ), overview as (
                select p.*, x.done, x.total,
                    case when p.osan_delivery_hold then 'Hold' when p.status='Completed' then 'Completed'
                         when x.done>0 or i.n>0 then 'InProgress' else 'NotStarted' end home_status,
                    i.n issue_count,
                    not p.osan_delivery_hold and p.status<>'Completed' and p.delivery_date<@today overdue,
                    case when p.status='Completed' then 100 when x.total=0 then 0
                         else least(99,x.done*100/x.total) end progress
                from accessible p join assigned a on a.id=p.osan_customer_id
                cross join lateral (select count(*)::int total,count(*) filter(where s.status='Completed')::int done
                    from osan_active_project_target_steps s where s.project_id=p.id) x
                cross join lateral (select count(*)::int n from osan_stage_issues i
                    join osan_active_project_targets t on t.id=i.target_id
                    where i.project_id=p.id and i.status='Open') i
                where not(p.status='Completed' and p.delivery_date<@today)
            ), customer_rows as (
                select a.id "customerId",a.name "customerName",count(p.id)::int "totalCount",
                    count(p.id) filter(where p.home_status='InProgress')::int "inProgressCount",
                    count(p.id) filter(where p.home_status='NotStarted')::int "notStartedCount",
                    count(p.id) filter(where p.home_status='Completed')::int "completedCount",
                    count(p.id) filter(where p.home_status='Hold')::int "holdCount",
                    count(p.id) filter(where p.overdue)::int "overdueCount",
                    count(p.id) filter(where p.issue_count>0)::int "openIssueCount",
                    coalesce(round(avg(p.progress)),0)::int "progressPercent"
                from assigned a left join overview p on p.osan_customer_id=a.id
                group by a.id,a.name order by a.name,a.id
            ), task_rows as (
                select r.operation_id id,'request' kind,p.id "projectId",s.target_id "targetId",
                    s.sequence_number "stageSequence",p.project_title "projectTitle",p.osan_product_name "productName",
                    s.step_name "stageName",'' comment,u.display_name "actorName",r.requested_at_utc "occurredAtUtc"
                from osan_stage_work_requests r
                join osan_stage_work_request_recipients recipient on recipient.operation_id=r.operation_id and recipient.recipient_user_id=@actor
                join accessible p on p.id=r.project_id
                join osan_active_project_target_steps s on s.id=r.step_id and s.project_id=p.id
                join qms_users u on u.id=r.requested_by_user_id
                where s.status<>'Completed'
                  and not exists(select 1 from osan_stage_records done where done.step_id=s.id
                    and done.event_type in ('Complete','Edit','IssueResolved') and done.occurred_at_utc>=r.requested_at_utc)
                union all
                select rejection.id,'rejection',p.id,s.target_id,s.sequence_number,p.project_title,p.osan_product_name,
                    s.step_name,coalesce(rejection.reason,''),u.display_name,rejection.occurred_at_utc
                from accessible p join osan_active_project_target_steps s on s.project_id=p.id
                join lateral(select r.* from osan_stage_records r where r.step_id=s.id and r.event_type='Reject'
                    order by r.occurred_at_utc desc,r.id desc limit 1) rejection on true
                join qms_users u on u.id=rejection.actor_user_id
                where s.rejected and s.status<>'Completed' and exists(
                    select 1 from osan_stage_records input where input.step_id=s.id and input.actor_user_id=@actor
                    and input.event_type in ('Complete','Edit','IssueRegistered','IssueRecorded','IssueResolved')
                    and input.occurred_at_utc<=rejection.occurred_at_utc)
            ), ordered_tasks as (
                select * from task_rows order by "occurredAtUtc" desc,id
            ), deadline_rows as (
                select p.id "projectId",p.project_title title,p.project_code "projectCode",p.customer_name "customerName",
                    p.osan_product_name "productName",p.delivery_date "deliveryDate",p.home_status status,p.progress "progressPercent"
                from overview p where not p.osan_delivery_hold and p.status<>'Completed'
                    and p.delivery_date<=@today+14
                order by p.delivery_date,p.project_code,p.id limit 20
            ), news_rows as (
                select r.id,p.id "projectId",s.target_id "targetId",s.sequence_number "stageSequence",
                    p.project_title "projectTitle",s.step_name "stageName",r.event_type kind,
                    u.display_name "actorName",r.occurred_at_utc "occurredAtUtc"
                from osan_stage_records r join accessible p on p.id=r.project_id
                join assigned a on a.id=p.osan_customer_id
                join osan_active_project_target_steps s on s.id=r.step_id
                join qms_users u on u.id=r.actor_user_id
                where r.occurred_at_utc>=@since and r.event_type in ('Complete','Edit','Reject','Reset','IssueRegistered','IssueRecorded','IssueResolved')
                order by r.occurred_at_utc desc,r.id desc limit 20
            )
            select json_build_object(
                'customers',coalesce((select json_agg(c) from customer_rows c),'[]'::json),
                'summary',(select json_build_object('totalCount',count(*),
                    'inProgressCount',count(*) filter(where home_status='InProgress'),
                    'holdCount',count(*) filter(where home_status='Hold'),
                    'overdueCount',count(*) filter(where overdue),'openIssueCount',count(*) filter(where issue_count>0)) from overview),
                'taskTotalCount',(select count(*) from task_rows),
                'deadlineTotalCount',(select count(*) from overview where not osan_delivery_hold and status<>'Completed' and delivery_date<=@today+14),
                'tasks',coalesce((select json_agg(t) from ordered_tasks t),'[]'::json),
                'deadlines',coalesce((select json_agg(d) from deadline_rows d),'[]'::json),
                'news',coalesce((select json_agg(n) from news_rows n),'[]'::json))::text;
            """);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("read_all", scope.HasProjectReadAll);
        command.Parameters.AddWithValue("keys", scope.ProjectKeys.ToArray());
        command.Parameters.AddWithValue("today", today);
        command.Parameters.AddWithValue("since", clock.GetUtcNow().AddDays(-30));
        return JsonSerializer.Deserialize<OsanPersonalHomeResponse>((string)(await command.ExecuteScalarAsync(ct))!, JsonOptions)!;
    }
}
