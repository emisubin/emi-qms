# TASK-EXPORT-001 Change 002 — 전 페이지 선택 Excel 1차 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/export-001-all-pages-planning.md`
- sourcePlanningModified: false
- reviewRound: 1
- reviewScope: 사용자 요구·전 route 누락·권한/scope·선택 UX·기존 API 호환·검증 증빙
- openBlockingDecisionCountAfterResolution: 0

## 1. 총평

Fable 1차안의 핵심인 `모든 반복 업무 page의 checkbox 선택 + 전체선택 + 단일 선택 Excel action`, 기존 `ProjectsSelected`의 request snapshot·전부-or-전무 재조회·formula-safe workbook·audit 일반화는 사용자 문제를 정확히 해결한다. primary 업무 page 12개를 실제 route와 store 경계로 분류하고 group/stage 화면의 전체선택 의미를 “현재 loaded/filtered selectable child”로 고정한 방향도 유지할 가치가 높다.

다만 1차안은 “모든 page”라는 사용자 직접 지시보다 범위를 좁혀 관리자 목록 8개를 보류했고, privacy-safe evidence 정책을 제품의 authorized export 데이터와 혼동했다. 또한 사용자는 중복 button 삭제를 요청했지만 기존 GET API endpoint까지 제거하도록 확장해 외부 소비자·회귀 가능성을 불필요하게 만든다. page별 endpoint 19개에 validation·audit·파일 응답을 반복하면 구현 누락 위험도 크므로, 공통 orchestration과 화면별 authoritative adapter의 경계를 더 명확히 해야 한다. 아래 resolution을 2차 기획에 반영하면 blocking decision은 0이다.

## 2. 기능 판정

### 유지

- primary 업무 page 12개: 프로젝트, 내 업무, 생산관리, 구매, 자재 입고, 키팅, 제조, IQC, panel 품질검사, 물류, Pending, 알림
- checkbox 개별선택·group 전체선택·page 전체선택·indeterminate·0건 disabled·실행 snapshot·진행 중 잠금
- 현재 tab/stage/filter에서 로드된 selectable row만 전체선택 대상으로 삼고 label로 범위를 명시
- 선택 성공·실패 뒤 선택 유지, 필터·tab·stage·reload·새 response에서 stale 선택 제거
- Backend가 같은 permission·scope·visibility·stage를 재적용하고 exact count가 다르면 generic 422·파일/audit 0건
- 화면별 explicit allowlist, 내부 ID·description/comment·첨부 bytes 제외, formula 0, 2-slot gate, append-only audit
- home·Teams projection·단일 detail·create/edit mutation form은 원본 list에서 담당하거나 반복 선택 대상 없음으로 분류
- mobile fixed bottom 금지, 목록 위 compact inline tray와 카드 checkbox

### 추가

1. **관리자 반복 목록 8개를 포함한다.**
   - `/admin/users`, `/admin/departments`, `/admin/calendar/holidays`, `/admin/permissions`, `/admin/history/master-data`, `/admin/history/work-items`, `/admin/system/notification-deliveries`, `/admin/system/work-item-escalations`를 선택 export 대상에 포함한다.
   - 사용자 관리: 표시명·업무 이메일은 해당 관리자 화면에서 이미 권한으로 조회되는 업무 필드이므로 `users.manage`/기존 admin access를 서버에서 재검증해 allowlist로 출력할 수 있다. Entra object/tenant id·내부 user id·원본 claim은 제외한다.
   - 기준정보 변경 이력: entity 표시·action·actor 표시명·시각·사유의 bounded summary만 포함하고 before/after JSON 원문은 제외한다.
   - 업무 이력·delivery·escalation: 화면 목록의 상태·channel/type·project 표시·stage·시각·count만 포함하고 payload snapshot·recipient address·오류 원문·deep link·내부 id는 제외한다.
   - 권한 matrix: 선택 단위는 permission row이며 각 role의 허용 여부를 typed boolean/표시 label column으로 flatten한다. 사용자 계정 원문은 없다.
   - 휴일: 기존 Excel import/template와 충돌하지 않는 read-only 선택 export를 별도 action으로 제공한다.

