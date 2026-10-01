#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="${repository_root}/scripts/deploy-azure-pilot-release.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-azure-release-test.XXXXXX")"

cleanup() {
  rm -f "${temporary_directory}/recipient.pem" "${temporary_directory}/recipient.key"
  rm -rf "${temporary_directory}"/pms-recovery-checkpoint.*
  rm -f "${temporary_directory}/recovery-backup.json" "${temporary_directory}/recovery-reads" "${temporary_directory}/step-output"
  rm -f \
    "${temporary_directory}/az" \
    "${temporary_directory}/curl" \
    "${temporary_directory}/backend-image" \
    "${temporary_directory}/frontend-image" \
    "${temporary_directory}/calls" \
    "${temporary_directory}/maintenance-action" \
    "${temporary_directory}/stdout" \
    "${temporary_directory}/stderr" \
    "${temporary_directory}/drain-completed" "${temporary_directory}/job-mode" \
    "${temporary_directory}/pms-synthetic-backend-active" "${temporary_directory}/pms-synthetic-frontend-active" \
    "${temporary_directory}/maintenance-CHEONGJU" "${temporary_directory}/maintenance-OSAN" "${temporary_directory}/job-forced-failure" \
    "${temporary_directory}/pms-synthetic-backend-image" "${temporary_directory}/pms-synthetic-frontend-image"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 1 -addext subjectKeyIdentifier=hash -subj '/CN=Synthetic Recovery Test' \
  -keyout "${temporary_directory}/recipient.key" -out "${temporary_directory}/recipient.pem" >/dev/null 2>&1
recovery_certificate="$(cat "${temporary_directory}/recipient.pem")"

cat >"${temporary_directory}/az" <<'MOCK_AZ'
#!/usr/bin/env bash
set -euo pipefail
for argument in "$@"; do
  if [[ "$argument" == --only-show-errors ]]; then
    exec python3 "$RECOVERY_MOCK_SCRIPT" "$@"
  fi
done

name=''
query=''
image=''
revision=''
inspection='false'
maintenance_action=''
selected_target=''
execution_name=''
job_mode=''
schema_approved=''
drain_required=''
drain_release=''
accepted_mail_snapshot=''
expected_postgres_host=''
inspection_environment_count=0
cpu=''
memory=''
for ((index = 1; index <= $#; index++)); do
  argument="${!index}"
  case "${argument}" in
    --name)
      next=$((index + 1))
      name="${!next}"
      ;;
    --job-execution-name)
      next=$((index + 1))
      execution_name="${!next}"
      ;;
    Maintenance__BusinessUnit=*|Database__BootstrapTarget=*|Database__MigrationTarget=*)
      [[ -z "${selected_target}" ]] || exit 2
      selected_target="${argument#*=}"
      ;;
    --args=--deployment-drain-check) job_mode=drain ;;
    --args=--migrate-only) job_mode=migration ;;
    --args=--bootstrap-database-roles) job_mode=bootstrap ;;
    --args=--backfill-business-unit-memberships) job_mode=backfill ;;
    Database__BusinessSchemaSeparationApproved=*) schema_approved="${argument#*=}" ;;
    DeploymentDrain__RequireMaintenance=*) drain_required="${argument#*=}" ;;
    Database__RecoveryPostgresHost=*) expected_postgres_host="${argument#*=}" ;;
    DeploymentDrain__ReleaseId=*) drain_release="${argument#*=}" ;;
    DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256=*) accepted_mail_snapshot="${argument#*=}" ;;
    --query)
      next=$((index + 1))
      query="${!next}"
      ;;
    --image)
      next=$((index + 1))
      image="${!next}"
      ;;
    --revision)
      next=$((index + 1))
      revision="${!next}"
      ;;
    --args=--inspect-business-unit-membership-backfill)
      inspection='true'
      ;;
    --args=--maintenance-*)
      maintenance_action="${argument#--args=--maintenance-}"
      ;;
    --cpu)
      next=$((index + 1))
      cpu="${!next}"
      ;;
    --memory)
      next=$((index + 1))
      memory="${!next}"
      ;;
    ASPNETCORE_ENVIRONMENT=Production|BusinessUnits__Enabled=true|\
    ConnectionStrings__QmsDirectoryMigration=secretref:qms-directory-migration|\
    ConnectionStrings__QmsCheongjuMigration=secretref:qms-cheongju-migration|\
    ConnectionStrings__QmsOsanMigration=secretref:qms-osan-migration|\
    BusinessUnits__MembershipBackfill__ApprovedUserIdsDelimited=secretref:approved-users|\
    BusinessUnits__MembershipBackfill__OverallAdministratorUserIdsDelimited=secretref:overall-administrators)
      inspection_environment_count=$((inspection_environment_count + 1))
      ;;
  esac
done

backend_template() {
  python3 - "${AZURE_RELEASE_TEST_SCENARIO}" "${1:-template}" <<'PY_TEMPLATE'
import json, sys
scenario, phase = sys.argv[1:]
env = [{"name": "ASPNETCORE_ENVIRONMENT", "value": "Production"},
       {"name": "BusinessUnits__Enabled", "value": "true"},
       {"name": "Database__ApplyMigrationsOnStartup", "value": "false"},
       {"name": "SyntheticPrivateMarker", "value": "synthetic-secret-value-must-not-log"}]
for branch, code, connection, marker, db, role in (
    ("Directory", "DIRECTORY", "QmsDirectoryRuntime", "0001_business_unit_directory", "synthetic_directory", "directory"),
    ("Units__Cheongju", "CHEONGJU", "QmsCheongjuRuntime", "0086_business_unit_database_identity", "synthetic_cheongju", "cheongju"),
    ("Units__Osan", "OSAN", "QmsOsanRuntime", "0086_business_unit_database_identity", "synthetic_osan", "osan")):
    for field, value in (("Code", code), ("RuntimeConnection", connection), ("ExpectedSchemaVersion", marker), ("ExpectedDatabaseName", db), ("RuntimeRoleName", role + "_app"), ("MigrationRoleName", role + "_migrator")):
        env.append({"name": "BusinessUnits__" + branch + "__" + field, "value": value})
    env.append({"name": "ConnectionStrings__" + connection, "secretRef": "qms-" + role + "-runtime"})
env.append({"name": "ConnectionStrings__QmsDatabase", "secretRef": "qms-cheongju-runtime"})
def set_value(name, value):
    next(item for item in env if item["name"] == name)["value"] = value
if scenario.startswith("backend-config-") or (scenario == "backend-final-invalid" and phase == "revision"):
    failure = scenario.removeprefix("backend-config-")
    if failure in ("disabled", "backend-final-invalid"): set_value("BusinessUnits__Enabled", "false")
    elif failure == "missing-enabled": env = [x for x in env if x["name"] != "BusinessUnits__Enabled"]
    elif failure == "duplicate-case": env.append({"name": "businessunits__enabled", "value": "true"})
    elif failure == "duplicate-colon": env.append({"name": "BusinessUnits:Enabled", "value": "true"})
    elif failure == "missing-runtime": env = [x for x in env if x["name"] != "ConnectionStrings__QmsOsanRuntime"]
    elif failure == "plaintext-runtime":
        item = next(x for x in env if x["name"] == "ConnectionStrings__QmsOsanRuntime")
        item.pop("secretRef"); item["value"] = "synthetic-secret-value-must-not-log"
    elif failure == "swapped-target": set_value("BusinessUnits__Units__Osan__Code", "CHEONGJU")
    elif failure == "wrong-binding": set_value("BusinessUnits__Units__Osan__RuntimeConnection", "QmsCheongjuRuntime")
    elif failure == "duplicate-db": set_value("BusinessUnits__Units__Osan__ExpectedDatabaseName", "synthetic_cheongju")
    elif failure == "system-db": set_value("BusinessUnits__Directory__ExpectedDatabaseName", "postgres")
    elif failure == "duplicate-role": set_value("BusinessUnits__Units__Osan__RuntimeRoleName", "cheongju_app")
    elif failure == "schema-marker": set_value("BusinessUnits__Directory__ExpectedSchemaVersion", "wrong")
    elif failure == "startup-migration": set_value("Database__ApplyMigrationsOnStartup", "true")
    elif failure == "privileged-secret": env.append({"name": "ConnectionStrings__QmsOsanMigration", "secretRef": "osan-migration"})
    elif failure == "reserved": env.append({"name": "database:businessschemaseparationapproved", "value": "true"})
    elif failure == "legacy-alias": next(x for x in env if x["name"] == "ConnectionStrings__QmsDatabase")["secretRef"] = "other-runtime"
    elif failure == "duplicate-secret": next(x for x in env if x["name"] == "ConnectionStrings__QmsOsanRuntime")["secretRef"] = "qms-cheongju-runtime"
container = {"env": env}
if scenario == "backend-config-command": container["args"] = ["--migrate-only"]
containers = [container]
if scenario == "backend-config-containers": containers.append(container)
print(json.dumps({"containers": containers}))
PY_TEMPLATE
}

