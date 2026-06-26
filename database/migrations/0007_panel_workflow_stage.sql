alter table panel_placeholders
    add column if not exists workflow_stage text not null default 'BeforeManufacturing';

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_panel_placeholders_workflow_stage'
    ) then
        alter table panel_placeholders
            add constraint ck_panel_placeholders_workflow_stage
            check (workflow_stage in (
                'BeforeManufacturing',
                'ManufacturingInProgress',
                'ManufacturingCompleted',
                'InspectionInProgress',
                'InspectionCompleted',
                'PackingCompleted',
                'ShipmentCompleted'
            ));
    end if;
end $$;

create index if not exists ix_panel_placeholders_project_workflow_stage
    on panel_placeholders(project_id, workflow_stage)
    where status = 'Active';