2. **page inventory를 20개 대상 page로 고정한다.**
   - primary 12 + admin 8. 관리자 delivery detail은 목록 export가 담당하고 detail 자체는 선택 대상 없음이다.
   - `pathForView`/`View` union 전수를 machine-readable inventory test에 고정해 새 list page 추가 시 미분류 실패가 나게 한다.

3. **공통 selected export orchestration을 추출한다.**
   - 공통 body validation(빈/형식/중복/상한), concurrency gate, workbook 생성, audit append, filename/response mapping은 한 경로로 둔다.
   - 화면별 adapter는 `permission/scope 검증 + 기존 list/store query 재사용 + exact-match stable key + explicit columns + audit kind`만 제공한다. generic client row payload를 authoritative data로 받지 않는다.
   - endpoint route는 화면별로 유지할 수 있으나 orchestration은 중복하지 않는다.

4. **page별 screenshot 증빙을 전부 수집한다.**
   - 대표 4개만 찍는 1차안은 fast-track의 page별 screenshot standing rule과 “모두 구현” 증빙에 부족하다.
   - 대상 20개 page 모두 desktop 1440과 mobile 390에서 checkbox·전체선택·단일 action이 보이는 screenshot을 자동 수집한다. 실제 Excel UI screenshot은 구조가 다른 최소 2개 workbook(일반 업무 1, 관리자 1)을 확인하고 모두 닫는다.

5. **선택 UX에서 action 용어를 하나로 고정한다.**
   - button label은 모든 page에서 `선택 Excel 내보내기` 하나로 통일하고 `전체 내보내기`, `현재 필터 Excel`, `Excel 다운로드` 같은 병행 action은 0개다.
   - 전체선택은 button이 아니라 checkbox label `현재 목록 전체 선택` 또는 group별 `이 프로젝트 전체 선택`으로 표현해 사용자가 export action 두 개로 오해하지 않게 한다.

6. **관리자·일반 화면 selection 상한을 실제 loaded row와 결합한다.**
   - request 상한 1,000은 유지하되 Frontend는 현재 response에 존재하는 selectable row만 보낼 수 있고 Backend exact-match가 최종 검증한다.
   - server pagination이 있는 page는 현재 page의 loaded row만 전체선택 대상으로 하며 “검색 결과 전체”를 의미하지 않는다. 전체 dataset 선택은 별도 후속 기능이다.

### 보류

- column picker, multi-sheet, CSV/PDF/ZIP, 비동기 job/storage·재다운로드·정기 발송
- server-side “필터 결과 전부 선택”, 현재 로드되지 않은 pagination 전체 선택
- create/edit form draft export, 단일 detail snapshot report, Home·Teams 중복 projection 전용 action
- Persistent UAT·실제 provider·대표 repo·main·push·PR·merge

### 제거

- 관리자 목록 8종을 privacy를 이유로 통째로 제외하는 판정
- 기존 GET 전체 export endpoint 3개 삭제
- Frontend row payload·DOM을 권한 재검증 없이 workbook source로 신뢰하는 generic snapshot export
- 대표 page 4개만 screenshot으로 보고 나머지 page 구현을 증빙하지 않는 방식
- page마다 validation/gate/audit/response를 복사하는 방식

