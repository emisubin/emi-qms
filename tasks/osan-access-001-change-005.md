# TASK-OSAN-ACCESS-001 Change 005 — 소속 대기 화면 개발·검수 사용자 전환

- instructionChainRead: true
- taskType: `BUGFIX`
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- roadmapSequenceMatch: true
- approvalSource: `USER_EXPLICIT_2026-09-08_PENDING_USER_SWITCH`
- branch: `fix/task-osan-access-001-integrated-user-approval`
- baseline: `fdde84e65f5dd22fba7c684bf7ec546cbc91f975`
- implementationOwnerRequested: `GPT_5_6_SOL_MEDIUM`
- implementationOwnerObserved: `NOT_REPORTED`

## 문제와 변경 범위

`dev-disabled`처럼 사업부 소속이 없는 개발 사용자는 `no_membership` 화면에서 정상 shell보다 먼저 반환되어 기존 개발·검수 사용자 전환 도구에 접근할 수 없었다. 기존 `ShellSwitchControls`와 사용자 전환 함수를 그대로 재사용해 사업부 소속 대기 화면에서도 표시한다. 기존 개발 모드 또는 검수 사용자 전환 권한 조건을 유지하며 production 노출 조건, pending 본문, 권한, backend, DB, migration과 `local_profile_pending` 동작은 변경하지 않는다.

## 완료·검증·게시 경계

- `dev-disabled` 대기 화면에서 기존 개발 사용자 목록으로 전환하면 business-unit request context가 초기화되고 정상 shell을 다시 로드한다.
- 테스트 코드 변경·삭제와 unit/E2E/typecheck/lint/full regression/CI 실행은 사용자 지시에 따라 모두 생략한다.
- 새 exact local commit의 isolated 3-DB review runtime 기동에 필요한 build/readiness만 수행하고, 대기 화면에서 `dev-admin` 전환을 실제 UI로 한 번 확인한다.
- local commit까지만 승인됐다. Push, PR 갱신, CI, `main`, Azure, Persistent UAT와 실제 provider는 제외한다.
