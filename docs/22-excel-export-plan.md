# TASK-EXPORT-001 — 모든 페이지 Excel 출력 공통 기능 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-EXPORT-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/export-001-interview.md`
- firstPlanningSource: `tasks/export-001-planning.md`
- codexReviewSource: `tasks/export-001-review.md`
- approvalChangeSource: `tasks/export-001-change-001.md`
- branchScope: `experiment/task-export-001-excel-export` 한정. 대표 repo·GitHub `main`·push·PR·merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

이 문서는 확인된 interview, Fable 1차 기획 전문과 Codex 내용 review 전문을 모두 읽고 review의 유지·보강·추가·제거·보류 판정과 8개 Finding resolution을 통합한 `TASK-EXPORT-001`의 authoritative implementation contract다. 1차 기획과 review 원문은 수정하지 않고 판단 이력으로 보존한다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 이 문서에 복사하지 않는다.

## 1. 목표와 Phase 1 범위

허용된 조회 화면에서 사용자가 현재 검색·필터와 자신의 조회 권한 범위를 그대로 반영한 안전한 `.xlsx` 파일을 한 번의 action으로 내려받을 수 있게 한다.

이번 구현은 `TASK-EXPORT-001 Phase 1 partial`이다. 공통 export 기반과 서로 다른 데이터 형태의 우선 3개 화면(프로젝트 목록, 구매 dashboard, 내 업무) vertical slice만 구현하며, Roadmap의 "모든 주요 페이지" 완료로 기록하지 않는다. Roadmap·implementation report·완료 보고 어디에도 `TASK-EXPORT-001 전체 완료`로 표시하지 않는다.

## 2. Review resolution 통합 상태

Codex review의 판정을 다음과 같이 반영한다. 모두 이 문서의 필수 계약이며 선택 사항이 아니다.

| Review 판정 | 반영 |
| --- | --- |
| 공통 workbook builder + 화면별 definition — 유지 | 4장 |
| 3개 화면 vertical slice — 유지 (Deleted tab·담당 프로젝트 tab v1 제외 명시) | 5장 |
| 기존 read permission·scope 재사용 — 유지+보강 (구매 scope 보정 선행) | 3·6장 |
| 고정 server allowlist 컬럼 — 유지 (컬럼 선택은 Phase 2 Deferred) | 5·14장 |
| bounded 동기 생성 — 유지+보강 (cap+1 검출·생성 전 차단·concurrency gate 필수) | 4장 |
| 일반 application log audit — 제거 | 7장으로 대체 |
| 최소 DB export audit — 추가 (additive migration) | 7장 |
| header-only 0건 workbook — 유지 (`0건 파일 생성` 안내) | 8장 |
| `다운로드 완료` 문구 — 제거 (`Excel 파일 생성을 완료했습니다`로 표현) | 8장 |
| 비동기 job/storage·CSV/PDF·모든 화면 일괄 — 보류 | 14장 |

8개 Finding(`EXPORT-PROCUREMENT-SCOPE` P1, `EXPORT-PROJECT-PAGING-EQUIVALENCE`·`EXPORT-PROCUREMENT-SILENT-CAP`·`EXPORT-RESOURCE-FENCE`·`EXPORT-AUDIT-DURABILITY`·`EXPORT-FORMULA-SAFETY`·`EXPORT-SENSITIVE-COLUMN-ALLOWLIST` P2, `EXPORT-UI-SUCCESS-SEMANTICS` P3)은 각각 6장, 5.1장, 5.2장, 4.4장, 7장, 4.3장, 5장, 8장의 계약과 12장의 검증 항목으로 해소한다. openBlockingDecisionCount는 0이다.

## 3. 대상 사용자와 권한 계약

- 신규 `data.export` permission을 만들지 않는다. 각 export endpoint는 해당 화면 list endpoint와 동일한 permission·scope 검사를 적용한다.
- 프로젝트 목록 export: `projects.read` + `GetProjectAccessScope` 기반 project claim scope. `Project.Read.All` 보유자는 전체.
- 구매 dashboard export: 6장의 보정된 `ProjectRead` + `ProjectAccessScope`를 화면과 export가 동일하게 적용한다.
- 내 업무 export: 인증된 사용자 본인의 work item만. 서버가 인증 user id로 고정하며 다른 사용자의 업무는 어떤 파라미터로도 포함할 수 없다.
- 민감 컬럼(매출액)은 `Project.SalesAmount.Read` 보유 시에만 컬럼 자체를 포함한다. 미보유 시 빈 값이 아니라 컬럼 미포함이다.
- System Administrator도 업무 mutation을 우회하지 않으며 export는 read 정책만 따른다.
- Frontend 버튼 숨김은 보조 수단이고 서버 Policy가 최종 차단 지점이다.

