# TASK-UL891-PRODUCTION-PLAN-001 Change 002 — 기본계획 일괄 입력과 일정표 가독성

## Task Identity Gate

- proposedTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TASK-AZURE-DEPLOY-001_CHANGE_005_PUBLISH_AND_WORKLOAD_READINESS`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: UL891 생산계획을 프로젝트 기본값으로 모든 활성 세트에 한 번에 채운 뒤 필요한 세트만 개별 조정하고, 일정표의 계획·실적·날짜 구분과 담당자 목록을 한눈에 확인한다.
- Root Finding 또는 정책 결정: 최초 입력도 세트마다 반복해야 하고 전체 범위는 수정할 수 없어 입력 부담이 크다. 일정표의 색 의미·날짜 경계가 직관적이지 않고 세트 범위 화면에는 생산관리 담당자 요약이 누락됐다.
- 변경·검증 경계: 기존 세트별 overlay에 프로젝트 기본값과 일괄 적용 동작을 추가하고 생산관리 조회·수정 UI와 Gantt 표시만 보정한다.
- 보존할 불변조건: 실물 세트별 독립 수정, 취소 세트 이력, 패널 원본 실적, 프로젝트 공통 실적, 기존 권한·CAS·audit, 비-세트 프로젝트 흐름을 유지한다.
- 예상 산출물: additive migration, Backend 기본값 조회·일괄 저장·후속 세트 상속, Frontend 전체 기본계획 입력·색상·날짜선·담당자 목록, 자동·브라우저 검증과 구현 보고.

## 사용자 승인 계약

1. 프로젝트 기본계획을 한 번 입력하면 모든 활성 세트의 계획 항목에 적용한다.
2. 기본 동작은 아직 값이 없는 세트만 채워 이미 개별 수정한 값을 보존한다.
3. 사용자가 명시적으로 선택한 경우에만 기존 세트 값까지 기본계획으로 덮어쓴다.
4. 이후 추가되는 활성 세트는 저장된 프로젝트 기본계획을 자동 상속한다.
5. 적용 후 각 세트는 기존처럼 독립 수정한다.
6. 일정표의 계획 막대는 흰색, 실적 막대는 검은색으로 표시한다.
7. 일정 범위에 맞는 세로 날짜 구분선을 추가하고 주요 날짜선을 더 분명하게 표시한다.
8. 세트 범위 일정표 아래에도 생산관리 담당자 목록을 표시한다.

## 실행 경계

- implementationApproved: `true`
- migrationApproved: `true` — 신규 additive migration과 isolated DB 검증만 포함
- persistentUatApproved: `false`
- runtimeHandoverApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `false`
- pushApproved: `false`
- pullRequestApproved: `false`
- mergeApproved: `false`

## 검증 계약

- 빈 세트만 적용, 명시적 전체 덮어쓰기, 개별 세트 재수정, 후속 세트 기본값 상속을 검증한다.
- 다른 프로젝트·취소 세트·권한 없음·stale revision·실패 rollback을 검증한다.
- 비-UL891과 기존 프로젝트 단위 생산계획을 회귀 검증한다.
- 계획 흰색·실적 검은색, 날짜 세로선, 일정표 아래 담당자 목록을 desktop·390px에서 검증한다.
