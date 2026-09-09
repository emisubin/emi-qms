alter table projects
    add column if not exists project_profile text not null default 'Cheongju';
alter table projects
    add column if not exists osan_po_number text;
alter table projects
    add column if not exists osan_work_order_number text;
alter table projects
    add column if not exists osan_product_name text;
alter table projects
    add column if not exists osan_quantity integer;

do $$
begin
    alter table projects add constraint ck_projects_project_profile
        check (project_profile in ('Cheongju', 'Osan'));
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table projects add constraint ck_projects_osan_registration_fields
        check (
            project_profile <> 'Osan'
            or (
                project_title is not null
                and length(btrim(project_title)) between 1 and 200
                and project_code is not null
                and length(btrim(project_code)) between 1 and 80
                and project_code = btrim(project_code)
                and customer_name is not null
                and length(btrim(customer_name)) between 1 and 200
                and delivery_date is not null
                and osan_product_name is not null
                and length(btrim(osan_product_name)) between 1 and 100
                and osan_quantity between 1 and 500
                and (osan_po_number is null or length(osan_po_number) between 1 and 100)
                and (osan_work_order_number is null or length(osan_work_order_number) between 1 and 100)
            )
        );
exception
    when duplicate_object then null;
end $$;

drop index if exists ux_projects_project_title_normalized_active;

create unique index ux_projects_project_title_normalized_active
    on projects(project_title_normalized)
    where project_profile = 'Cheongju'
      and project_title_normalized is not null
      and deleted_at_utc is null;

create unique index if not exists ux_projects_osan_project_code
    on projects(project_code)
    where project_profile = 'Osan';

create table if not exists osan_project_targets (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    sequence_number integer not null,
    display_name text not null,
    status text not null default 'NotStarted',
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_osan_project_targets_sequence check (sequence_number between 1 and 500),
    constraint ck_osan_project_targets_display_name check (length(btrim(display_name)) between 1 and 220),
    constraint ck_osan_project_targets_status check (status in ('NotStarted', 'InProgress', 'Completed')),
    constraint ux_osan_project_targets_project_sequence unique (project_id, sequence_number),
    constraint ux_osan_project_targets_project_id unique (project_id, id)
);

create index if not exists ix_osan_project_targets_project_id
    on osan_project_targets(project_id);

create table if not exists osan_project_target_steps (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    target_id uuid not null,
    sequence_number integer not null,
    step_code text not null,
    step_name text not null,
    status text not null default 'NotStarted',
    started_at_utc timestamptz,
    completed_at_utc timestamptz,
    completed_by_user_id uuid references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_osan_project_target_steps_sequence check (sequence_number between 1 and 7),
    constraint ck_osan_project_target_steps_status check (status in ('NotStarted', 'InProgress', 'Completed')),
    constraint fk_osan_project_target_steps_target
        foreign key (project_id, target_id)
        references osan_project_targets(project_id, id) on delete restrict,
    constraint ux_osan_project_target_steps_target_sequence unique (target_id, sequence_number),
    constraint ux_osan_project_target_steps_target_code unique (target_id, step_code)
);

create index if not exists ix_osan_project_target_steps_project_id
    on osan_project_target_steps(project_id);

create table if not exists osan_project_create_operations (
    operation_id uuid primary key,
    request_fingerprint text not null,
    created_by_user_id uuid not null references qms_users(id),
    project_id uuid unique references projects(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    completed_at_utc timestamptz,
    constraint ck_osan_project_create_operations_fingerprint
        check (request_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_osan_project_create_operations_completion
        check ((project_id is null) = (completed_at_utc is null))
);

create table if not exists osan_project_events (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    event_type text not null,
    actor_user_id uuid not null references qms_users(id),
    recipient_user_id uuid not null references qms_users(id),
    occurred_at_utc timestamptz not null default now(),
    constraint ck_osan_project_events_type check (event_type = 'ProjectCreated'),
    constraint ux_osan_project_events_project_type unique (project_id, event_type)
);

create index if not exists ix_osan_project_events_recipient_occurred
    on osan_project_events(recipient_user_id, occurred_at_utc desc);

drop trigger if exists trg_qms_global_audit_osan_project_targets on osan_project_targets;
create trigger trg_qms_global_audit_osan_project_targets
after insert or update or delete on osan_project_targets
for each row execute function qms_audit_capture_row_change();

drop trigger if exists trg_qms_global_audit_osan_project_target_steps on osan_project_target_steps;
create trigger trg_qms_global_audit_osan_project_target_steps
after insert or update or delete on osan_project_target_steps
for each row execute function qms_audit_capture_row_change();
