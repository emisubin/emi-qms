create table if not exists osan_notification_preference_profiles (
    user_id uuid primary key references qms_users(id) on delete cascade,
    version bigint not null default 0,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_osan_notification_preference_profiles_version check (version >= 0)
);

create table if not exists osan_notification_preferences (
    user_id uuid not null references qms_users(id) on delete cascade,
    event_kind text not null,
    channel text not null,
    stage_sequence smallint not null default 0,
    is_enabled boolean not null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    primary key (user_id, event_kind, channel, stage_sequence),
    constraint ck_osan_notification_preferences_sparse_opt_out check (is_enabled = false),
    constraint ck_osan_notification_preferences_kind check (event_kind in (
        'ProjectCreated','StepCompleted','StepRejected','StepEdited',
        'StepIssueRegistered','StepIssueResolved','ProjectCompleted'
    )),
    constraint ck_osan_notification_preferences_channel check (channel in ('Mail','WebPush')),
    constraint ck_osan_notification_preferences_stage check (
        (event_kind = 'StepCompleted' and stage_sequence between 0 and 7)
        or (event_kind <> 'StepCompleted' and stage_sequence = 0)
    )
);

create index if not exists ix_osan_notification_preferences_user
    on osan_notification_preferences(user_id);

-- Keep a stable event identity separate from delivery rows. In-app recipients always
-- remain, while mail and Web Push can re-check the latest preference before dispatch.
create table if not exists osan_notification_events (
    notification_id uuid primary key references notifications(id) on delete cascade,
    event_kind text not null,
    stage_sequence smallint not null default 0,
    constraint ck_osan_notification_events_kind check (event_kind in (
        'ProjectCreated','StepCompleted','StepRejected','StepEdited',
        'StepIssueRegistered','StepIssueResolved','ProjectCompleted'
    )),
    constraint ck_osan_notification_events_stage check (
        (event_kind = 'StepCompleted' and stage_sequence between 1 and 7)
        or (event_kind <> 'StepCompleted' and stage_sequence = 0)
    )
);

-- Older queued Osan mail snapshots used the default numeric enum JSON form.
-- Backfill their event identity so a preference changed after enqueue is still honored.
insert into osan_notification_events(notification_id,event_kind,stage_sequence)
select distinct on (delivery.notification_id)
    delivery.notification_id,
    case delivery.manual_payload_json->>'Kind'
        when '0' then 'ProjectCreated'
        when '1' then 'StepCompleted'
        when '2' then 'StepRejected'
        when '3' then 'StepEdited'
        when '4' then 'ProjectCompleted'
        when '5' then 'StepIssueRegistered'
        when '6' then 'StepIssueResolved'
        else delivery.manual_payload_json->>'Kind'
    end,
    case when delivery.manual_payload_json->>'Kind' in ('1','StepCompleted') then
        case delivery.manual_payload_json->>'StepName'
            when '입고검사' then 1 when '배치검사' then 2 when '배선검사' then 3
            when '8계통' then 4 when '동작검사' then 5 when '출하검사' then 6 when '포장' then 7
            else 0
        end
    else 0 end
from notification_deliveries delivery
join notifications notification on notification.id=delivery.notification_id
where delivery.delivery_type='OsanWorkflow'
  and delivery.manual_payload_json is not null
  and notification.source_kind='OsanWorkflow'
  and case delivery.manual_payload_json->>'Kind'
      when '0' then 'ProjectCreated' when '1' then 'StepCompleted'
      when '2' then 'StepRejected' when '3' then 'StepEdited' when '4' then 'ProjectCompleted'
      when '5' then 'StepIssueRegistered' when '6' then 'StepIssueResolved'
      else delivery.manual_payload_json->>'Kind'
  end in ('ProjectCreated','StepCompleted','StepRejected','StepEdited','ProjectCompleted')
  and (
      coalesce(delivery.manual_payload_json->>'Kind','') not in ('1','StepCompleted')
      or delivery.manual_payload_json->>'StepName' in ('입고검사','배치검사','배선검사','8계통','동작검사','출하검사','포장')
  )
on conflict(notification_id) do nothing;

drop trigger if exists trg_qms_global_audit_osan_notification_preference_profiles on osan_notification_preference_profiles;
create trigger trg_qms_global_audit_osan_notification_preference_profiles
after insert or update or delete on osan_notification_preference_profiles
for each row execute function qms_audit_capture_row_change();

drop trigger if exists trg_qms_global_audit_osan_notification_preferences on osan_notification_preferences;
create trigger trg_qms_global_audit_osan_notification_preferences
after insert or update or delete on osan_notification_preferences
for each row execute function qms_audit_capture_row_change();
