alter table notification_deliveries
    add column if not exists current_generation integer not null default 1,
    add column if not exists generation_attempt_count integer not null default 0;

update notification_deliveries
set generation_attempt_count = attempt_count
where current_generation = 1
  and generation_attempt_count = 0
  and attempt_count > 0;

alter table notification_delivery_attempts
    add column if not exists generation integer not null default 1;

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_current_generation;
    alter table notification_deliveries
        add constraint ck_notification_deliveries_current_generation
        check (current_generation between 1 and 5);

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_generation_attempt_count;
    alter table notification_deliveries
        add constraint ck_notification_deliveries_generation_attempt_count
        check (generation_attempt_count >= 0 and generation_attempt_count <= attempt_count);

    alter table notification_delivery_attempts
        drop constraint if exists ck_notification_delivery_attempts_generation;
    alter table notification_delivery_attempts
        add constraint ck_notification_delivery_attempts_generation
        check (generation between 1 and 5);
end $$;

create table if not exists notification_delivery_reprocess_events (
    id uuid primary key default uuid_generate_v4(),
    delivery_id uuid not null references notification_deliveries(id) on delete cascade,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    prior_generation integer not null,
    new_generation integer not null,
    prior_status text not null,
    prior_attempt_count integer not null,
    prior_generation_attempt_count integer not null,
    prior_error_code text null,
    prior_admin_handling_status text null,
    prior_admin_handling_note text null,
    reason text not null,
    duplicate_risk_acknowledged boolean not null,
    occurred_at_utc timestamptz not null default now(),
    constraint ux_notification_delivery_reprocess_generation unique (delivery_id, new_generation),
    constraint ck_notification_delivery_reprocess_generation check (
        prior_generation between 1 and 4 and new_generation = prior_generation + 1
    ),
    constraint ck_notification_delivery_reprocess_prior_status check (prior_status = 'Failed'),
    constraint ck_notification_delivery_reprocess_attempts check (
        prior_attempt_count >= 0
        and prior_generation_attempt_count >= 0
        and prior_generation_attempt_count <= prior_attempt_count
    ),
    constraint ck_notification_delivery_reprocess_reason check (
        char_length(btrim(reason)) between 10 and 500
    ),
    constraint ck_notification_delivery_reprocess_ack check (duplicate_risk_acknowledged = true)
);

create index if not exists ix_notification_delivery_attempts_generation
    on notification_delivery_attempts(delivery_id, generation, attempt_no desc);

create index if not exists ix_notification_delivery_reprocess_events_delivery
    on notification_delivery_reprocess_events(delivery_id, occurred_at_utc desc);

create index if not exists ix_notification_delivery_reprocess_events_actor
    on notification_delivery_reprocess_events(actor_user_id, occurred_at_utc desc);
