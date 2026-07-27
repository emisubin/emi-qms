# TASK-WORKFLOW-CONTINUITY-001 Change 016 — 패널별 출발·납품 선택

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `2247643`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `USER_VALIDATION_FAILURE`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE`
- policyInputResolution: `USER_CONFIRMED_PANEL_LEVEL`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 포장이 끝난 패널을 출발·납품 단계에서 포장 단위 전체가 아니라 개별 패널로 선택해 부분 출하·부분 납품할 수 있게 한다.
- Root Finding: 물류 queue와 batch API가 포장 완료 뒤 처리 대상을 `PackingUnit`으로 집계해, 같은 포장 단위의 패널을 전부 함께 출발·납품해야 한다.
- 변경·검증 경계: 출발·납품 queue의 패널 단위 projection, batch의 패널 membership, 패널 선택 API·화면, 기존 unit/history/evidence 호환과 관련 회귀 테스트를 포함한다.
- 보존할 불변조건: 같은 프로젝트만 함께 처리, 포장 완료 선행, 출발 후 납품 순서, 필수 증빙, 열린 Pending 차단, 권한·scope, CAS·멱등성, 확정 기록 append-only, 모든 활성 패널 납품 후 정산 인계는 유지한다.
- 예상 산출물: additive migration, Backend·Frontend 수정, 회귀 테스트, Implementation report와 사용자 검수 체크리스트.

## 사용자 확정 계약

1. 포장은 기존처럼 패널을 하나 이상 선택해 한 번에 처리할 수 있다.
2. 출발 화면에는 출발 가능한 패널이 한 행씩 표시되고 사용자가 원하는 패널만 선택한다.
3. 납품 화면에도 출발 완료된 패널이 한 행씩 표시되고 사용자가 원하는 패널만 선택한다.
4. 한 포장 단위에 여러 패널이 있어도 일부 패널만 먼저 출발·납품할 수 있다.
5. 같은 출발·납품 batch에는 같은 프로젝트의 패널만 담을 수 있다.
6. 선택하지 않은 패널의 업무·상태·증빙은 변경하지 않고 다음 처리 대상으로 남긴다.
7. 과거 unit 단위 출발·납품 기록은 모든 소속 패널을 선택한 기록으로 호환·보존한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 후속 사용자 검수에서 확인된 기존 패널별 물류 계약의 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 변경 allowlist

- `database/migrations/0057_logistics_batch_panels.sql`
- `backend/src/Emi.Qms.Api/Logistics/LogisticsContracts.cs`
- `backend/src/Emi.Qms.Api/Logistics/LogisticsStore.cs`
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`
- `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`
- `backend/src/Emi.Qms.Api/Sales/SalesBillingRequestStore.cs`
- `backend/src/Emi.Qms.Api/Sales/SalesSettlementStore.cs`
- `backend/src/Emi.Qms.Api/Ul891Sets/MonthlyBillingStore.cs`
- `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetStore.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/Ul891SetApiTests.cs`
- `frontend/src/logistics.ts`
- `frontend/src/api.ts`
- `frontend/src/LogisticsPage.tsx`
- `frontend/tests/LogisticsPage.test.tsx`
- 관련 isolated Full-Stack 물류 test
- `tasks/workflow-continuity-001-change-016.md`
- `tasks/workflow-continuity-001-change-016-implementation-report.md`
- `tasks/workflow-continuity-001-change-016-user-validation-checklist.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 검증 계약

- 같은 포장 단위의 두 패널 중 하나만 출발하면 선택한 패널만 출발 완료되고 나머지는 출발 queue에 남는지 확인한다.
- 출발한 패널 중 하나만 납품하면 선택한 패널만 납품 완료되고 나머지는 납품 queue에 남는지 확인한다.
- 여러 포장 단위에서 같은 프로젝트 패널을 함께 선택할 수 있고 다른 프로젝트 혼합은 차단되는지 확인한다.
- 과거 unitIds 요청과 기존 finalized batch가 패널 membership으로 호환되는지 확인한다.
- 선택하지 않은 패널 업무·상태, 필수 증빙, Pending, 권한, CAS·멱등성, 최종 정산 exactly-once가 보존되는지 확인한다.
- migration fresh/existing 적용, Backend build·집중·전체 test, Frontend lint·typecheck·집중·전체 test·build, Desktop·390px browser projection과 `git diff --check`를 수행한다.
