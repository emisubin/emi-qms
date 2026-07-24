# TASK-010A Change 003 — 선택형 키팅과 생산관리 제조 투입 요청 분리

## Task Identity Gate

- proposedTaskId: `TASK-010A`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-010A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-010A CHANGE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 현장 운영에 따라 키팅을 선택적으로 기록하면서도 생산관리가 패널별 제조 투입을 독립적으로 요청하고 제조 담당자가 작업을 시작할 수 있게 한다.
- Root Finding 또는 정책 결정: 기존 구현이 `키팅 완료 → 제조 업무 생성 → 제조 시작 가능`을 하나의 필수 전이로 묶어, 전체 키팅 없이 패널별 제조를 시작하는 실제 업무를 막았다.
- 변경·검증 경계: TASK-010A 키팅 handoff를 재사용해 backend·DB·자재 키팅·생산관리·제조 화면과 관련 테스트를 변경하며 TASK-011A 제조 실행은 회귀 검증한다.
- 보존할 불변조건: 프로젝트 접근 범위, 생산관리 수정 권한, 제조 담당자 정·부 알림, operation 단위 멱등성, 패널정보 완료 조건, 기존 제조 실행·Pending·LQC 전이는 유지한다.
- 예상 산출물: additive migration, 제조 투입 요청 API·화면, 선택형 키팅 알림, 제조 시작 조건 보정, 자동 테스트, desktop/mobile screenshot, change implementation report.

### 검색 범위

- [x] `tasks/`의 TASK-010A·TASK-011A planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·실험 완료 원장·Decision Log
- [x] local branch·worktree·현재 diff
- [x] local/remote 동일 목적 branch와 PR 추적 상태

## 사용자 결정과 문제 정의

- 패널 키팅 완료는 제조 시작의 필수조건이 아니라 실제 현장에서 키팅을 끝낸 경우에만 남기는 선택형 준비 정보다.
- 생산관리의 `제조 투입 요청`이 제조 담당자의 실행 가능한 내 업무를 만드는 유일한 주 흐름이다.
- 생산 예정일 저장만으로 제조팀에 업무를 보내지 않는다. 생산관리가 실제 투입 시점을 판단해 패널을 선택한다.
- 제조 업무에는 `키팅 완료` 또는 `키팅 미보고`와 자재 입고 요약을 함께 표시하되 미완료를 차단 사유로 사용하지 않는다.
- 제조 투입 요청 전후 어느 시점에 키팅 완료를 기록해도 된다. 뒤늦은 키팅 완료는 기존 제조 업무를 중복 생성하지 않고 참고 알림만 보낸다.

## 승인된 구현 계약

### Backend·DB

- `KittingCompleted` workflow stage를 optional로 전환해 미완료가 전체 필수 진행률을 막지 않게 한다.
- 기존에 자동 생성되던 열린 프로젝트 단위 `패널 키팅 완료` 업무를 취소하고 이후 입고 완료 시 같은 업무를 자동 생성하지 않는다.
- 생산관리 권한이 프로젝트의 활성 패널 중 패널정보가 완료된 패널을 선택해 제조 투입을 요청하는 API를 추가한다.
- 요청은 client `operationId`와 정렬된 패널 집합으로 멱등 처리한다. 동일 operation·동일 payload는 성공 replay, 다른 payload는 conflict, 이미 투입된 패널이 섞이면 전체 요청을 conflict로 처리한다.
- 제조 투입 요청 transaction에서 패널별 `ManufacturingWork` 업무와 제조 정·부 담당자의 인앱 알림을 생성한다.
- 기존 work item idempotency key는 과거 데이터·deep link와의 호환을 위해 유지하되 새 업무 제목과 설명은 `제조 투입 요청` 의미로 표시한다.
- 키팅 완료 transaction은 completion·선택형 stage event·참고 알림만 생성하며 제조 업무를 생성하거나 제조 담당자 지정을 필수로 요구하지 않는다.
- 제조 queue와 시작 조건은 키팅 completion이 아니라 제조 투입 work item을 기준으로 한다.

### Frontend

- 생산관리 프로젝트 확장 영역 최상단에 패널별 `제조 투입 요청` 도구를 추가한다.
- 발급 가능한 패널만 선택할 수 있고 전체선택·선택 요청을 제공한다. 각 행/카드는 패널정보, 키팅 상태, 자재 입고 요약, 이미 투입 여부를 한눈에 보여준다.
- 자재 키팅 화면은 `키팅 완료 알림` 표현을 사용하고 입고 상태는 참고 정보로 표시한다.
- 제조 화면은 `제조 투입 요청됨`을 준비 상태로 표시하고 `키팅 완료/미보고` 배지를 함께 보여준다.
- 모바일은 표를 축소하지 않고 핵심 상태·패널 선택·단일 action 중심의 카드 흐름으로 구성한다.

## 제외 범위

- 생산 예정일 기반 예약·자동 제조 투입, 반복 알림 scheduler
- BOM·패널별 자재 allocation, 키팅 취소·정정
- Teams·메일 같은 실제 외부 provider
- Persistent UAT migration·runtime handover
- 대표 repo·`main`, push·PR·merge

## 검증 계획

- 키팅 미보고 패널도 생산관리 요청 뒤 제조 queue에 나타나고 시작 가능한지 검증한다.
- 키팅 전/후 투입 요청 순서 모두에서 제조 업무가 1건만 존재하는지 검증한다.
- 동일 operation replay와 다른 payload conflict, 이미 투입된 패널 혼합 요청의 all-or-nothing을 검증한다.
- 키팅 완료가 제조 업무를 만들지 않으며 제조 담당자 부재에도 완료 가능한지 검증한다.
- 권한·project scope·패널정보 미완료·취소 프로젝트 방어를 검증한다.
- backend targeted/full test, frontend unit/typecheck/lint/build와 desktop/mobile browser screenshot을 수행한다.

## 승인·게시 경계

- 사용자 승인: 2026-07-21, “권장안대로 작업 계획하고 구현시작해. 계획은 codex 니가 해.”
- Fable: 호출하지 않음 — 신규 기능 재기획이 아니라 승인된 기존 키팅·제조 계약의 정책 보정이며 사용자가 Codex 계획을 명시했다.
- local experiment source·test·screenshot·문서 변경만 포함한다.
- commit은 별도 요청 전 수행하지 않는다.
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider는 제외한다. main merge 승인 수는 `0/3`이다.
