# TASK-NOTIFY-AUDIT-001 Change 001 — experiment fast-track과 2차 기획 승인

## 실행 기준

- canonicalTaskId: `TASK-NOTIFY-AUDIT-001`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `5a11df634353f4ee392a3e8436c16a30609295b8`
- interviewSource: `tasks/notify-audit-001-interview.md`
- firstPlanningSource: `tasks/notify-audit-001-planning.md`
- codexReviewSource: `tasks/notify-audit-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/35-notification-preference-audit-plan.md`
- codexSecondPlanningApproved: true
- codexSecondPlanningSource: `USER_EXPLICIT_FABLE_SUBSTITUTION_2026_07_20`
- codexSecondPlanningTarget: `docs/35-notification-preference-audit-plan.md`

사용자는 남은 작업 1번과 3번을 한꺼번에 구현하라고 명시했다. Task identity·기획·Finding은 분리하되 같은 experiment batch에서 구현·통합 검증·screenshot·local commit까지 진행한다. 1차와 2차 Fable 원문은 Codex가 수정하지 않는다.

## 승인·안전 경계

- planningApproved: `true` — review를 반영한 2차 기획 한정
- implementationApproved: `true` — 2차 기획 blocking decision 0 조건의 local experiment 구현
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Claude 사용량 기록

| 측정 시점 | 결과 |
| --- | --- |
| 1차 planning 직전 | `FAILED_PARSE` — session label 0, all-model/Fable label 존재. 수치 추정 안 함 |
| 1차 planning 직후 | `FAILED_PARSE` — session label 0, all-model/Fable label 존재. 수치 추정 안 함 |

1차 runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 성공했다. model 365초, stdout 22,709 bytes, stderr 0이며 원문은 `tasks/notify-audit-001-planning.md`에 byte-for-byte 저장됐다.

## 2차 기획 대체 결정

Claude CLI OAuth refresh 실패와 사람 확인 CAPTCHA로 Fable 2차 기획이 진행되지 않았다. 사용자는 2026-07-20에 `codex가 2차 기획하고 구현하라`고 명시해 이 Task에 한해 Codex 대체 기획과 구현을 승인했다. `docs/35-notification-preference-audit-plan.md`는 Fable 원문이 아니며 `CODEX_SECOND_PLANNING`으로 명시한다. 기존 Fable 1차 원문과 Codex review는 수정하지 않는다.
