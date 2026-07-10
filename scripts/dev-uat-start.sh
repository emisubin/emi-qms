#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

load_dotenv_file() {
  local env_file="$1"
  local allowed_key_prefix="${2:-}"
  local line
  local key
  local value

  [[ -f "${env_file}" ]] || return 0

  while IFS= read -r line || [[ -n "${line}" ]]; do
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [[ -z "${line}" || "${line}" == \#* || "${line}" != *=* ]] && continue

    key="${line%%=*}"
    value="${line#*=}"
    key="${key#"${key%%[![:space:]]*}"}"
    key="${key%"${key##*[![:space:]]}"}"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"

    if [[ "${value}" == \"*\" && "${value}" == *\" && "${#value}" -ge 2 ]]; then
      value="${value:1:${#value}-2}"
    elif [[ "${value}" == \'*\' && "${#value}" -ge 2 ]]; then
      value="${value:1:${#value}-2}"
    fi

    [[ "${key}" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    if [[ -n "${allowed_key_prefix}" && "${key}" != "${allowed_key_prefix}"* ]]; then
      continue
    fi
    export "${key}=${value}"
  done < "${env_file}"
}

if [[ -f .env ]]; then
  load_dotenv_file .env
fi

if [[ "${UAT_LOAD_NOTIFY_LOCAL_ENV:-false}" == "true" && -f .env.notify-local ]]; then
  load_dotenv_file .env.notify-local "Notifications__"
fi

export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_NAME="${UAT_DATABASE_NAME:-emi_qms_uat_005a}"
export FRONTEND_PORT="${FRONTEND_PORT:-5174}"
if [[ "${FRONTEND_PORT}" != "5174" ]]; then
  echo "Manual UAT frontend must use port 5174; received FRONTEND_PORT=${FRONTEND_PORT}." >&2
  exit 1
fi
export ASPNETCORE_ENVIRONMENT="Development"
export AUTH_MODE="Dev"
export Authentication__Mode="Dev"
export ASPNETCORE_URLS="http://127.0.0.1:5081"
export DEV_AUTHENTICATION_ENABLED="${DEV_AUTHENTICATION_ENABLED:-true}"
export DEV_DATA_SEED_ENABLED="${DEV_DATA_SEED_ENABLED:-true}"
export VITE_AUTH_MODE="Dev"
export VITE_DEV_USER_KEY="${VITE_DEV_USER_KEY:-dev-production}"
export UAT_FRONTEND_HTTPS="${UAT_FRONTEND_HTTPS:-false}"
UAT_FRONTEND_HTTPS_NORMALIZED="$(printf '%s' "${UAT_FRONTEND_HTTPS}" | tr '[:upper:]' '[:lower:]')"
if [[ "${UAT_FRONTEND_HTTPS_NORMALIZED}" == "true" || "${UAT_FRONTEND_HTTPS}" == "1" ]]; then
  FRONTEND_BASE_URL="https://localhost:${FRONTEND_PORT}"
  FRONTEND_WRONG_PROTOCOL_URL="http://localhost:${FRONTEND_PORT}"
  if [[ ",${FRONTEND_ORIGIN:-}," == *",${FRONTEND_BASE_URL},"* ]]; then
    export FRONTEND_ORIGIN="${FRONTEND_ORIGIN}"
  elif [[ -n "${FRONTEND_ORIGIN:-}" ]]; then
    export FRONTEND_ORIGIN="${FRONTEND_BASE_URL},${FRONTEND_ORIGIN}"
  else
    export FRONTEND_ORIGIN="${FRONTEND_BASE_URL},http://127.0.0.1:${FRONTEND_PORT},http://localhost:${FRONTEND_PORT}"
  fi
  export VITE_API_BASE_URL="${UAT_FRONTEND_HTTPS_API_BASE_URL:-}"
  export VITE_DEV_PROXY_TARGET="${VITE_DEV_PROXY_TARGET:-http://127.0.0.1:5081}"
  export VITE_DEV_HTTPS="${VITE_DEV_HTTPS:-true}"
  export VITE_DEV_HTTPS_CERT="${VITE_DEV_HTTPS_CERT:-${REPO_ROOT}/.certs/localhost.pem}"
  export VITE_DEV_HTTPS_KEY="${VITE_DEV_HTTPS_KEY:-${REPO_ROOT}/.certs/localhost-key.pem}"
  export VITE_HMR_HOST="${VITE_HMR_HOST:-localhost}"
else
  FRONTEND_BASE_URL="http://localhost:${FRONTEND_PORT}"
  FRONTEND_WRONG_PROTOCOL_URL="https://localhost:${FRONTEND_PORT}"
  export FRONTEND_ORIGIN="${FRONTEND_ORIGIN:-http://127.0.0.1:${FRONTEND_PORT},http://localhost:${FRONTEND_PORT}}"
  export VITE_API_BASE_URL="${VITE_API_BASE_URL:-http://127.0.0.1:5081}"
  export VITE_DEV_PROXY_TARGET="${VITE_DEV_PROXY_TARGET:-http://127.0.0.1:5081}"
  export VITE_HMR_HOST="${VITE_HMR_HOST:-127.0.0.1}"
fi
export VITE_DEV_SERVER_PORT="${FRONTEND_PORT}"
export VITE_HMR_CLIENT_PORT="${FRONTEND_PORT}"
export Notifications__Links__BaseUrl="${Notifications__Links__BaseUrl:-${FRONTEND_BASE_URL}}"

report_env_key_state() {
  local key="$1"
  local value
  value="$(printenv "${key}" 2>/dev/null || true)"
  if [[ -n "${value}" ]]; then
    echo "  ${key}: configured"
  else
    echo "  ${key}: missing"
  fi
}

if [[ "${UAT_LOAD_NOTIFY_LOCAL_ENV:-false}" == "true" ]]; then
  echo "Notification Development environment status (values hidden)"
  for notify_key in \
    Notifications__Dispatch__Enabled \
    Notifications__Teams__Enabled \
    Notifications__Teams__DryRun \
    Notifications__Mail__Enabled \
    Notifications__Mail__DryRun \
    Notifications__TeamsActivity__Enabled \
    Notifications__TeamsActivity__DryRun \
    Notifications__TeamsActivity__TenantId \
    Notifications__TeamsActivity__ClientId \
    Notifications__TeamsActivity__ClientSecret \
    Notifications__TeamsActivity__TeamsCatalogAppId \
    Notifications__Links__BaseUrl; do
    report_env_key_state "${notify_key}"
  done
fi

if [[ -z "${DATABASE_PASSWORD:-}" ]]; then
  echo "DATABASE_PASSWORD is required. Set it in .env or the shell environment." >&2
  exit 1
fi

echo "Starting PostgreSQL container for manual UAT..."
if [[ -f .env ]]; then
  docker compose --env-file .env -f infrastructure/docker-compose.yml up -d postgres
else
  docker compose -f infrastructure/docker-compose.yml up -d postgres
fi

echo "Waiting for PostgreSQL health..."
until [[ "$(docker inspect -f '{{.State.Health.Status}}' emi-qms-postgres 2>/dev/null || true)" == "healthy" ]]; do
  sleep 2
done

if ! docker exec emi-qms-postgres psql -U "${DATABASE_USER}" -d postgres -tAc "select 1 from pg_database where datname = '${DATABASE_NAME}'" | grep -q 1; then
  echo "Creating manual UAT database ${DATABASE_NAME}..."
  docker exec emi-qms-postgres createdb -U "${DATABASE_USER}" "${DATABASE_NAME}"
fi

echo "Applying migrations to manual UAT database..."
docker exec -i emi-qms-postgres psql -v ON_ERROR_STOP=1 -U "${DATABASE_USER}" -d "${DATABASE_NAME}" >/dev/null <<'SQL'
create table if not exists schema_migrations (
    version text primary key,
    applied_at_utc timestamptz not null default now()
);
SQL

for migration_file in "${REPO_ROOT}"/database/migrations/*.sql; do
  migration_version="$(basename "${migration_file}" .sql)"
  if docker exec emi-qms-postgres psql -U "${DATABASE_USER}" -d "${DATABASE_NAME}" -tAc "select 1 from schema_migrations where version = '${migration_version}'" | grep -q 1; then
    continue
  fi

  {
    printf 'begin;\n'
    cat "${migration_file}"
    printf '\ninsert into schema_migrations (version) values (%s);\n' "'${migration_version}'"
    printf 'commit;\n'
  } | docker exec -i emi-qms-postgres psql -v ON_ERROR_STOP=1 -U "${DATABASE_USER}" -d "${DATABASE_NAME}" >/dev/null
done

echo "Ensuring manual UAT workflow stage master data..."
docker exec -i emi-qms-postgres psql -v ON_ERROR_STOP=1 -U "${DATABASE_USER}" -d "${DATABASE_NAME}" >/dev/null <<'SQL'
begin;

update workflow_stages
set sequence_number = sequence_number + 1000
where stage_code in (
    'SalesProjectCreated',
    'ProductionPlanning',
    'DesignPanelInfo',
    'ProcurementInfo',
    'MaterialArrived',
    'IQC',
    'ReceiptConfirmed',
    'KittingCompleted',
    'ManufacturingWork',
    'LQC',
    'ManufacturingCompleted',
    'OQC',
    'CustomerInspection',
    'FAT',
    'PackingCompleted',
    'DepartureProcessed',
    'DeliveryCompleted',
    'SalesSettlementCompleted'
);

insert into workflow_stages (stage_code, sequence_number, department_code, stage_name, is_optional, is_active)
values
    ('SalesProjectCreated', 1, 'sales', '프로젝트 생성', false, true),
    ('ProductionPlanning', 2, 'production-planning', '생산계획·담당자', false, true),
    ('DesignPanelInfo', 3, 'design', '제품명·사이즈', false, true),
    ('ProcurementInfo', 4, 'procurement', '구매정보', false, true),
    ('MaterialArrived', 5, 'materials', '자재 도착', false, true),
    ('IQC', 6, 'quality', '수입검사', false, true),
    ('ReceiptConfirmed', 7, 'materials', '입고 확정', false, true),
    ('KittingCompleted', 8, 'materials', '키팅 완료', false, true),
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
on conflict (stage_code) do update
set sequence_number = excluded.sequence_number,
    department_code = excluded.department_code,
    stage_name = excluded.stage_name,
    is_optional = excluded.is_optional,
    is_active = excluded.is_active;

commit;
SQL

echo "Ensuring manual UAT production planning schema and master data..."
docker exec -i emi-qms-postgres psql -v ON_ERROR_STOP=1 -U "${DATABASE_USER}" -d "${DATABASE_NAME}" >/dev/null <<'SQL'
begin;

alter table if exists project_production_plan_items
    add column if not exists is_active boolean not null default true;

create unique index if not exists ux_project_production_plan_items_active_name
    on project_production_plan_items(production_plan_id, lower(btrim(step_name_snapshot)))
    where is_active = true;

create table if not exists production_planning_excel_import_batches (
    id uuid primary key default uuid_generate_v4(),
    original_file_name text not null,
    file_size_bytes bigint not null,
    file_sha256 text not null,
    total_row_count integer not null,
    applied_row_count integer not null,
    error_row_count integer not null,
    uploaded_by_user_id uuid null references qms_users(id),
    uploaded_at_utc timestamptz not null default now(),
    reason text null,
    constraint ck_production_planning_excel_file_name_not_blank check (btrim(original_file_name) <> ''),
    constraint ck_production_planning_excel_counts_nonnegative check (
        total_row_count >= 0 and applied_row_count >= 0 and error_row_count >= 0
    )
);

create table if not exists production_plan_template_audit_events (
    id uuid primary key default uuid_generate_v4(),
    product_type_id uuid not null references production_product_types(id) on delete restrict,
    template_id uuid not null references production_plan_templates(id) on delete restrict,
    action text not null,
    old_value text null,
    new_value text null,
    reason text null,
    changed_by_user_id uuid null references qms_users(id),
    changed_at_utc timestamptz not null default now(),
    correlation_id text null,
    constraint ck_production_plan_template_audit_action_not_blank check (btrim(action) <> '')
);

create index if not exists ix_production_plan_template_audit_product_type
    on production_plan_template_audit_events(product_type_id, changed_at_utc desc);

create table if not exists system_holidays (
    id uuid primary key default uuid_generate_v4(),
    holiday_date date not null,
    name text not null,
    country_code text not null default 'KR',
    source text not null,
    source_key text not null,
    is_active boolean not null default true,
    synced_at_utc timestamptz null,
    created_at_utc timestamptz not null default now(),
    updated_at_utc timestamptz not null default now(),
    constraint ck_system_holidays_name_not_blank check (btrim(name) <> ''),
    constraint ck_system_holidays_country_code_not_blank check (btrim(country_code) <> ''),
    constraint ck_system_holidays_source_not_blank check (btrim(source) <> ''),
    constraint ck_system_holidays_source_key_not_blank check (btrim(source_key) <> ''),
    constraint ux_system_holidays_country_date_source_key unique (country_code, holiday_date, source_key)
);

create index if not exists ix_system_holidays_active_lookup
    on system_holidays(country_code, holiday_date)
    where is_active = true;

insert into system_holidays (
    holiday_date,
    name,
    country_code,
    source,
    source_key,
    is_active,
    synced_at_utc,
    updated_at_utc
)
values
    (
        date '2026-06-03',
        '제9회 전국동시지방선거일',
        'KR',
        'ManualUat:PublicHoliday',
        'ManualUat:PublicHoliday:20260603:제9회 전국동시지방선거일',
        true,
        now(),
        now()
    ),
    (
        date '2026-07-17',
        '제헌절',
        'KR',
        'ManualUat:NationalHoliday',
        'ManualUat:NationalHoliday:20260717:제헌절',
        true,
        now(),
        now()
    ),
    (
        date '2026-12-25',
        '기독탄신일',
        'KR',
        'ManualUat:PublicHoliday',
        'ManualUat:PublicHoliday:20261225:기독탄신일',
        true,
        now(),
        now()
    )
on conflict (country_code, holiday_date, source_key) do update
set name = excluded.name,
    is_active = true,
    updated_at_utc = excluded.updated_at_utc;

with product_types(id, code, name) as (
    values
        ('31000000-0000-0000-0000-000000000067'::uuid, 'UL67', 'UL67'),
        ('31000000-0000-0000-0000-000000000891'::uuid, 'UL891', 'UL891'),
        ('31000000-0000-0000-0000-00000000508a'::uuid, 'UL508A', 'UL508A'),
        ('31000000-0000-0000-0000-0000000001ec'::uuid, 'IEC', 'IEC'),
        ('31000000-0000-0000-0000-000000000112'::uuid, 'LLP', 'LLP'),
        ('31000000-0000-0000-0000-000000000772'::uuid, 'RPP', 'RPP')
)
insert into production_product_types (id, code, name, is_active)
select id, code, name, true
from product_types
on conflict (code) do update
set name = excluded.name,
    is_active = true;

with product_types as (
    select id, code
    from production_product_types
    where code in ('UL67', 'UL891', 'UL508A', 'IEC', 'LLP', 'RPP')
),
template_ids(product_type_code, template_id) as (
    values
        ('UL67', '32000000-0000-0000-0000-000000000067'::uuid),
        ('UL891', '32000000-0000-0000-0000-000000000891'::uuid),
        ('UL508A', '32000000-0000-0000-0000-00000000508a'::uuid),
        ('IEC', '32000000-0000-0000-0000-0000000001ec'::uuid),
        ('LLP', '32000000-0000-0000-0000-000000000112'::uuid),
        ('RPP', '32000000-0000-0000-0000-000000000772'::uuid)
)
insert into production_plan_templates (id, product_type_id, version, is_active)
select template_ids.template_id, product_types.id, 1, true
from product_types
join template_ids on template_ids.product_type_code = product_types.code
on conflict (id) do nothing;

with active_templates as (
    select template_id
    from (values
        ('32000000-0000-0000-0000-000000000067'::uuid),
        ('32000000-0000-0000-0000-000000000891'::uuid),
        ('32000000-0000-0000-0000-00000000508a'::uuid),
        ('32000000-0000-0000-0000-0000000001ec'::uuid),
        ('32000000-0000-0000-0000-000000000112'::uuid),
        ('32000000-0000-0000-0000-000000000772'::uuid)
    ) as template_ids(template_id)
),
default_steps(sequence_number, step_name) as (
    values
        (1, '자재 입고'),
        (2, '조립 시작'),
        (3, '배선'),
        (4, '검사 준비')
)
insert into production_plan_template_steps (template_id, sequence_number, step_name, is_required, is_active)
select active_templates.template_id, default_steps.sequence_number, default_steps.step_name, true, true
from active_templates
cross join default_steps
on conflict (template_id, sequence_number) do nothing;

commit;
SQL

echo "Manual UAT fixed environment"
echo "  Backend:  http://127.0.0.1:5081"
if [[ "${UAT_FRONTEND_HTTPS_NORMALIZED}" == "true" || "${UAT_FRONTEND_HTTPS}" == "1" ]]; then
  echo "  Frontend: ${FRONTEND_BASE_URL}"
  echo "  API proxy: /api and /health -> ${VITE_DEV_PROXY_TARGET}"
else
  echo "  Frontend: ${FRONTEND_BASE_URL}"
fi
echo "  DB:       ${DATABASE_NAME}"
echo "  Note: E2E tests use their own temporary database and must not reuse this DB."

BACKEND_LOG="${UAT_BACKEND_LOG:-/tmp/emi-qms-dev-uat-backend.log}"
FRONTEND_LOG="${UAT_FRONTEND_LOG:-/tmp/emi-qms-dev-uat-frontend.log}"
BACKEND_PID_FILE="${UAT_BACKEND_PID_FILE:-/tmp/emi-qms-dev-uat-backend.pid}"
FRONTEND_PID_FILE="${UAT_FRONTEND_PID_FILE:-/tmp/emi-qms-dev-uat-frontend.pid}"
BACKEND_PORT=5081
BACKEND_SCREEN_SESSION="emi-qms-uat-backend"
FRONTEND_SCREEN_SESSION="emi-qms-uat-frontend"

find_port_pids() {
  local port="$1"
  lsof -tiTCP:"${port}" -sTCP:LISTEN 2>/dev/null || true
}

pid_cwd() {
  local pid="$1"
  lsof -a -p "${pid}" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | head -1
}

pid_comm() {
  local pid="$1"
  ps -p "${pid}" -o comm= 2>/dev/null | sed 's/^[[:space:]]*//;s/[[:space:]]*$//'
}

pid_args() {
  local pid="$1"
  ps -p "${pid}" -o args= 2>/dev/null || true
}

path_is_within() {
  local candidate="$1"
  local root="$2"
  [[ "${candidate}" == "${root}" || "${candidate}" == "${root}/"* ]]
}

describe_pid() {
  local pid="$1"
  local cwd
  local comm
  cwd="$(pid_cwd "${pid}")"
  comm="$(pid_comm "${pid}")"
  echo "PID ${pid} command=${comm:-unknown} cwd=${cwd:-unknown}"
}

is_expected_uat_backend_pid() {
  local pid="$1"
  local cwd
  local comm
  local args
  cwd="$(pid_cwd "${pid}")"
  comm="$(pid_comm "${pid}")"
  args="$(pid_args "${pid}")"

  [[ -n "${cwd}" ]] || return 1
  path_is_within "${cwd}" "${REPO_ROOT}" || return 1
  [[ "${cwd}" == "${REPO_ROOT}/backend/src/Emi.Qms.Api"* \
    || "${args}" == *"backend/src/Emi.Qms.Api"* \
    || "${args}" == *"Emi.Qms.Api"* \
    || "${comm}" == "Emi.Qms.Api"* \
    || "${comm}" == "dotnet"* ]] || return 1
  [[ "${args}" == *"Emi.Qms.Api"* \
    || "${args}" == *"backend/src/Emi.Qms.Api"* \
    || "${comm}" == "Emi.Qms.Api"* \
    || "${comm}" == "dotnet"* ]]
}

is_expected_uat_frontend_pid() {
  local pid="$1"
  local cwd
  local comm
  local args
  cwd="$(pid_cwd "${pid}")"
  comm="$(pid_comm "${pid}")"
  args="$(pid_args "${pid}")"

  [[ -n "${cwd}" ]] || return 1
  path_is_within "${cwd}" "${REPO_ROOT}/frontend" || return 1
  [[ "${args}" == *"vite"* ]] || return 1
  [[ "${comm}" == "node"* || "${args}" == *"node"* || "${args}" == *"vite"* ]]
}

wait_pid_exit() {
  local pid="$1"
  local attempts="${2:-20}"
  for _ in $(seq 1 "${attempts}"); do
    if ! kill -0 "${pid}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.5
  done
  return 1
}

stop_expected_backend_pid() {
  local pid="$1"
  echo "Stopping existing UAT backend listener PID ${pid}..."
  kill "${pid}" >/dev/null 2>&1 || true
  if wait_pid_exit "${pid}" 20; then
    return 0
  fi

  if is_expected_uat_backend_pid "${pid}"; then
    echo "Existing UAT backend PID ${pid} did not exit after SIGTERM; sending SIGKILL."
    kill -9 "${pid}" >/dev/null 2>&1 || true
    wait_pid_exit "${pid}" 10
    return
  fi

  echo "Refusing to force-stop unexpected process on backend port ${BACKEND_PORT}."
  describe_pid "${pid}"
  exit 1
}

stop_expected_frontend_pid() {
  local pid="$1"
  echo "Stopping existing UAT frontend listener PID ${pid}..."
  kill "${pid}" >/dev/null 2>&1 || true
  if wait_pid_exit "${pid}" 20; then
    return 0
  fi

  if is_expected_uat_frontend_pid "${pid}"; then
    echo "Existing UAT frontend PID ${pid} did not exit after SIGTERM; sending SIGKILL."
    kill -9 "${pid}" >/dev/null 2>&1 || true
    wait_pid_exit "${pid}" 10
    return
  fi

  echo "Refusing to force-stop unexpected process on frontend port ${FRONTEND_PORT}."
  describe_pid "${pid}"
  exit 1
}

stop_repo_owned_screen_session() {
  local session_name="$1"
  local sessions
  local line
  local session_ref
  local session_pid
  local cwd

  command -v screen >/dev/null 2>&1 || return 0
  sessions="$(screen -ls 2>/dev/null | sed -n "/\.${session_name}[[:space:]]/p" || true)"
  [[ -n "${sessions}" ]] || return 0

  while IFS= read -r line; do
    [[ -n "${line}" ]] || continue
    session_ref="$(printf '%s\n' "${line}" | awk '{print $1}')"
    session_pid="${session_ref%%.*}"
    cwd="$(pid_cwd "${session_pid}")"
    if [[ -z "${cwd}" ]] || ! path_is_within "${cwd}" "${REPO_ROOT}"; then
      echo "Refusing to stop screen session ${session_ref}; it is not owned by this repository."
      exit 1
    fi
    echo "Stopping existing repository UAT screen session ${session_ref}..."
    screen -S "${session_ref}" -X quit >/dev/null 2>&1 || true
  done <<< "${sessions}"
}

wait_port_closed() {
  local port="$1"
  for _ in $(seq 1 20); do
    if [[ -z "$(find_port_pids "${port}")" ]]; then
      return 0
    fi
    sleep 0.5
  done
  echo "Port ${port} is still occupied after stopping the expected repository UAT process."
  lsof -nP -iTCP:"${port}" -sTCP:LISTEN || true
  exit 1
}

stop_existing_uat_backend() {
  local pids
  pids="$(find_port_pids "${BACKEND_PORT}")"
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    if ! is_expected_uat_backend_pid "${pid}"; then
      echo "Port ${BACKEND_PORT} is occupied by an unexpected process; not stopping it."
      describe_pid "${pid}"
      exit 1
    fi
  done <<< "${pids}"

  stop_repo_owned_screen_session "${BACKEND_SCREEN_SESSION}"
  sleep 1

  pids="$(find_port_pids "${BACKEND_PORT}")"
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    stop_expected_backend_pid "${pid}"
  done <<< "${pids}"

  if [[ -n "${pids}" ]]; then
    wait_port_closed "${BACKEND_PORT}"
  fi
}

stop_existing_uat_frontend() {
  local pids
  pids="$(find_port_pids "${FRONTEND_PORT}")"
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    if ! is_expected_uat_frontend_pid "${pid}"; then
      echo "Port ${FRONTEND_PORT} is occupied by an unexpected process; not stopping it."
      describe_pid "${pid}"
      exit 1
    fi
  done <<< "${pids}"

  stop_repo_owned_screen_session "${FRONTEND_SCREEN_SESSION}"
  sleep 1

  pids="$(find_port_pids "${FRONTEND_PORT}")"
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    stop_expected_frontend_pid "${pid}"
  done <<< "${pids}"

  if [[ -n "${pids}" ]]; then
    wait_port_closed "${FRONTEND_PORT}"
  fi
}

wait_http_ok() {
  local url="$1"
  local label="$2"
  local log_file="${3:-}"
  for _ in $(seq 1 90); do
    if curl -fsS --max-time 3 "${url}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done

  echo "${label} did not become healthy in time."
  if [[ -n "${log_file}" && -f "${log_file}" ]]; then
    tail -80 "${log_file}" || true
  fi
  exit 1
}

assert_backend_started() {
  wait_http_ok "http://127.0.0.1:${BACKEND_PORT}/health/live" "UAT backend /health/live" "${BACKEND_LOG}"
  wait_http_ok "http://127.0.0.1:${BACKEND_PORT}/health/ready" "UAT backend /health/ready" "${BACKEND_LOG}"

  local backend_pid=""
  local pid
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    if is_expected_uat_backend_pid "${pid}"; then
      backend_pid="${pid}"
      break
    fi
  done <<< "$(find_port_pids "${BACKEND_PORT}")"

  if [[ -z "${backend_pid}" ]]; then
    echo "UAT backend port ${BACKEND_PORT} is listening, but the process is not the expected backend."
    lsof -nP -iTCP:"${BACKEND_PORT}" -sTCP:LISTEN || true
    exit 1
  fi

  if [[ -f "${BACKEND_LOG}" ]] && grep -qi "address already in use" "${BACKEND_LOG}"; then
    echo "UAT backend log contains an address-in-use startup failure."
    tail -80 "${BACKEND_LOG}" || true
    exit 1
  fi

  printf '%s\n' "${backend_pid}" > "${BACKEND_PID_FILE}"
  echo "UAT backend ready on port ${BACKEND_PORT}. PID: ${backend_pid}"
}

assert_frontend_started() {
  wait_http_ok "${FRONTEND_BASE_URL}/" "UAT frontend ${FRONTEND_BASE_URL}" "${FRONTEND_LOG}"
  wait_http_ok "${FRONTEND_BASE_URL}/health/live" "UAT frontend /health proxy" "${FRONTEND_LOG}"

  if [[ "${UAT_FRONTEND_HTTPS_NORMALIZED}" == "true" || "${UAT_FRONTEND_HTTPS}" == "1" ]]; then
    wait_http_ok "${FRONTEND_BASE_URL}/teams/activity" "UAT Teams Activity route" "${FRONTEND_LOG}"
  fi

  if curl -fsS --max-time 3 "${FRONTEND_WRONG_PROTOCOL_URL}/" >/dev/null 2>&1; then
    echo "UAT frontend protocol mismatch: ${FRONTEND_WRONG_PROTOCOL_URL} unexpectedly succeeded."
    exit 1
  fi

  local frontend_pid=""
  local pid
  while IFS= read -r pid; do
    [[ -n "${pid}" ]] || continue
    if is_expected_uat_frontend_pid "${pid}"; then
      frontend_pid="${pid}"
      break
    fi
  done <<< "$(find_port_pids "${FRONTEND_PORT}")"

  if [[ -z "${frontend_pid}" ]]; then
    echo "UAT frontend port ${FRONTEND_PORT} is listening, but the process is not this repository's Vite frontend."
    lsof -nP -iTCP:"${FRONTEND_PORT}" -sTCP:LISTEN || true
    exit 1
  fi

  if [[ -f "${FRONTEND_LOG}" ]] && grep -Eqi "address already in use|port ${FRONTEND_PORT} is already in use" "${FRONTEND_LOG}"; then
    echo "UAT frontend log contains a strict-port startup failure."
    tail -80 "${FRONTEND_LOG}" || true
    exit 1
  fi

  printf '%s\n' "${frontend_pid}" > "${FRONTEND_PID_FILE}"
  echo "UAT frontend ready at ${FRONTEND_BASE_URL}. PID: ${frontend_pid}"
}

if command -v screen >/dev/null 2>&1; then
  stop_existing_uat_frontend
  stop_existing_uat_backend

  mkdir -p "$(dirname "${BACKEND_LOG}")" "$(dirname "${FRONTEND_LOG}")"
  : > "${BACKEND_LOG}"
  screen -dmS "${BACKEND_SCREEN_SESSION}" bash -lc "cd '${REPO_ROOT}' && dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release > '${BACKEND_LOG}' 2>&1"
  assert_backend_started

  : > "${FRONTEND_LOG}"
  screen -dmS "${FRONTEND_SCREEN_SESSION}" bash -lc "cd '${REPO_ROOT}/frontend' && corepack pnpm exec vite --host 127.0.0.1 --port '${FRONTEND_PORT}' --strictPort > '${FRONTEND_LOG}' 2>&1"
  assert_frontend_started

  echo "Started manual UAT backend screen session: ${BACKEND_SCREEN_SESSION} Log: ${BACKEND_LOG}"
  echo "Started manual UAT frontend screen session: ${FRONTEND_SCREEN_SESSION} Log: ${FRONTEND_LOG}"
else
  stop_existing_uat_frontend
  stop_existing_uat_backend
  mkdir -p "$(dirname "${BACKEND_LOG}")" "$(dirname "${FRONTEND_LOG}")"
  : > "${BACKEND_LOG}"
  nohup dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release > "${BACKEND_LOG}" 2>&1 &
  echo "$!" > "${BACKEND_PID_FILE}"
  assert_backend_started

  (
    cd frontend
    nohup corepack pnpm exec vite --host 127.0.0.1 --port "${FRONTEND_PORT}" --strictPort > "${FRONTEND_LOG}" 2>&1 &
    echo "$!" > "${FRONTEND_PID_FILE}"
  )
  assert_frontend_started

  echo "Started manual UAT backend. PID: $(cat "${BACKEND_PID_FILE}") Log: ${BACKEND_LOG}"
  echo "Started manual UAT frontend. PID: $(cat "${FRONTEND_PID_FILE}") Log: ${FRONTEND_LOG}"
fi
