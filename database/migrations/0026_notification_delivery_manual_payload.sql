alter table notification_deliveries
    add column if not exists manual_payload_json jsonb null,
    add column if not exists manual_requested_by_user_id uuid null references qms_users(id) on delete set null,
    add column if not exists manual_requested_at_utc timestamptz null;

create index if not exists ix_notification_deliveries_manual_requested_by
    on notification_deliveries(manual_requested_by_user_id, manual_requested_at_utc desc)
    where manual_requested_by_user_id is not null;
