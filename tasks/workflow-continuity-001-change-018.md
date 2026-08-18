# TASK-WORKFLOW-CONTINUITY-001 Change 018

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001 Change 018`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- roadmapNextGate: `운영 관찰 중 확인된 전체 흐름 표시 결함 보정`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 프로젝트 전체 흐름에서 개인 업무로 오해되는 업무 건수를 제거하고 단계 상태만 명확히 표시한다.
- Root Finding 또는 정책 결정: 프로젝트 전체 흐름의 `내 업무` 건수는 로그인 사용자 기준이 아니며 완료·취소 이력까지 포함할 수 있어 진행 판단에 불필요하다. 사용자는 전체 흐름에는 상태만 표시하기로 확정했다.
- 변경·검증 경계: 전체 흐름 상단·단계별 업무 건수 표시를 제거하고 `Requested` 상태의 사용자 표시명을 `업무 요청됨`으로 바꾸며 관련 Frontend·Backend 회귀 테스트를 갱신한다.
- 보존할 불변조건: `/my-work`의 개인 업무 건수와 업무 생성·상태·알림·권한·API 호환 필드는 변경하지 않는다. 진행률·현재 단계·완료 판정도 변경하지 않는다.
- 예상 산출물: Change 문서, Frontend 표시 보정, Backend 상태 문구 보정, 자동 검증, Implementation report와 사용자 검수 체크리스트.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 승인된 구현 범위

1. 프로젝트 전체 흐름 상단의 `내 업무 N` 상태 칩을 제거한다.
2. 각 단계의 `· 내 업무 N건` 문구를 제거한다.
3. 단계 상태 `내 업무 생성됨`을 `업무 요청됨`으로 변경한다.
4. 개인별 `/my-work` 화면, 업무 생성·상태 전이와 알림은 유지한다.
5. 응답의 `generatedWorkItemCount`, `workItemCount` 필드는 기존 소비자 호환을 위해 유지하되 전체 흐름 UI에는 표시하지 않는다.

## 검증 계획

- Frontend 프로젝트 전체 흐름 targeted unit test
- Backend workflow 상태 label targeted test
- Frontend typecheck·lint·build
- `git diff --check`
