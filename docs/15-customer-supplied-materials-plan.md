# TASK-008B 사급 자재 기획 — 공급 유형·제공 예정량·잔량 추적 (Fable 2차 기획)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-008B`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/008b-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`, open blocking 결정 0)
- firstDraftSource: `tasks/008b-planning.md` (Fable 1차 기획 원문, 수정하지 않음)
- reviewSource: `tasks/008b-review.md` (Codex 내용 review, Finding 8건 resolution)
- approvalSource: `tasks/008b-change-001.md` (experiment two-pass 규칙에 따른 이 문서의 second-planning target 승인)

## 1. 문서 목적과 위치

이 문서는 `TASK-008B`의 두 번째 Fable 기획 전문이며 이 실험 Task의 authoritative implementation contract다. `tasks/008b-planning.md`의 채택된 방향(권장안 A: `supply_type` 한 컬럼 + 기존 `order_quantity`·`order_unit` pair 재사용, 신규 알림 없음, 008A 원장 전면 재사용)을 유지하되, `tasks/008b-review.md`의 유지·추가·보류·제거 판단과 8개 Finding resolution, `tasks/008b-change-001.md`의 필수 반영사항을 구현 가능한 최종 계약으로 통합한다. 1차 기획과 review 원문은 판단 이력으로 보존하며 어느 쪽도 덮어쓰지 않는다.

이 문서 자체는 게시 승인을 부여하지 않는다. 승인 값과 Git 경계는 `tasks/008b-change-001.md`를 따른다: experiment branch 한정 구현·검증·screenshot·local commit까지이며 대표 repo, GitHub `main`(merge 승인 `0/3`), push·PR·merge, Persistent UAT, 실제 provider는 포함하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 참조하며 여기에 복사하지 않는다.

## 2. 확정된 기준선

### 2.1 Interview·1차 기획에서 유지되는 확정 사항

- 사급은 구매품목의 공급 유형이다: `supply_type = Purchased(기본) | CustomerSupplied`. 별도 사급 원장·화면·테이블을 만들지 않는다.
- 제공 예정 수량·단위는 기존 `order_quantity`·`order_unit` pair를 재사용한다(라벨만 유형별 표기). 수량·단위·초과 검증의 단일 진실을 유지한다.
- 도착 건 상태 machine(`Arrived → IqcRequested → Passed/FailedBlocked → Confirmed`, `Arrived`에서만 사유 필수 취소), IQC attempt·Pending 차단·재검사 gate, `receipt_completed` derived 단일 진실과 DB trigger·writer 차단은 변경하지 않는다.
- 공급 유형 변경은 유효(미취소) 도착 건이 0건일 때만 양방향 허용한다.
- 사급 지정 시 제공 예정 수량·단위 pair는 필수다. 수량 하한은 누적 도착량, 단위는 유효 도착 건 존재 시 고정, row lock 후 합산 재검증을 적용한다.
- 신규 permission·policy·알림·외부 delivery를 만들지 않고 기존 IQC·입고 확정 내 업무와 deep link를 재사용한다.
- migration은 `0031` additive 1건뿐이며 `0030` 포함 기존 migration은 수정하지 않는다.

### 2.2 Codex review resolution (이 문서에서 고정)

| Finding | 이 문서의 반영 |
| --- | --- |
| `008B-SHORT-CLOSE` (P1) | 사급 전용 마감 gate: 누적 도착량=예정량을 추가로 강제, 일반 구매 마감 정책 불변 — 7장 |
| `008B-DELAY-SEMANTICS` (P1) | `제공 지연`을 `receipt_completed`가 아니라 미도착 잔량 기준으로 서버가 derived 계산 — 8장 |
| `008B-READ-AUTH-MISMATCH` (P1) | Materials/IQC endpoint 권한을 확대하지 않고, 구매 read projection에 supply 정보를 additive 노출 — 5·12장 |
| `008B-REQUEST-PRESERVATION` (P1) | direct PATCH omitted=preserve, 신규 item 기본 `Purchased`, Excel 보존 계약 — 10장 |
| `008B-DB-CONDITIONAL-PAIR` (P2) | `0031`에 CustomerSupplied conditional measurement DB CHECK 추가 — 6.4장 |
| `008B-QUANTITY-SEMANTICS` (P2) | 예정/누적 도착/입고 확정/미도착 잔량/처리 대기량 derived projection 분리 — 6.2장 |
| `008B-AUDIT-REASON` (P2) | 기존 item의 supply 필드 변경 시 3~500자 사유 필수와 old/new 동일 transaction 기록 — 11장 |
| `008B-SUPPLIER-DISPLAY` (P3) | 고객 제공 책임 label과 `supplier_name` 참고값의 의미 분리, 자동 삭제·덮어쓰기 금지 — 13장 |

