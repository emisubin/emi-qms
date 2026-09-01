-- TASK-SITE-ACCESS-001: follows the public G2 migration numbered 0084.
create table site_access_coverage_state (
    singleton boolean primary key default true check (singleton),
    coverage_started_at_utc timestamptz not null default clock_timestamp()
);

insert into site_access_coverage_state (singleton)
values (true)
on conflict (singleton) do nothing;

create or replace function qms_site_access_menu_codes_valid(p_menu_codes text[])
returns boolean
language sql
immutable
as $$
    select coalesce(cardinality(p_menu_codes), 0) > 0
       and cardinality(p_menu_codes) = (
            select count(distinct menu_code)
            from unnest(p_menu_codes) menu_code)
       and not exists (
            select 1
            from unnest(p_menu_codes) menu_code
            where menu_code not in (
                'Home', 'PrivacyNotice', 'NoticeBoard', 'MyWork', 'TeamsActivity',
                'Projects', 'Sales', 'G2', 'FormTemplates', 'ProductionPlanning',
                'Procurement', 'Materials', 'Manufacturing', 'Quality', 'Logistics',
                'Notifications', 'NotificationSettings', 'Pending', 'Administration'
            )
       );
$$;

create table site_access_sessions (
    id uuid primary key,
    actor_user_id uuid not null,
    actor_display_name text not null check (char_length(actor_display_name) between 1 and 160),
    actor_department_name text null check (actor_department_name is null or char_length(actor_department_name) <= 160),
    browser_client_id uuid not null,
    idempotency_receipt uuid not null unique,
    started_at_utc timestamptz not null,
    last_activity_at_utc timestamptz not null,
    ended_at_utc timestamptz null,
    end_reason text null check (end_reason is null or end_reason = 'ExplicitLogout'),
    client_ip inet null,
    browser_family text not null check (browser_family in ('Chrome', 'Edge', 'Firefox', 'Safari', 'Other')),
    os_family text not null check (os_family in ('Windows', 'macOS', 'iOS', 'Android', 'Linux', 'Other')),
    app_access_outcome text not null check (app_access_outcome in ('Allowed', 'ApprovalPending', 'Inactive')),
    menu_codes text[] not null,
    constraint ck_site_access_time_order check (
        last_activity_at_utc >= started_at_utc
        and (ended_at_utc is null or ended_at_utc >= last_activity_at_utc)
    ),
    constraint ck_site_access_end_reason check (
        (ended_at_utc is null and end_reason is null)
        or (ended_at_utc is not null and end_reason = 'ExplicitLogout')
    ),
    constraint ck_site_access_menu_codes check (qms_site_access_menu_codes_valid(menu_codes))
);

create index ix_site_access_sessions_time
    on site_access_sessions(started_at_utc desc, id desc);

create index ix_site_access_sessions_actor_time
    on site_access_sessions(actor_user_id, started_at_utc desc);

create index ix_site_access_sessions_active_client
    on site_access_sessions(actor_user_id, browser_client_id, last_activity_at_utc desc)
    where ended_at_utc is null;

create or replace function qms_site_access_guard_updates()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'Site access records cannot be deleted.';
    end if;

    if new.id is distinct from old.id
        or new.actor_user_id is distinct from old.actor_user_id
        or new.actor_display_name is distinct from old.actor_display_name
        or new.actor_department_name is distinct from old.actor_department_name
        or new.browser_client_id is distinct from old.browser_client_id
        or new.idempotency_receipt is distinct from old.idempotency_receipt
        or new.started_at_utc is distinct from old.started_at_utc
        or new.client_ip is distinct from old.client_ip
        or new.browser_family is distinct from old.browser_family
        or new.os_family is distinct from old.os_family
        or new.app_access_outcome is distinct from old.app_access_outcome then
        raise exception 'Site access identity metadata is immutable.';
    end if;

    if old.ended_at_utc is not null then
        raise exception 'Ended site access records are immutable.';
    end if;

    if new.last_activity_at_utc < old.last_activity_at_utc then
        raise exception 'Site access activity time cannot move backwards.';
    end if;

    if cardinality(new.menu_codes) < cardinality(old.menu_codes)
        or new.menu_codes[1:cardinality(old.menu_codes)] is distinct from old.menu_codes then
        raise exception 'Site access menu history is append-only.';
    end if;

    if new.ended_at_utc is not null and new.end_reason is distinct from 'ExplicitLogout' then
        raise exception 'Only explicit logout can end a site access record.';
    end if;

    if new.ended_at_utc is null and new.end_reason is not null then
        raise exception 'Site access end reason requires an end time.';
    end if;

    return new;
