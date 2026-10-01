#!/usr/bin/env bash
set -euo pipefail

required_environment=(
  SOURCE_SHA
  AZURE_SUBSCRIPTION_ID
  ACR_LOGIN_SERVER
  PUBLIC_HOSTNAME
  AZURE_RESOURCE_GROUP
  BACKEND_APP_NAME
  FRONTEND_APP_NAME
  MIGRATION_JOB_NAME
  DATABASE_BOOTSTRAP_JOB_NAME
  MEMBERSHIP_BACKFILL_JOB_NAME
  DEPLOY_BACKEND
  DEPLOY_FRONTEND
  RUN_MIGRATION
  RUN_DATABASE_BOOTSTRAP
  RUN_MEMBERSHIP_BACKFILL
  INSPECT_MEMBERSHIP_BACKFILL
)

for variable_name in "${required_environment[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'azurePilotRelease=MISSING_CONFIGURATION\n' >&2
    exit 63
  fi
done

BUSINESS_SCHEMA_SEPARATION_APPROVED="${BUSINESS_SCHEMA_SEPARATION_APPROVED:-false}"
ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256="${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256:-}"
if [[ -n "${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256}" \
  && ! "${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256}" =~ ^[0-9a-f]{64}$ ]]; then
  printf 'azurePilotRelease=INVALID_HISTORICAL_MAIL_SNAPSHOT\n' >&2
  exit 65
fi

for release_flag in \
  "${BUSINESS_SCHEMA_SEPARATION_APPROVED}" \
  "${DEPLOY_BACKEND}" \
  "${DEPLOY_FRONTEND}" \
  "${RUN_MIGRATION}" \
  "${RUN_DATABASE_BOOTSTRAP}" \
  "${RUN_MEMBERSHIP_BACKFILL}" \
  "${INSPECT_MEMBERSHIP_BACKFILL}"; do
  if [[ "${release_flag}" != 'true' && "${release_flag}" != 'false' ]]; then
    printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
    exit 65
  fi
done

