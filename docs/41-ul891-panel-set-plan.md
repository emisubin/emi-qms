# TASK-UL891-SET-001 — UL891 세트 사양·세트 인스턴스·개별 패널·부분출하·월별 청구 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-UL891-SET-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/ul891-set-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/ul891-set-001-planning.md` (판단 이력으로 보존, 수정하지 않음)
- codexReviewSource: `tasks/ul891-set-001-review.md` (`RESOLVED_FOR_SECOND_PLANNING`, Open P0/P1/P2 `0/0/0`)
- approvalChangeSource: `tasks/ul891-set-001-change-001.md` (`fableSecondPlanningApproved: true`, exact target `docs/41-ul891-panel-set-plan.md`)

이 문서는 `experiment/*` fast-track 2-pass 계약의 2차 기획이며, TASK-UL891-SET-001 실험 구현의 최종 source of truth다. 1차 기획의 유지 권고는 보존하고 Codex review의 추가·보류·제거·Finding resolution을 모두 구현 가능한 계약으로 확정했다. 이 문서만으로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있어야 하며, review 요약문이 아니다. 이 문서는 대표 repo·`main` merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

## 1. 한 줄 목표

영업·설계가 UL891 프로젝트를 `세트 사양(버전) → 세트 주문 인스턴스 → 개별 물리 패널` 계층으로 등록·변경하고, 기존 패널 단위 실행 원자를 그대로 유지한 채 부분출하 추적, 선택적 수량 감소와 발주 회수, 프로젝트×출하 달력월(Asia/Seoul) 월별 세금계산서 발행 요청과 확장된 프로젝트 완료 gate까지 한 시스템에서 처리한다.

## 2. 확정 기준선 요약

사용자가 interview에서 직접 확정한 정책(재질문 없음):

- 같은 세트 사양의 구성 패널 이름·규격은 모든 세트에서 동일하다. 구성 정의는 사양에 한 번, 실제 패널은 각자 고유 ID·상태를 가진다.
- 한 프로젝트에 서로 다른 세트 사양×주문 수량 줄이 여러 개 존재할 수 있다.
- 제조·LQC·OQC·전진검수·FAT·QR의 실행 원자는 개별 물리 패널이다. 부분출하를 허용한다.
- 세트별 단가·납기는 입력하지 않는다. 프로젝트 판매액·납기일이 권위값이다.
- 수량 감소는 취소 인스턴스 직접 선택: 미착수 허용, 진행 중 사유+예외 확인, 납품 포함 차단. 번호·식별자는 영구 결번.
- 발주일이 입력된 구매품목 관련 취소는 차단하지 않되 `고객 청구·회수 필요` 사례를 생성하고 `청구 필요 → 발행요청 반영 → 회계 발행 확인 → 회수 확인`으로 추적한다.
- 사양은 버전 관리: 미착수 인스턴스만 명시 선택+사유로 새 버전 적용, 착수 인스턴스는 snapshot 유지, 납품 패널 snapshot 불변. 구성 code는 버전 내 고유, 구성 수는 가변.
- 발행 요청은 기본 프로젝트 1건이되 출하 월이 다르면 복수 건. 계산 기간은 Asia/Seoul 달력 월(1일~말일). 금액은 영업 수동 입력, 누적 ≤ 프로젝트 판매액. 회수 전용 월 후보 허용.
- 프로젝트 완료: active 패널 전량 납품 + 월별 요청 회계 발행 확인 + 회수 사례 전량 회수 확인 + Open Pending 0.
- 비-UL891 Item과 기존 legacy UL891 프로젝트는 평면 패널 구조를 유지한다.

Repository 확인 사실(구현 전제):

- 다음 migration 번호는 `0053`이다(현재 최신 `0052_home_notice_board.sql`).
- `panel_placeholders`는 `(project_id, sequence_number)`·`(project_id, display_code)` unique, `Active/Cancelled` 상태이며, `ChangePanelCountAsync`가 expected active count 낙관 검증, `MaxSequenceNumber` 이후 신규 번호, 선택 취소 시 제조 실행·품질 검사·물류 draft·work item을 같은 transaction에서 정리하는 골격을 이미 제공한다.
- 정산 완료(`SalesSettlementStore.CompleteAsync`)는 project row lock 아래 active 패널 전량 납품(finalized `DeliveryCompleted` batch + active batch unit + active packing membership), Open Pending 0, `sales_billing_request_items` 존재를 검증하고 프로젝트를 `Completed`로 전이한다.
- `sales_billing_request_*`는 append-only trigger와 `unique (project_id)`·`departed_panel_count = active_panel_count` check를 가진 반월 batch 구조이고, 추천 기간은 서울 기준 16일 이후 당월 1~15일, 15일 이전 전월 16~말일이다.
- `project_audit_events.entity_type`은 신규 migration에서 constraint를 drop 후 확장 재정의하는 additive 패턴이 확립되어 있다(`0005`/`0008`/`0009`).
- 권한: 정산 mutation `sales.settle` + project scope + Sales 정/부 또는 open 정산 업무 assignee, 금액 노출 `Project.SalesAmount.Read`, 패널 정보 입력 `PanelInfo.Update`.
- 프로젝트 상세 탭은 전체 흐름·영업·생산관리·설계·구매·자재·제조·품질·물류이며, 패널 상세는 `GET /projects/{projectId}/panels/{panelId}` 기반 설계 요약 중심이다. QR scan landing이 사용하는 stage별 deep link route 패턴(`/manufacturing/work?...`, `/quality/inspections?stage=...`, `/logistics?stage=...`, `/materials/...`)이 이미 존재한다.
- 제조 실행 상태는 `InProgress/Blocked/Completed/Cancelled`이며 패널당 active 실행 1개 partial index가 있다.

