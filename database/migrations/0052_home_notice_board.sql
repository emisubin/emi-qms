create table if not exists notice_posts (
    id uuid primary key default uuid_generate_v4(),
    title text not null,
    body text not null,
    author_user_id uuid not null references qms_users(id) on delete restrict,
    author_display_name_snapshot text not null,
    author_department_name_snapshot text null,
    request_id uuid not null,
    created_at_utc timestamptz not null default now(),
    deleted_at_utc timestamptz null,
    deleted_by_user_id uuid null references qms_users(id) on delete restrict,
    constraint ux_notice_posts_author_request unique (author_user_id, request_id),
    constraint ck_notice_posts_title check (btrim(title) <> '' and char_length(title) <= 100),
    constraint ck_notice_posts_body check (btrim(body) <> '' and char_length(body) <= 2000),
    constraint ck_notice_posts_author_display_name check (btrim(author_display_name_snapshot) <> ''),
    constraint ck_notice_posts_author_department_name check (
        author_department_name_snapshot is null or btrim(author_department_name_snapshot) <> ''
    ),
    constraint ck_notice_posts_deleted_pair check (
        (deleted_at_utc is null and deleted_by_user_id is null)
        or (deleted_at_utc is not null and deleted_by_user_id is not null)
    )
);

create index if not exists ix_notice_posts_active_created
    on notice_posts(created_at_utc desc, id desc)
    where deleted_at_utc is null;
