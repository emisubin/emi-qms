# TASK-OSAN-ACCESS-001 Change 003 — 사용자 관리 통합 승인과 사업부별 접근 관리

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `USER_VALIDATED_AWAITING_PR_CI`
- userValidationStatus: `COMPLETED`
- userValidationSource: `USER_EXPLICIT_2026-09-08_NEXT_TASK_APPROVED`
- canonicalTask: `TASK-OSAN-ACCESS-001`
- canonicalChange: `TASK-OSAN-ACCESS-001 Change 003`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TASK-AZURE-DEPLOY-001_CHANGE_031_MAIN_MERGE_APPROVAL`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- implementationApprovalSource: `USER_EXPLICIT_2026-09-07_INTEGRATED_USER_APPROVAL`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH_ONLY`
- implementationOwnerObserved: `NOT_REPORTED`
- gpt6ReviewRequested: false
- gpt6ReviewProhibitedByUser: true
- implementationBranch: `fix/task-osan-access-001-integrated-user-approval`
- implementationBaseline: `11c1185ea9c550022e3f70d106e06a1c6bc517b1`
- originMainBaseline: `574cea66f602eb65eb1d110801d331151731b0a6`
- gitPublicationApproved: true
- remoteCiApproved: true
- mainMergeApproved: false
- persistentRuntimeMutationApproved: false
- providerOperationApproved: false

사용자는 기존 Azure 배포 순서를 멈추고 첫 로그인 승인과 사업부별 사용자 수정을 기존 `관리자 > 사용자 관리` 한 화면에 통합하라고 명시 승인했다. 2026-09-08 검수 안내 직후 사용자의 최신 원문 `다음작업 승인.`은 Change 003·004 결과 검수 수락, 기존 Draft PR #121의 non-force 갱신과 최종 remote CI 1회 실행 승인이다. Exact `main` 병합, Azure와 운영 DB mutation은 포함하지 않는다.

## 2. Purpose identity와 검색 결과

