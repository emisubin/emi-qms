# TASK-MANUFACTURING-BATCH-001 Change 001 — experiment fast-track과 2차 기획 승인

## 1. 실행 기준

- canonicalTaskId: `TASK-MANUFACTURING-BATCH-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `57796704255c81ec8c79be00d5cc5618a9fca3ca`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/manufacturing-batch-001-interview.md`
- firstPlanningSource: `tasks/manufacturing-batch-001-planning.md`
- codexReviewSource: `tasks/manufacturing-batch-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/42-manufacturing-batch-assembly-plan.md`

사용자는 프로젝트 전체 흐름의 네 표시명을 정확히 변경하고, 제조 담당자가 기존 선택 Excel용 checkbox를 이용해 여러 패널의 조립 단계를 한 번에 완료할 수 있도록 명시했다. 이 experiment branch의 standing instruction에 따라 interview·중간 승인·권장안 채택 확인 없이 Fable 2-pass 뒤 구현·자동 검증·desktop/mobile screenshot까지 진행한다. 1차 planning과 Codex review는 수정하지 않고 2차 planning을 최종 구현 source of truth로 사용한다.

## 2. 승인·안전 경계

- planningApproved: `true` — 사용자 직접 요청과 Fable 2차 기획의 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 experiment 범위
- commitApproved: `true` — 기존 dirty WIP와 겹치지 않는 exact allowlist를 안전하게 분리할 수 있을 때만
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
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 20:29 KST 초기화 | 37% 사용 / 63% 잔여 / 07-25 07:59 KST 초기화 | 73% 사용 / 27% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 20:30 KST 초기화 | 37% 사용 / 63% 잔여 / 07-25 07:59 KST 초기화 | 73% 사용 / 27% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 15% 사용 / 85% 잔여 / 20:30 KST 초기화 | 38% 사용 / 62% 잔여 / 07-25 08:00 KST 초기화 | 75% 사용 / 25% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 15% 사용 / 85% 잔여 / 20:30 KST 초기화 | 38% 사용 / 62% 잔여 / 07-25 08:00 KST 초기화 | 75% 사용 / 25% 잔여 / 초기화 parse 불가 |

1차 planning 직전 첫 조회는 Claude TUI 응답 시간 초과 `exit 23`으로 실패했고, 동일 read-only reporter 1회 재시도에서 위 projection을 확인했다.

## 4. 최종 기획에 요구하는 resolution

1. 단일 프로젝트의 기존 패널 선택 집합을 Excel과 제조 batch가 함께 쓰되 action 영역을 구분한다.
2. 조립 단계는 immutable execution template version의 `MANUFACTURING` item code와 snapshot sequence를 대응해 찾고 label·고정 순서를 추측하지 않는다.
3. 조립 단계까지의 선행 미완료 단계는 확인 sheet 안내 뒤 같은 batch에서 순서대로 함께 확인한다.
4. Frontend는 비대상 패널을 사유와 함께 제외하고, 서버에 전달된 대상은 전부 성공/전부 실패로 처리한다.
5. batch가 새로 확인한 각 단계마다 actor/time `StepChecked` event를 남기고 execution version을 1씩 증가시킨다.
6. additive `0056`에 batch operation replay projection과 event nullable correlation FK를 둔다.
7. batch는 execution 전체 완료, work item 완료, panel workflow stage, LQC/OQC 인계를 임의 전진시키지 않는다.
8. workflow 네 표시명은 Frontend stage code override로만 변경하고 내부 stage code·이름·optional·진행률은 유지한다.
9. 사후 작업시간·상세값 보정, 다른 부서 batch, 자동 시작·부분 성공·완료 되돌리기는 제외한다.

## 5. Fable 1차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `CREATED_FULL_BASELINE`
- baselineReused: `false`
- driftStatus: `NO_PRIOR_SESSION`
- modelSeconds: `566`
- stdoutBytes: `26843`
- stderrBytes: `0`
- artifactPath: `tasks/manufacturing-batch-001-planning.md`
- artifactWritten: `true`

## 6. Fable 2차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `REFRESHED_AFTER_DRIFT`
- baselineReused: `false`
- driftStatus: `SOURCE_OR_CONTRACT_CHANGED`
- modelSeconds: `365`
- stdoutBytes: `23056`
- stderrBytes: `0`
- artifactPath: `docs/42-manufacturing-batch-assembly-plan.md`
- artifactWritten: `true`

## 7. 구현·게시 경계

- 대표 repo·`main`, push, PR, merge, Persistent UAT, 실제 provider를 변경하지 않는다.
- 고정 사용자 검수 runtime은 Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`을 유지한다.
- 현재 worktree에는 선행 Task의 미커밋 변경이 있으므로 staging·commit 전에 exact path와 hunk 분리가 가능한지 다시 검증한다. 사용자 변경을 함께 commit하거나 정리하지 않는다.
