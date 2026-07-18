# TASK-014A 영업 정산·세금계산서·프로젝트 완료 Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Task | `TASK-014A` |
| Task 유형 | `APPROVED_FEATURE_IMPLEMENTATION` |
| Branch | `experiment/task-014a-sales-settlement` |
| 구현 기준 | [Fable 2차 기획](../docs/21-sales-settlement-plan.md) |
| 자동 검증 | 완료 |
| 사용자 검수 | `사용자 검수 대기` |
| Local commit | 완료 — 이 보고서를 포함한 local experiment commit |
| Push / PR / Merge | 미승인·미실행 |
| `main` merge 승인 | `0/3` |
| Persistent UAT / 실제 provider | 미승인·미변경 |

이 문서는 코드 구현과 isolated 자동 검증 결과를 기록한다. live UAT 적용·사용자 직접 검수·대표 저장소 반영을 완료로 표현하지 않는다.

## 2. 목적, 배경과 범위

납품이 끝난 프로젝트가 `SalesSettlementCompleted` 내 업무만 남긴 채 영구히 Active인 문제를 닫았다. 영업 담당자는 프로젝트 하위 정산 화면에서 납품·Pending·세금계산서 조건을 확인하고, 세금계산서 발행 정보를 기록한 뒤 정산과 프로젝트를 한 번에 완료할 수 있다.

포함 범위는 다음과 같다.

- project당 정산 record 1건과 `Draft → Completed/Cancelled` forward-only lifecycle
- 세금계산서 발행일 필수, 번호·메모 선택 bounded projection과 version
- active panel 1개 이상, Finalized 납품 relation 전수 충족, project 전체 open Pending 0건 재검증
- project-first lock, operation fingerprint/replay, expectedVersion과 stable 409
- settlement·정산 내 업무·workflow event·project Completed metadata·audit·인앱 알림·operation receipt의 단일 transaction
- `sales.settle` sales-only permission, project scope와 SalesPrimary/SalesSecondary/current work assignee actor 교집합
- generic 내 업무 완료 우회, 완료 후 project edit·면수·상태·삭제와 신규 Pending 생성 차단
- 프로젝트 상세와 내 업무 deep link만 사용하는 `/projects/{projectId}/settlement`
- 모바일 390px 한 열 구성, desktop project-context 2열 구성, 완료 후 동일 route read-only

제외 범위는 전역 정산 menu/queue, 외부 회계·전자세금계산서 연동, invoice 파일/OCR/PDF/Excel, 수금·채권, 완료 뒤 재오픈·수정발행, 외부 provider, Persistent UAT, 대표 저장소·`main` 반영이다.

## 3. Fable 5 기획과 Claude 사용량

Fable은 read-only runner로만 실행했고 1차 원문과 2차 원문은 byte-for-byte artifact로 보존했다. Codex review의 Finding 9건은 2차 기획에서 모두 `RESOLVED_IN_PLAN`으로 통합됐고 `openBlockingDecisionCount`는 `0`이다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 기획 직전 | 0% 사용 / 100% 잔여 / 12:10 KST 초기화 | 0% 사용 / 100% 잔여 / 초기화 parse 불가 | 0% 사용 / 100% 잔여 / 초기화 parse 불가 |
| 1차 기획 직후 | 0% 사용 / 100% 잔여 / 12:10 KST 초기화 | 0% 사용 / 100% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 기획 직전 | 16% 사용 / 84% 잔여 / 12:09 KST 초기화 | 2% 사용 / 98% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 기획 직후 | 16% 사용 / 84% 잔여 / 12:09 KST 초기화 | 2% 사용 / 98% 잔여 / 07-25 07:59 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 |
| 구현 종료 최신 조회 | 30% 사용 / 70% 잔여 / 12:09 KST 초기화 | 3% 사용 / 97% 잔여 / 07-25 07:59 KST 초기화 | 5% 사용 / 95% 잔여 / 07-25 07:59 KST 초기화 |

