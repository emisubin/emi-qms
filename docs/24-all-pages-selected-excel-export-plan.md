# EMI QMS 전 페이지 선택 Excel 내보내기 — Fable 2차 기획 (최종 구현 계약)

> 상태: 2차 기획 Draft (experiment fast-track, 구현 계약)
> 작성 단계: Codex 내용 review resolution 반영 완료, 구현 세션 시작 전
> 목적: 1차 기획의 유지 판정 내용과 Codex review 8건 resolution을 하나의 authoritative 구현 계약으로 통합

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-EXPORT-001-ALL-PAGES`
- authoringModel: `FABLE_5`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-002`
- interviewSource: `tasks/export-001-all-pages-interview.md`
- firstPlanningSource: `tasks/export-001-all-pages-planning.md`
- codexReviewSource: `tasks/export-001-all-pages-review.md`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewUserConfirmed: true

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 `experiment/task-export-001-all-pages-selected-export` branch 한정의 최종 구현 source of truth이며, 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider·게시에 대한 어떤 승인도 부여하지 않는다(main merge 승인 `0/3`). 1차 기획과 Codex review 원문은 수정하지 않고 판단 이력으로 보존한다.

## 0. 문서 지위와 반영 원칙

- 이 2차 기획은 1차 기획(`tasks/export-001-all-pages-planning.md`)의 유지 판정 내용을 보존하고, Codex review(`tasks/export-001-all-pages-review.md`)의 Finding 8건 resolution을 전부 본문 계약으로 흡수한 단일 구현 계약이다. 구현 세션은 이 문서만으로 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있어야 하며, 1차 기획과 review는 근거 확인용으로만 참조한다.
- 사용자가 확정하지 않은 새 정책은 만들지 않았다. 비차단 선택은 사용자 standing experiment 규칙(권장안 자동 채택)에 따라 Repository 근거와 trade-off를 남기고 확정했다(18장).
- Interview 문서에 없는 사용자 답변을 추측하지 않았다. 2차 기획 작성은 planning·구현의 게시 승인이 아니다.

## 1. 한 줄 목표

반복 가능한 업무 데이터가 있는 모든 사용자-facing page(관리자 목록 포함 20개)에서 사용자가 checkbox(전체선택 포함)로 항목을 고르고, 화면당 단 하나의 `선택 Excel 내보내기` action으로 선택한 항목만 담긴 안전한 `.xlsx`를 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 공통 Excel 기반(`TASK-EXPORT-001` Phase 1)은 프로젝트 목록·구매 dashboard·내 업무 3개 화면의 "현재 필터 전체 export"(GET)만 제공하고, `TASK-EXPORT-002`가 프로젝트 목록에만 선택 export(POST)를 추가했다.
- 그 결과 프로젝트 page는 같은 목적의 export action이 2개 보이고, 생산관리·자재·키팅·제조·품질·물류·Pending·알림과 관리자 목록에는 Excel 내보내기가 전혀 없다.
- 사용자는 필요한 행만 고른 파일을 원하며, 전체 export와 선택 export가 병존하면 어떤 버튼을 눌러야 할지 혼동하고 의도치 않은 대량 파일이 만들어진다.
- 사용자 직접 지시: 모든 page의 Excel 내보내기 여부를 확인해 전부 구현하되, 전체 내보내기 버튼은 삭제하고 checkbox 전체선택을 포함한 선택 내보내기 버튼 하나만 남긴다.

## 3. Codex review resolution 반영표

Review Finding 8건은 모두 본 계약에 다음과 같이 반영되어 종결된다. 이 표는 요약이 아니라 반영 위치의 색인이다.

| Finding | Severity | 본 문서 반영 위치와 계약 |
| --- | --- | --- |
| `ALL-EXPORT-ADMIN-OMISSION` | P1 | 관리자 반복 목록 8개를 선택 export 대상에 포함(7.2장). 1차안의 "관리자 8종 보류" 판정을 폐기 |
| `ALL-EXPORT-API-SCOPE-EXPANSION` | P1 | 기존 GET 전체 export 3종의 endpoint·service·helper 계약은 보존하고 UI 사용처(버튼)만 제거(11.4장). 1차안의 endpoint 삭제 판정을 폐기 |
| `ALL-EXPORT-AUTHORITATIVE-ADAPTER` | P1 | 공통 selected export orchestration + 화면별 authoritative server adapter 구조로 확정(11.1~11.2장). generic client row payload 수신과 화면별 복사 구현을 모두 금지 |
| `ALL-EXPORT-ADMIN-PRIVACY-PROJECTION` | P2 | 관리자 화면별 bounded display field allowlist와 명시적 제외 목록 확정(7.2장, 9장) |
| `ALL-EXPORT-INVENTORY-DRIFT` | P2 | `View` union 전수(39 view kind)를 machine-readable registry로 고정하고 미분류 시 실패하는 test 계약(7.4장, 16장) |
| `ALL-EXPORT-SELECTION-PAGINATION` | P2 | 전체선택 = 현재 로드된 selectable row로 고정, checkbox label로 범위 명시, 전체 dataset 선택은 Deferred(8장) |
| `ALL-EXPORT-EVIDENCE-COVERAGE` | P2 | 대상 20개 page 전부 desktop 1440·mobile 390 screenshot + 실제 Excel workbook 2종(업무 1·관리자 1) 확인·close(16장) |
| `ALL-EXPORT-UI-SEMANTICS` | P3 | button label을 모든 page·모든 폭에서 `선택 Excel 내보내기` 하나로 통일, 전체선택은 checkbox로만 표현(8장) |

