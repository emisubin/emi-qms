Baseline research is complete. Here is the requested first planning draft artifact for Codex to validate (target: `tasks/ul891-set-001-planning.md`).

---

# TASK-UL891-SET-001 — UL891 세트 사양·세트 인스턴스·개별 패널·부분출하·월별 청구 기획안

> 상태: Draft
> 작성 단계: Codex 내용 review 및 Fable 2차 기획 전 — experiment 2-pass의 1차 기획
> 목적: 사용자가 확정한 UL891 세트 정책을 현재 Repository 계약에 투영해 구현 가능한 범위·대안·권장안을 고정한다

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/ul891-set-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 프로젝트는 `panel_count`로 평면 패널 P01…PN만 생성하므로, 같은 세트 사양의 복수 주문, 세트별 구성 패널, 주문 수량 변경, 월이 다른 부분출하·청구, 발주 후 취소 회수를 표현할 수 없다.
- 대상 사용자·역할: 영업(세트 줄·수량·구성 code 입력, 수량 변경, 월별 발행 요청, 회수 추적), 설계(구성 패널명·규격 완성), 생산관리·제조·품질·물류(기존 개별 패널 원자 실행 유지), 조회전용 타부서(조회만).
- 정상 흐름: 영업이 UL891 프로젝트에 세트 사양 여러 줄과 주문 수량을 저장 → 설계가 구성 패널명·규격을 완성 → 세트 인스턴스별 실제 패널이 고유 ID로 생성 → 기존 제조·LQC·OQC·FAT·QR·Packing Unit·출하 흐름 실행 → 출하 월별 발행 요청 → 회계 발행 확인 → 프로젝트 완료.
- 예외·복구 흐름: 수량 감소 시 사용자가 취소할 세트 인스턴스를 직접 선택, 진행 인스턴스는 사유·예외 확인 필수, 납품 패널 포함 인스턴스는 취소 불가, 발주일 입력 품목이 있으면 `고객 청구·회수 필요` 사례 생성 후 `청구 필요 → 발행요청 반영 → 회계 발행 확인 → 회수 확인`으로 추적.
- 확정한 정책과 명시적 제외: interview 2~8장(핵심 모델, 수량 변경, 사양 버전, 부분출하, 월별 청구, 완료 조건, Repository 제약)과 11장(세트별 단가·납기, ERP/회계 실제 연동, Packing Unit 원자성 제거, 비-UL891 세트 전환, 대표 repo·`main`·Persistent UAT·push·PR·merge 제외)을 그대로 따른다.
- planning으로 넘긴 비차단 미결정 사항: interview 9장 8건. 사용자에게 다시 묻지 않으며 이 문서가 대안·권장안을 제시하고 Codex review 뒤 2차 기획이 확정한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

