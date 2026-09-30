-- Split the shared 0130 schema into the fixed Osan business schema.
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
    if current_setting('emi_qms.business_schema_separation', true) is distinct from 'OSAN' then
        raise exception using errcode = 'P0001', message = 'business_schema_explicit_consent_required';
    end if;

    if (select count(*) from qms_database_identity
        where singleton and database_kind = 'business' and business_unit_code = 'OSAN'
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
        'admin_master_change_logs',
        'busbar_audit',
        'busbar_bom_lines',
        'busbar_boms',
        'busbar_detached_pages',
        'busbar_ecount_attempts',
        'busbar_ecount_employees',
        'busbar_ecount_jobs',
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
        'busbar_shipment_products',
        'busbar_shipments',
        'busbar_stock',
        'busbar_workers',
        'form_template_audit_events',
        'form_template_manager_bindings',
        'g2_daily_metrics',
        'g2_defect_inventory_counts',
        'g2_inventory_counts',
        'g2_targets',
        'iqc_report_pdf_artifacts',
        'iqc_report_photos',
        'iqc_report_responses',
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
        'material_category_audit_events',
        'material_category_iqc_setting_audit_events',
        'material_iqc_attempts',
        'material_iqc_scan_attachments',
        'material_iqc_scan_reports',
        'material_receipt_events',
        'material_receipts',
        'notification_delivery_reprocess_events',
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
        'pending_action_photos',
        'pending_comments',
        'pending_history',
        'pending_issue_type_audit_events',
        'pending_issues',
        'pending_photo_operations',
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
        'production_planning_excel_import_batches',
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
        'work_item_escalations',
        'work_items'
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
    if (select count(*) from workflow_stages) <> 18 or exists (
        select 1 from workflow_stages
        where (stage_code, sequence_number, department_code, stage_name, is_optional, is_active) not in (values
            ('SalesProjectCreated', 1, 'sales', '프로젝트 생성', false, true),
            ('ProductionPlanning', 2, 'production-planning', '생산계획·담당자', false, true),
            ('DesignPanelInfo', 3, 'design', '제품명·사이즈', false, true),
            ('ProcurementInfo', 4, 'procurement', '구매정보', false, true),
            ('MaterialArrived', 5, 'materials', '자재 도착', false, true),
            ('IQC', 6, 'quality', '수입검사', false, true),
            ('ReceiptConfirmed', 7, 'materials', '입고 확정', false, true),
            ('KittingCompleted', 8, 'production-planning', '제조 요청', false, true),
            ('ManufacturingWork', 9, 'manufacturing', '제조 작업', false, true),
            ('LQC', 10, 'quality', 'LQC', false, true),
            ('ManufacturingCompleted', 11, 'manufacturing', '제조 완료', false, true),
            ('OQC', 12, 'quality', '자체검수', false, true),
            ('CustomerInspection', 13, 'quality', '전진검수', false, true),
            ('FAT', 14, 'quality', 'FAT 선택', true, true),
            ('PackingCompleted', 15, 'logistics', '포장 완료', false, true),
            ('DepartureProcessed', 16, 'logistics', '출발 처리', false, true),
            ('DeliveryCompleted', 17, 'logistics', '납품 완료', false, true),
            ('SalesSettlementCompleted', 18, 'sales', '세금계산서·완료', false, true)
        )
    ) then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'workflow_stages';
    end if;

    if (select count(*) from manufacturing_step_templates) <> 1
       or not exists (select 1 from manufacturing_step_templates
                      where id = '44000000-0000-0000-0000-000000000001'::uuid
                        and template_code = 'PANEL_MANUFACTURING' and display_name = '제조 작업 단계')
       or (select count(*) from manufacturing_step_template_versions) <> 1
       or not exists (select 1 from manufacturing_step_template_versions
                      where id = '44000000-0000-0000-0000-000000000002'::uuid
                        and template_id = '44000000-0000-0000-0000-000000000001'::uuid
                        and version_number = 1 and display_name = '제조 작업 단계 v1'
                        and lifecycle_status = 'Active' and is_active and row_version = 1
                        and activated_at_utc is not null and archived_at_utc is null
                        and created_by_user_id is null and updated_by_user_id is null)
       or (select count(*) from manufacturing_step_template_items) <> 4
       or exists (select 1 from manufacturing_step_template_items
                  where (id, template_version_id, item_code, display_order, label) not in (values
                    ('44000000-0000-0000-0000-000000000011'::uuid, '44000000-0000-0000-0000-000000000002'::uuid, 'WORK_ORDER', 1, '작업지시·도면 확인'),
                    ('44000000-0000-0000-0000-000000000012'::uuid, '44000000-0000-0000-0000-000000000002'::uuid, 'MATERIALS', 2, '자재·부품 확인'),
                    ('44000000-0000-0000-0000-000000000013'::uuid, '44000000-0000-0000-0000-000000000002'::uuid, 'MANUFACTURING', 3, '제조 작업 수행'),
                    ('44000000-0000-0000-0000-000000000014'::uuid, '44000000-0000-0000-0000-000000000002'::uuid, 'SELF_CHECK', 4, '자체 확인')
                  )) then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'manufacturing_step_templates';
    end if;

    if (select count(*) from production_product_types) <> 6
       or exists (select 1 from production_product_types
                  where (id, code, name, is_active) not in (values
                    ('31000000-0000-0000-0000-000000000067'::uuid, 'UL67', 'UL67', true),
                    ('31000000-0000-0000-0000-000000000891'::uuid, 'UL891', 'UL891', true),
                    ('31000000-0000-0000-0000-00000000508a'::uuid, 'UL508A', 'UL508A', true),
                    ('31000000-0000-0000-0000-0000000001ec'::uuid, 'IEC', 'IEC', true),
                    ('31000000-0000-0000-0000-000000000112'::uuid, 'LLP', 'LLP', true),
                    ('31000000-0000-0000-0000-000000000772'::uuid, 'RPP', 'RPP', true)
                  ))
       or (select count(*) from production_plan_templates) <> 6
       or exists (select 1 from production_plan_templates template
                  join production_product_types product on product.id = template.product_type_id
                  where template.id <> replace(product.id::text, '31000000-', '32000000-')::uuid
                     or template.version <> 1 or not template.is_active)
       or (select count(*) from production_plan_template_steps) <> 24
       or exists (select 1 from production_plan_template_steps step
                  where step.template_id not in (select id from production_plan_templates)
                     or not step.is_required or not step.is_active
                     or (step.sequence_number, step.step_name) not in (values
                        (1, '자재 입고'), (2, '조립 시작'), (3, '배선'), (4, '검사 준비')))
       or exists (select 1 from production_plan_templates template
                  where (select count(*) from production_plan_template_steps step where step.template_id = template.id) <> 4) then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'production_plan_templates';
    end if;

    if (select count(*) from material_categories) <> 5
       or exists (select 1 from material_categories
                  where row_version <> 1 or created_by_user_id is not null or updated_by_user_id is not null
                     or (id, code, display_name, requires_iqc, is_active, display_order) not in (values
                        ('67000000-0000-0000-0000-000000000001'::uuid, 'ENCLOSURE', '외함', true, true, 10),
                        ('67000000-0000-0000-0000-000000000002'::uuid, 'SHEET_METAL', '판금류', false, true, 20),
                        ('67000000-0000-0000-0000-000000000003'::uuid, 'BUSBAR', '부스바', false, true, 30),
                        ('67000000-0000-0000-0000-000000000004'::uuid, 'NAMEPLATE', '명판', false, true, 40),
                        ('67000000-0000-0000-0000-000000000005'::uuid, 'OTHER', '기타', false, true, 50)))
       or (select count(*) from material_category_iqc_settings) <> 5
       or exists (select 1
                  from material_category_iqc_settings setting
                  join material_categories category on category.id = setting.material_category_id
                  join iqc_report_template_versions version on version.id = setting.current_template_version_id
                  join iqc_report_templates template on template.id = version.template_id
                  where setting.is_enabled is distinct from category.requires_iqc
                     or setting.decision_mode <> 'ScanBased' or setting.row_version <> 1
                     or setting.updated_by_user_id is not null
                     or template.material_category_id is distinct from category.id
                     or version.version_number <> 1 or version.lifecycle_status <> 'Active'
                     or not version.is_active)
    then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'material_categories';
    end if;

    if (select count(*) from iqc_report_templates) <> 6
       or not exists (select 1 from iqc_report_templates
                      where id = '92000000-0000-0000-0000-000000000001'::uuid
                        and template_code = 'MATERIAL_IQC' and display_name = '자재 수입검사 기본 양식'
                        and is_system and material_category_id is null)
       or exists (select 1 from material_categories category
                  where not exists (select 1 from iqc_report_templates template
                                    where template.material_category_id = category.id
                                      and template.template_code = 'CATEGORY_IQC_' || category.code
                                      and template.display_name = category.display_name || ' 수입검사'
                                      and not template.is_system))
       or (select count(*) from iqc_report_template_versions) <> 6
       or exists (select 1 from iqc_report_templates template
                  where (select count(*) from iqc_report_template_versions version
                         where version.template_id = template.id and version.version_number = 1
                           and version.lifecycle_status = 'Active' and version.is_active
                           and version.row_version = 1 and version.activated_at_utc is not null
                           and version.archived_at_utc is null and version.created_by_user_id is null
                           and version.updated_by_user_id is null) <> 1)
       or (select count(*) from iqc_report_template_items) <> 6
       or exists (select 1 from iqc_report_template_items item
                  where item.template_version_id <> '92000000-0000-0000-0000-000000000101'::uuid
                     or item.definition_key <> item.id
                     or item.guidance is null
                     or item.max_text_length is distinct from case when item.item_code = 'NOTES' then 1000 else null end
                     or (item.id, item.label, item.guidance) not in (values
                        ('92000000-0000-0000-0000-000000000201'::uuid, '품명·규격이 발주 정보와 일치', '발주 품명과 입고품 표시를 대조해 주세요.'),
                        ('92000000-0000-0000-0000-000000000202'::uuid, '도착 수량이 등록 수량과 일치', '포장 단위와 등록 수량을 함께 확인해 주세요.'),
                        ('92000000-0000-0000-0000-000000000203'::uuid, '외관 손상·오염 없음', '찍힘, 긁힘, 오염과 포장 손상을 확인해 주세요.'),
                        ('92000000-0000-0000-0000-000000000204'::uuid, '식별 표시(라벨·명판) 확인', '라벨, 명판과 lot 식별 가능 여부를 확인해 주세요.'),
                        ('92000000-0000-0000-0000-000000000205'::uuid, '외함 상태 확인', '외함 전체 상태를 확인하고 증빙 사진을 등록해 주세요.'),
                        ('92000000-0000-0000-0000-000000000206'::uuid, '측정값·특이사항', '측정값이나 추가로 남길 내용을 입력해 주세요.'))
                     or (item.id, item.item_code, item.display_order, item.response_type, item.is_required, item.requires_photo) not in (values
                        ('92000000-0000-0000-0000-000000000201'::uuid, 'ITEM_SPEC', 1, 'Check', true, false),
                        ('92000000-0000-0000-0000-000000000202'::uuid, 'ARRIVAL_QUANTITY', 2, 'Check', true, false),
                        ('92000000-0000-0000-0000-000000000203'::uuid, 'APPEARANCE', 3, 'Check', true, false),
                        ('92000000-0000-0000-0000-000000000204'::uuid, 'IDENTIFICATION', 4, 'Check', true, false),
                        ('92000000-0000-0000-0000-000000000205'::uuid, 'ENCLOSURE', 5, 'Check', true, true),
                        ('92000000-0000-0000-0000-000000000206'::uuid, 'NOTES', 6, 'Text', false, false)))
    then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'iqc_report_templates';
    end if;

    if (select count(*) from panel_quality_template_versions) <> 10
       or exists (select 1 from panel_quality_template_versions version
                  where version.lifecycle_status <> 'Active' or not version.is_active
                     or version.version_number <> 1 or version.activated_at_utc is null
                     or version.archived_at_utc is not null
                     or version.created_by_user_id is not null or version.updated_by_user_id is not null)
       or exists (select 1 from production_product_types product
                  where not exists (select 1 from panel_quality_template_versions version
                                    where version.product_type_id = product.id and version.stage_code = 'LQC'
                                      and version.display_name = product.name || ' LQC 검사'
                                      and version.row_version = 2))
       or (select count(*) from panel_quality_template_versions where product_type_id is null) <> 4
       or exists (select 1 from panel_quality_template_versions
                  where product_type_id is null and
                    (id, stage_code, display_name, row_version) not in (values
                      ('93000000-0000-0000-0000-000000000101'::uuid, 'LQC', 'LQC 기본 검사', 1),
                      ('93000000-0000-0000-0000-000000000102'::uuid, 'OQC', 'OQC 자체검수', 1),
                      ('93000000-0000-0000-0000-000000000103'::uuid, 'CustomerInspection', '전진검수', 1),
                      ('93000000-0000-0000-0000-000000000104'::uuid, 'FAT', 'FAT 시험검사', 1)))
       or (select count(*) from panel_quality_template_items) <> 48
       or exists (select 1 from panel_quality_template_items item
                  where item.template_version_id in (
                            '93000000-0000-0000-0000-000000000101'::uuid,
                            '93000000-0000-0000-0000-000000000102'::uuid,
                            '93000000-0000-0000-0000-000000000103'::uuid,
                            '93000000-0000-0000-0000-000000000104'::uuid)
                    and (item.definition_key <> item.id
                         or item.guidance is null
                         or item.max_text_length is distinct from case when item.response_type = 'Text' then 1000 else null end
                         or (item.id, item.template_version_id, item.item_code, item.display_order,
                             item.label, item.guidance, item.response_type, item.is_required, item.requires_photo) not in (values
                            ('93000000-0000-0000-0000-000000000201'::uuid, '93000000-0000-0000-0000-000000000101'::uuid, 'DRAWING_STANDARD', 1, '도면·작업기준 일치', '도면과 작업 기준을 대조해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000202'::uuid, '93000000-0000-0000-0000-000000000101'::uuid, 'ASSEMBLY', 2, '조립 상태', '부품 조립과 고정 상태를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000203'::uuid, '93000000-0000-0000-0000-000000000101'::uuid, 'WIRING_FASTENING', 3, '배선·체결', '배선 정리와 체결 상태를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000204'::uuid, '93000000-0000-0000-0000-000000000101'::uuid, 'MARKING_FINISH', 4, '표시·마감', '명판, 표시와 마감 상태를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000205'::uuid, '93000000-0000-0000-0000-000000000101'::uuid, 'NOTES', 5, '추가 메모', '특이사항이 있으면 기록해 주세요.', 'Text', false, false),
                            ('93000000-0000-0000-0000-000000000211'::uuid, '93000000-0000-0000-0000-000000000102'::uuid, 'APPEARANCE_MARKING', 1, '외관·표시', '외관 손상과 식별 표시를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000212'::uuid, '93000000-0000-0000-0000-000000000102'::uuid, 'FUNCTION_CIRCUIT', 2, '기능·회로', '회로와 주요 기능을 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000213'::uuid, '93000000-0000-0000-0000-000000000102'::uuid, 'CONFIG_DIMENSION', 3, '구성·치수', '구성과 주요 치수를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000214'::uuid, '93000000-0000-0000-0000-000000000102'::uuid, 'SHIP_READY', 4, '출하 준비', '보호와 출하 준비 상태를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000215'::uuid, '93000000-0000-0000-0000-000000000102'::uuid, 'NOTES', 5, '추가 메모', '특이사항이 있으면 기록해 주세요.', 'Text', false, false),
                            ('93000000-0000-0000-0000-000000000221'::uuid, '93000000-0000-0000-0000-000000000103'::uuid, 'INSPECTION_SCOPE', 1, '검사 범위 확인', '고객과 합의한 검사 범위를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000222'::uuid, '93000000-0000-0000-0000-000000000103'::uuid, 'PUNCH_CLEAR', 2, '지적·PUNCH 여부', '남은 지적사항이 없는지 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000223'::uuid, '93000000-0000-0000-0000-000000000103'::uuid, 'CUSTOMER_NOTE', 3, '고객 확인 메모', '확인 내용과 특이사항을 기록해 주세요.', 'Text', false, false),
                            ('93000000-0000-0000-0000-000000000231'::uuid, '93000000-0000-0000-0000-000000000104'::uuid, 'TEST_SCOPE', 1, '시험 범위', '승인된 FAT 시험 범위를 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000232'::uuid, '93000000-0000-0000-0000-000000000104'::uuid, 'TEST_RESULT', 2, '시험 결과', '주요 시험 결과가 기준에 맞는지 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000233'::uuid, '93000000-0000-0000-0000-000000000104'::uuid, 'CUSTOMER_CONFIRM', 3, '고객 확인', '고객 확인 여부를 기록해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000234'::uuid, '93000000-0000-0000-0000-000000000104'::uuid, 'PUNCH_CLEAR', 4, 'PUNCH 여부', '남은 지적사항이 없는지 확인해 주세요.', 'Check', true, false),
                            ('93000000-0000-0000-0000-000000000235'::uuid, '93000000-0000-0000-0000-000000000104'::uuid, 'NOTES', 5, '추가 메모', '시험 특이사항이 있으면 기록해 주세요.', 'Text', false, false))))
       or exists (select 1 from panel_quality_template_versions version
                  where version.product_type_id is not null and
                    (select count(*) from panel_quality_template_items clone
                     join panel_quality_template_items source
                       on source.template_version_id = '93000000-0000-0000-0000-000000000101'::uuid
                      and source.item_code = clone.item_code
                      and (source.display_order, source.label, source.guidance, source.response_type,
                           source.is_required, source.requires_photo, source.max_text_length, source.definition_key)
                          is not distinct from
                          (clone.display_order, clone.label, clone.guidance, clone.response_type,
                           clone.is_required, clone.requires_photo, clone.max_text_length, clone.definition_key)
                     where clone.template_version_id = version.id) <> 5)
       or (select count(*) from lqc_item_settings) <> 6
       or exists (select 1 from lqc_item_settings setting
                  join panel_quality_template_versions version on version.id = setting.current_template_version_id
                  where version.product_type_id is distinct from setting.product_type_id
                     or version.stage_code <> 'LQC' or version.lifecycle_status <> 'Active'
                     or not version.is_active or setting.is_operational or setting.row_version <> 1
                     or setting.updated_by_user_id is not null)
    then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'panel_quality_templates';
    end if;

    if (select count(*) from pending_issue_type_catalog) <> 4
       or exists (select 1 from pending_issue_type_catalog
                  where row_version <> 1
                     or description is distinct from case code
                          when 'Nonconformance' then '검사 및 자재 품질 기준 미달'
                          when 'Punch' then '고객 검수 및 입회 검사 지적'
                          when 'ManufacturingStop' then '제조 작업을 차단하는 현장 이슈'
                          when 'Other' then '수동 등록을 위한 기본 유형' end
                     or (code, display_name, sort_order, is_system, is_manual_enabled, is_active) not in (values
                        ('Nonconformance', '부적합', 1, true, true, true),
                        ('Punch', 'PUNCH', 2, true, true, true),
                        ('ManufacturingStop', '제조 중단', 3, true, true, true),
                        ('Other', '기타', 4, true, true, true)))
       or (select count(*) from busbar_ecount_runtime
           where singleton and company_code is null and environment is null and not paused
             and message is null and last_login_at_utc is null and next_send_at_utc is null
             and consecutive_failures = 0) <> 1
       or (select count(*) from busbar_ecount_runtime) <> 1
       or (select count(*) from busbar_settings
           where singleton and common_project_code = '' and ecount_customer_code = '' and ecount_warehouse_code = '') <> 1
       or (select count(*) from busbar_settings) <> 1
       or (select count(*) from site_access_coverage_state where singleton) <> 1
       or (select count(*) from site_access_coverage_state) <> 1
    then
        raise exception using errcode = 'P0001', message = 'business_schema_unexpected_retired_table_data', detail = 'fixed_business_seed_state';
    end if;
end
$migration$;

do $migration$
begin
    if exists (
        select 1 from projects
        where project_profile <> 'Osan'
           or project_number is distinct from project_code
           or name is distinct from project_title
           -- OsanProjectStore writes NULL; normalization belongs to Cheongju only.
           or project_title_normalized is not null
           or coalesce(item, '') <> ''
           or sales_owner_user_id is not null
           or sales_amount is not null
           or currency_code is not null
           or delivery_location is not null
           or status_reason is not null
           or held_by_user_id is not null
           or held_at_utc is not null
           or cancelled_by_user_id is not null
           or cancelled_at_utc is not null
           or packaging_method is not null
           or deleted_correlation_id is not null
           or fat_required
           or completed_by_user_id is not null
           or completed_at_utc is not null
           or structure_mode is not null
           or iqc_routing_policy is distinct from 'AllReceipts'
           or lqc_operational_snapshot is distinct from true
           or lqc_template_version_id is distinct from '93000000-0000-0000-0000-000000000101'::uuid
           or lse_task_number is not null
    ) then
        raise exception using errcode = 'P0001', message = 'osan_projects_contain_unpreserved_cheongju_data';
    end if;

    if exists (select 1 from notifications where work_item_id is not null or generated_by_event_id is not null)
       or exists (select 1 from notification_deliveries where work_item_id is not null) then
        raise exception using errcode = 'P0001', message = 'osan_notifications_contain_cheongju_links';
    end if;
end
$migration$;

create temporary table business_schema_preserved_preferences
on commit drop
as
select
    coalesce((select jsonb_agg(to_jsonb(profile) order by profile.user_id)
              from osan_notification_preference_profiles profile), '[]'::jsonb) as profiles,
    coalesce((select jsonb_agg(to_jsonb(preference)
                              order by preference.user_id, preference.event_kind,
                                       preference.channel, preference.stage_sequence)
              from osan_notification_preferences preference), '[]'::jsonb) as preferences;

create temporary table business_schema_preserved_manual_payloads
on commit drop
as
select id, manual_payload_json
from notification_deliveries;

drop trigger if exists trg_guard_project_iqc_routing_policy_immutable on projects;
drop function if exists guard_project_iqc_routing_policy_immutable();
drop trigger if exists trg_guard_project_lqc_snapshot_immutable on projects;
drop function if exists guard_project_lqc_snapshot_immutable();

drop index if exists ux_projects_project_title_normalized_active;
drop index if exists ix_projects_sales_owner_user_id;
drop index if exists ix_projects_osan_project_code;
drop index if exists ux_projects_osan_project_code;
drop index if exists ix_projects_osan_customer_id;

alter table projects
    drop constraint if exists ck_projects_project_profile,
    drop constraint if exists ck_projects_osan_registration_fields,
    drop constraint if exists ck_projects_sales_amount_non_negative,
    drop constraint if exists ck_projects_currency_code_iso4217,
    drop constraint if exists ck_projects_packaging_method,
    drop constraint if exists ck_projects_completion_metadata,
    drop constraint if exists ck_projects_structure_mode,
    drop constraint if exists ck_projects_iqc_routing_policy,
    drop constraint if exists ck_projects_lse_task_number,
    drop constraint if exists ck_projects_status;

alter table notifications
    drop constraint if exists notifications_work_item_id_fkey,
    drop constraint if exists notifications_generated_by_event_id_fkey;
drop index if exists ix_notifications_work_item;
alter table notifications
    drop column work_item_id,
    drop column generated_by_event_id;

alter table notification_deliveries
    drop constraint if exists notification_deliveries_work_item_id_fkey,
    drop column work_item_id;

-- The protection remains BEFORE INSERT and keeps every recipient, permission,
-- subscription-generation and manual-mail payload condition. The selected database
-- identity replaces the retired project_profile predicate.
create or replace function prevent_osan_external_notification_delivery()
returns trigger language plpgsql as $function$
begin
    if not exists (
        select 1 from qms_database_identity
        where singleton and database_kind = 'business' and business_unit_code = 'OSAN'
    ) then
        raise exception using errcode = '42501', message = 'business_schema_identity_mismatch';
    end if;

    if not (
        (new.channel = 'Mail' and new.delivery_type = 'OsanWorkflow' and exists (
            select 1 from notifications notification
            join projects project on project.id = notification.project_id
            join notification_recipients recipient on recipient.notification_id = notification.id
            where notification.id = new.notification_id
              and notification.source_kind = 'OsanWorkflow'
              and notification.project_id = new.project_id
              and recipient.id = new.notification_recipient_id
              and recipient.user_id = new.recipient_user_id
        ))
        or (new.channel = 'WebPush' and new.delivery_type = 'WebPushNotification' and exists (
            select 1 from notifications notification
            join projects project on project.id = notification.project_id
            join notification_recipients recipient on recipient.notification_id = notification.id
            join qms_users user_account on user_account.id = recipient.user_id and user_account.is_active
            join web_push_subscriptions subscription
              on subscription.user_id = user_account.id and subscription.is_active
            where notification.id = new.notification_id
              and notification.source_kind = 'OsanWorkflow'
              and notification.visibility_scope = 'RecipientOnly'
              and notification.project_id = new.project_id
              and recipient.id = new.notification_recipient_id
              and recipient.user_id = new.recipient_user_id
              and subscription.id = new.web_push_subscription_id
              and subscription.generation = new.web_push_subscription_generation
              and subscription.activated_at_utc <= notification.created_at_utc
              and (user_account.auth_provider <> 'EntraId' or exists (
                  select 1 from user_roles approved where approved.user_id = user_account.id
              ))
        ))
    ) then
        raise exception using errcode = '42501', message = 'external_notification_delivery_disabled_for_business_unit';
    end if;
    return new;
end
$function$;

alter table projects
    drop column project_number,
    drop column name,
    drop column item,
    drop column project_title_normalized,
    drop column sales_owner_user_id,
    drop column sales_amount,
    drop column currency_code,
    drop column delivery_location,
    drop column status_reason,
    drop column held_by_user_id,
    drop column held_at_utc,
    drop column cancelled_by_user_id,
    drop column cancelled_at_utc,
    drop column packaging_method,
    drop column deleted_correlation_id,
    drop column fat_required,
    drop column completed_by_user_id,
    drop column completed_at_utc,
    drop column structure_mode,
    drop column iqc_routing_policy,
    drop column lqc_operational_snapshot,
    drop column lqc_template_version_id,
    drop column lse_task_number,
    drop column project_profile;

alter table projects
    add constraint ck_projects_osan_registration_fields check (
        project_title is not null
        and length(btrim(project_title)) between 1 and 200
        and project_code is not null
        and length(btrim(project_code)) between 1 and 80
        and project_code = btrim(project_code)
        and customer_name is not null
        and length(btrim(customer_name)) between 1 and 200
        and delivery_date is not null
        and osan_product_name is not null
        and length(btrim(osan_product_name)) between 1 and 100
        and osan_quantity between 1 and 500
        and (osan_po_number is null or length(osan_po_number) between 1 and 100)
        and (osan_work_order_number is null or length(osan_work_order_number) between 1 and 100)
    ),
    add constraint ck_projects_status check (status in ('Active', 'Completed'));
create index ix_projects_osan_customer_id on projects(osan_customer_id);

drop table
    panel_information_excel_import_batches,
    panel_placeholders,
    panel_qr_codes,
    panel_qr_events,
    project_assignees,
    project_audit_events,
    project_workflow_events,
    work_item_escalations,
    work_items,
    workflow_stages,
    procurement_excel_import_batch_projects,
    procurement_excel_import_batches,
    procurement_required_item_template_rows,
    procurement_required_item_templates,
    project_procurement_items,
    manufacturing_step_template_items,
    manufacturing_step_template_versions,
    manufacturing_step_templates,
    production_control_manufacturing_items,
    production_control_manufacturing_templates,
    production_control_manufacturing_versions,
    production_control_plan_connections,
    production_control_plan_items,
    production_control_plan_templates,
    production_control_plan_versions,
    production_plan_template_audit_events,
    production_plan_template_steps,
    production_plan_templates,
    production_planning_excel_import_batches,
    production_product_types,
    project_manufacturing_step_snapshots,
    project_production_plan_connections,
    project_production_plan_items,
    project_production_plan_set_default_values,
    project_production_plan_set_defaults,
    project_production_plan_set_item_values,
    project_production_plan_set_scopes,
    project_production_plans,
    material_categories,
    material_category_audit_events,
    material_category_iqc_setting_audit_events,
    material_category_iqc_settings,
    material_iqc_attempts,
    material_iqc_scan_attachments,
    material_iqc_scan_reports,
    material_receipt_events,
    material_receipts,
    panel_kitting_batches,
    panel_kitting_completions,
    panel_manufacturing_assembly_batch_operations,
    panel_manufacturing_completion_confirmations,
    panel_manufacturing_events,
    panel_manufacturing_execution_steps,
    panel_manufacturing_executions,
    panel_manufacturing_operations,
    panel_manufacturing_release_operations,
    iqc_report_pdf_artifacts,
    iqc_report_photos,
    iqc_report_responses,
    iqc_report_template_items,
    iqc_report_template_versions,
    iqc_report_templates,
    iqc_reports,
    lqc_item_setting_audit_events,
    lqc_item_settings,
    panel_quality_inspection_attempts,
    panel_quality_operations,
    panel_quality_report_pdf_artifacts,
    panel_quality_report_photos,
    panel_quality_report_responses,
    panel_quality_reports,
    panel_quality_template_items,
    panel_quality_template_versions,
    pending_action_photos,
    pending_comments,
    pending_history,
    pending_issue_type_audit_events,
    pending_issue_type_catalog,
    pending_issues,
    pending_photo_operations,
    logistics_batch_panels,
    logistics_batch_units,
    logistics_batches,
    logistics_delivery_results,
    logistics_evidence,
    logistics_operations,
    logistics_packing_unit_panels,
    logistics_packing_units,
    sales_billing_request_batches,
    sales_billing_request_download_events,
    sales_billing_request_items,
    sales_billing_request_operations,
    sales_monthly_billing_confirmations,
    sales_monthly_billing_ledgers,
    sales_monthly_billing_operations,
    sales_monthly_billing_revision_cases,
    sales_monthly_billing_revision_panels,
    sales_monthly_billing_revisions,
    sales_monthly_target_audit_events,
    sales_monthly_targets,
    sales_settlement_operations,
    sales_settlements,
    ul891_recovery_case_events,
    ul891_recovery_cases,
    ul891_set_design_slots,
    ul891_set_instances,
    ul891_set_operations,
    ul891_set_spec_components,
    ul891_set_spec_versions,
    ul891_set_specs,
    g2_daily_metrics,
    g2_defect_inventory_counts,
    g2_inventory_counts,
    g2_targets,
    busbar_audit,
    busbar_bom_lines,
    busbar_boms,
    busbar_detached_pages,
    busbar_ecount_attempts,
    busbar_ecount_employees,
    busbar_ecount_jobs,
    busbar_ecount_runtime,
    busbar_label_events,
    busbar_label_requests,
    busbar_ledger,
    busbar_master_access,
    busbar_materials,
    busbar_operations,
    busbar_photo_history,
    busbar_photos,
    busbar_plans,
    busbar_product_families,
    busbar_product_qr,
    busbar_products,
    busbar_projects,
    busbar_publication_recovery,
    busbar_purchases,
    busbar_receipts,
    busbar_settings,
    busbar_shipment_products,
    busbar_shipments,
    busbar_stock,
    busbar_workers,
    form_template_audit_events,
    form_template_manager_bindings,
    notification_delivery_reprocess_events,
    user_notification_preference_audit_events,
    user_notification_preference_profiles,
    user_notification_preferences,
    site_access_coverage_state,
    site_access_sessions,
    admin_master_change_logs,
    system_holidays;

-- The common catalog created these functions only for the retired Cheongju
-- tables. Remove them after their triggers/checks; RESTRICT rejects unknown
-- retained dependencies instead of removing dependent objects implicitly.
drop function public.busbar_guard_final_photo() restrict;
drop function public.busbar_prepare_panel_qr() restrict;
drop function public.guard_append_only_form_template_audit() restrict;
drop function public.guard_append_only_logistics_operation() restrict;
drop function public.guard_append_only_pending_issue_type_audit() restrict;
drop function public.guard_append_only_sales_monthly_target_audit() restrict;
drop function public.guard_append_only_sales_settlement_operation() restrict;
drop function public.guard_completed_sales_settlement() restrict;
drop function public.guard_finalized_iqc_report_children() restrict;
drop function public.guard_finalized_iqc_report_core() restrict;
drop function public.guard_finalized_logistics_child() restrict;
drop function public.guard_finalized_logistics_owner() restrict;
drop function public.guard_finalized_material_iqc_scan_attachment() restrict;
drop function public.guard_finalized_material_iqc_scan_report() restrict;
drop function public.guard_finalized_panel_quality_report_children() restrict;
drop function public.guard_finalized_panel_quality_report_core() restrict;
drop function public.guard_form_template_version_lifecycle() restrict;
drop function public.guard_iqc_report_pdf_artifact_immutable() restrict;
drop function public.guard_iqc_template_item_mutation() restrict;
drop function public.guard_lqc_item_setting_audit_append_only() restrict;
drop function public.guard_manufacturing_template_item_mutation() restrict;
drop function public.guard_material_category_audit_append_only() restrict;
drop function public.guard_material_category_iqc_projection_write() restrict;
drop function public.guard_material_category_iqc_setting_audit_append_only() restrict;
drop function public.guard_material_receipt_projection_write() restrict;
drop function public.guard_panel_qr_event_append_only() restrict;
drop function public.guard_panel_quality_pdf_artifact_immutable() restrict;
drop function public.guard_panel_quality_template_item_mutation() restrict;
drop function public.guard_pending_action_photo_evidence() restrict;
drop function public.guard_pending_issue_type_catalog() restrict;
drop function public.guard_pending_photo_operation_append_only() restrict;
drop function public.guard_pending_project_lifecycle() restrict;
drop function public.guard_sales_billing_request_append_only() restrict;
drop function public.guard_ul891_append_only() restrict;
drop function public.guard_ul891_component_immutability() restrict;
drop function public.guard_ul891_spec_version_immutability() restrict;
drop function public.sync_material_category_iqc_projection() restrict;
-- Remove callers before their dedicated site-access helpers.
drop function public.qms_record_site_access(uuid, uuid, text, text, inet, text, text) restrict;
drop function public.qms_end_site_access(uuid, uuid, uuid) restrict;
drop function public.qms_site_access_guard_updates() restrict;
drop function public.qms_site_access_menu_codes_valid(text[]) restrict;

do $migration$
begin
    -- Include unexpected overloads in the final absence check.
    if exists (
        select 1 from pg_proc
        where pronamespace = 'public'::regnamespace
          and proname = any(array[
              'busbar_guard_final_photo',
              'busbar_prepare_panel_qr',
              'guard_append_only_form_template_audit',
              'guard_append_only_logistics_operation',
              'guard_append_only_pending_issue_type_audit',
              'guard_append_only_sales_monthly_target_audit',
              'guard_append_only_sales_settlement_operation',
              'guard_completed_sales_settlement',
              'guard_finalized_iqc_report_children',
              'guard_finalized_iqc_report_core',
              'guard_finalized_logistics_child',
              'guard_finalized_logistics_owner',
              'guard_finalized_material_iqc_scan_attachment',
              'guard_finalized_material_iqc_scan_report',
              'guard_finalized_panel_quality_report_children',
              'guard_finalized_panel_quality_report_core',
              'guard_form_template_version_lifecycle',
              'guard_iqc_report_pdf_artifact_immutable',
              'guard_iqc_template_item_mutation',
              'guard_lqc_item_setting_audit_append_only',
              'guard_manufacturing_template_item_mutation',
              'guard_material_category_audit_append_only',
              'guard_material_category_iqc_projection_write',
              'guard_material_category_iqc_setting_audit_append_only',
              'guard_material_receipt_projection_write',
              'guard_panel_qr_event_append_only',
              'guard_panel_quality_pdf_artifact_immutable',
              'guard_panel_quality_template_item_mutation',
              'guard_pending_action_photo_evidence',
              'guard_pending_issue_type_catalog',
              'guard_pending_photo_operation_append_only',
              'guard_pending_project_lifecycle',
              'guard_sales_billing_request_append_only',
              'guard_ul891_append_only',
              'guard_ul891_component_immutability',
              'guard_ul891_spec_version_immutability',
              'qms_end_site_access',
              'qms_record_site_access',
              'qms_site_access_guard_updates',
              'qms_site_access_menu_codes_valid',
              'sync_material_category_iqc_projection'
          ])
    ) then
        raise exception using errcode = 'P0001', message = 'osan_retired_functions_remain';
    end if;
end
$migration$;

do $migration$
declare
    expected_profiles jsonb;
    expected_preferences jsonb;
    actual_profiles jsonb;
    actual_preferences jsonb;
begin
    select profiles, preferences into expected_profiles, expected_preferences
    from business_schema_preserved_preferences;
    select coalesce(jsonb_agg(to_jsonb(profile) order by profile.user_id), '[]'::jsonb)
    into actual_profiles from osan_notification_preference_profiles profile;
    select coalesce(jsonb_agg(to_jsonb(preference)
                              order by preference.user_id, preference.event_kind,
                                       preference.channel, preference.stage_sequence), '[]'::jsonb)
    into actual_preferences from osan_notification_preferences preference;
    if actual_profiles <> expected_profiles or actual_preferences <> expected_preferences then
        raise exception using errcode = 'P0001', message = 'osan_personal_notification_preferences_changed';
    end if;

    if exists (
        (select id, manual_payload_json from business_schema_preserved_manual_payloads
         except
         select id, manual_payload_json from notification_deliveries)
        union all
        (select id, manual_payload_json from notification_deliveries
         except
         select id, manual_payload_json from business_schema_preserved_manual_payloads)
    ) then
        raise exception using errcode = 'P0001', message = 'osan_notification_manual_payload_changed';
    end if;

    if not exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'notification_deliveries'
          and column_name = 'manual_payload_json'
    ) or not exists (
        select 1 from pg_trigger
        where tgrelid = 'public.notification_deliveries'::regclass
          and tgname = 'trg_prevent_osan_external_notification_delivery'
          and not tgisinternal
          and tgtype = 7
    ) or exists (
        select 1
        from pg_proc
        where oid = 'public.prevent_osan_external_notification_delivery()'::regprocedure
          and position('project_profile' in pg_get_functiondef(oid)) > 0
    ) then
        raise exception using errcode = 'P0001', message = 'osan_notification_delivery_contract_missing';
    end if;
