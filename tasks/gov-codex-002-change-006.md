# TASK-GOV-CODEX-002 Change 006 — Local GitHub 폴더 보존 통합과 최종 삭제

## 1. 사용자 발화 기준 문제

대표 Repository와 5176 디자인 실험 폴더 외에 과거 clone, history rewrite checkout, preserved clone과 worktree 상위 폴더가 GitHub 폴더 바로 아래에 남아 있었다. 1차 승인에서는 삭제 조건이 불명확한 자료를 접근 제한 보존 폴더로 통합했다. 이후 사용자는 보존 폴더 전체를 다시 감사해 삭제 가능 여부를 확정하고, Docker/PostgreSQL controlled maintenance와 stale handle 해제를 포함한 안전한 완전 삭제를 승인했다.

## 2. 기대 동작

- GitHub 폴더 바로 아래에는 대표 `emi-qms`와 디자인 실험 `emi-qms-task-design-login-001`만 둔다.
- 보존 폴더의 dirty checkout, branch, worktree, history, local 설정을 정확히 감사해 canonical 자료가 없는 경우에만 삭제한다.
- Docker가 연 과거 bind-mount handle은 Persistent PostgreSQL volume을 보존하는 controlled maintenance로 해제한다.
- 삭제 전후 동일 PostgreSQL container·volume, DB aggregate와 5174·5176·5081·5092·5190·5432 runtime 상태를 대조한다.
- Repository 밖 encrypted history backup은 변경하지 않는다.

## 3. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 006`
- taskType: `HOUSEKEEPING`
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

### Purpose identity

- 업무 목표: 활성 개발 폴더와 삭제 가능한 과거 checkout의 물리 경계를 명확히 하고 최상위 폴더를 대표·디자인 두 개로 정리한다.
- Root Finding 또는 정책 결정: Task별 checkout 누적을 막는 single canonical clone lifecycle을 유지한다.
- 변경·검증 경계: 보존 자료 exact audit, dirty checkout clean-up, local checkout 영구 삭제, stale Docker handle 해제와 runtime·DB 불변 검증이다.
- 보존할 불변조건: 대표·디자인 source와 runtime, Persistent PostgreSQL volume·data, 현재 canonical branch·stash, remote branch와 encrypted history backup을 보존한다.
- 예상 산출물: GitHub 최상위 폴더 2개와 privacy-safe 삭제·controlled maintenance 기록이다.

## 4. Exact audit 결과

- 1차 정리 전 GitHub 최상위 폴더: 6
- 1차 보존 통합 후 최상위 폴더: 3
- 보존 폴더 크기: 약 684MB
- 감사한 dirty checkout: 6/6
- 감사한 local branch: 32/32
- local tag: 0
- 보존 폴더 내부 encrypted backup artifact: 0
- open PR이 있는 보존 branch: 0
- 보존 폴더를 source cwd로 사용하는 application process: 0

Checkout별 diff는 다음 이유로 canonical 보존 필요성이 없었다.

- 과거 permission alignment 코드는 현재 main에 반영됐거나 더 최신 구현으로 대체됐다.
- History Rewrite 문서는 pre-closure `PRIVATE`·`SUPPORT_PENDING` 상태로 현재 `PUBLIC`·`REMOVED` 종료 문서에 의해 대체됐다.
- Finding Gate 문서는 pre-closure `NO_GO`·Open P2 상태로 현재 GO·Open P2 0 종료 문서에 의해 대체됐다.
- 과거 Root `AGENTS.md`·`CLAUDE.md` 장문 초안은 현재 instruction chain에 채택되지 않았고 canonical 지침으로 대체됐다.
- UAT 계획 초안은 현재 Phase A~D 산출물과 실제 handover 기록으로 실현·대체됐다.
- local env·certificate는 현재 canonical local 설정의 중복이었고 credential 값을 tracked 문서에 기록하지 않았다.
- `node_modules`, build/test 결과와 browser artifact는 재생성 가능한 산출물이었다.

Branch 32개는 main reachable 5개, tree-equivalent 25개, 개별 검토 2개였다. 나머지 UAT branch의 20/20 commit subject는 main history에서 확인했고, historical docs branch는 remote copy가 존재하며 open PR 0이고 내용은 현재 지침·종료 정책으로 대체됐다. 따라서 보존 폴더에는 유일한 canonical source, 미게시 제품 기능, 필요한 audit 원장 또는 유일한 복구 자료가 남아 있지 않았다.

## 5. 승인 범위

### 1차 보존 통합

- mode `0700`의 `emi-qms-preservation` 생성
- 과거 상위 폴더 4개 이동
- legacy Git repository 2개의 linked worktree 3개 repair
- 최신 `origin/main` 기준 Change 006 branch와 추적 문서 생성

### 최종 삭제

- 보존 폴더 전체 exact audit와 삭제 판정
- dirty checkout 6개를 강제 reset 없이 local stash로 clean 상태 전환
- Docker/PostgreSQL controlled maintenance와 stale bind-mount handle 해제
- `emi-qms-preservation` 전체 영구 삭제
- 동일 PostgreSQL container·persistent volume 재사용과 runtime·DB 전후 검증
- Change 006·Task·Implementation report·Roadmap 동기화

