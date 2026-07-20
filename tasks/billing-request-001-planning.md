The baseline is complete. I have the confirmed interview, roadmap, ledger, TASK-014A settlement contract, logistics delivery evidence, export/workbook infrastructure, permission model, and project data fields. Producing the primary planning draft now.

# TASK-BILLING-REQUEST-001 — 세금계산서 발행요청 자료 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/billing-request-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 영업의 실제 책임은 세금계산서 직접 발행이 아니라 회계팀 발행요청 자료 작성이다. 매월 1일·16일 정기 업무에서 반월 납품분을 수기로 대조·정리하면서 누락·중복·잘못된 금액 전달 위험이 있다.
- 대상 사용자·역할: 영업 정·부 담당(후보 조회·선택·Excel 생성·이력 확인), 영업 부서장(팀 요청 현황·누락 보정), System Administrator(운영 지원·재다운로드), 회계팀(시스템 밖 수신, 이번 MVP에 시스템 계정 workflow 없음), 다른 부서(프로젝트 탭 조회, 금액 권한 없으면 금액 비노출).
- 정상 흐름: 영업 발행요청 화면 → 1일/16일 기준 추천 반월 기간 확인 → 납품 완료 후보 조회 → 전체/개별 선택 → 요청자료 생성 → `.xlsx` 다운로드 → batch와 project별 요청 상태 기록.
- 예외·복구 흐름: 선택 0건·미납품·접근 불가·금액 권한 없음·open Pending·기존 요청 중복·stale candidate의 서버 재검증, Excel 생성 실패 시 요청 미기록, 다운로드 실패 시 같은 batch 재다운로드.
- 확정한 정책과 명시적 제외: 1일/16일 정기 요청·프로젝트 선택·서버 Excel·요청 이력·멱등, 실제 회계/ERP/국세청/메일/Teams provider 제외, 회계팀 계정 workflow·수정세금계산서 제외, 대표 repo·`main`·Persistent UAT·push·PR·merge 불변.
- planning으로 넘긴 비차단 미결정 사항: ① 기간 경계·수동 변경 범위, ② Excel 생성과 요청 완료 원자성 계약, ③ 중복·재요청 정책, ④ final 완료 의미와 18단계 정합, ⑤ 필수 Excel 열과 누락 회계 필드 처리. 모두 experiment standing instruction에 따라 Fable 권장안 자동 채택 대상이다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

영업 담당자가 반월 기간의 납품 완료 프로젝트를 선택해 회계팀 전달용 세금계산서 발행요청 `.xlsx`를 한 번의 행동으로 생성·기록하고, 이미 요청한 프로젝트를 구분할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 납품 완료 프로젝트를 화면별로 따로 찾아 회계팀 요청 목록을 수기로 정리한다. 시스템의 `TASK-014A` 정산 화면은 영업이 세금계산서 발행일·번호를 직접 입력해 프로젝트를 완료하는 계약이라 “영업이 직접 발행하는 것처럼” 보인다.
- 시간 손실·누락·중복은 반월 마감 기간 판별, 납품 근거 대조, 이미 요청한 프로젝트 구분, 회계팀 전달 열 구성의 수작업에서 발생한다.
- 현재 우회 방식은 프로젝트 목록 선택 Excel(`TASK-EXPORT-001/002`)을 받아 수작업으로 가공하는 것이나, 반월 기간·납품 완료 판정·요청 이력·중복 방지가 없다.
- 이 기능이 없으면 누락·중복 요청, 잘못된 기간·금액 전달, final stage 문구와 실제 책임의 불일치가 계속된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 정·부 담당 | 후보 조회, 선택, Excel 생성, 이력 확인, 재다운로드 | 기존 project access scope + `sales.settle` | 자신이 접근 가능한 납품 완료 프로젝트의 요청 batch 생성 |
| 영업 부서장 | 팀 요청 현황 확인, 누락 보정(재요청 포함) | 기존 영업 project scope | 같은 기능 (신규 권한 없음) |
| System Administrator | 운영 지원, 재다운로드 | 전체 (기존 권한) | 재다운로드만 (업무 입력 우회 없음) |
| 회계팀 | 생성된 Excel을 시스템 밖에서 수신 | 시스템 계정 없음 (범위 밖) | 없음 |
| 다른 부서 | 프로젝트 상세 영업 탭에서 요청 상태 조회 | 기존 project read scope | 없음. `Project.SalesAmount.Read` 없으면 금액 비노출 |

