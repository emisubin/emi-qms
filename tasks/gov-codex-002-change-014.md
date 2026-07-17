# TASK-GOV-CODEX-002 Change 014 — 실험 branch Fable 2-pass fast-track

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 014`
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

- 업무 목표: 이 실험 worktree의 신규 기능을 `Fable 1차 기획 → Codex review → review 기반 Fable 2차 기획 → 2차 기획 기준 Codex 구현`으로 중간 승인 없이 완료한다.
- Root Finding 또는 정책 결정: 기존 일반 계약은 Fable primary draft 1회와 Codex review에서 끝나고, `draft` mode도 review를 필수 source로 읽지 않아 사용자가 요구한 2-pass 실험 workflow를 영구적으로 보장하지 못한다.
- 변경·검증 경계: Root/Fable 지침, Fable read-only runner, governance 산출물과 Roadmap Decision Log. 일반 branch의 deep-interview·단일 draft·사용자 승인 계약은 보존한다.
- 보존할 불변조건: Fable read-only·원문 byte equality, 기존 1차 planning/review 보존, 서버 권한·migration 안전, Persistent UAT·실제 provider·대표 repo·`main` 무변경, local experiment commit만 허용, merge 승인 3회.
- 예상 산출물: experiment fast-track 정책, runner `second-planning` mode, 검증 결과, Roadmap 결정 이력과 local governance commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 2. 사용자 결정

- `experiment/*`에서 사용자가 fast-track을 명시한 신규 기능은 사용자-facing interview와 중간 승인 왕복을 생략한다.
- Fable 1차 planning, Codex 내용 review, Fable 2차 planning을 각각 한 번만 수행한다.
- Fable 2차 planning은 1차 planning과 Codex review를 직접 완전히 읽고 review resolution을 최종 구현 계약으로 통합한다.
- 2차 planning의 blocking decision이 0이면 Codex가 구현·검증·desktop/mobile screenshot·Implementation report·local commit까지 이어간다.
- 각 Fable planning 직전·직후 Claude `/usage`의 전체 모델과 Fable 사용·잔여 비율을 기록한다.
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider는 fast-track에 포함하지 않는다. `main` merge는 분리된 승인 3회 전까지 금지한다.

## 3. Runner 승인 계약

- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `N/A — 이 Change는 공통 runner 계약을 승인하며, 실제 Task의 exact target은 해당 Task 최신 change에 기록한다.`

실제 `second-planning` 호출은 각 기능 Task 최신 change의 exact target marker 세 개가 모두 일치해야 한다. 이 Change의 `N/A` target으로 제품 기획을 호출할 수 없다.

## 4. 구현 범위

### 포함

- Root `AGENTS.md`의 experiment-only 2-pass 예외와 사용량·local commit·merge 3회 경계
- `CLAUDE.md`의 2차 기획 작성자 계약
- `scripts/run-fable-readonly.sh`의 `second-planning` mode
- 1차 planning·Codex review·최신 approval change·exact target·experiment branch fail-closed gate
- 2차 기획 byte-identical exclusive create와 review 직접 참조 prompt
- 기존 governance Task·Implementation report·Roadmap 상태 동기화

### 제외

- 일반 branch의 Fable single-pass·deep-interview·사용자 승인 계약 변경
- 제품 Backend·Frontend·API·DB·migration·dependency·runtime·provider 변경
- 대표 repo, `main`, push, PR, merge와 worktree 정리

## 5. 승인 상태

- policyApproved: `true`
- implementationApproved: `true`
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 6. 검증 기준

- Bash syntax·ShellCheck warning 이상 0
- 일반 branch `second-planning` 차단
- 1차 planning 누락·Codex review 누락·approval marker 누락·target 불일치 차단
- existing target·symlink no-overwrite
- prompt가 1차 planning·review를 필수 source로 직접 읽도록 고정
- 일반 `planning|draft|revise` 계약 회귀 없음
- Markdown local link·duplicate heading·privacy/secret·`git diff --check`
- 제품 source·migration·dependency·runtime diff 0

## 7. 종료 산출물

자동 검증 결과:

- Bash syntax·ShellCheck: `PASS/PASS`
- `second-planning` static contract: `9/9 PASS`
- fail-closed: 일반 `main` branch 차단, experiment approval marker 누락 차단, invalid mode 차단 `3/3 PASS`
- 기존 mode 회귀: planning existing target, draft existing target, revise approval missing `3/3 PASS`
- Markdown file/local link/missing/duplicate heading: `6/124/0/0`
- 추가 diff의 email/UUID/private key/absolute user path/credential assignment 후보: `0/0/0/0/0`
- `git diff --check`: `PASS`
- 제품 Backend·Frontend·database diff: `0`

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/gov-codex-002-implementation-report.md` Change 014 | 작성·검증 후 갱신 |
| SOP | `tasks/gov-codex-002.md` 8장 | fast-track 절차 추가 |
| User manual | `tasks/gov-codex-002.md` 9장 | 실험 branch 안내 추가 |
| Roadmap update | `docs/00-product-roadmap.md` | Decision Log와 governance 상태 갱신 |
| User validation checklist | 이 문서 6장·`tasks/gov-codex-002.md` 13장 | 사용자 요청으로 정책·구현 승인, 자동 검증 완료 |