if [[ ( "${RUN_DATABASE_BOOTSTRAP}" == 'true' || "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ) \
  && "${RUN_MIGRATION}" != 'true' ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
  exit 65
fi

if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' \
  && "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_SCOPE\n' >&2
  exit 65
fi

# Exact migration-ledger validation in the live backend requires the matching
# backend image. Reject database-only migrations before any Azure mutation.
if [[ "${RUN_MIGRATION}" == 'true' && "${DEPLOY_BACKEND}" != 'true' ]]; then
  printf 'azurePilotRelease=MIGRATION_REQUIRES_BACKEND_RELEASE\n' >&2
  exit 65
fi

if [[ "${DEPLOY_BACKEND}" == 'false' \
  && "${DEPLOY_FRONTEND}" == 'false' \
  && "${RUN_MIGRATION}" == 'false' \
  && "${RUN_DATABASE_BOOTSTRAP}" == 'false' \
  && "${RUN_MEMBERSHIP_BACKFILL}" == 'false' \
  && "${INSPECT_MEMBERSHIP_BACKFILL}" == 'false' ]]; then
  printf 'azurePilotRelease=NO_CHANGES\n'
  exit 0
fi

# Public releases require a pre-provisioned maintenance job. The previous backend
# image must already contain its CLI; first-time bootstrap is a separate rollout.
maintenance_release='false'
if [[ "${DEPLOY_BACKEND}" == 'true' || "${DEPLOY_FRONTEND}" == 'true' \
  || "${RUN_MIGRATION}" == 'true' || "${RUN_DATABASE_BOOTSTRAP}" == 'true' \
  || "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  maintenance_release='true'
  for variable_name in MAINTENANCE_JOB_NAME MAINTENANCE_RELEASE_ID \
    MAINTENANCE_ACTOR_USER_ID MAINTENANCE_TITLE MAINTENANCE_BODY \
    MAINTENANCE_STARTS_AT_UTC MAINTENANCE_EXPECTED_ENDS_AT_UTC; do
    if [[ -z "${!variable_name:-}" ]]; then
      printf 'azurePilotRelease=MAINTENANCE_CONFIGURATION_MISSING\n' >&2
      exit 63
    fi
  done
fi

MAINTENANCE_PUBLISH_NOTICE="${MAINTENANCE_PUBLISH_NOTICE:-true}"
MAINTENANCE_PREPARE_ONLY="${MAINTENANCE_PREPARE_ONLY:-false}"
MAINTENANCE_PREPARED="${MAINTENANCE_PREPARED:-false}"
for flag in "$MAINTENANCE_PUBLISH_NOTICE" "$MAINTENANCE_PREPARE_ONLY" "$MAINTENANCE_PREPARED"; do
  if [[ "$flag" != true && "$flag" != false ]]; then
    printf 'azurePilotRelease=INVALID_MAINTENANCE_MODE\n' >&2; exit 65
  fi
done
if [[ "$MAINTENANCE_PREPARE_ONLY" == true && "$MAINTENANCE_PREPARED" == true ]]; then
  printf 'azurePilotRelease=INVALID_MAINTENANCE_MODE\n' >&2; exit 65
fi
if [[ ( "$MAINTENANCE_PUBLISH_NOTICE" == false || "$MAINTENANCE_PREPARED" == true ) && -z "${MAINTENANCE_PREPARATION_IMAGE:-}" ]]; then
  printf 'azurePilotRelease=MAINTENANCE_PREPARATION_IMAGE_REQUIRED\n' >&2; exit 65
fi

azure_cli_bin="${AZURE_RELEASE_AZ_BIN:-az}"
http_client_bin="${AZURE_RELEASE_HTTP_BIN:-curl}"
poll_attempts="${AZURE_RELEASE_POLL_ATTEMPTS:-60}"
poll_interval_seconds="${AZURE_RELEASE_POLL_INTERVAL_SECONDS:-10}"

if [[ "${azure_cli_bin}" != 'az' || "${http_client_bin}" != 'curl' ]]; then
  if [[ "${AZURE_RELEASE_ALLOW_TEST_OVERRIDES:-false}" != 'true' \
    || "${PUBLIC_HOSTNAME}" != 'pms.synthetic.internal' ]]; then
    printf 'azurePilotRelease=COMMAND_OVERRIDE_REJECTED\n' >&2
    exit 64
  fi
fi

if [[ ! "${poll_attempts}" =~ ^[1-9][0-9]{0,2}$ \
  || ! "${poll_interval_seconds}" =~ ^[0-9]{1,2}$ ]]; then
  printf 'azurePilotRelease=INVALID_POLL_CONFIGURATION\n' >&2
  exit 65
fi

digest_pattern='sha256:[0-9a-f]{64}'
if [[ ( "${DEPLOY_BACKEND}" == 'true' \
    || "${RUN_MIGRATION}" == 'true' \
    || "${RUN_DATABASE_BOOTSTRAP}" == 'true' \
    || "${RUN_MEMBERSHIP_BACKFILL}" == 'true' \
    || "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ) \
  && ( "${BACKEND_RELEASE_IMAGE:-}" != "${ACR_LOGIN_SERVER}/pms-backend@"* \
    || ! "${BACKEND_RELEASE_IMAGE:-}" =~ @${digest_pattern}$ ) ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_IMAGE\n' >&2
  exit 66
fi
if [[ "${DEPLOY_FRONTEND}" == 'true' \
  && ( "${FRONTEND_RELEASE_IMAGE:-}" != "${ACR_LOGIN_SERVER}/pms-frontend@"* \
    || ! "${FRONTEND_RELEASE_IMAGE:-}" =~ @${digest_pattern}$ ) ]]; then
  printf 'azurePilotRelease=INVALID_RELEASE_IMAGE\n' >&2
  exit 66
fi

task_tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/pms-azure-release.XXXXXX")"
cleanup() {
  local exit_status="$?"
  if [[ "${exit_status}" -ne 0 && ( "${quiescing:-false}" == 'true' || "${maintenance_uncertain:-false}" == 'true' ) ]]; then
    if [[ "${migration_started:-false}" == 'true' || "${maintenance_uncertain:-false}" == 'true' ]]; then
      stop_app "${FRONTEND_APP_NAME}" || printf 'azurePilotReleaseStop=FRONTEND_FAILED\n' >&2
      stop_app "${BACKEND_APP_NAME}" || printf 'azurePilotReleaseStop=BACKEND_FAILED\n' >&2
    else
      restore_baseline_revisions || printf 'azurePilotReleaseRestore=FAILED\n' >&2
    fi
  fi
  if [[ "${maintenance_active:-false}" == 'true' && "${exit_status}" -ne 0 && "${maintenance_uncertain:-false}" != 'true' ]]; then
    if ! run_maintenance_job fail; then
      printf 'azurePilotReleaseMaintenance=FAILURE_STATE_UPDATE_FAILED\n' >&2
    fi
  fi
  if [[ "${maintenance_uncertain:-false}" == 'true' ]]; then
    # A delayed prepare/complete/fail may still mutate its database. Keep every
    # entry point stopped and never race it with another maintenance execution.
    stop_app "${FRONTEND_APP_NAME}" || printf 'azurePilotReleaseStop=FRONTEND_FAILED\n' >&2
    stop_app "${BACKEND_APP_NAME}" || printf 'azurePilotReleaseStop=BACKEND_FAILED\n' >&2
    printf 'azurePilotReleaseMaintenance=EXECUTION_UNCERTAIN_MANUAL_RECONCILIATION_REQUIRED\n' >&2
  fi
  rm -f "${task_tmp_dir}/command-output" "${task_tmp_dir}/command-error" "${task_tmp_dir}/backend-template.json"
  rmdir "${task_tmp_dir}" 2>/dev/null || true
}
if [[ -n "${MAINTENANCE_PREPARATION_IMAGE:-}" && ( "$MAINTENANCE_PREPARATION_IMAGE" != "${ACR_LOGIN_SERVER}/pms-backend@"* || ! "$MAINTENANCE_PREPARATION_IMAGE" =~ @${digest_pattern}$ ) ]]; then
  printf 'azurePilotRelease=INVALID_MAINTENANCE_PREPARATION_IMAGE\n' >&2; exit 66
fi


trap cleanup EXIT


azure_read() {
  "${azure_cli_bin}" "$@" -o tsv \
    2>"${task_tmp_dir}/command-error"
}

azure_mutate() {
  "${azure_cli_bin}" "$@" -o none \
    >"${task_tmp_dir}/command-output" \
    2>"${task_tmp_dir}/command-error"
}

job_override_environment=()
job_override_cpu=''
job_override_memory=''
job_override_configuration_error='not-loaded'
maintenance_job_environment=()
maintenance_job_cpu=''
maintenance_job_memory=''
maintenance_active='false'
maintenance_uncertain='false'
job_terminal='false'
recovery_helper="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/azure-recovery-checkpoint.py"
recovery_state=''
recovery_postgres_host=''
recovery_drained_at=''
recovery_drains=()

run_recovery_checkpoint() {
  local output status phase
  if output="$(python3 "${recovery_helper}" "$@" 2>&1)"; then
    printf '%s\n' "${output}"
    return 0
  else
    status=$?
  fi
  if [[ "${output}" =~ ^recoveryCheckpoint=FAILED_NO_DATABASE_CHANGE_ALLOWED[[:space:]]phase=(preflight|arm|wait|verify)[[:space:]]reason=([A-Z][A-Z0-9_]{0,63})$ ]]; then
    phase="${BASH_REMATCH[1]}"
    if [[ "${1:-}" == "${phase}" ]]; then
      printf '%s\n' "${output}" >&2
    fi
  fi
  return "${status}"
}

load_job_execution_override() {
  local job_name="$1" purpose="${2:-backfill}"
  local retry_limit parallelism completion_count container_count
  local environment_values environment_secret_refs environment_name environment_value
  local required_environment_name configured_environment_name found
  local normalized_environment_name seen_environment_names=$'\n'
  local production_environment='false' business_units_enabled='false'

  job_override_environment=()
  job_override_cpu=''
  job_override_memory=''
  job_override_configuration_error='read-values'

  retry_limit="$(azure_read containerapp job show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" --query properties.configuration.replicaRetryLimit)" || return 1
  parallelism="$(azure_read containerapp job show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" --query properties.configuration.manualTriggerConfig.parallelism)" || return 1
  completion_count="$(azure_read containerapp job show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" --query properties.configuration.manualTriggerConfig.replicaCompletionCount)" || return 1
  container_count="$(azure_read containerapp job show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" --query 'length(properties.template.containers)')" || return 1
  if [[ "${retry_limit}" != 0 || "${parallelism}" != 1 || "${completion_count}" != 1 || "${container_count}" != 1 ]]; then
    job_override_configuration_error='unsafe-job-execution-shape'
    return 1
  fi

  # shellcheck disable=SC2016 # Backticks are JMESPath JSON literals.
  environment_values="$(azure_read containerapp job show \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" \
    --query 'properties.template.containers[0].env[?value != `null` && value != `""`].[name, value]')" \
    || return 1
  job_override_configuration_error='read-secret-refs'
  # shellcheck disable=SC2016 # Backticks are JMESPath JSON literals.
  environment_secret_refs="$(azure_read containerapp job show \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" \
    --query 'properties.template.containers[0].env[?secretRef != `null` && secretRef != `""`].[name, secretRef]')" \
    || return 1
  job_override_configuration_error='read-cpu'
  job_override_cpu="$(azure_read containerapp job show \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" \
    --query 'properties.template.containers[0].resources.cpu')" \
    || return 1
  job_override_configuration_error='read-memory'
  job_override_memory="$(azure_read containerapp job show \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${job_name}" \
    --query 'properties.template.containers[0].resources.memory')" \
    || return 1

  job_override_configuration_error='invalid-shape'

  if [[ -z "${environment_values}" || -z "${environment_secret_refs}" \
    || ! "${job_override_cpu}" =~ ^[0-9]+([.][0-9]+)?$ \
    || ! "${job_override_memory}" =~ ^[0-9]+([.][0-9]+)?(Mi|Gi)$ ]]; then
    return 1
  fi

  while IFS=$'\t' read -r environment_name environment_value; do
    if [[ ! "${environment_name}" =~ ^[A-Za-z_][A-Za-z0-9_]*$ \
      || -z "${environment_value}" ]]; then
      return 1
    fi
    job_override_environment+=("${environment_name}=${environment_value}")
  done <<<"${environment_values}"

  job_override_configuration_error='invalid-secret-ref'
  while IFS=$'\t' read -r environment_name environment_value; do
    if [[ ! "${environment_name}" =~ ^[A-Za-z_][A-Za-z0-9_]*$ \
      || ! "${environment_value}" =~ ^[a-z0-9-]+$ ]]; then
      return 1
    fi
    job_override_environment+=("${environment_name}=secretref:${environment_value}")
  done <<<"${environment_secret_refs}"

  for configured_environment_name in "${job_override_environment[@]}"; do
    normalized_environment_name="$(printf '%s' "${configured_environment_name%%=*}" | tr '[:upper:]' '[:lower:]')"
    normalized_environment_name="${normalized_environment_name//__/:}"
    if [[ "${seen_environment_names}" == *$'\n'"${normalized_environment_name}"$'\n'* ]]; then
      job_override_configuration_error='duplicate-environment-key'
      return 1
    fi
    seen_environment_names+="${normalized_environment_name}"$'\n'
    case "${normalized_environment_name}" in
      database:migrationtarget|database:bootstraptarget|database:businessschemaseparationapproved|database:recoverypostgreshost|deploymentdrain:*|maintenance:*)
        job_override_configuration_error='stale-execution-configuration'
        return 1 ;;
    esac
    [[ "${configured_environment_name}" == 'ASPNETCORE_ENVIRONMENT=Production' ]] \
      && production_environment='true'
    [[ "${configured_environment_name}" == 'BusinessUnits__Enabled=true' ]] \
      && business_units_enabled='true'
  done
  if [[ "${production_environment}" != 'true' || "${business_units_enabled}" != 'true' ]]; then
    job_override_configuration_error='invalid-required-environment-value'
    return 1
  fi

  for required_environment_name in \
    ASPNETCORE_ENVIRONMENT \
    BusinessUnits__Enabled \
    ConnectionStrings__QmsDirectoryMigration \
    ConnectionStrings__QmsCheongjuMigration \
    ConnectionStrings__QmsOsanMigration; do
    found='false'
    for configured_environment_name in "${job_override_environment[@]}"; do
      if [[ "${configured_environment_name%%=*}" == "${required_environment_name}" ]]; then
        found='true'
        break
      fi
    done
    if [[ "${found}" != 'true' ]]; then
      job_override_configuration_error='missing-required-environment'
      return 1
    fi
  done

  if [[ "${purpose}" == 'backfill' ]]; then
    for required_environment_name in \
      BusinessUnits__MembershipBackfill__ApprovedUserIdsDelimited \
      BusinessUnits__MembershipBackfill__OverallAdministratorUserIdsDelimited; do
      found='false'
      for configured_environment_name in "${job_override_environment[@]}"; do
        [[ "${configured_environment_name%%=*}" == "${required_environment_name}" ]] && found='true'
      done
      if [[ "${found}" != 'true' ]]; then
        job_override_configuration_error='missing-required-environment'
        return 1
      fi
    done
  fi
  job_override_configuration_error='none'
}