- proposedTaskId: `TASK-OSAN-ACCESS-001`
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- reuseExistingTask: true
- experimentStandingInstructionApplies: false
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`
- 업무 목표: 첫 로그인 승인과 승인 후 사업부별 접근 수정을 사용자 관리 한 화면·한 저장으로 처리한다.
- Root Finding: 현재 Directory membership 저장과 사업부 DB의 로컬 프로필·부서·역할 저장이 서로 다른 메뉴와 API에 있고 membership이 먼저 공개되어 local profile pending 상태와 일반 사용자의 다중 소속을 만들 수 있다.
- 변경·검증 경계: Directory additive migration, 통합 coordinator/API, 사용자 관리 화면·navigation, 관련 3-DB·component·browser·deployment 정적 검증과 Task 기록.
- 보존할 불변조건: 동일 Directory UUID, Directory/청주/오산 DB 분리, 사업부별 부서·역할 분리, 총괄 designation과 local `users.manage` 분리, 청주 기존 기능·데이터, identity contract 상수 `0001`/`0086`, fail-closed와 append-only audit.
- 예상 산출물: Directory 0003, durable operation/version/idempotency, local-first grant·membership-first revoke, 통합 사용자 관리 UI/API, 독립 membership PUT 차단, 자동 검증·격리 검수 환경·local commit.
- 검색 범위: 관련 Task·planning·review·change·implementation report, Roadmap·Decision Log, local/remote branch·worktree, PR #121을 확인했다.

## 3. Worktree 격리 기록

- purpose: 접근 불가능한 기존 `/private/tmp/emi-osan-project-001`과 canonical clone의 사용자 WIP를 건드리지 않고 Change 003을 exact base에서 구현·검증한다.
- owner: `TASK-OSAN-ACCESS-001 Change 003 / GPT-5.6 Sol xhigh`
- base: `11c1185ea9c550022e3f70d106e06a1c6bc517b1`
- boundedWorktree: `/Users/parksubin/.codex/visualizations/2026/09/05/01a07195-0215-7572-8126-f3d7e385169a/emi-osan-change-003`
- branch: `fix/task-osan-access-001-integrated-user-approval`
- expectedEnd: 자동 검증과 local commit 뒤 이 exact commit의 사용자 검수 runtime을 유지하는 동안 보존한다.
- cleanupBoundary: 사용자 검수와 후속 게시 방향이 확정되고 process 미사용·clean·commit reachable을 확인한 뒤 별도 승인 범위에서만 `git worktree remove`로 정리한다. 기존 inaccessible worktree/branch와 canonical WIP는 수정하지 않는다.

## 4. Implementation Direction Brief

### 구현 방식과 순서

1. Directory migration `0003`에 사용자 접근 version과 내구 operation 원장을 추가한다. Operation ID+request fingerprint와 원래 사업부별 요청 payload의 멱등성, expected version 충돌, 대상별 단일 진행 operation, 일반 사용자 1개 active membership 이하를 DB에서 강제한다.
2. 기존 독립 membership function은 signature를 보존하되 direct mutation을 안정된 오류로 거부하게 해 이전 image가 시작·조회 가능하고 우회 저장은 fail-closed가 되게 한다.
3. 통합 coordinator는 Directory에서 operation을 시작하고 회수 membership을 먼저 차단한다. 사업부별 runtime connection과 동일 Directory UUID로 로컬 프로필·부서·역할·부서장·활성을 각 DB에 commit한다. 모든 로컬 단계가 성공한 뒤에만 grant membership을 공개하고 operation을 완료한다.
4. 공통 operation/correlation UUID를 Directory audit와 각 사업부의 기존 business audit session context에 전달한다. 실패는 개인정보 없는 safe code와 `RetryRequired` 상태로 남기고 같은 operation 재시도로 이어간다.
5. 서버는 actor가 active 총괄이면서 변경하는 각 사업부에서 local `users.manage`를 가진 경우만 통합 mutation을 허용한다. 일반 local 관리자는 기존 선택 사업부의 사용자 수정 API를 그대로 사용한다. 총괄 designation 자체는 이 화면에서 변경하지 않는다.
6. Frontend는 별도 route/menu/page를 제거하고 `관리자 > 사용자 관리`에서 Directory 사용자와 사업부별 로컬 상태를 함께 표시한다. 첫 승인과 기존 사용자 변경 모두 사업부, 그 사업부의 부서, 1개 이상 역할, 부서장, 활성 상태를 한 저장 요청으로 보낸다. RetryRequired·version conflict·저장 중 잠금을 명확히 표시한다.
7. 오산 제한 shell과 업무 진입 전 총괄 gate도 사용자 관리 하나만 제공한다. Change 002의 selector 조건은 유지한다.

### Exact allowlist

- `database/directory-migrations/0003_unified_user_access_administration.sql`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/src/Emi.Qms.Api/DatabaseRuntimePrivilegeManager.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessAdministrationStore.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitAccessEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/DatabaseMigrationTests.cs`
- `frontend/src/App.tsx`
- `frontend/src/api.ts`
- `frontend/src/identity.ts`
- `frontend/src/styles.css`
- `frontend/tests/BusinessUnitAccess.test.tsx`
- `frontend/tests/AdminUsers.test.tsx`
- `frontend/e2e/mock-ui/business-unit-access.spec.ts`
- `frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts`
- `scripts/e2e-business-unit-access-full-stack.sh`
- `scripts/test-unified-user-access.sh`
- `tasks/osan-access-001-change-003.md`
- `tasks/osan-access-001.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-pilot-001-rollout-handoff.md`
- `docs/00-product-roadmap.md`

Allowlist의 기존 test 파일이 실제 checkout에 없거나 더 좁은 기존 test가 같은 검증을 소유하면 새 파일을 만들지 않고 해당 검증을 가장 가까운 기존 파일에 둔다. 새로운 필수 product/test 파일이 필요하면 구현 전에 이 목록과 근거를 갱신한다.

