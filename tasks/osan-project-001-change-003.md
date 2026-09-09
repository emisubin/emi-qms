# TASK-OSAN-PROJECT-001 Change 003 — 청주 UI·UX 직접 재사용

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_AWAITING_USER_VALIDATION`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-PROJECT-001`
- roadmapExpectedTaskId: `TASK-OSAN-PROGRESS-001`
- roadmapNextGate: `TASK-OSAN-PROGRESS-001_IMPLEMENTATION_APPROVAL`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- roadmapOverrideSource: `USER_EXPLICIT_CHEONGJU_UI_REUSE_CORRECTION_2026-09-07`
- samePurposeMatchCount: 1
- reuseExistingTask: true
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_CHEONGJU_UI_REUSE_CORRECTION_2026-09-07`
- localCommitApproved: true
- localCommitPolicySource: `USER_STANDING_INSTRUCTION_2026-09-07`
- userValidationStatus: `PENDING_CURRENT_SCREEN_REVIEW`
- persistentUatMutationApproved: false
- providerMutationApproved: false
- gitPublicationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- verificationOwnerObserved: `NOT_REPORTED`
- verificationResult: `PASS_GO`
- verificationOpenFindings: `P0 0 / P1 0 / P2 0 / P3 0`
- taskBranch: `feat/task-osan-project-001-project-registration`
- baselineSha: `7ec8dbc64830713b5dad0fdba8bd47de5773c2bf`

사용자는 Change 002 화면이 청주와 비슷한 오산 전용 UI를 새로 만든 결과여서 변경이 과도하다고 확인했다. `UI, UX는 변경하거나 새로 만들지 말고 청주 것을 그대로 가져온다`는 보정 방향과 구현을 명시적으로 승인했다.

## 2. Purpose identity

- 업무 목표: 오산 목록·상세의 별도 시각·상호작용 설계를 제거하고 청주 프로젝트 화면의 기존 presentation contract를 직접 재사용한다.
- Root Finding: Change 002가 `osan-project-list-*`, `osan-project-summary`, `osan-project-target-*` 전용 markup과 CSS를 추가해 청주 화면과 별도 UI·UX를 만들었다.
- 변경·검증 경계: 오산 목록·상세 React presentation, 관련 CSS 제거, component/browser test와 Task 기록만 수정한다.
- 보존할 불변조건: 오산 API·DB·권한·등록 양식·코드 공백 보존, 수량별 대상과 7단계 데이터 계약은 유지한다. 오산에는 `진행 관리` 탭 하나만 보이며 Pending·중단·보류·취소와 청주 전용 업무 기능은 추가하지 않는다.
- 예상 산출물: 청주와 같은 page shell, desktop row/mobile card, detail header/summary/tab/content presentation을 쓰는 오산 화면, 회귀 테스트, local commit과 검수 서버.

## 3. GPT-6 구현 방향

1. Change 002에서 추가한 오산 전용 시각·반응형 CSS를 제거한다. 프로젝트 코드의 대소문자와 내부 연속 공백을 보존하는 최소 데이터 표시 규칙만 유지할 수 있다.
2. 목록은 청주의 `project-list`, `project-list-table project-list-desktop`, `project-list-head`, `project-list-row`, `project-list-cards project-list-mobile`, `project-list-card` 구조와 동일한 DOM 순서·class·반응형 전환·행 열기 상호작용을 사용한다.
3. 목록의 데이터 열은 오산 계약에 맞게 프로젝트명, 거래처, Code, 제품명, 수량, 납기일, 상태, 진행률로 매핑한다. 진행률은 Task 4 전까지 목록 계약상 `0%`인 읽기 전용 표시다. 청주에만 존재하는 선택 내보내기, Pending, 삭제 보관함 기능을 흉내 내거나 추가하지 않는다.
4. 상세 outer shell은 청주의 `page-surface`, breadcrumb/mobile back, `page-header`/`mobile-detail-hero`, 요약, `project-department-tabs`, `project-detail-tab-content` 구조와 기존 CSS를 그대로 사용한다.
5. 기본정보와 진행 내용은 청주의 기존 `detail-grid`, `subsection project-department-section`, `subsection-header`, `project-department-metrics`, `project-panel-status-table`/`project-panel-status-cards`, progress/status presentation을 재사용한다. 새 오산 전용 카드·간격·색·border·mobile UX를 만들지 않는다.
6. 청주의 제조 탭 presentation을 오산 데이터에 맞춰 쓰되 사용자-facing 탭과 section 명칭은 `진행 관리`로 표시한다. 단일 탭에서 대상별 현재 단계와 완료 수/7을 요약한다. 7단계 원본·순서 계약은 API/DB와 기존 테스트에 유지하며 이 요약 화면에 별도 단계 카드나 전체 단계 목록을 추가하지 않는다.
7. 청주 화면 자체의 동작과 스타일은 바꾸지 않는다. 공유 코드 추출이 필요하면 청주 render 결과와 interaction이 동일하다는 회귀 테스트를 유지한다.

## 4. Exact allowlist

- `frontend/src/App.tsx`
- `frontend/src/styles.css`
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `tasks/osan-project-001-change-003.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`
- `docs/00-product-roadmap.md`

Implementer는 앞의 product/test 4개 파일만 수정한다. Parent가 Task 기록과 상태를 갱신한다. Backend, API contract, database, migration, dependency와 lockfile은 변경하지 않는다.

## 5. 완료 조건과 테스트

- 오산 화면이 청주와 동일한 기존 class와 layout component contract를 사용하며 Change 002의 전용 UI CSS가 제거된다.
- Desktop 목록은 청주와 같은 header/row 정렬, hover/focus와 행 열기 동작을 제공한다.
- 390px 목록은 청주와 같은 mobile card 구조와 정보 읽기 순서를 제공한다.
- 상세는 청주와 같은 page shell, 요약, sticky tab과 department section layout을 제공한다.
- 상세 탭은 `진행 관리` 하나만 보이고 청주 제조 탭과 같은 표 또는 모바일 카드에서 대상별 현재 단계와 완료 수/7이 표시된다. 7단계 전체를 다시 나열하는 별도 UI는 없다.
- Pending·보류·중단·취소와 청주 전용 탭·작업 버튼은 오산에 나타나지 않는다.
- 코드 대소문자·내부 연속 공백, loading/empty/error/forbidden과 등록→상세→목록 동작이 회귀하지 않는다.
- 오산 component test, 관련 청주 목록·상세 test, Frontend 전체 unit/component, lint, typecheck와 build를 통과한다.
- Mock browser desktop/390px에서 목록·상세, 단일 tab semantics, console error 0, request failure 0과 page horizontal overflow 0을 확인한다.
- Parent GPT-6 실제 diff review와 fresh GPT-6 High 독립 검증에서 open P0/P1/P2가 0이어야 한다.

## 6. 게시·runtime 경계

검증과 review를 통과한 exact allowlist는 별도 질문 없이 local commit한다. Push, PR, merge, `main`, Persistent UAT, 실제 Azure DB/provider는 승인 범위 밖이다. 검수 서버는 synthetic data만 사용한다.

## 7. 구현·검증 결과

- Sol xhigh 구현자는 product/test allowlist 4개 파일만 수정했다. 요청 모델은 `gpt-5.6-sol` xhigh이며 도구가 실제 모델을 별도로 반환하지 않아 observed model은 `NOT_REPORTED`다.
- Change 002의 `osan-project-list-*`, `osan-project-summary`, `osan-project-target-*` 전용 presentation과 반응형 CSS를 제거했다. 프로젝트 코드 대소문자·내부 연속 공백 보존을 위한 최소 `.osan-project-code-value` 규칙만 유지한다.
- 목록은 청주 `project-list-*` desktop/mobile 구조와 8열 grid를 사용한다. 상세는 청주 `page-surface`, summary/detail grid, department tab/content와 제조 현황의 지표·desktop table/mobile card 구조를 사용한다.
- 오산 업무 차이는 제품명·수량 매핑, `진행 관리` 단일 탭, 8개 입력정보, 대상별 현재 단계·완료 수/7과 금지 기능 제외로 한정했다. 청주 코드·공통 CSS·API·DB·Backend·권한·mutation은 변경하지 않았다.
- 오산 component `8/8`, 선택한 청주 목록·상세·제조 회귀 `4/4`, Frontend 전체 `36 files / 292 tests`, typecheck, build, diff check가 통과했다. Lint는 오류 0과 기존 `main.tsx` Fast Refresh warning 1이다. Parent 전체 test 첫 실행의 기존 품질검사 async-load flake 1건은 단독 `4/4` 및 제품 변경 없는 전체 재실행 `292/292`로 통과했다.
- Production preview mock Chromium `1/1`에서 desktop·390px 목록/상세, 단일 탭, 8개 열·필드, 대상별 `입고검사` 현재 단계와 `0/7`, 금지 action 부재, 코드 공백 보존, horizontal overflow 0, console error 0, request failure 0을 확인했다. Screenshot 4개는 ignored test output이다.
- Parent GPT-6가 실제 diff와 screenshot을 검토했다. Fresh GPT-6 High verifier가 찾은 비시각적 표 semantics P2 한 건을 보정한 뒤 `PASS / GO`, 열린 제품 P0/P1/P2/P3 `0/0/0/0`을 반환했다.

| Finding | 심각도 | 상태 | 원인·영향과 해소 |
| --- | --- | --- | --- |
| `OSAN-UI-REUSE-A11Y-01` | P2 | `RESOLVED` | 오산 desktop 진행 표에 row만 있고 columnheader/cell 역할이 없어 화면읽기 구조가 불완전했다. 5개 header와 행별 5개 cell 역할을 추가하고 component/browser assertion으로 확인했다. |
| `OSAN-UI-REUSE-DOC-01` | P3 | `RESOLVED` | 최초 Change 003 완료 조건이 최신 사용자 지시와 달리 7단계 전체 표시를 요구했다. 청주 제조 요약과 같은 현재 단계·완료 수/7 표시로 정정하고 7단계 원본·순서 검증은 API/DB 계약에 유지했다. |
