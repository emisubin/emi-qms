create extension if not exists "uuid-ossp";

create table if not exists departments (
    id uuid primary key,
    code text not null unique,
    name text not null
);

create table if not exists qms_users (
    id uuid primary key,
    development_user_key text not null unique,
    display_name text not null,
    department_id uuid not null references departments(id),
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now()
);

create table if not exists roles (
    id uuid primary key,
    code text not null unique,
    name text not null
);

create table if not exists permissions (
    id uuid primary key,
    code text not null unique,
    name text not null
);

create table if not exists user_roles (
    user_id uuid not null references qms_users(id) on delete cascade,
    role_id uuid not null references roles(id) on delete cascade,
    primary key (user_id, role_id)
);

create table if not exists role_permissions (
    role_id uuid not null references roles(id) on delete cascade,
    permission_id uuid not null references permissions(id) on delete cascade,
    primary key (role_id, permission_id)
);

create table if not exists projects (
    id uuid primary key,
    project_key text not null unique,
    project_number text not null unique,
    name text not null,
    created_at_utc timestamptz not null default now()
);

create table if not exists user_project_access (
    user_id uuid not null references qms_users(id) on delete cascade,
    project_id uuid not null references projects(id) on delete cascade,
    primary key (user_id, project_id)
);

create table if not exists authorization_audit_events (
    id uuid primary key default uuid_generate_v4(),
    occurred_at_utc timestamptz not null default now(),
    user_id uuid null references qms_users(id),
    reason text not null,
    endpoint text not null,
    target_project_key text null
);

create index if not exists ix_qms_users_department_id on qms_users(department_id);
create index if not exists ix_user_roles_role_id on user_roles(role_id);
create index if not exists ix_role_permissions_permission_id on role_permissions(permission_id);
create index if not exists ix_user_project_access_project_id on user_project_access(project_id);
create index if not exists ix_authorization_audit_events_occurred_at_utc on authorization_audit_events(occurred_at_utc);

insert into roles (id, code, name)
values
    ('20000000-0000-0000-0000-000000000001', 'system-administrator', 'System Administrator'),
    ('20000000-0000-0000-0000-000000000002', 'sales', 'Sales User'),
    ('20000000-0000-0000-0000-000000000003', 'production-planning', 'Production Planning User'),
    ('20000000-0000-0000-0000-000000000004', 'manufacturing', 'Manufacturing User'),
    ('20000000-0000-0000-0000-000000000005', 'quality', 'Quality User'),
    ('20000000-0000-0000-0000-000000000006', 'logistics', 'Logistics User'),
    ('20000000-0000-0000-0000-000000000007', 'read-only', 'Read Only User')
on conflict (code) do update set name = excluded.name;

insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000001', 'projects.read', 'Read projects'),
    ('30000000-0000-0000-0000-000000000002', 'projects.manage', 'Manage project basics'),
    ('30000000-0000-0000-0000-000000000003', 'projects.access.all', 'Access every project'),
    ('30000000-0000-0000-0000-000000000004', 'production.plan', 'Manage production plans'),
    ('30000000-0000-0000-0000-000000000005', 'manufacturing.update', 'Update manufacturing records'),
    ('30000000-0000-0000-0000-000000000006', 'quality.inspect', 'Record quality inspection'),
    ('30000000-0000-0000-0000-000000000007', 'quality.approve', 'Approve quality release'),
    ('30000000-0000-0000-0000-000000000008', 'logistics.ship', 'Manage packing and shipping'),
    ('30000000-0000-0000-0000-000000000009', 'users.manage', 'Manage users and roles')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
cross join permissions
where roles.code = 'system-administrator'
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = any(
    case roles.code
        when 'sales' then array['projects.read', 'projects.manage']
        when 'production-planning' then array['projects.read', 'production.plan']
        when 'manufacturing' then array['projects.read', 'manufacturing.update']
        when 'quality' then array['projects.read', 'quality.inspect', 'quality.approve']
        when 'logistics' then array['projects.read', 'logistics.ship']
        when 'read-only' then array['projects.read']
        else array[]::text[]
    end
)
where roles.code <> 'system-administrator'
on conflict do nothing;
