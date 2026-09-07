# TASK-OSAN-PROJECT-001 Change 005 — 청주 프로젝트 목록 전체 구성 공용화

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `USER_VALIDATION_COMPLETE`
- userValidationStatus: `COMPLETE`
- userValidationSource: `USER_EXPLICIT_2026-09-07_CURRENT_TASK_VALIDATION_COMPLETE`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-PROJECT-001`
- roadmapExpectedTaskId: `TASK-OSAN-PROGRESS-001`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 1
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- roadmapOverrideSource: `USER_EXPLICIT_VISUAL_PARITY_CORRECTION_2026-09-07`
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_VISUAL_PARITY_CORRECTION_2026-09-07`
- localCommitApproved: true
- localCommitPolicySource: `USER_STANDING_INSTRUCTION_2026-09-07`
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- persistentUatMutationApproved: false
- providerMutationApproved: false
- gitPublicationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- taskBranch: `feat/task-osan-project-001-project-registration`
- baselineSha: `8022fa2af03fd85d0286426ba28c028f0b1de9ca`
- implementationOwnerObserved: `NOT_REPORTED`
- verificationOwnerObserved: `NOT_REPORTED`
- finalVerificationResult: `PASS_GO`
- finalOpenFindingCount: `P0 0 / P1 0 / P2 0 / P3 0`
- productTestReviewedDigest: `eda2fa79b91c40e00a1a216b96159dacb874f98dcd9c61fe7d323f2ff05ad6fd`

## 2. Purpose identity

- 업무 목표: 오산 프로젝트 목록의 행뿐 아니라 page header, 검색·기간 filter, 요약, 상태 tab과 목록 배치까지 청주 프로젝트 목록의 실제 구성과 시각 규칙을 사용한다.
- Root Finding: Change 004는 공용 표시 영역을 목록 행·카드로 한정하고 청주 목록의 상위 page composition을 의도된 기능 차이로 제외했다. 사용자의 “청주 프로젝트 목록 페이지 그대로” 요구보다 비교 범위를 좁혀 전체 화면이 달라졌다.
- 변경·검증 경계: Frontend 목록 page composition, 오산 read-only 검색·기간 filter·요약·상태 분류, component/browser test와 Task 기록.
- 보존할 불변조건: 오산 등록 API·DB·권한·8개 입력·7단계와 단일 `진행 관리`는 유지한다. Pending, 중단, 보류, 취소, 삭제 보관함, Excel upload/export, 선택 export와 청주 업무 mutation은 오산에 추가하지 않는다.
- 예상 산출물: 청주와 오산이 같은 목록 page frame과 표시 컴포넌트를 사용하고, 오산 데이터에 맞는 비파괴 조회 도구만 연결한 desktop/mobile 화면과 paired visual evidence.

## 3. 구현 방향

1. 청주 목록의 page header, desktop/mobile filter, KPI grid, status tabs와 목록 배치 순서를 실제 공용 React presentation으로 추출하고 청주·오산이 직접 사용한다.
2. 오산 제목·등록 action은 같은 header slot에 넣고 검색어·납기 기간은 이미 불러온 오산 목록을 client-side로 filter한다. Backend/API를 확장하지 않는다.
3. 오산 KPI와 status tab은 현재 계약으로 계산 가능한 전체·시작 전·완료 범위만 제공한다. Pending·보류·취소·삭제·선택 export·Excel control을 빈 기능이나 disabled control로 흉내 내지 않는다.
4. 목록 결과가 없는 상태는 최초 empty와 filter 결과 empty를 구분하고 청주와 같은 filter 초기화 흐름을 제공한다.
5. 1440×900과 390×844에서 청주·오산 전체 목록 page를 같은 current-branch run으로 캡처한다. 공용 header/filter/KPI/tab/list frame의 DOM·class·computed style·순서와 실제 화면을 함께 확인한다.

## 4. Exact allowlist

- `frontend/src/App.tsx`
- `frontend/src/styles.css` (공용 composition에 필요한 경우만)
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `tasks/osan-project-001-change-005.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`
- `docs/00-product-roadmap.md`

Backend, API, database, migration, dependency, lockfile, 실제 provider와 Persistent UAT는 변경하지 않는다.

## 5. 완료 조건

- 청주와 오산이 목록 page의 같은 실제 composition component를 직접 사용한다.
- Desktop은 header → filter → KPI → status tab → 기능별 허용 도구 → 목록 순서를 유지하고 mobile은 청주와 같은 filter sheet·card 전환을 사용한다.
- 오산 검색·기간 filter, reset, 상태 tab과 filtered empty 복구가 동작한다.
- 청주의 검색·기간 filter, KPI, 상태 tab, 선택/export, 삭제, Excel과 기존 목록 동작이 회귀하지 않는다.
- 오산에 Pending·중단·보류·취소·삭제·Excel·선택 export와 승인되지 않은 mutation이 나타나지 않는다.
- 같은 viewport의 전체 목록 screenshot에서 공용 page frame의 배치·간격·테두리·반응형 전환을 Parent가 직접 비교한다.
- 집중 component, 전체 Frontend, lint, typecheck, build, paired mock browser와 `git diff --check`를 통과한다.
- Parent review와 fresh GPT-6 High 독립 검증에서 open P0/P1/P2가 0이다.

