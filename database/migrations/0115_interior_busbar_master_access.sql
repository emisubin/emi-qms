create table busbar_master_access (
 user_id uuid primary key references qms_users(id),
 can_edit boolean not null default false,
 updated_by uuid not null references qms_users(id),
 updated_at_utc timestamptz not null default now()
);
