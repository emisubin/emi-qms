alter table notifications
    drop constraint if exists ck_notifications_source_kind;

alter table notifications
    add constraint ck_notifications_source_kind
    check (source_kind in (
        'Automatic',
        'Manual',
        'ChannelNotice',
        'WorkAssignment',
        'PendingAssignment',
        'PendingClosed',
        'ProjectCreated',
        'ProjectDeliveryDateChanged',
        'ProjectStatusChanged',
        'ReinspectionRequested',
        'ProjectDeliveryCompleted',
        'ProjectCompletion',
        'DailyDigest',
        'Escalation',
        'System'
    ));

alter table work_items
    add column if not exists fallback_group_key text null,
    add column if not exists fallback_completed_by_user_id uuid null references qms_users(id) on delete set null,
    add column if not exists fallback_auto_closed_at_utc timestamptz null;

alter table work_items
    drop constraint if exists ck_work_items_fallback_group_key_not_blank;

alter table work_items
    add constraint ck_work_items_fallback_group_key_not_blank
    check (fallback_group_key is null or btrim(fallback_group_key) <> '');

create index if not exists ix_work_items_fallback_group
    on work_items(fallback_group_key, status)
    where fallback_group_key is not null;

-- Only open work with an exact schedule source is backfilled. Completed/cancelled
-- work and ambiguous project/panel work keep their historical due date unchanged.
update work_items work_item
set due_date = item.expected_receipt_date
from project_procurement_items item
where work_item.target_type = 'ProcurementItem'
  and work_item.target_id = item.id
  and item.status = 'Active'
  and work_item.status in ('Requested', 'InProgress')
  and work_item.due_date is distinct from item.expected_receipt_date;

update work_items work_item
set due_date = (
    select min(item.expected_receipt_date)
    from project_procurement_items item
    where item.project_id = work_item.project_id
      and item.status = 'Active'
      and item.receipt_completed = false
      and item.expected_receipt_date is not null
)
where work_item.target_type = 'Project'
  and work_item.workflow_stage_code = 'MaterialArrived'
  and work_item.status in ('Requested', 'InProgress')
  and exists (
      select 1
      from project_procurement_items source_item
      where source_item.project_id = work_item.project_id
  );

update work_items work_item
set due_date = coalesce(
    (
        select min(scope_value.planned_end_date)
        from project_production_plan_set_item_values scope_value
        join project_production_plan_set_scopes scope on scope.id = scope_value.set_scope_id
        where scope.production_plan_id = plan_item.production_plan_id
          and scope_value.production_plan_item_id = plan_item.id
          and scope_value.planned_end_date is not null
    ),
    plan_item.planned_end_date)
from project_production_plan_items plan_item
join project_production_plans plan on plan.id = plan_item.production_plan_id
where work_item.project_id = plan.project_id
  and work_item.target_type = 'ProductionPlan'
  and work_item.target_id = plan_item.id
  and plan_item.is_active = true
  and work_item.status in ('Requested', 'InProgress');