Review의 유지 판정 항목(대상 업무 page 12, 선택 UX·snapshot·잠금, 전부-or-전무 재검증, explicit allowlist, formula 0, 2-slot gate, append-only audit, projection/detail/form 분류, mobile inline tray)은 1차 기획 내용 그대로 본 계약에 보존한다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 각 page 조회 권한 보유 사용자 | row 선택과 선택 export 실행 | 해당 page list/queue endpoint와 동일한 permission·scope | 없음 (export는 read-only) |
| 매출 열 열람자 | 프로젝트 계열 export에 매출 열 포함 | `ProjectSalesAmountRead` 보유 시에만 열 존재 | 없음 |
| System Administrator | 관리자 목록 8개의 선택 export 실행 | 각 관리자 endpoint의 기존 admin policy와 동일 | 없음 (mutation 우회 없음) |

- 신규 permission을 만들지 않는다. 각 selected export endpoint는 대응하는 list/queue endpoint와 정확히 같은 permission·scope·soft-delete/visibility 조건을 재사용한다.
- 확인된 사실: 프로젝트·구매 계열은 `ProjectRead`+`ProjectAccessScope`, 매출 열은 `ProjectSalesAmountRead`, 내 업무·알림은 인증 사용자 본인 고정, 관리자 계열에는 `UsersManage`·`AdminHistoryRead` 등 기존 admin policy가 존재한다. 자재·키팅·제조·품질·물류·Pending queue와 관리자 화면별 정확한 policy 명칭은 구현 시 각 endpoint에서 재확인해 동일하게 재사용한다(추측 금지).
- Frontend checkbox 표시는 보조 수단이고 서버 policy가 최종 차단 지점이다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 프로젝트 목록 (단일 action)

1. 사용자가 프로젝트 목록에서 header 전체선택 또는 개별 checkbox로 프로젝트를 고른다. 기존 "현재 필터 Excel 내보내기" 버튼은 더 이상 보이지 않는다.
2. 선택 tray에 `N개 선택`이 표시되고 사용자가 `선택 Excel 내보내기`를 실행한다.
3. 시스템이 선택 ID snapshot을 서버로 보내 권한·scope를 전부-or-전무 재검증하고 선택 row만 담긴 파일을 내려준다.

### 시나리오 B — 품질 검사 (stage·group 화면)

1. 사용자가 LQC tab에서 프로젝트 group header checkbox로 한 프로젝트의 panel 전체를, 다른 프로젝트에서는 개별 panel만 고른다.
2. tray의 `현재 목록 전체 선택` checkbox는 현재 stage에 로드된 selectable panel 전체를 대상으로 하며 일부 선택 시 indeterminate로 표시된다.
3. `선택 Excel 내보내기` 실행 시 현재 stage와 선택 panel ID snapshot이 전송되고, 파일에는 해당 stage의 선택 panel row만 포함된다.

### 시나리오 C — 관리자 사용자 관리 (safe projection)

1. System Administrator가 사용자 관리 목록에서 일부 계정을 선택해 `선택 Excel 내보내기`를 실행한다.
2. 서버가 기존 admin policy를 재검증하고, 화면에 이미 표시되는 업무 필드(표시명·업무 이메일·부서·역할·상태)만 allowlist로 담은 파일을 생성한다.
3. Entra object/tenant ID, 내부 GUID, claim 원문은 어떤 열에도 존재하지 않는다.

### 시나리오 D — stale 선택 복구

1. 사용자가 선택한 항목이 그 사이 다른 사용자에 의해 상태 변경·삭제되어 조회 조건을 벗어난다.
2. 서버가 요청 수와 조회 수 불일치를 감지해 파일·audit 없이 generic 422를 반환한다.
3. 화면은 선택을 유지한 채 "목록을 새로고침한 뒤 다시 선택해 주세요"를 action 근처 `aria-live` 영역에 안내한다.

## 6. 기능 요구사항

### 필수

- [ ] 7장 inventory의 `선택 export 대상` 20개 page 전부에 checkbox 선택·전체선택·indeterminate·0건 disabled·단일 `선택 Excel 내보내기` action을 제공한다.
- [ ] 기존 전체(필터) export action을 UI에서 0개로 만든다(프로젝트 목록·구매 dashboard·내 업무). 해당 GET API 계약은 보존한다.
- [ ] 모든 selected request를 서버가 동일 permission·scope·visibility 조건으로 전부-or-전무 재검증한다.
- [ ] 공통 selected export orchestration(validation·gate·workbook·audit·응답)을 한 경로로 두고, 화면별 adapter는 권한·재조회·열 allowlist·audit kind만 제공한다.
- [ ] 기존 workbook builder·formula-safe writer·10,000행 cap·2-slot no-wait gate·append-only audit을 재사용한다.
- [ ] `View` union 전수의 machine-readable inventory registry와 미분류 실패 test로 누락 0을 증명한다.

### 선택

- [ ] 선택 수가 화면에 남아 있는 동안 같은 snapshot 재실행(성공 후 선택 유지에 의한 재다운로드).

### 명시적 제외

- [ ] column picker, multi-sheet, CSV/PDF/ZIP, 비동기 job·파일 storage·재다운로드 링크·정기 발송.
- [ ] server-side "필터 결과 전부 선택"과 현재 로드되지 않은 pagination 전체 선택(별도 후속 기능).
- [ ] create/edit form draft export, 단일 detail snapshot report, 홈·Teams 중복 projection 전용 action, `Deleted` tab export.
- [ ] 기존 Excel import 계약 변경, email/Teams 발송, Persistent UAT·실제 provider·대표 repo·main·push·PR·merge.

