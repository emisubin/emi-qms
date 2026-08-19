insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000081', 'G2.Read', 'Read G2 operations'),
    ('30000000-0000-0000-0000-000000000082', 'G2.Production.Update', 'Update G2 production quantities'),
    ('30000000-0000-0000-0000-000000000083', 'G2.Delivery.Update', 'Update G2 delivery quantities'),
    ('30000000-0000-0000-0000-000000000084', 'G2.Attendance.Update', 'Update G2 attendance quantities'),
    ('30000000-0000-0000-0000-000000000085', 'G2.Inventory.Manage', 'Manage G2 physical inventory counts'),
    ('30000000-0000-0000-0000-000000000086', 'G2.Target.Manage', 'Manage G2 production and inventory targets')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select role.id, permission.id from roles role
join permissions permission on permission.code = 'G2.Read'
where role.code = any(array['system-administrator','sales','production-planning','manufacturing','quality','logistics','read-only','design','procurement','materials'])
on conflict do nothing;

insert into role_permissions (role_id, permission_id)
select role.id, permission.id from roles role
join permissions permission on permission.code = any(
    case role.code
        when 'system-administrator' then array['G2.Production.Update','G2.Delivery.Update','G2.Attendance.Update','G2.Inventory.Manage','G2.Target.Manage']
        when 'sales' then array['G2.Production.Update','G2.Delivery.Update','G2.Attendance.Update','G2.Inventory.Manage','G2.Target.Manage']
        when 'manufacturing' then array['G2.Production.Update','G2.Attendance.Update','G2.Inventory.Manage','G2.Target.Manage']
        when 'logistics' then array['G2.Delivery.Update']
        else array[]::text[]
    end)
on conflict do nothing;

create table if not exists g2_daily_metrics (
    id uuid primary key default uuid_generate_v4(),
    work_date date not null,
    metric_code text not null,
    quantity integer null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_g2_daily_metrics_date_code unique (work_date, metric_code),
    constraint ck_g2_daily_metrics_code check (metric_code in ('MorningProduction','AfternoonProduction','Delivery','MorningEmiAttendance','MorningContractorAttendance','AfternoonEmiAttendance','AfternoonContractorAttendance')),
    constraint ck_g2_daily_metrics_quantity check (quantity is null or quantity >= 0),
    constraint ck_g2_daily_metrics_version check (version >= 1)
);
create index if not exists ix_g2_daily_metrics_date on g2_daily_metrics(work_date, metric_code);

create table if not exists g2_inventory_counts (
    id uuid primary key default uuid_generate_v4(),
    count_date date not null unique,
    quantity integer not null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_g2_inventory_counts_quantity check (quantity >= 0),
    constraint ck_g2_inventory_counts_version check (version >= 1)
);
create index if not exists ix_g2_inventory_counts_date on g2_inventory_counts(count_date);

create table if not exists g2_targets (
    id uuid primary key default uuid_generate_v4(),
    target_type text not null,
    effective_date date not null,
    quantity integer not null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_g2_targets_type_date unique (target_type, effective_date),
    constraint ck_g2_targets_type check (target_type in ('DailyProduction','Inventory')),
    constraint ck_g2_targets_quantity check (quantity >= 0),
    constraint ck_g2_targets_version check (version >= 1)
);
create index if not exists ix_g2_targets_type_date on g2_targets(target_type, effective_date);
