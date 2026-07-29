alter table projects
    add column if not exists structure_mode text null;

alter table projects
    drop constraint if exists ck_projects_structure_mode;

alter table projects
    add constraint ck_projects_structure_mode
    check (structure_mode is null or structure_mode = 'Ul891Set');

create table if not exists ul891_set_specs (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    spec_no integer not null,
    name text not null,
    row_version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_ul891_set_specs_project_no unique (project_id, spec_no),
    constraint ck_ul891_set_specs_no check (spec_no between 1 and 9999),
    constraint ck_ul891_set_specs_name check (char_length(btrim(name)) between 1 and 120),
    constraint ck_ul891_set_specs_version check (row_version >= 1)
);

create table if not exists ul891_set_spec_versions (
    id uuid primary key,
    spec_id uuid not null references ul891_set_specs(id) on delete cascade,
    version_number integer not null,
    status text not null default 'Draft',
    revision_reason text null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    published_by_user_id uuid null references qms_users(id),
    published_at_utc timestamptz null,
    constraint ux_ul891_set_spec_versions_spec_no unique (spec_id, version_number),
    constraint ck_ul891_set_spec_versions_no check (version_number >= 1),
    constraint ck_ul891_set_spec_versions_status check (status in ('Draft', 'Published', 'Superseded')),
    constraint ck_ul891_set_spec_versions_reason check (revision_reason is null or char_length(btrim(revision_reason)) between 1 and 500),
    constraint ck_ul891_set_spec_versions_publish check (
        (status = 'Draft' and published_by_user_id is null and published_at_utc is null)
        or (status in ('Published', 'Superseded') and published_by_user_id is not null and published_at_utc is not null)
    )
);

create unique index if not exists ux_ul891_set_spec_versions_one_draft
    on ul891_set_spec_versions(spec_id)
    where status = 'Draft';

create table if not exists ul891_set_spec_components (
    id uuid primary key,
    spec_version_id uuid not null references ul891_set_spec_versions(id) on delete cascade,
    component_code text not null,
    panel_name text null,
    panel_specification text null,
    width_mm numeric(12,2) null,
    height_mm numeric(12,2) null,
    depth_mm numeric(12,2) null,
    sort_order integer not null,
    constraint ux_ul891_set_spec_components_version_code unique (spec_version_id, component_code),
    constraint ux_ul891_set_spec_components_version_sort unique (spec_version_id, sort_order),
    constraint ck_ul891_set_spec_components_code check (char_length(btrim(component_code)) between 1 and 30),
    constraint ck_ul891_set_spec_components_name check (panel_name is null or char_length(btrim(panel_name)) between 1 and 200),
    constraint ck_ul891_set_spec_components_specification check (panel_specification is null or char_length(btrim(panel_specification)) between 1 and 500),
    constraint ck_ul891_set_spec_components_sort check (sort_order between 1 and 200),
    constraint ck_ul891_set_spec_components_dimensions check (
        (width_mm is null or width_mm >= 0)
        and (height_mm is null or height_mm >= 0)
        and (depth_mm is null or depth_mm >= 0)
    )
);

create table if not exists ul891_set_instances (
    id uuid primary key,
    spec_id uuid not null references ul891_set_specs(id) on delete cascade,
    instance_number integer not null,
    spec_version_id uuid not null references ul891_set_spec_versions(id) on delete restrict,
    status text not null default 'Active',
    row_version integer not null default 1,
    cancelled_reason text null,
    cancelled_exception_ack boolean not null default false,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ux_ul891_set_instances_spec_no unique (spec_id, instance_number),
    constraint ck_ul891_set_instances_no check (instance_number between 1 and 99999),
    constraint ck_ul891_set_instances_status check (status in ('Active', 'Cancelled')),
    constraint ck_ul891_set_instances_version check (row_version >= 1),
    constraint ck_ul891_set_instances_cancel check (
        (status = 'Active' and cancelled_reason is null and cancelled_by_user_id is null and cancelled_at_utc is null)
        or (status = 'Cancelled' and char_length(btrim(cancelled_reason)) between 1 and 500 and cancelled_by_user_id is not null and cancelled_at_utc is not null)
    )
);

alter table panel_placeholders
    add column if not exists set_instance_id uuid null references ul891_set_instances(id) on delete restrict;

alter table panel_placeholders
    add column if not exists component_code text null;

alter table panel_placeholders
    drop constraint if exists ck_panel_placeholders_set_component;

alter table panel_placeholders
    add constraint ck_panel_placeholders_set_component
    check (
        (set_instance_id is null and component_code is null)
        or (set_instance_id is not null and char_length(btrim(component_code)) between 1 and 30)
    );

create unique index if not exists ux_panel_placeholders_active_set_component
    on panel_placeholders(set_instance_id, component_code)
    where set_instance_id is not null and status = 'Active';