- 금액이 포함되는 workbook 생성은 `sales.settle`에 더해 `Project.SalesAmount.Read`를 요구한다(권장안 5). 회계 인계 자료에서 금액 열 생략은 업무 목적을 훼손하므로 열 생략 대신 생성 자체를 validation으로 차단한다.
- Backend가 권한·scope·납품·Pending·중복의 authoritative layer다. UI 숨김은 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 16일 정기 발행요청

1. 영업 담당자가 16일 오전 `영업 > 발행요청` 화면을 연다. 시스템이 Seoul 기준 오늘 날짜로 “당월 1일~15일” 추천 기간과 미요청 건수를 보여 준다.
2. 납품 완료 후보 목록에서 전체선택 후 제외할 프로젝트만 해제하고 `회계팀 발행요청 자료 생성`을 누른다.
3. 서버가 scope·납품 완료·open Pending·중복·금액 권한을 재검증하고 batch·project snapshot·workbook을 한 트랜잭션으로 기록한 뒤 `.xlsx`를 내려준다. 목록의 해당 프로젝트는 `요청됨`으로 바뀌고 요청 이력에 batch가 추가된다.

### 시나리오 B — 다운로드 실패 후 재다운로드

1. 담당자가 batch 생성 직후 네트워크 문제로 파일 저장에 실패한다.
2. 요청 이력에서 같은 batch의 `다시 다운로드`를 누른다.
3. 서버가 저장된 workbook을 동일 checksum으로 다시 내려주고 다운로드 이력을 남긴다. 새 batch는 생기지 않는다.

### 시나리오 C — 누락 보정 재요청

1. 부서장이 지난 반월에 빠진 프로젝트를 발견하고 기간을 수동으로 조정해 조회한다.
2. 해당 프로젝트가 이미 다른 batch에 `요청됨`이면 기본적으로 재선택이 차단되고, 명시적 재요청 사유를 입력한 경우에만 새 요청으로 포함된다.
3. 이전 요청 row는 `Superseded`로 전환되고 사유·행위자·시각이 이력에 남는다.

### 시나리오 D — 요청 상태 조회

1. 다른 부서 사용자가 프로젝트 상세의 영업 탭을 연다.
2. 시스템이 발행요청 상태(미요청/요청됨/재요청됨, 요청일)를 표시한다. 금액 권한이 없으면 금액은 보이지 않는다.
3. 영업은 같은 화면에서 정산(발행 확인) 단계로 이어지는 다음 행동을 인지한다.

## 5. 기능 요구사항

### 필수

