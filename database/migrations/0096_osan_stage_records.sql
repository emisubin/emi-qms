alter table osan_project_target_steps add column comment text not null default '' check(length(comment)<=1000);
alter table osan_project_target_steps add column rejected boolean not null default false;
alter table osan_project_target_steps add column current_record_id uuid;
alter table osan_photo_edit_requests add column invalidated_at timestamptz;
alter table osan_photo_edit_requests add column saved_by uuid references qms_users(id);
drop index ux_osan_photo_open_request;
create unique index ux_osan_photo_open_request on osan_photo_edit_requests(step_id)
 where used_at is null and invalidated_at is null;
create table osan_stage_records (
 id uuid primary key, operation_id uuid not null, project_id uuid not null, target_id uuid not null, step_id uuid not null,
 event_type text not null check(event_type in ('Complete','Edit','Request','Approve','Reject','Reset')),
 actor_user_id uuid not null references qms_users(id), occurred_at_utc timestamptz not null default now(),
 comment text not null default '' check(length(comment)<=1000), reason text, photo_ids uuid[] not null default '{}',
 fingerprint text not null default '',
 foreign key(project_id,target_id,step_id) references osan_project_target_steps(project_id,target_id,id),
 unique(operation_id,step_id,event_type)
);
create index ix_osan_stage_records_history on osan_stage_records(project_id,step_id,occurred_at_utc desc);
-- Existing original evidence and every saved revision remain independently readable.
insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,occurred_at_utc,photo_ids)
 select uuid_generate_v4(),s.id,s.project_id,s.target_id,s.id,'Complete',s.completed_by_user_id,s.completed_at_utc,
 array(select l.photo_id from osan_progress_step_photos l join osan_progress_photos p on p.id=l.photo_id
       where l.step_id=s.id order by p.display_order)
 from osan_project_target_steps s where s.status='Completed' and s.completed_by_user_id is not null;
insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,occurred_at_utc,photo_ids)
 select uuid_generate_v4(),r.id,r.project_id,r.target_id,r.step_id,'Edit',r.requested_by,r.used_at,
 array(select f.id from osan_photo_revision_files f where f.request_id=r.id order by f.display_order)
 from osan_photo_edit_requests r where r.used_at is not null;
insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,occurred_at_utc)
 select uuid_generate_v4(),r.id,r.project_id,r.target_id,r.step_id,'Request',r.requested_by,r.requested_at from osan_photo_edit_requests r;
insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,occurred_at_utc)
 select uuid_generate_v4(),r.id,r.project_id,r.target_id,r.step_id,'Approve',r.approved_by,r.approved_at
 from osan_photo_edit_requests r where r.approved_at is not null;
update osan_project_target_steps s set current_record_id=r.id,completed_at_utc=r.occurred_at_utc,completed_by_user_id=r.actor_user_id
 from (select distinct on(step_id) * from osan_stage_records where event_type in ('Complete','Edit')
       order by step_id,occurred_at_utc desc,id desc) r where s.id=r.step_id;
alter table osan_project_target_steps add foreign key(current_record_id) references osan_stage_records(id);
create trigger trg_guard_osan_stage_records before update or delete on osan_stage_records
 for each row execute function guard_osan_progress_append_only();
create or replace view osan_all_progress_photos as
 select p.project_id,p.id,p.original_file_name,p.normalized_mime,p.byte_size,p.sha256,p.uploaded_at_utc,p.uploaded_by_user_id
 from osan_progress_photos p
 union all
 select r.project_id,f.id,f.original_file_name,f.normalized_mime,octet_length(f.content),f.sha256,r.used_at,coalesce(r.saved_by,r.requested_by)
 from osan_photo_revision_files f join osan_photo_edit_requests r on r.id=f.request_id where r.used_at is not null;
create or replace view osan_current_progress_photos as
 select s.project_id,s.id as step_id,p.id,ids.ordinality::integer as display_order,p.original_file_name,p.normalized_mime,p.byte_size,
 p.sha256,p.uploaded_at_utc,p.uploaded_by_user_id
 from osan_project_target_steps s join osan_stage_records r on r.id=s.current_record_id
 cross join lateral unnest(r.photo_ids) with ordinality ids(id,ordinality)
 join osan_all_progress_photos p on p.id=ids.id and p.project_id=s.project_id;
create or replace view osan_active_project_target_steps as select * from osan_project_target_steps s
 where exists(select 1 from osan_project_targets t where t.id=s.target_id and t.is_active);
