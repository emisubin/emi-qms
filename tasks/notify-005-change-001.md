# TASK-NOTIFY-005 Change 001 — 사용자별 알림 설정 experiment fast-track과 Task Identity Gate

## 1. Task Identity Gate

- proposedTaskId: `TASK-NOTIFY-005`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK_007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-NOTIFY-005`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 사용자가 인앱 원본·필수 업무 알림을 보존하면서 허용된 event별 외부 채널 수신 방식을 조회·변경·기본값 복원한다.
- Root Finding 또는 정책 결정: 외부 delivery와 에스컬레이션은 구현됐지만 사용자별 preference가 없고, 단순 opt-out은 필수 알림 누락 위험이 있다. 고정 taxonomy·필수 잠금·기존 기본값 호환·audit가 필요하다.
- 변경·검증 경계: additive preference/audit migration, 본인·최소 관리자 API, dispatcher·escalation delivery 생성 gate, desktop·390px 사용자/관리자 UI, isolated DB·fake provider 검증.
- 보존할 불변조건: 인앱 원본 전체 보존, 필수 알림 opt-out 금지, Teams 통합 채널 개인 설정 제외, Backend authoritative, 기존 delivery·attempt·at-least-once 계약, 대표 repo·`main`·Persistent UAT·실제 provider·게시 제외.
- 예상 산출물: fast-track interview, Fable 1차 planning, Codex review, Fable 2차 planning, 구현·검증·페이지별 screenshot, implementation report·SOP·User manual·Roadmap update·user validation checklist, local commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적은 Product Roadmap의 canonical `TASK-NOTIFY-005` 한 건이다. Task planning·review·implementation report, local/remote branch, worktree와 open/merged PR은 0건이었다. 새 Task ID를 만들지 않고 canonical ID와 `change-001`을 사용한다.

## 2. 실험 재정렬 승인과 범위

사용자는 이 experiment 대화에서 Roadmap 밖 다음 작업도 인터뷰·중간 승인 없이 권장안으로 기획·검토·구현해 결과를 보여 주도록 명시했다. Roadmap의 후속 후보 상대 순서 `TASK-NOTIFY-004 → TASK-UX-001 → TASK-NOTIFY-005`에서 NOTIFY-004는 완료됐고 UX-001 A1은 직전 experiment에서 local 구현·자동 검증까지 완료됐으므로 다음 후보 `TASK-NOTIFY-005`를 선택한다. Canonical queue의 다음 `TASK-007A` Gate와 UX-001 사용자 검수·A2 상태는 변경하지 않는다.

- branch: `experiment/task-notify-005-preferences`
- baseExperimentCommit: `5cd223c87700a33924f29875286c71a7b8967041`
- representativeMainCommit: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/notify-005-interview.md`
- firstPlanningSource: `tasks/notify-005-planning.md`
- codexReviewSource: `tasks/notify-005-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/26-notification-preferences-plan.md`

## 3. 구현·Git 경계

- planningApproved: `true` — Codex review를 읽는 Fable 2차 기획 한정
- implementationApproved: `true` — 2차 기획이 review resolution을 반영하고 blocking decision 0인 조건의 experiment 구현
- userValidationCompleted: `false`
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 4. Fable 사용량 기록

Claude `/usage`는 Repository mutation 없이 `bash scripts/report-claude-usage.sh`로 측정한다. Reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 19% 사용 / 81% 잔여 / 00:00 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 08:00 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 19% 사용 / 81% 잔여 / 00:00 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 08:00 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 28% 사용 / 72% 잔여 / 23:59 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 07:59 KST 초기화 | 22% 사용 / 78% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 28% 사용 / 72% 잔여 / 23:59 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 07:59 KST 초기화 | 22% 사용 / 78% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 40% 사용 / 60% 잔여 / 00:00 KST 초기화 | 12% 사용 / 88% 잔여 / 07-25 08:00 KST 초기화 | 24% 사용 / 76% 잔여 / 초기화 parse 불가 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 완료됐다. model 426초, stdout 24,405 bytes, stderr 0이며 `tasks/notify-005-planning.md`에 Fable 원문을 byte-for-byte 저장했다. `openBlockingDecisionCount`는 0이고 권장안 자동 채택 대상 비차단 결정은 5건이다.

2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 완료됐다. model 135초, stdout 19,840 bytes, stderr 0이며 `docs/26-notification-preferences-plan.md`에 Fable 원문을 byte-for-byte 저장했다. `openBlockingDecisionCount`는 0이다.

구현 종료 최신 usage 조회 후 Fable private session cleanup을 실행했다. cleanup 결과는 `FABLE_TASK_SESSION_CLEANED`, session 1건과 transcript 1건 제거, missing transcript 0건이다.
