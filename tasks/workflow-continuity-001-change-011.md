# TASK-WORKFLOW-CONTINUITY-001 Change 011 — LQC·OQC 원자 확정과 오류 복구

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
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: LQC·OQC 판정 확정이 입력 검증에 한 번 실패한 뒤에도 새로고침 없이 원인을 확인하고 정상 재시도되게 한다.
- Root Finding: Frontend가 체크리스트를 별도 저장한 뒤 확정을 요청한다. 저장 성공 후 확정이 400으로 거절되면 서버 version만 증가하고 화면 version은 남아, 이후 저장이 계속 409로 충돌한다. 오류는 열린 판정 dialog 뒤 본문에 표시되고 내부 저장이 action lock을 해제해 중복 클릭도 허용한다.
- 변경·검증 경계: 패널 품질 확정 API의 optional 응답 payload와 단일 transaction, 판정 dialog 오류·충돌 복구·중복 submit 차단, LQC·OQC 거절/재시도 회귀를 포함한다.
- 보존할 불변조건: Backend 권한·CAS, 중간 임시저장, 검사 snapshot·PDF, 부적합 Pending, 재검사, 다음 단계 인계, 결정적 operation id와 finalized evidence 불변성을 유지한다.

## 사용자 확정 계약

1. LQC·OQC 판정 확정은 현재 체크리스트 응답과 판정을 한 요청으로 전송한다.
2. Backend는 응답 검증·교체·성적서 확정·Pending 또는 다음 단계 인계를 한 transaction에서 처리한다.
3. 입력 검증이나 인계 조건이 실패하면 응답·report/attempt version·업무·알림을 모두 롤백한다.
4. 별도 `임시 저장`은 유지하지만 판정 확정이 내부에서 별도 저장 요청을 만들지 않는다.
5. 부적합 항목이 있으면 dialog 기본 판정을 부적합으로 맞추고 합격 선택을 차단한다.
6. 필수항목·해당없음 사유·부적합 근거·조치 부서 조건을 Frontend와 Backend가 같은 의미로 검증한다.
7. 서버의 상세 field error는 열린 dialog 안에 표시한다. 409이면 최신 검사 내용을 명시적으로 다시 불러올 수 있다.
8. 확정 요청 동안 모든 판정 입력을 잠그고 synchronous in-flight guard로 빠른 중복 클릭도 차단한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 품질 확정 계약의 사용자 검수 실패 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- LQC와 OQC 각각 부적합 응답을 합격으로 제출하면 400이고 응답 row와 version이 변하지 않는지 확인한다.
- 같은 expected version으로 응답을 수정해 재시도하면 확정과 다음 단계 인계가 성공하는지 확인한다.
- Frontend가 판정 확정 중 별도 응답 저장 API를 호출하지 않는지 확인한다.
- 서버 field error가 dialog 안에 표시되고 dialog가 열린 채 유지되는지 확인한다.
- 같은 버튼을 연속 클릭해도 확정 요청이 한 번만 전송되고 실패 재시도는 같은 operation id를 사용하는지 확인한다.
- Backend Release build·전체 test, Frontend lint/typecheck/unit/build, isolated Full-Stack 품질 회귀와 고정 검수 runtime health를 검증한다.
