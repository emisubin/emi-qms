# TASK-EXPORT-002 — 선택 프로젝트 Excel 내보내기 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-EXPORT-002`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/export-002-interview.md`
- firstPlanningSource: `tasks/export-002-planning.md`
- codexReviewSource: `tasks/export-002-review.md`
- approvalChangeSource: `tasks/export-002-change-001.md`
- branchScope: `experiment/task-export-002-selected-project-export` 한정. 대표 repo·GitHub `main`·push·PR·merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

이 문서는 확인된 interview, Fable 1차 기획 전문과 Codex 내용 review 전문을 모두 읽고 review의 유지·보강·보류·제거 판정과 9개 Finding resolution을 통합한 `TASK-EXPORT-002`의 authoritative implementation contract다. 1차 기획과 review 원문은 수정하지 않고 판단 이력으로 보존한다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 이 문서에 복사하지 않는다.

## 1. 목표와 범위

사용자가 프로젝트 목록에서 필요한 프로젝트 여러 건을 checkbox로 직접 선택하고, 정확히 그 프로젝트만 포함된 단일 `.xlsx` 파일을 기존 권한·scope·Excel 안전 계약 그대로 내려받을 수 있게 한다.

- 기존 `TASK-EXPORT-001`의 현재 filter 전체 export를 대체하지 않고 같은 화면에서 병행한다. 기존 action은 "전체 조건 export", 신규 action은 "명시 선택 subset export"로 label·scope·감사를 구분한다.
- 대상 화면은 프로젝트 목록(Deleted tab 제외)뿐이다. 다른 화면의 다중 선택, 전 page 대량 선택, 복합 multi-sheet, column picker는 포함하지 않는다.
- 검증은 isolated synthetic 데이터만 사용한다. Persistent UAT·실제 업무 데이터·실제 provider는 사용하지 않는다.

## 2. Review resolution 통합 상태

Codex review의 판정을 다음과 같이 반영한다. 모두 이 문서의 필수 계약이며 선택 사항이 아니다.

| Review 판정 | 반영 위치 |
| --- | --- |
| desktop 행 checkbox·mobile 카드 checkbox — 유지+보강 (event/keyboard 완전 분리) | 5.1·5.2장 |
| visible-list header 전체 선택 + indeterminate — 유지 | 5.1장 |
| 선택 수·선택 해제·0건 disabled — 유지 | 5장 |
| 선택 최대 100건, 101건 이상 stable 422 — 유지 | 6장 |
| `POST /api/projects/export/selected` — 유지+보강 (string 배열 수동 validation) | 6장 |
| 동일 scope·sales permission·workbook builder 재사용 — 유지 | 7장 |
| 선택 전부-or-전무 조회 — 유지+보강 (exact count match·generic 422) | 6.2장 |
| 선택 전용 audit kind `ProjectsSelected` — 유지+보강 (0038 불변·additive migration) | 8장 |
| export 실패 시 선택 유지, 성공 시에도 유지 + 명시적 `선택 해제` — 유지 | 9.3장 |
| mobile fixed bottom bar — 제거 (card 목록 위 compact inline tray로 대체) | 5.2장 |
| 전체 filter 선택·pagination selection — 보류 | 14장 |
| multi-sheet·컬럼 picker·파일 저장 — 제거/보류 | 14장 |

9개 Finding(`SELECTED-EXPORT-ATOMIC-SCOPE` P1, `SELECTED-EXPORT-ROW-KEYBOARD-CONFLICT`·`SELECTED-EXPORT-STABLE-BODY-VALIDATION`·`SELECTED-EXPORT-AUDIT-MIGRATION`·`SELECTED-EXPORT-ERROR-RECOVERY`·`SELECTED-EXPORT-REQUEST-SNAPSHOT`·`SELECTED-EXPORT-SELECTION-LIFECYCLE` P2, `SELECTED-EXPORT-MOBILE-ACTION-POSITION`·`SELECTED-EXPORT-EXISTING-ACTION-REGRESSION` P3)은 각각 6.2장, 5.1장, 6.1장, 8장, 9.2장, 9.3장, 9.3장, 5.2장, 9.1장의 계약과 12장의 검증 항목으로 해소한다.

