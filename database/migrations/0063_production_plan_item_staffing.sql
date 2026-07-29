alter table project_production_plan_items
    add column if not exists assigned_user_id uuid null
        references qms_users(id) on delete restrict,
    add column if not exists required_headcount integer null,
    drop constraint if exists ck_project_production_plan_items_required_headcount,
    add constraint ck_project_production_plan_items_required_headcount check (
        required_headcount is null or required_headcount between 1 and 999
    );

create index if not exists ix_project_production_plan_items_assigned_user
    on project_production_plan_items(assigned_user_id)
    where assigned_user_id is not null and is_active;

comment on column project_production_plan_items.assigned_user_id is
    'Optional schedule owner for this production plan item. Does not change work-item or notification ownership.';

comment on column project_production_plan_items.required_headcount is
    'Optional planned staffing count for this production plan item, from 1 through 999.';
