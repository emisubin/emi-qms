-- Stage anomalies retain independent lifetime and immutable record/photo evidence.
alter table osan_progress_operations drop constraint ck_osan_progress_operations_action;
alter table osan_progress_operations add constraint ck_osan_progress_operations_action
 check(action in ('Start','Complete','IssueRegistered','IssueResolved'));
alter table osan_progress_operations drop constraint ck_osan_progress_operations_completion;
alter table osan_progress_operations add constraint ck_osan_progress_operations_completion check(
 (action='Start' and completion_mode is null and stage_sequence is null) or
 (action in ('Complete','IssueRegistered','IssueResolved') and completion_mode in ('individual','batch') and stage_sequence between 1 and 7));
alter table osan_stage_records drop constraint osan_stage_records_event_type_check;
alter table osan_stage_records add constraint osan_stage_records_event_type_check
 check(event_type in ('Complete','Edit','Request','Approve','Reject','Reset','IssueRegistered','IssueRecorded','IssueResolved'));
create table osan_stage_issues (
 id uuid primary key, project_id uuid not null, target_id uuid not null, step_id uuid not null,
 status text not null default 'Open' check(status in ('Open','Resolved','Reset')),
 registered_at_utc timestamptz not null default now(), registered_by_user_id uuid not null references qms_users(id),
 latest_record_id uuid not null references osan_stage_records(id),
 closed_at_utc timestamptz, closed_by_user_id uuid references qms_users(id), closed_record_id uuid references osan_stage_records(id),
 foreign key(project_id,target_id,step_id) references osan_project_target_steps(project_id,target_id,id),
 check((status='Open' and closed_at_utc is null and closed_by_user_id is null and closed_record_id is null)
    or (status<>'Open' and closed_at_utc is not null and closed_by_user_id is not null and closed_record_id is not null))
);
create unique index ux_osan_stage_issue_open on osan_stage_issues(step_id) where status='Open';
create index ix_osan_stage_issue_project on osan_stage_issues(project_id,target_id) where status='Open';