command_group="${1:-} ${2:-} ${3:-}"
case "${command_group}" in
  'account show --query')
    printf '%s\n' "${AZURE_SUBSCRIPTION_ID}"
    ;;
  'containerapp show --resource-group')
    image_file="${AZURE_RELEASE_TEST_STATE}/${name}-image"
    case "${query}" in
      properties.template) backend_template ;;
      properties.configuration.activeRevisionsMode)
        printf 'Single\n'
        ;;
      properties.latestRevisionName)
        if grep -q '@sha256:' "${image_file}"; then
          printf '%s-new\n' "${name}"
        else
          printf '%s-old\n' "${name}"
        fi
        ;;
      properties.latestReadyRevisionName)
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'baseline-database-unready' \
          && "${name}" == "${BACKEND_APP_NAME}" ]]; then
          printf '\n'
        elif [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'final-app-unready' \
          && "${name}" == "${BACKEND_APP_NAME}" ]] \
          && grep -Fxq 'frontend-update' "${AZURE_RELEASE_TEST_STATE}/calls"; then
          printf '\n'
        elif [[ ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'backend-release-failed' \
            || "${AZURE_RELEASE_TEST_SCENARIO}" == 'database-readiness-failed' ) \
          && "${name}" == "${BACKEND_APP_NAME}" ]] \
          && grep -q '@sha256:' "${image_file}"; then
          printf '%s-old\n' "${name}"
        elif [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'frontend-release-failed' \
          && "${name}" == "${FRONTEND_APP_NAME}" ]] \
          && grep -q '@sha256:' "${image_file}"; then
          printf '%s-old\n' "${name}"
        elif grep -q '@sha256:' "${image_file}"; then
          printf '%s-new\n' "${name}"
        else
          printf '%s-old\n' "${name}"
        fi
        ;;
      properties.provisioningState)
        printf 'Succeeded\n'
        ;;
      'properties.template.containers[0].image')
        sed -n '1p' "${image_file}"
        ;;
      *)
        exit 2
        ;;
    esac
    ;;
  'containerapp revision list')
    case "${query}" in
      '[?properties.active].name')
        [[ "$(cat "${AZURE_RELEASE_TEST_STATE}/${name}-active")" == '0' ]] || printf '%s-old\n' "${name}" ;;
      'length([?properties.active])') cat "${AZURE_RELEASE_TEST_STATE}/${name}-active" ;;
      '[].name') printf '%s-old\n' "${name}" ;;
      *) exit 2 ;;
    esac
    ;;
  'containerapp replica list')
    if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == replicas-not-drained ]]; then printf '1\n'
    else cat "${AZURE_RELEASE_TEST_STATE}/${name}-active"; fi
    ;;
  'containerapp revision deactivate')
    kind="${name#pms-synthetic-}"
    printf '%s-stop\n' "${kind}" >>"${AZURE_RELEASE_TEST_STATE}/calls"
    if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == stop-backend-failed && "${kind}" == backend ]]; then exit 1; fi
    printf '0\n' >"${AZURE_RELEASE_TEST_STATE}/${name}-active"
    ;;
  'containerapp revision activate')
    printf '%s-resume\n' "${name#pms-synthetic-}" >>"${AZURE_RELEASE_TEST_STATE}/calls"
    printf '1\n' >"${AZURE_RELEASE_TEST_STATE}/${name}-active"
    ;;
  'containerapp revision show')
    case "${query}" in
      properties.template) backend_template revision ;;
      properties.healthState)
        printf 'Healthy\n'
        ;;
      properties.runningState)
        if [[ "$(cat "${AZURE_RELEASE_TEST_STATE}/${name}-active")" == '0' ]]; then printf 'Stopped\n'; exit 0; fi
        case "${AZURE_RELEASE_TEST_SCENARIO}" in
          success-running-at-max-scale)
            printf 'RunningAtMaxScale\n'
            ;;
          baseline-stopped)
            printf 'Stopped\n'
            ;;
          baseline-scale-to-zero)
            printf 'ScaleToZero\n'
            ;;
          baseline-degraded)
            printf 'Degraded\n'
            ;;
          baseline-unknown)
            printf 'Unknown\n'
            ;;
          *)
            printf 'Running\n'
            ;;
        esac
        ;;
      *)
        exit 2
        ;;
    esac
    ;;
  'containerapp job show')
    case "${query}" in
      properties.configuration.triggerType)
        printf 'Manual\n'
        ;;
      properties.configuration.replicaRetryLimit)
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == unsafe-retry ]]; then printf '1\n'; else printf '0\n'; fi ;;
      properties.configuration.manualTriggerConfig.parallelism)
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == unsafe-parallel ]]; then printf '2\n'; else printf '1\n'; fi ;;
      properties.configuration.manualTriggerConfig.replicaCompletionCount)
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == unsafe-completion ]]; then printf '2\n'; else printf '1\n'; fi ;;
      'length(properties.template.containers)')
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == unsafe-containers ]]; then printf '2\n'; else printf '1\n'; fi ;;

      'properties.template.containers[0].env[?value != `null` && value != `""`].[name, value]')
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'inspection-config-invalid' ]]; then
          printf 'ASPNETCORE_ENVIRONMENT\tProduction\n'
        else
          printf 'ASPNETCORE_ENVIRONMENT\tProduction\n'
          printf 'BusinessUnits__Enabled\ttrue\n'
          printf 'SyntheticPrivateMarker\tsynthetic-secret-value-must-not-log\n'
          if [[ "${name}" == "${MAINTENANCE_JOB_NAME}" && "${AZURE_RELEASE_TEST_SCENARIO}" == maintenance-stale-* ]]; then
            printf '%s\ttrue\n' "${AZURE_RELEASE_TEST_SCENARIO#maintenance-stale-}"
          fi
          if [[ "${name}" == "${MIGRATION_JOB_NAME}" ]]; then
            case "${AZURE_RELEASE_TEST_SCENARIO}" in
              stale-*) printf '%s\ttrue\n' "${AZURE_RELEASE_TEST_SCENARIO#stale-}" ;;
              duplicate-*) printf '%s\ttrue\n' "${AZURE_RELEASE_TEST_SCENARIO#duplicate-}" ;;
            esac
          fi
        fi
        ;;
      'properties.template.containers[0].env[?secretRef != `null` && secretRef != `""`].[name, secretRef]')
        printf 'ConnectionStrings__QmsDirectoryMigration\tqms-directory-migration\n'
        printf 'ConnectionStrings__QmsCheongjuMigration\tqms-cheongju-migration\n'
        printf 'ConnectionStrings__QmsOsanMigration\tqms-osan-migration\n'
        printf 'ConnectionStrings__QmsCheongjuRuntime\tqms-cheongju-runtime\n'
        printf 'ConnectionStrings__QmsOsanRuntime\tqms-osan-runtime\n'
        printf 'BusinessUnits__MembershipBackfill__ApprovedUserIdsDelimited\tapproved-users\n'
        printf 'BusinessUnits__MembershipBackfill__OverallAdministratorUserIdsDelimited\toverall-administrators\n'
        ;;
      'properties.template.containers[0].resources.cpu')
        printf '0.5\n'
        ;;
      'properties.template.containers[0].resources.memory')
        printf '1Gi\n'
        ;;
      *)
        exit 2
        ;;
    esac
    ;;
  'containerapp job update')
    case "${name}" in
      "${DATABASE_BOOTSTRAP_JOB_NAME}") printf 'bootstrap-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls" ;;
      "${MEMBERSHIP_BACKFILL_JOB_NAME}") printf 'backfill-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls" ;;
      "${MIGRATION_JOB_NAME}") printf 'migration-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls" ;;
      *) exit 2 ;;
    esac
    ;;
  'containerapp job start')
    if [[ "${job_mode}" == drain || "${job_mode}" == migration || "${job_mode}" == bootstrap || "${job_mode}" == backfill ]]; then
      [[ "${expected_postgres_host}" == synthetic-pg.postgres.database.azure.com ]] || exit 2
    else
      [[ -z "${expected_postgres_host}" ]] || exit 2
    fi
    if [[ "${job_mode}" == drain && "${selected_target}" == OSAN && "${AZURE_RELEASE_TEST_SCENARIO}" == mail-exception ]]; then
      [[ "${accepted_mail_snapshot}" == "${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256}" ]] || exit 2
    else
      [[ -z "${accepted_mail_snapshot}" ]] || exit 2
    fi
    if [[ "${name}" == "${MIGRATION_JOB_NAME}" || "${name}" == "${DATABASE_BOOTSTRAP_JOB_NAME}" || "${name}" == "${MAINTENANCE_JOB_NAME}" || "${inspection}" == true || "${job_mode}" == backfill ]]; then
      for preserved_setting in \
        'ASPNETCORE_ENVIRONMENT=Production' \
        'BusinessUnits__Enabled=true' \
        'SyntheticPrivateMarker=synthetic-secret-value-must-not-log' \
        'ConnectionStrings__QmsDirectoryMigration=secretref:qms-directory-migration' \
        'ConnectionStrings__QmsCheongjuMigration=secretref:qms-cheongju-migration' \
        'ConnectionStrings__QmsOsanMigration=secretref:qms-osan-migration' \
        'ConnectionStrings__QmsCheongjuRuntime=secretref:qms-cheongju-runtime' \
        'ConnectionStrings__QmsOsanRuntime=secretref:qms-osan-runtime' \
        'BusinessUnits__MembershipBackfill__ApprovedUserIdsDelimited=secretref:approved-users' \
        'BusinessUnits__MembershipBackfill__OverallAdministratorUserIdsDelimited=secretref:overall-administrators'; do
        preserved_count=0
        for argument in "$@"; do
          [[ "${argument}" != "${preserved_setting}" ]] || preserved_count=$((preserved_count + 1))
        done
        [[ "${preserved_count}" == 1 ]] || exit 2
      done
    fi
    printf '0\n' >"${AZURE_RELEASE_TEST_STATE}/job-forced-failure"
    if [[ "${name}" == "${MIGRATION_JOB_NAME}" || "${name}" == "${DATABASE_BOOTSTRAP_JOB_NAME}" ]]; then
      [[ "$(cat "${AZURE_RELEASE_TEST_STATE}/${BACKEND_APP_NAME}-active")" == '0' \
        && "$(cat "${AZURE_RELEASE_TEST_STATE}/${FRONTEND_APP_NAME}-active")" == '0' \
        ]] || exit 2
      [[ "${image}" == "${BACKEND_RELEASE_IMAGE}" && "${cpu}" == 0.5 && "${memory}" == 1Gi ]] || exit 2
      if [[ "${job_mode}" == drain ]]; then
        [[ "${schema_approved}" == false && "${drain_required}" == true && "${drain_release}" == "${MAINTENANCE_RELEASE_ID}" ]] || exit 2
      else
        [[ "$(paste -sd, "${AZURE_RELEASE_TEST_STATE}/drain-completed")" == DIRECTORY,CHEONGJU,OSAN,DIRECTORY,CHEONGJU,OSAN ]] || exit 2
        python3 - "${AZURE_RELEASE_TEST_STATE}" <<'PY_RECOVERY'
