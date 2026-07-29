# TASK-WORKFLOW-CONTINUITY-001 Change 013 — 품질 재검사 최종 합성·물류 1회 확정

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

- 업무 목표: LQC·OQC 재검사 완료 후 최초 적합 항목과 재검사 적합 항목을 하나의 최종 검사 결과로 조회하고, 포장·출발·납품은 증빙을 먼저 선택한 뒤 저장 한 번으로 확정되게 한다.
- Root Finding: 품질 상세 조회가 재검사 중의 제한된 편집 범위를 최종 확정 뒤에도 그대로 반환해 이전 적합 응답을 숨긴다. 물류 화면은 대상 선택, draft 생성, 증빙 등록, 확정을 서로 다른 사용자 행동으로 나누고 draft URL에만 복구 정보를 남겨 화면 이탈 시 작업이 사라진 것처럼 보이게 한다.
- 변경·검증 경계: 재검사 최종 유효 응답 합성, 재검사 중 쓰기 범위 유지, 물류 증빙 선첨부와 단일 저장 오케스트레이션, 중간 실패 draft 자동 복구, 물류 데스크톱·모바일 레이아웃 정렬을 포함한다.
- 보존할 불변조건: 최초 검사·재검사 원본 기록은 변경하지 않는다. 재검사 중에는 부적합 항목만 수정 가능하다. 물류의 권한, 프로젝트 단위 선택, 열린 Pending 차단, 증빙 형식·개수, CAS·멱등성, 단계별 다음 담당자 인계는 유지한다.
- 예상 산출물: Backend·Frontend 수정, 회귀 테스트, privacy-safe 고정 runtime 검증, Implementation report와 사용자 검수 체크리스트.

## 사용자 확정 계약

1. LQC·OQC 재검사 진행 중에는 직전 부적합 항목만 표시·저장·확정한다.
2. 재검사가 적합으로 최종 확정되면 동일 Pending 검사 계보의 최초 적합 항목과 재검사 적합 항목을 합쳐 전체 양식 결과를 표시한다.
3. 같은 항목을 여러 번 재검사한 경우 가장 최근 확정 응답을 최종 유효 응답으로 사용하되 모든 시도 원본은 이력으로 보존한다.
4. 프로젝트 상세 품질 진행률과 핵심정보도 합성된 전체 결과를 기준으로 계산한다.
5. 포장·출발·납품은 대상과 필수 증빙을 먼저 입력한 뒤 각 단계 저장 버튼 한 번으로 draft 생성, 증빙 등록, 확정을 연속 수행한다.
6. 물류 저장 도중 네트워크 또는 검증 실패로 draft가 남으면 화면이 해당 draft를 즉시 복구해 다시 저장하거나 취소할 수 있게 하며 대상을 사라진 상태로 두지 않는다.
7. 물류 화면은 공통 블랙앤화이트 디자인 토큰과 사각형 기반 레이아웃으로 정렬하고 상태·오류에만 의미 색을 사용한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 품질·물류 연속성 계약의 사용자 검수 실패 BUGFIX
- localExperimentRuntimeMutationApproved: `true`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- LQC·OQC 최초 검사에서 일부 적합·일부 부적합 후 재검사를 합격 처리하면 최종 상세가 전체 필수 항목과 유효 응답을 반환하는지 확인한다.
- 재검사 진행 중에는 부적합 대상 한 항목만 노출되고 범위 밖 저장이 계속 거절되는지 확인한다.
- 프로젝트 상세 품질 탭이 전체 단계 수와 완료 수를 합성 결과로 표시하는지 확인한다.
- 포장·출발·납품에서 증빙이 없으면 저장할 수 없고 증빙 선택 후 한 번의 저장으로 확정되는지 확인한다.
- 물류 연속 처리 중 실패한 draft가 현재 화면과 URL에서 복구되고 재시도로 확정 가능한지 확인한다.
- 물류 데스크톱·모바일 화면의 넘침, 겹침, 잘림과 우선 행동 배치를 확인한다.
- Backend Release build·전체 test, Frontend lint/typecheck/unit/build, 관련 격리 Full-Stack E2E와 고정 검수 runtime health를 검증한다.
