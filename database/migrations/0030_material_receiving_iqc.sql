alter table project_procurement_items
    add column if not exists order_quantity numeric(18, 3) null,
    add column if not exists order_unit text null,
    add column if not exists material_arrivals_closed_at_utc timestamptz null,
    add column if not exists material_arrivals_closed_by_user_id uuid null references qms_users(id);

do $$
begin
    alter table project_procurement_items
        add constraint ck_project_procurement_items_order_measurement
        check (
            (order_quantity is null and order_unit is null)
            or (order_quantity > 0 and char_length(btrim(order_unit)) between 1 and 20)
        );
exception
    when duplicate_object then null;
end $$;

create table if not exists material_receipts (
    id uuid primary key default uuid_generate_v4(),
    procurement_item_id uuid not null references project_procurement_items(id) on delete restrict,
    quantity numeric(18, 3) null,
    unit text null,
    arrival_date date not null,
    note text null,
    status text not null default 'Arrived',
    is_legacy boolean not null default false,
    version integer not null default 1,
    created_by_user_id uuid null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    confirmed_by_user_id uuid null references qms_users(id),
    confirmed_at_utc timestamptz null,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    cancellation_reason text null,
    constraint ck_material_receipts_status check (
        status in ('Arrived', 'IqcRequested', 'Passed', 'FailedBlocked', 'Confirmed', 'Cancelled')
    ),
    constraint ck_material_receipts_measurement check (
        (is_legacy and quantity is null and unit is null)
        or (not is_legacy and quantity > 0 and char_length(btrim(unit)) between 1 and 20)
    ),
    constraint ck_material_receipts_version check (version >= 1),
    constraint ck_material_receipts_confirmed check (
        (status = 'Confirmed' and (is_legacy or confirmed_by_user_id is not null) and confirmed_at_utc is not null)
        or (status <> 'Confirmed' and confirmed_by_user_id is null and confirmed_at_utc is null)
    ),
    constraint ck_material_receipts_cancelled check (
        (status = 'Cancelled' and cancelled_by_user_id is not null and cancelled_at_utc is not null
            and char_length(btrim(cancellation_reason)) between 3 and 500)
        or (status <> 'Cancelled' and cancelled_by_user_id is null and cancelled_at_utc is null and cancellation_reason is null)
    ),
    constraint ck_material_receipts_note check (note is null or char_length(btrim(note)) between 1 and 1000)
);

create index if not exists ix_material_receipts_item_status
    on material_receipts(procurement_item_id, status, arrival_date desc, created_at_utc desc);

create unique index if not exists ux_material_receipts_legacy_item
    on material_receipts(procurement_item_id)
    where is_legacy;

create table if not exists material_iqc_attempts (
    id uuid primary key default uuid_generate_v4(),
    material_receipt_id uuid not null references material_receipts(id) on delete restrict,
    attempt_number integer not null,
    status text not null default 'Requested',
    reason text null,
    pending_issue_id uuid null references pending_issues(id) on delete restrict,
    requested_by_user_id uuid not null references qms_users(id),
    requested_at_utc timestamptz not null default now(),
    decided_by_user_id uuid null references qms_users(id),
    decided_at_utc timestamptz null,
    constraint ux_material_iqc_attempts_receipt_number unique (material_receipt_id, attempt_number),
    constraint ck_material_iqc_attempts_number check (attempt_number >= 1),
    constraint ck_material_iqc_attempts_status check (status in ('Requested', 'Passed', 'Failed')),
    constraint ck_material_iqc_attempts_decision check (
        (status = 'Requested' and decided_by_user_id is null and decided_at_utc is null and reason is null)
        or (status in ('Passed', 'Failed') and decided_by_user_id is not null and decided_at_utc is not null
            and char_length(btrim(reason)) between 3 and 1000)
    )
);

create unique index if not exists ux_material_iqc_attempts_open_receipt
    on material_iqc_attempts(material_receipt_id)
    where status = 'Requested';