import json, sys
from pathlib import Path
paths=list(Path(sys.argv[1]).glob("pms-recovery-checkpoint.*/recovery.json"))
assert len(paths)==1 and json.loads(paths[0].read_text())["phase"]=="verified"
PY_RECOVERY
        expected_approval=false
        if [[ "${job_mode}" == migration && "${selected_target}" != DIRECTORY ]]; then expected_approval="${BUSINESS_SCHEMA_SEPARATION_APPROVED:-false}"; fi
        [[ "${schema_approved}" == "${expected_approval}" ]] || exit 2
        [[ -z "${drain_required}" && -z "${drain_release}" ]] || exit 2
      fi
    fi
    printf '%s\n' "${job_mode}" >"${AZURE_RELEASE_TEST_STATE}/job-mode"
    case "${name}" in
      "${DATABASE_BOOTSTRAP_JOB_NAME}")
        [[ "${selected_target}" =~ ^(DIRECTORY|CHEONGJU|OSAN)$ ]] || exit 2
        printf 'bootstrap-start-%s\n' "${selected_target}" >>"${AZURE_RELEASE_TEST_STATE}/calls" ;;
      "${MEMBERSHIP_BACKFILL_JOB_NAME}")
        if [[ "${inspection}" == 'true' ]]; then
          if [[ "${inspection_environment_count}" -ne 7 \
            || "${cpu}" != '0.5' || "${memory}" != '1Gi' ]]; then
            exit 2
          fi
          printf 'backfill-inspect-start\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
        else
          [[ "$job_mode" == backfill && "$image" == "$BACKEND_RELEASE_IMAGE" && "$cpu" == 0.5 && "$memory" == 1Gi ]] || exit 2
          printf 'backfill-start\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
        fi
        ;;
      "${MIGRATION_JOB_NAME}")
        [[ "${selected_target}" =~ ^(DIRECTORY|CHEONGJU|OSAN)$ ]] || exit 2
        printf '%s-start-%s\n' "${job_mode}" "${selected_target}" >>"${AZURE_RELEASE_TEST_STATE}/calls" ;;
      "${MAINTENANCE_JOB_NAME}")
        [[ "${selected_target}" =~ ^(CHEONGJU|OSAN)$ ]] || exit 2
        [[ "${maintenance_action}" =~ ^(prepare|verify-prepared|activate|delay|fail|complete)$ \
          && "${cpu}" == '0.5' && "${memory}" == '1Gi' ]] || exit 2
        if [[ "${maintenance_action}" == 'complete' && "${DEPLOY_BACKEND}" == 'true' ]]; then
          [[ "${image}" == "${BACKEND_RELEASE_IMAGE}" ]] || exit 2
        elif [[ "${maintenance_action}" == complete ]]; then
          [[ "${image}" == 'pilotacr123.azurecr.io/pms-backend:cccccccccccccccccccccccccccccccccccccccc' ]] || exit 2
        elif [[ -n "${MAINTENANCE_PREPARATION_IMAGE:-}" ]]; then
          [[ "${image}" == "$MAINTENANCE_PREPARATION_IMAGE" ]] || exit 2
        elif [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
          [[ "${image}" == "${BACKEND_RELEASE_IMAGE}" ]] || exit 2
        else
          [[ "${image}" == 'pilotacr123.azurecr.io/pms-backend:cccccccccccccccccccccccccccccccccccccccc' ]] || exit 2
        fi
        printf 'maintenance-%s-%s\n' "${maintenance_action}" "${selected_target}" >>"${AZURE_RELEASE_TEST_STATE}/calls"
        printf '%s\n' "${maintenance_action}" >"${AZURE_RELEASE_TEST_STATE}/maintenance-action"
        state_file="${AZURE_RELEASE_TEST_STATE}/maintenance-${selected_target}"
        state="$(cat "${state_file}")"
        injected_failure='false'
        [[ "${AZURE_RELEASE_TEST_SCENARIO}" == cleanup-fail-running && "${maintenance_action}" == activate && "${selected_target}" == OSAN ]] && injected_failure='true'
        [[ "${AZURE_RELEASE_TEST_SCENARIO}" == "maintenance-${maintenance_action}-failed" ]] && injected_failure='true'
        [[ "${AZURE_RELEASE_TEST_SCENARIO}" == "maintenance-${maintenance_action}-osan-failed" && "${selected_target}" == OSAN ]] && injected_failure='true'
        [[ "${AZURE_RELEASE_TEST_SCENARIO}" == cached-healthy-database-unready && "${maintenance_action}" == complete ]] && injected_failure='true'
        if [[ "${injected_failure}" == false ]]; then
          case "${maintenance_action}:${state}" in
            prepare:None|prepare:Announced) next_state=Announced ;;
            verify-prepared:Announced) next_state=Announced ;;
            activate:Announced) next_state=Active ;;
            complete:Active|complete:Failed) next_state=Completed ;;
            fail:Announced|fail:Active|fail:Completed|fail:Failed) next_state=Failed ;;
            *) next_state="${state}"; printf '1\n' >"${AZURE_RELEASE_TEST_STATE}/job-forced-failure" ;;
          esac
          printf '%s\n' "${next_state}" >"${state_file}"
        fi
        ;;
      *) exit 2 ;;
    esac
    if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == maintenance-prepare-start-uncertain && "${maintenance_action}" == prepare ]]; then exit 1; fi
    if [[ "${job_mode}" == drain && "${AZURE_RELEASE_TEST_SCENARIO}" == "drain-${selected_target}-start-uncertain" ]]; then exit 1; fi
    if [[ "$job_mode" == drain ]]; then
      printf 'synthetic-drain-%s\n' "$(( $(wc -l <"${AZURE_RELEASE_TEST_STATE}/drain-completed") + 1 ))"
    else
      printf 'synthetic-execution-%s\n' "${selected_target}"
    fi
    ;;
  'containerapp job execution')
    if [[ "${query}" == properties.endTime ]]; then
      [[ "${AZURE_RELEASE_TEST_SCENARIO}" != recovery-missing-endtime ]] || exit 0
      python3 -c 'from datetime import datetime, timezone; print(datetime.now(timezone.utc).isoformat())'
      exit 0
    fi
    if [[ "$(cat "${AZURE_RELEASE_TEST_STATE}/job-mode")" == drain ]]; then
      case "$(( ( ${execution_name#synthetic-drain-} - 1 ) % 3 ))" in
        0) target=DIRECTORY ;; 1) target=CHEONGJU ;; 2) target=OSAN ;;
      esac
      case "${AZURE_RELEASE_TEST_SCENARIO}" in
        "drain-${target}-failed") printf 'Failed\n'; exit 0 ;;
        "drain-${target}-unknown") printf 'Unknown\n'; exit 0 ;;
        "drain-${target}-running") printf 'Running\n'; exit 0 ;;
      esac
      if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == recovery-final-drain-failed && "$(wc -l <"${AZURE_RELEASE_TEST_STATE}/drain-completed")" -ge 3 ]]; then
        printf 'Failed\n'; exit 0
      fi
      printf '%s\n' "${target}" >>"${AZURE_RELEASE_TEST_STATE}/drain-completed"
      printf 'Succeeded\n'; exit 0
    fi
    if [[ "$(cat "${AZURE_RELEASE_TEST_STATE}/job-forced-failure")" == 1 ]]; then printf 'Failed\n'; exit 0; fi
    if [[ "${name}" == "${MAINTENANCE_JOB_NAME}" ]]; then
      current_action="$(cat "${AZURE_RELEASE_TEST_STATE}/maintenance-action")"
      if [[ "${execution_name}" == *-OSAN && "${current_action}" == complete ]]; then
        [[ "${AZURE_RELEASE_TEST_SCENARIO}" != maintenance-complete-osan-status-unknown ]] || exit 1
        if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == maintenance-complete-osan-running ]]; then printf 'Running\n'; exit 0; fi
      fi
      if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == cleanup-fail-running ]]; then
        if [[ "${current_action}" == activate && "${execution_name}" == *-OSAN ]]; then printf 'Failed\n'; exit 0; fi
        if [[ "${current_action}" == fail && "${execution_name}" == *-CHEONGJU ]]; then printf 'Running\n'; exit 0; fi
      fi
    fi
    if [[ ( ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'maintenance-activate-osan-failed' || "${AZURE_RELEASE_TEST_SCENARIO}" == 'maintenance-prepare-osan-failed' || "${AZURE_RELEASE_TEST_SCENARIO}" == 'maintenance-complete-osan-failed' )
          && "${name}" == "${MAINTENANCE_JOB_NAME}" && "${execution_name}" == *-OSAN
          && "${AZURE_RELEASE_TEST_SCENARIO}" == "maintenance-$(cat "${AZURE_RELEASE_TEST_STATE}/maintenance-action")-osan-failed" ) \
      || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'migration-cheongju-failed'
          && "${name}" == "${MIGRATION_JOB_NAME}" && "${execution_name}" == *-CHEONGJU ) \
      || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'bootstrap-failed' \
          && "${name}" == "${DATABASE_BOOTSTRAP_JOB_NAME}" ) \
      || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'migration-failed' \
          && "${name}" == "${MIGRATION_JOB_NAME}" ) \
      || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'backfill-failed' \
          && "${name}" == "${MEMBERSHIP_BACKFILL_JOB_NAME}" ) \
      || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'inspection-failed' \
          && "${name}" == "${MEMBERSHIP_BACKFILL_JOB_NAME}" ) \
      || ( "${name}" == "${MAINTENANCE_JOB_NAME}" \
          && ( "${AZURE_RELEASE_TEST_SCENARIO}" == "maintenance-$(cat "${AZURE_RELEASE_TEST_STATE}/maintenance-action")-failed" \
            || ( "${AZURE_RELEASE_TEST_SCENARIO}" == 'cached-healthy-database-unready' \
              && "$(cat "${AZURE_RELEASE_TEST_STATE}/maintenance-action")" == 'complete' ) ) ) ]]; then
      printf 'Failed\n'
    else
      printf 'Succeeded\n'
    fi
    ;;
  'containerapp job logs')
    printf 'backfill-inspect-logs\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
    if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'inspection-evidence-missing' ]]; then
      printf 'inspection log marker missing\n'
    else
      printf 'businessUnitMembershipBackfillDryRun=PASS identityCount=23 overallAdministratorCount=3 configuredOverallAdministratorCount=3 activeDirectoryOverallAdministratorCount=3 activateMembershipCount=0 deactivateMembershipCount=1 normalizeDepartmentDefaultRoleCount=0 removeManagedRoleCount=0 resetDepartmentHeadCount=0 repairOverallProfileCount=0 designateOverallAdministratorCount=0 cheongjuSystemAdminPermissionGapCount=0 osanSystemAdminPermissionGapCount=0\n'
    fi
    ;;
  'containerapp update --resource-group')
    printf '1\n' >"${AZURE_RELEASE_TEST_STATE}/${name}-active"
    image_file="${AZURE_RELEASE_TEST_STATE}/${name}-image"
    if [[ "${name}" == "${BACKEND_APP_NAME}" ]]; then
      if [[ "${image}" == "${BACKEND_RELEASE_IMAGE}" ]]; then
        printf 'backend-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      else
        printf 'backend-rollback\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      fi
    elif [[ "${name}" == "${FRONTEND_APP_NAME}" ]]; then
      if [[ "${image}" == "${FRONTEND_RELEASE_IMAGE}" ]]; then
        printf 'frontend-update\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      else
        printf 'frontend-rollback\n' >>"${AZURE_RELEASE_TEST_STATE}/calls"
      fi
    else
      exit 2
    fi
    printf '%s\n' "${image}" >"${image_file}"
    ;;
  *)
    exit 2
    ;;