### 2.3 Review에서 자동 채택된 비차단 결정

- 사급 총량 model은 기존 pair 재사용(권장안 A 유지).
- 부족 마감은 차단하고, 고객 합의로 총량이 줄었으면 구매 담당이 사유와 함께 예정량을 실제 도착량 이상으로 정정한 뒤 마감한다.
- 제공 지연은 미도착 잔량 기준으로만 판정한다.
- 인앱 handoff는 기존 IQC·입고 확정 업무 재사용으로 충분하며 신규 알림 taxonomy를 만들지 않는다.
- 조회 권한은 기존 Materials(`MaterialReceiptUpdate`)/IQC(`QualityInspect`) policy를 유지한다.

### 2.4 선행 의존성 (현재 실험 branch 기준)

- TASK-008A가 이 branch에 구현·자동 검증 완료 상태다: `material_receipts`·`material_iqc_attempts`·`material_receipt_events`, 품목 row lock transaction owner인 Materials store, `0030` migration과 `receipt_completed` guard trigger, `/materials/receipts`·`/quality/iqc` 적응형 화면.
- `project_procurement_items`에는 `order_quantity`·`order_unit`(pair CHECK)과 도착 마감 컬럼이 이미 있다. 현재 pair는 자재 담당의 첫 도착 등록에서 입력되며 구매 direct PATCH 계약에는 없다.
- 구매 direct PATCH는 품목 row lock, row version 충돌 409, field old/new의 `project_audit_events` 기록(`Direct` source)과 history 조회를 이미 제공한다. 프로젝트 구매 GET은 프로젝트 read 접근 검사 기반이라 생산관리·Read-only도 조회할 수 있다.
- `/api/materials/receipts` GET은 `MaterialReceiptUpdate` policy에 묶여 있다 — 이 Task에서 넓히지 않는다.
- 현재 migration 최신 번호는 `0030`이며 다음 additive 번호는 `0031`이다.
- canonical Roadmap 순서 대비 실험 재정렬은 interview Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록되어 있다. canonical 실행 큐와 `TASK-007A` Next Gate는 변경하지 않는다.

## 3. 한 줄 목표

구매 담당이 구매품목을 사급(고객 제공)으로 분류하고 제공 예정량을 관리하면, 자재·품질 담당은 기존 도착·IQC·입고 확정 흐름 그대로 사급품의 누적 도착·입고 확정·미도착 잔량·처리 대기량과 고객 제공 지연을 일반 구매품과 혼동 없이 추적할 수 있다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 구매 담당 (`ProcurementPlanUpdate`) | 공급 유형 지정·변경(gate 안에서), 사급 제공 예정 수량·단위 입력·정정, 변경 사유 기록 | 접근 가능한 프로젝트 구매품목 | 기존 구매 direct PATCH 범위의 신규 supply 필드 |
| 자재 담당 (`MaterialReceiptUpdate`) | 사급품 도착 등록·`Arrived` 취소·IQC 요청·입고 확정·도착 마감(사급 gate 적용), 수량 projection 확인 | 기존 자재 목록(policy 불변) | 기존 008A 자재 mutation 전체(변경 없음) |
| 품질 IQC 담당 (`QualityInspect`) | 사급 badge가 표시된 기존 최소 판정 | 기존 IQC queue(policy 불변) | 기존 판정만(변경 없음) |
| 생산관리·Read-only (프로젝트 read 접근) | 프로젝트 구매 조회에서 공급 유형·제공 예정량 확인 | 기존 프로젝트 구매 GET 범위 | 업무 mutation 없음 |
| System Administrator | 기존 정책 범위 조회 | 기존 정책 범위 | 업무 mutation 우회 금지 |

