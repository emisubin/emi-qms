#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

export DATABASE_HOST="${DATABASE_HOST:-localhost}"
export DATABASE_PORT="${DATABASE_PORT:-5432}"
export DATABASE_USER="${DATABASE_USER:-emi_qms}"
export DATABASE_NAME="${UAT_DATABASE_NAME:-emi_qms_uat_005a}"
export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://127.0.0.1:5081"
export FRONTEND_ORIGIN="http://127.0.0.1:5174"
export DEV_AUTHENTICATION_ENABLED="${DEV_AUTHENTICATION_ENABLED:-true}"
export DEV_DATA_SEED_ENABLED="${DEV_DATA_SEED_ENABLED:-true}"
export VITE_API_BASE_URL="http://127.0.0.1:5081"
export VITE_DEV_USER_KEY="${VITE_DEV_USER_KEY:-dev-production}"
export VITE_HMR_HOST="127.0.0.1"
export VITE_HMR_CLIENT_PORT="5174"

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

echo "Ensuring manual UAT production planning schema and master data..."
docker exec -i emi-qms-postgres psql -U "${DATABASE_USER}" -d "${DATABASE_NAME}" >/dev/null <<'SQL'
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
        ('31000000-0000-0000-0000-000000000772'::uuid, 'RRP', 'RRP')
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
    where code in ('UL67', 'UL891', 'UL508A', 'IEC', 'LLP', 'RRP')
),
template_ids(product_type_code, template_id) as (
    values
        ('UL67', '32000000-0000-0000-0000-000000000067'::uuid),
        ('UL891', '32000000-0000-0000-0000-000000000891'::uuid),
        ('UL508A', '32000000-0000-0000-0000-00000000508a'::uuid),
        ('IEC', '32000000-0000-0000-0000-0000000001ec'::uuid),
        ('LLP', '32000000-0000-0000-0000-000000000112'::uuid),
        ('RRP', '32000000-0000-0000-0000-000000000772'::uuid)
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
SQL

echo "Manual UAT fixed environment"
echo "  Backend:  http://127.0.0.1:5081"
echo "  Frontend: http://127.0.0.1:5174"
echo "  DB:       ${DATABASE_NAME}"
echo "  Note: E2E tests use their own temporary database and must not reuse this DB."

BACKEND_LOG="${UAT_BACKEND_LOG:-/tmp/emi-qms-dev-uat-backend.log}"
FRONTEND_LOG="${UAT_FRONTEND_LOG:-/tmp/emi-qms-dev-uat-frontend.log}"
BACKEND_PID_FILE="${UAT_BACKEND_PID_FILE:-/tmp/emi-qms-dev-uat-backend.pid}"
FRONTEND_PID_FILE="${UAT_FRONTEND_PID_FILE:-/tmp/emi-qms-dev-uat-frontend.pid}"

if command -v screen >/dev/null 2>&1; then
  screen -S emi-qms-uat-backend -X quit >/dev/null 2>&1 || true
  screen -S emi-qms-uat-frontend -X quit >/dev/null 2>&1 || true

  screen -dmS emi-qms-uat-backend bash -lc "cd '${REPO_ROOT}' && dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release > '${BACKEND_LOG}' 2>&1"
  screen -dmS emi-qms-uat-frontend bash -lc "cd '${REPO_ROOT}/frontend' && corepack pnpm exec vite --host 127.0.0.1 --port 5174 > '${FRONTEND_LOG}' 2>&1"

  echo "Started manual UAT backend screen session: emi-qms-uat-backend Log: ${BACKEND_LOG}"
  echo "Started manual UAT frontend screen session: emi-qms-uat-frontend Log: ${FRONTEND_LOG}"
else
  nohup dotnet run --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release > "${BACKEND_LOG}" 2>&1 &
  echo "$!" > "${BACKEND_PID_FILE}"

  (
    cd frontend
    nohup corepack pnpm exec vite --host 127.0.0.1 --port 5174 > "${FRONTEND_LOG}" 2>&1 &
    echo "$!" > "${FRONTEND_PID_FILE}"
  )

  echo "Started manual UAT backend. PID: $(cat "${BACKEND_PID_FILE}") Log: ${BACKEND_LOG}"
  echo "Started manual UAT frontend. PID: $(cat "${FRONTEND_PID_FILE}") Log: ${FRONTEND_LOG}"
fi
