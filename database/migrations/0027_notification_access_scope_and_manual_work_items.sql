alter table notifications
    add column if not exists visibility_scope text not null default 'RecipientOnly',
    add column if not exists source_kind text not null default 'Automatic',
    add column if not exists work_item_id uuid null references work_items(id) on delete set null,
    add column if not exists manual_requested_by_user_id uuid null references qms_users(id) on delete set null;

do $$
begin
    alter table notifications
        drop constraint if exists ck_notifications_visibility_scope;

    alter table notifications
        add constraint ck_notifications_visibility_scope
        check (visibility_scope in ('RecipientOnly', 'Authenticated', 'AdminOnly'));

    alter table notifications
        drop constraint if exists ck_notifications_source_kind;

    alter table notifications
        add constraint ck_notifications_source_kind
        check (source_kind in ('Automatic', 'Manual', 'ChannelNotice', 'WorkAssignment', 'DailyDigest', 'Escalation', 'System'));
end $$;

create index if not exists ix_notifications_visibility_created
    on notifications(visibility_scope, created_at_utc desc);

create index if not exists ix_notifications_work_item
    on notifications(work_item_id)
    where work_item_id is not null;

create index if not exists ix_notifications_manual_requested_by
    on notifications(manual_requested_by_user_id, created_at_utc desc)
    where manual_requested_by_user_id is not null;