권한 경계 원칙: 이 Task는 어떤 endpoint의 policy도 넓히거나 좁히지 않는다. 생산관리·Read-only의 공급 책임 확인은 자재·IQC 상세 endpoint가 아니라 기존 프로젝트 구매 read projection의 additive 필드로 제공한다. 누적 도착·확정량 상세는 기존 자재 권한 화면에만 둔다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 사급 지정과 제공 예정 기준 관리

1. 구매 담당이 구매 편집에서 품목의 공급 유형을 `사급(고객 제공)`으로 선택한다. 같은 저장 요청에 제공 예정 수량·단위 pair가 없으면 서버가 field 단위 한글 오류로 거부한다.
2. 기존 item의 공급 유형·예정 수량·단위가 바뀌는 저장은 3~500자 수정 사유가 필수이며, `SupplyType`·`OrderQuantity`·`OrderUnit`의 old/new가 같은 transaction에서 기존 구매 변경 이력에 기록된다.
3. 저장 후 프로젝트 구매 조회에서는 프로젝트 read 권한 사용자도 `사급 · 고객 제공` label과 예정량을 보고, 자재 화면에서는 자재 담당이 수량 projection을 본다.
4. 유효 도착 건이 1건이라도 생기면 공급 유형 변경은 양방향 모두 서버가 거부한다. 예정 수량 정정은 도착 후에도 가능하나 누적 도착량 미만 축소와 단위 변경은 차단된다.

### 시나리오 B — 사급 분할 입고와 수량 projection (008A 흐름 재사용)

1. 자재 담당이 사급 품목에 도착 등록을 한다. 단위는 품목 단위를 상속하고, 유효 도착 수량 합이 제공 예정량을 초과하면 기존 row-lock 합산 검증이 유형별 문구(제공 예정량 기준)로 차단한다. 사급품은 pair가 이미 존재하므로 첫 도착 등록의 pair 입력 단계는 나타나지 않는다.
2. 목록 카드에 제공 예정량, 누적 도착량, 입고 확정량, 미도착 잔량, 처리 대기량이 표시된다.
3. IQC 요청 → 판정 → 부적합 Pending 차단·재검사 → 입고 확정은 기존 008A 계약 그대로다.

### 시나리오 C — 부족 마감 차단과 정정 후 마감

1. 미도착 잔량이 남은 사급 품목에 도착 마감을 시도하면 서버가 잔량과 함께 한글 오류로 거부한다. 일반 구매품의 기존 수동 마감 정책은 변경되지 않는다.
2. 고객과 합의해 총량이 줄었으면 구매 담당이 사유와 함께 예정량을 실제 누적 도착량 이상으로 정정한다.
3. 누적 도착량=예정량이고 기존 008A 마감 조건(유효 건 ≥ 1, 모든 유효 건 `Confirmed`)을 만족하면 자재 담당이 마감을 선언하고 `receipt_completed`가 derived로 `true`가 된다. 잔량이 남은 채 완료되는 경로는 존재하지 않는다.

### 시나리오 D — 제공 지연과 내부 처리 대기의 구분

1. 사급 품목이 입고예정일(고객 제공 예정일로 해석)을 지나고 미도착 잔량이 0보다 크면 서버가 `제공 지연` derived 값을 내려주고 목록이 이를 표시한다.
2. 전량 도착 후 IQC·확정 대기만 남은 품목은 `제공 지연`이 아니라 처리 대기량으로 표시된다. `receipt_completed=false`는 지연 판정에 사용하지 않는다.
3. 기준일은 서버가 일관되게 계산하며 Frontend는 local clock으로 별도 판정하지 않는다.
4. 동시 편집(공급 유형·수량 정정 vs 도착 등록)은 같은 품목 row lock 순서로 직렬화되고 version 불일치 쪽이 409와 재조회 안내를 받는다.

