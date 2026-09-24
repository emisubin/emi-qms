alter table notice_posts add column pinned boolean not null default false,
    add column popup_version integer not null default 0 check(popup_version >= 0),
    add column popup_enabled boolean not null default false;
create table notice_reads (
    notice_id uuid not null references notice_posts(id), user_id uuid not null references qms_users(id),
    read_at_utc timestamptz not null default now(), primary key(notice_id,user_id)
);
create table notice_popup_receipts (
    notice_id uuid not null references notice_posts(id), user_id uuid not null references qms_users(id),
    popup_version integer not null, shown_at_utc timestamptz not null default now(),
    primary key(notice_id,user_id,popup_version)
);
create table notice_setting_events (
    id uuid primary key default uuid_generate_v4(), notice_id uuid not null references notice_posts(id),
    actor_user_id uuid not null references qms_users(id), created_at_utc timestamptz not null default now(),
    pinned boolean not null,popup_enabled boolean not null,popup_version integer not null
);
alter table notice_attachments drop constraint ck_notice_attachments_size;
alter table notice_attachments add constraint ck_notice_attachments_size check(byte_size between 1 and 20971520 and octet_length(content)=byte_size);
alter table notice_attachments drop constraint ck_notice_attachments_mime;
alter table notice_attachments add constraint ck_notice_attachments_mime check(normalized_mime in (
 'application/pdf','image/jpeg','image/png','application/zip','application/x-hwp',
 'application/msword','application/vnd.ms-excel','application/vnd.ms-powerpoint',
 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
 'application/vnd.openxmlformats-officedocument.presentationml.presentation'));
