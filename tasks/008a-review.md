# TASK-008A — 자재 도착·분할 입고 1차 기획 Codex 내용 Review

> Review 대상: `tasks/008a-planning.md` Fable 5 원문
> Review 성격: 사용자 문제·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

도착 건 단위 분할 입고, 최소 IQC 판정, 기존 Pending 자동 연결, legacy 완료값 backfill이라는 큰 방향은 유지한다. 현재의 `receipt_completed` boolean 하나를 5~7단계 전체로 사용하는 문제를 실제 현장 흐름으로 분리하면서도 TASK-009A·010A·008B의 경계를 보존하는 합리적인 vertical slice다.

다만 1차 기획대로 바로 구현하면 세 가지 구조적 문제가 남는다. 첫째, `receipt_completed`의 다른 writer인 구매 직접 수정과 Excel apply가 남아 신규 상태와 충돌한다. 둘째, 현재 `PendingStore`와 `WorkflowStore`는 각자 connection·transaction을 만들기 때문에 “IQC 부적합 판정 + Pending + 업무·알림”을 한 transaction으로 묶을 수 없다. 셋째, 도착 건에 Pending FK 하나만 두면 재검사 뒤 다시 부적합한 반복 cycle의 이력을 잃는다. 이 세 항목은 2차 기획에서 구체적인 구현 계약으로 고쳐야 한다.

## 2. 기능 판단

### 유지

- 구매품목 nullable 주문 수량과 단위, 도착 건별 수량·날짜·메모
- 도착 건별 IQC 요청·최소 적합/부적합 판정과 적합 건별 입고 확정
- 부적합 판정 시 기존 Pending 공통 모듈의 `Nonconformance` 자동 생성
- `receipt_completed=true` legacy 품목의 표시용 확정 도착 건 backfill
- 자재 담당과 품질 담당 mutation을 분리하고 System Administrator 업무 우회를 금지하는 서버 권한
- Desktop 관리형 화면과 390px 모바일 card·단계·bottom sheet 구성
- 상세 IQC·사진·PDF, 키팅, 사급, Excel 수량 열과 실제 provider를 별도 Task로 유지

### 추가

1. **단일 진실 writer inventory**
   - `receipt_completed`는 신규 Materials store만 계산해 갱신한다.
   - `/api/materials/receipts` bulk PATCH뿐 아니라 프로젝트 구매정보 PATCH, Excel preview/apply, 신규 row insert/update의 모든 writer를 조사해 직접 변경을 차단한다.
   - 기존 API response·dashboard·Excel 표시 필드는 호환 projection으로 유지하되 mutation 입력의 완료값은 무시하지 말고 안정적인 validation error로 거부한다.

2. **명시적인 transaction-aware integration**
   - 신규 `MaterialsStore`가 도착·IQC·확정 transaction의 owner가 된다.
   - `PendingStore`에는 기존 검증·history·assignment artifact를 재사용하는 internal transaction-aware 생성/종결 helper를 추가한다. 별도 `PendingStore.CreateAsync` 호출이나 HTTP self-call은 금지한다.
   - IQC 업무와 입고 확정 업무·인앱 알림도 같은 connection/transaction에서 idempotency key로 생성한다. 실제 delivery row는 만들지 않는다.

3. **반복 부적합 cycle 보존**
   - 도착 건에 단일 `pending_issue_id`를 두지 않는다.
   - 각 IQC 요청/판정 attempt가 `pending_issue_id`를 nullable로 참조한다. 한 도착 건은 시간상 여러 Pending과 연결될 수 있고, 동일 시점의 open Pending만 최대 1건이다.
   - 재부적합 시 open Pending이 있으면 재사용하고, 이전 Pending이 Closed면 새 Pending을 만들어 과거 cycle을 보존한다.