## 6. 데이터·상태 모델

### 6.1 공급 유형

```text
공급 유형: Purchased ↔ CustomerSupplied
  변경 gate: 유효(미취소) 도착 건 = 0 (row lock 후 판정, 양방향 동일)
  기존 행·legacy backfill 행·신규 Excel 행: Purchased
```

- 공급 유형은 구매품목의 속성이고 도착 건 상태 machine은 변경하지 않는다.
- legacy backfill 품목은 `Confirmed` 유효 도착 건을 이미 가지므로 사급 전환이 자연히 차단된다.
- 전환 시 기존 `supplier_name`과 measurement pair를 자동 삭제·덮어쓰기하지 않는다. pair는 “품목의 총 예정량”이라는 공통 의미로 보존하되 `CustomerSupplied` 전환·유지에는 pair 존재를 강제한다.

### 6.2 사급 수량 projection (모두 derived, 저장하지 않음)

```text
예정량       = order_quantity
누적 도착량  = SUM(receipt.quantity WHERE status <> 'Cancelled')
입고 확정량  = SUM(receipt.quantity WHERE status = 'Confirmed')
미도착 잔량  = 예정량 - 누적 도착량
처리 대기량  = 누적 도착량 - 입고 확정량

제공 지연    = CustomerSupplied AND expected_receipt_date < 서버 기준일 AND 미도착 잔량 > 0
사급 마감    = 누적 도착량 = 예정량 AND 유효 도착 건 >= 1 AND 모든 유효 도착 건 = Confirmed
```

- DB에 중복 aggregate 컬럼을 만들지 않고 `material_receipts`에서 계산한다.
- “고객이 아직 제공하지 않음(미도착 잔량)”과 “고객은 제공했지만 내부 IQC·확정 대기(처리 대기량)”를 항상 분리해 표기한다.

### 6.3 데이터 개념

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `supply_type` | `Purchased`(기본)/`CustomerSupplied`, enum CHECK | 기존 테이블 additive 컬럼 | 변경 시 old/new·사유를 기존 구매 변경 이력에 기록 |
| 제공 예정 수량·단위 | 기존 `order_quantity`/`order_unit` pair 의미 확장 | 기존 컬럼 재사용 | 기존 pair CHECK 유지 + conditional CHECK 추가, 변경 사유 필수 |
| 수량 projection·제공 지연 | 6.2장 derived 값 | 파생 값 | 원장에서 재계산 가능, 상태 저장 없음 |
| 도착·IQC·event 원장 | `material_receipts`·`material_iqc_attempts`·`material_receipt_events` | 기존 재사용(변경 없음) | 기존 append-only 계약 유지 |
| `receipt_completed` | 기존 derived 단일 진실 | 변경 없음 | guard trigger·writer 차단 유지 |

### 6.4 `0031` migration 계약

- additive 1건: `supply_type text not null default 'Purchased'` 컬럼, `('Purchased','CustomerSupplied')` enum CHECK, 그리고 `CustomerSupplied`이면 `order_quantity > 0`이고 trim된 `order_unit`이 1~20자여야 한다는 conditional measurement CHECK를 추가한다. 기존 pair CHECK와 함께 `Purchased` 기존 행과 legacy null pair는 그대로 허용한다.
- 데이터 이동·backfill DML은 없다(기본값으로 호환). schema 적용 직후 위반 행 0을 isolated fresh/existing DB에서 검증한다.
- `0030` 포함 기존 migration은 수정하지 않는다. rollback은 destructive down이 아니라 forward-fix 원칙으로 문서화하고, 실 DB(운영·Persistent UAT) 적용은 이 문서와 별개의 사용자 승인이 필요하다.

## 7. 사급 전용 마감 gate

