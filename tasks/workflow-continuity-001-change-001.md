# TASK-WORKFLOW-CONTINUITY-001 Change 001 — experiment fast-track·Fable 2차 기획 승인 경계

## 1. 실행 기준

- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `f3cd72fd1a6bf1f8f8bb7264101384cf92d42f38`
- interviewSource: `tasks/workflow-continuity-001-interview.md`
- firstPlanningSource: `tasks/workflow-continuity-001-planning.md`
- codexReviewSource: `tasks/workflow-continuity-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/37-workflow-continuity-plan.md`

사용자는 현재 experiment branch에서 신규 기능을 인터뷰·중간 승인 없이 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 구현·검증·screenshot·local commit까지 연속 진행하도록 명시했다. 이번 요청은 사용자가 직접 프로젝트 전체 입력 중 발견한 검수 실패와 구체 변경 14개를 포함한다. Fable 2차 기획의 blocking decision이 0이면 권장안을 자동 채택한다.

## 2. 구현·Git 경계

- planningApproved: `true` — Codex review를 읽는 Fable 2차 기획 한정
- implementationApproved: `true` — 2차 기획의 blocking decision 0 조건
- userValidationCompleted: `false`
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 3. 기존 Task change 연결

- `TASK-QR-001 Change 002`: 발급 가능 패널 선택 batch 발급, checkbox·inline preview.
- `TASK-007A Change 001`: 검사 연계 Pending 조치 UI와 activity timeline, 수동 종결 차단.
- `TASK-008A Change 002`: 도착→IQC 자동 요청, 입고 정보 구조와 자재 하위 탭.
- `TASK-009A Change 002`: IQC evidence gate·deep link·재검사 handoff.
- `TASK-012A Change 002`: 후속 품질검사 evidence gate·재검사 handoff.
- `TASK-E2E-FULL-SUITE-001 Change 007`: 기본 전체 흐름, 설계·구매 동시 선행 활성화, 구매 완료 조건과 전체 lifecycle 검증.

이 연결은 완료 Task를 재구현하거나 완료 상태를 지우지 않는다. 이번 사용자 검수 실패로 확인된 변경 범위만 다음 change로 기록한다.

## 4. Fable 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`로 측정한다. Reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 14:29 KST 초기화 | 30% 사용 / 70% 잔여 / 07-25 07:59 KST 초기화 | 59% 사용 / 41% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 16% 사용 / 84% 잔여 / 14:30 KST 초기화 | 31% 사용 / 69% 잔여 / 07-25 08:00 KST 초기화 | 61% 사용 / 39% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 16% 사용 / 84% 잔여 / 14:30 KST 초기화 | 31% 사용 / 69% 잔여 / 07-25 08:00 KST 초기화 | 61% 사용 / 39% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 16% 사용 / 84% 잔여 / 14:30 KST 초기화 | 31% 사용 / 69% 잔여 / 07-25 08:00 KST 초기화 | 61% 사용 / 39% 잔여 / 초기화 parse 불가 |
| 구현 종료 점검 | 27% 사용 / 73% 잔여 / 14:29 KST 초기화 | 32% 사용 / 68% 잔여 / 07-25 07:59 KST 초기화 | 63% 사용 / 37% 잔여 / 초기화 parse 불가 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 완료됐다. model 579초, stdout 32,085 bytes, stderr 0이며 `tasks/workflow-continuity-001-planning.md`에 Fable 원문을 byte-for-byte 저장했다.

2차 planning runner는 Codex review·approval change가 추가된 예상 source drift를 감지해 `REFRESHED_AFTER_DRIFT`, `baselineReused=false`, `driftStatus=SOURCE_OR_CONTRACT_CHANGED`로 기준선을 안전하게 다시 읽고 완료됐다. model 293초, stdout 34,973 bytes, stderr 0이며 `docs/37-workflow-continuity-plan.md`에 Fable 원문을 byte-for-byte 저장했다.
