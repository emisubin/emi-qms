The baseline is complete. I now have everything needed: the confirmed interview, the roadmap's experiment fast-track approval for TASK-EXPORT-002, the TASK-EXPORT-001 export foundation (endpoints, workbook builder, concurrency gate, audit migration 0038 with a closed export-kind constraint), and the actual project list UI (desktop role="table" rows, mobile cards, single page of default 20 rows, no pagination UI). Below is the complete first planning draft artifact.

---

# TASK-EXPORT-002 — 선택 프로젝트 Excel 내보내기 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/export-002-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`
- sourceTask: `TASK-EXPORT-002`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 프로젝트 Excel export는 현재 filter 결과 전체만 내보낼 수 있어, 일부 프로젝트만 필요한 사용자는 검색 조건을 인위적으로 바꾸거나 파일에서 불필요한 행을 수동 삭제해야 한다.
- 대상 사용자·역할: 기존 `ProjectRead` 권한과 `ProjectAccessScope` 이하의 프로젝트 조회 역할 전체. 매출 열은 `Project.SalesAmount.Read` 보유자만.
- 정상 흐름: 프로젝트 목록 조회 → 여러 row/card 선택 → 선택 건수 확인 → 선택 내보내기 → Backend가 선택 집합 전체의 권한·scope·존재를 재검증 → 기존 workbook 계약으로 단일 `.xlsx` 생성 → 완료 feedback.
- 예외·복구 흐름: 선택 0건·중복/비정상 ID·상한 초과·stale/삭제/scope 밖 항목 혼입은 안정적인 한글 오류로 전체 거부하고, 실패 시 현재 선택을 유지해 수정·재시도할 수 있게 한다. 부분 성공 파일과 성공 audit을 만들지 않는다.
- 확정한 정책과 명시적 제외: 선택하지 않은 row 0, Backend 전체 집합 재검증, 부분 성공 금지, 기존 filter 전체 export 불변, synthetic isolated 검증. 다른 화면의 다중 선택, 전 page 대량 선택, 복합 multi-sheet 보고서, column picker, CSV/PDF/외부 발송, Persistent UAT·대표 repo·main·게시 제외.
- planning으로 넘긴 비차단 미결정 사항: 전체 선택 의미, filter/page 전환 시 선택 유지, 선택 상한, scope 밖·stale 처리의 오류·복구 세부, request/audit 세부 계약 — 5건. standing rule에 따라 아래 권장안을 자동 채택 대상으로 제시한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

사용자가 프로젝트 목록에서 필요한 프로젝트 여러 건을 직접 체크하고, 정확히 그 프로젝트만 포함된 단일 `.xlsx` 파일을 기존 권한·안전 계약 그대로 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 `TASK-EXPORT-001` Phase 1의 프로젝트 목록 export로 현재 검색·상태·납기 filter 결과 전체를 내려받는다.
- 부분 보고·인계처럼 특정 프로젝트 몇 건만 필요한 경우, 검색 조건으로는 정확한 subset을 만들 수 없어 파일에서 행을 수동 삭제하거나 프로젝트를 한 건씩 검색해 파일 여러 개를 만든다.
- 이 수작업은 잘못된 행이 남거나 필요한 행이 빠지는 실수를 만들고, 매번 반복된다.
- 이 기능이 없으면 "선택한 프로젝트만 정확히 한 파일로"라는 요구를 시스템이 보장하지 못하고 사용자 수작업 검증에 의존하게 된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 프로젝트 조회 역할 | 목록에서 여러 건 선택 후 선택분만 export | 기존 `ProjectRead` + `ProjectAccessScope` 이하 | 업무 데이터 변경 없음 |
| 매출 조회 권한 역할 | 선택 export에 매출액·통화 열 포함 | `Project.SalesAmount.Read` 보유 시에만 열 존재 | 없음 |
| 조회 전용/제한 scope 역할 | 자신에게 보이는 프로젝트만 선택·export | 기존 scope 이하. scope 밖 ID가 섞이면 전체 거부 | 없음 |