## 7. 전 page inventory — 39 view kind 전수 분류

Frontend `View` union의 view kind 전수(39종)를 다음 세 분류로 고정한다: `선택 export 대상 20` / `상위·원본 page가 담당 4` / `선택 대상 없음 15`. 1차안의 "관리자 8종 명시적 보류" 분류는 review resolution에 따라 폐기하고 대상으로 전환한다.

### 7.1 선택 export 대상 — 업무 page 12

| # | Page (route) | 선택 단위·stable key | 화면 구조 | 재조회 경계(stale 선택 제거 기준) | 열 allowlist 방향(구현 시 화면 표시 필드로 확정) |
| --- | --- | --- | --- | --- | --- |
| 1 | 프로젝트 목록 `/projects` | 프로젝트 `projectId` | table/card, status tab | 검색·tab·납기 범위·reload (기존 계약 유지) | 기존 `ProjectColumns` 그대로(매출 gate 포함) |
| 2 | 내 업무 `/my-work` | 업무 `workItemId` (본인 소유 고정) | status tab 목록 | tab 변경·reload | 기존 `MyWorkColumns` 그대로 |
| 3 | 생산관리 목록 `/production-planning` | 프로젝트 `projectId` | 검색 목록 | 검색·reload | Code·Title·고객·Item·면수·납기·계획상태 label·제품유형·필수단계 계획율·담당자 수 |
| 4 | 구매 dashboard `/procurement` | 프로젝트 요약 `projectId` | 검색·입고예정일 필터 | 검색·날짜·reload | 기존 `ProcurementColumns` 그대로 |
| 5 | 자재 입고 `/materials/receipts` | 구매품목 `itemId` | 검색·완료 포함·날짜·사급 tab | 필터 전체·reload | Code·Title·발주품목·업체·사급 구분·입고예정일·발주/도착/확정/잔여 수량·마감·완료 여부 (개별 receipt 원문·비고 제외) |
| 6 | 키팅 `/materials/kitting` | 패널 `panelId` | 프로젝트 group→panel | project 필터·reload | Code·Title·패널 표시코드·패널명·정보 완료·키팅 완료·완료일시·완료자 표시명 |
| 7 | 제조 `/manufacturing/work` | 패널 `panelId` | 프로젝트 group→panel | project 필터·reload | Code·Title·패널 표시코드·패널명·단계·상태 label·체크 진행(완료/전체 step 수)·시작일시 |
| 8 | 품질 IQC `/quality/iqc` | IQC 시도 `attemptId` | 판정 포함 toggle 목록 | toggle·reload | Code·Title·발주품목·수량·단위·시도 번호·사급 구분·상태 label·요청/판정 일시·성적서 상태 |
| 9 | 품질 검사 `/quality/inspections` | stage+패널 `panelId` (stage body 동반) | stage tab→프로젝트 group→panel | stage·project 변경·reload | Code·Title·패널 표시코드·패널명·검사 단계 label·시도 번호·상태 label·Pending 번호 |
| 10 | 물류 `/logistics` | stage+대상 `targetId`(Panel/PackingUnit) | stage tab→프로젝트 group→target | stage·project 변경·reload | Code·Title·대상 표시코드·대상 유형 label·제목·상태·포함 panel 수·차단 여부 |
| 11 | Pending `/pending` | 이슈 `pendingId` | 상태·유형·긴급도·프로젝트 필터 | 필터 전체·reload | 이슈 번호·Code·Title·유형 label·제목·상태 label·긴급도 label·조치부서 코드·담당 표시명·기한·지연 여부·등록일 (description·comment 원문 제외) |
| 12 | 알림 `/notifications` | 알림 `notificationId` (본인 수신만) | 읽음 상태 tab, 프로젝트 group | tab·reload | 유형 label·심각도·제목 snapshot·Code·Title·단계명·읽음 여부·생성일시 (본문·deep link 제외) |

### 7.2 선택 export 대상 — 관리자 page 8 (review resolution으로 포함)

모든 관리자 export는 해당 화면에서 이미 권한으로 조회되는 bounded display field만 allowlist로 출력한다. 각 adapter의 정확한 열 이름은 구현 시 실제 화면 표시 필드와 대조해 확정하되(추측 금지), 제외 목록은 아래에 확정한다.

| # | Page (route) | 선택 단위·stable key | 서버 재검증 policy(구현 시 endpoint에서 재확인) | 열 allowlist 방향 | 확정 제외(어떤 열에도 금지) |
| --- | --- | --- | --- | --- | --- |
| 13 | 사용자 관리 `/admin/users` | 사용자 `userId` | `UsersManage` 계열 기존 admin policy | 표시명·업무 이메일·부서·역할/권한 요약 label·계정 상태 label·변경 일시 | Entra object/tenant ID·내부 GUID·claim 원문·삭제 사유 자유서술 |
| 14 | 부서 관리 `/admin/departments` | 부서 `departmentId` | 기존 admin master-data policy | 부서명·코드·상태 label·소속 인원 수·변경 일시 | 내부 GUID·변경 이력 원문 |
| 15 | 휴일 관리 `/admin/calendar/holidays` | 휴일 `holidayId` | 기존 holiday admin policy | 날짜·이름·유형 label·상태 label | 내부 GUID. 기존 Excel 양식 다운로드·preview·apply(import) 계약은 불변이며, read-only 선택 export는 import action과 의미가 다른 별도 action으로 병존한다 |
| 16 | 권한 matrix `/admin/permissions` | permission row(안정 permission key) | 기존 admin policy | permission 표시명 + role별 허용 여부를 typed boolean/표시 label 열로 flatten | 사용자 계정 원문·내부 GUID |
| 17 | 기준정보 변경 이력 `/admin/history/master-data` | 이력 `changeLogId` | `AdminHistoryRead` | entity 표시·action label·actor 표시명·시각·사유 bounded summary | before/after JSON 원문 |
| 18 | 업무 이력 `/admin/history/work-items` | 이력 row key | `AdminHistoryRead` | 업무 제목·프로젝트 표시·단계·action/상태 label·actor 표시명·시각 | payload·변경 원문 |
| 19 | notification delivery `/admin/system/notification-deliveries` | delivery `deliveryId` | 기존 delivery admin policy | 상태·handling 상태·channel·유형 label·제목 snapshot·프로젝트 표시·시각·재시도 count | recipient address·payload snapshot·오류 원문·deep link·내부 ID |
| 20 | 업무 escalation `/admin/system/work-item-escalations` | escalation row key | 기존 escalation admin policy | 상태·level·업무 제목·프로젝트 표시·시각·count | 위 19번과 동일 |

