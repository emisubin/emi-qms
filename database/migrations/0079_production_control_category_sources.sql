alter table production_control_plan_connections
    drop constraint if exists ck_production_control_plan_connections_parameter,
    add constraint ck_production_control_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED')
            and source_definition_key is not null)
        or (source_code in (
                'IQC_PASSED',
                'PURCHASE_ORDERED',
                'MATERIAL_RECEIPT_CONFIRMED'
            ))
        or (source_code not in (
                'MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED', 'IQC_PASSED',
                'PURCHASE_ORDERED', 'MATERIAL_RECEIPT_CONFIRMED'
            )
            and source_definition_key is null)
    );

alter table project_production_plan_connections
    drop constraint if exists ck_project_production_plan_connections_parameter,
    add constraint ck_project_production_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED')
            and source_definition_key is not null)
        or (source_code in (
                'IQC_PASSED',
                'OQC_PASSED',
                'PURCHASE_ORDERED',
                'MATERIAL_RECEIPT_CONFIRMED'
            ))
        or (source_code not in (
                'MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED', 'IQC_PASSED', 'OQC_PASSED',
                'PURCHASE_ORDERED', 'MATERIAL_RECEIPT_CONFIRMED'
            )
            and source_definition_key is null)
    );

alter table project_manufacturing_step_snapshots
    drop constraint if exists ux_project_manufacturing_step_snapshots_order;

create unique index if not exists ux_project_manufacturing_step_snapshots_active_order
    on project_manufacturing_step_snapshots(project_id, sequence_number)
    where is_active;

comment on column production_control_plan_connections.source_definition_key is
    'Optional material category identity for purchase/receipt, IQC definition identity, or required manufacturing/LQC definition identity.';

comment on column project_production_plan_connections.source_definition_key is
    'Project plan connection parameter: optional material category for purchase/receipt, quality identity, or manufacturing/LQC identity.';
