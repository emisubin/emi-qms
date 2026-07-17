# TASK-008B — 사급 자재 추적 1차 기획 Codex 내용 Review

> Review 대상: `tasks/008b-planning.md` Fable 5 원문
> Review 성격: 사용자 문제·Roadmap·TASK-008A 실제 구현·권한·데이터 무결성·모바일 UX 1회 검토
> 결과: 큰 방향은 유지하되 2차 Fable 기획에서 아래 resolution을 구현 계약으로 고정

## 1. 총평

별도 사급 원장이나 고객 포털을 만들지 않고 구매품목에 공급 유형만 추가한 뒤 `order_quantity`·`order_unit`, `material_receipts`, IQC·Pending·입고확정 원장을 재사용하는 권장안 A는 적절하다. TASK-008B의 목적과 008A 재사용 원칙을 가장 작은 migration·화면 변화로 달성하고 일반 구매품 호환성도 보존한다. 신규 외부 알림을 만들지 않고 기존 IQC·입고확정 내 업무를 재사용하는 판단도 유지한다.

다만 1차 기획의 잔량·지연·마감 정의를 그대로 구현하면 고객이 제공해야 할 양을 모두 주지 않았는데도 `receipt_completed=true`가 될 수 있고, IQC 대기만 남은 품목을 고객 제공 지연으로 잘못 표시할 수 있다. 또한 현재 `/api/materials/receipts` 조회 자체가 `MaterialReceiptUpdate` policy에 묶여 있어 기획 표의 생산관리·Read-only 조회 범위와 실제 권한이 다르다. Request의 nullable 필드도 “미전송/보존”과 “null로 지움”을 구분하지 않으면 기존 일반 구매품의 008A 수량을 구매 일괄 저장이 지울 위험이 있다. 이 항목들은 2차 기획에서 명시적으로 닫아야 한다.

## 2. 기능 판단

### 유지

- `supply_type = Purchased | CustomerSupplied` 한 컬럼과 기존 수량 pair 재사용
- 기존 행·Excel 신규 행은 `Purchased`로 호환하고 `0030`을 수정하지 않는 additive `0031`
- 공급 유형 변경은 유효 도착 건 0건일 때만 허용
- 사급품의 예정 수량·단위 pair 필수, row lock 뒤 누적 도착 floor·단위 고정·초과 차단
- 기존 도착·IQC·부적합 Pending·재검사·입고확정·`receipt_completed` 상태 machine 전면 재사용
- 신규 알림·외부 delivery 없이 기존 IQC/입고확정 work item과 deep link 재사용
- 구매 편집의 공급 유형 입력, 자재·IQC의 텍스트 badge, 공급 유형 필터, desktop·390px 전용 구성
- Excel, 고객 포털·ERP·SCM, 증빙 첨부, 상세 IQC, 키팅, Persistent UAT를 별도 범위로 유지

### 추가

1. **사급 수량을 물리 도착과 입고 확정으로 분리 표시**
   - `제공 예정량`, `누적 도착량`(non-Cancelled), `입고 확정량`(Confirmed), `미도착 잔량 = 예정량 - 누적 도착량`, `검사·확정 대기량 = 누적 도착량 - 확정량`을 각각 derived로 정의한다.
   - DB에 중복 aggregate를 저장하지 않고 `material_receipts`에서 계산한다. 이 구분이 있어야 “고객은 제공했지만 IQC 중”과 “고객이 아직 제공하지 않음”을 혼동하지 않는다.

2. **사급 전용 마감 gate**
   - 일반 구매품의 기존 수동 마감 정책은 변경하지 않는다.
   - 사급품은 `누적 도착량 = 제공 예정량`이고 기존 008A 조건(유효 건 1개 이상, 모든 건 Confirmed/Cancelled)을 모두 만족할 때만 마감한다.
   - 고객과 합의해 총량이 줄었다면 구매 담당이 사유와 함께 제공 예정량을 실제 도착량 이상으로 정정한 뒤 마감한다. 잔량이 남은 채 마감·derived 완료되는 경로는 차단한다.

3. **지연 기준 교정**
   - `제공 지연`은 `CustomerSupplied && expectedReceiptDate < 기준일 && 미도착 잔량 > 0`으로 계산한다.
   - `receipt_completed=false`를 지연 기준으로 사용하지 않는다. 전량 도착 후 IQC/확정 대기는 고객 제공 지연이 아니라 내부 처리 대기다.
   - 상태는 저장하지 않고 API가 일관된 기준일로 derived 값을 내려준다. Frontend local clock만으로 별도 계산하지 않는다.

4. **DB 조건부 불변조건**
   - `0031`은 supply type enum CHECK만 추가하지 않고 `CustomerSupplied => order_quantity > 0 && trimmed order_unit 1~20` 조건을 DB CHECK로 고정한다.
   - 기존 pair CHECK와 함께 Purchased 기존 행·legacy null pair는 허용한다. 기존 행 backfill은 `Purchased`이고 schema 적용 직후 위반 행 0을 검증한다.

