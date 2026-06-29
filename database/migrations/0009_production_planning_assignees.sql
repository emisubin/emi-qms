insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000023', 'ProductionPlan.Update', 'Update production planning')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'ProductionPlan.Update'
where roles.code = 'production-planning'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'ProductionPlan.Update'
  and roles.code <> 'production-planning';

create table if not exists production_product_types (
    id uuid primary key default uuid_generate_v4(),
    code text not null,
    name text not null,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    constraint ux_production_product_types_code unique (code),
    constraint ck_production_product_types_code_not_blank check (btrim(code) <> ''),
    constraint ck_production_product_types_name_not_blank check (btrim(name) <> '')
);

create table if not exists production_plan_templates (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null references production_product_types(id) on delete restrict,
    version integer not null default 1,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    constraint ck_production_plan_templates_version_positive check (version >= 1)
);

create unique index if not exists ux_production_plan_templates_active_product_type
    on production_plan_templates(product_type_id)
    where is_active = true;

create table if not exists production_plan_template_steps (
    id uuid primary key default uuid_generate_v4(),
    template_id uuid not null references production_plan_templates(id) on delete cascade,
    sequence_number integer not null,
    step_name text not null,
    is_required boolean not null default true,
    is_active boolean not null default true,
    constraint ck_production_plan_template_steps_sequence_positive check (sequence_number >= 1),
    constraint ck_production_plan_template_steps_name_not_blank check (btrim(step_name) <> ''),
    constraint ux_production_plan_template_steps_sequence unique (template_id, sequence_number)
);

create table if not exists production_plan_template_audit_events (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null references production_product_types(id) on delete restrict,
    template_id uuid not null references production_plan_templates(id) on delete restrict,
    action text not null,
    old_value text null,
    new_value text null,
    reason text null,
    changed_by_user_id uuid null references qms_users(id),
    changed_at_utc timestamptz not null default now(),
    correlation_id text null,
    constraint ck_production_plan_template_audit_action_not_blank check (btrim(action) <> '')
);

create index if not exists ix_production_plan_template_audit_product_type
    on production_plan_template_audit_events(product_type_id, changed_at_utc desc);

create table if not exists system_holidays (
    id uuid primary key default uuid_generate_v4(),
    holiday_date date not null,
    name text not null,
    country_code text not null default 'KR',
    source text not null,
    source_key text not null,
    is_active boolean not null default true,
    synced_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_system_holidays_name_not_blank check (btrim(name) <> ''),
    constraint ck_system_holidays_country_code_not_blank check (btrim(country_code) <> ''),
    constraint ck_system_holidays_source_not_blank check (btrim(source) <> ''),
    constraint ck_system_holidays_source_key_not_blank check (btrim(source_key) <> ''),
    constraint ux_system_holidays_country_date_source_key unique (country_code, holiday_date, source_key)
);

create index if not exists ix_system_holidays_active_lookup
    on system_holidays(country_code, holiday_date)
    where is_active = true;

with product_types(id, code, name) as (
    values
        ('31000000-0000-0000-0000-000000000067'::uuid, 'UL67', 'UL67'),
        ('31000000-0000-0000-0000-000000000891'::uuid, 'UL891', 'UL891'),
        ('31000000-0000-0000-0000-00000000508a'::uuid, 'UL508A', 'UL508A'),
        ('31000000-0000-0000-0000-0000000001ec'::uuid, 'IEC', 'IEC'),
        ('31000000-0000-0000-0000-000000000112'::uuid, 'LLP', 'LLP'),
        ('31000000-0000-0000-0000-000000000772'::uuid, 'RRP', 'RRP')
)
insert into production_product_types (id, code, name, is_active)
select id, code, name, true
from product_types
on conflict (code) do update
set name = excluded.name,
    is_active = true;

with product_types as (
    select id, code
    from production_product_types
    where code in ('UL67', 'UL891', 'UL508A', 'IEC', 'LLP', 'RRP')
),
template_ids(product_type_code, template_id) as (
    values
        ('UL67', '32000000-0000-0000-0000-000000000067'::uuid),
        ('UL891', '32000000-0000-0000-0000-000000000891'::uuid),
        ('UL508A', '32000000-0000-0000-0000-00000000508a'::uuid),
        ('IEC', '32000000-0000-0000-0000-0000000001ec'::uuid),
        ('LLP', '32000000-0000-0000-0000-000000000112'::uuid),
        ('RRP', '32000000-0000-0000-0000-000000000772'::uuid)
)
insert into production_plan_templates (id, product_type_id, version, is_active)
select template_ids.template_id, product_types.id, 1, true
from product_types
join template_ids on template_ids.product_type_code = product_types.code
on conflict (id) do nothing;

