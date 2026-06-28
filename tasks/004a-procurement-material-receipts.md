# TASK-004A: Procurement Plan and Material Receipt Completion

## 목적

프로젝트별 구매 발주품목과 입고예정 정보를 시스템에서 조회·입력하고, 구매팀과 자재팀이 입고 완료를 기록할 수 있게 한다. 구매 Excel은 여러 프로젝트를 포함할 수 있으며 등록된 프로젝트와 매칭된 행만 적용한다.

## 구현범위

- 프로젝트 단위 구매정보 조회
- 구매정보 직접 입력
- 구매 Excel Preview·Apply
- Excel 프로젝트 매칭과 재업로드 변경분 Preview
- 구매 Excel `.xlsx` 양식 다운로드
- 영업팀 프로젝트 생성 Excel Preview·Apply
- 자재팀 입고 완료 입력 페이지
- 구매팀 입고 완료 수기 입력
- 관리자 전용 grouped history
- PC/Mobile 화면 분리

## 제외범위

미입고, 입고지연, 부분입고, IQC, 구매처, 단가, 재고, ECOUNT 연동, 구매 알림, 제조·품질·물류 기능은 구현하지 않는다.

## 구매 입력항목

- PJT
- PJT Code
- 통상납기
- 발주품목
- 기술 담당자
- 발주일
- 입고예정일
- 출하일: 구매품목 입력값이 아니라 프로젝트 납기일을 표시한다.
- 이슈사항
- 입고 완료
- 예정일까지: 프로젝트별 구매 목록 요약에서만 표시하고, 프로젝트 상세 구매표와 선택 프로젝트 구매정보 표에는 표시하지 않는다.

구매 행은 반드시 `ProjectId`와 연결한다. Excel 원본의 PJT/PJT CODE는 참고 텍스트로 저장할 수 있지만 업무 연결키는 `ProjectId`다.

## 입고 완료 입력

`receipt_completed`, `receipt_completed_at_utc`, `receipt_completed_by_user_id`, `receipt_completion_note`를 저장한다. 완료 체크 시 서버가 현재 UTC와 현재 사용자를 기록한다. 구매팀과 자재팀은 완료일과 비고를 수기 수정할 수 있다. 체크 해제도 허용하며 현재 상태값은 false/null로 변경하고 완료 비고는 유지한다. 사유가 없으면 기본 사유 `입고 완료 체크 해제`로 audit을 남긴다.

## 자재팀 페이지

`/materials/receipts`는 MaterialReceipt.Update 권한 사용자를 위한 입력 페이지다. 자재팀은 프로젝트 검색, 발주품목 조회, 입고 완료 체크, 완료일 수기 입력, 완료 비고 입력, 저장만 수행한다. 구매 본문 필드는 수정하지 못한다.

## Excel 구조

Template과 Preview는 `.xlsx`만 지원한다. 제목은 1행 `PS 사업부 PJT 발주 관리`, Header는 3행이다.

인식 Header:

- PJT
- PJT CODE
- 통상납기
- 발주품목
- 기술 담당자
- 발주일
- 입고일
- 출하일
- 이슈사항
- 입고 완료

Excel `입고일`은 `입고예정일`로 표시한다. Excel `출하일` 열은 참고값일 뿐 구매품목에 저장하지 않는다. 화면의 `출하일`은 프로젝트 납기일(`Project.DeliveryDate`)로 자동 표시한다. `입고 완료` 열은 시스템 추가 열이며 없어도 오류가 아니다.

## 프로젝트 매칭 규칙

- PJT 값이 있는 행은 새 프로젝트 그룹 시작이다.
- 아래 행의 PJT/PJT CODE가 비어 있으면 직전 프로젝트 그룹에 속한다.
- 그룹 첫 행의 PJT/PJT CODE를 하위 행에도 source 값으로 유지한다.
- 자동 매칭 순서:
  1. normalized ProjectTitle 정확 일치
  2. ProjectCode 보조 후보
  3. ProjectTitle 유사 후보
- 정확 일치가 아니면 사용자가 Preview에서 등록된 프로젝트를 선택해야 한다.
- `NeedsReview`, `Unmatched`, `Error`가 있으면 Apply 불가다.
- 프로젝트 자동 생성은 하지 않는다.

## Excel 재업로드 변경분 Preview

Excel에 안정적인 Row ID가 없으므로 다음 순서로 기존 구매품목과 매칭한다.