### 제외 범위와 반환 조건

총괄 designation 변경 UI, 새 business role/department model, business migration, 진행 mutation·자동 완료·dashboard, Pending/hold/cancel/deleted/Excel, 오산 외부 provider/worker, 실제 사용자·사업 데이터 생성, Azure/운영 DB, PR #121·remote CI·`main`은 제외한다. Business migration은 현재 `qms_users`, `user_roles`, `department_heads`, audit trigger 계약으로 충분할 때 만들지 않는다. Destructive migration·rollback, 실제 provider 또는 승인 scope 확대가 필요하면 해당 의존 작업만 중단해 보고한다.

## 5. 사용자 관찰 완료 조건

- 왼쪽 메뉴와 URL에 별도 `사업부 소속 관리`가 없고 청주·오산 모두 `사용자 관리`만 있다.
- 첫 로그인 승인 대기 사용자가 사용자 관리 목록에 보이고, 관리자는 사업부별 부서·역할 1개 이상·부서장·활성을 지정해 한 번 저장한다.
- 성공 응답 뒤에만 해당 membership이 공개되고 사용자는 다음 요청부터 선택한 사업부 업무에 들어간다.
- local DB 실패 시 새 membership은 0이며 화면은 같은 operation의 재시도 필요 상태를 보인다.
- 응답 유실 뒤 같은 operation/body 재시도는 중복 없이 완료 상태를 반환한다.
- stale version 동시 저장은 409로 거부되고 새 snapshot 재로딩을 안내한다.
- 일반 사용자는 두 사업부 active membership 요청이 거부된다. 지정 총괄은 각 사업부의 서로 다른 부서·역할을 가진 다중 소속을 저장할 수 있다.
- 무권한 actor와 다른 DB의 department/role ID 사용은 거부된다.
- 회수 시 membership이 먼저 사라지고 로컬 snapshot은 비활성 상태로 보존되며 실패 시 안전한 재시도가 가능하다.

## 6. 검증 계획

- Directory fresh 0001→0002→0003, existing 0001/0002→0003, exact ledger·identity·role privilege를 검증한다.
- 통합 성공, local DB failure 후 membership 0, 응답 유실 멱등 재시도, concurrent version conflict, 일반 사용자 다중 소속 거절, 지정 총괄 다중 소속과 사업부별 역할, 무권한/cross-DB 차단, 공통 audit correlation, 회수/stale local 비활성, old image direct PUT fail-closed를 실제 PostgreSQL 3-DB test에서 검증한다.
- Backend 집중/전체, Frontend 집중/전체·lint·typecheck·build, mock Chromium desktop/390px, isolated 3-DB full-stack, deployment script/static checks, `git diff --check`, link/privacy/secret/generated artifact 검사를 실행한다.
- exact commit에서 총괄, 청주 단일, 오산 단일, 첫 로그인 승인 대기 synthetic fixture를 제공하고 사용자가 브라우저에서 직접 확인하기 전 상태를 `IMPLEMENTED_AWAITING_USER_VALIDATION`으로 기록한다.

### 사용자 지정 검수 전 테스트 정책 변경

사용자는 2026-09-07 검수 전에는 Backend 582 전체, Frontend 297 전체, Full-Stack 66, 전체 CI와 광범위 회귀를 실행하지 않고, 사용자 검수 완료 뒤 원격 `main` 병합 준비 단계의 최종 head에서 딱 한 번 실행하라고 지시했다. 이미 실행된 Change 031 결과는 역사적 증거로 보존하며 Change 003의 통과 주장으로 바꾸거나 반복하지 않는다. Change 003 검수 환경 신뢰에 필요한 compile/typecheck, Directory 0003 fresh/existing, 핵심 통합 승인 시나리오, 관련 UI/API component와 단일 Chromium smoke만 실행한다. 실패 시 실패 항목과 직접 영향 범위만 재실행한다.

### 검수 전 최소 검증 결과

