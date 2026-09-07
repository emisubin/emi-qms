create table if not exists qms_database_identity (
    singleton boolean primary key default true check (singleton),
    database_kind text not null check (database_kind = 'business'),
    business_unit_code text not null check (business_unit_code in ('CHEONGJU', 'OSAN')),
    schema_contract text not null,
    bound_at_utc timestamptz not null default now(),
    check (schema_contract = '0086_business_unit_database_identity')
);

revoke insert, update, delete, truncate, references, trigger
    on table qms_database_identity from public;

create or replace function prevent_osan_external_notification_delivery()
returns trigger
language plpgsql
as $$
begin
    if exists (
        select 1
        from qms_database_identity identity_row
        where identity_row.singleton = true
          and identity_row.business_unit_code = 'OSAN'
    ) then
        raise exception using
            errcode = '42501',
            message = 'external_notification_delivery_disabled_for_business_unit';
    end if;

    return new;
end;
$$;

drop trigger if exists trg_prevent_osan_external_notification_delivery on notification_deliveries;
create trigger trg_prevent_osan_external_notification_delivery
before insert on notification_deliveries
for each row execute function prevent_osan_external_notification_delivery();
