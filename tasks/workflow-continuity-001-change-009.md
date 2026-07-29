# TASK-WORKFLOW-CONTINUITY-001 Change 009 — 패널별 제조·LQC 병행과 품질·물류 연속 인계

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 제조·품질·물류를 프로젝트 전체가 아니라 개별 패널의 실제 완료 조건으로 연결한다.
- Root Finding: 현재 구현은 제조 전체 단계 완료 뒤 LQC를 열고, 수동 제조 완료 확인 뒤 OQC를 열며, OQC·전진검수·FAT를 직렬로 연결한다. 이는 제조 중 단계검사인 LQC와 개별 패널 처리·부분출하 확정 정책에 맞지 않는다.
- 변경·검증 경계: 제조 시작·단계 진행, LQC 항목 가용성, 패널별 OQC 인계, 전진검수·필수 FAT 병행, 최종 품질 완료 후 패널별 포장·출발·납품 인계를 변경한다.
- 보존할 불변조건: 제조 단계 순서, 품질 부적합 Pending, 재검사, 검사 snapshot·PDF·증빙, 물류 증빙·멱등성·권한, 프로젝트 전체 완료 집계는 유지한다.

## 구현 계약

1. 패널 제조 시작 transaction에서 같은 패널의 LQC 업무와 알림을 함께 생성한다.
2. LQC 체크 항목은 제조 단계 순서와 1:1로 대응한다. 현재 진행 중이거나 완료한 제조 단계까지만 입력할 수 있고 미래 단계는 서버와 화면에서 잠근다.
3. 제조와 LQC는 어느 쪽이 먼저 끝나도 된다. 같은 패널의 제조 실행과 LQC가 모두 완료되면 별도 사용자 확인 없이 OQC 업무를 한 번만 생성한다.
4. OQC 합격 시 같은 패널의 전진검수를 열고, 프로젝트가 FAT 필수이면 FAT도 동시에 연다.
5. FAT 필수 프로젝트는 전진검수와 FAT가 모두 합격해야 포장을 연다. FAT 비필수 프로젝트는 전진검수 합격으로 포장을 연다.
6. 품질 부적합은 기존대로 Pending으로 이동하며, 재검사 합격으로 해제된 뒤에도 같은 패널의 병행 완료 조건을 다시 계산한다.
7. 포장 업무는 최종 품질 조건을 만족한 패널에만 개별 생성한다. 한 패널만 선택한 포장 단위, 출발 batch, 납품 batch와 단계별 증빙을 허용하며 다른 패널 완료를 기다리지 않는다.
8. 프로젝트 workflow event와 KPI는 모든 활성 패널 완료 시점의 집계로만 유지하고, 개별 패널 다음 단계 인계를 차단하지 않는다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- persistentUatMutationApproved: `false`
- mainMergeApprovalCount: `0`
