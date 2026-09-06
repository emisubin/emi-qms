#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${repo_root}"

# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

injection="${BUSINESS_UNIT_PRODUCTION_IMAGE_TEST_INJECTION:-none}"
# Synthetic cleanup tests only. Production packaging runs leave this unset.
case "${injection}" in
  none|after-inspection-container|wait-after-inspection-container) ;;
  *)
    echo "Business-unit production image test injection is invalid." >&2
    exit "${E2E_SAFETY_EXIT_CODE}"
    ;;
esac

# This wrapper owns the Compose scope. Do not inherit an unrelated caller override.
export E2E_COMPOSE_FILE="${repo_root}/infrastructure/docker-compose.e2e.yml"
e2e_initialize_environment "${repo_root}"
e2e_disable_external_providers

run_id="${E2E_RUN_ID}"
docker_run_id="${run_id//_/-}"
expected_compose_project="emi-qms-e2e-${docker_run_id}"
expected_database_name="emi_qms_e2e_${run_id}"
image_ref="emi-qms-business-unit-production-test:${docker_run_id}"
inspection_container="emi-qms-business-unit-production-catalog-${docker_run_id}"
migration_container_fresh="emi-qms-production-migration-${docker_run_id}-fresh"
migration_container_existing="emi-qms-production-migration-${docker_run_id}-existing"
migration_containers=("${migration_container_fresh}" "${migration_container_existing}")
owner_label="com.emi-qms.test.owner"
run_label="com.emi-qms.test.run-id"
owner_value="business-unit-production-image"
temp_prefix="${TMPDIR:-/tmp}"
temp_prefix="${temp_prefix%/}/emi-qms-business-unit-production-${run_id}."
temp_dir=""
temp_scope_claimed=0
image_scope_claimed=0
container_scope_claimed=0
migration_container_scope_claimed=0
compose_scope_claimed=0
cleanup_started=0

if [[ "${E2E_COMPOSE_PROJECT_NAME}" != "${expected_compose_project}" \
  || "${E2E_DATABASE_NAME}" != "${expected_database_name}" ]]; then
  echo "Business-unit production image test scope does not match its run id." >&2
  exit "${E2E_SAFETY_EXIT_CODE}"
fi

count_nonempty_lines() {
  local input="$1"
  local line
  local count=0

  while IFS= read -r line; do
    [[ -n "${line}" ]] && count=$((count + 1))
  done <<<"${input}"

  printf '%s\n' "${count}"
}

count_unique_nonempty_lines() {
  local input="$1"
  local count

  if ! count="$(awk 'NF && !seen[$0]++ { count += 1 } END { print count + 0 }' <<<"${input}")"; then
    return 1
  fi

  printf '%s\n' "${count}"
}

owned_container_count() {
  local output

  if ! output="$(docker ps -aq --no-trunc \
    --filter "label=${owner_label}=${owner_value}" \
    --filter "label=${run_label}=${run_id}")"; then
    return 1
  fi

  count_nonempty_lines "${output}"
}

owned_image_count() {
  local output

  if ! output="$(docker image ls -q --no-trunc \
    --filter "label=${owner_label}=${owner_value}" \
    --filter "label=${run_label}=${run_id}")"; then
    return 1
  fi

  count_unique_nonempty_lines "${output}"
}

compose_container_count() {
  local output

  if ! output="$(docker ps -aq --no-trunc \
    --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"; then
    return 1
  fi

  count_nonempty_lines "${output}"
}

compose_network_count() {
  local output

  if ! output="$(docker network ls -q --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"; then
    return 1
  fi

  count_nonempty_lines "${output}"
}

compose_volume_count() {
  local output

  if ! output="$(docker volume ls -q --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"; then
    return 1
  fi

  count_nonempty_lines "${output}"
}

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