5. **nullable request의 보존 의미와 writer inventory**
   - 기존 item에서 supply/measurement 필드가 요청에 없으면 현재 값을 보존한다. 일반 구매품을 일괄 저장할 때 Materials가 기록한 `order_quantity`·`order_unit`을 null로 지우면 안 된다.
   - 새 직접 입력 item은 omitted supply type을 `Purchased`로 생성한다. 새 `CustomerSupplied` item은 같은 요청에서 pair가 반드시 있어야 한다.
   - Excel preview/apply는 기존 item의 supply/measurement를 보존하고 신규 Excel item을 `Purchased`로 만든다. Excel이 사급 값을 바꾸는 묵시 경로는 만들지 않는다.
   - Purchased↔CustomerSupplied 전환 시 기존 measurement pair는 “품목의 총 예정량”이라는 공통 의미로 보존하되 CustomerSupplied 전환 시 pair 존재를 강제한다.

6. **변경 감사 사유**
   - 기존 item의 공급 유형, 제공 예정 수량 또는 단위가 바뀌면 3~500자 수정 사유를 필수로 한다.
   - 같은 구매 저장 transaction에서 `SupplyType`, `OrderQuantity`, `OrderUnit` old/new를 기존 `project_audit_events`에 기록한다. 신규 item의 최초 값도 기존 신규 행 감사 방식으로 기록한다.

7. **실제 조회 권한에 맞춘 화면 배치**
   - `/api/materials/receipts`와 `/quality/iqc`의 기존 policy를 이번 Task에서 넓히지 않는다. 자재·품질 상세 원장은 각각 기존 권한 사용자만 본다.
   - 구매품목 GET 응답에는 supply type과 예정량을 additive로 포함해 기존 ProjectRead 범위의 생산관리·Read-only가 프로젝트 구매정보에서 공급 책임을 확인할 수 있게 한다.
   - 누적 도착·확정량까지 광범위한 ProjectRead에 노출하려면 별도 권한 판단이 필요하므로 이번 Task에서는 자재 화면의 기존 권한 범위에 둔다. System Administrator의 업무 mutation 우회도 추가하지 않는다.

8. **표시 의미와 검색**
   - 사급품의 기존 `supplier_name`을 자동 삭제하거나 고객사명으로 덮어쓰지 않는다. `사급 · 고객 제공` 책임 label을 별도로 표시하고 업체 값은 참고 정보로 유지한다.
   - supply type 필터는 `All/Purchased/CustomerSupplied`만 허용하며 잘못된 query 값은 validation error로 거부한다. 필터 적용 후 summary와 item 목록이 같은 집합을 설명해야 한다.

### 보류

- 사급 제공 지연용 신규 work item·인앱 notification·Teams/Mail 에스컬레이션. 현재 008A handoff와 화면 표시로 시작하고 실제 운영 필요가 확인되면 별도 NEW_FEATURE/POLICY 계약으로 확장한다.
- 고객사별 제공 약속 version, provider contact, 납품 차수 계획, 약속 변경 승인 workflow.
- 사급품 전용 Excel 열, 증빙 첨부, 고객 포털·ERP·SCM.
- 사급 부족을 프로젝트 병목 집계에 자동 연결하는 작업. TASK-007B의 상태 matrix 없이 이번 Task에서 임의 집계하지 않는다.

### 제거

- `receipt_completed=false`만으로 `제공 지연`을 판정하는 규칙.
- 잔량이 남은 사급품을 마감할 수 있는 경로.
- 생산관리·Read-only가 현재 권한으로 자재·IQC 상세 endpoint까지 조회할 수 있다는 전제.
- 공급 유형 변경 시 기존 `supplier_name` 또는 수량 pair를 자동 삭제하는 동작.
- Frontend에서만 supply invariant·지연 기준을 결정하는 구현.

## 3. 권장 상태·수량 projection

```text
공급 유형: Purchased ↔ CustomerSupplied
  변경 gate: 유효 도착 건 = 0

사급 수량:
  예정량       = order_quantity
  누적 도착량  = SUM(receipt.quantity WHERE status <> Cancelled)
  입고 확정량  = SUM(receipt.quantity WHERE status = Confirmed)
  미도착 잔량  = 예정량 - 누적 도착량
  처리 대기량  = 누적 도착량 - 입고 확정량

제공 지연:
  CustomerSupplied AND expected_receipt_date < 기준일 AND 미도착 잔량 > 0

사급 마감:
  누적 도착량 = 예정량
  AND 유효 도착 건 >= 1
  AND 모든 도착 건이 Confirmed 또는 Cancelled
```

