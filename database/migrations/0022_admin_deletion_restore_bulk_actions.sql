alter table qms_users
    add column if not exists pre_delete_is_active boolean null;

alter table departments
    add column if not exists pre_delete_is_active boolean null;

alter table system_holidays
    add column if not exists pre_delete_is_active boolean null;

create index if not exists ix_qms_users_deletion_restore
    on qms_users(deletion_requested_at_utc, purge_blocked_at_utc)
    where deletion_requested_at_utc is not null or purge_blocked_at_utc is not null;

create index if not exists ix_departments_deletion_restore
    on departments(deletion_requested_at_utc, purge_blocked_at_utc)
    where deletion_requested_at_utc is not null or purge_blocked_at_utc is not null;

create index if not exists ix_system_holidays_deletion_restore
    on system_holidays(deletion_requested_at_utc, purge_blocked_at_utc)
    where deletion_requested_at_utc is not null or purge_blocked_at_utc is not null;
