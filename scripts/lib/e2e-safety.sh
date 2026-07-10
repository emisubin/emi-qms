#!/usr/bin/env bash

E2E_SAFETY_EXIT_CODE=64

e2e_safety_error() {
  echo "E2E safety check failed: $*" >&2
}

e2e_require_safe_database_name() {
  local database_name="${1:-}"

  if [[ -z "${database_name}" || ! "${database_name}" =~ ^emi_qms_e2e_[a-z0-9_]+$ ]]; then
    e2e_safety_error "E2E_DATABASE_NAME must match ^emi_qms_e2e_[a-z0-9_]+$."
    return "${E2E_SAFETY_EXIT_CODE}"
  fi
}

e2e_require_safe_run_id() {
  local run_id="${1:-}"

  if [[ -z "${run_id}" || ! "${run_id}" =~ ^[a-z0-9]+(_[a-z0-9]+)*$ ]]; then
    e2e_safety_error "E2E_RUN_ID must contain only lowercase letters, numbers, and single underscores between segments."
    return "${E2E_SAFETY_EXIT_CODE}"
  fi
}

e2e_require_safe_compose_project() {
  local project_name="${1:-}"

  if [[ -z "${project_name}" || ! "${project_name}" =~ ^emi-qms-e2e-[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
    e2e_safety_error "E2E_COMPOSE_PROJECT_NAME must match ^emi-qms-e2e-[a-z0-9-]+$."
    return "${E2E_SAFETY_EXIT_CODE}"
  fi
}

e2e_require_safe_port() {
  local port_name="$1"
  local port_value="${2:-}"

  if [[ ! "${port_value}" =~ ^[0-9]+$ || "${port_value}" -lt 1024 || "${port_value}" -gt 65535 ]]; then
    e2e_safety_error "${port_name} must be an integer TCP port between 1024 and 65535."
    return "${E2E_SAFETY_EXIT_CODE}"
  fi

  case "${port_value}" in
    5081|5174|5432)
      e2e_safety_error "${port_name} must not use reserved UAT port ${port_value}."
      return "${E2E_SAFETY_EXIT_CODE}"
      ;;
  esac
}

e2e_port_is_available() {
  local port="$1"
  ! (echo >/dev/tcp/127.0.0.1/"${port}") >/dev/null 2>&1
}

e2e_pick_free_port() {
  local excluded_port="${1:-0}"
  local candidate

  for _ in $(seq 1 200); do
    candidate=$((20000 + (RANDOM % 30000)))
    [[ "${candidate}" == "${excluded_port}" ]] && continue
    case "${candidate}" in
      5081|5174|5432) continue ;;
    esac
    if e2e_port_is_available "${candidate}"; then
      printf '%s\n' "${candidate}"
      return 0
    fi
  done

  e2e_safety_error "Could not allocate an unused local E2E port."
  return 1
}

e2e_initialize_environment() {
  local repo_root="$1"
  local allocated_port
  local generated_run_id
  local compose_run_id

  generated_run_id="$(date -u +%Y%m%d%H%M%S)_$$_${RANDOM}"
  export E2E_RUN_ID="${E2E_RUN_ID:-${generated_run_id}}"
  e2e_require_safe_run_id "${E2E_RUN_ID}" || return $?

  compose_run_id="${E2E_RUN_ID//_/-}"
  export E2E_COMPOSE_PROJECT_NAME="${E2E_COMPOSE_PROJECT_NAME:-emi-qms-e2e-${compose_run_id}}"
  e2e_require_safe_compose_project "${E2E_COMPOSE_PROJECT_NAME}" || return $?

  export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-emi_qms_e2e_${E2E_RUN_ID}}"
  e2e_require_safe_database_name "${E2E_DATABASE_NAME}" || return $?

  export E2E_COMPOSE_FILE="${E2E_COMPOSE_FILE:-${repo_root}/infrastructure/docker-compose.e2e.yml}"
  export E2E_POSTGRES_SERVICE="e2e-postgres"
  export E2E_DATABASE_USER="emi_qms_e2e"
  export E2E_DATABASE_PASSWORD="e2e_local_only_change_me"

  [[ -f "${E2E_COMPOSE_FILE}" ]] || {
    e2e_safety_error "Dedicated E2E Compose file was not found."
    return "${E2E_SAFETY_EXIT_CODE}"
  }

  if [[ -n "${E2E_BACKEND_PORT:-}" ]]; then
    e2e_require_safe_port "E2E_BACKEND_PORT" "${E2E_BACKEND_PORT}" || return $?
  else
    allocated_port="$(e2e_pick_free_port)"
    export E2E_BACKEND_PORT="${allocated_port}"
  fi

  if [[ -n "${E2E_FRONTEND_PORT:-}" ]]; then
    e2e_require_safe_port "E2E_FRONTEND_PORT" "${E2E_FRONTEND_PORT}" || return $?
  else
    allocated_port="$(e2e_pick_free_port "${E2E_BACKEND_PORT}")"
    export E2E_FRONTEND_PORT="${allocated_port}"
  fi

  [[ "${E2E_BACKEND_PORT}" != "${E2E_FRONTEND_PORT}" ]] || {
    e2e_safety_error "E2E backend and frontend ports must differ."
    return "${E2E_SAFETY_EXIT_CODE}"
  }
}