image_state() {
  local target_image="$1"
  local matching_ids

  if docker image inspect "${target_image}" >/dev/null 2>&1; then
    printf 'PRESENT\n'
    return 0
  fi

  if ! matching_ids="$(docker image ls -q --filter "reference=${target_image}")"; then
    printf 'UNKNOWN\n'
    return 1
  fi

  if [[ -n "${matching_ids}" ]]; then
    printf 'UNKNOWN\n'
    return 1
  fi

  printf 'ABSENT\n'
}

cleanup_container_count() {
  local output
  local container_name
  local state
  local resource_id

  if ! output="$(docker ps -aq --no-trunc \
    --filter "label=${owner_label}=${owner_value}" \
    --filter "label=${run_label}=${run_id}")"; then
    return 1
  fi

  if [[ "${container_scope_claimed}" == "1" ]]; then
    if ! state="$(container_state "${inspection_container}")"; then
      return 1
    fi
    if [[ "${state}" == "PRESENT" ]]; then
      if ! resource_id="$(docker container inspect -f '{{.Id}}' "${inspection_container}")"; then
        return 1
      fi
      output="${output}"$'\n'"${resource_id}"
    fi
  fi

  if [[ "${migration_container_scope_claimed}" == "1" ]]; then
    for container_name in "${migration_containers[@]}"; do
      if ! state="$(container_state "${container_name}")"; then
        return 1
      fi
      if [[ "${state}" == "PRESENT" ]]; then
        if ! resource_id="$(docker container inspect -f '{{.Id}}' "${container_name}")"; then
          return 1
        fi
        output="${output}"$'\n'"${resource_id}"
      fi
    done
  fi

  count_unique_nonempty_lines "${output}"
}

cleanup_image_count() {
  local output
  local state
  local resource_id

  if ! output="$(docker image ls -q --no-trunc \
    --filter "label=${owner_label}=${owner_value}" \
    --filter "label=${run_label}=${run_id}")"; then
    return 1
  fi

  if ! state="$(image_state "${image_ref}")"; then
    return 1
  fi
  if [[ "${state}" == "PRESENT" ]]; then
    if ! resource_id="$(docker image inspect -f '{{.Id}}' "${image_ref}")"; then
      return 1
    fi
    output="${output}"$'\n'"${resource_id}"
  fi

  count_unique_nonempty_lines "${output}"
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

  [[ "${actual_owner}" == "${owner_value}" && "${actual_run_id}" == "${run_id}" ]]
}

assert_image_ownership() {
  local actual_owner
  local actual_run_id

  if ! actual_owner="$(docker image inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.owner" }}' "${image_ref}")"; then
    return 2
  fi
  if ! actual_run_id="$(docker image inspect \
    -f '{{ index .Config.Labels "com.emi-qms.test.run-id" }}' "${image_ref}")"; then
    return 2
  fi

  [[ "${actual_owner}" == "${owner_value}" && "${actual_run_id}" == "${run_id}" ]]
}

remove_owned_container() {
  local container_name="$1"
  local state

  if ! state="$(container_state "${container_name}")"; then
    echo "Business-unit production image container state is unknown." >&2
    return 1
  fi
  [[ "${state}" == "ABSENT" ]] && return 0

  if ! assert_container_ownership "${container_name}"; then
    echo "Business-unit production image container ownership verification failed." >&2
    return 1
  fi
  if ! docker container rm --force "${container_name}" >/dev/null 2>&1; then
    echo "Business-unit production image container removal failed." >&2
    return 1
  fi
  if ! state="$(container_state "${container_name}")" || [[ "${state}" != "ABSENT" ]]; then
    echo "Business-unit production image container removal could not be verified." >&2
    return 1
  fi
}