esac
MOCK_AZ

cat >"${temporary_directory}/curl" <<'MOCK_CURL'
#!/usr/bin/env bash
set -euo pipefail

url="${!#}"
frontend_image="$(sed -n '1p' "${AZURE_RELEASE_TEST_STATE}/${FRONTEND_APP_NAME}-image")"
if [[ "${AZURE_RELEASE_TEST_SCENARIO}" == 'public-security-failed' \
  && "${frontend_image}" == *'@sha256:'* \
  && "${url}" == */health/live ]]; then
  printf '500'
elif [[ "${url}" == */health/live ]]; then
  printf '200'
else
  printf '401'
fi
MOCK_CURL

chmod 700 "${temporary_directory}/az" "${temporary_directory}/curl"

backend_digest="$(printf 'a%.0s' {1..64})"
frontend_digest="$(printf 'b%.0s' {1..64})"
case_number=0

run_case() {
  local scenario="$1"
  local expected_exit="$2"
  local expected_code="$3"
  local expected_calls="$4"
  local deploy_backend="${5:-true}"
  local deploy_frontend="${6:-true}"
  local run_migration="${7:-true}"
  local run_database_bootstrap="${8:-false}"
  local run_membership_backfill="${9:-false}"
  local inspect_membership_backfill="${10:-false}"
  local publish=true prepared=false prepare_only=false preparation_image=''
  local accepted_mail_snapshot=''
  [[ "${scenario}" != mail-exception ]] || accepted_mail_snapshot="$(printf '1%.0s' {1..64})"
  [[ "${scenario}" != invalid-mail-exception ]] || accepted_mail_snapshot='*'
  local schema_environment=()
  case "$scenario" in
    approval-true) schema_environment=(BUSINESS_SCHEMA_SEPARATION_APPROVED=true) ;;
    malformed-approval) schema_environment=(BUSINESS_SCHEMA_SEPARATION_APPROVED=TRUE) ;;
    popup-only-prepare) publish=false; prepare_only=true; preparation_image="pilotacr123.azurecr.io/pms-backend@sha256:${backend_digest}" ;;
    popup-only-prepared) publish=false; prepared=true; preparation_image="pilotacr123.azurecr.io/pms-backend@sha256:${backend_digest}" ;;
    popup-only-missing-image) publish=false ;;
  esac
  case_number=$((case_number + 1))

  printf '%s\n' 'pilotacr123.azurecr.io/pms-backend:cccccccccccccccccccccccccccccccccccccccc' \
    >"${temporary_directory}/pms-synthetic-backend-image"
  printf '%s\n' 'pilotacr123.azurecr.io/pms-frontend:dddddddddddddddddddddddddddddddddddddddd' \
    >"${temporary_directory}/pms-synthetic-frontend-image"
  if [[ "${scenario}" == 'unsafe-rollback' ]]; then
    printf '%s\n' 'pilotacr123.azurecr.io/pms-backend:latest' \
      >"${temporary_directory}/pms-synthetic-backend-image"
  fi
  rm -rf "${temporary_directory}"/pms-recovery-checkpoint.*
  rm -f "${temporary_directory}/recovery-backup.json" "${temporary_directory}/recovery-reads" "${temporary_directory}/step-output"
  : >"${temporary_directory}/calls"
  : >"${temporary_directory}/maintenance-action"
  printf '1\n' >"${temporary_directory}/pms-synthetic-backend-active"
  printf '1\n' >"${temporary_directory}/pms-synthetic-frontend-active"
  printf 'None\n' >"${temporary_directory}/maintenance-CHEONGJU"
  printf 'None\n' >"${temporary_directory}/maintenance-OSAN"
  if [[ "${prepared}" == true ]]; then
    printf 'Announced\n' >"${temporary_directory}/maintenance-CHEONGJU"
    printf 'Announced\n' >"${temporary_directory}/maintenance-OSAN"
  fi
  : >"${temporary_directory}/drain-completed"
  : >"${temporary_directory}/job-mode"

  set +e
  env -u BUSINESS_SCHEMA_SEPARATION_APPROVED ${schema_environment[@]+"${schema_environment[@]}"} \
    ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256="${accepted_mail_snapshot}" \
    RECOVERY_EVIDENCE_ENCRYPTION_REQUIRED=true \
    RECOVERY_EVIDENCE_CERTIFICATE_PEM="$([[ "$scenario" != recovery-no-certificate ]] && printf '%s' "$recovery_certificate")" \
    GITHUB_OUTPUT="${temporary_directory}/step-output" \
    TMPDIR="${temporary_directory}" \
    RECOVERY_MOCK_SCRIPT="${repository_root}/scripts/test-support/azure-recovery-mock.py" \
    RECOVERY_POSTGRES_SERVER_NAME='synthetic-pg' \
    RECOVERY_CHECKPOINT_TIMEOUT_SECONDS="$([[ "$scenario" == recovery-timeout ]] && printf 1 || printf 10)" \
    RECOVERY_CHECKPOINT_POLL_SECONDS=1 \
    SOURCE_SHA='1111111111111111111111111111111111111111' \
    AZURE_SUBSCRIPTION_ID='33333333-3333-4333-8333-333333333333' \
    ACR_LOGIN_SERVER='pilotacr123.azurecr.io' \
    PUBLIC_HOSTNAME='pms.synthetic.internal' \
    AZURE_RESOURCE_GROUP='pms-synthetic-rg' \
    BACKEND_APP_NAME='pms-synthetic-backend' \
    FRONTEND_APP_NAME='pms-synthetic-frontend' \
    MIGRATION_JOB_NAME='pms-synthetic-migration' \
    DATABASE_BOOTSTRAP_JOB_NAME='pms-synthetic-bootstrap' \
    MEMBERSHIP_BACKFILL_JOB_NAME='pms-synthetic-backfill' \
    MAINTENANCE_JOB_NAME='pms-synthetic-maintenance' \
    MAINTENANCE_RELEASE_ID='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa' \
    MAINTENANCE_ACTOR_USER_ID='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' \
    MAINTENANCE_PUBLISH_NOTICE="$publish" \
    MAINTENANCE_PREPARE_ONLY="$prepare_only" \
    MAINTENANCE_PREPARED="$prepared" \
    MAINTENANCE_PREPARATION_IMAGE="$preparation_image" \
    MAINTENANCE_TITLE='Synthetic release' \
    MAINTENANCE_BODY='Synthetic deployment notice' \
    MAINTENANCE_STARTS_AT_UTC='2099-09-24T07:00:00Z' \
    MAINTENANCE_EXPECTED_ENDS_AT_UTC='2099-09-24T07:30:00Z' \
    BACKEND_RELEASE_IMAGE="pilotacr123.azurecr.io/pms-backend@sha256:${backend_digest}" \
    FRONTEND_RELEASE_IMAGE="pilotacr123.azurecr.io/pms-frontend@sha256:${frontend_digest}" \
    DEPLOY_BACKEND="${deploy_backend}" \
    DEPLOY_FRONTEND="${deploy_frontend}" \
    RUN_MIGRATION="${run_migration}" \
    RUN_DATABASE_BOOTSTRAP="${run_database_bootstrap}" \
    RUN_MEMBERSHIP_BACKFILL="${run_membership_backfill}" \
    INSPECT_MEMBERSHIP_BACKFILL="${inspect_membership_backfill}" \
    AZURE_RELEASE_AZ_BIN="${temporary_directory}/az" \
    AZURE_RELEASE_HTTP_BIN="${temporary_directory}/curl" \
    AZURE_RELEASE_ALLOW_TEST_OVERRIDES='true' \
    AZURE_RELEASE_POLL_ATTEMPTS='2' \
    AZURE_RELEASE_POLL_INTERVAL_SECONDS='0' \
    AZURE_RELEASE_TEST_STATE="${temporary_directory}" \
    AZURE_RELEASE_TEST_SCENARIO="${scenario}" \
    "${release_script}" \
    >"${temporary_directory}/stdout" \
    2>"${temporary_directory}/stderr"
  actual_exit="$?"
  set -e

  if [[ "${actual_exit}" -ne "${expected_exit}" ]]; then
    sed -n '1,20p' "${temporary_directory}/stderr" >&2
    printf 'azurePilotReleaseTests=UNEXPECTED_EXIT_%s_EXPECTED_%s_ACTUAL_%s\n' \
      "${case_number}" "${expected_exit}" "${actual_exit}" >&2
    exit 1
  fi
  if [[ -n "${expected_code}" ]] \
    && ! grep -Fxq "azurePilotRelease=${expected_code}" "${temporary_directory}/stderr"; then
    printf 'azurePilotReleaseTests=UNEXPECTED_FAILURE_CODE_%s\n' "${case_number}" >&2
    exit 1
  fi
  if [[ "$scenario" == popup-only-prepare ]]; then
    expected_calls=maintenance-prepare
  elif [[ "$scenario" == popup-only-missing-image ]]; then
    expected_calls=''
  elif [[ "${scenario}" == 'maintenance-prepare-failed' ]]; then
    expected_calls='maintenance-prepare,maintenance-fail'
  elif [[ "${scenario}" == 'maintenance-activate-failed' ]]; then
    expected_calls='maintenance-prepare,maintenance-activate,maintenance-fail'
  elif [[ "${scenario}" == 'maintenance-prepare-osan-failed' ]]; then
    expected_calls='maintenance-prepare,maintenance-fail'
  elif [[ "${scenario}" == backend-config-* ]]; then
    expected_calls=''
  elif [[ "${scenario}" == maintenance-stale-* || "${scenario}" == malformed-approval || "${scenario}" == invalid-mail-exception || "${scenario}" == unsafe-retry || "${scenario}" == unsafe-parallel || "${scenario}" == unsafe-completion || "${scenario}" == unsafe-containers ]]; then
    expected_calls=''
  elif [[ "${scenario}" == 'maintenance-activate-osan-failed' ]]; then
    expected_calls='maintenance-prepare,maintenance-activate,maintenance-fail'
  elif [[ "${expected_code}" != 'INVALID_RELEASE_SCOPE' \
    && "${expected_code}" != 'MIGRATION_REQUIRES_BACKEND_RELEASE' \
    && ( "${deploy_backend}" == 'true' || "${deploy_frontend}" == 'true' \
      || "${run_migration}" == 'true' || "${run_database_bootstrap}" == 'true' \
      || "${run_membership_backfill}" == 'true' ) \
    && "${scenario}" != baseline-* && "${scenario}" != 'unsafe-rollback' ]]; then
    expected_calls="maintenance-prepare,maintenance-activate,${expected_calls}"
    if [[ "${expected_exit}" -eq 0 ]]; then
      expected_calls="${expected_calls},maintenance-complete"
    elif [[ "${scenario}" == 'maintenance-complete-failed' || "${scenario}" == 'maintenance-complete-osan-failed' \
      || "${scenario}" == 'cached-healthy-database-unready' ]]; then
      expected_calls="${expected_calls},maintenance-complete,maintenance-fail"
    else
      expected_calls="${expected_calls},maintenance-fail"
    fi
  fi
  if [[ "$scenario" == popup-only-prepared ]]; then expected_calls="${expected_calls/maintenance-prepare,/maintenance-verify-prepared,}"; fi
  if [[ "${run_migration}" == true ]]; then
    # Quiesce before the first database operation, and leave every revision
    # stopped after crossing that boundary when release/health fails.
    if [[ "${scenario}" == stop-backend-failed || "${scenario}" == drain-* || "${scenario}" == stale-* || "${scenario}" == duplicate-* || "${scenario}" == replicas-not-drained ]]; then
      expected_calls='maintenance-prepare,maintenance-activate,frontend-stop'
      [[ "${scenario}" == replicas-not-drained ]] || expected_calls="${expected_calls},backend-stop"
      if [[ "${scenario}" == drain-* ]]; then expected_calls="${expected_calls},drain-start"; fi
      expected_calls="${expected_calls},backend-resume,frontend-resume,maintenance-fail"
    elif [[ ",${expected_calls}," == *,migration-update,* || ",${expected_calls}," == *,bootstrap-update,* ]]; then
      if [[ "${run_database_bootstrap}" == true ]]; then
        expected_calls="${expected_calls/bootstrap-update/frontend-stop,backend-stop,drain-start,final-drain-start,bootstrap-update}"
      else
        expected_calls="${expected_calls/migration-update/frontend-stop,backend-stop,drain-start,final-drain-start,migration-update}"
      fi
      if [[ "${expected_exit}" == 0 && "${deploy_frontend}" == false ]]; then
        expected_calls="${expected_calls/maintenance-complete/frontend-resume,maintenance-complete}"
      fi
      if [[ "${expected_exit}" != 0 ]]; then
        stops=''
        [[ ",${expected_calls}," != *,frontend-update,* ]] || stops='frontend-stop,'
        [[ ",${expected_calls}," != *,backend-update,* ]] || stops="${stops}backend-stop,"
        expected_calls="${expected_calls/maintenance-fail/${stops}maintenance-fail}"
      fi
    fi
  fi
  if [[ "$scenario" == recovery-wrong-server || "$scenario" == recovery-no-certificate ]]; then
    expected_calls=''
  elif [[ "$scenario" == recovery-* ]]; then
    expected_calls='maintenance-prepare,maintenance-activate,frontend-stop,backend-stop,drain-start'
    if [[ "$scenario" == recovery-final-* ]]; then expected_calls="${expected_calls},final-drain-start"; fi
    expected_calls="${expected_calls},backend-resume,frontend-resume,maintenance-fail"
    [[ "$(cat "${temporary_directory}/pms-synthetic-backend-active")" == 1 && "$(cat "${temporary_directory}/pms-synthetic-frontend-active")" == 1 ]] || exit 1
  fi
  # Assert the actual target and ordering of each execution, including failure
  # in the second business and stopping before a later database is touched.
  local expanded='' event target_list selected
  IFS=',' read -r -a expected_events <<<"${expected_calls}"
  for event in "${expected_events[@]-}"; do
    [[ -n "${event}" ]] || continue
    target_list=''
    case "${event}" in
      maintenance-*)
        target_list='CHEONGJU OSAN'
        if [[ "${scenario}" == "${event}-failed" || ( "${scenario}" == cached-healthy-database-unready && "${event}" == maintenance-complete ) ]]; then
          target_list='CHEONGJU'
        fi
        ;;
      bootstrap-start)
        target_list='DIRECTORY CHEONGJU OSAN'
        [[ "${scenario}" == bootstrap-failed ]] && target_list='DIRECTORY'
        ;;
      final-drain-start)
        event=drain-start
        target_list='DIRECTORY CHEONGJU OSAN'
        [[ "$scenario" != recovery-final-drain-failed ]] || target_list='DIRECTORY'
        ;;
      drain-start)
        target_list='DIRECTORY CHEONGJU OSAN'
        [[ "$scenario" != recovery-missing-endtime ]] || target_list='DIRECTORY'
        [[ "${scenario}" != drain-DIRECTORY-* ]] || target_list='DIRECTORY'
        [[ "${scenario}" != drain-CHEONGJU-* ]] || target_list='DIRECTORY CHEONGJU'
        ;;
      migration-start)
        target_list='DIRECTORY CHEONGJU OSAN'
        [[ "${scenario}" == migration-failed ]] && target_list='DIRECTORY'
        [[ "${scenario}" == migration-cheongju-failed ]] && target_list='DIRECTORY CHEONGJU'
        ;;
      backend-rollback|frontend-rollback)
        [[ "${run_migration}" == true ]] && continue
        ;;
    esac
    if [[ -z "${target_list}" ]]; then
      expanded="${expanded:+${expanded},}${event}"
    else
      for selected in ${target_list}; do
        expanded="${expanded:+${expanded},}${event}-${selected}"
      done
    fi
  done
  expected_calls="${expanded}"
  local uncertain=false
  case "${scenario}" in
    maintenance-prepare-start-uncertain)
      uncertain=true
      expected_calls='maintenance-prepare-CHEONGJU,frontend-stop,backend-stop'
      ;;
    maintenance-complete-osan-running|maintenance-complete-osan-status-unknown)
      uncertain=true
      expected_calls='maintenance-prepare-CHEONGJU,maintenance-prepare-OSAN,maintenance-activate-CHEONGJU,maintenance-activate-OSAN,frontend-update,maintenance-complete-CHEONGJU,maintenance-complete-OSAN,frontend-stop,backend-stop'
      ;;
    cleanup-fail-running)
      uncertain=true
      expected_calls='maintenance-prepare-CHEONGJU,maintenance-prepare-OSAN,maintenance-activate-CHEONGJU,maintenance-activate-OSAN,maintenance-fail-CHEONGJU,frontend-stop,backend-stop'
      ;;
  esac
  if [[ "${uncertain}" == true ]]; then
    [[ "$(cat "${temporary_directory}/pms-synthetic-backend-active")" == 0 && "$(cat "${temporary_directory}/pms-synthetic-frontend-active")" == 0 ]] || exit 1
    grep -Fq EXECUTION_UNCERTAIN_MANUAL_RECONCILIATION_REQUIRED "${temporary_directory}/stderr" || exit 1
    # A delayed complete can still finish after the command exits. It cannot
    # reopen traffic because both apps remain stopped and no fail raced it.
    if [[ "${scenario}" == maintenance-complete-osan-* ]]; then
      printf 'Completed\n' >"${temporary_directory}/maintenance-OSAN"
      [[ "$(cat "${temporary_directory}/pms-synthetic-backend-active")" == 0 && "$(cat "${temporary_directory}/pms-synthetic-frontend-active")" == 0 ]] || exit 1
    fi
  fi
  if [[ "$run_migration" == true && "$expected_exit" == 0 && "$prepare_only" != true ]]; then
    python3 - "$temporary_directory" <<'PY_EVIDENCE'