4. **재검사 적합과 Pending 종결의 원자성**
   - 연결 Pending이 `ReinspectionRequested`일 때만 재요청을 허용한다.
   - 재검사 적합 판정 transaction에서 해당 Pending을 `Closed`로 전이하고 history·work item을 함께 종결한다. 그래야 적합 자재인데 프로젝트에는 open Pending이 남는 모순이 없다.
   - Pending→자재 자동 callback은 만들지 않는다. 상태 결합 방향은 계속 Materials→Pending이다.

5. **수량·단위 계약**
   - 수량은 `numeric(18,3)`의 양수로 사용한다.
   - 신규 비-legacy 품목은 주문 수량과 단위를 한 쌍으로 입력한다. 한쪽만 존재하는 상태는 서버와 DB에서 차단한다.
   - 단위는 아직 master를 만들지 않고 trim된 자유 입력 1~20자로 제한한다. 도착 건은 품목 단위를 상속해 서로 다른 단위를 섞지 않는다.
   - 주문 수량이 없는 legacy 완료 건만 `quantity=null`, `unit=null`, `is_legacy=true`를 허용한다.

6. **업무 단위와 deep link**
   - IQC work item은 `target_type='Inspection'`, `target_id=iqc_request_id`를 사용한다.
   - 입고 확정 work item은 `target_type='ProcurementItem'`, `target_id=procurement_item_id`를 사용하되 idempotency key에 도착 건 ID를 포함한다.
   - 각 업무는 `/quality/iqc?request=…`, `/materials/receipts?receipt=…`처럼 기존 shell 안의 실제 action 위치로 이동한다.

### 보류

- 프로젝트 대표 단계·진행률 공식을 이번 Task에서 새로 재설계하는 작업. 신규 흐름은 기존 `receipt_completed` 호환 projection과 work item sync까지만 연결하고 TASK-007B 병목 계약을 깨지 않는다.
- 구매 Excel의 주문 수량·단위 열 추가. 다만 기존 Excel의 입고 완료 변경 시도는 단일 진실 보호를 위해 validation error로 차단한다.
- 평균 리드타임, lot/vendor 품질 분석, bulk 도착 등록, barcode/QR 연결.
- Persistent UAT migration과 runtime handover.

### 제거

- 도착 수량의 in-place 수정: IQC 요청 전에는 사유 필수 취소 후 새 도착 건으로 다시 등록한다.
- IQC 요청 뒤 도착 건 취소, 입고 확정 취소, 도착 마감 해제: 18단계와 append-only 이력을 뒤로 움직이므로 이번 MVP에서 제거한다.
- Pending 상태 변경이 자재 상태를 자동 변경하는 역방향 callback.
- 기존 boolean 편집과 신규 상태를 병행하는 compatibility write.

## 3. 최종 상태·전이 권고

```text
도착 건: Arrived → IqcRequested → Passed → Confirmed
                         ↘ FailedBlocked → ReinspectionRequested → IqcRequested

취소: Arrived 상태에서만 Cancelled (사유 필수, 새 도착 건으로 재등록)
부적합: FailedBlocked에서 Pending open
재검사 적합: Passed + Pending Closed를 같은 transaction에서 처리
```