- [ ] Seoul 기준 오늘 날짜로 반월 추천 기간 계산: 1일~15일에 실행하면 직전 월 16일~말일, 16일~말일에 실행하면 당월 1일~15일을 기본값으로 제시
- [ ] 납품 완료 후보 조회: 활성 프로젝트 중 모든 활성 패널이 `DeliveryCompleted` Finalized batch로 납품 완료되고, 최종 납품일(Seoul)이 기간 안에 있는 프로젝트
- [ ] 후보별 표시: 프로젝트 코드·이름·고객사·최종 납품일·패널 수·open Pending 수·요청 상태·(권한 시) 공급가액/통화
- [ ] checkbox 전체선택·개별선택과 선택 요약 tray (기존 선택 export 패턴 재사용)
- [ ] 요청 batch 생성: 서버 재검증(선택 0건, 미납품, scope 밖, 금액 권한, open Pending>0, 중복 요청, stale candidate) 후 batch·project별 snapshot row·workbook·audit를 단일 트랜잭션으로 기록
- [ ] `operationId` 기반 멱등: 같은 operation 재시도는 같은 batch·파일을 재생하고 새 batch를 만들지 않음 (기존 `sales_settlement_operations` receipt 패턴 재사용)
- [ ] 프로젝트별 활성 요청 1건 강제(중복 차단)와 사유 필수 재요청(revision) 시 이전 row `Superseded` 전환
- [ ] 서버 생성 `.xlsx`: 존재하는 근거 열만 출력, formula-safe text, checksum 저장, 재다운로드 시 byte 동일
- [ ] 요청 이력 목록: batch별 기간·건수·생성자·생성일·재다운로드
- [ ] 프로젝트 상세 영업 탭·정산 화면에 요청 상태 표시
- [ ] 정산 화면 문구를 실제 책임에 맞게 조정: “영업이 발행”이 아니라 “회계팀에 발행 요청 → 발행 확인(발행일·번호) 입력 → 프로젝트 완료” 구조가 드러나게
- [ ] desktop 1440 / mobile 390px adaptive UI, 390px 핵심 흐름 horizontal overflow 0

### 선택

- [ ] batch 요청 메모(회계팀 전달 사항, 500자 이하) — workbook header 영역에 표기
- [ ] 후보 검색/고객사 filter (desktop)

### 명시적 제외

- [ ] 실제 회계·ERP·국세청 전자발행 API, 이메일·Teams 첨부 발송
- [ ] 회계팀 계정의 발행 완료 workflow와 수정세금계산서
- [ ] 사업자등록정보 마스터 구축, 세율·부가세 계산, 수금·채권·원가
- [ ] `TASK-014A` 완료 계약(발행일 입력 → 프로젝트 완료)의 구조 변경 — 문구·상태 표시만 조정
- [ ] 대표 repo·`main`·Persistent UAT·push·PR·merge·실제 provider

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 발행요청 (desktop) | `영업` 메뉴 하위 탭 (KPI와 나란히), `/sales/billing-requests` | 추천 기간, 요약 KPI(후보/미요청/요청됨), 후보 table(코드·이름·고객사·납품일·패널·Pending·상태·금액), 요청 이력 | 기간 조정, 검색, 전체/개별 선택, 자료 생성, 재다운로드, 사유 입력 재요청 | 생성 성공 시 다운로드+상태 갱신, 검증 실패는 action 근처 구조화 오류(기존 action feedback 패턴), 409 시 목록 재조회 |
| 발행요청 (mobile 390px) | 동일 URL adaptive | 현재 기간, 미요청 건수, 프로젝트 핵심 정보 카드 한 열, 선택·다운로드 | 선택과 자료 생성·최근 batch 재다운로드만 | 동일 feedback, 복잡한 열 설정·과거 batch 상세 관리는 PC 우선 |
| 프로젝트 상세 영업 탭 / 정산 화면 | 기존 경로 | 발행요청 상태·요청일, 정산 진행 조건 | 상태 확인, 정산 화면으로 이동 | 기존 패턴 유지 |

확인할 UX 항목:

- 후보 없음 / 이미 모두 요청함 / 선택 없음 / 생성 중 / stale candidate / 재다운로드 상태를 행동 근처에 표시한다.
- `세금계산서 발행` 대신 `회계팀 발행 요청`, `정산·완료` 대신 다음 행동이 드러나는 문구를 사용한다.
- WITHUS 기반 DESIGN-000 token과 `DsPageHeader`·`DsSurface`·`DsToolbar`·`DsTabs`·`DsBadge` 공통 component, 얇은 divider·절제된 shadow·compact controls·blue accent를 유지한다.
- 금액 권한 없는 사용자에게 금액 열·합계를 렌더링하지 않는다(서버 응답에서 제외).