- `CloseArrivals`는 `CustomerSupplied` 품목에 한해 기존 008A 조건에 `누적 도착량 = 예정량`을 추가로 강제한다. 잔량이 남으면 잔량 수치를 포함한 한글 오류로 거부한다.
- `Purchased` 품목의 기존 수동 마감 정책·문구·derived 완료 계약은 변경하지 않는다.
- 사급 마감 gate가 잔량 0을 먼저 보장하므로 `receipt_completed` derived 계산 자체는 수정할 필요가 없고 수정하지 않는다.
- 마감 판정은 품목 row lock 후 같은 transaction에서 유효 도착 건을 재조회해 수행한다.

## 8. 제공 지연 계약

- 판정식은 6.2장과 같다. `receipt_completed=false`를 지연 조건으로 사용하는 규칙은 제거되었다(review `제거` 판단).
- 서버가 일관된 기준일로 계산한 derived 값을 목록 응답에 포함하고, Frontend는 표시만 담당한다.
- 지연은 표시 전용이다. 신규 상태 저장, 신규 work item·notification·외부 채널 발송을 만들지 않는다(보류 항목, 20장).

## 9. Transaction·동시성 계약

- 구매 direct PATCH의 supply 필드 처리(유형 변경 gate, pair·floor·단위 고정 검증, audit 기록)는 기존 품목 row lock transaction 안에서 수행하며, gate·floor 판정에 필요한 `material_receipts` 합산은 lock 이후 같은 transaction에서 재조회한다.
- Materials의 도착 등록·마감도 기존대로 품목 row를 먼저 lock한다. 두 경로가 같은 품목 row lock을 첫 순서로 획득하므로 “유형 변경 vs 첫 도착 등록”, “예정량 축소 vs 신규 도착” 경쟁이 직렬화된다. 교차 경쟁 테스트를 추가한다.
- row version 불일치는 기존 409·재조회 안내 계약을 재사용한다.
- Materials store의 transaction owner 구조, Pending helper, work item idempotency는 변경하지 않는다.

## 10. Request 보존·writer inventory 계약

- 구매 direct PATCH item 계약에 supply 필드를 additive로 추가하되 omitted(null) = 현재 값 보존으로 고정한다. 일반 구매품 일괄 저장이 Materials 흐름이 기록한 `order_quantity`·`order_unit`을 null로 지우는 경로를 만들지 않는다.
- pair·supply type을 null로 “지우는” 기능은 이번 Task에서 제공하지 않는다.
- 신규 direct 입력 item: omitted supply type은 `Purchased`로 생성한다. 신규 `CustomerSupplied` item은 같은 요청에 pair가 반드시 있어야 한다.
- Excel preview/apply: supply 관련 열을 추가하지 않으며, 기존 item의 `supply_type`·measurement pair를 항상 보존하고 신규 Excel item은 `Purchased`로 생성한다. Excel이 사급 값을 바꾸는 묵시 경로가 없음을 회귀 테스트로 고정한다.
- `receipt_completed` direct writer 차단(API validation·store·DB trigger)은 기존 그대로 유지하고 supply 필드가 이를 우회하지 않음을 확인한다.

## 11. 변경 감사 계약

- 기존 item의 `SupplyType`, `OrderQuantity`, `OrderUnit` 중 하나라도 변경되는 direct 저장은 3~500자 수정 사유를 필수로 한다(신규 item 생성·무변경 저장은 기존 사유 정책 유지).
- 변경 old/new는 같은 구매 저장 transaction에서 기존 `project_audit_events` 방식(`Direct` source·correlation)으로 기록하고 기존 history 화면에서 조회된다.
- 신규 item의 최초 supply 값은 기존 신규 행 감사 방식으로 기록한다.
- `material_receipt_events`에 신규 event type을 추가하지 않는다. hard delete는 없다.

## 12. API·Backend 계약

신규 endpoint는 만들지 않는다 (경로·이름은 구현 시 기존 convention에 맞추고, 내부 클래스명·컬럼명·SQL 형태는 이 문서가 최종 확정하지 않는다).

