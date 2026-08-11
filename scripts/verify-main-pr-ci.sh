#!/usr/bin/env bash
set -euo pipefail

current_sha="${1:-}"
repository="${GITHUB_REPOSITORY:-}"
expected_app_id="${MAIN_PR_CI_EXPECTED_APP_ID:-15368}"
github_cli="${MAIN_PR_CI_GH_BIN:-gh}"

if [[ "${github_cli}" != 'gh' ]]; then
  if [[ "${MAIN_PR_CI_ALLOW_TEST_OVERRIDES:-false}" != 'true' \
    || "${repository}" != 'synthetic/emi-pms' ]]; then
    printf 'mainPrValidation=COMMAND_OVERRIDE_REJECTED\n' >&2
    exit 64
  fi
fi

emit_not_validated() {
  printf 'main_pr_validated=false\n'
  printf 'main_pr_validation_reason=%s\n' "$1"
  exit 0
}

if [[ ! "${current_sha}" =~ ^[0-9a-f]{40}$ \
  || ! "${repository}" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ \
  || ! "${expected_app_id}" =~ ^[1-9][0-9]*$ ]]; then
  emit_not_validated 'invalid-input'
fi

temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-main-pr-validation.XXXXXX")"
cleanup() {
  rm -f "${temporary_directory}/rulesets.json" \
    "${temporary_directory}/ruleset.json" \
    "${temporary_directory}/pulls.json" \
    "${temporary_directory}/compare.json" \
    "${temporary_directory}/checks.json"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

if ! "${github_cli}" api "repos/${repository}/rulesets?includes_parents=true" \
  >"${temporary_directory}/rulesets.json" 2>/dev/null; then
  emit_not_validated 'ruleset-read-failed'
fi

ruleset_guard='false'
while IFS= read -r ruleset_id; do
  [[ "${ruleset_id}" =~ ^[1-9][0-9]*$ ]] || continue
  if ! "${github_cli}" api "repos/${repository}/rulesets/${ruleset_id}" \
    >"${temporary_directory}/ruleset.json" 2>/dev/null; then
    continue
  fi
  if jq -e \
    --argjson app_id "${expected_app_id}" \
    '.enforcement == "active"
      and .target == "branch"
      and any(.conditions.ref_name.include[]?; . == "~DEFAULT_BRANCH" or . == "refs/heads/main")
      and any(.rules[]?; .type == "pull_request")
      and any(.rules[]?;
        .type == "required_status_checks"
        and any(.parameters.required_status_checks[]?;
          .context == "CI Gate" and .integration_id == $app_id))' \
    "${temporary_directory}/ruleset.json" >/dev/null; then
    ruleset_guard='true'
    break
  fi
done < <(jq -r '.[]? | select(.enforcement == "active" and .target == "branch") | .id' \
  "${temporary_directory}/rulesets.json")

if [[ "${ruleset_guard}" != 'true' ]]; then
  emit_not_validated 'required-check-not-enforced'
fi

if ! "${github_cli}" api "repos/${repository}/commits/${current_sha}/pulls" \
  >"${temporary_directory}/pulls.json" 2>/dev/null; then
  emit_not_validated 'pull-request-read-failed'
fi

head_sha="$(jq -r \
  --arg current_sha "${current_sha}" \
  '[.[]? | select(.merged_at != null and .merge_commit_sha == $current_sha) | .head.sha]
    | if length == 1 then .[0] else "" end' \
  "${temporary_directory}/pulls.json")"
if [[ ! "${head_sha}" =~ ^[0-9a-f]{40}$ ]]; then
  emit_not_validated 'merged-pr-not-unique'
fi
base_sha="$(jq -r \
  --arg current_sha "${current_sha}" \
  '[.[]? | select(.merged_at != null and .merge_commit_sha == $current_sha) | .base.sha]
    | if length == 1 then .[0] else "" end' \
  "${temporary_directory}/pulls.json")"
if [[ ! "${base_sha}" =~ ^[0-9a-f]{40}$ ]]; then
  emit_not_validated 'merged-pr-not-unique'
fi

if ! "${github_cli}" api "repos/${repository}/compare/${base_sha}...${head_sha}" \
  >"${temporary_directory}/compare.json" 2>/dev/null; then
  emit_not_validated 'compare-read-failed'
fi
if ! jq -e '.files | type == "array" and length < 300' \
  "${temporary_directory}/compare.json" >/dev/null; then
  emit_not_validated 'compare-incomplete'
fi
if jq -e \
  'any(.files[]?;
    (.filename == ".github/workflows/ci.yml"
      or .filename == "scripts/classify-change-scope.sh"
      or .filename == "scripts/verify-main-pr-ci.sh"
      or .previous_filename == ".github/workflows/ci.yml"
      or .previous_filename == "scripts/classify-change-scope.sh"
      or .previous_filename == "scripts/verify-main-pr-ci.sh"))' \
  "${temporary_directory}/compare.json" >/dev/null; then
  emit_not_validated 'ci-trust-source-changed'
fi

current_tree="$("${github_cli}" api "repos/${repository}/git/commits/${current_sha}" \
  --jq '.tree.sha' 2>/dev/null || true)"
head_tree="$("${github_cli}" api "repos/${repository}/git/commits/${head_sha}" \
  --jq '.tree.sha' 2>/dev/null || true)"
if [[ ! "${current_tree}" =~ ^[0-9a-f]{40}$ \
  || "${current_tree}" != "${head_tree}" ]]; then
  emit_not_validated 'tree-mismatch'
fi

if ! "${github_cli}" api "repos/${repository}/commits/${head_sha}/check-runs" \
  >"${temporary_directory}/checks.json" 2>/dev/null; then
  emit_not_validated 'check-read-failed'
fi

if ! jq -e \
  --argjson app_id "${expected_app_id}" \
  'any(.check_runs[]?;
    .name == "CI Gate"
    and .app.id == $app_id
    and .status == "completed"
    and .conclusion == "success")' \
  "${temporary_directory}/checks.json" >/dev/null; then
  emit_not_validated 'ci-gate-not-successful'
fi

printf 'main_pr_validated=true\n'
printf 'main_pr_validation_reason=validated-pr-tree\n'
