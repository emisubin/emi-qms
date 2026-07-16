insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000026', 'Pending.Read', 'Read pending issues'),
    ('30000000-0000-0000-0000-000000000027', 'Pending.Manage', 'Manage pending issues')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Pending.Read'
where roles.code in (
    'system-administrator',
    'sales',
    'design',
    'procurement',
    'materials',
    'production-planning',
    'manufacturing',
    'quality',
    'logistics',
    'read-only'
)
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Pending.Manage'
where roles.code in (
    'sales',
    'design',
    'procurement',
    'materials',
    'production-planning',
    'manufacturing',
    'quality',
    'logistics'
)
on conflict do nothing;

create sequence if not exists pending_issue_number_seq start with 1 increment by 1;

create table if not exists pending_issues (
    id uuid primary key default uuid_generate_v4(),
    issue_number bigint not null default nextval('pending_issue_number_seq'),
    project_id uuid not null references projects(id),
    target_type text not null default 'Project',
    target_id uuid not null,
    issue_type text not null,
    title text not null,
    description text not null,
    status text not null,
    priority text not null default 'Normal',
    assignee_user_id uuid null references qms_users(id),
    due_date date null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    closed_by_user_id uuid null references qms_users(id),
    closed_at_utc timestamptz null,
    constraint ux_pending_issues_number unique (issue_number),
    constraint ck_pending_issues_target_type check (target_type in ('Project', 'Panel', 'ProcurementItem', 'ProductionPlan', 'Inspection')),
    constraint ck_pending_issues_type check (issue_type in ('Nonconformance', 'Punch', 'ManufacturingStop', 'Other')),
    constraint ck_pending_issues_status check (status in ('Registered', 'ActionRequested', 'InProgress', 'ReinspectionRequested', 'Closed')),
    constraint ck_pending_issues_priority check (priority in ('Normal', 'Urgent')),
    constraint ck_pending_issues_title check (char_length(btrim(title)) between 3 and 160),
    constraint ck_pending_issues_description check (char_length(btrim(description)) between 10 and 2000),
    constraint ck_pending_issues_version check (version >= 1),
    constraint ck_pending_issues_assignee check (status = 'Registered' or assignee_user_id is not null),
    constraint ck_pending_issues_closed check (
        (status = 'Closed' and closed_at_utc is not null and closed_by_user_id is not null)
        or (status <> 'Closed' and closed_at_utc is null and closed_by_user_id is null)
    )
);

create index if not exists ix_pending_issues_open_priority
    on pending_issues(status, priority, due_date, updated_at_utc desc)
    where status <> 'Closed';

create index if not exists ix_pending_issues_project
    on pending_issues(project_id, status, updated_at_utc desc);

create index if not exists ix_pending_issues_assignee
    on pending_issues(assignee_user_id, status, updated_at_utc desc)
    where assignee_user_id is not null;

create table if not exists pending_comments (
    id uuid primary key default uuid_generate_v4(),
    pending_issue_id uuid not null references pending_issues(id) on delete cascade,
    body text not null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_pending_comments_body check (char_length(btrim(body)) between 1 and 2000)
);

create index if not exists ix_pending_comments_issue
    on pending_comments(pending_issue_id, created_at_utc, id);

create table if not exists pending_history (
    id uuid primary key default uuid_generate_v4(),
    pending_issue_id uuid not null references pending_issues(id) on delete cascade,
    event_type text not null,
    from_status text null,
    to_status text null,
    from_assignee_user_id uuid null references qms_users(id),
    to_assignee_user_id uuid null references qms_users(id),
    reason text null,
    changed_by_user_id uuid not null references qms_users(id),
    correlation_id text null,
    created_at_utc timestamptz not null default now(),
    constraint ck_pending_history_event check (event_type in ('Created', 'StatusChanged', 'AssigneeChanged')),
    constraint ck_pending_history_reason check (reason is null or char_length(btrim(reason)) between 1 and 500)
);

create index if not exists ix_pending_history_issue
    on pending_history(pending_issue_id, created_at_utc, id);
