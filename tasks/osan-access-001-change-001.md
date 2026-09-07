# TASK-OSAN-ACCESS-001 Change 001 — 사업부 접근 관리 구현 방향서

## 1. 승인·Gate·기준선

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeStatus: `IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- roadmapSequenceMatch: true
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_2026-09-06_NEXT_TASK_START`
- runtimeMutationApproved: false
- gitCommitApproved: true
- gitCommitApprovalSource: `USER_EXPLICIT_APPROVAL_2026-09-07`
- userValidationStatus: `PENDING_BATCHED_FINAL`
- gitBranchMutationApproved: true
- gitBaselineApprovalSource: `USER_EXPLICIT_2026-09-06_APPROVED`
- gitPublicationApproved: false
- productionRuntimeMutationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- verificationOwnerObserved: `NOT_REPORTED`
- finalVerificationVerdict: `GO`
- finalVerificationManifestSha256: `1debc180aadde8afb84853829ac7ca0b78d79cdffc0e5ef2b1de4b8acd347bb6`
- sourceBranch: `feat/task-osan-isolation-001-db-boundaries`
- sourceHead: `670b2ea`
- taskBranch: `feat/task-osan-access-001-membership-switching`
- taskWorktree: `OSAN_ACCESS_TEMP_WORKTREE`
- 적용 지침: Root `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `scripts/AGENTS.md`, Product Roadmap, Task 종료 및 산출물 정책, Validation Matrix, Privacy-safe Evidence와 이 Task의 승인된 planning·review·선행 구현 보고

목적 identity는 “총괄 관리자가 사용자의 사업부 소속을 관리하고, 지정 총괄이 탭별 사업부를 안전하게 전환하되 각 사업부의 실제 업무 변경은 그 사업부 local 권한으로만 허용하는 기능”이다. 같은 목적의 canonical Task는 이 Task 하나이며 같은 목적의 local/remote branch와 PR은 확인되지 않았다.

Task 1의 제품 코드·테스트 43파일만 사용자 승인 범위로 local commit `670b2ea`에 보존했다. 기존 지침·문서 WIP는 stage하거나 commit하지 않았다. 이 reachable 기준선에서 Task 2 branch와 임시 worktree를 만들었으며 원격 게시나 `main` 반영은 수행하지 않았다.

## 2. 현재 동작과 해결할 문제

Task 1은 공통 directory의 identity·membership·overall administrator 지정, 요청별 `X-Qms-Business-Unit` 판별, 선택 사업부 DB의 local profile 조회와 오산 비활성 capability 차단까지 구현했다. 그러나 다음 사용자 기능이 아직 없다.

- 총괄이 사용자의 청주·오산 소속을 조회·부여·회수하는 API와 화면
- 미소속, 사업부 선택 필요, local profile 미등록 상태별 사용자 안내
- 각 브라우저 탭에 독립적인 사업부 선택과 모든 API 요청에 대한 선택 header 전파
- 전환 직전 진행 중인 저장 차단, 이전 조회 취소와 늦게 도착한 응답 무효화
- 오산용 간소화 shell과 route 직접 입력 차단

기존 사용자 관리 API는 선택된 업무 DB의 부서·역할을 관리하므로 사업부 관리자가 자기 사업부의 local 권한을 관리하는 데 재사용한다. 공통 directory의 소속 관리는 별도 총괄 API로 분리한다. 기존 `system-administrator` 역할을 총괄로 자동 승격하지 않는다.

## 3. 구현 방향

### 3.1 공통 directory와 서버 권한

1. Directory migration `0002`를 추가해 membership 변경 감사 action과 실제 변경 actor를 저장할 수 있게 한다. 기존 bootstrap 감사 행과 append-only trigger를 보존하고 기존 `0001`을 수정하지 않는다.
2. `BusinessUnitAccessAdministrationStore`를 추가한다.
   - 활성 directory identity와 소속, 총괄 여부를 한 번에 조회한다.
   - membership 부여·회수를 transaction으로 처리하고 변경 전후를 audit에 남긴다.
   - 알려진 사업부 code만 받고 마지막 소속 회수도 명시적으로 허용하되 그 사용자는 즉시 `no_membership` 상태가 된다.
   - 총괄 designation 자체를 일반 membership 화면에서 변경하지 않는다. 초기 지정은 승인된 bootstrap 절차를 유지한다.
3. 총괄 전용 endpoint를 별도 route group으로 둔다.
   - `GET /api/admin/business-unit-access/users`
   - `PUT /api/admin/business-unit-access/users/{userId}/memberships`
   - 인증 claim과 request context의 `IsOverallAdministrator`가 모두 true일 때만 허용한다.
   - `system-administrator`이거나 현재 사업부에서 `users.manage`를 가진 사실만으로 directory mutation을 허용하지 않는다.
4. 기존 `/api/admin/users`와 수정 API는 선택 사업부 DB에 그대로 연결한다. 사업부 관리자는 자기 local `users.manage` 권한으로만 사용한다. 총괄도 선택 사업부에 local `users.manage`가 없으면 기존 사용자 권한 변경 API에서 403을 받는다.
5. `/api/me` 정상·pending 응답에 동일한 business-unit access envelope를 제공한다. 최소 필드는 현재 상태, 선택 사업부, 허용 사업부, 총괄 여부다. Frontend가 상태별 화면을 만들기 위해 pending 응답만 별도 모양으로 추론하지 않게 한다.

### 3.2 Frontend 요청 context와 전환 안전성

1. 선택 사업부는 `sessionStorage`에 저장해 탭별로 독립시킨다. `localStorage`에 저장하지 않는다.
2. 공통 API layer가 선택 사업부를 모든 인증 요청의 `X-Qms-Business-Unit` header에 넣는다. 호출 화면마다 header를 직접 조립하지 않는다.
3. API layer에 business-unit generation과 `AbortController`를 둔다.
   - 전환 시 이전 generation의 조회를 abort한다.
   - abort가 실제 전송을 취소하지 못했거나 늦은 응답이 도착해도 이전 generation의 결과는 오류로 변환해 화면 상태에 반영하지 않는다.
   - 로그아웃과 권한 재확인 실패 때 선택값과 요청 context를 초기화한다.
4. 변경 요청은 공통 API layer에서 in-flight 개수를 추적한다. 하나라도 진행 중이면 전환 control을 disabled로 유지하고 이유를 표시한다. 이미 시작한 mutation을 강제 abort해 성공 여부가 불명확해지는 동작은 하지 않는다.
5. 전환이 완료되면 현재 view를 사업부 홈으로 이동하고 shell 이하를 새 key로 remount하여 프로젝트 선택, 검색, pagination과 화면 cache를 초기화한 뒤 `/api/me`와 runtime 상태를 다시 읽는다.

### 3.3 상태별 화면과 메뉴

1. `no_membership`: 업무 DB 요청 없이 “사업부 소속 승인 대기”와 로그아웃만 표시한다.
2. `selection_required`: 총괄에게 허용된 사업부 선택 화면만 표시한다.
3. `local_profile_pending`: 선택한 사업부의 local 역할 승인이 필요하다는 안내와 다른 허용 사업부 선택/로그아웃을 제공한다.
4. 정상 선택 상태:
   - 총괄이며 소속이 둘 이상일 때 header에 사업부 selector를 표시한다.
   - 총괄 전용 “사업부 소속 관리” 화면에서 membership을 관리한다.
   - 기존 “사용자 관리”는 현재 사업부의 local 부서·역할 관리 화면으로 설명을 명확히 한다.
5. 오산 shell은 `진행 관리`와 오산에 필요한 프로젝트 영역만 노출한다. G2, Pending, 중단·보류와 아직 Task 3~5에서 열지 않은 route는 URL 직접 입력도 현재 사업부 capability 검사로 안전한 홈/안내 화면에 보낸다. Backend의 Task 1 capability middleware가 최종 보안 경계다.

## 4. Exact 변경 allowlist

### 제품 코드·migration

- `database/directory-migrations/0002_business_unit_access_administration.sql` 신규
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryMigrationCatalog.cs` 또는 schema contract 상수 소비 지점
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitDirectoryStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessAdministrationStore.cs` 신규
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessEndpointExtensions.cs` 신규
- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/Program.cs`
- `frontend/src/identity.ts`
- `frontend/src/api.ts`
- `frontend/src/App.tsx`
- `frontend/src/styles.css`

### 검증 코드

- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitAccessTests.cs` 신규, fixture 분리가 더 명확할 때만 생성
- `frontend/tests/App.test.tsx`
- `frontend/tests/auth.test.tsx` (기존 Entra logout handler 연결 검증만)
- `frontend/tests/BusinessUnitAccess.test.tsx` 신규
- `frontend/tests/api.test.ts` 신규 또는 기존 API test 파일이 확인되면 그 파일
- `frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts` 신규
- `frontend/e2e/mock/business-unit-access.spec.ts` 신규, 실제 e2e 폴더 구조에 맞춰 한 곳만 사용
- `frontend/playwright.business-unit-access.full-stack.config.ts` 신규
- `scripts/e2e-business-unit-access-full-stack.sh` 신규

