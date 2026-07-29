# TASK-EXPORT-001 Change 002 — 전 페이지 선택 Excel 내보내기 1차 기획안

> 상태: Draft (experiment fast-track 1차 기획)
> 작성 단계: Codex 내용 review 전
> 목적: 모든 사용자-facing 데이터 page의 선택형 Excel 내보내기 통합 계약 확정

- taskType: `NEW_FEATURE`
- canonicalTaskId: `TASK-EXPORT-001`
- canonicalChangeId: `change-002`
- authoringModel: `FABLE_5`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/export-001-all-pages-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 `experiment/task-export-001-all-pages-selected-export` branch 한정이며 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 프로젝트 page에는 의미가 겹치는 "현재 필터 Excel 내보내기"와 "선택 Excel 내보내기" 두 action이 함께 보이고, 나머지 주요 업무 page에는 Excel 내보내기가 없다.
- 사용자 실행 지시(원문 기준): 모든 page의 Excel 내보내기 여부를 확인해 전부 구현하되, 전체 내보내기 버튼은 삭제하고 checkbox 전체선택을 포함한 선택 내보내기 버튼 하나만 남긴다. 사용자가 같은 의미의 버튼 두 개를 보지 않아야 한다.
- 대상 사용자·역할: 각 page를 조회할 수 있는 모든 인증 사용자. 신규 permission 없음.
- 정상 흐름: row/card checkbox 선택(또는 전체선택) → 단일 `선택 Excel 내보내기` 실행 → 선택한 row만 포함한 `.xlsx` 다운로드.
- 예외·복구 흐름: 0건 선택 disabled, scope 밖·stale 혼입 시 전부-or-전무 generic 422와 "새로고침 후 재선택" 안내, 동시 생성 포화 429.
- 확정한 정책과 명시적 제외: 기존 전체(필터) export action UI 0개, isolated synthetic data만 사용, local experiment commit까지만 승인(main merge 승인 0/3).
- planning으로 넘긴 비차단 항목: interview의 8개 항목(전 page 판정, 전체선택 의미, 공통 UX, stable key·validation, 기존 GET endpoint 처리, audit kind 방식, 선택 상한, screenshot 조합)은 사용자 standing rule에 따라 본 문서의 권장안으로 확정한다(16장).

Interview 문서에 없는 사용자 답변을 추측하지 않았다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

반복 가능한 업무 데이터가 있는 모든 page에서 사용자가 checkbox(전체선택 포함)로 항목을 고르고, 단 하나의 `선택 Excel 내보내기` action으로 선택한 항목만 담긴 안전한 `.xlsx`를 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 공통 Excel 기반(`TASK-EXPORT-001` Phase 1)은 프로젝트 목록·구매 dashboard·내 업무 3개 화면의 "현재 필터 전체 export"(GET)만 제공하고, `TASK-EXPORT-002`가 프로젝트 목록에만 선택 export(POST)를 추가했다.
- 그 결과 프로젝트 page는 같은 목적의 export action이 2개 보이고, 생산관리·자재·키팅·제조·품질·물류·Pending·알림 등 나머지 업무 page는 Excel 내보내기가 전혀 없다.
- 사용자는 필요한 행만 고른 파일을 원하며, 전체 export와 선택 export가 병존하면 어떤 버튼을 눌러야 할지 혼동하고 의도치 않은 대량 파일이 만들어진다.
- 이 기능이 없으면 화면 캡처·수기 전사로 업무 데이터를 옮기게 되어 누락·오타와 시간 손실이 발생한다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 각 page 조회 권한 보유 사용자 | row 선택과 선택 export 실행 | 해당 page list/queue endpoint와 동일한 permission·scope | 없음 (export는 read-only) |
| 매출 열 열람자 | 프로젝트 계열 export에 매출 열 포함 | `Project.SalesAmount.Read` 보유 시에만 열 존재 | 없음 |
| System Administrator | 일반 사용자와 동일한 read 정책 | scope 전체 조회 가능 범위 | 없음 (mutation 우회 없음) |

