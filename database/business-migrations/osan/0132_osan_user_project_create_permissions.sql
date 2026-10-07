-- Move Osan project creation from a department-derived grant to an explicit
-- per-user setting. Existing eligible users are backfilled; future users have
-- no row and therefore start denied. Administrators remain allowed by role.

create table osan_user_project_create_permissions (
    user_id uuid primary key references qms_users(id) on delete cascade,
    allowed boolean not null,
    version bigint not null default 1 check (version > 0),
    updated_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id) on delete restrict
);

create table osan_user_project_create_permission_events (
    id uuid primary key default uuid_generate_v4(),
    user_id uuid not null references qms_users(id) on delete restrict,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    before_allowed boolean not null,
    after_allowed boolean not null,
    version bigint not null check (version > 0),
    occurred_at_utc timestamptz not null default now()
);

create index ix_osan_user_project_create_permission_events_user_time
    on osan_user_project_create_permission_events(user_id, occurred_at_utc desc);

insert into osan_user_project_create_permissions(user_id, allowed, version)
select user_account.id, true, 1
from qms_users user_account
join departments department on department.id = user_account.department_id
where user_account.is_active = true
  and department.code in ('sales', 'production-planning');

create trigger trg_osan_user_project_create_permission_events_append_only
before update or delete on osan_user_project_create_permission_events
for each row execute function qms_audit_guard_append_only();
