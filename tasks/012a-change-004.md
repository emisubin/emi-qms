# TASK-012A Change 004 — 모든 품질검사 Pending 판정 수명주기

## Task Identity Gate

- proposedTaskId: `TASK-012A`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-012A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: IQC·LQC·OQC·전진검수·FAT가 단계 수와 무관하게 부적합이면 반드시 Pending으로 연결되고, 같은 Pending의 재검사가 적합으로 최종화되면 Pending과 차단 상태가 함께 해제되도록 한다.
- Root Finding: IQC와 패널 품질 공통 저장 경로에는 Pending 순환이 있으나, 전진검수·FAT가 OQC형 체크리스트를 사용해 확정 판정 단위와 다르고 검사 종류별 회귀 계약이 충분하지 않다.
- 변경·검증 경계: 패널 품질 판정 모드, Pending 생성·재개·종결의 원자성, 화면 판정 UI와 API·통합 테스트를 보강한다.
- 보존할 불변조건: IQC는 구매품 도착분 단위, OQC는 항목별 판정, 전진검수·FAT는 패널 통합 판정, 기존 최종 성적서는 변경하지 않으며 하나의 부적합은 하나의 Pending을 재사용한다.
- 예상 산출물: `Checklist | Aggregate` 판정 계약, 실패→Pending·재검사 적합→종결 검증, 기존 이력 보존 migration.

## 확정 정책

1. IQC는 구매 품목의 도착분별 검사이며 부적합 회차가 Pending을 생성한다.
2. LQC·OQC는 항목별 판정이 있는 `Checklist` 검사다. 필수 항목 중 하나라도 부적합이면 최종 판정은 부적합이어야 하며 Pending을 생성한다.
3. 전진검수·FAT는 개별 패널에 대한 `Aggregate` 단일 적합·부적합 판정이며 항목별 응답을 요구하지 않는다.
4. 부적합 재검사는 새 Pending을 만들지 않고 기존 Pending을 재사용한다.
5. 조치 완료 후 재검사에서 적합으로 최종화되는 transaction 안에서 검사 결과를 적합으로 저장하고 연결 Pending·업무 차단을 함께 종결한다.
6. 재검사가 다시 부적합이면 같은 Pending을 조치 상태로 되돌리고 담당 업무·알림을 재개한다.
7. 기존 최종화된 전진검수·FAT 결과는 legacy read-only로 보존하고 신규 검사 회차부터 Aggregate snapshot을 적용한다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
