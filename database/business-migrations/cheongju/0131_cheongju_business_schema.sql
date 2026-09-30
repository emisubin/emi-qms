-- Split the shared 0130 schema into the fixed Cheongju business schema.
-- DatabaseMigrationRunner wraps this file and its ledger insert in one transaction.
-- Every DROP uses PostgreSQL's default RESTRICT behavior; no dependent object is
-- removed implicitly with CASCADE.

do $migration$
declare
    expected_tables text[] := array[
        'admin_master_change_logs',
        'audit_coverage_state',
        'audit_event_changes',
        'audit_events',
        'authorization_audit_events',
        'busbar_audit',
        'busbar_bom_lines',
        'busbar_boms',
        'busbar_detached_pages',
        'busbar_ecount_attempts',
        'busbar_ecount_employees',
        'busbar_ecount_jobs',
        'busbar_ecount_runtime',
        'busbar_label_events',
        'busbar_label_requests',
        'busbar_ledger',
        'busbar_master_access',
        'busbar_materials',
        'busbar_operations',
        'busbar_photo_history',
        'busbar_photos',
        'busbar_plans',
        'busbar_product_families',
        'busbar_product_qr',
        'busbar_products',
        'busbar_projects',
        'busbar_publication_recovery',
        'busbar_purchases',
        'busbar_receipts',
        'busbar_settings',
        'busbar_shipment_products',
        'busbar_shipments',
        'busbar_stock',
        'busbar_workers',
        'data_export_events',
        'departments',
        'deployment_maintenance',
        'deployment_maintenance_popup_receipts',
        'form_template_audit_events',
        'form_template_manager_bindings',
        'g2_daily_metrics',
        'g2_defect_inventory_counts',
        'g2_inventory_counts',
        'g2_targets',
        'iqc_report_pdf_artifacts',
        'iqc_report_photos',
        'iqc_report_responses',
        'iqc_report_template_items',
        'iqc_report_template_versions',
        'iqc_report_templates',
        'iqc_reports',
        'logistics_batch_panels',
        'logistics_batch_units',
        'logistics_batches',
        'logistics_delivery_results',
        'logistics_evidence',
        'logistics_operations',
        'logistics_packing_unit_panels',
        'logistics_packing_units',
        'lqc_item_setting_audit_events',
        'lqc_item_settings',
        'manufacturing_step_template_items',
        'manufacturing_step_template_versions',
        'manufacturing_step_templates',
        'material_categories',
        'material_category_audit_events',
        'material_category_iqc_setting_audit_events',
        'material_category_iqc_settings',
        'material_iqc_attempts',
        'material_iqc_scan_attachments',
        'material_iqc_scan_reports',
        'material_receipt_events',
        'material_receipts',
        'notice_attachments',
        'notice_popup_receipts',
        'notice_post_revisions',
        'notice_posts',
        'notice_reads',
        'notice_setting_events',
        'notification_deliveries',
        'notification_delivery_attempts',
        'notification_delivery_reprocess_events',
        'notification_recipients',
        'notifications',
        'osan_customer_assignment_versions',
        'osan_customer_assignments',
        'osan_customers',
        'osan_gate_configuration',
        'osan_gate_departments',
        'osan_notification_events',
        'osan_notification_global_preference_profiles',
        'osan_notification_global_preferences',
        'osan_notification_preference_profiles',
        'osan_notification_preferences',
        'osan_photo_edit_requests',
        'osan_photo_revision_files',
        'osan_progress_operations',
        'osan_progress_photos',
        'osan_progress_step_photos',
        'osan_project_completion_notifications',
        'osan_project_create_operations',
        'osan_project_events',
        'osan_project_management_history',
        'osan_project_target_steps',
        'osan_project_targets',
        'osan_stage_issues',
        'osan_stage_records',
        'osan_stage_work_request_recipients',
        'osan_stage_work_requests',
        'panel_information_excel_import_batches',
        'panel_kitting_batches',
        'panel_kitting_completions',
        'panel_manufacturing_assembly_batch_operations',
        'panel_manufacturing_completion_confirmations',
        'panel_manufacturing_events',
        'panel_manufacturing_execution_steps',
        'panel_manufacturing_executions',
        'panel_manufacturing_operations',
        'panel_manufacturing_release_operations',
        'panel_placeholders',
        'panel_qr_codes',
        'panel_qr_events',
        'panel_quality_inspection_attempts',
        'panel_quality_operations',
        'panel_quality_report_pdf_artifacts',
        'panel_quality_report_photos',
        'panel_quality_report_responses',
        'panel_quality_reports',
        'panel_quality_template_items',
        'panel_quality_template_versions',
        'pending_action_photos',
        'pending_comments',
        'pending_history',
        'pending_issue_type_audit_events',
        'pending_issue_type_catalog',
        'pending_issues',
        'pending_photo_operations',
        'permissions',
        'procurement_excel_import_batch_projects',
        'procurement_excel_import_batches',
        'procurement_required_item_template_rows',
        'procurement_required_item_templates',
        'production_control_manufacturing_items',
        'production_control_manufacturing_templates',
        'production_control_manufacturing_versions',
        'production_control_plan_connections',
        'production_control_plan_items',
        'production_control_plan_templates',
        'production_control_plan_versions',
        'production_plan_template_audit_events',
        'production_plan_template_steps',
        'production_plan_templates',
        'production_planning_excel_import_batches',
        'production_product_types',
        'project_assignees',
        'project_audit_events',
        'project_manufacturing_step_snapshots',
        'project_procurement_items',
        'project_production_plan_connections',
        'project_production_plan_items',
        'project_production_plan_set_default_values',
        'project_production_plan_set_defaults',
        'project_production_plan_set_item_values',
        'project_production_plan_set_scopes',
        'project_production_plans',
        'project_workflow_events',
        'projects',
        'qms_database_identity',
        'qms_users',
        'role_permissions',
        'roles',
        'sales_billing_request_batches',
        'sales_billing_request_download_events',
        'sales_billing_request_items',
        'sales_billing_request_operations',
        'sales_monthly_billing_confirmations',
        'sales_monthly_billing_ledgers',
        'sales_monthly_billing_operations',
        'sales_monthly_billing_revision_cases',
        'sales_monthly_billing_revision_panels',
        'sales_monthly_billing_revisions',
        'sales_monthly_target_audit_events',
        'sales_monthly_targets',
        'sales_settlement_operations',
        'sales_settlements',
        'schema_migrations',
        'site_access_coverage_state',
        'site_access_sessions',
        'system_holidays',
        'ul891_recovery_case_events',
        'ul891_recovery_cases',
        'ul891_set_design_slots',
        'ul891_set_instances',
        'ul891_set_operations',
        'ul891_set_spec_components',
        'ul891_set_spec_versions',
        'ul891_set_specs',
        'user_notification_preference_audit_events',
        'user_notification_preference_profiles',
        'user_notification_preferences',
        'user_profile_photo_audit_events',
        'user_profile_photos',
        'user_project_access',
        'user_roles',
        'web_push_subscription_events',
        'web_push_subscriptions',
        'work_item_escalations',
        'work_items',
        'workflow_stages'
    ]::text[];
    expected_project_columns text[] := array[
        'cancelled_at_utc',
        'cancelled_by_user_id',
        'completed_at_utc',
        'completed_by_user_id',
        'created_at_utc',
        'created_by_user_id',
        'currency_code',
        'customer_name',
        'delete_reason',
        'deleted_at_utc',
        'deleted_by_user_id',
        'deleted_correlation_id',
        'delivery_date',
        'delivery_location',
        'fat_required',
        'held_at_utc',
        'held_by_user_id',
        'id',
        'iqc_routing_policy',
        'item',
        'lqc_operational_snapshot',
        'lqc_template_version_id',
        'lse_task_number',
        'name',
        'osan_customer_id',
        'osan_delivery_hold',
        'osan_po_number',
        'osan_product_name',
        'osan_quantity',
        'osan_work_order_number',
        'packaging_method',
        'project_code',
        'project_key',
        'project_number',
        'project_profile',
        'project_title',
        'project_title_normalized',
        'sales_amount',
        'sales_owner_user_id',
        'status',
        'status_reason',
        'structure_mode',
        'updated_at_utc'
    ]::text[];
    actual_names text[];
    relation_name text;
    has_rows boolean;