## 6. 제외 범위

- 대표 `emi-qms`와 디자인 `emi-qms-task-design-login-001` 삭제·branch 전환·stash 삭제
- Repository 밖 encrypted history backup 삭제
- remote branch·tag 삭제
- Persistent PostgreSQL volume 삭제·reset·drop·truncate
- Backend·Frontend·API·migration·dependency·runtime configuration 변경
- Escalation·Purge·provider 발송 또는 Persistent UAT data write
- commit·push·PR·merge

## 7. 실행 결과

- GitHub 최상위 폴더: `6 → 3 → 2`
- 최종 폴더: 대표 1, 디자인 1
- 영구 삭제: `emi-qms-preservation` 1개, 약 684MB
- 감사·clean 처리한 checkout: `6/6`
- 강제 reset·강제 worktree remove: 0
- canonical worktree registry: 대표·디자인 `2/2`
- canonical Repository branch·stash 변경: 0
- remote branch·tag 변경: 0
- encrypted history backup 변경: 0

Git worktree 제거 명령은 실행 환경 정책에서 차단됐다. 모든 linked worktree와 그 소유 repository가 같은 삭제 대상 폴더 안에 있고 checkout 6개를 먼저 clean 상태로 만든 뒤, Finder의 exact-path 영구 삭제로 보존 폴더 전체를 한 번에 제거했다. 대표와 디자인 폴더는 삭제 대상에서 제외했고 최종 목록으로 재확인했다.

## 8. Controlled maintenance 결과

보존 폴더에는 Docker Desktop VM이 과거 PostgreSQL bind mount로 연 read-only 경로 4개가 남아 있었다. PostgreSQL container만 중지한 뒤에도 handle이 유지돼 Docker Desktop의 `Restart Docker Desktop`을 실행했다. Docker Desktop이 안내한 대로 container와 설정을 보존하는 재시작만 사용했고 data cleanup·factory reset·volume 삭제는 수행하지 않았다.

- stale open path: `4 → 0`
- PostgreSQL container ID: 전후 동일
- container state/health: 재기동 후 `running/healthy`
- restart count: `0`
- mount: canonical read-only init bind 1 + 동일 persistent volume 1
- persistent volume: `infrastructure_emi-qms-postgres-data` 유지
- DB aggregate 전후: `29/14/10/39/98/101` 동일
- listener PID 전후: 5174·5176·5081·5092·5190·5432 모두 동일

DB aggregate는 schema migration, 사용자, 역할, 업무, 알림, 알림 delivery의 privacy-safe count projection이며 raw row나 credential은 출력하지 않았다.

## 9. 검증 결과

- GitHub 최상위 exact directory: 대표·디자인 `2/2`
- 삭제 대상 path absent: true
- canonical worktree registry: 대표·디자인 `2/2`
- 동일 PostgreSQL container·volume·mount: PASS
- PostgreSQL healthy/restart: `healthy/0`
- Persistent DB aggregate 전후 일치: PASS
- known listener PID 존재·전후 일치: `6/6`, `6/6`
- 5174 root·live, 5081 live·ready, 5176 root, 5190 root, 5092 live: 모두 `200`
- changed allowlist: 문서 4개
- staged·제품 source·migration·runtime configuration diff: `0/0/0/0`
- independentVerification: `PASS`

첫 독립 projection은 Git porcelain 첫 줄의 leading space를 제거해 tracked unstaged 파일을 staged로 오판했다. 정확한 `git diff --cached --quiet`, cached name count와 leading-space-preserving porcelain parse로 staged 0·allowlist exact 4를 재현해 `GIT_VALIDATION_STATE_DRIFT`를 false positive로 해소했다. 최종 독립 검증은 filesystem, Git, Docker/PostgreSQL, DB aggregate, 7개 URL, 6개 listener, diff·Markdown·privacy와 문서 일관성을 모두 다시 확인했다.

## 10. 승인 상태

- folderConsolidationApproved: true
- preservationContainerApproved: true
- dirtyCheckoutDeletionApproved: true
- localPreservationRepoDeletionApproved: true
- controlledDockerMaintenanceApproved: true
- runtimeMutationApproved: true
- historyBackupDeletionApproved: false
- remoteBranchDeletionApproved: false
- publishingApproved: false
- mergeApproved: false
- independentVerificationComplete: true
- userValidationComplete: false

## 11. Finding과 후속 경계

- P0/P1/P2/P3: 0/0/0/0
- publishGate: `NO_GO_USER_VALIDATION_PENDING`
- Repository 밖 encrypted history backup은 이번 작업과 분리해 보존한다.
- 5176 디자인 experiment worktree는 사용자의 장기 디자인 source·runtime이므로 계속 유지한다.
- Roadmap의 다음 제품 Gate는 계속 `0.6 신규 기능 Go/No-Go`다.
