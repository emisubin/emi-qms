#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=scripts/lib/e2e-safety.sh
source "${repo_root}/scripts/lib/e2e-safety.sh"

e2e_require_safe_database_name "${E2E_DATABASE_NAME:-}"
e2e_assert_dedicated_postgres
e2e_disable_external_providers

cheongju_legacy_overlay="${repo_root}/database/business-migrations/cheongju/0132_g2_daily_workspace.sql"
common_migrations_path="${repo_root}/database/migrations"
[[ -f "${cheongju_legacy_overlay}" \
  && "$(basename "${cheongju_legacy_overlay}")" == "0132_g2_daily_workspace.sql" ]] || {
  e2e_safety_error "Canonical Cheongju G2 workspace migration was not found."
  exit "${E2E_SAFETY_EXIT_CODE}"
}
[[ -d "${common_migrations_path}" ]] || {
  e2e_safety_error "Common migration catalog was not found."
  exit "${E2E_SAFETY_EXIT_CODE}"
}
common_migration_count="$(find "${common_migrations_path}" -maxdepth 1 -type f -name '*.sql' -print | wc -l | tr -d '[:space:]')"
common_latest_file="$(find "${common_migrations_path}" -maxdepth 1 -type f -name '*.sql' -print | LC_ALL=C sort | tail -n 1)"
common_latest_version="$(basename "${common_latest_file}" .sql)"
[[ "${common_migration_count}" =~ ^[1-9][0-9]*$ \
  && "${common_latest_version}" =~ ^[0-9]{4}_[a-z0-9_]+$ ]] || {
  e2e_safety_error "Common migration catalog could not be identified safely."
  exit "${E2E_SAFETY_EXIT_CODE}"
}

run_e2e_database_sql() {
  local sql="$1"
  e2e_require_safe_database_name "${E2E_DATABASE_NAME:-}" || return $?
  e2e_assert_dedicated_postgres || return $?
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname "${E2E_DATABASE_NAME}" \
    --no-psqlrc \
    --set ON_ERROR_STOP=1 \
    --command "${sql}"
}

apply_cheongju_legacy_overlay() {
  e2e_require_safe_database_name "${E2E_DATABASE_NAME:-}"
  e2e_assert_dedicated_postgres
  e2e_compose exec -T "${E2E_POSTGRES_SERVICE}" psql \
    --username "${E2E_DATABASE_USER}" \
    --dbname "${E2E_DATABASE_NAME}" \
    --no-psqlrc \
    --set ON_ERROR_STOP=1 \
    --single-transaction \
    --file - < "${cheongju_legacy_overlay}"
}

# Selected end-to-end scenarios verify that notification outbox rows are created.
# External providers remain disabled/dry-run, so enabling the dispatcher here never
# calls Teams or mail outside the isolated test environment.
if [[ "${E2E_SAFE_NOTIFICATION_DISPATCH:-false}" == "true" ]]; then
  export Notifications__Dispatch__Enabled="true"
  export Notifications__Dispatch__WorkerIntervalSeconds="5"
fi

if [[ "${E2E_SAFE_WEB_PUSH_DRY_RUN:-false}" == "true" ]]; then
  export Notifications__WebPush__Enabled="true"
  export Notifications__WebPush__DryRun="true"
  export Notifications__WebPush__PublicKey="BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
  export Notifications__WebPush__AllowedEndpointHostSuffixes__0="example.test"
  unset Notifications__WebPush__PrivateKey
fi

export ASPNETCORE_ENVIRONMENT=Testing
export AUTH_MODE=Dev
export Authentication__Mode=Dev
export DEV_AUTHENTICATION_ENABLED=true
export DEV_DATA_SEED_ENABLED=true
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP=true
export DATABASE_NAME="${E2E_DATABASE_NAME}"
export DATABASE_HOST="${DATABASE_HOST:?DATABASE_HOST is required}"
export DATABASE_PORT="${DATABASE_PORT:?DATABASE_PORT is required}"
export DATABASE_USER="${DATABASE_USER:?DATABASE_USER is required}"
export DATABASE_PASSWORD="${DATABASE_PASSWORD:?DATABASE_PASSWORD is required}"
export E2E_BACKEND_PORT="${E2E_BACKEND_PORT:?E2E_BACKEND_PORT is required}"
export E2E_FRONTEND_PORT="${E2E_FRONTEND_PORT:?E2E_FRONTEND_PORT is required}"
export Frontend__Origin="http://127.0.0.1:${E2E_FRONTEND_PORT}"
export FRONTEND_ORIGIN="http://127.0.0.1:${E2E_FRONTEND_PORT}"

