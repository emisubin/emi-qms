# TASK-PROJECT-PENDING-001 Task Identity Gate

- proposedTaskId: `TASK-PROJECT-PENDING-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `UAT-PWA-PUSH-001-USER-VALIDATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PROJECT-PENDING-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 프로젝트 기본정보에 LSE TASK NO를 추가하고, 기존 Pending 화면에서 로그인 사용자의 부서 기준 목록과 오픈·종결 목록을 명확히 구분한다.
- Root Finding 또는 정책 결정: `quality-operating-model-001-change-005-planning.md`의 후속 작업 3·4를 사용자가 하나의 Codex-only 작업으로 묶어 우선 실행하도록 명시 승인했다.
- 변경·검증 경계: nullable 프로젝트 필드, 프로젝트 생성·수정·상세 UI/API, Pending 목록 조회 범위·상태군 필터와 관련 자동 검증만 포함한다.
- 보존할 불변조건: 서버 권한이 최종 기준이고 부서 범위는 로그인 사용자 부서를 서버에서 조회한다. 기존 Pending 상세 상태·전이·알림·내보내기 계약은 변경하지 않는다. 현재 흑백 화면 구조를 재사용하고 강조선을 추가하지 않는다.
- 예상 산출물: migration, backend/frontend 구현과 테스트, Implementation report 안의 SOP·사용자 안내, Roadmap 갱신, 사용자 검수 checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 상태

`PASS_CREATE` — 같은 목적의 기존 Task·branch·worktree·PR이 없고, 사용자가 현재 Roadmap 순서보다 본 작업을 먼저 진행하도록 명시 승인했다.