with active_templates as (
    select template_id
    from (values
        ('32000000-0000-0000-0000-000000000067'::uuid),
        ('32000000-0000-0000-0000-000000000891'::uuid),
        ('32000000-0000-0000-0000-00000000508a'::uuid),
        ('32000000-0000-0000-0000-0000000001ec'::uuid),
        ('32000000-0000-0000-0000-000000000112'::uuid),
        ('32000000-0000-0000-0000-000000000772'::uuid)
    ) as template_ids(template_id)
),
default_steps(sequence_number, step_name) as (
    values
        (1, '자재 입고'),
        (2, '조립 시작'),
        (3, '배선'),
        (4, '검사 준비')
)
insert into production_plan_template_steps (template_id, sequence_number, step_name, is_required, is_active)
select active_templates.template_id, default_steps.sequence_number, default_steps.step_name, true, true
from active_templates
cross join default_steps
on conflict (template_id, sequence_number) do nothing;

create table if not exists project_production_plans (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    product_type_id uuid null references production_product_types(id) on delete restrict,
    template_id uuid null references production_plan_templates(id) on delete restrict,
    notes text null,
    row_version integer not null default 0,
    created_at_utc timestamptz not null default now(),
    created_by_user_id uuid null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id),
    constraint ux_project_production_plans_project unique (project_id),
    constraint ck_project_production_plans_row_version_nonnegative check (row_version >= 0)
);

create table if not exists project_production_plan_items (
    id uuid primary key default uuid_generate_v4(),
    production_plan_id uuid not null references project_production_plans(id) on delete cascade,
    template_step_id uuid null references production_plan_template_steps(id) on delete restrict,
    sequence_number integer not null,
    step_name_snapshot text not null,
    is_required boolean not null default true,
    is_active boolean not null default true,
    planned_date date null,
    note text null,
    row_version integer not null default 0,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_project_production_plan_items_sequence_positive check (sequence_number >= 1),
    constraint ck_project_production_plan_items_name_not_blank check (btrim(step_name_snapshot) <> ''),
    constraint ck_project_production_plan_items_row_version_nonnegative check (row_version >= 0),
    constraint ux_project_production_plan_items_sequence unique (production_plan_id, sequence_number)
);

create unique index if not exists ux_project_production_plan_items_active_name
    on project_production_plan_items(production_plan_id, lower(btrim(step_name_snapshot)))
    where is_active = true;

create table if not exists production_planning_excel_import_batches (
    id uuid primary key default uuid_generate_v4(),
    original_file_name text not null,
    file_size_bytes bigint not null,
    file_sha256 text not null,
    total_row_count integer not null,
    applied_row_count integer not null,
    error_row_count integer not null,
    uploaded_by_user_id uuid null references qms_users(id),
    uploaded_at_utc timestamptz not null default now(),
    reason text null,
    constraint ck_production_planning_excel_file_name_not_blank check (btrim(original_file_name) <> ''),
    constraint ck_production_planning_excel_counts_nonnegative check (
        total_row_count >= 0 and applied_row_count >= 0 and error_row_count >= 0
    )
);

create table if not exists project_assignees (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    responsibility_type text not null,
    assigned_user_id uuid null references qms_users(id),
    assigned_by_user_id uuid null references qms_users(id),
    assigned_at_utc timestamptz null,
    note text null,
    row_version integer not null default 0,
    constraint ux_project_assignees_project_responsibility unique (project_id, responsibility_type),
    constraint ck_project_assignees_responsibility_type check (responsibility_type in ('Procurement', 'ProductionPlanning', 'Manufacturing', 'Quality', 'Logistics')),
    constraint ck_project_assignees_row_version_nonnegative check (row_version >= 0)
);

create index if not exists ix_project_production_plans_project
    on project_production_plans(project_id);

create index if not exists ix_project_assignees_project
    on project_assignees(project_id);

do $$
begin
    alter table project_audit_events drop constraint if exists ck_project_audit_events_entity_type;

    alter table project_audit_events add constraint ck_project_audit_events_entity_type
        check (entity_type in ('Project', 'PanelPlaceholder', 'Panel', 'ProcurementItem', 'ProductionPlan', 'ProductionPlanItem', 'ProjectAssignee'));
exception
    when duplicate_object then null;
end $$;