begin
    if current_setting('emi_qms.business_schema_separation', true) is distinct from 'CHEONGJU' then
        raise exception using errcode = 'P0001', message = 'business_schema_explicit_consent_required';
    end if;

    if (select count(*) from qms_database_identity
        where singleton and database_kind = 'business' and business_unit_code = 'CHEONGJU'
          and schema_contract = '0086_business_unit_database_identity') <> 1
       or (select count(*) from qms_database_identity) <> 1 then
        raise exception using errcode = 'P0001', message = 'business_schema_identity_mismatch';
    end if;

    select coalesce(array_agg(tablename order by tablename), array[]::text[])
    into actual_names
    from pg_tables
    where schemaname = 'public';
    if actual_names <> expected_tables then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_table_set';
    end if;

    select coalesce(array_agg(column_name order by column_name), array[]::text[])
    into actual_names
    from information_schema.columns
    where table_schema = 'public' and table_name = 'projects';
    if actual_names <> expected_project_columns then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_projects_columns';
    end if;

    foreach relation_name in array array[
        'notice_popup_receipts',
        'notice_setting_events',
        'osan_customer_assignments',
        'osan_customers',
        'osan_notification_events',
        'osan_notification_global_preferences',
        'osan_notification_preference_profiles',
        'osan_notification_preferences',
        'osan_photo_edit_requests',
        'osan_photo_revision_files',
        'osan_progress_operations',
        'osan_progress_photos',
        'osan_progress_step_photos',
        'osan_project_completion_notifications',
        'osan_project_create_operations',
        'osan_project_events',
        'osan_project_management_history',
        'osan_project_target_steps',
        'osan_project_targets',
        'osan_stage_issues',
        'osan_stage_records',
        'osan_stage_work_request_recipients',
        'osan_stage_work_requests'
    ]::text[]
    loop
        execute format('select exists(select 1 from %I limit 1)', relation_name) into has_rows;
        if has_rows then
            raise exception using errcode = 'P0001',
                message = 'business_schema_unexpected_retired_table_data', detail = relation_name;
        end if;
    end loop;
