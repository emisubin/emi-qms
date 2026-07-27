# TASK-UL891-SET-001 Change 008 — UL891 저장 오류·불필요 규격 제거

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `de8e05bc0383ebf5abbdcfd95cab3d5d85c9f5ce`
- roadmapExpectedTaskId: `TASK-UL891-SET-001`
- roadmapNextGate: `USER_VALIDATION_BATCHED_FINAL`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE`
- gateStatus: `PASS_REUSE`

## 확인된 증상과 원인

1. 별도 UL891 설계 입력 화면에서 현재 값을 입력하고 `저장`만 누르면 화면의 값을 먼저 임시본에 반영하지 않고 기존 서버 Draft를 바로 Publish했다.
2. Backend Publish 완료 조건이 구성 패널의 이름뿐 아니라 `panel_specification`까지 필수로 요구했다.
3. `규격`은 확정된 UL891 업무 입력값이 아닌데 Change 007 조회·수정 화면에 새 필드처럼 노출됐다.

## 승인된 보정 범위

1. `저장` 한 번으로 현재 입력값의 Draft 갱신과 Publish를 순서대로 실행한다.
2. UL891 Publish 완료 조건에서 규격을 제외하고 패널명과 포장방식별 치수만 검증한다.
3. UL891 주문 안내·설계 조회·설계 입력·패널 세트 문맥에서 규격 표시와 입력칸을 제거한다.
4. 기존 API·DB의 `panelSpecification` 필드는 과거 데이터 호환을 위해 삭제하지 않고, 화면에 이미 로드된 값도 임의 삭제하지 않는다.
5. Backend API 회귀 테스트와 Frontend 저장 순서·규격 미노출 테스트를 추가한다.

## 실행 경계

- implementationApproved: `true`
- fableInvocationRequired: `false`
- databaseMigrationRequired: `false`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