from pathlib import Path
import json, sys, subprocess
root=Path(sys.argv[1])
files=list(root.glob('pms-recovery-checkpoint.*/recovery.json'))
assert len(files)==1
state=json.loads(files[0].read_text())
assert state['phase']=='verified' and len(set(state['allowedDrains']))==6
key, value=(root/'step-output').read_text().strip().split('=', 1)
assert key=='recovery_evidence' and Path(value).resolve()==files[0].with_suffix('.p7m').resolve()
result=subprocess.run(['openssl','cms','-decrypt','-binary','-inform','DER','-in',value,
                       '-recip',str(root/'recipient.pem'),'-inkey',str(root/'recipient.key')], capture_output=True)
assert result.returncode==0 and result.stdout==files[0].read_bytes()
assert state['binding']['serverId'].encode() not in Path(value).read_bytes()
assert files[0].stat().st_mode & 0o777 == 0o600
PY_EVIDENCE
  fi
  if [[ "${expected_exit}" == 0 && "${expected_calls}" == *maintenance-complete* ]]; then
    [[ "$(cat "${temporary_directory}/maintenance-CHEONGJU")" == Completed && "$(cat "${temporary_directory}/maintenance-OSAN")" == Completed ]] || exit 1
  elif [[ "${expected_exit}" != 0 && "${expected_calls}" == *maintenance-fail* && "${uncertain}" == false ]]; then
    for selected in CHEONGJU OSAN; do
      state="$(cat "${temporary_directory}/maintenance-${selected}")"
      [[ "${state}" == Failed || "${state}" == None ]] || exit 1
    done
  fi
  if [[ "${expected_exit}" != 0 && "${run_migration}" == true && "${expected_calls}" == *migration-start* ]]; then
    [[ "$(cat "${temporary_directory}/pms-synthetic-backend-active")" == 0 && "$(cat "${temporary_directory}/pms-synthetic-frontend-active")" == 0 ]] || exit 1
  fi
  if grep -Fq 'synthetic-secret-value-must-not-log' "${temporary_directory}/stdout" "${temporary_directory}/stderr"; then
    printf 'azurePilotReleaseTests=SECRET_LEAK\n' >&2; exit 1
  fi
  if [[ "$(paste -sd, "${temporary_directory}/calls")" != "${expected_calls}" ]]; then
    printf 'expected=%s\nactual=%s\n' "${expected_calls}" "$(paste -sd, "${temporary_directory}/calls")" >&2
    printf 'azurePilotReleaseTests=UNEXPECTED_CALL_ORDER_%s\n' "${case_number}" >&2
    exit 1
  fi
  printf 'azurePilotReleaseTest=%s:%s:PASS\n' "${case_number}" "${scenario}"
}