## 3. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `ALL-EXPORT-ADMIN-OMISSION` | P1 | `RESOLVED_FOR_REDRAFT` | 사용자 “모든 page” 직접 지시와 관리자 list 8종 보류가 충돌 | admin 8종을 safe allowlist·기존 admin permission으로 포함, raw identity/payload/history JSON 제외 |
| `ALL-EXPORT-API-SCOPE-EXPANSION` | P1 | `RESOLVED_FOR_REDRAFT` | button 제거 요청을 endpoint 삭제로 확대하면 호환성·rollback 경계가 불필요하게 깨짐 | GET 3종 endpoint/service/helper contract는 보존, UI 사용처만 제거. selected action만 사용자에게 노출 |
| `ALL-EXPORT-AUTHORITATIVE-ADAPTER` | P1 | `RESOLVED_FOR_REDRAFT` | 19개 화면을 generic client row payload로 처리하면 위조 data와 audit 의미 약화, 개별 복사하면 scope 누락 위험 | 공통 orchestration + 화면별 server adapter·existing query·exact count·explicit columns |
| `ALL-EXPORT-ADMIN-PRIVACY-PROJECTION` | P2 | `RESOLVED_FOR_REDRAFT` | admin row에 개인 식별·payload/history 원문이 섞여 있음 | 화면에서 필요한 bounded display field만, object/tenant/internal id·recipient·payload·before/after JSON·raw error 제외 |
| `ALL-EXPORT-INVENTORY-DRIFT` | P2 | `RESOLVED_FOR_REDRAFT` | 수동 표만으로는 새 view/list 누락을 막지 못함 | machine-readable 20-page registry와 `View`/route 분류 누락 실패 test |
| `ALL-EXPORT-SELECTION-PAGINATION` | P2 | `RESOLVED_FOR_REDRAFT` | “전체선택”이 현재 loaded page인지 전체 검색 결과인지 오해 가능 | 현재 loaded selectable row로 고정, scope label·checkbox label 명시, server-side all-results는 Deferred |
| `ALL-EXPORT-EVIDENCE-COVERAGE` | P2 | `RESOLVED_FOR_REDRAFT` | 대표 4화면 screenshot은 “모두 구현” 사용자 검수 근거가 부족 | 대상 20개 desktop/mobile screenshot + 실제 workbook 2종 Excel screenshot·close |
| `ALL-EXPORT-UI-SEMANTICS` | P3 | `RESOLVED_FOR_REDRAFT` | page별 label variation과 export/전체선택 button 혼동 가능 | 단일 `선택 Excel 내보내기` button, 전체선택은 checkbox로만 표현 |

Open P0/P1/P2는 `0/0/0`이다. 이는 2차 기획이 위 resolution을 모두 반영한다는 조건부 판정이며 아직 구현 완료가 아니다.

## 4. 최종 대상 page 20개

### 업무 page 12

1. 프로젝트 목록
2. 내 업무
3. 생산관리 프로젝트 목록
4. 구매 dashboard
5. 자재 입고
6. 키팅
7. 제조
8. 품질 IQC
9. 품질 검사
10. 물류
11. Pending
12. 알림

### 관리자 page 8

13. 사용자 관리
14. 부서 관리
15. 휴일 관리
16. 권한 matrix
17. 기준정보 변경 이력
18. 업무 이력
19. notification delivery 목록
20. 업무 escalation 목록

## 5. 권장 구현 순서

1. 공통 selected validation·selection hook/tray·export orchestration과 `ProjectsSelected` 회귀
2. additive audit migration과 20개 allowlisted kind 또는 exact screen-key constraint
3. 기존 GET export UI 사용처만 제거하고 API 회귀 보존
4. primary page 12개 adapter·selection UX·targeted test
5. admin page 8개 safe projection adapter·selection UX·privacy test
6. inventory registry test, Backend·Frontend 전체 회귀
7. isolated Full-Stack 핵심 flow와 대상 20개 desktop/mobile screenshot
8. 실제 일반/admin workbook Excel 시각 확인·close, 종료 산출물·local commit

## 6. 2차 기획 판정

Fable 2차 기획이 위 8개 resolution과 최종 20개 page inventory를 authoritative contract로 통합하고 `openBlockingDecisionCount: 0`을 반환하면 experiment branch 구현은 `GO`다. 이 판정은 대표 repo·main·Persistent UAT·provider·push·PR·merge 승인이 아니다.
