# TASK-EXPORT-002 Fable 1차 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/export-002-planning.md`
- reviewScope: 선택 UX·권한/scope·감사·실패 복구·실제 Repository 구현 경계
- sourcePlanningModified: false
- reviewRound: 1
- openBlockingDecisionCountAfterResolution: 0

## 1. 총평

Fable 1차안의 핵심인 `현재 화면에 보이는 프로젝트를 명시적으로 선택 → 선택한 ID만 기존 프로젝트 Excel 형식으로 내보내기`는 사용자 문제에 정확히 맞는다. TASK-EXPORT-001의 전체 필터 내보내기를 대체하지 않고 같은 위치에서 두 작업을 구분하면, 사용자는 재검색이나 불필요한 행 삭제 없이 필요한 프로젝트만 전달할 수 있다.

권장안인 desktop 행 checkbox·mobile 카드 checkbox·visible-list 전체 선택·최대 100건·선택 전용 POST endpoint·전부-or-전무 검증은 유지한다. 다만 현재 desktop 행은 Space/Enter로 상세를 열고, 공통 Excel action은 모든 422를 “조건을 좁히라”고 안내하며, migration 0038은 export kind와 민감 컬럼을 check constraint로 고정한다. 이 세 구현 사실을 그대로 두면 checkbox 조작이 상세 열기로 이어지거나, stale 선택에 잘못된 복구 안내가 나오거나, 선택 export audit insert가 실패한다. 2차 기획은 아래 resolution을 필수 계약으로 포함해야 한다.

## 2. 사용자 문제·제품 방향 판단

- 사용자 문제 정합성: 유지. “여러 프로젝트만 골라서 같은 Excel로 전달”이 핵심이며 별도 보고서 builder나 컬럼 picker는 필요하지 않다.
- TASK-EXPORT-001과 관계: 대체가 아니라 병행. 기존 `현재 필터 Excel 내보내기`는 전체 조건 export, 신규 action은 명시 선택 subset export로 label·scope·감사를 구분한다.
- Roadmap 정합성: 선택 export는 TASK-007A보다 후순위이지만 사용자가 실험 branch에서 직접 요청했고 명시적 순서 변경 계약이 있으므로 TASK-EXPORT-002로 격리한다. canonical TASK-007A queue는 변경하지 않는다.
- 모바일 방향: 고정 하단 bar를 만들지 않는다. 카드 목록 위의 compact inline 선택 tray를 사용해 좁은 화면에서 핵심 count·내보내기·선택 해제만 보이고, desktop을 줄인 table 형태를 만들지 않는다.
- 운영 부담: 현재 로드된 visible list 최대 100건으로 경계를 제한한다. server-side “필터 전체 선택”, async job, 파일 보관은 사용자 가치 대비 과도해 보류한다.

## 3. 기능 판정