- 구매 direct PATCH (`ProcurementPlanUpdate`): item 계약에 supply 필드 additive 추가와 10·11장 보존·사유·audit, 9장 gate·floor·단위 고정 검증.
- 프로젝트 구매 GET (프로젝트 read 접근): item 응답에 `supplyType`과 예정 수량·단위를 additive 포함 — 생산관리·Read-only의 공급 책임 확인 경로. 대시보드 집계 계약은 변경하지 않는다.
- `/api/materials/receipts` GET (`MaterialReceiptUpdate`, policy 불변): item 응답에 `supplyType`과 6.2장 derived projection·`제공 지연` 값을 additive 포함. `supplyType` 필터는 `All/Purchased/CustomerSupplied`만 허용하고 잘못된 값은 validation error로 거부하며, 필터 적용 후 summary와 item 목록은 같은 집합을 설명한다.
- `/api/quality/iqc` GET (`QualityInspect`, policy 불변): queue item에 `supplyType` additive 포함(표시용).
- 자재 mutation: 도착 등록 초과 오류와 마감 안내 문구를 공급 유형별로 표기(발주 수량 ↔ 제공 예정량)하고, 사급 마감 gate(7장)를 추가한다. 그 외 mutation 계약·idempotency·업무 생성은 변경하지 않는다.
- 공통 규칙: 안정적 status와 한글 메시지, version 충돌 409, 동일 이벤트 재실행의 중복 생성 금지, System Administrator 우회 없음.

## 13. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 구매 편집 (기존) | 프로젝트 구매정보 편집 | 품목별 공급 유형 select, 사급 pair 입력, 변경 불가(도착 건 존재) 이유, 기존 값 보존 표시 | 공급 유형 지정·정정, 예정 수량 정정, 사유 입력, 기존 일괄 저장 | 기존 저장 feedback 재사용, field 오류·첫 오류 focus, 사유 누락 오류, 409 재조회 안내 |
| 프로젝트 구매 조회 (기존 read) | 프로젝트 상세 구매 tab | `사급 · 고객 제공` label과 예정량(additive) | 조회만 | 기존 계약 유지 |
| 자재 입고 현황 (기존 `/materials/receipts`) | 자재 메뉴 | `사급` badge, 공급 책임 label(업체 참고값과 분리 병기), 예정/도착/확정/미도착/대기 수량, `제공 지연` 표시, 공급 유형 필터 chip | 기존 도착 등록·취소·IQC 요청·확정·마감(사급 gate 문구 포함) | 기존 action 인접 feedback, 잔량 포함 마감 차단 오류 |
| IQC 대기 (기존 `/quality/iqc`) | 품질 메뉴 | 카드에 사급 badge 추가 | 기존 판정(변경 없음) | 기존 계약 유지 |

확인할 UX 항목:

- 일반/사급, 고객 제공 책임과 업체 참고값, 미도착 잔량과 처리 대기량이 각각 혼동 없이 읽히는가?
- 사급 지정·수량 정정의 저장 결과·사유 요구·차단 이유가 action 근처에 보이는가?
- 구분 변경 불가·권한 부족·409·마감 차단이 서로 구분되어 안내되는가?
- 390px·Teams narrow에서 표 축소가 아니라 badge·핵심 수량·다음 행동 우선의 카드·bottom sheet를 유지하고 page-level horizontal overflow 0인가?
- badge·지연 표시는 색상만이 아니라 텍스트로 전달되는가(접근성)?

Frontend는 기존 수동 router, `LoadState`·`StateMessage`·공통 Action Feedback(중복 submit 차단, 첫 오류 focus, `aria-live`)과 기존 자재 카드·sheet·필터 패턴을 재사용하며, supply invariant·지연 기준을 Frontend에서만 결정하는 구현은 금지한다.

## 14. 포함·제외 범위

### 포함

- `0031` additive migration: `supply_type` enum CHECK + CustomerSupplied conditional measurement CHECK
- 구매 direct PATCH의 supply 필드, omitted-preserve, 신규 기본값, gate·floor·단위 고정, 사유 필수·old/new audit
- Excel의 supply/measurement 보존 계약과 회귀 고정
- Materials 응답의 supplyType·수량 projection·제공 지연·허용 필터와 동일 집합 summary, IQC queue의 supplyType 표시
- 사급 전용 마감 gate와 유형별 오류·안내 문구
- 프로젝트 구매 read projection의 additive supply 정보
- 구매 편집·구매 조회·자재·IQC 화면의 desktop·390px 적응형 반영
- isolated migration·Backend·Frontend·Full-Stack E2E·browser 검증과 페이지별 screenshot