1. 시스템 Row ID가 있으면 Row ID
2. 기존 성공 import의 `project_id + source_group_sequence`
3. 같은 project_id 안에서 `row_match_key`가 유일한 경우
4. 모호하면 `NeedsReview`

`row_match_key`는 발주품목, 기술 담당자, 발주일, 입고예정일, 통상납기를 정규화해 계산한다. `MissingFromUpload`는 자동 삭제하지 않고 기존 행을 유지한다.

## 필수값 없음

구매정보 행에는 필수 업무값이 없다. 모든 인식 열이 빈 행은 `Skipped`다. 빈 셀은 기존값 삭제가 아니며 Excel로 삭제를 지원하지 않는다.

## 직접 입력 규칙

사용자는 등록된 프로젝트를 검색·선택해야 한다. 자유입력으로 프로젝트를 만들 수 없고 존재하지 않는 프로젝트에는 저장할 수 없다. 변경된 필드만 update/audit한다.

## Excel Preview·Apply

Preview ResultType:

- New
- Changed
- Unchanged
- Skipped
- MissingFromUpload
- NeedsReview
- Error

Apply는 파일 SHA, 파일 재파싱, 프로젝트 매칭, project row lock, 기존 procurement row lock, row_version, 변경분 계산을 다시 수행한다. 오류가 있으면 전체 rollback한다. 같은 `project_id + file_sha256` 성공 batch가 있으면 409로 차단한다.

## 입고예정일 표시

- 날짜 없음: `-`
- 미래: `D-n`
- 오늘: `D-Day`
- 과거: `예정일 n일 경과`

`미입고`, `입고지연`, `부분입고` 문구는 쓰지 않는다. 입고 완료 여부는 별도 열에만 표시한다.

## 권한

- 신규 Role: `procurement`, `materials`
- 신규 Permission: `ProcurementPlan.Update`, `MaterialReceipt.Update`
- Procurement: `Project.Read.All`, `ProcurementPlan.Update`, `MaterialReceipt.Update`
- Materials: `Project.Read.All`, `MaterialReceipt.Update`
- 구매정보 입력: Procurement only
- 입고 완료 입력: Procurement, Materials
- 관리자 grouped history: `Audit.Read.All` System Administrator only
- System Administrator는 구매정보/입고완료 입력 불가

## 구매 목록·대시보드

구매 업무 시작점은 `/procurement` 페이지다. 이 페이지는 KPI 카드, 검색, 프로젝트별 구매 목록, 선택 프로젝트 구매정보, 전체 Excel 업로드를 제공한다.

KPI:

- 입고대기품목
- 입고완료품목
- 입고예정일 경과 품목

`입고예정일 경과`는 상태가 아니라 날짜 참고값이다. `입고지연`, `미입고`, `부분입고` 상태를 만들지 않는다.

KPI와 프로젝트별 구매 요약은 backend summary API에서 계산한다. Frontend는 대량 데이터를 받아 임의 집계하지 않는다.

프로젝트와 구매는 모든 화면에서 접근 가능한 공통 메뉴로 제공한다. PC는 좌측 메뉴, 모바일은 compact 상단 메뉴를 사용한다. 현재 TASK에서 실제 제공되는 큰 페이지는 `프로젝트`와 `구매`이며, 향후 생산관리·제조·품질·물류·자재 페이지를 같은 구조에 추가할 수 있도록 NavigationItem 기반으로 확장한다.

프로젝트 목록 페이지도 KPI 카드를 제공한다. 프로젝트 KPI는 서버의 `/api/projects/summary`에서 계산하며 삭제 프로젝트와 Completed 프로젝트를 제외한다. 완료 프로젝트는 완료 탭에서 조회할 수 있지만 현재 업무 대시보드 KPI 집계에는 포함하지 않는다. 현재 항목은 전체 프로젝트, 진행 프로젝트, 보류 프로젝트, 취소 프로젝트, 제조 완료 프로젝트, 검사 완료 프로젝트다. 제조/검사 집계는 현재 `workflow_stage` 기반 임시 요약이며 향후 체크리스트 기반 진행률 엔진으로 교체될 수 있다.

검색이 있는 주요 화면은 기간 필터를 제공한다. 프로젝트 목록은 납기일 기준 `deliveryDateFrom`/`deliveryDateTo`, 구매 목록과 자재 입고 입력 목록은 입고예정일 기준 `expectedReceiptDateFrom`/`expectedReceiptDateTo`를 backend query로 전달한다.

