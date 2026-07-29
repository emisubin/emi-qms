# TASK-008B — 사급 자재 제공·입고·잔량 추적 기획안 (Fable 1차 기획)

> 상태: Draft
> 작성 단계: Codex 내용 review 전 (experiment two-pass 1차 기획)
> 목적: TASK-008A 자재 원장 위에 사급(고객 제공) 자재의 구분·제공 예정량·잔량 추적을 additive로 얹는 계약 확정

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/008b-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackMode: `EXPERIMENT_TWO_PASS`
- sourceTask: `TASK-008B`
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 구매품목이 일반 구매와 고객 사급을 구분하지 못해 공급 책임, 고객 제공 예정량 대비 누적 입고·잔량, 제공 지연을 화면·감사 이력에서 설명할 수 없고 품목명·비고 자유 기입으로 우회하고 있다.
- 대상 사용자·역할: 구매 담당(사급 구분·제공 예정 기준 입력), 자재 담당(기존 도착·IQC 요청·입고 확정·잔량 확인), 품질 IQC 담당(기존 최소 판정), 생산관리·Read-only(조회).
- 정상 흐름: 구매품목을 사급으로 지정하고 제공 예정 기준 입력 → 기존 008A 도착 등록 → 누적·잔량 확인 → IQC 요청·판정 → 입고 확정 → 도착 마감.
- 예외·복구 흐름: 수량·단위 pair, 0 이하·초과 수량, 상태에 맞지 않는 구분 변경, 권한 없는 mutation의 서버 차단. `Arrived` 사유 필수 취소와 forward-only 전이 유지. 부분 실패는 기존 transaction 경계 안에서 처리, migration은 additive forward-fix.
- 확정한 정책과 명시적 제외: 서버 권한 authoritative, `receipt_completed` 단일 진실, forward-only, Pending 차단, 기존 일반 구매품 동작·audit·migration 불변. 제외 — 008A 모델 재구현, 고객 포털·ERP·SCM·외부 공급망, 상세 IQC·사진·PDF·키팅·Excel 확장, 실제 provider·Persistent UAT·대표 repo·`main`.
- planning으로 넘긴 비차단 미결정 사항: 사급 계약의 구체 형태(공급 유형·예정량·잔량·변경 가능 시점·지연 표시)와 인앱 업무·알림 추가 여부 — 이 문서에서 Repository 근거와 함께 권장안을 확정 대상으로 명시한다(fast-track standing instruction에 따른 권장안 자동 채택).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

구매·자재 담당자가 구매품목을 사급(고객 제공)으로 명확히 분류하고, 고객 제공 예정량 대비 누적 입고·잔량과 제공 지연을 기존 자재 도착·IQC·입고 확정 흐름 안에서 일반 구매품과 혼동 없이 추적할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 구매품목(`project_procurement_items`)은 발주품목·업체·기술 담당·발주일·입고예정일·이슈만 가지며 공급 주체 구분이 없다. TASK-008A가 도착 건(`material_receipts`)·IQC attempt·event 원장과 발주 수량·단위 pair, 도착 마감, derived `receipt_completed`를 이미 구현했다.
- 사급품도 같은 화면에서 처리되므로 “누가 공급해야 하는가(우리 발주 vs 고객 제공)”, “고객이 주기로 한 양 대비 얼마나 왔고 얼마가 남았는가”를 시스템이 답하지 못한다.
- 우회 방식은 품목명·업체명·비고에 자유 형식으로 “사급” 표기를 섞는 것이며 검색·집계·감사가 불가능하다.
- 방치하면 고객 제공 지연을 일반 구매 지연과 구분하지 못해 생산 준비 판단과 부서 인수인계, 감사 이력이 계속 모호해진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 구매 담당 (`ProcurementPlanUpdate` policy) | 구매 편집에서 공급 유형(일반/사급) 지정, 사급 제공 예정 수량·단위 입력·정정 | 접근 가능한 프로젝트 구매품목 | 기존 구매 bulk PATCH 범위 안의 신규 사급 필드 |
| 자재 담당 (`MaterialReceiptUpdate` policy) | 사급품 도착 등록·`Arrived` 취소·IQC 요청·입고 확정·도착 마감, 잔량 확인 | 접근 가능한 프로젝트 자재 | 기존 008A 자재 mutation 전체(변경 없음) |
| 품질 IQC 담당 (`QualityInspect` policy) | 사급품 도착 건의 기존 최소 적합/부적합 판정 | 기존 IQC queue | 기존 판정만(변경 없음) |
| 생산관리·Read-only·System Administrator | 사급 badge·예정량·누적·잔량·지연 조회 | 기존 역할별 범위 | 업무 mutation 없음, 우회 금지 |