# Inspect configuration metadata only; never resolve or print secret values.
validate_backend_serving_configuration() {
  local revision="${1:-}"
  local configuration_file="${task_tmp_dir}/backend-template.json"
  local read_command=(containerapp show --resource-group "${AZURE_RESOURCE_GROUP}" --name "${BACKEND_APP_NAME}")
  if [[ -n "${revision}" ]]; then
    read_command=(containerapp revision show --resource-group "${AZURE_RESOURCE_GROUP}" --name "${BACKEND_APP_NAME}" --revision "${revision}")
  fi
  if ! "${azure_cli_bin}" "${read_command[@]}" --query properties.template -o json \
    >"${configuration_file}" 2>"${task_tmp_dir}/command-error"; then
    return 1
  fi
  python3 - "${configuration_file}" <<'PY_BACKEND'
import json, re, sys
# Assertions are validation here; an optimized Python must fail closed.
if sys.flags.optimize:
    sys.exit(1)
try:
    template = json.load(open(sys.argv[1], encoding="utf-8"))
    containers = template["containers"]
    assert len(containers) == 1
    container = containers[0]
    assert not container.get("command") and not container.get("args")
    env = {}
    for item in container["env"]:
        name = item["name"]
        assert re.fullmatch(r"[A-Za-z_][A-Za-z0-9_:]*", name)
        key = name.replace("__", ":").lower()
        assert key not in env
        assert (item.get("value") is not None) != (item.get("secretRef") is not None)
        env[key] = item
        assert key not in {"database:migrationtarget", "database:bootstraptarget", "database:businessschemaseparationapproved", "database:recoverypostgreshost"}
        assert not key.startswith(("deploymentdrain:", "maintenance:"))
        if key.startswith("connectionstrings:"):
            assert re.fullmatch(r"[a-z0-9-]+", item.get("secretRef", ""))
            assert key in {"connectionstrings:qmsdatabase", "connectionstrings:qmsdirectoryruntime", "connectionstrings:qmscheongjuruntime", "connectionstrings:qmsosanruntime"}
    def value(key):
        return env[key]["value"]
    def secret(key):
        return env[key]["secretRef"]
    assert value("aspnetcore_environment") == "Production"
    assert value("businessunits:enabled") == "true"
    assert value("database:applymigrationsonstartup") == "false"
    databases, runtime_roles, migration_roles, references = [], [], [], []
    for branch, code, connection, marker in (
        ("directory", "DIRECTORY", "QmsDirectoryRuntime", "0001_business_unit_directory"),
        ("units:cheongju", "CHEONGJU", "QmsCheongjuRuntime", "0086_business_unit_database_identity"),
        ("units:osan", "OSAN", "QmsOsanRuntime", "0086_business_unit_database_identity")):
        prefix = "businessunits:" + branch + ":"
        assert value(prefix + "code") == code
        assert value(prefix + "runtimeconnection") == connection
        assert value(prefix + "expectedschemaversion") == marker
        database = value(prefix + "expecteddatabasename")
        assert re.fullmatch(r"[a-z][a-z0-9_]{0,62}", database)
        assert database not in {"postgres", "template0", "template1", "azure_sys", "azure_maintenance"}
        databases.append(database)
        for field, values in (("runtimerolename", runtime_roles), ("migrationrolename", migration_roles)):
            role = value(prefix + field)
            assert re.fullmatch(r"[a-z][a-z0-9_]{0,62}", role)
            values.append(role)
        references.append(secret("connectionstrings:" + connection.lower()))
    assert len(set(databases)) == len(set(references)) == 3
    assert len(set(runtime_roles + migration_roles)) == 6
    # The current workload retains this legacy alias. It must be the same C
    # runtime reference and cannot substitute for the explicit split binding.
    if "connectionstrings:qmsdatabase" in env:
        assert secret("connectionstrings:qmsdatabase") == secret("connectionstrings:qmscheongjuruntime")
except (AssertionError, AttributeError, KeyError, TypeError, ValueError, OSError):
    sys.exit(1)
PY_BACKEND
}

