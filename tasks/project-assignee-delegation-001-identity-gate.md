# TASK-PROJECT-ASSIGNEE-DELEGATION-001 — Task Identity Gate

- proposedTaskId: `TASK-PROJECT-ASSIGNEE-DELEGATION-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `OPERATIONS-PROMOTION`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PROJECT-ASSIGNEE-DELEGATION-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`
- planningOwner: `Codex — 사용자 명시 승인`
- branch: `feat/task-production-control-001-unified-project-plans`
- baselineHead: `4520e641b98c1c464243e9988b1a373d57d49bed`

## Purpose identity

- 업무 목표: 생산관리팀이 모든 부서 담당자를 전화·메신저로 취합하는 대신, 생산관리 이외 부서장이 프로젝트 안에서 자기 부서 담당자만 직접 지정한다.
- Root Finding 또는 정책 결정: 현재 `ProductionPlan.Update` 권한만 전체 생산계획과 모든 부서 담당자를 함께 수정할 수 있어, 다른 부서장은 읽기만 가능하고 생산관리팀이 모든 지정 업무를 대행해야 한다.
- 변경·검증 경계: 부서장 전용 담당자 조회·저장 API, 자기 부서만 보이는 전용 화면, 프로젝트 생성 시 담당자 지정 요청 알림, 권한·동시성·알림·desktop/mobile 검증을 포함한다.
- 보존할 불변조건: 생산관리팀의 전체 생산계획 수정 권한, 프로젝트 조회 화면의 전체 계획·담당자 표시, 프로젝트별 담당자 row version·감사이력, 기존 담당자 지정 후속 알림과 업무 생성 계약을 유지한다.
- 예상 산출물: Codex 기획, Backend·Frontend 구현, 집중 자동검증, privacy-safe 검수 화면, Implementation report와 Roadmap 상태 갱신이다.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 중복·순서 판정

- `TASK-005A`는 생산관리 권한 사용자가 모든 프로젝트 담당자를 지정하는 기능이며, 다른 부서장의 자기 부서 한정 지정 권한은 포함하지 않는다.
- `TASK-PRODUCTION-CONTROL-001 change-011`은 프로젝트 전용 생산계획 기간·실적 연결·조회 통합이며 담당자 권한 위임과 생성 알림은 포함하지 않는다.
- Task 문서, Roadmap, local/remote branch, worktree와 PR에서 같은 purpose identity는 확인되지 않았다.
- Roadmap 기본 다음 gate는 운영 승격이지만, 사용자가 현재 미게시 생산계획 작업과 함께 추가 기능을 기획·구현한 뒤 한 번에 검수·병합하도록 명시했다. 이를 이번 신규 Task의 명시적 순서 변경 승인으로 기록한다.
- 기존 dirty 작업을 분리하거나 Git mutation하지 않고 같은 검수 묶음 branch에서 변경을 누적한다. 이는 Task identity 통합이 아니라 사용자가 승인한 게시 batch다.

