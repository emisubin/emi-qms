# TASK-EXPORT-001 Fable 1차 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/export-001-planning.md`
- reviewScope: 제품 방향·사용자 가치·실제 Repository 권한/필터/Excel 경계
- sourcePlanningModified: false
- reviewRound: 1
- openBlockingDecisionCountAfterResolution: 0

## 1. 총평

Fable 1차안의 핵심 방향인 `공통 workbook builder + 화면별 allowlist adapter + 기존 read permission/scope 재사용 + bounded 동기 export`는 유지할 가치가 높다. 사용자가 실제로 원하는 것은 별도 보고서 시스템이 아니라 “지금 보고 있는 조건을 Excel로 안전하게 옮기는 것”이고, 프로젝트·구매·내 업무 3개 화면은 project 단위·dashboard 집계·본인 업무라는 서로 다른 scope를 검증하기 좋은 최소 조합이다.

다만 그대로 구현하면 구매 dashboard의 기존 scope 누락을 파일로 확대하고, 프로젝트 목록 pagination을 우회하려다 별도 SQL 또는 불안정한 다중 page 조회를 만들 수 있다. 또한 10,000행 ClosedXML 생성의 동시 실행 제한을 선택으로 두면 process memory를 보호할 수 없고, Roadmap 포함 범위인 audit를 일반 application log만으로 대체하면 사용자가 찾을 수 있는 업무 감사 근거가 남지 않는다. 이 네 경계는 2차 기획에서 필수 계약으로 승격해야 한다.

## 2. 사용자 문제·제품 방향 판단

- 사용자 문제 정합성: 유지. 화면 복사·입력용 template 오용을 제거하고 현재 필터를 파일에 보존하는 가치는 명확하다.
- Roadmap 정합성: 부분 정합. Roadmap은 모든 주요 페이지와 컬럼 선택·audit를 포함하지만 1차안은 3개 vertical slice·고정 컬럼·로그 audit다. 실험에서는 공통 구조 증명이 합리적이지만 `TASK-EXPORT-001 전체 완료`로 표시하면 안 되고 `Phase 1/partial`로 기록해야 한다.
- 모바일 방향: 유지하되 export 전용 full-screen sheet를 모든 화면에 강제하지 않는다. 모바일에서 핵심은 현재 범위 요약·단일 실행·feedback이며, 기존 좌상단 숨김 menu와 page-specific compact action 흐름을 보존해야 한다.
- 운영 부담: bounded synchronous generation은 적절하다. 비동기 job·storage·재다운로드는 현재 가치 대비 과도해 보류한다.

## 3. 기능 판정

| 항목 | 판정 | 근거·2차 기획 요구 |
| --- | --- | --- |
| 공통 workbook builder와 화면별 definition | 유지 | 형식·formula·파일명·제한을 한곳에서 강제하고 adapter만 확장 가능 |
| 프로젝트 목록·구매 dashboard·내 업무 3화면 | 유지 | 서로 다른 row/권한/scope 모양으로 공통성 검증. 단, 삭제 보관함과 담당 프로젝트 tab은 v1 제외를 명시 |
| 기존 read permission·scope 재사용 | 유지+보강 | 신규 global permission 불필요. 구매 dashboard의 기존 scope 누락을 먼저 보정해야 함 |
| 고정 server allowlist 컬럼 | 유지 | 임의 컬럼 선택보다 안전. Roadmap의 컬럼 선택은 Phase 2 Deferred로 명시 |
| 10,000행 bounded 동기 생성 | 유지+보강 | cap+1로 초과 검출, workbook 생성 전 차단, singleton concurrency gate 필수 |
| 일반 application log audit | 제거 | actor GUID를 일반 로그에 남기는 방식은 privacy-safe evidence·운영 검색성 모두 약함 |
| 최소 DB export audit | 추가 | 파일·행 원문 없이 actor, export kind, row count, 필터 사용 여부/고정 enum, 성공 시각만 저장. additive migration 필요 |
| header-only 0건 workbook | 유지 | 빈 결과도 현재 조건을 기록한 유효 산출물. UI는 `0건 파일 생성`임을 명확히 안내 |
| browser 저장 완료 문구 | 제거 | anchor click 뒤 실제 로컬 저장 성공은 알 수 없음. `Excel 파일 생성을 완료했습니다`로만 표현 |
| 비동기 job/storage, CSV/PDF, 모든 화면 일괄 | 보류 | 실제 대용량·재다운로드 요구 전에는 과설계 |