- Backend test project compile: `PASS`, 경고 0·오류 0.
- Audit mutation endpoint 분류 집중 검증: `4/4 PASS`.
- Directory 0003와 통합 3-DB 집중 검증: `2/2 PASS`. Fresh Directory 0001→0003, existing Directory 0001/0002→0003, 기존 Cheongju prefix와 부분 Osan migration 승격, identity contract `0001`, 성공·local failure membership 0·RetryRequired·응답 유실 멱등 재시도·version 충돌·일반 다중 소속 거절·지정 총괄 다중 소속·회수와 Directory/사업부 audit correlation을 확인했다.
- Frontend typecheck: `PASS`.
- Business-unit/API component 집중 검증: `35/35 PASS`.
- 통합 사용자 관리 단일 Chromium mock smoke: `1/1 PASS`.
- Frontend 전체: targeted 명령의 인자 구분 오지정으로 의도치 않게 1회 실행되어 `297/297 PASS`. 사용자 테스트 정책 위반으로 명시 기록하며 이 결과를 계획된 최종 회귀로 간주하거나 반복하지 않는다.
- Broad Backend/Full-Stack/CI: `NOT_RUN_BY_USER_TEST_POLICY`; 사용자 검수 뒤 최종 게시 head에서 1회 실행한다.
- 테스트 중 발견한 `departments.default_role_code` 오조회와 audit route key 형식 오류를 제품 코드에서 보정했고 각 실패 항목만 재실행해 통과했다. 이후 최종 집중 묶음도 통과했다.

## 7. Phase-1 운영 불변조건

일반 사용자가 두 사업부 소속일 때 전환할 수 없는 기존 문제는 사용자가 이번 Change로 통합 저장에서 구조적으로 막도록 승인했다. Phase 1에서는 일반 사용자를 한 사업부 이하로 유지하며 다중 소속은 Directory에서 명시된 active 총괄에게만 허용한다. 기존 잘못된 ordinary 다중 소속은 새 통합 mutation에서 제거 대상으로만 다루고 새로운 incomplete access를 만들지 않는다.

## 8. 게시·운영·검수 상태

- Change 003·004 exact local head `a5142b6573e70b75d2a4aea4dc43c5d42e229c9b`의 격리 3-DB 검수에서 통합 승인, compact 표, 자동 진입, selector 조건, 오산 관리자 navigation 제거와 direct admin URL 처리를 확인했고 사용자가 결과를 수락했다.
- 검수 runtime은 owned session으로 정상 종료했고 5098/5198 listener와 해당 Compose container·network·volume 잔여 0건을 확인했다.
- 기존 Draft PR #121은 최신 `origin/main` 정합성을 확인한 뒤 같은 head branch에 non-force push하고, 최종 head의 required remote CI를 딱 한 번 실행한다.
- Exact `main` merge, Azure와 운영 DB mutation은 승인되지 않았다.
- Migration 0003은 additive이며 down migration을 제공하지 않는다. 이전 image의 독립 membership write는 `integrated_user_access_required`로 fail closed한다. 이전 image가 새 ledger를 수용한다고 주장하지 않으며 운영 적용 전 Change 031에서 새 image 재배포/forward-fix 경로를 고정한다.
- Change 002 selector 조건도 Change 003·004 exact-head 검수 화면에서 함께 수락됐다.

## 9. 사용자 UI 피드백 반영 — compact table

사용자는 첫 검수 화면에서 계정별 카드 높이가 과도하다고 판단하고 기존 청주 관리자 사용자 관리와 같은 조밀한 선택형 표로 변경하라고 명시했다. 후속 구현은 한 계정을 한 표 행으로 만들고 편집 헤더를 정확히 `활성 상태`, `사업부`, `부서`, `역할`, `부서장` 순서로 고정했다. 이름·이메일과 짧은 승인 상태는 왼쪽 row header의 두 줄 안에 두고 승인/저장은 무헤더 끝 셀로 최소화했다. 정상 행의 설명·도움말·카드·큰 배지는 제거했으며 RetryRequired와 오류만 해당 행 다음 한 줄에 표시한다.