## 4. 공통 export 기반 계약

### 4.1 Workbook builder

신규 Backend `ExcelExport` 공통 모듈이 다음을 한곳에서 강제한다.

- 시트 구성: 제목 행, 생성일시(`yyyy-mm-dd hh:mm`), 적용 필터 요약 행(고정 label + 사용자가 입력한 값의 bounded 표시), bold header, FreezeRows, AutoFilter, AdjustToContents. 기존 `CalendarHolidayExcelParser.CreateTemplate`의 ClosedXML 스타일 관례를 재사용한다.
- 값 형식: 날짜는 `yyyy-mm-dd` DateFormat, 숫자·boolean은 typed value, 상태는 화면과 같은 한글 label. 화면과 파일의 의미가 동일해야 한다.
- 파일명: `EMI_<화면명>_<yyyyMMdd_HHmmss>.xlsx` 형식과 Content-Disposition의 UTF-8 filename 처리. 응답은 `Results.File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName)` 패턴을 재사용한다.
- reflection/DTO 자동 직렬화를 금지한다. 각 adapter는 explicit column 정의와 value selector만 사용한다.

### 4.2 Row 제한

- export row 상한은 10,000행이다. 각 adapter는 cap+1(10,001)을 단일 조회로 요청해 초과를 검출하고, 초과 시 workbook 생성을 시작하기 전에 422와 "조회 조건을 좁혀 다시 시도해 주세요" 한글 안내를 반환한다. 부분 파일을 반환하지 않는다.
- 0건 결과는 header-only workbook을 정상 생성한다(현재 조건의 유효한 기록).

### 4.3 Formula injection 방어

- 공통 cell writer가 모든 자유 문자열을 명시적 text cell로 기록한다. `=`, `+`, `-`, `@`, tab/CR로 시작하는 값이 수식·특수 값으로 해석되지 않아야 한다.
- 사용 중인 ClosedXML 버전의 string assignment 동작을 가정하지 않고, 생성된 workbook을 재파싱해 formula 객체 0개를 검증하는 테스트와 leading marker별 synthetic negative fixture를 둔다.
- 숫자·날짜·boolean만 typed value로 기록하고 그 외는 모두 text다.

### 4.4 Resource fence와 취소

- singleton gate(`SemaphoreSlim` 또는 동등물)로 process당 동시 workbook 생성을 2개로 제한한다. 대기 슬롯 없이 포화 시 즉시 429와 "잠시 후 다시 시도해 주세요" 한글 안내를 반환한다.
- 조회, gate 획득, workbook 생성 전 구간에 `CancellationToken`을 전파하고, 예외·취소 경로에서 slot이 누수되지 않음을 테스트한다.
- export는 domain write 0, 알림 0, workflow 상태 전이 0이며 중복 실행이 업무 데이터를 바꾸지 않는다.

## 5. 화면별 adapter 계약

공통 원칙: 각 adapter는 해당 화면 list endpoint와 같은 query parser·store 조회 경로·정렬·scope helper를 재사용한다. export 전용 SQL을 새로 작성하지 않는다. 내부 GUID, raw enum, description/link 원문, 이메일·사번 등 개인 식별 원문은 어떤 adapter에도 넣지 않고 화면과 같은 표시명·표시 코드만 사용한다.

### 5.1 프로젝트 목록

- 필터: `All/Active/OnHold/Completed/Cancelled` 상태, 검색어, 납기일 범위 — 화면 list와 동일한 `ParseProjectListQuery` 계약.
- 조회: `ListProjectsAsync`의 기존 single-query 경로에 내부 overload를 추가해 일반 list는 기존 max 100 clamp를 유지하고 export는 cap+1(10,001)을 한 번에 조회한다. 화면 page 반복 호출 방식은 data drift에 의한 중복·누락 위험 때문에 금지한다. total_count와 cap+1 결과로 10,000 초과를 workbook 생성 전에 차단한다. filter·scope·정렬은 list와 동일하게 유지한다.
- 컬럼: 화면 표시 기준의 프로젝트 표시 코드·제목·고객·Item·상태·진행 정보·납기일 등 allowlist. 매출액 컬럼은 `Project.SalesAmount.Read` 보유 시에만 추가한다.
- `Deleted`(삭제 보관함) tab은 별도 admin scope·데이터 lifecycle이므로 Phase 1 export 대상에서 제외하고 해당 tab에서는 export action을 표시하지 않는다.

