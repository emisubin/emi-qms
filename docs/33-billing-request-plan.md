# TASK-BILLING-REQUEST-001 — 세금계산서 발행요청 자료 2차 기획 (최종 구현 계약)

> 상태: Review resolution을 반영한 2차 기획 전문
> 목적: Codex 구현·검증 세션이 그대로 실행할 수 있는 최종 구현 source of truth
> 이 문서 하나로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 이해할 수 있어야 한다.

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-BILLING-REQUEST-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/billing-request-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/billing-request-001-planning.md` (판단 이력으로 보존, 수정 없음)
- codexReviewSource: `tasks/billing-request-001-review.md` (판단 이력으로 보존, 수정 없음)
- approvalChangeSource: `tasks/billing-request-001-change-001.md` (`fableSecondPlanningApproved: true`)

이 문서는 1차 기획의 유지 권고를 보존하고 Codex review의 추가·보류·제거 resolution을 모두 반영한 최종 구현 계약이다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 복사하지 않는다. 이 문서는 main merge, push·PR, Persistent UAT, 실제 provider, 게시·사용자 최종 검수 승인을 부여하지 않는다.

## 0. Review resolution 반영 결과

Codex review의 모든 판정을 다음과 같이 계약에 반영했다. 이 표가 1차 기획과 이 문서의 차이 전부다.

| Review 판정 | 항목 | 이 계약의 확정 내용 |
| --- | --- | --- |
| 유지 | Seoul 반월 추천 기간 | 1일~15일 실행 → 직전 월 16일~말일, 16일~말일 실행 → 당월 1일~15일. 다른 날짜 접근도 가장 최근 마감 구간을 기본값으로 제시 |
| 보류→축소 | 수동 기간 최대 92일 | desktop만 시작≤종료≤오늘(Seoul)·최대 31일 보정 허용. mobile은 추천 반월 기간 고정 |
| 제거·교체 | `DeliveryCompleted` 기간 후보 | 모든 active panel이 Finalized `DepartureProcessed` batch에 속하고 프로젝트의 마지막 출발일이 기간 안인 **출하 완료** 후보로 교체 |
| 유지 | open Pending 차단 | open Pending > 0 프로젝트는 요청 불가, 후보에 사유 표시 |
| 유지 | 전체/개별 선택, batch+snapshot+workbook 단일 트랜잭션, bytea+sha256, project별 활성 요청 1건, 요청 이력·재다운로드, 프로젝트 영업 탭·정산 상태 projection | 1차 기획 내용 그대로 (본문 각 절) |
| 보류→제외 | 사유 있는 revision·`Superseded` | 이번 MVP에서 제거. 이미 요청된 프로젝트는 다시 선택할 수 없다. 취소·정정은 후속 POLICY/NEW_FEATURE |
| 추가 | 출하 근거 snapshot | batch item에 최초 출발일·최종 출발일·활성 패널 수·출발 완료 패널 수 snapshot, 생성 직전 서버 재확인 |
| 추가 | 회계팀 기입란 | 빈 `발행일`·`세금계산서 번호` 열을 header note `회계팀 기입란`으로 표시. `사업자번호`·`세율`·`공급가/부가세 분리`는 생성하지 않음 |
| 추가 | 완료 의미·문구 | 발행요청 ≠ 프로젝트 완료. 기존 발행일 입력의 label을 “회계팀 발행 확인일”로, 완료 버튼을 “회계 발행 확인 후 프로젝트 완료”로 교정. `TASK-014A` 완료 계약 구조는 불변 |
| 유지 | 권한 | 조회·생성·이력·다운로드는 `sales.settle`. workbook 생성·다운로드는 `Project.SalesAmount.Read` 추가 필수. 프로젝트 상세의 요청 여부·요청일은 기존 project read scope (금액 비노출) |
| 보류 | 실제 알림·메일 첨부, 회계팀 workflow, export registry 확장 | 모두 제외 유지. 이 workbook은 조회 export가 아니라 audit를 남기는 도메인 command 결과 |

## 1. 한 줄 목표

영업 담당자가 매월 1일·16일 반월 기간에 출하 완료된 프로젝트를 선택해 회계팀 전달용 세금계산서 발행요청 `.xlsx`를 한 번의 행동으로 생성·기록하고, 이미 요청한 프로젝트를 구분하며, 같은 batch를 언제든 동일 byte로 재다운로드할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 영업의 실제 책임은 세금계산서 직접 발행이 아니라 회계팀 발행요청 자료 작성·전달이다. 현재 정산 화면은 영업이 발행일·번호를 직접 입력해 완료하는 것처럼 보인다.
- 매월 1일·16일 마다 해당 기간에 출하된 프로젝트를 화면별로 수기로 찾고, 이미 요청한 건을 수기로 구분하며, 회계팀 전달 열을 Excel로 재가공한다. 여기서 누락·중복·금액 오류가 발생한다.
- 기존 선택 export(`TASK-EXPORT-001/002`)는 반월 기간 판정, 출하 완료 판정, 요청 이력, 중복 방지가 없어 우회 수단에 그친다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 정·부 담당 | 후보 조회, 선택, Excel 생성, 이력 확인, 재다운로드 | 기존 project access scope + `sales.settle` | 접근 가능한 출하 완료 프로젝트의 요청 batch 생성 |
| 영업 부서장 | 팀 요청 현황 확인, 기간 보정 조회로 누락 확인 | 기존 영업 project scope | 같은 기능 (신규 권한 없음) |
| System Administrator | 운영 지원, 재다운로드 | 전체 (기존 권한) | 재다운로드만 (업무 입력 우회 없음) |
| 회계팀 | 생성된 Excel을 시스템 밖에서 수신 | 시스템 계정 없음 (범위 밖) | 없음 |
| 다른 부서 | 프로젝트 상세 영업 탭에서 요청 상태 조회 | 기존 project read scope | 없음. `Project.SalesAmount.Read` 없으면 금액 비노출 |

- candidate/history/create/download endpoint는 모두 기존 `QmsPolicies.SalesSettle`을 요구한다.
- workbook에는 판매금액이 업무상 필수이므로 생성과 파일 다운로드는 `QmsPolicies.ProjectSalesAmountRead`를 추가로 요구한다. 금액 열 생략 대신 생성 자체를 차단한다.
- 신규 permission은 만들지 않는다. Backend가 권한·scope·출하·Pending·중복의 authoritative layer이며 UI 숨김은 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 16일 정기 발행요청

1. 영업 담당자가 16일 오전 `영업 > 발행요청` 화면을 연다. 시스템이 Seoul 기준 오늘 날짜로 “당월 1일~15일” 추천 기간과 후보·미요청 건수를 보여 준다.
2. 출하 완료 후보 목록에서 전체선택 후 제외할 프로젝트만 해제하고 `회계팀 발행요청 자료 생성`을 누른다.
3. 서버가 scope·출하 완료·open Pending·중복·금액 권한을 재검증하고 batch·item snapshot·workbook을 한 트랜잭션으로 기록한 뒤 `.xlsx`를 내려준다. 해당 프로젝트는 `요청됨`으로 바뀌고 요청 이력에 batch가 추가된다.

### 시나리오 B — 다운로드 실패 후 재다운로드

1. batch 생성 직후 네트워크 문제로 파일 저장에 실패한다.
2. 요청 이력에서 같은 batch의 `다시 다운로드`를 누른다.
3. 서버가 저장된 workbook bytes를 동일 sha256으로 다시 내려주고 다운로드 이력을 남긴다. 새 batch는 생기지 않는다.

### 시나리오 C — 기간 보정으로 누락 확인 (desktop)

1. 부서장이 지난 반월에 빠진 프로젝트를 확인하려고 desktop에서 기간을 수동으로 조정한다(시작≤종료≤오늘, 최대 31일).
2. 미요청 출하 완료 프로젝트는 선택해 새 batch로 요청할 수 있다.
3. 이미 `요청됨`인 프로젝트는 재선택할 수 없고 소속 batch·요청일이 표시된다. 잘못된 요청의 취소·정정은 이번 범위 밖임을 화면 문구로 안내하지 않고 단순히 선택 불가로 처리한다.

### 시나리오 D — 요청 상태 조회

1. 다른 부서 사용자가 프로젝트 상세의 영업 탭을 연다.
2. 시스템이 발행요청 상태(`미요청`/`요청됨`(요청일)/`회계 발행 확인 완료`)를 표시한다. 금액 권한이 없으면 금액은 보이지 않는다.
3. 영업은 같은 흐름에서 “회계팀 발행 확인일 입력 → 프로젝트 완료”라는 다음 행동을 인지한다.

## 5. 기능 요구사항

### 필수

- [ ] Seoul 기준 반월 추천 기간 계산: 실행일 1일~15일이면 직전 월 16일~말일, 16일~말일이면 당월 1일~15일을 기본값으로 제시
- [ ] 출하 완료 후보 조회: soft-delete되지 않은 프로젝트 중 활성 패널이 1개 이상이고, **모든 활성 패널이 Finalized `DepartureProcessed` batch의 active membership으로 출발 처리 완료**되었으며, 프로젝트의 **마지막 출발일(Seoul)** 이 조회 기간 안에 있는 프로젝트
- [ ] 여러 출발 batch로 나뉜 프로젝트는 최초 출발일·최종 출발일을 모두 집계하고 최종 출발일을 기간 판정 기준으로 사용
- [ ] 후보별 표시: 프로젝트 코드·이름·고객사·최초/최종 출발일·활성 패널 수·출발 완료 패널 수·open Pending 수·요청 상태·(권한 시) 공급가액/통화
- [ ] checkbox 전체선택·개별선택과 선택 요약 tray (기존 선택 export 패턴 재사용), 선택 상한 500
- [ ] 요청 batch 생성: 서버 재검증(선택 0건, 출하 미완료, scope 밖, 금액 권한 없음, open Pending>0, 이미 요청됨, 기간 무효, stale candidate) 후 batch·item snapshot·workbook·audit를 단일 트랜잭션으로 기록
- [ ] `operationId` 기반 멱등: 같은 operation 재시도는 같은 batch·파일을 재생하고 새 batch를 만들지 않음. payload fingerprint 불일치는 409 (기존 `sales_settlement_operations` receipt 패턴 복제)
- [ ] 프로젝트별 요청 정확히 1건: 요청된 프로젝트는 재선택 불가. DB unique index로 동시 중복을 차단하고 unique violation은 409로 변환
- [ ] 서버 생성 `.xlsx`: 존재 근거 열 + `회계팀 기입란` 빈 2열, formula-safe text, bytes·sha256 저장, 재다운로드 byte 동일
- [ ] 요청 이력 목록: batch별 기간·건수·생성자·생성일·재다운로드 (다운로드 이력 audit)
- [ ] 프로젝트 상세 영업 탭·정산 화면에 요청 상태(`미요청`/`요청됨`/`회계 발행 확인 완료`) 표시
- [ ] 정산 화면 문구 교정: 발행일 입력 label·도움말을 “회계팀 발행 확인일”로, 완료 행동을 “회계 발행 확인 후 프로젝트 완료”로 — `TASK-014A` 완료 계약의 구조·검증은 변경하지 않음
- [ ] desktop 1440 / mobile 390px adaptive UI, 390px 핵심 흐름 page-level horizontal overflow 0

### 선택

- [ ] batch 요청 메모(회계팀 전달 사항, 500자 이하) — workbook header 영역에 표기
- [ ] 후보 검색/고객사 filter (desktop)

### 명시적 제외

- [ ] 사유 있는 재요청 revision·`Superseded`·요청 취소·정정 (후속 POLICY/NEW_FEATURE)
- [ ] 실제 회계·ERP·국세청 전자발행 API, 이메일·Teams 첨부 발송, 인앱 알림 신규 생성
- [ ] 회계팀 계정의 발행 완료 workflow와 수정세금계산서
- [ ] 사업자등록정보 마스터, 세율·부가세 계산, 수금·채권·원가
- [ ] 선택 export registry(범용 export 화면) 확장
- [ ] `TASK-014A` 완료 계약(모든 활성 패널 납품 + open Pending 0 + 발행일 입력 + 원자 완료)의 구조 변경
- [ ] 대표 repo·`main`·Persistent UAT·push·PR·merge·실제 provider

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 발행요청 (desktop) | `영업` 메뉴에서 `DsTabs`로 `연간 KPI | 발행요청` 전환, `/sales/billing-requests` | 추천 기간, 요약 KPI(후보/미요청/요청됨), 후보 table(코드·이름·고객사·최초/최종 출발일·패널·Pending·상태·금액), 요청 이력 | 기간 보정(≤31일), 검색, 전체/개별 선택, 자료 생성, 재다운로드 | 생성 성공 시 다운로드+상태 갱신, 검증 실패는 action 근처 구조화 오류(기존 action feedback 패턴), 409는 목록 재조회 유도 |
| 발행요청 (mobile 390px) | 동일 URL adaptive | 추천 반월 기간(고정), 미요청 건수, 프로젝트 핵심 정보 카드 한 열 | 선택, 자료 생성, 최근 batch 재다운로드 | 동일 feedback. 기간 보정·과거 batch 상세 관리는 PC 우선 |
| 프로젝트 상세 영업 탭 / 정산 화면 | 기존 경로 | 발행요청 상태·요청일, “요청 → 회계 발행 확인 → 완료” 진행 문구 | 상태 확인, 정산 화면 이동 | 기존 패턴 유지 |

- 상태 표시: 후보 없음 / 이미 모두 요청함 / 선택 없음 / 생성 중 / stale candidate(409) / 재다운로드를 행동 근처에 표시한다.
- 문구: `세금계산서 발행`이 아니라 `회계팀 발행 요청`, 완료는 `회계 발행 확인 후 프로젝트 완료`.
- WITHUS 기반 DESIGN-000 token과 `DsPageHeader`·`DsSurface`·`DsToolbar`·`DsTabs`·`DsBadge` 공통 component, 얇은 divider·절제된 shadow·compact controls·blue accent를 유지한다.
- 금액 권한 없는 사용자에게는 서버 응답에서 금액을 제외하고 열·합계를 렌더링하지 않는다.
- 접근성: checkbox·버튼 label, 오류 focus 이동(기존 `feedbackRef` 패턴), 상태는 색+텍스트 병행.

## 7. 업무 규칙과 불변조건

- 출하 완료 전 프로젝트는 후보가 아니다. 판정 근거는 Finalized `DepartureProcessed` batch의 active membership과 `logistics_batches.departure_date`이며, 생성 트랜잭션에서 서버가 다시 확인한다.
- 납품 완료(`DeliveryCompleted`)와 발행 확인은 기존 `TASK-014A` 최종 완료 조건으로 유지하고 이번 후보 판정에 사용하지 않는다.
- open Pending이 있는 프로젝트는 요청에 포함할 수 없다(서버 차단, 화면 사유 표시).
- batch 생성·재검증·item snapshot·workbook 생성·audit는 하나의 트랜잭션이다. workbook 생성 실패는 요청 완료로 기록되지 않는다.
- 프로젝트별 요청은 정확히 1건이다. 이번 MVP에는 재요청·취소·`Superseded`가 없으며 어떤 요청 row도 수정·삭제하지 않는다(append-only).
- 같은 `operationId` 재시도는 같은 결과를 재생한다. fingerprint(기간·정렬된 projectIds·메모) 불일치는 409다.
- workbook은 서버가 authoritative하게 생성하고 저장 sha256과 byte 단위로 일치해야 재다운로드된다.
- 발행요청은 프로젝트 완료가 아니다. 프로젝트 완료는 기존 `TASK-014A` 계약을 그대로 따른다.
- 실제 외부 발송·인앱 알림 신규 생성은 없다(요청자가 곧 수신자이므로 이번 범위에서 알림 불필요).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 출하 완료 근거 | Finalized `DepartureProcessed` `logistics_batches`·active batch units·`departure_date` | 기존 | 읽기 전용 |
| 프로젝트 정보 | `projects`의 code·title·customer_name·item·delivery_location·sales_amount·currency_code, `project_assignees`의 영업 정담당 | 기존 | 읽기 전용 (snapshot 원본) |
| 발행요청 batch | 기간, 요청자, 생성 시각, 메모, workbook bytes·sha256·행 수·파일명 | 신규 (`sales_billing_request_batches` 후보명) | append-only, 수정·삭제 없음 |
| 발행요청 item | batch별 project snapshot: 코드·이름·고객사·Item·납품처·최초/최종 출발일·활성 패널 수·출발 완료 패널 수·공급가액·통화·영업담당 표시명 | 신규 (`sales_billing_request_items` 후보명), project별 unique index | append-only, 상태 전이 없음 |
| 멱등 receipt | operation_id·actor·payload fingerprint·결과 projection | 신규 (`sales_settlement_operations` 패턴 복제) | append-only |
| 다운로드 이력 | batch 재다운로드 actor·시각 | 신규 feature 전용 audit (batch 하위 이벤트) | append-only |

```text
[프로젝트] 출하 완료(후보) → 요청됨(batch item 소속, 이후 불변)
[정산 표시] 미요청 → 요청됨 → 회계 발행 확인 완료(기존 settlement Completed)
[batch] 생성(Completed, workbook 포함) — 생성 후 불변, 재다운로드만 허용
```

- `회계 발행 확인 완료`는 신규 상태가 아니라 기존 `sales_settlements` 완료(발행 확인일 입력)의 projection이다. 신규 상태·테이블 연결을 만들지 않는다.
- 범용 `data_export_events`(0038)는 확장하지 않는다. 이 workbook은 도메인 command 결과이므로 batch·item·download 이력이 feature 전용 audit 원장이다.

### Workbook 열 계약

1. 요청번호(batch 내 순번), 2. 프로젝트 코드, 3. 프로젝트명, 4. 고객사, 5. Item, 6. 납품처, 7. 최초 출발일, 8. 최종 출발일, 9. 활성 패널 수, 10. 출발 완료 패널 수, 11. 공급가액, 12. 통화, 13. 영업담당, 14. (빈) 발행일, 15. (빈) 세금계산서 번호.

- 14·15열은 header note로 `회계팀 기입란`임을 표시해 system fact처럼 보이지 않게 한다.
- `사업자번호`·`세율`·`공급가/부가세 분리` 열은 source가 없으므로 생성하지 않는다.
- header 영역에 생성일시·요청 기간·요청자 표시명·요청 메모를 기존 `ExcelWorkbookBuilder` header 패턴으로 기록하고, 모든 텍스트는 formula-safe `WriteText`를 사용한다.

## 9. API·Backend 계약

- endpoint (모두 `QmsPolicies.SalesSettle`, 생성·파일은 `QmsPolicies.ProjectSalesAmountRead` 추가):
  - `GET /api/sales/billing-requests/candidates?periodStart&periodEnd` — 후보+요청 상태. 금액 권한이 없으면 금액 field 제외
  - `POST /api/sales/billing-requests` — `{operationId, periodStart, periodEnd, projectIds[], note?}` → batch projection 반환, 이어서 파일 endpoint로 다운로드
  - `GET /api/sales/billing-requests` — batch 이력 (기간·건수·생성자·생성일)
  - `GET /api/sales/billing-requests/{batchId}/file` — 저장 bytes 재다운로드 (audited)
  - 기존 정산 detail 응답(`GET /api/projects/{id}/settlement`)에 요청 상태 projection 추가
- validation: 기간 형식·시작≤종료≤오늘(Seoul)·최대 31일, 선택 1~500건, scope·출하 완료·open Pending·중복·금액 권한 재검증. stale candidate(생성 시점 재검증 실패)는 실패 프로젝트 식별 정보와 함께 409/422 구조화 오류로 응답하고 부분 생성하지 않는다.
- transaction·동시성·idempotency: `SalesSettlementStore`의 project `for update` lock + operation receipt(fingerprint) 패턴을 복제한다. item의 project unique index로 동시 중복을 DB에서 차단하고 unique violation은 409로 변환한다.
- Excel: `ExcelWorkbookBuilder`(ClosedXML)와 `ExcelExportConcurrencyGate`를 재사용한다. workbook bytes는 batch에 저장하고 10MB 상한(기존 `logistics_evidence` 패턴 이하)을 둔다.
- migration: 최신 `0045` 다음 additive `0046` 1개. 신규 테이블·index만 추가하고 기존 테이블·migration을 수정하지 않는다.
- 외부 provider 영향: 없음.

내부 클래스·컬럼 최종 명명은 구현 세션이 기존 Sales 영역 convention에 맞춰 확정하되, 이 절의 계약(경로·권한·검증·원자성·멱등·unique)은 변경하지 않는다.

## 10. Frontend 계약

- route/component: `App.tsx`에 `sales-billing` view와 `/sales/billing-requests` route 추가. `영업` 메뉴는 `DsTabs`로 `연간 KPI | 발행요청` 전환(기존 `sales-kpi` view·URL 보존). 신규 `SalesBillingRequestPage`는 기존 `SalesKpiPage`·`SalesSettlementPage`·선택 export tray 패턴을 재사용한다.
- `SalesSettlementPage`: 발행요청 상태 표시와 문구 교정(“회계팀 발행 확인일”, “회계 발행 확인 후 프로젝트 완료”). 완료 로직·검증은 변경하지 않는다.
- loading/empty/error/success: 후보 없음·이미 모두 요청함·선택 없음·생성 중·재다운로드·409 재조회를 기존 구조화 action feedback(A1/A2) 패턴으로 표시한다.
- 성공 feedback: “회계팀 발행요청 자료를 생성했습니다” + 파일 다운로드. 부분 성공 상태는 없다(원자 계약).
- 390px: 한 열 카드, 추천 기간 고정, 선택·생성·최근 batch 재다운로드 중심. page-level horizontal overflow 금지.
- `api.ts`·type 정의·`styles.css`는 기존 패턴에 추가한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 상세 영업 탭·정산 화면에 상태 projection만 추가. 내 업무·workflow 18단계 구조·stage 전이는 변경하지 않는다.
- 권한/관리자: 신규 permission 없음. `sales.settle`·`Project.SalesAmount.Read`·project scope 재사용.
- Excel/PDF/첨부: `ExcelWorkbookBuilder`·`ExcelExportConcurrencyGate`·formula-safe 규칙 재사용. 선택 export registry에는 추가하지 않는다.
- Teams/Mail: 없음.
- 삭제·복구/감사: soft-delete 프로젝트(`deleted_at_utc`)는 후보 제외. batch·item·receipt·download 이력은 append-only.

## 12. 확정 구현안

1차 기획의 후보 A(서버 저장 batch: 생성 트랜잭션에서 재검증+snapshot+workbook 저장, 이후 재다운로드)를 review가 유지 판정했고 이 계약으로 확정한다. stateless export(B)는 이력·중복 방지 요구 미충족, on-demand 재생성(C)은 byte 재현 불가, 회계팀 workflow 포함(D)은 명시적 범위 밖으로 기각한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated existing/fresh DB만 사용한다.
- migration: additive `0046` 1개. 기존 테이블 변경 없음. rollback은 forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. synthetic 데이터로만 검증한다.
- runtime 교체: 없음. 5174/5081 Development 규칙 유지.
- 승인 경계: local experiment commit까지만(`tasks/billing-request-001-change-001.md`의 `commitApproved: true`). push·PR·merge·대표 repo·Persistent UAT·실제 provider는 미승인이며 `main` merge 승인은 `0/3`이다.

## 14. 검증 계획

- Backend 최소 테스트: 반월 기간 계산(Seoul 경계·월말·윤년), 31일 기간 상한, 후보 판정(출발 미완료·부분 출발·`DeliveryCompleted`와의 경계 혼입 방지·open Pending·soft-delete·scope 제외), 마지막 출발일 집계(다중 batch), 금액 권한 없는 생성/다운로드 차단, 멱등 replay·fingerprint 409, 동시 중복 unique→409, workbook 열 15개·행 수·회계팀 기입란·sha256, 재다운로드 byte 동일, 정산 detail 상태 projection.
- Frontend 최소 테스트: 선택·생성·오류 focus·상태 렌더링(미요청/요청됨/회계 발행 확인 완료), 금액 비노출, 정산 문구 교정.
- 회귀: `TASK-014A` 정산 완료 계약, Sales KPI 집계, 물류 lifecycle 관련 기존 Backend/Frontend 전체 테스트(현재 기준선 Backend 403·Frontend 109)와 disposable Full-Stack E2E(현 38 시나리오 + 발행요청 1개 추가).
- migration: isolated fresh 전체 적용과 `0045 → 0046` upgrade 검증.
- PR/CI: 해당 없음(local experiment commit만).
- 사용자 검수: desktop 1440·mobile 390 화면과 synthetic workbook(내용·레이아웃) privacy-safe screenshot, `사용자 검수 대기 — 마지막 일괄 검수`(`BATCHED_FINAL`)로 기록.

## 15. 완료 기준

- 기능/권한/데이터: 반월 후보 조회→선택→Excel 생성→이력·재다운로드가 서버 재검증·멱등·중복 차단과 함께 동작하고, workbook의 프로젝트 수·금액·출발일·통화가 화면 요약과 일치한다.
- UX: 390px 핵심 흐름 horizontal overflow 0, 요청/발행 확인 문구가 실제 책임과 일치한다.
- 자동 테스트: Backend/Frontend/migration/E2E 통과, Open P0/P1/P2 = 0.
- 5종 산출물: interview·planning(1·2차)·review·change·implementation report의 상태·위치 추적(SOP·manual·checklist는 report 내 섹션 또는 `N/A`+사유).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: N/A (게시 미승인, local commit만).

## 16. 비차단 결정의 최종 확정

Interview의 deferred 5건은 standing instruction(Fable 권장안 자동 채택)과 Codex review resolution으로 다음과 같이 확정되어 미결정이 남지 않는다.

| 번호 | 질문 | 최종 확정 |
| ---: | --- | --- |
| 1 | 기간 경계·수동 변경 | 반월 기본값 + desktop 한정 시작≤종료≤오늘·최대 31일 보정, mobile 고정 |
| 2 | 생성·요청 원자성 | 단일 트랜잭션 batch+workbook 저장, 재다운로드 분리 (확정안 A) |
| 3 | 중복·재요청 | 프로젝트별 요청 1건·재선택 불가. revision·`Superseded`·취소는 MVP 제외, 후속 Task |
| 4 | final 완료 의미 | 기존 `TASK-014A` 완료 계약 유지, label·도움말·버튼 문구만 “요청 → 회계 발행 확인 → 완료”로 교정 |
| 5 | Excel 열·누락 회계 필드 | 8절 15열 계약. 존재 근거만 출력, 회계팀 기입란 빈 2열, 사업자번호·세율·부가세 미생성 |

## 17. 예상 변경 범위

확정 allowlist가 아니라 구현 세션의 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Sales/` 신규 billing request store·contracts·endpoints, DI/route 등록, 정산 detail projection 확장
- Frontend: `frontend/src/App.tsx` route·nav·tabs, 신규 `SalesBillingRequestPage`, `SalesSettlementPage` 문구·상태, `api.ts`·type·`styles.css`
- DB/Migration: `database/migrations/0046_*.sql` (additive)
- Tests/Scripts: Backend API·migration tests, Frontend tests, Full-Stack E2E 시나리오·fixture, screenshot script
- Docs: Roadmap 실험 상태 추기, 실험 완료 원장, `tasks/billing-request-001-*` 산출물