1차 runner는 `CREATED_FULL_BASELINE`, model 400초, stdout 27,772 bytes, stderr 0이었다. 2차 runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`, model 226초, stdout 23,126 bytes, stderr 0이었다. 종료 최신 조회 첫 시도는 TUI timeout `exit 23`으로 실패했지만 동일 reporter의 1회 재시도는 성공했다. 수치는 추정하지 않았다. Task private Fable session과 transcript는 runner cleanup으로 제거했다.

## 4. 아키텍처와 업무 계약

### Database와 migration

additive migration `0037_sales_settlement_completion.sql`이 `sales_settlements`, append-only `sales_settlement_operations`, project 완료 actor/시각, `sales.settle`을 추가한다. Completed settlement UPDATE/DELETE와 operation receipt UPDATE/DELETE는 trigger로 차단한다. 승인된 project purge는 transaction-local guard 아래 FK 역순 삭제만 허용한다.

모든 `pending_issues` INSERT는 trigger에서 project row를 `FOR UPDATE`로 잠근다. 완료가 먼저면 Pending INSERT가 Completed를 보고 실패하고, Pending이 먼저면 completion이 새 open row를 보고 실패한다. 기존 migration을 수정하거나 번호를 재사용하지 않았고 기존 Completed project를 backfill하지 않았다.

### Backend, API와 권한

project-context API 3종을 추가했다.

- `GET /api/projects/{projectId}/settlement`: project read scope 안에서 조건 aggregate와 draft/completed projection 조회
- `PUT …/settlement/draft`: invoice projection upsert와 version 검사
- `POST …/settlement/complete`: operationId·fingerprint·version·완료 조건을 재검증하고 원자 완료

mutation은 `sales.settle` policy, project scope, SalesPrimary/SalesSecondary/open settlement 업무 assignee 중 하나를 모두 요구한다. 단순 sales owner라는 이유만으로는 허용하지 않는다. System Administrator는 조회만 가능하고 mutation은 endpoint permission에서 403이다. 발행일은 project 생성일 KST date 이상, Asia/Seoul 오늘 이하이고 번호 64자·메모 500자로 제한한다.

납품 완료는 delivery result 존재만 세지 않고 Finalized `DeliveryCompleted` batch, active batch-unit, active packing-unit-panel membership, result의 결합으로 active panel 각각을 판정한다. Pending은 target type과 무관하게 project의 모든 non-Closed row를 센다. API와 알림은 Pending/invoice 원문을 복제하지 않는다.

### 완료 transaction과 lifecycle fence

```text
project row FOR UPDATE
  → settlement/version/replay 확인
  → active panel delivery + project open Pending + invoice date 재검증
  → settlement Completed
  → SalesSettlementCompleted 내 업무 Completed
  → StageCompleted event exactly-once
  → project Completed + actor/시각
  → audit + bounded 인앱 알림 + operation receipt
  → commit
