alter table projects add column if not exists packaging_method text;
alter table projects add column if not exists deleted_at_utc timestamptz;
alter table projects add column if not exists deleted_by_user_id uuid null references qms_users(id);
alter table projects add column if not exists delete_reason text;
alter table projects add column if not exists deleted_correlation_id text;

do $$
begin
    alter table projects add constraint ck_projects_packaging_method
        check (
            packaging_method is null
            or packaging_method in ('WoodenCrate', 'StretchWrap', 'HeavyDutyBox')
        );
exception
    when duplicate_object then null;
end $$;

do $$
begin
    alter table projects add constraint ck_projects_delete_reason_length
        check (delete_reason is null or length(delete_reason) <= 500);
exception
    when duplicate_object then null;
end $$;

drop index if exists ux_projects_project_title_normalized;

create unique index if not exists ux_projects_project_title_normalized_active
    on projects(project_title_normalized)
    where project_title_normalized is not null
      and deleted_at_utc is null;

create index if not exists ix_projects_deleted_at_utc on projects(deleted_at_utc);
create index if not exists ix_projects_status_not_deleted on projects(status) where deleted_at_utc is null;
create index if not exists ix_projects_deleted_by_user_id on projects(deleted_by_user_id) where deleted_at_utc is not null;

insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000017', 'Project.Delete', 'Soft delete sales projects'),
    ('30000000-0000-0000-0000-000000000018', 'Project.Deleted.Read', 'Read soft deleted projects')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Project.Delete'
where roles.code = 'sales'
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Project.Deleted.Read'
where roles.code in ('system-administrator', 'sales')
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'Project.Delete'
  and roles.code <> 'sales';

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'Project.Deleted.Read'
  and roles.code not in ('system-administrator', 'sales');