구매 페이지에서 프로젝트를 클릭하면 해당 프로젝트 구매정보가 클릭한 row/card 바로 아래에 펼쳐진다. 동시에 하나의 프로젝트만 펼쳐지고, 같은 프로젝트를 다시 클릭하면 접힌다. 선택 프로젝트 구매정보를 페이지 맨 아래 고정 영역으로 분리하지 않는다.

## 조회·수정 페이지 분리

프로젝트 상세 구매 섹션은 조회 전용이며 input, disabled input, 저장 버튼, Excel 버튼이 없다. Procurement 사용자에게만 `/projects/{projectId}/procurement/edit` 수정 버튼을 표시한다. 프로젝트 상세에서는 `제품 목록`과 `구매` 섹션 버튼으로 한 섹션만 표시한다. Excel 다운로드·업로드·Preview·Apply는 구매 수정 페이지와 `/procurement` 구매 목록 페이지에서 제공한다. Materials는 `/materials/receipts`를 사용한다.

## 자재 입고 입력 기본 목록

`/materials/receipts`는 입력용 화면이므로 기본 조회에서 `receipt_completed = false` 또는 null인 항목만 표시한다. 완료 항목은 저장 후 기본 목록에서 사라진다.

조회 보조 토글 `완료 항목 포함`을 켜면 완료된 항목도 조회할 수 있다.

완료 항목 포함 상태에서 이미 완료된 항목의 완료일 또는 완료 비고를 수정할 수 있다. 완료 체크 해제도 허용하며 해제 사유는 필수가 아니다. 해제 시 완료일/완료자는 null로 바뀌고 과거 완료/해제 이력은 audit으로 보존한다.

## 삭제 보관함 복구

System Administrator는 삭제 보관함의 논리삭제 프로젝트를 복구할 수 있다. Sales는 삭제 보관함 조회 정책은 유지하되 복구와 완전삭제 버튼을 볼 수 없다. 복구 시 `deleted_at_utc`, `deleted_by_user_id`, `delete_reason`, `deleted_correlation_id`를 비우고 삭제 전 프로젝트 상태를 유지한다. 삭제되지 않은 동일 프로젝트명이 이미 있으면 복구하지 않고 409를 반환한다.

## 오류 메시지와 저장 후 이동

사용자 화면에는 영어 ProblemDetails 제목, stack trace, SQL, constraint 이름을 그대로 표시하지 않는다. 공통 오류 파서는 network 오류, 400/401/403/404/409/500 응답을 한글 문구로 변환하고, validation errors가 있으면 화면 용어 중심으로 표시한다.

정상 저장 시 입력/수정 화면은 조회 화면으로 돌아간다. 오류가 있으면 현재 입력 화면에 머물고 사용자가 입력한 값을 유지한 상태로 한글 오류 메시지를 표시한다. 구매정보 직접 저장과 구매 Excel 적용은 프로젝트 상세의 구매 섹션으로 돌아간다.

Excel Preview는 저장 불가 사유를 버튼 근처에 명시하고, 구매 Excel Preview는 `저장 가능한 데이터 목록`과 `저장 불가능한 데이터 목록` 두 섹션으로 표시한다. 두 목록은 같은 Header 구조를 사용하며, 저장 불가능한 행은 행 아래에 `사유`, `필드`, `입력값`, `문제`를 표시한다. `해결방법` 문구는 사용자 화면에 표시하지 않는다. 모달은 PC에서 `min(1200px, calc(100vw - 48px))`, 모바일에서 `calc(100vw - 24px)` 기준으로 표시하며 sticky header보다 높은 z-index를 사용한다.

수동 검수 환경은 Backend `http://127.0.0.1:5081`, Frontend `http://127.0.0.1:5174`, PostgreSQL DB `emi_qms_uat_004a`로 고정한다. E2E는 별도 임시 DB를 생성하고 종료 후 삭제하므로 수동 검수 DB와 섞지 않는다.

## 데이터 저장 위치

개발 환경의 업무 데이터는 Docker Compose PostgreSQL 컨테이너의 PostgreSQL DB에 저장된다. 주요 table은 `projects`, `panel_placeholders`, `project_procurement_items`, `project_audit_events`, `procurement_excel_import_batches`, `panel_information_excel_import_batches`다. Frontend는 업무 원본 데이터를 저장하지 않고, localStorage에는 표시 단위 같은 UI preference만 저장할 수 있다. Excel 원본 binary는 저장하지 않고 파일명, hash, import batch metadata만 DB에 저장한다.

