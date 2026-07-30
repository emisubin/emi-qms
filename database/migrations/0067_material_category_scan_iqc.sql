create table if not exists material_categories (
    id uuid primary key,
    code text not null unique,
    display_name text not null,
    requires_iqc boolean not null,
    is_active boolean not null default true,
    display_order integer not null,
    row_version integer not null default 1,
    created_by_user_id uuid null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_material_categories_code check (code ~ '^[A-Z0-9_]{2,40}$'),
    constraint ck_material_categories_name check (char_length(btrim(display_name)) between 1 and 80),
    constraint ck_material_categories_order check (display_order between 1 and 1000),
    constraint ck_material_categories_version check (row_version >= 1)
);

create unique index if not exists ux_material_categories_normalized_name
    on material_categories(upper(btrim(display_name)));

insert into material_categories (
    id, code, display_name, requires_iqc, is_active, display_order
)
values
    ('67000000-0000-0000-0000-000000000001', 'ENCLOSURE', '외함', true, true, 10),
    ('67000000-0000-0000-0000-000000000002', 'SHEET_METAL', '판금류', false, true, 20),
    ('67000000-0000-0000-0000-000000000003', 'BUSBAR', '부스바', false, true, 30),
    ('67000000-0000-0000-0000-000000000004', 'NAMEPLATE', '명판', false, true, 40),
    ('67000000-0000-0000-0000-000000000005', 'OTHER', '기타', false, true, 50)
on conflict (code) do nothing;

create table if not exists material_category_audit_events (
    id uuid primary key default uuid_generate_v4(),
    category_id uuid not null references material_categories(id) on delete restrict,
    action text not null,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    old_value jsonb null,
    new_value jsonb null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_material_category_audit_action check (
        action in ('Created', 'Updated', 'Deactivated', 'Reactivated')
    ),
    constraint ck_material_category_audit_old_value check (
        old_value is null or (jsonb_typeof(old_value) = 'object' and octet_length(old_value::text) <= 4096)
    ),
    constraint ck_material_category_audit_new_value check (
        new_value is null or (jsonb_typeof(new_value) = 'object' and octet_length(new_value::text) <= 4096)
    )
);

create index if not exists ix_material_category_audit_events_time
    on material_category_audit_events(occurred_at_utc desc, id);

create or replace function guard_material_category_audit_append_only()
returns trigger language plpgsql as $$
begin
    raise exception 'Material category audit events are append-only.' using errcode = 'P0001';
end $$;

drop trigger if exists trg_guard_material_category_audit_append_only on material_category_audit_events;
create trigger trg_guard_material_category_audit_append_only
before update or delete on material_category_audit_events
for each row execute function guard_material_category_audit_append_only();

alter table projects
    add column if not exists iqc_routing_policy text null;

update projects
set iqc_routing_policy = 'AllReceipts'
where iqc_routing_policy is null;

alter table projects
    alter column iqc_routing_policy set default 'AllReceipts',
    alter column iqc_routing_policy set not null;

do $$
begin
    alter table projects
        add constraint ck_projects_iqc_routing_policy
        check (iqc_routing_policy in ('AllReceipts', 'CategoryBased'));
exception
    when duplicate_object then null;
end $$;

create or replace function guard_project_iqc_routing_policy_immutable()
returns trigger language plpgsql as $$
begin
    if new.iqc_routing_policy is distinct from old.iqc_routing_policy then
        raise exception 'Project IQC routing policy is immutable.' using errcode = 'P0001';
    end if;
    return new;
end $$;

drop trigger if exists trg_guard_project_iqc_routing_policy_immutable on projects;
create trigger trg_guard_project_iqc_routing_policy_immutable
before update of iqc_routing_policy on projects
for each row execute function guard_project_iqc_routing_policy_immutable();

alter table project_procurement_items
    add column if not exists material_category_id uuid null references material_categories(id) on delete restrict,
    add column if not exists material_category_code_snapshot text null,
    add column if not exists material_category_name_snapshot text null,
    add column if not exists material_category_requires_iqc_snapshot boolean null;

do $$
begin
    alter table project_procurement_items
        add constraint ck_project_procurement_items_material_category_snapshot
        check (
            (
                material_category_id is null
                and material_category_code_snapshot is null
                and material_category_name_snapshot is null
                and material_category_requires_iqc_snapshot is null
            )
            or
            (
                material_category_id is not null
                and material_category_code_snapshot ~ '^[A-Z0-9_]{2,40}$'
                and char_length(btrim(material_category_name_snapshot)) between 1 and 80
                and material_category_requires_iqc_snapshot is not null
            )
        );
exception
    when duplicate_object then null;
end $$;

create index if not exists ix_project_procurement_items_material_category
    on project_procurement_items(material_category_id)
    where material_category_id is not null;

