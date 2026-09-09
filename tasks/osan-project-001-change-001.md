# TASK-OSAN-PROJECT-001 Change 001 — 오산 프로젝트 등록 구현 방향서

## 1. 승인·Gate·기준선

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeStatus: `IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-PROJECT-001`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- roadmapOverrideSource: `USER_EXPLICIT_2026-09-07_BATCH_VALIDATION_AND_TASK3_APPROVAL`
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_APPROVAL_2026-09-07`
- localCommitApproved: true
- localCommitPolicySource: `USER_STANDING_INSTRUCTION_2026-09-07`
- userValidationStatus: `PENDING_FINAL_BATCH`
- persistentUatMutationApproved: false
- providerMutationApproved: false
- gitPublicationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- taskBranch: `feat/task-osan-project-001-project-registration`
- baselineSha: `2e29938f754f3d95444df2b341a921cfd1fca43f`
- sourceTask: `TASK-OSAN-PROJECT-001`
- sourcePlanning: `tasks/osan-pilot-001-planning.md`
- sourceReview: `tasks/osan-pilot-001-review.md`

사용자는 Task 2의 직접 검수를 오산 개발 마지막에 한 번에 진행하도록 순서를 바꾸고 Task 3 구현을 승인했다. Task 2의 제품 구현과 필수 자동 검증, parent review와 fresh GPT-6 High 독립 검증은 open P0/P1/P2 `0/0/0`, `GO`로 끝났고 local commit `2e29938`에 보존됐다. 이번 순서 변경은 사용자 검수 완료를 뜻하지 않으며 push, PR, merge, Persistent UAT와 실제 provider 권한을 추가하지 않는다.

## 2. Purpose identity

- 업무 목표: 오산 사용자가 정확히 8개 화면 입력으로 프로젝트를 만들면 수량만큼의 진행 대상과 각 대상의 고정 7단계가 한 transaction에서 생성되고, 오산 목록과 상세에서 즉시 확인되게 한다.
- Root Finding: 현행 `/api/projects`와 프로젝트 화면은 청주 Item, 영업담당, 포장방식, FAT, 생산계획, 구매와 18단계 workflow를 전제로 하며 오산에서는 capability middleware가 전부 차단한다. 그대로 열면 승인되지 않은 청주 계약과 후속 업무가 생성된다.
- 변경·검증 경계: 오산 전용 create/list/detail API와 UI, 공통 `projects` 식별 row의 profile 분리, 오산 진행 대상·7단계 snapshot·생성 idempotency·인앱 event 저장, additive migration과 관련 테스트만 포함한다.
- 보존 불변조건: 청주 create/list/detail, 활성 Title unique, Item·담당자·포장·FAT, 18단계 workflow와 기존 provider 동작을 바꾸지 않는다. 오산에서는 Pending, hold/cancel, 설계·구매·품질·물류, 외부 알림과 실제 진행 mutation을 열지 않는다.
- 예상 산출물: 오산 프로젝트 등록·목록·상세 제품 변경, migration `0087`, 자동 검증과 privacy-safe desktop/mobile 증빙, Implementation report, SOP·사용자 안내·마지막 일괄 검수 checklist, local commit.

## 3. 현재 동작과 구현 방향

현재 오산 shell은 프로젝트 메뉴를 준비 중 화면으로 표시하고 Backend는 오산에서 `/api/projects` 전체를 `business_unit_capability_disabled`로 거부한다. 기존 생성은 등록 Item, 활성 Sales 사용자, 포장방식과 Title 정규화 unique를 요구하고 생성 뒤 production/procurement snapshot과 청주 workflow를 연다. 이는 오산의 8개 입력과 진행 단계 전용 계약에 맞지 않는다.

다음 순서로 구현한다.

1. 새 additive migration `0087`에서 기존 `projects` row를 `Cheongju`와 `Osan` profile로 구분하고 오산의 PO No, W/O No, 제품명과 수량 필드를 추가한다. 기존 row는 `Cheongju`로 유지한다. 기존 활성 Title unique index는 같은 이름과 청주 의미를 보존하는 profile 조건으로 재작성하고, 오산은 trim한 `project_code` 원문에 대한 별도 partial DB unique를 둔다. 대소문자와 내부 공백은 추가 정규화하지 않는다.
2. 오산 진행 대상, 대상별 7단계 snapshot, 생성 operation과 인앱 project event를 별도 테이블로 저장한다. 대상과 단계의 이름에는 제조 용어를 사용자 계약으로 노출하지 않는다. 생성자는 기존 `user_project_access`에 연결한다.
3. 전용 `/api/osan/projects` POST/GET과 `/api/osan/projects/{projectId}` GET을 추가한다. Endpoint와 store는 trusted business-unit context가 정확히 `OSAN`인지 다시 확인하고 기존 `Project.Create`/`projects.read` 및 project access scope를 적용한다. 다른 사업부에서 전용 route를 호출하면 거부한다.
4. POST는 사용자 입력 8개와 화면에 보이지 않는 operation id를 검증한다. Project Title·코드·거래처·납기일·제품명·수량은 필수, PO/W/O는 선택이다. 문자열은 앞뒤 공백만 제거하고 PO/W/O의 앞자리 0과 기호, 코드의 대소문자와 내부 공백을 보존한다. 수량은 1~500 정수로 제한한다.
5. 한 transaction에서 project row, creator access, 수량 N개의 대상, 대상마다 정확한 7단계, `ProjectCreated` 인앱 event와 operation replay 결과를 저장한다. 같은 operation id와 같은 payload는 기존 성공을 반환하고, 다른 payload는 409로 거부한다. 같은 오산 코드의 경쟁 요청은 DB unique로 하나만 성공한다. 생성 시 기존 `WorkflowStore`, production/procurement snapshot, work item, Pending, notification delivery와 외부 provider를 호출하지 않는다.
6. Frontend는 오산일 때 전용 프로젝트 목록·등록·상세를 사용한다. 등록 화면에는 Title, 프로젝트 코드, 거래처, PO No, W/O No, 납기일, 제품명, 수량만 순서대로 표시한다. 저장 중 중복 제출을 막고 field 오류, loading/empty/error/forbidden 상태를 제공한다. 생성 성공 뒤 전용 상세로 이동해 8개 정보, N개 대상과 각 7단계 `시작 전` 상태를 보여준다. 청주 화면과 API 호출 경로는 유지한다.

## 4. Exact allowlist

예상 기존 파일:

- `docs/00-product-roadmap.md`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `frontend/src/App.tsx`
- `frontend/src/api.ts`
- `frontend/src/projects.ts`
- `frontend/src/styles.css`
- `frontend/tests/App.test.tsx`
- `frontend/tests/BusinessUnitAccess.test.tsx`

예상 신규 파일:

- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectContracts.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectInputNormalizer.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectStore.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectEndpointExtensions.cs`
- `backend/tests/Emi.Qms.Api.Tests/OsanProjectRegistrationApiTests.cs`
- `database/migrations/0087_osan_project_registration.sql`
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `frontend/e2e/full-stack/osan-project-registration.full-stack.spec.ts`
- `frontend/playwright.osan-project-registration.mock.config.ts`
- `frontend/playwright.osan-project-registration.full-stack.config.ts`
- `scripts/e2e-osan-project-registration-full-stack.sh`
- `tasks/osan-project-001-change-001.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`

