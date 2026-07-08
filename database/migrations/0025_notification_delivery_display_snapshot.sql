alter table notification_deliveries
    add column if not exists display_title text null,
    add column if not exists display_message text null,
    add column if not exists display_project_name text null,
    add column if not exists display_work_item_title text null,
    add column if not exists display_recipient_name text null,
    add column if not exists display_recipient_email text null,
    add column if not exists display_recipient_kind text null,
    add column if not exists display_channel_target text null,
    add column if not exists manual_notification_kind text null,
    add column if not exists correlation_id text null;

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_display_recipient_kind;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_display_recipient_kind
        check (display_recipient_kind is null or display_recipient_kind in ('User', 'Email', 'TeamsChannel', 'Unknown'));

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_manual_notification_kind;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_manual_notification_kind
        check (manual_notification_kind is null or manual_notification_kind in ('ProjectCreated', 'WorkItemAssigned', 'Urgent', 'DailyDigest', 'Custom'));
end $$;

create index if not exists ix_notification_deliveries_correlation_id
    on notification_deliveries(correlation_id)
    where correlation_id is not null;

create index if not exists ix_notification_deliveries_manual_notification_kind
    on notification_deliveries(manual_notification_kind, created_at_utc desc)
    where manual_notification_kind is not null;
