# TASK-EXPORT-001 Change 001 — 실험 fast-track과 Fable 2차 기획 승인 경계

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 기반 Fable 2차 기획 → Codex 구현` 순서로 진행하고, 인터뷰·채택·중간 확인 없이 권장안을 적용해 검증·페이지별 screenshot·local commit까지 완료하도록 명시했다. 2026-07-18 `TASK-014A` local commit 뒤 “다음작업 시작하라”고 요청해 현재 실험 계보의 다음 Roadmap 기능 `TASK-EXPORT-001`을 진행한다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-EXPORT-001`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-export-001-excel-export`
- interviewSource: `tasks/export-001-interview.md`
- firstPlanningSource: `tasks/export-001-planning.md`
- codexReviewSource: `tasks/export-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/22-excel-export-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 완전히 읽고 review의 유지·추가·보류·제거와 Finding resolution을 authoritative implementation contract로 통합한다.

## 3. 구현·Git 승인 경계

- planningApproved: `true` — 2차 기획이 review resolution을 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/22-excel-export-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 4. Fable 사용량 기록

Claude `/usage`는 Repository mutation 없이 `bash scripts/report-claude-usage.sh`로 측정한다. reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 30% 사용 / 70% 잔여 / 12:09 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 | 5% 사용 / 95% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 30% 사용 / 70% 잔여 / 12:10 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 08:00 KST 초기화 | 5% 사용 / 95% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 43% 사용 / 57% 잔여 / 12:10 KST 초기화 | 4% 사용 / 96% 잔여 / 07-25 08:00 KST 초기화 | 7% 사용 / 93% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 43% 사용 / 57% 잔여 / 12:10 KST 초기화 | 4% 사용 / 96% 잔여 / 07-25 08:00 KST 초기화 | 7% 사용 / 93% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 54% 사용 / 46% 잔여 / 12:09 KST 초기화 | 4% 사용 / 96% 잔여 / 07-25 07:59 KST 초기화 | 8% 사용 / 92% 잔여 / 07-25 07:59 KST 초기화 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 실행됐다. model 344초, stdout 23,473 bytes, stderr 0이며 `tasks/export-001-planning.md`를 byte-for-byte 저장했다.

2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했다. model 160초, stdout 20,138 bytes, stderr 0이며 `docs/22-excel-export-plan.md`를 byte-for-byte 저장했다. 최종 문서의 `openBlockingDecisionCount`는 0이다.

## 5. Codex review Finding resolution

| ID | Severity | 2차 기획 요구 상태 | Resolution |
| --- | --- | --- | --- |
| `EXPORT-PROCUREMENT-SCOPE` | P1 | `RESOLVED_IN_PLAN` | 기존 dashboard와 export에 ProjectRead·ProjectAccessScope 동일 적용 |
| `EXPORT-PROJECT-PAGING-EQUIVALENCE` | P2 | `RESOLVED_IN_PLAN` | 기존 single-query list path를 normal/export cap만 달리해 재사용 |
| `EXPORT-PROCUREMENT-SILENT-CAP` | P2 | `RESOLVED_IN_PLAN` | caller limit+1과 explicit truncation/초과 판정 |
| `EXPORT-RESOURCE-FENCE` | P2 | `RESOLVED_IN_PLAN` | 동시 workbook 생성 2개·포화 즉시 429·cancellation slot 안전 |
| `EXPORT-AUDIT-DURABILITY` | P2 | `RESOLVED_IN_PLAN` | payload/file 없는 append-only 최소 audit migration |
| `EXPORT-FORMULA-SAFETY` | P2 | `RESOLVED_IN_PLAN` | 자유 문자열 text cell 강제·formula 0 재파싱 test |
| `EXPORT-SENSITIVE-COLUMN-ALLOWLIST` | P2 | `RESOLVED_IN_PLAN` | adapter explicit selector·내부 id/description/link 제외·민감 permission column omission |
| `EXPORT-UI-SUCCESS-SEMANTICS` | P3 | `RESOLVED_IN_PLAN` | 로컬 저장 단정 없이 파일 생성 완료만 안내 |

## 6. 완료 조건

- 1차 Fable planning과 Codex 내용 review 보존
- 2차 Fable planning `openBlockingDecisionCount: 0`
- 공통 export 계약과 우선 화면 adapter의 Backend·Frontend·isolated 검증
- 각 포함 화면 desktop·390px screenshot
- implementation report·SOP·user manual·user validation checklist·Roadmap 상태 갱신
- privacy/secret·diff·Finding gate 통과와 local experiment commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
