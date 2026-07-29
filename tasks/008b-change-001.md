# TASK-008B Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 앞으로 모든 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-008B`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-008b-customer-supplied-materials`
- interviewSource: `tasks/008b-interview.md`
- firstPlanningSource: `tasks/008b-planning.md`
- codexReviewSource: `tasks/008b-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/15-customer-supplied-materials-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- `supply_type = Purchased | CustomerSupplied`와 기존 `order_quantity`·`order_unit` 단일 pair 재사용
- 사급 `예정량 / 누적 도착량 / 입고 확정량 / 미도착 잔량 / 처리 대기량` derived projection 분리
- 사급 마감은 누적 도착량=예정량과 기존 008A 마감 조건을 모두 만족할 때만 허용
- 제공 지연은 `receipt_completed`가 아니라 미도착 잔량 기준으로 판정
- `0031` supply enum CHECK + CustomerSupplied conditional measurement CHECK
- Direct PATCH의 omitted-preserve, 신규 Purchased default, Excel supply/measurement 보존
- 기존 item의 공급 유형·예정량·단위 변경 시 사유 필수와 old/new audit
- Materials/IQC 기존 권한 유지, 구매 read projection의 additive supply 정보, 신규 permission·알림 없음
- 공급 유형·수량 update와 도착 등록 경쟁을 같은 품목 row lock 순서로 보호
- 일반 구매품, `0030`, 008A 상태 machine·`receipt_completed`·Pending·work item·Excel 계약 보존

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/15-customer-supplied-materials-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 5. Fable 사용량 기록

Claude `/usage` 퍼센트는 정수 반올림 값이다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| 1차 planning 직전 | 14% | 86% | 27% | 73% |
| 1차 planning 직후 | 14% | 86% | 28% | 72% |
| 2차 planning 직전 | 14% | 86% | 28% | 72% |
| 2차 planning 직후 | 15% | 85% | 29% | 71% |

1차 planning은 471초가 걸렸고 Fable 표시 사용량이 1%p 증가했다. 2차 planning은 baseline을 재사용해 preflight 1초·model 234초가 걸렸고 전체 모델과 Fable 표시 사용량이 각각 1%p 증가했다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- 구매 조회/편집, 자재 입고, IQC의 desktop·390px screenshot
- implementation report·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
