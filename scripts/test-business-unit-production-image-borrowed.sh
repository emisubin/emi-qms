#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scope="$(mktemp -d "${TMPDIR:-/tmp}/pms-borrowed-image-test.XXXXXX")"
trap 'rm -f "${scope}/docker" "${scope}/calls" "${scope}/container" "${scope}/output"; rmdir "${scope}"' EXIT
cat >"${scope}/docker" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >>"${BORROWED_MOCK_SCOPE}/calls"
case "$1 ${2:-}" in
  'info ') exit 0 ;;
  'image inspect')
    [[ "${BORROWED_MOCK_MODE}" != missing ]] || exit 1
    if [[ "$*" == *'{{.Id}}'* ]]; then
      if [[ "${BORROWED_MOCK_MODE}" == identity ]]; then printf 'sha256:%064d\n' 0; else printf '%s\n' "${BUSINESS_UNIT_PRODUCTION_IMAGE_REF}"; fi
    elif [[ "$*" == *'{{.Os}}/{{.Architecture}}'* ]]; then
      if [[ "${BORROWED_MOCK_MODE}" == platform ]]; then echo linux/arm64; else echo linux/amd64; fi
    fi ;;
  'image ls'|'network ls'|'volume ls') ;;
  'container inspect')
    [[ -f "${BORROWED_MOCK_SCOPE}/container" ]] || exit 1
    if [[ "$*" == *'test.owner'* ]]; then echo business-unit-production-image
    elif [[ "$*" == *'test.run-id'* ]]; then echo "${E2E_RUN_ID}"; fi ;;
  'ps -aq') [[ ! -f "${BORROWED_MOCK_SCOPE}/container" ]] || echo synthetic-container ;;
  'create --name') touch "${BORROWED_MOCK_SCOPE}/container"; echo synthetic-container ;;
  'container rm') rm "${BORROWED_MOCK_SCOPE}/container" ;;
  'cp '*) exit 1 ;;
  *) echo unexpected-mock-command >&2; exit 99 ;;
esac
MOCK
chmod +x "${scope}/docker"
image_id="sha256:$(printf 'a%.0s' {1..64})"
case_count=0
for mode in mutable missing platform identity injected extraction; do
  ref="${image_id}"
  injection=none
  expected=1
  case "${mode}" in
    mutable) ref=example:tag; expected=64 ;;
    missing) expected=64 ;;
    injected) injection=after-inspection-container; expected=97 ;;
  esac
  : >"${scope}/calls"
  set +e
  env -u E2E_COMPOSE_PROJECT_NAME -u E2E_DATABASE_NAME \
    PATH="${scope}:${PATH}" BORROWED_MOCK_SCOPE="${scope}" BORROWED_MOCK_MODE="${mode}" \
    E2E_RUN_ID="borrowed_test_${mode}" E2E_BACKEND_PORT=5089 E2E_FRONTEND_PORT=5189 \
    BUSINESS_UNIT_PRODUCTION_IMAGE_REF="${ref}" BUSINESS_UNIT_PRODUCTION_IMAGE_TEST_INJECTION="${injection}" \
    bash "${repo_root}/scripts/test-business-unit-production-image.sh" >"${scope}/output" 2>&1
  result=$?
  set -e
  if [[ "${result}" != "${expected}" ]]; then
    cat "${scope}/output" >&2
    printf 'borrowedImageMock=%s:EXPECTED_%s_ACTUAL_%s\n' "${mode}" "${expected}" "${result}" >&2
    exit 1
  fi
  [[ ! -f "${scope}/container" ]]
  if grep -Eq '^(build|image rm|compose|run) ' "${scope}/calls"; then
    echo 'borrowedImageMock=UNOWNED_RESOURCE_OPERATION' >&2; exit 1
  fi
  if [[ "${mode}" == injected || "${mode}" == extraction ]]; then
    grep -Fxq 'businessUnitProductionImageCleanupContainers=0' "${scope}/output"
    grep -Fxq 'businessUnitProductionImageCleanupImages=0' "${scope}/output"
    grep -Fq 'container rm --force emi-qms-business-unit-production-catalog-' "${scope}/calls"
  fi
  case_count=$((case_count + 1))
  printf 'borrowedImageMock=%s:PASS\n' "${mode}"
done
printf 'borrowedImageMockTests=%s:PASS\n' "${case_count}"