create index if not exists ix_panel_placeholders_set_instance
    on panel_placeholders(set_instance_id, sequence_number)
    where set_instance_id is not null;

create table if not exists ul891_recovery_cases (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    set_instance_id uuid not null references ul891_set_instances(id) on delete restrict,
    procurement_item_id uuid not null references project_procurement_items(id) on delete restrict,
    status text not null default 'BillingRequired',
    note text null,
    row_version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    recovered_by_user_id uuid null references qms_users(id),
    recovered_at_utc timestamptz null,
    constraint ux_ul891_recovery_cases_instance_item unique (set_instance_id, procurement_item_id),
    constraint ck_ul891_recovery_cases_status check (status in ('BillingRequired', 'AppliedToRequest', 'InvoiceConfirmed', 'Recovered')),
    constraint ck_ul891_recovery_cases_note check (note is null or char_length(btrim(note)) between 1 and 500),
    constraint ck_ul891_recovery_cases_version check (row_version >= 1),
    constraint ck_ul891_recovery_cases_recovered check (
        (status <> 'Recovered' and recovered_by_user_id is null and recovered_at_utc is null)
        or (status = 'Recovered' and recovered_by_user_id is not null and recovered_at_utc is not null)
    )
);

create table if not exists ul891_recovery_case_events (
    id uuid primary key,
    case_id uuid not null references ul891_recovery_cases(id) on delete cascade,
    from_status text null,
    to_status text not null,
    actor_user_id uuid not null references qms_users(id),
    reason text null,
    created_at_utc timestamptz not null default now(),
    constraint ck_ul891_recovery_case_events_status check (
        (from_status is null or from_status in ('BillingRequired', 'AppliedToRequest', 'InvoiceConfirmed', 'Recovered'))
        and to_status in ('BillingRequired', 'AppliedToRequest', 'InvoiceConfirmed', 'Recovered')
    ),
    constraint ck_ul891_recovery_case_events_reason check (reason is null or char_length(btrim(reason)) between 1 and 500)
);

create table if not exists sales_monthly_billing_ledgers (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    billing_month date not null,
    kind text not null default 'Shipment',
    status text not null default 'Open',
    row_version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    constraint ux_sales_monthly_billing_ledgers_project_month unique (project_id, billing_month),
    constraint ck_sales_monthly_billing_ledgers_month check (billing_month = date_trunc('month', billing_month)::date),
    constraint ck_sales_monthly_billing_ledgers_kind check (kind in ('Shipment', 'RecoveryOnly')),
    constraint ck_sales_monthly_billing_ledgers_status check (status in ('Open', 'Requested', 'InvoiceConfirmed', 'AdjustmentRequired')),
    constraint ck_sales_monthly_billing_ledgers_version check (row_version >= 1)
);

create table if not exists sales_monthly_billing_revisions (
    id uuid primary key,
    ledger_id uuid not null references sales_monthly_billing_ledgers(id) on delete cascade,
    revision_number integer not null,
    amount numeric(18,2) not null,
    note text null,
    is_adjustment boolean not null default false,
    adjustment_reason text null,
    invoice_confirmed_date date null,
    invoice_number text null,
    invoice_confirmed_by_user_id uuid null references qms_users(id),
    invoice_confirmed_at_utc timestamptz null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ux_sales_monthly_billing_revisions_ledger_no unique (ledger_id, revision_number),
    constraint ck_sales_monthly_billing_revisions_no check (revision_number >= 1),
    constraint ck_sales_monthly_billing_revisions_amount check (amount >= 0),
    constraint ck_sales_monthly_billing_revisions_note check (note is null or char_length(btrim(note)) between 1 and 500),
    constraint ck_sales_monthly_billing_revisions_adjustment check (
        (not is_adjustment and adjustment_reason is null)
        or (is_adjustment and char_length(btrim(adjustment_reason)) between 1 and 500)
    ),
    constraint ck_sales_monthly_billing_revisions_invoice check (
        (invoice_confirmed_date is null and invoice_number is null and invoice_confirmed_by_user_id is null and invoice_confirmed_at_utc is null)
        or (invoice_confirmed_date is not null and char_length(btrim(invoice_number)) between 1 and 64 and invoice_confirmed_by_user_id is not null and invoice_confirmed_at_utc is not null)
    )
);

create table if not exists sales_monthly_billing_revision_panels (
    revision_id uuid not null references sales_monthly_billing_revisions(id) on delete cascade,
    panel_id uuid not null references panel_placeholders(id) on delete restrict,
    panel_display_code text not null,
    packing_unit_label text null,
    departure_date date not null,
    primary key (revision_id, panel_id)
);

create table if not exists sales_monthly_billing_revision_cases (
    revision_id uuid not null references sales_monthly_billing_revisions(id) on delete cascade,
    recovery_case_id uuid not null references ul891_recovery_cases(id) on delete restrict,
    primary key (revision_id, recovery_case_id)
);

