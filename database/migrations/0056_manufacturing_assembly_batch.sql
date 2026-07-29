create table if not exists panel_manufacturing_assembly_batch_operations (
    operation_id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    requested_by_user_id uuid not null references qms_users(id) on delete restrict,
    panel_ids uuid[] not null,
    payload_fingerprint text not null,
    completed_panel_count integer not null,
    checked_step_count integer not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_manufacturing_assembly_batch_panels check (
        cardinality(panel_ids) between 1 and 500
        and completed_panel_count = cardinality(panel_ids)
        and checked_step_count >= completed_panel_count
    ),
    constraint ck_panel_manufacturing_assembly_batch_fingerprint check (
        payload_fingerprint ~ '^[0-9a-f]{64}$'
    )
);

create index if not exists ix_panel_manufacturing_assembly_batch_project_created
    on panel_manufacturing_assembly_batch_operations(project_id, created_at_utc desc);

alter table panel_manufacturing_events
    add column if not exists batch_operation_id uuid null
        references panel_manufacturing_assembly_batch_operations(operation_id) on delete restrict;

create index if not exists ix_panel_manufacturing_events_batch_operation
    on panel_manufacturing_events(batch_operation_id, execution_id, created_at_utc, id)
    where batch_operation_id is not null;
