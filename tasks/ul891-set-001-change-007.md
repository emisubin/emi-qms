# TASK-UL891-SET-001 Change 007 — UL891 설계 조회·수정 화면 분리

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `P2_REMEDIATION`
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
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_CHANGE_REQUEST`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 신규 UL891 세트 프로젝트의 설계 탭을 조회 전용으로 만들고, 한 개의 수정 버튼으로 별도 전체 폭 입력 화면에 진입해 세트 공통 사양을 수정한다.
- Root Finding: 프로젝트 상세 설계 탭에 UL891 세트 사양 편집기와 일반 평면 패널 설계 입력이 함께 노출되어 같은 설계정보를 두 방식으로 입력하는 것처럼 보이며, 저장 동작의 완료 여부도 action 근처에서 분명하지 않다.
- 변경·검증 경계: UL891의 Frontend 정보 구조·표시 명칭·action feedback만 변경한다. 기존 세트 사양 API, Draft/Published 내부 상태, 권한, 제조 준비 조건, 패널·QR 원자와 비-UL891 설계 흐름은 유지한다.
- 보존할 불변조건: 프로젝트 상세는 조회 허브, 담당자만 별도 입력 화면에서 수정, Backend authoritative validation, QR 일괄 관리 유지, 기존 Published version 불변과 개별 패널 workflow 유지.
- 예상 산출물: UL891 조회 전용 설계 탭, 별도 설계 수정 화면, `임시저장`·`저장` 명칭과 인라인 결과 안내, 중복 평면 설계 입력 제거, Frontend 자동·browser 검증과 종료 산출물.

### 검색 범위

- [x] `tasks/`의 UL891 planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log와 실험 완료 원장
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — 동일 목적 Task·branch 후보 없음

## 사용자 확정 계약

1. 신규 UL891 세트 프로젝트의 프로젝트 상세 설계 탭은 저장된 세트 사양·버전·구성 패널·실물 세트를 조회만 한다.
2. UL891 세트 사양 아래에 일반 평면 패널용 설계정보 입력·수정 영역을 중복 표시하지 않는다.
3. 설계 수정 권한과 Active 프로젝트 조건을 만족하면 조회 화면 상단에 `수정` 버튼을 표시하고 기존 설계 수정 route의 별도 전체 폭 입력 화면으로 이동한다.
4. 별도 입력 화면에서 Draft 저장의 사용자 표시 명칭은 `임시저장`, Publish의 사용자 표시 명칭은 `저장`으로 사용한다. 내부 API·DB 상태명은 변경하지 않는다.
5. 저장 중에는 실행한 동작을 버튼과 상태 안내에 표시하고, 성공·실패 결과를 해당 세트 사양 action 바로 아래에 표시한다.
6. 최초 저장 후의 새 Draft 생성은 `새 수정본 만들기`, Published version 선택은 `저장된 버전`으로 표시해 내부 용어 노출을 줄인다.
7. 기존 프로젝트 단위 QR 일괄 관리와 패널 상세 진입은 삭제하지 않는다.
8. 비-UL891·legacy UL891은 기존 평면 패널 설계 조회·수정 화면을 유지한다.

## 실행·안전 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false` — 기존 확정 기능의 입력 UX 보정
- backendContractChanged: `false`
- databaseChanged: `false`
- persistentUatMutationApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`

## 검증 계약

- UL891 프로젝트 상세 설계 탭에 입력·저장·버전 적용 control이 없고 저장된 공통 사양이 조회되는지 확인한다.
- UL891 프로젝트 상세에서 일반 평면 패널 설계 목록·수정 버튼이 중복되지 않고 QR 일괄 관리가 유지되는지 확인한다.
- `수정` 진입 후 별도 입력 화면에서 `임시저장`과 `저장`을 실행하고 loading·success·error feedback이 action 근처에 표시되는지 확인한다.
- 비-UL891 프로젝트의 기존 `패널명·사이즈 수정` 경로와 Excel 설계 입력이 회귀하지 않는지 확인한다.
- Frontend lint·typecheck·unit·build와 desktop·390px browser smoke, page-level overflow와 console/request failure를 검증한다.