역할은 별도 선택 UI를 없앴다. 부서를 선택하면 기존 `departments.defaultRoleCode` 계약으로 그 부서의 기본 역할을 자동 적용하고 읽기 전용 한 줄로 보여준다. 부서를 바꿀 때 사업부 catalog의 다른 부서 기본 역할만 교체하되 `system-administrator`는 관리 부서의 기본 역할이면서 별도 권한이므로 명시적으로 보존하고, 그 밖의 기존 특수·추가 역할도 보존한다. 기본 역할이 없는 부서는 역할을 추정하지 않고 저장을 비활성화한다. 일반 사용자가 활성 체크한 사업부는 다른 사업부 draft를 자동 비활성화하며, 지정 총괄은 사업부 select로 각 사업부 profile을 전환해 독립적으로 유지한다.

Desktop 표의 정상 셀 padding은 `4px 8px`, select/button 높이는 `32px`, checkbox는 `16px`다. 390px 화면에서도 카드를 만들지 않고 같은 compact table을 유지하며 수평 스크롤로 모든 열을 제공한다. Mock Chromium에서 정상 행 높이 `48px 이하`, narrow viewport의 `scrollWidth > clientWidth`, 정확한 헤더·role 자동 기입·해당 행만 loading 잠금을 확인했다.

검수 전 추가 검증은 사용자 정책에 따라 Frontend typecheck, `BusinessUnitAccess.test.tsx`와 단일 mock browser 시나리오로 제한했다. Typecheck는 최종 PASS다. Component 첫 실행은 20건 중 17 PASS/3 FAIL이었고 모두 새 test fixture·matcher 결함이었다. 실패 3건만 좁혀 재실행해 자동 역할과 특수 역할 보존, ReviewSafe mutation 차단이 PASS했고, 행별 오류 matcher 보정 뒤 fail-closed 1건도 PASS했다. Browser smoke는 첫 실행에서 행 높이 `48.48px`로 기준을 `0.48px` 초과해 셀 padding을 1px 줄였고, 같은 1건만 재실행해 PASS했다. 전체 suite는 추가 실행하지 않았다.

첫 exact-head 3-DB 브라우저 점검에서 통합 catalog가 공용 `Department` 응답을 그대로 사용해 `departmentId`와 `defaultRoleCode`를 내리지 않는 interface 결함을 발견했다. 화면은 부서를 선택해도 기본 역할을 알 수 없어 의도대로 fail closed했다. Backend 통합 응답 전용 department projection에 두 값을 추가하고, 관리 부서의 기본 역할인 `system-administrator` 보존 규칙을 component fixture로 고정했다. 이 보정 뒤 Frontend typecheck, 해당 역할 자동 기입·보존 component test 1건, 단일 mock browser smoke 1건만 영향 범위로 재실행해 모두 PASS했다. 첫 follow-up runtime build는 내부 `BusinessState`의 부서 타입 한 곳을 전용 projection으로 바꾸지 않아 compile 오류 2건으로 중단됐고 생성한 격리 자원 cleanup은 PASS했다. 해당 한 곳을 보정한 Release build는 경고·오류 없이 PASS했다. 최종 exact-head 3-DB 브라우저 흐름은 수정 commit에서 다시 확인한다.

격리 review harness는 서로 다른 pending synthetic 계정 두 개를 제공한다. `Synthetic Cheongju Approval`은 청주, `Synthetic Osan Approval`은 오산으로 승인되면 동일 Directory UUID를 유지한 채 runtime-only Dev identity `dev-sales`, `dev-quality`로 전환되어 승인한 계정 자체로 각 사업부 shell을 확인할 수 있다. 이 전환은 격리된 tmpfs 3-DB fixture에만 적용되며 tracked product identity나 운영 데이터 계약을 바꾸지 않는다. `dev-admin`은 계속 청주·오산 양쪽을 선택하는 총괄 fixture다.