## 3. Codex review resolution 반영표

| Review 항목 | 판정 | 본 계약 반영 위치 |
| --- | --- | --- |
| K1 세트 계층·기존 패널 원자 | 유지 | 5장 데이터 모델, 7장 불변조건 — P01…PN display_code·순번 불변, 세트 label은 projection |
| K2 UL891-only·legacy flat 보존 | 유지 | 5.1 `structure_mode`, 10.2 legacy 정책 |
| K3 선택 취소·결번·발주 회수 | 유지+보정 | 6.3, 8.3 — 발주일 품목 존재 시 품목 선택 0건이면 취소 완료 차단, 품목별 case 생성, legacy 자동 연결 금지 |
| K4 부분출하·Packing Unit | 유지 | 6.5, 9.4 — 기존 Packing Unit에 eligible subset, 출발 원자성 불변 |
| K5 프로젝트/패널 상세 정보 구조 | 유지 | 9장 — project-scoped aggregate와 panel-scoped 실행 데이터 분리 |
| A1 사양 버전 `Draft → Published → Superseded` | 추가 | 6.1 — Draft 편집, Published 불변, Published 전 downstream 착수 차단 (F-UL891-001 해소) |
| A2 세트 line·instance 상태 분리, panel_count 호환 | 추가 | 6.2, 10.3 — 주문 수량은 active instance 집계 projection, expected count+operation id CAS |
| A3 월별 canonical ledger + append-only revision | 추가 | 6.4 — 1일·16일 실무와 월별 1건 canonical 동시 보존 (F-UL891-002 해소) |
| A4 완료 조건에 남은 요청 가능액 0 | 추가 | 6.6 — 완료 5조건 (F-UL891-003 해소) |
| A5 회수 상태 자동 연계 | 추가 | 6.3 — revision 포함/확인 시 자동 전이, 역전이 금지 |
| A6 패널 상세 탭 구성 | 추가 | 9.3 — 조회 projection + 정확한 업무 deep link |
| A7 구조 변경·downstream readiness | 추가 | 6.1, 7장 — Published readiness, code 집합 변경 reconcile, structure/Item 변경 차단 (F-UL891-004 해소) |
| A8 감사·privacy-safe evidence | 추가 | 5.4, 11장 |
| D1 구매품목 자동 인스턴스 연결 | 보류(v2) | 13장 제외, 6.3은 수동 선택 필수 |
| D2 세트 단가·납기·구성 원가 | 보류/제외 | 13장 |
| D3 신규 알림·내 업무 종류 | 보류 | 13장 — 기존 downstream workflow 알림만 유지 (F-UL891-005 해소) |
| D4 전 프로젝트 월별 청구 전환 | 보류 | 10.1 — 비-UL891·legacy는 기존 경로 유지 |
| R1 “월 마감 후에만 확정” | 제거 | 6.4로 대체 |
| R2 신규 기능의 인앱 알림/outbox 자동 연결 | 제거 | 13장 — 이번 범위에서 신규 알림 연결 없음 |
| R3 패널 상세 mutation form 복제 | 제거 | 9.3 — 조회 projection + deep link만 |

Finding F-UL891-001~005는 모두 위 반영으로 `RESOLVED_FOR_PLAN` 상태를 구현 계약에 옮겼다. 신규 사용자 결정은 필요하지 않다.

## 4. 대상 사용자와 권한 (확정)

신규 permission code는 만들지 않는다. 기존 permission·scope·assignee 교집합 패턴을 재사용한다.

| 역할 | 행동 | 권한 근거 |
| --- | --- | --- |
| 영업 | 세트 줄·수량·구성 code/수 입력, 수량 증가, 인스턴스 선택 취소(+발주 품목 선택), 월별 발행 요청 revision 생성·금액 입력, 회계 발행 확인 기록, 회수 확인 | 프로젝트 생성·수정은 기존 프로젝트 mutation 권한·scope. 청구·회수·회계 확인은 `sales.settle` + project scope + SalesPrimary/SalesSecondary 또는 open 정산 업무 assignee(기존 `SalesSettlementStore.ActorCanMutateAsync` 패턴 재사용) |
| 설계 | Draft 버전의 구성 패널명·규격 완성, publish, 새 Draft 버전 생성, 미착수 인스턴스 버전 적용 | `PanelInfo.Update` + project scope + Design 정/부 assignee 패턴(기존 패널 정보 수정 계열 재사용) |
| 생산관리·제조·품질·물류 | 기존 개별 패널 원자 실행 — 변경 없음 | 기존 권한 그대로 |
| 조회전용 타부서 | 세트·패널·청구 projection 조회만 | 기존 project read scope. 판매액·요청 금액·잔액은 `Project.SalesAmount.Read` 보유자에게만 노출 |
| System Administrator | 조회·감사 이력 | 기존 관리자 조회 권한. 업무 mutation 우회 없음 |

