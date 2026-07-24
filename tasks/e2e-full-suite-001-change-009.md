# TASK-E2E-FULL-SUITE-001 Change 009 — 사용자 검수 서버 직접 실행

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 Change 009`
- taskType: `HOUSEKEEPING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `첨부·사진 storage/검역/보존/backup·restore 묶음`
- roadmapNextGate: `이름 있는 canonical Task 확정`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `N/A — 신규 제품 Task 선택이 아닌 기존 검수 runtime 운영 지원`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 사용자가 Codex에 재기동을 요청하지 않고 macOS Finder에서 파일을 더블클릭해 고정 실험 검수 Frontend와 Backend를 함께 시작한다.
- Root Finding: 고정 검수 주소가 server process 종료 뒤 반복적으로 접속 불가가 되고, 기존 두 개의 개발 script는 Terminal에서 각각 실행해야 해 사용자 단독 검수 재개가 어렵다.
- 변경·검증 경계: 기존 `42983/41166`과 검수 DB를 그대로 사용하고 통합 launcher, 소유권 검증, readiness, Docker/의존성 preflight와 browser open만 추가한다.
- 보존할 불변조건: 대표 repo·`main`·Persistent UAT·실제 provider·다른 port/process를 변경하지 않는다. strict port를 유지하고 미소유 listener를 종료하거나 자동 우회하지 않는다.
- 예상 산출물: macOS `.command`, 통합 Bash launcher, 고정 runtime 문서 갱신, 실제 최초·중복 실행 검증과 사용자 검수 checklist.

## 포함 범위

- 더블클릭 가능한 `사용자-검수-서버-실행.command`
- Frontend와 Backend의 순차 시작 및 최종 readiness 확인
- Docker Desktop과 기존 `emi-qms-postgres` container 준비 확인
- 고정 port의 PID·시작 fingerprint·Repository cwd·command·parent session 소유권 검증
- 이미 정상 실행 중일 때 중복 process를 만들지 않는 idempotent 실행
- runtime log와 PID state를 Repository 밖 private temp directory에 저장
- 준비 완료 뒤 기본 browser에서 Frontend 주소 열기

## 제외 범위

- server 종료 launcher
- validation DB 생성·초기화·reset
- 다른 port fallback
- 대표 repo·`main`·Persistent UAT·실제 외부 알림 provider
- commit, push, PR, merge와 branch/worktree 정리

## 검수 기준

- `.command` 실행 한 번으로 Backend `41166`과 Frontend `42983`이 모두 준비된다.
- Backend `/health/live`, `/health/ready`와 Frontend `/`, `/health/ready`가 성공한다.
- 같은 파일을 다시 실행해도 listener가 각 port에 하나만 존재한다.
- 미소유 port는 종료하지 않고 명확한 오류로 중단한다.
- Terminal을 닫아도 detached server가 유지된다.
- 사용자 검수 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 유지한다.
