create table if not exists user_profile_photos (
    user_id uuid primary key references qms_users(id) on delete cascade,
    normalized_mime text not null,
    byte_size integer not null,
    content_hash text not null,
    version bigint not null default 1,
    content bytea not null,
    updated_by_profile_user_id uuid not null references qms_users(id) on delete cascade,
    updated_at_utc timestamptz not null default now(),
    constraint ck_user_profile_photos_mime check (normalized_mime in ('image/jpeg', 'image/png')),
    constraint ck_user_profile_photos_size check (
        byte_size = octet_length(content)
        and byte_size between 1 and 5242880
    ),
    constraint ck_user_profile_photos_hash check (content_hash ~ '^[0-9a-f]{64}$'),
    constraint ck_user_profile_photos_version check (version >= 1),
    constraint ck_user_profile_photos_self_actor check (updated_by_profile_user_id = user_id)
);

create table if not exists user_profile_photo_audit_events (
    id uuid primary key default uuid_generate_v4(),
    profile_user_id uuid not null references qms_users(id) on delete cascade,
    actor_profile_user_id uuid not null references qms_users(id) on delete cascade,
    action text not null,
    content_hash text null,
    byte_size integer null,
    normalized_mime text null,
    occurred_at_utc timestamptz not null default now(),
    constraint ck_user_profile_photo_audit_action check (action in ('Upload', 'Replace', 'Remove')),
    constraint ck_user_profile_photo_audit_self_actor check (actor_profile_user_id = profile_user_id),
    constraint ck_user_profile_photo_audit_hash check (
        content_hash is null or content_hash ~ '^[0-9a-f]{64}$'
    ),
    constraint ck_user_profile_photo_audit_size check (
        byte_size is null or byte_size between 1 and 5242880
    ),
    constraint ck_user_profile_photo_audit_mime check (
        normalized_mime is null or normalized_mime in ('image/jpeg', 'image/png')
    )
);

create index if not exists ix_user_profile_photo_audit_profile_time
    on user_profile_photo_audit_events(profile_user_id, occurred_at_utc desc);

create or replace function guard_append_only_user_profile_photo_audit()
returns trigger language plpgsql as $$
begin
    if current_setting('emi_qms.user_profile_purge', true) = 'on' and tg_op = 'DELETE' then
        return old;
    end if;
    raise exception 'User profile photo audit events are append-only.';
end $$;

drop trigger if exists trg_guard_user_profile_photo_audit on user_profile_photo_audit_events;
create trigger trg_guard_user_profile_photo_audit
before update or delete on user_profile_photo_audit_events
for each row execute function guard_append_only_user_profile_photo_audit();
