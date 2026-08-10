# TASK-CI-COST-001 Task Identity Gate

- proposedTaskId: `TASK-CI-COST-001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-TEAMS-PWA-001`
- roadmapNextGate: `USER_ACTUAL_DEVICE_VALIDATION_AND_OPERATION_OBSERVATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-CI-COST-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: GitHub-hosted Actions 사용량이 월 한도의 90%에 도달한 상태에서 PR 품질 Gate를 보존하며 불필요한 runner minute를 줄인다.
- Root Finding 또는 정책 결정: 일반 CI가 문서 변경, 연속 PR commit과 `main` merge에서 동일한 Backend·Frontend·Full-Stack 작업을 반복하고, Full-Stack이 선행 실패와 무관하게 시작되는 `CI-MINUTES-OVERCONSUMPTION-001` P2를 보정한다.
- 변경·검증 경계: `.github/workflows/ci.yml`, 이 Task의 추적 문서와 Product Roadmap만 변경한다. GitHub-hosted runner에서 actionlint·분류/집계 실패 경로·문서·privacy 검증을 수행한다.
- 보존할 불변조건: 코드 PR의 Backend·Frontend·Full-Stack 검증, 항상 결론을 내는 `CI Gate`, 모호한 변경의 전체 검증 fallback, 수동 Azure 운영 release의 승인·SHA·concurrency 계약, 제품/API/DB/migration/runtime 불변.
- 예상 산출물: 변경 인지형 일반 CI, PR 중복 run 취소, 선행 성공 뒤 Full-Stack 실행, pnpm store cache, timeout, Task·Implementation report·Roadmap·사용자 검수 checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적 결과는 0개다. Azure 운영 release runner 경고 `GHA-AZURE-RUNNER-WARNINGS-001`은 수동 배포 action/runtime 호환성 P3이며 일반 CI minute 과소비와 목적·변경 경계·완료 조건이 다르다.

## Roadmap sequence resolution

기존 Next Gate는 실제 PC·Android·iPhone 검수와 운영 관찰이다. 사용자가 2026-08-10에 두 작업의 동시 진행과 이 Task의 구현을 명시 승인했다. 기존 사용자 검수·운영 관찰을 완료 처리하거나 중단하지 않고 병렬 보존한다.
