# TASK-UL891-SET-001 Change 006 — 프로젝트 상세 패널 현재 단계·품질 Pending 표시 보정

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `5779670`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 상세 제조·품질·물류 패널 현황에서 실제 열린 Pending과 현재 업무 단계만 정확히 표시하고, 핵심정보와 진행률 사이에 부서별 현재 단계 열을 추가한다.
- Root Finding: 품질 검사 queue가 종결된 Pending 연결도 그대로 내려 보내 합격 패널을 `Pending`으로 오표시한다. 품질 핵심정보는 OQC가 Pending이어도 아직 열리지 않은 전진검수·FAT를 항상 `대기`로 표시한다. 공통 현황 표는 단계 값을 계산하지만 별도 열로 노출하지 않는다.
- 변경·검증 경계: 품질 queue의 열린 Pending projection, 프로젝트 상세 품질 상태·설명 우선순위, 제조·품질·물류 현재 단계 열과 모바일 카드 표시를 포함한다.
- 보존할 불변조건: 검사·Pending 원본 이력과 상태 전이, OQC→전진검수·필수 FAT 병행, 개별 패널 진행률 계산, 패널 상세 deep link, 담당자 mutation 권한을 유지한다.
- 예상 산출물: Backend projection 보정, Frontend 현재 단계 열·카드, 지정 프로젝트 1·2번 패널 privacy-safe 검증, 자동 회귀와 구현 보고.

## 사용자 확정 계약

1. 종결된 Pending은 검사 이력에는 남지만 프로젝트 상세 패널의 현재 `Pending` 표시 근거로 사용하지 않는다.
2. 열린 Pending 또는 최신 검사 부적합만 현재 `Pending`으로 표시한다.
3. OQC가 Pending이면 핵심정보에는 OQC 부적합·조치 대기를 우선 표시하고 전진검수·FAT 대기를 함께 표시하지 않는다.
4. OQC 합격 뒤 전진검수와 필수 FAT가 동시에 열리면 현재 품질 단계는 `전진검수 · FAT`로 표시한다.
5. 제조 현재 단계는 첫 미완료 제조 항목 이름을 표시하고 완료·중단·착수 대기를 명시한다.
6. 품질 현재 단계는 LQC, OQC, 전진검수, FAT 또는 병행 단계를 표시한다.
7. 물류 현재 단계는 포장, 출발, 납품 또는 물류 완료를 표시한다.
8. Desktop 표는 `No·패널명·핵심정보·부서 단계·진행률`을 사용하고 모바일 카드에도 단계 필드를 표시한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 프로젝트 상세 현황 계약의 사용자 검수 실패 BUGFIX
- localExperimentRuntimeMutationApproved: `false` — 조회 projection만 검증
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- 종결 Pending에 연결된 OQC 합격 패널은 queue와 프로젝트 상세에서 `Pending`이 아닌지 확인한다.
- 열린 OQC 부적합 패널은 `Pending`, 현재 단계 `OQC`, 핵심정보 `OQC 부적합·조치 대기`로 표시되고 전진검수·FAT 대기 문구가 없는지 확인한다.
- OQC 합격 뒤 전진검수·필수 FAT가 열리면 현재 단계 `전진검수 · FAT`를 표시하는지 확인한다.
- 제조 첫 미완료 단계 이름과 완료 상태, 물류 포장·출발·납품 현재 단계가 desktop/mobile에 표시되는지 확인한다.
- Backend build·관련/전체 test, Frontend lint/typecheck/unit/build, browser desktop·390px와 고정 runtime health를 검증한다.