### 명시적 제외

- TASK-008A data model·상태 machine·`0030`·`receipt_completed`·Pending·work item 계약 재구현·수정
- Materials/IQC endpoint 권한 변경(확대·축소 모두), 신규 permission·policy
- 사급 지연용 신규 work item·인앱 notification·Teams/Mail 에스컬레이션, 실제 provider 발송
- 고객사별 제공 약속 version·contact·납품 차수 계획·약속 변경 승인 workflow, 고객 포털·ERP·SCM·고객 직접 입력
- 사급 부족의 프로젝트 병목 집계 자동 연결(TASK-007B 상태 matrix 확정 전 임의 집계 금지)
- 구매·자재 Excel의 supply 열 추가, 증빙 첨부, 상세 IQC·사진·PDF(`TASK-009A`), 키팅(`TASK-010A`)
- 일반 구매품의 기존 업무 정책 변경(pair 입력 경로·마감 정책 포함)
- Persistent UAT write·migration 적용·runtime handover, 대표 repo·GitHub `main`·push·PR·merge

## 15. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·synthetic 데이터만 사용한다.
- migration: `0031` additive 1건. 실 DB 적용은 별도 사용자 승인.
- 외부 발송/실제 데이터 영향: 없음. 신규 인앱 원본도 만들지 않는다.
- runtime 교체: 없음. experiment branch 내 isolated 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용, 대표 repo 반영(push·PR·merge — main merge 승인 `0/3`), Persistent UAT handover.

## 16. 검증 계획