end;
$$;

create trigger trg_site_access_coverage_append_only
before update or delete on site_access_coverage_state
for each row execute function qms_audit_guard_append_only();

create trigger trg_site_access_sessions_guard
before update or delete on site_access_sessions
for each row execute function qms_site_access_guard_updates();

create or replace function qms_record_site_access(
    p_actor_user_id uuid,
    p_browser_client_id uuid,
    p_menu_code text,
    p_app_access_outcome text,
    p_client_ip inet,
    p_browser_family text,
    p_os_family text)
returns table(
    session_id uuid,
    idempotency_receipt uuid,
    started_at_utc timestamptz,
    last_activity_at_utc timestamptz,
    created boolean)
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    observed_at_utc timestamptz;
    actor_name text;
    actor_department text;
    active_session_id uuid;
begin
    if p_actor_user_id is null
        or p_browser_client_id is null
        or not qms_site_access_menu_codes_valid(array[p_menu_code])
        or p_app_access_outcome not in ('Allowed', 'ApprovalPending', 'Inactive')
        or p_browser_family not in ('Chrome', 'Edge', 'Firefox', 'Safari', 'Other')
        or p_os_family not in ('Windows', 'macOS', 'iOS', 'Android', 'Linux', 'Other') then
        raise exception 'Invalid fixed site access metadata.';
    end if;

    perform pg_advisory_xact_lock(
        hashtextextended(p_actor_user_id::text || ':' || p_browser_client_id::text, 0));
    observed_at_utc := clock_timestamp();

    select access.id
    into active_session_id
    from site_access_sessions access
    where access.actor_user_id = p_actor_user_id
      and access.browser_client_id = p_browser_client_id
      and access.ended_at_utc is null
      and access.last_activity_at_utc > observed_at_utc - interval '30 minutes'
    order by access.last_activity_at_utc desc, access.id desc
    limit 1
    for update;

    if active_session_id is not null then
        update site_access_sessions access
        set last_activity_at_utc = observed_at_utc,
            menu_codes = case
                when p_menu_code = any(access.menu_codes) then access.menu_codes
                else array_append(access.menu_codes, p_menu_code)
            end
        where access.id = active_session_id;

        return query
        select access.id, access.idempotency_receipt, access.started_at_utc,
               access.last_activity_at_utc, false
        from site_access_sessions access
        where access.id = active_session_id;
        return;
    end if;

    select snapshot.display_name, snapshot.department_name
    into actor_name, actor_department
    from qms_audit_identity_snapshot(p_actor_user_id) snapshot;

    active_session_id := uuid_generate_v4();
    insert into site_access_sessions (
        id, actor_user_id, actor_display_name, actor_department_name,
        browser_client_id, idempotency_receipt, started_at_utc, last_activity_at_utc,
        client_ip, browser_family, os_family, app_access_outcome, menu_codes)
    values (
        active_session_id, p_actor_user_id, actor_name, actor_department,
        p_browser_client_id, uuid_generate_v4(), observed_at_utc, observed_at_utc,
        p_client_ip, p_browser_family, p_os_family, p_app_access_outcome, array[p_menu_code]);

    return query
    select access.id, access.idempotency_receipt, access.started_at_utc,
           access.last_activity_at_utc, true
    from site_access_sessions access
    where access.id = active_session_id;
end;
$$;

create or replace function qms_end_site_access(
    p_actor_user_id uuid,
    p_session_id uuid,
    p_idempotency_receipt uuid)
returns boolean
language plpgsql
security definer
set search_path = public, pg_temp
as $$
declare
    observed_at_utc timestamptz;
    current_end timestamptz;
begin
    perform pg_advisory_xact_lock(
        hashtextextended(p_actor_user_id::text || ':' || p_session_id::text, 0));
    observed_at_utc := clock_timestamp();

    select access.ended_at_utc
    into current_end
    from site_access_sessions access
    where access.id = p_session_id
      and access.actor_user_id = p_actor_user_id
      and access.idempotency_receipt = p_idempotency_receipt
    for update;

    if not found then
        return false;
    end if;

    if current_end is not null then
        return true;
    end if;

    update site_access_sessions
    set ended_at_utc = greatest(observed_at_utc, last_activity_at_utc),
        end_reason = 'ExplicitLogout'
    where id = p_session_id;

    return true;
end;
$$;