## 7. 업무 규칙과 불변조건

- 납품 완료 전 프로젝트는 후보가 아니다. 납품 판정은 `TASK-014A`와 동일한 근거(`logistics_delivery_results` + Finalized `DeliveryCompleted` batch + active membership)를 사용한다.
- open Pending이 있는 프로젝트는 요청에 포함할 수 없다(서버 차단, 화면에는 사유 표시).
- 요청 batch 생성·재검증·snapshot·workbook·audit는 하나의 트랜잭션이다. Excel 생성 실패는 요청 완료로 기록되지 않는다.
- 같은 프로젝트의 활성 요청은 정확히 1건이다. 재요청은 명시적 사유가 있어야 하고 이전 요청을 `Superseded`로 보존한다. 어떤 요청 row도 삭제하지 않는다.
- 같은 `operationId` 재시도는 같은 결과를 재생한다(payload fingerprint 불일치 시 409).
- workbook은 서버가 authoritative하게 생성하고 저장된 checksum과 byte 단위로 일치해야 재다운로드된다.
- 발행요청은 프로젝트 완료가 아니다. 프로젝트 완료는 기존 `TASK-014A` 계약(모든 활성 패널 납품 + open Pending 0 + 발행일 입력 + 원자 완료)을 그대로 따른다.
- 실제 외부 발송은 없다. 알림이 필요해도 인앱 원본 원칙과 idempotency key를 따른다(이번 MVP는 알림 생성 없음 — 요청자는 본인이므로).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 납품 완료 근거 | `logistics_delivery_results.delivered_at_utc`, Finalized `DeliveryCompleted` batch | 기존 | 변경 없음 (읽기 전용) |
| 프로젝트 정보 | `projects`의 code·title·customer_name·item·delivery_location·sales_amount·currency_code·sales_owner | 기존 | 변경 없음 (snapshot 원본) |
| 발행요청 batch | 기간, 요청자, 생성 시각, 메모, workbook bytes·sha256·행 수·파일명 | 신규 (`sales_billing_request_batches` 후보명) | append-only, 수정·삭제 없음 |
| 발행요청 row | batch별 project snapshot(코드·고객사·납품일·금액·통화 등)과 요청 상태 | 신규 (`sales_billing_request_items` 후보명) | `Requested → Superseded` 전환만 허용, 사유·행위자·시각 기록 |
| 멱등 receipt | operation_id·fingerprint·결과 projection | 신규 (기존 `sales_settlement_operations` 패턴 복제) | append-only |
| 다운로드 이력 | batch 재다운로드 actor·시각 | 신규 최소 audit (batch 하위 이벤트) | append-only |

```text
[프로젝트] 납품 완료(후보) → 요청됨(Requested, batch 소속) → (사유 있는 재요청 시) Superseded + 새 Requested
[batch] 생성(Completed, workbook 포함) — 생성 후 불변, 재다운로드만 허용
```

- 회계 발행 완료 확인은 기존 `sales_settlements` 완료(발행일 입력)로 연결한다. 신규 상태를 만들지 않고, 화면에서 “요청됨 + 발행 확인 대기 → 발행일 입력 시 완료”로 읽히게 한다(권장안 4).

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: scope·`sales.settle`·`Project.SalesAmount.Read`, 납품 완료·open Pending·중복·기간 유효성 재검증, workbook 생성·checksum, 멱등.
- 필요한 조회와 mutation (경로는 조사 대상 후보):
  - `GET /api/sales/billing-requests/candidates?periodStart&periodEnd` — 후보+상태 조회
  - `POST /api/sales/billing-requests` — `{operationId, periodStart, periodEnd, projectIds[], note?, resubmissions?[{projectId, reason}]}`
  - `GET /api/sales/billing-requests` — batch 이력
  - `GET /api/sales/billing-requests/{batchId}/file` — 재다운로드(audited)
  - 기존 `GET /api/projects/{id}/settlement` 응답에 요청 상태 projection 추가