alter table material_iqc_attempts
    drop constraint if exists ck_material_iqc_attempts_decision_mode,
    add constraint ck_material_iqc_attempts_decision_mode
        check (decision_mode in ('Legacy', 'Detailed', 'ScanBased'));

alter table material_receipts
    drop constraint if exists ck_material_receipts_status,
    add constraint ck_material_receipts_status check (
        status in (
            'Arrived', 'IqcRequested', 'Passed', 'FailedBlocked',
            'InspectionNotRequired', 'Confirmed', 'Cancelled'
        )
    );

alter table material_receipt_events
    drop constraint if exists ck_material_receipt_events_type,
    add constraint ck_material_receipt_events_type check (
        event_type in (
            'LegacyBackfilled', 'Arrived', 'IqcRequested', 'IqcNotRequired',
            'IqcPassed', 'IqcFailed', 'ReinspectionRequested', 'Confirmed',
            'Cancelled', 'ArrivalsClosed', 'DerivedCompletionChanged'
        )
    );

create table if not exists material_iqc_scan_reports (
    id uuid primary key default uuid_generate_v4(),
    attempt_id uuid not null unique references material_iqc_attempts(id) on delete restrict,
    status text not null default 'Draft',
    version integer not null default 1,
    result text null,
    reason text null,
    snapshot_sha256 text null,
    finalized_by_user_id uuid null references qms_users(id) on delete restrict,
    finalized_at_utc timestamptz null,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id) on delete restrict,
    updated_at_utc timestamptz not null default now(),
    constraint ck_material_iqc_scan_reports_status check (status in ('Draft', 'Finalized')),
    constraint ck_material_iqc_scan_reports_version check (version >= 1),
    constraint ck_material_iqc_scan_reports_result check (result is null or result in ('Passed', 'Failed')),
    constraint ck_material_iqc_scan_reports_reason check (
        reason is null or char_length(btrim(reason)) between 3 and 1000
    ),
    constraint ck_material_iqc_scan_reports_snapshot check (
        snapshot_sha256 is null or snapshot_sha256 ~ '^[a-f0-9]{64}$'
    ),
    constraint ck_material_iqc_scan_reports_lifecycle check (
        (
            status = 'Draft'
            and finalized_by_user_id is null
            and finalized_at_utc is null
            and snapshot_sha256 is null
        )
        or
        (
            status = 'Finalized'
            and result in ('Passed', 'Failed')
            and reason is not null
            and snapshot_sha256 is not null
            and finalized_by_user_id is not null
            and finalized_at_utc is not null
        )
    )
);

create table if not exists material_iqc_scan_attachments (
    id uuid primary key default uuid_generate_v4(),
    scan_report_id uuid not null references material_iqc_scan_reports(id) on delete restrict,
    original_file_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    content bytea not null,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    constraint ck_material_iqc_scan_attachments_name check (
        char_length(btrim(original_file_name)) between 1 and 180
    ),
    constraint ck_material_iqc_scan_attachments_mime check (
        normalized_mime in ('application/pdf', 'image/jpeg', 'image/png')
    ),
    constraint ck_material_iqc_scan_attachments_size check (
        byte_size between 1 and 10485760 and octet_length(content) = byte_size
    ),
    constraint ck_material_iqc_scan_attachments_sha check (sha256 ~ '^[a-f0-9]{64}$')
);

create index if not exists ix_material_iqc_scan_attachments_report
    on material_iqc_scan_attachments(scan_report_id, created_at_utc, id);

create or replace function guard_finalized_material_iqc_scan_report()
returns trigger language plpgsql as $$
begin
    if old.status = 'Finalized' then
        raise exception 'Finalized material IQC scan report is immutable.' using errcode = 'P0001';
    end if;
    return new;
end $$;

drop trigger if exists trg_guard_finalized_material_iqc_scan_report on material_iqc_scan_reports;
create trigger trg_guard_finalized_material_iqc_scan_report
before update or delete on material_iqc_scan_reports
for each row execute function guard_finalized_material_iqc_scan_report();

create or replace function guard_finalized_material_iqc_scan_attachment()
returns trigger language plpgsql as $$
declare
    parent_report_id uuid;
    parent_status text;
begin
    parent_report_id := case when tg_op = 'DELETE' then old.scan_report_id else new.scan_report_id end;
    select status into parent_status
    from material_iqc_scan_reports
    where id = parent_report_id;
    if parent_status = 'Finalized' then
        raise exception 'Finalized material IQC scan evidence is immutable.' using errcode = 'P0001';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

drop trigger if exists trg_guard_finalized_material_iqc_scan_attachment on material_iqc_scan_attachments;
create trigger trg_guard_finalized_material_iqc_scan_attachment
before insert or update or delete on material_iqc_scan_attachments
for each row execute function guard_finalized_material_iqc_scan_attachment();
