# TASK-GOV-CODEX-002 Change 013 — Fable primary draft 범용 계약 보정

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 013`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-USER-FLOW-001`
- roadmapNextGate: `Fable redraft 뒤 별도 merge`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

## 2. Root Finding

- stableFindingId: `FABLE_PRIMARY_DRAFT_MODE_CONTRACT_CONFLICT`
- severity: `P2`
- 원인: Root 정책은 승인된 `docs/` 전문을 planning 대신 단일 primary draft로 허용하지만 runner는 planning·review 양쪽의 구현 승인값을 요구했다. Generic `draft|revise` prompt와 postflight에도 USER-FLOW 전용 H1·18단계·13개 journey가 하드코딩돼 다른 신규 기능 문서에 잘못 적용될 수 있었다.
- 영향: 일반 신규 기능에서 합법적인 `docs/` primary draft gate를 충족하기 어렵고, 충족하더라도 대상과 무관한 USER-FLOW 구조가 생성될 수 있다.
- secondStableFindingId: `REPORTING_CHANGE001_COMPLETION_STATE_CONFLICT`
- secondFindingSeverity: `P2`
- 추가 원인·영향: 최초 Reporting Task 완료와 Change 001 현재 상태가 report·Task·Change에서 현재형으로 혼재해 검수·게시 gate를 조기에 닫을 수 있었다.
- thirdStableFindingId: `ROADMAP_CURRENT_STATE_CONFLICT`
- thirdFindingSeverity: `P2`
- 추가 원인·영향: Roadmap 실행 큐·Decision Log는 USER-FLOW redraft·별도 merge 승인을 기록했지만 뒤쪽 Task 상세와 추적표는 이전 미승인 상태를 current state로 유지해 다음 실행 Gate가 양쪽으로 갈렸다.

## 3. 사용자 결정과 승인 경계

사용자는 Governance 정책을 독립 검증 뒤 merge하고, 그 다음 USER-FLOW를 Fable redraft 뒤 별도 merge하도록 승인했다. 이 Change는 첫 독립 검증에서 확인된 P2를 Governance 게시 전에 기존 정책 범위 안에서 해소한다.

- implementationApproved: `true`
- userValidationComplete: `true`
- publishingApproved: `true`
- mergeApprovedAfterIndependentPass: `true`
- productMutationApproved: `false`
- runtimeMutationApproved: `false`

## 4. 보정 계약

- `planning`: 기본 `tasks/<task-id>-planning.md` primary draft를 생성한다.
- `draft`: planning을 중복 생성하지 않고, 최신 change에 아래 세 필드가 exact target과 함께 있을 때만 승인된 `docs/` primary draft를 생성한다.
  - `fablePrimaryDraftApproved: true`
  - `fablePrimaryDraftSource: USER_EXPLICIT_REQUEST`
  - `fablePrimaryDraftTarget: docs/<approved-target>.md`
- Generic `draft|revise`는 하나의 H1과 작성 상태·source Task·작성 모델 metadata만 검증하며 특정 업무 section·diagram·journey를 강제하지 않는다.
- `revise`는 최신 change의 redraft 승인·exact target, 기존 target과 Codex review를 요구하고 승인 change digest를 한 번만 소비한다.
- 기존 `TASK-USER-FLOW-001`의 H1·`previewStatus`·preview review path는 exact Task와 `docs/13-user-flow-baseline.md` 조합에만 historical compatibility contract로 유지한다.
- Draft와 revise 모두 호출 전·기록 직전 latest approval change와 digest를 재확인한다.

## 5. 포함·제외 범위

### 포함

- Root·Fable 기획자 지침
- Fable read-only runner
- 본 Change, Task, Implementation report와 Roadmap 상태
- Reporting Change 001의 최초 Task 완료와 현재 Change 상태 분리

### 제외

- Fable 도구 권한 완화
- 제품 source·dependency·migration·runtime·DB·provider
- USER-FLOW 원문 redraft와 게시 — Governance merge 뒤 별도 branch에서 수행
- branch 삭제와 private Fable session cleanup — USER-FLOW 종료 뒤 수행

## 6. 검증 기준

- Bash syntax·ShellCheck
- 기존 invalid mode·path·missing interview·existing target negative
- Generic draft: planning/review 없이 exact latest change approval과 target으로만 통과
- Generic draft: approval 누락·target mismatch·USER-FLOW 전용 output 거부
- USER-FLOW legacy revise: exact Task·target에서만 compatibility contract 적용
- Generic revise: USER-FLOW 전용 H1·diagram·journey 요구 0
- approval change·target 경쟁 변경 fail-closed
- Markdown·privacy·product/config/migration diff와 runtime/worktree 보존
- 수정 후 분리된 Codex 독립 재검증

## 7. Finding 상태

- `FABLE_PRIMARY_DRAFT_MODE_CONTRACT_CONFLICT`: `RESOLVED`
- `REPORTING_CHANGE001_COMPLETION_STATE_CONFLICT`: `RESOLVED`
- `ROADMAP_CURRENT_STATE_CONFLICT`: `RESOLVED`
- P0/P1: `0/0`
- independentVerificationComplete: `true`
- publishGate: `GO`