관리자 delivery 상세(`admin-notification-delivery-detail`)는 목록 export가 담당하며 detail 자체는 선택 대상 없음이다.

### 7.3 상위·원본 page가 담당 (4 view kind)

| View kind | 판정 근거 |
| --- | --- |
| `home` | 내 업무·Pending 등 원본 page의 요약 widget. 각 원본 page가 담당 |
| `teams-activity` | 알림의 Teams tab projection. 알림 page 선택 export가 담당 |
| `teams-activity-detail` | 상동 (단일 상세) |
| `teams-notification-detail` | 상동 (단일 상세) |

내 업무의 `AssignedProjects` tab은 프로젝트 row의 요약 projection이므로 프로젝트 목록 선택 export가 담당하고 중복 button을 두지 않는다.

### 7.4 선택 대상 없음 (15 view kind)과 registry 계약

`list`의 `Deleted` tab을 제외한 다음 view kind는 반복 선택 대상이 없는 화면으로 기록한다(“미구현”이 아니다): `create`, `detail`, `sales-settlement`, `deleted-detail`, `edit`, `panel-info-edit`, `production-planning-edit`, `production-planning-settings`, `procurement-edit`, `procurement-settings`, `pending-detail`, `admin-dashboard`, `admin-send-notification`, `admin-notification-delivery-detail`, `panel`.

- 위 세 분류(20+4+15=39)를 machine-readable registry(예: 분류 map 상수)로 코드에 고정하고, `View` union의 모든 kind가 정확히 한 분류에 속하는지 검증하는 test를 둔다. 새 view kind가 추가되면 분류 누락으로 test가 실패해야 한다(type-level exhaustive check 또는 runtime 전수 대조).
- registry는 UI 노출용이 아니라 검증용이며 별도 화면을 만들지 않는다.

## 8. 공통 선택 UX 계약 (모든 대상 page 동일)

- **단일 action·단일 label**: export button은 모든 page·모든 화면 폭에서 label `선택 Excel 내보내기` 하나다. `전체 내보내기`, `현재 필터 Excel`, `Excel 다운로드` 같은 병행 export action은 0개이며, 기존 프로젝트 mobile의 축약 label `선택 Excel`도 `선택 Excel 내보내기`로 통일한다.
- **전체선택은 checkbox로만 표현**: page 전체선택은 tray의 checkbox label `현재 목록 전체 선택`, group 전체선택은 group header checkbox(예: `이 프로젝트 전체 선택`)로 표현한다. 전체선택을 button으로 만들지 않아 export action 두 개로 오해할 여지를 없앤다.
- **전체선택 의미**: 현재 tab/stage/filter에서 실제로 로드된 selectable row 전체다. server pagination이 있는 page에서는 현재 로드된 page의 row만 대상이며 "검색 결과 전체"를 의미하지 않는다. 이 범위를 checkbox label·scope label로 화면에 명시한다. 전체 dataset 선택은 Deferred다.
- **desktop table 화면**(프로젝트)은 기존 header checkbox 열을 유지한다. group/card 화면은 group header checkbox(해당 group의 loaded selectable child 전체 + indeterminate)와 tray의 page 전체선택 checkbox(+ indeterminate)를 둔다. 같은 의미의 checkbox·action을 중복 배치하지 않는다.
- **선택 tray**: 기존 프로젝트 tray를 공통 component로 일반화한다 — `현재 목록 전체 선택 checkbox` + `N개 선택`(`aria-live`) + `선택 Excel 내보내기` + `선택 해제`만 노출. 0건이면 export 비활성과 이유 표시.
- **selection lifecycle**: 실행 시 ID snapshot 고정, 실행 중 선택 변경·전체선택·해제·재실행·필터 변경 잠금, 성공·실패 후 선택 유지, 필터·tab·stage·reload·새 response 수락 시 visible ID 밖 선택 값 제거, 명시적 `선택 해제` 제공.
- **Mobile 390px**: 카드 상단 충분한 touch target checkbox, 목록 바로 위 compact inline tray. fixed bottom bar 금지, page-level horizontal overflow 0, 기존 좌상단 숨김 메뉴 규칙 보존.
- **접근성**: checkbox control은 row/card의 열기·시작 action과 click·keyboard event를 완전히 분리한다. 각 checkbox는 row 식별 접근성 label(표시 코드 기반)을 가진다. 선택 수는 `aria-live`, focus 순서 유지.
- **feedback 문구**: 성공 `Excel 파일 생성을 완료했습니다`, 선택 422 hint `목록을 새로고침한 뒤 다시 선택해 주세요`, 429 재시도 안내 — 기존 `ExcelExportAction` 계약 유지.

