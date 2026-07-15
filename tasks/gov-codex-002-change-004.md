# TASK-GOV-CODEX-002 Change 004 — Repository worktree cleanup과 canonical root 정규화

## 1. 사용자 발화 기준 증상

Merge가 끝난 UAT publish, 로그인 promotion과 history closure worktree가 Repository 옆에 계속 남아 있었고, 원본 `emi-qms`는 실행 중인 5174와 과거 WIP 때문에 최신 main이 아닌 branch에 머물렀다.

## 2. 기대 동작

- 상시 worktree는 canonical root와 5176 디자인 실험용 하나만 유지한다.
- Merge 완료·clean·process 미사용·Open PR 0인 임시 worktree는 정상 제거한다.
- 원본 root의 중복 WIP는 최신 main 반영 여부를 확인하고, 고유한 Decision Log만 보존한다.
- 5174는 승인된 frontend-only 중단·재시작으로 최신 main 기반 root에서 복구한다.
- Repository의 실제 공개 상태와 cleanup 결과를 canonical 문서에 반영한다.

## 3. Task Identity Gate

- taskType: `HOUSEKEEPING`
- instructionChainRead: true
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-GOV-CODEX-002`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- gateStatus: `PASS_REUSE`

같은 목적의 canonical 계약은 Change 003의 single canonical clone lifecycle이다. 새 Task를 만들지 않고 현재 cleanup batch를 Change 004로 기록한다.

## 4. 승인 범위

- PR #48·#49·#50의 clean inactive worktree 3개 제거
- local·remote branch 보존
- 원본 WIP 전체를 이름 있는 stash로 임시 보존
- 최신 `origin/main` 기반 cleanup branch로 원본 root 전환
- 5174 frontend-only 중단·재시작
- 고유 Decision Log 2행, Repository `PUBLIC` 상태와 worktree cleanup 결과 문서화

## 5. 제외 범위

- 5176 디자인 실험 worktree와 미게시 디자인 변경
- Backend 5081, Review-safe 5190/5092, PostgreSQL 5432와 Persistent UAT
- Provider call, DB write, migration, dependency와 제품 source 변경
- local·remote branch 삭제
- stash 삭제, commit·push·PR·merge

## 6. 실행 결과

- linked worktree: `5 → 2`
- 제거: UAT Change 001 publish, 로그인 promotion, history Support closure
- 보존: canonical root, 5176 디자인 실험
- 제거 대상 clean/process/Open PR: `3/3`, `0`, `0`
- 제거 branch local/remote 보존: `3/3`
- 원본 WIP: 수정 9·미추적 2를 이름 있는 stash로 보존
- WIP 14개 path 중 최신 main과 동일: 13
- Roadmap 고유 내용: 과거 승인 Decision Log 2행
- Repository visibility: 인증된 fixed projection `PUBLIC`
- 제품 source 변경: 0
- 5174 HTTPS root·health·Teams Activity: `200/200/200`
- HTTP 5174: 실패 — HTTPS-only 유지
- 5174 Entra 로그인 shell·same-origin runtime API: 정상 / `200`
- 5081·5176·5190·5092: 모두 200
- PostgreSQL health/restart: `healthy/0`
- Public main classic protection / Repository ruleset: `0 / 0`

## 7. Runtime 보존

5174는 Repository 소유 screen·Vite·strict port를 확인한 뒤 frontend session만 중단했다. 전체 UAT startup script는 Backend 5081까지 재시작하므로 사용하지 않는다. 5081·5176·5190·5092와 PostgreSQL은 중단·재시작하지 않는다.

첫 frontend-only 재시작은 HTTPS server와 proxy target만 복원하고 `VITE_API_BASE_URL`의 same-origin 값과 Repository root의 기존 Entra local 설정 로드를 누락했다. 그 결과 정적 화면은 200이었지만 browser runtime은 기본 5080 연결 실패로 API·User·실행 모드를 읽지 못했고, 중간 복구에서는 개발 인증 또는 설정 누락 shell이 표시됐다. 사용자 보고 뒤 5174 frontend만 다시 중단하고 기존 local Entra 설정을 값 노출 없이 로드한 다음 `EntraId`, `https://localhost:5174` redirect와 same-origin API를 명시해 정상 로그인 shell로 복구했다. Backend·DB·다른 runtime restart는 0이다.

## 8. 검증과 cleanup gate

- worktree registry와 disk path 결과 확인
- local·remote branch 보존 확인
- root branch·HEAD·dirty/stash 상태 확인
- 5174 HTTPS, wrong-protocol, proxy health와 5081 live/ready 확인
- privacy-safe browser에서 Entra 로그인 shell과 설정 누락·실행 모드 오류 부재 확인
- 5176·5190·5092와 PostgreSQL health/restart 불변 확인
- 문서 link·anchor·duplicate heading·diff check·privacy/secret 검사
- Backend·Frontend·migration·dependency·제품 source diff 0 확인

## 9. 승인 상태

- cleanupApproved: true
- frontend5174StopRestartApproved: true
- canonicalRootNormalizationApproved: true
- designExperimentPreservationApproved: true
- branchDeletionApproved: false
- stashDeletionApproved: false
- publishingApproved: true
- mergeApproved: true

## 10. 현재 상태

- 구현·자동·독립 재검증: 완료
- 사용자 검수: 완료
- stash: 보존 중, 14개 path
- commit·push·PR·merge: 사용자 승인 / 실행 대기

## 11. Finding

- P0/P1/P2: 0
- P3 `PUBLIC_MAIN_SERVER_SIDE_PROTECTION_ABSENT`: Change 005의 active required-pull-request ruleset으로 Resolved. 승인 review와 required status check는 1인 개발 속도 정책에 따라 강제하지 않는다.
