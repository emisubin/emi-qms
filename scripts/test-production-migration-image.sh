#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

image_ref="${1:?usage: test-production-migration-image.sh <backend-image-ref>}"
if [[ ! "${image_ref}" =~ ^[A-Za-z0-9._:/@-]+$ ]]; then
  echo "Production migration image reference contains unsupported characters." >&2
  exit 64
fi

test_business_schemas="${PRODUCTION_MIGRATION_TEST_BUSINESS_SCHEMAS:-false}"
case "${test_business_schemas}" in
  true|false) ;;
  *) echo "Production migration business schema test flag is invalid." >&2; exit 64 ;;
esac

managed_owner="${PRODUCTION_MIGRATION_CONTAINER_OWNER:-}"
managed_run_id="${PRODUCTION_MIGRATION_CONTAINER_RUN_ID:-}"
managed_container_mode=0
owner_label="com.emi-qms.test.owner"
run_label="com.emi-qms.test.run-id"

if [[ -n "${managed_owner}" || -n "${managed_run_id}" ]]; then
  if [[ "${managed_owner}" != "business-unit-production-image" || -z "${managed_run_id}" ]]; then
    echo "Production migration container ownership context is invalid." >&2
    exit "${E2E_SAFETY_EXIT_CODE}"
  fi
  e2e_require_safe_run_id "${managed_run_id}" || exit $?
  managed_container_mode=1
fi

if [[ "${managed_container_mode}" == "1" ]]; then
  if ! docker image inspect "${image_ref}" >/dev/null 2>&1; then
    echo "Production migration image could not be inspected." >&2
    exit 1
  fi
else
  docker image inspect "${image_ref}" >/dev/null
fi

e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

if [[ "${managed_container_mode}" == "1" && "${E2E_RUN_ID}" != "${managed_run_id}" ]]; then
  echo "Production migration container scope does not match the E2E run id." >&2
  exit "${E2E_SAFETY_EXIT_CODE}"
fi

cleanup_started=0
resource_scope_initialized=0
managed_container_names=()
managed_container_count=0
fixture_failure_log=''

container_state() {
  local container_name="$1"
  local matching_ids

  if docker container inspect "${container_name}" >/dev/null 2>&1; then
    printf 'PRESENT\n'
    return 0
  fi

  if ! matching_ids="$(docker ps -aq --filter "name=^/${container_name}$")"; then
    printf 'UNKNOWN\n'
    return 1
  fi

  if [[ -n "${matching_ids}" ]]; then
    printf 'UNKNOWN\n'
    return 1
  fi

  printf 'ABSENT\n'
}

assert_container_ownership() {
  local container_name="$1"
  local actual_owner
  local actual_run_id

  if ! actual_owner="$(docker container inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.owner" }}' "${container_name}")"; then
    return 2
  fi
  if ! actual_run_id="$(docker container inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.run-id" }}' "${container_name}")"; then
    return 2
  fi

  [[ "${actual_owner}" == "${managed_owner}" && "${actual_run_id}" == "${managed_run_id}" ]]
}

remove_managed_container() {
  local container_name="$1"
  local state

  if ! state="$(container_state "${container_name}")"; then
    echo "Production migration container state is unknown." >&2
    return 1
  fi
  [[ "${state}" == "ABSENT" ]] && return 0

  if ! assert_container_ownership "${container_name}"; then
    echo "Production migration container ownership verification failed." >&2
    return 1
  fi
  if ! docker container rm --force "${container_name}" >/dev/null 2>&1; then
    echo "Production migration container removal failed." >&2
    return 1
  fi
  if ! state="$(container_state "${container_name}")" || [[ "${state}" != "ABSENT" ]]; then
    echo "Production migration container removal could not be verified." >&2
    return 1
  fi
}

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0
  local container_name

  if [[ "${cleanup_started}" == "1" ]]; then
    exit "${original_exit_code}"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  if [[ -n "${fixture_failure_log}" ]]; then
    rm -f -- "${fixture_failure_log}" || cleanup_exit_code=1
  fi

  if [[ "${managed_container_mode}" == "1" && "${managed_container_count}" -gt 0 ]]; then
    for container_name in "${managed_container_names[@]}"; do
      remove_managed_container "${container_name}" || cleanup_exit_code=1
    done
  fi

  if [[ "${resource_scope_initialized}" == "1" ]]; then
    bash "${repo_root}/scripts/e2e-db.sh" drop >&2 || cleanup_exit_code=1
    bash "${repo_root}/scripts/e2e-db.sh" assert-dropped >&2 || cleanup_exit_code=1
    e2e_stop_project >&2 || cleanup_exit_code=1
  fi

  if [[ "${original_exit_code}" -eq 0 && "${cleanup_exit_code}" -ne 0 ]]; then
    exit "${cleanup_exit_code}"
  fi

  exit "${original_exit_code}"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

