-- Extend approved change capture to new customer, Gate and maintenance state.
-- Composite targets retain each customer and Gate identity; free text keeps metadata-only projection.
create or replace function qms_audit_target_key(p_row jsonb)
returns text
language sql
immutable
as $$
    select left(coalesce(
        nullif(p_row ->> 'id', ''),
        nullif(concat_ws('|',
            case when nullif(p_row ->> 'project_id', '') is not null then 'project_id=' || (p_row ->> 'project_id') end,
            case when nullif(p_row ->> 'panel_id', '') is not null then 'panel_id=' || (p_row ->> 'panel_id') end,
            case when nullif(p_row ->> 'user_id', '') is not null then 'user_id=' || (p_row ->> 'user_id') end,
            case when nullif(p_row ->> 'role_id', '') is not null then 'role_id=' || (p_row ->> 'role_id') end,
            case when nullif(p_row ->> 'customer_id', '') is not null then 'customer_id=' || (p_row ->> 'customer_id') end,
            case when nullif(p_row ->> 'department_id', '') is not null then 'department_id=' || (p_row ->> 'department_id') end,
            case when nullif(p_row ->> 'stage_sequence', '') is not null then 'stage_sequence=' || (p_row ->> 'stage_sequence') end,
            case when nullif(p_row ->> 'item_id', '') is not null then 'item_id=' || (p_row ->> 'item_id') end,
            case when nullif(p_row ->> 'report_id', '') is not null then 'report_id=' || (p_row ->> 'report_id') end,
            case when nullif(p_row ->> 'attempt_id', '') is not null then 'attempt_id=' || (p_row ->> 'attempt_id') end,
            case when nullif(p_row ->> 'version_id', '') is not null then 'version_id=' || (p_row ->> 'version_id') end,
            case when nullif(p_row ->> 'work_item_id', '') is not null then 'work_item_id=' || (p_row ->> 'work_item_id') end,
            case when nullif(p_row ->> 'work_date', '') is not null then 'work_date=' || (p_row ->> 'work_date') end,
            case when nullif(p_row ->> 'date', '') is not null then 'date=' || (p_row ->> 'date') end,
            case when nullif(p_row ->> 'effective_date', '') is not null then 'effective_date=' || (p_row ->> 'effective_date') end,
            case when nullif(p_row ->> 'metric_code', '') is not null then 'metric_code=' || (p_row ->> 'metric_code') end,
            case when nullif(p_row ->> 'target_type', '') is not null then 'target_type=' || (p_row ->> 'target_type') end,
            case when nullif(p_row ->> 'code', '') is not null then 'code=' || (p_row ->> 'code') end
        ), ''),
        'row'
    ), 240);
$$;

create trigger trg_qms_global_audit_osan_customers
after insert or update or delete on osan_customers
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_osan_customer_assignments
after insert or update or delete on osan_customer_assignments
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_osan_gate_configuration
after insert or update or delete on osan_gate_configuration
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_osan_gate_departments
after insert or update or delete on osan_gate_departments
for each row execute function qms_audit_capture_row_change();

create trigger trg_qms_global_audit_deployment_maintenance
after insert or update or delete on deployment_maintenance
for each row execute function qms_audit_capture_row_change();

-- Notice setting events are their own canonical ledger and must remain append-only.
create trigger trg_notice_setting_events_append_only
before update or delete on notice_setting_events
for each row execute function qms_audit_guard_append_only();
