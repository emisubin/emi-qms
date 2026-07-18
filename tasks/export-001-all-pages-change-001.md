# TASK-EXPORT-001 Change 002 — Fable 2차 기획 runner 승인 projection

## Canonical 연결

- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-002`
- fableSessionKey: `TASK-EXPORT-001-ALL-PAGES`
- branch: `experiment/task-export-001-all-pages-selected-export`
- interviewSource: `tasks/export-001-all-pages-interview.md`
- firstPlanningSource: `tasks/export-001-all-pages-planning.md`
- codexReviewSource: `tasks/export-001-all-pages-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/24-all-pages-selected-excel-export-plan.md`

이 파일은 runner의 exact target 승인 계약을 제공하는 projection이며 canonical 사용자 변경 기록은 `tasks/export-001-change-002.md`다. Fable은 1차 planning과 Codex review 전문을 직접 읽고, 관리자 8종 포함·기존 GET API 보존·공통 orchestration·20개 page screenshot resolution을 최종 구현 계약에 통합한다.

## 구현·Git 경계

- planningApproved: `true` — 2차 기획이 review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — 위 조건을 충족한 2차 기획의 experiment branch 범위
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Fable 1차 사용량

| 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 28% 사용 / 72% 잔여 / 17:39 KST 초기화 | 7% 사용 / 93% 잔여 / 07-25 07:59 KST 초기화 | 13% 사용 / 87% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 28% 사용 / 72% 잔여 / 17:39 KST 초기화 | 7% 사용 / 93% 잔여 / 07-25 07:59 KST 초기화 | 13% 사용 / 87% 잔여 / 초기화 parse 불가 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`, model 528초, stdout 31,436 bytes, stderr 0으로 완료됐고 `tasks/export-001-all-pages-planning.md`에 byte-for-byte 저장됐다.

## Fable 2차 사용량과 session 종료

| 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 2차 planning 직전 | 48% 사용 / 52% 잔여 / 17:39 KST 초기화 | 8% 사용 / 92% 잔여 / 07-25 07:59 KST 초기화 | 15% 사용 / 85% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 48% 사용 / 52% 잔여 / 17:39 KST 초기화 | 8% 사용 / 92% 잔여 / 07-25 07:59 KST 초기화 | 15% 사용 / 85% 잔여 / 초기화 parse 불가 |
| 구현·자동 검증 종료 | 64% 사용 / 36% 잔여 / 17:40 KST 초기화 | 9% 사용 / 91% 잔여 / 07-25 08:00 KST 초기화 | 18% 사용 / 82% 잔여 / 초기화 parse 불가 |

2차 planning runner는 `REFRESHED_AFTER_DRIFT`, model 450초, stdout 41,532 bytes로 완료됐고 `docs/24-all-pages-selected-excel-export-plan.md`에 byte-for-byte 저장됐다. 구현 종료 뒤 `bash scripts/run-fable-readonly.sh cleanup tasks/export-001-all-pages-interview.md`를 실행해 이 Task 소유 session 2개와 transcript 2개를 제거했다.
