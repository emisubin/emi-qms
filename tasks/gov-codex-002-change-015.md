# TASK-GOV-CODEX-002 Change 015 — 실험 Task 완료 원장과 중복 실행 방지

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 015`
- taskType: `DOCS_GOVERNANCE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 현재 experiment 계보에서 구현·자동 검증이 끝난 Task와 실제 남은 Task를 한 원장으로 고정하고, 사용자 검수 일괄 대기 때문에 완료 Task가 다시 선택되는 일을 막는다.
- Root Finding: Roadmap의 canonical main queue는 `TASK-007A Reordered Pending`을 유지하는 반면 실제 experiment implementation report는 `TASK-007A`부터 후속 기능까지 구현·자동 검증 완료를 기록해, 새 세션이 canonical queue만 읽으면 완료 Task를 재선택할 수 있다.
- 변경·검증 경계: Root 지침, 종료 정책, Product Roadmap, experiment 완료 원장과 governance 종료 산출물만 변경한다.
- 보존할 불변조건: 사용자 검수를 완료로 가장하지 않음, 대표 repo·`main`·제품 source·migration·runtime·DB·provider 무변경, local experiment commit만 허용, merge 승인 `0/3`.
- 예상 산출물: 완료 Task inventory, 남은 Task queue, 조건부 backlog, 중복 실행 방지 gate, 문서 일관성·privacy 검증과 local commit.

## 2. 사용자 결정

- 실험 branch에서는 구현·필수 자동 검증·종료 산출물이 완료되고 사용자 직접 검수만 남은 Task를 실험 개발 완료로 인정한다.
- 사용자 직접 검수는 마지막에 일괄 수행하며, 그 대기 상태는 같은 Task를 다시 기획·구현하는 근거가 아니다.
- 완료 Task의 검수 실패나 수정 요청은 새 Task 복제가 아니라 기존 canonical Task의 다음 change 또는 bugfix로 처리한다.
- 대표 repo·`main` 반영, Persistent UAT와 실제 provider는 이 완료 판정에 포함하지 않는다.
- `main` merge는 서로 분리된 승인 3회 전까지 금지한다.

## 3. 감사 결과

- 구현 보고서가 있는 현재 실험 기능 Task: `19`개 scope. 이 중 `TASK-UX-001 A1`은 완료 slice이고 A2가 별도 후속이며, 나머지는 승인된 현재 실험 범위가 완료됐다.
- `TASK-010A`의 과거 미실행 Full-Stack 항목은 `TASK-E2E-FULL-SUITE-001`의 panel-kitting 포함 전체 `35/35 PASS`로 보완됐다.
- 최신 누적 기준선: Backend `391/391`, Frontend `101/101`, fresh migration `0041`까지 PASS.
- Open P0/P1/P2: 구현 보고서 기준 `0/0/0`.
- 사용자 직접 검수: 완료로 표시하지 않고 `BATCHED_FINAL`로 일괄 대기.
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider: 변경 없음.

## 4. 구현 범위

### 포함

- `docs/27-experiment-task-ledger.md`의 상태 정의·완료 목록·남은 목록·조건부 backlog·중복 방지 checklist
- Root `AGENTS.md`의 experiment 완료 원장 gate
- `docs/12-task-completion-policy.md`의 실험 개발 완료와 사용자 검수 완료 분리
- Product Roadmap 실행 큐·상세 상태·추적·Decision Log의 experiment 상태 동기화
- 기존 governance Task·Implementation report·검수 checklist 갱신

### 제외

- Backend·Frontend·API·DB·migration·dependency·runtime·provider 변경
- 완료 기능 재기획·재구현
- 대표 repo, `main`, push, PR, merge와 worktree 정리
- 사용자 최종 일괄 검수 실행

## 5. 승인 상태

- policyApproved: `true`
- implementationApproved: `true`
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 6. 검증 기준

- 완료 원장의 implementation report link가 모두 현재 checkout에서 유효
- 완료 Task가 Roadmap에서 다시 `Reordered Pending` 또는 `Dependency Pending`으로 단독 표시되지 않음
- 실험 완료와 사용자 검수·대표 repo·UAT·merge 상태가 분리됨
- `TASK-UX-001 A1/A2`, 선택 export 완료/column picker 후속과 조건부 P3가 중복 없이 구분됨
- Markdown local link·duplicate heading·privacy/secret·`git diff --check`
- 제품 source·migration·dependency·runtime diff `0`

## 7. 종료 산출물

자동 검증 결과:

- 완료 원장 scope count: `19` — full complete `18` + `TASK-UX-001 A1` complete slice `1`
- 이름이 확정된/설명 가능한 남은 제품 개발 범위: `10`개, 별도 미확정 Roadmap 입력은 9개 묶음으로 분리
- 변경 문서 local link missing: `0`
- 변경 문서 duplicate heading: `0`
- 추가 diff의 email·private key·credential assignment 후보: `0/0/0`
- `git diff --check`: `PASS`
- Backend·Frontend·database·migration·dependency·runtime diff: `0`
- 대표 repository: branch `main`, HEAD `b8f3e2104074d05c2e71999c08a7374e8729f68f`, clean

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/gov-codex-002-implementation-report.md` Change 015 | 갱신 |
| SOP | `tasks/gov-codex-002.md`, Root `AGENTS.md` | 완료 원장 조회·재개 절차 갱신 |
| User manual | `docs/27-experiment-task-ledger.md` 6·7장 | 상태 읽기·검수 실패·승격 경계 안내 |
| Roadmap update | `docs/00-product-roadmap.md` | experiment 완료 상태와 남은 큐 동기화 |
| User validation checklist | 이 문서 6장·`tasks/gov-codex-002.md` 13장 | 정책 사용자 승인·문서 자동 검증 완료, 제품 최종 검수는 일괄 대기 |