## 4. 권장 개발 순서

1. 구매 dashboard의 `ProjectRead + ProjectAccessScope` 보정과 일반/admin/scope 밖 matrix를 먼저 고정한다.
2. 공통 workbook builder, formula-safe cell writer, bounded filename/filter summary, concurrency gate와 최소 audit persistence를 구현한다.
3. 프로젝트 목록 export를 기존 `ListProjectsAsync`의 동일 query path에 export용 cap을 주는 방식으로 연결한다.
4. 내 업무 export를 인증 user id와 status filter에 고정하고 description·link·내부 id를 제외한다.
5. 구매 dashboard summary export를 보정된 scope·기존 filter와 연결한다.
6. 공통 Frontend action을 세 화면에 배치하고 desktop/390px 상태를 검증한다.
7. workbook 재파싱 E2E, 권한/scope/filter/audit/resource 회귀와 기존 import 불변을 검증한다.

## 5. Finding과 resolution

### `EXPORT-PROCUREMENT-SCOPE` — P1 — `RESOLVED_IN_REVIEW`

- 원인: `/api/procurement/dashboard`는 `.RequireAuthorization()`만 사용하고 `projects.read` 또는 `ProjectAccessScope`를 적용하지 않는다. Store query도 모든 non-deleted/non-completed project를 읽는다.
- 영향: export가 기존 query를 재사용하면 project scope 밖 프로젝트 제목·고객·구매 집계를 파일로 대량 노출한다. 현재 dashboard 자체도 동일한 노출 경계를 가진다.
- Resolution: 기존 dashboard와 신규 export 모두 `ProjectRead` 확인 후 `ProjectAccessScope`를 store where에 적용한다. System Administrator/Project.Read.All은 전체, 일반 사용자는 claim scope만 허용한다. list와 export의 scope matrix를 같은 test에서 검증한다.

### `EXPORT-PROJECT-PAGING-EQUIVALENCE` — P2 — `RESOLVED_IN_REVIEW`

- 원인: `ListProjectsAsync`는 pageSize를 최대 100으로 clamp하고 복잡한 workflow/pending lateral query와 정렬을 포함한다. 화면 page를 반복 호출하면 조회 사이 data drift로 중복·누락이 가능하고 별도 export SQL은 filter 정합을 깨뜨린다.
- 영향: “현재 필터 전체”와 export row가 달라질 수 있다.
- Resolution: 기존 single-query 경로를 내부 overload로 재사용해 normal list는 max 100, export는 cap+1(10,001)을 한 번에 조회한다. `total_count`와 cap+1을 확인해 10,000 초과면 workbook 생성 전 422로 차단한다. 같은 filter·scope·정렬을 유지한다.

### `EXPORT-PROCUREMENT-SILENT-CAP` — P2 — `RESOLVED_IN_REVIEW`

- 원인: procurement dashboard query는 `limit 500`이지만 total/truncated projection이 없어 export가 이를 재사용하면 500건을 전체로 오인한다.
- 영향: 파일이 조용히 잘려 사용자가 전체 결과라고 믿을 수 있다.
- Resolution: store에 caller row limit+1을 적용하는 공통 read path를 두고 dashboard는 기존 UX 호환을 유지하되 truncation metadata를 명시하며, export는 10,001 조회로 10,000 초과를 차단한다. scope·filter SQL은 하나만 유지한다.

### `EXPORT-RESOURCE-FENCE` — P2 — `RESOLVED_IN_REVIEW`

- 원인: ClosedXML은 전체 workbook을 memory에 구성한다. 10,000행 export를 동시 무제한 허용하면 process memory와 응답성을 위협한다.
- 영향: 여러 사용자의 동시 요청이 Backend 장애로 이어질 수 있다.
- Resolution: singleton `SemaphoreSlim` 또는 동등한 gate로 동시 생성 2개를 허용하고 대기 슬롯 없이 포화 시 429와 재시도 안내를 반환한다. 조회·gate 대기·workbook 생성 모두 CancellationToken을 존중하고 slot 누수를 test한다.

### `EXPORT-AUDIT-DURABILITY` — P2 — `RESOLVED_IN_REVIEW`