Backend가 권한·validation·lifecycle·동시성의 authoritative layer다. UI 숨김은 보조일 뿐이다.

## 5. 데이터 모델 (migration `0053`, additive 전용)

기존 migration 수정·번호 재사용 금지. fresh DB와 existing DB 모두 검증한다. `sales_billing_request_*`·`sales_settlements` schema는 변경하지 않는다.

### 5.1 projects·panel_placeholders 확장 (nullable additive 컬럼)

- `projects.structure_mode text null` — check `('Ul891Set')` (null = 기존 평면). 신규 UL891 프로젝트 생성 시에만 `Ul891Set`으로 설정하며 생성 이후 변경 불가(서버 차단 + guard). 기존/legacy UL891과 비-UL891은 null 유지.
- `panel_placeholders.set_instance_id uuid null references ul891_set_instances(id)`, `panel_placeholders.component_code text null`.
- partial unique index: `(set_instance_id, component_code) where set_instance_id is not null and status = 'Active'`.
- 기존 `sequence_number`·`display_code`(P01…) 규칙과 unique 계약은 그대로 사용한다. 세트 label(예: `사양2-3호기-A`)은 저장 컬럼이 아니라 조회 projection이다.

### 5.2 세트 구조 테이블

- `ul891_set_specs`: id, project_id FK, spec_no(프로젝트 내 unique, 결번 영구), name, created/updated actor·시각, version(CAS). soft 상태 없음 — 사양 자체 삭제는 제공하지 않고 인스턴스 취소로 표현.
- `ul891_set_spec_versions`: id, spec_id FK, version_number(spec 내 unique, 1부터 증가), status check `('Draft','Published','Superseded')`, revision_reason, published_by/at. partial unique index로 spec당 Draft 1개만 허용. guard trigger: `Published`·`Superseded` row의 field 변경 금지(단 `Published → Superseded` 상태 전이는 허용), `Draft` 삭제는 미참조 시에만.
- `ul891_set_spec_components`: id, spec_version_id FK, component_code(버전 내 unique, 1~30자 정규화), panel_name null 허용(Draft 동안), panel_specification null 허용, sort_order. guard trigger: 소속 버전이 `Draft`가 아니면 변경 금지.
- `ul891_set_instances`: id, spec_id FK, instance_number(spec 내 unique, 결번 영구·재사용 금지), spec_version_id FK(현재 참조 버전 = snapshot), status check `('Active','Cancelled')`, cancelled_reason, cancelled_exception_ack boolean, cancelled_by/at, version(CAS).

### 5.3 회수·월별 청구 테이블

- `ul891_recovery_cases`: id, project_id FK, set_instance_id FK(필수), procurement_item_id FK(필수 — 발주일 입력 품목), status check `('BillingRequired','AppliedToRequest','InvoiceConfirmed','Recovered')`, note, created_by/at, recovered_by/at, version(CAS). unique `(set_instance_id, procurement_item_id)`.
- `ul891_recovery_case_events`: append-only 상태 전이 이력(case_id, from/to, actor, reason, 시각). guard trigger로 UPDATE/DELETE 금지(purge 예외).
- `sales_monthly_billing_ledgers`: id, project_id FK, billing_month date(해당 월 1일로 정규화), kind check `('Shipment','RecoveryOnly')`, status check `('Open','Requested','InvoiceConfirmed','AdjustmentRequired')`, version(CAS). unique `(project_id, billing_month)` — 월별 1건 canonical key.
- `sales_monthly_billing_revisions`: id, ledger_id FK, revision_number(ledger 내 unique 증가), amount numeric(18,2) >= 0, note, is_adjustment boolean, adjustment_reason, created_by/at, invoice_confirmed_date/by(확인 시 기록). append-only guard trigger — UPDATE/DELETE 금지(purge 예외). "현재 유효 revision" = 최대 revision_number.
- `sales_monthly_billing_revision_panels`: revision_id FK, panel_id FK, packing unit 표시명·출발일 등 bounded snapshot 컬럼 — revision 생성 시점의 출하 근거 snapshot. append-only.
- `sales_monthly_billing_revision_cases`: revision_id FK, recovery_case_id FK — revision에 반영된 회수 사례. append-only.
- operation receipt: `ul891_set_operations`, `sales_monthly_billing_operations` — operation_id pk, actor, payload_fingerprint(sha256 hex check), result_projection jsonb(≤4096 bytes check), append-only guard. `0037`/`0046` 패턴 그대로.

### 5.4 감사·purge

- `project_audit_events.entity_type` check를 drop 후 재정의해 `'SetSpec','SetSpecVersion','SetInstance','RecoveryCase','MonthlyBillingLedger','MonthlyBillingRevision'`을 추가한다(기존 값 유지, `0009` 패턴).
- 모든 append-only guard는 기존 `emi_qms.project_purge` transaction-local 설정의 DELETE 예외를 동일하게 지원하고, 프로젝트 purge 경로에 신규 테이블의 FK 역순 삭제를 추가한다.
- 감사 이벤트에는 actor·사유·bounded before/after projection만 기록한다. 실제 사용자 이름·메일·원본 코멘트를 test artifact·screenshot·report에 남기지 않는다.

## 6. 상태 모델과 lifecycle (확정)

