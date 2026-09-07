create table qms_database_identity (
    singleton boolean primary key default true check (singleton),
    database_kind text not null check (database_kind = 'directory'),
    business_unit_code text null check (business_unit_code is null),
    schema_contract text not null check (schema_contract = '0001_business_unit_directory'),
    bound_at_utc timestamptz not null default now()
);

insert into qms_database_identity (
    singleton,
    database_kind,
    business_unit_code,
    schema_contract
)
values (true, 'directory', null, '0001_business_unit_directory');

create table directory_business_units (
    code text primary key check (code in ('CHEONGJU', 'OSAN')),
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now()
);

insert into directory_business_units (code)
values ('CHEONGJU'), ('OSAN');

create table directory_identities (
    user_id uuid primary key,
    auth_provider text not null check (auth_provider in ('EntraId', 'Dev')),
    external_subject text not null check (length(btrim(external_subject)) between 1 and 256),
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    unique (auth_provider, external_subject)
);

create table directory_business_unit_memberships (
    user_id uuid not null references directory_identities(user_id) on delete restrict,
    business_unit_code text not null references directory_business_units(code) on delete restrict,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    primary key (user_id, business_unit_code)
);

create table directory_overall_administrators (
    user_id uuid primary key references directory_identities(user_id) on delete restrict,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now()
);

create table directory_membership_audit_events (
    id uuid primary key,
    user_id uuid not null,
    business_unit_code text null,
    action text not null check (action in ('MembershipBackfilled', 'OverallAdministratorDesignated')),
    actor_kind text not null check (actor_kind = 'ApprovedBootstrap'),
    occurred_at_utc timestamptz not null default now()
);

create or replace function prevent_directory_audit_mutation()
returns trigger
language plpgsql
as $$
begin
    raise exception using
        errcode = '42501',
        message = 'directory_membership_audit_is_append_only';
end;
$$;

create trigger trg_prevent_directory_audit_update
before update or delete on directory_membership_audit_events
for each row execute function prevent_directory_audit_mutation();

revoke all privileges on all tables in schema public from public;
revoke execute on all functions in schema public from public;