- 신규 permission을 만들지 않는다. 각 selected export endpoint는 대응하는 list/queue endpoint와 정확히 같은 permission·scope·soft-delete/visibility 조건을 재사용한다. 프로젝트·구매 계열은 `ProjectRead`+`ProjectAccessScope`, 내 업무·알림은 인증 사용자 본인 고정이 이미 확인된 사실이며, 자재·키팅·제조·품질·물류·Pending queue의 정확한 permission 명칭은 구현 시 각 endpoint에서 재확인해 동일하게 재사용한다(추측 금지).
- Frontend checkbox 표시는 보조 수단이고 서버 Policy가 최종 차단 지점이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 프로젝트 목록 (단일 action)

1. 사용자가 프로젝트 목록에서 header 전체선택 또는 개별 checkbox로 프로젝트를 고른다. 기존 "현재 필터 Excel 내보내기" 버튼은 더 이상 보이지 않는다.
2. 선택 tray에 `N개 선택`이 표시되고 사용자가 `선택 Excel 내보내기`를 실행한다.
3. 시스템이 선택 ID snapshot을 서버로 보내 권한·scope를 전부-or-전무 재검증하고 선택 row만 담긴 파일을 내려준다.

### 시나리오 B — 품질 검사 (stage·group 화면)

1. 사용자가 LQC tab에서 프로젝트 group header checkbox로 한 프로젝트의 panel 전체를, 다른 프로젝트에서는 개별 panel만 고른다.
2. tray 전체선택 checkbox는 현재 stage에 로드된 selectable panel 전체를 대상으로 하며 일부 선택 시 indeterminate로 표시된다.
3. `선택 Excel 내보내기` 실행 시 현재 stage와 선택 panel ID snapshot이 전송되고, 파일에는 해당 stage의 선택 panel row만 포함된다.

### 시나리오 C — stale 선택 복구

1. 사용자가 선택한 항목이 그 사이 다른 사용자에 의해 상태 변경·삭제되어 조회 조건을 벗어난다.
2. 서버가 요청 수와 조회 수 불일치를 감지해 파일·audit 없이 generic 422를 반환한다.
3. 화면은 선택을 유지한 채 "목록을 새로고침한 뒤 다시 선택해 주세요"를 action 근처 `aria-live` 영역에 안내한다.

## 5. 기능 요구사항

### 필수

- [ ] 6장 inventory의 `선택 export 대상` 12개 page 전부에 checkbox 선택·전체선택·indeterminate·0건 disabled·단일 `선택 Excel 내보내기` action을 제공한다.
- [ ] 기존 전체(필터) export action을 UI에서 0개로 만든다(프로젝트 목록·구매 dashboard·내 업무).
- [ ] 모든 selected request를 서버가 동일 permission·scope·visibility 조건으로 전부-or-전무 재검증한다.
- [ ] 기존 workbook builder·formula-safe writer·10,000행 cap·2-slot no-wait gate·append-only audit을 재사용한다.
- [ ] 전 route/component inventory의 누락 0을 자동 검증(대상 page마다 계약 테스트 존재)으로 증명한다.

### 선택

- [ ] 선택 수가 화면에 남아 있는 동안 같은 snapshot 재실행(성공 후 선택 유지에 의한 재다운로드).

### 명시적 제외

- [ ] 관리자 list 8종 export(6장 보류 판정), column picker, multi-sheet, CSV/PDF/ZIP, async job·파일 storage, email/Teams 발송, 전 page(미로드 데이터) 대량 선택, Excel import 계약 변경, `Deleted` tab export.

## 6. 화면·UX 기획 — 전 page inventory와 판정

`pathForView`의 view kind 전수(홈, 내 업무, Teams activity 2종+목록, 프로젝트 목록/상세/panel, 정산, 편집 3종, 생산관리 3종, 자재 2종, 제조, 물류, 품질 2종, 구매 2종, 알림, Pending 2종, 관리자 11종)를 다음과 같이 분류한다. 누락 0 검증은 이 표와 실제 view kind 목록의 일치를 테스트로 고정한다.

### 6.1 선택 export 대상 (12개)

| # | Page (route) | 선택 단위·stable key | 화면 구조 | 재조회 경계(reset 기준) | 열 allowlist 방향(구현 시 확정) |
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

공통 UX 계약(모든 대상 page 동일):

