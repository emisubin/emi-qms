insert into departments (id, code, name)
values ('10000000-0000-0000-0000-000000000008', 'design', 'Design')
on conflict (code) do update set name = excluded.name;

insert into roles (id, code, name)
values ('20000000-0000-0000-0000-000000000008', 'design', 'Design User')
on conflict (code) do update set name = excluded.name;

insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000019', 'PanelInfo.Update', 'Update panel information')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code in ('projects.read', 'Project.Read.All', 'PanelInfo.Update')
where roles.code = 'design'
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'PanelInfo.Update'
where roles.code in ('sales', 'production-planning')
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'PanelInfo.Update'
  and roles.code not in ('design', 'sales', 'production-planning');

alter table panel_placeholders alter column width_mm type numeric(12, 3);
alter table panel_placeholders alter column height_mm type numeric(12, 3);
alter table panel_placeholders alter column depth_mm type numeric(12, 3);

alter table panel_placeholders add column if not exists panel_info_version integer not null default 0;
alter table panel_placeholders add column if not exists panel_info_updated_at_utc timestamptz;
alter table panel_placeholders add column if not exists panel_info_updated_by_user_id uuid null references qms_users(id);

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_panel_name_length
        check (panel_name is null or length(panel_name) <= 200);
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_width_range
        check (width_mm is null or (width_mm > 0 and width_mm <= 100000));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_height_range
        check (height_mm is null or (height_mm > 0 and height_mm <= 100000));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_depth_range
        check (depth_mm is null or (depth_mm > 0 and depth_mm <= 100000));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table panel_placeholders add constraint ck_panel_placeholders_size_all_or_none
        check (
            (width_mm is null and height_mm is null and depth_mm is null)
            or (width_mm is not null and height_mm is not null and depth_mm is not null)
        );
exception
    when duplicate_object then null;
end $$;

create table if not exists panel_information_excel_import_batches (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    original_file_name text not null,
    file_size_bytes bigint not null,
    file_sha256 text not null,
    input_unit text null,
    total_row_count integer not null,
    new_panel_count integer not null,
    changed_panel_count integer not null,
    unchanged_panel_count integer not null,
    uploaded_by_user_id uuid null references qms_users(id),
    uploaded_at_utc timestamptz not null default now(),
    reason text null,
    constraint ck_panel_information_excel_import_batches_file_size
        check (file_size_bytes >= 0 and file_size_bytes <= 10485760),
    constraint ck_panel_information_excel_import_batches_file_sha256
        check (file_sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_panel_information_excel_import_batches_input_unit
        check (input_unit is null or input_unit in ('Mm', 'Inch')),
    constraint ck_panel_information_excel_import_batches_counts
        check (
            total_row_count >= 0
            and new_panel_count >= 0
            and changed_panel_count >= 0
            and unchanged_panel_count >= 0
        ),
    constraint ck_panel_information_excel_import_batches_reason_length
        check (reason is null or length(reason) <= 500)
);

create index if not exists ix_panel_information_excel_import_batches_project_uploaded
    on panel_information_excel_import_batches(project_id, uploaded_at_utc desc);

create index if not exists ix_panel_information_excel_import_batches_uploaded_by
    on panel_information_excel_import_batches(uploaded_by_user_id);

do $$
begin
    alter table project_audit_events drop constraint if exists ck_project_audit_events_entity_type;

    alter table project_audit_events add constraint ck_project_audit_events_entity_type
        check (entity_type in ('Project', 'PanelPlaceholder', 'Panel'));
exception
    when duplicate_object then null;
end $$;

alter table project_audit_events add column if not exists input_source text;
alter table project_audit_events add column if not exists import_batch_id uuid null references panel_information_excel_import_batches(id) on delete restrict;
alter table project_audit_events add column if not exists input_unit text;
alter table project_audit_events add column if not exists original_input_value text;

do $$
begin
    alter table project_audit_events add constraint ck_project_audit_events_input_source
        check (input_source is null or input_source in ('Direct', 'Excel'));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table project_audit_events add constraint ck_project_audit_events_input_unit
        check (input_unit is null or input_unit in ('Mm', 'Inch'));
exception
    when duplicate_object then null;
end $$;

create index if not exists ix_project_audit_events_import_batch_id
    on project_audit_events(import_batch_id)
    where import_batch_id is not null;

create index if not exists ix_panel_placeholders_project_sequence_active
    on panel_placeholders(project_id, sequence_number)
    where status = 'Active';

create index if not exists ix_panel_placeholders_panel_info_updated_by
    on panel_placeholders(panel_info_updated_by_user_id)
    where panel_info_updated_by_user_id is not null;

update panel_placeholders
set panel_info_completed = case
        when projects.packaging_method is null then false
        when projects.packaging_method = 'WoodenCrate' then
            panel_placeholders.panel_name is not null
            and panel_placeholders.width_mm is not null
            and panel_placeholders.height_mm is not null
            and panel_placeholders.depth_mm is not null
        when projects.packaging_method in ('StretchWrap', 'HeavyDutyBox') then
            panel_placeholders.panel_name is not null
            and (
                (panel_placeholders.width_mm is null and panel_placeholders.height_mm is null and panel_placeholders.depth_mm is null)
                or (panel_placeholders.width_mm is not null and panel_placeholders.height_mm is not null and panel_placeholders.depth_mm is not null)
            )
        else false
    end,
    qr_eligible = projects.deleted_at_utc is null
        and projects.status = 'Active'
        and panel_placeholders.status = 'Active'
        and panel_placeholders.panel_name is not null
from projects
where projects.id = panel_placeholders.project_id;
