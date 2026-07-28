drop trigger if exists trg_guard_iqc_template_items on iqc_report_template_items;
drop trigger if exists trg_guard_panel_quality_template_items on panel_quality_template_items;

alter table iqc_report_template_items
    add column if not exists definition_key uuid null;

with canonical as (
    select distinct on (version.template_id, item.item_code)
           version.template_id,
           item.item_code,
           item.id as definition_key
    from iqc_report_template_items item
    join iqc_report_template_versions version on version.id=item.template_version_id
    order by version.template_id,
             item.item_code,
             case version.lifecycle_status when 'Active' then 0 when 'Draft' then 1 else 2 end,
             version.version_number desc,
             item.id
)
update iqc_report_template_items item
set definition_key=canonical.definition_key
from iqc_report_template_versions version,
     canonical
where version.id=item.template_version_id
  and canonical.template_id=version.template_id
  and canonical.item_code=item.item_code
  and item.definition_key is distinct from canonical.definition_key;

alter table iqc_report_template_items
    alter column definition_key set not null;

create unique index if not exists ux_iqc_report_template_items_definition
    on iqc_report_template_items(template_version_id, definition_key);

alter table panel_quality_template_items
    add column if not exists definition_key uuid null;

with canonical as (
    select distinct on (version.stage_code, item.item_code)
           version.stage_code,
           item.item_code,
           item.id as definition_key
    from panel_quality_template_items item
    join panel_quality_template_versions version on version.id=item.template_version_id
    order by version.stage_code,
             item.item_code,
             case version.lifecycle_status when 'Active' then 0 when 'Draft' then 1 else 2 end,
             version.version_number desc,
             item.id
)
update panel_quality_template_items item
set definition_key=canonical.definition_key
from panel_quality_template_versions version,
     canonical
where version.id=item.template_version_id
  and canonical.stage_code=version.stage_code
  and canonical.item_code=item.item_code
  and item.definition_key is distinct from canonical.definition_key;

alter table panel_quality_template_items
    alter column definition_key set not null;

create unique index if not exists ux_panel_quality_template_items_definition
    on panel_quality_template_items(template_version_id, definition_key);

update iqc_report_template_versions
set lifecycle_status='Archived',
    archived_at_utc=coalesce(archived_at_utc, now()),
    row_version=row_version+1,
    updated_at_utc=now()
where lifecycle_status='Draft';

update panel_quality_template_versions
set lifecycle_status='Archived',
    archived_at_utc=coalesce(archived_at_utc, now()),
    row_version=row_version+1,
    updated_at_utc=now()
where lifecycle_status='Draft'
  and stage_code in ('LQC','OQC');

alter table production_control_plan_connections
    drop constraint if exists ck_production_control_plan_connections_parameter,
    add constraint ck_production_control_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED')
            and source_definition_key is not null)
        or (source_code in ('IQC_PASSED', 'OQC_PASSED'))
        or (source_code not in (
                'MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED',
                'IQC_PASSED', 'OQC_PASSED'
            )
            and source_definition_key is null)
    );

alter table project_production_plan_connections
    drop constraint if exists ck_project_production_plan_connections_parameter,
    add constraint ck_project_production_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED')
            and source_definition_key is not null)
        or (source_code in ('IQC_PASSED', 'OQC_PASSED'))
        or (source_code not in (
                'MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED',
                'IQC_PASSED', 'OQC_PASSED'
            )
            and source_definition_key is null)
    );

comment on column iqc_report_template_items.definition_key is
    'Stable inspection definition identity preserved across hidden internal template snapshots and used by production-plan evidence links.';

comment on column panel_quality_template_items.definition_key is
    'Stable inspection definition identity preserved across hidden internal template snapshots and used by production-plan evidence links.';

create trigger trg_guard_iqc_template_items
before insert or update or delete on iqc_report_template_items
for each row execute function guard_iqc_template_item_mutation();

create trigger trg_guard_panel_quality_template_items
before insert or update or delete on panel_quality_template_items
for each row execute function guard_panel_quality_template_item_mutation();