- 최소 테스트: Backend Release build. targeted tests — `0031` catalog + fresh/existing isolated apply와 위반 행 0, conditional CHECK, omitted-preserve(일반 품목 pair 소실 0), 신규 item 기본값·사급 pair 필수, 유형 변경 gate(유효 도착 건 존재 시 양방향 거부), floor·단위 고정, 사유 필수·audit old/new, 사급 부족 마감 차단→예정량 정정→마감 성공, 제공 지연 판정(전량 도착+IQC 대기 = 비지연), 필터 validation·동일 집합 summary, supply update↔도착 등록 row-lock 경쟁.
- 영향 영역 회귀: 일반 구매품 저장·마감·완료 derived 회귀 0, Excel preview/apply 보존, `receipt_completed` writer 차단, 권한 allow/deny matrix(구매/자재/품질/프로젝트 read/Read-only/System Administrator), 기존 구매 목록·dashboard·history, 자재·IQC·Pending filtered tests.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(사급 지정→도착→IQC→확정→부족 마감 차단→정정→마감 신규 spec + 기존 구매·자재·IQC·Pending spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 구매 편집(사급 지정·사유·차단), 프로젝트 구매 조회(label), 자재 목록(badge·수량 projection·지연·필터·마감 차단), IQC 카드를 페이지별 synthetic screenshot으로 확인한다. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록한다.

## 17. 권장 구현 순서

1. `0031` schema·constraint와 migration tests
2. 구매 snapshot/contracts/read projection·direct writer·사유·audit·conditional validation
3. Materials projection·필터·유형별 문구·사급 마감 gate와 교차 transaction tests
4. Frontend type·구매 edit/read·자재·IQC adaptive UI
5. Backend targeted/전체, Frontend lint/typecheck/unit/build, isolated Full-Stack E2E
6. desktop·390px screenshot, implementation report·user validation checklist·Roadmap experiment 상태 갱신·local commit

## 18. 완료 기준과 중단 조건

- 기능/권한/데이터: 시나리오 A~D가 서버 authoritative로 동작하고, 6~11장 invariant 위반 시도(부족 마감, gate 우회, pair 소실, 사유 누락, 권한 우회, 잘못된 필터, direct write)가 모두 차단·테스트된다. 일반 구매품 회귀 0.
- UX: 13장 확인 항목과 390px overflow 0.
- 자동 테스트: 16장 전체 PASS, 미실행 항목은 이유와 함께 기록.
- 산출물: implementation report에 5종 산출물 상태·위치 추적, Fable 사용량 측정 기록.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff하고 완료로 표기하지 않는다.
- PR 상태: N/A — 실험 branch local commit까지가 범위다.

중단 조건: 기존 migration·008A 원장·Pending·workflow 계약과 additive로 해소할 수 없는 충돌, 문서와 구현의 의미 있는 충돌, conditional CHECK가 기존 행을 위반시키는 경우, omitted-preserve로 닫을 수 없는 writer 경로 발견 — 임의 선택하지 않고 보고 후 중단한다.

## 19. 미결정·deferred 사항

Blocking 결정은 없다. 아래는 명시적으로 deferred된 비차단 항목이다.

| 번호 | 항목 | 결정 시점 |
| ---: | --- | --- |
| 1 | 구매 Excel의 공급 유형·제공 예정량 열 추가 여부 | 후속 Task 결정 대기 |
| 2 | 사급 제공 지연의 외부 채널(Teams/Mail·에스컬레이션) 발송 여부 | 별도 NEW_FEATURE/POLICY 계약 |
| 3 | 고객 제공 약속 version·contact·납품 차수 계획, 고객 포털·증빙 첨부 | Roadmap 별도 Task |
| 4 | 사급 부족의 병목 집계 연결 | `TASK-007B` 상태 matrix 확정 후 |
| 5 | `0031` 실 DB 적용·운영 handover 시점 | 별도 사용자 승인 |

## 20. Roadmap 연결

- 선행 Task: TASK-008A — 이 실험 branch에 구현·자동 검증 완료(사용자 검수 대기, canonical 미반영).
- 후속 Task: `TASK-009A`(상세 IQC), `TASK-010A`(키팅), `TASK-007B`(병목 집계). canonical 실행 큐·`TASK-007A` Next Gate는 변경하지 않는다.
- Go/No-Go: `tasks/008b-review.md`는 이 2차 기획을 authoritative implementation contract로 사용하는 조건으로 실험 branch 구현 `GO`를 판정했다. 이는 대표 repo, push, PR, merge, `main`, Persistent UAT, 실제 provider 승인이 아니다.

## 21. Codex 구현 지시문

`tasks/008b-change-001.md`의 승인 경계 안에서 새 Codex 구현 세션이 사용할 계약 요약이다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, branch 기준선을 보고한다.
2. `0031` additive migration 1건으로 6.4장의 컬럼·enum CHECK·conditional measurement CHECK를 구현한다. 기존 migration은 변경하지 않는다.
3. Backend: 9~12장의 omitted-preserve·신규 기본값·pair 필수·유형 변경 gate·floor·단위 고정·사유 필수·old/new audit·Excel 보존·수량 projection·허용 필터·동일 집합 summary·사급 마감 gate·제공 지연 계산·유형별 문구를 구현한다. Materials/IQC/구매 policy는 변경하지 않고 프로젝트 구매 read projection에 supply 정보만 additive로 노출한다.
4. Frontend: 13장의 구매 편집·구매 조회·자재·IQC 화면을 desktop·390px에서 구현하고 기존 LoadState·Action Feedback·카드·sheet 패턴을 재사용한다. supply invariant·지연 판정을 client에서 재정의하지 않는다.
5. 검증: 16장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 19장 deferred 항목을 임의 결정하지 않고 14장 제외 범위를 추가하지 않는다. 완료 후 페이지별 screenshot·5종 산출물·Roadmap experiment 상태 갱신과 local experiment commit까지 수행한다.

---

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-008B`
- authoringModel: `FABLE_5`
- 이 문서는 게시·main merge·Persistent UAT 승인을 부여하지 않는다. 승인 값은 `tasks/008b-change-001.md`를 따른다.

openBlockingDecisionCount: 0