end
$migration$;


do $migration$
begin
    if exists (
        (select id from qms_users except select user_id from osan_customer_assignment_versions)
        union all
        (select user_id from osan_customer_assignment_versions except select id from qms_users)
    ) or exists (
        select 1 from osan_customer_assignment_versions where version <> 1
    ) then
        raise exception using errcode = 'P0001',
            message = 'business_schema_unexpected_retired_table_data',
            detail = 'osan_customer_assignment_versions';
    end if;

    if (select count(*) from osan_gate_configuration where id = 1 and version = 1) <> 1
       or (select count(*) from osan_gate_configuration) <> 1 then
        raise exception using errcode = 'P0001',
            message = 'business_schema_unexpected_retired_table_data',
            detail = 'osan_gate_configuration';
    end if;

    if exists (
        (select stage_sequence, department_id from osan_gate_departments
         except
         select stage.stage_sequence::smallint, department.id
         from generate_series(1, 7) stage(stage_sequence)
         cross join departments department
         where department.code in ('manufacturing', 'quality'))
        union all
        (select stage.stage_sequence::smallint, department.id
         from generate_series(1, 7) stage(stage_sequence)
         cross join departments department
         where department.code in ('manufacturing', 'quality')
         except
         select stage_sequence, department_id from osan_gate_departments)
    ) then
        raise exception using errcode = 'P0001',
            message = 'business_schema_unexpected_retired_table_data',
            detail = 'osan_gate_departments';
    end if;

    if (select count(*) from osan_notification_global_preference_profiles
        where scope_id = 1 and version = 0 and created_at_utc = updated_at_utc) <> 1
       or (select count(*) from osan_notification_global_preference_profiles) <> 1 then
        raise exception using errcode = 'P0001',
            message = 'business_schema_unexpected_retired_table_data',
            detail = 'osan_notification_global_preference_profiles';
    end if;
end
$migration$;

do $migration$
begin
    if exists (
        select 1 from projects
        where project_profile <> 'Cheongju'
           or osan_po_number is not null
           or osan_work_order_number is not null
           or osan_product_name is not null
           or osan_quantity is not null
           or osan_delivery_hold
           or osan_customer_id is not null
    ) then
        raise exception using errcode = 'P0001', message = 'cheongju_projects_contain_osan_data';
    end if;
end
$migration$;

-- These retained-table triggers are the only live Cheongju dependencies on Osan-only tables.
drop trigger if exists trg_osan_assign_existing_customers_to_new_user on qms_users;
drop function if exists osan_assign_existing_customers_to_new_user();
drop trigger if exists trg_prevent_osan_external_notification_delivery on notification_deliveries;
drop function if exists prevent_osan_external_notification_delivery();

drop view if exists osan_current_progress_photos;
drop view if exists osan_all_progress_photos;
drop view if exists osan_active_project_target_steps;
drop view if exists osan_active_project_targets;

alter table projects
    drop constraint if exists ck_projects_project_profile,
    drop constraint if exists ck_projects_osan_registration_fields;
drop index if exists ix_projects_osan_project_code;
drop index if exists ux_projects_osan_project_code;
drop index if exists ix_projects_osan_customer_id;
drop index if exists ux_projects_project_title_normalized_active;
create unique index ux_projects_project_title_normalized_active
    on projects(project_title_normalized)
    where project_title_normalized is not null and deleted_at_utc is null;

