update workflow_stages
set is_optional = true
where stage_code = 'KittingCompleted';

update work_items
set status = 'Cancelled',
    cancelled_at_utc = coalesce(cancelled_at_utc, now())
where target_type = 'Project'
  and workflow_stage_code = 'KittingCompleted'
  and status in ('Requested', 'InProgress');

update notification_recipients recipient
set read_at_utc = coalesce(recipient.read_at_utc, now())
where recipient.notification_id in (
    select notification.id
    from notifications notification
    join work_items work_item on work_item.id = notification.work_item_id
    where work_item.target_type = 'Project'
      and work_item.workflow_stage_code = 'KittingCompleted'
      and work_item.status = 'Cancelled'
);

alter table panel_kitting_batches
    drop constraint if exists ck_panel_kitting_batches_counts;

alter table panel_kitting_batches
    add constraint ck_panel_kitting_batches_counts
    check (
        completed_panel_count > 0
        and generated_work_item_count between 0 and completed_panel_count
        and readiness_active_item_count >= 0
        and readiness_completed_item_count between 0 and readiness_active_item_count
    );

create table if not exists panel_manufacturing_release_operations (
    operation_id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    requested_by_user_id uuid not null references qms_users(id) on delete restrict,
    panel_ids uuid[] not null,
    released_panel_count integer not null,
    generated_work_item_count integer not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_manufacturing_release_operations_panels
        check (
            cardinality(panel_ids) between 1 and 500
            and released_panel_count = cardinality(panel_ids)
            and generated_work_item_count between 0 and released_panel_count
        )
);

create index if not exists ix_panel_manufacturing_release_operations_project_created
    on panel_manufacturing_release_operations(project_id, created_at_utc desc);