create index if not exists ix_material_iqc_attempts_pending
    on material_iqc_attempts(pending_issue_id)
    where pending_issue_id is not null;

create table if not exists material_receipt_events (
    id uuid primary key default uuid_generate_v4(),
    procurement_item_id uuid not null references project_procurement_items(id) on delete restrict,
    material_receipt_id uuid null references material_receipts(id) on delete restrict,
    event_type text not null,
    from_status text null,
    to_status text null,
    reason text null,
    changed_by_user_id uuid null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    constraint ck_material_receipt_events_type check (
        event_type in ('LegacyBackfilled', 'Arrived', 'IqcRequested', 'IqcPassed', 'IqcFailed',
            'ReinspectionRequested', 'Confirmed', 'Cancelled', 'ArrivalsClosed', 'DerivedCompletionChanged')
    ),
    constraint ck_material_receipt_events_reason check (reason is null or char_length(btrim(reason)) between 1 and 1000)
);

create index if not exists ix_material_receipt_events_item
    on material_receipt_events(procurement_item_id, created_at_utc desc, id);

insert into material_receipts (
    procurement_item_id, arrival_date, note, status, is_legacy, version,
    created_by_user_id, created_at_utc, updated_by_user_id, updated_at_utc,
    confirmed_by_user_id, confirmed_at_utc
)
select
    item.id,
    coalesce((item.receipt_completed_at_utc at time zone 'Asia/Seoul')::date, current_date),
    nullif(btrim(item.receipt_completion_note), ''),
    'Confirmed',
    true,
    1,
    item.receipt_completed_by_user_id,
    coalesce(item.receipt_completed_at_utc, item.updated_at_utc, now()),
    item.receipt_completed_by_user_id,
    coalesce(item.receipt_completed_at_utc, item.updated_at_utc, now()),
    item.receipt_completed_by_user_id,
    coalesce(item.receipt_completed_at_utc, item.updated_at_utc, now())
from project_procurement_items item
where item.receipt_completed = true
  and not exists (
      select 1 from material_receipts receipt where receipt.procurement_item_id = item.id and receipt.is_legacy
  );

update project_procurement_items item
set material_arrivals_closed_at_utc = coalesce(item.material_arrivals_closed_at_utc, item.receipt_completed_at_utc, item.updated_at_utc, now()),
    material_arrivals_closed_by_user_id = coalesce(item.material_arrivals_closed_by_user_id, item.receipt_completed_by_user_id)
where item.receipt_completed = true
  ;

insert into material_receipt_events (
    procurement_item_id, material_receipt_id, event_type, to_status, reason, changed_by_user_id, created_at_utc
)
select
    receipt.procurement_item_id,
    receipt.id,
    'LegacyBackfilled',
    'Confirmed',
    '기존 입고 완료 기록 이관',
    receipt.confirmed_by_user_id,
    receipt.confirmed_at_utc
from material_receipts receipt
where receipt.is_legacy
  and not exists (
      select 1
      from material_receipt_events event
      where event.material_receipt_id = receipt.id and event.event_type = 'LegacyBackfilled'
  );

create or replace function guard_material_receipt_projection_write()
returns trigger
language plpgsql
as $$
begin
    if (new.receipt_completed, new.receipt_completed_at_utc, new.receipt_completed_by_user_id, new.receipt_completion_note)
       is distinct from
       (old.receipt_completed, old.receipt_completed_at_utc, old.receipt_completed_by_user_id, old.receipt_completion_note)
       and coalesce(current_setting('emi_qms.material_receipt_write', true), '') <> 'allowed' then
        raise exception 'receipt completion is a derived material receiving projection'
            using errcode = 'P0001';
    end if;
    return new;
end;
$$;

drop trigger if exists trg_guard_material_receipt_projection_write on project_procurement_items;
create trigger trg_guard_material_receipt_projection_write
before update on project_procurement_items
for each row execute function guard_material_receipt_projection_write();