e2e_compose() {
  e2e_require_safe_compose_project "${E2E_COMPOSE_PROJECT_NAME:-}" || return $?
  docker compose \
    --project-name "${E2E_COMPOSE_PROJECT_NAME}" \
    --file "${E2E_COMPOSE_FILE}" \
    "$@"
}

e2e_assert_dedicated_postgres() {
  local container_id
  local actual_project
  local actual_service
  local tmpfs_config
  local volume_mounts
  local uat_container_id

  e2e_require_safe_database_name "${E2E_DATABASE_NAME:-}" || return $?
  e2e_require_safe_compose_project "${E2E_COMPOSE_PROJECT_NAME:-}" || return $?

  container_id="$(e2e_compose ps -q "${E2E_POSTGRES_SERVICE}")"
  [[ -n "${container_id}" ]] || {
    e2e_safety_error "Dedicated E2E PostgreSQL container is not running."
    return 1
  }

  actual_project="$(docker inspect -f '{{ index .Config.Labels "com.docker.compose.project" }}' "${container_id}")"
  actual_service="$(docker inspect -f '{{ index .Config.Labels "com.docker.compose.service" }}' "${container_id}")"
  [[ "${actual_project}" == "${E2E_COMPOSE_PROJECT_NAME}" && "${actual_service}" == "${E2E_POSTGRES_SERVICE}" ]] || {
    e2e_safety_error "PostgreSQL container does not belong to the expected E2E Compose project/service."
    return 1
  }

  if docker inspect emi-qms-postgres >/dev/null 2>&1; then
    uat_container_id="$(docker inspect -f '{{.Id}}' emi-qms-postgres)"
    [[ "${container_id}" != "${uat_container_id}" ]] || {
      e2e_safety_error "Dedicated E2E PostgreSQL resolved to the persistent UAT container."
      return 1
    }
  fi

  tmpfs_config="$(docker inspect -f '{{json .HostConfig.Tmpfs}}' "${container_id}")"
  [[ "${tmpfs_config}" == *'"/var/lib/postgresql"'* ]] || {
    e2e_safety_error "Dedicated E2E PostgreSQL data root is not backed by tmpfs."
    return 1
  }

  volume_mounts="$(docker inspect -f '{{range .Mounts}}{{if eq .Type "volume"}}{{.Name}} {{end}}{{end}}' "${container_id}")"
  [[ -z "${volume_mounts}" ]] || {
    e2e_safety_error "Dedicated E2E PostgreSQL must not mount Docker volumes."
    return 1
  }

  export E2E_POSTGRES_CONTAINER_ID="${container_id}"
}

e2e_start_postgres() {
  local published_endpoint

  command -v docker >/dev/null 2>&1 || {
    e2e_safety_error "Docker is required for isolated Full-Stack E2E."
    return 1
  }

  e2e_compose up -d --wait --wait-timeout 120 "${E2E_POSTGRES_SERVICE}" || return $?
  e2e_assert_dedicated_postgres || return $?

  published_endpoint="$(e2e_compose port "${E2E_POSTGRES_SERVICE}" 5432)"
  export DATABASE_HOST="127.0.0.1"
  export DATABASE_PORT="${published_endpoint##*:}"
  export DATABASE_USER="${E2E_DATABASE_USER}"
  export DATABASE_PASSWORD="${E2E_DATABASE_PASSWORD}"
  export DATABASE_NAME="${E2E_DATABASE_NAME}"

  e2e_require_safe_port "DATABASE_PORT" "${DATABASE_PORT}" || return $?
}

e2e_disable_external_providers() {
  export ASPNETCORE_ENVIRONMENT="Testing"
  export Notifications__Dispatch__Enabled="false"
  export Notifications__DailyDigest__Enabled="false"
  export Notifications__Escalation__Enabled="false"
  export Notifications__Teams__Enabled="false"
  export Notifications__Teams__DryRun="true"
  export Notifications__TeamsActivity__Enabled="false"
  export Notifications__TeamsActivity__DryRun="true"
  export Notifications__Mail__Enabled="false"
  export Notifications__Mail__DryRun="true"
  export Notifications__Mail__Provider="DryRun"
}

e2e_assert_project_removed() {
  local containers
  local networks
  local volumes

  containers="$(docker ps -aq --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"
  networks="$(docker network ls -q --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"
  volumes="$(docker volume ls -q --filter "label=com.docker.compose.project=${E2E_COMPOSE_PROJECT_NAME}")"

  if [[ -n "${containers}" || -n "${networks}" || -n "${volumes}" ]]; then
    e2e_safety_error "E2E Compose resources remain after cleanup."
    return 1
  fi
}

e2e_stop_project() {
  local container_id
  local ephemeral_volumes=""
  local volume_name

  container_id="$(e2e_compose ps -q "${E2E_POSTGRES_SERVICE}" 2>/dev/null || true)"
  if [[ -n "${container_id}" ]]; then
    ephemeral_volumes="$(docker inspect -f '{{range .Mounts}}{{if eq .Type "volume"}}{{.Name}} {{end}}{{end}}' "${container_id}")"
  fi

  e2e_compose down --volumes --remove-orphans --timeout 15

  for volume_name in ${ephemeral_volumes}; do
    if docker volume inspect "${volume_name}" >/dev/null 2>&1; then
      e2e_safety_error "E2E ephemeral volume remains after cleanup."
      return 1
    fi
  done

  e2e_assert_project_removed
}