### 5.2 구매 dashboard

- 6장의 scope 보정을 선행한다. export는 보정된 `ProjectRead` + `ProjectAccessScope`와 기존 검색어·입고예정일 범위 필터를 적용한 project summary row만 포함한다. 펼친 구매품목 detail은 화면에서도 별도 request이므로 Phase 1 파일에 섞지 않는다.
- store에 caller row limit+1을 적용하는 공통 read path를 두고 scope·filter SQL은 하나만 유지한다. 기존 dashboard는 현행 UX 호환을 유지하되 truncation metadata(기존 `limit 500`의 잘림 여부)를 명시적으로 노출하고, export는 10,001 조회로 10,000 초과를 차단한다. 조용한 잘림(silent cap)을 파일로 재생산하지 않는다.

### 5.3 내 업무

- 필터: `All/Requested/InProgress/Completed` status만 지원한다.
- 조회 대상은 인증 사용자 본인 work item으로 서버에서 고정한다.
- 컬럼: 업무 title, 프로젝트 표시 정보, stage, status, priority, due/created/completed date만 포함한다. description, link/target, work item 내부 id는 제외한다.
- `AssignedProjects`(담당 프로젝트) tab은 다른 response type이므로 Phase 1 export를 표시하지 않고 후속 adapter로 둔다.

## 6. 구매 dashboard scope 보정 (P1 필수 선행)

- 현재 `/api/procurement/dashboard`는 기본 인증만 요구하고 `projects.read`·project scope를 적용하지 않으며 store query가 모든 non-deleted/non-completed project를 읽는다. 이 상태로 export를 연결하면 scope 밖 프로젝트 제목·고객·구매 집계가 파일로 대량 유출된다.
- 기존 dashboard endpoint와 신규 export endpoint 모두에 `ProjectRead` 확인 후 `ProjectAccessScope`를 store where 조건으로 적용한다. System Administrator/`Project.Read.All`은 전체, 일반 사용자는 claim scope만 조회한다.
- list(dashboard)와 export가 같은 scope matrix(일반 사용자/scope 밖/admin)를 같은 테스트에서 검증한다. 이 보정은 안전 예외(권한·scope 우회 금지)에 해당하므로 fast-track에서 생략할 수 없다.

## 7. Export audit 계약

- 일반 application log 기반 audit는 채택하지 않는다(review에서 제거 판정).
- 다음 additive migration(현재 실험 계보 최신 `0037` 다음 번호, 구현 시점에 ledger로 재확인)으로 append-only `data_export_events` 테이블을 추가한다.
  - 저장: actor user id FK, allowlisted export kind(고정 enum), row count, 필터 사용 여부 flag/고정 enum projection, 성공 시각.
  - 저장 금지: 검색어 원문, 고객·프로젝트명, 파일 bytes, 컬럼 data, 그 외 payload 원문.
  - update/delete trigger로 append-only를 보장한다.
- 성공 응답 직전에 audit insert를 수행하고, insert 실패 시 파일을 반환하지 않는다. workbook 생성과 audit insert를 exactly-once로 표현하지 않으며 실제 보장 수준을 implementation report에 기록한다.
- 실패·차단(403/422/429)된 생성은 성공 audit를 남기지 않는다.
- 기존 migration은 수정하지 않고, fresh DB와 기존 isolated DB 모두에서 migration을 검증한다. Persistent UAT에는 적용하지 않는다.

## 8. API·Frontend·UX 계약

### API

- 신규 GET endpoint 3개: 프로젝트 목록 export(`/api/projects` 계열), 구매 dashboard export(`/api/procurement/dashboard` 계열), 내 업무 export(`/api/my-work` 계열). mutation은 없다.
- 오류 계약: 권한 없음 403(또는 기존 관례의 Forbid), 미지원·잘못된 필터와 row 제한 초과 422 + 한글 안내, concurrency 포화 429 + 재시도 안내. raw SQL·stack trace·내부 식별자를 응답에 노출하지 않는다.

### Frontend

