alter table osan_project_targets
    add column if not exists version integer not null default 1;
alter table osan_project_targets
    add column if not exists started_at_utc timestamptz;
alter table osan_project_targets
    add column if not exists started_by_user_id uuid references qms_users(id);

do $migration$
begin
    if not exists (
        select 1 from pg_constraint
        where conname = 'ck_osan_project_targets_version'
          and conrelid = 'osan_project_targets'::regclass) then
        alter table osan_project_targets
            add constraint ck_osan_project_targets_version check (version >= 1);
    end if;
end
$migration$;

create unique index if not exists ux_osan_project_target_steps_project_target_id
    on osan_project_target_steps(project_id, target_id, id);

create table if not exists osan_progress_operations (
    operation_id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    action text not null,
    completion_mode text,
    stage_sequence integer,
    target_ids uuid[] not null,
    request_fingerprint text not null,
    requested_by_user_id uuid not null references qms_users(id),
    completed_at_utc timestamptz not null default now(),
    constraint ck_osan_progress_operations_action
        check (action in ('Start', 'Complete')),
    constraint ck_osan_progress_operations_completion
        check (
            (action = 'Start' and completion_mode is null and stage_sequence is null)
            or
            (action = 'Complete'
             and completion_mode in ('individual', 'batch')
             and stage_sequence between 1 and 7)
        ),
    constraint ck_osan_progress_operations_targets
        check (cardinality(target_ids) between 1 and 500),
    constraint ck_osan_progress_operations_fingerprint
        check (request_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ux_osan_progress_operations_project_id unique (project_id, operation_id)
);

create index if not exists ix_osan_progress_operations_project
    on osan_progress_operations(project_id, completed_at_utc desc);

create table if not exists osan_progress_photos (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    operation_id uuid not null,
    original_file_name text not null,
    display_order integer not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    content bytea not null,
    uploaded_by_user_id uuid not null references qms_users(id),
    uploaded_at_utc timestamptz not null default now(),
    constraint ck_osan_progress_photos_file_name
        check (length(btrim(original_file_name)) between 1 and 255),
    constraint ck_osan_progress_photos_display_order
        check (display_order between 1 and 5),
    constraint ck_osan_progress_photos_mime
        check (normalized_mime in ('image/jpeg', 'image/png')),
    constraint ck_osan_progress_photos_size
        check (byte_size between 1 and 5242880 and octet_length(content) = byte_size),
    constraint ck_osan_progress_photos_sha256
        check (sha256 ~ '^[0-9a-f]{64}$'),
    constraint fk_osan_progress_photos_operation
        foreign key (project_id, operation_id)
        references osan_progress_operations(project_id, operation_id) on delete restrict,
    constraint ux_osan_progress_photos_operation_sha unique (operation_id, sha256),
    constraint ux_osan_progress_photos_operation_order unique (operation_id, display_order),
    constraint ux_osan_progress_photos_project_id unique (project_id, id)
);

create index if not exists ix_osan_progress_photos_project
    on osan_progress_photos(project_id, uploaded_at_utc desc);

create table if not exists osan_progress_step_photos (
    project_id uuid not null,
    target_id uuid not null,
    step_id uuid not null,
    photo_id uuid not null,
    stage_sequence integer not null,
    linked_at_utc timestamptz not null default now(),
    primary key (step_id, photo_id),
    constraint fk_osan_progress_step_photos_target
        foreign key (project_id, target_id)
        references osan_project_targets(project_id, id) on delete restrict,
    constraint fk_osan_progress_step_photos_step
        foreign key (project_id, target_id, step_id)
        references osan_project_target_steps(project_id, target_id, id) on delete restrict,
    constraint fk_osan_progress_step_photos_photo
        foreign key (project_id, photo_id)
        references osan_progress_photos(project_id, id) on delete restrict,
    constraint ck_osan_progress_step_photos_stage
        check (stage_sequence between 1 and 7)
);

create index if not exists ix_osan_progress_step_photos_photo
    on osan_progress_step_photos(photo_id);

create or replace function guard_osan_progress_append_only()
returns trigger
language plpgsql
as $$
begin
    raise exception 'Osan progress evidence is append-only.' using errcode = '55000';
end;
$$;

drop trigger if exists trg_guard_osan_progress_operations on osan_progress_operations;
create trigger trg_guard_osan_progress_operations
before update or delete on osan_progress_operations
for each row execute function guard_osan_progress_append_only();

drop trigger if exists trg_guard_osan_progress_photos on osan_progress_photos;
create trigger trg_guard_osan_progress_photos
before update or delete on osan_progress_photos
for each row execute function guard_osan_progress_append_only();

drop trigger if exists trg_guard_osan_progress_step_photos on osan_progress_step_photos;
create trigger trg_guard_osan_progress_step_photos
before update or delete on osan_progress_step_photos
for each row execute function guard_osan_progress_append_only();

revoke all on function guard_osan_progress_append_only() from public;