Task 3의 stacked branch가 승인된 기획·선행 Task·후속 의존성을 자체적으로 추적할 수 있도록 canonical 문서화 작업에서 이미 작성·승인된 다음 문서를 같은 내용으로 함께 materialize한다. 제품 범위를 넓히거나 원문을 재작성하지 않는다.

- `tasks/osan-dashboard-001.md`
- `tasks/osan-isolation-001-change-001.md`
- `tasks/osan-isolation-001-change-002.md`
- `tasks/osan-isolation-001-change-003.md`
- `tasks/osan-isolation-001-implementation-report.md`
- `tasks/osan-isolation-001.md`
- `tasks/osan-pilot-001-implementation-report.md`
- `tasks/osan-pilot-001-interview.md`
- `tasks/osan-pilot-001-planning.md`
- `tasks/osan-pilot-001-review.md`
- `tasks/osan-pilot-001-rollout-handoff.md`
- `tasks/osan-pilot-001.md`
- `tasks/osan-progress-001.md`
- `tasks/osan-validation-001.md`

Sol은 실제 구조상 불필요한 예상 파일을 수정하지 않는다. Allowlist 안에서 새 test helper나 전용 frontend module이 필요하면 목적이 같은 `OsanProject*` 이름으로만 추가하고 완료 보고에 정확한 경로를 남긴다. Allowlist 밖 기존 파일 변경이 필요하면 먼저 Parent에 반환한다.

`BusinessSchemaVersion`과 사업부 예시 설정의 `ExpectedSchemaVersion` 값 `0086_business_unit_database_identity`는 최신 migration 번호가 아니라 0086에서 도입한 영구 DB identity binding 계약이다. 이를 0087로 바꾸면 기존 0086 DB가 0087 적용 전에 preflight에서 거부되므로 두 공통 파일은 변경하지 않는다. 최신 migration 적용 여부는 `schema_migrations` 원장과 migration test에서 0087까지 별도로 확인한다.

