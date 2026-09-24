create table deployment_maintenance (
    id smallint primary key check(id=1),
    release_id uuid,
    version integer not null default 0 check(version>=0),
    state text not null default 'Idle' check(state in ('Idle','Announced','Active','Delayed','Failed','Completed')),
    title text,
    body text,
    starts_at_utc timestamptz,
    expected_ends_at_utc timestamptz,
    notice_id uuid references notice_posts(id),
    updated_at_utc timestamptz not null default now(),
    check(state='Idle' or (release_id is not null and title is not null and body is not null
        and starts_at_utc is not null and expected_ends_at_utc is not null))
);
insert into deployment_maintenance(id) values(1);

create table deployment_maintenance_popup_receipts (
    release_id uuid not null,
    version integer not null,
    user_id uuid not null references qms_users(id) on delete cascade,
    shown_at_utc timestamptz not null default now(),
    primary key(release_id,version,user_id)
);
