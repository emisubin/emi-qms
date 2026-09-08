alter table user_roles
    add column if not exists assignment_source text not null default 'explicit';

do $migration$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_user_roles_assignment_source'
          and conrelid = 'user_roles'::regclass) then
        alter table user_roles
            add constraint ck_user_roles_assignment_source
            check (assignment_source in ('explicit', 'department-default', 'overall-administrator'));
    end if;
end
$migration$;

update user_roles user_role
set assignment_source = 'department-default'
from qms_users user_account, departments department, roles role
where user_role.user_id = user_account.id
  and department.id = user_account.department_id
  and role.id = user_role.role_id
  and user_role.assignment_source = 'explicit'
  and role.code = case department.code
      when 'administration' then 'system-administrator'
      when 'sales' then 'sales'
      when 'design' then 'design'
      when 'production-planning' then 'production-planning'
      when 'procurement' then 'procurement'
      when 'materials' then 'materials'
      when 'manufacturing' then 'manufacturing'
      when 'quality' then 'quality'
      when 'logistics' then 'logistics'
      when 'readonly' then 'read-only'
      else null
  end;

insert into role_permissions (role_id, permission_id)
select role.id, permission.id
from roles role
cross join permissions permission
where role.code = 'system-administrator'
on conflict do nothing;

create or replace function grant_new_permission_to_system_administrator()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
begin
    insert into public.role_permissions (role_id, permission_id)
    select role.id, new.id
    from public.roles role
    where role.code = 'system-administrator'
    on conflict do nothing;
    return new;
end;
$$;

drop trigger if exists trg_grant_new_permission_to_system_administrator on permissions;
create trigger trg_grant_new_permission_to_system_administrator
after insert on permissions
for each row execute function grant_new_permission_to_system_administrator();

revoke all on function grant_new_permission_to_system_administrator() from public;
