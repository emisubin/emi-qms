insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000010', 'Project.Read.All', 'Read all projects'),
    ('30000000-0000-0000-0000-000000000011', 'Project.SalesAmount.Read', 'Read project sales amounts'),
    ('30000000-0000-0000-0000-000000000012', 'Manufacturing.WorkTime.Read', 'Read manufacturing work time')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Project.Read.All'
where roles.code in (
    'system-administrator',
    'sales',
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
join permissions on permissions.code in (
    'Project.SalesAmount.Read',
    'Manufacturing.WorkTime.Read'
)
where roles.code in ('system-administrator', 'sales')
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code in (
      'Project.SalesAmount.Read',
      'Manufacturing.WorkTime.Read'
  )
  and roles.code not in ('system-administrator', 'sales');
