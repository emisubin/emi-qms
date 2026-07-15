# TASK-GOV-CODEX-002 Change 012 — Fable·USER-FLOW 선별 이식과 worktree 정규화

## 1. Task Identity·Roadmap Gate

- proposedTaskId: `TASK-GOV-CODEX-002`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- changeId: `Change 012`
- taskType: `HOUSEKEEPING`
- instructionChainRead: `true`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

Purpose identity는 Change 011의 P3 `PAUSED_WORKTREE_INTEGRATION_PENDING`을 해소하면서 대표 clone 한 곳에서 일반 branch를 전환하고 5176 디자인 worktree만 별도 유지하는 것이다. 사용자는 기존 “사용자 검수·게시 뒤 통합” 순서를 재정렬하고 로컬 보존·결과 커밋과 일반 worktree 제거를 승인했다.

## 2. 승인 범위

- Fable Change 007~010 정책·runner·Task 기록 선별 이식
- USER-FLOW Fable 원문·interview·planning·review·change·report 선별 보존
- 기존 두 dirty worktree의 exact WIP local preservation commit
- 대표 governance branch와 USER-FLOW branch의 local result commit
- clean·reachable·open PR 0·runtime 미소유 worktree의 일반 `git worktree remove`
- 5174·5176·Backend·DB 보존

## 3. 제외 범위

- Fable 원문 편집 또는 새 redraft
- Frontend·Backend·API·DB·migration·dependency·runtime configuration 변경
- Persistent UAT write와 provider 호출
- Push·PR·merge·branch 삭제
- 강제 worktree 제거, `rm -rf`, 자동 stash

## 4. 선별 이식 계약

### Governance branch

- 현재 Change 011의 5174 branch-following 정책과 `TASK-GOV-REPORTING-001 Change 001`을 보존한다.
- `.codex/rules/project-safety.rules`, `AGENTS.md`, `CLAUDE.md`, `scripts/run-fable-readonly.sh`, Change 007~010과 GOV Task·report의 해당 section만 병합한다.
- Roadmap은 whole-file overwrite 없이 Change 007~012·USER-FLOW 상태와 Decision Log만 합친다.

### USER-FLOW branch

- `docs/13-user-flow-baseline.md`는 source preservation commit과 byte-for-byte 동일하게 보존한다.
- Interview, planning, 기술 review, 내용 review, Change 001·002와 Implementation report를 보존한다.
- 중복 `.codex` Rule, Root/Fable 지침과 runner는 USER-FLOW 최종 tree에서 제외하고 governance branch를 단일 정책 source로 유지한다.
- `implementationApproved: true` 같은 과거 표현은 Fable 원문을 수정하지 않고 Change 003·Implementation report·Roadmap에서 “문서 작성 실행 승인 / 제품 구현 미승인”으로 정규화한다.
- 개인 참고 자료이며 public 게시와 merge는 승인되지 않았다.

## 5. Cleanup Gate

1. Source WIP changed path allowlist·diff·privacy를 확인한다.
2. 각 source branch에 exact local preservation commit을 만든다.
3. Worktree가 clean이고 HEAD가 이름 있는 branch에서 reachable하며 open PR이 0인지 확인한다.
4. 대표 branch의 선별 결과와 source hash를 검증하고 local result commit을 만든다.
5. Process handle이 있으면 owner·cwd·command type을 확인하고 불명확하거나 runtime 소유면 제거하지 않는다.
6. Gate를 통과한 worktree만 force 없이 제거한다.
7. 최종 worktree registry가 대표·디자인 `2/2`인지 확인한다.

## 6. 검증 계획

- `git diff --check`, Markdown local link·heading·Mermaid 정적 검사
- Bash syntax·ShellCheck와 runner safe negative contract
- Project Rule의 runner allow·generic shell prompt 유지
- Fable 원문 source/destination hash equality
- changed path allowlist, staged·deleted·generated artifact·secret/PII 0
- Frontend·Backend·migration·dependency·runtime configuration diff 0
- 5174·5176·5081·DB process 보존 projection
- worktree registry와 branch reachability 확인

## 7. 승인 상태

- sequenceOverrideApproved: `true`
- selectiveTransplantApproved: `true`
- localPreservationCommitApproved: `true`
- localResultCommitApproved: `true`
- normalWorktreeRemovalApproved: `true`
- runtimeRestartApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- branchDeletionApproved: `false`