신규 permission·policy는 추가하지 않는다. 권한은 UI 숨김이 아니라 기존 서버 policy와 store 검증으로 강제한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 사급 지정과 제공 예정 기준 입력

1. 구매 담당이 기존 구매 편집 화면(`/projects/{id}/procurement/edit`)에서 품목의 공급 유형을 `사급(고객 제공)`으로 선택하고 제공 예정 수량·단위를 함께 입력해 저장한다.
2. 시스템이 pair invariant(양수 수량 + 1~20자 단위)와 row version을 검증하고, 공급 유형·수량 변경을 기존 구매 field 변경 이력(`project_audit_events` 기반 history)에 남긴다.
3. 저장 후 자재 입고 화면(`/materials/receipts`)의 해당 품목 카드에 `사급` badge와 `공급 책임: 고객 제공`, 제공 예정량이 표시된다.

### 시나리오 B — 사급 분할 입고와 잔량 추적 (008A 흐름 재사용)

1. 자재 담당이 사급 품목에 도착 등록을 한다. 단위는 품목 단위를 상속하고, 유효 도착 수량 합이 제공 예정량을 초과하면 서버가 한글 validation 오류로 차단한다(기존 초과 검증 재사용, 오류 문구만 공급 유형에 맞게 표기).
2. 카드에 제공 예정량, 누적 도착, 잔량(= 제공 예정량 − 유효 도착 수량 합)이 표시된다.
3. IQC 요청 → 품질 판정 → 입고 확정 → 도착 마감은 기존 008A 계약 그대로다. 부적합 시 Pending 차단·재검사 gate도 변경 없다.
4. `receipt_completed` derived 계산은 변경하지 않는다.

### 시나리오 C — 구분 변경 시점 제한과 제공 지연 확인

1. 유효(미취소) 도착 건이 하나도 없는 품목은 공급 유형을 양방향으로 변경할 수 있다(변경 이력 기록).
2. 유효 도착 건이 1건이라도 생기면 공급 유형 변경을 서버가 validation 오류로 거부한다 — 원장의 공급 책임 의미를 소급 변경하지 않는다(forward-only 해석 보존).
3. 사급 품목의 제공 예정 수량은 도착 후에도 정정할 수 있으나, 유효 도착 수량 합 미만으로 줄이는 것과 단위 변경(유효 도착 건 존재 시)은 차단한다.
4. 사급 품목이 입고예정일(고객 제공 예정일로 해석)을 지나 미완료면 목록에서 `제공 지연` 표시를 derived로 보여준다. 새 상태값·알림은 만들지 않는다.
5. 동시 편집은 기존 row version 409 계약을 따르고 최신 상태 재조회를 안내한다.

## 5. 기능 요구사항

### 필수

- [ ] `project_procurement_items.supply_type` additive 컬럼: `'Purchased'`(기본) / `'CustomerSupplied'`, CHECK constraint, 기존 행은 기본값으로 호환
- [ ] 구매 bulk PATCH 확장: `supplyType`과 사급 품목의 제공 예정 수량·단위(기존 `order_quantity`/`order_unit` 컬럼 재사용) 저장·정정, pair·floor·단위 고정·변경 시점 invariant의 서버 검증, 기존 field 변경 이력 기록
- [ ] 공급 유형 변경 gate: 유효 도착 건 존재 시 변경 거부(양방향), row lock 후 판정
- [ ] 사급 지정 시 제공 예정 수량·단위 pair 필수(권장안 채택 — 잔량 추적이 목적이므로)
- [ ] `/api/materials/receipts` GET·IQC queue 응답에 `supplyType` additive 노출, 목록 supplyType 필터
- [ ] `/materials/receipts` UI: 사급 badge, 공급 책임 표시, 제공 예정량·누적 도착·잔량, `제공 지연` derived 표시, 공급 유형 필터, 초과·마감 문구의 공급 유형별 표기(발주 수량 ↔ 제공 예정량)
- [ ] 구매 편집 UI: 공급 유형 선택과 사급 기준 입력, 변경 불가 상태의 이유 표시(서버 차단이 최종 기준)
- [ ] `/quality/iqc` 카드에 사급 badge(조회 표시만)
- [ ] additive migration 1건(`0031`, 현재 최신 `0030` 이후), fresh/existing isolated 검증
- [ ] Backend·Frontend·isolated Full-Stack E2E와 desktop·390px screenshot