- 품목 도착 상태는 `미도착 / 부분도착 / 도착마감`으로 표시한다.
- 주문 수량을 모두 채우면 “도착 마감” action을 제안하지만 자동 마감하지 않는다.
- `receiptCompleted`는 `도착마감 && 유효 도착 건 1개 이상 && 모든 유효 도착 건 Confirmed`일 때만 true다.
- legacy backfill 건은 이미 `Confirmed`이며 재편집 action을 제공하지 않는다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `008A-SINGLE-TRUTH-WRITERS` | P1 | `RESOLVED_FOR_REDRAFT` | 구매 PATCH·Excel apply가 boolean을 계속 쓰면 신규 상태와 충돌 | 모든 writer inventory와 direct-write validation 차단을 필수 계약으로 추가 |
| `008A-ATOMIC-INTEGRATION` | P1 | `RESOLVED_FOR_REDRAFT` | Pending/Workflow store가 자체 transaction을 열어 원자적 부적합 흐름 불가 | Materials transaction owner + transaction-aware Pending helper로 고정 |
| `008A-REPEAT-NONCONFORMANCE` | P2 | `RESOLVED_FOR_REDRAFT` | 도착 건의 단일 Pending FK는 반복 검사 cycle 이력 손실 | IQC attempt별 Pending 참조와 open 1건 규칙으로 변경 |
| `008A-PENDING-CLOSURE` | P2 | `RESOLVED_FOR_REDRAFT` | 재검사 적합 뒤 Pending이 열려 있으면 자재 상태·프로젝트 완료 조건 모순 | 적합 판정과 Pending Closed를 같은 transaction으로 처리 |
| `008A-QUANTITY-UNIT` | P2 | `RESOLVED_FOR_REDRAFT` | nullable 수량·단위의 pair·정밀도·단위 혼합 규칙 미정 | numeric(18,3), pair invariant, 품목 단위 상속, legacy 예외 명시 |
| `008A-BACKWARD-TRANSITIONS` | P2 | `RESOLVED_FOR_REDRAFT` | in-place 정정·확정 취소·마감 해제는 forward-only 단계와 감사 해석 훼손 | Arrived에서만 사유 취소, 이후 상태는 되돌리지 않음 |
| `008A-WORK-ITEM-TARGET` | P2 | `RESOLVED_FOR_REDRAFT` | 프로젝트 stage 단위 idempotency는 여러 분할 건 업무를 덮을 수 있음 | Inspection/ProcurementItem target과 도착 건별 idempotency key 사용 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 2차 Fable 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 부적합 Pending 초기 담당 | `Registered`·`Urgent`·미배정 | 생산관리의 기존 배정 흐름과 잘못된 자동 담당 회피 |
| 주문 단위 | 자유 입력 1~20자, trim | master taxonomy를 이번 Task에 추가하지 않음 |
| 도착 마감 | 수량 도달 시 제안, 자재가 명시 실행 | legacy/오차/분할 상황에서 자동 전이 회피 |
| 재검사 적합 | Pending 자동 Closed | open Pending 0 완료 조건과 상태 일치 |
| 기존 Excel 입고 완료 변경 | validation error | silent ignore보다 사용자 행동 가능성이 높고 단일 진실 보존 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0030` migration 하나로 수량·단위, 도착 건, IQC attempt, event 이력과 legacy backfill을 구현한다. 기존 migration은 수정하지 않는다.
2. 신규 Materials API는 조회, 주문 수량·단위 저장, 도착 등록·Arrived 상태 취소, 도착 마감, IQC 요청, IQC 판정, 입고 확정만 제공한다.
3. `receipt_completed`는 신규 store만 갱신하고 기존 모든 direct writer는 validation error로 차단한다.
4. 부적합·재검사 적합은 Pending·history·work item·notification과 같은 transaction에 묶는다.
5. 상세 IQC·사진·PDF, 키팅, 사급, Excel 수량 열, reverse transition과 Persistent UAT는 포함하지 않는다.
6. Desktop과 모바일 자재 화면, 품질 IQC 화면, Pending deep link를 구현한다.
7. migration fresh/existing/backfill, 권한 matrix, 수량 경쟁, 중복 업무·Pending, 기존 Pending·구매·Excel 회귀, desktop·390px를 검증한다.

## 7. 권장 구현 순서

1. `0030` schema·backfill·DB constraint와 migration tests
2. Materials contracts·store·transaction-aware Pending helper
3. Materials/IQC endpoints·authorization·work item/notification 연결
4. 기존 receipt boolean writer 차단과 호환 read projection 회귀
5. Frontend API type·자재 화면·IQC 화면·deep link
6. Backend targeted/전체, Frontend unit/build, isolated E2E
7. desktop·390px screenshot, implementation report와 독립 검증

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
