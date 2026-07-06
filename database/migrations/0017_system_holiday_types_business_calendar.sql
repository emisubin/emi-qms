alter table system_holidays
    add column if not exists holiday_type text not null default 'National';

update system_holidays
set holiday_type = case
    when source ilike '%company%' then 'Company'
    when name like '%대체%' or source ilike '%substitute%' then 'Substitute'
    when name like '%임시%' or source ilike '%temporary%' then 'Temporary'
    else 'National'
end
where holiday_type = 'National';

do $$
begin
    alter table system_holidays drop constraint if exists ck_system_holidays_holiday_type;

    alter table system_holidays add constraint ck_system_holidays_holiday_type
        check (holiday_type in ('National', 'Substitute', 'Temporary', 'Company'));
exception
    when duplicate_object then null;
end $$;

create index if not exists ix_system_holidays_active_type_lookup
    on system_holidays(country_code, holiday_date, holiday_type)
    where is_active = true;