## 9. 업무 규칙과 불변조건

- 파일에는 사용자가 선택한 row만 존재한다. 선택하지 않은 row 0, 부분 성공 0, per-ID 실패 원인 노출 0.
- Backend가 permission·scope·visibility·상한·열 allowlist의 authoritative layer다. Frontend 선택 상태·row payload·DOM을 workbook source로 신뢰하지 않는다.
- 검증 실패 시 파일·성공 audit·성공 feedback을 만들지 않는다(전부-or-전무). 실패·차단(401/403/422/429)은 audit 0건.
- 모든 자유 문자열은 text cell로 기록하고 생성 파일 재파싱 시 formula 객체 0을 유지한다.
- 내부 GUID·Entra object/tenant ID·감사 원문(before/after JSON·payload snapshot)·자유서술(description·comment·사유 원문)·첨부 bytes·recipient address·deep link를 어떤 열에도 넣지 않는다.
- 개인 식별 필드의 경계(review resolution으로 확정): 업무 page에서는 화면 표시 label·상태·일자·수량만 출력한다. 관리자 사용자 관리에서는 해당 화면이 이미 권한으로 표시하는 표시명·업무 이메일을 bounded 업무 필드로 출력할 수 있으며, 이것이 유일한 예외다. 이 경계는 제품의 authorized export 데이터 정책이며, 검증 증빙 기록에는 별도로 `docs/development/privacy-safe-evidence.md`가 계속 적용된다(실제 원문 대신 synthetic data·aggregate만).
- export는 domain write 0, 알림 0, workflow 상태 전이 0. 선택 상태는 화면 로컬 임시 상태이며 서버에 저장하지 않는다.
- 기존 Excel import(프로젝트·패널정보·구매·생산계획·휴일)와 품질/제조/물류/Pending mutation·lifecycle 계약은 불변이다.
- 전체 export UI 제거 후에도 각 page의 조회·기존 업무 흐름과 기존 GET export API 계약은 그대로 동작한다.

## 10. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `data_export_events` | append-only export 성공 audit | 기존 (`0038`·`0039`) | update/delete trigger 유지 |
| export kind allowlist | check constraint의 화면별 고정 enum | 신규 kind 19종 추가 | additive migration으로 constraint drop/recreate만 |
| 화면별 선택 상태 | `Set<id>` 로컬 UI 상태 | 신규 (client-only) | 서버 저장·감사 없음 |

- 신규 audit kind 19종(화면별 explicit enum): 업무 11종 `MyWorkSelected`, `ProductionPlanningSelected`, `ProcurementDashboardSelected`, `MaterialReceiptsSelected`, `PanelKittingSelected`, `ManufacturingSelected`, `QualityIqcSelected`, `QualityInspectionsSelected`, `LogisticsSelected`, `PendingSelected`, `NotificationsSelected` + 관리자 8종 `AdminUsersSelected`, `AdminDepartmentsSelected`, `AdminCalendarHolidaysSelected`, `AdminPermissionMatrixSelected`, `AdminMasterChangeLogsSelected`, `AdminWorkHistorySelected`, `AdminNotificationDeliveriesSelected`, `AdminWorkItemEscalationsSelected`. `ProjectsSelected`는 기존 그대로 재사용한다.
- 기존 kind(`Projects`, `ProcurementDashboard`, `MyWork`)는 GET API가 보존되므로 historical이 아니라 **활성 kind로 유지**한다(1차안 대비 변경: endpoint 보존에 따름).
- `0038`·`0039`는 수정하지 않고 다음 additive migration(현재 ledger 최신 `0039` 다음 번호 — 구현 시 재확인, 예상 `0040`) 1건으로 `ck_data_export_events_kind`를 확장한다. `ck_data_export_events_sensitive_columns`의 매출 flag 허용 kind는 `Projects`/`ProjectsSelected`로 변경 없이 유지한다(신규 화면은 매출 열 없음).

```text
선택 0건 → 선택 N건 (개별/그룹/전체선택) → 실행 snapshot·잠금 → 성공(선택 유지) | 실패(선택 유지 + 복구 안내)
```

## 11. API·Backend 계약

### 11.1 공통 selected export orchestration (한 경로)

review resolution에 따라 다음을 화면별로 복사하지 않고 `DataExports` 공통 경로 하나에 둔다.

- **공통 body validation**: 항목별 trim → GUID/키 parse → 빈/누락 body·0건·malformed·중복·상한 초과를 동일한 422 validation contract(title+field error+한글 안내)로 반환. 선택 상한은 전 화면 공통 unique 1,000건/request이며 기존 프로젝트 100건 상한도 1,000건으로 통일한다. Frontend는 현재 response에 존재하는 selectable row만 보낼 수 있고 Backend exact-match가 최종 검증한다.
- **공통 실행**: `ExcelExportConcurrencyGate` 2-slot no-wait 재사용(포화 429), `CancellationToken` 전파·slot 누수 0, workbook builder 호출, 성공 시에만 `DataExportAuditStore.AppendSuccessAsync` 1건(화면별 kind, row count, `filtersApplied` false 고정, 매출 flag는 프로젝트 계열만).
- **공통 응답**: `.xlsx` content type·UTF-8 파일명(`EMI_<화면명>선택_<timestamp>.xlsx` 계열)·`X-Export-Row-Count`·기존 `ToResult` 상태 매핑(성공/10,000행 초과 422/429/선택 422) 재사용.

### 11.2 화면별 authoritative adapter (얇은 계약)

화면별 adapter는 다음만 제공하며 generic client row payload를 authoritative data로 받지 않는다.

