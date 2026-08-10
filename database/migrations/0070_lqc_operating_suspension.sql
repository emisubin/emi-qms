update workflow_stages
set is_active = true
where stage_code = 'LQC';

alter table panel_quality_template_versions
    add column if not exists product_type_id uuid null
        references production_product_types(id) on delete restrict;

alter table panel_quality_template_versions
    drop constraint if exists ux_panel_quality_template_versions;

drop index if exists ux_panel_quality_template_versions_active;

create unique index if not exists ux_panel_quality_template_versions_scope_version
    on panel_quality_template_versions (
        stage_code,
        coalesce(product_type_id, '00000000-0000-0000-0000-000000000000'::uuid),
        version_number
    );

create unique index if not exists ux_panel_quality_template_versions_scope_active
    on panel_quality_template_versions (
        stage_code,
        coalesce(product_type_id, '00000000-0000-0000-0000-000000000000'::uuid)
    )
    where lifecycle_status = 'Active' and is_active;

create table if not exists lqc_item_settings (
    product_type_id uuid primary key references production_product_types(id) on delete restrict,
    is_operational boolean not null default false,
    current_template_version_id uuid not null
        references panel_quality_template_versions(id) on delete restrict,
    row_version integer not null default 1,
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_lqc_item_settings_row_version check (row_version >= 1)
);

do $$
declare
    product_type record;
    source_version_id uuid;
    next_version_id uuid;
begin
    select id into source_version_id
    from panel_quality_template_versions
    where stage_code = 'LQC'
      and product_type_id is null
      and lifecycle_status = 'Active'
      and is_active
    order by version_number desc
    limit 1;

    if source_version_id is null then
        raise exception 'Active legacy LQC template is required before migration 0070.';
    end if;

    for product_type in
        select id, code, name
        from production_product_types
        order by code
    loop
        if not exists (
            select 1
            from lqc_item_settings setting
            where setting.product_type_id = product_type.id
        ) then
            next_version_id := uuid_generate_v4();

            insert into panel_quality_template_versions (
                id,
                stage_code,
                product_type_id,
                version_number,
                display_name,
                is_active,
                activated_at_utc,
                lifecycle_status,
                row_version,
                created_by_user_id,
                created_at_utc,
                updated_by_user_id,
                updated_at_utc
            )
            select
                next_version_id,
                'LQC',
                product_type.id,
                1,
                product_type.name || ' LQC 검사',
                false,
                null,
                'Draft',
                1,
                source.created_by_user_id,
                now(),
                source.updated_by_user_id,
                now()
            from panel_quality_template_versions source
            where source.id = source_version_id;

            insert into panel_quality_template_items (
                id,
                template_version_id,
                item_code,
                display_order,
                label,
                guidance,
                response_type,
                is_required,
                requires_photo,
                max_text_length,
                definition_key
            )
            select
                uuid_generate_v4(),
                next_version_id,
                item.item_code,
                item.display_order,
                item.label,
                item.guidance,
                item.response_type,
                item.is_required,
                item.requires_photo,
                item.max_text_length,
                item.definition_key
            from panel_quality_template_items item
            where item.template_version_id = source_version_id
            order by item.display_order;

            update panel_quality_template_versions
            set lifecycle_status = 'Active',
                is_active = true,
                activated_at_utc = now(),
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = next_version_id;

            insert into lqc_item_settings (
                product_type_id,
                is_operational,
                current_template_version_id
            )
            values (product_type.id, false, next_version_id);
        end if;
    end loop;
end $$;

create table if not exists lqc_item_setting_audit_events (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null references production_product_types(id) on delete restrict,
    action text not null,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    old_value jsonb not null,
    new_value jsonb not null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_lqc_item_setting_audit_action check (
        action in ('OperatingStatusChanged', 'TemplateChanged')
    ),
    constraint ck_lqc_item_setting_audit_old_value check (
        jsonb_typeof(old_value) = 'object' and octet_length(old_value::text) <= 4096
    ),
    constraint ck_lqc_item_setting_audit_new_value check (
        jsonb_typeof(new_value) = 'object' and octet_length(new_value::text) <= 4096
    )
);

