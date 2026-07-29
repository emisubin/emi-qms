alter table pending_issues
    add column if not exists action_department_code text null references departments(code);

update pending_issues issue
set action_department_code = department.code
from qms_users actor
join departments department on department.id = actor.department_id
where issue.issue_type = 'ManufacturingStop'
  and issue.action_department_code is null
  and actor.id = issue.created_by_user_id;

alter table pending_issues
    drop constraint if exists ck_pending_issues_manufacturing_department;

alter table pending_issues
    add constraint ck_pending_issues_manufacturing_department check (
        issue_type <> 'ManufacturingStop' or action_department_code is not null
    );

create index if not exists ix_pending_issues_action_department
    on pending_issues(action_department_code, status, updated_at_utc desc)
    where action_department_code is not null and status <> 'Closed';

create table if not exists panel_manufacturing_executions (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id),
    panel_id uuid not null references panel_placeholders(id),
    status text not null,
    started_by_user_id uuid not null references qms_users(id),
    started_at_utc timestamptz not null default now(),
    completed_by_user_id uuid null references qms_users(id),
    completed_at_utc timestamptz null,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    active_stop_pending_id uuid null references pending_issues(id) on delete set null,
    version integer not null default 1,
    updated_at_utc timestamptz not null default now(),
    constraint ck_panel_manufacturing_executions_status check (
        status in ('InProgress', 'Blocked', 'Completed', 'Cancelled')
    ),
    constraint ck_panel_manufacturing_executions_version check (version >= 1),
    constraint ck_panel_manufacturing_executions_terminal_time check (
        (status = 'Completed' and completed_at_utc is not null and completed_by_user_id is not null and cancelled_at_utc is null)
        or (status = 'Cancelled' and cancelled_at_utc is not null and cancelled_by_user_id is not null and completed_at_utc is null)
        or (status in ('InProgress', 'Blocked') and completed_at_utc is null and cancelled_at_utc is null)
    ),
    constraint ck_panel_manufacturing_executions_stop_link check (
        (status = 'Blocked' and active_stop_pending_id is not null)
        or (status <> 'Blocked' and active_stop_pending_id is null)
    )
);

create unique index if not exists ux_panel_manufacturing_executions_active_panel
    on panel_manufacturing_executions(panel_id)
    where status in ('InProgress', 'Blocked');

create index if not exists ix_panel_manufacturing_executions_project
    on panel_manufacturing_executions(project_id, status, updated_at_utc desc);

create table if not exists panel_manufacturing_execution_steps (
    id uuid primary key default uuid_generate_v4(),
    execution_id uuid not null references panel_manufacturing_executions(id),
    sequence_number integer not null,
    step_name text not null,
    checked_by_user_id uuid null references qms_users(id),
    checked_at_utc timestamptz null,
    constraint ux_panel_manufacturing_execution_steps_sequence unique (execution_id, sequence_number),
    constraint ck_panel_manufacturing_execution_steps_sequence check (sequence_number between 1 and 4),
    constraint ck_panel_manufacturing_execution_steps_name check (char_length(btrim(step_name)) between 2 and 80),
    constraint ck_panel_manufacturing_execution_steps_checked check (
        (checked_by_user_id is null and checked_at_utc is null)
        or (checked_by_user_id is not null and checked_at_utc is not null)
    )
);

create index if not exists ix_panel_manufacturing_execution_steps_execution
    on panel_manufacturing_execution_steps(execution_id, sequence_number);

create table if not exists panel_manufacturing_events (
    id uuid primary key default uuid_generate_v4(),
    execution_id uuid not null references panel_manufacturing_executions(id),
    event_type text not null,
    step_id uuid null references panel_manufacturing_execution_steps(id),
    pending_issue_id uuid null references pending_issues(id) on delete set null,
    stop_reason_code text null,
    stop_description text null,
    actor_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_manufacturing_events_type check (
        event_type in ('Started', 'StepChecked', 'Stopped', 'Resumed', 'Completed', 'Cancelled')
    ),
    constraint ck_panel_manufacturing_events_step check (
        (event_type = 'StepChecked' and step_id is not null)
        or (event_type <> 'StepChecked' and step_id is null)
    ),
    constraint ck_panel_manufacturing_events_stop check (
        (event_type = 'Stopped'
            and pending_issue_id is not null
            and stop_reason_code in ('Material', 'Staffing', 'WorkUnavailable', 'Other')
            and char_length(btrim(stop_description)) between 10 and 1000)
        or (event_type <> 'Stopped' and stop_reason_code is null and stop_description is null)
    )
);

create index if not exists ix_panel_manufacturing_events_execution
    on panel_manufacturing_events(execution_id, created_at_utc, id);

create unique index if not exists ux_panel_manufacturing_events_active_stop_pending
    on panel_manufacturing_events(pending_issue_id)
    where pending_issue_id is not null and event_type = 'Stopped';

create table if not exists panel_manufacturing_operations (
    operation_id uuid primary key,
    action text not null,
    project_id uuid not null references projects(id),
    panel_id uuid not null references panel_placeholders(id),
    execution_id uuid null references panel_manufacturing_executions(id),
    requested_by_user_id uuid not null references qms_users(id),
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_panel_manufacturing_operations_action check (
        action in ('Start', 'CheckStep', 'Stop', 'Resume', 'Complete')
    ),
    constraint ck_panel_manufacturing_operations_fingerprint check (
        payload_fingerprint ~ '^[0-9a-f]{64}$'
    ),
    constraint ck_panel_manufacturing_operations_projection check (
        jsonb_typeof(result_projection) = 'object'
    )
);

create index if not exists ix_panel_manufacturing_operations_panel
    on panel_manufacturing_operations(panel_id, created_at_utc desc);
