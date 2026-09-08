# TASK-OSAN-ACCESS-001 Change 001·002·003·004 구현 보고

## 1. 실행 기준과 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- currentChangeTaskType: `BUGFIX`
- canonicalTask: `TASK-OSAN-ACCESS-001`
- canonicalChange: `TASK-OSAN-ACCESS-001 Change 001, Change 002, Change 003, Change 004`
- instructionChainRead: true
- change001TaskIdentityGate: `PASS_REUSE`
- change001RoadmapSequenceMatch: true
- change001ImplementationApprovalSource: `USER_EXPLICIT_2026-09-06_NEXT_TASK_START`
- change001LocalBaselineApprovalSource: `USER_EXPLICIT_2026-09-06_APPROVED`
- change001ImplementationBranch: `feat/task-osan-access-001-membership-switching`
- change001ImplementationBaseline: `670b2eafaa142f4be2febec206bd4f494707aea0`
- change001ImplementationWorktree: `/private/tmp/emi-osan-access-001`
- change001ImplementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- change001ImplementationOwnerObserved: `NOT_REPORTED`
- implementationStatus: `CHANGE_003_004_USER_VALIDATED_AWAITING_PR_CI`
- change002ApprovalSource: `USER_EXPLICIT_2026-09-07_SELECTOR_VISIBILITY_FIX`
- change002TaskIdentityGate: `PASS_REUSE`
- change002RoadmapSequenceMatch: false
- change002ExplicitRoadmapOverrideApproved: true
- change002ImplementationOwnerRequested: `GPT_5_6_SOL_XHIGH_ONLY`
- change002ImplementationOwnerObserved: `NOT_REPORTED`
- change002Gpt6ReviewRequested: false
- change002Gpt6ReviewProhibitedByUser: true
- change002ImplementationBranch: `feat/task-osan-project-001-project-registration`
- change002ImplementationBaseline: `d7401610311638317a2606416845492f03ae58fb`
- change002ImplementationWorktree: `/private/tmp/emi-osan-project-001`
- change003ApprovalSource: `USER_EXPLICIT_2026-09-07_INTEGRATED_USER_APPROVAL`
- change003TaskIdentityGate: `PASS_REUSE`
- change003RoadmapSequenceMatch: false
- change003ExplicitRoadmapOverrideApproved: true
- change003ImplementationOwnerRequested: `GPT_5_6_SOL_XHIGH_ONLY`
- change003ImplementationOwnerObserved: `NOT_REPORTED`
- change003Gpt6ReviewProhibitedByUser: true
- change003ImplementationBranch: `fix/task-osan-access-001-integrated-user-approval`
- change003ImplementationBaseline: `11c1185ea9c550022e3f70d106e06a1c6bc517b1`
- change003ImplementationWorktree: `/Users/parksubin/.codex/visualizations/2026/09/05/01a07195-0215-7572-8126-f3d7e385169a/emi-osan-change-003`
- change004ApprovalSource: `USER_EXPLICIT_2026-09-07_AUTOMATIC_BUSINESS_ENTRY_AND_CHEONGJU_ADMIN_ONLY`
- change004TaskIdentityGate: `PASS_REUSE`
- change004ImplementationOwnerRequested: `GPT_5_6_SOL_HIGH_ONLY`
- change004ImplementationOwnerObserved: `NOT_REPORTED`
- change004ImplementationBranch: `fix/task-osan-access-001-integrated-user-approval`
- change004ImplementationBaseline: `958661459786acfb564591b3c46251c9b854c50c`
- change001FinalVerifierCorrectionTaskType: `P2_REMEDIATION`
- change001FinalVerifierRequested: `GPT_6_ASTRA_HIGH`
- change001FinalVerifierObserved: `NOT_REPORTED`
- change001FinalVerifierVerdict: `GO`
- change001FinalVerifierManifestSha256: `1debc180aadde8afb84853829ac7ca0b78d79cdffc0e5ef2b1de4b8acd347bb6`
- gitPublicationApproved: true
- remoteCiApproved: true
- mainMergeApproved: false
- userValidationStatus: `COMPLETED`
- latestUserApprovalSource: `USER_EXPLICIT_2026-09-08_NEXT_TASK_APPROVED`
- persistentRuntimeMutationApproved: false

구현 전에 canonical Root 지침과 이 worktree의 Backend·Frontend·Scripts 지침, Product Roadmap, Task 종료 정책, Validation Matrix, Privacy-safe Evidence, 승인 Task와 Change 001을 다시 읽었다. Fresh GPT-6 High NO-GO 보정을 시작하기 전에도 현재 instruction chain, branch, 기준선과 dirty diff를 다시 확인했다. Parent가 Change 001에 기록한 제품 allowlist 확장과 Task 전용 combined full-stack test infrastructure 확장을 적용한다.

- `BusinessUnitCapabilityMiddleware.cs`: 승인된 총괄 membership endpoint를 오산에서 허용
- `AuditMutationRegistry.cs`: exact membership PUT을 known mutation/local business audit 제외로 분류
- `EntraClaimsTransformation.cs`, `DbIdentityStore.cs`: 신규 Entra 사용자 onboarding 단절 보정
- `scripts/e2e-business-unit-access-full-stack.sh`, `frontend/playwright.business-unit-access.full-stack.config.ts`: 실제 synthetic 3개 DB와 실제 backend/frontend를 함께 검증하는 additive Task 전용 harness
- `frontend/tests/auth.test.tsx`: Parent가 `PARENT_APPROVED_SAME_TASK_TEST_DEPENDENCY_2026-09-07`로 추가한 기존 Entra logout handler test-only 경로

네 차례 Fresh verifier가 확인한 implicit single-membership context, stored selection 없는 membership denial, ReviewSafe Entra write와 gate mutation control, raw external subject label, membership lock ordering, 실제 logout wiring, combined full-stack 및 process ownership/cleanup, 일반 미소속 runtime remount loop를 보정했다. 최종 보정 소스에 대한 새 집중·전체·browser 검증 결과만 아래 최종 증거로 사용한다.

Change 002는 Task 3 Change 005 화면의 사용자 검수 완료 직후 사용자가 직접 요청한 selector 가시성 보정이다. 사용자는 GPT-6 없이 GPT-5.6 Sol Extra high가 단독 구현·검증·기록·local commit하도록 지시했다. 현재 Roadmap의 Task 4보다 이 보정을 먼저 수행하라는 명시적 우선순위 변경으로 `TASK-OSAN-ACCESS-001`을 재사용했고, 기존 Change 001의 GPT-6 검증 기록은 역사적 근거로 보존하되 Change 002에 새 GPT-6 검증을 요청하거나 실행하지 않았다.

## 2. 구현 결과

### Directory 소속과 총괄 권한 분리

- Directory migration `0002`가 display name/email과 membership 변경 actor·before·after 감사를 추가한다. 기존 bootstrap 감사 행과 append-only 보호는 유지한다.
- Membership 변경은 fixed safe `search_path`를 가진 `SECURITY DEFINER` 함수 하나로만 수행한다. 함수가 같은 transaction에서 active actor identity와 active overall designation, target identity, active known business unit을 다시 검증한다.
- 함수는 actor와 target identity를 UUID 순서의 `FOR UPDATE`로 먼저 확보하고 overall designation도 `FOR UPDATE`로 검증한다. Actor와 target이 같거나 두 총괄이 서로를 target으로 삼아도 lock upgrade나 역순 잠금 없이 직렬화된다.
- 함수는 membership만 변경하며 overall designation을 변경하지 않는다. 실제 변경이 없으면 감사 행을 만들지 않고, 실제 변경이면 before/after/actor 감사 행을 정확히 하나 추가한다.
- PUBLIC execute를 회수하고 기존 runtime privilege reconciliation이 configured directory runtime role에만 owned function execute를 부여한다. Runtime role의 directory table·audit 직접 write는 계속 거부된다.
- 총괄 전용 API는 다음 두 route뿐이다.
  - `GET /api/admin/business-unit-access/users`
  - `PUT /api/admin/business-unit-access/users/{userId}/memberships`
- Endpoint는 trusted request context와 overall claim, directory UUID 일치를 모두 요구하고 mutation 함수도 DB에서 actor를 다시 확인한다. Local `system-administrator` 또는 `users.manage`만으로 총괄 권한을 얻지 않는다.

### 신규 Entra 사용자 onboarding

- 인증된 Entra identity의 첫 요청은 제한된 directory 함수로 본인의 pending identity와 최소 display name/email만 등록·갱신한다. 서버가 UUID를 만들거나 기존 UUID를 보존하며 membership과 overall designation은 생성하지 않는다.
- 소속 전에는 business DB에 연결하거나 local role·department·project 데이터를 만들지 않는다.
- 소속과 선택이 확인된 뒤에만 선택 business DB에 directory UUID·oid·Entra provider·identity key가 모두 일치하는 active local profile을 만든다. 신규 profile은 department가 없고 role도 없다.
- 다중 DB 경로는 legacy email bootstrap administrator 승격을 호출하지 않는다. UUID, oid, provider, identity key 충돌 또는 inactive profile은 `local_profile_pending`으로 fail closed한다.
- 총괄 목록과 화면은 Entra display name/email을 표시해 generic card를 구별할 수 있다.

