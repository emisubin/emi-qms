# TASK-008A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 다음 작업부터 `Fable 기획 → Codex review → review 내용을 바탕으로 Fable 2차 기획 → 2차 기획 기준 Codex 코딩` 순서로 진행하도록 명시했다. 이 실험 branch에서는 인터뷰·채택·확인을 다시 묻지 않고 권장안을 적용해 결과물까지 만들며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Fable 2차 기획 승인

- fablePrimaryDraftApproved: true
- fablePrimaryDraftSource: `USER_EXPLICIT_REQUEST`
- fablePrimaryDraftTarget: `docs/14-material-receiving-plan.md`
- fableRedraftApproved: false
- fableRedraftSource: `NOT_APPLICABLE`
- fableRedraftTarget: `N/A`

`tasks/008a-planning.md`는 Fable 1차 기획 원문이다. `docs/14-material-receiving-plan.md`는 사용자가 명시 요청한 두 번째 Fable 기획이며, `tasks/008a-review.md`의 유지·추가·보류·제거 판단과 7개 resolution을 입력으로 다시 작성한다. 두 문서는 이번 사용자 workflow 때문에 의도적으로 분리되며 Codex는 어느 Fable 원문도 수정하지 않는다.

## 3. 2차 기획 필수 반영사항

- `receipt_completed` 단일 진실과 기존 구매 PATCH·Excel apply 포함 모든 writer 차단
- Materials transaction owner와 transaction-aware Pending 생성·종결 helper
- IQC attempt별 Pending 참조와 반복 부적합 cycle 보존
- 재검사 적합·Pending Closed·work item/history의 원자적 처리
- `numeric(18,3)` 수량, 수량·단위 pair invariant, legacy 예외
- Arrived에서만 사유 필수 취소, IQC 이후 reverse transition 제거
- IQC/입고확정 work item target과 도착 건별 idempotency key
- 부적합 Pending은 `Registered`·`Urgent`·미배정으로 생성
- 상세 IQC·사진·PDF, 키팅, 사급, Excel 수량 열, Persistent UAT·실제 provider 제외

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 review resolution을 반영하는 조건, experiment branch 한정
- implementationApproved: `true` — `docs/14-material-receiving-plan.md`와 `tasks/008a-review.md`의 최소 계약 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` — main merge 승인 `0/3`

## 5. Fable 사용량 기록

Claude `/usage` 퍼센트는 정수 반올림 값이다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| 최초 Fable 호출 전 | 10% | 90% | 19% | 81% |
| 1차 planning 직전 | 10% | 90% | 19% | 81% |
| 1차 planning 직후 | 13% | 87% | 25% | 75% |
| 2차 planning 직전 | 13% | 87% | 25% | 75% |
| 2차 planning 직후 | 13% | 87% | 25% | 75% |

2차 기획 호출은 정수 반올림 구간 안에서 사용량이 증가해 표시 퍼센트 변화가 없었다.

2차 planning 직전·직후 값을 이 표와 implementation report에 추가한다.