1. 해당 화면 list/queue endpoint와 동일한 permission·scope 검증.
2. 화면별 store에 기존 list/queue query 경로(동일 permission·scope·soft-delete/visibility·정렬)에 ID 집합 조건만 추가한 **단일 조회**. 조회 수가 요청 unique 수와 정확히 일치할 때만 workbook을 생성하고, 불일치 시 generic 422("선택한 항목 중 내보낼 수 없는 항목이 있습니다…")·파일·audit 0건. stage 동반 화면(품질 검사·물류)은 stage 조건을 함께 재적용한다. 단건 반복 조회와 export 전용 별도 SQL 금지.
3. explicit 열 allowlist(7장 표)와 sheet/화면명, audit kind.

### 11.3 endpoint

- endpoint route는 화면별로 유지한다(신규 mutation-free POST 19개: 업무 11 + 관리자 8). body 기본형은 `{ "ids": [...] }`이며 품질 검사는 `{ "stage": "...", "panelIds": [...] }`, 물류는 `{ "stage": "...", "targetIds": [...] }`처럼 화면 조회 경계를 함께 받는다. 정확한 route·property 이름은 기존 `POST /api/projects/export/selected` 관례와 각 영역 route group에 맞춰 구현 시 확정한다(예상 관례: `/api/my-work/export/selected`, `/api/admin/users/export/selected` 등).
- 기존 `POST /api/projects/export/selected`는 공통 orchestration으로 이관하되 동작 계약(권한·422 문구·응답·audit)은 기존 테스트로 회귀를 고정한다.

### 11.4 기존 GET 전체 export 3종의 처리 (review resolution)

- `GET /api/projects/export`, `GET /api/procurement/dashboard/export`, `GET /api/my-work/export`의 **endpoint·service·helper 계약과 테스트는 보존**한다. 1차안의 endpoint 삭제 판정은 폐기한다.
- 제거하는 것은 Frontend UI 사용처(버튼) 3곳뿐이다. 사용자에게 노출되는 export action은 `선택 Excel 내보내기` 하나이며, GET 경로는 API 호환·rollback 경계 보존용으로 남는다. Frontend GET helper 함수는 계약 보존 대상으로 유지하고 신규 UI에서 호출하지 않는다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서로 확정하지 않는다(위 명칭은 확인된 기존 코드 기준이며, 신규 명칭은 관례 표시다).

## 12. Frontend 계약

- **공통화**: 프로젝트에 구현된 선택 상태·tray·checkbox 계약을 공통 hook(`useRowSelection` 계열)과 공통 tray component로 추출하고, `ExcelExportAction`·`downloadExcelExport`의 POST 확장·선택 전용 422 hint·busy 전달을 그대로 재사용한다. 프로젝트 page는 공통 모듈로 이관하되 동작 계약(초기화·유지·잠금·snapshot)은 기존 테스트로 회귀를 고정한다.
- **각 대상 page**: loading/empty/error 상태에서는 선택 UI를 렌더하지 않고, 새 response 수락 시 visible ID 밖 선택 값을 항상 제거한다. group header checkbox는 selectable child만 대상으로 한다(예: 키팅의 selectable 아님 panel 제외).
- **분리된 page component**(자재·키팅·제조·품질·물류·Pending 등)와 `App.tsx` 내 page(내 업무·생산관리·구매·알림·관리자 8종)에 공통 모듈을 배치한다.
- **기존 전체 export 버튼 3개 제거**: 프로젝트 목록·구매 dashboard·내 업무의 GET `ExcelExportAction` 사용처를 제거하고 선택 tray로 대체한다(helper는 11.4장 계약으로 보존).
- **label**: 8장 단일 label·checkbox 표현 계약을 모든 page에 적용한다.

## 13. 기존 기능과의 연결

- 프로젝트/업무/알림: 각 page의 조회·시작/완료·읽음 처리 흐름은 불변. 선택 UI는 조회 위에만 얹힌다.
- 권한/관리자: 신규 permission 0. 관리자 8개 화면은 기존 admin policy 재검증으로 포함(7.2장).
- Excel/PDF/첨부: 기존 import 5계열(휴일 포함)과 IQC/검사 PDF 계약 불변. workbook builder·formula-safe writer 재사용. 휴일 화면의 양식 다운로드·import action은 선택 export와 의미가 달라 병존한다.
- Teams/Mail: 영향 없음(발송 0).
- 삭제·복구/감사: soft-delete 제외 조건 재사용, `data_export_events` append-only 유지, `Deleted` tab 제외 유지.

## 14. 확정 구현안

1차안의 후보 A(화면별 explicit endpoint + explicit adapter + 화면별 audit kind)를 유지하되, review resolution으로 **공통 orchestration 계층을 추가**한 A′로 확정한다.

| 구성 | 확정 내용 | 근거 |
| --- | --- | --- |
| endpoint | 화면별 route 유지(권한·scope를 각자 정확히 재사용) | 핵심 위험인 "화면별 권한·scope 재검증 누락"을 구조적으로 차단 |
| orchestration | validation·gate·workbook·audit·응답은 공통 한 경로 | 19개 화면 반복 구현의 누락·불일치 위험 제거 |
| data source | server adapter의 기존 query 재조회만. generic client payload 금지 | 위조 data·audit 의미 약화 차단 |
| audit | 화면별 explicit kind + DB constraint allowlist | 기존 패턴 일치, DB가 allowlist 강제 |

generic 단일 endpoint + screen key 방식(1차안 후보 B)은 계속 보류한다.

