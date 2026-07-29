# TASK-BILLING-REQUEST-001 구현 보고서

## 1. 상태와 경계

- Task 유형: `NEW_FEATURE` → experiment Fable 2-pass fast-track → Codex 구현
- Branch: `experiment/task-home-002-personalized-shell`
- 최종 기획 source: `docs/33-billing-request-plan.md`
- 구현 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- main merge 승인: `0/3`
- 제외: 회계팀 계정 workflow, 국세청·ERP, 실제 메일·Teams 발송, Persistent UAT, push·PR·merge

## 2. 사용자 결과

영업 담당자는 `영업 → 세금계산서 발행요청`에서 기간 내 출하 완료 프로젝트를 조회하고 checkbox로 필요한 프로젝트만 선택한다. `선택 N개 Excel 만들기`를 누르면 회계팀 전달용 요청 batch와 동일 byte workbook이 함께 저장되며, 이후 같은 요청을 재다운로드할 수 있다.

최종 영업 화면은 세금계산서를 직접 발행하는 화면이 아니다. 회계팀 발행요청이 존재해야 하고, 회계팀이 회신한 발행일·세금계산서 번호·메모를 입력한 뒤에만 프로젝트를 완료한다.

## 3. 구현 계약

- 서울 기준 추천 기간: 1일에는 직전월 16일~말일, 16일에는 당월 1~15일.
- 후보 근거: 모든 활성 패널의 `DepartureProcessed` 확정과 프로젝트 최종 출발일.
- open Pending, 미완료 출하, 이미 활성 요청에 포함된 프로젝트는 서버에서 차단.
- 1~500개 선택, operationId 멱등, actor+fingerprint replay, 다른 payload 재사용 409.
- batch, project snapshot, workbook bytea, SHA-256을 한 transaction으로 저장.
- formula-like text는 안전한 text로 기록하며 금액·날짜·개수는 typed cell format 사용.
- 정산 완료 전 해당 프로젝트의 활성 발행요청 존재를 서버에서 재검증.

## 4. 주요 변경

### Backend·DB

- `database/migrations/0046_sales_billing_requests.sql`
- `SalesBillingRequestContracts`, `SalesBillingRequestStore`, `SalesBillingRequestEndpointExtensions`
- `GET /api/sales/billing-requests/candidates`
- `GET /api/sales/billing-requests`
- `POST /api/sales/billing-requests`
- `GET /api/sales/billing-requests/{batchId}/file`
- `SalesSettlementStore` 발행요청 gate·상태 projection

### Frontend

- `/sales/billing-requests` route와 영업 navigation
- 추천/수동 기간, 후보·차단 사유, 전체선택·개별 선택, 메모, 선택 Excel, 요청 이력
- 영업 KPI에서 발행요청 화면 진입
- 정산 화면의 회계 발행요청 상태·회계팀 발행 확인 문구

## 5. 검증

| 검증 | 결과 |
| --- | --- |
| Backend Release build | PASS, warning 0/error 0 |
| Backend 전체 test | `403/403 PASS` |
| Migration | fresh `0046` 적용과 latest assertion PASS |
| Frontend unit | `109/109 PASS` |
| TypeScript·production build | PASS |
| 실제 역할 lifecycle | 출하 후보 선택→Excel→요청 상태→회계 확인→18/18 완료 `1/1 PASS` |
| Workbook integrity | ZIP/XML 정상, `발행요청!A1:O6`, filter·freeze·typed 금액 확인 |

최종 synthetic workbook SHA-256: `4b4a8def002e413e6910fa0f610a3d21b405238a1c6be88799973eafb592efed`.

## 6. 사용자 매뉴얼

1. 영업 메뉴에서 `세금계산서 발행요청`을 연다.
2. 1일·16일 추천 기간을 확인하거나 desktop에서 기간을 조정하고 `조회`를 누른다.
3. 회계팀에 요청할 출하 프로젝트 checkbox를 선택한다. 목록 전체가 필요하면 `전체선택` checkbox 하나만 사용한다.
4. 참고 메모를 입력하고 `선택 N개 Excel 만들기`를 누른다.
5. 생성된 Excel을 회계팀에 전달한다. 회계팀이 `발행일`, `세금계산서 번호`를 기입할 수 있다.
6. 회계 발행 회신 뒤 프로젝트 정산 화면에서 발행 확인 정보를 저장하고 최종 완료한다.

## 7. Finding·rollback

- Open P0/P1/P2: `0/0/0`.
- P3: workbook은 15열이라 기본 Letter 인쇄 설정에서는 가로 3페이지로 나뉜다. Excel 일반 화면과 데이터 사용에는 문제가 없으며, 회계팀이 인쇄 중심으로 사용할 경우 landscape/fit-to-width preset을 후속 change로 검토한다.
- Rollback은 신규 route/API/DI와 migration `0046` 이후 기능 사용을 되돌린다. 이미 적용한 additive schema를 destructive drop하지 않는다.
- 사용자 직접 검수는 마지막 일괄 검수 대기다. 대표 repo·`main`·Persistent UAT·실제 provider는 변경하지 않았다.
