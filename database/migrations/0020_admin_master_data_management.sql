insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000025', 'admin-history.read', 'Read administrator history')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'admin-history.read'
where roles.code = 'system-administrator'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'admin-history.read'
  and roles.code <> 'system-administrator';

alter table departments
    add column if not exists is_active boolean not null default true,
    add column if not exists sort_order integer,
    add column if not exists updated_at_utc timestamptz not null default now(),
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

alter table qms_users
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

alter table system_holidays
    add column if not exists deletion_requested_at_utc timestamptz null,
    add column if not exists scheduled_hard_delete_at_utc timestamptz null,
    add column if not exists purge_blocked_at_utc timestamptz null,
    add column if not exists purge_blocked_reason text null;

update departments
set sort_order = case code
    when 'administration' then 10
    when 'sales' then 20
    when 'design' then 30
    when 'production-planning' then 40
    when 'procurement' then 50
    when 'materials' then 60
    when 'manufacturing' then 70
    when 'quality' then 80
    when 'logistics' then 90
    when 'readonly' then 100
    else 1000
end
where sort_order is null;

alter table departments
    alter column sort_order set not null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_departments_sort_order_positive'
    ) then
        alter table departments
            add constraint ck_departments_sort_order_positive check (sort_order >= 0);
    end if;
end $$;

create table if not exists admin_master_change_logs (
    id uuid primary key default uuid_generate_v4(),
    entity_type text not null,
    entity_id uuid null,
    action text not null,
    before_json text null,
    after_json text null,
    reason text null,
    changed_by_user_id uuid null references qms_users(id),
    changed_at_utc timestamptz not null default now(),
    constraint ck_admin_master_change_logs_entity_type_not_blank check (btrim(entity_type) <> ''),
    constraint ck_admin_master_change_logs_action_not_blank check (btrim(action) <> '')
);

create index if not exists ix_admin_master_change_logs_entity
    on admin_master_change_logs(entity_type, changed_at_utc desc);

create index if not exists ix_admin_master_change_logs_changed_by
    on admin_master_change_logs(changed_by_user_id, changed_at_utc desc);

create index if not exists ix_qms_users_scheduled_hard_delete
    on qms_users(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;

create index if not exists ix_departments_scheduled_hard_delete
    on departments(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;

create index if not exists ix_system_holidays_scheduled_hard_delete
    on system_holidays(scheduled_hard_delete_at_utc)
    where deletion_requested_at_utc is not null;