### Task 산출물

- `tasks/osan-access-001.md`
- `tasks/osan-access-001-change-001.md`
- `tasks/osan-access-001-implementation-report.md` 신규
- `tasks/osan-pilot-001-implementation-report.md`
- `tasks/osan-project-001.md`
- `docs/00-product-roadmap.md`

구현자가 실제 테스트 구조에서 기존 동등 파일을 발견하면 같은 책임의 파일로 대체할 수 있으나, 위 경계 밖 제품 기능·기존 청주 업무 의미·Task 3~5 기능은 변경하지 않는다. 새 변경 경로가 필요하면 parent에 이유와 영향을 먼저 반환한다.

구현 전 코드 대조에서 기존 오산 allowlist가 `/api/business-units`만 허용해 승인된 총괄 endpoint `/api/admin/business-unit-access/users`도 차단하는 충돌을 확인했다. Parent는 2026-09-06 승인된 Task 목적 안에서 `BusinessUnitCapabilityMiddleware.cs`를 allowlist에 추가했다. 변경은 이 총괄 관리 route prefix만 허용하며 다른 오산 업무 API를 열지 않고 endpoint의 별도 overall-administrator 검사를 최종 권한 경계로 유지한다.

같은 사전 대조에서 startup의 mutation registry가 승인된 membership PUT route를 미분류 mutation으로 거부하는 충돌을 확인했다. Parent는 `AuditMutationRegistry.cs`를 allowlist에 추가했다. 이 exact route는 directory transaction의 append-only before/after audit이 authoritative record이므로 known mutation이면서 local business audit에서 제외되는 route로 분류한다. 다른 mutation route나 기존 감사 포함/제외 의미는 바꾸지 않는다.