```

generic 내 업무 start/complete/cancel은 전용 정산 route 안내와 409를 반환한다. 프로젝트 완료 뒤 기본정보 수정, 면수 변경, hold/cancel/reactivate, 삭제와 Pending 등록은 서버/DB에서 차단한다. 프로젝트가 정산 전 취소되면 Draft settlement도 Cancelled terminal 상태로 맞춘다.

### Frontend와 UI·UX

전역 메뉴는 만들지 않고 프로젝트 상세 `정산·완료` action과 내 업무 deep link만 추가했다. 화면은 `완료 조건 → 세금계산서 → 최종 확인` 순서다. desktop은 조건 전체 폭 + invoice/final 2열이고, mobile은 같은 정보를 축소 복제하지 않고 390px 한 열로 재구성했다.

납품은 원형, Pending은 각진 정사각형, invoice는 타원·비대칭 둥근 사각형을 사용했다. 핵심 완료 action은 44px 이상 touch target을 유지한다. loading/error/success, 409 후 최신 상태 재조회, 중복 submit 차단, error focus와 `aria-live`, 완료 후 read-only 기록을 구분했다. 좌상단 숨김 메뉴를 유지하고 하단 고정 메뉴는 추가하지 않았다.

### Excel, PDF와 첨부파일 영향

- Excel: `N/A` — 정산 export/import는 범위 밖이다.
- PDF: `N/A` — invoice PDF 생성·업로드를 구현하지 않았다.
- 첨부파일: `N/A` — 세금계산서 파일/OCR은 범위 밖이다.

## 5. 실제 변경 파일

| 영역 | 파일과 역할 |
| --- | --- |
| 기획·판단 | `tasks/014a-interview.md`, `014a-planning.md`, `014a-review.md`, `014a-change-001.md`, `docs/21-sales-settlement-plan.md` |
| Migration | `database/migrations/0037_sales_settlement_completion.sql` |
| Backend 신규 | `backend/src/Emi.Qms.Api/Sales/*` — contracts, endpoints, transactional store |
| Backend 연동 | authorization/identity/`Program.cs`, `PendingStore`, `ProjectStore`, `WorkflowStore` |
| Backend 검증 | `PostgreSqlMigrationTests.cs` — latest ledger, schema, trigger, least privilege |
| Frontend 신규 | `SalesSettlementPage.tsx`, `salesSettlement.ts`, `SalesSettlementPage.test.tsx` |
| Frontend 연동 | `App.tsx`, `api.ts`, `styles.css` — route, deep link, project action, adaptive visual system |
| Full-Stack E2E | `sales-settlement.full-stack.spec.ts` — 실제 HTTP/browser와 disposable PostgreSQL 완료 흐름 |
| 종료 산출물 | `docs/00-product-roadmap.md`, 이 Implementation report |

## 6. 실행한 검증과 결과

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Backend Release 전체 | 적용 | `381/381` 통과 | `dotnet test backend/Emi.Qms.sln --configuration Release --no-restore --blame-hang --blame-hang-timeout 120s` |
| Migration 단독 | 적용 | `1/1` 통과 | sales settlement schema·trigger·sales-only permission |
| Frontend lint | 적용 | 오류 0, 기존 Fast Refresh 경고 1 | `pnpm --dir frontend lint` |
| Frontend typecheck | 적용 | 통과 | `pnpm --dir frontend typecheck` |
| Frontend unit | 적용 | 8 files, `84/84` 통과 | 정산 모바일 조건·최종 확인·Pending 이동 포함 |
| Frontend production build | 적용 | 통과 | 기존 500kB chunk warning 유지 |
| Full-Stack E2E | 적용 | `2/2` 통과 | actual HTTP/browser, disposable PostgreSQL, desktop·390px, 완료 전후·동시 완료·zero panel |
| Visual QA | 적용 | 통과 | desktop, 390px draft, 390px completed; blank=false, overflow=0 |
| CI | 미실행 | `N/A` | push/PR 미승인 local experiment |
| Persistent UAT | 미실행 | `N/A` | 명시적 제외·미승인 |
| 실제 provider | 미실행 | `N/A` | 외부 delivery row 0 검증, 실제 발송 미승인 |

Full-Stack E2E의 초기 두 시도는 test fixture가 Pending Closed constraint 필드와 panel-count concurrency 필드를 덜 채워 실패했고, 제품 코드는 변경하지 않고 fixture를 실제 schema contract에 맞췄다. 세 번째 시도는 관리자에게 없는 Project.Delete를 사용해 403이었고 sales delete로 lifecycle 409를 검증하도록 persona를 바로잡았다. 최종 실행은 전부 통과했다. 전체 Backend suite의 첫 minimal logger 실행은 진행 상황이 없어 6분 40초에 취소했고, individual hang 120초 감시와 normal logger를 켠 동일 전체 suite를 다시 실행해 381건 전부 통과했다.

## 7. Finding과 resolution

| ID | Severity | 상태 | 해소 위치 |
| --- | --- | --- | --- |
| `014A-PENDING-COMPLETION-RACE` | P1 | `RESOLVED` | project-first completion lock + Pending INSERT project lifecycle trigger + E2E 전후 순서 |
| `014A-POST-COMPLETION-MUTATION` | P1 | `RESOLVED` | project edit·면수·상태·삭제·Pending 409/DB fence |
| `014A-DELIVERY-SOURCE-OF-TRUTH` | P2 | `RESOLVED` | Finalized delivery/active membership 결합 집계 |
| `014A-PENDING-SCOPE` | P2 | `RESOLVED` | target type 무관 project 전체 non-Closed aggregate |
| `014A-NAVIGATION-SCOPE` | P2 | `RESOLVED` | project detail + My Work only, global menu/queue 없음 |
| `014A-ADMIN-LEAST-PRIVILEGE` | P2 | `RESOLVED` | sales-only seed, System Administrator mutation 403 |
| `014A-INVOICE-DATE-BOUNDARY` | P2 | `RESOLVED` | project creation KST date ≤ 발행일 ≤ KST today |
| `014A-ZERO-PANEL-VACUOUS-COMPLETION` | P2 | `RESOLVED` | active panel count > 0 별도 완료 조건 |
| `014A-NOTIFICATION-BOUNDARY` | P2 | `RESOLVED` | fixed completion payload·bounded recipients·provider delivery 0 |

Open P0/P1/P2/P3는 `0/0/0/0`이다.

## 8. 개인정보·secret과 artifact 검토

테스트는 고정 dev persona와 synthetic project/invoice 값만 사용했다. 실제 사용자·회사 계정·고객·프로젝트·알림 원문, credential, connection string, token, raw API/DB body를 tracked 문서에 기록하지 않았다. 실제 UAT browser/DB는 사용하지 않았다.

screenshots는 isolated E2E 화면을 `/tmp/task-014a-sales-settlement-*.png`에 생성해 채팅으로만 전달하며 Git staging에서 제외한다. Playwright report/test-results, build output와 process sample도 staging 대상에서 제외한다.

## 9. SOP

### 실험 환경 정산 절차

1. 영업 담당자가 프로젝트 상세의 `정산·완료` 또는 내 업무의 `정산 화면에서 진행`을 연다.
2. 납품 panel 수와 open Pending 건수를 확인한다. 미납품 또는 Pending이 있으면 해당 업무를 먼저 처리한다.
3. 세금계산서 발행일을 입력하고 필요하면 번호·내부 메모를 기록한다.
4. 진행 중에 보존할 필요가 있으면 `임시 저장`한다.
5. 세 조건이 모두 초록색인지 확인하고 `최종 완료 확인`을 누른다.
6. 되돌릴 수 없다는 안내를 읽은 뒤 `정산·프로젝트 완료`를 실행한다.
7. 같은 route의 완료 actor/시각과 read-only invoice projection을 확인한다.

### 장애와 복구

- `409 다른 사용자가 먼저 변경`: 화면이 자동으로 최신 상태를 다시 읽는다. 최신 version을 확인한 뒤 재시도한다.
- `열린 Pending`: `목록 열기`로 project filter Pending을 처리하고 정산 화면을 다시 읽는다.
- `납품 미완료`: 물류 납품 단계의 Finalized relation을 완료한다. DB result를 직접 만들거나 수정하지 않는다.
- `정산 업무 없음`: 내 업무와 물류 인계 상태를 확인한다. domain record를 DB에서 임의 생성하지 않는다.
- 완료 뒤 정정 필요: 이번 기능은 reopen/수정발행을 허용하지 않는다. DB 직접 수정 대신 별도 정책 Task와 additive forward-fix를 사용한다.
- migration 장애: Persistent UAT에 `0037`을 적용하지 않았다. 향후 승인 적용 뒤 결함은 기존 migration 수정이 아닌 다음 additive migration으로 보정한다.

### 운영 적용 전 checklist

- 최신 승인 `main` 기반 branch에서 migration 번호·ledger drift 재확인
- fresh/existing DB apply, race/concurrency, scope·role matrix 전체 재검증
- 완료 뒤 정정·재오픈·수정발행 정책 확정 여부 확인
- Persistent UAT migration·runtime handover 별도 승인
- 사용자 검수와 main merge의 서로 분리된 3회 승인 확인

## 10. User manual

### 완료 조건 읽기

- `납품 2/2`: active panel 중 Finalized 납품이 끝난 수다. 0면 프로젝트를 완료할 수 없다.
- `Pending 0건`: target 종류와 관계없이 프로젝트의 열린 Pending이 없다는 뜻이다. 건수가 있으면 목록으로 이동한다.
- `세금계산서 발행 기록`: 발행일을 입력하면 준비 상태가 된다. 미래 날짜나 프로젝트 생성 전 날짜는 저장되지 않는다.

### 임시 저장

발행일·번호·메모를 입력하고 `임시 저장`을 누른다. 저장 version이 표시된다. 다른 사용자가 먼저 저장한 경우 최신 값으로 다시 불러온다.

### 최종 완료

세 조건이 모두 충족되면 `최종 완료 확인`이 활성화된다. 2차 확인 후 정산·내 업무·workflow와 프로젝트가 함께 완료된다. 완료 뒤에는 `프로젝트 완료 내역`에서 발행일, 번호, 메모, 완료 담당·시각을 조회할 수 있지만 수정할 수 없다.

### 권한 메시지

정산을 조회할 수 있어도 sales 업무 permission과 대상 project 담당 조건을 모두 충족하지 않으면 수정 버튼이 없다. System Administrator도 업무 정산은 대신 완료할 수 없다.

## 11. User validation checklist

상태: `사용자 검수 대기`

### 자동 검증 완료

- [x] desktop 1440 project-context 조건 + invoice/final 2열
- [x] mobile 390 조건 → invoice → 최종 확인 한 열과 overflow 0
- [x] 완료 후 mobile read-only 결과와 완료 actor/시각
- [x] 좌상단 숨김 메뉴 유지, 하단 고정 메뉴·전역 정산 메뉴 없음
- [x] draft/version, same-operation replay, future date·open Pending·generic 완료 차단
- [x] sales-only mutation과 System Administrator 403
- [x] settlement/work item/event/project/audit/알림/receipt 원자 완료
- [x] 완료 후 신규 Pending·면수·상태·삭제 차단
- [x] 알림 invoice/Pending 원문 없음과 외부 delivery row 0

### 사용자 직접 확인 대기

- [ ] desktop screenshot의 조건 요약과 최종 확인 영역이 한눈에 이해되는지 확인
- [ ] mobile 390에서 글씨·도형 크기와 한 화면 정보 밀도가 편한지 확인
- [ ] 원형·각진 정사각형·타원/둥근 도형의 의미 구분이 자연스러운지 확인
- [ ] `임시 저장`과 `최종 완료`의 위험도 차이가 명확한지 확인
- [ ] 완료 후 read-only 화면의 담당·시각·invoice 정보가 충분한지 확인

검수 증빙에는 실제 사용자·프로젝트 원문 대신 날짜, 환경, 익명 역할명과 성공/실패만 기록한다.

## 12. Rollback, known issue와 후속 경계

local experiment commit 폐기는 이 branch를 대표 저장소에 merge하지 않고 보존하거나 별도 승인 아래 branch를 정리하면 된다. 대표 저장소와 `main`에는 변경이 없으므로 원본 rollback 작업은 없다.

known/deferred 항목은 다음과 같다.

- 사용자 직접 검수 미완료
- CI 미실행(push/PR 미승인)
- Persistent UAT migration·runtime 미적용
- 완료 뒤 재오픈·수정발행·세금계산서 취소 정책 미구현
- 외부 회계·전자세금계산서·파일/OCR/PDF/Excel·수금 미구현
- 프로젝트 완료의 Teams/Mail 외부 event 연결 미구현
- 기존 `App.tsx` 대형 파일과 production chunk 500kB warning은 범위 밖 기존 기술 부채

이 항목들은 실험 범위의 명시적 제외 또는 사용자 검수 gate이며 현재 Open P0/P1/P2 Finding은 아니다.

## 13. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | [이 문서](014a-implementation-report.md) |
| SOP | 완료 | [이 문서의 SOP](014a-implementation-report.md#9-sop) |
| User manual | 완료 | [이 문서의 User manual](014a-implementation-report.md#10-user-manual) |
| Roadmap update | 완료 | [Product Roadmap의 TASK-014A](../docs/00-product-roadmap.md#task-014a-영업-정산--세금계산서--프로젝트-완료) |
| User validation checklist | 작성·자동 검증 완료·사용자 검수 대기 | [이 문서의 checklist](014a-implementation-report.md#11-user-validation-checklist) |

## 14. 개발 블로그 소재

### 1. 해결한 업무 문제

납품 완료와 프로젝트 완료 사이에 비어 있던 정산 단계를 project-context 화면과 원자 transaction으로 연결했다. 영업은 세 가지 조건만 확인하고 완료하며, 다른 부서는 완료된 프로젝트를 동일 route에서 읽을 수 있다.

### 2. 기술적 결정과 검토한 대안

정산 정보를 projects 컬럼에 섞는 대신 1:1 settlement record와 append-only operation receipt로 분리했다. DB isolation level만 믿지 않고 Pending INSERT와 completion이 같은 project row를 project-first로 잠그게 해 phantom race를 닫았다. 전역 queue는 기존 내 업무와 중복되므로 만들지 않았다.

### 3. 시행착오 및 폐기한 접근

delivery result 단순 count, project/panel Pending만 검사, generic 내 업무 완료, System Administrator mutation, global settlement menu를 폐기했다. E2E fixture도 실제 Pending terminal metadata·panel count version·Project.Delete persona 계약을 따르도록 세 번 보정했다. 전체 test의 무출력 minimal logger를 중단하고 individual hang 감시가 있는 normal logger로 동일 suite를 재실행해 장기 실행과 hang을 구분했다.

### 4. 사용자 검수 결과와 남은 항목

자동 검증과 synthetic screenshot 생성은 완료했다. 사용자 직접 검수, Persistent UAT, CI·게시, 완료 뒤 정정 정책과 외부 회계 연동은 별도 승인·Task로 남는다.