### 선택

- [ ] 자재 목록 요약 rail에 사급 관련 count 추가 — 기본은 필터·badge로 충분하므로 구현 중 UX 판단에 위임(신규 API 없이 기존 응답으로 계산 가능한 범위만)

### 명시적 제외

- [ ] TASK-008A 데이터 모델·상태 machine·`0030` migration 재구현·수정
- [ ] 고객 포털, ERP·SCM, 외부 공급망 API, 고객 직접 입력
- [ ] 상세 IQC 체크리스트·사진·PDF(`TASK-009A`), 키팅(`TASK-010A`)
- [ ] 구매·자재 Excel import/export의 공급 유형·수량 열 추가(기존 Excel 계약 불변 — 단, Excel이 supply 필드를 바꿀 경로 자체가 없음을 회귀로 확인)
- [ ] 일반 구매품의 기존 업무 정책 변경(일반 품목의 발주 수량·단위 입력 경로는 기존 “첫 도착 등록 시 입력” 유지)
- [ ] 신규 알림 유형·외부 delivery·에스컬레이션, 실제 provider 발송
- [ ] Persistent UAT write·migration 적용·runtime handover, 대표 repo·GitHub `main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 구매 편집 (기존 `/projects/{id}/procurement/edit`) | 프로젝트 구매정보 | 품목별 공급 유형 select, 사급이면 제공 예정 수량·단위 입력, 변경 불가 시 이유 | 공급 유형 지정·해제, 예정 수량 정정, 기존 일괄 저장 | 기존 bulk 저장 feedback 재사용, field 단위 한글 오류·첫 오류 focus, 409 재조회 안내 |
| 자재 입고 현황 (기존 `/materials/receipts`) | 자재 메뉴 | 품목 카드에 `사급` badge·`고객 제공` 책임 표시, 제공 예정량/누적 도착/잔량, `제공 지연` 표시, 공급 유형 필터 | 기존 도착 등록·취소·IQC 요청·확정·마감(변경 없음) | 기존 action 인접 feedback 재사용, 초과 오류 문구만 유형별 표기 |
| IQC 대기 (기존 `/quality/iqc`) | 품질 메뉴 | 카드에 사급 badge 추가 | 기존 판정(변경 없음) | 기존 계약 유지 |

확인할 UX 항목:

- 목록에서 일반/사급이 즉시 구분되고 잔량·지연이 카드 안에서 읽히는가?
- 사급 지정·수량 정정의 저장 결과와 차단 이유가 action 근처에 보이는가?
- 구분 변경 불가(도착 건 존재)·권한 부족·409가 서로 구분되어 안내되는가?
- 390px·Teams narrow에서 badge·핵심 수량·다음 행동 우선의 카드·bottom sheet를 유지하고 page-level horizontal overflow 0인가? (기존 `MaterialsWorkspace`의 모바일 카드·sheet·요약 rail 패턴 재사용)

## 7. 업무 규칙과 불변조건

- 공급 유형은 구매품목의 속성이고 도착 건 상태 machine(`Arrived → IqcRequested → Passed/FailedBlocked → Confirmed`, `Arrived`에서만 취소)은 변경하지 않는다.
- `receipt_completed`는 계속 신규 Materials 흐름만 갱신하는 derived 단일 진실이며, DB trigger·PATCH 차단·Excel 차단 계약을 그대로 보존한다.
- 유효 도착 수량 합 ≤ (발주/제공 예정) 수량, 수량·단위 pair, 단위 상속, row lock 후 합산 재검증의 기존 invariant를 사급에도 동일하게 적용한다.
- 공급 유형 변경은 유효 도착 건 0건일 때만, 제공 예정 수량 하한은 유효 도착 수량 합, 단위는 유효 도착 건 존재 시 고정 — 모두 서버·transaction 안에서 강제한다.
- 사급 기준 변경(유형·수량·단위)은 기존 구매 변경 이력에 old/new·행위자·시각·사유로 남고 hard delete하지 않는다.
- 기존 일반 구매품의 저장·Excel·dashboard 동작과 응답 필드는 회귀 없이 유지한다(additive 필드만 추가).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 공급 유형 `supply_type` | `Purchased`(기본)/`CustomerSupplied`, CHECK constraint | 기존 테이블 additive 컬럼 | 기존 구매 field 변경 이력으로 old/new 추적 |
| 제공 예정 수량·단위 | 사급 품목의 고객 제공 예정 기준 — 기존 `order_quantity`/`order_unit` pair 재사용(라벨만 유형별 표기) | 기존 컬럼 의미 확장 | 기존 pair CHECK 유지 + 구매 이력 추적 |
| 잔량·누적 | 유효(미취소) 도착 건 수량 합과 예정량의 차 — derived projection, 저장하지 않음 | 파생 값 | 원장(`material_receipts`)에서 재계산 가능 |
| 제공 지연 | 사급 ∧ 입고예정일 경과 ∧ 미완료 — derived 표시 | 파생 값 | 상태 저장 없음 |
| 도착·IQC·event 원장 | `material_receipts`·`material_iqc_attempts`·`material_receipt_events` | 기존 재사용(변경 없음) | 기존 append-only 계약 유지 |

```text
품목 공급 유형: Purchased ↔ CustomerSupplied (유효 도착 건 0건일 때만, 이력 기록)
도착 건: (기존 008A 그대로) Arrived → IqcRequested → Passed/FailedBlocked → Confirmed, Arrived에서만 Cancelled
```

Migration: `0031` additive 1건 — `supply_type` 컬럼·CHECK 추가. `0030`과 기존 migration은 수정하지 않는다. backfill은 컬럼 기본값(`Purchased`)으로 충분하며 데이터 이동이 없다. rollback은 destructive down이 아니라 forward-fix 원칙으로 문서화한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장 invariant 전부(유형 변경 gate, pair 필수, floor, 단위 고정, 초과 차단, 권한).
- 필요한 조회와 mutation:
  - 기존 구매 bulk PATCH(`/api/projects/{projectId}/procurement`, `ProcurementPlanUpdate`)의 item update 계약에 `SupplyType`·`OrderQuantity`·`OrderUnit`(사급 품목 한정 편집)을 additive로 추가. 신규 endpoint는 만들지 않는다.
  - `/api/materials/receipts` GET 목록·`/api/quality/iqc` queue 응답에 `SupplyType` additive 필드, 목록에 `supplyType` 필터 파라미터.
  - 자재 mutation endpoint는 변경하지 않는다(초과 검증 오류 문구의 유형별 표기는 store 내부 처리).
- 권한·validation: 신규 policy 없음. 사급 필드 편집은 `ProcurementPlanUpdate`, 자재·품질은 기존 policy 유지. 오류는 안정적 status와 한글 메시지.
- transaction·동시성·idempotency: 유형·수량 변경은 기존 구매 저장 transaction과 row version을 재사용하되, 유형 변경 gate와 floor 검증은 품목 row lock 후 `material_receipts` 유효 건을 재조회해 판정한다(check-then-write 경쟁 차단, 동시성 테스트 포함). 도착 등록의 기존 row lock 합산 검증과 교차 경쟁(사급 수량 축소 vs 신규 도착)이 invariant를 깨지 않음을 테스트한다.
- audit trail: 기존 구매 field 변경 수집(`Change` 목록 → `project_audit_events`/history)에 `SupplyType`·수량·단위 항목을 추가한다. `material_receipt_events`에 신규 event type은 추가하지 않는다.
- 외부 provider 영향: 없음. 신규 업무·알림·delivery를 만들지 않는다(인터뷰 결정 2의 권장안 채택 — 기존 IQC·입고 확정 내 업무가 사급 도착 건에도 동일하게 생성되므로 추가 handoff 불필요).

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서가 최종 확정하지 않으며, 구현 시 기존 convention(`ProcurementStore`·`MaterialsStore` 소유 경계)에 맞춘다.

## 10. Frontend 고려사항

- route/component: 신규 route 없음. `MaterialsWorkspace.tsx`(자재·IQC), 구매 편집 화면, `materials.ts`·`projects.ts`·`api.ts` type 확장.
- loading/empty/error/success: 기존 `LoadState`·`StateMessage`·공통 Action Feedback 패턴 재사용. 409는 최신 상태 재조회 안내.
- 공통 Action Feedback: 중복 submit 차단, 첫 오류 focus, `aria-live` 기존 계약 유지.
- 접근성: badge는 색상만이 아니라 텍스트 라벨(`사급`, `제공 지연`)로 전달, 기존 keyboard·label 계약 회귀 검증.
- 390px/mobile/narrow pane: 표 축소가 아니라 기존 모바일 카드·bottom sheet에 badge·잔량·지연을 우선 배치, page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 기존 IQC·입고 확정 내 업무와 deep link를 그대로 재사용한다. 신규 업무·알림 유형 없음.
- 권한/관리자: 기존 3개 policy 재사용, 관리자 기준정보 변경 없음.
- Excel/PDF/첨부: 계약 불변. Excel preview/apply가 supply 필드·완료값을 변경할 수 없음을 회귀로 확인한다.
- Teams/Mail: 영향 없음(발송 없음).
- 삭제·복구/감사: 구매 이력·자재 event 원장의 기존 append-only 계약 유지.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | `supply_type` 컬럼 + 기존 `order_quantity`/`order_unit` pair를 제공 예정량으로 재사용, 잔량은 derived | 008A의 초과 검증·pair CHECK·단위 상속·마감 계약을 그대로 상속, migration·코드 최소, 단일 수량 진실 | 라벨이 유형에 따라 달라져 화면 표기 관리 필요, 발주 수량과 제공 예정량을 동시에 갖는 모델은 표현 불가 |
| B | 사급 전용 별도 컬럼 pair(`supplied_quantity`/`supplied_unit`) 추가 | 개념 분리가 명시적 | 초과·pair·단위 invariant를 이중으로 복제, 도착 검증이 두 수량 중 어느 것을 따르는지 모호, 잔량 이중 진실 위험 |
| C | 사급 전용 별도 테이블·별도 화면 | 완전한 격리 | 008A 재사용이라는 Task 목적·Roadmap 범위와 충돌, 화면·권한·업무 중복 |

권장안 A를 채택한다(사용자 fast-track 지시에 따른 권장안 자동 채택). 사급 품목은 “고객이 제공하기로 한 총량” 하나만 추적하면 잔량·초과·마감이 모두 기존 계약으로 성립하며, Roadmap TASK-008B의 “TASK-008A 데이터 모델 재사용” 취지와 일치한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·synthetic 데이터만 사용한다.
- migration 필요 여부: `0031` additive 1건. `0030` 포함 기존 migration 불변. 실 DB 적용은 별도 사용자 승인.
- 외부 발송/실제 데이터 영향: 없음. 인앱 신규 원본도 만들지 않는다.
- runtime 교체 여부: 없음. experiment branch 내 isolated 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용, 대표 repo 반영(push·PR·merge — main merge는 분리된 승인 3회 전 금지), Persistent UAT handover.

## 14. 검증 계획

- 최소 테스트: Backend Release build. targeted tests — 사급 지정 pair 필수, 유형 변경 gate(유효 도착 건 존재 시 거부), floor·단위 고정, 사급 도착 초과 차단, 기존 일반 품목 저장 회귀, 권한 allow/deny(구매/자재/품질/Read-only/System Administrator), 유형 변경 vs 도착 등록 동시 경쟁. migration catalog + fresh/existing isolated apply(기존 행 `Purchased` 기본값·완료 boolean 회귀 0).
- 영향 영역 회귀: 구매 목록·dashboard·Excel preview/apply·history, `receipt_completed` writer 차단, 자재·IQC 기존 filtered tests, Pending 연동 회귀.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(사급 지정→도착→IQC→확정→마감 신규 spec + 기존 자재·구매·Pending spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 구매 편집(사급 지정·수량 정정·차단 사유), 자재 목록(badge·잔량·지연·필터), IQC 카드를 페이지별 synthetic screenshot으로 확인한다. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록한다.

## 15. 완료 기준

- 기능/권한/데이터: 시나리오 A~C가 서버 authoritative로 동작하고 7장 invariant 위반 시도가 모두 차단·테스트된다. 기존 일반 구매품 회귀 0.
- UX: 6장 확인 항목과 390px overflow 0.
- 자동 테스트: 14장 전체 PASS, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report에서 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff.
- PR 상태: N/A — 실험 branch local commit까지가 범위.

## 16. 미결정 사항

Blocking 결정은 없다. 아래는 명시적으로 deferred된 비차단 사용자 결정 항목이다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 구매 Excel에 공급 유형·제공 예정량 열을 추가할지 | 추가 / 현행 유지 | 대기 (후속 Task) |
| 2 | 사급 제공 지연을 외부 채널(Teams/Mail 에스컬레이션)로 발송할지 | 기존 인앱·표시만 / 채널 확장 | 대기 (NOTIFY 정책) |
| 3 | 고객 포털·외부 공급망 협업과 증빙 첨부 | 별도 NEW_FEATURE | 대기 (Roadmap) |
| 4 | `0031` 실 DB 적용·운영 handover 시점 | 별도 승인 절차 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Procurement/ProcurementContracts.cs`·`ProcurementStore.cs`(supply 필드·검증·이력), `Materials/MaterialsContracts.cs`·`MaterialsStore.cs`(projection·필터·문구), Excel 경로 회귀 확인
- Frontend: 구매 편집 화면(`App.tsx` 내 procurement-edit 영역), `MaterialsWorkspace.tsx`, `materials.ts`, `projects.ts`, `api.ts`, `styles.css`
- DB/Migration: `database/migrations/0031_*.sql` 신규 1건
- Tests/Scripts: `PostgreSqlMigrationTests.cs`, `ProcurementApiTests.cs`, Frontend unit, Full-Stack E2E spec
- Docs: Roadmap TASK-008B 실험 상태 기록(canonical 큐 불변), interview·planning·review·implementation report

