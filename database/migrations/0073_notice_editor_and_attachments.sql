alter table notice_posts
    add column body_format text not null default 'PlainTextV1',
    add column version integer not null default 1,
    add column updated_at_utc timestamptz null,
    add column updated_by_user_id uuid null references qms_users(id) on delete restrict,
    add constraint ck_notice_posts_body_format check (body_format in ('PlainTextV1', 'BoldMarkupV1')),
    add constraint ck_notice_posts_version check (version >= 1),
    add constraint ck_notice_posts_updated_pair check (
        (updated_at_utc is null and updated_by_user_id is null)
        or (updated_at_utc is not null and updated_by_user_id is not null)
    );

create table notice_post_revisions (
    id uuid primary key default uuid_generate_v4(),
    notice_post_id uuid not null references notice_posts(id) on delete restrict,
    version integer not null,
    title text not null,
    body text not null,
    body_format text not null,
    changed_by_user_id uuid not null references qms_users(id) on delete restrict,
    changed_at_utc timestamptz not null default now(),
    constraint uq_notice_post_revisions_version unique (notice_post_id, version),
    constraint ck_notice_post_revisions_version check (version >= 1),
    constraint ck_notice_post_revisions_title check (btrim(title) <> '' and char_length(title) <= 100),
    constraint ck_notice_post_revisions_body check (btrim(body) <> '' and char_length(body) <= 2000),
    constraint ck_notice_post_revisions_body_format check (body_format in ('PlainTextV1', 'BoldMarkupV1'))
);

create index ix_notice_post_revisions_notice
    on notice_post_revisions(notice_post_id, version desc);

create table notice_attachments (
    id uuid primary key default uuid_generate_v4(),
    notice_post_id uuid not null references notice_posts(id) on delete restrict,
    original_file_name text not null,
    normalized_mime text not null,
    byte_size integer not null,
    sha256 text not null,
    content bytea not null,
    created_by_user_id uuid not null references qms_users(id) on delete restrict,
    created_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null,
    deleted_by_user_id uuid null references qms_users(id) on delete restrict,
    constraint ck_notice_attachments_file_name check (
        char_length(btrim(original_file_name)) between 1 and 180
    ),
    constraint ck_notice_attachments_mime check (normalized_mime in (
        'application/pdf',
        'image/jpeg',
        'image/png',
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'application/vnd.openxmlformats-officedocument.presentationml.presentation'
    )),
    constraint ck_notice_attachments_size check (
        byte_size between 1 and 10485760 and octet_length(content) = byte_size
    ),
    constraint ck_notice_attachments_sha check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint ck_notice_attachments_deleted_pair check (
        (deleted_at_utc is null and deleted_by_user_id is null)
        or (deleted_at_utc is not null and deleted_by_user_id is not null)
    )
);

create index ix_notice_attachments_active
    on notice_attachments(notice_post_id, created_at_utc, id)
    where deleted_at_utc is null;

create or replace function guard_notice_post_revision_append_only()
returns trigger
language plpgsql
as $$
begin
    raise exception 'Notice post revisions are append-only.' using errcode = 'P0001';
end;
$$;

create trigger trg_guard_notice_post_revision_append_only
before update or delete on notice_post_revisions
for each row execute function guard_notice_post_revision_append_only();

create or replace function guard_notice_attachment_evidence()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'DELETE' then
        raise exception 'Notice attachments use soft delete.' using errcode = 'P0001';
    end if;

    if (
        new.id, new.notice_post_id, new.original_file_name, new.normalized_mime,
        new.byte_size, new.sha256, new.content, new.created_by_user_id, new.created_at_utc
    ) is not distinct from (
        old.id, old.notice_post_id, old.original_file_name, old.normalized_mime,
        old.byte_size, old.sha256, old.content, old.created_by_user_id, old.created_at_utc
    ) and old.deleted_at_utc is null
      and old.deleted_by_user_id is null
      and new.deleted_at_utc is not null
      and new.deleted_by_user_id is not null then
        return new;
    end if;

    raise exception 'Notice attachment evidence is immutable.' using errcode = 'P0001';
end;
$$;

create trigger trg_guard_notice_attachment_evidence
before update or delete on notice_attachments
for each row execute function guard_notice_attachment_evidence();