영업·설계가 UL891 프로젝트를 `세트 사양(버전) → 세트 주문 인스턴스 → 개별 물리 패널` 계층으로 등록·변경하고, 기존 패널 단위 실행 원자를 그대로 유지한 채 부분출하 추적, 선택적 수량 감소·발주 회수, 출하 월별 세금계산서 발행 요청과 확장된 프로젝트 완료 gate까지 한 시스템에서 처리할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 `projects.item`은 `UL67, UL891, UL508A, IEC, LLP, RPP` enum이고, 프로젝트 생성은 `panel_count`로 `panel_placeholders`(P01…PN, `Active/Cancelled`, `(project_id, sequence_number)`·`(project_id, display_code)` unique)를 평면 생성한다.
- 수량 변경은 `ChangePanelCountAsync`가 이미 존재한다: expected active count 낙관적 검증, 증가 시 `MaxSequenceNumber` 이후 신규 번호(결번 재사용 없음), 감소 시 사용자가 취소 패널을 직접 선택하고 사유를 입력하며 제조·품질·물류 draft를 원자적으로 정리한다. 그러나 진행/납품 상태에 따른 차단·예외 확인 구분과 세트 개념이 없다.
- 청구는 `sales_billing_request_*`(TASK-BILLING-REQUEST-001)가 append-only 반월(서울 기준 1~15일/16~말일 추천) batch Excel을 제공하지만, `ux_sales_billing_request_items_project unique (project_id)`와 `departed_panel_count = active_panel_count` check 때문에 프로젝트당 평생 1회·전량 출하 후에만 가능하다. 정산은 `sales_settlements`가 `ux_sales_settlements_project unique`로 프로젝트당 1건이며, 완료 시 모든 active 패널 납품·Open Pending 0·발행요청 존재를 검증하고 프로젝트를 `Completed`로 전이한다.
- 따라서 같은 프로젝트에서 월이 다른 부분출하·복수 청구, 발주 후 취소 회수, 세트 계층 추적은 현재 구조로 불가능하고, 실무는 시스템 밖 수기로 우회한다. 이 기능이 없으면 UL891 프로젝트의 수량 변경·부분출하·월별 청구가 시스템 기록과 어긋나고 회수 누락 위험이 남는다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 | 세트 줄(사양×수량) 입력, 수량 증감, 인스턴스 취소, 월별 발행 요청 생성·금액 입력·확정, 회수 사례 추적·회수 확인 | 프로젝트·세트·패널·청구·회수 전체 | 세트 주문·청구·회수 (기존 `Project.*`, `sales.settle` 계열 재사용) |
| 설계 | 세트 사양 구성 패널명·규격 완성, 사양 버전 개정 | 프로젝트·세트·패널 | 사양 구성·버전 (기존 패널 정보 수정 권한 계열 재사용) |
| 생산관리·제조·품질·물류 | 기존 개별 패널 원자 실행(변경 없음) | 세트 맥락이 추가된 패널 상세 | 기존 범위 그대로 |
| 회계 발행 확인 처리자(영업 or 관리자) | 월별 요청의 회계 발행 확인 기록 | 청구 화면 | 발행 확인 상태 |
| 조회전용 타부서 | 조회만 | 프로젝트·세트·패널·청구 요약(매출 권한 gate 유지) | 없음 |

Backend가 권한·검증·lifecycle·동시성의 authoritative layer다. 매출 금액 노출은 기존 sales amount 권한 gate를 그대로 따른다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 세트 등록과 패널 생성

1. 영업이 UL891 프로젝트 생성/수정에서 세트 줄을 입력한다: 1번 세트 사양 × 3, 2번 세트 사양 × 5, 3번 세트 사양 × 3. 각 사양에 구성 패널 code(A~G 등, 개수 가변)와 구성 수를 입력한다.
2. 시스템이 세트 인스턴스(사양별 1..N)와 인스턴스×구성별 실제 패널을 고유 ID·고유 순번으로 생성한다. 패널 번호·식별자는 기존 규칙대로 영구 결번·재사용 없음.
3. 설계가 프로젝트 상세 → 설계 탭에서 사양 버전의 구성 패널명·규격을 완성하면 소속 패널에 반영된다.
4. 이후 제조·LQC·OQC·FAT·QR·키팅은 기존 화면에서 개별 패널 원자로 그대로 실행된다.

### 시나리오 B — 부분출하와 월별 발행 요청

1. 물류가 출하 가능한 패널 subset(예: 2번 세트 3번째 인스턴스의 A·B·C 패널)을 기존 Packing Unit에 담아 출발·납품 처리한다(기존 원자성 유지).
2. 프로젝트·세트·패널 상세에서 어떤 인스턴스의 어떤 구성 패널이 어느 Packing Unit·출하일·납품 상태인지 조회된다.
3. Asia/Seoul 달력 월이 닫히면 그 월의 출하분이 해당 프로젝트의 월별 발행 요청 후보로 합산되고, 영업이 금액을 직접 입력해 발행 요청한다. 화면은 프로젝트 판매액·누적 요청액·잔여 가능액·해당 월 출하 근거를 함께 보여주고, 누적이 판매액을 초과하면 서버가 차단한다.
4. 회계 발행 확인이 기록되면 해당 월 요청이 확정 상태가 된다.

### 시나리오 C — 수량 감소와 발주 회수

