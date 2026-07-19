alter table iqc_report_template_versions
    add column if not exists lifecycle_status text,
    add column if not exists row_version integer not null default 1,
    add column if not exists created_by_user_id uuid null references qms_users(id),
    add column if not exists updated_by_user_id uuid null references qms_users(id),
    add column if not exists updated_at_utc timestamptz not null default now(),
    add column if not exists archived_at_utc timestamptz null;

update iqc_report_template_versions
set lifecycle_status = case when is_active then 'Active' else 'Archived' end,
    archived_at_utc = case when is_active then null else coalesce(archived_at_utc, now()) end
where lifecycle_status is null;

alter table iqc_report_template_versions
    alter column lifecycle_status set not null,
    drop constraint if exists ck_iqc_report_template_versions_activation,
    add constraint ck_iqc_report_template_versions_lifecycle_status
        check (lifecycle_status in ('Draft', 'Active', 'Archived')),
    add constraint ck_iqc_report_template_versions_row_version check (row_version >= 1),
    add constraint ck_iqc_report_template_versions_lifecycle check (
        (lifecycle_status = 'Draft' and not is_active and activated_at_utc is null and archived_at_utc is null)
        or (lifecycle_status = 'Active' and is_active and activated_at_utc is not null and archived_at_utc is null)
        or (lifecycle_status = 'Archived' and not is_active and archived_at_utc is not null)
    );

alter table panel_quality_template_versions
    add column if not exists lifecycle_status text,
    add column if not exists row_version integer not null default 1,
    add column if not exists created_by_user_id uuid null references qms_users(id),
    add column if not exists updated_by_user_id uuid null references qms_users(id),
    add column if not exists updated_at_utc timestamptz not null default now(),
    add column if not exists archived_at_utc timestamptz null;

update panel_quality_template_versions
set lifecycle_status = case when is_active then 'Active' else 'Archived' end,
    archived_at_utc = case when is_active then null else coalesce(archived_at_utc, now()) end
where lifecycle_status is null;

alter table panel_quality_template_versions
    alter column lifecycle_status set not null,
    drop constraint if exists ck_panel_quality_template_versions_activation,
    add constraint ck_panel_quality_template_versions_lifecycle_status
        check (lifecycle_status in ('Draft', 'Active', 'Archived')),
    add constraint ck_panel_quality_template_versions_row_version check (row_version >= 1),
    add constraint ck_panel_quality_template_versions_lifecycle check (
        (lifecycle_status = 'Draft' and not is_active and activated_at_utc is null and archived_at_utc is null)
        or (lifecycle_status = 'Active' and is_active and activated_at_utc is not null and archived_at_utc is null)
        or (lifecycle_status = 'Archived' and not is_active and archived_at_utc is not null)
    );

create table if not exists manufacturing_step_templates (
    id uuid primary key,
    template_code text not null unique,
    display_name text not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_manufacturing_step_templates_code check (template_code ~ '^[A-Z0-9_]{3,40}$'),
    constraint ck_manufacturing_step_templates_name check (char_length(btrim(display_name)) between 2 and 100)
);

create table if not exists manufacturing_step_template_versions (
    id uuid primary key,
    template_id uuid not null references manufacturing_step_templates(id) on delete restrict,
    version_number integer not null,
    display_name text not null,
    lifecycle_status text not null,
    is_active boolean not null default false,
    row_version integer not null default 1,
    activated_at_utc timestamptz null,
    archived_at_utc timestamptz null,
    created_by_user_id uuid null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_manufacturing_step_template_versions unique (template_id, version_number),
    constraint ck_manufacturing_step_template_versions_number check (version_number >= 1),
    constraint ck_manufacturing_step_template_versions_name check (char_length(btrim(display_name)) between 2 and 100),
    constraint ck_manufacturing_step_template_versions_status check (lifecycle_status in ('Draft', 'Active', 'Archived')),
    constraint ck_manufacturing_step_template_versions_row_version check (row_version >= 1),
    constraint ck_manufacturing_step_template_versions_lifecycle check (
        (lifecycle_status = 'Draft' and not is_active and activated_at_utc is null and archived_at_utc is null)
        or (lifecycle_status = 'Active' and is_active and activated_at_utc is not null and archived_at_utc is null)
        or (lifecycle_status = 'Archived' and not is_active and archived_at_utc is not null)
    )
);