| 항목 | 판정 | 근거·2차 기획 요구 |
| --- | --- | --- |
| desktop 행·mobile 카드 checkbox | 유지+보강 | 명시 선택이 가장 이해하기 쉽다. checkbox 조작은 행 상세 열기와 event·keyboard 경계를 완전히 분리한다. |
| visible-list header 전체 선택 | 유지 | 현재 로드된 항목만 선택하며 indeterminate를 제공한다. 검색·tab·날짜·목록 reload 시 선택을 초기화한다. |
| 선택 수·선택 해제·0건 disabled | 유지 | 별도 modal 없이 현재 문맥에서 실행과 복구가 가능하다. |
| 선택 최대 100건 | 유지 | 현재 list pageSize 경계와 맞고 POST body·workbook 비용을 작게 유지한다. 101건 이상은 stable 422다. |
| `POST /api/projects/export/selected` | 유지+보강 | body의 ID를 수동 검증해 malformed·빈 배열·중복·101건 이상을 동일한 422 validation contract로 반환한다. |
| 동일 scope·sales permission·workbook builder | 유지 | 기존 보안과 파일 형식이 기준선이다. 별도 export SQL과 DTO reflection을 만들지 않는다. |
| 선택 전부-or-전무 조회 | 유지+보강 | scope 밖·삭제·동시 변경 ID가 하나라도 있으면 generic 422, 파일·audit 0건. 어떤 ID가 문제인지 노출하지 않는다. |
| 선택 전용 audit kind | 유지+보강 | 기존 `Projects`와 구분 가능한 `ProjectsSelected`를 추가 migration으로 허용한다. 0038은 수정하지 않는다. |
| export 실패 시 선택 유지 | 유지 | 사용자가 복구 후 다시 실행할 수 있어야 한다. 422는 목록 reload/reselect, 429는 잠시 후 재시도 안내다. |
| export 성공 시 선택 유지 | 유지 | export는 데이터 mutation이 아니며 같은 subset을 재다운로드할 수 있다. 명시적 `선택 해제`를 제공한다. |
| 전체 필터 선택·pagination selection | 보류 | 현재 목록에 보이지 않는 행의 선택 상태·동시 변경 정책이 필요해 별도 Task가 적절하다. |
| 별도 multi-sheet·컬럼 picker·파일 저장 | 제거/보류 | 기존 프로젝트 workbook 형식을 그대로 재사용하는 것이 이번 요청의 최소 완결안이다. |

## 4. 권장 개발 순서

1. 선택 request validation과 전부-or-전무 store query를 기존 프로젝트 list SQL path에 추가한다.
2. `ProjectsSelected`를 허용하는 additive migration과 migration ledger·audit tests를 추가한다.
3. 선택 export service·POST endpoint를 기존 permission/scope/sales amount/concurrency/workbook builder에 연결한다.
4. 공통 download helper가 POST JSON을 지원하도록 확장하고, 선택 전용 422 복구 문구와 busy callback을 제공한다.
5. ProjectListPage에 selected ID state를 두고 desktop/mobile view에 동일한 선택 계약을 전달한다.
6. desktop checkbox와 row click/Enter/Space event를 분리하고 mobile inline tray를 구현한다.
7. backend·frontend·E2E에서 선택 3건 중 2건 파일, scope mismatch, stale selection, audit, formula safety와 기존 전체 필터 export 회귀를 검증한다.

## 5. Finding과 resolution

### `SELECTED-EXPORT-ATOMIC-SCOPE` — P1 — `RESOLVED_IN_REVIEW`

- 원인: 선택 ID를 단건 조회로 반복하거나 조회 후 누락 ID를 응답에 표시하면 scope 밖 프로젝트의 존재를 추측할 수 있고 일부 행만 파일에 담길 수 있다.
- 영향: 명시 선택이 권한 경계를 우회하거나 사용자가 불완전한 파일을 완전한 결과로 오해할 수 있다.
- Resolution: 기존 `ProjectRead`, `ProjectAccessScope`, soft-delete 제외 조건과 동일한 query에서 unique requested ID 집합을 한 번에 조회한다. 조회 `TotalCount`와 requested count가 정확히 같을 때만 workbook을 만들며 하나라도 다르면 generic 422를 반환한다. 누락·삭제·scope 밖 ID를 구분하거나 원문 ID를 로그/응답에 남기지 않고 audit도 쓰지 않는다.

### `SELECTED-EXPORT-ROW-KEYBOARD-CONFLICT` — P2 — `RESOLVED_IN_REVIEW`

- 원인: 현재 desktop project row는 click 및 Enter/Space로 상세를 연다. checkbox의 click/Space가 bubbling되면 선택과 동시에 상세가 열린다.
- 영향: 선택 작업이 예측 불가능해지고 keyboard·touch 사용자에게 오작동이 발생한다.
- Resolution: checkbox cell/control에서 click·keyboard propagation을 차단하고 row handler는 input/button/link 등 interactive target을 무시한다. checkbox Space는 선택만 변경하고, 행의 비-interactive 영역 Enter/Space는 기존 상세 열기를 유지한다. tests로 mouse와 keyboard 양쪽을 고정한다.

