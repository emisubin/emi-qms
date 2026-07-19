insert into permissions (id, code, name)
values ('30000000-0000-0000-0000-000000000045', 'PendingType.Manage', 'Manage pending issue types')
on conflict (code) do update set name = excluded.name;

insert into role_permissions (role_id, permission_id)
select role.id, permission.id
from roles role
join permissions permission on permission.code = 'PendingType.Manage'
where role.code = 'system-administrator'
on conflict do nothing;

delete from role_permissions
using roles, permissions
where role_permissions.role_id = roles.id
  and role_permissions.permission_id = permissions.id
  and permissions.code = 'PendingType.Manage'
  and roles.code <> 'system-administrator';

create table if not exists pending_issue_type_catalog (
    code text primary key,
    display_name text not null,
    description text null,
    sort_order integer not null,
    is_system boolean not null,
    is_manual_enabled boolean not null,
    is_active boolean not null,
    row_version integer not null default 1,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ux_pending_issue_type_catalog_sort unique (sort_order) deferrable initially immediate,
    constraint ck_pending_issue_type_catalog_code check (
        code in ('Nonconformance', 'Punch', 'ManufacturingStop', 'Other')
        or code ~ '^CUSTOM_[A-F0-9]{32}$'
    ),
    constraint ck_pending_issue_type_catalog_system check (
        is_system = (code in ('Nonconformance', 'Punch', 'ManufacturingStop', 'Other'))
    ),
    constraint ck_pending_issue_type_catalog_name check (char_length(btrim(display_name)) between 2 and 80),
    constraint ck_pending_issue_type_catalog_description check (
        description is null or char_length(btrim(description)) between 2 and 300
    ),
    constraint ck_pending_issue_type_catalog_sort check (sort_order between 1 and 10000),
    constraint ck_pending_issue_type_catalog_row_version check (row_version >= 1),
    constraint ck_pending_issue_type_catalog_system_active check (not is_system or is_active),
    constraint ck_pending_issue_type_catalog_other_fallback check (
        code <> 'Other' or (is_active and is_manual_enabled)
    )
);

create unique index if not exists ux_pending_issue_type_catalog_name
    on pending_issue_type_catalog(lower(btrim(display_name)));

insert into pending_issue_type_catalog (
    code, display_name, description, sort_order, is_system, is_manual_enabled, is_active)
values
    ('Nonconformance', '부적합', '검사 및 자재 품질 기준 미달', 1, true, true, true),
    ('Punch', 'PUNCH', '고객 검수 및 입회 검사 지적', 2, true, true, true),
    ('ManufacturingStop', '제조 중단', '제조 작업을 차단하는 현장 이슈', 3, true, true, true),
    ('Other', '기타', '수동 등록을 위한 기본 유형', 4, true, true, true)
on conflict (code) do nothing;

alter table pending_issues
    drop constraint if exists ck_pending_issues_type,
    drop constraint if exists fk_pending_issues_issue_type_catalog,
    add constraint fk_pending_issues_issue_type_catalog
        foreign key (issue_type) references pending_issue_type_catalog(code)
        on update restrict on delete restrict;

create table if not exists pending_issue_type_audit_events (
    id uuid primary key default uuid_generate_v4(),
    action text not null,
    issue_type_code text not null references pending_issue_type_catalog(code) on update restrict on delete restrict,
    actor_user_id uuid not null references qms_users(id) on delete restrict,
    previous_value jsonb null,
    next_value jsonb null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_pending_issue_type_audit_action check (
        action in ('CustomCreated', 'Updated', 'Reordered', 'Activated', 'Deactivated')
    ),
    constraint ck_pending_issue_type_audit_previous check (
        previous_value is null
        or (jsonb_typeof(previous_value) = 'object' and octet_length(previous_value::text) <= 2048)
    ),
    constraint ck_pending_issue_type_audit_next check (
        next_value is null
        or (jsonb_typeof(next_value) = 'object' and octet_length(next_value::text) <= 2048)
    )
);

create index if not exists ix_pending_issue_type_audit_time
    on pending_issue_type_audit_events(occurred_at_utc desc, id);

create or replace function guard_pending_issue_type_catalog()
returns trigger language plpgsql as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'Pending issue types cannot be deleted.';
    end if;
    if new.code <> old.code or new.is_system <> old.is_system then
        raise exception 'Pending issue type identity is immutable.';
    end if;
    if new.row_version <> old.row_version + 1 then
        raise exception 'Pending issue type update must increment row version.';
    end if;
    if old.is_system and not new.is_active then
        raise exception 'System pending issue types must remain active.';
    end if;
    if old.code = 'Other' and not new.is_manual_enabled then
        raise exception 'The Other pending issue type must remain available for manual entry.';
    end if;
    return new;
end $$;

drop trigger if exists trg_guard_pending_issue_type_catalog on pending_issue_type_catalog;
create trigger trg_guard_pending_issue_type_catalog
before update or delete on pending_issue_type_catalog
for each row execute function guard_pending_issue_type_catalog();

create or replace function guard_append_only_pending_issue_type_audit()
returns trigger language plpgsql as $$
begin
    raise exception 'Pending issue type audit events are append-only.';
end $$;

drop trigger if exists trg_guard_pending_issue_type_audit on pending_issue_type_audit_events;
create trigger trg_guard_pending_issue_type_audit
before update or delete on pending_issue_type_audit_events
for each row execute function guard_append_only_pending_issue_type_audit();
