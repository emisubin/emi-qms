insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000028', 'sales.settle', 'Complete sales settlement')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select roles.id, permissions.id
from roles
join permissions on permissions.code = 'sales.settle'
where roles.code = 'sales'
on conflict do nothing;

alter table projects add column if not exists completed_by_user_id uuid null references qms_users(id);
alter table projects add column if not exists completed_at_utc timestamptz null;

alter table projects add constraint ck_projects_completion_metadata
    check (
        (completed_by_user_id is null and completed_at_utc is null)
        or (completed_by_user_id is not null and completed_at_utc is not null and status = 'Completed')
    );

create table sales_settlements (
    id uuid primary key,
    project_id uuid not null references projects(id) on delete restrict,
    status text not null default 'Draft',
    invoice_issued_date date null,
    invoice_number text null,
    note text null,
    version integer not null default 1,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    updated_by_user_id uuid not null references qms_users(id),
    updated_at_utc timestamptz not null default now(),
    completed_by_user_id uuid null references qms_users(id),
    completed_at_utc timestamptz null,
    cancelled_by_user_id uuid null references qms_users(id),
    cancelled_at_utc timestamptz null,
    constraint ux_sales_settlements_project unique (project_id),
    constraint ck_sales_settlements_status check (status in ('Draft', 'Completed', 'Cancelled')),
    constraint ck_sales_settlements_version check (version >= 1),
    constraint ck_sales_settlements_invoice_number check (
        invoice_number is null or char_length(btrim(invoice_number)) between 1 and 64
    ),
    constraint ck_sales_settlements_note check (
        note is null or char_length(btrim(note)) between 1 and 500
    ),
    constraint ck_sales_settlements_terminal check (
        (status = 'Draft' and completed_at_utc is null and completed_by_user_id is null and cancelled_at_utc is null and cancelled_by_user_id is null)
        or (status = 'Completed' and invoice_issued_date is not null and completed_at_utc is not null and completed_by_user_id is not null and cancelled_at_utc is null and cancelled_by_user_id is null)
        or (status = 'Cancelled' and completed_at_utc is null and completed_by_user_id is null and cancelled_at_utc is not null and cancelled_by_user_id is not null)
    )
);

create index ix_sales_settlements_status on sales_settlements(status, updated_at_utc desc);

create table sales_settlement_operations (
    operation_id uuid primary key,
    settlement_id uuid not null references sales_settlements(id) on delete restrict,
    project_id uuid not null references projects(id) on delete restrict,
    action text not null,
    actor_user_id uuid not null references qms_users(id),
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_sales_settlement_operations_action check (char_length(btrim(action)) between 3 and 80),
    constraint ck_sales_settlement_operations_fingerprint check (payload_fingerprint ~ '^[0-9a-f]{64}$'),
    constraint ck_sales_settlement_operations_projection check (octet_length(result_projection::text) <= 4096)
);

create index ix_sales_settlement_operations_project on sales_settlement_operations(project_id, created_at_utc desc);

create or replace function guard_completed_sales_settlement()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    if old.status = 'Completed' then
        raise exception 'Completed sales settlement records are immutable.';
    end if;
    return case when tg_op = 'DELETE' then old else new end;
end $$;

create trigger trg_guard_completed_sales_settlement
before update or delete on sales_settlements
for each row execute function guard_completed_sales_settlement();

create or replace function guard_append_only_sales_settlement_operation()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' and tg_op = 'DELETE' then
        return old;
    end if;
    raise exception 'Sales settlement operation receipts are append-only.';
end $$;

create trigger trg_guard_sales_settlement_operation
before update or delete on sales_settlement_operations
for each row execute function guard_append_only_sales_settlement_operation();

create or replace function guard_pending_project_lifecycle()
returns trigger language plpgsql as $$
declare
    project_status text;
    project_deleted_at timestamptz;
begin
    select status, deleted_at_utc
    into project_status, project_deleted_at
    from projects
    where id = new.project_id
    for update;

    if project_status is null then
        return new;
    end if;

    if project_status in ('Completed', 'Cancelled') or project_deleted_at is not null then
        raise exception using
            errcode = '23514',
            constraint = 'ck_pending_issues_project_lifecycle',
            message = '완료·취소·삭제된 프로젝트에는 Pending을 등록할 수 없습니다.';
    end if;

    return new;
end $$;

create trigger trg_guard_pending_project_lifecycle
before insert on pending_issues
for each row execute function guard_pending_project_lifecycle();