resource_scope_initialized=1
e2e_start_postgres
bash "${repo_root}/scripts/e2e-db.sh" reset

network_name="${E2E_COMPOSE_PROJECT_NAME}_default"
connection_string="Host=e2e-postgres;Port=5432;Database=${E2E_DATABASE_NAME};Username=${E2E_DATABASE_USER};Password=${E2E_DATABASE_PASSWORD};SSL Mode=Disable;GSS Encryption Mode=Disable"

run_image_command() {
  local phase="$1"
  shift
  local container_name=""
  local state
  local docker_arguments=(run --rm)

  if [[ "${managed_container_mode}" == "1" ]]; then
    container_name="emi-qms-production-migration-${managed_run_id//_/-}-${phase}"
    if ! state="$(container_state "${container_name}")"; then
      echo "Production migration container preflight state is unknown." >&2
      return 1
    fi
    if [[ "${state}" != "ABSENT" ]]; then
      echo "Production migration container scope already exists." >&2
      return "${E2E_SAFETY_EXIT_CODE}"
    fi

    # Claim the predictable name before Docker can create the container.
    managed_container_names+=("${container_name}")
    managed_container_count=$((managed_container_count + 1))
    docker_arguments+=(
      --name "${container_name}"
      --label "${owner_label}=${managed_owner}"
      --label "${run_label}=${managed_run_id}"
    )
  fi

  docker "${docker_arguments[@]}" \
    --network "${network_name}" \
    --env ASPNETCORE_ENVIRONMENT=Testing \
    --env Authentication__Mode=Dev \
    --env Database__ApplyMigrationsOnStartup=false \
    --env DevelopmentData__SeedEnabled=false \
    --env DevAuthentication__Enabled=false \
    --env AdminUserSwitch__Enabled=false \
    "$@"
}

run_migration() {
  run_image_command "$1" --env "ConnectionStrings__QmsDatabase=${connection_string}" \
    "${image_ref}" --migrate-only
}

run_migration fresh
run_migration existing

