# TASK-GOV-CODEX-002 Change 011 — 대표 5174 branch-following 운영

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-CODEX-002 Change 011`
- taskType: `DOCS_GOVERNANCE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `0.6 신규 기능 Go/No-Go`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

Change 007~010은 별도 dirty worktree에서 진행 중인 Fable·USER-FLOW governance WIP이므로 번호를 재사용하거나 파일을 이 branch로 임의 이식하지 않는다. 이번 독립 정책 보정은 다음 번호인 Change 011로 기록한다.

### Purpose identity

- 업무 목표: 일반 Task를 대표 clone 하나에서 수행하면서 HTTPS 5174 Vite server를 branch 전환마다 중단하지 않는다.
- Root Finding: canonical clone 재사용 규칙과 실행 중 process 소유 시 branch 전환 금지가 결합돼, 대표 clone의 5174가 켜져 있으면 일반 문서·Frontend Task도 새 worktree를 만들거나 server를 중단해야 했다.
- 변경 경계: Root Repository 작업 격리 규칙, Task 운영 문서와 Roadmap 상태다.
- 보존할 불변조건: clean branch 전환, current WIP 보존, HTTPS-only 5174, Backend 5081·다른 runtime·DB·migration·provider의 별도 승인 경계, 디자인 5176 독립성이다.
- 예상 산출물: 5174 branch-following·조건부 재시작 정책과 대표 clone branch 전환 검증이다.

## 2. 사용자 결정

- 5174는 대표 폴더의 현재 branch를 그대로 반영한다.
- Clean branch 전환 시 Vite server를 유지하고 HMR 또는 full reload 뒤 화면을 확인한다.
- `.env*`·process env, dependency·lockfile·`node_modules`, Vite config·plugin·HTTPS certificate·port·proxy·startup command 변경이 있을 때만 선제 재시작한다.
- 자동 갱신 실패, stale module, 빈 화면 또는 지속 오류가 있을 때 재시작한다.
- 5174가 실행 중이라는 이유만으로 새 worktree를 만들지 않는다.
- 5174는 현재 branch Development runtime이며 latest-main 고정 runtime 또는 review-safe candidate가 아니다.
- Clean·reachable branch의 open PR이나 게시 승인 대기는 branch 전환을 막지 않고 완료 보고의 중단·보류 Task로 추적한다.
- Dirty WIP, 이름 없는 stash, 보존되지 않은 detached commit과 source-of-truth 미기록 상태에서는 branch를 전환하지 않는다.

## 3. 적용 범위

### 포함

- Root `AGENTS.md`의 canonical clone·5174·bounded worktree 규칙
- `TASK-GOV-CODEX-002` Task·Implementation report
- Product Roadmap 추적과 Decision Log
- 대표 clone의 최신 `origin/main` 기반 branch 전환과 5174 유지 확인

### 제외

- Frontend·Backend 제품 source 변경
- 환경변수·dependency·Vite 설정·인증서·runtime configuration 변경
- 5174·5176·5081·5092·5190·5432 중단·재시작
- Persistent UAT DB·volume·migration·worker·provider 변경
- Change 007~010과 `TASK-USER-FLOW-001` dirty WIP 수정·이식·정리
- commit·push·PR·merge와 branch·worktree cleanup

## 4. 기존 추가 worktree 해석

대표 clone 하나에서 개발한다는 운영 의도와 기존 지침의 process ownership 규칙 사이에 충돌이 있었다. 5174가 대표 clone을 사용 중인 동안 literal rule을 지키기 위해 Change 007 governance와 USER-FLOW 기획 worktree가 추가됐다. 이는 Vite의 기술적 필요가 아니며, 해당 시점 정책을 따라 WIP를 보호하려는 선택이었다.

현재 두 worktree는 dirty WIP를 보유하므로 이 Change에서 삭제하거나 대표 clone에 덮어쓰지 않는다. 각각의 사용자 검수·게시 판단 뒤 대표 clone으로 안전하게 통합하고 cleanup 승인을 별도로 받는다. 새 일반 Task부터는 이 Change의 대표 clone 정책을 사용한다.