신규 permission은 만들지 않는다. 선택 export는 기존 filter export와 동일하게 `ProjectRead` + `GetProjectAccessScope` + `CanReadSalesAmount`를 재사용하고, Frontend 표시 여부와 무관하게 서버 Policy가 최종 차단 지점이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — desktop 다중 선택 내보내기

1. 사용자가 프로젝트 목록에서 검색·상태 tab으로 대상을 좁힌 뒤 row 앞의 checkbox로 프로젝트 2건 이상을 선택한다.
2. 화면이 `선택 N건`을 표시하고 `선택 내보내기` 버튼이 활성화된다.
3. 실행하면 Backend가 선택 집합 전체의 권한·scope·존재를 재검증한 뒤 선택한 프로젝트만 포함한 `.xlsx`를 생성하고, 기존과 같은 action-near feedback으로 `Excel 파일 생성을 완료했습니다`를 표시한다.
4. 파일에는 선택한 프로젝트 행만, 기존 목록과 동일한 정렬·컬럼·표시값으로 존재한다.

### 시나리오 B — 모바일(390px) card 선택

1. 사용자가 현장 프로젝트 card 목록에서 각 card의 선택 control을 눌러 필요한 프로젝트를 고른다.
2. 화면 하단/상단의 compact 선택 요약이 `선택 N건`과 `선택 내보내기`, `선택 해제`를 제공한다.
3. 실행·결과 feedback은 desktop과 동일한 계약을 따르며 page-level horizontal overflow가 없다.

### 시나리오 C — 실패와 복구

1. 사용자가 선택한 프로젝트 중 하나가 그 사이 삭제되었거나 scope 밖이면, Backend는 파일과 성공 audit을 만들지 않고 422와 "선택한 프로젝트 중 내보낼 수 없는 항목이 있습니다. 목록을 새로고침한 뒤 다시 선택해 주세요." 계열의 안정적인 한글 안내를 반환한다.
2. 화면은 현재 선택을 유지한 채 오류와 다음 행동을 `aria-live` 영역에 표시한다.
3. 사용자가 목록을 새로고침하면 선택은 초기화되고, 현재 보이는 항목에서 다시 선택해 재시도한다.

## 5. 기능 요구사항

### 필수

- [ ] 프로젝트 목록 desktop table row별 checkbox와 "보이는 목록 전체 선택" header checkbox(전체/일부 선택의 indeterminate 표시 포함)
- [ ] 모바일 card별 선택 control과 compact 선택 요약·실행 UI
- [ ] `선택 N건` 실시간 카운트와 0건 시 실행 비활성 + 이유 안내
- [ ] 선택 subset 전용 Backend endpoint: 선택 ID 집합 전체의 `ProjectRead`·scope·존재(비삭제) 재검증 후 전체 성공 시에만 파일 생성
- [ ] 기존 `ExcelWorkbookBuilder`·`ProjectColumns`(매출 permission 열 omission 포함)·formula-safe text·2-slot concurrency gate·`X-Export-Row-Count`·UTF-8 파일명 재사용
- [ ] 선택 0건·중복/비정상 ID·상한 초과·stale/삭제/scope 밖 혼입에 대한 422 한글 오류와 부분 성공 금지
- [ ] 성공 시 append-only export audit 1건(선택 export임을 식별 가능한 kind)
- [ ] 기존 filter 전체 export·목록 조회·Excel import 계약 불변

### 선택

- [ ] 선택된 row/card의 시각적 강조(선택 상태 배경)
- [ ] `선택 해제` 일괄 action(모바일 요약 bar에는 필수, desktop은 header checkbox로 대체 가능)

### 명시적 제외

