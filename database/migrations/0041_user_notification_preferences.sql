create table if not exists user_notification_preference_profiles (
    user_id uuid primary key references qms_users(id) on delete cascade,
    version bigint not null default 0,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_user_notification_preference_profiles_version check (version >= 0)
);

create table if not exists user_notification_preferences (
    user_id uuid not null references qms_users(id) on delete cascade,
    delivery_type text not null,
    channel text not null,
    is_enabled boolean not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    primary key (user_id, delivery_type, channel),
    constraint ck_user_notification_preferences_sparse_opt_out check (is_enabled = false),
    constraint ck_user_notification_preferences_supported_pair check (
        (delivery_type = 'WorkItemCreated' and channel = 'TeamsDirectMessage')
        or (delivery_type = 'DueSoonL0' and channel = 'TeamsDirectMessage')
        or (delivery_type = 'DailyDigest' and channel = 'Mail')
    )
);

create index if not exists ix_user_notification_preferences_user
    on user_notification_preferences(user_id);

create table if not exists user_notification_preference_audit_events (
    id uuid primary key default uuid_generate_v4(),
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    target_user_id uuid not null references qms_users(id) on delete restrict,
    action text not null,
    delivery_type text not null,
    channel text not null,
    old_value boolean not null,
    new_value boolean not null,
    resulting_version bigint not null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_user_notification_preference_audit_action check (
        action in ('Save', 'Reset', 'AdminSave', 'AdminReset')
    ),
    constraint ck_user_notification_preference_audit_changed check (old_value <> new_value),
    constraint ck_user_notification_preference_audit_version check (resulting_version > 0),
    constraint ck_user_notification_preference_audit_supported_pair check (
        (delivery_type = 'WorkItemCreated' and channel = 'TeamsDirectMessage')
        or (delivery_type = 'DueSoonL0' and channel = 'TeamsDirectMessage')
        or (delivery_type = 'DailyDigest' and channel = 'Mail')
    )
);

create index if not exists ix_user_notification_preference_audit_target
    on user_notification_preference_audit_events(target_user_id, occurred_at_utc desc);

create index if not exists ix_user_notification_preference_audit_actor
    on user_notification_preference_audit_events(actor_user_id, occurred_at_utc desc);