### 6.1 사양 버전: `Draft → Published → Superseded`

- 영업이 세트 줄 생성 시 v1 `Draft`가 만들어지고 구성 code·구성 수가 입력된다. 인스턴스·물리 패널은 이 시점에 즉시 생성된다(패널명·규격은 미정 허용).
- 설계는 `Draft`에서 구성 패널명·규격을 채우고, 필수값이 모두 채워지면 `Published`로 확정한다. Published 구성·버전은 불변이다.
- 변경은 최신 Published를 복사한 다음 `Draft` 버전(spec당 1개)을 만들어 편집 후 publish한다. publish 시 직전 Published는 `Superseded`가 되지만 이를 참조 중인 착수 인스턴스의 snapshot 유효성은 유지된다.
- downstream 착수 차단: `structure_mode='Ul891Set'` 프로젝트의 패널은 소속 인스턴스의 참조 버전이 `Published`가 아니면 제조 작업 시작을 서버에서 차단하고, 생산계획·제조 투입 조회에 “설계 사양 미확정” readiness를 표시한다. 이미 착수한 패널은 영향 없다.
- 새 버전 적용(명시 대상 선택+사유 필수):
  - 구성 code 집합이 동일하면: 미착수 인스턴스는 참조만 교체. 진행 중 인스턴스도 code 집합이 같은 사양 변경에 한해 사유와 함께 허용.
  - code 추가/삭제가 있으면: 미착수 인스턴스에만 허용. 추가 code 패널은 새 ID·새 순번으로 생성, 제거 code 패널은 기존 취소 cascade로 `Cancelled` 처리(ID 재사용 금지). 진행 중 인스턴스의 구조 변경은 차단.
  - 납품 패널 포함 인스턴스는 모든 버전 적용 차단.

### 6.2 세트 인스턴스와 수량

- 주문 수량 = 해당 spec의 `Active` 인스턴스 수 projection이며 직접 덮어쓰는 숫자가 아니다.
- 수량 증가: 프로젝트 완료 전 허용. `max(instance_number)+1`부터 새 인스턴스를 만들고 현재 참조 버전 구성대로 새 패널(새 ID, `MaxSequenceNumber` 이후 순번)을 생성한다.
- 수량 감소: 취소할 인스턴스를 직접 선택. 판정은 6.3.
- 모든 증감 mutation은 `expectedActiveInstanceCount`(spec 단위)+`operationId`(receipt)로 replay·경쟁을 차단하고 project row lock 아래 수행한다.
- 착수 판정(authoritative, 같은 transaction 내 조회): 인스턴스 소속 Active 패널 중 하나라도 ① 제조 실행 `InProgress/Blocked/Completed` 존재 ∨ ② 키팅 완료 ∨ ③ LQC/OQC/고객검수/FAT 검사 기록 존재 ∨ ④ active packing membership 존재.
- 납품 판정: 기존 정산 완료와 동일한 finalized `DeliveryCompleted` batch + active membership + delivery result 판정을 재사용.

### 6.3 인스턴스 취소와 회수 사례

```text
인스턴스: Active → Cancelled
  미착수: 즉시 허용(사유 입력)
  착수: 사유 + 예외 확인 체크 필수
  납품 패널 포함: 차단
회수 사례: BillingRequired → AppliedToRequest → InvoiceConfirmed → Recovered
```

- 취소 transaction: 대상 인스턴스의 Active 패널 각각에 기존 `ChangePanelCountAsync` 취소 cascade(제조 실행 취소, 품질 검사 취소, 물류 draft 취소, ManufacturingWork work item 취소, 패널 `Cancelled`, 감사 이벤트)를 재사용해 원자적으로 적용한다.
- 프로젝트에 발주일이 입력된 구매품목이 하나라도 있으면, 영업이 이번 취소와 관련된 품목을 명시적으로 선택해야 한다(0..N이 아니라 최소 1건 — 선택 0건이면 취소를 완료하지 않고 선택을 요구한다). 선택 품목마다 회수 사례 1건을 같은 transaction에서 생성한다. 발주일 품목이 전혀 없으면 사례 없이 취소만 완료한다. legacy 품목의 자동 연결·추정 연결은 하지 않는다.
- 상태 자동 연계: 사례가 월별 revision에 포함되면 `AppliedToRequest`, 그 revision이 회계 확인되면 `InvoiceConfirmed`로 자동 전이. `Recovered`는 영업이 확인일·비고와 함께 수동 전이. 역방향 전이는 금지하며 revision 대체·조정은 감사 이벤트로만 남기고 이미 확인된 사례 상태를 조용히 낮추지 않는다.

### 6.4 월별 발행 요청: canonical ledger + append-only revision

```text
월 ledger: Open → Requested → InvoiceConfirmed
                 ↑(새 revision 대체)      ↓(확인 후 추가 근거)
             Requested ← AdjustmentRequired
```