remove_owned_image() {
  local state

  if ! state="$(image_state "${image_ref}")"; then
    echo "Business-unit production image state is unknown." >&2
    return 1
  fi
  [[ "${state}" == "ABSENT" ]] && return 0

  if ! assert_image_ownership; then
    echo "Business-unit production image ownership verification failed." >&2
    return 1
  fi
  if ! docker image rm "${image_ref}" >/dev/null 2>&1; then
    echo "Business-unit production image removal failed." >&2
    return 1
  fi
  if ! state="$(image_state "${image_ref}")" || [[ "${state}" != "ABSENT" ]]; then
    echo "Business-unit production image removal could not be verified." >&2
    return 1
  fi
}

cleanup() {
  local original_exit_code="$?"
  local cleanup_exit_code=0
  local marker_run_id=""
  local container_name
  local container_remaining=0
  local image_remaining=0
  local compose_containers_before=0
  local compose_networks_before=0
  local compose_volumes_before=0
  local compose_containers_remaining=0
  local compose_networks_remaining=0
  local compose_volumes_remaining=0
  local database_remaining=0
  local temp_remaining=0
  local postgres_container_id=""
  local compose_cleanup_safe=1

  if [[ "${cleanup_started}" == "1" ]]; then
    exit "${original_exit_code}"
  fi

  cleanup_started=1
  trap - EXIT INT TERM
  set +e

  if [[ "${temp_scope_claimed}" == "1" && -d "${temp_dir}" ]]; then
    marker_run_id="$(cat "${temp_dir}/.ownership-run-id" 2>/dev/null)"
    if [[ "${marker_run_id}" == "${run_id}" && "${temp_dir}" == "${temp_prefix}"* ]]; then
      rm -rf -- "${temp_dir}" || cleanup_exit_code=1
    else
      echo "Business-unit production image temp ownership verification failed." >&2
      cleanup_exit_code=1
    fi
  fi

  if [[ "${container_scope_claimed}" == "1" ]]; then
    remove_owned_container "${inspection_container}" || cleanup_exit_code=1
  fi

  if [[ "${migration_container_scope_claimed}" == "1" ]]; then
    for container_name in "${migration_containers[@]}"; do
      remove_owned_container "${container_name}" || cleanup_exit_code=1
    done
  fi

  if [[ "${compose_scope_claimed}" == "1" ]]; then
    if ! compose_containers_before="$(compose_container_count)"; then
      compose_containers_before=UNKNOWN
      compose_cleanup_safe=0
      cleanup_exit_code=1
    fi
    if ! compose_networks_before="$(compose_network_count)"; then
      compose_networks_before=UNKNOWN
      compose_cleanup_safe=0
      cleanup_exit_code=1
    fi
    if ! compose_volumes_before="$(compose_volume_count)"; then
      compose_volumes_before=UNKNOWN
      compose_cleanup_safe=0
      cleanup_exit_code=1
    fi

    if [[ "${compose_cleanup_safe}" == "1" \
      && ( "${compose_containers_before}" != "0" \
        || "${compose_networks_before}" != "0" \
        || "${compose_volumes_before}" != "0" ) ]]; then
      if ! postgres_container_id="$(e2e_compose ps -q "${E2E_POSTGRES_SERVICE}" 2>/dev/null)"; then
        echo "Business-unit production image Compose ownership query failed." >&2
        compose_cleanup_safe=0
        cleanup_exit_code=1
      elif [[ -n "${postgres_container_id}" ]] \
        && ! e2e_assert_dedicated_postgres >/dev/null 2>&1; then
        echo "Business-unit production image database ownership verification failed." >&2
        compose_cleanup_safe=0
        cleanup_exit_code=1
      fi

      if [[ "${compose_cleanup_safe}" == "1" ]]; then
        if [[ -n "${postgres_container_id}" ]]; then
          bash "${repo_root}/scripts/e2e-db.sh" drop >/dev/null 2>&1 || cleanup_exit_code=1
          bash "${repo_root}/scripts/e2e-db.sh" assert-dropped >/dev/null 2>&1 || cleanup_exit_code=1
        fi
        e2e_stop_project >/dev/null 2>&1 || cleanup_exit_code=1
      fi
    fi
  fi

  if [[ "${image_scope_claimed}" == "1" ]]; then
    remove_owned_image || cleanup_exit_code=1
  fi

  [[ "${temp_scope_claimed}" != "1" || ! -e "${temp_dir}" ]] || temp_remaining=1

  if [[ "${container_scope_claimed}" == "1" \
    || "${migration_container_scope_claimed}" == "1" ]]; then
    if ! container_remaining="$(cleanup_container_count)"; then
      container_remaining=UNKNOWN
      cleanup_exit_code=1
    fi
  fi
  if [[ "${image_scope_claimed}" == "1" ]]; then
    if ! image_remaining="$(cleanup_image_count)"; then
      image_remaining=UNKNOWN
      cleanup_exit_code=1
    fi
  fi
  if [[ "${compose_scope_claimed}" == "1" ]]; then
    if ! compose_containers_remaining="$(compose_container_count)"; then
      compose_containers_remaining=UNKNOWN
      cleanup_exit_code=1
    fi
    if ! compose_networks_remaining="$(compose_network_count)"; then
      compose_networks_remaining=UNKNOWN
      cleanup_exit_code=1
    fi
    if ! compose_volumes_remaining="$(compose_volume_count)"; then
      compose_volumes_remaining=UNKNOWN
      cleanup_exit_code=1
    fi

    if [[ "${compose_containers_remaining}" == "UNKNOWN" \
      || "${compose_volumes_remaining}" == "UNKNOWN" ]]; then
      database_remaining=UNKNOWN
    elif [[ "${compose_containers_remaining}" != "0" \
      || "${compose_volumes_remaining}" != "0" ]]; then
      database_remaining=1
    fi
  fi

  if [[ "${temp_remaining}" != "0" || "${container_remaining}" != "0" \
    || "${image_remaining}" != "0" || "${compose_containers_remaining}" != "0" \
    || "${compose_networks_remaining}" != "0" || "${compose_volumes_remaining}" != "0" \
    || "${database_remaining}" != "0" ]]; then
    cleanup_exit_code=1
  fi

  echo "businessUnitProductionImageCleanupTemp=${temp_remaining}"
  echo "businessUnitProductionImageCleanupContainers=${container_remaining}"
  echo "businessUnitProductionImageCleanupImages=${image_remaining}"
  echo "businessUnitProductionImageCleanupDatabases=${database_remaining}"
  echo "businessUnitProductionImageCleanupComposeContainers=${compose_containers_remaining}"
  echo "businessUnitProductionImageCleanupNetworks=${compose_networks_remaining}"
  echo "businessUnitProductionImageCleanupVolumes=${compose_volumes_remaining}"

  if [[ "${original_exit_code}" -eq 0 && "${cleanup_exit_code}" -ne 0 ]]; then
    exit "${cleanup_exit_code}"
  fi

  exit "${original_exit_code}"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

command -v docker >/dev/null 2>&1 || {
  echo "Docker is required for the business-unit production image test." >&2
  exit 1
}
docker info >/dev/null 2>&1 || {
  echo "Docker is unavailable for the business-unit production image test." >&2
  exit 1
}

if ! inspection_state="$(container_state "${inspection_container}")" \
  || ! image_preflight_state="$(image_state "${image_ref}")" \
  || ! existing_owned_containers="$(owned_container_count)" \
  || ! existing_owned_images="$(owned_image_count)" \
  || ! existing_compose_containers="$(compose_container_count)" \
  || ! existing_compose_networks="$(compose_network_count)" \
  || ! existing_compose_volumes="$(compose_volume_count)"; then
  echo "Business-unit production image test resource scope query failed." >&2
  exit "${E2E_SAFETY_EXIT_CODE}"
fi

if [[ "${inspection_state}" != "ABSENT" || "${image_preflight_state}" != "ABSENT" \
  || "${existing_owned_containers}" != "0" || "${existing_owned_images}" != "0" \
  || "${existing_compose_containers}" != "0" || "${existing_compose_networks}" != "0" \
  || "${existing_compose_volumes}" != "0" ]]; then
  echo "Business-unit production image test resource scope already exists." >&2
  exit "${E2E_SAFETY_EXIT_CODE}"
fi

temp_dir="$(mktemp -d "${temp_prefix}XXXXXX")"
temp_scope_claimed=1
printf '%s\n' "${run_id}" >"${temp_dir}/.ownership-run-id"
mkdir -p "${temp_dir}/image-business-migrations" "${temp_dir}/image-directory-migrations"

image_scope_claimed=1
if ! docker build \
  --file "${repo_root}/backend/Dockerfile.production" \
  --label "${owner_label}=${owner_value}" \
  --label "${run_label}=${run_id}" \
  --tag "${image_ref}" \
  "${repo_root}" >"${temp_dir}/docker-build.log" 2>&1; then
  echo "Business-unit production image build failed." >&2
  exit 1
fi
if ! assert_image_ownership; then
  echo "Business-unit production image label verification failed." >&2
  exit 1
fi

container_scope_claimed=1
if ! docker create \
  --name "${inspection_container}" \
  --label "${owner_label}=${owner_value}" \
  --label "${run_label}=${run_id}" \
  "${image_ref}" >/dev/null; then
  echo "Business-unit production image inspection container creation failed." >&2
  exit 1
fi
if ! assert_container_ownership "${inspection_container}"; then
  echo "Business-unit production image inspection container label verification failed." >&2
  exit 1
fi

case "${injection}" in
  after-inspection-container)
    echo "businessUnitProductionImageInjectedFailure=true"
    exit 97
    ;;
  wait-after-inspection-container)
    echo "businessUnitProductionImageSignalReady=true"
    while true; do sleep 1; done
    ;;