### `SELECTED-EXPORT-STABLE-BODY-VALIDATION` — P2 — `RESOLVED_IN_REVIEW`

- 원인: ASP.NET DTO를 `Guid[]`로 직접 binding하면 malformed GUID가 framework 400으로 끝날 수 있어 empty·duplicate·101건의 422 계약과 달라진다.
- 영향: Frontend가 오류별 복구 동작을 안정적으로 제공할 수 없고 API 계약이 입력 모양에 따라 흔들린다.
- Resolution: request는 문자열 배열로 받아 수동 trim/Guid parse/empty/duplicate/count 검증을 수행한다. 모든 사용자 입력 validation은 title과 field error가 있는 422를 반환한다. 중복은 조용히 제거하지 않는다.

### `SELECTED-EXPORT-AUDIT-MIGRATION` — P2 — `RESOLVED_IN_REVIEW`

- 원인: migration 0038의 `ck_data_export_events_kind`는 세 kind만 허용하고, 민감 매출 컬럼 check는 `Projects`만 허용한다.
- 영향: `ProjectsSelected` 성공 audit가 DB constraint에서 실패하거나 민감 컬럼 포함 여부를 거짓으로 기록하게 된다.
- Resolution: 0038을 수정하지 않고 다음 additive migration에서 두 check constraint를 drop/recreate한다. kind에는 `ProjectsSelected`를 추가하고 민감 매출은 `Projects` 또는 `ProjectsSelected`에만 허용한다. append-only trigger·row count·index는 유지한다. fresh/upgrade migration과 PostgreSQL contract test를 추가한다.

### `SELECTED-EXPORT-ERROR-RECOVERY` — P2 — `RESOLVED_IN_REVIEW`

- 원인: 현재 `ExcelExportAction`은 모든 422 뒤에 “조건을 좁혀 다시 시도”를 붙인다. 선택 export의 422는 보통 malformed/stale/scope mismatch이므로 이 조언은 해결책이 아니다.
- 영향: 사용자가 필터를 바꿔도 문제를 해결하지 못하고 선택 상태의 신뢰가 떨어진다.
- Resolution: 기존 전체 필터 action의 문구는 보존하고 선택 action에는 별도의 422 hint를 주입한다. 선택 실패는 선택을 유지하고 “목록을 새로고침한 뒤 다시 선택”을 안내한다. 429는 잠시 후 재시도, 일반 실패는 기존 generic 문구를 유지한다.

### `SELECTED-EXPORT-REQUEST-SNAPSHOT` — P2 — `RESOLVED_IN_REVIEW`

- 원인: download 중 checkbox를 계속 바꿀 수 있으면 화면의 현재 선택 수와 실제 POST body가 달라질 수 있다.
- 영향: 사용자가 어떤 subset이 파일에 들어갔는지 혼동한다.
- Resolution: 실행 시 unique ID snapshot을 만들고 export 진행 중 checkbox·전체 선택·선택 해제와 재실행을 잠근다. 공통 action은 optional busy callback을 제공하거나 선택 전용 sibling component가 busy state를 page에 전달한다. 성공·실패 후 잠금을 해제하며 실패 선택은 유지한다.

### `SELECTED-EXPORT-SELECTION-LIFECYCLE` — P2 — `RESOLVED_IN_REVIEW`

- 원인: ProjectListPage의 fetch callback은 search/status/date 변경과 reload에서 다시 실행된다. state를 단순 effect dependency나 render에 묶으면 불필요하게 초기화되거나 stale ID가 남을 수 있다.
- 영향: 보이지 않는 프로젝트가 export되거나 사용자가 선택한 직후 선택이 사라질 수 있다.
- Resolution: 검색 submit·status tab·납기 범위 적용/초기화·명시 reload와 새 response 수락 시 선택을 비운다. export 실패와 성공은 선택을 유지한다. response의 visible ID 집합 밖 값은 항상 제거한다. Deleted tab에는 선택 UI와 선택 export action을 노출하지 않는다.

