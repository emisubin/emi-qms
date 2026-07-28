# TASK-PRODUCTION-CONTROL-001 Change 005 — Item별 제조 양식 저장 오류 보정

## Task Identity Gate

- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
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
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 모든 제조 항목 값을 입력한 Item별 제조 양식이 일반 입력 오류로 저장되지 않는 결함을 고친다.
- Root Finding 또는 정책 결정: Frontend 요청값과 Backend 제조 양식 validation·연결 무결성 오류가 사용자에게 구체적으로 전달되는지 확인하고 실제 저장 차단 원인을 제거한다.
- 변경·검증 경계: Item별 제조 양식 편집·저장 요청, 서버 validation·오류 응답, 관련 Frontend/Backend 회귀와 고정 검수 runtime만 포함한다.
- 보존할 불변조건: 기존 프로젝트 제조 snapshot, 생산계획 연결 identity, Legacy 실행, 양식 관리자 권한과 optimistic concurrency를 유지한다.
- 예상 산출물: 결함 수정, 회귀 테스트, 실제 저장 검증, Implementation report·사용자 검수 체크리스트 갱신.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 실행 기준

- branch: `experiment/task-home-002-personalized-shell`
- startHead: `a7651b5`
- source: 사용자 직접 검수 실패 보고
- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_FIX_REQUEST`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`

## 확인·수정 계약

1. 기존 값이 모두 채워진 Item별 제조 양식을 실제 UI/API 경로로 저장해 실패 응답의 field와 원인을 확인한다.
2. 사용자 입력이 유효하면 저장되어야 하며 기존 생산계획 연결이 끊기는 변경이면 해당 제조 항목과 연결 관계를 사용자가 이해할 수 있게 정확히 안내한다.
3. Frontend는 서버 field validation의 첫 구체 오류를 일반 `입력값을 확인해 주세요.`로 덮어쓰지 않는다.
4. Backend는 변경하지 않은 기존 제조 항목의 불변 identity를 정상적으로 수용하고, 신규 항목에만 새 identity를 발급한다.
5. 관련 Backend·Frontend 회귀, build와 고정 PC 검수 runtime의 실제 저장을 확인한다.

## 후속 사용자 정책 확인

- 제조 단계 변경은 저장 이후 생성되는 프로젝트에만 적용한다.
- 기존 프로젝트는 생성 당시 제조 단계와 생산계획 연결 snapshot을 그대로 유지한다.
- 사용자 화면에 `v1`, `v2` 양식을 다시 누적하지 않는다. 과거 프로젝트의 생성 당시 snapshot이 과거 실행 계약을 보존한다.
- 기존 제조 행의 이름·구분 수정은 불변 identity를 유지해 현재 생산계획 연결도 유지한다.
- 연결된 제조 행 자체를 삭제·교체한 경우에만 앞으로 생성될 프로젝트용 현재 생산계획에서 새 제조 항목을 다시 선택한다.

## 안전 경계

- 대표 repo·`main`·Persistent UAT·실제 provider를 변경하지 않는다.
- 기존 미커밋 Change 003·004와 사용자 WIP를 정리하거나 되돌리지 않는다.
- commit·push·PR·merge를 수행하지 않는다.