- 권한·validation: 조회·생성 모두 `QmsPolicies.SalesSettle`(기존 `sales.settle`), 생성은 `Project.SalesAmount.Read` 추가 확인. 선택 상한은 기존 선택 export와 동일한 방식의 고정 cap(예: 500)을 둔다.
- transaction·동시성·idempotency: `SalesSettlementStore`의 `for update` lock + receipt(fingerprint) 패턴을 그대로 복제한다. 프로젝트별 활성 요청 partial unique index로 동시 중복을 DB에서 차단하고 unique violation은 409로 변환한다.
- audit trail: batch·row 자체가 append-only 원장이며, 재요청 사유와 다운로드 이력을 추가한다. `data_export_events`(0038)는 `sensitive_sales_amount_included`가 `Projects` kind에만 허용되는 제약이 있으므로 그대로 재사용하지 말고, 이번 migration에서 제약 확장 또는 feature 전용 audit 중 구현 조사 후 결정한다.
- Excel: `ExcelWorkbookBuilder`(ClosedXML, formula-safe `WriteText`, 생성일시·적용 필터 header)와 `ExcelExportConcurrencyGate`를 재사용한다. workbook bytes는 batch에 저장하며 크기 상한(기존 `logistics_evidence` 10MB 패턴 이하)을 둔다.
- 외부 provider 영향: 없음. 실제 발송 없음.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: `App.tsx`에 `sales-billing` view와 `/sales/billing-requests` route 추가, `영업` 메뉴 안에서 `DsTabs`로 `연간 KPI | 발행요청` 전환(기존 `sales-kpi` view 보존). 신규 `SalesBillingRequestPage` component (기존 `SalesKpiPage`·`SalesSettlementPage`·선택 export tray 패턴 재사용).
- loading/empty/error/success: 후보 없음·이미 모두 요청함·선택 없음·생성 중·재다운로드·stale(409 → 재조회) 상태를 기존 구조화 action feedback(A1/A2 패턴)으로 표시.
- 공통 Action Feedback: 성공 시 “회계팀 발행요청 자료를 생성했습니다” + 파일 다운로드, 부분 실패 없음(원자 계약).
- 접근성: checkbox·버튼 label, 오류 focus 이동(기존 `feedbackRef` 패턴), 상태는 색+텍스트 병행.
- 390px/mobile/narrow pane: 한 열 카드, 현재 기간·미요청 건수·선택 다운로드 중심. 과거 batch 상세·기간 수동 보정은 PC 우선. page-level horizontal overflow 금지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 상세 영업 탭·정산 화면에 요청 상태 표시. 내 업무·workflow stage는 변경하지 않는다(18단계 구조 유지).
- 권한/관리자: 신규 permission 없음. 기존 `sales.settle`·`Project.SalesAmount.Read`·project scope 재사용.
- Excel/PDF/첨부: `ExcelWorkbookBuilder`·concurrency gate·formula-safe 규칙 재사용. 선택 export registry(20개 화면)에는 추가하지 않는다 — 이 기능은 조회 export가 아니라 업무 요청 기록이다.
- Teams/Mail: 없음.
- 삭제·복구/감사: 프로젝트 soft-delete 시 후보 제외(기존 `deleted_at_utc` 필터). batch·row는 append-only.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 서버 저장 batch: 생성 트랜잭션에서 재검증+snapshot+workbook 저장, 이후 재다운로드 | 감사·재다운로드·byte 동일성 보장, 멱등 단순, “생성 실패=요청 없음” 계약 명확 | workbook bytea 저장 용량 (상한과 반월 주기 특성상 낮음) |
| B | stateless export: 다운로드만 하고 요청 기록 없음 | 구현 최소 | 요청 이력·중복 방지·재다운로드 불가 — 인터뷰 요구 미충족 |
| C | 요청 기록 + workbook 재생성(스냅샷 기반 on-demand) | 저장 공간 절약 | 코드 변경 시 과거 파일 byte 재현 불가, checksum 계약 약화 |
| D | 회계팀 계정 workflow 포함 | 완결된 흐름 | 명시적 범위 밖 (인터뷰 제외 항목) |

