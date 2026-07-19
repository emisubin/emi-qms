insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000043', 'Sales.Target.Manage', 'Manage monthly sales targets')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select role.id, permission.id
from roles role
join permissions permission on permission.code = 'Sales.Target.Manage'
where role.code = 'system-administrator'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'Sales.Target.Manage'
  and roles.code <> 'system-administrator';

create table if not exists sales_monthly_targets (
    id uuid primary key default uuid_generate_v4(),
    target_year integer not null,
    target_month integer not null,
    currency_code text not null,
    amount numeric(18, 2) not null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_sales_monthly_targets_period unique (target_year, target_month, currency_code),
    constraint ck_sales_monthly_targets_year check (target_year between 2000 and 2100),
    constraint ck_sales_monthly_targets_month check (target_month between 1 and 12),
    constraint ck_sales_monthly_targets_currency check (currency_code ~ '^[A-Z]{3}$'),
    constraint ck_sales_monthly_targets_amount check (amount >= 0),
    constraint ck_sales_monthly_targets_version check (version >= 1)
);

create index if not exists ix_sales_monthly_targets_year_currency
    on sales_monthly_targets(target_year, currency_code, target_month);

create table if not exists sales_monthly_target_audit_events (
    id uuid primary key default uuid_generate_v4(),
    target_id uuid not null references sales_monthly_targets(id) on delete restrict,
    target_year integer not null,
    target_month integer not null,
    currency_code text not null,
    action text not null,
    previous_amount numeric(18, 2) null,
    next_amount numeric(18, 2) not null,
    actor_user_id uuid not null references qms_users(id),
    occurred_at_utc timestamptz not null default now(),
    constraint ck_sales_target_audit_year check (target_year between 2000 and 2100),
    constraint ck_sales_target_audit_month check (target_month between 1 and 12),
    constraint ck_sales_target_audit_currency check (currency_code ~ '^[A-Z]{3}$'),
    constraint ck_sales_target_audit_action check (action in ('Create', 'Update')),
    constraint ck_sales_target_audit_amounts check (
        next_amount >= 0
        and (previous_amount is null or previous_amount >= 0)
        and ((action = 'Create' and previous_amount is null) or action = 'Update')
    )
);

create index if not exists ix_sales_target_audit_period
    on sales_monthly_target_audit_events(target_year, currency_code, target_month, occurred_at_utc desc);

create or replace function guard_append_only_sales_monthly_target_audit()
returns trigger language plpgsql as $$
begin
    raise exception 'Sales monthly target audit events are append-only.';
end $$;

drop trigger if exists trg_guard_sales_monthly_target_audit on sales_monthly_target_audit_events;
create trigger trg_guard_sales_monthly_target_audit
before update or delete on sales_monthly_target_audit_events
for each row execute function guard_append_only_sales_monthly_target_audit();