create unique index if not exists ux_manufacturing_step_template_versions_active
    on manufacturing_step_template_versions(template_id)
    where is_active;

create table if not exists manufacturing_step_template_items (
    id uuid primary key,
    template_version_id uuid not null references manufacturing_step_template_versions(id) on delete restrict,
    item_code text not null,
    display_order integer not null,
    label text not null,
    constraint ux_manufacturing_step_template_items_code unique (template_version_id, item_code),
    constraint ux_manufacturing_step_template_items_order unique (template_version_id, display_order),
    constraint ck_manufacturing_step_template_items_code check (item_code ~ '^[A-Z0-9_]{2,40}$'),
    constraint ck_manufacturing_step_template_items_order check (display_order between 1 and 10),
    constraint ck_manufacturing_step_template_items_label check (char_length(btrim(label)) between 2 and 80)
);

insert into manufacturing_step_templates (id, template_code, display_name)
values ('44000000-0000-0000-0000-000000000001', 'PANEL_MANUFACTURING', '제조 작업 단계')
on conflict (template_code) do nothing;

insert into manufacturing_step_template_versions (
    id, template_id, version_number, display_name, lifecycle_status, is_active, activated_at_utc)
values (
    '44000000-0000-0000-0000-000000000002',
    '44000000-0000-0000-0000-000000000001',
    1, '제조 작업 단계 v1', 'Active', true, now())
on conflict (template_id, version_number) do nothing;

insert into manufacturing_step_template_items (id, template_version_id, item_code, display_order, label)
values
    ('44000000-0000-0000-0000-000000000011', '44000000-0000-0000-0000-000000000002', 'WORK_ORDER', 1, '작업지시·도면 확인'),
    ('44000000-0000-0000-0000-000000000012', '44000000-0000-0000-0000-000000000002', 'MATERIALS', 2, '자재·부품 확인'),
    ('44000000-0000-0000-0000-000000000013', '44000000-0000-0000-0000-000000000002', 'MANUFACTURING', 3, '제조 작업 수행'),
    ('44000000-0000-0000-0000-000000000014', '44000000-0000-0000-0000-000000000002', 'SELF_CHECK', 4, '자체 확인')
on conflict (template_version_id, item_code) do nothing;

alter table panel_manufacturing_executions
    add column if not exists template_version_id uuid null references manufacturing_step_template_versions(id) on delete restrict;

alter table panel_manufacturing_execution_steps
    drop constraint if exists ck_panel_manufacturing_execution_steps_sequence,
    add constraint ck_panel_manufacturing_execution_steps_sequence check (sequence_number between 1 and 10);

create table if not exists form_template_manager_bindings (
    id uuid primary key default uuid_generate_v4(),
    user_id uuid not null references qms_users(id) on delete restrict,
    department_id uuid not null references departments(id) on delete restrict,
    domain text not null,
    assigned_by_user_id uuid not null references qms_users(id) on delete restrict,
    assigned_at_utc timestamptz not null default now(),
    revoked_by_user_id uuid null references qms_users(id) on delete restrict,
    revoked_at_utc timestamptz null,
    constraint ck_form_template_manager_domain check (domain in ('Quality', 'Manufacturing')),
    constraint ck_form_template_manager_revoke check (
        (revoked_by_user_id is null and revoked_at_utc is null)
        or (revoked_by_user_id is not null and revoked_at_utc is not null)
    )
);

create unique index if not exists ux_form_template_manager_bindings_active
    on form_template_manager_bindings(user_id, department_id, domain)
    where revoked_at_utc is null;

create table if not exists form_template_audit_events (
    id uuid primary key default uuid_generate_v4(),
    action text not null,
    domain text not null,
    family text not null,
    template_key text not null,
    version_id uuid null,
    binding_id uuid null references form_template_manager_bindings(id) on delete restrict,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    detail jsonb not null default '{}'::jsonb,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_form_template_audit_action check (action in (
        'DraftCreated', 'DraftSaved', 'VersionActivated', 'DraftArchived',
        'ManagerAssigned', 'ManagerRevoked', 'VersionsExported'
    )),
    constraint ck_form_template_audit_domain check (domain in ('Quality', 'Manufacturing', 'Administration')),
    constraint ck_form_template_audit_family check (char_length(btrim(family)) between 3 and 40),
    constraint ck_form_template_audit_key check (char_length(btrim(template_key)) between 2 and 80),
    constraint ck_form_template_audit_detail check (jsonb_typeof(detail) = 'object' and octet_length(detail::text) <= 4096)
);

