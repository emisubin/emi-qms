# TASK-HOME-002 Change 001 — experiment fast-track과 구현 경계

## 1. Task Identity Gate

- proposedTaskId: `TASK-HOME-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UX-001 A2`
- roadmapNextGate: `TASK_UX_001_A2_FABLE_2_PASS_PLANNING`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-HOME-002`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

사용자의 “먼저 홈 화면 개선, 디자인 수정부터” 요청은 현재 experiment의 권장 다음 범위인 `TASK-UX-001 A2`보다 `TASK-HOME-002`를 먼저 수행하는 명시적 재정렬 승인으로 기록한다. 완료된 `TASK-HOME-001` 4개 widget과 `DESIGN-001` 전체 화면 통일을 재기획하지 않고, 신규 개인화 Home·profile shell 능력만 추가한다.

## 2. 기준선과 기획 target

- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `1235d5e572c0e958e03fcb24cff7e3318cd20f12`
- representativeMainCommit: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/home-002-interview.md`
- firstPlanningSource: `tasks/home-002-planning.md`
- codexReviewSource: `tasks/home-002-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/28-personalized-home-profile-plan.md`

## 3. 구현·Git 경계

- planningApproved: `true` — Codex review를 읽는 Fable 2차 기획 한정
- implementationApproved: `true` — 2차 기획이 review resolution을 반영하고 `openBlockingDecisionCount: 0`인 조건의 experiment 구현
- userValidationCompleted: `false`
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 4. 포함 범위

- 공통 login user identity, profile account menu/sheet, 본인 photo upload·fallback·logout.
- dev selector sidebar/drawer footer 이동, desktop full-height sidebar, 중복 자재 top shortcut 제거.
- 부서별 Home 핵심 metric과 기존 source deep link.
- reference 기반 밝은 compact shell·Home layout을 EMI red·white로 재구성.
- Backend/Frontend/필요한 additive migration, 권한·validation·audit, isolated test, desktop/mobile screenshot.

## 5. 제외 범위

- 기존 HOME-001 4개 widget 재구현·삭제.
- 모든 업무 페이지 정보 구조 전면 재설계.
- Graph profile photo, 관리자 대리 변경, 실제 provider와 운영 storage.
- 대표 repo·`main`·Persistent UAT·runtime handover·push·PR·merge.

## 6. Fable 사용량 기록

Claude `/usage`는 Repository mutation 없이 `bash scripts/report-claude-usage.sh`로 측정한다. Reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 43% 사용 / 57% 잔여 / 00:00 KST 초기화 | 12% 사용 / 88% 잔여 / 07-25 08:00 KST 초기화 | 24% 사용 / 76% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 43% 사용 / 57% 잔여 / 00:00 KST 초기화 | 12% 사용 / 88% 잔여 / 07-25 08:00 KST 초기화 | 24% 사용 / 76% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 52% 사용 / 48% 잔여 / 23:59 KST 초기화 | 13% 사용 / 87% 잔여 / 07-25 07:59 KST 초기화 | 26% 사용 / 74% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 52% 사용 / 48% 잔여 / 23:59 KST 초기화 | 13% 사용 / 87% 잔여 / 07-25 07:59 KST 초기화 | 26% 사용 / 74% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 0% 사용 / 100% 잔여 / 05:50 KST 초기화 | 14% 사용 / 86% 잔여 / 07-25 07:59 KST 초기화 | 28% 사용 / 72% 잔여 / 07-25 07:59 KST 초기화 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 완료됐다. model 378초, stdout 28,259 bytes, stderr 0이며 `tasks/home-002-planning.md`에 Fable 원문을 byte-for-byte 저장했다.

2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 완료됐다. model 147초, stdout 17,869 bytes, stderr 0이며 `docs/28-personalized-home-profile-plan.md`에 Fable 원문을 byte-for-byte 저장했다.

Task 종료 시 `cleanup` runner는 `FABLE_TASK_SESSION_CLEANED`를 반환했고 private session·transcript 각 1개를 제거했다(`sessionsRemoved=1`, `transcriptsRemoved=1`, `missing=0`).
