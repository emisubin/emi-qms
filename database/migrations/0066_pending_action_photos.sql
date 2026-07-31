create table pending_action_photos (
    id uuid primary key default uuid_generate_v4(),
    pending_issue_id uuid not null references pending_issues(id) on delete cascade,
    display_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    alt_text text not null,
    content bytea not null,
    status text not null default 'Draft',
    action_round integer null,
    action_reason_snapshot text null,
    created_by_user_id uuid not null references qms_users(id),
    created_at_utc timestamptz not null default now(),
    confirmed_by_user_id uuid null references qms_users(id),
    confirmed_at_utc timestamptz null,
    constraint ck_pending_action_photos_name check (display_name ~ '^photo-[1-5]\.(jpg|png)$'),
    constraint ck_pending_action_photos_mime check (normalized_mime in ('image/jpeg', 'image/png')),
    constraint ck_pending_action_photos_size check (
        byte_size between 1 and 5242880 and octet_length(content) = byte_size
    ),
    constraint ck_pending_action_photos_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_pending_action_photos_alt check (char_length(btrim(alt_text)) between 1 and 200),
    constraint ck_pending_action_photos_status check (status in ('Draft', 'Confirmed')),
    constraint ck_pending_action_photos_confirmation check (
        (status = 'Draft'
            and action_round is null
            and action_reason_snapshot is null
            and confirmed_by_user_id is null
            and confirmed_at_utc is null)
        or
        (status = 'Confirmed'
            and action_round >= 1
            and char_length(btrim(action_reason_snapshot)) between 3 and 500
            and confirmed_by_user_id is not null
            and confirmed_at_utc is not null)
    ),
    constraint uq_pending_action_photos_content unique (pending_issue_id, sha256)
);

create index ix_pending_action_photos_pending
    on pending_action_photos(pending_issue_id, status, action_round, created_at_utc, id);

create table pending_photo_operations (
    operation_id uuid primary key,
    pending_issue_id uuid not null references pending_issues(id) on delete cascade,
    action text not null,
    requested_by_user_id uuid not null references qms_users(id),
    payload_fingerprint text not null,
    result_projection jsonb not null,
    created_at_utc timestamptz not null default now(),
    constraint ck_pending_photo_operations_action check (action in ('AddPhoto', 'RemovePhoto')),
    constraint ck_pending_photo_operations_fingerprint check (payload_fingerprint ~ '^[a-f0-9]{64}$'),
    constraint ck_pending_photo_operations_projection check (jsonb_typeof(result_projection) = 'object')
);

create index ix_pending_photo_operations_pending
    on pending_photo_operations(pending_issue_id, created_at_utc desc);

create or replace function guard_pending_action_photo_evidence()
returns trigger
language plpgsql
as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;

    if tg_op = 'DELETE' then
        if old.status = 'Confirmed' then
            raise exception 'Confirmed Pending action photos are immutable.' using errcode = 'P0001';
        end if;
        return old;
    end if;

    if old.status = 'Draft'
       and new.status = 'Confirmed'
       and (
           new.id, new.pending_issue_id, new.display_name, new.normalized_mime,
           new.byte_size, new.sha256, new.alt_text, new.content,
           new.created_by_user_id, new.created_at_utc
       ) is not distinct from (
           old.id, old.pending_issue_id, old.display_name, old.normalized_mime,
           old.byte_size, old.sha256, old.alt_text, old.content,
           old.created_by_user_id, old.created_at_utc
       )
       and new.action_round >= 1
       and new.action_reason_snapshot is not null
       and new.confirmed_by_user_id is not null
       and new.confirmed_at_utc is not null then
        return new;
    end if;

    raise exception 'Pending action photo evidence is immutable.' using errcode = 'P0001';
end;
$$;

create trigger trg_guard_pending_action_photo_evidence
before update or delete on pending_action_photos
for each row execute function guard_pending_action_photo_evidence();

create or replace function guard_pending_photo_operation_append_only()
returns trigger
language plpgsql
as $$
begin
    if current_setting('emi_qms.project_purge', true) = 'on' then
        return case when tg_op = 'DELETE' then old else new end;
    end if;
    raise exception 'Pending photo operation receipts are append-only.' using errcode = 'P0001';
end;
$$;

create trigger trg_guard_pending_photo_operation_append_only
before update or delete on pending_photo_operations
for each row execute function guard_pending_photo_operation_append_only();
