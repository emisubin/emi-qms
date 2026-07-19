# TASK-EXPORT-001 Change 003 — 사용자 컬럼 선택 실행 계약

## Canonical 연결

- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-003`
- fableSessionKey: `TASK-EXPORT-001-COLUMN-PICKER`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `f78ec331b63fe3c3043d6d707bab4d08bd138ceb`
- interviewSource: `tasks/export-001-column-picker-interview.md`
- firstPlanningSource: `tasks/export-001-column-picker-planning.md`
- codexReviewSource: `tasks/export-001-column-picker-review.md`
- secondPlanningTargetCandidate: `docs/35-selected-export-column-picker-plan.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/35-selected-export-column-picker-plan.md`

## Task Identity Gate

- proposedTaskId: `TASK-EXPORT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-EXPORT-001`
- roadmapNextGate: `OPTIONAL_COLUMN_PICKER_USER_REQUEST`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPORT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

## 실행·안전 경계

- Fable 1차 planning → Codex 내용 review → Fable 2차 planning → Codex 구현·검증·screenshot·local commit.
- 기존 20개 screen 선택 export와 server permission·scope·formula·resource·audit 계약을 보존한다.
- 사용자-facing interview·중간 승인은 standing experiment rule로 생략하고 비차단 권장안을 자동 채택한다.
- 대표 repo·`main`·origin·Persistent UAT·실제 provider·push·PR·merge는 미승인·제외다.
- main merge 승인: `0/3`.

## 사용량 기록

Fable 1차·2차 planning 직전/직후의 5시간 현재 세션, 주간 전체 모델과 주간 Fable 사용·잔여 비율·초기화 시각은 각 호출 직후 이 파일과 Implementation report에 기록한다.

| 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 56% 사용 / 44% 잔여 / 13:30 KST 초기화 | 20% 사용 / 80% 잔여 / 07-25 08:00 KST 초기화 | 40% 사용 / 60% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 56% 사용 / 44% 잔여 / 13:30 KST 초기화 | 20% 사용 / 80% 잔여 / 07-25 08:00 KST 초기화 | 40% 사용 / 60% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 73% 사용 / 27% 잔여 / 13:29 KST 초기화 | 22% 사용 / 78% 잔여 / 07-25 07:59 KST 초기화 | 42% 사용 / 58% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 73% 사용 / 27% 잔여 / 13:29 KST 초기화 | 22% 사용 / 78% 잔여 / 07-25 07:59 KST 초기화 | 42% 사용 / 58% 잔여 / 초기화 parse 불가 |

- 1차 runner: `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`, model 444초, stdout 25,979 bytes, stderr 0.
- Fable 원문: `tasks/export-001-column-picker-planning.md`에 byte-for-byte 저장.
- 2차 runner: `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`, model 255초, stdout 36,214 bytes, stderr 0.
- Fable 최종 구현 계약: `docs/35-selected-export-column-picker-plan.md`에 byte-for-byte 저장, `openBlockingDecisionCount: 0`.

## 구현 종료

- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- implementationReport: `tasks/export-001-column-picker-implementation-report.md`
- backendValidation: Release build warning 0/error 0, tests `401/401`
- frontendValidation: lint error 0/warning 1, typecheck, tests `109/109`, build PASS
- fullStackValidation: disposable PostgreSQL E2E `1/1`, desktop picker 20개·mobile 1개·실제 Excel 2개
- findingGate: Open P0/P1/P2 `0/0/0`; P3 `BACKEND-FORMAT-EXPERIMENT-BASELINE`은 조건부 housekeeping backlog
- fableCleanup: `FABLE_TASK_SESSION_CLEANED`, sessions 1·transcripts 1 제거, missing 0
- publishingBoundary: local experiment commit만 허용. 대표 repo·`main`·push·PR·merge·Persistent UAT 미반영, main merge 승인 `0/3`.