- canonical key: `project_id + 출하 달력월(Asia/Seoul, 1일~말일)` 1건. 실제 출발·출하일 기준으로 월을 귀속한다.
- ledger는 해당 월 첫 출하 근거가 생기거나, 회수 사례 반영을 위해 영업이 명시적으로 열 때(`RecoveryOnly`) 생성된다. `Open` 동안 출하 근거가 계속 누적된다.
- 영업은 매월 1일·16일 등 실무 시점 제약 없이 `Open`/`Requested` ledger에서 발행요청 revision을 생성할 수 있다(월 진행 중에도 가능 — 1차안의 “마감 월만 확정”은 제거됨). revision은 append-only이며 수동 금액과 당시 출하·회수 근거 snapshot을 보존한다.
- 회계 발행 확인 전에는 새 revision이 이전 revision을 대체한다(최신 revision이 유효). 회계 확인 뒤 같은 월의 추가 출하가 확인되면 ledger를 `AdjustmentRequired`로 전이하고 사유를 포함한 조정 revision을 허용한다.
- 월 마감은 확정 조건이 아니라 누락 점검 기준이다: 마감된 월에 미청구 출하가 있으면 화면에 `발행요청 필요`, 확인 금액보다 추가 근거가 생기면 `조정 필요`를 표시한다.
- 금액 검증: 모든 ledger의 현재 유효 revision 금액 합계가 프로젝트 판매액을 초과하면 project row lock 아래 서버가 차단한다. 화면은 판매액·누적 요청액·잔여 가능액·해당 월 출하/회수 근거를 함께 보여준다(금액은 `Project.SalesAmount.Read` gate).
- 회계 발행 확인은 발행 확인일(프로젝트 생성일 이상, 서울 오늘 이하)·번호(64자)·메모(500자) 규칙 등 기존 정산 invoice 검증 규칙을 재사용해 최신 revision에 기록하고 ledger를 `InvoiceConfirmed`로 전이한다.

### 6.5 부분출하 추적

- 세트는 주문·추적 묶음이며 포장 단위가 아니다. 물류는 출하 가능한 개별 패널 subset을 기존 Packing Unit에 담아 기존 원자성 그대로 출발·납품 처리한다. 물류 화면·store는 변경하지 않는다.
- 프로젝트·세트·패널 상세 projection에서 인스턴스×구성 패널별 Packing Unit·출발일·납품 상태를 조회한다(신규 read-only 조회, mutation 없음).

### 6.6 프로젝트 완료 gate (Ul891Set 프로젝트, 5조건)

기존 정산 완료 transaction에서 `structure_mode='Ul891Set'`이면 기존 `HasBillingRequest` 검사를 아래로 대체한다. 비-UL891·legacy는 기존 gate 그대로.

1. active 패널 전량 납품(기존 판정 재사용)
2. Open Pending 0(기존 판정 재사용)
3. 모든 월 ledger가 `InvoiceConfirmed`이고 `AdjustmentRequired`·미청구 출하 월(`Open` 상태로 근거만 있는 월 포함)이 없음
4. 모든 회수 사례가 `Recovered`
5. 프로젝트 판매액 − 확인된 현재 유효 revision 금액 합계 = 0. 판매액이 미입력·0 이하이면 완료 차단(기존 영업 정산 validation과 같은 방식의 행동 가능한 한글 오류)

완료 후 세트·수량·사양·청구·회수 mutation은 기존 lifecycle fence 패턴으로 차단한다.

## 7. 업무 규칙과 불변조건 (최종)

- 패널 식별자·순번·display_code는 영구 결번, 재사용 금지. 인스턴스 번호·spec 번호도 결번 영구.
- 제조·LQC·OQC·전진검수·FAT·QR·키팅·물류 실행 원자는 개별 `panel_placeholder`. 세트 aggregate로 합치지 않으며 downstream store의 실행 계약을 변경하지 않는다.
- Packing Unit 출발 원자성·증빙 계약 불변.
- Published 버전·구성 불변, 납품 패널 snapshot 불변, 착수 인스턴스의 구조(code 집합) 변경 금지.
- `structure_mode='Ul891Set'` 프로젝트: Item 변경·평면 회귀·`ChangePanelCountAsync` 평면 경로 사용을 서버에서 차단하고 행동 가능한 한글 오류를 반환한다. 반대로 평면 프로젝트는 세트 API를 사용할 수 없다.
- 발주일 품목 관련 취소는 차단하지 않되 회수 사례 생성을 같은 transaction에서 누락 없이 수행한다.
- 월별 요청 누적 ≤ 프로젝트 판매액 — project row lock 하 서버 검증. revision·회수 이력은 append-only.
- 모든 mutation은 기존 패턴을 따른다: 단일 transaction, project row lock(+대상 패널 lock), version/expected-count 낙관 검증, operation receipt(fingerprint) 멱등성, `project_audit_events` + append-only operation 기록.
- 비-UL891·legacy 프로젝트의 생성·수정·수량 변경·정산·반월 발행요청 흐름은 회귀 없이 유지한다.

## 8. Backend API 계약 (모두 `/api/projects/{projectId}` 하위, 기존 endpoint group 재사용)