### 선택 사업부 local 사용자 관리

- 기존 `/api/admin/users` GET과 exact `/api/admin/users/{guid}` PATCH를 선택 business DB의 local profile·department·role 관리에 그대로 사용한다.
- 오산 화면은 제목을 `현재 사업부 사용자 관리`로 표시하고 active, department, department head, role 수정만 제공한다.
- 오산에서는 notification settings, export, 선택 삭제·복구, 개별 delete·restore·purge와 lifecycle 문구/control을 숨긴다. Backend capability도 해당 route를 계속 거부한다.
- 청주 single-DB와 청주 선택 화면은 기존 전체 사용자 관리 control을 유지한다.

### 탭별 선택과 요청 무효화

- 사업부 선택은 `sessionStorage`에만 보관하고 공통 API layer가 `X-Qms-Business-Unit`을 붙인다.
- Safe request는 함수 진입 시 generation과 선택값을 고정하고 token 취득 전부터 AbortController를 등록한다. Token 대기, network read, response header 이후 JSON/blob body 소비 중 어느 시점에 전환되어도 이전 요청은 `BusinessUnitRequestInvalidatedError`로 끝나며 데이터를 반환하지 않는다.
- Raw GET body의 controller와 forwarded signal listener는 body 완료 후 해제된다. Profile photo 404도 빈 body를 소비한 뒤 null을 반환해 tracking resource가 누적되지 않는다.
- POST download/export는 response body 소비가 끝날 때까지 mutation switch lock을 유지한다. 모든 mutation은 결과가 확정되기 전 강제 abort하지 않고 selector를 잠근다.
- 로그아웃은 tab selection을 제거하고 outstanding read를 abort하며 business generation을 무효화한 뒤 MSAL active account/token context를 해제하고 `logoutRedirect`에 연결된다.
- 어느 API든 `business_unit_membership_denied`, `business_unit_alternate_selection_denied`, `business_unit_selector_invalid`, `directory_membership_required`를 반환하면 저장된 선택을 지우고 shell을 remount한다. 일반 permission/capability 403은 선택을 지우지 않는다.
- `/api/me`의 성공 응답도 stored selection과 함께 `selection_denied` 또는 `no_membership`이면 자동 초기화한다. `local_profile_pending`은 선택을 보존한다.
- 단일 membership의 header 없는 `/api/me`가 `selected`를 반환하면 해당 사업부를 먼저 `sessionStorage`와 request generation에 고정하고 그 응답은 shell state로 채택하지 않는다. 새 generation의 `/api/me`가 같은 사업부 header로 확인된 뒤에만 business 화면을 연다.
- membership 거부 뒤에는 사용자가 사업부를 명시적으로 다시 선택하거나 context를 초기화하기 전까지 unsafe 요청을 네트워크로 보내지 않는다. 오래된 청주 shell event가 header 없이 새 단일 오산 DB의 mutation으로 해석되는 경로를 차단한다.

### 상태 화면과 오산 route

- `/api/me` ready/pending 응답은 같은 `businessUnitAccess` envelope를 제공한다.
- Frontend는 `/api/me`의 access 상태를 먼저 확정한다. 일반 `no_membership`은 runtime mode나 업무 API를 호출하지 않고 안정된 대기 화면에 머물며, `selected` 또는 총괄 gate에서만 runtime mode를 조회한다.
- `no_membership`, `selection_required`, `selection_denied`, `local_profile_pending`, `selected`를 분리해 업무 DB 화면을 열기 전에 안내한다.
- 둘 이상 소속을 가진 지정 총괄만 selector를 사용한다. 일반 사용자의 다른 사업부 header 선택은 서버에서 거부된다.
- Change 002에서 desktop header, mobile header와 업무 진입 gate의 가시성 판정을 하나의 helper로 통일했다. 총괄 designation만 있거나 active `allowedBusinessUnits`가 한 곳 이하이면 selector, 단일 이동 button과 selector label을 렌더링하지 않는다.
- 오산 shell은 홈, 프로젝트 placeholder, 진행 관리 placeholder와 허용된 local/overall 관리만 제공한다. G2, Pending, hold/cancel과 Task 3~5 업무 route는 닫혀 있다.
- 오산 진행 안내는 G2·Pending·hold·cancel을 오산에서 사용하지 않는다고 명시하고, 승인된 7단계 진행 UI는 후속 진행 Task에서 제공한다고 안내한다.

### ReviewSafe membership control과 harness ownership

- Gate에 포함된 총괄 membership 화면과 정상 shell 화면은 같은 `runtimeMode.kind === 'ready' && mutationAllowed` 값을 사용한다.
- ReviewSafe, runtime 확인 중과 runtime 오류 상태에서는 checkbox와 save를 disabled하고 각각의 한국어 차단 이유를 표시한다. Component handler도 같은 값을 다시 검사하므로 DOM을 강제로 조작해 호출해도 membership PUT을 보내지 않는다. API의 423 방어는 그대로 유지한다.
- 3-DB harness는 임시 파일, DB와 Compose 생성 전에 실행별로 선택한 backend/frontend port가 모두 비어 있는지 확인한다.
- Backend는 Release DLL을 직접 실행한다. 기록 PID와 listener PID가 정확히 같고 PID file, repository cwd, DLL command와 port, process session이 일치해야 readiness를 받아들이고 cleanup에서 종료한다. 하나라도 달라지면 해당 process를 종료하지 않고 stable exit 65로 실패한다.
- Startup failure self-test는 정상 harness와 동일한 PostgreSQL 기동, 3 DB·6 bounded role bootstrap, migration과 directory fixture 뒤 backend launch 지점에서 test-owned process를 exit 98로 실패시킨다. 설치된 trap이 생성된 DB·role·Compose/process와 세 임시 파일을 정리하고 각 cleanup assertion을 실행한다.

## 3. 변경 파일

### 제품·migration

- `database/directory-migrations/0002_business_unit_access_administration.sql`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryStore.cs`
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Program.cs`
- `frontend/src/App.tsx`
- `frontend/src/api.ts`
- `frontend/src/identity.ts`
- `frontend/src/styles.css`

### 검증·Task 산출물

- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `frontend/tests/App.test.tsx`
- `frontend/tests/auth.test.tsx`
- `frontend/tests/BusinessUnitAccess.test.tsx`
- `frontend/tests/api.test.ts`
- `frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts`
- `frontend/e2e/mock-ui/business-unit-access.spec.ts`
- `frontend/playwright.business-unit-access.full-stack.config.ts`
- `scripts/e2e-business-unit-access-full-stack.sh`
- `tasks/osan-access-001.md`
- `tasks/osan-access-001-change-001.md`
- `tasks/osan-access-001-implementation-report.md`

Change 002 실제 변경은 다음 12개 파일이다.

- `frontend/src/App.tsx`
- `frontend/tests/BusinessUnitAccess.test.tsx`
- `frontend/e2e/mock-ui/business-unit-access.spec.ts`
- `tasks/osan-access-001-change-002.md`
- `tasks/osan-access-001.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-project-001-change-005.md`
- `tasks/osan-project-001.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-pilot-001.md`
- `tasks/osan-pilot-001-implementation-report.md`
- `docs/00-product-roadmap.md`

Change가 제안한 `frontend/e2e/mock/business-unit-access.spec.ts` 대신 실제 기존 mock harness의 동등 경로인 `frontend/e2e/mock-ui/business-unit-access.spec.ts`를 사용했다. 기존 API test 파일은 없었으므로 allowlist의 신규 `frontend/tests/api.test.ts`를 사용했다. Combined 검증은 shared single-DB harness를 바꾸지 않고 승인된 additive script, config와 spec 세 파일로 구현했다.

최종 manifest는 기존 파일 수정 15개와 신규 파일 12개, 합계 27개다. 신규 파일을 Git 상태 용어인 “untracked Task 파일”로 잘못 일반화하지 않는다.

## 4. 검증 결과

### Change 002 selector 가시성 검증