- 공통 export hook/버튼 component를 만들어 3개 화면의 filter/action 영역에 배치한다. `api.ts`의 기존 blob 다운로드 패턴(`fetchWithAuth` → blob → objectURL → anchor.download, Content-Disposition 파일명)을 재사용한다.
- 생성 중 버튼 비활성·중복 submit 차단. 성공 문구는 `Excel 파일 생성을 완료했습니다`로 한정한다 — anchor click 이후 브라우저 로컬 저장 성공은 앱이 확인할 수 없으므로 `다운로드 완료`를 단정하지 않는다. 0건은 `조건에 맞는 데이터가 없어 0건 파일을 생성했습니다`류의 명시 안내를 표시한다.
- 실패·422·429는 action 근처 `aria-live` 영역에 다음 행동(조건 축소, 잠시 후 재시도)과 함께 표시하고, 조건을 유지한 채 재시도할 수 있게 한다.
- 모바일(390px)·Teams narrow: PC action bar를 축소하지 않고 현재 범위 요약·단일 실행·완료 feedback을 compact하게 재배치한다. export 전용 full-screen sheet를 모든 화면에 강제하지 않으며, 각 화면의 기존 compact action 흐름과 좌상단 숨김 메뉴 원칙을 보존한다. page-level horizontal overflow 0, keyboard/focus 접근성을 유지한다.

## 9. 업무 규칙과 불변조건

- 같은 사용자·같은 필터에서 export row 집합은 화면 list 결과와 동일해야 한다(단일 조회, cap 이내 전체). 이 동등성은 같은 query 경로 재사용으로 구조적으로 보장하고 테스트로 고정한다.
- Backend가 permission·scope·필터·컬럼·제한의 authoritative layer다.
- 기존 Excel import(template/preview/apply) 계약은 불변이다. export 모듈·route·버튼을 제거해도 기존 조회·import는 그대로 동작한다(rollback 경계).
- raw PII/secret/내부 GUID 출력 0, domain write 0, 외부 발송 0.
- 실패한 생성은 부분 파일·성공 audit·성공 feedback을 만들지 않는다.

## 10. 구현 순서

1. 구매 dashboard의 `ProjectRead` + `ProjectAccessScope` 보정과 일반/admin/scope 밖 접근 matrix 테스트 고정.
2. 공통 workbook builder, formula-safe cell writer, bounded 파일명·필터 요약, concurrency gate, `data_export_events` additive migration과 audit 기록 경로.
3. 프로젝트 목록 export: `ListProjectsAsync` 내부 overload(cap+1)와 adapter 연결.
4. 내 업무 export: 인증 user id·status filter 고정, 제외 컬럼 계약 적용.
5. 구매 dashboard export: 보정된 scope·기존 filter·limit+1 공통 read path 연결.
6. 공통 Frontend export action을 3개 화면 desktop·390px에 배치.
7. workbook 재파싱 E2E, 권한/scope/filter/audit/resource 회귀와 기존 import 불변 검증, 화면별 desktop·390px screenshot.

## 11. 안전 경계

- Persistent UAT 영향 없음: isolated synthetic runtime·DB만 사용하며 신규 migration도 Persistent UAT에 적용하지 않는다.
- migration: `data_export_events` additive 1건만. 기존 migration 수정·번호 재사용 금지.
- 외부 발송·실제 데이터 영향 없음: 실제 사용자·고객·프로젝트 원문으로 export를 실행하지 않는다.
- runtime 교체 없음: canonical runtime·5174 handover 불포함.
- Git 경계: local experiment commit까지만. push·PR·merge 미승인, main merge 승인 0/3. 이 문서는 게시·main merge·Persistent UAT 승인을 부여하지 않는다.

## 12. 검증 계약

- Backend 테스트: 화면별 권한 거부(permission 없음·scope 밖), 구매 dashboard scope matrix(보정 전 노출 경로의 회귀 방지 포함), list-export row 동등성, cap+1 초과 422(workbook 미생성), 구매 silent cap 제거(truncation metadata·export 차단), 민감 컬럼 포함/제외(`Project.SalesAmount.Read` 유무별 컬럼 존재 여부), formula 객체 0개 재파싱 + leading marker negative fixture, concurrency gate 포화 429·slot 누수 없음·취소 전파, audit insert 성공/실패 경로(실패 시 파일 미반환, 실패 요청의 성공 audit 0), append-only trigger, content type·파일명, 0건 header-only, 잘못된 날짜 범위 422, migration fresh/기존 isolated DB 검증.
- Frontend 테스트: export hook/버튼의 loading·중복 차단·성공(`생성 완료` 문구)·0건·422·429·오류 상태 unit test, lint·typecheck·build.
- 회귀: 기존 import(template/preview/apply) 3개 영역과 3개 화면 list 조회, isolated Full-Stack E2E에서 다운로드 파일을 재파싱해 header·row·formula 0을 검사하는 시나리오 1개 이상.
- 증빙: 3개 화면 desktop·390px synthetic screenshot을 privacy-safe projection으로 수집한다. 사용자 검수 완료로 표시하지 않고 `사용자 검수 대기`로 종료한다.