| 구분 | Endpoint(안) | 권한 | 핵심 규칙 |
| --- | --- | --- | --- |
| 세트 구조 조회 | GET `/set-structure` | project read scope | spec·버전·구성·인스턴스·패널·출하 추적 projection. 금액 없음 |
| 세트 프로젝트 생성 | 기존 프로젝트 생성 요청에 UL891 세트 줄 입력 확장 | 기존 프로젝트 생성 권한 | Item=UL891 + 세트 줄 ≥ 1이면 `structure_mode='Ul891Set'`로 생성, spec·v1 Draft·구성·인스턴스·패널을 한 transaction에 생성 |
| 세트 줄 추가 | POST `/set-specs` | 영업(프로젝트 수정 권한) | 신규 spec+v1 Draft+인스턴스+패널 생성 |
| 구성 편집 | PUT `/set-specs/{specId}/versions/{versionId}` | 설계(`PanelInfo.Update`) | Draft만. code 추가/삭제 시 소속 미착수 인스턴스 패널 reconcile은 publish 시점에 수행 |
| Publish | POST `/set-specs/{specId}/versions/{versionId}/publish` | 설계 | 필수값 완비 검증, 직전 Published `Superseded` |
| 새 Draft 버전 | POST `/set-specs/{specId}/versions` | 설계 | 최신 Published 복사, spec당 Draft 1개 |
| 버전 적용 | POST `/set-specs/{specId}/apply-version` | 설계 | 대상 인스턴스 명시 선택+사유, 6.1 판정 |
| 수량 증가 | POST `/set-specs/{specId}/instances/increase` | 영업 | expected count+operationId, 새 인스턴스·패널 |
| 인스턴스 취소 | POST `/set-instances/cancel` | 영업 | 6.3 판정, 발주 품목 선택, 회수 사례 생성, 기존 취소 cascade |
| 회수 조회/확인 | GET `/recovery-cases`, POST `/recovery-cases/{caseId}/recover` | 영업(`sales.settle` 계열) | 수동 `Recovered` 전이, CAS |
| 월별 청구 조회 | GET `/monthly-billing` | project read scope(금액은 `Project.SalesAmount.Read`) | ledger·revision·근거·누적/잔여 |
| 회수 전용 월 열기 | POST `/monthly-billing/open` | 영업 | `RecoveryOnly` ledger 생성 |
| Revision 생성 | POST `/monthly-billing/{ledgerId}/revisions` | 영업(`sales.settle` 계열 + `Project.SalesAmount.Read`) | 수동 금액, 근거 snapshot, 회수 사례 선택 포함, cap 검증, AdjustmentRequired 시 사유 필수 |
| 회계 발행 확인 | POST `/monthly-billing/{ledgerId}/confirm` | 영업(`sales.settle` 계열) | invoice 검증 재사용, 회수 자동 전이 |
| 정산 완료 | 기존 POST `…/settlement/complete` 확장 | 기존 | `Ul891Set`이면 6.6 5조건으로 대체 |

- 모든 mutation body는 `operationId`·`expectedVersion`(또는 expected count)을 요구하고 receipt replay를 반환한다.
- 제조 시작 endpoint에 `Ul891Set` 패널의 Published readiness 검증을 추가한다(기존 제조 store의 시작 검증 지점, 다른 로직 불변).
- 오류 메시지는 기존 관례대로 행동 가능한 한글 문장으로 반환한다. raw API/DB body를 로그·문서에 남기지 않는다.

## 9. Frontend·UX 계약

### 9.1 프로젝트 생성/수정·영업 탭

- Item=UL891 신규 생성: `panel_count` 입력 대신 세트 줄 편집기(사양명, 주문 수량, 구성 code 목록·구성 수). 비-UL891·legacy는 기존 입력 유지. 기존 Action Feedback 계약과 필드별 오류 표시 재사용.
- 영업 탭 확장: 세트 줄×인스턴스 집계(active/취소/진행/납품), 수량 증가·인스턴스 선택 취소(차단/예외 확인 dialog — 사유·예외 확인 label 명시), 발주 품목 선택 단계, 회수 사례 요약과 회수 확인. 판매액·요청액은 권한 gate.

### 9.2 설계 탭·세트 추적

- 설계 탭: 사양 버전 목록(Draft/Published/Superseded badge), Draft 구성 편집, publish, 새 버전, 미착수 인스턴스 버전 적용. 착수 인스턴스 잠금 안내.
- 세트 추적(물류 탭 확장 또는 전체 흐름 내 섹션): 인스턴스×구성 패널의 Packing Unit·출발일·납품 상태 matrix. desktop은 표, 390px 모바일은 인스턴스별 card 접기로 page-level horizontal overflow 0. 조회전용은 수정 버튼 비노출.

### 9.3 패널 상세 (A6 확정, mutation form 복제 금지)

기존 패널 상세를 조회 projection 중심으로 확장한다:

- 기본/세트: 프로젝트, 사양·버전, 인스턴스 번호, 구성 code·패널명·규격, Active/Cancelled
- 설계: 기존 패널 정보 + 사양 snapshot
- 구매·자재: 패널 직접 연결 항목이 없으면 “프로젝트 공통” 명시 + 프로젝트 탭 deep link
- 제조: 상태·4단계·담당자·최근 event / 품질: LQC·OQC·고객검수·FAT 판정·Pending·evidence / 물류: Packing Unit·출발·납품 / QR: 발급 상태·보기
- 입력은 기존 담당 workspace가 authoritative — QR landing이 쓰는 기존 route 패턴(`/manufacturing/work?...`, `/quality/inspections?stage=...`, `/logistics?stage=...`, `/materials/...`)으로 정확히 이동시키고 패널 상세에 입력 form을 만들지 않는다.

### 9.4 월별 청구 화면