- `corepack pnpm exec vitest run tests/BusinessUnitAccess.test.tsx`: `1 file / 18 tests PASS`. 단일 청주·단일 오산의 local-profile-pending gate와 selected shell에서 selection UI가 없고, 기존 no-membership·selection-required·selection-denied·loading/error·mutation lock 회귀가 통과했다.
- `corepack pnpm exec vitest run`: `36 files / 297 tests PASS`.
- `corepack pnpm exec playwright test e2e/mock-ui/business-unit-access.spec.ts`: `3/3 PASS`. 복수 사업부 전환, 탭별 독립 선택과 단일 오산 총괄의 gate/shell desktop·390px selector 부재를 같은 run에서 확인했다.
- `bash scripts/test-business-unit-isolation.sh`: 실제 PostgreSQL 3-DB `2/2 PASS`. Backend의 active membership·active business unit filtering, inactive/revoked membership 거부와 총괄/일반 권한 경계를 재확인했다.
- `corepack pnpm run lint`: 오류 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1.
- `corepack pnpm run typecheck`: `PASS`.
- `corepack pnpm run build`: 399 modules, `PASS`; 기존 500 kB chunk warning만 남았다.
- 1440×900·390×844 단일 오산 gate와 shell screenshot 4개를 직접 확인했다. selector와 빈 label은 없고 390px horizontal overflow는 0이다.
- 변경 문서 local Markdown link 227개 누락 0, 추가 diff의 non-fixture email/private key/bearer/client secret 0, tracked/staged browser·build artifact 0과 `git diff --check` PASS를 확인했다.
- Change 002 self-review 결과 open P0/P1/P2는 `0/0/0`이다. 사용자 지시에 따라 GPT-6 review는 실행하지 않았다.

### 최종 집중 검증

- `bash scripts/test-business-unit-isolation.sh`
  - 최종 결과: 2 passed, 0 failed, 0 skipped, exit 0, 57초
  - Fresh/upgrade/re-run directory migration, three-DB boundary, runtime privilege, overall/local authorization, 신규·기존 Entra onboarding, ReviewSafe no-write, audit, revocation, local PATCH selected-DB isolation과 legacy null metadata fallback을 포함한다.
  - 실제 PostgreSQL의 test-owned overall actor 두 명을 사용했다. 자기 자신을 target으로 한 두 transaction과 서로를 target으로 한 두 transaction을 `pg_blocking_pids` lock barrier로 동시에 대기시킨 뒤 해제했고, statement/lock timeout과 deadlock/500 없이 직렬화됐다. 각 실제 변경의 event ID별 감사 1건, before→after 체인과 최종 membership이 일치했다.
- `dotnet test backend/tests/Emi.Qms.Api.Tests/Emi.Qms.Api.Tests.csproj --configuration Release --no-build --nologo --filter 'FullyQualifiedName~AuditMutationCoverageTests'`
  - 결과: 3 passed, 0 failed, 0 skipped, exit 0
  - Startup registry 전체 분류와 exact membership PUT의 deliberate local-audit exclusion을 검증했다.
- `corepack pnpm exec vitest run tests/api.test.ts tests/BusinessUnitAccess.test.tsx`
  - 최종 결과: 2 files, 31 passed, 0 failed, exit 0
  - 기존 context·stale·mutation/export 검증과 함께 selection-required ReviewSafe, local-profile-pending runtime loading, no-membership runtime error에서 disabled control, 명확한 사유와 강제 handler 호출 뒤 membership PUT 0건을 검증했다. 일반 미소속 첫 로그인은 `/api/me` 1회, runtime·업무 API 0건으로 안정되고, 마지막 소속 회수는 generation 1회 증가 뒤 stale 화면을 제거하며 새 미소속 shell에서 runtime을 재호출하지 않는다. 정상 runtime의 실제 UI 변경은 PUT 1건을 전송한다.
- `corepack pnpm exec vitest run tests/auth.test.tsx`
  - 결과: 1 file, 24 passed, 0 failed, exit 0, 8.47초
  - Mocked MSAL account로 App의 실제 계정 메뉴 `로그아웃` action을 실행했다. `sessionStorage` 선택 삭제, 진행 중 `/api/admin/users` read의 AbortSignal 취소와 `BusinessUnitRequestInvalidatedError`, generation 1 증가, active account null 설정 및 `logoutRedirect` 1회를 함께 검증했다.
- `corepack pnpm exec vitest run tests/App.test.tsx tests/api.test.ts`
  - 결과: 2 files, 106 passed, 0 failed, exit 0, 64.68초
  - Generation remount, 개발 사용자 변경, context API와 기존 청주 화면 회귀를 집중 확인했다.
- `corepack pnpm exec playwright test e2e/mock-ui/business-unit-access.spec.ts`
  - 결과: 2 passed, 0 failed, exit 0
  - 총괄 선택·membership 관리·mutation lock·오산 route 제한과 두 독립 browser context의 tab-scoped selection을 검증했다.
- `bash scripts/e2e-business-unit-access-full-stack.sh`
  - 최종 결과: 1 passed, 0 failed, exit 0, browser 19.9초·Playwright 24.4초
  - Test-owned directory/CHEONGJU/OSAN DB와 6개 bounded role, 실제 Release backend, 실제 Vite와 Chromium 한 run에서 기존 결합 시나리오를 검증했다.
  - Readiness와 cleanup에서 직접 실행한 Release DLL PID가 listener PID와 같고 PID file·cwd·command·session이 모두 일치했다. 종료 시 3개 DB와 6개 role count 0, backend/Vite/Compose cleanup assertion이 통과했다.
- Backend port 점유 negative: test-owned sentinel이 backend 선택 port를 점유한 상태에서 harness가 resource 생성 전 exit 64로 거부했다. 거부 뒤 sentinel HEAD가 200이어서 harness가 종료하지 않았음을 확인하고 sentinel owner가 정리했다.
- Frontend port 점유 negative: 같은 방식으로 frontend 선택 port를 점유했을 때 resource 생성 전 exit 64, sentinel 유지 200과 owner cleanup을 확인했다.
- `bash scripts/e2e-business-unit-access-full-stack.sh --self-test-backend-startup-failure`
  - 결과: post-bootstrap backend launch failure self-test 통과, exit 0
  - 정상 harness와 동일하게 Compose PostgreSQL, directory/CHEONGJU/OSAN DB, 6개 bounded role, migration과 directory fixture를 만든 뒤 backend launch 위치에서 test-owned process의 exit 98을 감지했다. 설치된 trap이 3개 DB·6개 role, Compose/process와 세 임시 파일을 정리했고 자체 count/assertion 및 exact Compose label·port 후속 조회에서 residual이 0이었다.
- `bash -n scripts/e2e-business-unit-access-full-stack.sh`
  - 결과: syntax 정상, exit 0

### 최종 회귀·build

- `bash scripts/e2e-backend-tests.sh`
  - 결과: 576 passed, 0 failed, 0 skipped, exit 0, test 기간 30분 36초
  - Test-owned database를 drop하고 Compose container/network를 제거한 종료 출력을 확인했다.
  - 이번 최종 P2에서 Backend C# 제품 코드는 바뀌지 않았고 변경점은 directory migration 함수와 그 전용 isolation test뿐이다. 30분 전체 suite는 반복하지 않고 기존 576/576 증거를 유지했으며, 변경 SQL과 C# test가 실제로 실행되는 3-DB isolation 2/2와 아래 Release test-project build로 영향 범위를 다시 검증했다.
- `dotnet build backend/tests/Emi.Qms.Api.Tests/Emi.Qms.Api.Tests.csproj --configuration Release --no-restore --nologo`
  - 결과: 0 warnings, 0 errors, exit 0, 21.51초
- `corepack pnpm exec vitest run`
  - 최종 결과: 35 files, 284 passed, 0 failed, exit 0, 78.72초
- `corepack pnpm exec eslint .`
  - 결과: 0 errors, 1 pre-existing `frontend/src/main.tsx` fast-refresh warning, exit 0
- `corepack pnpm run typecheck`
  - 결과: TypeScript no-emit typecheck 성공, exit 0
- `corepack pnpm run build`
  - 결과: TypeScript/Vite build 성공, 399 modules transformed, exit 0
  - 기존 large chunk warning은 유지되며 이 Change가 새 오류를 만들지 않았다.
- 마지막 미소속 gate 테스트와 auth test의 명시적 `AbortSignal` type 보정 뒤 Parent가 lint, typecheck와 build를 다시 실행해 각각 exit 0을 확인했다. 같은 최종 소스에서 auth 24/24, business-unit/API 31/31과 전체 Frontend 284/284를 확인했다.
- `git diff --check`
  - 결과: 오류 없음, exit 0

