create table if not exists ul891_set_design_slots (
    id uuid primary key,
    spec_id uuid not null references ul891_set_specs(id) on delete cascade,
    position_number integer not null,
    internal_code text not null,
    panel_name text null,
    panel_specification text null,
    width_mm numeric(12,2) null,
    height_mm numeric(12,2) null,
    depth_mm numeric(12,2) null,
    status text not null default 'Active',
    row_version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_ul891_set_design_slots_code unique (spec_id, internal_code),
    constraint ck_ul891_set_design_slots_position check (position_number between 1 and 200),
    constraint ck_ul891_set_design_slots_code check (char_length(btrim(internal_code)) between 1 and 30),
    constraint ck_ul891_set_design_slots_name check (panel_name is null or char_length(btrim(panel_name)) between 1 and 200),
    constraint ck_ul891_set_design_slots_specification check (panel_specification is null or char_length(btrim(panel_specification)) between 1 and 500),
    constraint ck_ul891_set_design_slots_status check (status in ('Active', 'Removed')),
    constraint ck_ul891_set_design_slots_version check (row_version >= 1),
    constraint ck_ul891_set_design_slots_dimensions check (
        (width_mm is null or width_mm >= 0)
        and (height_mm is null or height_mm >= 0)
        and (depth_mm is null or depth_mm >= 0)
    )
);

create unique index if not exists ux_ul891_set_design_slots_active_position
    on ul891_set_design_slots(spec_id, position_number)
    where status = 'Active';

create index if not exists ix_ul891_set_design_slots_spec
    on ul891_set_design_slots(spec_id, status, position_number);

with ranked_versions as (
    select version.id,
           version.spec_id,
           version.created_by_user_id,
           row_number() over (
               partition by version.spec_id
               order by
                   case version.status when 'Draft' then 0 when 'Published' then 1 else 2 end,
                   version.version_number desc
           ) as rank
    from ul891_set_spec_versions version
), current_components as (
    select ranked.spec_id,
           ranked.created_by_user_id,
           component.component_code,
           component.panel_name,
           component.panel_specification,
           component.width_mm,
           component.height_mm,
           component.depth_mm,
           component.sort_order
    from ranked_versions ranked
    join ul891_set_spec_components component on component.spec_version_id = ranked.id
    where ranked.rank = 1
)
insert into ul891_set_design_slots (
    id, spec_id, position_number, internal_code,
    panel_name, panel_specification, width_mm, height_mm, depth_mm,
    created_by_user_id, updated_by_user_id
)
select uuid_generate_v4(), component.spec_id, component.sort_order, component.component_code,
       component.panel_name, component.panel_specification,
       component.width_mm, component.height_mm, component.depth_mm,
       component.created_by_user_id, component.created_by_user_id
from current_components component
on conflict (spec_id, internal_code) do nothing;

alter table panel_placeholders
    add column if not exists design_slot_id uuid null references ul891_set_design_slots(id) on delete restrict;

with panel_positions as (
    select panel.id as panel_id,
           instance.spec_id,
           component.sort_order
    from panel_placeholders panel
    join ul891_set_instances instance on instance.id = panel.set_instance_id
    join ul891_set_spec_components component
      on component.spec_version_id = instance.spec_version_id
     and component.component_code = panel.component_code
    where panel.status = 'Active'
)
update panel_placeholders panel
set design_slot_id = slot.id
from panel_positions position
join ul891_set_design_slots slot
  on slot.spec_id = position.spec_id
 and slot.position_number = position.sort_order
 and slot.status = 'Active'
where panel.id = position.panel_id
  and panel.design_slot_id is null;

do $$
begin
    if exists (
        select 1
        from panel_placeholders panel
        where panel.set_instance_id is not null
          and panel.status = 'Active'
          and panel.design_slot_id is null
    ) then
        raise exception 'Active UL891 panels must map to a current design position.';
    end if;
end $$;

create unique index if not exists ux_panel_placeholders_active_set_design_slot
    on panel_placeholders(set_instance_id, design_slot_id)
    where set_instance_id is not null
      and design_slot_id is not null
      and status = 'Active';

create index if not exists ix_panel_placeholders_design_slot
    on panel_placeholders(design_slot_id)
    where design_slot_id is not null;

create table if not exists project_production_plan_set_defaults (
    id uuid primary key,
    production_plan_id uuid not null references project_production_plans(id) on delete cascade,
    row_version integer not null default 1,
    created_by_user_id uuid null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_project_production_plan_set_defaults_plan unique (production_plan_id),
    constraint ck_project_production_plan_set_defaults_version check (row_version >= 1)
);

create table if not exists project_production_plan_set_default_values (
    id uuid primary key,
    set_default_id uuid not null references project_production_plan_set_defaults(id) on delete cascade,
    production_plan_item_id uuid not null references project_production_plan_items(id) on delete cascade,
    planned_start_date date null,
    planned_end_date date null,
    assigned_user_id uuid null references qms_users(id) on delete restrict,
    required_headcount integer null,
    note text null,
    row_version integer not null default 1,
    updated_at_utc timestamptz not null default now(),
    constraint ux_project_production_plan_set_default_values_item unique (set_default_id, production_plan_item_id),
    constraint ck_project_production_plan_set_default_values_dates check (
        planned_start_date is null or planned_end_date is null or planned_end_date >= planned_start_date
    ),
    constraint ck_project_production_plan_set_default_values_headcount check (
        required_headcount is null or required_headcount between 1 and 999
    ),
    constraint ck_project_production_plan_set_default_values_note check (
        note is null or char_length(btrim(note)) between 1 and 1000
    ),
    constraint ck_project_production_plan_set_default_values_version check (row_version >= 1)
);

insert into project_production_plan_set_defaults (
    id, production_plan_id, created_by_user_id, updated_by_user_id
)
select uuid_generate_v4(), plan.id, plan.created_by_user_id, plan.created_by_user_id
from project_production_plans plan
join projects project on project.id = plan.project_id
where plan.model_version = 'LINKED_V1'
  and project.structure_mode = 'Ul891Set'
on conflict (production_plan_id) do nothing;

insert into project_production_plan_set_default_values (
    id, set_default_id, production_plan_item_id,
    planned_start_date, planned_end_date, assigned_user_id,
    required_headcount, note
)
select uuid_generate_v4(), defaults.id, item.id,
       item.planned_start_date, item.planned_end_date, item.assigned_user_id,
       item.required_headcount, item.note
from project_production_plan_set_defaults defaults
join project_production_plan_items item on item.production_plan_id = defaults.production_plan_id
on conflict (set_default_id, production_plan_item_id) do nothing;
