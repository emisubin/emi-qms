#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
classifier="${repository_root}/scripts/classify-change-scope.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-change-scope-test.XXXXXX")"

cleanup() {
  rm -rf "${temporary_directory}/repository"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

synthetic_repository="${temporary_directory}/repository"
git init --quiet "${synthetic_repository}"
git -C "${synthetic_repository}" config user.name 'Synthetic CI'
git -C "${synthetic_repository}" config user.email 'synthetic@invalid'

commit_file() {
  local path="$1"
  local value="$2"
  mkdir -p "${synthetic_repository}/$(dirname "${path}")"
  printf '%s\n' "${value}" >"${synthetic_repository}/${path}"
  git -C "${synthetic_repository}" add -- "${path}"
  git -C "${synthetic_repository}" commit --quiet -m "synthetic change"
}

commit_file README.md baseline
baseline_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"

assert_scope() {
  local name="$1"
  local expected="$2"
  local base="$3"
  local head="$4"
  local output
  output="$(CHANGE_SCOPE_REPOSITORY="${synthetic_repository}" \
    CHANGE_SCOPE_ALLOW_TEST_REPOSITORY='true' \
    "${classifier}" "${base}" "${head}")"
  if ! grep -Fqx "${expected}" <<<"${output}"; then
    printf 'changeScopeTests=UNEXPECTED_%s_%s\n' "${name}" "${expected}" >&2
    exit 1
  fi
}

commit_file docs/guide.md docs
docs_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope docs 'classification=documentation-only' "${baseline_sha}" "${docs_sha}"
assert_scope docs-backend 'run_backend=false' "${baseline_sha}" "${docs_sha}"

commit_file backend/tests/SampleTests.cs test
backend_test_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope backend-test 'classification=backend-only' "${docs_sha}" "${backend_test_sha}"
assert_scope backend-test-deploy 'deploy_backend=false' "${docs_sha}" "${backend_test_sha}"

commit_file backend/src/Emi.Qms.Api/Projects/ProjectStore.cs store
backend_store_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope backend-store 'run_backend=true' "${backend_test_sha}" "${backend_store_sha}"
assert_scope backend-store-front 'run_frontend=false' "${backend_test_sha}" "${backend_store_sha}"
assert_scope backend-store-deploy 'deploy_backend=true' "${backend_test_sha}" "${backend_store_sha}"

commit_file backend/src/Emi.Qms.Api/Projects/ProjectContracts.cs contract
contract_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope contract 'classification=cross-layer' "${backend_store_sha}" "${contract_sha}"
assert_scope contract-e2e 'run_full_stack=true' "${backend_store_sha}" "${contract_sha}"

commit_file frontend/src/styles.css style
style_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope style 'classification=frontend-only' "${contract_sha}" "${style_sha}"
assert_scope style-e2e 'run_full_stack=false' "${contract_sha}" "${style_sha}"

commit_file frontend/src/api.ts api
api_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope api 'classification=cross-layer' "${style_sha}" "${api_sha}"
assert_scope api-backend-deploy 'deploy_backend=false' "${style_sha}" "${api_sha}"
assert_scope api-frontend-deploy 'deploy_frontend=true' "${style_sha}" "${api_sha}"

commit_file database/migrations/9999_synthetic.sql migration
migration_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope migration 'run_migration=true' "${api_sha}" "${migration_sha}"
assert_scope migration-e2e 'run_full_stack=true' "${api_sha}" "${migration_sha}"

commit_file .github/workflows/ci.yml workflow
workflow_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope workflow 'classification=workflow-policy-only' "${migration_sha}" "${workflow_sha}"
assert_scope workflow-policy 'run_policy_validation=true' "${migration_sha}" "${workflow_sha}"

commit_file .github/workflows/azure-pilot-images.yml azure
azure_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope azure 'classification=azure-policy-only' "${workflow_sha}" "${azure_sha}"
assert_scope azure-validation 'run_azure_validation=true' "${workflow_sha}" "${azure_sha}"

commit_file unknown.configuration unknown
unknown_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope unknown 'classification=fail-safe' "${azure_sha}" "${unknown_sha}"
assert_scope unknown-migration 'run_migration=true' "${azure_sha}" "${unknown_sha}"

mkdir -p "${synthetic_repository}/backend/src/Emi.Qms.Api/Projects"
printf 'rename\n' >"${synthetic_repository}/backend/src/Emi.Qms.Api/Projects/RenameStore.cs"
git -C "${synthetic_repository}" add --all
git -C "${synthetic_repository}" commit --quiet -m 'rename source'
rename_base_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
mkdir -p "${synthetic_repository}/docs"
git -C "${synthetic_repository}" mv \
  backend/src/Emi.Qms.Api/Projects/RenameStore.cs \
  docs/renamed.md
git -C "${synthetic_repository}" commit --quiet -m 'rename to docs'
rename_head_sha="$(git -C "${synthetic_repository}" rev-parse HEAD)"
assert_scope rename 'run_backend=true' "${rename_base_sha}" "${rename_head_sha}"

assert_scope no-change 'classification=no-changes' "${rename_head_sha}" "${rename_head_sha}"
assert_scope invalid 'classification=fail-safe' invalid "${rename_head_sha}"

printf 'changeScopeTests=PASS\n'
