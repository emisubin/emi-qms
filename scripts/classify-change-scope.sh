#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -n "${CHANGE_SCOPE_REPOSITORY:-}" ]]; then
  if [[ "${CHANGE_SCOPE_ALLOW_TEST_REPOSITORY:-false}" != 'true' ]]; then
    printf 'changeScope=REPOSITORY_OVERRIDE_REJECTED\n' >&2
    exit 64
  fi
  repository_root="${CHANGE_SCOPE_REPOSITORY}"
fi

base_sha="${1:-}"
head_sha="${2:-}"
valid_sha='^[0-9a-f]{40}$'

changed_file_count=0
documentation_only='true'
run_backend='false'
run_frontend='false'
run_full_stack='false'
run_policy_validation='false'
run_azure_validation='false'
deploy_backend='false'
deploy_frontend='false'
run_migration='false'
fail_safe='false'

mark_fail_safe() {
  documentation_only='false'
  run_backend='true'
  run_frontend='true'
  run_full_stack='true'
  run_policy_validation='true'
  run_azure_validation='true'
  deploy_backend='true'
  deploy_frontend='true'
  run_migration='true'
  fail_safe='true'
}

if [[ ! "${base_sha}" =~ ${valid_sha} \
  || ! "${head_sha}" =~ ${valid_sha} \
  || "${base_sha}" =~ ^0+$ \
  || ! -e "${repository_root}/.git" ]] \
  || ! git -C "${repository_root}" cat-file -e "${base_sha}^{commit}" 2>/dev/null \
  || ! git -C "${repository_root}" cat-file -e "${head_sha}^{commit}" 2>/dev/null; then
  mark_fail_safe
else
  while IFS= read -r -d '' changed_path; do
    [[ -n "${changed_path}" ]] || continue
    changed_file_count=$((changed_file_count + 1))

    case "${changed_path}" in
      *.md \
        | FILE_INVENTORY.txt \
        | .github/ISSUE_TEMPLATE/*.yml \
        | .github/ISSUE_TEMPLATE/*.yaml \
        | docs/*.png | docs/*.jpg | docs/*.jpeg | docs/*.webp \
        | docs/*.gif | docs/*.svg | docs/*.pdf | docs/*.xlsx \
        | docs/*.csv | docs/*.json \
        | tasks/*.png | tasks/*.jpg | tasks/*.jpeg | tasks/*.webp \
        | tasks/*.gif | tasks/*.svg | tasks/*.pdf | tasks/*.xlsx \
        | tasks/*.csv | tasks/*.json)
        continue
        ;;
    esac

    documentation_only='false'
    case "${changed_path}" in
      .github/workflows/azure-pilot-images.yml \
        | scripts/deploy-azure-pilot-release.sh \
        | scripts/test-azure-pilot-release.sh \
        | scripts/validate-azure-image-publish-inputs.sh \
        | scripts/test-azure-image-publish-inputs.sh \
        | scripts/validate-azure-pilot-artifacts.sh \
        | scripts/classify-change-scope.sh \
        | scripts/test-change-scope.sh \
        | infrastructure/azure-pilot/* \
        | infrastructure/teams/*)
        run_policy_validation='true'
        run_azure_validation='true'
        ;;
      .github/workflows/* | scripts/*.sh | scripts/*.ps1 | scripts/lib/*)
        run_policy_validation='true'
        ;;
      infrastructure/docker-compose.yml \
        | frontend/playwright.full-stack.config.ts \
        | frontend/e2e/full-stack/* \
        | scripts/e2e-*)
        run_policy_validation='true'
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        ;;
      database/migrations/*)
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        run_azure_validation='true'
        deploy_backend='true'
        run_migration='true'
        ;;
      database/*)
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        run_azure_validation='true'
        deploy_backend='true'
        run_migration='true'
        ;;
      backend/tests/*)
        run_backend='true'
        ;;
      backend/src/*Contracts.cs \
        | backend/src/*EndpointExtensions.cs \
        | backend/src/*/Authorization/* \
        | backend/src/*/Identity/* \
        | backend/src/*/Security/* \
        | backend/src/*/Workflow/* \
        | backend/src/*/Notifications/* \
        | backend/src/*/Program.cs \
        | backend/src/*/appsettings*.json \
        | backend/src/*.csproj \
        | backend/*.sln \
        | backend/*.props \
        | backend/*.targets)
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        deploy_backend='true'
        ;;
      backend/src/*)
        run_backend='true'
        deploy_backend='true'
        ;;
      backend/Dockerfile* | backend/*.json)
        run_backend='true'
        run_azure_validation='true'
        deploy_backend='true'
        ;;
      frontend/src/api.ts \
        | frontend/src/auth.ts \
        | frontend/src/identity.ts \
        | frontend/src/main.tsx \
        | frontend/src/App.tsx)
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        deploy_frontend='true'
        ;;
      frontend/src/*)
        run_frontend='true'
        deploy_frontend='true'
        ;;
      frontend/e2e/* | frontend/tests/*)
        run_frontend='true'
        ;;
      frontend/public/* \
        | frontend/index.html \
        | frontend/vite.config.* \
        | frontend/Dockerfile*)
        run_frontend='true'
        run_azure_validation='true'
        deploy_frontend='true'
        ;;
      frontend/package.json \
        | package.json \
        | pnpm-lock.yaml \
        | pnpm-workspace.yaml)
        run_backend='true'
        run_frontend='true'
        run_full_stack='true'
        deploy_frontend='true'
        ;;
      frontend/*.json \
        | frontend/*.js \
        | frontend/*.ts \
        | frontend/*.mjs \
        | frontend/*.cjs \
        | .node-version)
        run_frontend='true'
        deploy_frontend='true'
        ;;
      assets/branding/*)
        run_frontend='true'
        run_azure_validation='true'
        deploy_frontend='true'
        ;;
      *)
        mark_fail_safe
        ;;
    esac
  done < <(git -C "${repository_root}" diff \
    --no-renames \
    --name-only \
    -z \
    --diff-filter=ACDMRTUXB \
    "${base_sha}" \
    "${head_sha}")
fi

if [[ "${fail_safe}" == 'true' ]]; then
  classification='fail-safe'
elif [[ "${changed_file_count}" -eq 0 ]]; then
  classification='no-changes'
elif [[ "${documentation_only}" == 'true' ]]; then
  classification='documentation-only'
elif [[ "${run_backend}" == 'true' && "${run_frontend}" == 'false' ]]; then
  classification='backend-only'
elif [[ "${run_backend}" == 'false' && "${run_frontend}" == 'true' ]]; then
  classification='frontend-only'
elif [[ "${run_backend}" == 'true' && "${run_frontend}" == 'true' ]]; then
  classification='cross-layer'
elif [[ "${run_azure_validation}" == 'true' ]]; then
  classification='azure-policy-only'
else
  classification='workflow-policy-only'
fi

printf 'classification=%s\n' "${classification}"
printf 'changed_file_count=%s\n' "${changed_file_count}"
printf 'run_backend=%s\n' "${run_backend}"
printf 'run_frontend=%s\n' "${run_frontend}"
printf 'run_full_stack=%s\n' "${run_full_stack}"
printf 'run_policy_validation=%s\n' "${run_policy_validation}"
printf 'run_azure_validation=%s\n' "${run_azure_validation}"
printf 'deploy_backend=%s\n' "${deploy_backend}"
printf 'deploy_frontend=%s\n' "${deploy_frontend}"
printf 'run_migration=%s\n' "${run_migration}"
printf 'fail_safe=%s\n' "${fail_safe}"