alter table projects
    drop column project_profile,
    drop column osan_po_number,
    drop column osan_work_order_number,
    drop column osan_product_name,
    drop column osan_quantity,
    drop column osan_delivery_hold,
    drop column osan_customer_id;

drop table
    osan_customer_assignment_versions,
    osan_customer_assignments,
    osan_customers,
    osan_gate_configuration,
    osan_gate_departments,
    osan_project_create_operations,
    osan_project_events,
    osan_project_management_history,
    osan_project_target_steps,
    osan_project_targets,
    osan_photo_edit_requests,
    osan_photo_revision_files,
    osan_progress_operations,
    osan_progress_photos,
    osan_progress_step_photos,
    osan_stage_issues,
    osan_stage_records,
    osan_stage_work_request_recipients,
    osan_stage_work_requests,
    osan_notification_events,
    osan_notification_global_preference_profiles,
    osan_notification_global_preferences,
    osan_project_completion_notifications,
    notice_popup_receipts,
    notice_setting_events,
    osan_notification_preference_profiles,
    osan_notification_preferences;

-- All callers belonged to the retired Osan progress/history tables.
drop function public.guard_osan_progress_append_only() restrict;

do $migration$
begin
    if exists (
        select 1 from pg_proc
        where pronamespace = 'public'::regnamespace
          and proname = 'guard_osan_progress_append_only'
    ) then
        raise exception using errcode = 'P0001', message = 'cheongju_retired_functions_remain';
    end if;
end
$migration$;

