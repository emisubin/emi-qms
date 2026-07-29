# TASK-ADMIN-002 Change 001 — experiment fast-track과 2차 기획 승인

## 실행 기준

- canonicalTaskId: `TASK-ADMIN-002`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `c4b999f2e76cd3763e4cbcbe7594582a0a6ced29`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/admin-002-interview.md`
- firstPlanningSource: `tasks/admin-002-planning.md`
- codexReviewSource: `tasks/admin-002-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/33-form-template-management-plan.md`

사용자는 관리자와 각 부서 부서장이 code 수정 없이 양식을 관리하도록 `TASK-ADMIN-002`를 인터뷰·중간 승인 없이 구현해 결과물까지 보여 달라고 명시했다. 1차 Fable 원문과 Codex review는 수정하지 않으며, 2차 Fable 기획을 최종 구현 source of truth로 사용한다.

## 승인·안전 경계

- planningApproved: `true` — Fable 2차 기획의 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 experiment 범위
- commitApproved: `true` — 검증·screenshot·종료 문서 완료 뒤 local commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Claude 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`의 privacy-safe projection만 기록한다. raw TUI와 계정 식별자는 저장하지 않으며 실패 시 값을 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 18% 사용 / 82% 잔여 / 13:29 KST 초기화 | 17% 사용 / 83% 잔여 / 07-25 07:59 KST 초기화 | 34% 사용 / 66% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 18% 사용 / 82% 잔여 / 13:29 KST 초기화 | 17% 사용 / 83% 잔여 / 07-25 07:59 KST 초기화 | 34% 사용 / 66% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 42% 사용 / 58% 잔여 / 13:30 KST 초기화 | 19% 사용 / 81% 잔여 / 07-25 08:00 KST 초기화 | 38% 사용 / 62% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 42% 사용 / 58% 잔여 / 13:30 KST 초기화 | 19% 사용 / 81% 잔여 / 07-25 08:00 KST 초기화 | 38% 사용 / 62% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 50% 사용 / 50% 잔여 / 13:30 KST 초기화 | 20% 사용 / 80% 잔여 / 07-25 08:00 KST 초기화 | 39% 사용 / 61% 잔여 / 초기화 parse 불가 |

Fable runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 성공했으며 model 349초, stdout 23,857 bytes, stderr 0이고 원문을 `tasks/admin-002-planning.md`에 byte-for-byte 저장했다.

2차 runner는 review/change 추가를 예상 source drift로 판정해 `REFRESHED_AFTER_DRIFT`, `baselineReused=false`, `driftStatus=SOURCE_OR_CONTRACT_CHANGED`로 안전하게 기준선을 갱신했다. model 388초, stdout 28,049 bytes, stderr 0이며 최종 원문을 `docs/33-form-template-management-plan.md`에 byte-for-byte 저장했다.

구현 종료 뒤 private Fable state는 `FABLE_TASK_SESSION_CLEANED`로 정리했고 session·transcript 각 2개를 제거했다.
