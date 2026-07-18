create table data_export_events (
    id uuid primary key,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    export_kind text not null,
    row_count integer not null,
    filters_applied boolean not null,
    sensitive_sales_amount_included boolean not null,
    succeeded_at_utc timestamptz not null default now(),
    constraint ck_data_export_events_kind check (export_kind in ('Projects', 'ProcurementDashboard', 'MyWork')),
    constraint ck_data_export_events_row_count check (row_count between 0 and 10000),
    constraint ck_data_export_events_sensitive_columns check (
        not sensitive_sales_amount_included or export_kind = 'Projects'
    )
);

create index ix_data_export_events_actor_time
    on data_export_events(actor_user_id, succeeded_at_utc desc);

create or replace function guard_append_only_data_export_event()
returns trigger language plpgsql as $$
begin
    raise exception 'Data export events are append-only.';
end $$;

create trigger trg_guard_data_export_event
before update or delete on data_export_events
for each row execute function guard_append_only_data_export_event();