migration_files=("${repo_root}"/database/migrations/*.sql)
expected_count="${#migration_files[@]}"
actual_count="$(
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname "${E2E_DATABASE_NAME}" \
    --no-psqlrc \
    --tuples-only \
    --no-align \
    --set ON_ERROR_STOP=1 \
    --command "select count(*) from schema_migrations;" |
    tr -d '[:space:]'
)"

if [[ "${actual_count}" != "${expected_count}" ]]; then
  echo "Production migration image ledger verification failed." >&2
  exit 1
fi

echo "productionMigrationImageFreshApply=passed"
echo "productionMigrationImageExistingApply=passed"
echo "productionMigrationLedgerExact=true"

if [[ "${test_business_schemas}" != true ]]; then
  exit 0
fi

# Every additional database and role lives in this run's dedicated tmpfs server.
# Nothing below accepts an external connection string or changes a server default.
e2e_assert_dedicated_postgres >/dev/null
fixture_failure_log="$(mktemp "${TMPDIR:-/tmp}/pms-image-consent.XXXXXX")"
business_environment=()
business_database_names=()

fixture_sql() {
  local database_name="$1"
  shift
  e2e_require_safe_database_name "${database_name}"
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" --dbname "${database_name}" \
    --no-psqlrc --tuples-only --no-align --set ON_ERROR_STOP=1 "$@"
}

configure_business_fixture() {
  local fixture_phase="$1" approved="$2"
  local target section suffix database_name migrator runtime schema_contract
  business_environment=(--env BusinessUnits__Enabled=true)
  business_schema_approved="${approved}"
  business_database_names=()
  for target in DIRECTORY CHEONGJU OSAN; do
    case "${target}" in
      DIRECTORY) section=Directory; suffix=directory; schema_contract=0001_business_unit_directory ;;
      CHEONGJU) section=Units__Cheongju; suffix=cheongju; schema_contract=0086_business_unit_database_identity ;;
      OSAN) section=Units__Osan; suffix=osan; schema_contract=0086_business_unit_database_identity ;;
    esac
    database_name="${E2E_DATABASE_NAME}_${fixture_phase}_${suffix}"
    e2e_require_safe_database_name "${database_name}"
    if [[ "${#database_name}" -gt 63 ]]; then
      echo "Production migration fixture database name exceeds PostgreSQL's limit." >&2
      return 64
    fi
    business_database_names+=("${database_name}")
    migrator="image_${fixture_phase}_${suffix}_migrator"
    runtime="image_${fixture_phase}_${suffix}_runtime"
    business_environment+=(
      --env "BusinessUnits__${section}__Code=${target}"
      --env "BusinessUnits__${section}__RuntimeConnection=${target}Runtime"
      --env "BusinessUnits__${section}__MigrationConnection=${target}Migration"
      --env "BusinessUnits__${section}__AdministratorConnection=${target}Admin"
      --env "BusinessUnits__${section}__ExpectedDatabaseName=${database_name}"
      --env "BusinessUnits__${section}__MigrationRoleName=${migrator}"
      --env "BusinessUnits__${section}__RuntimeRoleName=${runtime}"
      --env "BusinessUnits__${section}__ExpectedSchemaVersion=${schema_contract}"
      --env "ConnectionStrings__${target}Admin=Host=e2e-postgres;Database=${database_name};Username=${E2E_DATABASE_USER};Password=${E2E_DATABASE_PASSWORD};SSL Mode=Disable;GSS Encryption Mode=Disable"
      --env "ConnectionStrings__${target}Migration=Host=e2e-postgres;Database=${database_name};Username=${migrator};Password=synthetic_migration_password_only;SSL Mode=Disable;GSS Encryption Mode=Disable"
      --env "ConnectionStrings__${target}Runtime=Host=e2e-postgres;Database=${database_name};Username=${runtime};Password=synthetic_runtime_password_only;SSL Mode=Disable;GSS Encryption Mode=Disable"
    )
  done
}

assert_target_schema() {
  local target="$1" database_name="$2" actual expected file
  local expected_versions='' source_paths=()
  if [[ "${target}" == DIRECTORY ]]; then
    source_paths=("${repo_root}"/database/directory-migrations/*.sql)
  else
    source_paths=("${repo_root}"/database/migrations/*.sql)
    case "${target}" in
      CHEONGJU) source_paths+=("${repo_root}"/database/business-migrations/cheongju/*.sql) ;;
      OSAN) source_paths+=("${repo_root}"/database/business-migrations/osan/*.sql) ;;
    esac
  fi
  for file in "${source_paths[@]}"; do
    [[ -f "${file}" ]] || { echo "Business migration catalog is missing." >&2; return 1; }
    file="${file##*/}"
    expected_versions+="${file%.sql}"$'\n'
  done
  expected_versions="$(printf '%s' "${expected_versions}" | LC_ALL=C sort)"
  actual="$(fixture_sql "${database_name}" --command 'select version from schema_migrations order by version;')"
  [[ "${actual}" == "${expected_versions}" ]] || { echo "Business migration ledger differs from its selected catalog." >&2; return 1; }

  # Approved ownership literals copied from BusinessSchemaOwnershipTests, not
  # inferred from the packaged migrations or the database under test.
  local expected_tables expected_sequences='' expected_views='' runtime migrator suffix
  case "${target}" in
    DIRECTORY)
      suffix=directory
      expected_tables='directory_business_unit_memberships,directory_business_units,directory_identities,directory_membership_audit_events,directory_overall_administrators,directory_user_access_operations,qms_database_identity,schema_migrations'
      ;;
    CHEONGJU)
      suffix=cheongju
      expected_tables='admin_master_change_logs,audit_coverage_state,audit_event_changes,audit_events,authorization_audit_events,busbar_audit,busbar_bom_lines,busbar_boms,busbar_detached_pages,busbar_ecount_attempts,busbar_ecount_employees,busbar_ecount_jobs,busbar_ecount_runtime,busbar_label_events,busbar_label_requests,busbar_ledger,busbar_master_access,busbar_materials,busbar_operations,busbar_photo_history,busbar_photos,busbar_plans,busbar_product_families,busbar_product_qr,busbar_products,busbar_projects,busbar_publication_recovery,busbar_purchases,busbar_receipts,busbar_settings,busbar_shipment_products,busbar_shipments,busbar_stock,busbar_workers,data_export_events,departments,deployment_maintenance,deployment_maintenance_popup_receipts,form_template_audit_events,form_template_manager_bindings,g2_daily_metrics,g2_defect_inventory_counts,g2_inventory_counts,g2_targets,iqc_report_pdf_artifacts,iqc_report_photos,iqc_report_responses,iqc_report_template_items,iqc_report_template_versions,iqc_report_templates,iqc_reports,logistics_batch_panels,logistics_batch_units,logistics_batches,logistics_delivery_results,logistics_evidence,logistics_operations,logistics_packing_unit_panels,logistics_packing_units,lqc_item_setting_audit_events,lqc_item_settings,manufacturing_step_template_items,manufacturing_step_template_versions,manufacturing_step_templates,material_categories,material_category_audit_events,material_category_iqc_setting_audit_events,material_category_iqc_settings,material_iqc_attempts,material_iqc_scan_attachments,material_iqc_scan_reports,material_receipt_events,material_receipts,notice_attachments,notice_post_revisions,notice_posts,notice_reads,notification_deliveries,notification_delivery_attempts,notification_delivery_reprocess_events,notification_recipients,notifications,panel_information_excel_import_batches,panel_kitting_batches,panel_kitting_completions,panel_manufacturing_assembly_batch_operations,panel_manufacturing_completion_confirmations,panel_manufacturing_events,panel_manufacturing_execution_steps,panel_manufacturing_executions,panel_manufacturing_operations,panel_manufacturing_release_operations,panel_placeholders,panel_qr_codes,panel_qr_events,panel_quality_inspection_attempts,panel_quality_operations,panel_quality_report_pdf_artifacts,panel_quality_report_photos,panel_quality_report_responses,panel_quality_reports,panel_quality_template_items,panel_quality_template_versions,pending_action_photos,pending_comments,pending_history,pending_issue_type_audit_events,pending_issue_type_catalog,pending_issues,pending_photo_operations,permissions,procurement_excel_import_batch_projects,procurement_excel_import_batches,procurement_required_item_template_rows,procurement_required_item_templates,production_control_manufacturing_items,production_control_manufacturing_templates,production_control_manufacturing_versions,production_control_plan_connections,production_control_plan_items,production_control_plan_templates,production_control_plan_versions,production_plan_template_audit_events,production_plan_template_steps,production_plan_templates,production_planning_excel_import_batches,production_product_types,project_assignees,project_audit_events,project_manufacturing_step_snapshots,project_procurement_items,project_production_plan_connections,project_production_plan_items,project_production_plan_set_default_values,project_production_plan_set_defaults,project_production_plan_set_item_values,project_production_plan_set_scopes,project_production_plans,project_workflow_events,projects,qms_database_identity,qms_users,role_permissions,roles,sales_billing_request_batches,sales_billing_request_download_events,sales_billing_request_items,sales_billing_request_operations,sales_monthly_billing_confirmations,sales_monthly_billing_ledgers,sales_monthly_billing_operations,sales_monthly_billing_revision_cases,sales_monthly_billing_revision_panels,sales_monthly_billing_revisions,sales_monthly_target_audit_events,sales_monthly_targets,sales_settlement_operations,sales_settlements,schema_migrations,site_access_coverage_state,site_access_sessions,system_holidays,ul891_recovery_case_events,ul891_recovery_cases,ul891_set_design_slots,ul891_set_instances,ul891_set_operations,ul891_set_spec_components,ul891_set_spec_versions,ul891_set_specs,user_notification_preference_audit_events,user_notification_preference_profiles,user_notification_preferences,user_profile_photo_audit_events,user_profile_photos,user_project_access,user_roles,web_push_subscription_events,web_push_subscriptions,work_item_escalations,work_items,workflow_stages'
      expected_sequences='audit_event_changes_id_seq,busbar_product_number_seq,pending_issue_number_seq,sales_billing_request_batches_request_number_seq'
      ;;
    OSAN)
      suffix=osan
      expected_tables='audit_coverage_state,audit_event_changes,audit_events,authorization_audit_events,data_export_events,departments,deployment_maintenance,deployment_maintenance_popup_receipts,notice_attachments,notice_popup_receipts,notice_post_revisions,notice_posts,notice_reads,notice_setting_events,notification_deliveries,notification_delivery_attempts,notification_recipients,notifications,osan_customer_assignment_versions,osan_customer_assignments,osan_customers,osan_gate_configuration,osan_gate_departments,osan_notification_events,osan_notification_global_preference_profiles,osan_notification_global_preferences,osan_notification_preference_profiles,osan_notification_preferences,osan_photo_edit_requests,osan_photo_revision_files,osan_progress_operations,osan_progress_photos,osan_progress_step_photos,osan_project_completion_notifications,osan_project_create_operations,osan_project_events,osan_project_management_history,osan_project_target_steps,osan_project_targets,osan_stage_issues,osan_stage_records,osan_stage_work_request_recipients,osan_stage_work_requests,permissions,projects,qms_database_identity,qms_users,role_permissions,roles,schema_migrations,user_profile_photo_audit_events,user_profile_photos,user_project_access,user_roles,web_push_subscription_events,web_push_subscriptions'
      expected_sequences='audit_event_changes_id_seq'
      expected_views='osan_active_project_target_steps,osan_active_project_targets,osan_all_progress_photos,osan_current_progress_photos'
      ;;
    *) echo "Unknown packaged schema target." >&2; return 1 ;;
  esac
  runtime="image_${fixture_phase}_${suffix}_runtime"
  migrator="image_${fixture_phase}_${suffix}_migrator"
  actual="$(fixture_sql "${database_name}" --command "select coalesce(string_agg(tablename, ',' order by tablename), '') from pg_tables where schemaname='public';")"
  [[ "${actual}" == "${expected_tables}" ]] || { echo "Packaged table ownership mismatch for ${target}." >&2; return 1; }
  actual="$(fixture_sql "${database_name}" --command "select coalesce(string_agg(relname, ',' order by relname), '') from pg_class where relnamespace='public'::regnamespace and relkind='S';")"
  [[ "${actual}" == "${expected_sequences}" ]] || { echo "Packaged sequence ownership mismatch for ${target}." >&2; return 1; }
  actual="$(fixture_sql "${database_name}" --command "select coalesce(string_agg(relname, ',' order by relname), '') from pg_class where relnamespace='public'::regnamespace and relkind in ('v','m');")"
  [[ "${actual}" == "${expected_views}" ]] || { echo "Packaged view ownership mismatch for ${target}." >&2; return 1; }

  # A PostgreSQL FK's OIDs are local to this database. Require public ordinary
  # retained tables at both ends; disallow foreign tables/servers and extra schemas.
  actual="$(fixture_sql "${database_name}" --command "select
    not exists(select 1 from pg_foreign_server)
    and not exists(select 1 from pg_foreign_table)
    and not exists(select 1 from pg_namespace where nspname not in ('public','pg_catalog','information_schema') and nspname !~ '^pg_')
    and not exists(
      select 1 from pg_constraint fk
      join pg_class child on child.oid=fk.conrelid
      join pg_class parent on parent.oid=fk.confrelid
      where fk.contype='f' and child.relnamespace='public'::regnamespace
        and (parent.relnamespace<>'public'::regnamespace or child.relkind not in ('r','p') or parent.relkind not in ('r','p')))
    and not exists(
      select 1 from pg_class where relnamespace='public'::regnamespace and relkind='S'
        and (not has_sequence_privilege('${runtime}', oid, 'USAGE')
          or not has_sequence_privilege('${runtime}', oid, 'SELECT')))
    and has_database_privilege('${runtime}', current_database(), 'CONNECT')
    and not has_table_privilege('${runtime}', 'public.schema_migrations', 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
    and not has_table_privilege('${runtime}', 'public.qms_database_identity', 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
    and not exists(
      select 1 from pg_default_acl defaults cross join lateral aclexplode(defaults.defaclacl) privilege
      where defaults.defaclrole='${migrator}'::regrole and defaults.defaclnamespace='public'::regnamespace
        and defaults.defaclobjtype='f' and privilege.grantee=0 and privilege.privilege_type='EXECUTE')
    and not exists(
      select 1 from aclexplode(coalesce(
        (select defaclacl from pg_default_acl where defaclrole='${migrator}'::regrole and defaclnamespace=0 and defaclobjtype='f'),
        acldefault('f','${migrator}'::regrole))) privilege
      where privilege.grantee=0 and privilege.privilege_type='EXECUTE');")"
  [[ "${actual}" == t ]] || { echo "Packaged dependency/runtime/default-function privilege contract failed for ${target}." >&2; return 1; }
  if [[ "${target}" == DIRECTORY ]]; then
    actual="$(fixture_sql "${database_name}" --command "select
      not exists(select 1 from pg_tables where schemaname='public'
        and (not has_table_privilege('${runtime}', quote_ident(schemaname)||'.'||quote_ident(tablename), 'SELECT')
          or has_table_privilege('${runtime}', quote_ident(schemaname)||'.'||quote_ident(tablename), 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')))
      and not exists(
        select 1 from pg_default_acl defaults cross join lateral aclexplode(defaults.defaclacl) privilege
        where defaults.defaclrole='${migrator}'::regrole
          and defaults.defaclnamespace in (0,'public'::regnamespace)
          and privilege.grantee in (0,'${runtime}'::regrole)
          and ((defaults.defaclobjtype='r' and privilege.privilege_type<>'SELECT') or defaults.defaclobjtype='S'))
      and exists(
        select 1 from pg_default_acl defaults cross join lateral aclexplode(defaults.defaclacl) privilege
        where defaults.defaclrole='${migrator}'::regrole and defaults.defaclnamespace='public'::regnamespace
          and defaults.defaclobjtype='r' and privilege.grantee='${runtime}'::regrole and privilege.privilege_type='SELECT');")"
    [[ "${actual}" == t ]] || { echo "Packaged Directory read-only/default privilege contract failed." >&2; return 1; }
  fi
  if [[ "${target}" == DIRECTORY ]]; then
    actual="$(fixture_sql "${database_name}" --command "select database_kind || ':' || coalesce(business_unit_code, '') from qms_database_identity;")"
    [[ "${actual}" == directory: ]] || return 1
    return 0
  fi
  actual="$(fixture_sql "${database_name}" --command "select
    (select count(*) from pg_tables where schemaname='public')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='projects')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='notifications')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and table_name='notification_deliveries')::text || ':' ||
    (select count(*) from information_schema.columns where table_schema='public' and column_name='project_profile')::text;")"
  case "${target}" in
    CHEONGJU) expected=182:36:14:46:0 ;;
    OSAN) expected=56:19:12:45:0 ;;
  esac
  [[ "${actual}" == "${expected}" ]] || { echo "Business migration schema contract failed for ${target}." >&2; return 1; }
  # These are uncustomized synthetic fixtures. Production retains existing links
  # for the seven approved Osan permissions rather than imposing a fixed count.
  actual="$(fixture_sql "${database_name}" --command "select
    (select count(*) from permissions)::text || ':' ||
    (select count(*) from roles)::text || ':' ||
    (select count(*) from departments)::text || ':' ||
    (select count(*) from role_permissions)::text;")"
  case "${target}" in
    CHEONGJU) expected=35:11:10:111 ;;
    OSAN) expected=7:10:10:29 ;;
  esac
  [[ "${actual}" == "${expected}" ]] || { echo "Packaged baseline permission/role contract failed for ${target}." >&2; return 1; }
  if [[ "${target}" == OSAN ]]; then
    actual="$(fixture_sql "${database_name}" --command "select
      not exists(select code from permissions except values
        ('projects.read'),('Project.Read.All'),('Project.Create'),('Project.Update'),
        ('Project.Delete'),('manufacturing.update'),('users.manage'))
      and not exists(select 1 from roles where code='interior-busbar-manager');")"
    [[ "${actual}" == t ]] || { echo "Packaged Osan permission ownership mismatch." >&2; return 1; }
  fi
  actual="$(fixture_sql "${database_name}" --command 'select business_unit_code from qms_database_identity;')"
  [[ "${actual}" == "${target}" ]] || return 1
}