public_status() {
  local path="$1"
  "${http_client_bin}" \
    --silent \
    --show-error \
    --output /dev/null \
    --write-out '%{http_code}' \
    --max-time 20 \
    "https://${PUBLIC_HOSTNAME}${path}" \
    2>"${task_tmp_dir}/command-error"
}

wait_for_app() {
  local app_name="$1"
  local expected_image="$2"
  local attempt latest_revision ready_revision provisioning_state image health_state running_state

  for ((attempt = 1; attempt <= poll_attempts; attempt++)); do
    latest_revision="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.latestRevisionName)" || latest_revision=''
    ready_revision="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.latestReadyRevisionName)" || ready_revision=''
    provisioning_state="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query properties.provisioningState)" || provisioning_state=''
    image="$(azure_read containerapp show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" \
      --query 'properties.template.containers[0].image')" || image=''

    if [[ -n "${latest_revision}" && "${latest_revision}" == "${ready_revision}" \
      && "${provisioning_state}" == 'Succeeded' && "${image}" == "${expected_image}" ]]; then
      health_state="$(azure_read containerapp revision show \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --name "${app_name}" \
        --revision "${latest_revision}" \
        --query properties.healthState)" || health_state=''
      running_state="$(azure_read containerapp revision show \
        --resource-group "${AZURE_RESOURCE_GROUP}" \
        --name "${app_name}" \
        --revision "${latest_revision}" \
        --query properties.runningState)" || running_state=''
      if [[ "${health_state}" == 'Healthy' \
        && ( "${running_state}" == 'Running' \
          || "${running_state}" == 'RunningAtMaxScale' ) ]]; then
        return 0
      fi
    fi

    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done

  return 1
}

wait_for_job() {
  local job_name="$1"
  local execution_name="$2"
  local attempt execution_status
  job_terminal='false'

  for ((attempt = 1; attempt <= poll_attempts; attempt++)); do
    execution_status="$(azure_read containerapp job execution show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${job_name}" \
      --job-execution-name "${execution_name}" \
      --query properties.status)" || execution_status=''

    if [[ "${execution_status}" == 'Succeeded' ]]; then
      job_terminal='true'
      return 0
    fi
    if [[ "${execution_status}" == 'Failed' || "${execution_status}" == 'Stopped' ]]; then
      job_terminal='true'
      return 1
    fi
    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done

  return 1
}

stop_app() {
  local app_name="$1" active_revisions all_revisions revision count attempt stopped
  active_revisions="$(azure_read containerapp revision list --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${app_name}" --query '[?properties.active].name')" || return 1
  while IFS= read -r revision; do
    [[ -n "${revision}" ]] || continue
    [[ "${revision}" =~ ^[a-zA-Z0-9-]+$ ]] || return 1
    azure_mutate containerapp revision deactivate --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" --revision "${revision}" || return 1
  done <<<"${active_revisions}"
  for ((attempt = 1; attempt <= poll_attempts; attempt++)); do
    count="$(azure_read containerapp revision list --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" --query 'length([?properties.active])')" || return 1
    stopped='true'
    [[ "${count}" == '0' ]] || stopped='false'
    all_revisions="$(azure_read containerapp revision list --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${app_name}" --query '[].name')" || return 1
    while IFS= read -r revision; do
      [[ -n "${revision}" ]] || continue
      [[ "${revision}" =~ ^[a-zA-Z0-9-]+$ ]] || return 1
      count="$(azure_read containerapp replica list --resource-group "${AZURE_RESOURCE_GROUP}" \
        --name "${app_name}" --revision "${revision}" --query 'length(@)')" || return 1
      [[ "${count}" == '0' ]] || stopped='false'
    done <<<"${all_revisions}"
    [[ "${stopped}" == 'true' ]] && return 0
    [[ "${poll_interval_seconds}" -eq 0 ]] || sleep "${poll_interval_seconds}"
  done
  return 1
}

restore_baseline_revisions() {
  azure_mutate containerapp revision activate --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${BACKEND_APP_NAME}" --revision "${previous_backend_revision}" || return 1
  azure_mutate containerapp revision activate --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" --revision "${previous_frontend_revision}" || return 1
  wait_for_app "${BACKEND_APP_NAME}" "${previous_backend_image}" \
    && wait_for_app "${FRONTEND_APP_NAME}" "${previous_frontend_image}"
}

quiesce_apps() {
  previous_backend_revision="$(azure_read containerapp show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${BACKEND_APP_NAME}" --query properties.latestReadyRevisionName)" || return 1
  previous_frontend_revision="$(azure_read containerapp show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" --query properties.latestReadyRevisionName)" || return 1
  [[ "${previous_backend_revision}" =~ ^[a-zA-Z0-9-]+$ \
    && "${previous_frontend_revision}" =~ ^[a-zA-Z0-9-]+$ ]] || return 1
  quiescing='true'
  stop_app "${FRONTEND_APP_NAME}" && stop_app "${BACKEND_APP_NAME}" \
    && run_drain_check
}