## 13. 완료 기준과 중단 조건

완료 기준:

- 3개 화면에서 현재 필터·scope 기준 `.xlsx` 생성·다운로드가 동작하고 12장의 자동 검증이 통과한다.
- 구매 dashboard scope 보정이 화면과 export 양쪽에 적용되어 있다.
- audit row가 성공 export당 1건 기록되고 원문 데이터를 포함하지 않는다.
- 5종 산출물(implementation report·SOP·user manual·Roadmap update·user validation checklist)의 상태·위치를 추적하고, Roadmap에는 `Phase 1 partial`과 잔여 화면·Deferred 항목을 기록한다.
- local experiment commit까지 완료, push·PR·merge 없음.

중단 조건: 권한·scope 우회, 민감 필드·내부 식별자 노출, formula injection 미방어, unbounded 자원 사용, import 계약 회귀, migration ledger 충돌 또는 Repository 계약 충돌이 발견되면 구현을 중단하고 blocking으로 보고한다.

## 14. 포함·제외·Deferred

포함: 공통 export 기반(4장), 3개 화면 adapter(5장), 구매 scope 보정(6장), audit persistence(7장), 공통 Frontend action(8장), 12장 검증.

제외·Deferred:

- 자재·품질·제조·물류·정산·Pending·알림·관리자 화면 adapter — Phase 2 이후.
- 사용자 컬럼 picker — Roadmap 대상이나 고정 allowlist 검증 뒤 별도 UX Task로 Deferred.
- 프로젝트 `Deleted` tab·내 업무 `AssignedProjects` tab export — 후속 adapter.
- 비동기 job·파일 storage·재다운로드·예약/정기 export, CSV·PDF·ZIP·이메일/Teams 발송, 외부 storage·회계·BI·Graph 연동 — 실제 규모 요구 확인 후 별도 Task.
- Excel import 계약 변경, Persistent UAT·실제 업무 데이터 export, 대표 repo·`main`·push·PR·merge.

## 15. 채택된 비차단 결정 기록

사용자 standing rule(권장안 자동 채택)과 review resolution에 따라 다음을 확정한다. 별도 사용자 확인이 필요한 blocking 결정은 없다.

| 번호 | 결정 | 채택 내용 | 근거 |
| ---: | --- | --- | --- |
| 1 | 첫 화면 조합 | 프로젝트 목록 + 구매 dashboard + 내 업무 3개 | 서로 다른 row/권한/scope 형태로 공통 구조 증명 (1차안 유지, review 유지 판정) |
| 2 | 권한 모델 | 신규 permission 없음, 화면 read permission·scope 재사용 + 구매 scope 보정 선행 | 최소 권한·이중 정책 방지 (review 유지+보강) |
| 3 | 컬럼 선택 | 서버 고정 allowlist, 컬럼 picker Deferred | 노출 위험 최소화 (review 유지) |
| 4 | 자원 제한 | bounded 동기 10,000행, cap+1 단일 조회, 동시 2개 gate 필수, 429/422 안내 | ClosedXML memory 특성 (review 보강: gate를 선택에서 필수로 승격) |
| 5 | 감사 수준 | 일반 로그 audit 제거, append-only `data_export_events` 최소 DB audit + additive migration | Roadmap audit 포함 범위와 privacy-safe 검색성 (review 제거+추가 판정으로 1차안 대체) |

## 16. Roadmap 기록 지침

- canonical 실행 큐의 4.3 `Deferred` 상태와 다음 `TASK-007A` Next Gate는 변경하지 않는다.
- `TASK-EXPORT-001` 절에 2026-07-18 실험 재정렬 승인 아래 실험 상태를 추가하되 `Phase 1 partial`(3개 화면·고정 컬럼·최소 audit)임과 잔여 화면·Deferred 항목을 명시한다.
- 대표 repo·`main`·Persistent UAT·provider 불변을 함께 기록한다.

openBlockingDecisionCount: 0