## 5. 검증과 완료 Gate

- 최신 remote `main`과 local `origin/main` SHA 일치
- 대표 clone clean 상태에서 최신 `origin/main` 기반 branch 전환
- branch 전환 전후 5174 중단·재시작 명령 0
- HTTPS 5174 root 응답과 필수 화면 확인
- Backend·DB·다른 runtime mutation 0
- 제품 source·dependency·migration·runtime configuration diff 0
- 문서 link·heading·diff·privacy·secret·allowlist 검증
- 사용자 검수와 별도 Git 게시 승인

## 6. 승인 상태

- policyApproved: `true`
- implementationApproved: `true`
- branchSwitchWith5174RunningApproved: `true`
- runtimeRestartApproved: `false`
- pausedWorktreeMutationApproved: `false`
- publishingApproved: `false`
- mergeApproved: `false`

## 7. 실행 결과

- remote `main`과 local `origin/main` 기준: 일치
- 대표 clone: 최신 `origin/main` 기반 Change 011 branch 전환 완료
- 5174 중단·재시작 명령: `0/0`
- HTTPS 5174 root·Teams Activity·live·ready: `200/200/200/200`
- 잘못 조회한 `/api/health`: `404` — 제품 오류가 아니라 실제 health contract가 `/health/live`·`/health/ready`인 경로 선택 오류이며 올바른 두 endpoint로 재검증했다.
- Backend·DB·다른 runtime mutation: `0`
- 제품 source·dependency·migration·runtime configuration diff: `0/0/0/0`

이번 branch 전환은 제품 source가 같은 기준선 사이의 문서 Task 전환이므로 실제 Frontend module 변경에 대한 HMR 시각 검증은 수행하지 않았다. Server를 유지한 branch 전환과 필수 route 생존을 검증했고, 향후 첫 Frontend source branch 전환에서는 HMR 또는 full reload 후 화면 확인을 적용한다.

## 8. Finding

- `CANONICAL_VITE_PROCESS_BRANCH_SWITCH_CONFLICT` / P3 / `RESOLVED_IN_CHANGE_011`: 대표 clone 재사용과 5174 process ownership 규칙의 충돌이 일반 Task worktree 추가를 유도했다. 제품·데이터 손상은 없었고 5174를 branch-following runtime으로 명시해 해소했다.
- `PAUSED_WORKTREE_INTEGRATION_PENDING` / P3 / `BACKLOG`: Change 007~010 governance WIP과 USER-FLOW WIP가 별도 dirty worktree에 남아 있다. 해당 Task 사용자 검수·게시 결정 뒤 대표 clone 통합과 승인된 cleanup으로 해소한다.
- `USER_FLOW_P3_001_IDENTITY_TRACE_GAP` / P3 / `BACKLOG`: USER-FLOW WIP Implementation report는 과거 `P3-001` 해소 사실만 남기고 원래 label·증상·영향을 보존하지 않았다. 주변 실행 이력상 Fable preview의 첫 비공백 H1 contract 보정과 함께 해소된 문서 품질 Finding으로 보이지만 원래 review가 덮어써져 단정할 수 없다. USER-FLOW 게시 전 stable label·원인·영향·해소 근거를 복원하거나 `UNKNOWN_HISTORICAL_DETAIL`로 정직하게 기록한다.

## 9. 검증 상태

- `git diff --check`: PASS
- Markdown file/local link/missing: `11/127/0`
- duplicate heading: `0`
- Runtime policy static contract: `5/5`
- PII·UUID·private key·credential assignment·absolute user path candidate: `0/0/0/0/0`
- staged·deleted·product source diff: `0/0/0`
- 5174 root·Teams Activity·live·ready: `200/200/200/200`
- 5176 root·5081 live·5190 root·5092 live: `200/200/200/200`
- automaticValidationComplete: `true`
- independentVerificationComplete: `false`
- userValidationComplete: `false`
- publishGate: `NO_GO_INDEPENDENT_VERIFICATION_AND_USER_VALIDATION_PENDING`