- Desktop table 화면(프로젝트)은 기존 header checkbox 열을 유지한다. Group/card 화면(4~12)은 각 group header checkbox(해당 group의 loaded selectable child 전체 + indeterminate)와 선택 tray의 page 전체선택 checkbox(현재 tab/stage/filter에서 로드된 selectable row 전체 + indeterminate)를 둔다. 같은 의미의 action·checkbox를 중복 배치하지 않는다.
- 선택 tray는 기존 프로젝트 `선택 N건` tray를 공통 component로 일반화한다: `전체선택 checkbox` + `N개 선택`(`aria-live`) + `선택 Excel 내보내기` + `선택 해제`만 노출한다. 0건이면 export 비활성과 이유 표시.
- Mobile 390px: 카드 상단 충분한 touch target checkbox, 목록 바로 위 compact inline tray. fixed bottom bar 금지, page-level horizontal overflow 0, 기존 좌상단 숨김 메뉴 규칙 보존.
- checkbox control은 row/card의 열기·시작 action과 click·keyboard event를 완전히 분리한다(기존 프로젝트 계약과 동일). 각 checkbox는 row 식별 접근성 label을 가진다.
- 실행 중에는 선택 변경·전체선택·해제·재실행·필터 변경을 잠그고, 성공·실패 후 해제한다. 성공 문구 `Excel 파일 생성을 완료했습니다`, 선택 422 hint `목록을 새로고침한 뒤 다시 선택해 주세요`, 429 재시도 안내를 기존 `ExcelExportAction` 계약대로 유지한다.

### 6.2 상위 page에서 포함 (3개)

| Page | 판정 근거 |
| --- | --- |
| 내 업무 `AssignedProjects` tab | 프로젝트 row의 요약 projection. 프로젝트 목록 선택 export가 동일 데이터를 담당하므로 중복 button을 두지 않는다 |
| Teams activity `/teams/activity`(+상세 2종) | 알림의 Teams tab projection. 알림 page 선택 export가 담당 |
| 홈 `/` widget | 내 업무·Pending 등 원본 page의 요약. 각 원본 page가 담당 |

### 6.3 선택 대상 없음 (반복 선택 데이터가 없는 route)

로그인·인증 shell과 승인 대기, 프로젝트 상세(`/projects/:id` 및 section)·panel 상세, Pending 상세, 프로젝트 생성/수정·패널정보 편집·구매정보 편집·생산계획 편집의 mutation form, 영업 정산(`/projects/:id/settlement`, 프로젝트 단위 정산 workspace), 생산관리 설정, 구매 필수 항목 설정, 관리자 홈 `/admin`, 관리자 수동 알림 발송 form, 알림/배송 상세. 이 route들은 "미구현"이 아니라 반복 선택 대상이 없는 화면으로 기록한다.

### 6.4 명시적 보류 (관리자 list 8종)

`/admin/users`, `/admin/departments`, `/admin/calendar/holidays`, `/admin/permissions`, `/admin/history/master-data`, `/admin/history/work-items`, `/admin/system/notification-deliveries`(+상세), `/admin/system/work-item-escalations`.

- 근거: System Administrator 전용 화면으로 실사용 export 가치가 낮고, 개인 이메일·EntraId 계정, 기준정보 변경 이력의 before/after 원문 JSON, delivery payload snapshot 등 이번 Task의 privacy 불변조건(실제 사용자 원문·감사 원문 미출력)과 정면 충돌하는 데이터가 중심이다. 필요해지면 별도 NEW_FEATURE로 익명화·열 정책을 따로 기획한다. 휴일 관리는 이미 Excel 양식 다운로드·import를 보유한다.

## 7. 업무 규칙과 불변조건