for failure in disabled missing-enabled duplicate-case duplicate-colon missing-runtime plaintext-runtime swapped-target wrong-binding duplicate-db system-db duplicate-role schema-marker startup-migration privileged-secret reserved legacy-alias duplicate-secret command containers; do
  run_case "backend-config-${failure}" 68 BACKEND_SERVING_CONFIGURATION_INVALID ''
done
run_case backend-final-invalid 1 BACKEND_SERVING_CONFIGURATION_INVALID 'migration-update,migration-start,backend-update,frontend-update'
run_case 'popup-only-prepare' 0 '' ''
run_case 'popup-only-prepared' 0 '' 'migration-update,migration-start,backend-update,frontend-update'
run_case 'popup-only-prepared' 0 '' 'frontend-update' false true false
run_case 'popup-only-missing-image' 65 MAINTENANCE_PREPARATION_IMAGE_REQUIRED ''
run_case 'success' 0 '' \
  'migration-update,migration-start,backend-update,frontend-update'
run_case 'success-running-at-max-scale' 0 '' \
  'migration-update,migration-start,backend-update,frontend-update'
run_case 'maintenance-prepare-failed' 79 MAINTENANCE_PREPARE_FAILED ''
run_case 'maintenance-activate-failed' 79 MAINTENANCE_ACTIVATION_FAILED ''
run_case 'maintenance-prepare-start-uncertain' 79 MAINTENANCE_PREPARE_FAILED ''
run_case 'maintenance-complete-osan-running' 80 MAINTENANCE_RELEASE_FAILED 'frontend-update' false true false
run_case 'maintenance-complete-osan-status-unknown' 80 MAINTENANCE_RELEASE_FAILED 'frontend-update' false true false
run_case 'cleanup-fail-running' 79 MAINTENANCE_ACTIVATION_FAILED ''
run_case 'maintenance-prepare-osan-failed' 79 MAINTENANCE_PREPARE_FAILED ''
run_case 'stop-backend-failed' 79 QUIESCENCE_OR_DRAIN_FAILED ''
run_case 'replicas-not-drained' 79 QUIESCENCE_OR_DRAIN_FAILED ''
for target in DIRECTORY CHEONGJU OSAN; do
  for failure in failed unknown running start-uncertain; do
    run_case "drain-${target}-${failure}" 79 QUIESCENCE_OR_DRAIN_FAILED ''
  done
