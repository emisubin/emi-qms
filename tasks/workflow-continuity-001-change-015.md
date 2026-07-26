# TASK-WORKFLOW-CONTINUITY-001 Change 015 — 품질 판정 단일화·재조치 재요청·입고 업무 요약

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

- 업무 목표: 체크리스트 품질검사의 최종 판정을 실제 항목 결과에서 자동 결정하고, 재검사 부적합을 같은 Pending의 조치 요청으로 정확히 되돌리며, 자재 입고 확정 내 업무를 한 줄로 요약한다.
- Root Finding: LQC·OQC와 상세 IQC 화면은 실제 응답과 무관한 합격·부적합 선택을 함께 노출하고, 공통 재검사 실패 로직은 `ReinspectionRequested`를 `ActionRequested`가 아닌 `InProgress`로 직접 전이한다. 입고 확정 업무 설명은 프로젝트·품목·수량·도착일을 여러 줄로 반복한다.
- 변경·검증 경계: 상세 IQC·LQC·OQC 체크리스트 판정 UI, 공통 Pending 재검사 실패 전이, 자재 입고 확정 업무 문구와 관련 Backend·Frontend 회귀 테스트를 포함한다.
- 보존할 불변조건: 전진검수·FAT의 패널 통합 수동 판정, 동일 Pending 수명주기, 정·부 담당자 알림, 멱등 키, 품질 재검사 범위, 권한·감사 이력·Teams·메일 outbox 정책은 유지한다.
- 예상 산출물: Backend·Frontend 수정, 회귀 테스트, Implementation report와 사용자 검수 체크리스트.

## 사용자 확정 계약

1. 상세 IQC·LQC·OQC는 검사 가능한 체크 항목 중 하나라도 `Fail`이면 부적합 확정 동작만, 하나도 없으면 합격 확정 동작만 노출한다.
2. 서버도 상세 IQC에서 부적합 항목 없는 `Failed` 확정을 거부해 화면 우회 요청을 차단한다.
3. 전진검수·FAT는 항목별 체크리스트가 없는 패널 통합 판정이므로 기존의 명시적 적합·부적합 선택을 유지한다.
4. IQC·LQC·OQC·전진검수·FAT 재검사에서 다시 부적합이면 같은 Pending을 `ActionRequested`로 전이하고 같은 Pending 업무를 `Requested`로 재활성화한다.
5. 재조치 알림은 기존 정·부 담당자와 같은 멱등 계약을 유지하며 새 Pending 또는 중복 업무를 만들지 않는다.
6. 자재 입고 확정 내 업무 상세는 `IQC 합격 도착분의 입고 확정을 진행해 주세요. (품목명 수량 단위)` 한 줄로 표시한다.

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

- 체크리스트가 모두 적합일 때 합격 확정 동작만, 하나라도 부적합일 때 부적합 확정 동작만 보이는지 확인한다.
- 상세 IQC 서버가 항목 응답과 반대되는 최종 판정을 모두 거부하는지 확인한다.
- Legacy·상세 IQC와 패널 품질 공통 재검사 실패가 `ActionRequested`·업무 `Requested`·재조치 알림으로 이어지는지 확인한다.
- LQC·OQC 공통 통합 회귀와 전진검수·FAT가 사용하는 동일 공통 Pending 전이 경계를 확인한다.
- 입고 확정 내 업무 설명이 품목·수량만 포함한 한 줄 요약인지 확인한다.
- Backend build·집중 test·전체 test, Frontend lint·typecheck·집중 unit·전체 test·build, `git diff --check`를 수행한다.
