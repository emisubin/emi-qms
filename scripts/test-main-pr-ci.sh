#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
validator="${repository_root}/scripts/verify-main-pr-ci.sh"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/pms-main-pr-ci-test.XXXXXX")"

cleanup() {
  rm -f "${temporary_directory}/gh" "${temporary_directory}/stdout" "${temporary_directory}/stderr"
  rmdir "${temporary_directory}" 2>/dev/null || true
}
trap cleanup EXIT

cat >"${temporary_directory}/gh" <<'MOCK_GH'
#!/usr/bin/env bash
set -euo pipefail

endpoint="${2:-}"
case "${endpoint}" in
  'repos/synthetic/emi-pms/rulesets?includes_parents=true')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'api-failed' ]]; then
      exit 1
    fi
    printf '[{"id":1,"enforcement":"active","target":"branch"}]\n'
    ;;
  'repos/synthetic/emi-pms/rulesets/1')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'ruleset-missing' ]]; then
      printf '{"enforcement":"active","target":"branch","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"]}},"rules":[{"type":"pull_request"}]}\n'
    else
      printf '{"enforcement":"active","target":"branch","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"]}},"rules":[{"type":"pull_request"},{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"CI Gate","integration_id":15368}]}}]}\n'
    fi
    ;;
  'repos/synthetic/emi-pms/commits/1111111111111111111111111111111111111111/pulls')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'no-pr' ]]; then
      printf '[]\n'
    else
      printf '[{"merged_at":"2026-01-01T00:00:00Z","merge_commit_sha":"1111111111111111111111111111111111111111","head":{"sha":"2222222222222222222222222222222222222222"},"base":{"sha":"3333333333333333333333333333333333333333"}}]\n'
    fi
    ;;
  'repos/synthetic/emi-pms/compare/3333333333333333333333333333333333333333...2222222222222222222222222222222222222222')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'self-change' ]]; then
      printf '{"files":[{"filename":".github/workflows/ci.yml"}]}\n'
    else
      printf '{"files":[{"filename":"frontend/src/styles.css"}]}\n'
    fi
    ;;
  'repos/synthetic/emi-pms/git/commits/1111111111111111111111111111111111111111')
    printf 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n'
    ;;
  'repos/synthetic/emi-pms/git/commits/2222222222222222222222222222222222222222')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'tree-mismatch' ]]; then
      printf 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n'
    else
      printf 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n'
    fi
    ;;
  'repos/synthetic/emi-pms/commits/2222222222222222222222222222222222222222/check-runs')
    if [[ "${MAIN_PR_CI_TEST_SCENARIO}" == 'check-failed' ]]; then
      printf '{"check_runs":[{"name":"CI Gate","app":{"id":15368},"status":"completed","conclusion":"failure"}]}\n'
    else
      printf '{"check_runs":[{"name":"CI Gate","app":{"id":15368},"status":"completed","conclusion":"success"}]}\n'
    fi
    ;;
  *)
    exit 2
    ;;
esac
MOCK_GH
chmod 700 "${temporary_directory}/gh"

run_case() {
  local scenario="$1"
  local expected="$2"
  MAIN_PR_CI_TEST_SCENARIO="${scenario}" \
    MAIN_PR_CI_GH_BIN="${temporary_directory}/gh" \
    MAIN_PR_CI_ALLOW_TEST_OVERRIDES='true' \
    GITHUB_REPOSITORY='synthetic/emi-pms' \
    "${validator}" '1111111111111111111111111111111111111111' \
    >"${temporary_directory}/stdout" 2>"${temporary_directory}/stderr"
  if ! grep -Fqx "${expected}" "${temporary_directory}/stdout"; then
    printf 'mainPrCiTests=UNEXPECTED_%s\n' "${scenario}" >&2
    exit 1
  fi
}

run_case success 'main_pr_validated=true'
run_case ruleset-missing 'main_pr_validation_reason=required-check-not-enforced'
run_case no-pr 'main_pr_validation_reason=merged-pr-not-unique'
run_case tree-mismatch 'main_pr_validation_reason=tree-mismatch'
run_case check-failed 'main_pr_validation_reason=ci-gate-not-successful'
run_case self-change 'main_pr_validation_reason=ci-trust-source-changed'
run_case api-failed 'main_pr_validation_reason=ruleset-read-failed'

printf 'mainPrCiTests=PASS\n'
