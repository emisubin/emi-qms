create table if not exists project_production_plan_set_scopes (
    id uuid primary key default uuid_generate_v4(),
    production_plan_id uuid not null
        references project_production_plans(id) on delete cascade,
    set_instance_id uuid not null
        references ul891_set_instances(id) on delete restrict,
    row_version integer not null default 1,
    created_at_utc timestamptz not null default now(),
    created_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    constraint ux_project_production_plan_set_scopes_plan_instance
        unique (production_plan_id, set_instance_id),
    constraint ck_project_production_plan_set_scopes_row_version
        check (row_version >= 1)
);

create table if not exists project_production_plan_set_item_values (
    id uuid primary key default uuid_generate_v4(),
    set_scope_id uuid not null
        references project_production_plan_set_scopes(id) on delete cascade,
    production_plan_item_id uuid not null
        references project_production_plan_items(id) on delete restrict,
    planned_start_date date null,
    planned_end_date date null,
    assigned_user_id uuid null references qms_users(id) on delete restrict,
    required_headcount integer null,
    note text null,
    row_version integer not null default 1,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_project_production_plan_set_item_values_scope_item
        unique (set_scope_id, production_plan_item_id),
    constraint ck_project_production_plan_set_item_values_period check (
        planned_start_date is null
        or planned_end_date is null
        or planned_start_date <= planned_end_date
    ),
    constraint ck_project_production_plan_set_item_values_headcount check (
        required_headcount is null or required_headcount between 1 and 999
    ),
    constraint ck_project_production_plan_set_item_values_row_version
        check (row_version >= 1)
);

create index if not exists ix_project_production_plan_set_scopes_instance
    on project_production_plan_set_scopes(set_instance_id);

create index if not exists ix_project_production_plan_set_item_values_item
    on project_production_plan_set_item_values(production_plan_item_id);

-- Existing UL891 set projects keep the project-level values users already entered.
-- Each physical set receives an independent overlay copy; base rows and connections
-- remain untouched as the project-specific structure source of truth.
insert into project_production_plan_set_scopes (
    id,
    production_plan_id,
    set_instance_id,
    row_version,
    created_at_utc,
    created_by_user_id,
    updated_at_utc,
    updated_by_user_id
)
select
    uuid_generate_v4(),
    plan.id,
    instance.id,
    1,
    now(),
    plan.created_by_user_id,
    now(),
    plan.updated_by_user_id
from projects project
join project_production_plans plan
  on plan.project_id = project.id
 and plan.model_version = 'LINKED_V1'
join ul891_set_specs spec on spec.project_id = project.id
join ul891_set_instances instance on instance.spec_id = spec.id
where project.structure_mode = 'Ul891Set'
on conflict (production_plan_id, set_instance_id) do nothing;

insert into project_production_plan_set_item_values (
    id,
    set_scope_id,
    production_plan_item_id,
    planned_start_date,
    planned_end_date,
    assigned_user_id,
    required_headcount,
    note,
    row_version,
    created_at_utc,
    updated_at_utc
)
select
    uuid_generate_v4(),
    scope.id,
    item.id,
    item.planned_start_date,
    item.planned_end_date,
    item.assigned_user_id,
    item.required_headcount,
    item.note,
    1,
    now(),
    now()
from project_production_plan_set_scopes scope
join project_production_plans plan on plan.id = scope.production_plan_id
join projects project
  on project.id = plan.project_id
 and project.structure_mode = 'Ul891Set'
join project_production_plan_items item
  on item.production_plan_id = plan.id
where plan.model_version = 'LINKED_V1'
on conflict (set_scope_id, production_plan_item_id) do nothing;

do $$
begin
    if exists (
        select 1
        from project_production_plan_set_scopes scope
        join project_production_plans plan on plan.id = scope.production_plan_id
        join ul891_set_instances instance on instance.id = scope.set_instance_id
        join ul891_set_specs spec on spec.id = instance.spec_id
        where plan.project_id <> spec.project_id
    ) then
        raise exception 'UL891 production plan set scope project ownership mismatch';
    end if;

    if exists (
        select 1
        from project_production_plan_set_item_values value
        join project_production_plan_set_scopes scope on scope.id = value.set_scope_id
        join project_production_plan_items item on item.id = value.production_plan_item_id
        where scope.production_plan_id <> item.production_plan_id
    ) then
        raise exception 'UL891 production plan set value plan ownership mismatch';
    end if;
end $$;

comment on table project_production_plan_set_scopes is
    'Independent schedule scope for one physical UL891 set instance. Project plan items and result connections remain shared.';

comment on table project_production_plan_set_item_values is
    'Per-set schedule, staffing and production-control comment overlay for a shared project production plan item.';
