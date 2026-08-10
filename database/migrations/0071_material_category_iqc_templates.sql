alter table iqc_report_templates
    add column if not exists material_category_id uuid null
        references material_categories(id) on delete restrict;

create unique index if not exists ux_iqc_report_templates_material_category
    on iqc_report_templates(material_category_id)
    where material_category_id is not null;

create table if not exists material_category_iqc_settings (
    material_category_id uuid primary key references material_categories(id) on delete restrict,
    is_enabled boolean not null default false,
    decision_mode text not null default 'ScanBased',
    current_template_version_id uuid not null
        references iqc_report_template_versions(id) on delete restrict,
    row_version integer not null default 1,
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_material_category_iqc_settings_mode
        check (decision_mode in ('ScanBased', 'Detailed')),
    constraint ck_material_category_iqc_settings_row_version
        check (row_version >= 1)
);

do $$
declare
    category record;
    category_template_id uuid;
    category_version_id uuid;
begin
    for category in
        select id, code, display_name, requires_iqc
        from material_categories
        order by code
    loop
        select id into category_template_id
        from iqc_report_templates
        where material_category_id = category.id;

        if category_template_id is null then
            category_template_id := uuid_generate_v4();
            insert into iqc_report_templates (
                id, template_code, display_name, is_system, material_category_id
            ) values (
                category_template_id,
                'CATEGORY_IQC_' || category.code,
                category.display_name || ' 수입검사',
                false,
                category.id
            );
        end if;

        select id into category_version_id
        from iqc_report_template_versions
        where template_id = category_template_id
          and lifecycle_status = 'Active'
          and is_active
        order by version_number desc
        limit 1;

        if category_version_id is null then
            category_version_id := uuid_generate_v4();
            insert into iqc_report_template_versions (
                id, template_id, version_number, is_active, activated_at_utc,
                lifecycle_status, row_version
            ) values (
                category_version_id, category_template_id, 1, true, now(),
                'Active', 1
            );
        end if;

        insert into material_category_iqc_settings (
            material_category_id, is_enabled, decision_mode, current_template_version_id
        ) values (
            category.id, category.requires_iqc, 'ScanBased', category_version_id
        )
        on conflict (material_category_id) do nothing;
    end loop;
end $$;

create table if not exists material_category_iqc_setting_audit_events (
    id uuid primary key default uuid_generate_v4(),
    material_category_id uuid not null references material_categories(id) on delete restrict,
    action text not null,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    old_value jsonb not null,
    new_value jsonb not null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_material_category_iqc_setting_audit_action check (
        action in ('SettingChanged', 'TemplateChanged')
    ),
    constraint ck_material_category_iqc_setting_audit_old_value check (
        jsonb_typeof(old_value) = 'object' and octet_length(old_value::text) <= 4096
    ),
    constraint ck_material_category_iqc_setting_audit_new_value check (
        jsonb_typeof(new_value) = 'object' and octet_length(new_value::text) <= 4096
    )
);

create index if not exists ix_material_category_iqc_setting_audit_time
    on material_category_iqc_setting_audit_events(
        material_category_id, occurred_at_utc desc, id
    );

create or replace function guard_material_category_iqc_setting_audit_append_only()
returns trigger language plpgsql as $$
begin
    raise exception 'Material category IQC setting audit events are append-only.'
        using errcode = 'P0001';
end $$;

drop trigger if exists trg_guard_material_category_iqc_setting_audit_append_only
    on material_category_iqc_setting_audit_events;
create trigger trg_guard_material_category_iqc_setting_audit_append_only
before update or delete on material_category_iqc_setting_audit_events
for each row execute function guard_material_category_iqc_setting_audit_append_only();

alter table project_procurement_items
    add column if not exists material_category_iqc_enabled_snapshot boolean null,
    add column if not exists material_category_iqc_decision_mode_snapshot text null;

update project_procurement_items
set material_category_iqc_enabled_snapshot = material_category_requires_iqc_snapshot,
    material_category_iqc_decision_mode_snapshot = 'ScanBased'
where material_category_id is not null
  and (
      material_category_iqc_enabled_snapshot is null
      or material_category_iqc_decision_mode_snapshot is null
  );

alter table project_procurement_items
    drop constraint if exists ck_project_procurement_items_material_category_iqc_snapshot,
    add constraint ck_project_procurement_items_material_category_iqc_snapshot check (
        (
            material_category_id is null
            and material_category_iqc_enabled_snapshot is null
            and material_category_iqc_decision_mode_snapshot is null
        )
        or (
            material_category_id is not null
            and material_category_iqc_enabled_snapshot is not null
            and material_category_iqc_decision_mode_snapshot in ('ScanBased', 'Detailed')
        )
    );

alter table material_categories
    alter column requires_iqc set default false;

create or replace function guard_material_category_iqc_projection_write()
returns trigger language plpgsql as $$
begin
    if new.requires_iqc is distinct from old.requires_iqc
       and coalesce(current_setting('emi_qms.material_category_iqc_projection_write', true), '') <> 'allowed' then
        raise exception 'Material category IQC state is managed by material_category_iqc_settings.'
            using errcode = 'P0001';
    end if;
    return new;
end $$;

drop trigger if exists trg_guard_material_category_iqc_projection_write on material_categories;
create trigger trg_guard_material_category_iqc_projection_write
before update of requires_iqc on material_categories
for each row execute function guard_material_category_iqc_projection_write();

create or replace function sync_material_category_iqc_projection()
returns trigger language plpgsql as $$
begin
    perform set_config('emi_qms.material_category_iqc_projection_write', 'allowed', true);
    update material_categories
    set requires_iqc = new.is_enabled
    where id = new.material_category_id
      and requires_iqc is distinct from new.is_enabled;
    perform set_config('emi_qms.material_category_iqc_projection_write', '', true);
    return new;
end $$;

drop trigger if exists trg_sync_material_category_iqc_projection on material_category_iqc_settings;
create trigger trg_sync_material_category_iqc_projection
after insert or update of is_enabled on material_category_iqc_settings
for each row execute function sync_material_category_iqc_projection();
