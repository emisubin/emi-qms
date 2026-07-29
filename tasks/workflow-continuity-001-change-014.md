# TASK-WORKFLOW-CONTINUITY-001 Change 014 — 진행률 단일 기준·구매 완료 정책 정합

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `5779670`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE`
- policyInputResolution: `CONFIRMED_REQUIREMENT`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 상세 기본정보와 전체 흐름이 동일한 workflow 진행률을 표시하고, 구매 완료 판정을 확정된 공급유형별 필수 입력 정책에 맞춘다.
- Root Finding: 프로젝트 상세는 초기 4단계만 세는 `projectProgressPercent`를 표시하지만 전체 흐름은 모든 필수 단계의 실제 상태를 센다. 구매 단계는 일반 구매품의 선택 입력인 발주수량·단위를 완료 필수로 요구하며 프로젝트 요약 SQL은 반대로 품목명만으로 완료할 수 있어 판정기가 서로 다르다.
- 변경·검증 경계: 프로젝트 상세 표시의 workflow 진행률 단일화, Workflow·ProjectStore 구매 완결성 조건 정합, 집중 회귀 테스트를 포함한다.
- 보존할 불변조건: FAT 분모, 이미 완료된 구매 단계 비회귀, 사급 수량·단위 필수, required template match, 자재 도착·IQC·입고 확정의 후속 단계 분리, 권한·알림·이력 계약은 유지한다.
- 예상 산출물: Backend·Frontend 수정, 회귀 테스트, Implementation report와 사용자 검수 체크리스트.

## 사용자 확정 계약

1. 프로젝트 상세 기본정보의 진행률은 전체 흐름 API가 계산한 `완료된 필수 단계 수 / 전체 필수 단계 수`를 표시한다.
2. 전체 흐름을 불러오지 못한 경우에만 기존 프로젝트 응답 진행률을 fallback으로 표시한다.
3. 구매 완료에는 활성 구매품목이 1개 이상 필요하며 모든 활성 품목에 발주품목명·공급구분·입고예정일이 있어야 한다.
4. 일반 구매품은 업체명·발주일을 추가로 요구하고 발주수량·단위는 함께 입력하거나 함께 비울 수 있는 선택값으로 유지한다.
5. 사급 자재는 제공 예정 수량·단위를 필수로 유지하고 업체명·발주일은 요구하지 않는다.
6. Item별 required template가 있으면 모든 필수 row가 실제 저장·확정된 활성 품목과 match되어야 한다.
7. 자재 도착·IQC·입고 확정은 구매 단계 완료 조건에 포함하지 않는다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 확정 계약의 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- 프로젝트 응답의 이전 진행률과 workflow 진행률이 다를 때 상세 기본정보와 전체 흐름이 workflow 값을 동일하게 표시하는지 확인한다.
- 일반 구매품이 품목명·업체·발주일·입고예정일을 갖고 수량·단위가 없어도 구매 단계가 완료되는지 확인한다.
- 사급 자재는 수량·단위가 없으면 완료되지 않는 기존 계약을 유지하는지 확인한다.
- required template 일부 입력은 진행 중, 전부 입력·확정은 완료인지 확인한다.
- 프로젝트 요약의 구매 현재 단계 판정도 같은 공급유형별 필수 입력을 사용하는지 확인한다.
- Backend build·집중 test, Frontend typecheck·집중 unit, `git diff --check`를 수행한다.
