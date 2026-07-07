alter table qms_users
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

alter table departments
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

alter table system_holidays
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

create index if not exists ix_qms_users_scheduled_hard_delete
    on qms_users(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;

create index if not exists ix_departments_scheduled_hard_delete
    on departments(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;

create index if not exists ix_system_holidays_scheduled_hard_delete
    on system_holidays(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;