1. 영업이 수량 감소에서 취소할 세트 인스턴스를 직접 선택한다.
2. 시스템이 인스턴스 상태를 판정한다: 납품 패널 포함이면 차단, 진행 중이면 사유·예외 확인 요구, 미착수면 즉시 허용.
3. 취소 인스턴스와 연결(또는 영업이 선택)된 구매품목 중 발주일이 입력된 항목이 있으면 `고객 청구·회수 필요` 사례가 생성된다.
4. 회수 사례는 `청구 필요 → 발행요청 반영 → 회계 발행 확인 → 회수 확인`으로 추적되고, 해당 취소 월의 발행 요청(출하가 없으면 회수 전용 월 후보)에 선택 반영된다.
5. 프로젝트 완료는 모든 회수 사례가 `회수 확인`이어야 통과한다.

## 5. 기능 요구사항

### 필수

- [ ] UL891 프로젝트의 세트 사양(가변 구성, code 사양 버전 내 고유)·사양 버전·세트 인스턴스 관리와 인스턴스×구성별 실제 패널 자동 생성
- [ ] 착수 인스턴스의 사양 snapshot 유지(버전 불변), 미착수 인스턴스의 명시적 최신 버전 적용(대상 선택+사유), 납품 패널 snapshot 불변
- [ ] 수량 증가(새 인스턴스·새 패널 ID)와 선택적 수량 감소(미착수 허용/진행 사유·예외 확인/납품 포함 차단), 결번 영구 유지
- [ ] 발주일 입력 구매품목 관련 취소 시 회수 사례 자동 생성과 4단계 회수 상태 추적
- [ ] 프로젝트×출하 달력월(Asia/Seoul) 단위 복수 발행 요청, 수동 금액, 누적 ≤ 프로젝트 판매액 서버 잠금, 월 출하 근거·잔여액 표시, 회수 전용 월 후보
- [ ] 프로젝트 완료 gate 확장: active 패널 전량 납품 + 모든 월별 요청 회계 발행 확인 + 회수 사례 전량 회수 확인 + Open Pending 0
- [ ] 프로젝트 상세의 세트·패널 추적 projection과 패널 상세의 세트 맥락·부서별 데이터, desktop·390px
- [ ] UL891 세트 종속 데이터 생성 후 평면 구조 회귀·Item 변경 차단, 비-UL891·legacy 흐름 무회귀

### 선택

- [ ] 세트 단위 진행 요약 badge(인스턴스별 제조/품질/출하 집계)
- [ ] 월별 발행 요청 이력의 Excel projection(기존 선택 export registry 재사용)

### 명시적 제외

- [ ] 세트별 판매단가·세트별 납기일·구성 패널별 원가
- [ ] ERP/회계·국세청 실제 발행, 실제 Teams/Mail provider 발송
- [ ] Packing Unit 출발 원자성 변경, 비-UL891 Item 세트 전환
- [ ] 대표 repo·`main`, Persistent UAT migration/runtime handover, push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 생성/수정 (UL891) | 프로젝트 목록 → 생성/수정 | Item=UL891일 때 세트 줄 편집기(사양명·수량·구성 code/수), 비-UL891은 기존 `panel_count` 유지 | 세트 줄 추가/삭제, 구성 code 입력 | 기존 Action Feedback 계약 재사용, 검증 오류 필드별 표시 |
| 프로젝트 상세 · 영업 탭 확장 | 프로젝트 상세 | 세트 줄·인스턴스 상태 집계, 판매액·누적 요청액·잔여액, 회수 사례 요약 | 수량 증감, 인스턴스 취소, 회수 사례 열람 | 취소 차단/예외 확인 dialog, 저장 결과 인라인 |
| 프로젝트 상세 · 설계 탭 확장 | 프로젝트 상세 | 사양 버전·구성 패널명/규격, 인스턴스→패널 매핑 | 구성 완성, 버전 개정, 미착수 인스턴스 버전 적용 | 착수 인스턴스 잠금 안내 |
| 프로젝트 상세 · 물류 탭/세트 추적 | 프로젝트 상세 | 인스턴스×구성 패널의 Packing Unit·출하일·납품 상태 matrix | 조회, 패널 상세 이동 | 조회전용 시 수정 버튼 비노출 |
| 패널 상세 확장 | 설계/세트 추적 → 패널 | 세트 사양·버전·인스턴스 번호·구성 code + 기존 설계 요약, 부서별 실행 이력 | 조회, 담당 부서만 해당 데이터 수정 진입 | 기존 패턴 유지 |
| 월별 발행 요청 화면 | 정산·완료 또는 영업 메뉴 | 월별 후보(출하 근거·회수 사례), Draft/발행요청/발행확인 상태, 판매액·누적·잔여 | 금액 입력, 요청 확정, 발행 확인 기록, 회수 사례 반영 선택 | 초과 금액 서버 차단 메시지, 월 미마감 안내 |