esac

if ! docker cp \
  "${inspection_container}:/app/database/migrations/." \
  "${temp_dir}/image-business-migrations" >/dev/null 2>&1 \
  || ! docker cp \
  "${inspection_container}:/app/database/directory-migrations/." \
  "${temp_dir}/image-directory-migrations" >/dev/null 2>&1; then
  echo "Business-unit production image catalog extraction failed." >&2
  exit 1
fi

if ! diff -qr \
  "${repo_root}/database/migrations" \
  "${temp_dir}/image-business-migrations" >/dev/null \
  || ! diff -qr \
  "${repo_root}/database/directory-migrations" \
  "${temp_dir}/image-directory-migrations" >/dev/null; then
  echo "Business-unit production image catalog verification failed." >&2
  exit 1
fi

compose_scope_claimed=1
migration_container_scope_claimed=1
if ! PRODUCTION_MIGRATION_CONTAINER_OWNER="${owner_value}" \
  PRODUCTION_MIGRATION_CONTAINER_RUN_ID="${run_id}" \
  bash "${repo_root}/scripts/test-production-migration-image.sh" "${image_ref}" \
  >"${temp_dir}/business-migration-test.log" 2>&1; then
  echo "Business-unit production image migration execution failed." >&2
  exit 1
fi

if ! grep -Fxq 'productionMigrationImageFreshApply=passed' "${temp_dir}/business-migration-test.log" \
  || ! grep -Fxq 'productionMigrationImageExistingApply=passed' "${temp_dir}/business-migration-test.log" \
  || ! grep -Fxq 'productionMigrationLedgerExact=true' "${temp_dir}/business-migration-test.log"; then
  echo "Business-unit production image migration evidence verification failed." >&2
  exit 1
fi

echo "businessUnitProductionImageBuild=passed"
echo "businessUnitProductionImageBusinessCatalogExact=true"
echo "businessUnitProductionImageDirectoryCatalogExact=true"
echo "businessUnitProductionImageFreshApply=passed"
echo "businessUnitProductionImageExistingApply=passed"