end
$migration$;


do $migration$
declare
    expected_tables text[] := array[
        'audit_coverage_state',
        'audit_event_changes',
        'audit_events',
        'authorization_audit_events',
        'data_export_events',
        'departments',
        'deployment_maintenance',
        'deployment_maintenance_popup_receipts',
        'notice_attachments',
        'notice_popup_receipts',
        'notice_post_revisions',
        'notice_posts',
        'notice_reads',
        'notice_setting_events',
        'notification_deliveries',
        'notification_delivery_attempts',
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
        'permissions',
        'projects',
        'qms_database_identity',
        'qms_users',
        'role_permissions',
        'roles',
        'schema_migrations',
        'user_profile_photo_audit_events',
        'user_profile_photos',
        'user_project_access',
        'user_roles',
        'web_push_subscription_events',
        'web_push_subscriptions'
    ]::text[];
    expected_project_columns text[] := array[
        'created_at_utc',
        'created_by_user_id',
        'customer_name',
        'delete_reason',
        'deleted_at_utc',
        'deleted_by_user_id',
        'delivery_date',
        'id',
        'osan_customer_id',
        'osan_delivery_hold',
        'osan_po_number',
        'osan_product_name',
        'osan_quantity',
        'osan_work_order_number',
        'project_code',
        'project_key',
        'project_title',
        'status',
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
