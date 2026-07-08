alter table notification_deliveries
    add column if not exists admin_handling_status text null,
    add column if not exists admin_handled_at_utc timestamptz null,
    add column if not exists admin_handled_by_user_id uuid null references qms_users(id) on delete set null,
    add column if not exists admin_handling_note text null;

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_admin_handling_status;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_admin_handling_status
        check (admin_handling_status is null or admin_handling_status in ('Open', 'Acknowledged', 'Dismissed'));
end $$;

create index if not exists ix_notification_deliveries_admin_handling
    on notification_deliveries(status, admin_handling_status, created_at_utc desc);

create index if not exists ix_notification_deliveries_admin_handled_by
    on notification_deliveries(admin_handled_by_user_id, admin_handled_at_utc desc)
    where admin_handled_by_user_id is not null;