## 15. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용, 신규 migration도 Persistent UAT 미적용.
- migration 필요 여부: audit kind constraint 확장 additive 1건만(예상 `0040`, ledger로 재확인). `0038`·`0039` 수정·번호 재사용 금지.
- 외부 발송/실제 데이터 영향: 없음. 실제 사용자·고객·프로젝트 원문으로 export를 실행하지 않으며, 관리자 화면 증빙도 isolated synthetic 계정만 사용한다.
- runtime 교체 여부: 없음. canonical runtime·5174 handover 불포함.
- 추가 사용자 승인 필요 작업: push·PR·merge(미승인), main merge(승인 `0/3`), Persistent UAT·실제 provider. 이 문서는 어떤 게시 승인도 부여하지 않는다.

## 16. 검증 계획

- **Backend 화면별 계약 테스트**(20개 대상 전부): 권한 없음 403·미인증 401, 공통 validation 422 동일 계약(빈/0건/malformed/중복/1,001건), scope 밖·삭제·미존재·stage 불일치 혼입 각각 generic 422+파일·audit 0건, 성공 시 선택 row만 존재·화면 정렬 동일, 매출 열 gate(프로젝트 계열), formula 재파싱 0, 429 포화·slot 누수·취소 전파, 화면별 audit kind·row count 기록, append-only trigger.
- **관리자 privacy 계약 테스트**(8개 화면): 생성 workbook 재파싱으로 7.2장 확정 제외 필드(Entra ID·내부 GUID·recipient·payload·before/after JSON·오류 원문·deep link) 부재를 명시적으로 검증.
- **기존 API 회귀**: 보존된 GET export 3종의 기존 계약 테스트 유지·통과, 기존 목록·queue·import(휴일 포함) 회귀, 기존 `ProjectsSelected` 회귀(상한 1,000 반영 갱신).
- **migration**: fresh/upgrade(기존 isolated DB) 검증, ledger 충돌 0.
- **Frontend**: 공통 선택 hook/tray unit(개별·그룹·전체선택·indeterminate·0건 disabled·초기화 경계·visible 밖 제거·성공/실패 유지·실행 잠금·snapshot), 페이지별 checkbox와 row action 분리(mouse·keyboard), 전체 export 버튼 부재 확인(각 page export action 정확히 1개), 단일 label 계약, lint·typecheck·build.
- **Inventory registry**: 7.4장 registry test — `View` union 39 kind 전수가 정확히 한 분류에 속하고 대상 20개가 모두 adapter·UI를 가지는지 실패 가능하게 고정.
- **E2E(isolated Full-Stack)**: 프로젝트·품질 검사·자재 입고·관리자 사용자 4개 시나리오에서 일부 선택 → 다운로드 파일 재파싱으로 선택 row만 존재·formula 0·(관리자) 제외 필드 부재 확인. desktop·390px page-level horizontal overflow 0.
- **Screenshot 증빙(review resolution)**: 대상 20개 page **전부** desktop 1440·mobile 390에서 checkbox·전체선택·단일 action이 보이는 screenshot을 synthetic data로 자동 수집한다(대표 4개 방식 폐기). 실제 Microsoft Excel UI로는 구조가 다른 workbook 2종(업무 1: 품질 검사 계열, 관리자 1: 사용자 관리 계열)을 열어 시각 확인 screenshot을 남기고 모두 닫았음을 기록한다.
- **사용자 검수**: 자동 검증 완료 후 `사용자 검수 대기`로 종료한다. 완료로 표시하지 않는다.

## 17. 완료 기준과 중단 조건

완료 기준:

- 기능/권한/데이터: 20개 대상 page 전부에서 선택 export가 동작하고, 전체 export UI 0개·부분 성공 0·scope 우회 0·formula 0·내부 GUID/확정 제외 필드 0·domain write 0. 보존된 GET API 3종 회귀 0.
- UX: desktop·390px에서 checkbox·전체선택·indeterminate·잠금·복구 안내가 동작하고, export action이 화면당 정확히 1개·label이 전 화면 동일하다.
- 자동 테스트: 16장 계약 전부 통과, registry 누락 0, 기존 회귀 0.
- 증빙: 20개 page desktop/mobile screenshot 전량과 실제 Excel workbook 2종 확인·close 기록.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적(`docs/12-task-completion-policy.md` 기준).
- 사용자 검수 상태: `대기`. Git: local experiment commit까지만(push·PR·merge 없음).

중단 조건: 권한·scope 우회, 부분 성공 파일, 선택 외 row 포함, per-ID 원인 노출, 확정 제외 필드 출력, formula 미방어, migration ledger 충돌, 기존 import·업무 mutation·보존 GET API 회귀, Repository 계약 충돌 발견 시 구현을 중단하고 blocking으로 보고한다.

## 18. 확정한 비차단 결정 (standing rule 자동 채택, review resolution 반영본)

