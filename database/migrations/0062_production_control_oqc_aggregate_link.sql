update production_control_plan_connections
set source_definition_key = null
where source_code = 'OQC_PASSED'
  and source_definition_key is not null;

alter table production_control_plan_connections
    drop constraint if exists ck_production_control_plan_connections_parameter,
    add constraint ck_production_control_plan_connections_parameter check (
        (source_code in ('MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED')
            and source_definition_key is not null)
        or (source_code = 'IQC_PASSED')
        or (source_code not in (
                'MANUFACTURING_STEP_COMPLETED', 'LQC_PASSED', 'IQC_PASSED'
            )
            and source_definition_key is null)
    );

comment on constraint ck_production_control_plan_connections_parameter
    on production_control_plan_connections is
    'Current planning templates use manufacturing/LQC/IQC detail identities; OQC and other stage-level results use no detail identity.';

comment on column project_production_plan_connections.source_definition_key is
    'Project snapshot parameter. Legacy OQC detail identities remain readable; newly saved OQC links use the aggregate passed event with no parameter.';
