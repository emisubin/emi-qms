# TASK-OSAN-PROJECT-001 Change 001·002·003·004 구현 보고

## 1. 실행 기준과 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- change004TaskType: `BUGFIX`
- canonicalTask: `TASK-OSAN-PROJECT-001`
- canonicalChange: `TASK-OSAN-PROJECT-001 Change 001, Change 002, Change 003, Change 004`
- instructionChainRead: true
- taskIdentityGate: `PASS_REUSE`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- implementationApprovalSource: `USER_EXPLICIT_APPROVAL_2026-09-07`
- predecessorUserValidationStatus: `PENDING_FINAL_BATCH`
- implementationBranch: `feat/task-osan-project-001-project-registration`
- change001ImplementationBaseline: `2e29938f754f3d95444df2b341a921cfd1fca43f`
- change002ImplementationBaseline: `013298ae5058ec9435333956f57ee3917dd69d04`
- change003ImplementationBaseline: `7ec8dbc64830713b5dad0fdba8bd47de5773c2bf`
- change004ImplementationBaseline: `2292810a6d18b67f61dcac53ef87b9a923f60fc6`
- implementationWorktree: `/private/tmp/emi-osan-project-001`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- implementationOwnerObserved: `NOT_REPORTED`
- implementationStatus: `IMPLEMENTED_AWAITING_USER_VALIDATION`
- finalVerifierRequested: `GPT_6_ASTRA_HIGH`
- finalVerifierObserved: `NOT_REPORTED`
- finalVerifierResult: `GO`
- finalVerifierOpenFindingCount: `P0 0 / P1 0 / P2 0 / P3 0`
- change001ReviewedDigestBeforeFinalStatusSync: `49d308d3fa078b64907bd77667f8915d6df2f16b28228916e2d1c4086fe4f3cf`
- change002ProductTestReviewedDigest: `82a9f519c6e5d03d15871647027ae91f0d9fe59951389d4b09f9410509428199`
- change003ProductTestReviewedDigest: `ee7e5cd7c968668546ae461189299157d9e1c8c7b8581da34337027628bd57ef`
- change004ProductTestReviewedDigest: `243ce46643c51fd6d4f69300516fd84cacf6527a8c194ee936df7e9d2d41bc9c`
- localCommitApprovedAfterReviewAndFreshVerification: true
- gitPublicationApproved: false
- persistentRuntimeMutationApproved: false
- providerMutationApproved: false

구현 전에 Task worktree의 Root·Backend·Frontend·Scripts 지침, Product Roadmap, Task 종료 정책, Validation Matrix, Privacy-safe Evidence, canonical Task·planning·review·Change를 읽고, session-current canonical Root 지침의 GPT-6→Sol→GPT-6 역할 계약과 local commit 정책을 함께 확인했다. 사용자가 Task 2 직접 검수를 마지막 오산 일괄 검수로 미루고 Task 3 구현을 승인한 범위에서만 작업했다.

`BusinessUnitConfiguration.BusinessSchemaVersion`과 예시 설정의 `ExpectedSchemaVersion`은 최신 migration 번호가 아니라 migration `0086`이 도입한 영구 database identity binding 계약이다. 구현 중 이를 최신 원장 번호 `0087`로 해석한 시안을 폐기하고 두 공통 파일을 기준선과 동일하게 되돌렸다. 최신 migration 적용 여부는 `schema_migrations`와 migration test가 `0087_osan_project_registration`까지 별도로 검증한다.

## 2. 구현 결과

### 오산 프로젝트 저장 계약

- Additive migration `0087`이 기존 `projects` 행을 기본 `Cheongju` profile로 유지하고 오산 PO No, W/O No, 자유 제품명, 수량을 추가한다.
- 기존 활성 Title unique index는 `Cheongju` profile에만 적용한다. 오산은 완료 여부와 관계없이 trim이 끝난 `project_code` 원문을 unique key로 사용한다. 따라서 `ABC`와 `abc`, `A B`와 `A  B`는 서로 다르고, ` ABC `와 `ABC`는 같은 코드다.
- 오산 진행 대상, 대상별 7단계 snapshot, 생성 operation, local in-app `ProjectCreated` event를 전용 테이블에 저장한다. Step의 `(project_id, target_id)` composite FK가 다른 프로젝트의 대상을 연결하지 못하게 한다.
- 이후 상태가 바뀌는 대상과 단계 원본은 0087에서 기존 `qms_audit_capture_row_change()`에 global audit trigger로 연결한다. Local project event는 canonical ledger, operation row는 idempotency ledger로 명시 분류한다. 기존 0083 migration은 수정하지 않았다.
- 한 transaction에서 project, creator access, N targets, N×7 steps, event 1건과 operation 완료 결과를 만든다. Snapshot insert를 강제로 실패시키는 실제 PostgreSQL trigger test에서 모든 partial row와 operation row가 rollback됨을 확인했다.
- 같은 operation ID와 같은 payload는 동일 project를 replay하고, 같은 operation ID의 다른 payload는 conflict다. 같은 code의 경쟁 생성은 DB unique로 정확히 한 요청만 성공한다.
- `WorkflowStore`, production/procurement snapshot, panel placeholder, work item, Pending, notification delivery 또는 provider를 호출하지 않는다.

### API와 권한 경계

