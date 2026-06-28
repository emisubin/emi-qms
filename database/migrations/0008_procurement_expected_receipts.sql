insert into departments (id, code, name)
values
    ('10000000-0000-0000-0000-000000000009', 'procurement', 'Procurement'),
    ('10000000-0000-0000-0000-000000000010', 'materials', 'Materials')
on conflict (code) do update set name = excluded.name;

insert into roles (id, code, name)
values
    ('20000000-0000-0000-0000-000000000009', 'procurement', 'Procurement User'),
    ('20000000-0000-0000-0000-000000000010', 'materials', 'Materials User')
on conflict (code) do update set name = excluded.name;

insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000021', 'ProcurementPlan.Update', 'Update procurement plan'),
    ('30000000-0000-0000-0000-000000000022', 'MaterialReceipt.Update', 'Update material receipt completion')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code in ('projects.read', 'Project.Read.All', 'ProcurementPlan.Update', 'MaterialReceipt.Update')
where roles.code = 'procurement'
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code in ('projects.read', 'Project.Read.All', 'MaterialReceipt.Update')
where roles.code = 'materials'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'ProcurementPlan.Update'
  and roles.code <> 'procurement';

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'MaterialReceipt.Update'
  and roles.code not in ('procurement', 'materials');

create table if not exists project_procurement_items (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    sequence_number integer not null,
    source_project_text text null,
    source_project_code_text text null,
    standard_lead_time text null,
    order_item text null,
    technical_owner text null,
    order_date date null,
    expected_receipt_date date null,
    issue_note text null,
    receipt_completed boolean not null default false,
    receipt_completed_at_utc timestamptz null,
    receipt_completed_by_user_id uuid null references qms_users(id),
    receipt_completion_note text null,
    row_version integer not null default 0,
    source_excel_row_number integer null,
    source_group_sequence integer null,
    row_match_key text null,
    created_at_utc timestamptz not null default now(),
    created_by_user_id uuid null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id),
    status text not null default 'Active',
    constraint ck_project_procurement_items_status check (status in ('Active', 'Cancelled')),
    constraint ck_project_procurement_items_sequence_positive check (sequence_number >= 1),
    constraint ck_project_procurement_items_row_version_nonnegative check (row_version >= 0),
    constraint ck_project_procurement_items_source_excel_row_positive check (source_excel_row_number is null or source_excel_row_number >= 1),
    constraint ck_project_procurement_items_group_sequence_positive check (source_group_sequence is null or source_group_sequence >= 1),
    constraint ck_project_procurement_items_receipt_completed_consistency check (
        receipt_completed
        or (receipt_completed_at_utc is null and receipt_completed_by_user_id is null)
    )
);

create unique index if not exists ux_project_procurement_items_project_sequence_active
    on project_procurement_items(project_id, sequence_number)
    where status = 'Active';

create index if not exists ix_project_procurement_items_project_status
    on project_procurement_items(project_id, status, sequence_number);

create index if not exists ix_project_procurement_items_expected_receipt_date
    on project_procurement_items(expected_receipt_date)
    where status = 'Active';

create index if not exists ix_project_procurement_items_row_match_key
    on project_procurement_items(project_id, row_match_key)
    where status = 'Active' and row_match_key is not null;

create table if not exists procurement_excel_import_batches (
    id uuid primary key default uuid_generate_v4(),
    original_file_name text not null,
    file_size_bytes bigint not null,
    file_sha256 text not null,
    total_row_count integer not null,
    new_item_count integer not null,
    changed_item_count integer not null,
    unchanged_item_count integer not null,
    skipped_item_count integer not null,
    missing_from_upload_count integer not null,
    uploaded_by_user_id uuid null references qms_users(id),
    uploaded_at_utc timestamptz not null default now(),
    reason text null,
    constraint ck_procurement_excel_import_batches_file_size check (file_size_bytes >= 0 and file_size_bytes <= 10485760),
    constraint ck_procurement_excel_import_batches_file_sha256 check (file_sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_procurement_excel_import_batches_counts check (
        total_row_count >= 0
        and new_item_count >= 0
        and changed_item_count >= 0
        and unchanged_item_count >= 0
        and skipped_item_count >= 0
        and missing_from_upload_count >= 0
    ),
    constraint ck_procurement_excel_import_batches_reason_length check (reason is null or length(reason) <= 500)
);

create index if not exists ix_procurement_excel_import_batches_uploaded
    on procurement_excel_import_batches(uploaded_at_utc desc);

create table if not exists procurement_excel_import_batch_projects (
    import_batch_id uuid not null references procurement_excel_import_batches(id) on delete cascade,
    project_id uuid not null references projects(id) on delete restrict,
    primary key (import_batch_id, project_id)
);

create unique index if not exists ux_procurement_excel_import_batches_project_file
    on procurement_excel_import_batch_projects(project_id, import_batch_id);

alter table project_audit_events add column if not exists procurement_import_batch_id uuid null references procurement_excel_import_batches(id) on delete restrict;

create index if not exists ix_project_audit_events_procurement_import_batch_id
    on project_audit_events(procurement_import_batch_id)
    where procurement_import_batch_id is not null;

do $$
begin
    alter table project_audit_events drop constraint if exists ck_project_audit_events_entity_type;

    alter table project_audit_events add constraint ck_project_audit_events_entity_type
        check (entity_type in ('Project', 'PanelPlaceholder', 'Panel', 'ProcurementItem'));
exception
    when duplicate_object then null;
end $$;
