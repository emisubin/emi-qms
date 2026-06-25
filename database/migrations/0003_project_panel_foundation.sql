do $$
begin
    if exists (
        select 1
        from (
            select upper(regexp_replace(btrim(name), '\s+', ' ', 'g')) as normalized_title
            from projects
        ) normalized_projects
        group by normalized_title
        having count(*) > 1
    ) then
        raise exception 'Project Title normalized duplicates were found. Resolve duplicate legacy titles before applying migration 0003.';
    end if;
end $$;

alter table projects drop constraint if exists projects_project_number_key;

alter table projects add column if not exists customer_name text;
alter table projects add column if not exists item text;
alter table projects add column if not exists project_code text;
alter table projects add column if not exists project_title text;
alter table projects add column if not exists project_title_normalized text;
alter table projects add column if not exists delivery_date date;
alter table projects add column if not exists sales_owner_user_id uuid null references qms_users(id);
alter table projects add column if not exists sales_amount numeric(18, 2);
alter table projects add column if not exists currency_code text;
alter table projects add column if not exists delivery_location text;
alter table projects add column if not exists status text not null default 'Active';
alter table projects add column if not exists status_reason text;
alter table projects add column if not exists held_by_user_id uuid null references qms_users(id);
alter table projects add column if not exists held_at_utc timestamptz;
alter table projects add column if not exists cancelled_by_user_id uuid null references qms_users(id);
alter table projects add column if not exists cancelled_at_utc timestamptz;
alter table projects add column if not exists created_by_user_id uuid null references qms_users(id);
alter table projects add column if not exists updated_at_utc timestamptz not null default now();

update projects
set project_code = coalesce(project_code, project_number),
    project_title = coalesce(project_title, name),
    project_title_normalized = coalesce(project_title_normalized, upper(regexp_replace(btrim(name), '\s+', ' ', 'g'))),
    customer_name = coalesce(customer_name, ''),
    item = coalesce(item, ''),
    delivery_date = coalesce(delivery_date, current_date)
where project_title_normalized is null
   or project_code is null
   or project_title is null
   or customer_name is null
   or item is null
   or delivery_date is null;

do $$
begin
    alter table projects add constraint ck_projects_status
        check (status in ('Active', 'OnHold', 'Cancelled', 'Completed'));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table projects add constraint ck_projects_sales_amount_non_negative
        check (sales_amount is null or sales_amount >= 0);
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table projects add constraint ck_projects_currency_code_iso4217
        check (currency_code is null or currency_code ~ '^[A-Z]{3}$');
exception
    when duplicate_object then null;
end $$;

create unique index if not exists ux_projects_project_title_normalized
    on projects(project_title_normalized)
    where project_title_normalized is not null;

create index if not exists ix_projects_status on projects(status);
create index if not exists ix_projects_sales_owner_user_id on projects(sales_owner_user_id);
create index if not exists ix_projects_delivery_date on projects(delivery_date);
create index if not exists ix_projects_project_code on projects(project_code);

create table if not exists panel_placeholders (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    sequence_number integer not null,
    display_code text not null,
    panel_name text null,
    width_mm numeric(12, 2) null,
    height_mm numeric(12, 2) null,
    depth_mm numeric(12, 2) null,
    status text not null default 'Active',
    panel_info_completed boolean not null default false,
    qr_eligible boolean not null default false,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    cancellation_reason text null,
    constraint ck_panel_placeholders_sequence_positive check (sequence_number >= 1),
    constraint ck_panel_placeholders_status check (status in ('Active', 'Cancelled')),
    constraint ck_panel_placeholders_width_non_negative check (width_mm is null or width_mm >= 0),
    constraint ck_panel_placeholders_height_non_negative check (height_mm is null or height_mm >= 0),
    constraint ck_panel_placeholders_depth_non_negative check (depth_mm is null or depth_mm >= 0),
    constraint ux_panel_placeholders_project_sequence unique (project_id, sequence_number),
    constraint ux_panel_placeholders_project_display_code unique (project_id, display_code)
);

create index if not exists ix_panel_placeholders_project_id on panel_placeholders(project_id);
create index if not exists ix_panel_placeholders_status on panel_placeholders(status);

create table if not exists project_audit_events (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    entity_type text not null,
    entity_id uuid not null,
    action text not null,
    field_name text null,
    old_value text null,
    new_value text null,
    reason text null,
    changed_by_user_id uuid null references qms_users(id),
    changed_at_utc timestamptz not null default now(),
    correlation_id text not null,
    is_sensitive boolean not null default false,
    constraint ck_project_audit_events_entity_type check (entity_type in ('Project', 'PanelPlaceholder')),
    constraint ck_project_audit_events_reason_length check (reason is null or length(reason) <= 500)
);

create index if not exists ix_project_audit_events_project_id on project_audit_events(project_id);
create index if not exists ix_project_audit_events_changed_at_utc on project_audit_events(changed_at_utc);
create index if not exists ix_project_audit_events_entity on project_audit_events(entity_type, entity_id);

insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000013', 'Project.Create', 'Create sales projects'),
    ('30000000-0000-0000-0000-000000000014', 'Project.Update', 'Update sales projects'),
    ('30000000-0000-0000-0000-000000000015', 'Project.Hold', 'Hold sales projects'),
    ('30000000-0000-0000-0000-000000000016', 'Project.Cancel', 'Cancel sales projects')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code in (
    'Project.Create',
    'Project.Update',
    'Project.Hold',
    'Project.Cancel'
)
where roles.code = 'sales'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code in (
      'Project.Create',
      'Project.Update',
      'Project.Hold',
      'Project.Cancel'
  )
  and roles.code <> 'sales';
