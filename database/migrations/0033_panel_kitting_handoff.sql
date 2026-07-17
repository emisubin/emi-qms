create table if not exists panel_kitting_batches (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete restrict,
    operation_id uuid not null unique,
    requested_by_user_id uuid not null references qms_users(id) on delete restrict,
    panel_set_fingerprint text not null,
    completed_panel_count integer not null,
    generated_work_item_count integer not null,
    project_kitting_completed boolean not null,
    readiness_active_item_count integer not null,
    readiness_completed_item_count integer not null,
    readiness_predicate_version integer not null default 1,
    readiness_verified_at_utc timestamptz not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_kitting_batches_fingerprint
        check (panel_set_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_panel_kitting_batches_counts
        check (
            completed_panel_count > 0
            and generated_work_item_count between 0 and completed_panel_count
            and readiness_active_item_count > 0
            and readiness_completed_item_count = readiness_active_item_count
        ),
    constraint ck_panel_kitting_batches_predicate_version
        check (readiness_predicate_version = 1)
);

create index if not exists ix_panel_kitting_batches_project_created
    on panel_kitting_batches(project_id, created_at_utc desc);

create table if not exists panel_kitting_completions (
    id uuid primary key default uuid_generate_v4(),
    batch_id uuid not null references panel_kitting_batches(id) on delete restrict,
    project_id uuid not null references projects(id) on delete restrict,
    panel_id uuid not null references panel_placeholders(id) on delete restrict,
    completed_by_user_id uuid not null references qms_users(id) on delete restrict,
    completed_at_utc timestamptz not null default now(),
    constraint ux_panel_kitting_completions_panel unique (panel_id)
);

create index if not exists ix_panel_kitting_completions_project
    on panel_kitting_completions(project_id, completed_at_utc desc);

create index if not exists ix_panel_kitting_completions_batch
    on panel_kitting_completions(batch_id);