도착 건의 기존 forward-only 상태와 `receipt_completed` derived 계산은 변경하지 않는다. 사급 마감 gate가 먼저 잔량 0을 보장하므로 기존 완료 projection과 의미가 일치한다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `008B-SHORT-CLOSE` | P1 | `RESOLVED_FOR_REDRAFT` | 기존 008A 마감은 예정량 미달도 허용해 사급 잔량이 남은 채 완료 가능 | 사급만 도착 합계=예정량 gate 추가, 일반 구매 정책 불변 |
| `008B-DELAY-SEMANTICS` | P1 | `RESOLVED_FOR_REDRAFT` | `receipt_completed=false`는 고객 미제공과 내부 IQC 대기를 구분하지 못함 | 지연을 미도착 잔량 기준으로 교정 |
| `008B-READ-AUTH-MISMATCH` | P1 | `RESOLVED_FOR_REDRAFT` | 실제 Materials GET은 update policy인데 기획은 생산관리·Read-only 상세 조회를 약속 | endpoint 권한 확대 제거, 구매 read projection과 기존 자재 권한 분리 |
| `008B-REQUEST-PRESERVATION` | P1 | `RESOLVED_FOR_REDRAFT` | nullable bulk field가 기존 008A measurement를 null로 지울 수 있음 | omitted=preserve, 신규 기본 Purchased, Excel 보존 계약 고정 |
| `008B-DB-CONDITIONAL-PAIR` | P2 | `RESOLVED_FOR_REDRAFT` | Backend 검증만으로 CustomerSupplied null pair row 가능 | 0031 conditional DB CHECK 추가 |
| `008B-QUANTITY-SEMANTICS` | P2 | `RESOLVED_FOR_REDRAFT` | 누적 입고가 물리 도착인지 확정 입고인지 모호 | 예정/도착/확정/미도착/처리대기 projection 분리 |
| `008B-AUDIT-REASON` | P2 | `RESOLVED_FOR_REDRAFT` | 공급 책임·예정량 변경 사유가 optional이면 감사 설명 부족 | 기존 item 변경 시 사유 필수, old/new와 같은 transaction 기록 |
| `008B-SUPPLIER-DISPLAY` | P3 | `RESOLVED_FOR_REDRAFT` | 사급 badge와 업체 값이 동시에 보이면 공급 주체를 오해 가능 | 고객 제공 책임 label과 업체 참고값의 의미를 분리 |

Review 기준 Open P0/P1/P2/P3는 `0/0/0/0`이다. 이는 2차 Fable 기획이 위 resolution을 모두 반영한다는 조건부 판정이며 구현 완료 판정은 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 사급 총량 model | 기존 `order_quantity`/`order_unit` 재사용 | 008A 수량·단위·초과 검증의 단일 진실 유지 |
| 부족 마감 | 차단 후 예정량 정정 | 잔량과 완료의 모순 방지, 변경 이력 보존 |
| 제공 지연 | 미도착 잔량 기준 | 고객 책임 지연과 내부 IQC 대기 분리 |
| 변경 사유 | 기존 item supply 필드 변경 시 필수 | 공급 책임·약속 수량 변경의 감사 가능성 |
| 인앱 handoff | 기존 IQC·입고확정 업무만 재사용 | 신규 알림 taxonomy·운영 부담 회피 |
| 조회 권한 | 기존 Materials/IQC policy 유지 | 별도 권한 확대 없이 Task 범위 준수 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. `0031` additive migration 하나에 supply enum CHECK와 CustomerSupplied conditional measurement CHECK를 추가한다. `0030`과 기존 migration은 수정하지 않는다.
2. 구매 direct PATCH는 supply field의 omitted-preserve, 신규 Purchased default, CustomerSupplied pair, 변경 gate·floor·단위 고정·사유·audit를 같은 row-lock transaction에서 처리한다. Excel은 supply field를 변경하지 않는다.
3. Materials 응답은 supply type과 예정/도착/확정/미도착/처리대기 derived projection을 제공하고, 허용된 supply filter와 동일 집합 summary를 반환한다.
4. 사급 `CloseArrivals`는 잔량 0을 추가로 강제하고 `제공 지연`은 미도착 잔량으로 계산한다. 일반 구매품의 008A 상태 machine·마감·완료 계약은 변경하지 않는다.
5. Materials/IQC 권한은 확대하지 않고 구매 read projection에 supply 정보만 additive로 노출한다. 신규 permission·업무·notification·provider는 만들지 않는다.
6. 구매 편집, 프로젝트 구매 조회, 자재 입고, IQC 화면을 desktop·390px에 맞춰 구현한다. 모바일은 표 축소가 아니라 카드·sheet와 수량 우선 구조를 사용한다.
7. migration fresh/existing, 일반/사급 pair·마감·지연·권한, direct/Excel writer, supply update↔arrival 경쟁, 기존 구매·자재·IQC·Pending 회귀와 desktop/mobile을 검증한다.

## 7. 권장 구현 순서

1. `0031` schema·constraint와 migration tests
2. Procurement snapshot/contracts/read·direct writer·audit·conditional validation
3. Materials projection/filter/사급 마감 gate와 교차 transaction tests
4. Frontend type·구매 edit/read·자재·IQC adaptive UI
5. Backend targeted/전체, Frontend unit/build, isolated Full-Stack E2E
6. desktop·390px screenshot, implementation report·user checklist·local commit

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 authoritative implementation contract로 사용하면 이 실험 branch의 구현은 `GO`다. 이 판정은 대표 repo, GitHub `main`, push, PR, merge, Persistent UAT 또는 실제 provider 승인이 아니다. main merge 승인은 계속 `0/3`이다.
