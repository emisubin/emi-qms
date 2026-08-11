create table if not exists web_push_subscriptions (
    id uuid primary key default uuid_generate_v4(),
    user_id uuid not null references qms_users(id) on delete cascade,
    endpoint text not null,
    endpoint_hash text not null,
    p256dh_key text not null,
    auth_key text not null,
    generation bigint not null default 1,
    is_active boolean not null default true,
    activated_at_utc timestamptz not null default now(),
    deactivated_at_utc timestamptz null,
    deactivation_reason text null,
    last_success_at_utc timestamptz null,
    last_failure_at_utc timestamptz null,
    last_failure_code text null,
    consecutive_failure_count integer not null default 0,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_web_push_subscriptions_endpoint_hash unique (endpoint_hash),
    constraint ck_web_push_subscriptions_endpoint_not_blank check (btrim(endpoint) <> ''),
    constraint ck_web_push_subscriptions_endpoint_hash_not_blank check (btrim(endpoint_hash) <> ''),
    constraint ck_web_push_subscriptions_p256dh_not_blank check (btrim(p256dh_key) <> ''),
    constraint ck_web_push_subscriptions_auth_not_blank check (btrim(auth_key) <> ''),
    constraint ck_web_push_subscriptions_failure_count check (consecutive_failure_count >= 0),
    constraint ck_web_push_subscriptions_generation check (generation > 0),
    constraint ck_web_push_subscriptions_active_state check (
        (is_active = true and deactivated_at_utc is null and deactivation_reason is null)
        or (is_active = false and deactivated_at_utc is not null and btrim(deactivation_reason) <> '')
    )
);

create index if not exists ix_web_push_subscriptions_active_user
    on web_push_subscriptions(user_id, activated_at_utc)
    where is_active = true;

create table if not exists web_push_subscription_events (
    id uuid primary key default uuid_generate_v4(),
    subscription_id uuid not null references web_push_subscriptions(id) on delete cascade,
    user_id uuid null references qms_users(id) on delete set null,
    event_type text not null,
    reason text null,
    created_at_utc timestamptz not null default now(),
    constraint ck_web_push_subscription_events_type check (
        event_type in ('Registered', 'Reactivated', 'CurrentDeviceDeactivated', 'AllDevicesDeactivated', 'AccountDeactivated', 'ProviderAccepted', 'ProviderFailed', 'ProviderDeactivated')
    ),
    constraint ck_web_push_subscription_events_reason_not_blank check (reason is null or btrim(reason) <> '')
);

create index if not exists ix_web_push_subscription_events_subscription
    on web_push_subscription_events(subscription_id, created_at_utc desc);

alter table notification_deliveries
    add column if not exists web_push_subscription_id uuid null references web_push_subscriptions(id) on delete cascade;

alter table notification_deliveries
    add column if not exists web_push_subscription_generation bigint null;

drop index if exists ux_notification_deliveries_notification_recipient_channel_type;
drop index if exists ux_notification_deliveries_notification_channel_type;

create unique index ux_notification_deliveries_notification_recipient_channel_type
    on notification_deliveries(notification_id, recipient_user_id, channel, delivery_type)
    where notification_id is not null
      and recipient_user_id is not null
      and channel <> 'WebPush';

create unique index ux_notification_deliveries_notification_channel_type
    on notification_deliveries(notification_id, channel, delivery_type)
    where notification_id is not null
      and recipient_user_id is null
      and channel <> 'WebPush';

create unique index ux_notification_deliveries_web_push_subscription
    on notification_deliveries(notification_id, web_push_subscription_id, delivery_type)
    where channel = 'WebPush'
      and notification_id is not null
      and web_push_subscription_id is not null;

create index if not exists ix_notification_deliveries_web_push_subscription
    on notification_deliveries(web_push_subscription_id, created_at_utc desc)
    where web_push_subscription_id is not null;

do $$
begin
    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_channel;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_channel
        check (channel in ('TeamsChannel', 'TeamsDirectMessage', 'TeamsActivity', 'Mail', 'WebPush'));

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_delivery_type;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_delivery_type
        check (delivery_type in (
            'WorkItemCreated',
            'ReferenceDigest',
            'UrgentBlocking',
            'DailyDigest',
            'ProjectCompletion',
            'ManualTest',
            'DueSoonL0',
            'OverdueL1',
            'OverdueL2',
            'OverdueL3',
            'WebPushNotification'
        ));

    alter table notification_deliveries
        drop constraint if exists ck_notification_deliveries_web_push_target;

    alter table notification_deliveries
        add constraint ck_notification_deliveries_web_push_target
        check (
            (channel = 'WebPush'
                and web_push_subscription_id is not null
                and web_push_subscription_generation is not null
                and web_push_subscription_generation > 0)
            or (channel <> 'WebPush'
                and web_push_subscription_id is null
                and web_push_subscription_generation is null)
        );
end $$;