| 검증 구분 | 적용 | 최종 결과 | 근거 |
| --- | --- | --- | --- |
| Backend·authorization·migration 최소/영향 검증 | 적용 | PASS | 최종 3-DB 집중 2/2와 self/cross lock barrier, audit registry 3/3, 기존 전체 576/576, 최종 test-project build 경고·오류 0 |
| Frontend 최소/영향 검증 | 적용 | PASS | auth 24/24, App+API 106/106, business-unit/API 31/31, 최종 영향 144/144, 최종 전체 284/284, lint/typecheck/build exit 0 |
| 실제 synthetic combined full-stack | 적용 | PASS | 실제 3 DB·6 bounded role·backend·Vite·Chromium 1/1, exact listener ownership과 cleanup assertion |
| Harness negative | 적용 | PASS | occupied backend/frontend port는 자원 생성 전 각 exit 64와 sentinel 유지, post-bootstrap launch failure는 exit 98 감지·trap cleanup 뒤 self-test exit 0 |
| Persistent UAT·실제 provider | 미적용 | N/A | 이 Change의 승인·안전 경계에서 명시적으로 제외 |
| 사용자 직접 검수 | 적용 | `CHANGE_001_FINAL_BATCH_AND_CHANGE_002_PENDING` | Change 002 자동·시각 검증 뒤 사용자 확인 대기; 사용자 지시에 따라 새 GPT-6 검증 없음 |

시행착오 기록: delayed-body test double의 non-configurable property 때문에 집중 test 1건이 실패했고 descriptor를 고쳤다. 첫 full-stack setup은 한 transaction 안의 `DROP/CREATE DATABASE` 때문에 exit 1이었고 명령을 분리했다. Browser selector visibility, StrictMode와 checkbox 경쟁을 실제 UI 상태 대기로 보정했다. 전체 Frontend 병렬 실행의 test-results 경쟁과 이전 DOM assertion도 검증 순차화와 current-profile helper로 해소했다. 두 번째 verifier 보정에서 `dotnet run` wrapper PID와 실제 listener child PID/cwd가 달라 ownership 검증이 두 run을 exit 65로 안전 중단했고 불일치 process를 kill하지 않았다. Release DLL을 직접 실행해 PID와 listener를 하나로 만든 뒤 정상 ownership을 검증했다. 최종 lock-order test 첫 run은 동시 시나리오를 통과한 뒤 test-only overall actor 두 명을 활성 상태로 남겨 기존 count assertion이 1 expected/3 actual로 실패했으며, fixture를 비활성화한 최종 run은 2/2로 통과했다.

## 5. Privacy-safe 증거와 cleanup

- Desktop screenshot: `frontend/test-results/business-unit-access-overa-22c64-ell-and-manages-memberships-chromium/business-unit-access-desktop.png`
- 390px screenshot: `frontend/test-results/business-unit-access-overa-22c64-ell-and-manages-memberships-chromium/business-unit-access-mobile.png`
- 두 screenshot의 identity fixture는 `Synthetic Overall Admin`, `Synthetic New User`, `example.invalid` placeholder만 사용한다. Desktop/mobile에서 selector, membership card, role distinction과 responsive layout을 직접 확인했다.
- Backend 집중·전체 script가 만든 임시 PostgreSQL database, container와 network는 각 run의 trap으로 제거되고 drop assertion이 통과했다.
- Ownership 보정 중 안전 중단된 두 run, 진단 run과 최종 성공 run의 exact Compose project label을 각각 조회해 container/network/volume 잔여가 모두 0임을 확인했다.
- 최종 startup-failure project `emi-qms-e2e-osanfailure-20260907-0140`과 정상 project `emi-qms-e2e-osanfinal-20260907-0142`의 container/network/volume 출력은 각각 0건이었다. 두 run이 사용한 backend/frontend port 48231/48232 listener도 각각 0건이었다.
- 마지막 full-stack run이 공통 test-results를 정리한 뒤 mock browser 2/2를 다시 실행해 desktop/mobile screenshot을 재생성했다. Parent가 두 파일을 직접 열어 synthetic identity, 사업부 selector, membership card, 역할 구분과 390px 배치를 확인했다. Playwright가 시작한 Vite process는 종료됐고 screenshot은 ignored test-results에 보존되어 tracked/status-visible artifact는 0건이다.
- 기준선 diff와 신규 12개 파일을 검사한 결과 비허용 email domain, private-key literal과 hardcoded bearer token은 각각 0건이다. 문서와 fixture의 identity·email·UUID는 synthetic placeholder만 사용한다.
- 실제 Azure, Persistent UAT, 외부 provider와 production data는 사용하거나 변경하지 않았다.
- Change 002 screenshot은 `single-membership-gate-desktop.png`, `single-membership-gate-mobile.png`, `single-membership-shell-desktop.png`, `single-membership-shell-mobile.png` 네 synthetic artifact다. 이름과 화면에는 synthetic placeholder만 있고 실제 사용자·credential·token은 없다. 모두 ignored `frontend/test-results/` 아래에 있어 tracked/staged artifact가 되지 않는다.

## 6. 남은 검증 한계

실제 Microsoft 365 provider redirect는 호출하지 않았다. Provider/Azure mutation 금지 경계를 지키면서 mocked MSAL account와 `logoutRedirect`를 사용한 component/auth test가 App의 실제 계정 메뉴 logout action과 handler를 실행해 tab storage 삭제, outstanding read abort/generation 무효화, active account 해제 및 redirect 연결을 검증했다. Combined browser spec은 production reset primitive의 tab storage 삭제와 gate remount를 검증하며 provider redirect를 실행했다고 주장하지 않는다.

Persistent UAT와 실제 Azure/Entra는 실행하지 않았다. Change 002 selector 보정과 Change 003·004 최종 동작은 local exact-head synthetic 검수에서 사용자가 수락했으며, 이는 운영 적용 또는 실제 provider 검증 근거가 아니다.

## 7. Git·게시 확인

Sol 구현자는 승인 범위의 worktree file edit만 수행했고 Stage, commit, push, PR 생성, merge, branch 전환, branch mutation, worktree 제거와 canonical clone 변경을 수행하지 않았다. 제품 품질 GO 뒤 Parent는 이 Task의 문서 3개를 canonical clone에 동기화하고 Roadmap·상위 오산 Task/보고서·통합 검증 인계 상태만 갱신했다. Task 2 제품 코드는 canonical clone에 복사하지 않았고 기존 canonical WIP를 정리·stage·commit하지 않았다.

위 문단은 Change 001 당시 실행 이력이다. Change 002는 현재 cumulative Task 3 branch에서 사용자의 exact 지시에 따라 구현·검증·문서 동기화 후 exact allowlist만 local commit한다. Push·PR·merge·branch/worktree 정리·Persistent UAT·provider·운영 mutation은 수행하지 않는다.

## 8. Change 001 Fresh 독립 검증과 Change 002 단독 검증

Fresh GPT-6 High read-only verifier는 기준선 `670b2eafaa142f4be2febec206bd4f494707aea0` 대비 기록 상태 전환 전의 최종 제품·테스트·Task 문서 27개 스냅샷을 직접 읽고, 검증 전후 HEAD·branch·status·staged 상태와 각 파일 SHA-256이 동일함을 확인했다. 요청 모델은 `gpt-6-astra/high`, 관측 모델은 도구 미보고로 `NOT_REPORTED`이며 그 검증 스냅샷의 manifest digest는 `1debc180aadde8afb84853829ac7ca0b78d79cdffc0e5ef2b1de4b8acd347bb6`이다. 이후 Parent가 바꾼 것은 GO·사용자 검수 대기 상태와 screenshot retention을 반영하는 Task 문서뿐이며, 최종 기록 verifier가 별도 지문으로 동기화를 확인한다.

최종 판정은 `GO`, open P0/P1/P2는 `0/0/0`이다. 이 판정은 Task 2의 제품 품질 gate만 닫는다. 사용자는 2026-09-07 사용자 검수를 오산 개발 마지막 일괄 검수로 미루고 Task 2 local commit 및 Task 3 진행을 승인했다. Push, PR, merge, runtime·provider·Persistent UAT 승인은 포함하지 않는다. Screenshot retention P3는 mock browser 2/2 재실행, 파일 재생성, Parent 직접 시각 확인과 이 기록 보정으로 해소했다.

Change 002는 사용자 명시 지시에 따라 GPT-6 review 없이 요청된 GPT-5.6 Sol Extra high 한 명이 implementation direction, 구현, 테스트, 시각 확인, self-review, 기록과 local commit을 수행한다. 실행 수단은 실제 모델을 반환하지 않아 관측 모델은 `NOT_REPORTED`로 기록한다. Component·전체 Frontend·browser·실제 PostgreSQL 검증과 diff/privacy/secret/문서 검사를 기준으로 open P0/P1/P2 `0/0/0`을 확인했다.

## 9. 영향·제외 범위

