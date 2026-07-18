# TASK-014A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다. 2026-07-18 `TASK-013A` local commit 뒤 사용자가 “다음작업 시작”을 요청해 Roadmap의 다음 실험 기능 `TASK-014A`를 진행한다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-014A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-014a-sales-settlement`
- interviewSource: `tasks/014a-interview.md`
- firstPlanningSource: `tasks/014a-planning.md`
- codexReviewSource: `tasks/014a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/21-sales-settlement-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- 모든 active panel 납품·세금계산서 발행일·project 전체 open Pending 0건의 서버 재검증
- active panel 1개 이상과 Finalized DeliveryCompleted relation 기준의 납품 판정
- project-first lock order와 모든 Pending INSERT의 DB project lifecycle fence
- `UpdateProjectAsync`·panel count·일반 상태 mutation의 Completed lifecycle 차단
- `sales.settle`은 sales 업무 role에만 부여하고 System Administrator mutation permission 제거
- project scope + SalesPrimary/SalesSecondary/current settlement assignee actor 교집합
- 전역 정산 menu/queue 제거, project detail action/section + My Work deep link
- settlement Draft→Completed forward-only, invoice 발행일·번호·메모 bounded, 완료 후 불변
- 발행일은 project 생성일 이상·Asia/Seoul 오늘 이하
- operation fingerprint/replay·version·project→settlement row lock·stable 409
- settlement·work item·project status·stage event·audit·인앱 완료 알림의 단일 transaction
- 알림에 invoice/Pending 원문 복제 금지, active project assignee와 sales owner 중 actor 제외 recipient
- generic SalesSettlementCompleted work transition 차단, cancel/purge 정합
- 모바일 완료 조건 → invoice 입력 → 최종 확인 한 열과 desktop project-context composition
- 외부 회계·전자세금계산서·파일/OCR/PDF/Excel·수금·정정·provider·Persistent UAT·대표 repo 불변

## 4. Review Finding resolution

| ID | Severity | 2차 기획 요구 상태 | Resolution |
| --- | --- | --- | --- |
| `014A-PENDING-COMPLETION-RACE` | P1 | `RESOLVED_IN_PLAN` | project completion과 Pending insert가 project row를 같은 순서로 lock하고 Completed/Cancelled/deleted insert를 DB에서 거부 |
| `014A-POST-COMPLETION-MUTATION` | P1 | `RESOLVED_IN_PLAN` | project edit·panel count·일반 상태 mutation Completed 409 |
| `014A-DELIVERY-SOURCE-OF-TRUTH` | P2 | `RESOLVED_IN_PLAN` | Finalized delivery relation + active panel NOT EXISTS 검증 |
| `014A-PENDING-SCOPE` | P2 | `RESOLVED_IN_PLAN` | target type 무관 project 전체 non-Closed Pending 차단 |
| `014A-NAVIGATION-SCOPE` | P2 | `RESOLVED_IN_PLAN` | project subordinate route + My Work만 유지 |
| `014A-ADMIN-LEAST-PRIVILEGE` | P2 | `RESOLVED_IN_PLAN` | sales role만 `sales.settle`, admin mutation 403 |
| `014A-INVOICE-DATE-BOUNDARY` | P2 | `RESOLVED_IN_PLAN` | project 생성일≤발행일≤KST 오늘 |
| `014A-ZERO-PANEL-VACUOUS-COMPLETION` | P2 | `RESOLVED_IN_PLAN` | active panel count 0 별도 차단 |
| `014A-NOTIFICATION-BOUNDARY` | P2 | `RESOLVED_IN_PLAN` | bounded recipient, category/deep link only, 실제 provider 미생성 |

## 5. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/21-sales-settlement-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 6. Fable 사용량 기록

Claude `/usage` 측정은 Repository mutation 없이 `bash scripts/report-claude-usage.sh`만 사용한다. 5시간 현재 세션 사용·잔여·초기화 시각과 주간 전체/Fable 사용·잔여·초기화 시각을 함께 기록한다. reporter가 제공하지 못한 초기화 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 12:10 KST 초기화 | 0% 사용 / 100% 잔여 / 초기화 parse 불가 | 0% 사용 / 100% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 12:10 KST 초기화 | 0% 사용 / 100% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 16% 사용 / 84% 잔여 / 12:09 KST 초기화 | 2% 사용 / 98% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직후 | 16% 사용 / 84% 잔여 / 12:09 KST 초기화 | 2% 사용 / 98% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 구현 종료 최신 조회 | 30% 사용 / 70% 잔여 / 12:09 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 | 5% 사용 / 95% 잔여 / 07-25 07:59 KST 초기화 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 실행됐다. model 400초, stdout 27,772 bytes, stderr 0이며 `tasks/014a-planning.md`를 byte-for-byte 저장했다. 2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했고 model 226초, stdout 23,126 bytes, stderr 0이며 `docs/21-sales-settlement-plan.md`를 byte-for-byte 저장했다. 구현 종료 최신 조회는 첫 reporter 시도가 TUI timeout `exit 23`으로 실패한 뒤 같은 read-only reporter의 1회 재시도에서 성공했다. Task private Fable session과 transcript는 구현 종료 뒤 runner `cleanup`으로 제거했다.

## 7. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- Review Finding 9건이 최종 구현 계약에 resolution과 test 위치로 통합
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- project-context 정산 화면 desktop·390px screenshot
- implementation report·SOP·user manual·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