감사 relation coverage에서 `osan_project_targets`, `osan_project_target_steps`는 이후 Task 4에서 상태가 바뀌는 업무 원본이므로 0087에서 기존 전역 변경 감사 함수에 trigger로 연결한다. 과거 migration 0083은 수정하지 않는다. `osan_project_events`는 자체 이벤트 원장, `osan_project_create_operations`는 idempotency 원장으로 명시 분류해 재귀·중복 감사를 피한다. Audit infrastructure test는 0083 이후 추가된 tracked relation과 두 예외 범주를 정확히 검증한다.

## 5. Interface·data·authorization·workflow 불변조건

- 화면의 사용자 입력은 8개뿐이다. Operation id는 client가 생성하는 기술 필드이며 사용자 입력으로 표시하지 않는다.
- 오산 전용 응답은 PO/W/O의 null과 원문, 제품명, 수량, 대상 순번·표시명, 7단계 순서·이름과 상태를 보존한다.
- 오산 코드 비교값은 trim한 원문이다. `ABC`와 `abc`, `A B`와 `A  B`는 서로 다르며 ` ABC `와 `ABC`는 같다.
- 같은 Title과 다른 코드는 허용한다. 완료된 오산 프로젝트의 코드도 재사용할 수 없다.
- 청주 기존 활성 Title unique와 기존 duplicate-code 허용 테스트는 그대로 통과해야 한다.
- 모든 list/detail/create는 선택된 오산 DB와 project access scope 안에서만 동작한다. 생성자는 생성 transaction 안에서 접근을 얻지만 진행 변경 권한은 자동으로 얻지 않는다.
- 생성은 전부 성공하거나 전부 rollback한다. Partial target·step·access·event·operation row를 남기지 않는다.
- 7단계는 `입고검사`, `배치검사`, `배선검사`, `8계통`, `동작검사`, `출하검사`, `포장` 순서다. 생성 시 시작·완료 시각이나 처리자를 기록하지 않는다.
- 청주 workflow stage, production plan, procurement item, work item, Pending, 별도 IQC/LQC/OQC/FAT, logistics handoff, G2, provider delivery를 생성하지 않는다.
- Task 4 범위인 단계 시작·완료·일괄 처리·마지막 포장 자동 완료와 Task 5 대시보드 집계는 추가하지 않는다.

## 6. 사용자가 관찰할 수 있는 완료 조건

- 오산 프로젝트 메뉴에서 목록과 `프로젝트 등록` 버튼이 보이고, 권한이 없는 사용자에게 등록 버튼과 mutation이 허용되지 않는다.
- 등록 화면에는 승인된 8개 항목만 순서대로 보인다.
- 유효한 값을 저장하면 한 번만 생성되고 상세 화면에서 입력값, 수량 N개의 대상과 대상별 7단계가 모두 `시작 전`으로 보인다.
- 같은 Title·다른 코드 생성은 성공하고, 같은 코드·다른 Title 및 코드 앞뒤 공백 중복은 이해 가능한 409/field 오류로 막힌다.
- PO/W/O를 비워도 생성되며 앞자리 0과 기호를 입력하면 상세에서 그대로 보인다.
- 수량 1과 500은 성공하고 0·음수·소수·501은 막힌다.
- 저장 실패 뒤 입력값을 유지하고 재시도할 수 있으며 저장 중 중복 제출이 차단된다.
- 청주 프로젝트 등록·목록·상세와 기존 회귀 테스트 결과가 변하지 않는다.

## 7. 테스트와 기대 증거

### Backend·DB

- `dotnet build backend/Emi.Qms.sln --configuration Release`와 전체 Backend test를 실행한다.
- 전용 API allow/deny matrix: OSAN 선택+권한, 권한 없음, CHEONGJU 선택, membership/local profile 대기, 다른 프로젝트 access scope.
- 8개 field validation과 길이 경계, PO/W/O null·앞자리 0·기호 보존, 수량 1/500 성공과 0·음수·소수·501 실패를 검증한다.
- 동일 Title/다른 code 성공, 동일 code/다른 Title 충돌, trim 중복 충돌, case/internal-space 구분, 완료 row code 재사용 거부를 검증한다.
- 실제 isolated PostgreSQL에서 같은 code 경쟁 생성은 정확히 한 성공·한 충돌이고 orphan/partial row가 0인지 확인한다.
- 같은 operation id replay와 다른 payload 충돌, 중간 실패 전체 rollback을 실제 DB에서 확인한다.
- N개 target과 N×7 step, 고정 순서, created access와 인앱 event 1건을 확인하고 기존 workflow/production/procurement/work item/Pending/provider delivery row가 0인지 확인한다.
- Migration catalog, fresh DB와 existing DB apply, 기존 migration hash 불변, 청주 Title unique와 duplicate-code 회귀를 검증한다.

### Frontend·browser