| 번호 | 항목 | 확정 내용 | 근거 |
| ---: | --- | --- | --- |
| 1 | 전 page 판정 | 39 view kind 전수: 대상 20(업무 12+관리자 8) / 상위·원본 담당 4 / 선택 대상 없음 15 / 보류 0 | 사용자 "모든 page" 직접 지시, review P1 resolution, 실제 route·data model 전수 대조 |
| 2 | 전체선택 의미·lifecycle | 현재 tab/stage/filter에서 로드된 selectable row 전체(현재 loaded page 한정). group checkbox는 group 한정. 필터·tab·stage·reload·새 response에서 stale 제거, 성공·실패 후 유지, 명시적 해제 | 보이는 것과 파일의 일치, `TASK-EXPORT-002` 확정 계약 일반화, review resolution 6 |
| 3 | 공통 UX 계약 | 공통 선택 hook + inline tray + `ExcelExportAction` 재사용, 단일 label `선택 Excel 내보내기`, 전체선택은 checkbox로만 | 중복 구현 방지, review resolution 8 |
| 4 | stable key·검증 | 화면별 기존 key(7장 표), 공통 orchestration + adapter의 서버 단일 query 전부-or-전무 exact count, stage 재적용 | fail-closed, review resolution 3 |
| 5 | 기존 GET 전체 export | endpoint·service·helper·테스트 보존, UI 사용처만 제거 | 사용자 요구는 button 중복 0이며 API 삭제가 아님. review P1 resolution |
| 6 | audit kind 방식 | 화면별 explicit selected kind 19종 + additive constraint 확장, 기존 GET kind 활성 유지 | 기존 패턴 일치, DB allowlist 강제 |
| 7 | 선택 상한 | 전 화면 공통 unique 1,000건/request(프로젝트 100→1,000 통일), workbook 10,000행 cap 유지, grouped parent는 자체 row 없이 children만 | bounded body, 단일 상한 UX 일관 |
| 8 | 증빙 조합 | 대상 20개 전부 desktop 1440·390px screenshot + 실제 Excel workbook 2종(업무 1·관리자 1) 확인·close | fast-track page별 screenshot standing rule, review resolution 7 |

안전상 blocking 결정은 발견되지 않았다. 사용자 확인이 필요한 신규 정책은 0건이다.

## 19. 예상 변경 범위 (확정 allowlist가 아니라 조사 대상)

- Backend: `DataExports` 모듈(공통 orchestration·validation·응답), 화면별 store의 선택 재조회 overload(프로젝트·구매·workflow·자재·키팅·제조·품질·물류·Pending·알림·관리자 계열), 화면별 endpoint 19개 추가, 기존 GET 3종·프로젝트 selected 보존·이관.
- Frontend: 공통 선택 hook/tray, `ExcelExportAction`, `App.tsx` 내 page들과 분리된 page component(자재·키팅·제조·품질·물류·Pending), 관리자 page 8종, `api.ts` selected helper 19종 추가(GET helper 3종 보존).
- DB/Migration: additive constraint 확장 1건(예상 `0040`).
- Tests/Scripts: Backend 계약·privacy·migration 테스트, Frontend unit, inventory registry test, isolated Full-Stack E2E 4종, 20개 page screenshot 자동 수집.
- Docs: Roadmap `TASK-EXPORT-001`/`TASK-EXPORT-002` 실험 상태 갱신(진행 시점), 후속 SOP·user manual.

## 20. Roadmap 연결

- 선행 Task: `TASK-EXPORT-001` Phase 1(공통 기반), `TASK-EXPORT-002`(선택 UX 선행 구현 — 본 변경이 일반화해 흡수하되 판단 이력·commit 보존).
- 후속 Task(별도 분리): server-side "필터 결과 전체 선택"·미로드 pagination 전체 선택, column picker·multi-sheet·CSV/PDF/ZIP, 비동기 job·storage·정기 발송, detail snapshot report.
- 현재 Go/No-Go: experiment fast-track 한정 Go(사용자 직접 지시·명시적 순서 override가 change에 기록됨). canonical 실행 큐의 다음 `TASK-007A` Gate와 4.3/4.3A 기록 원칙은 변경하지 않는다.

## 21. Codex 구현 지시문 (review 권장 순서 반영)

1. 공통 selected export orchestration(공통 body validation trim→parse→중복/0건/1,001건 422, gate, workbook, audit, filename/response)과 공통 선택 hook/tray를 추출하고, 기존 프로젝트 selected 경로를 이관해 회귀를 고정한다(상한 100→1,000 통일 포함).
2. additive migration(예상 `0040`, ledger 재확인)으로 신규 selected kind 19종을 `ck_data_export_events_kind`에 추가하고 fresh/upgrade·append-only 테스트를 확장한다.
3. 기존 GET 전체 export 3종의 UI 사용처만 제거하고 API·helper·테스트 회귀를 보존한다. 각 page의 export action이 정확히 1개임을 테스트로 고정한다.
4. 업무 page 12개 adapter(기존 list/queue query + ID 집합 + exact count + 7.1장 열 allowlist)·선택 UX·화면별 권한/scope/422/audit 계약 테스트를 7.1장 표 순서로 추가한다.
5. 관리자 page 8개 safe projection adapter(7.2장 allowlist·확정 제외)·선택 UX·privacy 계약 테스트를 추가한다.
6. inventory registry(39 kind 전수 분류)와 미분류 실패 test를 고정하고 Backend·Frontend 전체 회귀를 실행한다.
7. isolated Full-Stack E2E 4종(프로젝트·품질 검사·자재 입고·관리자 사용자)과 대상 20개 page의 desktop 1440·390px screenshot을 synthetic data로 수집한다.
8. 실제 Microsoft Excel로 업무 1종·관리자 1종 workbook을 시각 확인·screenshot 후 닫았음을 기록하고, implementation report·5종 산출물 상태·Roadmap 실험 상태를 갱신한 뒤 local experiment commit까지만 수행한다(push·PR·merge·Persistent UAT·provider 금지).

---

- planningApproved: 조건부(본 2차 기획이 review resolution 전부 반영·blocking 0인 조건의 experiment 한정 승인, `tasks/export-001-all-pages-change-001.md`)
- implementationApproved: experiment branch 한정 true(같은 change 계약), 대표 repo·main·게시 승인 아님
- persistentUatApproved: false
- mainMergeApprovalCount: `0/3`

openBlockingDecisionCount: 0
