-- Persist the rollout boundary once; subsequent reads must not move it.
create table g2_quality_settings(singleton boolean primary key default true check(singleton), activated_on date not null);
insert into g2_quality_settings(singleton,activated_on) values(true,(now() at time zone 'Asia/Seoul')::date);
-- Current information and expiring modification/deletion snapshots belong only to Cheongju.
create table g2_nonconformances (
 id uuid primary key,
 data jsonb not null,
 updated_by uuid not null references qms_users(id),
 updated_at timestamptz not null default now()
);
create unique index g2_nonconformances_serial on g2_nonconformances (upper(btrim(data->>'serial')));
create table g2_nonconformance_history (
 id bigint generated always as identity primary key,
 case_id uuid not null,
 snapshot jsonb not null,
 changed_by uuid not null references qms_users(id),
 changed_at timestamptz not null default now(),
 expires_at timestamptz not null
);
create index on g2_nonconformance_history(expires_at);
-- Legacy quantities are preserved. Individual events are additive, including selected past dates.
create view g2_projected_metrics as
 select id,work_date,metric_code,quantity,is_forecast,version,updated_at_utc,updated_by_user_id from g2_daily_metrics
 union all
 select id,(data->>'occurrenceDate')::date,'Defect',1,false,(data->>'version')::int,updated_at,updated_by
 from g2_nonconformances where not (data->>'initialStock')::boolean
 union all
 select id,(data->'action'->>'completionDate')::date,'AfternoonRepair',1,false,(data->>'version')::int,updated_at,updated_by
 from g2_nonconformances where data->'action' is not null and data->'action'<>'null'::jsonb;
