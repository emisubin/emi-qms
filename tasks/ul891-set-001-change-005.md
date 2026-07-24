# TASK-UL891-SET-001 Change 005 — 프로젝트 상세 탭 레이아웃 정렬

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 프로젝트 상세의 전체 흐름·생산관리·설계·구매·제조·품질·물류·영업 탭이 같은 시작선, 간격, 제목 구조와 콘텐츠 폭을 사용하도록 정렬한다.
- Root Finding: 탭마다 최상위 section의 margin·padding·border 계약이 달라 일부 탭의 제목과 KPI가 경계에 붙고, 탭 전환 시 콘텐츠 시작선과 밀도가 달라진다.
- 변경·검증 경계: 프로젝트 상세 탭 콘텐츠의 공통 wrapper와 데스크톱·모바일 spacing만 보정한다. 각 탭의 업무 데이터·권한·상태 전이는 바꾸지 않는다.
- 보존할 불변조건: 흑백 사각형 디자인, 상태 의미 색상, 패널 상세 deep link, 담당자 mutation 권한, 프로젝트별 조회 계약을 유지한다.
- 예상 산출물: 모든 탭의 공통 레이아웃, 390px 전용 간격, 실제 브라우저 desktop/mobile 증빙.

## 구현 계약

1. 탭 바 아래 모든 탭 콘텐츠를 하나의 공통 컨테이너에 넣고 동일한 상단 간격과 세로 rhythm을 적용한다.
2. 탭별 첫 section은 동일한 좌우 inset을 사용하며 제목·설명·액션·KPI가 section 경계에 붙지 않게 한다.
3. 표·카드·빈 상태는 부모 폭을 넘지 않고 긴 텍스트가 레이아웃을 밀지 않게 한다.
4. 모바일은 데스크톱을 단순 축소하지 않고, 제목·액션·KPI·목록이 세로 우선으로 정렬되게 한다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
