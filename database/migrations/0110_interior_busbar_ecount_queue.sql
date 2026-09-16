-- No historical projects are backfilled into the outbound queue.
create table busbar_ecount_jobs(
 id uuid primary key,
 project_id uuid not null references busbar_projects,
 kind text not null check(kind in ('Order','Sale')),
 state text not null default 'Pending' check(state in ('Pending','Held','InFlight','Succeeded','Failed','Unknown')),
 needs_review boolean not null default false,
 current_attempt_id uuid null,
 message text null,
 slip_number text null,
 created_at_utc timestamptz not null default now(),
 updated_at_utc timestamptz not null default now(),
 unique(project_id,kind)
);
create table busbar_ecount_attempts(
 id uuid primary key,
 job_id uuid not null references busbar_ecount_jobs,
 payload jsonb not null,
 state text not null check(state in ('InFlight','Succeeded','Failed','Unknown')),
 slip_number text null,
 started_at_utc timestamptz not null,
 finished_at_utc timestamptz null
);
alter table busbar_ecount_jobs add constraint busbar_ecount_current_attempt_fk foreign key(current_attempt_id) references busbar_ecount_attempts;
create index ix_busbar_ecount_pending on busbar_ecount_jobs(created_at_utc) where state='Pending' and not needs_review;
