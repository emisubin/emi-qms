create table if not exists logistics_packing_units (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    unit_number integer not null,
    status text not null default 'Draft',
    version integer not null default 1,
    note text null,
    specification text null,
    weight_text text null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    finalized_by_user_id uuid null references qms_users(id),
    finalized_at_utc timestamptz null,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    constraint ux_logistics_packing_units_project_number unique (project_id, unit_number),
    constraint ck_logistics_packing_units_number check (unit_number >= 1),
    constraint ck_logistics_packing_units_status check (status in ('Draft', 'Finalized', 'Cancelled')),
    constraint ck_logistics_packing_units_version check (version >= 1),
    constraint ck_logistics_packing_units_note check (note is null or char_length(btrim(note)) between 1 and 500),
    constraint ck_logistics_packing_units_specification check (specification is null or char_length(btrim(specification)) between 1 and 120),
    constraint ck_logistics_packing_units_weight check (weight_text is null or char_length(btrim(weight_text)) between 1 and 80),
    constraint ck_logistics_packing_units_terminal check (
        (status = 'Draft' and finalized_at_utc is null and cancelled_at_utc is null)
        or (status = 'Finalized' and finalized_at_utc is not null and finalized_by_user_id is not null and cancelled_at_utc is null)
        or (status = 'Cancelled' and cancelled_at_utc is not null and cancelled_by_user_id is not null and finalized_at_utc is null)
    )
);

create index if not exists ix_logistics_packing_units_project
    on logistics_packing_units(project_id, status, unit_number);

create table if not exists logistics_packing_unit_panels (
    packing_unit_id uuid not null references logistics_packing_units(id) on delete restrict,
    panel_id uuid not null references panel_placeholders(id) on delete restrict,
    active boolean not null default true,
    added_by_user_id uuid not null references qms_users(id),
    added_at_utc timestamptz not null default now(),
    primary key (packing_unit_id, panel_id)
);

create unique index if not exists ux_logistics_packing_unit_panels_active_panel
    on logistics_packing_unit_panels(panel_id) where active;

create table if not exists logistics_batches (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    stage_code text not null references workflow_stages(stage_code),
    batch_number integer not null,
    status text not null default 'Draft',
    version integer not null default 1,
    departure_date date null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    finalized_by_user_id uuid null references qms_users(id),
    finalized_at_utc timestamptz null,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    constraint ux_logistics_batches_project_stage_number unique (project_id, stage_code, batch_number),
    constraint ck_logistics_batches_stage check (stage_code in ('DepartureProcessed', 'DeliveryCompleted')),
    constraint ck_logistics_batches_number check (batch_number >= 1),
    constraint ck_logistics_batches_status check (status in ('Draft', 'Finalized', 'Cancelled')),
    constraint ck_logistics_batches_version check (version >= 1),
    constraint ck_logistics_batches_departure check (stage_code <> 'DeliveryCompleted' or departure_date is null),
    constraint ck_logistics_batches_terminal check (
        (status = 'Draft' and finalized_at_utc is null and cancelled_at_utc is null)
        or (status = 'Finalized' and finalized_at_utc is not null and finalized_by_user_id is not null and cancelled_at_utc is null)
        or (status = 'Cancelled' and cancelled_at_utc is not null and cancelled_by_user_id is not null and finalized_at_utc is null)
    )
);

create index if not exists ix_logistics_batches_project_stage
    on logistics_batches(project_id, stage_code, status, batch_number);

create table if not exists logistics_batch_units (
    batch_id uuid not null references logistics_batches(id) on delete restrict,
    packing_unit_id uuid not null references logistics_packing_units(id) on delete restrict,
    stage_code text not null,
    active boolean not null default true,
    added_by_user_id uuid not null references qms_users(id),
    added_at_utc timestamptz not null default now(),
    primary key (batch_id, packing_unit_id),
    constraint ck_logistics_batch_units_stage check (stage_code in ('DepartureProcessed', 'DeliveryCompleted'))
);

create unique index if not exists ux_logistics_batch_units_active_stage
    on logistics_batch_units(packing_unit_id, stage_code) where active;