확인할 UX 항목: 세트 계층에서도 현재 상태·다음 행동이 명확한가, 저장 결과가 action 근처에 보이는가, 조회전용·권한 부족 상태가 명확한가, 390px에서 세트 matrix가 page-level overflow 없이 접히는가(모바일은 인스턴스별 card 접기 권장), 기존 DESIGN-000 token·primitive 우선.

## 7. 업무 규칙과 불변조건

- 패널 식별자·순번은 영구 결번이며 재사용하지 않는다(기존 `MaxSequenceNumber` 증가 방식 유지).
- 제조·LQC·OQC·전진검수·FAT·QR·키팅·물류의 실행 원자는 개별 `panel_placeholder`이며 세트 aggregate로 합치지 않는다.
- Packing Unit 출발 처리 원자성과 납품 판정(finalized `DeliveryCompleted` batch + active membership + delivery result)은 기존 계약을 그대로 재사용한다.
- 납품된 패널·그 사양 snapshot은 불변. 착수 인스턴스의 사양 버전 참조는 불변(새 버전 적용은 미착수 대상 명시 선택+사유).
- 발주일 입력 품목 관련 취소는 차단하지 않되 회수 사례 생성을 누락하지 않는다(같은 transaction).
- 월별 발행 요청 누적 금액은 프로젝트 판매액을 초과할 수 없다 — 프로젝트 row lock 하의 서버 검증.
- 프로젝트 완료 후 수량·사양·청구 mutation은 차단(기존 lifecycle fence 확장). 세트 종속 데이터가 생긴 UL891 프로젝트의 Item 변경·평면 회귀 차단.
- 기존 migration은 수정하지 않고 additive만 추가한다. 비-UL891·legacy 프로젝트는 조회·수정·workflow 무회귀.
- 모든 mutation은 기존 패턴(트랜잭션, row lock, version/expected-count 낙관적 검증, operation receipt 멱등성, project_audit_events 및 append-only operation 기록)을 따른다.

## 8. 데이터와 상태 모델

아래 이름은 2차 기획 확정 대상 제안이다(기존 개체는 확인된 사실).

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `projects`, `panel_placeholders` | 프로젝트·개별 패널(P01…PN) | 기존 | 변경 없음, audit 기존 |
| 세트 사양 (제안 `ul891_set_specs`) | 프로젝트 내 세트 줄 정의 | 신규 | soft 상태·audit |
| 사양 버전 (제안 `ul891_set_spec_versions`) | 불변 버전 row + 구성 rows(code 버전 내 고유, 가변 개수, 패널명·규격) | 신규 | append-only 버전, 개정 사유 |
| 세트 인스턴스 (제안 `ul891_set_instances`) | 주문 수량 1개 = 인스턴스 1개, 사양 버전 참조, `Active/Cancelled` | 신규 | 취소 사유·예외 확인·감사, 결번 영구 |
| 패널↔세트 연결 (제안 `panel_placeholders`에 nullable 컬럼 또는 mapping table) | 패널의 인스턴스·구성 code 소속 | 신규(additive) | 비-UL891은 null 유지 |
| 회수 사례 (제안 `ul891_recovery_cases`) | 취소 인스턴스×관련 발주 품목 추적 | 신규 | 상태 전이 감사, append-only receipt |
| 월별 발행 요청 (제안 `sales_monthly_billing_requests`) | 프로젝트×달력월 unique, 수동 금액, 출하/회수 근거 projection | 신규 | 확정 후 불변, operation receipt |
| `sales_billing_request_*`, `sales_settlements` | 기존 반월 batch Excel·단일 정산 | 기존 | 수정 금지, 호환 계층은 12장 |