- API: `/api/me` business-unit envelope, exact overall membership GET/PUT와 선택 business의 기존 user GET/PATCH만 영향을 받는다.
- DB/Migration: additive directory migration `0002`; 기존 business migration과 기존 `0001`은 수정하지 않았다. Membership·overall directory와 local role/department/project source를 합치지 않는다.
- Authorization: overall designation, local `system-administrator`, local `users.manage`를 서로 자동 변환하지 않는다. 오산의 Task 3~5/G2/Pending/hold/cancel route는 계속 닫혀 있다.
- UI/UX: 선택·대기·local profile pending·revocation gate, selector lock, restricted Osan shell과 local user edit 범위를 유지한다. Change 002는 actual active 목적지가 둘 이상인 총괄에게만 selector를 표시하고 단일 소속 사용자의 선택 UI를 완전히 숨긴다. 청주 lifecycle/notification/export control은 보존했다.
- Excel/PDF/첨부: 새 문서 format이나 export endpoint는 추가하지 않았다. 기존 GET blob/첨부/PDF/template 소비는 business generation 뒤 검증되며 POST export는 body 완료까지 switch lock을 유지한다.
- Worker/provider: 외부 worker·mail·Teams·Azure provider 등록과 호출을 변경하지 않았다.
- 실제 provider, Persistent UAT, production, Task 3~5, overall designation UI, cross-business 집계는 제외했다.

## 10. 운영 적용·rollback/forward-fix

이 Change는 live runtime에 적용하지 않았다. Migration `0002`는 column, constraint와 function을 추가하는 additive migration이며 이미 적용한 환경에서는 파일을 역수정하거나 data/table을 drop하지 않는다. 결함이 발견되면 다음 additive migration과 product forward-fix로 function/constraint/API를 보정한다. 배포 전에는 approved directory migrator/runtime role에 대한 function EXECUTE reconciliation, 세 DB identity, backup/rollback 운영 절차와 Persistent UAT read-only 검증을 별도 승인된 runtime Task에서 확인해야 한다.

## 11. 종료 산출물과 사용자 검수

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | Change 001~004 구현·자동/시각 검증과 사용자 검수 완료, PR CI 대기 | 이 문서 전체 |
| SOP | runtime fail-closed와 harness process ownership 절차 작성됨·운영 적용 전 검증 필요 | 이 문서 §2, §10 |
| User manual | 상태 화면, 실제 전환 가능한 사용자만 보는 selector와 소속/권한 관리 설명 작성됨 | 이 문서 §2 |
| Roadmap update | Task 2 Change 003·004 검수 완료와 PR CI 승인 상태를 상위 Task·rollout 인계에 동기화함 | `tasks/osan-access-001.md`, `tasks/osan-pilot-001-rollout-handoff.md` |
| User validation checklist | Change 003·004 exact-head 사용자 검수 완료 | 아래 checklist |

사용자 검수 checklist:

- [x] active 사업부가 두 곳 이상인 총괄에게 청주·오산 selector가 보이고, 단일 청주·단일 오산 사용자에게는 selector·단일 이동 button·빈 label이 전혀 보이지 않는다.
- [x] 총괄은 선택 전용 화면 없이 유효한 tab 선택 또는 청주 fallback으로 진입하고 우측 selector로 전환한다.
- [x] 오산 navigation에는 사용자 관리와 기타 관리자 item·빈 관리 group이 없다.
- [x] 청주 사용자 관리에서 청주·오산 승인 대기 사용자의 사업부·부서·자동 역할·부서장·활성을 지정할 수 있다.
- [x] membership 0과 local-profile-pending 안내가 유지된다.
- [x] 오산 admin URL은 청주 가능 총괄에게 청주 context로 복구되고 오산 단일 사용자는 홈으로 fail closed한다.

상태는 `Change 003·004 사용자 검수 완료 / Draft PR #121 최종 CI 대기`다. Exact-head 5198/5098 runtime과 synthetic 3-DB 자원은 검수 뒤 종료·정리됐다.

## 12. Finding과 개발 블로그 기록

- `OSAN-ACCESS-IMPLICIT-CONTEXT` P1: `RESOLVED`. 단일 membership을 header 없는 shell state로 채택하던 경로가 잘못된 DB 전환 위험을 만들었다. Selection 선고정, generation remount, denial 재확인 block과 unsafe request network-call 0 테스트로 해소했다.
- `OSAN-ACCESS-REVIEWSAFE-ENTRA` P1: `RESOLVED`. 다중 DB ReviewSafe가 pending directory/local provisioning write를 호출할 수 있었다. Directory registration을 생략하고 exact directory UUID/OID/provider/key/active local lookup만 허용해 해소했다.
- `OSAN-ACCESS-IDENTITY-LABEL` P2: `RESOLVED`. Null metadata legacy Entra row가 raw external subject를 표시할 수 있었다. 고정 한국어 fallback과 응답 비노출 검증으로 해소했다.
- `OSAN-ACCESS-REVIEWSAFE-GATE-CONTROLS` P2: `RESOLVED`. Gate와 정상 shell에 같은 runtime mutation state를 전달하고 세 fail-closed 상태의 disabled reason, 강제 handler 호출 뒤 PUT 0건과 정상 PUT 1건을 검증했다.
- `OSAN-ACCESS-MEMBERSHIP-SELF-DEADLOCK` P2: `RESOLVED`. Actor/target identity를 UUID 순서의 `FOR UPDATE`로 먼저 확보하고 overall designation을 같은 강도로 검증한다. 실제 PostgreSQL self/cross lock-barrier transaction과 감사 체인 검증이 2/2 집중 suite 안에서 통과했다.
- `OSAN-ACCESS-LOGOUT-WIRING-EVIDENCE` P2: `RESOLVED`. Mocked MSAL을 통한 App 실제 logout action이 selection 삭제, outstanding read abort, generation 증가, active account 해제와 `logoutRedirect`를 모두 수행함을 auth 24/24에서 검증했다.
- `OSAN-ACCESS-COMBINED-FULL-STACK` P2: `RESOLVED`. 양쪽 port의 resource 전 preflight, 직접 실행한 Release DLL의 exact listener/PID/cwd/command/session ownership, 정상 3-DB browser run을 검증했다. Occupied-port는 자원 0 상태에서 거부하고, post-bootstrap launch failure는 생성한 3 DB·6 role·Compose/process/temp file을 trap이 정리하는 실제 경로로 분리했다.
- `OSAN-ACCESS-PENDING-RUNTIME-REMOUNT-LOOP` P2: `RESOLVED`. `/api/me` 상태를 먼저 확정하고 일반 미소속 사용자는 runtime mode를 조회하지 않는다. 첫 로그인은 `/api/me` 1회·업무 API 0건으로 안정되며, 마지막 소속 회수는 generation을 한 번만 무효화하고 stale 업무 화면을 제거한 뒤 반복 요청 없이 대기 gate에 머무는 것을 component test로 검증했다.
- Provider-backed redirect logout 검증: `N/A`. 실제 provider 금지 경계이며 combined harness Finding의 미해결 상태로 분류하지 않는다.
- `OSAN-ACCESS-FILE-MANIFEST-WORDING` P3: `RESOLVED`. 보고서의 부정확한 “12개 untracked Task 파일”을 “신규 12개 파일”과 최종 manifest 27개로 바로잡았다.
- `OSAN-ACCESS-SCREENSHOT-RETENTION` P3: `RESOLVED`. 마지막 full-stack run 뒤 사라진 ignored screenshot 두 파일을 mock browser 2/2 재실행으로 다시 만들고 Parent가 desktop/mobile 이미지를 직접 확인했다.
- `OSAN-ACCESS-SELECTOR-VISIBILITY` P2: `RESOLVED`. 정상 shell은 목적지 두 곳 이상을 검사했지만 access gate는 한 곳 이상만 검사해 단일 소속 총괄에게도 이동 button을 보였다. 세 렌더 경로를 공통 actual-switchability 판정으로 통일하고 single Cheongju/Osan, multi, no-membership, loading/error, desktop/mobile 회귀를 확인했다.
- Open P0/P1/P2: Change 001 final fresh GPT-6 High 판정과 Change 002 단독 self-review 모두 `0/0/0`. 사용자 검수와 Git·운영 gate는 별도다.

### 해결한 업무 문제

총괄 membership과 사업부 local 권한을 분리하면서도 신규 Microsoft 365 사용자가 directory 대기에서 local 역할 승인까지 이동할 수 있게 했다. 열린 탭이 소속 변경 뒤 다른 DB로 조용히 넘어가거나 이전 조회를 표시하는 경로도 닫았다. Change 002에서는 실제 대체 목적지가 없는 사용자가 의미 없는 단일 선택 UI를 보지 않게 했다.

### 기술적 결정과 검토한 대안

Membership write는 runtime table 권한 확대 대신 fixed `search_path` SECURITY DEFINER function으로 한정했고 DB 안에서 actor overall status를 재검증한다. Shared single-DB harness 확대 대신 Task 전용 additive 3-DB harness를 사용해 기존 회귀 기반을 건드리지 않았다.

### 시행착오 및 폐기한 접근

Never-settling stale Promise, response header에서 controller 조기 해제, token await 뒤 generation capture, generic Entra label과 mock-only combined 검증을 폐기했다. Test harness의 DB DDL transaction, selector visibility와 StrictMode 경쟁은 §4에 기록한 실제 실패를 통해 보정했다.