create table if not exists logistics_evidence (
    id uuid primary key,
    owner_type text not null,
    packing_unit_id uuid null references logistics_packing_units(id) on delete restrict,
    batch_id uuid null references logistics_batches(id) on delete restrict,
    display_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    alt_text text null,
    content bytea not null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_logistics_evidence_owner_type check (owner_type in ('PackingPhoto', 'DeparturePhoto', 'DeliveryDocument')),
    constraint ck_logistics_evidence_owner check (
        (owner_type = 'PackingPhoto' and packing_unit_id is not null and batch_id is null)
        or (owner_type in ('DeparturePhoto', 'DeliveryDocument') and packing_unit_id is null and batch_id is not null)
    ),
    constraint ck_logistics_evidence_name check (display_name ~ '^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$'),
    constraint ck_logistics_evidence_mime check (normalized_mime in ('image/jpeg', 'image/png', 'application/pdf')),
    constraint ck_logistics_evidence_size check (byte_size = octet_length(content) and byte_size > 0 and byte_size <= 10485760),
    constraint ck_logistics_evidence_sha check (sha256 ~ '^[0-9a-f]{64}$'),
    constraint ck_logistics_evidence_alt check (
        (owner_type in ('PackingPhoto', 'DeparturePhoto') and char_length(btrim(alt_text)) between 2 and 160)
        or (owner_type = 'DeliveryDocument' and (alt_text is null or char_length(btrim(alt_text)) between 2 and 160))
    )
);

create index if not exists ix_logistics_evidence_unit on logistics_evidence(packing_unit_id, created_at_utc);
create index if not exists ix_logistics_evidence_batch on logistics_evidence(batch_id, created_at_utc);

create table if not exists logistics_delivery_results (
    batch_id uuid not null references logistics_batches(id) on delete restrict,
    packing_unit_id uuid not null references logistics_packing_units(id) on delete restrict,
    panel_id uuid not null references panel_placeholders(id) on delete restrict,
    delivered_by_user_id uuid not null references qms_users(id),
    delivered_at_utc timestamptz not null default now(),
    primary key (batch_id, panel_id),
    constraint ux_logistics_delivery_results_panel unique (panel_id)
);

create table if not exists logistics_operations (
    operation_id uuid primary key,
    action text not null,
    project_id uuid not null references projects(id) on delete restrict,
    packing_unit_id uuid null references logistics_packing_units(id) on delete restrict,
    batch_id uuid null references logistics_batches(id) on delete restrict,
    actor_user_id uuid not null references qms_users(id),
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_logistics_operations_action check (char_length(btrim(action)) between 3 and 80),
    constraint ck_logistics_operations_fingerprint check (payload_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_logistics_operations_projection check (octet_length(result_projection::text) <= 4096)
);

create or replace function guard_finalized_logistics_owner()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    if tg_op = 'DELETE' and old.status = 'Finalized' then
        raise exception 'Finalized logistics records are immutable.';
    end if;
    if tg_op = 'UPDATE' and old.status = 'Finalized' and new is distinct from old then
        raise exception 'Finalized logistics records are immutable.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

drop trigger if exists trg_guard_finalized_logistics_packing_unit on logistics_packing_units;
create trigger trg_guard_finalized_logistics_packing_unit
before update or delete on logistics_packing_units
for each row execute function guard_finalized_logistics_owner();

drop trigger if exists trg_guard_finalized_logistics_batch on logistics_batches;
create trigger trg_guard_finalized_logistics_batch
before update or delete on logistics_batches
for each row execute function guard_finalized_logistics_owner();

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
        elsif tg_table_name = 'logistics_batch_units' or tg_table_name = 'logistics_delivery_results' then
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
        elsif tg_table_name = 'logistics_batch_units' or tg_table_name = 'logistics_delivery_results' then
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

drop trigger if exists trg_guard_logistics_packing_unit_panel on logistics_packing_unit_panels;
create trigger trg_guard_logistics_packing_unit_panel before insert or update or delete on logistics_packing_unit_panels
for each row execute function guard_finalized_logistics_child();
drop trigger if exists trg_guard_logistics_batch_unit on logistics_batch_units;
create trigger trg_guard_logistics_batch_unit before insert or update or delete on logistics_batch_units
for each row execute function guard_finalized_logistics_child();
drop trigger if exists trg_guard_logistics_evidence on logistics_evidence;
create trigger trg_guard_logistics_evidence before insert or update or delete on logistics_evidence
for each row execute function guard_finalized_logistics_child();
drop trigger if exists trg_guard_logistics_delivery_result on logistics_delivery_results;
create trigger trg_guard_logistics_delivery_result before insert or update or delete on logistics_delivery_results
for each row execute function guard_finalized_logistics_child();

create or replace function guard_append_only_logistics_operation()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' and tg_op = 'DELETE' then return old; end if;
    raise exception 'Logistics operation receipts are append-only.';
end $$;

drop trigger if exists trg_guard_logistics_operation on logistics_operations;
create trigger trg_guard_logistics_operation before update or delete on logistics_operations
for each row execute function guard_append_only_logistics_operation();
