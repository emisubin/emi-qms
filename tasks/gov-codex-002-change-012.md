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

## 8. 실행 결과

- Governance source preservation commit: `4058849`
- USER-FLOW source preservation commit: `1cc66fe`
- Governance selective result commit: `a6232b2`
- USER-FLOW normalized result commit: `c4b2858`
- Governance 정책 final owner: `fix/task-gov-codex-002-runtime-reporting-011`
- USER-FLOW 산출물 final owner: `feat/task-user-flow-001-website-flow`
- Fable 원문 blob equality: `true`
- USER-FLOW 최종 branch의 중복 Fable 정책·runner diff: `0`
- USER-FLOW 최종 branch의 제품 source diff: `0`
- 제거한 일반 worktree: Governance source 1, USER-FLOW source 1
- 제거 방식: 두 worktree 모두 `git worktree remove`, force·`rm -rf`·branch 삭제 없음
- 최종 worktree registry: 대표 1, 디자인 1, 합계 `2/2`
- 5174·5176·Backend·DB restart 또는 runtime mutation: `0`

USER-FLOW worktree는 처음 확인할 때 terminal과 Fable process handle이 남아 있어 제거를 중단했다. 사용자가 terminal 종료를 확인한 뒤 handle `0`을 다시 확인하고 일반 제거했다. Runtime 소유 process를 종료하거나 강제 제거하지 않았다.

## 9. 검증·Finding 상태

- `git diff --check`: PASS
- Governance Bash syntax·ShellCheck: `PASS/PASS`
- Runner safe negative contract: `4/4` stable failure
- Project Rule execpolicy: runner allow 1, generic bash/zsh wrapper prompt 2
- Governance Markdown: 변경 Markdown 17개, local link `127/127`, duplicate heading 0, privacy candidate 0
- USER-FLOW Markdown: 변경 문서 10개, local link target 누락 0, duplicate heading 0, Mermaid block/fence `2/0`, privacy candidate 0
- Changed-file allowlist·제품 source·migration·dependency·runtime configuration: PASS
- Fable 원문 source/destination blob equality: PASS
- Runtime·worktree 보존 projection: URL `5/5` HTTP 200, listener `4/4`, PostgreSQL `running/healthy/restart 0`, worktree `2/2`
- Preservation/result commit named-branch reachability: `4/4`; 관련 open PR: `0`
- P0/P1/P2: `0/0/0`
- P3 `PAUSED_WORKTREE_INTEGRATION_PENDING`: `RESOLVED` — 두 source WIP를 local commit으로 고정하고 일반 worktree 제거 완료
- P3 `USER_FLOW_P3_001_IDENTITY_TRACE_GAP`: `RESOLVED` — USER-FLOW Change 003·report·Roadmap에 문서 작성 승인과 제품 구현·게시 미승인을 분리 기록
- Finding `WORKTREE_PROCESS_HANDLE_ACTIVE`: `RESOLVED` — 사용자 terminal 종료 뒤 handle 0을 확인하고 force 없이 제거
- 독립 Codex 검증: 대기
- 사용자 검수·Git 게시: 대기