- 오산 전용 route는 `POST /api/osan/projects`, `GET /api/osan/projects`, `GET /api/osan/projects/{projectId}` 세 개다.
- Capability middleware는 exact collection GET/POST와 GUID detail GET만 오산에서 허용한다. 일반 `/api/projects`, 다른 method, hold/cancel, Pending, G2 및 기존 업무 route는 계속 차단된다.
- Endpoint도 선택된 trusted business-unit context가 `OSAN`인지 다시 확인한다. POST는 기존 `Project.Create`와 `projects.read`, GET은 `projects.read`와 기존 project access scope를 사용한다. 청주 context의 전용 route 호출은 `403 business_unit_capability_disabled`로 거부한다.
- POST는 화면 입력 8개와 보이지 않는 client operation ID를 받는다. 문자열은 앞뒤 공백만 제거하며 PO/W/O의 앞자리 0·기호와 코드의 대소문자·내부 공백을 보존한다. 수량은 1~500 정수만 허용한다.
- Validation은 field problem으로, code 또는 operation 충돌은 field map을 포함한 `409`로 반환한다. 성공은 생성된 전용 detail과 replay 여부를 반환한다.
- 새 생성은 같은 요청에서 creator access를 만든 뒤 정상 성공한다. Idempotent replay는 저장된 detail을 응답하기 전에 GET과 같은 project scope를 다시 검사하므로 현재 access가 없으면 `403`이며, `Project.Read.All`은 기존처럼 허용한다.

### 오산 목록·등록·상세 화면

- 오산 shell의 프로젝트 메뉴가 전용 목록을 열며 loading, empty, error/retry, forbidden과 ready 상태를 제공한다.
- `Project.Create`, `projects.read`와 runtime mutation 가능 상태를 모두 충족할 때만 등록 버튼과 등록 화면을 제공한다. `Project.Create`만 가진 사용자의 버튼을 숨기고 직접 등록 URL도 forbidden으로 처리한다. 등록 화면에는 `프로젝트 Title`, `프로젝트 코드`, `거래처`, `PO No`, `W/O No`, `납기일`, `제품명`, `수량`만 이 순서대로 표시한다. operation ID는 사용자 화면에 표시하지 않는다.
- Client validation이 필수값, 길이와 수량 범위를 검사한다. 저장 중 버튼을 잠그고 handler도 재진입을 차단해 중복 submit을 한 번의 요청으로 제한한다.
- Server의 visible field/code conflict 오류를 해당 입력에 연결하고 입력값을 보존한다. Operation conflict는 전역 안내를 표시하고 다음 제출 전에 새 operation ID를 발급해 복구 가능한 재시도를 제공한다.
- Change 001 당시 성공 화면은 trim 후 저장된 8개 값, N개 대상과 각 7단계 `시작 전`을 직접 표시했다. Change 004 현재 화면은 같은 원본을 유지하면서 대상별 현재 단계와 완료 수/7로 요약한다. 목록에서도 내부 공백을 포함한 코드를 그대로 표시한다.
- 목록과 상세의 프로젝트 코드 값에만 `white-space: break-spaces`를 적용해 `A B`와 `A  B`의 차이가 화면에서도 보인다. 다른 상세 값의 whitespace 표현은 바꾸지 않는다.
- Change 001에서는 청주 shell·화면과 일반 `/api/projects` 경로를 수정하지 않았다. Change 004는 청주와 오산의 presentation 구현과 공용 geometry를 함께 변경했지만 청주 shell·API·업무 interaction은 보존했다.

### Change 002 — 청주형 목록·상세 정렬

- Desktop 오산 목록을 column header 아래 프로젝트별 한 행으로 정렬했다. 프로젝트명·코드, 거래처, 제품명, 수량, 납기일, 상태와 상세 이동을 한 행에서 읽을 수 있고 keyboard Enter/Space로 열 수 있다.
- 860px 이하에서는 청주 mobile project list와 같은 카드 읽기 순서로 전환한다. Desktop table과 mobile card를 동시에 노출하지 않는다.
- 상세는 청주 상세의 breadcrumb/mobile back, 제목 header, 기본 정보 summary, sticky department tab, tab content 순서를 따른다.
- 오산 department tab은 `진행 관리` 하나만 제공한다. 기존 진행 대상 N개와 대상별 7단계는 이 tab panel에 그대로 배치한다. 일곱 단계를 각각 tab으로 만들지 않았다.
- 생성 직후 기술 상태 `Active`는 현재 생성 전용 slice의 업무 의미에 맞춰 `시작 전`으로 표시한다. 실제 진행 집계와 상태 전환은 Task 4에 남겼다.
- Desktop/mobile 목록·상세 네 위치에서 코드의 대소문자와 내부 연속 공백이 변형되지 않도록 전용 style을 적용했다.
- Backend, DB, API, 권한, 등록·idempotency, Pending·중단·보류·취소와 진행 mutation은 변경하지 않았다.

### Change 003 — 청주 UI·UX 구조 재사용

- 사용자 검수에서 Change 002가 청주를 참고한 별도 오산 UI를 만든 결과라 과도하다고 판정했다. 별도 `osan-project-list-*`, `osan-project-summary`, `osan-project-target-*` markup·CSS를 제거하고 청주 presentation을 직접 적용했다.
- 목록은 청주의 `project-list-*` desktop row와 mobile card, 8열 grid와 반응형 전환을 그대로 사용한다. 오산 데이터는 프로젝트명·거래처·Code·제품명·수량·납기일·상태·진행률에 매핑한다.
- 상세는 청주의 `page-surface`, breadcrumb/mobile hero, compact summary와 `기본정보 전체 보기`, sticky department tab/content를 사용한다. 승인된 입력 8개는 모두 유지한다.
- `진행 관리`는 청주 제조 탭과 같은 section header, 네 지표, desktop status table 또는 mobile status card와 progress meter를 사용한다. 대상별 현재 단계와 완료 수/7만 요약하며 Change 002의 별도 7단계 카드 목록은 제거했다. 7단계 원본·순서 계약은 API/DB와 기존 test에 유지한다.
- 사용자-facing 차이는 `진행 관리` 단일 탭, 제품명·수량 표시와 Pending·보류·중단·취소·청주 전용 action 제외뿐이다. 청주 코드·공통 style·Backend/API/DB/권한/mutation은 변경하지 않았다.
- 오산 전용 presentation CSS는 프로젝트 코드의 대소문자·내부 연속 공백을 보존하는 최소 규칙만 남겼다.

