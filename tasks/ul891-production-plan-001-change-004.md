# TASK-UL891-PRODUCTION-PLAN-001 Change 004 — 일정표 날짜 세로선 표시 복구

## Task Identity Gate

- proposedTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- policyInputResolution: `USER_VALIDATION_FEEDBACK`
- gateStatus: `PASS_REUSE`

## 확인된 결함

- Gantt는 기간에 따라 세로선 DOM을 생성했지만 DESIGN-000 wireframe의 전역 `span` background reset이 선 배경색을 `transparent !important`로 바꿨다.
- 기존 Full-Stack 검증은 선 element 개수만 확인해 실제 투명 상태를 검출하지 못했다.

## 사용자 승인 계약

1. 날짜축 세로선과 본문 일/주/월 세로선을 실제 화면에 표시한다.
2. 주요 날짜선은 진하게, 보조 날짜선은 옅게 구분한다.
3. 계획 흰색·실적 검은색 막대와 기존 기간별 선 간격은 유지한다.
4. Full-Stack 검증은 element 존재뿐 아니라 실제 computed color를 확인한다.

## 실행 경계

- implementationApproved: `true`
- migrationApproved: `false`
- persistentUatApproved: `false`
- runtimeHandoverApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- pullRequestApproved: `false`
- mergeApproved: `false`
