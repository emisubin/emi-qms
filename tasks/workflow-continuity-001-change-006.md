# TASK-WORKFLOW-CONTINUITY-001 Change 006 — 품질 Pending 해제 원자성 보강

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
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

- 업무 목표: 모든 품질검사에서 Pending의 해제와 연결 검사 적합 판정이 분리되지 않도록 기존 순환 계약을 검사 종류별로 고정한다.
- Root Finding: 기존 구현은 같은 transaction에서 종결하도록 설계됐지만, 판정 모델 확장과 E2E 정책 변경 시 회귀하지 않도록 공통 불변조건과 검증이 필요하다.
- 변경·검증 경계: Pending 조치 완료→재검사 업무→적합 종결 또는 부적합 재조치의 기존 상태 전이와 멱등성만 보강한다.
- 보존할 불변조건: 동일 이벤트 중복 알림 금지, 정·부 담당 업무, append-only 활동 이력, 실제 provider 비호출을 유지한다.
- 예상 산출물: 검사별 실패·재검사 통합 테스트와 종결 상태 일치 검증.

## 구현 계약

1. 품질 부적합 최종화와 Pending 생성은 같은 transaction에서 처리한다.
2. Pending 조치 완료는 품질 재검사 업무를 생성하지만 Pending 자체를 종결하지 않는다.
3. 재검사 적합 최종화만 검사 결과를 `Passed`로 저장하고 Pending을 `Closed`로 바꾼다.
4. 재검사 부적합은 기존 Pending을 재개하고 새 Pending을 생성하지 않는다.
5. 재시도 시 work item·알림·Pending 중복 생성을 멱등키와 통합 테스트로 방지한다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
