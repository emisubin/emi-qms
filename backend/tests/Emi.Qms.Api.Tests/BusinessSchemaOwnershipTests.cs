using Emi.Qms.Api.BusinessUnits;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    // Ownership reviewed against common 0001..0130 trigger/caller definitions.
    private static readonly string[] CheongjuOnlyFunctionNames =
    [
        "busbar_guard_final_photo",
        "busbar_prepare_panel_qr",
        "guard_append_only_form_template_audit",
        "guard_append_only_logistics_operation",
        "guard_append_only_pending_issue_type_audit",
        "guard_append_only_sales_monthly_target_audit",
        "guard_append_only_sales_settlement_operation",
        "guard_completed_sales_settlement",
        "guard_finalized_iqc_report_children",
        "guard_finalized_iqc_report_core",
        "guard_finalized_logistics_child",
        "guard_finalized_logistics_owner",
        "guard_finalized_material_iqc_scan_attachment",
        "guard_finalized_material_iqc_scan_report",
        "guard_finalized_panel_quality_report_children",
        "guard_finalized_panel_quality_report_core",
        "guard_form_template_version_lifecycle",
        "guard_iqc_report_pdf_artifact_immutable",
        "guard_iqc_template_item_mutation",
        "guard_lqc_item_setting_audit_append_only",
        "guard_manufacturing_template_item_mutation",
        "guard_material_category_audit_append_only",
        "guard_material_category_iqc_projection_write",
        "guard_material_category_iqc_setting_audit_append_only",
        "guard_material_receipt_projection_write",
        "guard_panel_qr_event_append_only",
        "guard_panel_quality_pdf_artifact_immutable",
        "guard_panel_quality_template_item_mutation",
        "guard_pending_action_photo_evidence",
        "guard_pending_issue_type_catalog",
        "guard_pending_photo_operation_append_only",
        "guard_pending_project_lifecycle",
        "guard_sales_billing_request_append_only",
        "guard_ul891_append_only",
        "guard_ul891_component_immutability",
        "guard_ul891_spec_version_immutability",
        "qms_end_site_access",
        "qms_record_site_access",
        "qms_site_access_guard_updates",
        "qms_site_access_menu_codes_valid",
        "sync_material_category_iqc_projection",
    ];
    private static readonly string[] OsanOnlyFunctionNames =
    [
        "guard_osan_progress_append_only",
        "osan_assign_existing_customers_to_new_user",
        "prevent_osan_external_notification_delivery",
    ];
    private static readonly string[] DirectoryOwnedTables =
        "directory_business_unit_memberships,directory_business_units,directory_identities,directory_membership_audit_events,directory_overall_administrators,directory_user_access_operations,qms_database_identity,schema_migrations".Split(',');
    private static readonly string[] DirectoryFunctionSignatures =
    [
        "begin_directory_user_access_operation(uuid,uuid,text[],uuid,bigint,text,jsonb)",
        "begin_directory_user_access_operation(uuid,uuid,text[],uuid,bigint,text,jsonb,boolean)",
        "mark_directory_user_access_retry_required(uuid,uuid,text)",
        "prevent_directory_audit_mutation()",
        "publish_directory_user_access_operation(uuid,uuid)",
        "register_or_update_pending_entra_directory_identity(uuid,text,text,text)",
        "set_directory_business_unit_memberships(uuid,uuid,text[],uuid)",
    ];
    private static readonly string[] CheongjuSequences =
    [
        "audit_event_changes_id_seq",
        "busbar_product_number_seq",
        "pending_issue_number_seq",
        "sales_billing_request_batches_request_number_seq",
    ];
    private static readonly string[] OsanSequences = ["audit_event_changes_id_seq"];
    private static readonly string[] OsanViews =
    [
        "osan_active_project_target_steps",
        "osan_active_project_targets",
        "osan_all_progress_photos",
        "osan_current_progress_photos",
    ];

    // Approved 2026-09-29 ownership and column lists; not read from migration SQL.
    private static readonly string[] CheongjuOwnedTables = "admin_master_change_logs,audit_coverage_state,audit_event_changes,audit_events,authorization_audit_events,busbar_audit,busbar_bom_lines,busbar_boms,busbar_detached_pages,busbar_ecount_attempts,busbar_ecount_employees,busbar_ecount_jobs,busbar_ecount_runtime,busbar_label_events,busbar_label_requests,busbar_ledger,busbar_master_access,busbar_materials,busbar_operations,busbar_photo_history,busbar_photos,busbar_plans,busbar_product_families,busbar_product_qr,busbar_products,busbar_projects,busbar_publication_recovery,busbar_purchases,busbar_receipts,busbar_settings,busbar_shipment_products,busbar_shipments,busbar_stock,busbar_workers,data_export_events,departments,deployment_maintenance,deployment_maintenance_popup_receipts,form_template_audit_events,form_template_manager_bindings,g2_daily_metrics,g2_defect_inventory_counts,g2_inventory_counts,g2_targets,iqc_report_pdf_artifacts,iqc_report_photos,iqc_report_responses,iqc_report_template_items,iqc_report_template_versions,iqc_report_templates,iqc_reports,logistics_batch_panels,logistics_batch_units,logistics_batches,logistics_delivery_results,logistics_evidence,logistics_operations,logistics_packing_unit_panels,logistics_packing_units,lqc_item_setting_audit_events,lqc_item_settings,manufacturing_step_template_items,manufacturing_step_template_versions,manufacturing_step_templates,material_categories,material_category_audit_events,material_category_iqc_setting_audit_events,material_category_iqc_settings,material_iqc_attempts,material_iqc_scan_attachments,material_iqc_scan_reports,material_receipt_events,material_receipts,notice_attachments,notice_post_revisions,notice_posts,notice_reads,notification_deliveries,notification_delivery_attempts,notification_delivery_reprocess_events,notification_recipients,notifications,panel_information_excel_import_batches,panel_kitting_batches,panel_kitting_completions,panel_manufacturing_assembly_batch_operations,panel_manufacturing_completion_confirmations,panel_manufacturing_events,panel_manufacturing_execution_steps,panel_manufacturing_executions,panel_manufacturing_operations,panel_manufacturing_release_operations,panel_placeholders,panel_qr_codes,panel_qr_events,panel_quality_inspection_attempts,panel_quality_operations,panel_quality_report_pdf_artifacts,panel_quality_report_photos,panel_quality_report_responses,panel_quality_reports,panel_quality_template_items,panel_quality_template_versions,pending_action_photos,pending_comments,pending_history,pending_issue_type_audit_events,pending_issue_type_catalog,pending_issues,pending_photo_operations,permissions,procurement_excel_import_batch_projects,procurement_excel_import_batches,procurement_required_item_template_rows,procurement_required_item_templates,production_control_manufacturing_items,production_control_manufacturing_templates,production_control_manufacturing_versions,production_control_plan_connections,production_control_plan_items,production_control_plan_templates,production_control_plan_versions,production_plan_template_audit_events,production_plan_template_steps,production_plan_templates,production_planning_excel_import_batches,production_product_types,project_assignees,project_audit_events,project_manufacturing_step_snapshots,project_procurement_items,project_production_plan_connections,project_production_plan_items,project_production_plan_set_default_values,project_production_plan_set_defaults,project_production_plan_set_item_values,project_production_plan_set_scopes,project_production_plans,project_workflow_events,projects,qms_database_identity,qms_users,role_permissions,roles,sales_billing_request_batches,sales_billing_request_download_events,sales_billing_request_items,sales_billing_request_operations,sales_monthly_billing_confirmations,sales_monthly_billing_ledgers,sales_monthly_billing_operations,sales_monthly_billing_revision_cases,sales_monthly_billing_revision_panels,sales_monthly_billing_revisions,sales_monthly_target_audit_events,sales_monthly_targets,sales_settlement_operations,sales_settlements,schema_migrations,site_access_coverage_state,site_access_sessions,system_holidays,ul891_recovery_case_events,ul891_recovery_cases,ul891_set_design_slots,ul891_set_instances,ul891_set_operations,ul891_set_spec_components,ul891_set_spec_versions,ul891_set_specs,user_notification_preference_audit_events,user_notification_preference_profiles,user_notification_preferences,user_profile_photo_audit_events,user_profile_photos,user_project_access,user_roles,web_push_subscription_events,web_push_subscriptions,work_item_escalations,work_items,workflow_stages".Split(',');
    private static readonly string[] OsanOwnedTables = "audit_coverage_state,audit_event_changes,audit_events,authorization_audit_events,data_export_events,departments,deployment_maintenance,deployment_maintenance_popup_receipts,notice_attachments,notice_popup_receipts,notice_post_revisions,notice_posts,notice_reads,notice_setting_events,notification_deliveries,notification_delivery_attempts,notification_recipients,notifications,osan_customer_assignment_versions,osan_customer_assignments,osan_customers,osan_gate_configuration,osan_gate_departments,osan_notification_events,osan_notification_global_preference_profiles,osan_notification_global_preferences,osan_notification_preference_profiles,osan_notification_preferences,osan_photo_edit_requests,osan_photo_revision_files,osan_progress_operations,osan_progress_photos,osan_progress_step_photos,osan_project_completion_notifications,osan_project_create_operations,osan_project_events,osan_project_management_history,osan_project_target_steps,osan_project_targets,osan_stage_issues,osan_stage_records,osan_stage_work_request_recipients,osan_stage_work_requests,permissions,projects,qms_database_identity,qms_users,role_permissions,roles,schema_migrations,user_profile_photo_audit_events,user_profile_photos,user_project_access,user_roles,web_push_subscription_events,web_push_subscriptions".Split(',');
    private static readonly string[] CheongjuProjectColumns = "cancelled_at_utc,cancelled_by_user_id,completed_at_utc,completed_by_user_id,created_at_utc,created_by_user_id,currency_code,customer_name,delete_reason,deleted_at_utc,deleted_by_user_id,deleted_correlation_id,delivery_date,delivery_location,fat_required,held_at_utc,held_by_user_id,id,iqc_routing_policy,item,lqc_operational_snapshot,lqc_template_version_id,lse_task_number,name,packaging_method,project_code,project_key,project_number,project_title,project_title_normalized,sales_amount,sales_owner_user_id,status,status_reason,structure_mode,updated_at_utc".Split(',');
    private static readonly string[] OsanProjectColumns = "created_at_utc,created_by_user_id,customer_name,delete_reason,deleted_at_utc,deleted_by_user_id,delivery_date,id,osan_customer_id,osan_delivery_hold,osan_po_number,osan_product_name,osan_quantity,osan_work_order_number,project_code,project_key,project_title,status,updated_at_utc".Split(',');
    private static readonly string[] CheongjuNotificationColumns = "created_at_utc,generated_by_event_id,id,idempotency_key,link_url,manual_requested_by_user_id,message,notification_type,project_id,severity,source_kind,title,visibility_scope,work_item_id".Split(',');
    private static readonly string[] OsanNotificationColumns = "created_at_utc,id,idempotency_key,link_url,manual_requested_by_user_id,message,notification_type,project_id,severity,source_kind,title,visibility_scope".Split(',');
    private static readonly string[] CheongjuNotificationDeliveryColumns = "admin_handled_at_utc,admin_handled_by_user_id,admin_handling_note,admin_handling_status,attempt_count,channel,claim_expires_at_utc,claim_token,claimed_at_utc,claimed_by_instance_id,correlation_id,created_at_utc,current_generation,dedupe_key,delivery_type,display_channel_target,display_message,display_project_name,display_recipient_email,display_recipient_kind,display_recipient_name,display_title,display_work_item_title,error_code,error_message,generation_attempt_count,group_key,id,last_attempt_at_utc,manual_notification_kind,manual_payload_json,manual_requested_at_utc,manual_requested_by_user_id,next_attempt_at_utc,notification_id,notification_recipient_id,project_id,provider_message_id,recipient_user_id,sent_at_utc,status,suppressed_at_utc,updated_at_utc,web_push_subscription_generation,web_push_subscription_id,work_item_id".Split(',');
    private static readonly string[] OsanNotificationDeliveryColumns = CheongjuNotificationDeliveryColumns
        .Where(column => column != "work_item_id")
        .ToArray();

    private static async Task AssertApprovedSchemaAsync(IsolationDatabaseSet databases, CancellationToken ct)
    {
        var directoryTables = await databases.ReadColumnAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
            "select table_name from information_schema.tables where table_schema='public' and table_type='BASE TABLE' order by table_name", ct);
        Assert.Equal(DirectoryOwnedTables, directoryTables);
        Assert.Empty(await ReadPublicRelationNamesAsync(databases, "DIRECTORY", 'S', ct));
        Assert.Empty(await ReadPublicViewsAsync(databases, "DIRECTORY", ct));
        var directoryFunctions = await databases.ReadColumnAsync("DIRECTORY", BusinessUnitConnectionPurpose.Migration,
            "select proname || '(' || replace(oidvectortypes(proargtypes), ', ', ',') || ')' " +
            "from pg_proc where pronamespace='public'::regnamespace order by 1", ct);
        Assert.Equal(DirectoryFunctionSignatures, directoryFunctions);
        var executableDirectoryFunctions = await databases.ReadColumnAsync("DIRECTORY", BusinessUnitConnectionPurpose.Runtime,
            "select proname || '(' || replace(oidvectortypes(proargtypes), ', ', ',') || ')' " +
            "from pg_proc where pronamespace='public'::regnamespace and has_function_privilege(current_user, oid, 'execute') order by 1", ct);
        Assert.Equal(DirectoryFunctionSignatures, executableDirectoryFunctions);

        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            var functions = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Runtime,
                "select proname from pg_proc where pronamespace='public'::regnamespace order by proname", ct);
            Assert.Equal(code == BusinessUnitCodes.Osan ? [] : CheongjuOnlyFunctionNames,
                functions.Where(CheongjuOnlyFunctionNames.Contains).ToArray());
            Assert.Equal(code == BusinessUnitCodes.Osan ? OsanOnlyFunctionNames : [],
                functions.Where(OsanOnlyFunctionNames.Contains).ToArray());
            foreach (var shared in new[] { "qms_audit_capture_row_change", "qms_audit_guard_append_only",
                         "qms_audit_target_key", "qms_audit_identity_snapshot" })
                Assert.Contains(shared, functions);
            var tables = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
                "select table_name from information_schema.tables where table_schema='public' and table_type='BASE TABLE' order by table_name", ct);
            Assert.Equal(code == BusinessUnitCodes.Osan ? OsanOwnedTables : CheongjuOwnedTables, tables);
            var columns = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
                "select column_name from information_schema.columns where table_schema='public' and table_name='projects' order by column_name", ct);
            Assert.Equal(code == BusinessUnitCodes.Osan ? OsanProjectColumns : CheongjuProjectColumns, columns);
            var notificationColumns = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
                "select column_name from information_schema.columns where table_schema='public' and table_name='notifications' order by column_name", ct);
            Assert.Equal(code == BusinessUnitCodes.Osan ? OsanNotificationColumns : CheongjuNotificationColumns,
                notificationColumns);
            var deliveryColumns = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
                "select column_name from information_schema.columns where table_schema='public' and table_name='notification_deliveries' order by column_name", ct);
            Assert.Equal(code == BusinessUnitCodes.Osan
                ? OsanNotificationDeliveryColumns
                : CheongjuNotificationDeliveryColumns, deliveryColumns);

            var expectedSequences = code == BusinessUnitCodes.Osan ? OsanSequences : CheongjuSequences;
            Assert.Equal(expectedSequences, await ReadPublicRelationNamesAsync(databases, code, 'S', ct));
            var runtimeSequences = await databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Runtime,
                "select relname from pg_class where relnamespace='public'::regnamespace and relkind='S' " +
                "and has_sequence_privilege(current_user, oid, 'usage') " +
                "and has_sequence_privilege(current_user, oid, 'select') order by relname", ct);
            Assert.Equal(expectedSequences, runtimeSequences);
            Assert.Equal(code == BusinessUnitCodes.Osan ? OsanViews : [],
                await ReadPublicViewsAsync(databases, code, ct));
        }
    }

    private static Task<IReadOnlyList<string>> ReadPublicRelationNamesAsync(
        IsolationDatabaseSet databases,
        string code,
        char relationKind,
        CancellationToken ct) =>
        databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
            $"select relname from pg_class where relnamespace='public'::regnamespace and relkind='{relationKind}' order by relname", ct);

    private static Task<IReadOnlyList<string>> ReadPublicViewsAsync(
        IsolationDatabaseSet databases,
        string code,
        CancellationToken ct) =>
        databases.ReadColumnAsync(code, BusinessUnitConnectionPurpose.Migration,
            "select table_name from information_schema.views where table_schema='public' order by table_name", ct);
}