## 18. Roadmap 연결

- 선행 Task: `TASK-013A`(물류 출발·납품 근거), `TASK-014A`(정산·완료 계약), `TASK-EXPORT-001`(workbook 패턴), `TASK-SALES-KPI-001`(영업 화면 구조) — 실험 계보에서 완료.
- 후속 Task: 요청 취소·정정 정책, 회계팀 계정 발행 확인 workflow·수정세금계산서, 회계/ERP 연동, 사업자등록정보 마스터, 발행요청 자료 메일 발송.
- Go/No-Go: Roadmap canonical `Next Gate`(`TASK-QR-001`)와 다르지만 interview Task Identity Gate에 `explicitRoadmapOverrideApproved: true`·`PASS_CREATE`가 기록되어 실험 계보 내 Go.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | 영업은 회계팀에 발행 요청. 1일/16일 출하분 선택 Excel 즉시 생성. 인터뷰·중간 승인 생략, 비차단 선택은 Fable 권장안 자동 채택 | interview `COMPLETED_CONFIRMED`, 1차 기획·review, 본 2차 기획 |
| 2026-07-19 | 2차 기획 target `docs/33-billing-request-plan.md` 승인 (`tasks/billing-request-001-change-001.md`) | 본 문서를 최종 구현 source of truth로 확정 |