- 파일에는 사용자가 선택한 row만 존재한다. 선택하지 않은 row 0, 부분 성공 0, per-ID 실패 원인 노출 0.
- Backend가 permission·scope·visibility·상한·열 allowlist의 authoritative layer다. Frontend 선택 상태를 신뢰하지 않는다.
- 검증 실패 시 파일·성공 audit·성공 feedback을 만들지 않는다(전부-or-전무). 실패·차단(401/403/422/429)은 audit 0건.
- 모든 자유 문자열은 text cell로 기록하고 생성 파일 재파싱 시 formula 객체 0을 유지한다.
- 내부 GUID·실제 고객/사용자 개인 식별 원문(이메일·사번)·감사 원문·description/comment 자유서술·첨부 bytes를 어떤 열에도 넣지 않는다. 화면 목록에 표시되는 식별 label·상태·일자·수량만 사용한다.
- export는 domain write 0, 알림 0, workflow 상태 전이 0. 선택 상태는 화면 로컬 임시 상태이며 서버에 저장하지 않는다.
- 기존 Excel import(프로젝트·패널정보·구매·생산계획·휴일)와 품질/제조/물류/Pending mutation·lifecycle 계약은 불변이다.
- 전체 export UI 제거 후에도 각 page의 조회·기존 업무 흐름은 그대로 동작한다(rollback 경계: 선택 기능 제거 시 원상 복귀).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `data_export_events` | append-only export 성공 audit | 기존 (`0038`·`0039`) | update/delete trigger 유지 |
| export kind allowlist | check constraint의 화면별 고정 enum | 신규 kind 추가 | additive migration으로 constraint drop/recreate만 |
| 화면별 선택 상태 | `Set<id>` 로컬 UI 상태 | 신규 (client-only) | 서버 저장·감사 없음 |

- 신규 audit kind(화면별 explicit enum, 16장 결정 6): `MyWorkSelected`, `ProductionPlanningSelected`, `ProcurementDashboardSelected`, `MaterialReceiptsSelected`, `PanelKittingSelected`, `ManufacturingSelected`, `QualityIqcSelected`, `QualityInspectionsSelected`, `LogisticsSelected`, `PendingSelected`, `NotificationsSelected`. `ProjectsSelected`는 기존 그대로 재사용한다.
- 기존 kind(`Projects`, `ProcurementDashboard`, `MyWork`)는 historical row 보존을 위해 constraint에 유지한다. `0038`·`0039`는 수정하지 않고 다음 additive migration(현재 ledger 최신 `0039` 다음 번호 — 구현 시 재확인, 예상 `0040`) 1건으로 `ck_data_export_events_kind`를 확장한다. `ck_data_export_events_sensitive_columns`의 매출 flag 허용 kind는 `Projects`/`ProjectsSelected`로 변경 없이 유지한다(신규 화면은 매출 열 없음).

```text
선택 0건 → 선택 N건 (개별/그룹/전체선택) → 실행 snapshot·잠금 → 성공(선택 유지) | 실패(선택 유지 + 복구 안내)
```

## 9. API·Backend 고려사항

- Backend authoritative 규칙: 3장 권한, 7장 불변조건 전부.
- 신규 mutation-free POST endpoint 11개(JSON body는 bounded ID 운반 목적): 각 대상 page의 `POST /api/<영역>/export/selected` 계열. body 기본형은 `{ "ids": ["<id>", ...] }`이며 품질 검사는 `{ "stage": "...", "panelIds": [...] }`, 물류는 `{ "stage": "...", "targetIds": [...] }`처럼 화면 조회 경계를 함께 받는다. 정확한 route·property 이름은 기존 `/api/projects/export/selected` 관례에 맞춰 구현 시 확정한다.
- 입력 validation은 기존 selected 계약을 공통화해 재사용한다: 항목별 trim → GUID parse → 빈/누락 body·0건·malformed·중복·상한 초과를 모두 동일한 422 validation contract(title+field error+한글 안내)로 반환. 상한은 공통 unique 1,000건(16장 결정 7).
- 전부-or-전무 재조회: 화면별 store에 기존 list/queue query 경로(동일 permission·scope·soft-delete/visibility·정렬)에 ID 집합 조건만 추가한 단일 조회를 두고, 조회 수가 요청 unique 수와 정확히 일치할 때만 workbook을 생성한다. 불일치 시 generic 422("선택한 항목 중 내보낼 수 없는 항목이 있습니다…"), 파일·audit 0건. stage 동반 화면은 stage 조건을 함께 재적용한다. 단건 반복 조회와 export 전용 별도 SQL 금지.
- 실행·응답: `ExcelExportConcurrencyGate` 2-slot no-wait 재사용(포화 429), `CancellationToken` 전파·slot 누수 0, `.xlsx` content type·UTF-8 파일명(`EMI_<화면명>선택_<timestamp>.xlsx` 계열)·`X-Export-Row-Count` 기존 계약 유지.
- 기존 GET 전체 export 3종(`/api/projects/export`, `/api/procurement/dashboard/export`, `/api/my-work/export`)은 UI와 함께 endpoint도 제거한다(16장 결정 5). Frontend가 유일한 소비자이므로 미노출 data-egress 경로를 남기지 않는다. 관련 helper·테스트는 selected 계약 테스트로 대체한다.
- audit: 성공 시에만 `DataExportAuditStore.AppendSuccessAsync`로 1건, 화면별 selected kind, row count, `filtersApplied` false 고정, 매출 flag는 프로젝트 계열만.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서로 확정하지 않는다(위 명칭은 확인된 기존 코드 기준이며, 신규 명칭은 관례 표시다).