- 진입: 프로젝트 상세 정산·완료 흐름에서 `Ul891Set` 프로젝트일 때 월별 청구 view. 비-UL891은 기존 화면 유지(프로젝트 유형별 경로 안내 문구 포함).
- 표시: 월 ledger 목록(상태 badge: Open/Requested/InvoiceConfirmed/AdjustmentRequired/발행요청 필요), revision 이력(금액·작성자·시각·조정 사유), 해당 월 출하 근거(패널·Packing Unit·출발일)와 반영 회수 사례, 판매액·누적 요청액·잔여 가능액.
- 행동: revision 생성(금액 입력, 회수 사례 선택), 회계 발행 확인 기록, 회수 전용 월 열기. cap 초과·조건 미충족은 서버 오류 메시지를 action 근처에 표시.
- 공통: 기존 `toLoadError`·Action Feedback·tablist aria 패턴·DESIGN-000 token/primitive 재사용. desktop과 390px 모두 핵심 행동 가능.

## 10. 기존 기능과의 연결·호환 경계

### 10.1 청구·정산 공존

- `Ul891Set` 프로젝트: 월별 ledger/revision이 발행 요청 경로이고 완료 gate는 6.6. 기존 반월 batch Excel 화면에는 `Ul891Set` 프로젝트를 후보로 넣지 않는다(unique(project_id)·전량 출하 check와 충돌 방지).
- 비-UL891·legacy UL891: 기존 반월 batch·단일 정산·기존 완료 gate 유지. `sales_billing_request_*`·`sales_settlements`는 schema·데이터 모두 불변.

### 10.2 legacy UL891

- 자동 전환 없음. 기존 UL891 프로젝트는 `structure_mode` null로 남고 상세 화면에 `Legacy 평면` badge만 표시한다. migration은 데이터 변환을 수행하지 않는다.

### 10.3 panel_count·수량 변경 호환

- active 패널 수는 기존처럼 `panel_placeholders` projection으로 일관 유지된다. 세트 mutation이 같은 transaction에서 실제 패널을 생성·취소하므로 별도 동기화 컬럼이 필요 없다.
- `ChangePanelCountAsync`(평면 경로)는 `Ul891Set` 프로젝트에서 한글 오류로 거부한다. 세트 경로 취소는 그 내부 cascade 로직을 함수 단위로 재사용한다.

### 10.4 기타

- QR·제조·품질·물류·Pending·키팅: 계약 불변. 세트 맥락은 조회 projection으로만 추가.
- Excel: 신규 export는 선택 범위(월별 revision 이력의 기존 선택 export registry 재사용)이며 v1 필수가 아니다.
- 알림·내 업무: 신규 종류를 만들지 않는다. 패널 취소 시 기존 work item 정리, 기존 단계 handoff 알림만 그대로 동작한다.
- 삭제·복구: 프로젝트 soft-delete·purge 경로에 신규 테이블 정리 포함(5.4).

## 11. 검증 계획

- migration: `PostgreSqlMigrationTests` fresh + existing 경로에 `0053` 포함 통과. append-only·guard trigger·unique·check 동작 검증.
- Backend 단위/통합(신규): 세트 생성(프로젝트 생성 확장 포함), Draft 편집·publish·버전 적용(코드 집합 동일/변경, 착수/미착수/납품 판정), 수량 증가·선택 취소(미착수/진행/납품, 발주 품목 선택 필수, cascade), 회수 사례 생성·자동/수동 전이·역전이 금지, 월 ledger unique·revision append-only·cap 초과 차단·회수 전용 월·조정 흐름, 완료 5조건 각각의 실패 사례, operation receipt replay·CAS 충돌, 권한(조회전용 403, 관리자 mutation 403), Published 전 제조 시작 차단.
- Backend 회귀: 비-UL891 프로젝트 생성·수량 변경·정산 완료, 기존 반월 발행요청, 물류 출발·납품, Backend 전체 test suite 통과(현재 기준선 418건 + 신규).
- Frontend: lint/typecheck/unit/build. 신규 unit — 세트 줄 편집기, 취소 dialog(사유·예외 확인), 월별 청구 화면(금액 gate 포함), 패널 상세 projection·deep link.
- Full-Stack E2E(isolated 전용 DB·container, Persistent UAT 미사용): UL891 세트 프로젝트 생성 → 설계 publish → 제조·품질·물류 부분출하 → 월별 revision·회계 확인 → 수량 감소·회수 → 완료 gate까지 1개 시나리오 + 기존 project registration·logistics·settlement·billing E2E 회귀.
- Visual: 페이지별 desktop·390px screenshot, page-level overflow 0, privacy-safe 합성 데이터만 사용.
- 사용자 검수: 완료 원장 6장의 고정 검수 runtime에서 handoff하고 `사용자 검수 대기`로 추적한다. 완료로 가장하지 않는다.

## 12. 완료 기준과 중단 조건

완료 기준:

- interview 12장 성공 기준 전부 + 3장 review resolution 전부가 구현·검증됨.
- 11장 자동 검증 전부 통과, Open P0/P1/P2 = 0.
- 5종 종료 산출물 상태·위치를 `docs/12-task-completion-policy.md` 기준으로 추적(implementation report `tasks/ul891-set-001-implementation-report.md` 중심).
- desktop·390px 핵심 행동 검증 screenshot 확보.
- local experiment commit까지만 수행. Git 게시 상태와 사용자 검수 대기 상태를 명시 보고.