상태 전이:

```text
세트 인스턴스: Active → Cancelled(미착수 즉시 / 진행 사유·예외 확인 / 납품 포함 차단)
회수 사례: 청구 필요 → 발행요청 반영 → 회계 발행 확인 → 회수 확인
월별 발행 요청: Draft(월 진행 중 근거 누적) → Requested(마감 월 확정, 금액 잠금) → InvoiceConfirmed(회계 발행 확인)
프로젝트: Active → Completed(전량 납품 ∧ 전 월별 요청 InvoiceConfirmed ∧ 전 회수 사례 회수 확인 ∧ Open Pending 0)
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 인스턴스 취소 가능 판정(진행/납품 상태), 사양 버전 적용 가능 판정, 월 마감·금액 cap, 회수 사례 생성·상태 전이, 완료 gate 전체.
- 필요한 조회와 mutation: 세트 구조 조회(프로젝트 상세 projection), 세트 줄/사양 버전/인스턴스 CRUD-형 mutation, 수량 증감, 월별 후보·요청·발행 확인, 회수 사례 조회·상태 전이. 기존 `ChangePanelCountAsync`의 취소 정리 로직(제조·품질·물류 draft 취소, work item 취소, audit)은 인스턴스 취소에서 재사용한다.
- 권한·validation: 기존 sales/설계 권한 계열 재사용을 기본으로 하고, 신규 permission 추가는 최소화한다(2차 기획에서 확정).
- transaction·동시성·idempotency: 프로젝트 row lock + 대상 패널 lock, version 컬럼, operation receipt(fingerprint) 패턴을 기존 정산·물류 store와 동일하게 적용. "진행 시작" 판정은 같은 transaction 안에서 authoritative 원장(`panel_manufacturing_executions`의 `InProgress/Blocked/Completed`, 키팅 완료, 품질 검사 기록, 물류 membership)을 직접 조회한다.
- audit trail: `project_audit_events` entity_type 확장(additive check 재정의는 신규 migration에서) 또는 신규 감사 테이블. 회수·청구는 append-only operation 기록.
- 외부 provider 영향: 없음(인앱 알림·내 업무 연결은 기존 outbox 구조 재사용, 실제 발송 제외).

## 10. Frontend 고려사항

- route/component: 기존 `App.tsx` 프로젝트 상세 탭 구조(전체 흐름·영업·생산관리·설계·구매·자재·제조·품질·물류)에 additive로 확장, 월별 청구는 `SalesSettlementPage`/`SalesBillingRequestPage` 계열에 연결. 대형 신규 페이지보다 기존 탭 확장 우선.
- loading/empty/error/success: 기존 `toLoadError`·Action Feedback 계약 재사용.
- 접근성: 탭 rolelist·aria-selected 기존 패턴 유지, dialog의 사유·예외 확인 입력은 label 명시.
- 390px/mobile/narrow pane: 세트 matrix는 모바일에서 인스턴스 card 접기, page-level overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 인스턴스 취소·회수 사례·월별 요청의 내 업무/인앱 알림 연결은 기존 work_items·notifications 패턴 재사용(실제 provider 제외).
- 권한/관리자: 매출 금액 권한 gate 유지, 조회전용 타부서 read-only.
- Excel/PDF/첨부: 기존 반월 발행요청 Excel은 legacy 경로로 보존. 월별 요청 화면의 export는 선택 범위.
- 삭제·복구/감사: 프로젝트 soft-delete·purge 경로에 신규 테이블 참조 무결성 포함(기존 purge guard 패턴 준수).

## 12. 후보 구현안과 대안

### 12.1 세트 schema·snapshot (interview 9.1)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 불변 사양 버전 row + 인스턴스가 버전을 참조. snapshot = 참조 버전 자체. 패널은 인스턴스·구성 code로 연결 | 복사 없는 snapshot, 버전 개정이 append-only, 최소 additive | 버전 row 불변 강제 trigger 필요 |
| B | 인스턴스 착수 시 구성 전체를 인스턴스로 복사 | 참조 없는 완전 격리 | 데이터 중복, 정합성 관리 비용 |

### 12.2 패널 번호 체계

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 기존 프로젝트 전역 순번·display_code(P01…) 유지 + UI에 세트 label(예: 사양2-3호기-A) 병기 | QR·제조·품질·물류 downstream 무변경 | label 이해를 UI가 보조해야 함 |
| B | display_code 자체를 세트 기반으로 변경 | 사람이 읽기 쉬움 | unique 계약·기존 화면·QR 회귀 위험 큼 |

### 12.3 legacy UL891 프로젝트 (interview 9.3)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 자동 전환 없음. 기존 UL891 프로젝트는 `legacy 평면` badge로 조회·기존 흐름 유지, 세트 구조는 신규 생성 프로젝트부터 | 무회귀, migration 위험 0 | 혼재 기간 두 표현 공존 |
| B | 미착수 legacy를 1구성 세트로 자동 변환 | 표현 통일 | 데이터 전환 위험, 사용자 미확정 정책 |

### 12.4 월별 청구 lifecycle과 1일·16일 실무 (interview 6.8, 9.4)

Repository 대조 결과: 현재 반월 추천 기간(서울 기준, 16일 이후엔 당월 1~15일, 15일 이전엔 전월 16~말일)은 "닫힌 반월만 청구"하는 실무다. 월 단위 후보에서 "마감된 달만 요청"을 강제하면 16일의 당월 1~15일 청구가 다음 달 1일로 늦춰지는 충돌이 실제로 존재한다.

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 월 후보는 월 진행 중에도 `Draft`로 상시 노출(출하 근거 누적, 1일·16일 점검 실무 지원)하되, `Requested` 확정(금액 잠금)은 월 마감 후에만 허용. 회수 전용 월 후보 포함 | 사용자 기본 권장안(마감 월만 확정)과 월별 1건 불변을 지키면서 16일 점검 실무를 조회로 흡수 | 16일 "확정" 관행은 월 1회로 변경됨 — 비차단 운영 변화로 안내 필요 |
| B | 월 중 조기 확정을 예외 확인과 함께 1회 허용, 이후 같은 달 추가 출하는 차단·경고 | 16일 확정 유지 | 뒤늦은 출하 누락 위험이 그대로 남음(사용자가 막고자 한 문제) |
| C | 반월 후보 유지 + 월 단위 합산 별도 | 실무 무변화 | 월별 1건 정책 위반 |

### 12.5 구매품목↔취소 인스턴스 연결 v1 (interview 9.5)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | 회수 사례가 취소 인스턴스를 필수 참조하고, 발주일 입력 품목을 영업이 사례에 선택 연결(0..N). 직접 연결 정보가 없는 legacy 품목은 "프로젝트에 발주일 입력 품목 존재" 사실만으로 사례를 생성하고 품목 연결은 후속 입력 허용 | 구매 schema 무변경, 보수적, 누락 없음 | 품목-인스턴스 자동 판정 없음(수동) |
| B | 구매품목에 인스턴스/패널 FK를 추가하고 발주 시점부터 연결 | 자동 판정 가능 | 구매 입력 UX·Excel 계약 변경 범위가 큼 — v2 후보 |

### 12.6 진행·납품 판정과 race-safe 수량 변경 (interview 9.6)

권장: "착수" = 해당 인스턴스 소속 패널 중 하나라도 제조 실행 존재(`InProgress/Blocked/Completed`) ∨ 키팅 완료 ∨ 품질 검사 기록 ∨ 물류 packing membership. "납품" = 기존 정산 조건과 동일한 finalized `DeliveryCompleted` 판정 재사용. 판정과 취소는 프로젝트·패널 row lock 하나의 transaction에서 수행(기존 `ChangePanelCountAsync` 골격 재사용).

### 12.7 정보 구조·권한 (interview 9.2, 9.7)

권장: 영업이 세트 줄·수량·구성 code/수 입력, 설계가 구성 패널명·규격 완성이라는 역할 분리를 탭 권한으로 표현. 프로젝트 상세에서 전 데이터 조회, 패널 상세로 진입해 패널별 데이터 조회·입력. 조회전용 타부서는 모든 신규 화면 read-only.

### 12.8 기존 단일 정산·반월 Excel과의 호환 (interview 9.8)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A(권장) | UL891 세트 프로젝트는 완료 gate에서 기존 `HasBillingRequest` 검사를 월별 요청 전량 `InvoiceConfirmed` + 회수 전량 확인 검사로 대체하고, 비-UL891·legacy는 기존 반월 batch·단일 정산 경로 유지. `sales_billing_request_*`·`sales_settlements` schema는 불변 | 기존 append-only·unique 계약 무손상, rollout 경계 명확 | 두 청구 경로 공존 — 화면에서 프로젝트 유형별 안내 필요 |
| B | 전 프로젝트를 월별 요청으로 전환 | 경로 단일화 | 기존 unique(project) 계약·완료 이력과 충돌, 범위 초과 |

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증.
- migration 필요 여부: additive 신규 migration 1건(현재 다음 번호 `0053`) — 신규 테이블·nullable 연결 컬럼·필요 시 audit check 재정의. 기존 migration 수정·번호 재사용 금지, fresh·existing 모두 검증.
- 외부 발송/실제 데이터 영향: 없음(dry-run·인앱만).
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge·Persistent UAT·실제 provider(모두 승인 없음 상태 유지). local experiment commit만 승인됨.

## 14. 검증 계획

- 최소 테스트: Backend — 세트 생성/버전/인스턴스 취소 판정(미착수/진행/납품), 회수 사례 생성·상태 전이, 월별 후보 합산·금액 cap·마감 gate, 완료 gate 4조건, 멱등·동시성. Frontend — 세트 편집기·취소 dialog·청구 화면 unit.
- 영향 영역 회귀: 비-UL891 프로젝트 생성·수량 변경·정산 완료, 기존 반월 발행요청, 물류 출발·납품, PostgreSqlMigrationTests fresh+existing, 관련 Full-Stack E2E(project registration, logistics, sales settlement, billing) 갱신.
- PR/CI: 해당 없음(local experiment). Backend 전체, Frontend lint/typecheck/unit/build, isolated Full-Stack E2E, 페이지별 desktop·390px screenshot.
- 사용자 검수: `BATCHED_FINAL` 일괄 검수 대기로 추적하며 완료로 가장하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: interview 12장 성공 기준 전부 충족, Backend authoritative 검증·권한·감사·멱등성 동작.
- UX: desktop·390px 핵심 행동 page-level overflow 0.
- 자동 테스트: 위 14장 전부 통과, Open P0/P1/P2 0.
- 5종 산출물: implementation report 중심으로 상태·위치 추적(`docs/12` 기준).
- 사용자 검수 상태: `BATCHED_FINAL` 대기로 명시.
- PR 상태: N/A(local commit만, push·PR·merge 승인 없음).

중단 조건: 기존 계약과의 의미 있는 충돌(예: 기존 migration 수정이 불가피해지는 경우), 안전 blocking decision 발생, read-only·격리 경계 위반 필요 시 구현을 중단하고 보고한다.

## 16. 미결정 사항

사용자 재질문 없음. 아래는 standing instruction에 따라 Codex review 뒤 Fable 2차 기획이 Repository 근거로 확정하는 비차단 항목이다.

| 번호 | 항목 | 1차 권장안 | 확정 주체 |
| ---: | --- | --- | --- |
| 1 | 세트 schema·snapshot | 12.1-A 불변 버전 참조 | 2차 기획 |
| 2 | 입력·역할 분리 UX | 12.7 | 2차 기획 |
| 3 | legacy UL891 표시 | 12.3-A 자동 전환 없음 | 2차 기획 |
| 4 | 월별 lifecycle·1일/16일 보정 | 12.4-A Draft 상시·마감 후 확정 | 2차 기획 |
| 5 | 구매품목 연결 v1 | 12.5-A 수동 선택+보수적 legacy | 2차 기획 |
| 6 | 진행·납품 판정·race-safe | 12.6 기존 원장 재사용 | 2차 기획 |
| 7 | 상세 정보 구조·권한 | 12.7 | 2차 기획 |
| 8 | 단일 정산 호환 경계 | 12.8-A UL891 gate 대체 | 2차 기획 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Projects/`(store·contracts·endpoints·normalizer), `Sales/`(settlement gate·신규 monthly billing), 신규 UL891 세트 모듈, `Logistics/`·`Manufacturing/`·`QualityInspections/` projection 참조(원자 불변).
- Frontend: `App.tsx` 프로젝트 상세·패널 상세 확장, `SalesSettlementPage`/`SalesBillingRequestPage` 연결, `api.ts` 계약.
- DB/Migration: additive `0053`(세트 사양·버전·구성·인스턴스·패널 연결·회수 사례·월별 발행 요청·guard trigger).
- Tests/Scripts: Backend 테스트 추가·회귀, Frontend unit, Full-Stack E2E 갱신, migration 테스트.
- Docs: 2차 기획 target `docs/41-ul891-panel-set-plan.md`(runner가 기록), Roadmap·완료 원장 갱신(Codex 범위).

