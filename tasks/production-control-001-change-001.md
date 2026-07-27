# TASK-PRODUCTION-CONTROL-001 Change 001 — experiment fast-track과 2차 기획 승인

## 1. 실행 기준

- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `de8e05bc0383ebf5abbdcfd95cab3d5d85c9f5ce`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- identityGateSource: `tasks/production-control-001-identity-gate.md`
- interviewSource: `tasks/production-control-001-interview.md`
- firstPlanningSource: `tasks/production-control-001-planning.md`
- codexReviewSource: `tasks/production-control-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/43-production-control-plan.md`

사용자는 Item별 동적 생산계획·제조 양식, 프로젝트 생성 시 version snapshot, 프로젝트별 다중 실적 연결 수정, 계획 시작·종료, 부서 실데이터 기반 자동 실적 시작·종료와 계획/실적 가로 막대 캘린더를 최종 목표로 확정했다. 사용자가 모든 세부 정책을 직접 결정하고 Fable Round 8 전체 요약을 `요약 확인`했으므로 1차 planning, Codex review, 2차 planning과 구현을 experiment fast-track으로 연속 진행한다.

## 2. 승인·안전 경계

- planningApproved: `true`
- planningApprovalSource: `USER_EXPLICIT_EXPERIMENT_RULE_AFTER_CONFIRMED_INTERVIEW`
- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_EXPERIMENT_RULE_AFTER_CONFIRMED_INTERVIEW`
- commitApproved: `true`
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
| 1차 planning 직전 | 사용 70% / 잔여 30% / 초기화 20:00 (Asia/Seoul) | 사용 6% / 잔여 94% / 초기화 8월 1일 08:00 (Asia/Seoul) | 사용 11% / 잔여 89% / 초기화 시각 TUI 파싱 불가 |
| 1차 planning 직후 | 사용 70% / 잔여 30% / 초기화 20:00 (Asia/Seoul) | 사용 6% / 잔여 94% / 초기화 8월 1일 08:00 (Asia/Seoul) | 사용 11% / 잔여 89% / 초기화 시각 TUI 파싱 불가 |
| 2차 planning 직전 | 사용 79% / 잔여 21% / 초기화 19:59 (Asia/Seoul) | 사용 6% / 잔여 94% / 초기화 8월 1일 07:59 (Asia/Seoul) | 사용 12% / 잔여 88% / 초기화 시각 TUI 파싱 불가 |
| 2차 planning 직후 | 사용 86% / 잔여 14% / 초기화 20:00 (Asia/Seoul) | 사용 7% / 잔여 93% / 초기화 8월 1일 08:00 (Asia/Seoul) | 사용 13% / 잔여 87% / 초기화 시각 TUI 파싱 불가 |

## 4. 최종 기획에 요구하는 불변조건

- 계획 milestone은 stable semantic code와 project snapshot을 사용하고 이름·순서·ordinal로 source를 추측하지 않는다.
- 계획 시작·종료는 생산관리 담당자만 입력하며 실적 시작·종료는 각 부서 authoritative data에서 자동 파생한다.
- 구매·자재는 구매품목·도착분, 제조·LQC·OQC·전진검수·FAT·물류는 개별 physical panel이라는 확정 처리 단위를 보존한다.
- OQC는 단계별 판정, 전진검수·FAT는 패널별 aggregate 판정, FAT optional과 부분 입고·부분 출하를 보존한다.
- Open Pending은 과거 실적을 삭제하거나 workflow 단계를 후퇴시키지 않고 현재 차단 상태와 근거로 표시한다.
- 표·상세 펼침·가로 막대 일정은 같은 Backend projection을 사용한다.
- 기존 18단계 전체 흐름 진행률은 바꾸지 않고 일정 진행률과 명확히 분리한다.
- legacy `planned_date`와 custom 항목은 data loss 없이 보수적으로 옮기며 모호한 항목을 임의 source에 연결하지 않는다.
- `work_items.due_date` 자동 동기화와 지연 외부 알림은 이번 범위에서 제외한다.
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.

## 5. Fable 실행 결과

### 1차 planning

- previousAttemptStatusCode: `ABORTED_BY_USER_DIRECTION`
- previousAttemptArtifactWritten: `false`
- statusCode: `FABLE_READONLY_OUTPUT_READY`
- artifactWritten: `true`
- note: 사용자가 세부 정책을 직접 결정한다고 변경해 이전 호출을 중단했고, Round 8 요약 확인 뒤 새 1차 planning을 시작한다.
- artifactPath: `tasks/production-control-001-planning.md`

### 2차 planning

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- artifactWritten: `true`
- artifactPath: `docs/43-production-control-plan.md`
