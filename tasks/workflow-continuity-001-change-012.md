# TASK-WORKFLOW-CONTINUITY-001 Change 012 — Pending 전 부서 코멘트·품질 재검사 범위·LQC 누락 복구

## Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `5779670`
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

- 업무 목표: Pending의 조치·판정 권한은 유지하면서 모든 부서 조회 사용자가 협업 코멘트를 남길 수 있게 하고, LQC·OQC 재검사는 실제 부적합 항목만 다시 판정하며, 제조 시작 또는 완료 후 LQC 업무가 누락된 기존 패널까지 복구한다.
- Root Finding: Pending 코멘트 POST와 `canComment`가 `pending.manage` 및 조치·품질 참여자에 묶여 있다. LQC·OQC 재검사 성적서는 전체 활성 양식을 다시 노출·검증한다. 품질 인계 재조정은 OQC 이후만 복구하고 제조 실행은 있으나 LQC 업무가 없는 패널을 후보로 포함하지 않는다.
- 변경·검증 경계: 기존 Pending 관리 권한 안의 전 부서 코멘트 참여, LQC·OQC 재검사 대상 항목의 서버 필터·검증·UI 근거 표시, LQC 누락 인계 재조정과 지정 패널 기존 데이터 복구를 포함한다.
- 보존할 불변조건: Pending 전이·담당 변경·조치 입력은 기존 담당자와 품질/조정자 권한을 유지한다. IQC 동작, 일반 LQC·OQC 전체 체크리스트, 제조 단계별 LQC 개방, 패널별 OQC 인계, CAS·멱등성·알림 중복 방지는 유지한다.
- 예상 산출물: Backend·Frontend 수정, 회귀 테스트, privacy-safe 실제 누락 진단 및 재조정 결과, Implementation report와 검수 체크리스트.

## 사용자 확정 계약

1. 기존 Pending 관리 권한이 있는 활성 부서 사용자는 소속 부서와 무관하게 열린 Pending에 협업 코멘트를 등록할 수 있다. 조회 전용 계정에는 쓰기 권한을 확대하지 않는다.
2. 다른 부서 사용자는 코멘트 외 전이·조치 시작/완료·담당 변경·품질 재검사 판정 권한을 새로 얻지 않는다.
3. LQC·OQC 부적합 후 생성된 재검사는 직전 부적합 성적서의 `Fail` 항목만 표시·저장·확정할 수 있다.
4. 재검사 화면은 각 대상 항목의 이전 부적합 근거를 함께 표시한다.
5. 현재 양식이 바뀌어도 재검사는 직전 부적합 성적서의 양식 version을 기준으로 생성한다. 기존 재검사에는 안정적인 item code로 범위를 복원한다.
6. 제조 실행이 시작됐는데 LQC 업무가 없는 활성 패널은 품질 인계 재조정에서 LQC 업무와 담당자 알림을 멱등 생성한다.
7. 지정 프로젝트 2번 패널을 포함한 기존 누락 데이터는 고정 검수 runtime에서 재조정하고, 실제 이름·식별자 없이 결과 개수와 상태만 보고한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 Pending·품질 연속성 계약의 사용자 검수 실패 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- 조회 권한만 있는 다른 부서 사용자가 열린 Pending에 코멘트를 추가할 수 있고 전이·담당 변경은 계속 거절되는지 확인한다.
- LQC·OQC에서 여러 항목 중 일부만 부적합 처리한 뒤 Pending 조치 완료 시 재검사 상세에 해당 항목만 표시되는지 확인한다.
- 재검사 대상이 아닌 항목의 응답·사진 저장과 확정 payload를 Backend가 거절하는지 확인한다.
- 재검사 합격은 Pending을 닫고, 재검사 부적합은 같은 Pending을 재조치 상태로 되돌리는지 확인한다.
- 제조 실행이 있으나 LQC 업무가 없는 패널을 재조정하면 LQC 업무·정/부 담당 알림이 한 번만 생성되고 재실행은 중복을 만들지 않는지 확인한다.
- 지정 프로젝트 2번 패널이 LQC queue에 복구되고 OQC는 LQC 합격 전에는 계속 열리지 않는지 확인한다.
- Backend Release build·전체 test, Frontend lint/typecheck/unit/build, 격리 Full-Stack 회귀와 고정 검수 runtime health를 검증한다.
