# TASK-G2-OPERATIONS-002 — Task Identity Gate

- proposedTaskId: `TASK-G2-OPERATIONS-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AUDIT-001`
- roadmapNextGate: `POST_DEPLOYMENT_LOGIN_VALIDATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-G2-OPERATIONS-002`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: G2 일일 운영에 정식 납품 목표와 불량 수량을 추가하고, 홈 생산 현황표의 임시 예상값을 저장 없이 즉시 그래프·재고에 반영해 조회 시뮬레이션할 수 있게 한다.
- Root Finding 또는 정책 결정: `TASK-G2-OPERATIONS-001`은 납품 목표를 명시적으로 제외했고 불량 원본을 저장하지 않는다. 사용자는 이번 단순 신규 기능에 한해 Fable 대신 Codex 직접 기획·구현과 현재 Roadmap보다 우선하는 순서 변경을 승인했다.
- 변경·검증 경계: additive migration, G2 DB/API/권한·재고 계산, 홈·생산/출하·출근 UI, desktop/390px와 격리 DB 검증을 포함한다. 운영 DB, Persistent UAT, 실제 provider, push·PR·merge·Azure 배포는 제외한다.
- 보존할 불변조건: 기존 G2 실사 checkpoint, metric별 CAS, 역할별 서버 권한, 미래 예상값 날짜 도래 초기화, 기존 PMS 데이터 분리, ReviewSafe 차단과 사용자 WIP를 유지한다.
- 예상 산출물: Codex planning·review, additive migration과 코드/tests, Implementation report·SOP·User manual·Roadmap·검수 checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — 기존 G2 PR `#110` 한 건만 확인했고 이번 목적 PR은 0건이다.

## 기존 Task와의 경계

`TASK-G2-OPERATIONS-001`의 현재 화면·저장·그래프 계약은 선행 기반으로 유지한다. 이번 Task는 기존 완료 scope를 재구현하는 change가 아니라, 당시 제외된 납품 목표와 새 불량 원본·임시 홈 시뮬레이션을 추가하는 별도 사용자 능력이다.

## 기준선과 작업공간

- branch: `feat/task-g2-operations-002-daily-input`
- base: 최신 `origin/main` `3590c27b7281e77199c8773495e0bafe6311517a`
- 적용 지침: Root `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, 종료 정책, Validation Matrix, Privacy-safe Evidence
- 격리 사유: canonical clone에 사용자 WIP가 있고 기존 G2 worktree는 검수 runtime source로 사용 중이다.
- cleanup: 자동 정리하지 않는다. 사용자 검수 이후 clean·process 미사용·commit reachable을 확인하고 승인 범위에서만 제거한다.

## 사용자 workflow override

- 승인 일자: 2026-09-01
- 승인 내용: 단순 `NEW_FEATURE`이므로 Fable을 생략하고 Codex가 직접 기획·구현한다.
- 해석: Codex planning·review resolution과 구현을 승인하며, 현재 `TASK-AUDIT-001` 운영 검수보다 이번 G2 확장을 우선한다.
- 제외: commit, push, PR, merge, Persistent UAT와 Azure 공개배포 승인은 포함하지 않는다.