중단 조건: 기존 migration 수정이 불가피해지는 충돌, 기존 계약(반월 unique·Packing Unit 원자성·panel unique)과의 해소 불가 충돌, read-only·격리·승인 경계 위반이 필요한 상황 — 구현을 중단하고 위치·영향·선택지를 보고한다.

## 13. 명시적 제외 (v1)

- 세트별 판매단가·세트별 납기일·구성 패널별 원가 계산
- 구매품목의 세트/패널 자동 귀속(v2 후속 — 발주 입력·Excel·자재·IQC 계약 확장 필요)
- 신규 알림·내 업무 종류, 신규 outbox 연결, 실제 Teams/Mail provider 발송
- ERP/회계·국세청 실제 발행 연동
- 비-UL891·legacy UL891의 세트 구조 전환, 전 프로젝트 월별 청구 전환
- Packing Unit 출발 원자성 변경, 물류 화면 재작성
- 패널 상세의 부서별 mutation form 복제
- 대표 repo·`main`, push·PR·merge, Persistent UAT migration/runtime handover

## 14. 안전·승인 경계

- Persistent UAT 영향 없음. isolated PostgreSQL·disposable E2E DB만 사용.
- migration `0053` 1건, additive 전용, forward-fix 원칙. rollback은 신규 테이블·컬럼 미사용 상태에서의 forward 제거 migration으로 기록.
- 실제 provider 발송·runtime 교체 없음. push·PR·merge·Persistent UAT·실제 provider는 승인되지 않음(`main` merge 승인 0/3). local experiment commit만 change-001 범위에서 승인됨.
- 이 문서는 게시·merge·Persistent UAT·사용자 검수 완료를 주장하지 않는다.

## 15. Codex 구현 지시문 (최종)

1. 이 2차 기획만 구현 계약으로 사용한다. `tasks/ul891-set-001-planning.md`·`tasks/ul891-set-001-review.md`는 수정 없이 판단 이력으로 보존한다.
2. `database/migrations/0053` 하나로 5장 schema 전체(guard trigger·audit entity_type 확장·purge 연동 포함)를 additive로 추가하고 fresh·existing 모두 검증한다. 기존 migration과 `sales_billing_request_*`·`sales_settlements`는 수정하지 않는다.
3. Backend는 6~8장 계약을 기존 패턴(project row lock, version/expected-count CAS, operation receipt, 취소 cascade 재사용, 한글 오류)으로 authoritative하게 구현한다. 제조 시작에 Published readiness 검증을 추가하되 다른 실행 계약은 바꾸지 않는다.
4. Frontend는 9장 계약대로 기존 프로젝트 상세 탭·정산 흐름을 additive 확장하고 DESIGN-000 token·기존 feedback 계약을 재사용한다. 패널 상세는 projection+deep link만 추가한다.
5. 11장 검증 전부(Backend 전체, Frontend lint/typecheck/unit/build, migration fresh+existing, isolated Full-Stack E2E, desktop·390px screenshot)를 통과시키고, 비-UL891·legacy 회귀 0을 확인한 뒤 local experiment commit만 수행한다.
6. 종료 시 implementation report에 5종 산출물 상태·미실행 항목·rollback·사용자 검수 대기 상태를 기록하고, Roadmap·완료 원장 동기화는 Codex 범위로 수행한다.

## 16. 비차단 항목 확정 기록

interview 9장 8건은 모두 standing instruction에 따라 아래와 같이 확정했다. 사용자 재질문은 없다.

| 항목 | 확정안 | 근거 |
| ---: | --- | --- |
| 1 세트 schema·snapshot | 불변 Published 버전 참조 + Draft lifecycle(5·6.1장) | 복사 없는 snapshot과 최초 설계 입력을 동시에 만족(F-UL891-001) |
| 2 입력·역할 분리 UX | 영업 code/수량 → 설계 Draft 완성·publish(9장) | 사용자 확정 역할 분리와 기존 탭 권한 구조 일치 |
| 3 legacy UL891 | 자동 전환 없음, `Legacy 평면` badge(10.2) | 안전한 대응 추론 source 부재, migration 위험 0 |
| 4 월별 lifecycle·1일/16일 | 월 ledger + append-only revision + 조정(6.4) | 월별 1건 canonical과 1일·16일 실무를 동시 보존(F-UL891-002) |
| 5 구매품목 연결 v1 | 취소 시 발주일 품목 명시 선택 필수, 품목별 사례, 자동 연결 금지(6.3) | 구매 schema 무변경·회수 누락 방지, 자동 귀속은 v2 |
| 6 진행·납품 판정 | 기존 제조·키팅·검사·물류 원장 직접 조회 + 기존 납품 판정 재사용, 단일 transaction(6.2) | authoritative 원장 재사용, race-safe |
| 7 상세 정보 구조·권한 | 프로젝트 상세 aggregate + 패널 상세 projection/deep link, 조회전용 read-only(9장) | 사용자 ownership 경계와 R3 반영 |
| 8 정산 호환 경계 | `Ul891Set`만 완료 gate 대체(5조건), 기존 경로·schema 불변(6.6, 10.1) | append-only·unique 계약 무손상, 잔액 0 gate 포함(F-UL891-003) |

openBlockingDecisionCount: 0
