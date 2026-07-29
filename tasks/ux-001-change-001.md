# TASK-UX-001 Change 001 — A1 experiment fast-track과 Task Identity Gate

## 1. Task Identity Gate

- proposedTaskId: `TASK-UX-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK_007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UX-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 내 업무·알림의 mutation action에 action 인접 loading·success·error·partial, 다음 행동, 중복 submit 차단과 접근 가능한 focus/announcement를 제공한다.
- Root Finding 또는 정책 결정: Roadmap의 후속 기능 후보 A는 공통 feedback contract를 A1에서 먼저 검수한 뒤 A2 업무 화면으로 확대하도록 정한다. 현재 구현은 공통 component와 화면별 문자열 message가 혼재하고 success/error tone을 문자열 포함 여부로 추론한다.
- 변경·검증 경계: A1 공통 Frontend 계약, 내 업무·알림, 관련 unit/isolated E2E와 desktop·390px screenshot. Backend는 계약 확인만 하고 신규 능력을 만들지 않는다.
- 보존할 불변조건: Backend 권한·업무 상태·audit, 인앱 알림 원본, 기존 deep link, 모바일 적응형 layout, 대표 repo·`main`·Persistent UAT·provider·게시 제외.
- 예상 산출물: fast-track interview, Fable 1차 planning, Codex review, Fable 2차 planning, A1 구현·검증·screenshot, implementation report, Roadmap update, local commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적은 Product Roadmap의 canonical `TASK-UX-001` 한 건이다. Task 파일·local/remote UX branch·worktree·open/merged PR은 0건이었다. 새 Task ID를 만들지 않고 canonical ID와 `change-001`을 사용한다.

## 2. 실험 재정렬 승인과 범위

사용자는 이 experiment 대화에서 Roadmap 밖 다음 작업도 인터뷰·중간 승인 없이 권장안으로 기획·검토·구현해 결과를 보여 주도록 명시했다. 18단계 기능, 전체 선택 Excel과 전체 E2E 종료 뒤 “다음 작업 시작해”라고 지시했으므로 Roadmap의 후속 후보 순서 `TASK-NOTIFY-004 → TASK-UX-001 → TASK-NOTIFY-005`에서 완료된 NOTIFY-004 다음의 `TASK-UX-001` A1을 선택한다.

- branch: `experiment/task-ux-001-action-feedback`
- interviewSource: `tasks/ux-001-interview.md`
- firstPlanningSource: `tasks/ux-001-planning.md`
- codexReviewSource: `tasks/ux-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/25-action-feedback-a1-plan.md`

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

Claude `/usage`는 Repository mutation 없이 `bash scripts/report-claude-usage.sh`로 측정한다. reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 23:59 KST 초기화 | 9% 사용 / 91% 잔여 / 07-25 08:00 KST 초기화 | 18% 사용 / 82% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 08:00 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 11% 사용 / 89% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 07:59 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 11% 사용 / 89% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 07:59 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 19% 사용 / 81% 잔여 / 23:59 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 07:59 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 parse 불가 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 완료됐다. model 263초, stdout 22,453 bytes, stderr 0이며 `tasks/ux-001-planning.md`에 Fable 원문을 byte-for-byte 저장했다.

2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 완료됐다. model 116초, stdout 17,223 bytes, stderr 0이며 `docs/25-action-feedback-a1-plan.md`에 Fable 원문을 byte-for-byte 저장했다. `openBlockingDecisionCount`는 0이다.

구현·검증 종료 뒤 runner cleanup은 `FABLE_TASK_SESSION_CLEANED`로 완료됐고 Task 전용 session 1개와 transcript 1개만 제거했다. One-time approval receipt와 Repository 산출물은 보존했다.