# The generic browser test fixture still uses a legacy-shaped single database.
# Cheongju's 0131 separation migration is destructive to that combined fixture,
# so this E2E-only adapter applies the later canonical G2 migration without
# recording a business-target version in the legacy common ledger.
dotnet run \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --configuration Release \
  --no-build \
  -- --migrate-only

run_e2e_database_sql "
do \$legacy_overlay_guard\$
begin
  if current_database() <> '${E2E_DATABASE_NAME}' then
    raise exception 'e2e_legacy_overlay_database_mismatch';
  end if;
  if (select count(*) from schema_migrations) <> ${common_migration_count}
     or not exists (select 1 from schema_migrations where version = '${common_latest_version}') then
    raise exception 'e2e_legacy_overlay_common_ledger_incomplete';
  end if;
  if exists (select 1 from qms_database_identity) then
    raise exception 'e2e_legacy_overlay_business_database_forbidden';
  end if;
  if to_regclass('public.g2_daily_values') is not null
     or to_regclass('public.g2_daily_notes') is not null then
    raise exception 'e2e_legacy_overlay_already_applied';
  end if;
  if to_regclass('public.g2_daily_metrics') is null
     or to_regclass('public.g2_inventory_counts') is null
     or to_regclass('public.g2_defect_inventory_counts') is null
     or to_regclass('public.permissions') is null
     or to_regclass('public.role_permissions') is null
     or to_regclass('public.qms_users') is null then
    raise exception 'e2e_legacy_overlay_base_schema_missing';
  end if;
end
\$legacy_overlay_guard\$;"

apply_cheongju_legacy_overlay

run_e2e_database_sql "
do \$legacy_overlay_verify\$
begin
  if to_regclass('public.g2_daily_values') is null
     or to_regclass('public.g2_daily_notes') is null
     or to_regclass('public.ux_g2_daily_metrics_actual_date_code') is null
     or to_regclass('public.ux_g2_daily_metrics_forecast_date_code') is null then
    raise exception 'e2e_legacy_overlay_relations_missing';
  end if;
  if (select count(*) from information_schema.columns
      where table_schema = 'public'
        and ((table_name = 'g2_inventory_counts' and column_name = 'quantity')
          or (table_name = 'g2_defect_inventory_counts' and column_name = 'quantity'))
        and is_nullable = 'YES') <> 2 then
    raise exception 'e2e_legacy_overlay_nullable_counts_missing';
  end if;
  if (select count(*) from permissions where code in (
      'G2.ProductionPlan.Update','G2.DeliveryPlan.Update','G2.PlanNote.Update','G2.Note.Update')) <> 4 then
    raise exception 'e2e_legacy_overlay_permissions_missing';
  end if;
  if (select count(*) from pg_trigger
      where not tgisinternal and tgname in (
        'trg_qms_global_audit_g2_daily_values','trg_qms_global_audit_g2_daily_notes')) <> 2 then
    raise exception 'e2e_legacy_overlay_audit_triggers_missing';
  end if;
  if (select count(*) from schema_migrations) <> ${common_migration_count}
     or not exists (select 1 from schema_migrations where version = '${common_latest_version}')
     or exists (select 1 from schema_migrations where version = '0132_g2_daily_workspace') then
    raise exception 'e2e_legacy_overlay_common_ledger_changed';
  end if;
  if exists (select 1 from qms_database_identity) then
    raise exception 'e2e_legacy_overlay_business_identity_created';
  end if;
end
\$legacy_overlay_verify\$;"

export DATABASE_APPLY_MIGRATIONS_ON_STARTUP=false
export Database__ApplyMigrationsOnStartup=false

dotnet run \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --configuration Release \
  --no-build \
  --urls "http://127.0.0.1:${E2E_BACKEND_PORT}"
