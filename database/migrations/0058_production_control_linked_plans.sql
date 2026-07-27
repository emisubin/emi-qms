alter table form_template_manager_bindings
    drop constraint if exists ck_form_template_manager_domain,
    add constraint ck_form_template_manager_domain
        check (domain in ('Quality', 'Manufacturing', 'ProductionPlanning'));

alter table form_template_audit_events
    drop constraint if exists ck_form_template_audit_domain,
    add constraint ck_form_template_audit_domain
        check (domain in ('Quality', 'Manufacturing', 'ProductionPlanning', 'Administration'));

create table if not exists production_control_manufacturing_templates (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null unique references production_product_types(id) on delete restrict,
    created_at_utc timestamptz not null default now()
);

create table if not exists production_control_manufacturing_versions (
    id uuid primary key default uuid_generate_v4(),
    template_id uuid not null references production_control_manufacturing_templates(id) on delete restrict,
    version_number integer not null,
    lifecycle_status text not null,
    row_version integer not null default 1,
    activated_at_utc timestamptz null,
    archived_at_utc timestamptz null,
    created_by_user_id uuid null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_production_control_manufacturing_versions unique (template_id, version_number),
    constraint ck_production_control_manufacturing_versions_status
        check (lifecycle_status in ('Draft', 'Active', 'Archived')),
    constraint ck_production_control_manufacturing_versions_row_version check (row_version >= 1),
    constraint ck_production_control_manufacturing_versions_lifecycle check (
        (lifecycle_status = 'Draft' and activated_at_utc is null and archived_at_utc is null)
        or (lifecycle_status = 'Active' and activated_at_utc is not null and archived_at_utc is null)
        or (lifecycle_status = 'Archived' and archived_at_utc is not null)
    )
);

create unique index if not exists ux_production_control_manufacturing_versions_active
    on production_control_manufacturing_versions(template_id)
    where lifecycle_status = 'Active';

create table if not exists production_control_manufacturing_items (
    id uuid primary key default uuid_generate_v4(),
    template_version_id uuid not null references production_control_manufacturing_versions(id) on delete cascade,
    definition_key uuid not null,
    display_order integer not null,
    label text not null,
    step_role text not null default 'General',
    constraint ux_production_control_manufacturing_items_definition
        unique (template_version_id, definition_key),
    constraint ux_production_control_manufacturing_items_order
        unique (template_version_id, display_order),
    constraint ck_production_control_manufacturing_items_order check (display_order between 1 and 50),
    constraint ck_production_control_manufacturing_items_label
        check (char_length(btrim(label)) between 1 and 100),
    constraint ck_production_control_manufacturing_items_role
        check (step_role in ('General', 'Assembly'))
);

create unique index if not exists ux_production_control_manufacturing_items_assembly
    on production_control_manufacturing_items(template_version_id)
    where step_role = 'Assembly';

create table if not exists production_control_plan_templates (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null unique references production_product_types(id) on delete restrict,
    created_at_utc timestamptz not null default now()
);

create table if not exists production_control_plan_versions (
    id uuid primary key default uuid_generate_v4(),
    template_id uuid not null references production_control_plan_templates(id) on delete restrict,
    version_number integer not null,
    lifecycle_status text not null,
    row_version integer not null default 1,
    activated_at_utc timestamptz null,
    archived_at_utc timestamptz null,
    created_by_user_id uuid null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ux_production_control_plan_versions unique (template_id, version_number),
    constraint ck_production_control_plan_versions_status
        check (lifecycle_status in ('Draft', 'Active', 'Archived')),
    constraint ck_production_control_plan_versions_row_version check (row_version >= 1),
    constraint ck_production_control_plan_versions_lifecycle check (
        (lifecycle_status = 'Draft' and activated_at_utc is null and archived_at_utc is null)
        or (lifecycle_status = 'Active' and activated_at_utc is not null and archived_at_utc is null)
        or (lifecycle_status = 'Archived' and archived_at_utc is not null)
    )
);

create unique index if not exists ux_production_control_plan_versions_active
    on production_control_plan_versions(template_id)
    where lifecycle_status = 'Active';

create table if not exists production_control_plan_items (
    id uuid primary key default uuid_generate_v4(),
    template_version_id uuid not null references production_control_plan_versions(id) on delete cascade,
    definition_key uuid not null,
    display_order integer not null,
    label text not null,
    is_required boolean not null default true,
    constraint ux_production_control_plan_items_definition
        unique (template_version_id, definition_key),
    constraint ux_production_control_plan_items_order
        unique (template_version_id, display_order),
    constraint ck_production_control_plan_items_order check (display_order between 1 and 100),
    constraint ck_production_control_plan_items_label
        check (char_length(btrim(label)) between 1 and 120)
);

create table if not exists production_control_plan_connections (
    id uuid primary key default uuid_generate_v4(),
    plan_item_id uuid not null references production_control_plan_items(id) on delete cascade,
    source_code text not null,
    source_definition_key uuid null,
    constraint ck_production_control_plan_connections_source check (
        source_code in (
            'PURCHASE_ORDERED',
            'MATERIAL_RECEIPT_CONFIRMED',
            'IQC_PASSED',
            'MANUFACTURING_STEP_COMPLETED',
            'LQC_PASSED',
            'OQC_PASSED',
            'CUSTOMER_INSPECTION_PASSED',
            'FAT_PASSED',
            'PACKED',
            'DEPARTED',
            'DELIVERED'
        )
    ),
    constraint ck_production_control_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED') and source_definition_key is not null)
        or (source_code not in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED') and source_definition_key is null)
    )
);