create index if not exists ix_lqc_item_setting_audit_time
    on lqc_item_setting_audit_events(product_type_id, occurred_at_utc desc, id);

create or replace function guard_lqc_item_setting_audit_append_only()
returns trigger language plpgsql as $$
begin
    raise exception 'LQC item setting audit events are append-only.' using errcode = 'P0001';
end $$;

drop trigger if exists trg_guard_lqc_item_setting_audit_append_only on lqc_item_setting_audit_events;
create trigger trg_guard_lqc_item_setting_audit_append_only
before update or delete on lqc_item_setting_audit_events
for each row execute function guard_lqc_item_setting_audit_append_only();

alter table projects
    add column if not exists lqc_operational_snapshot boolean null,
    add column if not exists lqc_template_version_id uuid null
        references panel_quality_template_versions(id) on delete restrict;

update projects
set lqc_operational_snapshot = true,
    lqc_template_version_id = coalesce(
        lqc_template_version_id,
        (
            select version.id
            from panel_quality_template_versions version
            where version.stage_code = 'LQC'
              and version.product_type_id is null
              and version.lifecycle_status = 'Active'
              and version.is_active
            order by version.version_number desc
            limit 1
        )
    )
where lqc_operational_snapshot is null
   or lqc_template_version_id is null;

alter table projects
    alter column lqc_operational_snapshot set default true,
    alter column lqc_operational_snapshot set not null,
    alter column lqc_template_version_id set default '93000000-0000-0000-0000-000000000101',
    alter column lqc_template_version_id set not null;

create or replace function guard_project_lqc_snapshot_immutable()
returns trigger language plpgsql as $$
begin
    if new.lqc_operational_snapshot is distinct from old.lqc_operational_snapshot
       or new.lqc_template_version_id is distinct from old.lqc_template_version_id then
        raise exception 'Project LQC snapshot is immutable.' using errcode = 'P0001';
    end if;
    return new;
end $$;

drop trigger if exists trg_guard_project_lqc_snapshot_immutable on projects;
create trigger trg_guard_project_lqc_snapshot_immutable
before update of lqc_operational_snapshot, lqc_template_version_id on projects
for each row execute function guard_project_lqc_snapshot_immutable();

alter table panel_manufacturing_completion_confirmations
    add column if not exists manufacturing_execution_id uuid null
        references panel_manufacturing_executions(id) on delete restrict,
    add column if not exists handoff_basis text not null default 'ManufacturingAndLqc';

with latest_execution as (
    select distinct on (candidate.panel_id)
           candidate.panel_id,
           candidate.id
    from panel_manufacturing_executions candidate
    where candidate.status = 'Completed'
    order by candidate.panel_id,
             candidate.completed_at_utc desc nulls last,
             candidate.started_at_utc desc,
             candidate.id
)
update panel_manufacturing_completion_confirmations confirmation
set manufacturing_execution_id = execution.id
from latest_execution execution
where confirmation.panel_id = execution.panel_id
  and confirmation.manufacturing_execution_id is null;

do $$
begin
    if exists (
        select 1
        from panel_manufacturing_completion_confirmations
        where manufacturing_execution_id is null
    ) then
        raise exception 'Manufacturing completion confirmations require a completed execution before migration 0070.';
    end if;
end $$;

alter table panel_manufacturing_completion_confirmations
    alter column manufacturing_execution_id set not null,
    alter column lqc_attempt_id drop not null,
    drop constraint if exists ck_panel_manufacturing_completion_confirmations_handoff_basis;

alter table panel_manufacturing_completion_confirmations
    add constraint ck_panel_manufacturing_completion_confirmations_handoff_basis check (
        (handoff_basis = 'ManufacturingAndLqc' and lqc_attempt_id is not null)
        or (handoff_basis = 'ManufacturingOnly' and lqc_attempt_id is null)
    );

comment on column projects.lqc_operational_snapshot is
    'Immutable LQC operating decision copied from the project Item setting at project creation.';

comment on column projects.lqc_template_version_id is
    'Immutable Item-specific LQC form version copied at project creation.';

comment on column panel_manufacturing_completion_confirmations.handoff_basis is
    'ManufacturingAndLqc for LQC-enabled projects; ManufacturingOnly for LQC-suspended projects.';
