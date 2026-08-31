# TASK-AUDIT-001 Change 004 — MSAL v5 대화형 로그인 기록 보정

- taskType: `BUGFIX`
- changeStatus: `LOCAL_IMPLEMENTATION_VERIFIED / INDEPENDENT_VERIFICATION_PASS / PUBLICATION_APPROVED`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapSequenceMatch: true
- gateStatus: `PASS_REUSE`
- proposedTaskId: `TASK-AUDIT-001`
- canonicalTaskId: `TASK-AUDIT-001`
- samePurposeMatchCount: 1
- reuseExistingTask: true
- userInstructionDate: 2026-08-31
- approvalSource: `USER_EXPLICIT_IN_RESPONSE_TO_RECOMMENDED_MINIMUM_FIX`
- approvedRecommendationExact: `이 최소 수정안으로 바로 고치고 전체 프론트엔드 검증까지 진행할까요?`
- userApprovalExact: `네`
- publicationApprovalDate: 2026-08-31
- publicationApprovalSource: `USER_EXPLICIT`
- publicationApprovalExact: `공개 배포 승인`
- implementationBranch: `fix/task-audit-001-login-record`
- implementationBase: `origin/main@92b93d1e294e18bb7c19ed0495757a3734fddf86`
- candidateRevision: `UNCOMMITTED_WORKTREE — commit not approved`
- sourceTestDiffSha256: `3a272ee86730189da9791ea7d698ea513302ab70da50a7541f555a0ca05b1731`
- independentVerification: `PASS`
- openP0P1P2: `0/0/0`
- commitApproved: true
- pushApproved: true
- pullRequestApproved: true
- mergeApproved: true
- azureDeploymentApproved: true
- frontendDeploymentApproved: true
- backendDeploymentApproved: false
- productionMigrationApproved: false

## 확인된 운영 결함

공개 운영 검수에서 로그인 기록이 생성되지 않았다. 2026-08-28 배포 이후부터 2026-08-31 진단 시점까지의 개인정보 제외 운영 aggregate에서 대화형 로그인 endpoint 호출은 `0`, Backend 로그인 저장 실패도 `0`이었다. 사용자에게 실제 로그인 성공이 있었지만 통제된 재현 횟수는 수집하지 않았으므로, 이 aggregate와 아래 MSAL runtime 계약·source 대조를 함께 root cause 근거로 사용했다. 따라서 DB append 실패가 아니라 Frontend에서 로그인 기록 요청을 시작하지 않은 결함으로 범위를 좁혔다.

MSAL Browser `5.16.0`의 `LOGIN_SUCCESS` event payload는 `AccountInfo` 자체다. 기존 `auth.ts`는 이를 이전 형식인 `AuthenticationResult`로 간주해 `.account`를 읽었고, 결과가 `undefined`여서 pending interaction 기록 전에 반환했다. 실제 업무 로그인은 성공하지만 login correlation과 로그인 감사 row는 생성되지 않았다.

정상 계약은 tracker가 pending interaction UUID를 account별 storage에 저장하고, 인증 token을 얻은 `App.tsx`가 이를 읽어 `POST /api/audit/sessions/interactive-login`을 한 번 호출한 뒤 서버가 반환한 correlation과 receipt를 저장하는 흐름이다. 기존 결함은 이 흐름의 첫 저장 전에 반환했다.

## 승인된 최소 수정 범위

- `frontend/src/auth.ts`에서 `LOGIN_SUCCESS` payload를 `AccountInfo`로 직접 해석하고 유효한 `homeAccountId`가 있을 때만 pending interaction UUID를 저장한다.
- `frontend/src/App.tsx`에서 로그인 시작 시 생성한 UUID를 MSAL request correlation ID와 tab-scoped owner marker로 함께 전달한다.
- `frontend/src/auth.ts`는 로그인 시작 탭만 event를 소비하고 API 요청용 pending을 그 탭의 sessionStorage에 둔다. remember-session 탭에는 shared pending marker만 전달해 이전 correlation 사용을 중단하며, 서버 session 발급 뒤 공유 session으로 교체한다.
- `frontend/tests/auth.test.tsx`에서 MSAL v5의 실제 `AccountInfo` payload를 Redirect와 Popup 각각 두 탭 callback에 전달해 pending consumer가 한 탭뿐인지 검증하고 Silent event 제외를 고정한다.
- Frontend lint, typecheck, 전체 unit test와 production build를 실행한다.
- silent token, cached account 복원, 기존 세션은 로그인 사건으로 기록하지 않는 기존 계약을 보존한다.