## 3. 대상 사용자와 권한 계약

- 신규 permission을 만들지 않는다. 선택 export는 기존 프로젝트 목록 export와 동일하게 `ProjectRead` 확인, `GetProjectAccessScope` 기반 project claim scope, `CanReadSalesAmount` 기반 매출 열 gate를 재사용한다.
- `ProjectRead` 없으면 403(Forbid), 인증 사용자 식별 실패는 401이다.
- 매출액·통화 열은 `Project.SalesAmount.Read` 보유 시에만 열 자체가 존재한다. 미보유 시 빈 값이 아니라 열 미포함이다.
- scope 밖 프로젝트 ID가 하나라도 섞이면 전체를 거부한다(6.2장). 부분 export는 없다.
- Frontend 선택 UI 표시는 보조 수단이고 서버 Policy가 최종 차단 지점이다. System Administrator도 read 정책만 따르며 domain mutation 우회는 없다.

## 4. 업무 규칙과 불변조건

- 파일에는 사용자가 선택한 프로젝트만 존재한다. 선택하지 않은 row 0, 검증 실패 상태의 부분 포함 0.
- Backend가 선택 집합 전체에 대해 permission·`ProjectAccessScope`·soft-delete 제외 존재를 재검증하는 authoritative layer다. Frontend 선택 상태를 신뢰하지 않는다.
- 검증 실패 시 파일·성공 audit·성공 feedback을 만들지 않는다(전부-or-전무).
- 기존 filter 전체 export 3종(프로젝트·구매·내 업무), 프로젝트 목록 조회, Excel import(template/preview/apply) 계약은 불변이다. 선택 UI·subset route·migration을 제외한 어떤 기존 경로도 의미가 바뀌지 않는다(rollback 경계: 선택 기능 제거 시 기존 기능 그대로 동작).
- 문자열은 모두 text cell로 기록하고 생성 파일 재파싱 시 formula 객체 0개를 유지한다.
- export는 domain write 0, 알림 0, workflow 상태 전이 0이다. 선택 상태는 화면 로컬 임시 UI 상태이며 서버에 저장하지 않는다.
- 선택한 project ID 원문·프로젝트명·검색어·파일 bytes를 audit·서버 로그·오류 응답에 남기지 않는다.

## 5. 화면·선택 UX 계약

공통: 선택 대상은 현재 로드된 visible non-deleted 목록뿐이다(기본 list page 20건, 상한 100건). `선택 N건` 카운트는 선택·해제 즉시 갱신되고 `aria-live`로 안내한다. 0건이면 실행 action을 비활성화하고 이유를 안내한다. Deleted tab에는 선택 UI와 선택 export action을 노출하지 않는다. 두 viewport 모두 page-level horizontal overflow 0을 유지한다.

### 5.1 Desktop table (`SELECTED-EXPORT-ROW-KEYBOARD-CONFLICT` resolution 포함)

- 기존 role="table" 구조의 첫 column에 행 checkbox를, header에 visible 전체 선택 checkbox를 추가한다. header checkbox는 전체/일부 선택의 indeterminate 상태를 제공하며 현재 로드된 항목만 대상으로 한다.
- checkbox cell/control은 click·keyboard propagation을 차단한다. checkbox의 Space는 선택만 변경한다.
- 행 click/Enter/Space 상세 열기 handler는 input·button·link 등 interactive target에서 발생한 event를 무시한다. 행의 비-interactive 영역 Enter/Space는 기존 상세 열기를 유지한다.
- 각 checkbox는 프로젝트를 식별하는 접근성 label(PJT Code 기반)을 가진다. mouse와 keyboard 양쪽 동작을 테스트로 고정한다.
- `선택 내보내기` action은 기존 page export action 영역에 기존 filter export와 나란히 배치하고 label로 두 작업을 구분한다.

### 5.2 Mobile 390px (`SELECTED-EXPORT-MOBILE-ACTION-POSITION` resolution 포함)