done
for stale in Database__RecoveryPostgresHost Database:RecoveryPostgresHost database__recoverypostgreshost database:recoverypostgreshost DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256 DeploymentDrain:AcceptedHistoricalOsanMailAttemptSha256 Database__MigrationTarget Database__BootstrapTarget Database__BusinessSchemaSeparationApproved DeploymentDrain__RequireMaintenance DeploymentDrain__ReleaseId; do
  run_case "stale-${stale}" 79 QUIESCENCE_OR_DRAIN_FAILED ''
done
# Environment providers fold casing and normalize __ to :. These spellings
# must not retain a release approval or create an ambiguous duplicate setting.
for stale in database__migrationtarget dAtAbAsE__BootstrapTarget database__businessschemaseparationapproved deploymentdrain__requiremaintenance DeploymentDrain__releaseid \
  Database:MigrationTarget Database:BootstrapTarget Database:BusinessSchemaSeparationApproved DeploymentDrain:RequireMaintenance DeploymentDrain:ReleaseId; do
  run_case "stale-${stale}" 79 QUIESCENCE_OR_DRAIN_FAILED ''
done
for duplicate in BusinessUnits__Enabled businessunits__enabled BusinessUnits:Enabled businessunits:enabled; do
  run_case "duplicate-${duplicate}" 79 QUIESCENCE_OR_DRAIN_FAILED ''
