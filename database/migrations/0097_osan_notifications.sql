alter table notifications drop constraint if exists ck_notifications_source_kind;
alter table notifications add constraint ck_notifications_source_kind check (source_kind in (
'Automatic','Manual','ChannelNotice','WorkAssignment','PendingAssignment','PendingClosed',
'ProjectCreated','ProjectDeliveryDateChanged','ProjectStatusChanged','ReinspectionRequested',
'ProjectDeliveryCompleted','ProjectCompletion','DailyDigest','Escalation','System','OsanWorkflow'));
alter table notification_deliveries drop constraint if exists ck_notification_deliveries_delivery_type;
alter table notification_deliveries add constraint ck_notification_deliveries_delivery_type check (delivery_type in (
'WorkItemCreated','ReferenceDigest','UrgentBlocking','DailyDigest','ProjectCompletion','ManualTest',
'DueSoonL0','OverdueL1','OverdueL2','OverdueL3','WebPushNotification','OsanWorkflow'));

-- Lifetime completion marker also covers projects completed before notifications were introduced.
create table if not exists osan_project_completion_notifications (
    project_id uuid primary key references projects(id) on delete restrict,
    first_completed_at timestamptz not null
);
insert into osan_project_completion_notifications(project_id,first_completed_at)
select id,updated_at_utc from projects where project_profile='Osan' and status='Completed'
on conflict(project_id) do nothing;

-- Osan enables only the approved workflow mail path; other external delivery types stay blocked.
create or replace function prevent_osan_external_notification_delivery()
returns trigger language plpgsql as $$
begin
    if exists(select 1 from qms_database_identity where singleton=true and business_unit_code='OSAN')
       and not (new.channel='Mail' and new.delivery_type='OsanWorkflow' and exists (
           select 1 from notifications n
           join projects p on p.id=n.project_id and p.project_profile='Osan'
           join notification_recipients r on r.notification_id=n.id
           where n.id=new.notification_id and n.source_kind='OsanWorkflow'
             and n.project_id=new.project_id and r.id=new.notification_recipient_id
             and r.user_id=new.recipient_user_id
       )) then
        raise exception using errcode='42501',message='external_notification_delivery_disabled_for_business_unit';
    end if;
    return new;
end;
$$;

drop trigger if exists trg_guard_osan_completion_notification on osan_project_completion_notifications;
create trigger trg_guard_osan_completion_notification
before update or delete on osan_project_completion_notifications
for each row execute function guard_osan_progress_append_only();
