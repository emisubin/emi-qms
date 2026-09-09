alter table directory_identities
    add column access_version bigint not null default 0
        check (access_version >= 0);

alter table directory_membership_audit_events
    drop constraint directory_membership_audit_events_action_check;

alter table directory_membership_audit_events
    add constraint directory_membership_audit_events_action_check
    check (action in (
        'MembershipBackfilled',
        'OverallAdministratorDesignated',
        'MembershipsUpdated',
        'AccessRevoked',
        'AccessPublished'
    ));

alter table directory_membership_audit_events
    add column correlation_id uuid null;

create table directory_user_access_operations (
    operation_id uuid primary key,
    target_user_id uuid not null references directory_identities(user_id) on delete restrict,
    actor_user_id uuid not null references directory_identities(user_id) on delete restrict,
    expected_version bigint not null check (expected_version >= 0),
    request_hash text not null check (length(request_hash) = 64),
    requested_profiles jsonb not null check (jsonb_typeof(requested_profiles) = 'array'),
    requested_memberships text[] not null,
    memberships_before text[] not null,
    status text not null check (status in ('Preparing', 'RetryRequired', 'Completed')),
    failure_code text null check (failure_code is null or length(failure_code) between 1 and 100),
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    completed_at_utc timestamptz null,
    check ((status = 'RetryRequired') = (failure_code is not null)),
    check ((status = 'Completed') = (completed_at_utc is not null))
);

create unique index ux_directory_user_access_operations_active_target
    on directory_user_access_operations(target_user_id)
    where status in ('Preparing', 'RetryRequired');

create index ix_directory_user_access_operations_target_created
    on directory_user_access_operations(target_user_id, created_at_utc desc);

create or replace function begin_directory_user_access_operation(
    p_operation_id uuid,
    p_target_user_id uuid,
    p_requested_memberships text[],
    p_actor_user_id uuid,
    p_expected_version bigint,
    p_request_hash text,
    p_requested_profiles jsonb)
returns table (operation_status text, current_version bigint)
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare
    normalized_memberships text[];
    before_memberships text[];
    existing_operation directory_user_access_operations%rowtype;
    target_is_overall boolean;
    target_version bigint;
    revoked boolean := false;
begin
    if p_operation_id is null
        or p_target_user_id is null
        or p_actor_user_id is null
        or p_expected_version is null
        or p_expected_version < 0
        or p_request_hash is null
        or length(p_request_hash) <> 64
        or p_requested_profiles is null
        or jsonb_typeof(p_requested_profiles) <> 'array' then
        raise exception using errcode = '22023', message = 'user_access_request_invalid';
    end if;

    select coalesce(array_agg(code order by code), array[]::text[])
    into normalized_memberships
    from (
        select distinct upper(btrim(input_code)) as code
        from unnest(coalesce(p_requested_memberships, array[]::text[])) input_code
    ) normalized;

    if exists (
        select 1
        from unnest(normalized_memberships) requested_code
        left join public.directory_business_units business_unit
          on business_unit.code = requested_code
         and business_unit.is_active = true
        where business_unit.code is null
    ) then
        raise exception using errcode = '22023', message = 'business_unit_unknown';
    end if;

    perform 1
    from public.directory_identities locked_identity
    where locked_identity.user_id in (p_actor_user_id, p_target_user_id)
    order by locked_identity.user_id
    for update;

    perform 1
    from public.directory_identities actor_identity
    join public.directory_overall_administrators administrator
      on administrator.user_id = actor_identity.user_id
     and administrator.is_active = true
    where actor_identity.user_id = p_actor_user_id
      and actor_identity.is_active = true
    for update of administrator;

    if not found then
        raise exception using errcode = '42501', message = 'overall_administrator_required';
    end if;

    select target_identity.access_version,
           exists (
               select 1
               from public.directory_overall_administrators administrator
               where administrator.user_id = target_identity.user_id
                 and administrator.is_active = true)
    into target_version, target_is_overall
    from public.directory_identities target_identity
    where target_identity.user_id = p_target_user_id
      and target_identity.is_active = true;

    if not found then
        raise exception using errcode = 'P0002', message = 'directory_identity_not_found';
    end if;

    select operation_row.*
    into existing_operation
    from public.directory_user_access_operations operation_row
    where operation_row.operation_id = p_operation_id
    for update;

    if found then
        if existing_operation.target_user_id <> p_target_user_id
            or existing_operation.actor_user_id <> p_actor_user_id
            or existing_operation.expected_version <> p_expected_version
            or existing_operation.request_hash <> p_request_hash
            or existing_operation.requested_profiles <> p_requested_profiles
            or existing_operation.requested_memberships <> normalized_memberships then
            raise exception using errcode = '22023', message = 'user_access_idempotency_mismatch';
        end if;

        if existing_operation.status = 'Completed' then
            return query select 'Completed'::text, target_version;
            return;
        end if;

        update public.directory_user_access_operations operation_row
        set status = 'Preparing',
            failure_code = null,
            updated_at_utc = now()
        where operation_row.operation_id = p_operation_id;

        return query select 'Preparing'::text, target_version;
        return;
    end if;

    if exists (
        select 1
        from public.directory_user_access_operations operation_row
        where operation_row.target_user_id = p_target_user_id
          and operation_row.status in ('Preparing', 'RetryRequired')
    ) then
        raise exception using errcode = '40001', message = 'user_access_operation_in_progress';
    end if;

    if target_version <> p_expected_version then
        raise exception using errcode = '40001', message = 'user_access_version_conflict';
    end if;

    if cardinality(normalized_memberships) > 1 and not target_is_overall then
        raise exception using errcode = '23514', message = 'ordinary_user_multiple_memberships_forbidden';
    end if;

    select coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code), array[]::text[])
    into before_memberships
    from public.directory_business_unit_memberships membership
    join public.directory_business_units business_unit
      on business_unit.code = membership.business_unit_code
     and business_unit.is_active = true
    where membership.user_id = p_target_user_id
      and membership.is_active = true;

    insert into public.directory_user_access_operations (
        operation_id,
        target_user_id,
        actor_user_id,
        expected_version,
        request_hash,
        requested_profiles,
        requested_memberships,
        memberships_before,
        status)
    values (
        p_operation_id,
        p_target_user_id,
        p_actor_user_id,
        p_expected_version,
        p_request_hash,
        p_requested_profiles,
        normalized_memberships,
        before_memberships,
        'Preparing');

    update public.directory_business_unit_memberships membership
    set is_active = false,
        updated_at_utc = now()
    where membership.user_id = p_target_user_id
      and membership.is_active = true
      and not (membership.business_unit_code = any(normalized_memberships));
    revoked := found;

    if revoked then
        insert into public.directory_membership_audit_events (
            id,
            user_id,
            business_unit_code,
            action,
            actor_kind,
            actor_user_id,
            memberships_before,
            memberships_after,
            correlation_id)
        values (
            p_operation_id,
            p_target_user_id,
            null,
            'AccessRevoked',
            'OverallAdministrator',
            p_actor_user_id,
            before_memberships,
            array(
                select membership_code
                from unnest(before_memberships) membership_code
                where membership_code = any(normalized_memberships)
                order by membership_code),
            p_operation_id);
    end if;

    return query select 'Preparing'::text, target_version;
