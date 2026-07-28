alter table form_template_audit_events
    drop constraint if exists ck_form_template_audit_action,
    add constraint ck_form_template_audit_action check (action in (
        'DraftCreated', 'DraftSaved', 'VersionActivated', 'DraftArchived',
        'ManagerAssigned', 'ManagerRevoked', 'VersionsExported',
        'CurrentCreated', 'CurrentSaved'
    ));

create temporary table production_control_current_manufacturing_versions
on commit drop
as
select distinct on (template_id)
       template_id,
       id
from production_control_manufacturing_versions
order by template_id,
         case lifecycle_status
             when 'Draft' then 0
             when 'Active' then 1
             else 2
         end,
         version_number desc,
         updated_at_utc desc,
         id;

update production_control_manufacturing_versions version
set lifecycle_status='Archived',
    archived_at_utc=coalesce(version.archived_at_utc, now()),
    row_version=version.row_version+1,
    updated_at_utc=now()
where version.lifecycle_status <> 'Archived'
  and not exists (
      select 1
      from production_control_current_manufacturing_versions current_version
      where current_version.id=version.id
  );

update production_control_manufacturing_versions version
set lifecycle_status='Active',
    activated_at_utc=coalesce(version.activated_at_utc, now()),
    archived_at_utc=null,
    row_version=version.row_version+1,
    updated_at_utc=now()
from production_control_current_manufacturing_versions current_version
where version.id=current_version.id
  and version.lifecycle_status <> 'Active';

create temporary table production_control_current_plan_versions
on commit drop
as
select distinct on (template_id)
       template_id,
       id
from production_control_plan_versions
order by template_id,
         case lifecycle_status
             when 'Draft' then 0
             when 'Active' then 1
             else 2
         end,
         version_number desc,
         updated_at_utc desc,
         id;

update production_control_plan_versions version
set lifecycle_status='Archived',
    archived_at_utc=coalesce(version.archived_at_utc, now()),
    row_version=version.row_version+1,
    updated_at_utc=now()
where version.lifecycle_status <> 'Archived'
  and not exists (
      select 1
      from production_control_current_plan_versions current_version
      where current_version.id=version.id
  );

update production_control_plan_versions version
set lifecycle_status='Active',
    activated_at_utc=coalesce(version.activated_at_utc, now()),
    archived_at_utc=null,
    row_version=version.row_version+1,
    updated_at_utc=now()
from production_control_current_plan_versions current_version
where version.id=current_version.id
  and version.lifecycle_status <> 'Active';

with ranked_connections as (
    select connection.id,
           row_number() over (
               partition by connection.plan_item_id
               order by case connection.source_code
                   when 'DELIVERED' then 11
                   when 'DEPARTED' then 10
                   when 'PACKED' then 9
                   when 'FAT_PASSED' then 8
                   when 'CUSTOMER_INSPECTION_PASSED' then 7
                   when 'OQC_PASSED' then 6
                   when 'LQC_PASSED' then 5
                   when 'MANUFACTURING_STEP_COMPLETED' then 4
                   when 'IQC_PASSED' then 3
                   when 'MATERIAL_RECEIPT_CONFIRMED' then 2
                   when 'PURCHASE_ORDERED' then 1
                   else 0
               end desc,
               connection.id
           ) as position
    from production_control_plan_connections connection
)
delete from production_control_plan_connections connection
using ranked_connections ranked
where connection.id=ranked.id
  and ranked.position > 1;

drop index if exists ux_production_control_plan_connections;

create unique index if not exists ux_production_control_plan_connections_one_to_one
    on production_control_plan_connections(plan_item_id);

delete from production_control_manufacturing_items item
where not exists (
    select 1
    from production_control_current_manufacturing_versions current_version
    where current_version.id=item.template_version_id
);

delete from production_control_plan_items item
where not exists (
    select 1
    from production_control_current_plan_versions current_version
    where current_version.id=item.template_version_id
);

delete from production_control_manufacturing_versions version
where not exists (
          select 1
          from production_control_current_manufacturing_versions current_version
          where current_version.id=version.id
      )
  and not exists (
          select 1
          from project_production_plans plan
          where plan.linked_manufacturing_template_version_id=version.id
      )
  and not exists (
          select 1
          from project_manufacturing_step_snapshots snapshot
          where snapshot.source_template_version_id=version.id
      );

delete from production_control_plan_versions version
where not exists (
          select 1
          from production_control_current_plan_versions current_version
          where current_version.id=version.id
      )
  and not exists (
          select 1
          from project_production_plans plan
          where plan.linked_plan_template_version_id=version.id
      );

comment on index ux_production_control_plan_connections_one_to_one is
    'A current production plan template item selects exactly one actual-result source.';
