# TASK-EXPERIMENT-PROMOTION-001 Change 002 — Local Main과 Azure Main 통합

## Task Identity Gate

- proposedTaskId: `TASK-EXPERIMENT-PROMOTION-001 Change 002`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `AZURE_PRE_TRAFFIC_GATE`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPERIMENT-PROMOTION-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 사용자 검수가 끝난 5174 local `main` 기준선과 Azure 변경이 먼저 병합된 GitHub `main`을 하나의 검증 가능한 계보로 통합하고 Ready PR·CI를 거쳐 원격 `main`에 반영한다.
- Root Finding: local 제품 기준선은 DESIGN-000 Change 006과 TASK-UAT-001 Change 007을 포함하지만 원격 미게시이며, 원격 `main`은 Azure Change 003~005를 포함해 양쪽이 분기됐다. 한쪽을 덮어쓰면 사용자 검수 완료 UI 또는 Azure 보안·배포 변경이 유실된다.
- 변경·검증 경계: 원격 `main`을 기준으로 local `main`의 6개 commit 계보를 merge commit으로 통합하고, 충돌·회귀·Azure artifact·privacy·문서 상태를 검증한 뒤 branch push, Ready PR, CI 성공과 mergeable 상태를 확인해 merge한다.
- 보존할 불변조건: 5174·5081·5175 runtime과 Persistent UAT를 변경하지 않는다. UL891 미커밋 작업, migration `0068`, 실제 Azure resource·DNS·traffic·provider와 다른 dirty worktree를 포함하지 않는다. direct `main` push와 squash로 local main 계보를 평탄화하지 않는다.
- 예상 산출물: 통합 merge commit, Task 상태 동기화, 전체 자동 검증, Ready PR과 merge commit 방식의 원격 `main` 반영.

## 사용자 승인과 순서 변경

- 사용자는 5174를 현재 제품 기준선으로 확정했다.
- 사용자는 Azure 작업과 5175 UL891 작업의 관계를 확인한 뒤 5174 기준선 통합을 `1단계`로 먼저 실행하라고 승인했다.
- 승인 범위는 별도 integration branch/worktree, 문서 상태 동기화, 검증, commit, push, Ready PR, CI 확인과 원격 `main` merge를 포함한다.
- 실제 Azure mutation·재배포와 5175 UL891 porting은 이번 변경에서 제외한다.

## 기준선과 임시 작업공간

- 원격 `main`: `69a725880f2da67589f18d321a9fb71b0540c79f`
- 5174 local `main`: `07718bc19d5cb91afb47737895849086d9543590`
- 공통 조상: `4d15b7cee0d97f1846a1838500f9c9edf11b68bf`
- integration branch: `fix/task-experiment-promotion-001-main-integration`
- integration merge commit: `33fffeafe9346cc4e475920dd4e63f9887c7b3b7`
- merge result: 충돌 없음. Azure 원격 기준선과 5174 local 제품 기준선의 두 parent를 모두 보존
- 임시 worktree 목적: 위 두 기준선의 병합·검증·게시 전용
- runtime ownership: 없음. 5174·5081·5175 process source로 사용하지 않는다.
- cleanup 경계: merge 후 clean·commit reachable·process 미사용을 확인하되 별도 사용자 승인 전 worktree와 branch를 삭제하지 않는다.

## 포함·제외 범위

### 포함

- DESIGN-000 Change 006 Graphite UI와 탐색 구조
- TASK-UAT-001 Change 007 Pending 상세 mixed-version 복구
- 원격 `main`의 Azure Change 003~005 전체
- 위 상태와 실제 Azure readiness를 반영하는 Roadmap·Implementation report·검수 상태 동기화
- Backend·Frontend·mock UI·isolated Full-Stack·Azure artifact·CI 검증

### 제외

- `fix/task-ul891-set-001-user-corrections`와 5175/5082의 모든 dirty·untracked 변경
- migration `0068`과 UL891 1~7 수정
- Persistent UAT migration·seed·data·runtime handover
- Azure resource, image, DNS, TLS, traffic, Teams·Gmail provider mutation
- 다른 worktree의 evidence·local parameter·untracked artifact와 branch/worktree 삭제

## 완료 Gate

1. merge conflict와 범위 밖 변경이 없거나 승인 범위 안에서 해소된다.
2. `git diff --check`, allowlist, secret·PII·generated artifact와 문서 link 검사가 통과한다.
3. Backend Release build·전체 test, Frontend lint·typecheck·unit·build, mock UI E2E, isolated Full-Stack E2E와 Azure artifact 검증이 통과한다.
4. Open P0/P1/P2가 0이고 기존 P3는 canonical backlog에 연결된다.
5. Ready PR 최신 head의 GitHub CI가 성공하고 mergeable일 때 merge commit 방식으로 원격 `main`에 병합한다.

## Rollback

- PR merge 전에는 integration branch를 보존하고 merge를 중단한다.
- PR merge 후에는 merge commit을 되돌리는 revert PR 또는 forward-fix PR을 사용한다.
- 이번 변경은 runtime·DB·Azure resource를 바꾸지 않으므로 운영 resource rollback은 적용 대상이 아니다.
