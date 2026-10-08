insert into permissions (id, code, name)
values
    ('30000000-0000-0000-0000-000000000087', 'G2.ProductionPlan.Update', 'Update G2 daily production plans'),
    ('30000000-0000-0000-0000-000000000088', 'G2.DeliveryPlan.Update', 'Update G2 daily delivery plans'),
    ('30000000-0000-0000-0000-000000000089', 'G2.PlanNote.Update', 'Update G2 daily plan notes'),
    ('30000000-0000-0000-0000-000000000090', 'G2.Note.Update', 'Update G2 daily actual and row notes')
on conflict (code) do update set name = excluded.name;

delete from role_permissions role_permission
using roles role, permissions permission
where role_permission.role_id = role.id
  and role_permission.permission_id = permission.id
  and role.code <> 'system-administrator'
  and permission.code = any(array[
      'G2.Production.Update',
      'G2.Delivery.Update',
      'G2.Inventory.Manage',
      'G2.ProductionPlan.Update',
      'G2.DeliveryPlan.Update',
      'G2.PlanNote.Update',
      'G2.Note.Update'
  ]);

insert into role_permissions (role_id, permission_id)
select role.id, permission.id
from roles role
join permissions permission on permission.code = any(
    case role.code
        when 'system-administrator' then array[
            'G2.ProductionPlan.Update','G2.DeliveryPlan.Update','G2.PlanNote.Update','G2.Note.Update'
        ]
        when 'sales' then array[
            'G2.Delivery.Update','G2.Inventory.Manage','G2.ProductionPlan.Update',
            'G2.DeliveryPlan.Update','G2.PlanNote.Update','G2.Note.Update'
        ]
        when 'manufacturing' then array[
            'G2.Production.Update','G2.Inventory.Manage','G2.ProductionPlan.Update',
            'G2.PlanNote.Update','G2.Note.Update'
        ]
        when 'quality' then array[
            'G2.Production.Update','G2.Inventory.Manage','G2.Note.Update'
        ]
        else array[]::text[]
    end)
on conflict do nothing;

alter table g2_daily_metrics
    drop constraint if exists ux_g2_daily_metrics_date_code;

create unique index if not exists ux_g2_daily_metrics_actual_date_code
    on g2_daily_metrics(work_date, metric_code)
    where not is_forecast;

create unique index if not exists ux_g2_daily_metrics_forecast_date_code
    on g2_daily_metrics(work_date, metric_code)
    where is_forecast;

alter table g2_inventory_counts alter column quantity drop not null;
alter table g2_defect_inventory_counts alter column quantity drop not null;

create table g2_daily_values (
    id uuid primary key default uuid_generate_v4(),
    work_date date not null,
    value_code text not null,
    quantity integer null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_g2_daily_values_date_code unique (work_date, value_code),
    constraint ck_g2_daily_values_code check (value_code in ('ProductionPlan', 'DeliveryPlan')),
    constraint ck_g2_daily_values_quantity check (quantity is null or quantity >= 0),
    constraint ck_g2_daily_values_version check (version >= 1)
);

create index ix_g2_daily_values_date on g2_daily_values(work_date, value_code);

create table g2_daily_notes (
    id uuid primary key default uuid_generate_v4(),
    work_date date not null,
    note_code text not null,
    note_text text null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_g2_daily_notes_date_code unique (work_date, note_code),
    constraint ck_g2_daily_notes_code check (note_code in ('PlanNote', 'ActualNote', 'RowNote')),
    constraint ck_g2_daily_notes_text check (note_text is null or char_length(note_text) <= 2000),
    constraint ck_g2_daily_notes_version check (version >= 1)
);

create index ix_g2_daily_notes_date on g2_daily_notes(work_date, note_code);

create trigger trg_qms_global_audit_g2_daily_values
after insert or update or delete on g2_daily_values
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_g2_daily_notes
after insert or update or delete on g2_daily_notes
for each row execute function qms_audit_capture_row_change();
