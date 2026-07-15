# TASK-GOV-CODEX-002 Change 009 — Fable 원문 직접 작성과 Codex 사후 리뷰

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 009`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-USER-FLOW-001`
- roadmapNextGate: `PHASE_A_PREVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: Fable 5가 interview 질문과 승인된 기획 전문의 실제 작성자가 되고 Codex가 원문을 바꾸지 않은 채 사후 review만 수행하도록 책임 경계를 바로잡는다.
- Root Finding: 기존 runner는 Fable stdout을 private artifact로 반환한 뒤 Codex가 파일에 반영하도록 설계돼, Fable 원문과 최종 문서 사이에 Codex의 의미 변경 가능성이 남았다. 사용자 확인 결과 이는 최초 의도와 다르다.
- 변경·검증 경계: Root/Fable 지침, fail-closed runner, project rule, governance 산출물과 `TASK-USER-FLOW-001` 작성·review 계약.
- 보존할 불변조건: Fable의 Read·Glob·Grep-only 도구, safe mode, fixed target, private capture, contract/privacy fail-closed, 사용자 승인, 제품 source·runtime·DB·provider와 Git 게시 경계.
- 예상 산출물: Change 009, runner `draft|revise` mode, exact interview artifact, 지침·Roadmap·Implementation report 갱신과 USER-FLOW preview 검증.

## 2. 사용자 정정 계약

- Fable 질문은 Codex가 요약·재작성·순서 변경하지 않고 원문 그대로 전달한다.
- Codex는 질문의 표현·선택지·권장안을 바꾸거나 질문을 합치고 나누지 않는다. 설명이 필요하면 Fable 원문과 별도 구역에 표시한다.
- Fable 5가 승인된 preview Markdown 전문을 작성한다.
- Runner는 Fable stdout을 의미 변경 없이 대상 파일에 byte-for-byte로 저장한다.
- Codex는 Fable 원문 파일을 수정하지 않고 별도 review 파일에 Finding만 작성한다.
- 수정이 필요하면 Codex가 원문을 patch하지 않고 Fable `revise` mode가 review를 읽어 전문 전체를 다시 작성한다.
- 기계적 contract·privacy guard는 위반 시 저장을 거부할 뿐 문장을 수정하지 않는다.
- 질문·전문의 contract·privacy 검사는 `거부/통과`만 판정하며 Codex 문장을 삽입하거나 Fable 문장을 교체하지 않는다.

## 3. 구현 범위

### 포함

- Interview round별 Fable 원문 artifact의 direct write
- 승인된 `docs/` target용 `draft` mode와 review 기반 `revise` mode
- Planning·review 승인 상태와 fixed target 검증
- Private stdout과 Repository target의 byte equality 검증
- Draft 기존 target 덮어쓰기 차단, revise의 target·review 존재 검증
- GPT-5.6 SOL read-only 사후 review 계약
- Root·Fable 지침, project rule, governance 문서와 USER-FLOW 상태 동기화

### 제외

- Fable에 Write·Edit·shell·Git·MCP·browser·recursive agent 권한 부여
- Codex의 Fable 원문 사전 편집 또는 사후 patch
- Frontend·Backend·API·DB·migration·dependency·runtime·provider 변경
- 기존 `docs/02-business-flow.md`·`docs/04-permission-matrix.md` 변경
- commit·push·PR·merge와 worktree 정리

## 4. 실행 순서

1. Runner가 interview·planning·review 승인 상태와 exact target을 확인한다.
2. Fable 5를 safe/read-only 도구 경계에서 실행하고 stdout/stderr를 private artifact로 받는다.
3. Contract·privacy guard를 기계적으로 검사한다. 실패하면 Repository 파일을 만들거나 바꾸지 않는다.
4. 통과한 stdout을 same-filesystem 임시 파일에 복사하고 byte equality를 확인한다.
5. Draft는 target이 없을 때만, revise는 target과 preview review가 있을 때만 atomic replace한다.
6. 최종 target과 Fable stdout의 byte equality를 다시 확인한다.
7. GPT-5.6 SOL이 read-only로 target을 검토하고 별도 review를 작성한다.
8. Finding 수정은 Fable revise로만 수행한다.

## 5. 검증 계획

- Bash syntax와 ShellCheck
- invalid mode·path·argument, 미승인 planning/review, existing draft, missing revision target/review의 stable failure
- Interview·draft direct-write byte equality와 실패 전 target mutation 0
- Project Rule의 runner allow와 generic wrapper prompt 보존
- GPT-5.6 SOL model availability와 read-only review
- 문서 link·heading·privacy·secret·allowlist와 제품 source diff 0
- runtime·Persistent UAT·provider mutation 0

## 6. 승인 상태

- policyApproved: true
- implementationApproved: true
- runtimeMutationApproved: false
- publishingApproved: false
- mergeApproved: false
- userValidationComplete: false

## 7. 구현·검증 결과

- USER-FLOW Fable preview `draft` 1회와 `revise` 2회에서 stdout과 target의 byte equality를 확인했다.
- 최종 source SHA-256: `d0ec40cac42f27c5f2fbde5c0976d46b45067020e8e7327f347482c56369b87b`
- 최종 첫 비공백 행은 승인된 H1이며, 원문 앞 reasoning·preface 삽입 0이다.
- GPT-5.6 SOL read-only review 결과: `READY_FOR_USER_REVIEW_PHASE_A`, source 수정 `false`, 신규 P0/P1/P2 `0/0/0`.
- 기존 P2-006~008은 Fable revise로 해소했고 Codex의 source patch는 0이다.
- Bash syntax, ShellCheck, `git diff --check`는 governance와 USER-FLOW runner 양쪽에서 통과했다.
- 두 runner copy는 byte-identical하다.
- Negative gate 4건: invalid mode, traversal target, missing interview, existing draft target을 각각 stable failure로 거부했다.
- Frontend·Backend·API·DB·migration·runtime·provider mutation은 0이다.

Interview direct-write는 향후 새 질문 round에서 같은 공통 byte-copy 경로를 사용한다. 이번 USER-FLOW interview는 이미 완료됐으므로 새 질문을 만들기 위한 불필요한 Fable 호출은 수행하지 않았다. 과거 relay 문구를 exact 원문으로 소급 보증하지 않고, Change 009 이후 질문부터 원문 artifact가 canonical 전달 source다.

## 8. Finding과 게시 Gate

- Open P0/P1/P2/P3: `0/0/0/0`
- automaticValidationComplete: true
- USER-FLOW independentPreviewReviewComplete: true
- governanceIndependentVerificationComplete: false
- publishGate: `NO_GO_USER_VALIDATION_AND_GOVERNANCE_INDEPENDENT_VERIFICATION_PENDING`
