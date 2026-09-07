# TASK-OSAN-ACCESS-001 Change 006 — 공통 로그인 승인 대기 화면

- instructionChainRead: true
- taskType: `BUGFIX`
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-ACCESS-001`
- roadmapSequenceMatch: true
- approvalSource: `USER_EXPLICIT_2026-09-08_REUSE_LOGIN_APPROVAL_PENDING`
- mainMergeApprovalSource: `USER_EXPLICIT_2026-09-08_MERGE_REMOTE_MAIN`
- branch: `fix/task-osan-access-001-integrated-user-approval`
- baseline: `03486d9d2996273766f9863aedc74ceb350a7c9a`
- implementationOwnerRequested: `GPT_5_6_SOL_MEDIUM`
- implementationOwnerObserved: `NOT_REPORTED`

## 문제와 변경 범위

`no_membership`과 `local_profile_pending`은 관리·진단 상태가 다르지만 사용자는 모두 관리자의 로그인 승인을 기다린다. 별도 `BusinessUnitAccessGate`와 사업부 소속 대기 문구를 삭제하고 기존 `ApprovalPendingPage`를 두 상태의 공통 사용자 화면으로 재사용한다. Backend 상태, 승인 순서, 권한, DB, migration과 총괄의 통합 사용자 관리는 변경하지 않는다.

Change 005에서 연결한 기존 `ShellSwitchControls`는 공통 승인 대기 화면에 유지한다. 기존 개발 모드 또는 검수 사용자 전환 권한 조건을 그대로 사용하므로 production 일반 사용자에게 개발·검수 selector를 노출하지 않는다.

## 검증·게시 경계

- 로컬 unit/E2E/typecheck/lint/full suite는 실행하지 않는다. 테스트 파일도 변경하거나 삭제하지 않는다.
- 최소 diff·allowlist·privacy 확인 뒤 local commit과 non-force push를 수행한다.
- Push가 만든 PR #121 필수 CI 한 번에서 Backend, Frontend, 일반 Full-Stack, 사업부 3DB, 오산 3DB와 CI Gate 전체 회귀를 확인한다.
- 모든 required check가 통과한 exact head만 이번 사용자의 명시 승인으로 `main`에 병합한다.
- Azure, 운영 DB, Persistent UAT와 실제 provider 배포는 포함하지 않는다.