Registry 분류 변경의 기존 canonical contract test가 `AuditMutationCoverageTests.cs`에 있으므로 같은 책임의 검증 파일을 allowlist에 추가했다. 새 route 한 건의 known/excluded 분류만 검증하며 기존 route 집합의 의미는 바꾸지 않는다.

Parent 직접 review에서 신규 Microsoft 365 사용자는 directory row가 없어 총괄 목록에 나타나지 않고, 소속을 받은 뒤에도 선택 사업부의 local profile을 만들 경로가 없어 승인 대기에서 벗어날 수 없는 onboarding 단절을 확인했다. 승인된 “신규·미소속·미승인 사용자 대기 → 총괄 소속 지정 → 사업부 관리자 local 역할 지정” 흐름을 완성하기 위해 `EntraClaimsTransformation.cs`와 `DbIdentityStore.cs`를 allowlist에 추가했다. 인증된 Entra identity의 pending directory 등록은 membership·총괄 권한을 전혀 부여하지 않는 제한 함수로 수행하고, membership 확인 뒤 선택 사업부에 directory UUID와 일치하는 역할 없는 local profile만 생성한다. 기존 email 기반 bootstrap 관리자 자동 승격은 이 다중 DB 경로에 적용하지 않는다. 총괄 목록에는 잘못된 사용자 선택을 막기 위해 directory의 최소 display name과 email을 표시하며 local 역할·부서·프로젝트 정보는 저장하지 않는다.

