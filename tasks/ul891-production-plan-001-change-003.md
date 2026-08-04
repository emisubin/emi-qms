# TASK-UL891-PRODUCTION-PLAN-001 Change 003 — 기본계획 저장 검증과 계획 구조 한 행 편집

## Task Identity Gate

- proposedTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TASK-AZURE-DEPLOY-001_CHANGE_005_PUBLISH_AND_WORKLOAD_READINESS`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_VALIDATION_FEEDBACK`
- gateStatus: `PASS_REUSE`

## 확인된 결함

1. UL891 `전체 기본계획`의 일정 row는 구조가 소유한 실적 연결을 포함하지 않는 것이 정상인데, Frontend 공통 검증이 모든 `LINKED_V1` row에 연결 1개를 요구해 저장 요청 전 차단했다.
2. 계획 구조에서 실적 연결 선택이 별도 전체 행을 차지하고, 범용 label/input 규칙이 필수 checkbox를 약 145×42px로 늘려 항목 구조를 한눈에 읽기 어려웠다.
3. Frontend set-default test fixture가 실제 API와 달리 구조 연결을 기본계획 row에 복사해 첫 결함을 가렸다.

## 사용자 승인 계약

1. `계획 구조`에서만 각 활성 항목의 실적 연결 1개를 검증한다.
2. `전체 기본계획`과 `개별 세트 일정`은 구조 연결을 재검증하지 않고 일정 전용 저장 API를 호출한다.
3. desktop 계획 구조는 `순번 | 계획 항목 | 필수 | 실적 연결 | 삭제` 순서의 한 행으로 구성한다.
4. 필수 checkbox는 20px 고정 크기와 수평 정렬을 사용한다.
5. 390px에서는 연결 선택을 안전하게 다음 줄 전체 너비로 배치하고 page-level 가로 넘침을 만들지 않는다.

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

## 검증 계약

- 실제 API와 같이 기본계획 row의 `connections: []`인 fixture에서 set-default 저장 요청이 실행되는지 검증한다.
- 계획 구조의 연결 누락은 계속 차단되는지 기존 계약을 유지한다.
- desktop에서 checkbox 20×20과 실적 선택의 오른쪽 배치를 확인한다.
- 390px에서 계획 구조와 기본계획 모두 가로 넘침이 없는지 확인한다.
- Frontend unit, typecheck, lint, build와 격리 Full-Stack Chromium을 실행한다.