end;
$$;

create or replace function mark_directory_user_access_retry_required(
    p_operation_id uuid,
    p_actor_user_id uuid,
    p_failure_code text)
returns void
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
begin
    if p_operation_id is null
        or p_actor_user_id is null
        or p_failure_code is null
        or length(p_failure_code) not between 1 and 100 then
        raise exception using errcode = '22023', message = 'user_access_retry_state_invalid';
    end if;

    update public.directory_user_access_operations operation_row
    set status = 'RetryRequired',
        failure_code = p_failure_code,
        updated_at_utc = now()
    where operation_row.operation_id = p_operation_id
      and operation_row.actor_user_id = p_actor_user_id
      and operation_row.status = 'Preparing';

    if not found then
        raise exception using errcode = 'P0002', message = 'user_access_operation_not_found';
    end if;
end;
$$;

create or replace function publish_directory_user_access_operation(
    p_operation_id uuid,
    p_actor_user_id uuid)
returns table (changed boolean, current_version bigint)
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare
    operation_row directory_user_access_operations%rowtype;
    memberships_before_publish text[];
    memberships_after_publish text[];
    version_after bigint;
begin
    select stored_operation.*
    into operation_row
    from public.directory_user_access_operations stored_operation
    where stored_operation.operation_id = p_operation_id
    for update;

    if not found or operation_row.actor_user_id <> p_actor_user_id then
        raise exception using errcode = 'P0002', message = 'user_access_operation_not_found';
    end if;

    select identity_row.access_version
    into version_after
    from public.directory_identities identity_row
    where identity_row.user_id = operation_row.target_user_id
    for update;

    if operation_row.status = 'Completed' then
        return query select false, version_after;
        return;
    end if;

    if operation_row.status <> 'Preparing' then
        raise exception using errcode = '55000', message = 'user_access_retry_required';
    end if;

    select coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code), array[]::text[])
    into memberships_before_publish
    from public.directory_business_unit_memberships membership
    where membership.user_id = operation_row.target_user_id
      and membership.is_active = true;

    insert into public.directory_business_unit_memberships (
        user_id,
        business_unit_code,
        is_active)
    select operation_row.target_user_id, requested_code, true
    from unnest(operation_row.requested_memberships) requested_code
    on conflict (user_id, business_unit_code) do update
    set is_active = true,
        updated_at_utc = now()
    where directory_business_unit_memberships.is_active = false;

    select coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code), array[]::text[])
    into memberships_after_publish
    from public.directory_business_unit_memberships membership
    where membership.user_id = operation_row.target_user_id
      and membership.is_active = true;

    update public.directory_identities identity_row
    set access_version = identity_row.access_version + 1,
        updated_at_utc = now()
    where identity_row.user_id = operation_row.target_user_id
    returning identity_row.access_version into version_after;

    update public.directory_user_access_operations stored_operation
    set status = 'Completed',
        failure_code = null,
        updated_at_utc = now(),
        completed_at_utc = now()
    where stored_operation.operation_id = p_operation_id;

    insert into public.directory_membership_audit_events (
        id,
        user_id,
        business_unit_code,
        action,
        actor_kind,
        actor_user_id,
        memberships_before,
        memberships_after,
        correlation_id)
    values (
        md5(p_operation_id::text || ':publish')::uuid,
        operation_row.target_user_id,
        null,
        'AccessPublished',
        'OverallAdministrator',
        p_actor_user_id,
        operation_row.memberships_before,
        memberships_after_publish,
        p_operation_id);

    return query select true, version_after;
end;
$$;

create or replace function set_directory_business_unit_memberships(
    p_event_id uuid,
    p_target_user_id uuid,
    p_business_unit_codes text[],
    p_actor_user_id uuid)
returns boolean
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
begin
    raise exception using errcode = '55000', message = 'integrated_user_access_required';
end;
$$;

revoke all on function begin_directory_user_access_operation(uuid, uuid, text[], uuid, bigint, text, jsonb) from public;
revoke all on function mark_directory_user_access_retry_required(uuid, uuid, text) from public;
revoke all on function publish_directory_user_access_operation(uuid, uuid) from public;
revoke all on function set_directory_business_unit_memberships(uuid, uuid, text[], uuid) from public;