- 오산 목록 loading/empty/error/success, create 권한, 8개 field, client validation, duplicate submit, server field/conflict error, create→detail 이동을 unit/component test로 검증한다.
- `pnpm lint`, `pnpm typecheck`, 전체 frontend test와 build를 실행한다.
- Mock browser에서 desktop과 390px로 목록→등록→상세를 확인하고 console/request failure 0, horizontal overflow 0의 privacy-safe projection과 screenshot을 남긴다.
- 실제 3개 DB isolated Full-Stack에서 오산 요청은 오산 DB에만 쓰고 청주 DB count가 unchanged이며, create→list→detail과 필드·대상·7단계가 맞는지 확인한다. 실행별 임시 DB·process·port·artifact ownership과 cleanup을 검증한다.

테스트를 실행할 수 없으면 성공으로 쓰지 않고 명령, 실패 지점, 원인, 영향과 재실행 조건을 Implementation report에 기록한다. Screenshot과 browser artifact는 tracked/staged하지 않는다.

## 8. Parent 반환 조건과 제외 범위

다음 상황은 해당 의존 변경을 시작하지 않고 Parent에 반환한다.

- 8개 입력, 코드 비교, 7단계, 최대 수량, 권한 또는 Task 4/5 경계를 바꿔야 하는 경우
- 기존 migration을 수정·재번호화하거나 destructive data operation이 필요한 경우
- 실제 Azure/Persistent UAT DB, Entra, mail/Teams/web-push provider 또는 shared runtime을 변경해야 하는 경우
- Allowlist 밖 공통 contract 변경이나 청주 workflow 변경이 필요한 경우
- Push, PR, merge, `main` 반영 또는 branch/worktree 정리가 필요한 경우

이번 Change는 실제 단계 처리, 자동 프로젝트 완료, 현황 대시보드, Excel/PDF/첨부, 실제 외부 알림, 운영 migration과 Git 게시를 포함하지 않는다.

## 9. Fresh 최종 검증 보정

- verifierResult: `GO_AFTER_DOCUMENT_ONLY_RECHECK`
- verifierFindingCount: `P0 0 / P1 0 / P2 3`
- verifierOpenFindingCount: `P0 0 / P1 0 / P2 0 / P3 0`
- verifierModelRequested: `GPT_6_ASTRA_HIGH`
- verifierModelObserved: `NOT_REPORTED`
- reviewedDigestBeforeFinalStatusSync: `49d308d3fa078b64907bd77667f8915d6df2f16b28228916e2d1c4086fe4f3cf`
- remediationStatus: `COMPLETE`

| Finding | 상태 | 보정과 증거 |
| --- | --- | --- |
| `OSAN-PROJECT-VERIFY-01` | `RESOLVED` | Idempotent POST replay가 저장된 detail을 반환하기 전에 GET과 같은 project scope를 다시 검사한다. 새 생성은 같은 transaction에서 creator access를 만든 뒤 기존처럼 성공하며, access가 제거된 replay는 `403`이고 detail을 노출하거나 관련 row를 바꾸지 않는다. 실제 selected-OSAN 3 DB endpoint test에서 create·GET 성공, access 삭제 뒤 GET·replay `403`, row 상태 불변, access 복원 뒤 같은 project `replayed=true`, `Project.Read.All` replay를 확인했다. Targeted 2/2와 전체 Backend 582/582가 통과했다. |
| `OSAN-PROJECT-VERIFY-02` | `RESOLVED` | 목록과 상세의 프로젝트 코드 값에만 전용 `osan-project-code-value` class와 `white-space: break-spaces`를 적용했다. Mock browser가 두 위치의 `textContent='AbC  001'`와 computed style `break-spaces`를 직접 확인하고 desktop·390px screenshot, overflow·console·request failure 0을 다시 확인했다. Frontend 전용 7/7, 전체 291/291, lint·typecheck·build와 mock 1/1이 통과했다. |
| `OSAN-PROJECT-VERIFY-03` | `RESOLVED` | 구현 승인에는 production build/preview 기반 mock browser 검증과 같은 목적의 helper가 포함됐고 Implementation report에도 실제 경로와 필요성을 기록했지만, 신규 `frontend/playwright.osan-project-registration.mock.config.ts`가 위 exact allowlist에 빠져 있었다. 승인 범위를 넓히지 않고 실제 파일 경로를 allowlist에 명시해 staging 경계를 고정했다. 제품 코드·테스트 결과는 바꾸지 않았으며 fresh verifier의 document-only 재확인이 `PASS / GO`를 반환했다. |

사용자 직접 검수는 기존 결정대로 마지막 오산 일괄 검수에 남고, push·PR·merge·Persistent UAT·provider 권한은 추가되지 않았다.