## 제외 범위와 불변조건

- Backend, API, DB schema와 migration은 변경하지 않는다.
- 누락된 과거 로그인은 추정하거나 소급 합성하지 않는다.
- token, 계정 원문, 운영 사용자 식별자와 운영 조회 원문은 코드·문서 증빙에 기록하지 않는다.
- 승인된 게시 범위는 이 Change의 Commit·Push·PR·main merge와 Azure Frontend 공개배포다. Backend·migration 배포는 실행하지 않는다.
- 수정 배포 후 검수는 완전한 로그아웃 뒤 운영 UI의 Microsoft Redirect 로그인을 1회 수행해 확인한다. Popup 계약은 자동 회귀로 보존한다.

## 검증 결과

- 실행 환경: Node `v24.18.0`, pnpm `11.19.0`, `@azure/msal-browser` lockfile `5.16.0`, 2026-08-31 KST
- 집중 auth test: `pnpm exec vitest run tests/auth.test.tsx`, `23/23` PASS
- Frontend 전체 unit test: `pnpm run test`, `238/238` PASS. 위 집중 test를 포함한 별도 전체 실행이다.
- Frontend typecheck: `pnpm run typecheck`, PASS
- Frontend lint: `pnpm run lint`, error `0`, 기존 `main.tsx` fast-refresh warning `1`
- Frontend production build: `pnpm run build`, PASS, 기존 대형 bundle warning 유지
- Repository whitespace check: `git diff --check`, PASS
- Backend·DB 변경: 없음

## Finding

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `AUDIT-IMPL-F18` | P1 | `RESOLVED_VERIFIED` | MSAL v5 `LOGIN_SUCCESS`가 `AccountInfo`를 직접 전달하지만 Frontend가 `AuthenticationResult.account`로 읽어 로그인 감사 API 호출이 전부 누락됐다. | payload를 `AccountInfo`로 직접 처리하고 Redirect·Popup 회귀 테스트를 추가했다. 독립 검증 PASS이며 공개 반영에는 별도 게시 승인이 필요하다. |
| `AUDIT-IMPL-F19` | P1 | `RESOLVED_VERIFIED` | MSAL v5 localStorage 모드는 `LOGIN_SUCCESS`를 같은 도메인의 다른 탭에도 전달한다. shared pending을 모든 탭이 소비하면 한 로그인에 API가 중복 호출될 수 있었다. | 로그인 request correlation ID와 tab-scoped owner marker를 연결하고 API용 pending은 owner sessionStorage, 이전 session 차단 표식만 shared localStorage에 분리했다. 두 탭 callback에서 pending write가 owner 1회인지 검증했다. |
| `AUDIT-TEST-F01` | P2 | `RESOLVED_VERIFIED` | 기존 테스트는 pending/session과 API 함수만 검증하고 MSAL event payload 계약을 직접 실행하지 않아 전체 회귀가 결함을 놓쳤다. | 실제 v5 event shape의 callback 테스트를 추가했다. |
| `PRIVACY_QUERY_COMMAND_LEAK` | P2 | `RESOLVED` | 진단 중 한 조회 실패가 허용 fixed projection 밖 운영 식별 metadata를 tool error에 포함했다. Repository artifact에는 저장되지 않았다. | 원문을 재인용하지 않고 폐기했으며 이후 운영 확인은 endpoint count·fixed failure count만 사용하는 성공 aggregate 조회로 제한한다. Repository containment는 완료했고 tool-owned retention은 이 Task에서 변경할 수 없는 platform 경계로 남긴다. |

## 다음 Gate

고정 source/test diff의 독립 read-only 재검토는 `PASS`, Open P0/P1/P2 `0/0/0`이고 사용자가 공개 배포를 승인했다. 다음은 local commit → 원격 Task branch Push·Ready PR 필수 CI → main merge → exact main SHA Azure Frontend 배포 → 공개 사용자 재검수 순서다.

공개 재검수는 동시 검수자가 없는 5분 구간을 정하고, 시작 직전 login endpoint·login row aggregate를 기준값으로 저장한 뒤 Microsoft 세션까지 완전히 로그아웃하고 Redirect 로그인을 정확히 1회 수행한다. 5분 뒤 endpoint 호출과 로그인 row가 각각 기준값 대비 `+1`인지 확인한다. Popup은 자동 회귀로 고정하며 운영 UI가 Redirect를 사용하므로 공개 검수는 Redirect 경로를 사용한다. 사용자·계정·IP 원문은 증빙하지 않는다.