create unique index if not exists ux_production_control_plan_connections
    on production_control_plan_connections (
        plan_item_id,
        source_code,
        coalesce(source_definition_key, '00000000-0000-0000-0000-000000000000'::uuid)
    );

alter table project_production_plans
    add column if not exists model_version text not null default 'LEGACY',
    add column if not exists linked_plan_template_version_id uuid null
        references production_control_plan_versions(id) on delete restrict,
    add column if not exists linked_manufacturing_template_version_id uuid null
        references production_control_manufacturing_versions(id) on delete restrict,
    drop constraint if exists ck_project_production_plans_model_version,
    add constraint ck_project_production_plans_model_version
        check (model_version in ('LEGACY', 'LINKED_V1')),
    drop constraint if exists ck_project_production_plans_linked_versions,
    add constraint ck_project_production_plans_linked_versions check (
        (model_version = 'LEGACY'
            and linked_plan_template_version_id is null
            and linked_manufacturing_template_version_id is null)
        or (model_version = 'LINKED_V1'
            and linked_plan_template_version_id is not null
            and linked_manufacturing_template_version_id is not null)
    );

alter table project_production_plan_items
    add column if not exists definition_key uuid null,
    add column if not exists planned_start_date date null,
    add column if not exists planned_end_date date null,
    drop constraint if exists ck_project_production_plan_items_period,
    add constraint ck_project_production_plan_items_period check (
        planned_start_date is null
        or planned_end_date is null
        or planned_start_date <= planned_end_date
    );

create unique index if not exists ux_project_production_plan_items_definition
    on project_production_plan_items(production_plan_id, definition_key)
    where definition_key is not null and is_active;

create table if not exists project_production_plan_connections (
    id uuid primary key default uuid_generate_v4(),
    production_plan_item_id uuid not null
        references project_production_plan_items(id) on delete cascade,
    source_code text not null,
    source_definition_key uuid null,
    created_at_utc timestamptz not null default now(),
    constraint ck_project_production_plan_connections_source check (
        source_code in (
            'PURCHASE_ORDERED',
            'MATERIAL_RECEIPT_CONFIRMED',
            'IQC_PASSED',
            'MANUFACTURING_STEP_COMPLETED',
            'LQC_PASSED',
            'OQC_PASSED',
            'CUSTOMER_INSPECTION_PASSED',
            'FAT_PASSED',
            'PACKED',
            'DEPARTED',
            'DELIVERED'
        )
    ),
    constraint ck_project_production_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED') and source_definition_key is not null)
        or (source_code not in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED') and source_definition_key is null)
    )
);

create unique index if not exists ux_project_production_plan_connections
    on project_production_plan_connections (
        production_plan_item_id,
        source_code,
        coalesce(source_definition_key, '00000000-0000-0000-0000-000000000000'::uuid)
    );

create table if not exists project_manufacturing_step_snapshots (
    id uuid primary key default uuid_generate_v4(),
    project_id uuid not null references projects(id) on delete cascade,
    source_template_version_id uuid not null
        references production_control_manufacturing_versions(id) on delete restrict,
    definition_key uuid not null,
    sequence_number integer not null,
    step_name_snapshot text not null,
    step_role text not null default 'General',
    is_active boolean not null default true,
    row_version integer not null default 1,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_project_manufacturing_step_snapshots_definition
        unique (project_id, definition_key),
    constraint ux_project_manufacturing_step_snapshots_order
        unique (project_id, sequence_number),
    constraint ck_project_manufacturing_step_snapshots_order
        check (sequence_number between 1 and 50),
    constraint ck_project_manufacturing_step_snapshots_name
        check (char_length(btrim(step_name_snapshot)) between 1 and 100),
    constraint ck_project_manufacturing_step_snapshots_role
        check (step_role in ('General', 'Assembly')),
    constraint ck_project_manufacturing_step_snapshots_row_version check (row_version >= 1)
);

create unique index if not exists ux_project_manufacturing_step_snapshots_assembly
    on project_manufacturing_step_snapshots(project_id)
    where step_role = 'Assembly' and is_active;

alter table panel_manufacturing_execution_steps
    add column if not exists project_manufacturing_step_id uuid null
        references project_manufacturing_step_snapshots(id) on delete restrict,
    add column if not exists definition_key uuid null;

alter table panel_manufacturing_execution_steps
    drop constraint if exists ck_panel_manufacturing_execution_steps_sequence,
    add constraint ck_panel_manufacturing_execution_steps_sequence
        check (sequence_number between 1 and 50),
    drop constraint if exists ck_panel_manufacturing_execution_steps_name,
    add constraint ck_panel_manufacturing_execution_steps_name
        check (char_length(btrim(step_name)) between 1 and 100);

create index if not exists ix_panel_manufacturing_execution_steps_definition
    on panel_manufacturing_execution_steps(execution_id, definition_key)
    where definition_key is not null;

comment on column project_production_plans.model_version is
    'LEGACY keeps the single planned-date/check-calendar contract. LINKED_V1 uses versioned templates, source bindings and derived actuals.';