for fixture_phase in fresh upgrade; do
  configure_business_fixture "${fixture_phase}" false
  if [[ "${fixture_phase}" == fresh ]]; then
    # Production validation must return a fixed error and exit 1 before attempting
    # a connection, even when the operation target/credentials are invalid.
    for invalid_configuration in missing-target invalid-target missing-credentials migration-invalid-target; do
      invalid_arguments=(--env ASPNETCORE_ENVIRONMENT=Production)
      operation=--deployment-drain-check
      case "${invalid_configuration}" in
        missing-target) invalid_arguments+=(--env Database__MigrationTarget=) ;;
        invalid-target) invalid_arguments+=(--env Database__MigrationTarget=UNKNOWN) ;;
        missing-credentials) invalid_arguments+=(--env Database__MigrationTarget=CHEONGJU --env ConnectionStrings__CHEONGJUMigration=) ;;
        migration-invalid-target) invalid_arguments+=(--env Database__MigrationTarget=UNKNOWN); operation=--migrate-only ;;
      esac
      invalid_exit=0
      run_image_command "business-configuration-${invalid_configuration}" \
        "${business_environment[@]}" "${invalid_arguments[@]}" \
        "${image_ref}" "${operation}" >"${fixture_failure_log}" 2>&1 || invalid_exit=$?
      if [[ "${invalid_exit}" -ne 1 ]]; then
        echo "Packaged CLI invalid operation configuration exit mismatch. Configuration=${invalid_configuration}; Exit=${invalid_exit}." >&2
        exit 1
      fi
      if ! rg -q 'Code=database_operation_configuration_invalid' "${fixture_failure_log}"; then
        echo "Packaged CLI invalid operation configuration fixed code missing. Configuration=${invalid_configuration}; Exit=${invalid_exit}." >&2
        exit 1
      fi
    done
  fi
  for database_name in "${business_database_names[@]}"; do
    # The identifier has already passed the strict synthetic-name and length guards.
    fixture_sql "${E2E_DATABASE_NAME}" --command "create database \"${database_name}\";" >/dev/null
  done
  for target in DIRECTORY CHEONGJU OSAN; do
    run_image_command "business-${fixture_phase}-${target}-bootstrap" \
      "${business_environment[@]}" --env "Database__BootstrapTarget=${target}" \
      "${image_ref}" --bootstrap-database-roles
  done
  if [[ "${fixture_phase}" == upgrade ]]; then
    target_index=0
    for target in DIRECTORY CHEONGJU OSAN; do
      database_name="${business_database_names[${target_index}]}"
      target_index=$((target_index + 1))
      [[ "${target}" != DIRECTORY ]] || continue
      # The real packaged CLI builds the common prefix, then must refuse 0131.
      # Do not accept an unrelated startup/target error as this negative result.
      if run_image_command "business-${fixture_phase}-${target}-unapproved" \
        "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
        --env Database__BusinessSchemaSeparationApproved=false \
        "${image_ref}" --migrate-only >"${fixture_failure_log}" 2>&1; then
        echo "Business schema migration unexpectedly accepted missing consent." >&2
        exit 1
      fi
      if ! grep -Fq business_schema_explicit_consent_required "${fixture_failure_log}"; then
        echo "Unapproved business migration failed for an unexpected reason." >&2
        exit 1
      fi
      baseline="$(fixture_sql "${database_name}" --command "select
        (select count(*) from schema_migrations)::text || ':' ||
        (select max(version) from schema_migrations) || ':' ||
        (select count(*) from pg_tables where schemaname='public')::text || ':' ||
        (select count(*) from information_schema.columns where table_schema='public' and table_name='projects')::text;")"
      last_common="${migration_files[${#migration_files[@]}-1]##*/}"
      [[ "${baseline}" == "${expected_count}:${last_common%.sql}:209:43" ]] || {
        echo "Unapproved business migration did not preserve its complete common schema prefix." >&2
        exit 1
      }
    done
  fi
  configure_business_fixture "${fixture_phase}" true
  target_index=0
  for target in DIRECTORY CHEONGJU OSAN; do
    database_name="${business_database_names[${target_index}]}"
    target_index=$((target_index + 1))
    target_schema_approved=false
    [[ "${target}" == DIRECTORY ]] || target_schema_approved="${business_schema_approved}"
    run_image_command "business-${fixture_phase}-${target}-apply" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env "Database__BusinessSchemaSeparationApproved=${target_schema_approved}" \
      "${image_ref}" --migrate-only
    assert_target_schema "${target}" "${database_name}"
    run_image_command "business-${fixture_phase}-${target}-existing" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env Database__BusinessSchemaSeparationApproved=false \
      "${image_ref}" --migrate-only
    assert_target_schema "${target}" "${database_name}"
    run_image_command "business-${fixture_phase}-${target}-drain" \
      "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
      --env Database__BusinessSchemaSeparationApproved=false \
      --env "DeploymentDrain__RequireMaintenance=$([[ "${target}" == DIRECTORY ]] && echo true || echo false)" \
      --env DeploymentDrain__ReleaseId=60000000-0000-0000-0000-000000000001 \
      "${image_ref}" --deployment-drain-check
    if [[ "${target}" != DIRECTORY ]]; then
      if run_image_command "business-${fixture_phase}-${target}-drain-required" \
        "${business_environment[@]}" --env "Database__MigrationTarget=${target}" \
        --env DeploymentDrain__RequireMaintenance=true \
        --env DeploymentDrain__ReleaseId=60000000-0000-0000-0000-000000000001 \
        "${image_ref}" --deployment-drain-check >"${fixture_failure_log}" 2>&1; then
        echo "Packaged drain CLI accepted an inactive maintenance state." >&2
        exit 1
      fi
      if ! rg -q 'Code=deployment_drain_maintenance_state_mismatch' "${fixture_failure_log}"; then
        echo "Packaged drain CLI failed for an unexpected reason." >&2
        exit 1
      fi
    fi
  done
done

echo "productionBusinessMigrationFreshApply=passed"
echo "productionBusinessMigrationUpgradeApply=passed"
echo "productionBusinessMigrationSchemaContracts=passed"
echo "productionBusinessDeploymentDrainCli=passed"
echo "productionBusinessOperationConfigurationFailures=passed"