## 10. Frontend 고려사항

- 공통화: 프로젝트에 구현된 선택 상태·tray·checkbox 계약을 공통 hook(`useRowSelection` 계열)과 공통 tray component로 추출하고, `ExcelExportAction`·`downloadExcelExport` POST 확장·선택 전용 422 hint·busy 전달을 그대로 재사용한다. 프로젝트 page는 공통 모듈로 이관하되 동작 계약(초기화·유지·잠금·snapshot)은 byte 수준이 아니라 계약 수준에서 동일해야 하며 기존 테스트로 회귀를 고정한다.
- 각 대상 page: loading/empty/error 상태에서는 선택 UI를 렌더하지 않고, 새 response 수락 시 visible ID 밖 선택 값을 항상 제거한다. group header checkbox는 selectable child만 대상으로 한다(예: 키팅의 `selectable=false` panel 제외).
- 기존 전체 export 버튼 3개 제거: 프로젝트 목록, 구매 dashboard, 내 업무의 `ExcelExportAction`(GET) 사용처를 제거하고 선택 tray로 대체한다.
- 접근성: checkbox label에 row 식별 정보(표시 코드 기반), 선택 수 `aria-live`, keyboard 선택과 row 열기 분리, focus 순서 유지.
- 390px/Teams narrow: inline tray·카드 checkbox·page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 각 page의 조회·시작/완료·읽음 처리 흐름은 불변. 선택 UI는 조회 위에만 얹힌다.
- 권한/관리자: 신규 permission 0. 관리자 list export는 명시적 보류(6.4).
- Excel/PDF/첨부: 기존 import 5계열과 IQC/검사 PDF 계약 불변. workbook builder·formula-safe writer 재사용.
- Teams/Mail: 영향 없음(발송 0).
- 삭제·복구/감사: soft-delete 제외 조건 재사용, `data_export_events` append-only 유지, `Deleted` tab 제외 유지.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 화면별 explicit endpoint + explicit adapter(열 allowlist·store 재조회) + 화면별 audit kind | 기존 `ProjectsSelected` 패턴과 완전 일치, 화면별 permission·scope를 각자 정확히 재사용, constraint가 kind를 자동 검증 | endpoint·adapter 수가 많음(11개). 공통 validation·gate·builder 재사용으로 중복 최소화 |
| B | generic 단일 `POST /api/export/selected` + screen key 파라미터 + generic audit kind | endpoint 1개 | screen key 분기 하나에 이질적 permission·scope·visibility가 모여 우회 결함 위험, audit allowlist 의미 약화, 기존 계약과 불일치 |

- 권장 근거: 이번 Task의 핵심 위험은 "화면별 권한·scope 재검증 누락"이며, A는 각 endpoint가 해당 화면의 기존 검사 코드를 직접 재사용하므로 구조적으로 안전하다. B는 보류한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용, 신규 migration도 Persistent UAT 미적용.
- migration 필요 여부: audit kind constraint 확장 additive 1건만(예상 `0040`, ledger로 재확인). `0038`·`0039` 수정·번호 재사용 금지.
- 외부 발송/실제 데이터 영향: 없음. 실제 사용자·고객·프로젝트 원문으로 export를 실행하지 않는다.
- runtime 교체 여부: 없음. canonical runtime·5174 handover 불포함.
- 추가 사용자 승인 필요 작업: push·PR·merge(미승인), main merge(승인 0/3), Persistent UAT·실제 provider. 이 문서는 어떤 게시 승인도 부여하지 않는다.