### Change 004 — 실제 공용 컴포넌트와 양쪽 시각 검증

- Change 003은 청주 class와 유사 DOM을 오산 branch에 다시 작성해 코드상 유사성만 확보했다. 목록·요약·현황을 각각 `ProjectListPresentation`, `ProjectSummaryPresentation`, `ProjectDepartmentStatusBoard`로 추출해 청주와 오산이 같은 React 표시 구현을 직접 호출하도록 바꿨다.
- 업무 명칭·값·허용 action만 adapter·option으로 전달한다. 청주의 선택·내보내기·다중 부서 tab·현황 action은 유지하고 오산은 단일 `진행 관리` tab과 읽기 전용 현황을 유지한다.
- 공용 desktop 목록 행을 60px로 맞추고 header/body 열 기준선을 정렬했다. Action이 없는 오산 현황 행·카드는 `div`/`article`로 렌더링해 button hover·focus를 제거하면서 청주의 기존 button 상호작용은 보존했다.
- `frontend/AGENTS.md`에 화면 동일성 변경은 실제 공용 component 재사용과 같은 run의 desktop/mobile 양쪽 screenshot, 사람의 시각 확인을 완료 조건으로 삼도록 기록했다. 이는 검증 기준을 강화하며 mutation·Git·운영 권한을 확대하지 않는다.
- 1440×900과 390×844에서 청주·오산 목록·상세 8개를 같은 production-preview run으로 캡처했다. 전체 page는 승인된 기능 수가 달라 section 수가 다르지만 공용 목록 행·카드, 상세 요약, 현황 표·카드의 간격·테두리·구조와 반응형 전환은 같은 구현과 시각 규칙을 사용한다.

## 3. 변경 파일

### 제품·migration