create table if not exists sales_monthly_billing_confirmations (
    id uuid primary key,
    ledger_id uuid not null references sales_monthly_billing_ledgers(id) on delete cascade,
    revision_id uuid not null references sales_monthly_billing_revisions(id) on delete restrict,
    invoice_confirmed_date date not null,
    invoice_number text not null,
    note text null,
    confirmed_by_user_id uuid not null references qms_users(id),
    confirmed_at_utc timestamptz not null default now(),
    constraint ux_sales_monthly_billing_confirmations_revision unique (revision_id),
    constraint ck_sales_monthly_billing_confirmations_number check (char_length(btrim(invoice_number)) between 1 and 64),
    constraint ck_sales_monthly_billing_confirmations_note check (note is null or char_length(btrim(note)) between 1 and 500)
);

create table if not exists ul891_set_operations (
    operation_id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    actor_user_id uuid not null references qms_users(id),
    action text not null,
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_ul891_set_operations_action check (char_length(btrim(action)) between 3 and 80),
    constraint ck_ul891_set_operations_fingerprint check (payload_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_ul891_set_operations_projection check (octet_length(result_projection::text) <= 4096)
);

create table if not exists sales_monthly_billing_operations (
    operation_id uuid primary key,
    project_id uuid not null references projects(id) on delete cascade,
    actor_user_id uuid not null references qms_users(id),
    action text not null,
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_sales_monthly_billing_operations_action check (char_length(btrim(action)) between 3 and 80),
    constraint ck_sales_monthly_billing_operations_fingerprint check (payload_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_sales_monthly_billing_operations_projection check (octet_length(result_projection::text) <= 4096)
);

create or replace function guard_ul891_spec_version_immutability()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;

    if tg_op = 'DELETE' and old.status <> 'Draft' then
        raise exception 'Published UL891 specification versions are immutable.';
    end if;

    if tg_op = 'UPDATE' and old.status in ('Published', 'Superseded') then
        if old.status = 'Published' and new.status = 'Superseded'
           and new.spec_id = old.spec_id
           and new.version_number = old.version_number
           and new.revision_reason is not distinct from old.revision_reason
           and new.created_by_user_id = old.created_by_user_id
           and new.created_at_utc = old.created_at_utc
           and new.published_by_user_id = old.published_by_user_id
           and new.published_at_utc = old.published_at_utc then
            return new;
        end if;
        raise exception 'Published UL891 specification versions are immutable.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

create trigger trg_guard_ul891_spec_version
before update or delete on ul891_set_spec_versions
for each row execute function guard_ul891_spec_version_immutability();

create or replace function guard_ul891_component_immutability()
returns trigger language plpgsql as $$
declare
    version_status text;
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    select status into version_status
    from ul891_set_spec_versions
    where id = case when tg_op = 'DELETE' then old.spec_version_id else new.spec_version_id end;
    if version_status <> 'Draft' then
        raise exception 'Published UL891 specification components are immutable.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

create trigger trg_guard_ul891_component
before insert or update or delete on ul891_set_spec_components
for each row execute function guard_ul891_component_immutability();

create or replace function guard_ul891_append_only()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' and tg_op = 'DELETE' then
        return old;
    end if;
    raise exception 'UL891 operation and event records are append-only.';
end $$;

create trigger trg_guard_ul891_recovery_case_event
before update or delete on ul891_recovery_case_events
for each row execute function guard_ul891_append_only();

create trigger trg_guard_ul891_set_operation
before update or delete on ul891_set_operations
for each row execute function guard_ul891_append_only();

create trigger trg_guard_sales_monthly_billing_operation
before update or delete on sales_monthly_billing_operations
for each row execute function guard_ul891_append_only();

create trigger trg_guard_sales_monthly_billing_revision
before update or delete on sales_monthly_billing_revisions
for each row execute function guard_ul891_append_only();

create trigger trg_guard_sales_monthly_billing_revision_panel
before update or delete on sales_monthly_billing_revision_panels
for each row execute function guard_ul891_append_only();

create trigger trg_guard_sales_monthly_billing_revision_case
before update or delete on sales_monthly_billing_revision_cases
for each row execute function guard_ul891_append_only();

create trigger trg_guard_sales_monthly_billing_confirmation
before update or delete on sales_monthly_billing_confirmations
for each row execute function guard_ul891_append_only();

do $$
begin
    alter table project_audit_events drop constraint if exists ck_project_audit_events_entity_type;
    alter table project_audit_events add constraint ck_project_audit_events_entity_type
        check (entity_type in (
            'Project', 'PanelPlaceholder', 'Panel', 'ProcurementItem', 'ProductionPlan',
            'ProductionPlanItem', 'ProjectAssignee', 'SetSpec', 'SetSpecVersion',
            'SetInstance', 'RecoveryCase', 'MonthlyBillingLedger', 'MonthlyBillingRevision'
        ));
exception
    when duplicate_object then null;
end $$;