## 14. 검증 계획

- Backend: 화면별 계약 테스트 — 권한 없음 403·미인증 401, 입력 validation 422 동일 계약(빈/0건/malformed/중복/1,001건), scope 밖·삭제·미존재·stage 불일치 혼입 각각 generic 422+파일·audit 0건, 성공 시 선택 row만 존재·화면 정렬 동일, 매출 열 gate(프로젝트 계열), formula 재파싱 0, 429 포화·slot 누수·취소 전파, 화면별 audit kind·row count 기록, append-only trigger, migration fresh/upgrade(기존 isolated DB) 검증, 제거된 GET export 3종의 404 확인, 기존 목록·queue·import 회귀.
- Frontend: 공통 선택 hook/tray unit(개별·그룹·전체선택·indeterminate·0건 disabled·초기화 경계·visible 밖 제거·성공/실패 유지·실행 잠금·snapshot), 페이지별 checkbox와 row action 분리(mouse·keyboard), 전체 export 버튼 부재 확인(프로젝트 목록 action 1개), 문구 계약, lint·typecheck·build.
- E2E(isolated Full-Stack): 최소 프로젝트·품질 검사·자재 입고 3개 시나리오에서 일부 선택 → 다운로드 파일 재파싱으로 선택 row만 존재·formula 0 확인. desktop·390px page-level horizontal overflow 0.
- Inventory 누락 0: 6장 분류표와 실제 view kind 전수의 일치를 테스트 또는 검증 checklist로 고정한다.
- 증빙: 대표 page desktop 1440·mobile 390 screenshot(16장 결정 8 조합)과 실제 Microsoft Excel로 연 생성 workbook screenshot을 synthetic data로 수집하고, 확인한 workbook을 닫았음을 기록한다.
- 사용자 검수: 자동 검증 완료 후 `사용자 검수 대기`로 종료한다. 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 12개 대상 page 전부에서 선택 export가 동작하고, 전체 export UI 0개·부분 성공 0·scope 우회 0·formula 0·내부 GUID/민감 원문 0·domain write 0.
- UX: desktop·390px에서 checkbox·전체선택·indeterminate·잠금·복구 안내가 동작하고 같은 의미의 button이 화면당 1개다.
- 자동 테스트: 14장 계약 전부 통과, 기존 회귀 0.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `대기`.
- PR 상태: 없음(local experiment commit까지만).

중단 조건: 권한·scope 우회, 부분 성공 파일, 선택 외 row 포함, per-ID 원인 노출, formula 미방어, migration ledger 충돌, 기존 import·업무 mutation 회귀, Repository 계약 충돌 발견 시 구현을 중단하고 blocking으로 보고한다.

## 16. 미결정 사항 — standing rule로 채택한 비차단 결정

사용자 standing experiment 규칙(권장안 자동 채택)에 따라 다음을 확정한다. 별도 사용자 확인이 필요한 blocking 결정은 0건이다.

| 번호 | 항목 | 채택한 권장안 | 근거 |
| ---: | --- | --- | --- |
| 1 | 전 page 판정 | 6장 inventory: 대상 12 / 상위 포함 3 / 선택 대상 없음 다수 / 관리자 8종 명시적 보류 | 실제 route·data model 전수 대조, privacy 불변조건 |
| 2 | 전체선택 의미·lifecycle | 현재 tab/stage/filter에서 로드된 selectable row 전체. group checkbox는 group 한정. 필터·tab·stage·reload·새 response에서 초기화, 성공·실패 후 유지, 명시적 해제 | 보이는 것과 파일의 일치, 기존 `TASK-EXPORT-002` 확정 계약 일반화 |
| 3 | 공통 UX 계약 | 공통 선택 hook + inline tray(전체선택·N건·실행·해제) + `ExcelExportAction` 재사용, mobile inline tray | 중복 구현 방지, 검증된 기존 계약 |
| 4 | stable key·검증 | 화면별 기존 GUID key(6.1 표), 서버 단일 query 전부-or-전무 exact count, stage 동반 화면은 stage 재적용 | 정보 누출 차단·fail-closed |
| 5 | 기존 GET 전체 export | UI와 endpoint 모두 제거, audit historical kind는 constraint에 유지 | 사용자 요구는 중복 0. Frontend가 유일 소비자로 미노출 egress 경로를 남길 이유 없음. rollback은 branch 단위로 가능 |
| 6 | audit kind 방식 | 화면별 explicit selected kind enum + additive constraint 확장 | 기존 패턴 일치, DB가 allowlist를 강제 |
| 7 | 선택 상한 | 전 화면 공통 unique 1,000건/request(프로젝트 100→1,000 통일), workbook 10,000행 cap 유지, grouped parent는 자체 row 없이 children만 | queue 화면의 현실적 전체선택 규모 수용, bounded body(<40KB), 단일 상한으로 UX 일관 |
| 8 | screenshot 조합 | 프로젝트(단일 action 확인)·품질 검사(stage+group)·자재 입고(중첩 item)·물류(target 혼합)의 desktop 1440·390px + 프로젝트·품질 선택 workbook의 실제 Excel 확인·close | 서로 다른 화면 구조 대표성 |

