create table if not exists logistics_batch_panels (
    batch_id uuid not null references logistics_batches(id) on delete restrict,
    packing_unit_id uuid not null references logistics_packing_units(id) on delete restrict,
    panel_id uuid not null references panel_placeholders(id) on delete restrict,
    stage_code text not null,
    active boolean not null default true,
    added_by_user_id uuid not null references qms_users(id),
    added_at_utc timestamptz not null default now(),
    primary key (batch_id, panel_id),
    constraint fk_logistics_batch_panels_unit_panel
        foreign key (packing_unit_id, panel_id)
        references logistics_packing_unit_panels(packing_unit_id, panel_id)
        on delete restrict,
    constraint ck_logistics_batch_panels_stage
        check (stage_code in ('DepartureProcessed', 'DeliveryCompleted'))
);

insert into logistics_batch_panels (
    batch_id,
    packing_unit_id,
    panel_id,
    stage_code,
    active,
    added_by_user_id,
    added_at_utc
)
select
    batch_unit.batch_id,
    batch_unit.packing_unit_id,
    unit_panel.panel_id,
    batch_unit.stage_code,
    batch_unit.active,
    batch_unit.added_by_user_id,
    batch_unit.added_at_utc
from logistics_batch_units batch_unit
join logistics_packing_unit_panels unit_panel
  on unit_panel.packing_unit_id = batch_unit.packing_unit_id
 and unit_panel.active
on conflict (batch_id, panel_id) do nothing;

drop index if exists ux_logistics_batch_units_active_stage;

create unique index if not exists ux_logistics_batch_panels_active_stage
    on logistics_batch_panels(panel_id, stage_code)
    where active;

create index if not exists ix_logistics_batch_panels_unit_stage
    on logistics_batch_panels(packing_unit_id, stage_code, active);

create or replace function guard_finalized_logistics_child()
returns trigger language plpgsql as $$
declare old_owner_status text;
declare new_owner_status text;
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    if tg_op <> 'INSERT' then
        if tg_table_name = 'logistics_packing_unit_panels' then
            select status into old_owner_status from logistics_packing_units where id = old.packing_unit_id;
        elsif tg_table_name in ('logistics_batch_units', 'logistics_batch_panels', 'logistics_delivery_results') then
            select status into old_owner_status from logistics_batches where id = old.batch_id;
        elsif tg_table_name = 'logistics_evidence' then
            if old.packing_unit_id is not null then
                select status into old_owner_status from logistics_packing_units where id = old.packing_unit_id;
            else
                select status into old_owner_status from logistics_batches where id = old.batch_id;
            end if;
        end if;
    end if;
    if tg_op <> 'DELETE' then
        if tg_table_name = 'logistics_packing_unit_panels' then
            select status into new_owner_status from logistics_packing_units where id = new.packing_unit_id;
        elsif tg_table_name in ('logistics_batch_units', 'logistics_batch_panels', 'logistics_delivery_results') then
            select status into new_owner_status from logistics_batches where id = new.batch_id;
        elsif tg_table_name = 'logistics_evidence' then
            if new.packing_unit_id is not null then
                select status into new_owner_status from logistics_packing_units where id = new.packing_unit_id;
            else
                select status into new_owner_status from logistics_batches where id = new.batch_id;
            end if;
        end if;
    end if;
    if old_owner_status = 'Finalized' or new_owner_status = 'Finalized' then
        raise exception 'Finalized logistics evidence and membership are immutable.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

drop trigger if exists trg_guard_logistics_batch_panel on logistics_batch_panels;
create trigger trg_guard_logistics_batch_panel
before insert or update or delete on logistics_batch_panels
for each row execute function guard_finalized_logistics_child();