## 18. Roadmap 연결

- 선행 Task: TASK-005A/010A/012A/013A/014A/BILLING-REQUEST-001의 experiment 완료 scope(재구현 금지, 재사용만).
- 후속 Task: 구매품목-인스턴스 자동 연결 v2, 첨부·사진 storage Gate(원래 roadmap Next Gate), canonical 승격·UAT 통합 Task.
- 현재 Go/No-Go: 사용자 명시 지시로 `explicitRoadmapOverrideApproved: true`(interview 1장) — experiment fast-track 진행.
- 별도 Task로 분리할 항목: 실제 회계 연동, 세트별 단가, 대표 repo 승격.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| (interview 완료일) | 정책 질문 4건 답변 후 세트 계층·수량 변경·부분출하·월별 청구 정책 확정, fast-track 지시 | interview 2~8장으로 고정, 본 1차 기획 입력 |

## 20. 최종 승인 상태

- [x] 기능 목표와 업무 문제 승인 — interview `COMPLETED_CONFIRMED`
- [x] 포함·제외 범위 승인 — interview 10·11장
- [x] 시나리오와 권한·업무 규칙 승인 — interview 2~8장
- [ ] UI/UX 방향 — 2차 기획 확정 후 experiment 범위 내 자동 채택
- [x] Task 고유 안전 경계 승인 — change-001 승인·안전 경계
- [ ] 검증·사용자 체크리스트 — 구현 후 `BATCHED_FINAL` 추적
- [ ] Codex 구현 프롬프트 — 아래 초안, 2차 기획이 최종 확정

### Codex 구현 지시문 초안

1. 2차 기획(`docs/41-ul891-panel-set-plan.md`) 확정 계약만 구현한다. 1차 기획·review는 판단 이력으로 보존한다.
2. additive migration `0053` 하나로 신규 테이블·연결 컬럼·guard를 추가하고 fresh·existing 모두 검증한다. 기존 migration·`sales_billing_request_*`·`sales_settlements` schema는 수정하지 않는다.
3. 인스턴스 취소·월별 요청·완료 gate는 기존 store 패턴(row lock, version, operation receipt, audit)을 재사용해 Backend authoritative로 구현한다.
4. 비-UL891·legacy 회귀 테스트와 isolated Full-Stack E2E, desktop·390px screenshot까지 통과 후 local experiment commit만 수행한다. push·PR·merge·Persistent UAT·실제 provider는 승인 없음.

---

planningStatus: DRAFT
implementationApproved: false
userDecisionRequiredCount: 0