## 20. Codex 구현 지시문

blocking decision이 0이므로 change-001의 experiment fast-track 범위에서 구현·검증까지 진행한다.

1. 현재 `experiment/*` branch에서 instruction chain gate를 수행하고 interview·1차 기획·review·change-001과 이 2차 기획을 다시 읽는다. 이 문서가 구현 계약이며 1차 기획·review와 충돌하는 항목은 이 문서를 따른다.
2. additive migration `0046`을 작성해 isolated fresh 전체 적용과 `0045 → 0046` upgrade를 검증한다. 기존 migration을 수정하지 않는다.
3. Backend: `SalesSettlementStore`의 lock·receipt·fingerprint 패턴과 `ExcelWorkbookBuilder`·`ExcelExportConcurrencyGate` 재사용으로 9절 계약을 구현한다. 후보 판정은 `DepartureProcessed` Finalized 근거만 사용하고 모든 검증을 서버에서 재수행하며 unique violation은 409로 변환한다.
4. Frontend: `영업` 탭에 발행요청 화면을 추가하고 정산 문구를 교정한다. DESIGN-000 token·공통 component만 사용한다.
5. 테스트: 14절의 최소·회귀·E2E를 실행하고 실패나 미실행을 성공으로 기록하지 않는다.
6. 증빙: desktop 1440·mobile 390 screenshot과 synthetic workbook 검증을 privacy-safe로 기록하고 implementation report·완료 원장·Roadmap 실험 상태를 갱신한다.
7. local commit까지만 수행한다. push·PR·merge·대표 repo·Persistent UAT·실제 provider는 범위 밖이며 `main` merge 승인은 `0/3`이다.

---

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-BILLING-REQUEST-001`
- authoringModel: `FABLE_5`
- implementationApproved: true (change-001의 experiment 범위, blocking decision 0 조건 충족)
- openBlockingDecisionCount: 0
