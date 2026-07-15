# TASK-GOV-CODEX-002 Change 005 — Public main 최소 PR 강제

## 1. 사용자 발화 기준 문제

Repository를 public으로 재개한 뒤 default branch `main`에는 classic branch protection과 Repository ruleset이 없어, Repository 지침이 금지하는 direct main push를 GitHub가 서버 측에서 차단하지 않았다.

## 2. 정책 결정

1인 개발 속도를 유지하면서 기존 Repository 게시 절차의 핵심만 GitHub가 강제한다.

- `main` 변경은 pull request를 통해서만 반영한다.
- 승인 review 수는 0으로 유지한다.
- CI 통과, 최신 base 동기화, review thread 해결과 code owner review는 강제하지 않는다.
- Repository에서 이미 허용된 merge·squash·rebase 방식을 제한하지 않는다.
- 별도 bypass actor를 두지 않는다.

## 3. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 005`
- taskType: `POLICY_DECISION`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `0.6 신규 기능 Go/No-Go`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- gateStatus: `PASS_REUSE`

Roadmap 추적 항목 85가 동일 목적의 canonical Finding이다. 사용자가 Git 게시 전에 P3를 해소하도록 순서 변경과 최소 PR 강제를 승인했으므로 새 Task를 만들지 않고 Change 005로 재사용한다.

## 4. 승인 범위

- Repository ruleset 1개 생성
- 대상: default branch `main`
- enforcement: active
- rule: pull request required 1개
- 승인·status check·최신화·review thread 강제: 0
- 관련 Task·Roadmap·History SOP·User manual 상태 문서화

## 5. 제외 범위

- CI required status check
- approving review, code owner review와 last-push approval
- strict base update와 review thread resolution
- merge method 제한
- Backend·Frontend·API·DB·migration·dependency·runtime configuration 변경
- 5174·5176·5081·5092·5190·5432 중단·재시작
- commit·push·PR·merge와 branch·worktree·stash 정리

## 6. 실행 결과

- Repository visibility/default branch: `PUBLIC/main`
- Repository ruleset: `0 → 1`
- enforcement/target: `active/default branch`
- effective main rule: `pull_request` 1
- required approving review count: 0
- required status check count: 0
- strict base update/review thread/code owner/last-push approval: `false/false/false/false`
- bypass actor count: 0
- 제품 source·runtime·DB·provider 변경: 0

## 7. 검증

- Repository ruleset 목록에서 active ruleset 1개 확인
- `main` effective rules projection에서 `pull_request` 1개 확인
- 승인 0, status check 0과 네 optional gate false 확인
- ruleset 이외 GitHub Repository 설정 변경 0
- local staged/commit/push/PR/merge 0
- 문서 diff·link·anchor·privacy-safe evidence 검증
- 독립 검증에서 발견한 History Rewrite 운영 문서 current-state drift 보정·재검증

최종 검증 결과는 changed allowlist 9개 일치, staged·deleted·제품 source·runtime configuration diff 0, `git diff --check` PASS다. 추가 local link·anchor syntax와 duplicate heading은 `0/0/0`, 독립 privacy/secret 후보 email·UUID·credential assignment는 `0/0/0`이다. GitHub effective rules와 문서 Finding closure의 분리된 read-only 검증을 완료했다.

실제 direct push로 차단을 시험하지 않는다. 그 검증은 원격 `main` mutation을 시도하므로, GitHub effective rules API의 서버 측 적용 projection으로 대체한다.

## 8. 승인 상태

- policyDecisionApproved: true
- githubRulesetMutationApproved: true
- requiredPullRequestApproved: true
- requiredStatusChecksApproved: false
- requiredReviewsApproved: false
- runtimeMutationApproved: false
- userValidationComplete: true
- publishingApproved: true
- mergeApproved: true

## 9. Finding

- P0/P1: 0
- P2 `HISTORY_REWRITE_OPERATIONAL_DOC_STATE_DRIFT`: Resolved — SOP와 User manual의 과거 `PRIVATE`·public 재개 대기 표기를 실제 `PUBLIC`·required PR 상태로 동기화했다.
- P3 `PUBLIC_MAIN_SERVER_SIDE_PROTECTION_ABSENT`: Resolved — public default branch `main`에 active Repository ruleset의 required pull request가 적용돼 Repository 지침의 direct main push 금지를 서버 측에서 강제한다.
