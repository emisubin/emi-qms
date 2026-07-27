alter table panel_quality_report_responses
    add column if not exists manufacturing_definition_key uuid null;

create index if not exists ix_panel_quality_report_responses_manufacturing_definition
    on panel_quality_report_responses(report_id, manufacturing_definition_key)
    where manufacturing_definition_key is not null;

comment on column panel_quality_report_responses.manufacturing_definition_key is
    'Immutable project manufacturing definition matched when an LQC check response is saved. Null for legacy executions and non-LQC stages.';
