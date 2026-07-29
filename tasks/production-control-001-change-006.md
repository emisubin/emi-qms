# TASK-PRODUCTION-CONTROL-001 Change 006 — OQC 합격 단일 실적 연결

## Task Identity Gate

- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-PRODUCTION-CONTROL-001`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-PRODUCTION-CONTROL-001`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 생산계획 실적 연결에서 OQC 검사 항목을 하나씩 고르지 않고 `OQC 합격` 한 건만 선택한다.
- Root Finding 또는 정책 결정: OQC 검사는 내부적으로 단계별 판정하되 생산계획 실적은 패널의 OQC 최종 합격 사건으로 집계한다.
- 변경·검증 경계: 현재 생산계획 양식과 프로젝트 생산계획의 OQC 연결 선택, OQC 자동 실적 projection, additive migration, 관련 Backend·Frontend·migration 회귀를 포함한다.
- 보존할 불변조건: OQC 성적서 단계별 판정·Pending·재검사, 기존 프로젝트의 생성 당시 상세 연결 snapshot과 조회 호환, IQC 항목별 연결, 제조·LQC 단계별 연결을 유지한다.
- 예상 산출물: 정책 변경 문서, OQC aggregate 연결 구현, migration·회귀 테스트, Implementation report·Roadmap·검수 checklist 갱신.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 실행 기준

- branch: `experiment/task-home-002-personalized-shell`
- startHead: `a7651b5c266d`
- source: 사용자 명시 정책 변경 및 구현 요청
- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_POLICY_AND_IMPLEMENTATION_REQUEST`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`

## 변경 계약

1. 생산계획 실적 catalog에서 OQC는 전진검수·FAT와 같은 세부 항목 없는 `OQC 합격` 단일 선택값으로 제공한다.
2. 현재 생산계획 양식에 남아 있는 OQC 세부 항목 연결은 `OQC 합격` aggregate 연결로 정리한다.
3. 이후 생성되는 프로젝트는 OQC 검사 항목 `definition_key`가 아니라 패널별 OQC 최종 합격을 snapshot하고 실적으로 집계한다.
4. 기존 프로젝트의 세부 OQC 연결 snapshot은 migration으로 변경하지 않는다. 기존 연결은 항목별 projection으로 계속 조회할 수 있고, 해당 프로젝트 계획을 사용자가 수정·저장하면 현재 `OQC 합격` 연결로 정리한다.
5. OQC 검사 자체의 단계별 입력·적합/부적합·Pending·재검사·최종 합격 계약은 변경하지 않는다.
6. IQC는 검사 항목별, 제조·LQC는 제조 단계별 연결을 계속 사용한다.

## 검증 계약

- Backend source catalog가 OQC `definitionKind=None`, 세부 정의 0건을 반환하는지 검증한다.
- 현재 양식 저장에서 OQC는 `sourceDefinitionKey=null`만 허용하고 세부 key 요청은 거부하는지 검증한다.
- 새 프로젝트 snapshot과 자동 실적이 패널별 OQC 최종 합격을 사용하는지 검증한다.
- migration이 현재 양식의 OQC 세부 key만 제거하고 기존 프로젝트 OQC 세부 key는 보존하는지 검증한다.
- Frontend 양식·프로젝트 수정 드롭다운에 OQC 세부 항목 없이 `OQC 합격` 한 건만 표시되는지 검증한다.
- Backend·Frontend 관련 회귀, 전체 회귀, migration fresh/existing과 고정 검수 runtime 화면을 확인한다.

## 안전 경계

- 대표 repo·`main`·Persistent UAT·실제 provider를 변경하지 않는다.
- 기존 미커밋 Change 003~005와 제조 일괄 처리 WIP를 정리하거나 되돌리지 않는다.
- commit·push·PR·merge를 수행하지 않는다.