권장안은 A다. 근거: 기존 `sales_settlement_operations` receipt·`logistics_evidence` bytea 패턴이 이미 있고, 인터뷰의 “batch 생성 후 재다운로드 가능한 2단계 계약 권장”과 일치한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated existing/fresh DB만 사용.
- migration 필요 여부: 있음 — `0045` 다음 additive migration 1개(`0046` 후보). 기존 테이블 변경 없음, 신규 테이블·index·(필요 시) export audit 제약 확장만.
- 외부 발송/실제 데이터 영향: 없음. synthetic 데이터로만 검증.
- runtime 교체 여부: 없음. 5174/5081 Development 규칙 유지.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo·Persistent UAT 반영(기존 `main` merge 승인 0/3 유지).

## 14. 검증 계획

- 최소 테스트: Backend — 반월 기간 계산(Seoul 경계·월말), 후보 판정(미납품·부분 납품·open Pending·soft-delete 제외), 권한(금액 권한 없는 생성 차단, scope 밖 422/404), 멱등 replay·fingerprint 409, 동시 중복 unique→409, 재요청 사유·Superseded 전환, workbook 열·행·checksum. Frontend — 선택·생성·오류 focus·상태 렌더링, 금액 비노출.
- 영향 영역 회귀: `TASK-014A` 정산 완료 계약, Sales KPI 집계, 물류 납품 판정 관련 기존 Backend/Frontend 전체 테스트(현재 기준선 Backend 403·Frontend 109)와 disposable Full-Stack E2E(현 38 시나리오 + 발행요청 1개 추가).
- migration: isolated fresh 전체 적용과 기존 `0045 → 0046` upgrade 검증.
- PR/CI: 해당 없음(local experiment commit만).
- 사용자 검수: desktop 1440·mobile 390 화면과 synthetic workbook(내용·레이아웃) screenshot, `BATCHED_FINAL` 일괄 검수 대기로 기록.

## 15. 완료 기준

- 기능/권한/데이터: 반월 후보 조회→선택→Excel 생성→이력·재다운로드가 서버 재검증·멱등·중복 차단과 함께 동작하고, workbook 수치가 화면 요약과 일치한다.
- UX: 390px 핵심 흐름 horizontal overflow 0, 요청/발행 확인 문구가 실제 책임과 일치한다.
- 자동 테스트: Backend/Frontend/migration/E2E 통과, Open P0/P1/P2 = 0.
- 5종 산출물: interview·planning(1·2차)·review·change·implementation report 상태와 위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: N/A (게시 미승인, local commit만).

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 권장안 (Repository 근거) | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 기간 경계·수동 변경 | 고정 반월만 / 자유 기간 / 반월 기본+제한적 수동 | 반월 기본값(Seoul, 1일→직전월 16일~말일, 16일→당월 1~15일) + 수동 기간 허용(시작≤종료≤오늘, 최대 92일) — 누락 보정 가능하고 서버 검증 단순 | Fable 권장안 자동 채택 (실험 standing instruction) |
| 2 | 생성·요청 원자성 | 다운로드만 / commit 후 재다운로드 2단계 | 후보 A: 단일 트랜잭션 batch+workbook 저장, 재다운로드 분리 | 동일 |
| 3 | 중복·재요청 | 완전 금지 / 무제한 / 사유 있는 revision | 프로젝트별 활성 요청 1건 + 사유 필수 재요청·`Superseded` 보존 | 동일 |
| 4 | final 완료 의미 | 요청 시 완료 / 회계 확인 시 완료 / 기존 유지+문구 조정 | 기존 `TASK-014A` 완료 계약 유지, 문구·상태 표시만 “요청→발행 확인→완료”로 조정 — KPI·완료 fence·18단계와 무충돌 최소안 | 동일 |
| 5 | Excel 열·누락 회계 필드 | 현재 data만 / 빈 기입란 추가 / 마스터 신설 | 존재 근거 열(코드·이름·고객사·Item·납품처·최종 납품일·패널 수·공급가액·통화·영업담당·메모) + 회계팀 기입용 빈 열(발행일·번호) 2개. 사업자번호·세율·부가세는 열 미생성으로 명시 | 동일 |