### 사용자 검수 결과와 남은 항목

Change 001의 자동 검증과 당시 fresh GPT-6 read-only 제품 품질 검증은 완료했다. Change 002~004는 사용자 지정 단독 Sol 경로에서 자동·시각 검증과 self-review를 완료했고, 2026-09-08 사용자가 exact-head 검수 결과를 수락했다. Draft PR #121 갱신과 최종 remote CI 1회는 승인됐으며 exact `main` merge와 운영 runtime은 승인되지 않았다.

### Azure phase 1 승격 상태

`TASK-AZURE-DEPLOY-001 Change 031`은 로그인·사업부 해석, no-membership·local-profile-pending gate, 총괄 membership과 선택 사업부 local role 관리를 포함한다. 일반 사용자는 한 사업부 이하로 강제하고 지정 총괄만 다중 소속을 사용한다. Change 002~004 사용자 검수는 완료됐지만 Change 031의 역사적 전체 검증 수치를 새 head에 재사용하지 않는다. Azure mutation과 실제 계정 검증은 아직 수행하지 않았다.

## 13. Change 003 통합 사용자 승인 구현

### 사용자 문제와 최종 동작

기존 화면은 총괄의 `사업부 소속 관리`와 선택 사업부의 `사용자 관리`가 나뉘어 있었다. Membership을 먼저 저장한 뒤 local profile의 부서·역할을 따로 채워야 했기 때문에 첫 로그인 사용자가 `local_profile_pending`에 머물 수 있었다.

Change 003은 별도 왼쪽 메뉴와 화면을 제거하고 기존 `관리자 > 사용자 관리` 하나로 합쳤다. Directory에 등록된 첫 로그인 승인 대기 사용자도 목록에 나타난다. 총괄 관리자는 사업부별 부서, 역할 1개 이상, 부서장 여부와 활성 상태를 한 화면에서 한 번 저장한다. 기존 `/admin/business-unit-access` bookmark는 같은 사용자 관리 화면으로 이동하지만 별도 화면이나 메뉴를 되살리지 않는다.

### 저장·권한·감사 계약

- Additive Directory migration `0003_unified_user_access_administration`은 `access_version`과 durable operation 원장을 추가한다. 원장은 원래 요청한 사업부별 부서·역할·부서장·활성 payload도 보관해 페이지를 새로 연 뒤 같은 operation을 정확히 재시도할 수 있다. Identity contract 상수는 Directory `0001_business_unit_directory`, business `0086_business_unit_database_identity` 그대로다. Business migration은 추가하지 않았다.
- Grant/update는 같은 Directory UUID와 Entra subject를 검증하고 각 사업부 local profile·부서·역할·부서장·활성을 먼저 commit한 뒤 Directory membership을 공개한다. Local 단계가 실패하면 새 membership은 0이고 operation은 개인정보 없는 `RetryRequired` 상태로 남는다.
- 회수는 Directory membership을 먼저 차단하고 local profile을 비활성화한다. 부서·역할 snapshot은 보존하며 같은 operation 재시도로 forward-fix할 수 있다.
- Operation ID와 request fingerprint는 응답 유실 뒤 같은 요청을 중복 없이 완료 상태로 돌려준다. Expected version 불일치는 409로 거부한다.
- 일반 사용자는 active membership 한 곳 이하를 DB 함수에서 강제한다. 다중 소속은 active Directory overall designation이 있는 사용자만 허용한다. Overall designation과 사업부의 local `system-administrator`·`users.manage`는 자동 변환하지 않는다.
- Actor는 active 총괄이어야 하고 변경 대상이 걸친 각 사업부에서 local `users.manage`를 가져야 한다. Department와 role catalog는 해당 사업부 DB에서 각각 다시 검증한다. DB fallback은 없다.
- Directory audit와 각 사업부 field audit는 같은 operation UUID를 correlation ID로 사용한다. 기존 membership-only PUT과 DB function은 통합 저장을 요구하는 안정된 오류로 fail closed한다.

### 검수 전 최소 검증과 정책

사용자는 Change 003 사용자 검수 전에는 Backend 582, Frontend 297, Full-Stack 66, 전체 CI와 광범위 회귀를 다시 실행하지 않고, 검수 완료 뒤 원격 `main` 병합 준비의 최종 head에서 한 번만 실행하라고 명시했다. 따라서 이전 Change 031 전체 결과는 역사적 기록으로 보존하고 Change 003 증거로 재사용하지 않았다.

| 검증 | 결과 |
| --- | --- |
| Backend test project compile | PASS, 경고 0·오류 0 |
| Audit mutation endpoint 분류 | 4/4 PASS |
| Directory 0003 fresh/existing + 통합 3-DB 핵심 | 2/2 PASS |
| Frontend typecheck | PASS |
| BusinessUnitAccess/API component | 35/35 PASS |
| 통합 사용자 관리 Chromium smoke | 1/1 PASS |
| Frontend 전체 | 명령 필터 오지정으로 의도치 않게 1회 실행, 297/297 PASS; 정책 위반으로 기록하고 반복 금지 |
| 전체 Backend/Full-Stack/CI | 사용자 지정 정책으로 검수 뒤 최종 head 1회 대기 |

3-DB 집중 검증은 성공, local failure 뒤 membership 0과 RetryRequired·durable payload 복원·보정 뒤 동일 operation 재시도, 응답 유실 뒤 완료 operation 멱등 재시도, 같은 expected version의 동시 요청 1개 성공·1개 충돌, 일반 사용자 다중 소속 거절, 지정 총괄의 사업부별 서로 다른 local profile, 회수 뒤 membership 0·local snapshot 비활성 보존, Directory/사업부 audit correlation을 확인했다. Fresh Directory 0001→0003과 existing 0001/0002→0003 모두 exact ledger이며 identity contract `0001`을 유지한다.

### Finding, rollback과 게시 상태

구현 중 실제 DB 검증이 존재하지 않는 `departments.default_role_code` 조회와 field audit에 허용되지 않는 route key 형식을 찾았다. 현재 department identity policy를 사용하고 고정 endpoint name `UpdateIntegratedUserAccess`를 audit route key로 전달하도록 보정한 뒤 실패한 집중 Fact만 재실행했고, 최종 2/2 묶음도 통과했다.

Migration 0003은 down migration 없이 additive로 유지한다. 이전 image의 membership-only mutation은 `integrated_user_access_required`로 fail closed한다. 이전 image가 0003 ledger에서 ready라고 주장하지 않으며, 운영 적용 전 배포 Task에서 새 image 재배포/forward-fix를 복구 경로로 고정해야 한다. Change 003 자체는 운영 DB나 Azure에 적용하지 않았다.

기존 PR #121 remote head `11c1185ea9c550022e3f70d106e06a1c6bc517b1`는 그대로다. Change 003은 `fix/task-osan-access-001-integrated-user-approval`의 local commit과 exact-commit 격리 검수 runtime까지만 승인됐다. Push, CI, `main` merge, Azure mutation은 실행하지 않는다. Change 002 selector 사용자 검수 완료를 추정하지 않으며 Change 003 검수에서 함께 확인한다.

Change 003 사용자 검수 항목:

- [ ] 총괄에게 청주·오산 selector가 보이고 단일 청주·단일 오산 사용자에게 selector·빈 label이 보이지 않는다.
- [ ] 왼쪽 관리자 메뉴에는 `사용자 관리`만 있고 `사업부 소속 관리`가 없다.
- [ ] 첫 로그인 승인 대기 사용자가 사용자 관리 목록에 표시된다.
- [ ] 한 행에서 활성 상태, 사업부, 해당 사업부 부서와 부서장 여부를 지정하면 부서 기본 역할이 자동 표시되고 한 번 승인할 수 있다.
- [ ] 이미 승인된 사용자의 사업부별 부서·역할·부서장·활성을 같은 화면에서 수정할 수 있다.
- [ ] 일반 사용자는 두 사업부를 동시에 활성화할 수 없고 총괄 표시는 local 역할과 별도로 보인다.
- [ ] 오산 shell에는 G2, Pending, hold/cancel 등 phase 1 제외 기능이 나타나지 않는다.

## 14. Change 003 compact table 후속 구현

사용자가 첫 검수 화면에서 계정별 카드 높이를 줄이고 선택 칸 중심의 UI로 바꾸라고 지시했다. 통합 승인 backend와 별도 메뉴 제거는 유지하고 Frontend를 기존 청주 사용자 관리 표의 시각 언어로 다시 구현했다.