create index if not exists ix_form_template_audit_time
    on form_template_audit_events(occurred_at_utc desc, id);

create or replace function guard_append_only_form_template_audit()
returns trigger language plpgsql as $$
begin
    raise exception 'Form template audit events are append-only.';
end $$;

drop trigger if exists trg_guard_form_template_audit on form_template_audit_events;
create trigger trg_guard_form_template_audit
before update or delete on form_template_audit_events
for each row execute function guard_append_only_form_template_audit();

create or replace function guard_form_template_version_lifecycle()
returns trigger language plpgsql as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'Form template versions are immutable.';
    end if;
    if new.row_version <> old.row_version + 1 then
        raise exception 'Form template version transition must increment row version.';
    end if;
    if old.lifecycle_status = 'Archived' then
        raise exception 'Archived form template versions are immutable.';
    end if;
    if old.lifecycle_status = 'Active' then
        if new.lifecycle_status <> 'Archived'
           or (to_jsonb(new) - array[
                'lifecycle_status', 'is_active', 'row_version',
                'updated_by_user_id', 'updated_at_utc', 'archived_at_utc'
              ])
              is distinct from
              (to_jsonb(old) - array[
                'lifecycle_status', 'is_active', 'row_version',
                'updated_by_user_id', 'updated_at_utc', 'archived_at_utc'
              ]) then
            raise exception 'Active form template versions may only be archived.';
        end if;
    end if;
    return new;
end $$;

create or replace function guard_iqc_template_item_mutation()
returns trigger language plpgsql as $$
declare owner_status text;
begin
    select lifecycle_status into owner_status
    from iqc_report_template_versions
    where id = case when tg_op = 'DELETE' then old.template_version_id else new.template_version_id end;
    if owner_status is distinct from 'Draft' then
        raise exception 'Only draft IQC template items can be changed.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

create or replace function guard_panel_quality_template_item_mutation()
returns trigger language plpgsql as $$
declare owner_status text;
begin
    select lifecycle_status into owner_status
    from panel_quality_template_versions
    where id = case when tg_op = 'DELETE' then old.template_version_id else new.template_version_id end;
    if owner_status is distinct from 'Draft' then
        raise exception 'Only draft panel quality template items can be changed.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

create or replace function guard_manufacturing_template_item_mutation()
returns trigger language plpgsql as $$
declare owner_status text;
begin
    select lifecycle_status into owner_status
    from manufacturing_step_template_versions
    where id = case when tg_op = 'DELETE' then old.template_version_id else new.template_version_id end;
    if owner_status is distinct from 'Draft' then
        raise exception 'Only draft manufacturing template items can be changed.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

drop trigger if exists trg_guard_iqc_template_version on iqc_report_template_versions;
create trigger trg_guard_iqc_template_version
before update or delete on iqc_report_template_versions
for each row execute function guard_form_template_version_lifecycle();

drop trigger if exists trg_guard_panel_quality_template_version on panel_quality_template_versions;
create trigger trg_guard_panel_quality_template_version
before update or delete on panel_quality_template_versions
for each row execute function guard_form_template_version_lifecycle();

drop trigger if exists trg_guard_manufacturing_template_version on manufacturing_step_template_versions;
create trigger trg_guard_manufacturing_template_version
before update or delete on manufacturing_step_template_versions
for each row execute function guard_form_template_version_lifecycle();

drop trigger if exists trg_guard_iqc_template_items on iqc_report_template_items;
create trigger trg_guard_iqc_template_items
before insert or update or delete on iqc_report_template_items
for each row execute function guard_iqc_template_item_mutation();

drop trigger if exists trg_guard_panel_quality_template_items on panel_quality_template_items;
create trigger trg_guard_panel_quality_template_items
before insert or update or delete on panel_quality_template_items
for each row execute function guard_panel_quality_template_item_mutation();

drop trigger if exists trg_guard_manufacturing_template_items on manufacturing_step_template_items;
create trigger trg_guard_manufacturing_template_items
before insert or update or delete on manufacturing_step_template_items
for each row execute function guard_manufacturing_template_item_mutation();
