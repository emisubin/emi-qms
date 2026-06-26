insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000020', 'Audit.Read.All', 'Read all audit history')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'Audit.Read.All'
where roles.code = 'system-administrator'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'Audit.Read.All'
  and roles.code <> 'system-administrator';

alter table panel_information_excel_import_batches
    add column if not exists skipped_panel_count integer not null default 0;

do $$
begin
    alter table panel_information_excel_import_batches add constraint ck_panel_information_excel_import_batches_skipped_count
        check (skipped_panel_count >= 0);
exception
    when duplicate_object then null;
end $$;