done
for shape in retry parallel completion containers; do
  run_case "unsafe-${shape}" 79 MAINTENANCE_JOB_CONFIGURATION_INVALID ''
done
run_case malformed-approval 65 INVALID_RELEASE_SCOPE ''
run_case invalid-mail-exception 65 INVALID_HISTORICAL_MAIL_SNAPSHOT ''
run_case mail-exception 0 '' 'migration-update,migration-start,backend-update,frontend-update'
for stale in Maintenance__BusinessUnit maintenance__businessunit Maintenance:BusinessUnit maintenance:releaseid; do
  run_case "maintenance-stale-${stale}" 79 MAINTENANCE_JOB_CONFIGURATION_INVALID ''
done
run_case approval-true 0 '' 'bootstrap-update,bootstrap-start,migration-update,migration-start,backend-update,frontend-update' true true true true
run_case 'maintenance-complete-osan-failed' 80 MAINTENANCE_RELEASE_FAILED 'migration-update,migration-start,backend-update,frontend-update'
run_case 'maintenance-activate-osan-failed' 79 MAINTENANCE_ACTIVATION_FAILED ''
run_case 'migration-cheongju-failed' 74 MIGRATION_FAILED 'migration-update,migration-start'
run_case 'maintenance-complete-failed' 80 MAINTENANCE_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,frontend-update'
run_case 'success' 0 '' \
  'bootstrap-update,bootstrap-start,migration-update,migration-start,backfill-update,backfill-start,backend-update,frontend-update' \
  true true true true true
run_case 'success' 65 MIGRATION_REQUIRES_BACKEND_RELEASE '' \
  false false true true true
run_case 'baseline-stopped' 70 BASELINE_NOT_READY ''
run_case 'baseline-scale-to-zero' 70 BASELINE_NOT_READY ''
run_case 'baseline-degraded' 70 BASELINE_NOT_READY ''
run_case 'baseline-unknown' 70 BASELINE_NOT_READY ''
run_case 'baseline-database-unready' 70 BASELINE_NOT_READY ''
run_case 'unsafe-rollback' 69 UNSAFE_ROLLBACK_BASELINE ''
run_case 'bootstrap-failed' 73 DATABASE_BOOTSTRAP_FAILED \
  'bootstrap-update,bootstrap-start' true true true true true
run_case 'migration-failed' 74 MIGRATION_FAILED \
  'migration-update,migration-start'
run_case 'backfill-failed' 76 MEMBERSHIP_BACKFILL_FAILED \
  'bootstrap-update,bootstrap-start,migration-update,migration-start,backfill-update,backfill-start' \
  true true true true true
run_case 'success' 0 '' \
  'backfill-inspect-start,backfill-inspect-logs' false false false false false true
run_case 'inspection-failed' 77 MEMBERSHIP_BACKFILL_INSPECTION_FAILED \
  'backfill-inspect-start' false false false false false true
run_case 'inspection-evidence-missing' 78 MEMBERSHIP_BACKFILL_INSPECTION_EVIDENCE_MISSING \
  'backfill-inspect-start,backfill-inspect-logs,backfill-inspect-logs,backfill-inspect-logs,backfill-inspect-logs,backfill-inspect-logs,backfill-inspect-logs' \
  false false false false false true
run_case 'inspection-config-invalid' 79 MEMBERSHIP_BACKFILL_INSPECTION_CONFIGURATION_INVALID \
  '' false false false false false true
run_case 'backend-release-failed' 1 BACKEND_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,backend-rollback'
run_case 'database-readiness-failed' 1 BACKEND_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,backend-rollback'
run_case 'frontend-release-failed' 1 FRONTEND_RELEASE_FAILED \
  'migration-update,migration-start,backend-update,frontend-update,frontend-rollback,backend-rollback'
run_case 'public-security-failed' 1 PUBLIC_SECURITY_SMOKE_FAILED \
  'migration-update,migration-start,backend-update,frontend-update,frontend-rollback,backend-rollback'
run_case 'final-app-unready' 1 FINAL_APP_NOT_READY \
  'migration-update,migration-start,backend-update,frontend-update,frontend-rollback,backend-rollback'
run_case 'backend-release-failed' 1 BACKEND_RELEASE_FAILED 'backend-update,backend-rollback' true false false
run_case 'success' 0 '' 'backend-update' true false false
run_case 'success' 0 '' 'frontend-update' false true false
run_case 'success' 0 '' 'migration-update,migration-start,backend-update' true false true
run_case 'success' 65 MIGRATION_REQUIRES_BACKEND_RELEASE '' false false true
run_case 'cached-healthy-database-unready' 80 MAINTENANCE_RELEASE_FAILED \
  'frontend-update' false true false
run_case 'success' 65 INVALID_RELEASE_SCOPE '' true false false true false
run_case 'success' 65 INVALID_RELEASE_SCOPE '' true false false false true
run_case 'success' 65 INVALID_RELEASE_SCOPE '' false false true false true true
run_case 'success' 0 '' '' false false false

run_case recovery-no-certificate 79 RECOVERY_PREFLIGHT_FAILED ''
run_case recovery-wrong-server 79 RECOVERY_PREFLIGHT_FAILED ''
run_case recovery-missing-endtime 79 QUIESCENCE_OR_DRAIN_FAILED ''
for scenario in recovery-timeout recovery-read-failed recovery-foreign-backup recovery-future-backup recovery-missing-time recovery-incomplete-list recovery-evidence-failed recovery-active-app recovery-job-running recovery-terminal-job recovery-final-drain-failed recovery-final-backup-missing; do
  run_case "$scenario" 79 RECOVERY_CHECKPOINT_FAILED ''
done

printf 'azurePilotReleaseTests=PASS cases=%s\n' "${case_number}"