- [ ] 프로젝트 이외 화면(구매·내 업무 등)의 다중 선택 export
- [ ] 모든 filter 결과를 전 page에 걸쳐 선택하는 대량 selection job
- [ ] 선택 프로젝트의 상세·패널·구매 데이터를 묶는 복합 multi-sheet 보고서
- [ ] column picker, CSV·PDF·ZIP, email/Teams 발송, 외부 저장
- [ ] `Deleted`(삭제 보관함) tab의 선택·export
- [ ] Persistent UAT·대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 목록 desktop | 기존 프로젝트 목록 (Deleted 제외 tab) | row별 checkbox, header 전체 선택, `선택 N건` 카운트 | 개별/전체 선택·해제, `선택 내보내기` 실행 | 기존 export action과 동일한 action-near `aria-live` 안내: 생성 완료/0건 불가/422/429/오류 |
| 프로젝트 목록 모바일 390px | 현장 프로젝트 card 목록 | card별 선택 control, compact 선택 요약 bar | card 선택·해제, 요약 bar에서 실행·일괄 해제 | 동일 문구·동일 `aria-live` 계약, overflow 0 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — `선택 N건`이 선택·해제 즉시 갱신되고, 전체 선택이 "현재 화면에 보이는 목록"임을 label로 명시한다.
- 다음 행동이 명확한가 — 0건이면 실행 버튼 비활성과 "프로젝트를 먼저 선택해 주세요" 안내, 실패 시 새로고침·재선택 안내를 제공한다.
- 저장·변경 결과가 action 근처에 보이는가 — 기존 `ExcelExportAction` feedback 위치·문구 계약(`Excel 파일 생성을 완료했습니다`)을 재사용한다.
- 권한 부족·오류 상태가 명확한가 — 403/422/429를 구분된 한글 안내로 표시하고 내부 식별자·원인 세부(어떤 항목이 scope 밖인지)는 노출하지 않는다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 모바일은 PC table 축소판이 아니라 card 선택 + 요약 bar로 설계하고, checkbox touch target·keyboard 접근·label·focus 순서를 유지한다. row 본문 클릭은 기존대로 상세 이동이며 선택 control과 이벤트가 충돌하지 않게 분리한다.

## 7. 업무 규칙과 불변조건

- 파일에는 사용자가 선택한 프로젝트만 존재한다. 선택하지 않은 row 0, 선택했지만 검증 실패한 상태의 부분 포함 0.
- Backend가 선택 집합 전체에 대해 permission·`ProjectAccessScope`·비삭제 존재를 재검증하는 authoritative layer다. Frontend 선택 상태를 신뢰하지 않는다.
- 검증 실패 시 파일·성공 audit·성공 feedback을 만들지 않는다(전체 fail-closed).
- 기존 filter 전체 export, 프로젝트 목록 조회, Excel import(template/preview/apply) 계약은 불변이다. 선택 UI·subset route를 제거해도 기존 기능은 그대로 동작한다(rollback 경계).
- 매출액·통화 열은 `Project.SalesAmount.Read` 보유 시에만 열 자체가 존재한다.
- 문자열은 모두 text cell로 기록하고 재파싱 시 formula 객체 0개를 유지한다.
- export는 domain write 0, 알림 0, workflow 상태 전이 0이다. 선택 상태는 화면 로컬의 임시 UI 상태이며 서버에 저장하지 않는다.
- audit에 선택한 project ID 목록·검색어 원문·고객/프로젝트명·파일 bytes를 저장하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| client selection state | 화면 로컬 `Set<projectId>` 임시 상태 | 신규 (UI 한정) | 저장·감사 없음. filter/tab/검색/재조회 시 초기화 |
| selected-project export request | bounded ID 목록 요청 (1~상한) | 신규 (transient) | 서버 미저장. 검증 실패 시 흔적 없음 |
| `data_export_events` | 기존 append-only export audit | 기존 + kind 확장 | 성공 1건당 1 row. 선택 export 식별 가능한 kind, row count, 매출 열 포함 여부만 |
| 프로젝트 domain 데이터 | 조회 전용 | 기존 | 상태 전이·변경 없음 |