- 원인: Fable 1차안의 일반 로그 audit는 actor·필터를 privacy-safe하게 검색·보존하는 제품 audit가 아니며 Roadmap의 audit 포함 범위를 충족하지 못한다.
- 영향: 누가 어떤 업무 범위를 export했는지 운영자가 안정적으로 추적할 수 없고 로그 원문에 식별자가 퍼질 수 있다.
- Resolution: additive migration으로 최소 append-only `data_export_events`를 추가한다. actor id FK, allowlisted export kind, row count, filter flags/고정 enum만 저장하고 검색어·고객/프로젝트명·파일 bytes·컬럼 data는 저장하지 않는다. 성공 workbook 생성과 audit insert를 DB transaction으로 exactly-once라고 표현하지 않으며, 성공 응답 직전 audit insert 실패 시 파일을 반환하지 않는다. update/delete trigger로 append-only를 보장한다.

### `EXPORT-FORMULA-SAFETY` — P2 — `RESOLVED_IN_REVIEW`

- 원인: ClosedXML string assignment 방식에 따라 `=`, `+`, `-`, `@`, tab/CR 시작값이 Excel에서 수식 또는 특수 값으로 취급될 수 있다.
- 영향: 사용자/외부 입력이 포함된 workbook을 열 때 spreadsheet formula injection 위험이 있다.
- Resolution: 공통 writer가 모든 자유 문자열을 명시적 text cell로 기록하고 formula 객체가 0개임을 workbook 재파싱으로 확인한다. leading marker별 synthetic negative fixture를 둔다. 숫자·날짜·boolean만 typed value로 기록한다.

### `EXPORT-SENSITIVE-COLUMN-ALLOWLIST` — P2 — `RESOLVED_IN_REVIEW`

- 원인: list response에는 내부 id, description, link, 담당자 id와 민감 매출 projection이 섞여 있다.
- 영향: DTO 전체를 reflection으로 export하면 화면에 표시되지 않는 내부·민감 data가 노출된다.
- Resolution: reflection/DTO 자동 직렬화를 금지하고 각 adapter가 explicit column/value selector를 정의한다. 매출액은 `Project.SalesAmount.Read` 때만 컬럼 자체를 추가한다. My Work의 description/link/target/workItem id와 Procurement의 내부 id는 제외한다.

### `EXPORT-UI-SUCCESS-SEMANTICS` — P3 — `RESOLVED_IN_REVIEW`

- 원인: 브라우저 anchor click 이후 실제 사용자 filesystem 저장 성공은 앱이 확인할 수 없다.
- 영향: `다운로드 완료` 문구가 확인하지 않은 성공을 단정한다.
- Resolution: HTTP/blob 생성 성공은 `Excel 파일 생성을 완료했습니다`로 표현하고, 브라우저 저장 위치는 사용자가 확인하도록 한다. 실패·429·422는 action 근처 `aria-live`에 다음 행동과 함께 표시한다.

## 6. 2차 기획에 유지할 화면별 계약

- 프로젝트 목록: `All/Active/OnHold/Completed/Cancelled`와 search·delivery date filter를 지원한다. `Deleted` tab은 별도 admin scope·데이터 lifecycle이므로 Phase 1 export action을 숨기고 후속으로 둔다. 매출액은 permission별 column omission을 검증한다.
- 구매 dashboard: search·expected receipt date 범위와 보정된 project scope를 적용한 project summary rows만 export한다. 펼친 구매품목 detail은 현재 화면의 별도 request이므로 Phase 1 파일에 섞지 않는다.
- 내 업무: `All/Requested/InProgress/Completed` status만 지원한다. `AssignedProjects` tab은 다른 response type이므로 Phase 1 export를 숨기고 후속 adapter로 둔다. title·project display·stage/status/priority/due/created/completed date만 포함한다.

## 7. 보류·제외

- 자재·품질·제조·물류·정산·Pending·알림·관리자 adapter는 Phase 2 이후.
- 사용자 컬럼 picker는 Roadmap 대상이지만 Phase 1 고정 allowlist 뒤 별도 UX로 보류.
- 비동기 job·파일 storage·재다운로드·예약/정기 export는 실제 규모 요구 확인 후 별도 Task.
- Persistent UAT, 실제 사용자 data export, provider, push·PR·merge는 제외.

## 8. Review resolution 결론

위 8개 Finding을 2차 기획의 필수 계약과 검증 위치로 통합하면 blocking decision은 0이다. Fable 2차 기획은 1차 원문을 수정하지 않고 `docs/22-excel-export-plan.md`에 완전한 최종 구현 계약으로 작성한다. 구현 완료 상태는 `TASK-EXPORT-001 Phase 1 partial`로 기록하며 “모든 페이지 완료”로 표시하지 않는다.