run_maintenance_job_for_target() {
  local action="$1" business_target="$2"
  local execution_name='' verified='false' maintenance_image="${MAINTENANCE_PREPARATION_IMAGE:-${previous_backend_image}}"
  # The new release CLI understands fixed targets, including preparation before
  # migration. An older multi-target CLI must not be invoked once per target.
  if [[ "${DEPLOY_BACKEND}" == 'true' && -z "${MAINTENANCE_PREPARATION_IMAGE:-}" ]]; then
    maintenance_image="${BACKEND_RELEASE_IMAGE}"
  fi
  if [[ "${action}" == 'complete' ]]; then
    maintenance_image="${previous_backend_image}"
    verified='true'
    if [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
      maintenance_image="${BACKEND_RELEASE_IMAGE}"
    fi
  fi
  execution_name="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MAINTENANCE_JOB_NAME}" \
    --container-name "${MAINTENANCE_JOB_NAME}" \
    --image "${maintenance_image}" \
    --cpu "${maintenance_job_cpu}" \
    --memory "${maintenance_job_memory}" \
    --env-vars "${maintenance_job_environment[@]}" \
      "Maintenance__BusinessUnit=${business_target}" \
      "Maintenance__ReleaseId=${MAINTENANCE_RELEASE_ID}" \
      "Maintenance__ActorUserId=${MAINTENANCE_ACTOR_USER_ID}" \
      "Maintenance__Title=${MAINTENANCE_TITLE}" \
      "Maintenance__Body=${MAINTENANCE_BODY}" \
      "Maintenance__StartsAtUtc=${MAINTENANCE_STARTS_AT_UTC}" \
      "Maintenance__ExpectedEndsAtUtc=${MAINTENANCE_EXPECTED_ENDS_AT_UTC}" \
      "Maintenance__Verified=${verified}" \
      "Maintenance__PublishNotice=${MAINTENANCE_PUBLISH_NOTICE}" \
    --args="--maintenance-${action}" \
    --query name)" || execution_name=''
  if [[ -z "${execution_name}" || "${execution_name}" =~ [[:space:]] ]]; then
    maintenance_uncertain='true'
    printf 'azurePilotReleaseMaintenance=START_UNCERTAIN target=%s\n' "${business_target}" >&2
    return 1
  fi
  if wait_for_job "${MAINTENANCE_JOB_NAME}" "${execution_name}"; then
    return 0
  fi
  if [[ "${job_terminal}" != 'true' ]]; then
    maintenance_uncertain='true'
    printf 'azurePilotReleaseMaintenance=STATUS_UNCERTAIN target=%s execution=%s\n' "${business_target}" "${execution_name}" >&2
  fi
  return 1
}

run_maintenance_job() {
  local action="$1" business_target failed='false'
  for business_target in CHEONGJU OSAN; do
    if ! run_maintenance_job_for_target "${action}" "${business_target}"; then
      failed='true'
      # Failure marking must still reach the other business after a partial
      # activation/completion. Ordinary actions stop on the first uncertainty.
      [[ "${action}" == 'fail' && "${maintenance_uncertain}" != 'true' ]] || return 1
    fi
  done
  [[ "${failed}" == 'false' ]]
}

run_drain_check() {
  local database_target execution_name
  recovery_drains=()
  local -a accepted_snapshot_environment
  load_job_execution_override "${MIGRATION_JOB_NAME}" database || return 1
  for database_target in DIRECTORY CHEONGJU OSAN; do
    accepted_snapshot_environment=()
    if [[ "${database_target}" == 'OSAN' && -n "${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256}" ]]; then
      accepted_snapshot_environment+=("DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256=${ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256}")
    fi
    execution_name="$(azure_read containerapp job start \
      --resource-group "${AZURE_RESOURCE_GROUP}" --name "${MIGRATION_JOB_NAME}" \
      --container-name "${MIGRATION_JOB_NAME}" --image "${BACKEND_RELEASE_IMAGE}" \
      --cpu "${job_override_cpu}" --memory "${job_override_memory}" \
      --env-vars "${job_override_environment[@]}" \
      "Database__MigrationTarget=${database_target}" \
      "Database__BusinessSchemaSeparationApproved=false" \
      "DeploymentDrain__RequireMaintenance=true" \
      "DeploymentDrain__ReleaseId=${MAINTENANCE_RELEASE_ID}" \
      "Database__RecoveryPostgresHost=${recovery_postgres_host}" \
      ${accepted_snapshot_environment[@]+"${accepted_snapshot_environment[@]}"} \
      --args=--deployment-drain-check --query name)" || execution_name=''
    [[ -n "${execution_name}" && ! "${execution_name}" =~ [[:space:]] ]] \
      && wait_for_job "${MIGRATION_JOB_NAME}" "${execution_name}" || return 1
    recovery_drained_at="$(azure_read containerapp job execution show \
      --resource-group "${AZURE_RESOURCE_GROUP}" --name "${MIGRATION_JOB_NAME}" \
      --job-execution-name "${execution_name}" --query properties.endTime)" || return 1
    [[ -n "${recovery_drained_at}" ]] || return 1
    recovery_drains+=(--drain-execution "${execution_name}")
  done
}

run_database_job() {
  local job_name="$1" setting="$2" command="$3" database_target execution_name configured schema_approved
  load_job_execution_override "${job_name}" database || return 1
  for configured in "${job_override_environment[@]}"; do
    [[ "${configured%%=*}" != "${setting}" ]] || return 1
  done
  for database_target in DIRECTORY CHEONGJU OSAN; do
    # Preserve the whole configured environment and secret references; only this
    # execution receives the selected target. The stored template has no default.
    migration_started='true'
    schema_approved='false'
    if [[ "${command}" == '--migrate-only' && "${database_target}" != DIRECTORY ]]; then
      schema_approved="${BUSINESS_SCHEMA_SEPARATION_APPROVED}"
    fi
    execution_name="$(azure_read containerapp job start \
      --resource-group "${AZURE_RESOURCE_GROUP}" --name "${job_name}" \
      --container-name "${job_name}" --image "${BACKEND_RELEASE_IMAGE}" \
      --cpu "${job_override_cpu}" --memory "${job_override_memory}" \
      --env-vars "${job_override_environment[@]}" "${setting}=${database_target}" \
      "Database__BusinessSchemaSeparationApproved=${schema_approved}" \
      "Database__RecoveryPostgresHost=${recovery_postgres_host}" \
      --args="${command}" --query name)" || execution_name=''
    [[ -n "${execution_name}" && ! "${execution_name}" =~ [[:space:]] ]] \
      && wait_for_job "${job_name}" "${execution_name}" || return 1
  done
}

migration_started='false'
quiescing='false'
previous_backend_revision=''
previous_frontend_revision=''
previous_backend_image=''
previous_frontend_image=''
backend_changed='false'
frontend_changed='false'

rollback_apps() {
  local rollback_failed='false'

  if [[ "${migration_started}" == 'true' ]]; then
    # The selected schema may already have advanced, including after a lost
    # start response. Keep maintenance closed for a forward fix.
    printf 'azurePilotReleaseRollback=FORWARD_FIX_REQUIRED\n' >&2
    return 1
  fi

  if [[ "${frontend_changed}" == 'true' && -n "${previous_frontend_image}" ]]; then
    if ! azure_mutate containerapp update \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${FRONTEND_APP_NAME}" \
      --image "${previous_frontend_image}" \
      || ! wait_for_app "${FRONTEND_APP_NAME}" "${previous_frontend_image}"; then
      rollback_failed='true'
    fi
  fi

  if [[ "${backend_changed}" == 'true' && -n "${previous_backend_image}" ]]; then
    if ! azure_mutate containerapp update \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${BACKEND_APP_NAME}" \
      --image "${previous_backend_image}" \
      || ! wait_for_app "${BACKEND_APP_NAME}" "${previous_backend_image}"; then
      rollback_failed='true'
    fi
  fi

  if [[ "${rollback_failed}" == 'true' ]]; then
    printf 'azurePilotReleaseRollback=FAILED\n' >&2
    return 1
  fi

  printf 'azurePilotReleaseRollback=PASS\n' >&2
  return 0
}