## 6. 게시 경계

검증과 review를 통과한 exact allowlist는 별도 질문 없이 local commit한다. Push, PR, merge, `main`, Persistent UAT와 실제 provider는 승인 범위 밖이다.

## 7. 구현 결과

- `ProjectListPageComposition`을 만들고 청주와 오산 목록이 같은 page header, desktop/mobile filter, KPI, 상태 tab, 기능별 tool slot과 목록 배치 순서를 직접 사용하게 했다.
- 오산 검색은 이미 한 번 조회한 목록에서 프로젝트 Title·코드·거래처·제품명을 찾고 납기 기간, 초기화, `전체·시작 전·완료` 상태 filter를 제공한다. 검색 때문에 API를 추가 호출하거나 데이터를 변경하지 않는다.
- 오산은 3개 KPI와 3개 상태 tab만 사용한다. 승인되지 않은 Pending·보류·취소·삭제 보관함·Excel·선택 내보내기·mutation은 추가하지 않았다.
- 청주는 기존 server 조회, 6개 KPI·6개 tab, 선택·Excel·삭제·Pending과 행 이동을 유지한다.
- Desktop과 mobile 모두 생성 action을 같은 header 위치에 둔다. Mobile에서 공용 action container가 숨김 class를 상속해 생성 버튼이 사라지던 문제를 고쳐 청주·오산 모두 `+ 프로젝트`가 보이게 했다.
- 오산 filtered empty는 `필터 초기화`, 최초 empty는 `신규 프로젝트`로 복구한다. Mobile 안내 문구는 오산에 없는 병목·Pending을 말하지 않고 납기와 프로젝트 선택을 안내한다.
- `frontend/src/styles.css`는 수정할 필요가 없었다. 제품·test 실제 변경은 `frontend/src/App.tsx`, 두 component test와 paired browser test 네 파일이다.

## 8. 검증 결과

- 집중 component: `2 files / 98 tests PASS`.
- Frontend 전체: `36 files / 293 tests PASS`.
- Lint: 오류 0. 기존 `frontend/src/main.tsx` Fast Refresh warning 1만 남았다.
- Typecheck와 production build: `PASS`. 기존 chunk-size warning만 남았다.
- Paired production-preview Chromium: `1/1 PASS`. 청주·오산 목록·상세를 1440×900과 390×844에서 캡처하고 console error, request failure, unexpected request와 horizontal overflow가 없음을 확인했다.
- Parent가 목록 최종 screenshot과 열린 local 화면을 청주 화면과 직접 비교했다. 두 목록은 같은 frame과 읽기 순서를 사용하며 기능 수에 따른 KPI·tab·tool 개수만 다르다.
- Fresh GPT-6 High read-only verifier가 실제 diff, test와 screenshot 8개를 검토해 `PASS / GO`, open P0/P1/P2/P3 `0/0/0/0`을 반환했다. 요청 모델은 `GPT_6_ASTRA_HIGH`, 관측 모델은 `NOT_REPORTED`다.
- `git diff --check`: `PASS`.

## 9. Root cause와 재발 방지

Change 004는 “같은 화면”의 비교 단위를 목록 행·카드, 상세 요약과 부서 현황으로 좁혔다. 청주의 검색·기간 filter, KPI와 상태 tab은 업무 기능 차이라고 분류해 page composition 자체를 공용화하지 않았다. 자동 test도 선택한 공용 영역의 DOM·geometry만 비교했으므로 사용자가 보는 전체 목록 페이지의 차이를 통과시켰다.

앞으로 화면 동일성 요청은 route 전체를 `header → filter → summary → navigation → tools → content → empty/loading/error` 단위로 먼저 inventory한다. 지원하지 않는 기능은 control을 복제하지 않되, 남은 영역은 같은 page composition을 직접 사용한다. Desktop/mobile 양쪽 전체 screenshot을 같은 run에서 캡처하고 사람이 페이지 전체 순서와 주요 action 가시성을 확인한 뒤 완료로 판정한다. 기능 차이로 제외한 항목은 비교 전에 exact 목록으로 기록한다.

이 재발 방지 기준은 더 넓은 화면 범위를 검토하고 공유하도록 구현자의 완료 의무를 넓힌다. 사용자 승인 범위, mutation, Git 게시, 운영 적용 권한은 확대하지 않는다.

## 10. 사용자 검수 상태

상태: `COMPLETE — USER_EXPLICIT_2026-09-07_CURRENT_TASK_VALIDATION_COMPLETE`.

사용자는 2026-09-07 Change 005가 반영된 현재 Task 3 화면의 검수 완료를 명시했다. 이 완료는 Task 3의 프로젝트 등록·목록·상세와 승인된 청주형 전체 page composition에 한정한다. 별도로 요청한 Task 2 selector 가시성 Change 002, 아직 구현하지 않은 Task 4~6, Push·PR·merge·Persistent UAT·provider·운영 적용을 완료로 만들지 않는다.
