create table if not exists osan_notification_global_preference_profiles (
    scope_id smallint primary key check (scope_id = 1),
    version bigint not null default 0,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_osan_notification_global_preference_profiles_version check (version >= 0)
);

create table if not exists osan_notification_global_preferences (
    scope_id smallint not null references osan_notification_global_preference_profiles(scope_id),
    event_kind text not null,
    channel text not null,
    stage_sequence smallint not null default 0,
    is_enabled boolean not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    primary key (scope_id, event_kind, channel, stage_sequence),
    constraint ck_osan_notification_global_preferences_sparse_opt_out check (is_enabled = false),
    constraint ck_osan_notification_global_preferences_kind check (event_kind in (
        'ProjectCreated','StepCompleted','StepRejected','StepEdited',
        'StepIssueRegistered','StepIssueResolved','ProjectCompleted'
    )),
    constraint ck_osan_notification_global_preferences_channel check (channel in ('Mail','WebPush')),
    constraint ck_osan_notification_global_preferences_stage check (
        (event_kind = 'StepCompleted' and stage_sequence between 0 and 7)
        or (event_kind <> 'StepCompleted' and stage_sequence = 0)
    )
);

create index if not exists ix_osan_notification_global_preferences_user
    on osan_notification_global_preferences(scope_id);


-- Personal preferences remain untouched for a later return to individual control.
insert into osan_notification_global_preference_profiles(scope_id) values(1) on conflict do nothing;

create trigger trg_qms_global_audit_osan_notification_global_preference_profiles
after insert or update or delete on osan_notification_global_preference_profiles
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_osan_notification_global_preferences
after insert or update or delete on osan_notification_global_preferences
for each row execute function qms_audit_capture_row_change();