do $migration$
declare
    expected_tables text[] := array[
        'admin_master_change_logs',
        'audit_coverage_state',
        'audit_event_changes',
        'audit_events',
        'authorization_audit_events',
        'busbar_audit',
        'busbar_bom_lines',
        'busbar_boms',
        'busbar_detached_pages',
        'busbar_ecount_attempts',
        'busbar_ecount_employees',
        'busbar_ecount_jobs',
        'busbar_ecount_runtime',
        'busbar_label_events',
        'busbar_label_requests',
        'busbar_ledger',
        'busbar_master_access',
        'busbar_materials',
        'busbar_operations',
        'busbar_photo_history',
        'busbar_photos',
        'busbar_plans',
        'busbar_product_families',
        'busbar_product_qr',
        'busbar_products',
        'busbar_projects',
        'busbar_publication_recovery',
        'busbar_purchases',
        'busbar_receipts',
        'busbar_settings',
        'busbar_shipment_products',
        'busbar_shipments',
        'busbar_stock',
        'busbar_workers',
        'data_export_events',
        'departments',
        'deployment_maintenance',
        'deployment_maintenance_popup_receipts',
        'form_template_audit_events',
        'form_template_manager_bindings',
        'g2_daily_metrics',
        'g2_defect_inventory_counts',
        'g2_inventory_counts',
        'g2_targets',
        'iqc_report_pdf_artifacts',
        'iqc_report_photos',
        'iqc_report_responses',
        'iqc_report_template_items',
        'iqc_report_template_versions',
        'iqc_report_templates',
        'iqc_reports',
        'logistics_batch_panels',
        'logistics_batch_units',
        'logistics_batches',
        'logistics_delivery_results',
        'logistics_evidence',
        'logistics_operations',
        'logistics_packing_unit_panels',
        'logistics_packing_units',
        'lqc_item_setting_audit_events',
        'lqc_item_settings',
        'manufacturing_step_template_items',
        'manufacturing_step_template_versions',
        'manufacturing_step_templates',
        'material_categories',
        'material_category_audit_events',
        'material_category_iqc_setting_audit_events',
        'material_category_iqc_settings',
        'material_iqc_attempts',
        'material_iqc_scan_attachments',
        'material_iqc_scan_reports',
        'material_receipt_events',
        'material_receipts',
        'notice_attachments',
        'notice_post_revisions',
        'notice_posts',
        'notice_reads',
        'notification_deliveries',
        'notification_delivery_attempts',
        'notification_delivery_reprocess_events',
        'notification_recipients',
        'notifications',
        'panel_information_excel_import_batches',
        'panel_kitting_batches',
        'panel_kitting_completions',
        'panel_manufacturing_assembly_batch_operations',
        'panel_manufacturing_completion_confirmations',
        'panel_manufacturing_events',
        'panel_manufacturing_execution_steps',
        'panel_manufacturing_executions',
        'panel_manufacturing_operations',
        'panel_manufacturing_release_operations',
        'panel_placeholders',
        'panel_qr_codes',
        'panel_qr_events',
        'panel_quality_inspection_attempts',
        'panel_quality_operations',
        'panel_quality_report_pdf_artifacts',
        'panel_quality_report_photos',
        'panel_quality_report_responses',
        'panel_quality_reports',
        'panel_quality_template_items',
        'panel_quality_template_versions',
        'pending_action_photos',
        'pending_comments',
        'pending_history',
        'pending_issue_type_audit_events',
        'pending_issue_type_catalog',
        'pending_issues',
        'pending_photo_operations',
        'permissions',
        'procurement_excel_import_batch_projects',
        'procurement_excel_import_batches',
        'procurement_required_item_template_rows',
        'procurement_required_item_templates',
        'production_control_manufacturing_items',
        'production_control_manufacturing_templates',
        'production_control_manufacturing_versions',
        'production_control_plan_connections',
        'production_control_plan_items',
        'production_control_plan_templates',
        'production_control_plan_versions',
        'production_plan_template_audit_events',
        'production_plan_template_steps',
        'production_plan_templates',
        'production_planning_excel_import_batches',
        'production_product_types',
        'project_assignees',
        'project_audit_events',
        'project_manufacturing_step_snapshots',
        'project_procurement_items',
        'project_production_plan_connections',
        'project_production_plan_items',
        'project_production_plan_set_default_values',
        'project_production_plan_set_defaults',
        'project_production_plan_set_item_values',
        'project_production_plan_set_scopes',
        'project_production_plans',
        'project_workflow_events',
        'projects',
        'qms_database_identity',
        'qms_users',
        'role_permissions',
        'roles',
        'sales_billing_request_batches',
        'sales_billing_request_download_events',
        'sales_billing_request_items',
        'sales_billing_request_operations',
        'sales_monthly_billing_confirmations',
        'sales_monthly_billing_ledgers',
        'sales_monthly_billing_operations',
        'sales_monthly_billing_revision_cases',
        'sales_monthly_billing_revision_panels',
        'sales_monthly_billing_revisions',
        'sales_monthly_target_audit_events',
        'sales_monthly_targets',
        'sales_settlement_operations',
        'sales_settlements',
        'schema_migrations',
        'site_access_coverage_state',
        'site_access_sessions',
        'system_holidays',
        'ul891_recovery_case_events',
        'ul891_recovery_cases',
        'ul891_set_design_slots',
        'ul891_set_instances',
        'ul891_set_operations',
        'ul891_set_spec_components',
        'ul891_set_spec_versions',
        'ul891_set_specs',
        'user_notification_preference_audit_events',
        'user_notification_preference_profiles',
        'user_notification_preferences',
        'user_profile_photo_audit_events',
        'user_profile_photos',
        'user_project_access',
        'user_roles',
        'web_push_subscription_events',
        'web_push_subscriptions',
        'work_item_escalations',
        'work_items',
        'workflow_stages'
    ]::text[];
    expected_project_columns text[] := array[
        'cancelled_at_utc',
        'cancelled_by_user_id',
        'completed_at_utc',
        'completed_by_user_id',
        'created_at_utc',
        'created_by_user_id',
        'currency_code',
        'customer_name',
        'delete_reason',
        'deleted_at_utc',
        'deleted_by_user_id',
        'deleted_correlation_id',
        'delivery_date',
        'delivery_location',
        'fat_required',
        'held_at_utc',
        'held_by_user_id',
        'id',
        'iqc_routing_policy',
        'item',
        'lqc_operational_snapshot',
        'lqc_template_version_id',
        'lse_task_number',
        'name',
        'packaging_method',
        'project_code',
        'project_key',
        'project_number',
        'project_title',
        'project_title_normalized',
        'sales_amount',
        'sales_owner_user_id',
        'status',
        'status_reason',
        'structure_mode',
        'updated_at_utc'
    ]::text[];
    actual_names text[];
begin
    select coalesce(array_agg(tablename order by tablename), array[]::text[])
    into actual_names
    from pg_tables
    where schemaname = 'public';
    if actual_names <> expected_tables then
        raise exception using errcode = 'P0001', message = 'business_schema_target_table_set_mismatch';
    end if;

    select coalesce(array_agg(column_name order by column_name), array[]::text[])
    into actual_names
    from information_schema.columns
    where table_schema = 'public' and table_name = 'projects';
    if actual_names <> expected_project_columns then
        raise exception using errcode = 'P0001', message = 'business_schema_target_projects_columns_mismatch';
    end if;
end
$migration$;
