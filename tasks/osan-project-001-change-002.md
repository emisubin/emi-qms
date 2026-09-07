# TASK-OSAN-PROJECT-001 Change 002 — 청주형 목록·상세 화면 정렬

## 1. 승인·Gate·기준선

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeStatus: `IMPLEMENTED_AWAITING_USER_VALIDATION`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-PROJECT-001`
- roadmapExpectedTaskId: `TASK-OSAN-PROGRESS-001`
- roadmapNextGate: `TASK-OSAN-PROGRESS-001_IMPLEMENTATION_APPROVAL`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- roadmapOverrideSource: `USER_EXPLICIT_UI_CHANGE_AND_SERVER_REQUEST_2026-09-07`
- samePurposeMatchCount: 1
- reuseExistingTask: true
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_UI_CHANGE_AND_SERVER_REQUEST_2026-09-07`
- localCommitApproved: true
- localCommitPolicySource: `USER_STANDING_INSTRUCTION_2026-09-07`
- userValidationStatus: `PENDING_CURRENT_SCREEN_REVIEW`
- persistentUatMutationApproved: false
- providerMutationApproved: false
- gitPublicationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- taskBranch: `feat/task-osan-project-001-project-registration`
- baselineSha: `013298ae5058ec9435333956f57ee3917dd69d04`

사용자는 Task 4보다 먼저 현재 오산 프로젝트 화면을 청주 프로젝트 화면과 같은 정보 구조로 수정하고 검수 서버를 열도록 지시했다. 직전 확인에서 오산 상세의 부서 탭은 7개 진행 단계를 각각 탭으로 만드는 것이 아니라 기존 청주 상세의 제조 탭 자리에 `진행 관리` 탭 하나를 두는 것으로 확정했다. 이 명시적 지시는 같은 Task의 화면 change를 우선 수행하는 순서 변경 승인으로 기록한다.

## 2. Purpose identity

- 업무 목표: 오산 프로젝트 목록과 상세를 청주 사용자가 익숙한 행 목록·상세 탭 구조로 맞춘다.
- Root Finding: 현재 오산 목록은 카드 grid이고 상세는 기본 정보와 대상 카드가 바로 이어져 청주 프로젝트 화면의 탐색 구조와 다르다.
- 변경·검증 경계: 오산 전용 목록·상세 React markup, 전용 반응형 CSS, 관련 component/browser test와 Task 문서만 변경한다.
- 보존할 불변조건: 오산 8개 입력, 자유 제품명, 수량별 대상과 7단계, 권한·API·DB·idempotency·코드 공백 보존 계약을 바꾸지 않는다. Pending·중단·보류·취소와 실제 진행 mutation을 추가하지 않는다. 청주 화면과 API 동작을 바꾸지 않는다.
- 예상 산출물: 청주형 오산 행 목록, 청주형 상세 header·요약·단일 `진행 관리` 탭, desktop·390px 자동 검증과 screenshot, 구현 보고 갱신, local commit, 검수용 로컬 서버.

## 3. GPT-6 구현 방향

1. 오산 목록은 청주 데스크톱 목록처럼 column header 아래 프로젝트 하나를 한 행으로 표시한다. 오산 계약에 맞춰 프로젝트명·코드, 거래처, 제품명, 수량, 납기일, 상태와 열기 affordance를 사용한다.
2. 좁은 화면은 청주 모바일 목록의 읽기 순서와 터치 영역을 따르되 페이지 가로 overflow를 만들지 않는다.
3. 오산 상세는 청주 상세처럼 breadcrumb/뒤로가기, 프로젝트 제목 header, 기본 정보 요약, sticky department tab, tab content 순서로 구성한다.
4. 오산의 department tab은 `진행 관리` 하나만 표시하며 선택 상태와 접근 가능한 tab semantics를 제공한다. 이 탭 내용에 기존 수량별 대상과 각 7단계 `시작 전` 표시를 배치한다.
5. 청주 전용 workflow·생산관리·설계·구매·품질·물류·영업 탭과 Pending/hold/cancel action은 오산에 노출하지 않는다.
6. 기존 오산 등록 화면과 Backend는 변경하지 않는다.

## 4. Exact allowlist

- `frontend/src/App.tsx`
- `frontend/src/styles.css`
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `tasks/osan-project-001-change-002.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`
- `docs/00-product-roadmap.md`

Allowlist 밖 파일이 필요하면 구현자는 변경하지 않고 Parent에 보고한다. Backend, database migration, API contract, dependency와 lockfile은 이번 Change에서 수정하지 않는다.

## 5. 완료 조건과 테스트

