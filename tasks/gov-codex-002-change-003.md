# TASK-GOV-CODEX-002 Change 003 — 재사용 worktree lifecycle

## 1. 사용자 발화 기준 증상

Task마다 같은 Repository 파일을 가진 worktree가 계속 남고, 각 폴더에 `node_modules`와 Backend build artifact가 반복 생성돼 local 디스크가 지속적으로 증가했다.

## 2. 기대 동작

일반 Task는 fresh canonical clone 하나에서 branch만 바꿔 순차 수행한다. Task 자료는 `tasks/*.md`, commit과 PR에 누적하고 Task별 source 폴더는 남기지 않는다. Runtime 또는 병렬 검증처럼 물리 격리가 필요한 예외만 bounded worktree로 관리한다.

## 3. 확인된 원인

- Root 지침이 Task별 전용 worktree를 기본으로 요구했지만 merge 뒤 lifecycle과 재사용 기준이 없었다.
- 30개 linked worktree는 Git object를 공유했지만 각 working tree의 dependency와 build artifact는 공유하지 않았다.
- 약 6.04GB 중 약 5.77GB가 반복 생성된 `node_modules`와 Backend `bin/obj`였다.
- Task 문서 129개 자체는 약 1.3MB로 용량 증가의 원인이 아니었다.

## 4. 승인된 운영 모델

- canonical clone: fetch, 일반 Task 구현과 history 관리의 유일한 대표 workspace
- runtime worktree: 실제 process가 source ownership을 요구할 때만 유지
- temporary worktree: 병렬 write, runtime handover, history/migration rehearsal에 한정하고 cleanup 조건을 생성 시 기록
- Codex session 분리와 source folder 분리를 동일하게 취급하지 않음

## 5. 포함 범위

- Root `AGENTS.md` worktree lifecycle
- `TASK-GOV-CODEX-002` SOP·사용자 안내·Implementation report와 Roadmap 갱신
- merged·clean·process 미사용·open PR 없음·commit reachable worktree 정리
- 기존 Backend Format worktree를 cleanup·게시 전 임시 `current`로 전환한 뒤 fresh canonical clone으로 대표 경로 교체

## 6. 제외 범위

- dirty worktree, stash와 사용자 WIP 수정·이전·삭제
- 실행 중 Development·Review-safe·Candidate runtime 종료·재시작·source handover
- local/remote branch 자동 삭제
- history rewrite backup·raw artifact와 GitHub Support 상태 변경
- Persistent UAT, migration, product source와 dependency manifest 변경
- Commit, push, PR과 merge

## 7. 영향 파일

- `AGENTS.md`
- `tasks/gov-codex-002-change-003.md`
- `tasks/gov-codex-002.md`
- `tasks/gov-codex-002-implementation-report.md`
- `docs/00-product-roadmap.md`

## 8. 보존할 불변조건

- `main` 직접 개발·push 금지와 Task별 branch 사용을 유지한다.
- dirty, process 사용, open PR과 unreachable detached commit은 자동 정리하지 않는다.
- branch는 worktree 제거와 별개로 보존하며 별도 승인 없이 삭제하지 않는다.
- Runtime·Persistent UAT·provider와 실제 사용자 데이터는 변경하지 않는다.
- 정리는 `git worktree remove`를 사용하고 강제 filesystem 삭제를 사용하지 않는다.

## 9. 검증 방법

- worktree before/after count와 disk usage 비교
- removed 대상의 clean·process 미사용·open PR 0·commit reachable 확인
- dirty·runtime worktree 보존 count 확인
- 게시 branch가 최신 `origin/main` 기준이며 tracked diff가 문서 allowlist인지 확인
- merge 후 fresh clone의 HEAD·tracked tree·remote와 동작 검증 뒤 canonical 대표 경로 지정
- Markdown link·anchor·heading, `git diff --check`, secret/PII와 generated artifact 검사
- Backend·Frontend·migration·dependency·script·runtime source diff 0

## 10. 사용자 승인 상태

- approved: true
- approvedAt: 2026-07-14
- selectedOption: `SINGLE_CANONICAL_CLONE_WITH_BOUNDED_RUNTIME_WORKTREES`
- cleanInactiveWorktreeCleanupApproved: true
- representativeCloneTransitionApproved: true
- existingFolderDeletionApproved: false
- dirtyWorktreeCleanupApproved: false
- runtimeRestartApproved: false
- branchDeletionApproved: false
- publishingApproved: true
- mergeApproved: true
