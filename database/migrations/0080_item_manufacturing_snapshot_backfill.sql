update project_manufacturing_step_snapshots snapshot
set is_active=false,
    row_version=snapshot.row_version+1,
    updated_at_utc=now()
from (
    select project.id as project_id
    from projects project
    join production_product_types product_type
      on product_type.is_active
     and upper(btrim(project.item))=upper(btrim(product_type.code))
    join production_control_manufacturing_templates template
      on template.product_type_id=product_type.id
    join production_control_manufacturing_versions version
      on version.template_id=template.id
     and version.lifecycle_status='Active'
    where project.deleted_at_utc is null
      and exists (
          select 1
          from production_control_manufacturing_items item
          where item.template_version_id=version.id
      )
) current
where snapshot.project_id=current.project_id
  and snapshot.is_active;

insert into project_manufacturing_step_snapshots (
    project_id, source_template_version_id, definition_key, sequence_number,
    step_name_snapshot, step_role, is_active
)
select project.id,
       version.id,
       item.definition_key,
       item.display_order,
       item.label,
       item.step_role,
       true
from projects project
join production_product_types product_type
  on product_type.is_active
 and upper(btrim(project.item))=upper(btrim(product_type.code))
join production_control_manufacturing_templates template
  on template.product_type_id=product_type.id
join production_control_manufacturing_versions version
  on version.template_id=template.id
 and version.lifecycle_status='Active'
join production_control_manufacturing_items item
  on item.template_version_id=version.id
where project.deleted_at_utc is null
order by project.id,item.display_order
on conflict (project_id, definition_key) do update
set source_template_version_id=excluded.source_template_version_id,
    sequence_number=excluded.sequence_number,
    step_name_snapshot=excluded.step_name_snapshot,
    step_role=excluded.step_role,
    is_active=true,
    row_version=project_manufacturing_step_snapshots.row_version+1,
    updated_at_utc=now();