- 카드 상단에 충분한 touch target과 간결한 label의 checkbox를 둔다. 카드의 `상세 보기` 버튼·기존 동작과 event가 충돌하지 않는다.
- fixed bottom bar를 사용하지 않는다. 카드 목록 바로 위에 compact inline tray를 두고 `N개 선택`, `선택 Excel 내보내기`, `선택 해제`만 노출한다.
- desktop table을 축소한 layout을 만들지 않으며 기존 좌상단 숨김 메뉴·mobile header 규칙을 보존한다.

## 6. API·request 계약

신규 endpoint 1개: `POST /api/projects/export/selected`, JSON body `{ "projectIds": ["<id>", ...] }`. domain mutation은 없으며 POST는 bounded body 운반 목적이다(URL 길이·ID 노출 회피). GET query 방식은 채택하지 않는다.

### 6.1 입력 validation (`SELECTED-EXPORT-STABLE-BODY-VALIDATION` resolution)

- request body의 ID는 framework의 `Guid[]` 직접 binding에 맡기지 않고 문자열 배열로 받아 수동 검증한다: 항목별 trim → GUID parse → empty/누락 body·0건·parse 실패·중복·100건 초과를 모두 동일한 422 validation contract(title + field error + 한글 안내)로 반환한다.
- 중복 ID는 조용히 제거하지 않고 422로 거부한다.
- 모든 사용자 입력 오류가 입력 모양과 무관하게 안정적인 422가 되도록 테스트로 고정한다. framework 기본 400 경로가 이 계약을 대체하지 않아야 한다.

### 6.2 전부-or-전무 scope 검증 (`SELECTED-EXPORT-ATOMIC-SCOPE` resolution, P1)

- unique requested ID 집합을 기존 프로젝트 list SQL 경로와 동일한 조건(`ProjectRead` 후 `ProjectAccessScope` where, soft-delete 제외, 동일 정렬·컬럼 구성)에 ID 집합 조건만 추가한 단일 query로 한 번에 조회한다. 단건 반복 조회와 export 전용 별도 SQL을 금지한다.
- 조회 결과 count가 requested unique count와 정확히 같을 때만 workbook을 생성한다. 하나라도 다르면(삭제·scope 밖·미존재·동시 변경) generic 422 — "선택한 프로젝트 중 내보낼 수 없는 항목이 있습니다. 목록을 새로고침한 뒤 다시 선택해 주세요." 계열 — 를 반환하고 파일·audit을 만들지 않는다.
- 어떤 ID가 왜 실패했는지 구분하거나 원문 ID를 응답·로그·audit에 남기지 않는다.

### 6.3 실행·응답 계약

- 기존 `ExcelExportConcurrencyGate` 2-slot no-wait를 재사용한다. 포화 시 즉시 429와 기존 재시도 안내를 반환한다. 조회~생성 구간 `CancellationToken` 전파와 slot 누수 없음을 유지한다.
- 성공 응답은 기존 계약과 동일: `.xlsx` content type, UTF-8 filename(`EMI_프로젝트선택_<yyyyMMdd_HHmmss>.xlsx` 형식의 선택 전용 화면명), `X-Export-Row-Count`.
- raw SQL·stack trace·내부 식별자를 응답에 노출하지 않는다.

## 7. Workbook 계약

- 기존 `ExcelWorkbookBuilder`와 프로젝트 목록 export의 `ProjectColumns` allowlist(매출 permission 열 gate 포함), typed value/text cell 규칙, formula-safe writer, 제목·생성시각·filter 요약 행 구조를 그대로 재사용한다. 신규 column 정의·DTO reflection·별도 sheet 형식을 만들지 않는다.
- filter 요약 행에는 검색어·프로젝트 식별 원문 대신 `선택 프로젝트 N건`만 bounded 표기한다.
- 파일 row 순서는 기존 목록과 동일한 정렬을 따른다(선택 click 순서가 아님).
- 선택 export에서 0건 파일 경로는 존재하지 않는다 — 0건 선택은 6.1장에서 차단된다.

## 8. Audit·migration 계약 (`SELECTED-EXPORT-AUDIT-MIGRATION` resolution)