- 한 계정은 desktop과 narrow viewport 모두 기본 한 행이다. 왼쪽 두 줄 row header에 이름·이메일과 짧은 승인 상태를 압축 표시한다.
- 편집 헤더는 `활성 상태`, `사업부`, `부서`, `역할`, `부서장` 순서다. 승인/저장은 무헤더 끝 셀이다.
- 역할 select와 multiselect는 제거했다. 선택한 부서의 `defaultRoleCode`를 payload에 자동 반영하고 읽기 전용 한 줄로 표시한다.
- 부서 변경 시 다른 부서 default role만 교체한다. 관리 부서의 default role이기도 한 `system-administrator`는 별도 권한 보존 대상으로 명시하고, 그 밖의 기존 특수·추가 역할도 보존한다. Default role이 없으면 그 행의 저장을 막고 한 줄 오류를 연결한다.
- 일반 사용자의 활성 사업부 선택은 다른 사업부 draft를 자동 해제한다. 지정 총괄은 business-unit select로 각 profile을 오가며 여러 사업부를 유지할 수 있다.
- 정상 셀은 `4px 8px`, select/button은 `32px`, checkbox는 `16px`이며 browser 측정 정상 행은 `48px 이하`다. 390px에서는 동일 표를 수평 스크롤한다.

검수 전 테스트 정책을 지켜 TypeScript typecheck와 영향 component/browser만 수행했다. Typecheck는 PASS했다. Targeted component 첫 실행 20건 중 17건이 통과했고 새 test fixture/matcher 3건만 실패해 실패 filter로 보정 검증했다. 자동 default role·특수 역할 보존, default role 부재 fail-closed, ReviewSafe 차단이 모두 PASS다. 단일 mock Chromium은 최초 `48.48px` 높이 실패 뒤 CSS 1px 보정과 같은 1건 재실행으로 PASS했고 desktop/narrow screenshot을 직접 확인했다. Backend/Frontend/Full-Stack 전체와 CI는 추가 실행하지 않았다.

첫 exact-head 3-DB 실제 화면 점검에서는 통합 API department 응답이 Frontend 계약의 `departmentId`, `defaultRoleCode`를 누락해, 부서 선택 뒤 역할을 결정할 수 없고 저장이 fail closed하는 interface 결함을 발견했다. 공용 business `Department` model을 바꾸지 않고 통합 API 전용 projection을 추가해 두 값을 반환하도록 고쳤다. 동시에 `system-administrator`가 관리 부서의 default role이기도 하다는 실제 identity 계약을 test fixture에 반영해 부서 변경 시에도 해당 특수 권한을 보존한다. 보정 후 Frontend typecheck, 역할 자동 기입·보존 targeted component 1건, 기존 단일 mock Chromium 1건을 각 1회만 실행해 모두 PASS했다. 첫 follow-up runtime build에서는 내부 `BusinessState`에 남은 공용 부서 타입 한 곳 때문에 compile 오류 2건이 발생했고, harness는 생성한 DB·role·Compose 자원을 정리했다. 그 한 곳을 전용 projection으로 바꾼 Release build는 경고 0·오류 0으로 PASS했다. 수정 commit 뒤 격리 3-DB runtime에서 전체 사용자 검수 동선을 다시 확인한다.

Exact-head 검수 harness에는 초기 membership 0인 `Synthetic Cheongju Approval`과 `Synthetic Osan Approval`을 runtime-only fixture로 둔다. 각각 청주와 오산으로 승인된 뒤 같은 UUID의 `dev-sales`, `dev-quality`가 되어 해당 승인 계정으로 다시 접속할 수 있다. `dev-admin`은 양쪽 사업부를 선택할 수 있다. 외부 provider·worker·Azure·운영 DB는 사용하지 않는다.

## 15. Change 004 자동 진입과 청주 전용 관리 동선

Change 004는 `/api/me`가 양쪽 사업부 지정 총괄에게 `selection_required`를 반환할 때 전용 선택 화면을 열지 않는다. 현재 tab에 유효한 선택이 없으면 청주가 허용된 경우 청주를, 아니면 서버가 반환한 허용 목록의 첫 항목을 선택하고 기존 generation invalidation으로 새 header의 `/api/me`를 다시 확인한다. 유효한 `sessionStorage` 선택은 기존 header 계약으로 먼저 복구되며, 거부된 선택은 기존 fail-closed 초기화 뒤 같은 fallback을 사용한다. 단일 membership은 기존 implicit context 선고정·재조회 계약을 유지한다.

Access gate에서는 사업부 이동 button, 선택 전용 문구와 선택 초기화 action을 제거했다. membership 0의 소속 승인 대기와 local-profile-pending의 사용자 등록 대기, 총괄이 대기 상태에서 사용하는 Change 003 통합 승인 표는 유지한다. Header selector는 기존 compact 디자인과 두 곳 이상 총괄 조건을 유지하고 빈 option을 만들지 않는다.

오산 navigation은 홈·프로젝트·진행 관리만 구성하므로 관리자 item과 빈 관리 group이 생기지 않는다. 모든 admin workspace component는 오산 context에서 렌더하지 않는다. 오산에서 admin URL을 직접 열면 청주 membership이 있는 지정 총괄은 URL을 유지한 채 청주 context로 전환하며, 청주가 허용되지 않은 계정은 오산 홈으로 replace한다. 전환 전에 오산 header로 admin data API를 보내지 않는다. 청주 `관리자 > 사용자 관리`와 사업부·부서·자동 역할·부서장·활성 저장 계약은 변경하지 않았다.

### 검수 전 표적 검증

- Frontend typecheck: `PASS`.
- `BusinessUnitAccess.test.tsx`: 최종 `20/20 PASS`. 자동 청주 fallback, 단일 membership 재확인, membership 0/local-profile-pending, selector 비노출, 오산 navigation, dual-overall 청주 admin 전환, 오산 단일 admin URL fail-closed, 통합 승인·자동 역할·ReviewSafe·revocation을 확인했다.
- `business-unit-access.spec.ts`: 최초 실행에서 단일 membership desktop/mobile `1/1 PASS`. Dual-overall과 tab 두 사례는 반응형 DOM에 함께 존재하는 숨은 desktop/mobile select 중 `.first()`를 조작해 timeout되었다. Product 결함은 아니며 visible selector locator로 고친 뒤 실패한 두 사례만 재실행해 `2/2 PASS`했다.
- 생성된 synthetic desktop·390px screenshot을 직접 열어 청주 통합 사용자 관리의 compact 표, 우측 상단 selector와 narrow viewport의 수평 scroll을 확인했다. 실제 사용자 식별자·credential·token·provider는 사용하지 않았다.
- 전체 Backend 582, Frontend 297, Full-Stack 66, 전체 CI와 전체 lint는 사용자 지정 정책에 따라 실행하지 않았다. 이 변경은 Backend·DB·migration을 바꾸지 않는다.

### 사용자 검수·게시 상태

Change 003·004 exact-head `a5142b6573e70b75d2a4aea4dc43c5d42e229c9b`의 3-DB runtime에서 자동 진입, selector 표시 조건, 오산 관리자 menu 부재, 양쪽 전환, 청주 통합 사용자 관리와 실제 synthetic 승인, pending 상태와 direct admin URL 처리를 확인했다. 2026-09-08 검수 안내 직후 사용자의 최신 원문 `다음작업 승인.`에 따라 `userValidationStatus=COMPLETED`로 기록한다. Runtime은 owned session으로 정상 종료했고 5098/5198 listener와 해당 Compose container·network·volume 잔여 0건을 확인했다.

이 승인은 기존 Draft PR #121의 같은 head branch non-force 갱신과 최종 remote CI 1회를 포함한다. Exact `main` merge, Azure, Persistent UAT, 실제 provider와 운영 DB·image mutation은 포함하지 않는다. CI 통과 뒤 PR exact head/base/mergeable/latest main을 확인하고 `main` 병합 직전에서 멈춘다.

OpenAI 공식 모델 문서는 높은 reasoning effort가 더 긴 응답 시간을 만들 수 있고 실제 latency는 end-to-end workload로 측정해야 한다고 안내한다. GPT-6 출시가 GPT-5.6 Sol 자체 속도를 낮췄다는 공식 근거는 확인되지 않았다. 이번 세션에서 관찰된 긴 시간은 앞선 xhigh, 22~28분 Backend 전체, 12~15분 Full-Stack 전체의 반복, 3-DB/Docker 준비·정리, CI 재실행, UI 재작업, workspace permission 전환, 긴 Repository gate·문서와 parent handoff가 누적된 결과다. High가 xhigh보다 항상 빠르다고 보장하지 않으며, 검수 전 targeted test와 최종 게시 head의 전체 CI 1회 정책은 긴 suite 고정비와 불필요한 재실행을 줄인다.

### 첫 PR CI 실패와 표적 보정

검수 기록 commit `d34d372220f3cf082752690986de212e544c31ec`의 자동 CI run `34136633185`에서 Change Classification·Workflow Validation·Frontend는 PASS했고, Backend는 583/584 PASS, Full-Stack 일반 묶음은 63/64 PASS 뒤 격리 2건 skip, CI Gate는 FAIL했다. Backend는 통합 사용자 승인 뒤에도 onboarding 사용자가 pending이라고 본 legacy test 기대값이 원인이었고, Full-Stack은 이미 선택된 mock 사용자를 다시 선택해 자동 business resolution과 menu open을 경쟁시킨 helper가 원인이었다. 제품 권한·routing·데이터 계약 실패는 관찰되지 않았다.