### `SELECTED-EXPORT-MOBILE-ACTION-POSITION` — P3 — `RESOLVED_IN_REVIEW`

- 원인: 선택 action을 fixed bottom bar로 만들면 기존 모바일 좌상단 숨김 menu 원칙과 충돌하고 카드 내용을 가린다.
- 영향: 한 화면 핵심 정보 밀도가 낮아지고 작은 화면에서 조작 영역이 겹친다.
- Resolution: mobile 카드 목록 바로 위에 compact inline tray를 두고 선택 수·Excel 내보내기·선택 해제를 배치한다. 카드 checkbox는 충분한 touch target과 간결한 label을 가지며 desktop table을 축소한 layout을 사용하지 않는다.

### `SELECTED-EXPORT-EXISTING-ACTION-REGRESSION` — P3 — `RESOLVED_IN_REVIEW`

- 원인: 공통 helper/action을 POST와 custom errors로 확장하면 TASK-EXPORT-001의 기존 GET exports가 함께 영향을 받는다.
- 영향: 프로젝트 전체 필터·구매·내 업무 export가 깨질 수 있다.
- Resolution: `downloadExcelExport`에 optional `RequestInit`을 추가하되 기본은 기존 GET과 동일하게 유지하고, 기존 `ExcelExportAction` props는 모두 backward-compatible optional로 확장한다. 세 기존 action tests와 API download tests를 회귀 실행한다.

## 6. 2차 기획에 유지할 화면·API 계약

- Desktop: 첫 column에 행 checkbox, header에 visible 전체 선택 checkbox와 indeterminate를 둔다. 행 상세 열기와 선택 event는 독립한다.
- Mobile: 카드 상단에 checkbox를 두고 목록 위 inline tray에 `N개 선택`, `선택 Excel`, `선택 해제`만 노출한다. fixed bottom UI는 사용하지 않는다.
- Selection: current visible non-deleted items만 대상이다. 0건은 disabled, 최대 100건, 검색/tab/date/reload/new response에서 초기화, export 성공/실패에서는 유지한다.
- API: `POST /api/projects/export/selected`, JSON `{ "projectIds": ["..."] }`, 사용자 입력 오류와 stale/scope mismatch는 privacy-safe 422, busy는 429다.
- File: TASK-EXPORT-001의 프로젝트 sheet·allowlist·typed value·formula safety·permission별 매출 컬럼·파일명 규칙을 재사용한다. filter summary에는 개인/프로젝트 식별자를 넣지 않고 `선택 프로젝트 N건`만 기록한다.
- Audit: 성공에만 `ProjectsSelected`, selected row count, filtersApplied false, sensitive sales included flag를 기록한다. request ID·프로젝트명·파일 bytes는 기록하지 않는다.

## 7. 보류·제외

- 현재 보이지 않는 page까지의 selection, server-side “필터 결과 모두 선택”, 선택 저장·공유는 후속 Task.
- 선택 전용 컬럼 picker, multi-sheet, CSV/PDF, async job/storage/re-download는 제외.
- Deleted tab, 실제 사용자 data, Persistent UAT, provider, 대표 repo, push·PR·merge는 제외.

## 8. Review resolution 결론

위 8개 Finding을 2차 기획의 필수 계약과 검증 위치로 통합하면 blocking decision은 0이다. Fable 2차 기획은 1차 원문을 수정하지 않고 `docs/23-selected-project-export-plan.md`에 최종 구현 source of truth를 새로 작성한다. 이 실험 Task는 2차 기획의 blocking decision이 0이면 별도 사용자 확인 없이 구현·검증·desktop/mobile 페이지 screenshot·실제 Excel screenshot·local commit까지 진행한다.