- 성공 export에만 기존 `DataExportAuditStore.AppendSuccessAsync`로 append-only audit 1건을 기록한다: export kind `ProjectsSelected`, 선택 row count, `filtersApplied` false 고정(선택 export는 서버 filter를 사용하지 않고 kind 자체가 선택 사용을 의미한다), 매출 열 포함 여부 flag.
- migration `0038`은 수정하지 않는다. 다음 additive migration(현재 계보 최신 `0038` 다음 번호 — 구현 시 ledger로 재확인, 예상 `0039`)에서 두 check constraint를 drop/recreate한다:
  - `ck_data_export_events_kind`: 허용 kind에 `ProjectsSelected` 추가.
  - `ck_data_export_events_sensitive_columns`: 민감 매출 flag를 `Projects` 또는 `ProjectsSelected`에만 허용.
- row count check, index, append-only update/delete trigger는 변경하지 않고 유지한다.
- fresh DB와 기존 isolated(upgrade) DB 양쪽에서 migration과 PostgreSQL contract test를 검증한다. Persistent UAT에는 적용하지 않는다.
- 실패·차단(401/403/422/429)된 요청은 어떤 audit도 남기지 않는다. audit insert 실패 시 파일을 반환하지 않는 기존 순서를 유지한다.

## 9. Frontend 계약

### 9.1 공통 helper·action 확장 (`SELECTED-EXPORT-EXISTING-ACTION-REGRESSION` resolution)

- `api.ts`의 `downloadExcelExport`에 optional `RequestInit` 계열 확장만 추가하고 기본 동작은 기존 GET과 byte 단위로 동일하게 유지한다. 신규 `exportSelectedProjectsExcel`이 JSON body POST로 이를 사용한다.
- `ExcelExportAction`의 신규 props(비활성 상태·비활성 사유·선택 전용 422 hint·busy 상태 전달 callback)는 모두 backward-compatible optional로 추가한다. 기존 3개 화면 사용처는 코드 변경 없이 기존 동작을 유지해야 하며, 공통 확장이 기존 계약과 충돌하면 선택 전용 최소 sibling component로 분리한다.
- 기존 3개 export action tests와 API download tests를 회귀 실행한다.

### 9.2 오류·복구 문구 (`SELECTED-EXPORT-ERROR-RECOVERY` resolution)

- 기존 filter export action의 422 문구("조건을 좁혀 다시 시도")는 보존한다.
- 선택 export action의 422는 별도 hint를 사용한다: 선택을 유지한 채 "목록을 새로고침한 뒤 다시 선택해 주세요"를 안내한다.
- 429는 기존 "잠시 후 다시 시도" 안내, 일반 실패는 기존 generic 문구를 유지한다. 성공은 `Excel 파일 생성을 완료했습니다`로 고정하고 브라우저 저장 완료를 단정하지 않는다.
- 모든 feedback은 action 근처 `aria-live` 영역에 표시한다.

### 9.3 선택 상태 lifecycle과 실행 snapshot (`SELECTED-EXPORT-SELECTION-LIFECYCLE`·`SELECTED-EXPORT-REQUEST-SNAPSHOT` resolution)

- 프로젝트 목록 페이지가 `Set<projectId>` 선택 상태를 소유하고 desktop/mobile view에 동일한 선택 계약을 전달한다.
- 초기화 시점: 검색 submit, status tab 변경, 납기 범위 적용/초기화, 명시적 reload, 새 목록 response 수락. 새 response를 수락할 때마다 visible ID 집합 밖의 선택 값을 항상 제거한다.
- 유지 시점: export 실패와 export 성공 모두 선택을 유지한다(같은 subset 재다운로드 가능). 비우기는 명시적 `선택 해제`와 위 초기화 시점으로만 발생한다.
- 실행 시 unique ID snapshot을 만들어 POST body로 보내고, export 진행 중에는 개별 checkbox·전체 선택·선택 해제·재실행을 잠근다. 성공·실패 후 잠금을 해제한다. 화면의 선택 수와 실제 body가 달라질 수 있는 경로를 만들지 않는다.

## 10. 구현 순서