fail_after_mutation() {
  local code="$1"
  rollback_apps || true
  printf 'azurePilotRelease=%s\n' "${code}" >&2
  exit 1
}

signed_in_subscription="$(azure_read account show --query id)" || signed_in_subscription=''
if [[ "${signed_in_subscription}" != "${AZURE_SUBSCRIPTION_ID}" ]]; then
  printf 'azurePilotRelease=SUBSCRIPTION_MISMATCH\n' >&2
  exit 67
fi

backend_revision_mode="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${BACKEND_APP_NAME}" \
  --query properties.configuration.activeRevisionsMode)" || backend_revision_mode=''
frontend_revision_mode="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${FRONTEND_APP_NAME}" \
  --query properties.configuration.activeRevisionsMode)" || frontend_revision_mode=''
migration_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${MIGRATION_JOB_NAME}" \
  --query properties.configuration.triggerType)" || migration_trigger_type=''
database_bootstrap_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${DATABASE_BOOTSTRAP_JOB_NAME}" \
  --query properties.configuration.triggerType)" || database_bootstrap_trigger_type=''
membership_backfill_trigger_type="$(azure_read containerapp job show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
  --query properties.configuration.triggerType)" || membership_backfill_trigger_type=''
maintenance_trigger_type=''
if [[ "${maintenance_release}" == 'true' ]]; then
  maintenance_trigger_type="$(azure_read containerapp job show \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MAINTENANCE_JOB_NAME}" \
    --query properties.configuration.triggerType)" || maintenance_trigger_type=''
fi

