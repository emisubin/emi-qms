alter table osan_notification_events
    drop constraint ck_osan_notification_events_kind;
alter table osan_notification_events
    add constraint ck_osan_notification_events_kind check (event_kind in (
        'ProjectCreated','StepCompleted','StepRejected','StepEdited',
        'StepIssueRegistered','StepIssueResolved','ProjectCompleted','StepWorkRequested'
    ));

alter table osan_notification_global_preferences
    drop constraint ck_osan_notification_global_preferences_kind;
alter table osan_notification_global_preferences
    add constraint ck_osan_notification_global_preferences_kind check (event_kind in (
        'ProjectCreated','StepCompleted','StepRejected','StepEdited',
        'StepIssueRegistered','StepIssueResolved','ProjectCompleted','StepWorkRequested'
    ));

alter table osan_stage_records
    drop constraint osan_stage_records_event_type_check;
alter table osan_stage_records
    add constraint osan_stage_records_event_type_check check(event_type in (
        'Complete','Edit','Request','Approve','Reject','Reset',
        'IssueRegistered','IssueRecorded','IssueResolved','WorkRequested'
    ));

create table osan_stage_work_requests (
    operation_id uuid primary key,
    project_id uuid not null,
    target_id uuid not null,
    step_id uuid not null,
    stage_sequence smallint not null check(stage_sequence between 1 and 7),
    requested_by_user_id uuid not null references qms_users(id),
    requested_at_utc timestamptz not null default now(),
    payload_fingerprint text not null check(length(payload_fingerprint) = 64),
    history_record_id uuid not null unique references osan_stage_records(id),
    foreign key(project_id,target_id,step_id)
        references osan_project_target_steps(project_id,target_id,id)
);

create index ix_osan_stage_work_requests_history
    on osan_stage_work_requests(project_id,step_id,requested_at_utc desc);

create table osan_stage_work_request_recipients (
    operation_id uuid not null references osan_stage_work_requests(operation_id),
    recipient_user_id uuid not null references qms_users(id),
    display_name_snapshot text not null,
    department_name_snapshot text,
    primary key(operation_id,recipient_user_id)
);

create trigger trg_guard_osan_stage_work_requests
before update or delete on osan_stage_work_requests
for each row execute function guard_osan_progress_append_only();

create trigger trg_guard_osan_stage_work_request_recipients
before update or delete on osan_stage_work_request_recipients
for each row execute function guard_osan_progress_append_only();

create trigger trg_qms_global_audit_osan_stage_work_requests
after insert or update or delete on osan_stage_work_requests
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_osan_stage_work_request_recipients
after insert or update or delete on osan_stage_work_request_recipients
for each row execute function qms_audit_capture_row_change();