## 18. Roadmap 연결

- 선행 Task: TASK-008A — 이 실험 branch에 구현·자동 검증 완료(사용자 검수 대기, canonical 미반영). 실험 순서 재정렬은 interview Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록됨.
- 후속 Task: `TASK-009A`(상세 IQC), `TASK-010A`(키팅). canonical 실행 큐의 `Dependency Pending`과 다음 `TASK-007A` Gate는 변경하지 않는다.
- 현재 Go/No-Go: 이 문서는 1차 기획이다. Codex 내용 review → Fable 2차 기획(별도 승인된 target) 뒤에만 구현이 시작된다.
- 별도 Task로 분리할 항목: 16장 1~3번.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 사용자 experiment fast-track 지시(인터뷰 생략·권장안 자동 채택·local commit까지) | 비차단 정책을 12장 권장안 A와 9장 알림 최소안으로 확정 대상 기록 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

2차 기획 확정 전 참고용 초안이며 구현 승인이 아니다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, branch 기준선을 보고한다.
2. `0031` additive migration으로 `supply_type` 컬럼·CHECK를 추가한다. `0030` 포함 기존 migration은 수정하지 않는다.
3. Backend: 구매 bulk PATCH에 supply 필드와 7·9장 invariant(pair 필수, 유형 변경 gate, floor, 단위 고정, row lock 판정, 이력 기록)를 구현하고, Materials 목록·IQC queue projection과 supplyType 필터, 유형별 오류 문구를 추가한다. 자재 상태 machine·`receipt_completed` 계약은 변경하지 않는다.
4. Frontend: 구매 편집의 공급 유형·사급 기준 입력, 자재 목록의 badge·잔량·지연·필터, IQC badge를 desktop·390px에서 구현하고 기존 LoadState·Action Feedback 패턴을 재사용한다.
5. 검증: 14장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 16장 deferred 항목을 임의 결정하지 않고 5장 제외 범위를 추가하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