Fresh GPT-6 High 검증은 2026-09-06에 다음 NO-GO Finding을 확인했고 Parent가 같은 Change 001 범위의 보정을 Sol implementer에게 반환했다.

- 단일 소속의 implicit `/api/me` 선택을 tab request context에 먼저 고정하고, 저장 선택이 없는 상태에서도 정확한 membership denial이 shell generation을 무효화해야 한다.
- ReviewSafe 다중 DB Entra 인증은 directory와 local DB에 write하지 않고 정확한 기존 identity/profile read만 허용해야 한다.
- metadata가 없는 legacy Entra identity의 외부 subject를 화면 이름으로 노출하지 않고 privacy-safe 한국어 fallback을 사용해야 한다.
- 분리 검증으로 남겨 둔 combined three-DB browser 계약을 이번 Change에서 닫아야 한다.

Combined 검증을 제품 계측 없이 추가하기 위해 Parent는 additive test infrastructure인 `scripts/e2e-business-unit-access-full-stack.sh`와 `frontend/playwright.business-unit-access.full-stack.config.ts`를 allowlist에 추가했다. 기존 shared harness는 변경하지 않는다. 이 script는 test-owned synthetic directory·청주·오산 DB와 서로 다른 migration/runtime role만 생성하고 trap에서 자신이 만든 자원만 제거한다. 실제 backend와 frontend를 기동하는 전용 Playwright config와 Task 전용 spec을 사용한다.

두 번째 Fresh GPT-6 High 검증은 2026-09-07에 다음 P2 Finding을 확인했고 Parent가 같은 Change 001 범위의 보정을 반환했다.

- Gate가 조기 반환될 때 포함되는 총괄 membership 관리에도 정상 shell과 같은 authoritative runtime mutation 상태를 전달한다. ReviewSafe, runtime loading과 runtime 오류에서는 control과 handler를 모두 차단하고 membership mutation network call이 0건이어야 한다.
- 3-DB harness는 임시 파일·DB·Compose 생성 전에 선택한 backend/frontend port가 모두 비어 있는지 확인한다. Backend readiness와 cleanup은 직접 실행한 Release DLL의 exact PID, PID file, listener PID, cwd, command, session이 모두 일치할 때만 성공하며 불일치 process를 종료하지 않는다.
- 실제 provider redirect logout은 combined harness Finding과 분리한 N/A 항목으로 유지한다.

Final Fresh GPT-6 High 검증은 2026-09-07에 membership 함수의 self/cross lock ordering, 실제 Entra logout handler 연결 증거, startup failure injection의 실제 자원 cleanup 경로를 P2로 반환했다. 기존 Entra logout handler를 실제 App action에서 검증하기 위해 Parent는 `frontend/tests/auth.test.tsx`를 test-only allowlist에 추가했다. 제품 계측이나 provider 호출은 추가하지 않으며 승인 출처는 `USER_EXPLICIT_2026-09-06_NEXT_TASK_START`, dependency resolution은 `PARENT_APPROVED_SAME_TASK_TEST_DEPENDENCY_2026-09-07`이다.

이 보정의 taskType은 `P2_REMEDIATION`이며 기준선과 branch는 바뀌지 않았다. Membership 함수는 actor와 target directory identity를 UUID 순서의 `FOR UPDATE`로 먼저 잠그고 overall designation도 `FOR UPDATE`로 검증한다. 자기 대상 동시 변경과 두 총괄의 교차 대상 변경은 같은 순서로 직렬화하며 기존 active actor/target, known business unit, membership-only, no-op 감사 0건, 실제 변경당 감사 정확히 1건 계약을 유지한다.

다음 Fresh GPT-6 High 검증은 일반 미소속 사용자의 실제 runtime 403이 generation remount를 반복할 수 있는 P2 `OSAN-ACCESS-PENDING-RUNTIME-REMOUNT-LOOP`를 확인했다. Parent는 같은 승인 범위에서 `/api/me` 상태를 먼저 확정하고 `selected` 또는 총괄 gate에서만 runtime mode를 조회하도록 보정했다. 저장 선택 없는 최초 denial의 1회 무효화와 unsafe 요청 차단은 유지한다. 일반 미소속 첫 로그인과 마지막 소속 회수 테스트는 안정된 gate, 유한한 `/api/me`·runtime 요청, 업무 API 0건과 stale 화면 제거를 검증한다.