- `database/migrations/0087_osan_project_registration.sql`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitCapabilityMiddleware.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectContracts.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectInputNormalizer.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectStore.cs`
- `backend/src/Emi.Qms.Api/OsanProjects/OsanProjectEndpointExtensions.cs`
- `frontend/src/App.tsx`
- `frontend/src/api.ts`
- `frontend/src/projects.ts`
- `frontend/src/styles.css`

### 검증·Task 산출물

- `docs/00-product-roadmap.md`
- `frontend/AGENTS.md`
- `backend/tests/Emi.Qms.Api.Tests/AuditMutationCoverageTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/OsanProjectRegistrationApiTests.cs`
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `frontend/e2e/full-stack/osan-project-registration.full-stack.spec.ts`
- `frontend/playwright.osan-project-registration.full-stack.config.ts`
- `frontend/playwright.osan-project-registration.mock.config.ts`
- `scripts/e2e-osan-project-registration-full-stack.sh`
- `tasks/osan-project-001-change-002.md`
- `tasks/osan-project-001-change-003.md`
- `tasks/osan-project-001-change-004.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`

Task 3 stacked branch에는 canonical 문서화 작업에서 이미 승인된 오산 기획·Task 계보 14개도 같은 내용으로 포함해 기획 source, 선행 완료, 후속 Task와 Roadmap 링크가 끊기지 않게 했다. Task 3 상태에 직접 관련된 `osan-pilot-001.md`, `osan-pilot-001-implementation-report.md`, `osan-project-001.md`와 Roadmap만 현재 자동 검증·review Gate에 맞춰 갱신했으며 기획 원문과 독립 기획 review 원문은 재작성하지 않았다.

Mock browser도 production build/preview에서 독립 실행하고 screenshot output ownership을 분리해야 했으므로 같은 목적의 전용 helper `frontend/playwright.osan-project-registration.mock.config.ts`를 추가했다. React development Strict Mode가 의도적으로 취소하는 첫 read를 request failure로 오인하지 않고 실제 배포형 client 동작을 검증하기 위한 설정이다. Change 004에서는 `frontend/tests/App.test.tsx`도 청주 공용 component 회귀 검증을 위해 변경했다. `frontend/tests/BusinessUnitAccess.test.tsx`는 변경할 필요가 없었다.

`backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs`와 `backend/src/Emi.Qms.Api/appsettings.BusinessUnits.example.json`은 위 identity binding 해석을 확정한 뒤 기준선과 diff 0임을 확인했다. Fresh verifier 보정 지시에 따라 canonical Task, Change와 이 보고서에 Finding·해소·재검증 결과만 추가했으며 다른 Task 문서는 수정하거나 stage하지 않았다. Change 004의 정확한 변경 경계는 해당 Change의 allowlist 10개 파일이며 Backend·DB·migration은 포함하지 않는다.

## 4. 검증 결과

### Backend·DB 집중 검증

- Release solution build: `PASS`, 경고 0, 오류 0.
- 오산 API·store·audit 집중: `8/8 PASS`.
- Global audit relation 분류: `1/1 PASS`. 0083 registry 94개와 post-0083 tracked relation 2개를 합쳐 schema relation을 정확히 분류하고, event·operation 예외까지 중복·누락 없이 확인한다.
- Migration 0087 단독 upgrade·반복 적용: `1/1 PASS`. 정렬된 migration 목록의 0087 직전인 0086까지만 baseline을 만들고 0087을 두 번 적용해 대상·단계 global audit trigger 2개 존재를 실제 PostgreSQL에서 확인한다.
- PostgreSQL migration test: `63/63 PASS`.
- 실제 selected-OSAN 3 DB endpoint·business-unit isolation targeted: `2/2 PASS`, 19초. Access revoke·restore와 `Project.Read.All` replay까지 확인했고 test-owned DB와 Compose 자원을 정리했다.
- 전체 Backend suite: `582/582 PASS`, 22분 56초. Test-owned DB drop과 Compose container/network cleanup도 통과했다.
- 전용 test는 exact endpoint catalog, trusted OSAN allow/CHEONGJU deny, Project.Create·projects.read·access scope, 모든 입력 길이/필수값, 수량 1·500과 invalid bounds, 소수 JSON 거부, outer trim, PO/W/O 보존을 다룬다.
- 실제 PostgreSQL에서 동일 operation 동시 replay, operation payload conflict, 같은 code 경쟁 1 success/1 conflict, completed code 재사용 거부, case/internal-space 구분, 같은 Title/다른 code 허용, N targets와 N×7 steps, creator access 1건, event 1건, 금지 downstream row 0건과 강제 중간 실패 rollback을 확인했다.
- Migration test는 catalog 최신 `0087`, fresh·existing DB apply, repeat apply, 기존 migration hash 불변, 기존 row `Cheongju`, 청주 활성 Title unique와 duplicate code 허용, 오산 code unique를 확인한다.

### Change 001 Frontend·browser 검증

- Frontend lint: `PASS`, 오류 0, 기존 `frontend/src/main.tsx` fast-refresh warning 1.
- Frontend typecheck: `PASS`.
- Frontend 전체 unit/component: `36 files, 291/291 PASS`, 25.33초.
- 오산 전용 component: `7/7 PASS`. 등록 8개 field/순서, 입력 보존, duplicate submit 1회, code conflict 동일 operation replay, operation conflict 전역 안내·새 operation ID 재시도, quantity validation, create-only 권한의 버튼 숨김·직접 URL forbidden, loading/error/retry/empty를 포함한다.
- Production build: `PASS`, 399 modules transformed. 기존 500 kB chunk warning은 유지된다.
- Mock browser: `1/1 PASS`. Desktop과 390px에서 목록→등록→상세, 8개 값 보존, N targets, 각 7단계, console error 0, request failure 0, horizontal overflow 0을 확인했다. 상세와 목록 코드의 정확한 `textContent`가 `AbC  001`, computed `white-space`가 `break-spaces`인지 별도로 검증했다.
- Change 001 당시 privacy-safe screenshot은 untracked browser output에만 두고 stage하지 않았다. Change 002가 같은 scenario를 새 화면으로 다시 검증하면서 현재 artifact 4개로 교체했다.
- 실제 synthetic 3 DB full-stack: 직전 최종 `1/1 PASS` 증거를 재사용했다. Test-owned Directory/CHEONGJU/OSAN DB와 6 bounded roles, Release backend, production frontend와 Chromium에서 create→detail→list, 오산 프로젝트 +1, targets 2, steps 14와 청주 project row count·API body 불변, owned cleanup을 확인한 실행이다. 이번 보정은 replay access branch와 코드 표시 CSS에 한정됐고 최신 실제 selected-OSAN 3 DB endpoint test가 create·GET·replay·access revoke/restore를 직접 실행했으므로 combined full-stack은 다시 실행하지 않았다.
- `git diff --check`: `PASS`.

### Change 002 Frontend·browser 검증

- 오산 전용 component: `8/8 PASS`. Desktop table의 header·단일 row·클릭 이동, 상세의 단일 `진행 관리` tab/tabpanel과 기존 등록 흐름을 포함한다. Keyboard Enter/Space handler는 parent와 fresh verifier가 code review에서 확인했다.
- Frontend 전체 unit/component 최종: `36 files, 292/292 PASS`. 첫 전체 run의 기존 AuditPage async-load flake는 제품 변경 없이 재실행해 전체 통과했다.
- Frontend lint: 오류 0, 기존 `frontend/src/main.tsx` fast-refresh warning 1. Typecheck와 production build도 `PASS`이며 기존 chunk-size warning만 남았다.
- Mock browser: `1/1 PASS`. Desktop/390px 목록·상세, 프로젝트별 한 행, mobile card, breadcrumb/back, 단일 tab semantics, N×7단계, 네 코드 위치의 정확한 `AbC  001`과 computed `break-spaces`/`text-transform: none`, 생성 직후 `시작 전`, console error 0, request failure 0과 page horizontal overflow 0을 확인했다.
- Privacy-safe screenshot은 `frontend/test-results/osan-project-registration-mock/` 아래 desktop/mobile 목록·상세 4개 untracked artifact로 갱신했고 stage하지 않는다.
- Parent GPT-6 검토 뒤 첫 fresh GPT-6 High verifier가 코드 표시 변형과 생성 직후 상태 표기의 P2 두 건을 찾았다. Sol이 같은 allowlist에서 보정했고 새 fresh GPT-6 High verifier가 `PASS / GO`, open P0/P1/P2/P3 `0/0/0/0`을 반환했다.
- 이번 Change는 Frontend-only이므로 Backend·DB·실제 3 DB full-stack을 재실행하지 않았다. Change 001의 통과 증거와 계약은 변경되지 않았다.

### Change 003 Frontend·browser 검증

- 오산 전용 component `8/8 PASS`, 선택한 청주 목록·상세·제조 회귀 `4/4 PASS`다. Parent도 오산과 청주 `App.test.tsx`를 함께 `97/97 PASS`로 확인했다.
- Frontend 전체 최종 `36 files / 292/292 PASS`다. Parent 첫 run에서 기존 `QualityInspectionsPage` async loading test 한 건이 간헐 실패했지만 해당 파일 단독 `4/4`와 제품 변경 없는 전체 재실행 `292/292`로 통과했다.
- Lint는 오류 0과 기존 `frontend/src/main.tsx` Fast Refresh warning 1이다. Typecheck, production build와 `git diff --check`도 `PASS`이며 기존 chunk-size warning만 남았다.
- Task 전용 production preview mock Chromium `1/1 PASS`다. Desktop·390px 목록/상세, 청주 class·반응형 구조, 8개 열·입력값, 단일 `진행 관리` tab, 대상별 `입고검사` 현재 단계와 `0/7`, 금지 action 부재, 코드 공백 보존, console error 0, request failure 0, horizontal overflow 0을 확인했다.
- Privacy-safe screenshot 4개는 기존 ignored test output 경로에서 갱신했다. 일반 development-server config의 예비 실행에서 React Strict Mode가 취소한 중복 mocked GET을 request failure로 잡았으나, 배포형 client를 검증하는 승인된 전용 production preview config에서는 실제 request failure가 0이었다.
- Fresh GPT-6 High verifier는 첫 review에서 desktop 진행 표의 columnheader/cell semantics 누락 P2 한 건을 찾았다. Sol이 5개 header와 행별 5개 cell role 및 test를 추가했고, verifier가 최종 `PASS / GO`, 열린 제품 P0/P1/P2/P3 `0/0/0/0`을 반환했다.
- 이번 Change는 Frontend-only이며 Backend·DB·실제 3 DB full-stack을 재실행하지 않았다. Change 001의 통과 증거와 7단계 순서·초기 상태 assertion은 변경되지 않았다.

### Change 004 공용 컴포넌트·paired visual 검증

- Parent 집중 component는 `2 files / 97/97 PASS`, Frontend 전체는 `36 files / 292/292 PASS`다.
- Lint는 오류 0과 기존 `frontend/src/main.tsx` Fast Refresh warning 1이다. Typecheck, production build와 `git diff --check`도 `PASS`이며 기존 chunk-size warning만 남았다.
- Production-preview Chromium `1/1 PASS`다. 같은 run에서 청주·오산의 목록·상세를 1440×900과 390×844로 캡처하고 실제 공용 component marker, DOM element, computed style, 60px 목록 행, 8개 공통 열의 left/width, header/body 정렬과 page overflow를 비교했다.
- Parent가 8개 screenshot을 직접 눈으로 확인했다. 공용 목록 행·카드, 상세 요약, 현황 표·카드는 간격·테두리·구조·반응형 전환이 일치한다. 청주의 필터·KPI·다중 부서 tab·업무 action과 오산의 단일 `진행 관리` tab 같은 승인된 기능 차이는 비교 범위에서 명시적으로 분리했다.
- 첫 fresh GPT-6 High verifier가 비상호작용 오산 현황의 native button, desktop 목록 높이·열 geometry, 청주 panelName null fallback을 Finding으로 반환했다. 모두 같은 allowlist에서 보정하고 browser test를 강화했다. 증빙 상태가 한쪽만 펼쳐진 문제도 Parent가 찾아 양쪽 `details.open=false`를 assert한 뒤 다시 캡처했다.
- 새 fresh GPT-6 High verifier가 8개 최종 screenshot과 실제 diff를 읽고 `PASS / GO`, open P0/P1/P2/P3 `0/0/0/0`을 반환했다. 요청 모델은 `GPT_6_ASTRA_HIGH`, 도구의 관측 모델은 `NOT_REPORTED`다.
- Product/test reviewed digest는 `243ce46643c51fd6d4f69300516fd84cacf6527a8c194ee936df7e9d2d41bc9c`다. Screenshot 8개는 ignored test output이며 stage하지 않는다.
- 이번 Change는 Frontend-only이므로 Backend·DB·실제 3 DB full-stack을 재실행하지 않았다. Change 001의 통과 증거와 API·권한·7단계 저장 계약은 변경되지 않았다.

| 검증 구분 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend build·API·authorization·DB | 적용 | `PASS` | Release 0/0, 집중 8/8, migration 63/63, selected-OSAN 3 DB targeted 2/2, 전체 582/582 |
| Frontend unit·quality | 적용 | `PASS` | Change 001 전체 291/291·전용 7/7, Change 002·003·004 전체 292/292, Change 004 집중 97/97, lint error 0, typecheck와 production build 성공 |
| Mock desktop·390px | 적용 | `PASS` | Change 004 paired production-preview browser 1/1, 청주·오산 screenshot 8개 직접 비교, console/request failure/unexpected request/overflow 각 0 |
| 실제 synthetic combined full-stack | 적용 | `PASS_PRIOR_EVIDENCE_REUSED` | 직전 3 DB·6 role create/list/detail와 DB row assertion·owned cleanup; 최신 selected-OSAN 3 DB endpoint targeted 2/2로 보정 경로 확인 |
| Persistent UAT·실제 provider | 미적용 | `N/A` | 승인 범위 밖이며 로컬 합성 환경만 사용 |
| 사용자 직접 검수 | 대기 | `PENDING_CURRENT_SCREEN_REVIEW_AND_FINAL_BATCH` | Change 004 화면은 local synthetic server에서 검수하고 전체 흐름은 오산 개발 마지막 일괄 검수로 추적 |

## 5. 품질 Finding과 제한

- Fresh GPT-6 High 독립 검증은 Change 001~003의 기존 Finding 해소와 Change 004의 실제 공용 컴포넌트·paired visual 검증을 확인했다. 검증 중 발견된 구조·geometry·fallback·증빙 상태 문제를 아래와 같이 해소했으며 최종 열린 제품·문서 P0/P1/P2/P3는 `0/0/0/0`이다.

| Finding | 심각도 | 상태 | 원인·영향과 해소 |
| --- | --- | --- | --- |
| `OSAN-PROJECT-IMPL-F01` | P2 | `RESOLVED` | Hidden operation conflict가 field error로만 매핑돼 안내가 사라지고 같은 ID가 반복됐다. Visible 8개 field만 field error로 취급하고 전역 안내와 다음 제출용 새 ID를 추가했다. |
| `OSAN-PROJECT-IMPL-F02` | P2 | `RESOLVED` | UI가 `Project.Create`만 확인해 `projects.read` 없는 사용자가 Backend에서 거부될 등록 화면을 열 수 있었다. 오산 list/create의 gate를 두 permission과 mutation 상태 조합으로 맞추고 직접 URL을 포함해 검증했다. |
| `OSAN-PROJECT-IMPL-F03` | P2 | `RESOLVED` | 신규 대상·단계 relation이 global audit 분류와 trigger에 없었다. 0087 trigger와 post-0083 tracked relation을 추가하고 event·operation의 ledger 예외를 명시했다. |
| `OSAN-PROJECT-IMPL-F04` | P2 | `RESOLVED` | 0087 upgrade fixture가 제외 filter를 사용해 미래 0088+를 0087 전에 적용할 수 있었다. 정렬 목록의 0087 직전까지만 취하도록 바꿨다. |
| `OSAN-PROJECT-VERIFY-01` | P2 | `RESOLVED` | Idempotent POST replay가 현재 project access를 다시 확인하지 않고 detail을 반환했다. Replay 응답 직전에 GET과 같은 scope를 적용하고, 실제 selected-OSAN 3 DB에서 access 삭제 뒤 GET·POST `403`과 row 불변, access 복원·`Project.Read.All` replay 성공을 검증했다. |
| `OSAN-PROJECT-VERIFY-02` | P2 | `RESOLVED` | 코드 내부 공백은 데이터로 구분됐지만 기본 CSS가 화면에서 연속 공백을 접었다. 목록·상세 코드에만 전용 class와 `break-spaces`를 적용하고 양쪽 `textContent`·computed style 및 새 desktop/mobile screenshot을 검증했다. |
| `OSAN-PROJECT-VERIFY-03` | P2 | `RESOLVED` | Mock browser를 production build/preview에서 독립 실행하는 전용 설정은 승인된 테스트 방식의 같은 목적 helper이고 보고서에 경로·필요성이 기록됐지만 Change exact allowlist에는 빠져 있었다. `frontend/playwright.osan-project-registration.mock.config.ts`를 exact allowlist에 추가해 staging 경계를 실제 변경과 맞췄고 document-only 재확인 `PASS / GO`를 받았다. 제품 파일과 테스트 결과는 바꾸지 않았다. |
| `OSAN-UI-VERIFY-01` | P2 | `RESOLVED` | Mobile code에 전역 uppercase style이 적용되고 detail hero는 내부 공백도 접었다. 전용 code class와 `text-transform: none`, `break-spaces`를 desktop/mobile 목록·상세 네 위치에 적용하고 computed style로 확인했다. |
| `OSAN-UI-VERIFY-02` | P2 | `RESOLVED` | 모든 target/step이 `시작 전`인 생성 직후 기술 상태 `Active`를 `진행 중`으로 표시했다. 현재 slice에서 `Active`를 `시작 전`으로 표시하고 unit/browser test를 보정했다. |
| `OSAN-UI-RECORD-01` | P2 | `RESOLVED` | Component test가 확인한 클릭 이동을 keyboard 검증으로 기록했다. 실제 test evidence에 맞춰 클릭 이동으로 정정하고 keyboard handler 확인은 code review 근거로 구분했다. |
| `OSAN-UI-RECORD-02` | P3 | `RESOLVED` | Change 001 metadata와 Change 002 현재 상태, 파일 목록·검수 상태가 혼재했다. Change별 baseline/digest와 현재 화면 검수·마지막 일괄 검수 상태를 명시했다. |
| `OSAN-UI-REUSE-A11Y-01` | P2 | `RESOLVED` | 청주 presentation을 적용한 오산 desktop 진행 표의 header/data span에 table semantics가 없었다. 5개 `columnheader`와 행별 5개 `cell` role을 추가하고 component/browser test로 검증했다. |
| `OSAN-UI-REUSE-DOC-01` | P3 | `RESOLVED` | Change 003 초안이 대상별 7단계 전체 표시를 완료 조건으로 남겨 최신 사용자 지시와 충돌했다. 청주 제조 탭과 같은 현재 단계·완료 수/7 요약으로 정정하고 7단계 원본·순서 검증은 API/DB 계약에 유지했다. |
| `OSAN-UI-PARITY-01` | P2 | `RESOLVED` | Action이 없는 오산 현황 행·카드에 native button이 남아 높이·radius와 hover/focus affordance가 달랐다. 오산은 비상호작용 `div`/`article`, 청주는 기존 button으로 렌더링하고 양쪽 동작을 검증했다. |
| `OSAN-UI-PARITY-02` | P2 | `RESOLVED` | 청주 desktop 목록 행은 약 60px, 오산은 약 42px였고 header/body 열도 2px 어긋났다. 공용 60px 행과 열 정렬을 적용하고 같은 run의 geometry assertion을 추가했다. |
| `OSAN-UI-PARITY-03` | P3 | `RESOLVED` | 공용화 중 청주 null panelName fallback이 달라졌다. Desktop code fallback과 mobile `패널명 미입력`을 복원하고 회귀 test로 확인했다. |
| `OSAN-UI-EVIDENCE-01` | P3 | `RESOLVED` | 첫 paired screenshot에서 오산 상세만 기본정보가 펼쳐져 비교 상태가 달랐다. 양쪽 `details.open=false`를 assert하고 최종 증빙 8개를 다시 만들었다. |
| `OSAN-UI-DOC-AUTHORITY-01` | P2 | `RESOLVED` | `frontend/AGENTS.md`의 새 시각 동일성 절에 승인 권한 불변이 직접 적히지 않았는데 Change·Roadmap이 이를 주장했다. 해당 절에 Task 승인 출처·allowlist·상위 승인 경계를 그대로 따른다는 문장을 추가했다. |
| `OSAN-UI-DOC-SCOPE-01` | P3 | `RESOLVED` | 누적 구현 보고의 “청주 화면은 수정하지 않았다”는 Change 001 사실이 Change 004 현재 설명처럼 읽혔다. Change 001 시점으로 한정하고 Change 004의 청주 presentation 변경과 shell·API·업무 interaction 보존을 구분했다. |

- 검증 관찰: 기존 Frontend fast-refresh warning 1과 production chunk size warning이 남아 있다. 이 Change의 동작·build를 막지 않으며 변경 범위 밖 기존 출력이다.
- 실제 Azure/Persistent UAT migration, shared runtime handover, Entra, mail/Teams/web-push provider와 외부 notification delivery는 실행하지 않았다. 따라서 이 보고의 통과 주장은 local test-owned PostgreSQL과 합성 계정 범위다.
- Task 4의 진행 시작·완료·일괄 처리·포장 완료 자동 프로젝트 완료와 Task 5 dashboard는 구현하지 않았다.
- Excel, PDF, 첨부 파일과 외부 알림은 이 Change의 데이터 계약과 화면에 추가하지 않았다.
- 개인정보·secret 검토: `PASS`. Tracked 대상에는 합성 UUID·합성 역할·placeholder 데이터만 사용했고 실제 사용자 식별정보, tenant/client/object ID, secret, token, password와 Authorization header를 기록하지 않았다. Desktop/mobile screenshot은 합성 사용자·프로젝트만 포함하며 untracked test output으로 유지한다.

## 6. SOP

### 로컬 검증·운영 절차

1. Product DB migration runner로 `0087_osan_project_registration.sql`을 적용한다. `BusinessSchemaVersion=0086_business_unit_database_identity`는 identity binding preflight 값이므로 바꾸지 않는다.
2. Directory에서 사용자의 active OSAN membership과 Osan DB local profile·role을 확인한다. 생성자는 `Project.Create`와 `projects.read`가 모두 필요하다.
3. 전용 3 DB harness는 매 실행 test-owned PostgreSQL, DB 이름, bounded role, backend/frontend port를 만들고 소유권을 검증한 뒤 종료 때 모두 제거한다.
4. 적용 후 `schema_migrations`의 `0087`, 오산 프로젝트 profile, targets·steps·event·operation과 청주 count 불변을 확인한다.
5. 적용 뒤 결함은 기존 migration을 수정하지 않고 후속 additive migration과 code forward-fix로 보정한다. 운영 적용 전에는 code를 revert할 수 있으나 운영 적용 후 destructive rollback은 수행하지 않는다.

이번 구현은 Persistent UAT나 공유 runtime에 적용하지 않았으므로 runtime handover와 운영 rollback 작업은 발생하지 않았다.

## 7. 사용자 안내

1. 오산 사업부로 들어가 공통 메뉴의 `프로젝트`를 선택한다.
2. 등록 권한이 있으면 `프로젝트 등록`을 선택한다.
3. 프로젝트 Title, 프로젝트 코드, 거래처, PO No, W/O No, 납기일, 제품명, 수량 순서로 입력한다. PO No와 W/O No는 비워도 된다.
4. 프로젝트 코드는 앞뒤 공백이 제거되어 저장된다. 대소문자와 내부 공백은 데이터와 목록·상세 화면에서 그대로 구분되며 이미 사용한 코드는 완료 프로젝트의 코드도 다시 쓸 수 없다.
5. 수량은 1~500의 정수를 입력한다. 저장 실패 시 입력은 유지되므로 표시된 field 오류를 고쳐 다시 등록할 수 있다.
6. 등록 성공 뒤 상세에서 입력값과 수량만큼의 대상이 보이고, 각 대상의 현재 단계가 `입고검사`, 진행이 `0/7`, 상태가 `시작 전`인지 확인한다. 원본에는 `입고검사 → 배치검사 → 배선검사 → 8계통 → 동작검사 → 출하검사 → 포장` 7단계가 저장된다.
7. 목록과 상세가 청주 프로젝트 화면과 같은 행·카드·요약·제조 현황 구조를 사용하는지 확인하고, 상세에는 `진행 관리` tab 하나만 표시되는지 확인한다.

이 화면에서는 단계 상태를 바꾸거나 프로젝트를 완료하지 않는다. 해당 기능은 후속 Task에서 제공한다.

## 8. 사용자 검수 checklist

- [ ] 오산 프로젝트 목록과 권한 있는 사용자의 `프로젝트 등록` 버튼을 확인한다.
- [ ] 등록 화면에 승인된 8개 항목만 정해진 순서로 표시되는지 확인한다.
- [ ] PO/W/O 빈값과 앞자리 0·기호가 상세에 그대로 보이는지 확인한다.
- [ ] 같은 Title·다른 code는 생성되고, 같은/outer-trimmed code는 이해 가능한 오류로 막히는지 확인한다.
- [ ] 수량 1과 500은 성공하고 0·음수·소수·501은 막히는지 확인한다.
- [ ] 실패 뒤 입력이 유지되고 빠른 중복 submit으로 프로젝트가 중복되지 않는지 확인한다.
- [ ] 상세에서 N개 대상의 상태가 `시작 전`, 현재 단계가 `입고검사`, 진행이 `0/7`인지 확인한다.
- [ ] Desktop 목록과 mobile 목록이 청주 프로젝트의 기존 행·카드 UI와 동일하게 표시되는지 확인한다.
- [ ] 상세가 청주 상세 UI를 사용하고 부서 tab은 `진행 관리` 하나, tab 내용은 청주 제조 현황과 같은 표·모바일 카드인지 확인한다.
- [ ] 프로젝트 코드의 대소문자와 내부 연속 공백이 desktop/mobile 목록·상세에서 그대로 보이는지 확인한다.
- [ ] 청주 프로젝트 목록·등록·상세가 기존처럼 동작하는지 확인한다.

상태: `사용자 검수 대기 — Change 004 현재 화면 검수 및 마지막 오산 일괄 검수`.

## 9. 필수 산출물 상태

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/osan-project-001-implementation-report.md` | `IMPLEMENTED_AWAITING_USER_VALIDATION` |
| SOP | 이 보고서 6절 | `COMPLETE_LOCAL_SCOPE` |
| 사용자 안내 | 이 보고서 7절 | `COMPLETE_LOCAL_SCOPE` |
| Roadmap update | `docs/00-product-roadmap.md`, `tasks/osan-project-001.md` | `IMPLEMENTED_AWAITING_USER_VALIDATION` |
| 사용자 검수 checklist | 이 보고서 8절 | `PENDING_CURRENT_SCREEN_REVIEW_AND_FINAL_BATCH_BY_USER` |

