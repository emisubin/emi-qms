alter table system_holidays
    add column if not exists note text null;

create index if not exists ix_system_holidays_year_type_lookup
    on system_holidays(country_code, (extract(year from holiday_date)), holiday_type);