userDecisionRequired: 0건.

## 17. 예상 변경 범위 (확정 allowlist가 아니라 조사 대상)

- Backend: `DataExports` 모듈(service·endpoint·validation 공통화), 화면별 store의 선택 재조회 overload(프로젝트·구매·workflow·자재·키팅·제조·품질·물류·Pending·알림), 기존 GET export 3종 제거.
- Frontend: 공통 선택 hook/tray, `App.tsx` 각 page와 분리된 page component 5종(자재·키팅·제조·품질·물류)·Pending page, `api.ts` selected export helper 11종·GET helper 3종 제거.
- DB/Migration: additive constraint 확장 1건(예상 `0040`).
- Tests/Scripts: Backend 계약·migration 테스트, Frontend unit, isolated Full-Stack E2E, screenshot 수집.
- Docs: Roadmap `TASK-EXPORT-001`/`TASK-EXPORT-002` 실험 상태 갱신(진행 시점), 후속 SOP·user manual.

## 18. Roadmap 연결

- 선행 Task: `TASK-EXPORT-001` Phase 1(공통 기반), `TASK-EXPORT-002`(선택 UX 선행 구현 — 본 변경이 일반화해 흡수하되 판단 이력·commit 보존).
- 후속 Task: 관리자 list export(보류, 필요 시 별도 NEW_FEATURE), column picker·multi-sheet(Deferred), 서버측 "필터 결과 전체 선택"(별도 Task).
- 현재 Go/No-Go: experiment fast-track 한정 Go(사용자 직접 지시·명시적 순서 override 기록). canonical 실행 큐의 다음 `TASK-007A` Gate와 4.3/4.3A 기록 원칙은 변경하지 않는다.
- 별도 Task로 분리할 항목: 위 후속 항목 전부.

## 19. Codex 구현 지시문 초안

1. 공통 selected request validation(trim→GUID parse→중복/0건/1,001건 422)과 공통 응답 변환을 `DataExports`에 추출하고 기존 프로젝트 selected 경로를 이관, 회귀 고정.
2. additive migration(예상 `0040`)으로 신규 selected kind 11종을 constraint에 추가하고 migration ledger·fresh/upgrade·append-only 테스트를 확장.
3. 화면별 store 선택 재조회(기존 list/queue query + ID 집합 + exact count)와 adapter 열 allowlist를 6.1 표 순서(내 업무→생산관리→구매→자재→키팅→제조→IQC→검사→물류→Pending→알림)로 추가하며 화면마다 권한·scope·422·audit 계약 테스트를 함께 고정.
4. Frontend 공통 선택 hook·tray를 추출해 프로젝트 page를 이관하고 GET 전체 export 버튼·helper를 제거.
5. 나머지 11개 page에 선택 UX·selected export를 배치(stage/group 화면은 group checkbox 포함), page별 unit 테스트 작성.
6. 기존 GET export endpoint 3종 제거와 404 확인, 기존 import·queue 회귀 실행.
7. isolated Full-Stack E2E 3종, inventory 누락 0 검증, desktop·390px screenshot과 실제 Excel workbook screenshot 수집·workbook close 기록.
8. implementation report·5종 산출물 상태·Roadmap 실험 상태를 갱신하고 local experiment commit까지만 수행한다(push·PR·merge·Persistent UAT·provider 금지).

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
