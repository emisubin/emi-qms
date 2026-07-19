# TASK-PENDING-TYPE-001 Change 001 — experiment fast-track과 2차 기획 승인

## 1. 실행 기준

- canonicalTaskId: `TASK-PENDING-TYPE-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `35af25abfa4adebec5929071d55c2703faefc74f`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/pending-type-001-interview.md`
- firstPlanningSource: `tasks/pending-type-001-planning.md`
- codexReviewSource: `tasks/pending-type-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/34-pending-type-management-plan.md`

사용자는 이 experiment branch의 신규 기능을 인터뷰·중간 승인·권장안 채택 확인 없이 Fable 2-pass 뒤 구현 결과까지 진행하도록 standing instruction을 명시했다. Pending 유형·권한의 비차단 정책은 Fable 권장안을 자동 채택하며 1차 planning과 Codex review는 수정하지 않고 2차 planning을 최종 구현 source of truth로 사용한다.

## 2. 승인·안전 경계

- planningApproved: `true` — standing instruction과 Fable 2차 기획의 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 experiment 범위
- commitApproved: `true` — 검증·screenshot·종료 산출물 완료 뒤 local commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 3. Claude 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`의 privacy-safe projection만 기록한다. raw TUI와 계정 식별자는 저장하지 않으며 실패 시 값을 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 22:50 KST 초기화 | 23% 사용 / 77% 잔여 / 07-25 07:59 KST 초기화 | 45% 사용 / 55% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 22:49 KST 초기화 | 24% 사용 / 76% 잔여 / 07-25 07:59 KST 초기화 | 47% 사용 / 53% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 14% 사용 / 86% 잔여 / 22:49 KST 초기화 | 24% 사용 / 76% 잔여 / 07-25 07:59 KST 초기화 | 47% 사용 / 53% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 14% 사용 / 86% 잔여 / 22:49 KST 초기화 | 24% 사용 / 76% 잔여 / 07-25 07:59 KST 초기화 | 47% 사용 / 53% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 22% 사용 / 78% 잔여 / 22:49 KST 초기화 | 24% 사용 / 76% 잔여 / 07-25 07:59 KST 초기화 | 48% 사용 / 52% 잔여 / 초기화 parse 불가 |

## 4. 최종 기획에 요구하는 안전 불변조건

- 자동 `Nonconformance`·`Punch`·`ManufacturingStop` semantic을 rename/delete/remap할 수 없다.
- 과거 Pending의 유형 의미·label 근거·audit을 보존하고 hard delete를 제공하지 않는다.
- System Administrator의 업무 Pending mutation 우회 금지와 기존 `Pending.Manage` actor 규칙을 변경하지 않는다.
- 새 관리 permission·scope·CAS·audit을 Backend와 DB에서 강제한다.
- 대표 repo·`main`·Persistent UAT·provider·push·PR·merge는 제외한다.

## 5. Fable 1차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `CREATED_FULL_BASELINE`
- baselineReused: `false`
- driftStatus: `NO_PRIOR_SESSION`
- modelSeconds: `362`
- stdoutBytes: `24723`
- stderrBytes: `0`
- artifactPath: `tasks/pending-type-001-planning.md`
- artifactWritten: `true`

## 6. Fable 2차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `REFRESHED_AFTER_DRIFT`
- baselineReused: `false`
- driftStatus: `SOURCE_OR_CONTRACT_CHANGED`
- modelSeconds: `383`
- stdoutBytes: `29161`
- stderrBytes: `0`
- artifactPath: `docs/34-pending-type-management-plan.md`
- artifactWritten: `true`
- openBlockingDecisionCount: `0`