- Desktop 목록에 column header와 프로젝트별 단일 행이 있고 클릭하면 해당 상세로 이동한다.
- 목록의 코드 내부 공백, 프로젝트 정보와 상태가 정확히 표시된다.
- 상세에 청주형 header·기본 정보 요약과 `진행 관리` 탭 하나만 보인다.
- `진행 관리` 탭에는 N개 대상과 대상별 7단계가 순서대로 표시되며 기존 상태 의미를 유지한다.
- 등록→상세→목록 흐름, loading/empty/error/forbidden과 권한 조건이 회귀하지 않는다.
- `frontend/tests/OsanProjectRegistration.test.tsx`를 통과한다.
- Frontend 전체 unit/component, lint, typecheck와 production build를 통과한다.
- Production build 기반 mock browser에서 desktop과 390px의 목록·상세, tab semantics, console error 0, request failure 0, page horizontal overflow 0을 확인하고 privacy-safe screenshot을 남긴다.
- Parent GPT-6가 실제 diff와 테스트를 검토하고 fresh GPT-6 High verifier가 read-only 독립 검증해 open P0/P1/P2가 0이어야 한다.

## 6. 게시·runtime 경계

필수 검증과 review를 통과한 exact allowlist는 standing instruction에 따라 별도 질문 없이 local commit한다. Push, PR, merge, `main`, Persistent UAT, 실제 Azure DB/provider는 승인 범위 밖이다. 검수 서버는 test-owned synthetic data로 열며 실제 DB 적용으로 기록하지 않는다.

## 7. 구현·검증 결과

- Sol xhigh 구현자는 exact product/test allowlist 4개 파일만 수정했다. 요청 모델은 기록됐고 실제 실행 모델은 도구에서 별도로 반환하지 않아 `NOT_REPORTED`다.
- Desktop 목록은 7개 column header와 프로젝트별 단일 행, keyboard Enter/Space 이동을 제공한다. 860px 이하에서는 청주형 mobile project card로 전환한다.
- 상세는 desktop breadcrumb와 mobile back, 제목, 8개 기본 정보 요약, sticky tab bar와 tab content 순서로 정렬했다. Department tab은 `진행 관리` 하나이며 기존 N개 대상과 각 7단계를 그 panel 안에 유지한다.
- 생성 직후 Backend의 기술 상태 `Active`는 이 생성 전용 slice에서 사용자 의미인 `시작 전`으로 표시한다. 실제 진행 집계와 `진행 중` 전환은 Task 4 범위다.
- Desktop/mobile 목록·상세의 프로젝트 코드는 `white-space: break-spaces`와 `text-transform: none`으로 대소문자·내부 연속 공백을 보존한다.
- 오산 전용 component test `8/8`, 전체 Frontend `36 files / 292 tests`, lint 오류 0과 기존 `main.tsx` warning 1, typecheck, production build, mock Playwright Chromium `1/1`이 통과했다. 전체 test 첫 실행의 기존 AuditPage async-load flake는 제품 변경 없이 재실행해 전체 통과했다. Build의 기존 chunk-size warning은 유지된다.
- Mock browser는 desktop/390px 목록·상세, 단일 tab semantics, 정확한 코드와 computed style, `시작 전` 상태, console error 0, request failure 0, page horizontal overflow 0을 확인했다. Screenshot 4개는 `frontend/test-results/osan-project-registration-mock/` 아래 test-owned untracked artifact이며 stage하지 않는다.
- Parent GPT-6가 실제 diff와 screenshot을 검토했다. 첫 fresh GPT-6 High 검증의 `OSAN-UI-VERIFY-01/02` 두 P2를 보정한 뒤 새 fresh GPT-6 High 재검증에서 `PASS / GO`, open P0/P1/P2/P3 `0/0/0/0`을 확인했다. 요청 모델은 기록됐고 actual model은 도구에서 별도로 반환하지 않아 `NOT_REPORTED`다.

| Finding | 심각도 | 상태 | 원인·영향과 해소 |
| --- | --- | --- | --- |
| `OSAN-UI-VERIFY-01` | P2 | `RESOLVED` | Mobile code에 전역 uppercase style이 적용되고 detail hero는 내부 공백도 접었다. 전용 code class와 `text-transform: none`, `break-spaces`를 네 render 위치에 적용하고 browser computed style로 확인했다. |
| `OSAN-UI-VERIFY-02` | P2 | `RESOLVED` | 생성 직후 모든 target/step이 `시작 전`인데 기술 상태 `Active`를 `진행 중`으로 표시했다. 현재 slice에서 `Active`를 `시작 전`으로 표시하고 unit/browser test를 보정했다. |
| `OSAN-UI-RECORD-01` | P2 | `RESOLVED` | 구현 보고가 component test의 클릭 이동을 keyboard 이동 검증으로 넓게 기록했다. 실제 test evidence에 맞춰 클릭 이동으로 정정했다. |
| `OSAN-UI-RECORD-02` | P3 | `RESOLVED` | Change 001 baseline/digest와 Change 002 현재 상태, 파일 목록·검수 상태가 한 보고서에서 혼재했다. Change별 metadata와 현재 화면 검수·마지막 일괄 검수 상태를 분리해 정렬했다. |

Persistent UAT·실제 Azure DB/provider·Backend full-stack은 이번 frontend-only Change에서 실행하거나 변경하지 않았다. Backend와 DB 계약은 Change 001의 통과 증거를 유지한다.