## 10. 블로그 초안

### 1. 해결한 업무 문제

오산 사업부는 청주의 Item·담당자·포장·FAT·18단계 workflow 없이 8개 정보만으로 프로젝트를 등록하고 수량별 진행 대상을 준비해야 했다. 기존 공통 생성 경로를 열지 않고 오산 전용 목록·등록·상세와 저장 profile을 추가해 이 차이를 분리했다.

### 2. 기술적 결정과 검토한 대안

기존 `projects` 식별 row는 공유하되 profile로 청주와 오산을 나누고, 오산 업무 데이터는 전용 target·step·operation·event table에 저장했다. 기존 프로젝트 API를 조건 분기로 확장하는 대안은 청주 workflow와 승인되지 않은 후속 row 생성 위험이 있어 채택하지 않았다. 오산 코드 unique는 애플리케이션 사전 조회가 아니라 DB partial unique로 보장해 경쟁 요청도 같은 계약을 따른다.

### 3. 시행착오 및 폐기한 접근

처음에는 `BusinessSchemaVersion`을 최신 migration 번호로 해석했지만 실제 계약과 test를 대조해 `0086` identity binding 값임을 확인하고 공통 설정 변경을 모두 되돌렸다. Development React Strict Mode가 취소한 첫 read를 browser request failure로 집계한 mock 실행은 production build/preview 검증으로 바꿨다. Full-stack test가 다중 사업부 선택 UI를 기대한 시안은 single OSAN membership 사용자가 바로 오산 홈으로 들어가는 기존 UX에 맞춰 보정했다. Parent review가 찾은 hidden operation conflict 무표시·영구 재시도 문제는 visible 8개 field만 field error로 매핑하고 operation conflict 안내와 새 operation ID를 발급하는 방식으로 보정했다. `Project.Create`만 확인하던 오산 등록 UI는 Backend와 동일하게 `projects.read`도 요구하도록 좁혔다. 전체 Backend 첫 run은 신규 대상·단계 relation 감사 분류 누락 1건으로 581/582였고, Parent가 승인한 0087 trigger와 post-0083 tracked relation 분류로 보정했다. 다음 전체 run은 실행 중 소스의 global audit 관계 기대값을 94에서 96으로 보정해 이미 적재된 test binary에서만 같은 assertion 1건이 남아 581/582였으며 전용 자원은 정상 정리됐다. 최신 Release를 다시 단독 실행한 최종 run은 582/582로 통과했다. Backend 장시간 suite와 동시에 실행한 Frontend 전체 test 두 시도는 기존 AuditPage/App 비동기 test가 서로 다른 위치에서 한 번씩 timeout됐고, 각 실패 test를 단독 실행하면 통과했다. Backend 검증 종료 후 Frontend 전체를 단독 재실행해 291/291로 최종 통과했다. Fresh final verifier가 발견한 replay access 누락과 코드 내부 공백 표시 축약은 endpoint scope 재검사와 코드 전용 `break-spaces`로 보정했다. 첫 revoke targeted 실행은 test 사용자에게 기본 `Project.Read.All`이 있어 예상 `403` 대신 기존 계약대로 `200`이었고, 시나리오에서 read-all을 일시 제거·복원하도록 고쳐 scoped access와 read-all 동작을 각각 검증했다. 최종 Backend 582/582와 Frontend 291/291, mock 1/1이 다시 통과했다.

### 4. 사용자 검수 결과와 남은 항목

자동 검증에서는 오산 등록·목록·상세, 값 보존, 수량별 대상과 7단계, 동시성·idempotency·rollback, 청주 불변을 local 합성 환경에서 확인했다. Change 002의 별도 오산 UI는 Change 003에서 제거했고, Change 004에서 청주와 오산이 목록·요약·현황의 같은 React 표시 컴포넌트를 직접 사용하도록 통합했다. 같은 run의 desktop/mobile 증빙 8개를 Parent와 fresh verifier가 눈으로 확인했으며 현재 화면은 local synthetic server에서 사용자 검수 대기다. 전체 업무 검수는 마지막 오산 일괄 검수로도 추적한다. Persistent UAT, 실제 provider, push·PR·merge·main 반영은 수행하지 않았다.