Full-Stack E2E는 전용 임시 DB를 생성하고 테스트 종료 후 삭제한다. 따라서 E2E 중 만든 프로젝트는 개발 검수 DB에 남지 않는다. 개발 DB의 테스트 데이터도 운영 데이터가 아니며 Docker volume 삭제, DB reset, 임시 DB 삭제 시 사라질 수 있다.

실제 운영에서는 업무 데이터와 감사이력을 PostgreSQL 운영 DB에 저장한다. 사진, 문서, PDF, Excel 원본처럼 파일 보존이 필요한 경우 Azure Blob Storage 또는 동등한 파일 저장소를 사용한다. Secret과 connection string은 Azure Key Vault 또는 운영 secret store에 저장한다. 감사이력은 PostgreSQL audit table에 append-only 성격으로 보존하고, 운영 DB는 automated backup과 필요 시 PITR 정책을 적용한다. 개발/테스트 DB는 운영 DB와 완전히 분리한다.

## 관리자 전용 이력

Field-level audit row는 유지하고 화면/API에서는 ImportBatchId 또는 CorrelationId 기준으로 그룹화한다. 모든 구매 history API는 `Audit.Read.All`을 요구하며 비관리자는 403이다.

## PC/Mobile 화면

- 프로젝트 상세 구매 조회 PC: sticky table
- 프로젝트 상세 구매 조회 Mobile: 구매 행 card
- 구매 수정 PC: grid
- 구매 수정 Mobile: card input
- 자재팀 입력 PC: checkbox table
- 자재팀 입력 Mobile: checkbox card
- Excel Preview PC: table
- Excel Preview Mobile: compact card/list

## API

- `GET /api/projects/{projectId}/procurement`
- `GET /api/projects/summary`
- `GET /api/projects/import/template`
- `POST /api/projects/import/preview`
- `POST /api/projects/import/apply`
- `PATCH /api/projects/{projectId}/procurement`
- `GET /api/projects/{projectId}/procurement/history`
- `GET /api/projects/{projectId}/procurement/import/template`
- `POST /api/procurement/import/preview`
- `POST /api/procurement/import/apply`
- `GET /api/procurement/dashboard`
- `GET /api/materials/receipts?includeCompleted=false`
- `PATCH /api/materials/receipts`

## Migration

`database/migrations/0008_procurement_expected_receipts.sql`을 추가한다. 기존 0001~0007은 수정하지 않는다. Role, Permission, procurement item table, procurement import batch table, indexes, FK, constraints를 추가하고 실제 업무 seed는 넣지 않는다.

## 테스트 완료조건

- Backend PostgreSQL 통합 테스트: 권한, 프로젝트 매칭, Excel preview/apply, receipt, D-day, history, migration
- Frontend 테스트: 조회/수정 분리, PC/Mobile, matching preview, receipt page, D-day 문구, history 권한
- Full-Stack E2E: 직접 입력, Excel matching/apply, 재업로드 변경분, 자재 입고 입력, 관리자 history, 모바일, 권한 차단

## 사용자 직접 검수 절차

자동 테스트 후 Docker PostgreSQL, Development Seed, Backend, Frontend를 실행 상태로 유지한다. 가짜 UAT 프로젝트 `UAT-PROC-현재시각`과 `/tmp/emi-qms-procurement-uat/` Excel 샘플을 준비한다.

체크리스트:

- [ ] 구매 조회 페이지 input 없음
- [ ] Procurement만 구매 수정 가능
- [ ] Materials는 자재 입고 입력 페이지만 접근
- [ ] 구매 목록 페이지에 KPI 카드가 있음
- [ ] 구매 목록 페이지에서 전체 Excel 업로드 가능
- [ ] Sales는 프로젝트 Excel 업로드로 프로젝트를 일괄 생성 가능
- [ ] 입고 완료 저장 후 기본 자재 목록에서 사라짐
- [ ] 완료 항목 포함 토글 시 다시 표시됨
- [ ] Excel 프로젝트 매칭 표시
- [ ] 존재하지 않는 프로젝트는 Apply 불가
- [ ] Excel 변경분 Preview
- [ ] 담당자 확인 후 Apply
- [ ] 입고 완료 체크 시 완료일 자동 입력
- [ ] 완료일 수기 수정 가능
- [ ] D-day 표시
- [ ] 입고지연/미입고 문구 없음
- [ ] Admin만 이력 조회
- [ ] PC table / Mobile card
- [ ] Console 오류 없음