## 5. 보존할 불변조건

- 공통 directory는 membership과 총괄 지정만 보유하고 local 역할·부서·프로젝트 권한 원본을 모으지 않는다.
- 요청에서 받은 사업부 header만 믿지 않고 directory membership, 총괄 전환 자격, 실제 DB identity와 local permission을 서버에서 다시 검사한다.
- 총괄 지정과 `system-administrator`, 사업부 membership과 기존 department를 서로 자동 변환하지 않는다.
- 총괄은 사업부를 전환할 수 있지만 선택 사업부의 local 업무 권한이 없으면 조회·변경할 수 없다.
- 일반 사용자와 사업부 관리자에게 다른 사업부 선택을 허용하지 않는다.
- 소속 회수와 로그아웃 뒤 기존 탭의 cache나 늦은 응답으로 업무 데이터가 다시 나타나지 않는다.
- 오산에서 G2·Pending·중단·보류·외부 알림·Task 3~5 업무 mutation을 새로 열지 않는다.
- 청주 기존 메뉴, 권한, 프로젝트와 개발용 테스트 사용자 전환은 다중 DB 기능이 비활성일 때 그대로 유지한다.
- 실제 Azure, Persistent UAT, provider, push, PR, merge와 `main` 반영은 이 Change에 포함하지 않는다.

## 6. 완료 조건과 테스트 지시

### Backend

- Directory migration 0001→0002와 fresh 0001+0002, 재실행 no-op, append-only 감사 보호를 검증한다.
- 미소속, 단일 소속 일반 사용자, 단일 소속 사업부 관리자, 복수 소속 비총괄, 복수 소속 총괄을 포함한 allow/deny matrix를 검증한다.
- 총괄이 membership을 부여·회수했을 때 즉시 다음 요청부터 적용되고 before/after/actor audit이 정확히 한 건 남는지 검증한다.
- System Administrator만 가진 사용자, `users.manage`만 가진 사용자와 URL/header 변조 요청이 총괄 API에서 403인지 검증한다.
- 총괄이 local `users.manage` 없는 사업부에서 기존 사용자 권한 mutation을 수행하면 403인지 검증한다.
- 두 업무 DB에 같은 user/project UUID를 두어도 local 사용자 목록·수정과 업무 조회가 선택 DB 밖으로 나가지 않는지 검증한다.
- membership 회수, local profile 없음, 잘못된 사업부, directory contract mismatch에서 fallback이 0인지 검증한다.

### Frontend unit/component

- 탭 A 청주, 탭 B 오산의 `sessionStorage` 선택이 서로 공유되지 않는지 검증한다.
- 모든 인증 API 요청에 현재 사업부 header가 붙고 전환 뒤 새 header만 사용되는지 검증한다.
- 늦은 이전 조회 응답이 전환 뒤 state를 덮어쓰지 않고 abort/stale 오류가 사용자 오류로 노출되지 않는지 검증한다.
- mutation 진행 중 selector가 잠기고 완료 후에만 전환되는지 검증한다.
- `no_membership`, `selection_required`, `local_profile_pending`, 정상 선택 화면을 각각 검증한다.
- 오산에서 G2/Pending/비활성 route가 숨겨지고 URL 직접 입력도 안전하게 전환되는지, 청주에서는 기존 navigation이 유지되는지 검증한다.
- 총괄 소속 관리와 현재 사업부 local 사용자 관리가 UI에서 혼동되지 않는지 검증한다.

### 통합·회귀

- Backend release build, 관련 집중 테스트와 전체 Backend 회귀를 실행한다.
- Frontend typecheck, lint, unit, build와 관련 mock E2E를 실행한다.
- 승인된 synthetic 격리 DB 환경에서 두 탭, 느린 응답, mutation 중 전환, 로그아웃, membership 회수 full-stack 시나리오를 실행한다.
- desktop과 390px에서 selector, pending 안내, 소속 관리 화면을 privacy-safe screenshot으로 확인한다.
- 테스트 자원이 생성되면 테스트 소유 자원만 정상 cleanup하고 사용자 legacy Docker 자원은 건드리지 않는다.

