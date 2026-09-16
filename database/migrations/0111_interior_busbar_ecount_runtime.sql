create table busbar_ecount_runtime(
 singleton boolean primary key default true check(singleton),
 company_code text null,
 environment text null check(environment in ('Test','Production')),
 paused boolean not null default false,
 message text null,
 last_login_at_utc timestamptz null,
 next_send_at_utc timestamptz null,
 consecutive_failures integer not null default 0 check(consecutive_failures>=0)
);
insert into busbar_ecount_runtime(singleton) values(true);

alter table busbar_ecount_jobs add column reviewed_payload jsonb null, add column reviewed_shipped integer null;