1. 선택 request validation(6.1)과 전부-or-전무 store query(6.2)를 기존 프로젝트 list SQL 경로에 추가하고 계약 테스트를 고정한다.
2. `ProjectsSelected`를 허용하는 additive migration(8장)과 migration ledger·audit contract tests를 추가한다.
3. 선택 export service 경로와 `POST /api/projects/export/selected` endpoint를 기존 permission/scope/sales gate/concurrency/workbook builder/audit에 연결한다.
4. `downloadExcelExport` optional POST 확장과 선택 전용 422 hint·busy 전달을 backward-compatible로 구현하고 기존 export 회귀 테스트를 실행한다.
5. 프로젝트 목록 페이지에 selection state·lifecycle·snapshot 잠금(9.3)을 두고 desktop checkbox column과 row event 분리(5.1), mobile 카드 checkbox와 inline tray(5.2)를 구현한다.
6. Backend·Frontend·isolated Full-Stack E2E에서 12장 검증을 수행하고 desktop 1440·mobile 390 페이지 screenshot과 실제 생성된 선택 Excel workbook screenshot을 synthetic 데이터로 수집한 뒤 열었던 workbook을 닫았음을 확인·기록한다.
7. implementation report·5종 산출물 상태·Roadmap 상태를 갱신하고 local experiment commit까지만 수행한다.

## 11. 안전 경계

- Persistent UAT 영향 없음: isolated synthetic runtime·DB만 사용하며 신규 migration도 Persistent UAT에 적용하지 않는다.
- migration: audit check constraint 확장 additive 1건만. `0038` 수정·번호 재사용 금지, 실제 번호는 구현 시 ledger로 확정.
- 외부 발송·실제 데이터 영향 없음: 실제 사용자·고객·프로젝트 원문으로 export를 실행하지 않는다.
- runtime 교체 없음: canonical runtime·5174 handover 불포함.
- Git 경계: `tasks/export-002-change-001.md` 기준 local experiment commit까지 승인. push·PR·merge 미승인, main merge 승인 0/3, Persistent UAT·실제 provider 미승인. 이 문서는 게시·main merge·Persistent UAT 승인을 부여하지 않는다.

## 12. 검증 계약

- Backend 테스트: `ProjectRead` 없음 403·미인증 401, 빈/누락 body·0건·malformed GUID·중복·101건 422(모두 동일 validation contract), scope 밖 혼입·삭제 프로젝트 혼입·미존재 ID 혼입 각각 generic 422 + 파일 미반환 + audit 0건, 성공 시 선택 row만 존재(선택 외 row 0)·기존 목록 정렬 동일, 매출 permission 유무별 열 존재/부재, workbook 재파싱 formula 객체 0, filter 요약 행 `선택 프로젝트 N건` bounded 표기, 429 포화·slot 누수 없음·취소 전파, audit kind `ProjectsSelected`·row count·filtersApplied false·sensitive flag 기록, append-only trigger 유지, migration fresh/upgrade 검증, 기존 export 3종·목록·import 회귀.
- Frontend 테스트: 선택 상태 unit(개별/전체 선택·해제, indeterminate, 검색·tab·date·reload·new response 초기화, visible 밖 ID 제거, 성공/실패 유지), 실행 중 잠금·snapshot, 0건 disabled·카운트 표시, desktop checkbox와 row click/Enter/Space 분리(mouse·keyboard), 선택 전용 422 hint·429·성공 문구, 기존 `ExcelExportAction` 3개 사용처 회귀. lint·typecheck·build.
- E2E: isolated Full-Stack에서 synthetic 프로젝트 3건 중 2건 선택 → 다운로드 파일 재파싱으로 선택 2건만 존재·formula 0 확인, 기존 전체 filter export 회귀 시나리오 유지. desktop·390px page-level horizontal overflow 0.
- 증빙: 프로젝트 목록 desktop 1440·mobile 390 선택 상태 screenshot과 실제 생성된 선택 Excel workbook screenshot을 privacy-safe synthetic 데이터로 수집하고, 확인용으로 연 workbook을 닫았음을 기록한다. 사용자 검수 완료로 표시하지 않고 `자동 검증 완료 / 사용자 검수 대기`로 종료한다.

## 13. 완료 기준과 중단 조건

완료 기준:

- 프로젝트 2건 이상 선택 → 단일 파일에 선택한 프로젝트 행만 존재하고 12장의 자동 검증이 전부 통과한다.
- 권한 밖·부분 성공 export 0, 매출 열 gate·formula 0·내부 GUID 열 0·domain write 0.
- 성공 export당 `ProjectsSelected` audit 1건이 기록되고 ID·원문 데이터를 포함하지 않는다.
- 기존 filter export 3종·목록·import 회귀 0.
- 5종 산출물(implementation report·SOP·user manual·Roadmap update·user validation checklist)의 상태·위치를 추적하고 Roadmap `TASK-EXPORT-002` 상태를 갱신한다.
- local experiment commit까지 완료, push·PR·merge 없음.

중단 조건: 권한·scope 우회, 부분 성공 파일, 선택 외 row 포함, per-ID 실패 원인 노출, formula injection 미방어, audit constraint 충돌 또는 migration ledger 충돌, 기존 export/import 회귀, Repository 계약 충돌이 발견되면 구현을 중단하고 blocking으로 보고한다.

## 14. 포함·제외·Deferred

포함: 프로젝트 목록 desktop·390px 다중 선택 UX(5장), 선택 subset POST endpoint와 전부-or-전무 검증(6장), 기존 workbook 재사용(7장), `ProjectsSelected` audit과 additive migration(8장), Frontend helper·lifecycle·snapshot 계약(9장), 12장 검증과 screenshot 증빙.

제외·Deferred:

- 현재 보이지 않는 page까지의 selection, server-side "필터 결과 모두 선택", 선택 저장·공유 — 별도 Task.
- 프로젝트 이외 화면의 다중 선택 export — 필요 시 별도 NEW_FEATURE.
- 선택 전용 컬럼 picker, multi-sheet 보고서, CSV·PDF·ZIP, async job·파일 storage·재다운로드, email/Teams 발송 — 제외.
- `Deleted` tab 선택·export, Excel import 계약 변경 — 제외.
- Persistent UAT·실제 업무 데이터·대표 repo·`main`·push·PR·merge — 제외.

## 15. 채택된 비차단 결정 기록

사용자 standing rule(권장안 자동 채택)과 review resolution에 따라 다음을 확정한다. 별도 사용자 확인이 필요한 blocking 결정은 없다.

| 번호 | 결정 | 채택 내용 | 근거 |
| ---: | --- | --- | --- |
| 1 | 전체 선택 의미 | 현재 로드된 visible 목록만(header 전체 선택 + indeterminate) | 보이는 것과 파일의 일치가 기능 목적. 전 page 선택은 명시적 제외 (1차안 유지, review 유지 판정) |
| 2 | 선택 lifecycle | 검색·tab·납기 범위·reload·새 response에서 초기화, export 성공·실패에서는 유지, 명시적 `선택 해제` 제공 | stale·숨은 선택 차단과 재다운로드 편의의 균형 (review가 1차안의 성공 후 처리를 유지로 확정) |
| 3 | 선택 상한 | request당 unique 100건, 초과는 stable 422 | 현재 list pageSize 경계·bounded body·workbook 비용 (1차안 유지) |
| 4 | scope 밖·stale 처리 | 전부-or-전무 fail-closed generic 422, per-ID 원인 미노출, "새로고침 후 재선택" 복구 안내 | 정보 누출 차단과 복구 가능성 (review 보강: exact count match·audit 0건 명시) |
| 5 | request·audit 계약 | `POST` JSON body(string 배열 수동 검증) + 신규 kind `ProjectsSelected` + check constraint 확장 additive migration, filtersApplied false 고정 | URL 노출·길이 위험 제거, append-only audit 의미 보존, `0038` constraint가 신규 kind를 거부하는 구현 사실 (review 보강 반영) |

## 16. Roadmap 기록 지침

- canonical 실행 큐와 다음 `TASK-007A` Next Gate는 변경하지 않는다.
- `TASK-EXPORT-002` 절과 실행 큐 4.3A 상태를 2차 기획·구현 진행에 맞춰 갱신하되 experiment fast-track 한정임과 대표 repo·`main`·Persistent UAT·provider 불변을 함께 기록한다.
- `TASK-EXPORT-001` Phase 1 partial 상태와 잔여 범위 기록은 변경하지 않는다.

openBlockingDecisionCount: 0