if [[ "${backend_revision_mode}" != 'Single' \
  || "${frontend_revision_mode}" != 'Single' \
  || "${migration_trigger_type}" != 'Manual' \
  || ( "${maintenance_release}" == 'true' && "${maintenance_trigger_type}" != 'Manual' ) \
  || ( "${RUN_DATABASE_BOOTSTRAP}" == 'true' && "${database_bootstrap_trigger_type}" != 'Manual' ) \
  || ( ( "${RUN_MEMBERSHIP_BACKFILL}" == 'true' || "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ) \
    && "${membership_backfill_trigger_type}" != 'Manual' ) ]]; then
  printf 'azurePilotRelease=UNSAFE_RUNTIME_MODE\n' >&2
  exit 68
fi

# Announcement-only preparation does not certify or change public serving mode.
if [[ "${MAINTENANCE_PREPARE_ONLY}" != true && "${maintenance_release}" == true ]]; then
  if ! validate_backend_serving_configuration; then
    printf 'azurePilotRelease=BACKEND_SERVING_CONFIGURATION_INVALID\n' >&2
    exit 68
  fi
fi

previous_backend_image="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${BACKEND_APP_NAME}" \
  --query 'properties.template.containers[0].image')" || previous_backend_image=''
previous_frontend_image="$(azure_read containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${FRONTEND_APP_NAME}" \
  --query 'properties.template.containers[0].image')" || previous_frontend_image=''

backend_rollback_image_safe='false'
frontend_rollback_image_safe='false'
if [[ "${previous_backend_image}" == "${ACR_LOGIN_SERVER}/pms-backend:"* ]]; then
  previous_backend_tag="${previous_backend_image#"${ACR_LOGIN_SERVER}/pms-backend:"}"
  [[ "${previous_backend_tag}" =~ ^[0-9a-f]{40}$ ]] && backend_rollback_image_safe='true'
elif [[ "${previous_backend_image}" == "${ACR_LOGIN_SERVER}/pms-backend@"* ]]; then
  previous_backend_digest="${previous_backend_image#"${ACR_LOGIN_SERVER}/pms-backend@"}"
  [[ "${previous_backend_digest}" =~ ^${digest_pattern}$ ]] && backend_rollback_image_safe='true'
fi
if [[ "${previous_frontend_image}" == "${ACR_LOGIN_SERVER}/pms-frontend:"* ]]; then
  previous_frontend_tag="${previous_frontend_image#"${ACR_LOGIN_SERVER}/pms-frontend:"}"
  [[ "${previous_frontend_tag}" =~ ^[0-9a-f]{40}$ ]] && frontend_rollback_image_safe='true'
elif [[ "${previous_frontend_image}" == "${ACR_LOGIN_SERVER}/pms-frontend@"* ]]; then
  previous_frontend_digest="${previous_frontend_image#"${ACR_LOGIN_SERVER}/pms-frontend@"}"
  [[ "${previous_frontend_digest}" =~ ^${digest_pattern}$ ]] && frontend_rollback_image_safe='true'
fi

if [[ -z "${previous_backend_image}" || -z "${previous_frontend_image}" \
  || "${previous_backend_image}" =~ [[:space:]] \
  || "${previous_frontend_image}" =~ [[:space:]] \
  || "${backend_rollback_image_safe}" != 'true' \
  || "${frontend_rollback_image_safe}" != 'true' ]]; then
  printf 'azurePilotRelease=UNSAFE_ROLLBACK_BASELINE\n' >&2
  exit 69
fi

if ! wait_for_app "${BACKEND_APP_NAME}" "${previous_backend_image}" \
  || ! wait_for_app "${FRONTEND_APP_NAME}" "${previous_frontend_image}"; then
  printf 'azurePilotRelease=BASELINE_NOT_READY\n' >&2
  exit 70
fi

baseline_live_status="$(public_status '/health/live')" || baseline_live_status=''
baseline_root_status="$(public_status '/')" || baseline_root_status=''
baseline_api_status="$(public_status '/api/me')" || baseline_api_status=''
if [[ "${baseline_live_status}" != '200' \
  || "${baseline_root_status}" != '401' \
  || "${baseline_api_status}" != '401' ]]; then
  printf 'azurePilotRelease=BASELINE_PUBLIC_SECURITY_FAILED\n' >&2
  exit 71
fi

if [[ "${maintenance_release}" == 'true' ]]; then
  if [[ "${RUN_MIGRATION}" == true && "${MAINTENANCE_PREPARE_ONLY}" != true ]]; then
    recovery_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-recovery-checkpoint.XXXXXX")"
    recovery_state="${recovery_directory}/recovery.json"
    printf 'azurePilotRecoveryEvidence=%s\n' "${recovery_directory}"
    if ! recovery_postgres_host="$(run_recovery_checkpoint preflight \
      --state "${recovery_state}" --az-bin "${azure_cli_bin}")"; then
      printf 'azurePilotRelease=RECOVERY_PREFLIGHT_FAILED\n' >&2
      exit 79
    fi
    if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
      # Never expose raw identifiers through an Actions artifact, even if a
      # caller accidentally omits the workflow's encryption-required flag.
      [[ -f "${recovery_directory}/recovery.p7m" ]] || {
        printf 'azurePilotRelease=RECOVERY_ENCRYPTED_EVIDENCE_REQUIRED\n' >&2
        exit 79
      }
      printf 'recovery_evidence=%s\n' "${recovery_directory}/recovery.p7m" >>"${GITHUB_OUTPUT}"
    fi
  fi
  if ! load_job_execution_override "${MAINTENANCE_JOB_NAME}" maintenance; then
    printf 'azurePilotRelease=MAINTENANCE_JOB_CONFIGURATION_INVALID\n' >&2
    exit 79
  fi
  for required_environment_name in \
    ConnectionStrings__QmsCheongjuRuntime ConnectionStrings__QmsOsanRuntime; do
    found='false'
    for configured_environment_name in "${job_override_environment[@]}"; do
      if [[ "${configured_environment_name%%=*}" == "${required_environment_name}" ]]; then
        found='true'
        break
      fi
    done
    if [[ "${found}" != 'true' ]]; then
      printf 'azurePilotRelease=MAINTENANCE_RUNTIME_CONNECTION_MISSING\n' >&2
      exit 79
    fi
  done
  maintenance_job_environment=("${job_override_environment[@]}")
  for configured_environment_name in "${maintenance_job_environment[@]}"; do
    if [[ "${configured_environment_name%%=*}" == Maintenance__* ]]; then
      printf 'azurePilotRelease=MAINTENANCE_JOB_STALE_RELEASE_CONFIGURATION\n' >&2
      exit 79
    fi
  done
  maintenance_job_cpu="${job_override_cpu}"
  maintenance_job_memory="${job_override_memory}"
  preparation_action=prepare
  [[ "$MAINTENANCE_PREPARED" == true ]] && preparation_action=verify-prepared
  maintenance_active='true'
  if ! run_maintenance_job "$preparation_action"; then
    printf 'azurePilotRelease=MAINTENANCE_PREPARE_FAILED\n' >&2
    exit 79
  fi
  if [[ "$MAINTENANCE_PREPARE_ONLY" == true ]]; then
    printf 'azurePilotReleaseBackendServing=NOT_VERIFIED_PREPARE_ONLY\n'
    printf 'azurePilotRelease=ANNOUNCED\n'
    exit 0
  fi
  maintenance_active='true'
  if ! run_maintenance_job activate; then
    printf 'azurePilotRelease=MAINTENANCE_ACTIVATION_FAILED\n' >&2
    exit 79
  fi
  maintenance_active='true'
fi

if [[ "${RUN_MIGRATION}" == 'true' ]]; then
  if ! run_recovery_checkpoint arm --state "${recovery_state}" --az-bin "${azure_cli_bin}" \
    || ! quiesce_apps; then
    printf 'azurePilotRelease=QUIESCENCE_OR_DRAIN_FAILED\n' >&2
    exit 79
  fi
  if ! run_recovery_checkpoint wait --state "${recovery_state}" \
    --az-bin "${azure_cli_bin}" --drained-at "${recovery_drained_at}" "${recovery_drains[@]}" \
    || ! run_drain_check \
    || ! run_recovery_checkpoint verify --state "${recovery_state}" --az-bin "${azure_cli_bin}" "${recovery_drains[@]}"; then
    printf 'azurePilotRelease=RECOVERY_CHECKPOINT_FAILED\n' >&2
    exit 79
  fi
fi

if [[ "${RUN_DATABASE_BOOTSTRAP}" == 'true' ]]; then
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${DATABASE_BOOTSTRAP_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=DATABASE_BOOTSTRAP_JOB_UPDATE_FAILED\n' >&2
    exit 72
  fi

  if ! run_database_job "${DATABASE_BOOTSTRAP_JOB_NAME}" Database__BootstrapTarget --bootstrap-database-roles; then
    printf 'azurePilotRelease=DATABASE_BOOTSTRAP_FAILED\n' >&2
    exit 73
  fi
fi

if [[ "${RUN_MIGRATION}" == 'true' ]]; then
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MIGRATION_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=MIGRATION_JOB_UPDATE_FAILED\n' >&2
    exit 72
  fi

  if ! run_database_job "${MIGRATION_JOB_NAME}" Database__MigrationTarget --migrate-only; then
    printf 'azurePilotRelease=MIGRATION_FAILED\n' >&2
    exit 74
  fi
fi

if [[ "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  if ! load_job_execution_override "${MEMBERSHIP_BACKFILL_JOB_NAME}"; then
    printf 'membershipBackfillInspectionConfiguration=%s\n' \
      "${job_override_configuration_error}" >&2
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_INSPECTION_CONFIGURATION_INVALID\n' >&2
    exit 79
  fi

  membership_backfill_inspection_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --container-name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}" \
    --cpu "${job_override_cpu}" \
    --memory "${job_override_memory}" \
    --env-vars "${job_override_environment[@]}" \
    --args=--inspect-business-unit-membership-backfill \
    --query name)" || membership_backfill_inspection_execution=''
  if [[ -z "${membership_backfill_inspection_execution}" \
    || "${membership_backfill_inspection_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${MEMBERSHIP_BACKFILL_JOB_NAME}" "${membership_backfill_inspection_execution}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_INSPECTION_FAILED\n' >&2
    exit 77
  fi

  membership_backfill_inspection_summary=''
  for ((attempt = 1; attempt <= 6; attempt++)); do
    if "${azure_cli_bin}" containerapp job logs show \
      --resource-group "${AZURE_RESOURCE_GROUP}" \
      --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
      --execution "${membership_backfill_inspection_execution}" \
      --container "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
      --format text \
      --tail 100 \
      >"${task_tmp_dir}/command-output" \
      2>"${task_tmp_dir}/command-error"; then
      membership_backfill_inspection_summary="$(sed -n \
        's/^.*\(businessUnitMembershipBackfillDryRun=PASS identityCount=[0-9][0-9]* overallAdministratorCount=[0-9][0-9]* configuredOverallAdministratorCount=[0-9][0-9]* activeDirectoryOverallAdministratorCount=[0-9][0-9]* activateMembershipCount=[0-9][0-9]* deactivateMembershipCount=[0-9][0-9]* normalizeDepartmentDefaultRoleCount=[0-9][0-9]* removeManagedRoleCount=[0-9][0-9]* resetDepartmentHeadCount=[0-9][0-9]* repairOverallProfileCount=[0-9][0-9]* designateOverallAdministratorCount=[0-9][0-9]* cheongjuSystemAdminPermissionGapCount=[0-9][0-9]* osanSystemAdminPermissionGapCount=[0-9][0-9]*\).*$/\1/p' \
        "${task_tmp_dir}/command-output" | tail -n 1)"
    fi
    if [[ -n "${membership_backfill_inspection_summary}" ]]; then
      break
    fi
    if [[ "${poll_interval_seconds}" -gt 0 ]]; then
      sleep "${poll_interval_seconds}"
    fi
  done
  if [[ -z "${membership_backfill_inspection_summary}" ]]; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_INSPECTION_EVIDENCE_MISSING\n' >&2
    exit 78
  fi
  printf '%s\n' "${membership_backfill_inspection_summary}"
fi

if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  if ! load_job_execution_override "${MEMBERSHIP_BACKFILL_JOB_NAME}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_CONFIGURATION_INVALID\n' >&2
    exit 75
  fi
  if ! azure_mutate containerapp job update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_JOB_UPDATE_FAILED\n' >&2
    exit 75
  fi

  membership_backfill_execution="$(azure_read containerapp job start \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${MEMBERSHIP_BACKFILL_JOB_NAME}" \
    --container-name "${MEMBERSHIP_BACKFILL_JOB_NAME}" --image "${BACKEND_RELEASE_IMAGE}" \
    --cpu "${job_override_cpu}" --memory "${job_override_memory}" \
    --env-vars "${job_override_environment[@]}" \
    "Database__RecoveryPostgresHost=${recovery_postgres_host}" \
    --args=--backfill-business-unit-memberships \
    --query name)" || membership_backfill_execution=''
  if [[ -z "${membership_backfill_execution}" || "${membership_backfill_execution}" =~ [[:space:]] ]] \
    || ! wait_for_job "${MEMBERSHIP_BACKFILL_JOB_NAME}" "${membership_backfill_execution}"; then
    printf 'azurePilotRelease=MEMBERSHIP_BACKFILL_FAILED\n' >&2
    exit 76
  fi
fi

if [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
  backend_changed='true'
  if ! azure_mutate containerapp update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${BACKEND_APP_NAME}" \
    --image "${BACKEND_RELEASE_IMAGE}" \
    || ! wait_for_app "${BACKEND_APP_NAME}" "${BACKEND_RELEASE_IMAGE}"; then
    fail_after_mutation 'BACKEND_RELEASE_FAILED'
  fi
fi

if [[ "${DEPLOY_FRONTEND}" == 'true' ]]; then
  frontend_changed='true'
  if ! azure_mutate containerapp update \
    --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" \
    --image "${FRONTEND_RELEASE_IMAGE}" \
    || ! wait_for_app "${FRONTEND_APP_NAME}" "${FRONTEND_RELEASE_IMAGE}"; then
    fail_after_mutation 'FRONTEND_RELEASE_FAILED'
  fi
fi

# A schema release stopped both entry points. Reactivate the unchanged frontend
# only after the selected backend image is healthy; never restore an old backend.
if [[ "${quiescing}" == 'true' && "${DEPLOY_FRONTEND}" == 'false' ]]; then
  if ! azure_mutate containerapp revision activate --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" --revision "${previous_frontend_revision}"; then
    fail_after_mutation 'FRONTEND_RESUME_FAILED'
  fi
fi

# Recheck running revisions and exact ledgers before releasing maintenance.
final_backend_image="${previous_backend_image}"
final_frontend_image="${previous_frontend_image}"
[[ "${DEPLOY_BACKEND}" == 'true' ]] && final_backend_image="${BACKEND_RELEASE_IMAGE}"
[[ "${DEPLOY_FRONTEND}" == 'true' ]] && final_frontend_image="${FRONTEND_RELEASE_IMAGE}"
if ! wait_for_app "${BACKEND_APP_NAME}" "${final_backend_image}" \
  || ! wait_for_app "${FRONTEND_APP_NAME}" "${final_frontend_image}"; then
  fail_after_mutation 'FINAL_APP_NOT_READY'
fi

if [[ "${maintenance_release}" == true ]]; then
  serving_revision="$(azure_read containerapp show --resource-group "${AZURE_RESOURCE_GROUP}" \
    --name "${BACKEND_APP_NAME}" --query properties.latestReadyRevisionName)" || serving_revision=''
  if [[ -z "${serving_revision}" ]] || ! validate_backend_serving_configuration "${serving_revision}"; then
    fail_after_mutation 'BACKEND_SERVING_CONFIGURATION_INVALID'
  fi
fi

final_live_status="$(public_status '/health/live')" || final_live_status=''
final_root_status="$(public_status '/')" || final_root_status=''
final_api_status="$(public_status '/api/me')" || final_api_status=''
if [[ "${final_live_status}" != '200' \
  || "${final_root_status}" != '401' \
  || "${final_api_status}" != '401' ]]; then
  fail_after_mutation 'PUBLIC_SECURITY_SMOKE_FAILED'
fi

if [[ "${maintenance_release}" == 'true' ]]; then
  if ! run_maintenance_job complete; then
    printf 'azurePilotRelease=MAINTENANCE_RELEASE_FAILED\n' >&2
    exit 80
  fi
  maintenance_active='false'
fi

if [[ "${RUN_MIGRATION}" == 'true' ]]; then
  printf 'azurePilotReleaseMigration=PASS\n'
else
  printf 'azurePilotReleaseMigration=SKIPPED\n'
fi
if [[ "${RUN_DATABASE_BOOTSTRAP}" == 'true' ]]; then
  printf 'azurePilotReleaseDatabaseBootstrap=PASS\n'
else
  printf 'azurePilotReleaseDatabaseBootstrap=SKIPPED\n'
fi
if [[ "${RUN_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotReleaseMembershipBackfill=PASS\n'
else
  printf 'azurePilotReleaseMembershipBackfill=SKIPPED\n'
fi
if [[ "${INSPECT_MEMBERSHIP_BACKFILL}" == 'true' ]]; then
  printf 'azurePilotReleaseMembershipBackfillInspection=PASS\n'
else
  printf 'azurePilotReleaseMembershipBackfillInspection=SKIPPED\n'
fi
if [[ "${DEPLOY_BACKEND}" == 'true' ]]; then
  printf 'azurePilotReleaseBackend=PASS\n'
else
  printf 'azurePilotReleaseBackend=SKIPPED\n'
fi
if [[ "${DEPLOY_FRONTEND}" == 'true' ]]; then
  printf 'azurePilotReleaseFrontend=PASS\n'
else
  printf 'azurePilotReleaseFrontend=SKIPPED\n'
fi
printf 'azurePilotReleasePublicSecurity=PASS\n'
