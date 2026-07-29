#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
cd "${repo_root}"

export ASPNETCORE_ENVIRONMENT="Testing"
export AUTH_MODE="Dev"
export Authentication__Mode="Dev"
export DEV_AUTHENTICATION_ENABLED="true"
export DEV_DATA_SEED_ENABLED="true"
export DATABASE_APPLY_MIGRATIONS_ON_STARTUP="true"
export DATABASE_HOST="127.0.0.1"
export DATABASE_PORT="5432"
export DATABASE_USER="emi_qms_experiment_validation"
export DATABASE_PASSWORD="e2e_local_only_change_me"
export DATABASE_NAME="emi_qms_experiment_validation_41164"
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
export AdminDeletionPurge__Enabled="false"
export Frontend__Origin="http://127.0.0.1:42983"
export FRONTEND_ORIGIN="http://127.0.0.1:42983"

exec dotnet watch \
  --project backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj \
  --no-hot-reload \
  --non-interactive \
  --configuration Release \
  run \
  --urls http://127.0.0.1:41166
