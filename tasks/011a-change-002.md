# TASK-011A Change 002 — 제조 단계 저장 실행 잠금

## Task Identity Gate

- proposedTaskId: `TASK-011A Change 002`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-011A`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-011A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 한 패널의 제조 단계 저장이 완료되기 전 연속 클릭이 들어와도 서버 mutation을 한 번만 보내고, 최신 상태를 받은 후에만 다음 단계를 연다.
- Root Finding: `MANUFACTURING-RAPID-STAGE-SAVE-LOSS` P2.
- 변경 경계: `ManufacturingPage` 즉시 mutation fence, 저장 중 피드백·선택 잠금, unit·isolated Full-Stack E2E·screenshot.
- 보존 불변조건: 4단계 순서, operation receipt·expectedVersion, API·DB·migration·권한·Pending·LQC 인계 계약을 바꾸지 않는다.

## 승인·운영 경계

- investigationApproved: `true`
- implementationApproved: `true` — 사용자가 남은 P2 자동 진행을 명시
- Fable: `NOT_APPLICABLE` — 확정된 순차 mutation·중복 submit 차단 계약의 결함 보정
- localCommitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 권장 최소안과 완료 기준

1. React render보다 먼저 반영되는 ref 기반 실행 잠금을 사용해 같은 event tick의 중복 mutation을 차단한다.
2. 저장 중에는 단계·중단·완료·패널/프로젝트 선택을 잠그고 상태 안내를 노출한다.
3. 서버 응답과 queue/detail refresh가 완료된 후에만 잠금을 해제한다.
4. 1단계 버튼에 동일 tick 3회 클릭을 발생시켜도 POST 1건, 1/4 저장, 2단계 활성을 검증한다.
