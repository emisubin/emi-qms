# TASK-ATTACHMENT-001 Change 001 — experiment fast-track과 2차 기획 승인

## 실행 기준

- canonicalTaskId: `TASK-ATTACHMENT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `48fe8cd78293`
- interviewSource: `tasks/attachment-001-interview.md`
- firstPlanningSource: `tasks/attachment-001-planning.md`
- codexReviewSource: `tasks/attachment-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/45-pending-action-attachment-plan.md`

## 승인·안전 경계

- planningApproved: `true`
- implementationApproved: `true` — 2차 기획의 blocking decision 0 조건
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Claude 사용량 기록

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 측정 실패 — privacy-safe usage script exit 23, 값 추정 안 함 | 측정 실패 | 측정 실패 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 / 08-01 08:00 KST 초기화 | 22% 사용 / 78% 잔여 / 08-01 08:00 KST 초기화 |
| 2차 planning 직전 | 0% 사용 / 100% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 / 08-01 08:00 KST 초기화 | 22% 사용 / 78% 잔여 / 08-01 08:00 KST 초기화 |
| 2차 planning 직후 | 17% 사용 / 83% 잔여 / 15:09 KST 초기화 | 12% 사용 / 88% 잔여 / 08-01 08:00 KST 초기화 | 22% 사용 / 78% 잔여 / 초기화 시각 파싱 불가 |

## Fable 2차 기획 결과

- result: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `REFRESHED_AFTER_DRIFT`
- baselineReused: `false`
- driftStatus: `SOURCE_OR_CONTRACT_CHANGED`
- modelSeconds: `500`
- artifact: `docs/45-pending-action-attachment-plan.md`
- openBlockingDecisionCount: `0`

## 구현 불변조건

- 사용자 확정 정책은 `tasks/attachment-001-interview.md`를 따른다.
- Fable 1차 planning과 Codex review는 수정하지 않는다.
- Fable 2차 planning은 `docs/45-pending-action-attachment-plan.md`에 별도 기록하고 최종 구현 source로 사용한다.
- 대표 Repository, `main`, Persistent UAT, 실제 provider, push·PR·merge를 변경하지 않는다.
