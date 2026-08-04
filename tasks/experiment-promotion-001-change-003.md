# TASK-EXPERIMENT-PROMOTION-001 Change 003 — 통합 main 기준 UL891 수정 이식

## Task Identity Gate

- proposedTaskId: `TASK-EXPERIMENT-PROMOTION-001 Change 003`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `UL891_PORTING_AND_REGRESSION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPERIMENT-PROMOTION-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 5175에서 사용자 검수한 UL891 생산계획·현재 설계 수정분을 통합된 원격 `main` 기준선에 선택 이식하고, Graphite 화면 구조와 Azure 변경을 보존한 전체 회귀 후보를 만든다.
- Root Finding: 5175 수정본은 통합 전 `main`에서 시작해 현재 Graphite·Pending 복구·Azure 계보를 포함하지 않는다. 기존 branch를 그대로 병합하면 현재 제품 디자인과 기능을 퇴행시킬 수 있으므로 통합 기준선 위에 승인된 UL891 변경만 다시 적용해야 한다.
- 변경·검증 경계: `TASK-UL891-PRODUCTION-PLAN-001 Change 002~008`, `TASK-UL891-SET-001 Change 009`, additive migration `0068`, 관련 Backend·Frontend·unit·Full-Stack test와 문서 상태를 현재 구조에 맞춰 이식하고 전체 회귀한다.
- 보존할 불변조건: Graphite 정보구조·디자인, Azure Change 003~005, Pending 상세 복구, 기존 UL891 물리 패널 ID·취소 이력·권한·CAS·audit, 비-UL891 흐름, 18단계 workflow를 유지한다. 5174/5081·5175/5082 source/runtime, Persistent UAT, Azure resource·traffic·실제 provider와 다른 dirty worktree를 변경하지 않는다.
- 예상 산출물: 통합 기준선 기반 UL891 port branch, migration `0068`, 현재 UI 구조에 맞춘 코드·테스트, 전체 자동 검증 결과, 사용자 검수 대기 상태의 구현 보고와 체크리스트.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 기준선과 임시 작업공간

- 통합 기준선: `origin/main` `1d9e386fd5afe739bcb9c93c9094e158cdb4baba`
- 5175 원본 기준선: `69a725880f2da67589f18d321a9fb71b0540c79f`
- 원본 branch: `fix/task-ul891-set-001-user-corrections` — dirty·untracked 상태를 그대로 보존
- port branch: `fix/task-experiment-promotion-001-ul891-port`
- 임시 worktree: `/private/tmp/emi-qms-ul891-port.GoHD7X`
- runtime ownership: 없음. 기존 5174/5081·5175/5082 process source로 사용하지 않는다.
- cleanup 경계: 사용자 검수·게시 결정 전 worktree와 branch를 삭제하지 않는다.

## 사용자 승인과 실행 경계

- 사용자는 Azure 배포보다 UL891 수정 1~7 전체 구현을 우선하라고 승인했고, 이후 5174 기준선 통합을 1단계, UL891 재구현을 2단계로 확정했다.
- 사용자는 2026-08-04 `2단계 작업 시작`을 지시했다.
- implementationApproved: `true`
- migrationApproved: `true` — additive `0068`과 isolated migration 검증만 포함
- runtimeHandoverApproved: `true` — 2026-08-04 사용자 `서버 열어봐 확인해볼게`; 통합 후보 Frontend `5191`만 시작하고 기존 UL891 검수 Backend `5082`를 재사용
- persistentUatApproved: `false`
- azureMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- pullRequestApproved: `false`
- mergeApproved: `false`

## 사용자 검수 runtime

- Frontend: `http://127.0.0.1:5191`
- Backend: `http://127.0.0.1:5082` — 기존 5175 UL891 검수 데이터·Backend를 변경 없이 재사용
- Frontend source: 이 Change의 통합 후보 worktree
- Database mutation: 없음
- migration·seed·worker·실제 provider activation: 없음
- 기존 5174/5081·5175 Frontend process: 변경·재시작 없음

## 포함 범위

1. UL891 전체 기본계획 일괄 적용·빈 세트 보호·명시적 덮어쓰기·후속 세트 상속.
2. 계획 흰색·실적 검은색 막대, 본문 날짜선, 일정표 아래 생산관리 담당자 목록.
3. 기본계획 저장의 잘못된 실적 연결 검증 제거와 계획 구조의 한 행 편집·20px 필수 checkbox.
4. 날짜 헤더 무선, 주요선 2px 실선, 내부 보조선 1px 점선, 외곽·왼쪽 구분선 일반 실선, 양끝 중복 날짜선 제거.
5. UL891 단일 현재 설계 `수정 → 저장 → 다시 수정`, 사용자 version·code 제거, 위치 identity 기반 반복 사양 허용.
6. 사양 값 수정 시 물리 패널 identity 보존, 위치 추가·삭제 때만 패널 생성·취소, 현재 화면의 활성 위치·활성 패널 projection.
7. 기존 검수 프로젝트의 활성 42면 유지와 취소 이력 12면의 현재 54면 오표시 방지.
8. 현재 Graphite 페이지 구조에 맞춘 UI 통합과 관련 자동 회귀.

## 제외 범위

- 기존 5175 branch의 통째 merge 또는 원본 WIP 수정·정리
- Graphite redesign, Azure 배포 코드 변경, 통합 source image 재배포
- Persistent UAT migration·seed·runtime handover
- Azure resource·DNS·TLS·traffic·실제 Teams/Mail provider mutation
- 범위 밖 기능·정책 변경, direct `main` push, 승인 전 commit·push·PR·merge

## 검증 계약

1. `0068` fresh와 기존 ledger upgrade, backfill·active panel·cancelled history 불변을 검증한다.
2. Backend Release build·전체 test와 UL891 권한·CAS·rollback·비-UL891 회귀를 검증한다.
3. Frontend lint·typecheck·unit·production build를 검증한다.
4. Mock UI와 isolated Full-Stack 전체를 실행하고 UL891 사용자 수정 전용 desktop·390px 시나리오를 포함한다.
5. Graphite 화면 구조·표 밀도·responsive layout·page-level overflow 0, console error 0을 확인한다.
6. allowlist·secret·개인정보·generated artifact·문서 link·`git diff --check`를 확인한다.
7. Open P0/P1/P2가 0일 때만 사용자 검수 후보로 보고한다.

## Rollback

- 게시 전에는 이 port branch/worktree만 보존하고 통합 `main`과 기존 runtime을 변경하지 않는다.
- migration은 기존 version·패널·취소 이력을 삭제하지 않는 additive 구조다. 적용 후 문제는 destructive rollback 대신 forward-fix migration을 사용한다.
- 향후 게시 뒤에는 history rewrite 없이 revert PR 또는 forward-fix PR을 사용하며 DB 복구는 별도 controlled handover 계약을 따른다.