```text
선택 0건(비활성) → 선택 N건(실행 가능) → 생성 중(중복 차단)
  → 성공(파일 + audit 1건, 선택 유지 여부는 UI 정책상 유지)
  → 실패 422/429(파일·audit 0건, 선택 유지 + 복구 안내)
목록 재조회/filter·tab 변경 → 선택 초기화
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 선택 집합 전체의 permission·scope·존재 재검증, 상한, 매출 열 gate, formula safety, resource fence, audit.
- 필요한 조회와 mutation: 신규 endpoint 1개 — `POST /api/projects/export/selected`(JSON body `{ "projectIds": [...] }`). domain mutation은 없으며 POST는 bounded request body 운반 목적이다(권장안, 12장 비교). 조회는 기존 `ProjectStore.ListProjectsAsync` private 공통 경로에 ID 집합 조건(`projects.id = any(...)` + 기존 `deleted_at_utc is null` + scope where)을 추가한 export용 variant를 재사용한다. 정렬·컬럼·scope 구성은 기존 목록과 동일 SQL 조각을 공유하고 export 전용 별도 SQL을 새로 작성하지 않는다.
- 권한·validation: `ProjectRead` 없으면 403, 인증 사용자 식별 실패 401. body가 비었거나 0건, GUID 형식 오류, 중복 ID, 상한(권장 100) 초과는 422 + 한글 안내. 조회 결과 건수가 요청 distinct 건수와 다르면(삭제·scope 밖·미존재 혼입) 어떤 항목이 문제인지 구분해 노출하지 않고 전체 422로 거부한다.
- transaction·동시성·idempotency: 기존 `ExcelExportConcurrencyGate` 2-slot no-wait 재사용, 포화 시 429. 조회~생성 구간 `CancellationToken` 전파와 slot 누수 없음 유지. 반복 실행해도 업무 데이터가 바뀌지 않는다.
- audit trail: 기존 `DataExportAuditStore.AppendSuccessAsync` 재사용. 신규 export kind(예: `ProjectsSelected`)를 추가하고, migration `0038`의 `ck_data_export_events_kind`·`ck_data_export_events_sensitive_columns` check 제약이 신규 kind를 거부하므로 additive migration(현재 계보 최신 `0038` 다음 번호, 구현 시 ledger 재확인 — 예상 `0039`)으로 두 제약을 확장한다. `0038` 파일 자체는 수정하지 않는다. filter summary 행에는 검색어 대신 `선택 N건`만 bounded 표기한다.
- 외부 provider 영향: 없음.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다(위 명칭은 현재 코드에서 확인한 재사용 대상이다).

## 10. Frontend 고려사항

- route/component: 신규 route 없음. 프로젝트 목록 페이지 component에 선택 상태(`Set<string>`)를 추가하고 `ProjectListDesktop`/`ProjectListMobile`에 선택 UI를 확장한다. `api.ts`에 `exportSelectedProjectsExcel`을 추가하고 기존 `downloadExcelExport`를 POST body를 지원하도록 확장한다(`fetchWithAuth`는 이미 `RequestInit`을 받는다). 실행 UI는 기존 `ExcelExportAction`을 disabled/label 확장으로 재사용하는 방안을 우선 조사하고, 계약이 어긋나면 최소 sibling component로 분리한다.
- loading/empty/error/success: 생성 중 버튼 비활성·중복 차단, 성공 `Excel 파일 생성을 완료했습니다`(0건 파일 경로는 선택 export에서는 발생하지 않음 — 0건 선택 자체가 차단됨), 422/429/일반 오류를 기존 문구 계약대로 구분 표시.
- 공통 Action Feedback: action 근처 `aria-live` 유지, 실패 시 선택 유지 + 다음 행동 안내.
- 접근성: checkbox마다 프로젝트를 식별하는 label(예: PJT Code 기반), header 전체 선택 label, 선택 카운트 `aria-live`, keyboard로 선택·실행 가능, 기존 row Enter/Space 상세 이동과 충돌 없는 focus 설계.
- 390px/mobile/narrow pane: card 선택 control + compact 선택 요약 bar, page-level horizontal overflow 0, 기존 좌상단 숨김 메뉴·mobile header 규칙 보존. PC table 축소판 금지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 목록 조회·상세 이동·KPI·병목 badge 동작 불변. 알림 생성 없음.
- 권한/관리자: 기존 `ProjectRead`·scope·매출 permission 재사용. 신규 permission·관리자 화면 없음.
- Excel/PDF/첨부: `TASK-EXPORT-001`의 workbook builder·formula safety·파일명·`X-Export-Row-Count` 계약을 그대로 재사용. 기존 filter export 3종(프로젝트·구매·내 업무)과 Excel import 불변.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: Deleted tab은 선택·export 제외. `data_export_events` append-only 의미를 유지하며 선택 export를 별도 kind로 식별한다. 사용자 purge 시 `actor_user_id` 참조 차단 로직은 기존 그대로 적용된다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 전체 선택 = 현재 화면에 보이는 목록(현재 로드된 page, 기본 20건) | header checkbox가 로드된 row만 선택 | 사용자가 보는 것과 선택이 항상 일치, stale·숨은 선택 0, 상한 자연 충족 | 여러 검색 결과를 합쳐 선택할 수 없음(명시적 제외 범위와 일치) |
| B. 전체 선택 = 모든 filter 결과 | 서버 기준 전체 선택 | 대량 작업 편의 | 보이지 않는 행 포함 위험, 전 page 대량 선택은 interview에서 명시적 제외 |
| C. GET + query string ID 전달 | 기존 GET export와 대칭 | 기존 패턴 유사 | GUID 수십 건이면 URL 길이 한계·proxy 위험, ID가 URL·로그에 노출 |
| D. POST + JSON body ID 전달 | bounded body로 선택 집합 전달 | 길이 안전, URL/서버 로그에 ID 미노출, 상한 검증 명확 | 조회성 POST라는 관례 예외(문서로 명시하면 무해) |
| E. audit를 기존 `Projects` kind로 재사용 | migration 없음 | 최소 변경 | filter export와 선택 export를 구분 못해 audit 의미가 흐려지고, 제약상 매출 열 flag는 호환되나 "선택 사용 여부" 추적이라는 interview 요구를 충족 못함 |
| F. 신규 kind `ProjectsSelected` + additive 제약 확장 migration | check 제약 2건을 신규 번호 migration으로 확장 | audit 의미 정확, append-only·기존 파일 불변 | migration 1건 추가 |

권장안: A + D + F, 그리고 선택 유지는 "filter·검색·tab 변경과 목록 재조회 시 초기화, export 실패 시에는 유지". 근거 — 이 기능의 핵심 가치가 "보이는 것과 파일의 정확한 일치"이므로 숨은 선택을 만들지 않는 bounded v1이 안전하고, POST body는 URL 노출·길이 위험을 제거하며, 신규 kind는 append-only audit의 의미를 참으로 유지한다. standing rule에 따라 자동 채택 대상이며 16장에 기록한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용한다.
- migration 필요 여부: additive 1건(export kind 제약 확장, 예상 `0039` — 구현 시 ledger 재확인). `0038` 파일 수정·번호 재사용 금지. fresh DB와 기존 isolated DB 모두 검증. Persistent UAT 미적용.
- 외부 발송/실제 데이터 영향: 없음. 실제 고객·프로젝트 원문으로 export를 실행하지 않는다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge(main merge 승인 0/3), Persistent UAT migration/runtime handover. 이 planning은 어떤 게시 승인도 부여하지 않는다.

## 14. 검증 계획

- 최소 테스트(Backend): 403(permission 없음)/401, 빈 body·0건·GUID 오류·중복·상한 초과 422, scope 밖 혼입 422·파일 미반환·성공 audit 0, 삭제 프로젝트 혼입 422, 성공 시 선택 row만 포함(선택 외 row 0)·목록 정렬 동일, 매출 permission 유무별 열 존재/부재, workbook 재파싱 formula 0, 429 포화·slot 누수 없음, audit kind·row count 기록, append-only trigger 유지, migration fresh/기존 ledger 검증.
- 최소 테스트(Frontend): 선택 상태 unit(개별/전체 선택·해제, filter/tab 변경 초기화, 실패 시 유지), 0건 비활성, 카운트 표시, export action의 loading·중복 차단·성공/422/429 문구. lint·typecheck·build.
- 영향 영역 회귀: 기존 filter export 3종, 프로젝트 목록 조회·상세 이동, Excel import 3단계, Backend 전체 test suite와 Frontend 전체 test.
- PR/CI: local experiment commit까지만. push·PR 없음(승인 시 별도).
- 사용자 검수: isolated Full-Stack E2E에서 synthetic 프로젝트 3건 이상 중 2건 선택 → 다운로드 파일 재파싱으로 선택 2건만 존재 확인. desktop 1440·mobile 390 선택 상태 페이지 screenshot과 생성된 Excel 파일 화면 screenshot을 privacy-safe synthetic 데이터로 수집해 보고하되, 사용자 직접 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 프로젝트 2건 이상 선택 → 단일 파일에 선택 행만 존재. 권한 밖 부분 export 0, 매출 열 gate·formula 0·내부 GUID 열 0·domain write 0. 기존 filter export와 import 회귀 0.
- UX: desktop·390px에서 선택→건수 확인→실행→feedback 흐름이 동작하고 overflow 0, keyboard·label·`aria-live` 유지.
- 자동 테스트: 14장의 Backend/Frontend/E2E 검증 전부 통과.
- 5종 산출물: implementation report(내부 section 포함 가능)·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: `자동 검증 완료 / 사용자 검수 대기`로 종료하고 screenshot을 첨부한다.
- PR 상태: 없음(local experiment commit만). 게시 가능 여부는 별도 승인 gate.

중단 조건: 권한·scope 우회, 부분 성공 파일, 선택 외 row 포함, formula 미방어, migration ledger 충돌, 기존 export/import 회귀 또는 Repository 계약 충돌 발견 시 구현을 중단하고 blocking으로 보고한다.

## 16. 미결정 사항

standing rule에 따라 아래 권장안은 2차 기획에서 자동 채택 대상이며, 각 항목은 사용자 결정 기록 대상으로 남는다.

| 번호 | 질문 | 선택지 | 권장안과 근거 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 전체 선택 의미 | 보이는 목록만 / 모든 filter 결과 | 현재 화면에 로드된 목록만(기본 page 20건). 보이는 것과 파일의 일치가 이 기능의 목적이고 전 page 대량 선택은 명시적 제외 | 권장안 자동 채택 예정 |
| 2 | 선택 유지 | filter/page/tab 변경 시 유지 / 초기화 | 검색·filter·tab 변경과 목록 재조회 시 초기화, export 실패 시 유지. 숨은 stale 선택이 잘못된 행 포함 위험을 재생산하므로 초기화가 안전 | 권장안 자동 채택 예정 |
| 3 | 선택 상한 | 화면 page cap / 별도 상한 / 기존 10,000 | request 상한 100건(기존 목록 max page size clamp와 정렬, bounded body·즉시 검증). UI는 권장안 1·2로 자연히 그 이하 | 권장안 자동 채택 예정 |
| 4 | scope 밖·stale 처리 세부 | 허용분만 부분 export / 전체 fail | 전체 fail-closed 422(확정 불변조건과 일치). 어떤 항목이 왜 실패했는지 구분 노출하지 않고 "새로고침 후 재선택" 복구 안내만 제공 | 권장안 자동 채택 예정 |
| 5 | request/audit 세부 계약 | GET query / POST body, 기존 audit kind 재사용 / 신규 kind + additive migration | `POST` JSON body + 신규 kind `ProjectsSelected` + 제약 확장 additive migration(예상 `0039`). URL 노출·길이 위험 제거와 append-only audit 의미 보존 | 권장안 자동 채택 예정 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `DataExports`의 endpoint·service에 선택 export 경로 추가, `ProjectStore`에 ID 집합 조회 variant, `DataExportAuditStore` kind 사용 확장.
- Frontend: 프로젝트 목록 페이지 선택 상태와 `ProjectListDesktop`/`ProjectListMobile` 선택 UI, `ExcelExportAction` 최소 확장 또는 sibling, `api.ts` POST download 지원과 신규 함수, `styles.css`.
- DB/Migration: additive 1건 — `data_export_events` kind·sensitive 제약 확장(예상 `0039`).
- Tests/Scripts: Backend export/migration tests, Frontend selection unit tests, isolated Full-Stack E2E 시나리오 1건 이상, screenshot 수집.
- Docs: Roadmap `TASK-EXPORT-002` 상태 갱신(canonical 큐·Next Gate 불변), interview/planning/review/change/implementation report 계보.

## 18. Roadmap 연결

- 선행 Task: `TASK-EXPORT-001` Phase 1 공통 export 기반(현재 experiment 계보에 구현 완료, 사용자 검수 대기).
- 후속 Task: `TASK-EXPORT-001` Phase 2(잔여 화면·column picker), 다른 화면 다중 선택 export(필요 시 별도 NEW_FEATURE).
- 현재 Go/No-Go: 2026-07-18 실험 재정렬 승인으로 experiment fast-track Go. canonical 다음 Gate(`TASK-007A`)와 실행 큐는 변경하지 않는다.
- 별도 Task로 분리할 항목: 전 page 대량 선택 job, 복합 multi-sheet 보고서, 선택 export의 다른 화면 확장.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-18 | 여러 프로젝트를 선택해 내보내는 기능 확인 요청, 없으면 구현 후 페이지·Excel screenshot 보고 | 기능 부재 확인을 전제로 본 기획 작성. screenshot을 검증 산출물에 포함 |
| 2026-07-18 | standing experiment 규칙: interview 왕복·중간 승인 생략, 비차단 권장안 자동 채택, 게시 제외 | 16장 자동 채택 구조와 13장 안전 경계에 반영 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

Codex review와 2차 기획 승인 이후의 구현 세션을 위한 초안이며, 이 자체는 구현 승인이 아니다.

1. migration ledger에서 실제 최신 번호를 확인한 뒤 kind 제약 확장 additive migration을 추가하고 fresh/기존 isolated DB로 검증한다.
2. `ProjectStore`에 기존 목록 SQL 경로를 공유하는 ID 집합 조회 variant를 추가한다(scope·비삭제·정렬 동일, 별도 SQL 금지).
3. `ExcelExportService`에 선택 export 경로를 추가한다: distinct·상한 검증 → gate 획득 → 조회 → 요청 건수와 결과 건수 불일치 시 fail-closed → 기존 `ProjectColumns`로 workbook 생성 → 신규 kind audit → 파일 반환.
4. `POST /api/projects/export/selected` endpoint를 기존 permission·오류 계약(403/401/422/429)으로 연결한다.
5. Frontend에 선택 상태·checkbox/card 선택 UI·선택 요약·실행 action을 추가하고 filter/tab/재조회 초기화와 실패 시 유지를 구현한다.
6. 14장의 테스트 전부와 isolated E2E, desktop 1440·mobile 390 페이지 screenshot과 생성된 Excel 파일 screenshot을 synthetic 데이터로 수집한다.
7. 기존 filter export·import·목록 회귀를 확인하고 implementation report와 5종 산출물 상태를 기록한 뒤 local experiment commit까지만 수행한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