standing instruction상 위 5건은 비차단이며 Codex review를 거쳐 2차 기획에서 확정한다. blocking decision은 0건이다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Sales/` 신규 billing request store·contracts·endpoints, `Program`/DI 등록, 정산 detail projection 확장
- Frontend: `frontend/src/App.tsx` route·nav, 신규 `SalesBillingRequestPage`, `SalesSettlementPage` 문구·상태, `api.ts`·type 파일, `styles.css`
- DB/Migration: `database/migrations/0046_*.sql` (additive)
- Tests/Scripts: Backend API·migration tests, Frontend tests, Full-Stack E2E 시나리오·fixture, screenshot script
- Docs: Roadmap 실험 상태 추기, 완료 원장, Task 산출물 (`tasks/billing-request-001-*`)

## 18. Roadmap 연결

- 선행 Task: `TASK-013A`(납품 근거), `TASK-014A`(정산·완료 계약), `TASK-EXPORT-001`(workbook·audit 패턴), `TASK-SALES-KPI-001`(확정 매출 집계) — 모두 `EXPERIMENT_COMPLETE`.
- 후속 Task: 회계팀 계정 workflow·수정세금계산서(NEW_FEATURE), 회계/ERP 연동(운영 전환 이후), 사업자등록정보 마스터.
- 현재 Go/No-Go: Roadmap canonical `Next Gate`(`TASK-QR-001`)와 다르나 interview의 Task Identity Gate에 `explicitRoadmapOverrideApproved: true`·`PASS_CREATE`가 기록되어 있다. 실험 계보 내 Go.
- 별도 Task로 분리할 항목: 회계 발행 완료의 시스템 내 확인 workflow, 발행요청 자료의 메일 자동 발송.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | 영업은 회계팀에 발행을 요청. 1일/16일 반월 선택 Excel 즉시 생성. 비차단 선택은 Fable 권장안 자동 채택 | 본 기획 전체. interview `COMPLETED_CONFIRMED` |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

Codex review와 Fable 2차 기획 확정 뒤의 구현 세션을 위한 초안이다. 승인 전 실행하지 않는다.

1. `experiment/*` 현재 branch에서 instruction chain gate를 다시 수행하고 이 planning·review·2차 기획을 source of truth로 읽는다.
2. additive migration(`0046` 후보)을 작성해 isolated fresh 전체 적용과 `0045 → 0046` upgrade를 검증한다. 기존 migration을 수정하지 않는다.
3. Backend: billing request store를 `SalesSettlementStore`의 lock·receipt·fingerprint 패턴과 `ExcelWorkbookBuilder`·concurrency gate 재사용으로 구현한다. 모든 검증은 서버에서 재수행하고 unique violation은 409로 변환한다.
4. Frontend: `영업` 탭 구조에 발행요청 화면을 추가하고 정산 화면 문구를 조정한다. 기존 DESIGN-000 token·공통 component만 사용한다.
5. 테스트: 14절의 최소·회귀·E2E를 실행하고 실패를 성공으로 기록하지 않는다.
6. 증빙: desktop 1440·mobile 390 screenshot과 synthetic workbook 검증을 privacy-safe로 기록하고 implementation report·완료 원장·Roadmap 실험 상태를 갱신한다.
7. local commit까지만 수행한다. push·PR·merge·Persistent UAT·실제 provider는 범위 밖이다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
