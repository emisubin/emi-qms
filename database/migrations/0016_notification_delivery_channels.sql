create table if not exists notification_deliveries (
    id uuid primary key default uuid_generate_v4(),
    notification_id uuid null references notifications(id) on delete cascade,
    notification_recipient_id uuid null references notification_recipients(id) on delete cascade,
    recipient_user_id uuid null references qms_users(id) on delete set null,
    project_id uuid null references projects(id) on delete cascade,
    work_item_id uuid null references work_items(id) on delete set null,
    channel text not null,
    delivery_type text not null,
    status text not null default 'Pending',
    attempt_count integer not null default 0,
    next_attempt_at_utc timestamptz null,
    last_attempt_at_utc timestamptz null,
    sent_at_utc timestamptz null,
    suppressed_at_utc timestamptz null,
    error_code text null,
    error_message text null,
    dedupe_key text not null,
    group_key text null,
    provider_message_id text null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_notification_deliveries_channel check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'Mail')),
    constraint ck_notification_deliveries_delivery_type check (delivery_type in ('WorkItemCreated', 'ReferenceDigest', 'UrgentBlocking', 'DailyDigest', 'ProjectCompletion', 'ManualTest')),
    constraint ck_notification_deliveries_status check (status in ('Pending', 'Sent', 'Failed', 'Suppressed', 'Disabled', 'DryRunSent')),
    constraint ck_notification_deliveries_attempt_count check (attempt_count >= 0),
    constraint ck_notification_deliveries_error_code_not_blank check (error_code is null or btrim(error_code) <> ''),
    constraint ck_notification_deliveries_dedupe_key_not_blank check (btrim(dedupe_key) <> '')
);

create unique index if not exists ux_notification_deliveries_notification_recipient_channel_type
    on notification_deliveries(notification_id, recipient_user_id, channel, delivery_type)
    where notification_id is not null and recipient_user_id is not null;

create unique index if not exists ux_notification_deliveries_notification_channel_type
    on notification_deliveries(notification_id, channel, delivery_type)
    where notification_id is not null and recipient_user_id is null;

create unique index if not exists ux_notification_deliveries_daily_digest
    on notification_deliveries(recipient_user_id, channel, delivery_type, dedupe_key)
    where delivery_type = 'DailyDigest' and recipient_user_id is not null;

create index if not exists ix_notification_deliveries_dedupe_key
    on notification_deliveries(dedupe_key);

create index if not exists ix_notification_deliveries_next_attempt
    on notification_deliveries(status, next_attempt_at_utc, created_at_utc);

create index if not exists ix_notification_deliveries_status
    on notification_deliveries(status, created_at_utc desc);

create index if not exists ix_notification_deliveries_recipient
    on notification_deliveries(recipient_user_id, created_at_utc desc);

create index if not exists ix_notification_deliveries_notification
    on notification_deliveries(notification_id);