### Fresh verifier 보정 결과

- 단일 membership `/api/me` selected 응답은 tab selection을 먼저 저장하고 generation을 바꾼 뒤, 같은 사업부 header가 붙은 새 `/api/me`가 확인되어야 shell이 열린다.
- 기존 사업부가 거부된 뒤에는 명시 재선택 전 unsafe API를 네트워크로 보내지 않는다. Stored selection이 없는 context denial도 generation을 바꾸고 열린 shell을 remount한다.
- Multi-DB ReviewSafe Entra는 directory registration과 local profile create/update를 모두 생략하고 directory UUID, OID, provider, development key와 active 상태가 정확히 맞는 기존 local profile만 읽는다.
- Legacy null-metadata Entra card는 외부 subject 대신 `Microsoft 365 사용자 (정보 확인 필요)`를 표시한다.
- Additive Task 전용 harness가 실제 directory/CHEONGJU/OSAN DB, 6개 bounded role, Release backend, Vite와 Chromium 한 run을 구성하고 자신이 만든 자원을 모두 정리한다. 실제 provider redirect logout은 금지 경계 때문에 실행하지 않고 production reset primitive만 browser 안에서 검증한다.
- Gate와 정상 shell의 총괄 membership 화면은 같은 runtime mutation state를 사용한다. ReviewSafe, runtime loading/error/unavailable에서는 명확한 한국어 이유와 함께 checkbox/save를 disabled하고 handler가 호출되어도 PUT을 보내지 않는다.
- Harness는 양쪽 선택 port를 resource 생성 전에 검사하고, 직접 실행한 Release DLL의 PID와 실제 listener PID가 같은지 cwd·command·session·PID file과 함께 readiness/cleanup에서 재검증한다. 소유권이 불일치하면 사용자 process를 종료하지 않고 stable failure로 끝난다.
- Occupied backend/frontend port는 임시 파일·DB·role·Compose 생성 전에 exit 64로 끝난다. Backend startup failure 주입은 정상 harness와 같은 bootstrap/migration 및 fixture 단계 뒤 실제 backend launch 위치에서 exit 98을 발생시키고, 설치된 trap이 자신이 만든 3개 DB, 6개 role, Compose/process와 임시 파일을 모두 정리한 뒤 self-test를 완료한다.
- Microsoft provider를 호출하지 않는 component/auth test가 App의 실제 계정 메뉴 `로그아웃` action을 실행해 tab selection 삭제, outstanding read abort와 generation 증가, active account 해제 및 mocked `logoutRedirect` 호출을 함께 검증한다.
- 일반 미소속 사용자의 `/api/me`를 먼저 처리하고 runtime mode를 조회하지 않는다. 선택된 shell에서 마지막 소속이 회수되면 generation을 한 번만 바꾸고 stale 업무 화면을 제거한 뒤, 새 미소속 shell에서는 runtime·업무 API를 다시 호출하지 않는다.

## 7. 제외 범위와 다음 Task 인계

오산 8개 입력 프로젝트 생성, 7단계 진행, 자동 완료, 전체 현황판은 각각 Task 3~5 범위다. 총괄 designation UI, 범용 권한 모델 재설계, cross-business 집계, 펜딩·중단 기능과 외부 알림도 추가하지 않는다.

완료 뒤 Task 3에는 인증된 오산 actor가 선택 사업부와 local `Project.Create`를 모두 충족해야 생성할 수 있다는 계약을 넘긴다. 프로젝트 생성 권한은 이후 진행 단계 변경 권한을 자동 부여하지 않는다.

## 8. Parent review 기준

Parent GPT-6 High는 구현 후 실제 diff를 읽어 directory/local 권한 분리, stale request 처리, 오산 route 폐쇄와 allowlist 준수를 직접 확인한다. 테스트 결과는 종료 코드와 실패/통과 수를 대조한다. 그 뒤 fresh GPT-6 High read-only verifier가 고정 기준선에서 승인 계약, 실제 diff, 테스트 증거와 P0/P1/P2 Finding gate를 독립 검증한다.
