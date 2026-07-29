# TASK-WORKFLOW-CONTINUITY-001 Change 010 — 프로젝트 흐름 실데이터 집계와 누락 인계 재조정

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 전체 흐름이 패널·구매품목의 실제 진행 사실을 부분 완료부터 전체 완료까지 표시하고, 과거 runtime에서 누락된 제조·LQC 이후 품질 인계를 자동 복구한다.
- Root Finding: 프로젝트 흐름은 생산계획·설계·구매·IQC만 실데이터로 보정하고 나머지 단계는 전체 완료 event 또는 열린 업무에 의존한다. 따라서 일부 패널 LQC 합격은 `미시작`, 생산관리 제조 투입 요청은 선택형 키팅 단계 `미시작`, 새 자동 인계 로직 적용 전 제조+LQC 완료 패널은 OQC 업무 없음으로 남는다.
- 변경·검증 경계: 프로젝트 흐름의 파생 상태 계산, 키팅/제조 투입 준비 집계, 품질·물류 패널 진행 집계, 품질 후속 업무 재조정 API와 검사함 자동 호출, 기존 실험 검수 DB 누락분 멱등 복구를 포함한다.
- 보존할 불변조건: 패널별 실행 원자, 키팅 선택 조건, 생산관리만 제조 투입 요청, 부적합 Pending 차단, 최신 검사 회차 기준, 담당 정·부 알림, 업무·알림 멱등성, 프로젝트 전체 완료 event의 전 패널 완료 조건을 유지한다.
- 예상 산출물: Backend/Frontend 수정, 집중·전체 자동 회귀, 고정 검수 DB 재조정 결과, 사용자 검수 runtime, Change 구현 보고서와 Roadmap·완료 원장 동기화.

## 사용자 확정 계약

1. 제조 완료와 최신 LQC 합격이 모두 있는 활성 패널에 자동 완료 확인 또는 OQC 업무가 없으면 품질 담당자가 검사함을 열 때 멱등 복구한다.
2. OQC 합격 뒤 전진검수·필수 FAT, 최종 품질 합격 뒤 포장 업무도 같은 방식으로 누락 여부를 검사해 복구한다.
3. 프로젝트 흐름은 완료 event만 보지 않고 실제 품목·패널별 저장 사실로 18단계를 계산한다.
4. 일부 대상만 완료된 단계는 `부분 완료`로 표시한다. 전부 완료됐을 때만 `완료`와 전체 완료 event를 사용한다.
5. 선택형 키팅 단계의 패널별 준비 완료 조건은 `자재 키팅 완료 OR 생산관리 제조 투입 요청`이다. 일부 패널이면 `부분 완료`, 모든 활성 패널이면 `완료`다.
6. 제조·LQC·OQC·전진검수·FAT·포장·출발·납품은 최신 패널별 실제 상태를 기준으로 부분/전체 완료를 계산한다.
7. 부적합 최신 회차가 있으면 완료 수에 포함하지 않고 `차단`을 우선한다. 과거 합격 회차가 있어도 최신 재검사가 부적합 또는 진행 중이면 합격으로 계산하지 않는다.
8. 재조정은 새 업무·알림을 결정적 idempotency key로 생성하고 반복 실행 시 중복을 만들지 않는다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 승인된 워크플로 계약의 사용자 검수 실패 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- 제조 투입만 한 일부 패널과 키팅만 한 일부 패널을 합산해 `부분 완료`, 전체 활성 패널을 덮으면 `완료`인지 검증한다.
- 패널 하나의 LQC 합격이 프로젝트 흐름에서 `부분 완료`인지 검증한다.
- 제조 완료+LQC 합격이지만 OQC가 누락된 fixture를 재조정해 완료 확인·OQC 업무·정/부 알림을 한 번만 생성하는지 검증한다.
- OQC 이후 전진검수·FAT와 최종 품질 이후 포장 누락도 복구하고 반복 호출 중복이 0인지 검증한다.
- Backend Release build·영향 test·전체 regression, Frontend lint/typecheck/unit/build, 고정 검수 runtime health와 privacy-safe 집계 검증을 수행한다.
