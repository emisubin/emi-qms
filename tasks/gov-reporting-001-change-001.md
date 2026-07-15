# TASK-GOV-REPORTING-001 Change 001 — 남은 작업과 Roadmap 다음 Gate 보고

## 1. Task Identity Gate

- proposedTaskId: `TASK-GOV-REPORTING-001 Change 001`
- taskType: `DOCS_GOVERNANCE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `0.6 신규 기능 Go/No-Go`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-GOV-REPORTING-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

## 2. 사용자 결정

모든 Task 종료·중단·사용자 검수 handoff 보고에서 다음 상태를 한눈에 확인할 수 있어야 한다.

- 현재 Task와 단계, 현재 Task에 남은 일
- Commit·Push·PR·Merge 각각의 완료·미완료·승인 대기 상태
- 진행하다 중단·보류된 Task, 중단 단계·사유·재개 조건
- 어떤 Task를 먼저 재개할지
- 모든 활성·중단·보류 작업이 끝난 뒤 Product Roadmap 기준 다음 canonical Task와 `Next Gate`
- Finding의 단순 count가 아니라 ID·severity·상태·원인·영향·해소 또는 후속 위치

## 3. 구현 범위

### 포함

- Root `AGENTS.md`의 `작업 현황 요약`과 고정 10개 항목 상세 규칙
- `docs/12-task-completion-policy.md` canonical 완료 보고 규칙
- 본 Task·Implementation report·SOP·User manual
- Product Roadmap 추적과 Decision Log

### 제외

- 제품 source·runtime·DB·migration·dependency·provider
- 기존 Task WIP 내용 변경 또는 자동 재개
- commit·push·PR·merge

## 4. 고정 상태 요약

10개 완료 보고 제목은 유지하고 그 앞에 `작업 현황 요약` 표를 둔다. 표의 필드는 다음과 같다.

1. 현재 Task와 현재 단계
2. 현재 Task에 남은 일
3. Git 게시 상태: Commit·Push·PR·Merge
4. 중단·보류 Task와 중단 단계·사유·재개 조건
5. 재개 우선순위
6. 모든 작업 종료 후 Roadmap 다음 Task와 `Next Gate`

현재 Task가 끝났더라도 Roadmap 다음 Task를 생략하지 않는다. 중단 Task가 없으면 `없음`으로 명시한다.

## 5. 10개 항목 보강

- `8. 미커밋 변경사항`: changed/staged, Commit·Push·PR·Merge 각각의 상태, 다음 Git action과 필요한 승인을 기록한다.
- `9. 남은 문제`: 현재 Task 잔여 단계, 중단·보류 Task, 재개 조건, Finding, 미검증, external blocker, 별도 승인과 Roadmap 다음 Gate를 기록한다.
- Finding은 count만 기록하지 않고 stable identity와 해소 근거 또는 backlog 위치를 남긴다.

## 6. 검증과 완료 Gate

- docs-only·runtime·중단·전체 완료 예시에서 요약 필드와 고정 10개 제목 확인
- Git 게시 네 단계가 서로 독립적으로 표시되는지 확인
- 중단 Task와 Roadmap next가 혼동되지 않는지 확인
- resolved Finding의 원인·영향·resolution 추적 가능 여부 확인
- 문서 link·heading·diff·privacy·secret·allowlist 검증
- 제품 source·runtime·dependency·migration diff 0
- 사용자 검수와 별도 Git 게시 승인

## 7. 승인 상태

- reportingPolicyApproved: `true`
- implementationApproved: `true`
- productMutationApproved: `false`
- publishingApproved: `false`
- mergeApproved: `false`

## 8. 검증 결과

- `작업 현황 요약` + 고정 10개 항목 static contract: `2/2`
- docs-only·runtime·blocked·all-complete dry run: `4/4`
- missing-field negative detection: `1/1`
- `git diff --check`: PASS
- Markdown file/local link/missing: `11/127/0`
- duplicate heading: `0`
- PII·UUID·private key·credential assignment·absolute user path candidate: `0/0/0/0/0`
- staged·deleted·product source diff: `0/0/0`
- automaticValidationComplete: `true`
- independentVerificationComplete: `false`
- userValidationComplete: `false`
- publishGate: `NO_GO_INDEPENDENT_VERIFICATION_AND_USER_VALIDATION_PENDING`
