alter table directory_membership_audit_events
    drop constraint directory_membership_audit_events_action_check;

alter table directory_membership_audit_events
    add constraint directory_membership_audit_events_action_check
    check (action in (
        'MembershipBackfilled',
        'OverallAdministratorDesignated',
        'MembershipsUpdated'
    ));

alter table directory_membership_audit_events
    drop constraint directory_membership_audit_events_actor_kind_check;

alter table directory_membership_audit_events
    add constraint directory_membership_audit_events_actor_kind_check
    check (actor_kind in ('ApprovedBootstrap', 'OverallAdministrator'));

alter table directory_membership_audit_events
    add column actor_user_id uuid null,
    add column memberships_before text[] null,
    add column memberships_after text[] null;

alter table directory_identities
    add column display_name text null,
    add column email text null;

alter table directory_identities
    add constraint directory_identities_display_name_check
        check (display_name is null or length(btrim(display_name)) between 1 and 200),
    add constraint directory_identities_email_check
        check (email is null or length(btrim(email)) between 3 and 320);

alter table directory_membership_audit_events
    add constraint directory_membership_audit_events_actor_contract_check
    check (
        (actor_kind = 'ApprovedBootstrap'
            and actor_user_id is null
            and memberships_before is null
            and memberships_after is null)
        or
        (actor_kind = 'OverallAdministrator'
            and actor_user_id is not null
            and memberships_before is not null
            and memberships_after is not null)
    );

create or replace function register_or_update_pending_entra_directory_identity(
    p_proposed_user_id uuid,
    p_external_subject text,
    p_display_name text,
    p_email text)
returns uuid
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare
    existing_user_id uuid;
    normalized_external_subject text := btrim(p_external_subject);
    normalized_display_name text := coalesce(nullif(btrim(p_display_name), ''), 'Microsoft 365 사용자');
    normalized_email text := nullif(lower(btrim(p_email)), '');
begin
    if p_proposed_user_id is null
        or normalized_external_subject is null
        or length(normalized_external_subject) not between 1 and 256
        or length(normalized_display_name) not between 1 and 200
        or (normalized_email is not null and length(normalized_email) not between 3 and 320) then
        raise exception using errcode = '22023', message = 'directory_identity_registration_invalid';
    end if;

    perform pg_catalog.pg_advisory_xact_lock(
        pg_catalog.hashtextextended('EntraId:' || normalized_external_subject, 0));

    select identity_row.user_id
    into existing_user_id
    from public.directory_identities identity_row
    where identity_row.auth_provider = 'EntraId'
      and identity_row.external_subject = normalized_external_subject
    for update;

    if found then
        update public.directory_identities identity_row
        set display_name = normalized_display_name,
            email = normalized_email,
            updated_at_utc = now()
        where identity_row.user_id = existing_user_id
          and identity_row.is_active = true;
        return existing_user_id;
    end if;

    insert into public.directory_identities (
        user_id,
        auth_provider,
        external_subject,
        display_name,
        email,
        is_active)
    values (
        p_proposed_user_id,
        'EntraId',
        normalized_external_subject,
        normalized_display_name,
        normalized_email,
        true);

    return p_proposed_user_id;
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
declare
    before_memberships text[];
    after_memberships text[];
    normalized_memberships text[];
begin
    if p_event_id is null or p_target_user_id is null or p_actor_user_id is null then
        raise exception using errcode = '22023', message = 'directory_membership_request_invalid';
    end if;

    -- Membership mutations may target the actor or another overall administrator.
    -- Lock both directory identities at the strongest required level and in one
    -- deterministic order before validating either role. This avoids lock upgrades
    -- for self-edits and opposite actor/target ordering for cross-edits.
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

    perform 1
    from public.directory_identities target_identity
    where target_identity.user_id = p_target_user_id
      and target_identity.is_active = true;

    if not found then
        raise exception using errcode = 'P0002', message = 'directory_identity_not_found';
    end if;

    select coalesce(array_agg(code order by code), array[]::text[])
    into normalized_memberships
    from (
        select distinct upper(btrim(input_code)) as code
        from unnest(coalesce(p_business_unit_codes, array[]::text[])) input_code
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

    select coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code), array[]::text[])
    into before_memberships
    from public.directory_business_unit_memberships membership
    join public.directory_business_units business_unit
      on business_unit.code = membership.business_unit_code
     and business_unit.is_active = true
    where membership.user_id = p_target_user_id
      and membership.is_active = true;

    if before_memberships = normalized_memberships then
        return false;
    end if;

    update public.directory_business_unit_memberships membership
    set is_active = false,
        updated_at_utc = now()
    where membership.user_id = p_target_user_id
      and membership.is_active = true
      and not (membership.business_unit_code = any(normalized_memberships));

    insert into public.directory_business_unit_memberships (
        user_id,
        business_unit_code,
        is_active
    )
    select p_target_user_id, requested_code, true
    from unnest(normalized_memberships) requested_code
    on conflict (user_id, business_unit_code) do update
    set is_active = true,
        updated_at_utc = now()
    where directory_business_unit_memberships.is_active = false;

    select coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code), array[]::text[])
    into after_memberships
    from public.directory_business_unit_memberships membership
    join public.directory_business_units business_unit
      on business_unit.code = membership.business_unit_code
     and business_unit.is_active = true
    where membership.user_id = p_target_user_id
      and membership.is_active = true;

    insert into public.directory_membership_audit_events (
        id,
        user_id,
        business_unit_code,
        action,
        actor_kind,
        actor_user_id,
        memberships_before,
        memberships_after
    )
    values (
        p_event_id,
        p_target_user_id,
        null,
        'MembershipsUpdated',
        'OverallAdministrator',
        p_actor_user_id,
        before_memberships,
        after_memberships
    );

    return true;
end;
$$;

revoke all on function set_directory_business_unit_memberships(uuid, uuid, text[], uuid) from public;
revoke all on function register_or_update_pending_entra_directory_identity(uuid, text, text, text) from public;
