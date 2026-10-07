-- Direct edits have independent append-only evidence; legacy approval evidence stays readable.
create table osan_direct_edit_files (
 id uuid primary key, project_id uuid not null references projects(id), operation_id uuid not null,
 uploaded_by_user_id uuid not null references qms_users(id), uploaded_at_utc timestamptz not null default now(),
 display_order integer not null check(display_order between 1 and 5), original_file_name text not null,
 normalized_mime text not null check(normalized_mime in ('image/jpeg','image/png','image/heic')),
 sha256 text not null, content bytea not null check(octet_length(content) between 1 and 41943040),
 unique(operation_id,display_order)
);
create trigger trg_guard_osan_direct_edit_files before update or delete on osan_direct_edit_files
 for each row execute function guard_osan_progress_append_only();
create or replace view osan_all_progress_photos as
 select p.project_id,p.id,p.original_file_name,p.normalized_mime,p.byte_size,p.sha256,p.uploaded_at_utc,p.uploaded_by_user_id
 from osan_progress_photos p
 union all
 select r.project_id,f.id,f.original_file_name,f.normalized_mime,octet_length(f.content),f.sha256,r.used_at,coalesce(r.saved_by,r.requested_by)
 from osan_photo_revision_files f join osan_photo_edit_requests r on r.id=f.request_id where r.used_at is not null
 union all
 select f.project_id,f.id,f.original_file_name,f.normalized_mime,octet_length(f.content),f.sha256,f.uploaded_at_utc,f.uploaded_by_user_id
 from osan_direct_edit_files f;
