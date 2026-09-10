create table if not exists osan_project_management_history (
 id uuid primary key default uuid_generate_v4(), project_id uuid not null references projects(id),
 actor_user_id uuid not null references qms_users(id), action text not null check(action in ('Update','Delete')),
 before_json jsonb not null, after_json jsonb not null, occurred_at_utc timestamptz not null default now()
);
create table if not exists osan_photo_edit_requests (
 id uuid primary key, project_id uuid not null, target_id uuid not null, step_id uuid not null,
 requested_by uuid not null references qms_users(id), requested_at timestamptz not null default now(),
 approved_by uuid references qms_users(id), approved_at timestamptz,
 used_at timestamptz, fingerprint text,
 foreign key(project_id,target_id,step_id) references osan_project_target_steps(project_id,target_id,id),
 check ((approved_by is null)=(approved_at is null)),
 check ((used_at is null)=(fingerprint is null)), check(used_at is null or approved_at is not null)
);
create unique index if not exists ux_osan_photo_open_request on osan_photo_edit_requests(step_id) where used_at is null;
create table if not exists osan_photo_revision_files (
 id uuid primary key, request_id uuid not null references osan_photo_edit_requests(id),
 display_order integer not null check(display_order between 1 and 5), original_file_name text not null,
 normalized_mime text not null check(normalized_mime in ('image/jpeg','image/png')),
 sha256 text not null, content bytea not null check(octet_length(content) between 1 and 5242880),
 unique(request_id,display_order)
);
drop trigger if exists trg_guard_osan_management_history on osan_project_management_history;
create trigger trg_guard_osan_management_history before update or delete on osan_project_management_history
 for each row execute function guard_osan_progress_append_only();
drop trigger if exists trg_guard_osan_photo_revision_files on osan_photo_revision_files;
create trigger trg_guard_osan_photo_revision_files before update or delete on osan_photo_revision_files
 for each row execute function guard_osan_progress_append_only();

create or replace view osan_current_progress_photos as
 select l.project_id,l.step_id,p.id,p.display_order,p.original_file_name,p.normalized_mime,p.byte_size,
   p.sha256,p.uploaded_at_utc,p.uploaded_by_user_id
 from osan_progress_step_photos l join osan_progress_photos p on p.id=l.photo_id
 where not exists(select 1 from osan_photo_edit_requests r where r.step_id=l.step_id and r.used_at is not null)
 union all
 select r.project_id,r.step_id,f.id,f.display_order,f.original_file_name,f.normalized_mime,octet_length(f.content),
   f.sha256,r.used_at,r.requested_by
 from osan_photo_edit_requests r join osan_photo_revision_files f on f.request_id=r.id
 where r.used_at is not null and not exists(select 1 from osan_photo_edit_requests newer
   where newer.step_id=r.step_id and newer.used_at is not null and (newer.used_at,newer.id)>(r.used_at,r.id));
alter table osan_project_targets add column if not exists is_active boolean not null default true;
create or replace view osan_active_project_targets as select * from osan_project_targets where is_active;
create or replace view osan_active_project_target_steps as select * from osan_project_target_steps s
 where exists(select 1 from osan_project_targets t where t.id=s.target_id and t.is_active);