Backend assertion을 현재 통합 승인 결과에 맞추고, mobile helper가 실제 값이 바뀔 때만 user selection event를 만들도록 보정했다. 전체 로컬 suite는 반복하지 않았다. 영향받은 Full-Stack spec 1건은 격리 3-DB에서 `1/1 PASS`, Backend test project targeted build는 경고 0·오류 0, 실패했던 Backend 3-DB fact는 `1/1 PASS`했으며 각각 owned runtime 자원을 정리했다. 이 보정의 push가 자동 생성하는 새 PR CI만 최종 전체 회귀로 추적하고 첫 run retry나 수동 workflow dispatch는 하지 않는다.

첫 보정 commit `b8c6a2dcf73341ed980e31a833c30f497c1490aa`의 자동 run `34140283892`에서 Backend 584/584, Frontend와 일반 Full-Stack 64/64는 PASS했다. Business-unit access 격리 spec은 Change 003의 승인 전 pending fixture에 단일 membership을 기대하고 Change 004가 제거한 선택 화면을 계속 조작해 FAIL했으며 Osan 격리는 skip됐다. Spec을 membership 0 대기, 총괄 Cheongju fallback, 통합 승인, tab별 selector 전환과 reset 뒤 자동 복구 계약으로 갱신한 뒤 해당 3-DB spec만 `1/1 PASS`했고 owned 자원을 정리했다. 후속 test-only push의 자동 CI를 최종 전체 검증으로 추적하며 수동 재실행은 만들지 않는다.

격리 spec 보정 commit `b095ad97d96dcf792501d217b8db6cf6d25a4d33`의 자동 run `34142984018`은 Backend 584/584와 Frontend가 PASS했지만 일반 Full-Stack의 project-registration mobile helper가 초기 business 자동 확정과 경쟁해 drawer selector를 찾지 못하면서 63/64 PASS로 끝났다. Business-unit·Osan 격리는 skip됐다. Full-Stack·mock browser의 같은 패턴을 전수 대조해 확인한 mobile drawer helper 6개를 network 안정화, visible selector, same-value no-op와 remount 대기로 통일했다. Mock-ui에는 같은 drawer user-switch helper가 없었다. 대표 검증은 project-registration 1/1, IQC 1/1, 나머지 helper 4/4, targeted ESLint PASS이며 생성 자원과 screenshot 변경을 모두 정리했다. 후속 test-only push가 자동 생성하는 한 run만 다음 최종 후보로 추적한다.

## 16. Change 007 운영 총괄 접근·사용자 식별 보정

최초 오산 phase-1 운영 배포 뒤 사용자 관리 목록의 기존 계정 이름·계정 ID가 비어 있고, 총괄 세 명에게 Cheongju membership만 있어 사업부 selector와 Osan 전체 권한이 생기지 않는 결함을 운영에서 확인했다. 통합 저장 요청에도 총괄 지정 필드가 없어 같은 행에서 부서 역할과 총괄 여부를 함께 저장할 수 없었다.

Change 007은 business profile의 이름·계정 ID를 Directory UUID뿐 아니라 provider·external subject까지 일치할 때만 목록에 병합한다. 통합 저장에는 총괄 checkbox가 추가됐고, 지정 시 두 business profile에 부서 기본 역할을 보존하면서 `system-administrator`를 추가한 뒤 두 membership과 designation을 한 번에 공개한다. 총괄 해제는 현재 선택 사업부 하나만 남기며, 복수 총괄은 허용하고 마지막 활성 총괄은 Directory transaction에서 보호한다. UI는 기존 compact table과 청주 관리자 동선을 유지한다.

Additive Directory migration `0004_overall_administrator_access`는 durable operation에 요청·이전 총괄 상태를 더하고 designation·membership publish를 같은 transaction과 correlation ID로 감사한다. 이전 7-argument begin function은 현재 designation을 보존하는 wrapper로 남겨 이전 image와 additive rollback 호환성을 유지한다. Identity contract는 Directory `0001`, business `0086` 그대로다.

기존 backfill은 Cheongju 이름·계정 ID를 Directory에 멱등 보정하고, private 승인 목록의 Cheongju System Administrator만 Osan `administration` profile과 `system-administrator`를 local-first로 만든 뒤 Cheongju·Osan membership과 overall designation을 공개한다. 일반 사용자는 Cheongju 한 곳만 유지하고, identity mismatch나 준비되지 않은 business DB는 fail closed한다.

집중 검증은 Backend compile, Directory `0004` existing `1/1`, fresh 3-DB `1/1`, Frontend targeted `37/37`, typecheck, mock Chromium `1/1`, 실제 HTTP+3-DB Full-Stack `1/1`, Bicep/Portal JSON/Azure static PASS다. Fresh Fact가 찾은 provider collision은 identity binding을 강화해 보정했고 복수 총괄 지정·한 명 해제·마지막 총괄 차단까지 확인했다. Targeted Vitest argument 전달 실수로 전체 `299/299 PASS`가 1회 실행됐으며 정책 위반으로 기록하고 반복하지 않았다. 최종 전체 회귀는 게시된 exact head의 원격 CI 한 번만 사용한다.

제품 commit `3d337c69bb225e324fc8a2339e18f68420d0c63d`, PR #122와 CI run `34186728030`이 통과했고 exact main `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`로 squash merge됐다. Azure release run `34188740914`에서 Directory `0004`, 기존 총괄 `3`명의 양 사업부 local-first backfill과 Backend·Frontend 전환이 성공했다. 운영 aggregate는 identity `23`, membership `26`, overall `3`, overall dual `3`, ordinary dual `0`이다. 새 로그인 세션에서 사용자 이름·계정 ID, 총괄 checkbox, 우측 상단 selector와 청주↔오산 전환을 확인했다. 일반 사용자 실제 계정 미노출과 첫 실제 Osan 프로젝트 저장은 사용자 검수로 남긴다.

## Change 008 — 접근 readiness·부서 역할·총괄 permission 정합성

운영 검수의 세 후속 결함을 같은 canonical Task의 BUGFIX로 재개했다. Root cause는 부서 기본 역할 provenance 부재, membership만 본 통합 승인 표시와 역할 수만 본 로그인·기존 집계의 서로 다른 판정, 후속 permission migration에서 System Administrator 누락이다.

Business migration `0088`은 역할 source와 System Administrator 전체·future permission invariant를 추가한다. 통합 저장은 explicit 역할만 보존하고 부서 기본·overall 역할을 재계산하며 부서 이동 때 head를 명시 재선택 없이는 해제한다. 로그인·사용자 목록·홈은 active local profile·유효 부서·default role을 함께 보는 공통 readiness를 사용한다. 기존 membership backfill은 roleless Cheongju membership을 audit와 함께 차단하고 ready/Osan 이동/overall 상태를 보존한다.

Backend compile, Frontend typecheck, migration existing/idempotency `1/1`, 격리 3-DB 통합 `1/1`, compact UI mock `3/3`이 통과했다. Local 전체 suite는 사용자 지시대로 실행하지 않았고 exact PR head의 원격 CI 한 번으로 검증한다. 구현 모델 requested `gpt-5.6-sol` xhigh, observed `NOT_REPORTED`다.

## 18. Change 008 게시·운영 보정 완료

제품 PR #123과 safe inspection PR #124~#126을 거쳐 현재 Directory designation authority를 보존하는 PR #127 exact head `7e95129ce0cbf5b389ead02b7d6558c67b268675`의 CI `34221079465`가 통과했다. 승인된 squash merge의 exact main은 `b41c932e2154a921cf8b0aab753fc6d0692b209f`다.

Release `34225420777`은 Business `0088`, roleless membership `1`건의 fail-closed 회수, Backend·Frontend와 public security를 완료했다. Post-deploy에서 사용자가 overall을 해제한 뒤 local managed System Administrator `1`건이 남았고, current Directory overall `3`을 권위 집합으로 유지한 DB-only repair `34228436474`에서 해당 역할만 제거했다. 최종 rollback-only inspect `34229045510`은 모든 변경 marker `0`, overall effective/configured/current `3/3/3`, 두 business permission gap `0/0`이다.

최종 app은 Backend `backend--0000040`, Frontend `frontend--0000029`, 각 immutable digest와 latest traffic `100%`다. 자동 공개 security/API와 DB aggregate는 PASS했다. 실제 계정의 부서 이동, Osan 승인 직후 상태, 총괄 selector·양 사업부 전체 읽기/쓰기는 마지막 사용자 smoke로 남긴다. Observed model은 `NOT_REPORTED`다.
